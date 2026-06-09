# M17 — QuestGraphView (GraphView visualization)

> **Дата:** 2026-06-09
> **Сессия:** M17 (T-Q09b)
> **Roadmap:** расширяет `08_ROADMAP.md` §8.3.5
> **Статус:** ✅ DONE 2026-06-09 (verified by Roslyn)
> **Зависимости:** M16 ✅

---

## 1. Проблема (audit 2026-06-09)

**До M17:**
- ✅ M16 даёт **list view** + textual detail panel
- ❌ Нет **визуальной** схемы связей quest → stages → objectives → rewards
- ❌ Content creator не видит как objective "depends on" itemId 26 (Медная руда), как onEnter fires перед objective complete

**Жалоба:** "M16 показывает всё списком, а хочется видеть графом чтобы сразу понимать поток"

## 2. Что сделано

### Файлы

| Файл | Lines | Что |
|------|-------|-----|
| `QuestGraphView.cs` | ~340 | `QuestGraphView` + 4 node types |
| `QuestGraphWindow.cs` | ~110 | EditorWindow с toolbar + GraphView |

### Node types

| Node | Размер | Header | Body |
|------|--------|--------|------|
| `QuestNode` | 220×120 | `📜 questId` | displayName + description |
| `StageNode` | 200×80+ | `Stage N: stageId` | nextStageId + objective count + onEnter/onComplete counts |
| `ObjectiveNode` | 240×50 | `[type] objectiveId` | qty + itemTradeItemId + targetNpcId |
| `RewardNode` | 220×120 | `🎁 Rewards` | CR + items + reputation |

### Layout

```
[Quest ObjectField] [🔄] [⊡ Fit]
┌─────────────────────────────────────┐
│ Grid + GraphView                    │
│ ┌──────┐    ┌──────┐    ┌──────┐   │
│ │ Quest│───▶│Stage │───▶│Obj   │   │
│ │Node  │    │Node  │    │Node  │   │
│ └──────┘    └──────┘    └──────┘   │
│     │                               │
│     ▼                               │
│ ┌──────┐                            │
│ │Reward│                            │
│ │Node  │                            │
│ └──────┘                            │
└─────────────────────────────────────┘
[Status: Quest: collect_copper_ore | stages: 1 | CR=200]
```

### Меню

1. `Tools > ProjectC > Quests > Quest Graph View` — пустое окно
2. `Assets/ProjectC/Open Quest Graph` — в context menu Project window (только если выбран QuestDefinition)

## 3. Архитектурные решения

- **Unity GraphView framework** (`UnityEditor.Experimental.GraphView`): встроенный, mature, не требует 3rd-party
- **Read-only mode:** `OnGraphViewChanged` блокирует element removal (т.к. graph = visualization, не editing)
- **Standard manipulators:** ContentZoomer, ContentDragger, SelectionDragger, RectangleSelector
- **Per-kind node classes:** `QuestNode/StageNode/ObjectiveNode/RewardNode` — каждый со своими полями и цветами
- **Edge между stage → objective:** Input port (in) + Output port (objectives)
- **RewardNode connected to QuestNode:** visual hint что rewards apply to entire quest
- **UnityEditor.UIElements.ObjectField** в toolbar — стандартный Unity UI Toolkit asset picker
- **ObjectField value-changed callback:** auto-load quest при assign

## 4. Что НЕ сделано (out of scope M17)

- ❌ **Editable nodes** — drag/drop/create (M18, ~3 ч)
- ❌ **Save back to QuestDefinition** — M18
- ❌ **Conditional edges** (event-based transitions) — M18
- ❌ **Visual diff** между 2 versions — M19
- ❌ **Search/filter** — M19
- ❌ **Mini-map** (GraphView встроенный) — опционально M20
- ❌ **Localization** labels — пока русский

## 5. Verify

**Roslyn verify (2026-06-09):**
```
Found in: Assembly-CSharp-Editor
Window opened
Quest: collect_copper_ore stages=1
Loaded quest into graph
Graph elements: 14
```

14 elements = 1 QuestNode + 1 StageNode + 1 ObjectiveNode + 1 RewardNode + 10 edges/containers.

**Compile:** 0 errors ✅

## 6. Manual verify план

1. Открой Unity Editor
2. **Метод 1:** В Project window выбери `CollectCopperOre.asset` → правый клик → **Assets > ProjectC > Open Quest Graph** → window opens с графом
3. **Метод 2:** Меню **Tools > ProjectC > Quests > Quest Graph View** → window opens пустой → drag `CollectCopperOre.asset` в ObjectField → граф строится
4. **Кликни `⊡ Fit`** → камера fits all nodes
5. **Drag пустого фона** → pan
6. **Scroll wheel** → zoom
7. **Drag node** → move
8. **Кликни на stage node** → highlight + select
9. **Visual check:**
   - QuestNode (📜) слева
   - StageNode (Stage 0) справа
   - ObjectiveNode ещё правее (с item: 26)
   - RewardNode снизу от QuestNode
   - Edges соединяют: Quest→Stage, Stage→Objective, Quest→Reward

## 7. Следующие шаги

- **M18 (CRUD graph editor):** drag/drop создание stages, inline edit fields, save back to QuestDefinition. ~3 ч.
- **M19 (Search/filter):** search bar в M16 TreeView + filter by questId в graph. ~1 ч.
- **M20 (Mini-map + dark/light theme):** polish UX. ~1 ч.

M18 уже не в scope (deferred). M19 — на твой выбор.
