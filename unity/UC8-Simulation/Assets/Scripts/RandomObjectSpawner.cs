using System.Collections;
using UnityEngine;

/// <summary>
/// Sinh ngẫu nhiên các khối hộp (Đỏ, Xanh, Vàng) ở đầu băng chuyền.
/// Duy trì dòng chảy liên tục: Ít nhất 2 vật thể luôn hiện diện trên băng tải (2 - 3 khối).
/// Tự động kiểm soát khoảng cách an toàn (minSpacingDistance) để các khối hộp không va chạm hay chồng lên nhau.
/// Dự án: UC8 - Robotic Arm Conveyor Pick-and-Place
/// Tuần: Week 04 (Continuous Multi-Item Flow)
/// </summary>
public class RandomObjectSpawner : MonoBehaviour
{
    [Header("Cube Dimensions (m)")]
    [SerializeField] private float cubeSize = 0.06f; // 6cm

    [Header("Continuous Flow Configuration")]
    [Tooltip("Target minimum simultaneous objects on conveyor belt")]
    [SerializeField] private int minTargetObjects = 2;
    public int MinTargetObjects => minTargetObjects;

    [Tooltip("Maximum simultaneous objects allowed on conveyor belt")]
    [SerializeField] private int maxObjectsOnBelt = 3;

    [Tooltip("Minimum distance (m) required between consecutive cubes to avoid stacking")]
    [SerializeField] private float minSpacingDistance = 0.38f;

    [Tooltip("Minimum cooldown (s) between consecutive spawns")]
    [SerializeField] private float minSpawnCooldown = 1.4f;

    private int currentSpawnCount = 0;
    private float lastSpawnTime = -999f;
    private ConveyorController conveyor;

    private void Awake()
    {
        conveyor = FindObjectOfType<ConveyorController>();
        // Ensure values allow continuous multi-item flow
        if (minSpawnCooldown > 1.5f) minSpawnCooldown = 1.4f;
        if (maxObjectsOnBelt < 2) maxObjectsOnBelt = 3;
    }

    private void Start()
    {
        // Chạy vòng lặp duy trì dòng chảy liên tục (Continuous Flow Watchdog)
        StartCoroutine(ContinuousFlowLoop());
    }

    private IEnumerator ContinuousFlowLoop()
    {
        // Chờ 0.5s sau khi bấm Play để hệ thống ổn định
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            // Nếu băng chuyền đang chạy và số lượng vật thể trên băng tải chưa đạt chỉ tiêu (2 - 3 hộp)
            if (conveyor == null || conveyor.IsRunning)
            {
                if (GetActiveObjectsOnBeltCount() < maxObjectsOnBelt)
                {
                    TrySpawnObject();
                }
            }

            yield return new WaitForSeconds(0.35f);
        }
    }

    /// <summary>
    /// Đếm chính xác số lượng khối hộp thực sự đang nằm trên mặt băng chuyền
    /// (không tính các hộp đã gắp lên tay robot hoặc đã rơi vào thùng phân loại)
    /// </summary>
    public int GetActiveObjectsOnBeltCount()
    {
        int count = 0;
        SortableObject[] allObjects = FindObjectsOfType<SortableObject>();
        foreach (SortableObject obj in allObjects)
        {
            if (obj == null) continue;
            // Nếu đã được phân loại vào thùng -> Đã rời khỏi băng tải
            if (obj.isSorted) continue;
            // Nếu đang được tay kẹp giữ (đã SetParent vào tool0) -> Đã rời khỏi băng tải
            if (obj.transform.parent != null) continue;
            // Băng chuyền nằm ở Z = -0.60m. Nếu hộp đã được nhấc sang vùng Z > -0.35m -> Đã rời băng tải
            if (obj.transform.position.z > -0.35f) continue;
            // Nếu hộp rơi khỏi bàn -> Bỏ qua
            if (obj.transform.position.y < -0.2f) continue;

            count++;
        }
        return count;
    }

    /// <summary>
    /// Tương thích ngược với các script cũ
    /// </summary>
    public bool HasActiveObjectOnBelt()
    {
        return GetActiveObjectsOnBeltCount() > 0;
    }

    /// <summary>
    /// Kiểm tra xem vị trí đầu băng chuyền (Spawn Area) đã thông thoáng chưa
    /// </summary>
    public bool IsSpawnAreaClear()
    {
        SortableObject[] allObjects = FindObjectsOfType<SortableObject>();
        foreach (SortableObject obj in allObjects)
        {
            if (obj == null || obj.isSorted || obj.transform.parent != null) continue;

            // Kiểm tra khoảng cách theo trục di chuyển băng chuyền (trục X) và trục Z
            float distX = Mathf.Abs(obj.transform.position.x - transform.position.x);
            float distZ = Mathf.Abs(obj.transform.position.z - transform.position.z);
            if (distX < minSpacingDistance && distZ < 0.25f)
            {
                return false; // Còn khối hộp quá gần điểm spawn
            }
        }
        return true;
    }

    /// <summary>
    /// Được gọi bởi GripperController khi robot nhả hộp vào thùng để chuẩn bị cho nhịp tiếp theo
    /// </summary>
    public void TriggerNextSpawn(float delay = 0.5f)
    {
        StartCoroutine(SpawnAfterDelayRoutine(delay));
    }

    private IEnumerator SpawnAfterDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        TrySpawnObject();
    }

    public void TrySpawnObject()
    {
        // 1. Kiểm tra thời gian hồi chiêu tối thiểu
        if (Time.time - lastSpawnTime < minSpawnCooldown)
        {
            return;
        }

        // 2. Không vượt quá giới hạn tối đa trên băng tải (mặc định 3 hộp)
        if (GetActiveObjectsOnBeltCount() >= maxObjectsOnBelt)
        {
            return;
        }

        // 3. Đảm bảo vị trí đầu băng chuyền không bị cản trở bởi hộp trước đó
        if (!IsSpawnAreaClear())
        {
            return;
        }

        SpawnSingleObject();
    }

    private void SpawnSingleObject()
    {
        lastSpawnTime = Time.time;

        // 1. Tạo Cube 3D
        GameObject newCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        newCube.name = $"Cube_{++currentSpawnCount}";
        newCube.transform.position = transform.position;
        newCube.transform.localScale = Vector3.one * cubeSize;

        // 2. Thêm Rigidbody để tương tác vật lý trôi mượt mà trên băng tải
        Rigidbody rb = newCube.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        // Khóa hoàn toàn góc xoay để phôi luôn trôi vuông vắn dọc rãnh băng chuyền, song song 100% với tay gắp
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        // Triệt tiêu độ nảy (Zero-Bounce)
        PhysicMaterial noBounceMat = new PhysicMaterial("NoBounceMat")
        {
            bounciness = 0f,
            bounceCombine = PhysicMaterialCombine.Minimum,
            dynamicFriction = 0.8f,
            staticFriction = 0.8f,
            frictionCombine = PhysicMaterialCombine.Maximum
        };
        BoxCollider boxCol = newCube.GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            boxCol.material = noBounceMat;
        }

        // 3. Gắn SortableObject
        SortableObject sortable = newCube.AddComponent<SortableObject>();

        // 4. Phân bố ngẫu nhiên 3 màu: Red, Blue, Yellow
        float rand = Random.value;
        Color cubeColor;
        SortableObject.ColorType type;

        if (rand < 0.33f)
        {
            type = SortableObject.ColorType.Red;
            cubeColor = new Color(0.9f, 0.15f, 0.15f); // Đỏ tươi
        }
        else if (rand < 0.66f)
        {
            type = SortableObject.ColorType.Blue;
            cubeColor = new Color(0.15f, 0.45f, 0.95f); // Xanh dương
        }
        else
        {
            type = SortableObject.ColorType.Yellow;
            cubeColor = new Color(0.95f, 0.85f, 0.15f); // Vàng
        }

        Renderer renderer = newCube.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = cubeColor;
            renderer.material = mat;
        }

        sortable.SetObjectType(type);

        // Tự hủy sau 90s để chống rò rỉ bộ nhớ
        Destroy(newCube, 90.0f);

        Debug.Log($"[Spawner] Spawned {newCube.name} ({type}). Active cubes on conveyor: {GetActiveObjectsOnBeltCount()}/{maxObjectsOnBelt}");
    }
}
