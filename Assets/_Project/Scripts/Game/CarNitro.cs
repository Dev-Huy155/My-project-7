using UnityEngine;
using UnityEngine.InputSystem;
using PoliceChase.Vehicles;

namespace PoliceChase.Game
{
    /// <summary>
    /// Nitro: giữ Shift (tay cầm: RB) để tăng tốc. Bình nitro tự nạp lại khi không dùng.
    /// Dung lượng (giây) do cấp nâng cấp Nitro quyết định; 0 = chưa có nitro.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarNitro : MonoBehaviour
    {
        public float capacitySeconds = 0f;
        [Tooltip("Số giây để nạp đầy từ rỗng")]
        public float rechargeSeconds = 12f;
        public float speedMultiplier = 1.35f;
        public float accelerationMultiplier = 1.9f;
        public float pushForce = 8f;

        public float Charge01 => capacitySeconds > 0f ? charge / capacitySeconds : 0f;
        public bool Active { get; private set; }

        CarController car;
        Rigidbody rb;
        float charge, baseMaxSpeed, baseAccel;

        void Awake()
        {
            car = GetComponent<CarController>();
            rb = GetComponent<Rigidbody>();
        }

        /// <summary>Gọi sau khi đã gán chỉ số xe (để nitro biết tốc độ gốc).</summary>
        public void Init(float capacity)
        {
            capacitySeconds = capacity;
            charge = capacity;
            baseMaxSpeed = car.maxSpeed;
            baseAccel = car.acceleration;
        }

        void Update()
        {
            if (baseMaxSpeed <= 0f) { baseMaxSpeed = car.maxSpeed; baseAccel = car.acceleration; }

            bool wants = false;
            var kb = Keyboard.current;
            if (kb != null) wants |= kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            var gp = Gamepad.current;
            if (gp != null) wants |= gp.rightShoulder.isPressed;

            Active = wants && capacitySeconds > 0f && charge > 0f && car.Throttle > 0.1f;
            if (Active) charge = Mathf.Max(0f, charge - Time.deltaTime);
            else if (capacitySeconds > 0f) charge = Mathf.Min(capacitySeconds, charge + capacitySeconds / rechargeSeconds * Time.deltaTime);

            car.maxSpeed = Active ? baseMaxSpeed * speedMultiplier : baseMaxSpeed;
            car.acceleration = Active ? baseAccel * accelerationMultiplier : baseAccel;
        }

        void FixedUpdate()
        {
            if (Active && car.IsGrounded && rb != null && car.ForwardSpeed < baseMaxSpeed * speedMultiplier)
                rb.AddForce(transform.forward * pushForce, ForceMode.Acceleration);
        }
    }
}
