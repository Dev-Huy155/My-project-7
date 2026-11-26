using UnityEngine;
using UnityEngine.InputSystem;

namespace PoliceChase.Vehicles
{
    /// <summary>
    /// Đọc bàn phím / tay cầm và điều khiển CarController.
    /// Bàn phím: W/S hoặc ↑/↓ = ga/phanh-lùi, A/D hoặc ←/→ = lái, Space = phanh tay, R = lật xe lại.
    /// Tay cầm: RT = ga, LT = phanh/lùi, cần trái = lái, A = phanh tay, Y = lật xe lại.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class PlayerCarInput : MonoBehaviour
    {
        CarController car;

        void Awake() => car = GetComponent<CarController>();

        void Update()
        {
            float throttle = 0f, steer = 0f;
            bool handbrake = false, reset = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                handbrake |= kb.spaceKey.isPressed;
                reset |= kb.rKey.wasPressedThisFrame;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                throttle += gp.rightTrigger.ReadValue() - gp.leftTrigger.ReadValue();
                steer += gp.leftStick.ReadValue().x;
                handbrake |= gp.buttonSouth.isPressed;
                reset |= gp.buttonNorth.wasPressedThisFrame;
            }

            // Nút cảm ứng (điện thoại)
            throttle += MobileInput.Throttle;
            steer += MobileInput.Steer;
            handbrake |= MobileInput.Handbrake;

            car.SetInput(throttle, steer, handbrake);
            if (reset) car.ResetUpright();
        }
    }
}
