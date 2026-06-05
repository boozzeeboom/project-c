using System.Collections.Generic;
using ProjectC.Trade;
using ProjectC.Trade.Dto;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// UI Toolkit контроллер окна контрактной доски NPC-агента НП.
    /// Держит VisualTreeAsset из UXML, подписывается на
    /// <see cref="ContractClientState"/>, проецирует snapshot в UI и шлёт
    /// команды через ContractClientState.RequestXxx().
    ///
    /// Требования к сцене:
    ///   • GameObject с этим компонентом
    ///   • Соседний UIDocument с PanelSettings (используем существующий
    ///     <c>Assets/_Project/Trade/Resources/UI/MarketPanelSettings.asset</c>)
    ///   • UXML файл в Resources/UI/ContractBoardWindow.uxml
    ///   • USS файл в Resources/UI/ContractBoardWindow.uss
    ///
    /// Создание: ставится в BootstrapScene рядом с [MarketWindow]. Если GO нет —
    /// ContractInteractor создаёт динамически (как legacy ContractBoardUI).
    ///
    /// C2-этап миграции контрактов на v2-архитектуру.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ContractBoardWindow : MonoBehaviour
    {
        public static ContractBoardWindow Instance { get; private set; }

        [Header("UI Assets (можно Resources fallback)")]
        [SerializeField] private VisualTreeAsset contractWindowUxml;
        [SerializeField] private StyleSheet contractWindowUss;

        [Header("Behavior")]
        [SerializeField] private bool visibleOnStart = false;

        // === Runtime refs ===
        private UIDocument _doc;
        private VisualElement _root;
        private ContractClientState _state;

        // UI element refs
        private VisualElement _mainContainer;
        private Label _titleLabel;
        private Label _locationLabel;
        private Label _debtLabel;
        private Label _activeCountLabel;
        private Label _timeInfoLabel;
        private VisualElement _availableSection;
        private VisualElement _activeSection;
        private ListView _availableList;
        private ListView _activeList;
        private Button _tabAvailableBtn;
        private Button _tabActiveBtn;
        private Button _acceptBtn;
        private Button _completeBtn;
        private Button _failBtn;
        private Button _closeBtn;
        private Label _messageLabel;
        private Label _hintLabel;

        // State
        private int _selectedAvailableIndex = -1;
        private int _selectedActiveIndex = -1;
        private string _activeTab = "available"; // "available" / "active"
        private bool _built = false;

        // Локальные кэши для ListView
        private ContractDto[] _availableCache = System.Array.Empty<ContractDto>();
        private ContractDto[] _activeCache = System.Array.Empty<ContractDto>();

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (contractWindowUxml == null) contractWindowUxml = Resources.Load<VisualTreeAsset>("UI/ContractBoardWindow");
            if (contractWindowUss == null) contractWindowUss = Resources.Load<StyleSheet>("UI/ContractBoardWindow");
            if (Instance == null) Instance = this;
        }

        private void OnEnable()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                Debug.LogError("[ContractBoardWindow] нет UIDocument на GameObject");
                return;
            }
            EnsureBuilt();

            // Подписываемся на события ContractClientState
            _state = ContractClientState.Instance;
            if (_state != null)
            {
                _state.OnSnapshotUpdated += HandleSnapshot;
                _state.OnContractResult += HandleResult;
            }
            else
            {
                Debug.LogWarning("[ContractBoardWindow] ContractClientState.Instance is null при OnEnable — подпишемся в Update");
            }
        }

        private void OnDisable()
        {
            if (_state != null)
            {
                _state.OnSnapshotUpdated -= HandleSnapshot;
                _state.OnContractResult -= HandleResult;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Lazy subscribe если ContractClientState не был готов в OnEnable
            if (_state == null)
            {
                _state = ContractClientState.Instance;
                if (_state != null)
                {
                    _state.OnSnapshotUpdated += HandleSnapshot;
                    _state.OnContractResult += HandleResult;
                }
            }
        }

        // ========================================================
        // UI BUILD
        // ========================================================

        private void EnsureBuilt()
        {
            if (_built) return;
            if (contractWindowUxml == null)
            {
                Debug.LogError("[ContractBoardWindow] UXML не задан");
                return;
            }
            _root = contractWindowUxml.Instantiate();
            if (contractWindowUss != null) _root.styleSheets.Add(contractWindowUss);

            // Подключаем к root visualElement UIDocument
            _doc.rootVisualElement.Add(_root);

            // Bind UI elements
            _mainContainer = _root.Q<VisualElement>("main-container");
            _titleLabel = _root.Q<Label>("title-label");
            _locationLabel = _root.Q<Label>("location-label");
            _debtLabel = _root.Q<Label>("debt-label");
            _activeCountLabel = _root.Q<Label>("active-count-label");
            _timeInfoLabel = _root.Q<Label>("time-info-label");
            _availableSection = _root.Q<VisualElement>("available-section");
            _activeSection = _root.Q<VisualElement>("active-section");
            _availableList = _root.Q<ListView>("available-list");
            _activeList = _root.Q<ListView>("active-list");
            _tabAvailableBtn = _root.Q<Button>("tab-available");
            _tabActiveBtn = _root.Q<Button>("tab-active");
            _acceptBtn = _root.Q<Button>("accept-btn");
            _completeBtn = _root.Q<Button>("complete-btn");
            _failBtn = _root.Q<Button>("fail-btn");
            _closeBtn = _root.Q<Button>("close-btn");
            _messageLabel = _root.Q<Label>("message-label");
            _hintLabel = _root.Q<Label>("hint-label");

            // Настройка ListView
            ConfigureListView(_availableList, onSelect: idx => { _selectedAvailableIndex = idx; OnSelectedAvailableChanged(); });
            ConfigureListView(_activeList, onSelect: idx => { _selectedActiveIndex = idx; OnSelectedActiveChanged(); });

            // Привязка событий
            if (_tabAvailableBtn != null) _tabAvailableBtn.clicked += () => SwitchTab("available");
            if (_tabActiveBtn != null) _tabActiveBtn.clicked += () => SwitchTab("active");
            if (_acceptBtn != null) _acceptBtn.clicked += OnAcceptClicked;
            if (_completeBtn != null) _completeBtn.clicked += OnCompleteClicked;
            if (_failBtn != null) _failBtn.clicked += OnFailClicked;
            if (_closeBtn != null) _closeBtn.clicked += OnCloseClicked;

            // Скрыть по умолчанию
            _mainContainer.style.display = visibleOnStart ? DisplayStyle.Flex : DisplayStyle.None;

            _built = true;
        }

        private void ConfigureListView(ListView listView, System.Action<int> onSelect)
        {
            if (listView == null) return;
            listView.makeItem = () => MakeContractRow();
            listView.bindItem = (element, index) => BindContractRow(element, index, listView);
            listView.fixedItemHeight = 50;
            listView.selectionType = SelectionType.Single;
            listView.selectedIndex = -1;
            listView.onSelectionChange += (items) =>
            {
                if (items == null) return;
                int idx = -1;
                foreach (var _ in items) { idx = listView.selectedIndex; break; }
                onSelect?.Invoke(idx);
            };
        }

        private VisualElement MakeContractRow()
        {
            var row = new VisualElement();
            row.AddToClassList("contract-row");
            var typeLabel = new Label { name = "type" };
            typeLabel.style.width = 90;
            typeLabel.style.fontSize = 12;
            row.Add(typeLabel);
            var itemLabel = new Label { name = "item" };
            itemLabel.style.flexGrow = 1;
            itemLabel.style.fontSize = 12;
            row.Add(itemLabel);
            var rewardLabel = new Label { name = "reward" };
            rewardLabel.style.width = 80;
            rewardLabel.style.fontSize = 12;
            rewardLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(rewardLabel);
            var timerLabel = new Label { name = "timer" };
            timerLabel.style.width = 50;
            timerLabel.style.fontSize = 12;
            timerLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(timerLabel);
            return row;
        }

        private void BindContractRow(VisualElement element, int index, ListView listView)
        {
            ContractDto c;
            if (listView == _availableList)
            {
                if (index < 0 || index >= _availableCache.Length) return;
                c = _availableCache[index];
            }
            else
            {
                if (index < 0 || index >= _activeCache.Length) return;
                c = _activeCache[index];
            }

            var typeLabel = element.Q<Label>("type");
            var itemLabel = element.Q<Label>("item");
            var rewardLabel = element.Q<Label>("reward");
            var timerLabel = element.Q<Label>("timer");

            if (typeLabel != null)
            {
                typeLabel.text = GetTypeDisplayName((ContractType)c.type);
                typeLabel.RemoveFromClassList("type-standard");
                typeLabel.RemoveFromClassList("type-urgent");
                typeLabel.RemoveFromClassList("type-receipt");
                typeLabel.AddToClassList(GetTypeClass((ContractType)c.type));
            }
            if (itemLabel != null) itemLabel.text = $"{c.displayName} x{c.quantity}  ({c.fromLocationId}→{c.toLocationId})";
            if (rewardLabel != null) rewardLabel.text = $"{c.reward:F0} CR";
            if (timerLabel != null) timerLabel.text = GetTimeRemainingString(c);
        }

        // ========================================================
        // SHOW / HIDE
        // ========================================================

        public void Show()
        {
            if (!_built) EnsureBuilt();
            if (_mainContainer == null) return;
            _mainContainer.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_mainContainer == null) return;
            _mainContainer.style.display = DisplayStyle.None;
        }

        public void Toggle()
        {
            if (_mainContainer == null) return;
            bool isVisible = _mainContainer.style.display == DisplayStyle.Flex;
            _mainContainer.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ========================================================
        // EVENT HANDLERS
        // ========================================================

        private void HandleSnapshot(ContractSnapshotDto snapshot)
        {
            if (_mainContainer == null || _mainContainer.style.display == DisplayStyle.None)
            {
                // Авто-открытие при получении snapshot (если игрок нажал C)
                Show();
            }

            // Заголовок
            if (_titleLabel != null) _titleLabel.text = "КОНТРАКТЫ НП";
            if (_locationLabel != null) _locationLabel.text = $"[{snapshot.displayName ?? snapshot.locationId}]";

            // Долг
            UpdateDebtDisplay(snapshot.debtAmount, (DebtLevel)snapshot.debtLevel);

            // Счётчики
            if (_activeCountLabel != null) _activeCountLabel.text = $"Активных: {snapshot.active?.Length ?? 0}/{GetMaxActive()}";
            if (_timeInfoLabel != null) _timeInfoLabel.text = $"Скорость: x{snapshot.marketTimeMultiplier:F1}";

            // Кэши для ListView
            _availableCache = snapshot.available ?? System.Array.Empty<ContractDto>();
            _activeCache = snapshot.active ?? System.Array.Empty<ContractDto>();

            if (_availableList != null)
            {
                _availableList.itemsSource = _availableCache;
                _availableList.Rebuild();
            }
            if (_activeList != null)
            {
                _activeList.itemsSource = _activeCache;
                _activeList.Rebuild();
            }

            // Если нет доступных и нет активных — сообщение
            if (_availableCache.Length == 0 && _activeCache.Length == 0)
            {
                ShowMessage("Нет контрактов на этой локации");
            }
            else
            {
                ShowMessage($"Доступно: {_availableCache.Length} | Активных: {_activeCache.Length}");
            }
        }

        private void HandleResult(ContractResultDto result)
        {
            ShowMessage(result.message);

            // Если был accept/complete/fail и в snapshot появился новый active —
            // подсветить соответствующую вкладку
            if (result.success && result.updatedContract.HasValue)
            {
                var c = result.updatedContract.Value;
                if (c.state == (byte)ContractState.Active)
                {
                    SwitchTab("active");
                }
            }
        }

        // ========================================================
        // UI ACTIONS
        // ========================================================

        private void OnAcceptClicked()
        {
            if (_selectedAvailableIndex < 0 || _selectedAvailableIndex >= _availableCache.Length)
            {
                ShowMessage("Выберите контракт!");
                return;
            }
            var c = _availableCache[_selectedAvailableIndex];
            if (ContractClientState.Instance == null) return;
            ContractClientState.Instance.RequestAccept(c.contractId);
        }

        private void OnCompleteClicked()
        {
            if (_selectedActiveIndex < 0 || _selectedActiveIndex >= _activeCache.Length)
            {
                ShowMessage("Выберите активный контракт!");
                return;
            }
            var c = _activeCache[_selectedActiveIndex];
            if (ContractClientState.Instance == null) return;
            ContractClientState.Instance.RequestComplete(c.contractId);
        }

        private void OnFailClicked()
        {
            if (_selectedActiveIndex < 0 || _selectedActiveIndex >= _activeCache.Length)
            {
                ShowMessage("Выберите активный контракт!");
                return;
            }
            var c = _activeCache[_selectedActiveIndex];
            if (ContractClientState.Instance == null) return;
            ContractClientState.Instance.RequestFail(c.contractId);
        }

        private void OnCloseClicked()
        {
            Hide();
        }

        private void OnSelectedAvailableChanged()
        {
            if (_selectedAvailableIndex < 0 || _selectedAvailableIndex >= _availableCache.Length) return;
            var c = _availableCache[_selectedAvailableIndex];
            ShowMessage($"{GetTypeDisplayName((ContractType)c.type)} | {c.displayName} x{c.quantity} | {c.fromLocationId}→{c.toLocationId} | {c.reward:F0} CR | {GetTimeRemainingString(c)}");
        }

        private void OnSelectedActiveChanged()
        {
            if (_selectedActiveIndex < 0 || _selectedActiveIndex >= _activeCache.Length) return;
            var c = _activeCache[_selectedActiveIndex];
            ShowMessage($"{GetTypeDisplayName((ContractType)c.type)} | {c.displayName} x{c.quantity} | {c.fromLocationId}→{c.toLocationId} | {GetTimeRemainingString(c)} | Награда: {c.reward:F0} CR");
        }

        private void SwitchTab(string tab)
        {
            _activeTab = tab;
            if (tab == "available")
            {
                if (_availableSection != null) _availableSection.style.display = DisplayStyle.Flex;
                if (_activeSection != null) _activeSection.style.display = DisplayStyle.None;
                _selectedActiveIndex = -1;
            }
            else
            {
                if (_availableSection != null) _availableSection.style.display = DisplayStyle.None;
                if (_activeSection != null) _activeSection.style.display = DisplayStyle.Flex;
                _selectedAvailableIndex = -1;
            }
        }

        // ========================================================
        // UI HELPERS
        // ========================================================

        private void UpdateDebtDisplay(float debtAmount, DebtLevel level)
        {
            if (_debtLabel == null) return;
            if (debtAmount > 0f)
            {
                _debtLabel.text = $"[ДОЛГ] {debtAmount:F0} CR — {GetDebtPenaltyString(level)}";
                _debtLabel.style.display = DisplayStyle.Flex;
                _debtLabel.style.color = GetDebtColor(level);
            }
            else
            {
                _debtLabel.style.display = DisplayStyle.None;
            }
        }

        private void ShowMessage(string msg)
        {
            if (_messageLabel != null) _messageLabel.text = msg;
        }

        private int GetMaxActive()
        {
            if (ProjectC.Trade.Network.ContractServer.Instance != null)
            {
                return 3; // default; сервер может изменить через inspector
            }
            return 3;
        }

        // ========================================================
        // STATIC HELPERS (для UI и для ContractWorld/Server)
        // ========================================================

        public static string GetTypeDisplayName(ContractType type)
        {
            switch (type)
            {
                case ContractType.Standard: return "[Стандарт]";
                case ContractType.Urgent: return "[Срочный]";
                case ContractType.Receipt: return "[Расписка]";
                default: return type.ToString();
            }
        }

        public static string GetTypeClass(ContractType type)
        {
            switch (type)
            {
                case ContractType.Standard: return "type-standard";
                case ContractType.Urgent: return "type-urgent";
                case ContractType.Receipt: return "type-receipt";
                default: return "type-standard";
            }
        }

        public static string GetTimeRemainingString(ContractDto c)
        {
            if (c.timeLimit <= 0f) return "∞";
            int minutes = Mathf.FloorToInt(c.timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(c.timeRemaining % 60f);
            return $"{minutes}:{seconds:D2}";
        }

        public static string GetTimerClass(ContractDto c)
        {
            if (c.timeLimit <= 0f) return "timer-ok";
            float pct = c.timeRemaining / c.timeLimit;
            if (pct < 0.1f) return "timer-danger";
            if (pct < 0.3f) return "timer-warn";
            return "timer-ok";
        }

        public static string GetDebtPenaltyString(DebtLevel level)
        {
            switch (level)
            {
                case DebtLevel.None: return "Нет долга";
                case DebtLevel.Warning: return "Предупреждение НП";
                case DebtLevel.Restricted: return "Ограничение контрактов";
                case DebtLevel.Hunted: return "Патруль НП преследует";
                case DebtLevel.Bounty: return "Ордер на арест";
                case DebtLevel.Headhunt: return "Наёмные охотники";
                default: return level.ToString();
            }
        }

        public static Color GetDebtColor(DebtLevel level)
        {
            switch (level)
            {
                case DebtLevel.None: return Color.white;
                case DebtLevel.Warning: return new Color(1f, 0.9f, 0.4f);
                case DebtLevel.Restricted: return new Color(1f, 0.5f, 0f);
                case DebtLevel.Hunted: return new Color(1f, 0.3f, 0f);
                case DebtLevel.Bounty: return new Color(1f, 0.2f, 0.2f);
                case DebtLevel.Headhunt: return new Color(0.8f, 0f, 0.8f);
                default: return Color.white;
            }
        }
    }
}
