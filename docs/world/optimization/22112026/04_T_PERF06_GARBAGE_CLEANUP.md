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
| **4** | Временная проба в `PrimumCrowdNpc.ApplyWorldRebase` — объяснить 53 предупреждения из `:401` | нет | Play-прогон + сдвиг (F8) |
| **2** | Ленивая регистрация навмеша палуб | да | **заблокирован контрактом FO** — нужно решение владельца |

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

## Этап 4 — проба вставлена, ждёт одного прогона

Файл: `Assets/_Project/Scripts/World/Npc/Primum/PrimumCrowdNpc.cs`. Всё под `#if UNITY_EDITOR`,
поведение не меняется, вызовы пробы не переставляют существующие ветки и `return`.

Что пишется (только аномалии, успешные случаи молчат):

```
[T-PERF06/4] <name> onBefore=<bool> enabled=<bool> sample4m=<bool> sampleDy=<f2>
             onAfter=<bool> state=<State> pos=(…) home=(…) target=(…)
```

Логируются два случая: агент пришёл на rebase вне меша (`onBefore=False`) и агент был на меше,
но под его позицией нет меша в 4 м (`sample4m=False`) — второй и есть подозреваемый: `Location`
предупреждений — `:401`, а это `_agent.Warp(transform.position)` в ветке «был на меше».

### Инструкция пользователю (один прогон)

1. Console → **Clear**.
2. Play, дождаться загрузки мира, нажать **F8** (один controlled rebase).
3. Дать 5–10 секунд и прислать строки `[T-PERF06/4]` (или разрешить агенту прочитать консоль).

По этим строкам видно, лечится ли 53 предупреждения посадкою через `SamplePosition` вместо `Warp`
в сырую позицию, и не надо ли трогать тир/радиус толпы. После замера проба удаляется отдельным
коммитом — она временная по определению.

## Этап 4 — сделано: парковка вместо `Warp` в пустоту

Проба отработала в замере H (`ProjectC_client_2026-11-22_12-52-39.data` + `console.txt`): **38 строк**,
и все одинаковой формы — `onBefore=True enabled=True sample4m=False onAfter=False`. Читается однозначно:

| Факт из пробы | Что значит |
|---|---|
| `onBefore=True` | агент пришёл на rebase «на меше» — ветка `if (_agent.isOnNavMesh)` |
| `sample4m=False` (38/38) | под пост-ребейз позицией меша нет ни в 4 м |
| `onAfter=False` | `Warp(transform.position)` **отцепил** агента и дал нативное `Failed to create agent` |

Координаты подтверждают причину: `pos` = `home` минус один и тот же сдвиг (≈39 680 / 2 562 / 39 937),
т.е. тело стоит там, где надо, а **навмеш переезжает вместе с `WorldRoot_0_0` не в тот же кадр**.
Комментарий в коде («навмеш едет вместе с корнем») верен по итогу, но не по времени.

**Правка** (файл `Assets/_Project/Scripts/World/Npc/Primum/PrimumCrowdNpc.cs`):

| Было | Стало |
|---|---|
| `ResetPath(); Warp(transform.position)` в ветке «на меше» | `TryRefreshAgentOnNavMesh()`: `SamplePosition(4 м)` → `Warp(hit.position)`, иначе парковка |
| `ReLandOnNavMesh()` вслепую, в т.ч. на выключенном агенте | `Warp` только при `enabled` и только на найденный меш |
| — | `ParkAgent()`: `enabled = false` + флаг; `Tick` будит через `TryRestoreParkedAgent()` |

Восстановление в `Tick`: `SamplePosition(4 м)` → тело садится на меш (`transform.position = land.position`)
→ `enabled = true`; если агент не сел, остаётся запаркованным и пробует следующий тик. Пока запаркован —
`Tick` выходит раньше обычного (LOD/аниматор продолжают работать), поэтому ошибок в консоль нет.

**Что осталось как есть:** `RebaseSampleRadius = 4f` (высота агента ×2, как в `NpcBrain` этапа 1) — тот же
радиус, что был у прежнего `ReLandOnNavMesh`; шире не делаем, чтобы не телепортировать NPC по горизонтали.

**Проверка:** `check_compile_errors` — 0 errors. Play Mode — **NOT RUN, за пользователем**. Ожидание по консоли:
`Failed to create agent` 56 → ~18 (остаются 16 нативных без `Location`, 1 прокси `NpcBrain.cs:913`,
1 `ShipCrewSpawner.cs:154`), предупреждения `PrimumCrowdNpc.cs:410` 38 → 0. Проба оставлена до этого прогона.

## Этап 5 — сделано: 18 null-`GetComponent` в кадре

Замер H дал адрес, которого не было в плане: `GetComponentNullErrorMessage` — **18 вызовов и 11.7 KB**
на геймплейный кадр (20% всего мусора кадра). Механизм известен с T-PERF05: `GetComponent` /
`GetComponentInParent` на **промахе** строит null-сообщение ≈0.63–0.67 KB, `TryGetComponent` — нет.

| Где (узел в профайлере) | Промахов/кадр | KB/кадр | Источник |
|---|---|---|---|
| `AI.NpcBrain.Update` | 8 | 5.05 | `_socialBrain = GetComponent<NpcSocialBrain>()` — lazy-init в `Update`, у NPC без компонента повторяется **каждый** кадр |
| `GlobalMotionWorld.FixedUpdate` → `ApplyFixedPose` | 6 | 4.02 | `GlobalMotionPoseAdapter.CurrentNetworkAncestor()` |
| `GlobalMotionPoseAdapter.LateUpdate` | 3 | 2.01 | тот же путь (`PrepareBaseline` → `ContextValid` → `HierarchyReady`) |
| `GlobalMotionWorld.Update` → `PrepareBaseline` | 1 | 0.67 | тот же путь |

Правки:

- `NpcBrain` (`Update`, `ApplyRebaseTranslation`): `TryGetComponent(out _socialBrain)` — семантика lazy-init
  сохранена (компонент, добавленный позже, так же подхватится), но промах больше не аллоцирует.
- `GlobalMotionPoseAdapter`: `CurrentNetworkAncestor()` и проверка `Rigidbody` в `SupportedStructure()`
  идут по родителям через `FindComponentInParent<T>` (цикл `TryGetComponent`), а не через
  `GetComponentInParent`. Кеша нет намеренно: иерархия читается свежей, поведение 1:1, промах — 0 байт.

**Проверка:** `check_compile_errors` — 0 errors. Play Mode — **NOT RUN, за пользователем**. Ожидание:
`GetComponentNullErrorMessage` 18 → 0 вызовов, −11.7 KB/кадр (f120: 59.1 → ~47.4 KB).

## Этап 2 — заблокирован контрактом FO (кода нет)

**Поправка к первому прочтению замера G.** `ShipDeckNav.LateUpdate` даёт 42.0 KB/кадр в срезе f41..f282,
но это **не дрейф палуб**: 19 вызовов `NotifyNavMeshAdded` — очередь первичной регистрации 20 кораблей
при `MAX_REGISTRATIONS_PER_FRAME = 1` (один на f17 и девятнадцать подряд на f78..f96). 9 750 KB =
**разовая цена входа в мир** (≈513 KB на регистрацию, Editor-side маршаллинг `NavMesh.AddNavMeshData`),
а не повторяющаяся протечка. Повторная регистрация требует сдвига корабля на `_navFrameSeparation * 0.5 =
2 500 м` и истёкшего 30-секундного кулдауна.

**Почему «ленивая регистрация» не ставится кодом.** FO-манифест rebase требует, чтобы **каждая** палуба
сцены была `IsRegistered && IsNavMeshInstanceValid && IsReady`
(`GlobalMotionRebaseSceneRuntimeManifestSource.cs:126`), иначе транзакция отклоняется —
`ship_deck_nav_not_ready:<name>`. То же требуют `ShipDeckNav.TryCapture*` (`:174`, `:197`),
`GlobalMotionShipDeckCombinedTransactionHost` (`:216`) и lifecycle-контракт (`:83`). Значит «регистрировать
только когда на палубе кто-то есть» — это правка FO-контракта, а не локальная оптимизация, и её нельзя
делать без Play-прогона rebase.

**Состояние:** этап закрыт как «решение требуется», код не менялся. Варианты, которые надо выбрать
владельцу:

- (а) оставить как есть: 20 регистраций на вход в мир, дальше — только при сдвиге на 2 500 м;
- (б) разрешить в манифесте «неиспользуемую палубу» — правка контракта + прогон rebase;
- (в) снижать цену одной регистрации: часть 513 KB — Editor-маршаллинг, и в билде цифра другая,
  т.е. сначала нужен не-Editor замер.

## Замер I — `ProjectC_client_2026-11-22_13-47-22.data` + `console.txt` (Allocation Call Stacks ON)

299 кадров, файл **700 MB** — включённые Allocation Call Stacks. Это первый замер, в котором видна
**строка** аллокации, а не только узел дерева.

**Консоль:** 548 записей (H: 582) — `Log` 461, `Warning` 87 (H: 122), `Error`/`Exception` **0**.
`Failed to create agent` **18** (H: 56), из них managed `Location` есть только у двух
(`ShipCrewSpawner.cs:154`, `NpcBrain.cs:915`), остальные 16 — нативные без стека.
`PrimumCrowdNpc` в консоли **отсутствует** (в H — 38 × `PrimumCrowdNpc.cs:410`) ⇒ этап 4 по предупреждениям
подтверждён. Строк пробы `[T-PERF06/4]` — **52**, т.е. 52 парковки за прогон (в H было 38).
Предупреждения: 34 `EngineThrusterVisual.cs:185`, 23 `ShipCargoVisual.cs:211`, 2 `DoorVisibilityBudget.cs:256`,
по одной в девяти местах (новые относительно H: `QuestWorld.cs:87`, `ShipPositionServer.cs:148`,
`GlobalMotionControlledRebaseSlice.cs:1281`).

**Профайлер:** GC/кадр — min 36.6, медиана **77.6** (H: 67.7), p90 165.3, max 12 719.9 (f0 — старт);
`PlayerLoop` медиана 163.12 ms. Пики: f0 12 719.9, f50 6 577.3, f298 4 740.3 (стоп), f48 2 851.7.

### Как читать байты в этом формате (важно для следующих замеров)

| Наблюдение | Разрешение |
|---|---|
| узел `NpcShipWorld.FixedUpdate` = `47088`, листья = `46.0` | `HierarchyFrameDataView.columnGcMemory` — **байты**: 47 088 B = 45.98 KB. Это ошибка формата, а не агрегации |
| сумма всех `GC.Alloc` кадра | 1 132 сэмпла = **80 779 B = 78.9 KB** = ровно `rootGcKB` у `PlayerLoop` |
| f150 и f151 дают одинаковые 46.0 KB / 639 | не баг: те же NPC делают те же аллокации; f152 уже 56.2 KB / 781 (другой путь) |
| размер одной аллокации | метаданные `GC.Alloc` (`GetSampleMetadataAsInt(i, 0)`) — 160 / 40 / 686 / 54 B |
| имена кадров стека | `RawFrameDataView.ResolveMethodInfo` в загруженном профиле не резолвит **ничего** (0/300); работает только `HierarchyFrameDataView.ResolveItemMergedSampleCallstack(itemId, s)`, и только у листьев `GC.Alloc` — callstacks пишутся только для аллокаций |

### Куда уходят 46.0 KB/кадр внутри `NpcShipWorld.FixedUpdate` (f150, 639 аллокаций → 3 строки)

| Строка | Аллокаций | Байт | Механизм |
|---|---|---|---|
| `NpcShipZoneRegistry.cs:97` | 350 | 26 978 (26.4 KB) | `new List<NpcShipController>(16)` в каждом `QueryNearby` ← `NpcProximityZone.cs:230` (`FindClosestConflict`) ← `NpcShipController.cs:842` |
| `MarketConfigCollector.cs:20` | 229 | 17 308 (16.9 KB) | `id.Trim().ToUpperInvariant()` в каждом `GetByLocation` (`DockingZoneRegistry.cs:68`) ← `NpcShipController.cs:1620` (`ResolveCommZone`) ← `TickCruise:1190` ← `NavTick:877` |
| `NpcProximityZone.cs:285` | 60 | 2 802 (2.7 KB) | `foreach` по `NpcBuildZoneRegistry.All` (`IReadOnlyList`) боксит энумератор ≈46 B |

### Промахи `GetComponent` — 14 вызовов / 9.39 KB (этап 5 закрыл не всё)

| Узел | Промахов | Источник |
|---|---|---|
| `GlobalMotionWorld.FixedUpdate` | 9 | `GlobalMotionWorld.cs:205` — `parentTransform.GetComponent<NetworkObject>()` (`ReactivateFromCurrentPose`) |
| `GlobalMotionWorld.Update` | 1 | тот же вызов |
| `GlobalMotionPoseAdapter.LateUpdate` | 3 | на этом пути остался `GlobalMotionReplicator.cs:353` — `GetComponent<NetworkTransform>()` (`HasCompetingWriter`, зовётся из `IsBaselinePlaced`) |
| `OuterCommZone.Update` | 1 | `OuterCommZone.cs:219` — `GetComponent<NetworkPlayerSpawner>()` |

`NpcBrain.Update` (было 8 промахов) и `CurrentNetworkAncestor()` (было 10) в замере I **не промахиваются** —
правки этапа 5 работают.

## Этап 6 — сделано: аллокации в `NavTick` и остаток промахов

| # | Файл / правка | Снято с кадра |
|---|---|---|
| 1 | `NpcShipZoneRegistry.QueryNearby(pos, radius, buffer)` — заполняет буфер вызывающей стороны; `NpcProximityZone`, `NpcShipController`, `NpcShipTrafficManager` держат свой `List` (реестр server-only, вызовы в одном потоке) | 26.4 KB |
| 2 | `DockingZoneRegistry.GetByLocation` — прямой lookup по исходному `locationId` (он нормализуется при регистрации), `Trim/ToUpperInvariant` только при промахе | 16.9 KB |
| 3 | `NpcProximityZone.FindClosestBuildConflict` — индексный обход `IReadOnlyList` вместо `foreach` | 2.7 KB |
| 4 | `GlobalMotionWorld.cs:205` — `TryGetComponent(out parentObject)` | 6.86 KB |
| 5 | `GlobalMotionReplicator.HasCompetingWriter` — `TryGetComponent(out NetworkTransform)` | 2.06 KB |
| 6 | `OuterCommZone.cs:219` — `TryGetComponent<NetworkPlayerSpawner>(out _)` | 0.70 KB |

Ожидание на следующем замере: 78.9 KB/кадр → **~2–3 KB** на том же кадре (остаются `ContractServer` 7.4,
`GlobalMotionWorld` 6.0 + 3.2, `NpcBrain.Update` 1.4, URP 3.8, прочее).

**Проверка:** `check_compile_errors` — 0 errors. Play Mode — **NOT RUN, за пользователем**.
Осталось непроверенным прогоном: восстановление 52 запаркованных агентов толпы после F8 (проба пишет
только факт парковки), поэтому сама проба `[T-PERF06/4]` пока остаётся.
