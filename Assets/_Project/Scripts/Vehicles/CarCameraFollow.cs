using UnityEngine;
using UnityEngine.InputSystem;

namespace PoliceChase.Vehicles
{
    /// <summary>
    /// Camera bám sau xe từ trên cao, nhìn trước theo hướng chạy và tự zoom ra khi chạy nhanh.
    /// Cuộn chuột = zoom ra/vào. Phím C (tay cầm: D-pad lên) = đổi góc nhìn Gần → Xa → Toàn cảnh.
    /// Gắn vào Main Camera, kéo xe vào ô Target.
    /// </summary>
    public class CarCameraFollow : MonoBehaviour
    {
        [System.Serializable]
        public struct ViewPreset
        {
            public string name;
            public float height;
            public float distance;
        }

        public Transform target;

        [Header("Các góc nhìn (phím C để đổi)")]
        public ViewPreset[] presets =
        {
            new ViewPreset { name = "Gần",       height = 14f,  distance = 12f },
            new ViewPreset { name = "Xa",        height = 35f,  distance = 25f },
            new ViewPreset { name = "Toàn cảnh", height = 110f, distance = 35f },
        };
        public int currentPreset = 0;

        [Header("Zoom bằng cuộn chuột")]
        public float zoom = 1f;
        public float minZoom = 0.5f;
        public float maxZoom = 4f;
        public float scrollSensitivity = 0.1f;

        [Header("Theo tốc độ")]
        [Tooltip("Mỗi 1 m/s tốc độ, camera lùi/cao thêm bao nhiêu mét")]
        public float speedZoom = 0.25f;
        [Tooltip("Nhìn trước theo vận tốc bao nhiêu giây")]
        public float lookAhead = 0.4f;

        [Header("Độ mượt")]
        public float positionSmoothTime = 0.25f;
        public float rotationSharpness = 6f;

        [Header("Tầm nhìn")]
        [Tooltip("Khoảng cách nhìn xa tối đa của camera (m)")]
        public float farClip = 3000f;

        Rigidbody targetRb;
        Vector3 smoothVel;
        Camera cam;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null && cam.farClipPlane < farClip) cam.farClipPlane = farClip;
            SnapToTarget();
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    zoom = Mathf.Clamp(zoom * (1f - Mathf.Sign(scroll) * scrollSensitivity), minZoom, maxZoom);
            }

            bool next = false;
            if (Keyboard.current != null) next |= Keyboard.current.cKey.wasPressedThisFrame;
            if (Gamepad.current != null) next |= Gamepad.current.dpad.up.wasPressedThisFrame;
            if (next && presets.Length > 0)
            {
                currentPreset = (currentPreset + 1) % presets.Length;
                zoom = 1f;
            }
        }

        void LateUpdate()
        {
            if (target == null) return;
            Compute(out Vector3 desired, out Vector3 focus);

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref smoothVel, positionSmoothTime);
            Quaternion look = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
        }

        /// <summary>Đặt camera ngay vào vị trí (không trượt mượt) – gọi sau khi spawn/teleport xe.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            Compute(out Vector3 desired, out Vector3 focus);
            transform.position = desired;
            transform.rotation = Quaternion.LookRotation(focus - desired, Vector3.up);
            smoothVel = Vector3.zero;
        }

        void Compute(out Vector3 desired, out Vector3 focus)
        {
            if (targetRb == null) targetRb = target.GetComponent<Rigidbody>();
            Vector3 v = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
            v.y = 0f;
            float extra = v.magnitude * speedZoom;

            float height = 14f, distance = 12f;
            if (presets != null && presets.Length > 0)
            {
                currentPreset = Mathf.Clamp(currentPreset, 0, presets.Length - 1);
                height = presets[currentPreset].height;
                distance = presets[currentPreset].distance;
            }

            Vector3 flatFwd = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;
            flatFwd.Normalize();

            focus = target.position + v * lookAhead;
            desired = focus - flatFwd * (distance * zoom + extra) + Vector3.up * (height * zoom + extra);
        }
    }
}
