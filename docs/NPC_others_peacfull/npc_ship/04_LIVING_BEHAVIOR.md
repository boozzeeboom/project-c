# 04 — Living Behavior: «живость» NPC-траффика

> **Цель:** Описать что делает NPC-корабли «живыми», а не просто движущимися по точкам. Расписания, dwell times, traffic shaping, pad contention, визуальные маркеры.
>
> **Обновлено 2026-06-22** под решения Q2 (`_hasNpcPilot`), Q5 (Loading 30-90 сек), Q8 (anti-grav boost), Q11 (4 NPC), Q12 (Примум + 1 зона вблизи).

---

## 1. Что значит «живость»

Игрок, стоящий на платформе города, должен видеть:
1. Корабль подлетает издалека → постепенно увеличивается
2. Замедляется → снижается → заходит на pad
3. Pad меняет цвет (красный — занят)
4. Корабль стоит 30-90 секунд (люди «грузят») — **Q5**
5. Отстыковывается → pad зеленеет → улетает
6. Через некоторое время — следующий корабль

**Без живости:** корабли телепортируются на pad, стоят бесконечно, исчезают.

---

## 2. NPC Ship FSM (12 состояний)

```
[NpcShipWorld.TickNpc]
                         ┌─────────────────────────────────────┐
                         │                                     │
                         ▼                                     │
                     ┌────────┐   route.stops > 0            │
  [Idle] ───────────►│Departing├──────────────────────────┐ │
                     └────────┘                           │ │
                         │   ExitDocked() + rb.isKinematic=false │
                         │   + AntiGravityBoostRoutine(5s) │ Q8
                         │   ApplyMovementInput(thrust=0.6, pitch=+0.3, vertical=+0.5) │
                         ▼                                │ │
                     ┌──────────┐                          │ │
                     │InTransit  │◄───────────────────────┼──┤
                     └──────────┘                          │ │
                         │   dist to target < 500m        │ │
                         ▼                                │ │
                     ┌───────────┐    ┌──────────┐       │ │
              ┌─────►│Approaching├───►│  Holding  │ (5s retry) │
              │      └─────┬─────┘    └──────────┘       │ │
              │            │                              │ │
              │     AssignPad success?                    │ │
              │       YES         NO                      │ │
              │       │            │                      │ │
              │       ▼            └─────────────────►────┘ │
              │   ┌────────┐                                │
              │   │ Docking│  ← ServerTeleport к pad        │
              │   └────┬───┘                                │
              │        │  Auto-detect touchdown             │
              │        ▼                                    │
              │   ┌────────┐                                │
              │   │ Docked │  ← EnterDocked()               │
              │   └────┬───┘                                │
              │        │                                    │
              │        ├──[player displacement]──►Diverting──► nextStop
              │        │                                    │
              │        ▼  dwellTimeSec elapsed (30-90s Q5)  │
              │   ┌─────────┐                               │
              │   │ Loading │  (30-90 сек no-op пауза, Q5)  │
              │   └────┬────┘                               │
              │        ▼                                    │
              │   ┌──────────┐                              │
              │   │ Undocking│  ← ship.ExitDocked()         │
              │   └────┬─────┘                              │
              │        │                                    │
              │        ▼  nextStop → Departing              │
              │   ┌──────┐                                  │
              │   │ Done │  (если цикл закончен)            │
              │   └──────┘                                  │
              │        │ restart                            │
              └────────┘                                    │
```

### 2.1 State transitions — ключевые правила

| From | To | Trigger | Notes |
|------|----|---------|-------|
| Idle | Departing | Один раз после регистрации NPC | First leg |
| Departing | InTransit | `ExitDocked()` + thrust > 0.1 + **anti-grav boost 5s (Q8)** | Рывок от pad |
| InTransit | Approaching | dist < 500m до станции | Начинаем снижение |
| Approaching | Docking | `AssignPadForNpc` success + `maxConcurrentLandings` не превышен (Q6) | Pad выделен |
| Approaching | Holding | `AssignPadForNpc` fail | Все pads заняты |
| Approaching | Diverting | Timeout > 30s | Идём к следующей |
| Holding | Approaching | `AssignPadForNpc` retry success | Через 5 сек |
| Docking | Docked | Touchdown auto-detect | Сработал триггер |
| Docked | Diverting | Player displacement | Игрок занял pad |
| Docked | Loading | `dwellTimeSec` elapsed (30-90 сек Q5) | Минимальная пауза |
| Diverting | InTransit | Начало следующего лега | Перелёт |
| Loading | Undocking | Loading timer elapsed | Разгрузка завершена |

### 2.2 `_hasNpcPilot` flag (Q2)

```csharp
// В ShipController.FixedUpdate (line 773), БЫЛО:
if (_pilots.Count == 0) return;

// СТАЛО (Q2):
if (_pilots.Count == 0 && !_hasNpcPilot) return;
```

`EnableNpcPilot(true)` вызывается из `NpcShipController.OnNetworkSpawn`. `EnableNpcPilot(false)` — из `OnNetworkDespawn` или при ручном debug-disable.

### 2.3 Anti-gravity boost (Q8)

При переходе `Docked → Undocking → Departing`:

```csharp
private IEnumerator AntiGravityBoostAfterExitDocked()
{
    var ship = GetComponent<ShipController>();
    float originalAntiGrav = ship.AntiGravity;
    ship.AntiGravity = antiGravityBoostValue;  // 1.5
    yield return new WaitForSeconds(antiGravityBoostDuration);  // 5 sec
    ship.AntiGravity = originalAntiGrav;
}
```

Зачем: между `ExitDocked()` (снимает `rb.isKinematic`) и первым `ApplyMovementInput` (подаёт thrust+vertical) корабль подвержен gravity. Без boost может «упасть» под платформу. Anti-grav 1.5 компенсирует gravity пока NPC-pilot не подаст вход.

---

## 3. Traffic shaping

### 3.1 Gaussian arrival distribution

```csharp
float u1 = Random.value;
float u2 = Random.value;
float z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
float nextInterval = schedule.meanArrivalIntervalSec + schedule.arrivalIntervalStdDev * z;
nextInterval = Mathf.Clamp(nextInterval, schedule.minArrivalSpacingSec, schedule.meanArrivalIntervalSec * 2f);
```

**Параметры по умолчанию:**

| Параметр | Значение | Эффект |
|----------|----------|--------|
| `meanArrivalIntervalSec` | 480 (8 мин) | Среднее между прибытиями |
| `arrivalIntervalStdDev` | 90 (1.5 мин) | Разброс: 99.7% в [3.5, 12.5] мин |
| `minArrivalSpacingSec` | 60 (1 мин) | Минимум между кораблями на одной станции |
| `globalJitterMaxSec` | 2 | Случайное смещение ±2 сек |

### 3.2 Расчёт для 4 NPC в одной сцене (Q11, Q12)

**Тестовая сцена:** Примум + ещё 1 зона вблизи в `WorldScene_0_0`. 4 NPC корабля:
- 2 корабля: Примум → Зона 2 (RoundTrip)
- 2 корабля: Зона 2 → Примум (RoundTrip)

**Поток:**
- На станции (допустим Примум): ~2 NPC направляются сюда
- Средний интервал между их прибытиями: mean/2 = 480/2 = 240 сек ≈ 4 мин
- С min spacing = 60 сек: игрок видит NPC примерно раз в 1-4 мин на станцию
- 4 NPC = 4 pad-assignments в ~20 мин цикле → **живой трафик**, не перегружает

### 3.3 Jitter + min spacing enforcement

```
proposed = now + gaussian_sample(mean, stdDev)
clamped to [minArrivalSpacingSec, mean * 2]
enforce spacing vs last arrival at same station:
    if proposed - lastArrival < minSpacing:
        proposed = lastArrival + minSpacing
add jitter: proposed += Random.Range(-jitter, +jitter)
```

### 3.4 Когда пересчитывать

- **RegisterNpc:** первое `nextArrivalAt` = now + Gaussian-интервал от schedule.
- **UnregisterNpc:** убрать из расписания.
- **Scene reload:** пересчитать, чтобы не было «все сразу» после рестарта.

---

## 4. Dwell times (Q5)

| Leg | Default dwell | Игрок видит |
|-----|---------------|-------------|
| Docked → Loading | min 30, max 90 сек (Q5) | «Грузят контейнеры» |
| Loading → Undocking | завершение dwellTime | Прощание |
| Undocking → Departing | 5-10 сек | Отход от pad |
| Diverting | 2-3 сек (пауза) | Разворот |

**Loading в M1 = no-op пауза (Q5).** Без cargo нет реальной загрузки, но визуальная пауза создаёт впечатление работы. v2 заменит на `OnNpcShipLoaded/Unloaded` event → `TradeWorld.GetOrLoadCargo`.

**Визуальные подсказки во время Loading:**
- M1: `DockPadVisualMarker` цвет (красный)
- M1.5: TMP label «Загрузка 57%» (progress bar над pad?)
- V2: Particle systems (дым, искры у грузового люка)

---

## 5. Pad contention — сценарии (Q6)

| Ситуация | Реакция | Игрок видит |
|----------|---------|------------|
| Игрок запросил pad, он свободен | Normal assign | Pad зелёный → жёлтый (pending) → красный (docked) |
| Игрок запросил pad, он занят NPC | NPC Divert, освобождает | Pad занят 5 сек → зеленеет |
| Игрок занял pad, NPC в Approaching | NPC Holding | Корабль кружит над станцией |
| Игрок на pad, NPC в Holding 30+ сек | NPC Divert → next station | Корабль улетает прочь |
| `maxConcurrentLandings` достигнут (Q6) | NPC получает `STATION_FULL` → Holding/Diverting | Пад на станции полный — NPC не может сесть |

### 5.1 Player displacement — детали

```csharp
// В DockingWorld.ConfirmAssignment (дополнение)
ulong prevOccupant;
if (_occupiedPads.TryGetValue(padKey, out prevOccupant) && IsNpcInstanceId(prevOccupant))
{
    var npc = NpcShipZoneRegistry.Get(prevOccupant);
    if (npc != null)
    {
        NpcShipWorld.Instance.OnPadTakenByPlayer(prevOccupant);
    }
}
_occupiedPads[padKey] = clientId;  // player takes over
```

На NPC-стороне: `OnPadTakenByPlayer` → `ExitDocked()` → `Undocking` → `Diverting` (next leg).

### 5.2 maxConcurrentLandings для NPC (Q6)

```csharp
// В DockingWorld.AssignPadForNpc
var stationDef = station.StationDefinition;
if (stationDef != null)
{
    int currentLandings = CountLandingsAtStation(stationDef.StationId);
    if (currentLandings >= stationDef.MaxConcurrentLandings)
    {
        return MakeFail("STATION_FULL", ship.NetworkObjectId);
    }
}
```

NPC и игрок ограничены одинаково — нет «NPC-привилегий».

---

## 6. Movement в открытом небе

### 6.1 Waypoint flight (без NavMesh)

Воздушные корабли в открытом небе не требуют pathfinding. Используется:

1. **Прямая линия** к целевой станции (InTransit)
2. **Smooth arrival:** за 500м до станции — замедление (`thrust *= dist / 500`)
3. **Approach arc:** лёгкое снижение за 200м
4. **Snap:** в 10м от pad — `ServerTeleport` на точную позицию

### 6.2 Формулы движения

```csharp
// InTransit
float dist = Vector3.Distance(rb.position, targetStation.position);
float bearing = CalcBearing(rb.position, targetStation.position) - rb.rotation.eulerAngles.y;

ship.ApplyMovementInput(
    thrust: Mathf.Clamp01(dist / 500f) * 0.6f,  // замедление при подходе
    yaw: Mathf.Clamp(bearing, -30f, 30f) * 0.5f,  // плавный поворот
    pitch: 0f,
    vertical: dist < 200f ? -0.2f : 0f  // снижение за 200м
);

// При dist < 10f → ServerTeleport
```

### 6.3 Q1 — ApplyServerInput для автопилота игрока (v2 hook)

Пользователь отметил, что `ApplyServerInput()` может стать основой для автопилота игрока. В v2:

```csharp
// ProjectC.Player.AutoPilot.AutoPilotController (v2, не M1)
[Rpc(SendTo.Server)]
public void RequestAutopilotRpc(Vector3 destination, RpcParams rpcParams)
{
    var ship = GetComponent<ShipController>();
    if (ship == null) return;
    // Запускает корутину, которая периодически вызывает
    // ship.ApplyServerInput(thrust, yaw, pitch, vertical, boost)
    // для движения к destination без участия игрока
}
```

Это означает: `ApplyServerInput` не должен иметь hard-coded ссылок на `NpcShipController` — он generic.

---

## 7. Visual markers (M1 → M1.5 → V2)

| Фича | M1 | M1.5 | V2 |
|------|----|------|----|
| Pad цвет | ✅ Зелёный/Красный | | |
| NPC name label над кораблём | ❌ | ✅ World-space TMP | |
| Loading progress на паде | ❌ | ❌ | ✅ Progress bar |
| Particle effects (дым/пар) | ❌ | ✅ Basic | ✅ Full |
| HUD список NPC в зоне | ❌ | ❌ | ✅ List + status |

---

## 8. Anti-patterns

| ❌ Не делать | ✅ Вместо этого |
|------------|----------------|
| Телепортировать NPC на pad | ServerTeleport только на финальные 10м |
| 15+ состояний FSM | 6 ключевых состояний для M1 |
| NPC занимает pad без проверки | Всегда через DockingWorld.AssignPadForNpc() |
| NPC игнорирует maxConcurrentLandings | Учитывать через CountLandingsAtStation() (Q6) |
| NPC исчезает при unload сцены | Финиширует лега → Idle → re-registration |
| Каждый NPC — unique prefab | 1-2 префаба, locationId определяет маршрут |
| «4 NPC сразу» на станцию | Gaussian spacing + minArrivalSpacingSec |
| NPC «падает» после ExitDocked | Anti-gravity boost 5 сек (Q8) |