# Battle Skills UI — Design & Implementation Plan

> **Дата:** 2026-06-26 (сессия #2.2)
> **Подсистема:** Character Progression → Skill Tree → UI (CharacterWindow)
> **База:** `docs/Character/Skills/AUDIT_2026-06-26_CURRENT_STATE_AND_NEXT_STEPS.md`, `docs/dev/SKILLS_NEXT_STEPS_T-CB_LOG.md`
> **Проблема:** навыки есть в базе (30 .asset), видны в P→НАВЫКИ, но нет кнопок "Изучить"/"Забыть", нет фильтра по дисциплинам (CombatFilter enum есть, но UI для переключения отсутствует), нет отображения эффектов (StatMod бонусов), нет визуальной иерархии зависимостей.
> **Цель:** полноценный UI управление навыками: просмотр, изучение, забывание, фильтрация, визуализация tree-структуры.
> **Scope сессии:** design-doc (этот файл) + кодовая реализация + USS-стили.
> **Принцип:** не ломать существующие системы. Additive-only к CharacterWindow.

---

## 1. Архитектура

### 1.1 UI-структура (в рамках существующего CharacterWindow)

Текущий layout (из `CharacterWindow.uxml`):

```
Window root
└── progression-section
    ├── equip-row (Одежда + Модули)         ← row 1
    └── stats-row (Характеристики | Боевые | Социальные)  ← row 2
                        ↑ колонка 2    ↑ колонка 3
```

Нам нужно **вписаться в существующие колонки `skills-col`** (40% каждая), не меняя общий layout. Внутри каждой колонки добавляем:

```
skills-col  (боевые 40%)
└── filter-chip-row       ← НОВОЕ: [Все] [⚔ Melee] [🏹 Ranged] [💣 Explosives] [🌌 Antigrav] [🛡 Defense]
    └── skill-list scroll
        ├── skill-row (LEARNED)   → state badge + title + cost + [Забыть]
        ├── skill-row (AVAILABLE) → state badge + title + cost + prereq text + [Изучить]
        └── skill-row (LOCKED)    → state badge + title + cost + prereq text (disabled)
```

skills-col (социальные 40%)
```
└── social list scroll (без фильтра, только 4 навыка)
```

### 1.2 Row-фабрика (C#, замена MakeManualSkillRow)

Каждый ряд = `VisualElement` с:

```
.row
├── .skill-state (✅/○/✕)       — 18px
├── .skill-title ("BasicStrike") — flex-grow
├── .skill-cost ("Free"/"50XP")  — 36px
├── .skill-tier ("T0"/"T1")      — 26px
├── .skill-action-btn (опционально) — 60px, "Изучить"/"Забыть", 0px если LOCKED
└── .skill-prereq (текст "Нужно: BasicStrike", только для AVAILABLE/LOCKED) — full-width под строкой
```

Условия показа кнопки:
- `LEARNED`: кнопка **[Забыть]** (шлёт `RequestForgetSkillRpc`).
- `AVAILABLE`: кнопка **[Изучить]** (шлёт `RequestLearnSkillRpc`).
- `LOCKED`: кнопки нет, только текст "Требуется: ..."

### 1.3 Filter-chip row

Horizontal row над combat-scroll:

```
filter-chip-row
├── [Все]           ← active color + underline
├── [⚔ Melee]
├── [🏹 Ranged]
├── [💣 Explosives]
├── [🌌 Antigrav]
└── [🛡 Defense]
```

Каждый чип — `VisualElement` (не Button, для единого стиля). При клике:
1. `SetCombatFilter(CombatFilter.X)` — обновляет `_activeCombatFilter` (enum уже существует).
2. Убирает класс `.chip-active` со всех чипов.
3. Добавляет `.chip-active` на выбранный.
4. Вызывает `RebuildSkillsListView()` (уже фильтрует по `MatchesCombatFilter`).

**USS:** `.chip-row`, `.chip`, `.chip-active`, `.chip:hover`.

### 1.4 Отображение эффектов навыка (stat bonuses)

В `SkillRow` struct нужно добавить поле `EffectsText` (string). Заполняется в `RefreshSkillsCache`:

```
var effects = new List<string>();
foreach (var effect in skill.effects)
{
    if (effect.type == SkillEffect.Type.StatMod)
    {
        if (effect.floatValue > 0) effects.Add($"{effect.statType}+{effect.floatValue}");
        if (effect.multiplier > 0) effects.Add($"×{effect.multiplier:F2} {effect.statType}");
    }
}
row.EffectsText = string.Join(", ", effects);
```

Показывать в строке после cost: `.skill-effects { width: 80px; font-size: 9px; color: rgb(150,220,150); }`

### 1.5 Визуализация tree-зависимостей (без Painter2D)

Вместо полноценного графа (Phase 2, T-P19) — **иерархический отступ по treeX**:

```
melee_basic_sword
 └─ melee_great_sword (на 1 отступ вправо)
    └─ melee_precision_strike (на 2 отступа)
```

Поле `treeX` в `SkillNodeConfig` уже есть (pixels). В `RefreshSkillsCache`: если `treeX > 0` → добавляем `margin-left: treeX/10 px` на row. Это даёт визуальную вложенность.

Также: **порядок сортировки** — по `treeY` (сверху вниз) + `treeX` (слева направо). Сейчас навыки выводятся в порядке `Resources.LoadAll` (недетерминировано). В `RefreshSkillsCache` добавляем `.OrderBy(s => s.treeY).ThenBy(s => s.treeX)`.

---

## 2. План реализации (шаги)

| # | Шаг | Файлы | Что | Сложность |
|---|---|---|---|---|
| 1 | **USS**: `.skill-btn-learn`, `.skill-btn-forget`, `.skill-effects`, `.chip-*`, `.skill-prereq` | `CharacterWindow.uss` | Новые стили для action-кнопок, чипов, эффектов, prereq-текста | ~20м |
| 2 | **SkillRow struct**: добавить `EffectsText` + сортировка | `CharacterWindow.cs` | Поле для отображения stat-бонусов. `OrderBy(treeY).ThenBy(treeX)` | ~15м |
| 3 | **RefreshSkillsCache**: заполнять `EffectsText` из `skill.effects[]` | `CharacterWindow.cs` | Чтение StatMod bonus + multiplier + stringParam эффектов | ~20м |
| 4 | **MakeManualSkillRow**: кнопки [Изучить]/[Забыть] + prereq-текст + effects | `CharacterWindow.cs` | Замена строки: визуальная кнопка, а не click на всю строку | ~30м |
| 5 | **OnForgetSkillClicked**: reflection-RPC для забывания | `CharacterWindow.cs` | Аналог `OnLearnSkillClicked`, но `RequestForgetSkillRpc` | ~10м |
| 6 | **UXML**: добавить `filter-chip-row` в combat-skills колонку | `CharacterWindow.uxml` | Horizontal VisualElement с 6 чипами | ~10м |
| 7 | **C#**: инициализация чипов + клик → SetCombatFilter | `CharacterWindow.cs` | В `InitProgressionTab()` или новом методе `InitSkillFilterChips()` | ~20м |
| 8 | **MakeManualSkillRow**: иерархический отступ по treeX | `CharacterWindow.cs` | `row.style.marginLeft = skill.treeX / 5` (clamped) | ~10м |
| 9 | **Сборка + compile check** | — | `refresh_unity` + `read_console` | ~5м |
| 10 | **Документ**: этот файл (design) + changelog | `docs/dev/SKILLS_NEXT_STEPS_T-CB_LOG.md` | Фиксация сделанного | ~10м |

**Итого ~2.5ч. Каждый шаг проверяем compile отдельно.**

---

## 3. Макет (USS-скиз)

```css
/* === Filter row === */
.chip-row {
    flex-direction: row !important;
    flex-wrap: wrap !important;
    padding: 2px 0px !important;
    gap: 2px !important;
    flex-shrink: 0 !important;
}
.chip {
    font-size: 9px !important;
    padding: 2px 6px !important;
    border-width: 1px !important;
    border-radius: 3px !important;
    border-color: rgba(80, 100, 130, 0.3) !important;
    color: rgb(160, 180, 200) !important;
    cursor: link !important;
    -unity-text-align: middle-center !important;
    background-color: rgba(30, 40, 60, 0.5) !important;
}
.chip:hover {
    background-color: rgba(60, 80, 120, 0.6) !important;
}
.chip-active {
    background-color: rgba(80, 140, 200, 0.4) !important;
    border-color: rgba(100, 180, 230, 0.7) !important;
    color: rgb(220, 240, 255) !important;
}

/* === Action buttons === */
.skill-action-btn {
    width: 56px !important;
    height: 18px !important;
    font-size: 9px !important;
    border-radius: 2px !important;
    -unity-text-align: middle-center !important;
    cursor: link !important;
    flex-shrink: 0 !important;
}
.skill-btn-learn {
    background-color: rgba(60, 140, 80, 0.5) !important;
    border-width: 1px !important;
    border-color: rgba(80, 180, 100, 0.4) !important;
    color: rgb(200, 255, 200) !important;
}
.skill-btn-learn:hover {
    background-color: rgba(80, 180, 100, 0.8) !important;
}
.skill-btn-forget {
    background-color: rgba(180, 80, 60, 0.4) !important;
    border-width: 1px !important;
    border-color: rgba(220, 100, 80, 0.4) !important;
    color: rgb(255, 200, 180) !important;
}
.skill-btn-forget:hover {
    background-color: rgba(220, 100, 80, 0.7) !important;
}

/* === Effects text === */
.skill-effects {
    width: 60px !important;
    font-size: 9px !important;
    color: rgb(150, 220, 150) !important;
    -unity-text-align: middle-left !important;
    flex-shrink: 0 !important;
    margin-left: 2px !important;
}

/* === Prereq row (full-width under row) === */
.skill-prereq {
    font-size: 8px !important;
    color: rgba(180, 160, 100, 0.8) !important;
    padding-left: 22px !important;  /* выравнивание под title */
    -unity-text-align: middle-left !important;
    margin-top: -1px !important;
    margin-bottom: 1px !important;
}
```

---

## 4. Row-строитель (C#, псевдокод)

```csharp
private VisualElement MakeManualSkillRow(SkillRow data)
{
    var row = new VisualElement();
    row.AddToClassList("skill-row");

    // State badge
    var badge = new Label { name = "skill-row-state",
        text = data.State switch { "LEARNED" => "✅", "AVAILABLE" => "○", _ => "✕" } };
    badge.AddToClassList("skill-row-state");
    row.Add(badge);

    // Title
    var title = new Label { name = "skill-row-title", text = data.DisplayName };
    title.AddToClassList("skill-row-title");
    row.Add(title);

    // Effects (stat bonuses)
    if (!string.IsNullOrEmpty(data.EffectsText))
    {
        var eff = new Label { name = "skill-row-effects", text = data.EffectsText };
        eff.AddToClassList("skill-effects");
        row.Add(eff);
    }

    // Cost
    var cost = new Label { name = "skill-row-cost",
        text = data.XpCost > 0 ? $"{data.XpCost:F0}XP" : "Free" };
    cost.AddToClassList("skill-row-cost");
    row.Add(cost);

    // Tier badge
    var tier = new Label { name = "skill-row-tier", text = $"T{data.RequiredTier}" };
    tier.AddToClassList("skill-row-tier");
    row.Add(tier);

    // Action button (Learn or Forget)
    string btnText = data.State switch { "LEARNED" => "Забыть", "AVAILABLE" => "Изучить", _ => null };
    string btnClass = data.State switch { "LEARNED" => "skill-btn-forget", "AVAILABLE" => "skill-btn-learn", _ => null };
    if (btnText != null)
    {
        var btn = new Label { name = "skill-action-btn", text = btnText };
        btn.AddToClassList("skill-action-btn");
        btn.AddToClassList(btnClass);
        var capturedId = data.SkillId;
        btn.RegisterCallback<ClickEvent>(evt =>
        {
            if (data.State == "AVAILABLE") OnLearnSkillClicked(capturedId);
            else OnForgetSkillClicked(capturedId);
            evt.StopPropagation();
        });
        row.Add(btn);
    }

    row.AddToClassList(data.State switch
    {
        "LEARNED"   => "skill-row-learned",
        "AVAILABLE" => "skill-row-available",
        "LOCKED"    => "skill-row-locked",
        _ => "skill-row-locked",
    });

    // Prereq text (under row)
    if (!string.IsNullOrEmpty(data.PrereqNames))
    {
        var prereq = new Label { name = "skill-row-prereq", text = $"→ {data.PrereqNames}" };
        prereq.AddToClassList("skill-prereq");
        row.Add(prereq);
    }

    // Tree indent (Phase 1: margin-left based on treeX)
    // TODO: add treeX/treeY to SkillRow struct and use here
    // row.style.marginLeft = Mathf.Min(data.TreeX / 5f, 40f);

    return row;
}
```

---

## 5. Зависимости

| Шаг | Блокирует | Зависит от |
|---|---|---|
| 1 (USS) | 4, 6 | — |
| 2 (SkillRow.EffectsText) | 3 | — |
| 3 (Effects заполнение) | 4 | 2 |
| 4 (MakeManualSkillRow rewrite) | — | 1, 3 |
| 5 (OnForgetSkillClicked) | — | — |
| 6 (UXML chip-row) | 7 | 1 |
| 7 (C# chips init) | — | 6 |
| 8 (treeX indent) | — | 2 |

---

## 6. Play Mode проверка

| Шаг | Что проверяем |
|---|---|
| После 4 | P→НАВЫКИ: LEARNED row → badge ✅ + title + effects "[STR+2]" + cost + [Забыть] кнопка. Клик → Console: `[SkillInputService] ...` или `[SkillsServer]`. |
| После 4 | AVAILABLE row → badge ○ + title + effects + prereq "→ BasicStrike" + [Изучить] кнопка. Клик → Console `[CharacterWindow] RequestLearnSkillRpc: skillId=...` |
| После 4 | LOCKED row → badge ✕ + title + prereq "→ BasicSword, BasicStrike". Кнопки нет. |
| После 7 | Клик на чип `[⚔ Melee]` → только melee-строки видны. `[Все]` → все 22. Чип подсвечен. |
| После 8 | Навыки отсортированы по treeY (сверху вниз). Первые 4 базовых (treeY=0), затем tier 1 (treeY=100), затем tier 2 (treeY=200). |

---

## 7. История документа

| Дата | Изменения |
|---|---|
| 2026-06-26 | Первая версия. Design & implementation plan для UI навыков в CharacterWindow. |