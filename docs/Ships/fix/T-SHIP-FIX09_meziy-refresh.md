# T-SHIP-FIX09 — мезия-активатор переживает рантайм-смену модулей

> Тикет: `T-SHIP-FIX09` (P1, фаза C). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Тесты — за пользователем (кейсы также в `docs/dev/global_needtotest/SHIP_TESTS.md`).

## Перепроверка (до фикса, чтением кода — ПОДТВЕРЖДЕНО)

- `MeziyModuleActivator.Initialize()` строит `meziyStates` один раз (старт); подписки на смену
  модулей нет. Вновь установленный мезий → `TryActivate` warn «not found» → мёртв до рестарта;
  снятый → запись остаётся (фантом). Install/remove идут через `ShipModuleServer`
  (сервер-RPC + `OnModuleChangedClientRpc`), активатор не уведомляется нигде (проверено grep).
- Ключ словаря — `moduleId` (`:84,:112`): два одинаковых модуля в разных слотах — второй
  игнорируется. Ключ НЕ меняем (публичный API весь на moduleId: Try/Deactivate/IsInstalled/
  GetState/IsOverheated + вызовы ShipController) — зафиксировано как ограничение, кейс в тесты.
- Топливо: `TryActivate:156` — гейт `CurrentFuel < meziyFuelCost` (разовый порог 4–5 по ассетам),
  расход — `meziyFuelCost*2*dt`/сек (`:277`, т.е. 8–10/сек). Порог грубый, но безвредный
  (не краш/эксплойт) — поведение НЕ меняем, единицы зафиксированы комментом; доводка — баланс-тикет.
- Шапка файла (`:45-48`) врёт про клавиши (W/S для pitch — реально C/V после FIX01) — чиню коммент.
- `GetActiveStates` отдаёт живой словарь — итерация только в `ApplyMeziyEffects` (FixedUpdate),
  `Refresh` дёргается из RPC-контекста (main thread, вне итерации) — безопасно, зафиксировано.
- Проводка цела: `Tick`, `ConsumeFuelForActiveModules`, `GetPassiveModifier`, `Initialize`
  вызываются из ShipController (`:1369,:1804-1807,:2095,:2192`).

## Решение (минимальный дифф, 3 файла)

- `MeziyModuleActivator.RefreshInstalledModules()` (новый public): сверить словарь со слотами —
  добавить недостающие (с heat-полями по нулям), обновить `state.module`-ссылки, удалить снятые
  (включая залипший active). Идемпотентен, вызывать можно после любой смены.
- `ShipController.RefreshMeziyModules()` — 1-строчный форвардер с нулл-гардом (паттерн `ClearHullBroken`).
- `ShipModuleServer`: вызовы форвардера после успешных install/remove/sell (сервер, 3 места) и
  в `OnModuleChangedClientRpc` после Replace/Remove (клиент, 2 ветки). Repaint/hull — без смены
  модулей, не трогаем.

## Изменения

- `MeziyModuleActivator.cs`: `RefreshInstalledModules()` + правка шапки клавиш + коммент единиц гейта.
- `ShipController.cs`: форвардер.
- `ShipModuleServer.cs`: 5 вызовов.
- `docs/Ships/ITERATIONS.md` — секция тикета.

## Проверка

- Compile: Console → 0 errors/warnings (MCP; см. секцию ITERATIONS).
- Tests: Test Runner — за пользователем.
- Manual (за пользователем, также в реестре): поставить мезий в доке → работает без рестарта
  (пассив + актив + перегрев); снять → погас, без фантомного torque; переустановить другой мезий —
  старый ушёл, новый жив; два одинаковых модуля — второй игнорируется (зафиксированное ограничение);
  heat/кулдаун переживают пересборку списка (не сбрасываются без причины).
