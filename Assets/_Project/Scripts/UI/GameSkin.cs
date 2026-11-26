using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PoliceChase.UI
{
    public enum FrameShape { Rounded, Chamfer, Pill }
    public enum ButtonStyle { Blue, Gold, Green, Dark, Disabled }

    /// <summary>
    /// Skin "neon đua xe" vẽ hoàn toàn bằng code: khung xanh đậm có viền sáng + quầng sáng,
    /// nút gradient (xanh / vàng / xanh lá / tối), tiêu đề chữ nghiêng vàng, sọc trang trí, ô caro, logo đồng hồ tốc độ.
    /// </summary>
    public static class GameSkin
    {
        // ───────────────────────── Bảng màu ─────────────────────────
        public static readonly Color PanelTop = new Color(0.09f, 0.16f, 0.36f, 0.93f);
        public static readonly Color PanelBottom = new Color(0.025f, 0.05f, 0.14f, 0.95f);
        public static readonly Color RowTop = new Color(0.1f, 0.15f, 0.3f, 0.95f);
        public static readonly Color RowBottom = new Color(0.05f, 0.08f, 0.18f, 0.95f);
        public static readonly Color Edge = new Color(0.3f, 0.65f, 1f, 1f);
        public static readonly Color EdgeSoft = new Color(0.3f, 0.6f, 1f, 0.45f);
        public static readonly Color Glow = new Color(0.1f, 0.45f, 1f, 0.55f);
        public static readonly Color Yellow = new Color(1f, 0.8f, 0.16f);
        public static readonly Color TextLight = new Color(0.94f, 0.96f, 1f);
        public static readonly Color TextDim = new Color(0.68f, 0.74f, 0.86f);
        public static readonly Color Cyan = new Color(0.45f, 0.8f, 1f);
        public static readonly Color PipOff = new Color(0.25f, 0.3f, 0.42f, 1f);

        // ───────────────────────── Sprite ─────────────────────────
        const int S = 128, M = 32;
        public const float GlowMargin = M;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static void ShapeParams(FrameShape shape, out float radius, out float chamfer, out int slice)
        {
            switch (shape)
            {
                case FrameShape.Chamfer: radius = 10f; chamfer = 30f; slice = 42; break;
                case FrameShape.Pill: radius = S / 2f - 1f; chamfer = 0f; slice = S / 2; break;
                default: radius = 16f; chamfer = 0f; slice = 22; break;
            }
        }

        /// <summary>Nền đặc (tô gradient bằng UIGradient).</summary>
        public static Sprite Fill(FrameShape shape) => Cached("fill" + shape, () =>
        {
            ShapeParams(shape, out float r, out float ch, out int slice);
            return Make(S, S, slice, (x, y) =>
            {
                float d = UIShapes.PanelSdf(x + 0.5f, y + 0.5f, S / 2f, S / 2f, S / 2f - 1f, S / 2f - 1f, r, ch);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            });
        });

        /// <summary>Viền sáng 2.5px + ánh sáng mờ phía trong.</summary>
        public static Sprite Border(FrameShape shape) => Cached("border" + shape, () =>
        {
            ShapeParams(shape, out float r, out float ch, out int slice);
            return Make(S, S, slice, (x, y) =>
            {
                float d = UIShapes.PanelSdf(x + 0.5f, y + 0.5f, S / 2f, S / 2f, S / 2f - 1f, S / 2f - 1f, r, ch);
                float inside = Mathf.Clamp01(0.5f - d);
                float stroke = inside * Mathf.Clamp01(d + 3f);
                float inner = inside * 0.28f * Mathf.Exp(d / 5f);
                return new Color(1f, 1f, 1f, Mathf.Max(stroke, inner));
            });
        });

        /// <summary>Quầng sáng bên ngoài (đặt lệch ra ngoài GlowMargin).</summary>
        public static Sprite Halo(FrameShape shape) => Cached("glow" + shape, () =>
        {
            ShapeParams(shape, out float r, out float ch, out int slice);
            int T = S + 2 * M;
            return Make(T, T, slice + M, (x, y) =>
            {
                float d = UIShapes.PanelSdf(x + 0.5f, y + 0.5f, T / 2f, T / 2f, S / 2f - 1f, S / 2f - 1f, r, ch);
                float a = d > 0f ? Mathf.Exp(-d / 9f) : Mathf.Clamp01(1f + d / 3f);
                return new Color(1f, 1f, 1f, a);
            });
        });

        /// <summary>4 sọc xiên mờ dần (trang trí cạnh tiêu đề).</summary>
        public static Sprite Stripes => Cached("stripes", () => Make(128, 32, 0, (x, y) =>
        {
            float u = (x + y * 0.75f) / 22f;
            bool on = Mathf.Repeat(u, 1f) < 0.5f && x + y * 0.75f < 100f;
            float fade = 1f - x / 128f;
            return new Color(1f, 1f, 1f, on ? fade : 0f);
        }));

        /// <summary>Ô caro (cờ đua) mờ dần sang phải – trang trí nút CHƠI.</summary>
        public static Sprite Checker => Cached("checker", () => Make(64, 64, 0, (x, y) =>
        {
            bool cell = ((x / 10) + (y / 10)) % 2 == 0;
            float fade = Mathf.Clamp01(1f - x / 64f);
            return new Color(1f, 1f, 1f, cell ? fade * fade : 0f);
        }));

        public static Sprite SpeedLogo => Cached("speedlogo", () => MakeColor(160, UIShapes.SpeedLogo));
        public static Sprite Coin => Cached("coin", () => MakeColor(128, UIShapes.GoldCoin));

        /// <summary>Vòng sáng tròn cho mặt sàn dưới bệ xe (dùng cho SpriteRenderer).</summary>
        public static Sprite FloorGlow => Cached("floorglow", () => Make(256, 256, 0, (x, y) =>
        {
            float d = UIShapes.Dist((x + 0.5f) / 256f, (y + 0.5f) / 256f, 0.5f, 0.5f);
            float ring = Mathf.Exp(-Mathf.Pow((d - 0.4f) / 0.035f, 2f));
            float halo = Mathf.Exp(-Mathf.Pow((d - 0.4f) / 0.1f, 2f)) * 0.35f;
            float inner = d < 0.4f ? 0.12f : 0f;
            float a = Mathf.Clamp01(ring + halo + inner) * Mathf.Clamp01((0.5f - d) * 20f);
            return new Color(1f, 1f, 1f, a);
        }, 256f));

        /// <summary>Đốm sáng mềm hình tròn.</summary>
        public static Sprite SoftDot => Cached("softdot", () => Make(128, 128, 0, (x, y) =>
        {
            float d = UIShapes.Dist((x + 0.5f) / 128f, (y + 0.5f) / 128f, 0.5f, 0.5f) * 2f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Exp(-d * d * 4f) - 0.02f));
        }));

        static Sprite Cached(string key, Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            cache[key] = s;
            return s;
        }

        static Sprite Make(int w, int h, int slice, Func<int, int, Color> px, float ppu = 100f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var cols = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    cols[y * w + x] = px(x, y);
            tex.SetPixels(cols);
            tex.Apply();
            var border = slice > 0 ? new Vector4(slice, slice, slice, slice) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
        }

        delegate void ColorFn(float x, float y, out float r, out float g, out float b, out float a);

        static Sprite MakeColor(int n, ColorFn fn)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var cols = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float R = 0, G = 0, B = 0, A = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            fn((x + 0.25f + sx * 0.5f) / n, (y + 0.25f + sy * 0.5f) / n, out float r, out float g, out float b, out float a);
                            R += r * a; G += g * a; B += b * a; A += a;
                        }
                    cols[y * n + x] = A > 0f ? new Color(R / A, G / A, B / A, A / 4f) : new Color(0, 0, 0, 0);
                }
            tex.SetPixels(cols);
            tex.Apply(true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        // ───────────────────────── Dựng khối ─────────────────────────

        /// <summary>Khung neon: quầng sáng + nền gradient + viền sáng. Đặt vị trí cho frame.rect sau khi tạo.</summary>
        public static NeonFrame Frame(Transform parent, string name, FrameShape shape, Color top, Color bottom, Color edge, Color glow)
        {
            var root = UIKit.NewRect(parent, name);
            var f = root.gameObject.AddComponent<NeonFrame>();
            f.rect = root;
            f.shape = shape;

            f.glow = SlicedImage(root, "Glow", Halo(shape));
            UIKit.Stretch(f.glow.rectTransform, -GlowMargin);
            f.glow.raycastTarget = false;

            f.fill = SlicedImage(root, "Fill", Fill(shape));
            UIKit.Stretch(f.fill.rectTransform);
            f.gradient = f.fill.gameObject.AddComponent<UIGradient>();

            f.border = SlicedImage(root, "Border", Border(shape));
            UIKit.Stretch(f.border.rectTransform);
            f.border.raycastTarget = false;

            f.SetColors(top, bottom, edge, glow);
            return f;
        }

        /// <summary>Khung bảng lớn (vát góc, viền sáng, quầng sáng xanh).</summary>
        public static NeonFrame Panel(Transform parent, string name, bool chamfer = true) =>
            Frame(parent, name, chamfer ? FrameShape.Chamfer : FrameShape.Rounded, PanelTop, PanelBottom, Edge, Glow);

        /// <summary>Khung hàng / thẻ nhỏ bên trong bảng.</summary>
        public static NeonFrame Row(Transform parent, string name) =>
            Frame(parent, name, FrameShape.Rounded, RowTop, RowBottom, EdgeSoft, Color.clear);

        static Image SlicedImage(Transform parent, string name, Sprite sprite)
        {
            var img = UIKit.NewRect(parent, name).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            return img;
        }

        /// <summary>Chữ tiêu đề nghiêng đậm (font Title) có bóng.</summary>
        public static Text Title(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = UIKit.Label(parent, text, size, color, align);
            t.font = UIKit.Title;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0.05f, 0.2f, 0.75f);
            sh.effectDistance = new Vector2(2f, -3f);
            return t;
        }

        /// <summary>Tiêu đề bảng: icon vàng + chữ nghiêng vàng + sọc xanh. Đặt ở góc trên-trái của parent.</summary>
        public static RectTransform Header(Transform parent, string icon, string text, float y = -18f)
        {
            var row = UIKit.NewRect(parent, "Header");
            UIKit.Anchors(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, y - 56f), new Vector2(-26f, y));
            var ic = UIKit.IconImage(row, icon, Yellow, 44f);
            UIKit.Place(ic.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            var t = Title(row, text, 40, Yellow);
            UIKit.Anchors(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(60f, 0f), Vector2.zero);
            var st = new GameObject("Stripes", typeof(RectTransform)).AddComponent<Image>();
            st.transform.SetParent(row, false);
            st.sprite = Stripes;
            st.color = new Color(0.25f, 0.55f, 1f, 0.55f);
            st.raycastTarget = false;
            UIKit.Place(st.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 4f), new Vector2(150f, 38f));
            return row;
        }

        /// <summary>Ô vuông chứa icon (dùng trong hàng nâng cấp).</summary>
        public static NeonFrame IconTile(Transform parent, string icon, float size)
        {
            var f = Frame(parent, "IconTile", FrameShape.Rounded, new Color(0.14f, 0.2f, 0.36f, 1f), new Color(0.05f, 0.08f, 0.17f, 1f),
                          new Color(0.45f, 0.7f, 1f, 0.7f), Color.clear);
            var ic = UIKit.IconImage(f.rect, icon, new Color(0.86f, 0.93f, 1f), size * 0.62f);
            UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.62f, size * 0.62f));
            ic.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return f;
        }

        /// <summary>Nút neon. style đổi được sau bằng SetStyle.</summary>
        public static NeonButton Button(Transform parent, string text, ButtonStyle style, Action onClick,
                                        string icon = null, int fontSize = 34, bool chevron = false, FrameShape shape = FrameShape.Rounded)
        {
            var frame = Frame(parent, "Button_" + text, shape, Color.white, Color.white, Color.white, Color.clear);
            var go = frame.gameObject;
            var nb = go.AddComponent<NeonButton>();
            nb.frame = frame;

            // Caro trang trí (chỉ hiện ở nút vàng)
            nb.checkers = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                var c = new GameObject("Checker", typeof(RectTransform)).AddComponent<Image>();
                c.transform.SetParent(frame.rect, false);
                c.sprite = Checker;
                c.raycastTarget = false;
                var rt = c.rectTransform;
                rt.anchorMin = new Vector2(i, 0f);
                rt.anchorMax = new Vector2(i, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(90f, -10f);
                rt.anchoredPosition = new Vector2(i == 0 ? 6f : -6f, 0f);
                rt.localScale = new Vector3(i == 0 ? 1f : -1f, 1f, 1f);
                nb.checkers[i] = c;
            }

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = frame.fill;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            go.AddComponent<ButtonFX>();
            nb.button = btn;

            var row = UIKit.NewRect(frame.rect, "Content");
            UIKit.Stretch(row);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 14f;
            h.padding = new RectOffset(20, 20, 0, 0);
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            nb.content = row;

            if (!string.IsNullOrEmpty(icon)) nb.icon = UIKit.IconImage(row, icon, Color.white, fontSize * 1.05f);
            nb.label = Title(row, text, fontSize, Color.white, TextAnchor.MiddleCenter);
            nb.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var fit = nb.label.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (string.IsNullOrEmpty(text)) nb.label.gameObject.SetActive(false);

            if (chevron)
            {
                nb.chevron = UIKit.IconImage(frame.rect, "chevron", Color.white, 34f);
                UIKit.Place(nb.chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(30f, 34f));
            }

            nb.SetStyle(style);
            return nb;
        }

        public static void StyleColors(ButtonStyle s, out Color top, out Color bottom, out Color edge, out Color glow, out Color text)
        {
            switch (s)
            {
                case ButtonStyle.Gold:
                    top = new Color(1f, 0.9f, 0.32f); bottom = new Color(1f, 0.62f, 0.04f);
                    edge = new Color(1f, 0.97f, 0.7f); glow = new Color(1f, 0.7f, 0.1f, 0.55f); text = new Color(0.12f, 0.1f, 0.2f); break;
                case ButtonStyle.Green:
                    top = new Color(0.32f, 0.9f, 0.48f); bottom = new Color(0.04f, 0.56f, 0.26f);
                    edge = new Color(0.7f, 1f, 0.78f); glow = new Color(0.1f, 0.9f, 0.4f, 0.45f); text = Color.white; break;
                case ButtonStyle.Dark:
                    top = new Color(0.2f, 0.26f, 0.42f, 0.95f); bottom = new Color(0.1f, 0.13f, 0.25f, 0.95f);
                    edge = new Color(0.45f, 0.58f, 0.85f, 0.55f); glow = Color.clear; text = new Color(0.86f, 0.9f, 1f); break;
                case ButtonStyle.Disabled:
                    top = new Color(0.17f, 0.19f, 0.26f, 0.95f); bottom = new Color(0.1f, 0.11f, 0.16f, 0.95f);
                    edge = new Color(0.35f, 0.4f, 0.5f, 0.35f); glow = Color.clear; text = new Color(0.5f, 0.54f, 0.62f); break;
                default:
                    top = new Color(0.25f, 0.62f, 1f); bottom = new Color(0.04f, 0.3f, 0.88f);
                    edge = new Color(0.65f, 0.85f, 1f); glow = new Color(0.1f, 0.5f, 1f, 0.55f); text = Color.white; break;
            }
        }

        /// <summary>Thanh 5 vạch cấp nâng cấp.</summary>
        public static Image[] Pips(Transform parent, int count, Vector2 pos, Vector2 pipSize, float gap)
        {
            var pips = new Image[count];
            for (int i = 0; i < count; i++)
            {
                pips[i] = UIKit.Box(parent, "Pip" + i, PipOff, false, 0.3f);
                UIKit.Place(pips[i].rectTransform, new Vector2(0f, 0f), pos + new Vector2(i * (pipSize.x + gap), 0f), pipSize);
            }
            return pips;
        }

        /// <summary>Thanh chỉ số vàng có gradient.</summary>
        public static ProgressBar StatBar(Transform parent)
        {
            var bar = UIKit.Bar(parent, new Color(0.18f, 0.22f, 0.34f, 1f), Color.white);
            var g = bar.fillImage.gameObject.AddComponent<UIGradient>();
            g.top = new Color(1f, 0.88f, 0.3f);
            g.bottom = new Color(1f, 0.62f, 0.05f);
            bar.fillImage.sprite = UIKit.Rounded;
            bar.GetComponent<Image>().sprite = UIKit.Rounded;
            return bar;
        }
    }

    /// <summary>Đổ màu gradient dọc (trên → dưới) cho Image/Text, kể cả ảnh 9-slice.</summary>
    [AddComponentMenu("UI/Effects/Police Chase Gradient")]
    public class UIGradient : BaseMeshEffect
    {
        public Color top = Color.white;
        public Color bottom = Color.white;

        public void Set(Color t, Color b)
        {
            top = t;
            bottom = b;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var v = new UIVertex();
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                min = Mathf.Min(min, v.position.y);
                max = Mathf.Max(max, v.position.y);
            }
            float h = Mathf.Max(0.001f, max - min);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Color c = v.color;
                v.color = c * Color.Lerp(bottom, top, (v.position.y - min) / h);
                vh.SetUIVertex(v, i);
            }
        }
    }

    /// <summary>Khung neon gồm 3 lớp: quầng sáng, nền gradient, viền.</summary>
    public class NeonFrame : MonoBehaviour
    {
        public RectTransform rect;
        public FrameShape shape;
        public Image glow, fill, border;
        public UIGradient gradient;

        public void SetColors(Color top, Color bottom, Color edge, Color glowColor)
        {
            gradient.Set(top, bottom);
            border.color = edge;
            glow.color = glowColor;
            glow.enabled = glowColor.a > 0.01f;
        }
    }

    /// <summary>Nút neon: đổi kiểu (xanh / vàng / xanh lá / tối / vô hiệu) và chữ lúc chạy.</summary>
    public class NeonButton : MonoBehaviour
    {
        public NeonFrame frame;
        public UnityEngine.UI.Button button;
        public RectTransform content;
        public Text label;
        public Image icon, chevron;
        public Image[] checkers;
        public ButtonStyle Style { get; private set; }

        public void SetStyle(ButtonStyle s)
        {
            Style = s;
            GameSkin.StyleColors(s, out var top, out var bottom, out var edge, out var glow, out var text);
            frame.SetColors(top, bottom, edge, glow);
            label.color = text;
            if (icon != null) icon.color = text;
            if (chevron != null) chevron.color = new Color(text.r, text.g, text.b, s == ButtonStyle.Dark || s == ButtonStyle.Disabled ? 0.4f : 1f);
            var sh = label.GetComponent<Shadow>();
            if (sh != null) sh.enabled = s != ButtonStyle.Gold && s != ButtonStyle.Disabled;
            foreach (var c in checkers)
            {
                c.gameObject.SetActive(s == ButtonStyle.Gold);
                c.color = new Color(0.75f, 0.42f, 0f, 0.45f);
            }
        }

        public void SetText(string text)
        {
            label.text = text;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void SetIcon(string name)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(!string.IsNullOrEmpty(name));
            if (!string.IsNullOrEmpty(name)) icon.sprite = UIKit.Icon(name);
        }
    }
}
