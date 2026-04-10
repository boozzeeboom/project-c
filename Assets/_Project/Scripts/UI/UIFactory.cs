using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectC.UI
{
    /// <summary>
    /// Фабрика для программного создания UI-элементов.
    /// Устраняет дублирование кода между TradeUI, ContractBoardUI и другими UI-экранами.
    /// Все текстовые элементы используют TextMeshProUGUI (не legacy UnityEngine.UI.Text).
    /// </summary>
    public static class UIFactory
    {
        // ==================== PANEL ====================

        /// <summary>
        /// Создаёт панель с фоном и границей.
        /// </summary>
        /// <param name="name">Имя GameObject</param>
        /// <param name="parent">Родительский Transform</param>
        /// <param name="x">Позиция X (anchored)</param>
        /// <param name="y">Позиция Y (anchored)</param>
        /// <param name="width">Ширина</param>
        /// <param name="height">Высота</param>
        /// <returns>Созданный GameObject панели</returns>
        public static GameObject CreatePanel(string name, Transform parent, int x, int y, int width, int height)
        {
            return CreatePanel(name, parent, x, y, width, height, UITheme.Default.PanelBackground);
        }

        /// <summary>
        /// Создаёт панель с указанным цветом фона.
        /// </summary>
        public static GameObject CreatePanel(string name, Transform parent, int x, int y, int width, int height, Color backgroundColor)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreatePanel: parent is null");
                return null;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);

            var img = go.AddComponent<Image>();
            img.color = backgroundColor;

            return go;
        }

        // ==================== LABEL ====================

        /// <summary>
        /// Создаёт текстовую метку (TextMeshProUGUI) с центрированным выравниванием.
        /// </summary>
        /// <param name="name">Имя GameObject</param>
        /// <param name="parent">Родительский Transform</param>
        /// <param name="text">Текст метки</param>
        /// <param name="x">Позиция X (anchored)</param>
        /// <param name="y">Позиция Y (anchored)</param>
        /// <param name="fontSize">Размер шрифта</param>
        /// <param name="color">Цвет текста (null = TextPrimary из темы)</param>
        /// <param name="width">Ширина RectTransform</param>
        /// <returns>Компонент TextMeshProUGUI</returns>
        public static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, int x, int y, int fontSize, Color? color = null, int width = 200)
        {
            return CreateLabel(name, parent, text, x, y, fontSize, color, width, TextAlignmentOptions.Center);
        }

        /// <summary>
        /// Создаёт текстовую метку с указанием выравнивания.
        /// </summary>
        public static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, int x, int y, int fontSize, Color? color, int width, TextAlignmentOptions alignment)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreateLabel: parent is null");
                return null;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 24);
            rect.anchoredPosition = new Vector2(x, y);

            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color ?? UITheme.Default.TextPrimary;
            txt.alignment = alignment;

            return txt;
        }

        // ==================== BUTTON ====================

        /// <summary>
        /// Создаёт кнопку с текстом (TextMeshProUGUI) и обработчиком клика.
        /// </summary>
        /// <param name="name">Имя GameObject</param>
        /// <param name="parent">Родительский Transform</param>
        /// <param name="text">Текст на кнопке</param>
        /// <param name="x">Позиция X (anchored)</param>
        /// <param name="y">Позиция Y (anchored)</param>
        /// <param name="width">Ширина</param>
        /// <param name="height">Высота</param>
        /// <param name="onClick">Обработчик нажатия</param>
        /// <returns>Компонент Button</returns>
        public static Button CreateButton(string name, Transform parent, string text, int x, int y, int width, int height, UnityEngine.Events.UnityAction onClick)
        {
            return CreateButton(name, parent, text, onClick, new Vector2(width, height), x, y);
        }

        /// <summary>
        /// Создаёт кнопку с текстом (TextMeshProUGUI) и обработчиком клика (перегрузка с Vector2 size).
        /// </summary>
        public static Button CreateButton(string name, Transform parent, string text, UnityEngine.Events.UnityAction onClick, Vector2 size, float x = 0, float y = 0)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreateButton: parent is null");
                return null;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(x, y);

            var img = go.AddComponent<Image>();
            img.color = UITheme.Default.ButtonDefault;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            var cols = btn.colors;
            cols.highlightedColor = UITheme.Default.ButtonHover;
            cols.pressedColor = UITheme.Default.ButtonPressed;
            cols.disabledColor = UITheme.Default.ButtonDisabled;
            btn.colors = cols;

            // Текст кнопки — через child TextMeshProUGUI
            var tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);

            var tRect = tGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var t = tGo.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = UITheme.Default.FontSizeButton;
            t.color = UITheme.Default.TextOnButton;
            t.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        // ==================== SCROLL AREA ====================

        /// <summary>
        /// Создаёт ScrollRect (область прокрутки) с Viewport и Content.
        /// </summary>
        /// <param name="parent">Родительский Transform</param>
        /// <param name="content">Выходной параметр — RectTransform контента (для добавления элементов)</param>
        /// <returns>Компонент ScrollRect</returns>
        public static ScrollRect CreateScrollArea(Transform parent, out RectTransform content)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreateScrollArea: parent is null");
                content = null;
                return null;
            }

            // ScrollArea root
            var scrollGO = new GameObject("ScrollArea");
            scrollGO.transform.SetParent(parent, false);
            var scrollRect = scrollGO.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.04f, 0.19f);
            scrollRect.anchorMax = new Vector2(0.96f, 0.65f);
            scrollRect.sizeDelta = Vector2.zero;

            // Viewport
            var vpGO = new GameObject("Viewport");
            vpGO.transform.SetParent(scrollGO.transform, false);
            var vpRect = vpGO.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMax = new Vector2(-14, 0);

            // Content
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(vpGO.transform, false);
            content = contentGO.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 0);

            // Layout on Content
            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = UITheme.Default.ListItemSpacing;
            layout.padding = new RectOffset(4, 4, 4, 4);

            contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Mask + ScrollRect
            scrollGO.AddComponent<Mask>().showMaskGraphic = false;
            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = vpRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;

            return sr;
        }

        /// <summary>
        /// Создаёт ScrollArea с кастомными anchor-точками.
        /// </summary>
        public static ScrollRect CreateScrollArea(Transform parent, out RectTransform content, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreateScrollArea: parent is null");
                content = null;
                return null;
            }

            var scrollGO = new GameObject("ScrollArea");
            scrollGO.transform.SetParent(parent, false);
            var scrollRect = scrollGO.AddComponent<RectTransform>();
            scrollRect.anchorMin = anchorMin;
            scrollRect.anchorMax = anchorMax;
            scrollRect.sizeDelta = Vector2.zero;

            var vpGO = new GameObject("Viewport");
            vpGO.transform.SetParent(scrollGO.transform, false);
            var vpRect = vpGO.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMax = new Vector2(-14, 0);

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(vpGO.transform, false);
            content = contentGO.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 0);

            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = UITheme.Default.ListItemSpacing;
            layout.padding = new RectOffset(4, 4, 4, 4);

            contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollGO.AddComponent<Mask>().showMaskGraphic = false;
            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = vpRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;

            return sr;
        }

        // ==================== DIVIDER ====================

        /// <summary>
        /// Создаёт строку-разделитель с текстом (cyan, 11px).
        /// </summary>
        public static GameObject CreateDivider(Transform parent, string text)
        {
            return CreateDivider(parent, text, UITheme.Default.AccentInfo);
        }

        /// <summary>
        /// Создаёт строку-разделитель с текстом и указанным цветом.
        /// </summary>
        public static GameObject CreateDivider(Transform parent, string text, Color? color = null)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreateDivider: parent is null");
                return null;
            }

            var go = new GameObject("DividerRow");
            go.transform.SetParent(parent, false);

            var r = go.AddComponent<RectTransform>();
            r.sizeDelta = new Vector2(0, 22);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = UITheme.Default.FontSizeCaption;
            t.color = color ?? UITheme.Default.AccentInfo;
            t.alignment = TextAlignmentOptions.Center;

            return go;
        }

        /// <summary>
        /// Создаёт строку-разделитель с указанием размера шрифта и цвета (для TradeUI/ContractBoardUI).
        /// </summary>
        public static void CreateDividerRow(Transform parent, string text, int fontSize, Color color)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreateDividerRow: parent is null");
                return;
            }

            var go = new GameObject("DividerRow");
            go.transform.SetParent(parent, false);

            var r = go.AddComponent<RectTransform>();
            r.sizeDelta = new Vector2(0, 22);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
        }

        // ==================== EMPTY ROW ====================

        /// <summary>
        /// Создаёт пустую строку с сообщением (gray, 13px, 30px height).
        /// </summary>
        public static GameObject CreateEmptyRow(Transform parent, string message)
        {
            return CreateEmptyRow(parent, message, UITheme.Default.TextSecondary);
        }

        /// <summary>
        /// Создаёт пустую строку с сообщением и указанным цветом.
        /// </summary>
        public static GameObject CreateEmptyRow(Transform parent, string message, Color? color = null)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreateEmptyRow: parent is null");
                return null;
            }

            var go = new GameObject("EmptyRow");
            go.transform.SetParent(parent, false);

            var r = go.AddComponent<RectTransform>();
            r.sizeDelta = new Vector2(0, 30);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = message;
            t.fontSize = UITheme.Default.FontSizeBody;
            t.color = color ?? UITheme.Default.TextSecondary;
            t.alignment = TextAlignmentOptions.Center;

            return go;
        }

        // ==================== DESTROY UI ====================

        /// <summary>
        /// Уничтожает все UI-элементы и очищает ссылки.
        /// </summary>
        /// <param name="elements">Список GameObject-элементов для очистки (rows, etc.)</param>
        /// <param name="rootCanvas">Корневой Canvas (уничтожается целиком)</param>
        public static void DestroyUIElements(List<GameObject> elements, GameObject rootCanvas)
        {
            if (rootCanvas != null)
            {
                Object.Destroy(rootCanvas);
            }

            if (elements != null)
            {
                elements.Clear();
            }
        }

        // ==================== CANVAS SETUP ====================

        /// <summary>
        /// Создаёт корневой Canvas с CanvasScaler и GraphicRaycaster.
        /// </summary>
        /// <param name="name">Имя Canvas GameObject</param>
        /// <param name="sortingOrder">Порядок сортировки (выше = поверх)</param>
        /// <returns>GameObject корневого Canvas</returns>
        public static GameObject CreateRootCanvas(string name, int sortingOrder = 5000)
        {
            var theme = UITheme.Default;

            var go = new GameObject(name);
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = theme.ReferenceResolution;
            scaler.matchWidthOrHeight = theme.CanvasMatchWidthOrHeight;

            go.AddComponent<GraphicRaycaster>();

            return go;
        }

        // ==================== ROOT ROW (Clickable) ====================

        /// <summary>
        /// Создаёт кликабельную строку списка с фоном (zebra striping).
        /// </summary>
        /// <param name="name">Имя GameObject</param>
        /// <param name="parent">Родительский Transform (обычно Content)</param>
        /// <param name="index">Индекс строки (для чередования цветов)</param>
        /// <param name="height">Высота строки</param>
        /// <param isMarket">true для рыночных цветов, false для склада</param>
        /// <returns>GameObject строки с Image и Button</returns>
        public static GameObject CreateListRow(string name, Transform parent, int index, float height = 30f, bool isMarket = true)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreateListRow: parent is null");
                return null;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rRect = go.AddComponent<RectTransform>();
            rRect.anchorMin = Vector2.zero;
            rRect.anchorMax = Vector2.one;
            rRect.sizeDelta = Vector2.zero;

            var layoutElem = go.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = height;

            var bg = go.AddComponent<Image>();
            var theme = UITheme.Default;
            bg.color = index % 2 == 0
                ? (isMarket ? theme.MarketRowEven : theme.WarehouseRowEven)
                : (isMarket ? theme.MarketRowOdd : theme.WarehouseRowOdd);

            // Текст
            CreateRowLabel("Text", go.transform, name, theme.FontSizeList, theme.TextPrimary, TextAlignmentOptions.Left);

            return go;
        }

        /// <summary>
        /// Перегрузка CreateListRow для TradeUI/ContractBoardUI — создаёт строку с текстом и указанным цветом.
        /// </summary>
        public static GameObject CreateListRow(Transform parent, string text, Color textColor, int index, bool isMarket = false, bool isCargo = false, bool isActive = false)
        {
            if (parent == null)
            {
                Debug.LogError("[UIFactory] CreateListRow: parent is null");
                return null;
            }

            var theme = UITheme.Default;
            string rowName = $"Row_{index}";
            var go = new GameObject(rowName);
            go.transform.SetParent(parent, false);

            var rRect = go.AddComponent<RectTransform>();
            rRect.anchorMin = Vector2.zero;
            rRect.anchorMax = Vector2.one;
            rRect.sizeDelta = Vector2.zero;

            var layoutElem = go.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 30f;

            var bg = go.AddComponent<Image>();
            if (isCargo)
            {
                bg.color = index % 2 == 0 ? theme.CargoRowEven : theme.CargoRowOdd;
            }
            else if (isActive)
            {
                bg.color = theme.ActiveContractRow;
            }
            else
            {
                bg.color = index % 2 == 0 ? theme.MarketRowEven : theme.MarketRowOdd;
            }

            // Текст
            CreateRowLabel("Text", go.transform, text, theme.FontSizeList, textColor, TextAlignmentOptions.Left);

            return go;
        }

        /// <summary>
        /// Создаёт текстовый элемент внутри строки с заполнением по ширине.
        /// </summary>
        /// <param name="name">Имя GameObject текста</param>
        /// <param name="parentRow">Родительская строка</param>
        /// <param name="text">Текст</param>
        /// <param name="fontSize">Размер шрифта</param>
        /// <param name="color">Цвет текста</param>
        /// <param name="alignment">Выравнивание</param>
        /// <param name="paddingX">Горизонтальный отступ (по умолчанию 8)</param>
        /// <returns>TextMeshProUGUI</returns>
        public static TextMeshProUGUI CreateRowLabel(string name, Transform parentRow, string text, int fontSize, Color color, TextAlignmentOptions alignment, int paddingX = 8)
        {
            if (parentRow == null)
            {
                Debug.LogError("[UIFactory] CreateRowLabel: parentRow is null");
                return null;
            }

            var tGO = new GameObject(name);
            tGO.transform.SetParent(parentRow, false);

            var tRect = tGO.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(paddingX, 0);
            tRect.offsetMax = new Vector2(-paddingX, 0);

            var t = tGO.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;

            return t;
        }
    }
}
