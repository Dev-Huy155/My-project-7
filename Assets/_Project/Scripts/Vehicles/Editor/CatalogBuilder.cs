using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PoliceChase.Game;

namespace PoliceChase.Vehicles.EditorTools
{
    /// <summary>
    /// Tools > Police Chase > Tạo danh sách xe tự động
    /// Tìm mọi prefab xe trong project (tên chứa Car, Taxi, Police, Bus, Truck, Van, Jeep, SUV...),
    /// tạo Assets/_Project/Resources/CarCatalog.asset với giá và chỉ số tăng dần. Xe đầu tiên miễn phí.
    /// Sau đó bạn có thể chỉnh tay tên / giá / chỉ số trong Inspector của file CarCatalog.
    /// </summary>
    public static class CatalogBuilder
    {
        const string Folder = "Assets/_Project/Resources";
        const string AssetPath = Folder + "/CarCatalog.asset";

        static readonly string[] Include = { "car", "taxi", "police", "bus", "truck", "van", "jeep", "suv", "pickup", "sedan", "vehicle", "ambulance" };
        static readonly string[] Exclude = { "wheel", "door", "glass", "light", "part", "tire", "tyre", "window", "cart", "card", "carpet", "caret", "scar", "stop", "station", "sign", "garage", "parking", "road", "bush", "spoiler", "rim", "seat", "mirror", "bumper", "hood", "trunk_lid", "plate" };
        static readonly int[] Prices = { 0, 600, 1500, 3000, 5000, 8000, 12000, 18000, 25000, 35000 };

        [MenuItem("Tools/Police Chase/Tạo danh sách xe tự động")]
        static void Build()
        {
            var found = new List<GameObject>();
            var names = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                string n = Path.GetFileNameWithoutExtension(path);
                string lower = n.ToLowerInvariant();
                if (!Contains(lower, Include) || Contains(lower, Exclude)) continue;
                if (!names.Add(n)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponentInChildren<Renderer>(true) == null) continue;
                found.Add(go);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("Tạo danh sách xe",
                    "Không tìm thấy prefab xe nào (tên chứa Car, Taxi, Police, Bus...). " +
                    "Bạn có thể tạo tay: Assets > Create > Police Chase > Car Catalog, đặt vào Assets/_Project/Resources.", "OK");
                return;
            }
            if (found.Count > Prices.Length) found.RemoveRange(Prices.Length, found.Count - Prices.Length);

            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(AssetPath);
            if (catalog != null && catalog.cars.Count > 0 &&
                !EditorUtility.DisplayDialog("Tạo danh sách xe",
                    $"Đã có danh sách {catalog.cars.Count} xe. Tạo lại sẽ ghi đè tên, giá và chỉ số. Tiếp tục?", "Ghi đè", "Huỷ"))
                return;

            if (catalog == null)
            {
                EnsureFolder(Folder);
                catalog = ScriptableObject.CreateInstance<CarCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            catalog.cars.Clear();
            catalog.startingCoins = 100;
            for (int i = 0; i < found.Count; i++)
            {
                float t = found.Count > 1 ? i / (float)(found.Count - 1) : 0f;
                catalog.cars.Add(new CarDef
                {
                    id = found[i].name.ToLowerInvariant(),
                    displayName = found[i].name.Replace('_', ' '),
                    model = found[i],
                    price = Prices[i],
                    maxSpeed = Mathf.Lerp(30f, 42f, t),
                    acceleration = Mathf.Lerp(12f, 18f, t),
                    turnRate = Mathf.Lerp(100f, 120f, t),
                    grip = Mathf.Lerp(8.5f, 11f, t),
                    mass = Mathf.Lerp(1100f, 1500f, t),
                });
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
            Debug.Log($"[CatalogBuilder] Đã tạo danh sách {catalog.cars.Count} xe tại {AssetPath}: " +
                      string.Join(", ", catalog.cars.ConvertAll(c => $"{c.displayName} ({c.price} xu)")));
        }

        static bool Contains(string s, string[] words)
        {
            foreach (var w in words) if (s.Contains(w)) return true;
            return false;
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
