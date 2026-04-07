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
/// Простой UI через UnityEngine.UI.Text (без TMP).
/// </summary>
public class TradeUI : MonoBehaviour
{
    [Header("Data")]
    public LocationMarket currentMarket;
    public PlayerTradeStorage playerStorage;
    public Transform tradeLocation;

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
    private List<GameObject> _itemRows = new List<GameObject>();

    private bool _isOpen;
    private int _selectedIndex = -1;
    private int _buyQuantity = 1;
    private bool _showWarehouseTab = false;
    private CargoSystem _nearbyCargo;
    private NetworkPlayer _player;

    private void Awake()
    {
        if (playerStorage == null)
            playerStorage = FindAnyObjectByType<PlayerTradeStorage>();
        // Находим игрока для блокировки ввода
        _player = FindAnyObjectByType<NetworkPlayer>();
    }

    private void Start()
    {
        BuildUI();
    }

    private void OnDestroy()
    {
        DestroyUI();
    }

    private void Update()
    {
        if (!_isOpen) return;
        HandleInput();
    }

    private void BuildUI()
    {
        // --- Root Canvas ---
        _rootCanvas = new GameObject("[TradeUI]_RootCanvas");
        _rootCanvas.layer = LayerMask.NameToLayer("UI");

        var canvas = _rootCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvas.pixelPerfect = false;

        var scaler = _rootCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _rootCanvas.AddComponent<GraphicRaycaster>();

        // --- Панель ---
        _tradePanel = CreatePanel("TradePanel", _rootCanvas.transform, 0, 0, 520, 640);
        _tradePanel.SetActive(false);

        // --- Заголовок ---
        MakeLabel("Title", _tradePanel.transform, "ТОРГОВЛЯ", 0, 280, 22, Color.yellow, 480);
        _modeText = MakeLabel("ModeText", _tradePanel.transform, "[РЫНОК]", 0, 252, 15, Color.cyan, 200);

        // --- Инфо ---
        _creditsText = MakeLabel("CreditsText", _tradePanel.transform, "Кредиты: 1000 CR", 0, 220, 16, Color.green, 480);
        _warehouseInfoText = MakeLabel("WarehouseInfo", _tradePanel.transform, "Склад: 0/10000 кг | 0/200 m3 | 0/50", 0, 196, 12, Color.grey, 480);
        _shipCargoInfoText = MakeLabel("ShipCargoInfo", _tradePanel.transform, "Корабль: нет рядом", 0, 176, 12, Color.grey, 480);

        // --- Кол-во ---
        MakeLabel("QtyLabel", _tradePanel.transform, "Кол-во (< >):", -120, 152, 13, Color.white, 150);
        _quantityText = MakeLabel("QuantityText", _tradePanel.transform, "1", 140, 152, 14, Color.white, 60);

        // --- Scroll-зона ---
        var scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(_tradePanel.transform, false);
        var scrollRect2 = scrollGO.AddComponent<RectTransform>();
        scrollRect2.anchorMin = new Vector2(0.04f, 0.19f);
        scrollRect2.anchorMax = new Vector2(0.96f, 0.65f);
        scrollRect2.sizeDelta = Vector2.zero;

        var vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        var vpRect = vpGO.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMax = new Vector2(-14, 0);

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

        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollGO.AddComponent<Mask>().showMaskGraphic = false;
        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.content = contentRect;
        sr.viewport = vpRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // --- Кнопки (внизу панели) ---
        // Используем anchoredPosition для точного позиционирования
        _uiButtons.Add(MakeBtn("BuyBtn", _tradePanel.transform, "КУПИТЬ (Enter)", 0, -80, 240, 36, OnBuyClicked));
        _uiButtons.Add(MakeBtn("SellBtn", _tradePanel.transform, "ПРОДАТЬ (Shift+Enter)", 0, -125, 280, 36, OnSellClicked));
        _uiButtons.Add(MakeBtn("LoadBtn", _tradePanel.transform, "ПОГРУЗИТЬ (L)", -130, -175, 240, 36, OnLoadClicked));
        _uiButtons.Add(MakeBtn("UnloadBtn", _tradePanel.transform, "РАЗГРУЗИТЬ (U)", 130, -175, 240, 36, OnUnloadClicked));
        _uiButtons.Add(MakeBtn("CloseBtn", _tradePanel.transform, "ЗАКРЫТЬ (Esc)", 0, -285, 200, 36, OnCloseClicked));

        // --- Сообщение ---
        _messageText = MakeLabel("MsgText", _tradePanel.transform, "Выберите товар и нажмите Enter", 0, -230, 13, new Color(0.9f, 0.9f, 0.4f), 480);
        MakeLabel("Hint1", _tradePanel.transform, "B - склад | Up/Down - выбор | Left/Right - кол-во", 0, -255, 11, Color.grey, 480);
        MakeLabel("Hint2", _tradePanel.transform, "L/U - погрузить/разгрузить | Esc - закрыть | R - сброс", 0, -272, 11, Color.grey, 480);
    }

    private void DestroyUI()
    {
        if (_rootCanvas != null) { Destroy(_rootCanvas); _rootCanvas = null; }
        _tradePanel = null;
        _contentPanel = null;
        _creditsText = null;
        _warehouseInfoText = null;
        _shipCargoInfoText = null;
        _quantityText = null;
        _messageText = null;
        _modeText = null;
        _uiButtons.Clear();
        _itemRows.Clear();
    }

    // --- Панель ---
    private GameObject CreatePanel(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.04f, 0.04f, 0.07f, 0.97f);
        return go;
    }

    // --- Текст ---
    private Text MakeLabel(string name, Transform parent, string text, float x, float y, int fontSize, Color color, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 24);
        rect.anchoredPosition = new Vector2(x, y);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return txt;
    }

    // --- Кнопка ---
    private Button MakeBtn(string name, Transform parent, string label, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, y);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.22f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.22f, 0.22f, 0.30f);
        cols.pressedColor = new Color(0.28f, 0.28f, 0.38f);
        btn.colors = cols;

        // Текст кнопки — через child Text
        var tGo = new GameObject("Text");
        tGo.transform.SetParent(go.transform, false);
        var tRect = tGo.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;
        var t = tGo.AddComponent<Text>();
        t.text = label;
        t.fontSize = 13;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btn;
    }

    // ==================== ОТКРЫТИЕ / ЗАКРЫТИЕ ====================

    public void ToggleTrade()
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
        _selectedIndex = -1;
        if (playerStorage != null)
        {
            playerStorage.Load();
            Debug.Log($"[TradeUI] После Load(): credits={playerStorage.credits:F0}, warehouse={playerStorage.warehouse.Count}");
        }
        CheckNearbyShip();

        // Блокируем ввод игрока (не нужно — используем другие кнопки)
        // if (_player != null) _player.InputLocked = true;

        // Автовыбор первого товара
        _selectedIndex = 0;

        _tradePanel.SetActive(true);
        _tradePanel.transform.SetAsLastSibling();
        RenderItems();
        UpdateDisplays();

        // Снимаем фокус с кнопок чтобы Enter не срабатывал на них
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

        Debug.Log($"[TradeUI] Открыт рынок: {market.locationName} | Товаров: {market.items.Count}");
    }

    public void CloseTrade()
    {
        _isOpen = false;
        _selectedIndex = -1;
        if (_tradePanel != null) _tradePanel.SetActive(false);
        if (playerStorage != null) playerStorage.Save();

        // Разблокируем ввод игрока
        // if (_player != null) _player.InputLocked = false;

        ShowMessage("");
    }

    private void CheckNearbyShip()
    {
        Vector3 checkPos = tradeLocation != null ? tradeLocation.position : transform.position;
        var ships = FindObjectsByType<ShipController>(FindObjectsInactive.Exclude);
        foreach (var ship in ships)
        {
            var cargo = ship.GetComponent<CargoSystem>();
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
        for (int i = _contentPanel.childCount - 1; i >= 0; i--)
            Destroy(_contentPanel.GetChild(i).gameObject);
        _itemRows.Clear();

        int index = 0;
        if (_showWarehouseTab)
        {
            if (playerStorage != null)
            {
                foreach (var wItem in playerStorage.warehouse)
                {
                    if (wItem.item == null) continue;
                    MakeRow(wItem.item.displayName, 0, wItem.quantity, index, false);
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
                    MakeRow(mi.item.displayName, mi.currentPrice, mi.availableStock, index, true);
                    index++;
                }
            }
            if (index == 0) MakeEmptyRow("Рынок пуст");
        }
        if (_modeText != null) _modeText.text = _showWarehouseTab ? "[СКЛАД]" : "[РЫНОК]";
    }

    private void MakeRow(string name, float price, int qty, int index, bool isMarket)
    {
        var rowGO = new GameObject($"Row_{index}");
        rowGO.transform.SetParent(_contentPanel, false);
        var rRect = rowGO.AddComponent<RectTransform>();
        rRect.sizeDelta = new Vector2(0, 30);

        var bg = rowGO.AddComponent<Image>();
        bg.color = index % 2 == 0 ? new Color(0.06f, 0.06f, 0.10f) : new Color(0.10f, 0.10f, 0.15f);

        var tGO = new GameObject("Text");
        tGO.transform.SetParent(rowGO.transform, false);
        var tRect = tGO.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = new Vector2(8, 0);
        tRect.offsetMax = new Vector2(-8, 0);
        var t = tGO.AddComponent<Text>();
        t.text = isMarket ? $"{name}  -  {price:F0} CR  (сток: {qty})" : $"{name}  -  {qty} ед.";
        t.fontSize = 13;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleLeft;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var btn = rowGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        int ci = index;
        bool mkt = isMarket;
        btn.onClick.AddListener(() => SelectItem(ci, mkt));
        _itemRows.Add(rowGO);
    }

    private void MakeEmptyRow(string msg)
    {
        var go = new GameObject("EmptyRow");
        go.transform.SetParent(_contentPanel, false);
        var r = go.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(0, 30);
        var t = go.AddComponent<Text>();
        t.text = msg;
        t.fontSize = 13;
        t.color = Color.gray;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void SelectItem(int index, bool isMarket)
    {
        _selectedIndex = index;
        HighlightRow(index);
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
                ShowMessage($"{wi.item.displayName} | {wi.quantity} ед.");
        }
    }

    private void HighlightRow(int index)
    {
        for (int i = 0; i < _contentPanel.childCount; i++)
        {
            var bg = _contentPanel.GetChild(i).GetComponent<Image>();
            if (bg != null)
                bg.color = i == index ? new Color(0.2f, 0.25f, 0.15f) : (i % 2 == 0 ? new Color(0.06f, 0.06f, 0.10f) : new Color(0.10f, 0.10f, 0.15f));
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
                _shipCargoInfoText.text = $"Корабль: {_nearbyCargo.CurrentWeight:F0}/{_nearbyCargo.MaxWeight} кг | {_nearbyCargo.CurrentVolume:F1}/{_nearbyCargo.MaxVolume} m3";
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

        // Left/Right — кол-во
        if (kb.leftArrowKey.wasPressedThisFrame) { _buyQuantity = Mathf.Max(_buyQuantity - 1, 1); UpdateDisplays(); }
        if (kb.rightArrowKey.wasPressedThisFrame) { _buyQuantity = Mathf.Min(_buyQuantity + 1, 99); UpdateDisplays(); }

        // Up/Down — выбор товара
        if (kb.upArrowKey.wasPressedThisFrame)
        {
            int max = (_showWarehouseTab ? (playerStorage?.warehouse.Count ?? 0) : (currentMarket?.items.Count ?? 0)) - 1;
            _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
            if (_selectedIndex > max) _selectedIndex = max;
            HighlightRow(_selectedIndex);
        }
        if (kb.downArrowKey.wasPressedThisFrame)
        {
            int max = (_showWarehouseTab ? (playerStorage?.warehouse.Count ?? 0) : (currentMarket?.items.Count ?? 0)) - 1;
            _selectedIndex = Mathf.Min(max, _selectedIndex + 1);
            HighlightRow(_selectedIndex);
        }

        // Enter — купить
        if (kb.enterKey.wasPressedThisFrame && !kb.leftShiftKey.isPressed && !kb.rightShiftKey.isPressed)
            OnBuyClicked();

        // Shift+Enter — продать
        if (kb.enterKey.wasPressedThisFrame && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            OnSellClicked();

        // B — смена вкладки (Tab занят инвентарём)
        if (kb.bKey.wasPressedThisFrame)
        {
            _showWarehouseTab = !_showWarehouseTab;
            _selectedIndex = 0;
            RenderItems();
            UpdateDisplays();
        }

        // L — погрузить
        if (kb.lKey.wasPressedThisFrame && _nearbyCargo != null && _showWarehouseTab && _selectedIndex >= 0 && playerStorage != null)
            OnLoadClicked();

        // U — разгрузить
        if (kb.uKey.wasPressedThisFrame && _nearbyCargo != null && !_showWarehouseTab && _selectedIndex >= 0 && currentMarket != null && playerStorage != null)
            OnUnloadClicked();

        // Esc — закрыть
        if (kb.escapeKey.wasPressedThisFrame) CloseTrade();

        // R — сбросить кредиты (отладка)
        if (kb.rKey.wasPressedThisFrame && playerStorage != null)
        {
            playerStorage.credits = 1000;
            playerStorage.warehouse.Clear();
            PlayerPrefs.DeleteKey("TradeCredits");
            PlayerPrefs.DeleteKey("TradeWarehouse");
            UpdateDisplays();
            RenderItems();
            ShowMessage("Кредиты сброшены: 1000 CR");
        }
    }

    // ==================== КНОПКИ ====================

    private void OnBuyClicked()
    {
        Debug.Log($"[TradeUI] OnBuyClicked: showWarehouse={_showWarehouseTab}, selIdx={_selectedIndex}, items.Count={currentMarket?.items.Count}");
        if (_showWarehouseTab || _selectedIndex < 0 || currentMarket == null) return;
        if (_selectedIndex >= currentMarket.items.Count) return;
        var mi = currentMarket.items[_selectedIndex];
        Debug.Log($"[TradeUI] Selected item: {mi?.item?.displayName}, price={mi?.currentPrice}, stock={mi?.availableStock}");
        if (mi?.item == null) return;
        BuyItem(mi.item, _buyQuantity);
    }

    private void OnSellClicked()
    {
        if (_showWarehouseTab || _selectedIndex < 0 || currentMarket == null) return;
        if (_selectedIndex >= currentMarket.items.Count) return;
        var mi = currentMarket.items[_selectedIndex];
        if (mi?.item == null) return;
        SellItem(mi.item, _buyQuantity);
    }

    private void OnLoadClicked()
    {
        Debug.Log($"[TradeUI] OnLoadClicked: cargo={_nearbyCargo != null}, storage={playerStorage != null}, warehouseCount={playerStorage?.warehouse.Count}");
        if (_nearbyCargo == null || playerStorage == null || playerStorage.warehouse.Count == 0)
        {
            Debug.Log($"[TradeUI] OnLoadClicked FAILED: нет склада или склада пуст");
            return;
        }
        // Берём товар из склада (независимо от вкладки)
        int idx = _showWarehouseTab ? _selectedIndex : 0;
        if (idx < 0 || idx >= playerStorage.warehouse.Count)
        {
            Debug.Log($"[TradeUI] OnLoadClicked: index {idx} out of range");
            return;
        }
        var item = playerStorage.warehouse[idx]?.item;
        Debug.Log($"[TradeUI] OnLoadClicked: item={item?.displayName}, qty={_buyQuantity}");
        if (item != null) playerStorage.LoadToShip(item.itemId, _buyQuantity, _nearbyCargo);
        UpdateDisplays();
        RenderItems();
    }

    private void OnUnloadClicked()
    {
        if (_showWarehouseTab || _nearbyCargo == null || currentMarket == null || playerStorage == null || _selectedIndex < 0) return;
        if (_selectedIndex >= currentMarket.items.Count) return;
        var item = currentMarket.items[_selectedIndex]?.item;
        if (item != null) playerStorage.UnloadFromShip(item.itemId, _buyQuantity, _nearbyCargo);
        UpdateDisplays();
        RenderItems();
    }

    private void OnCloseClicked() => CloseTrade();

    // ==================== ПОКУПКА / ПРОДАЖА ====================

    private void BuyItem(TradeItemDefinition item, int quantity)
    {
        Debug.Log($"[TradeUI] BuyItem: {item.displayName} x{quantity} @ {item.basePrice} CR");
        Debug.Log($"[TradeUI] currentMarket={currentMarket != null}, playerStorage={playerStorage != null}, credits={playerStorage?.credits}");

        if (currentMarket == null || playerStorage == null) { Debug.LogError("[TradeUI] market или storage null"); return; }

        var mi = currentMarket.items.Find(m => m.item != null && m.item.itemId == item.itemId);
        Debug.Log($"[TradeUI] marketItem found: {mi != null}, stock: {mi?.availableStock}, price: {mi?.currentPrice}");
        if (mi == null || mi.item == null) { Debug.LogError("[TradeUI] товар не найден в рынке"); return; }
        if (mi.availableStock < quantity) { ShowMessage($"Недостаточно товара! Есть {mi.availableStock}"); return; }

        bool ok = playerStorage.BuyItem(item, quantity, mi.currentPrice);
        Debug.Log($"[TradeUI] BuyItem result: {ok}, credits after: {playerStorage.credits}");
        if (!ok) { ShowMessage("Недостаточно кредитов или места!"); return; }

        mi.availableStock -= quantity;
        mi.UpdateDemand(quantity);
        UpdateDisplays();
        RenderItems();
        ShowMessage($"КУПЛЕНО: {item.displayName} x{quantity} за {mi.currentPrice * quantity:F0} CR");
    }

    private void SellItem(TradeItemDefinition item, int quantity)
    {
        if (currentMarket == null || playerStorage == null) return;
        var mi = currentMarket.items.Find(m => m.item != null && m.item.itemId == item.itemId);
        if (mi == null || mi.item == null) return;

        float sellPrice = mi.currentPrice * 0.8f;
        bool ok = playerStorage.SellItem(item, quantity, sellPrice);
        if (!ok) { ShowMessage("Нет товара на складе!"); return; }

        mi.availableStock += quantity;
        mi.UpdateSupply(quantity);
        UpdateDisplays();
        RenderItems();
        ShowMessage($"ПРОДАНО: {item.displayName} x{quantity} за {sellPrice * quantity:F0} CR (80%)");
    }
}
