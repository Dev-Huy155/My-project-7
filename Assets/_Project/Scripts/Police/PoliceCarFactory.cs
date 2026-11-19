using UnityEngine;
using UnityEngine.Rendering;
using PoliceChase.Vehicles;

namespace PoliceChase.Police
{
    /// <summary>
    /// Dựng xe cảnh sát bằng khối hộp lúc đang chơi (không cần prefab):
    /// thân xanh đậm + sọc trắng, đèn còi đỏ/xanh nhấp nháy, 4 bánh, CarController + PoliceAI.
    /// </summary>
    public static class PoliceCarFactory
    {
        static Material bodyMat, whiteMat, glassMat, wheelMat, redMat, blueMat;

        /// <param name="model">Tuỳ chọn: model xe (vd. từ bộ Cartoon City). Null = dựng xe khối hộp.</param>
        public static PoliceAI Build(Vector3 position, Quaternion rotation, Transform target, GameObject model = null)
        {
            if (model != null) return BuildFromModel(position, rotation, target, model);

            EnsureMaterials();

            var root = new GameObject("PoliceCar");
            root.SetActive(false); // gắn xong hết rồi mới bật, để Awake của các script chạy đúng
            root.transform.SetPositionAndRotation(position, rotation);

            root.AddComponent<Rigidbody>();
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.35f, 0f);
            col.size = new Vector3(1.9f, 0.9f, 4.4f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Part(PrimitiveType.Cube, "Body", visual, new Vector3(0f, 0.35f, 0f), new Vector3(1.9f, 0.7f, 4.4f), Quaternion.identity, bodyMat);
            Part(PrimitiveType.Cube, "DoorStripe", visual, new Vector3(0f, 0.4f, 0f), new Vector3(1.94f, 0.3f, 1.6f), Quaternion.identity, whiteMat);
            Part(PrimitiveType.Cube, "Cabin", visual, new Vector3(0f, 0.95f, -0.3f), new Vector3(1.7f, 0.55f, 2.2f), Quaternion.identity, glassMat);
            var red = Part(PrimitiveType.Cube, "LightRed", visual, new Vector3(-0.35f, 1.3f, -0.2f), new Vector3(0.6f, 0.18f, 0.3f), Quaternion.identity, redMat);
            var blue = Part(PrimitiveType.Cube, "LightBlue", visual, new Vector3(0.35f, 1.3f, -0.2f), new Vector3(0.6f, 0.18f, 0.3f), Quaternion.identity, blueMat);

            var car = root.AddComponent<CarController>();
            car.maxSpeed = 33f;
            car.acceleration = 15f;
            car.showDebugHud = false;

            string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            float x = car.wheelOffset.x, z = car.wheelOffset.y, y = car.wheelAnchorHeight - car.suspensionLength * 0.8f;
            Vector3[] positions = { new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(-x, y, -z), new Vector3(x, y, -z) };
            car.wheelVisuals = new Transform[4];
            float d = car.wheelRadius * 2f;
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject(names[i]).transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = positions[i];
                Part(PrimitiveType.Cylinder, "Mesh", pivot, Vector3.zero, new Vector3(d, 0.15f, d), Quaternion.Euler(0f, 0f, 90f), wheelMat);
                car.wheelVisuals[i] = pivot;
            }

            var ai = root.AddComponent<PoliceAI>();
            ai.target = target;
            ai.lightRed = red.GetComponent<Renderer>();
            ai.lightBlue = blue.GetComponent<Renderer>();

            root.SetActive(true);
            return ai;
        }

        static PoliceAI BuildFromModel(Vector3 position, Quaternion rotation, Transform target, GameObject model)
        {
            EnsureMaterials();
            var root = new GameObject("PoliceCar");
            root.SetActive(false);
            root.transform.SetPositionAndRotation(position, rotation);
            root.AddComponent<Rigidbody>();
            var col = root.AddComponent<BoxCollider>();
            var car = root.AddComponent<CarController>();
            car.maxSpeed = 33f;
            car.acceleration = 15f;
            car.showDebugHud = false;

            var inst = Object.Instantiate(model);
            Bounds b = CarModelFitter.Attach(root.transform, inst, col, car);

            // Đèn còi trên nóc xe
            var lights = new GameObject("SirenLights").transform;
            lights.SetParent(root.transform, false);
            float topY = b.max.y + 0.09f;
            var red = Part(PrimitiveType.Cube, "LightRed", lights, new Vector3(-0.35f, topY, b.center.z - 0.2f), new Vector3(0.6f, 0.18f, 0.3f), Quaternion.identity, redMat);
            var blue = Part(PrimitiveType.Cube, "LightBlue", lights, new Vector3(0.35f, topY, b.center.z - 0.2f), new Vector3(0.6f, 0.18f, 0.3f), Quaternion.identity, blueMat);

            var ai = root.AddComponent<PoliceAI>();
            ai.target = target;
            ai.lightRed = red.GetComponent<Renderer>();
            ai.lightBlue = blue.GetComponent<Renderer>();

            root.SetActive(true);
            return ai;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void EnsureMaterials()
        {
            if (bodyMat != null) return;
            bodyMat = Mat(new Color(0.08f, 0.12f, 0.35f));
            whiteMat = Mat(new Color(0.95f, 0.95f, 0.95f));
            glassMat = Mat(new Color(0.1f, 0.12f, 0.15f));
            wheelMat = Mat(new Color(0.08f, 0.08f, 0.08f));
            redMat = Mat(new Color(1f, 0.1f, 0.1f));
            blueMat = Mat(new Color(0.1f, 0.35f, 1f));
        }

        static Material Mat(Color c)
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            var shader = Shader.Find(srp ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }
    }
}
