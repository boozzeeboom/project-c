# T-SHIP-FIX08 — ClientRpc модулей через Manager + TargetRpc-уведомления

> Тикет: `T-SHIP-FIX08` (P1, фаза C). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Тесты — за пользователем (кейсы также в `docs/dev/global_needtotest/SHIP_TESTS.md`).

## Перепроверка (до фикса, чтением кода — ПОДТВЕРЖДЕНО + настоящее расхождение)

- `OnModuleChangedClientRpc:325-361` применял изменения через `slot.InstallModule/RemoveModule`
  напрямую, минуя `ShipModuleManager`. Реальное расхождение (не теоретическое):
  `ModuleSlot` НЕ пересчитывает `ShipModuleManager.currentPowerUsage` → после каждого
  install/remove энергия на клиентах stale (сервер через Manager пересчитывает).
  Плюс двойная работа на хосте (сервер применил → Everyone ClientRpc применяет снова +
  двойной `OnModuleChanged`).
- `NotifyErrorClientRpc:380` / `NotifySuccessClientRpc:391` — `SendTo.Everyone` + фильтр
  `LocalClientId` вместо TargetRpc (трафик + лик всем); заголовок секции врёт
  («Notifications (TargetRpc)»), `TODO:350`; `NotifySuccess` — пустое тело.
- Единственный подписчик `OnModuleChanged` — `ShipModuleVisualApplier:45` (клиентский визуал);
  серверная логика на событие не завязана (проверено grep) → пропуск серверного исполнения безопасен
  и бонусом останавливает спавн module-визуалов на сервере.
- Паттерн TargetRpc в проекте устоявшийся: `[Rpc(SendTo.SpecifiedInParams)]` +
  `RpcTarget.Single(clientId, RpcTargetUse.Temp)` (`CombatServer:270-303`).

## Решение (только `ShipModuleServer.cs`)

- `OnModuleChangedClientRpc`: `if (IsServer) return` (сервер уже применил авторитетно);
  install — `_moduleManager.ReplaceModule` (атомарно + rollback + пересчёт энергии),
  remove — `_moduleManager.RemoveModule`; модуль вне клиентского каталога / отказ Manager →
  Warning (сигнал рассинхрона, состояние не портим). Поиск слота и событие — без изменений.
- `NotifyError/SuccessClientRpc` → `[Rpc(SendTo.SpecifiedInParams)]` + `RpcTarget.Single(...Temp)`;
  фильтр `LocalClientId` убран (адресация точная); `NotifySuccess` — `Debug.Log` вместо пустоты;
  `TODO:370` закрыт. Сигнатуры wrapper'ов не тронуты (вызывающие не меняем).
- Что НЕ делаем: rate-limit/dist-NPC для модулей (отдельный тикет), `FindSlot/ValidateOwnership`
  хелперы (косметика, FIX13).

## Изменения

- `Assets/_Project/Scripts/Ship/ShipModuleServer.cs` (ClientRpc-блок + Notifications-блок).
- `docs/Ships/ITERATIONS.md` — секция тикета.

## Проверка

- Compile: Console → 0 errors/warnings (MCP; см. секцию ITERATIONS).
- Tests: Test Runner — за пользователем.
- Manual (за пользователем, также в реестре): install/remove модуля в доке → у всех клиентов
  слот + энергия (`currentPowerUsage`) сходятся с сервером; граничный модуль (не хватает энергии) —
  везде одинаковый отказ; ошибки (чужой ключ, не в доке) видит только запросивший (второй клиент —
  тишина в консоли); repaint/hull/sell — флоу без регрессий; хост — без двойных `OnModuleChanged`.
