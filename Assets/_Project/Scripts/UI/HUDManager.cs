using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectC.UI
{
    /// <summary>
    /// Централизованный менеджер HUD Canvas.
    /// Все HUD создают свой UI в едином Canvas вместо создания своих собственных.
    /// 
    /// Создаёт один ScreenSpaceOverlay Canvas с правильным sortOrder.
    /// Все скрипты HUD должны использовать GetOrCreateHUDCanvas() вместо создания своих Canvas.
    /// 
    /// Сессия 5_3: Консолидация разбросанных HUD.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        [Header("Canvas Settings")]
        [Tooltip("Sort order для HUD Canvas (выше = поверх)")]
        [SerializeField] private int canvasSortOrder = 100;

        [Tooltip("Reference Resolution для CanvasScaler")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);

        [Tooltip("Match для масштабирования (0=Width, 1=Height, 0.5=Balanced)")]
        [SerializeField] private float uiScaleMatch = 0.5f;

        private Canvas _hudCanvas;
        private CanvasScaler _canvasScaler;
        private GraphicRaycaster _graphicRaycaster;

        // Кэш для быстрого доступа к дочерним элементам
        private readonly System.Collections.Generic.Dictionary<string, RectTransform> _namedPanels = 
            new System.Collections.Generic.Dictionary<string, RectTransform>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeCanvas();
            }
            else
            {
                if (Instance != this)
                {
                    // Уже есть другой HUDManager — переиспользуем его
                    Destroy(gameObject);
                }
                return;
            }
        }

        /// <summary>
        /// Инициализировать HUD Canvas если его ещё нет.
        /// </summary>
        private void InitializeCanvas()
        {
            if (_hudCanvas != null) return;

            // Ищем существующий или создаём новый
            var existingCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var canvas in existingCanvases)
            {
                if (canvas.CompareTag("HUDCanvas"))
                {
                    _hudCanvas = canvas;
                    break;
                }
            }

            if (_hudCanvas == null)
            {
                // Создаём новый Canvas
                var canvasObj = new GameObject("HUD_Canvas");
                canvasObj.tag = "HUDCanvas";
                canvasObj.transform.SetParent(transform);

                _hudCanvas = canvasObj.AddComponent<Canvas>();
                _hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _hudCanvas.sortingOrder = canvasSortOrder;

                _canvasScaler = canvasObj.AddComponent<CanvasScaler>();
                _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                _canvasScaler.referenceResolution = referenceResolution;
                _canvasScaler.matchWidthOrHeight = uiScaleMatch;

                _graphicRaycaster = canvasObj.AddComponent<GraphicRaycaster>();
                _graphicRaycaster.blockingMask = LayerMask.GetMask("Default");
                _graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.All;
            }
            else
            {
                // Восстанавливаем ссылки
                _canvasScaler = _hudCanvas.GetComponent<CanvasScaler>();
                _graphicRaycaster = _hudCanvas.GetComponent<GraphicRaycaster>();

                if (_canvasScaler == null)
                {
                    _canvasScaler = _hudCanvas.gameObject.AddComponent<CanvasScaler>();
                    _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    _canvasScaler.referenceResolution = referenceResolution;
                    _canvasScaler.matchWidthOrHeight = uiScaleMatch;
                }

                if (_graphicRaycaster == null)
                {
                    _graphicRaycaster = _hudCanvas.gameObject.AddComponent<GraphicRaycaster>();
                }
            }

            Debug.Log($"[HUDManager] Canvas initialized: sortOrder={canvasSortOrder}");
        }

        /// <summary>
        /// Получить или создать HUD Canvas.
        /// Все HUD должны использовать этот метод вместо создания своих Canvas.
        /// </summary>
        public Canvas GetOrCreateHUDCanvas()
        {
            if (_hudCanvas == null)
            {
                InitializeCanvas();
            }
            return _hudCanvas;
        }

        /// <summary>
        /// Создать текстовый элемент в HUD Canvas.
        /// </summary>
        /// <param name="name">Имя объекта</param>
        /// <param name="parent">Родитель (null = HUD Canvas)</param>
        /// <param name="fontSize">Размер шрифта</param>
        /// <param name="color">Цвет текста</param>
        /// <param name="alignment">Выравнивание</param>
        /// <param name="anchoredPosition">Позиция anchored</param>
        /// <param name="sizeDelta">Размер RectTransform</param>
        /// <returns>Tuple: (GameObject, RectTransform, TextMeshProUGUI)</returns>
        public (GameObject obj, RectTransform rect, TextMeshProUGUI text) CreateHUDText(
            string name,
            Transform parent = null,
            int fontSize = 14,
            Color color = default,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft,
            Vector2 anchoredPosition = default,
            Vector2 sizeDelta = default)
        {
            var canvas = GetOrCreateHUDCanvas();

            if (parent == null)
            {
                parent = canvas.transform;
            }

            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            var rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = anchoredPosition == default ? Vector2.zero : anchoredPosition;
            rect.sizeDelta = sizeDelta == default ? new Vector2(300, 100) : sizeDelta;

            var tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.fontSize = fontSize;
            tmpText.color = color == default ? Color.white : color;
            tmpText.alignment = alignment;
            tmpText.textWrappingMode = TextWrappingModes.Normal;

            return (textObj, rect, tmpText);
        }

        /// <summary>
        /// Создать панель с фоном.
        /// </summary>
        public (GameObject obj, RectTransform rect, Image background) CreateHUDPanel(
            string name,
            Transform parent = null,
            Vector2 anchorMin = default,
            Vector2 anchorMax = default,
            Vector2 anchoredPosition = default,
            Vector2 sizeDelta = default,
            Color backgroundColor = default)
        {
            var canvas = GetOrCreateHUDCanvas();

            if (parent == null)
            {
                parent = canvas.transform;
            }

            var panelObj = new GameObject(name);
            panelObj.transform.SetParent(parent);

            var rect = panelObj.AddComponent<RectTransform>();

            if (anchorMin != default)
            {
                rect.anchorMin = anchorMin;
            }
            if (anchorMax != default)
            {
                rect.anchorMax = anchorMax;
            }
            if (anchoredPosition != default)
            {
                rect.anchoredPosition = anchoredPosition;
            }
            if (sizeDelta != default)
            {
                rect.sizeDelta = sizeDelta;
            }

            var bg = panelObj.AddComponent<Image>();
            bg.color = backgroundColor == default ? new Color(0, 0, 0, 0.7f) : backgroundColor;

            return (panelObj, rect, bg);
        }

        /// <summary>
        /// Зарегистрировать панель по имени для быстрого доступа.
        /// </summary>
        public void RegisterPanel(string name, RectTransform panelRect)
        {
            if (!_namedPanels.ContainsKey(name))
            {
                _namedPanels[name] = panelRect;
            }
        }

        /// <summary>
        /// Получить зарегистрированную панель.
        /// </summary>
        public bool TryGetPanel(string name, out RectTransform panelRect)
        {
            return _namedPanels.TryGetValue(name, out panelRect);
        }

        /// <summary>
        /// Установить видимость панели по имени.
        /// </summary>
        public void SetPanelVisibility(string name, bool visible)
        {
            if (_namedPanels.TryGetValue(name, out var panel))
            {
                panel?.gameObject?.SetActive(visible);
            }
        }

        /// <summary>
        /// Получить TMP_FontAsset по умолчанию.
        /// </summary>
        public TMP_FontAsset GetDefaultFont()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font != null) return font;

            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) return font;

            var fonts = FindObjectsByType<TMP_FontAsset>(FindObjectsInactive.Include);
            if (fonts.Length > 0)
            {
                font = fonts[0];
                TMP_Settings.defaultFontAsset = font;
            }

            return font;
        }

        /// <summary>
        /// Проверить что HUDManager готов.
        /// </summary>
        public static bool IsReady()
        {
            return Instance != null && Instance._hudCanvas != null;
        }

        /// <summary>
        /// Убедиться что HUDManager существует.
        /// Создаёт новый если не найден.
        /// </summary>
        public static HUDManager EnsureExists()
        {
            if (Instance != null) return Instance;

            var existing = FindObjectsByType<HUDManager>(FindObjectsInactive.Include);
            if (existing.Length > 0)
            {
                Instance = existing[0];
                return Instance;
            }

            var go = new GameObject("[HUDManager]");
            Instance = go.AddComponent<HUDManager>();
            Debug.Log("[HUDManager] Created automatically");
            return Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}