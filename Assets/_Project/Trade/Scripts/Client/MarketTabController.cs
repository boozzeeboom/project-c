using System;
using System.Collections;
using System.Collections.Generic;
using ProjectC.Localization;
using ProjectC.UI.Client;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Контроллер вкладок РЫНОК и СКЛАД / ТРЮМ.
    /// Владеет market snapshot, trade feedback, warehouse/cargo selection,
    /// quantity controls и per-ship cargo cache projection.
    /// </summary>
    public sealed class MarketTabController
    {
        private readonly MarketWindowHost _host;

        private VisualElement _root;
        private MarketClientState _state;
        private Label _locationLabel;
        private Label _creditsLabel;
        private Label _warehouseInfoLabel;
        private Label _timeInfoLabel;
        private Label _messageLabel;

        private VisualElement _itemSection;
        private VisualElement _warehouseSection;
        private VisualElement _cargoSection;
        private VisualElement _shipSelectorContainer;
        private CustomDropdown _shipSelector;
        private ListView _itemList;
        private ListView _warehouseList;
        private ListView _cargoList;
        private Button _buyBtn;
        private Button _sellBtn;
        private Button _loadBtn;
        private Button _unloadBtn;
        private Button _myItemsToggle;
        private Label _qtyLabel;
        private Label _warehouseQtyLabel;
        private Label _cargoWeightLabel;
        private Label _cargoSlotsLabel;
        private VisualElement _cargoBarFill;

        private Button _marketQtyMinus10;
        private Button _marketQtyMinus1;
        private Button _marketQtyPlus1;
        private Button _marketQtyPlus10;
        private Button _warehouseQtyMinus10;
        private Button _warehouseQtyMinus1;
        private Button _warehouseQtyPlus1;
        private Button _warehouseQtyPlus10;
        private Button _marketQtyMin;
        private Button _marketQtyMax;
        private Button _warehouseQtyMin;
        private Button _warehouseQtyMax;

        private int _marketQty = 1;
        private int _warehouseQty = 1;
        private int _selectedMarketItem = -1;
        private int _selectedWarehouseItem = -1;
        private int _selectedCargoItem = -1;
        private int _selectedShipIndex;
        private bool _myItemsOnly;

        private ItemPriceDto[] _marketItemsCache = Array.Empty<ItemPriceDto>();
        private WarehouseEntryDto[] _marketWhCache = Array.Empty<WarehouseEntryDto>();
        private WarehouseEntryDto[] _cargoCache = Array.Empty<WarehouseEntryDto>();
        private bool _subscribed;

        public MarketClientState State => _state != null ? _state : MarketClientState.Instance;
        public string CurrentLocationId => State != null ? State.CurrentLocationId : null;

        public MarketTabController(MarketWindowHost host)
        {
            _host = host;
        }

        public void BuildUI(
            VisualElement root,
            Label locationLabel,
            Label creditsLabel,
            Label warehouseInfoLabel,
            Label timeInfoLabel,
            Label messageLabel)
        {
            _root = root;
            _locationLabel = locationLabel;
            _creditsLabel = creditsLabel;
            _warehouseInfoLabel = warehouseInfoLabel;
            _timeInfoLabel = timeInfoLabel;
            _messageLabel = messageLabel;

            _itemSection = root.Q<VisualElement>("item-section");
            _warehouseSection = root.Q<VisualElement>("warehouse-section");
            _cargoSection = root.Q<VisualElement>("cargo-section");
            _shipSelectorContainer = root.Q<VisualElement>("ship-selector-container");
            _itemList = root.Q<ListView>("item-list");
            _warehouseList = root.Q<ListView>("warehouse-list");
            _cargoList = root.Q<ListView>("cargo-list");
            _buyBtn = root.Q<Button>("buy-btn");
            _sellBtn = root.Q<Button>("sell-btn");
            _loadBtn = root.Q<Button>("load-btn");
            _unloadBtn = root.Q<Button>("unload-btn");
            _myItemsToggle = root.Q<Button>("my-items-toggle");
            _qtyLabel = root.Q<Label>("qty-label-value");
            _warehouseQtyLabel = root.Q<Label>("warehouse-qty-label-value");
            _cargoWeightLabel = root.Q<Label>("cargo-weight-label");
            _cargoSlotsLabel = root.Q<Label>("cargo-slots-label");
            _cargoBarFill = root.Q<VisualElement>("cargo-weight-bar-fill");

            _marketQtyMinus10 = root.Q<Button>("market-qty-minus10");
            _marketQtyMinus1 = root.Q<Button>("market-qty-minus1");
            _marketQtyPlus1 = root.Q<Button>("market-qty-plus1");
            _marketQtyPlus10 = root.Q<Button>("market-qty-plus10");
            _warehouseQtyMinus10 = root.Q<Button>("warehouse-qty-minus10");
            _warehouseQtyMinus1 = root.Q<Button>("warehouse-qty-minus1");
            _warehouseQtyPlus1 = root.Q<Button>("warehouse-qty-plus1");
            _warehouseQtyPlus10 = root.Q<Button>("warehouse-qty-plus10");
            _marketQtyMin = root.Q<Button>("market-qty-min");
            _marketQtyMax = root.Q<Button>("market-qty-max");
            _warehouseQtyMin = root.Q<Button>("warehouse-qty-min");
            _warehouseQtyMax = root.Q<Button>("warehouse-qty-max");

            var selectorElement = root.Q<VisualElement>("ship-selector");
            if (selectorElement != null)
            {
                _shipSelector = new ProjectC.UI.Client.CustomDropdown();
                selectorElement.Add(_shipSelector);
                _shipSelector.OnSelectionChanged += OnShipSelectorChanged;
            }

            ConfigureLists();
            ConfigureButtons();
            SetupQtyRow();
        }

        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            _state = MarketClientState.Instance;
            if (_state == null)
            {
                Debug.LogWarning("[MarketTabController] MarketClientState.Instance == null, вкладки рынка ждут state");
                return;
            }

            _state.OnSnapshotUpdated += HandleSnapshot;
            _state.OnTradeResult += HandleTradeResult;
            if (_state.CurrentSnapshot.HasValue)
                HandleSnapshot(_state.CurrentSnapshot.Value);
        }

        public void Unsubscribe()
        {
            if (_state != null)
            {
                _state.OnSnapshotUpdated -= HandleSnapshot;
                _state.OnTradeResult -= HandleTradeResult;
            }
            _subscribed = false;
            _state = null;
        }

        public void SetTabVisible(string tab)
        {
            bool isMarket = tab == "market";
            bool isWarehouse = tab == "warehouse";

            if (_itemSection != null) _itemSection.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_warehouseSection != null) _warehouseSection.style.display = isWarehouse ? DisplayStyle.Flex : DisplayStyle.None;
            if (_cargoSection != null) _cargoSection.style.display = isWarehouse ? DisplayStyle.Flex : DisplayStyle.None;

            if (_buyBtn != null) _buyBtn.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_sellBtn != null) _sellBtn.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_loadBtn != null) _loadBtn.style.display = isWarehouse ? DisplayStyle.Flex : DisplayStyle.None;
            if (_unloadBtn != null) _unloadBtn.style.display = isWarehouse ? DisplayStyle.Flex : DisplayStyle.None;

            if (_shipSelectorContainer != null)
            {
                bool showShip = isWarehouse && IsShipSelectorVisible();
                _shipSelectorContainer.style.display = showShip ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_qtyLabel != null && _qtyLabel.parent != null)
                _qtyLabel.parent.style.display = isMarket ? DisplayStyle.Flex : DisplayStyle.None;
            if (_warehouseQtyLabel != null && _warehouseQtyLabel.parent != null)
                _warehouseQtyLabel.parent.style.display = isWarehouse ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ConfigureLists()
        {
            if (_itemList != null)
            {
                _itemList.makeItem = MakeMarketRow;
                _itemList.bindItem = BindMarketRow;
                _itemList.fixedItemHeight = 32;
                _itemList.selectionType = SelectionType.Single;
                _itemList.selectedIndex = -1;
                _itemList.selectionChanged += selectedItems =>
                {
                    _selectedMarketItem = FindSelectedItemIndex<ItemPriceDto>(_itemList, selectedItems);
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
                };
            }
        }

        private void ConfigureButtons()
        {
            if (_buyBtn != null)
            {
                _buyBtn.text = Loc.Get("ui.market.btn.buy");
                _buyBtn.clicked += OnBuyClicked;
            }
            if (_sellBtn != null)
            {
                _sellBtn.text = Loc.Get("ui.market.btn.sell");
                _sellBtn.clicked += OnSellClicked;
            }
            if (_loadBtn != null)
            {
                _loadBtn.text = Loc.Get("ui.market.btn.load");
                _loadBtn.clicked += OnLoadClicked;
            }
            if (_unloadBtn != null)
            {
                _unloadBtn.text = Loc.Get("ui.market.btn.unload");
                _unloadBtn.clicked += OnUnloadClicked;
            }
            if (_myItemsToggle != null)
            {
                _myItemsToggle.text = Loc.Get("ui.market.btn.show_mine");
                _myItemsToggle.clicked += OnMyItemsToggleClicked;
            }

            if (_marketQtyMinus10 != null) _marketQtyMinus10.clicked += () => AdjustMarketQty(-10);
            if (_marketQtyMinus1 != null) _marketQtyMinus1.clicked += () => AdjustMarketQty(-1);
            if (_marketQtyPlus1 != null) _marketQtyPlus1.clicked += () => AdjustMarketQty(1);
            if (_marketQtyPlus10 != null) _marketQtyPlus10.clicked += () => AdjustMarketQty(10);
            if (_warehouseQtyMinus10 != null) _warehouseQtyMinus10.clicked += () => AdjustWarehouseQty(-10);
            if (_warehouseQtyMinus1 != null) _warehouseQtyMinus1.clicked += () => AdjustWarehouseQty(-1);
            if (_warehouseQtyPlus1 != null) _warehouseQtyPlus1.clicked += () => AdjustWarehouseQty(1);
            if (_warehouseQtyPlus10 != null) _warehouseQtyPlus10.clicked += () => AdjustWarehouseQty(10);
            if (_marketQtyMin != null) _marketQtyMin.clicked += () => SetMarketQty(1);
            if (_marketQtyMax != null) _marketQtyMax.clicked += () => SetMarketQty(999);
            if (_warehouseQtyMin != null) _warehouseQtyMin.clicked += () => SetWarehouseQty(1);
            if (_warehouseQtyMax != null) _warehouseQtyMax.clicked += () => SetWarehouseQty(999);
        }

        private VisualElement MakeMarketRow()
        {
            var row = new VisualElement();
            row.AddToClassList("market-row");
            row.Add(new Label { name = "row-label" });
            return row;
        }

        private void BindMarketRow(VisualElement row, int index)
        {
            var source = _itemList != null ? _itemList.itemsSource : null;
            if (source == null || index < 0 || index >= source.Count) return;
            var item = (ItemPriceDto)source[index];
            var snapshot = State != null ? State.CurrentSnapshot : null;
            int warehouseQuantity = snapshot.HasValue ? FindWarehouseQty(snapshot.Value.warehouse, item.itemId) : 0;
            row.Q<Label>("row-label").text =
                $"{item.displayName}  —  {item.currentPrice:F0} CR  (сток: {item.availableStock})  (у вас: {warehouseQuantity})";
            row.style.backgroundColor = index == _selectedMarketItem
                ? new StyleColor(new Color(0.4f, 0.6f, 0.9f, 0.4f))
                : StyleKeyword.Null;
        }

        private static VisualElement MakeWarehouseRow()
        {
            var row = new VisualElement();
            row.AddToClassList("warehouse-row");
            row.Add(new Label { name = "row-label" });
            return row;
        }

        private void BindWarehouseRow(VisualElement row, int index)
        {
            var source = _warehouseList != null ? _warehouseList.itemsSource : null;
            if (source == null || index < 0 || index >= source.Count) return;
            var entry = (WarehouseEntryDto)source[index];
            row.Q<Label>("row-label").text = $"{entry.displayName}  —  {entry.quantity} ед.";
            row.style.backgroundColor = index == _selectedWarehouseItem
                ? new StyleColor(new Color(0.4f, 0.6f, 0.9f, 0.4f))
                : StyleKeyword.Null;
        }

        private static VisualElement MakeCargoRow()
        {
            var row = new VisualElement();
            row.AddToClassList("cargo-row");
            row.Add(new Label { name = "row-label" });
            return row;
        }

        private void BindCargoRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _cargoCache.Length) return;
            var entry = _cargoCache[index];
            row.Q<Label>("row-label").text = $"{entry.displayName}  —  {entry.quantity} ед.  ({GetSelectedShipName()})";
            row.style.backgroundColor = index == _selectedCargoItem
                ? new StyleColor(new Color(0.4f, 0.9f, 0.6f, 0.4f))
                : StyleKeyword.Null;
        }

        private void HandleSnapshot(MarketSnapshotDto snapshot)
        {
            if (_locationLabel != null) _locationLabel.text = $"Рынок: {snapshot.displayName}";
            if (_creditsLabel != null) _creditsLabel.text = $"Кредиты: {snapshot.credits:F0} CR";
            if (_warehouseInfoLabel != null)
                _warehouseInfoLabel.text = $"Склад: {snapshot.warehouse?.Length ?? 0} типов / {snapshot.warehouseMaxTypes}";
            if (_timeInfoLabel != null)
            {
                int seconds = Mathf.CeilToInt(snapshot.secondsUntilNextTick);
                _timeInfoLabel.text = $"Скорость рынка: x{snapshot.marketTimeMultiplier:F1} | Тик через: {seconds}с";
            }

            ulong currentShipId = GetSelectedShipId();
            if (State != null && State.CurrentShipCargos != null && currentShipId != 0
                && State.CurrentShipCargos.TryGetValue(currentShipId, out var cachedCargo))
            {
                _cargoCache = cachedCargo ?? Array.Empty<WarehouseEntryDto>();
            }
            else
            {
                _cargoCache = snapshot.cargo ?? Array.Empty<WarehouseEntryDto>();
            }

            _marketItemsCache = snapshot.items ?? Array.Empty<ItemPriceDto>();
            _marketWhCache = snapshot.warehouse ?? Array.Empty<WarehouseEntryDto>();

            if (_myItemsOnly) ApplyMarketFilter();
            else SetListSource(_itemList, _marketItemsCache);
            SetListSource(_warehouseList, _marketWhCache);
            SetListSource(_cargoList, _cargoCache);
            RefreshCargoInfo();

            if (_shipSelector != null && snapshot.nearbyShips != null)
            {
                var choices = new List<string>();
                foreach (var ship in snapshot.nearbyShips) choices.Add(ship.displayName);
                _shipSelector.SetChoices(choices,
                    _selectedShipIndex >= 0 && _selectedShipIndex < choices.Count ? _selectedShipIndex : 0);
                if (choices.Count > 0 && _shipSelector.SelectedIndex < 0)
                    _shipSelector.SetSelectedIndex(0, fireEvent: true);
                if (_shipSelectorContainer != null)
                    _shipSelectorContainer.style.display = snapshot.nearbyShips.Length > 1 && _host.ActiveTab == "warehouse"
                        ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void HandleTradeResult(TradeResultDto result)
        {
            if (result.IsSuccess)
            {
                _host.SetMessage($"{LocalizeOp(result.op)}: OK ({result.itemId} x{result.quantity})");
            }
            else
            {
                _host.SetMessage($"Ошибка: {MarketClientState.LocalizeResultCode(result.code)}", true);
            }

            if (result.IsSuccess && (result.op == TradeOp.LoadToShip || result.op == TradeOp.UnloadFromShip))
            {
                _cargoCache = result.updatedCargoSnapshot ?? Array.Empty<WarehouseEntryDto>();
                SetListSource(_cargoList, _cargoCache);
                _selectedCargoItem = -1;
                if (result.shipNetworkObjectId != 0 && State != null)
                    State.UpdateShipCargo(result.shipNetworkObjectId, _cargoCache);
            }
        }

        private void OnShipSelectorChanged(int index)
        {
            var snapshot = State != null ? State.CurrentSnapshot : null;
            if (!snapshot.HasValue || snapshot.Value.nearbyShips == null
                || index < 0 || index >= snapshot.Value.nearbyShips.Length) return;

            _selectedShipIndex = index;
            ulong shipId = snapshot.Value.nearbyShips[index].shipNetworkObjectId;
            ApplySelectedShipCargoFromCache(shipId);
            RefreshCargoInfo();
            State?.RequestSetSelectedShip(snapshot.Value.locationId, shipId);
        }

        private void OnBuyClicked()
        {
            if (!TryGetSelectedItem(_itemList, _selectedMarketItem, out ItemPriceDto item)) return;
            var snapshot = State != null ? State.CurrentSnapshot : null;
            if (!snapshot.HasValue) return;
            int quantity = Mathf.Min(GetMarketQty(), item.availableStock);
            State.RequestBuy(snapshot.Value.locationId, item.itemId, quantity);
        }

        private void OnSellClicked()
        {
            if (!TryGetSelectedItem(_itemList, _selectedMarketItem, out ItemPriceDto item)) return;
            var snapshot = State != null ? State.CurrentSnapshot : null;
            if (!snapshot.HasValue) return;
            int warehouseQuantity = FindWarehouseQty(snapshot.Value.warehouse, item.itemId);
            int quantity = Mathf.Min(GetMarketQty(), warehouseQuantity);
            State.RequestSell(snapshot.Value.locationId, item.itemId, quantity);
        }

        private void OnLoadClicked()
        {
            if (!TryGetSelectedItem(_warehouseList, _selectedWarehouseItem, out WarehouseEntryDto entry)) return;
            var snapshot = State != null ? State.CurrentSnapshot : null;
            if (!snapshot.HasValue) return;
            ulong shipId = GetSelectedShipId();
            if (shipId == 0)
            {
                _host.SetMessage(Loc.Get("ui.market.select_ship_first"), true);
                return;
            }
            State.RequestLoadToShip(snapshot.Value.locationId, entry.itemId,
                Mathf.Min(GetWarehouseQty(), entry.quantity), shipId);
        }

        private void OnUnloadClicked()
        {
            if (!TryGetSelectedItem(_cargoList, _selectedCargoItem, out WarehouseEntryDto entry)) return;
            var snapshot = State != null ? State.CurrentSnapshot : null;
            if (!snapshot.HasValue) return;
            ulong shipId = GetSelectedShipId();
            if (shipId == 0)
            {
                _host.SetMessage(Loc.Get("ui.market.select_ship_first"), true);
                return;
            }
            State.RequestUnloadFromShip(snapshot.Value.locationId, entry.itemId,
                Mathf.Min(GetWarehouseQty(), entry.quantity), shipId);
        }

        private void OnMyItemsToggleClicked()
        {
            _myItemsOnly = !_myItemsOnly;
            if (_myItemsToggle != null)
                _myItemsToggle.text = _myItemsOnly
                    ? Loc.Get("ui.market.show_all")
                    : Loc.Get("ui.market.show_mine");
            ApplyMarketFilter();
        }

        private void ApplyMarketFilter()
        {
            if (_itemList == null) return;
            if (!_myItemsOnly)
            {
                SetListSource(_itemList, _marketItemsCache);
                return;
            }

            var filtered = new List<ItemPriceDto>();
            for (int i = 0; i < _marketItemsCache.Length; i++)
            {
                if (FindWarehouseQty(_marketWhCache, _marketItemsCache[i].itemId) > 0)
                    filtered.Add(_marketItemsCache[i]);
            }
            SetListSource(_itemList, filtered);
        }

        private void SetListSource(ListView list, IList source)
        {
            if (list == null) return;
            list.itemsSource = null;
            list.Rebuild();
            list.itemsSource = source;
            list.selectedIndex = -1;
            list.Rebuild();
        }

        private static bool TryGetSelectedItem<T>(ListView list, int index, out T value)
        {
            value = default;
            if (list == null || list.itemsSource == null || index < 0 || index >= list.itemsSource.Count)
                return false;
            if (!(list.itemsSource[index] is T selected)) return false;
            value = selected;
            return true;
        }

        private static int FindSelectedItemIndex<T>(ListView list, IEnumerable<object> selectedItems)
        {
            if (list == null || list.itemsSource == null || selectedItems == null) return -1;
            foreach (var selected in selectedItems)
            {
                for (int i = 0; i < list.itemsSource.Count; i++)
                {
                    if (selected is T && Equals(list.itemsSource[i], selected)) return i;
                }
                break;
            }
            return -1;
        }

        private static int FindWarehouseQty(WarehouseEntryDto[] warehouse, string itemId)
        {
            if (warehouse == null || string.IsNullOrEmpty(itemId)) return 0;
            for (int i = 0; i < warehouse.Length; i++)
                if (warehouse[i].itemId == itemId) return warehouse[i].quantity;
            return 0;
        }

        private void RefreshCargoInfo()
        {
            if (_cargoWeightLabel == null || _cargoSlotsLabel == null || _cargoBarFill == null) return;
            var snapshot = State != null ? State.CurrentSnapshot : null;
            if (!snapshot.HasValue || snapshot.Value.nearbyShips == null)
            {
                ResetCargoInfo();
                return;
            }

            ulong shipId = GetSelectedShipId();
            for (int i = 0; i < snapshot.Value.nearbyShips.Length; i++)
            {
                var ship = snapshot.Value.nearbyShips[i];
                if (ship.shipNetworkObjectId != shipId) continue;
                float pct = ship.maxWeight > 0f ? Mathf.Clamp01(ship.currentWeight / ship.maxWeight) * 100f : 0f;
                _cargoWeightLabel.text = $"{ship.currentWeight:F1} / {ship.maxWeight:F0} кг";
                _cargoSlotsLabel.text = $"{ship.currentSlots} / {ship.maxSlots} слотов";
                _cargoBarFill.style.width = new StyleLength(Length.Percent(pct));
                return;
            }
            ResetCargoInfo();
        }

        private void ResetCargoInfo()
        {
            if (_cargoWeightLabel != null) _cargoWeightLabel.text = "— / — кг";
            if (_cargoSlotsLabel != null) _cargoSlotsLabel.text = "— / — слотов";
            if (_cargoBarFill != null) _cargoBarFill.style.width = new StyleLength(Length.Percent(0f));
        }

        private ulong GetSelectedShipId()
        {
            var snapshot = State != null ? State.CurrentSnapshot : null;
            if (!snapshot.HasValue || snapshot.Value.nearbyShips == null
                || _selectedShipIndex < 0 || _selectedShipIndex >= snapshot.Value.nearbyShips.Length) return 0;
            return snapshot.Value.nearbyShips[_selectedShipIndex].shipNetworkObjectId;
        }

        private string GetSelectedShipName()
        {
            var snapshot = State != null ? State.CurrentSnapshot : null;
            if (!snapshot.HasValue || snapshot.Value.nearbyShips == null
                || _selectedShipIndex < 0 || _selectedShipIndex >= snapshot.Value.nearbyShips.Length) return "—";
            return snapshot.Value.nearbyShips[_selectedShipIndex].displayName;
        }

        private bool IsShipSelectorVisible()
        {
            var snapshot = State != null ? State.CurrentSnapshot : null;
            return snapshot.HasValue && snapshot.Value.nearbyShips != null && snapshot.Value.nearbyShips.Length > 1;
        }

        private void ApplySelectedShipCargoFromCache(ulong shipId)
        {
            _cargoCache = Array.Empty<WarehouseEntryDto>();
            if (State != null && State.CurrentShipCargos != null
                && State.CurrentShipCargos.TryGetValue(shipId, out var cargo))
                _cargoCache = cargo ?? Array.Empty<WarehouseEntryDto>();
            SetListSource(_cargoList, _cargoCache);
            _selectedCargoItem = -1;
        }

        private void AdjustMarketQty(int delta) => SetMarketQty(_marketQty + delta);
        private void AdjustWarehouseQty(int delta) => SetWarehouseQty(_warehouseQty + delta);

        private void SetMarketQty(int value)
        {
            _marketQty = Mathf.Clamp(value, 1, 9999);
            if (_qtyLabel != null) _qtyLabel.text = _marketQty.ToString();
        }

        private void SetWarehouseQty(int value)
        {
            _warehouseQty = Mathf.Clamp(value, 1, 9999);
            if (_warehouseQtyLabel != null) _warehouseQtyLabel.text = _warehouseQty.ToString();
        }

        private int GetMarketQty() => Mathf.Clamp(_marketQty, 1, 9999);
        private int GetWarehouseQty() => Mathf.Clamp(_warehouseQty, 1, 9999);

        private void SetupQtyRow()
        {
            StyleQtyLabel(_qtyLabel);
            StyleQtyLabel(_warehouseQtyLabel);
            var qtyLabel = _root?.Q<Label>(className: "qty-label");
            if (qtyLabel != null)
            {
                qtyLabel.style.color = new StyleColor(new Color(0.78f, 0.78f, 0.86f));
                qtyLabel.style.fontSize = 11;
                qtyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                qtyLabel.style.marginLeft = 2;
                qtyLabel.style.marginRight = 2;
            }

            StyleQtyBtn(_marketQtyMinus10, true);
            StyleQtyBtn(_marketQtyMinus1, true);
            StyleQtyBtn(_marketQtyPlus1, false);
            StyleQtyBtn(_marketQtyPlus10, false);
            StyleQtyBtn(_warehouseQtyMinus10, true);
            StyleQtyBtn(_warehouseQtyMinus1, true);
            StyleQtyBtn(_warehouseQtyPlus1, false);
            StyleQtyBtn(_warehouseQtyPlus10, false);
            StyleQtyExtremeBtn(_marketQtyMin, "MIN");
            StyleQtyExtremeBtn(_marketQtyMax, "MAX");
            StyleQtyExtremeBtn(_warehouseQtyMin, "MIN");
            StyleQtyExtremeBtn(_warehouseQtyMax, "MAX");
        }

        private static void StyleQtyLabel(Label label)
        {
            if (label == null) return;
            label.style.width = 50;
            label.style.minWidth = 50;
            label.style.height = 24;
            label.style.backgroundColor = new StyleColor(new Color(0.92f, 0.94f, 0.97f, 0.95f));
            label.style.color = new StyleColor(new Color(0.05f, 0.05f, 0.08f));
            label.style.fontSize = 13;
            label.style.borderTopWidth = 1;
            label.style.borderBottomWidth = 1;
            label.style.borderLeftWidth = 1;
            label.style.borderRightWidth = 1;
            label.style.borderTopColor = new StyleColor(new Color(0.7f, 0.75f, 0.85f, 0.5f));
            label.style.borderBottomColor = new StyleColor(new Color(0.7f, 0.75f, 0.85f, 0.5f));
            label.style.borderLeftColor = new StyleColor(new Color(0.7f, 0.75f, 0.85f, 0.5f));
            label.style.borderRightColor = new StyleColor(new Color(0.7f, 0.75f, 0.85f, 0.5f));
            label.style.borderTopLeftRadius = 3;
            label.style.borderTopRightRadius = 3;
            label.style.borderBottomLeftRadius = 3;
            label.style.borderBottomRightRadius = 3;
            label.style.paddingLeft = 4;
            label.style.paddingRight = 4;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginLeft = 2;
            label.style.marginRight = 2;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private static void StyleQtyBtn(Button button, bool isMinus)
        {
            if (button == null) return;
            button.style.width = 22;
            button.style.height = 22;
            button.style.minWidth = 22;
            button.style.minHeight = 22;
            button.style.borderTopLeftRadius = 11;
            button.style.borderTopRightRadius = 11;
            button.style.borderBottomLeftRadius = 11;
            button.style.borderBottomRightRadius = 11;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.fontSize = 10;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.alignSelf = Align.Center;
            var color = new Color(0.16f, 0.16f, 0.2f);
            button.style.color = new StyleColor(color);
            button.style.backgroundColor = isMinus
                ? new StyleColor(new Color(0.86f, 0.63f, 0.63f, 0.85f))
                : new StyleColor(new Color(0.63f, 0.86f, 0.63f, 0.85f));
        }

        private static void StyleQtyExtremeBtn(Button button, string text)
        {
            if (button == null) return;
            button.text = text;
            button.style.width = 32;
            button.style.height = 22;
            button.style.minWidth = 32;
            button.style.minHeight = 22;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.fontSize = 9;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.paddingLeft = 2;
            button.style.paddingRight = 2;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.alignSelf = Align.Center;
            button.style.flexShrink = 0;
            button.style.color = new StyleColor(new Color(0.78f, 0.82f, 0.9f));
            button.style.backgroundColor = new StyleColor(new Color(0.24f, 0.31f, 0.51f, 0.6f));
        }

        private static string LocalizeOp(TradeOp op)
        {
            switch (op)
            {
                case TradeOp.Buy: return Loc.Get("ui.market.op.buy");
                case TradeOp.Sell: return Loc.Get("ui.market.op.sell");
                case TradeOp.LoadToShip: return Loc.Get("ui.market.op.load");
                case TradeOp.UnloadFromShip: return Loc.Get("ui.market.op.unload");
                default: return op.ToString();
            }
        }
    }
}
