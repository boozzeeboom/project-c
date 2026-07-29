# План: Кастомный редактор QuestDefinition.asset

> **Дата:** 2026-07-21
> **Цель:** Сделать редактирование квестового ассета удобным для не-технаря: drag-and-drop, контекстно-зависимые поля, сводка наверху, плоские формы вместо многоуровневых массивов.

---

## Архитектура редактора

**Единый `[CustomEditor(typeof(QuestDefinition))]`** с тремя вкладками:

```
┌──────────────────────────────────────────────────────────┐
│  📜 Quest: collect_copper_ore      [Validate] [Save]    │
│  📋 Stages  │  🎁 Rewards  │  ⚙️ Prerequisites & Flags │
├──────────────────────────────────────────────────────────┤
│  [Сводка flow: Stage1 → Stage2 → ... → Rewards]         │
│  [Редактирование выбранного stage / награды]             │
└──────────────────────────────────────────────────────────┘
```

---

## Фаза 1: Object-reference поля в data-классах

Добавляем поля drag-and-drop **поверх** существующих строковых (для обратной совместимости с CSV).

### 1.1 QuestObjective — `targetNpc` (NpcDefinition)
- **Файл:** `Quests/Quests/QuestObjective.cs`
- **Добавить:** `public NpcDefinition targetNpc;` — для TalkToNpc, DeliverItem
- **Приоритет:** если `targetNpc != null` → использовать его npcId, иначе `targetNpcId`

### 1.2 QuestPrerequisite — `requiredQuest` (QuestDefinition)
- **Файл:** `Quests/Quests/QuestPrerequisite.cs`
- **Добавить:** `public QuestDefinition requiredQuest;` — для QuestCompleted
- **Приоритет:** если `requiredQuest != null` → использовать его questId, иначе `stringParam`

### 1.3 QuestRewardUnlock — `unlockDialog` (DialogTree)
- **Файл:** `Quests/Quests/QuestReward.cs`
- **Добавить:** `public DialogTree unlockDialog;` — для unlockType=DialogTree
- **Приоритет:** если `unlockDialog != null` → использовать его, иначе `unlockId`

---

## Фаза 2: PropertyDrawer'ы (5 шт.)

Все drawer'ы следуют паттерну `DialogueConditionDrawer` — показывают только релевантные поля.

### 2.1 QuestObjectiveDrawer
- **Файл:** `Quests/Editor/QuestObjectiveDrawer.cs` (NEW)
- **Логика:** switch по `objectiveType` → показывать только нужные поля
- **TalkToNpc:** objectiveId, description, **targetNpc (ObjectField)**, targetNpcId (скрыто), optional, required
- **HaveItem:** objectiveId, description, **pickupItem (ObjectField)**, itemTradeItemId (скрыто), requiredQuantity, optional, required
- **DeliverItem:** objectiveId, description, **pickupItem**, **targetNpc**, requiredQuantity
- **ReachLocation:** objectiveId, description, targetSceneId, targetPosition, targetRadius
- **ReputationAtLeast:** objectiveId, description, targetFaction, reputationValue
- **EventDriven/WaitForEvent:** objectiveId, description, eventId
- **KillEntity:** objectiveId, description, targetEntityType, requiredQuantity

### 2.2 DialogueActionDrawer
- **Файл:** `Quests/Editor/DialogueActionDrawer.cs` (NEW)
- **Логика:** switch по `type` → показывать только нужные поля
- **GiveCredits:** intParam (credits amount)
- **AddReputation:** factionParam + intParam
- **AddNpcAttitude:** stringParam (npcId) + intParam
- **GiveItem/TakeItem:** itemId + itemType + intParam (count)
- **OfferQuest/AcceptQuest:** stringParam (questId)
- **CompleteObjective:** stringParam (questId) + stageIdParam (objectiveId)
- **EmitEvent/SetFlag:** stringParam (eventId/flagId)
- **OpenMarket:** stringParam (zoneId)
- **EndConversation:** (no params)
- И т.д.

### 2.3 QuestPrerequisiteDrawer
- **Файл:** `Quests/Editor/QuestPrerequisiteDrawer.cs` (NEW)
- **Логика:** switch по `type`
- **QuestCompleted:** **requiredQuest (ObjectField)**, stringParam (скрыто)
- **ReputationAtLeast:** factionParam + intParam
- **HaveItem:** stringParam (itemId) + intParam (count)
- **NpcAttitudeAtLeast:** stringParam (npcId) + intParam
- **FlagIsSet:** stringParam (flagId)
- **QuestActive/PlayerFaction:** stringParam

### 2.4 QuestRewardDrawer
- **Файл:** `Quests/Editor/QuestRewardDrawer.cs` (NEW)
- **Логика:** плоская форма — все секции видны сразу без свёрнутых массивов
- Секция «Credits»: credits (int field)
- Секция «Inventory Items»: список [pickupItem (ObjectField) × count] + кнопка [+]
- Секция «Cargo Items»: список [cargoItem (ObjectField) × count] + кнопка [+]
- Секция «Reputation»: список [faction ▼ + value] + кнопка [+]
- Секция «Unlocks»: список [unlockType ▼ + unlockDialog (ObjectField)] + кнопка [+]

### 2.5 QuestStageDrawer
- **Файл:** `Quests/Editor/QuestStageDrawer.cs` (NEW)
- **Логика:** карточки objectives/actions вместо свёрнутых массивов
- `stageId`, `description`, `nextStageId` (выпадающий список из sibling stages + [END])
- Objectives: каждая — компактная строка с иконкой типа, при клике раскрывается (использует QuestObjectiveDrawer)
- onEnterActions: каждая — строка с DialogueActionDrawer
- onCompleteActions: аналогично
- Кнопки [+ Add Objective], [+ Add Enter Action], [+ Add Complete Action]

---

## Фаза 3: Главный CustomEditor

### 3.1 QuestDefinitionEditor
- **Файл:** `Quests/Editor/QuestDefinitionEditor.cs` (NEW)
- **Три вкладки** через `GUILayout.Toolbar`
- **Верхняя панель**: сводка квеста (flow всех stages → rewards)
- **Вкладка «Stages»**: список stages с QuestStageDrawer, drag-and-drop переупорядочивание
- **Вкладка «Rewards»**: QuestRewardDrawer
- **Вкладка «Prerequisites & Flags»**: faction, minReputation, prerequisites[], oneShot, discoverable
- **Кнопка [Validate]**: запускает QuestDefinitionValidator для этого ассета, показывает результат цветным боксом
- **Авто-валидация**: при каждом изменении — иконка статуса (зелёная/жёлтая/красная)

---

## Фаза 4: Runtime resolution (минимальные правки)

### 4.1 QuestWorld.ResolveItemId
- Уже поддерживает `ItemData pickupItem` (из T-QREWARD)
- Без изменений

### 4.2 QuestWorld — поддержка NpcDefinition ref
- В `IsObjectiveSatisfied` для TalkToNpc: если `obj.targetNpc != null`, использовать `obj.targetNpc.npcId`
- В `EvaluateAndAdvanceStage`: аналогично

---

## Порядок реализации

| Шаг | Что | Статус |
|---|---|---|
| 1 | План (этот документ) | ✅ |
| 2 | QuestObjective.cs — добавить `targetNpc` | ✅ |
| 3 | QuestPrerequisite.cs — добавить `requiredQuest` | ✅ |
| 4 | QuestReward.cs — добавить `unlockDialog` в QuestRewardUnlock | ✅ |
| 5 | QuestObjectiveDrawer.cs | ✅ |
| 6 | DialogueActionDrawer.cs | ✅ |
| 7 | QuestPrerequisiteDrawer.cs | ✅ |
| 8 | QuestRewardDrawer.cs | ✅ |
| 9 | QuestStageDrawer.cs | ✅ |
| 10 | QuestDefinitionEditor.cs | ✅ |
| 11 | Runtime: QuestWorld.cs — targetNpc + requiredQuest резолв | ✅ |
| 12 | Runtime: QuestServer.cs — targetNpc резолв | ✅ |
| 13 | Validator: QuestDefinitionValidator.cs — targetNpc check | ✅ |
| 14 | Компиляция | ✅ 0 errors |
| 15 | git commit | ⬜ |
