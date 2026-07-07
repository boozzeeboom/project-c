# Skills/Battle — Next Steps Log (T-INP / T-CB series)

> **Серия:** T-INP-* (input layer + SkillInputService) + T-CB01+ (Battle skill system extensions)
> **База:** `docs/Character/Skills/AUDIT_2026-06-26_CURRENT_STATE_AND_NEXT_STEPS.md`
> **Дата:** 2026-06-26 (сессия #2 — реализация первой пачки)
> **Статус:** 🟢 Все 8 запланированных шагов из audit §4 завершены. Compile clean (0 errors). Требуется Play Mode verify.

---

## Сессия #2 (2026-06-26) — реализация

### Что сделано

| # | Ticket | Файл | Что | Статус |
|---|---|---|---|---|
| 1 | **T-INP-01** | `Assets/_Project/Scripts/Skills/SkillInputService.cs` (новый) | Новый `MonoBehaviour` singleton (owner-only). `SkillInputSlot` enum (None/Primary/Secondary/Slot1..4). API: `Initialize()`, `TryActivate()`, `BindSlot()`, `IsOnCooldown()`. Per-slot cooldown, target finder через `System.Func<ulong>` | ✅ |
| 2 | **T-INP-02** | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | `InitializeSkillInputService()` (AddComponent + Initialize) + `HandlePrimaryAttackInput()` (delegate). Вызов в IsOwner блоке `OnNetworkSpawn`. K-attack handler переделан: `DebugAttackNearestNpc()` → `HandlePrimaryAttackInput()`. `using ProjectC.Skills;` добавлен | ✅ |
| 3 | **T-INP-03** | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | ЛКМ (Mouse 0) handler в `Update()` сразу после K-attack. Параллельный к K-fallback (оба зовут `HandlePrimaryAttackInput()` → `SkillInputService.TryActivate(Primary)`) | ✅ |
| 4 | **T-INP-04** | `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | Новый event `OnAttackPressed`. В `Update()` после Mouse-delta: emit при ЛКМ **ИЛИ** K. Legacy K-handling в NetworkPlayer НЕ тронут (parallel пути) | ✅ |
| 5 | **T-CB01** | `Assets/_Project/Scripts/Skills/SkillEffect.cs` | Enum `Type` расширен с 3 до 8 значений: `WeaponProficiencyUnlock=3, ArmorProficiencyUnlock=4, WeaponTechniqueUnlock=5, ExplosiveRecipeUnlock=6, AntigravTechniqueUnlock=7`. 5 factory methods добавлены. **Backward-compat**: 8 существующих .asset используют только `StatMod=0/AbilityUnlock=1` — никаких поломок | ✅ |
| 6 | **T-INP-06** (variant A per audit §4) | `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` | Warning-only proficiency log в `TryEquip` ветке `WeaponItemData`. Hard gate (`requiredSkills` check) уже работал раньше. Дополнительно: `Debug.Log` показывает weapon name + class + proficiency + hasProficiency | ✅ |
| 7 | **T-INP-07** | `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | `CombatFilter` enum (All/Melee/Ranged/Explosives/Antigrav/Defense). Поле `_activeCombatFilter`, методы `SetCombatFilter/GetCombatFilter`, `MatchesCombatFilter` (substring по skillId prefix). `RebuildSkillsListView()` фильтрует combat-строки. UI-chip-row **НЕ** добавлен (отдельная задача) | ✅ |

### Compile verification

| Этап | read_console errors |
|---|---|
| После Task 1 (SkillInputService.cs создан) | 2 → CS0246 (NetworkPlayer not found) → fix (using) → 0 |
| После Task 2 (NetworkPlayer patches × 4) | 0 |
| После Task 3 (ЛКМ handler) | 0 |
| После Task 4 (OnAttackPressed event) | 0 |
| После Task 5 (SkillEffect.Type enum + factories) | 0 |
| После Task 6 (EquipmentWorld warning log) | 0 |
| После Task 7 (CharacterWindow filter) | 0 |

**Итог:** все 8 шагов компилируются чисто.

---

## Что НЕ сделано в этой сессии (явные deferral)

| Item | Причина | Где доделать |
|---|---|---|
| **UX-чипы фильтра в CharacterWindow.uxml** (горизонтальный chip-row `[Все] [⚔ Melee] [🏹 Ranged] [💣 Explosives] [🌌 Antigrav] [🛡 Defense]`) | API есть (`SetCombatFilter`), но UI не подключён. Можно подключить через `Q<VisualElement>("skill-filter-row")` если добавим в UXML. Решил не трогать — отдельная сессия для UX-полировки | Session #5 (audit roadmap §5) |
| **Drag-and-drop skill → skill slot bar** | SkillInputService.BindSlot API готов, но UI нет | Phase 2 |
| **Raycast targeting** (замена "nearest NpcTarget в 15м") | Target finder delegate работает, но использует legacy nearest. Phase 2 | Session #8 |
| **ApplySkillEffects runtime handler для новых типов** (T-CB07) | Enum добавлен, но runtime switch в SkillsServer — no-op. WeaponProficiency ещё не проверяется при TryEquip | Session #7 |
| **`SkillNodeConfig.CombatDiscipline` поле** (T-CB02) | Substring fallback достаточно для MVP. Поле добавим когда CharacterWindow UI-chip будет готов | Session #4 |
| **ExplosiveItemData SO + WeaponClassCatalog + ArmorClassCatalog** (T-CB04/05) | Не в scope этой сессии | Session #12+ |
| **Remove `DebugAttackNearestNpc` method** | Оставлен как reference / fallback. Удалим в Session #9 | Session #9 |
| **Input System рефакторинг (legacy → Input Actions)** | Не в scope | Session #11 |

---

## Verification checklist для Play Mode

Когда будете готовы проверить в Play Mode:

1. **Compile:** открыть Unity Editor → Console → **0 errors**.
2. **Compile-fix:** если есть warnings о .meta — это нормально (Unity создаёт .meta при импорте).
3. **Play Mode (single-player host):**
   - StartHost → Console должно показать `[CombatServer] OnNetworkSpawn: Instance set` + `[NetworkPlayer] InitializeSkillInputService: SkillInputService ready (owner-only)`.
   - Подойти к NPC на расстояние ≤ 15м.
   - Нажать **K** → Console: `[SkillInputService] TryActivate: slot=Primary skill='' target=X trigger='Attack'` (skill пуст, потому что нет bind — это OK; должны видеть `_animator.SetTrigger("Attack")` если Animator жив).
   - Нажать **ЛКМ** → то же поведение.
   - Проверить что Animator Controller проигрывает Attack state.
4. **Equip weapon flow:**
   - В инвентаре (P → персонаж → инвентарь): найти `Weapon_WoodenSword`.
   - Нажать [НАДЕТЬ] → Console должно показать `[EquipmentWorld] Weapon equip attempt: client=0 weapon='Деревянный меч' class=Sword proficiency='<none>' hasProficiency=True` (или False если skill не learned).
5. **Skill filter:**
   - P → НАВЫКИ → должны видеть все combat-навыки (фильтр All).
   - Через execute_code: `var cw = ProjectC.UI.Client.CharacterWindow.Instance; cw.SetCombatFilter(ProjectC.UI.Client.CharacterWindow.CombatFilter.Melee);` → должны видеть только 6 melee навыков.

---

## Файлы изменены

| Файл | Тип | Строки изменений |
|---|---|---|
| `Assets/_Project/Scripts/Skills/SkillInputService.cs` | NEW (262 строк) | +262 |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | EDIT | +60 / -4 |
| `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | EDIT | +17 / -0 |
| `Assets/_Project/Scripts/Skills/SkillEffect.cs` | EDIT | +50 / -2 |
| `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` | EDIT | +12 / -0 |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | EDIT | +29 / -0 |

**Итого:** 6 файлов, 1 новый, 5 отредактированных, +430 / -6 строк.

---

## Что дальше (по audit roadmap §5)

| Session | Тема | Сложность |
|---|---|---|
| **#3** | `SkillEffect.Type` runtime handler (T-CB07, Variant A+B — proficiency tracking) | ~2ч |
| **#4** | `SkillNodeConfig.CombatDiscipline` поле (T-CB02) + CharacterWindow UX-чипы | ~1.5ч |
| **#5** | CharacterWindow UI-chip фильтр | ~1.5ч |
| **#6** | EquipmentServer.TryEquip + WeaponItemData hard proficiency gate (T-CB06 full) | ~2ч |
| **#7** | ApplySkillEffects handler Variant B (proficiency tracking в SkillsWorld) | ~2ч |
| **#8** | Targeting: raycast от камеры (замена "nearest NpcTarget в 15м") | ~1.5ч |
| **#9** | Cleanup: удалить `DebugAttackNearestNpc` метод | ~15м |
| **#10** | Skill tree Painter2D UI (T-P19) | ~3-4ч |
| **#11** | Input System рефакторинг (legacy → Input Actions) | ~3-4ч |
| **#12+** | ExplosiveItemData, WeaponClassCatalog, ArmorClassCatalog | ~3-4ч |

---


---

## Сессия #3 (2026-06-26) — Battle Skills UI: реализация по 50_UI_DESIGN_PLAN.md

**План:** `docs/Character/Skills/Battle/50_UI_DESIGN_PLAN.md` (10 шагов).
**Дизайн-гайд:** `docs/UI/UI_TOOLKIT_GUIDE.md` (manual rows pattern, !important, click handlers).

### Что сделано

| # | Шаг | Файл | Что |
|---|---|---|---|
| 1 | USS новые классы | `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | `.skill-row-learned/available/locked` (state-подсветка), `.skill-row-effects`, `.skill-action-btn` + `.skill-btn-learn/forget` (action-кнопки), `.skill-row-prereq` (text под строкой), `.skill-chip-row` + `.skill-chip` + `.skill-chip-active` (фильтр) |
| 2 | SkillRow struct | `CharacterWindow.cs` | +3 поля: `EffectsText`, `TreeX`, `TreeY` |
| 3 | RefreshSkillsCache | `CharacterWindow.cs` | Заполнение EffectsText через `FormatEffectsText()` (StatMod бонус + multiplier + new effect types 3..7), сортировка combat по treeY/treeX (с 0/0 — в конец) |
| 4 | MakeManualSkillRow rewrite | `CharacterWindow.cs` | State-класс на row, effects label, action-кнопка `[Изучить]`/`[Забыть]`, prereq text под строкой, treeX → paddingLeft (clamp 0..32px) |
| 5 | OnForgetSkillClicked | `CharacterWindow.cs` | Reflection-RPC `RequestForgetSkillRpc(skillId, RpcParams)` (Q3.4 free respec) |
| 6 | UXML chip-row | `CharacterWindow.uxml` | Horizontal VisualElement с 6 чипами: `[Все] [⚔ Melee] [🏹 Ranged] [💣 Explosives] [🌌 Antigrav] [🛡 Defense]`. Все классы `skill-chip`, активный `skill-chip-active` |
| 7 | InitSkillFilterChips + BindChipClick | `CharacterWindow.cs` | Привязка ClickEvent на каждый чип → SetCombatFilter() + toggle `.skill-chip-active` class |
| 8 | treeX indent | (в task 4) | paddingLeft на row = min(treeX/5, 32) px |
| 9 | Compile check | — | `refresh_unity scope=all` + `read_console` → **0 errors** |
| 10 | Changelog | этот файл | — |

### Compile verification

- Brace balance OK во всех файлах (572=572, 588=588, 1020 строк USS).
- read_console: 0 errors, 0 warnings (только my own code).
- `scope=all` потребовался — UXML+USS изменения в Resources/UI/ подхватывают asset import.

### Что НЕ реализовано (явно, отложено)

- **Drag-to-slot** для Skill1-4 — нужен `InputBindingsConfig` SO (O-1 в audit).
- **Painter2D skill tree** (T-P19) — full DAG visualization, Phase 2.
- **Soft glow / tier color** на effects — пока только базовый green.
- **Toasts** на learn/forget success — пока Debug.Log (CharacterWindow.HandleSkillResult).

### Play Mode verify

1. `refresh_unity` → compile clean.
2. P (открыть CharacterWindow) → должна быть вкладка "ПЕРСОНАЖ".
3. В секции "Характеристики | Боевые | Социальные" у "Боевые навыки" сверху — 6 чипов, `[Все]` подсвечен.
4. Клик `[⚔ Melee]` → только melee-строки (BasicSword, HeavySwing, PrecisionStrike, DaggerMastery, SpearReach, DualWield). Чип подсвечивается.
5. Клик `[Все]` → все combat-строки возвращаются.
6. `AVAILABLE` строка: state ○ + title + `[STR+1]` effects + cost + T1 + зелёная `[Изучить]` кнопка + text `→ BasicStrike` (если prereq).
7. `LOCKED` строка: state ✕ + title + effects + cost + T1 + text `→ BasicSword, BasicStrike`. Без кнопки.
8. Клик `[Изучить]` → Console: `[CharacterWindow] RequestLearnSkillRpc: skillId=melee_basic_sword`. После snapshot: state меняется на LEARNED, появляется красная `[Забыть]`.
9. Клик `[Забыть]` → Console: `[CharacterWindow] RequestForgetSkillRpc: skillId=melee_basic_sword`. После snapshot: снова AVAILABLE (XP не возвращается по Q3.4).

### Файлы изменены

| Файл | Изменение |
|---|---|
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | +111 строк (новые классы) |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | +8 строк (chip-row) |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | +110 строк, рефакторинг MakeManualSkillRow, OnForgetSkillClicked, InitSkillFilterChips, BindChipClick, FormatEffectsText |
| `docs/Character/Skills/Battle/50_UI_DESIGN_PLAN.md` | design-doc (без изменений) |


---

## Сессия #4 (2026-06-26) — SkillTreeWindow overlay (полная реализация)

**План:** `docs/Character/Skills/Battle/60_SKILL_TREE_WINDOW_DESIGN.md`
**Trigger:** текущий CharacterWindow → "Боевые навыки" перегружен — 30 навыков в 40% колонке, кнопки Изучить/Забыть клипаются, prereq-строки не помещаются, невозможно понять что даёт навык.

**Решение:** новый полноэкранный overlay `SkillTreeWindow` (720×500) с 6 chip-фильтрами, поиском по имени+эффектам, детальной панелью. CharacterWindow → combat-блок оставлен ТОЛЬКО для просмотра изученных навыков + кнопка `[ИЗУЧИТЬ НАВЫК]` → открывает overlay.

### Что сделано (14 шагов)

| # | Файл | Что |
|---|---|---|
| 1 | `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs` (новый, 634 строк) | Класс-обёртка. 4 фикса из UI_TOOLKIT_GUIDE (pickingMode, layout fallback, cursor, MarkDirtyRepaint), Esc-handler, OnEnable+Start idempotency, lazy-subscribe |
| 2 | `Assets/_Project/Resources/UI/SkillTreeWindow.uxml` (новый, 62 строк) | Top (title+chips+search) + Middle (list 40% + detail 60%) + Bottom (close). Все inline `style="display: none;"` для тоггл-кнопок |
| 3 | `Assets/_Project/UI/Resources/UI/SkillTreePanelSettings.asset` (новый) | Копия CharacterPanelSettings (proven themeUss) с sortingOrder=300 через MCP CopyAsset + SerializedObject |
| 4 | `Assets/_Project/Resources/UI/SkillTreeWindow.uss` (новый, 302 строк) | Все стили `!important`. NO `cursor: link` (UGUI 6 spam fix). Background hover вместо cursor |
| 5 | `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | Добавлен `using ProjectC.Skills.UI` + `CreateSkillTreeWindow()` метод + вызов в Awake (auto-spawn как root GO) |
| 6 | (covered by Task 5) | Scene-placed не нужен — NMC auto-spawn fallback с idempotency check |
| 7 | `CharacterWindow.uxml` + `.uss` | Кнопка `open-skill-tree-btn` (label `ИЗУЧИТЬ НАВЫК`). Title изменён на "Изученные боевые навыки". Chip-row удалён (фильтр в SkillTreeWindow) |
| 8 | `CharacterWindow.cs` | `RefreshSkillsCache` упрощён: combat = ТОЛЬКО LEARNED. Без prereq-строк и action-кнопок. Social оставлен как был. Добавлен `InitOpenSkillTreeButton()` метод. **Bug fix:** InitSkillFilterChips() НЕ вызывался — вызов добавлен рядом с InitOpenSkillTreeButton() |
| 9-12 | (в Task 1) | `RefreshAllSkillsList`, `OnSkillSelected`, `UpdateDetailPanel`, `OnLearnClicked`, `OnForgetClicked` — все реализованы в SkillTreeWindow.cs (Task 1, единым блоком) |
| 13 | Compile verify | `refresh_unity scope=all` + `read_console` → 0 моих errors. Reflection smoke test: SkillTreeWindow класс загружен, SkillDisciplineFilter enum 6 значений |
| 14 | (этот блок) | Changelog |

### Применённые lessons (UI_TOOLKIT_GUIDE)

- **4 фикса на Show/Hide**: `pickingMode=Position`/`Ignore` (FIX 1), `ApplyInlineFallbackStyles` для frame-1 layout (FIX 2), `Cursor.lockState=None`/`Locked` (FIX 3), `MarkDirtyRepaint` + `schedule.Execute(...).StartingIn(50)` (FIX 4)
- **Esc-handler ДО NetworkManager guard** — нельзя блокировать Esc на `if (nm == null) return`
- **UIDocument auto-load race** — `CloneTree()` + `Clear()` + `Add(_rootContainer)` pattern
- **PanelSettings** — копия через MCP `CopyAsset` (НЕ `CreateInstance` — `themeUss=null` баг)
- **display toggles inline C#** — НЕ `!important` в USS
- **NO `cursor: link`** — UGUI 6 спам (исправлено в сессии #3)
- **Manual rows в ScrollView** — не ListView (≤30 items)
- **Background hover вместо cursor** для clickability hint

### Reflection smoke test (compile clean)

```
SkillTreeWindow: FOUND
  Show: True
  Hide: True
  Toggle: True
  SkillDisciplineFilter enum: FOUND
    values (6): All, Melee, Ranged, Explosives, Antigrav, Defense
```

### Что тестировать в Play Mode

1. `StartHost` → P → "ПЕРСОНАЖ" → секция "Изученные боевые навыки" пустая (LEARNED=0) + кнопка `ИЗУЧИТЬ НАВЫК`
2. Клик `ИЗУЧИТЬ НАВЫК` → overlay 720×500, cursor свободен
3. 6 chip-фильтров сверху, search field, слева 30 навыков, справа "Выберите навык"
4. Клик на чип → список фильтруется
5. Поиск "STR" → навыки с бонусом к силе
6. Клик на `GreatSword` в списке → детали: name + effects + cost + prereq `→ BasicSword` + dependents
7. Клик `Изучить` → Console: `RequestLearnSkillRpc: skillId=melee_great_sword`. После snapshot → строка показывает LEARNED, кнопка переключается на `Забыть`
8. Закрыть Esc → cursor lock. CharacterWindow → "Изученные боевые навыки" → появилась строка `GreatSword`

### Что осталось (явно)

- Drag-to-slot для привязки навыка к Slot1-4 клавишам
- Painter2D skill tree graph (полноценный DAG, T-P19)
- CombatDiscipline поле в SkillNodeConfig (T-CB02) — пока substring фильтр
- Real `Receive*TargetRpc` вместо reflection (T-CB07)


## История документа

| Дата | Сессия | Изменения |
|---|---|---|
| 2026-06-26 | #2 (эта) | Первая пачка из 8 шагов. SkillInputService + ЛКМ/K-attack unified flow + SkillEffect.Type expansion + warning-only proficiency + combat-filter API. Все 8 шагов compile clean. |
| 2026-06-28 | #5 (эта) | 2D граф навыков Painter2D (T-P19). Canvas 2000×2000 px, absolute-позиционированные узлы, Painter2D линии, filter/search, scroll-to-selected. USS стили для state-цвета и selected. Синий фон _rootContainer debug-баг зафиксирован и исправлен. |
| **2026-07-07** | **#6** | **T-SKILL-03:** Исправлены фильтры SkillTreeWindow (4 чипа: melee/ranged/defense/placed вместо 6 с explosives/antigrav). Реализована полная persistence изученных навыков (JSON save/load через CharacterSaveData). StatsWorld.BuildSaveData теперь собирает полный DTO (stats + skills + equipment). StatsServer.OnClientConnectedForStats загружает скиллы и шлёт начальный снапшот клиенту. SkillsServer сохраняет мгновенно после learn/forget. КРИТИЧЕСКИЙ ФИКС: SkillsWorld.Reset() перенесён из SkillsServer.OnNetworkDespawn в StatsServer.OnNetworkDespawn (после flush-save). |
| **2026-07-08** | **#7** | **T-SKILL-04:** throwCount consumption. `HasThrowableInInventory` теперь проверяет суммарное количество Throwable-предметов (≥ requiredCount). `ConsumeThrowableFromInventory` принимает count и потребляет до N штук (добирает из следующих стаков). Потребление перенесено ДО target collection — гранаты списываются независимо от попадания. |
| **2026-07-08** | **#8** | **T-SKILL-05:** slot bindings persistence. `SkillInputService` теперь сохраняет бинды слотов (Primary/Secondary/Slot1-4) в `Skills/slot_bindings_{clientId}.json` при каждом `BindSlot`. Загружаются при `Initialize`. JsonUtility DTO: `SlotBindingsSave` + `SlotBindingEntry[]`. |

---

## Сессия #5 (2026-06-28) — 2D граф навыков Painter2D

**План:** `docs/Character/Skills/Battle/70_SKILL_TREE_2D_GRAPH.md`
**База:** SkillTreeWindow overlay (сессия #4)

### Что сделано

1. **Canvas** — list-container заменён на ScrollView + tree-content (2000×2000 px)
2. **Узлы** — VisualElement для каждого навыка, absolute позиционирование по treeX/treeY (scale ×2.5 + padding)
3. **State-цвета** — `tree-node-learned` (зелёная рамка), `tree-node-available` (жёлтая), `tree-node-locked` (серая)
4. **Выбор узла** — класс `tree-node-selected` (голубая рамка 3px)
5. **Линии** — `generateVisualContent` → `ctx.painter2D` (lineWidth=2, цвет по state родителя, MoveTo→LineTo от prereq → skill)
6. **Filter + Search** — скрывают несоответствующие узлы и их рёбра
7. **ScrollTo** — при клике/выборе из поиска, ScrollView скроллит к узлу

### Блоки и фиксы в этой сессии

| № | Проблема | Фикс |
|---|---|---|
| 1 | Синий фон `_rootContainer` закрывал весь экран | Убран `_rootContainer.style.backgroundColor` (был debug fallback `Color(0.08f, 0.12f, 0.18f)`) |
| 2 | Размер окна нестабилен | USS `width: 760px` + `left: 50%; translate: -50% 0` |
| 3 | Full-screen stretch конфликтовал с центровкой | Оставлен USS-путь (C# не менялся после коммита `4d87d99`) |

### Compile verification

- `refresh_unity mode=force compile=request` → 0 errors
- `read_console` → 0 новых ошибок

### Что осталось

- ✅ Painter2D skill tree graph (T-P19) — **отмести из «осталось»**, сделано
- ❌ CombatDiscipline поле в SkillNodeConfig (T-CB02) — фильтр по substring
- ❌ Real Receive*TargetRpc вместо reflection
- ❌ Drag-to-slot — Phase 2
- ❌ Toasts на learn/forget — пока Debug.Log