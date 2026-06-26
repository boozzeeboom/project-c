# SkillTreeWindow — Design & Implementation Plan

> **Дата:** 2026-06-26 (сессия #4)
> **Подсистема:** Character Progression → Skill Tree UI (overlay)
> **База:** `docs/Character/Skills/AUDIT_2026-06-26_CURRENT_STATE_AND_NEXT_STEPS.md`, `docs/Character/Skills/Battle/50_UI_DESIGN_PLAN.md`
> **Проблема:** текущий CharacterWindow → "Боевые навыки" — перегружен и нефункционален: узлы в маленькой колонке 40%, prereq-строки и кнопки [Изучить]/[Забыть] не помещаются, фильтры визуально есть но семантика неочевидна. Игрок не может понять что даёт навык и зачем его учить.
> **Решение:** новый полноэкранный overlay `SkillTreeWindow` с нормальным layout, поиском, фильтрами и детальной панелью. В CharacterWindow оставляем только LEARNED-список + кнопку `[ИЗУЧИТЬ НАВЫК]`.
> **Scope:** design-doc (этот файл) + кодовая реализация + USS-стили.

---

## 1. Архитектура (высокоуровневая)

```
[CharacterWindow] (правки)              [SkillTreeWindow] (новый)
├── P→ПЕРСОНАЖ:                         ├── Оверлей 720x500 (открывается по
│   ├── Одежда | Модули                  │   [ИЗУЧИТЬ НАВЫК] из CharacterWindow
│   ├── Харатеристики | Боевые | Социал │   или по Esc)
│   │                  ↑                 │
│   │  LEARNED-список                  ├── Top: 6 chip-фильтров [Все][Melee]
│   │  (только изученные, компактный)   │   [Ranged][Explosives][Antigrav][Defense]
│   │                                  │   + [ПОИСК] text field
│   └── [ИЗУЧИТЬ НАВЫК] кнопка ────────► ├── Left: ScrollView (skill list)
│                                        │   Каждый row: badge + name + tier
│                                        │   + [Изучить]/[Забыть] кнопка
│                                        ├── Right: detail panel
│                                        │   - name, описание, effects [+STR+2×1.15]
│                                        │   - cost, INT tier req
│                                        │   - prereq (кого нужно изучить)
│                                        │   - dependents (что разблокирует)
│                                        └── [ЗАКРЫТЬ] кнопка (низ)
```

**Поток:**
1. P → CharacterWindow → tab "ПЕРСОНАЖ" → секция "Боевые навыки"
2. Видим только LEARNED навыки (компактный список, без кнопок)
3. Клик `[ИЗУЧИТЬ НАВЫК]` → открывается SkillTreeWindow
4. В SkillTreeWindow: фильтр → выбор навыка → детали справа → `[Изучить]`
5. Esc или `[ЗАКРЫТЬ]` → возврат в CharacterWindow

---

## 2. План реализации (12 шагов)

| # | Шаг | Файлы | Что | Сложность |
|---|---|---|---|---|
| 1 | **SkillTreeWindow.cs (skeleton)** | новый `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs` | Класс-обёртка над UIDocument. `Instance`, `Show()/Hide()` с 4 фиксами по UI_TOOLKIT_GUIDE | ~45м |
| 2 | **SkillTreeWindow.uxml** | новый `Assets/_Project/Resources/UI/SkillTreeWindow.uxml` | Layout: top (chips+search) + middle (list+detail, flex 1:1) + bottom (close) | ~30м |
| 3 | **SkillTreePanelSettings.asset** | новый в `Resources/UI/` | Копия существующего PanelSettings (sortingOrder=300) | ~10м |
| 4 | **SkillTreeWindow.uss** | новый `Resources/UI/SkillTreeWindow.uss` | Стили (всё `!important`): chip, search, list-row, detail-panel, close-btn | ~30м |
| 5 | **NetworkManagerController auto-spawn** | `NetworkManagerController.cs` | `CreateSkillTreeWindow()` в Awake (idempotent) | ~10м |
| 6 | **BootstrapScene placement** | `Assets/_Project/Scenes/BootstrapScene.unity` | GameObject `[SkillTreeWindow]` с UIDocument + panelSettings + uxml/uss bindings | ~10м (MCP) |
| 7 | **CharacterWindow: добавить кнопку [ИЗУЧИТЬ НАВЫК]** | `CharacterWindow.cs` + `.uxml` + `.uss` | В combat-блоке, под chip-row, открывает SkillTreeWindow | ~15м |
| 8 | **CharacterWindow: очистить skill-list** | `CharacterWindow.cs` | Оставить ТОЛЬКО LEARNED (filter `_skillsCombatCache.Where(s => s.State=="LEARNED")`). Без кнопок, без prereq, без chip-row | ~20м |
| 9 | **SkillTreeWindow: инициализация чипов + поиска** | `SkillTreeWindow.cs` | `InitFilterChips()` + `InitSearchField()` | ~20м |
| 10 | **SkillTreeWindow: список всех навыков** | `SkillTreeWindow.cs` | `RefreshAllSkillsList()` — копирует из `SkillsClientState.CurrentSnapshot` + локальные `SkillNodeConfig` assets | ~30м |
| 11 | **SkillTreeWindow: детальная панель** | `SkillTreeWindow.cs` | `OnSkillSelected(skillId)` → показывает name/desc/effects/cost/prereq/dependents | ~30м |
| 12 | **SkillTreeWindow: Изучить/Забыть** | `SkillTreeWindow.cs` | `OnLearnClicked(skillId)`, `OnForgetClicked(skillId)` — reflection-RPC как в CharacterWindow | ~15м |
| 13 | **Compile + verify** | — | `refresh_unity scope=all` + `read_console` | ~5м |
| 14 | **Документ changelog** | `docs/dev/SKILLS_NEXT_STEPS_T-CB_LOG.md` | Запись сессии #4 | ~10м |

**Итого: ~4-5ч. Каждый шаг compile отдельно.**

---

## 3. UI-структура (SkillTreeWindow)

### 3.1 UXML layout (skeleton)

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <!-- ROOT — full-screen overlay, скрыт по умолчанию (display: none inline) -->
  <ui:VisualElement name="skill-tree-root" class="stw-root">

    <!-- TOP: filter chips + search field -->
    <ui:VisualElement class="stw-top">
      <ui:Label text="Дерево навыков" class="stw-title" />
      <ui:VisualElement class="stw-chip-row">
        <ui:Label name="chip-all"        text="Все"       class="stw-chip stw-chip-active" />
        <ui:Label name="chip-melee"      text="⚔ Melee"    class="stw-chip" />
        <ui:Label name="chip-ranged"     text="🏹 Ranged"   class="stw-chip" />
        <ui:Label name="chip-explosives" text="💣 Explosives" class="stw-chip" />
        <ui:Label name="chip-antigrav"   text="🌌 Antigrav" class="stw-chip" />
        <ui:Label name="chip-defense"    text="🛡 Defense"  class="stw-chip" />
      </ui:VisualElement>
      <ui:TextField name="skill-search" placeholder-text="Поиск по имени или эффекту..." class="stw-search" />
    </ui:VisualElement>

    <!-- MIDDLE: list (left 40%) + detail (right 60%) -->
    <ui:VisualElement class="stw-middle">

      <!-- LEFT: list of skills -->
      <ui:VisualElement class="stw-list-col">
        <ui:Label text="Доступные навыки" class="stw-section-title" />
        <ui:ScrollView name="skill-list-scroll" class="stw-scroll">
          <ui:VisualElement name="skill-list-container" class="stw-list-container" />
        </ui:ScrollView>
      </ui:VisualElement>

      <!-- RIGHT: detail panel -->
      <ui:VisualElement class="stw-detail-col">
        <ui:Label name="detail-name" text="Выберите навык" class="stw-detail-name" />
        <ui:Label name="detail-desc" text="..." class="stw-detail-desc" />
        <ui:VisualElement class="stw-detail-stats">
          <ui:Label name="detail-effects" text="" class="stw-detail-effects" />
          <ui:Label name="detail-cost" text="" class="stw-detail-cost" />
          <ui:Label name="detail-tier" text="" class="stw-detail-tier" />
        </ui:VisualElement>
        <ui:VisualElement class="stw-detail-prereq">
          <ui:Label text="Требуется:" class="stw-detail-prereq-title" />
          <ui:VisualElement name="detail-prereq-container" class="stw-detail-prereq-container" />
        </ui:VisualElement>
        <ui:VisualElement class="stw-detail-deps">
          <ui:Label text="Откроет:" class="stw-detail-deps-title" />
          <ui:VisualElement name="detail-deps-container" class="stw-detail-deps-container" />
        </ui:VisualElement>
        <ui:VisualElement class="stw-detail-actions">
          <ui:Label name="btn-learn" text="Изучить" class="stw-btn stw-btn-learn" />
          <ui:Label name="btn-forget" text="Забыть" class="stw-btn stw-btn-forget" />
        </ui:VisualElement>
      </ui:VisualElement>
    </ui:VisualElement>

    <!-- BOTTOM: close -->
    <ui:VisualElement class="stw-bottom">
      <ui:Label name="btn-close" text="Закрыть (Esc)" class="stw-btn stw-btn-close" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

### 3.2 Layout split (visual)

```
┌────────────────────────────────────────────────────────────────────┐
│  Дерево навыков                                       [Закрыть X]    │  ← top
│  [Все] [⚔ Melee] [🏹 Ranged] [💣 Explosives] [🌌 Antigrav] [🛡 Defense]
│  [Поиск по имени или эффекту: _____________________________]
├────────────────────────────────────────────────────────────────────┤
│ ┌──────────────┬─────────────────────────┐
│ │              │ Базовый удар (BasicStrike)│
│ │ Доступные    │ ─────────────────────────│
│ │ навыки:      │ STR+2                    │
│ │              │ Стоимость: Free          │
│ │ ✓ BasicStrike│ Тир: T0                  │
│ │ ○ GreatSword │ ─────────────────────────│
│ │ ✕ MasterSword│ Требуется: (ничего)      │
│ │ ✓ DodgeRoll  │ Откроет: HeavySwing      │
│ │ ...          │ ─────────────────────────│
│ │              │ [Изучить] [Забыть]       │
│ └──────────────┴─────────────────────────┘
└────────────────────────────────────────────────────────────────────┘
```

### 3.3 Кнопки (UXML — VisualElement + ClickEvent, не Button!)

По UI_TOOLKIT_GUIDE §0a.4: для тоггл-элементов `display: none` / `flex` управляется inline C#, а не USS `!important`. Кнопки `Изучить`/`Забыть` — visibility переключается по `data.State`:

```csharp
private void OnSkillSelected(string skillId) {
    // ...
    if (data.State == "AVAILABLE") {
        _btnLearn.style.display = DisplayStyle.Flex;
        _btnForget.style.display = DisplayStyle.None;
    } else if (data.State == "LEARNED") {
        _btnLearn.style.display = DisplayStyle.None;
        _btnForget.style.display = DisplayStyle.Flex;
    } else { // LOCKED
        _btnLearn.style.display = DisplayStyle.None;
        _btnForget.style.display = DisplayStyle.None;
    }
}
```

**КРИТИЧНО (UI_TOOLKIT_GUIDE §0a.4c):** НЕЛЬЗЯ ставить `display: none !important` в USS — это блокирует inline toggle.

---

## 4. Скелет SkillTreeWindow.cs (с учётом 4 фиксов из UI_TOOLKIT_GUIDE)

```csharp
[RequireComponent(typeof(UIDocument))]
public class SkillTreeWindow : MonoBehaviour {
    public static SkillTreeWindow Instance { get; private set; }

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _rootContainer;  // outer full-screen
    private bool _built = false;

    // === FIX 1: pickingMode = Ignore when closed (UI_TOOLKIT_GUIDE) ===
    private void OnEnable() {
        _doc = GetComponent<UIDocument>();
        EnsureBuilt();
    }

    public void EnsureBuilt() {
        if (_built) return;
        if (_doc == null) _doc = GetComponent<UIDocument>();
        if (_doc == null || _doc.rootVisualElement == null) return;

        var uxml = Resources.Load<VisualTreeAsset>("UI/SkillTreeWindow");
        var uss = Resources.Load<StyleSheet>("UI/SkillTreeWindow");
        if (uxml == null) { Debug.LogError("[SkillTreeWindow] UXML not found"); return; }

        // FIX 4: CloneTree (UIDocument.OnEnable may overwrite _doc.visualTreeAsset)
        _doc.rootVisualElement.Clear();
        if (uss != null) _doc.rootVisualElement.styleSheets.Add(uss);
        _rootContainer = uxml.CloneTree();
        _doc.rootVisualElement.Add(_rootContainer);

        // Cache refs
        _root = _rootContainer.Q<VisualElement>("skill-tree-root");
        ApplyInlineFallbackStyles(_rootContainer);  // FIX: position/size at frame 1

        // Initial display: hidden via inline (NOT USS)
        _rootContainer.style.display = DisplayStyle.None;
        _rootContainer.pickingMode = PickingMode.Ignore;

        InitFilterChips();
        InitSearchField();
        InitActionButtons();

        _built = true;
    }

    // === FIX 2 + 3: Show() with cursor unlock + MarkDirtyRepaint ===
    public void Show() {
        if (!_built) EnsureBuilt();
        if (_rootContainer == null) return;
        _rootContainer.style.display = DisplayStyle.Flex;
        _rootContainer.pickingMode = PickingMode.Position;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        // FIX: reflow after first frame (UI_TOOLKIT_GUIDE §0a.4d)
        _rootContainer.MarkDirtyRepaint();
        _rootContainer.schedule.Execute(() => _rootContainer.MarkDirtyRepaint()).StartingIn(50);

        RefreshAllSkillsList();
    }

    public void Hide() {
        if (_rootContainer == null) return;
        _rootContainer.style.display = DisplayStyle.None;
        _rootContainer.pickingMode = PickingMode.Ignore;
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    public void Toggle() {
        if (_rootContainer != null && _rootContainer.style.display == DisplayStyle.Flex) Hide();
        else Show();
    }

    // === Esc-handler BEFORE NetworkManager guard (UI_TOOLKIT_GUIDE +1 lesson) ===
    private void Update() {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && IsOpen()) {
            Hide();
            return;
        }
    }

    private bool IsOpen() => _rootContainer != null && _rootContainer.style.display == DisplayStyle.Flex;

    // === FIX: position/size inline styles (no USS fight) ===
    private static void ApplyInlineFallbackStyles(VisualElement root) {
        root.style.position = Position.Absolute;
        root.style.top = new Length(50, LengthUnit.Percent);
        root.style.left = new Length(50, LengthUnit.Percent);
        root.style.translate = new StyleTranslate(new Translate(
            new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent)));
        root.style.width = 720;
        root.style.maxWidth = new Length(90, LengthUnit.Percent);
        root.style.maxHeight = new Length(90, LengthUnit.Percent);
    }
}
```

---

## 5. Данные: где брать список навыков

```csharp
private List<SkillNodeConfig> _allSkillConfigs = new();  // все 30 нод
private List<string> _learnedIds = new();  // из SkillsClientState.CurrentSnapshot.learnedSkillIds

private void RefreshAllSkillsList() {
    // 1. Все ноды из Resources
    _allSkillConfigs = Resources.LoadAll<SkillNodeConfig>("Skills").ToList();

    // 2. Learned set из SkillsClientState (свежий Instance!)
    var s = SkillsClientState.Instance;
    _learnedIds = s?.CurrentSnapshot?.learnedSkillIds?.ToList() ?? new List<string>();

    // 3. Apply filter + search
    ApplyFilterAndSearch();
}
```

**Per skill row** — то же что в CharacterWindow, но с кнопкой `[Изучить]`/`[Забыть]` inline.

---

## 6. Поиск по эффектам

```csharp
private void OnSearchChanged(string query) {
    if (string.IsNullOrEmpty(query)) { ApplyFilterAndSearch(); return; }
    var q = query.ToLower();
    var filtered = _allSkillConfigs.Where(s => {
        if (s == null || string.IsNullOrEmpty(s.skillId)) return false;
        if (s.skillId.ToLower().Contains(q)) return true;
        if (s.displayName?.ToLower().Contains(q) == true) return true;
        // Поиск по эффектам: "STR+2", "DEX", "+3" и т.д.
        foreach (var e in s.effects ?? Array.Empty<SkillEffect>()) {
            if (e.statType.ToString().ToLower().Contains(q)) return true;
            if (e.floatValue.ToString("F0").Contains(q)) return true;
            if (e.multiplier > 0 && e.multiplier.ToString("F2").Contains(q)) return true;
        }
        return false;
    });
    RebuildSkillList(filtered);
}
```

**Примеры:**
- "melee" → все мечные
- "heavy" → HeavySwing, HeavyArmor
- "STR" → все навыки с бонусом к силе
- "+2" → все навыки с floatValue=2 (например BasicStrike STR+2)
- "×1.15" → навыки с multiplier=1.15

---

## 7. Правки CharacterWindow

### 7.1 UXML — добавить кнопку `[ИЗУЧИТЬ НАВЫК]` в combat-блок

```xml
<ui:VisualElement class="progression-col skills-col">
    <ui:Label text="Боевые навыки" class="progression-col-title" />
    <ui:VisualElement name="skill-combat-chip-row" class="skill-chip-row"> ... </ui:VisualElement>  <!-- УБРАТЬ чипы -->
    <ui:ScrollView name="skills-combat-scroll" class="skills-scroll"> ... </ui:ScrollView>  <!-- показывать только LEARNED -->
    <ui:Label name="open-skill-tree-btn" text="ИЗУЧИТЬ НАВЫК" class="open-skill-tree-btn" />  <!-- НОВОЕ -->
</ui:VisualElement>
```

### 7.2 C# — упростить `_skillsCombatCache` (только LEARNED)

```csharp
private void RefreshSkillsCache(HashSet<string> learned) {
    _skillsCombatCache.Clear();
    _skillsSocialCache.Clear();
    var all = Resources.LoadAll<SkillNodeConfig>("Skills");
    foreach (var skill in all) {
        if (skill == null || string.IsNullOrEmpty(skill.skillId)) continue;
        // SESSION #4 FIX: combat list = только LEARNED. Управление изучением/забыванием в SkillTreeWindow.
        if (skill.category == SkillCategory.Combat) {
            if (learned == null || !learned.Contains(skill.skillId)) continue;
            _skillsCombatCache.Add(new SkillRow {
                SkillId = skill.skillId,
                DisplayName = skill.displayName,
                State = "LEARNED",
                XpCost = skill.LearnXpCost,
                RequiredTier = skill.RequiredIntelligenceTier,
            });
        } else {
            // Social оставляем как есть (там логика проще)
            _skillsSocialCache.Add(new SkillRow { /* ... */ });
        }
    }
}
```

### 7.3 C# — клик на кнопку `[ИЗУЧИТЬ НАВЫК]`

```csharp
private void InitOpenSkillTreeButton() {
    var root = _root;
    if (root == null) return;
    var btn = root.Q<VisualElement>("open-skill-tree-btn");
    if (btn == null) return;
    btn.RegisterCallback<ClickEvent>(_ => {
        var stw = ProjectC.Skills.UI.SkillTreeWindow.Instance;
        if (stw != null) stw.Show();
        else Debug.LogWarning("[CharacterWindow] SkillTreeWindow.Instance==null (not spawned yet)");
    });
}
```

---

## 8. Предупреждения и паттерны (UI_TOOLKIT_GUIDE + lessons)

| # | Pitfall | Защита |
|---|---|---|
| 1 | `cursor: link` в USS спамит UGUI errors | **НЕ использовать** `cursor: link` (см. session #3 fix) |
| 2 | USS `display: none !important` ломает toggle | `display` только inline C# |
| 3 | ListView для малых наборов глючит | **Manual rows** в ScrollView (≤30 навыков) |
| 4 | `OnEnable` race на sibling UIDocument | `EnsureBuilt()` и в `OnEnable`, и в `Start()` (idempotent) |
| 5 | Esc-handler не работает при отсутствующем NM | Esc-проверка ДО NM-guard в `Update()` |
| 6 | UIDocument.OnEnable перезаписывает `_doc.visualTreeAsset` | `CloneTree()` + `_doc.rootVisualElement.Clear()` + `Add(_rootContainer)` |
| 7 | USS не подгружается | Explicit `_doc.rootVisualElement.styleSheets.Add(uss)` |
| 8 | PanelSettings.themeUss=null → не рендерит | Копировать существующий `PanelSettings.asset` через MCP, не `CreateInstance` |
| 9 | `pickingMode=Position` блокирует другие окна | `pickingMode=Ignore` когда скрыто |
| 10 | Singleton-кэш stale | Всегда `Instance` свежий в обработчиках |

---

## 9. Verify в Play Mode

| Шаг | Ожидание |
|---|---|
| 1 | P → CharacterWindow → "Боевые навыки" → пустой список (LEARNED=0) + кнопка `[ИЗУЧИТЬ НАВЫК]` |
| 2 | Клик `[ИЗУЧИТЬ НАВЫК]` → открывается overlay 720x500, cursor свободен |
| 3 | Видны 6 chip-фильтров + search field, слева список из 30 навыков, справа "Выберите навык" |
| 4 | Клик на `[⚔ Melee]` → список фильтруется до 6+ melee |
| 5 | Печатать в search "STR" → список фильтруется по эффектам (навыки с STR бонусом) |
| 6 | Клик на "GreatSword" в списке → справа детали: name + effects + cost + prereq "→ BasicSword" + dependents (если есть) |
| 7 | Клик `[Изучить]` → Console: `[CharacterWindow] RequestLearnSkillRpc: skillId=melee_great_sword` → после snapshot строка меняется на LEARNED → кнопка переключается на `[Забыть]` |
| 8 | Клик `[Забыть]` → снова AVAILABLE |
| 9 | Закрыть SkillTreeWindow (Esc или `[ЗАКРЫТЬ]`) → cursor lock возвращается, в CharacterWindow LEARNED строка появилась |

---

## 10. История документа

| Дата | Изменения |
|---|---|
| 2026-06-26 | Первая версия. Plan нового SkillTreeWindow overlay + правки CharacterWindow. Применены lessons UI_TOOLKIT_GUIDE (4 фикса, manual rows, cursor fix, display inline toggle). |