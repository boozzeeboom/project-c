# Именные NPC — канонический пайплайн

> **Статус:** рабочий гайд. Сверено с кодом и сценой на 2026-11-26.
> **Область:** именные NPC (квестодатели, торговцы, капитаны), которые стоят в мире постоянно.
> **Не область:** процедурная толпа PRIMUM (`PrimumNpcCrowdDirector` / `PrimumCrowdNpc`).

Связанные документы:

- `docs/NPC_quests/Quests/00_UNIVERSAL_QUEST_GUIDE.md` — квесты, стадии, objectives, награды.
- `docs/NPC_quests/Faction/FACTION_ID_DATA_DRIVEN_PLAN.md` — фракции и data-driven `FactionDefinition`.
- `docs/NPC_quests/merchants/00_NPC_MERCHANT_GUIDE.md` — магазины именных NPC (правый блок в `DialogWindow`, личные таблицы `NpcMerchantTable`, тестовый режим «продаёт всё», репутация `+0.1 за 100 CR`).
- `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md` — правила контента с мировыми координатами.

---

## 1. Чем именной NPC отличается от толпы

| | Именной NPC | Толпа PRIMUM |
|---|---|---|
| Владелец | Сцена `WorldScene_*` | `PrimumNpcCrowdDirector` в рантайме |
| Префаб | `Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab` | `PrimumCrowd`-префаб |
| Личность | `NpcController.definition` задан в сцене | `NpcController.SetDefinition()` в рантайме |
| Наличие | Постоянно, пока загружена сцена | Спавнится/пере-спавнится директором |
| Память к игроку | Серверная, по `npcId`, сохраняется в JSON | Нет (только косметика) |
| Квесты/диалоги | Полный пайплайн | Обычно нет |
| Позиция | Точка сцены + якоря/leash | Дом/улица, выбирается директором |

Правило: **именного NPC никогда не спавним через толпу и не выдаём ему личность через `SetDefinition`**.
`SetDefinition` — это контракт толпы; на сценовом именном NPC он перетрёт сценовую ссылку в рантайме.

---

## 2. Как это работает в рантайме

```text
Игрок жмёт E
  → NetworkPlayer.TryInteractNearestNpc()
  → NpcController.FindNearest()            (реестр scene-placed NPC)
  → QuestServer.RequestTalkToNpcRpc(npcId)  (клиент → сервер)
      → серверная проверка дистанции по NpcController.InteractionDistance
      → QuestWorld.OpenDialog(npcId, tree)
  → DialogStepDto → DialogWindow
      → выбор реплики: DialogueCondition[] (AND)
      → действие: DialogueAction (OfferQuest / AcceptQuest / CompleteObjective / AddNpcAttitude …)
```

Ключевые инварианты:

- Сервер — источник истины. Клиент только просит открыть диалог/выбрать реплику.
- Диалог ищется по `npcId`, а не по координате. NPC может ходить.
- Состояние квестов и отношений хранится по `npcId` в `QuestWorld`, а не на `MonoBehaviour` NPC.
- Сцена даёт только «тело» NPC и его стартовую точку/якоря.

---

## 3. Контракт ID (зафиксировать до создания ассетов)

| ID | Пример | Где используется |
|---|---|---|
| `npcId` | `npc_lyra_01`, `mira_01` | `NpcDefinition`, диалоги, квесты, сейв, `NpcController` |
| `treeId` | `dlg_lyra_q001`, `mira_default` | `DialogTree`, `QuestDatabase` |
| `questId` | `q_001_ash_under_glass` | `QuestDefinition`, `QuestDatabase` |
| `stageId` / `objectiveId` | `st_q001_follow_the_note` | `QuestDefinition`, условия диалога |
| scene anchor name | `anchor_q001_lyra_archive_desk` | сцена, `ReachLocation`, якоря |

Правила:

- `npcId` уникален и **не меняется после появления в сейвах** — иначе потеряются отношение, квесты и знание NPC.
- `treeId` уникален. Два ассета с одним `treeId` — коллизия lookup. Живой пример: `mira_default.asset` и `MiraDefault.asset` оба имеют `treeId: mira_default` и оба лежат в `QuestDatabase`; `GetDialogTree` вернёт первый. Новые NPC так не делать, дубль разобрать отдельной задачей.
- Object reference приоритетнее строкового поля (`questOfferRefs` > `questOffers`, `requiredNpc` > `stringParam`).

---

## 4. Шаг 1 — `NpcDefinition`

Создать: `Assets > Create > ProjectC > NPC Definition`.
Положить: `Assets/_Project/Quests/Data/Npcs/<Area>/<Name>.asset` (например `Q001/Lyra_Q001.asset`).

| Поле | Что писать |
|---|---|
| `npcId` | Стабильный ID, `npc_<name>_01`. |
| `displayName` | Имя для заголовка диалога. Лучше loc key `dialogue.npc.<npcId>.name`, либо литерал. См. §9. |
| `factionRef` | `FactionDefinition` (приоритет) + legacy `faction` для старых потребителей. |
| `portrait` | Портрет для `DialogWindow` (можно оставить пустым на прототипе). |
| `prefab` | Канонический NPC-префаб (метаданные; сцена ставится вручную). |
| `animatorTriggerPrefix` | Префикс триггеров аниматора, если используется. |
| `defaultDialogTree` | Основное дерево диалога этого NPC. |
| `questOfferRefs` | Квесты, которые NPC **выдаёт**. |
| `questTurnInRefs` | Квесты, которые NPC **принимает** на сдачу. |
| `services` | `Trade`, `Repair`, `Refuel`, `Restock`, … |
| `merchantTable` | Личный прайс-лист торговца (`NpcMerchantTable`). Если задан — при нажатии `E` правее диалога открывается магазин (см. `merchants/00_NPC_MERCHANT_GUIDE.md`). |
| `sellAllItemsForTesting` | `WARNING FOR TESTS ONLY`: включает продажу всех pickable-предметов из `ItemRegistry` для тестов. |
| `greetingText` | Loc key/литерал. Используется только fallback-деревом, если `defaultDialogTree` пуст. |
| `attitudeLinks` | Кросс-фракционное влияние при изменении отношения. |
| `personalAttitudeMin/Max` | Границы отношения именно с этим NPC (по умолчанию −100…200). |

Проверка: `NpcDefinition` должен быть добавлен в `QuestDatabase.npcs`. `QuestDatabaseExplorer` покажет его связи.

Мелочь, о которой легко забыть: `NpcDefinition.interactionRadius` — **authoring-поле**. Рантайм дистанцию берёт из `NpcController.InteractionDistance` на сцене (см. §6). Держи их согласованными.

---

## 5. Шаг 2 — `DialogTree`

Создать: `Assets > Create > ProjectC > Dialogue > Dialog Tree`.
Положить: `Assets/_Project/Quests/Data/Dialogs/dlg_<npc>_<quest>.asset`.

Минимум для квестодателя:

```text
greeting  →  [Offer quest]  →  offer  →  [Accept]  →  accepted
                                                        ↓
                                          (квест ушёл в Active)
```

- `treeId` — стабильный, совпадает с тем, что ищет `QuestDatabase`.
- `rootNodeId` — существующий `nodeId`.
- Узел: `speaker.refId = npcId`, `text` — loc key или литерал.
- Ребро: `label` (видит игрок), `conditions[]` (AND), `action` (одно атомарное действие), `targetNodeId`.
- Для «Пока» использовать `targetNodeId = ""` + `EndConversation`.

Поддерживаемые `DialogueCondition` на сервере:

`HasItem`, `QuestStateEquals`, `QuestStageEquals`, `QuestCompleted`, `QuestDiscovered`,
`ReputationAtLeast`, `ReputationAtMost`, `NpcAttitudeAtLeast`, `FlagIsSet`, `TimeOfDayIn`.

Не использовать для прогрессии: `CargoHasItem`, `PlayerInZone`, `WasNodeVisited` — они возвращают `false` с warning.

Основные `DialogueAction`:

`OfferQuest`, `AcceptQuest`, `CompleteObjective`, `FailQuest`, `DiscoverQuest`,
`GiveItem`, `TakeItem`, `GiveCredits`, `AddReputation`, `AddNpcAttitude`,
`OpenMarket`, `OpenService`, `SetFlag`, `EmitEvent`, `SwitchDialogTree`, `EndConversation`.

Валидация: `GetUnreachableNodes()` пуст, нет рёбер в несуществующие `nodeId`.

---

## 6. Шаг 3 — постановка в сцену (главное)

Единственная рабочая мировая сцена: `Assets/_Project/Scenes/World/WorldScene_0_0.unity`.
Не создавать новые сцены и не дублировать существующие.

### 6.1. Префаб

`Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab` — **канонический шаблон, не изменять**.

На корне уже есть:

| Компонент | Зачем |
|---|---|
| `CapsuleCollider` (trigger) | зона взаимодействия; `NpcController.Awake` принудительно ставит `isTrigger` |
| `NpcController` | `definition`, дистанция, нейм-плейт, реестр для E |
| `NetworkObject` + `NetworkTransform` | серверный спавн и репликация движения |
| `NavMeshAgent` | ходьба по навмешу |
| `NpcBrain` | состояния, aggro/leash, респавн, штраф отношения |
| `NpcSocialBrain` | idle-активности, якоря, патруль, grudge |
| `NpcTarget` + `NpcAttacker` | бой |
| `CharacterController` | физическое тело (не заменять на Rigidbody) |

Дочерний `Text (TMP)` — нейм-плейт (по умолчанию текст `MIRA`).

### 6.2. Куда ставить

Два допустимых варианта:

1. **Под `WorldRoot_0_0`** (предпочтительно для постоянного мира). Тогда Floating Origin двигает NPC бесплатно.
2. **В отдельный корень-контейнер с `GlobalSceneSourceMarker`** — так сделано у `NPC` (Onboarding alfa) и `Q001_RuntimeTest_Line` (Q001 NPC). Маркер обязателен: без него корневой объект не поедет при F8.

Не ставить именного NPC в `BootstrapScene`: это не мировая сцена, он не будет стримиться вместе с миром.

### 6.3. Обязательные override на экземпляре

Выделить экземпляр префаба и переопределить:

| Компонент / поле | Значение |
|---|---|
| `NpcController.Definition` | **Свой `NpcDefinition`**. Если не переопределить — NPC останется Мирой. |
| `NpcController.Interaction Distance` | 2.5–3 м. Это реальный радиус E и серверной проверки. |
| `Text (TMP).text` | Имя на табличке, например `ЛИРА`. Если не переопределить — будет `MIRA`. |
| `NpcSocialBrain.Faction` | Своя фракция, если отличается от значения в префабе. |
| `NpcBrain.Behavior Type` | Для квестодателя — `Passive` (в префабе уже `Passive`). `Neutral` — если не должен драться вообще. `Aggressive` — только для врагов. |

Дополнительно проверить:

- `NpcController.Name Label` назначен (в префабе уже назначен);
- `NpcBrain.Leash Range` = 40 м (по умолчанию) — дальше NPC не уйдёт;
- `NpcBrain.Respawn Enabled` включён; `Respawn Delay` 30 с; `Max Respawns = 0` (без лимита);
- `NavMeshAgent` стоит на навмеше. Если NPC вне меша, агент паркуется и якоря работать не будут.

Имя объекта в сцене: `<Area>_NPC_<Name>__<Name>`, например `Q001_NPC_Lyra__Lyra`.

### 6.4. Якоря и idle-активность (`NpcSocialBrain`)

`Idle Activity`:

| Значение | Поведение |
|---|---|
| `StandStill` | Стоит на месте (квестодатель по умолчанию). |
| `Patrol` | Ходит по `patrolWaypointMarkers` (приоритет) или `patrolWaypoints`; `Patrol Pattern` — Loop/PingPong/Random. |
| `LookAround` | Осматривается. |
| `Socialize` | Идёт к `socializeAnchors`. |
| `Work` | Обходит `workAnchors` и играет рабочую анимацию. |
| `Sit` | `sitAnchors` (приоритет над поиском `SitPoint`). |
| `Sleep` | `sleepAnchors`. |
| `Wander` | Блуждает вокруг `wanderAnchor`, иначе вокруг `NpcBrain.SpawnPoint`. |

Якоря — это `Transform`-маркеры. Делать их Empty-объектами в сцене (можно детьми NPC) и перетаскивать в массивы. Для квестодателя проще `StandStill`; для «живого» NPC — `Work`/`Patrol` с 2–4 точками.

Возврат домой: `NpcBrain` помнит `SpawnPoint` (позиция экземпляра при спавне) и держит NPC в пределах `Leash Range`; после боя `NpcSocialBrain` возвращает его к `SpawnPoint`.

### 6.5. Спавн и стриминг

- `ScenePlacedObjectSpawner` на сервере при загрузке `WorldScene_*` находит `NetworkObject` с `IsSpawned == false` и вызывает `Spawn(destroyWithScene: true)`.
- Ничего вручную спавнить не нужно. Не добавлять второй `NetworkObject` и не тащить NPC в `BootstrapScene`.
- При выгрузке сцены NPC исчезает; при повторной загрузке появляется на своей сценовой позиции. Память при этом не теряется — она на сервере и в JSON.

---

## 7. Шаг 4 — регистрация в базе

Открыть `Assets/_Project/Quests/Data/QuestDatabase.asset` и **аддитивно** добавить:

- `NpcDefinition` → массив `npcs`;
- `DialogTree` → `dialogTrees`;
- `QuestDefinition` → `quests`.

Массивы не заменять целиком. Проверить `Tools > Project C > Quests > Quest Database Explorer`.

---

## 8. Память NPC о игроке

Память живёт **на сервере, по `npcId` и `clientId`**, а не в компонентах NPC.

| Что помнит | Где | Переживает рестарт |
|---|---|---|
| Состояние квестов, stage, objective progress | `QuestWorld` → `QuestSaveData.quests` | да |
| Репутация с фракциями | `QuestWorld` → `QuestSaveData.reputation` | да |
| Отношение к игроку (личное) | `QuestWorld` → `QuestSaveData.npcAttitude` | да |
| События мира (`EmitEvent`) | `_eventsOccurred` | да |
| Флаги мира (`SetFlag`) | `_worldFlags` | да |
| С кем игрок говорил | `_npcTalkedTo` | да |
| Знание NPC/фракций | `_knownNpcs`, `_knownFactions` | да |
| Обида после удара (grudge) | `GrudgeTable` | **нет**, живёт 300 с |
| Позиция, анимация, боевое состояние | компоненты сцены | **нет**, сбрасывается при перезагрузке сцены |

Файл: `Application.persistentDataPath/quest_state_<clientId>.json`.

Запись: immediate, атомарно (`tmp` → rename), на каждое изменение состояния; плюс при отключении и завершении сервера.
Чтение: при подключении клиента.

### 8.1. Отношение (NpcAttitude)

- Диапазон: `personalAttitudeMin…personalAttitudeMax` из `NpcDefinition` (по умолчанию −100…200).
- Старт: `0`.
- Удар по NPC: `−2`. Убийство: `−20` (делает `NpcBrain`).
- Диалоговое действие `AddNpcAttitude` меняет отношение явно.
- `attitudeLinks` двигают репутацию связанных фракций.
- При отношении ниже `NpcBrain.Hostility Threshold` (−50) `Passive`-NPC становится враждебным.

### 8.2. Что это даёт в диалоге

Условия читают серверное состояние игрока:

```text
QuestDiscovered / QuestStateEquals / QuestStageEquals / QuestCompleted
NpcAttitudeAtLeast
ReputationAtLeast / ReputationAtMost
FlagIsSet / HasItem / TimeOfDayIn
```

Поэтому «NPC помнит» = разное приветствие/ветки на разных stage, при низком отношении, после событий.

### 8.3. Чего память не делает

- Не сохраняет позицию NPC между перезагрузками сцены.
- Не сохраняет grudge дольше 300 с.
- Не хранит память «на NPC-объекте»: два экземпляра с одним `npcId` — это один и тот же NPC для сервера.

---

## 9. Локализация

`ProjectC.Localization.Loc` маршрутизирует по префиксу:

| Префикс | Таблица |
|---|---|
| `static.` | `Static_Table` |
| `ui.` | `UI_Table` |
| `dialogue.` | `Dialogue_Table` |
| `sys.` | `System_Table` |
| всё остальное | `UI_Table` (и если ключа нет — вернётся сам ключ) |

Правильные ключи для именного NPC:

```text
dialogue.npc.<npcId>.displayName     — имя в заголовке диалога
dialogue.npc.<npcId>.greeting        — приветствие (если нужно)
dialogue.<treeId>.displayName        — имя дерева
dialogue.<treeId>.node.<nodeId>      — текст реплики
dialogue.<treeId>.option.<edgeName>  — текст варианта ответа
```

Важно:

- **Не использовать префикс `dialog.`** — он не маршрутизируется в `Dialogue_Table` (так ломался Q001).
- **Не использовать `npc.<id>.name`** без `dialogue.`/`ui.` — уйдёт в `UI_Table`, и если ключа там нет, игрок увидит сырой ключ. Сейчас у Q001 `displayName = npc.lyra_01.name` и такого ключа в таблицах нет — это известный хвост, который надо закрыть при следующем проходе Q001.
- Нейм-плейт над головой — **литерал в `Text (TMP)` экземпляра**, не loc key. Поэтому `ЛИРА`, `СЕЛА`, `МИРА` пишутся прямо в сцену.
- Тексты `DialogWindow` проходят через `Loc.Get`, поэтому литерал без ключа тоже отобразится как есть.

Файлы таблиц: `Assets/_Project/Settings/Localization/Dialogue_Table_ru.asset` и `..._en.asset` (+ `Dialogue_Table Shared Data.asset`).

---

## 10. Проверка

### 10.1. Статически (без Play Mode)

- [ ] Console → 0 errors после компиляции.
- [ ] `QuestDatabase` содержит NPC, дерево и квест.
- [ ] `GetUnreachableStages() == 0` для квеста.
- [ ] `GetUnreachableNodes()` пуст для дерева.
- [ ] Все `TalkToNpc` имеют `targetNpc` + `targetNpcId`.
- [ ] `npcId` / `treeId` / `questId` уникальны.
- [ ] У turn-in NPC квест есть в `questTurnInRefs`.
- [ ] На сцене у экземпляра переопределены `Definition` и `Text (TMP).text`.
- [ ] Контейнер NPC под `WorldRoot_0_0` или имеет `GlobalSceneSourceMarker`.
- [ ] RU/EN ключи существуют (если используем ключи, а не литералы).

### 10.2. В редакторе

- `Tools > Project C > NPC World Inspector` → `Scan All World Scenes` → NPC виден в `WorldScene_0_0` с нужным типом, фракцией и квестами.
- `Tools > Project C > Quests > Quest Database Explorer` → связи NPC ↔ квест ↔ диалог.

### 10.3. В игре (запускает пользователь)

1. Подойти к NPC, нажать E → диалог открылся.
2. Взять квест → он появился в журнале сразу.
3. Выполнить objective (разговор/предмет/зона) → stage сменился.
4. Сдать квест правильному NPC → награда выдалась один раз.
5. Повторно открыть диалог → награда не повторилась.
6. Ударить NPC → отношение упало; при повторном разговоре доступны другие ветки/тон.
7. Отойти далеко и вернуться / перезагрузить клиента → квест и отношение на месте, NPC снова у своей точки.

---

## 11. Запреты и типовые ошибки

| Ошибка | Последствие | Как правильно |
|---|---|---|
| Забыли override `NpcController.Definition` | Все NPC — Мира | Переопределить `definition` на экземпляре. |
| Забыли override `Text (TMP).text` | Над всеми «MIRA» | Вписать имя в нейм-плейт экземпляра. |
| Правили канонический префаб | Изменились все NPC сразу | Менять только экземпляр в сцене. |
| Использовали `SetDefinition()` | Личность перетирается в рантайме | Только для толпы. |
| Дублировали `treeId` | Диалог открывает чужое дерево | Уникальный `treeId`. |
| Ключ `dialog.*` | Сырой ключ в UI | Только `dialogue.*`. |
| `npc.*` без таблицы | Сырой ключ в заголовке | `dialogue.npc.*` или литерал. |
| NPC вне навмеша | Агент запаркован, не идёт к якорям | Поставить на NavMesh. |
| Контейнер без `GlobalSceneSourceMarker` | Не едет при F8 | Под `WorldRoot_0_0` или добавить маркер. |
| `Interaction Distance` 10–20 м | E «сквозь стены» | 2.5–3 м. |
| `NpcDefinition.interactionRadius` верят как рантайму | Фактический радиус другой | Смотреть `NpcController.InteractionDistance`. |
| Второй `NetworkManager` / новая сцена-дубль | Ломается сеть/стриминг | Не создавать. |
| Правка `.unity`/`.prefab` YAML руками | Битые GUID/сцена | Только через Unity-инструменты. |

---

## 12. Живой пример в проекте

В `WorldScene_0_0` уже стоят 8 именных NPC на каноническом префабе:

| Объект | Definition |
|---|---|
| `[Mira]` | `Mira` (по умолчанию из префаба) |
| `[Onboarding alfa]` | `OnboardingAlfa` |
| `Q001_NPC_Lyra__Lyra` | `Lyra_Q001` |
| `Q001_NPC_Bram__Bram` | `Bram_Q001` |
| `Q001_NPC_Veska__Veska` | `Veska_Q001` |
| `Q001_NPC_Noll__Noll` | `Noll_Q001` |
| `Q001_NPC_Kael__Kael` | `Kael_Q001` |
| `Q001_NPC_Sela__Sela` | `Sela_Q001` |

Устройство:

- `[Mira]` лежит под корнем `NPC` (`GlobalSceneSourceMarker`). Она не переопределяет `definition` и текст нейм-плейта: префаб уже указывает на `Mira.asset`, а табличка — `MIRA`. Зато у неё `Idle Activity = Work`, один `workAnchor` и два `socializeAnchors` — это пример «живого» именного NPC с якорями.
- `[Onboarding alfa]` лежит под корнем `NPC` (`GlobalSceneSourceMarker`), переопределяет `definition`, `Interaction Distance = 3`, фракцию и текст нейм-плейта.
- Q001-цепочка лежит под `Q001_RuntimeTest_Line` (`GlobalSceneSourceMarker`) и переопределяет `definition` + `Text (TMP)` (ЛИРА/БРАМ/…).
- Это **тестовая линейка** для прогона Q001; финальные точки NPC/зон ещё расставляются вручную.

Поэтому для Миры достаточно поставить префаб как есть и задать позицию/якоря; для любого другого NPC `definition` и нейм-плейт нужно переопределить обязательно.

---

## 13. Чеклист нового именного NPC

```text
[ ] Придуман и зафиксирован npcId (snake_case, уникальный)
[ ] Создан NpcDefinition в Data/Npcs/<Area>/
[ ] Назначены factionRef, defaultDialogTree, questOfferRefs, questTurnInRefs
[ ] Создан DialogTree в Data/Dialogs/ с уникальным treeId
[ ] Текст/лейблы — dialogue.* ключи или литералы
[ ] Квест(ы) созданы и связаны object refs
[ ] Всё добавлено в QuestDatabase (npcs/dialogTrees/quests)
[ ] Экземпляр канонического префаба поставлен в WorldScene_0_0
[ ] Переопределены Definition, Interaction Distance, Text (TMP)
[ ] Проверены Faction и Behavior Type (Passive/Neutral)
[ ] NPC на NavMesh, контейнер под WorldRoot_0_0 или с GlobalSceneSourceMarker
[ ] Якоря/idle настроены (или StandStill)
[ ] Compile: 0 errors
[ ] NPC World Inspector видит NPC и его квесты
[ ] Ручной Play Mode: E → диалог → квест → сдача → память после рестарта
```
