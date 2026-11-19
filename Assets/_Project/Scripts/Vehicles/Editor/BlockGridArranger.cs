using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PoliceChase.Vehicles.EditorTools
{
    /// <summary>
    /// Tools > Police Chase > Xếp khối thành lưới
    /// Chọn các khối thành phố trong Hierarchy → bấm "Xếp": các khối được đặt sát nhau thành lưới,
    /// cùng độ cao, cùng góc xoay. Khối đầu tiên (theo thứ tự Hierarchy) giữ nguyên vị trí.
    /// Kích thước khối được đo theo các vật PHẲNG (đường, vỉa hè, nền) để cây / biển quảng cáo
    /// nhô ra ngoài không làm hở khe. Có thể Undo (Ctrl+Z).
    /// </summary>
    public class BlockGridArranger : EditorWindow
    {
        int columns = 3;
        float gap = 0f;
        bool flatOnly = true;
        bool rowsDown = true;

        [MenuItem("Tools/Police Chase/Xếp khối thành lưới")]
        static void Open() => GetWindow<BlockGridArranger>("Xếp khối");

        void OnSelectionChange() => Repaint();

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "1. Trong Hierarchy, chọn các khối thành phố (bấm khối đầu, giữ Shift, bấm khối cuối).\n" +
                "2. Chọn số cột rồi bấm Xếp. Khối đầu tiên giữ nguyên vị trí, các khối khác xếp sát vào.",
                MessageType.Info);

            var blocks = GetBlocks();
            EditorGUILayout.LabelField("Đang chọn", blocks.Count + " khối");
            columns = EditorGUILayout.IntSlider("Số cột", columns, 1, 10);
            gap = EditorGUILayout.FloatField(new GUIContent("Khoảng hở (m)", "0 = sát nhau. Số âm = chồng lên nhau một chút"), gap);
            flatOnly = EditorGUILayout.Toggle(new GUIContent("Đo theo mặt đường",
                "Chỉ đo các vật phẳng (đường, vỉa hè, nền) để cây, biển quảng cáo nhô ra không làm hở khe"), flatOnly);
            rowsDown = EditorGUILayout.Toggle(new GUIContent("Hàng mới xếp xuống dưới", "Bỏ tích = hàng mới xếp lên trên (trục +Z)"), rowsDown);

            if (blocks.Count > 0 && TryFootprint(blocks[0], out Bounds fp))
                EditorGUILayout.LabelField("Kích thước 1 khối", $"{fp.size.x:0.##} m × {fp.size.z:0.##} m");

            using (new EditorGUI.DisabledScope(blocks.Count < 2))
            {
                if (GUILayout.Button("Xếp", GUILayout.Height(32))) EditorApplication.delayCall += () => Arrange(blocks);
            }
        }

        List<Transform> GetBlocks()
        {
            var list = new List<Transform>(Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable));
            list.Sort((a, b) => CompareHierarchy(a, b));
            return list;
        }

        static int CompareHierarchy(Transform a, Transform b)
        {
            var pa = Path(a);
            var pb = Path(b);
            for (int i = 0; i < Mathf.Min(pa.Count, pb.Count); i++)
                if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
            return pa.Count.CompareTo(pb.Count);
        }

        static List<int> Path(Transform t)
        {
            var p = new List<int>();
            while (t != null) { p.Insert(0, t.GetSiblingIndex()); t = t.parent; }
            return p;
        }

        bool TryFootprint(Transform block, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            var renderers = block.GetComponentsInChildren<Renderer>();

            if (flatOnly)
            {
                foreach (var r in renderers)
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var rb = r.bounds;
                    bool flat = rb.size.y < 0.75f && rb.size.x * rb.size.z > 4f;
                    if (!flat) continue;
                    if (!any) { bounds = rb; any = true; } else bounds.Encapsulate(rb);
                }
            }

            if (!any)
            {
                foreach (var r in renderers)
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                }
            }
            return any;
        }

        void Arrange(List<Transform> blocks)
        {
            Undo.RecordObjects(blocks.ToArray(), "Xếp khối thành lưới");
            var anchor = blocks[0];

            // Cùng góc xoay và độ cao với khối đầu tiên
            for (int i = 1; i < blocks.Count; i++)
            {
                var t = blocks[i];
                t.rotation = anchor.rotation;
                var p = t.position;
                p.y = anchor.position.y;
                t.position = p;
            }

            if (!TryFootprint(anchor, out Bounds a))
            {
                Debug.LogWarning("[BlockGridArranger] Khối đầu tiên không có hình (Renderer) để đo kích thước.");
                return;
            }
            float w = a.size.x, d = a.size.z;

            for (int i = 1; i < blocks.Count; i++)
            {
                int row = i / columns, col = i % columns;
                float targetMinX = a.min.x + col * (w + gap);
                float targetMinZ = rowsDown ? a.min.z - row * (d + gap) : a.min.z + row * (d + gap);

                var t = blocks[i];
                if (!TryFootprint(t, out Bounds b)) continue;
                t.position += new Vector3(targetMinX - b.min.x, 0f, targetMinZ - b.min.z);
            }

            EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
            Debug.Log($"[BlockGridArranger] Đã xếp {blocks.Count} khối thành lưới {columns} cột, mỗi khối {w:0.##} × {d:0.##} m. " +
                      "Nhớ Bake lại NavMesh.");
        }
    }
}
