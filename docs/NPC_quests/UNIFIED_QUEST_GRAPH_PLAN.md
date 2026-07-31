# Unified Quest Graph — план рефакторинга и реализации

> **Дата:** 2026-07-22
> **Цель:** Единый визуальный редактор для составления сложных квестов и диалогов в одном окне.
> **Подход:** A — «Тонкий слой» (расширение существующего GraphView над DialogTree + QuestDefinition).
> **База:** `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` (674 строки, GraphView).

---

## 0. Проблема

Сейчас квесты и диалоги — разные SO, разные редакторы. Дизайнер прыгает между `DialogTreeEditor`, `QuestDefinitionEditor`, `QuestNodeGraphWindow`, `NpcDefinitionEditor`. Нет одного места где видно:

- «Этот диалог ведёт к этому квесту»
- «Эта реплика NPC обусловлена состоянием этого квеста»
- «Этот NPC говорит эти фразы → даёт эти квесты → принимает эти квесты»

Нужен **единый граф** где dialog-ноды и quest-ноды сосуществуют, а связи между ними визуальны.

---

## 1. Архитектурное решение

### Подход A: Unified GraphView над существующими SO

```
┌─────────────────────────────────────────┐
│  UnifiedQuestGraphWindow (EditorWindow) │
│  ┌──────────────────────────────────┐   │
│  │  UnifiedQuestGraphView (GraphView)│   │
│  │                                   │   │
│  │  Читает:                          │   │
│  │  • QuestDefinition.stages[]       │   │
│  │  • QuestDefinition.rewards        │   │
│  │  • DialogTree.nodes[]             │   │
│  │  • DialogTree.nodes[].edges[]     │   │
│  │                                   │   │
│  │  Пишет сразу в те же SO:         │   │
│  │  • Редактирование node → DialogTree│   │
│  │  • Редактирование stage → QuestDef│   │
│  │  • Связь dialog→quest → action    │   │
│  └──────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

**Почему не Подход B (новый единый ассет):**
- Ломает обратную совместимость: все существующие DialogTree и QuestDefinition нужно мигрировать
- Дублирует данные: DialogTree и QuestDefinition продолжают существовать для runtime
- Высокий риск: если генератор сломается — квесты перестанут работать
- Много работы: новый ассет, новый редактор, генератор, мигратор

**Почему не BTGraph (Asset Store):**
- BTGraph — сторонний граф-фреймворк, требует изучения API с нуля
- Dialogue Quest Pack — расширение к BTGraph, а не самостоятельный ассет
- Вся существующая data-модель (DialogTree, QuestDefinition, PropertyDrawer'ы) несовместима
- Пришлось бы писать адаптер: BTGraph node ↔ твой SO
- `QuestNodeGraphView` уже даёт 80% того что нужно, на своём коде

---

## 2. Корневые баги текущего QuestNodeGraphView (нужно починить ДО unified-функционала)

### Баг #1: `OnGraphViewChanged` блокирует всё
Файл: `QuestNodeGraphView.cs` строки 559-577.
Текущий код запрещает удаление нод, перемещение нод, удаление auto-edges. Это сделано для «readonly» режима, но ломает базовые механики GraphView. Для обхода используется `_suppressReadOnly` флаг.

**Следствие:** schedule.Execute костыли (строки 71-81), Edit Mode задержка 40ms.

### Баг #2: Fixed-position layout
Файл: `QuestNodeGraphView.cs` строки 179-184.
Жёсткие `COL1_X=0, COL2_X=360, COL3_X=720, COL4_X=1100`. Добавление/удаление stage ломает позиционирование.

**Следствие:** граф «разъезжается», ноды накладываются друг на друга.

### Баг #3: `AddStage`/`DeleteStage` делают полный `LoadQuest`
Файл: `QuestNodeGraphView.cs` строки 467-487.
Каждая мутация → `ClearAllElements` + полный rebuild. Это медленно и вызывает мерцание.

**Следствие:** требуется `ForceAllNodesExpanded` с задержкой 30ms.

### Баг #4: Безымянные порты
Файл: `QuestNodeGraphView.cs` строки 523, 530. `port.portName = ""`.
Порты визуально неразличимы — непонятно куда тянуть ребро.

**Следствие:** невозможность показать «Yes»/«No» branching в unified графе.

---

## 3. План: три слоя

```
Слой 0: Починить фундамент (без новой функциональности)
  ↓
Слой 1: Unified Graph — Dialog + Quest в одном графе
  ↓
Слой 2: Новое окно + UX
```

---

## 4. Слой 0 — Починить фундамент (~6.5 часов)

### T-U01: Model-driven `OnGraphViewChanged`

**Файл:** `QuestNodeGraphView.cs`

Переписать `OnGraphViewChanged` с «запретить всё» на «разрешить всё, мутировать SO через binding»:

- `change.elementsToRemove` → разрешить удаление edges; при удалении — удалять соответствующий объект из SO
- `change.movedElements` → разрешить перемещение; сохранять позиции в сериализованный словарь на графе
- `change.edgesToCreate` → разрешить создание рёбер; добавлять edge в SO
- Убрать `_suppressReadOnly` флаг
- Убрать все `schedule.Execute` костыли для repaint

### T-U02: Node ↔ SO binding

**Файлы:** `QuestNodeGraphView.cs` + новый `QuestGraphNode.cs`

Вместо `Node` с текстовыми полями — наследник с source reference:

```csharp
public class QuestGraphNode : Node
{
    public ScriptableObject OwnerAsset;  // QuestDefinition или DialogTree
    public string SourcePath;           // "stages[0].objectives[1]"
    public object SourceData;           // QuestStage / QuestObjective / DialogueNode
}
```

- `SaveAll()` → `EditorUtility.SetDirty(OwnerAsset)` для уникальных ассетов
- `AddStage()` → создаёт `QuestStage` в SO + добавляет ОДНУ ноду в граф (без rebuild)
- `DeleteStage()` → удаляет из SO + `RemoveElement(нода)` (без rebuild)

### T-U03: Авто-лейаут

**Файл:** `QuestNodeGraphView.cs`

Заменить `COL1_X=0` на направленный древовидный layout:

- Вычисление позиций по BFS от корневой ноды
- Константы: `V_GAP=40f`, `H_GAP=60f`, `NODE_W=240f`
- Вызывается после каждого изменения графа в `OnGraphViewChanged`
- Сохраняет ручные позиции если нода была перемещена пользователем

### T-U04: Осмысленные порты

**Файл:** `QuestNodeGraphView.cs`

- У каждого порта читаемое имя (не пустая строка)
- Выходные порты: "→" / "True" / "False" / "OnComplete"
- Входные порты: "←" / "Prev"
- Цветовое кодирование: зелёный = success, красный = fail, серый = default

---

## 5. Слой 1 — Unified Graph (~6.5 часов)

### T-U05: DialogNodeView — новый тип ноды

**Файл:** новый `UnifiedQuestGraphView.cs` (расширяет `QuestNodeGraphView`)

Новый тип ноды для dialog-реплик:

| Свойство | Значение |
|---|---|
| Цвет | Синий (`0.3, 0.5, 1.0`) |
| Source Data | `DialogueNode` |
| Порты | 1 вход, N выходов (по одному на каждый `DialogueEdge`) |
| Заголовок | `🤖 {speakerName}: "{text_preview}"` |
| Контент | Speaker (NPC drag-drop), text, portraitEmotion |
| Edit | При двойном клике — TextField для редактирования текста |

### T-U06: Загрузка DialogTree в граф

**Файл:** `UnifiedQuestGraphView.cs`

- При открытии квеста → найти связанный `DialogTree`:
  - Поиск по всем `NpcDefinition`: у кого этот квест в `questOffers[]`
  - Взять `npc.defaultDialogTree`
  - Если несколько — показать выбор в toolbar'е
- Загрузить `DialogTree.nodes[]` как `DialogNodeView`
- Разместить dialog-ноды **над** quest-нодами (выше по Y)
- Рёбра между dialog-нодами = `DialogueEdge[].targetNodeId`

### T-U07: Связи Dialog ↔ Quest (пунктирные рёбра)

**Файл:** `UnifiedQuestGraphView.cs`

- `DialogueEdge` с `action.type == OfferQuest` → пунктирное ребро от DialogNode к первому QuestStageNode
- Цвет: оранжевый (`0.9, 0.5, 0.1`)
- Стиль: пунктир (dash)
- Label на ребре: «Offers: {questName}»
- При создании связи в графе (drag edge от DialogNode к QuestStageNode) → автоматически создать `DialogueEdge` с `action.type = OfferQuest`, `action.questRef = quest`

### T-U08: ConditionNodeView — ромбовидная нода

**Файл:** `UnifiedQuestGraphView.cs`

Нода-ромб для условий (if/else):

| Свойство | Значение |
|---|---|
| Цвет | Жёлтый (`0.9, 0.7, 0.2`) |
| Source Data | `DialogueCondition[]` |
| Порты | 1 вход, 2 выхода: «True» (зелёный), «False» (красный) |
| Контент | Тип условия + параметры (read-only preview) |

Переиспользует `DialogueConditionDrawer` для редактирования при двойном клике.

---

## 6. Слой 2 — Новое окно + UX (~2 часа)

### T-U09: `UnifiedQuestGraphWindow`

**Файл:** новый `UnifiedQuestGraphWindow.cs`

```csharp
[MenuItem("Tools/Project C/Quests/Unified Quest Graph", priority = 100)]
public class UnifiedQuestGraphWindow : EditorWindow
```

Toolbar:
```
[Quest: ▾ collect_copper_ore]  [Dialog Tree: ▾ MiraDefault]  [✏️ Edit] [💾 Save All] [⊡ Fit]  [+ Node ▾]
```

`+ Node ▾` dropdown:
- 🤖 Add Dialog Node
- 🔷 Add Condition Node
- 📋 Add Quest Stage
- 🎯 Add Objective
- 🎁 Add Reward

Status bar:
```
Nodes: 12  |  Edges: 18  |  ✅ All reachable  |  Quest: collect_copper_ore  |  Dialog: MiraDefault
```

### T-U10: Интеграция с существующими редакторами

- Кнопка «Open in Unified Graph» в `QuestDefinitionEditor.cs`
- Кнопка «Open in Unified Graph» в `DialogTreeEditor.cs`
- Двойной клик по элементу в `QuestDatabaseWindow` → открывает Unified Graph

---

## 7. Общий план тикетов

| Тикет | Слой | Что | ~Часов | Зависимости |
|---|---|---|---|---|
| **T-U01** | 0 | Model-driven `OnGraphViewChanged` | 3 | — |
| **T-U02** | 0 | Node ↔ SO binding | 2 | T-U01 |
| **T-U03** | 0 | Авто-лейаут | 1.5 | T-U01 |
| **T-U04** | 0 | Осмысленные порты | 1 | T-U01 |
| **T-U05** | 1 | DialogNodeView | 3 | T-U02, T-U04 |
| **T-U06** | 1 | Загрузка DialogTree в граф | 2 | T-U05 |
| **T-U07** | 1 | Связи Dialog↔Quest | 1.5 | T-U06 |
| **T-U08** | 1 | ~~ConditionNodeView~~ → 🚫 Отменён (см. T-U08_CONDITION_NODE_ANALYSIS.md) | — | — |

| **T-U09** | 2 | UnifiedQuestGraphWindow | 1.5 | T-U06, T-U07 |
| **T-U10** | 2 | Интеграция с редакторами | 0.5 | T-U09 |
| **Итого** | | | **~17 часов** | |

---

## 8. Что НЕ меняется

- **Runtime:** QuestServer, QuestWorld, DialogWindow — без изменений
- **SO формат:** DialogTree, QuestDefinition, NpcDefinition — без изменений
- **CSV-импорт:** продолжает работать (пишет строковые ID как fallback)
- **PropertyDrawer'ы:** все 5 продолжают работать (переиспользуются в нодах)
- **Валидация:** QuestDefinitionValidator — без изменений

---

## 9. Риски

| # | Риск | Severity | Митигация |
|---|---|---|---|
| 1 | `OnGraphViewChanged` переписывается с нуля — можно сломать существующий функционал | 🟡 | Сохранить старый файл, новый — отдельный `UnifiedQuestGraphView.cs` |
| 2 | Авто-лейаут может давать «прыгающие» ноды при каждом изменении | 🟢 | Сохранять ручные позиции, авто-лейаут только для новых нод |
| 3 | Загрузка двух SO в один граф — сложность синхронизации | 🟡 | Чёткий контракт: граф — view, SO — source of truth. Сохранение однонаправленное (граф → SO) |
| 4 | Большой квест (796 нод) может тормозить GraphView | 🟢 | Ленивая загрузка: показывать только первые 2 уровня, остальное — collapse |
| 5 | ConditionNode меняет структуру DialogTree (сейчас conditions внутри edges, а не отдельными нодами) | 🟡 | ConditionNode — визуальная абстракция. При сохранении разбирается в `DialogueEdge.conditions[]` |

---

## 10. Критерии приёмки

- [ ] Открыть `collect_copper_ore` → видно dialog-ноды Mira + quest stages + rewards в одном графе
- [ ] Переместить dialog-ноду → позиция сохраняется при переоткрытии
- [ ] Создать связь от DialogNode к QuestStageNode → в SO появляется `DialogueEdge.action = OfferQuest`
- [ ] Добавить ConditionNode между двумя DialogNode → в SO появляются `DialogueEdge.conditions[]`
- [ ] Сохранить → `DialogTree.asset` и `QuestDefinition.asset` обновлены
- [ ] F5 в Play Mode → квест по-прежнему проходится
- [ ] CSV-импорт → квесты по-прежнему создаются
- [ ] 0 compile errors
