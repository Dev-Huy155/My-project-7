using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PoliceChase.Game;

namespace PoliceChase.UI
{
    /// <summary>
    /// Menu garage kiểu game đua xe (neon xanh + vàng), dựng hoàn toàn bằng code:
    /// logo + tên game, xu + cài đặt, bảng NÂNG CẤP, thẻ thông tin xe, bảng CHẾ ĐỘ CHƠI, nút CHƠI vàng,
    /// phông thành phố đêm + bệ xe phát sáng. Chạy trên PC và điện thoại.
    /// Đặt vào 1 GameObject rỗng trong scene MainMenu.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Tên scene chơi (phải có trong File > Build Profiles > Scene List)")]
        public string gameSceneName = "CityGame";
        public string gameTitle = "CẢNH SÁT TRUY ĐUỔI";
        public string gameSubtitle = "SPEED CHASE";
        public string tagline = "TRỐN THOÁT  •  TĂNG TỐC  •  HUYỀN THOẠI";
        public float turntableSpeed = 18f;

        [Header("Camera xem xe")]
        public Vector3 cameraPosition = new Vector3(0f, 3.1f, -10.5f);
        public Vector3 cameraLookAt = new Vector3(0f, -0.55f, 0f);
        public float cameraFov = 31f;

        static readonly UpgradeType[] UpgradeOrder = { UpgradeType.Engine, UpgradeType.Handling, UpgradeType.Durability, UpgradeType.Nitro };
        static readonly string[] UpgradeIcons = { "engine", "spring", "disc", "bottle" };
        static readonly float[] DurationValues = { 180f, 300f, 0f };
        static readonly string[] DurationNames = { "3 phút", "5 phút", "Không giới hạn" };

        CarCatalog catalog;
        int index;
        Transform turntable, currentModel;
        CarStats maxStats;

        // Phần giao diện cần cập nhật
        RectTransform coinIcon;
        Text coinsText, carNameText, statusText, modeDesc, toastText;
        NeonFrame statusPill;
        Image statusIcon;
        NeonButton actionButton, playButton;
        CanvasGroup toastGroup;
        float toastTime = -100f, toastDuration = 2.2f;
        readonly List<ProgressBar> statBars = new List<ProgressBar>();
        readonly List<Text> statValues = new List<Text>();
        readonly List<NeonButton> upgradeButtons = new List<NeonButton>();
        readonly List<Image[]> upgradePips = new List<Image[]>();
        readonly List<NeonButton> modeButtons = new List<NeonButton>();
        readonly List<NeonButton> durationButtons = new List<NeonButton>();
        readonly List<NeonButton> qualityButtons = new List<NeonButton>();
        NeonButton resetButton;
        GameObject upgradesLocked;
        UIFade settingsPanel;
        float resetArmedUntil;

        void Start()
        {
            catalog = CarCatalog.Load();
            SceneLoader.GameTitle = gameTitle;
            if (GameSession.RoundSeconds < 0f) GameSession.RoundSeconds = DurationValues[0];
            SetupScene();
            BuildUI();

            if (catalog == null || catalog.cars.Count == 0)
            {
                Toast("Chưa có danh sách xe – dùng Tools > Police Chase > Tạo danh sách xe tự động", 999f);
                return;
            }
            ComputeMaxStats();
            index = Mathf.Max(0, catalog.IndexOf(SaveSystem.Data.selectedCar));
            ShowCar();
            SaveSystem.Changed += Refresh;
        }

        void OnDestroy() => SaveSystem.Changed -= Refresh;

        void Update()
        {
            if (turntable != null) turntable.Rotate(0f, turntableSpeed * Time.deltaTime, 0f);

            float ta = Time.unscaledTime - toastTime;
            toastGroup.alpha = ta < 0.15f ? ta / 0.15f : (ta < toastDuration ? 1f : Mathf.Clamp01(1f - (ta - toastDuration) / 0.3f));

            if (resetButton != null && resetArmedUntil > 0f && Time.unscaledTime > resetArmedUntil)
            {
                resetArmedUntil = 0f;
                resetButton.SetText("XOÁ DỮ LIỆU CHƠI");
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || settingsPanel.gameObject.activeSelf) return;
            if (catalog != null && catalog.cars.Count > 0)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Step(-1);
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Step(1);
                if (kb.enterKey.wasPressedThisFrame) OnPlay();
            }
        }

        // ───────────────────────── Hành động ─────────────────────────

        CarDef Current => catalog != null && catalog.cars.Count > 0 ? catalog.cars[Mathf.Clamp(index, 0, catalog.cars.Count - 1)] : null;

        void Step(int dir)
        {
            if (catalog == null || catalog.cars.Count == 0) return;
            index = (index + dir + catalog.cars.Count) % catalog.cars.Count;
            ShowCar();
        }

        void OnAction()
        {
            var car = Current;
            if (car == null) return;
            if (!SaveSystem.Owns(car.id))
            {
                if (SaveSystem.TryBuy(car)) { SaveSystem.Select(car.id); Toast($"Đã mua {car.displayName}!"); }
                else Toast($"Chưa đủ xu · cần thêm {car.price - SaveSystem.Coins:N0} xu");
            }
            else if (SaveSystem.Data.selectedCar != car.id)
            {
                SaveSystem.Select(car.id);
                Toast($"Đã chọn {car.displayName}");
            }
            Refresh();
        }

        void OnUpgrade(UpgradeType type)
        {
            var car = Current;
            if (car == null || !SaveSystem.Owns(car.id)) return;
            int level = SaveSystem.GetUpgrades(car.id).Get(type);
            if (level >= UpgradeRules.MaxLevel) { Toast("Đã nâng cấp tối đa"); return; }
            int cost = UpgradeRules.Cost(car, type, level);
            if (SaveSystem.TryUpgrade(car, type)) Toast($"{UpgradeRules.Name(type)} lên cấp {level + 1}!");
            else Toast($"Chưa đủ xu · cần {cost:N0} xu");
            Refresh();
        }

        void SetMode(GameMode m) { GameSession.Mode = m; Refresh(); }
        void SetDuration(float s) { GameSession.RoundSeconds = s; Refresh(); }

        void OnPlay()
        {
            var sel = catalog != null ? catalog.Find(SaveSystem.Data.selectedCar) : null;
            if (sel != null && !SaveSystem.Owns(sel.id)) { Toast("Hãy chọn một xe bạn đã sở hữu"); return; }
            if (!SceneLoader.Load(gameSceneName, "Đang vào thành phố..."))
                Toast($"Chưa thêm scene '{gameSceneName}' vào File > Build Profiles", 5f);
        }

        void Toast(string msg, float seconds = 2.2f)
        {
            toastText.text = msg;
            toastTime = Time.unscaledTime;
            toastDuration = seconds;
        }

        // ───────────────────────── Cập nhật giao diện ─────────────────────────

        void ShowCar()
        {
            var car = Current;
            if (car == null) return;
            if (currentModel != null) Destroy(currentModel.gameObject);
            currentModel = BuildPreview(car);
            Refresh();
        }

        void Refresh()
        {
            coinsText.text = "Xu: " + SaveSystem.Coins.ToString("N0");
            coinIcon.anchoredPosition = new Vector2(-150f - coinsText.preferredWidth - 14f, -24f);

            bool chase = GameSession.Mode == GameMode.Chase;
            modeButtons[0].SetStyle(chase ? ButtonStyle.Blue : ButtonStyle.Dark);
            modeButtons[1].SetStyle(chase ? ButtonStyle.Dark : ButtonStyle.Blue);
            modeDesc.text = chase
                ? "Bị truy nã! Trốn cảnh sát, nhặt xu. Bị truy nã càng lâu, càng nhiều sao, thưởng càng lớn."
                : "Không có cảnh sát. Thoải mái dạo phố, nhặt xu và chạy thử xe.";
            for (int i = 0; i < DurationValues.Length; i++)
                durationButtons[i].SetStyle(Mathf.Approximately(GameSession.RoundSeconds, DurationValues[i]) ? ButtonStyle.Blue : ButtonStyle.Dark);
            RefreshQuality();

            var car = Current;
            if (car == null) return;
            bool owned = SaveSystem.Owns(car.id);
            bool selected = SaveSystem.Data.selectedCar == car.id;
            var up = SaveSystem.GetUpgrades(car.id);
            var s = UpgradeRules.Compute(car, up);

            // Thẻ xe
            carNameText.text = car.displayName;
            if (owned)
            {
                statusText.text = selected ? "Đang dùng" : "Đã sở hữu";
                statusText.color = new Color(0.45f, 1f, 0.6f);
                statusIcon.sprite = UIKit.Icon("checkCircle");
                statusIcon.color = new Color(0.3f, 0.95f, 0.5f);
                statusPill.SetColors(new Color(0.05f, 0.25f, 0.14f, 0.95f), new Color(0.02f, 0.14f, 0.08f, 0.95f), new Color(0.35f, 1f, 0.55f, 0.7f), Color.clear);
            }
            else
            {
                statusText.text = $"{car.price:N0} Xu";
                statusText.color = GameSkin.Yellow;
                statusIcon.sprite = UIKit.Icon("lock");
                statusIcon.color = GameSkin.Yellow;
                statusPill.SetColors(new Color(0.28f, 0.2f, 0.04f, 0.95f), new Color(0.16f, 0.1f, 0.02f, 0.95f), new Color(1f, 0.8f, 0.3f, 0.7f), Color.clear);
            }

            float[] values = { s.maxSpeed, s.acceleration, s.turnRate * s.grip, s.mass, s.nitroSeconds };
            float[] maxes = { maxStats.maxSpeed, maxStats.acceleration, maxStats.turnRate * maxStats.grip, maxStats.mass, maxStats.nitroSeconds };
            float handling = maxes[2] > 0f ? values[2] / maxes[2] * 100f : 0f;
            string[] labels =
            {
                $"{s.maxSpeed * 3.6f:0} km/h", $"{s.acceleration:0.0}", $"{handling:0} %", $"{s.mass:0} kg",
                s.nitroSeconds > 0f ? $"{s.nitroSeconds:0.0} giây" : "Chưa có",
            };
            for (int i = 0; i < statBars.Count; i++)
            {
                statBars[i].Set(maxes[i] > 0f ? Mathf.Max(0.03f, values[i] / maxes[i]) : 0.03f);
                statValues[i].text = labels[i];
            }

            // Nút hành động
            if (!owned)
            {
                bool afford = SaveSystem.Coins >= car.price;
                actionButton.SetText($"MUA XE · {car.price:N0} XU");
                actionButton.SetIcon("lock");
                actionButton.SetStyle(afford ? ButtonStyle.Green : ButtonStyle.Disabled);
            }
            else if (!selected)
            {
                actionButton.SetText("CHỌN XE NÀY");
                actionButton.SetIcon("play");
                actionButton.SetStyle(ButtonStyle.Blue);
            }
            else
            {
                actionButton.SetText("ĐANG DÙNG XE NÀY");
                actionButton.SetIcon("checkCircle");
                actionButton.SetStyle(ButtonStyle.Dark);
            }

            var sel = catalog.Find(SaveSystem.Data.selectedCar);
            playButton.SetText(sel != null ? "CHƠI  ·  " + sel.displayName : "CHƠI");

            // Nâng cấp
            upgradesLocked.SetActive(!owned);
            for (int i = 0; i < UpgradeOrder.Length; i++)
            {
                int level = up.Get(UpgradeOrder[i]);
                for (int p = 0; p < upgradePips[i].Length; p++)
                    upgradePips[i][p].color = p < level ? GameSkin.Yellow : GameSkin.PipOff;
                if (level >= UpgradeRules.MaxLevel)
                {
                    upgradeButtons[i].SetText("TỐI ĐA");
                    upgradeButtons[i].SetStyle(ButtonStyle.Dark);
                }
                else
                {
                    int cost = UpgradeRules.Cost(car, UpgradeOrder[i], level);
                    upgradeButtons[i].SetText($"{cost:N0} Xu");
                    upgradeButtons[i].SetStyle(owned && SaveSystem.Coins >= cost ? ButtonStyle.Blue : ButtonStyle.Disabled);
                }
            }
        }

        void ComputeMaxStats()
        {
            var full = new CarUpgrades { engine = UpgradeRules.MaxLevel, handling = UpgradeRules.MaxLevel, durability = UpgradeRules.MaxLevel, nitro = UpgradeRules.MaxLevel };
            maxStats = default;
            foreach (var c in catalog.cars)
            {
                if (c == null) continue;
                var s = UpgradeRules.Compute(c, full);
                maxStats.maxSpeed = Mathf.Max(maxStats.maxSpeed, s.maxSpeed);
                maxStats.acceleration = Mathf.Max(maxStats.acceleration, s.acceleration);
                maxStats.turnRate = Mathf.Max(maxStats.turnRate, s.turnRate);
                maxStats.grip = Mathf.Max(maxStats.grip, s.grip);
                maxStats.mass = Mathf.Max(maxStats.mass, s.mass);
                maxStats.nitroSeconds = Mathf.Max(maxStats.nitroSeconds, s.nitroSeconds);
            }
        }

        // ───────────────────────── Cảnh 3D ─────────────────────────

        void SetupScene()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.transform.position = cameraPosition;
            cam.transform.LookAt(cameraLookAt);
            cam.fieldOfView = cameraFov;

            var root = new GameObject("CarPreview").transform;
            MenuBackdrop.Build(cam, root);
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(root, false);
            turntable.localPosition = new Vector3(0f, 0.065f, 0f);
        }

        Transform BuildPreview(CarDef car)
        {
            GameObject go;
            if (car.model != null) go = Instantiate(car.model);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = new Vector3(1.9f, 1.2f, 4.3f);
            }
            go.name = "PreviewCar";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in go.GetComponentsInChildren<Rigidbody>()) Destroy(r);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;

            go.transform.SetParent(turntable, false);
            go.transform.localRotation = Quaternion.identity;
            if (TryBounds(go, out Bounds b))
            {
                if (b.size.x > b.size.z * 1.2f) { go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); TryBounds(go, out b); }
                float s = 4.6f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
                go.transform.localScale *= s;
                TryBounds(go, out b);
                go.transform.position += new Vector3(turntable.position.x - b.center.x, turntable.position.y - b.min.y, turntable.position.z - b.center.z);
            }
            return go.transform;
        }

        static bool TryBounds(GameObject go, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        // ───────────────────────── Dựng giao diện ─────────────────────────

        void BuildUI()
        {
            var canvas = UIKit.CreateCanvas("MenuCanvas");
            BuildBottomBar(canvas.transform);
            var safe = UIKit.SafeArea(canvas.transform);

            BuildTopBar(safe);
            BuildUpgrades(safe);
            BuildModePanel(safe);
            BuildCarCard(safe);

            // Thông báo
            var toast = GameSkin.Frame(safe, "Toast", FrameShape.Pill, GameSkin.PanelTop, GameSkin.PanelBottom, GameSkin.Edge, GameSkin.Glow);
            UIKit.Place(toast.rect, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(780f, 66f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastText = UIKit.Label(toast.rect, "", 28, GameSkin.TextLight);
            UIKit.Anchors(toastText.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, 0f));

            BuildSettings(canvas.transform);
        }

        void BuildTopBar(RectTransform safe)
        {
            var logo = new GameObject("Logo", typeof(RectTransform)).AddComponent<Image>();
            logo.transform.SetParent(safe, false);
            logo.sprite = GameSkin.SpeedLogo;
            logo.raycastTarget = false;
            UIKit.Place(logo.rectTransform, new Vector2(0f, 1f), new Vector2(26f, 6f), new Vector2(160f, 160f));

            var title = GameSkin.Title(safe, gameTitle, 76, Color.white);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(196f, -14f), new Vector2(1000f, 90f));
            title.gameObject.AddComponent<UIGradient>().Set(Color.white, new Color(0.72f, 0.84f, 1f));
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.2f, 0.6f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            var sub = GameSkin.Title(safe, gameSubtitle, 36, GameSkin.Cyan);
            UIKit.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(202f, -100f), new Vector2(600f, 44f));
            var stripes = new GameObject("Stripes", typeof(RectTransform)).AddComponent<Image>();
            stripes.transform.SetParent(safe, false);
            stripes.sprite = GameSkin.Stripes;
            stripes.color = new Color(0.25f, 0.55f, 1f, 0.8f);
            stripes.raycastTarget = false;
            UIKit.Place(stripes.rectTransform, new Vector2(0f, 1f), new Vector2(212f + sub.preferredWidth, -104f), new Vector2(200f, 36f));

            // Xu + cài đặt (góc phải)
            var gear = GameSkin.Button(safe, "", ButtonStyle.Dark, OpenSettings, "gear", 46);
            gear.frame.SetColors(new Color(0.1f, 0.18f, 0.4f), new Color(0.03f, 0.07f, 0.18f), GameSkin.Edge, GameSkin.Glow);
            UIKit.Place(gear.frame.rect, new Vector2(1f, 1f), new Vector2(-26f, -20f), new Vector2(92f, 92f));
            gear.content.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);

            var div = UIKit.Box(safe, "Divider", new Color(0.7f, 0.8f, 1f, 0.6f), true);
            UIKit.Place(div.rectTransform, new Vector2(1f, 1f), new Vector2(-142f, -36f), new Vector2(4f, 60f));

            coinsText = GameSkin.Title(safe, "Xu: 0", 52, GameSkin.Yellow, TextAnchor.MiddleRight);
            UIKit.Place(coinsText.rectTransform, new Vector2(1f, 1f), new Vector2(-160f, -20f), new Vector2(420f, 92f));
            var coin = new GameObject("Coin", typeof(RectTransform)).AddComponent<Image>();
            coin.transform.SetParent(safe, false);
            coin.sprite = GameSkin.Coin;
            coin.raycastTarget = false;
            coinIcon = coin.rectTransform;
            UIKit.Place(coinIcon, new Vector2(1f, 1f), new Vector2(-300f, -24f), new Vector2(72f, 72f));
        }

        void BuildBottomBar(Transform canvas)
        {
            var bar = UIKit.NewRect(canvas, "BottomBar").gameObject.AddComponent<Image>();
            bar.raycastTarget = false;
            UIKit.Anchors(bar.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 84f));
            bar.gameObject.AddComponent<UIGradient>().Set(new Color(0.06f, 0.12f, 0.3f, 0.95f), new Color(0.01f, 0.03f, 0.09f, 0.98f));

            var glow = UIKit.NewRect(bar.transform, "Glow").gameObject.AddComponent<Image>();
            glow.sprite = GameSkin.SoftDot;
            glow.color = new Color(0.2f, 0.55f, 1f, 0.55f);
            glow.raycastTarget = false;
            UIKit.Anchors(glow.rectTransform, new Vector2(0.1f, 1f), new Vector2(0.9f, 1f), new Vector2(0f, -14f), new Vector2(0f, 14f));
            var line = UIKit.NewRect(bar.transform, "Line").gameObject.AddComponent<Image>();
            line.color = new Color(0.4f, 0.75f, 1f);
            line.raycastTarget = false;
            UIKit.Anchors(line.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), Vector2.zero);

            var stripes = UIKit.NewRect(bar.transform, "Stripes").gameObject.AddComponent<Image>();
            stripes.sprite = GameSkin.Stripes;
            stripes.color = new Color(0.25f, 0.55f, 1f, 0.7f);
            stripes.raycastTarget = false;
            UIKit.Place(stripes.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, -4f), new Vector2(150f, 46f));

            var hint = UIKit.Label(bar.transform, "A / D  đổi xe     ENTER  vào chơi", 24, GameSkin.TextDim, TextAnchor.MiddleLeft, false);
            UIKit.Place(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(200f, -4f), new Vector2(600f, 40f));
            if (Application.isMobilePlatform) hint.gameObject.SetActive(false);

            var tag = GameSkin.Title(bar.transform, tagline, 32, new Color(0.6f, 0.72f, 0.95f), TextAnchor.MiddleRight);
            UIKit.Place(tag.rectTransform, new Vector2(1f, 0.5f), new Vector2(-150f, -4f), new Vector2(900f, 50f));
            var flag = UIKit.IconImage(bar.transform, "flag", new Color(0.5f, 0.7f, 1f, 0.9f), 56f);
            UIKit.Place(flag.rectTransform, new Vector2(1f, 0.5f), new Vector2(-70f, -4f), new Vector2(56f, 56f));
        }

        void BuildUpgrades(RectTransform safe)
        {
            var panel = GameSkin.Panel(safe, "Upgrades");
            UIKit.Place(panel.rect, new Vector2(0f, 1f), new Vector2(28f, -150f), new Vector2(520f, 740f));
            GameSkin.Header(panel.rect, "wrench", "NÂNG CẤP");

            for (int i = 0; i < UpgradeOrder.Length; i++)
            {
                var type = UpgradeOrder[i];
                var row = GameSkin.Row(panel.rect, "Row_" + type);
                UIKit.Anchors(row.rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -90f - (i + 1) * 158f + 12f), new Vector2(-16f, -90f - i * 158f));

                var tile = GameSkin.IconTile(row.rect, UpgradeIcons[i], 118f);
                UIKit.Place(tile.rect, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(118f, 118f));

                var name = UIKit.Label(row.rect, UpgradeRules.Name(type), 34, GameSkin.TextLight, TextAnchor.MiddleLeft);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(146f, -10f), new Vector2(330f, 40f));
                var desc = UIKit.Label(row.rect, UpgradeRules.Description(type), 22, GameSkin.TextDim, TextAnchor.MiddleLeft, false);
                desc.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(146f, -50f), new Vector2(330f, 28f));

                upgradePips.Add(GameSkin.Pips(row.rect, UpgradeRules.MaxLevel, new Vector2(146f, 18f), new Vector2(30f, 24f), 6f));

                var t = type;
                var btn = GameSkin.Button(row.rect, "0 Xu", ButtonStyle.Blue, () => OnUpgrade(t), null, 32);
                UIKit.Place(btn.frame.rect, new Vector2(1f, 0f), new Vector2(-12f, 10f), new Vector2(160f, 56f));
                upgradeButtons.Add(btn);
            }

            var locked = UIKit.Box(panel.rect, "Locked", new Color(0.02f, 0.04f, 0.1f, 0.86f), false, 1f);
            UIKit.Anchors(locked.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -84f));
            var lockIcon = UIKit.IconImage(locked.transform, "lock", GameSkin.Yellow, 80f);
            UIKit.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(80f, 80f));
            var lockText = GameSkin.Title(locked.transform, "MUA XE NÀY ĐỂ NÂNG CẤP", 34, GameSkin.TextLight, TextAnchor.MiddleCenter);
            UIKit.Place(lockText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(460f, 50f));
            upgradesLocked = locked.gameObject;
        }

        void BuildModePanel(RectTransform safe)
        {
            var panel = GameSkin.Panel(safe, "Mode");
            UIKit.Place(panel.rect, new Vector2(1f, 1f), new Vector2(-28f, -150f), new Vector2(520f, 740f));
            GameSkin.Header(panel.rect, "flag", "CHẾ ĐỘ CHƠI");

            var chase = GameSkin.Button(panel.rect, "TRỐN CẢNH SÁT", ButtonStyle.Blue, () => SetMode(GameMode.Chase), "flag", 40, true);
            TopRow(chase.frame.rect, -90f, 88f);
            var free = GameSkin.Button(panel.rect, "LÁI TỰ DO", ButtonStyle.Dark, () => SetMode(GameMode.FreeRoam), "car", 40, true);
            TopRow(free.frame.rect, -190f, 88f);
            modeButtons.Add(chase);
            modeButtons.Add(free);

            var info = UIKit.IconImage(panel.rect, "info", new Color(0.35f, 0.65f, 1f), 40f);
            UIKit.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -298f), new Vector2(40f, 40f));
            modeDesc = UIKit.Label(panel.rect, "", 23, GameSkin.TextDim, TextAnchor.UpperLeft, false);
            UIKit.Place(modeDesc.rectTransform, new Vector2(0f, 1f), new Vector2(82f, -294f), new Vector2(412f, 80f));

            var h2 = GameSkin.Title(panel.rect, "THỜI GIAN MỖI VÁN", 34, GameSkin.Yellow);
            UIKit.Place(h2.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -378f), new Vector2(460f, 44f));
            for (int i = 0; i < DurationNames.Length; i++)
            {
                float v = DurationValues[i];
                var b = GameSkin.Button(panel.rect, DurationNames[i], ButtonStyle.Dark, () => SetDuration(v), null, i == 2 ? 24 : 28);
                float w = (520f - 44f - 20f) / 3f;
                var rt = b.frame.rect;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(22f + i * (w + 10f), -430f);
                rt.sizeDelta = new Vector2(w, 62f);
                b.content.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(4, 4, 0, 0);
                durationButtons.Add(b);
            }

            actionButton = GameSkin.Button(panel.rect, "CHỌN XE NÀY", ButtonStyle.Blue, OnAction, "play", 38);
            TopRow(actionButton.frame.rect, -510f, 84f);

            playButton = GameSkin.Button(panel.rect, "CHƠI", ButtonStyle.Gold, OnPlay, null, 52, false, FrameShape.Chamfer);
            TopRow(playButton.frame.rect, -612f, 106f);
        }

        static void TopRow(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(22f, y - height);
            rt.offsetMax = new Vector2(-22f, y);
        }

        void BuildCarCard(RectTransform safe)
        {
            var card = GameSkin.Panel(safe, "CarInfo", false);
            UIKit.Place(card.rect, new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(800f, 356f));

            var prev = GameSkin.Button(card.rect, "", ButtonStyle.Dark, () => Step(-1), "chevronLeft", 40);
            prev.frame.SetColors(new Color(0.1f, 0.2f, 0.45f), new Color(0.04f, 0.08f, 0.2f), GameSkin.Edge, Color.clear);
            UIKit.Place(prev.frame.rect, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(80f, 80f));
            prev.content.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            var next = GameSkin.Button(card.rect, "", ButtonStyle.Dark, () => Step(1), "chevron", 40);
            next.frame.SetColors(new Color(0.1f, 0.2f, 0.45f), new Color(0.04f, 0.08f, 0.2f), GameSkin.Edge, Color.clear);
            UIKit.Place(next.frame.rect, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(80f, 80f));
            next.content.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);

            carNameText = UIKit.Label(card.rect, "", 58, Color.white);
            UIKit.Place(carNameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(560f, 70f));
            carNameText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.05f, 0.2f, 0.8f);

            statusPill = GameSkin.Frame(card.rect, "Status", FrameShape.Pill, Color.black, Color.black, Color.white, Color.clear);
            UIKit.Place(statusPill.rect, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(250f, 46f));
            statusIcon = UIKit.IconImage(statusPill.rect, "checkCircle", Color.white, 30f);
            UIKit.Place(statusIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(30f, 30f));
            statusText = UIKit.Label(statusPill.rect, "", 28, Color.white);
            UIKit.Anchors(statusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(46f, 0f), new Vector2(-14f, 0f));

            string[] names = { "Tốc độ", "Tăng tốc", "Lái", "Độ bền", "Nitro" };
            string[] icons = { "speed", "rocket", "steering", "shield", "bolt" };
            for (int i = 0; i < names.Length; i++)
            {
                float y = -150f - i * 40f;
                var ic = UIKit.IconImage(card.rect, icons[i], new Color(0.75f, 0.86f, 1f), 32f);
                UIKit.Place(ic.rectTransform, new Vector2(0f, 1f), new Vector2(32f, y + 2f), new Vector2(32f, 32f));
                var l = UIKit.Label(card.rect, names[i], 27, GameSkin.TextLight, TextAnchor.MiddleLeft);
                UIKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(78f, y + 4f), new Vector2(150f, 36f));
                var bar = GameSkin.StatBar(card.rect);
                UIKit.Anchors((RectTransform)bar.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(232f, y - 24f), new Vector2(-186f, y - 6f));
                statBars.Add(bar);
                var v = UIKit.Label(card.rect, "", 27, GameSkin.TextLight, TextAnchor.MiddleRight);
                UIKit.Place(v.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, y + 4f), new Vector2(150f, 36f));
                statValues.Add(v);
            }
        }

        // ───────────────────────── Cài đặt ─────────────────────────

        void BuildSettings(Transform canvas)
        {
            var root = UIKit.NewRect(canvas, "Settings");
            UIKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UIKit.Dim;
            settingsPanel = root.gameObject.AddComponent<UIFade>();

            var panel = GameSkin.Panel(root, "Card");
            UIKit.Place(panel.rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 540f));
            panel.rect.pivot = new Vector2(0.5f, 0.5f);
            GameSkin.Header(panel.rect, "gear", "CÀI ĐẶT");

            var close = GameSkin.Button(panel.rect, "", ButtonStyle.Dark, () => settingsPanel.Hide(), "close", 34);
            UIKit.Place(close.frame.rect, new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(64f, 64f));
            close.content.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);

            var q = GameSkin.Title(panel.rect, "CHẤT LƯỢNG ĐỒ HOẠ", 32, GameSkin.Yellow);
            UIKit.Place(q.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -104f), new Vector2(560f, 44f));
            string[] qn = { "Thấp", "Vừa", "Cao" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = GameSkin.Button(panel.rect, qn[i], ButtonStyle.Dark, () => SetQuality(k), null, 30);
                float w = (660f - 44f - 20f) / 3f;
                var rt = b.frame.rect;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(22f + i * (w + 10f), -156f);
                rt.sizeDelta = new Vector2(w, 70f);
                qualityButtons.Add(b);
            }

            resetButton = GameSkin.Button(panel.rect, "XOÁ DỮ LIỆU CHƠI", ButtonStyle.Dark, OnResetPressed, "restart", 32);
            TopRow(resetButton.frame.rect, -262f, 80f);
            var quit = GameSkin.Button(panel.rect, "THOÁT GAME", ButtonStyle.Dark, Application.Quit, "power", 32);
            TopRow(quit.frame.rect, -356f, 80f);
            var back = GameSkin.Button(panel.rect, "QUAY LẠI", ButtonStyle.Blue, () => settingsPanel.Hide(), "chevronLeft", 36);
            TopRow(back.frame.rect, -446f, 74f);

            root.gameObject.SetActive(false);
        }

        void OpenSettings()
        {
            RefreshQuality();
            settingsPanel.Show();
        }

        int QualityLevelFor(int option)
        {
            int n = QualitySettings.names.Length;
            return option == 0 ? 0 : (option == 1 ? n / 2 : n - 1);
        }

        void SetQuality(int option)
        {
            QualitySettings.SetQualityLevel(QualityLevelFor(option), true);
            RefreshQuality();
            Toast("Đồ hoạ: " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        }

        void RefreshQuality()
        {
            int cur = QualitySettings.GetQualityLevel();
            int best = 0;
            for (int i = 0; i < 3; i++)
                if (Mathf.Abs(QualityLevelFor(i) - cur) < Mathf.Abs(QualityLevelFor(best) - cur)) best = i;
            for (int i = 0; i < qualityButtons.Count; i++) qualityButtons[i].SetStyle(i == best ? ButtonStyle.Blue : ButtonStyle.Dark);
        }

        void OnResetPressed()
        {
            if (resetArmedUntil <= 0f)
            {
                resetArmedUntil = Time.unscaledTime + 3f;
                resetButton.SetText("BẤM LẦN NỮA ĐỂ XOÁ");
                return;
            }
            resetArmedUntil = 0f;
            resetButton.SetText("XOÁ DỮ LIỆU CHƠI");
            SaveSystem.ResetAll();
            index = Mathf.Max(0, catalog != null ? catalog.IndexOf(SaveSystem.Data.selectedCar) : 0);
            ShowCar();
            Toast("Đã xoá dữ liệu chơi");
        }

        // ───────────────────────── Thử nghiệm ─────────────────────────

        [ContextMenu("Thử nghiệm: +1000 xu")]
        void AddTestCoins() => SaveSystem.AddCoins(1000);

        [ContextMenu("Thử nghiệm: xoá toàn bộ dữ liệu lưu")]
        void ResetSave() => SaveSystem.ResetAll();
    }
}
