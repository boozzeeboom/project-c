# Итерации разработки

## Итерация от 2026-11-23 (T-SHIPDECK-01-fix — probe палубы не доставал до палубы)

**Задача:** Перезаход всё ещё роняет игрока вместо высадки на корабль. Лог — `ProfilerCaptures/console.txt` (23.11 21:18).

**Что в логе:** `[T-FO-PERSIST03] PilotSpawnRestored: client=0 pos=(40200.72, 2509.62, 40154.98)` — игрок поставлен ровно в точку палубы из сейва, но строки `[NetworkPlayer] T-FO09H: boarded restored ship` нет вовсе → `TryLoadPlayerShip` вернул false, т.е. в загруженном `ShipPositions.json` (сейв 20:21) `platformShipId=""`. Корабли ресторятся на ~6 с позже (`RestoreCoroutine: waiting 6s`, затем `loading save` → `Restored 23/23 ships`), за это время игрок ушёл вниз, сработал `_deathY` → `ForceDefaultRespawn` в index=0. То есть весь путь T-SHIPDECK-02 не при чём — не было самого `platformShipId`.

**Причина (проверено по сцене, без Play Mode):** у `Ship_Light_root` верх палубы (`BARGE_MAIN_DECK`, BoxCollider, `rb=Ship_Light_root`) лежит на **+0.46** от корня, а игрок в том забеге висел на **+3.48** над корнем (центр капсулы) — последствие мёртвого ввода из T-SHIPDECK-02: ступни в 2.12 м над палубой. Прежний probe (от `pos+1` вниз 2.5 м) останавливался на +1.98 от корня — палубы не доставал. Плюс требование `attachedRigidbody` было ложным фильтром: Rigidbody есть только на корне корабля.

**Правка `ShipController.TryResolveDeckShipId`:** старт `pos + up*0.5`, радиус 0.35, вниз **6 м**, `QueryTriggerInteraction.Ignore`, требование `attachedRigidbody` снято — решает `GetComponentInParent<ShipController>()` + `IsSpawned`.

**Проверка (edit mode, `execute_code`, без Play Mode):**

| центр капсулы | старый probe | новый probe |
|---|---|---|
| корень +1.36 (стоит на палубе) | hit `BARGE_MAIN_DECK` ✅ | hit `BARGE_MAIN_DECK`, dist 1.05 ✅ |
| корень +3.48 (завис, как в сейве) | **miss** ❌ | hit `BARGE_MAIN_DECK`, dist 3.17 ✅ |
| корень +5.00 | miss ❌ | hit `BARGE_MAIN_DECK`, dist 4.69 ✅ |

`check_compile_errors` — 0 errors. Play Mode — **NOT RUN, за пользователем**.

**Условие теста:** текущий сейв (21:17) снова без `platformShipId`, поэтому нужен один тик сейва (5 с) с игроком, стоящим на палубе, и только затем рестарт. Ожидание в логе второго запуска: `[NetworkPlayer] T-FO09H: boarded restored ship 'WorldScene_0_0/Ship_Light_root'`.

## Итерация от 2026-11-23 (T-SHIPDECK-02 — после F-выхода мертвы WASD/F/E)

**Задача:** После регрессии выхода: в полёте (двигатель ON) нажать F → игрок оказывается на палубе, но камера крутится мышью, а WASD/F/E не работают вообще.

**Причина (не сейв/рестор):** гейт координат. Посадка в корабль делает `transform.SetParent(_currentShip.ShipRoot, true)` (`NetworkPlayer.cs:1818`); NGO на смену родителя зовёт `GlobalMotionReplicator.OnNetworkObjectParentChanged` → `StopServer()` (`GlobalMotionReplicator.cs:253-257`, «coordinator reactivates explicitly»). В кресле это терпимо — `Update` содержит исключение `&& !_inShip` (`NetworkPlayer.cs:974`). На выходе `_inShip=false`, а поток никто не поднимал: `TryResolveRole` требует `control.IsActive` (`GlobalMotionApplication.cs:15`) → `IsReadyForSimulation=false` → `CanSimulateInCurrentCoordinates=false` → `Update` выходит ДО движения и до обработки F; `ProcessMovement`/`FixedUpdate` тоже ранний return, поэтому **гравитация не считается** и игрок висит в точке выхода. Явное включение существовало в виде `GlobalMotionWorld.ReactivateFromCurrentPose` (`GlobalMotionWorld.cs:199-217`, комментарий «handoff completion after game logic changed parent/owner»), но **не вызывалось ниоткуда** (0 вызовов по репо).

**Доказательство в сейве (23.11 20:21):** `Ship_Light_root` engine=true в `(8.7183, −53.8580, −37.0131)`; игрок в `(8.7190, −50.3766, −37.0152)` — XZ совпадает до миллиметра (точка `GetExitPosition`), Y выше на 3.48, `inShip=false`, `platformShipId=""`. То есть игрок стоял ровно в точке телепорта выхода, не падал и не дошел до палубы — probe T-SHIPDECK-01 палубу и не мог увидеть (его SphereCast от центра капсулы +1 вниз 2.5 м до палубы не достаёт); сам сейв-путь тут не при чём.

**Правка:** `NetworkPlayer` — после `_inShip = false` в ветке выхода `ReactivateGlobalMotionAfterExit()` (только `IsServer && UsesGlobalCoordinates`): находит `GlobalMotionPoseAdapter` на игроке и `GlobalMotionWorld` на `NetworkManager`, вызывает `ReactivateFromCurrentPose(adapter, GlobalMotionAuthority.Owner, _ => true)`. Правило-пропуск нужен, потому что `null`-валидатор `ActivateServer` отвергает для remote-owner (`GlobalMotionReplicator.cs:145`), а `WrapRules` сам добавляет `actor.ValidateCandidate`.

**Проверка:** `check_compile_errors` — 0 errors. Play Mode — **NOT RUN, за пользователем**. Ожидание в логе: `[NetworkPlayer] T-SHIPDECK-02: global stream reactivated after ship exit`, после чего WASD работает и персонаж встаёт на палубу; если вместо этого предупреждение `reactivate ... refused` — сигнал, что `CanStart` не прошёл (актор вне текущего фрейма/не зарегистрирован).

**Остаток:** сценарий перезахода (T-SHIPDECK-01) теперь надо перепроверить после этой правки: игрок впервые реально дойдёт до палубы, и тогда станет видно, ловит ли probe `TryResolveDeckShipId` палубу в 2.5 м.

## Итерация от 2026-11-23 (T-SHIPDECK-01 — игрок на палубе корабля при перезаходе)

**Задача:** Корабль в воздухе, двигатель ON, игрок вышел по F и стоит пешком на палубе →
выход из игры / рестарт → игрок загружается и падает, вернуться на корабль невозможно.
Ожидание: система возвращает игрока на палубу его корабля (как паром — T-PAROM-21).

**Причина (три звена):**
1. **Сейв не различал палубу корабля.** `CollectPlayers` писал `inShip` (только пилот)
   и `platformRouteId` (только кабинка парома). Пеший игрок на палубе корабля — это
   `inShip=false`, `shipPersistentId=""`; проба `ParomRoute.TryResolveRouteId` палубы
   кораблей не трогает по контракту («у них inShip-путь» — верно только в кресле).
   Факт из `ShipPositions.json` (репро): `players:[{clientId:0, inShip:false,
   shipPersistentId:"", platformRouteId:""}]` при летающем `Ship_Light_root` (engine=true).
2. **Порядок загрузки.** `GlobalMotionPilotSpawnSource` (T-FO-PERSIST03) ставит игрока на
   скорректированную точку сейва СРАЗУ при спавне, а `ShipPositionServer.RestoreCoroutine`
   ресторит корабли позже (`restoreDelaySec = 6` в BootstrapScene). Мост
   `WaitForShipAndBoard` (T-FO09H) ждёт `RestoreCompleted` и сажает на exit живого корабля —
   но он вызывался только для `inShip=true`. Для пешего на палубе не было ничего →
   падение в пустоту точки палубы → смерть по `_deathY` → дефолтный респавн.
3. **Нет спасения после падения.** `TryResolveShipRescuePosition`: `LastShip` после
   свежего старта процесса `null`, а «ближайший owned» считается в 3D с радиусом 200 м —
   зависший в воздухе корабль в момент смерти далеко по Y → цепочка не срабатывает.

**Изменения:**
- `ShipPositionSaveData.cs` — `PlayerPositionSaveData.platformShipId` (T-SHIPDECK-01).
- `ShipController.cs` — `static TryResolveDeckShipId(feetPos, out shipId)`: server-side
  SphereCast вниз (зеркало `ParomRoute.TryResolveRouteId`), `GetComponentInParent<ShipController>`
  на хите с Rigidbody → `ShipPersistentId`. Только пеший режим.
- `PlayerPositionServer.cs` — `CollectPlayers` пишет `platformShipId` при `!inShip`;
  `RestorePlayer` (legacy-путь) — ветка «на палубу живого корабля» до fallback на позицию
  (`GetExitPosition` + `BindRiddenPlatform(ship.transform)`).
- `ShipPositionServer.cs` — `TryLoadPlayerShip` возвращает корабль и для
  `inShip=false + platformShipId` → global-путь FO переиспользует `WaitForShipAndBoard`
  (ждёт `RestoreCompleted`, телепорт на exit, rebase reconnect_board).

**Проверка:** compile — clean. Play-проверку делает пользователь (Play Mode не запускался).

## Итерация от 2026-07-21 (T-PLAYER-PERSIST)

**Задача:** Player-ship position persistence — привязка респавна и сохранения позиции игрока к кораблю. Игрок, вышедший в полёте, при возвращении появляется у своего корабля (зависшего в воздухе). При падении с корабля — респавн на нём же. Корабль без пилота замирает, не тратит топливо, не сдувается ветром.

**Коммит:** `269d4ad` — T-PLAYER-PERSIST: Player-ship position persistence — freeze, save, restore, ship-proximity respawn

**Изменения:**
- `ShipController.cs` — _frozenByNoPilot, freeze в RemovePilotRpc, unfreeze в AddPilotRpc, velocity-zeroing каждый FixedUpdate, guards на idle fuel/wind/antiGravity/ApplyExternalForce
- `PlayerPositionServer.cs` (новый) — singleton, CollectPlayers, GetPendingPlayers, LoadSavedPlayers, RestorePlayer
- `ShipPositionSaveData.cs` — PlayerPositionSaveData + ShipPositionListWrapper.players
- `ShipPositionRepository.cs` — LoadAllWrapper / SaveAll(ShipPositionListWrapper)
- `ShipPositionServer.cs` — единый write ships+players, загрузка players в RestoreCoroutine
- `NetworkPlayer.cs` — RestorePlayerPositionCoroutine (5s delay)
- `PlayerRespawnTracker.cs` — ship-proximity respawn (IsInShip→GetExitPosition, TryFindNearestOwnedShip через MetaRequirementRegistry)
- `BootstrapScene.unity` — [PlayerPositionServer] GameObject

## Итерация от 2026-07-21 (T-PLAYER-PERSIST fix 1 — engine persistence)

**Задача:** Корабль без пилота падал после рестарта сервера — состояние двигателя (`isEngineRunning`) и `_frozenByNoPilot` не сохранялись в ShipPositions.json.

**Коммит:** `8d03865` — T-PLAYER-PERSIST: fix — сохранять isEngineRunning в DTO, восстанавливать двигатель + freeze при рестарте сервера

**Изменения:**
- `ShipPositionSaveData.cs` — +isEngineRunning поле
- `ShipPositionServer.cs` (Update) — сохраняется ship.IsEngineRunning
- `ShipPositionServer.cs` (ApplyRestore) — SetEngineRunning(true) + ApplyPersistenceFreeze()
- `ShipController.cs` — ApplyPersistenceFreeze(): новый public метод для принудительного freeze

## Итерация от 2026-07-21 (T-PLAYER-PERSIST fix 2 — ship-proximity respawn)

**Задача:** Респавн на корабле не работал — после выхода из корабля `IsInShip=false` и `CurrentShip=null`, первый чек в PerformRespawn никогда не срабатывал. `TryFindNearestOwnedShip` падал на проверке KeyRodInstanceWorld.

**Коммит:** `bf6fbf9` — T-PLAYER-PERSIST: fix ship-proximity respawn — LastShip вместо IsInShip/CurrentShip

**Изменения:**
- `NetworkPlayer.cs` — +_lastShip поле, LastShip property, сохраняется перед обнулением _currentShip при выходе
- `PlayerRespawnTracker.cs` — LastShip fallback в PerformRespawn (игрок вышел → упал → респавн на LastShip)
- Восстановлены потерянные поля _inShip, _currentShip, _playerRenderers

## Итерация от 2026-07-21 (T-PLAYER-PERSIST fix 3 — rescue button)

**Задача:** Кнопка «СПАСЕНИЕ» в EscMenu для экстренного респавна на дефолтную точку (игрок застрял / баг / нужен ТП).

**Коммит:** `785f250` — T-PLAYER-PERSIST: кнопка «СПАСЕНИЕ» в EscMenu — респавн на дефолтную точку

**Изменения:**
- `EscMenuWindow.uxml` — +esc-rescue-btn между настройками и выходом
- `EscMenuWindow.cs` — OnRescueClicked → ForceDefaultRespawnServerRpc
- `PlayerRespawnTracker.cs` — ForceDefaultRespawnServerRpc + PerformDefaultRespawn (без ship-проверок)

## Итерация от 2026-07-21 (T-HP01)

**Задача:** Система здоровья персонажа — HP зависит от STR с настраиваемым множителем, отображение в CharacterWindow, респавн при смерти с восстановлением 30% HP.

**Коммит:** `d73322f` — T-HP01: Система здоровья персонажа (HP = base + STR × multiplier)

**Изменения:**
- `Assets/_Project/Scripts/Stats/HealthConfig.cs` — новый ScriptableObject (baseHp=100, strToHpMultiplier=10, respawnHpPercent=0.3)
- `Assets/_Project/Resources/Stats/HealthConfig_Default.asset` — дефолтный конфиг
- `Assets/_Project/Scripts/Stats/StatsServer.cs` — _healthConfig, ComputeMaxHp(), HP в снапшоте
- `Assets/_Project/Scripts/Stats/Dto/StatsSnapshotDto.cs` — currentHp, maxHp поля
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` — динамический HP, death→respawn
- `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs` — RespawnWithHpRestore()
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — HP bar в UI
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — HP элементы
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — HP стили
- `Assets/_Project/Scripts/UI/EscMenu/EscMenuWindow.cs` — фикс SceneManager
- `Assets/BootstrapScene.unity` — StatsServer._healthConfig назначен

## Итерация от 2026-07-22 (T-PLAYER-PERSIST fix 4 — timing races)

**Задача:** Два timing race при restore позиций:
1. Первый save (5s) срабатывал до restore (6s) — перезаписывал `ShipPositions.json` текущими позициями, старые терялись.
2. `RestorePlayerPositionCoroutine` (5s) срабатывала до `ShipPositionServer.RestoreCoroutine` (6s) — `_savedPlayers` был пуст, игрок спавнился на дефолте.

**Коммит:** `64dc04e` — T-PLAYER-PERSIST: fix timing races — save-before-restore + player restore before data loaded

**Изменения:**
- `ShipPositionServer.cs` — `_restoreCompleted` флаг: Update() пропускает save до завершения RestoreCoroutine; сброс `_nextSaveTime` после restore
- `PlayerPositionServer.cs` — `_dataLoaded` флаг + `DataLoaded` property: сигнал что RestoreCoroutine загрузил данные
- `NetworkPlayer.cs` — `RestorePlayerPositionCoroutine` заменён фиксированный `WaitForSeconds(5f)` на цикл опроса `DataLoaded` (timeout 30s)

## Итерация от 2026-07-24 (T-PERSIST-FIX)

**Задача:** Позиции кораблей и игроков перестали сохраняться/восстанавливаться — файл `ShipPositions.json` не создавался, респавн не работал.

**Коммит:** `054386c` — T-PERSIST-FIX: deadlock _restoreCompleted + ThreadPool persistentDataPath

**Изменения:**
- `ShipPositionServer.cs` — `RestoreCoroutine`: `_restoreCompleted` теперь выставляется даже при пустом save (без этого `Update()` никогда не запускал сохранение)
- `ShipPositionServer.cs` — `Update()`: синхронный `_repo.SaveAll()` вместо `ThreadPool.QueueUserWorkItem` (`Application.persistentDataPath` — main-thread-only API)
- `ShipPositionRepository.cs` — `SaveAll(wrapper)`: убран `throw` из фонового потока, добавлен `Debug.Log`

**Причины (2 бага):**
1. Deadlock `_restoreCompleted`: при первом запуске/после удаления save-файла `RestoreCoroutine` делал `yield break` без флага → `Update()` никогда не входил в цикл сохранения
2. ThreadPool + `Application.persistentDataPath`: save уходил в фоновый поток, `Application.persistentDataPath` кидал `UnityException` → `FilePath` молча фоллбэчился на относительный путь → файл писался в корень проекта, а читался из `persistentDataPath`

## Итерация от 2026-07-16

**Задача:** Fix death respawn — NetworkBehaviour.IsServer врал в coroutine/timer (баг NGO 2.x), респавн не срабатывал.

**Коммит:** `efd35c6` — T-HP01: fix death respawn — NetworkManager.Singleton.IsServer вместо NB.IsServer

**Изменения:**
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` — timer вместо корутины, NetworkManager.Singleton.IsServer в TriggerDeathRespawn/SetHp
- `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs` — NetworkManager.Singleton.IsServer в RespawnWithHpRestore
- `Assets/_Project/Scripts/Combat/Implementations/PlayerAttacker.cs` — попутные
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — попутные
- `Assets/_Project/Scripts/Skills/SkillInputService.cs` — попутные
- `docs/Character/respawn/T-HP01-respawn-fix.md` — документация всех 4 итераций debugging
- `docs/Character/respawn/ITERATIONS.md` — перенесён из Assets/_Project/Docs

---

## Итерация от 2026-07-13

**Задача:** Аудит архитектуры систем HP / Damage / Death / Respawn.

**Коммит:** `1ae3690` (рабочих изменений нет, только документация)

**Изменения:**
- `docs/Character/respawn/03_ARCHITECTURE_AUDIT.md` — полный аудит

**Ключевые находки:**
1. 🔴 Три разных `IsServer` паттерна — `PlayerRespawnTracker.Update()` всё ещё на `NB.IsServer`
2. 🔴 Два канала синхронизации HP (NetworkVariable + StatsSnapshotDto)
3. 🟡 `RespawnWithHpRestore` не вызывает `ResetFallTimer()` — риск double-respawn
4. 🟡 Fallback HP=100 при race condition не пересчитывается
5. 🟡 Циклическая зависимость Stats ↔ Combat через GetComponent

**Рекомендация:** рефакторинг R4+R6 из аудита перед следующим этапом.

---

## История изменений

| Дата | Сессия | Изменения |
|------|--------|-----------|
| 2026-07-21 | T-HP01 | Первая имплементация: HealthConfig, HP=base+STR×multiplier, death→respawn |
| 2026-07-16 | T-HP01 fix | 4 итерации debug: IsServer→NM.IsServer, timer вместо coroutine |
| 2026-07-13 | Аудит | 03_ARCHITECTURE_AUDIT.md — 9 дефектов, анализ as-built |
| 2026-07-13 | T-AUDIT01 | Исправление 4 архитектурных расхождений (R1-R4) |

---

## Итерация от 2026-07-13 (T-AUDIT01 fix)

**Задача:** Применить 4 немедленные правки из аудита 03_ARCHITECTURE_AUDIT.md

**Коммит:** `f12e8dc` — T-AUDIT01: исправление 4 архитектурных расхождений HP/Death/Respawn

**Изменения:**
- `Assets/_Project/Scripts/Combat/Implementations/NpcTarget.cs` — R1: IsServer → NM.Singleton.IsServer
- `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs` — R1: IsServer в Update(), R2: ResetFallTimer() в RespawnWithHpRestore
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` — R3: _hpFallbackUsed + retry-loop продолжается после fallback=100
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — R4: RefreshHpFromNetworkVariable() читает HP из PlayerTarget напрямую
- `docs/Character/respawn/03_ARCHITECTURE_AUDIT.md` — статус исправлений
