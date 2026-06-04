# Markets — Fixes History
Хронология багов, диагнозов и фиксов рыночной подсистемы. Текущая версия (2026-06-04) — стабильная: полный цикл BUY/LOAD/UNLOAD/SELL работает.

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
