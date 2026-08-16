# First Auto Quest — Onboarding alfa

> Исторический гайдлайн реализации и исправления конкретного onboarding-квеста `onboarding_alfa`.
>
> Универсальный порядок создания новых квестов находится в `docs/NPC_quests/00_UNIVERSAL_QUEST_GUIDE.md`. Этот файл хранит только onboarding-специфику, реальные IDs, сценовые точки и историю найденных ошибок.

## 0. Текущий результат задания

Реализован control-slice квеста `onboarding_alfa`:

```text
meet_mira
  → go_repair
  → return_from_repair
  → go_market
  → return_from_market
  → Completed
  → turn-in у Mira
  → Key_light_ship
```

Что подтверждено автоматически:

- Unity-компиляция: `No compile errors`.
- В QuestDatabase находятся квест `onboarding_alfa` и NPC `onboarding_alfa`.
- Граф квеста не содержит недостижимых стадий.
- Графы `OnboardingAlfaDialog` и `MiraDefault` не содержат недостижимых узлов.
- Mira принимает `onboarding_alfa` на turn-in.
- Награда квеста имеет тип `Key`.

Что подтверждено пользователем в Play Mode:

- После принятия квеста цель отображается.
- Разговор с Mira переводит квест на `RepairManager`.
- `RepairManager` переводит цель на разговор с Mira.
- Без повторного разговора с Mira возвратная стадия не завершается автоматически.
- `MarketZone_Primium` переводит цель на разговор с Mira.
- После финального разговора с Mira выдаётся ключ.

Runtime-проверка подтверждена пользователем. Автоматические проверки по-прежнему не заменяют ручной Play Mode-тест после следующих изменений.

---

## 1. Жёсткие ограничения проекта

Перед любой правкой сначала проверить эти ограничения.

1. Не изменять и не удалять канонический NPC-префаб:
   `Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab`
2. Для новых NPC использовать экземпляр канонического префаба, а не отдельный самодельный prefab.
3. Не переименовывать и не удалять стабильные ID:
   - `onboarding_alfa`
   - `mira_01`
   - `Key_light_ship`
   - `collect_copper_ore`
   - `PAD-005`
4. Не переименовывать `ItemType.Key` и не заменять его на `Resources`.
5. Не использовать для посещения точек маршрута условия `PlayerInZone` или `WasNodeVisited`: в текущем `QuestServer.EvaluateSingleCondition` они не реализованы и возвращают `false` с предупреждением.
6. Для статичной точки/зоны использовать objective `ReachLocation` с фактическими world-space координатами и радиусом.
7. Для разговора с NPC использовать objective `TalkToNpc` с `targetNpcId` и ссылкой `targetNpc` на definition NPC.
8. `TalkToNpc` должен проверять transient-событие текущего взаимодействия, а не исторический факт «с NPC когда-то говорили». История общения используется только для knowledge.
9. При принятии квеста сразу инициализировать progress objectives текущего stage; после перехода stage очищать старый progress и создавать новый.
10. Не сохранять сцену в новый корневой путь вроде `Assets/WorldScene_0_0.unity`. Канонический путь сцены:
   `Assets/_Project/Scenes/World/WorldScene_0_0.unity`
11. После изменений сцены проверить, что активная сцена снова имеет канонический путь и в проекте не остался случайный дубль.
12. Не считать прохождение задачи завершённым только по успешной компиляции: runtime-flow всё равно должен быть проверен в Play Mode пользователем.

---

## 2. Опорные объекты и файлы

| Объект | Путь / имя | Роль |
|---|---|---|
| Канонический NPC prefab | `Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab` | Шаблон всех NPC-сценовых экземпляров |
| NPC в сцене | `NPC` | Корневой объект NPC в `WorldScene_0_0` |
| Primum | `WorldRoot_0_0/Primum` | Контекст объектов маршрута |
| Mira definition | `Assets/_Project/Quests/Data/Npcs/Mira.asset` | `mira_01`, выдача/turn-in onboarding-квеста |
| Onboarding NPC definition | `Assets/_Project/Quests/Data/Npcs/OnboardingAlfa.asset` | `onboarding_alfa`, предлагает квест |
| Mira dialog | `Assets/_Project/Quests/Data/Dialogs/MiraDefault.asset` | Реплики и stage-gated маршрут у Mira |
| Onboarding dialog | `Assets/_Project/Quests/Data/Dialogs/OnboardingAlfaDialog.asset` | Greeting → offer → accept |
| Quest definition | `Assets/_Project/Quests/Data/Quests/onboarding_alfa.asset` | 5 стадий маршрута |
| Quest DB | `Assets/_Project/Quests/Data/QuestDatabase.asset` | Регистрация NPC/dialog/quest |
| Reward item | `Assets/_Project/Resources/Items/Key_light_ship.asset` | Ключ корабля, `ItemType.Key` |
| World scene | `Assets/_Project/Scenes/World/WorldScene_0_0.unity` | NPC и объекты маршрута |
| Localization | `Assets/_Project/Settings/Localization/Dialogue_Table*.asset` | RU/EN строки |

Ожидаемые сценовые объекты маршрута:

- `WorldRoot_0_0/Primum/RepairManager`
- `WorldRoot_0_0/Primum/MarketZone_Primium`
- `NPC/[Onboarding alfa]`
- `NPC/[Mira]`

Имена путей могут включать hierarchy-wrapper-ы Unity. Перед редактированием искать реальные объекты по иерархии, а не придумывать путь.

---

## 3. Правильная последовательность реализации

### Шаг 1. Исследовать проект до изменений

Перед авторингом найти и прочитать:

1. Канонический NPC prefab и его `NpcController`.
2. `Mira.asset`, чтобы повторить существующую структуру `NpcDefinition`.
3. `MiraDefault.asset`, чтобы повторить формат `DialogTree`, node, option и edge.
4. Существующий простой quest asset, чтобы повторить сериализацию `QuestDefinition`, stages, objectives и rewards.
5. `QuestDatabase.asset`, чтобы понять массивы регистрации.
6. `Dialogue_Table_RU.asset` и `Dialogue_Table_EN.asset`, чтобы сохранить формат ключей локализации.
7. `QuestServer.cs` и `QuestWorld.cs`, особенно:
   - обработку `ReachLocation`;
   - `FireDialogAction`;
   - `TryAdvanceStage`;
   - `TryTurnIn`;
   - `ApplyQuestRewards`;
   - `BuildDialogStep`;
   - `EvaluateSingleCondition`.

Цель исследования: сначала подтвердить реальные имена полей и поддержанные enum/condition/action, а не строить YAML/asset вручную по предположению.

### Шаг 2. Определить стабильные идентификаторы

До создания assets зафиксировать:

- quest ID: `onboarding_alfa`;
- onboarding NPC ID: `onboarding_alfa`;
- Mira NPC ID: `mira_01`;
- reward item ID/path: `Key_light_ship`;
- dialog tree ID: `onboarding_alfa`;
- stage IDs:
  - `meet_mira`;
  - `go_repair`;
  - `return_from_repair`;
  - `go_market`;
  - `return_from_market`.

Эти значения затем должны совпадать одновременно в quest asset, NPC definitions, dialogs, database и localization.

### Шаг 3. Создать quest definition

Создать:

`Assets/_Project/Quests/Data/Quests/onboarding_alfa.asset`

Параметры:

- `oneShot: true`;
- `discoverable: true`;
- `prerequisites: []`;
- `faction: None`;
- reward:
  - `pickupItem: Key_light_ship`;
  - `count: 1`.

Порядок stages:

| Stage ID | Objective ID | Objective | Target |
|---|---|---|---|
| `meet_mira` | `talk_mira_first` | `TalkToNpc` | `mira_01`, через `targetNpc = Mira.asset` |
| `go_repair` | `reach_repair_manager` | `ReachLocation` | `(40020.4, 2501.84, 40139.2)`, radius `8` |
| `return_from_repair` | `return_to_mira_after_repair` | `TalkToNpc` | `mira_01`, через `targetNpc = Mira.asset` |
| `go_market` | `reach_market_zone` | `ReachLocation` | `(40096.5, 2510, 40140.6)`, radius `12` |
| `return_from_market` | `return_to_mira_after_market` | `TalkToNpc` | `mira_01`, через `targetNpc = Mira.asset` |

Важно:

- Каждый `TalkToNpc` должен ссылаться на реальное определение нужного NPC и содержать его стабильный `npcId`.
- Точки `ReachLocation` использовать только для статичных объектов/зон: брать координаты из фактического расположения объектов в сцене.
- Для `ReachLocation` указывать корректный world-space target и радиус.
- Возврат к Mira после `RepairManager` и `MarketZone_Primium` закрывать только фактом разговора с `mira_01`.
- Не подменять разговор с перемещающимся NPC координатным objective: координата NPC меняется, `TalkToNpc` остаётся стабильным.
- Не использовать persistent `HasNpcTalkedTo` для завершения текущего stage: одно взаимодействие должно быть transient и не переноситься на следующие этапы.

### Шаг 4. Создать definition onboarding NPC

Создать:

`Assets/_Project/Quests/Data/Npcs/OnboardingAlfa.asset`

Минимум проверить:

- `npcId: onboarding_alfa`;
- faction: `Neutral`;
- ссылка на dialog tree `OnboardingAlfaDialog`;
- выдаваемый квест `onboarding_alfa`.

Не добавлять onboarding-квест в `Mira.asset` как offer, если по дизайну его предлагает отдельный onboarding NPC. Для Mira использовать turn-in-ссылку.

### Шаг 5. Создать onboarding dialog

Создать:

`Assets/_Project/Quests/Data/Dialogs/OnboardingAlfaDialog.asset`

Структура:

```text
greeting → offer → accepted
```

Правила:

1. `treeId: onboarding_alfa`.
2. Корневой node: `greeting`.
3. Edge `greeting → offer` выполняет `OfferQuest`.
4. Для `OfferQuest` использовать:
   - `stringParam: onboarding_alfa`;
   - `questRef: onboarding_alfa.asset`.
5. Edge `offer → accepted` выполняет `AcceptQuest`.
6. `AcceptQuest` должен быть защищён условием `QuestDiscovered(onboarding_alfa)`.
7. Все node text, option labels и edge labels должны иметь localization key и literal fallback через `Loc.Get`.
8. После создания проверить, что root node действительно достижим и в графе нет висячих узлов.

### Шаг 6. Настроить Mira как stage-gated guide и turn-in NPC

Изменить:

`Assets/_Project/Quests/Data/Npcs/Mira.asset`

Настроить:

- `questOfferRefs: []`;
- `questTurnInRefs: [onboarding_alfa]`.

Изменить:

`Assets/_Project/Quests/Data/Dialogs/MiraDefault.asset`

Добавить stage-gated edges из `greeting`:

- `QuestStageEquals(meet_mira)` → `meet`;
- `QuestStageEquals(go_repair)` → `repair`;
- `QuestStageEquals(go_market)` → `market`;
- `QuestStateEquals(Completed)` → `reward`.

Для завершённого состояния:

- edge `reward` должен выполнять `CompleteObjective`;
- server-side обработка должна довести квест до `TryTurnIn`;
- reward нельзя выдавать повторно при каждом повторном открытии диалога.

`return_from_repair` и `return_from_market` — это этапы разговора с Mira. Они закрываются через `TalkToNpc` на `mira_01`; `ReachLocation` применяется только к статичным `RepairManager` и `MarketZone_Primium`.

### Шаг 7. Зарегистрировать assets в QuestDatabase

В:

`Assets/_Project/Quests/Data/QuestDatabase.asset`

добавить ссылки на:

- `OnboardingAlfa.asset`;
- `OnboardingAlfaDialog.asset`;
- `onboarding_alfa.asset`.

Сохранить существующие записи Mira, Zipun и sample collect-квеста. Не заменять массивы регистрации целиком, если требуется только добавить новые элементы.

После регистрации проверить через runtime/editor query:

- `QuestDatabase.GetQuest("onboarding_alfa")` возвращает quest;
- `QuestDatabase.GetNpc("onboarding_alfa")` возвращает NPC;
- dialog tree находится по ожидаемому ID.

### Шаг 8. Исправить runtime-localization

Проверить UI/server pipeline:

1. `Assets/_Project/Quests/UI/DialogWindow.cs`
   - labels вариантов ответа проходят через `ProjectC.Localization.Loc.Get`.
2. `Assets/_Project/Quests/UI/QuestTracker.cs`
   - display name квеста и objective description проходят через `Loc.Get`.
3. `Assets/_Project/Quests/Network/QuestServer.cs`
   - `BuildDialogStep` локализует перед отправкой:
     - speaker display name;
     - edge label;
     - node text.

Локализовать нужно до формирования DTO, иначе клиент может получить literal localization keys вместо текста.

### Шаг 9. Исправить выдачу предмета награды

В:

`Assets/_Project/Quests/Core/QuestWorld.cs`

в `ApplyQuestRewards` использовать `ri.pickupItem.itemType`, а не жёстко заданный `ItemType.Resources`.

Причина: `Key_light_ship` должен остаться `ItemType.Key` (`ItemType.Key = 8`), иначе квест формально выдаст item, но нарушит slot/inventory-логику ключей.

Проверка:

- asset `Key_light_ship.asset` существует;
- его item ID стабилен;
- его `itemType` равен `Key`;
- reward в quest ссылается именно на этот item.

### Шаг 10. Добавить localization keys

В RU и EN таблицы:

`Assets/_Project/Settings/Localization/Dialogue_Table*.asset`

добавить строки для:

- NPC onboarding alfa;
- quest display name и description;
- всех пяти stage/objective labels;
- onboarding dialog nodes/options/edges;
- Mira route nodes/options/edges;
- fallback-текстов, используемых в dialog assets.

Сохранять единый namespace-стиль:

```text
dialogue.quest.onboarding_alfa.*
dialogue.tree.onboarding_alfa.*
```

После добавления проверить обе локали, а не только RU.

### Шаг 11. Разместить onboarding NPC в сцене

Сцена:

`Assets/_Project/Scenes/World/WorldScene_0_0.unity`

Действия:

1. Инстанцировать канонический prefab под `NPC`.
2. Назвать объект `[Onboarding alfa]`.
3. Поставить NPC в фактическую точку рядом с `Ангар1 средняя часть.001`:
   - position `(39832.93, 2532.90, 40000.89)`.
4. На `NpcController` назначить:
   - `definition = OnboardingAlfa.asset`;
   - `interactionDistance = 3`.
5. Не менять сам канонический prefab.
6. Сохранить строго в канонический путь сцены.
7. После сохранения проверить активную сцену и удалить случайный дубль, если `save_scene` создал файл в `Assets/WorldScene_0_0.unity`.

### Шаг 12. Выполнить статические проверки

До Play Mode выполнить:

1. Refresh/compile Unity project.
2. `check_compile_errors`.
3. Проверку graph reachability:
   - quest unreachable stages = `0`;
   - onboarding dialog unreachable nodes = `0`;
   - Mira dialog unreachable nodes = `0`.
4. Проверку database lookup.
5. Проверку Mira turn-in reference.
6. Проверку reward type = `Key`.
7. Проверку, что статичные точки используют `ReachLocation`, а этапы разговора с NPC — `TalkToNpc`.
8. Проверку, что у каждого `TalkToNpc` заполнены `targetNpcId` и `targetNpc`.
9. Проверку отсутствия accidental scene duplicate.

Если проверка падает, сначала исправить причину и повторить тот же checkpoint. Не переходить к следующему этапу с незакрытой ошибкой.

### Шаг 13. Передать runtime-проверку пользователю

Play Mode сценарий:

1. Запустить `WorldScene_0_0`.
2. Найти `[Onboarding alfa]`.
3. Открыть диалог, получить предложение `onboarding_alfa`.
4. Принять квест.
5. Проверить стадию `meet_mira`.
6. Дойти до Mira и проверить переход на `go_repair`.
7. Дойти до `RepairManager` и проверить `go_repair → return_from_repair`.
8. Найти Mira по её текущей позиции и именно открыть с ней разговор; проверить, что `return_from_repair → go_market`.
9. Дойти до `MarketZone_Primium` и проверить `go_market → return_from_market`.
10. Найти Mira по её текущей позиции и именно открыть с ней разговор; проверить, что `return_from_market → Completed`/turn-in.
11. Проверить получение ровно одного `Key_light_ship` типа `Key`.
12. Проверить повторное открытие диалога: награда не должна выдаваться повторно.

Пользователь подтвердил этот сценарий в Play Mode. После любых следующих изменений статус снова считать неподтверждённым до повторного ручного прохода.

---

## 4. Что исправлять при типовых ошибках

### Квест не появляется у onboarding NPC

Проверить по порядку:

1. На NPC назначен `OnboardingAlfa.asset`, а не только имя объекта `[Onboarding alfa]`.
2. NPC ID равен `onboarding_alfa`.
3. В definition есть offer reference на quest.
4. Dialog tree действительно назначен NPC.
5. `OnboardingAlfaDialog` зарегистрирован в QuestDatabase.
6. `onboarding_alfa` зарегистрирован в QuestDatabase.
7. `greeting → offer` имеет `OfferQuest` и правильный `stringParam`.

### Квест предлагается, но не принимается

Проверить:

1. `offer → accepted` выполняет `AcceptQuest`.
2. Условия используют `QuestDiscovered(onboarding_alfa)`.
3. `QuestDatabase.GetQuest("onboarding_alfa")` возвращает тот же asset, на который ссылается edge.
4. ID не отличается регистром/символом.

### Mira не показывает нужную реплику

Проверить:

1. Quest stage ID совпадает с `meet_mira`, `go_repair` или `go_market`.
2. У Mira в сцене и в asset используется `mira_01`.
3. Stage-gated edges находятся на достижимом node `greeting`.
4. Проверить порядок и приоритет edges: completed route не должен маскировать активную stage route.
5. Проверить локализацию server-side и client-side.

### Стадия не продвигается на статичной точке

Проверить:

1. Для статичного объекта objective имеет тип `ReachLocation`.
2. Target координаты записаны в world-space.
3. Координаты и радиус соответствуют реальному объекту сцены.
4. `QuestWorld.PlayerPositionProvider` назначается `QuestServer`.
5. Не использовать `PlayerInZone` или `WasNodeVisited` как замену `ReachLocation`.
6. Проверить, что polling/tick квеста выполняется во время игры.

### Возврат к перемещающемуся NPC не засчитывается

Проверить:

1. Objective имеет тип `TalkToNpc`, а не `ReachLocation`.
2. `targetNpcId` равен стабильному ID NPC, для Mira — `mira_01`.
3. `targetNpc` ссылается на реальный `Mira.asset`.
4. Игрок действительно вызывает `RequestTalkToNpc`/открывает диалог с этим NPC.
5. NPC может находиться в любой точке своего маршрута: не проверять его текущую позицию как фиксированную координату.
6. `QuestWorld.EvaluateObjective` использует transient `HasNpcTalkEvent`, а не исторический `HasNpcTalkedTo`.
7. `_npcTalkEvents` очищается после tick, а progress текущего stage не переносится на следующий stage.

### Ключ выдан как ресурс или не попал в правильный слот

Проверить:

1. `Key_light_ship.asset` существует.
2. `pickupItem.itemType == ItemType.Key`.
3. `QuestWorld.ApplyQuestRewards` использует `ri.pickupItem.itemType`.
4. ID награды не заменён другим предметом.
5. `TryTurnIn` вызывается после `Completed`.
6. Не добавлять выдачу награды вручную в `TryAdvanceStage`, иначе возможен double-grant.

### При повторном открытии диалога награда выдаётся снова

Проверить:

1. Reward выдаётся только через `TryTurnIn`.
2. `TryAdvanceStage` не вызывает повторную выдачу.
3. Квест `oneShot: true`.
4. Состояние переводится в `TurnedIn`, а не остаётся `Completed` после успешного hand-in.
5. Edge `reward` не вызывается повторно как обычное действие без проверки состояния.

### Локализация отображает ключи

Проверить все три слоя:

1. DialogueWindow: option labels → `Loc.Get`.
2. QuestTracker: quest/objective text → `Loc.Get`.
3. QuestServer.BuildDialogStep: speaker, edge label, node text → `Loc.Get` до DTO.

Также проверить наличие ключа в RU и EN таблицах и literal fallback в asset.

### После сохранения пропала или задублировалась сцена

Проверить:

1. Активная сцена: `Assets/_Project/Scenes/World/WorldScene_0_0.unity`.
2. Нет случайного файла `Assets/WorldScene_0_0.unity`.
3. Build Index сцены остаётся `1`.
4. После исправления выполнить refresh и повторно проверить scene path/loaded scene.
5. Не сериализовывать YAML сцены вручную и не заменять сцену бинарным файлом без отдельного подтверждения пользователя.

---

## 5. Формат работы с каждой следующей ошибкой

Когда пользователь сообщает баг, работать так:

1. Зафиксировать симптом буквально: NPC/стадия/диалог/награда/локализация/сцена.
2. Найти asset/code path, который отвечает за симптом.
3. Сначала прочитать текущий asset/script и проверить реальные значения.
4. Сформулировать одну проверяемую причину.
5. Внести минимальную правку, не затрагивая канонический prefab и стабильные IDs.
6. Повторить соответствующий статический checkpoint.
7. Если причина новая, добавить её в раздел `Что исправлять при типовых ошибках`.
8. Если меняется архитектурная последовательность, обновить раздел `Правильная последовательность реализации`.
9. Обновить раздел `Текущий результат`, если изменились подтверждённые/неподтверждённые проверки.
10. Только после успешной проверки выполнить commit и записать hash в документацию итерации.

Не исправлять симптом в сцене, если причина находится в definition/dialog/database/runtime-коде. Сначала определить владельца данных.

---

## 6. Контрольный чек-лист перед сдачей

- [ ] Все stable IDs совпадают.
- [ ] Канонический prefab не изменён.
- [ ] Quest asset зарегистрирован в QuestDatabase.
- [ ] NPC definition зарегистрирован и назначен scene instance.
- [ ] Dialog asset зарегистрирован и назначен нужному NPC.
- [ ] Quest graph: unreachable stages = `0`.
- [ ] Dialog graphs: unreachable nodes = `0`.
- [ ] Статичные точки используют `ReachLocation` с фактическими world coordinates.
- [ ] Все этапы разговора с NPC используют `TalkToNpc` с `targetNpcId` и `targetNpc`.
- [ ] Возвраты после `RepairManager` и `MarketZone_Primium` требуют разговора с Mira, а не нахождения в координате.
- [ ] Mira принимает `onboarding_alfa` как turn-in.
- [ ] Reward item = `Key_light_ship`.
- [ ] Reward type сохраняется как `ItemType.Key`.
- [ ] RU и EN localization keys присутствуют.
- [ ] UI и server dialog pipeline используют `Loc.Get`.
- [ ] Сцена сохранена по каноническому пути.
- [ ] Accidental scene duplicate удалён.
- [ ] `check_compile_errors` возвращает `No compile errors`.
- [x] Play Mode проверен пользователем; после следующих изменений требуется повторная проверка.
- [ ] Новые найденные ошибки добавлены в этот файл.
- [ ] Commit hash записан в рабочей документации итераций.

---

## 7. История текущего задания

- Реализованный квест: `onboarding_alfa`.
- Сценовый NPC: `NPC/[Onboarding alfa]`.
- Награда: `Key_light_ship`.
- Текущий compile-check: пройден.
- В текущем control-slice статичные переходы используют `ReachLocation`, а возвраты к Mira — `TalkToNpc`.
- Runtime-check: подтверждён пользователем в Play Mode.

---

## 8. Правило обновления этого гайда

Каждая исправленная ошибка должна оставлять после себя один из результатов:

- уточнённый шаг реализации;
- новый запрет/инвариант;
- новый пункт статической проверки;
- новый типовой симптом с диагностикой;
- изменение статуса runtime-проверки.

Если исправление не объяснено в этом файле, значит следующий проход квеста снова будет зависеть от памяти и вероятность повторить ошибку остаётся высокой.
