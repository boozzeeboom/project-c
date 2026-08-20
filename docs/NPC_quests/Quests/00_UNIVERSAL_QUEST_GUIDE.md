# Universal Quest Guide — ProjectC

> Канонический короткий гайдлайн для создания и исправления квестов. Не содержит данных конкретного квеста.
> Сначала читать этот файл, затем только реально затронутые assets/scripts.

## 1. Модель квеста

```text
NPC definition + DialogTree
        ↓
OfferQuest → AcceptQuest
        ↓
QuestDefinition: Stage → Objectives → nextStageId
        ↓
Completed → TryTurnIn у разрешённого NPC
        ↓
TurnedIn → QuestReward ровно один раз
```

- `QuestDefinition` — статические данные.
- `QuestInstance` внутри `QuestWorld` — runtime state игрока.
- Сервер (`QuestWorld`/`QuestServer`) — источник истины.
- `QuestStage.objectives` выполняются как `AND`: все `required=true` должны быть выполнены.
- `optional=true` не блокирует stage.
- Пустой `nextStageId` завершает последний stage и переводит квест в `Completed`.
- Награда квеста выдаётся только при `Completed → TurnedIn`, а не при обычном переходе stage.

## 2. Минимальный набор исследования

Перед изменениями не искать весь проект. Прочитать только:

1. Один существующий похожий `QuestDefinition`.
2. Один существующий `NpcDefinition` и его `DialogTree`.
3. `QuestObjective.cs`, `QuestStage.cs`, `QuestReward.cs`.
4. `DialogueAction.cs`, `DialogueCondition.cs`.
5. `QuestWorld.cs`: `TryAccept`, `TryTurnIn`, `TryAdvanceStage`, `EvaluateObjective`, `ApplyQuestRewards`.
6. `QuestServer.cs`: `RequestTalkToNpcRpc`, `FireDialogAction`, `BuildDialogStep`, проверку conditions.
7. `QuestDatabase.asset` и `QuestDatabase.cs`.
8. RU/EN localization tables.
9. Только те scene objects/NPC, которые участвуют в маршруте.

Пути проекта:

```text
Assets/_Project/Quests/Data/Quests/
Assets/_Project/Quests/Data/Npcs/
Assets/_Project/Quests/Data/Dialogs/
Assets/_Project/Quests/Data/QuestDatabase.asset
Assets/_Project/Quests/Core/QuestWorld.cs
Assets/_Project/Quests/Network/QuestServer.cs
Assets/_Project/Settings/Localization/Dialogue_Table*.asset
```

Всегда сверять реальные поля и runtime switch с кодом, а не собирать asset по памяти.

## 3. Стабильные ID и ссылки

До создания assets определить и зафиксировать:

- `questId`;
- `npcId` каждого участника;
- `treeId` диалогов;
- `stageId` и `objectiveId`;
- item/event/faction IDs;
- идентификаторы сцен и точек.

ID должны совпадать в quest asset, NPC definitions, dialogs, QuestDatabase, сцене и localization. После публикации ID не переименовывать.

Object reference имеет приоритет над строковым fallback:

- `QuestObjective.targetNpc` → `targetNpcId`;
- `QuestObjective.pickupItem` → `itemTradeItemId`;
- `DialogueAction.questRef`/`itemRef`/`npcRef` → строковые поля;
- `DialogueCondition.requiredQuest`/`requiredItem`/`requiredNpc` → `stringParam`.

Для новых assets использовать object references, а строковые поля заполнять согласованно для CSV/debug.

## 4. Создание QuestDefinition

Обязательные поля:

- уникальный `questId`;
- `displayName` и `description` как localization keys;
- `stages` в явном порядке;
- `oneShot=true`, если квест не должен повторяться;
- `discoverable=true`, если он сначала появляется в журнале через `OfferQuest`;
- корректные `prerequisites`/faction gates;
- `rewards` только для финальной выдачи.

Для каждого stage:

- уникальный `stageId`;
- локализуемый `description`;
- objectives с уникальными `objectiveId`;
- `required`/`optional` выставлены явно;
- `nextStageId` указывает на существующий stage или пуст для конца;
- `onEnterActions` и `onCompleteActions` содержат только нужные atomic actions и идут в требуемом порядке.

Не оставлять stage, до которого нельзя дойти от `stages[0]`.

## 5. Типы objectives

### Поговорить с NPC

```text
objectiveType = TalkToNpc
 targetNpc = нужный NpcDefinition
 targetNpcId = тот же стабильный npcId
```

Правила:

- Проверяется успешное текущее взаимодействие, transient `HasNpcTalkEvent`.
- История `_npcTalkedTo`/`HasNpcTalkedTo` используется только для knowledge.
- Перемещение NPC не влияет на objective.
- Разговор до принятия квеста не должен засчитывать первый stage.
- После tick talk event очищается.

### Посетить статичную точку или зону

```text
objectiveType = ReachLocation
targetSceneId = реальный scene id
targetPosition = фактическая world-space координата
targetRadius = радиус в метрах
```

- Координаты брать из реального объекта сцены.
- Использовать только для статичных объектов/зон.
- Не заменять им разговор с перемещающимся NPC.
- `PlayerInZone` сейчас не использовать для progression: condition не реализован и возвращает `false` с warning.

### Иметь предмет

```text
objectiveType = HaveItem
pickupItem = ItemData
requiredQuantity = N
```

Проверяется количество в inventory. Допустим строковый fallback `itemTradeItemId`, но reference предпочтительнее.

### Принести/передать предмет NPC

```text
objectiveType = DeliverItem
pickupItem = ItemData
requiredQuantity = N
targetNpc = принимающий NPC
 targetNpcId = тот же npcId
```

В текущем MVP наличие предмета проверяется как у `HaveItem`, а предмет потребляется в `TryTurnIn`. Финальный turn-in должен происходить у правильного NPC.

### Репутация

```text
objectiveType = ReputationAtLeast
targetFaction = нужная фракция
reputationValue = порог
```

### Событие

```text
objectiveType = WaitForEvent или EventDriven
eventId = стабильный event id
```

Использовать только если в проекте существует реальный emitter этого event через `EmitEvent`/event bus.

### Неиспользуемые без отдельной проверки

- `KillEntity` сейчас runtime-stub и всегда остаётся невыполненным.
- `PlayerInZone` и `WasNodeVisited` не использовать для progression.
- `GiveCargoItem`/`TakeCargoItem` и `OpenService` требуют отдельной проверки текущей реализации перед дизайном квеста.

## 6. NPC и диалоги

### NPC definition

Для каждого NPC проверить:

- уникальный `npcId`;
- назначенный `DialogTree`;
- `questOfferRefs` для выдаваемых квестов;
- `questTurnInRefs` для принимаемых квестов.

Сценовый NPC должен быть экземпляром канонического prefab:

```text
Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab
```

Prefab не изменять. На scene instance назначить нужный `NpcController.definition`.

### Выдача квеста

Минимальный flow:

```text
greeting → offer → accepted
```

- `greeting → offer`: action `OfferQuest`.
- В action задать `questRef`; `stringParam` должен совпадать с `questId`.
- `offer → accepted`: action `AcceptQuest`.
- Для discoverable-квеста принять только при `QuestDiscovered`.
- После accept snapshot должен сразу содержать objectives entry stage.

### Stage-gated диалог

Для реплик NPC использовать conditions:

- `QuestStageEquals` — активный stage;
- `QuestStateEquals` — состояние квеста;
- `QuestCompleted` — завершённый квест;
- `QuestDiscovered` — квест предложен, но ещё не принят;
- `HasItem`, reputation/attitude conditions — только если они реально нужны.

`conditions[]` на одном edge объединяются через `AND`. Для `OR` создавать отдельные edges. Root node и все target nodes должны быть достижимы.

### Завершение и turn-in

- Финальная реплика должна быть доступна только для правильного состояния/stage.
- `CompleteObjective` на turn-in edge вызывает серверную проверку и `TryTurnIn`.
- NPC обязан содержать quest в `questTurnInRefs`.
- Награда не должна выдаваться через `TryAdvanceStage`, `onCompleteActions` или повторное открытие диалога.
- После успешной выдачи состояние должно стать `TurnedIn`.

## 7. Actions: что чем делать

- `OfferQuest` — добавить квест в `Discovered/Offered`.
- `AcceptQuest` — перевести квест в `Active`.
- `CompleteObjective` — завершить objective/запустить финальный turn-in flow.
- `FailQuest` — перевести квест в `Failed`.
- `DiscoverQuest` — событийно открыть квест.
- `GiveItem`/`TakeItem` — промежуточное изменение inventory; использовать explicit `itemRef`, `itemType`, `intParam`.
- `GiveCredits` — изменить валюту.
- `AddReputation`/`AddNpcAttitude` — изменить репутацию/отношение.
- `SetFlag`/`EmitEvent` — изменить world state или вызвать event-driven objectives.
- `SwitchDialogTree` — сменить дерево диалога NPC.
- `EndConversation` — закрыть диалог.

Atomic actions выполняются в порядке массива. Не рассчитывать на composite action.

## 8. Rewards

Финальные rewards задаются в `QuestDefinition.rewards`:

- `credits`;
- `items[]` для inventory;
- `cargoItems[]` для корабельного cargo;
- `reputation[]`;
- `unlocks[]`.

Для inventory item:

- использовать реальный `pickupItem`;
- сохранять корректный `ItemType` предмета;
- не подменять тип жёстко на `Resources`;
- проверять количество и правильный inventory slot.

Ожидаемый инвариант: повторное открытие диалога после `TurnedIn` не выдаёт reward повторно.

## 9. Регистрация и сцена

После создания добавить в `Assets/_Project/Quests/Data/QuestDatabase.asset`:

- QuestDefinition;
- все NPC definitions;
- все DialogTrees.

Не заменять существующие массивы регистрации целиком.

Если нужен NPC в сцене:

1. инстанцировать канонический NPC prefab;
2. назначить `NpcController.definition`;
3. проверить interaction distance;
4. статичные target objects оставить в канонической сцене;
5. сохранить сцену по её существующему canonical path;
6. удалить случайный duplicate scene, если он появился.

Не сериализовать YAML сцены вручную и не сохранять сцену в новый корневой путь.

## 10. Localization

Для каждого квеста добавить RU и EN keys для:

- quest display name/description;
- stage/objective descriptions;
- NPC display name;
- dialog node text;
- option labels;
- edge labels;
- fallback text в assets.

Перед отправкой DTO сервер локализует speaker, node и edge. UI локализует options и quest tracker через `Loc.Get`. Literal key не должен отображаться игроку.

## 11. Static checkpoint до Play Mode

Не переходить к следующему шагу при незакрытой ошибке:

- [ ] Unity refresh/compile выполнен.
- [ ] `check_compile_errors` → `No compile errors`.
- [ ] Все IDs совпадают и уникальны.
- [ ] QuestDatabase lookup возвращает quest/NPC/dialog.
- [ ] `GetUnreachableStages()` возвращает `0`.
- [ ] Dialog graphs не имеют unreachable nodes.
- [ ] Каждый `TalkToNpc` имеет `targetNpc` и `targetNpcId`.
- [ ] Каждый `ReachLocation` имеет реальные scene/world coordinates и radius.
- [ ] Deliver/HaveItem имеют item reference и quantity.
- [ ] Все conditions/actions реально поддержаны runtime.
- [ ] Turn-in NPC содержит quest в `questTurnInRefs`.
- [ ] Reward references и item types корректны.
- [ ] RU/EN localization keys существуют.
- [ ] Канонический prefab не изменён.
- [ ] Сцена и build settings не задублированы.

## 12. Обязательный Play Mode сценарий

Проверять не только компиляцию:

1. Найти quest giver и открыть диалог.
2. Убедиться, что предложение и accept работают.
3. Убедиться, что objective entry stage виден сразу.
4. Выполнить ровно требуемое действие: разговор, посещение, item/event и т.д.
5. Проверить переход stage и новый objective.
6. Проверить, что неправильное действие не продвигает квест.
7. Для moving NPC проверить, что используется interaction, а не координата.
8. Для статичной точки проверить radius и фактическое положение.
9. Дойти до финала и выполнить turn-in у правильного NPC.
10. Проверить reward, количество и тип.
11. Повторно открыть диалог и убедиться, что reward не выдан повторно.

Статус после только статических проверок: `compile-validated, runtime-not-tested`. После ручного прохода: `runtime-verified-by-user`.

## 13. Диагностика бага

Порядок всегда один:

1. Зафиксировать симптом и ID: NPC, quest, stage, objective, dialog, reward.
2. Найти владельца данных: asset, scene instance или runtime code.
3. Прочитать текущие значения до редактирования.
4. Сформулировать одну проверяемую причину.
5. Внести минимальную правку.
6. Повторить соответствующий static checkpoint.
7. Повторить Play Mode шаг, который сломался.
8. Новую архитектурную ошибку добавить в этот гайдлайн.

Не исправлять runtime/data-проблему перемещением объекта в сцене. Не исправлять проблему сцены изменением definition, если координаты/instance действительно неверны.

## 14. Жёсткие запреты

- Не изменять канонический NPC prefab.
- Не менять уже используемые stable IDs.
- Не использовать исторический `HasNpcTalkedTo` для текущего `TalkToNpc` objective.
- Не использовать `PlayerInZone`/`WasNodeVisited` как замену `ReachLocation`.
- Не использовать координату moving NPC для проверки разговора.
- Не переносить progress objectives между stages.
- Не выдавать финальную reward в нескольких местах.
- Не заменять тип предмета reward на `Resources` без причины.
- Не считать compile-check доказательством runtime-работы.
- Не сериализовать YAML сцены вручную.

## 15. После завершения

Обновить:

- краткий статус квеста и подтверждённые проверки;
- этот гайдлайн, если найден новый инвариант или типовой баг;
- рабочую документацию итерации;
- commit с понятным summary и hash.
