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

    [Header("Mount Transform (Overhead Pick Station)")]
    [Tooltip("Camera position directly above the conveyor pick station (Z=-0.60m in Unity world)")]
    [SerializeField] private Vector3 mountPosition = new Vector3(0.0f, 1.25f, -0.60f);
    [Tooltip("Looking straight down onto the conveyor")]
    [SerializeField] private Vector3 mountEulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
    [SerializeField] private float fieldOfView = 45f;

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

        Debug.Log($"[OverheadCamera] Initialized Overhead Camera at {transform.position}, resolution {imageWidth}x{imageHeight} @ {publishRateHz}Hz");
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
        GUI.Box(new Rect(pipRect.x - 2, pipRect.y - 20, pipWidth + 4, pipHeight + 22), "Overhead Camera View [V]");
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
