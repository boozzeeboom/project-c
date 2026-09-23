# T-CARGO-UI-03: Приватность деталей трюма (targeted snapshot по ключу)

> **Дата:** 2026-09-23
> **Статус:** ✅ Phase 1 реализован (этот коммит), Phase 2 (удаление NV) — отдельный тикет
> **Основание:** T-KEY-11 закрыл открытие `ShipCargoConsole` без ключа, но содержимое чужого трюма
> продолжает рассылаться всем клиентам через broadcast-телеметрию.
> **Приоритет:** P1 (не блокер: мутации уже закрыты `IsOwnerOfShip`, честные клиенты ничего не видят).

---

## 1. Проблема

Детали груза (`ShipCargoDetailState.cargoDetail`, до 32 позиций с `itemId/displayName/quantity`)
публикуются сервером в `NetworkVariable` с `ReadPermission.Everyone`
(`ShipController._telemetryCargoState`, `Scripts/Player/ShipController.cs:971-974`)
и сидируются в клиентский кэш при подписке
(`ShipTelemetryClientState.SubscribeToShip`, `Scripts/Ship/Client/ShipTelemetryClientState.cs:106`).

Итог: **любой клиент держит в памяти содержимое трюмов всех кораблей** — достаточно
мода/инспектора памяти, F-гейт T-KEY-11 тут не помогает. Это read-утечка, не write:
мутации (`RequestStoreToCargoRpc` / `RequestRetrieveFromCargoRpc`) уже проверяют
`KeyRodInstanceWorld.IsOwnerOfShip` (`Trade/Exchange/Network/ShipCargoServer.cs:115,250`).

Ограничение движка: у NGO `NetworkVariable` read-права только «всем или никому» —
поштучную приватность broadcast-переменной сделать нельзя в принципе.

## 2. Решение: детали — по запросу, только владельцу (TargetRpc)

Быстрый снапшот (`ShipTelemetryState`: `cargoUsed/cargoMax`, позиция, топливо, `ownerClientId`)
остаётся broadcast — это coarse-данные для HUD/баров, утечка принята by design.
Детали (`cargoDetail`) уезжают на приватный канал:

```
Окно открыто (T-KEY-11 allowed)
  → ShipCargoClientState.RequestDetail(shipNetId)
  → ShipCargoServer.RequestCargoDetailRpc (SendTo.Server)
  → IsOwnerOfShip? ── нет ──→ ReceiveShipCargoDetailTargetRpc(success=false) → Hide + тост
       └── да ──→ BuildCargoDetailSnapshot() → ReceiveShipCargoDetailTargetRpc(success=true, detail)
                  → ShipCargoClientState.OnCargoDetailReceived → окно рисует список
```

Обновление после мутаций: `ShipCargoConsoleWindow.HandleResult` (уже вызывается после
store/retrieve) повторно запрашивает детали вместо ожидания broadcast-события.

### 2.1 Изменения (Phase 1, этот тикет)

| Файл | Изменение |
|---|---|
| `Scripts/Player/ShipController.cs` | + `public ShipCargoDetailState BuildCargoDetailSnapshot()` (server-only): тонкая обёртка над существующими `BuildCargoDetailDto()` + `NetworkObjectId`. Приватный билдер не меняется. |
| `Trade/Exchange/Network/ShipCargoServer.cs` | + `RequestCargoDetailRpc(shipNetId)` (`SendTo.Server`, `Everyone` — сервер сам проверяет `IsOwnerOfShip`, как store/retrieve). Allow → снапшот через `BuildCargoDetailSnapshot()` корабля из `SpawnManager`; deny → `success=false`. |
| `Scripts/Player/NetworkPlayer.cs` | + `ReceiveShipCargoDetailTargetRpc(shipNetId, detail, success, reason)` (`SendTo.Owner`): форвард в `ShipCargoClientState.OnCargoDetailReceived` (паттерн `ReceiveShipCargoResultTargetRpc`). |
| `Trade/Scripts/Client/ShipCargoClientState.cs` | + кэш деталей по кораблям (заполняется только приватными ответами) + событие `OnCargoDetailReceived` + `RequestDetail(shipNetId)`. |
| `Trade/Scripts/Client/ShipCargoConsoleWindow.cs` | `RefreshCargo` читает новый кэш вместо `ShipTelemetryClientState.GetShipCargoDetail`; `Show()` и `HandleResult` запрашивают детали; deny → `Hide()` + тост. |
| `Scripts/Ship/Client/ShipTelemetryClientState.cs` | `SubscribeToShip` больше НЕ сидирует `_cargoByShip` из broadcast (`ship.TelemetryCargoState`); метод `GetShipCargoDetail` — `[Obsolete]` на время миграции. |

### 2.2 НЕ входит (Phase 2, отдельный тикет)

- Удаление самого `_telemetryCargoState` NetworkVariable из `ShipController` (код-онли, сцены не тронуты, но дифф шире — делаем после стабилизации Phase 1).
- Фильтрация coarse-счётчиков `cargoUsed/cargoMax` в быстром снапшоте (нужны барам/спискам, оставляем broadcast осознанно).

## 3. Почему не альтернативы

| Вариант | Отказ |
|---|---|
| Оставить broadcast, закрыть только UI (B) | Не чинит утечку для модов — честный клиент и так не видит (T-KEY-11). |
| Отдельный NV на игрока | NGO не умеет per-client NV; N переменных на N игроков — не масштабируется. |
| Шифрование деталей в NV | Ключ всё равно нужен всем читателям; театр безопасности + нагрузка. |

## 4. Безопасность (threat model)

- Честный клиент без ключа: окно не открывается (T-KEY-11) + деталей в памяти нет (этот тикет). ✅
- Мод-клиент: детали чужих трюмов недоступны (только свои, сервер проверяет владение). Мутации уже закрыты. ✅
- Остаточный риск (принят): coarse-счётчики (`cargoUsed/cargoMax`, позиция, владелец) видны всем — нужно HUD и спискам кораблей. Перечисление «у кого полный трюм» возможно, состав — нет.

## 5. Совместимость и ограничения проекта (AGENTS.md)

- Additive-only: новые RPC/методы, существующие сигнатуры не меняются; `GetShipCargoDetail` — через `[Obsolete]`, не удалением.
- Floating Origin: в payload нет мировых координат (`shipNetId` + items) — хуки сдвига не нужны. 🟢
- Persistence: не затрагивается (детали всегда строятся из live-`TradeWorld`).
- Сцены/префабы/`.meta`/`.asmdef`: не трогаем. `NetworkManager`: не трогаем.
- NGO-паттерны: `SendTo.Server` + `IsOwnerOfShip` (как store/retrieve), `SendTo.Owner` TargetRpc (как результаты).

## 6. Оценка

| Работа | Оценка |
|---|---|
| `BuildCargoDetailSnapshot` + `RequestCargoDetailRpc` + deny-путь | 1–1.5 ч |
| `ReceiveShipCargoDetailTargetRpc` + `ShipCargoClientState` (кэш/событие/запрос) | 1 ч |
| `ShipCargoConsoleWindow` (миграция чтения, запросы, deny→Hide) + deprecate seed | 1–1.5 ч |
| Compile + ручная проверка (§7) | 0.5–1 ч |
| **Итого** | **~3–5 ч** |

## 7. Приёмка (ручная, пользователь)

1. Host + клиент. Клиент без ключа: F на консоли → тост (T-KEY-11, без изменений).
2. Владелец открывает консоль → список груза появился после короткой задержки (статус «загрузка…», не пустое окно).
3. Store/retrieve → список обновился (повторный запрос из `HandleResult`).
4. Клиент без ключа: в памяти/агрегаторе нет `cargoDetail` чужих кораблей (проверка через дебаг-дамп `ShipCargoClientState`, либо отсутствие обновлений `OnShipStateChanged` по чужим мутациям трюма).
5. Быстрые счётчики (`cargoUsed/cargoMax`) и «Мои корабли» работают как раньше.
6. Console → 0 errors.

## 8. Связанные документы

- `CARGO_UI_01_DESIGN_2026-07-02.md` (T-CARGO-UI-01: `cargoDetail` в телеметрии — что переносим).
- `CARGO_UI_02_PLAN.md` (T-CARGO-UI-02: окно и сервер, куда встраиваемся).
- `CARGO_OWNERSHIP_DESIGN.md` (владение трюмом — серверные гарды, уже есть).
- `../Key-subsystem/99_CHANGELOG.md` — T-KEY-10 (fail-closed реестра), T-KEY-11 (гейт открытия консоли).
- Код: `ShipController.cs:966-991,1166-1225` (писатель), `ShipTelemetryClientState.cs:104-109` (seed), `ShipCargoServer.cs:86-115,221-250` (образец гарда), `ShipCargoConsoleWindow.cs:444-527` (читатель).
