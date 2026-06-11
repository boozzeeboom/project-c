# Resources Exchanger — История фиксов и багов

> **Дата создания:** 2026-06-11
> **Хронология** — все проблемы, найденные при реализации T-E01..T-E05

---

## 2026-06-11 — T-E05: Полный цикл Pack/Unpack

### Баги, найденные и исправленные

#### 1. Scene-placed `[ExchangeServer]` не спавнился

**Симптом:** Консоль не показывает `[ExchangeServer] OnNetworkSpawn`. Любой RPC молча отбрасывается.
**Причина:** `InScenePlacedSourceGlobalObjectIdHash == 0` → NGO не спавнит объект.
`ScenePlacedObjectSpawner` спавнит, но только если `IsServer == true` на момент `Start()`. А `StartHost` ещё не нажат.
**Фикс:** `ScenePlacedObjectSpawner` подписан на `NetworkManager.OnServerStarted` → `HandleServerStarted` → `SpawnInAllLoadedScenes()`.

#### 2. Stacking отсутствует — UI показывает 100 строк вместо 1

**Симптом:** Pack-список показывает 100 одинаковых записей «Антигравий (осколок)».
**Причина:** `InventoryData` хранит `List<int> ids` — каждый id = 1 единица.
`BuildSnapshot` → `quantity = 1` (MVP). Цикл в `RefreshExchangeData` 100 раз добавлял ту же запись.
**Фикс:** Группировка по `itemId` в `Dictionary<int, int>` до цикла.

#### 3. Inventory Full — Unpack фейлится

**Симптом:** `_world.Unpack returned: success=False message='Инвентарь полон: не удалось добавить 100 предметов'`.
**Причина:** `MAX_SLOTS = 32` жестко в `InventoryWorld`. 7 существующих + 100 новых = 107 > 32.
**Фикс:** `MAX_SLOTS = 1000`. Вынесен в конфигурируемое поле через `InventoryServer.maxSlots`.

#### 4. countToRemove semantics mismatch

**Симптом:** `Pack _world.Pack returned: success=False message='Количество должно быть кратно 100'`.
**Причина:** Клиент шлёт `countToRemove=1` (1 коробка), сервер ждёт `countToRemove=100` (100 единиц).
**Фикс:** `OnPackClicked` шлёт `countToRemove = item.inventoryQty` (100 для antigrav).
`OnUnpackClicked` шлёт `countToRemove = item.warehouseQty` (1 для antigrav).

#### 5. Склад не обновляется после Pack

**Симптом:** L1: `warehouse persisted` — L2: `PushSnapshot called` — **склад в UI не изменился**.
**Причина:** `InventoryServer.PushSnapshot` обновляет инвентарь, но не склад.
**Фикс:** `MarketServer.Instance.PushPlayerSnapshot(clientId)` — найдёт зону игрока и пришлёт свежий market snapshot.

#### 6. Пустой инвентарь при первом открытии обменника

**Симптом:** Правый список (склад) показывает ящики, левый (инвентарь) — пусто.
Пока не откроешь вкладку инвентаря (Character → клавиша I), список не заполняется.
**Причина:** `InventoryClientState` ещё не получил snapshot — `RequestRefresh` не вызывался.
**Фикс:** `MarketWindow.Show()` → `InventoryClientState.RequestRefresh()` + `RequestSubscribeMarket()` + принудительный `RefreshExchangeData()`.

#### 7. IsReady() молча отбрасывал RPC

**Симптом:** RPC доходит до сервера, но ничего не происходит (нет fail-сообщения).
**Причина:** `IsReady()` и `CheckRateLimit()` возвращали `false` без `SendResult`.
**Фикс:** `IsReadyOrResult()` / `CheckRateLimitOrResult()` шлют fail-результат клиенту с причиной.

#### 8. RPC exception не ловился

**Симптом:** При exception в RPC-методе клиент ждал бесконечно.
**Причина:** NGO на хосте проглатывает exception без `Debug.LogError`.
**Фикс:** Весь `RequestPackRpc` / `RequestUnpackRpc` обёрнут в `try-catch` с `SendResult`.

#### 9. TradeWorld race condition

**Симптом:** При старте хоста `[Exchange Server] TradeWorld не инициализирован!`.
**Причина:** `ExchangeServer.OnNetworkSpawn` вызывается до `MarketServer.OnNetworkSpawn`,
который создаёт `TradeWorld.Instance`.
**Фикс:** `StartCoroutine(InitWhenReady())` — ждёт `TradeWorld.Instance` до 10 секунд.

#### 10. ExchangeServer был child NetworkManager

**Симптом:** `[Netcode] NetworkManager cannot be a NetworkObject.`
**Причина:** `[ExchangeServer]` GameObject был дочерним к `NetworkManager`.
**Фикс:** Перенесён на root иерархии BootstrapScene.

---

## 2026-06-11 — T-E04: UI и интеграция

*(без критических багов — все проблемы T-E04 относятся к ранним сессиям)*

## 2026-06-11 — T-E01..T-E03: Core logic

*(без критических багов — все проблемы Core решены в T-E05)*
