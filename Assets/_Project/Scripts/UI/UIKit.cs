using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PoliceChase.UI
{
    /// <summary>
    /// Bộ công cụ giao diện dùng chung – phong cách tối giản hiện đại:
    /// bảng trong mờ bo tròn, nút bo tròn có hiệu ứng nhấn, icon vẽ bằng code, font tuỳ chọn.
    /// Font: đặt file .ttf vào Assets/_Project/Resources/Fonts/ với tên UIFont-Bold.ttf và UIFont-Regular.ttf
    /// (khuyên dùng Be Vietnam Pro trên Google Fonts). Không có thì dùng font mặc định.
    /// </summary>
    public static class UIKit
    {
        // ───────────────────────── Màu ─────────────────────────
        public static readonly Color Panel = new Color(0.035f, 0.065f, 0.16f, 0.86f);
        public static readonly Color PanelSolid = new Color(0.045f, 0.08f, 0.19f, 0.97f);
        public static readonly Color Surface = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color SurfaceHover = new Color(1f, 1f, 1f, 0.13f);
        public static readonly Color Accent = new Color(0.22f, 0.56f, 1f);
        public static readonly Color Success = new Color(0.17f, 0.80f, 0.49f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.16f);
        public static readonly Color Danger = new Color(1f, 0.30f, 0.37f);
        public static readonly Color TextColor = new Color(0.96f, 0.97f, 1f);
        public static readonly Color TextMuted = new Color(0.65f, 0.69f, 0.76f);
        public static readonly Color Disabled = new Color(0.25f, 0.28f, 0.34f);
        public static readonly Color Dim = new Color(0.02f, 0.03f, 0.05f, 0.72f);

        // ───────────────────────── Font ─────────────────────────
        static Font bold, regular, title;
        /// <summary>Font tiêu đề nghiêng đậm (Resources/Fonts/UIFont-Title), không có thì dùng Bold.</summary>
        public static Font Title
        {
            get
            {
                if (title == null) title = Resources.Load<Font>("Fonts/UIFont-Title");
                if (title == null) title = Bold;
                return title;
            }
        }
        public static Font Bold
        {
            get
            {
                if (bold == null) bold = Resources.Load<Font>("Fonts/UIFont-Bold");
                if (bold == null) bold = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return bold;
            }
        }
        public static Font Regular
        {
            get
            {
                if (regular == null) regular = Resources.Load<Font>("Fonts/UIFont-Regular");
                if (regular == null) regular = Bold;
                return regular;
            }
        }

        // ───────────────────────── Sprite bo tròn ─────────────────────────
        static Sprite rounded, pill, circle;
        const int RoundTex = 64, RoundRadius = 24, PillRadius = 32;

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(RoundTex, RoundRadius));
        public static Sprite Pill => pill != null ? pill : (pill = MakeRounded(RoundTex, PillRadius));
        public static Sprite Circle => circle != null ? circle : (circle = Icon("circle"));

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            int b = Mathf.Min(radius, size / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // ───────────────────────── Icon vẽ bằng code ─────────────────────────
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        /// <summary>
        /// Icon trắng 128px (tô màu bằng Image.color). Tên: pause, play, star, coin, clock, home, restart, bolt,
        /// left, right, up, down, check, lock, gauge, wheel, shield, circle, arrow, car, map.
        /// </summary>
        public static Sprite Icon(string name)
        {
            if (icons.TryGetValue(name, out var s) && s != null) return s;
            Func<float, float, bool> shape = Shape(name);
            const int n = 128, ss = 3;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                            if (shape((x + (sx + 0.5f) / ss) / n, (y + (sy + 0.5f) / ss) / n)) hit++;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, hit / (float)(ss * ss)));
                }
            tex.Apply(true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            icons[name] = s;
            return s;
        }

        static Func<float, float, bool> Shape(string name)
        {
            switch (name)
            {
                case "pause": return (x, y) => y > 0.2f && y < 0.8f && ((x > 0.27f && x < 0.43f) || (x > 0.57f && x < 0.73f));
                case "play": return (x, y) => InPoly(x, y, 0.3f, 0.18f, 0.3f, 0.82f, 0.82f, 0.5f);
                case "left": return (x, y) => InPoly(x, y, 0.68f, 0.14f, 0.68f, 0.86f, 0.2f, 0.5f);
                case "right": return (x, y) => InPoly(x, y, 0.32f, 0.14f, 0.32f, 0.86f, 0.8f, 0.5f);
                case "up": return (x, y) => InPoly(x, y, 0.14f, 0.3f, 0.86f, 0.3f, 0.5f, 0.82f);
                case "down": return (x, y) => InPoly(x, y, 0.14f, 0.7f, 0.86f, 0.7f, 0.5f, 0.18f);
                case "circle": return (x, y) => Dist(x, y, 0.5f, 0.5f) < 0.48f;
                case "star":
                    {
                        var p = new float[20];
                        for (int i = 0; i < 10; i++)
                        {
                            float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                            float r = i % 2 == 0 ? 0.47f : 0.2f;
                            p[i * 2] = 0.5f + Mathf.Cos(a) * r;
                            p[i * 2 + 1] = 0.47f + Mathf.Sin(a) * r;
                        }
                        return (x, y) => InPoly(x, y, p);
                    }
                case "coin":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.5f);
                        return d < 0.46f && !(d > 0.31f && d < 0.37f);
                    };
                case "clock":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.5f);
                        bool ring = d < 0.46f && d > 0.36f;
                        bool hand1 = x > 0.46f && x < 0.54f && y > 0.48f && y < 0.76f;
                        bool hand2 = y > 0.46f && y < 0.54f && x > 0.48f && x < 0.7f;
                        return ring || hand1 || hand2;
                    };
                case "home":
                    return (x, y) =>
                    {
                        bool roof = InPoly(x, y, 0.08f, 0.52f, 0.5f, 0.9f, 0.92f, 0.52f);
                        bool body = x > 0.2f && x < 0.8f && y > 0.12f && y < 0.56f;
                        bool door = x > 0.42f && x < 0.58f && y > 0.12f && y < 0.38f;
                        return (roof || body) && !door;
                    };
                case "restart":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.5f);
                        float ang = Mathf.Atan2(y - 0.5f, x - 0.5f) * Mathf.Rad2Deg;
                        bool ring = d < 0.4f && d > 0.28f && !(ang > 20f && ang < 75f);
                        bool head = InPoly(x, y, 0.62f, 0.95f, 0.9f, 0.62f, 0.56f, 0.6f);
                        return ring || head;
                    };
                case "bolt": return (x, y) => InPoly(x, y, 0.58f, 0.96f, 0.18f, 0.44f, 0.46f, 0.44f, 0.38f, 0.04f, 0.82f, 0.58f, 0.53f, 0.58f);
                case "check": return (x, y) => InPoly(x, y, 0.12f, 0.5f, 0.27f, 0.65f, 0.42f, 0.5f, 0.76f, 0.84f, 0.9f, 0.7f, 0.42f, 0.22f);
                case "lock":
                    return (x, y) =>
                    {
                        bool body = x > 0.22f && x < 0.78f && y > 0.08f && y < 0.54f;
                        float d = Dist(x, y, 0.5f, 0.58f);
                        bool shackle = y > 0.54f && d < 0.24f && d > 0.14f;
                        return body || shackle;
                    };
                case "gauge":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.4f);
                        bool arc = y > 0.36f && d < 0.46f && d > 0.36f;
                        bool needle = InPoly(x, y, 0.46f, 0.36f, 0.54f, 0.44f, 0.8f, 0.72f);
                        bool hub = d < 0.08f;
                        return arc || needle || hub;
                    };
                case "wheel":
                    return (x, y) =>
                    {
                        float d = Dist(x, y, 0.5f, 0.5f);
                        bool ring = d < 0.46f && d > 0.36f;
                        bool hub = d < 0.12f;
                        bool spokeH = y > 0.45f && y < 0.55f && d < 0.4f;
                        bool spokeV = x > 0.45f && x < 0.55f && y < 0.5f && d < 0.4f;
                        return ring || hub || spokeH || spokeV;
                    };
                case "shield": return (x, y) => InPoly(x, y, 0.5f, 0.95f, 0.88f, 0.8f, 0.82f, 0.36f, 0.5f, 0.04f, 0.18f, 0.36f, 0.12f, 0.8f);
                case "arrow": return (x, y) => InPoly(x, y, 0.5f, 0.95f, 0.85f, 0.1f, 0.5f, 0.3f, 0.15f, 0.1f);
                case "car":
                    return (x, y) =>
                    {
                        bool body = x > 0.08f && x < 0.92f && y > 0.3f && y < 0.55f;
                        bool cabin = InPoly(x, y, 0.25f, 0.55f, 0.35f, 0.78f, 0.68f, 0.78f, 0.8f, 0.55f);
                        bool w1 = Dist(x, y, 0.28f, 0.3f) < 0.11f, w2 = Dist(x, y, 0.72f, 0.3f) < 0.11f;
                        return body || cabin || w1 || w2;
                    };
                case "map":
                    return (x, y) =>
                    {
                        bool frame = x > 0.12f && x < 0.88f && y > 0.15f && y < 0.85f;
                        bool fold1 = x > 0.36f && x < 0.4f, fold2 = x > 0.6f && x < 0.64f;
                        return frame && !fold1 && !fold2;
                    };
                default: return UIShapes.Get(name);
            }
        }

        static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        static bool InPoly(float x, float y, params float[] p)
        {
            bool inside = false;
            int n = p.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi + 1e-6f) + xi) inside = !inside;
            }
            return inside;
        }

        // ───────────────────────── Dựng giao diện ─────────────────────────

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                return;
            }
            // EventSystem cũ dùng StandaloneInputModule (Input Manager cũ) sẽ không nhận chuột/chạm → đổi sang module mới.
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                foreach (var m in es.GetComponents<BaseInputModule>()) UnityEngine.Object.Destroy(m);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        /// <summary>Vùng an toàn của điện thoại (tránh tai thỏ / bo góc). Mọi thứ nên đặt bên trong.</summary>
        public static RectTransform SafeArea(Transform canvas)
        {
            var rt = NewRect(canvas, "SafeArea");
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        public static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Đặt theo neo + vị trí + kích thước (pivot = neo).</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static void Anchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        public static Image Box(Transform parent, string name, Color color, bool pill = false, float cornerScale = 1f)
        {
            var rt = NewRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = pill ? Pill : Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.05f, cornerScale);
            img.color = color;
            return img;
        }

        public static Image IconImage(Transform parent, string icon, Color color, float size)
        {
            var rt = NewRect(parent, "Icon_" + icon);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Icon(icon);
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool boldFont = true)
        {
            var rt = NewRect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = boldFont ? Bold : Regular;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.05f;
            return t;
        }

        /// <summary>Nút bo tròn: nền màu + (icon) + chữ, có hiệu ứng nhấn. Trả về Button; label/icon lấy qua out.</summary>
        public static Button MakeButton(Transform parent, string text, Color color, Action onClick, out Text label,
                                    string icon = null, int fontSize = 26, bool pill = true)
        {
            var bg = Box(parent, "Button", color, pill);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            bg.gameObject.AddComponent<ButtonFX>();

            var row = NewRect(bg.transform, "Content");
            Stretch(row);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 10f;
            h.padding = new RectOffset(18, 18, 0, 0);
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            if (!string.IsNullOrEmpty(icon)) IconImage(row, icon, TextColor, fontSize * 1.1f);
            label = Label(row, text, fontSize, TextColor);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var fitter = label.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (string.IsNullOrEmpty(text)) label.gameObject.SetActive(false);
            return btn;
        }

        /// <summary>Nút tròn chỉ có icon.</summary>
        public static Button IconButton(Transform parent, string icon, Color color, Action onClick, float size, float iconScale = 0.5f)
        {
            var bg = Box(parent, "IconButton", color, true);
            bg.sprite = Circle;
            bg.type = Image.Type.Simple;
            bg.rectTransform.sizeDelta = new Vector2(size, size);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            bg.gameObject.AddComponent<ButtonFX>();
            var ic = IconImage(bg.transform, icon, TextColor, size * iconScale);
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * iconScale, size * iconScale));
            ic.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return btn;
        }

        /// <summary>Thanh tiến trình bo tròn. Gán giá trị bằng Progress.Set(0..1).</summary>
        public static ProgressBar Bar(Transform parent, Color back, Color fill)
        {
            var bg = Box(parent, "Bar", back, true, 0.5f);
            var f = Box(bg.transform, "Fill", fill, true, 0.5f);
            var bar = bg.gameObject.AddComponent<ProgressBar>();
            bar.fill = f.rectTransform;
            bar.fillImage = f;
            bar.Set(0f);
            return bar;
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }
    }

    // ───────────────────────── Thành phần nhỏ ─────────────────────────

    /// <summary>Hiệu ứng nhấn: thu nhỏ khi bấm, phóng nhẹ khi rê chuột. Chạy cả khi game tạm dừng.</summary>
    public class ButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        float target = 1f;
        bool down, over;

        public void OnPointerDown(PointerEventData e) { down = true; Refresh(); }
        public void OnPointerUp(PointerEventData e) { down = false; Refresh(); }
        public void OnPointerEnter(PointerEventData e) { over = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; Refresh(); }
        void OnDisable() { down = over = false; transform.localScale = Vector3.one; }

        void Refresh() => target = down ? 0.93f : (over ? 1.04f : 1f);

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Thanh tiến trình bo tròn, chạy mượt tới giá trị mới.</summary>
    public class ProgressBar : MonoBehaviour
    {
        public RectTransform fill;
        public Image fillImage;
        public bool smooth = true;
        float value, shown;

        public void Set(float v, bool instant = false)
        {
            value = Mathf.Clamp01(v);
            if (instant || !smooth) { shown = value; Apply(); }
        }

        void Update()
        {
            if (Mathf.Abs(shown - value) < 0.0005f) return;
            shown = Mathf.Lerp(shown, value, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            Apply();
        }

        void Apply()
        {
            if (fill == null) return;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Max(0.0001f, shown), 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            if (fillImage != null) fillImage.enabled = shown > 0.01f;
        }
    }

    /// <summary>Xuất hiện mềm: mờ → rõ + phóng nhẹ. Gọi Show()/Hide(). Chạy cả khi tạm dừng.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIFade : MonoBehaviour
    {
        public float duration = 0.2f;
        public bool scale = true;
        CanvasGroup group;
        float t, from, to = 1f;
        bool hiding;

        void Awake() => group = GetComponent<CanvasGroup>();

        public void Show()
        {
            gameObject.SetActive(true);
            if (group == null) group = GetComponent<CanvasGroup>();
            hiding = false;
            from = 0f; to = 1f; t = 0f;
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = true;
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            hiding = true;
            from = group.alpha; to = 0f; t = 0f;
            group.interactable = group.blocksRaycasts = false;
        }

        void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            float e = 1f - (1f - t) * (1f - t);
            group.alpha = Mathf.Lerp(from, to, e);
            if (scale) transform.localScale = Vector3.one * Mathf.Lerp(hiding ? 1f : 0.94f, hiding ? 0.97f : 1f, e);
            if (t >= 1f && hiding) gameObject.SetActive(false);
        }
    }

    /// <summary>Co khung theo vùng an toàn của điện thoại (Screen.safeArea).</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var area = Screen.safeArea;
            if (area == last || Screen.width <= 0 || Screen.height <= 0) return;
            last = area;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rt.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Nút giữ (cho điều khiển cảm ứng): báo true khi đang giữ, false khi nhả hoặc trượt ngón ra ngoài.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> onChanged;
        public Image visual;
        public Color normal = new Color(1f, 1f, 1f, 0.16f);
        public Color pressed = new Color(1f, 1f, 1f, 0.38f);
        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData e) => SetHeld(true);
        public void OnPointerUp(PointerEventData e) => SetHeld(false);
        public void OnPointerExit(PointerEventData e) => SetHeld(false);
        void OnDisable() => SetHeld(false);

        void SetHeld(bool h)
        {
            if (IsHeld == h) return;
            IsHeld = h;
            if (visual != null) visual.color = h ? pressed : normal;
            transform.localScale = h ? Vector3.one * 0.94f : Vector3.one;
            onChanged?.Invoke(h);
        }
    }
}
