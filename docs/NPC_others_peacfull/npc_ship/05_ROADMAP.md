# 05 — Roadmap: Peaceful NPC Ships

> **Статус:** Дизайн-документ ✅. 13 решений приняты 2026-06-22.
> **Тикеты:** T-NS00..T-NS10 (NS = Non-player Ship)
> **Всего:** ~11 тикетов, ~10-15 часов coding + verify
>
> **Скоп M1:** 4 NPC, 2 станции (Примум + 1 зона вблизи), `WorldScene_0_0`. Тестовая конфигурация, расширяемая в v1.5.

---

## 1. Milestones

| Milestone | Название | Что внутри | Тикетов | Статус |
|-----------|----------|------------|---------|--------|
| **M-NS-1** | Core + Movement | POCOs, ShipController hook, scene-placed controller, SO | T-NS00..03 (4) | ⏳ |
| **M-NS-2** | Server hub + Traffic | NpcShipWorld, TrafficManager, server-internal docking API | T-NS04..06 (3) | ⏳ |
| **M-NS-3** | Integration + 1st test | ClientState, pad contention, 4 NPC корабля в WorldScene_0_0 | T-NS07..10 (4) | ⏳ |
| **M-NS-V2** | Cargo + Market + Autopilot (v2) | Cargo manifest, demand routing, events, persistence, HUD, player autopilot | T-NS-V2-01..06 (6) | ⏳ |

**M-NS-1 + M-NS-2:** ~5-7 часов coding  
**M-NS-3:** ~5 часов coding + расстановка в сцене  
**V2:** отдельная фаза

---

## 2. Dependency graph

```
T-NS00 (Core: NpcShipState + NpcShipRoute + NpcShipCargoManifest)
  ↓
T-NS01 (ShipController.ApplyServerInput + _hasNpcPilot flag)
  ↓
T-NS02 (NpcShipSchedule SO + NpcShipController scene-placed NetworkBehaviour)
  ↓
T-NS03 (NpcShipWorld — server singleton + FSM tick)
  ↓
T-NS04 (NpcShipTrafficManager — Gaussian arrival shaping)
  │           └───────────────┐
  │                           ▼
T-NS05 (DockingWorld.AssignPadForNpc + ReleaseNpcAssignment + maxConcurrentLandings support)
  │          └──────────────┐
  │                         ▼
T-NS06 (NpcShipServer hub + BootstrapScene placement + ScenePlacedObjectSpawner)
  │
  ├──────────────────────────────────────────────┐
  ▼                                               ▼
T-NS07 (NpcShipClientState + DTOs)      T-NS08 (Pad contention: player displacement)
  │                                               │
  └───────────────────┬──────────────────────────┘
                      ▼
            T-NS09 (4 NPC корабля в WorldScene_0_0)
                      ↓
            T-NS10 (Smoke test + документация + testing guide)
```

---

## 3. Детальные тикеты

### T-NS00: Core POCOs (60 мин, ~150 LOC)

**Milestone:** M-NS-1  
**Файлы:**
- `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipState.cs` — enum + class
- `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipRoute.cs` — struct + NpcShipDemandCategory enum
- `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipCargoManifest.cs` — v2-hook struct + NpcCargoEntryDto (Q10: пустой в M1)

**Acceptance:** compile 0 errors, все struct INetworkSerializable реализованы.

---

### T-NS01: ShipController.ApplyServerInput + _hasNpcPilot (30 мин, ~50 LOC)

**Milestone:** M-NS-1  
**Файл:** `Assets/_Project/Scripts/Player/ShipController.cs`

**Что делаем (Q1, Q2):**
- Добавляем public server-only метод `ApplyServerInput(thrust, yaw, pitch, vertical, boost)` — generic API (может быть использован NPC-pilot и v2 player-autopilot)
- Добавляем bool `_hasNpcPilot` + public server-only `EnableNpcPilot(bool)`
- Добавляем public property `AntiGravity` (для Q8 override)
- Изменяем `FixedUpdate` gate (line 773): `if (_pilots.Count == 0 && !_hasNpcPilot) return;`

**Acceptance:** Player input продолжает работать, NPC input работает параллельно.

---

### T-NS02: NpcShipSchedule SO + NpcShipController (90 мин, ~250 LOC)

**Milestone:** M-NS-1  
**Файлы:**
- `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipSchedule.cs` (ScriptableObject)
- `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` (NetworkBehaviour)
- `Assets/_Project/Scripts/PeacefulShip/Network/NpcShipZoneRegistry.cs` (static lookup)

**Что делаем:**
- SO с маршрутами + Gaussian параметрами
- NetworkBehaviour на корне NPC-корабля с `ApplyMovementInput()` + `ServerTeleport()`
- Статический реестр `NpcInstanceId → NpcShipController`
- Q8: `AntiGravityBoostAfterExitDocked()` корутина в NpcShipController
- Q3: `npcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL`
- Q2: в `OnNetworkSpawn` вызывается `ship.EnableNpcPilot(true)`

**Acceptance:** NPC-корабль на сцене + через MCP в Unity → спавнится через ScenePlacedObjectSpawner + EnableNpcPilot = true.

---

### T-NS03: NpcShipWorld — server state (60 мин, ~200 LOC)

**Milestone:** M-NS-2  
**Файл:** `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipWorld.cs`

**Что делаем:**
- Singleton, `CreateAndInitialize()` / `Shutdown()`
- `RegisterNpc()` / `UnregisterNpc()` / `GetNpc()`
- `TickNpc(state, dt)` — FSM (см. 04_LIVING_BEHAVIOR.md §2)
- Events: `OnNpcShipArrived`, `OnNpcShipDeparted`, `OnNpcShipLoaded/Unloaded` (stubs для v2)

**Acceptance:** Ticks на сервере, не падает.

---

### T-NS04: NpcShipTrafficManager (45 мин, ~120 LOC)

**Milestone:** M-NS-2  
**Файл:** `Assets/_Project/Scripts/PeacefulShip/Network/NpcShipTrafficManager.cs`

**Что делаем:**
- Singleton MonoBehaviour в BootstrapScene
- `ScheduleNextArrival(stationId, schedule, now)` — Gaussian + spacing (Q11: 4 NPC → ~4 мин среднее между прибытиями на одной станции)
- `GetLastArrivalAt(stationId)` / `RegisterArrival(stationId, proposedAt)`
- Box-Muller transform

**Acceptance:** `Debug.Log` печатает расписание прибытий, 0-errors.

---

### T-NS05: DockingWorld.AssignPadForNpc + maxConcurrentLandings (60 мин, ~100 LOC)

**Milestone:** M-NS-2  
**Файлы:**
- `Assets/_Project/Scripts/Docking/Core/DockingWorld.cs` (расширение)
- `Assets/_Project/Scripts/Docking/Network/DockingServer.cs` (NotifyTouchedDownRpc NPC-рута)

**Что делаем:**
- `AssignPadForNpc(station, ship, shipClass, npcInstanceId)` — Assign + Confirm в один шаг
- Q6: проверка `maxConcurrentLandings` — если лимит достигнут, `STATION_FULL`
- `ReleaseNpcAssignment(npcInstanceId, shipNetId)` — Release + ExitDocked
- Helper `IsNpcInstanceId(ulong id)` — `id > 0x7FFF_FFFF_FFFF_FFFFUL`
- Модифицируем `NotifyTouchedDownRpc`: если `ship.GetComponent<NpcShipController>()` → server-internal ConfirmTouchdown

**Acceptance:** NPC может занять pad, не превышает maxConcurrentLandings, освобождает pad корректно.

---

### T-NS06: NpcShipServer hub (60 мин, ~150 LOC)

**Milestone:** M-NS-2  
**Файлы:**
- `Assets/_Project/Scripts/PeacefulShip/Network/NpcShipServer.cs`
- BootstrapScene placement (через MCP)
- Регистрация в `ScenePlacedObjectSpawner`

**Что делаем:**
- NetworkBehaviour hub, создаёт `NpcShipWorld` и `NpcShipTrafficManager`
- `DiscoverNpcShipsDelayed()` — поиск всех `NpcShipController` в сцене через 2 сек

**Acceptance:** При старте сервера Debug.Log показывает «Discovered 4 NPC ships».

---

### T-NS07: NpcShipClientState + DTOs (45 мин, ~120 LOC)

**Milestone:** M-NS-3  
**Файлы:**
- `Assets/_Project/Scripts/PeacefulShip/Client/NpcShipClientState.cs`
- `Assets/_Project/Scripts/PeacefulShip/Dto/NpcShipSpawnDto.cs`
- `Assets/_Project/Scripts/PeacefulShip/Dto/NpcShipStatusDto.cs`

**Acceptance:** ClientState singleton живёт, DTOs сериализуются.

---

### T-NS08: Pad contention (player displacement) (30 мин, ~60 LOC)

**Milestone:** M-NS-3  
**Файл:** `Assets/_Project/Scripts/Docking/Core/DockingWorld.cs`

**Что делаем:**
- В `ConfirmAssignment`: проверка на NPC-occupant → `OnPadTakenByPlayer` event
- NPC: Docked → Diverting → next leg

**Acceptance:** Игрок подтверждает pad, занятый NPC → NPC отстыковывается и улетает.

---

### T-NS09: 4 NPC корабля в WorldScene_0_0 (60 мин, расстановка)

**Milestone:** M-NS-3  
**Что делаем (через Unity MCP, Q11, Q12):**
- 4 префаба NPC-корабля (Light-class «Курьер Примум-Терциус» + Medium-class «Торговец»)
- Расстановка на pad'ах `DockStation_Primium` в WorldScene_0_0
- Расстановка на pad'ах второй тестовой станции (Зона 2 вблизи)
- Назначение `NpcShipSchedule` каждому (RoundTrip Примум ↔ Зона 2)

**Acceptance:** При старте host'а — 4 NPC корабля заспавнены, через 2 сек зарегистрированы в NpcShipWorld.

### T-NS10: Smoke test + документация (30 мин)

**Milestone:** M-NS-3  
**Файлы:** `docs/NPC_others_peacfull/pc_ship/CHANGELOG.md` (обновление)

**Чеклист smoke test (4 NPC в WorldScene_0_0):**
1. ✅ Start Host → console: «Discovered 4 NPC ships»
2. ✅ 4 NPC корабля стоят на pad'ах → `IsDocked` true, kinematic true
3. ✅ Через 30-90 сек dwell → `ExitDocked()` → `AntiGravityBoost` → `Departing` → движение
4. ✅ В полёте → `ApplyServerInput()` работает через `_hasNpcPilot=true`, корабль движется
5. ✅ Прибыл к Зоне 2 → `AssignPadForNpc` success → `EnterDocked`
6. ✅ Игрок нажал T → RequestDocking на pad, занятый NPC → NPC Divert → next leg
7. ✅ При `maxConcurrentLandings=1` и оба NPC хотят сесть → второй получает `STATION_FULL` → Holding → Diverting
8. ✅ 0 errors в консоли

---

## 4. Оценка времени (M1)

| Тикет | Часов | LOC | Risk |
|-------|-------|-----|------|
| T-NS00 | 1.0 | 150 | Low |
| T-NS01 | 0.5 | 50 | Medium (ShipController hack + flag) |
| T-NS02 | 1.5 | 250 | Low |
| T-NS03 | 1.0 | 200 | Medium (FSM сложность) |
| T-NS04 | 0.75 | 120 | Low |
| T-NS05 | 1.0 | 100 | Medium (DockingWorld модификация + maxConcurrent) |
| T-NS06 | 1.0 | 150 | Low |
| T-NS07 | 0.75 | 120 | Low |
| T-NS08 | 0.5 | 60 | Medium (логика contention) |
| T-NS09 | 1.0 | 0 (MCP) | Low |
| T-NS10 | 0.5 | doc | Low |
| **Итого M1** | **~10.5** | **~1200 LOC** | |

**V2 (отдельная фаза):** T-NS-V2-01..06 — ~7 часов.

---

## 5. V2: Cargo + Market + Autopilot

Отдельный Phase после стабильной M1. Тикеты:

| Тикет | Часов | Что |
|-------|-------|-----|
| T-NS-V2-01 | 1.5 | `OnNpcShipLoaded/Unloaded` → `TradeWorld.GetOrLoadCargo` integration |
| T-NS-V2-02 | 1.0 | `NpcShipRoute.demandCategory` dynamic из `MarketTick` |
| T-NS-V2-03 | 1.5 | `JsonNpcShipStateRepository` (persistence) |
| T-NS-V2-04 | 1.0 | HUD widgets (NPC list в `ShipHudController`) |
| T-NS-V2-05 | 1.0 | `QuestServer` подписка на `OnNpcShipArrived` |
| **T-NS-V2-06** | **1.5** | **Player autopilot через `ApplyServerInput`** (Q1 hook) — новый `ProjectC.Player.AutoPilot.AutoPilotController` |
| **Итого V2** | **7.5** | |

---

## 6. Известные риски

| Риск | Вероятность | Митигация |
|------|-------------|-----------|
| `ShipController` регрессия от `ApplyServerInput` | Low | Параллельный pilot-путь не трогается, `_hasNpcPilot` отдельный флаг |
| NPC movement выглядит роботизированно | Medium | Gaussian dwell + jitter + approach arc |
| NPC stuck в Holding навсегда | Medium | Timeout → Diverting → next station |
| Physics-frozen NPC после ExitDocked (gravity) | **Solved (Q8)** | Anti-gravity override 5 сек |
| `maxConcurrentLandings=1` создаёт постоянные contention | Medium | NPC auto-divert → next station быстро |
| Scene unload во время InTransit | Low | M1: только WorldScene_0_0, не стримится |
| 4 NPC недостаточно для визуала | Medium | Q11: расширим в v1.5 после smoke test |
| Z-2 станция не существует | **Задача T-NS09** | Создать минимальный concept-префаб Зоны 2 в той же сцене |

---

## 7. Готовность к старту

✅ Все 13 решений зафиксированы (`06_OPEN_QUESTIONS.md` → Final Decisions)  
✅ `00_README.md`, `03_V2_ARCHITECTURE.md`, `04_LIVING_BEHAVIOR.md` обновлены  
✅ Dependency graph построен  
✅ Оценка времени: ~10.5 часов на M1

**Следующий шаг:** T-NS00 (Core POCOs). Скажите «поехали T-NS00» и начнём.