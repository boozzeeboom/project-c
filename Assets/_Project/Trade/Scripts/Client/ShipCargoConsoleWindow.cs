// =====================================================================================
// ShipCargoConsoleWindow.cs — UI Toolkit окно грузового отсека (T-CARGO-UI-02)
// =====================================================================================
// Паттерн: MarketWindow (UI Toolkit + singleton, канон из docs/UI/UI_TOOLKIT_GUIDE.md).
// Левая панель = инвентарь игрока, правая = cargo корабля.
// Кнопки: «В трюм», «Из трюма».
// =====================================================================================

using System;
using System.Collections.Generic;
using ProjectC.Items.Client;
using ProjectC.Ship.Client;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Network;
using UnityEngine;
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
        private VisualElement _container;
        private Label _titleLabel;
        private ListView _invList;
        private ListView _cargoList;
        private Button _storeBtn;
        private Button _retrieveBtn;
        private Button _closeBtn;
        private Label _statusLabel;
        private bool _built;

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
        }

        private struct CargoEntry
        {
            public string itemId;
            public string displayName;
            public int count;
            public int inventoryItemId;
        }

        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _doc = GetComponent<UIDocument>();

            // UXML fallback на Resources (VisualTreeAsset работает)
            if (_uxml == null)
                _uxml = Resources.Load<VisualTreeAsset>("UI/ShipCargoConsoleWindow");
            // USS fallback НЕ делаем — см. UI_TOOLKIT_GUIDE.md §2 Ошибка 1
        }

        private void OnEnable() => EnsureBuilt();

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
            if (_uxml == null)
            {
                Debug.LogError("[ShipCargoConsoleWindow] UXML не назначен!", this);
                return;
            }

            _root = _doc.rootVisualElement;
            _root.Clear();

            // Добавляем USS один раз
            if (_uss != null && !_root.styleSheets.Contains(_uss))
                _root.styleSheets.Add(_uss);

            _doc.sortingOrder = 10;

            // Клонируем UXML
            _container = _uxml.CloneTree();
            _root.Add(_container);

            // Ищем элементы
            _titleLabel = _container.Q<Label>("title-label");
            _invList = _container.Q<ListView>("inv-list");
            _cargoList = _container.Q<ListView>("cargo-list");
            _storeBtn = _container.Q<Button>("store-btn");
            _retrieveBtn = _container.Q<Button>("retrieve-btn");
            _closeBtn = _container.Q<Button>("close-btn");
            _statusLabel = _container.Q<Label>("status-label");

            // ListView setup
            if (_invList != null)
            {
                _invList.makeItem = () => new Label();
                _invList.bindItem = (e, i) =>
                {
                    if (i < 0 || i >= _invCache.Count) return;
                    var lbl = e as Label;
                    if (lbl == null) return;
                    var entry = _invCache[i];
                    lbl.text = $"{entry.displayName} ×{entry.count}";
                };
                _invList.itemsSource = _invCache;
                _invList.selectionChanged += _ => _selectedInvIndex = _invList.selectedIndex;
            }

            if (_cargoList != null)
            {
                _cargoList.makeItem = () => new Label();
                _cargoList.bindItem = (e, i) =>
                {
                    if (i < 0 || i >= _cargoCache.Count) return;
                    var lbl = e as Label;
                    if (lbl == null) return;
                    var entry = _cargoCache[i];
                    lbl.text = $"{entry.displayName} ×{entry.count}";
                };
                _cargoList.itemsSource = _cargoCache;
                _cargoList.selectionChanged += _ => _selectedCargoIndex = _cargoList.selectedIndex;
            }

            // De-dup подписок
            if (_storeBtn != null) { _storeBtn.clicked -= OnStoreClicked; _storeBtn.clicked += OnStoreClicked; }
            if (_retrieveBtn != null) { _retrieveBtn.clicked -= OnRetrieveClicked; _retrieveBtn.clicked += OnRetrieveClicked; }
            if (_closeBtn != null) { _closeBtn.clicked -= Hide; _closeBtn.clicked += Hide; }

            _built = true;
            SetVisible(false);

            Debug.Log($"[ShipCargoConsoleWindow] Built: root.children={_root.childCount}, styleSheets={_root.styleSheets.count}");
        }

        private void SetVisible(bool visible)
        {
            var target = _container ?? _root;
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
        }

        public bool IsVisible()
        {
            if (!_built) return false;
            var target = _container ?? _root;
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
        }

        private void TryUnsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            var invState = InventoryClientState.Instance;
            if (invState != null) invState.OnSnapshotUpdated -= RefreshData;

            var cargoState = ShipCargoClientState.Instance;
            if (cargoState != null) cargoState.OnResultReceived -= HandleResult;
        }

        // ============================================================
        // Data Refresh
        // ============================================================

        private void RefreshData(Items.Dto.InventorySnapshotDto _ = default)
        {
            RefreshInventory();
            RefreshCargo();
        }

        private void RefreshInventory()
        {
            _invCache.Clear();
            var invState = InventoryClientState.Instance;
            if (invState == null) return;

            var items = invState.GetItems();
            if (items == null) return;

            var grouped = new Dictionary<int, int>();
            foreach (var inv in items)
            {
                if (!grouped.ContainsKey(inv.itemId)) grouped[inv.itemId] = 0;
                grouped[inv.itemId]++;
            }

            foreach (var kvp in grouped)
            {
                var def = invState.GetItemDefinition(kvp.Key);
                _invCache.Add(new InvEntry
                {
                    itemId = kvp.Key,
                    displayName = def != null ? def.itemName : $"#{kvp.Key}",
                    count = kvp.Value,
                });
            }

            _invList?.Rebuild();
        }

        private void RefreshCargo()
        {
            _cargoCache.Clear();
            if (_shipNetId == 0) return;

            var telemetry = ShipTelemetryClientState.Instance;
            if (telemetry == null) return;

            var state = telemetry.GetShipState(_shipNetId);
            if (state == null) return;

            var cargoDetail = state.Value.cargoDetail;
            if (cargoDetail == null) return;

            foreach (var cd in cargoDetail)
            {
                if (cd.quantity <= 0) continue;
                _cargoCache.Add(new CargoEntry
                {
                    itemId = cd.itemId,
                    displayName = cd.displayName.ToString(),
                    count = cd.quantity,
                    inventoryItemId = ResolveInventoryItemId(cd.itemId),
                });
            }

            _cargoList?.Rebuild();
        }

        private int ResolveInventoryItemId(string cargoItemId)
        {
            if (string.IsNullOrEmpty(cargoItemId)) return 0;
            var invState = InventoryClientState.Instance;
            if (invState == null) return 0;
            var items = invState.GetItems();
            if (items == null) return 0;
            foreach (var inv in items)
            {
                var def = invState.GetItemDefinition(inv.itemId);
                if (def != null && def.itemName == cargoItemId) return inv.itemId;
            }
            return 0;
        }

        // ============================================================
        // Button Handlers
        // ============================================================

        private void OnStoreClicked()
        {
            if (_shipNetId == 0) return;
            if (_selectedInvIndex < 0 || _selectedInvIndex >= _invCache.Count) { SetStatus("Выберите предмет в инвентаре", false); return; }

            var entry = _invCache[_selectedInvIndex];
            var server = ShipCargoServer.Instance;
            if (server == null) { SetStatus("Сервер грузового отсека не доступен", false); return; }

            server.RequestStoreToCargoRpc(_shipNetId, entry.itemId, entry.count);
            SetStatus($"Отправка {entry.displayName} ×{entry.count} в трюм...", true);
        }

        private void OnRetrieveClicked()
        {
            if (_shipNetId == 0) return;
            if (_selectedCargoIndex < 0 || _selectedCargoIndex >= _cargoCache.Count) { SetStatus("Выберите предмет в трюме", false); return; }

            var entry = _cargoCache[_selectedCargoIndex];
            if (entry.inventoryItemId <= 0) { SetStatus($"Не удалось сопоставить '{entry.itemId}' с предметом инвентаря", false); return; }

            var server = ShipCargoServer.Instance;
            if (server == null) { SetStatus("Сервер грузового отсека не доступен", false); return; }

            server.RequestRetrieveFromCargoRpc(_shipNetId, entry.itemId, entry.count, entry.inventoryItemId);
            SetStatus($"Извлечение {entry.displayName} ×{entry.count} из трюма...", true);
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
