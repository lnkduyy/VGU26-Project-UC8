using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;

/// <summary>
/// Tính toán tọa độ 3D của vật thể theo gốc base_link của Robot,
/// chuyển đổi từ hệ trục Unity sang hệ trục ROS (FLU) và bắn dữ liệu qua TCP.
/// Dự án: UC8 - Robotic Arm Conveyor Pick-and-Place
/// Tuần: Week 03
/// </summary>
public class ObjectLocalizationReporter : MonoBehaviour
{
    [Header("Reference Frame (Robot base_link)")]
    [Tooltip("Robot base_link origin. If left empty, defaults to UnityWorld")]
    [SerializeField] private Transform referenceFrame;

    [Header("ROS Topic Names")]
    [SerializeField] private string poseTopic = "/detected_object_pose";
    [SerializeField] private string typeTopic = "/detected_object_type";

    private ROSConnection ros;

    private void Awake()
    {
        // Ensure topic names are valid
        if (string.IsNullOrEmpty(poseTopic)) poseTopic = "/detected_object_pose";
        if (string.IsNullOrEmpty(typeTopic)) typeTopic = "/detected_object_type";
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(poseTopic)) poseTopic = "/detected_object_pose";
        if (string.IsNullOrEmpty(typeTopic)) typeTopic = "/detected_object_type";

        // Initialize ROS TCP Endpoint connection
        ros = ROSConnection.GetOrCreateInstance();

        // Register publishers
        ros.RegisterPublisher<PoseMsg>(poseTopic);
        ros.RegisterPublisher<StringMsg>(typeTopic);

        Debug.Log($"[Localization] Registered ROS publishers: {poseTopic}, {typeTopic}");
    }

    /// <summary>
    /// Calculate and publish object pose to ROS
    /// </summary>
    public void ReportObjectPose(Transform detectedObject, SortableObject sortableObject)
    {
        if (detectedObject == null)
        {
            Debug.LogWarning("[Localization] Detected object is null!");
            return;
        }

        if (string.IsNullOrEmpty(poseTopic)) poseTopic = "/detected_object_pose";
        if (string.IsNullOrEmpty(typeTopic)) typeTopic = "/detected_object_type";

        Vector3 relativePosition;
        Quaternion relativeRotation;
        string frameName;

        // 1. Calculate relative coordinates in robot base_link frame
        if (referenceFrame != null)
        {
            relativePosition = referenceFrame.InverseTransformPoint(detectedObject.position);
            relativeRotation = Quaternion.Inverse(referenceFrame.rotation) * detectedObject.rotation;
            frameName = referenceFrame.name;
        }
        else
        {
            relativePosition = detectedObject.position;
            relativeRotation = detectedObject.rotation;
            frameName = "UnityWorld";
        }

        string objectType = (sortableObject != null) ? sortableObject.ObjectType.ToString() : "Unknown";

        // 2. Convert coordinate system from Unity (Left-handed) to ROS FLU (Right-handed)
        PoseMsg poseMessage = new PoseMsg
        {
            position = relativePosition.To<FLU>(),
            orientation = relativeRotation.To<FLU>()
        };

        StringMsg typeMessage = new StringMsg
        {
            data = objectType
        };

        // 3. Publish Pose first, then Type label
        ros.Publish(poseTopic, poseMessage);
        ros.Publish(typeTopic, typeMessage);

        Debug.Log($"[ROS Publish] Published object pose: Type={objectType}, Frame={frameName}, " +
                  $"ROS coordinates: X={poseMessage.position.x:F3}, Y={poseMessage.position.y:F3}, Z={poseMessage.position.z:F3} m");
    }
}
