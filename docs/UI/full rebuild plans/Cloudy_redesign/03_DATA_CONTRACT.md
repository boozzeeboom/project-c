# 03 — Контракт данных: каждое поле мода → источник в игре

> Правило: окно показывает **только** то, что есть в этой таблице со статусом «есть».
> «Нет в игре» — либо скрывается, либо вычисляется на клиенте только для показа и помечается в интерфейсе.
> Пути и имена найдены в коде 2026-10-28; «проверить» — найдено частично, нужна сверка при реализации.
>
> **Шаг 0 закрыт (2026-10-29):** мод `v4-2` приведён к этим именам буквально — `state.contracts.active/available/`
> `canAcceptContracts/marketTimeMultiplier/secondsUntilNextTick/debtAmount/debtLevel`, элементы `ContractDto`
> (`contractId`, `fromLocationId`/`toLocationId`, `timeRemaining` в секундах, `isReceiptContract`, …);
> `quests[].objectives[] {description, completed, currentValue, requiredQuantity}`;
> `state.bag = {locationId, items[], maxSlots, credits}` с `InventoryItemDto`; `ITEM_DEFS[itemId] = {itemName,`
> `itemType, description, icon, equipSlot, maxStack, weightKg}`; `ships[]` — `hullCurrent/hullMax`,
> `fuelNormalized/fuelMax`, `cargoUsed/cargoMax`, `moduleCount`, `state`, `flags`, `shipColorR/G/B`,
> `isEngineRunning`; `modules[] {slotType, installedModule}`; `cargo[shipId].cargoDetail[]` с битовой маской
> `flags` (бит0 опасный, бит1 хрупкий) и `unitWeight = weightKg`. Колонка «Поле в моде» ниже называет смысл
> значения; точные идентификаторы брать из этого списка. Всё без игрового источника помечено в моде
> `Derived`/`*_DERIVED`.

## 1. Персонаж (домашняя станция)

| Поле в моде | Источник | Событие / обновление | Статус |
|---|---|---|---|
| HP `cur/max` | `ProjectC.Combat.PlayerTarget.GetCurrentHp()/GetMaxHp()` на локальном `NetworkPlayer` (так читает легаси `RefreshHpFromNetworkVariable`); дубль — `StatsSnapshotDto.currentHp/maxHp` | опрос / `StatsClientState.OnStatsUpdated` | есть; источник — `PlayerTarget` (как легаси, аудит R4) |
| STR/DEX/INT, тиры, XP до тира | `ProjectC.Stats.StatsClientState.Instance.CurrentStats` → `StatsSnapshotDto` (`strength…`, `*Tier`, `*XpForNextTier`, `effective*`) | `OnStatsUpdated`, `OnStatTierUp` | есть |
| Уровень / опыт персонажа | легаси пишет заглушки `"1"` / `"0"` | — | **нет в игре** → не показывать |
| Кредиты | легаси: `MarketClientState.Instance.CurrentSnapshot.credits` (`{credits:F0} CR`); также `InventorySnapshotDto.credits` | `MarketClientState.OnSnapshotUpdated` / `InventoryClientState.OnSnapshotUpdated` | **решено: как легаси** (`MarketClientState`). **Известная проблема:** в игре два источника кредитов — нужна одна истина (задача на потом, см. `04` §6) |
| Долг | `ContractClientState.CurrentSnapshot.debtAmount/debtLevel` | `OnSnapshotUpdated` | есть |
| Слоты экипировки (одежда/модули) | `ProjectC.Equipment.EquipmentClientState.Instance.CurrentSnapshot` → `EquipmentSnapshotDto.equip` | `OnEquipmentUpdated`, `OnEquipResult` | есть |
| Имя персонажа | легаси пишет локализованное «игрок / владелец» | — | **имени в данных нет** (проверить `CustomisationClientState`) |
| Время, день, месяц, год, фаза | `ProjectC.Core.ServerWeatherController.Instance`: `CurrentGameTime` (`GameTimeData`), `TimeOfDay`, `GetWeekdayName`, `GetMonthName`; фаза — `DayNightController.Instance.CurrentPhase.phaseName` | `OnCalendarChanged` + опрос 2 с (как легаси) | есть |
| Календарь 12×30, неделя 7 | `CalendarConfig.daysPerMonth=30`, `monthsPerYear=12`, `daysPerWeek=7`, `monthNames/weekdayNames` | — | есть; брать из конфига, не хардкодить |
| Заметки календаря (`v4.calendar.notes`) | — | — | **нет в игре** → `PlayerPrefs` с префиксом окна, только визуал |
| Высота / альтиметр | легаси HUD `AltitudeUI`: `ShipController.transform.position.y` + `AltitudeCorridorSystem.Instance` (`AltitudeCorridorData.GetStatus`) | опрос 0.2 с | есть; **проверить floating origin** (`GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset`) — высота должна быть абсолютной. Коридор брать только из `GetActiveCorridor(shipPos)`: расхождение с демо-коридором мода — проблема D16 в `04` §6 |

## 2. Репутация и отношения

| Поле в моде | Источник | Статус |
|---|---|---|
| Репутация фракций | `ReputationClientState.Instance.CurrentReputation` → `ReputationSnapshotDto.entries`; имена — `ProjectC.Knowledge.FactionCatalog.Instance.GetByWireId` | есть; **диапазон −100…+100** (решение пользователя, как в ассетах); мод переведён на ±100 |
| Отношение NPC | `NpcAttitudeClientState.Instance.CurrentNpcAttitude`, `GetAttitudeForNpc`; диапазон `NpcAttitude.MinValue=-100`, `MaxValue=200` | есть |
| Тиры репутации | `FactionDefinition.reputationThresholds[]` → `ReputationTier {tier, value, color, ussClass}`; выбор — `FactionDefinition.GetTier(rep)` (нижняя граница включительно, побеждает самый высокий порог ≤ значения). Ассеты `Assets/_Project/Resources/Data/Factions/`: у 8 фракций (FreeTraders, GuildOfCreation, GuildOfSecrets, GuildOfStrength, GuildOfSuccess, GuildOfThoughts, Neutral, Pirates) 6 тиров −100 Враг / −25 Недруг / 0 Нейтрален / 25 Друг / 75 Уважаемый / 150 Почитаемый | есть. **Проблемы:** тир «Почитаемый 150» недостижим при ±100 (вопрос баланса); у `Faction_Bandits` и `Faction_Villagers` тиров нет — в окне «тиры не заданы — будущая реализация» |
| Граф связей и его раскладка (`v4.relations.layout`) | узлы — из фракций/NPC выше; раскладка и заметки — `PlayerPrefs`, только визуал | раскладка: нет в игре |

## 3. Задания (квесты)

| Поле в моде | Источник | Статус |
|---|---|---|
| Список, состояние, цели | `QuestClientState.Instance.CurrentSnapshot` → `QuestSnapshotDto.quests[]` → `QuestProgressDto {questId, displayName, state, currentStageId, isTracked, objectives[]}`; цели `ObjectiveProgressDto {description, completed, currentValue, requiredQuantity}` | есть |
| `QuestState` | `ProjectC.Quests.QuestState : byte` — Discovered, Offered, Active, Completed, Failed, TurnedIn | есть |
| Новые обнаруженные | `QuestSnapshotDto.newlyDiscoveredQuestIds`, `OnQuestDiscovered(questId, displayName)` | есть |
| Слежение (один квест) | `QuestTracker` (`Track/Untrack/Toggle`, `OnTrackChanged`) — так делает легаси; в DTO есть `isTracked` и сервер `RequestTrackQuestRpc` | есть; окно использует **только** `QuestTracker` |
| Принять | `QuestClientState.RequestAcceptQuest(questId, fromNpcId)` | есть |
| Отказаться | `ui.quest.reject_unavailable` | **нет на сервере** |
| Сдать | `QuestServer.RequestTurnInQuestRpc(questId, toNpcId)` — без клиентской обёртки, вызывается из диалога | окно **не вызывает** |
| Кто выдаёт / кто принимает | в `QuestProgressDto` нет | **проверить** клиентский каталог определений квестов; иначе скрыть |
| Награды | легаси не показывает | **нет** → скрыть или «предложение, не канон» |

## 4. Контракты

| Поле в моде | Источник | Статус |
|---|---|---|
| Контракт | `ContractDto {contractId, type, typeLocalizationKey, typeUiClass, state, itemId, displayName, quantity, fromLocationId, toLocationId, reward, cargoValue, timeLimit, timeRemaining, isReceiptContract, receiptCargoIssued, receiptCargoShipNetworkObjectId}` | есть |
| Активные / доступные | `ContractSnapshotDto.active[]` / `available[]` (только после `RequestList(locationId)` у локации) | есть; доступные — только у терминала локации |
| Может брать | `ContractSnapshotDto.canAcceptContracts` | есть |
| Таймер рынка | `marketTimeMultiplier`, `secondsUntilNextTick` | есть |
| Действия | `ContractClientState.RequestAccept / RequestComplete / RequestFail / RequestReceiveCargo` | есть |

## 5. Корабли

| Поле в моде | Источник | Статус |
|---|---|---|
| Список кораблей игрока | как `MyShipsTab.RefreshShipList`: хост — `InventoryWorld.Instance.GetMyShips(localClientId)`; клиент — Key-предметы `InventoryClientState` → `KeyRodInstanceWorld.GetInstance(instanceId).registeredShipId` → `NetworkManager.SpawnManager.SpawnedObjects` → `ShipController` | есть |
| Имя, класс, позиция, топливо, прочность, груз (слоты) | `ShipController.TelemetryState` → `ShipTelemetryState {displayName, className, position, fuelNormalized, fuelMax, hullCurrent, hullMax, cargoUsed, cargoMax, moduleCount, state, flags, shipColorR/G/B}`; также `ShipTelemetryClientState.GetShipState(netId)`, `OnShipStateChanged` | есть; `cargoUsed/cargoMax` — слоты с учётом модулей |
| Двигатель ON/OFF | `ShipController.IsEngineRunning` | есть |
| Дозаправка | `ShipTelemetryState.flags & FlagRefueling` | есть |
| Модули | `ShipController.ShipModuleManager.slots[]` → `ModuleSlot {isOccupied, installedModule}`; `ShipModule {moduleId, displayName, …}`; подпись слота в легаси — `slot.gameObject.name` | есть; тип слота и `powerConsumption/cargoSlotsBonus` — **проверить поля** `ShipModule`/`ModuleSlot` |
| Класс груза (Light/Medium/HeavyI/HeavyII) | `ShipClassMappingConfig.Default.Resolve(ship.ShipFlightClass)` (так делает сервер) | есть |
| Лимиты класса | `ShipCargoLimitsConfig.Default.Resolve(cls)` → `maxSlots, maxWeight, maxVolume, penaltyFactor` (4/100/3/.05, 10/500/12/.08, 20/2000/40/.10, 30/5000/80/.12) | есть; эффективные лимиты с модулями считает сервер (`TradeWorld.TryCheckEffectiveCargoLimits`) — на клиенте только слоты в телеметрии |
| Строки груза | `ShipCargoClientState.RequestDetail(netId)` → `GetCargoDetail(netId)` → `ShipCargoDetailState.cargoDetail[]` (`CargoDetailDto {itemId, displayName, quantity, unitWeight, flags: bit0 опасный, bit1 хрупкий}`), событие `OnCargoDetailUpdated(netId, ok, reason)` | есть; легаси `MyShipsTab` читает `sc.TelemetryCargoState.cargoDetail`, а `ShipTelemetryClientState.GetShipCargoDetail` помечен `Obsolete` («broadcast-кэш больше не заполняется») — **проверить**, какой канал реально наполнен |
| Объём груза (м³) | в `CargoDetailDto` нет; `TradeItemDefinition.volume` по `itemId` | **проверить** клиентский доступ к `TradeItemDatabase` |
| 3D-вид корабля | компонента превью нет | новое, только визуал (`02` §5) |

## 6. Багаж игрока и перенос

| Поле в моде | Источник | Статус |
|---|---|---|
| Предметы | `InventoryClientState.Instance.CurrentSnapshot` → `InventorySnapshotDto {locationId, items[], maxSlots, credits}`; `InventoryItemDto {itemId, type, quantity, slotIndex, instanceId, itemName}`; `GetItems()`, `GetItemsByType(ItemType)` | есть |
| Тип | `ProjectC.Items.ItemType`: Resources=0, Equipment, Food, Fuel, Antigrav, Meziy, Medical, Tech, Key=8 | есть |
| Вес, стек, описание, иконка | `InventoryClientState.GetItemDefinition(itemId)` → `ItemData {itemName, itemType, description, icon, equipSlot, maxStack, weightKg}` | есть |
| Лимит веса багажа | нет | нет (как и в моде — честно безлимитный) |
| Курс упаковки | `ExchangeRateEntry {warehouseItemId, warehouseQty, inventoryItemName, inventoryQty, displayName}` из `ExchangeRateConfig` (`Resources/Exchange/DefaultExchangeRate.asset`) | есть |
| Перенос в трюм / из трюма | `ShipCargoServer.Instance.RequestStoreToCargoRpc(shipNetId, inventoryItemId, count)` — `count % inventoryQty == 0`; `RequestRetrieveFromCargoRpc(shipNetId, cargoItemId, count)` — кратно `warehouseQty`; ответ — `ShipCargoClientState.OnResultReceived(ShipCargoResultDto)` | есть; в игре вызывается из `ShipCargoConsoleWindow` |
| «Последние поступления» | нет | **нет в игре** → `CnReceiptsJournal`: разница между снапшотами `OnSnapshotUpdated`; источник — по совпадению с `ContractClientState.OnContractResult`, `MarketClientState.OnTradeResult`, `CraftingClientState.OnCraftingCompleted`, `GatheringClientState.OnGatherCompleted`, `ShipCargoClientState.OnResultReceived`; иначе «источник неизвестен»; хранится только в памяти окна |

## 7. Локализация

Все подписи — через `ProjectC.Localization.Loc.Get(key)` и `Loc.OnLocaleChanged` (как легаси). Ключи легаси переиспользуются
(`ui.character.*`, `ui.ship.*`, `ui.quest.*`, `ui.cargo.*`); новые ключи — с префиксом `ui.cn.*`.
