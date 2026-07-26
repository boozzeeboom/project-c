# NPC Ship Movement Refactoring — M2

**Date:** 2026-06-23
**Author:** Mavis
**Jira:** M2 (post-POSTMORTEM)
**Status:** Design → Code

## Executive Summary

Полный рефакторинг движения NPC-кораблей. Замена ad-hoc input-based logic на
**диагональный полёт с периодической коррекцией курса** и жёстким разделением
осей: yaw НИКОГДА одновременно с thrust/lift.

## Root Causes (из POSTMORTEM)

| # | Проблема | Корень |
|---|----------|--------|
| P1 | NPC улетают на Y=2900 вместо Y=2535 | Departing→InTransit проверяет только Y, но vertical input остаётся в InTransit |
| P2 | NPC «танцуют» (wiggle) | Thrust + yaw + vertical подаются в одном кадре |
| P3 | Нет самокоррекции курса | FlightDirection не хранится, bearing пересчитывается каждый кадр |
| P4 | Hardcoded thresholds для высоты | cruiseAltitude=30, dist<500 — меняются при смене сил двигателей |
| P5 | Нет учёта разницы высот портов | Корабль сначала набирает 30м, потом летит горизонтально — не diagonal |

## Новая модель движения (Barge 2.0)

### Принципы

1. **Yaw ТОЛЬКО на месте** — никогда не смешивается с thrust или vertical
2. **Diagonal flight** — thrust + vertical могут быть вместе (диагональная прямая A→B)
3. **Periodic course correction** — каждые N секунд проверка отклонения от курса
4. **Trigger-based arrival** — OuterCommZone.commRange для входа в Approaching,
   `DockingPadTriggerBox.IsShipInside` для touchdown
5. **Никаких magic numbers** — все threshold-ы концептуальные (углы, расстояния),
   не привязанные к силам двигателя

### FSM Flow (изменённые состояния)

```
[Idle]
  → [Departing] LIFT ONLY — подъём на 5м над падом
    → [InTransit] YAW (на месте) → DIAGONAL (thrust+vertical) → при дрейфе >30° → YAW → DIAGONAL
      → [Approaching] вошли в OuterCommRange → HOVER (все input=0, anti-grav держит)
        → [if pad assigned] YAW to pad → DIAGONAL to pad → VERTICAL descent
          → [if IsShipInside] EnterDocked → [Docking]
            → [Docked → Loading → Undocking] (без изменений)
              → [Departing] (новый leg)
```

### Movement methods (переписываются)

| Method | Что делает | Input axes |
|--------|-----------|------------|
| `ApplyDepartingMovement` | Vertical lift на 5м | vertical ONLY |
| `ApplyTransitMovement` | Yaw OR Diagonal | yaw ONLY или thrust+vertical |
| `ApplyApproachMovement` | Hover / Yaw / Diagonal / Vertical descent | Одна ось за раз |

### Новые поля NpcShipState

```csharp
public Vector3 FlightDirection;       // направление полёта (Vector3.zero = не установлено)
public Vector3 StartPathPos;           // позиция начала маршрута (для диагонали)
public float LastCourseCheckTime;      // время последней проверки курса
```

### Course correction

- Каждые `COURSE_RECHECK_INTERVAL = 5f` секунд:
  - Вычислить `idealDir = (targetPos - currentPos).normalized`
  - Если `bearing > BEARING_DRIFT_LIMIT (30°)`: сохранить новое направление,
    войти в yaw-фазу (IsYawing)
- В yaw-фазе: только yaw input, пока `|bearing| < YAW_DEAD_ZONE - HYSTERESIS`
- После yaw-фазы: thrust + vertical по диагонали

### Diagonal flight

- Start = `state.StartPathPos` (позиция при Departing)
- End = target station position
- Во время полёта: `targetY = Lerp(startY, endY, progress)`
- Thrust = `Clamp01(dist / 100f) * 0.8f`
- Vertical = `ComputeAltitudeInput(ship, targetY)` (PD-controller)

### Approach sequence

1. **Enter OuterCommRange** → Stop all inputs (hover in anti-grav)
2. **Wait for pad assignment** (throttled 2s) → `state.AssignedPadId` set
3. **Yaw on spot** toward pad position (no thrust, no vertical)
4. **Diagonal** thrust+vertical toward pad (when aligned)
5. **Vertical descent** when `horizDist < 10m` (no thrust, no yaw)
6. **IsShipInside** → EnterDocked → Docking

### Что удаляется

- `_staggerOffset` словарь (unused dead code)
- `_lastArrivalAtStation` в NpcShipWorld (дубликат NpcShipTrafficManager)
- `NPC_ALT_HOLD_GAIN`, `NPC_YAW_GAIN` — больше не нужны (yaw через угол, не через gain)
- `flightDurationSec` из NpcShipRoute (не используется в движении)
- All magic constant thresholds (30f, 500f, 100f как cruiseAlt)

### Что остаётся

- `NpcShipController.ApplyMovementInput` — без изменений (умножает на npcThrustMult/npcYawMult)
- `ShipController.ApplyServerInput` — без изменений
- `DockingWorld.AssignPadForNpc` — без изменений
- `NpcShipServer`, `NpcShipTrafficManager`, `NpcShipZoneRegistry` — без изменений

## Verification

1. Один NPC, маршрут PRIMIUM→TEST_ZONE → должен:
   - Взлететь на 5м (vertical only)
   - Повернуться к TEST_ZONE (yaw on spot)
   - Лететь диагонально (thrust+vertical)
   - Войти в OuterCommRange → остановиться
   - Получить пад → повернуться → долететь → сесть
2. Проверить лог: `[NpcShipWorld:NPC]` каждый transition
3. Проверить консоль: нет спама bearing/yaw/logs
4. После посадки → Undocking → обратный маршрут
