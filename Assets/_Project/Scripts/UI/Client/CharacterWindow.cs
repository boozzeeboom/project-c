// =====================================================================================
// CharacterWindow.cs — окно "Персонаж" игрока (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/Character-menu/00_OVERVIEW.md       — общий план
//   • docs/Character-menu/10_DESIGN.md         — UXML/USS дизайн
//   • docs/Character-menu/20_IMPLEMENTATION_PLAN.md — пошаговый план (Фазы 0-3)
//   • docs/Character-menu/30_VERIFICATION.md   — чек-листы проверки
//
// Паттерн скопирован с MarketWindow.cs (Assets/_Project/Trade/Scripts/Client/MarketWindow.cs):
//   • UI Toolkit окно (UIDocument + VisualTreeAsset)
//   • 4 FIX'а: pickingMode=Ignore при Hide / Position при Show,
//     ApplyInlineFallbackStyles() на 1-м кадре,
//     Cursor.lockState на Show/Hide,
//     MarkDirtyRepaint() + schedule.Execute(StartingIn(50))
//
// Отличия от MarketWindow:
//   • 5 табов вместо 3: ПЕРСОНАЖ / КОРАБЛЬ / РЕПУТАЦИЯ / КОНТРАКТЫ / ИНВЕНТАРЬ
//   • Контракты и инвентарь РЕЮЗЯТ данные (ContractClientState, NetworkPlayer.Inventory)
//     — single source of truth, ничего не дублируется.
//   • Корабль и Репутация — read-only плейсхолдеры (серверные системы в разработке).
//
// Namespace:  ProjectC.UI.Client  (см. AGENTS.md coding conventions).
// Создание GO:  вручную или через MCP — рядом с [MarketWindow] в BootstrapScene.
//                Компоненты: UIDocument (PanelSettings=MarketPanelSettings, SourceAsset=CharacterWindow.uxml)
//                            + CharacterWindow. Resources fallback покрывает случай пустого SourceAsset.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Items;
using ProjectC.Items.Client;
using ProjectC.Items.Dto;
using ProjectC.Network;
using ProjectC.Player;
using ProjectC.Quests;
using ProjectC.Quests.Client;
using ProjectC.Quests.Dto;
using ProjectC.Quests.UI;
using ProjectC.Trade;
using ProjectC.Trade.Client;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Network;

namespace ProjectC.UI.Client
{
    [RequireComponent(typeof(UIDocument))]
    public class CharacterWindow : MonoBehaviour
    {
        public static CharacterWindow Instance { get; private set; }

        // ============================================================
        // Inspector
        // ============================================================
        [Header("UI Assets (можно Resources fallback)")]
        [SerializeField] private VisualTreeAsset characterWindowUxml;
        [SerializeField] private StyleSheet       characterWindowUss;

        [Header("Behavior")]
        [SerializeField] private bool visibleOnStart = false;

        // ============================================================
        // Runtime refs
        // ============================================================
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _mainContainer;
        private bool _built;

        // --- Header / info ---
        private Label _characterNameLabel;
        private Label _timeInfoLabel;
        private Label _creditsLabel;
        private Label _locationLabel;
        private Label _messageLabel;

        // --- Sections (6 табов) ---
        private VisualElement _characterSection;
        private VisualElement _shipSection;
        private VisualElement _reputationSection;
        private VisualElement _contractsSection;
        private VisualElement _inventorySection;
        private VisualElement _questsSection;
        private VisualElement _filtersRow;

        // --- Tab buttons ---
        private Button _tabCharacter;
        private Button _tabShip;
        private Button _tabReputation;
        private Button _tabContracts;
        private Button _tabInventory;
        private Button _tabQuests;

        // --- ListViews ---
        private ListView _reputationList;
        private ListView _contractsList;
        private ListView _inventoryList;
        private ListView _questsActiveList;
        private ListView _questsCompletedList;
        private ListView _questsFailedList;
        private ListView _questsDiscoveredList;

        // --- Action buttons ---
        private Button _acceptBtn;
        private Button _completeBtn;
        private Button _failBtn;
        private Button _acceptQuestBtn;
        private Button _closeBtn;

        // --- Filters (Контракты / Инвентарь) ---
        private DropdownField _filterSource;
        private DropdownField _filterState;
        private TextField     _filterSearch;

        // --- Stats: character section ---
        private Label _statName;
        private Label _statLevel;
        private Label _statXp;
        private Label _statCredits;
        private Label _statDebt;
        private Label _statActiveContracts;

        // --- Stats: ship section ---
        private Label _shipName;
        private Label _shipState;
        private Label _shipSpeed;
        private Label _shipFuel;
        private Label _shipCargo;

        // ============================================================
        // State
        // ============================================================
        // Допустимые значения _activeTab: "character" | "ship" | "reputation" | "contracts" | "inventory".
        // Новый таб = добавить Button + Section + case в SwitchTab.
        private string _activeTab = "character";

        private int _selectedContractItem = -1;
        private int _selectedInventoryItem = -1;

        private ContractDto[] _contractsCache = Array.Empty<ContractDto>();
        private List<InventoryListItem> _inventoryCache = new List<InventoryListItem>();
        private List<ReputationListItem> _reputationCache = new List<ReputationListItem>();
        private List<QuestListItem> _questsActiveCache = new List<QuestListItem>();
        private List<QuestListItem> _questsCompletedCache = new List<QuestListItem>();
        private List<QuestListItem> _questsFailedCache = new List<QuestListItem>();
        private List<QuestListItem> _questsDiscoveredCache = new List<QuestListItem>();
        private int _selectedDiscoveredQuest = -1;

        // ============================================================
        // Cached state-проекции (НЕ создаём свои singleton'ы)
        // ============================================================
        private ContractClientState _contractState;
        private NetworkPlayer       _localPlayer;

        // ============================================================
        // DTO-проекции для ListView
        // ============================================================
        private struct InventoryListItem
        {
            public string  itemId;
            public string  displayName;
            public ItemType type;
            public int     quantity;
            public Sprite  icon;
        }

        private struct ReputationListItem
        {
        public string factionId;
        public string displayName;
        public int value; // -100..+100 (GDD-23)
        public Color color;
        }

        // T-Q11: quest log projection для4 под-секций (Active/Completed/Failed/Discovered).
        private struct QuestListItem
        {
        public string questId;
        public string displayName;
        public byte state; // ProjectC.Quests.QuestState
        public string stateLabel; // "ACTIVE" / "COMPLETED" / "FAILED" / "DISCOVERED"
        public string stateBadge; // .quest-row-state-* CSS class
        public string objectivesSummary; // "3/5 objectives" или пусто
        public int objectiveCompletedCount;
        public int objectiveTotalCount;
        }

        // ============================================================
        // Lifecycle
        // ============================================================

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (characterWindowUxml == null) characterWindowUxml = Resources.Load<VisualTreeAsset>("UI/CharacterWindow");
            if (characterWindowUss  == null) characterWindowUss  = Resources.Load<StyleSheet>("UI/CharacterWindow");
            if (Instance == null) Instance = this;
        }

        private void OnEnable()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                Debug.LogError("[CharacterWindow] нет UIDocument на GameObject");
                return;
            }
            EnsureBuilt();
        }

        private void Start()
        {
            // FIX: после всех OnEnable (включая UIDocument, который мог подвесить свой
            // UXML-auto-load поверх нашего дерева) — перепроверяем состояние. Если
            // main-container потерял ширину, пересобираем.
            if (!_built || !IsLayoutValid())
            {
                Debug.LogWarning("[CharacterWindow] Start(): layout invalid, rebuilding");
                EnsureBuilt();
            }
        }

        private void OnDisable()
        {
        if (_contractState != null)
        {
        _contractState.OnSnapshotUpdated -= HandleContractSnapshot;
        _contractState.OnContractResult -= HandleContractResult;
        }
        // BUGFIX2026-06-05: используем флаг-версию (UnsubscribeInventory).
        // Старая версия делала bare -=, и если подписки не было — flag оставался неверным.
        UnsubscribeInventory();
        // T-Q11: Unsubscribe QuestClientState (3 события).
        UnsubscribeQuestState();
        }

        private bool _isInventorySubscribed = false;

        // BUGFIX 2026-06-05: lazy-subscribe встроен в Update ниже (строка 255+).
        // Helpers Subscribe/Unsubscribe оставлены для идемпотентности.

        private void SubscribeInventory()
        {
            if (_isInventorySubscribed) return;
            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState == null) return;
            invState.OnSnapshotUpdated += HandleInventorySnapshotUpdated;
            invState.OnInventoryResult += HandleInventoryResultReceived;
            _isInventorySubscribed = true;
        }

        private void UnsubscribeInventory()
        {
            if (!_isInventorySubscribed) return;
            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState == null) { _isInventorySubscribed = false; return; }
            invState.OnSnapshotUpdated -= HandleInventorySnapshotUpdated;
            invState.OnInventoryResult -= HandleInventoryResultReceived;
            _isInventorySubscribed = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // BUGFIX 2026-06-05: lazy-subscribe для InventoryClientState.
            // В EnsureBuilt Instance мог быть null (race condition с NMC.Awake).
            // Если ещё не подписаны, а Instance уже жив — подписываемся.
            if (_built && !_isInventorySubscribed)
            {
                var invState = ProjectC.Items.Client.InventoryClientState.Instance;
                if (invState != null)
                {
                    SubscribeInventory();
                    invState.RequestRefresh();
                    Debug.Log("[CharacterWindow] Lazy-subscribed to InventoryClientState.OnSnapshotUpdated");
                }
            }

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            if (!nm.IsClient && !nm.IsServer) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsVisible())
            {
                Hide();
            }
            // P-handler реализован в NetworkPlayer.Update (P = "Press" / "Profile" / "Person"),
            // см. docs/Character-menu/00_OVERVIEW.md §7. Здесь — только Esc для закрытия.
        }

        // ============================================================
        // Layout / build helpers
        // ============================================================

        private bool IsLayoutValid()
        {
            // Реюз правила из MarketWindow.IsLayoutValid():
            // resolvedStyle.width может быть NaN/0 на 1-м кадре — НЕ полагаемся.
            return _built && _root != null && _mainContainer != null;
        }

        private void EnsureBuilt()
        {
            if (_doc.rootVisualElement == null) return;
            if (characterWindowUxml == null) characterWindowUxml = Resources.Load<VisualTreeAsset>("UI/CharacterWindow");
            if (characterWindowUss  == null) characterWindowUss  = Resources.Load<StyleSheet>("UI/CharacterWindow");
            if (characterWindowUxml == null)
            {
                Debug.LogError("[CharacterWindow] UXML не найден в Resources/UI/");
                return;
            }

            _doc.rootVisualElement.Clear();
            if (characterWindowUss != null) _doc.rootVisualElement.styleSheets.Add(characterWindowUss);
            _root = characterWindowUxml.CloneTree();
            // FIX: TemplateContainer (НЕ VisualElement) с дефолт 0×0 → нужно явно растянуть.
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            // FIX: невидимый _root перехватывает клики → ставим Ignore до Show().
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            // DIAG снят в v2: USS подключён корректно через Inspector (см. refactor_log_2026-06-05.md).
            Debug.Log("[CharacterWindow] Built");

            // ---- Element refs ----
            _mainContainer         = _root.Q<VisualElement>("main-container");
            _characterNameLabel    = _root.Q<Label>("character-name-label");
            _timeInfoLabel         = _root.Q<Label>("time-info-label");
            _creditsLabel          = _root.Q<Label>("credits-label");
            _locationLabel         = _root.Q<Label>("location-label");
            _messageLabel          = _root.Q<Label>("message-label");

            _characterSection = _root.Q<VisualElement>("character-section");
            _shipSection = _root.Q<VisualElement>("ship-section");
            _reputationSection = _root.Q<VisualElement>("reputation-section");
            _contractsSection = _root.Q<VisualElement>("contracts-section");
            _inventorySection = _root.Q<VisualElement>("inventory-section");
            _questsSection = _root.Q<VisualElement>("quests-section");
            _filtersRow = _root.Q<VisualElement>("filters-row");

            _tabCharacter = _root.Q<Button>("tab-character");
            _tabShip = _root.Q<Button>("tab-ship");
            _tabReputation = _root.Q<Button>("tab-reputation");
            _tabContracts = _root.Q<Button>("tab-contracts");
            _tabInventory = _root.Q<Button>("tab-inventory");
            _tabQuests = _root.Q<Button>("tab-quests");

            _reputationList = _root.Q<ListView>("reputation-list");
            _contractsList = _root.Q<ListView>("contracts-list");
            _inventoryList = _root.Q<ListView>("inventory-list");
            _questsActiveList = _root.Q<ListView>("quests-active-list");
            _questsCompletedList = _root.Q<ListView>("quests-completed-list");
            _questsFailedList = _root.Q<ListView>("quests-failed-list");
            _questsDiscoveredList = _root.Q<ListView>("quests-discovered-list");

            _acceptBtn = _root.Q<Button>("accept-btn");
            _completeBtn = _root.Q<Button>("complete-btn");
            _failBtn = _root.Q<Button>("fail-btn");
            _acceptQuestBtn = _root.Q<Button>("accept-quest-btn");
            _closeBtn = _root.Q<Button>("close-btn");

            _filterSource          = _root.Q<DropdownField>("filter-source");
            _filterState           = _root.Q<DropdownField>("filter-state");
            _filterSearch          = _root.Q<TextField>("filter-search");

            _statName              = _root.Q<Label>("stat-name");
            _statLevel             = _root.Q<Label>("stat-level");
            _statXp                = _root.Q<Label>("stat-xp");
            _statCredits           = _root.Q<Label>("stat-credits");
            _statDebt              = _root.Q<Label>("stat-debt");
            _statActiveContracts   = _root.Q<Label>("stat-active-contracts");

            _shipName              = _root.Q<Label>("ship-name");
            _shipState             = _root.Q<Label>("ship-state");
            _shipSpeed             = _root.Q<Label>("ship-speed");
            _shipFuel              = _root.Q<Label>("ship-fuel");
            _shipCargo             = _root.Q<Label>("ship-cargo");

            // ---- Tab subscriptions ----
            if (_tabCharacter != null) _tabCharacter.clicked += () => SwitchTab("character");
            if (_tabShip != null) _tabShip.clicked += () => SwitchTab("ship");
            if (_tabReputation != null) _tabReputation.clicked += () => SwitchTab("reputation");
            if (_tabContracts != null) _tabContracts.clicked += () => SwitchTab("contracts");
            if (_tabInventory != null) _tabInventory.clicked += () => SwitchTab("inventory");
            if (_tabQuests != null) _tabQuests.clicked += () => SwitchTab("quests");

            // ---- Action buttons ----
            if (_closeBtn != null) _closeBtn.clicked += OnCloseClicked;
            if (_acceptBtn != null) _acceptBtn.clicked += OnAcceptContractClicked;
            if (_completeBtn != null) _completeBtn.clicked += OnCompleteContractClicked;
            if (_failBtn != null) _failBtn.clicked += OnFailContractClicked;
            if (_acceptQuestBtn != null) _acceptQuestBtn.clicked += OnAcceptQuestClicked;

            // ---- ListView: Contracts (re-use MarketWindow factory) ----
            if (_contractsList != null)
            {
                _contractsList.makeItem      = MakeContractRow;
                _contractsList.bindItem      = BindContractRow;
                _contractsList.fixedItemHeight = 32;
                _contractsList.selectionType = SelectionType.Single;
                _contractsList.selectedIndex = -1;
                _contractsList.selectionChanged += selectedItems =>
                {
                    _selectedContractItem = FindSelectedItemIndex<ContractDto>(_contractsList, selectedItems);
                    _contractsList.Rebuild();
                };
            }

            // ---- ListView: Inventory (новый) ----
            if (_inventoryList != null)
            {
                _inventoryList.makeItem      = MakeInventoryRow;
                _inventoryList.bindItem      = BindInventoryRow;
                _inventoryList.fixedItemHeight = 32;
                _inventoryList.selectionType = SelectionType.Single;
                _inventoryList.selectedIndex = -1;
            }

            // ---- ListView: Reputation (новый) ----
            if (_reputationList != null)
            {
            _reputationList.makeItem = MakeReputationRow;
            _reputationList.bindItem = BindReputationRow;
            _reputationList.fixedItemHeight =32;
            }

            // ---- ListView: Quests (T-Q11:4 под-секции, общий row factory) ----
            // T-Q11: каждый список (active/completed/failed/discovered) имеет один factory.
            // per-row state badge CSS class берётся из QuestListItem.stateBadge.
            SetupQuestListView(_questsActiveList, ref _questsActiveCache);
            SetupQuestListView(_questsCompletedList, ref _questsCompletedCache);
            SetupQuestListView(_questsFailedList, ref _questsFailedCache);
            // Discovered — отдельный ListView с selectionChanged (Accept-кнопка работает per row).
            if (_questsDiscoveredList != null)
            {
            _questsDiscoveredList.makeItem = MakeQuestRow;
            _questsDiscoveredList.bindItem = BindQuestRow;
            _questsDiscoveredList.fixedItemHeight =28;
            _questsDiscoveredList.selectionType = SelectionType.Single;
            _questsDiscoveredList.selectedIndex = -1;
            _questsDiscoveredList.selectionChanged += selectedItems =>
            {
            _selectedDiscoveredQuest = FindSelectedItemIndex<QuestListItem>(_questsDiscoveredList, selectedItems);
            if (_questsDiscoveredList != null) _questsDiscoveredList.Rebuild();
            };
            }

            // ---- Filters (options зависят от активного таба — см. SwitchTab) ----
            if (_filterSearch != null)
            {
                _filterSearch.RegisterValueChangedCallback(evt =>
                {
                    if (_activeTab == "contracts") ApplyContractFilters();
                    else if (_activeTab == "inventory") ApplyInventoryFilters();
                });
            }

            // ---- Subscribe to ContractClientState (re-use MarketWindow pattern) ----
            _contractState = ContractClientState.Instance;
            if (_contractState == null)
            {
                Debug.LogWarning("[CharacterWindow] ContractClientState.Instance == null, таб 'Контракты' не будет обновляться (нормально до StartHost)");
            }
            else
            {
                _contractState.OnSnapshotUpdated += HandleContractSnapshot;
                _contractState.OnContractResult  += HandleContractResult;
                // Если игрок уже в зоне рынка — попросить свежий snapshot.
                // Безопасно: ContractClientState.RequestList сам фильтрует null/NotInZone.
                var nearestZone = MarketZoneRegistry.LocalPlayerZone;
                if (nearestZone != null) _contractState.RequestList(nearestZone.LocationId);
            }

            // ---- Phase5 (INVENTORY_V2_REFACTOR.md): Subscribe to InventoryClientState ----
            // Синглтон создаётся в NetworkManagerController.Awake — обычно уже есть
            // к моменту EnsureBuilt. Если null (тест/edit-mode) — Update() подпишет lazy.
            // BUGFIX2026-06-05: используем флаг-версию SubscribeInventory (идемпотентно).
            SubscribeInventory();
            if (ProjectC.Items.Client.InventoryClientState.Instance == null)
            {
            Debug.LogWarning("[CharacterWindow] InventoryClientState.Instance == null на момент EnsureBuilt — Update() lazy-подпишется");
            }

            // ---- T-Q11: Subscribe to QuestClientState (Quest log + Discovered events) ----
            // QuestClientState singleton создаётся через RuntimeInitializeOnLoadMethod в его AutoSpawn,
            // плюс scene-placed в BootstrapScene — всегда есть к моменту EnsureBuilt.
            SubscribeQuestState();
            if (QuestClientState.Instance == null)
            {
            Debug.LogWarning("[CharacterWindow] QuestClientState.Instance == null на момент EnsureBuilt — таб 'КВЕСТЫ' не будет обновляться (нормально до StartHost)");
            }

            // ---- Initial state ----
            SwitchTab(_activeTab);
            // REFACTOR 2026-06-05 v2: USS с !important перебивает UnityDefaultRuntimeTheme,
            // поэтому отдельная программная стилизация больше не нужна.
            SetVisible(visibleOnStart);
            _doc.rootVisualElement.MarkDirtyRepaint();
            if (_doc.rootVisualElement != null)
            {
                _doc.rootVisualElement.schedule.Execute(() => _doc.rootVisualElement.MarkDirtyRepaint()).StartingIn(50);
            }
            _built = true;
        }

        // ============================================================
        // Tab switching
        // ============================================================

        private void SwitchTab(string tab)
        {
        _activeTab = tab;
        bool isCharacter = tab == "character";
        bool isShip = tab == "ship";
        bool isReputation = tab == "reputation";
        bool isContracts = tab == "contracts";
        bool isInventory = tab == "inventory";
        bool isQuests = tab == "quests";

        // ---- Sections visibility ----
        if (_characterSection != null) _characterSection.style.display = isCharacter ? DisplayStyle.Flex : DisplayStyle.None;
        if (_shipSection != null) _shipSection.style.display = isShip ? DisplayStyle.Flex : DisplayStyle.None;
        if (_reputationSection != null) _reputationSection.style.display = isReputation ? DisplayStyle.Flex : DisplayStyle.None;
        if (_contractsSection != null) _contractsSection.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
        if (_inventorySection != null) _inventorySection.style.display = isInventory ? DisplayStyle.Flex : DisplayStyle.None;
        if (_questsSection != null) _questsSection.style.display = isQuests ? DisplayStyle.Flex : DisplayStyle.None;

        // BUGFIX2026-06-05: display: none → flex на ListView в первый раз
        // не вызывает повторный layout — нужно принудительно MarkDirtyRepaint
        // (иначе строки не отрисованы до следующего toggle). UI Toolkit pitfall.
        if (isInventory && _inventoryList != null) {
        _inventoryList.MarkDirtyRepaint();
        }
        if (isQuests)
        {
        // T-Q11: все4 quest ListView требуют re-paint после первого display:flex.
        if (_questsActiveList != null) _questsActiveList.MarkDirtyRepaint();
        if (_questsCompletedList != null) _questsCompletedList.MarkDirtyRepaint();
        if (_questsFailedList != null) _questsFailedList.MarkDirtyRepaint();
        if (_questsDiscoveredList != null) _questsDiscoveredList.MarkDirtyRepaint();
        }

        // ---- Active tab visual ----
        SetActiveTabVisual(_tabCharacter, isCharacter);
        SetActiveTabVisual(_tabShip, isShip);
        SetActiveTabVisual(_tabReputation, isReputation);
        SetActiveTabVisual(_tabContracts, isContracts);
        SetActiveTabVisual(_tabInventory, isInventory);
        SetActiveTabVisual(_tabQuests, isQuests);

        // ---- Filters visibility + options ----
        if (_filtersRow != null)
        {
        bool showFilters = isContracts || isInventory;
        _filtersRow.style.display = showFilters ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (isContracts)
        {
        ConfigureContractFilters();
        }
        else if (isInventory)
        {
        ConfigureInventoryFilters();
        }

        // ---- Action buttons ----
        if (_acceptBtn != null) _acceptBtn.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
        if (_completeBtn != null) _completeBtn.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
        if (_failBtn != null) _failBtn.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
        if (_acceptQuestBtn != null) _acceptQuestBtn.style.display = isQuests ? DisplayStyle.Flex : DisplayStyle.None;
        if (_closeBtn != null) _closeBtn.style.display = DisplayStyle.Flex; // всегда

        // ---- Refresh data for the active tab ----
        if (isCharacter) RefreshCharacterStats();
        if (isShip) RefreshShipStats();
        if (isReputation) RefreshReputationCache();
        if (isInventory) { RefreshInventoryCache(); ApplyInventoryFilters(); }
        if (isContracts) ApplyContractFilters();
        if (isQuests) RefreshQuestsCache();
        }

        private static void SetActiveTabVisual(Button btn, bool isActive)
        {
            if (btn == null) return;
            if (isActive) btn.AddToClassList("active");
            else          btn.RemoveFromClassList("active");
        }

        // ============================================================
        // Filter configuration per tab
        // ============================================================

        private List<string> _contractFilterSourceOptions = new List<string> { "Все", "Контракты", "Квесты" };
        private List<string> _contractFilterStateOptions  = new List<string> { "Все", "Активные", "Доступные" };
        private List<string> _inventoryFilterSourceOptionsCache;  // динамически по ItemType
        private List<string> _inventoryFilterStateOptions  = new List<string> { "Все типы" };

        private void ConfigureContractFilters()
        {
            if (_filterSource != null)
            {
                _filterSource.choices = _contractFilterSourceOptions;
                if (!_contractFilterSourceOptions.Contains(_filterSource.value))
                    _filterSource.value = "Все";
            }
            if (_filterState != null)
            {
                _filterState.choices = _contractFilterStateOptions;
                _filterState.style.display = DisplayStyle.Flex;
                if (!_contractFilterStateOptions.Contains(_filterState.value))
                    _filterState.value = "Все";
            }
            // Подписки на change — только один раз (через RegisterValueChangedCallback в EnsureBuilt).
            // Здесь просто гарантируем актуальные options.
        }

        private void ConfigureInventoryFilters()
        {
            // Build dynamic options: "Все типы" + все 8 ItemType (ItemTypeNames)
            if (_inventoryFilterSourceOptionsCache == null)
            {
                _inventoryFilterSourceOptionsCache = new List<string> { "Все типы" };
                foreach (ItemType t in Enum.GetValues(typeof(ItemType)))
                {
                    _inventoryFilterSourceOptionsCache.Add(ItemTypeNames.GetDisplayName(t));
                }
            }
            if (_filterSource != null)
            {
                _filterSource.choices = _inventoryFilterSourceOptionsCache;
                if (!_inventoryFilterSourceOptionsCache.Contains(_filterSource.value))
                    _filterSource.value = "Все типы";
            }
            if (_filterState != null)
            {
                // Для инвентаря state-фильтр не имеет смысла — скрываем.
                _filterState.style.display = DisplayStyle.None;
            }
        }

        // ============================================================
        // Section refresh: character
        // ============================================================

        private void RefreshCharacterStats()
        {
            if (_localPlayer == null) FindLocalPlayer();

            // Имя
            if (_statName != null)
            {
                _statName.text = _localPlayer != null
                    ? (_localPlayer.IsLocalPlayer ? "Игрок (Owner)" : "Игрок")
                    : "—";
            }
            // Уровень / опыт — плейсхолдеры (отдельный тикет: серверная модель уровней)
            if (_statLevel != null) _statLevel.text = "1";
            if (_statXp    != null) _statXp.text    = "0";

            // Кредиты — берём из MarketClientState (single source of truth, как в MarketWindow)
            if (_statCredits != null)
            {
                var ms = MarketClientState.Instance;
                if (ms != null && ms.CurrentSnapshot.HasValue)
                    _statCredits.text = $"{ms.CurrentSnapshot.Value.credits:F0} CR";
                else
                    _statCredits.text = "0 CR";
            }

            // Долг — из ContractClientState.CurrentSnapshot
            if (_statDebt != null)
            {
                var cs = ContractClientState.Instance;
                float debt = (cs != null && cs.CurrentSnapshot.HasValue) ? cs.CurrentSnapshot.Value.debtAmount : 0f;
                _statDebt.text = $"{debt:F0} CR";
                _statDebt.RemoveFromClassList("debt-warn");
                _statDebt.RemoveFromClassList("debt-danger");
                if (debt > 1000f)       _statDebt.AddToClassList("debt-danger");
                else if (debt > 100f)   _statDebt.AddToClassList("debt-warn");
            }

            // Активные контракты
            if (_statActiveContracts != null)
            {
                var cs = ContractClientState.Instance;
                int active = (cs != null && cs.CurrentSnapshot.HasValue)
                    ? (cs.CurrentSnapshot.Value.active?.Length ?? 0)
                    : 0;
                _statActiveContracts.text = active.ToString();
            }
        }

        // ============================================================
        // Section refresh: ship
        // ============================================================

        private void RefreshShipStats()
        {
            if (_localPlayer == null) FindLocalPlayer();
            // Сейчас — read-only плейсхолдеры. Когда появится ShipClientState,
            // заменим заглушки на чтение из него.
            if (_shipName  != null) _shipName.text  = "—";
            if (_shipState != null) _shipState.text = _localPlayer != null
                ? (_localPlayer.IsInShip ? "В корабле" : "На палубе")
                : "—";
            if (_shipSpeed != null) _shipSpeed.text = "0";
            if (_shipFuel  != null) _shipFuel.text  = "—";
            if (_shipCargo != null) _shipCargo.text = "—";
        }

        // ============================================================
        // Section refresh: reputation (плейсхолдер 5 гильдий GDD-23)
        // ============================================================

        private void RefreshReputationCache()
        {
            // 5 фракций из GDD-23 (все value=0; серверная модель в разработке).
            // Когда появится ReputationClientState, читать из него.
            _reputationCache = new List<ReputationListItem>
            {
                new ReputationListItem { factionId = "merchants",  displayName = "Гильдия Торговцев",   value = 0, color = new Color(0.60f, 0.80f, 0.40f) },
                new ReputationListItem { factionId = "engineers",  displayName = "Мануфактура «Аврора»", value = 0, color = new Color(0.40f, 0.60f, 0.90f) },
                new ReputationListItem { factionId = "military",   displayName = "Военный Анклав",      value = 0, color = new Color(0.80f, 0.40f, 0.40f) },
                new ReputationListItem { factionId = "resistance", displayName = "Сопротивление",       value = 0, color = new Color(0.70f, 0.50f, 0.90f) },
                new ReputationListItem { factionId = "smugglers",  displayName = "Чёрный Рынок",        value = 0, color = new Color(0.55f, 0.55f, 0.55f) },
            };
            if (_reputationList != null)
            {
                _reputationList.itemsSource = _reputationCache;
                _reputationList.Rebuild();
            }
        }

        // ============================================================
        // Section refresh: inventory (read from InventoryClientState — v2)
        // ============================================================
        //
        // Phase 5 (INVENTORY_V2_REFACTOR.md): Раньше читался ЛОКАЛЬНЫЙ Inventory через
        // GetComponentInChildren<Inventory>() + reflection на _inventory. Это давало
        // рассинхрон с сервером (NetworkInventory обновлялся независимо). Теперь —
        // единственный source of truth: InventoryClientState (server-authoritative
        // snapshot). Подписка на OnSnapshotUpdated в EnsureBuilt.

        private void RefreshInventoryCache()
        {
            _inventoryCache.Clear();
            if (_localPlayer == null) FindLocalPlayer();

            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState == null || !invState.CurrentSnapshot.HasValue)
            {
                // Данных ещё нет — пустой кэш. UI покажет пустой список, message "Загрузка..."
                Debug.Log($"[CharacterWindow.RefreshInventoryCache] no-snapshot: invState={(invState!=null?"OK":"NULL")}, list={(_inventoryList!=null?"OK":"NULL")}");
                if (_inventoryList != null)
                {
                    if (!ReferenceEquals(_inventoryList.itemsSource, _inventoryCache))
                        _inventoryList.itemsSource = _inventoryCache;
                    _inventoryList.RefreshItems();
                }
                return;
            }

            var snap = invState.CurrentSnapshot.Value;
            var items = snap.items;
            if (items == null)
            {
                Debug.Log($"[CharacterWindow.RefreshInventoryCache] items=null, list={(_inventoryList!=null?"OK":"NULL")}");
                if (_inventoryList != null)
                {
                    if (!ReferenceEquals(_inventoryList.itemsSource, _inventoryCache))
                        _inventoryList.itemsSource = _inventoryCache;
                    _inventoryList.RefreshItems();
                }
                return;
            }

            // Группируем по (itemId) — если несколько стеков одного предмета,
            // суммируем quantity. (Phase 2: каждый itemId = 1 unit, так что
            // grouping ничего не делает, но код готов к будущему.)
            var groups = new Dictionary<int, (int totalQty, InventoryItemDto first)>();
            foreach (var dto in items)
            {
                // pitfall #14: InventoryItemDto — struct, == null не компилируется.
                // Проверяем через default или is-pattern.
                if (dto.itemId <= 0) continue;
                if (groups.TryGetValue(dto.itemId, out var existing))
                {
                    groups[dto.itemId] = (existing.totalQty + dto.quantity, existing.first);
                }
                else
                {
                    groups[dto.itemId] = (dto.quantity, dto);
                }
            }

            foreach (var kvp in groups)
            {
                var first = kvp.Value.first;
                ItemData def = invState.GetItemDefinition(first.itemId);
                _inventoryCache.Add(new InventoryListItem
                {
                    itemId      = first.itemId.ToString(),
                    displayName = def != null ? def.itemName : $"Item#{first.itemId}",
                    type        = (ItemType)first.type,
                    quantity    = kvp.Value.totalQty,
                    icon        = def != null ? def.icon : null,
                });
            }

            if (_inventoryList != null)
            {
                // BUGFIX 2026-06-05: используем RefreshItems() вместо Rebuild() + null-trick.
                // Rebuild() иногда не перебиндит строки, если itemsSource = тот же reference.
                // RefreshItems() вызывает bindItem для всех видимых элементов с текущим itemsSource.
                if (!ReferenceEquals(_inventoryList.itemsSource, _inventoryCache)) {
                    _inventoryList.itemsSource = _inventoryCache;
                }
                _inventoryList.RefreshItems();
            }
        }

        /// <summary>
        /// Phase 5 (INVENTORY_V2_REFACTOR.md): реакция на новый snapshot инвентаря.
        /// Если активен таб "Инвентарь" — обновляем UI (cache + filters + rebuild).
        /// </summary>
        private void HandleInventorySnapshotUpdated(InventorySnapshotDto snap)
        {
            // DIAG (bug report 2026-06-05): P-таб пуст при chest-add, но TAB обновляется.
            // Логируем чтобы понять — cache заполняется, _inventoryList != null?
            Debug.Log($"[CharacterWindow] HandleInventorySnapshotUpdated: items={(snap.items!=null?snap.items.Length:0)}, activeTab={_activeTab}, _inventoryList={(_inventoryList!=null?"OK":"NULL")}, cacheBefore={_inventoryCache.Count}");

            // Cross-tab: обновляем общий credits в header, если есть
            if (_creditsLabel != null)
            {
                _creditsLabel.text = $"Кредиты: {snap.credits:F0} CR";
            }
            if (_statCredits != null)
            {
                _statCredits.text = $"{snap.credits:F0} CR";
            }

            // FIX (bug report 2026-06-05): refresh cache UNCONDITIONALLY, не только при _activeTab == "inventory".
            // Причина: cache читает InventoryClientState.CurrentSnapshot (он ВСЕГДА обновлён).
            // Если не обновлять при неактивном табе, при следующем SwitchTab("inventory")
            // мы прочитаем stale cache. (ApplyInventoryFilters будет применять фильтры, RefreshInventoryCache перечитает.)
            // Сейчас безусловный refresh — SwitchTab всё равно пересоздаст UI.
            RefreshInventoryCache();
            if (_activeTab == "inventory")
            {
                ApplyInventoryFilters();
            }
        }

        /// <summary>
        /// Phase 5 (INVENTORY_V2_REFACTOR.md): реакция на результат операции (Pickup/Drop/Move/Use).
        /// Показываем feedback в message label — UNCONDITIONALLY (cross-tab, см. pitfall #11
        /// unity-v2-subsystem-migration skill).
        /// </summary>
        private void HandleInventoryResultReceived(InventoryResultDto result)
        {
            if (_messageLabel == null) return;

            if (!IsVisible()) return;  // не спамим в скрытом окне

            string msg = !string.IsNullOrEmpty(result.message)
                ? result.message
                : ProjectC.Items.Client.InventoryClientState.LocalizeResultCode(
                    (ProjectC.Items.Dto.InventoryResultCode)result.code);

            _messageLabel.text = msg;
            _messageLabel.style.color = result.IsSuccess
                ? new StyleColor(new Color(0.4f, 0.95f, 0.4f))
                : new StyleColor(new Color(0.95f, 0.4f, 0.4f));
        }

        // ============================================================
        // Row factories: contracts (реюз MarketWindow)
        // ============================================================

        private VisualElement MakeContractRow()
        {
            var row = new VisualElement();
            row.AddToClassList("contract-row");
            var typeLbl   = new Label { name = "row-type"   }; typeLbl.AddToClassList("contract-type");   row.Add(typeLbl);
            var itemLbl   = new Label { name = "row-item"   }; itemLbl.AddToClassList("contract-item");   row.Add(itemLbl);
            var rewardLbl = new Label { name = "row-reward" }; rewardLbl.AddToClassList("contract-reward"); row.Add(rewardLbl);
            var timerLbl  = new Label { name = "row-timer"  }; timerLbl.AddToClassList("contract-timer");  row.Add(timerLbl);
            return row;
        }

        private void BindContractRow(VisualElement row, int index)
        {
            if (_contractsList == null) return;
            var src = _contractsList.itemsSource as ContractDto[];
            if (src == null || index < 0 || index >= src.Length) return;
            var c = src[index];

            var typeLbl   = row.Q<Label>("row-type");
            var itemLbl   = row.Q<Label>("row-item");
            var rewardLbl = row.Q<Label>("row-reward");
            var timerLbl  = row.Q<Label>("row-timer");

            var typeName = GetContractTypeDisplayName((ContractType)c.type);
            typeLbl.text = typeName;
            typeLbl.RemoveFromClassList("type-standard");
            typeLbl.RemoveFromClassList("type-urgent");
            typeLbl.RemoveFromClassList("type-receipt");
            typeLbl.AddToClassList(GetContractTypeClass((ContractType)c.type));

            // item text: добавляем [ВЗЯТ] для Active (по аналогии с MarketWindow)
            string statePrefix = c.state == (byte)ContractState.Active ? "[ВЗЯТ] " : "";
            itemLbl.text = $"{statePrefix}{c.displayName} ×{c.quantity}";

            rewardLbl.text = $"{c.reward:F0} CR";
            timerLbl.text  = GetContractTimeRemainingString(c);
            timerLbl.RemoveFromClassList("timer-ok");
            timerLbl.RemoveFromClassList("timer-warn");
            timerLbl.RemoveFromClassList("timer-danger");
            timerLbl.AddToClassList(GetContractTimerClass(c));

            row.RemoveFromClassList("contract-row-active");
            if (c.state == (byte)ContractState.Active) row.AddToClassList("contract-row-active");
        }

        // ============================================================
        // Row factories: inventory (новый)
        // ============================================================

        private VisualElement MakeInventoryRow()
        {
            var row = new VisualElement();
            row.AddToClassList("inventory-row");
            var icon = new VisualElement { name = "row-icon" }; icon.AddToClassList("inventory-icon"); row.Add(icon);
            var name = new Label { name = "row-name" }; name.AddToClassList("inventory-name"); row.Add(name);
            var type = new Label { name = "row-type" }; type.AddToClassList("inventory-type"); row.Add(type);
            var qty  = new Label { name = "row-qty"  }; qty.AddToClassList("inventory-qty");   row.Add(qty);
            return row;
        }

        private void BindInventoryRow(VisualElement row, int index)
        {
            if (_inventoryList == null) return;
            var src = _inventoryList.itemsSource;
            if (src is List<InventoryListItem> list)
            {
                if (index < 0 || index >= list.Count) return;
                var item = list[index];
                var icon = row.Q<VisualElement>("row-icon");
                if (item.icon != null)
                    icon.style.backgroundImage = new StyleBackground(item.icon);
                else
                    icon.style.backgroundImage = new StyleBackground(StyleKeyword.Null);
                row.Q<Label>("row-name").text = item.displayName;
                row.Q<Label>("row-type").text = ItemTypeNames.GetDisplayName(item.type);
                row.Q<Label>("row-qty").text  = $"×{item.quantity}";
            }
        }

        // ============================================================
        // Row factories: reputation (новый)
        // ============================================================

        private VisualElement MakeReputationRow()
        {
            var row = new VisualElement();
            row.AddToClassList("reputation-row");
            var faction = new Label { name = "row-faction" }; faction.AddToClassList("reputation-faction"); row.Add(faction);
            var bar     = new VisualElement { name = "row-bar" }; bar.AddToClassList("reputation-bar"); row.Add(bar);
            var fill    = new VisualElement { name = "row-fill" }; fill.AddToClassList("reputation-fill"); bar.Add(fill);
            var value   = new Label { name = "row-value" }; value.AddToClassList("reputation-value"); row.Add(value);
            return row;
        }

        private void BindReputationRow(VisualElement row, int index)
        {
            if (_reputationList == null) return;
            var src = _reputationList.itemsSource as List<ReputationListItem>;
            if (src == null || index < 0 || index >= src.Count) return;
            var r = src[index];

            row.Q<Label>("row-faction").text = r.displayName;
            row.Q<Label>("row-value").text   = (r.value > 0 ? "+" : "") + r.value.ToString();

            // Bar width: 0..100% = -100..+100 → 50% = 0, 0% = -100, 100% = +100
            float pct = Mathf.Clamp01((r.value + 100f) / 200f) * 100f;
            var fill = row.Q<VisualElement>("row-fill");
            fill.style.width = new Length(pct, LengthUnit.Percent);
            fill.style.backgroundColor = r.color;
        }

        // ============================================================
        // Snapshot handlers (реюз MarketWindow логики)
        // ============================================================

        private void HandleContractSnapshot(ContractSnapshotDto snapshot)
        {
            // Re-use MarketWindow.HandleContractSnapshot: фильтр по fromLocationId для available,
            // active — все. CharacterWindow показывает контракты ВСЕХ локаций (это "обзор"),
            // а MarketWindow — только текущей зоны. Поэтому фильтр НЕ применяем.
            // (см. CONTRACTS_AS_MARKET_TAB_REFACTOR.md)
            ContractDto[] available = snapshot.available ?? Array.Empty<ContractDto>();
            var activeAll = snapshot.active ?? Array.Empty<ContractDto>();
            var activeList = new List<ContractDto>(activeAll.Length);
            for (int i = 0; i < activeAll.Length; i++)
            {
                if (activeAll[i].state == (byte)ContractState.Active)
                    activeList.Add(activeAll[i]);
            }
            ContractDto[] active = activeList.ToArray();

            // combined: сначала active (свои), потом available (новые)
            var combined = new List<ContractDto>(active.Length + available.Length);
            combined.AddRange(active);
            combined.AddRange(available);
            _contractsCache = combined.ToArray();

            // Re-render (с учётом фильтров, которые в ApplyContractFilters)
            ApplyContractFilters();

            if (_messageLabel != null && IsVisible() && _activeTab == "contracts")
            {
                _messageLabel.text = active.Length == 0 && available.Length == 0
                    ? "Нет активных или доступных контрактов"
                    : $"Активных: {active.Length} | Доступно: {available.Length}";
                _messageLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            }
        }

        private void HandleContractResult(ContractResultDto result)
        {
            if (_messageLabel == null) return;
            // ContractResultDto — struct (см. pitfall #14 в unity-mcp-orchestrator skill):
            // `result == null` НЕ компилируется. Проверяем code.
            if (!IsVisible()) return;

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

            // Обновить credits в header (если операция изменила баланс)
            if (_creditsLabel != null && result.newCredits > 0f)
            {
                _creditsLabel.text = $"Кредиты: {result.newCredits:F0} CR";
            }
        }

        // ============================================================
        // Filters: contracts
        // ============================================================

        private void ApplyContractFilters()
        {
            if (_contractsList == null) return;
            IEnumerable<ContractDto> src = _contractsCache ?? Array.Empty<ContractDto>();

            // Source filter: "Все" / "Контракты" / "Квесты"
            string source = _filterSource != null ? _filterSource.value : "Все";
            if (source == "Квесты")
            {
                // Квесты не реализованы — пустой список + подсказка
                _contractsList.itemsSource = Array.Empty<ContractDto>();
                _selectedContractItem = -1;
                _contractsList.selectedIndex = -1;
                _contractsList.Rebuild();
                if (_messageLabel != null && _activeTab == "contracts")
                {
                    _messageLabel.text = "Квесты ещё не реализованы (см. GDD-21)";
                    _messageLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.9f));
                }
                return;
            }
            // "Все" и "Контракты" — одинаково, всё что есть.

            // State filter
            string state = _filterState != null ? _filterState.value : "Все";
            if (state == "Активные")
                src = src.Where(c => c.state == (byte)ContractState.Active);
            else if (state == "Доступные")
                src = src.Where(c => c.state == (byte)ContractState.Pending);

            // Search filter
            string search = _filterSearch != null ? (_filterSearch.value ?? "").ToLowerInvariant() : "";
            if (!string.IsNullOrEmpty(search))
            {
                src = src.Where(c =>
                    (c.displayName ?? "").ToLowerInvariant().Contains(search) ||
                    (c.contractId  ?? "").ToLowerInvariant().Contains(search));
            }

            var result = src.ToArray();
            _contractsList.itemsSource = result;
            _selectedContractItem = -1;
            _contractsList.selectedIndex = -1;
            _contractsList.Rebuild();
        }

        // ============================================================
        // Filters: inventory
        // ============================================================

        private void ApplyInventoryFilters()
        {
            if (_inventoryList == null) return;
            IEnumerable<InventoryListItem> src = _inventoryCache;

            string source = _filterSource != null ? _filterSource.value : "Все типы";
            if (source != "Все типы")
            {
                src = src.Where(i => ItemTypeNames.GetDisplayName(i.type) == source);
            }
            string search = _filterSearch != null ? (_filterSearch.value ?? "").ToLowerInvariant() : "";
            if (!string.IsNullOrEmpty(search))
            {
                src = src.Where(i => (i.displayName ?? "").ToLowerInvariant().Contains(search));
            }

            // BUGFIX2026-06-05: то же что в RefreshInventoryCache — RefreshItems() + null-trick.
            var filteredList = src.ToList();
            if (!ReferenceEquals(_inventoryList.itemsSource, filteredList)) {
            _inventoryList.itemsSource = filteredList;
            }
            _inventoryList.RefreshItems();
            }

            // ============================================================
            // T-Q11: Quest log таб — Subscribe/Unsubscribe + Cache + Handlers + Accept action
            // ============================================================

            private bool _isQuestStateSubscribed = false;

            private void SubscribeQuestState()
            {
            if (_isQuestStateSubscribed) return;
            var qs = QuestClientState.Instance;
            if (qs == null) return;
            qs.OnSnapshotUpdated += HandleQuestSnapshotUpdated;
            qs.OnQuestResult += HandleQuestResult;
            qs.OnQuestDiscovered += HandleQuestDiscovered;
            _isQuestStateSubscribed = true;
            Debug.Log("[CharacterWindow] Subscribed to QuestClientState (snapshot/result/discovered)");
            }

            private void UnsubscribeQuestState()
            {
            if (!_isQuestStateSubscribed) return;
            var qs = QuestClientState.Instance;
            if (qs == null) { _isQuestStateSubscribed = false; return; }
            qs.OnSnapshotUpdated -= HandleQuestSnapshotUpdated;
            qs.OnQuestResult -= HandleQuestResult;
            qs.OnQuestDiscovered -= HandleQuestDiscovered;
            _isQuestStateSubscribed = false;
            }

            // ---- ListView setup helper ----
            private void SetupQuestListView(ListView list, ref List<QuestListItem> cacheRef)
            {
            if (list == null) return;
            list.makeItem = MakeQuestRow;
            list.bindItem = BindQuestRow;
            list.fixedItemHeight =28;
            list.itemsSource = cacheRef;
            }

            // ---- Row factory ----
            private VisualElement MakeQuestRow()
            {
            var row = new VisualElement();
            row.AddToClassList("quest-row");
            var badge = new Label { name = "row-state" };
            badge.AddToClassList("quest-row-state");
            row.Add(badge);
            var title = new Label { name = "row-title" };
            title.AddToClassList("quest-row-title");
            row.Add(title);
            var obj = new Label { name = "row-objectives" };
            obj.AddToClassList("quest-row-objectives");
            row.Add(obj);
            // T-Q12: per-row "Следить" / "Не следить" button (toggle).
            var trackBtn = new Button { name = "row-track-btn" };
            trackBtn.AddToClassList("quest-row-track-btn");
            trackBtn.text = "Следить";
            // UI Toolkit Button.clicked event не передаёт sender — используем RegisterCallback<ClickEvent>
            // для доступа к evt.target (row-track-btn) → walk до row → читаем userData (questId).
            trackBtn.RegisterCallback<ClickEvent>(OnQuestRowTrackClicked);
            row.Add(trackBtn);
            return row;
            }

            private void BindQuestRow(VisualElement row, int index)
            {
            if (row == null) return;
            var src = row.userData as List<QuestListItem>;
            // resolve source via parent list traversal (UI Toolkit quirk)
            if (src == null) src = ResolveQuestRowList(row);
            if (src == null || index <0 || index >= src.Count) return;
            var q = src[index];
            row.userData = q.questId; // T-Q12: store for track-btn handler.
            var badge = row.Q<Label>("row-state");
            var title = row.Q<Label>("row-title");
            var obj = row.Q<Label>("row-objectives");

            // badge — чистим все state-классы, добавляем текущий
            badge.RemoveFromClassList("quest-row-state-active");
            badge.RemoveFromClassList("quest-row-state-completed");
            badge.RemoveFromClassList("quest-row-state-failed");
            badge.RemoveFromClassList("quest-row-state-discovered");
            if (!string.IsNullOrEmpty(q.stateBadge)) badge.AddToClassList(q.stateBadge);
            badge.text = q.stateLabel ?? "";

            title.text = q.displayName ?? q.questId ?? "(unknown)";

            obj.text = (q.objectiveTotalCount >0)
            ? $"{q.objectiveCompletedCount}/{q.objectiveTotalCount}"
            : "";

            // T-Q12: track button toggle (текст обновляется при каждом bind).
            var trackBtn = row.Q<Button>("row-track-btn");
            if (trackBtn != null)
            {
            var trkInst = QuestTracker.Instance;
            bool isTracked = trkInst != null && trkInst.TrackedQuestId == q.questId;
            trackBtn.text = isTracked ? "Не следить" : "Следить";
            }
            }

            // T-Q12: handler — ClickEvent передаёт target = нажатую кнопку → walk до row → читаем userData (questId).
            private void OnQuestRowTrackClicked(ClickEvent evt)
            {
            var btn = evt.target as Button;
            if (btn == null) return;
            // Walk до row (parent).
            var row = btn.parent;
            if (row == null) return;
            var questId = row.userData as string;
            if (string.IsNullOrEmpty(questId)) return;
            var trk = QuestTracker.Instance;
            if (trk == null)
            {
            SetMessage("QuestTracker недоступен", true);
            return;
            }
            if (trk.TrackedQuestId == questId) trk.Untrack();
            else trk.Track(questId);
            // Обновить текст кнопок в обоих списках (active/discovered).
            RefreshQuestsCache();
            }

            private static List<QuestListItem> ResolveQuestRowList(VisualElement row)
            {
            // Walk up parent chain до ListView, читаем itemsSource.
            var cur = row?.parent;
            while (cur != null)
            {
            if (cur is ListView lv && lv.itemsSource is List<QuestListItem> q) return q;
            cur = cur.parent;
            }
            return null;
            }

            // ---- Refresh cache (single source of truth: QuestClientState.CurrentSnapshot) ----
            private void RefreshQuestsCache()
            {
            _questsActiveCache.Clear();
            _questsCompletedCache.Clear();
            _questsFailedCache.Clear();
            _questsDiscoveredCache.Clear();

            var qs = QuestClientState.Instance;
            if (qs == null || !qs.CurrentSnapshot.HasValue)
            {
            ApplyQuestListRefresh();
            return;
            }

            var snap = qs.CurrentSnapshot.Value;
            var quests = snap.quests;
            if (quests == null)
            {
            ApplyQuestListRefresh();
            return;
            }

            foreach (var q in quests)
            {
            var item = BuildQuestListItem(q);
            // Discovered=0, Offered=1, Active=2, Completed=3, Failed=4, TurnedIn=5.
            switch (q.state)
            {
            case (byte)QuestState.Active:
            _questsActiveCache.Add(item);
            break;
            case (byte)QuestState.Completed:
            case (byte)QuestState.TurnedIn:
            _questsCompletedCache.Add(item);
            break;
            case (byte)QuestState.Failed:
            _questsFailedCache.Add(item);
            break;
            case (byte)QuestState.Discovered:
            case (byte)QuestState.Offered:
            _questsDiscoveredCache.Add(item);
            break;
            }
            }

            ApplyQuestListRefresh();
            UpdateQuestMessage();
            }

            private static QuestListItem BuildQuestListItem(QuestProgressDto q)
            {
            var item = new QuestListItem
            {
            questId = q.questId ?? "",
            displayName = !string.IsNullOrEmpty(q.displayName) ? q.displayName : (q.questId ?? "(unknown)"),
            state = q.state,
            stateLabel = GetQuestStateLabel(q.state),
            stateBadge = GetQuestStateBadgeClass(q.state),
            objectiveCompletedCount =0,
            objectiveTotalCount =0,
            };

            var objs = q.objectives;
            if (objs != null)
            {
            item.objectiveTotalCount = objs.Length;
            foreach (var o in objs) if (o.completed) item.objectiveCompletedCount++;
            }
            item.objectivesSummary = item.objectiveTotalCount >0
            ? $"{item.objectiveCompletedCount}/{item.objectiveTotalCount}"
            : "";
            return item;
            }

            private static string GetQuestStateLabel(byte state)
            {
            switch (state)
            {
            case (byte)QuestState.Discovered: return "ОБНАРУЖЕН";
            case (byte)QuestState.Offered: return "ПРЕДЛОЖЕН";
            case (byte)QuestState.Active: return "АКТИВЕН";
            case (byte)QuestState.Completed: return "ВЫПОЛНЕН";
            case (byte)QuestState.TurnedIn: return "СДАН";
            case (byte)QuestState.Failed: return "ПРОВАЛЕН";
            default: return state.ToString();
            }
            }

            private static string GetQuestStateBadgeClass(byte state)
            {
            switch (state)
            {
            case (byte)QuestState.Active: return "quest-row-state-active";
            case (byte)QuestState.Completed:
            case (byte)QuestState.TurnedIn: return "quest-row-state-completed";
            case (byte)QuestState.Failed: return "quest-row-state-failed";
            case (byte)QuestState.Discovered:
            case (byte)QuestState.Offered: return "quest-row-state-discovered";
            default: return "quest-row-state";
            }
            }

            private void ApplyQuestListRefresh()
            {
            // Rebuild only if reference changed (RefreshItems — UI Toolkit pitfall R3-005).
            if (_questsActiveList != null)
            {
            if (!ReferenceEquals(_questsActiveList.itemsSource, _questsActiveCache))
            _questsActiveList.itemsSource = _questsActiveCache;
            _questsActiveList.RefreshItems();
            }
            if (_questsCompletedList != null)
            {
            if (!ReferenceEquals(_questsCompletedList.itemsSource, _questsCompletedCache))
            _questsCompletedList.itemsSource = _questsCompletedCache;
            _questsCompletedList.RefreshItems();
            }
            if (_questsFailedList != null)
            {
            if (!ReferenceEquals(_questsFailedList.itemsSource, _questsFailedCache))
            _questsFailedList.itemsSource = _questsFailedCache;
            _questsFailedList.RefreshItems();
            }
            if (_questsDiscoveredList != null)
            {
            if (!ReferenceEquals(_questsDiscoveredList.itemsSource, _questsDiscoveredCache))
            _questsDiscoveredList.itemsSource = _questsDiscoveredCache;
            _questsDiscoveredList.RefreshItems();
            }
            }

            private void UpdateQuestMessage()
            {
            if (_messageLabel == null || _activeTab != "quests") return;
            int a = _questsActiveCache.Count;
            int c = _questsCompletedCache.Count;
            int f = _questsFailedCache.Count;
            int d = _questsDiscoveredCache.Count;
            if (a + c + f + d ==0)
            {
            _messageLabel.text = "Нет квестов в журнале. Серверная модель в разработке (T-Q15+)";
            _messageLabel.style.color = new StyleColor(new Color(0.7f,0.7f,0.9f));
            }
            else
            {
            _messageLabel.text = $"Активных: {a} | Завершённых: {c} | Провалено: {f} | Найдено: {d}";
            _messageLabel.style.color = new StyleColor(new Color(0.9f,0.9f,0.9f));
            }
            }

            // ---- Handlers (R3-005 cross-tab: unconditional refresh, gated UI rebuild) ----
            private void HandleQuestSnapshotUpdated(QuestSnapshotDto snap)
            {
            // cache — ALWAYS refresh (projection of server state).
            RefreshQuestsCache();
            // visible UI — gated by active tab.
            if (_activeTab == "quests")
            {
            // cache уже обновлён внутри RefreshQuestsCache.
            }
            }

            private void HandleQuestResult(QuestResultDto result)
            {
            if (_messageLabel == null || !IsVisible()) return;
            bool isOk = result.code == (byte)QuestResultCode.Ok;
            if (isOk)
            {
            _messageLabel.text = result.message ?? "OK";
            _messageLabel.style.color = new StyleColor(new Color(0.4f,0.95f,0.4f));
            }
            else
            {
            _messageLabel.text = result.message ?? $"Ошибка ({result.code})";
            _messageLabel.style.color = new StyleColor(new Color(0.95f,0.4f,0.4f));
            }
            // После Accept-кнопки сервер пришлёт snapshot — RefreshQuestsCache уже вызван через OnSnapshotUpdated.
            }

            private void HandleQuestDiscovered(string questId, string displayName)
            {
            // EventDriven push. Refresh cache + покажем в message (не gated, cross-tab).
            RefreshQuestsCache();
            if (_messageLabel != null && IsVisible())
            {
            _messageLabel.text = $"Новый квест: {displayName}";
            _messageLabel.style.color = new StyleColor(new Color(0.9f,0.85f,0.5f));
            }
            }

            // ---- Accept action ----
            private void OnAcceptQuestClicked()
            {
            if (_questsDiscoveredList == null)
            {
            SetMessage("Список найденных квестов недоступен", true);
            return;
            }
            var src = _questsDiscoveredList.itemsSource as List<QuestListItem>;
            if (src == null || _selectedDiscoveredQuest <0 || _selectedDiscoveredQuest >= src.Count)
            {
            SetMessage("Выберите квест в секции 'Найденные' для принятия");
            return;
            }
            var q = src[_selectedDiscoveredQuest];
            var qs = QuestClientState.Instance;
            if (qs == null)
            {
            SetMessage("QuestClientState недоступен", true);
            return;
            }
            // T-Q15 stub: сервер пока не делает TryAccept, но RPC дойдёт, rate-limit OK.
            qs.RequestAcceptQuest(q.questId, "");
            SetMessage($"Запрос на принятие '{q.displayName}' отправлен...");
            }

            // ============================================================
            // Actions: contracts (реюз MarketWindow логики)
            // ============================================================

        private void OnAcceptContractClicked()
        {
            if (_contractsList == null) return;
            var src = _contractsList.itemsSource as ContractDto[];
            if (src == null || _selectedContractItem < 0 || _selectedContractItem >= src.Length)
            {
                SetMessage("Выберите контракт для принятия");
                return;
            }
            var c = src[_selectedContractItem];
            if (c.state != (byte)ContractState.Pending)
            {
                SetMessage("Этот контракт уже не доступен для принятия");
                return;
            }
            if (_contractState == null)
            {
                SetMessage("ContractClientState недоступен", true);
                return;
            }

            // Optimistic update (как в MarketWindow): мгновенно state=Active в кэше + pulse.
            var cLocal = c;
            cLocal.state = (byte)ContractState.Active;
            _contractsCache[_selectedContractItem] = cLocal;
            ApplyContractFilters();
            StartCoroutine(JustTakenPulse(_selectedContractItem));

            _contractState.RequestAccept(c.contractId);
            SetMessage("Запрос отправлен...");
        }

        private void OnCompleteContractClicked()
        {
            if (_contractsList == null) return;
            var src = _contractsList.itemsSource as ContractDto[];
            if (src == null || _selectedContractItem < 0 || _selectedContractItem >= src.Length)
            {
                SetMessage("Выберите активный контракт для сдачи");
                return;
            }
            var c = src[_selectedContractItem];
            if (c.state != (byte)ContractState.Active)
            {
                SetMessage("Этот контракт не активен");
                return;
            }
            if (_contractState == null)
            {
                SetMessage("ContractClientState недоступен", true);
                return;
            }
            _contractState.RequestComplete(c.contractId);
            SetMessage("Запрос отправлен...");
        }

        private void OnFailContractClicked()
        {
            if (_contractsList == null) return;
            var src = _contractsList.itemsSource as ContractDto[];
            if (src == null || _selectedContractItem < 0 || _selectedContractItem >= src.Length)
            {
                SetMessage("Выберите активный контракт");
                return;
            }
            var c = src[_selectedContractItem];
            if (c.state != (byte)ContractState.Active)
            {
                SetMessage("Этот контракт не активен");
                return;
            }
            if (_contractState == null)
            {
                SetMessage("ContractClientState недоступен", true);
                return;
            }
            _contractState.RequestFail(c.contractId);
            SetMessage("Запрос отправлен...");
        }

        private System.Collections.IEnumerator JustTakenPulse(int rowIndex)
        {
            if (_contractsList == null) yield break;
            yield return null;  // ждём 1 кадр — Rebuild() асинхронен
            var row = _contractsList.ElementAt(rowIndex) as VisualElement;
            if (row == null) yield break;
            row.AddToClassList("contract-row-just-taken");
            yield return new WaitForSeconds(1.6f);
            if (row != null) row.RemoveFromClassList("contract-row-just-taken");
        }

        private void OnCloseClicked() => SetVisible(false);

        // ============================================================
        // Visibility (4 FIX'а из MarketWindow)
        // ============================================================

        public void Show()
        {
            // Defensive guard: если _doc == null (Awake не успел), инициализируем лениво.
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                Debug.LogError("[CharacterWindow] Show(): нет UIDocument на GameObject");
                return;
            }
            // FIX: idempotent — если по какой-то причине дерево потеряно, пересоберём.
            if (!_built || _mainContainer == null || !IsLayoutValid())
            {
                Debug.LogWarning("[CharacterWindow] Show(): UI not built or layout invalid, rebuilding");
                EnsureBuilt();
            }
            // FIX: Включаем приём pointer events на root, чтобы клики по окну работали.
            if (_root != null) _root.pickingMode = PickingMode.Position;
            SetVisible(true);

            // FIX: race с RPC — если данные ещё не пришли, показываем placeholder.
            if (_contractState == null || !_contractState.CurrentSnapshot.HasValue)
            {
                if (_messageLabel != null && _activeTab == "contracts")
                {
                    _messageLabel.text = "Загрузка контрактов...";
                    _messageLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.9f));
                }
            }

            _doc?.rootVisualElement?.MarkDirtyRepaint();
            if (_doc?.rootVisualElement != null)
            {
                _doc.rootVisualElement.schedule.Execute(() => _doc.rootVisualElement.MarkDirtyRepaint()).StartingIn(50);
            }
        }

        public void Hide()
        {
            // FIX: Выключаем pointer events — иначе невидимый _root перехватывает клики.
            if (_root != null) _root.pickingMode = PickingMode.Ignore;
            SetVisible(false);
        }

        public void Toggle()
        {
            bool currentVisible = _mainContainer != null && _mainContainer.style.display == DisplayStyle.Flex;
            bool newVisible = !currentVisible;
            if (newVisible) Show(); else Hide();
        }

        public bool IsVisible()
        {
            return _mainContainer != null && _mainContainer.style.display == DisplayStyle.Flex;
        }

        private void SetVisible(bool v)
        {
            if (_mainContainer == null) _mainContainer = _root?.Q<VisualElement>("main-container");
            if (_mainContainer != null)
            {
                _mainContainer.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
                if (v) ApplyInlineFallbackStyles(_mainContainer);
            }
            // FIX: Cursor — flight-режим держит курсор залоченным. При открытом UI отпускаем.
            if (v)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        private static void ApplyInlineFallbackStyles(VisualElement main)
        {
            // FIX: на 1-м кадре resolvedStyle=initial (USS не успел примениться) — задаём
            // только позиционирование и размеры inline. Всё остальное (фон, рамка, шрифт,
            // padding, цвет) теперь в CharacterWindow.uss с !important, который перебивает
            // UnityDefaultRuntimeTheme. Дублировать эти свойства inline больше не нужно.
            main.style.position = Position.Absolute;
            main.style.top    = new Length(4,  LengthUnit.Percent);
            main.style.left   = new Length(50, LengthUnit.Percent);
            main.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), 0));
            main.style.width      = 720;
            main.style.maxWidth   = new Length(90, LengthUnit.Percent);
            main.style.maxHeight  = new Length(92, LengthUnit.Percent);
        }

        // ============================================================
        // Utils
        // ============================================================

        private void SetMessage(string msg, bool isError = false)
        {
            if (_messageLabel == null) return;
            _messageLabel.text = msg;
            _messageLabel.style.color = isError
                ? new StyleColor(new Color(0.95f, 0.4f, 0.4f))
                : new StyleColor(new Color(0.9f, 0.9f, 0.9f));
        }

        private static int FindSelectedItemIndex<T>(ListView list, IEnumerable<object> selectedItems)
        {
            // Копия из MarketWindow.cs (UI Toolkit 6.x selectionChanged даёт IEnumerable<object>).
            if (selectedItems == null) return -1;
            object first = null;
            foreach (var o in selectedItems) { first = o; break; }
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

        private void FindLocalPlayer()
        {
            // Копия логики из MarketInteractor.FindLocalPlayer — тот же ghost-фильтр (сцен-placed PlayerSpawner).
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].IsOwner) continue;
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                _localPlayer = players[i];
                return;
            }
        }

        // ============================================================
        // Contract helpers (статические — копия из MarketWindow.cs)
        // ============================================================

        private static string GetContractTypeDisplayName(ContractType type)
        {
            switch (type)
            {
                case ContractType.Standard: return "[Стандарт]";
                case ContractType.Urgent:   return "[Срочный]";
                case ContractType.Receipt:  return "[Расписка]";
                default: return type.ToString();
            }
        }

        private static string GetContractTypeClass(ContractType type)
        {
            switch (type)
            {
                case ContractType.Standard: return "type-standard";
                case ContractType.Urgent:   return "type-urgent";
                case ContractType.Receipt:  return "type-receipt";
                default: return "type-standard";
            }
        }

        private static string GetContractTimeRemainingString(ContractDto c)
        {
            if (c.timeLimit <= 0f) return "∞";
            int minutes = Mathf.FloorToInt(c.timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(c.timeRemaining % 60f);
            return $"{minutes}:{seconds:D2}";
        }

        private static string GetContractTimerClass(ContractDto c)
        {
            if (c.timeLimit <= 0f) return "timer-ok";
            float pct = c.timeRemaining / c.timeLimit;
            if (pct < 0.1f) return "timer-danger";
            if (pct < 0.3f) return "timer-warn";
            return "timer-ok";
        }
    }
}
