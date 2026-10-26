using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

/// <summary>
/// Quản lý cơ chế Kẹp / Nhả vật thể bằng Attach Logic (Parenting) kết hợp Visual Animated 2-Finger Claw:
/// - Mô hình tay kẹp 2 ngón chuẩn công nghiệp (Parallel-Jaw Industrial Gripper) gắn trực tiếp tại tool0.
/// - Ngón kẹp chuyển động mượt mà (Smooth Kinematic Stroke): mở rộng 9.2cm khi đón phôi, khép ôm sát 6.1cm khi kẹp.
/// - Đệm cao su ma sát cao (Rubber Gripper Pads) và đèn LED trạng thái (Xanh lá khi kẹp, Cyan khi mở).
/// - Giữ vững độ tin cậy tuyệt đối 100% bằng Attach Logic theo chỉ đạo của Giảng viên.
/// Dự án: UC8 - Robotic Arm Conveyor Pick-and-Place
/// Tuần: Week 04 & Milestone 1 (R4 - Industrial Gripper & Sorting)
/// </summary>
public class GripperController : MonoBehaviour
{
    [Header("ROS Topics Configuration")]
    [SerializeField] private string commandTopic = "/ur10e/gripper_command";
    [SerializeField] private string statusTopic = "/ur10e/gripper_status";

    [Header("Attach Logic Configuration")]
    [Tooltip("Search radius around end-effector for target cube (meters)")]
    [SerializeField] private float graspRadius = 0.09f;

    [Tooltip("Transform of tool0 end-effector. If left empty, uses this GameObject")]
    [SerializeField] private Transform attachPoint;

    [Header("Visual Claw Kinematics (m)")]
    [SerializeField] private float openHalfSpan = 0.046f;   // Tổng độ mở 9.2cm
    [SerializeField] private float closedHalfSpan = 0.0305f; // Tổng độ khép 6.1cm ôm sát hộp 6cm
    [SerializeField] private float fingerSpeed = 0.25f;     // Tốc độ trượt ngón kẹp (m/s)

    private ROSConnection ros;
    private GameObject attachedObject = null;
    private bool isHolding = false;

    // Visual Claw references
    private Transform clawRoot;
    private Transform leftFingerCarriage;
    private Transform rightFingerCarriage;
    private Material ledMaterial;
    private float currentHalfSpan;
    private bool clawInitialized = false;

    private Vector3 toolDirLocal = Vector3.forward;
    private Vector3 clampDirLocal = Vector3.right;
    private Vector3 depthDirLocal = Vector3.up;

    private void Awake()
    {
        ResolveAttachPoint();
        if (graspRadius > 0.10f) graspRadius = 0.09f;
    }

    private void Start()
    {
        ResolveAttachPoint();

        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<BoolMsg>(commandTopic, OnReceiveGripperCommand);
        ros.RegisterPublisher<BoolMsg>(statusTopic);

        Debug.Log($"[Gripper] Initialized Industrial 2-Finger Claw Gripper at {attachPoint.name}, listening to {commandTopic}");
    }

    private void LateUpdate()
    {
        if (!clawInitialized && attachPoint != null)
        {
            // Kiểm tra xem robot đã vào tư thế chúc thẳng đứng xuống đất chưa (để định vị trục vuông góc chính xác)
            float maxDown = 0f;
            Vector3[] cardinal = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            foreach (Vector3 c in cardinal)
            {
                float dot = Vector3.Dot(attachPoint.TransformDirection(c), Vector3.down);
                if (dot > maxDown) maxDown = dot;
            }

            // Chờ robot chúc xuống đất (hoặc sau 1.5s) để chốt hướng kẹp chuẩn xác
            if (maxDown > 0.85f || Time.timeSinceLevelLoad > 1.5f)
            {
                SetupVisualClaw();
                clawInitialized = true;
            }
        }

        AnimateFingers();
    }

    private void ResolveAttachPoint()
    {
        if (attachPoint == null)
        {
            Transform root = transform.root;
            attachPoint = FindDeepChild(root, "tool0");
            if (attachPoint == null)
            {
                attachPoint = FindDeepChild(root, "wrist_3_link");
            }
            if (attachPoint == null)
            {
                attachPoint = transform;
            }
            Debug.Log($"[GripperController] Attach point resolved to: {attachPoint.name}");

            // Tự động gắn TrajectoryVisualizer vào tool0 nếu chưa có
            if (attachPoint != null && attachPoint.GetComponent<TrajectoryVisualizer>() == null)
            {
                attachPoint.gameObject.AddComponent<TrajectoryVisualizer>();
            }
        }
    }

    [ContextMenu("Rebuild Visual Claw")]
    public void SetupVisualClaw()
    {
        if (attachPoint == null) ResolveAttachPoint();
        if (attachPoint == null) return;

        currentHalfSpan = openHalfSpan;

        // Xóa sạch bất kỳ mô hình VisualClaw cũ nào
        Transform existingClaw = attachPoint.Find("VisualClaw");
        if (existingClaw != null)
        {
            DestroyImmediate(existingClaw.gameObject);
        }

        ResolveLocalAxes();
        BuildVisualClawProcedural();
    }

    /// <summary>
    /// Tự động dò tìm trục cục bộ của attachPoint chĩa thẳng đứng chúc xuống đất (Vector3.down)
    /// </summary>
    private void ResolveLocalAxes()
    {
        Vector3[] cardinal = {
            Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back,
            Vector3.right, Vector3.left
        };

        // 1. Trục chĩa thẳng đứng chúc xuống đất (Vector3.down)
        float bestDownDot = -999f;
        toolDirLocal = Vector3.forward;
        foreach (Vector3 c in cardinal)
        {
            float dot = Vector3.Dot(attachPoint.TransformDirection(c), Vector3.down);
            if (dot > bestDownDot)
            {
                bestDownDot = dot;
                toolDirLocal = c;
            }
        }

        // 2. Trục nằm ngang mở/khép 2 ngón kẹp (Vector3.right dọc băng chuyền)
        float bestRightDot = -999f;
        clampDirLocal = Vector3.right;
        foreach (Vector3 c in cardinal)
        {
            if (c == toolDirLocal || c == -toolDirLocal) continue;
            float dot = Vector3.Dot(attachPoint.TransformDirection(c), Vector3.right);
            if (dot > bestRightDot)
            {
                bestRightDot = dot;
                clampDirLocal = c;
            }
        }

        // 3. Trục bề dày thân kẹp (vuông góc với 2 trục trên)
        depthDirLocal = Vector3.Cross(toolDirLocal, clampDirLocal).normalized;

        Debug.Log($"[GripperController] Auto-resolved axes -> Downward: {toolDirLocal}, Clamping: {clampDirLocal}, Depth: {depthDirLocal}");
    }

    private void BuildVisualClawProcedural()
    {
        GameObject rootObj = new GameObject("VisualClaw");
        rootObj.transform.SetParent(attachPoint, false);
        rootObj.transform.localPosition = Vector3.zero;
        rootObj.transform.localRotation = Quaternion.identity;
        clawRoot = rootObj.transform;

        // 1. Vật liệu công nghiệp
        Material darkSteelMat = new Material(Shader.Find("Standard"))
        {
            color = new Color(0.18f, 0.20f, 0.23f),
            name = "Gripper_DarkSteel_Mat"
        };
        darkSteelMat.SetFloat("_Metallic", 0.85f);
        darkSteelMat.SetFloat("_Glossiness", 0.65f);

        Material chassisMat = new Material(Shader.Find("Standard"))
        {
            color = new Color(0.12f, 0.16f, 0.23f),
            name = "Gripper_Chassis_Mat"
        };
        chassisMat.SetFloat("_Metallic", 0.70f);
        chassisMat.SetFloat("_Glossiness", 0.55f);

        Material aluMat = new Material(Shader.Find("Standard"))
        {
            color = new Color(0.82f, 0.84f, 0.88f),
            name = "Gripper_Aluminum_Mat"
        };
        aluMat.SetFloat("_Metallic", 0.90f);
        aluMat.SetFloat("_Glossiness", 0.75f);

        Material rubberMat = new Material(Shader.Find("Standard"))
        {
            color = new Color(0.08f, 0.09f, 0.10f),
            name = "Gripper_RubberPad_Mat"
        };
        rubberMat.SetFloat("_Metallic", 0.05f);
        rubberMat.SetFloat("_Glossiness", 0.15f);

        ledMaterial = new Material(Shader.Find("Standard"))
        {
            name = "Gripper_LED_Mat"
        };
        ledMaterial.EnableKeyword("_EMISSION");

        // 2. Mặt bích kết nối (Flange Mount Adapter) - dày 1.2cm theo toolDirLocal
        CreatePartBox(clawRoot, "FlangeMount", toolDirLocal * 0.006f, new Vector3(0.075f, 0.012f, 0.075f), darkSteelMat);

        // 3. Thân bộ truyền động kẹp (Chassis Housing) - cao 2.0cm
        GameObject chassis = CreatePartBox(clawRoot, "GripperChassis", toolDirLocal * 0.022f, new Vector3(0.065f, 0.020f, 0.048f), chassisMat);

        // 4. Đèn LED chỉ thị trạng thái kẹp trên mặt trước thân kẹp
        GameObject ledObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ledObj.name = "StatusLED";
        ledObj.transform.SetParent(chassis.transform, false);
        ledObj.transform.localPosition = depthDirLocal * 0.025f;
        ledObj.transform.localScale = Vector3.one * 0.010f;
        Collider ledCol = ledObj.GetComponent<Collider>();
        if (ledCol != null) DestroyImmediate(ledCol);
        MeshRenderer ledRend = ledObj.GetComponent<MeshRenderer>();
        if (ledRend != null) ledRend.material = ledMaterial;

        // 5. Cụm ngón kẹp trái (Left Finger Carriage)
        GameObject lCarriage = new GameObject("LeftFingerCarriage");
        lCarriage.transform.SetParent(clawRoot, false);
        leftFingerCarriage = lCarriage.transform;

        // Tay kẹp nhôm trái - dài 4.0cm CHĨA THẲNG ĐỨNG XUỐNG ĐẤT (dừng an toàn cách băng chuyền > 2cm)
        CreatePartBox(leftFingerCarriage, "LeftArm", toolDirLocal * 0.020f - clampDirLocal * 0.005f, new Vector3(0.010f, 0.040f, 0.024f), aluMat);
        // Đệm cao su tiếp xúc mặt trong
        CreatePartBox(leftFingerCarriage, "LeftRubberPad", toolDirLocal * 0.020f + clampDirLocal * 0.002f, new Vector3(0.004f, 0.032f, 0.020f), rubberMat);

        // 6. Cụm ngón kẹp phải (Right Finger Carriage)
        GameObject rCarriage = new GameObject("RightFingerCarriage");
        rCarriage.transform.SetParent(clawRoot, false);
        rightFingerCarriage = rCarriage.transform;

        // Tay kẹp nhôm phải - dài 4.0cm CHĨA THẲNG ĐỨNG XUỐNG ĐẤT
        CreatePartBox(rightFingerCarriage, "RightArm", toolDirLocal * 0.020f + clampDirLocal * 0.005f, new Vector3(0.010f, 0.040f, 0.024f), aluMat);
        // Đệm cao su tiếp xúc mặt trong
        CreatePartBox(rightFingerCarriage, "RightRubberPad", toolDirLocal * 0.020f - clampDirLocal * 0.002f, new Vector3(0.004f, 0.032f, 0.020f), rubberMat);

        UpdateStatusLED();
        AnimateFingers();
    }

    private GameObject CreatePartBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        // Khóa hướng khối hộp sao cho:
        // - Chiều cao (scale.y) dài theo toolDirLocal (chĩa thẳng xuống đất)
        // - Chiều rộng (scale.x) theo clampDirLocal (mở/khép ngón kẹp)
        // - Chiều sâu (scale.z) theo depthDirLocal
        obj.transform.localRotation = Quaternion.LookRotation(depthDirLocal, toolDirLocal);
        obj.transform.localScale = scale;

        Collider col = obj.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        if (mat != null)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = mat;
        }
        return obj;
    }

    private void AnimateFingers()
    {
        if (leftFingerCarriage == null || rightFingerCarriage == null) return;

        float targetHalfSpan = isHolding ? closedHalfSpan : openHalfSpan;
        currentHalfSpan = Mathf.MoveTowards(currentHalfSpan, targetHalfSpan, fingerSpeed * Time.deltaTime);

        leftFingerCarriage.localPosition = toolDirLocal * 0.032f - clampDirLocal * currentHalfSpan;
        rightFingerCarriage.localPosition = toolDirLocal * 0.032f + clampDirLocal * currentHalfSpan;
    }

    private void UpdateStatusLED()
    {
        if (ledMaterial != null)
        {
            Color ledColor = isHolding ? new Color(0.1f, 0.95f, 0.25f) : new Color(0.1f, 0.75f, 0.95f);
            ledMaterial.color = ledColor;
            ledMaterial.SetColor("_EmissionColor", ledColor * 1.5f);
        }
    }

    private void OnReceiveGripperCommand(BoolMsg msg)
    {
        if (msg.data)
        {
            GraspNearestObject();
        }
        else
        {
            ReleaseObject();
        }

        ros.Publish(statusTopic, new BoolMsg { data = isHolding });
    }

    public void GraspNearestObject()
    {
        if (isHolding) return;

        ResolveAttachPoint();

        Collider[] hits = Physics.OverlapSphere(attachPoint.position, graspRadius);
        GameObject bestTarget = null;
        float minDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            SortableObject sortable = hit.GetComponentInParent<SortableObject>();
            if (sortable != null && !sortable.isSorted)
            {
                float dist = Vector3.Distance(attachPoint.position, sortable.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = sortable.gameObject;
                }
            }
        }

        if (bestTarget != null)
        {
            attachedObject = bestTarget;
            Rigidbody rb = attachedObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            Collider col = attachedObject.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // Tự định tâm cơ khí (Self-Centering Parallel Grasp):
            // 1. Căn phẳng góc xoay của hộp vuông vắn 100% với má kẹp cao su và băng chuyền
            attachedObject.transform.rotation = Quaternion.identity;

            // 2. Căn tâm phôi khớp chính xác vào trục giữa 2 ngón kẹp (X và Z)
            Vector3 snappedPos = attachedObject.transform.position;
            snappedPos.x = attachPoint.position.x;
            snappedPos.z = attachPoint.position.z;
            attachedObject.transform.position = snappedPos;

            // 3. Khóa làm con của tay gắp tool0
            attachedObject.transform.SetParent(attachPoint, true);
            isHolding = true;

            UpdateStatusLED();

            // Cập nhật màu vệt quỹ đạo phát sáng theo màu hộp đang gắp
            SortableObject sortable = attachedObject.GetComponent<SortableObject>();
            if (sortable != null && attachPoint != null)
            {
                TrajectoryVisualizer traj = attachPoint.GetComponent<TrajectoryVisualizer>();
                if (traj != null)
                {
                    traj.OnObjectGrasped(sortable.ObjectType);
                }
            }

            Debug.Log($"[Gripper] GRASP SUCCESS: Clamped and attached {attachedObject.name} into 2-Finger Claw!");
        }
        else
        {
            Debug.LogWarning($"[Gripper] GRASP FAILED: No target cube found within radius {graspRadius} m!");
            isHolding = false;
            UpdateStatusLED();
        }
    }

    public void ReleaseObject()
    {
        if (!isHolding || attachedObject == null) return;

        attachedObject.transform.SetParent(null, true);

        Vector3 curPos = attachedObject.transform.position;
        if (curPos.y < 0.05f)
        {
            curPos.y = 0.05f;
            attachedObject.transform.position = curPos;
        }

        Collider col = attachedObject.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = false;
        }

        Rigidbody rb = attachedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.down * 0.15f;
            rb.angularVelocity = Vector3.zero;
            // Mở khóa constraints để rơi tự nhiên vào thùng phân loại
            rb.constraints = RigidbodyConstraints.None;
            rb.WakeUp();
        }

        SortableObject sortable = attachedObject.GetComponent<SortableObject>();
        if (sortable != null)
        {
            sortable.isSorted = true;
        }

        isHolding = false;
        UpdateStatusLED();

        // Xóa hoặc reset vệt quỹ đạo sau khi thả hộp vào thùng
        if (attachPoint != null)
        {
            TrajectoryVisualizer traj = attachPoint.GetComponent<TrajectoryVisualizer>();
            if (traj != null)
            {
                traj.OnObjectReleased();
            }
        }

        Debug.Log($"[Gripper] RELEASE SUCCESS: Released {attachedObject.name} into sorting bin!");

        PickZoneSensor sensor = FindObjectOfType<PickZoneSensor>();
        if (sensor != null)
        {
            sensor.ForceResume();
        }
        else
        {
            ConveyorController conveyor = FindObjectOfType<ConveyorController>();
            if (conveyor != null) conveyor.StartConveyor();
        }

        RandomObjectSpawner spawner = FindObjectOfType<RandomObjectSpawner>();
        if (spawner != null)
        {
            spawner.TriggerNextSpawn(0.5f);
        }

        Destroy(attachedObject, 15.0f);
        attachedObject = null;
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                return child;
            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Transform pt = (attachPoint != null) ? attachPoint : transform;
        Gizmos.color = isHolding ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(pt.position, graspRadius);
    }
}
