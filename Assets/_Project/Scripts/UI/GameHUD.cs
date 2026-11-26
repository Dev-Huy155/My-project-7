using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PoliceChase.Game;
using PoliceChase.Police;
using PoliceChase.Vehicles;

namespace PoliceChase.UI
{
    /// <summary>
    /// Giao diện trong trận (tối giản hiện đại), tạo tự động bởi GameplayBootstrap:
    /// đồng hồ + xu, sao truy nã + trạng thái, bản đồ nhỏ bo tròn (chạm để phóng to), thanh nitro,
    /// thông báo, thanh "đang bị bắt", nút tạm dừng, bảng tạm dừng, bảng kết quả có sao,
    /// và nút cảm ứng trên điện thoại.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        GameplayBootstrap game;
        Minimap minimap;
        bool touch;

        // Trên cùng
        Text timerText, coinText, runText, statusText, toastText, bigText;
        Image[] stars = new Image[3];
        RectTransform toastBox, wantedBox;
        CanvasGroup toastGroup;
        ProgressBar escapeBar, arrestBar, nitroBar;
        GameObject arrestRoot, nitroRoot, pauseButton, touchRoot;
        string lastBigMessage;
        float bigStart;

        // Bản đồ nhỏ
        RectTransform mapFrame, mapArea, playerMarker;
        RawImage mapImage;
        readonly List<Image> policeMarkers = new List<Image>();
        readonly List<Image> boostMarkers = new List<Image>();

        // Nút tăng tốc tên lửa
        Image boostRing, boostGlow, boostIcon;
        Text boostLabel;
        bool boostHinted;
        Vector2 mapSmallPos, mapSmallSize;
        Vector2 mapSmallAnchor;

        // Bảng
        UIFade pausePanel, resultPanel;
        Text pauseInfo, resultTitle, resultNote;
        readonly List<Text> resultValues = new List<Text>();
        readonly List<Text> resultLabels = new List<Text>();
        readonly Image[] resultStars = new Image[3];
        bool resultsShown, pauseShown;
        float resultsStart;

        // Nút cảm ứng
        bool leftHeld, rightHeld, gasHeld, brakeHeld;

        public void Init(GameplayBootstrap g, Minimap map, bool touchControls)
        {
            game = g;
            minimap = map;
            touch = touchControls;
            Build();
        }

        void OnDestroy() => MobileInput.Reset();

        // ───────────────────────── Dựng giao diện ─────────────────────────

        void Build()
        {
            var canvas = UIKit.CreateCanvas("HUD", 10);
            var safe = UIKit.SafeArea(canvas.transform);

            // ── Nút tạm dừng (trên trái) ──
            var pb = UIKit.IconButton(safe, "pause", UIKit.Panel, () => game.SetPaused(true), 84f, 0.42f);
            UIKit.Place((RectTransform)pb.transform, new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(84f, 84f));
            pauseButton = pb.gameObject;

            // ── Đồng hồ + xu (giữa trên) ──
            var top = GameSkin.Frame(safe, "TopInfo", FrameShape.Pill, GameSkin.PanelTop, GameSkin.PanelBottom, GameSkin.Edge, GameSkin.Glow);
            UIKit.Place(top.rect, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(520f, 76f));
            var clock = UIKit.IconImage(top.transform, "clock", UIKit.TextColor, 34f);
            UIKit.Place(clock.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(34f, 34f));
            timerText = UIKit.Label(top.transform, "00:00", 44, UIKit.TextColor, TextAnchor.MiddleLeft);
            timerText.font = UIKit.Title;
            UIKit.Place(timerText.rectTransform, new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(170f, 60f));
            var div = UIKit.Box(top.transform, "Divider", new Color(1f, 1f, 1f, 0.12f), true);
            UIKit.Place(div.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-14f, 0f), new Vector2(3f, 40f));
            var coin = UIKit.IconImage(top.transform, "coin", Color.white, 38f);
            coin.sprite = GameSkin.Coin;
            UIKit.Place(coin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(10f, 0f), new Vector2(34f, 34f));
            coinText = UIKit.Label(top.transform, "0", 42, UIKit.Gold, TextAnchor.MiddleLeft);
            coinText.font = UIKit.Title;
            UIKit.Place(coinText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(54f, 0f), new Vector2(200f, 60f));
            runText = UIKit.Label(safe, "", 20, UIKit.TextMuted, TextAnchor.MiddleCenter, false);
            UIKit.Place(runText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(600f, 28f));

            // ── Truy nã (trên phải) ──
            var wanted = GameSkin.Frame(safe, "Wanted", FrameShape.Pill, GameSkin.PanelTop, GameSkin.PanelBottom, GameSkin.EdgeSoft, Color.clear);
            wantedBox = wanted.rect;
            UIKit.Place(wantedBox, new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(380f, 76f));
            for (int i = 0; i < 3; i++)
            {
                stars[i] = UIKit.IconImage(wanted.transform, "star", UIKit.Gold, 44f);
                UIKit.Place(stars[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(26f + i * 52f, 0f), new Vector2(44f, 44f));
            }
            escapeBar = UIKit.Bar(wanted.transform, new Color(1f, 1f, 1f, 0.12f), UIKit.Success);
            UIKit.Place((RectTransform)escapeBar.transform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(150f, 10f));
            statusText = UIKit.Label(safe, "", 20, UIKit.TextMuted, TextAnchor.MiddleRight, false);
            UIKit.Place(statusText.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -106f), new Vector2(520f, 28f));
            if (GameSession.Mode != GameMode.Chase) { wanted.gameObject.SetActive(false); statusText.gameObject.SetActive(false); }

            // ── Thông báo nhỏ ──
            var toast = GameSkin.Frame(safe, "Toast", FrameShape.Pill, GameSkin.PanelTop, GameSkin.PanelBottom, GameSkin.Yellow, Color.clear);
            toastBox = toast.rect;
            UIKit.Place(toastBox, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(420f, 56f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastText = UIKit.Label(toast.transform, "", 26, UIKit.Gold);
            UIKit.Stretch(toastText.rectTransform);

            // ── Thông báo lớn (BỊ TRUY NÃ!, ĐÃ CẮT ĐUÔI!...) ──
            bigText = GameSkin.Title(safe, "", 96, UIKit.TextColor, TextAnchor.MiddleCenter);
            UIKit.Place(bigText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1400f, 110f));
            bigText.GetComponent<Shadow>().effectDistance = new Vector2(3f, -4f);

            // ── Thanh bị bắt ──
            var arrest = UIKit.NewRect(safe, "Arrest");
            UIKit.Place(arrest, new Vector2(0.5f, 0f), new Vector2(0f, touch ? 260f : 70f), new Vector2(520f, 70f));
            arrestRoot = arrest.gameObject;
            var al = UIKit.Label(arrest, "ĐANG BỊ BẮT – CHẠY ĐI!", 26, UIKit.Danger);
            UIKit.Place(al.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(520f, 36f));
            arrestBar = UIKit.Bar(arrest, new Color(0f, 0f, 0f, 0.55f), UIKit.Danger);
            UIKit.Place((RectTransform)arrestBar.transform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(520f, 18f));
            arrestBar.smooth = false;

            // ── Nút TĂNG TỐC (tên lửa) ──
            BuildBoostButton(safe);

            BuildMinimap(safe);
            if (touch) BuildTouch(safe);
            BuildPause(canvas.transform);
            BuildResults(canvas.transform);
        }

        void BuildMinimap(RectTransform safe)
        {
            if (minimap == null) return;
            var frame = UIKit.Box(safe, "Minimap", UIKit.PanelSolid, false, 1.4f);
            mapFrame = frame.rectTransform;
            mapSmallSize = touch ? new Vector2(250f, 250f) : new Vector2(300f, 300f);
            mapSmallAnchor = touch ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            mapSmallPos = touch ? new Vector2(28f, -128f) : new Vector2(28f, 28f);
            UIKit.Place(mapFrame, mapSmallAnchor, mapSmallPos, mapSmallSize);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => minimap.Big = !minimap.Big);

            var maskImg = UIKit.Box(frame.transform, "Mask", Color.white, false, 1.25f);
            UIKit.Stretch(maskImg.rectTransform, 6f);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            maskImg.raycastTarget = false;
            mapArea = maskImg.rectTransform;

            var raw = UIKit.NewRect(mapArea, "Map").gameObject.AddComponent<RawImage>();
            UIKit.Stretch(raw.rectTransform);
            raw.raycastTarget = false;
            mapImage = raw;

            playerMarker = UIKit.IconImage(mapArea, "arrow", new Color(0.3f, 1f, 0.55f), 30f).rectTransform;
            playerMarker.anchorMin = playerMarker.anchorMax = playerMarker.pivot = new Vector2(0.5f, 0.5f);
        }

        void BuildTouch(RectTransform safe)
        {
            var root = UIKit.NewRect(safe, "TouchControls");
            UIKit.Stretch(root);
            touchRoot = root.gameObject;

            Hold(root, "left", new Vector2(0f, 0f), new Vector2(40f, 40f), 190f, h => { leftHeld = h; SyncTouch(); });
            Hold(root, "right", new Vector2(0f, 0f), new Vector2(250f, 40f), 190f, h => { rightHeld = h; SyncTouch(); });
            Hold(root, "up", new Vector2(1f, 0f), new Vector2(-40f, 40f), 210f, h => { gasHeld = h; SyncTouch(); });
            Hold(root, "down", new Vector2(1f, 0f), new Vector2(-270f, 40f), 160f, h => { brakeHeld = h; SyncTouch(); });
            var drift = Hold(root, null, new Vector2(1f, 0f), new Vector2(-270f, 220f), 130f, h => MobileInput.Handbrake = h);
            var dl = UIKit.Label(drift.transform, "DRIFT", 26, UIKit.TextColor);
            UIKit.Stretch(dl.rectTransform);
        }

        void BuildBoostButton(RectTransform safe)
        {
            float bs = touch ? 170f : 190f;
            var root = UIKit.NewRect(safe, "BoostButton");
            UIKit.Place(root, new Vector2(1f, 0f), touch ? new Vector2(-40f, 280f) : new Vector2(-40f, 46f), new Vector2(bs, bs));
            nitroRoot = root.gameObject;

            boostGlow = UIKit.NewRect(root, "Glow").gameObject.AddComponent<Image>();
            boostGlow.sprite = GameSkin.SoftDot;
            boostGlow.raycastTarget = false;
            UIKit.Stretch(boostGlow.rectTransform, -bs * 0.35f);

            var back = UIKit.NewRect(root, "Back").gameObject.AddComponent<Image>();
            back.sprite = UIKit.Circle;
            back.color = new Color(0.02f, 0.05f, 0.14f, 0.85f);
            UIKit.Stretch(back.rectTransform);

            boostRing = UIKit.NewRect(root, "Ring").gameObject.AddComponent<Image>();
            boostRing.sprite = UIKit.Circle;
            boostRing.type = Image.Type.Filled;
            boostRing.fillMethod = Image.FillMethod.Radial360;
            boostRing.fillOrigin = 2;           // bắt đầu từ trên
            boostRing.fillClockwise = true;
            boostRing.raycastTarget = false;
            UIKit.Stretch(boostRing.rectTransform, 5f);

            var inner = UIKit.NewRect(root, "Inner").gameObject.AddComponent<Image>();
            inner.sprite = UIKit.Circle;
            Color innerCol = new Color(0.06f, 0.13f, 0.32f, 1f);
            inner.color = innerCol;
            UIKit.Stretch(inner.rectTransform, 17f);

            boostIcon = UIKit.IconImage(root, "rocket", GameSkin.Yellow, bs * 0.36f);
            UIKit.Place(boostIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(bs * 0.36f, bs * 0.36f));
            boostIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            boostLabel = GameSkin.Title(root, "TĂNG TỐC", touch ? 24 : 26, Color.white, TextAnchor.MiddleCenter);
            boostLabel.raycastTarget = false;
            UIKit.Place(boostLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, bs * 0.17f), new Vector2(bs, 32f));
            boostLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            if (!touch)
            {
                var hint = UIKit.Label(root, "giữ SHIFT", 20, UIKit.TextMuted, TextAnchor.MiddleCenter, false);
                hint.raycastTarget = false;
                UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(200f, 26f));
            }

            var hb = root.gameObject.AddComponent<HoldButton>();
            hb.visual = inner;
            hb.normal = innerCol;
            hb.pressed = new Color(0.2f, 0.4f, 0.85f, 1f);
            hb.onChanged = h => MobileInput.Nitro = h;
        }

        HoldButton Hold(RectTransform parent, string icon, Vector2 anchor, Vector2 pos, float size, System.Action<bool> changed)
        {
            var bg = UIKit.Box(parent, "Touch_" + (icon ?? "drift"), new Color(1f, 1f, 1f, 0.16f), true);
            bg.sprite = UIKit.Circle;
            bg.type = Image.Type.Simple;
            UIKit.Place(bg.rectTransform, anchor, pos, new Vector2(size, size));
            if (icon != null)
            {
                var ic = UIKit.IconImage(bg.transform, icon, new Color(1f, 1f, 1f, 0.9f), size * 0.42f);
                UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.42f, size * 0.42f));
            }
            var hb = bg.gameObject.AddComponent<HoldButton>();
            hb.visual = bg;
            hb.onChanged = changed;
            return hb;
        }

        void SyncTouch()
        {
            MobileInput.Steer = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
            MobileInput.Throttle = (gasHeld ? 1f : 0f) - (brakeHeld ? 1f : 0f);
        }

        void BuildPause(Transform canvas)
        {
            var root = UIKit.NewRect(canvas, "PausePanel");
            UIKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UIKit.Dim;
            pausePanel = root.gameObject.AddComponent<UIFade>();

            var card = GameSkin.Panel(root, "Card");
            UIKit.Place(card.rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 600f));
            var title = GameSkin.Title(card.transform, "TẠM DỪNG", 66, GameSkin.Yellow, TextAnchor.MiddleCenter);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(560f, 80f));
            pauseInfo = UIKit.Label(card.transform, "", 24, UIKit.TextMuted, TextAnchor.MiddleCenter, false);
            UIKit.Place(pauseInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(560f, 36f));

            PanelButton(card.transform, "TIẾP TỤC", "play", ButtonStyle.Gold, () => game.SetPaused(false), -200f);
            PanelButton(card.transform, "CHƠI LẠI", "restart", ButtonStyle.Blue, () => game.Restart(), -300f);
            PanelButton(card.transform, "VỀ MENU", "home", ButtonStyle.Dark, () => game.GoToMenu(), -400f);
            root.gameObject.SetActive(false);
        }

        void PanelButton(Transform card, string text, string icon, ButtonStyle style, System.Action click, float y)
        {
            var b = GameSkin.Button(card, text, style, click, icon, 36);
            UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(440f, 84f));
            ((RectTransform)b.transform).pivot = new Vector2(0.5f, 1f);
        }

        void BuildResults(Transform canvas)
        {
            var root = UIKit.NewRect(canvas, "ResultPanel");
            UIKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UIKit.Dim;
            resultPanel = root.gameObject.AddComponent<UIFade>();
            resultPanel.duration = 0.3f;

            var card = GameSkin.Panel(root, "Card");
            UIKit.Place(card.rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 760f));

            resultTitle = GameSkin.Title(card.transform, "", 66, UIKit.TextColor, TextAnchor.MiddleCenter);
            UIKit.Place(resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(740f, 76f));

            for (int i = 0; i < 3; i++)
            {
                float size = i == 1 ? 120f : 96f;
                resultStars[i] = UIKit.IconImage(card.transform, "star", UIKit.Gold, size);
                UIKit.Place(resultStars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 130f, i == 1 ? -190f : -200f), new Vector2(size, size));
                resultStars[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            for (int i = 0; i < 6; i++)
            {
                float y = -290f - i * 50f;
                var row = UIKit.Box(card.transform, "Row" + i, i % 2 == 0 ? new Color(1f, 1f, 1f, 0.04f) : new Color(0f, 0f, 0f, 0f), false, 0.6f);
                UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(700f, 46f));
                var l = UIKit.Label(row.transform, "", 26, UIKit.TextMuted, TextAnchor.MiddleLeft, false);
                UIKit.Anchors(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
                var v = UIKit.Label(row.transform, "", 28, UIKit.TextColor, TextAnchor.MiddleRight);
                UIKit.Anchors(v.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
                resultLabels.Add(l);
                resultValues.Add(v);
            }

            resultNote = UIKit.Label(card.transform, "", 22, UIKit.TextMuted, TextAnchor.MiddleCenter, false);
            UIKit.Place(resultNote.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(700f, 32f));

            var again = GameSkin.Button(card.transform, "CHƠI LẠI", ButtonStyle.Gold, () => game.Restart(), "restart", 36);
            UIKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-175f, 32f), new Vector2(330f, 84f));
            ((RectTransform)again.transform).pivot = new Vector2(0.5f, 0f);
            var home = GameSkin.Button(card.transform, "VỀ MENU", ButtonStyle.Blue, () => game.GoToMenu(), "home", 36);
            UIKit.Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(175f, 32f), new Vector2(330f, 84f));
            ((RectTransform)home.transform).pivot = new Vector2(0.5f, 0f);

            root.gameObject.SetActive(false);
        }

        // ───────────────────────── Cập nhật mỗi frame ─────────────────────────

        void Update()
        {
            if (game == null) return;
            float now = Time.unscaledTime;

            // Đồng hồ + xu
            if (game.RoundSeconds > 0f)
            {
                float left = game.TimeLeft;
                timerText.text = UIKit.Clock(left);
                bool hurry = left <= 30f && !game.IsEnded;
                timerText.color = hurry && Mathf.Repeat(now * 2f, 1f) < 0.5f ? UIKit.Danger : UIKit.TextColor;
            }
            else timerText.text = UIKit.Clock(game.Elapsed);
            coinText.text = SaveSystem.Coins.ToString("N0");
            runText.text = (GameSession.Mode == GameMode.Chase ? "Trốn cảnh sát" : "Lái tự do") + $"  ·  ván này +{GameSession.RunCoins}";

            // Truy nã
            var d = game.Dispatcher;
            if (d != null && GameSession.Mode == GameMode.Chase)
            {
                for (int i = 0; i < 3; i++)
                    stars[i].color = i < d.WantedLevel ? UIKit.Gold : new Color(1f, 1f, 1f, 0.15f);
                statusText.text = d.StatusText;
                statusText.color = d.PlayerSeen ? UIKit.Danger : UIKit.TextMuted;
                escapeBar.Set(d.EscapeProgress);
                arrestRoot.SetActive(d.ArrestProgress > 0.01f && !game.IsEnded);
                arrestBar.Set(d.ArrestProgress, true);

                string msg = d.CurrentMessage;
                if (game.IsEnded && game.Reason == GameplayBootstrap.EndReason.Busted && !game.ShowResults) msg = "BỊ BẮT!";
                if (msg != lastBigMessage) { lastBigMessage = msg; bigStart = now; }
            }
            else arrestRoot.SetActive(false);

            bigText.text = lastBigMessage ?? "";
            if (!string.IsNullOrEmpty(lastBigMessage))
            {
                float t = now - bigStart;
                float s = 1f + 0.35f * Mathf.Exp(-9f * t);
                bigText.rectTransform.localScale = new Vector3(s, s, 1f);
                bigText.color = lastBigMessage.Contains("CẮT ĐUÔI") ? UIKit.Success
                              : (lastBigMessage.Contains("BẮT") || lastBigMessage.Contains("TRUY NÃ") ? UIKit.Danger : UIKit.TextColor);
            }

            // Thông báo nhỏ
            float ta = now - game.ToastTime;
            toastGroup.alpha = ta < 0.15f ? ta / 0.15f : (ta < 1.8f ? 1f : Mathf.Clamp01(1f - (ta - 1.8f) / 0.3f));
            if (toastGroup.alpha > 0f) toastText.text = game.ToastText;

            // Nút tăng tốc tên lửa
            var n = game.Nitro;
            bool hasNitro = n != null && n.capacitySeconds > 0f && !game.IsEnded && !game.IsPaused;
            nitroRoot.SetActive(hasNitro);
            if (hasNitro)
            {
                float c = n.Charge01;
                boostRing.fillAmount = Mathf.Lerp(boostRing.fillAmount, c, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                boostRing.color = n.Active ? new Color(1f, 0.55f, 0.12f) : (c >= 0.999f ? new Color(0.3f, 0.85f, 1f) : UIKit.Accent);
                boostIcon.color = c < 0.03f ? new Color(1f, 1f, 1f, 0.3f) : (n.Active ? Color.white : GameSkin.Yellow);
                boostLabel.text = c < 0.03f ? "HẾT NĂNG LƯỢNG" : "TĂNG TỐC";
                boostLabel.fontSize = c < 0.03f ? 18 : (touch ? 24 : 26);
                bool chased = d != null && GameSession.Mode == GameMode.Chase && d.PlayerSeen && d.WantedLevel > 0;
                float pulse = chased && !n.Active && c > 0.25f ? 0.5f + 0.5f * Mathf.Sin(now * 8f) : 0f;
                boostGlow.color = n.Active ? new Color(1f, 0.55f, 0.1f, 0.85f) : new Color(0.3f, 0.7f, 1f, 0.7f * pulse);
                if (chased && !boostHinted && c > 0.25f)
                {
                    boostHinted = true;
                    game.Toast(touch ? "Bị đuổi! Giữ nút TĂNG TỐC để cắt đuôi" : "Bị đuổi! Giữ SHIFT để phóng tên lửa");
                }
            }

            UpdateMinimap();

            // Các bảng
            bool playing = !game.IsPaused && !game.IsEnded;
            pauseButton.SetActive(playing);
            if (touchRoot != null) touchRoot.SetActive(playing);

            if (game.IsPaused != pauseShown)
            {
                pauseShown = game.IsPaused;
                if (pauseShown) pausePanel.Show(); else pausePanel.Hide();
            }
            if (game.IsPaused)
                pauseInfo.text = (game.RoundSeconds > 0f ? "Còn " + UIKit.Clock(game.TimeLeft) : "Đã chơi " + UIKit.Clock(game.Elapsed))
                                 + $"  ·  ván này +{GameSession.RunCoins} xu";

            if (game.ShowResults && !resultsShown) ShowResults();
            if (resultsShown) AnimateResults(now - resultsStart);
        }

        void UpdateMinimap()
        {
            if (minimap == null || mapFrame == null) return;
            if (mapImage.texture != minimap.MapTexture) mapImage.texture = minimap.MapTexture;

            // Phóng to / thu nhỏ
            bool big = minimap.Big && !game.IsEnded;
            Vector2 anchor = big ? new Vector2(0.5f, 0.5f) : mapSmallAnchor;
            float bigSize = Mathf.Min(((RectTransform)mapFrame.parent).rect.height * 0.8f, 860f);
            Vector2 size = big ? new Vector2(bigSize, bigSize) : mapSmallSize;
            Vector2 pos = big ? Vector2.zero : mapSmallPos;
            mapFrame.anchorMin = mapFrame.anchorMax = mapFrame.pivot = anchor;
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            mapFrame.sizeDelta = Vector2.Lerp(mapFrame.sizeDelta, size, k);
            mapFrame.anchoredPosition = Vector2.Lerp(mapFrame.anchoredPosition, pos, k);
            mapFrame.gameObject.SetActive(!game.IsPaused && !game.ShowResults);

            var cam = minimap.MapCamera;
            if (cam == null || minimap.Target == null) return;
            Rect area = mapArea.rect;

            // Xe người chơi
            Vector3 pv = cam.WorldToViewportPoint(minimap.Target.position);
            playerMarker.anchoredPosition = new Vector2((pv.x - 0.5f) * area.width, (pv.y - 0.5f) * area.height);
            playerMarker.localEulerAngles = new Vector3(0f, 0f, minimap.rotateWithPlayer ? 0f : -minimap.Target.eulerAngles.y);
            playerMarker.SetAsLastSibling();

            // Cảnh sát
            var list = minimap.TrackedPolice;
            int count = 0;
            bool blink = Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f;
            if (list != null)
                foreach (var p in list)
                {
                    if (p == null) continue;
                    if (count >= policeMarkers.Count)
                    {
                        var m = UIKit.IconImage(mapArea, "circle", Color.red, 20f);
                        m.rectTransform.anchorMin = m.rectTransform.anchorMax = m.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        policeMarkers.Add(m);
                    }
                    var mk = policeMarkers[count++];
                    mk.gameObject.SetActive(true);
                    Vector3 vp = cam.WorldToViewportPoint(p.transform.position);
                    float hx = area.width * 0.5f - 12f, hy = area.height * 0.5f - 12f;
                    mk.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp((vp.x - 0.5f) * area.width, -hx, hx),
                                                                    Mathf.Clamp((vp.y - 0.5f) * area.height, -hy, hy));
                    mk.color = p.state == PoliceAI.State.Leave ? new Color(0.6f, 0.6f, 0.6f) : (blink ? UIKit.Danger : UIKit.Accent);
                }
            for (int i = count; i < policeMarkers.Count; i++) policeMarkers[i].gameObject.SetActive(false);

            // Điểm năng lượng tên lửa (tia sét vàng, nằm ngoài thì dính ở mép bản đồ)
            int bc = 0;
            float bx = area.width * 0.5f - 12f, by = area.height * 0.5f - 12f;
            foreach (var b in BoostPickup.All)
            {
                if (b == null || b.Taken) continue;
                if (bc >= boostMarkers.Count)
                {
                    var m = UIKit.IconImage(mapArea, "bolt", GameSkin.Yellow, 24f);
                    m.rectTransform.anchorMin = m.rectTransform.anchorMax = m.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    boostMarkers.Add(m);
                }
                var mk = boostMarkers[bc++];
                mk.gameObject.SetActive(true);
                Vector3 vp = cam.WorldToViewportPoint(b.transform.position);
                mk.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp((vp.x - 0.5f) * area.width, -bx, bx),
                                                                Mathf.Clamp((vp.y - 0.5f) * area.height, -by, by));
                mk.color = blink ? GameSkin.Yellow : new Color(0.4f, 0.9f, 1f);
            }
            for (int i = bc; i < boostMarkers.Count; i++) boostMarkers[i].gameObject.SetActive(false);
            playerMarker.SetAsLastSibling();
        }

        void ShowResults()
        {
            resultsShown = true;
            resultsStart = Time.unscaledTime;
            resultPanel.Show();

            bool chase = GameSession.Mode == GameMode.Chase;
            if (game.Reason == GameplayBootstrap.EndReason.Busted) { resultTitle.text = "BỊ BẮT!"; resultTitle.color = UIKit.Danger; }
            else if (chase) { resultTitle.text = "BẠN ĐÃ THOÁT!"; resultTitle.color = UIKit.Success; }
            else { resultTitle.text = "HẾT GIỜ"; resultTitle.color = UIKit.TextColor; }

            var rows = new List<(string, string)>
            {
                ("Thời gian", UIKit.Clock(game.Elapsed)),
            };
            if (chase) rows.Add(("Mức truy nã cao nhất", game.MaxWanted + " sao"));
            rows.Add(("Xu nhặt được", "+" + (GameSession.RunCoins - game.EndBonus)));
            if (game.EndBonus > 0) rows.Add(("Thưởng hoàn thành", "+" + game.EndBonus));
            rows.Add(("Tổng xu", SaveSystem.Coins.ToString("N0")));
            if (chase) rows.Add((game.NewRecord ? "KỶ LỤC MỚI!" : "Kỷ lục sống sót", UIKit.Clock(SaveSystem.Data.bestSurvivalSeconds)));

            for (int i = 0; i < resultLabels.Count; i++)
            {
                bool on = i < rows.Count;
                resultLabels[i].transform.parent.gameObject.SetActive(on);
                if (!on) continue;
                resultLabels[i].text = rows[i].Item1;
                resultValues[i].text = rows[i].Item2;
                bool gold = rows[i].Item1.StartsWith("Xu") || rows[i].Item1.StartsWith("Thưởng") || rows[i].Item1.StartsWith("Tổng");
                resultValues[i].color = gold ? UIKit.Gold : (rows[i].Item1 == "KỶ LỤC MỚI!" ? UIKit.Success : UIKit.TextColor);
                resultLabels[i].color = rows[i].Item1 == "KỶ LỤC MỚI!" ? UIKit.Success : UIKit.TextMuted;
            }
            resultNote.text = game.Reason == GameplayBootstrap.EndReason.Busted ? "Bạn vẫn giữ toàn bộ xu đã nhặt" : "";
        }

        void AnimateResults(float t)
        {
            int rating = game.RatingStars();
            for (int i = 0; i < 3; i++)
            {
                float st = t - 0.25f - i * 0.22f;
                float s = st <= 0f ? 0f : (st < 0.25f ? Mathf.Lerp(0f, 1.25f, st / 0.25f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((st - 0.25f) / 0.15f)));
                resultStars[i].rectTransform.localScale = new Vector3(s, s, 1f);
                resultStars[i].color = i < rating ? UIKit.Gold : new Color(1f, 1f, 1f, 0.12f);
            }
        }
    }
}
