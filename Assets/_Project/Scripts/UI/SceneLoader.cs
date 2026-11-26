using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PoliceChase.UI
{
    /// <summary>
    /// Màn hình chờ khi chuyển scene: tối dần → tên game + thanh tải + mẹo chơi → sáng dần vào scene mới.
    /// Dùng: SceneLoader.Load("CityGame", "Đang vào thành phố...");
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static string GameTitle = "CẢNH SÁT TRUY ĐUỔI";
        public static bool IsLoading => instance != null;

        static SceneLoader instance;

        static readonly string[] Tips =
        {
            "Rẽ vào hẻm để nhà che khuất tầm nhìn của cảnh sát.",
            "Giữ Shift hoặc nút tia sét để dùng nitro.",
            "Bị truy nã càng nhiều sao, thưởng sống sót càng lớn.",
            "Túi tiền xanh đáng 50 xu – đừng bỏ qua!",
            "Nâng cấp Độ bền để cảnh sát khó bắt bạn hơn.",
            "Bấm M hoặc chạm vào bản đồ nhỏ để mở bản đồ lớn.",
            "Phanh tay giúp ôm cua gắt khi bị đuổi sát.",
        };

        CanvasGroup group;
        static Texture2D cityTex;
        ProgressBar bar;

        /// <summary>Chuyển sang scene khác kèm màn hình chờ. Trả về false nếu scene chưa có trong Build Profiles.</summary>
        public static bool Load(string sceneName, string subtitle = null)
        {
            if (instance != null) return true;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' chưa có trong File > Build Profiles > Scene List.");
                return false;
            }
            Time.timeScale = 1f;
            var go = new GameObject("SceneLoader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneLoader>();
            instance.StartCoroutine(instance.Run(sceneName, subtitle));
            return true;
        }

        IEnumerator Run(string sceneName, string subtitle)
        {
            Build(subtitle);
            yield return Fade(0f, 1f, 0.25f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            float t = 0f;
            const float minShow = 0.9f; // hiển thị tối thiểu để không bị chớp
            while (op.progress < 0.9f || t < minShow)
            {
                t += Time.unscaledDeltaTime;
                bar.Set(Mathf.Min(op.progress / 0.9f, t / minShow));
                yield return null;
            }
            bar.Set(1f);
            yield return new WaitForSecondsRealtime(0.15f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;
            UIKit.EnsureEventSystem();
            yield return Fade(1f, 0f, 0.35f);
            instance = null;
            Destroy(gameObject);
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
                group.alpha = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }
        }

        void Build(string subtitle)
        {
            var canvas = UIKit.CreateCanvas("LoadingCanvas", 1000);
            canvas.transform.SetParent(transform, false);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            var bg = UIKit.NewRect(canvas.transform, "Background").gameObject.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.075f, 1f);
            UIKit.Stretch(bg.rectTransform);

            var city = UIKit.NewRect(canvas.transform, "City").gameObject.AddComponent<RawImage>();
            city.texture = cityTex != null ? cityTex : (cityTex = MenuBackdrop.CityTexture(1024, 512, 7));
            city.color = new Color(0.55f, 0.6f, 0.75f);
            city.raycastTarget = false;
            var fit = city.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 2f;

            var safe = UIKit.SafeArea(canvas.transform);

            var icon = UIKit.IconImage(safe, "car", Color.white, 190f);
            icon.sprite = GameSkin.SpeedLogo;
            UIKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(190f, 190f));
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var title = GameSkin.Title(safe, GameTitle, 96, UIKit.TextColor, TextAnchor.MiddleCenter);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1400f, 120f));
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var sub = UIKit.Label(safe, subtitle ?? "Đang tải...", 28, UIKit.TextMuted, TextAnchor.MiddleCenter, false);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1200f, 40f));
            sub.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            bar = GameSkin.StatBar(safe);
            var brt = (RectTransform)bar.transform;
            UIKit.Place(brt, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(760f, 18f));
            brt.pivot = new Vector2(0.5f, 0f);
            bar.Set(0f, true);

            var tip = UIKit.Label(safe, "Mẹo: " + Tips[Random.Range(0, Tips.Length)], 24, UIKit.TextMuted, TextAnchor.MiddleCenter, false);
            UIKit.Place(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(1400f, 40f));
            tip.rectTransform.pivot = new Vector2(0.5f, 0f);
        }
    }
}
