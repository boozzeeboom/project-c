// =====================================================================================
// ContractsTab.cs — вкладка КОНТРАКТЫ для CharacterWindow (Project C: The Clouds)
// =====================================================================================
// T-P19 refactor: вынесено из CharacterWindow.cs.
// Отвечает за список контрактов, фильтры (source/state/search), кнопки ВЗЯТЬ/ЗАКРЫТЬ/ПРОВАЛ.
//
// Подписки:
//   • ContractClientState.OnSnapshotUpdated — server-authoritative snapshot
//   • ContractClientState.OnContractResult — результат операции (accept/complete/fail)
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using ProjectC.Trade;
using ProjectC.Trade.Client;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UI.Client
{
    /// <summary>
    /// Вкладка КОНТРАКТЫ для CharacterWindow.
    /// </summary>
    public class ContractsTab
    {
        // ============================================================
        // State
        // ============================================================
        private CharacterWindow _owner;
        private VisualElement _root;

        // Shared UI refs (владеет CharacterWindow, ContractsTab конфигурирует)
        private DropdownField _filterSource;
        private DropdownField _filterState;
        private TextField _filterSearch;
        private Label _creditsLabel;
        private Label _messageLabel;

        // Contracts-specific refs
        private VisualElement _contractsSection;
        private ListView _contractsList;
        private Button _acceptBtn;
        private Button _completeBtn;
        private Button _failBtn;

        // Caches
        private List<ContractDto> _contractsCache = new List<ContractDto>();  // ТОЛЬКО активные контракты
        private int _selectedContractItem = -1;

        // Filters — только "Все" | "Активные" | "Завершённые"
        private List<string> _contractFilterStateOptions  = new List<string> { "Все", "Активные", "Завершённые" };

        // Subscription
        private ContractClientState _contractState;

        // ============================================================
        // API
        // ============================================================

        public void BuildUI(CharacterWindow owner, VisualElement root,
            DropdownField filterSource, DropdownField filterState, TextField filterSearch,
            Label creditsLabel, Label messageLabel)
        {
            _owner = owner;
            _root = root;
            _filterSource = filterSource;
            _filterState = filterState;
            _filterSearch = filterSearch;
            _creditsLabel = creditsLabel;
            _messageLabel = messageLabel;

            _contractsSection = root.Q<VisualElement>("contracts-section");
            _contractsList = root.Q<ListView>("contracts-list");
            _acceptBtn = root.Q<Button>("accept-btn");
            _completeBtn = root.Q<Button>("complete-btn");
            _failBtn = root.Q<Button>("fail-btn");

            // ---- ListView setup ----
            if (_contractsList != null)
            {
                _contractsList.makeItem = MakeContractRow;
                _contractsList.bindItem = BindContractRow;
                _contractsList.fixedItemHeight = 48;
                _contractsList.selectionType = SelectionType.Single;
                _contractsList.selectedIndex = -1;
                _contractsList.selectionChanged += selectedItems =>
                {
                    _selectedContractItem = FindSelectedItemIndex<ContractDto>(_contractsList, selectedItems);
                    // BUGFIX T-P19: НЕ вызываем Rebuild() здесь — это вызывает рекурсивный фриз,
                    // потому что Rebuild → selectionChanged → Rebuild → ...
                };
            }

            // ---- Buttons ----
            if (_acceptBtn != null) _acceptBtn.clicked += OnAcceptContractClicked;
            if (_completeBtn != null) _completeBtn.clicked += OnCompleteContractClicked;
            if (_failBtn != null) _failBtn.clicked += OnFailContractClicked;

            // ---- Subscribe ----
            _contractState = ContractClientState.Instance;
            if (_contractState == null)
            {
                Debug.LogWarning("[ContractsTab] ContractClientState.Instance == null, таб 'Контракты' не будет обновляться (нормально до StartHost)");
            }
            else
            {
                _contractState.OnSnapshotUpdated += HandleContractSnapshot;
                _contractState.OnContractResult += HandleContractResult;
                var nearestZone = MarketZoneRegistry.LocalPlayerZone;
                if (nearestZone != null) _contractState.RequestList(nearestZone.LocationId);
            }
        }

        public void OnTabShown()
        {
            if (_contractsSection != null)
                _contractsSection.style.display = DisplayStyle.Flex;

            if (_contractsList != null)
                _contractsList.MarkDirtyRepaint();

            // BUGFIX T-P19: берём свежий Instance (синглтон мог пересоздаться после BuildUI)
            var contractState = ProjectC.Trade.Client.ContractClientState.Instance;

            ConfigureContractFilters();

            Debug.Log($"[ContractsTab] OnTabShown: contractState={(contractState != null ? "OK" : "NULL")}, " +
                $"hasSnapshot={(contractState != null && contractState.CurrentSnapshot.HasValue ? "YES" : "NO")}");

            if (contractState != null && contractState.CurrentSnapshot.HasValue)
            {
                var snap = contractState.CurrentSnapshot.Value;
                Debug.Log($"[ContractsTab] Processing CurrentSnapshot: active={snap.active?.Length ?? 0}, available={snap.available?.Length ?? 0}");
                HandleContractSnapshot(snap);
            }
            else
            {
                ApplyContractFilters();
            }

            if (contractState != null)
            {
                var nearestZone = MarketZoneRegistry.LocalPlayerZone;
                if (nearestZone != null) contractState.RequestList(nearestZone.LocationId);
            }
        }

        public void OnTabHidden()
        {
            if (_contractsSection != null)
                _contractsSection.style.display = DisplayStyle.None;
        }

        public void SetButtonsVisible(bool visible)
        {
            if (_acceptBtn != null) _acceptBtn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_completeBtn != null) _completeBtn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_failBtn != null) _failBtn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ApplyFilters()
        {
            ApplyContractFilters();
        }

        public void Unsubscribe()
        {
            if (_contractState != null)
            {
                _contractState.OnSnapshotUpdated -= HandleContractSnapshot;
                _contractState.OnContractResult -= HandleContractResult;
                _contractState = null;
            }
        }

        // ============================================================
        // Filters
        // ============================================================

        private void ConfigureContractFilters()
        {
            // T-P19: только контракты — source показывает "Контракты" (фикс), state = фильтр
            if (_filterSource != null)
            {
                _filterSource.choices = new List<string> { "Контракты" };
                _filterSource.value = "Контракты";
                _filterSource.SetEnabled(false);  // readonly
            }
            if (_filterState != null)
            {
                _filterState.choices = _contractFilterStateOptions;
                _filterState.style.display = DisplayStyle.Flex;
                if (!_contractFilterStateOptions.Contains(_filterState.value))
                    _filterState.value = "Все";
                _filterState.SetEnabled(true);
            }
        }

        private void ApplyContractFilters()
        {
            if (_contractsList == null) return;
            IEnumerable<ContractDto> src = _contractsCache;

            // T-P19: только контракты (квесты убраны)

            string state = _filterState != null ? _filterState.value : "Все";
            if (state == "Активные")
                src = src.Where(c => c.state == (byte)ContractState.Active);
            else if (state == "Завершённые")
                src = src.Where(c => c.state == (byte)ContractState.Completed);

            string search = _filterSearch != null ? (_filterSearch.value ?? "").ToLowerInvariant() : "";
            if (!string.IsNullOrEmpty(search))
            {
                src = src.Where(c =>
                    (c.displayName ?? "").ToLowerInvariant().Contains(search) ||
                    (c.contractId ?? "").ToLowerInvariant().Contains(search));
            }

            var result = src.ToList();
            _contractsList.itemsSource = result;
            _selectedContractItem = -1;
            _contractsList.selectedIndex = -1;
            _contractsList.Rebuild();
        }

        // ============================================================
        // Row factory
        // ============================================================

        private VisualElement MakeContractRow()
        {
            var row = new VisualElement();
            row.AddToClassList("contract-row");

            // Top line: type + displayName + quantity
            var topLine = new VisualElement();
            topLine.AddToClassList("contract-row-line");
            row.Add(topLine);

            var typeLbl = new Label { name = "row-type" };
            typeLbl.AddToClassList("contract-type");
            topLine.Add(typeLbl);

            var itemLbl = new Label { name = "row-item" };
            itemLbl.AddToClassList("contract-item");
            topLine.Add(itemLbl);

            var qtyLbl = new Label { name = "row-qty" };
            qtyLbl.AddToClassList("contract-qty");
            topLine.Add(qtyLbl);

            var rewardLbl = new Label { name = "row-reward" };
            rewardLbl.AddToClassList("contract-reward");
            topLine.Add(rewardLbl);

            var timerLbl = new Label { name = "row-timer" };
            timerLbl.AddToClassList("contract-timer");
            topLine.Add(timerLbl);

            // Bottom line: route (from → to)
            var routeLbl = new Label { name = "row-route" };
            routeLbl.AddToClassList("contract-route");
            row.Add(routeLbl);

            return row;
        }

        private void BindContractRow(VisualElement row, int index)
        {
            if (_contractsList == null) return;
            var src = _contractsList.itemsSource as List<ContractDto>;
            if (src == null || index < 0 || index >= src.Count) return;
            var c = src[index];

            // Type badge
            var typeLbl = row.Q<Label>("row-type");
            typeLbl.text = GetContractTypeDisplayName((ContractType)c.type);
            typeLbl.RemoveFromClassList("type-standard");
            typeLbl.RemoveFromClassList("type-urgent");
            typeLbl.RemoveFromClassList("type-receipt");
            typeLbl.AddToClassList(GetContractTypeClass((ContractType)c.type));

            // Item name + quantity
            row.Q<Label>("row-item").text = c.displayName ?? c.itemId ?? "?";
            row.Q<Label>("row-qty").text = $"×{c.quantity}";

            // Reward
            row.Q<Label>("row-reward").text = $"+{c.reward:F0} CR";

            // Timer: timeRemaining / timeLimit
            var timerLbl = row.Q<Label>("row-timer");
            if (c.timeLimit > 0f)
            {
                float remaining = Mathf.Max(0f, c.timeRemaining);
                int min = Mathf.FloorToInt(remaining / 60f);
                int sec = Mathf.FloorToInt(remaining % 60f);
                timerLbl.text = c.timeRemaining <= 0f ? "—" : $"{min}:{sec:D2}";
                timerLbl.RemoveFromClassList("timer-ok");
                timerLbl.RemoveFromClassList("timer-warn");
                timerLbl.RemoveFromClassList("timer-danger");
                timerLbl.AddToClassList(remaining < 60f ? "timer-danger" : remaining < 300f ? "timer-warn" : "timer-ok");
            }
            else
            {
                timerLbl.text = "∞";
                timerLbl.style.color = new StyleColor(new Color(0.6f, 0.8f, 0.6f));
            }

            // Route: fromLocationId → toLocationId
            var routeLbl = row.Q<Label>("row-route");
            string fromName = ResolveLocationDisplayName(c.fromLocationId);
            string toName = ResolveLocationDisplayName(c.toLocationId);
            routeLbl.text = $"{fromName} → {toName}";

            row.RemoveFromClassList("contract-row-active");
            if (c.state == (byte)ContractState.Active) row.AddToClassList("contract-row-active");
        }

        /// <summary>T-P19: резолвит LocationId в читаемое имя.</summary>
        private static string ResolveLocationDisplayName(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return "?";
            switch (locationId.ToLowerInvariant())
            {
                case "primium":  return "Примум";
                case "secundus": return "Секундус";
                case "tertius":  return "Терциус";
                case "quartus":  return "Квартус";
                default:         return locationId;
            }
        }

        // ============================================================
        // Snapshot/Result handlers
        // ============================================================

        private void HandleContractSnapshot(ContractSnapshotDto snapshot)
        {
            // T-P19: Показываем ТОЛЬКО активные контракты (которые взяли в Market).
            // Доступные (available) — не показываем, это таб "мои контракты".
            _contractsCache.Clear();
            var activeAll = snapshot.active ?? System.Array.Empty<ContractDto>();
            for (int i = 0; i < activeAll.Length; i++)
            {
                if (activeAll[i].state == (byte)ContractState.Active)
                    _contractsCache.Add(activeAll[i]);
            }

            ApplyContractFilters();

            if (_messageLabel != null && _owner != null && _owner.IsVisible() && _owner.GetActiveTab() == "contracts")
            {
                _messageLabel.text = _contractsCache.Count == 0
                    ? "Нет активных контрактов"
                    : $"Активных: {_contractsCache.Count}";
                _messageLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            }
        }

        private void HandleContractResult(ContractResultDto result)
        {
            if (_messageLabel == null) return;
            if (_owner != null && !_owner.IsVisible()) return;

            if (result.IsSuccess)
            {
                _messageLabel.text = result.message ?? "OK";
                _messageLabel.style.color = new StyleColor(new Color(0.4f, 0.95f, 0.4f));
            }
            else
            {
                _messageLabel.text = result.message
                    ?? ContractClientState.LocalizeResultCode((ContractResultCode)result.code);
                _messageLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
            }

            if (_creditsLabel != null && result.newCredits > 0f)
            {
                _creditsLabel.text = $"Кредиты: {result.newCredits:F0} CR";
            }
        }

        // ============================================================
        // Button handlers
        // ============================================================

        private void OnAcceptContractClicked()
        {
            if (_selectedContractItem < 0 || _selectedContractItem >= _contractsCache.Count)
            {
                if (_messageLabel != null)
                    _messageLabel.text = "Выберите контракт из списка";
                return;
            }
            var c = _contractsCache[_selectedContractItem];
            if (_contractState != null)
                _contractState.RequestAccept(c.contractId);
        }

        private void OnCompleteContractClicked()
        {
            if (_selectedContractItem < 0 || _selectedContractItem >= _contractsCache.Count)
            {
                if (_messageLabel != null)
                    _messageLabel.text = "Выберите контракт из списка";
                return;
            }
            var c = _contractsCache[_selectedContractItem];
            if (_contractState != null)
                _contractState.RequestComplete(c.contractId);
        }

        private void OnFailContractClicked()
        {
            if (_selectedContractItem < 0 || _selectedContractItem >= _contractsCache.Count)
            {
                if (_messageLabel != null)
                    _messageLabel.text = "Выберите контракт из списка";
                return;
            }
            var c = _contractsCache[_selectedContractItem];
            if (_contractState != null)
                _contractState.RequestFail(c.contractId);
        }

        private System.Collections.IEnumerator JustTakenPulse(int rowIndex)
        {
            if (_contractsList == null) yield break;
            var itemsSource = _contractsList.itemsSource as List<ContractDto>;
            if (itemsSource == null || rowIndex < 0 || rowIndex >= itemsSource.Count) yield break;

            var row = _contractsList.GetRootElementForIndex(rowIndex);
            if (row != null) row.AddToClassList("contract-row-just-taken");

            yield return new WaitForSecondsRealtime(1.5f);

            if (row != null) row.RemoveFromClassList("contract-row-just-taken");
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static int FindSelectedItemIndex<T>(ListView list, System.Collections.Generic.IEnumerable<object> selectedItems)
        {
            if (list == null || list.itemsSource == null) return -1;
            if (selectedItems == null) return -1;
            var arr = selectedItems.ToArray();
            if (arr.Length == 0) return -1;
            var src = list.itemsSource as System.Collections.IList;
            if (src == null) return -1;
            int idx = src.IndexOf(arr[0]);
            return idx;
        }

        private static string GetContractTypeDisplayName(ContractType type)
        {
            switch (type)
            {
                case ContractType.Standard: return "Обычный";
                case ContractType.Urgent: return "Срочный";
                case ContractType.Receipt: return "Квитанция";
                default: return type.ToString();
            }
        }

        private static string GetContractTypeClass(ContractType type)
        {
            switch (type)
            {
                case ContractType.Standard: return "type-standard";
                case ContractType.Urgent: return "type-urgent";
                case ContractType.Receipt: return "type-receipt";
                default: return "type-standard";
            }
        }

        private static float GetTimerSeconds(ContractDto c)
        {
            // Плейсхолдер — будет заменён на реальный таймер в будущем
            return 0f;
        }

        private static string GetContractTimeRemainingString(ContractDto c)
        {
            float sec = GetTimerSeconds(c);
            if (sec <= 0f) return "—";
            if (sec < 60f) return $"{(int)sec}с";
            if (sec < 3600f) return $"{(int)(sec / 60)}м";
            return $"{(int)(sec / 3600)}ч {(int)((sec % 3600) / 60)}м";
        }

        private static string GetContractTimerClass(ContractDto c)
        {
            float sec = GetTimerSeconds(c);
            if (sec <= 0f) return "timer-ok";
            if (sec < 60f) return "timer-danger";
            if (sec < 300f) return "timer-warn";
            return "timer-ok";
        }
    }
}
