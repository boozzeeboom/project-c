# T-PERF05 — null-`GetComponent` в FO-слое и разбор консоли

Продолжение T-PERF04 (`02_TRIGGER_CALLBACKS_FIX.md`). Замер-основание — `ProjectC_client_2026-11-22_11-31-27.data`.

## Консоль (сохранённый буфер редактора, 1 391 запись)

| Кол-во | Сообщение | Что это |
|---|---|---|
| **671** | `Failed to create agent because it is not close enough to the NavMesh` | нативное предупреждение `NavMeshAgent`, стека нет |
| 23 | `[ShipController] Some meziy modules NOT found!` | наш лог, 1× на корабль |
| 23 | `[KeyRodInstanceWorld] AutoSave: saved 23 active instances.` | наш лог |
| 23 | `[MeziyModuleActivator] Initialized 0 meziy modules (passive mode).` | наш лог |
| 23 | `[JsonKeyRodInstanceRepository] Saved 23 instances…` | наш лог |
| 23 | `[ShipCargoVisual] '_boxPrefabs array is empty'` | наш warning — реальная недоработка префаба, не глушить |
| 14 | `[PilotSeatController] 'PilotSeat': принудительно IsTrigger=true` | наш лог |
| ~20 | `[EngineThrusterVisual] … плечо pivot→visuals N м (> 5 м)` | наш warning |
| 7×3 | `[ShipPositionServer] Saved`, `[PlayerPositionServer] Collected`, `[JsonShipPositionRepository] Saved` | наши логи автосейва |

Распределение по кадрам (замер D): 815 из 1 076 log-вызовов — кадры 1..99, пик f27 = 119 и f28 = 325,
в этих же кадрах `ShipCrewSpawner.SpawnWhenReady()` ×20 и `GameObject.ActivateAwakeRecursively` ×23.
После кадра 100 — 0.68 вызова на кадр. То есть шум стартовый, а не постоянный.

**Главное:** 671 из 1 391 (48%) — не наш лог и из кода не глушится. Это нативное предупреждение
`NavMeshAgent` в момент, когда агент создаётся/включается вне навмеша; стека у него нет
(`callstackTextStartUTF16` = 0), `Debug.unityLogger` и `Application.SetStackTraceLogType`
нативные сообщения не фильтруют. Убрать его можно только устранив причину.

## Что исправлено (коммит `81b7ec8d`)

`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionPoseAdapter.cs`:
`_transport`, пара `_body`/`_controller`/`_agent` (в `Bind` и в `SupportedStructure`) и
`HasCompetingWriter` переведены на `TryGetComponent`. Семантика 1:1.

Это крупнейший оставшийся источник null-сообщений (замер D): `GlobalMotionWorld.FixedUpdate`
5 888 KB / 8 932 вызова, `GlobalMotionWorld.Update` 1 208 KB / 1 832, `NetworkTickSystem.Tick`
946 KB / 1 412 — вместе ~7.7 MB за сессию (8% всего GC).

Ожидание: `GetComponentNullErrorMessage` 31.25 → ~15 KB/кадр, GC сессии ~98.6 → ~91 MB.

## Вариант C — реализован: `NavMeshAgentAudit`

Файл: `Assets/_Project/Scripts/Debug/NavMeshAgentAudit.cs` (namespace `ProjectC.Diagnostics`, новый).
Разовый счётчик живых агентов вне навмеша — гипотезу «671 сообщение = крю на палубах»
нельзя ни подтвердить, ни опровергнуть по профайлеру.

Код compile: 0 errors (`check_compile_errors`). Play Mode агентом **не запускался** — прогон за пользователем.

Как работает:
- `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` поднимает скрытый (`HideAndDontSave`)
  `DontDestroyOnLoad`-объект `[PerfNavAudit]` с компонентом. Гейт — `Application.isEditor || Debug.isDebugBuild`.
- Выключатель без правки кода: `NavMeshAgentAudit.Enabled` (по умолчанию `true`, сбрасывается в
  `SubsystemRegistration`, чтобы выключенное состояние не унаследовалось при выключенном domain reload).
- Два снимка: **8 с** и **20 с** после старта (`WaitForSecondsRealtime`), по одному `Debug.Log` на снимок
  (многострочный, но одна запись консоли — не добавляет шума в замер).
- На снимке: `FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, None)` — считаются и
  выключенные компоненты, и неактивные объекты, и прокси (они `HideAndDontSave`, но находятся).

Формат записи:

```
[PerfNavAudit] t=8s frame=NNN | agents=NN enabled=NN onMesh=NN OFF-MESH=NN (driving=NN, inactiveGo=NN, updatePositionOff=NN)
   off-mesh: onShip=NN world=NN | navmeshWithin60m=NN maxDist=N.N
   off-mesh by name: <Bucket>=N, …            // топ-12 по убыванию
   all agents by name: <Bucket>=N, …          // топ-12 по всем живым компонентам (включая выключенные)
   off-mesh samples: <Bucket>@(x,y,z), …      // до 5 штук
```

Ключи разбивки: имя GameObject агента. Все `NpcDeckNavProxy_<name>` свёрнуты в один бакет
`NpcDeckNavProxy_*` (их per-NPC десятки, иначе разбивка бесполезна). У префабов капитанов имя
корня переопределено вариантом (`zhuk_Captain`, `albatros_Captain`, …), поэтому в бакетах они видны поимённо.
`onShip` = у агента в родителях есть `ShipDeckNav`; `navmeshWithin60m` = `NavMesh.SamplePosition`
нашёл меш в радиусе 60 м (ложь = агент в открытом море, подтверждение палубной гипотезы).

### Инструкция пользователю (один прогон)

1. Console → **Clear** (иначе читаем накопленную историю).
2. Play тот же сценарий, что в замере D, и подождать **25 секунд** — это обязательное условие,
   второй снимок делается на 20-й секунде.
3. Прислать две строки `[PerfNavAudit]` (или разрешить чтение консоли агенту) — по ним
   решается судьба варианта B. Stop Play.
4. После вердикта файл можно удалить: он самодостаточен и ничего в проекте не держит.

Что означают результаты:

| `OFF-MESH` | Вывод |
|---|---|
| 0 на обоих снимках | гипотеза неверна: 671 сообщение приходит в момент создания/включения агента, а не от живых off-mesh агентов. Вариант B не нужен |
| > 0, в разбивке `<ship>_Captain` | подтверждение: гасим `NavMeshAgent` у префабов капитанов (вариант B) |
| > 0, но в разбивке другие имена | правим то, что показал счётчик, а не капитанов |

## Вариант B — применён (решение по результатам C)

### Результат аудита C (два снимка, прогон пользователя)

```
t=8s  frame=4  agents=25  enabled=25  onMesh=9   OFF-MESH=16 (driving=16, updatePositionOff=0)
      off-mesh: onShip=0 world=16 | navmeshWithin60m=8
      off-mesh by name: Npc_peacfull_market_zone=8, Q001_NPC_Bram__Bram=1, Q001_NPC_Kael__Kael=1,
                        Q001_NPC_Lyra__Lyra=1, Q001_NPC_Noll__Noll=1, Q001_NPC_Sela__Sela=1,
                        Q001_NPC_Veska__Veska=1, [Mira]=1, [Onboarding alfa]=1
t=20s frame=34 agents=113 enabled=113 onMesh=83 OFF-MESH=30 (driving=16, updatePositionOff=20)
      off-mesh: onShip=14 world=16 | navmeshWithin60m=15
      off-mesh by name: Npc_peacfull_market_zone=8, Q001_NPC_*=6, [Mira]=1, [Onboarding alfa]=1,
                        bereg_Captain(Clone)=1, citadel_Captain(Clone)=1, gigant_Captain(Clone)=1, …(+11 имён)
```

1. **Гипотеза подтверждена:** 14 капитанов на палубах стоят вне навмеша (`onShip=14`) — ровно те,
   кого `ShipCrewSpawner.SpawnMember` инстансит на палубу в море. Они появляются между 8-й и 20-й
   секундой (на 8 с их ещё нет: `agents=25`), то есть совпадают с пиком логов f27/f28.
2. **16 мировых NPC вне навмеша — не капитаны:** `Npc_peacfull_market_zone` ×8, квесторы
   `Q001_NPC_*` ×6, `[Mira]`, `[Onboarding alfa]`. У 8 из 16 навмеш есть в радиусе 60 м — это
   плейсмент до готовности меша, отдельная тема; вариант B их не касается.
3. `updatePositionOff=20` при `driving=16`: у 14 капитанов `updatePosition=false`
   (`NpcBrain.BeginRide`), но `enabled` остаётся `true` — агент жив и зарегистрирован в нав-системе.
   Это и есть механизм 671 сообщения.
4. `[Mira]` и `[Onboarding alfa]` в списке — инстансы базового префаба в сцене, поэтому базовому
   префабу агент нужен (см. ниже).

### Что разбор префабов дал к оценке B

1. Префабов капитанов **20**, не 23: `Assets/_Project/Prefabs/NPC/Ships/Captains/*.prefab` — 20 файлов
   (`albatros`, `bereg`, `citadel`, `gigant`, `letuchiy`, `lorein`, `mastodont`, `npc_reference_ship`,
   `olimp`, `peshchera`, `reka`, `shmel`, `silfida`, `skat`, `strannik`, `torrens`, `ugolshchik`,
   `vavilon`, `vetrovorot`, `zhuk`). Число 23 в таблице консоли — это число кораблей в мире
   (23 × `[ShipController]`, 23 × `AutoSave`), а не число префабов.
2. Все 20 префабов капитанов — **варианты одного базового префаба**
   `Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab`
   (guid `172a127fa0f35124bb11eda19a17b16e`), каждый переопределяет только `m_Name`, `definition`,
   позицию и текст. `NavMeshAgent` с `m_Enabled: 1` лежит в базовом префабе (fileID `6435950676967608688`
   на корневом GameObject `836964523381807351`).

### Что сделано (этот коммит)

В каждом из 20 файлов `Captains/*.prefab` добавлен override
`m_Enabled: 0` на компонент `NavMeshAgent` базового префаба. Базовый префаб **не тронут** —
у него `m_Enabled: 1`, поэтому сценовые квесторы (`Q001_NPC_*`, `[Mira]`, `[Onboarding alfa]`)
и `Gorgona_Pilot` сохраняют ходьбу по мировому навмешу.

Diff: ровно +4 строки на файл (80 вставок), без переупорядочивания, `.meta` в репозитории игнорируются.
Проверка: `AssetDatabase.LoadAssetAtPath` → `NavMeshAgent.enabled == false` у 20 из 20, у базового — `true`.

Почему это безопасно — проверено, а не предположено:
- капитаны нигде не стоят в сцене: 0 ссылок на их guid в `*.unity`/`*.prefab` (скан 231 файла);
  они появляются только через `ShipCrewSpawner`;
- `GlobalMotionPoseAdapter` не добавляется в рантайме нигде (`AddComponent<GlobalMotionPoseAdapter>` —
  0 совпадений), а в базовом префабе FO-компонентов нет ⇒ капитаны не попадают в `_participants`,
  и `NpcBrain.CanApplyGlobalBaseline` для них никто не спрашивает. Инвариант FO не меняется
  (опасение из варианта A сюда не переносится);
- все навигационные вызовы `_agent` в `NpcBrain` (`SetDestination`, `ResetPath`, `isStopped`,
  `velocity`, `Warp`) стоят под `_agent != null && _agent.isOnNavMesh`; у выключенного агента
  `isOnNavMesh` = false ⇒ новых ошибок и спама не будет;
- ходят капитаны через `_proxyAgent` (`EnsureProxy`, только при `_deckNav.IsReady`).

Оговорка: `NpcBrain.Respawn` делает `_agent.enabled = true`. У капитанов `_respawnEnabled = 0`,
поэтому путь неактивен; если респавн когда-нибудь включат — сообщение вернётся.

## Замер F — `ProjectC_client_2026-11-22_11-57-08.data` (после B)

391 геймплейный кадр, одна сессия. Код — с B (аудит C в проекте: он пишет две строки в консоль и на
метрики не влияет).

| Метрика | E (11-49-36, до B) | F (11-57-08, после B) |
|---|---|---|
| консоль: `Failed to create agent` | 671 | **438 (−35%)** |
| записей в консоли всего | 1 391 | 1 063 |
| `NavMeshManager.NotifyNavMeshAdded` | 17 818 KB / 20 вызовов (891 KB/вызов) | **10 295 KB / 20 (515 KB/вызов, −42%)** |
| `LogStringToConsole` | 28 224 KB / 1 012 | 21 247 KB / 795 (54.3 KB/кадр) |
| GC сессии | 77 730 KB | **64 870 KB (−17%)** |
| GC среднее / медиана | 222.1 / 67.9 KB | 165.9 / **61.0 KB** |
| GC boot f1..40 | 35 909 KB (898 KB/кадр) | 30 784 KB (770) |
| GC steady f41+ | 41 821 KB (134.9 avg / 60.7 med) | 34 086 KB (**97.1** / 56.7) |
| `GetComponentNullErrorMessage` | 10.7 KB/кадр (3 747 KB / 5 723) | 9.2 KB/кадр (3 590 KB / 5 485) |
| `CoroutinesDelayedCalls` | 22 758 KB / 58 | 20 850 KB / 56 (`PrepareAndStartHost` 15 504) |
| `PlayerLoop` CPU медиана / среднее | 64.8 / — | **56.6 / 74.1** |
| `Physics.SendTriggerEvents` медиана / среднее | 17.75 / 35.4 (p90 55.9) | **15.98** / 23.75 (115 вызовов) |

Вердикты:
1. **B подтверждён по трём осям сразу.** Консоль 671 → 438 (−35%), нативная цена одной регистрации
   палубы 891 → 515 KB (−42%), GC сессии 77.7 → 64.9 MB (−17%), CPU-медиана 64.8 → 56.6 ms (−8.2 ms).
   Прогнозы не разошлись с замером ни в одном пункте.
2. **`*_Captain` из `Location` исчез полностью** — капитаны больше не участвуют ни в одном сообщении;
   остаток имеет три других причины (ниже).
3. **CPU по-прежнему упирается в объём триггеров:** `SendTriggerEvents` — #1 по времени (медиана 15.98 ms,
   среднее 23.75). Это геометрия триггеров префаба корабля и `_interactionRadius` консоли, т.е. данные,
   а не код.

## Остаток 438: разбор по `Location` (консоль `console.txt`, 1 063 записи)

Буфер после B, в отличие от буфера D (`callstackTextStartUTF16` = 0, стека не было), **уже содержит
`Location` у нативных предупреждений** — это и даёт адрес каждой группы:

| Записи (N) | Кол-во | `Location` | Что это |
|---|---|---|---|
| 65–77 | 13 | — (нет managed-кадра) | нативные: агент создан вне меша на старте |
| 78–80 | 3 | `Runtime/Export/Debug/Debug.bindings.h:39` | то же, в стеке только нативный кадр |
| 485 | 1 | `PeacefulShip/Crew/ShipCrewSpawner.cs:154` | спавн экипажа |
| **568–667** | **100** | `World/Npc/Primum/PrimumCrowdNpc.cs:401` | `_agent.Warp(transform.position)` в `ApplyWorldRebase()` — ровно 1 сообщение на NPC за один rebase (тир 100) |
| 669–684 | 16 | `PackageCache/com.unity.ai.navigation@42d8…/NavMeshSurface.cs:221` | пере-регистрация мирового навмеша (`RemoveData()`+`AddData()`), на ней переоцениваются 16 мировых NPC вне меша |
| **701–1027** | **304** | `Ship/ShipDeckNav.cs:342` | 20 регистраций палуб × ~15 живых off-mesh агентов |
| 805 | 1 | `AI/NpcBrain.cs:866` | `EnsureProxy` (прокси палубы) |
| | **438** | | |

Механика видна по нумерации записей — они идут **непрерывными блоками**, а не ровным фоном:
100 подряд (568–667) — один проход `ApplyWorldRebase` по 100 NPC; 16 подряд (669–684) — одна
пере-регистрация мирового навмеша; 304 на `ShipDeckNav.cs:342` рассыпаны по всей сессии (N 701…1027)
и сидят точно на 20 вызовах `AddNavMeshData` за сессию (`NotifyNavMeshAdded` = 20).
Это подтверждает механизм, записанный у T-PERF02: живой off-mesh агент переоценивается на каждом
**чужом** `AddNavMeshData` и пишет по сообщению.

Три причины, они не пересекаются:

| Причина | Сообщений | Доля | Где правится |
|---|---|---|---|
| 16 мировых NPC вне мирового навмеша (`Npc_peacfull_market_zone` ×8, `Q001_NPC_*` ×6, `[Mira]`, `[Onboarding alfa]`) — их переоценивают и регистрация палуб, и пере-регистрация мира | 304 + 16 = **320** | 73% | расстановка/бейк в `WorldScene_0_0` — сцена, руки пользователя |
| `PrimumCrowdNpc.ApplyWorldRebase → Warp` | **100** | 23% | наш код, см. ниже |
| нативные кадры + `ShipCrewSpawner` | 18 | 4% | не наш путь вызова |

### PRIMUM: почему `Warp` ругается

`PrimumCrowdNpc.cs:401` — это `_agent.Warp(transform.position)` в ветке `if (_agent.isOnNavMesh)`.
Своего `AddNavMeshData` у толпы нет: мировой навмеш — `NavMeshSurface` на `WorldRoot_0_0`, а пакет
пере-регистрирует его сам, когда двигается трансформ поверхности:

```csharp
// com.unity.ai.navigation NavMeshSurface.cs:520
void UpdateDataIfTransformChanged() { if (HasTransformChanged()) { RemoveData(); AddData(); } }
// :231  RemoveData() → m_NavMeshDataInstance.Remove()
```

`Remove()` отвязывает от инстанса **всех** агентов, которые на нём стояли, при этом `isOnNavMesh`
ещё может отдавать `true` (устаревшее внутреннее состояние). Слепой `Warp` в такой момент заново
создаёт агента и проваливает проверку «близко ли он к навмешу» — ровно это сообщение, 1 на NPC.
Косвенно это подтверждает и следующая группа (669–684) с `Location` = `AddData`: пере-регистрация
в этой сессии действительно идёт сразу после прохода толпы.

Косвенное подтверждение «дело в слепом `Warp`»: `ReLandOnNavMesh()` (строка 423) тоже делает `Warp`,
но в `Location` он не появляется ни разу — там цель уже взята из `NavMesh.SamplePosition`, т.е. заведомо
лежит на меше.

**Правку поведения толпы не делал.** Цена ошибки здесь — не логи, а NPC, застывшие вне навмеша
(это уже случалось: `34185824` → `45a77123`), а проверить это можно только прогоном, который делает
пользователь. Проверка стоит одного прогона и одного временного лога в `ApplyWorldRebase`
(`#if UNITY_EDITOR`, раз на проход): `isOnNavMesh` до и после, результат `NavMesh.SamplePosition(transform.position, 4f)`,
`agent.enabled`. По нему видно, отвязан агент в этот момент или ещё нет.

## Вариант G — гашение своих info-логов: `ProjectLog.Verbose`

Основание — замер F: `LogStringToConsole` 21 247 KB за сессию, из них 10 295 KB — нативная цена
регистрации палуб; остаток **10 952 KB (17% всего GC сессии)** — наши info-строки, 557 из 1 063
записей консоли. Класс шума один: 5–6 сообщений на корабль при спавне, повторённые 23 раза
(23 — число кораблей в мире).

Файл: `Assets/_Project/Scripts/Core/ProjectLog.cs` (новый, namespace `ProjectC.Core`).

```csharp
public static class ProjectLog
{
    public static bool Verbose;                      // по умолчанию false — и в Editor, и в билде
    public static void Info(string message);         // helper: if (Verbose) Debug.Log(...)
    public static void Info(string message, Object context);
}
```

`Verbose` сбрасывается в `SubsystemRegistration` — при выключенном domain reload статика живёт
между сессиями Play Mode. Включается из кода (`ProjectC.Core.ProjectLog.Verbose = true;`), без
правки ассетов и сцены.

Два способа применения — по температуре места:
- **горячие** (строка строится из нескольких значений) — guard, чтобы интерполяция не выполнялась:
  `if (ProjectLog.Verbose) Debug.Log($"…");` — так сделаны все правки этого этапа;
- **холодные** — helper `ProjectLog.Info(...)`.

Что осталось как было: `Debug.LogWarning` / `Debug.LogError` мастер-флаг **не глушит** — реальные
недоработки данных (`[ShipCargoVisual] _boxPrefabs array is empty`, `[EngineThrusterVisual]` плечо
pivot→visuals) остаются в консоли. Локальные флаги компонентов (`_debugLog`, `verboseLog`)
сохранили смысл «детально по этой системе» и работают вместе с мастер-флагом.

Переведено под флаг 7 мест (по частоте из консоли F):

| Строка | Записей F | Файл | Было |
|---|---|---|---|
| `[ShipController] Some meziy modules NOT found!` | 23 | `Player/ShipController.cs:2241` | `if (Debug.isDebugBuild)` |
| `[JsonKeyRodInstanceRepository] Saved N instances to …` | 23 | `Ship/Key/KeyRodInstanceRepository.cs:133` | без гейта |
| `[MeziyModuleActivator] Initialized N meziy modules` + `Registered passive …` | 23 | `Ship/MeziyModuleActivator.cs:134,127` | `if (Debug.isDebugBuild)` / без гейта |
| `[KeyRodInstanceWorld] AutoSave: saved N active instances.` | 23 | `Ship/Key/KeyRodInstanceWorld.cs:159` | без гейта |
| `[CombatServer] RecoverExistingEntities: registered …` + `done` | 32 | `Combat/Network/CombatServer.cs:79,91,104,117,123` | `if (_debugLog)` |
| `[ShipFuelSystem] Initialized. Class: …` | 21 | `Ship/ShipFuelSystem.cs:247` | `if (Debug.isDebugBuild)` |
| `[PilotSeatController] '…': принудительно IsTrigger=true` | 14 | `Ship/PilotSeatController.cs:90` | `if (verboseLog)` |

Итого гасится **159 из 557** info-записей (29%). Остальные 398 — разовые диагностики по одному разу
(383 разных текста), они полезны и оставлены.

Ожидание на следующем замере: консоль 1 063 → ~904 записи; `LogStringToConsole` 21 247 → ~19 000 KB
(при средней цене ~13.8 KB на вызов), т.е. GC сессии 64 870 → ~62 700 KB. Точные цифры — за замером:
оценка построена на средней цене вызова, а не на измеренной цене именно этих строк.

Проверка compile: 0 errors. Play Mode — **NOT RUN, за пользователем**.

## Замер G — `ProjectC_client_2026-11-22_12-13-57.data` + `console.txt` (после варианта G)

### Консоль: все 7 погашенных видов = 0

878 записей против 1 063 в замере F (−185): `Log` 421 (F: 557), `Warning` 457 (F: 506), `Error`/`Exception` 0.
Поиск по тексту сообщений не нашёл **ни одного** вхождения ни одного из 7 погашенных видов
(`Some meziy modules NOT found`, `JsonKeyRodInstanceRepository Saved`, `MeziyModuleActivator`,
`KeyRodInstanceWorld AutoSave`, `CombatServer RecoverExistingEntities`, `ShipFuelSystem Initialized`,
`PilotSeatController`) — 0 из 878. Мастер-флаг работает, `Verbose` не включён нигде.

Остаток шума — ровно тот, что вариант G объявил вне своей области:

| Кол-во | Сообщение | Статус |
|---|---|---|
| **391** | `Failed to create agent because it is not close enough to the NavMesh` (нативное) | разобран по `Location` в замере F: 320 — 16 мировых NPC (сцена), 100 — `PrimumCrowdNpc.ApplyWorldRebase`, 18 — не наш путь |
| 43 | `[CombatServer]` (прочие строки, не `RecoverExistingEntities`) | 1–3 записи на событие |
| 34 | `[EngineThrusterVisual]` | `LogWarning`, реальная недоработка префаба (плечо pivot→visuals) |
| 23 | `[ShipCargoVisual]` | `LogWarning`, пустой `_boxPrefabs` — данные префаба |

86% предупреждений (391 из 457) — снова нативные nav-сообщения, т.е. после G консоль на 86% состоит
из одной темы: пере-регистрация навмешей и агенты вне меша.

### Профайлер: сессия выросла, установившийся режим — нет

283 кадра (f0..f282). GC сессии **87 549 KB** (F: 64 870), среднее 309.4 / медиана 69.2 / p90 384.0 KB
на кадр. `PlayerLoop` медиана 60.4 ms, среднее 103.2, p90 134.2, максимум 4 466.9 ms (стойл-кадр).

| Метрика | F (391 кадр) | G (283 кадра) | Δ |
|---|---|---|---|
| GC сессии | 64 870 KB | 87 549 KB | +35% |
| boot (первые 41 кадр) | 770 KB/кадр | 1 318 KB/кадр = 54 053 KB, **62% сессии** | +71% |
| установившийся режим, f41+ | 97.1 KB/кадр, медиана 61.0 | 138.4 KB/кадр, **медиана 62.1** | среднее +43%, медиана +2% |
| `LogStringToConsole` | 21 247 KB / 795 | 20 191 KB / 745 | −5% |
| `NotifyNavMeshAdded` | 10 295 KB / 20 | 10 326 KB / 20 | 0 |
| `GetComponentNullErrorMessage` | ~9.2 KB/кадр | 3 049 KB / 4 664 | без изменений |
| `PlayerLoop` медиана | 56.6 ms | 60.4 ms | +7% |
| записей консоли | 1 063 | 878 | −18% |

**Сессии несравнимы напрямую, и это надо читать до цифр.** 283 кадра против 391, при этом boot занимает
62% всего мусора G-сессии (54 053 KB за 41 кадр), а средний кадр сессии раздут стойл-кадрами
(p90 384 KB против медианы 69.2, максимум `PlayerLoop` 4.5 с). Медиана установившегося режима
61.0 → 62.1 KB — то есть **в рабочем режиме G не ухудшил и не улучшил ничего**, а рост среднего
(97.1 → 138.4) — это хвост, а не режим. Плюс F-захват удалён с диска: перепроверить его тем же
экстрактором уже нельзя, сравнение идёт с цифрами, снятыми ранее.

**Оценка «~2 200 KB экономии» не подтвердилась.** Факт: `LogStringToConsole` 21 247 → 20 191 KB,
т.е. **−1 056 KB (−5%)**, а не −2.2 MB. Объяснение — в самой метрике: число вызовов упало 795 → 745
при 159 снятых строках, значит цена в профайлере агрегируется по кадрам, а не по сообщениям, и
«13.8 KB на строку» было средней, а не ценой именно этих строк. Фактическая цена снятой записи
≈ **6.6 KB**, и она неотделима от нативных предупреждений, которые идут через тот же путь.

### Где теперь мусор: установившийся режим (f41..f282, 242 кадра, 138.4 KB/кадр)

| KB/кадр | Всего KB | Вызовов | Источник |
|---|---|---|---|
| **45.7** | 11 066 | 364 | `LogStringToConsole` (→ `Application.CallLogCallback` 42.3) — 391 нативное nav-предупреждение + прочие логи |
| **42.0** | 10 164 | 380 | `ShipDeckNav.LateUpdate`, из них 100% — `NavMeshManager.NotifyNavMeshAdded` **9 750 / 19 вызовов** |
| 33.6 | 8 135 | 242 | `Update.ScriptRunBehaviourUpdate` (внутри — пункт ниже) |
| 29.0 | 7 018 | 1 012 | `FixedUpdate.ScriptRunBehaviourFixedUpdate` (`NpcShipWorld.FixedUpdate` 4 260 KB за сессию) |
| 18.7 | 4 517 | — | `PostLateUpdate.UIElementsRepaintPanels` |
| **18.1** | 4 378 | 241 | `Player.NetworkPlayer.Update` |

#### Находка 1: `NetworkPlayer.Update` — это один массив дверей

Сначала поправка к счёту: в первом проходе разбора этот узел дал «10 000 KB / 530 вызовов» — это двойной
счёт, профайлер держит на один вызов два узла (`Assembly-CSharp.dll!…::NetworkPlayer.Update() [Invoke]`
и `Player.NetworkPlayer.Update`), по 5 000 KB каждый. Верные числа: **5 000 KB / 265 вызовов = 18.9 KB
на вызов**, внутри — 15 289 `GC.Alloc` (≈58 аллокаций на вызов).

Источник — `ResolveInteractionHint()` → `HasNearbyDoorForInteraction()` (`NetworkPlayer.cs:1912`):
`FindObjectsByType<DoorController>(FindObjectsInactive.Exclude)` по **2 800 дверям** = один массив
**21.94 KB**. Троттлинг 5 Гц (`INTERACTION_HINT_INTERVAL = 0.2f`, `:1826`) при кадре ~200 ms
пропускает **каждый** кадр, поэтому до этой строки доходит 227 вызовов из 265:
227 × 21.94 KB ≈ **4 980 KB** — сходится с измеренными 5 000 KB. Остальные ~57 мелких аллокаций на
вызов — `GetComponentInChildren<PilotSeatController>(true)` и `GetComponentInChildren<Collider>()`
по каждому из 20 кораблей (`InteractableManager.cs:367,385`) и массив NPC в
`HasNearbyNpcForInteraction` (`:1933`).

Оговорка: в захвате нет callstack'ов аллокаций (`GC.Alloc` — лист без детей), поэтому адрес получен
из кода и арифметики. Для точного указания строки нужен захват с **Allocation Call Stacks**.

#### Находка 2: `ShipDeckNav` — 30% установившегося мусора и те же 391 предупреждение

`ShipDeckNav.LateUpdate` = 42.0 KB/кадр, и целиком это `NavMeshManager.NotifyNavMeshAdded`:
**9 750 KB / 19 вызовов = 513 KB на вызов**. За 242 кадра палуба перерегистрировала навмеш 19 раз
(≈ раз в 13 кадров), и каждая регистрация стоит полмегабайта — это та же нативная цена
`NavMesh.AddNavMeshData`, которая в замере F дала 304 из 438 предупреждений по `Location`
(`ShipDeckNav.cs:342`). Один рычаг закрывает и логи, и 30% мусора установившегося режима;
причина дрейфа палуб — тема T-PERF02, здесь она только измерена.

#### Находка 3: UI Toolkit — boot-цена главного меню, не игровой режим

`PostLateUpdate.UIElementsRepaintPanels` за сессию **17 526 KB (20% всего GC)**, но в установившемся
режиме только 4 517 KB (18.7 KB/кадр), т.е. **13 009 KB (74%) приходится на первые 41 кадр** — постановка
меню. Панель — `PCMM_PanelSettings` (`Assets/Aura/Mods/ProjectCMainMenu/UI/PCMM_PanelSettings.asset`),
18 render tree на кадр (`RenderTreeManager.Process` 5 112 = 18 × 284), аллокации размазаны
(1.8 KB на `RenderTree.UpdateVisuals`) — одного горячего места нет, рычаг здесь «меньше деревьев / не
перерисовывать», а не «починить одну аллокацию». Что меню живо в этой сессии, видно по консоли:
`[PCMM] atmosphere camera hidden/shown` (записи 163, 180, 506) и `[PCMM] gust …`.

#### Находка 4: boot — 62% мусора сессии, и это отдельный фронт

54 053 KB за 41 кадр: `Update.ScriptRunDelayedDynamicFrameRate` **20 465 KB / 40 вызовов**
(82% внутри — корутины, `CoroutinesDelayedCalls`; там же `GlobalMotionPilotRuntime.PrepareAndStartHost`
**13 290 KB / 4 вызова = 3 322 KB на вызов**), UI Toolkit 13 009 KB, `LogStringToConsole` ~9 125 KB
(20 191 за сессию минус 11 066 в установившемся режиме). Это цена старта хоста и постановки меню —
следующий заход должен начинаться с неё, а не с игрового кадра.

### Дальше (не делал, ждёт решения)

1. `ShipDeckNav`: почему палуба перерегистрируется 19 раз за 242 кадра и можно ли регистрировать один
   раз на корабль (9 750 KB + 391 предупреждение одним рычагом).
2. `NetworkPlayer.ResolveInteractionHint`: заменить `FindObjectsByType<DoorController>` на реестр
   (как уже сделано для кораблей в `InteractableManager`) — 5 000 KB за сессию.
3. Boot: `PrepareAndStartHost` 13 290 KB / 4 вызова и постановка меню 13 009 KB.

Все правки требуют Play-прогона для подтверждения — **NOT RUN, за пользователем**.

## Чего не делал и почему

1. **671 → 438 нативных предупреждений: остаток разложен, но не устранён.** B снял капитанов — консоль
   и нативная цена регистрации палубы упали (замер F), но не до нуля. Почему именно так — разобрано выше
   по `Location`: 320 (73%) — 16 мировых NPC вне мирового навмеша (расстановка/бейк в сцене; правка сцены
   агенту запрещена), 100 (23%) — `PrimumCrowdNpc.ApplyWorldRebase → Warp` (нужен прогон с пробой, а не
   слепая правка поведения толпы), 18 (4%) — нативные кадры и `ShipCrewSpawner`. Варианты A и C закрыты:
   A менял FO-инвариант `CanApplyGlobalBaseline` и не понадобился, C выполнен и дал аудит выше.
2. **Наши info-логи — закрыто вариантом G** (`ProjectLog.Verbose`, по умолчанию false): под флаг ушли
   7 мест с 159 записями из 557 (шесть самых частых строк — по одной на корабль). Остаток 398 записей —
   разовые диагностики (383 разных текста), оставлены намеренно. До правки эта группа стоила ~10.9 MB
   (17% GC сессии F). Ожидаемая экономия ~2.2 MB — оценка по средней цене вызова. Замер G: 7 видов = 0
   в консоли, но экономия `LogStringToConsole` всего −1 056 KB (−5%) — см. «Замер G» выше.
3. **Остаток null-сообщений вне адаптера:** `NetworkPlayer.FixedUpdate` 1 496 KB / 2 233,
   `NpcBrain.Update` 1 499 KB / 2 376, `SkillInputService`/`SkillAnimationPlayer` ~0.9 MB —
   источник глубже прямого вызова, отдельный проход.
