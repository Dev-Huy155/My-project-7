using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PoliceChase.Game;

namespace PoliceChase.UI
{
    /// <summary>
    /// Menu chính / garage – dựng toàn bộ giao diện bằng code (không cần kéo thả UI):
    /// - xem xe 3D quay trên bệ, chuyển xe bằng nút &lt; &gt; (hoặc phím ← →),
    /// - mua xe, chọn xe đang dùng, nâng cấp 4 hạng mục (Động cơ, Lái, Độ bền, Nitro),
    /// - chọn chế độ (Trốn cảnh sát / Lái tự do) rồi bấm CHƠI.
    /// Đặt vào 1 GameObject rỗng trong scene MainMenu.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Tên scene chơi (phải có trong File > Build Profiles > Scene List)")]
        public string gameSceneName = "ColorCity";
        public string gameTitle = "CẢNH SÁT TRUY ĐUỔI";
        public Color backgroundColor = new Color(0.12f, 0.14f, 0.2f);
        public float turntableSpeed = 25f;

        static readonly UpgradeType[] UpgradeOrder = { UpgradeType.Engine, UpgradeType.Handling, UpgradeType.Durability, UpgradeType.Nitro };
        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color Accent = new Color(1f, 0.78f, 0.15f);
        static readonly Color Green = new Color(0.25f, 0.75f, 0.35f);
        static readonly Color Blue = new Color(0.25f, 0.55f, 0.95f);
        static readonly Color Gray = new Color(0.35f, 0.37f, 0.42f);

        CarCatalog catalog;
        int index;
        Font font;

        // Xem trước 3D
        Transform turntable, currentModel;

        // Các phần giao diện cần cập nhật
        Text coinsText, carNameText, carStatusText, actionLabel, modeDesc, messageText, playLabel;
        Image actionImage;
        Button actionButton;
        readonly List<Image> statFills = new List<Image>();
        readonly List<Text> statValues = new List<Text>();
        readonly List<Text> upgradeCostLabels = new List<Text>();
        readonly List<Image> upgradeButtonImages = new List<Image>();
        readonly List<Image[]> upgradePips = new List<Image[]>();
        Image chaseModeImage, freeModeImage;
        static readonly float[] DurationValues = { 180f, 300f, 0f };
        static readonly string[] DurationNames = { "3 phút", "5 phút", "Không giới hạn" };
        readonly List<Image> durationImages = new List<Image>();
        GameObject upgradesLocked;
        float messageUntil;
        CarStats maxStats;

        void Start()
        {
            catalog = CarCatalog.Load();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (GameSession.RoundSeconds < 0f) GameSession.RoundSeconds = DurationValues[0];
            SetupScene();
            BuildUI();

            if (catalog == null || catalog.cars.Count == 0)
            {
                ShowMessage("Chưa có danh sách xe. Dùng Tools > Police Chase > Tạo danh sách xe tự động", 999f);
                return;
            }
            ComputeMaxStats();
            index = catalog.IndexOf(SaveSystem.Data.selectedCar);
            ShowCar();
            SaveSystem.Changed += Refresh;
        }

        void OnDestroy() => SaveSystem.Changed -= Refresh;

        void Update()
        {
            if (turntable != null) turntable.Rotate(0f, turntableSpeed * Time.deltaTime, 0f);
            if (messageText != null && Time.time > messageUntil) messageText.text = "";

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && catalog != null && catalog.cars.Count > 0)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Step(-1);
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Step(1);
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
                if (SaveSystem.TryBuy(car))
                {
                    SaveSystem.Select(car.id);
                    ShowMessage($"Đã mua {car.displayName}!", 2.5f);
                }
                else ShowMessage($"Chưa đủ xu – cần thêm {car.price - SaveSystem.Coins:N0} xu", 2.5f);
            }
            else if (SaveSystem.Data.selectedCar != car.id)
            {
                SaveSystem.Select(car.id);
                ShowMessage($"Đã chọn {car.displayName}", 2f);
            }
            Refresh();
        }

        void OnUpgrade(UpgradeType type)
        {
            var car = Current;
            if (car == null || !SaveSystem.Owns(car.id)) return;
            int level = SaveSystem.GetUpgrades(car.id).Get(type);
            if (level >= UpgradeRules.MaxLevel) { ShowMessage("Đã nâng cấp tối đa", 2f); return; }
            int cost = UpgradeRules.Cost(car, type, level);
            if (SaveSystem.TryUpgrade(car, type)) ShowMessage($"{UpgradeRules.Name(type)} lên cấp {level + 1}!", 2f);
            else ShowMessage($"Chưa đủ xu – cần {cost:N0} xu", 2f);
            Refresh();
        }

        void SetMode(GameMode mode)
        {
            GameSession.Mode = mode;
            Refresh();
        }

        void SetDuration(float seconds)
        {
            GameSession.RoundSeconds = seconds;
            Refresh();
        }

        void OnPlay()
        {
            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                ShowMessage($"Chưa thêm scene '{gameSceneName}' vào File > Build Profiles > Scene List", 5f);
                return;
            }
            SceneManager.LoadScene(gameSceneName);
        }

        void ShowMessage(string msg, float seconds)
        {
            if (messageText == null) return;
            messageText.text = msg;
            messageUntil = Time.time + seconds;
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
            coinsText.text = $"Xu: {SaveSystem.Coins:N0}";
            bool chase = GameSession.Mode == GameMode.Chase;
            chaseModeImage.color = chase ? Blue : Gray;
            freeModeImage.color = chase ? Gray : Blue;
            for (int i = 0; i < durationImages.Count; i++)
                durationImages[i].color = Mathf.Approximately(GameSession.RoundSeconds, DurationValues[i]) ? Blue : Gray;
            modeDesc.text = chase
                ? "Bị truy nã! Trốn cảnh sát, nhặt xu. Bị truy nã càng lâu, càng nhiều sao, thưởng càng lớn."
                : "Không có cảnh sát. Thoải mái dạo phố, nhặt xu và chạy thử xe mới.";

            var car = Current;
            if (car == null) return;
            bool owned = SaveSystem.Owns(car.id);
            bool selected = SaveSystem.Data.selectedCar == car.id;
            var up = SaveSystem.GetUpgrades(car.id);
            var s = UpgradeRules.Compute(car, up);

            carNameText.text = car.displayName;
            carStatusText.text = owned ? (selected ? "Đang dùng" : "Đã sở hữu") : $"Giá: {car.price:N0} xu";
            carStatusText.color = owned ? Green : Accent;

            float[] values = { s.maxSpeed, s.acceleration, s.turnRate * s.grip, s.mass, s.nitroSeconds };
            float[] maxes = { maxStats.maxSpeed, maxStats.acceleration, maxStats.turnRate * maxStats.grip, maxStats.mass, maxStats.nitroSeconds };
            string[] labels =
            {
                $"{s.maxSpeed * 3.6f:0} km/h", $"{s.acceleration:0.0}", $"{s.turnRate:0}°/s", $"{s.mass:0} kg",
                s.nitroSeconds > 0f ? $"{s.nitroSeconds:0.0} s" : "chưa có",
            };
            for (int i = 0; i < statFills.Count; i++)
            {
                float t = maxes[i] > 0f ? Mathf.Clamp01(values[i] / maxes[i]) : 0f;
                statFills[i].rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, t), 1f);
                statValues[i].text = labels[i];
            }

            if (!owned)
            {
                actionLabel.text = $"MUA – {car.price:N0} xu";
                actionImage.color = SaveSystem.Coins >= car.price ? Green : Gray;
            }
            else if (!selected)
            {
                actionLabel.text = "CHỌN XE NÀY";
                actionImage.color = Blue;
            }
            else
            {
                actionLabel.text = "ĐANG DÙNG";
                actionImage.color = Gray;
            }

            var selectedDef = catalog.Find(SaveSystem.Data.selectedCar);
            playLabel.text = selectedDef != null ? $"CHƠI  ·  {selectedDef.displayName}" : "CHƠI";

            upgradesLocked.SetActive(!owned);
            for (int i = 0; i < UpgradeOrder.Length; i++)
            {
                int level = up.Get(UpgradeOrder[i]);
                for (int p = 0; p < upgradePips[i].Length; p++)
                    upgradePips[i][p].color = p < level ? Accent : new Color(1f, 1f, 1f, 0.15f);
                if (level >= UpgradeRules.MaxLevel)
                {
                    upgradeCostLabels[i].text = "TỐI ĐA";
                    upgradeButtonImages[i].color = Gray;
                }
                else
                {
                    int cost = UpgradeRules.Cost(car, UpgradeOrder[i], level);
                    upgradeCostLabels[i].text = $"{cost:N0} xu";
                    upgradeButtonImages[i].color = owned && SaveSystem.Coins >= cost ? Green : Gray;
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

        // ───────────────────────── Cảnh 3D xem trước ─────────────────────────

        void SetupScene()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            // Nhìn hơi thấp hơn xe để xe hiện ở khoảng giữa-trên màn hình (phần dưới có bảng thông tin).
            cam.transform.position = new Vector3(0f, 3f, -10f);
            cam.transform.LookAt(new Vector3(0f, -0.8f, 0f));
            cam.fieldOfView = 40f;

            if (FindFirstObjectByType<Light>() == null)
            {
                var l = new GameObject("Directional Light").AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.3f;
                l.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            }

            var root = new GameObject("CarPreview").transform;
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(root, false);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Platform";
            Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(turntable, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(7f, 0.05f, 7f);
            floor.GetComponent<MeshRenderer>().material.color = new Color(0.22f, 0.24f, 0.3f);
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

            // Nằm ngang → xoay cho đầu xe theo trục Z; co giãn dài ~4.5m; đặt lên bệ.
            if (TryBounds(go, out Bounds b))
            {
                if (b.size.x > b.size.z * 1.2f) { go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); TryBounds(go, out b); }
                float s = 4.5f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
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
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // ── Thanh trên cùng ──
            var top = Panel(root, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -90f), Vector2.zero, PanelColor);
            Label(top, gameTitle, 40, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(40f, 0f), Vector2.zero);
            coinsText = Label(top, "Xu: 0", 38, Accent, TextAnchor.MiddleRight, new Vector2(0.5f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-40f, 0f));

            // ── Bên trái: nâng cấp ──
            var left = Panel(root, "Upgrades", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(30f, 180f), new Vector2(560f, -120f), PanelColor);
            Label(left, "NÂNG CẤP", 32, Accent, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -60f), new Vector2(-24f, -10f));
            for (int i = 0; i < UpgradeOrder.Length; i++)
            {
                var type = UpgradeOrder[i];
                float y = -80f - i * 150f;
                var row = Panel(left, "Row_" + type, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, y - 140f), new Vector2(-16f, y), new Color(1f, 1f, 1f, 0.06f));
                Label(row, UpgradeRules.Name(type), 26, Color.white, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 10f), new Vector2(-16f, -10f));
                Label(row, UpgradeRules.Description(type), 17, new Color(1f, 1f, 1f, 0.7f), TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 10f), new Vector2(-16f, -46f));

                var pips = new Image[UpgradeRules.MaxLevel];
                for (int p = 0; p < pips.Length; p++)
                    pips[p] = Panel(row, "Pip" + p, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f + p * 36f, 18f), new Vector2(16f + p * 36f + 28f, 40f), Color.white).GetComponent<Image>();
                upgradePips.Add(pips);

                var t = type;
                var btn = MakeButton(row, "", 22, Green, () => OnUpgrade(t), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-200f, 12f), new Vector2(-12f, 62f), out Text costLabel);
                upgradeCostLabels.Add(costLabel);
                upgradeButtonImages.Add(btn.GetComponent<Image>());
            }
            var lockGo = Panel(left, "Locked", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -70f), new Color(0f, 0f, 0f, 0.6f));
            Label(lockGo, "Mua xe này để nâng cấp", 28, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            upgradesLocked = lockGo.gameObject;

            // ── Bên phải: chế độ chơi + mua/chọn + chơi ──
            var right = Panel(root, "Modes", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-560f, 180f), new Vector2(-30f, -120f), PanelColor);
            Label(right, "CHẾ ĐỘ CHƠI", 32, Accent, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -60f), new Vector2(-24f, -10f));
            var chaseBtn = MakeButton(right, "TRỐN CẢNH SÁT", 26, Blue, () => SetMode(GameMode.Chase), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -150f), new Vector2(-24f, -80f), out _);
            var freeBtn = MakeButton(right, "LÁI TỰ DO", 26, Gray, () => SetMode(GameMode.FreeRoam), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -235f), new Vector2(-24f, -165f), out _);
            chaseModeImage = chaseBtn.GetComponent<Image>();
            freeModeImage = freeBtn.GetComponent<Image>();
            modeDesc = Label(right, "", 20, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -340f), new Vector2(-24f, -248f));

            Label(right, "THỜI GIAN MỖI VÁN", 24, Accent, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -385f), new Vector2(-24f, -350f));
            for (int i = 0; i < DurationValues.Length; i++)
            {
                float v = DurationValues[i];
                float a0 = i / 3f, a1 = (i + 1) / 3f;
                var b = MakeButton(right, DurationNames[i], i == 2 ? 18 : 22, Gray, () => SetDuration(v),
                                   new Vector2(a0, 1f), new Vector2(a1, 1f),
                                   new Vector2(i == 0 ? 24f : 6f, -455f), new Vector2(i == 2 ? -24f : -6f, -395f), out _);
                durationImages.Add(b.GetComponent<Image>());
            }
            actionButton = MakeButton(right, "", 28, Green, OnAction, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 140f), new Vector2(-24f, 215f), out actionLabel);
            actionImage = actionButton.GetComponent<Image>();
            MakeButton(right, "CHƠI", 36, Accent, OnPlay, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 24f), new Vector2(-24f, 120f), out playLabel);
            playLabel.color = new Color(0.15f, 0.1f, 0f);

            // ── Dưới giữa: tên xe + chỉ số ──
            var info = Panel(root, "CarInfo", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-380f, 20f), new Vector2(380f, 380f), PanelColor);
            carNameText = Label(info, "", 44, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(90f, -70f), new Vector2(-90f, -10f));
            carStatusText = Label(info, "", 24, Accent, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(90f, -105f), new Vector2(-90f, -70f));
            MakeButton(info, "<", 48, Gray, () => Step(-1), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -100f), new Vector2(84f, -14f), out _);
            MakeButton(info, ">", 48, Gray, () => Step(1), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-84f, -100f), new Vector2(-14f, -14f), out _);

            string[] statNames = { "Tốc độ", "Tăng tốc", "Lái", "Độ bền", "Nitro" };
            for (int i = 0; i < statNames.Length; i++)
            {
                float y = -125f - i * 46f;
                Label(info, statNames[i], 22, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, y - 34f), new Vector2(160f, y));
                var bg = Panel(info, "StatBg" + i, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(165f, y - 26f), new Vector2(-150f, y - 8f), new Color(1f, 1f, 1f, 0.12f));
                var fill = Panel(bg, "Fill", new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, Accent).GetComponent<Image>();
                statFills.Add(fill);
                statValues.Add(Label(info, "", 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-145f, y - 34f), new Vector2(-24f, y)));
            }

            // ── Thông báo ──
            messageText = Label(root, "", 30, Accent, TextAnchor.MiddleCenter, new Vector2(0.25f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, -170f), new Vector2(0f, -110f));
            Label(root, "← → hoặc A D: đổi xe", 18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-300f, 385f), new Vector2(300f, 410f));
        }

        // ───────────────────────── Tiện ích UI ─────────────────────────

        RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        Text Label(Transform parent, string text, int size, Color color, TextAnchor align,
                   Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Shadow));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            go.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            return t;
        }

        Button MakeButton(Transform parent, string text, int size, Color color, UnityEngine.Events.UnityAction onClick,
                          Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, out Text label)
        {
            var rt = Panel(parent, "Button", anchorMin, anchorMax, offsetMin, offsetMax, color);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            label = Label(rt, text, size, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return btn;
        }

        // ───────────────────────── Thử nghiệm ─────────────────────────

        [ContextMenu("Thử nghiệm: +1000 xu")]
        void AddTestCoins() => SaveSystem.AddCoins(1000);

        [ContextMenu("Thử nghiệm: xoá toàn bộ dữ liệu lưu")]
        void ResetSave() => SaveSystem.ResetAll();
    }
}
