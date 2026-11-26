using UnityEngine;
using PoliceChase.Game;

namespace PoliceChase.Vehicles
{
    /// <summary>
    /// Âm thanh xe người chơi, tự tạo bằng code (không cần file âm thanh):
    /// - tiếng động cơ: nhấn ga thì gầm to và vòng tua tăng, có cảm giác sang số khi tăng tốc,
    /// - tiếng tên lửa khi tăng tốc + tiếng "vút" lúc bắt đầu,
    /// - tiếng "ting" khi nhặt điểm năng lượng.
    /// Muốn dùng âm thanh thật: kéo file vào các ô Clip bên dưới.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarAudio : MonoBehaviour
    {
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Tooltip("Để trống = tự tạo tiếng động cơ")]
        public AudioClip engineClip;
        public AudioClip rocketLoopClip;
        public AudioClip rocketStartClip;
        public AudioClip pickupClip;
        public int gears = 5;

        CarController car;
        CarNitro nitro;
        AudioSource engine, rocket, oneShot;
        float pitch = 0.8f, vol;

        static AudioClip genEngine, genRocket, genWhoosh, genPickup;
        static AudioClip EngineClip => genEngine != null ? genEngine : (genEngine = MakeEngine());
        static AudioClip RocketClip => genRocket != null ? genRocket : (genRocket = MakeRocket());
        static AudioClip WhooshClip => genWhoosh != null ? genWhoosh : (genWhoosh = MakeWhoosh());
        static AudioClip PickupClip => genPickup != null ? genPickup : (genPickup = MakePickup());

        void Start()
        {
            car = GetComponent<CarController>();
            nitro = GetComponent<CarNitro>();

            engine = MakeSource(engineClip != null ? engineClip : EngineClip, true);
            rocket = MakeSource(rocketLoopClip != null ? rocketLoopClip : RocketClip, true);
            oneShot = MakeSource(null, false);
            engine.Play();
            rocket.volume = 0f;
            rocket.Play();

            if (nitro != null) nitro.BoostStarted += OnBoostStarted;
            BoostPickup.Collected += OnPickup;
        }

        void OnDestroy()
        {
            if (nitro != null) nitro.BoostStarted -= OnBoostStarted;
            BoostPickup.Collected -= OnPickup;
        }

        AudioSource MakeSource(AudioClip clip, bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0f;      // xe của mình: nghe rõ như nhau
            s.dopplerLevel = 0f;
            return s;
        }

        void Update()
        {
            if (engine == null) return;
            bool paused = Time.timeScale < 0.01f;
            float dt = Time.unscaledDeltaTime;

            float throttle = Mathf.Abs(car.Throttle);
            float top = Mathf.Max(5f, car.maxSpeed);
            float speed01 = Mathf.Clamp01(Mathf.Abs(car.ForwardSpeed) / top);
            bool boosting = nitro != null && nitro.Active;

            // Vòng tua theo số: mỗi số quét từ thấp lên cao, sang số thì tụt xuống.
            int g = Mathf.Clamp(Mathf.FloorToInt(speed01 * gears), 0, gears - 1);
            float local = speed01 * gears - g;
            float rpm01 = g == 0 ? local * 0.85f : 0.3f + local * 0.7f;
            if (throttle > 0.1f && speed01 < 0.08f) rpm01 = Mathf.Max(rpm01, 0.55f);      // rồ ga tại chỗ
            if (!car.IsGrounded && throttle > 0.1f) rpm01 = Mathf.Max(rpm01, 0.9f);       // bánh quay tự do
            float targetPitch = Mathf.Lerp(0.65f, 1.9f, rpm01) + g * 0.04f + (boosting ? 0.15f : 0f);
            pitch = Mathf.Lerp(pitch, targetPitch, 1f - Mathf.Exp(-(targetPitch < pitch ? 9f : 5f) * dt));

            float targetVol = (0.32f + 0.5f * throttle + (boosting ? 0.12f : 0f)) * masterVolume;
            vol = Mathf.Lerp(vol, paused ? 0f : targetVol, 1f - Mathf.Exp(-10f * dt));
            engine.pitch = pitch;
            engine.volume = vol;

            float rv = boosting && !paused ? 0.85f * masterVolume : 0f;
            rocket.volume = Mathf.MoveTowards(rocket.volume, rv, dt * (rv > rocket.volume ? 6f : 2.5f));
            rocket.pitch = 0.9f + speed01 * 0.3f;
        }

        void OnBoostStarted()
        {
            oneShot.PlayOneShot(rocketStartClip != null ? rocketStartClip : WhooshClip, 0.9f * masterVolume);
        }

        void OnPickup(BoostPickup p)
        {
            oneShot.PlayOneShot(pickupClip != null ? pickupClip : PickupClip, 0.8f * masterVolume);
        }

        // ───────────────────────── Tạo âm thanh ─────────────────────────
        const int Rate = 44100;

        /// <summary>Động cơ 4 xi-lanh: chuỗi xung nổ tắt dần, lặp liền mạch 1 giây.</summary>
        static AudioClip MakeEngine()
        {
            int n = Rate;
            var d = new float[n];
            const float fire = 40f;                         // số lần nổ / giây ở pitch = 1
            float[] cyl = { 1f, 0.82f, 0.93f, 0.76f };      // mỗi xi-lanh mạnh yếu khác nhau chút
            var rnd = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float ph = t * fire;
                int k = (int)ph;
                float f = ph - k;
                float a = cyl[k % 4];
                float pulse = Mathf.Exp(-f * 5f) * Mathf.Sin(2f * Mathf.PI * f * 3f) + 0.45f * Mathf.Exp(-f * 9f) * Mathf.Sin(2f * Mathf.PI * f * 7f);
                float rumble = 0.35f * Mathf.Sin(2f * Mathf.PI * 20f * t) + 0.2f * Mathf.Sin(2f * Mathf.PI * 80f * t);
                d[i] = a * pulse + rumble + ((float)rnd.NextDouble() * 2f - 1f) * 0.06f;
            }
            LowPassCyclic(d, 0.35f);
            Normalize(d, 0.85f);
            var clip = AudioClip.Create("EngineLoop", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Tiếng tên lửa: tiếng gầm trầm + xì, lặp liền mạch.</summary>
        static AudioClip MakeRocket()
        {
            int n = Rate * 2, fade = Rate / 5;
            var raw = new float[n + fade];
            var rnd = new System.Random(5);
            float brown = 0f, lp = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float w = (float)rnd.NextDouble() * 2f - 1f;
                brown = Mathf.Clamp(brown + w * 0.08f, -1f, 1f) * 0.995f;
                lp += (w - lp) * 0.25f;
                float hiss = w - lp;
                float flutter = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 31f * i / Rate);
                raw[i] = (brown * 1.6f + hiss * 0.35f) * flutter;
            }
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = raw[i];
            for (int i = 0; i < fade; i++)                   // trộn đuôi vào đầu để lặp không bị "tách"
            {
                float k = i / (float)fade;
                d[i] = raw[i] * k + raw[n + i] * (1f - k);
            }
            Normalize(d, 0.8f);
            var clip = AudioClip.Create("RocketLoop", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Tiếng "vút – bùm" khi bật tên lửa.</summary>
        static AudioClip MakeWhoosh()
        {
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            var rnd = new System.Random(9);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 4f);
                float cutoff = Mathf.Lerp(0.05f, 0.6f, Mathf.Clamp01(t / 0.15f)) * Mathf.Exp(-t * 2f) + 0.03f;
                lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * cutoff;
                float boom = Mathf.Sin(2f * Mathf.PI * (70f - 30f * t) * t) * Mathf.Exp(-t * 7f);
                d[i] = lp * env * 1.4f + boom * 0.8f;
            }
            Normalize(d, 0.9f);
            var clip = AudioClip.Create("RocketStart", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Tiếng "ting-ting-ting" đi lên khi nhặt năng lượng.</summary>
        static AudioClip MakePickup()
        {
            int n = (int)(Rate * 0.55f);
            var d = new float[n];
            float[] notes = { 880f, 1174.7f, 1568f, 2093f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, v = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float st = t - k * 0.07f;
                    if (st < 0f) continue;
                    float env = Mathf.Clamp01(st / 0.005f) * Mathf.Exp(-st * 9f);
                    v += env * (Mathf.Sin(2f * Mathf.PI * notes[k] * st) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[k] * st));
                }
                d[i] = v;
            }
            Normalize(d, 0.8f);
            var clip = AudioClip.Create("BoostPickup", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static void LowPassCyclic(float[] d, float k)
        {
            float s = 0f;
            for (int pass = 0; pass < 2; pass++)       // chạy 2 vòng để điểm đầu/cuối khớp nhau khi lặp
                for (int i = 0; i < d.Length; i++)
                {
                    s += (d[i] - s) * k;
                    if (pass == 1) d[i] = s;
                }
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0.0001f;
            foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
            float g = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
        }
    }
}
