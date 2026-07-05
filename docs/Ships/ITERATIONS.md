# Project C — Iterations Log

## Итерация от 2026-07-21

**Задача:** P1 Refactor Key Subsystem — удаление 7 obsolete/дублирующих файлов, приведение к single source of truth (KeyRodInstanceWorld)

**Ветка:** `refactor/key-subsystem-p1-2026-07-21` → merged to main

**Коммиты:**
- `9b7cf18` — docs: P1 analysis (5 проблем, 4-шаговый план)
- `d04c5e8` — refactor: удалить Obsolete legacy (ShipKeyBinding/Server/ClientState/Toast)
- `f97bdcf` — refactor: fix registeredShipId=0 при fallback CreateInstance
- `37f25a2` — refactor: удалить ShipOwnershipRegistry, ownerClientId из telemetry
- `6742a84` — refactor: удалить KeyRodInstanceBinding, ShipController создаёт instance
- `01a4d13` — fix: guard от дубликата ключа, корутина CreateKeyInstanceWhenReady
- `af0fd55` — docs: обновлены 00_OVERVIEW, 99_CHANGELOG, SHIP_REFACTOR_PLAN

**Изменения:**
- Удалено 7 файлов: ShipKeyBinding.cs, ShipKeyServer.cs, ShipKeyClientState.cs, ShipKeyToast.cs, ShipOwnershipRegistry.cs, KeyRodInstanceBinding.cs (+ .meta)
- Изменено: NetworkManagerController.cs, NetworkPlayer.cs, ShipController.cs, PickupItem.cs, InventoryWorld.cs, ShipTelemetryClientState.cs
- Создано: 31_KEY_ANALYSIS_2026-07-21.md, SHIP_REFACTOR_PLAN_2026-07-21.md
- Обновлено: 00_OVERVIEW.md, 99_CHANGELOG.md

**Итог:** -1139 строк, +651 строк (net -488). 0 reflection. 1 source of truth.
