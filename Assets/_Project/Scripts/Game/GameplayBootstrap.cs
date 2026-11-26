using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using PoliceChase.Vehicles;
using PoliceChase.Police;
using PoliceChase.UI;

namespace PoliceChase.Game
{
    /// <summary>
    /// Đặt 1 cái trong scene chơi. Lo phần LUẬT CHƠI của 1 ván:
    /// - đổi xe người chơi sang xe đã chọn trong garage + áp chỉ số nâng cấp + nitro,
    /// - bật cảnh sát (Trốn cảnh sát) hoặc tắt (Lái tự do), rải xu, thưởng sống sót,
    /// - đếm ngược thời gian ván, tạm dừng, kết thúc khi hết giờ hoặc bị bắt.
    /// Giao diện (đồng hồ, xu, bảng tạm dừng, kết quả, nút cảm ứng) do GameHUD vẽ – được tạo tự động.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameplayBootstrap : MonoBehaviour
    {
        public enum EndReason { TimeUp, Busted }

        [Tooltip("Tên scene menu chính (phải có trong File > Build Profiles > Scene List)")]
        public string menuSceneName = "MainMenu";

        [Header("Thời gian ván")]
        [Tooltip("Dùng khi bấm Play thẳng scene này (không qua menu). 0 = không giới hạn")]
        public float defaultRoundSeconds = 180f;

        [Header("Thưởng sống sót (chế độ Trốn cảnh sát)")]
        public float survivalTick = 10f;
        public int survivalBonusPerStar = 3;

        [Header("Thưởng hoàn thành (trốn được tới hết giờ)")]
        public int finishBonus = 30;
        public int finishBonusPerStar = 20;

        [Header("Điện thoại")]
        [Tooltip("Hiện nút cảm ứng kể cả trên PC (để thử trong Editor). Trên điện thoại luôn hiện")]
        public bool forceTouchControls = false;

        // ───────────── Trạng thái cho giao diện đọc ─────────────
        public CarController Player { get; private set; }
        public PoliceDispatcher Dispatcher { get; private set; }
        public CarNitro Nitro { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsEnded { get; private set; }
        public EndReason Reason { get; private set; }
        public float RoundSeconds { get; private set; }
        public float Elapsed { get; private set; }
        public float TimeLeft => RoundSeconds > 0f ? Mathf.Max(0f, RoundSeconds - Elapsed) : 0f;
        public int MaxWanted { get; private set; }
        public int EndBonus { get; private set; }
        public bool NewRecord { get; private set; }
        /// <summary>Đã tới lúc hiện bảng kết quả (sau khi hiện "BỊ BẮT!" một chút).</summary>
        public bool ShowResults => IsEnded && Time.unscaledTime >= endPanelAt;
        public string ToastText { get; private set; }
        public float ToastTime { get; private set; } = -100f;

        float survivalTimer, endPanelAt;
        bool frozen;

        void Awake()
        {
            GameSession.RunCoins = 0;
            Time.timeScale = 1f;
            MobileInput.Reset();
            RoundSeconds = GameSession.RoundSeconds;
            if (RoundSeconds < 0f) RoundSeconds = defaultRoundSeconds;

            var go = GameObject.FindWithTag("Player");
            if (go != null) Player = go.GetComponent<CarController>();
            Dispatcher = FindFirstObjectByType<PoliceDispatcher>();
            if (Dispatcher != null)
            {
                Dispatcher.enabled = GameSession.Mode == GameMode.Chase;
                Dispatcher.respawnOnBusted = false;
                Dispatcher.drawHud = false;
                Dispatcher.PlayerBusted += OnBusted;
            }
            var minimap = FindFirstObjectByType<Minimap>();
            if (minimap != null) minimap.drawGui = false;

            if (Player == null)
            {
                Debug.LogError("[GameplayBootstrap] Không tìm thấy xe người chơi (tag Player + CarController).", this);
                enabled = false;
                return;
            }

            // Luôn đảm bảo xe có script điều khiển và đang bật.
            var input = Player.GetComponent<PlayerCarInput>();
            if (input == null)
            {
                Debug.LogWarning("[GameplayBootstrap] Xe người chơi thiếu PlayerCarInput – đã tự thêm.", Player);
                input = Player.gameObject.AddComponent<PlayerCarInput>();
            }
            input.enabled = true;
            Player.enabled = true;

            try { ApplySelectedCar(); }
            catch (System.Exception e) { Debug.LogException(e, this); }

            var spawner = FindFirstObjectByType<CoinSpawner>();
            if (spawner == null) spawner = new GameObject("CoinSpawner").AddComponent<CoinSpawner>();
            spawner.player = Player.transform;

            // Tên lửa tăng tốc: luôn có bình năng lượng, hiệu ứng tên lửa, âm thanh xe, điểm nạp năng lượng.
            if (Nitro == null)
            {
                Nitro = Player.GetComponent<CarNitro>();
                if (Nitro == null) Nitro = Player.gameObject.AddComponent<CarNitro>();
                Nitro.Init(3f);
            }
            if (Player.GetComponent<RocketBoostFX>() == null) Player.gameObject.AddComponent<RocketBoostFX>();
            if (Player.GetComponent<CarAudio>() == null) Player.gameObject.AddComponent<CarAudio>();
            var boostSpawner = FindFirstObjectByType<BoostSpawner>();
            if (boostSpawner == null) boostSpawner = new GameObject("BoostSpawner").AddComponent<BoostSpawner>();
            boostSpawner.player = Player.transform;

            Coin.Collected += OnCoinCollected;

            try
            {
                var hud = gameObject.AddComponent<GameHUD>();
                hud.Init(this, minimap, forceTouchControls || Application.isMobilePlatform);
            }
            catch (System.Exception e) { Debug.LogException(e, this); }
        }

        void OnDestroy()
        {
            Coin.Collected -= OnCoinCollected;
            if (Dispatcher != null) Dispatcher.PlayerBusted -= OnBusted;
            MobileInput.Reset();
            Time.timeScale = 1f;
        }

        void ApplySelectedCar()
        {
            var catalog = CarCatalog.Load();
            if (catalog == null) return;
            var carDef = catalog.Find(SaveSystem.Data.selectedCar);
            if (carDef == null) return;

            var stats = UpgradeRules.Compute(carDef, SaveSystem.GetUpgrades(carDef.id));
            if (stats.maxSpeed < 5f || stats.acceleration < 1f)
                Debug.LogWarning($"[GameplayBootstrap] Xe '{carDef.displayName}' trong CarCatalog có Max Speed / Acceleration = 0 → giữ chỉ số mặc định của xe. Hãy sửa trong Resources/CarCatalog.", this);
            else
                UpgradeRules.Apply(stats, Player);

            if (carDef.model != null)
            {
                var col = Player.GetComponent<BoxCollider>();
                var old = new List<GameObject>();
                foreach (Transform c in Player.transform)
                    if (c.name == "Visual" || c.name.StartsWith("Wheel_")) old.Add(c.gameObject);
                foreach (var g in old) DestroyImmediate(g);

                var inst = Instantiate(carDef.model);
                if (col != null) CarModelFitter.Attach(Player.transform, inst, col, Player);
                else inst.transform.SetParent(Player.transform, false);
                Player.RecalculateAnchors();
            }

            Nitro = Player.GetComponent<CarNitro>();
            if (Nitro == null) Nitro = Player.gameObject.AddComponent<CarNitro>();
            Nitro.Init(stats.nitroSeconds);

            if (Dispatcher != null) Dispatcher.arrestTime *= stats.arrestTimeMultiplier;
        }

        // ───────────────────────── Vòng lặp ─────────────────────────

        void Update()
        {
            if (IsEnded)
            {
                if (!frozen && Time.unscaledTime >= endPanelAt) { frozen = true; Time.timeScale = 0f; }
                return;
            }

            var kb = Keyboard.current;
            bool pausePressed = (kb != null && kb.escapeKey.wasPressedThisFrame) ||
                                (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (pausePressed) SetPaused(!IsPaused);
            if (IsPaused) return;

            float dt = Time.deltaTime;
            Elapsed += dt;
            if (Dispatcher != null && Dispatcher.enabled) MaxWanted = Mathf.Max(MaxWanted, Dispatcher.WantedLevel);

            if (GameSession.Mode == GameMode.Chase && Dispatcher != null && Dispatcher.WantedLevel > 0)
            {
                survivalTimer += dt;
                if (survivalTimer >= survivalTick)
                {
                    survivalTimer = 0f;
                    int bonus = survivalBonusPerStar * Dispatcher.WantedLevel;
                    AddRunCoins(bonus);
                    Toast($"+{bonus} xu sống sót");
                }
            }

            if (RoundSeconds > 0f && Elapsed >= RoundSeconds) EndRound(EndReason.TimeUp);
        }

        void OnBusted()
        {
            if (!IsEnded) EndRound(EndReason.Busted);
        }

        void EndRound(EndReason reason)
        {
            if (IsPaused) { IsPaused = false; Time.timeScale = 1f; }
            IsEnded = true;
            Reason = reason;
            MobileInput.Reset();

            var input = Player.GetComponent<PlayerCarInput>();
            if (input != null) input.enabled = false;
            Player.SetInput(0f, 0f, true);
            if (Nitro != null) Nitro.enabled = false;
            if (Dispatcher != null)
            {
                foreach (var p in Dispatcher.Police)
                    if (p != null) { p.enabled = false; p.Car.SetInput(0f, 0f, true); }
                Dispatcher.enabled = false;
            }

            EndBonus = 0;
            if (reason == EndReason.TimeUp)
            {
                EndBonus = GameSession.Mode == GameMode.Chase ? finishBonus + finishBonusPerStar * MaxWanted : finishBonus / 2;
                AddRunCoins(EndBonus);
            }

            NewRecord = GameSession.Mode == GameMode.Chase && Elapsed > SaveSystem.Data.bestSurvivalSeconds;
            if (GameSession.Mode == GameMode.Chase) SaveSystem.RecordSurvival(Elapsed);

            endPanelAt = Time.unscaledTime + (reason == EndReason.Busted ? 1.5f : 0.3f);
            frozen = false;
        }

        /// <summary>Số sao đánh giá ván (0–3).</summary>
        public int RatingStars()
        {
            if (Reason == EndReason.TimeUp) return 3;
            float f = RoundSeconds > 0f ? Elapsed / RoundSeconds : Elapsed / 180f;
            return f >= 0.66f ? 2 : (f >= 0.33f ? 1 : 0);
        }

        // ───────────────────────── Hành động (giao diện gọi) ─────────────────────────

        void OnCoinCollected(Coin coin, int value)
        {
            if (IsEnded) return;
            AddRunCoins(value);
            if (value >= 20) Toast($"+{value} xu · túi tiền!");
        }

        void AddRunCoins(int value)
        {
            GameSession.RunCoins += value;
            SaveSystem.AddCoins(value);
        }

        public void Toast(string msg)
        {
            ToastText = msg;
            ToastTime = Time.unscaledTime;
        }

        public void SetPaused(bool p)
        {
            if (IsEnded) return;
            IsPaused = p;
            Time.timeScale = p ? 0f : 1f;
            if (p) MobileInput.Reset();
        }

        public void GoToMenu()
        {
            if (!IsEnded && GameSession.Mode == GameMode.Chase) SaveSystem.RecordSurvival(Elapsed);
            if (!SceneLoader.Load(menuSceneName, "Đang về garage..."))
                Toast($"Chưa thêm scene '{menuSceneName}' vào Build Profiles");
        }

        public void Restart()
        {
            if (!IsEnded && GameSession.Mode == GameMode.Chase) SaveSystem.RecordSurvival(Elapsed);
            string scene = SceneManager.GetActiveScene().name;
            if (!SceneLoader.Load(scene, "Chơi lại..."))
                Toast($"Chưa thêm scene '{scene}' vào Build Profiles");
        }
    }
}
