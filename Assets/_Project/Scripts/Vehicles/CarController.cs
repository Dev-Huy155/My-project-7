using UnityEngine;

namespace PoliceChase.Vehicles
{
    /// <summary>
    /// Bộ điều khiển xe kiểu arcade dùng chung cho xe người chơi và xe cảnh sát AI.
    /// - Giảm xóc bằng 4 tia raycast (không dùng WheelCollider) → chạy êm trên mesh thành phố, qua chỗ nối đường.
    /// - Ai muốn lái xe chỉ cần gọi SetInput(ga, lái, phanh tay) mỗi frame
    ///   (PlayerCarInput cho người chơi, sau này PoliceAI cho cảnh sát).
    /// Yêu cầu: Unity 6 (dùng Rigidbody.linearVelocity).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class CarController : MonoBehaviour
    {
        [Header("Động cơ")]
        [Tooltip("Tốc độ tối đa (m/s). 35 m/s ≈ 126 km/h")]
        public float maxSpeed = 35f;
        public float maxReverseSpeed = 10f;
        [Tooltip("Gia tốc khi nhấn ga (m/s²)")]
        public float acceleration = 14f;
        [Tooltip("Lực phanh (m/s²)")]
        public float brakeDeceleration = 30f;
        [Tooltip("Tự giảm tốc khi nhả ga (m/s²)")]
        public float coastDeceleration = 3f;

        [Header("Lái")]
        [Tooltip("Tốc độ quay đầu tối đa (độ/giây)")]
        public float turnRate = 110f;
        [Tooltip("Dưới tốc độ này (m/s) xe quay chậm hơn – xe đứng yên không xoay tại chỗ")]
        public float speedForFullSteer = 8f;
        [Tooltip("Giảm độ nhạy lái khi chạy nhanh (0 = không giảm)")]
        [Range(0f, 1f)] public float highSpeedSteerReduction = 0.4f;

        [Header("Bám đường")]
        [Tooltip("Độ bám ngang. Cao = bám chặt, thấp = dễ trượt")]
        public float grip = 10f;
        [Tooltip("Độ bám khi kéo phanh tay (thấp → drift)")]
        public float handbrakeGrip = 1.5f;
        [Tooltip("Lực ép xuống theo tốc độ, giúp xe không bị bay khi chạy nhanh")]
        public float downforce = 0.15f;

        [Header("Giảm xóc")]
        [Tooltip("x = nửa bề ngang, y = nửa chiều dài (vị trí 4 bánh)")]
        public Vector2 wheelOffset = new Vector2(0.85f, 1.35f);
        [Tooltip("Điểm bắt đầu tia dò mặt đường – nên nằm BÊN TRONG collider thân xe")]
        public float wheelAnchorHeight = 0f;
        public float suspensionLength = 0.6f;
        public float springStrength = 35000f;
        public float springDamper = 4000f;
        public float wheelRadius = 0.35f;

        [Header("Hình bánh xe (tuỳ chọn) – thứ tự: Trước trái, Trước phải, Sau trái, Sau phải")]
        [Tooltip("Mỗi phần tử là 1 object con TRỰC TIẾP của xe; object con đầu tiên của nó sẽ được quay tròn")]
        public Transform[] wheelVisuals = new Transform[4];
        public float maxVisualSteerAngle = 30f;

        [Header("Rigidbody")]
        public float mass = 1200f;
        public Vector3 centerOfMass = new Vector3(0f, -0.4f, 0f);
        [Tooltip("Tự lật xe lại nếu bị lật quá số giây này (0 = tắt)")]
        public float autoResetDelay = 3f;
        [Tooltip("Rơi thấp hơn độ cao này (so với chỗ an toàn cuối cùng) thì tự đưa xe về chỗ an toàn")]
        public float fallResetDepth = 30f;

        [Header("Gỡ lỗi")]
        [Tooltip("Hiện tốc độ và số bánh chạm đất ở góc màn hình")]
        public bool showDebugHud = true;

        // ───── Trạng thái đọc được từ bên ngoài ─────
        public float Throttle { get; private set; }
        public float Steer { get; private set; }
        public bool Handbrake { get; private set; }
        /// <summary>Tốc độ theo hướng đầu xe (m/s), âm khi lùi.</summary>
        public float ForwardSpeed { get; private set; }
        public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
        public bool IsGrounded { get; private set; }
        public Rigidbody Body => rb;

        Rigidbody rb;
        readonly Vector3[] anchors = new Vector3[4];
        readonly bool[] wheelHit = new bool[4];
        readonly float[] wheelDist = new float[4];
        Quaternion[] wheelBaseRot;
        float wheelSpin;
        float flippedTimer;
        Vector3 lastSafePosition;
        Quaternion lastSafeRotation;
        float safeTimer;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = mass;
            rb.centerOfMass = centerOfMass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 1.5f;

            // Thân xe trơn để không bị "dính" khi quẹt tường.
            foreach (var col in GetComponents<Collider>())
            {
                if (col.isTrigger || col.sharedMaterial != null) continue;
                col.sharedMaterial = new PhysicsMaterial("CarBody")
                {
                    dynamicFriction = 0.1f,
                    staticFriction = 0.1f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            RecalculateAnchors();
            lastSafePosition = transform.position;
            lastSafeRotation = transform.rotation;

            wheelBaseRot = new Quaternion[wheelVisuals.Length];
            for (int i = 0; i < wheelVisuals.Length; i++)
                if (wheelVisuals[i] != null && wheelVisuals[i].childCount > 0)
                    wheelBaseRot[i] = wheelVisuals[i].GetChild(0).localRotation;
        }

        void OnValidate() => RecalculateAnchors();

        public void RecalculateAnchors()
        {
            float x = wheelOffset.x, z = wheelOffset.y, h = wheelAnchorHeight;
            anchors[0] = new Vector3(-x, h, z);  // trước trái
            anchors[1] = new Vector3(x, h, z);   // trước phải
            anchors[2] = new Vector3(-x, h, -z); // sau trái
            anchors[3] = new Vector3(x, h, -z);  // sau phải
        }

        // ───────────────────────── API ─────────────────────────

        /// <summary>throttle: -1 (lùi/phanh) → 1 (ga); steer: -1 (trái) → 1 (phải).</summary>
        public void SetInput(float throttle, float steer, bool handbrake)
        {
            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Steer = Mathf.Clamp(steer, -1f, 1f);
            Handbrake = handbrake;
        }

        /// <summary>Lật xe đứng thẳng lại tại chỗ (phím R).</summary>
        public void ResetUpright()
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Teleport(transform.position + Vector3.up * 2f, Quaternion.LookRotation(fwd.normalized, Vector3.up));
        }

        /// <summary>Dịch chuyển xe tức thời (dùng khi spawn / hồi sinh).</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            flippedTimer = 0f;
            lastSafePosition = position;
            lastSafeRotation = rotation;
            safeTimer = 0f;
        }

        // ───────────────────────── Vật lý ─────────────────────────

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 up = transform.up;
            int grounded = 0;
            Vector3 groundNormal = Vector3.zero;

            // 1) Giảm xóc
            for (int i = 0; i < 4; i++)
            {
                Vector3 origin = transform.TransformPoint(anchors[i]);
                if (Physics.Raycast(origin, -up, out RaycastHit hit, suspensionLength + wheelRadius,
                                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.rigidbody != rb)
                {
                    float dist = hit.distance - wheelRadius;
                    float rawCompression = suspensionLength - dist;
                    float compression = Mathf.Clamp(rawCompression, 0f, suspensionLength);
                    float springVel = Vector3.Dot(rb.GetPointVelocity(origin), up);
                    float force = compression * springStrength - springVel * springDamper;
                    // Chặn cuối hành trình: giảm xóc bị nén hết thì đẩy cứng lên để thân xe không chạm đất.
                    if (rawCompression > suspensionLength)
                        force += (rawCompression - suspensionLength) * springStrength * 4f;
                    if (force > 0f) rb.AddForceAtPosition(up * force, origin);

                    wheelHit[i] = true;
                    wheelDist[i] = Mathf.Max(dist, 0f);
                    groundNormal += hit.normal;
                    grounded++;
                }
                else
                {
                    wheelHit[i] = false;
                    wheelDist[i] = suspensionLength;
                }
            }

            IsGrounded = grounded > 0;
            Vector3 vel = rb.linearVelocity;
            ForwardSpeed = Vector3.Dot(vel, transform.forward);

            if (IsGrounded)
            {
                groundNormal.Normalize();
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
                Vector3 right = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;
                float contact = grounded / 4f;

                // 2) Ga / phanh / lùi
                float accel = 0f;
                if (Throttle > 0.01f)
                {
                    if (ForwardSpeed < -0.5f) accel = brakeDeceleration * Throttle;          // đang lùi → phanh
                    else if (ForwardSpeed < maxSpeed) accel = acceleration * Throttle;
                }
                else if (Throttle < -0.01f)
                {
                    if (ForwardSpeed > 0.5f) accel = brakeDeceleration * Throttle;           // đang tiến → phanh
                    else if (ForwardSpeed > -maxReverseSpeed) accel = acceleration * 0.6f * Throttle;
                }
                else
                {
                    accel = -Mathf.Sign(ForwardSpeed) * Mathf.Min(coastDeceleration, Mathf.Abs(ForwardSpeed) / dt);
                }
                if (Handbrake)
                    accel -= Mathf.Sign(ForwardSpeed) * Mathf.Min(brakeDeceleration * 0.4f, Mathf.Abs(ForwardSpeed) / dt);

                rb.AddForce(fwd * accel * contact, ForceMode.Acceleration);

                // 3) Bám ngang (chống trượt) – phanh tay thì bám kém → drift
                float lateral = Vector3.Dot(vel, right);
                float g = Handbrake ? handbrakeGrip : grip;
                rb.AddForce(-right * lateral * g * contact, ForceMode.Acceleration);

                // 4) Lái
                float lowSpeed = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / speedForFullSteer);
                float highSpeed = Mathf.Lerp(1f, 1f - highSpeedSteerReduction, Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / maxSpeed));
                float dir = ForwardSpeed >= 0f ? 1f : -1f;
                float targetYaw = Steer * turnRate * Mathf.Deg2Rad * lowSpeed * highSpeed * dir * (Handbrake ? 1.3f : 1f);
                Vector3 localAv = transform.InverseTransformDirection(rb.angularVelocity);
                localAv.y = Mathf.Lerp(localAv.y, targetYaw, 10f * dt);
                rb.angularVelocity = transform.TransformDirection(localAv);

                // 5) Lực ép xuống
                rb.AddForce(-up * Mathf.Abs(ForwardSpeed) * downforce, ForceMode.Acceleration);
            }

            // 6) Ghi nhớ chỗ an toàn (4 bánh chạm đất, xe đứng thẳng) mỗi 0.5 giây
            safeTimer += dt;
            if (grounded == 4 && transform.up.y > 0.8f && safeTimer > 0.5f)
            {
                safeTimer = 0f;
                lastSafePosition = transform.position;
                Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (f.sqrMagnitude > 0.01f) lastSafeRotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            }

            // 7) Rơi khỏi bản đồ → đưa về chỗ an toàn
            if (fallResetDepth > 0f && transform.position.y < lastSafePosition.y - fallResetDepth)
            {
                Debug.LogWarning($"[CarController] {name} rơi khỏi bản đồ tại {transform.position} – đưa về chỗ an toàn. " +
                                 "Chỗ xe rơi có thể chưa có collider.", this);
                Teleport(lastSafePosition + Vector3.up * 1.5f, lastSafeRotation);
                return;
            }

            // 8) Tự lật lại khi bị lật
            if (autoResetDelay > 0f)
            {
                bool flipped = transform.up.y < 0.3f && rb.linearVelocity.magnitude < 2f;
                flippedTimer = flipped ? flippedTimer + dt : 0f;
                if (flippedTimer > autoResetDelay) ResetUpright();
            }
        }

        // ───────────────────────── Hình bánh xe ─────────────────────────

        void Update()
        {
            wheelSpin = (wheelSpin + ForwardSpeed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime) % 360f;
            for (int i = 0; i < wheelVisuals.Length && i < 4; i++)
            {
                var w = wheelVisuals[i];
                if (w == null) continue;
                float d = wheelHit[i] ? wheelDist[i] : suspensionLength;
                w.localPosition = anchors[i] - Vector3.up * d;
                w.localRotation = Quaternion.Euler(0f, i < 2 ? Steer * maxVisualSteerAngle : 0f, 0f);
                if (w.childCount > 0)
                    w.GetChild(0).localRotation = Quaternion.Euler(wheelSpin, 0f, 0f) * wheelBaseRot[i];
            }
        }

        void OnGUI()
        {
            if (!showDebugHud) return;
            int hits = 0;
            for (int i = 0; i < 4; i++) if (wheelHit[i]) hits++;
            GUI.Label(new Rect(10, 10, 400, 22), $"{name}: {SpeedKmh:0} km/h | Bánh chạm đất: {hits}/4 | Ga: {Throttle:0.0} | Lái: {Steer:0.0}");
        }

        void OnDrawGizmosSelected()
        {
            RecalculateAnchors();
            Gizmos.color = Color.yellow;
            for (int i = 0; i < 4; i++)
            {
                Vector3 o = transform.TransformPoint(anchors[i]);
                Gizmos.DrawLine(o, o - transform.up * (suspensionLength + wheelRadius));
                Gizmos.DrawWireSphere(o - transform.up * suspensionLength, wheelRadius);
            }
        }
    }
}
