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
using ProjectC.Factions;
using ProjectC.Network;
using ProjectC.Player;
using ProjectC.Quests;
using ProjectC.Quests.Client;
using ProjectC.Quests.Dto;
using ProjectC.Quests.UI;
using ProjectC.Reputation;
using ProjectC.Skills;
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

        [Header("Debug")]
        [Tooltip("Verbose logging для CharacterWindow. Выключи для тишины в консоли.")]
        [SerializeField] private bool _debugLogging = false;

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
        // T-P16: 4 sub-tab buttons (now unused — removed in UXML refactor)

        // --- ListViews ---
        private ListView _reputationList;
        private ListView _npcAttitudeList; // T-Q13
        private ListView _contractsList;
        private ListView _inventoryList;  // Сессия 2 ROLLBACK: обратно на ListView
        private ListView _questsActiveList;
        private ListView _questsCompletedList;
        private ListView _questsFailedList;
        private ListView _questsDiscoveredList;

        private ProgressBar _statStrBar;
        private Label _statStrValue;
        private ProgressBar _statDexBar;
        private Label _statDexValue;
        private ProgressBar _statIntBar;
        private Label _statIntValue;
        private VisualElement _statStrRow;
        private VisualElement _statDexRow;
        private VisualElement _statIntRow;

        // --- Action buttons ---
        private Button _acceptBtn;
        private Button _completeBtn;
        private Button _failBtn;
        // T-P19: Quests buttons
        private Button _acceptQuestBtn;
        private Button _rejectQuestBtn;
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
        /// <summary>T-P19: публичный геттер для InventoryTab и других табов.</summary>
        public string GetActiveTab() => _activeTab;

        // T-P19: InventoryTab — вынесенная вкладка инвентаря
                private InventoryTab _inventoryTab;
                // T-KEY-08: MyShipsTab — вынесенная вкладка "Мои корабли"
                private MyShipsTab _myShipsTab;

        private int _selectedContractItem = -1;
        private int _selectedInventoryItem = -1;

        // T-P19: ContractsTab — вынесенная вкладка контрактов
        private ContractsTab _contractsTab;

        private ContractDto[] _contractsCache = Array.Empty<ContractDto>();
        [Obsolete("Переехало в InventoryTab._inventoryCache — удалить при следующем рефакторе")]
        private List<InventoryListItem> _inventoryCache = new List<InventoryListItem>();
        private List<ReputationListItem> _reputationCache = new List<ReputationListItem>();
        private List<NpcAttitudeListItem> _npcAttitudeCache = new List<NpcAttitudeListItem>(); // T-Q13
        private List<QuestListItem> _questsActiveCache = new List<QuestListItem>();
        private List<QuestListItem> _questsCompletedCache = new List<QuestListItem>();
        private List<QuestListItem> _questsFailedCache = new List<QuestListItem>();
        private List<QuestListItem> _questsDiscoveredCache = new List<QuestListItem>();
        private int _selectedDiscoveredQuest = -1;

        // T-P17: Clothing/Modules ListView refs + caches + per-row fields
        private VisualElement _clothingContainer;   // SESSION 2: container instead of ListView
        private VisualElement _modulesContainer;    // SESSION 2: container instead of ListView
        private List<EquipRow> _clothingCache = new List<EquipRow>();
        private List<EquipRow> _modulesCache = new List<EquipRow>();
        private bool _isEquipmentSubscribed = false;
        private Label _equipRowItem;
        private Label _equipRowBonuses;
        private Button _equipRowBtn;

        // SESSION 2: Inventory detail labels (правая панель с описанием предмета)
        private Label _invDetailName;
        private Label _invDetailType;
        private Label _invDetailWeight;
        private Label _invDetailStat;
        private Label _invDetailDesc;

        /// <summary>
        /// T-P17: Row DTO для clothing/modules ListView. IsModule=true для modules-list (icon/type вариация).
        /// </summary>
        private struct EquipRow
        {
            public ProjectC.Equipment.EquipSlot Slot;
            public int ItemId;          // inventory id; 0 = пусто
            public string ItemName;
            public string SlotName;     // "Head" / "Chest" / "Module1" — для удобства
            public string Bonuses;      // "+3 STR" / "+1 STR, +1 INT" / etc
            public string TierText;     // "T2"
            public bool IsModule;
        }

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

        // T-Q13: NpcAttitude row projection для под-секции "Отношения к NPC".
        private struct NpcAttitudeListItem
        {
        public string npcId;
        public string displayName;
        public int value; // -100..+200 (NpcAttitude.MinValue..MaxValue)
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
        // T-Q21: per-objective detail list (для рендера в row под quest'ом). Lazy-initialized.
        public System.Collections.Generic.List<ObjectiveRowItem> objectives;
        }

        /// <summary>T-Q21: per-objective row data for the nested objectives list.</summary>
        public class ObjectiveRowItem
        {
            public string description;
            public bool completed;
            public int currentValue;
            public int requiredQuantity;
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
        // T-P19: Inventory переехал в InventoryTab.
        if (_inventoryTab != null) _inventoryTab.Unsubscribe();
        // T-P19: Contracts переехал в ContractsTab.
        if (_contractsTab != null) _contractsTab.Unsubscribe();
        // T-Q11: Unsubscribe QuestClientState (3 события).
        UnsubscribeQuestState();
        // T-Q13: Unsubscribe Reputation + NpcAttitude singletons.
        UnsubscribeReputation();
        UnsubscribeNpcAttitude();
        // T-P14: Unsubscribe SkillsClientState (2 события).
        UnsubscribeSkills();
        // T-P16: Unsubscribe StatsClientState (1 событие).
        UnsubscribeStats();
        // T-P17: Unsubscribe EquipmentClientState (2 события).
        UnsubscribeEquipment();
        }

        private bool _isInventorySubscribed = false;
        // T-Q13: subscribe flags для ReputationClientState и NpcAttitudeClientState.
        private bool _isReputationSubscribed = false;
        private bool _isNpcAttitudeSubscribed = false;

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
        if (_inventoryList != null) _inventoryList.selectionChanged -= OnInventorySelectionChanged;
        _isInventorySubscribed = false;
        }

        // T-Q13: Reputation subscribe/unsubscribe (lazy race-condition fix по аналогии с InventoryClientState).
        private void SubscribeReputation()
        {
        if (_isReputationSubscribed) return;
        var repState = ReputationClientState.Instance;
        if (repState == null) return;
        repState.OnReputationUpdated += HandleReputationSnapshot;
        _isReputationSubscribed = true;
        }
        private void UnsubscribeReputation()
        {
        if (!_isReputationSubscribed) return;
        var repState = ReputationClientState.Instance;
        if (repState == null) { _isReputationSubscribed = false; return; }
        repState.OnReputationUpdated -= HandleReputationSnapshot;
        _isReputationSubscribed = false;
        }

        // T-Q13: NpcAttitude subscribe/unsubscribe.
        private void SubscribeNpcAttitude()
        {
        if (_isNpcAttitudeSubscribed) return;
        var attState = NpcAttitudeClientState.Instance;
        if (attState == null) return;
        attState.OnNpcAttitudeUpdated += HandleNpcAttitudeSnapshot;
        _isNpcAttitudeSubscribed = true;
        }
        private void UnsubscribeNpcAttitude()
        {
        if (!_isNpcAttitudeSubscribed) return;
        var attState = NpcAttitudeClientState.Instance;
        if (attState == null) { _isNpcAttitudeSubscribed = false; return; }
        attState.OnNpcAttitudeUpdated -= HandleNpcAttitudeSnapshot;
        _isNpcAttitudeSubscribed = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // T-P19: lazy-subscribe для InventoryClientState переехал в InventoryTab.TryLazySubscribe.
            if (_built && _inventoryTab != null)
            {
                _inventoryTab.TryLazySubscribe();
            }

            // T-Q13: lazy-subscribe Reputation + NpcAttitude singletons.
            if (_built && !_isReputationSubscribed)
            {
                var repState = ReputationClientState.Instance;
                if (repState != null)
                {
                    SubscribeReputation();
                    Debug.Log("[CharacterWindow] Lazy-subscribed to ReputationClientState.OnReputationUpdated");
                }
            }
            if (_built && !_isNpcAttitudeSubscribed)
            {
                var attState = NpcAttitudeClientState.Instance;
                if (attState != null)
                {
                    SubscribeNpcAttitude();
                    Debug.Log("[CharacterWindow] Lazy-subscribed to NpcAttitudeClientState.OnNpcAttitudeUpdated");
                }
            }

            // SESSION 1 fix: lazy-subscribe для Stats/Skills/Equipment ClientStates.
            // В EnsureBuilt Instance мог быть null если client state создан позже (race condition).
            if (_built && !_isStatsSubscribed)
            {
                var statsState = ProjectC.Stats.StatsClientState.Instance;
                if (statsState != null)
                {
                    SubscribeStats();
                    Debug.Log("[CharacterWindow] Lazy-subscribed to StatsClientState.OnStatsUpdated");
                    // Триггерим немедленный refresh — если CurrentStats уже есть, отобразим
                    if (statsState.CurrentStats.HasValue) RefreshStatsDisplay(statsState.CurrentStats.Value);
                }
            }
            if (_built && !_isEquipmentSubscribed)
            {
                var eqState = ProjectC.Equipment.EquipmentClientState.Instance;
                if (eqState != null)
                {
                    SubscribeEquipment();
                    Debug.Log("[CharacterWindow] Lazy-subscribed to EquipmentClientState");
                }
            }
            if (_built && !_isSkillsSubscribed)
            {
                var skState = ProjectC.Skills.SkillsClientState.Instance;
                if (skState != null)
                {
                    SubscribeSkills();
                    Debug.Log("[CharacterWindow] Lazy-subscribed to SkillsClientState");
                }
            }

            // BUGFIX T-P19: Esc проверяем ДО guard'а NetworkManager, чтобы закрытие
            // работало независимо от состояния сети (был баг: Esc не закрывал окно).
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsVisible())
            {
                Hide();
                return;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            if (!nm.IsClient && !nm.IsServer) return;
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

            // T-P16: stat-row-progress refs (3 stat bars + 3 value labels)
            _statStrBar = _root.Q<ProgressBar>("stat-str-bar");
            _statStrValue = _root.Q<Label>("stat-str-value");
            _statDexBar = _root.Q<ProgressBar>("stat-dex-bar");
            _statDexValue = _root.Q<Label>("stat-dex-value");
            _statIntBar = _root.Q<ProgressBar>("stat-int-bar");
            _statIntValue = _root.Q<Label>("stat-int-value");
            _statStrRow = _statStrBar != null ? _statStrBar.parent as VisualElement : null;
            _statDexRow = _statDexBar != null ? _statDexBar.parent as VisualElement : null;
            _statIntRow = _statIntBar != null ? _statIntBar.parent as VisualElement : null;

            // T-P17: clothing/modules containers (SESSION 2: ручные rows вместо ListView)
            _clothingContainer = _root.Q<VisualElement>("clothing-container");
            _modulesContainer = _root.Q<VisualElement>("modules-container");

            _tabCharacter = _root.Q<Button>("tab-character");
            _tabShip = _root.Q<Button>("tab-ship");
            _tabReputation = _root.Q<Button>("tab-reputation");
            _tabContracts = _root.Q<Button>("tab-contracts");
            _tabInventory = _root.Q<Button>("tab-inventory");
            _tabQuests = _root.Q<Button>("tab-quests");

            _reputationList = _root.Q<ListView>("reputation-list");
            _npcAttitudeList = _root.Q<ListView>("npc-attitude-list"); // T-Q13
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
            _rejectQuestBtn = _root.Q<Button>("reject-quest-btn");
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
            if (_acceptQuestBtn != null) _acceptQuestBtn.clicked += OnAcceptQuestClicked;
            if (_rejectQuestBtn != null) _rejectQuestBtn.clicked += OnRejectQuestClicked;
            // T-P19: Accept/Complete/Fail для контрактов переехали в ContractsTab.BuildUI

            // T-P19: ContractsTab вынесен — BuildUI делает ListView setup + подписку + кнопки.
            _contractsTab = new ContractsTab();
            _contractsTab.BuildUI(this, _root, _filterSource, _filterState, _filterSearch,
                _creditsLabel, _messageLabel);

            // T-P19: InventoryTab вынесен — BuildUI делает ListView + detail labels + подписку.
                        _inventoryTab = new InventoryTab();
                        _inventoryTab.BuildUI(this, _root, _filterSource, _filterState, _filterSearch,
                            _creditsLabel, _messageLabel, _statCredits);

                        // T-KEY-08: MyShipsTab вынесен — вкладка "Мои корабли"
                        _myShipsTab = new MyShipsTab();
                        _myShipsTab.BuildUI(this, _root);

            // T-Q13: NpcAttitude + Reputation listviews (остаются в CharacterWindow)
            if (_reputationList != null)
            {
                _reputationList.makeItem = MakeReputationRow;
                _reputationList.bindItem = BindReputationRow;
                _reputationList.fixedItemHeight = 32;
            }

            // ---- ListView: NpcAttitude (T-Q13, под-секция "Отношения к NPC") ----
            if (_npcAttitudeList != null)
            {
            _npcAttitudeList.makeItem = MakeNpcAttitudeRow;
            _npcAttitudeList.bindItem = BindNpcAttitudeRow;
            _npcAttitudeList.fixedItemHeight =28;
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
            _questsDiscoveredList.unbindItem = UnbindQuestRow;
            _questsDiscoveredList.fixedItemHeight =64;  // T-Q21 fix: см. SetupQuestListView.
            _questsDiscoveredList.selectionType = SelectionType.Single;
            _questsDiscoveredList.selectedIndex = -1;
            _questsDiscoveredList.selectionChanged += selectedItems =>
            {
            _selectedDiscoveredQuest = FindSelectedItemIndex<QuestListItem>(_questsDiscoveredList, selectedItems);
            if (_questsDiscoveredList != null) _questsDiscoveredList.Rebuild();
            };
            }

            // T-P17: clothing/modules containers setup + pre-populate
            // SESSION 2: InitEquipmentContainers удалён — containers используются напрямую из UXML
            // (ScrollView НЕ оборачиваем — каждый container уже имеет flex-grow: 1; min-height: 0).
            // T-P17: subscribe to EquipmentClientState (M2) for live updates
            SubscribeEquipment();

            // SESSION 2: pre-populate caches + rebuild containers
            InitEquipmentCache(_clothingCache, 0, 10, false);
            InitEquipmentCache(_modulesCache, 10, 3, true);
            RebuildEquipmentListView();
            // Skills: Resources.LoadAll -> LOCKED state.
            InitSkillsCache();
            RebuildSkillsListView();

            // ---- Filter change callback (общий для Contracts и Inventory) ----
            if (_filterSearch != null)
            {
                _filterSearch.RegisterValueChangedCallback(evt =>
                {
                    if (_activeTab == "contracts" && _contractsTab != null) _contractsTab.ApplyFilters();
                    else if (_activeTab == "inventory" && _inventoryTab != null) _inventoryTab.ApplyFilters();
                });
            }
            // T-P19: dropdown source (тип предмета) — триггерит ApplyFilters для инвентаря
            if (_filterSource != null)
            {
                _filterSource.RegisterValueChangedCallback(evt =>
                {
                    if (_activeTab == "inventory" && _inventoryTab != null) _inventoryTab.ApplyFilters();
                });
            }

            // T-P19: ContractClientState subscription переехал в ContractsTab.BuildUI.
            // InventoryClientState subscription — в InventoryTab.BuildUI.

            // T-Q13: subscribe to ReputationClientState + NpcAttitudeClientState (idempotent).
            // Синглтоны создаются scene-placed в BootstrapScene (рядом с [QuestClientState]).
            // Если null — Update() lazy-подпишется.
            SubscribeReputation();
            SubscribeNpcAttitude();
            if (ReputationClientState.Instance == null)
            {
            Debug.LogWarning("[CharacterWindow] ReputationClientState.Instance == null на момент EnsureBuilt — Update() lazy-подпишется");
            }
            if (NpcAttitudeClientState.Instance == null)
            {
            Debug.LogWarning("[CharacterWindow] NpcAttitudeClientState.Instance == null на момент EnsureBuilt — Update() lazy-подпишется");
            }

            // ---- T-Q11: Subscribe to QuestClientState (Quest log + Discovered events) ----
            // QuestClientState singleton создаётся через RuntimeInitializeOnLoadMethod в его AutoSpawn,
            // плюс scene-placed в BootstrapScene — всегда есть к моменту EnsureBuilt.
            SubscribeQuestState();
            // T-Q21 fix: подписаться на QuestTracker.OnTrackChanged чтобы кнопки "Следить"/"Не следить" в списке
            // обновлялись когда игрок жмёт "Скрыть" прямо в HUD (или наоборот).
            SubscribeQuestTracker();

            // T-P14: Subscribe to SkillsClientState (M3) — refresh skill rows on snapshot/result.
            // T-P15 создаст UXML refs `skills-combat-list`/`skills-social-list`; T-P14 логика готова.
            SubscribeSkills();
            // T-P16: Subscribe to StatsClientState (M1) — refresh stat-row-progress on snapshot.
            SubscribeStats();
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
        // T-P19: Contracts/Inventory/Quests display переехали в табы
        if (_contractsSection != null) _contractsSection.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
        if (_inventorySection != null) _inventorySection.style.display = isInventory ? DisplayStyle.Flex : DisplayStyle.None;
        if (_questsSection != null) _questsSection.style.display = isQuests ? DisplayStyle.Flex : DisplayStyle.None;

        // BUGFIX: display: none → flex — MarkDirtyRepaint через табы
        if (isContracts && _contractsTab != null) {
            _contractsTab.OnTabShown();
        }
        if (isInventory && _inventoryTab != null) {
            _inventoryTab.OnTabShown();
        }
        if (isQuests)
        {
        if (_questsActiveList != null) _questsActiveList.MarkDirtyRepaint();
        if (_questsCompletedList != null) _questsCompletedList.MarkDirtyRepaint();
        if (_questsFailedList != null) _questsFailedList.MarkDirtyRepaint();
        if (_questsDiscoveredList != null) _questsDiscoveredList.MarkDirtyRepaint();
        }
        SetActiveTabVisual(_tabCharacter, isCharacter);
        SetActiveTabVisual(_tabShip, isShip);
        SetActiveTabVisual(_tabReputation, isReputation);
        SetActiveTabVisual(_tabContracts, isContracts);
        SetActiveTabVisual(_tabInventory, isInventory);
        SetActiveTabVisual(_tabQuests, isQuests);

        // ---- Filters visibility + options ----
        // T-P19: фильтры показаны для контрактов И для инвентаря
        if (_filtersRow != null)
        {
            bool showFilters = isContracts || isInventory;
            _filtersRow.style.display = showFilters ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (isContracts)
        {
        // T-P19: ConfigureContractFilters, ApplyContractFilters — в ContractsTab.OnTabShown
        }
        // T-P19: Inventory filters — в InventoryTab.OnTabShown

        // ---- Action buttons ----
        // T-P19: accept/complete/fail кнопки — переехали в ContractsTab.BuildUI
        if (_acceptQuestBtn != null) _acceptQuestBtn.style.display = isQuests ? DisplayStyle.Flex : DisplayStyle.None;
        if (_rejectQuestBtn != null) _rejectQuestBtn.style.display = isQuests ? DisplayStyle.Flex : DisplayStyle.None;
        if (_closeBtn != null) _closeBtn.style.display = DisplayStyle.Flex; // всегда

        // ---- Refresh data for the active tab ----
        if (isCharacter) RefreshCharacterStats();
        if (isShip) {
            if (_myShipsTab != null) _myShipsTab.OnTabShown();
        }
        if (isReputation) { RefreshReputationCache(); RefreshNpcAttitudeCache(); }
        // T-P19: Contracts/Inventory refresh — в OnTabShown табов
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
                    // T-KEY-08: делегировано в MyShipsTab.OnTabShown. Метод оставлен как no-op
                    // для backward compat с возможными вызывающими (если есть).
                }

        // ============================================================
        // T-Q13: Section refresh: reputation (read from ReputationClientState)
        // ============================================================

        /// <summary>
        /// 5 фракций GDD-23 как fallback (если snapshot ещё не пришёл). Placeholder — UI не пустой.
        /// </summary>
        private static readonly (string id, string name, Color color)[] FactionFallback = new[]
        {
            ("merchants",  "Гильдия Торговцев",   new Color(0.60f, 0.80f, 0.40f)),
            ("engineers",  "Мануфактура «Аврора»", new Color(0.40f, 0.60f, 0.90f)),
            ("military",   "Военный Анклав",      new Color(0.80f, 0.40f, 0.40f)),
            ("resistance", "Сопротивление",       new Color(0.70f, 0.50f, 0.90f)),
            ("smugglers",  "Чёрный Рынок",        new Color(0.55f, 0.55f, 0.55f)),
        };

        private void RefreshReputationCache()
        {
            _reputationCache.Clear();
            var repState = ReputationClientState.Instance;
            if (repState == null || !repState.CurrentReputation.HasValue)
            {
                // Snapshot ещё не пришёл — показать placeholder 5 фракций с value=0.
                for (int i = 0; i < FactionFallback.Length; i++)
                {
                    var f = FactionFallback[i];
                    _reputationCache.Add(new ReputationListItem
                    {
                        factionId = f.id, displayName = f.name, value = 0, color = f.color
                    });
                }
            }
            else
            {
                // Snapshot пришёл: рендерим только те фракции что есть в snapshot
                // (а если там < 5 — дополним placeholder'ом, чтобы UI не "потерял" строку).
                var entries = repState.CurrentReputation.Value.entries;
                if (entries == null || entries.Length == 0)
                {
                    for (int i = 0; i < FactionFallback.Length; i++)
                    {
                        var f = FactionFallback[i];
                        _reputationCache.Add(new ReputationListItem
                        {
                            factionId = f.id, displayName = f.name, value = 0, color = f.color
                        });
                    }
                }
                else
                {
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var e = entries[i];
                        var fb = FindFactionFallback((FactionId)e.faction);
                        _reputationCache.Add(new ReputationListItem
                        {
                            factionId = fb.id,
                            displayName = fb.name,
                            value = e.value,
                            color = fb.color
                        });
                    }
                }
            }
            if (_reputationList != null)
            {
                _reputationList.itemsSource = _reputationCache;
                _reputationList.Rebuild();
            }
        }

        private static (string id, string name, Color color) FindFactionFallback(FactionId id)
        {
            // Маппинг FactionId → fallback. GDD-23 5 фракций. None → "Unknown".
            switch (id)
            {
                case FactionId.GuildOfSuccess:  return FactionFallback[0]; // merchants
                case FactionId.GuildOfCreation: return FactionFallback[1]; // engineers
                case FactionId.GuildOfStrength: return FactionFallback[2]; // military
                case FactionId.Resistance:      return FactionFallback[3];
                case FactionId.Underground:     return FactionFallback[4]; // smugglers
                default: return ("unknown", id.ToString(), new Color(0.5f, 0.5f, 0.5f));
            }
        }

        // T-Q13: handlers + refresh для NpcAttitude под-секции.
        private void HandleReputationSnapshot(ReputationSnapshotDto snapshot)
        {
            RefreshReputationCache();
            if (_messageLabel != null && IsVisible() && _activeTab == "reputation")
            {
                _messageLabel.text = snapshot.entries != null
                    ? $"Фракций: {snapshot.entries.Length}"
                    : "Нет данных о репутации";
                _messageLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            }
        }

        private void HandleNpcAttitudeSnapshot(NpcAttitudeSnapshotDto snapshot)
        {
            RefreshNpcAttitudeCache();
            if (_messageLabel != null && IsVisible() && _activeTab == "reputation")
            {
                _messageLabel.text = snapshot.entries != null
                    ? $"Отношений: {snapshot.entries.Length}"
                    : "Нет данных об отношениях";
                _messageLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            }
        }

        private void RefreshNpcAttitudeCache()
        {
            _npcAttitudeCache.Clear();
            var attState = NpcAttitudeClientState.Instance;
            if (attState != null && attState.CurrentNpcAttitude.HasValue)
            {
                var entries = attState.CurrentNpcAttitude.Value.entries;
                if (entries != null)
                {
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var e = entries[i];
                        // T-P19: красивое имя вместо raw npcId
                        string displayName = FormatNpcDisplayName(e.npcId);
                        Color c = e.value > 0
                            ? new Color(0.5f, 0.85f, 0.5f)
                            : (e.value < 0 ? new Color(0.95f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f));
                        _npcAttitudeCache.Add(new NpcAttitudeListItem
                        {
                            npcId = e.npcId,
                            displayName = displayName,
                            value = e.value,
                            color = c
                        });
                    }
                }
            }
            if (_npcAttitudeList != null)
            {
                _npcAttitudeList.itemsSource = _npcAttitudeCache;
                _npcAttitudeList.Rebuild();
            }
        }

        // T-P19: красивое имя NPC. NpcDefinition ассеты в Assets/_Project/Quests/Data/Npcs/
        private static string FormatNpcDisplayName(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return "?";

            // В редакторе — через AssetDatabase (ассеты не в Resources, Runtime.Load не работает)
#if UNITY_EDITOR
            try
            {
                string path = "Assets/_Project/Quests/Data/Npcs/" + npcId + ".asset";
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.NpcDefinition>(path);
                if (def != null && !string.IsNullOrEmpty(def.displayName))
                    return def.displayName;
            }
            catch { }
#endif

            // Runtime fallback: пытаемся загрузить через Resources (если ассеты дублированы в Resources)
            try
            {
                var def = Resources.Load<ProjectC.Quests.NpcDefinition>("Data/Npcs/" + npcId);
                if (def != null && !string.IsNullOrEmpty(def.displayName))
                    return def.displayName;
            }
            catch { }

            // Fallback: "npc_004" → "Npc 004"
            var parts = npcId.Split('_');
            for (int j = 0; j < parts.Length; j++)
            {
                if (parts[j].Length > 0)
                    parts[j] = char.ToUpper(parts[j][0]) + parts[j].Substring(1);
            }
            return string.Join(" ", parts);
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
                // Данных ещё нет — пустой кэш. UI покажет пустой список.
                // СESSION 2 ROLLBACK: вернулись к ListView (было рабочее)
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
                if (_inventoryList != null)
                {
                    if (!ReferenceEquals(_inventoryList.itemsSource, _inventoryCache))
                        _inventoryList.itemsSource = _inventoryCache;
                    _inventoryList.RefreshItems();
                }
                return;
            }

            // Группируем по (itemId) — если несколько стеков одного предмета,
            // суммируем quantity.
            var groups = new Dictionary<int, (int totalQty, InventoryItemDto first)>();
            foreach (var dto in items)
            {
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

            // T-P19 refactor: сортировка по (ItemType, displayName) — категории сгруппированы,
            // внутри категории — алфавитный порядок. Потерялась при рефакторе f136fd5.
            // Dictionary не гарантирует порядка, поэтому сортируем явно.
            var sortedGroups = groups
                .Select(kvp => new { kvp.Key, Value = kvp.Value })
                .OrderBy(x => (int)x.Value.first.type)
                .ThenBy(x =>
                {
                    var def = invState.GetItemDefinition(x.Value.first.itemId);
                    return def != null ? def.itemName : $"Item#{x.Value.first.itemId}";
                }, System.StringComparer.OrdinalIgnoreCase);

            foreach (var entry in sortedGroups)
            {
                var first = entry.Value.first;
                ItemData def = invState.GetItemDefinition(first.itemId);
                _inventoryCache.Add(new InventoryListItem
                {
                    itemId      = first.itemId.ToString(),
                    displayName = def != null ? def.itemName : $"Item#{first.itemId}",
                    type        = (ItemType)first.type,
                    quantity    = entry.Value.totalQty,
                    icon        = def != null ? def.icon : null,
                });
            }

            // СESSION 2 ROLLBACK: ListView itemsSource
            if (_inventoryList != null)
            {
                if (!ReferenceEquals(_inventoryList.itemsSource, _inventoryCache))
                    _inventoryList.itemsSource = _inventoryCache;
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
        // Row factories: inventory (legacy MakeInventoryRow/BindInventoryRow
        // заменены на MakeManualInventoryRow для ScrollView-based render)
        // ============================================================
        /// <summary>
        /// SESSION 2 fix: equip item прямо из inventory через reflection RPC.
        /// Принимает ItemData (уже резолвленный из _itemDatabase), а не string-id,
        /// потому что ID из InventoryClientState может НЕ совпадать с EquipmentServer ID.
        /// </summary>
        private void OnEquipFromInventoryClicked(ProjectC.Items.ItemData def)
        {
            try
            {
                if (def == null) { Debug.LogWarning("[CharacterWindow] item is null"); return; }
                ProjectC.Equipment.EquipSlot slot = ProjectC.Equipment.EquipSlot.None;
                if (def is ProjectC.Equipment.ClothingItemData c) slot = c.slot;
                else if (def is ProjectC.Equipment.ModuleItemData m) slot = m.slot;
                if (slot == ProjectC.Equipment.EquipSlot.None) { Debug.LogWarning("[CharacterWindow] item not equipable"); return; }

                // Резолвим EquipmentServer ID — ищем в _itemDatabase по ItemData reference.
                int itemId = -1;
                var invDbField = typeof(ProjectC.Items.InventoryWorld).GetField("_itemDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var invDb = invDbField?.GetValue(ProjectC.Items.InventoryWorld.Instance) as System.Collections.Generic.Dictionary<int, ProjectC.Items.ItemData>;
                if (invDb != null)
                {
                    foreach (var kvp in invDb)
                    {
                        if (kvp.Value == def) { itemId = kvp.Key; break; }
                    }
                }
                if (itemId <= 0) { Debug.LogWarning("[CharacterWindow] item not found in db"); return; }

                var t = System.Type.GetType("ProjectC.Equipment.EquipmentServer, Assembly-CSharp");
                if (t == null) { Debug.LogWarning("[CharacterWindow] EquipmentServer type not found"); return; }
                var inst = t.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (inst == null) { Debug.LogWarning("[CharacterWindow] EquipmentServer.Instance is null"); return; }
                var mi = t.GetMethod("RequestEquipRpc");
                if (mi == null) { Debug.LogWarning("[CharacterWindow] RequestEquipRpc not found"); return; }
                var rpcParams = System.Activator.CreateInstance(typeof(Unity.Netcode.RpcParams));
                mi.Invoke(inst, new object[] { itemId, slot, rpcParams });
                Debug.Log($"[CharacterWindow] RequestEquipRpc from inventory: itemId={itemId} slot={slot} name={def.itemName}");
            }
            catch (System.Exception ex) { Debug.LogWarning($"[CharacterWindow] OnEquipFromInventoryClicked error: {ex.Message}"); }
        }

        // SESSION 2: при выборе предмета в инвентаре — обновить detail panel справа.
        private void OnInventorySelectionChanged(System.Collections.Generic.IEnumerable<object> selectedItems)
        {
            if (selectedItems == null) return;
            if (_inventoryList == null) return;
            int selectedIdx = _inventoryList.selectedIndex;
            if (selectedIdx < 0 || selectedIdx >= _inventoryCache.Count)
            {
                ClearInventoryDetail();
                return;
            }
            var item = _inventoryCache[selectedIdx];
            UpdateInventoryDetail(item);
        }

        private void ClearInventoryDetail()
        {
            if (_invDetailName != null) _invDetailName.text = "Выберите предмет слева";
            if (_invDetailType != null) _invDetailType.text = "—";
            if (_invDetailWeight != null) _invDetailWeight.text = "—";
            if (_invDetailStat != null) _invDetailStat.text = "—";
            if (_invDetailDesc != null) _invDetailDesc.text = "—";
        }

        private void UpdateInventoryDetail(InventoryListItem item)
        {
            // Резолвим ItemData для деталей
            ProjectC.Items.ItemData def = null;
            if (int.TryParse(item.itemId, out int parsedId))
            {
                def = ProjectC.Items.InventoryWorld.Instance?.GetItemDefinition(parsedId);
            }
            if (def == null)
            {
                var invDbField = typeof(ProjectC.Items.InventoryWorld).GetField("_itemDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var invDb = invDbField?.GetValue(ProjectC.Items.InventoryWorld.Instance) as System.Collections.Generic.Dictionary<int, ProjectC.Items.ItemData>;
                if (invDb != null)
                {
                    foreach (var kvp in invDb)
                    {
                        if (kvp.Value != null && kvp.Value.itemName == item.displayName) { def = kvp.Value; break; }
                    }
                }
            }

            if (_invDetailName != null) _invDetailName.text = item.displayName;
            if (_invDetailType != null) _invDetailType.text = $"Тип: {ItemTypeNames.GetDisplayName(item.type)}";
            if (_invDetailWeight != null) _invDetailWeight.text = $"Вес: {(def != null ? def.weightKg : 0):F1} кг";
            if (_invDetailDesc != null) _invDetailDesc.text = def != null && !string.IsNullOrEmpty(def.description) ? def.description : "—";

            // Stat bonuses для ClothingItemData/ModuleItemData
            if (_invDetailStat != null)
            {
                if (def is ProjectC.Equipment.ClothingItemData c)
                {
                    string sb = "Бонусы: ";
                    if (c.strengthBonus != 0) sb += $"STR {(c.strengthBonus>=0?"+":"")}{c.strengthBonus:F0} ";
                    if (c.dexterityBonus != 0) sb += $"DEX {(c.dexterityBonus>=0?"+":"")}{c.dexterityBonus:F0} ";
                    if (c.intelligenceBonus != 0) sb += $"INT {(c.intelligenceBonus>=0?"+":"")}{c.intelligenceBonus:F0} ";
                    if (c.strengthMultiplier != 0) sb += $"\nSTR ×{(c.strengthMultiplier):F2} ";
                    if (c.dexterityMultiplier != 0) sb += $"\nDEX ×{(c.dexterityMultiplier):F2} ";
                    if (c.intelligenceMultiplier != 0) sb += $"\nINT ×{(c.intelligenceMultiplier):F2} ";
                    _invDetailStat.text = sb;
                }
                else if (def is ProjectC.Equipment.ModuleItemData m)
                {
                    string sb = "Бонусы: ";
                    if (m.strengthBonus != 0) sb += $"STR {(m.strengthBonus>=0?"+":"")}{m.strengthBonus:F0} ";
                    if (m.dexterityBonus != 0) sb += $"DEX {(m.dexterityBonus>=0?"+":"")}{m.dexterityBonus:F0} ";
                    if (m.intelligenceBonus != 0) sb += $"INT {(m.intelligenceBonus>=0?"+":"")}{m.intelligenceBonus:F0} ";
                    if (m.weaponDamageBonus != 0) sb += $"\nWeapon DMG +{m.weaponDamageBonus:F0}";
                    if (m.sensorRangeBonus != 0) sb += $"\nSensor +{m.sensorRangeBonus:F0}";
                    if (m.craftingSpeedMultiplier != 0) sb += $"\nCraft ×{m.craftingSpeedMultiplier:F2}";
                    _invDetailStat.text = sb;
                }
                else
                {
                    _invDetailStat.text = "—";
                }
            }
        }

        // ============================================================
        // Row factories: inventory (восстановлено)
        // ============================================================

        private VisualElement MakeInventoryRow()
        {
            var row = new VisualElement();
            row.AddToClassList("inventory-row");
            var icon = new VisualElement { name = "row-icon" }; icon.AddToClassList("inventory-icon"); row.Add(icon);
            var name = new Label { name = "row-name" }; name.AddToClassList("inventory-name"); row.Add(name);
            var type = new Label { name = "row-type" }; type.AddToClassList("inventory-type"); row.Add(type);
            var qty  = new Label { name = "row-qty"  }; qty.AddToClassList("inventory-qty");   row.Add(qty);
            var equipBtn = new VisualElement { name = "row-equip-btn" };
            equipBtn.AddToClassList("inventory-equip-btn");
            var equipLabel = new Label { name = "row-equip-label", text = "НАДЕТЬ" };
            equipLabel.AddToClassList("inventory-equip-label");
            equipBtn.Add(equipLabel);
            row.Add(equipBtn);
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

                // SESSION 2: [НАДЕТЬ] для всех Equipment items
                var equipBtn = row.Q<VisualElement>("row-equip-btn");
                if (equipBtn == null) return;

                ProjectC.Items.ItemData def = null;
                if (int.TryParse(item.itemId, out int parsedItemId))
                {
                    def = ProjectC.Items.InventoryWorld.Instance?.GetItemDefinition(parsedItemId);
                }
                if (def == null && !string.IsNullOrEmpty(item.displayName))
                {
                    var invDbField = typeof(ProjectC.Items.InventoryWorld).GetField("_itemDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var invDb = invDbField?.GetValue(ProjectC.Items.InventoryWorld.Instance) as System.Collections.Generic.Dictionary<int, ProjectC.Items.ItemData>;
                    if (invDb != null)
                    {
                        foreach (var kvp in invDb)
                        {
                            if (kvp.Value != null && (kvp.Value.itemName == item.displayName ||
                                kvp.Value.itemName.Contains(item.displayName) ||
                                item.displayName.Contains(kvp.Value.itemName)))
                            {
                                if (kvp.Value is ProjectC.Equipment.ClothingItemData || kvp.Value is ProjectC.Equipment.ModuleItemData)
                                { def = kvp.Value; break; }
                            }
                        }
                    }
                }
                bool isEquipable = def is ProjectC.Equipment.ClothingItemData || def is ProjectC.Equipment.ModuleItemData;
                if (!isEquipable && item.type == ProjectC.Items.ItemType.Equipment) isEquipable = true;

                if (isEquipable)
                {
                    equipBtn.style.display = DisplayStyle.Flex;
                    var capturedDef = def;
                    string capturedDisplayName = item.displayName;
                    // SESSION 2 fix: unregister old handler to avoid duplicates (ListView recycling)
                    equipBtn.UnregisterCallback<ClickEvent>(OnInventoryEquipBtnClick);
                    equipBtn.RegisterCallback<ClickEvent>(evt => {
                        Debug.Log("!!!!! EQUIP FROM INVENTORY !!!!! name=" + capturedDisplayName);
                        OnEquipFromInventoryClicked(capturedDef);
                        evt.StopPropagation();
                    });
                }
                else
                {
                    equipBtn.style.display = DisplayStyle.None;
                }
            }
        }

        private void OnInventoryEquipBtnClick(ClickEvent evt) { /* placeholder for Unregister */ }

        // ============================================================
        // Row factories: reputation (новый)
        // ============================================================

        private VisualElement MakeReputationRow()
        {
            var row = new VisualElement();
            row.AddToClassList("reputation-row");
            var faction = new Label { name = "rep-faction" };
            faction.AddToClassList("rep-faction");
            row.Add(faction);
            var bar = new VisualElement { name = "rep-bar" };
            bar.AddToClassList("rep-bar");
            row.Add(bar);
            var negFill = new VisualElement { name = "rep-neg" };
            negFill.AddToClassList("rep-bar-neg");
            bar.Add(negFill);
            var center = new VisualElement { name = "rep-center" };
            center.AddToClassList("rep-bar-center");
            bar.Add(center);
            var posFill = new VisualElement { name = "rep-pos" };
            posFill.AddToClassList("rep-bar-pos");
            bar.Add(posFill);
            var spacer = new VisualElement();
            spacer.AddToClassList("rep-bar-spacer");
            bar.Add(spacer);
            var value = new Label { name = "rep-value" };
            value.AddToClassList("rep-value");
            row.Add(value);
            return row;
        }

        private void BindReputationRow(VisualElement row, int index)
        {
            if (_reputationList == null) return;
            var src = _reputationList.itemsSource as List<ReputationListItem>;
            if (src == null || index < 0 || index >= src.Count) return;
            var r = src[index];

            row.Q<Label>("rep-faction").text = r.displayName;
            row.Q<Label>("rep-value").text   = (r.value > 0 ? "+" : "") + r.value.ToString();

            // Bar: center-zero, negative=red (left), positive=green (right).
            // Диапазон -100..+100 → 200 единиц, 0 = 50% от ширины бара.
            var negFill = row.Q<VisualElement>("rep-neg");
            var posFill = row.Q<VisualElement>("rep-pos");
            if (r.value < 0)
            {
                float negPct = Mathf.Clamp01(Mathf.Abs(r.value) / 100f) * 50f;
                negFill.style.width = new Length(negPct, LengthUnit.Percent);
                negFill.style.backgroundColor = new Color(0.85f, 0.2f, 0.2f);
                posFill.style.width = new Length(0, LengthUnit.Percent);
            }
            else
            {
                negFill.style.width = new Length(0, LengthUnit.Percent);
                float posPct = Mathf.Clamp01(r.value / 100f) * 50f;
                posFill.style.width = new Length(posPct, LengthUnit.Percent);
                posFill.style.backgroundColor = new Color(0.2f, 0.85f, 0.2f);
            }
        }

        // T-Q13: NpcAttitude row factory + binder.
        private VisualElement MakeNpcAttitudeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("npc-attitude-row");
            var name  = new Label { name = "npc-att-name" }; name.AddToClassList("npc-att-name"); row.Add(name);
            // Center-zero bar (как reputation, но диапазон -100..+200)
            var bar = new VisualElement { name = "npc-att-bar" };
            bar.AddToClassList("rep-bar");
            row.Add(bar);
            var negFill = new VisualElement { name = "npc-att-neg" };
            negFill.AddToClassList("rep-bar-neg");
            bar.Add(negFill);
            var center = new VisualElement { name = "npc-att-center" };
            center.AddToClassList("rep-bar-center");
            bar.Add(center);
            var posFill = new VisualElement { name = "npc-att-pos" };
            posFill.AddToClassList("rep-bar-pos");
            bar.Add(posFill);
            var spacer = new VisualElement();
            spacer.AddToClassList("rep-bar-spacer");
            bar.Add(spacer);
            var value = new Label { name = "npc-att-value" };
            value.AddToClassList("rep-value");
            row.Add(value);
            return row;
        }

        private void BindNpcAttitudeRow(VisualElement row, int index)
        {
            if (_npcAttitudeList == null) return;
            var src = _npcAttitudeList.itemsSource as List<NpcAttitudeListItem>;
            if (src == null || index < 0 || index >= src.Count) return;
            var r = src[index];

            row.Q<Label>("npc-att-name").text = r.displayName;
            row.Q<Label>("npc-att-value").text = (r.value > 0 ? "+" : "") + r.value.ToString();

            // Bar: -100..+200 → 300 единиц, 0 = 33.33% от ширины.
            var negFill = row.Q<VisualElement>("npc-att-neg");
            var posFill = row.Q<VisualElement>("npc-att-pos");
            float total = 300f;
            float zeroPct = 100f / total; // 33.33%
            if (r.value < 0)
            {
                float negPct = Mathf.Clamp01(Mathf.Abs(r.value) / total) * 100f;
                negFill.style.width = new Length(negPct, LengthUnit.Percent);
                negFill.style.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
                posFill.style.width = new Length(0, LengthUnit.Percent);
            }
            else
            {
                negFill.style.width = new Length(0, LengthUnit.Percent);
                float posPct = Mathf.Clamp01(r.value / total) * 100f;
                posFill.style.width = new Length(posPct, LengthUnit.Percent);
                posFill.style.backgroundColor = new Color(0.25f, 0.85f, 0.25f);
            }
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
            private bool _isQuestTrackerSubscribed = false;

            // ============================================================
            // T-P14: Skills subscription state (M3) — lazy-subscribe pattern как 5 других state'ов
            // ============================================================

            private bool _isSkillsSubscribed = false;
            private List<SkillRow> _skillsCombatCache = new List<SkillRow>();
            private List<SkillRow> _skillsSocialCache = new List<SkillRow>();

            /// <summary>
            /// Row DTO для skill list (skillId, displayName, category, state, cost, prereqs).
            /// State = "LEARNED" | "AVAILABLE" | "LOCKED" (per skill tree UI states).
            /// </summary>
            private struct SkillRow
            {
                public string SkillId;
                public string DisplayName;
                public SkillCategory Category;
                public string State;          // LEARNED / AVAILABLE / LOCKED
                public float XpCost;
                public int RequiredTier;
                public string PrereqNames;    // comma-separated "Нужно: A, B"
            }

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

            // T-Q21 fix: подписка на QuestTracker.OnTrackChanged для sync кнопок "Следить"/"Не следить".
            private void SubscribeQuestTracker()
            {
            if (_isQuestTrackerSubscribed) return;
            var trk = QuestTracker.GetOrFindInstance();
            if (trk == null) return;
            trk.OnTrackChanged += HandleQuestTrackerChanged;
            _isQuestTrackerSubscribed = true;
            }

            private void UnsubscribeQuestTracker()
            {
            if (!_isQuestTrackerSubscribed) return;
            // T-Q21 fix: используем только Instance (не Find) чтобы не дёргать GameObject.Find во время shutdown.
            var trk = QuestTracker.Instance;
            if (trk != null) trk.OnTrackChanged -= HandleQuestTrackerChanged;
            _isQuestTrackerSubscribed = false;
            }

            private void HandleQuestTrackerChanged()
            {
            // Tracking state изменился — перестроить quest cache чтобы кнопки обновились.
            RefreshQuestsCache();
            }

            private void UnsubscribeQuestState()
            {
            if (!_isQuestStateSubscribed) return;
            // T-Q21 fix: also unsubscribe QuestTracker.
            UnsubscribeQuestTracker();
            var qs = QuestClientState.Instance;
            if (qs == null) { _isQuestStateSubscribed = false; return; }
            qs.OnSnapshotUpdated -= HandleQuestSnapshotUpdated;
            qs.OnQuestResult -= HandleQuestResult;
            qs.OnQuestDiscovered -= HandleQuestDiscovered;
            _isQuestStateSubscribed = false;
            }

            // ============================================================
            // T-P14: Skills subscription + row factories (M3)
            // ============================================================

            private void SubscribeSkills()
            {
                if (_isSkillsSubscribed) return;
                var sk = ProjectC.Skills.SkillsClientState.Instance;
                if (sk == null) return;
                sk.OnSkillsUpdated += HandleSkillsSnapshot;
                sk.OnSkillResult += HandleSkillResult;
                _isSkillsSubscribed = true;
                Debug.Log("[CharacterWindow] Subscribed to SkillsClientState (snapshot/result)");
            }

            private void UnsubscribeSkills()
            {
                if (!_isSkillsSubscribed) return;
                var sk = ProjectC.Skills.SkillsClientState.Instance;
                if (sk == null) { _isSkillsSubscribed = false; return; }
                sk.OnSkillsUpdated -= HandleSkillsSnapshot;
                sk.OnSkillResult -= HandleSkillResult;
                _isSkillsSubscribed = false;
            }

            // ============================================================
            // T-P16: Stats subscription + display (M1 stats → M4 UI)
            // ============================================================

            private bool _isStatsSubscribed = false;

            private void SubscribeStats()
            {
                if (_isStatsSubscribed) return;
                var st = ProjectC.Stats.StatsClientState.Instance;
                if (st == null) return;
                st.OnStatsUpdated += HandleStatsSnapshot;
                _isStatsSubscribed = true;
                Debug.Log("[CharacterWindow] Subscribed to StatsClientState (snapshot)");
            }

            private void UnsubscribeStats()
            {
                if (!_isStatsSubscribed) return;
                var st = ProjectC.Stats.StatsClientState.Instance;
                if (st == null) { _isStatsSubscribed = false; return; }
                st.OnStatsUpdated -= HandleStatsSnapshot;
                _isStatsSubscribed = false;
            }

            private void HandleStatsSnapshot(ProjectC.Stats.Dto.StatsSnapshotDto snap)
            {
                if (_debugLogging) Debug.Log($"[CharacterWindow] HandleStatsSnapshot: STR={snap.strength:F2} DEX={snap.dexterity:F2} INT={snap.intelligence:F2} | refs: strBar={_statStrBar!=null} dexBar={_statDexBar!=null} intBar={_statIntBar!=null} strVal={_statStrValue!=null}");
                RefreshStatsDisplay(snap);
            }

            /// <summary>
            /// Обновить 3 stat-row-progress (label+ProgressBar+value) по snapshot.
            /// tier-low/mid/high/master CSS classes на fill bar (per Q4.3).
            /// </summary>
            private void RefreshStatsDisplay(ProjectC.Stats.Dto.StatsSnapshotDto snap)
            {
                // STR (effective = base + equip bonus)
                if (_statStrBar != null)
                {
                    float maxStr = snap.strengthXpForNextTier > 0 ? snap.strengthXpForNextTier : 100f;
                    _statStrBar.value = Mathf.Clamp01(snap.effectiveStrength / maxStr);
                    ApplyTierClass(_statStrBar, snap.strengthTier);
                }
                if (_statStrValue != null)
                {
                    string bonusStr = (snap.effectiveStrength - snap.strength > 0.01f) ? $" (+{snap.effectiveStrength - snap.strength:F0})" : "";
                    _statStrValue.text = $"{snap.effectiveStrength:F1}/{snap.strengthXpForNextTier:F0} T{snap.strengthTier}{bonusStr}";
                }
                if (_statStrRow != null) ApplyRowClass(_statStrRow, snap.strengthTier);

                // DEX
                if (_statDexBar != null)
                {
                    float maxDex = snap.dexterityXpForNextTier > 0 ? snap.dexterityXpForNextTier : 100f;
                    _statDexBar.value = Mathf.Clamp01(snap.effectiveDexterity / maxDex);
                    ApplyTierClass(_statDexBar, snap.dexterityTier);
                }
                if (_statDexValue != null)
                {
                    string bonusDex = (snap.effectiveDexterity - snap.dexterity > 0.01f) ? $" (+{snap.effectiveDexterity - snap.dexterity:F0})" : "";
                    _statDexValue.text = $"{snap.effectiveDexterity:F1}/{snap.dexterityXpForNextTier:F0} T{snap.dexterityTier}{bonusDex}";
                }
                if (_statDexRow != null) ApplyRowClass(_statDexRow, snap.dexterityTier);

                // INT
                if (_statIntBar != null)
                {
                    float maxInt = snap.intelligenceXpForNextTier > 0 ? snap.intelligenceXpForNextTier : 100f;
                    _statIntBar.value = Mathf.Clamp01(snap.effectiveIntelligence / maxInt);
                    ApplyTierClass(_statIntBar, snap.intelligenceTier);
                }
                if (_statIntValue != null)
                {
                    string bonusInt = (snap.effectiveIntelligence - snap.intelligence > 0.01f) ? $" (+{snap.effectiveIntelligence - snap.intelligence:F0})" : "";
                    _statIntValue.text = $"{snap.effectiveIntelligence:F1}/{snap.intelligenceXpForNextTier:F0} T{snap.intelligenceTier}{bonusInt}";
                }
                if (_statIntRow != null) ApplyRowClass(_statIntRow, snap.intelligenceTier);
            }

            /// <summary>
            /// Apply tier-low/mid/high/master CSS class к ProgressBar's inner fill (per roadmap Q4.3 + T-P15 USS).
            /// Thresholds: low=T0-T2, mid=T3-T5, high=T6-T9, master=T10+.
            /// </summary>
            private void ApplyTierClass(UnityEngine.UIElements.ProgressBar bar, int tier)
            {
                if (bar == null) return;
                // ProgressBar в Unity 6: fill через `bar.Q<VisualElement>(className: "unity-progress-bar__progress")`
                // Или просто на сам bar — T-P15 USS имеет `.stat-progress-fill.tier-*` для внутреннего fill.
                // Apply к bar (для cascading) — USS применится к inner fill через `.stat-progress-fill` selector
                bar.RemoveFromClassList("tier-low");
                bar.RemoveFromClassList("tier-mid");
                bar.RemoveFromClassList("tier-high");
                bar.RemoveFromClassList("tier-master");
                string tierClass = tier switch
                {
                    <= 2 => "tier-low",
                    <= 5 => "tier-mid",
                    <= 9 => "tier-high",
                    _    => "tier-master",
                };
                bar.AddToClassList(tierClass);
            }

            private void ApplyRowClass(UnityEngine.UIElements.VisualElement row, int tier)
            {
                if (row == null) return;
                row.RemoveFromClassList("tier-promoted");
                if (tier > 0) row.AddToClassList("tier-promoted"); // visual marker на T>0
            }

            /// <summary>
            /// FIX 2026-06-17: pre-fill equipment cache с пустыми слотами (до первого snapshot).
            /// </summary>
            private void InitEquipmentCache(System.Collections.Generic.List<EquipRow> cache, int startIdx, int count, bool isModule)
            {
                cache.Clear();
                for (int i = startIdx; i < startIdx + count; i++)
                {
                    var slot = ProjectC.Equipment.EquipmentData.IndexToSlot(i);
                    cache.Add(new EquipRow
                    {
                        Slot = slot,
                        ItemId = 0,
                        ItemName = "—",
                        SlotName = slot.ToString(),
                        Bonuses = "",
                        TierText = "",
                        IsModule = isModule,
                    });
                }
            }

            /// <summary>
            /// FIX 2026-06-17: pre-fill skills cache из Resources (все LOCKED до первого snapshot).
            /// </summary>
            private void InitSkillsCache()
            {
                var all = Resources.LoadAll<ProjectC.Skills.SkillNodeConfig>("Skills");
                _skillsCombatCache.Clear();
                _skillsSocialCache.Clear();
                if (all == null) return;
                foreach (var skill in all)
                {
                    if (skill == null || string.IsNullOrEmpty(skill.skillId)) continue;
                    var row = new SkillRow
                    {
                        SkillId = skill.skillId,
                        DisplayName = !string.IsNullOrEmpty(skill.displayName) ? skill.displayName : skill.skillId,
                        Category = skill.category,
                        State = "LOCKED",
                        XpCost = skill.LearnXpCost,
                        RequiredTier = skill.RequiredIntelligenceTier,
                        PrereqNames = "",
                    };
                    if (skill.category == ProjectC.Skills.SkillCategory.Combat) _skillsCombatCache.Add(row);
                    else _skillsSocialCache.Add(row);
                }
            }

            private void HandleSkillsSnapshot(System.Collections.Generic.HashSet<string> learned)
            {
                RefreshSkillsCache(learned);
                RebuildSkillsListView();
            }

            private void HandleSkillResult(ProjectC.Skills.Dto.SkillResultDto result)
            {
                // Toast-стиль log для отладки (UI toast появится в T-P15/M4 если roadmap расширится)
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[CharacterWindow] Skill result: code={result.code} skillId='{result.skillId}' reason='{result.reason}'");
                }
                // Snapshot перепридёт через SendSnapshotToOwner после learn/forget, так что cache обновится.
            }

            /// <summary>
            /// Заполняет _skillsCombatCache + _skillsSocialCache из Resources/Skills/ + learned set.
            /// Состояние каждой строки: LEARNED / AVAILABLE / LOCKED (по roadmap §4.3).
            /// </summary>
            private void RefreshSkillsCache(System.Collections.Generic.HashSet<string> learned)
            {
                _skillsCombatCache.Clear();
                _skillsSocialCache.Clear();
                var all = Resources.LoadAll<ProjectC.Skills.SkillNodeConfig>("Skills");
                if (all == null) return;
                // INT tier нужен для LOCKED check. Из StatsClientState (lazy).
                int intTier = 0;
                var statsSt = ProjectC.Stats.StatsClientState.Instance;
                if (statsSt != null && statsSt.CurrentStats.HasValue)
                {
                    intTier = statsSt.CurrentStats.Value.intelligenceTier;
                }
                foreach (var skill in all)
                {
                    if (skill == null || string.IsNullOrEmpty(skill.skillId)) continue;
                    bool isLearned = learned != null && learned.Contains(skill.skillId);
                    string state;
                    if (isLearned) state = "LEARNED";
                    else if (CanLearn(skill, learned, intTier)) state = "AVAILABLE";
                    else state = "LOCKED";
                    var row = new SkillRow
                    {
                        SkillId = skill.skillId,
                        DisplayName = !string.IsNullOrEmpty(skill.displayName) ? skill.displayName : skill.skillId,
                        Category = skill.category,
                        State = state,
                        XpCost = skill.LearnXpCost,
                        RequiredTier = skill.RequiredIntelligenceTier,
                        PrereqNames = GetMissingPrereqNames(skill, learned),
                    };
                    if (skill.category == ProjectC.Skills.SkillCategory.Combat) _skillsCombatCache.Add(row);
                    else _skillsSocialCache.Add(row);
                }
            }

            private bool CanLearn(ProjectC.Skills.SkillNodeConfig skill, System.Collections.Generic.HashSet<string> learned, int intTier)
            {
                if (learned == null) return false;
                if (skill.prerequisites != null)
                {
                    foreach (var p in skill.prerequisites)
                    {
                        if (p != null && !learned.Contains(p.skillId)) return false;
                    }
                }
                if (intTier < skill.RequiredIntelligenceTier) return false;
                return true;
            }

            private string GetMissingPrereqNames(ProjectC.Skills.SkillNodeConfig skill, System.Collections.Generic.HashSet<string> learned)
            {
                if (skill.prerequisites == null || skill.prerequisites.Length == 0) return string.Empty;
                var missing = new System.Collections.Generic.List<string>();
                foreach (var p in skill.prerequisites)
                {
                    if (p == null) continue;
                    if (learned == null || !learned.Contains(p.skillId))
                    {
                        missing.Add(!string.IsNullOrEmpty(p.displayName) ? p.displayName : p.skillId);
                    }
                }
                return string.Join(", ", missing);
            }

            private void RebuildSkillsListView()
            {
                // SESSION 2: manual rebuild into skill containers (no ListView).
                var combatContainer = _root?.Q<VisualElement>("skills-combat-container");
                var socialContainer = _root?.Q<VisualElement>("skills-social-container");
                if (combatContainer == null || socialContainer == null) return;
                combatContainer.Clear();
                socialContainer.Clear();
                foreach (var sk in _skillsCombatCache)
                {
                    var ve = MakeManualSkillRow(sk);
                    if (ve != null) combatContainer.Add(ve);
                }
                foreach (var sk in _skillsSocialCache)
                {
                    var ve = MakeManualSkillRow(sk);
                    if (ve != null) socialContainer.Add(ve);
                }
            }

            /// <summary>
            /// SESSION 2: ручная skill row — простой, всегда видна.
            /// </summary>
            private VisualElement MakeManualSkillRow(SkillRow data)
            {
                var row = new VisualElement();
                row.AddToClassList("skill-row");
                var state = new Label { name = "skill-row-state", text = data.State switch { "LEARNED" => "✓", "AVAILABLE" => "○", _ => "✕" } };
                state.AddToClassList("skill-row-state");
                row.Add(state);
                var title = new Label { name = "skill-row-title", text = data.DisplayName };
                title.AddToClassList("skill-row-title");
                row.Add(title);
                var cost = new Label { name = "skill-row-cost", text = data.XpCost > 0 ? $"{data.XpCost:F0}XP" : "Free" };
                cost.AddToClassList("skill-row-cost");
                row.Add(cost);
                var tier = new Label { name = "skill-row-tier", text = $"T{data.RequiredTier}" };
                tier.AddToClassList("skill-row-tier");
                row.Add(tier);
                // SESSION 2: click row to learn (только AVAILABLE).
                if (data.State == "AVAILABLE")
                {
                    var capturedSkillId = data.SkillId;
                    row.RegisterCallback<ClickEvent>(evt => {
                        Debug.Log("!!!!! SKILL CLICK !!!!! skill=" + data.DisplayName + " id=" + capturedSkillId);
                        OnLearnSkillClicked(capturedSkillId);
                        evt.StopPropagation();
                    });
                }
                return row;
            }

            private void OnLearnSkillClicked(string skillId)
            {
                // Reflection-based RPC to SkillsServer.RequestLearnSkillRpc(string, RpcParams)
                try
                {
                    var t = System.Type.GetType("ProjectC.Skills.SkillsServer, Assembly-CSharp");
                    if (t == null) { Debug.LogWarning("[CharacterWindow] SkillsServer type not found"); return; }
                    var inst = t.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                    if (inst == null) { Debug.LogWarning("[CharacterWindow] SkillsServer.Instance is null"); return; }
                    var mi = t.GetMethod("RequestLearnSkillRpc");
                    if (mi == null) { Debug.LogWarning("[CharacterWindow] RequestLearnSkillRpc not found"); return; }
                    var rpcParams = System.Activator.CreateInstance(typeof(Unity.Netcode.RpcParams));
                    mi.Invoke(inst, new object[] { skillId, rpcParams });
                    Debug.Log($"[CharacterWindow] RequestLearnSkillRpc: skillId={skillId}");
                }
                catch (System.Exception ex) { Debug.LogWarning($"[CharacterWindow] OnLearnSkillClicked error: {ex.Message}"); }
            }

            private UnityEngine.UIElements.ListView _skillsCombatList;  // SESSION 2: unused (manual rebuild)
            private UnityEngine.UIElements.ListView _skillsSocialList;  // SESSION 2: unused (manual rebuild)

            /// <summary>VisualElement factory: state badge + title + cost + prereq + status.</summary>
            private UnityEngine.UIElements.VisualElement MakeSkillRow()
            {
                var row = new UnityEngine.UIElements.VisualElement();
                row.AddToClassList("skill-row");

                var stateBadge = new UnityEngine.UIElements.Label { name = "skill-row-state" };
                stateBadge.AddToClassList("skill-row-state");
                row.Add(stateBadge);

                var title = new UnityEngine.UIElements.Label { name = "skill-row-title" };
                title.AddToClassList("skill-row-title");
                row.Add(title);

                var cost = new UnityEngine.UIElements.Label { name = "skill-row-cost" };
                cost.AddToClassList("skill-row-cost");
                row.Add(cost);

                var desc = new UnityEngine.UIElements.Label { name = "skill-row-desc" };
                desc.AddToClassList("skill-row-desc");
                row.Add(desc);

                var prereq = new UnityEngine.UIElements.Label { name = "skill-row-prereq" };
                prereq.AddToClassList("skill-row-prereq");
                row.Add(prereq);

                return row;
            }

            private void BindSkillRow(UnityEngine.UIElements.VisualElement row, int index, System.Collections.Generic.List<SkillRow> cache)
            {
                if (index < 0 || index >= cache.Count) return;
                var data = cache[index];

                var stateLabel = row.Q<UnityEngine.UIElements.Label>("skill-row-state");
                var titleLabel = row.Q<UnityEngine.UIElements.Label>("skill-row-title");
                var costLabel = row.Q<UnityEngine.UIElements.Label>("skill-row-cost");
                var descLabel = row.Q<UnityEngine.UIElements.Label>("skill-row-desc");
                var prereqLabel = row.Q<UnityEngine.UIElements.Label>("skill-row-prereq");

                if (stateLabel != null)
                {
                    stateLabel.text = data.State switch
                    {
                        "LEARNED"   => "✅",
                        "AVAILABLE" => "○",
                        "LOCKED"    => "✕",
                        _ => "?",
                    };
                }
                if (titleLabel != null) titleLabel.text = data.DisplayName;
                if (costLabel != null)
                {
                    costLabel.text = data.XpCost > 0
                        ? $"{data.XpCost:F0} XP"
                        : "Free";
                }
                if (descLabel != null) descLabel.text = $"Tier ≥ {data.RequiredTier}";
                if (prereqLabel != null)
                {
                    prereqLabel.text = !string.IsNullOrEmpty(data.PrereqNames)
                        ? $"Нужно: {data.PrereqNames}"
                        : string.Empty;
                }

                row.RemoveFromClassList("skill-row-learned");
                row.RemoveFromClassList("skill-row-available");
                row.RemoveFromClassList("skill-row-locked");
                switch (data.State)
                {
                    case "LEARNED":   row.AddToClassList("skill-row-learned"); break;
                    case "AVAILABLE": row.AddToClassList("skill-row-available"); break;
                    case "LOCKED":    row.AddToClassList("skill-row-locked"); break;
                }
            }

            // ---- ListView setup helper ----
            private void SetupQuestListView(ListView list, ref List<QuestListItem> cacheRef)
            {
            if (list == null) return;
            list.makeItem = MakeQuestRow;
            list.bindItem = BindQuestRow;
            list.unbindItem = UnbindQuestRow;  // T-Q21: cleanup nested objectives container (prevent leak).
            // T-Q21 fix: variable heights (fixedItemHeight=0) + RefreshItems race → ArgumentOutOfRangeException
            // в UI Toolkit 6.0 (PostRefresh.ReleaseItem). Use fixedItemHeight=64 — fits badge row + 3 objectives.
            // Если у quest >3 objectives — последние могут быть clipped, но стабильность важнее.
            list.fixedItemHeight =64;
            list.itemsSource = cacheRef;
            }

            // ============================================================
            // T-P17: Clothing/Modules ListView (M2 equipment в M4 UI)
            // ============================================================

            /// <summary>
            /// SESSION 2 rewrite: ручное построение equip rows (вместо ListView).
            /// ListView НЕ использовался — только клиппинг и проблемы с click.
            /// Строим rows напрямую как children container, все rows видны всегда.
            /// </summary>
            private void RebuildEquipmentListView()
            {
                // Clothing: rebuild в контейнер _clothingContainer (VisualElement)
                if (_clothingContainer != null)
                {
                    _clothingContainer.Clear();
                    foreach (var row in _clothingCache)
                    {
                        var ve = MakeManualEquipRow(row);
                        _clothingContainer.Add(ve);
                    }
                }
                // Modules
                if (_modulesContainer != null)
                {
                    _modulesContainer.Clear();
                    foreach (var row in _modulesCache)
                    {
                        var ve = MakeManualEquipRow(row);
                        _modulesContainer.Add(ve);
                    }
                }
            }

            /// <summary>
            /// SESSION 2: ручная row без ListView. Всегда видна, click работает.
            /// Используем VisualElement как кнопку + RegisterCallback<ClickEvent>
            /// вместо Button.clicked (которое требует focusable=true).
            /// </summary>
            private VisualElement MakeManualEquipRow(EquipRow data)
            {
                var row = new VisualElement();
                row.AddToClassList("equip-slot-row");

                var slot = new Label { name = "equip-slot-name", text = data.SlotName };
                slot.AddToClassList("equip-slot-name");
                row.Add(slot);

                var item = new Label { name = "equip-slot-item", text = data.ItemName };
                item.AddToClassList("equip-slot-item");
                row.Add(item);

                var bonuses = new Label { name = "equip-slot-bonuses" };
                bonuses.AddToClassList("equip-slot-bonuses");
                string bonusText = !string.IsNullOrEmpty(data.Bonuses) ? data.Bonuses : "—";
                if (!string.IsNullOrEmpty(data.TierText)) bonusText += $" ({data.TierText})";
                bonuses.text = bonusText;
                row.Add(bonuses);

                // Кнопка — VisualElement с RegisterCallback<ClickEvent> (нативный UI Toolkit click).
                // В отличие от Button.clicked, срабатывает на любой click без focusable.
                var btn = new VisualElement { name = "equip-slot-btn" };
                btn.AddToClassList("equip-slot-btn");
                var btnLabel = new Label { text = data.ItemId > 0 ? "СНЯТЬ" : "—" };
                btnLabel.AddToClassList("equip-slot-btn-label");
                btnLabel.style.flexGrow = 1;
                btnLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                btn.Add(btnLabel);
                if (data.ItemId > 0)
                {
                    var capturedData = data;
                    btn.RegisterCallback<ClickEvent>(evt => {
                        Debug.Log("!!!!! EQUIP CLICKED !!!!! slot=" + capturedData.SlotName + " id=" + capturedData.ItemId);
                        OnUnequipClicked(capturedData);
                        evt.StopPropagation();
                    });
                }
                else
                {
                    btn.SetEnabled(false);
                }
                row.Add(btn);

                return row;
            }

            /// <summary>
            /// SESSION 2 final: handler получает EquipRow напрямую (уже извлечено из userData).
            /// </summary>
            private void OnUnequipClicked(EquipRow data)
            {
                // RequestUnequipRpc(slot) → EquipmentServer
                var eqSvrType = System.Type.GetType("ProjectC.Equipment.EquipmentServer, Assembly-CSharp");
                if (eqSvrType == null) { Debug.LogWarning("[CharacterWindow] EquipmentServer not found"); return; }
                var inst = eqSvrType.GetMethod("GetStaticInstance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, null)
                           ?? eqSvrType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (inst == null) { Debug.LogWarning("[CharacterWindow] EquipmentServer.Instance is null"); return; }
                var mi = eqSvrType.GetMethod("RequestUnequipRpc");
                if (mi == null) { Debug.LogWarning("[CharacterWindow] RequestUnequipRpc method not found"); return; }
                var defaultRpcParams = System.Activator.CreateInstance(typeof(Unity.Netcode.RpcParams));
                mi.Invoke(inst, new object[] { data.Slot, defaultRpcParams });
                if (Debug.isDebugBuild) Debug.Log($"[CharacterWindow] RequestUnequipRpc: slot={data.Slot} itemId={data.ItemId}");
            }

            // ----- Equipment subscription + refresh (M2 → M4) -----

            private void SubscribeEquipment()
            {
                if (_isEquipmentSubscribed) return;
                var eq = ProjectC.Equipment.EquipmentClientState.Instance;
                if (eq == null) return;
                eq.OnEquipmentUpdated += HandleEquipmentSnapshot;
                eq.OnEquipResult += HandleEquipResult;
                _isEquipmentSubscribed = true;
                Debug.Log("[CharacterWindow] Subscribed to EquipmentClientState (snapshot/result)");
            }

            private void UnsubscribeEquipment()
            {
                if (!_isEquipmentSubscribed) return;
                var eq = ProjectC.Equipment.EquipmentClientState.Instance;
                if (eq == null) { _isEquipmentSubscribed = false; return; }
                eq.OnEquipmentUpdated -= HandleEquipmentSnapshot;
                eq.OnEquipResult -= HandleEquipResult;
                _isEquipmentSubscribed = false;
            }

            private void HandleEquipmentSnapshot(ProjectC.Equipment.Dto.EquipmentSnapshotDto snap)
            {
                RefreshEquipmentCache(snap);
                RebuildEquipmentListView();
            }

            private void HandleEquipResult(ProjectC.Equipment.Dto.EquipResultDto result)
            {
                if (Debug.isDebugBuild)
                {
                    string msg = result.code switch
                    {
                        ProjectC.Equipment.Dto.EquipResultCode.Equipped   => $"✅ Надето: {result.itemId} ({result.slot})",
                        ProjectC.Equipment.Dto.EquipResultCode.Unequipped => $"✅ Снято: {result.slot}",
                        ProjectC.Equipment.Dto.EquipResultCode.Denied     => $"❌ {result.reason}",
                        _ => $"? unknown code={result.code}",
                    };
                    Debug.Log($"[CharacterWindow] OnEquipResult: {msg}");
                }
            }

            /// <summary>
            /// Fill _clothingCache + _modulesCache из EquipmentSnapshotDto (server-side per-player state).
            /// Показываем 13 clothing slots (Head..Accessory2, WeaponMain, WeaponOff) + 3 module slots.
            /// T-P17 MVP: показываем все 13 + 3 всегда (пустые слоты = "—" + disabled button).
            /// </summary>
            private void RefreshEquipmentCache(ProjectC.Equipment.Dto.EquipmentSnapshotDto snap)
            {
                _clothingCache.Clear();
                _modulesCache.Clear();

                // 13 clothing slots (Head..Accessory2, WeaponMain, WeaponOff) — заполняем всегда
                // (10 clothing + Module1..3 = 13, но Module1..3 относятся к modules)
                // Per EquipmentData.SLOT_COUNT=13: Head=0, Chest=1, ..., Module3=12
                // "clothing" = indices 0..9 (Head..WeaponOff), "modules" = indices 10..12 (Module1..3)

                // Clothing rows (10 slots)
                for (int idx = 0; idx < 10; idx++)
                {
                    var slot = ProjectC.Equipment.EquipmentData.IndexToSlot(idx);
                    int itemId = 0;
                    string itemName = "—";
                    string bonuses = "";
                    string tierText = "";
                    if (idx < snap.equip.slotOccupied.Length && snap.equip.slotOccupied[idx] == 1)
                    {
                        itemId = snap.equip.slotItemIds[idx];
                        itemName = LookupItemName(itemId);
                        var itemData = LookupItemData(itemId);
                        if (itemData != null)
                        {
                            bonuses = FormatBonuses(itemData);
                            if (itemData is ProjectC.Equipment.ClothingItemData cd) tierText = $"T{cd.tier}";
                        }
                    }
                    _clothingCache.Add(new EquipRow
                    {
                        Slot = slot,
                        ItemId = itemId,
                        ItemName = itemName,
                        SlotName = slot.ToString(),
                        Bonuses = bonuses,
                        TierText = tierText,
                        IsModule = false,
                    });
                }

                // Module rows (3 slots)
                for (int idx = 10; idx < 13; idx++)
                {
                    var slot = ProjectC.Equipment.EquipmentData.IndexToSlot(idx);
                    int itemId = 0;
                    string itemName = "—";
                    string bonuses = "";
                    string tierText = "";
                    string moduleType = "";
                    if (idx < snap.equip.slotOccupied.Length && snap.equip.slotOccupied[idx] == 1)
                    {
                        itemId = snap.equip.slotItemIds[idx];
                        itemName = LookupItemName(itemId);
                        var itemData = LookupItemData(itemId);
                        if (itemData != null)
                        {
                            bonuses = FormatBonuses(itemData);
                            if (itemData is ProjectC.Equipment.ModuleItemData md)
                            {
                                moduleType = md.moduleType.ToString();
                                tierText = $"T{md.tier}";
                            }
                        }
                    }
                    string fullBonuses = !string.IsNullOrEmpty(moduleType) ? $"[{moduleType}] {bonuses}" : bonuses;
                    _modulesCache.Add(new EquipRow
                    {
                        Slot = slot,
                        ItemId = itemId,
                        ItemName = itemName,
                        SlotName = slot.ToString(),
                        Bonuses = fullBonuses,
                        TierText = tierText,
                        IsModule = true,
                    });
                }
            }

            /// <summary>Lookup ItemData по itemId (InventoryWorld API). T-P17: best-effort — null если не найден.</summary>
            private ProjectC.Items.ItemData LookupItemData(int itemId)
            {
                if (itemId == 0) return null;
                var inv = ProjectC.Items.InventoryWorld.Instance;
                if (inv == null) return null;
                return inv.GetItemDefinition(itemId);
            }

            private string LookupItemName(int itemId)
            {
                var d = LookupItemData(itemId);
                return d != null && !string.IsNullOrEmpty(d.itemName) ? d.itemName : $"#{itemId}";
            }

            /// <summary>Format stat bonuses clothing/module → "+3 STR" / "+1 STR, +1 INT".</summary>
            private string FormatBonuses(ProjectC.Items.ItemData itemData)
            {
                if (itemData is ProjectC.Equipment.ClothingItemData cd)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    if (cd.strengthBonus != 0) parts.Add($"{(cd.strengthBonus > 0 ? "+" : "")}{cd.strengthBonus:F0} STR");
                    if (cd.dexterityBonus != 0) parts.Add($"{(cd.dexterityBonus > 0 ? "+" : "")}{cd.dexterityBonus:F0} DEX");
                    if (cd.intelligenceBonus != 0) parts.Add($"{(cd.intelligenceBonus > 0 ? "+" : "")}{cd.intelligenceBonus:F0} INT");
                    if (cd.strengthMultiplier > 0) parts.Add($"STR×{1f + cd.strengthMultiplier:F2}");
                    if (cd.dexterityMultiplier > 0) parts.Add($"DEX×{1f + cd.dexterityMultiplier:F2}");
                    if (cd.intelligenceMultiplier > 0) parts.Add($"INT×{1f + cd.intelligenceMultiplier:F2}");
                    return parts.Count > 0 ? string.Join(", ", parts) : "—";
                }
                if (itemData is ProjectC.Equipment.ModuleItemData md)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    if (md.strengthBonus != 0) parts.Add($"{(md.strengthBonus > 0 ? "+" : "")}{md.strengthBonus:F0} STR");
                    if (md.dexterityBonus != 0) parts.Add($"{(md.dexterityBonus > 0 ? "+" : "")}{md.dexterityBonus:F0} DEX");
                    if (md.intelligenceBonus != 0) parts.Add($"{(md.intelligenceBonus > 0 ? "+" : "")}{md.intelligenceBonus:F0} INT");
                    if (md.sensorRangeBonus > 0) parts.Add($"Sensor+{md.sensorRangeBonus:F0}m");
                    if (md.craftingSpeedMultiplier > 0) parts.Add($"Craft×{1f + md.craftingSpeedMultiplier:F2}");
                    if (md.weaponDamageBonus > 0) parts.Add($"DMG+{md.weaponDamageBonus:F0}");
                    return parts.Count > 0 ? string.Join(", ", parts) : "—";
                }
                return "—";
            }

            // ---- Row factory ----
            private VisualElement MakeQuestRow()
            {
            var row = new VisualElement();
            row.AddToClassList("quest-row");
            // T-Q21 fix: top-line (badge+title+counter+track) в одном horizontal row.
            // Раньше всё было column → track-btn уезжал за границу 64px фиксированной высоты.
            var topLine = new VisualElement { name = "row-top-line" };
            topLine.AddToClassList("quest-row-top-line");
            row.Add(topLine);

            var badge = new Label { name = "row-state" };
            badge.AddToClassList("quest-row-state");
            topLine.Add(badge);
            var title = new Label { name = "row-title" };
            title.AddToClassList("quest-row-title");
            topLine.Add(title);
            var obj = new Label { name = "row-objectives" };
            obj.AddToClassList("quest-row-objectives");
            topLine.Add(obj);
            // T-Q12: per-row "Следить" / "Не следить" button (toggle).
            var trackBtn = new Button { name = "row-track-btn" };
            trackBtn.AddToClassList("quest-row-track-btn");
            trackBtn.text = "Следить";
            trackBtn.RegisterCallback<ClickEvent>(OnQuestRowTrackClicked);
            topLine.Add(trackBtn);

            // T-Q21: nested objectives list (vertical container, populated в BindQuestRow).
            var objContainer = new VisualElement { name = "row-objectives-container" };
            objContainer.AddToClassList("quest-row-objectives-container");
            row.Add(objContainer);
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

            // T-Q21: render objectives list under the quest row.
            var objContainer = row.Q<VisualElement>("row-objectives-container");
            if (objContainer != null)
            {
                objContainer.Clear();
                if (q.objectives != null)
                {
                    for (int k = 0; k < q.objectives.Count; k++)
                    {
                        var o = q.objectives[k];
                        var lbl = new Label();
                        lbl.AddToClassList("quest-row-objective-line");
                        if (o.completed) lbl.AddToClassList("quest-row-objective-line-done");
                        // Format: ☐/☑ Description (current/total) или просто description
                        string bullet = o.completed ? "☑" : "☐";
                        string counter = "";
                        if (o.requiredQuantity > 1)
                        {
                            counter = $" ({o.currentValue}/{o.requiredQuantity})";
                        }
                        lbl.text = $"  {bullet} {o.description}{counter}";
                        objContainer.Add(lbl);
                    }
                }
            }
            }

            // T-Q21: cleanup nested objectives container (prevent VisualElement leak).
            private void UnbindQuestRow(VisualElement row, int index)
            {
            if (row == null) return;
            var objContainer = row.Q<VisualElement>("row-objectives-container");
            if (objContainer != null) objContainer.Clear();
            }
            private void OnQuestRowTrackClicked(ClickEvent evt)
            {
            var btn = evt.target as Button;
            if (btn == null) return;
            // T-Q21 fix: кнопка теперь внутри row-top-line, ищем parent с userData (row).
            VisualElement row = btn.parent;
            while (row != null && row.userData == null) row = row.parent;
            if (row == null) {
                Debug.LogWarning("[CharacterWindow] OnQuestRowTrackClicked: row not found (no parent with userData)");
                return;
            }
            var questId = row.userData as string;
            if (string.IsNullOrEmpty(questId)) {
                Debug.LogWarning($"[CharacterWindow] OnQuestRowTrackClicked: questId is null (userData type={row.userData?.GetType().Name ?? "null"})");
                return;
            }
            Debug.Log($"[CharacterWindow] OnQuestRowTrackClicked: questId={questId}");
            var trk = QuestTracker.GetOrFindInstance();
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
            // T-Q21: populate nested objectives list (description + current/required for UI).
            if (item.objectives == null) item.objectives = new System.Collections.Generic.List<ObjectiveRowItem>(objs.Length);
            else item.objectives.Clear();
            for (int j = 0; j < objs.Length; j++)
            {
                var o = objs[j];
                item.objectives.Add(new ObjectiveRowItem
                {
                    description = o.description ?? o.objectiveId ?? "",
                    completed = o.completed,
                    currentValue = o.currentValue,
                    requiredQuantity = o.requiredQuantity > 0 ? o.requiredQuantity : 1
                });
            }
            }
            else if (item.objectives != null)
            {
                item.objectives.Clear();
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
            // T-Q21: schedule на next frame — fixedItemHeight=0 (variable) layout update + bindItem
            // могут race с одновременным RefreshItems → ArgumentOutOfRangeException в ReleaseItem.
            // Отложенный вызов гарантирует что предыдущий layout pass завершился.
            if (_pendingQuestRefresh) return;
            _pendingQuestRefresh = true;
            StartCoroutine(DeferredQuestRefresh());
            }

            private bool _pendingQuestRefresh;

            private System.Collections.IEnumerator DeferredQuestRefresh()
            {
            yield return null;  // wait one frame
            _pendingQuestRefresh = false;
            ApplyQuestListRefreshImmediate();
            }

            private void ApplyQuestListRefreshImmediate()
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
            if (Debug.isDebugBuild) Debug.Log($"[CharacterWindow] HandleQuestSnapshotUpdated: {snap.quests?.Length ?? 0} quests, tab={_activeTab}, discovered={_questsDiscoveredCache.Count}");
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

            // T-P19: Отказаться от квеста (reject/abandon — заглушка, серверная часть не реализована)
            private void OnRejectQuestClicked()
            {
            SetMessage("Отказ от квеста пока не реализован (ждёт серверную часть)");
            }


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
