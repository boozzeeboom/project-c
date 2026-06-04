using System;
using System.Collections.Generic;
using ProjectC.Trade.Dto;
using UnityEngine;
using UnityEngine.UIElements;

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
        private VisualElement _itemSection;        // FIX: wrapper вокруг item-list + title
        private VisualElement _warehouseSection;   // FIX: wrapper вокруг warehouse-list + title
        private VisualElement _cargoSection;       // FIX: wrapper вокруг cargo-list + title
        private VisualElement _shipSelectorContainer;
        private DropdownField _shipSelector;
        private Button _buyBtn;
        private Button _sellBtn;
        private Button _loadBtn;
        private Button _unloadBtn;
        private Button _closeBtn;
        private Label _messageLabel;
        private TextField _qtyField;

        // State
        private int _selectedMarketItem = -1;
        private int _selectedWarehouseItem = -1;
        private int _selectedCargoItem = -1;
        private int _selectedShipIndex = 0;
        private string _activeTab = "market"; // "market" / "warehouse"

        // Локальный кэш cargo выбранного корабля (обновляется из TradeResultDto).
        // В snapshot cargo не входит (слишком жирно слать весь груз на каждый tick),
        // но после каждой операции (Load/Unload) сервер присылает updatedCargoSnapshot.
        private WarehouseEntryDto[] _cargoCache = Array.Empty<WarehouseEntryDto>();

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
            _itemSection = _root.Q<VisualElement>("item-section");
            _warehouseSection = _root.Q<VisualElement>("warehouse-section");
            _cargoSection = _root.Q<VisualElement>("cargo-section");
            _shipSelectorContainer = _root.Q<VisualElement>("ship-selector-container");
            _shipSelector = _root.Q<DropdownField>("ship-selector");
            _buyBtn = _root.Q<Button>("buy-btn");
            _sellBtn = _root.Q<Button>("sell-btn");
            _loadBtn = _root.Q<Button>("load-btn");
            _unloadBtn = _root.Q<Button>("unload-btn");
            _closeBtn = _root.Q<Button>("close-btn");
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

            var marketTabBtn = _root.Q<Button>("tab-market");
            var warehouseTabBtn = _root.Q<Button>("tab-warehouse");
            if (marketTabBtn != null) marketTabBtn.clicked += () => SwitchTab("market");
            if (warehouseTabBtn != null) warehouseTabBtn.clicked += () => SwitchTab("warehouse");

            if (_buyBtn != null) _buyBtn.clicked += OnBuyClicked;
            if (_sellBtn != null) _sellBtn.clicked += OnSellClicked;
            if (_loadBtn != null) _loadBtn.clicked += OnLoadClicked;
            if (_unloadBtn != null) _unloadBtn.clicked += OnUnloadClicked;
            if (_closeBtn != null) _closeBtn.clicked += OnCloseClicked;

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
        }

        private void Update()
        {
            // FIX: убран E-handler — он дублировал NetworkPlayer.cs и открывал окно ВЕЗДЕ,
            // даже вне зоны MarketZone. Открытие теперь идёт ТОЛЬКО через NetworkPlayer
            // → MarketInteractor.TryOpenMarket() (только в зоне).
            // Здесь оставлен только Esc для закрытия открытого окна.
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            if (!nm.IsClient && !nm.IsServer) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsVisible())
            {
                Hide();
            }
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
            row.Q<Label>("row-label").text = $"{it.displayName}  —  {it.currentPrice:F0} CR  (сток: {it.availableStock})";
            row.style.backgroundColor = (index == _selectedMarketItem) ? new StyleColor(new Color(0.4f, 0.6f, 0.9f, 0.4f)) : StyleKeyword.Null;
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

            // Обновляем кэш cargo при Load/Unload (сервер присылает updatedCargoSnapshot)
            if (result.IsSuccess && (result.op == TradeOp.LoadToShip || result.op == TradeOp.UnloadFromShip))
            {
                if (result.updatedCargoSnapshot != null)
                {
                    _cargoCache = result.updatedCargoSnapshot;
                    if (_cargoList != null)
                    {
                        _cargoList.itemsSource = _cargoCache;
                        _cargoList.Rebuild();
                    }
                }
            }
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

        private void SwitchTab(string tab)
        {
            _activeTab = tab;
            bool isMarket = tab == "market";
            // FIX: прячем ВСЮ секцию (заголовок + список), а не только ListView —
            // иначе 3 заголовка "Товары на рынке / Ваш склад / Груз корабля" висят одновременно
            // и контейнер сжимается через flex-shrink, ломая layout.
            if (_itemSection != null) _itemSection.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_warehouseSection != null) _warehouseSection.style.display = isMarket ? DisplayStyle.None : DisplayStyle.Flex;
            if (_cargoSection != null) _cargoSection.style.display = isMarket ? DisplayStyle.None : DisplayStyle.Flex;
            if (_buyBtn != null) _buyBtn.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_sellBtn != null) _sellBtn.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_loadBtn != null) _loadBtn.style.display = isMarket ? DisplayStyle.None : DisplayStyle.Flex;
            if (_unloadBtn != null) _unloadBtn.style.display = isMarket ? DisplayStyle.None : DisplayStyle.Flex;
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
    }
}
