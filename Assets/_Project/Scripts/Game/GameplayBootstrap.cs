using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using PoliceChase.Vehicles;
using PoliceChase.Police;

namespace PoliceChase.Game
{
    /// <summary>
    /// Đặt 1 cái trong scene chơi. Khi vào game:
    /// - đổi xe người chơi sang xe đã chọn trong garage + áp chỉ số nâng cấp + nitro,
    /// - bật cảnh sát (Trốn cảnh sát) hoặc tắt (Lái tự do), rải xu, thưởng sống sót,
    /// - ĐẾM NGƯỢC thời gian ván (chọn ở menu: 3 phút / 5 phút / không giới hạn),
    /// - nút Tạm dừng trên màn hình (hoặc Esc): Tiếp tục / Chơi lại / Về menu,
    /// - kết thúc ván khi HẾT GIỜ hoặc BỊ BẮT → màn kết quả: Chơi lại / Về menu.
    /// Xu được lưu ngay khi nhặt, bị bắt vẫn giữ nguyên.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameplayBootstrap : MonoBehaviour
    {
        enum State { Playing, Paused, Ended }
        enum EndReason { TimeUp, Busted }

        [Tooltip("Tên scene menu chính (phải có trong File > Build Profiles > Scene List)")]
        public string menuSceneName = "MainMenu";

        [Header("Thời gian ván")]
        [Tooltip("Dùng khi bấm Play thẳng scene này (không qua menu). 0 = không giới hạn")]
        public float defaultRoundSeconds = 180f;

        [Header("Thưởng sống sót (chế độ Trốn cảnh sát)")]
        [Tooltip("Mỗi bao nhiêu giây bị truy nã thì được thưởng")]
        public float survivalTick = 10f;
        [Tooltip("Số xu thưởng mỗi lần, nhân với số sao truy nã")]
        public int survivalBonusPerStar = 3;

        [Header("Thưởng hoàn thành (trốn được tới hết giờ)")]
        public int finishBonus = 30;
        [Tooltip("Thưởng thêm theo số sao cao nhất đạt được trong ván")]
        public int finishBonusPerStar = 20;

        CarController player;
        PoliceDispatcher dispatcher;
        CarNitro nitro;
        CarDef carDef;

        State state = State.Playing;
        EndReason endReason;
        float roundSeconds, elapsed, survivalTimer;
        int maxWanted, endBonus;
        bool newRecord, frozen;
        float endPanelAt;

        string toast;
        float toastUntil;
        [System.NonSerialized] GUIStyle bigStyle, midStyle, smallStyle, buttonStyle, timerStyle;

        void Awake()
        {
            GameSession.RunCoins = 0;
            Time.timeScale = 1f;
            roundSeconds = GameSession.RoundSeconds;
            if (roundSeconds < 0f) roundSeconds = defaultRoundSeconds;

            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.GetComponent<CarController>();
            dispatcher = FindFirstObjectByType<PoliceDispatcher>();
            if (dispatcher != null)
            {
                dispatcher.enabled = GameSession.Mode == GameMode.Chase;
                dispatcher.respawnOnBusted = false;
                dispatcher.PlayerBusted += OnBusted;
            }

            if (player == null)
            {
                Debug.LogError("[GameplayBootstrap] Không tìm thấy xe người chơi (tag Player + CarController).", this);
                enabled = false;
                return;
            }

            ApplySelectedCar();

            var spawner = FindFirstObjectByType<CoinSpawner>();
            if (spawner == null) spawner = new GameObject("CoinSpawner").AddComponent<CoinSpawner>();
            spawner.player = player.transform;

            Coin.Collected += OnCoinCollected;
        }

        void OnDestroy()
        {
            Coin.Collected -= OnCoinCollected;
            if (dispatcher != null) dispatcher.PlayerBusted -= OnBusted;
            Time.timeScale = 1f;
        }

        void ApplySelectedCar()
        {
            var catalog = CarCatalog.Load();
            if (catalog == null) return;
            carDef = catalog.Find(SaveSystem.Data.selectedCar);
            if (carDef == null) return;

            var stats = UpgradeRules.Compute(carDef, SaveSystem.GetUpgrades(carDef.id));
            UpgradeRules.Apply(stats, player);

            if (carDef.model != null)
            {
                var col = player.GetComponent<BoxCollider>();
                var old = new List<GameObject>();
                foreach (Transform c in player.transform)
                    if (c.name == "Visual" || c.name.StartsWith("Wheel_")) old.Add(c.gameObject);
                foreach (var g in old) DestroyImmediate(g);

                var inst = Instantiate(carDef.model);
                if (col != null) CarModelFitter.Attach(player.transform, inst, col, player);
                else inst.transform.SetParent(player.transform, false);
                player.RecalculateAnchors();
            }

            nitro = player.GetComponent<CarNitro>();
            if (nitro == null) nitro = player.gameObject.AddComponent<CarNitro>();
            nitro.Init(stats.nitroSeconds);

            if (dispatcher != null) dispatcher.arrestTime *= stats.arrestTimeMultiplier;
        }

        // ───────────────────────── Vòng lặp ─────────────────────────

        void Update()
        {
            if (state == State.Ended)
            {
                // Đợi một chút cho người chơi thấy "BỊ BẮT!" rồi mới đứng hình + hiện kết quả.
                if (!frozen && Time.unscaledTime >= endPanelAt) { frozen = true; Time.timeScale = 0f; }
                return;
            }

            var kb = Keyboard.current;
            bool pausePressed = (kb != null && kb.escapeKey.wasPressedThisFrame) ||
                                (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (pausePressed) SetPaused(state != State.Paused);
            if (state == State.Paused) return;

            float dt = Time.deltaTime;
            elapsed += dt;
            if (dispatcher != null && dispatcher.enabled) maxWanted = Mathf.Max(maxWanted, dispatcher.WantedLevel);

            if (GameSession.Mode == GameMode.Chase && dispatcher != null && dispatcher.WantedLevel > 0)
            {
                survivalTimer += dt;
                if (survivalTimer >= survivalTick)
                {
                    survivalTimer = 0f;
                    int bonus = survivalBonusPerStar * dispatcher.WantedLevel;
                    AddRunCoins(bonus);
                    Toast($"+{bonus} xu sống sót ({dispatcher.WantedLevel} sao)");
                }
            }

            if (roundSeconds > 0f && elapsed >= roundSeconds) EndRound(EndReason.TimeUp);
        }

        void OnBusted()
        {
            if (state != State.Ended) EndRound(EndReason.Busted);
        }

        void EndRound(EndReason reason)
        {
            if (state == State.Paused) Time.timeScale = 1f;
            state = State.Ended;
            endReason = reason;

            // Dừng xe + cảnh sát
            var input = player.GetComponent<PlayerCarInput>();
            if (input != null) input.enabled = false;
            player.SetInput(0f, 0f, true);
            if (nitro != null) nitro.enabled = false;
            if (dispatcher != null)
            {
                foreach (var p in dispatcher.Police)
                    if (p != null) { p.enabled = false; p.Car.SetInput(0f, 0f, true); }
                dispatcher.enabled = false;
            }

            // Thưởng khi trốn được tới hết giờ
            endBonus = 0;
            if (reason == EndReason.TimeUp)
            {
                endBonus = GameSession.Mode == GameMode.Chase ? finishBonus + finishBonusPerStar * maxWanted : finishBonus / 2;
                AddRunCoins(endBonus);
            }

            newRecord = GameSession.Mode == GameMode.Chase && elapsed > SaveSystem.Data.bestSurvivalSeconds;
            if (GameSession.Mode == GameMode.Chase) SaveSystem.RecordSurvival(elapsed);

            endPanelAt = Time.unscaledTime + (reason == EndReason.Busted ? 1.5f : 0.3f);
            frozen = false;
        }

        // ───────────────────────── Hành động ─────────────────────────

        void OnCoinCollected(Coin coin, int value)
        {
            if (state == State.Ended) return;
            AddRunCoins(value);
            if (value >= 20) Toast($"+{value} xu – túi tiền!");
        }

        void AddRunCoins(int value)
        {
            GameSession.RunCoins += value;
            SaveSystem.AddCoins(value);
        }

        void Toast(string msg)
        {
            toast = msg;
            toastUntil = Time.unscaledTime + 2f;
        }

        void SetPaused(bool p)
        {
            if (state == State.Ended) return;
            state = p ? State.Paused : State.Playing;
            Time.timeScale = p ? 0f : 1f;
        }

        void GoToMenu()
        {
            if (state != State.Ended && GameSession.Mode == GameMode.Chase) SaveSystem.RecordSurvival(elapsed);
            if (!Application.CanStreamedLevelBeLoaded(menuSceneName))
            {
                Toast($"Chưa thêm scene '{menuSceneName}' vào File > Build Profiles > Scene List");
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }

        void Restart()
        {
            if (state != State.Ended && GameSession.Mode == GameMode.Chase) SaveSystem.RecordSurvival(elapsed);
            string scene = SceneManager.GetActiveScene().name;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Toast($"Chưa thêm scene '{scene}' vào File > Build Profiles > Scene List");
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        // ───────────────────────── Giao diện ─────────────────────────

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        void OnGUI()
        {
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                midStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                timerStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            }
            float w = Screen.width, h = Screen.height;

            // ── Xu + đồng hồ (giữa phía trên) ──
            var box = new Rect(w / 2f - 170f, 10f, 340f, 84f);
            DrawRect(box, new Color(0f, 0f, 0f, 0.55f));
            if (roundSeconds > 0f)
            {
                float left = roundSeconds - elapsed;
                bool hurry = left <= 30f && state != State.Ended;
                Color c = hurry && Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f ? new Color(1f, 0.3f, 0.3f) : Color.white;
                Shadowed(new Rect(box.x, box.y + 2f, box.width, 34f), "Còn " + Clock(left), timerStyle, c);
            }
            else Shadowed(new Rect(box.x, box.y + 2f, box.width, 34f), "Đã chơi " + Clock(elapsed), timerStyle, Color.white);
            Shadowed(new Rect(box.x, box.y + 36f, box.width, 26f), $"Xu: {SaveSystem.Coins:N0}", midStyle, new Color(1f, 0.85f, 0.2f));
            Shadowed(new Rect(box.x, box.y + 62f, box.width, 18f),
                     (GameSession.Mode == GameMode.Chase ? "Trốn cảnh sát" : "Lái tự do") + $"   ·   ván này +{GameSession.RunCoins}",
                     smallStyle, Color.white);

            if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toast) && state != State.Ended)
                Shadowed(new Rect(0f, 100f, w, 26f), toast, midStyle, new Color(1f, 0.9f, 0.3f));

            // ── Nút tạm dừng (góc trên bên trái) ──
            if (state == State.Playing && GUI.Button(new Rect(16f, 16f, 170f, 46f), "II  Tạm dừng", buttonStyle)) SetPaused(true);

            // ── Thanh nitro (góc dưới bên phải) ──
            if (nitro != null && nitro.capacitySeconds > 0f && state != State.Ended)
            {
                var bar = new Rect(w - 280f, h - 60f, 250f, 22f);
                DrawRect(bar, new Color(0f, 0f, 0f, 0.65f));
                DrawRect(new Rect(bar.x + 3f, bar.y + 3f, (bar.width - 6f) * nitro.Charge01, bar.height - 6f),
                         nitro.Active ? new Color(1f, 0.5f, 0.1f) : new Color(0.2f, 0.8f, 1f));
                Shadowed(new Rect(bar.x, bar.y - 22f, bar.width, 20f), "NITRO (giữ Shift)", smallStyle, Color.white);
            }

            if (state == State.Ended && endReason == EndReason.Busted && Time.unscaledTime < endPanelAt)
                Shadowed(new Rect(0f, h * 0.3f, w, 70f), "BỊ BẮT!", bigStyle, new Color(1f, 0.3f, 0.3f));
            if (state == State.Paused) DrawPause(w, h);
            if (state == State.Ended && Time.unscaledTime >= endPanelAt) DrawResults(w, h);
        }

        void DrawPause(float w, float h)
        {
            DrawRect(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.6f));
            Shadowed(new Rect(0f, h * 0.2f, w, 60f), "TẠM DỪNG", bigStyle, Color.white);
            string time = roundSeconds > 0f ? "Còn " + Clock(roundSeconds - elapsed) : "Đã chơi " + Clock(elapsed);
            Shadowed(new Rect(0f, h * 0.2f + 60f, w, 30f), $"{time}   ·   ván này +{GameSession.RunCoins} xu", midStyle, new Color(1f, 0.85f, 0.2f));

            float bw = 300f, bh = 54f, x = w / 2f - bw / 2f, y = h * 0.42f;
            if (GUI.Button(new Rect(x, y, bw, bh), "Tiếp tục", buttonStyle)) SetPaused(false);
            if (GUI.Button(new Rect(x, y + 68f, bw, bh), "Chơi lại", buttonStyle)) Restart();
            if (GUI.Button(new Rect(x, y + 136f, bw, bh), "Về menu", buttonStyle)) GoToMenu();
        }

        void DrawResults(float w, float h)
        {
            DrawRect(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.7f));
            var panel = new Rect(w / 2f - 280f, h * 0.12f, 560f, 470f);
            DrawRect(panel, new Color(0.08f, 0.09f, 0.13f, 0.95f));

            string title;
            Color titleColor;
            if (endReason == EndReason.Busted) { title = "BỊ BẮT!"; titleColor = new Color(1f, 0.3f, 0.3f); }
            else if (GameSession.Mode == GameMode.Chase) { title = "HẾT GIỜ – BẠN ĐÃ THOÁT!"; titleColor = new Color(0.4f, 1f, 0.5f); }
            else { title = "HẾT GIỜ"; titleColor = Color.white; }
            Shadowed(new Rect(panel.x, panel.y + 20f, panel.width, 60f), title, bigStyle, titleColor);

            float y = panel.y + 100f;
            Line(panel, ref y, "Thời gian", Clock(elapsed));
            if (GameSession.Mode == GameMode.Chase) Line(panel, ref y, "Mức truy nã cao nhất", maxWanted + " sao");
            Line(panel, ref y, "Xu nhặt được", "+" + (GameSession.RunCoins - endBonus));
            if (endBonus > 0) Line(panel, ref y, "Thưởng hoàn thành", "+" + endBonus);
            Line(panel, ref y, "Tổng xu hiện có", SaveSystem.Coins.ToString("N0"));
            if (GameSession.Mode == GameMode.Chase)
                Line(panel, ref y, newRecord ? "KỶ LỤC MỚI!" : "Kỷ lục sống sót", Clock(SaveSystem.Data.bestSurvivalSeconds));
            if (endReason == EndReason.Busted)
                Shadowed(new Rect(panel.x, y + 4f, panel.width, 24f), "Bạn vẫn giữ toàn bộ xu đã nhặt", smallStyle, new Color(1f, 1f, 1f, 0.8f));

            float bw = 230f, bh = 56f, by = panel.yMax - bh - 24f;
            if (GUI.Button(new Rect(panel.x + 30f, by, bw, bh), "Chơi lại", buttonStyle)) Restart();
            if (GUI.Button(new Rect(panel.xMax - 30f - bw, by, bw, bh), "Về menu", buttonStyle)) GoToMenu();
        }

        void Line(Rect panel, ref float y, string label, string value)
        {
            var l = new GUIStyle(midStyle) { alignment = TextAnchor.MiddleLeft };
            var r = new GUIStyle(midStyle) { alignment = TextAnchor.MiddleRight };
            Shadowed(new Rect(panel.x + 40f, y, panel.width - 80f, 30f), label, l, Color.white);
            Shadowed(new Rect(panel.x + 40f, y, panel.width - 80f, 30f), value, r, new Color(1f, 0.85f, 0.2f));
            y += 38f;
        }

        static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        static void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
