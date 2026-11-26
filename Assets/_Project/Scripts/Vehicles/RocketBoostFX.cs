using UnityEngine;
using PoliceChase.Game;

namespace PoliceChase.Vehicles
{
    /// <summary>
    /// Hiệu ứng tên lửa ở đuôi xe khi tăng tốc: 2 ống tên lửa bật ra, lửa phụt + khói + ánh sáng cam,
    /// camera mở rộng góc nhìn cho cảm giác tốc độ. Gắn tự động vào xe người chơi (GameplayBootstrap).
    /// </summary>
    [RequireComponent(typeof(CarNitro))]
    public class RocketBoostFX : MonoBehaviour
    {
        public Color flameColor = new Color(1f, 0.55f, 0.12f);
        public float cameraFovKick = 12f;
        public bool showRocketsWhenIdle = false;

        CarNitro nitro;
        Transform rocketsRoot;
        readonly Transform[] flames = new Transform[2];
        readonly Transform[] cores = new Transform[2];
        readonly ParticleSystem[] fire = new ParticleSystem[2];
        readonly ParticleSystem[] smoke = new ParticleSystem[2];
        Light glow;
        float deploy, baseFov = -1f;
        Camera cam;

        static Material unlitCache;

        void Start()
        {
            nitro = GetComponent<CarNitro>();
            Build();
        }

        void Build()
        {
            // Lấy kích thước xe theo BoxCollider (hoặc theo model).
            Vector3 center = new Vector3(0f, 0.6f, 0f), size = new Vector3(1.9f, 1.3f, 4.4f);
            var box = GetComponent<BoxCollider>();
            if (box != null) { center = box.center; size = box.size; }

            rocketsRoot = new GameObject("RocketBoost").transform;
            rocketsRoot.SetParent(transform, false);
            rocketsRoot.localPosition = new Vector3(0f, center.y - size.y * 0.12f, center.z - size.z * 0.5f + 0.15f);

            var metal = LitMaterial(new Color(0.22f, 0.24f, 0.28f), 0.8f, 0.8f);
            var red = LitMaterial(new Color(0.85f, 0.1f, 0.1f), 0.6f, 0.2f);
            var dark = LitMaterial(new Color(0.05f, 0.05f, 0.06f), 0.4f, 0.6f);
            float side = Mathf.Clamp(size.x * 0.3f, 0.35f, 0.8f);

            for (int i = 0; i < 2; i++)
            {
                var r = new GameObject(i == 0 ? "RocketL" : "RocketR").transform;
                r.SetParent(rocketsRoot, false);
                r.localPosition = new Vector3(i == 0 ? -side : side, 0f, 0f);

                Part(r, PrimitiveType.Cylinder, metal, new Vector3(0f, 0f, 0.1f), new Vector3(90f, 0f, 0f), new Vector3(0.3f, 0.45f, 0.3f));     // thân
                Part(r, PrimitiveType.Sphere, red, new Vector3(0f, 0f, 0.55f), Vector3.zero, new Vector3(0.3f, 0.3f, 0.45f));                 // mũi
                Part(r, PrimitiveType.Cylinder, red, new Vector3(0f, 0f, 0.25f), new Vector3(90f, 0f, 0f), new Vector3(0.32f, 0.05f, 0.32f));  // vòng đỏ
                Part(r, PrimitiveType.Cylinder, dark, new Vector3(0f, 0f, -0.4f), new Vector3(90f, 0f, 0f), new Vector3(0.38f, 0.08f, 0.38f)); // loa phụt
                Part(r, PrimitiveType.Cube, metal, new Vector3(0f, 0.2f, -0.2f), Vector3.zero, new Vector3(0.03f, 0.18f, 0.3f));              // cánh
                Part(r, PrimitiveType.Cube, metal, new Vector3(i == 0 ? -0.2f : 0.2f, 0f, -0.2f), Vector3.zero, new Vector3(0.18f, 0.03f, 0.3f));

                // Lửa: lõi trắng-vàng + vỏ cam (sáng không cần đèn)
                var flame = new GameObject("Flame").transform;
                flame.SetParent(r, false);
                flame.localPosition = new Vector3(0f, 0f, -0.48f);
                cores[i] = Part(flame, PrimitiveType.Sphere, Unlit(new Color(2.2f, 2f, 1.6f)), new Vector3(0f, 0f, -0.45f), Vector3.zero, new Vector3(0.22f, 0.22f, 0.9f));
                Part(flame, PrimitiveType.Sphere, Unlit(new Color(flameColor.r * 2f, flameColor.g * 2f, flameColor.b * 2f, 0.85f)), new Vector3(0f, 0f, -0.7f), Vector3.zero, new Vector3(0.34f, 0.34f, 1.5f));
                flames[i] = flame;

                fire[i] = MakeParticles(flame, false);
                smoke[i] = MakeParticles(flame, true);
            }

            glow = new GameObject("BoostLight").AddComponent<Light>();
            glow.transform.SetParent(rocketsRoot, false);
            glow.transform.localPosition = new Vector3(0f, 0.2f, -1.2f);
            glow.type = LightType.Point;
            glow.color = flameColor;
            glow.range = 8f;
            glow.intensity = 0f;

            rocketsRoot.localScale = Vector3.zero;
            SetFlames(false);
        }

        void LateUpdate()
        {
            if (nitro == null || rocketsRoot == null) return;
            bool active = nitro.Active && nitro.enabled;

            // Bật / thu tên lửa
            float target = active || (showRocketsWhenIdle && nitro.Charge01 > 0f) ? 1f : 0f;
            deploy = Mathf.MoveTowards(deploy, target, Time.deltaTime * (target > deploy ? 7f : 2.5f));
            float s = deploy < 1f ? Mathf.SmoothStep(0f, 1f, deploy) * (1f + 0.15f * Mathf.Sin(deploy * Mathf.PI)) : 1f;
            rocketsRoot.localScale = Vector3.one * Mathf.Max(0.0001f, s);

            SetFlames(active);
            if (active)
            {
                for (int i = 0; i < 2; i++)
                {
                    float f = Random.Range(0.75f, 1.25f);
                    flames[i].localScale = new Vector3(Random.Range(0.9f, 1.1f), Random.Range(0.9f, 1.1f), f);
                }
                glow.intensity = Random.Range(2.5f, 4.5f);
            }
            else glow.intensity = Mathf.MoveTowards(glow.intensity, 0f, Time.deltaTime * 20f);

            // Camera mở rộng góc nhìn khi tăng tốc
            if (cam == null) cam = Camera.main;
            if (cam != null && cameraFovKick > 0f)
            {
                if (baseFov < 0f) baseFov = cam.fieldOfView;
                float want = baseFov + (active ? cameraFovKick : 0f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, want, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            }
        }

        void OnDisable()
        {
            if (cam != null && baseFov > 0f) cam.fieldOfView = baseFov;
        }

        void SetFlames(bool on)
        {
            for (int i = 0; i < 2; i++)
            {
                if (flames[i] == null) continue;
                flames[i].gameObject.SetActive(on);
                var e1 = fire[i].emission; e1.enabled = on;
                var e2 = smoke[i].emission; e2.enabled = on;
            }
        }

        // ───────────────────────── Dựng phần tử ─────────────────────────

        static Transform Part(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static Material LitMaterial(Color c, float smooth, float metal)
        {
            var src = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var m = new Material(src.GetComponent<MeshRenderer>().sharedMaterial);
            Destroy(src);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        /// <summary>Vật liệu tự sáng (Sprites/Default luôn có sẵn trong bản build).</summary>
        public static Material Unlit(Color c)
        {
            if (unlitCache == null) unlitCache = new Material(Shader.Find("Sprites/Default"));
            var m = new Material(unlitCache) { color = c };
            return m;
        }

        static Texture2D softTex;
        static Texture2D SoftTexture()
        {
            if (softTex != null) return softTex;
            const int n = 64;
            softTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
                }
            softTex.SetPixels(px);
            softTex.Apply();
            return softTex;
        }

        ParticleSystem MakeParticles(Transform parent, bool isSmoke)
        {
            var go = new GameObject(isSmoke ? "Smoke" : "Fire");
            go.transform.SetParent(parent.parent, false);                 // gắn vào tên lửa (không theo scale nhấp nháy của lửa)
            go.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);  // phun về phía sau
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = isSmoke ? 120 : 200;
            main.startLifetime = isSmoke ? new ParticleSystem.MinMaxCurve(0.5f, 0.9f) : new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startSpeed = isSmoke ? new ParticleSystem.MinMaxCurve(2f, 4f) : new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSize = isSmoke ? new ParticleSystem.MinMaxCurve(0.4f, 0.7f) : new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startColor = isSmoke ? new Color(0.55f, 0.55f, 0.6f, 0.35f) : new Color(1f, 0.85f, 0.5f, 1f);

            var emission = ps.emission;
            emission.rateOverTime = isSmoke ? 28f : 90f;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = isSmoke ? 12f : 6f;
            shape.radius = 0.08f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, isSmoke ? AnimationCurve.Linear(0f, 0.6f, 1f, 3f) : AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            if (isSmoke)
                g.SetKeys(new[] { new GradientColorKey(new Color(0.6f, 0.6f, 0.65f), 0f), new GradientColorKey(new Color(0.3f, 0.3f, 0.33f), 1f) },
                          new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) });
            else
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f), new GradientColorKey(flameColor, 0.45f), new GradientColorKey(new Color(0.7f, 0.12f, 0.05f), 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftTexture() };
            rend.sharedMaterial = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();
            return ps;
        }
    }
}
