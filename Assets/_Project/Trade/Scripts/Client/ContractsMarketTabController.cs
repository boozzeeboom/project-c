using System;
using System.Collections;
using System.Collections.Generic;
using ProjectC.Localization;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Контроллер вкладки КОНТРАКТЫ внутри MarketWindow.
    /// Владеет offer/active projection, selection и accept/complete/fail actions.
    /// После каждого результата явно запрашивает свежий список, поэтому UI не
    /// зависит от переключения вкладок или от порядка доставки snapshot/result.
    /// </summary>
    public sealed class ContractsMarketTabController
    {
        private readonly MarketWindowHost _host;
        private VisualElement _contractsSection;
        private ListView _contractsList;
        private Button _acceptBtn;
        private Button _receiveBtn;
        private Button _completeBtn;
        private Button _failBtn;
        private bool _contractsTabVisible;
        private Label _creditsLabel;
        private Label _messageLabel;
        private ContractClientState _state;
        private ContractDto[] _contractsCache = Array.Empty<ContractDto>();
        private int _selectedContractItem = -1;
        private bool _subscribed;

        public ContractsMarketTabController(MarketWindowHost host)
        {
            _host = host;
        }

        public void BuildUI(VisualElement root, Label creditsLabel, Label messageLabel)
        {
            _creditsLabel = creditsLabel;
            _messageLabel = messageLabel;
            _contractsSection = root.Q<VisualElement>("contracts-section");
            _contractsList = root.Q<ListView>("contracts-list");
            _acceptBtn = root.Q<Button>("accept-btn");
            _receiveBtn = root.Q<Button>("receive-btn");
            _completeBtn = root.Q<Button>("complete-btn");
            _failBtn = root.Q<Button>("fail-btn");

            if (_contractsList != null)
            {
                _contractsList.makeItem = MakeContractRow;
                _contractsList.bindItem = BindContractRow;
                _contractsList.fixedItemHeight = 32;
                _contractsList.selectionType = SelectionType.Single;
                _contractsList.selectedIndex = -1;
                _contractsList.selectionChanged += selectedItems =>
                {
                    _selectedContractItem = FindSelectedItemIndex(_contractsList, selectedItems);
                    UpdateActionButtons();
                };
            }

            if (_acceptBtn != null)
            {
                _acceptBtn.text = Loc.Get("ui.market.btn.accept");
                _acceptBtn.clicked += OnAcceptClicked;
            }
            if (_receiveBtn != null)
            {
                _receiveBtn.text = "ПОЛУЧИТЬ ГРУЗ";
                _receiveBtn.clicked += OnReceiveClicked;
            }
            if (_completeBtn != null)
            {
                _completeBtn.text = Loc.Get("ui.market.btn.complete");
                _completeBtn.clicked += OnCompleteClicked;
            }
            if (_failBtn != null)
            {
                _failBtn.text = Loc.Get("ui.market.btn.fail");
                _failBtn.clicked += OnFailClicked;
            }
        }

        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            _state = ContractClientState.Instance;
            if (_state == null)
            {
                Debug.LogWarning("[ContractsMarketTabController] ContractClientState.Instance == null, вкладка ждёт state");
                return;
            }

            _state.OnSnapshotUpdated += HandleSnapshot;
            _state.OnContractResult += HandleResult;
            if (_state.CurrentSnapshot.HasValue)
                HandleSnapshot(_state.CurrentSnapshot.Value);
        }

        public void Unsubscribe()
        {
            if (_state != null)
            {
                _state.OnSnapshotUpdated -= HandleSnapshot;
                _state.OnContractResult -= HandleResult;
            }
            _state = null;
            _subscribed = false;
        }

        public void SetTabVisible(bool visible)
        {
            if (_contractsSection != null)
                _contractsSection.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _contractsTabVisible = visible;
            if (_acceptBtn != null) _acceptBtn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_receiveBtn != null) _receiveBtn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_completeBtn != null) _completeBtn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_failBtn != null) _failBtn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (!visible) return;
            if (_contractsList != null) _contractsList.MarkDirtyRepaint();
            RefreshFromCurrentSnapshot();
            UpdateActionButtons();
            RequestList();
        }

        private void RefreshFromCurrentSnapshot()
        {
            var state = State;
            if (state != null && state.CurrentSnapshot.HasValue)
                HandleSnapshot(state.CurrentSnapshot.Value);
        }

        private ContractClientState State => _state != null ? _state : ContractClientState.Instance;

        private void HandleSnapshot(ContractSnapshotDto snapshot)
        {
            string locationId = _host.CurrentLocationId;
            var available = new List<ContractDto>();
            if (!string.IsNullOrEmpty(locationId) && snapshot.available != null)
            {
                for (int i = 0; i < snapshot.available.Length; i++)
                {
                    var contract = snapshot.available[i];
                    if (contract.state != (byte)ContractState.Pending) continue;
                    if (!string.Equals(contract.fromLocationId, locationId, StringComparison.OrdinalIgnoreCase)) continue;
                    available.Add(contract);
                }
            }

            var active = new List<ContractDto>();
            var activeSource = snapshot.active ?? Array.Empty<ContractDto>();
            for (int i = 0; i < activeSource.Length; i++)
            {
                if (activeSource[i].state == (byte)ContractState.Active)
                    active.Add(activeSource[i]);
            }

            var combined = new List<ContractDto>(active.Count + available.Count);
            combined.AddRange(active);
            combined.AddRange(available);
            _contractsCache = combined.ToArray();
            SetListSource(_contractsList, _contractsCache);
            _selectedContractItem = -1;
            UpdateActionButtons();

            if (_messageLabel != null && _host.IsVisible() && _host.ActiveTab == "contracts")
            {
                _messageLabel.text = active.Count == 0 && available.Count == 0
                    ? Loc.Get("ui.market.no_contracts_here")
                    : Loc.Format("ui.character.active_available", active.Count, available.Count);
                _messageLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            }
        }

        private void HandleResult(ContractResultDto result)
        {
            if (result.IsSuccess)
            {
                _host.SetMessage(result.message ?? "OK");
                if (_host.MarketState != null && !string.IsNullOrEmpty(_host.MarketState.CurrentLocationId))
                    _host.MarketState.RequestSubscribeMarket(_host.MarketState.CurrentLocationId);
            }
            else
            {
                _host.SetMessage(result.message ?? ContractClientState.LocalizeResultCode((ContractResultCode)result.code), true);
            }

            if (_creditsLabel != null && result.newCredits > 0f)
                _creditsLabel.text = $"Кредиты: {result.newCredits:F0} CR";

            // Ключевой UI-фикс MKT-UI-003: result не считается достаточным для
            // проекции. Всегда запрашиваем новый contract snapshot после accept,
            // complete и fail, включая failure result.
            RequestList();
        }

        private void RequestList()
        {
            var state = State;
            if (state == null) return;
            string locationId = _host.CurrentLocationId;
            if (string.IsNullOrEmpty(locationId)) locationId = state.CurrentLocationId;
            if (string.IsNullOrEmpty(locationId)) return;
            state.RequestList(locationId);
        }

        private void OnAcceptClicked()
        {
            if (!TryGetSelected(out var contract))
            {
                _host.SetMessage(Loc.Get("ui.character.select_contract"));
                return;
            }
            if (contract.state != (byte)ContractState.Pending)
            {
                _host.SetMessage(Loc.Get("ui.character.contract_unavailable"));
                return;
            }
            State?.RequestAccept(contract.contractId);
            _host.SetMessage(Loc.Get("ui.character.request_sent"));
        }

        private void OnReceiveClicked()
        {
            if (!TryGetSelected(out var contract))
            {
                _host.SetMessage(Loc.Get("ui.character.select_contract"));
                return;
            }
            if (contract.state != (byte)ContractState.Active || !contract.isReceiptContract)
            {
                _host.SetMessage(Loc.Get("ui.character.contract_not_active"));
                return;
            }
            if (contract.receiptCargoIssued)
            {
                _host.SetMessage("Груз по расписке уже выдан.");
                return;
            }
            State?.RequestReceiveCargo(contract.contractId);
            _host.SetMessage(Loc.Get("ui.character.request_sent"));
        }

        private void OnCompleteClicked()
        {
            if (!TryGetSelected(out var contract))
            {
                _host.SetMessage(Loc.Get("ui.character.select_contract"));
                return;
            }
            if (contract.state != (byte)ContractState.Active)
            {
                _host.SetMessage(Loc.Get("ui.character.contract_not_active"));
                return;
            }
            State?.RequestComplete(contract.contractId);
            _host.SetMessage(Loc.Get("ui.character.request_sent"));
        }

        private void OnFailClicked()
        {
            if (!TryGetSelected(out var contract))
            {
                _host.SetMessage(Loc.Get("ui.character.select_contract"));
                return;
            }
            if (contract.state != (byte)ContractState.Active)
            {
                _host.SetMessage(Loc.Get("ui.character.contract_not_active"));
                return;
            }
            State?.RequestFail(contract.contractId);
            _host.SetMessage(Loc.Get("ui.character.request_sent"));
        }

        private void UpdateActionButtons()
        {
            if (!_contractsTabVisible) return;
            bool hasSelection = TryGetSelected(out var contract);
            bool isPending = hasSelection && contract.state == (byte)ContractState.Pending;
            bool isActive = hasSelection && contract.state == (byte)ContractState.Active;
            bool canReceive = isActive && contract.isReceiptContract && !contract.receiptCargoIssued;

            if (_acceptBtn != null) _acceptBtn.SetEnabled(isPending);
            if (_receiveBtn != null) _receiveBtn.style.display = canReceive ? DisplayStyle.Flex : DisplayStyle.None;
            if (_completeBtn != null) _completeBtn.SetEnabled(isActive && (!contract.isReceiptContract || contract.receiptCargoIssued));
            if (_failBtn != null) _failBtn.SetEnabled(isActive);
        }

        private bool TryGetSelected(out ContractDto contract)
        {
            contract = default;
            if (_selectedContractItem < 0 || _selectedContractItem >= _contractsCache.Length) return false;
            contract = _contractsCache[_selectedContractItem];
            return true;
        }

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

        private void BindContractRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _contractsCache.Length) return;
            var contract = _contractsCache[index];
            bool active = contract.state == (byte)ContractState.Active;
            if (active) row.AddToClassList("contract-row-active");
            else row.RemoveFromClassList("contract-row-active");

            var typeLabel = row.Q<Label>("type");
            if (typeLabel != null)
            {
                var contractType = (ContractType)contract.type;
                typeLabel.text = active
                    ? $"{ContractTypePresentation.GetDisplayName(contractType, contract.typeLocalizationKey)} [ВЗЯТ]"
                    : ContractTypePresentation.GetDisplayName(contractType, contract.typeLocalizationKey);
                string previousTypeClass = typeLabel.userData as string;
                if (!string.IsNullOrWhiteSpace(previousTypeClass))
                    typeLabel.RemoveFromClassList(previousTypeClass);

                string typeClass = ContractTypePresentation.GetUiClass(contractType, contract.typeUiClass);
                typeLabel.AddToClassList(typeClass);
                typeLabel.userData = typeClass;
            }

            var itemLabel = row.Q<Label>("item");
            if (itemLabel != null)
                itemLabel.text = $"{contract.displayName} x{contract.quantity} ({contract.fromLocationId}→{contract.toLocationId})";

            var rewardLabel = row.Q<Label>("reward");
            if (rewardLabel != null) rewardLabel.text = active ? "" : $"{contract.reward:F0} CR";

            var timerLabel = row.Q<Label>("timer");
            if (timerLabel != null)
            {
                timerLabel.text = GetContractTimeRemainingString(contract);
                timerLabel.RemoveFromClassList("timer-ok");
                timerLabel.RemoveFromClassList("timer-warn");
                timerLabel.RemoveFromClassList("timer-danger");
                timerLabel.AddToClassList(GetContractTimerClass(contract));
            }

            row.style.backgroundColor = index == _selectedContractItem
                ? new StyleColor(new Color(0.4f, 0.6f, 0.9f, 0.4f))
                : StyleKeyword.Null;
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


        private static string GetContractTimeRemainingString(ContractDto contract)
        {
            if (contract.timeLimit <= 0f) return "∞";
            int minutes = Mathf.FloorToInt(contract.timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(contract.timeRemaining % 60f);
            return $"{minutes}:{seconds:D2}";
        }

        private static string GetContractTimerClass(ContractDto contract)
        {
            if (contract.timeLimit <= 0f) return "timer-ok";
            float pct = contract.timeRemaining / contract.timeLimit;
            if (pct < 0.1f) return "timer-danger";
            if (pct < 0.3f) return "timer-warn";
            return "timer-ok";
        }
    }
}
