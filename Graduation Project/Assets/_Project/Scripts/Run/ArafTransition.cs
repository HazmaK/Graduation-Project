using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace RetroFPS.Run
{
    /// <summary>
    /// Araf'a düşüş, Araf'tan kaçış ve gerçek ölüm ekran geçişlerini (kararma, ağır çekim, mesaj)
    /// yöneten salt sunum (Presentation) bileşeni.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArafTransition : MonoBehaviour
    {
        [Header("Zamanlama ve Ağır Çekim")]
        [SerializeField] private float fadeOutDuration = 0.6f;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fallTimeScale = 0.35f;

        [Header("Görsel ve Metin Ayarları")]
        [SerializeField] private Color arafTint = new Color(0.28f, 0.02f, 0.04f, 1f);
        [TextArea(2, 4)]
        [SerializeField] private string trueDeathMessage = "GERÇEK ÖLÜM\nYeniden başlamak için R";
        [SerializeField] private string arafEnterMessage = "ARAF";
        [SerializeField] private string escapeMessage = "KAÇIŞ";

        private Canvas _canvas;
        private Image _overlayImage;
        private Text _messageText;

        /// <summary>Ekranın kararma süresi (saniye, unscaled).</summary>
        public float FadeOutDuration => fadeOutDuration;

        /// <summary>Ekranın açılma süresi (saniye, unscaled).</summary>
        public float FadeInDuration => fadeInDuration;

        /// <summary>Araf'a düşerken uygulanacak ağır çekim zaman ölçeği.</summary>
        public float FallTimeScale => fallTimeScale;

        /// <summary>Araf'a geçişte kullanılan koyu kızıl renk tonu.</summary>
        public Color ArafTint => arafTint;

        /// <summary>Gerçek ölüm ekranında gösterilecek kalıcı mesaj.</summary>
        public string TrueDeathMessage => trueDeathMessage;

        /// <summary>Araf'a düşerken gösterilecek başlık mesajı.</summary>
        public string ArafEnterMessage => arafEnterMessage;

        /// <summary>Araf'tan kaçarken gösterilecek başlık mesajı.</summary>
        public string EscapeMessage => escapeMessage;

        private void Awake()
        {
            BuildCanvas();
            Hide();
        }

        private void OnDisable()
        {
            EndSlowMo();
        }

        private void BuildCanvas()
        {
            GameObject canvasGo = new GameObject("ArafTransition_Canvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject imageGo = new GameObject("FadeOverlay");
            imageGo.transform.SetParent(canvasGo.transform, false);
            _overlayImage = imageGo.AddComponent<Image>();
            _overlayImage.raycastTarget = false;
            _overlayImage.color = new Color(0f, 0f, 0f, 0f);

            RectTransform imgRect = _overlayImage.rectTransform;
            imgRect.anchorMin = Vector2.zero;
            imgRect.anchorMax = Vector2.one;
            imgRect.offsetMin = Vector2.zero;
            imgRect.offsetMax = Vector2.zero;

            GameObject textGo = new GameObject("MessageText");
            textGo.transform.SetParent(canvasGo.transform, false);
            _messageText = textGo.AddComponent<Text>();
            _messageText.raycastTarget = false;
            _messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _messageText.fontSize = 52;
            _messageText.alignment = TextAnchor.MiddleCenter;
            _messageText.color = new Color(1f, 1f, 1f, 0f);

            RectTransform txtRect = _messageText.rectTransform;
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Araf'a düşüş sırasında zamanı yavaşlatır (Slow-Motion).
        /// </summary>
        public void BeginSlowMo()
        {
            float scale = Mathf.Clamp(fallTimeScale, 0.05f, 1f);
            Time.timeScale = scale;
            Time.fixedDeltaTime = 0.02f * scale;
        }

        /// <summary>
        /// Zaman ölçeğini ve fizik adımını standart değerlerine (1.0 / 0.02) geri döndürür.
        /// </summary>
        public void EndSlowMo()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        /// <summary>
        /// Ekranı verilen renge doğru karartır ve mesajı görünür kılar (unscaled time).
        /// </summary>
        public async Awaitable FadeOut(Color color, string message, CancellationToken ct = default)
        {
            if (_canvas == null) BuildCanvas();

            _canvas.gameObject.SetActive(true);
            _messageText.text = message ?? string.Empty;

            float duration = Mathf.Max(0.01f, fadeOutDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                _overlayImage.color = new Color(color.r, color.g, color.b, t);
                _messageText.color = new Color(1f, 1f, 1f, t);

                await Awaitable.NextFrameAsync(ct);
            }

            _overlayImage.color = new Color(color.r, color.g, color.b, 1f);
            _messageText.color = new Color(1f, 1f, 1f, 1f);
        }

        /// <summary>
        /// Kararmış ekranı tekrar şeffaflaştırarak oyuna döner ve zaman ölçeğini normale çeker (unscaled time).
        /// </summary>
        public async Awaitable FadeIn(CancellationToken ct = default)
        {
            if (_canvas == null) BuildCanvas();

            EndSlowMo();

            Color startOverlay = _overlayImage.color;
            float duration = Mathf.Max(0.01f, fadeInDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / duration);

                _overlayImage.color = new Color(startOverlay.r, startOverlay.g, startOverlay.b, t);
                _messageText.color = new Color(1f, 1f, 1f, t);

                await Awaitable.NextFrameAsync(ct);
            }

            Hide();
        }

        /// <summary>
        /// Gerçek ölüm gibi durumlarda ekranda kalıcı arkaplan ve mesaj gösterir.
        /// </summary>
        public void ShowPersistentMessage(Color color, string message)
        {
            if (_canvas == null) BuildCanvas();

            EndSlowMo();
            _canvas.gameObject.SetActive(true);
            _overlayImage.color = new Color(color.r, color.g, color.b, 1f);
            _messageText.text = message ?? string.Empty;
            _messageText.color = Color.white;
        }

        /// <summary>
        /// Geçiş katmanını tamamen gizler ve zaman ölçeğini normale döndürür.
        /// </summary>
        public void Hide()
        {
            EndSlowMo();
            if (_overlayImage != null)
            {
                _overlayImage.color = new Color(0f, 0f, 0f, 0f);
            }
            if (_messageText != null)
            {
                _messageText.text = string.Empty;
                _messageText.color = new Color(1f, 1f, 1f, 0f);
            }
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(false);
            }
        }
    }
}
