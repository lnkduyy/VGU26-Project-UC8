using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cảm biến vùng gắp (PickZone) - Vận hành thuần vật lý thực tế (Pure Realistic Physics):
/// Tự động phát hiện và hãm phôi chính xác tại tâm trạm gắp (stopThresholdX).
/// Khắc phục triệt để lỗi miss khối trong chế độ đa vật thể (Continuous Multi-Item Flow):
/// - Quét liên tục tất cả khối hộp trên băng chuyền thay vì phụ thuộc sự kiện OnTriggerEnter (vốn chỉ bắn 1 lần).
/// - Khóa cứng việc dừng băng chuyền khi có khối hộp đã tới trạm gắp, ngăn hiện tượng trôi quá đà.
/// - Chỉ tiếp tục dòng chảy khi vật thể đã được gắp và trạm gắp sẵn sàng đón phôi tiếp theo.
/// </summary>
public class PickZoneSensor : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private ConveyorController conveyor;
    [SerializeField] private ObjectLocalizationReporter localizationReporter;

    [Header("Pose Publishing Rate (Hz)")]
    [SerializeField] private float publishRateHz = 10f;

    [Header("Natural Stop Target Coordinate (World X Axis)")]
    [Tooltip("Threshold X coordinate where conveyor cuts power to let object coast to a stop")]
    [SerializeField] private float stopThresholdX = -0.015f;

    [Tooltip("Maximum allowed X coordinate before object is considered beyond reach")]
    [SerializeField] private float maxPickZoneX = 0.25f;

    [Tooltip("Approach detection window start coordinate (World X)")]
    [SerializeField] private float minApproachX = -0.60f;

    private float lastPublishTime = 0f;
    private GameObject currentTargetObject = null;
    private bool hasStoppedConveyor = false;

    private void Awake()
    {
        if (conveyor == null) conveyor = FindObjectOfType<ConveyorController>();
        if (localizationReporter == null) localizationReporter = GetComponent<ObjectLocalizationReporter>();
        // Khóa điểm dừng chuẩn xác tại tâm trạm gắp X = 0.0m
        stopThresholdX = 0.0f;
    }

    private void Update()
    {
        // 1. Quét tìm khối hộp hợp lệ dẫn đầu trên băng chuyền
        GameObject candidate = FindLeadingCubeOnConveyor();

        if (candidate == null)
        {
            // Không có khối hộp nào đang ở trong trạm gắp
            if (hasStoppedConveyor)
            {
                hasStoppedConveyor = false;
                if (conveyor != null) conveyor.StartConveyor();
            }
            currentTargetObject = null;
            return;
        }

        currentTargetObject = candidate;
        SortableObject sortable = currentTargetObject.GetComponent<SortableObject>();

        // 2. Kiểm tra vị trí của khối hộp dẫn đầu
        float cubeX = currentTargetObject.transform.position.x;

        if (cubeX >= stopThresholdX && cubeX <= maxPickZoneX)
        {
            // Khối hộp đã vào đúng tâm trạm gắp -> Hãm dừng băng chuyền ngay lập tức
            if (!hasStoppedConveyor)
            {
                hasStoppedConveyor = true;
                if (conveyor != null) conveyor.StopConveyor();

                // Triệt tiêu quán tính trôi vật lý (Electro-mechanical brake)
                Rigidbody rb = currentTargetObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // Căn chuẩn nhẹ tọa độ X về đúng tâm trạm gắp (X=0.0m)
                Vector3 settledPos = currentTargetObject.transform.position;
                settledPos.x = stopThresholdX;
                currentTargetObject.transform.position = settledPos;

                Debug.Log($"[PickZone] Object {currentTargetObject.name} reached pick station center (X={currentTargetObject.transform.position.x:F3}m). Conveyor STOPPED & Braked.");
            }

            // Stream tọa độ sang ROS với tần số publishRateHz (10 Hz)
            float interval = 1f / Mathf.Max(1f, publishRateHz);
            if (Time.time - lastPublishTime >= interval)
            {
                lastPublishTime = Time.time;
                if (localizationReporter != null && sortable != null)
                {
                    localizationReporter.ReportObjectPose(currentTargetObject.transform, sortable);
                }
            }
        }
        else if (cubeX < stopThresholdX)
        {
            // Khối hộp đang trên đường trôi tới trạm gắp -> Đảm bảo băng chuyền đang chạy
            if (hasStoppedConveyor)
            {
                hasStoppedConveyor = false;
                if (conveyor != null) conveyor.StartConveyor();
            }
        }
    }

    /// <summary>
    /// Tìm kiếm khối hộp chưa phân loại, chưa bị gắp, đang nằm trên mặt băng tải và đi xa nhất (dẫn đầu)
    /// </summary>
    public GameObject FindLeadingCubeOnConveyor()
    {
        SortableObject[] allObjects = FindObjectsOfType<SortableObject>();
        GameObject leadingObj = null;
        float maxX = float.MinValue;

        foreach (SortableObject obj in allObjects)
        {
            if (obj == null || obj.isSorted) continue;
            // Nếu đã được gắp bởi robot (đã SetParent vào tool0) -> Bỏ qua
            if (obj.transform.parent != null) continue;

            Vector3 pos = obj.transform.position;
            // Đảm bảo khối hộp nằm trên mặt băng chuyền (Z quanh -0.60m, Y > 0)
            if (Mathf.Abs(pos.z - (-0.60f)) > 0.25f || pos.y < 0f) continue;

            // Nằm trong dải tiếp cận và đón phôi của trạm gắp
            if (pos.x >= minApproachX && pos.x <= maxPickZoneX)
            {
                if (pos.x > maxX)
                {
                    maxX = pos.x;
                    leadingObj = obj.gameObject;
                }
            }
        }

        return leadingObj;
    }

    /// <summary>
    /// Cho phép gọi ForceResume từ ngoài, kiểm tra an toàn trước khi bật băng chuyền
    /// </summary>
    public void ForceResume()
    {
        // Kiểm tra xem có khối nào đang đứng ở trạm gắp không
        GameObject candidate = FindLeadingCubeOnConveyor();
        if (candidate != null && candidate.transform.position.x >= stopThresholdX)
        {
            // Vẫn còn khối đang đợi tại trạm -> Không bật băng chuyền để chống trôi quá đà!
            hasStoppedConveyor = true;
            if (conveyor != null) conveyor.StopConveyor();
            return;
        }

        hasStoppedConveyor = false;
        currentTargetObject = null;
        if (conveyor != null)
        {
            conveyor.StartConveyor();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ dải đón phôi của trạm gắp trong Scene view
        Gizmos.color = hasStoppedConveyor ? Color.red : Color.green;
        Vector3 center = new Vector3((minApproachX + maxPickZoneX) * 0.5f, 0.15f, -0.60f);
        Vector3 size = new Vector3(maxPickZoneX - minApproachX, 0.25f, 0.35f);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(stopThresholdX, 0f, -0.80f), new Vector3(stopThresholdX, 0.35f, -0.40f));
    }
}
