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
   enabled by name: <Bucket>=N, …             // топ-12, весь список
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

## Уточнение по варианту B (по факту разбора префабов)

Разбор перед реализацией C дал две правки к предыдущей оценке:

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

   **Следствие:** вариант B нельзя сделать одной строкой в базовом префабе. Тот же базовый префаб
   используется сценовыми квесторами в `WorldScene_0_0.unity` (`[Onboarding alfa]`,
   `Q001_NPC_Lyra__Lyra` и др.) и префабом `Gorgona_Pilot` — этим агент нужен: они стоят на мировом
   навмеше. Значит B = **override `m_Enabled: 0` на компоненте `NavMeshAgent` в каждом из 20 файлов
   вариантов** (правка только файлов капитанов, базовый префаб и квестовые NPC не затрагиваются).

## Чего не делал и почему

1. **671 нативное предупреждение.** Механизм: off-mesh агент, оставшийся живым, переоценивается
   на каждом чужом `NavMesh.AddNavMeshData` (записано в `NpcBrain` у T-PERF02: «~24 шт./Add»)
   и при каждом enable. Кандидат — крю: 23 префаба (`Assets/_Project/Prefabs/NPC/Ships/Captains/*`)
   имеют `NavMeshAgent.enabled = true` и инстансятся на палубу в море
   (`ShipCrewSpawner.SpawnMember`: `Instantiate(member.prefab, spawnAnchor.position, …)`).
   - Вариант A (1 строка, но риск): в `NpcBrain.BeginRide` гасить `_agent.enabled`, в `EndRide`
     включать после `Warp`. Меняет инвариант `CanApplyGlobalBaseline` (`_agent == null || !_agent.enabled`)
     для крю на палубе — это FO-контракт, без прогона не беру.
   - Вариант B (без риска для FO): снять `m_Enabled` у `NavMeshAgent` в 23 префабах капитанов —
     но это правка ассетов и тоже требует прогона.
   - Вариант C (диагностика): **сделан** — `NavMeshAgentAudit` (см. раздел «Вариант C — реализован»).
2. **Наши info-логи** (~400 записей, ~10 MB в Editor): это 5–6 строк на корабль при спавне.
   Гасить — правка ~8 файлов; предложен один флаг `ProjectLog.Verbose` (по умолчанию false)
   с переводом туда всех info-строк, чтобы не терять диагностику. Ждёт решения.
3. **Остаток null-сообщений вне адаптера:** `NetworkPlayer.FixedUpdate` 1 496 KB / 2 233,
   `NpcBrain.Update` 1 499 KB / 2 376, `SkillInputService`/`SkillAnimationPlayer` ~0.9 MB —
   источник глубже прямого вызова, отдельный проход.
