using UnityEngine;

namespace PoliceChase.Vehicles
{
    /// <summary>
    /// Gắn 1 model xe (vd. xe trong bộ Cartoon City) làm hình dáng cho xe dùng CarController:
    /// - xoá collider/rigidbody có sẵn trong model (xe dùng BoxCollider + Rigidbody riêng),
    /// - xoay cho đầu xe hướng +Z nếu model nằm ngang, co giãn theo chiều dài mong muốn,
    /// - đặt gầm xe chạm mặt đường, chỉnh BoxCollider thân xe và vị trí 4 bánh theo model.
    /// Chạy được cả khi xe đang tắt (inactive) vì tính kích thước từ mesh, không từ Renderer.
    /// </summary>
    public static class CarModelFitter
    {
        public static Bounds Attach(Transform carRoot, GameObject model, BoxCollider bodyCollider, CarController car, float targetLength = 4.4f)
        {
            var t = model.transform;
            t.SetParent(carRoot, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            model.name = "Visual";

            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);

            if (!TryLocalBounds(carRoot, model, out Bounds b)) return new Bounds(Vector3.zero, new Vector3(1.9f, 1.4f, 4.4f));

            // Model nằm ngang (dài theo X) → xoay 90° cho đầu xe theo trục Z.
            if (b.size.x > b.size.z * 1.2f)
            {
                t.localRotation = Quaternion.Euler(0f, 90f, 0f);
                TryLocalBounds(carRoot, model, out b);
            }

            float s = targetLength / Mathf.Max(0.01f, b.size.z);
            t.localScale *= s;
            TryLocalBounds(carRoot, model, out b);

            // Độ cao mặt đường so với tâm xe khi xe đứng yên.
            float restCompression = car.mass * 9.81f / 4f / Mathf.Max(1f, car.springStrength);
            float groundY = car.wheelAnchorHeight - (car.suspensionLength - restCompression + car.wheelRadius);
            t.localPosition += new Vector3(-b.center.x, groundY - b.min.y, -b.center.z);
            TryLocalBounds(carRoot, model, out b);

            // Thân xe: chừa gầm 0.45m để không cọ mặt đường.
            float bottom = b.min.y + 0.45f;
            bodyCollider.center = new Vector3(0f, (bottom + b.max.y) * 0.5f, 0f);
            bodyCollider.size = new Vector3(b.size.x * 0.95f, Mathf.Max(0.3f, b.max.y - bottom), b.size.z * 0.95f);

            // 4 bánh theo kích thước model.
            car.wheelOffset = new Vector2(b.size.x * 0.4f, b.size.z * 0.32f);
            car.wheelVisuals = new Transform[0];
            return b;
        }

        /// <summary>Hộp bao của mọi mesh trong model, tính trong toạ độ của carRoot.</summary>
        public static bool TryLocalBounds(Transform carRoot, GameObject model, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) Add(carRoot, mf.transform, mf.sharedMesh.bounds, ref bounds, ref any);
            foreach (var sm in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (sm.sharedMesh != null) Add(carRoot, sm.transform, sm.sharedMesh.bounds, ref bounds, ref any);
            return any;
        }

        static void Add(Transform root, Transform t, Bounds mb, ref Bounds result, ref bool any)
        {
            Vector3 mn = mb.min, mx = mb.max;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
                Vector3 p = root.InverseTransformPoint(t.TransformPoint(corner));
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                else result.Encapsulate(p);
            }
        }
    }
}
