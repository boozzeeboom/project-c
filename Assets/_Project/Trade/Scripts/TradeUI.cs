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
    private Button _buyBtn;
    private Button _sellBtn;

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
        Debug.Log("[TradeUI] Start() вызван");
        try
        {
            BuildUI();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TradeUI] Ошибка в BuildUI: {e.Message}\n{e.StackTrace}");
        }
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
        Debug.Log("[TradeUI] BuildUI() START");
        
        try
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
        Debug.Log("[TradeUI] BuildUI: Canvas создан");

        // --- Панель ---
        _tradePanel = CreatePanel("TradePanel", _rootCanvas.transform, 0, 0, 520, 640);
        _tradePanel.SetActive(false);
        Debug.Log("[TradeUI] BuildUI: Панель создана");

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

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(vpGO.transform, false);
        _contentPanel = contentGO.transform; // Сохраняем ПОСЛЕ SetParent
        Debug.Log($"[TradeUI] BuildUI: _contentPanel создан, null={_contentPanel == null}");
        
        var contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        Debug.Log($"[TradeUI] BuildUI: после RectTransform, _contentPanel={_contentPanel != null}");
        var layout = contentGO.AddComponent<VerticalLayoutGroup>();
        Debug.Log($"[TradeUI] BuildUI: после VerticalLayoutGroup, _contentPanel={_contentPanel != null}");
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 2;
        layout.padding = new RectOffset(4, 4, 4, 4);

        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Debug.Log($"[TradeUI] BuildUI: после ContentSizeFitter, _contentPanel={_contentPanel != null}");

        scrollGO.AddComponent<Mask>().showMaskGraphic = false;
        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.content = contentRect;
        sr.viewport = vpRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        Debug.Log("[TradeUI] BuildUI: ScrollRect создан");

        // --- Кнопки (внизу панели) ---
        _buyBtn = MakeBtn("BuyBtn", _tradePanel.transform, "КУПИТЬ (Enter)", 0, -80, 240, 36, OnBuyClicked);
        _uiButtons.Add(_buyBtn);
        _sellBtn = MakeBtn("SellBtn", _tradePanel.transform, "ПРОДАТЬ (Shift+Enter)", 0, -125, 280, 36, OnSellClicked);
        _uiButtons.Add(_sellBtn);
        _uiButtons.Add(MakeBtn("LoadBtn", _tradePanel.transform, "ПОГРУЗИТЬ (L)", -130, -175, 240, 36, OnLoadClicked));
        _uiButtons.Add(MakeBtn("UnloadBtn", _tradePanel.transform, "РАЗГРУЗИТЬ (U)", 130, -175, 240, 36, OnUnloadClicked));
        _uiButtons.Add(MakeBtn("CloseBtn", _tradePanel.transform, "ЗАКРЫТЬ (Esc)", 0, -285, 200, 36, OnCloseClicked));

        // --- Сообщение ---
        _messageText = MakeLabel("MsgText", _tradePanel.transform, "Выберите товар и нажмите Enter", 0, -230, 13, new Color(0.9f, 0.9f, 0.4f), 480);
        MakeLabel("Hint1", _tradePanel.transform, "T - склад | Up/Down - выбор | Left/Right - кол-во", 0, -255, 11, Color.grey, 480);
        MakeLabel("Hint2", _tradePanel.transform, "L/U - погрузить/разгрузить | Esc - закрыть | R - сброс", 0, -272, 11, Color.grey, 480);
        
        Debug.Log($"[TradeUI] BuildUI() END — _contentPanel={_contentPanel != null}, _tradePanel={_tradePanel != null}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TradeUI] Ошибка в BuildUI: {e.Message}\n{e.StackTrace}");
        }
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
        if (market == null) return;
        
        Debug.Log($"[TradeUI] OpenTrade() вызван. _tradePanel={( _tradePanel != null ? "Создан" : "null")}");
        
        // Создаём UI если ещё не создан
        if (_tradePanel == null) 
        {
            Debug.Log("[TradeUI] BuildUI() вызывается из OpenTrade()");
            BuildUI();
        }
        if (_tradePanel == null) { Debug.LogError("[TradeUI] Не удалось создать UI!"); return; }
        
        currentMarket = market;
        _isOpen = true;
        _showWarehouseTab = false;
        _selectedIndex = -1;
        if (playerStorage != null) playerStorage.Load();
        CheckNearbyShip();

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
        if (_contentPanel == null) { Debug.LogWarning("[TradeUI] RenderItems: _contentPanel == null!"); return; }
        
        Debug.Log($"[TradeUI] RenderItems: showWarehouse={_showWarehouseTab}, склад={playerStorage?.warehouse.Count ?? 0}, рынок={currentMarket?.items.Count ?? 0}, груз={_nearbyCargo?.cargo.Count ?? 0}");
        
        for (int i = _contentPanel.childCount - 1; i >= 0; i--)
            Destroy(_contentPanel.GetChild(i).gameObject);
        _itemRows.Clear();

        int index = 0;
        if (_showWarehouseTab)
        {
            // === ВКЛАДКА [СКЛАД] — показываем склад игрока + груз корабля ===
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

            // Разделитель
            if (_nearbyCargo != null)
            {
                MakeDividerRow("─── ГРУЗ КОРАБЛЯ ───");
                foreach (var cItem in _nearbyCargo.cargo)
                {
                    if (cItem.item == null) continue;
                    MakeRow(cItem.item.displayName, 0, cItem.quantity, index, false, true);
                    index++;
                }
            }
        }
        else
        {
            // === ВКЛАДКА [РЫНОК] ===
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
        if (_modeText != null) _modeText.text = _showWarehouseTab ? "[СКЛАД + ТРЮМ]" : "[РЫНОК]";
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool showBuySell = !_showWarehouseTab;
        bool showLoadUnload = _showWarehouseTab;

        if (_buyBtn != null) _buyBtn.gameObject.SetActive(showBuySell);
        if (_sellBtn != null) _sellBtn.gameObject.SetActive(showBuySell);

        // Находим кнопки Load/Unload по имени
        foreach (var btn in _uiButtons)
        {
            if (btn == null) continue;
            if (btn.gameObject.name == "LoadBtn") btn.gameObject.SetActive(showLoadUnload);
            if (btn.gameObject.name == "UnloadBtn") btn.gameObject.SetActive(showLoadUnload);
        }
    }

    private void MakeDividerRow(string text)
    {
        var go = new GameObject("DividerRow");
        go.transform.SetParent(_contentPanel, false);
        var r = go.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(0, 22);
        var t = go.AddComponent<Text>();
        t.text = text;
        t.fontSize = 11;
        t.color = Color.cyan;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void MakeRow(string name, float price, int qty, int index, bool isMarket, bool isInCargo = false)
    {
        var rowGO = new GameObject($"Row_{index}");
        rowGO.transform.SetParent(_contentPanel, false);
        var rRect = rowGO.AddComponent<RectTransform>();
        rRect.sizeDelta = new Vector2(0, 30);

        var bg = rowGO.AddComponent<Image>();
        if (isInCargo)
            bg.color = index % 2 == 0 ? new Color(0.12f, 0.08f, 0.04f) : new Color(0.15f, 0.10f, 0.06f);
        else
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
        t.color = isInCargo ? new Color(1f, 0.85f, 0.5f) : Color.white;
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
        else if (_showWarehouseTab && playerStorage != null)
        {
            int warehouseCount = playerStorage.warehouse.Count;
            int cargoStartIndex = warehouseCount + 1;

            if (index < warehouseCount)
            {
                // Товар на складе игрока
                var wi = playerStorage.warehouse[index];
                if (wi?.item != null)
                    ShowMessage($"{wi.item.displayName} | {wi.quantity} ед. (СКЛАД)");
            }
            else if (index >= cargoStartIndex && _nearbyCargo != null)
            {
                // Товар в трюме корабля
                int cargoIdx = index - cargoStartIndex;
                if (cargoIdx >= 0 && cargoIdx < _nearbyCargo.cargo.Count)
                {
                    var ci = _nearbyCargo.cargo[cargoIdx];
                    if (ci?.item != null)
                        ShowMessage($"{ci.item.displayName} | {ci.quantity} ед. (ТРЮМ)");
                }
            }
        }
    }

    private void HighlightRow(int index)
    {
        if (_contentPanel == null) return;
        for (int i = 0; i < _contentPanel.childCount; i++)
        {
            var child = _contentPanel.GetChild(i);
            var bg = child.GetComponent<Image>();
            if (bg == null) continue; // DividerRow без Image

            // Определяем тип строки
            bool isInCargo = child.name.StartsWith("Row_") && _showWarehouseTab && _nearbyCargo != null;
            if (isInCargo)
            {
                int warehouseCount = playerStorage != null ? playerStorage.warehouse.Count : 0;
                int cargoStartIndex = warehouseCount + 1;
                isInCargo = i >= cargoStartIndex;
            }

            if (i == index)
                bg.color = new Color(0.2f, 0.25f, 0.15f); // selected
            else if (isInCargo)
                bg.color = i % 2 == 0 ? new Color(0.12f, 0.08f, 0.04f) : new Color(0.15f, 0.10f, 0.06f);
            else
                bg.color = i % 2 == 0 ? new Color(0.06f, 0.06f, 0.10f) : new Color(0.10f, 0.10f, 0.15f);
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
            int totalRows = _contentPanel != null ? _contentPanel.childCount : 0;
            if (_showWarehouseTab)
            {
                // Пропускаем разделитель
                int warehouseCount = playerStorage != null ? playerStorage.warehouse.Count : 0;
                int dividerIndex = warehouseCount;
                _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
                if (_selectedIndex == dividerIndex) _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
            }
            else
            {
                int max = (currentMarket?.items.Count ?? 0) - 1;
                _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
            }
            HighlightRow(_selectedIndex);
        }
        if (kb.downArrowKey.wasPressedThisFrame)
        {
            int totalRows = _contentPanel != null ? _contentPanel.childCount : 0;
            if (_showWarehouseTab)
            {
                int warehouseCount = playerStorage != null ? playerStorage.warehouse.Count : 0;
                int dividerIndex = warehouseCount;
                int maxIndex = totalRows - 1;
                _selectedIndex = Mathf.Min(maxIndex, _selectedIndex + 1);
                if (_selectedIndex == dividerIndex) _selectedIndex = Mathf.Min(maxIndex, _selectedIndex + 1);
            }
            else
            {
                int max = (currentMarket?.items.Count ?? 0) - 1;
                _selectedIndex = Mathf.Min(max, _selectedIndex + 1);
            }
            HighlightRow(_selectedIndex);
        }

        // Enter — купить
        if (kb.enterKey.wasPressedThisFrame && !kb.leftShiftKey.isPressed && !kb.rightShiftKey.isPressed)
            OnBuyClicked();

        // Shift+Enter — продать
        if (kb.enterKey.wasPressedThisFrame && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            OnSellClicked();

        // T — смена вкладки (B занят инвентарём)
        if (kb.tKey.wasPressedThisFrame)
        {
            _showWarehouseTab = !_showWarehouseTab;
            _selectedIndex = 0;
            Debug.Log($"[TradeUI] Переключение на {(_showWarehouseTab ? "[СКЛАД + ТРЮМ]" : "[РЫНОК]")}, склад предметов: {playerStorage?.warehouse.Count ?? 0}, груз корабля: {_nearbyCargo?.cargo.Count ?? 0}");
            RenderItems();
            UpdateDisplays();
        }

        // L — погрузить (работает с вкладки [СКЛАД])
        if (kb.lKey.wasPressedThisFrame && _nearbyCargo != null && _showWarehouseTab && _selectedIndex >= 0 && playerStorage != null && playerStorage.warehouse.Count > 0)
            OnLoadClicked();

        // U — разгрузить (работает с вкладки [СКЛАД], если рядом корабль)
        if (kb.uKey.wasPressedThisFrame && _nearbyCargo != null && _showWarehouseTab && playerStorage != null && _selectedIndex >= 0)
        {
            // Ищем товар в грузе корабля (пропускаем склад игрока)
            int warehouseCount = playerStorage.warehouse.Count;
            // Пропускаем разделитель
            int cargoStartIndex = warehouseCount + 1;
            if (_selectedIndex >= cargoStartIndex)
            {
                int cargoIdx = _selectedIndex - cargoStartIndex;
                if (cargoIdx >= 0 && cargoIdx < _nearbyCargo.cargo.Count)
                {
                    var cargoItem = _nearbyCargo.cargo[cargoIdx];
                    if (cargoItem?.item != null)
                    {
                        bool ok = playerStorage.UnloadFromShip(cargoItem.item.itemId, _buyQuantity, _nearbyCargo);
                        if (ok) ShowMessage($"РАЗГРУЖЕНО: {cargoItem.item.displayName} x{_buyQuantity}");
                        else ShowMessage("Не хватает места на складе!");
                        UpdateDisplays();
                        RenderItems();
                    }
                }
            }
        }

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
        if (_showWarehouseTab || _selectedIndex < 0 || currentMarket == null) return;
        if (_selectedIndex >= currentMarket.items.Count) return;
        var mi = currentMarket.items[_selectedIndex];
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
        if (_nearbyCargo == null || playerStorage == null) return;
        // Погружаем со склада игрока в трюм корабля
        int idx = _selectedIndex;
        if (idx < 0 || idx >= playerStorage.warehouse.Count) return;
        var item = playerStorage.warehouse[idx]?.item;
        if (item == null) return;
        bool ok = playerStorage.LoadToShip(item.itemId, _buyQuantity, _nearbyCargo);
        if (ok) ShowMessage($"ПОГРУЖЕНО: {item.displayName} x{_buyQuantity}");
        else ShowMessage("Не хватает места в трюме!");
        UpdateDisplays();
        RenderItems();
    }

    private void OnUnloadClicked()
    {
        if (_nearbyCargo == null || playerStorage == null) return;
        // Разгружаем из трюма корабля на склад игрока
        int idx = _selectedIndex;
        if (idx < 0) { ShowMessage("Выберите товар"); return; }
        // Ищем товар в грузе корабля
        if (idx >= _nearbyCargo.cargo.Count) { ShowMessage("Товар не найден в трюме"); return; }
        var cargoItem = _nearbyCargo.cargo[idx];
        if (cargoItem?.item == null) return;
        bool ok = playerStorage.UnloadFromShip(cargoItem.item.itemId, _buyQuantity, _nearbyCargo);
        if (ok) ShowMessage($"РАЗГРУЖЕНО: {cargoItem.item.displayName} x{_buyQuantity}");
        else ShowMessage("Не хватает места на складе!");
        UpdateDisplays();
        RenderItems();
    }

    private void OnCloseClicked() => CloseTrade();

    // ==================== ПОКУПКА / ПРОДАЖА ====================

    private void BuyItem(TradeItemDefinition item, int quantity)
    {
        if (currentMarket == null || playerStorage == null) return;
        var mi = currentMarket.items.Find(m => m.item != null && m.item.itemId == item.itemId);
        if (mi == null || mi.item == null) return;
        if (mi.availableStock < quantity) { ShowMessage($"Недостаточно товара! Есть {mi.availableStock}"); return; }

        bool ok = playerStorage.BuyItem(item, quantity, mi.currentPrice);
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
