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
   - Вариант C (диагностика): разово посчитать живых off-mesh агентов по префабам в рантайме и
     подтвердить, что 671 — именно крю.
2. **Наши info-логи** (~400 записей, ~10 MB в Editor): это 5–6 строк на корабль при спавне.
   Гасить — правка ~8 файлов; предложен один флаг `ProjectLog.Verbose` (по умолчанию false)
   с переводом туда всех info-строк, чтобы не терять диагностику. Ждёт решения.
3. **Остаток null-сообщений вне адаптера:** `NetworkPlayer.FixedUpdate` 1 496 KB / 2 233,
   `NpcBrain.Update` 1 499 KB / 2 376, `SkillInputService`/`SkillAnimationPlayer` ~0.9 MB —
   источник глубже прямого вызова, отдельный проход.
