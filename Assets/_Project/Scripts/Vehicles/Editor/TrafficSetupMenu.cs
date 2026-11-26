using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PoliceChase.Police;

namespace PoliceChase.Vehicles.EditorTools
{
    /// <summary>
    /// Menu Tools > Police Chase > Giao thông: biến các ô tô trang trí trong thành phố thành xe tự chạy.
    /// - Bỏ "Static" (xe static không di chuyển được),
    /// - thêm NavMeshModifier "Ignore From Build" (để NavMesh không bị thủng chỗ xe đỗ),
    /// - thêm TrafficCar + tạo TrafficManager.
    /// </summary>
    public static class TrafficSetupMenu
    {
        static readonly string[] Include = { "car", "taxi", "police", "bus", "truck", "van", "jeep", "suv", "pickup", "sedan", "vehicle", "ambulance" };
        static readonly string[] Exclude = { "wheel", "door", "glass", "light", "part", "tire", "tyre", "window", "cart", "card", "carpet", "caret", "scar", "stop", "station", "sign", "garage", "parking", "road", "bush", "spoiler", "rim", "seat", "mirror", "bumper", "hood", "trunk_lid", "plate", "collider", "traffic_" };

        const string Root = "Tools/Police Chase/Giao thông/";

        [MenuItem(Root + "1. Tự tìm ô tô trong thành phố", false, 200)]
        static void FindAll()
        {
            var found = FindCandidates();
            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("Giao thông", "Không tìm thấy ô tô nào theo tên (car, taxi, bus, truck...).\n\nHãy chọn các xe trong Hierarchy rồi dùng mục 2.", "OK");
                return;
            }
            string sample = string.Join("\n", found.Take(12).Select(g => "• " + g.name)) + (found.Count > 12 ? $"\n... và {found.Count - 12} xe khác" : "");
            int pick = EditorUtility.DisplayDialogComplex("Giao thông",
                $"Tìm thấy {found.Count} ô tô:\n\n{sample}\n\nBiến tất cả thành xe tự chạy?",
                "Biến tất cả", "Huỷ", "Chỉ chọn trong Hierarchy để xem");
            if (pick == 1) return;
            Selection.objects = found.Cast<UnityEngine.Object>().ToArray();
            if (pick == 2) return;
            Apply(found);
        }

        [MenuItem(Root + "2. Biến xe đang chọn thành xe giao thông", false, 201)]
        static void ConvertSelected()
        {
            var list = Selection.gameObjects.Where(g => g.scene.IsValid()).Distinct().ToList();
            list = list.Where(g => !list.Any(o => o != g && g.transform.IsChildOf(o.transform))).ToList();   // bỏ object con trùng
            list = list.Where(g => !IsPlayerOrPolice(g)).ToList();
            if (list.Count == 0)
            {
                EditorUtility.DisplayDialog("Giao thông", "Hãy chọn (Ctrl + click) các ô tô trong Hierarchy trước.\nXe người chơi và xe cảnh sát sẽ được bỏ qua.", "OK");
                return;
            }
            Apply(list);
        }

        [MenuItem(Root + "3. Chọn tất cả xe giao thông", false, 220)]
        static void SelectAll()
        {
            Selection.objects = UnityEngine.Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(t => (UnityEngine.Object)t.gameObject).ToArray();
            Debug.Log($"[Traffic] Đang có {Selection.objects.Length} xe giao thông.");
        }

        [MenuItem(Root + "4. Gỡ xe giao thông đang chọn", false, 221)]
        static void RemoveSelected()
        {
            int n = 0;
            foreach (var g in Selection.gameObjects)
            {
                var t = g.GetComponentInParent<TrafficCar>();
                if (t == null) continue;
                var mod = ModifierType != null ? t.GetComponent(ModifierType) : null;
                if (mod != null) Undo.DestroyObjectImmediate(mod);
                Undo.DestroyObjectImmediate(t);
                n++;
            }
            Debug.Log($"[Traffic] Đã gỡ {n} xe giao thông. (Muốn xe đứng yên làm vật cản thì Bake lại NavMesh.)");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // ───────────────────────── Thực hiện ─────────────────────────

        static void Apply(List<GameObject> cars)
        {
            Undo.SetCurrentGroupName("Tạo xe giao thông");
            int group = Undo.GetCurrentGroup();
            int added = 0;
            foreach (var g in cars)
            {
                foreach (var t in g.GetComponentsInChildren<Transform>(true))
                {
                    Undo.RecordObject(t.gameObject, "Bỏ Static");
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    t.gameObject.isStatic = false;
                }
                if (ModifierType != null && g.GetComponent(ModifierType) == null)
                {
                    var mod = Undo.AddComponent(g, ModifierType);
                    var p = ModifierType.GetProperty("ignoreFromBuild");
                    if (p != null) p.SetValue(mod, true);
                }
                if (g.GetComponent<TrafficCar>() == null) { Undo.AddComponent<TrafficCar>(g); added++; }
            }

            if (UnityEngine.Object.FindFirstObjectByType<TrafficManager>() == null)
            {
                var m = new GameObject("TrafficManager");
                Undo.RegisterCreatedObjectUndo(m, "Tạo TrafficManager");
                m.AddComponent<TrafficManager>();
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Giao thông",
                $"Đã biến {added} xe thành xe giao thông (tổng {cars.Count} xe được chọn).\n\n" +
                "VIỆC CẦN LÀM TIẾP:\n" +
                "1. Chọn object có NavMeshSurface → bấm Bake (để chỗ xe đỗ không còn là lỗ trên NavMesh).\n" +
                "2. Ctrl + S để lưu scene.\n" +
                "3. Bấm Play.", "OK");
        }

        static List<GameObject> FindCandidates()
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var cand = new HashSet<Transform>();
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();
                if (!Include.Any(n.Contains) || Exclude.Any(n.Contains)) continue;
                if (t.GetComponentInParent<TrafficCar>() != null || IsPlayerOrPolice(t.gameObject)) continue;
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                float len = Mathf.Max(b.size.x, b.size.z);
                if (len < 2.2f || len > 16f || b.size.y > 6f) continue;     // không phải 1 chiếc xe (nhóm nhiều xe, đồ vật nhỏ...)
                cand.Add(t);
            }
            // Chỉ giữ object cao nhất (bỏ object con của xe đã chọn).
            return cand.Where(t => !HasAncestorIn(t, cand)).Select(t => t.gameObject).OrderBy(g => g.name).ToList();
        }

        static bool HasAncestorIn(Transform t, HashSet<Transform> set)
        {
            for (var p = t.parent; p != null; p = p.parent) if (set.Contains(p)) return true;
            return false;
        }

        static bool IsPlayerOrPolice(GameObject g) =>
            g.CompareTag("Player") ||
            g.GetComponentInParent<CarController>() != null || g.GetComponentInChildren<CarController>() != null ||
            g.GetComponentInParent<PoliceAI>() != null;

        static Type modifierType;
        static bool modifierSearched;
        static Type ModifierType
        {
            get
            {
                if (!modifierSearched)
                {
                    modifierSearched = true;
                    modifierType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType("Unity.AI.Navigation.NavMeshModifier", false))
                        .FirstOrDefault(t => t != null);
                }
                return modifierType;
            }
        }
    }
}
