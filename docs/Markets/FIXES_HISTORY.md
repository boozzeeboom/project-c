# Markets — Fixes History
Хронология багов, диагнозов и фиксов рыночной подсистемы. Текущая версия (2026-06-05) — стабильная: полный цикл BUY/LOAD/UNLOAD/SELL работает + per-ship cargo cache (мгновенное переключение между кораблями без потери данных) + контракты как 3-й таб `MarketWindow` (accept/complete/fail, награды, активные, таймеры, визуальное отличие Active vs Pending).

## 2026-06-05 — FIX: потеря cargo при переключении между кораблями на рынке

### Симптом (из юзерского репорта)

> «Загружаю товар на ship_light. Переключаюсь на ship_medium, загружаю туда. Переключаюсь обратно на ship_light — cargo пустой. Потеряно.»

**Цепочка:** BUY 1 mesium → SELECT ship_light → LOAD 1 (TradeResult обновляет `_cargoCache`, в snapshot есть `ship_light.cargo=1`) → SELECT ship_medium в dropdown (snapshot НЕ приходит, `_cargoCache` остаётся старым) → LOAD 1 на ship_medium (TradeResult обновляет `_cargoCache` до `ship_medium.cargo=1`, теряя ship_light) → SELECT ship_light обратно (snapshot НЕ приходит, `_cargoCache` отражает ship_medium) → UI показывает чужой cargo либо пустой.

### Что было до фикса

`MarketClientState` хранил **только** `WarehouseEntryDto[] _cargoCache` для **текущего выбранного корабля**. Обновления приходили только:
- в `OnSnapshotReceived` — раз в ~5 мин (тик)
- в `HandleTradeResult` — для **одного** (затронутого) корабля

При переключении dropdown'а **никакого нового snapshot не приходило** → `_cargoCache` оставался вчерашним. Если TradeResult после переключения обновлял его на cargo другого корабля — клиент считал это cargo нового выбора.

`MarketServer.SendSnapshotToClient` рассылал `cargo = cargoOf(selectedShip)` — **только одного** корабля. Остальные `nearbyShips` шли без cargo.

### Корневая причина

Серверная модель (`TradeWorld._cargoCache[shipId]`, персистенция в `PlayerPrefs` под ключом `cargo:{shipNetworkObjectId}`) была **уже корректна** — cargo каждого корабля живёт независимо. Баг был только в **клиентской проекции**: один общий `_cargoCache` не отражал мульти-корабельную реальность.

Это **регрессия от FIX 2026-06-04** (см. ниже § "Что не делали" — там прямо сказано, что cargo per-ship на клиенте не реализовано).

### Фикс (4 файла, additive — не сломал ничего)

**1. `Assets/_Project/Trade/Scripts/Dto/MarketSnapshotDto.cs`** — добавлен новый тип `ShipCargoDto` + поле `shipCargos[]`. **Старое поле `cargo` оставлено** (backward compat для существующих клиентов и для UI fallback'а).

```csharp
[Serializable]
public struct ShipCargoDto
{
    public ulong shipNetworkObjectId;
    public WarehouseEntryDto[] cargo;
}

public struct MarketSnapshotDto
{
    // ... существующие поля ...
    public WarehouseEntryDto[] cargo;          // legacy: cargo выбранного корабля
    public ShipCargoDto[] shipCargos;          // NEW: cargo ВСЕХ nearby ships
    // ...
}
```

**2. `Assets/_Project/Trade/Scripts/Network/MarketServer.cs:SendSnapshotToClient`** — в дополнение к старому `cargo = cargoOf(selectedShip)` собираем `shipCargos[]` для всех nearby ships:

```csharp
// FIX (2026-06-05): cargo ВСЕХ кораблей в зоне
var shipCargosList = new List<ShipCargoDto>(ships.Count);
for (int i = 0; i < ships.Count; i++)
{
    var shipDto = ships[i];
    var shipCls = ResolveShipClass(shipDto.shipNetworkObjectId);
    var shipCargo = TradeWorld.Instance.GetOrLoadCargo(shipDto.shipNetworkObjectId, shipCls);
    shipCargosList.Add(new ShipCargoDto {
        shipNetworkObjectId = shipDto.shipNetworkObjectId,
        cargo = BuildCargoDtos(shipCargo != null ? shipCargo.SaveToList() : new List<WarehouseEntry>())
    });
}
```

**3. `Assets/_Project/Trade/Scripts/Network/MarketServer.cs:SetSelectedShipRpc`** — после обновления `_clientSelectedShip` сразу вызывает `SendSnapshotToClient(clientId, zone)` (safety net для случая, когда игрок впервые подошёл к рынку и выбрал корабль — следующий тик может быть через 5 минут).

**4. `Assets/_Project/Trade/Scripts/Client/MarketClientState.cs`** — добавлен per-ship кэш:

```csharp
public IReadOnlyDictionary<ulong, WarehouseEntryDto[]> CurrentShipCargos { get; private set; }
public void UpdateShipCargo(ulong shipId, WarehouseEntryDto[] cargo) { /* merge */ }
```

`OnSnapshotReceived` заполняет `CurrentShipCargos` из нового `shipCargos[]`. `UpdateShipCargo` дёргается из `MarketWindow.HandleTradeResult` при Load/Unload.

**5. `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs`** — три точки:

- **Ship-selector callback** (dropdown изменён): `ApplySelectedShipCargoFromCache(newShipId)` — мгновенное переключение cargo из локального кэша, без roundtrip.
- **HandleSnapshot** — приоритет у `CurrentShipCargos[GetSelectedShipId()]`, fallback на `snap.cargo` (если вдруг новый кэш не пришёл, например snapshot со старого сервера).
- **HandleTradeResult** — при Load/Unload вызывает `_state.UpdateShipCargo(result.shipNetworkObjectId, _cargoCache)`, чтобы кэш для конкретного корабля обновился до прихода следующего snapshot.

### Поток данных после фикса

```
[Server] TradeWorld._cargoCache[shipId] (single source of truth, persistent)
        ↓ SendSnapshotToClient (каждые ~5 мин тик)
[Client] MarketClientState.CurrentShipCargos[shipId] (projection cache)
        ↓ ship-selector onChange
[UI]     MarketWindow._cargoList (instant from cache, no roundtrip)
        ↓ LOAD/UNLOAD
[Server] TradeResult с обновлённым cargo затронутого корабля
        ↓ HandleTradeResult
[Client] UpdateShipCargo(shipId, _cargoCache) — точечный апдейт до прихода snapshot
```

### Что не делали (по AGENTS.md — минимальный фикс)

- ❌ **Не удаляли** legacy-поле `cargo` из `MarketSnapshotDto` — additive change, старые клиенты ещё могут его читать. Полный переход на `shipCargos` — отдельный тикет.
- ❌ **Не вводили** ownership/access-control (чужой игрок видит чужой cargo) — `TradeWorld` отдаёт cargo по `shipNetworkObjectId` независимо от владельца. Это by design для текущего прототипа, но для MMO-сценария нужна авторизация (см. **Архитектурный план** ниже).
- ❌ **Не рефакторили** `MarketZone.BuildNearbyShipsDtos()` — DTO-контракт не изменился.
- ❌ **Не трогали** `CargoSystem` / `Warehouse` / `IPlayerDataRepository` — серверный слой уже корректен (cargo keyed by shipNetworkObjectId).
- ❌ **Не убирали** diagnostic-логи.

### Архитектурный план для будущего расширения (multi-ship inventory как first-class)

Текущий фикс закрывает **минимум**: cargo per-ship на клиенте, без потерь при переключении. Следующие шаги (P1..P3) превращают это в полноценную MMO-фичу «корабль = инвентарь, видимый другим игрокам»:

#### P1 — Ownership model (кто может грузить/выгружать)

Сейчас: `TradeWorld.SetCargo(shipId, ...)` принимает любой `(clientId, shipId)`. Нет проверки, что `shipId` принадлежит `clientId`.

Нужно:
- Ввести `ShipOwnership` маппинг: `Dictionary<ulong /*shipId*/, ulong /*ownerClientId*/>` в `ShipRegistry` или новом `ShipOwnershipService`.
- При `Load` / `Unload` проверять `shipOwnership[shipId] == clientId` (либо allow crew/guild — это уже GDD).
- При `Buy` / `Sell` — без изменений (это `warehouse[clientId][locationId]`, не зависит от корабля).
- Server-Rpc: `SetSelectedShip` — тоже валидировать ownership, иначе чужой игрок может подсмотреть cargo через snapshot (см. P2).

#### P2 — Privacy: cargo чужих кораблей в snapshot

Сейчас: `SendSnapshotToClient` отдаёт `shipCargos[]` **всех** nearby ships всем клиентам в зоне. Это утечка: другой игрок видит твой cargo.

Нужно:
- На сервере фильтровать `shipCargos` по `shipOwnership[shipId] == clientId` ИЛИ public-flag (товарный корабль, таможня).
- Альтернатива: `shipCargos` отдавать **только владельцу** + выдавать `ShipSummaryDto` остальным с `cargo: []` (но с `cargoSlots`, `cargoUsed` — обезличено).
- Сейчас: пусть будет утечка (текущий прототип — single-player сценарий на dedicated server). **Документируем как known issue** [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

#### P3 — Persistence beyond PlayerPrefs

Сейчас: `PlayerPrefsRepository` хранит `cargo:{shipId}` в `PlayerPrefs` — это **локально на клиенте**. На dedicated server это не сработает (PlayerPrefs на сервере != клиент).

Нужно:
- Ввести `IServerCargoRepository` (SQLite, JSON-файл под `ServerData/`, или просто `Application.persistentDataPath/shipCargo.json`).
- `TradeWorld` при `IsServer == true` использует server-репозиторий, при `IsClient == true` — PlayerPrefs (или оба: клиент кэширует, сервер истина).
- Сейчас: тестируем только host (server+client в одном процессе) — PlayerPrefs работает. Для dedicated server — отдельный тикет (см. [KNOWN_ISSUES.md](KNOWN_ISSUES.md)).

#### P4 — UI: cargo left by previous owner (rescue/recovery)

Из GDD: «Если предыдущий владелец покинул корабль, новый владелец видит оставшийся cargo (для лута/спасения)».

Нужно:
- При смене ownership (`ShipOwnershipService.Transfer(shipId, newOwner)`) cargo **не очищается** — новый владелец наследует.
- В UI пометить cargo как «inherited» (tooltip с датой, прошлым владельцем).
- Сейчас: корабли не передаются между игроками, фича отложена.

### Подтверждение фикса

- **Compile:** `validate_script` × 4 файла + `read_console` → 0 errors (1 pre-existing unrelated warning "string concat in Update" в MarketWindow — косметика, не блокер).
- **Smoke test (live host, `unityMCP_execute_code`):**  
  - snapshot = "primium"  
  - `MarketClientState._cargoCache` items=1, itemsSource ships=3, **shipCargos=3, CurrentShipCargos cacheKeys=3** — все три корабля имеют cargo в кэше, переключение мгновенное. ✓
- **End-to-end (юзер гоняет руками в Play mode):** `BUY → LOAD ship_light → SELECT ship_medium → LOAD → SELECT ship_light → cargo сохранён, LOAD инвентарь не теряется`. Тест принят юзером.

**См. также:**
- [ARCHITECTURE.md § "Поток данных"](../Markets/ARCHITECTURE.md) — обновлён per-ship cache
- [FLOW_TRADE.md § "Переключение корабля"](../Markets/FLOW_TRADE.md) — обновлён мгновенный UI flow
- [FILES_INDEX.md § "Per-ship cargo"](../Markets/FILES_INDEX.md) — новые поля/методы
- [KNOWN_ISSUES.md § "Cargo privacy / ownership"](../Markets/KNOWN_ISSUES.md) — P1..P4 из плана

---

## 2026-06-04 — FIX: «продажа вслепую» — на вкладке РЫНОК не видно сколько у игрока на складе

**Файл:** `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs:315-346`

**Симптом (из репорта пользователя):**
> "сейчас продажа осуществляется "вслепую". на странице рынка можно товар купить и не понятно сколько на складе такого товара и сколько можно продать. когда переходим на вкладку склада - все понятно все хорошо, но чтобы продать товар нужно вернуться на вкладку рынка и вслепую продавать."

**Что сделано:**
- В `BindMarketRow` (`MarketWindow.cs:315-331`) к строке списка товаров добавлен сегмент `(у вас: {whQty})` после существующего `(сток: {availableStock})`. Источник — `snap.Value.warehouse` (уже приходит в `MarketSnapshotDto`).
- Добавлен helper `FindWarehouseQty(WarehouseEntryDto[] warehouse, string itemId)` (`MarketWindow.cs:333-346`) — линейный поиск по плоскому массиву (≤ `warehouseMaxTypes` типов, в игре единицы, не сотни). Возвращает 0 если товара нет на складе.
- Вкладку «СКЛАД / ТРЮМ» и логику LOAD/UNLOAD/Buy/Sell **не трогали** — по запросу пользователя она «хорошая».

**До/после (пример):**
```
Было:  Мезиум  —  10 CR  (сток: 47)
Стало: Мезиум  —  10 CR  (сток: 47)  (у вас: 12)
```

**Что не делали (по AGENTS.md, минимальный фикс):**
- ❌ Не показывали `(в трюме: Y)` — у вкладки «СКЛАД / ТРЮМ» и так ясно сколько в cargo; лишняя колонка усложнила бы строку.
- ❌ Не трогали сервер (`MarketServer`, `MarketSnapshotDto`) — поле `warehouse` уже шлётся.
- ❌ Не меняли формат строки склада/груза (`BindWarehouseRow`/`BindCargoRow`) — там всё ОК.
- ❌ Не выключали SELL-кнопку при `whQty == 0` — задача чисто информационная; оставляем серверу право вернуть `NotEnoughInWarehouse`.

**Что проверить вручную (Play Mode, host):**
1. Чистый `PlayerPrefs.DeleteAll()` → открыть рынок → список товаров на вкладке «РЫНОК» показывает `(у вас: 0)` для всех.
2. BUY `mesium` x3 → `(у вас: 3)`. SELL `mesium` x1 → `(у вас: 2)`. SELL `mesium` x10 (больше чем есть) → красное сообщение, `(у вас: 2)` остаётся.
3. LOAD с вкладки «СКЛАД / ТРЮМ» → на вкладке «РЫНОК» `(у вас: 2)` (уменьшилось), `(сток: ...)` не меняется.
4. Регрессий быть не должно — `BindMarketRow` ничего больше не трогает.

---

## 2026-06-04 — FIX: «LOAD 1 → 2 в корабле, UNLOAD 2 → на складе +1 бесплатный товар» (stale cargo в UI)

**Файлы:**
- `Assets/_Project/Trade/Scripts/Dto/MarketSnapshotDto.cs` — добавлено поле `WarehouseEntryDto[] cargo` + сериализация.
- `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` — добавлены `_clientSelectedShip` map, `SetSelectedShipRpc`, `SelectedShipKey`; `SendSnapshotToClient` теперь включает cargo выбранного корабля.
- `Assets/_Project/Trade/Scripts/Client/MarketClientState.cs` — добавлен `RequestSetSelectedShip(locationId, shipId)`.
- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` — `_cargoCache` синхронизируется из `snapshot.cargo`; `RequestSetSelectedShip` вызывается при смене корабля и на первом show.

**Симптом (из репорта пользователя):**
> "если я купил 5 товаров к примеру выбрал корабль нужный и нажал погрузить: то сразу пишется 2 товара в корабле, и выгрузить можно 2, на складе будет 6 (эксплойт)"

**Сценарий:** host-only, `BootstrapScene` + `WorldScene_0_0`, игрок спавнится с двумя-тремя кораблями в зоне (есть stale cargo с прошлой сессии). `PlayerPrefs.DeleteAll()` НЕ делался.

**Что показал лог (`Editor.log`):**
- `[TradeWorld] BUY ... qty=1` x5 → склад = 5 mesium
- `[TradeWorld] LOAD ship=6 qty=1` → склад = 4, **cargo ship 6 = 2** (была 1 stale с прошлой сессии)
- `[TradeWorld] UNLOAD ship=6 qty=1` → склад = 5, cargo = 1
- `[TradeWorld] UNLOAD ship=6 qty=1` → склад = 6, cargo = 0
- `PlayerPrefs`: `PD2_Warehouse_0_primium = mesium x6`, `PD2_Cargo_7 = mesium x1` (тоже stale)

**Корневая причина:** `MarketSnapshotDto` НЕ содержал cargo выбранного корабля (комментарий в `MarketWindow.cs:718` явно: "Cargo не входит в MarketSnapshotDto (слишком жирно слать груз на каждый tick)"). Клиент узнавал cargo **только** из `TradeResultDto.updatedCargoSnapshot`, который приходит **после** успешного Load/Unload. До первой операции (или после смены корабля) UI показывал cargo из локального `_cargoCache` — а там stale или пусто. Игрок не знал, что в трюме уже лежат предметы с прошлой сессии → жал LOAD qty=1, на сервере cargo=1+1=2, потом UNLOAD qty=2 (или дважды UNLOAD qty=1) → склад получал +2 «лишних» единицы. **Серверная логика `TradeWorld.TryLoadToShip/TryUnloadFromShip` была полностью корректна** — баг был исключительно в проекции cargo в UI.

**Фикс (4 точки):**
1. `MarketSnapshotDto.cargo` — новый `WarehouseEntryDto[]` (nullable). Сервер заполняет его cargo выбранного клиентом корабля. При пустом трюме = `null`/`[]`.
2. `MarketServer.SetSelectedShipRpc(locationId, shipId)` — клиент сообщает, какой корабль сейчас выбран. Сервер валидирует (`zone.IsShipInZone(shipId)`) и сохраняет в `_clientSelectedShip: Dictionary<(clientId, locationId) → shipId>`. Если клиент не прислал — fallback на первый корабль в зоне (старое поведение UI: дефолтный `ships[0]`).
3. `MarketClientState.RequestSetSelectedShip(locationId, shipId)` — обёртка над RPC.
4. `MarketWindow.HandleSnapshot` — `_cargoCache = snap.cargo ?? Array.Empty<WarehouseEntryDto>()` (теперь это source of truth, не stale). `MarketWindow` зовёт `RequestSetSelectedShip` при (а) смене корабля через ship-selector, (б) первом auto-select первого корабля на show. `HandleTradeResult` продолжает обновлять `_cargoCache` мгновенно после успешной операции — snapshot-обновление придёт следом и перезапишет то же значение (идемпотентно, без визуального мерцания).

**Что не делали (по AGENTS.md, минимальный фикс):**
- ❌ Не очищали `PD2_Cargo_*`/`PD2_Warehouse_*` при старте — сломало бы legitimate persistence между сессиями.
- ❌ Не делали «сброс cargo при выходе из зоны» — это была бы потеря данных при вылете из игры в трюме.
- ❌ Не трогали `TradeWorld.TryLoadToShip` / `TryUnloadFromShip` / `CargoData.TryAdd` / `TryRemove` / `Warehouse.TryAdd` / `TryRemove` — всё это было корректно (см. изолированный repro через `unityMCP_execute_code` ниже в FIXES_HISTORY).
- ❌ Не ломали существующий поток `TradeResultDto.updatedCargoSnapshot` — оставлен для мгновенного feedback после операции.
- ❌ Не включали cargo ВСЕХ кораблей в snapshot (тяжело для сцен с 5+ кораблями) — только выбранного.

**Изолированная проверка серверной логики (через `unityMCP_execute_code`):**
```
=== Buy 5x mesium ===
  buy 1..5: ok=True, credits: 1000→948
  warehouse: mesium x5
=== Load qty=1 to ship 6 ===
  warehouse: mesium x4, cargo 6: mesium x1
=== Unload qty=1 from ship 6 ===
  warehouse: mesium x5, cargo 6: <empty>
=== Unload qty=1 from ship 6 (AGAIN, should fail) ===
  unload: ok=False, code=ItemNotInCargo
  warehouse: mesium x5, cargo 6: <empty>
```
Сервер **отвергает** попытку UNLOAD из пустого трюма с `ItemNotInCargo`. Никакого дублирования нет.

**Что проверить вручную (Play Mode, host):**
1. В чистом `PlayerPrefs.DeleteAll()` → BUY 5 mesium → LOAD qty=1 на ship X → UI должен показать cargo = 1 ед. (а не 2).
2. UNLOAD qty=1 → cargo = 0, склад = 5. Повторный UNLOAD → красное сообщение «Товара нет в трюме», склад остаётся 5.
3. С преднамеренно stale cargo (`TradeDebugTools` или ручной PlayerPrefs с `PD2_Cargo_X = {"items":[{"itemId":"mesium_canister_v01","quantity":1}]}`): открыть рынок → UI cargo должен **сразу** показать «1 ед.» (а не 0 как раньше). LOAD qty=1 → cargo = 2, UNLOAD qty=2 → cargo = 0, склад = X-1+2 = X+1 (это **корректно**: 1 stale + 1 новый уехал в склад, плюс честный возврат).
4. Сцена с >1 кораблём в зоне: переключить ship через ship-selector → cargo мгновенно подменяется на cargo нового корабля в следующем snapshot (в консоли `[MarketClientState] OnSnapshotReceived: ... cargo=N`).

---

## 2026-06-04 — INVESTIGATION OPEN: E не открывает рынок после E вне зоны (intermittent)

**Статус:** ⚠️ OPEN. Баг не воспроизводится на каждом запуске — нужен свежий лог от пользователя с подтверждённым сценарием. Не фиксили вслепую (по AGENTS.md — "минимальный фикс, не ломая остальное").

**Симптом (из репорта пользователя):**
> "если нажать E сразу после спавна (вне зоны), а потом войти в зону и нажать E — окно не открывается. Без предварительного E вне зоны — работает."

**Сценарий:** host-only, `BootstrapScene` + `WorldScene_0_0`, `MarketZone_Primium` (tradeRadius=36, shipDockRadius=30), spawn игрока `(39999.50, 3000.00, 39999.50)`, зона `(40096.50, 2510.00, 40140.60)`, dist ~196м, **вне** зоны.

**Что было прочитано в рамках анализа:**
- `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs` (130+ строк): `TryOpenMarket`, `FindNearestZone`, `OpenNearest`. Поведение: сначала `MarketZoneRegistry.LocalPlayerZone`, если null — fallback `FindNearestZone` по `localPlayer.GetEffectivePosition()`.
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs`: `PollLocalPlayerZone` (throttled 0.25с) и `OnTriggerEnter` обновляют `LocalPlayerZone` строго по дистанции (FIX 2026-06-04 убрал `if (LocalPlayerZone == this) return;`).
- `Assets/_Project/Trade/Scripts/Network/MarketZoneRegistry.cs`: static `LocalPlayerZone`, `Registry.All` dictionary.
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs:55-116, 280-499`: `GetEffectivePosition()` (effective = корабль если `_inShip`), E-handler — если `_inShip` E резерв, иначе `FindNearestInteractable()` → `TryPickup()` (chest) **ИЛИ** `TryOpenMarket()` **else** → `TryPickup()` fallback.
- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs:1-95, 270-369, 620-720`: `Show/Hide/Toggle/EnsureBuilt/IsLayoutValid`, E-handler в `Update` **убран** (FIX 2026-06-04 UI — был дубликат).

**Что показал лог текущего запуска (не воспроизвёл баг):**
- 8 нажатий E подряд, все **вне зоны** (dist 5238..10308м, `X=33252.68` константа, `Y` упал 3000→1903 — игрок упал с платформы).
- `MarketZoneRegistry.LocalPlayerZone = null` все 8 раз. `Registry.All.Count = 1` (только `primium`).
- `MarketInteractor.FindNearestZone: localPlayerPos=(33252.68, ...) ... primium(d=7258/r=36) => best=null`.
- `MarketZone:primium DIAG PollLocalPlayerZone: outside zone, dist=7258,6` — клиент ни разу не вошёл в зону.
- **Сервер один раз** детектил игрока в зоне (`[MarketZone:primium] server detected player in zone: clientId=0`) — это известный client/server desync на хосте: `transform.position` заморожен `ApplyShipState`-ом (`_controller.enabled = false`), а `Physics.OverlapSphere` на сервере находит коллайдер корабля.
- Игрок в итоге **дисконнектнулся** (`[NetworkTestMenu] Player disconnected: 0`) — тест не прошёл до конца, **баг не воспроизведён**.

**Гипотезы (без подтверждения, не фиксили):**

**Гипотеза A** — `NetworkPlayer.cs:280-499` E-handler после `!TryOpenMarket()` делает `TryPickup()` fallback. `InteractableManager.FindNearestChest(pos, float.MaxValue)` использует **глобальный** радиус → при повторном E внутри зоны сначала идёт `TryPickup()` (на далёкий сундук) вместо `TryOpenMarket()`. Тогда "первый E вне зоны" мог установить какой-то side-effect (открыть chest-инвентарь), а второй E внутри зоны уходит в chest-pickup.  
*Кандидатный фикс:* в E-handler поменять порядок — `TryOpenMarket()` сначала, `TryPickup()` только если зона рынка не в радиусе. **Не применён** — без подтверждения может сломать chest pickup.

**Гипотеза B** — `MarketInteractor.TryOpenMarket` кеширует `MarketZoneRegistry.LocalPlayerZone`. Если `OnTriggerEnter` или `PollLocalPlayerZone` оставил stale-ссылку (например, после FIX 2026-06-04 с `GetEffectivePosition` — позиция корабля отличается от `transform.position` игрока), повторный E открывает **старую** зону, и если игрок визуально не в ней — `MarketServer` отвечает `NotInZone`.  
*Кандидатный фикс:* в `TryOpenMarket` всегда вызывать `FindNearestZone()` заново, не полагаясь на кеш `LocalPlayerZone`. **Не применён** — потенциально лишний `OverlapSphere` каждый E.

**Что нужно для подтверждения и фикса:**
1. Свежий кусок `Editor.log` где **видно** весь цикл: spawn → E (вне зоны, dist>X) → игрок входит в зону (dist<36) → E → окно НЕ открылось. Строки `MarketInteractor/MarketZone/MarketWindow` вокруг второго E.
2. Текущий `Assets/_Project/Scripts/Player/NetworkPlayer.cs` E-handler целиком (особенно порядок `TryOpenMarket` vs `TryPickup`).
3. `Assets/_Project/Scripts/Player/InteractableManager.cs` — подтвердить `FindNearestChest(pos, float.MaxValue)` или найти реальный радиус.

**Что не делали (по AGENTS.md, минимальный фикс без воспроизведения):**
- ❌ Не применяли фикс ни по гипотезе A, ни по B — обе требуют ручной верификации, иначе рискуем сломать chest pickup или regressить `GetEffectivePosition` fix.
- ❌ Не добавляли новые диагностические логи — `MarketInteractor/MarketZone` уже логируют (KNOWN_ISSUES §1) и в воспроизведённом логе не хватило **самого факта входа в зону** (игрок туда не дошёл).
- ❌ Не трогали `NetworkPlayer.E`-handler и `MarketInteractor.TryOpenMarket`.

**См. также:** [KNOWN_ISSUES.md §13](KNOWN_ISSUES.md#13-investigation-open-e-не-открывает-рынок-после-e-вне-зоны-intermittent).

---

## 2026-06-04 — FIX: рынок не открывается, если игрок подлетел на корабле (GetEffectivePosition)

**Файлы:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs:104-116` — новый helper `GetEffectivePosition()`
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs:169, 287` — использовать effective position в `PollLocalPlayerZone` и `OnTriggerEnter`
- `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs:2, 83` — `using ProjectC.Player;` + использовать effective position в `FindNearestZone`

**Симптом (из лога теста, см. unity-mcp):**
```
[MarketZone:primium] server detected player in zone: clientId=0
[MarketZone:primium] DIAG PollLocalPlayerZone: outside zone, dist=4045,9, tradeRadius=36,0, localPlayerPos=(39820.91, -1128.69, 41888.07), zonePos=(40096.50, 2510.00, 40140.60)
```
Сервер видит игрока в зоне (через OverlapSphere — попадает в коллайдер корабля), клиент упорно сообщает дистанцию 1600–4000м. `MarketInteractor.TryOpenMarket` уходит в `FindNearestZone`, который тоже мерит от `localPlayer.transform.position` — получает best=null → возвращает false → окно рынка не открывается. UI выглядит зависшим.

**Сценарий:** игрок сидит в корабле и подлетает к причалу рынка. Если до посадки/входа в зону нажать E (выход из зоны → `LocalPlayerZone` сбрасывается), а потом залететь в зону, E перестаёт открывать рынок. Пешком — работает, потому что `CharacterController` обновляет `transform.position` каждый кадр.

**Корневая причина:** в `ApplyShipState` (`NetworkPlayer.cs:441-448`) `_controller.enabled = false` — игрок больше не двигается через `CharacterController`, его `transform.position` заморожен на точке посадки. Реально в мире летит корабль, а пилот «висит» в воздухе в исходной точке. Все клиентские дистанционные проверки (рынок, OnTriggerEnter) брали `localPlayer.transform.position` напрямую — получали замороженную позицию, хотя сервер через `Physics.OverlapSphere` корректно детектил коллайдер корабля внутри `tradeRadius`.

**Фикс (один helper, 3 точки использования):**
- В `NetworkPlayer` добавлен публичный `GetEffectivePosition()`: возвращает `_currentShip.transform.position` если `_inShip && _currentShip != null`, иначе `transform.position`.
- `MarketZone.PollLocalPlayerZone` (client-side, обновление `LocalPlayerZone`): `Vector3.Distance(zone, localPlayer.GetEffectivePosition())`
- `MarketZone.OnTriggerEnter` (client-side, ранняя установка `LocalPlayerZone` при срабатывании SphereCollider): `Vector3.Distance(zone, np.GetEffectivePosition())`
- `MarketInteractor.FindNearestZone` (fallback, когда `LocalPlayerZone == null`): использует тот же `GetEffectivePosition()`

**Что не делали (важно):**
- ❌ Не рефакторили `ApplyShipState` чтобы «правильно» парентить игрока к кораблю или двигать `transform.position` — это сломало бы `NetworkTransform` репликацию, камеру и CharacterController при выходе.
- ❌ Не трогали `MarketZone.PollPlayersInRadius` (server-side) — там `OverlapSphere` уже корректно находит коллайдер корабля и через `GetComponentInParent<NetworkPlayer>` матчит пилота.
- ❌ Не убирали diagnostic-логи из `MarketInteractor`/`MarketZone` — оставлены на случай следующих регрессий (KNOWN_ISSUES §1).
- ❌ Не рефакторили legacy `TradeTrigger` / `AutoTradeZone` / `TradeUI` (KNOWN_ISSUES §3) — отдельный cleanup.

**Что проверить вручную (в Play Mode, host):**
1. Сесть в корабль (F) → улететь за пределы зоны рынка (X<40000, Y<2000) → нажать E в полёте → должна быть `[MarketInteractor] LocalPlayerZone is null and no zone in range`.
2. Залететь в зону на корабле → в консоли появится `[MarketZone:primium] client: local player entered zone (dist=~0..36)`.
3. Нажать E → откроется окно рынка, в консоли `[MarketInteractor] TryOpenMarket: zone='primium'`.
4. Сойти с корабля (F) на палубе внутри зоны → `LocalPlayerZone` остаётся `this` (расстояние меряется так же — от корабля, но корабль в той же точке, что игрок).
5. Обычный сценарий (пешком) — регрессий быть не должно.

---

## 2026-06-04 — INVESTIGATION CLOSED: «покупаешь 1 → на склад попадает 2»

**Симптом (из репорта):** при первой покупке `qty=1` на склад игрока приходит `2 ед.`. Подозрение на двойное добавление в `Warehouse.TryAdd` или двойной RPC.

**Диагностика:**
- Прочитан `MarketWindow.OnBuyClicked` → `MarketClientState.RequestBuy` → `MarketServer.RequestBuyRpc` → `TradeWorld.TryBuy` → `Warehouse.TryAdd` — единственный путь, дублей вызовов не найдено.
- Из unity-mcp лога: на каждое нажатие ровно один `[TradeWorld] BUY ... qty=1`, кредиты списываются один раз (цена растёт по инфляции: 10→11→…).
- Поле `wh` в `[MarketClientState] OnSnapshotReceived: ... wh=1` — это `snapshot.warehouse.Length` (число **типов**, не единиц). Прямого `e.quantity` в логах нет.
- Через `unityMCP_execute_code` проверены PlayerPrefs: ключ `PD2_Warehouse_0_primium` отсутствовал, `PD2_Credits_0 = 891.32`. До этого в логе `wh=1` уже на подписке — склад был непустой, что объясняется остатками из прошлой сессии.

**Воспроизведение:** пользователь запустил тест на чистом PlayerPrefs, склад пуст → купил 1 → на UI отобразилось «1 ед.». Баг не воспроизводится.

**Заключение:** исходное наблюдение «покупаешь 1 → попадает 2» объясняется остатками `PD2_Warehouse_*` из прошлой сессии: на складе уже было `mesium, qty=1` (видно как `wh=1` ещё на subscribe), покупка `+1` давала `mesium, qty=2` — это корректное сложение с остатком, не дублирование. Код покупки не виноват.

**Что не делали (по AGENTS.md, минимальный фикс):**
- ❌ Не добавляли диагностические логи в `Warehouse.TryAdd` / `TradeWorld.TryBuy` — после подтверждения от пользователя они не нужны.
- ❌ Не правили `Warehouse.TryAdd` (там `e.quantity += quantity` с правильной работой со struct-копией) и `MarketClientState.RequestBuy` — оба корректны.
- ❌ Не вводили авто-сброс `PD2_*` ключей на старте — это сломало бы legitimate use case (persistence между сессиями).

**Рекомендация на будущее (если репорт повторится):**
1. Перед диагностикой «удвоения при покупке» просить пользователя сбросить `PD2_Warehouse_*` и `PD2_Cargo_*` (через `PlayerPrefs.DeleteAll()` или временную кнопку в `TradeDebugTools`).
2. Добавить в `MarketClientState.OnSnapshotReceived` рядом с `wh=` ещё и `whQty=` — сумму `e.quantity` по `snapshot.warehouse`, чтобы сразу было видно «до и после покупки».
3. Если и на чистом PlayerPrefs воспроизводится — добавить одноразовый `Debug.Log` в `Warehouse.TryAdd` с `itemId, qty, existingQtyBefore, existingQtyAfter`.

---

## 2026-06-04 — UI верстка (4 фикса + 1 fix жизненного цикла + 3 диагностических лога)

### FIX 1 — ListView selection не обновлял `_selectedMarketItem`

**Файл:** `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs:177-216`

**Симптом:** Клик по строке в списке товаров не выделял её. Кнопки КУПИТЬ/ПРОДАТЬ сразу выходили по `if (_selectedMarketItem < 0) return;`. Покупка не работала, хотя цены отображались.

**Корневая причина:** В коде не было `selectionType` / `selectionChanged` callback на ListView. ListView обновлял свой внутренний `selectedIndex`, но UI-контроллер не получал уведомления. Плюс в Unity 6 `onSelectionChange` deprecated — нужно `selectionChanged` с `IEnumerable<object> selectedItems` (сами объекты, а не индексы).

**Фикс:**
- На всех 3 ListView (`_itemList`, `_warehouseList`, `_cargoList`):
  - `_list.selectionType = SelectionType.Single`
  - `_list.selectedIndex = -1` (стартовое)
  - `_list.selectionChanged += selectedItems => { _index = FindSelectedItemIndex<T>(list, selectedItems); _list.Rebuild(); }`
- Новый helper `FindSelectedItemIndex<T>` (MarketWindow.cs:520-538) — ищет объект в `itemsSource` через `Array.IndexOf` или линейный поиск, возвращает индекс или -1.

### FIX 2 — `IsLayoutValid()` был слишком строгим

**Файл:** `MarketWindow.cs:107-114`

**Симптом:** Первый E после запуска сцены — `EnsureBuilt()` не вызывался (или вызывался лишний раз). UI не появлялся до второго нажатия E.

**Корневая причина:** Старая проверка полагалась на `resolvedStyle.width` — на первом кадре после `Clear() + CloneTree()` он бывает `NaN/0` (USS layout не успел посчитаться). Это приводило к двойной пересборке или, наоборот, пропуску пересборки.

**Фикс:** Проверяем только что дерево существует: `return _built && _root != null && _mainContainer != null;`. Не полагаемся на `resolvedStyle`.

### FIX 3 — `MarketClientState.Instance == null` на хосте

**Файл:** `Assets/_Project/Scripts/Core/NetworkManagerController.cs` (в `Awake()`)

**Симптом:** Сервер видел игрока в зоне, отправлял `Subscribe OK`, но клиент (на том же процессе) не получал `OnSnapshotReceived` — `MarketClientState.Instance == null` в `NetworkPlayer.ReceiveMarketSnapshotTargetRpc`.

**Корневая причина:** `MarketClientState` GO не существовал на старте — `[MarketClientState]` GameObject нужно было создавать вручную в `BootstrapScene`. Если забыли — NRE.

**Фикс:** В `NetworkManagerController.Awake()` создаём `[MarketClientState]` как root GameObject (DontDestroyOnLoad) с компонентом `MarketClientState`. Гарантирует наличие singleton до старта `NetworkManager`.

### FIX 4 — `pickingMode` на `_root` ломал UGUI клики

**Файл:** `MarketWindow.cs:138-148, 647, 685`

**Симптом:** Когда окно рынка было **закрыто** (display:None на main-container, но `_root` TemplateContainer растянут на весь rootVE с position:Absolute, inset:0), невидимый `_root` перехватывал ВСЕ клики → UGUI кнопки (Host, Connect, ...) не реагировали.

**Корневая причина:** UI Toolkit PanelSettings получает pointer events РАНЬШЕ UGUI Canvas (InputSystemUIInputModule маршрутизирует так в Unity 6). По умолчанию `pickingMode = Position`, который перехватывает клики по всему растянутому root.

**Фикс:**
- В `EnsureBuilt()`: `_root.pickingMode = PickingMode.Ignore;` (по умолчанию)
- В `Show()`: `_root.pickingMode = PickingMode.Position;` (включаем только когда окно открыто)
- В `Hide()`: `_root.pickingMode = PickingMode.Ignore;` (возвращаем)

### FIX 4b — `.list-section` flex-shrink ломал layout

**Файл:** `Assets/_Project/Trade/Resources/UI/MarketWindow.uss`

**Симптом:** Списки товаров/склада/груза схлопывались до 0px высоты. Заголовки "Товары на рынке / Ваш склад / Груз корабля" висели одновременно (FIX 4a тоже, но это была другая причина).

**Корневая причина:** В USS на `.list-section` стояло `flex-shrink: 1` и `min-height: 0`. Внутри `flex-direction: column` с фиксированной высотой это приводит к сжатию секции до 0. Контейнер `main-container` имеет `flex-direction: column; align-items: stretch;`, и секции конкурировали за вертикальное пространство.

**Фикс:** Убрали `flex-shrink: 1` и `min-height: 0` на `.list-section`. Теперь секции занимают естественную высоту. Дополнительно (FIX для одновременных заголовков) — `SwitchTab("market")` в `MarketWindow.cs:488-502` скрывает через `display:None` всю секцию (заголовок + список), а не только ListView.

### FIX 5 (diagnostic) — `MarketZone.PollLocalPlayerZone` логирует дистанцию

**Файл:** `MarketZone.cs:147-196`

**Назначение:** Throttled debug-логи (раз в ~5 сек, при `_diagTickCounter % 20 == 0`) для диагностики «игрок не в зоне, хотя кажется что в зоне»:
- `Debug.Log("[MarketZone:primium] DIAG PollLocalPlayerZone: outside zone, dist=344,3, tradeRadius=30, ...")`
- `Debug.Log("[MarketZone:primium] DIAG PollLocalPlayerZone: FindLocalPlayer=null (total NetworkPlayers=1, IsSpawned=1, IsOwner=1)")`

Это помогло выявить, что tradeRadius реально 30м (а не 5 как в спеке), и что LocalPlayerZone не обновлялся из-за guard `if (LocalPlayerZone == this) return;` в старой версии — игрок мог уйти на 100м, а LocalPlayerZone оставался `this`.

### FIX 6 (diagnostic) — `MarketInteractor.TryOpenMarket` логирует Registry

**Файл:** `MarketInteractor.cs:27, 50, 59, 88-104`

**Назначение:** Логирует `LocalPlayerZone` и `Registry.All.Count` при каждом вызове E. Плюс `FindNearestZone` логирует дистанции ко ВСЕМ зонам, чтобы видеть какие вообще зарегистрированы и какие в радиусе.

### FIX 7 (diagnostic) — `MarketInteractor.FindNearestZone` логирует каждую зону

**Файл:** `MarketInteractor.cs:64-106`

**Назначение:** Когда `LocalPlayerZone == null`, fallback `FindNearestZone` логирует:
```
[MarketInteractor] FindNearestZone: localPlayerPos=(x,y,z), zones=1 — primium(d=28,7/r=30,0@(x,y,z)) => best=primium
```

## Что ещё было исправлено (более ранние сессии)

### Race condition: `MarketZone.OnEnable` до `NetworkManager.Start`

**Файл:** `MarketZone.cs:68-88`

**Симптом:** Zone не регистрировалась в `MarketZoneRegistry` если сцена грузилась раньше старта NetworkManager. Клиент потом не находил зону через `FindNearestZone`, сервер не находил через `MarketZoneRegistry.Get`.

**Фикс:** Всегда регистрируем в `OnEnable` + подписываемся на `NetworkManager.OnServerStarted`/`OnClientStarted` для повторной регистрации. Дублирующая регистрация безопасна (`Register` проверяет `_zones[locationId] == this`).

### Guard `if (LocalPlayerZone == this) return;` блокировал cleanup

**Файл:** `MarketZone.cs:170-195` (PollLocalPlayerZone)

**Симптом:** Игрок уходил из зоны (dist > tradeRadius), но `LocalPlayerZone` оставался `this`. TryOpenMarket работал, но игрок был далеко.

**Фикс:** Убран ранний return. Poll ВСЕГДА пересчитывает дистанцию и ставит/сбрасывает `LocalPlayerZone` строго по факту попадания.

### Debounce на `_playersInZone` remove

**Файл:** `MarketZone.cs:208-256` (PollPlayersInRadius)

**Симптом:** CharacterController + SphereCollider Trigger timing → OverlapSphere иногда «промахивался» (NetworkTransform interpolation, физика), игрок удалялся из `_playersInZone` на 250мс → следующий RPC получал `NotInZone`.

**Фикс:** `MISS_THRESHOLD = 3` подряд пропусков (~0.75с) перед удалением. `Dictionary<ulong, int> _missingTicks` счётчик.

### SphereCollider radius = max(tradeRadius, shipDockRadius) = 591м

**Файл:** `MarketZone.cs:55-66` (Awake)

**Симптом:** Awake ставил `sphere.radius = Mathf.Max(tradeRadius, shipDockRadius)`. SphereCollider детектил игрока в 591м от центра зоны, `OnTriggerEnter` срабатывал преждевременно → `LocalPlayerZone = this` до того, как игрок в реальном tradeRadius.

**Фикс:** `_sphere.radius = tradeRadius` (только для player detection). Корабли детектятся через `PollShipsInRadius` (OverlapSphere с shipDockRadius) — для них SphereCollider не нужен. Дополнительная defense-in-depth проверка `dist ≤ tradeRadius` в `OnTriggerEnter` (MarketZone.cs:287-288).

## Известные ограничения, оставшиеся после 2026-06-04

См. [KNOWN_ISSUES.md](KNOWN_ISSUES.md):
- §1 Diagnostic-логи остаются — можно убрать после стабилизации
- §2 Initial `wh=0` → `wh=1` warning в `[MarketWindow] Show(): main w=0 h=0` — косметика
- §3 Старая v1 архитектура (`TradeUI`, `TradeMarketServer`, `PlayerTradeStorage`, ...) не удалена
- §4 NetworkPlayer.TradeBuyServerRpc/SellServerRpc (lines 588-617) — dead code, не вызывается

---

## 2026-06-04 — FIX: ghost PlayerSpawner маскирует реального игрока в MarketZone.FindLocalPlayer

**Файлы:**
- `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs:110-129` — `FindLocalPlayer` skip'ит GameObject с компонентом `NetworkPlayerSpawner`
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs:198-219` — `FindLocalPlayer` тот же guard

**Симптом (из юзерского репорта + live play mode через `unityMCP_execute_code`):**
> "рынок просто при каком-то старте - не открывается вообще. персонаж в зоне действия а рынок не открывается"

Live state до фикса (host, `BootstrapScene` + `WorldScene_0_0`):
```
All NetworkPlayers: 2
  'PlayerSpawner'         IsOwner=True IsSpawned=True HasNetworkPlayerSpawner=True  pos=(39999.50, 2510.00, 39999.50)
  'NetworkPlayer(Clone)'  IsOwner=True IsSpawned=True HasNetworkPlayerSpawner=False pos=(40092.28, 2501.32, 40138.48)
MarketZones: 2
  'primium'  pos=(40096.50, 2510.00, 40140.60)  tradeR=36
  'TEST_1'   pos=(39874.10, 2510.00, 39970.00)  tradeR=30
MarketZoneRegistry.LocalPlayerZone=null
```

`FindLocalPlayer` в обоих файлах итерировал `FindObjectsByType<NetworkPlayer>` и возвращал первого с `IsOwner=True` — а это `PlayerSpawner` ghost (его InstanceID ниже, чем у свежеспавненного `NetworkPlayer(Clone)`). `GetEffectivePosition()` ghost'а → `(39999.50, 2510, 39999.50)`. Дистанция до `primium` = 171м > tradeRadius=36 → `PollLocalPlayerZone` всегда "outside zone" → `LocalPlayerZone=null` → `TryOpenMarket` → `FindNearestZone` → `best=null` → рынок не открывается.

При этом реальный `NetworkPlayer(Clone)` стоял в ~5м от центра `primium` (внутри `tradeRadius=36`) — он мог нажать E 100 раз, но ghost-ссылка ломала весь pipeline.

**Корневая причина:** scene-placed `PlayerSpawner` GameObject в `BootstrapScene` имеет компоненты `NetworkPlayerSpawner` (маркер) + `NetworkPlayer`. NGO 2.x на хосте даёт `OwnerClientId=0` (server-owned) и scene-placed NetworkObject'ам → `IsOwner==true` для ghost'а (footgun, см. `NetworkPlayer.cs:130-147` — то же самое для camera/inventory init).

**Почему "intermittent" в логах прошлых попыток:** в INVESTIGATION OPEN §13 логе игрок **падал с платформы** (Y 3000→1903) и не доходил до зоны, поэтому dist=5238..10308м > tradeRadius у обоих зон. Реальная причина (ghost-ссылка) была замаскирована тем, что "игрок в любом случае не в зоне" — после падения. Но даже когда игрок стоял на платформе, ghost-ссылка ломала open — просто у пользователя в тестовом сценарии этого не случилось.

**Фикс (1 guard, 2 файла):** в обеих `FindLocalPlayer` пропускаем `NetworkPlayer`, если на его GameObject есть `NetworkPlayerSpawner` (тот же discriminator, что в `NetworkPlayer.OnNetworkSpawn:148`). Реальный `NetworkPlayer(Clone)` из `PlayerPrefab` этого компонента не имеет — значит он единственный кандидат.

**Что не делали (по AGENTS.md — минимальный фикс):**
- ❌ Не удаляли scene-placed `PlayerSpawner` GameObject — на нём висят `NetworkPlayerSpawner`, `CharacterController`, `PlayerInputReader`, `NetworkObject` с референсами из других систем. Удаление — отдельная задача (см. `NetworkPlayerSpawner.cs:14-26`).
- ❌ Не трогали `NetworkPlayer.cs` / E-handler / `InteractableManager` — гипотеза A из INVESTIGATION OPEN §13 отвергнута, корень был в `FindLocalPlayer`.
- ❌ Не рефакторили `MarketInteractor.TryOpenMarket`/`FindNearestZone` — fallback-логика корректна, проблема была только в `FindLocalPlayer`.
- ❌ Не убирали diagnostic-логи — оставлены на случай следующих регрессий (KNOWN_ISSUES §1).

**Подтверждение фикса (через `unityMCP_execute_code` после фикса, host):**
```
1) E OUTSIDE (500м от primium):  TryOpenMarket=False  ✓
2) E INSIDE  (0м от primium):    TryOpenMarket=True   ✓
   LocalPlayerZone=primium
   MarketWindow.IsVisible=True
```

**См. также:** [KNOWN_ISSUES.md §13 RESOLVED](KNOWN_ISSUES.md#13-investigation-open-e-не-открывает-рынок-после-e-вне-зоны-intermittent).

---

## 2026-06-05 — C2-этап: контракты как 3-й таб рынка + 4 bug-фикса

**Контекст:** после успешной миграции `Markets` v2 (commit `3395d8e`) и per-ship cargo cache (commit `3395d8e`) приоритетом стала миграция контрактов с v1-инфраструктуры (`ContractSystem.cs` + `ContractBoardUI.cs` UGUI + `ContractZone.cs` + `ContractZoneRegistry.cs`) на v2-архитектуру (`ContractServer` + `ContractClientState` + DTO). См. аудит `docs/Markets/MARKETS_V2_AUDIT_2026-06-05.md` (этап 1, C2).

### Шаг 1 — коммит `первичная интеграция контрактов`: v2-архитектура

Создана полная цепочка v2-контрактов:

**Серверная сторона:**
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` (19 КБ) — `NetworkBehaviour`, RPC hub, ставится в `BootstrapScene` рядом с `MarketServer`. Методы: `RequestListRpc`, `RequestAcceptRpc`, `RequestCompleteRpc`, `RequestFailRpc`, `ReceiveContractSnapshotClientRpc` (server→owner), `ReceiveContractResultClientRpc`. `FixedUpdate` тикает таймеры активных контрактов + авто-fail по `TimerExpired`. `MaxActiveContractsPerPlayer=3` + rate limit `30 ops/min` (FIX F2).
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` (28 КБ) — POCO singleton, бизнес-логика (`GetAvailableForLocation`, `GetActiveForPlayer`, `TryAccept`, `TryComplete`, `TryFail`, `HandleFailedContract`, `Tick`, `BuildSnapshot`). Хранит `_availableContracts: Dictionary<string, ContractData>`, `_locationContracts: Dictionary<string, List<string>>`, `_playerContracts: Dictionary<ulong, List<string>>`, `_playerDebts`. Реюз `IPlayerDataRepository` из `TradeWorld` (если есть) или `PlayerPrefsRepository`.
- `Assets/_Project/Trade/Scripts/Core/ContractDebt.cs` (5.8 КБ) — POCO долга. `CurrentDebt`, `Level` (`None/Warning/Restricted/Hunted/Bounty/Headhunt`), `AddDebt`, `PayDebt`, `DecayRate`, `CanAcceptContracts` (false при `Level >= Restricted`).
- `Assets/_Project/Trade/Scripts/Core/ContractWorldItemResolver.cs` (4.3 КБ) — мини-резолвер товаров, не зависит от `TradeDatabase` (есть `CreateWithDefaults` с 7 items).

**Клиентская сторона:**
- `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs` (6.5 КБ) — singleton-проекция, аналог `MarketClientState`. `CurrentSnapshot`, `LastResult`, события `OnSnapshotUpdated`, `OnContractResult`. Convenience API: `RequestList`, `RequestAccept`, `RequestComplete`, `RequestFail` + `LocalizeResultCode(ContractResultCode)`.
- `Assets/_Project/Trade/Scripts/Client/ContractBoardWindow.cs` (21.8 КБ) — UI Toolkit контроллер (временное отдельное окно, см. шаг 2 — удалено).
- `Assets/_Project/Trade/Scripts/Client/ContractInteractor.cs` (4.5 КБ) — E-handler (временный, удалено в шаге 2).
- `Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uxml` + `.uss` (2.8+4.3 КБ) — UI Toolkit assets (удалено в шаге 2).

**Сетевой слой:**
- `Assets/_Project/Trade/Scripts/Dto/ContractDto.cs` (3.7 КБ) — `struct INetworkSerializable`, поля: `contractId, type, state, itemId, displayName, quantity, fromLocationId, toLocationId, reward, cargoValue, timeLimit, timeRemaining, isReceiptContract`.
- `Assets/_Project/Trade/Scripts/Dto/ContractSnapshotDto.cs` (3.8 КБ) — `struct INetworkSerializable`, поля: `locationId, displayName, available[], active[], debtAmount, debtLevel, canAcceptContracts, marketTimeMultiplier, secondsUntilNextTick`.
- `Assets/_Project/Trade/Scripts/Dto/ContractResultDto.cs` (3.7 КБ) — `struct INetworkSerializable`, поля: `code, contractId, success, message, reward, newCredits, newDebt, updatedContract: ContractDto?`.
- `Assets/_Project/Trade/Scripts/Dto/ContractResultCode.cs` (3.1 КБ) — enum: `Ok, NotInZone, ContractNotFound, ContractNotPending, ContractNotActive, ContractNotAssigned, MaxActiveReached, TooMuchDebt, TimerExpired, WrongDestination, CargoMissing, WarehouseFull, ItemNotFound, RateLimited, InternalError`.

**NetworkPlayer.cs** — добавлены 2 TargetRpc (вызываются сервером на owner'е):
- `ReceiveContractSnapshotTargetRpc(ContractSnapshotDto)` — обновляет `ContractClientState.CurrentSnapshot`
- `ReceiveContractResultTargetRpc(ContractResultDto)` — обновляет `LastResult`

**NetworkManagerController.cs** — auto-spawn `[ContractClientState]` (паттерн FIX 2026-06-04 для `MarketClientState`):
```csharp
var go = new GameObject("[ContractClientState]");
go.AddComponent<ProjectC.Trade.Client.ContractClientState>();
```

**Scene setup (MCP):**
- Создан GO `[ContractServer]` в `BootstrapScene` с `NetworkObject` + `ContractServer` + опционально `TradeItemDatabase` в инспекторе.
- Создан GO `[ContractBoardWindow]` в `BootstrapScene` с `UIDocument` (`MarketPanelSettings` + `ContractBoardWindow.uxml`) + `ContractBoardWindow` component.
- Создан GO `[NPCAgent_Primium]` в `WorldScene_0_0` рядом с `MarketZone_Primium` (`SphereCollider` isTrigger r=5 + `ContractZone` с `locationId="primium"`).
- (Позже в шаге 2 — оба UI/Zone GO удалены как дублирующая инфраструктура.)

**Design notes:**
- `docs/dev/CONTRACT_V2_MIGRATION.md` (36 КБ) — полная архитектура, файлы, риски, verification.
- `docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` (28 КБ) — план рефакторинга UI в 3-й таб.

### Шаг 2 — коммит `контракты отладка и работа`: рефакторинг + 4 bug-фикса

После smoke-test'а пользователь пожаловался: **"меню контрактов сразу перекрывает стартовый UI (host\server)"** + **"новое UI контрактов может содержать прошлые баги что были исправлены в рынке"**. Решение: **контракты → 3-й таб** в `MarketWindow`. Один UI-стек, одни FIX'ы, `MarketZone` остаётся единственной зоной.

#### 2.1 Рефакторинг: контракты в таб рынка

**Удалено 6 файлов** (v2-дубликаты, см. шаг 1):
- `ContractBoardWindow.cs` + .meta
- `ContractInteractor.cs` + .meta
- `ContractZone.cs` + .meta
- `ContractZoneRegistry.cs` + .meta
- `ContractBoardWindow.uxml` + .meta
- `ContractBoardWindow.uss` + .meta

**Удалено 2 GO из сцен** (через MCP):
- `[ContractBoardWindow]` в `BootstrapScene`
- `[NPCAgent_Primium]` в `WorldScene_0_0`

**Осталось (серверная часть v2, не тронута):**
- `ContractServer.cs`, `ContractWorld.cs`, `ContractWorldItemResolver.cs`, `ContractDebt.cs`, `ContractClientState.cs`
- 4 DTO (`ContractDto`, `ContractSnapshotDto`, `ContractResultDto`, `ContractResultCode`)
- RPC в `NetworkPlayer.cs`
- `[ContractServer]` GO в `BootstrapScene` (нужен — единственный RPC hub)
- `[ContractClientState]` auto-spawn в `NetworkManagerController`
- Дизайн-ноты (CONTRACT_V2_MIGRATION + CONTRACTS_AS_MARKET_TAB_REFACTOR)

**Legacy v1-стек** (оставлен как регресс-сетка, удалится в C1-cleanup):
- `ContractSystem.cs` (838 строк), `ContractBoardUI.cs` (549 строк, UGUI), `ContractData.cs` (223 строки), `ContractTrigger.cs` (теперь — scene-marker для NPC-агента, вызывает `MarketInteractor.TryOpenMarket` вместо legacy UI), `PlayerTradeStorage` (storage игрока).

**Modified файлы (7):**
- `MarketWindow.uxml` — +1 таб `tab-contracts` + `contracts-section` с `ListView` + 3 кнопки (`accept-btn`/`complete-btn`/`fail-btn`).
- `MarketWindow.uss` — +9 классов: 3 action-btn цвета (`.accept` зелёный, `.complete` синий, `.fail` красный) + 7 стилей для `.contract-row`/`.contract-type`/`.contract-item`/`.contract-reward`/`.contract-timer`/`.type-standard`/`.type-urgent`/`.type-receipt`/`.timer-warn`/`.timer-danger`/`.timer-ok`.
- `MarketWindow.cs` — +260 строк:
  - **Поля**: `_contractsList`, `_contractsSection`, `_acceptBtn`, `_completeBtn`, `_failBtn`, `_selectedContractItem`, `_contractsCache`, `IsShipSelectorVisible()`.
  - **`EnsureBuilt`**: инициализация ListView (makeItem=`MakeContractRow`, bindItem=`BindContractRow`, selectionType=Single, selectionChanged) + кнопки + `tab-contracts` handler + **подписка на `ContractClientState`**.
  - **`OnEnable`/`OnDisable`**: подписка/отписка от `ContractClientState.OnSnapshotUpdated`/`OnContractResult`.
  - **`MakeContractRow`/`BindContractRow`**: 4 Label'а (type/item/reward/timer) с CSS-классами. Highlight selected.
  - **`HandleContractSnapshot`**: фильтр available по `fromLocationId == currentLocationId` + `state == Pending` (защита), active без фильтра локации, фильтр `state == Active`. Combined list: active first, then available.
  - **`HandleContractResult`**: message feedback, `IsVisible()` check.
  - **`OnAcceptContractClicked`/`OnCompleteContractClicked`/`OnFailContractClicked`**: проверка `_selectedContractItem` + `state` + `ContractClientState.RequestXxx(contractId)`. Кнопка СДАТЬ требует чтобы игрок был в `toLocationId` (валидация на сервере).
  - **`SwitchTab`**: 3 таба — `isMarket`/`isWarehouse`/`isContracts`. Sections + кнопки видимы только в своём табе. qty row виден только в РЫНОК.
  - **Static helpers** (портированы из `ContractBoardWindow.cs`): `GetContractTypeDisplayName`, `GetContractTypeClass`, `GetContractTimeRemainingString`, `GetContractTimerClass`.
- `MarketInteractor.cs` — +5 строк: после `RequestSubscribeMarket` вызывает `ContractClientState.RequestList(locationId)` (синхронизация таба "КОНТРАКТЫ" с текущей зоной).
- `ContractServer.cs` — 5 замен `ContractZoneRegistry.Get` → `MarketZoneRegistry.Get`. Сигнатура `ValidateInZone(out ContractZone)` → `out MarketZone` (использует `MarketZone.IsPlayerInZone`).
- `ContractTrigger.cs` — переписан: scene-marker, OnTriggerEnter/Exit обновляет `_nearbyPlayer`. По `C` → `MarketInteractor.TryOpenMarket()`. OnTriggerExit → `MarketWindow.Hide()`. DrawGizmosSelected для визуализации.
- `NetworkManagerController.cs` — auto-spawn `[ContractClientState]` (без изменений от шага 1).

#### 2.2 Bug #1: `ContractResultDto.NetworkSerialize` — `InvalidOperationException: Nullable object must have a value`

**Симптом (юзерский репорт):**
> "после того как открываю контракты и беру контракт: `InvalidOperationException: Nullable object must have a value. System.Nullable\`1[T].get_Value ()` ... `at Assets/_Project/Trade/Scripts/Dto/ContractResultDto.cs:64`"

**Корневая причина:** NGO 2.x не сериализует `Nullable<T>` (`ContractDto? updatedContract`). Старая логика на reader-пути:
```csharp
bool hasContract = updatedContract.HasValue;  // default-инициализирован в null → false
serializer.SerializeValue(ref hasContract);    // читаем из буфера: true
if (hasContract) {
    var c = updatedContract.Value;  // 💥 Value на null
    c.NetworkSerialize(serializer);
}
```
На reader-пути `updatedContract` уже дефолт-инициализирован в `null` NGO'ом, и попытка `.Value` бросает `InvalidOperationException`.

**Фикс** (явно разделил writer/reader ветки):
```csharp
if (serializer.IsWriter) {
    bool hasContract = updatedContract.HasValue;  // OK — значение есть
    serializer.SerializeValue(ref hasContract);
    if (hasContract) { var c = updatedContract.Value; c.NetworkSerialize(serializer); }
} else {
    bool hasContract = false;
    serializer.SerializeValue(ref hasContract);
    if (hasContract) {
        var c = default(ContractDto);  // локальная переменная, не .Value!
        c.NetworkSerialize(serializer);
        updatedContract = c;  // присваиваем в конце
    } else {
        updatedContract = null;
    }
}
```

**Smoke test:** `RequestListRpc` → snapshot доходит → UI обновляется. RPC без exception.

#### 2.3 Bug #2: награда не отображается в UI

**Симптом (юзерский репорт):**
> "когда сдается - нужно поправить выдачу наград"

**Корневая причина:** `HandleContractResult` обновлял **только** `_messageLabel.text` после RPC. Сервер начислял reward в `_repository.SetCredits(clientId, current + contract.reward)` (`ContractWorld.TryComplete:417`), возвращал `result.newCredits` в DTO, **но** клиент игнорировал это поле. `_creditsLabel` оставался прежним, `MarketClientState._currentSnapshot.credits` тоже.

**Фикс (2 места):**
```csharp
private void HandleContractResult(ContractResultDto result) {
    // ...
    if (_creditsLabel != null && result.newCredits > 0f)
        _creditsLabel.text = $"Кредиты: {result.newCredits:F0} CR";
    
    // Синхронизировать MarketClientState snapshot
    if (result.IsSuccess && _state != null && _state.CurrentSnapshot.HasValue)
        _state.RequestSubscribeMarket(_state.CurrentSnapshot.Value.locationId);
    // ...
}
```

**Smoke test:** "награда выдается" (подтверждено юзером). Кредиты видны сразу в `_creditsLabel`, при следующем snapshot — в `MarketClientState`.

#### 2.4 Bug #3: message не показывается вне таба КОНТРАКТЫ

**Симптом (юзерский репорт):**
> "когда беру контракт до сих пор в UI нигде не отмечается"

**Корневая причина:** `HandleContractResult` имел `if (!IsVisible() || _activeTab != "contracts") return;` — игрок жмёт ВЗЯТЬ в табе РЫНОК, RPC доходит, message игнорируется.

**Фикс:**
```csharp
- if (!IsVisible() || _activeTab != "contracts") return;
+ if (!IsVisible()) return;  // message показываем в любом табе
```

#### 2.5 Bug #4: взятый контракт визуально не отличается от pending

**Симптом (юзерский репорт):**
> "когда берем контракт до сих пор в UI нигде не отмечается что контракт взят. его нужно может красить в зеленый и писать что взят или он должен сразу при взятии скрываться из списка контрактов чтобы не путать"

**Корневая причина:** после accept сервер переводит контракт в `state=Active`, но визуально строка в `ListView` выглядела так же (тот же текст, тот же фон, то же reward). Игрок не понимал, что c1 уже "его".

**Фикс (3 уровня):**

**1. Защита от дублей в фильтре** (`HandleContractSnapshot`):
```csharp
// Available: фильтр по state == Pending И fromLocationId == currentLocationId
if (c.state != (byte)ContractState.Pending) continue;
if (!string.Equals(c.fromLocationId, currentLocationId, ...)) continue;

// Active: фильтр по state == Active (защита)
if (activeAll[i].state == (byte)ContractState.Active) activeList.Add(...);
```

**2. Визуальное отличие Active в `BindContractRow`:**
```csharp
bool isActive = c.state == (byte)ContractState.Active;
if (isActive) row.AddToClassList("contract-row-active");
typeLabel.text = isActive ? $"{typeName} [ВЗЯТ]" : typeName;
rewardLabel.text = isActive ? "" : $"{c.reward:F0} CR";  // reward не показываем
```

**3. CSS** (`MarketWindow.uss`):
```css
.contract-row-active {
    background-color: rgba(80, 200, 100, 0.25);
    border-left-width: 3px;
    border-left-color: rgb(80, 220, 100);
}
.contract-row-just-taken {  /* pulse 1.5с после accept */
    background-color: rgba(120, 255, 140, 0.5);
    border-left-width: 3px;
    border-left-color: rgb(160, 255, 180);
    transition-property: background-color, border-left-color;
    transition-duration: 1.5s;
}
```

**4. Optimistic update + pulse** (`OnAcceptContractClicked`):
```csharp
// Сразу после нажатия ВЗЯТЬ:
var cLocal = c;
cLocal.state = (byte)ContractState.Active;
_contractsCache[_selectedContractItem] = cLocal;
_contractsList?.Rebuild();  // мгновенная перерисовка
StartCoroutine(JustTakenPulse(_selectedContractItem));
// Потом — RPC
contractState.RequestAccept(c.contractId);

private IEnumerator JustTakenPulse(int rowIndex) {
    yield return null;  // ждём frame — Rebuild() асинхронен
    var row = _contractsList.ElementAt(rowIndex) as VisualElement;
    if (row == null) yield break;
    row.AddToClassList("contract-row-just-taken");
    yield return new WaitForSeconds(1.6f);
    if (row != null) row.RemoveFromClassList("contract-row-just-taken");
}
```

**Smoke test:** "контракты работают" (подтверждено юзером). Строка зеленеет мгновенно, через ~1.5с pulse гаснет, остаётся зелёная active-подсветка.

### Открыто (после C2-этапа)

| # | Issue | Приоритет | Блокер? |
|---|-------|-----------|---------|
| 17 | Auto-complete при входе в toLocationId (TODO) | Low | No |
| 18 | Receipt контракт cargo (TODO в `ContractWorld.TryAccept:357`) | Medium | No (v1 был тот же) |
| 19 | HUD кредитов за пределами `MarketWindow` (`HUDManager.cs` не подписан) | Low | No (out of scope) |
| 20 | C1-cleanup: удалить 16 legacy файлов (`ContractSystem`, `ContractBoardUI`, `ContractData`, `ContractTrigger`, `PlayerTradeStorage`, `TradeMarketServer`, `TradeUI`, `PlayerDataStore`, `LocationMarket`, `MarketItem`, `MarketEvent` старый, `NPCTrader` старый, `AutoTradeZone`, `TradeSetup`, `TradeSceneSetup`, `TradeDebugTest`, `TradeDebugTools`) | Medium | No |
| 21 | C5-cleanup: удалить 6 legacy RPC в `NetworkPlayer.cs:725-815` (`ContractRequestServerRpc`/`ContractAcceptServerRpc`/`ContractCompleteServerRpc`/`ContractFailServerRpc`/`ContractListClientRpc`/`ContractResultClientRpc`) | Medium | No |
| 22 | C3-cleanup: 4 legacy `Market_*.asset` (старые `LocationMarket` SO) в `Assets/_Project/Trade/Data/Markets/` | Low | No |
| 23 | C7-cleanup: `ProjectC_1.unity` (тестовая сцена) | Low | No |
| 24 | C8-cleanup: 3 файла `_Test*.uss` | Low | No |

**См. также:** [KNOWN_ISSUES.md §4 RESOLVED](KNOWN_ISSUES.md#4-open-resolved-2026-06-05-контракты-не-мигрированы-на-v2-архитектуру--контракты-как-3-й-таб-рынка).

---

## 2026-06-18 — R3: разделение количества рынка и склада/трюма

### Симптом
На вкладке «СКЛАД / ТРЮМ» поля количества не было, использовалось значение с вкладки «РЫНОК» — неудобно (погрузка/разгрузка зависела от последнего введённого рыночного количества).

### Фикс (3 файла)
1. **`MarketWindow.uxml`** — добавлен `<ui:VisualElement name="warehouse-qty-row">` с `<ui:TextField name="warehouse-qty-field">` после списка груза (cargo-section). По умолчанию `display: none`.
2. **`MarketWindow.uss`** — новый `<TextField>` использует существующий класс `.qty-field` (чёрный шрифт на светлом фоне, 80px ширина).
3. **`MarketWindow.cs`**:
   - Добавлено поле `_warehouseQtyField` и запрос в `EnsureBuilt`
   - Добавлен метод `ParseWarehouseQty()` (аналог `ParseQty()` для склада)
   - `OnLoadClicked()` / `OnUnloadClicked()` переведены на `ParseWarehouseQty()`
   - `SwitchTab` показывает `warehouse-qty-row` только когда `tab == "warehouse"`

### Проверка
- Play Mode → открыть рынок → вкладка «РЫНОК» — поле кол-ва видно
- Переключиться на «СКЛАД / ТРЮМ» — своё поле кол-ва между списками и кнопками ПОГРУЗИТЬ/РАЗГРУЗИТЬ
- Значения полей независимы

---

## 2026-06-18 — R3-верстка: перемещение qty-row + круговые кнопки ±

### Что изменилось

**UXML (`MarketWindow.uxml`)**
- `qty-row` (рынок) перенесён из позиции между вкладками и списком товаров **вниз**, между списком товаров (`item-section`) и блоком кнопок действий — именно там, где нужно (над кнопками КУПИТЬ/ПРОДАТЬ).
- В обе qty-строки (рынок + склад) добавлены **6 элементов**: 4 круглых кнопки `[-10] [-1] [+1] [+10]` по бокам от текстового поля.

**USS (`MarketWindow.uss`)**
- `.qty-field`: ширина уменьшена с `80px` → `50px`, высота `22px`, шрифт `12px`, отцентрирован по центру, убран бордер, скругление `3px`.
- `.qty-btn`: круглые кнопки `22×22px`, `border-radius: 11px` (идеальный круг), шрифт `10px`, жирный. Hover → scale 1.15, Active → scale 0.9.
- `.qty-btn-minus`: красноватый фон `rgba(220,160,160,0.85)`.
- `.qty-btn-plus`: зеленоватый фон `rgba(160,220,160,0.85)`.
- `.qty-label`: шрифт `11px`, bold, без фиксированной ширины (центрируется через flex).

**C# (`MarketWindow.cs`)**
- Добавлены 8 полей `_marketQtyMinus10 / _marketQtyMinus1 / _marketQtyPlus1 / _marketQtyPlus10` (и аналоги для warehouse).
- Запросы в `EnsureBuilt` через `_root.Q<Button>()`.
- Клики забинжены на статический `AdjustQty(TextField, delta)` — читает текущее значение, добавляет delta, зажимает в `[1, 9999]`, записывает обратно.

### Проверка
- Play Mode → открыть рынок → вкладка «РЫНОК»: ❶ список товаров ❷ qty-row ([−10] [−1] Кол-во: [__] [+1] [+10]) ❸ кнопки КУПИТЬ/ПРОДАТЬ
- Вкладка «СКЛАД / ТРЮМ»: ❶ склад ❷ груз корабля ❸ qty-row ([−10] [−1] Кол-во: [__] [+1] [+10]) ❹ выбор корабля ❺ ПОГРУЗИТЬ/РАЗГРУЗИТЬ
- Клик по ± кнопкам меняет число в поле (не уходит ниже 1, не выше 9999)
- Два поля количества независимы друг от друга
