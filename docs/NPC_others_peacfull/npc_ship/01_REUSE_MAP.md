# 01 — Reuse Map: существующие API для NPC-кораблей

> **Цель:** Аудит подсистем Docking, Ship, World — что уже готово для NPC-кораблей, что требует расширения, что — новый код.
>
> **Обновлено 2026-06-22** под финальные решения Q1, Q2 (см. `06_OPEN_QUESTIONS.md`).

---

## 1. Existing API matrix

| API | File:Line | NPC ready? | Reuse level |
|-----|-----------|-----------|-------------|
| `DockingWorld._occupiedPads` | `DockingWorld.cs:25` | ✅ Комментарий «для NPC (Phase 2): clientId = NpcInstanceId» | **Full** |
| `DockingWorld.ScanExistingOccupants()` | `DockingWorld.cs:77-118` | ✅ Находит любой ShipController внутри DockingPadTriggerBox | **Full** |
| `DockingWorld.AssignPad(station, ship, class)` | `DockingWorld.cs:135-212` | ⚠️ RPC-only (от клиента) — нужен server-direct вызов | **Partial** |
| `DockingWorld.RegisterPendingAssignment()` | `DockingWorld.cs:218-235` | ⚠️ NPC не имеет pending — но ConfirmAssign переиспользуем | **Partial** |
| `DockingWorld.ConfirmAssignment()` | `DockingWorld.cs:237-249` | ✅ Принимает ulong clientId (NPC: npcInstanceId) | **Full** |
| `DockingWorld.ReleaseAssignment()` | `DockingWorld.cs:287-313` | ✅ Принимает ulong — NPC не требует изменений | **Full** |
| `DockingWorld.IsPadOccupied()` | `DockingWorld.cs:315-318` | ✅ Не зависит от owner identity | **Full** |
| `DockingWorld.IsPending()` | `DockingWorld.cs:320-328` | ✅ Не зависит от owner identity | **Full** |
| `DockingWorld.ConfirmTouchdown()` | `DockingWorld.cs:263-285` | ✅ Принимает ulong clientId | **Full** |
| `DockingServer.RequestDockingRpc` | `DockingServer.cs:124-177` | ❌ RPC от клиента — NPC не использует | **None** |
| `DockingServer.RequestTakeoffRpc` | `DockingServer.cs:230-249` | ❌ RPC от клиента — но внутри ReleaseAssignment + ExitDocked переиспользуемы | **Partial** |
| `DockingZoneRegistry._stationsByLocation` | `DockingZoneRegistry.cs:17-18` | ✅ LocationId → станция, NPC routing | **Full** |
| `DockStationController.StationId/LocationId` | `DockStationController.cs:30-34` | ✅ Read-only, не зависит от owner | **Full** |
| `DockStationDefinition.LocationId` | `DockStationDefinition.cs:36` | ✅ Синк с MarketZone.LocationId | **Full** |
| `DockStationDefinition.MaxConcurrentLandings` | `DockStationDefinition.cs:42` | ⚠️ Q6 — NPC должны учитывать лимит | **Partial** (расширение нужно) |
| `DockPadLayout.Pads` | `DockPadLayout.cs:44` | ✅ Список pads с compatibleShipClasses | **Full** |
| `DockingPadTriggerBox.IsShipInside` | `DockingPadTriggerBox.cs:31` | ✅ OnTriggerEnter любого ShipController | **Full** |
| `DockPadVisualMarker` (color) | `DockPadVisualMarker.cs:99-130` | ✅ Physics.OverlapSphere — любой ShipController | **Full** |
| `ShipController.EnterDocked()` | `ShipController.cs:71-85` | ✅ Server-only, не зависит от пилота | **Full** |
| `ShipController.ExitDocked()` | `ShipController.cs:88-98` | ✅ Server-only, не зависит от пилота | **Full** |
| `ShipController.SubmitShipInputRpc` | `ShipController.cs:1024-1038` | ❌ Guard `_pilots.Contains(sender)` — не для NPC | **None** |
| `ShipController.SendShipInput()` | `ShipController.cs:1040-1050` | ❌ Вызывает RPC — не для NPC | **None** |
| `ShipController.FixedUpdate (pilot gate)` | `ShipController.cs:773` | ⚠️ Q2 — будет исправлено через `_hasNpcPilot` flag | **Blocker (resolved in M1)** |
| `ShipController.AddPilot(NetworkPlayer)` | `ShipController.cs:1355-1386` | ❌ Только для NetworkPlayer | **None** |
| `ShipController.AntiGravity` (Q8) | TBD — нужно expose | ⚠️ Q8 — публичный getter/setter для override | **Partial (расширение)** |
| `ScenePlacedObjectSpawner.SpawnInScene()` | `ScenePlacedObjectSpawner.cs:166-210` | ✅ Спавнит любой scene-placed NetworkObject | **Full** |
| `ShipOwnershipRegistry.SetOwner()` | `ShipOwnershipRegistry.cs:79-108` | ⚠️ Q4 — NPC НЕ регистрируются | **Skip** |
| `NetworkingUtils.IsServerSafe()` | DockingWorld.cs:363 | ✅ | **Full** |

---

## 2. Hub Pattern — что переиспользовать

**Каноничный паттерн проекта** (см. `docs/Docking_stations/ARCHITECTURE.md §6`):

```
Server Hub (NetworkBehaviour, BootstrapScene)
  → Server State (MonoBehaviour singleton, server-only, DontDestroyOnLoad)
  → Client Projection (MonoBehaviour singleton)
  → DTO (INetworkSerializable struct)
```

Уже реализовано для игроков (Docking):
```
DockingServer
  ├── DockingWorld (server state)
  ├── RPCs: RequestDocking / Confirm / Touchdown / Takeoff
  └── TargetRpcs → DockingClientState
```

Для NPC — аналогично, **но без RPC**:
```
NpcShipServer (NetworkBehaviour, BootstrapScene)
  ├── NpcShipWorld (server state, DontDestroyOnLoad)
  ├── NpcShipTrafficManager (Gaussian shaping, BootstrapScene)
  ├── Server-direct calls (НЕ RPC): AssignPadForNpc / TakeoffForNpc / TickFleet
  └── BroadcastRpcs: NpcShipArrivedClientRpc / NpcShipDepartedClientRpc (для UI)
```

**Ключевые отличия от DockingServer:**
- Нет RPC от клиента — NPC-pilot на сервере, вызывает напрямую
- Нет двустороннего подтверждения (Q7) — NPC не «думает» 30 секунд
- Нет UI-диалогов (CommPanel, DispatcherVoiceLines)
- Есть NPC-трафик-менеджер (Gaussian на nextArrivalAt)

---

## 3. Где bottleneck (требуется новый код)

### Blocker #1: `ShipController._pilots` gate (Q2 — resolved)

```csharp
private void FixedUpdate() {
    if (!IsServer) return;
    if (_pilots.Count == 0) return;  // ← NPC не проходит
    // ...
}
```

**Решение (Q2):** добавить `_hasNpcPilot` flag + new public method `EnableNpcPilot(bool)`.

```csharp
// В ShipController (server-only секция):
private bool _hasNpcPilot = false;

public void EnableNpcPilot(bool enable)
{
    if (!IsServer) return;
    _hasNpcPilot = enable;
}

// FixedUpdate gate:
if (_pilots.Count == 0 && !_hasNpcPilot) return;
```

### Blocker #2: `ShipController.SubmitShipInputRpc` — не для NPC (Q1 — resolved)

```csharp
[Rpc(SendTo.Server)]
private void SubmitShipInputRpc(...) {
    if (!_pilots.Contains(sender)) return;  // NPC блокируется
}
```

**Решение (Q1):** new public server-only method `ApplyServerInput(thrust, yaw, pitch, vertical, boost)`. Generic — может быть использован и для v2 player autopilot.

### Blocker #3: DockingServer RPCs — не для NPC (Q5/T-NS05 — resolved)

Все 4 RPC требуют `rpcParams.Receive.SenderClientId` — NPC не шлёт RPC.

**Решение:** server-internal API `DockingWorld.AssignPadForNpc(npcInstanceId, stationId, shipNetId)` + проверка `maxConcurrentLandings` (Q6).

---

## 4. Что уже работает «из коробки»

1. **Scene-placed спавн:** `ScenePlacedObjectSpawner` спавнит NPC-корабли как любой другой `NetworkObject`.
2. **ScanExistingOccupants:** NPC-корабль, стоящий на pad в сцене, при старте сервера автоматически регистрируется как occupant.
3. **DockPadVisualMarker:** уже показывает pad красным, если там стоит корабль (любой).
4. **LocationId справочник:** DockingZoneRegistry и MarketZoneRegistry — единая система координат городов.
5. **EnterDocked/ExitDocked:** server-only, работают для NPC без изменений.

---

## 5. NPC не использует

| Не используем | Почему |
|--------------|--------|
| `ShipOwnershipRegistry` (Q4) | NPC не имеют KeyRodInstance → не регистрируются |
| `DockStationDefinition.VoiceLines` | NPC не общаются с диспетчером |
| `CommPanelWindow` | NPC не открывает UI диалоги |
| `Departure subsystem` (T-DEPART-*) | NPC не запрашивает вылет у диспетчера — сервер-direct |

---

## 6. Ссылки (file:line)

| Файл | Ключевые строки |
|------|-----------------|
| `Assets/_Project/Scripts/Docking/Core/DockingWorld.cs` | 25 (`_occupiedPads`), 41 (NPC comment), 77 (ScanExistingOccupants), 237 (ConfirmAssignment), 287 (ReleaseAssignment) |
| `Assets/_Project/Scripts/Docking/Core/DockStationDefinition.cs` | 36 (LocationId), 42 (MaxConcurrentLandings) |
| `Assets/_Project/Scripts/Docking/Network/DockingServer.cs` | 124/182/230/255 (RPCs) |
| `Assets/_Project/Scripts/Docking/Network/DockingZoneRegistry.cs` | 17 (_stationsByLocation) |
| `Assets/_Project/Scripts/Docking/Network/DockStationController.cs` | 30 (StationDefinition) |
| `Assets/_Project/Scripts/Docking/Stations/DockingPadTriggerBox.cs` | 31 (IsShipInside) |
| `Assets/_Project/Scripts/Docking/Stations/DockPadVisualMarker.cs` | 115 (OverlapSphere) |
| `Assets/_Project/Scripts/Player/ShipController.cs` | 71 (EnterDocked), 88 (ExitDocked), 773 (pilot gate → Q2 fix), 1024 (RPC guard) |
| `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` | 112 (SpawnInAllLoadedScenes) |
| `Assets/_Project/Scripts/Ship/Network/ShipOwnershipRegistry.cs` | 79 (SetOwner) — Q4: NPC skip |

---

## 7. Что добавляется в M1 (новый код)

| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Scripts/PeacefulShip/Core/*.cs` | POCO state, FSM, route, cargo manifest |
| `Assets/_Project/Scripts/PeacefulShip/Network/NpcShipServer.cs` | Server hub (BootstrapScene) |
| `Assets/_Project/Scripts/PeacefulShip/Network/NpcShipTrafficManager.cs` | Gaussian arrival shaping |
| `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipSchedule.cs` | SO — расписание + routes |
| `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` | Scene-placed NetworkBehaviour |
| `Assets/_Project/Scripts/PeacefulShip/Client/NpcShipClientState.cs` | UI projection |
| `Assets/_Project/Scripts/PeacefulShip/Dto/*.cs` | INetworkSerializable structs |

**Расширения существующих:**
| Файл | Что |
|------|-----|
| `ShipController.cs` | `ApplyServerInput()`, `EnableNpcPilot()`, `AntiGravity` property, FixedUpdate gate fix |
| `DockingWorld.cs` | `AssignPadForNpc`, `ReleaseNpcAssignment`, `IsNpcInstanceId`, maxConcurrentLandings check, displacement event |
| `DockingServer.cs` | `NotifyTouchedDownRpc` — маршрутизация на NPC-internal path |