using System;
using System.Collections;
using System.Collections.Generic;
using ProjectC.Items.Client;
using ProjectC.Localization;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Контроллер вкладки ОБМЕННИК.
    /// Изолирует inventory/warehouse projection, Pack/Unpack actions и feedback.
    /// </summary>
    public sealed class ExchangeTabController
    {
        private readonly MarketWindowHost _host;
        private readonly Label _messageLabel;
        private VisualElement _exchangeSection;
        private ListView _inventoryList;
        private ListView _warehouseList;
        private Button _packButton;
        private Button _unpackButton;
        private MarketClientState _marketState;
        private ExchangeClientState _exchangeState;
        private InventoryClientState _inventoryState;
        private readonly List<ItemRow> _inventoryCache = new List<ItemRow>();
        private readonly List<ItemRow> _warehouseCache = new List<ItemRow>();
        private int _selectedInventoryItem = -1;
        private int _selectedWarehouseItem = -1;
        private bool _subscribed;

        private struct ItemRow
        {
            public string displayName;
            public int haveQty;
            public int maxPacks;
            public int inventoryQty;
            public int warehouseQty;
            public string warehouseItemId;
            public int inventoryItemId;
        }

        public ExchangeTabController(MarketWindowHost host, Label messageLabel)
        {
            _host = host;
            _messageLabel = messageLabel;
        }

        public void BuildUI(VisualElement root)
        {
            _exchangeSection = root.Q<VisualElement>("exchange-section");
            _inventoryList = root.Q<ListView>("exchange-inventory-list");
            _warehouseList = root.Q<ListView>("exchange-warehouse-list");
            _packButton = root.Q<Button>("pack-btn");
            _unpackButton = root.Q<Button>("unpack-btn");

            if (_inventoryList != null)
            {
                _inventoryList.makeItem = MakeExchangeRow;
                _inventoryList.bindItem = BindInventoryRow;
                _inventoryList.fixedItemHeight = 30;
                _inventoryList.selectionType = SelectionType.Single;
                _inventoryList.selectedIndex = -1;
                _inventoryList.selectionChanged += selectedItems =>
                {
                    _selectedInventoryItem = FindSelectedItemIndex(_inventoryList, selectedItems);
                };
            }
            if (_warehouseList != null)
            {
                _warehouseList.makeItem = MakeExchangeRow;
                _warehouseList.bindItem = BindWarehouseRow;
                _warehouseList.fixedItemHeight = 30;
                _warehouseList.selectionType = SelectionType.Single;
                _warehouseList.selectedIndex = -1;
                _warehouseList.selectionChanged += selectedItems =>
                {
                    _selectedWarehouseItem = FindSelectedItemIndex(_warehouseList, selectedItems);
                };
            }

            if (_packButton != null)
            {
                _packButton.text = Loc.Get("ui.market.btn.pack");
                _packButton.clicked += OnPackClicked;
            }
            if (_unpackButton != null)
            {
                _unpackButton.text = Loc.Get("ui.market.btn.unpack");
                _unpackButton.clicked += OnUnpackClicked;
            }
        }

        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            _marketState = MarketClientState.Instance;
            _exchangeState = ExchangeClientState.Instance;
            _inventoryState = ProjectC.Items.Client.InventoryClientState.Instance;

            if (_marketState != null) _marketState.OnSnapshotUpdated += HandleMarketSnapshot;
            if (_exchangeState != null) _exchangeState.OnResultReceived += HandleExchangeResult;
            if (_inventoryState != null) _inventoryState.OnSnapshotUpdated += RefreshData;
            RefreshData();
        }

        public void Unsubscribe()
        {
            if (_marketState != null) _marketState.OnSnapshotUpdated -= HandleMarketSnapshot;
            if (_exchangeState != null) _exchangeState.OnResultReceived -= HandleExchangeResult;
            if (_inventoryState != null) _inventoryState.OnSnapshotUpdated -= RefreshData;
            _marketState = null;
            _exchangeState = null;
            _inventoryState = null;
            _subscribed = false;
        }

        public void SetTabVisible(bool visible)
        {
            if (_exchangeSection != null)
                _exchangeSection.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_packButton != null) _packButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_unpackButton != null) _unpackButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) RefreshData();
        }

        public void RefreshData(ProjectC.Items.Dto.InventorySnapshotDto ignored = default)
        {
            var inventoryState = _inventoryState != null ? _inventoryState : ProjectC.Items.Client.InventoryClientState.Instance;
            var marketState = _marketState != null ? _marketState : MarketClientState.Instance;
            var snapshot = marketState != null ? marketState.CurrentSnapshot : null;
            var warehouse = snapshot.HasValue && snapshot.Value.warehouse != null
                ? snapshot.Value.warehouse
                : Array.Empty<WarehouseEntryDto>();

            _inventoryCache.Clear();
            _warehouseCache.Clear();

            if (inventoryState != null)
            {
                var grouped = new Dictionary<int, int>();
                var items = inventoryState.GetItems();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (!grouped.ContainsKey(item.itemId)) grouped[item.itemId] = 0;
                        grouped[item.itemId]++;
                    }
                }

                foreach (var pair in grouped)
                {
                    var definition = inventoryState.GetItemDefinition(pair.Key);
                    if (definition == null) continue;
                    var resolver = ResourceExchangeResolver.Default;
                    if (resolver == null) continue;
                    var rate = resolver.FindRateForItemName(definition.itemName);
                    if (!rate.HasValue) continue;
                    var exchangeRate = rate.Value;
                    int packs = pair.Value / exchangeRate.inventoryQty;
                    if (packs <= 0) continue;
                    _inventoryCache.Add(new ItemRow
                    {
                        displayName = $"{definition.itemName} ×{pair.Value} ({definition.itemType})",
                        haveQty = pair.Value,
                        maxPacks = packs,
                        inventoryQty = exchangeRate.inventoryQty,
                        warehouseQty = exchangeRate.warehouseQty,
                        inventoryItemId = pair.Key
                    });
                }
            }

            var warehouseResolver = ResourceExchangeResolver.Default;
            if (warehouseResolver != null)
            {
                foreach (var entry in warehouse)
                {
                    var rate = warehouseResolver.FindRateForWarehouseItem(entry.itemId);
                    if (!rate.HasValue) continue;
                    var exchangeRate = rate.Value;
                    int boxes = entry.quantity / exchangeRate.warehouseQty;
                    if (boxes <= 0) continue;
                    _warehouseCache.Add(new ItemRow
                    {
                        displayName = entry.displayName + " (" + Loc.Get("ui.market.boxed_suffix", "boxes") + ")",
                        haveQty = entry.quantity,
                        maxPacks = boxes,
                        inventoryQty = exchangeRate.inventoryQty,
                        warehouseQty = exchangeRate.warehouseQty,
                        warehouseItemId = entry.itemId
                    });
                }
            }

            SetListSource(_inventoryList, _inventoryCache);
            SetListSource(_warehouseList, _warehouseCache);
            _selectedInventoryItem = -1;
            _selectedWarehouseItem = -1;
        }

        private void HandleMarketSnapshot(MarketSnapshotDto snapshot)
        {
            RefreshData();
        }

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
            RefreshData();
        }

        private static VisualElement MakeExchangeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("exchange-row");
            var label = new Label { name = "row-label" };
            label.AddToClassList("exchange-row-label");
            row.Add(label);
            var quantity = new Label { name = "row-qty" };
            quantity.AddToClassList("exchange-row-qty");
            row.Add(quantity);
            return row;
        }

        private void BindInventoryRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _inventoryCache.Count) return;
            var item = _inventoryCache[index];
            row.Q<Label>("row-label").text = item.displayName;
            row.Q<Label>("row-qty").text = $"{item.haveQty} → {item.maxPacks} {Loc.Get("ui.market.packs_suffix", "packs")}";
            row.style.backgroundColor = index == _selectedInventoryItem
                ? new StyleColor(new Color(0.4f, 0.8f, 0.8f, 0.4f))
                : StyleKeyword.Null;
        }

        private void BindWarehouseRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _warehouseCache.Count) return;
            var item = _warehouseCache[index];
            row.Q<Label>("row-label").text = item.displayName;
            row.Q<Label>("row-qty").text = $"{item.haveQty} → {item.maxPacks} {Loc.Get("ui.market.packs_suffix", "packs")}";
            row.style.backgroundColor = index == _selectedWarehouseItem
                ? new StyleColor(new Color(0.8f, 0.6f, 0.4f, 0.4f))
                : StyleKeyword.Null;
        }

        private void OnPackClicked()
        {
            if (_selectedInventoryItem < 0 || _selectedInventoryItem >= _inventoryCache.Count)
            {
                _host.SetMessage(Loc.Get("ui.market.select_left"), true);
                return;
            }
            var snapshot = _marketState != null ? _marketState.CurrentSnapshot : null;
            if (!snapshot.HasValue)
            {
                _host.SetMessage(Loc.Get("ui.market.no_data"), true);
                return;
            }
            var exchangeServer = Network.ExchangeServer.Instance;
            if (exchangeServer == null)
            {
                _host.SetMessage(Loc.Get("ui.market.server_unavailable"), true);
                return;
            }
            var item = _inventoryCache[_selectedInventoryItem];
            int countToRemove = item.inventoryQty > 0 ? item.inventoryQty : 1;
            exchangeServer.RequestPackRpc(snapshot.Value.locationId, item.inventoryItemId, countToRemove);
            _host.SetMessage($"Отправлен запрос на упаковку {item.displayName}...");
        }

        private void OnUnpackClicked()
        {
            if (_selectedWarehouseItem < 0 || _selectedWarehouseItem >= _warehouseCache.Count)
            {
                _host.SetMessage(Loc.Get("ui.market.select_right"), true);
                return;
            }
            var snapshot = _marketState != null ? _marketState.CurrentSnapshot : null;
            if (!snapshot.HasValue)
            {
                _host.SetMessage(Loc.Get("ui.market.no_data"), true);
                return;
            }
            var exchangeServer = Network.ExchangeServer.Instance;
            if (exchangeServer == null)
            {
                _host.SetMessage(Loc.Get("ui.market.server_not_ready"), true);
                return;
            }
            var item = _warehouseCache[_selectedWarehouseItem];
            int countToRemove = item.warehouseQty > 0 ? item.warehouseQty : 1;
            exchangeServer.RequestUnpackRpc(snapshot.Value.locationId, item.warehouseItemId, countToRemove);
            _host.SetMessage($"Отправлен запрос на распаковку {item.displayName}...");
        }

        private static void SetListSource(ListView list, IList source)
        {
            if (list == null) return;
            list.itemsSource = null;
            list.Rebuild();
            list.itemsSource = source;
            list.selectedIndex = -1;
            list.Rebuild();
        }

        private static int FindSelectedItemIndex(ListView list, IEnumerable<object> selectedItems)
        {
            if (list == null || list.itemsSource == null || selectedItems == null) return -1;
            foreach (var selected in selectedItems)
            {
                for (int i = 0; i < list.itemsSource.Count; i++)
                    if (Equals(list.itemsSource[i], selected)) return i;
                break;
            }
            return -1;
        }
    }
}
