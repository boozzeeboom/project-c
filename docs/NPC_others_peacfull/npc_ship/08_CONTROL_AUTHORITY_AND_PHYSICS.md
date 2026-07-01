# 08 — Control Authority & Physical Ship Model

> **Project C: The Clouds** | Unity 6000.4.1f1 | NGO 2.11.0
> **Статус:** M4 — Step (1) реализован (2026-07-01): control-authority switch + гейт NavTick.
> **Связано с:** `07_SHIP_PROXIMITY_AVOIDANCE.md`, `M2_FSM_DIAGNOSIS.md` §3.1.

---

## 1. Цель

Сделать NPC-корабль полноценной **физической сущностью**, которой в будущем сможет
завладеть игрок: пока на борту нет живого пилота — кораблём рулит NPC-автопилот по
своим правилам; как только игрок высаживается и берёт управление — корабль летает по
**правилам игрока** (тот же силовой конвейер `ShipController`).

Ключевой факт: **корабль уже физический.** Игрок летает через силы
(`ShipController.FixedUpdate` → `AddForce`/`AddTorque`, `:1066-1069,1128-1164`).
Единственное, что мешало корректной пересадке — параллельная модель движения NPC
(прямой `Rigidbody`-контроль в `NavTick`), которая продолжала бы работать и «драться»
с игроком.

---

## 2. Модель control authority

Одно физическое тело, **взаимоисключающая власть управления**:

| Authority | Условие | Кто пишет в Rigidbody |
|-----------|---------|-----------------------|
| `HumanPilot` | `ShipController.PilotCount > 0` | Силовой конвейер игрока (`_sumThrust` → `AddForce`/`AddTorque`) |
| `NpcAutopilot` | `_hasNpcPilot && PilotCount == 0` | `NpcShipController.NavTick` (прямой Rigidbody-контроль) |
| `None` | нет пилотов и не NPC | никто (`FixedUpdate` ранний `return`) |

Переключение уже частично закодировано в `ShipController.FixedUpdate`:

```csharp
if (_pilots.Count == 0 && !_hasNpcPilot) return;   // никто не управляет
if (_hasNpcPilot && _pilots.Count == 0) return;    // NPC один → силовую физику пропускаем, рулит NavTick
```

---

## 3. Что сделано в Step (1)

Реализовано в `NpcShipController` (низкий риск, тюнинг физики не тронут):

1. **Гейт `NavTick` при живом пилоте.** В начале `NavTick`, если `ship.PilotCount > 0`,
   NPC-автопилот **немедленно уступает** — не пишет в `Rigidbody`, управление целиком
   у силового конвейера игрока. Это устраняет конфликт «NavTick vs игрок».
2. **Корректный возврат управления.** Когда игрок покидает корабль
   (`PilotCount == 0`), NPC-автопилот возобновляется:
   - если корабль оказался не на паде, а режим завис в `Docked` → переходим в `Cruising`;
   - пересчитываем цель `CruiseTargetPos` от текущего маршрута (`ResolveTargetStation`);
   - сбрасываем незавершённый манёвр расхождения (`_avoidOther = null`).
3. **Явное состояние власти** для телеметрии/логики:
   - `NpcShipController.Authority` (`HumanPilot` / `NpcAutopilot` / `None`);
   - `NpcShipController.IsPlayerControlled`;
   - лог переходов: `Player took control — NPC autopilot yielding` /
     `Player released control — NPC autopilot resuming`.

**Пересадка теперь безопасна:** посадка игрока (`AddPilot` → `PilotCount>0`) сразу
включает силовой конвейер, а NPC-мозг молчит; выход игрока возвращает автопилот.

---

## 4. Что НЕ тронуто (границы Step (1))

- **Тюнинг движения NPC** (`linearVelocity`/`MoveRotation`, `MaxYawRate`, dwell,
  `isKinematic` в `Docked`, `detectCollisions` на взлёте) — без изменений.
- **`ShipController`, `DockingWorld`, NGO/RPC** — без изменений.
- **Физические коллизии под NPC-автопилотом** пока НЕ включены — это Step (2).

---

## 5. Целевая модель (Step (2), не реализовано)

Чтобы коллизии работали и под NPC-автопилотом, и модель движения стала единой:

- Увести NPC с прямого `Rigidbody`-контроля на **тот же силовой путь** через
  `ShipController.ApplyServerInput(...)` (замысел: «NPC = диспетчер маршрута, а не
  второй пилот», `M2_FSM_DIAGNOSIS.md §3.1`).
- Мигрировать в два приёма:
  1. **Трансляция — через `AddForce`** (даёт честные коллизии; с `mass=2000`
     translation стабильна);
  2. **Рыскание — мост через `MoveRotation`**, пока не отладим силовой yaw
     (`AddTorque` в `ForceMode.Acceleration` или масштабирование по `inertiaTensor` —
     исходная причина, по которой силовой yaw был слабым: `ShipController.cs:842-844`).
- После миграции: коллизии, зоны расхождения (`07_...`) и передача управления игроку
  работают в **одной** модели, без частных случаев.

---

## 6. Verification (Step (1))

- Compile clean.
- Play Mode: NPC летит по маршруту; игрок садится в NPC-корабль →
  в консоли `Player took control — NPC autopilot yielding`, корабль управляется как
  обычный игровой (силы, коллизии); игрок выходит →
  `Player released control — NPC autopilot resuming`, NPC продолжает маршрут.

---

## 7. Открытые вопросы

| # | Вопрос |
|---|--------|
| Q1 | Нужна ли передача NGO-ownership игроку при посадке, или достаточно server-authoritative `_pilots` + RPC-ввода (как сейчас)? |
| Q2 | При возврате управления — продолжать прежний leg маршрута или пересчитывать ближайшую станцию? Сейчас — прежний leg. |
| Q3 | Step (2): начинать миграцию на силы с трансляции или сразу делать полный силовой yaw? |
