using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace PoliceChase.Vehicles
{
    /// <summary>
    /// Khi bắt đầu game, đặt xe ở một điểm NGẪU NHIÊN trên mặt đường.
    /// - Nếu có danh sách Spawn Points (đặt tay) → chọn ngẫu nhiên trong đó.
    /// - Nếu để trống → chọn ngẫu nhiên trên NavMesh đã bake, chỉ lấy điểm:
    ///     + cách mép đường đủ xa,
    ///     + nối được bằng đường đi tới chỗ bạn đặt xe trong Editor (loại nóc nhà, khu tách rời),
    ///   rồi quay đầu xe theo hướng đường thoáng nhất.
    /// Phím N (khi đang chơi): đổi sang chỗ ngẫu nhiên khác – để thử.
    /// Gắn vào PlayerCar (cùng chỗ với CarController).
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class RandomSpawn : MonoBehaviour
    {
        [Tooltip("Tuỳ chọn: các điểm spawn đặt tay. Để trống = tự chọn ngẫu nhiên trên NavMesh")]
        public Transform[] spawnPoints;
        public bool spawnOnStart = true;

        [Header("Chọn ngẫu nhiên trên NavMesh")]
        [Tooltip("Tên Agent Type đã dùng khi Bake NavMesh")]
        public string agentTypeName = "Car";
        [Tooltip("Khoảng cách tối thiểu tới mép đường (m)")]
        public float minEdgeDistance = 2.5f;
        [Tooltip("Chỉ chọn điểm có đường đi tới chỗ đặt xe trong Editor (loại nóc nhà, đảo rời)")]
        public bool requireConnectedToStart = true;
        public int maxAttempts = 200;
        [Tooltip("Thả xe cao hơn mặt đường bao nhiêu mét")]
        public float spawnHeight = 1.2f;

        [Header("Thử nghiệm")]
        [Tooltip("Phím đổi chỗ ngẫu nhiên khi đang chơi (None = tắt)")]
        public Key respawnKey = Key.N;

        CarController car;
        NavMeshQueryFilter filter;
        Vector3 referencePoint;
        bool hasReference;

        Vector3[] verts;
        int[] indices;
        float[] cumulativeArea;
        float totalArea;

        void Awake() => car = GetComponent<CarController>();

        void Start()
        {
            filter = new NavMeshQueryFilter { agentTypeID = FindAgentTypeID(agentTypeName), areaMask = NavMesh.AllAreas };

            // Điểm tham chiếu = chỗ bạn đặt xe trong Editor (phải nằm trên đường chính).
            hasReference = NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20f, filter);
            if (hasReference) referencePoint = hit.position;
            else if (requireConnectedToStart)
                Debug.LogWarning("[RandomSpawn] Chỗ đặt xe trong Editor không nằm gần NavMesh – bỏ qua kiểm tra 'nối được tới chỗ đặt xe'. " +
                                 "Nên đặt xe trên mặt đường chính (vd. cao tốc) trước khi Play.", this);

            if (spawnOnStart) SpawnRandom();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && respawnKey != Key.None && kb[respawnKey].wasPressedThisFrame)
                SpawnRandom();
        }

        // ───────────────────────── API ─────────────────────────

        public void SpawnRandom()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                for (int tries = 0; tries < 20; tries++)
                {
                    var p = spawnPoints[Random.Range(0, spawnPoints.Length)];
                    if (p == null) continue;
                    Place(p.position + Vector3.up * spawnHeight, Quaternion.LookRotation(FlatForward(p), Vector3.up));
                    return;
                }
            }

            if (!BuildTriangulation())
            {
                Debug.LogWarning("[RandomSpawn] Scene CHƯA CÓ NavMesh (trống) – giữ nguyên vị trí xe. " +
                                 "Chọn object NavMesh > NavMesh Surface > bấm Bake, và Ctrl+S lưu scene.", this);
                return;
            }
            if (TryFindRandomRoadPoint(out Vector3 pos, out Quaternion rot)) Place(pos, rot);
            else Debug.LogWarning($"[RandomSpawn] Có NavMesh ({totalArea:0} m²) nhưng không tìm được điểm phù hợp với Agent Type '{agentTypeName}'. " +
                                  "Kiểm tra NavMesh Surface đang dùng đúng Agent Type này, hoặc giảm Min Edge Distance.", this);
        }

        /// <summary>Tìm 1 điểm ngẫu nhiên trên mặt đường (dùng được cho cả việc sinh xe cảnh sát sau này).</summary>
        public bool TryFindRandomRoadPoint(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            if (!BuildTriangulation()) return false;

            var path = new NavMeshPath();
            bool checkConnected = requireConnectedToStart && hasReference;

            for (int i = 0; i < maxAttempts * 2; i++)
            {
                // Nửa sau số lần thử: nới điều kiện (bỏ kiểm tra nối đường + mép đường) để chắc chắn tìm được chỗ.
                bool relaxed = i >= maxAttempts;
                if (relaxed && i == maxAttempts)
                    Debug.LogWarning("[RandomSpawn] Không có điểm nào thoả mọi điều kiện – nới điều kiện để tìm chỗ khác.", this);
                Vector3 p = RandomPointOnNavMesh();
                if (!NavMesh.SamplePosition(p, out NavMeshHit h, 2f, filter)) continue;
                p = h.position;

                if (!relaxed && NavMesh.FindClosestEdge(p, out NavMeshHit edge, filter) && edge.distance < minEdgeDistance) continue;

                if (!relaxed && checkConnected &&
                    (!NavMesh.CalculatePath(p, referencePoint, filter, path) || path.status != NavMeshPathStatus.PathComplete))
                    continue;

                position = p + Vector3.up * spawnHeight;
                rotation = Quaternion.LookRotation(BestRoadDirection(p), Vector3.up);
                return true;
            }
            return false;
        }

        // ───────────────────────── Nội bộ ─────────────────────────

        void Place(Vector3 pos, Quaternion rot)
        {
            Debug.Log($"[RandomSpawn] Đặt xe tại {pos} (diện tích NavMesh: {totalArea:0} m²).", this);
            car.Teleport(pos, rot);
            foreach (var cam in FindObjectsByType<CarCameraFollow>(FindObjectsSortMode.None))
                if (cam.target == transform) cam.SnapToTarget();
        }

        bool BuildTriangulation()
        {
            if (verts != null && verts.Length > 0) return true;
            var tri = NavMesh.CalculateTriangulation();
            verts = tri.vertices;
            indices = tri.indices;
            if (verts == null || verts.Length == 0 || indices.Length < 3) return false;

            int count = indices.Length / 3;
            cumulativeArea = new float[count];
            totalArea = 0f;
            for (int t = 0; t < count; t++)
            {
                Vector3 a = verts[indices[t * 3]], b = verts[indices[t * 3 + 1]], c = verts[indices[t * 3 + 2]];
                totalArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                cumulativeArea[t] = totalArea;
            }
            return totalArea > 0f;
        }

        Vector3 RandomPointOnNavMesh()
        {
            // Chọn tam giác theo diện tích (tam giác lớn được chọn nhiều hơn) rồi chọn điểm ngẫu nhiên trong đó.
            float r = Random.value * totalArea;
            int lo = 0, hi = cumulativeArea.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (cumulativeArea[mid] < r) lo = mid + 1; else hi = mid;
            }
            Vector3 a = verts[indices[lo * 3]], b = verts[indices[lo * 3 + 1]], c = verts[indices[lo * 3 + 2]];
            float r1 = Mathf.Sqrt(Random.value), r2 = Random.value;
            return (1f - r1) * a + r1 * (1f - r2) * b + r1 * r2 * c;
        }

        Vector3 BestRoadDirection(Vector3 p)
        {
            // Thử 16 hướng, chọn ngẫu nhiên trong các hướng thoáng nhất → đầu xe thường nằm dọc theo đường.
            const int dirs = 16;
            const float probe = 60f;
            var dist = new float[dirs];
            float best = 0f;
            for (int i = 0; i < dirs; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 360f / dirs, 0f) * Vector3.forward;
                dist[i] = NavMesh.Raycast(p, p + d * probe, out NavMeshHit hit, filter) ? hit.distance : probe;
                best = Mathf.Max(best, dist[i]);
            }
            int chosen = 0, seen = 0;
            for (int i = 0; i < dirs; i++)
            {
                if (dist[i] < best * 0.9f) continue;
                seen++;
                if (Random.Range(0, seen) == 0) chosen = i;
            }
            return Quaternion.Euler(0f, chosen * 360f / dirs, 0f) * Vector3.forward;
        }

        static Vector3 FlatForward(Transform t)
        {
            Vector3 f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            return f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
        }

        static int FindAgentTypeID(string agentName)
        {
            int count = NavMesh.GetSettingsCount();
            for (int i = 0; i < count; i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == agentName) return s.agentTypeID;
            }
            Debug.LogWarning($"[RandomSpawn] Không thấy Agent Type '{agentName}', dùng Agent Type mặc định.");
            return count > 0 ? NavMesh.GetSettingsByIndex(0).agentTypeID : 0;
        }
    }
}
