using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PoliceChase.Vehicles.EditorTools
{
    /// <summary>
    /// Tools > Police Chase > Tìm script bị thiếu
    ///   Liệt kê mọi object trong scene đang mở có component "Missing (Mono Script)", chọn sẵn chúng trong Hierarchy.
    /// Tools > Police Chase > Xoá các script bị thiếu
    ///   Gỡ các component hỏng đó (có hỏi xác nhận). Sau đó Add Component lại script đúng nếu cần.
    /// </summary>
    public static class MissingScriptFinder
    {
        [MenuItem("Tools/Police Chase/Tìm script bị thiếu")]
        static void Find()
        {
            var found = Collect();
            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("Tìm script bị thiếu", "Không có object nào bị thiếu script trong scene đang mở.", "OK");
                return;
            }
            foreach (var go in found)
                Debug.LogWarning($"[MissingScriptFinder] '{Path(go)}' có {GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)} component bị thiếu script. " +
                                 "Bấm vào dòng này để chọn object.", go);
            Selection.objects = found.ToArray();
            EditorUtility.DisplayDialog("Tìm script bị thiếu",
                $"Tìm thấy {found.Count} object bị thiếu script (đã chọn sẵn trong Hierarchy, xem danh sách trong Console):\n\n" +
                string.Join("\n", found.ConvertAll(Path)), "OK");
        }

        [MenuItem("Tools/Police Chase/Xoá các script bị thiếu")]
        static void Remove()
        {
            var found = Collect();
            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("Xoá script bị thiếu", "Không có object nào bị thiếu script.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Xoá script bị thiếu",
                    $"Gỡ component hỏng khỏi {found.Count} object:\n\n" + string.Join("\n", found.ConvertAll(Path)) +
                    "\n\nObject vẫn giữ nguyên, chỉ bỏ component hỏng. Tiếp tục?", "Gỡ", "Huỷ"))
                return;

            int removed = 0;
            foreach (var go in found)
            {
                Undo.RegisterCompleteObjectUndo(go, "Xoá script bị thiếu");
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[MissingScriptFinder] Đã gỡ {removed} component hỏng. Nhớ Ctrl+S để lưu scene.");
        }

        static List<GameObject> Collect()
        {
            var result = new List<GameObject>();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                            result.Add(t.gameObject);
            }
            return result;
        }

        static string Path(GameObject go)
        {
            string p = go.name;
            var t = go.transform.parent;
            while (t != null) { p = t.name + "/" + p; t = t.parent; }
            return p;
        }
    }
}
