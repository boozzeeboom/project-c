using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using ProjectC.Trade;
using ProjectC.Player;
using ProjectC.UI;

/// <summary>
/// Клиентский UI торговли. Сессия 5: Серверная торговля (NGO RPC).
/// Поток: Рынок <-> Склад игрока <-> Трюм корабля
///
/// Мигрировано на TextMeshProUGUI (Спринт 2).
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
    private TextMeshProUGUI _creditsText;
    private TextMeshProUGUI _warehouseInfoText;
    private TextMeshProUGUI _shipCargoInfoText;
    private TextMeshProUGUI _quantityText;
    private TextMeshProUGUI _messageText;
    private TextMeshProUGUI _modeText;

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
            if (_player == null)
                Debug.LogWarning("[TradeUI] NetworkPlayer не найден — торговля недоступна");
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
            Debug.Log("[TradeUI] PlayerTradeStorage не найден на NetworkPlayer — добавляю (однократно)");
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
            var theme = UITheme.Default;

            // --- Root Canvas ---
            _rootCanvas = UIFactory.CreateRootCanvas("[TradeUI]_RootCanvas", theme.TradeUISortingOrder);

            // --- Панель ---
            _tradePanel = UIFactory.CreatePanel("TradePanel", _rootCanvas.transform, 0, 0, 520, 640);
            _tradePanel.SetActive(false);

            // --- Заголовок ---
            MakeLabel("Title", _tradePanel.transform, "ТОРГОВЛЯ", 0, 280, theme.FontSizeSubheading, theme.TextTitle, 480);
            _modeText = MakeLabel("ModeText", _tradePanel.transform, "[РЫНОК]", 0, 252, theme.FontSizeBody, theme.AccentInfo, 200);

            // --- Инфо ---
            _creditsText = MakeLabel("CreditsText", _tradePanel.transform, "Кредиты: 1000 CR", 0, 220, theme.FontSizeBody, theme.TextCredits, 480);
            _warehouseInfoText = MakeLabel("WarehouseInfo", _tradePanel.transform, "Склад: 0/10000 кг | 0/200 m3 | 0/50", 0, 196, theme.FontSizeInfo, theme.TextSecondary, 480);
            _shipCargoInfoText = MakeLabel("ShipCargoInfo", _tradePanel.transform, "Корабль: нет рядом", 0, 176, theme.FontSizeInfo, theme.TextSecondary, 480);

            // --- Кол-во ---
            MakeLabel("QtyLabel", _tradePanel.transform, "Кол-во (< >):", -120, 152, theme.FontSizeButton, theme.TextPrimary, 150);
            _quantityText = MakeLabel("QuantityText", _tradePanel.transform, "1", 140, 152, theme.FontSizeBody, theme.TextPrimary, 60);

            // --- Scroll-зона ---
            var scrollArea = UIFactory.CreateScrollArea(_tradePanel.transform, out RectTransform content);
            var scrollRect2 = scrollArea.GetComponent<RectTransform>();
            scrollRect2.anchorMin = new Vector2(0.04f, 0.19f);
            scrollRect2.anchorMax = new Vector2(0.96f, 0.65f);
            scrollRect2.sizeDelta = new Vector2(0, 0);
            _contentPanel = content;

            // --- Кнопки (внизу панели) ---
            _buyBtn = MakeBtn("BuyBtn", _tradePanel.transform, "КУПИТЬ", 0, -80, 240, 36, OnBuyClicked);
            _uiButtons.Add(_buyBtn);
            _sellBtn = MakeBtn("SellBtn", _tradePanel.transform, "ПРОДАТЬ", 0, -125, 280, 36, OnSellClicked);
            _uiButtons.Add(_sellBtn);
            _uiButtons.Add(MakeBtn("LoadBtn", _tradePanel.transform, "ПОГРУЗИТЬ (L)", -130, -175, 240, 36, OnLoadClicked));
            _uiButtons.Add(MakeBtn("UnloadBtn", _tradePanel.transform, "РАЗГРУЗИТЬ (U)", 130, -175, 240, 36, OnUnloadClicked));
            _uiButtons.Add(MakeBtn("CloseBtn", _tradePanel.transform, "ЗАКРЫТЬ (Esc)", 0, -285, 200, 36, OnCloseClicked));

            // --- Сообщение ---
            _messageText = MakeLabel("MsgText", _tradePanel.transform, "Выберите товар и нажмите КУПИТЬ/ПРОДАТЬ", 0, -230, theme.FontSizeButton, theme.TextMessage, 480);
            MakeLabel("Hint1", _tradePanel.transform, "T - склад | Up/Down - выбор | Left/Right - кол-во", 0, -255, theme.FontSizeCaption, theme.TextMuted, 480);
            MakeLabel("Hint2", _tradePanel.transform, "1-КУПИТЬ 2-ПРОДАТЬ | L/U - погрузить/разгрузить | Esc - закрыть", 0, -272, theme.FontSizeCaption, theme.TextMuted, 480);
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

    // ==================== UI FACTORY WRAPPERS ====================

    // --- Панель ---
    private GameObject CreatePanel(string name, Transform parent, float x, float y, float w, float h)
    {
        return UIFactory.CreatePanel(name, parent, (int)x, (int)y, (int)w, (int)h);
    }

    // --- Текст (TextMeshProUGUI) ---
    private TextMeshProUGUI MakeLabel(string name, Transform parent, string text, float x, float y, int fontSize, Color color, float width)
    {
        return UIFactory.CreateLabel(name, parent, text, (int)x, (int)y, fontSize, color, (int)width);
    }

    // --- Кнопка (TextMeshProUGUI внутри) ---
    private Button MakeBtn(string name, Transform parent, string label, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick)
    {
        return UIFactory.CreateButton(name, parent, label, onClick, new Vector2((int)w, (int)h), (int)x, (int)y);
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

        // Регистрируем в UIManager
        UIManager.EnsureExists().OpenPanel("TradeUI", 200, OnTradePanelClosed, _tradePanel);

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
            // Сессия 8F: Загружаем из PlayerDataStore (единый источник)
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            playerStorage.LoadFromPlayerDataStore(clientId);
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

        // Закрываем через UIManager
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClosePanel("TradeUI");
        }
        else
        {
            // Fallback если UIManager недоступен
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Сессия 8: Убрал Save() из CloseTrade — сохранение происходит при модификации данных (BuyItem, SellItem, LoadToShip, UnloadFromShip)
        // Save() здесь был проблемой: он перезаписывал данные склада пустыми данными при закрытии

        // Разблокируем ввод игрока
        // if (_player != null) _player.InputLocked = false;

        ShowMessage("");
    }

    /// <summary>
    /// Callback при закрытии торговой панели (вызывается из UIManager)
    /// </summary>
    private void OnTradePanelClosed()
    {
        Debug.Log("[TradeUI] Панель торговли закрыта через UIManager");
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
        var theme = UITheme.Default;
        UIFactory.CreateDividerRow(_contentPanel, text, theme.FontSizeCaption, theme.AccentInfo);
    }

    private void MakeCargoRow(string name, int qty, int index)
    {
        var theme = UITheme.Default;
        var rowGO = UIFactory.CreateListRow(_contentPanel, $"{name}  -  {qty} ед.", theme.TextCargo, index, isCargo: true);

        var btn = rowGO.GetComponent<Button>();
        if (btn == null) btn = rowGO.AddComponent<Button>();
        var bg = rowGO.GetComponent<Image>();
        if (bg != null) btn.targetGraphic = bg;
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
        var theme = UITheme.Default;
        string rowText = isMarket ? $"{name}  -  {price:F0} CR  (сток: {qty})" : $"{name}  -  {qty} ед.";
        Color textColor = isInCargo ? theme.TextCargo : theme.TextPrimary;

        var rowGO = UIFactory.CreateListRow(_contentPanel, rowText, textColor, index, isMarket: isMarket, isCargo: isInCargo);

        var btn = rowGO.GetComponent<Button>();
        if (btn == null) btn = rowGO.AddComponent<Button>();
        var bg = rowGO.GetComponent<Image>();
        if (bg != null) btn.targetGraphic = bg;
        int ci = index;
        bool mkt = isMarket;
        btn.onClick.AddListener(() => SelectItem(ci, mkt));
        _itemRows.Add(rowGO);
    }

    private void MakeEmptyRow(string msg)
    {
        UIFactory.CreateEmptyRow(_contentPanel, msg);
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
        var theme = UITheme.Default;
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
                bg.color = theme.SelectedRow;
            }
            else if (isCargoRow)
            {
                bg.color = i % 2 == 0 ? theme.CargoRowEven : theme.CargoRowOdd;
            }
            else if (isWarehouseRow)
            {
                bg.color = theme.GetMarketRowColor(i);
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
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                playerStorage.LoadFromPlayerDataStore(clientId);
            }
        }

        if (_creditsText != null && playerStorage != null)
        {
            _creditsText.text = $"Кредиты: {playerStorage.credits:F0} CR";
        }

        if (_warehouseInfoText != null && playerStorage != null)
            _warehouseInfoText.text = $"Склад: {playerStorage.CurrentWeight:F0}/{playerStorage.maxWeight} кг | {playerStorage.CurrentVolume:F1}/{playerStorage.maxVolume} m3 | {playerStorage.warehouse.Count}/{playerStorage.maxItemTypes}";

        if (_shipCargoInfoText != null)
        {
            var theme = UITheme.Default;
            if (_nearbyCargo != null)
            {
                _shipCargoInfoText.text = $"Корабль: {_nearbyCargo.CurrentWeight:F0}/{_nearbyCargo.MaxWeight} кг | {_nearbyCargo.CurrentVolume:F1}/{_nearbyCargo.MaxVolume} m3";
                _shipCargoInfoText.color = theme.TextTitle;
            }
            else
            {
                _shipCargoInfoText.text = "Корабль: нет рядом";
                _shipCargoInfoText.color = theme.TextMuted;
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

    /// <summary>
    /// Обработать результат контракта (когда ContractBoardUI закрыт)
    /// Сессия 8E: Обновляем кредиты из авторитетного источника TradeMarketServer
    /// </summary>
    public void OnContractResult(bool success, string message, float reward)
    {
        ShowMessage(message);
        if (success && playerStorage != null)
        {
            // Сессия 8F: Загружаем актуальные данные из PlayerDataStore
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            playerStorage.LoadFromPlayerDataStore(clientId);
            UpdateDisplays();
        }
    }

    // ==================== ВВОД ====================

    private void HandleInput()
    {
        // Проверяем что эта панель может получать ввод (она верхняя)
        if (!UIManager.EnsureExists().CanReceiveInput("TradeUI")) return;

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
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                playerStorage.LoadFromPlayerDataStore(clientId);
            }
        }

        if (success)
        {
            ShowMessage(message);
            if (playerStorage != null)
            {
                // Сессия 8F: Загружаем склад из PlayerDataStore
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                playerStorage.LoadFromPlayerDataStore(clientId);

                // Переопределяем кредиты значением от сервера
                // Сервер шлёт актуальные newCredits через ClientRpc
                playerStorage.credits = newCredits;

                // Сохраняем чтобы новые кредиты не потерялись
                playerStorage.Save();

                // Обновляем UI — КРИТИЧНО: должен быть после Save()
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

        ShowMessage($"[Событие] {displayName}! Длительность: {durationTicks} тиков");

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

        ShowMessage($"[Событие] '{displayName}' окончилось. Цены возвращаются к норме.");

        RenderItems();
        UpdateDisplays();
    }
}
