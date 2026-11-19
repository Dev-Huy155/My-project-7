using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PoliceChase.Vehicles;

namespace PoliceChase.Police
{
    /// <summary>
    /// Bộ chỉ huy cảnh sát + hệ thống truy nã:
    /// - Sau vài giây bị truy nã 1 sao; bị nhìn thấy lâu thì tăng sao, mỗi sao thêm xe cảnh sát.
    /// - Sinh xe cảnh sát trên đường, xa người chơi và ngoài khung hình camera.
    /// - Bộ đàm: 1 xe thấy bạn → mọi xe đều biết vị trí.
    /// - Cắt đuôi: không xe nào thấy bạn trong X giây → hết truy nã, cảnh sát rút đi.
    /// - Bị bắt: xe cảnh sát ở sát mà bạn gần như đứng yên trong vài giây → BỊ BẮT, hồi sinh chỗ khác.
    /// Gắn vào 1 GameObject rỗng trong scene.
    /// </summary>
    public class PoliceDispatcher : MonoBehaviour
    {
        [Header("Người chơi (để trống = tự tìm object có tag Player)")]
        public CarController player;
        [Tooltip("Tuỳ chọn: prefab xe cảnh sát (có CarController). Để trống = tự dựng xe khối hộp")]
        public GameObject policeCarPrefab;
        [Tooltip("Tuỳ chọn: model xe bất kỳ (vd. xe trong bộ Cartoon City) – script tự gắn AI + đèn còi. Để trống = xe khối hộp")]
        public GameObject policeCarModel;

        [Header("Mức truy nã")]
        public float startDelay = 3f;
        public int startWantedLevel = 1;
        public int maxWantedLevel = 3;
        [Tooltip("Số xe cảnh sát theo mức truy nã: [0 sao, 1 sao, 2 sao, 3 sao]")]
        public int[] policePerLevel = { 0, 2, 4, 6 };
        [Tooltip("Bị nhìn thấy cộng dồn bấy nhiêu giây thì tăng 1 sao")]
        public float escalateAfter = 30f;
        [Tooltip("Không xe nào thấy bạn trong bấy nhiêu giây = cắt đuôi thành công")]
        public float escapeTime = 10f;
        [Tooltip("Sau khi cắt đuôi bao lâu thì bị truy nã lại (0 = không) – tiện để thử nghiệm")]
        public float reheatDelay = 20f;

        [Header("Bắt giữ")]
        public float arrestDistance = 7f;
        public float arrestMaxSpeedKmh = 10f;
        public float arrestTime = 3f;
        [Tooltip("Bị bắt xong thì tự hồi sinh chỗ khác chơi tiếp. GameplayBootstrap tắt ô này để bị bắt = kết thúc ván")]
        public bool respawnOnBusted = true;

        /// <summary>Phát ra ngay khi người chơi bị bắt.</summary>
        public event System.Action PlayerBusted;

        [Header("Sinh / xoá xe cảnh sát")]
        public float spawnMinDistance = 60f;
        public float spawnMaxDistance = 220f;
        public float despawnDistance = 320f;
        public float spawnInterval = 2f;

        public int WantedLevel { get; private set; }
        public bool PlayerSeen { get; private set; }
        public float ArrestProgress => arrestProgress;
        public IReadOnlyList<PoliceAI> Police => police;

        readonly List<PoliceAI> police = new List<PoliceAI>();
        RandomSpawn playerSpawner;
        float unseenTimer, heatTimer, arrestProgress, spawnTimer, reheatTimer = -1f;
        bool seenSinceWanted, busted;
        string message;
        float messageUntil;
        [System.NonSerialized] GUIStyle bigStyle, smallStyle;

        IEnumerator Start()
        {
            if (player == null)
            {
                var go = GameObject.FindWithTag("Player");
                if (go != null) player = go.GetComponent<CarController>();
            }
            if (player == null)
            {
                Debug.LogError("[PoliceDispatcher] Không tìm thấy xe người chơi (object có tag Player + CarController).", this);
                enabled = false;
                yield break;
            }
            playerSpawner = player.GetComponent<RandomSpawn>();

            yield return new WaitForSeconds(startDelay);
            if (!busted) SetWanted(startWantedLevel, "BỊ TRUY NÃ!");
        }

        void Update()
        {
            if (player == null || busted) return;
            float dt = Time.deltaTime;
            police.RemoveAll(p => p == null);
            Vector3 playerPos = player.transform.position;

            // Ai đang thấy người chơi?
            PlayerSeen = false;
            float nearest = float.MaxValue;
            int chasing = 0;
            foreach (var p in police)
            {
                if (p.state != PoliceAI.State.Chase) continue;
                chasing++;
                if (p.CanSeeTarget) PlayerSeen = true;
                nearest = Mathf.Min(nearest, p.DistanceToTarget);
            }
            if (PlayerSeen) seenSinceWanted = true;

            // Bộ đàm: khi có xe thấy (hoặc vừa bắt đầu truy nã) → báo vị trí cho tất cả.
            if (WantedLevel > 0 && (PlayerSeen || !seenSinceWanted))
                foreach (var p in police)
                    if (p.state == PoliceAI.State.Chase) p.ReportTargetPosition(playerPos);

            // Truy nã / cắt đuôi
            if (WantedLevel > 0)
            {
                if (PlayerSeen)
                {
                    unseenTimer = 0f;
                    heatTimer += dt;
                    if (heatTimer >= escalateAfter && WantedLevel < maxWantedLevel)
                        SetWanted(WantedLevel + 1, "MỨC TRUY NÃ TĂNG!");
                }
                else if (seenSinceWanted)
                {
                    unseenTimer += dt;
                    if (unseenTimer >= escapeTime) Escape();
                }
            }
            else if (reheatTimer > 0f)
            {
                reheatTimer -= dt;
                if (reheatTimer <= 0f) SetWanted(startWantedLevel, "BỊ TRUY NÃ LẠI!");
            }

            // Sinh thêm xe nếu thiếu
            int wanted = policePerLevel != null && policePerLevel.Length > 0
                ? policePerLevel[Mathf.Clamp(WantedLevel, 0, policePerLevel.Length - 1)] : 0;
            spawnTimer -= dt;
            if (chasing < wanted && spawnTimer <= 0f)
            {
                spawnTimer = spawnInterval;
                TrySpawnPolice(playerPos);
            }

            // Xoá xe đã rút đi / ở quá xa (khi camera không nhìn thấy)
            for (int i = police.Count - 1; i >= 0; i--)
            {
                var p = police[i];
                bool visible = IsVisible(p.transform.position);
                bool gone = (p.state == PoliceAI.State.Leave && p.DistanceToTarget > 120f) || p.DistanceToTarget > despawnDistance;
                if (gone && !visible)
                {
                    Destroy(p.gameObject);
                    police.RemoveAt(i);
                }
            }

            // Bắt giữ
            if (WantedLevel > 0 && nearest < arrestDistance && player.SpeedKmh < arrestMaxSpeedKmh)
                arrestProgress += dt / arrestTime;
            else
                arrestProgress = Mathf.Max(0f, arrestProgress - dt / arrestTime);
            if (arrestProgress >= 1f) StartCoroutine(Busted());
        }

        // ───────────────────────── Trạng thái ─────────────────────────

        public void SetWanted(int level, string msg)
        {
            int old = WantedLevel;
            WantedLevel = Mathf.Clamp(level, 0, maxWantedLevel);
            heatTimer = 0f;
            unseenTimer = 0f;
            reheatTimer = -1f;
            if (old == 0 && WantedLevel > 0) seenSinceWanted = false;
            foreach (var p in police) if (p != null) p.Chase();
            ShowMessage(msg, 2.5f);
        }

        void Escape()
        {
            WantedLevel = 0;
            arrestProgress = 0f;
            foreach (var p in police) if (p != null) p.Leave();
            reheatTimer = reheatDelay;
            ShowMessage("ĐÃ CẮT ĐUÔI!", 3f);
        }

        IEnumerator Busted()
        {
            busted = true;
            arrestProgress = 0f;
            ShowMessage("BỊ BẮT!", 3f);

            var input = player.GetComponent<PlayerCarInput>();
            if (input != null) input.enabled = false;
            player.SetInput(0f, 0f, true);
            foreach (var p in police)
                if (p != null) { p.enabled = false; p.Car.SetInput(0f, 0f, true); }

            PlayerBusted?.Invoke();
            if (!respawnOnBusted) yield break; // ván kết thúc – màn kết quả do GameplayBootstrap lo

            yield return new WaitForSeconds(3f);

            foreach (var p in police) if (p != null) Destroy(p.gameObject);
            police.Clear();
            WantedLevel = 0;
            reheatTimer = -1f;
            if (playerSpawner != null) playerSpawner.SpawnRandom();
            if (input != null) input.enabled = true;
            busted = false;

            yield return new WaitForSeconds(startDelay);
            if (!busted && WantedLevel == 0) SetWanted(startWantedLevel, "BỊ TRUY NÃ!");
        }

        // ───────────────────────── Sinh xe ─────────────────────────

        void TrySpawnPolice(Vector3 playerPos)
        {
            // Lượt 1: đúng mọi điều kiện. Lượt 2: nới điều kiện (bản đồ nhỏ).
            for (int pass = 0; pass < 2; pass++)
            {
                float minD = pass == 0 ? spawnMinDistance : spawnMinDistance * 0.5f;
                for (int i = 0; i < 12; i++)
                {
                    Vector3 pos;
                    Quaternion rot;
                    if (playerSpawner != null)
                    {
                        if (!playerSpawner.TryFindRandomRoadPoint(out pos, out rot)) return;
                    }
                    else
                    {
                        Vector2 r = Random.insideUnitCircle.normalized * Random.Range(minD, spawnMaxDistance);
                        if (!NavMesh.SamplePosition(playerPos + new Vector3(r.x, 0f, r.y), out NavMeshHit h, 15f, NavMesh.AllAreas)) continue;
                        pos = h.position + Vector3.up * 1.2f;
                        rot = Quaternion.identity;
                    }

                    float d = Vector3.Distance(pos, playerPos);
                    if (d < minD || d > spawnMaxDistance) continue;
                    if (pass == 0 && IsVisible(pos)) continue;

                    Vector3 toPlayer = Vector3.ProjectOnPlane(playerPos - pos, Vector3.up);
                    if (Vector3.Dot(rot * Vector3.forward, toPlayer) < 0f) rot *= Quaternion.Euler(0f, 180f, 0f);
                    Spawn(pos, rot);
                    return;
                }
            }
        }

        void Spawn(Vector3 pos, Quaternion rot)
        {
            PoliceAI ai;
            if (policeCarPrefab != null)
            {
                var go = Instantiate(policeCarPrefab, pos, rot);
                ai = go.GetComponent<PoliceAI>();
                if (ai == null) ai = go.AddComponent<PoliceAI>();
                ai.target = player.transform;
            }
            else ai = PoliceCarFactory.Build(pos, rot, player.transform, policeCarModel);

            ai.ReportTargetPosition(player.transform.position);
            police.Add(ai);
        }

        static bool IsVisible(Vector3 worldPos)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(worldPos);
            return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        }

        // ───────────────────────── Giao diện ─────────────────────────

        void ShowMessage(string msg, float seconds)
        {
            message = msg;
            messageUntil = Time.time + seconds;
        }

        void OnGUI()
        {
            if (player == null) return;
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            }

            float w = Screen.width;

            // Ô sao truy nã (góc trên bên phải)
            const float box = 26f, gap = 6f;
            float startX = w - 20f - maxWantedLevel * (box + gap);
            for (int i = 0; i < maxWantedLevel; i++)
            {
                var r = new Rect(startX + i * (box + gap), 14f, box, box);
                DrawRect(r, new Color(0f, 0f, 0f, 0.6f));
                DrawRect(new Rect(r.x + 3f, r.y + 3f, box - 6f, box - 6f),
                         i < WantedLevel ? new Color(1f, 0.82f, 0.1f) : new Color(0.35f, 0.35f, 0.35f));
            }

            string status;
            if (busted) status = "Bị bắt";
            else if (WantedLevel == 0) status = reheatTimer > 0f ? $"An toàn ({reheatTimer:0}s nữa bị truy nã lại)" : "Không bị truy nã";
            else if (PlayerSeen) status = "CẢNH SÁT ĐANG THẤY BẠN!";
            else if (seenSinceWanted) status = $"Đang trốn... còn {Mathf.Max(0f, escapeTime - unseenTimer):0.0}s";
            else status = "Cảnh sát đang tới...";
            Shadowed(new Rect(w - 420f, 46f, 400f, 24f), status, smallStyle);
            Shadowed(new Rect(w - 420f, 68f, 400f, 24f), $"Xe cảnh sát: {police.Count}", smallStyle);

            // Thanh bắt giữ
            if (arrestProgress > 0.01f)
            {
                var bar = new Rect(w / 2f - 150f, Screen.height - 70f, 300f, 22f);
                DrawRect(bar, new Color(0f, 0f, 0f, 0.7f));
                DrawRect(new Rect(bar.x + 3f, bar.y + 3f, (bar.width - 6f) * Mathf.Clamp01(arrestProgress), bar.height - 6f), new Color(0.9f, 0.15f, 0.15f));
                var c = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
                Shadowed(new Rect(bar.x, bar.y - 24f, bar.width, 22f), "ĐANG BỊ BẮT – CHẠY ĐI!", c);
            }

            // Thông báo lớn giữa màn hình
            if (Time.time < messageUntil && !string.IsNullOrEmpty(message))
                Shadowed(new Rect(0f, Screen.height * 0.3f, w, 80f), message, bigStyle);
        }

        static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            style.normal.textColor = Color.white;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
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
