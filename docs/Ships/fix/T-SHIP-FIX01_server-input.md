# T-SHIP-FIX01 — ввод мезии/ролла/дозаправки через RPC владельца

> Тикет: `T-SHIP-FIX01` (P0, фаза B). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Тесты — за пользователем (кейсы также в `docs/dev/global_needtotest/SHIP_TESTS.md`).

## Перепроверка (до фикса, чтением кода — ПОДТВЕРЖДЕНО)

Серверный `FixedUpdate` читал клавиатуру хоста (`ShipController.IsKeyDown`, дозаправка L,
мезия C/V/Z/X/Shift+A/D/Shift+W/S, ролл Z/C): нажатия клиентов игнорировались, нажатия
на хосте двигали чужие корабли. Попутно установлено:
- Meziy press-events `ShipInputReader` (`OnMeziyPitchUp…`) — **0 подписчиков** во всём проекте.
  НЕ удалены (мёртвый код зафиксирован, снос в `T-SHIP-FIX13` после отдельной проверки).
- `GetCurrentPitchInput` / `GetCurrentYawInput` — 0 вызовов (удалены в этом тикете).
- Основной ввод уже шёл правильно: `NetworkPlayer.Update` (`InputBindingsConfig`-actions) →
  `SendShipInput` → `SubmitShipInputRpc` каждый кадр; хост — тоже клиент и шлёт так же.
- В `InputBindingsConfig` НЕТ actions для Roll/Meziy/Refuel — упаковка в `NetworkPlayer`
  читает те же 9 клавиш напрямую (паритет 1:1). Перенос на actions/ребиндинг — отдельный тикет.
- Перекрытия клавиш сохранены как были (дизайн, не меняем): `C` = roll+1 И meziy-pitch −1;
  `Z` = roll−1 И meziy-roll −1; `Shift+A/D/W/S` = boost+meziy. Кандидат в баланс-тикет.
- NPC-путь `ApplyServerInput`: новые каналы — опциональные параметры (=0/false), старые вызовы не тронуты.

## Решение (`ShipController.cs` + `NetworkPlayer.cs`, сигнатуры расширены, клиенты кроме пилота не тронуты)

- `NetworkPlayer.Update` (блок `_inShip`): упаковка `roll/meziyPitch/meziyRoll/meziyYaw/meziyThrust/refuel`
  → расширенный `SendShipInput` (единственный вызывающий, проверено grep).
- `SubmitShipInputRpc` + `ApplyServerInput`: 6 новых каналов, суммы/средние (`_sumRoll/_sumMeziy*/_refuelCount`,
  те же reset-сайты ×4, isIdle/engineStalled-обнуления); **все 10 float clamp `[-1,1]`**
  (заход на территорию FIX02 — зафиксировано здесь, в FIX02 останется AddPilot-check).
- Сервер: refuel-блок и meziy-блок — на средних вместо `IsKeyDown` (press+release объединены,
  расщеплённость устранена); ролл — `avgRoll` (2 сайта).
- Удалено: `IsKeyDown`, `KeyCodeToKey`, `GetCurrentRollInput/PitchInput/YawInput`
  (все использования — только в заменённых блоках, проверено поиском по диску;
  `:2033` double-dt НЕ тронут — это FIX05).

## Изменения

- `Assets/_Project/Scripts/Player/ShipController.cs` (~−140/+100): новые каналы, сервер на средних, чистка опроса.
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (+31/−1): упаковка интентов пилота.
- `docs/Ships/ITERATIONS.md` — секция тикета.

## Проверка

- Compile: Console → 0 errors/warnings (MCP; см. секцию ITERATIONS).
- Tests: Test Runner — за пользователем.
- Manual (за пользователем, также в реестре): пилот жмёт C/V/Z/X/Shift+A/D/Shift+W/S/L —
  мезия/ролл/дозаправка работают; второй клиент жмёт те же клавиши — чужой корабль стоит;
  хост жмёт — чужие стоят (было: двигались); IDLE без пилотов на L не заправляется;
  NPC-курсирование без регрессий; кооп: два пилота — усреднение.
