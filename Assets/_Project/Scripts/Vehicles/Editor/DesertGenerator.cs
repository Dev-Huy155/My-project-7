using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoliceChase.Vehicles.EditorTools
{
    /// <summary>
    /// Tools > Police Chase > Tạo vùng sa mạc
    /// Tạo Terrain sa mạc BAO QUANH thành phố (hoặc chỉ 1 phía):
    /// - vùng phẳng sát thành phố, cao bằng mặt đường → nối liền mạch,
    /// - dưới thành phố terrain được hạ thấp 1m để không trồi lên xuyên qua đường,
    /// - cồn cát nhấp nhô, vách cát cao ở rìa ngoài làm ranh giới bản đồ,
    /// - đường nhựa uốn lượn đi ra sa mạc từ MỖI object có tên bắt đầu bằng "DesertRoadStart"
    ///   (không có → tự tạo đường ở giữa mỗi cạnh),
    /// - xương rồng / đá (prefab của bạn, hoặc tự dựng bằng khối cơ bản).
    /// Terrain có sẵn va chạm → xe chạy được, bake NavMesh được.
    /// </summary>
    public class DesertGenerator : EditorWindow
    {
        static readonly string[] ModeNames = { "Bao quanh thành phố", "Phía Đông (+X)", "Phía Tây (-X)", "Phía Bắc (+Z)", "Phía Nam (-Z)" };

        [SerializeField] int mode = 0;
        [SerializeField] float ringWidth = 400f;
        [SerializeField] float width = 800f;
        [SerializeField] float depth = 600f;
        [SerializeField] float duneHeight = 14f;
        [SerializeField] float duneFrequency = 0.006f;
        [SerializeField] float flatBorder = 40f;
        [SerializeField] float rimHeight = 30f;
        [SerializeField] float rimWidth = 60f;
        [SerializeField] float roadWidth = 14f;
        [SerializeField] float roadOffset = 0f;
        [SerializeField] float roadCurve = 60f;
        [SerializeField] int seed = 1234;
        [SerializeField] int decorCount = 400;
        [SerializeField] GameObject[] cactusPrefabs = new GameObject[0];
        [SerializeField] GameObject[] rockPrefabs = new GameObject[0];

        const string Folder = "Assets/_Project/Generated/Desert";
        const string RootName = "DesertArea";
        const float BaseH = 1.05f; // độ cao (so với gốc terrain) của vùng phẳng = mặt đường thành phố

        struct Road { public Vector2 S, D, L; }

        // Trạng thái trong lúc tạo
        float cMinX, cMaxX, cMinZ, cMaxZ;   // hình chữ nhật thành phố
        float tMinX, tMinZ, tSX, tSZ;       // hình chữ nhật terrain
        bool rimMinX, rimMaxX, rimMinZ, rimMaxZ;
        readonly List<Road> roads = new List<Road>();
        float offX, offZ;

        SerializedObject so;
        Vector2 scroll;

        [MenuItem("Tools/Police Chase/Tạo vùng sa mạc")]
        static void Open() => GetWindow<DesertGenerator>("Sa mạc");

        void OnSelectionChange() => Repaint();

        void OnGUI()
        {
            if (so == null) so = new SerializedObject(this);
            so.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.HelpBox(
                "1. Trong Hierarchy, chọn TẤT CẢ các khối thành phố đã ghép.\n" +
                "2. Chọn kiểu rồi bấm Tạo. Bấm lại để tạo lại (đổi Seed = cồn cát khác).\n" +
                "Lối ra: tạo object rỗng tên 'DesertRoadStart' (nhiều cái thì Ctrl+D nhân bản) đặt trên mặt đường ở rìa thành phố → " +
                "mỗi cái thành 1 con đường đi vào sa mạc. Không có → tự tạo đường ở giữa mỗi cạnh.",
                MessageType.Info);

            var city = new List<Transform>(Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable));
            EditorGUILayout.LabelField("Khối thành phố đang chọn", city.Count.ToString());
            EditorGUILayout.LabelField("Lối ra (DesertRoadStart)", FindRoadStarts().Count.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Vị trí và kích thước", EditorStyles.boldLabel);
            mode = EditorGUILayout.Popup("Kiểu", mode, ModeNames);
            if (mode == 0)
                ringWidth = EditorGUILayout.Slider("Độ rộng vành sa mạc (m)", ringWidth, 100f, 1500f);
            else
            {
                width = EditorGUILayout.FloatField("Chiều dài dọc thành phố (m)", width);
                depth = EditorGUILayout.FloatField("Chiều sâu ra xa (m)", depth);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cồn cát", EditorStyles.boldLabel);
            duneHeight = EditorGUILayout.Slider("Độ cao cồn cát (m)", duneHeight, 0f, 40f);
            duneFrequency = EditorGUILayout.Slider(new GUIContent("Độ dày cồn cát", "Lớn = nhiều cồn nhỏ, nhỏ = ít cồn to"), duneFrequency, 0.002f, 0.02f);
            flatBorder = EditorGUILayout.Slider("Vùng phẳng sát thành phố (m)", flatBorder, 0f, 150f);
            rimHeight = EditorGUILayout.Slider("Độ cao vách rìa (m)", rimHeight, 0f, 60f);
            rimWidth = EditorGUILayout.Slider("Độ rộng vách rìa (m)", rimWidth, 10f, 150f);
            seed = EditorGUILayout.IntField("Seed", seed);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Đường", EditorStyles.boldLabel);
            roadWidth = EditorGUILayout.Slider("Bề rộng đường (m)", roadWidth, 0f, 30f);
            if (mode != 0)
                roadOffset = EditorGUILayout.FloatField(new GUIContent("Lệch đường (m)", "Chỉ dùng khi không có DesertRoadStart"), roadOffset);
            roadCurve = EditorGUILayout.Slider("Độ uốn lượn (m)", roadCurve, 0f, 200f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Xương rồng và đá", EditorStyles.boldLabel);
            decorCount = EditorGUILayout.IntSlider("Số lượng", decorCount, 0, 3000);
            EditorGUILayout.PropertyField(so.FindProperty("cactusPrefabs"), new GUIContent("Prefab xương rồng (tuỳ chọn)"), true);
            EditorGUILayout.PropertyField(so.FindProperty("rockPrefabs"), new GUIContent("Prefab đá (tuỳ chọn)"), true);
            so.ApplyModifiedProperties();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(city.Count == 0))
            {
                if (GUILayout.Button("Tạo vùng sa mạc", GUILayout.Height(32)))
                    EditorApplication.delayCall += () => Generate(city); // chạy sau khi vẽ xong cửa sổ
            }
            if (GUILayout.Button("Xoá vùng sa mạc")) EditorApplication.delayCall += ClearExisting;
            EditorGUILayout.EndScrollView();
        }

        // ───────────────────────── Tạo ─────────────────────────

        void Generate(List<Transform> city)
        {
            Physics.SyncTransforms();
            if (!CityBounds(city, out Bounds cb))
            {
                EditorUtility.DisplayDialog("Tạo vùng sa mạc", "Các object đang chọn không có hình để đo vị trí thành phố.", "OK");
                return;
            }
            cMinX = cb.min.x; cMaxX = cb.max.x; cMinZ = cb.min.z; cMaxZ = cb.max.z;
            float cx = cb.center.x, cz = cb.center.z;

            // Hình chữ nhật terrain + rìa nào có vách
            rimMinX = rimMaxX = rimMinZ = rimMaxZ = true;
            switch (mode)
            {
                case 0: tMinX = cMinX - ringWidth; tMinZ = cMinZ - ringWidth; tSX = cMaxX - cMinX + 2f * ringWidth; tSZ = cMaxZ - cMinZ + 2f * ringWidth; break;
                case 1: tMinX = cMaxX; tMinZ = cz - width / 2f; tSX = depth; tSZ = width; rimMinX = false; break;
                case 2: tMinX = cMinX - depth; tMinZ = cz - width / 2f; tSX = depth; tSZ = width; rimMaxX = false; break;
                case 3: tMinX = cx - width / 2f; tMinZ = cMaxZ; tSX = width; tSZ = depth; rimMinZ = false; break;
                default: tMinX = cx - width / 2f; tMinZ = cMinZ - depth; tSX = width; tSZ = depth; rimMaxZ = false; break;
            }

            BuildRoads();
            float groundY = FindGroundY(cb);

            ClearExisting();
            EnsureFolder(Folder);

            var rnd = new System.Random(seed);
            offX = (float)rnd.NextDouble() * 1000f;
            offZ = (float)rnd.NextDouble() * 1000f;
            float maxH = BaseH + Mathf.Max(duneHeight * 1.6f, rimHeight) + 2f;
            var origin = new Vector3(tMinX, groundY - BaseH, tMinZ);

            // ── Độ cao ──
            int res = Mathf.Max(tSX, tSZ) > 900f ? 1025 : 513;
            var data = new TerrainData { heightmapResolution = res };
            data.size = new Vector3(tSX, maxH, tSZ);
            data.alphamapResolution = res - 1;
            var heights = new float[res, res];
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    float x = tMinX + xi / (float)(res - 1) * tSX;
                    float z = tMinZ + zi / (float)(res - 1) * tSZ;
                    heights[zi, xi] = Mathf.Clamp01(HeightAt(x, z) / maxH);
                }
            data.SetHeights(0, 0, heights);

            // ── Màu: cát sáng, cát sẫm, nhựa đường ──
            data.terrainLayers = new[]
            {
                MakeLayer("Sand_Light", new Color(0.93f, 0.80f, 0.55f), new Color(0.88f, 0.74f, 0.48f), 6f),
                MakeLayer("Sand_Dark", new Color(0.82f, 0.62f, 0.38f), new Color(0.76f, 0.56f, 0.33f), 6f),
                MakeLayer("Asphalt", new Color(0.22f, 0.22f, 0.24f), new Color(0.18f, 0.18f, 0.20f), 4f),
            };
            int ar = data.alphamapResolution;
            var alpha = new float[ar, ar, 3];
            for (int zi = 0; zi < ar; zi++)
                for (int xi = 0; xi < ar; xi++)
                {
                    float x = tMinX + (xi + 0.5f) / ar * tSX;
                    float z = tMinZ + (zi + 0.5f) / ar * tSZ;
                    float road = roadWidth > 0f && CityDist(x, z) > -1f
                        ? 1f - Smooth01((MinRoadDist(x, z) - roadWidth * 0.5f) / 1.5f) : 0f;
                    float dark = Smooth01((Mathf.PerlinNoise(x * 0.02f + offX, z * 0.02f + offZ) - 0.45f) / 0.2f);
                    alpha[zi, xi, 2] = road;
                    alpha[zi, xi, 1] = (1f - road) * dark;
                    alpha[zi, xi, 0] = (1f - road) * (1f - dark);
                }
            data.SetAlphamaps(0, 0, alpha);

            string dataPath = Folder + "/Desert_TerrainData.asset";
            AssetDatabase.DeleteAsset(dataPath);
            AssetDatabase.CreateAsset(data, dataPath);

            var root = new GameObject(RootName);
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "DesertTerrain";
            terrainGo.transform.SetParent(root.transform, false);
            terrainGo.transform.position = origin;
            var terrain = terrainGo.GetComponent<Terrain>();

            // ── Vạch kẻ giữa đường ──
            if (roadWidth > 0f)
            {
                var lineMat = GetMaterial("Desert_RoadLine", new Color(0.97f, 0.85f, 0.25f));
                var lines = new GameObject("RoadLines").transform;
                lines.SetParent(root.transform, false);
                foreach (var r in roads)
                {
                    for (float along = 4f; along < 5000f; along += 6f)
                    {
                        Vector2 a2 = RoadPoint(r, along), b2 = RoadPoint(r, along + 3f);
                        if (!InsideTerrain(b2.x, b2.y) || EdgeDist(b2.x, b2.y) < rimWidth * 0.8f) break;
                        Vector3 a = new Vector3(a2.x, 0f, a2.y), b = new Vector3(b2.x, 0f, b2.y);
                        Vector3 mid = (a + b) * 0.5f;
                        mid.y = terrain.SampleHeight(mid) + origin.y + 0.03f;
                        var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Object.DestroyImmediate(dash.GetComponent<Collider>());
                        dash.name = "Dash";
                        dash.transform.SetParent(lines, false);
                        dash.transform.position = mid;
                        dash.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                        dash.transform.localScale = new Vector3(0.3f, 0.02f, 3f);
                        dash.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
                    }
                }
            }

            // ── Xương rồng và đá ──
            var decor = new GameObject("Decor").transform;
            decor.SetParent(root.transform, false);
            var cactusMat = GetMaterial("Desert_Cactus", new Color(0.30f, 0.58f, 0.25f));
            var rockMat = GetMaterial("Desert_Rock", new Color(0.62f, 0.42f, 0.30f));
            int placed = 0;
            for (int i = 0; i < decorCount * 6 && placed < decorCount; i++)
            {
                float x = tMinX + (float)rnd.NextDouble() * tSX;
                float z = tMinZ + (float)rnd.NextDouble() * tSZ;
                if (CityDist(x, z) < flatBorder * 0.6f) continue;
                if (EdgeDist(x, z) < rimWidth * 0.6f) continue;
                if (roadWidth > 0f && MinRoadDist(x, z) < roadWidth * 0.5f + 8f) continue;

                var pos = new Vector3(x, 0f, z);
                pos.y = terrain.SampleHeight(pos) + origin.y;
                bool cactus = rnd.NextDouble() < 0.55;
                float yaw = (float)rnd.NextDouble() * 360f;
                float scale = 0.8f + (float)rnd.NextDouble() * 0.6f;
                var prefab = PickPrefab(cactus ? cactusPrefabs : rockPrefabs, rnd);

                GameObject go;
                if (prefab != null)
                {
                    go = PrefabUtility.InstantiatePrefab(prefab, decor) as GameObject;
                    if (go == null) go = Object.Instantiate(prefab, decor);
                    go.transform.position = pos;
                    go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    go.transform.localScale *= scale;
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                else go = cactus ? BuildCactus(decor, pos, yaw, scale, cactusMat, rnd) : BuildRock(decor, pos, yaw, scale, rockMat, rnd);
                go.isStatic = true;
                placed++;
            }

            Undo.RegisterCreatedObjectUndo(root, "Tạo vùng sa mạc");
            Selection.activeGameObject = terrainGo;
            EditorSceneManager.MarkSceneDirty(root.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DesertGenerator] Đã tạo sa mạc '{ModeNames[mode]}' {tSX:0} × {tSZ:0} m, mặt đất cao {groundY:0.##}, " +
                      $"{roads.Count} đường ra, {placed} xương rồng/đá. Nhớ Bake lại NavMesh.");
        }

        // ───────────────────────── Đường ─────────────────────────

        static List<Transform> FindRoadStarts()
        {
            var list = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name.StartsWith("DesertRoadStart")) list.Add(t);
            return list;
        }

        void BuildRoads()
        {
            roads.Clear();
            var starts = FindRoadStarts();
            if (starts.Count > 0)
            {
                foreach (var s in starts) roads.Add(RoadFromPoint(s.position.x, s.position.z));
                return;
            }

            float cx = (cMinX + cMaxX) / 2f, cz = (cMinZ + cMaxZ) / 2f;
            switch (mode)
            {
                case 0:
                    roads.Add(RoadFromPoint(cMaxX, cz));
                    roads.Add(RoadFromPoint(cMinX, cz));
                    roads.Add(RoadFromPoint(cx, cMaxZ));
                    roads.Add(RoadFromPoint(cx, cMinZ));
                    break;
                case 1: roads.Add(RoadFromPoint(cMaxX, cz + roadOffset)); break;
                case 2: roads.Add(RoadFromPoint(cMinX, cz + roadOffset)); break;
                case 3: roads.Add(RoadFromPoint(cx + roadOffset, cMaxZ)); break;
                default: roads.Add(RoadFromPoint(cx + roadOffset, cMinZ)); break;
            }
        }

        // Đường bắt đầu ở cạnh thành phố gần điểm nhất, đi thẳng ra ngoài.
        Road RoadFromPoint(float x, float z)
        {
            float dE = Mathf.Abs(cMaxX - x), dW = Mathf.Abs(x - cMinX), dN = Mathf.Abs(cMaxZ - z), dS = Mathf.Abs(z - cMinZ);
            float m = Mathf.Min(Mathf.Min(dE, dW), Mathf.Min(dN, dS));
            var r = new Road();
            if (m == dE) { r.S = new Vector2(cMaxX, z); r.D = Vector2.right; }
            else if (m == dW) { r.S = new Vector2(cMinX, z); r.D = Vector2.left; }
            else if (m == dN) { r.S = new Vector2(x, cMaxZ); r.D = Vector2.up; }
            else { r.S = new Vector2(x, cMinZ); r.D = Vector2.down; }
            r.L = new Vector2(-r.D.y, r.D.x);
            return r;
        }

        Vector2 RoadPoint(Road r, float along) => r.S + r.D * along + r.L * (Mathf.Sin(along * 0.008f) * roadCurve);

        float RoadDist(Road r, float x, float z)
        {
            Vector2 rel = new Vector2(x, z) - r.S;
            float along = Vector2.Dot(rel, r.D);
            if (along < -3f) return float.MaxValue;
            float lat = Vector2.Dot(rel, r.L);
            return Mathf.Abs(lat - Mathf.Sin(Mathf.Max(0f, along) * 0.008f) * roadCurve);
        }

        float MinRoadDist(float x, float z)
        {
            float best = float.MaxValue;
            foreach (var r in roads) best = Mathf.Min(best, RoadDist(r, x, z));
            return best;
        }

        // ───────────────────────── Hình dạng ─────────────────────────

        float HeightAt(float x, float z)
        {
            float v = CityDist(x, z);
            if (v < 0f) return BaseH - Smooth01(-v / 3f); // hạ thấp 1m dưới thành phố

            float n = 0f, amp = 1f, freq = duneFrequency, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                n += Mathf.PerlinNoise(x * freq + offX + o * 31.7f, z * freq + offZ + o * 17.3f) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            n /= norm;
            float dunes = Mathf.Pow(n, 1.5f) * duneHeight * 1.6f;

            float border = Smooth01((v - flatBorder) / 60f);
            float roadF = roadWidth > 0f ? Smooth01((MinRoadDist(x, z) - (roadWidth * 0.5f + 4f)) / 25f) : 1f;
            float h = BaseH + dunes * border * roadF;

            float rim = 1f - Smooth01(EdgeDist(x, z) / rimWidth);
            return Mathf.Max(h, BaseH + rim * rim * rimHeight);
        }

        // Khoảng cách tới hình chữ nhật thành phố (âm = nằm trong thành phố)
        float CityDist(float x, float z)
        {
            float dx = Mathf.Max(cMinX - x, 0f, x - cMaxX);
            float dz = Mathf.Max(cMinZ - z, 0f, z - cMaxZ);
            if (dx > 0f || dz > 0f) return Mathf.Sqrt(dx * dx + dz * dz);
            return -Mathf.Min(Mathf.Min(x - cMinX, cMaxX - x), Mathf.Min(z - cMinZ, cMaxZ - z));
        }

        // Khoảng cách tới rìa ngoài của terrain (chỉ các rìa có vách)
        float EdgeDist(float x, float z)
        {
            float d = float.MaxValue;
            if (rimMinX) d = Mathf.Min(d, x - tMinX);
            if (rimMaxX) d = Mathf.Min(d, tMinX + tSX - x);
            if (rimMinZ) d = Mathf.Min(d, z - tMinZ);
            if (rimMaxZ) d = Mathf.Min(d, tMinZ + tSZ - z);
            return d;
        }

        bool InsideTerrain(float x, float z) => x >= tMinX && x <= tMinX + tSX && z >= tMinZ && z <= tMinZ + tSZ;

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // ───────────────────────── Đo thành phố ─────────────────────────

        static bool CityBounds(List<Transform> city, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var t in city)
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var rb = r.bounds;
                    if (rb.size.y > 0.75f || rb.size.x * rb.size.z < 4f) continue; // chỉ vật phẳng (đường, vỉa hè, nền)
                    if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
                }
            if (any) return true;
            foreach (var t in city)
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
            return any;
        }

        float FindGroundY(Bounds cb)
        {
            foreach (var s in FindRoadStarts())
                if (Physics.Raycast(s.position + Vector3.up * 5f, Vector3.down, out RaycastHit sh, 50f,
                                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return sh.point.y;

            // Dò dọc 4 cạnh thành phố (ngay bên trong mép), lấy điểm thấp nhất = mặt đường
            float best = float.MaxValue;
            for (int i = 1; i < 10; i++)
            {
                float t = i / 10f;
                var pts = new[]
                {
                    new Vector3(cb.max.x - 3f, 0f, Mathf.Lerp(cb.min.z, cb.max.z, t)),
                    new Vector3(cb.min.x + 3f, 0f, Mathf.Lerp(cb.min.z, cb.max.z, t)),
                    new Vector3(Mathf.Lerp(cb.min.x, cb.max.x, t), 0f, cb.max.z - 3f),
                    new Vector3(Mathf.Lerp(cb.min.x, cb.max.x, t), 0f, cb.min.z + 3f),
                };
                foreach (var p0 in pts)
                {
                    var p = p0;
                    p.y = cb.max.y + 200f;
                    if (Physics.Raycast(p, Vector3.down, out RaycastHit hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        best = Mathf.Min(best, hit.point.y);
                }
            }
            return best < float.MaxValue ? best : cb.min.y;
        }

        // ───────────────────────── Đồ trang trí dựng sẵn ─────────────────────────

        static GameObject BuildCactus(Transform parent, Vector3 pos, float yaw, float s, Material mat, System.Random rnd)
        {
            var root = new GameObject("Cactus");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            float h = (2.5f + (float)rnd.NextDouble() * 2f) * s;
            Prim(PrimitiveType.Capsule, root.transform, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.7f, h * 0.5f, 0.7f), Quaternion.identity, mat, true);
            if (rnd.NextDouble() < 0.8)
            {
                Prim(PrimitiveType.Capsule, root.transform, new Vector3(0.6f, h * 0.55f, 0f), new Vector3(0.45f, 0.5f, 0.45f), Quaternion.Euler(0f, 0f, 90f), mat, false);
                Prim(PrimitiveType.Capsule, root.transform, new Vector3(1.0f, h * 0.7f, 0f), new Vector3(0.45f, h * 0.18f, 0.45f), Quaternion.identity, mat, false);
            }
            if (rnd.NextDouble() < 0.6)
            {
                Prim(PrimitiveType.Capsule, root.transform, new Vector3(-0.55f, h * 0.4f, 0f), new Vector3(0.4f, 0.45f, 0.4f), Quaternion.Euler(0f, 0f, 90f), mat, false);
                Prim(PrimitiveType.Capsule, root.transform, new Vector3(-0.9f, h * 0.52f, 0f), new Vector3(0.4f, h * 0.14f, 0.4f), Quaternion.identity, mat, false);
            }
            return root;
        }

        static GameObject BuildRock(Transform parent, Vector3 pos, float yaw, float s, Material mat, System.Random rnd)
        {
            var root = new GameObject("Rock");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            float w = (1.5f + (float)rnd.NextDouble() * 3f) * s;
            float h = (0.8f + (float)rnd.NextDouble() * 1.8f) * s;
            Prim(PrimitiveType.Sphere, root.transform, new Vector3(0f, h * 0.35f, 0f), new Vector3(w, h, w * 0.8f), Quaternion.Euler(0f, 0f, (float)rnd.NextDouble() * 20f), mat, true);
            return root;
        }

        static void Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Quaternion rot, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static GameObject PickPrefab(GameObject[] arr, System.Random rnd)
        {
            if (arr == null || arr.Length == 0) return null;
            return arr[rnd.Next(arr.Length)];
        }

        // ───────────────────────── Asset ─────────────────────────

        static TerrainLayer MakeLayer(string name, Color a, Color b, float tile)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
            var rnd = new System.Random(name.GetHashCode());
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.7f + (float)rnd.NextDouble() * 0.3f;
                    tex.SetPixel(x, y, Color.Lerp(a, b, n));
                }
            tex.Apply(true);

            string texPath = $"{Folder}/{name}_Tex.asset";
            string layerPath = $"{Folder}/{name}.terrainlayer";
            AssetDatabase.DeleteAsset(texPath);
            AssetDatabase.DeleteAsset(layerPath);
            AssetDatabase.CreateAsset(tex, texPath);
            var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile) };
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        static Material GetMaterial(string name, Color color)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            var shader = Shader.Find(srp ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void ClearExisting()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
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
