using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

/// <summary>
/// Lắng nghe chuỗi góc khớp từ MoveIt (/joint_states) và điều khiển 
/// 6 khớp xoay của cánh tay UR10e chuyển động mượt mà trong Unity bằng ArticulationBody.
/// Tự động loại bỏ Controller mặc định của URDF Importer (thủ phạm set forceLimit = 0),
/// khóa cứng đế robot (immovable) và triệt tiêu võng do trọng lực.
/// Dự án: UC8 - Robotic Arm Conveyor Pick-and-Place
/// Tuần: Week 04 (R4 - Trajectory Execution)
/// </summary>
public class UR10eController : MonoBehaviour
{
    [Header("ROS Topic Configuration")]
    [SerializeField] private string jointStatesTopic = "/joint_states";

    [Header("PhysX Joint Drive Parameters")]
    [SerializeField] private float stiffness = 50000f;
    [SerializeField] private float damping = 1000f;
    [SerializeField] private float forceLimit = 10000f;

    // 6 tên khớp chuẩn mực của UR10e theo chuẩn ROS URDF
    private readonly string[] rosJointNames =
    {
        "shoulder_pan_joint",
        "shoulder_lift_joint",
        "elbow_joint",
        "wrist_1_joint",
        "wrist_2_joint",
        "wrist_3_joint"
    };

    // Tên các GameObject Link tương ứng trong cây phân cấp của robot
    private readonly string[] linkNames =
    {
        "shoulder_link",
        "upper_arm_link",
        "forearm_link",
        "wrist_1_link",
        "wrist_2_link",
        "wrist_3_link"
    };

    private readonly Dictionary<string, int> jointNameToIndex = new Dictionary<string, int>();
    private ArticulationBody[] joints;
    private float[] targetAnglesDegrees;
    private bool isInitialized = false;
    private bool hasReceivedFirstMsg = false;

    [Header("Waypoint Visual Markers (Toggle Key [M])")]
    [Tooltip("Hiển thị các quả cầu định vị ảo (Tắt/Bật bằng phím [M])")]
    [SerializeField] private bool showWaypointMarkers = true;
    [SerializeField] private float markerScale = 0.045f;
    [SerializeField] private Vector3 markerIdlePos = new Vector3(0f, 0.42f, -0.60f);
    [SerializeField] private Vector3 markerPickPos = new Vector3(0f, 0.16f, -0.60f);
    [SerializeField] private Vector3 markerRedBinPos = new Vector3(-0.55f, 0.25f, -0.10f);
    [SerializeField] private Vector3 markerYellowBinPos = new Vector3(0.55f, 0.25f, -0.10f);
    [SerializeField] private Vector3 markerBlueBinPos = new Vector3(0f, 0.25f, 0.50f);

    // Góc chờ công nghiệp tối ưu cho Phương án 1:
    // Cánh tay co cao thanh thoát, cổ tay chúc 90 độ, ngón kẹp song song tuyệt đối với thành hộp (16.9 độ triệt tiêu góc xoay đế 16.9 độ)
    private readonly float[] defaultStandbyAnglesDegrees = { 16.9f, -84.6f, -121.9f, -63.2f, 90.0f, 16.9f };

    private void Awake()
    {
        // QUAN TRỌNG: Xóa Controller mặc định của URDF-Importer (thủ phạm set forceLimit = 0 làm liệt robot)
        MonoBehaviour[] allScripts = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour s in allScripts)
        {
            if (s != null && s != this && (s.GetType().Name == "Controller" || s.GetType().Name == "JointControl" || s.GetType().Name == "FKRobot"))
            {
                Debug.Log($"[UR10eController] Removed conflicting script: {s.GetType().Name}");
                DestroyImmediate(s);
            }
        }

        // Đồng bộ vị trí các thiết bị ngoại vi theo Phương án 1 (Front Pick - Flanking Bins)
        EnsureWorkcellLayout();

        // Tự động gắn bộ điều khiển góc máy điện ảnh cho Main Camera
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.GetComponent<WorkcellCameraController>() == null)
        {
            mainCam.gameObject.AddComponent<WorkcellCameraController>();
        }
    }

    private void EnsureWorkcellLayout()
    {
        GameObject table = GameObject.Find("Table");
        if (table != null)
        {
            table.transform.position = new Vector3(0f, -0.05f, 0f);
            table.transform.localScale = new Vector3(4.0f, 0.1f, 2.0f);
        }

        GameObject conveyor = GameObject.Find("Conveyor");
        if (conveyor != null)
        {
            conveyor.transform.position = new Vector3(0f, 0.1f, -0.6f);
            conveyor.transform.localScale = new Vector3(3.6f, 0.1f, 0.35f);
        }

        GameObject spawner = GameObject.Find("ObjectSpawner");
        if (spawner != null) spawner.transform.position = new Vector3(-1.5f, 0.35f, -0.6f);

        GameObject sensor = GameObject.Find("PickZoneSensor");
        if (sensor != null)
        {
            sensor.transform.localPosition = new Vector3(0f, 1f, 0f);
            sensor.transform.localScale = new Vector3(0.25f, 1f, 1f);
        }

        GameObject binRed = GameObject.Find("Bin_Red");
        if (binRed != null)
        {
            binRed.transform.position = new Vector3(-0.55f, 0.01f, -0.1f);
            binRed.transform.localScale = new Vector3(1.35f, 1.25f, 1.35f);
        }

        GameObject binYellow = GameObject.Find("Bin_Yellow");
        if (binYellow != null)
        {
            binYellow.transform.position = new Vector3(0.55f, 0.01f, -0.1f);
            binYellow.transform.localScale = new Vector3(1.35f, 1.25f, 1.35f);
        }

        GameObject binBlue = GameObject.Find("Bin_Blue");
        if (binBlue != null)
        {
            binBlue.transform.position = new Vector3(0f, 0.01f, 0.5f);
            binBlue.transform.localScale = new Vector3(1.35f, 1.25f, 1.35f);
        }

        // Tự động tạo và đồng bộ Tủ điều khiển UR Control Box (Góc thao tác kỹ sư - Requirement R8)
        EnsureURControlBox();

        // Tự động tạo và đồng bộ Tấm lưới an toàn mica Acrylic Safety Shield (Chặn giữa Băng tải và Thùng Đỏ - Requirement R8)
        EnsureAcrylicSafetyShield();

        // Tự động tạo và cập nhật các quả cầu Waypoint Visual Markers tại vị trí mới
        EnsureWaypointMarkers();

        Debug.Log("[UR10eController] Workcell layout, UR Control Box, Acrylic Safety Shield (R8), and Waypoint visual markers synchronized (Option 1)!");
    }

    private void EnsureURControlBox()
    {
        // Xóa vật cản cũ nếu có
        GameObject oldWall = GameObject.Find("Static_Barrier_Wall");
        if (oldWall != null) DestroyImmediate(oldWall);

        GameObject boxRoot = GameObject.Find("UR_Control_Box");
        if (boxRoot == null)
        {
            boxRoot = new GameObject("UR_Control_Box");

            // Base Shader
            Shader stdShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Diffuse");

            // Material: Thân tủ kim loại công nghiệp (Industrial Slate Metallic)
            Material cabinetMat = new Material(stdShader);
            cabinetMat.color = new Color(0.18f, 0.20f, 0.24f);
            if (cabinetMat.HasProperty("_Metallic")) cabinetMat.SetFloat("_Metallic", 0.7f);
            if (cabinetMat.HasProperty("_Glossiness")) cabinetMat.SetFloat("_Glossiness", 0.6f);

            // Material: Mặt trước panel nhôm xước (Brushed Aluminum)
            Material panelMat = new Material(stdShader);
            panelMat.color = new Color(0.38f, 0.42f, 0.48f);
            if (panelMat.HasProperty("_Metallic")) panelMat.SetFloat("_Metallic", 0.5f);

            // Material: Nút E-Stop vàng & đỏ
            Material yellowMat = new Material(stdShader);
            yellowMat.color = new Color(0.98f, 0.82f, 0.05f);
            Material redMat = new Material(stdShader);
            redMat.color = new Color(0.90f, 0.12f, 0.12f);

            // Material: Đèn LED báo nguồn xanh lục phát sáng
            Material greenLedMat = new Material(stdShader);
            greenLedMat.color = new Color(0.1f, 0.95f, 0.3f);
            greenLedMat.EnableKeyword("_EMISSION");
            greenLedMat.SetColor("_EmissionColor", new Color(0.1f, 0.95f, 0.3f) * 0.8f);

            // Material: Màn hình Teach Pendant (Dark Glass)
            Material screenMat = new Material(stdShader);
            screenMat.color = new Color(0.08f, 0.10f, 0.13f);
            if (screenMat.HasProperty("_Glossiness")) screenMat.SetFloat("_Glossiness", 0.9f);

            // A. Thân tủ chính (Cabinet Body: 32cm x 38cm x 24cm)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Cabinet_Body";
            body.transform.SetParent(boxRoot.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0f);
            body.transform.localScale = new Vector3(0.32f, 0.38f, 0.24f);
            body.GetComponent<MeshRenderer>().material = cabinetMat;
            DestroyImmediate(body.GetComponent<Collider>());

            // B. Mặt Panel trước (Front Panel Inset)
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Front_Panel";
            panel.transform.SetParent(boxRoot.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, -0.121f);
            panel.transform.localScale = new Vector3(0.28f, 0.34f, 0.008f);
            panel.GetComponent<MeshRenderer>().material = panelMat;
            DestroyImmediate(panel.GetComponent<Collider>());

            // C. Nút dừng khẩn cấp E-Stop (Vòng vàng + Nấm đỏ)
            GameObject eStopBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            eStopBase.name = "EStop_Base";
            eStopBase.transform.SetParent(boxRoot.transform, false);
            eStopBase.transform.localPosition = new Vector3(0.09f, 0.10f, -0.128f);
            eStopBase.transform.localScale = new Vector3(0.045f, 0.005f, 0.045f);
            eStopBase.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            eStopBase.GetComponent<MeshRenderer>().material = yellowMat;
            DestroyImmediate(eStopBase.GetComponent<Collider>());

            GameObject eStopCap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eStopCap.name = "EStop_Mushroom";
            eStopCap.transform.SetParent(boxRoot.transform, false);
            eStopCap.transform.localPosition = new Vector3(0.09f, 0.10f, -0.138f);
            eStopCap.transform.localScale = new Vector3(0.038f, 0.022f, 0.038f);
            eStopCap.GetComponent<MeshRenderer>().material = redMat;
            DestroyImmediate(eStopCap.GetComponent<Collider>());

            // D. Đèn LED báo trạng thái nguồn
            GameObject led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            led.name = "Power_LED";
            led.transform.SetParent(boxRoot.transform, false);
            led.transform.localPosition = new Vector3(-0.10f, 0.12f, -0.128f);
            led.transform.localScale = new Vector3(0.014f, 0.014f, 0.014f);
            led.GetComponent<MeshRenderer>().material = greenLedMat;
            DestroyImmediate(led.GetComponent<Collider>());

            // E. Cột đỡ Teach Pendant (Pendant Stand)
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pendant_Stand_Pole";
            pole.transform.SetParent(boxRoot.transform, false);
            pole.transform.localPosition = new Vector3(-0.11f, 0.22f, 0f);
            pole.transform.localScale = new Vector3(0.018f, 0.08f, 0.018f);
            pole.GetComponent<MeshRenderer>().material = cabinetMat;
            DestroyImmediate(pole.GetComponent<Collider>());

            // F. Màn hình Teach Pendant (Tablet cảm ứng nghiêng 25 độ hướng ra ngoài)
            GameObject pendant = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pendant.name = "Teach_Pendant_Screen";
            pendant.transform.SetParent(boxRoot.transform, false);
            pendant.transform.localPosition = new Vector3(-0.11f, 0.28f, -0.04f);
            pendant.transform.localScale = new Vector3(0.20f, 0.14f, 0.016f);
            pendant.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            pendant.GetComponent<MeshRenderer>().material = screenMat;
            DestroyImmediate(pendant.GetComponent<Collider>());

            // G. BoxCollider bao trọn toàn bộ tủ và màn hình để phát hiện va chạm vật lý
            BoxCollider col = boxRoot.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.02f, 0f);
            col.size = new Vector3(0.32f, 0.42f, 0.24f);
            col.isTrigger = false;
        }

        // Tọa độ Unity: (-0.48m, 0.19m, 0.35m) ở góc vuông phía sau giữa Thùng Đỏ và Thùng Xanh
        // Xoay mặt chính (màn hình & nút E-Stop) song song 100% với mặt thùng đỏ
        boxRoot.transform.position = new Vector3(-0.48f, 0.19f, 0.35f);
        boxRoot.transform.rotation = Quaternion.identity;
    }

    private void EnsureAcrylicSafetyShield()
    {
        // Dọn dẹp các vật cản cũ nếu còn tồn tại
        GameObject oldCurtain = GameObject.Find("Safety_Light_Curtain");
        if (oldCurtain != null) DestroyImmediate(oldCurtain);
        GameObject oldWall = GameObject.Find("Static_Barrier_Wall");
        if (oldWall != null) DestroyImmediate(oldWall);

        GameObject shieldRoot = GameObject.Find("Acrylic_Safety_Shield");
        if (shieldRoot != null) DestroyImmediate(shieldRoot);

        shieldRoot = new GameObject("Acrylic_Safety_Shield");

        Shader stdShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Diffuse");

        // Material: Khung nhôm định hình xám kim loại công nghiệp (Anodized Aluminum Frame)
        Material aluminumMat = new Material(stdShader);
        aluminumMat.color = new Color(0.28f, 0.30f, 0.34f);
        if (aluminumMat.HasProperty("_Metallic")) aluminumMat.SetFloat("_Metallic", 0.75f);
        if (aluminumMat.HasProperty("_Glossiness")) aluminumMat.SetFloat("_Glossiness", 0.65f);

        // Material: Chân đế kim loại bắt bu-lông màu đen nhám (Base Flange)
        Material blackMat = new Material(stdShader);
        blackMat.color = new Color(0.12f, 0.13f, 0.15f);

        // Material: Tấm chắn Mica / Polycarbonate trong suốt (Transparent Acrylic Glass)
        Material glassMat = new Material(stdShader);
        glassMat.color = new Color(0.85f, 0.93f, 1.0f, 0.35f); // Mica trong suốt ánh xanh nhẹ
        glassMat.SetFloat("_Mode", 3); // Transparent mode
        glassMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glassMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glassMat.SetInt("_ZWrite", 0);
        glassMat.DisableKeyword("_ALPHATEST_ON");
        glassMat.DisableKeyword("_ALPHABLEND_ON");
        glassMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        glassMat.renderQueue = 3000;
        if (glassMat.HasProperty("_Glossiness")) glassMat.SetFloat("_Glossiness", 0.95f);
        if (glassMat.HasProperty("_Metallic")) glassMat.SetFloat("_Metallic", 0.1f);

        // Material: Vạch an toàn cảnh báo vàng công nghiệp (Safety Yellow Warning Stripe)
        Material yellowMat = new Material(stdShader);
        yellowMat.color = new Color(0.96f, 0.75f, 0.05f);

        // A. 4 Cột nhôm định hình: 2 cột ngoài cùng và 2 cột tạo khung Cổng gắp phôi trung tâm (Pick Portal)
        float[] postX = { -0.80f, -0.22f, 0.22f, 0.80f };
        for (int i = 0; i < postX.Length; i++)
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = $"Post_{i + 1}";
            post.transform.SetParent(shieldRoot.transform, false);
            post.transform.localPosition = new Vector3(postX[i], 0f, 0f);
            post.transform.localScale = new Vector3(0.025f, 0.36f, 0.035f);
            post.GetComponent<MeshRenderer>().material = aluminumMat;
            DestroyImmediate(post.GetComponent<Collider>());

            // Chân đế bắt bu-lông xuống bàn
            GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foot.name = $"Foot_{i + 1}";
            foot.transform.SetParent(shieldRoot.transform, false);
            foot.transform.localPosition = new Vector3(postX[i], -0.175f, 0f);
            foot.transform.localScale = new Vector3(0.055f, 0.01f, 0.08f);
            foot.GetComponent<MeshRenderer>().material = blackMat;
            DestroyImmediate(foot.GetComponent<Collider>());
        }

        // B. CÁNH TRÁI (Bảo hộ trước Thùng Đỏ - Left Protective Wing): từ X = -0.80m đến X = -0.22m
        GameObject wingLeft = new GameObject("Wing_Left_RedBin");
        wingLeft.transform.SetParent(shieldRoot.transform, false);

        GameObject topRailL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topRailL.name = "TopRail_Left";
        topRailL.transform.SetParent(wingLeft.transform, false);
        topRailL.transform.localPosition = new Vector3(-0.51f, 0.17f, 0f);
        topRailL.transform.localScale = new Vector3(0.58f, 0.02f, 0.035f);
        topRailL.GetComponent<MeshRenderer>().material = aluminumMat;
        DestroyImmediate(topRailL.GetComponent<Collider>());

        GameObject bottomRailL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bottomRailL.name = "BottomRail_Left";
        bottomRailL.transform.SetParent(wingLeft.transform, false);
        bottomRailL.transform.localPosition = new Vector3(-0.51f, -0.17f, 0f);
        bottomRailL.transform.localScale = new Vector3(0.58f, 0.02f, 0.035f);
        bottomRailL.GetComponent<MeshRenderer>().material = aluminumMat;
        DestroyImmediate(bottomRailL.GetComponent<Collider>());

        GameObject glassL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glassL.name = "Glass_Left";
        glassL.transform.SetParent(wingLeft.transform, false);
        glassL.transform.localPosition = new Vector3(-0.51f, 0f, 0f);
        glassL.transform.localScale = new Vector3(0.55f, 0.32f, 0.008f);
        glassL.GetComponent<MeshRenderer>().material = glassMat;
        DestroyImmediate(glassL.GetComponent<Collider>());

        GameObject stripeL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripeL.name = "Stripe_Left";
        stripeL.transform.SetParent(wingLeft.transform, false);
        stripeL.transform.localPosition = new Vector3(-0.51f, 0f, 0f);
        stripeL.transform.localScale = new Vector3(0.55f, 0.025f, 0.010f);
        stripeL.GetComponent<MeshRenderer>().material = yellowMat;
        DestroyImmediate(stripeL.GetComponent<Collider>());

        // BoxCollider cánh trái chắn trước Thùng Đỏ (Bảo hộ thùng đỏ, robot vồng qua đỉnh mica)
        BoxCollider colL = wingLeft.AddComponent<BoxCollider>();
        colL.center = new Vector3(-0.51f, 0f, 0f);
        colL.size = new Vector3(0.58f, 0.36f, 0.08f);
        colL.isTrigger = false;

        // C. CÁNH PHẢI (Bảo hộ trước Thùng Vàng - Right Protective Wing): từ X = +0.22m đến X = +0.80m
        GameObject wingRight = new GameObject("Wing_Right_YellowBin");
        wingRight.transform.SetParent(shieldRoot.transform, false);

        GameObject topRailR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topRailR.name = "TopRail_Right";
        topRailR.transform.SetParent(wingRight.transform, false);
        topRailR.transform.localPosition = new Vector3(0.51f, 0.17f, 0f);
        topRailR.transform.localScale = new Vector3(0.58f, 0.02f, 0.035f);
        topRailR.GetComponent<MeshRenderer>().material = aluminumMat;
        DestroyImmediate(topRailR.GetComponent<Collider>());

        GameObject bottomRailR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bottomRailR.name = "BottomRail_Right";
        bottomRailR.transform.SetParent(wingRight.transform, false);
        bottomRailR.transform.localPosition = new Vector3(0.51f, -0.17f, 0f);
        bottomRailR.transform.localScale = new Vector3(0.58f, 0.02f, 0.035f);
        bottomRailR.GetComponent<MeshRenderer>().material = aluminumMat;
        DestroyImmediate(bottomRailR.GetComponent<Collider>());

        GameObject glassR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glassR.name = "Glass_Right";
        glassR.transform.SetParent(wingRight.transform, false);
        glassR.transform.localPosition = new Vector3(0.51f, 0f, 0f);
        glassR.transform.localScale = new Vector3(0.55f, 0.32f, 0.008f);
        glassR.GetComponent<MeshRenderer>().material = glassMat;
        DestroyImmediate(glassR.GetComponent<Collider>());

        GameObject stripeR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripeR.name = "Stripe_Right";
        stripeR.transform.SetParent(wingRight.transform, false);
        stripeR.transform.localPosition = new Vector3(0.51f, 0f, 0f);
        stripeR.transform.localScale = new Vector3(0.55f, 0.025f, 0.010f);
        stripeR.GetComponent<MeshRenderer>().material = yellowMat;
        DestroyImmediate(stripeR.GetComponent<Collider>());

        // BoxCollider cánh phải chắn trước Thùng Vàng
        BoxCollider colR = wingRight.AddComponent<BoxCollider>();
        colR.center = new Vector3(0.51f, 0f, 0f);
        colR.size = new Vector3(0.58f, 0.36f, 0.08f);
        colR.isTrigger = false;

        // Tọa độ Unity: (0m, 0.18m, -0.38m) -> Chiều cao 0.36m (đỉnh ở Y = 0.36m)
        shieldRoot.transform.position = new Vector3(0f, 0.18f, -0.38f);
        shieldRoot.transform.rotation = Quaternion.identity;
    }

    private void EnsureWaypointMarkers()
    {
        GameObject parentObj = GameObject.Find("Waypoint_Visual_Markers");
        if (parentObj == null)
        {
            parentObj = new GameObject("Waypoint_Visual_Markers");
        }

        CreateOrUpdateMarker(parentObj.transform, "Marker_IDLE / READY", markerIdlePos, new Color(0.2f, 0.95f, 0.3f));
        CreateOrUpdateMarker(parentObj.transform, "Marker_PICK ZONE",    markerPickPos, new Color(0.1f, 0.85f, 0.95f));
        CreateOrUpdateMarker(parentObj.transform, "Marker_RED BIN",      markerRedBinPos, new Color(0.95f, 0.2f, 0.2f));
        CreateOrUpdateMarker(parentObj.transform, "Marker_YELLOW BIN",   markerYellowBinPos, new Color(0.98f, 0.88f, 0.15f));
        CreateOrUpdateMarker(parentObj.transform, "Marker_BLUE BIN",     markerBlueBinPos, new Color(0.2f, 0.5f, 1.0f));

        parentObj.SetActive(showWaypointMarkers);
    }

    private void CreateOrUpdateMarker(Transform parent, string name, Vector3 pos, Color color)
    {
        Transform child = parent.Find(name);
        GameObject markerObj = child != null ? child.gameObject : null;

        if (markerObj == null)
        {
            markerObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObj.name = name;
            markerObj.transform.SetParent(parent, false);
            markerObj.transform.localScale = Vector3.one * markerScale;

            // Tắt Collider để tuyệt đối không cản trở chuyển động vật lý
            Collider col = markerObj.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.5f);
            MeshRenderer mr = markerObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = mat;
        }

        markerObj.transform.localScale = Vector3.one * markerScale;
        markerObj.transform.position = pos;
    }

    private void Start()
    {
        joints = new ArticulationBody[6];
        targetAnglesDegrees = new float[6];

        // 1. Tìm tất cả ArticulationBody và cố định đế robot (base_link)
        ArticulationBody[] allBodies = GetComponentsInChildren<ArticulationBody>(true);
        foreach (ArticulationBody body in allBodies)
        {
            if (body.isRoot || body.name == "base_link" || body.name == "ur10e" || body.name == "ur10e_robot" || body.name == "base_link_inertia" || body.name == "base")
            {
                body.immovable = true;
                Debug.Log($"[UR10eController] Locked robot base (immovable = true): {body.name}");
            }
        }

        // 2. Cấu hình 6 khớp xoay của cánh tay UR10e
        for (int i = 0; i < linkNames.Length; i++)
        {
            foreach (ArticulationBody body in allBodies)
            {
                if (body.name == linkNames[i])
                {
                    joints[i] = body;
                    break;
                }
            }

            if (joints[i] == null)
            {
                Debug.LogWarning($"[UR10eController] ArticulationBody not found for link: {linkNames[i]}");
                continue;
            }

            jointNameToIndex[rosJointNames[i]] = i;

            // Khóa trọng lực để cánh tay không bị trĩu/rụng do trọng trường PhysX
            joints[i].useGravity = false;

            // Khởi tạo tư thế Standby công nghiệp thanh thoát ngay từ frame đầu tiên
            float initialAngle = defaultStandbyAnglesDegrees[i];
            targetAnglesDegrees[i] = initialAngle;

            if (joints[i].dofCount > 0)
            {
                joints[i].jointPosition = new ArticulationReducedSpace(initialAngle * Mathf.Deg2Rad);
            }

            // Cấu hình thông số PID Drive giữ vị trí cực kỳ vững chãi và lực kéo mạnh mẽ
            ArticulationDrive drive = joints[i].xDrive;
            drive.stiffness = stiffness;
            drive.damping = damping;
            drive.forceLimit = forceLimit;
            drive.target = initialAngle;
            joints[i].xDrive = drive;
        }

        // 3. Bỏ qua va chạm vật lý giữa robot và các bề mặt tĩnh (Table, Conveyor) để chống kẹt/đập bàn
        string[] staticEnvNames = { "Table", "Conveyor" };
        Collider[] robotColliders = GetComponentsInChildren<Collider>(true);
        foreach (string envName in staticEnvNames)
        {
            GameObject envObj = GameObject.Find(envName);
            if (envObj != null)
            {
                Collider[] envColliders = envObj.GetComponentsInChildren<Collider>(true);
                foreach (var rc in robotColliders)
                {
                    foreach (var ec in envColliders)
                    {
                        Physics.IgnoreCollision(rc, ec, true);
                    }
                }
            }
        }
        Debug.Log("[UR10eController] Configured collision avoidance: Ignored collisions between robot arm and Table/Conveyor.");

        // Đăng ký Subscriber nhận lệnh từ MoveIt qua ROS-TCP
        ROSConnection.GetOrCreateInstance().Subscribe<JointStateMsg>(jointStatesTopic, OnReceiveJointState);
        isInitialized = true;

        Debug.Log($"[UR10eController] Successfully initialized 6 UR10e joints (forceLimit={forceLimit}). Ready to receive {jointStatesTopic}");
    }

    private void OnReceiveJointState(JointStateMsg msg)
    {
        if (!isInitialized || msg == null || msg.name == null || msg.position == null)
            return;

        int count = Math.Min(msg.name.Length, msg.position.Length);

        // Khớp góc xoay theo tên khớp (không phụ thuộc vào thứ tự trong message)
        for (int n = 0; n < count; n++)
        {
            if (jointNameToIndex.TryGetValue(msg.name[n], out int jointIndex))
            {
                double angleRadian = msg.position[n];
                if (!double.IsNaN(angleRadian) && !double.IsInfinity(angleRadian))
                {
                    // Chuyển từ Radian (ROS) sang Độ (Unity)
                    targetAnglesDegrees[jointIndex] = (float)(angleRadian * Mathf.Rad2Deg);
                }
            }
        }

        if (!hasReceivedFirstMsg)
        {
            Debug.Log($"[UR10eController] Received /joint_states stream from ROS! Joint shoulder_pan: {targetAnglesDegrees[0]:F1} deg");
            hasReceivedFirstMsg = true;
        }
    }

    private void Update()
    {
        // Phím tắt [M]: Bật / Tắt hiển thị các chấm định vị ảo (Waypoint Visual Markers)
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleWaypointMarkers();
        }
    }

    public void ToggleWaypointMarkers()
    {
        showWaypointMarkers = !showWaypointMarkers;
        GameObject parentObj = GameObject.Find("Waypoint_Visual_Markers");
        if (parentObj != null)
        {
            parentObj.SetActive(showWaypointMarkers);
        }
        Debug.Log($"[UR10eController] Waypoint Visual Markers: {(showWaypointMarkers ? "VISIBLE" : "HIDDEN")}");
    }

    private void FixedUpdate()
    {
        if (!isInitialized) return;

        // Cập nhật mục tiêu chuyển động của từng khớp vào vật lý PhysX
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] != null)
            {
                ArticulationDrive drive = joints[i].xDrive;
                drive.target = targetAnglesDegrees[i];
                joints[i].xDrive = drive;
            }
        }
    }
}
