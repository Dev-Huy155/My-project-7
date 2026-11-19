using UnityEditor;
using UnityEngine;

namespace PoliceChase.Vehicles.EditorTools
{
    /// <summary>
    /// GameObject > Police Chase > Thêm va chạm cho object đang chọn:
    /// thêm Mesh Collider cho object đang chọn VÀ tất cả object con có hình (MeshFilter)
    /// mà chưa có collider nào. Có thể Undo (Ctrl+Z).
    /// </summary>
    public static class ColliderTools
    {
        [MenuItem("GameObject/Police Chase/Thêm va chạm cho object đang chọn", false, 20)]
        static void AddMeshColliders()
        {
            var selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Thêm va chạm", "Hãy chọn ít nhất 1 object trong Hierarchy trước.", "OK");
                return;
            }

            int added = 0, skipped = 0;
            Undo.SetCurrentGroupName("Add Mesh Colliders");
            int group = Undo.GetCurrentGroup();

            foreach (var root in selected)
            {
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    if (mf.GetComponent<Collider>() != null) { skipped++; continue; }
                    var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                    mc.sharedMesh = mf.sharedMesh;
                    added++;
                }
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[PoliceChase] Đã thêm {added} Mesh Collider (bỏ qua {skipped} object đã có collider).");
        }

        [MenuItem("GameObject/Police Chase/Thêm va chạm cho object đang chọn", true)]
        static bool ValidateAddMeshColliders() => Selection.gameObjects.Length > 0;
    }
}
