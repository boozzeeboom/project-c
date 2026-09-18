# T-SHIP-FIX03 — серверный прайс в ShipModuleServer (client-цены не доверяем)

> Тикет: `T-SHIP-FIX03` (P0, фаза B). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Тесты — за пользователем (см. Manual внизу).

## Перепроверка (до фикса, чтением кода — ПОДТВЕРЖДЕНО)

Три RPC доверяли клиентским цифрам (`ShipModuleServer.cs`):
- `RequestSellModuleRpc:227` — `sellCredits` от клиента → `TryModifyCredits(clientId, sellCredits)` (`:290`).
  Клиент считает `max(1, costCredits/2)` (`RepairManagerWindow.ComputeSellPrice:750-759`), но мод-клиент
  шлёт любое число (`int.MaxValue` = фарм).
- `RequestRepaintShipRpc:388` — `cost` от клиента → `TryModifyCredits(clientId, -cost)` (`:425`).
  `cost=0` = бесплатно; `cost<0` = **начисление** кредитов. Клиент берёт цену из
  `RepairManager._repaintCost` (дефолт 500, `:24`) через `Show()` → окно (`:358`).
- `RequestRepairHullRpc:464` — то же через `_hullRepairCost` (дефолт 300, `:515`).
- Попутно: установка модулей кредитов **не списывает вообще**
  (`RequestInstallModuleRpc` без цены; `OnInstallClicked:918-926` без `TryModifyCredits`) —
  цена в UI есть, списания нет. НЕ этот тикет (кандидат в отдельный баланс-тикет, поведение не меняем).

## Решение (минимальный дифф, сигнатуры RPC не тронуты — вызывающий код не меняем)

Сервер считает цены сам, клиентские цифры — только display-hint (mismatch → `Warning` в лог как чит-сигнал):
- **Sell:** `max(1, costCredits/2)` — формула побайтово равна клиентской, но источник —
  серверный `ShipModuleCatalog` (`FindModuleById(removedModuleId)`). Неизвестный модуль → 0 + warning.
- **Repaint/Hull:** новые `[SerializeField]` на `ShipModuleServer` —
  `_serverRepaintCost = 500`, `_serverHullRepairCost = 300` (дефолты = дефолтам `RepairManager`).
  Сервер использует `max(0, _server*)`; клиента игнорирует.
- **Чистка мёртвого кода (с проверкой):** `ModuleShopEntry` (`[Obsolete]`, T-MOD03) —
  grep: 0 использований вне собственного файла; `ShopEntry_*.asset` — 0 файлов в `Data/Modules/`;
  `ModuleShopDatabaseEditor` уже на `ShipModule`. Вердикт: **точно мёртв**, но файл НЕ удалён
  в этом тикете (нужен `.meta`-аккуратный снос) — удаление в `T-SHIP-FIX13` (батч-чистка).

## Изменения

- `Assets/_Project/Scripts/Ship/ShipModuleServer.cs`:
  - `+ _serverRepaintCost / _serverHullRepairCost` (SerializeField, Header «Серверные цены»);
  - `+ ComputeServerSellPrice(ShipModule)` (паритет с клиентом, серверный источник);
  - sell/repaint/hull RPC — серверные цены + mismatch-warning; `cost<=0` от клиента больше не влияет.
- `docs/Ships/ITERATIONS.md` — секция тикета.

## Проверка

- Compile: Console → 0 errors (проверено через MCP после правки).
- Tests: Test Runner — за пользователем (юнит-тестов на сервер нет — отдельная задача `.asmdef`).
- Manual (за пользователем):
  1. Док → E → продать модуль: начислено = `max(1, costCredits/2)` из SO, не цифра клиента.
  2. Мод-клиент/подмена `sellCredits=int.MaxValue` → начислен серверный прайс + Warning в консоли сервера.
  3. Подмена `cost=0/-100` (repaint/hull) → списана серверная цена (500/300 по дефолту), баланс не растёт.
  4. Неизвестный модуль в слоте (если возможно) → продажа даёт 0 + Warning, без NRE.
  5. Обычные флоу (install за 0 — как было; sell/repaint/hull по честным ценам) — без регрессий.
