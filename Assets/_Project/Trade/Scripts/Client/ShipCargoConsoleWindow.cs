// =====================================================================================
// ShipCargoConsoleWindow.cs — UI Toolkit окно грузового отсека (T-CARGO-UI-02)
// =====================================================================================
// Паттерн: ExchangeServer + MarketWindow ExchangerTab (обмен ЧЕРЕЗ курс).
// Левая панель = инвентарь игрока (только packable), правая = cargo корабля (ящики).
// Кнопки: «→ В трюм» (pack), «← Из трюма» (unpack).
//
// ОБЯЗАТЕЛЬНО использует ResourceExchangeResolver.Default (DefaultExchangeRate.asset).
// Без курса предметы не показываются — НЕТ прямого 1:1 переноса.
// =====================================================================================

using System;
using System.Collections.Generic;
using ProjectC.Items.Client;
using ProjectC.Ship.Client;
using ProjectC.Trade.Config;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ProjectC.Trade.Client
{
    [RequireComponent(typeof(UIDocument))]
    public class ShipCargoConsoleWindow : MonoBehaviour
    {
        public static ShipCargoConsoleWindow Instance { get; private set; }

        [Header("UI Assets (назначить в Inspector)")]
        [SerializeField] private VisualTreeAsset _uxml;
        [SerializeField] private StyleSheet _uss;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _backdrop;
        private VisualElement _panel;
        private Label _titleLabel;
        private ListView _invList;
        private ListView _cargoList;
        private Button _storeBtn;
        private Button _retrieveBtn;
        private Button _closeBtn;
        private Label _statusLabel;
        private bool _built;

        // Qty buttons
        private Button _invQtyMin, _invQtyMinus10, _invQtyMinus1, _invQtyPlus1, _invQtyPlus10, _invQtyMax;
        private Button _cargoQtyMin, _cargoQtyMinus10, _cargoQtyMinus1, _cargoQtyPlus1, _cargoQtyPlus10, _cargoQtyMax;
        private Label _invQtyLabel, _cargoQtyLabel;
        private int _invQty = 1;
        private int _cargoQty = 1;

        // Данные
        private readonly List<InvEntry> _invCache = new List<InvEntry>();
        private readonly List<CargoEntry> _cargoCache = new List<CargoEntry>();
        private int _selectedInvIndex = -1;
        private int _selectedCargoIndex = -1;
        private ulong _shipNetId;
        private string _shipName;
        private bool _subscribed;

        // ============================================================
        // Data model
        // ============================================================

        private struct InvEntry
        {
            public int itemId;
            public string displayName;
            public int count;
            public ExchangeRateEntry rate;
        }

        private struct CargoEntry
        {
            public string itemId;       // warehouseItemId
            public string displayName;  // из rate
            public int count;           // количество в трюме
            public ExchangeRateEntry rate;
        }

        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _doc = GetComponent<UIDocument>();

            if (_uxml == null)
                _uxml = Resources.Load<VisualTreeAsset>("UI/ShipCargoConsoleWindow");
        }

        private void OnEnable() => EnsureBuilt();

        private void Update()
        {
            if (IsVisible() && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
            }
        }

        private void OnDisable()
        {
            TryUnsubscribe();
            if (_built) SetVisible(false);
        }

        private void OnDestroy()
        {
            TryUnsubscribe();
            if (Instance == this) Instance = null;
        }

        // ============================================================
        // Build UI (канон: UI_TOOLKIT_GUIDE.md §3)
        // ============================================================

        private void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null || _doc.rootVisualElement == null) return;

            _root = _doc.rootVisualElement;

            if (_uss != null && !_root.styleSheets.Contains(_uss))
                _root.styleSheets.Add(_uss);

            _doc.sortingOrder = 10;

            _backdrop = _root.Q<VisualElement>("root") ?? _root;
            _panel = _root.Q<VisualElement>("panel");
            _titleLabel = _root.Q<Label>("title-label");
            _invList = _root.Q<ListView>("inv-list");
            _cargoList = _root.Q<ListView>("cargo-list");
            _storeBtn = _root.Q<Button>("store-btn");
            _retrieveBtn = _root.Q<Button>("retrieve-btn");
            _closeBtn = _root.Q<Button>("close-btn");
            _statusLabel = _root.Q<Label>("status-label");

            // Qty buttons — INV
            _invQtyMin     = _root.Q<Button>("inv-qty-min");
            _invQtyMinus10 = _root.Q<Button>("inv-qty-minus10");
            _invQtyMinus1  = _root.Q<Button>("inv-qty-minus1");
            _invQtyPlus1   = _root.Q<Button>("inv-qty-plus1");
            _invQtyPlus10  = _root.Q<Button>("inv-qty-plus10");
            _invQtyMax     = _root.Q<Button>("inv-qty-max");
            _invQtyLabel   = _root.Q<Label>("inv-qty-label-value");

            // Qty buttons — CARGO
            _cargoQtyMin     = _root.Q<Button>("cargo-qty-min");
            _cargoQtyMinus10 = _root.Q<Button>("cargo-qty-minus10");
            _cargoQtyMinus1  = _root.Q<Button>("cargo-qty-minus1");
            _cargoQtyPlus1   = _root.Q<Button>("cargo-qty-plus1");
            _cargoQtyPlus10  = _root.Q<Button>("cargo-qty-plus10");
            _cargoQtyMax     = _root.Q<Button>("cargo-qty-max");
            _cargoQtyLabel   = _root.Q<Label>("cargo-qty-label-value");

            // Inventory ListView
            if (_invList != null)
            {
                _invList.makeItem = () =>
                {
                    var lbl = new Label();
                    lbl.AddToClassList("cargo-console-list-item");
                    return lbl;
                };
                _invList.bindItem = (e, i) =>
                {
                    if (i < 0 || i >= _invCache.Count) return;
                    var lbl = e as Label;
                    if (lbl == null) return;
                    var entry = _invCache[i];
                    int packs = entry.count / entry.rate.inventoryQty;
                    lbl.text = $"{entry.displayName}  ×{entry.count}  (→ {packs} ящ.)";
                };
                _invList.itemsSource = _invCache;
                _invList.selectionChanged += _ => { _selectedInvIndex = _invList.selectedIndex; _invQty = 1; UpdateInvQtyLabel(); };
            }

            // Cargo ListView
            if (_cargoList != null)
            {
                _cargoList.makeItem = () =>
                {
                    var lbl = new Label();
                    lbl.AddToClassList("cargo-console-list-item");
                    return lbl;
                };
                _cargoList.bindItem = (e, i) =>
                {
                    if (i < 0 || i >= _cargoCache.Count) return;
                    var lbl = e as Label;
                    if (lbl == null) return;
                    var entry = _cargoCache[i];
                    int unpacks = entry.count / entry.rate.warehouseQty;
                    lbl.text = $"{entry.displayName}  ×{entry.count}  (→ {unpacks * entry.rate.inventoryQty} шт.)";
                };
                _cargoList.itemsSource = _cargoCache;
                _cargoList.selectionChanged += _ => { _selectedCargoIndex = _cargoList.selectedIndex; _cargoQty = 1; UpdateCargoQtyLabel(); };
            }

            // De-dup подписок
            if (_storeBtn != null) { _storeBtn.clicked -= OnStoreClicked; _storeBtn.clicked += OnStoreClicked; }
            if (_retrieveBtn != null) { _retrieveBtn.clicked -= OnRetrieveClicked; _retrieveBtn.clicked += OnRetrieveClicked; }
            if (_closeBtn != null) { _closeBtn.clicked -= Hide; _closeBtn.clicked += Hide; }

            // Qty button handlers — INV
            if (_invQtyMin     != null) _invQtyMin.clicked     += () => SetInvQty(1);
            if (_invQtyMinus10 != null) _invQtyMinus10.clicked += () => AdjustInvQty(-10);
            if (_invQtyMinus1  != null) _invQtyMinus1.clicked  += () => AdjustInvQty(-1);
            if (_invQtyPlus1   != null) _invQtyPlus1.clicked   += () => AdjustInvQty(1);
            if (_invQtyPlus10  != null) _invQtyPlus10.clicked  += () => AdjustInvQty(10);
            if (_invQtyMax     != null) _invQtyMax.clicked     += SetInvQtyMax;

            // Qty button handlers — CARGO
            if (_cargoQtyMin     != null) _cargoQtyMin.clicked     += () => SetCargoQty(1);
            if (_cargoQtyMinus10 != null) _cargoQtyMinus10.clicked += () => AdjustCargoQty(-10);
            if (_cargoQtyMinus1  != null) _cargoQtyMinus1.clicked  += () => AdjustCargoQty(-1);
            if (_cargoQtyPlus1   != null) _cargoQtyPlus1.clicked   += () => AdjustCargoQty(1);
            if (_cargoQtyPlus10  != null) _cargoQtyPlus10.clicked  += () => AdjustCargoQty(10);
            if (_cargoQtyMax     != null) _cargoQtyMax.clicked     += SetCargoQtyMax;

            _built = true;
            SetVisible(false);

            Debug.Log($"[ShipCargoConsoleWindow] Built: root.children={_root.childCount}, styleSheets={_root.styleSheets.count}");
        }

        private void SetVisible(bool visible)
        {
            var target = _backdrop ?? _panel ?? _root;
            if (target == null) return;
            target.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            target.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        }

        // ============================================================
        // Public API
        // ============================================================

        public void Show(ulong shipNetId, string shipDisplayName)
        {
            if (!_built) EnsureBuilt();
            if (!_built) return;

            _shipNetId = shipNetId;
            _shipName = shipDisplayName;

            if (_titleLabel != null)
                _titleLabel.text = $"Грузовой отсек: {_shipName}";

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            SetVisible(true);
            TrySubscribe();
            RefreshData();
        }

        public void Hide()
        {
            TryUnsubscribe();
            SetVisible(false);
            _invCache.Clear();
            _cargoCache.Clear();
            _shipNetId = 0;

            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        public bool IsVisible()
        {
            if (!_built) return false;
            var target = _backdrop ?? _panel ?? _root;
            return target != null && target.style.display == DisplayStyle.Flex;
        }

        // ============================================================
        // Subscribe / Unsubscribe
        // ============================================================

        private void TrySubscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            var invState = InventoryClientState.Instance;
            if (invState != null) invState.OnSnapshotUpdated += RefreshData;

            var cargoState = ShipCargoClientState.Instance;
            if (cargoState != null) cargoState.OnResultReceived += HandleResult;

            // Телеметрия корабля: NetworkVariable sync ~100-200ms, но OnShipStateChanged
            // срабатывает сразу при получении обновления — обновляем трюм без задержки.
            var telemetry = ShipTelemetryClientState.Instance;
            if (telemetry != null) telemetry.OnShipStateChanged += OnTelemetryUpdated;
        }

        private void TryUnsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            var invState = InventoryClientState.Instance;
            if (invState != null) invState.OnSnapshotUpdated -= RefreshData;

            var cargoState = ShipCargoClientState.Instance;
            if (cargoState != null) cargoState.OnResultReceived -= HandleResult;

            var telemetry = ShipTelemetryClientState.Instance;
            if (telemetry != null) telemetry.OnShipStateChanged -= OnTelemetryUpdated;
        }

        private void OnTelemetryUpdated(ulong shipNetId)
        {
            if (shipNetId == _shipNetId)
                RefreshCargo();
        }

        // ============================================================
        // Data Refresh (через ResourceExchangeResolver)
        // ============================================================

        private void RefreshData(Items.Dto.InventorySnapshotDto _ = default)
        {
            RefreshInventory();
            RefreshCargo();
        }

        private void RefreshInventory()
        {
            _invCache.Clear();
            var resolver = ResourceExchangeResolver.Default;
            if (resolver == null) return;

            var invState = InventoryClientState.Instance;
            if (invState == null) return;

            var items = invState.GetItems();
            if (items == null) return;

            // Группируем по itemId, только те что имеют курс обмена
            var grouped = new Dictionary<int, int>();
            foreach (var inv in items)
            {
                var def = invState.GetItemDefinition(inv.itemId);
                if (def == null) continue;
                var rate = resolver.FindRateForItemName(def.itemName);
                if (rate == null) continue; // нет курса → не показываем

                if (!grouped.ContainsKey(inv.itemId)) grouped[inv.itemId] = 0;
                grouped[inv.itemId]++;
            }

            foreach (var kvp in grouped)
            {
                var def = invState.GetItemDefinition(kvp.Key);
                var rate = resolver.FindRateForItemName(def.itemName);
                if (rate == null) continue;

                _invCache.Add(new InvEntry
                {
                    itemId = kvp.Key,
                    displayName = def.itemName,
                    count = kvp.Value,
                    rate = rate.Value,
                });
            }

            _invList?.Rebuild();
        }

        private void RefreshCargo()
        {
            _cargoCache.Clear();
            if (_shipNetId == 0) return;

            var resolver = ResourceExchangeResolver.Default;
            if (resolver == null) return;

            var telemetry = ShipTelemetryClientState.Instance;
            if (telemetry == null) return;

            var state = telemetry.GetShipState(_shipNetId);
            if (state == null) return;

            var cargoDetail = state.Value.cargoDetail;
            if (cargoDetail == null) return;

            foreach (var cd in cargoDetail)
            {
                if (cd.quantity <= 0) continue;

                var rate = resolver.FindRateForWarehouseItem(cd.itemId);
                if (rate == null) continue; // нет курса → не показываем

                _cargoCache.Add(new CargoEntry
                {
                    itemId = cd.itemId,
                    displayName = rate.Value.displayName,
                    count = cd.quantity,
                    rate = rate.Value,
                });
            }

            _cargoList?.Rebuild();
        }

        // ============================================================
        // Button Handlers
        // ============================================================

        private void OnStoreClicked()
        {
            if (_shipNetId == 0) return;
            if (_selectedInvIndex < 0 || _selectedInvIndex >= _invCache.Count)
            {
                SetStatus("Выберите предмет в инвентаре", false);
                return;
            }

            var entry = _invCache[_selectedInvIndex];
            var server = ShipCargoServer.Instance;
            if (server == null) { SetStatus("Сервер грузового отсека не доступен", false); return; }

            int itemsToSend = _invQty * entry.rate.inventoryQty;
            if (itemsToSend > entry.count) { SetStatus($"Недостаточно (есть {entry.count}, нужно {itemsToSend})", false); return; }

            server.RequestStoreToCargoRpc(_shipNetId, entry.itemId, itemsToSend);
            int boxes = _invQty * entry.rate.warehouseQty;
            SetStatus($"Упаковка {_invQty}× ({entry.displayName} ×{itemsToSend}) → {boxes} ящ. в трюм...", true);
            _invQty = 1; UpdateInvQtyLabel();
        }

        private void OnRetrieveClicked()
        {
            if (_shipNetId == 0) return;
            if (_selectedCargoIndex < 0 || _selectedCargoIndex >= _cargoCache.Count)
            {
                SetStatus("Выберите ящик в трюме", false);
                return;
            }

            var entry = _cargoCache[_selectedCargoIndex];
            var server = ShipCargoServer.Instance;
            if (server == null) { SetStatus("Сервер грузового отсека не доступен", false); return; }

            int boxesToSend = _cargoQty * entry.rate.warehouseQty;
            if (boxesToSend > entry.count) { SetStatus($"Недостаточно (есть {entry.count}, нужно {boxesToSend})", false); return; }

            server.RequestRetrieveFromCargoRpc(_shipNetId, entry.itemId, boxesToSend);
            int items = _cargoQty * entry.rate.inventoryQty;
            SetStatus($"Распаковка {_cargoQty}× ({entry.displayName} ×{boxesToSend}) → {items} шт. в инвентарь...", true);
            _cargoQty = 1; UpdateCargoQtyLabel();
        }

        // ============================================================
        // Qty adjustment (min/-10/-1/+1/+10/max)
        // ============================================================

        private void AdjustInvQty(int delta)
        {
            int max = GetInvQtyMax();
            _invQty = Mathf.Clamp(_invQty + delta, 1, max);
            UpdateInvQtyLabel();
        }
        private void SetInvQty(int v) { int max = GetInvQtyMax(); _invQty = Mathf.Clamp(v, 1, max); UpdateInvQtyLabel(); }
        private void SetInvQtyMax() { _invQty = GetInvQtyMax(); UpdateInvQtyLabel(); }
        private void UpdateInvQtyLabel() { if (_invQtyLabel != null) _invQtyLabel.text = _invQty.ToString(); }

        private int GetInvQtyMax()
        {
            if (_selectedInvIndex < 0 || _selectedInvIndex >= _invCache.Count) return 1;
            var entry = _invCache[_selectedInvIndex];
            return entry.count / entry.rate.inventoryQty;
        }

        private void AdjustCargoQty(int delta)
        {
            int max = GetCargoQtyMax();
            _cargoQty = Mathf.Clamp(_cargoQty + delta, 1, max);
            UpdateCargoQtyLabel();
        }
        private void SetCargoQty(int v) { int max = GetCargoQtyMax(); _cargoQty = Mathf.Clamp(v, 1, max); UpdateCargoQtyLabel(); }
        private void SetCargoQtyMax() { _cargoQty = GetCargoQtyMax(); UpdateCargoQtyLabel(); }
        private void UpdateCargoQtyLabel() { if (_cargoQtyLabel != null) _cargoQtyLabel.text = _cargoQty.ToString(); }

        private int GetCargoQtyMax()
        {
            if (_selectedCargoIndex < 0 || _selectedCargoIndex >= _cargoCache.Count) return 1;
            var entry = _cargoCache[_selectedCargoIndex];
            return entry.count / entry.rate.warehouseQty;
        }

        // ============================================================
        // Result Handler
        // ============================================================

        private void HandleResult(ShipCargoResultDto result)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = result.message;
                _statusLabel.style.color = result.success
                    ? new StyleColor(new Color(0.4f, 0.95f, 0.4f))
                    : new StyleColor(new Color(0.95f, 0.4f, 0.4f));
            }
            RefreshData();
        }

        private void SetStatus(string text, bool ok)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = text;
                _statusLabel.style.color = ok
                    ? new StyleColor(new Color(0.7f, 0.85f, 0.5f))
                    : new StyleColor(new Color(0.95f, 0.5f, 0.3f));
            }
        }
    }
}
