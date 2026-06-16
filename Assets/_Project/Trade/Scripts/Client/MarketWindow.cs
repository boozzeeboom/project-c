using System;
using System.Collections.Generic;
using ProjectC.Trade;
using ProjectC.Trade.Dto;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Trade.Core;
using ProjectC.Trade.Network;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// UI Toolkit контроллер окна рынка. Держит VisualTreeAsset из UXML,
    /// подписывается на <see cref="MarketClientState"/>, проецирует
    /// snapshot в UI и шлёт команды через MarketClientState.RequestXxx().
    ///
    /// Требования к сцене:
    ///   • GameObject с этим компонентом
    ///   • Соседний UIDocument с PanelSettings (создать в Editor)
    ///   • UXML файл в Resources/UI/MarketWindow.uxml
    ///   • USS файл в Resources/UI/MarketWindow.uss
    ///
    /// Создание PanelSettings: правый клик в Assets/_Project/UI/
    /// → Create → UI Toolkit → Panel Settings Asset
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MarketWindow : MonoBehaviour
    {
        public static MarketWindow Instance { get; private set; }

        [Header("UI Assets (можно Resources fallback)")]
        [SerializeField] private VisualTreeAsset marketWindowUxml;
        [SerializeField] private StyleSheet marketWindowUss;

        [Header("Behavior")]
        [SerializeField] private bool visibleOnStart = false;

        // === Runtime refs ===
        private UIDocument _doc;
        private VisualElement _root;
        private MarketClientState _state;

        // UI element refs
        private VisualElement _mainContainer;
        private Label _locationLabel;
        private Label _creditsLabel;
        private Label _warehouseInfoLabel;
        private Label _timeInfoLabel;
        private ListView _itemList;
        private ListView _warehouseList;
        private ListView _cargoList;
        // C2-refactor: 3-й таб КОНТРАКТЫ (CONTRACTS_AS_MARKET_TAB_REFACTOR.md)
        private ListView _contractsList;
        private VisualElement _itemSection;        // FIX: wrapper вокруг item-list + title
        private VisualElement _warehouseSection;   // FIX: wrapper вокруг warehouse-list + title
        private VisualElement _cargoSection;       // FIX: wrapper вокруг cargo-list + title
        private VisualElement _contractsSection;   // C2-refactor
        private VisualElement _shipSelectorContainer;
        private DropdownField _shipSelector;
        private Button _buyBtn;
        private Button _sellBtn;
        private Button _loadBtn;
        private Button _unloadBtn;
        private Button _closeBtn;
        // C2-refactor: 3 кнопки для таба КОНТРАКТЫ
        private Button _acceptBtn;
        private Button _completeBtn;
        private Button _failBtn;
        private Label _messageLabel;
        private TextField _qtyField;

        // State
        private int _selectedMarketItem = -1;
        private int _selectedWarehouseItem = -1;
        private int _selectedCargoItem = -1;
        private int _selectedContractItem = -1;     // C2-refactor
        private int _selectedShipIndex = 0;
        private string _activeTab = "market"; // "market" / "warehouse" / "contracts"

        // Локальный кэш cargo выбранного корабля (обновляется из TradeResultDto).
        // В snapshot cargo не входит (слишком жирно слать весь груз на каждый tick),
        // но после каждой операции (Load/Unload) сервер присылает updatedCargoSnapshot.
        private WarehouseEntryDto[] _cargoCache = Array.Empty<WarehouseEntryDto>();

        // C2-refactor: кэш контрактов
        private ContractDto[] _contractsCache = Array.Empty<ContractDto>();

        // T-E04: Exchange tab (Resources Exchanger)
        private VisualElement _exchangeSection;
        private ListView _exchangeInvList;   // слева: инвентарь (пикаблы)
        private ListView _exchangeWhList;    // справа: склад (ящики)
        private Button _packBtn;
        private Button _unpackBtn;
        private int _selectedExchangeInvItem = -1;
        private int _selectedExchangeWhItem = -1;
        private List<ItemRow> _exchangeInvCache = new List<ItemRow>();
        private List<ItemRow> _exchangeWhCache = new List<ItemRow>();

        /// <summary>
        /// T-E04: Строка для списков обменника (инвентарь→склад / склад→инвентарь).
        /// </summary>
        private struct ItemRow
        {
            public string displayName;      // название
            public int haveQty;             // сколько есть
            public int maxPacks;            // максимум упаковок/распаковок
            public int inventoryQty;        // сколько штук = 1 паку (rate.inventoryQty) — для Pack
            public int warehouseQty;        // сколько коробок = 1 распаковку (rate.warehouseQty) — для Unpack
            public string warehouseItemId;  // ID товара на складе (для unpack)
            public int inventoryItemId;     // ID предмета в инвентаре (для pack)
        }

        private bool _built = false;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (marketWindowUxml == null) marketWindowUxml = Resources.Load<VisualTreeAsset>("UI/MarketWindow");
            if (marketWindowUss == null) marketWindowUss = Resources.Load<StyleSheet>("UI/MarketWindow");
            if (Instance == null) Instance = this;
        }

        private void OnEnable()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                Debug.LogError("[MarketWindow] нет UIDocument на GameObject");
                return;
            }
            EnsureBuilt();
        }

        private void Start()
        {
            // FIX: после всех OnEnable (включая UIDocument, который мог подвесить
            // свой UXML-auto-load ПОВЕРХ нашего дерева) — перепроверяем состояние.
            // Если main-container потерял ширину (значит USS не дошёл), пересобираем.
            if (!_built || !IsLayoutValid())
            {
                Debug.LogWarning("[MarketWindow] Start(): layout invalid, rebuilding");
                EnsureBuilt();
            }
        }

        private bool IsLayoutValid()
        {
            // FIX (2026-06-04): Не полагаемся на resolvedStyle.width — на первом кадре после
            // Clear()+CloneTree() он бывает NaN/0 (USS layout не успел посчитаться),
            // и мы на каждом E вызывали EnsureBuilt заново, ломая UI.
            // Достаточно проверить, что дерево существует и main-container на месте.
            return _built && _root != null && _mainContainer != null;
        }

        private void EnsureBuilt()
        {
            if (_doc.rootVisualElement == null) return;
            if (marketWindowUxml == null)
                marketWindowUxml = Resources.Load<VisualTreeAsset>("UI/MarketWindow");
            if (marketWindowUss == null)
                marketWindowUss = Resources.Load<StyleSheet>("UI/MarketWindow");
            if (marketWindowUxml == null)
            {
                Debug.LogError("[MarketWindow] UXML не найден в Resources/UI/");
                return;
            }

            _doc.rootVisualElement.Clear();
            if (marketWindowUss != null)
                _doc.rootVisualElement.styleSheets.Add(marketWindowUss);
            _root = marketWindowUxml.CloneTree();
            // FIX: CloneTree() возвращает TemplateContainer (НЕ VisualElement) с дефолт
            // position: relative и 0×0. Без явной растяжки на rootVE этот TemplateContainer
            // становится containing block для .market-window (position: absolute) —
            // top: 5% × 0 = 0, left: 50% × 0 = 0 → панель уезжает в (-320, 0).
            // Решение: растянуть TemplateContainer на весь rootVE (absolute, inset 0).
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            // FIX (2026-06-04): UI Toolkit PanelSettings получает pointer events РАНЬШЕ UGUI
            // Canvas (InputSystemUIInputModule маршрутизирует так в Unity 6). Без Ignore
            // невидимый _root, растянутый на весь экран, перехватывает ВСЕ клики →
            // UGUI кнопки (Host, Connect, ...) не реагируют. По умолчанию Show() включает
            // приём событий; Hide() выключает обратно.
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            _mainContainer = _root.Q<VisualElement>("main-container");
            _locationLabel = _root.Q<Label>("location-label");
            _creditsLabel = _root.Q<Label>("credits-label");
            _warehouseInfoLabel = _root.Q<Label>("warehouse-info-label");
            _timeInfoLabel = _root.Q<Label>("time-info-label");
            _itemList = _root.Q<ListView>("item-list");
            _warehouseList = _root.Q<ListView>("warehouse-list");
            _cargoList = _root.Q<ListView>("cargo-list");
            // C2-refactor: contracts list (3-й таб)
            _contractsList = _root.Q<ListView>("contracts-list");
            _itemSection = _root.Q<VisualElement>("item-section");
            _warehouseSection = _root.Q<VisualElement>("warehouse-section");
            _cargoSection = _root.Q<VisualElement>("cargo-section");
            // C2-refactor: contracts section
            _contractsSection = _root.Q<VisualElement>("contracts-section");
            // T-E04: exchange elements
            _exchangeSection = _root.Q<VisualElement>("exchange-section");
            _exchangeInvList = _root.Q<ListView>("exchange-inventory-list");
            _exchangeWhList = _root.Q<ListView>("exchange-warehouse-list");
            _packBtn = _root.Q<Button>("pack-btn");
            _unpackBtn = _root.Q<Button>("unpack-btn");
            _shipSelectorContainer = _root.Q<VisualElement>("ship-selector-container");
            _shipSelector = _root.Q<DropdownField>("ship-selector");
            _buyBtn = _root.Q<Button>("buy-btn");
            _sellBtn = _root.Q<Button>("sell-btn");
            _loadBtn = _root.Q<Button>("load-btn");
            _unloadBtn = _root.Q<Button>("unload-btn");
            _closeBtn = _root.Q<Button>("close-btn");
            // C2-refactor: contract action buttons
            _acceptBtn = _root.Q<Button>("accept-btn");
            _completeBtn = _root.Q<Button>("complete-btn");
            _failBtn = _root.Q<Button>("fail-btn");
            _messageLabel = _root.Q<Label>("message-label");
            _qtyField = _root.Q<TextField>("qty-field");

            if (_itemList != null)
            {
                _itemList.makeItem = MakeMarketRow;
                _itemList.bindItem = BindMarketRow;
                _itemList.fixedItemHeight = 32;
                // FIX (2026-06-04): Раньше не было selectionType/selectionChanged —
                // клик по строке обновлял ВНУТРЕННИЙ selectedIndex ListView, но
                // _selectedMarketItem оставался -1, и OnBuyClicked/Sell сразу выходили
                // по `if (_selectedMarketItem < 0) return;` → кнопки не работали.
                // В Unity 6 callback selectionChanged даёт IEnumerable<object> selectedItems
                // (это сами объекты из itemsSource, а не индексы) — ищем индекс через
                // Array.IndexOf. onSelectionChange deprecated в пользу selectionChanged.
                _itemList.selectionType = SelectionType.Single;
                _itemList.selectedIndex = -1;
                _itemList.selectionChanged += selectedItems =>
                {
                    _selectedMarketItem = FindSelectedItemIndex<ItemPriceDto>(_itemList, selectedItems);
                    _itemList.Rebuild();
                };
            }
            if (_warehouseList != null)
            {
                _warehouseList.makeItem = MakeWarehouseRow;
                _warehouseList.bindItem = BindWarehouseRow;
                _warehouseList.fixedItemHeight = 32;
                _warehouseList.selectionType = SelectionType.Single;
                _warehouseList.selectedIndex = -1;
                _warehouseList.selectionChanged += selectedItems =>
                {
                    _selectedWarehouseItem = FindSelectedItemIndex<WarehouseEntryDto>(_warehouseList, selectedItems);
                    _warehouseList.Rebuild();
                };
            }
            if (_cargoList != null)
            {
                _cargoList.makeItem = MakeCargoRow;
                _cargoList.bindItem = BindCargoRow;
                _cargoList.fixedItemHeight = 32;
                _cargoList.selectionType = SelectionType.Single;
                _cargoList.selectedIndex = -1;
                _cargoList.selectionChanged += selectedItems =>
                {
                    _selectedCargoItem = FindSelectedItemIndex<WarehouseEntryDto>(_cargoList, selectedItems);
                    _cargoList.Rebuild();
                };
            }
            // C2-refactor: contracts ListView — row factory MakeContractRow / binder BindContractRow.
            // По выбору пользователя «по зоне (fromLocationId)» в _contractsCache лежат
            // только те контракты, у которых fromLocationId == текущая локация (см. HandleContractSnapshot).
            if (_contractsList != null)
            {
                _contractsList.makeItem = MakeContractRow;
                _contractsList.bindItem = BindContractRow;
                _contractsList.fixedItemHeight = 32;
                _contractsList.selectionType = SelectionType.Single;
                _contractsList.selectedIndex = -1;
                _contractsList.selectionChanged += selectedItems =>
                {
                    _selectedContractItem = FindSelectedItemIndex<ContractDto>(_contractsList, selectedItems);
                    _contractsList.Rebuild();
                };
            }

            // T-E04: Exchange inventory list (left — pickable items)
            if (_exchangeInvList != null)
            {
                _exchangeInvList.makeItem = MakeExchangeRow;
                _exchangeInvList.bindItem = BindExchangeInvRow;
                _exchangeInvList.fixedItemHeight = 30;
                _exchangeInvList.selectionType = SelectionType.Single;
                _exchangeInvList.selectedIndex = -1;
                _exchangeInvList.selectionChanged += selectedItems =>
                {
                    _selectedExchangeInvItem = FindSelectedItemIndex<ItemRow>(_exchangeInvList, selectedItems);
                    _exchangeInvList.Rebuild();
                };
            }

            // T-E04: Exchange warehouse list (right — boxes)
            if (_exchangeWhList != null)
            {
                _exchangeWhList.makeItem = MakeExchangeRow;
                _exchangeWhList.bindItem = BindExchangeWhRow;
                _exchangeWhList.fixedItemHeight = 30;
                _exchangeWhList.selectionType = SelectionType.Single;
                _exchangeWhList.selectedIndex = -1;
                _exchangeWhList.selectionChanged += selectedItems =>
                {
                    _selectedExchangeWhItem = FindSelectedItemIndex<ItemRow>(_exchangeWhList, selectedItems);
                    _exchangeWhList.Rebuild();
                };
            }

            var marketTabBtn = _root.Q<Button>("tab-market");
            var warehouseTabBtn = _root.Q<Button>("tab-warehouse");
            // C2-refactor: 3-й таб КОНТРАКТЫ
            var contractsTabBtn = _root.Q<Button>("tab-contracts");
            // T-E04: 4-й таб ОБМЕННИК
            var exchangeTabBtn = _root.Q<Button>("tab-exchanger");
            if (marketTabBtn != null) marketTabBtn.clicked += () => SwitchTab("market");
            if (warehouseTabBtn != null) warehouseTabBtn.clicked += () => SwitchTab("warehouse");
            if (contractsTabBtn != null) contractsTabBtn.clicked += () => SwitchTab("contracts");
            if (exchangeTabBtn != null) exchangeTabBtn.clicked += () => SwitchTab("exchange");

            if (_buyBtn != null) _buyBtn.clicked += OnBuyClicked;
            if (_sellBtn != null) _sellBtn.clicked += OnSellClicked;
            if (_loadBtn != null) _loadBtn.clicked += OnLoadClicked;
            if (_unloadBtn != null) _unloadBtn.clicked += OnUnloadClicked;
            if (_closeBtn != null) _closeBtn.clicked += OnCloseClicked;
            // C2-refactor: contract action handlers
            if (_acceptBtn != null) _acceptBtn.clicked += OnAcceptContractClicked;
            if (_completeBtn != null) _completeBtn.clicked += OnCompleteContractClicked;
            if (_failBtn != null) _failBtn.clicked += OnFailContractClicked;
            // T-E04: exchange button handlers
            if (_packBtn != null) _packBtn.clicked += OnPackClicked;
            if (_unpackBtn != null) _unpackBtn.clicked += OnUnpackClicked;

            if (_shipSelector != null)
            {
                _shipSelector.RegisterValueChangedCallback(evt =>
                {
                    var snap = _state?.CurrentSnapshot;
                    if (!snap.HasValue) return;
                    var ships = snap.Value.nearbyShips;
                    for (int i = 0; i < ships.Length; i++)
                    {
                        if (ships[i].displayName == evt.newValue)
                        {
                            _selectedShipIndex = i;
                            ulong newShipId = ships[i].shipNetworkObjectId;

                            // FIX (2026-06-05): мгновенное переключение cargo из
                            // per-ship клиентского кэша MarketClientState.CurrentShipCargos.
                            // Раньше _cargoCache обновлялся ТОЛЬКО по приходу snapshot
                            // (каждые ~5 мин тика) или по TradeResult.updatedCargoSnapshot
                            // (после успешной операции). При простом переключении
                            // ship-selector без других действий — кэш оставался
                            // cargo предыдущего корабля → баг "switch light→medium→light
                            // показывает пусто". Теперь: мгновенный UI-апдейт из
                            // локального кэша + отправка RPC (safety net: сервер
                            // пришлёт свежий snapshot, если корабль не был в кэше).
                            ApplySelectedShipCargoFromCache(newShipId);

                            _state?.RequestSetSelectedShip(snap.Value.locationId, newShipId);
                            return;
                        }
                    }
                });
            }

            _state = MarketClientState.Instance;
            if (_state == null)
            {
                Debug.LogWarning("[MarketWindow] MarketClientState.Instance == null, UI не будет обновляться");
            }
            else
            {
                _state.OnSnapshotUpdated += HandleSnapshot;
                _state.OnTradeResult += HandleTradeResult;
            }

            // C2-refactor: подписка на ContractClientState для таба КОНТРАКТЫ.
            // Контракты живут в отдельном singleton-проекции, как и рынок.
            var contractState = ProjectC.Trade.Client.ContractClientState.Instance;
            if (contractState == null)
            {
                Debug.LogWarning("[MarketWindow] ContractClientState.Instance == null, контракты UI не будут обновляться");
            }
            else
            {
                contractState.OnSnapshotUpdated += HandleContractSnapshot;
                contractState.OnContractResult += HandleContractResult;
            }

            // T-E04: подписка на ExchangeClientState для таба ОБМЕННИК.
            var exchangeState = ProjectC.Trade.Client.ExchangeClientState.Instance;
            if (exchangeState != null)
            {
                exchangeState.OnResultReceived += HandleExchangeResult;
            }
            else
            {
                Debug.LogWarning("[MarketWindow] ExchangeClientState.Instance == null, обменник UI не будет получать результаты");
            }

            // T-E04: подписка на InventoryClientState для инвентаря в табе ОБМЕННИК.
            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState != null)
            {
                invState.OnSnapshotUpdated += RefreshExchangeData;
            }

            SwitchTab("market");
            SetVisible(visibleOnStart);
            _doc.rootVisualElement.MarkDirtyRepaint();
            _built = true;
            Debug.Log($"[MarketWindow] Built: root.children={_doc.rootVisualElement.childCount}, styleSheets={_doc.rootVisualElement.styleSheets.count}");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            if (_state != null)
            {
                _state.OnSnapshotUpdated -= HandleSnapshot;
                _state.OnTradeResult -= HandleTradeResult;
            }
            // C2-refactor: отписка от ContractClientState
            var contractState = ProjectC.Trade.Client.ContractClientState.Instance;
            if (contractState != null)
            {
                contractState.OnSnapshotUpdated -= HandleContractSnapshot;
                contractState.OnContractResult -= HandleContractResult;
            }
            // T-E04: отписка от ExchangeClientState
            var exchangeState = ProjectC.Trade.Client.ExchangeClientState.Instance;
            if (exchangeState != null)
            {
                exchangeState.OnResultReceived -= HandleExchangeResult;
            }
            // T-E04: отписка от InventoryClientState
            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState != null)
            {
                invState.OnSnapshotUpdated -= RefreshExchangeData;
            }
        }

        private void Update()
        {
            // FIX: убран E-handler — он дублировал NetworkPlayer.cs и открывал окно ВЕЗДЕ,
            // даже вне зоны MarketZone. Открытие теперь идёт ТОЛЬКО через NetworkPlayer
            // → MarketInteractor.TryOpenMarket() (только в зоне).
            // Здесь оставлен только Esc для закрытия открытого окна.
            // BUGFIX T-P19: Esc проверяем ДО guard'а NetworkManager — окно должно
            // закрываться Esc независимо от состояния сети.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsVisible())
            {
                Hide();
                return;
            }

            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            if (!nm.IsClient && !nm.IsServer) return;
        }

        // ========================================================
        // ROW TEMPLATES
        // ========================================================

        private VisualElement MakeMarketRow()
        {
            var row = new VisualElement();
            row.AddToClassList("market-row");
            var label = new Label { name = "row-label" };
            label.AddToClassList("market-row-label");
            row.Add(label);
            return row;
        }

        private void BindMarketRow(VisualElement row, int index)
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue) return;
            var items = snap.Value.items;
            if (index < 0 || index >= items.Length) return;
            var it = items[index];
            // FIX (2026-06-04): На вкладке «РЫНОК» показываем не только рыночный сток
            // (сколько можно КУПИТЬ), но и количество на складе игрока (сколько можно
            // ПРОДАТЬ). Раньше это было видно только после переключения на вкладку
            // «СКЛАД / ТРЮМ» — продажа шла «вслепую», игрок вводил qty наугад и
            // получал ошибку NotEnoughInWarehouse от сервера.
            int whQty = FindWarehouseQty(snap.Value.warehouse, it.itemId);
            row.Q<Label>("row-label").text =
                $"{it.displayName}  —  {it.currentPrice:F0} CR  (сток: {it.availableStock})  (у вас: {whQty})";
            row.style.backgroundColor = (index == _selectedMarketItem) ? new StyleColor(new Color(0.4f, 0.6f, 0.9f, 0.4f)) : StyleKeyword.Null;
        }

        /// <summary>
        /// FIX (2026-06-04): Линейный поиск количества товара на складе игрока.
        /// Warehouse — это плоский массив (≤ warehouseMaxTypes типов), не Dictionary;
        /// линейный поиск приемлем (типов товаров в игре единицы, не сотни).
        /// </summary>
        private static int FindWarehouseQty(WarehouseEntryDto[] warehouse, string itemId)
        {
            if (warehouse == null || string.IsNullOrEmpty(itemId)) return 0;
            for (int i = 0; i < warehouse.Length; i++)
            {
                if (warehouse[i].itemId == itemId) return warehouse[i].quantity;
            }
            return 0;
        }

        private VisualElement MakeWarehouseRow()
        {
            var row = new VisualElement();
            row.AddToClassList("warehouse-row");
            var label = new Label { name = "row-label" };
            row.Add(label);
            return row;
        }

        private void BindWarehouseRow(VisualElement row, int index)
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue) return;
            var wh = snap.Value.warehouse;
            if (index < 0 || index >= wh.Length) return;
            var e = wh[index];
            row.Q<Label>("row-label").text = $"{e.displayName}  —  {e.quantity} ед.";
            row.style.backgroundColor = (index == _selectedWarehouseItem) ? new StyleColor(new Color(0.4f, 0.6f, 0.9f, 0.4f)) : StyleKeyword.Null;
        }

        private VisualElement MakeCargoRow()
        {
            var row = new VisualElement();
            row.AddToClassList("cargo-row");
            var label = new Label { name = "row-label" };
            row.Add(label);
            return row;
        }

        private void BindCargoRow(VisualElement row, int index)
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue) return;
            var cargo = SnapCargo(snap.Value);
            if (index < 0 || index >= cargo.Count) return;
            var e = cargo[index];
            row.Q<Label>("row-label").text = $"{e.displayName}  —  {e.quantity} ед.  ({GetSelectedShipName()})";
            row.style.backgroundColor = (index == _selectedCargoItem) ? new StyleColor(new Color(0.4f, 0.9f, 0.6f, 0.4f)) : StyleKeyword.Null;
        }

        // ========================================================
        // STATE PROJECTION
        // ========================================================

        private void HandleSnapshot(MarketSnapshotDto snap)
        {
            if (_locationLabel != null) _locationLabel.text = $"Рынок: {snap.displayName}";
            if (_creditsLabel != null) _creditsLabel.text = $"Кредиты: {snap.credits:F0} CR";
            if (_warehouseInfoLabel != null)
            {
                _warehouseInfoLabel.text = $"Склад: {snap.warehouse?.Length ?? 0} типов / {snap.warehouseMaxTypes}";
            }
            if (_timeInfoLabel != null)
            {
                int seconds = Mathf.CeilToInt(snap.secondsUntilNextTick);
                _timeInfoLabel.text = $"Скорость рынка: x{snap.marketTimeMultiplier:F1} | Тик через: {seconds}с";
            }
            // FIX (2026-06-04): Синхронизировать _cargoCache с серверным cargo из snapshot.
            // До этого UI cargo обновлялся ТОЛЬКО из TradeResultDto.updatedCargoSnapshot
            // (после успешного Load/Unload). При открытии рынка / смене корабля UI
            // показывал stale _cargoCache. Теперь сервер шлёт cargo выбранного корабля
            // в каждом snapshot — UI всегда видит реальный груз. TradeResult продолжает
            // обновлять _cargoCache мгновенно после операции, snapshot-обновление придёт
            // следом и перезапишет то же значение (идемпотентно).
            //
            // FIX (2026-06-05): Per-ship кэш MarketClientState.CurrentShipCargos
            // уже обновлён в OnSnapshotReceived (до того, как мы сюда пришли через
            // event). Используем его как single source of truth — чтобы UI cargo
            // для ТЕКУЩЕГО выбранного корабля всегда соответствовал per-ship
            // кэшу. Если в кэше есть запись для выбранного корабля — берём её;
            // иначе fallback на snap.cargo (старое поле, обратная совместимость).
            ulong currentShipId = GetSelectedShipId();
            if (_state != null && _state.CurrentShipCargos != null
                && currentShipId != 0
                && _state.CurrentShipCargos.TryGetValue(currentShipId, out var cachedCargo))
            {
                _cargoCache = cachedCargo ?? Array.Empty<WarehouseEntryDto>();
            }
            else
            {
                _cargoCache = snap.cargo ?? Array.Empty<WarehouseEntryDto>();
            }
            // Списки — назначаем itemsSource (массивы DTO).
            // Без этого ListView знает callbacks (makeItem/bindItem), но не знает сколько элементов.
            if (_itemList != null) _itemList.itemsSource = snap.items;
            if (_warehouseList != null) _warehouseList.itemsSource = snap.warehouse ?? Array.Empty<WarehouseEntryDto>();
            if (_cargoList != null) _cargoList.itemsSource = _cargoCache ?? Array.Empty<WarehouseEntryDto>();
            if (_itemList != null) _itemList.Rebuild();
            if (_warehouseList != null) _warehouseList.Rebuild();
            if (_cargoList != null) _cargoList.Rebuild();

            // Ship selector
            if (_shipSelector != null && snap.nearbyShips != null)
            {
                var choices = new List<string>();
                foreach (var s in snap.nearbyShips) choices.Add(s.displayName);
                _shipSelector.choices = choices;
                if (choices.Count > 0 && _shipSelector.value == "")
                {
                    _shipSelector.value = choices[0];
                    _selectedShipIndex = 0;
                    // FIX (2026-06-04): Сообщить серверу о начальном выборе корабля, чтобы
                    // он включил cargo этого корабля в следующий snapshot (на случай,
                    // если сервер не выбрал дефолт сам).
                    _state?.RequestSetSelectedShip(snap.locationId, snap.nearbyShips[0].shipNetworkObjectId);
                }
                // Показываем селектор только если кораблей > 1
                if (_shipSelectorContainer != null)
                {
                    _shipSelectorContainer.style.display = (snap.nearbyShips.Length > 1) ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private void HandleTradeResult(TradeResultDto result)
        {
            if (_messageLabel != null)
            {
                if (result.IsSuccess)
                {
                    _messageLabel.text = $"{LocalizeOp(result.op)}: OK ({result.itemId} x{result.quantity})";
                    _messageLabel.style.color = new StyleColor(new Color(0.4f, 0.95f, 0.4f));
                }
                else
                {
                    _messageLabel.text = $"Ошибка: {MarketClientState.LocalizeResultCode(result.code)}";
                    _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
                }
            }

            // Обновляем кэш cargo при Load/Unload (сервер присылает updatedCargoSnapshot).
            // FIX (2026-06-04): MarketServer.BuildWarehouseDtos/BuildCargoDtos возвращает null
            // когда коллекция пуста (Count == 0). После Unload последней единицы товара cargo
            // становится пустым, dto.updatedCargoSnapshot == null. Старое условие
            // `if (... != null)` пропускало этот случай — _cargoCache не очищался,
            // и в UI оставалась "призрачная" строка товара, которого фактически нет
            // (повторный Unload падал, потому что на сервере груз уже пуст).
            // Теперь трактуем null как "груз пуст" и безусловно обновляем кэш на успешной операции.
            //
            // FIX (2026-06-05): дополнительно обновляем per-ship клиентский кэш
            // MarketClientState.CurrentShipCargos[shipId] = updatedCargoSnapshot.
            // Без этого при последующем переключении на другой корабль и обратно
            // UI показал бы stale cargo (из старого snapshot), а не реальный
            // после Load/Unload. TradeResult всегда приходит сразу после операции,
            // а snapshot может прийти через ~5 мин (тик) — нельзя полагаться только
            // на snapshot для поддержания per-ship кэша в актуальном состоянии.
            if (result.IsSuccess && (result.op == TradeOp.LoadToShip || result.op == TradeOp.UnloadFromShip))
            {
                _cargoCache = result.updatedCargoSnapshot ?? Array.Empty<WarehouseEntryDto>();
                if (_cargoList != null)
                {
                    _cargoList.itemsSource = _cargoCache;
                    _selectedCargoItem = -1;
                    _cargoList.selectedIndex = -1;
                    _cargoList.Rebuild();
                }
                if (result.shipNetworkObjectId != 0 && _state != null)
                {
                    _state.UpdateShipCargo(result.shipNetworkObjectId, _cargoCache);
                }
            }
        }

        // ========================================================
        // CONTRACT ROW TEMPLATES (C2-refactor)
        // ========================================================

        /// <summary>
        /// C2-refactor: row factory для таба КОНТРАКТЫ.
        /// Создаёт 4 Label'а (type / item / reward / timer), заполняется в BindContractRow.
        /// Размеры/цвета — из .contract-row/.contract-type/.contract-item/etc. в MarketWindow.uss.
        /// </summary>
        private static VisualElement MakeContractRow()
        {
            var row = new VisualElement();
            row.AddToClassList("contract-row");
            var typeLabel = new Label { name = "type" };
            typeLabel.AddToClassList("contract-type");
            row.Add(typeLabel);
            var itemLabel = new Label { name = "item" };
            itemLabel.AddToClassList("contract-item");
            row.Add(itemLabel);
            var rewardLabel = new Label { name = "reward" };
            rewardLabel.AddToClassList("contract-reward");
            row.Add(rewardLabel);
            var timerLabel = new Label { name = "timer" };
            timerLabel.AddToClassList("contract-timer");
            row.Add(timerLabel);
            return row;
        }

        /// <summary>
        /// C2-refactor: binder для строки контракта. Заполняет 4 Label'а данными из ContractDto.
        /// Цвет type и timer берём из .type-* / .timer-* классов (MarketWindow.uss).
        /// </summary>
        private void BindContractRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _contractsCache.Length) return;
            var c = _contractsCache[index];
            var typeLabel = row.Q<Label>("type");
            var itemLabel = row.Q<Label>("item");
            var rewardLabel = row.Q<Label>("reward");
            var timerLabel = row.Q<Label>("timer");

            // FIX (2026-06-05): визуальное отличие Active vs Pending.
            // Active → " [ВЗЯТ]" префикс в typeLabel + зелёный класс на row.
            // Pending → обычный вид, без класса.
            bool isActive = c.state == (byte)ContractState.Active;
            if (isActive)
            {
                row.AddToClassList("contract-row-active");
            }
            else
            {
                row.RemoveFromClassList("contract-row-active");
            }

            if (typeLabel != null)
            {
                string typeName = GetContractTypeDisplayName((ContractType)c.type);
                typeLabel.text = isActive ? $"{typeName} [ВЗЯТ]" : typeName;
                typeLabel.RemoveFromClassList("type-standard");
                typeLabel.RemoveFromClassList("type-urgent");
                typeLabel.RemoveFromClassList("type-receipt");
                typeLabel.AddToClassList(GetContractTypeClass((ContractType)c.type));
            }
            if (itemLabel != null) itemLabel.text = $"{c.displayName} x{c.quantity}  ({c.fromLocationId}→{c.toLocationId})";
            // Для active не показываем reward (он уже "твой" — игрок помнит).
            if (rewardLabel != null) rewardLabel.text = isActive ? "" : $"{c.reward:F0} CR";
            if (timerLabel != null)
            {
                timerLabel.text = GetContractTimeRemainingString(c);
                timerLabel.RemoveFromClassList("timer-ok");
                timerLabel.RemoveFromClassList("timer-warn");
                timerLabel.RemoveFromClassList("timer-danger");
                timerLabel.AddToClassList(GetContractTimerClass(c));
            }
            // Highlight selected (поверх active-подсветки)
            row.style.backgroundColor = (index == _selectedContractItem)
                ? new StyleColor(new Color(0.4f, 0.6f, 0.9f, 0.4f))
                : StyleKeyword.Null;
        }

        /// <summary>
        /// C2-refactor: обновление списка контрактов из snapshot'а ContractClientState.
        /// По выбору пользователя «по зоне (fromLocationId)» фильтруем available по текущей
        /// локации (используем CurrentSnapshot рынка как источник locationId — мы в той же зоне).
        /// active показываем ВСЕ активные контракты игрока (state==Active), независимо от локации —
        /// потому что игрок должен видеть свои задачи даже если зашёл в рынок не на fromLocationId.
        /// </summary>
        private void HandleContractSnapshot(ContractSnapshotDto snapshot)
        {
            if (_contractsList == null) return;

            string currentLocationId = _state?.CurrentSnapshot.HasValue == true
                ? _state.CurrentSnapshot.Value.locationId
                : null;

            // FIX (2026-06-05): фильтруем available по двум критериям:
            //   1) fromLocationId == currentLocationId (только для текущей локации)
            //   2) state == Pending (защита от случайных дублей; на сервере уже фильтруется)
            // Без #2: после accept сервер отдаёт в available[] контракты, которые уже
            // Active (если фильтр сломался), и они дублируются в active[].
            ContractDto[] available = Array.Empty<ContractDto>();
            if (!string.IsNullOrEmpty(currentLocationId) && snapshot.available != null)
            {
                var list = new List<ContractDto>();
                for (int i = 0; i < snapshot.available.Length; i++)
                {
                    var c = snapshot.available[i];
                    if (c.state != (byte)ContractState.Pending) continue;
                    if (!string.Equals(c.fromLocationId, currentLocationId, StringComparison.OrdinalIgnoreCase)) continue;
                    list.Add(c);
                }
                available = list.ToArray();
            }

            // Active: все активные контракты игрока (без фильтра по локации — игрок
            // должен видеть свои задачи даже если зашёл в рынок не на fromLocationId).
            // FIX: фильтр state==Active для защиты (на сервере уже фильтруется).
            ContractDto[] activeAll = snapshot.active ?? Array.Empty<ContractDto>();
            var activeList = new List<ContractDto>(activeAll.Length);
            for (int i = 0; i < activeAll.Length; i++)
            {
                if (activeAll[i].state == (byte)ContractState.Active)
                {
                    activeList.Add(activeAll[i]);
                }
            }
            ContractDto[] active = activeList.ToArray();

            // Показываем: сначала active (свои задачи), потом available (новые)
            var combined = new List<ContractDto>(active.Length + available.Length);
            combined.AddRange(active);
            combined.AddRange(available);
            _contractsCache = combined.ToArray();

            _contractsList.itemsSource = _contractsCache;
            _selectedContractItem = -1;
            _contractsList.selectedIndex = -1;
            _contractsList.Rebuild();

            // Message feedback
            if (_messageLabel != null && IsVisible() && _activeTab == "contracts")
            {
                _messageLabel.text = active.Length == 0 && available.Length == 0
                    ? "Нет контрактов на этой локации"
                    : $"Активных: {active.Length} | Доступно: {available.Length}";
                _messageLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            }
        }

        /// <summary>
        /// C2-refactor: обновление message после операции accept/complete/fail.
        /// Сервер сам шлёт новый snapshot (ContractServer.RequestXxxRpc) — HandleContractSnapshot
        /// обновит UI автоматически.
        /// </summary>
        private void HandleContractResult(ContractResultDto result)
        {
            if (_messageLabel == null) return;
            // ContractResultDto — struct, поэтому проверка на null не нужна.

            // FIX (2026-06-05): обновляем кредиты/долг из result.newCredits/newDebt
            // независимо от таба — игрок должен видеть наградy в HUD сразу, даже если
            // он не в табе КОНТРАКТЫ (например, сидит на вкладке РЫНОК).
            // _creditsLabel общий для всех табов (лежит в шапке окна).
            if (_creditsLabel != null && result.newCredits > 0f)
            {
                _creditsLabel.text = $"Кредиты: {result.newCredits:F0} CR";
            }

            // FIX (2026-06-05): попросить сервер прислать свежий market snapshot чтобы
            // синхронизировать _state.CurrentSnapshot.credits. Без этого MarketClientState
            // показывает старые кредиты (его _currentSnapshot не обновляется при
            // контрактных операциях), и при следующем открытии вкладки РЫНОК игрок видит
            // несоответствие: HUD говорит 1500 CR, а _creditsLabel в шапке — 1000.
            if (result.IsSuccess && _state != null && _state.CurrentSnapshot.HasValue)
            {
                _state.RequestSubscribeMarket(_state.CurrentSnapshot.Value.locationId);
            }

            // FIX (2026-06-05): message показываем ВСЕГДА (любой таб), а не только в
            // "contracts". Иначе игрок жмёт ВЗЯТЬ в табе РЫНОК и не получает обратной
            // связи — думает что кнопка сломана. Message-label общий для всех табов.
            if (!IsVisible()) return;
            if (result.IsSuccess)
            {
                _messageLabel.text = result.message ?? "OK";
                _messageLabel.style.color = new StyleColor(new Color(0.4f, 0.95f, 0.4f));
            }
            else
            {
                _messageLabel.text = result.message ?? ProjectC.Trade.Client.ContractClientState.LocalizeResultCode((ContractResultCode)result.code);
                _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
            }
        }

        // C2-refactor: static helpers (port from ContractBoardWindow.cs)
        public static string GetContractTypeDisplayName(ContractType type)
        {
            switch (type)
            {
                case ContractType.Standard: return "[Стандарт]";
                case ContractType.Urgent: return "[Срочный]";
                case ContractType.Receipt: return "[Расписка]";
                default: return type.ToString();
            }
        }

        public static string GetContractTypeClass(ContractType type)
        {
            switch (type)
            {
                case ContractType.Standard: return "type-standard";
                case ContractType.Urgent: return "type-urgent";
                case ContractType.Receipt: return "type-receipt";
                default: return "type-standard";
            }
        }

        public static string GetContractTimeRemainingString(ContractDto c)
        {
            if (c.timeLimit <= 0f) return "∞";
            int minutes = Mathf.FloorToInt(c.timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(c.timeRemaining % 60f);
            return $"{minutes}:{seconds:D2}";
        }

        public static string GetContractTimerClass(ContractDto c)
        {
            if (c.timeLimit <= 0f) return "timer-ok";
            float pct = c.timeRemaining / c.timeLimit;
            if (pct < 0.1f) return "timer-danger";
            if (pct < 0.3f) return "timer-warn";
            return "timer-ok";
        }

        // ========================================================
        // ACTIONS
        // ========================================================

        private void OnBuyClicked()
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue) return;
            if (_selectedMarketItem < 0 || _selectedMarketItem >= snap.Value.items.Length) return;
            int qty = ParseQty();
            var it = snap.Value.items[_selectedMarketItem];
            _state.RequestBuy(snap.Value.locationId, it.itemId, qty);
        }

        private void OnSellClicked()
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue) return;
            if (_selectedMarketItem < 0 || _selectedMarketItem >= snap.Value.items.Length) return;
            int qty = ParseQty();
            var it = snap.Value.items[_selectedMarketItem];
            _state.RequestSell(snap.Value.locationId, it.itemId, qty);
        }

        private void OnLoadClicked()
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue) return;
            if (_selectedWarehouseItem < 0 || _selectedWarehouseItem >= (snap.Value.warehouse?.Length ?? 0)) return;
            int qty = ParseQty();
            var wh = snap.Value.warehouse[_selectedWarehouseItem];
            ulong shipId = GetSelectedShipId();
            if (shipId == 0) { SetMessage("Сначала выберите корабль", true); return; }
            _state.RequestLoadToShip(snap.Value.locationId, wh.itemId, qty, shipId);
        }

        private void OnUnloadClicked()
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue) return;
            var cargo = SnapCargo(snap.Value);
            if (_selectedCargoItem < 0 || _selectedCargoItem >= cargo.Count) return;
            int qty = ParseQty();
            ulong shipId = GetSelectedShipId();
            if (shipId == 0) { SetMessage("Сначала выберите корабль", true); return; }
            var it = cargo[_selectedCargoItem];
            _state.RequestUnloadFromShip(snap.Value.locationId, it.itemId, qty, shipId);
        }

        private void OnCloseClicked() => SetVisible(false);

        // C2-refactor: contract action handlers
        private void OnAcceptContractClicked()
        {
            if (_selectedContractItem < 0 || _selectedContractItem >= _contractsCache.Length)
            {
                SetMessage("Выберите контракт для принятия");
                return;
            }
            var c = _contractsCache[_selectedContractItem];
            if (c.state != (byte)ContractState.Pending)
            {
                SetMessage("Этот контракт уже не доступен для принятия");
                return;
            }
            var contractState = ProjectC.Trade.Client.ContractClientState.Instance;
            if (contractState == null) return;

            // FIX (2026-06-05): optimistic update — мгновенный визуальный feedback
            // игроку ДО прихода RPC. Контракт сразу помечается как Active в локальном
            // _contractsCache, и строка перекрашивается в зелёный + [ВЗЯТ].
            // Через ~1.5с придёт серверный snapshot, который заменит эту запись на
            // настоящую (но визуально будет то же самое — state=Active).
            var cLocal = c;
            cLocal.state = (byte)ContractState.Active;
            // Сохраняем копию как value-type (struct) → меняется элемент массива.
            _contractsCache[_selectedContractItem] = cLocal;
            _contractsList?.Rebuild();

            // Pulse-эффект на 1.5с (визуально подтверждает "только что взят")
            StartCoroutine(JustTakenPulse(_selectedContractItem));

            contractState.RequestAccept(c.contractId);
            SetMessage("Запрос отправлен...");
        }

        /// <summary>
        /// FIX (2026-06-05): на 1.5с добавляет класс contract-row-just-taken на
        /// указанный row (по индексу в ListView). При смене snapshot класс уйдёт
        /// сам (CSS transition-duration 1.5s вернёт фон в норму).
        /// </summary>
        private System.Collections.IEnumerator JustTakenPulse(int rowIndex)
        {
            if (_contractsList == null) yield break;
            // Ждём один frame — Rebuild() асинхронен.
            yield return null;
            var row = _contractsList.ElementAt(rowIndex) as VisualElement;
            if (row == null) yield break;
            row.AddToClassList("contract-row-just-taken");
            yield return new WaitForSeconds(1.6f);
            if (row != null) row.RemoveFromClassList("contract-row-just-taken");
        }

        private void OnCompleteContractClicked()
        {
            if (_selectedContractItem < 0 || _selectedContractItem >= _contractsCache.Length)
            {
                SetMessage("Выберите активный контракт для сдачи");
                return;
            }
            var c = _contractsCache[_selectedContractItem];
            if (c.state != (byte)ContractState.Active)
            {
                SetMessage("Этот контракт не активен");
                return;
            }
            // Сервер сам валидирует что игрок в toLocationId (см. ContractServer.RequestCompleteRpc).
            // Если нет — вернёт WrongDestination.
            var contractState = ProjectC.Trade.Client.ContractClientState.Instance;
            if (contractState == null) return;
            contractState.RequestComplete(c.contractId);
            SetMessage("Запрос отправлен...");
        }

        private void OnFailContractClicked()
        {
            if (_selectedContractItem < 0 || _selectedContractItem >= _contractsCache.Length)
            {
                SetMessage("Выберите активный контракт");
                return;
            }
            var c = _contractsCache[_selectedContractItem];
            if (c.state != (byte)ContractState.Active)
            {
                SetMessage("Этот контракт не активен");
                return;
            }
            var contractState = ProjectC.Trade.Client.ContractClientState.Instance;
            if (contractState == null) return;
            contractState.RequestFail(c.contractId);
            SetMessage("Запрос отправлен...");
        }

        private void SwitchTab(string tab)
        {
            _activeTab = tab;
            // T-E04: 4 таба — market / warehouse / contracts / exchange
            bool isMarket = tab == "market";
            bool isWarehouse = tab == "warehouse";
            bool isContracts = tab == "contracts";
            bool isExchange = tab == "exchange";

            // FIX: прячем ВСЮ секцию (заголовок + список), а не только ListView —
            // иначе 3 заголовка "Товары на рынке / Ваш склад / Груз корабля" висят одновременно
            // и контейнер сжимается через flex-shrink, ломая layout.
            if (_itemSection != null) _itemSection.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_warehouseSection != null) _warehouseSection.style.display = isWarehouse ? DisplayStyle.Flex : DisplayStyle.None;
            if (_cargoSection != null) _cargoSection.style.display = isWarehouse ? DisplayStyle.Flex : DisplayStyle.None;
            // C2-refactor: contracts section
            if (_contractsSection != null) _contractsSection.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
            // T-E04: exchange section
            if (_exchangeSection != null) _exchangeSection.style.display = isExchange ? DisplayStyle.Flex : DisplayStyle.None;

            // Кнопки — набор меняется по табу
            if (_buyBtn != null) _buyBtn.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_sellBtn != null) _sellBtn.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_loadBtn != null) _loadBtn.style.display = isWarehouse ? DisplayStyle.Flex : DisplayStyle.None;
            if (_unloadBtn != null) _unloadBtn.style.display = isWarehouse ? DisplayStyle.Flex : DisplayStyle.None;
            // C2-refactor: contract action buttons — только в табе contracts
            if (_acceptBtn != null) _acceptBtn.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
            if (_completeBtn != null) _completeBtn.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
            if (_failBtn != null) _failBtn.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
            // T-E04: exchange buttons — только в табе exchange
            if (_packBtn != null) _packBtn.style.display = isExchange ? DisplayStyle.Flex : DisplayStyle.None;
            if (_unpackBtn != null) _unpackBtn.style.display = isExchange ? DisplayStyle.Flex : DisplayStyle.None;

            // T-E04 FIX: при первом переключении на таб exchange — заполнить списки.
            // RefreshExchangeData подписан только на OnSnapshotUpdated инвентаря,
            // но когда игрок впервые открывает market window и переключается на exchange,
            // инвентарь мог не меняться — списки остаются пустыми.
            if (isExchange) RefreshExchangeData();

            // Ship selector виден только в табе СКЛАД (multi-ship load/unload)
            if (_shipSelectorContainer != null)
            {
                bool isWarehouseShowShip = isWarehouse && IsShipSelectorVisible();
                _shipSelectorContainer.style.display = isWarehouseShowShip ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // C2-refactor: qty row виден только в табе РЫНОК (qty не используется для контрактов)
            // ИСПРАВЛЕНО: раньше qty был показан в обоих market/warehouse, но в warehouse qty
            // уже парсится из _qtyField без изменений. В contracts qty не имеет смысла.
            if (_qtyField != null && _qtyField.parent != null)
            {
                _qtyField.parent.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// C2-refactor: вспомогательный метод для SwitchTab — определяет, нужно ли показывать
        /// ship selector. Использует текущий snapshot (если есть), иначе false.
        /// </summary>
        private bool IsShipSelectorVisible()
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue) return false;
            return snap.Value.nearbyShips != null && snap.Value.nearbyShips.Length > 1;
        }

        // ========================================================
        // UTILS
        // ========================================================

        private int ParseQty()
        {
            if (_qtyField == null) return 1;
            if (!int.TryParse(_qtyField.value, out var q)) return 1;
            return Mathf.Clamp(q, 1, 9999);
        }

        /// <summary>
        /// FIX (2026-06-04): UI Toolkit selectionChanged в Unity 6 передаёт сами
        /// selected items (IEnumerable&lt;object&gt;), а не индексы. Достаём первый
        /// объект и ищем его в текущем itemsSource. Если пусто / не нашли — -1.
        /// </summary>
        private static int FindSelectedItemIndex<T>(ListView list, IEnumerable<object> selectedItems)
        {
            if (selectedItems == null) return -1;
            object first = null;
            foreach (var o in selectedItems)
            {
                first = o;
                break;
            }
            if (first == null) return -1;
            var src = list.itemsSource;
            if (src is T[] arr) return Array.IndexOf(arr, (T)first);
            if (src is IList<T> listT)
            {
                for (int i = 0; i < listT.Count; i++)
                    if (EqualityComparer<T>.Default.Equals(listT[i], (T)first)) return i;
            }
            return -1;
        }

        private void SetMessage(string msg, bool isError = false)
        {
            if (_messageLabel == null) return;
            _messageLabel.text = msg;
            _messageLabel.style.color = isError
                ? new StyleColor(new Color(0.95f, 0.4f, 0.4f))
                : new StyleColor(new Color(0.9f, 0.9f, 0.9f));
        }

        private void SetVisible(bool v)
        {
            if (_mainContainer == null) _mainContainer = _root?.Q<VisualElement>("main-container");
            if (_mainContainer != null)
            {
                _mainContainer.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
                if (v)
                {
                    // FIX: apply inline fallback styles so the window is visually
                    // correct even if USS rules haven't propagated (Unity 6
                    // occasionally shows resolvedStyle=initial on the frame
                    // SetVisible is called, before the layout pass runs).
                    // USS still wins for children; these only anchor the window.
                    ApplyInlineFallbackStyles(_mainContainer);
                }
            }

            // FIX: Курсор по умолчанию залочен и спрятан (flight-режим).
            // При открытом UI нужно отпустить курсор чтобы кликать кнопки.
            // При закрытии — вернуть как было (если игра запущена, не из главного меню).
            if (v)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else
            {
                // Возвращаем в "игровой" режим, только если сеть запущена
                // (т.е. игрок уже в игре, а не на главном меню).
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
                // Иначе оставляем курсор свободным (главное меню).
            }
        }

        // FIX: дублируем ключевые правила из .market-window inline.
        // Причина: resolvedStyle=initial (pos=Relative, w=0) в момент первого
        // Show() — layout pass не успел, USS не применился. Через кадр всё ОК,
        // но визуально первый кадр — мусор. Inline-стили это фиксят мгновенно.
        private static void ApplyInlineFallbackStyles(VisualElement main)
        {
            main.style.position = Position.Absolute;
            main.style.top = new Length(5, LengthUnit.Percent);
            main.style.left = new Length(50, LengthUnit.Percent);
            main.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), 0));
            main.style.width = 640;
            main.style.maxWidth = new Length(90, LengthUnit.Percent);
            main.style.maxHeight = new Length(90, LengthUnit.Percent);
            main.style.backgroundColor = new Color(0.078f, 0.098f, 0.137f, 0.95f);
            main.style.borderTopWidth = 2;
            main.style.borderRightWidth = 2;
            main.style.borderBottomWidth = 2;
            main.style.borderLeftWidth = 2;
            main.style.borderTopColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderRightColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderBottomColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderLeftColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderTopLeftRadius = 8;
            main.style.borderTopRightRadius = 8;
            main.style.borderBottomLeftRadius = 8;
            main.style.borderBottomRightRadius = 8;
            main.style.paddingTop = 12;
            main.style.paddingRight = 12;
            main.style.paddingBottom = 12;
            main.style.paddingLeft = 12;
            main.style.color = new Color(0.863f, 0.863f, 0.902f);
            main.style.fontSize = 14;
            main.style.flexDirection = FlexDirection.Column;
            main.style.alignItems = Align.Stretch;
        }

        public void Toggle()
        {
            bool currentVisible = _mainContainer != null && _mainContainer.style.display == DisplayStyle.Flex;
            bool newVisible = !currentVisible;
            Debug.Log($"[MarketWindow] Toggle: current={currentVisible} → new={newVisible}");
            SetVisible(newVisible);
        }

        /// <summary>
        /// Показать окно (вызывается из MarketInteractor.TryOpenMarket после подписки).
        /// </summary>
        public void Show()
        {
            // FIX: idempotent — если по какой-то причине дерево потеряно
            // (UIDocument пересоздал rootVE, GO toggle, и т.п.) — пересоберём.
            if (!_built || _mainContainer == null || !IsLayoutValid())
            {
                Debug.LogWarning("[MarketWindow] Show(): UI not built or layout invalid, rebuilding");
                EnsureBuilt();
            }
            // FIX (2026-06-04): Включаем приём pointer events на root, чтобы клики
            // по самому окну (списки, кнопки) работали. Изначально Ignore —
            // чтобы UGUI Canvas (Host и пр.) получал клики когда окно закрыто.
            if (_root != null) _root.pickingMode = PickingMode.Position;
            SetVisible(true);

            // FIX: race с SubscribeMarketRpc — RPC асинхронный, snapshot придёт через
            // ~30-100мс. Если UI показать ДО snapshot, игрок видит placeholder
            // "Откройте рынок, чтобы торговать" и думает, что окно сломано.
            // Решение: пока snapshot не пришёл, показываем "Загрузка рынка..."
            // (HandleSnapshot перезапишет message когда снапшот придёт).
            if (_state == null || !_state.CurrentSnapshot.HasValue)
            {
                if (_messageLabel != null)
                {
                    _messageLabel.text = "Загрузка рынка...";
                    _messageLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.9f));
                }
            }

            _doc?.rootVisualElement?.MarkDirtyRepaint();
            // Force a layout update on the next frame (стили могут не примениться в текущем)
            if (_doc?.rootVisualElement != null)
            {
                _doc.rootVisualElement.schedule.Execute(() => _doc.rootVisualElement.MarkDirtyRepaint()).StartingIn(50);
            }

            // T-E04 FIX: запросить у сервера свежий snapshot инвентаря и склада.
            // Без этого при первом открытии обменника InventoryClientState может быть
            // ещё не заполнен → RefreshExchangeData покажет пустые списки.
            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState != null) invState.RequestRefresh();
            if (_state != null && !string.IsNullOrEmpty(_state.CurrentLocationId))
            {
                _state.RequestSubscribeMarket(_state.CurrentLocationId);
            }
            // Refresh сразу — если snapshot уже есть, покажем; если нет, OnSnapshotUpdated дёрнет ещё раз.
            RefreshExchangeData();
            if (_mainContainer != null)
            {
                var rs = _mainContainer.resolvedStyle;
                Debug.Log($"[MarketWindow] Show(): main w={rs.width:F0} h={rs.height:F0} pos={rs.position} bg={rs.backgroundColor}");
            }
        }

        /// <summary>
        /// Скрыть окно (вызывается по Esc внутри Update или по кнопке ЗАКРЫТЬ).
        /// </summary>
        public void Hide()
        {
            Debug.Log("[MarketWindow] Hide()");
            // FIX (2026-06-04): Выключаем приём pointer events — иначе невидимый
            // _root перехватывает клики у UGUI (Host и т.п.).
            if (_root != null) _root.pickingMode = PickingMode.Ignore;
            SetVisible(false);
        }

        public bool IsVisible()
        {
            return _mainContainer != null && _mainContainer.style.display == DisplayStyle.Flex;
        }

        private ulong GetSelectedShipId()
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue || snap.Value.nearbyShips == null) return 0;
            if (_selectedShipIndex < 0 || _selectedShipIndex >= snap.Value.nearbyShips.Length) return 0;
            return snap.Value.nearbyShips[_selectedShipIndex].shipNetworkObjectId;
        }

        private string GetSelectedShipName()
        {
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue || snap.Value.nearbyShips == null) return "—";
            if (_selectedShipIndex < 0 || _selectedShipIndex >= snap.Value.nearbyShips.Length) return "—";
            return snap.Value.nearbyShips[_selectedShipIndex].displayName;
        }

        private List<WarehouseEntryDto> SnapCargo(MarketSnapshotDto snap)
        {
            // Cargo не входит в MarketSnapshotDto (слишком жирно слать груз на каждый tick).
            // Возвращаем локальный кэш, обновляемый из TradeResultDto (Load/Unload).
            return _cargoCache != null ? new List<WarehouseEntryDto>(_cargoCache) : new List<WarehouseEntryDto>();
        }

        /// <summary>
        /// FIX (2026-06-05): мгновенно подменить UI cargo на cargo выбранного корабля
        /// из per-ship клиентского кэша <see cref="MarketClientState.CurrentShipCargos"/>.
        /// Вызывается из ship-selector callback (мгновенный отклик UI) и из
        /// <see cref="HandleSnapshot"/> (синхронизация с сервером, если сервер
        /// прислал cargo для текущего выбора — например, после SetSelectedShipRpc
        /// safety net).
        /// Если корабля нет в кэше — показываем пустой cargo (новый корабль, ещё
        /// не было snapshot с ним; следующий snapshot/TradeResult обновит).
        /// </summary>
        private void ApplySelectedShipCargoFromCache(ulong shipNetworkObjectId)
        {
            WarehouseEntryDto[] newCargo = Array.Empty<WarehouseEntryDto>();
            if (_state != null && _state.CurrentShipCargos != null
                && _state.CurrentShipCargos.TryGetValue(shipNetworkObjectId, out var cached))
            {
                newCargo = cached ?? Array.Empty<WarehouseEntryDto>();
            }
            _cargoCache = newCargo;
            if (_cargoList != null)
            {
                _cargoList.itemsSource = _cargoCache;
                _selectedCargoItem = -1;
                _cargoList.selectedIndex = -1;
                _cargoList.Rebuild();
            }
        }

        private static string LocalizeOp(TradeOp op)
        {
            switch (op)
            {
                case TradeOp.Buy: return "Куплено";
                case TradeOp.Sell: return "Продано";
                case TradeOp.LoadToShip: return "Погрузка";
                case TradeOp.UnloadFromShip: return "Разгрузка";
                default: return op.ToString();
            }
        }

        // ========================================================
        // EXCHANGE TAB (T-E04: Resources Exchanger)
        // ========================================================

        /// <summary>
        /// T-E04: Row factory для списков обменника.
        /// Переиспользуется для exchange-inventory-list и exchange-warehouse-list.
        /// </summary>
        private static VisualElement MakeExchangeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("exchange-row");
            var label = new Label { name = "row-label" };
            label.AddToClassList("exchange-row-label");
            row.Add(label);
            var qty = new Label { name = "row-qty" };
            qty.AddToClassList("exchange-row-qty");
            row.Add(qty);
            return row;
        }

        /// <summary>
        /// T-E04: Binder для левого списка (инвентарь → упаковка на склад).
        /// </summary>
        private void BindExchangeInvRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _exchangeInvCache.Count) return;
            var item = _exchangeInvCache[index];
            row.Q<Label>("row-label").text = item.displayName;
            row.Q<Label>("row-qty").text = $"{item.haveQty} → {item.maxPacks} пач.";
            row.style.backgroundColor = (index == _selectedExchangeInvItem)
                ? new StyleColor(new Color(0.4f, 0.8f, 0.8f, 0.4f))
                : StyleKeyword.Null;
        }

        /// <summary>
        /// T-E04: Binder для правого списка (склад → распаковка в инвентарь).
        /// </summary>
        private void BindExchangeWhRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _exchangeWhCache.Count) return;
            var item = _exchangeWhCache[index];
            row.Q<Label>("row-label").text = item.displayName;
            row.Q<Label>("row-qty").text = $"{item.haveQty} → {item.maxPacks} пач.";
            row.style.backgroundColor = (index == _selectedExchangeWhItem)
                ? new StyleColor(new Color(0.8f, 0.6f, 0.4f, 0.4f))
                : StyleKeyword.Null;
        }

        /// <summary>
        /// T-E04: Обновляет кэши exchange-списков из InventoryClientState + MarketClientState.
        /// </summary>
        private void RefreshExchangeData(ProjectC.Items.Dto.InventorySnapshotDto _ = default)
        {
            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            var snap = _state?.CurrentSnapshot;
            var wh = snap?.warehouse ?? Array.Empty<WarehouseEntryDto>();

            _exchangeInvCache.Clear();
            _exchangeWhCache.Clear();

            if (invState != null)
            {
                var invItems = invState.GetItems();
                if (invItems != null)
                {
                    // T-E04 FIX: InventoryData ещё не стэкает (каждый id = 1 запись, quantity=1 в snapshot).
                    // Группируем по itemId, чтобы получить реальное количество для Pack.
                    var grouped = new System.Collections.Generic.Dictionary<int, int>(); // itemId → кол-во записей
                    foreach (var inv in invItems)
                    {
                        if (!grouped.ContainsKey(inv.itemId)) grouped[inv.itemId] = 0;
                        grouped[inv.itemId]++;
                    }

                    foreach (var kvp in grouped)
                    {
                        int itemId = kvp.Key;
                        int count = kvp.Value; // кол-во stack-записей (= кол-ву предметов пока без stacking)
                        var def = invState.GetItemDefinition(itemId);
                        if (def == null) continue;
                        string name = def.itemName;
                        // Ищем rate по имени предмета
                        var resolver = ResourceExchangeResolver.Default;
                        if (resolver == null) continue;
                        var rate = resolver.FindRateForItemName(name);
                        if (rate == null) continue;
                        var entry = rate.Value;
                        int packs = count / entry.inventoryQty;
                        if (packs <= 0) continue;
                        _exchangeInvCache.Add(new ItemRow
                        {
                            displayName = $"{name} ×{count} ({def.itemType})",
                            haveQty = count,
                            maxPacks = packs,
                            inventoryQty = entry.inventoryQty,
                            warehouseQty = entry.warehouseQty,
                            inventoryItemId = itemId,
                        });
                    }
                }
            }

            // Правый список: товары склада, для которых есть rate (boxed items)
            foreach (var entry in wh)
            {
                var resolver = ResourceExchangeResolver.Default;
                if (resolver == null) continue;
                var rate = resolver.FindRateForWarehouseItem(entry.itemId);
                if (rate == null) continue;
                var whRate = rate.Value;
                int boxes = entry.quantity / whRate.warehouseQty;
                if (boxes <= 0) continue;
                _exchangeWhCache.Add(new ItemRow
                {
                    displayName = $"{entry.displayName} (ящ.)",
                    haveQty = entry.quantity,
                    maxPacks = boxes,
                    inventoryQty = whRate.inventoryQty,
                    warehouseQty = whRate.warehouseQty,
                    warehouseItemId = entry.itemId,
                });
            }

            if (_exchangeInvList != null)
            {
                _exchangeInvList.itemsSource = _exchangeInvCache;
                _exchangeInvList.Rebuild();
            }
            if (_exchangeWhList != null)
            {
                _exchangeWhList.itemsSource = _exchangeWhCache;
                _exchangeWhList.Rebuild();
            }
        }

        /// <summary>
        /// T-E04: Обработчик результата Pack/Unpack от ExchangeServer.
        /// Обновляет message-label и перезапрашивает данные.
        /// </summary>
        private void HandleExchangeResult(ExchangeResultDto result)
        {
            if (_messageLabel != null)
            {
                if (result.success)
                {
                    _messageLabel.text = $"Обмен: OK (Δ склад={result.warehouseDelta}, инвентарь={result.inventoryDelta})";
                    _messageLabel.style.color = new StyleColor(new Color(0.4f, 0.95f, 0.4f));
                }
                else
                {
                    _messageLabel.text = $"Ошибка: {result.message}";
                    _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
                }
            }
            // Перезапрашиваем данные — инвентарь обновится через OnSnapshotUpdated
            // (подписка уже висит), склад — через MarketClientState.
            // Явно вызываем RefreshExchangeData для мгновенного UI.
            RefreshExchangeData();
        }

        /// <summary>
        /// T-E04: Упаковать выбранный предмет инвентаря → склад.
        /// </summary>
        private void OnPackClicked()
        {
            if (_selectedExchangeInvItem < 0 || _selectedExchangeInvItem >= _exchangeInvCache.Count)
            {
                if (_messageLabel != null)
                {
                    _messageLabel.text = "Выберите предмет в левом списке";
                    _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.8f, 0.4f));
                }
                return;
            }
            var item = _exchangeInvCache[_selectedExchangeInvItem];
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue)
            {
                if (_messageLabel != null)
                {
                    _messageLabel.text = "Нет данных о рынке";
                    _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
                }
                return;
            }

            // Шлём RPC серверу через ExchangeServer.Instance
            var ex = Network.ExchangeServer.Instance;
            if (ex == null)
            {
                if (_messageLabel != null)
                {
                    _messageLabel.text = "Сервер обменника не доступен";
                    _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
                }
                return;
            }
            // T-E04: countToRemove = кол-во ЕДИНИЦ инвентаря (не паков!).
            // 1 pack = inventoryQty единиц. Клиент запаковывает 1 пачку за раз → шлёт inventoryQty.
            int countToRemove = item.inventoryQty > 0 ? item.inventoryQty : 1;
            ex.RequestPackRpc(snap.Value.locationId, item.inventoryItemId, countToRemove);
            if (_messageLabel != null)
            {
                _messageLabel.text = $"Отправлен запрос на упаковку {item.displayName}...";
                _messageLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 1.0f));
            }
        }

        /// <summary>
        /// T-E04: Распаковать выбранный товар склада → инвентарь.
        /// </summary>
        private void OnUnpackClicked()
        {
            if (_selectedExchangeWhItem < 0 || _selectedExchangeWhItem >= _exchangeWhCache.Count)
            {
                if (_messageLabel != null)
                {
                    _messageLabel.text = "Выберите товар в правом списке";
                    _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.8f, 0.4f));
                }
                return;
            }
            var item = _exchangeWhCache[_selectedExchangeWhItem];
            var snap = _state?.CurrentSnapshot;
            if (!snap.HasValue)
            {
                if (_messageLabel != null)
                {
                    _messageLabel.text = "Нет данных о рынке";
                    _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
                }
                return;
            }

            var ex = Network.ExchangeServer.Instance;
            if (ex == null)
            {
                Debug.LogError("[MarketWindow][Unpack] ExchangeServer.Instance == null — RPC не отправляется!");
                if (_messageLabel != null)
                {
                    _messageLabel.text = "Сервер обменника не инициализирован. Подождите пару секунд.";
                    _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
                }
                return;
            }
            // T-E04: countToRemove = кол-во КОРОБОК склада. 1 распаковка = warehouseQty коробок.
            int countToRemove = item.warehouseQty > 0 ? item.warehouseQty : 1;
            ex.RequestUnpackRpc(snap.Value.locationId, item.warehouseItemId, countToRemove);
            if (_messageLabel != null)
            {
                _messageLabel.text = $"Отправлен запрос на распаковку {item.displayName}...";
                _messageLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 1.0f));
            }
        }
    }
}
