# CharacterWindow — Дизайн UXML / USS

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`

---

## 1. Файлы

| Файл | Назначение |
|------|------------|
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | MonoBehaviour singleton, контроллер окна, подписки на state'ы, переключение табов |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | Дерево визуальных элементов (header / tabs / 5 секций / actions / message) |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | Стили, реюз концепций из `MarketWindow.uss` |

**Важно:** без `.meta`/`.asmdef` — Unity создаст сам на первом импорте.

## 2. UXML — структура

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement name="main-container" class="character-window">
        <!-- === Header === -->
        <ui:VisualElement class="header">
            <ui:Label name="character-name-label" text="Игрок" class="character-name" />
            <ui:Label name="time-info-label" text="—" class="time-label" />
        </ui:VisualElement>

        <!-- === Info bar === -->
        <ui:VisualElement class="info-bar">
            <ui:Label name="credits-label" text="Кредиты: 0 CR" class="credits-label" />
            <ui:Label name="location-label" text="Локация: —" class="location-info" />
        </ui:VisualElement>

        <!-- === Tabs === -->
        <!--
            Набор табов. ПОРЯДОК ВАЖЕН — соответствует внутреннему _activeTab.
            Новый таб = добавить Button + соответствующую секцию + case в SwitchTab.
        -->
        <ui:VisualElement class="tabs">
            <ui:Button name="tab-character"   text="ПЕРСОНАЖ"        class="tab-btn" />
            <ui:Button name="tab-ship"        text="КОРАБЛЬ"          class="tab-btn" />
            <ui:Button name="tab-reputation"  text="РЕПУТАЦИЯ"        class="tab-btn" />
            <ui:Button name="tab-contracts"   text="КОНТРАКТЫ"        class="tab-btn" />
            <ui:Button name="tab-inventory"   text="ИНВЕНТАРЬ"        class="tab-btn" />
        </ui:VisualElement>

        <!-- === Filters (общий для табов Контракты / Инвентарь) === -->
        <ui:VisualElement name="filters-row" class="filters-row" style="display: none;">
            <ui:DropdownField name="filter-source" class="filter-dd" />  <!-- Контракты/Квесты или Все/По типу -->
            <ui:DropdownField name="filter-state"  class="filter-dd" />  <!-- Активные/Завершённые/Все -->
            <ui:TextField      name="filter-search" class="filter-search" placeholder="поиск..." />
        </ui:VisualElement>

        <!-- === Section 1: ПЕРСОНАЖ (характеристики + будущее) === -->
        <ui:VisualElement name="character-section" class="list-section">
            <ui:Label text="Характеристики" class="section-title" />
            <ui:VisualElement class="stats-grid">
                <ui:VisualElement class="stat-row"><ui:Label text="Имя"        class="stat-label" /><ui:Label name="stat-name"        text="—" class="stat-value" /></ui:VisualElement>
                <ui:VisualElement class="stat-row"><ui:Label text="Уровень"    class="stat-label" /><ui:Label name="stat-level"       text="1"  class="stat-value" /></ui:VisualElement>
                <ui:VisualElement class="stat-row"><ui:Label text="Опыт"       class="stat-label" /><ui:Label name="stat-xp"          text="0"  class="stat-value" /></ui:VisualElement>
                <ui:VisualElement class="stat-row"><ui:Label text="Кредиты"    class="stat-label" /><ui:Label name="stat-credits"     text="0 CR" class="stat-value" /></ui:VisualElement>
                <ui:VisualElement class="stat-row"><ui:Label text="Долг"       class="stat-label" /><ui:Label name="stat-debt"        text="0 CR" class="stat-value debt-ok" /></ui:VisualElement>
                <ui:VisualElement class="stat-row"><ui:Label text="Контракты активные" class="stat-label" /><ui:Label name="stat-active-contracts" text="0" class="stat-value" /></ui:VisualElement>
            </ui:VisualElement>
            <ui:Label text="Одежда, гаджеты и прочие фичи — в будущих итерациях" class="placeholder-hint" />
        </ui:VisualElement>

        <!-- === Section 2: КОРАБЛЬ (заглушка + план) === -->
        <ui:VisualElement name="ship-section" class="list-section" style="display: none;">
            <ui:Label text="Текущий корабль" class="section-title" />
            <ui:VisualElement class="stats-grid">
                <ui:VisualElement class="stat-row"><ui:Label text="Корабль"      class="stat-label" /><ui:Label name="ship-name"        text="—"        class="stat-value" /></ui:VisualElement>
                <ui:VisualElement class="stat-row"><ui:Label text="Состояние"    class="stat-label" /><ui:Label name="ship-state"       text="На палубе" class="stat-value" /></ui:VisualElement>
                <ui:VisualElement class="stat-row"><ui:Label text="Скорость"     class="stat-label" /><ui:Label name="ship-speed"       text="0"        class="stat-value" /></ui:VisualElement>
                <ui:VisualElement class="stat-row"><ui:Label text="Топливо"      class="stat-label" /><ui:Label name="ship-fuel"        text="—"        class="stat-value" /></ui:VisualElement>
                <ui:VisualElement class="stat-row"><ui:Label text="Грузоподъёмность" class="stat-label" /><ui:Label name="ship-cargo"   text="—"        class="stat-value" /></ui:VisualElement>
            </ui:VisualElement>
            <ui:Label text="Управление флотом, модификации, экипаж — в будущих итерациях (см. GDD-10)" class="placeholder-hint" />
        </ui:VisualElement>

        <!-- === Section 3: РЕПУТАЦИЯ (заглушка + план) === -->
        <ui:VisualElement name="reputation-section" class="list-section" style="display: none;">
            <ui:Label text="Репутация по фракциям" class="section-title" />
            <ui:ListView name="reputation-list" class="item-list" />
            <ui:Label text="Репутация: см. GDD-23. Серверная модель в разработке. Сейчас отображаются плейсхолдер-данные." class="placeholder-hint" />
        </ui:VisualElement>

        <!-- === Section 4: КОНТРАКТЫ / КВЕСТЫ (re-use ContractClientState) === -->
        <ui:VisualElement name="contracts-section" class="list-section" style="display: none;">
            <ui:Label text="Контракты и квесты" class="section-title" />
            <ui:ListView name="contracts-list" class="item-list" />
        </ui:VisualElement>

        <!-- === Section 5: ИНВЕНТАРЬ (re-use NetworkPlayer.Inventory) === -->
        <ui:VisualElement name="inventory-section" class="list-section" style="display: none;">
            <ui:Label text="Инвентарь" class="section-title" />
            <ui:ListView name="inventory-list" class="item-list" />
        </ui:VisualElement>

        <!-- === Actions (набор меняется по табу) === -->
        <ui:VisualElement class="actions">
            <!-- Контракты (tab=contracts) -->
            <ui:Button name="accept-btn"    text="ВЗЯТЬ"        class="action-btn accept"    style="display: none;" />
            <ui:Button name="complete-btn"  text="СДАТЬ"        class="action-btn complete"  style="display: none;" />
            <ui:Button name="fail-btn"      text="ПРОВАЛИТЬ"    class="action-btn fail"      style="display: none;" />
            <!-- Закрыть — всегда видна -->
            <ui:Button name="close-btn"     text="ЗАКРЫТЬ"      class="action-btn close" />
        </ui:VisualElement>

        <!-- === Message (общий, как в MarketWindow) === -->
        <ui:Label name="message-label" text="Откройте меню персонажа" class="message-label" />
    </ui:VisualElement>
</ui:UXML>
```

### 2.1. Имена элементов — реестр (для subagent'ов при кодировании)

`CharacterWindow.cs` через `Q<>` должен найти **ровно эти** имена (уникальные в файле):

- Контейнеры: `main-container`, `character-section`, `ship-section`, `reputation-section`, `contracts-section`, `inventory-section`, `filters-row`
- Labels: `character-name-label`, `time-info-label`, `credits-label`, `location-label`, `message-label`
- Табы: `tab-character`, `tab-ship`, `tab-reputation`, `tab-contracts`, `tab-inventory`
- ListView: `reputation-list`, `contracts-list`, `inventory-list`
- Кнопки: `accept-btn`, `complete-btn`, `fail-btn`, `close-btn`
- Фильтры (только для контрактов/инвентаря): `filter-source`, `filter-state`, `filter-search`
- Stats (в character-section): `stat-name`, `stat-level`, `stat-xp`, `stat-credits`, `stat-debt`, `stat-active-contracts`
- Stats (в ship-section): `ship-name`, `ship-state`, `ship-speed`, `ship-fuel`, `ship-cargo`

## 3. USS — стили

Полный файл: `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — пишется subagent'ом на основе `MarketWindow.uss`. Базовые блоки:

```css
/* === Window (реюз .market-window концепции) === */
.character-window {
    position: absolute;
    top: 5%;
    left: 50%;
    translate: -50% 0;
    width: 720px;          /* чуть шире чем MarketWindow — 5 табов */
    max-width: 90%;
    max-height: 90%;
    background-color: rgba(20, 25, 35, 0.95);
    border-width: 2px;
    border-color: rgba(120, 150, 200, 0.8);
    border-radius: 8px;
    padding: 12px;
    color: rgb(220, 220, 230);
    font-size: 14px;
    display: flex;
    flex-direction: column;
    align-items: stretch;
}

/* === Header / info-bar / tabs (реюз) === */
.header { flex-direction: row; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.info-bar { flex-direction: row; justify-content: space-between; margin-bottom: 12px; padding: 6px 0; border-top-width: 1px; border-bottom-width: 1px; border-color: rgba(120, 150, 200, 0.3); }
.tabs { flex-direction: row; flex-wrap: wrap; margin-bottom: 8px; }  /* wrap на случай 6+ табов */
.tab-btn { flex-grow: 1; height: 30px; background-color: rgba(60, 80, 120, 0.4); color: rgb(220, 220, 230); border-width: 0; border-bottom-width: 2px; border-color: transparent; -unity-font-style: bold; }
.tab-btn:hover { background-color: rgba(80, 100, 140, 0.6); }
.tab-btn.active { border-bottom-color: rgb(255, 220, 130); }  /* активный таб подсвечивается */

/* === Filters row (только для Контракты/Инвентарь) === */
.filters-row { flex-direction: row; margin-bottom: 6px; padding: 4px; background-color: rgba(30, 45, 70, 0.4); border-radius: 4px; }
.filter-dd { flex-grow: 1; margin-right: 4px; }
.filter-search { width: 140px; }

/* === List sections (реюз .list-section) === */
.list-section { min-height: 90px; margin-bottom: 6px; }
.section-title { color: rgb(180, 200, 220); font-size: 12px; margin-bottom: 4px; }
.item-list { min-height: 80px; max-height: 280px; background-color: rgba(30, 40, 60, 0.4); border-width: 1px; border-color: rgba(80, 100, 130, 0.4); border-radius: 4px; }

/* === Stats grid (для character-section / ship-section) === */
.stats-grid { flex-direction: column; padding: 6px; background-color: rgba(30, 40, 60, 0.4); border-radius: 4px; }
.stat-row { flex-direction: row; justify-content: space-between; padding: 3px 6px; border-bottom-width: 1px; border-bottom-color: rgba(80, 100, 130, 0.15); }
.stat-label { color: rgb(180, 180, 200); }
.stat-value { color: rgb(220, 220, 230); -unity-font-style: bold; }
.stat-value.debt-warn  { color: rgb(255, 200, 100); }
.stat-value.debt-danger{ color: rgb(255, 100, 100); }

/* === Placeholder hint === */
.placeholder-hint { color: rgb(140, 140, 160); font-size: 11px; -unity-font-style: italic; margin-top: 8px; }

/* === Actions (реюз) === */
.actions { flex-direction: row; flex-wrap: wrap; margin-top: 8px; min-height: 40px; }
.action-btn { flex-grow: 1; height: 36px; margin: 2px; color: rgb(240, 240, 240); -unity-font-style: bold; border-width: 0; border-radius: 4px; }
.action-btn.accept   { background-color: rgba(80, 160, 80, 0.7); }
.action-btn.complete { background-color: rgba(80, 130, 200, 0.7); }
.action-btn.fail     { background-color: rgba(200, 80, 60, 0.7); }
.action-btn.close    { background-color: rgba(120, 80, 80, 0.7); }
.action-btn:hover    { scale: 1.05 1.05; }

/* === Message (реюз) === */
.message-label { margin-top: 8px; padding: 6px; color: rgb(220, 220, 230); font-size: 12px; background-color: rgba(0, 0, 0, 0.3); border-radius: 4px; -unity-text-align: middle-center; }

/* === Row states (для contracts/inventory rows) === */
.row-base { flex-direction: row; align-items: center; height: 30px; padding: 0 8px; border-bottom-width: 1px; border-color: rgba(80, 100, 130, 0.2); }
.row-base:hover { background-color: rgba(100, 130, 180, 0.3); }

/* Contract rows (реюз .contract-row из MarketWindow.uss) */
.contract-row { flex-direction: row; align-items: center; height: 30px; padding: 0 8px; border-bottom-width: 1px; border-color: rgba(80, 100, 130, 0.2); }
.contract-row:hover { background-color: rgba(100, 130, 180, 0.3); }
.contract-type   { width: 90px;  font-size: 12px; }
.contract-item   { flex-grow: 1; font-size: 12px; }
.contract-reward { width: 80px;  font-size: 12px; -unity-text-align: middle-right; }
.contract-timer  { width: 50px;  font-size: 12px; -unity-text-align: middle-right; }
.type-standard { color: rgb(80, 150, 255); }
.type-urgent   { color: rgb(255, 130, 50); -unity-font-style: bold; }
.type-receipt  { color: rgb(80, 255, 100); }
.contract-row-active { background-color: rgba(80, 200, 100, 0.25); border-left-width: 3px; border-left-color: rgb(80, 220, 100); }

/* Inventory rows */
.inventory-row { flex-direction: row; align-items: center; height: 30px; padding: 0 8px; border-bottom-width: 1px; border-color: rgba(80, 100, 130, 0.2); }
.inventory-icon { width: 24px; height: 24px; margin-right: 6px; }
.inventory-name { flex-grow: 1; font-size: 12px; }
.inventory-type { width: 110px; font-size: 11px; color: rgb(150, 180, 220); }
.inventory-qty  { width: 40px;  font-size: 12px; -unity-text-align: middle-right; }
.inventory-row-empty { opacity: 0.5; -unity-font-style: italic; }

/* Reputation rows */
.reputation-row { flex-direction: row; align-items: center; height: 32px; padding: 0 8px; border-bottom-width: 1px; border-color: rgba(80, 100, 130, 0.2); }
.reputation-faction { width: 180px; font-size: 12px; }
.reputation-bar { flex-grow: 1; height: 14px; background-color: rgba(40, 50, 70, 0.6); border-radius: 2px; margin: 0 8px; }
.reputation-fill { height: 100%; border-radius: 2px; }
.reputation-value { width: 60px; font-size: 12px; -unity-text-align: middle-right; }
```

## 4. CharacterWindow.cs — структура класса

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Items;
using ProjectC.Player;
using ProjectC.Trade.Client;        // ContractClientState
using ProjectC.Trade.Dto;            // ContractDto, ContractSnapshotDto, ContractResultDto

namespace ProjectC.UI.Client
{
    [RequireComponent(typeof(UIDocument))]
    public class CharacterWindow : MonoBehaviour
    {
        public static CharacterWindow Instance { get; private set; }

        // === Inspector ===
        [SerializeField] private VisualTreeAsset characterWindowUxml;
        [SerializeField] private StyleSheet       characterWindowUss;
        [SerializeField] private bool visibleOnStart = false;

        // === Runtime ===
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _mainContainer;
        private bool _built;

        // Header / info
        private Label _characterNameLabel, _timeInfoLabel, _creditsLabel, _locationLabel, _messageLabel;

        // Sections
        private VisualElement _characterSection, _shipSection, _reputationSection, _contractsSection, _inventorySection;
        private VisualElement _filtersRow;

        // Tabs
        private Button _tabCharacter, _tabShip, _tabReputation, _tabContracts, _tabInventory;

        // ListViews
        private ListView _reputationList, _contractsList, _inventoryList;

        // Action buttons
        private Button _acceptBtn, _completeBtn, _failBtn, _closeBtn;

        // Filters (для Контракты/Инвентарь)
        private DropdownField _filterSource, _filterState;
        private TextField _filterSearch;

        // Stats (character)
        private Label _statName, _statLevel, _statXp, _statCredits, _statDebt, _statActiveContracts;
        // Stats (ship)
        private Label _shipName, _shipState, _shipSpeed, _shipFuel, _shipCargo;

        // === State ===
        private string _activeTab = "character";  // "character" | "ship" | "reputation" | "contracts" | "inventory"
        private int _selectedContractItem = -1;
        private int _selectedInventoryItem = -1;
        private ContractDto[] _contractsCache = Array.Empty<ContractDto>();
        private List<InventoryListItem> _inventoryCache = new List<InventoryListItem>();
        private List<ReputationListItem> _reputationCache = new List<ReputationListItem>();

        // === Cached references ===
        private ContractClientState _contractState;
        private NetworkPlayer _localPlayer;  // для доступа к _inventory (Owner)

        // === Lifecycle ===
        private void Awake() { /* Instance + UXML/USS load — копия MarketWindow.Awake */ }
        private void OnEnable() { /* EnsureBuilt */ }
        private void Start()   { /* rebuild если IsLayoutValid()==false */ }
        private void OnDisable(){ /* unsubscribe */ }
        private void Update()  { /* Esc → Hide, опционально Tab toggle */ }

        // === Build ===
        private void EnsureBuilt() { /* Find elements, subscribe, build ListView, SwitchTab("character"), SetVisible(visibleOnStart) — копия MarketWindow.EnsureBuilt */ }
        private bool IsLayoutValid() => _built && _root != null && _mainContainer != null;

        // === Tab switching ===
        private void SwitchTab(string tab) {
            // 1) _activeTab = tab
            // 2) DisplayStyle.Flex/None для 5 секций + filters-row
            // 3) Accept/Complete/Fail visible только в tab=contracts
            // 4) Подсветка активного таба (class "active")
            // 5) При tab=="contracts" → заполнить filters-source options
            // 6) При tab=="inventory" → обновить кэш из NetworkPlayer._inventory
        }

        // === Row templates ===
        private VisualElement MakeContractRow()  { /* реюз MakeContractRow из MarketWindow */ }
        private void       BindContractRow(VisualElement row, int index) { /* реюз BindContractRow */ }
        private VisualElement MakeInventoryRow()  { /* новый — icon + name + type + qty */ }
        private void       BindInventoryRow(VisualElement row, int index) { /* new */ }
        private VisualElement MakeReputationRow() { /* новый — faction + bar + value */ }
        private void       BindReputationRow(VisualElement row, int index) { /* new */ }

        // === Snapshot handlers ===
        private void HandleContractSnapshot(ContractSnapshotDto snap)  { /* реюз логики из MarketWindow: active+available, фильтр, обновить кэш, Rebuild */ }
        private void HandleContractResult(ContractResultDto result)    { /* реюз: message + credits + debt update */ }
        private void RefreshCharacterStats()  { /* читать из NetworkPlayer._inventory, ContractClientState, локальных плейсхолдеров */ }
        private void RefreshShipStats()       { /* читать из NetworkPlayer._currentShip (если есть), плейсхолдеры */ }
        private void RefreshReputationCache() { /* плейсхолдер-данные (5 гильдий, нули) + plan-комментарий */ }
        private void RefreshInventoryCache()  { /* читать _localPlayer._inventory.GetItemsByType, спроецировать в List<InventoryListItem> */ }

        // === Filters ===
        private void ApplyContractFilters()  { /* фильтр _contractsCache по _filterSource (Contracts/Quests/All) + _filterState (Active/Completed/All) + _filterSearch (substring match) */ }
        private void ApplyInventoryFilters() { /* фильтр _inventoryCache по _filterSource (itemType) + _filterSearch */ }

        // === Actions ===
        private void OnAcceptContractClicked()   { /* реюз OnAcceptContractClicked */ }
        private void OnCompleteContractClicked() { /* реюз */ }
        private void OnFailContractClicked()     { /* реюз */ }
        private void OnCloseClicked()            => SetVisible(false);

        // === Visibility ===
        public void Show() { /* копия MarketWindow.Show: EnsureBuilt, pickingMode=Position, SetVisible(true), ApplyInlineFallbackStyles, MarkDirtyRepaint + schedule.StartingIn(50) */ }
        public void Hide() { /* pickingMode=Ignore, SetVisible(false) */ }
        public void Toggle() { /* */ }
        public bool IsVisible() { /* */ }
        private void SetVisible(bool v) { /* main-container display + cursor lock/unlock + ApplyInlineFallbackStyles */ }
        private static void ApplyInlineFallbackStyles(VisualElement main) { /* inline-копия из MarketWindow */ }
        private void SetMessage(string msg, bool isError = false) { /* */ }

        // === DTO проекции для ListView ===
        private struct InventoryListItem { public string itemId; public string displayName; public ItemType type; public int quantity; public Sprite icon; }
        private struct ReputationListItem { public string factionId; public string displayName; public int value; public int rankIndex; public Color color; }

        // === Helpers ===
        private static int FindSelectedItemIndex<T>(ListView list, IEnumerable<object> selectedItems) { /* копия из MarketWindow */ }
        private NetworkPlayer FindLocalPlayer() { /* копия логики из MarketInteractor.FindLocalPlayer (с тем же ghost-фильтром) */ }
    }
}
```

## 5. GameObject setup в BootstrapScene

Через MCP (или вручную в Editor):

```
Hierarchy:
  [CharacterWindow]  ← новый GO (рядом с [MarketWindow])
    ├── UIDocument  ← asset = CharacterWindow.uxml, PanelSettings = MarketPanelSettings (re-use)
    └── CharacterWindow  ← компонент (ссылается на UIDocument; visualTreeAsset = Resources/UI/CharacterWindow)
```

- Не привязывать к PlayerPrefab — окно персистентно на сцене, как MarketWindow
- PanelSettings — **можно реюзать** `Assets/_Project/Trade/Resources/UI/MarketPanelSettings.asset` (тот же target-экран, тот же scale mode). Если будет конфликт z-order — отдельный PanelSettings с higher sortOrder

## 6. Сетевые RPC для будущего

**Сейчас — НЕ ДОБАВЛЯТЬ** в NetworkPlayer. CharacterWindow работает ТОЛЬКО с уже существующими RPC:
- `ReceiveContractSnapshotTargetRpc` / `ReceiveContractResultTargetRpc` → `ContractClientState` → CharacterWindow подписывается
- `Inventory` — локальный, server-authoritative через `NetworkChestContainer` / pickup'ы (уже работает)

Новые RPC (для reputation / character stats / ship list) — **отдельный тикет**, когда серверная модель будет спроектирована. CharacterWindow уже умеет читать из любого singleton-проекции — добавить будет = 1 новая подписка.

## 7. Что НЕ делать (явные запреты для subagent'ов)

- ❌ Создавать `CharacterMenuWindow.cs` — это **CharacterWindow.cs**
- ❌ Создавать `CharacterInventoryWindow.cs` — вкладка внутри CharacterWindow
- ❌ Создавать `CharacterReputationWindow.cs` — вкладка
- ❌ Писать `.meta` файлы вручную
- ❌ Писать `.asmdef`
- ❌ Делать `NetworkManagerController.CreateCharacterClientState()` — CharacterWindow не требует singleton-проекции (читает из существующих)
- ❌ `git commit` / `git push` (Mavis их не делает; subagent'ам тоже)
- ❌ Вызывать `run_tests` через MCP
- ❌ Менять файлы в `docs/gdd/` (GDD-авторы) — могут быть добавлены новые GDD, но существующие не трогать

## 8. Связанные документы

- `00_OVERVIEW.md` — общий план
- `20_IMPLEMENTATION_PLAN.md` — пошаговый план для subagent'ов
- `30_VERIFICATION.md` — чек-листы
- `docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` — референс реализации табов
- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` — эталон UI Toolkit-окна
- `Assets/_Project/Trade/Resources/UI/MarketWindow.uss` — реюз стилей
- `AGENTS.md` — хард-рулы
