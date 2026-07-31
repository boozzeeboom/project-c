# Unified Quest-Dialog-NPC Graph Architecture Plan

## 0. Diagnosis: Why 4 Attempts Failed

### Root Cause
Каждая реализация пыталась «читать и писать» напрямую в 3 разных ScriptableObject через UI-слой без промежуточной модели. `OnEdgeCreated` в `UnifiedQuestGraphView` (строка 601) **создаёт новый DialogueEdge** при драг-коннекте вместо того чтобы модифицировать существующий — это ломает данные.

### What Already Works
- Object-reference поля уже есть во всех трёх SO: `questRef`, `npcRef`, `dialogTreeRef`, `targetNpc`, `pickupItem`
- PropertyDrawer'ы (QuestStageDrawer, DialogueActionDrawer, etc.) — отличные, переиспользуем
- `QuestGraphNode` с `OwnerAsset`/`SourcePath`/`SourceData` — правильный паттерн, сохраняем

### What Needs to Change
- Убрать практику «UI создаёт новые объекты данных при драг-коннекте»
- Ввести `QuestGraphModel` — чистый C# адаптер над тремя SO
- Один GraphView с семантическими портами вместо трёх параллельных

---

## 1. Core Architecture: Ephemeral Graph Model

### Принцип
**Граф — это проекция состояния трёх SO.** Он не хранит свои данные. Все связи уже сериализованы как object-reference поля в NpcDefinition / DialogTree / QuestDefinition. Граф только:
- **Читает** SO → строит визуальные ноды и рёбра
- **Пишет** в SO при действиях пользователя (drag edge, edit field)

### GraphModel (чистый C#, не MonoBehaviour, не SO)

```
QuestGraphModel
├── Активы (читает, но не владеет):
│   ├── List<NpcDefinition> _npcs
│   ├── List<DialogTree> _dialogs
│   └── List<QuestDefinition> _quests
│
├── Ноды (строятся из активов):
│   ├── NpcNodeInfo { npc, position }
│   ├── DialogNodeInfo { tree, nodeIndex, position }
│   ├── QuestNodeInfo { quest, position }
│   ├── StageNodeInfo { quest, stageIndex, position }
│   ├── ObjectiveNodeInfo { quest, stageIndex, objIndex, position }
│   └── RewardNodeInfo { quest, position }
│
└── Рёбра (извлекаются из SO-полей, не хранятся отдельно):
    ├── NPC.defaultDialogTree    → NPC→Dialog
    ├── NPC.questOfferRefs[i]    → NPC→Quest
    ├── Edge.targetNodeId        → DialogNode→DialogNode (внутри дерева)
    ├── Edge.action.questRef     → DialogEdge→Quest (OfferQuest)
    ├── Edge.action.dialogTreeRef → DialogEdge→DialogTree (SwitchDialogTree)
    ├── Objective.targetNpc      → Objective→NPC
    ├── Stage.nextStageId        → Stage→Stage
    └── Quest.rewards            → LastStage→Reward
```

Ключевое: **model не дублирует данные, она — read-only view + write-through API.**

---

## 2. Semantic Port System

Каждый порт знает свою «роль» — это позволяет `OnEdgeCreated` понять, какое поле в каком SO обновить.

```csharp
public enum PortSemantic
{
    // === Outputs ===
    NpcOffersQuest,       // NPC → Quest (пишет в npc.questOfferRefs)
    NpcDefaultDialog,     // NPC → DialogTree root
    DialogEdgeAction,     // DialogEdge[i] → цель (quest/stage/other dialog)
    StageNext,            // Stage → next stage
    ObjectiveTarget,      // Objective → target NPC
    
    // === Inputs ===
    QuestOfferedBy,       // Квест готов принять связь
    DialogIn,             // Диалоговая нода принимает вход
    StageIn,              // Stage принимает вход
    NpcTargetedBy,        // NPC как цель objective
}
```

---

## 3. Edge Creation Logic (write-through)

| Source Port | Target Port | Write Action |
|---|---|---|
| NpcOffersQuest | QuestOfferedBy | `npc.questOfferRefs += quest` |
| NpcDefaultDialog | DialogIn (root node) | `npc.defaultDialogTree = tree` |
| DialogEdgeAction(idx) | DialogIn (same tree) | `edge.targetNodeId = targetNodeId` |
| DialogEdgeAction(idx) | StageIn | `edge.action.type=OfferQuest; edge.action.questRef=quest` |
| DialogEdgeAction(idx) | DialogIn (other tree) | `edge.action.type=SwitchDialogTree; edge.action.dialogTreeRef=otherTree` |
| ObjectiveTarget | NpcTargetedBy | `obj.targetNpc = npc; obj.objectiveType=TalkToNpc` |
| StageNext | StageIn | `stage.nextStageId = next.stageId` |

Каждая операция:
1. Модифицирует соответствующее поле в SO
2. Вызывает `EditorUtility.SetDirty(so)`
3. Не создаёт новых объектов данных

---

## 4. Node Visual Design

Каждая нода — `QuestGraphNode` (GraphView.Node) с:
- **Title bar**: цвет по типу (NPC=фиолетовый, Dialog=синий, Quest=оранжевый, Stage=зелёный, Objective=жёлтый, Reward=золотой)
- **Ports**: семантические, с осмысленными именами
- **Inline editor**: IMGUI-область, которая рендерит поля через SerializedProperty (как сейчас делает DialogNodeView)
- **Editable в Edit Mode**: текстовые поля появляются, в View Mode — только лейблы

---

## 5. Implementation Plan (6 steps)

### Step 1: `QuestGraphModel.cs` (~200 lines)
Создать класс-адаптер:
- `AddNpc(NpcDefinition)` → парсит npc.questOfferRefs, npc.defaultDialogTree
- `AddDialogTree(DialogTree)` → парсит nodes[], edges[], actions
- `AddQuest(QuestDefinition)` → парсит stages[], objectives[], rewards
- `GetNodes()` → возвращает плоский список node-infos
- `GetEdges()` → возвращает список edge-infos
- `SetConnection(PortSemantic from, PortSemantic to, object fromData, object toData)` → write-through

### Step 2: Node classes (`NpcNode`, `DialogNode`, `QuestNode`, `StageNode`, `ObjectiveNode`, `RewardNode`)
Каждая наследуется от `QuestGraphNode`, добавляет свои порты, свой IMGUI-контент.

### Step 3: `UnifiedGraphView.cs` (rewrite)
- BuildGraph() вызывает GraphModel.GetNodes() + GetEdges()
- OnEdgeCreated — один обработчик, смотрит на PortSemantic и вызывает model.SetConnection()
- Панорамирование, зум, grid (переиспользовать из QuestNodeGraphView)
- Edit mode toggle

### Step 4: `UnifiedGraphWindow.cs` (rewrite)
- Toolbar: +NPC field, +Quest field, Edit/Save/Revert, Fit, Clear
- Сохраняет позиции нод в EditorPrefs (по PersistKey)

### Step 5: Cleanup
- Старый `UnifiedQuestGraphView.cs` → переименовать в `UnifiedQuestGraphView_DEPRECATED.cs`
- `QuestGraphView.cs` (Painter2D) — оставить как read-only preview
- `QuestNodeGraphView.cs` — оставить как single-quest editor
- Обновить кнопки «Unified Graph» в трёх инспекторах

### Step 6: Verify
- Загрузить Mira (NPC) → увидеть её DialogTree → увидеть Quest → Objective → NPC
- Создать связь NPC1→Quest→NPC2 перетаскиванием
- Сохранить, переоткрыть — связи intact
- Проверить через Inspector что поля заполнены корректно

---

## 6. What We DON'T Do (Anti-Goals)
- ❌ Не создаём новый ScriptableObject для хранения графа
- ❌ Не дублируем данные из трёх SO
- ❌ Не используем string-based ID для новых связей (только object refs)
- ❌ Не трогаем runtime-код (QuestServer, QuestWorld, DialogWindow)
- ❌ Не усложняем: никаких JSON-сериализаций, бинарных форматов
