# CharacterWindow — Пошаговый план реализации

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`, `10_DESIGN.md`

---

## 0. Общий подход

Реализация **строго в 4 фазы**, каждая компилируется и проверяется отдельно:

| Фаза | Что | Почему отдельно |
|------|-----|------------------|
| **0** | UXML + USS файлы + заглушка `CharacterWindow.cs` + GO в BootstrapScene | Без скрипта не запустится; проверяем что UXML/USS валидны (без ошибок импорта) |
| **1** | Базовое окно: Show/Hide, 4 FIX'а, SwitchTab, header/info-bar, таб "Персонаж" с stats | Smoke test: открыть/закрыть/переключить — должно работать |
| **2** | Таб "Контракты" + подписка на `ContractClientState` (реюз из MarketWindow) | Можно проверить изолированно: открыть таб — данные подтянутся если есть контракты |
| **3** | Табы "Корабль", "Репутация", "Инвентарь" (read-only плейсхолдеры + фильтры) | Финальная полировка + проверка адаптивности |

**Декомпозиция под сабагентов** — сабагент = **одна фаза** (фазы независимы компиляционно; 1-3 можно запустить параллельно если 0 уже закоммичена).

**Критично:** перед стартом — `refresh_unity` + `read_console` (если Unity запущено) и подтвердить `git status` пользователя. Своих `.meta`/`.asmdef` не создавать.

---

## Фаза 0: каркас (UXML, USS, скрипт-минимум, GO)

**Цель:** новое окно открывается (показывает пустую шапку + 5 кнопок таба), Esc закрывает, pickingMode/курсор работают корректно.

### 0.1. Создать UXML

**Файл:** `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml`

Взять полный UXML из `10_DESIGN.md` §2 (там готовый XML). Записать строго как в доке. **Без `.meta`** — Unity создаст.

### 0.2. Создать USS

**Файл:** `Assets/_Project/UI/Resources/UI/CharacterWindow.uss`

Взять полный USS из `10_DESIGN.md` §3. Записать строго. **Без `.meta`**.

### 0.3. Создать CharacterWindow.cs (минимальная версия)

**Файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UI.Client
{
    [RequireComponent(typeof(UIDocument))]
    public class CharacterWindow : MonoBehaviour
    {
        public static CharacterWindow Instance { get; private set; }

        [SerializeField] private VisualTreeAsset characterWindowUxml;
        [SerializeField] private StyleSheet characterWindowUss;
        [SerializeField] private bool visibleOnStart = false;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _mainContainer;
        private bool _built;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (characterWindowUxml == null) characterWindowUxml = Resources.Load<VisualTreeAsset>("UI/CharacterWindow");
            if (characterWindowUss == null) characterWindowUss = Resources.Load<StyleSheet>("UI/CharacterWindow");
            if (Instance == null) Instance = this;
        }

        private void OnEnable()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) { Debug.LogError("[CharacterWindow] нет UIDocument"); return; }
            EnsureBuilt();
        }

        private void Start()
        {
            if (!_built || _root == null || _mainContainer == null)
            {
                Debug.LogWarning("[CharacterWindow] Start(): layout invalid, rebuilding");
                EnsureBuilt();
            }
        }

        private void EnsureBuilt()
        {
            if (_doc.rootVisualElement == null) return;
            if (characterWindowUxml == null) characterWindowUxml = Resources.Load<VisualTreeAsset>("UI/CharacterWindow");
            if (characterWindowUss == null) characterWindowUss = Resources.Load<StyleSheet>("UI/CharacterWindow");
            if (characterWindowUxml == null) { Debug.LogError("[CharacterWindow] UXML not found"); return; }

            _doc.rootVisualElement.Clear();
            if (characterWindowUss != null) _doc.rootVisualElement.styleSheets.Add(characterWindowUss);
            _root = characterWindowUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            _mainContainer = _root.Q<VisualElement>("main-container");

            // === Subscriptions (phase 0: только close-кнопка и Esc) ===
            var closeBtn = _root.Q<Button>("close-btn");
            if (closeBtn != null) closeBtn.clicked += OnCloseClicked;

            // === Initial state ===
            SetVisible(visibleOnStart);
            _doc.rootVisualElement.MarkDirtyRepaint();
            if (_doc.rootVisualElement != null)
            {
                _doc.rootVisualElement.schedule.Execute(() => _doc.rootVisualElement.MarkDirtyRepaint()).StartingIn(50);
            }
            _built = true;
            Debug.Log($"[CharacterWindow] Built (phase 0): root.children={_doc.rootVisualElement.childCount}");
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            var nm = NetworkManager.Singleton;  // ← добавь `using Unity.Netcode;`
            if (nm == null || !nm.IsListening) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsVisible())
            {
                Hide();
            }
            // TODO: открытие по P — handled by NetworkPlayer, см. 1.2
        }

        private void OnCloseClicked() => SetVisible(false);

        public void Show()
        {
            if (!_built || _mainContainer == null) EnsureBuilt();
            if (_root != null) _root.pickingMode = PickingMode.Position;
            SetVisible(true);
            _doc?.rootVisualElement?.MarkDirtyRepaint();
        }

        public void Hide()
        {
            if (_root != null) _root.pickingMode = PickingMode.Ignore;
            SetVisible(false);
        }

        public bool IsVisible() => _mainContainer != null && _mainContainer.style.display == DisplayStyle.Flex;

        private void SetVisible(bool v)
        {
            if (_mainContainer != null)
            {
                _mainContainer.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
                if (v) ApplyInlineFallbackStyles(_mainContainer);
            }
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
            // Копия логики из MarketWindow.cs строки ~1105-1135
            main.style.position = Position.Absolute;
            main.style.top = new Length(5, LengthUnit.Percent);
            main.style.left = new Length(50, LengthUnit.Percent);
            main.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), 0));
            main.style.width = 720;
            main.style.maxWidth = new Length(90, LengthUnit.Percent);
            main.style.maxHeight = new Length(90, LengthUnit.Percent);
            main.style.backgroundColor = new Color(0.078f, 0.098f, 0.137f, 0.95f);
            main.style.borderTopWidth = 2; main.style.borderRightWidth = 2; main.style.borderBottomWidth = 2; main.style.borderLeftWidth = 2;
            main.style.borderTopColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderRightColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderBottomColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderLeftColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderTopLeftRadius = 8; main.style.borderTopRightRadius = 8;
            main.style.borderBottomLeftRadius = 8; main.style.borderBottomRightRadius = 8;
            main.style.paddingTop = 12; main.style.paddingRight = 12; main.style.paddingBottom = 12; main.style.paddingLeft = 12;
            main.style.color = new Color(0.863f, 0.863f, 0.902f);
            main.style.fontSize = 14;
            main.style.flexDirection = FlexDirection.Column;
            main.style.alignItems = Align.Stretch;
        }
    }
}
```

**Без `.meta`**, **без `.asmdef`**.

### 0.4. Создать GameObject в BootstrapScene

**Способ А (рекомендую) — через MCP `manage_gameobject`:**
```bash
# Если Unity запущено с Coplay:
mcp__unityMCP__manage_gameobject action=create name="[CharacterWindow]" components="UIDocument,ProjectC.UI.Client.CharacterWindow"
# Затем:
mcp__unityMCP__manage_gameobject action=set_component_property target="[CharacterWindow]" component="UIDocument" properties={...}
# Прописать visualTreeAsset и PanelSettings
```

**Способ Б — вручную в Editor:** File → Open Scene → BootstrapScene.unity → Hierarchy → Create Empty → `[CharacterWindow]` → Add Component → UIDocument + CharacterWindow. В инспекторе: UIDocument.SourceAsset = `UI/CharacterWindow.uxml` (выбрать из Project view); UIDocument.PanelSettings = `MarketPanelSettings` (re-use). CharacterWindow.visualTreeAsset — auto-resolved через Resources fallback, можно оставить пустым.

**Способ В (если Unity не запущено и MCP недоступно):** попросить пользователя создать GO вручную после компиляции. **Не делать через Bash-правку .unity-файла** — это критично ломает GUID-ы и scene-кэш.

### 0.5. Compile + smoke test

1. `refresh_unity` (force, compile=request, wait_for_ready=true) — Unity пересоберёт ассеты, импортирует UXML/USS
2. `read_console` (errors+warnings) — должно быть 0 errors
3. Если MCP GO создан способом А — проверить, что в Hierarchy появился `[CharacterWindow]` через `find_gameobjects`
4. Save scene (через MCP или вручную)

### 0.6. Проверка

Открыть Unity Editor → Play → Host → нажать Esc (закрывает любые открытые окна) → кликнуть руками в Hierarchy `[CharacterWindow]` Inspector → в ContextMenu `Show` не работает без скриптинга, но VisualTree должен собраться при Start (Debug.Log `[CharacterWindow] Built` появится в Console).

Если кнопка `Show()` нужна в тестах — добавить временный debug-вызов из NetworkPlayer (потом убрать).

---

## Фаза 1: SwitchTab + таб "Персонаж" (read-only stats)

**Цель:** все 5 табов кликаются; по умолчанию открыт "Персонаж"; в нём видны стат-поля.

### 1.1. Patch CharacterWindow.cs — добавить все поля

В класс добавить:

```csharp
// === State ===
private string _activeTab = "character";

// Header / info
private Label _characterNameLabel, _timeInfoLabel, _creditsLabel, _locationLabel, _messageLabel;

// Sections
private VisualElement _characterSection, _shipSection, _reputationSection, _contractsSection, _inventorySection;
private VisualElement _filtersRow;

// Tabs
private Button _tabCharacter, _tabShip, _tabReputation, _tabContracts, _tabInventory;

// Stats (character)
private Label _statName, _statLevel, _statXp, _statCredits, _statDebt, _statActiveContracts;

// Action buttons
private Button _closeBtn;
```

### 1.2. Patch EnsureBuilt — найти все элементы, повесить click-handlers на табы

```csharp
// === Element refs ===
_characterNameLabel = _root.Q<Label>("character-name-label");
_timeInfoLabel      = _root.Q<Label>("time-info-label");
_creditsLabel       = _root.Q<Label>("credits-label");
_locationLabel      = _root.Q<Label>("location-label");
_messageLabel       = _root.Q<Label>("message-label");

_characterSection  = _root.Q<VisualElement>("character-section");
_shipSection       = _root.Q<VisualElement>("ship-section");
_reputationSection = _root.Q<VisualElement>("reputation-section");
_contractsSection  = _root.Q<VisualElement>("contracts-section");
_inventorySection  = _root.Q<VisualElement>("inventory-section");
_filtersRow        = _root.Q<VisualElement>("filters-row");

_tabCharacter  = _root.Q<Button>("tab-character");
_tabShip       = _root.Q<Button>("tab-ship");
_tabReputation = _root.Q<Button>("tab-reputation");
_tabContracts  = _root.Q<Button>("tab-contracts");
_tabInventory  = _root.Q<Button>("tab-inventory");

_statName            = _root.Q<Label>("stat-name");
_statLevel           = _root.Q<Label>("stat-level");
_statXp              = _root.Q<Label>("stat-xp");
_statCredits         = _root.Q<Label>("stat-credits");
_statDebt            = _root.Q<Label>("stat-debt");
_statActiveContracts = _root.Q<Label>("stat-active-contracts");

_closeBtn = _root.Q<Button>("close-btn");
if (_closeBtn != null) _closeBtn.clicked += OnCloseClicked;

// === Tab subscriptions ===
if (_tabCharacter  != null) _tabCharacter.clicked  += () => SwitchTab("character");
if (_tabShip       != null) _tabShip.clicked       += () => SwitchTab("ship");
if (_tabReputation != null) _tabReputation.clicked += () => SwitchTab("reputation");
if (_tabContracts  != null) _tabContracts.clicked  += () => SwitchTab("contracts");
if (_tabInventory  != null) _tabInventory.clicked  += () => SwitchTab("inventory");

SwitchTab(_activeTab);  // по умолчанию "character"
```

### 1.3. SwitchTab

```csharp
private void SwitchTab(string tab)
{
    _activeTab = tab;
    bool isCharacter  = tab == "character";
    bool isShip       = tab == "ship";
    bool isReputation = tab == "reputation";
    bool isContracts  = tab == "contracts";
    bool isInventory  = tab == "inventory";

    if (_characterSection  != null) _characterSection.style.display  = isCharacter  ? DisplayStyle.Flex : DisplayStyle.None;
    if (_shipSection       != null) _shipSection.style.display       = isShip       ? DisplayStyle.Flex : DisplayStyle.None;
    if (_reputationSection != null) _reputationSection.style.display = isReputation ? DisplayStyle.Flex : DisplayStyle.None;
    if (_contractsSection  != null) _contractsSection.style.display  = isContracts  ? DisplayStyle.Flex : DisplayStyle.None;
    if (_inventorySection  != null) _inventorySection.style.display  = isInventory  ? DisplayStyle.Flex : DisplayStyle.None;

    // Подсветка активного таба
    SetActiveTabVisual(_tabCharacter,  isCharacter);
    SetActiveTabVisual(_tabShip,       isShip);
    SetActiveTabVisual(_tabReputation, isReputation);
    SetActiveTabVisual(_tabContracts,  isContracts);
    SetActiveTabVisual(_tabInventory,  isInventory);

    // filters-row виден только для Контракты/Инвентарь
    if (_filtersRow != null)
    {
        bool showFilters = isContracts || isInventory;
        _filtersRow.style.display = showFilters ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // Action buttons — пока только close (контрактные добавятся в Фазе 2)
    if (_closeBtn != null) _closeBtn.style.display = DisplayStyle.Flex;  // всегда

    // На каждое переключение обновляем данные таба
    if (isCharacter)  RefreshCharacterStats();
    if (isShip)       RefreshShipStats();
    if (isReputation) RefreshReputationCache();
    if (isInventory)  RefreshInventoryCache();
    if (isContracts)  ApplyContractFilters();  // фаза 2 — будет реальная подписка
}

private static void SetActiveTabVisual(Button btn, bool isActive)
{
    if (btn == null) return;
    if (isActive) btn.AddToClassList("active"); else btn.RemoveFromClassList("active");
}
```

### 1.4. RefreshCharacterStats (заглушка)

```csharp
private NetworkPlayer _localPlayer;

private NetworkPlayer FindLocalPlayer()
{
    if (_localPlayer != null && _localPlayer.IsOwner) return _localPlayer;
    var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    for (int i = 0; i < players.Length; i++)
    {
        if (players[i] == null || !players[i].IsOwner) continue;
        if (players[i].GetComponent<ProjectC.Network.NetworkPlayerSpawner>() != null) continue;  // ghost-фильтр
        _localPlayer = players[i];
        return _localPlayer;
    }
    return null;
}

private void RefreshCharacterStats()
{
    if (_localPlayer == null) FindLocalPlayer();
    if (_statName != null) _statName.text = _localPlayer != null ? (_localPlayer.IsLocalPlayer ? "Игрок (Owner)" : "Игрок") : "—";
    if (_statLevel != null) _statLevel.text = "1";  // placeholder
    if (_statXp != null) _statXp.text = "0";        // placeholder
    if (_statCredits != null)
    {
        var ms = ProjectC.Trade.Client.MarketClientState.Instance;
        _statCredits.text = ms != null && ms.CurrentSnapshot.HasValue
            ? $"{ms.CurrentSnapshot.Value.credits:F0} CR"
            : "0 CR";
    }
    if (_statDebt != null)
    {
        var cs = ProjectC.Trade.Client.ContractClientState.Instance;
        float debt = cs != null && cs.CurrentSnapshot.HasValue ? cs.CurrentSnapshot.Value.debtAmount : 0f;
        _statDebt.text = $"{debt:F0} CR";
        _statDebt.RemoveFromClassList("debt-warn");
        _statDebt.RemoveFromClassList("debt-danger");
        if (debt > 1000f) _statDebt.AddToClassList("debt-danger");
        else if (debt > 100f) _statDebt.AddToClassList("debt-warn");
    }
    if (_statActiveContracts != null)
    {
        var cs = ProjectC.Trade.Client.ContractClientState.Instance;
        int active = cs != null && cs.CurrentSnapshot.HasValue
            ? (cs.CurrentSnapshot.Value.active?.Length ?? 0)
            : 0;
        _statActiveContracts.text = active.ToString();
    }
}
```

### 1.5. RefreshShipStats / RefreshReputationCache / RefreshInventoryCache — заглушки (фаза 3 наполнит)

```csharp
private void RefreshShipStats()
{
    // Phase 1: placeholder
    if (_shipName != null) _shipName.text = "—";
    if (_shipState != null) _shipState.text = "—";
    if (_shipSpeed != null) _shipSpeed.text = "—";
    if (_shipFuel != null) _shipFuel.text = "—";
    if (_shipCargo != null) _shipCargo.text = "—";
}
private void RefreshReputationCache() { /* phase 3 */ }
private void RefreshInventoryCache() { /* phase 3 */ }
```

### 1.6. RefreshReputationCache, RefreshInventoryCache, ApplyContractFilters — заглушки на этом этапе

Объявить пустые методы (чтобы компилировалось). Наполним в фазе 2/3.

### 1.7. Compile + smoke test

1. `refresh_unity` → 0 errors
2. `read_console` — warnings о `using` не критичны, если компилируется
3. Save scene
4. Проверить в Play: Esc по-прежнему закрывает, табы кликаются (можно увидеть Debug.Log если добавить в SwitchTab)

---

## Фаза 2: таб "Контракты" (re-use MarketWindow логики)

**Цель:** таб "Контракты" подписывается на `ContractClientState`, показывает active+available, работают кнопки ВЗЯТЬ/СДАТЬ/ПРОВАЛИТЬ.

### 2.1. Patch — добавить поля

```csharp
// ListView для контрактов
private ListView _contractsList;
private Button _acceptBtn, _completeBtn, _failBtn;

// Кэш
private ContractDto[] _contractsCache = Array.Empty<ContractDto>();
private int _selectedContractItem = -1;

// Filter controls
private DropdownField _filterSource, _filterState;
private TextField _filterSearch;

// Подписка
private ContractClientState _contractState;
```

### 2.2. Patch EnsureBuilt — найти + подписаться

```csharp
// В EnsureBuilt после прочих Q<...>:
_contractsList = _root.Q<ListView>("contracts-list");
_acceptBtn     = _root.Q<Button>("accept-btn");
_completeBtn   = _root.Q<Button>("complete-btn");
_failBtn       = _root.Q<Button>("fail-btn");
_filterSource  = _root.Q<DropdownField>("filter-source");
_filterState   = _root.Q<DropdownField>("filter-state");
_filterSearch  = _root.Q<TextField>("filter-search");

if (_contractsList != null)
{
    _contractsList.makeItem = MakeContractRow;
    _contractsList.bindItem = BindContractRow;
    _contractsList.fixedItemHeight = 32;
    _contractsList.selectionType = SelectionType.Single;
    _contractsList.selectedIndex = -1;
    _contractsList.selectionChanged += selectedItems =>
    {
        _selectedContractItem = FindSelectedItemIndex<ContractDto>(_contractsList, selectedItems);
        _contractsList.Rebuild();
    };
}

if (_acceptBtn   != null) _acceptBtn.clicked   += OnAcceptContractClicked;
if (_completeBtn != null) _completeBtn.clicked += OnCompleteContractClicked;
if (_failBtn     != null) _failBtn.clicked     += OnFailContractClicked;

// Filters — заполняем options
if (_filterSource != null)
{
    _filterSource.choices = new List<string> { "Все", "Контракты", "Квесты" };  // Квесты — placeholder
    _filterSource.value = "Все";
    _filterSource.RegisterValueChangedCallback(evt => ApplyContractFilters());
}
if (_filterState != null)
{
    _filterState.choices = new List<string> { "Все", "Активные", "Доступные" };
    _filterState.value = "Все";
    _filterState.RegisterValueChangedCallback(evt => ApplyContractFilters());
}
if (_filterSearch != null)
{
    _filterSearch.RegisterValueChangedCallback(evt => ApplyContractFilters());
}

// === Subscribe to ContractClientState (re-use MarketWindow pattern) ===
_contractState = ContractClientState.Instance;
if (_contractState != null)
{
    _contractState.OnSnapshotUpdated += HandleContractSnapshot;
    _contractState.OnContractResult  += HandleContractResult;
    // Запросить список (даже если в зоне, это безопасно — server rate-limited)
    var nearestZone = MarketZoneRegistry.LocalPlayerZone;
    if (nearestZone != null) _contractState.RequestList(nearestZone.LocationId);
}
```

### 2.3. MakeContractRow / BindContractRow — копия из MarketWindow.cs

**Скопировать дословно** `MakeContractRow` / `BindContractRow` / `GetContractTypeDisplayName` / `GetContractTypeClass` / `GetContractTimeRemainingString` / `GetContractTimerClass` из `MarketWindow.cs:780-820` (static helpers). Это **уже сделано** в MarketWindow, не дублировать логику — сделать static в MarketWindow публичными (или скопировать как есть в CharacterWindow — небольшое дублирование приемлемо для 1-го этапа, рефакторинг shared-helpers — отдельный тикет).

### 2.4. SwitchTab — показать action buttons в contracts

В методе `SwitchTab` добавить:

```csharp
bool isContracts = tab == "contracts";
if (_acceptBtn   != null) _acceptBtn.style.display   = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
if (_completeBtn != null) _completeBtn.style.display = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
if (_failBtn     != null) _failBtn.style.display     = isContracts ? DisplayStyle.Flex : DisplayStyle.None;
```

### 2.5. HandleContractSnapshot / HandleContractResult / ApplyContractFilters

**Скопировать логику** из `MarketWindow.cs:HandleContractSnapshot` (line 690-733) и `HandleContractResult` (line 740-778). Применить `ApplyContractFilters()` после заполнения `_contractsCache`.

Дополнительно — `ApplyContractFilters()`:
```csharp
private void ApplyContractFilters()
{
    if (_contractsList == null) return;
    IEnumerable<ContractDto> src = _contractsCache ?? Array.Empty<ContractDto>();

    // Source filter
    string source = _filterSource?.value ?? "Все";
    if (source == "Квесты")
    {
        // Quests не реализованы — показываем пустой список
        _contractsList.itemsSource = Array.Empty<ContractDto>();
        _contractsList.Rebuild();
        return;
    }
    // "Все" и "Контракты" — одинаково (всё что есть)

    // State filter
    string state = _filterState?.value ?? "Все";
    if (state == "Активные")
    {
        src = src.Where(c => c.state == (byte)ProjectC.Trade.ContractState.Active);
    }
    else if (state == "Доступные")
    {
        src = src.Where(c => c.state == (byte)ProjectC.Trade.ContractState.Pending);
    }
    // "Все" — не фильтруем

    // Search filter
    string search = _filterSearch?.value?.ToLower() ?? "";
    if (!string.IsNullOrEmpty(search))
    {
        src = src.Where(c => (c.displayName ?? "").ToLower().Contains(search) || (c.contractId ?? "").ToLower().Contains(search));
    }

    var result = src.ToArray();
    _contractsList.itemsSource = result;
    _selectedContractItem = -1;
    _contractsList.selectedIndex = -1;
    _contractsList.Rebuild();
}
```

### 2.6. OnAcceptContractClicked / OnCompleteContractClicked / OnFailContractClicked

**Скопировать** из `MarketWindow.cs:872-961` (с optimistic update для accept + pulse). Если `MarketZoneRegistry.LocalPlayerZone` недоступен — `_contractState.RequestXxx(contractId)` всё равно работает (server сам проверит зону). Это безопасно — сервер вернёт `NotInZone`, и `HandleContractResult` покажет ошибку в `_messageLabel`.

### 2.7. Patch OnDisable / OnDestroy — отписка

```csharp
private void OnDisable()
{
    if (_contractState != null)
    {
        _contractState.OnSnapshotUpdated -= HandleContractSnapshot;
        _contractState.OnContractResult  -= HandleContractResult;
    }
}
```

### 2.8. Compile + smoke test

1. `refresh_unity` → 0 errors
2. `read_console` — 0 errors
3. Save scene
4. Play → host → подойти к `MarketZone_Primium` → E → открыть `MarketWindow` → таб "Контракты" → ВЗЯТЬ → затем `CharacterWindow` → таб "Контракты" → тот же контракт виден с таймером в активных

---

## Фаза 3: табы "Корабль", "Репутация", "Инвентарь" (плейсхолдеры + фильтры)

**Цель:** все 5 табов функциональны. Корабль/Репутация — read-only плейсхолдеры. Инвентарь — реальный список из `NetworkPlayer.Inventory` с фильтрами по типу.

### 3.1. Корабль — заглушка с локальными данными

```csharp
private Label _shipName, _shipState, _shipSpeed, _shipFuel, _shipCargo;

private void RefreshShipStats()
{
    if (_localPlayer == null) FindLocalPlayer();
    if (_shipName != null)  _shipName.text  = "—";  // будет читать из ShipController когда появится client-state
    if (_shipState != null) _shipState.text = _localPlayer != null ? (_localPlayer.IsInShip ? "В корабле" : "На палубе") : "—";
    if (_shipSpeed != null) _shipSpeed.text = "—";
    if (_shipFuel  != null) _shipFuel.text  = "—";
    if (_shipCargo != null) _shipCargo.text = "—";
}
```

### 3.2. Репутация — плейсхолдер

```csharp
private ListView _reputationList;
private List<ReputationListItem> _reputationCache = new List<ReputationListItem>();

private struct ReputationListItem
{
    public string factionId;
    public string displayName;
    public int value;       // -100..+100
    public Color color;
}

private void RefreshReputationCache()
{
    // Плейсхолдер — 5 гильдий из GDD-23, все по нулям
    _reputationCache = new List<ReputationListItem>
    {
        new ReputationListItem { factionId = "merchants",    displayName = "Гильдия Торговцев",   value = 0,   color = new Color(0.6f, 0.8f, 0.4f) },
        new ReputationListItem { factionId = "engineers",    displayName = "Мануфактура «Аврора»", value = 0,   color = new Color(0.4f, 0.6f, 0.9f) },
        new ReputationListItem { factionId = "military",     displayName = "Военный Анклав",       value = 0,   color = new Color(0.8f, 0.4f, 0.4f) },
        new ReputationListItem { factionId = "resistance",   displayName = "Сопротивление",        value = 0,   color = new Color(0.7f, 0.5f, 0.9f) },
        new ReputationListItem { factionId = "smugglers",    displayName = "Чёрный Рынок",         value = 0,   color = new Color(0.5f, 0.5f, 0.5f) },
    };
    if (_reputationList != null)
    {
        _reputationList.itemsSource = _reputationCache;
        _reputationList.Rebuild();
    }
}

private VisualElement MakeReputationRow()
{
    var row = new VisualElement();
    row.AddToClassList("reputation-row");
    var faction = new Label { name = "rep-faction" }; faction.AddToClassList("reputation-faction"); row.Add(faction);
    var bar = new VisualElement { name = "rep-bar" }; bar.AddToClassList("reputation-bar"); row.Add(bar);
    var fill = new VisualElement { name = "rep-fill" }; fill.AddToClassList("reputation-fill"); bar.Add(fill);
    var value = new Label { name = "rep-value" }; value.AddToClassList("reputation-value"); row.Add(value);
    return row;
}

private void BindReputationRow(VisualElement row, int index)
{
    if (index < 0 || index >= _reputationCache.Count) return;
    var r = _reputationCache[index];
    row.Q<Label>("rep-faction").text = r.displayName;
    row.Q<Label>("rep-value").text = r.value.ToString("+#;-#;0");
    // Bar width: 0..100% = -100..+100 → 50% = 0, 0% = -100, 100% = +100
    float pct = Mathf.Clamp01((r.value + 100f) / 200f) * 100f;
    var fill = row.Q<VisualElement>("rep-fill");
    fill.style.width = new Length(pct, LengthUnit.Percent);
    fill.style.backgroundColor = r.color;
}
```

В `EnsureBuilt`:
```csharp
_reputationList = _root.Q<ListView>("reputation-list");
if (_reputationList != null)
{
    _reputationList.makeItem = MakeReputationRow;
    _reputationList.bindItem = BindReputationRow;
    _reputationList.fixedItemHeight = 32;
}
```

### 3.3. Инвентарь — реальные данные из NetworkPlayer.Inventory

```csharp
private ListView _inventoryList;
private List<InventoryListItem> _inventoryCache = new List<InventoryListItem>();

private struct InventoryListItem
{
    public string itemId;
    public string displayName;
    public ItemType type;
    public int quantity;
    public Sprite icon;
}

private void RefreshInventoryCache()
{
    _inventoryCache.Clear();
    if (_localPlayer == null) FindLocalPlayer();
    var inv = _localPlayer != null ? _localPlayer.GetComponentInChildren<Inventory>(true) : null;
    // Альтернатива: получить через рефлексию _inventory поле, если GetComponentInChildren не сработает
    if (inv == null)
    {
        // Try via reflection (на случай если Inventory спрятан глубже)
        var fi = typeof(NetworkPlayer).GetField("_inventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fi != null) inv = fi.GetValue(_localPlayer) as Inventory;
    }
    if (inv != null)
    {
        var nonEmptyTypes = inv.GetNonEmptyTypes();
        foreach (var t in nonEmptyTypes)
        {
            var items = inv.GetItemsByType(t);
            // group by itemName для склейки одинаковых
            var groups = items.GroupBy(i => i != null ? i.itemName : "(null)");
            foreach (var g in groups)
            {
                var first = g.FirstOrDefault();
                _inventoryCache.Add(new InventoryListItem
                {
                    itemId = first != null ? first.itemName : "(null)",
                    displayName = g.Key,
                    type = t,
                    quantity = g.Count(),
                    icon = first?.icon
                });
            }
        }
    }
    if (_inventoryList != null)
    {
        _inventoryList.itemsSource = _inventoryCache;
        _inventoryList.Rebuild();
    }
}

private VisualElement MakeInventoryRow()
{
    var row = new VisualElement();
    row.AddToClassList("inventory-row");
    var icon = new VisualElement { name = "inv-icon" }; icon.AddToClassList("inventory-icon"); row.Add(icon);
    var name = new Label { name = "inv-name" }; name.AddToClassList("inventory-name"); row.Add(name);
    var type = new Label { name = "inv-type" }; type.AddToClassList("inventory-type"); row.Add(type);
    var qty  = new Label { name = "inv-qty" };  qty.AddToClassList("inventory-qty");  row.Add(qty);
    return row;
}

private void BindInventoryRow(VisualElement row, int index)
{
    if (index < 0 || index >= _inventoryCache.Count) return;
    var item = _inventoryCache[index];
    var icon = row.Q<VisualElement>("inv-icon");
    icon.style.backgroundImage = item.icon != null ? new StyleBackground(item.icon) : new StyleBackground(StyleKeyword.Null);
    row.Q<Label>("inv-name").text = item.displayName;
    row.Q<Label>("inv-type").text = ItemTypeNames.GetDisplayName(item.type);
    row.Q<Label>("inv-qty").text  = $"×{item.quantity}";
}
```

В `EnsureBuilt`:
```csharp
_inventoryList = _root.Q<ListView>("inventory-list");
if (_inventoryList != null)
{
    _inventoryList.makeItem = MakeInventoryRow;
    _inventoryList.bindItem = BindInventoryRow;
    _inventoryList.fixedItemHeight = 32;
    _inventoryList.selectionType = SelectionType.Single;
}
```

### 3.4. Фильтры для инвентаря

В `EnsureBuilt` — обновить `filter-source` options при tab=inventory (нужно в `SwitchTab`):
```csharp
if (tab == "inventory" && _filterSource != null)
{
    var choices = new List<string> { "Все типы" };
    foreach (ItemType t in System.Enum.GetValues(typeof(ItemType))) choices.Add(ItemTypeNames.GetDisplayName(t));
    if (_filterSource.choices != choices)
    {
        _filterSource.choices = choices;
        _filterSource.value = "Все типы";
    }
    // State filter для инвентаря не имеет смысла — скроем
    if (_filterState != null) _filterState.style.display = DisplayStyle.None;
}
else if (tab == "contracts" && _filterSource != null)
{
    // Восстановим contracts-фильтры
    if (_filterSource.choices == null || !_filterSource.choices.Contains("Контракты"))
    {
        _filterSource.choices = new List<string> { "Все", "Контракты", "Квесты" };
        _filterSource.value = "Все";
    }
    if (_filterState != null) _filterState.style.display = DisplayStyle.Flex;
}
```

В `ApplyInventoryFilters`:
```csharp
private void ApplyInventoryFilters()
{
    if (_inventoryList == null) return;
    IEnumerable<InventoryListItem> src = _inventoryCache;
    string source = _filterSource?.value ?? "Все типы";
    if (source != "Все типы")
    {
        src = src.Where(i => ItemTypeNames.GetDisplayName(i.type) == source);
    }
    string search = _filterSearch?.value?.ToLower() ?? "";
    if (!string.IsNullOrEmpty(search))
    {
        src = src.Where(i => (i.displayName ?? "").ToLower().Contains(search));
    }
    _inventoryList.itemsSource = src.ToArray();
    _inventoryList.Rebuild();
}
```

В `SwitchTab` для inventory:
```csharp
if (isInventory) { RefreshInventoryCache(); ApplyInventoryFilters(); }
```

### 3.5. Patch `ApplyContractFilters` — из `SwitchTab` уже вызывается корректно

Ничего дополнительно.

### 3.6. Compile + smoke test

1. `refresh_unity` → 0 errors
2. `read_console` — 0 errors
3. Save scene
4. Полный smoke test: открыть CharacterWindow → пройтись по всем 5 табам → в "Инвентарь" видны добавленные предметы (если что-то подбирал) → фильтр "Ресурсы" оставляет только ресурсы

---

## Открытые доработки (отдельные тикеты, НЕ в этом этапе)

- Кнопка `C` в `NetworkPlayer.Update` для открытия CharacterWindow
- Shared helpers (MakeContractRow, BindContractRow, static-методы `GetContract*Display*`) — вынести в `ProjectC.Trade.Client.ContractRowFactory` static class, чтобы MarketWindow и CharacterWindow реюзали
- Серверный `ReputationServer` + `ReputationClientState` singleton + RPC
- Реальный список кораблей игрока (если в будущем `ShipManager` хранит несколько)
- История завершённых контрактов (хранить в `ContractClientState.History`, заполнять при `ContractResultDto` с `success && newSnapshot != null`)
- Квесты — когда появится GDD-21 реализация, добавить в `filter-source` как полноценный 3-й вариант

## Чек-листы готовности

См. `30_VERIFICATION.md`.
