using UnityEngine;

namespace ProjectC.UI
{
    /// <summary>
    /// Централизованная тема UI для консистентности всех экранов.
    /// Создаётся через CreateAssetMenu → ProjectC/UI Theme
    /// 
    /// Используется TradeUI, ContractBoardUI, InventoryUI и др.
    /// Заменяет 51+ хардкодный Color в коде.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme_Default", menuName = "ProjectC/UI Theme")]
    public class UITheme : ScriptableObject
    {
        // ==================== PANEL COLORS ====================
        
        [Header("📐 Panel Colors")]
        [Tooltip("Основной фон панелей (TradeUI, ContractBoard)")]
        public Color PanelBackground = new Color(0.04f, 0.04f, 0.07f, 0.97f);
        
        [Tooltip("Фон ContractBoard (чуть темнее)")]
        public Color ContractBoardBackground = new Color(0.03f, 0.05f, 0.08f, 0.97f);
        
        [Tooltip("Фон инвентаря (серый полупрозрачный)")]
        public Color InventoryBackground = new Color(0.1f, 0.1f, 0.1f, 0.92f);
        
        [Tooltip("Границы панелей")]
        public Color PanelBorder = new Color(0.12f, 0.12f, 0.19f, 1f);

        // ==================== ROW COLORS (Zebra Striping) ====================
        
        [Header("📊 Row Colors — Zebra Striping")]
        
        [Tooltip("Рыночные строки — чётные")]
        public Color MarketRowEven = new Color(0.06f, 0.06f, 0.10f, 1f);
        
        [Tooltip("Рыночные строки — нечётные")]
        public Color MarketRowOdd = new Color(0.10f, 0.10f, 0.15f, 1f);
        
        [Tooltip("Строки склада — чётные")]
        public Color WarehouseRowEven = new Color(0.06f, 0.06f, 0.10f, 1f);
        
        [Tooltip("Строки склада — нечётные")]
        public Color WarehouseRowOdd = new Color(0.10f, 0.10f, 0.15f, 1f);
        
        [Tooltip("Грузовые строки (тёплые тона) — чётные")]
        public Color CargoRowEven = new Color(0.12f, 0.08f, 0.04f, 1f);
        
        [Tooltip("Грузовые строки (тёплые тона) — нечётные")]
        public Color CargoRowOdd = new Color(0.15f, 0.10f, 0.06f, 1f);
        
        [Tooltip("Контрактные строки — чётные")]
        public Color ContractRowEven = new Color(0.06f, 0.06f, 0.10f, 1f);
        
        [Tooltip("Контрактные строки — нечётные")]
        public Color ContractRowOdd = new Color(0.10f, 0.10f, 0.15f, 1f);
        
        [Tooltip("Активные контракты (тёплый фон)")]
        public Color ActiveContractRow = new Color(0.15f, 0.10f, 0.05f, 1f);
        
        [Tooltip("Выделенная строка (selected)")]
        public Color SelectedRow = new Color(0.2f, 0.25f, 0.15f, 1f);
        
        [Tooltip("Строка при наведении (hover)")]
        public Color HoverRow = new Color(0.22f, 0.22f, 0.15f, 1f);

        // ==================== BUTTON COLORS ====================
        
        [Header("🔘 Button Colors")]
        
        [Tooltip("Кнопка по умолчанию")]
        public Color ButtonDefault = new Color(0.15f, 0.15f, 0.22f, 1f);
        
        [Tooltip("Кнопка при наведении")]
        public Color ButtonHover = new Color(0.22f, 0.22f, 0.30f, 1f);
        
        [Tooltip("Кнопка при нажатии")]
        public Color ButtonPressed = new Color(0.28f, 0.28f, 0.38f, 1f);
        
        [Tooltip("Кнопка отключена")]
        public Color ButtonDisabled = new Color(0.1f, 0.1f, 0.15f, 0.5f);

        // ==================== ACCENT COLORS ====================
        
        [Header("🎨 Accent Colors")]
        
        [Tooltip("Основной акцент — sci-fi голубой")]
        public Color Accent = new Color(0.30f, 0.64f, 1.0f, 1f); // #4DA3FF
        
        [Tooltip("Предупреждение — оранжевый")]
        public Color AccentWarning = new Color(1.0f, 0.72f, 0.0f, 1f); // #FFB800
        
        [Tooltip("Опасность — красный")]
        public Color AccentDanger = new Color(1.0f, 0.27f, 0.27f, 1f); // #FF4545
        
        [Tooltip("Успех — зелёный")]
        public Color AccentSuccess = new Color(0.0f, 1.0f, 0.0f, 1f); // #00FF00
        
        [Tooltip("Информация — циан")]
        public Color AccentInfo = new Color(0.0f, 1.0f, 1.0f, 1f); // #00FFFF

        // ==================== TEXT COLORS ====================
        
        [Header("📝 Text Colors")]
        
        [Tooltip("Заголовки")]
        public Color TextTitle = Color.yellow;
        
        [Tooltip("Основной текст")]
        public Color TextPrimary = new Color(0.91f, 0.91f, 0.94f, 1f); // #E8E8F0
        
        [Tooltip("Вторичный текст")]
        public Color TextSecondary = new Color(0.53f, 0.53f, 0.63f, 1f); // #8788A1
        
        [Tooltip("Приглушённый текст (подсказки)")]
        public Color TextMuted = new Color(0.53f, 0.53f, 0.53f, 1f); // #878787
        
        [Tooltip("Кредиты — зелёный")]
        public Color TextCredits = Color.green;
        
        [Tooltip("Текст сообщений — светло-жёлтый")]
        public Color TextMessage = new Color(0.9f, 0.9f, 0.4f, 1f); // #E6E666
        
        [Tooltip("Текст груза — тёплый золотистый")]
        public Color TextCargo = new Color(1f, 0.85f, 0.5f, 1f); // #FFD980
        
        [Tooltip("Текст на кнопках")]
        public Color TextOnButton = Color.white;

        // ==================== FONT SIZES ====================
        
        [Header("🔤 Font Sizes")]
        
        [Tooltip("Заголовки (24-26px)")]
        public int FontSizeHeading = 24;
        
        [Tooltip("Подзаголовки (20-22px)")]
        public int FontSizeSubheading = 22;
        
        [Tooltip("Основной текст (14-16px)")]
        public int FontSizeBody = 14;
        
        [Tooltip("Кнопки (13-14px)")]
        public int FontSizeButton = 13;
        
        [Tooltip("Строки списков (13px)")]
        public int FontSizeList = 13;
        
        [Tooltip("Информационный текст (12px)")]
        public int FontSizeInfo = 12;
        
        [Tooltip("Подсказки и таймеры (11px)")]
        public int FontSizeCaption = 11;

        // ==================== SPACING ====================
        
        [Header("📏 Spacing")]
        
        [Tooltip("Маленький отступ")]
        public float SpacingSmall = 4f;
        
        [Tooltip("Средний отступ")]
        public float SpacingMedium = 8f;
        
        [Tooltip("Большой отступ")]
        public float SpacingLarge = 16f;
        
        [Tooltip("Отступ между строками в списке")]
        public float ListItemSpacing = 2f;
        
        [Tooltip("Высота строки списка")]
        public float ListItemHeight = 30f;
        
        [Tooltip("Высота строки контракта")]
        public float ContractRowHeight = 50f;
        
        [Tooltip("Высота активного контракта")]
        public float ActiveContractRowHeight = 40f;

        // ==================== CANVAS SETTINGS ====================
        
        [Header("🎬 Canvas Settings")]
        
        [Tooltip("Reference resolution для CanvasScaler")]
        public Vector2 ReferenceResolution = new Vector2(1920, 1080);
        
        [Tooltip("Match width or height для CanvasScaler")]
        public float CanvasMatchWidthOrHeight = 0.5f;
        
        [Tooltip("Sorting order для TradeUI")]
        public int TradeUISortingOrder = 5000;
        
        [Tooltip("Sorting order для ContractBoardUI")]
        public int ContractBoardUISortingOrder = 5100;
        
        [Tooltip("Sorting order для InventoryUI")]
        public int InventoryUISortingOrder = 5200;

        // ==================== ICONS (Emoji Replacements) ====================
        
        [Header("🖼️ Sci-Fi Icons (TextMeshPro Sprite)")]
        
        [Tooltip("Иконка для списка контрактов")]
        public string IconContract = "\uE8A5"; // Unity default: list
        
        [Tooltip("Иконка для груза")]
        public string IconCargo = "\uE8B4"; // Unity default: box
        
        [Tooltip("Иконка для срочного контракта")]
        public string IconUrgent = "\uE7BA"; // Unity default: warning
        
        [Tooltip("Иконка для подтверждения")]
        public string IconConfirm = "\uE73E"; // Unity default: check
        
        [Tooltip("Иконка для предупреждения")]
        public string IconWarning = "\uE7BA"; // Unity default: warning

        // ==================== CONTRACT TYPE COLORS ====================
        
        [Header("📋 Contract Type Colors")]
        
        [Tooltip("Стандартный контракт")]
        public Color ContractStandard = new Color(0.30f, 0.60f, 1.0f, 1f); // #4D99FF
        
        [Tooltip("Срочный контракт")]
        public Color ContractUrgent = new Color(1.0f, 0.50f, 0.0f, 1f); // #FF8000
        
        [Tooltip("Контракт под расписку")]
        public Color ContractReceipt = new Color(0.30f, 1.0f, 0.30f, 1f); // #4DFF4D

        // ==================== DEBT LEVEL COLORS ====================
        
        [Header("💰 Debt Level Colors")]
        
        [Tooltip("Нет долга")]
        public Color DebtNone = Color.green;
        
        [Tooltip("Предупреждение о долге")]
        public Color DebtWarning = Color.yellow;
        
        [Tooltip("Ограничения")]
        public Color DebtRestricted = new Color(1.0f, 0.50f, 0.0f, 1f); // #FF8000
        
        [Tooltip("Охота")]
        public Color DebtHunted = Color.red;
        
        [Tooltip("Розыск")]
        public Color DebtBounty = new Color(0.8f, 0.0f, 0.0f, 1f); // #CC0000
        
        [Tooltip("Глобальный розыск")]
        public Color DebtHeadhunt = new Color(0.5f, 0.0f, 0.0f, 1f); // #800000

        // ==================== STATIC INSTANCE ====================
        
        private static UITheme _default;

        /// <summary>
        /// Получить тему по умолчанию (создаёт если нет)
        /// </summary>
        public static UITheme Default
        {
            get
            {
                if (_default == null)
                {
#if UNITY_EDITOR
                    // Попробуем найти в Resources
                    _default = UnityEditor.AssetDatabase.LoadAssetAtPath<UITheme>("Assets/_Project/Resources/UITheme_Default.asset");
                    
                    // Если не найден — создадим автоматически
                    if (_default == null)
                    {
                        Debug.Log("[UITheme] Автоматическое создание темы по умолчанию");
                        _default = CreateInstance<UITheme>();
#if UNITY_EDITOR
                        // Сохраним в Resources для будущего использования
                        string dir = "Assets/_Project/Resources";
                        if (!System.IO.Directory.Exists(dir))
                            System.IO.Directory.CreateDirectory(dir);
                        
                        UnityEditor.AssetDatabase.CreateAsset(_default, "Assets/_Project/Resources/UITheme_Default.asset");
                        UnityEditor.AssetDatabase.SaveAssets();
                        Debug.Log("[UITheme] Тема сохранена: Assets/_Project/Resources/UITheme_Default.asset");
#endif
                    }
#else
                    _default = CreateInstance<UITheme>();
                    Debug.LogWarning("[UITheme] Runtime fallback — создайте UITheme через CreateAssetMenu > ProjectC/UI Theme");
#endif
                }
                return _default;
            }
        }

        // ==================== HELPER METHODS ====================
        
        /// <summary>
        /// Получить цвет строки по индексу (zebra striping)
        /// </summary>
        public Color GetRowColor(bool isEven, Color evenColor, Color oddColor)
        {
            return isEven ? evenColor : oddColor;
        }

        /// <summary>
        /// Получить цвет строки для рынка
        /// </summary>
        public Color GetMarketRowColor(int index)
        {
            return index % 2 == 0 ? MarketRowEven : MarketRowOdd;
        }

        /// <summary>
        /// Получить цвет строки для груза
        /// </summary>
        public Color GetCargoRowColor(int index)
        {
            return index % 2 == 0 ? CargoRowEven : CargoRowOdd;
        }

        /// <summary>
        /// Получить цвет строки для контракта
        /// </summary>
        public Color GetContractRowColor(int index, bool isActive = false)
        {
            if (isActive) return ActiveContractRow;
            return index % 2 == 0 ? ContractRowEven : ContractRowOdd;
        }
    }
}
