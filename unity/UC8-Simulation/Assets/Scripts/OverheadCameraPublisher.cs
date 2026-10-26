using System.Collections;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;

/// <summary>
/// Overhead RGB Camera Publisher for Computer Vision (Requirement R6 & R7):
/// Mounts an overhead camera directly above the conveyor pick station,
/// captures realistic RGB frames, and publishes to ROS via ROS-TCP-Connector:
/// - Topic 1: /camera/color/image_raw/compressed (sensor_msgs/CompressedImage)
/// - Topic 2: /camera/color/image_raw (sensor_msgs/Image)
/// 
/// Supports Picture-in-Picture (PiP) viewport in bottom-right corner (Toggle with key [V]).
/// Features automatic scene injection via RuntimeInitializeOnLoadMethod.
/// Project: UC8 - Robotic Arm Conveyor Pick-and-Place with Obstacle Avoidance
/// Week: Week 07 (Milestone 2 - Computer Vision Integration)
/// </summary>
public class OverheadCameraPublisher : MonoBehaviour
{
    [Header("ROS Topic Configuration")]
    [SerializeField] private string compressedImageTopic = "/camera/color/image_raw/compressed";
    [SerializeField] private string rawImageTopic = "/camera/color/image_raw";
    [SerializeField] private string cameraFrameId = "overhead_camera_optical_frame";

    [Header("Camera Resolution & Framing")]
    [SerializeField] private int imageWidth = 640;
    [SerializeField] private int imageHeight = 480;
    [SerializeField] private float publishRateHz = 15f;
    [Range(30, 95)]
    [SerializeField] private int jpegQuality = 80;

    [Header("Mount Transform (Model 1: Upstream Inspection Station)")]
    [Tooltip("Upstream inspection mount (X=-0.65m in Unity, 65cm upstream of pick station to eliminate arm visual occlusion)")]
    [SerializeField] private Vector3 mountPosition = new Vector3(-0.65f, 1.25f, -0.60f);
    [Tooltip("Looking straight down onto the conveyor")]
    [SerializeField] private Vector3 mountEulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
    [SerializeField] private float fieldOfView = 45f;

    [Header("3D Physical Gantry Arch")]
    [SerializeField] private bool renderPhysicalGantry = true;

    [Header("GUI Preview Window")]
    [SerializeField] private bool showPreview = true;

    private ROSConnection ros;
    private Camera targetCamera;
    private RenderTexture renderTexture;
    private Texture2D texture2D;
    private float lastPublishTime = 0f;
    private uint sequenceNumber = 0;

    // Automatic runtime initialization if not manually placed in scene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureInScene()
    {
        if (FindObjectOfType<OverheadCameraPublisher>() == null)
        {
            GameObject camObj = new GameObject("OverheadCamera_CV");
            camObj.AddComponent<OverheadCameraPublisher>();
            Debug.Log("[OverheadCamera] Auto-instantiated OverheadCamera_CV with OverheadCameraPublisher component.");
        }
    }

    private void Awake()
    {
        // 1. Setup camera component
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
        {
            targetCamera = gameObject.AddComponent<Camera>();
        }

        // Apply camera spatial transform
        transform.position = mountPosition;
        transform.rotation = Quaternion.Euler(mountEulerAngles);

        targetCamera.clearFlags = CameraClearFlags.Skybox;
        targetCamera.fieldOfView = fieldOfView;
        targetCamera.nearClipPlane = 0.1f;
        targetCamera.farClipPlane = 5.0f;
        targetCamera.depth = 1; // Render after main camera

        // 2. Setup render targets
        renderTexture = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.ARGB32);
        renderTexture.antiAliasing = 2;
        targetCamera.targetTexture = renderTexture;

        texture2D = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

        // 3. Procedurally generate realistic 3D industrial gantry arch in Unity scene
        if (renderPhysicalGantry)
        {
            BuildPhysicalInspectionGantry();
        }

        Debug.Log($"[OverheadCamera] Initialized Model 1 Upstream Camera at {transform.position}, resolution {imageWidth}x{imageHeight} @ {publishRateHz}Hz");
    }

    /// <summary>
    /// Procedurally constructs a realistic industrial inspection arch/gantry over the conveyor.
    /// Provides physical realism in Unity scene while completely avoiding collision interference.
    /// </summary>
    private void BuildPhysicalInspectionGantry()
    {
        if (transform.Find("GantryArch") != null) return;

        GameObject gantryRoot = new GameObject("GantryArch");
        gantryRoot.transform.SetParent(this.transform.parent, false);

        Color postColor = new Color(0.22f, 0.24f, 0.26f); // Industrial dark matte steel
        Color beamColor = new Color(0.85f, 0.72f, 0.08f); // Safety inspection yellow
        Color camBodyColor = new Color(0.12f, 0.12f, 0.14f); // Cognex/Keyence black housing

        Shader stdShader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material postMat = new Material(stdShader) { color = postColor };
        Material beamMat = new Material(stdShader) { color = beamColor };
        Material camMat = new Material(stdShader) { color = camBodyColor };

        // Post 1 (Left flank across conveyor)
        CreateGantryPart(gantryRoot.transform, "Post_Left", new Vector3(-0.65f, 0.60f, -0.88f), new Vector3(0.06f, 1.20f, 0.06f), postMat);
        // Post 2 (Right flank across conveyor)
        CreateGantryPart(gantryRoot.transform, "Post_Right", new Vector3(-0.65f, 0.60f, -0.32f), new Vector3(0.06f, 1.20f, 0.06f), postMat);
        // Crossbeam spanning over conveyor
        CreateGantryPart(gantryRoot.transform, "Crossbeam", new Vector3(-0.65f, 1.20f, -0.60f), new Vector3(0.08f, 0.06f, 0.62f), beamMat);
        // Camera enclosure
        CreateGantryPart(gantryRoot.transform, "CameraHousing", new Vector3(-0.65f, 1.15f, -0.60f), new Vector3(0.10f, 0.06f, 0.10f), camMat);
    }

    private void CreateGantryPart(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.position = pos;
        part.transform.localScale = scale;

        // Remove collider to prevent any physics or raycast conflicts
        Collider col = part.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = part.GetComponent<Renderer>();
        if (rend != null) rend.material = mat;
    }

    private void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<CompressedImageMsg>(compressedImageTopic);
        ros.RegisterPublisher<ImageMsg>(rawImageTopic);
        Debug.Log($"[OverheadCamera] Registered ROS publishers: {compressedImageTopic} and {rawImageTopic}");
    }

    private void Update()
    {
        // Keyboard toggle for picture-in-picture preview
        if (Input.GetKeyDown(KeyCode.V))
        {
            showPreview = !showPreview;
            Debug.Log($"[OverheadCamera] Camera PiP Preview: {(showPreview ? "ENABLED" : "DISABLED")}");
        }

        // Throttle to publishRateHz
        if (Time.time - lastPublishTime < 1.0f / publishRateHz)
        {
            return;
        }

        lastPublishTime = Time.time;
        PublishCameraFrame();
    }

    private void PublishCameraFrame()
    {
        if (targetCamera == null || renderTexture == null || texture2D == null) return;

        // 1. Render camera into render texture
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = renderTexture;
        targetCamera.Render();

        // 2. Read pixels from active RenderTexture
        texture2D.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        texture2D.Apply();
        RenderTexture.active = currentRT;

        sequenceNumber++;
        float currentTime = Time.time;
        uint sec = (uint)currentTime;
        uint nanosec = (uint)((currentTime - sec) * 1e9);

        HeaderMsg header = new HeaderMsg
        {
            seq = sequenceNumber,
            stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg
            {
                sec = sec,
                nanosec = nanosec
            },
            frame_id = cameraFrameId
        };

        // 3. Publish CompressedImage (JPEG) - Ultra-fast, minimal bandwidth
        byte[] jpgBytes = texture2D.EncodeToJPG(jpegQuality);
        CompressedImageMsg compressedMsg = new CompressedImageMsg
        {
            header = header,
            format = "jpeg",
            data = jpgBytes
        };
        ros.Publish(compressedImageTopic, compressedMsg);

        // 4. Publish Raw ImageMsg (RGB8) - Standard ROS format
        byte[] rawBytes = texture2D.GetRawTextureData();
        ImageMsg rawMsg = new ImageMsg
        {
            header = header,
            height = (uint)imageHeight,
            width = (uint)imageWidth,
            encoding = "rgb8",
            is_bigendian = 0,
            step = (uint)(imageWidth * 3),
            data = rawBytes
        };
        ros.Publish(rawImageTopic, rawMsg);
    }

    private void OnGUI()
    {
        if (!showPreview || renderTexture == null) return;

        // Draw Picture-in-Picture window at bottom-right corner
        float pipWidth = 240f;
        float pipHeight = 180f;
        float padding = 12f;
        Rect pipRect = new Rect(Screen.width - pipWidth - padding, Screen.height - pipHeight - padding, pipWidth, pipHeight);

        // Outer background box
        GUI.Box(new Rect(pipRect.x - 2, pipRect.y - 20, pipWidth + 4, pipHeight + 22), "Upstream Inspection Station [V]");
        GUI.DrawTexture(pipRect, renderTexture, ScaleMode.ScaleToFit, false);
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
        if (texture2D != null)
        {
            Destroy(texture2D);
        }
    }
}
