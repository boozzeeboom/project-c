# M19-T7: dialogs.csv — формат и пример

> **Дата:** 2026-06-09
> **Статус:** ✅ DONE (verified by Roslyn)
> **Файл:** `Assets/_Project/Quests/Editor/DialogCsvImporter.cs`
> **Sample:** `Assets/_Project/Quests/Import/example_dialogs.csv`

---

## 1. Концепция

**Один row = один edge (стрелка между нодами).** Узлы создаются автоматически из fromNodeId/toNodeId.

## 2. Формат файла

| Колонка | Обязательно | Что |
|----------|-------------|-----|
| treeId | да | ID диалогового дерева |
| fromNodeId | да | ID ноды-источника |
| fromText | да | Текст реплики (что говорит fromNodeId) |
| fromSpeaker | нет | `Npc: npcId` / `Player` / `Narrator` (default `Npc`) |
| edgeLabel | да | Текст кнопки выбора игрока |
| toNodeId | да | ID ноды-цели (пусто = end conversation) |
| hideIfUnavailable | нет | y/n (default y) |
| conditionType | нет | `QuestCompleted`, `ReputationAtLeast`, `HaveItem`, `QuestStateEquals`, ... |
| conditionStringParam | нет | questId / itemId / npcId / flag |
| conditionIntParam | нет | quantity / value |
| conditionFactionParam | нет | FactionId |
| actionType | нет | `OfferQuest`, `AcceptQuest`, `GiveCredits`, `AddReputation`, ... |
| actionStringParam | нет | questId / itemId / npcId |
| actionIntParam | нет | amount / delta |
| actionFactionParam | нет | FactionId |

Если колонка не используется — оставь пустой.

## 3. Пример (mira_default)

```csv
treeId,fromNodeId,fromText,fromSpeaker,edgeLabel,toNodeId,hideIfUnavailable,conditionType,conditionStringParam,conditionIntParam,conditionFactionParam,actionType,actionStringParam,actionIntParam,actionFactionParam
mira_default,greeting,Привет, путешественник! Что привело тебя сюда?,Npc: mira_01,Я просто осматриваюсь,greeting_end,FALSE,,,,,,
mira_default,greeting,Привет, путешественник! Что привело тебя сюда?,Npc: mira_01,У меня есть работа для тебя,offer_quest,TRUE,QuestCompleted,stage_intro demo,,OfferQuest,find_artifact,,
mira_default,greeting,Привет, путешественник! Что привело тебя сюда?,Npc: mira_01,У меня есть работа для тебя,offer_quest,FALSE,,,,OfferQuest,find_artifact,,
mira_default,offer_quest,У меня есть задание. Найди древний артефакт в руинах к северу. Это опасно.,Npc: mira_01,Я согласен!,quest_active,FALSE,,,,,,
mira_default,offer_quest,У меня есть задание. Найди древний артефакт в руинах к северу. Это опасно.,Npc: mira_01,Слишком опасно,reject,FALSE,,,,,,
mira_default,quest_active,Удачи, ищи внимательно! Вернёшься когда найдёшь артефакт.,Npc: mira_01,Принял!,done,FALSE,,,,,,
mira_default,done,Буду ждать тебя с находкой. Не подведи!,Npc: mira_01,До встречи,end,FALSE,,,,,,
mira_default,greeting_end,Заходи ещё! У нас тут всегда есть что-то интересное.,Npc: mira_01,Пока,end,FALSE,,,,,,
mira_default,end,,,,Player,Закончить разговор,,FALSE,,,,EndConversation,,
```

## 4. Что создаётся

После импорта `example_dialogs.csv`:

```
DialogTree: mira_default
├── rootNodeId: "greeting"
├── nodes: 5 (greeting, offer_quest, quest_active, done, greeting_end, end)
└── edges: 8 (greeting→greeting_end, greeting→offer_quest, ...)
```

**Структура:**
- `greeting` (NPC: mira_01) → "Я просто осматриваюсь" → `greeting_end`
- `greeting` (NPC: mira_01) → "У меня есть работа для тебя" → `offer_quest` (с condition, action)
- `offer_quest` (NPC: mira_01) → "Я согласен!" → `quest_active`
- ... и т.д.

**Узлы-сироты (end, reject)**: создаются автоматически, можно потом добавить edges к ним.

## 5. Как использовать (writer'у)

1. **Скопировать** `example_dialogs.csv` → переименовать → `myquest_dialogs.csv`
2. **Открыть** в Excel
3. **Заменить** колонки на свои
4. **Сохранить** в `Assets/_Project/Quests/Import/`
5. В Unity: **Tools > ProjectC > Quests > CSV Import/Export** → Browse
6. Окно **авто-подхватит** `myquest_dialogs.csv` рядом с `myquest.csv` (T7)
7. **Preview → Import** — dialog создан

## 6. Ключевые правила

| Правило | Пример |
|---------|--------|
| Если `fromText` пустой — нода не показывается (auto-terminator) | `end,,,,Player,Закончить...` |
| Если `toNodeId` пустой — EndConversation (без перехода) | `end,,,,Player,Закончить...,,FALSE,,,,EndConversation,,` |
| `hideIfUnavailable=TRUE` + condition false = edge скрыт | "offer_quest" с QuestCompleted condition невидим после выполнения |
| `fromSpeaker` по умолчанию = `Npc` (нужен refId) | `"Npc: mira_01"` или просто `"mira_01"` |
| `fromText` с запятыми → обернуть в `"..."` | `"Привет, путешественник!"` |
| Multi-line text → `"..."` с `\n` внутри | `"Привет!\nКак дела?"` |

## 7. Conditions & Actions полный список

### Conditions (DialogueConditionType)
- `HasItem` — есть предмет (itemId, qty)
- `QuestCompleted` — квест завершён (questId)
- `QuestActive` — квест активен
- `QuestDiscovered` — квест обнаружен
- `QuestStateEquals` — квест в конкретном состоянии (questId, state)
- `ReputationAtLeast` — репутация >= value (faction, value)
- `ReputationAtMost` — репутация <= value
- `NpcAttitudeAtLeast` — отношение NPC >= value (npcId, value)
- `FlagIsSet` — global flag = true (flagId)
- `WasNodeVisited` — этот dialog node уже был показан
- `TimeOfDay` — игровое время (e.g. "day" / "night")

### Actions (DialogueActionType)
- `OfferQuest` — предложить квест (questId)
- `AcceptQuest` — сразу принять (questId)
- `CompleteObjective` — отметить objective (questId, objectiveId)
- `FailQuest` — провалить квест (questId, reason)
- `DiscoverQuest` — добавить в discovered (questId)
- `GiveItem` / `TakeItem` (itemId, qty)
- `GiveCredits` (amount)
- `AddReputation` (faction, value)
- `AddNpcAttitude` (npcId, value)
- `OpenMarket` / `OpenService` (zone)
- `SetFlag` (flagId)
- `SwitchDialogTree` (treeId) — переход на другой dialog
- `EndConversation` — завершить (no params)

## 8. Verify (Roslyn 2026-06-09)

```
Import example_dialogs.csv:
  Trees: 1
  Tree: mira_default, Nodes: 5, Root: greeting, Total edges: 8
  Created: 1, Errors: 0 ✅
```

Структура проверена: корневой `greeting`, ноды правильно сгруппированы, edges корректно соединяют fromNodeId→toNodeId.
