# UI Integration — расширение CharacterWindow

> **Дата:** 2026-06-14
> **Базируется на:** существующий `CharacterWindow.cs` (2156 строк, 6 табов, 4 FIX'а UI Toolkit)
> **Подход:** добавляем **1 top-level таб "ПРОГРЕССИЯ"** + **4 nested sub-tabs** внутри (Статы / Одежда / Модули / Навыки) — избегаем 9 top-level tabs (оборачивались бы на 2 ряда)

---

## 1. Почему вложенные sub-tabs

### 1.1 Проблема: 9 top-level tabs

**Существующие табы (6):** character / ship / reputation / contracts / inventory / quests

**Новые табы (4):** stats / clothing / modules / skills

**Итого:** 10 tabs (1 для character был расширен). При `flex-wrap: wrap` — оборачиваются на 2 ряда, съедают ~44px chrome.

### 1.2 Решение: nested sub-tabs

```
[ПЕРСОНАЖ] [КОРАБЛЬ] [РЕПУТАЦИЯ] [ПРОГРЕССИЯ▼] [КОНТРАКТЫ] [ИНВЕНТАРЬ] [КВЕСТЫ]
                                              ↓
                                              [Статы] [Одежда] [Модули] [Навыки]
```

**7 top-level табов** (один ряд) + **4 nested sub-tabs** (внутри progression-section).

**UX-альтернативы (отвергнуты):**

| Подход | Почему нет |
|--------|-----------|
| DropdownField "Раздел" | ломает существующий SwitchTab pattern |
| Sidebar меню (вертикальное) | ломает horizontal layout |
| Tabs overflow → scroll | хуже discoverability |
| Icon-only tabs | теряем readable labels |

---

## 2. UXML — структура

### 2.1 Top-level табы (добавляем 1)

```xml
<ui:VisualElement class="tabs">
  <ui:Button name="tab-character"    text="ПЕРСОНАЖ"     class="tab-btn" />
  <ui:Button name="tab-ship"         text="КОРАБЛЬ"      class="tab-btn" />
  <ui:Button name="tab-reputation"   text="РЕПУТАЦИЯ"    class="tab-btn" />
  <ui:Button name="tab-progression"  text="ПРОГРЕССИЯ"   class="tab-btn" />  <!-- NEW -->
  <ui:Button name="tab-contracts"    text="КОНТРАКТЫ"    class="tab-btn" />
  <ui:Button name="tab-inventory"    text="ИНВЕНТАРЬ"    class="tab-btn" />
  <ui:Button name="tab-quests"       text="КВЕСТЫ"       class="tab-btn" />
</ui:VisualElement>
```

### 2.2 Progression-section (parent для sub-tabs)

```xml
<ui:VisualElement name="progression-section" class="list-section" style="display: none;">
  <!-- Sub-tabs внутри (видены только когда activeTab == "progression") -->
  <ui:VisualElement name="progression-sub-tabs" class="sub-tabs" style="display: none;">
    <ui:Button name="tab-stats"    text="СТАТЫ"    class="sub-tab-btn" />
    <ui:Button name="tab-clothing" text="ОДЕЖДА"   class="sub-tab-btn" />
    <ui:Button name="tab-modules"  text="МОДУЛИ"   class="sub-tab-btn" />
    <ui:Button name="tab-skills"   text="НАВЫКИ"   class="sub-tab-btn" />
  </ui:VisualElement>

  <!-- Sub-section 1: Статы (Сила/Ловкость/Интеллект + прогресс) -->
  <ui:VisualElement name="stats-sub-section" class="list-sub-section" style="display: flex;">
    <ui:Label text="Характеристики" class="section-title" />
    <ui:VisualElement class="stats-grid">
      <!-- 3 stat-row-progress для STR/DEX/INT -->
      <ui:VisualElement class="stat-row stat-row-progress">
        <ui:Label text="Сила" class="stat-label" />
        <ui:VisualElement class="stat-progress-container">
          <ui:Label name="stat-str-tier" text="Тир 0" class="stat-tier-label" />
          <ui:VisualElement class="stat-progress-bar"><ui:VisualElement name="stat-str-fill" class="stat-progress-fill" /></ui:VisualElement>
          <ui:Label name="stat-str-value" text="0 / 100" class="stat-value" />
        </ui:VisualElement>
      </ui:VisualElement>
      <!-- Аналогично для DEXTERITY и INTELLIGENCE -->
      <ui:VisualElement class="stat-row stat-row-progress">
        <ui:Label text="Ловкость" class="stat-label" />
        <ui:VisualElement class="stat-progress-container">
          <ui:Label name="stat-dex-tier" text="Тир 0" class="stat-tier-label" />
          <ui:VisualElement class="stat-progress-bar"><ui:VisualElement name="stat-dex-fill" class="stat-progress-fill" /></ui:VisualElement>
          <ui:Label name="stat-dex-value" text="0 / 100" class="stat-value" />
        </ui:VisualElement>
      </ui:VisualElement>
      <ui:VisualElement class="stat-row stat-row-progress">
        <ui:Label text="Интеллект" class="stat-label" />
        <ui:VisualElement class="stat-progress-container">
          <ui:Label name="stat-int-tier" text="Тир 0" class="stat-tier-label" />
          <ui:VisualElement class="stat-progress-bar"><ui:VisualElement name="stat-int-fill" class="stat-progress-fill" /></ui:VisualElement>
          <ui:Label name="stat-int-value" text="0 / 100" class="stat-value" />
        </ui:VisualElement>
      </ui:VisualElement>
    </ui:VisualElement>
    <ui:Label text="Характеристики растут от действий: добыча руды (Сила), движение (Ловкость), диалоги/крафт (Интеллект)" class="placeholder-hint" />
  </ui:VisualElement>

  <!-- Sub-section 2: Одежда (экипированные слоты) -->
  <ui:VisualElement name="clothing-sub-section" class="list-sub-section" style="display: none;">
    <ui:Label text="Одежда" class="section-title" />
    <ui:ListView name="clothing-list" class="item-list" />
    <ui:Label text="Экипированные предметы дают бонусы к характеристикам" class="placeholder-hint" />
  </ui:VisualElement>

  <!-- Sub-section 3: Модули -->
  <ui:VisualElement name="modules-sub-section" class="list-sub-section" style="display: none;">
    <ui:Label text="Модули (импланты)" class="section-title" />
    <ui:ListView name="modules-list" class="item-list" />
    <ui:Label text="Модули — персональные импланты, дающие бонусы" class="placeholder-hint" />
  </ui:VisualElement>

  <!-- Sub-section 4: Навыки (Боевые + Социальные) -->
  <ui:VisualElement name="skills-sub-section" class="list-sub-section" style="display: none;">
    <ui:VisualElement class="quest-sub">
      <ui:Label text="Боевые навыки" class="quest-section-title" />
      <ui:ListView name="skills-combat-list" class="item-list quest-list" />
    </ui:VisualElement>
    <ui:VisualElement class="quest-sub">
      <ui:Label text="Социальные навыки" class="quest-section-title" />
      <ui:ListView name="skills-social-list" class="item-list quest-list" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:VisualElement>
```

**Изменения:** ~80 строк UXML, новый сектор для progression (parent) + 4 sub-sections + 4 sub-tab buttons.

---

## 3. USS — стили

### 3.1 Sub-tabs (внутри progression-section)

```css
.sub-tabs {
    flex-direction: row !important;
    flex-wrap: wrap !important;
    margin-bottom: 8px !important;
    padding: 4px !important;
    background-color: rgba(30, 45, 70, 0.3) !important;
    border-radius: 4px !important;
}
.sub-tab-btn {
    flex-grow: 1 !important;
    flex-basis: 0 !important;
    height: 24px !important;
    background-color: rgba(60, 80, 120, 0.4) !important;
    color: rgb(220, 220, 230) !important;
    border-width: 0 !important;
    border-bottom-width: 2px !important;
    border-color: transparent !important;
    -unity-font-style: bold !important;
    font-size: 11px !important;
}
.sub-tab-btn:hover { background-color: rgba(80, 100, 140, 0.6) !important; }
.sub-tab-btn.active { border-bottom-color: rgb(255, 220, 130) !important; }
```

### 3.2 Stat-row с прогрессом

```css
.stat-row-progress {
    flex-direction: row !important;
    align-items: center !important;
    justify-content: space-between !important;
    padding: 4px 6px !important;
}
.stat-row-progress .stat-label {
    width: 90px !important;
    flex-shrink: 0 !important;
}
.stat-progress-container {
    flex-grow: 1 !important;
    flex-direction: row !important;
    align-items: center !important;
    min-width: 0 !important;
}
.stat-progress-bar {
    flex-grow: 1 !important;
    height: 8px !important;
    background-color: rgba(40, 50, 70, 0.6) !important;
    border-radius: 2px !important;
    margin-right: 6px !important;
}
.stat-progress-fill {
    height: 100% !important;
    border-radius: 2px !important;
    transition-property: width !important;
    transition-duration: 0.3s !important;
}
.stat-progress-fill.tier-low    { background-color: rgba(180, 180, 200, 0.7) !important; }
.stat-progress-fill.tier-mid    { background-color: rgba(100, 180, 255, 0.8) !important; }
.stat-progress-fill.tier-high   { background-color: rgba(255, 200, 100, 0.9) !important; }
.stat-progress-fill.tier-master { background-color: rgba(255, 130, 200, 0.95) !important; }
.stat-tier-label {
    color: rgb(255, 220, 130) !important;
    font-size: 10px !important;
    -unity-font-style: bold !important;
    margin-right: 4px !important;
}
```

### 3.3 Skill rows

```css
.skill-row {
    flex-direction: column !important;
    padding: 6px 8px !important;
    border-bottom-width: 1px !important;
    border-bottom-color: rgba(80, 100, 130, 0.2) !important;
}
.skill-row-top {
    flex-direction: row !important;
    justify-content: space-between !important;
    align-items: center !important;
}
.skill-row-state {
    width: 90px !important;
    font-size: 10px !important;
    -unity-font-style: bold !important;
    color: rgb(180, 180, 200) !important;
}
.skill-row-title {
    flex-grow: 1 !important;
    font-size: 12px !important;
    -unity-font-style: bold !important;
    margin: 0 6px !important;
}
.skill-row-cost {
    width: 60px !important;
    font-size: 11px !important;
    -unity-text-align: middle-right !important;
    color: rgb(220, 200, 130) !important;
}
.skill-row-desc {
    font-size: 10px !important;
    color: rgb(180, 180, 200) !important;
    margin-top: 2px !important;
}
.skill-row-prereq {
    font-size: 10px !important;
    color: rgb(200, 180, 130) !important;
    margin-top: 2px !important;
    -unity-font-style: italic !important;
}
.skill-row-btn {
    height: 22px !important;
    margin-top: 4px !important;
    background-color: rgba(80, 160, 80, 0.7) !important;
    color: rgb(240, 240, 240) !important;
    border-width: 0 !important;
    border-radius: 3px !important;
}
.skill-row-btn:hover { scale: 1.05 1.05 !important; }
.skill-row-btn:disabled { background-color: rgba(80, 80, 80, 0.5) !important; opacity: 0.5 !important; }

/* Skill states */
.skill-row-locked { opacity: 0.5 !important; -unity-font-style: italic !important; }
.skill-row-locked .skill-row-state { color: rgb(180, 100, 100) !important; }
.skill-row-available { background-color: rgba(80, 200, 100, 0.10) !important; }
.skill-row-available .skill-row-state { color: rgb(120, 220, 120) !important; }
.skill-row-learned { background-color: rgba(120, 150, 200, 0.20) !important; }
.skill-row-learned .skill-row-state { color: rgb(150, 180, 220) !important; }
```

### 3.4 Equipment slots

```css
.equip-slot-row {
    flex-direction: row !important;
    align-items: center !important;
    height: 30px !important;
    padding: 0 8px !important;
    border-bottom-width: 1px !important;
    border-color: rgba(80, 100, 130, 0.2) !important;
}
.equip-slot-name {
    width: 100px !important;
    font-size: 12px !important;
    color: rgb(180, 200, 220) !important;
}
.equip-slot-item {
    flex-grow: 1 !important;
    font-size: 12px !important;
    -unity-font-style: bold !important;
}
.equip-slot-bonuses {
    width: 150px !important;
    font-size: 11px !important;
    color: rgb(220, 200, 130) !important;
    -unity-text-align: middle-right !important;
}
.equip-slot-btn {
    width: 80px !important;
    height: 22px !important;
    margin-left: 4px !important;
    background-color: rgba(120, 80, 80, 0.7) !important;
    color: rgb(240, 240, 240) !important;
    border-width: 0 !important;
    border-radius: 3px !important;
    font-size: 10px !important;
}
```

**Изменения:** ~120 строк USS, новые sub-tab стили + stat-row-progress + skill-row-* + equip-slot-*.

---

## 4. CharacterWindow.cs — расширение

### 4.1 Новые поля

```csharp
// Добавить рядом с существующими полями:

// Top-level "ПРОГРЕССИЯ" tab
private Button _tabProgression;
private VisualElement _progressionSection;

// Sub-tabs внутри progression
private VisualElement _progressionSubTabs;
private Button _tabStats, _tabClothing, _tabModules, _tabSkills;

// Sub-sections
private VisualElement _statsSubSection, _clothingSubSection, _modulesSubSection, _skillsSubSection;

// Stat labels (Сила/Ловкость/Интеллект)
private Label _statStrTier, _statStrValue;
private VisualElement _statStrFill;
private Label _statDexTier, _statDexValue;
private VisualElement _statDexFill;
private Label _statIntTier, _statIntValue;
private VisualElement _statIntFill;

// Equipment / Skills ListView
private ListView _clothingList, _modulesList, _skillsCombatList, _skillsSocialList;

// Sub-tab state
private string _activeProgressionTab = "stats";  // "stats" | "clothing" | "modules" | "skills"

// Client states (sub-pattern как 5 существующих)
private StatsClientState _statsState;
private bool _isStatsSubscribed = false;
private EquipmentClientState _equipmentState;
private bool _isEquipmentSubscribed = false;
private SkillsClientState _skillsState;
private bool _isSkillsSubscribed = false;

// Caches
private List<EquipSlotRow> _clothingCache = new();
private List<EquipSlotRow> _modulesCache = new();
private List<SkillRow> _skillsCombatCache = new();
private List<SkillRow> _skillsSocialCache = new();

private struct EquipSlotRow {
    public EquipSlot Slot;
    public int ItemId;
    public string DisplayName;
    public string BonusesText;
}

private struct SkillRow {
    public string SkillId;
    public string DisplayName;
    public string Description;
    public SkillCategory Category;
    public SkillState State;  // Locked / Available / Learned
    public float XpCost;
    public int RequiredTier;
    public string[] Prerequisites;
}
```

### 4.2 EnsureBuilt — расширение

```csharp
private void EnsureBuilt() {
    if (_doc.rootVisualElement == null) return;
    if (_built) return;

    // ... existing setup (lines 397-630) ...

    // === NEW: Find progression elements ===
    _tabProgression = _root.Q<Button>("tab-progression");
    _progressionSection = _root.Q<VisualElement>("progression-section");
    _progressionSubTabs = _root.Q<VisualElement>("progression-sub-tabs");
    _tabStats = _root.Q<Button>("tab-stats");
    _tabClothing = _root.Q<Button>("tab-clothing");
    _tabModules = _root.Q<Button>("tab-modules");
    _tabSkills = _root.Q<Button>("tab-skills");
    _statsSubSection = _root.Q<VisualElement>("stats-sub-section");
    _clothingSubSection = _root.Q<VisualElement>("clothing-sub-section");
    _modulesSubSection = _root.Q<VisualElement>("modules-sub-section");
    _skillsSubSection = _root.Q<VisualElement>("skills-sub-section");

    // Stat elements
    _statStrTier = _root.Q<Label>("stat-str-tier");
    _statStrValue = _root.Q<Label>("stat-str-value");
    _statStrFill = _root.Q<VisualElement>("stat-str-fill");
    _statDexTier = _root.Q<Label>("stat-dex-tier");
    _statDexValue = _root.Q<Label>("stat-dex-value");
    _statDexFill = _root.Q<VisualElement>("stat-dex-fill");
    _statIntTier = _root.Q<Label>("stat-int-tier");
    _statIntValue = _root.Q<Label>("stat-int-value");
    _statIntFill = _root.Q<VisualElement>("stat-int-fill");

    // ListView
    _clothingList = _root.Q<ListView>("clothing-list");
    _modulesList = _root.Q<ListView>("modules-list");
    _skillsCombatList = _root.Q<ListView>("skills-combat-list");
    _skillsSocialList = _root.Q<ListView>("skills-social-list");

    // === NEW: Wire top-level tab ===
    if (_tabProgression != null) _tabProgression.clicked += () => SwitchTab("progression");

    // === NEW: Wire sub-tabs ===
    if (_tabStats != null) _tabStats.clicked += () => SwitchProgressionTab("stats");
    if (_tabClothing != null) _tabClothing.clicked += () => SwitchProgressionTab("clothing");
    if (_tabModules != null) _tabModules.clicked += () => SwitchProgressionTab("modules");
    if (_tabSkills != null) _tabSkills.clicked += () => SwitchProgressionTab("skills");

    // === NEW: ListView factories ===
    if (_clothingList != null) {
        _clothingList.fixedItemHeight = 32;
        _clothingList.makeItem = MakeEquipmentRow;
        _clothingList.bindItem = BindClothingRow;
    }
    if (_modulesList != null) {
        _modulesList.fixedItemHeight = 32;
        _modulesList.makeItem = MakeEquipmentRow;
        _modulesList.bindItem = BindModuleRow;
    }
    if (_skillsCombatList != null) {
        _skillsCombatList.fixedItemHeight = 80;
        _skillsCombatList.makeItem = MakeSkillRow;
        _skillsCombatList.bindItem = (row, idx) => BindSkillRow(row, idx, _skillsCombatCache);
    }
    if (_skillsSocialList != null) {
        _skillsSocialList.fixedItemHeight = 80;
        _skillsSocialList.makeItem = MakeSkillRow;
        _skillsSocialList.bindItem = (row, idx) => BindSkillRow(row, idx, _skillsSocialCache);
    }

    // === NEW: Subscribe to client states ===
    SubscribeStats();
    SubscribeEquipment();
    SubscribeSkills();

    // ... existing setup (SwitchTab + SetVisible + MarkDirtyRepaint) ...
}
```

### 4.3 SwitchTab — расширение

```csharp
private void SwitchTab(string tab) {
    _activeTab = tab;
    bool isCharacter    = tab == "character";
    bool isShip         = tab == "ship";
    bool isReputation   = tab == "reputation";
    bool isContracts    = tab == "contracts";
    bool isInventory    = tab == "inventory";
    bool isQuests       = tab == "quests";
    bool isProgression  = tab == "progression";  // NEW

    // === Existing sections ===
    if (_characterSection != null)    _characterSection.style.display    = isCharacter    ? DisplayStyle.Flex : DisplayStyle.None;
    if (_shipSection != null)         _shipSection.style.display         = isShip         ? DisplayStyle.Flex : DisplayStyle.None;
    if (_reputationSection != null)   _reputationSection.style.display   = isReputation   ? DisplayStyle.Flex : DisplayStyle.None;
    if (_contractsSection != null)    _contractsSection.style.display    = isContracts    ? DisplayStyle.Flex : DisplayStyle.None;
    if (_inventorySection != null)    _inventorySection.style.display    = isInventory    ? DisplayStyle.Flex : DisplayStyle.None;
    if (_questsSection != null)       _questsSection.style.display       = isQuests       ? DisplayStyle.Flex : DisplayStyle.None;

    // === NEW: progression ===
    if (_progressionSection != null)  _progressionSection.style.display  = isProgression  ? DisplayStyle.Flex : DisplayStyle.None;
    if (_progressionSubTabs != null)  _progressionSubTabs.style.display  = isProgression  ? DisplayStyle.Flex : DisplayStyle.None;
    if (isProgression) SwitchProgressionTab(_activeProgressionTab);

    // ... existing filters + actions + tab highlighting ...
}
```

### 4.4 SwitchProgressionTab — новый метод

```csharp
private void SwitchProgressionTab(string tab) {
    _activeProgressionTab = tab;
    bool isStats    = tab == "stats";
    bool isClothing = tab == "clothing";
    bool isModules  = tab == "modules";
    bool isSkills   = tab == "skills";

    if (_statsSubSection != null)    _statsSubSection.style.display    = isStats    ? DisplayStyle.Flex : DisplayStyle.None;
    if (_clothingSubSection != null) _clothingSubSection.style.display = isClothing ? DisplayStyle.Flex : DisplayStyle.None;
    if (_modulesSubSection != null)  _modulesSubSection.style.display  = isModules  ? DisplayStyle.Flex : DisplayStyle.None;
    if (_skillsSubSection != null)   _skillsSubSection.style.display   = isSkills   ? DisplayStyle.Flex : DisplayStyle.None;

    // Sub-tab highlighting (class "active")
    SetSubTabActive(_tabStats, isStats);
    SetSubTabActive(_tabClothing, isClothing);
    SetSubTabActive(_tabModules, isModules);
    SetSubTabActive(_tabSkills, isSkills);

    // Refresh content
    if (isStats) RefreshStatsDisplay();
    if (isClothing) RebuildClothingListView();
    if (isModules) RebuildModulesListView();
    if (isSkills) RebuildSkillsListView();
}

private void SetSubTabActive(Button btn, bool active) {
    if (btn == null) return;
    if (active) btn.AddToClassList("active");
    else btn.RemoveFromClassList("active");
}
```

### 4.5 Subscribe/Unsubscribe — 3 новых client states

```csharp
private void SubscribeStats() {
    if (_isStatsSubscribed) return;
    _statsState = StatsClientState.Instance;
    if (_statsState == null) return;
    _statsState.OnStatsUpdated += HandleStatsSnapshot;
    _isStatsSubscribed = true;
    Debug.Log("[CharacterWindow] Subscribed to StatsClientState");
}

private void UnsubscribeStats() {
    if (!_isStatsSubscribed) return;
    var state = StatsClientState.Instance;
    if (state == null) { _isStatsSubscribed = false; return; }
    state.OnStatsUpdated -= HandleStatsSnapshot;
    _isStatsSubscribed = false;
}

// ... аналогично SubscribeEquipment/UnsubscribeEquipment, SubscribeSkills/UnsubscribeSkills
```

### 4.6 Handlers — Refresh*Cache + Rebuild*ListView

```csharp
private void HandleStatsSnapshot(StatsSnapshotDto snap) {
    _lastStatsSnapshot = snap;
    if (_activeTab == "progression" && _activeProgressionTab == "stats") {
        RefreshStatsDisplay();
    }
}

private void RefreshStatsDisplay() {
    if (_lastStatsSnapshot == null) return;
    var snap = _lastStatsSnapshot.Value;

    // Strength
    _statStrTier.text = $"Тир {snap.strengthTier}";
    float strPct = snap.strengthXpForNextTier > 0 ? Mathf.Clamp01(snap.strength / snap.strengthXpForNextTier) * 100f : 0f;
    _statStrFill.style.width = new Length(strPct, LengthUnit.Percent);
    _statStrValue.text = $"{snap.strength:F0} / {snap.strengthXpForNextTier:F0}";
    UpdateTierClass(_statStrFill, snap.strengthTier);

    // Dexterity, Intelligence — аналогично
}

private void UpdateTierClass(VisualElement fill, int tier) {
    fill.RemoveFromClassList("tier-low");
    fill.RemoveFromClassList("tier-mid");
    fill.RemoveFromClassList("tier-high");
    fill.RemoveFromClassList("tier-master");
    if (tier >= 15) fill.AddToClassList("tier-master");
    else if (tier >= 10) fill.AddToClassList("tier-high");
    else if (tier >= 5) fill.AddToClassList("tier-mid");
    else fill.AddToClassList("tier-low");
}

private void HandleEquipmentSnapshot(EquipmentSnapshotDto snap) {
    RefreshEquipmentCache(snap);
    if (_activeTab == "progression" && (_activeProgressionTab == "clothing" || _activeProgressionTab == "modules")) {
        RebuildClothingListView();
        RebuildModulesListView();
    }
}

private void RefreshEquipmentCache(EquipmentSnapshotDto snap) {
    _clothingCache.Clear();
    _modulesCache.Clear();
    foreach (var slot in snap.equip.EnumerateOccupiedSlots()) {
        int idx = EquipmentData.SlotToIndex(slot);
        int itemId = snap.equip.slotItemIds[idx];
        if (!InventoryWorld.Instance.GetItemDataById(itemId, out var itemData)) continue;
        string displayName = itemData.itemName;
        string bonuses = BuildBonusesText(itemData);

        var row = new EquipSlotRow { Slot = slot, ItemId = itemId, DisplayName = displayName, BonusesText = bonuses };
        if (slot >= EquipSlot.Module1) _modulesCache.Add(row);
        else _clothingCache.Add(row);
    }
}

private string BuildBonusesText(ItemData itemData) {
    var sb = new System.Text.StringBuilder();
    if (itemData is ClothingItemData clothing) {
        if (clothing.strengthBonus != 0) sb.Append($"+{clothing.strengthBonus:F0} STR ");
        if (clothing.dexterityBonus != 0) sb.Append($"+{clothing.dexterityBonus:F0} DEX ");
        if (clothing.intelligenceBonus != 0) sb.Append($"+{clothing.intelligenceBonus:F0} INT ");
    } else if (itemData is ModuleItemData module) {
        if (module.sensorRangeBonus != 0) sb.Append($"+{module.sensorRangeBonus:F0} Sensor ");
        if (module.craftingSpeedMultiplier > 0) sb.Append($"×{1f + module.craftingSpeedMultiplier:F2} Craft ");
        // ...
    }
    return sb.ToString().TrimEnd();
}
```

### 4.7 Row factories — MakeXxx/BindXxx (по pattern существующих)

```csharp
private VisualElement MakeEquipmentRow() {
    var row = new VisualElement();
    row.AddToClassList("equip-slot-row");
    var slot = new Label { name = "equip-slot" };
    slot.AddToClassList("equip-slot-name");
    var item = new Label { name = "equip-item" };
    item.AddToClassList("equip-slot-item");
    var bonuses = new Label { name = "equip-bonuses" };
    bonuses.AddToClassList("equip-slot-bonuses");
    var btn = new Button { name = "equip-unequip-btn", text = "СНЯТЬ" };
    btn.AddToClassList("equip-slot-btn");
    row.Add(slot);
    row.Add(item);
    row.Add(bonuses);
    row.Add(btn);
    return row;
}

private void BindClothingRow(VisualElement row, int index) {
    if (index >= _clothingCache.Count) return;
    var entry = _clothingCache[index];
    row.Q<Label>("equip-slot").text = entry.Slot.ToString();
    row.Q<Label>("equip-item").text = entry.DisplayName;
    row.Q<Label>("equip-bonuses").text = entry.BonusesText;
    var btn = row.Q<Button>("equip-unequip-btn");
    btn.clicked -= null;
    btn.clicked += () => EquipmentClientState.Instance?.RequestUnequip(entry.Slot);
}

private void BindModuleRow(VisualElement row, int index) {
    if (index >= _modulesCache.Count) return;
    var entry = _modulesCache[index];
    // ... аналогично BindClothingRow
}

private VisualElement MakeSkillRow() {
    // (см. SkillTree.md §4.3)
}

private void BindSkillRow(VisualElement row, int index, List<SkillRow> cache) {
    if (index >= cache.Count) return;
    var skill = cache[index];
    // ... bind state, title, cost, desc, prereq, button
}
```

---

## 5. NetworkPlayer — новые Target RPCs

### 5.1 Receive*TargetRpc методы

```csharp
// В NetworkPlayer.cs, добавить рядом с существующими:

[Rpc(SendTo.Owner)]
public void ReceiveStatsSnapshotTargetRpc(StatsSnapshotDto snap, RpcParams rpcParams = default)
    => StatsClientState.Instance?.OnStatsSnapshotReceived(snap);

[Rpc(SendTo.Owner)]
public void ReceiveEquipmentSnapshotTargetRpc(EquipmentSnapshotDto snap, RpcParams rpcParams = default)
    => EquipmentClientState.Instance?.OnEquipmentSnapshotReceived(snap);

[Rpc(SendTo.Owner)]
public void ReceiveSkillsSnapshotTargetRpc(SkillsSnapshotDto snap, RpcParams rpcParams = default)
    => SkillsClientState.Instance?.OnSkillsSnapshotReceived(snap);

[Rpc(SendTo.Owner)]
public void ReceiveEquipResultTargetRpc(EquipResultDto result, RpcParams rpcParams = default)
    => EquipmentClientState.Instance?.OnEquipResultReceived(result);

[Rpc(SendTo.Owner)]
public void ReceiveSkillResultTargetRpc(SkillResultDto result, RpcParams rpcParams = default)
    => SkillsClientState.Instance?.OnSkillResultReceived(result);
```

---

## 6. BootstrapScene — новые GameObjects

### 6.1 [StatsServer] GameObject

```
[StatsServer] (new)
├── NetworkObject (scene-placed)
└── StatsServer (NetworkBehaviour)
    └── StatsConfig (SerializeField) → Resources/Stats/StatsConfig_Default.asset
```

### 6.2 [EquipmentServer] GameObject

```
[EquipmentServer] (new)
├── NetworkObject (scene-placed)
└── EquipmentServer (NetworkBehaviour)
```

### 6.3 [SkillsServer] GameObject

```
[SkillsServer] (new)
├── NetworkObject (scene-placed)
└── SkillsServer (NetworkBehaviour)
    └── SkillsConfig (SerializeField) → Resources/Skills/SkillsConfig_Default.asset
```

### 6.4 ClientState singletons (auto-spawn)

**В NetworkManagerController.Awake**, добавить:

```csharp
private void CreateStatsClientState() {
    if (StatsClientState.Instance != null) return;
    var go = new GameObject("[StatsClientState]");
    go.AddComponent<StatsClientState>();
    DontDestroyOnLoad(go);
}
// + CreateEquipmentClientState, CreateSkillsClientState
```

**Эти GameObjects создаются runtime**, не требуют scene-placement.

---

## 7. UI Toolkit 4 FIX'а — применены автоматически

Все 4 FIX'а уже работают в `CharacterWindow.cs`:
- `pickingMode Ignore/Position` (line 418 init, 1991 Show, 2014 Hide)
- `ApplyInlineFallbackStyles()` (line 2036)
- `Cursor.lockState` (line 2041-2051)
- `MarkDirtyRepaint + schedule.Execute StartingIn(50)` (line 624-627, 2004-2008)

**Новые секции наследуют поведение** через `CloneTree()` + `EnsureBuilt()`. Дополнительные FIX'ы не нужны.

**Но для новых ListView обязательно:**
- `MarkDirtyRepaint()` после `display: flex` (pitfall R3-005)
- `fixedItemHeight = 80` для skills (pitfall R3-005b)
- `if (!ReferenceEquals(itemsSource, cache))` trick (pitfall R3-005c)

---

## 8. Edge cases

### 8.1 CharacterWindow открыт на progression/stats, stats snapshot приходит

**Сценарий:** игрок на табе progression/stats, StatsServer присылает snapshot → `HandleStatsSnapshot` → `RefreshStatsDisplay`.

**Решение:** Handler проверяет `_activeTab == "progression" && _activeProgressionTab == "stats"`. Если не — кэширует snapshot, отображает при следующем `SwitchProgressionTab("stats")`.

### 8.2 Player equips item → stats recompute → UI desync

**Сценарий:** Equip → StatsServer.RecomputeAndSendSnapshot → effective STR изменился → UI показывает базовую (без бонуса).

**Решение:** `StatsSnapshotDto.effectiveStrength` (computed в StatsServer после equip). UI показывает effective stat.

### 8.3 SkillsConfig.defaultSkills = empty array

**Сценарий:** Default skills пустой. Player connect → `GrantDefaultSkills` → ничего.

**Решение:** Acceptable — игрок будет учить все skills с нуля. Если дизайнер хочет стартовые навыки — добавляет в SkillsConfig.defaultSkills.

### 8.4 SkillsConfig.defaultSkills содержит skill с XP cost > 0

**Сценарий:** `SkillsConfig.defaultSkills = [HeavySwing]` (XP cost 100). `GrantDefaultSkills` добавляет в learned, минуя XP check.

**Решение:** В `GrantDefaultSkills` — warning если default skill имеет XP cost > 0. Это misuse — default skills должны быть бесплатными.

### 8.5 Skill prerequisites содержат skill не из Resources

**Сценарий:** Designer создал Skill_X с prereq = Skill_Y (не существует в Resources).

**Решение:** `OnValidate` SO — warning если prereq.skillId is null/empty или prereq asset == null. Runtime — пропускаем null entries (`if (prereq != null && !learned.Contains(prereq.skillId))`).

### 8.6 CharacterWindow 9 tabs (вдруг добавим ещё)

**Сценарий:** В будущем добавится ещё таб → 8 top-level → wrap.

**Решение:** Использовать `.tabs { overflow-x: auto; }` (Phase 2). Или перейти на sidebar (Phase 2). MVP — 7 top-level OK.

### 8.7 StatsClientState.OnStatsUpdated fire spam (tier-up +5)

**Сценарий:** Большой XP gain → 5 tier promotions → 5 StatTierUpEvent → UI tier-up notification spam.

**Решение:** Throttle StatTierUpEvent (200ms minimum between notifications) + queue для остальных.

### 8.8 EquipmentSnapshot sync timing vs Stats recompute

**Сценарий:** EquipmentServer отправляет EquipmentSnapshot, StatsServer отправляет StatsSnapshot — порядок не гарантирован.

**Решение:** UI читает оба snapshot. Если Stats ещё не пришёл — показывает "—". Когда приходит — обновляется. Acceptable для MVP.

---

## 9. Pitfalls

### 9.1 UXML element name collision

**Проблема:** Новый `_statStrValue` (Label) vs существующий `_statCredits` (Label) — оба `.stat-value` class. Если использовать `Q<Label>("stat-str-value")` — OK (Q by name). Но если в USS collision через class — может перебить стили.

**Решение:** Используем уникальные `name` атрибуты в UXML (`stat-str-value`, `stat-dex-value`, `stat-int-value`). USS classes общие (`.stat-value`) — нет collision.

### 9.2 ListView itemsSource sync race

**Проблема:** `_clothingList.itemsSource = _clothingCache; _clothingList.RefreshItems()` — если `itemsSource` reference == старый reference, RefreshItems не сработает.

**Решение:** Используем `if (!ReferenceEquals(_clothingList.itemsSource, _clothingCache)) { _clothingList.itemsSource = _clothingCache; _clothingList.RefreshItems(); }` (pitfall R3-005c).

### 9.3 Switching tab → ListView first display = empty

**Проблема:** Первый раз `display: none → flex` для ListView — rows не отрисовываются до следующего toggle.

**Решение:** После `display = Flex` → `_listView.MarkDirtyRepaint()` (pitfall R3-005).

### 9.4 Sub-tab "active" class collision

**Проблема:** Top-level tab `.active` border-color vs sub-tab `.active` border-color. Если оба используют `.active` class — visual conflict.

**Решение:** Sub-tab использует `.sub-tab-btn.active`, top-level — `.tab-btn.active` (уникальные selectors).

### 9.5 Fixed-size ListView row count vs cache size mismatch

**Проблема:** `fixedItemHeight = 80` для skill rows, но в cache 5 skills. BindItem вызывается 5 раз.

**Решение:** В `BindItem` проверка `if (index >= cache.Count) return` — safety.

### 9.6 SubscribeX lazy-subscribe в Update — overhead

**Проблема:** 3 новых SubscribeX в Update (lazy-subscribe pattern) — 3 проверки каждый кадр.

**Решение:** Минимальный overhead — `if (!_isXSubscribed && XClientState.Instance != null)`. После первой подписки — флаг = true, проверка skip.

### 9.7 Tab highlighting (active class) не сбрасывается при SwitchTab

**Проблема:** После `SwitchTab("inventory")` → tab-character.active остаётся.

**Решение:** В `SwitchTab` → сбрасываем все `.tab-btn.active` → добавляем только текущему. Pattern уже есть в существующем коде (line 636-706), повторяем для sub-tabs.

### 9.8 NetworkManager.Singleton.ConnectedClients — playerObject may be null during disconnect

**Проблема:** Player disconnecting → playerObject = null → SendSnapshotToOwner NRE.

**Решение:** `if (playerObject == null) return` в каждом send method. Already handled в существующих серверах — copy pattern.

---

## 10. Что НЕ делаем

- ❌ Не создаём новые окна (`CharacterProgressionWindow.cs`, `SkillTreeWindow.cs`) — всё внутри CharacterWindow
- ❌ Не используем `UnityEditor.Experimental.GraphView` в runtime
- ❌ Не делаем Painter2D skill tree в MVP (Phase 2)
- ❌ Не делаем drag-and-drop для equip (кнопки)
- ❌ Не делаем tier-up notification visual feedback (MVP — просто обновить progress bar)
- ❌ Не делаем StatsServer.RecomputeAndSendSnapshot как Periodic (only on-change)
- ❌ Не пишем `.meta` / `.asmdef` файлы
- ❌ Не делаем tier-downgrade при XP spend (clamp at 0)
- ❌ Не делаем filtering by stat name в equipment (filter по slot, не stat)
