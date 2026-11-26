using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace PoliceChase.Vehicles
{
    /// <summary>
    /// Cài đặt chung cho xe giao thông (tuỳ chọn – không có thì dùng giá trị mặc định).
    /// Tạo tự động bởi menu Tools > Police Chase > Giao thông.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        public static TrafficManager Instance { get; private set; }

        [Header("Tốc độ (m/s – 10 m/s = 36 km/h)")]
        public float minSpeed = 8f;
        public float maxSpeed = 13f;

        [Header("Vùng chạy")]
        [Tooltip("Để trống = tự lấy theo NavMesh đã bake. Muốn giới hạn: tạo 1 object có BoxCollider (Is Trigger) bao thành phố và kéo vào đây.")]
        public BoxCollider trafficArea;
        [Tooltip("Xe vào sát mép bản đồ trong khoảng này (mét) thì biến mất và chạy lại từ đầu")]
        public float edgeMargin = 25f;
        [Tooltip("Xe phải chạy ít nhất từng này mét rồi mới được biến mất (tránh xe đỗ sẵn gần mép biến mất ngay)")]
        public float minTravelBeforeRespawn = 40f;

        [Header("Khác")]
        [Tooltip("Tên Agent Type trong Navigation (đã dùng để bake NavMesh)")]
        public string agentTypeName = "Car";
        [Tooltip("Không cho xe xuất hiện lại ngay trước mặt camera")]
        public bool hideRespawnFromCamera = true;
        [Tooltip("Hiện khung vùng chạy trong Scene view")]
        public bool drawArea = true;

        Bounds? navBounds;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Vùng chạy (theo BoxCollider hoặc theo NavMesh).</summary>
        public static Bounds Area
        {
            get
            {
                var m = Instance;
                if (m != null && m.trafficArea != null) return m.trafficArea.bounds;
                if (m != null && m.navBounds.HasValue) return m.navBounds.Value;
                var b = ComputeNavBounds();
                if (m != null) m.navBounds = b;
                else cachedStatic = b;
                return b;
            }
        }
        static Bounds? cachedStatic;

        static Bounds ComputeNavBounds()
        {
            if (cachedStatic.HasValue) return cachedStatic.Value;
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 2000f);
            var b = new Bounds(tri.vertices[0], Vector3.zero);
            foreach (var v in tri.vertices) b.Encapsulate(v);
            return b;
        }

        public static float MinSpeed => Instance != null ? Instance.minSpeed : 8f;
        public static float MaxSpeed => Instance != null ? Instance.maxSpeed : 13f;
        public static float EdgeMargin => Instance != null ? Instance.edgeMargin : 25f;
        public static float MinTravel => Instance != null ? Instance.minTravelBeforeRespawn : 40f;
        public static string AgentTypeName => Instance != null ? Instance.agentTypeName : "Car";
        public static bool HideFromCamera => Instance == null || Instance.hideRespawnFromCamera;

        void OnDrawGizmosSelected()
        {
            if (!drawArea) return;
            var b = trafficArea != null ? trafficArea.bounds : (Application.isPlaying ? Area : new Bounds());
            if (b.size == Vector3.zero) return;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(b.center, new Vector3(Mathf.Max(0f, b.size.x - edgeMargin * 2f), b.size.y, Mathf.Max(0f, b.size.z - edgeMargin * 2f)));
        }
    }

    /// <summary>
    /// Biến một chiếc ô tô trang trí trong thành phố thành xe tự chạy:
    /// chạy theo NavMesh về phía mép bản đồ, gần tới mép thì biến mất và xuất hiện lại ở vị trí ban đầu, rồi chạy tiếp.
    /// Tự tránh / dừng khi có xe khác hoặc xe người chơi phía trước. Không ảnh hưởng xe người chơi.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrafficCar : MonoBehaviour
    {
        [Tooltip("Tick nếu xe chạy lùi (đầu xe của model quay ngược)")]
        public bool reverseModel = false;
        [Tooltip("Khoảng cách nhìn phía trước để phanh (mét)")]
        public float brakeDistance = 9f;

        Transform body;          // object "Traffic_..." chứa model, gắn NavMeshAgent
        NavMeshAgent agent;
        Vector3 startPos;
        Quaternion startRot;
        Vector3 lastPos;
        float travelled, stuckTimer, cruiseSpeed, nextCheck, respawnRetry;
        bool hidden;
        Renderer[] renderers;
        Collider[] colliders;
        static readonly RaycastHit[] hits = new RaycastHit[8];

        void Start()
        {
            // Bỏ qua xe người chơi / cảnh sát (có CarController).
            if (GetComponentInParent<CarController>() != null || GetComponentInChildren<CarController>() != null) { enabled = false; return; }
            if (!TryBuildBody()) { enabled = false; return; }
        }

        bool TryBuildBody()
        {
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Debug.LogWarning($"[Traffic] '{name}' không có model – bỏ qua.", this); return false; }
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            // Hướng đầu xe = trục dài nhất, chọn chiều gần với hướng forward của model.
            Vector3 axis = b.size.x > b.size.z ? Vector3.right : Vector3.forward;
            if (Vector3.Dot(axis, transform.forward) < 0f) axis = -axis;
            if (reverseModel) axis = -axis;

            // Tìm điểm NavMesh gần xe.
            int agentType = FindAgentType(TrafficManager.AgentTypeName);
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            Vector3 bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            if (!NavMesh.SamplePosition(bottom, out var hit, 12f, filter))
            {
                Debug.LogWarning($"[Traffic] '{name}' không nằm gần NavMesh (agent '{TrafficManager.AgentTypeName}') – bỏ qua. Hãy Bake lại NavMesh.", this);
                return false;
            }

            // Tạo khung chứa: gốc ở đáy-giữa xe, hướng theo đầu xe.
            var holder = new GameObject("Traffic_" + name).transform;
            holder.SetPositionAndRotation(hit.position, Quaternion.LookRotation(axis, Vector3.up));
            holder.SetParent(transform.parent, true);
            transform.SetParent(holder, true);
            body = holder;

            // Va chạm: giữ collider sẵn có, nếu không có thì thêm hộp.
            colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
            {
                var box = holder.gameObject.AddComponent<BoxCollider>();
                Vector3 size = holder.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                box.center = holder.InverseTransformPoint(b.center);
                colliders = new Collider[] { box };
            }
            var rb = holder.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;

            agent = holder.gameObject.AddComponent<NavMeshAgent>();
            agent.agentTypeID = agentType;
            float length = Mathf.Max(b.size.x, b.size.z), width = Mathf.Min(b.size.x, b.size.z);
            agent.radius = Mathf.Clamp(width * 0.5f, 0.6f, 1.6f);
            agent.height = Mathf.Max(1f, b.size.y);
            agent.baseOffset = 0f;
            cruiseSpeed = Random.Range(TrafficManager.MinSpeed, TrafficManager.MaxSpeed);
            agent.speed = cruiseSpeed;
            agent.acceleration = 7f;
            agent.angularSpeed = 140f;
            agent.stoppingDistance = 2f;
            agent.autoBraking = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(30, 70);
            agent.Warp(hit.position);

            startPos = hit.position;
            startRot = holder.rotation;
            lastPos = startPos;
            travelled = 0f;
            PickDestination(holder.forward);
            nextCheck = Time.time + Random.value * 0.3f;
            return true;
        }

        void Update()
        {
            if (agent == null) return;

            if (hidden)
            {
                if (Time.time >= respawnRetry) TryRespawn();
                return;
            }
            if (!agent.isOnNavMesh) { Hide(); return; }

            Vector3 p = body.position;
            travelled += Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(lastPos.x, 0f, lastPos.z));
            lastPos = p;

            // Gần mép bản đồ → biến mất, chạy lại từ đầu.
            if (travelled > TrafficManager.MinTravel && NearEdge(p)) { Hide(); return; }

            // Phanh khi có xe phía trước.
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 0.2f;
                agent.speed = BlockedAhead() ? 0.01f : cruiseSpeed;
            }

            // Hết đường hoặc kẹt → chọn đường mới / chạy lại.
            if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance < 4f))
                PickDestination(body.forward);
            if (agent.speed > 1f && agent.velocity.sqrMagnitude < 0.25f) stuckTimer += Time.deltaTime;
            else stuckTimer = 0f;
            if (stuckTimer > 8f) { stuckTimer = 0f; Hide(); }
        }

        bool NearEdge(Vector3 p)
        {
            var a = TrafficManager.Area;
            float m = TrafficManager.EdgeMargin;
            return p.x < a.min.x + m || p.x > a.max.x - m || p.z < a.min.z + m || p.z > a.max.z - m;
        }

        /// <summary>Chọn đích ở mép bản đồ theo hướng đang chạy (thử lệch dần nếu không có đường).</summary>
        void PickDestination(Vector3 heading)
        {
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
            heading.Normalize();
            var area = TrafficManager.Area;
            float[] angles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
            var path = new NavMeshPath();
            foreach (float ang in angles)
            {
                Vector3 dir = Quaternion.Euler(0f, ang + Random.Range(-8f, 8f), 0f) * heading;
                Vector3 target = EdgePoint(body.position, dir, area, TrafficManager.EdgeMargin * 0.5f);
                if (!NavMesh.SamplePosition(target, out var hit, 40f, filter)) continue;
                if (!NavMesh.CalculatePath(body.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                agent.SetPath(path);
                return;
            }
            // Không tìm được: chọn điểm ngẫu nhiên trong vùng.
            for (int i = 0; i < 6; i++)
            {
                Vector3 r = new Vector3(Random.Range(area.min.x, area.max.x), area.center.y, Random.Range(area.min.z, area.max.z));
                if (NavMesh.SamplePosition(r, out var hit, 50f, filter) && agent.SetDestination(hit.position)) return;
            }
        }

        /// <summary>Điểm trên mép vùng (lùi vào trong 'inset') theo tia từ 'from' hướng 'dir'.</summary>
        static Vector3 EdgePoint(Vector3 from, Vector3 dir, Bounds a, float inset)
        {
            float tx = dir.x > 0.001f ? (a.max.x - inset - from.x) / dir.x : (dir.x < -0.001f ? (a.min.x + inset - from.x) / dir.x : float.MaxValue);
            float tz = dir.z > 0.001f ? (a.max.z - inset - from.z) / dir.z : (dir.z < -0.001f ? (a.min.z + inset - from.z) / dir.z : float.MaxValue);
            float t = Mathf.Max(5f, Mathf.Min(tx, tz));
            return from + dir * t;
        }

        bool BlockedAhead()
        {
            Vector3 origin = body.position + Vector3.up * 0.8f + body.forward * (agent.radius + 0.5f);
            int n = Physics.SphereCastNonAlloc(origin, agent.radius * 0.8f, body.forward, hits, brakeDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(body)) continue;
                if (h.rigidbody != null) return true;    // xe người chơi, cảnh sát, xe giao thông khác
            }
            return false;
        }

        void Hide()
        {
            hidden = true;
            SetVisible(false);
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            respawnRetry = Time.time + Random.Range(1f, 3f);
        }

        void TryRespawn()
        {
            respawnRetry = Time.time + 1.5f;
            // Chỗ xuất phát đang có xe khác → chờ.
            foreach (var c in Physics.OverlapSphere(startPos + Vector3.up, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                if (c.attachedRigidbody != null && !c.transform.IsChildOf(body)) return;
            // Không xuất hiện ngay trước mặt camera (gần).
            if (TrafficManager.HideFromCamera && Camera.main != null)
            {
                var cam = Camera.main;
                Vector3 v = cam.WorldToViewportPoint(startPos);
                bool onScreen = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
                if (onScreen && v.z < 60f) return;
            }

            body.SetPositionAndRotation(startPos, startRot);
            agent.enabled = true;
            agent.Warp(startPos);
            hidden = false;
            SetVisible(true);
            travelled = 0f;
            lastPos = startPos;
            stuckTimer = 0f;
            cruiseSpeed = Random.Range(TrafficManager.MinSpeed, TrafficManager.MaxSpeed);
            agent.speed = cruiseSpeed;
            PickDestination(body.forward);
        }

        void SetVisible(bool v)
        {
            foreach (var r in renderers) if (r != null) r.enabled = v;
            foreach (var c in colliders) if (c != null) c.enabled = v;
        }

        static readonly Dictionary<string, int> agentIds = new Dictionary<string, int>();
        static int FindAgentType(string agentName)
        {
            if (agentIds.TryGetValue(agentName, out int id)) return id;
            int count = NavMesh.GetSettingsCount();
            id = count > 0 ? NavMesh.GetSettingsByIndex(0).agentTypeID : 0;
            for (int i = 0; i < count; i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == agentName) { id = s.agentTypeID; break; }
            }
            agentIds[agentName] = id;
            return id;
        }
    }
}
