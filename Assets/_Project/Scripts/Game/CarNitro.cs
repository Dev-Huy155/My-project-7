using System;
using UnityEngine;
using UnityEngine.InputSystem;
using PoliceChase.Vehicles;

namespace PoliceChase.Game
{
    /// <summary>
    /// Tăng tốc tên lửa: giữ Shift (tay cầm: RB, màn hình: nút TĂNG TỐC) để phóng nhanh, cắt đuôi cảnh sát.
    /// Năng lượng nạp lại bằng cách nhặt các điểm năng lượng (BoostPickup) trên bản đồ, và tự hồi rất chậm.
    /// Dung lượng (giây) = 3 giây cơ bản + cấp nâng cấp Nitro.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarNitro : MonoBehaviour
    {
        public float capacitySeconds = 3f;
        [Tooltip("Số giây để tự hồi đầy từ rỗng (0 = không tự hồi, chỉ nạp bằng điểm năng lượng)")]
        public float rechargeSeconds = 45f;
        public float speedMultiplier = 1.45f;
        public float accelerationMultiplier = 2.2f;
        [Tooltip("Lực đẩy thêm khi tăng tốc (m/s²)")]
        public float pushForce = 12f;

        public float Charge01 => capacitySeconds > 0f ? charge / capacitySeconds : 0f;
        public bool Active { get; private set; }
        public bool Wants { get; private set; }
        /// <summary>Gọi khi vừa bắt đầu tăng tốc.</summary>
        public event Action BoostStarted;
        /// <summary>Gọi khi nạp năng lượng (số giây được nạp).</summary>
        public event Action<float> Charged;

        CarController car;
        Rigidbody rb;
        float charge, baseMaxSpeed, baseAccel;

        void Awake()
        {
            car = GetComponent<CarController>();
            rb = GetComponent<Rigidbody>();
            charge = capacitySeconds;
        }

        /// <summary>Gọi sau khi đã gán chỉ số xe (để nitro biết tốc độ gốc).</summary>
        public void Init(float capacity)
        {
            if (car == null) car = GetComponent<CarController>();
            if (rb == null) rb = GetComponent<Rigidbody>();
            capacitySeconds = Mathf.Max(0.5f, capacity);
            charge = capacitySeconds;
            baseMaxSpeed = car.maxSpeed;
            baseAccel = car.acceleration;
        }

        /// <summary>Nạp thêm năng lượng (giây). Trả về số giây thực sự nạp được.</summary>
        public float AddCharge(float seconds)
        {
            float before = charge;
            charge = Mathf.Min(capacitySeconds, charge + seconds);
            float added = charge - before;
            Charged?.Invoke(added);
            return added;
        }

        public void Refill() => AddCharge(capacitySeconds);

        void Update()
        {
            if (baseMaxSpeed <= 0f) { baseMaxSpeed = car.maxSpeed; baseAccel = car.acceleration; }

            bool wants = false;
            var kb = Keyboard.current;
            if (kb != null) wants |= kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            var gp = Gamepad.current;
            if (gp != null) wants |= gp.rightShoulder.isPressed;
            wants |= MobileInput.Nitro;
            Wants = wants;

            bool was = Active;
            Active = wants && capacitySeconds > 0f && charge > 0.02f && car.Throttle > -0.1f;
            if (Active && !was) BoostStarted?.Invoke();

            if (Active) charge = Mathf.Max(0f, charge - Time.deltaTime);
            else if (rechargeSeconds > 0f) charge = Mathf.Min(capacitySeconds, charge + capacitySeconds / rechargeSeconds * Time.deltaTime);

            car.maxSpeed = Active ? baseMaxSpeed * speedMultiplier : baseMaxSpeed;
            car.acceleration = Active ? baseAccel * accelerationMultiplier : baseAccel;
        }

        void FixedUpdate()
        {
            if (Active && car.IsGrounded && rb != null && car.ForwardSpeed < baseMaxSpeed * speedMultiplier)
                rb.AddForce(transform.forward * pushForce, ForceMode.Acceleration);
        }

        void OnDisable()
        {
            Active = false;
            if (car != null && baseMaxSpeed > 0f) { car.maxSpeed = baseMaxSpeed; car.acceleration = baseAccel; }
        }
    }
}
