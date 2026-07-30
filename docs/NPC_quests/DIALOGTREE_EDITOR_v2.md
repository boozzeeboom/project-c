# DialogTree Custom Editor v2

> **Дата:** 2026-07-21
> **Задача:** Кастомный редактор DialogTree — карточки нод с цветовым кодированием, drag-and-drop условий, читаемый граф диалога.

---

## Что изменилось

### DialogueCondition.cs — drag-and-drop поля
- `requiredQuest` (QuestDefinition) — для QuestStateEquals, QuestStageEquals, QuestCompleted, QuestDiscovered
- `requiredNpc` (NpcDefinition) — для NpcAttitudeAtLeast
- `requiredItem` (ItemData) — для HasItem, CargoHasItem
- Хелперы: `GetResolvedQuestId()`, `GetResolvedNpcId()`, `GetResolvedItemName()`

### SpeakerRef.cs — drag-and-drop NPC
- `speakerNpc` (NpcDefinition) — для speakerKind=Npc
- Хелпер: `GetResolvedNpcId()`

### DialogueConditionDrawer.cs — обновлён
- Контекстно-зависимые ObjectField-ы: requiredQuest, requiredNpc, requiredItem
- Строковый фолбэк visible только когда object ref не задан

### SpeakerRefDrawer.cs (NEW)
- PropertyDrawer: при Npc → ObjectField + строковый фолбэк
- При Player → зелёный лейбл «👤 Player (auto-detected)»
- При Narrator → жёлтый лейбл «📖 Narrator (italic, no portrait)»

### DialogTreeEditor.cs (NEW)
Кастомный Editor с карточками нод:

```
┌──────────────────────────────────────────────────────────────┐
│  💬 Мира — обычный разговор                                  │
│  ID: mira_default                                            │
├──────────────────────────────────────────────────────────────┤
│  🟢 Nodes: 5    ➡ Edges: 12    🔚 End: 4    ✅ All reachable│
├──────────────────────────────────────────────────────────────┤
│  🏠 greeting                    [🤖 Mira]  [➡3]  [▲][▼][×] │
│    "Приветствую, искатель знаний."                            │
│    ➡ "Расскажи о заданиях."  → quests        [Hide]    [×] │
│       ⚡ SwitchDialogTree  🔒 ×1                              │
│    ➡ "Поговорим о гильдии."  → about_guild    [Hide]    [×] │
│    🔚 "До свидания."         → end conversation [Hide]  [×]  │
├──────────────────────────────────────────────────────────────┤
│     quests                      [🤖 Mira]  [➡3]  [▲][▼][×] │
│     "Вот что у меня есть:"                                    │
│     ➡ "Взять квест: Сбор меди"  → accepted_0  [Hide]   [×]  │
│        ⚡ OfferQuest                                          │
│     ...                                                       │
├──────────────────────────────────────────────────────────────┤
│                        [+ Add Node]                           │
└──────────────────────────────────────────────────────────────┘
```

### Ключевые фичи редактора
- **Цветовое кодирование**: NPC=синий, Player=зелёный, Narrator=жёлтый, EndConversation=серый
- **Карточки нод**: свёрнуты по умолчанию, показывают текст реплики и список рёбер
- **Рёбра**: label → target (с иконкой 🔚 для end), баджи действий (⚡ OfferQuest) и условий (🔒 ×2)
- **Сводка**: N nodes, E edges, статус достижимости (✅/⚠/❌)
- **Валидация**: root missing → error, unreachable nodes → warning
- **▲▼×**: переупорядочивание и удаление нод/рёбер

### Runtime consumers обновлены
- `QuestServer.cs`:
  - `EvaluateSingleCondition` — HasItem использует `c.requiredItem` через `QuestWorld.ResolveItemId`
  - `EvaluateSingleCondition` — QuestStateEquals использует `c.GetResolvedQuestId()`
  - `BuildFilteredEdgeList` — speaker refId → `GetResolvedNpcId()`

---

## Затронутые файлы

| Файл | Изменение |
|------|-----------|
| `Dialogue/DialogueCondition.cs` | +requiredQuest, +requiredNpc, +requiredItem, +GetResolved*() |
| `Dialogue/SpeakerRef.cs` | +speakerNpc, +GetResolvedNpcId() |
| `Network/QuestServer.cs` | EvaluateSingleCondition → GetResolvedQuestId(), requiredItem; speaker → GetResolvedNpcId() |
| `Editor/DialogueConditionDrawer.cs` | ObjectField для quest/npc/item + строковый фолбэк |
| `Editor/SpeakerRefDrawer.cs` | NEW: PropertyDrawer с drag-and-drop |
| `Editor/DialogTreeEditor.cs` | NEW: CustomEditor с карточками нод |

---

## Как использовать

1. Открыть DialogTree .asset (например `MiraDefault.asset`)
2. Видна сводка: сколько нод, рёбер, статус достижимости
3. Каждая нода — карточка с цветом (NPC/Player/Narrator)
4. Развернуть ноду → редактировать текст, speaker (drag-and-drop NpcDefinition), рёбра
5. В условиях (DialogueCondition): перетащить QuestDefinition вместо ввода questId строкой
6. В SpeakerRef: перетащить NpcDefinition вместо ввода refId строкой
