using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PoliceChase.Police;

namespace PoliceChase.UI
{
    /// <summary>
    /// Bản đồ nhỏ ở góc màn hình:
    /// - 1 camera nhìn thẳng từ trên xuống, bám theo xe người chơi, vẽ ra RenderTexture,
    /// - mũi tên xanh = xe bạn, chấm đỏ/xanh nhấp nháy = cảnh sát (ngoài phạm vi thì hiện ở mép bản đồ),
    /// - phím M: bật/tắt bản đồ lớn giữa màn hình.
    /// Gắn vào 1 GameObject rỗng bất kỳ trong scene.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        public enum Corner { BottomLeft, BottomRight, TopLeft, TopRight }

        [Header("Mục tiêu (để trống = tự tìm object có tag Player)")]
        public Transform target;

        [Header("Khung bản đồ nhỏ")]
        public Corner corner = Corner.BottomLeft;
        public float size = 230f;
        public float margin = 16f;
        [Tooltip("Bán kính vùng hiển thị của bản đồ nhỏ (m)")]
        public float viewRadius = 120f;
        [Tooltip("Bán kính vùng hiển thị khi mở bản đồ lớn (m)")]
        public float bigMapRadius = 600f;
        [Tooltip("Bản đồ quay theo hướng xe (đầu xe luôn hướng lên trên)")]
        public bool rotateWithPlayer = true;
        public Key toggleKey = Key.M;

        [Header("Camera bản đồ")]
        public float cameraHeight = 300f;
        public int textureSize = 512;
        public Color backgroundColor = new Color(0.85f, 0.75f, 0.55f);
        public LayerMask cullingMask = ~0;

        [Header("Điểm đánh dấu")]
        public Color playerColor = new Color(0.2f, 1f, 0.4f);
        public float markerSize = 12f;

        // Dữ liệu lúc chạy – KHÔNG lưu vào scene
        [System.NonSerialized] Camera cam;
        [System.NonSerialized] RenderTexture rt;
        [System.NonSerialized] Texture2D dotTex, arrowTex;
        [System.NonSerialized] bool big;
        [System.NonSerialized] float findTimer;
        [System.NonSerialized] PoliceDispatcher dispatcher;
        readonly List<PoliceAI> police = new List<PoliceAI>();
        [System.NonSerialized] GUIStyle labelStyle;

        void Start()
        {
            if (target == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) target = p.transform;
            }

            rt = new RenderTexture(textureSize, textureSize, 16) { name = "MinimapRT", antiAliasing = 2 };
            var camGo = new GameObject("MinimapCamera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = viewRadius;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            cam.cullingMask = cullingMask;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = cameraHeight + 600f;
            cam.depth = -10f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.targetTexture = rt;

            dotTex = MakeDot(32);
            arrowTex = MakeArrow(32);
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
        }

        void LateUpdate()
        {
            var kb = Keyboard.current;
            if (kb != null && toggleKey != Key.None && kb[toggleKey].wasPressedThisFrame) big = !big;

            if (target == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) target = p.transform; else return;
            }
            if (cam == null) return;

            float yaw = rotateWithPlayer ? target.eulerAngles.y : 0f;
            cam.transform.SetPositionAndRotation(target.position + Vector3.up * cameraHeight, Quaternion.Euler(90f, yaw, 0f));
            cam.orthographicSize = big ? bigMapRadius : viewRadius;

            findTimer -= Time.deltaTime;
            if (findTimer <= 0f)
            {
                findTimer = 0.5f;
                police.Clear();
                if (dispatcher == null) dispatcher = FindFirstObjectByType<PoliceDispatcher>();
                if (dispatcher != null) foreach (var p in dispatcher.Police) police.Add(p);
                else police.AddRange(FindObjectsByType<PoliceAI>(FindObjectsSortMode.None));
            }
        }

        // ───────────── Thông tin cho giao diện (GameHUD) ─────────────

        [Tooltip("Tự vẽ bản đồ kiểu cũ (OnGUI). GameHUD tắt ô này và vẽ bản đồ bo tròn mới")]
        public bool drawGui = true;
        public Camera MapCamera => cam;
        public RenderTexture MapTexture => rt;
        public Transform Target => target;
        public IReadOnlyList<PoliceAI> TrackedPolice => police;
        public bool Big { get => big; set => big = value; }

        void OnGUI()
        {
            if (!drawGui || cam == null || rt == null || target == null) return;
            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            Rect r = big ? BigRect() : CornerRect();

            // Khung + hình bản đồ
            DrawRect(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0f, 0f, 0f, 0.75f));
            GUI.DrawTexture(r, rt, ScaleMode.StretchToFill, false);

            // Cảnh sát
            bool blink = Mathf.Repeat(Time.time * 4f, 1f) < 0.5f;
            foreach (var p in police)
            {
                if (p == null) continue;
                Vector3 vp = cam.WorldToViewportPoint(p.transform.position);
                bool inside = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                var pos = new Vector2(r.x + Mathf.Clamp01(vp.x) * r.width, r.y + (1f - Mathf.Clamp01(vp.y)) * r.height);
                Color c = p.state == PoliceAI.State.Leave ? new Color(0.6f, 0.6f, 0.6f)
                        : (blink ? new Color(1f, 0.15f, 0.15f) : new Color(0.2f, 0.45f, 1f));
                float s = inside ? markerSize : markerSize * 0.75f;
                DrawTex(dotTex, new Rect(pos.x - s * 0.5f, pos.y - s * 0.5f, s, s), c);
            }

            // Xe người chơi (mũi tên)
            Vector3 pv = cam.WorldToViewportPoint(target.position);
            var ppos = new Vector2(r.x + pv.x * r.width, r.y + (1f - pv.y) * r.height);
            float ms = markerSize * 1.4f;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotateWithPlayer ? 0f : target.eulerAngles.y, ppos);
            DrawTex(arrowTex, new Rect(ppos.x - ms * 0.5f, ppos.y - ms * 0.5f, ms, ms), playerColor);
            GUI.matrix = old;

            // Chữ hướng dẫn
            if (!rotateWithPlayer) Shadowed(new Rect(r.x, r.y + 2f, r.width, 16f), "N");
            Shadowed(new Rect(r.x, r.yMax + 4f, r.width, 16f), big ? "M: thu nhỏ bản đồ" : "M: bản đồ lớn");
        }

        Rect CornerRect()
        {
            float x = (corner == Corner.BottomLeft || corner == Corner.TopLeft) ? margin : Screen.width - margin - size;
            float y = (corner == Corner.TopLeft || corner == Corner.TopRight) ? margin + 40f : Screen.height - margin - size - 20f;
            return new Rect(x, y, size, size);
        }

        Rect BigRect()
        {
            float s = Mathf.Min(Screen.width, Screen.height) * 0.8f;
            return new Rect((Screen.width - s) * 0.5f, (Screen.height - s) * 0.5f - 10f, s, s);
        }

        void Shadowed(Rect r, string text)
        {
            labelStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, labelStyle);
            labelStyle.normal.textColor = Color.white;
            GUI.Label(r, text, labelStyle);
        }

        static void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void DrawTex(Texture2D tex, Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, tex);
            GUI.color = old;
        }

        // Chấm tròn trắng viền đen (tô màu bằng GUI.color)
        static Texture2D MakeDot(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (n - 1) * 0.5f, rOuter = n * 0.5f - 0.5f, rInner = rOuter - 3f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    Color col = d <= rInner ? Color.white : (d <= rOuter ? Color.black : Color.clear);
                    t.SetPixel(x, y, col);
                }
            t.Apply();
            return t;
        }

        // Mũi tên tam giác chỉ lên trên, viền đen
        static Texture2D MakeArrow(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float cx = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float top = n - 3f, bottom = 3f;
                    float half = Mathf.Lerp(n * 0.42f, 0f, Mathf.InverseLerp(bottom, top, y));
                    float dx = Mathf.Abs(x - cx);
                    bool outer = y >= bottom && y <= top && dx <= half;
                    bool inner = y >= bottom + 2.5f && y <= top - 3.5f && dx <= half - 2.5f;
                    t.SetPixel(x, y, inner ? Color.white : (outer ? Color.black : Color.clear));
                }
            t.Apply();
            return t;
        }
    }
}
