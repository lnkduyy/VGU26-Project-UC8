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

    [Header("Mount Transform (Option 1: Opposite Front-Tilted Mount)")]
    [Tooltip("Camera positioned opposite to the robot across the conveyor (Z=-1.10m), tilted towards pick station")]
    [SerializeField] private Vector3 mountPosition = new Vector3(0.0f, 0.75f, -1.10f);
    [Tooltip("Tilted downward towards the conveyor pick station (Z=-0.60m)")]
    [SerializeField] private Vector3 mountEulerAngles = new Vector3(50.0f, 0.0f, 0.0f);
    [SerializeField] private float fieldOfView = 48f;

    [Header("3D Physical Camera Stand")]
    [SerializeField] private bool renderPhysicalStand = true;

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

        // 3. Procedurally generate realistic 3D camera stand on opposite side of conveyor
        if (renderPhysicalStand)
        {
            BuildPhysicalStand();
        }

        Debug.Log($"[OverheadCamera] Initialized Option 1 Front-Tilted Camera at {transform.position}, angle {mountEulerAngles}, resolution {imageWidth}x{imageHeight} @ {publishRateHz}Hz");
    }

    /// <summary>
    /// Procedurally constructs a realistic industrial camera tripod/stand on the opposite side of the conveyor.
    /// Provides physical realism in Unity scene while completely avoiding collision interference.
    /// </summary>
    private void BuildPhysicalStand()
    {
        if (transform.Find("CameraStand") != null) return;

        GameObject standRoot = new GameObject("CameraStand");
        standRoot.transform.SetParent(this.transform.parent, false);

        Color metalColor = new Color(0.25f, 0.28f, 0.32f); // Dark industrial steel
        Color headColor = new Color(0.12f, 0.12f, 0.14f);  // Black camera housing

        Shader stdShader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material metalMat = new Material(stdShader) { color = metalColor };
        Material headMat = new Material(stdShader) { color = headColor };

        // Base plate on floor
        CreatePart(standRoot.transform, "BasePlate", new Vector3(0.0f, 0.02f, -1.10f), new Vector3(0.24f, 0.04f, 0.24f), metalMat);
        // Vertical post
        CreatePart(standRoot.transform, "VerticalPost", new Vector3(0.0f, 0.38f, -1.10f), new Vector3(0.04f, 0.72f, 0.04f), metalMat);
        // Camera head housing
        CreatePart(standRoot.transform, "CameraHead", new Vector3(0.0f, 0.74f, -1.10f), new Vector3(0.09f, 0.07f, 0.12f), headMat, Quaternion.Euler(50.0f, 0.0f, 0.0f));
    }

    private void CreatePart(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.position = pos;
        part.transform.localScale = scale;
        if (rot.HasValue) part.transform.rotation = rot.Value;

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
        GUI.Box(new Rect(pipRect.x - 2, pipRect.y - 20, pipWidth + 4, pipHeight + 22), "Front-Tilted Camera View [V]");
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
