using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoliceChase.Vehicles.EditorTools
{
    /// <summary>
    /// Menu GameObject > Police Chase > Player Car:
    /// tạo xe người chơi (Rigidbody, collider, CarController, PlayerCarInput) và gắn camera bám xe.
    /// - Nếu đang chọn 1 model/prefab xe trong cửa sổ Project → dùng model đó làm hình xe.
    /// - Nếu không → dựng xe bằng khối hộp màu đỏ.
    /// Vị trí: object tên "PlayerSpawn" nếu có, nếu không thì mặt đường ở giữa cửa sổ Scene.
    /// </summary>
    public static class CarSetupMenu
    {
        const string MaterialFolder = "Assets/_Project/Generated/CarMaterials";

        [MenuItem("GameObject/Police Chase/Player Car", false, 11)]
        static void CreatePlayerCar(MenuCommand command)
        {
            // Model xe đang chọn trong cửa sổ Project (nếu có)
            GameObject model = Selection.activeObject as GameObject;
            if (model != null && !AssetDatabase.Contains(model)) model = null;

            FindSpawn(out Vector3 pos, out Quaternion rot);

            var root = new GameObject("PlayerCar");
            root.tag = "Player";
            root.transform.SetPositionAndRotation(pos, rot);

            root.AddComponent<Rigidbody>();
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.35f, 0f);
            col.size = new Vector3(1.9f, 0.9f, 4.3f);
            var car = root.AddComponent<CarController>();

            if (model != null)
            {
                var inst = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (inst == null) inst = Object.Instantiate(model);
                if (PrefabUtility.IsPartOfPrefabInstance(inst))
                    PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                CarModelFitter.Attach(root.transform, inst, col, car);
            }
            else
            {
                BuildBoxVisual(root.transform, car);
            }

            root.AddComponent<PlayerCarInput>();

            // ── Camera ──
            var cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
            }
            cam.gameObject.tag = "MainCamera";
            if (cam.farClipPlane < 1500f) cam.farClipPlane = 1500f;
            var follow = cam.GetComponent<CarCameraFollow>();
            if (follow == null) follow = Undo.AddComponent<CarCameraFollow>(cam.gameObject);
            follow.target = root.transform;
            follow.SnapToTarget();

            Undo.RegisterCreatedObjectUndo(root, "Create Player Car");
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[PoliceChase] Đã tạo PlayerCar tại {pos}" + (model != null ? $" với model '{model.name}'" : " (xe khối hộp)") +
                      ". Bấm Play: W A S D để lái, Space = phanh tay, R = lật xe lại, C = đổi góc camera.", root);
        }

        [MenuItem("GameObject/Police Chase/Đổi model xe người chơi", false, 12)]
        static void ChangePlayerModel(MenuCommand command)
        {
            GameObject model = Selection.activeObject as GameObject;
            if (model == null || !AssetDatabase.Contains(model))
            {
                EditorUtility.DisplayDialog("Đổi model xe",
                    "Hãy bấm chọn 1 model xe trong cửa sổ PROJECT trước (biểu tượng khối xanh kẻ sọc), rồi chọn lại menu này.", "OK");
                return;
            }
            var player = GameObject.FindWithTag("Player");
            var car = player != null ? player.GetComponent<CarController>() : null;
            var col = player != null ? player.GetComponent<BoxCollider>() : null;
            if (car == null || col == null)
            {
                EditorUtility.DisplayDialog("Đổi model xe",
                    "Chưa có xe người chơi trong scene (object có tag Player + Car Controller + Box Collider). " +
                    "Hãy tạo bằng GameObject > Police Chase > Player Car.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Đổi model xe người chơi");
            int group = Undo.GetCurrentGroup();
            Undo.RecordObjects(new Object[] { car, col }, "Đổi model xe người chơi");

            // Xoá hình cũ (model cũ hoặc khối hộp + 4 bánh)
            var old = new List<GameObject>();
            foreach (Transform c in player.transform)
                if (c.name == "Visual" || c.name.StartsWith("Wheel_")) old.Add(c.gameObject);
            foreach (var g in old) Undo.DestroyObjectImmediate(g);

            var inst = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (inst == null) inst = Object.Instantiate(model);
            if (PrefabUtility.IsPartOfPrefabInstance(inst))
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(inst, "Đổi model xe người chơi");
            CarModelFitter.Attach(player.transform, inst, col, car);

            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(car);
            EditorUtility.SetDirty(col);
            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(player.scene);
            Debug.Log($"[PoliceChase] Đã đổi xe người chơi sang model '{model.name}'.", player);
        }

        static void BuildBoxVisual(Transform root, CarController car)
        {
            var bodyMat = GetMaterial("Car_Body", new Color(0.85f, 0.15f, 0.12f));
            var glassMat = GetMaterial("Car_Glass", new Color(0.1f, 0.12f, 0.15f));
            var wheelMat = GetMaterial("Car_Wheel", new Color(0.08f, 0.08f, 0.08f));
            var lightMat = GetMaterial("Car_Light", new Color(1f, 0.95f, 0.7f));

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            Part(PrimitiveType.Cube, "Body", visual, new Vector3(0f, 0.35f, 0f), new Vector3(1.9f, 0.7f, 4.3f), Quaternion.identity, bodyMat);
            Part(PrimitiveType.Cube, "Cabin", visual, new Vector3(0f, 0.95f, -0.3f), new Vector3(1.7f, 0.55f, 2.2f), Quaternion.identity, glassMat);
            Part(PrimitiveType.Cube, "HeadlightL", visual, new Vector3(-0.6f, 0.4f, 2.16f), new Vector3(0.4f, 0.15f, 0.05f), Quaternion.identity, lightMat);
            Part(PrimitiveType.Cube, "HeadlightR", visual, new Vector3(0.6f, 0.4f, 2.16f), new Vector3(0.4f, 0.15f, 0.05f), Quaternion.identity, lightMat);

            string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            float x = car.wheelOffset.x, z = car.wheelOffset.y, y = car.wheelAnchorHeight - car.suspensionLength * 0.8f;
            Vector3[] positions = { new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(-x, y, -z), new Vector3(x, y, -z) };
            car.wheelVisuals = new Transform[4];
            float d = car.wheelRadius * 2f;
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject(names[i]).transform;
                pivot.SetParent(root, false);
                pivot.localPosition = positions[i];
                Part(PrimitiveType.Cylinder, "Mesh", pivot, Vector3.zero, new Vector3(d, 0.15f, d), Quaternion.Euler(0f, 0f, 90f), wheelMat);
                car.wheelVisuals[i] = pivot;
            }
        }

        static void FindSpawn(out Vector3 pos, out Quaternion rot)
        {
            Physics.SyncTransforms();
            var spawn = GameObject.Find("PlayerSpawn");
            Vector3 probe;
            rot = Quaternion.identity;
            if (spawn != null)
            {
                probe = spawn.transform.position;
                Vector3 f = Vector3.ProjectOnPlane(spawn.transform.forward, Vector3.up);
                if (f.sqrMagnitude > 0.01f) rot = Quaternion.LookRotation(f.normalized, Vector3.up);
            }
            else
            {
                var sv = SceneView.lastActiveSceneView;
                probe = sv != null ? sv.pivot : Vector3.zero;
                if (sv != null)
                {
                    Vector3 f = Vector3.ProjectOnPlane(sv.rotation * Vector3.forward, Vector3.up);
                    if (f.sqrMagnitude > 0.01f) rot = Quaternion.LookRotation(f.normalized, Vector3.up);
                }
            }

            if (Physics.Raycast(probe + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 2000f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                pos = hit.point + Vector3.up * 1.2f;
            else
            {
                pos = probe + Vector3.up * 1.2f;
                Debug.LogWarning("[PoliceChase] Không tìm thấy mặt đất có collider bên dưới – xe có thể rơi. Hãy kéo xe đặt lên mặt đường.");
            }
        }

        static void Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Quaternion localRot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Material GetMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            bool srp = GraphicsSettings.currentRenderPipeline != null;
            var shader = Shader.Find(srp ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
