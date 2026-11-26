using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PoliceChase.UI
{
    /// <summary>
    /// Phông nền 3D cho menu garage, dựng hoàn toàn bằng code:
    /// - ảnh thành phố đêm (vẽ thủ tục) đặt xa phía sau camera,
    /// - bệ xe kim loại tối có các vòng neon xanh phát sáng + vòng sáng dưới sàn,
    /// - ánh sáng xanh, ambient tối, bloom + vignette (URP).
    /// </summary>
    public static class MenuBackdrop
    {
        public const float Horizon = 0.56f;   // vị trí đường chân trời trong ảnh nền (0 = đáy, 1 = đỉnh)

        public static void Build(Camera cam, Transform root)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.02f, 0.06f);
            cam.allowHDR = true;
            if (cam.farClipPlane < 200f) cam.farClipPlane = 200f;

            BuildSky(cam);
            BuildPlatform(root);
            BuildLights(root);
            BuildPostFx(cam);
        }

        // ───────────────────────── Ảnh thành phố ─────────────────────────

        static void BuildSky(Camera cam)
        {
            var go = new GameObject("BackdropCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 120f;
            canvas.sortingOrder = -100;

            var raw = UIKit.NewRect(go.transform, "City").gameObject.AddComponent<RawImage>();
            raw.texture = CityTexture(1024, 512, 7);
            raw.raycastTarget = false;
            var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 2f;
        }

        /// <summary>Vẽ thành phố đêm: trời gradient, 3 lớp nhà có cửa sổ sáng, biển neon, mặt sàn phản chiếu.</summary>
        public static Texture2D CityTexture(int w, int h, int seed)
        {
            var rnd = new System.Random(seed);
            int hy = Mathf.RoundToInt(h * Horizon);

            // Lớp nhà: chiều cao theo cột + mã toà nhà để vẽ cửa sổ.
            int layers = 3;
            var height = new int[layers, w];
            var bid = new int[layers, w];
            var bx0 = new int[layers, w];
            Color[] body = { new Color(0.07f, 0.11f, 0.26f), new Color(0.045f, 0.075f, 0.18f), new Color(0.025f, 0.04f, 0.1f) };
            float[] litChance = { 0.12f, 0.2f, 0.24f };
            for (int l = 0; l < layers; l++)
            {
                int x = 0, id = 0;
                while (x < w)
                {
                    int bw = rnd.Next(18, 60) + l * 10;
                    float edge = Mathf.Abs((x + bw * 0.5f) / w - 0.5f) * 2f;            // 0 ở giữa, 1 ở mép
                    float maxH = l == 0 ? 0.26f : (l == 1 ? 0.36f : 0.6f);
                    float centerDip = l == 2 ? Mathf.Lerp(0.25f, 1f, edge * edge) : Mathf.Lerp(0.55f, 1f, edge);
                    int bh = Mathf.RoundToInt(h * maxH * centerDip * (0.45f + (float)rnd.NextDouble() * 0.55f));
                    if (l == 2 && edge < 0.35f && rnd.NextDouble() < 0.6) bh /= 3;                // chừa chỗ cho xe ở giữa
                    bool spire = rnd.NextDouble() < 0.15;
                    for (int i = x; i < Mathf.Min(w, x + bw); i++)
                    {
                        int hh = bh;
                        if (spire && Mathf.Abs(i - (x + bw / 2)) < 3) hh += h / 14;
                        height[l, i] = hh;
                        bid[l, i] = id;
                        bx0[l, i] = x;
                    }
                    x += bw + rnd.Next(0, 6);
                    id++;
                }
            }

            // Biển neon nhỏ trên lớp giữa
            var neon = new System.Collections.Generic.List<RectInt>();
            var neonCol = new System.Collections.Generic.List<Color>();
            for (int i = 0; i < 10; i++)
            {
                int nx = rnd.Next(0, w - 30);
                if (Mathf.Abs(nx / (float)w - 0.5f) < 0.18f) continue;
                int top = height[1, nx];
                if (top < 30) continue;
                neon.Add(new RectInt(nx + 2, hy + top - rnd.Next(14, 40), rnd.Next(10, 26), rnd.Next(3, 6)));
                neonCol.Add(rnd.NextDouble() < 0.5 ? new Color(1f, 0.25f, 0.7f) : new Color(0.2f, 0.9f, 1f));
            }

            var px = new Color[w * h];
            // Phần trên đường chân trời
            for (int y = hy; y < h; y++)
            {
                float t = (y - hy) / (float)(h - hy);
                Color sky = Color.Lerp(new Color(0.08f, 0.15f, 0.38f), new Color(0.01f, 0.02f, 0.07f), Mathf.Pow(t, 0.6f));
                sky += new Color(0.08f, 0.2f, 0.5f) * Mathf.Exp(-t * t * 60f) * 0.8f;   // quầng sáng chân trời
                for (int x = 0; x < w; x++)
                {
                    Color c = sky;
                    int ry = y - hy;
                    for (int l = 2; l >= 0; l--)
                    {
                        if (ry >= height[l, x]) continue;
                        c = body[l];
                        // cửa sổ
                        int lx = x - bx0[l, x], ly = ry;
                        int cw = 5 + l, chh = 7 + l;
                        if (lx % cw < cw - 2 && ly % chh < chh - 3 && lx > 1 && ly > 2 && ly < height[l, x] - 3)
                        {
                            uint hash = (uint)(bid[l, x] * 73856093 ^ (lx / cw) * 19349663 ^ (ly / chh) * 83492791 ^ l * 2654435761);
                            hash ^= hash >> 13; hash *= 0x5bd1e995; hash ^= hash >> 15;
                            float r01 = (hash & 0xffff) / 65535f;
                            if (r01 < litChance[l])
                            {
                                float k = (hash >> 16 & 0xff) / 255f;
                                Color win = k < 0.4f ? new Color(1f, 0.8f, 0.42f) : (k < 0.85f ? new Color(0.45f, 0.72f, 1f) : new Color(0.85f, 0.92f, 1f));
                                float dim = l == 0 ? 0.35f : (l == 1 ? 0.6f : 0.85f);
                                c = Color.Lerp(c, win, dim);
                            }
                        }
                        // ánh sáng xanh hắt từ dưới lên
                        c += new Color(0.02f, 0.06f, 0.16f) * Mathf.Exp(-ry / 40f);
                        break;
                    }
                    px[y * w + x] = c;
                }
            }
            for (int i = 0; i < neon.Count; i++)
            {
                var r = neon[i];
                for (int y = r.yMin - 3; y < r.yMax + 3; y++)
                    for (int x = r.xMin - 3; x < r.xMax + 3; x++)
                    {
                        if (x < 0 || x >= w || y < hy || y >= h) continue;
                        bool core = x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;
                        px[y * w + x] = core ? neonCol[i] : Color.Lerp(px[y * w + x], neonCol[i], 0.25f);
                    }
            }
            // Đèn đường dọc chân trời
            for (int x = 6; x < w; x += 22)
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int yy = hy + 4 + dy, xx = x + dx;
                        if (xx < 0 || xx >= w) continue;
                        float k = Mathf.Exp(-(dx * dx + dy * dy) / 2.5f);
                        px[yy * w + xx] = Color.Lerp(px[yy * w + xx], new Color(1f, 0.85f, 0.5f), k);
                    }

            // Mặt sàn bóng: phản chiếu mờ + vệt sáng dọc + lưới phối cảnh
            for (int y = 0; y < hy; y++)
            {
                float t = (hy - y) / (float)hy;                    // 0 ở chân trời, 1 ở đáy
                Color floor = Color.Lerp(new Color(0.05f, 0.09f, 0.2f), new Color(0.01f, 0.02f, 0.05f), Mathf.Pow(t, 0.5f));
                for (int x = 0; x < w; x++)
                {
                    // phản chiếu nén theo chiều dọc, làm mờ ngang + dọc
                    int my = hy + (hy - y) / 2;
                    Color refl = new Color(0f, 0f, 0f, 0f);
                    for (int j = 0; j < 4; j++)
                        for (int dx = -3; dx <= 3; dx += 3)
                        {
                            int sy = Mathf.Min(h - 1, my + j * 3), sx = Mathf.Clamp(x + dx, 0, w - 1);
                            refl += px[sy * w + sx];
                        }
                    refl = refl * (1f / 12f);
                    float k = 0.3f * (1f - t) * (1f - t);
                    Color c = floor + refl * k;
                    // lưới phối cảnh
                    float depth = 1f / Mathf.Max(0.02f, t);
                    float gx = ((x / (float)w) - 0.5f) * depth * 6f;
                    float lineX = Mathf.Abs(Mathf.Repeat(gx, 1f) - 0.5f);
                    float lineZ = Mathf.Abs(Mathf.Repeat(depth * 0.8f, 1f) - 0.5f);
                    float grid = Mathf.Max(Mathf.Clamp01(1f - (0.5f - lineX) * depth * 1.4f), Mathf.Clamp01(1f - (0.5f - lineZ) * 9f));
                    c += new Color(0.1f, 0.3f, 0.8f) * grid * 0.12f * Mathf.Clamp01(t * 2f);
                    px[y * w + x] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CityBackdrop" };
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ───────────────────────── Bệ xe ─────────────────────────

        static void BuildPlatform(Transform root)
        {
            var dark = LitMaterial(new Color(0.05f, 0.07f, 0.13f), 0.85f, 0.6f);
            var darkInner = LitMaterial(new Color(0.07f, 0.1f, 0.19f), 0.9f, 0.4f);
            var neon = GlowMaterial(new Color(0.25f, 0.6f, 1f) * 3.2f);
            var neonDim = GlowMaterial(new Color(0.2f, 0.45f, 1f) * 1.4f);

            Disc(root, "Base", 8f, 0.3f, -0.15f, dark);
            Disc(root, "RimGlow", 8.35f, 0.08f, -0.22f, neon);
            Disc(root, "Ring", 7.3f, 0.02f, 0.011f, neon);
            Disc(root, "Top", 6.9f, 0.03f, 0.016f, darkInner);
            Disc(root, "InnerRing", 4.6f, 0.035f, 0.02f, neonDim);
            Disc(root, "Center", 4.35f, 0.04f, 0.024f, darkInner);

            // Đèn mốc nhỏ quanh viền (giống ảnh mẫu)
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m.name = "Marker";
                Object.Destroy(m.GetComponent<Collider>());
                m.transform.SetParent(root, false);
                m.transform.localPosition = new Vector3(Mathf.Sin(a) * 4.02f, -0.05f, Mathf.Cos(a) * 4.02f);
                m.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                m.transform.localScale = new Vector3(0.7f, 0.12f, 0.08f);
                m.GetComponent<MeshRenderer>().sharedMaterial = GlowMaterial(new Color(0.6f, 0.85f, 1f) * 4f);
            }

            // Vòng sáng hắt xuống sàn
            var glow = new GameObject("FloorGlow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(root, false);
            glow.sprite = GameSkin.FloorGlow;
            glow.color = new Color(0.25f, 0.55f, 1f, 0.9f);
            glow.transform.localPosition = new Vector3(0f, -0.27f, 0f);
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glow.transform.localScale = Vector3.one * 10.5f;
        }

        static void Disc(Transform parent, string name, float diameter, float height, float topY, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);   // Cylinder cao 2 đơn vị
            go.transform.localPosition = new Vector3(0f, topY - height * 0.5f, 0f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material LitMaterial(Color c, float smooth, float metal)
        {
            var src = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var m = new Material(src.GetComponent<MeshRenderer>().sharedMaterial);
            Object.Destroy(src);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        /// <summary>Vật liệu tự phát sáng (không chịu ánh sáng) – màu HDR > 1 để bloom toả sáng.</summary>
        static Material GlowMaterial(Color hdr)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            Material m;
            if (sh != null)
            {
                m = new Material(sh);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
                if (m.HasProperty("_Color")) m.SetColor("_Color", hdr);
            }
            else
            {
                m = LitMaterial(new Color(Mathf.Min(1f, hdr.r), Mathf.Min(1f, hdr.g), Mathf.Min(1f, hdr.b)), 0.5f, 0f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", hdr);
            }
            return m;
        }

        // ───────────────────────── Ánh sáng + hậu kỳ ─────────────────────────

        static void BuildLights(Transform root)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.22f, 0.4f);
            RenderSettings.fog = false;

            var sun = Object.FindFirstObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.color = new Color(0.82f, 0.88f, 1f);
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(38f, -30f, 0f);

            Point(root, "BlueBack", new Vector3(0f, 3f, 5f), new Color(0.25f, 0.5f, 1f), 4f, 14f);
            Point(root, "CyanLeft", new Vector3(-6f, 2f, -2f), new Color(0.3f, 0.85f, 1f), 2.5f, 12f);
            Point(root, "PinkRight", new Vector3(6f, 1.5f, 1f), new Color(0.9f, 0.35f, 1f), 1.6f, 10f);
        }

        static void Point(Transform root, string name, Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = pos;
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
        }

        static void BuildPostFx(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;

            var vol = new GameObject("MenuPostFX").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 50f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.3f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.72f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.5f);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(12f);
            ca.contrast.Override(8f);
            vol.sharedProfile = profile;
        }
    }
}
