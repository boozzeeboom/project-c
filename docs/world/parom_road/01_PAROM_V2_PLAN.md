# Паром v2 — экипаж, оплата проезда, зайцы (план, НЕ реализовано)

> Статус: план. v1: `00_PAROM_DESIGN.md` (маршрут, тросы, кабинка, carry).
> Лор: на пароме 1–2 NPC — машинист и контролёр, либо машинист-контролёр
> в одном лице. Проезд оплачивается разговором с конкретным NPC.
> Безбилетный проезд роняет отношения с фракциями (настраивается).

## 0. Что уже есть (переиспользуем, не пишем заново)

| Система | Что берём | Файл |
|---|---|---|
| Именной NPC + история | `NpcDefinition` (id, имя, фракция, портрет, `defaultDialogTree`, `attitudeLinks`, квесты) + регистрация в `QuestDatabase.npcs[]` | `Assets/_Project/Quests/Npcs/NpcDefinition.cs` |
| Разговор по E | `NpcController` (триггер + `PlayerInteractor` → `RequestTalkToNpcRpc` → `QuestServer` → `DialogTree`) | `Assets/_Project/Quests/NpcController.cs` |
| Живое отношение | `QuestWorld.ModifyNpcAttitude` (+ каскад `attitudeLinks` по фракциям), `ModifyReputation` (клейм ±100, ивент, `SavePlayer`) | `.../Quests/Core/QuestWorld.cs:290,333` |
| Деньги | Кредиты CR, сервер-авторитетно: `TradeWorld.Repository.TryModifyCredits` + пуш снапшотов (паттерн — оплата recall) | `ShipController.cs:2445`, `ShipModuleServer.cs:570` |
| Диалоговые эффекты | `DialogueActionType`: `GiveCredits/TakeItem/AddReputation/AddNpcAttitude`, результат `DialogActionResultDto(success, resultData)` | `DialogueAction.cs`, `QuestServer.cs:1293+` |
| Диалоговые условия | `HasItem`, `ReputationAtLeast/AtMost`, `NpcAttitudeAtLeast`, флаги, `WasNodeVisited` | `DialogueCondition.cs:31` |
| Привязка именного NPC к платформе | `ShipCrewManifest` (memberId + `NpcDefinition` + роль + якоря) — образец для `ParomCrew` | `Scripts/PeacefulShip/Crew/ShipCrewManifest.cs` |
| Езда NPC на платформе | `NpcBrain` carry + `ApplyRebaseTranslation`; связка `NpcBrain`+`NpcController` уже поддержана (`NpcBrain.cs:552` — npcId подхватывается из контроллера) | `Scripts/AI/NpcBrain.cs` |
| Сувенир-билет | Айтем `Ferry Waybill` уже есть в лоре/импорте (`GiveItem`/`TakeItem` готовы) | `Items/Data/Resources_Import.csv:288` |

## 1. UX-сценарий v2 (ответы зафиксированы)

1. Игрок запрыгивает на кабинку (v1 carry, без изменений).
2. Подходит к машинисту-контролёру (v2-тест: **1 NPC, режим 2-в-1**),
   жмёт E → диалог: «Проезд — **2 CR на весь маршрут, до любой станции**» →
   ветка «Заплатить» / «Отказаться» / «Не хватает».
3. Оплата: −2 CR, тикет на маршрут, +attitude контролёру,
   в инвентарь падает **`Ferry Waybill` с серверной датой выдачи**
   (игровой день сервера; см. §3 — инстанс-модель).
4. **Выход с парома** (сошёл с тележки): билет помечается **«использован»**
   (штамп на инстансе + закрытие серверного тикета).
5. Заяц: **пересёк точку маршрута** (прибыл на станцию) без валидного тикета →
   везём дальше, но **1 раз за лег** списываем штраф: тост + `−X` к репутации
   у фракций из конфига + контролёр «запоминает» (−attitude, каскад `attitudeLinks`).
6. **Контролёр для штрафа не обязателен**: едешь без билета — штраф
   (даже если NPC нет на борту). Оплативший едет спокойно.

## 2. Экипаж (живые NPC, упрощённые относительно капитанов)

Состав на кабинку — дети `ParomRoute_01` (едут с тележкой иерархией, FO 🟢):

- **Машинист-контролёр 2-в-1** (v2-тест, `singleNpcMode`): один NPC с диалогом
  «машинист-контролёр» (оплата + флейвор). Отдельные машинист/контролёр —
  конфиг умеет, позже. Для штрафа контролёр на борту **не обязателен**
  (`graceIfNoController = false`, см. §4).

Отличия от капитанов кораблей (сознательное упрощение):

| Капитаны | Экипаж парома |
|---|---|
| `NpcBrain` + `NpcSocialBrain` (патруль, flee, grudge, бой) | `NpcController` + `NpcDefinition` достаточно; `NpcBrain` — только если позже захотим реакции/мораль (тогда NPC станет `NetworkObject`! см. §6) |
| Перемещаются по палубе | Стоят на якорях (idle-анимация; `SitPoint`-паттерн при желании посадить) |
| История через квесты корабля | Та же: `questOfferRefs`, `attitudeLinks`, `personalAttitudeMin/Max` — «живость» даёт диалоговая подсистема, а не ноги |

Конфиг (без хардкода, по образцу `ShipCrewManifest`): новый SO `ParomCrewManifest`
(routeId + записи: role [Machinist/Controller], `NpcDefinition`, anchor,
`required`) либо прямые поля на `ParomRoute` (crew-контролёр, crew-машинист).
Решение при реализации; SO гибче при нескольких ветках.

## 3. Оплата (новый диалоговый экшен — TakeCredits нет)

В `DialogueActionType` **нет списания кредитов** (есть только `GiveCredits`)
и **нет условия «хватает денег»**. Поэтому:

- **Дописать:** `DialogueActionType.PayParomFare = 33` (рядом с валютой/репутацией):
  params — `stringParam = routeId` (цена читается из `ParomRoute`, а не из узла —
  иначе рассинхрон цены; `intParam` — резерв/override).
- **Серверный флоу в `QuestServer.FireDialogAction`** (паттерн `GiveCredits`,
  `QuestServer.cs:1572`):
  1. Найти маршрут по routeId (реестр `ParomRoute` по сценам; server-only).
  2. `repo.GetCredits(clientId) >= fare`? Нет → `success=false, resultData="insufficient"`
     (диалог показывает ветку «не хватает» — условие ветвления по результату
     докрутить в `DialogWindow`, см. §7-п.5).
  3. Да → `TryModifyCredits(clientId, −fare)` (как recall, `ShipController:2445`),
     пуш снапшотов (`ContractServer`, `InventoryServer` — скопировать 3 строки).
  4. Выдать тикет: `ParomFareService.MarkPaid(clientId, routeId, upToLeg)`.
  5. `ModifyNpcAttitude(clientId, controllerNpcId, +payAttitudeDelta)` (уважение;
     каскад `attitudeLinks` — бесплатно).
  6. **Билет-инстанс** (не простой сувенир): `GiveItem(Ferry Waybill, 1)` +
     серверная запись `WaybillInstance { instanceId, ownerId, routeId,
     issueDay (игровой день сервера), state: Valid }` — по образцу
     `KeyRodInstance/KeyRodInstanceWorld` (инстанс-слой инвентаря уже есть:
     `InventorySlot{itemId + instanceId}`, `docs/Ships/Key-subsystem/`).
     Тултип билета показывает день выдачи. При выходе с парома (§4) —
     `state = Used` («использован»). Авторитет — серверная запись;
     айтем — носитель и история. Персист инстансов — как у ключей
     (T-KEY-PERSIST — отдельный тикет; для v2 допустимо session-only
     с правилом «после рестарта старые Valid считаются истёкшими»).
  7. `SendDialogActionResultToClient(success=true, "paid:<routeId>")` (+ `BroadcastReputationChange`/
     `BroadcastNpcAttitudeChange` для бейджей, как у соседних кейсов).
- Цена живёт **только** в `ParomRoute.fareCredits` (менеджер, инспектор).
  Альтернатива без нового типа экшена (хуже): связка `TakeItem(жетон)` — требует
  чеканки жетонов вне диалога; отклонено, кредиты — канон оплаты (recall, ремонт).

## 4. Зайцы (детект + пенальти, всё server-side)

- **Детект райдеров:** триггер-объём на тележке — `ParomRiderTracker`
  (паттерн `NpcController`: триггер, серверное множество `clientId`; движется
  вместе с кабинкой; живых позиций не кэширует — FO-хук не нужен).
  Фолбэк без триггера: дистанция `|player − trolley| < R` в момент событий.
- **Тикет:** `ParomFareService` (server-only, host): `clientId → {routeId, validUntilLeg}`.
  Scope v2-теста: **весь маршрут** (`ticketScope = Route`; `ticketLegs` — резерв
  под будущее деление). Легитимный «туда-обратно» = 2 тикета.
- **Пенальти на прибытии** (в `ParomRoute.Arrive`, только `IsServer`):
  райдер пересёк точку маршрута без валидного тикета на этот лег → везём дальше,
  но штраф **1 раз за лег** (`lastPenalizedLeg`):
  1. Для каждой записи `fareDodgePenalties[]` (менеджер!): 
     `QuestWorld.ModifyReputation(clientId, factionRef, −repDelta)` —
     уже с клампом, ивентом, персистом (`SavePlayer`, `FactionRepSaveEntry`).
  2. `ModifyNpcAttitude(clientId, controllerNpcId, −dodgeAttitudeDelta)` —
     контролёр запоминает; каскад `attitudeLinks` может ударить по другим фракциям.
  3. Тост клиенту (`QuestToast`-паттерн: `AddReputation`-строка уже рендерится).
- **Антифарм:** пенальти не чаще 1 раза на лег; прыжки «сошёл-зашёл» не сбрасывают
  флаг до конца лега; респаун/телепорт на кабинку = тот же райдер-трекер (без льгот).
- **Выход с парома = гашение билета:** `ParomRiderTracker` ловит `OnTriggerExit`
  (сошёл с тележки) → сервер закрывает тикет и ставит инстансу `Used`
  (день/лег гашения — в запись). Дисконнект в пути = выход (Valid → Used,
  переиспользовать нельзя). Выход на промежуточной станции с дальнейшим
  продолжением поездки = новый тикет (новый проезд — новая оплата).
- **Grace-правила (конфиг):** `fareCredits = 0` → маршрут бесплатный, трекер выключен;
  `graceIfNoController` (дефолт **false** — штраф и без контролёра, по ответу);
  `penalties[]` пуст → молча возим (режим «добрый паром» для тестов).

## 5. Конфиг менеджера (всё в инспекторе `ParomRoute`, нового хардкода — ноль)

```
Fare (секция “Оплата”):
  fareCredits = 2 (CR на весь маршрут, до любой станции; 0 = бесплатно)
  ticketScope = Route (весь маршрут; ticketLegs — резерв)
  payAttitudeDelta (+ к контролёру за оплату)
  fareDodgePenalties[]: { factionRef (FactionDefinition), repDelta (−) }
    // v2-тест: GuildOfSuccess; дизайнер правит массив сам
  dodgeAttitudeDelta (− к контролёру за заячий лег)
  graceIfNoController = false (штраф и без контролёра)
Crew (v2-тест: 1 NPC, 2-в-1):
  controllerNpc (NpcDefinition: parom_<route>_crew), controllerAnchor
  driverNpc (опционально, позже), driverAnchor (= Driver Anchor)
  singleNpcMode = true (машинист-контролёр)
Riders:
  riderRadius (триггер), trackRiders (bool)
```

## 6. Сеть, FO, closed-world (допуск!)

- **Авторитет:** симуляция и тикеты — сервер (`IsServer`); диалог уже ходит
  `client → server RPC`; нового NGO-трафика нет (тикет — host-dict, не `NetworkVariable`).
  Мультиплеер v2 — dormant по скоупу FO (как весь проект): тикеты второго пира —
  отдельной задачей.
- **FO:** экипаж и триггер — дети тележки под `WorldRoot_0_0` (🟢 едут бесплатно);
  тикеты keyed by `clientId` (не позиции) — `ApplyRebaseTranslation` не нужен;
  репутация/кредиты персистятся штатно (`QuestSaveData`, Trade repo) — кумулятив
  сдвига им не нужен (не координаты).
- **Closed-world (важно!):** если экипаж — только `NpcController` (plain
  `MonoBehaviour`, без `NetworkObject`), записей в каталог НЕ требуется
  (свип видит только маркеры и NO). Если позже добавим `NpcBrain`
  (`NetworkBehaviour`!) — каждому такому NPC нужны маркер + `Unmanaged`-запись
  + пересчёт digest по процедуре `00_PAROM_DESIGN.md` §8. Поэтому v2 — без `NpcBrain`.
- **Сейвы:** тикеты живут сессию (после рестарта все «безбилетники» — приемлемо,
  задокументировать); rep/attitude/credits — персистентны из коробки.

## 7. Этапы реализации (оценка: S < дня, M = дни, L = неделя+)

| # | Что | S/M/L | Трогаем / пишем |
|---|---|---|---|
| 1 | `ParomRiderTracker` (триггер, множество райдеров) + тест вхолостую | S | Новое, паттерн `NpcController` |
| 2 | Fare-конфиг в `ParomRoute` + `ParomFareService` (тикеты, `lastPenalizedLeg`) | S | Адаптация `ParomRoute`, новое service |
| 3 | Экипаж: `ParomCrewManifest` (SO) + 1 `NpcDefinition` 2-в-1 (`parom_<route>_crew`) + `DialogTree` (узлы: цена 2 CR/оплатить/нет денег/прощание) + привязка в сцене | M | Новое (SO+ассеты), адаптация сцены; NPC без NO — каталог не трогаем |
| 4 | `DialogueActionType.PayParomFare` + ветка в `QuestServer.FireDialogAction` (кредиты/тикет/attitude/Waybill-инстанс/снапшоты) + `WaybillInstance` (POCO по образцу `KeyRodInstance`) | M | Дописать enum + 1 case (паттерн `GiveCredits`) + инстанс-модель |
| 5 | Ветвление диалога по `success=false` (`DialogWindow`: показать «не хватает»-узел по `resultData`) — проверить, хватает ли готового; если нет — дописать | S–M | Адаптация UI |
| 6 | Пенальти в `Arrive` (реп + attitude + тост) + grace-правила | S | Адаптация `ParomRoute` |
| 7 | Ручная приёмка: Host, оплата, заяц (−rep, тост), F8/F9 с экипажем,carry NPC, персист rep после рестарта; дока `ITERATIONS` + коммит | M | Тест-план |

Зависимости: 4→5 (результат нужен UI), 2→6, 3→4 (npcId контролёра).
Параллельно: 1+2, 3 (ассеты).

## 8. Ответы зафиксированы (бывшие открытые вопросы)

1. ✅ Цена: **2 CR на весь маршрут, до любой станции.**
2. ✅ Тикет: **весь маршрут** (`ticketScope = Route`); туда-обратно = 2 тикета.
3. ✅ Штраф: v2-тест — **GuildOfSuccess**; массив `{factionRef, repDelta}` правит дизайнер.
4. ✅ Контролёр для штрафа **не обязателен** (`graceIfNoController = false`).
5. ✅ `Ferry Waybill` выдавать: **инстанс с серверным днём выдачи**; при выходе —
   штамп **«использован»** (модель — `WaybillInstance` по образцу `KeyRodInstance`).
6. ✅ Экипаж v2-теста: **1 NPC 2-в-1** (`singleNpcMode = true`).
7. ✅ Зайца **везём дальше**, штраф 1 раз за пересечение точки маршрута.

## 9. Вне v2 (не делать)

Высадка зайцев, турникеты/двери, dynamic pricing,-multileg билеты с пересадками,
контролёр-боец (`NpcBrain`, бой, арест), расписание-табло, звук/анимации посадки,
мультиплеер-v2 тикетов, перенос тикетов через рестарт.
