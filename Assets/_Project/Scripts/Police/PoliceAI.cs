using UnityEngine;
using UnityEngine.AI;
using PoliceChase.Vehicles;

namespace PoliceChase.Police
{
    /// <summary>
    /// AI lái xe cảnh sát. Dùng chung CarController với xe người chơi.
    /// - Nhìn thấy người chơi (tia nhìn không bị nhà che) → đuổi theo chỗ người chơi SẮP tới; gần thì lao thẳng vào húc.
    /// - Không thấy → chạy tới chỗ thấy lần cuối (hoặc chỗ được bộ đàm báo), tới nơi thì lùng sục xung quanh.
    /// - Đường đi tính bằng NavMesh, xe tự giảm tốc khi vào cua, tự lùi khi bị kẹt.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class PoliceAI : MonoBehaviour
    {
        public enum State { Chase, Leave }

        [Header("Mục tiêu")]
        public Transform target;
        public State state = State.Chase;

        [Header("Tầm nhìn")]
        public float visionRange = 120f;
        public float eyeHeight = 1.2f;
        public float visionInterval = 0.2f;

        [Header("Lái theo đường")]
        public string agentTypeName = "Car";
        public float repathInterval = 0.4f;
        [Tooltip("Nhắm tới điểm trên đường đi cách xe ít nhất bấy nhiêu mét")]
        public float lookAheadDistance = 9f;
        public float maxSteerAngle = 35f;
        [Tooltip("Tốc độ khi vào cua gắt (m/s)")]
        public float cornerSpeed = 12f;
        [Tooltip("Gần hơn khoảng này và nhìn thấy người chơi → lao thẳng vào húc")]
        public float ramDistance = 20f;

        [Header("Đuổi kịp")]
        [Tooltip("Xa người chơi hơn khoảng này thì được tăng tốc tối đa")]
        public float catchUpDistance = 80f;
        public float catchUpBoost = 1.15f;

        [Header("Đèn còi")]
        public Renderer lightRed;
        public Renderer lightBlue;
        public float blinkRate = 4f;

        public bool CanSeeTarget { get; private set; }
        public float DistanceToTarget { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public CarController Car => car;

        CarController car;
        Rigidbody targetRb;
        NavMeshPath path;
        NavMeshQueryFilter filter;
        Vector3[] corners = new Vector3[0];
        float repathTimer, visionTimer, stuckTimer, reverseTimer;
        float baseMaxSpeed;
        Vector3 searchPoint, leavePoint;
        bool hasSearchPoint, hasLeavePoint;
        MaterialPropertyBlock mpb;

        void Awake()
        {
            car = GetComponent<CarController>();
            path = new NavMeshPath();
            filter = new NavMeshQueryFilter { agentTypeID = FindAgentTypeID(agentTypeName), areaMask = NavMesh.AllAreas };
            baseMaxSpeed = car.maxSpeed;
        }

        void Start()
        {
            if (target == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) target = p.transform;
            }
            if (target != null)
            {
                targetRb = target.GetComponent<Rigidbody>();
                LastSeenPosition = target.position;
            }
            repathTimer = Random.Range(0f, repathInterval); // để các xe không tính đường cùng 1 frame
        }

        // ───────────────────────── API cho PoliceDispatcher ─────────────────────────

        /// <summary>Bộ đàm báo vị trí người chơi.</summary>
        public void ReportTargetPosition(Vector3 position)
        {
            LastSeenPosition = position;
            hasSearchPoint = false;
        }

        public void Chase() { state = State.Chase; hasSearchPoint = false; }
        public void Leave() { state = State.Leave; hasLeavePoint = false; }

        // ───────────────────────── Vòng lặp ─────────────────────────

        void Update()
        {
            Blink();
            if (target == null) { car.SetInput(0f, 0f, true); return; }
            if (targetRb == null) targetRb = target.GetComponent<Rigidbody>();

            float dt = Time.deltaTime;
            DistanceToTarget = Vector3.Distance(transform.position, target.position);

            visionTimer -= dt;
            if (visionTimer <= 0f)
            {
                visionTimer = visionInterval;
                CanSeeTarget = CheckVision();
                if (CanSeeTarget) LastSeenPosition = target.position;
            }

            Vector3 goal;
            bool ram = false;
            if (state == State.Chase)
            {
                if (CanSeeTarget)
                {
                    Vector3 v = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
                    float lead = Mathf.Clamp(DistanceToTarget / 30f, 0f, 1.2f);
                    goal = target.position + v * lead;
                    ram = DistanceToTarget < ramDistance;
                    hasSearchPoint = false;
                }
                else
                {
                    if (!hasSearchPoint) { searchPoint = LastSeenPosition; hasSearchPoint = true; }
                    if (Flat(transform.position - searchPoint).magnitude < 10f)
                        searchPoint = RandomNavPointNear(LastSeenPosition, 40f);
                    goal = searchPoint;
                }
            }
            else
            {
                if (!hasLeavePoint || Flat(transform.position - leavePoint).magnitude < 10f)
                {
                    Vector3 away = Flat(transform.position - target.position);
                    if (away.sqrMagnitude < 0.01f) away = transform.forward;
                    leavePoint = RandomNavPointNear(transform.position + away.normalized * 80f, 60f);
                    hasLeavePoint = true;
                }
                goal = leavePoint;
            }

            Drive(goal, ram, dt);
        }

        // ───────────────────────── Lái ─────────────────────────

        void Drive(Vector3 goal, bool ram, float dt)
        {
            Vector3 steerPoint;
            if (ram) steerPoint = goal;
            else
            {
                repathTimer -= dt;
                if (repathTimer <= 0f) Repath(goal);
                steerPoint = NextPathPoint(goal);
            }

            Vector3 local = transform.InverseTransformPoint(steerPoint);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(angle / maxSteerAngle, -1f, 1f);
            float speed = car.ForwardSpeed;
            bool handbrake = false;
            float throttle;

            if (ram) throttle = 1f;
            else
            {
                float desired = Mathf.Lerp(car.maxSpeed, cornerSpeed, Mathf.Clamp01(Mathf.Abs(angle) / 70f));
                throttle = speed < desired ? 1f : (speed > desired + 4f ? -0.6f : 0.1f);
            }
            if (Mathf.Abs(angle) > 70f && speed > 14f) handbrake = true;

            // Mục tiêu ở phía sau và xe đang chậm → lùi + bẻ lái ngược để quay đầu.
            if (Mathf.Abs(angle) > 120f && speed < 6f)
            {
                throttle = -1f;
                steer = -Mathf.Sign(angle);
                handbrake = false;
            }

            // Bị kẹt (đâm tường) → lùi 1.2 giây rồi thử lại.
            if (reverseTimer > 0f)
            {
                reverseTimer -= dt;
                throttle = -1f;
                steer = -steer;
                handbrake = false;
            }
            else
            {
                if (throttle > 0.5f && Mathf.Abs(speed) < 1f) stuckTimer += dt; else stuckTimer = 0f;
                if (stuckTimer > 1.5f) { stuckTimer = 0f; reverseTimer = 1.2f; }
            }

            car.maxSpeed = (state == State.Chase && DistanceToTarget > catchUpDistance)
                ? baseMaxSpeed * catchUpBoost : baseMaxSpeed;
            car.SetInput(throttle, steer, handbrake);
        }

        void Repath(Vector3 goal)
        {
            repathTimer = repathInterval;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit a, 6f, filter) &&
                NavMesh.SamplePosition(goal, out NavMeshHit b, 15f, filter) &&
                NavMesh.CalculatePath(a.position, b.position, filter, path) &&
                path.status != NavMeshPathStatus.PathInvalid)
                corners = path.corners;
            else
                corners = new Vector3[0];
        }

        Vector3 NextPathPoint(Vector3 goal)
        {
            if (corners.Length < 2) return goal;
            Vector3 pos = transform.position;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 to = Flat(corners[i] - pos);
                bool behind = Vector3.Dot(transform.forward, to) < 0f && to.magnitude < 15f;
                if (behind) continue;
                if (to.magnitude > lookAheadDistance) return corners[i];
            }
            return corners[corners.Length - 1];
        }

        // ───────────────────────── Tầm nhìn ─────────────────────────

        bool CheckVision()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 tgt = target.position + Vector3.up * 0.8f;
            Vector3 d = tgt - eye;
            float dist = d.magnitude;
            if (dist > visionRange) return false;
            if (dist < 0.01f) return true;
            if (Physics.Raycast(eye, d / dist, out RaycastHit hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.transform == target || hit.transform.IsChildOf(target) || (targetRb != null && hit.rigidbody == targetRb);
            return true;
        }

        // ───────────────────────── Tiện ích ─────────────────────────

        Vector3 RandomNavPointNear(Vector3 center, float radius)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector2 r = Random.insideUnitCircle * radius;
                if (NavMesh.SamplePosition(center + new Vector3(r.x, 0f, r.y), out NavMeshHit hit, 10f, filter))
                    return hit.position;
            }
            return center;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void Blink()
        {
            if (lightRed == null && lightBlue == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            bool phase = Mathf.Repeat(Time.time * blinkRate, 1f) < 0.5f;
            SetColor(lightRed, phase ? new Color(1f, 0.1f, 0.1f) : new Color(0.25f, 0f, 0f));
            SetColor(lightBlue, phase ? new Color(0f, 0f, 0.25f) : new Color(0.1f, 0.35f, 1f));
        }

        void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }

        static int FindAgentTypeID(string agentName)
        {
            int count = NavMesh.GetSettingsCount();
            for (int i = 0; i < count; i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == agentName) return s.agentTypeID;
            }
            return count > 0 ? NavMesh.GetSettingsByIndex(0).agentTypeID : 0;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (int i = 1; i < corners.Length; i++) Gizmos.DrawLine(corners[i - 1], corners[i]);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(LastSeenPosition, 2f);
        }
    }
}
