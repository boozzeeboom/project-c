// =====================================================================================
// ShipCargoConsoleWindow.cs — UI Toolkit окно грузового отсека (T-CARGO-UI-02)
// =====================================================================================
// Паттерн: MarketWindow (UI Toolkit + singleton).
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
    public class ShipCargoConsoleWindow : MonoBehaviour
    {
        public static ShipCargoConsoleWindow Instance { get; private set; }

        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        // Кэш UI элементов
        private VisualElement _root;
        private Label _titleLabel;
        private ListView _invList;
        private ListView _cargoList;
        private Button _storeBtn;
        private Button _retrieveBtn;
        private Button _closeBtn;
        private Label _statusLabel;

        // Данные
        private List<InventoryEntry> _invCache = new List<InventoryEntry>();
        private List<CargoEntry> _cargoCache = new List<CargoEntry>();
        private int _selectedInvIndex = -1;
        private int _selectedCargoIndex = -1;

        // Текущий корабль
        private ulong _shipNetId;
        private string _shipName;

        // ============================================================
        // Data model
        // ============================================================

        private struct InventoryEntry
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
            public int inventoryItemId; // для обратного Retrieve
        }

        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            BuildUI();
            Hide();
        }

        private void OnDisable()
        {
            var invState = InventoryClientState.Instance;
            if (invState != null)
                invState.OnSnapshotUpdated -= RefreshData;

            var cargoState = ShipCargoClientState.Instance;
            if (cargoState != null)
                cargoState.OnResultReceived -= HandleResult;
        }

        // ============================================================
        // Build UI
        // ============================================================

        private void BuildUI()
        {
            if (_uiDocument == null)
            {
                Debug.LogError("[ShipCargoConsoleWindow] UIDocument not assigned");
                return;
            }

            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            _titleLabel = _root.Q<Label>("title-label");
            _invList = _root.Q<ListView>("inv-list");
            _cargoList = _root.Q<ListView>("cargo-list");
            _storeBtn = _root.Q<Button>("store-btn");
            _retrieveBtn = _root.Q<Button>("retrieve-btn");
            _closeBtn = _root.Q<Button>("close-btn");
            _statusLabel = _root.Q<Label>("status-label");

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
                _invList.selectionChanged += sel =>
                {
                    _selectedInvIndex = _invList.selectedIndex;
                };
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
                _cargoList.selectionChanged += sel =>
                {
                    _selectedCargoIndex = _cargoList.selectedIndex;
                };
            }

            if (_storeBtn != null) _storeBtn.clicked += OnStoreClicked;
            if (_retrieveBtn != null) _retrieveBtn.clicked += OnRetrieveClicked;
            if (_closeBtn != null) _closeBtn.clicked += Hide;
        }

        // ============================================================
        // Public API
        // ============================================================

        public void Show(ulong shipNetId, string shipDisplayName)
        {
            _shipNetId = shipNetId;
            _shipName = shipDisplayName;

            if (_root == null) BuildUI();
            if (_root == null) return;

            if (_titleLabel != null)
                _titleLabel.text = $"Грузовой отсек: {_shipName}";

            _root.style.display = DisplayStyle.Flex;

            // Подписки
            var invState = InventoryClientState.Instance;
            if (invState != null)
                invState.OnSnapshotUpdated += RefreshData;

            var cargoState = ShipCargoClientState.Instance;
            if (cargoState != null)
                cargoState.OnResultReceived += HandleResult;

            RefreshData();
        }

        public void Hide()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;

            var invState = InventoryClientState.Instance;
            if (invState != null)
                invState.OnSnapshotUpdated -= RefreshData;

            var cargoState = ShipCargoClientState.Instance;
            if (cargoState != null)
                cargoState.OnResultReceived -= HandleResult;

            _invCache.Clear();
            _cargoCache.Clear();
            _shipNetId = 0;
        }

        public bool IsVisible() => _root != null && _root.style.display == DisplayStyle.Flex;

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

            // Группируем по itemId
            var grouped = new Dictionary<int, int>();
            foreach (var inv in items)
            {
                if (!grouped.ContainsKey(inv.itemId)) grouped[inv.itemId] = 0;
                grouped[inv.itemId]++;
            }

            foreach (var kvp in grouped)
            {
                var def = invState.GetItemDefinition(kvp.Key);
                _invCache.Add(new InventoryEntry
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

                // Пытаемся найти inventoryItemId по имени предмета
                int invItemId = ResolveInventoryItemId(cd.itemId);

                _cargoCache.Add(new CargoEntry
                {
                    itemId = cd.itemId,
                    displayName = cd.displayName.ToString(),
                    count = cd.quantity,
                    inventoryItemId = invItemId,
                });
            }

            _cargoList?.Rebuild();
        }

        /// <summary>
        /// Резолвим строковый cargo itemId в числовой inventory itemId.
        /// Ищем в ItemDatabase по имени.
        /// </summary>
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
                if (def != null && def.itemName == cargoItemId)
                    return inv.itemId;
            }
            return 0;
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
            if (server == null)
            {
                SetStatus("Сервер грузового отсека не доступен", false);
                return;
            }

            server.RequestStoreToCargoRpc(_shipNetId, entry.itemId, entry.count);
            SetStatus($"Отправка {entry.displayName} ×{entry.count} в трюм...", true);
        }

        private void OnRetrieveClicked()
        {
            if (_shipNetId == 0) return;

            if (_selectedCargoIndex < 0 || _selectedCargoIndex >= _cargoCache.Count)
            {
                SetStatus("Выберите предмет в трюме", false);
                return;
            }

            var entry = _cargoCache[_selectedCargoIndex];
            if (entry.inventoryItemId <= 0)
            {
                SetStatus($"Не удалось сопоставить '{entry.itemId}' с предметом инвентаря", false);
                return;
            }

            var server = ShipCargoServer.Instance;
            if (server == null)
            {
                SetStatus("Сервер грузового отсека не доступен", false);
                return;
            }

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
