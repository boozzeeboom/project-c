using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ProjectC.Trade;
using ProjectC.Player;

/// <summary>
/// Клиентский UI торговли. Сессия 5: Серверная торговля (NGO RPC).
/// Поток: Рынок <-> Склад игрока <-> Трюм корабля
///
/// Простой UI через UnityEngine.UI.Text (без TMP).
/// Все торговые операции идут через NetworkPlayer → ServerRpc → сервер авторитетен.
/// </summary>
public class TradeUI : MonoBehaviour
{
    public static TradeUI Instance { get; private set; }
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

    /// <summary>
    /// Получить NetworkPlayer — ленивый поиск, т.к. в Awake() объект ещё не заспавнен сетью
    /// </summary>
    private NetworkPlayer Player
    {
        get
        {
            if (_player == null)
                _player = FindAnyObjectByType<NetworkPlayer>();
            return _player;
        }
    }

    /// <summary>
    /// Получить PlayerTradeStorage — берём с NetworkPlayer (тот же что у сервера), а не FindObjectOfType.
    /// Сессия 8C: клиент и сервер работают с одним и тем же хранилищем.
    /// </summary>
    private PlayerTradeStorage GetPlayerStorageFromNetworkPlayer()
    {
        if (Player == null) return null;
        var storage = Player.GetComponent<PlayerTradeStorage>();
        if (storage == null)
        {
            storage = Player.gameObject.AddComponent<PlayerTradeStorage>();
        }
        return storage;
    }

    private void Awake()
    {
        if (playerStorage == null)
            playerStorage = FindAnyObjectByType<PlayerTradeStorage>();

        // Singleton
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DestroyUI();
    }

    private void Update()
    {
        if (!_isOpen) return;
        HandleInput();
    }

    private void BuildUI()
    {
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

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(vpGO.transform, false);
        _contentPanel = contentGO.transform;

        var contentRect = contentGO.GetComponent<RectTransform>();
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
        _buyBtn = MakeBtn("BuyBtn", _tradePanel.transform, "КУПИТЬ", 0, -80, 240, 36, OnBuyClicked);
        _uiButtons.Add(_buyBtn);
        _sellBtn = MakeBtn("SellBtn", _tradePanel.transform, "ПРОДАТЬ", 0, -125, 280, 36, OnSellClicked);
        _uiButtons.Add(_sellBtn);
        _uiButtons.Add(MakeBtn("LoadBtn", _tradePanel.transform, "ПОГРУЗИТЬ (L)", -130, -175, 240, 36, OnLoadClicked));
        _uiButtons.Add(MakeBtn("UnloadBtn", _tradePanel.transform, "РАЗГРУЗИТЬ (U)", 130, -175, 240, 36, OnUnloadClicked));
        _uiButtons.Add(MakeBtn("CloseBtn", _tradePanel.transform, "ЗАКРЫТЬ (Esc)", 0, -285, 200, 36, OnCloseClicked));

        // --- Сообщение ---
        _messageText = MakeLabel("MsgText", _tradePanel.transform, "Выберите товар и нажмите КУПИТЬ/ПРОДАТЬ", 0, -230, 13, new Color(0.9f, 0.9f, 0.4f), 480);
        MakeLabel("Hint1", _tradePanel.transform, "T - склад | Up/Down - выбор | Left/Right - кол-во", 0, -255, 11, Color.grey, 480);
        MakeLabel("Hint2", _tradePanel.transform, "1-КУПИТЬ 2-ПРОДАТЬ | L/U - погрузить/разгрузить | Esc - закрыть", 0, -272, 11, Color.grey, 480);
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

        // Создаём UI если ещё не создан
        if (_tradePanel == null)
        {
            BuildUI();
        }
        if (_tradePanel == null) { Debug.LogError("[TradeUI] Не удалось создать UI!"); return; }

        currentMarket = market;
        _isOpen = true;
        _showWarehouseTab = false;
        _selectedIndex = -1;

        // Сессия 8C: используем PlayerTradeStorage с NetworkPlayer (тот же что у сервера)
        // вместо FindObjectOfType — чтобы клиент и сервер работали с одним хранилищем
        PlayerTradeStorage storage = GetPlayerStorageFromNetworkPlayer();
        if (storage == null)
        {
            // Fallback: ищем в сцене (для совместимости)
            storage = playerStorage != null ? playerStorage : FindAnyObjectByType<PlayerTradeStorage>();
        }
        playerStorage = storage; // Сохраняем ссылку для всего UI

        // Устанавливаем локацию склада перед загрузкой
        if (playerStorage != null)
        {
            if (!string.IsNullOrEmpty(market.locationId))
            {
                playerStorage.currentLocationId = market.locationId;
            }
            else if (!string.IsNullOrEmpty(market.locationName))
            {
                Debug.LogWarning($"[TradeUI] market.locationId пустой! Использую '{market.locationName}' как fallback");
                playerStorage.currentLocationId = market.locationName;
            }
            else
            {
                Debug.LogWarning($"[TradeUI] market.locationId И locationName пустые! Проверь ScriptableObject LocationMarket");
            }
            playerStorage.Load();
        }
        CheckNearbyShip();

        // Автовыбор первого товара
        _selectedIndex = 0;

        _tradePanel.SetActive(true);
        _tradePanel.transform.SetAsLastSibling();
        RenderItems();
        UpdateDisplays();
    }

    public void CloseTrade()
    {
        _isOpen = false;
        _selectedIndex = -1;
        if (_tradePanel != null) _tradePanel.SetActive(false);
        // Сессия 8: Убрал Save() из CloseTrade — сохранение происходит при модификации данных (BuyItem, SellItem, LoadToShip, UnloadFromShip)
        // Save() здесь был проблемой: он перезаписывал данные склада пустыми данными при закрытии

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

    public void RenderItems()
    {
        if (_contentPanel == null) { Debug.LogWarning("[TradeUI] RenderItems: _contentPanel == null!"); return; }

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
                    // Явно создаём строку груза с Button для кликабельности
                    MakeCargoRow(cItem.item.displayName, cItem.quantity, index);
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
        
        // Принудительный пересчёт layout
        if (_contentPanel != null)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_contentPanel as RectTransform);
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

    private void MakeCargoRow(string name, int qty, int index)
    {
        var rowGO = new GameObject($"CargoRow_{index}");
        rowGO.transform.SetParent(_contentPanel, false);
        var rRect = rowGO.AddComponent<RectTransform>();
        rRect.anchorMin = Vector2.zero;
        rRect.anchorMax = Vector2.one;
        rRect.sizeDelta = Vector2.zero;

        var layoutElem = rowGO.AddComponent<UnityEngine.UI.LayoutElement>();
        layoutElem.preferredHeight = 30f;

        var bg = rowGO.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.08f, 0.04f);

        var tGO = new GameObject("Text");
        tGO.transform.SetParent(rowGO.transform, false);
        var tRect = tGO.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = new Vector2(8, 0);
        tRect.offsetMax = new Vector2(-8, 0);
        var t = tGO.AddComponent<Text>();
        t.text = $"{name}  -  {qty} ед.";
        t.fontSize = 13;
        t.color = new Color(1f, 0.85f, 0.5f);
        t.alignment = TextAnchor.MiddleLeft;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var btn = rowGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        int ci = index;
        btn.onClick.AddListener(() => SelectCargoItem(ci));
        _itemRows.Add(rowGO);
    }

    private void SelectCargoItem(int index)
    {
        _selectedIndex = index;
        HighlightRow(index);
        if (_nearbyCargo != null)
        {
            int warehouseCount = playerStorage != null ? playerStorage.warehouse.Count : 0;
            // Divider does NOT increment index, so cargo starts at warehouseCount
            int cargoIdx = index - warehouseCount;
            if (cargoIdx >= 0 && cargoIdx < _nearbyCargo.cargo.Count)
            {
                var ci = _nearbyCargo.cargo[cargoIdx];
                if (ci?.item != null)
                    ShowMessage($"{ci.item.displayName} | {ci.quantity} ед. (ТРЮМ) | Нажмите U для разгрузки");
            }
        }
    }

    private void MakeRow(string name, float price, int qty, int index, bool isMarket, bool isInCargo = false)
    {
        var rowGO = new GameObject($"Row_{index}");
        rowGO.transform.SetParent(_contentPanel, false);
        var rRect = rowGO.AddComponent<RectTransform>();
        rRect.anchorMin = Vector2.zero;
        rRect.anchorMax = Vector2.one;
        rRect.sizeDelta = Vector2.zero;
        
        // Явная высота через LayoutElement
        var layoutElem = rowGO.AddComponent<UnityEngine.UI.LayoutElement>();
        layoutElem.preferredHeight = 30f;

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
            int cargoStartIndex = warehouseCount + 1; // +1 для разделителя

            if (index < warehouseCount)
            {
                // Товар на складе игрока
                var wi = playerStorage.warehouse[index];
                if (wi?.item != null)
                    ShowMessage($"{wi.item.displayName} | {wi.quantity} ед. (СКЛАД) | Нажмите L для погрузки");
            }
            else if (index >= cargoStartIndex && _nearbyCargo != null)
            {
                // Товар в трюме корабля
                int cargoIdx = index - cargoStartIndex;
                if (cargoIdx >= 0 && cargoIdx < _nearbyCargo.cargo.Count)
                {
                    var ci = _nearbyCargo.cargo[cargoIdx];
                    if (ci?.item != null)
                        ShowMessage($"{ci.item.displayName} | {ci.quantity} ед. (ТРЮМ) | Нажмите U для разгрузки");
                }
            }
            else
            {
                ShowMessage("Выберите товар из списка");
            }
        }
    }

    private void HighlightRow(int index)
    {
        if (_contentPanel == null) return;
        int warehouseCount = playerStorage != null ? playerStorage.warehouse.Count : 0;
        int cargoStartIndex = warehouseCount + 1;

        for (int i = 0; i < _contentPanel.childCount; i++)
        {
            var child = _contentPanel.GetChild(i);
            var bg = child.GetComponent<Image>();
            if (bg == null) continue; // DividerRow без Image

            bool isCargoRow = child.name.StartsWith("CargoRow_");
            bool isWarehouseRow = child.name.StartsWith("Row_");

            if (i == index)
            {
                bg.color = new Color(0.2f, 0.25f, 0.15f); // selected (green)
            }
            else if (isCargoRow)
            {
                bg.color = new Color(0.12f, 0.08f, 0.04f); // cargo default
            }
            else if (isWarehouseRow)
            {
                bg.color = i % 2 == 0 ? new Color(0.06f, 0.06f, 0.10f) : new Color(0.10f, 0.10f, 0.15f);
            }
        }
    }

    // ==================== ДИСПЛЕИ ====================

    public void UpdateDisplays()
    {
        // КРИТИЧНО: гарантируем что playerStorage инициализирован
        if (playerStorage == null)
        {
            playerStorage = GetPlayerStorageFromNetworkPlayer();
            if (playerStorage != null)
            {
                // Устанавливаем локацию перед использованием
                if (currentMarket != null && !string.IsNullOrEmpty(currentMarket.locationId))
                {
                    playerStorage.currentLocationId = currentMarket.locationId;
                }
                playerStorage.Load();
            }
        }

        if (_creditsText != null && playerStorage != null)
        {
            _creditsText.text = $"Кредиты: {playerStorage.credits:F0} CR";
            Debug.Log($"[TradeUI] UpdateDisplays: credits={playerStorage.credits:F0}");
        }

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

    /// <summary>
    /// Публичный метод для отображения сообщения (используется NetworkPlayer)
    /// Сессия 7: ContractSystem.
    /// </summary>
    public void ShowMessagePublic(string msg)
    {
        ShowMessage(msg);
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

        // Сессия 8C: Enter/Shift+Enter УБРАНЫ — вызывали двойные RPC вместе с Button.onClick
        // Сессия 8D: 1 — купить, 2 — продать (альтернатива клику мышью по кнопкам)
        if (kb.digit1Key.wasPressedThisFrame || (kb.numpad1Key != null && kb.numpad1Key.wasPressedThisFrame))
        {
            TryBuyItem();
        }
        if (kb.digit2Key.wasPressedThisFrame || (kb.numpad2Key != null && kb.numpad2Key.wasPressedThisFrame))
        {
            TrySellItem();
        }

        // T — смена вкладки (B занят инвентарём)
        if (kb.tKey.wasPressedThisFrame)
        {
            _showWarehouseTab = !_showWarehouseTab;
            _selectedIndex = 0;
            RenderItems();
            UpdateDisplays();
        }

        // L — погрузить (работает с вкладки [СКЛАД])
        if (kb.lKey.wasPressedThisFrame && _nearbyCargo != null && _showWarehouseTab && _selectedIndex >= 0 && playerStorage != null && playerStorage.warehouse.Count > 0)
            OnLoadClicked();

        // U — разгрузить (работает с вкладки [СКЛАД], если рядом корабль)
        // Divider НЕ увеличивает index, поэтому груз начинается с warehouseCount
        if (kb.uKey.wasPressedThisFrame && _nearbyCargo != null && _showWarehouseTab && playerStorage != null && _selectedIndex >= 0)
        {
            int warehouseCount = playerStorage.warehouse.Count;

            if (_selectedIndex < warehouseCount)
            {
                ShowMessage("Выберите товар из секции ГРУЗ КОРАБЛЯ!");
            }
            else
            {
                int cargoIdx = _selectedIndex - warehouseCount;
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

        // R — сбросить кредиты (отладка, только текущая локация)
        if (kb.rKey.wasPressedThisFrame && playerStorage != null)
        {
            string locKey = string.IsNullOrEmpty(playerStorage.currentLocationId) ? "global" : playerStorage.currentLocationId.ToLower();
            playerStorage.credits = 1000;
            playerStorage.warehouse.Clear();
            PlayerPrefs.DeleteKey($"TradeCredits_{locKey}");
            PlayerPrefs.DeleteKey($"TradeWarehouse_{locKey}");
            UpdateDisplays();
            RenderItems();
            ShowMessage("Кредиты сброшены: 1000 CR");
        }
    }

    private float _lastTradeTime = 0f;
    private const float TRADE_COOLDOWN = 0.5f; // Дебаунс — защита от двойного клика

    // Сессия 8D: Флаг блокировки сбрасывается ТОЛЬКО при получении ответа от сервера
    // а не через Invoke — это гарантирует защиту от двойных RPC
    private bool _tradeLocked = false;

    // ==================== КНОПКИ ====================
    // Сессия 8D: Кнопки Buy/Sell ТОЛЬКО для визуала — клик обрабатывается через HandleInput
    // Это гарантированно исключает двойной вызов (onClick + keyboard в одном кадре)

    private void OnBuyClicked()
    {
        // Сессия 8D: Перенаправляем на TryBuyItem — вся защита там
        TryBuyItem();
    }

    private void OnSellClicked()
    {
        // Сессия 8D: Перенаправляем на TrySellItem — вся защита там
        TrySellItem();
    }

    /// <summary>
    /// Попытка покупки — единая точка входа с надёжной защитой от двойных вызовов
    /// Сессия 8D: _tradeLocked сбрасывается ТОЛЬКО в OnTradeResult()
    /// </summary>
    private void TryBuyItem()
    {
        // Блокировка — сбрасывается ТОЛЬКО при получении ответа от сервера
        if (_tradeLocked)
        {
            Debug.LogWarning("[TradeUI] Покупка заблокирована — ожидание ответа от сервера");
            return;
        }
        if (Time.time - _lastTradeTime < TRADE_COOLDOWN) return;

        if (_showWarehouseTab || _selectedIndex < 0 || currentMarket == null) return;
        if (_selectedIndex >= currentMarket.items.Count) return;
        var mi = currentMarket.items[_selectedIndex];
        if (mi?.item == null) { ShowMessage("Выберите товар!"); return; }

        // Устанавливаем блокировку ПЕРЕД отправкой RPC
        _tradeLocked = true;
        _lastTradeTime = Time.time;
        Debug.Log($"[TradeUI] Покупка: {mi.item.displayName} x{_buyQuantity} (index={_selectedIndex})");

        BuyItemViaServer(mi.item.itemId, _buyQuantity);
    }

    /// <summary>
    /// Попытка продажи — единая точка входа с надёжной защитой
    /// </summary>
    private void TrySellItem()
    {
        if (_tradeLocked)
        {
            Debug.LogWarning("[TradeUI] Продажа заблокирована — ожидание ответа от сервера");
            return;
        }
        if (Time.time - _lastTradeTime < TRADE_COOLDOWN) return;

        if (_showWarehouseTab || _selectedIndex < 0 || currentMarket == null) return;
        if (_selectedIndex >= currentMarket.items.Count) return;
        var mi = currentMarket.items[_selectedIndex];
        if (mi?.item == null) { ShowMessage("Выберите товар!"); return; }

        _tradeLocked = true;
        _lastTradeTime = Time.time;
        Debug.Log($"[TradeUI] Продажа: {mi.item.displayName} x{_buyQuantity} (index={_selectedIndex})");

        SellItemViaServer(mi.item.itemId, _buyQuantity);
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
        int idx = _selectedIndex;
        if (idx < 0) { ShowMessage("Выберите товар"); return; }

        // Divider НЕ увеличивает index, поэтому груз начинается с warehouseCount
        int warehouseCount = playerStorage.warehouse.Count;

        if (idx < warehouseCount)
        {
            ShowMessage("Выберите товар из секции ГРУЗ КОРАБЛЯ!");
            return;
        }

        int cargoIdx = idx - warehouseCount;
        if (cargoIdx < 0 || cargoIdx >= _nearbyCargo.cargo.Count)
        {
            ShowMessage("Товар не найден в трюме");
            return;
        }

        var cargoItem = _nearbyCargo.cargo[cargoIdx];
        if (cargoItem?.item == null) return;

        bool ok = playerStorage.UnloadFromShip(cargoItem.item.itemId, _buyQuantity, _nearbyCargo);
        if (ok) ShowMessage($"РАЗГРУЖЕНО: {cargoItem.item.displayName} x{_buyQuantity}");
        else ShowMessage("Не хватает места на складе!");
        UpdateDisplays();
        RenderItems();
    }

    private void OnCloseClicked() => CloseTrade();

    // ==================== ПОКУПКА / ПРОДАЖА (СЕРВЕРНАЯ ЧЕРЕЗ RPC) ====================

    private void BuyItemViaServer(string itemId, int quantity)
    {
        // Отправляем RPC через NetworkPlayer — сервер авторитетен, fallback запрещён
        if (Player != null)
        {
            Player.TradeBuyServerRpc(itemId, quantity, currentMarket.locationId);
        }
        else
        {
            ShowMessage("ОШИБКА: NetworkPlayer не найден. Попробуйте перезайти.");
            Debug.LogError("[TradeUI] NetworkPlayer не найден — невозможно выполнить покупку");
        }
    }

    private void SellItemViaServer(string itemId, int quantity)
    {
        if (Player != null)
        {
            Player.TradeSellServerRpc(itemId, quantity, currentMarket.locationId);
        }
        else
        {
            ShowMessage("ОШИБКА: NetworkPlayer не найден. Попробуйте перезайти.");
            Debug.LogError("[TradeUI] NetworkPlayer не найден — невозможно выполнить продажу");
        }
    }

    /// <summary>
    /// Вызывается из TradeResultClientRpc (NetworkPlayer) с результатом торговли
    /// Сессия 8C: добавлена синхронизация предметов — сервер передаёт itemId и quantity при успешной покупке/продаже
    /// Сессия 8D: сброс _tradeLocked — разрешение на следующую операцию
    /// Сессия 8D hotfix: загружаем актуальные данные с сервера вместо ручного добавления/удаления
    /// </summary>
    public void OnTradeResult(bool success, string message, float newCredits, string itemId = "", int itemQuantity = 0, bool isPurchase = true)
    {
        // Сессия 8D: Сброс блокировки — сервер ответил, можно продолжать
        _tradeLocked = false;

        Debug.Log($"[TradeUI] OnTradeResult: success={success}, newCredits={newCredits:F0}, itemId={itemId}, qty={itemQuantity}, isPurchase={isPurchase}");

        // КРИТИЧНО: playerStorage мог не инициализироваться если OpenTrade() ещё не вызывался
        // Гарантируем что playerStorage всегда валиден перед работой с кредитами
        if (playerStorage == null)
        {
            playerStorage = GetPlayerStorageFromNetworkPlayer();
            if (playerStorage != null)
            {
                // Устанавливаем локацию перед загрузкой
                if (currentMarket != null && !string.IsNullOrEmpty(currentMarket.locationId))
                {
                    playerStorage.currentLocationId = currentMarket.locationId;
                }
                playerStorage.Load();
            }
        }

        if (success)
        {
            ShowMessage(message);
            if (playerStorage != null)
            {
                // Сессия 8E: Порядок критично важен!
                // 1. Загружаем склад (сервер уже сохранил обновлённый warehouse)
                playerStorage.Load();

                // 2. Переопределяем кредиты значением от сервера
                // Load() загружает из PlayerPrefs где может быть старое значение credits
                // Сервер шлёт актуальные newCredits через ClientRpc
                playerStorage.credits = newCredits;

                // 3. Сохраняем чтобы новые creditы не потерялись
                playerStorage.Save();

                // 4. Обновляем UI — КРИТИЧНО: должен быть после Save()
                UpdateDisplays();
                RenderItems();
            }
            else
            {
                // playerStorage всё ещё null — логгируем ошибку
                Debug.LogError("[TradeUI] OnTradeResult: playerStorage == null! Кредиты НЕ обновлены в UI!");
            }
        }
        else
        {
            ShowMessage($"ОШИБКА: {message}");
            // При ошибке тоже обновляем UI если playerStorage доступен
            if (playerStorage != null)
            {
                UpdateDisplays();
                RenderItems();
            }
        }
    }

    /// <summary>
    /// Синхронизировать предмет на складе игрока после серверной операции
    /// Сессия 8C: обеспечивает отображение товаров, добавленных сервером при покупке
    /// Сессия 8D: вызываем Save() чтобы данные не терялись при следующем Load()
    /// </summary>
    private void SyncWarehouseItem(string itemId, int quantity)
    {
        if (playerStorage == null) return;

        var db = FindTradeDatabase();
        if (db == null) return;

        var itemDef = db.GetItemById(itemId);
        if (itemDef == null)
        {
            Debug.LogWarning($"[TradeUI] Не найден TradeItemDefinition для {itemId}");
            return;
        }

        var existing = playerStorage.warehouse.Find(w => w.item != null && w.item.itemId == itemId);
        if (existing != null)
        {
            existing.quantity += quantity;
        }
        else
        {
            playerStorage.warehouse.Add(new WarehouseItem { item = itemDef, quantity = quantity });
        }

        // Сессия 8D: Сохраняем чтобы данные не терялись
        playerStorage.Save();
    }

    /// <summary>
    /// Удалить предмет из клиентского склада после успешной серверной продажи
    /// Сессия 8C: клиентский склад должен совпадать с серверным
    /// Сессия 8D: вызываем Save() чтобы данные не терялись
    /// </summary>
    private void RemoveFromWarehouse(string itemId, int quantity)
    {
        if (playerStorage == null) return;

        var wi = playerStorage.warehouse.Find(w => w.item != null && w.item.itemId == itemId);
        if (wi != null)
        {
            wi.quantity -= quantity;
            if (wi.quantity <= 0)
            {
                playerStorage.warehouse.Remove(wi);
            }

            // Сессия 8D: Сохраняем после модификации
            playerStorage.Save();
        }
        else
        {
            Debug.LogWarning($"[TradeUI] RemoveFromWarehouse: {itemId} не найден на клиентском складе!");
        }
    }

    private static TradeDatabase FindTradeDatabase()
    {
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TradeDatabase");
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<TradeDatabase>(path);
        }
#endif
        return Resources.Load<TradeDatabase>("Trade/TradeItemDatabase");
    }

    // ==================== ЛОКАЛЬНАЯ ТОРГОВЛЯ (ОТКЛЮЧЕНО — Сессия 8C) ====================
    //Fallback удалён: все операции должны идти через сервер.
    // Эти методы оставлены только для отладки/референса.

    /*
    private void BuyItemLocal(string itemId, int quantity)
    {
        // ОТКЛЮЧЕНО: локальная покупка обходит сервер, контракты не видят товар
        Debug.LogError("[TradeUI] BuyItemLocal вызван — это ошибка! Все покупки должны идти через сервер.");
        // ... старый код ...
    }

    private void SellItemLocal(string itemId, int quantity)
    {
        // ОТКЛЮЧЕНО: локальная продажа обходит сервер
        Debug.LogError("[TradeUI] SellItemLocal вызван — это ошибка! Все продажи должны идти через сервер.");
        // ... старый код ...
    }
    */

    // ==================== СОБЫТИЯ РЫНКА (Сессия 6) ====================

    private Dictionary<string, string> _activeEventDisplayNames = new Dictionary<string, string>();
    private Dictionary<string, string> _activeEventIcons = new Dictionary<string, string>();

    /// <summary>
    /// Вызывается из BroadcastEventClientRpc при начале события
    /// </summary>
    public void OnMarketEventStarted(string eventId, string displayName, string displayIcon, int durationTicks, string affectedItemId)
    {
        _activeEventDisplayNames[eventId] = displayName;
        _activeEventIcons[eventId] = displayIcon;

        ShowMessage($"📢 {displayIcon} {displayName}! Длительность: {durationTicks} тиков");

        // Перерендерим рынок чтобы показать изменённые цены
        RenderItems();
        UpdateDisplays();
    }

    /// <summary>
    /// Вызывается из RemoveEventClientRpc при окончании события
    /// </summary>
    public void OnMarketEventEnded(string eventId)
    {
        string displayName = _activeEventDisplayNames.ContainsKey(eventId)
            ? _activeEventDisplayNames[eventId] : eventId;

        _activeEventDisplayNames.Remove(eventId);
        _activeEventIcons.Remove(eventId);

        ShowMessage($"📢 Событие '{displayName}' окончилось. Цены возвращаются к норме.");

        RenderItems();
        UpdateDisplays();
    }
}
