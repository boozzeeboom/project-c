using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectC.Ship;
using ProjectC.Player;

namespace ProjectC.UI
{
    /// <summary>
    /// HUD для отображения высоты корабля и статуса коридора.
    /// Создаёт весь UI программно при старте — не требует настройки в Inspector.
    /// Просто добавьте скрипт на любой GameObject в сцене.
    ///
    /// Статусы:
    /// Safe — зелёный (0.2, 0.8, 0.2)
    /// Warning — жёлтый (1, 0.85, 0)
    /// Danger — красный (1, 0.2, 0.2)
    /// </summary>
    public class AltitudeUI : MonoBehaviour
    {
        [Header("Цвета Статусов")]
        [SerializeField] private Color safeColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color warningColor = new Color(1f, 0.85f, 0f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.2f, 0.2f);

        [Header("Настройки Обновления")]
        [Tooltip("Как часто обновлять UI (сек)")]
        [SerializeField] private float updateInterval = 0.2f;

        // Внутренние ссылки на UI элементы
        private Canvas _canvas;
        private RectTransform _panelRect;
        private TextMeshProUGUI _statusIconText;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _altitudeText;
        private TextMeshProUGUI _corridorText;
        private Image _background;

        private float _updateTimer;
        private ShipController _shipController;

        private void Awake()
        {
            SetupUI();
        }

        private void Start()
        {
            // Ищем ShipController на сцене
            _shipController = FindAnyObjectByType<ShipController>();
            if (_shipController == null)
            {
                Debug.LogWarning("[AltitudeUI] ShipController not found on scene. HUD will show placeholder data.");
            }
        }

        private void Update()
        {
            _updateTimer += Time.deltaTime;
            if (_updateTimer < updateInterval) return;
            _updateTimer = 0f;

            UpdateUI();
        }

        /// <summary>
        /// Создать всю иерархию UI программно.
        /// </summary>
        private void SetupUI()
        {
            // 1. Найти или создать Canvas
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                _canvas = FindAnyObjectByType<Canvas>();
            }

            if (_canvas == null)
            {
                GameObject canvasGo = new GameObject("HUD_Canvas");
                _canvas = canvasGo.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Убедиться что Canvas в Screen Space Overlay
            if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // 2. Создать панель HUD
            GameObject panelGo = new GameObject("AltitudeHUD_Panel");
            panelGo.transform.SetParent(_canvas.transform, false);
            _panelRect = panelGo.GetComponent<RectTransform>();
            if (_panelRect == null)
            {
                _panelRect = panelGo.AddComponent<RectTransform>();
            }

            // Позиция: верхний центр
            _panelRect.anchorMin = new Vector2(0.5f, 1f);
            _panelRect.anchorMax = new Vector2(0.5f, 1f);
            _panelRect.pivot = new Vector2(0.5f, 1f);
            _panelRect.anchoredPosition = new Vector2(0f, -20f);
            _panelRect.sizeDelta = new Vector2(400f, 120f);

            // 3. Фон панели
            _background = panelGo.AddComponent<Image>();
            _background.color = new Color(0f, 0f, 0f, 0.5f);

            // Скруглённые углы (если есть компонент)
            // Примечание: Requires additional component, skip for simplicity

            // 4. Создать текстовые элементы
            float yPos = -10f;
            float lineHeight = 28f;

            // Status Icon
            _statusIconText = CreateTextElement(panelGo.transform, "StatusIcon", fontSize: 22, yPos: yPos);
            yPos -= lineHeight;

            // Status Text
            _statusText = CreateTextElement(panelGo.transform, "StatusText", fontSize: 18, yPos: yPos);
            yPos -= lineHeight + 5f;

            // Altitude Text
            _altitudeText = CreateTextElement(panelGo.transform, "AltitudeText", fontSize: 16, yPos: yPos);
            yPos -= lineHeight;

            // Corridor Text
            _corridorText = CreateTextElement(panelGo.transform, "CorridorText", fontSize: 14, yPos: yPos);

            Debug.Log("[AltitudeUI] UI created programmatically at top-center of screen.");
        }

        /// <summary>
        /// Создать TextMeshProUGUI элемент.
        /// </summary>
        private TextMeshProUGUI CreateTextElement(Transform parent, string name, float fontSize, float yPos)
        {
            GameObject textGo = new GameObject(name);
            textGo.transform.SetParent(parent, false);

            var rect = textGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(-20f, fontSize + 4f);

            var tmpText = textGo.AddComponent<TextMeshProUGUI>();
            tmpText.fontSize = fontSize;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;
            tmpText.font = GetDefaultTMPFont();
            tmpText.textWrappingMode = TextWrappingModes.NoWrap;
            tmpText.overflowMode = TextOverflowModes.Overflow;

            return tmpText;
        }

        /// <summary>
        /// Получить дефолтный шрифт TMP.
        /// </summary>
        private TMP_FontAsset GetDefaultTMPFont()
        {
            // Пробуем найти стандартный шрифт TMP
            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
                return defaultFont;

            // Ищем в ресурсах
            defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (defaultFont != null)
                return defaultFont;

            // Ищем любой TMP шрифт
            defaultFont = FindAnyObjectByType<TMP_FontAsset>();
            if (defaultFont != null)
            {
                TMP_Settings.defaultFontAsset = defaultFont;
                return defaultFont;
            }

            Debug.LogWarning("[AltitudeUI] No TMP_FontAsset found! Text may not render correctly. Import TextMeshPro Essentials via Window > TextMeshPro > Import TMP Essentials.");
            return null;
        }

        /// <summary>
        /// Обновить UI данными из ShipController и AltitudeCorridorSystem.
        /// </summary>
        private void UpdateUI()
        {
            // Определяем позицию и коридор
            Vector3 shipPosition = Vector3.zero;
            AltitudeCorridorData corridor = null;
            AltitudeStatus status = AltitudeStatus.Safe;
            float altitude = 0f;

            if (_shipController != null && _shipController.gameObject.activeInHierarchy)
            {
                shipPosition = _shipController.transform.position;
                altitude = shipPosition.y;
            }

            var corridorSystem = AltitudeCorridorSystem.Instance;
            if (corridorSystem != null)
            {
                corridor = corridorSystem.GetActiveCorridor(shipPosition);
                status = corridorSystem.ValidateAltitude(shipPosition, corridor);
            }

            SetStatus(status, altitude, corridor);
        }

        /// <summary>
        /// Установить статус и обновить визуал.
        /// </summary>
        private void SetStatus(AltitudeStatus status, float altitude, AltitudeCorridorData corridor)
        {
            // Тексты высоты и коридора
            if (_altitudeText != null)
                _altitudeText.text = $"Altitude: {altitude:F0}m";

            if (_corridorText != null && corridor != null)
                _corridorText.text = $"[{corridor.minAltitude:F0}m — {corridor.maxAltitude:F0}m]";
            else if (_corridorText != null)
                _corridorText.text = "[No corridor data]";

            // Цвета и иконки по статусу
            Color textColor;
            string icon;
            string statusLabel;

            switch (status)
            {
                case AltitudeStatus.Safe:
                    icon = "🟢";
                    statusLabel = "SAFE";
                    textColor = safeColor;
                    break;

                case AltitudeStatus.WarningLower:
                    icon = "🟡";
                    statusLabel = "WARNING: Approaching lower limit";
                    textColor = warningColor;
                    break;

                case AltitudeStatus.WarningUpper:
                    icon = "🟡";
                    statusLabel = "WARNING: Approaching upper limit";
                    textColor = warningColor;
                    break;

                case AltitudeStatus.DangerLower:
                    icon = "🔴";
                    statusLabel = "DANGER: BELOW CORRIDOR! TURBULENCE!";
                    textColor = dangerColor;
                    break;

                case AltitudeStatus.DangerUpper:
                    icon = "🔴";
                    statusLabel = "DANGER: ABOVE CRITICAL ALTITUDE!";
                    textColor = dangerColor;
                    break;

                default:
                    icon = "⚪";
                    statusLabel = "UNKNOWN";
                    textColor = Color.white;
                    break;
            }

            // Применяем тексты
            if (_statusIconText != null)
                _statusIconText.text = icon;

            if (_statusText != null)
                _statusText.text = statusLabel;

            // Применяем цвет ко всем текстам
            if (_statusIconText != null) _statusIconText.color = textColor;
            if (_statusText != null) _statusText.color = textColor;
            if (_altitudeText != null) _altitudeText.color = textColor;
            if (_corridorText != null) _corridorText.color = textColor;

            // Фон панели
            if (_background != null)
            {
                Color bgColor = textColor;
                bgColor.a = 0.3f;
                _background.color = bgColor;
            }
        }

        /// <summary>
        /// Скрыть панель.
        /// </summary>
        public void Hide()
        {
            if (_panelRect != null)
                _panelRect.gameObject.SetActive(false);
        }

        /// <summary>
        /// Показать панель.
        /// </summary>
        public void Show()
        {
            if (_panelRect != null)
                _panelRect.gameObject.SetActive(true);
        }
    }
}
