using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ProjectC.Trade;
using ProjectC.Player;

/// <summary>
/// Клиентский UI торговли. Сессия 4.
/// Поток: Рынок <-> Склад игрока <-> Трюм корабля
///
/// Архитектура: ВСЕ UI-объекты создаются в Start() и уничтожаются в OnDestroy().
/// Все UI-ссылки — private, не-сериализуемые.
/// </summary>
public class TradeUI : MonoBehaviour
{
    [Header("Data")]
    public ProjectC.Trade.LocationMarket currentMarket;
    public ProjectC.Trade.PlayerTradeStorage playerStorage;
    public Transform tradeLocation;

    // === ВСЕ UI-ссылки — private, не-сериализуемые ===
    private GameObject _rootCanvas;
    private GameObject _tradePanel;
    private Transform _contentPanel;
    private Text _creditsText;
    private Text _warehouseInfoText;
    private Text _shipCargoInfoText;
    private Text _quantityText;
    private Text _messageText;
    private Text _modeText;

    private List<Button> _uiButtons = new List<Button>();

    // === Состояние ===
    private bool _isOpen;
    private int _selectedMarketIndex = -1;
    private int _buyQuantity = 1;
    private bool _showWarehouseTab = false;
    private ProjectC.Player.CargoSystem _nearbyCargo;

    private InputAction _toggleAction;

    // ==================== LIFECYCLE ====================

    private void Awake()
    {
        // Автоматический поиск PlayerTradeStorage если не назначен
        if (playerStorage == null)
        {
            var ps = FindAnyObjectByType<ProjectC.Trade.PlayerTradeStorage>();
            if (ps != null)
            {
                playerStorage = ps;
                Debug.Log("[TradeUI] Найден PlayerTradeStorage автоматически");
            }
        }

        _toggleAction = new InputAction("ToggleTrade", binding: "<Keyboard>/t");
        _toggleAction.performed += ctx => ToggleTrade();
    }

    private void Start()
    {
        BuildUI();
    }

    private void OnEnable() => _toggleAction?.Enable();
    private void OnDisable() => _toggleAction?.Disable();

    private void OnDestroy()
    {
        if (_toggleAction != null)
        {
            _toggleAction.performed -= ctx => ToggleTrade();
            _toggleAction.Dispose();
        }
        DestroyUI();
    }

    private void Update()
    {
        if (!_isOpen) return;
        HandleInput();
    }

    // ==================== ПОСТРОЕНИЕ UI ====================

    private void BuildUI()
    {
        // Canvas
        _rootCanvas = new GameObject("TradeUI_Root");
        var canvas = _rootCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        canvas.pixelPerfect = false;

        var scaler = _rootCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        _rootCanvas.AddComponent<GraphicRaycaster>();

        // Панель — anchoring через anchoredPositionMin/Max для надёжности
        _tradePanel = new GameObject("TradePanel");
        _tradePanel.transform.SetParent(_rootCanvas.transform, false);
        var panelRect = _tradePanel.AddComponent<RectTransform>();

        // Центрирование: anchorMin=(0.5,0.5), anchorMax=(0.5,0.5), sizeDelta=(500,620)
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500, 620);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.localScale = Vector3.one;

        // Фон панели — БЕЗ alpha чтобы точно видеть
        var img = _tradePanel.AddComponent<Image>();
        img.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        img.type = Image.Type.Simple;
        img.fillCenter = true;

        _tradePanel.SetActive(false);

        // --- Заголовок ---
        MakeText("Title", "ТОРГОВЛЯ", new Vector2(0, 270), 22, Color.yellow, 480);
        _modeText = MakeText("ModeText", "[РЫНОК]", new Vector2(0, 245), 15, Color.cyan, 200);

        // --- Инфо ---
        _creditsText = MakeText("CreditsText", "Кредиты: 1000 CR", new Vector2(0, 210), 16, Color.green, 460);
        _warehouseInfoText = MakeText("WarehouseInfo", "Склад: 0/10000 кг | 0/200 m3 | 0/50", new Vector2(0, 188), 12, new Color(0.6f, 0.6f, 0.6f), 460);
        _shipCargoInfoText = MakeText("ShipCargoInfo", "Корабль: нет рядом", new Vector2(0, 168), 12, new Color(0.5f, 0.5f, 0.5f), 460);

        // --- Кол-во ---
        MakeText("QtyLabel", "Кол-во (W/S):", new Vector2(-120, 145), 13, Color.white, 150);
        _quantityText = MakeText("QuantityText", "1", new Vector2(140, 145), 14, Color.white, 60);

        // --- Scroll-зона (товары) ---
        var scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(_tradePanel.transform, false);
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
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        _contentPanel = contentGO.transform;
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        var layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 2;
        layout.padding = new RectOffset(4, 4, 4, 4);

        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollGO.AddComponent<Mask>().showMaskGraphic = false;
        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.content = contentRect;
        sr.viewport = vpRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // --- Кнопки ---
        _uiButtons.Add(MakeButton("BuyBtn", "КУПИТЬ (Enter)", new Vector2(-135, -95), 160, 30, OnBuyClicked));
        _uiButtons.Add(MakeButton("SellBtn", "ПРОДАТЬ (Shift+Enter)", new Vector2(135, -95), 200, 30, OnSellClicked));
        _uiButtons.Add(MakeButton("LoadBtn", "ПОГРУЗИТЬ (L)", new Vector2(-120, -135), 220, 30, OnLoadClicked));
        _uiButtons.Add(MakeButton("UnloadBtn", "РАЗГРУЗИТЬ (U)", new Vector2(120, -135), 220, 30, OnUnloadClicked));
        _uiButtons.Add(MakeButton("CloseBtn", "ЗАКРЫТЬ (Esc)", new Vector2(0, -275), 200, 30, OnCloseClicked));

        // --- Подсказки ---
        _messageText = MakeText("MsgText", "Выберите товар и нажмите Enter", new Vector2(0, -175), 13, new Color(0.9f, 0.9f, 0.4f), 480);
        MakeText("Hint1", "Tab - сменить вкладку | Up/Down - выбор | W/S - кол-во", new Vector2(0, -200), 11, new Color(0.5f, 0.5f, 0.5f), 480);
        MakeText("Hint2", "L - погрузить на корабль | U - разгрузить | Esc - закрыть", new Vector2(0, -220), 11, new Color(0.5f, 0.5f, 0.5f), 480);
    }

    private void DestroyUI()
    {
        if (_rootCanvas != null)
        {
            Destroy(_rootCanvas);
            _rootCanvas = null;
        }
        _tradePanel = null;
        _contentPanel = null;
        _creditsText = null;
        _warehouseInfoText = null;
        _shipCargoInfoText = null;
        _quantityText = null;
        _messageText = null;
        _modeText = null;
        _uiButtons.Clear();
    }

    // ==================== ХЕЛПЕРЫ ====================

    private Text MakeText(string name, string text, Vector2 pos, int fontSize, Color color, float width)
    {
        if (_tradePanel == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(_tradePanel.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 22);
        rect.anchoredPosition = pos;
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return txt;
    }

    private Button MakeButton(string name, string label, Vector2 pos, float w, float h, UnityEngine.Events.UnityAction onClick)
    {
        if (_tradePanel == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(_tradePanel.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.18f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.28f);
        colors.pressedColor = new Color(0.25f, 0.25f, 0.35f);
        btn.colors = colors;

        var tGo = new GameObject("Text");
        tGo.transform.SetParent(go.transform, false);
        var tRect = tGo.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        var t = tGo.AddComponent<Text>();
        t.text = label;
        t.fontSize = 12;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btn;
    }

    // ==================== ОТКРЫТИЕ / ЗАКРЫТИЕ ====================

    private void ToggleTrade()
    {
        if (_isOpen) CloseTrade();
        else if (currentMarket != null) OpenTrade(currentMarket);
    }

    public void OpenTrade(LocationMarket market)
    {
        if (market == null || _tradePanel == null) return;

        currentMarket = market;
        _isOpen = true;
        _showWarehouseTab = false;
        _selectedMarketIndex = -1;

        if (playerStorage != null) playerStorage.Load();
        CheckNearbyShip();

        _tradePanel.SetActive(true);
        _tradePanel.transform.SetAsLastSibling();

        RenderItems();
        UpdateDisplays();
        Debug.Log($"[TradeUI] Открыт рынок: {market.locationName}");
    }

    public void CloseTrade()
    {
        _isOpen = false;
        _selectedMarketIndex = -1;
        if (_tradePanel != null) _tradePanel.SetActive(false);
        if (playerStorage != null) playerStorage.Save();
        ShowMessage("");
    }

    private void CheckNearbyShip()
    {
        Vector3 checkPos = tradeLocation != null ? tradeLocation.position : transform.position;
        var ships = FindObjectsByType<ProjectC.Player.ShipController>(FindObjectsInactive.Exclude);
        foreach (var ship in ships)
        {
            var cargo = ship.GetComponent<ProjectC.Player.CargoSystem>();
            if (cargo != null && Vector3.Distance(checkPos, ship.transform.position) < 15f)
            {
                _nearbyCargo = cargo;
                return;
            }
        }
        _nearbyCargo = null;
    }

    // ==================== РЕНДЕР ====================

    private void RenderItems()
    {
        if (_contentPanel == null) return;

        // Очистка
        for (int i = _contentPanel.childCount - 1; i >= 0; i--)
            Destroy(_contentPanel.GetChild(i).gameObject);

        int index = 0;

        if (_showWarehouseTab)
        {
            if (playerStorage != null)
            {
                foreach (var wItem in playerStorage.warehouse)
                {
                    if (wItem.item == null) continue;
                    MakeItemRow(wItem.item.displayName, 0, wItem.quantity, index, false);
                    index++;
                }
            }
            if (index == 0) MakeEmptyRow("Склад пуст");
        }
        else
        {
            if (currentMarket != null)
            {
                foreach (var mi in currentMarket.items)
                {
                    if (mi.item == null) continue;
                    MakeItemRow(mi.item.displayName, mi.currentPrice, mi.availableStock, index, true);
                    index++;
                }
            }
            if (index == 0) MakeEmptyRow("Рынок пуст");
        }
        if (_modeText != null) _modeText.text = _showWarehouseTab ? "[СКЛАД]" : "[РЫНОК]";
    }

    private void MakeItemRow(string name, float price, int qty, int index, bool isMarket)
    {
        var rowGO = new GameObject($"Row_{index}");
        rowGO.transform.SetParent(_contentPanel, false);
        var rRect = rowGO.AddComponent<RectTransform>();
        rRect.sizeDelta = new Vector2(0, 28);

        var bg = rowGO.AddComponent<Image>();
        bg.color = index % 2 == 0 ? new Color(0.08f, 0.08f, 0.12f) : new Color(0.12f, 0.12f, 0.18f);

        var tGO = new GameObject("ItemText");
        tGO.transform.SetParent(rowGO.transform, false);
        var tRect = tGO.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = new Vector2(8, 0);
        tRect.offsetMax = new Vector2(-8, 0);

        var t = tGO.AddComponent<Text>();
        t.text = isMarket
            ? $"{name}  -  {price:F0} CR  (сток: {qty})"
            : $"{name}  -  {qty} ед.";
        t.fontSize = 13;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleLeft;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var btn = rowGO.AddComponent<Button>();
        int ci = index;
        bool mkt = isMarket;
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => SelectItem(ci, mkt));
    }

    private void MakeEmptyRow(string msg)
    {
        var go = new GameObject("EmptyRow");
        go.transform.SetParent(_contentPanel, false);
        var r = go.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(0, 28);
        var t = go.AddComponent<Text>();
        t.text = msg;
        t.fontSize = 13;
        t.color = Color.gray;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void SelectItem(int index, bool isMarket)
    {
        _selectedMarketIndex = index;
        if (isMarket && currentMarket != null && index >= 0 && index < currentMarket.items.Count)
        {
            var mi = currentMarket.items[index];
            if (mi?.item != null)
                ShowMessage($"{mi.item.displayName} | {mi.currentPrice:F0} CR | Сток: {mi.availableStock}");
        }
        else if (!isMarket && playerStorage != null && index >= 0 && index < playerStorage.warehouse.Count)
        {
            var wi = playerStorage.warehouse[index];
            if (wi?.item != null)
                ShowMessage($"{wi.item.displayName} | На складе: {wi.quantity} ед.");
        }
    }

    // ==================== ДИСПЛЕИ ====================

    private void UpdateDisplays()
    {
        if (_creditsText != null && playerStorage != null)
            _creditsText.text = $"Кредиты: {playerStorage.credits:F0} CR";

        if (_warehouseInfoText != null && playerStorage != null)
            _warehouseInfoText.text = $"Склад: {playerStorage.CurrentWeight:F0}/{playerStorage.maxWeight} кг | {playerStorage.CurrentVolume:F1}/{playerStorage.maxVolume} m3 | {playerStorage.warehouse.Count}/{playerStorage.maxItemTypes}";

        if (_shipCargoInfoText != null)
        {
            if (_nearbyCargo != null)
            {
                _shipCargoInfoText.text = $"Корабль: {_nearbyCargo.CurrentWeight:F0}/{_nearbyCargo.MaxWeight} кг | {_nearbyCargo.CurrentVolume:F1}/{_nearbyCargo.MaxVolume} m3 | {_nearbyCargo.UsedSlots}/{_nearbyCargo.MaxSlots}";
                _shipCargoInfoText.color = Color.yellow;
            }
            else
            {
                _shipCargoInfoText.text = "Корабль: нет рядом";
                _shipCargoInfoText.color = Color.gray;
            }
        }
        if (_quantityText != null) _quantityText.text = _buyQuantity.ToString();
    }

    private void ShowMessage(string msg)
    {
        if (_messageText != null) _messageText.text = msg;
    }

    // ==================== ВВОД ====================

    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.wasPressedThisFrame) { _buyQuantity = Mathf.Min(_buyQuantity + 1, 99); UpdateDisplays(); }
        if (kb.sKey.wasPressedThisFrame) { _buyQuantity = Mathf.Max(_buyQuantity - 1, 1); UpdateDisplays(); }

        if (kb.upArrowKey.wasPressedThisFrame) { _selectedMarketIndex = Mathf.Max(0, _selectedMarketIndex - 1); ShowSelected(); }
        if (kb.downArrowKey.wasPressedThisFrame)
        {
            int max = _showWarehouseTab ? (playerStorage?.warehouse.Count ?? 0) - 1 : (currentMarket?.items.Count ?? 0) - 1;
            _selectedMarketIndex = Mathf.Min(max, _selectedMarketIndex + 1);
            ShowSelected();
        }

        if (kb.enterKey.wasPressedThisFrame && !kb.leftShiftKey.isPressed && !kb.rightShiftKey.isPressed)
            OnBuyClicked();

        if (kb.enterKey.wasPressedThisFrame && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            OnSellClicked();

        if (kb.tabKey.wasPressedThisFrame) { _showWarehouseTab = !_showWarehouseTab; RenderItems(); UpdateDisplays(); }

        if (kb.lKey.wasPressedThisFrame && _nearbyCargo != null && _showWarehouseTab && _selectedMarketIndex >= 0 && playerStorage != null)
            OnLoadClicked();

        if (kb.uKey.wasPressedThisFrame && _nearbyCargo != null && !_showWarehouseTab && _selectedMarketIndex >= 0 && currentMarket != null && playerStorage != null)
            OnUnloadClicked();

        if (kb.escapeKey.wasPressedThisFrame) CloseTrade();
    }

    private void ShowSelected()
    {
        if (_showWarehouseTab && playerStorage != null && _selectedMarketIndex >= 0 && _selectedMarketIndex < playerStorage.warehouse.Count)
        {
            var wi = playerStorage.warehouse[_selectedMarketIndex];
            if (wi?.item != null) ShowMessage($"{wi.item.displayName} | {wi.quantity} ед. | L - погрузить");
        }
        else if (currentMarket != null && _selectedMarketIndex >= 0 && _selectedMarketIndex < currentMarket.items.Count)
        {
            var mi = currentMarket.items[_selectedMarketIndex];
            if (mi?.item != null) ShowMessage($"{mi.item.displayName} | {mi.currentPrice:F0} CR | Enter: купить | Shift+Enter: продать");
        }
    }

    // ==================== CALLBACK КНОПОК ====================

    private void OnBuyClicked()
    {
        if (_showWarehouseTab || _selectedMarketIndex < 0 || currentMarket == null) return;
        if (_selectedMarketIndex >= currentMarket.items.Count) return;
        var item = currentMarket.items[_selectedMarketIndex]?.item;
        if (item == null) return;
        BuyItem(item.itemId, _buyQuantity);
    }

    private void OnSellClicked()
    {
        if (_showWarehouseTab || _selectedMarketIndex < 0 || currentMarket == null) return;
        if (_selectedMarketIndex >= currentMarket.items.Count) return;
        var item = currentMarket.items[_selectedMarketIndex]?.item;
        if (item == null) return;
        SellItem(item.itemId, _buyQuantity);
    }

    private void OnLoadClicked()
    {
        if (!_showWarehouseTab || _nearbyCargo == null || playerStorage == null || _selectedMarketIndex < 0) return;
        if (_selectedMarketIndex >= playerStorage.warehouse.Count) return;
        var item = playerStorage.warehouse[_selectedMarketIndex]?.item;
        if (item != null) playerStorage.LoadToShip(item.itemId, _buyQuantity, _nearbyCargo);
        UpdateDisplays();
        RenderItems();
    }

    private void OnUnloadClicked()
    {
        if (_showWarehouseTab || _nearbyCargo == null || currentMarket == null || playerStorage == null || _selectedMarketIndex < 0) return;
        if (_selectedMarketIndex >= currentMarket.items.Count) return;
        var item = currentMarket.items[_selectedMarketIndex]?.item;
        if (item != null) playerStorage.UnloadFromShip(item.itemId, _buyQuantity, _nearbyCargo);
        UpdateDisplays();
        RenderItems();
    }

    private void OnCloseClicked()
    {
        CloseTrade();
    }

    // ==================== ПОКУПКА / ПРОДАЖА ====================

    private void BuyItem(string itemId, int quantity)
    {
        if (currentMarket == null || playerStorage == null) return;
        var mi = currentMarket.items.Find(m => m.item != null && m.item.itemId == itemId);
        if (mi == null || mi.item == null) return;
        if (mi.availableStock < quantity) { ShowMessage($"Недостаточно товара! Есть {mi.availableStock}"); return; }

        bool ok = playerStorage.BuyItem(mi.item, quantity, mi.currentPrice);
        if (!ok) return;

        mi.availableStock -= quantity;
        mi.UpdateDemand(quantity);
        UpdateDisplays();
        RenderItems();
        ShowMessage($"КУПЛЕНО: {mi.item.displayName} x{quantity} за {mi.currentPrice * quantity:F0} CR");
    }

    private void SellItem(string itemId, int quantity)
    {
        if (currentMarket == null || playerStorage == null) return;
        var mi = currentMarket.items.Find(m => m.item != null && m.item.itemId == itemId);
        if (mi == null || mi.item == null) return;

        float sellPrice = mi.currentPrice * 0.8f;
        bool ok = playerStorage.SellItem(mi.item, quantity, sellPrice);
        if (!ok) return;

        mi.availableStock += quantity;
        mi.UpdateSupply(quantity);
        UpdateDisplays();
        RenderItems();
        ShowMessage($"ПРОДАНО: {mi.item.displayName} x{quantity} за {sellPrice * quantity:F0} CR (80%)");
    }
}
