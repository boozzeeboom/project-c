# T-PERF06 — мусор в горячих путях, агенты вне меша, палубы

Продолжение цепочки T-PERF03 → T-PERF04 → T-PERF05 (замеры D/E/F/G). Основание — замер G
(`ProjectC_client_2026-11-22_12-13-57.data` + `console.txt`), разбор в `03_FO_NULL_COMPONENT_AND_CONSOLE.md`.

## План из четырёх этапов

Порядок выбран пользователем: **3 → 1 → 4 → 2** — сначала то, что не меняет поведение, потом то, что требует
Play-прогона, и в конце то, что упирается в FO-контракт.

| Этап | Что | Меняет поведение | Блокер |
|---|---|---|---|
| **3** | Мусор в горячих путях без изменения поведения | нет | — |
| **1** | `NavMeshAgent` вне меша: не держать `enabled = true` (304 предупреждения `ShipDeckNav.cs:342`) | да | Play-прогон пользователя (код готов) |
| **4** | Временная проба в `PrimumCrowdNpc.ApplyWorldRebase` — объяснить 53 предупреждения из `:401` | нет | Play-прогон пользователя |
| **2** | Ленивая регистрация навмеша палуб (30% установившегося мусора G) | да | FO-контракт: `ship_deck_nav_not_ready` |

Замер G задал адреса: `NetworkPlayer.Update` 5 000 KB за сессию (18.9 KB на вызов, 15 289 `GC.Alloc`) —
это один массив `FindObjectsByType<DoorController>` по 2 800 дверям плюс `GetComponentInChildren<T>(true)`
по 20 кораблям на каждый тик подсказки. Плюс неохраняемые payload-строки пробника в кадрах/тиках.

## Этап 3 — сделано (этот коммит)

### 3.1 Реестр дверей вместо `FindObjectsByType`

`NetworkPlayer` дважды за тик подсказки (`HasNearbyDoorForInteraction`, `TryInteractNearestDoor`) собирал
массив всех `DoorController` мира: 2 800 дверей = **21.94 KB** на вызов, 227 вызовов из 265 →
**4 980 KB** из измеренных 5 000.

Сделано:

- `InteractableManager`: `List<DoorController> _doors` (ёмкость 4096), `RegisterDoor` / `UnregisterDoor`,
  `FindNearestDoor(Vector3, float, out float)`. Обход без аллокаций; уничтоженные элементы вычищаются
  по месту (`RemoveAt(i--)`).
- `DoorController`: `OnEnable` → `RegisterDoor(this)`, `OnDisable` → `UnregisterDoor(this)` с локальным
  флагом `_registeredInInteractableManager`, чтобы двойная активация не давала дубль. Набор = ровно
  «активна и включена», как и было в старом цикле `FindObjectsByType(FindObjectsInactive.Exclude)` +
  `isActiveAndEnabled`.
- `DoorController.DistanceToInteraction(Vector3)`: расстояние до ближайшей точки коллайдера зоны
  взаимодействия, fallback — до трансформа. Коллайдер кэшируется (`InteractionCollider`), а не
  запрашивается `GetComponent` на каждой двери на каждой итерации.
- `InteractableManager.ClearAll()` очищает и `_doors` (сам `ClearAll` по-прежнему без вызовов —
  реестр живёт на `OnEnable`/`OnDisable`, выход из Play снимает регистрацию через `OnDisable`).

### 3.2 Кэш коллайдера посадки на корабль

`FindNearestShip` вызывал `GetComponentInChildren<PilotSeatController>(true)` и
`GetComponentInChildren<Collider>()` на каждый корабль на каждый тик подсказки. Этот overload
`GetComponentInChildren` реализован через `GetComponentsInChildren` и потому **аллоцирует массив**.

Сделано:

- `PilotSeatController.SeatCollider` — коллайдер, закэшированный в `Awake`.
- `InteractableManager`: `Dictionary<ShipController, ShipBoardingTarget>` (кресло, коллайдер, кадр резолва)
  + `ResolveBoardingCollider(ship, out seat)`. Семантика повторяет прежнюю ветку 1:1:
  если коллайдер кресла найден — радиус посадки считается от его bounds; если нет — `seat = null`,
  срабатывает fallback на первый коллайдер в детях, радиус = переданный `boardDistance`.
- Если кресло есть, но его `Awake` ещё не проставил `SeatCollider`, берётся `seat.GetComponent<Collider>()`
  (без аллокаций), иначе кэш навсегда зафиксировал бы чужой коллайдер.
- Не разрешившийся за 60 кадров корабль пере-резолвится (`BoardingTargetRetryFrames`), чтобы не
  «залипнуть» на пустом кэше; уже найденный коллайдер держится без ограничений.
- `UnregisterShip` снимает запись из кэша.

### 3.3 Payload-строки пробника под `IsActive`

`GlobalMotionRuntimeEvidenceProbe.RecordEvent` сам no-op при выключенном захвате, но аргумент-строка
интерполируется у вызывающего, т.е. аллоцируется всегда. Под охрану `if (GlobalMotionRuntimeEvidenceProbe.IsActive)`
переведены оставшиеся **периодические** вызовы:

| Место | Частота |
|---|---|
| `NetworkPlayer.Update` → `Update.beforeProcessMovement` | каждый кадр |
| `NetworkPlayer.FixedUpdate` → `FixedUpdate.begin` / `FixedUpdate.end` | каждый Fixed-кадр |
| `NetworkPlayer.ProcessMovement` → `CharacterController.Move.before` / `.after` | каждый Fixed-кадр |
| `NetworkPlayer.ApplyPlatformCarry` → `PlatformCarry` | каждый кадр на платформе |
| `PlayerRespawnTracker.Update` → `Update.begin` | каждый кадр (сервер) |
| `GlobalMotionReplicator.OnNetworkTick` → `NetworkTick` | каждый сетевой тик |
| `GlobalMotionPoseAdapter` → `SyncTransforms.begin` / `.end` | каждый кадр для актёров без `Rigidbody` (игрок на `CharacterController`) |

Разовые событийные вызовы (спавн, baseline, телепорт, rebase, respawn-переходы) намеренно **не** тронуты:
они идут единицами за сессию, и их тексты — доказательная база FO.

### Ожидаемый эффект

- `NetworkPlayer.Update`: 18.9 KB/вызов → ~0. Остаток — `HasNearbyNpcForInteraction`
  (`FindObjectsByType<NpcController>` на 25–30 NPC, ~0.3 KB) и сама строка пробника при включённом захвате.
- `GlobalMotionPoseAdapter` / `PlayerRespawnTracker` / `GlobalMotionReplicator`: по одной-две строки на кадр
  или тик снимаются; в замере G они были размазаны и отдельными строками не видны, но это тот же путь аллокаций.

### Что НЕ меняется

- Логика подсказки и приоритетов F (дверь → корабль) — тот же порядок и те же радиусы.
- `TryInteractNearestDoor` теперь фильтрует по `isActiveAndEnabled`, а не по `activeSelf` — как и путь
  подсказки. Практической разницы нет (дверей с выключенным `DoorController` в сцене нет), но теперь два
  пути снова согласованы.
- Стоимость обхода: `FindNearestDoor` остаётся O(N) по ~2 800 дверям. Порядок CPU тот же, снят массив
  (21.94 KB) — пространственная разбивка, если понадобится, отдельная задача.

### Проверка

`check_compile_errors` — **0 errors**. Play Mode агентом **не запускался** — прогон и замер за пользователем.

## Этап 1 — сделано: парковка мир-агента вне навмеша

Механизм выбран пользователем: паркуем в `NpcBrain`, как T-PERF02 уже сделал с deck-прокси, сцену
не правим. Файл: `Assets/_Project/Scripts/AI/NpcBrain.cs`.

Что добавлено:

- поля `_agentParkedOffMesh`, `_nextOffMeshParkCheckTime`, константы `OffMeshParkRetrySeconds = 1f`
  и `OffMeshReturnHorizontalTolerance = 0.5f`;
- `MaintainOffMeshAgentPark()` вызывается первым в `Update()` — **до** серверных гейтов, потому что
  предупреждение нативное и шумит на любом пире; внутри собственный троттлинг 1 с.

Логика гейта:

| Состояние | Действие |
|---|---|
| агент включён, `isOnNavMesh` | ничего, снять флаг парка |
| агент включён, вне меша | `enabled = false`, поднять флаг «наш парк» |
| агент выключен и флаг не наш | не трогать (смерть/контракт/сцена) |
| запаркован нами, `SamplePosition(position, height * 2)` даёт меш в ≤ 0.5 м по горизонтали | `enabled = true`; если агент не сел на меш — сразу обратно в парк |

Палуба (`_deckNavActive`, там живёт `_proxyAgent`) и смерть (`BrainState.Dead`) исключены.

**Что меняется по контрактам.** `HasPreparedNavigation()` уже требует `isOnNavMesh`, т.е. для off-mesh
NPC он и так был `false` — вердикт FO-потока (`IsGlobalMotionReady`, `ConfirmGlobalNavigationPrepared`)
не меняется. Меняется `CanApplyGlobalBaseline`: выражение `(_agent == null || !_agent.enabled)` становится
`true`. Для запаркованного (неподвижного) NPC это безопасное направление: приём baseline его не двигает,
а поток как и раньше не готов. Именно из-за этой строки когда-то был отклонён вариант A — здесь
изменение осознанное, по решению владельца.

**Проверка:** `check_compile_errors` — 0 errors. Play Mode — **NOT RUN, за пользователем**. Ожидание:
304 предупреждения `ShipDeckNav.cs:342` → ~0 (все 16 мировых NPC идут через `NpcBrain`: по аудиту C —
`Npc_peacfull_market_zone` ×8, `Q001_NPC_*` ×6, `[Mira]`, `[Onboarding alfa]`).

**Риск, который надо увидеть в прогоне:** если мировой навмеш появляется позже первого кадра, легитимный
NPC запаркуется на 1–2 с и вернётся сам (сэмпл под ним появится, `enabled` включится). Признак — NPC
стоит на месте сразу после старта и потом идёт.

## Этап 4 — не начат

53 предупреждения (`PrimumCrowdNpc.ApplyWorldRebase`, `:401`) объясняются пробой, а не догадкой:
`agent.isOnNavMesh` до/после `Warp`, `NavMesh.SamplePosition(position, 4f)`, `agent.enabled` — под
`#if UNITY_EDITOR`, снимается одним прогоном. Правку поведения толпы без этой пробы делать нельзя.

## Этап 2 — не начат

`ShipDeckNav.LateUpdate` = 42.0 KB/кадр установившегося мусора G, целиком
`NavMeshManager.NotifyNavMeshAdded` (9 750 KB / 19 вызовов = 513 KB на вызов). Один рычаг закрывает
и 30% мусора, и 304 предупреждения. Блокер: `CreateFloatingOriginSnapshot` отклоняет rebase с
`ship_deck_nav_not_ready`, когда палуба не готова, поэтому ленивая регистрация — это правка FO-контракта,
нужно решение владельца и Play-прогон.
