# Cargo System — что осталось доделать (сводный план, 2026-07-02)

> **Автор:** Mavis (Mavis)
> **Назначение:** сводный план оставшейся работы по cargo-подсистеме после T-CARGO-01..06.
> **Связано с:** [CARGO_DIAGNOSIS_2026-06-17.md](CARGO_DIAGNOSIS_2026-06-17.md) (диагноз), [CARGO_REFACTOR_PLAN_2026-06-17.md](CARGO_REFACTOR_PLAN_2026-06-17.md) (что уже сделано)
> **Статус:** 📋 План на согласование пользователем. **Делать НЕ начинаю** — нужна явная команда «погнали» по конкретному эпику.

---

## TL;DR

Серверная cargo-логика (Trade v2 + T-CARGO-06) **готова и протестирована**: CargoData POCO, TradeWorld._cargoCache, IPlayerDataRepository, NetworkVariable<float> cargoPenalty, ShipCollisionDamageConfig, OnCargoChanged event, ShipTelemetryState.cargoUsed/cargoMax — всё это **уже работает**.

**Что осталось — 4 эпика UI/визуала/расширения:**

1. **T-CARGO-UI-01: детальный список груза игрока** — в CharacterWindow, таб «Корабль» (или «Груз»), показать items[], а не только progress bar. ✅ **СДЕЛАНО 2026-07-02** (см. [CARGO_UI_01_DESIGN_2026-07-02.md](CARGO_UI_01_DESIGN_2026-07-02.md))
2. **T-CARGO-UI-02: cargo manager (Exchanger-стиль консоль) на корабле** — UI-окно для просмотра/правки cargo в любой момент (без рынка), подход по аналогии с ResourcesExchanger (4-я вкладка MarketWindow).
3. **T-CARGO-VIS-01: 3D визуал наполнения трюма** — наполняемость блоками/ящиками на палубе (visual representation).
4. **T-CARGO-NPC-01: универсальная cargo для NPC-кораблей** — расширить NpcShipCargoManifest (сейчас пустой hook) до полноценной системы, чтобы NPC могли реально перевозить товар (тот же TradeWorld, те же API).

Эпики 2-4 — **входы в roadmap на следующие спринты**, готовы к старту по команде.

---

## 1. Что уже работает (по факту кода на 2026-07-02)

| Подсистема | Статус | Где |
|---|---|---|
| `CargoData` POCO (slots/weight/volume/limits) | ✅ | `Trade/Scripts/Core/CargoData.cs` |
| `TradeWorld._cargoCache[shipId]` | ✅ | `Trade/Scripts/Core/TradeWorld.cs:381-395` |
| `TradeWorld.TryLoadToShip / TryUnloadFromShip` | ✅ | `Trade/Scripts/Core/TradeWorld.cs:283-395` |
| `IPlayerDataRepository.SetCargo / GetCargo` | ✅ | `Trade/Scripts/Repository/IPlayerDataRepository.cs:29-30` |
| `ShipClassLimits.Get(cls)` (fallback) | ✅ | `Trade/Scripts/Core/CargoData.cs:157-178` |
| `ShipClassMappingConfig` (Flight→Cargo) | ✅ | `Scripts/Ship/ShipClassMappingConfig.cs` |
| Per-instance лимиты (T-CARGO-06) | ✅ | `Scripts/Ship/ShipCargoRegistry.cs` + поля `ShipController.baseMaxCargo*` |
| Cargo-бонусы модулей | ✅ | `ShipModule.cargoSlotsBonus/...` + `ShipModuleManager.GetCargoXxxBonus()` |
| `TradeWorld.OnCargoChanged` event | ✅ | `Trade/Scripts/Core/TradeWorld.cs:50` |
| `TradeWorld.GetSpeedPenalty` (server) | ✅ | `Trade/Scripts/Core/TradeWorld.cs:436` |
| `TradeWorld.TryDamageCargo` (collisions) | ✅ | `Trade/Scripts/Core/TradeWorld.cs:436` |
| `ShipController._serverCargoPenalty` (NetworkVariable<float>) | ✅ | `Scripts/Player/ShipController.cs:646` |
| `ShipController._telemetryState.cargoUsed/cargoMax` | ✅ | `Scripts/Player/ShipController.cs:711-733` + `Scripts/Ship/Network/ShipTelemetryState.cs:37-38` |
| `ShipCollisionDamageConfig` (leak/fragile params) | ✅ | `Scripts/Ship/ShipCollisionDamageConfig.cs` + asset |
| `ShipController.OnCollisionEnter → TryDamageCargo` | ✅ | `Scripts/Player/ShipController.cs:405-428` |
| `MarketSnapshotDto.cargo + shipCargos[]` (multi-ship) | ✅ | `Trade/Scripts/Dto/MarketSnapshotDto.cs:49,57,144` |
| MarketWindow показ cargo (в зоне рынка) | ✅ | `Trade/Scripts/Client/MarketWindow.cs` |
| `MyShipsTab` (CharacterWindow таб «Корабль») — заглушка с progress bar | ⚠️ плейсхолдер | `Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` |
| `NpcShipCargoManifest` (hook, пустой) | ⏳ M1-пустышка | `Scripts/PeacefulShip/Core/NpcShipCargoManifest.cs` |
| `ExchangerTab` (4-я вкладка MarketWindow) | ✅ | `Trade/Scripts/Client/MarketWindow.cs` + `Trade/Exchange/*` |

**Итог:** server-side логика и telemetry-проекция cargo — **полностью готовы**. UI-проекция — **частично** (только в зоне рынка). NPC-cargo — **только hook**.

---

## 2. Что НЕ доделано (4 эпика)

### 2.1 T-CARGO-UI-01: детальный список груза игрока в CharacterWindow

**Проблема.** `MyShipsTab` сейчас показывает только progress bar (`cargoUsed / cargoMax`) через `ShipTelemetryState`. Самого списка items **нет** — игрок видит «Груз: 6/10», но не знает что именно лежит.

**Что нужно (без деталей реализации — отсечка).**
- В `MyShipsTab` (CharacterWindow, таб «Корабль») добавить **список содержимого трюма**: itemIcon + displayName + quantity, по строкам, под progress bar.
- Источник: расширить `ShipTelemetryState` (или сделать отдельный `ShipCargoDetailState` NetworkVariable) чтобы синхронизировать не только counts, но и массив `(itemId, displayName, quantity)`.
- Альтернатива: не плодить NetworkVariable, а добавить RPC `RequestShipCargoRpc(shipNetworkObjectId) → ShipCargoDetailDto` в существующий серверный hub (по аналогии с MarketServer). **Нужно решение пользователя: push (NetworkVariable) vs pull (RPC).**
- UI-паттерн: реюз `InventoryTab` row-template (icon + name + type + qty) из `Scripts/UI/Client/CharacterWindow/InventoryTab.cs` — не изобретать новый layout.

**Триггер сессии:** когда пользователь явно скажет «делаем T-CARGO-UI-01» / «давай UI трюма» / т.п.

**Оценка:** ~2-3 ч (1-2 тикета). **Блокируется:** ничем. **Зависимости:** `ShipTelemetryState` уже синхронизирует cargo, остаётся только расширить payload.

---

### 2.2 T-CARGO-UI-02: cargo manager — Exchanger-стиль консоль на корабле

**Проблема.** `MarketWindow` позволяет грузить/разгружать cargo **только в зоне рынка** (`MarketZone` радиус + RPC хаб). Если игрок хочет переложить вещи из инвентаря в трюм посреди полёта — **негде**. Exchanger (4-я вкладка MarketWindow) — единственный «standalone» UI для pack/unpack, но он тоже **в окне рынка**.

**Что нужно (отсечка).**
- Отдельное UI-окно `ShipCargoConsoleWindow` (UI Toolkit, по паттерну ExchangerTab): левая панель = инвентарь игрока, правая = cargo корабля, кнопки «[ → В трюм ]» / «[ ← Из трюма ]» + «[ Упаковать ]» / «[ Распаковать ]» (reюз `ExchangeServer`).
- Вызов: либо новая кнопка в MyShipsTab «[ Открыть консоль груза ]», либо шорт-кат, **либо интерактивный объект** на палубе корабля (как CraftingStation).
- Серверная сторона: переиспользуем `TradeWorld.TryLoadToShip / TryUnloadFromShip` + `ExchangeServer.RequestPack/UnpackRpc` — **новой серверной логики почти не нужно**, только клиентский UI + пере-выборка владельца корабля.
- Требование пользователя: «exchanger в маркете наш» — то есть паттерн (4-я вкладка) подтверждён как образец. UI по аналогии, не новый дизайн.

**Триггер сессии:** когда пользователь явно скажет «делаем cargo manager на корабле» / «T-CARGO-UI-02» / «открыть груз в полёте».

**Оценка:** ~4-6 ч (2-3 тикета). **Блокируется:** ничем. **Зависимости:** T-CARGO-UI-01 желателен (чтобы было что показывать в деталях), но не блок.

---

### 2.3 T-CARGO-VIS-01: 3D визуал наполнения трюма (ящики/блоки)

**Проблема.** Cargo — это «голые данные» в `TradeWorld._cargoCache`. На палубе корабля **не отображается** ничего: ни ящиков, ни слотов, ни индикации перегруза. Игрок видит ship с пустой палубой, а в cargo может лежать 5 т руды.

**Что нужно (отсечка).**
- Новый компонент `ShipCargoVisual` (MonoBehaviour), цепляется на `ShipController`/`ShipRoot`.
- На каждую запись `(itemId, quantity)` в `TradeWorld._cargoCache[shipId]` — спавнить префаб «ящик/бочка/контейнер» на `Transform[]` (пул точек привязки на палубе, настраивается в инспекторе префаба корабля).
- Префабы: per-itemId (через `ItemData.visualPrefab`?) или общий «crate/canister» с цветом/иконкой itemId.
- Реактивность: подписка на `TradeWorld.OnCargoChanged` → пересчитать количество визуальных ящиков на палубе (incr/decr, не пересоздавать всё).
- Скрытие/показ: при посадке игрока (PilotSeat) — видно, при виде от 3-го лица — видно, при непилотном корабле — **тоже видно** (для UX NPC-кораблей и для подсветки «у этого корабля есть cargo»).
- Лимит: если `quantity > capacity` — показать overflow-индикатор (красный мигающий ящик поверх стопки).

**Триггер сессии:** когда пользователь явно скажет «давай визуал cargo» / «ящики на палубе» / «T-CARGO-VIS-01».

**Оценка:** ~4-6 ч (2-3 тикета). **Блокируется:** T-CARGO-NPC-01 частично (если хотим чтобы NPC cargo тоже визуализировался — нужна универсальная точка подписки). **Зависимости:** `ItemData.visualPrefab` уже есть (см. memory: «ItemData SO fields: ... visualPrefab»).

---

### 2.4 T-CARGO-NPC-01: универсальная cargo для NPC-кораблей

**Проблема.** `NpcShipCargoManifest` (PeacefulShip/Core) — **пустой hook**: `capacitySlots=0, capacityWeight=0, items=null`. NPC-корабли в `WorldScene_0_0` летают, швартуются, но **реального груза не возят**. GDD задумывал «NPC traders» — корабли, привозящие товар от одного рынка к другому. Сейчас это невозможно.

**Требование пользователя (явное):** «мы используем npc корабли такие же как игрока, чтобы они тоже потом могли обладать cargo system (именно перевозить груз)».

**Что нужно (отсечка).**
- Использовать **тот же `TradeWorld`** как источник истины для cargo NPC-кораблей. Не параллельный `NpcCargoStore` — единое хранилище, как у игрока. `TradeWorld._cargoCache[shipId]` уже ключуется по `NetworkObjectId` — NPC-корабль тоже NetworkObject, значит **работает из коробки** (только `InvalidateCargo` уже вызывается на despawn — но NPC-корабли не despawn, они persistent в `WorldScene_0_0`).
- В `NpcShipController` / `NpcShipState.Cargo` — хранить `NpcShipCargoManifest` как **projection/replica** (для UI и OnCargoChanged-эффектов), но источник истины = `TradeWorld._cargoCache[npcShipNetworkObjectId]`.
- FSM-фаза `Loading` (v2) → `NpcShipWorld.TickLoading` → дёргать `TradeWorld.TryLoadToShip(clientId=server, locationId, itemId, qty, npcShipNetworkObjectId, shipClass)` (нужен server-only `TryLoadToShipServer` без ownership check — или новый публичный API `TryLoadNpcCargo(npcShipId, ...)`).
- `NpcShipRoute.demandCategory` уже есть (`ProjectC.PeacefulShip.Core.NpcShipDemandCategory`); `NpcShipWorld` подбирает товар по категории и грузит в NPC-cargo перед вылетом.
- DTO: `NpcShipSnapshotDto` расширить `NpcShipCargoManifest` (уже сериализуется), сервер шлёт с каждым snapshot.
- Покупка/разгрузка: NPC при стыковке на станции может «продать» cargo на склад этой станции (через существующий `TradeWorld.TrySell` с `clientId=server` или специальный `TradeWorld.TryNpcSell(npcShipId, ...)`). **Решить: ownership NPC = сервер, поэтому покупатель cargo = какой `clientId`?** Варианты: (a) `clientId=0` (server), (b) новый `npcOwnerId` парам, (c) `clientId=station.locationId` (привязка к локации). **Открытый вопрос — обсудить с пользователем при старте эпика.**
- 3D-визуал: T-CARGO-VIS-01 уже подходит для NPC, если подписка на `TradeWorld.OnCargoChanged` универсальная.

**Триггер сессии:** когда пользователь явно скажет «давай NPC cargo» / «T-CARGO-NPC-01» / «NPC-трейдеры возят товар».

**Оценка:** ~6-10 ч (3-5 тикетов). **Блокируется:** ничем. **Зависимости:** желательно делать **после** T-CARGO-VIS-01 (визуализация тогда покроет и NPC автоматически).

---

## 3. Сводный roadmap (предлагаемый порядок)

| # | Epic | ~Часы | Блоки | Триггер от юзера |
|---|---|---|---|---|
| **0** | (текущее) ничего | — | — | — |
| **1** | T-CARGO-UI-01 (список items) | 2-3 | — | «давай UI трюма» |
| **2** | T-CARGO-UI-02 (cargo manager) | 4-6 | — | «открыть груз в полёте» |
| **3** | T-CARGO-VIS-01 (3D ящики) | 4-6 | — | «ящики на палубе» |
| **4** | T-CARGO-NPC-01 (NPC cargo) | 6-10 | частично #3 | «NPC трейдеры» |

**Альтернативный порядок** (если пользователь хочет сначала NPC):
1. T-CARGO-NPC-01 (базовый, без 3D визуала) → 2. T-CARGO-VIS-01 (покрывает и player, и NPC) → 3. T-CARGO-UI-01 → 4. T-CARGO-UI-02.

**Принцип: каждый эпик = отдельная сессия** (по правилу AGENTS.md «1-2 тикета за сессию»). Начинаю только по явной команде.

---

## 4. Архитектурные принципы (фиксирую)

| # | Решение | Обоснование |
|---|---|---|
| **D14** | **Cargo — единая подсистема Trade**, не разделяем на Player-cargo и NPC-cargo | `TradeWorld._cargoCache[shipId]` уже работает по `NetworkObjectId` — NPC-корабли такие же `NetworkObject`, не нужны параллельные хранилища |
| **D15** | **3D-визуал подписывается на `TradeWorld.OnCargoChanged`** (event), не на свой собственный state | Один источник правды. Любой мутатор cargo (рынок, exchanger, столкновение, NPC trader) автоматически триггерит визуал |
| **D16** | **Cargo manager UI = реюз Exchanger-паттерна** (4-я вкладка, left/right панели, Pack/Unpack кнопки) | Пользователь явно: «exchanger в маркете наш». Не новый UI design |
| **D17** | **MyShipsTab получает детальные items** (T-CARGO-UI-01), **НЕ создаём** отдельный `ShipCargoTab` | Соответствует правилу «таб в CharacterWindow, не новое окно» (AGENTS.md) |
| **D18** | **NPC-cargo = `NpcShipCargoManifest` как projection** от `TradeWorld._cargoCache[npcShipId]` | DTO уже INetworkSerializable — менять структуру не нужно, только заполнять |

**Открытые вопросы (для обсуждения на старте эпика, не сейчас):**
- **Q6 (T-CARGO-UI-01):** push (расширить `ShipTelemetryState` массивом items) vs pull (новый RPC `RequestShipCargoDetailRpc`)? Tradeoff: bandwidth vs latency на 100+ items.
- **Q7 (T-CARGO-UI-02):** вызов через кнопку в MyShipsTab vs интерактивный объект на палубе vs шорт-кат. **Решит пользователь.**
- **Q8 (T-CARGO-NPC-01):** `clientId` для NPC-trader операций Buy/Sell. 3 варианта (см. §2.4). **Решит пользователь.**

---

## 5. Файлы, которые будут затронуты (без кода)

| Epic | Новые | Изменяемые |
|---|---|---|
| T-CARGO-UI-01 | — | `MyShipsTab.cs`, `ShipTelemetryState.cs` (или новый RPC) |
| T-CARGO-UI-02 | `ShipCargoConsoleWindow.uxml/uss/cs` | `CharacterWindow.cs` (или `MyShipsTab.cs`), возможно новый `ShipCargoServer.cs` (reюз `ExchangeServer`?) |
| T-CARGO-VIS-01 | `ShipCargoVisual.cs`, префабы ящиков | `ShipController.cs` (подписка), `ItemData` (visualPrefab уже есть) |
| T-CARGO-NPC-01 | возможно `NpcCargoService.cs` (server-only) | `NpcShipController.cs`, `NpcShipWorld.cs`, `TradeWorld.cs` (новый API `TryNpcLoad/Unload/Sell`), `NpcShipSnapshotDto.cs` (заполнение) |

---

## 6. Verification чек-лист (общий для всех эпиков)

После каждого эпика — пользователь проверяет в Play Mode:

```bash
# Compile
# Open Unity Editor → Console → 0 errors

# Test recipe (зависит от эпика):
# - Open BootstrapScene
# - Start Host
# - Place 1 player ship + 1 NPC ship в WorldScene_0_0
# - Press Play, пройти в зону рынка, загрузить cargo
# - Проверить [что именно меняется в зависимости от эпика]
```

**НЕ делаю:**
- ❌ Не трогаю `docs/gdd/*` без approval
- ❌ Не создаю `.meta` / `.asmdef` файлы
- ❌ Не коммичу (юзер коммитит сам)
- ❌ Не вызываю `run_tests` / Build через MCP

**Делаю по команде:**
- ✅ Каждый эпик = отдельная сессия, не bundle'ить всё в одну
- ✅ Перед стартом — `git status --short` + `git log --oneline -5` для проверки что предыдущая сессия закоммичена
- ✅ После каждого .cs — `refresh_unity` + `read_console` (verify trio)
- ✅ Документация: править `docs/Ships/cargo_system/CHANGELOG.md` + `docs/MMO_Development_Plan.md` после каждого эпика

---

## 7. Что НЕ входит в эти 4 эпика (явно out of scope)

- ❌ Ownership/security (кто может грузить в чужой корабль) — отдельная задача, упоминалась в T-CARGO open questions
- ❌ NPC trade route (где NPC берёт товар, везёт на какой рынок) — это **уже** в `NpcShipSchedule.demandCategory`, но **логика выбора itemId по категории** ещё не написана (открыто в NPC ships roadmap)
- ❌ Cargo decay / spoilage (GDD_25 Phase 4+)
- ❌ Multi-ship cargo transfer (корабль→корабль без рынка) — обсуждается только если юзер попросит
- ❌ Модернизация item-визуалов (3D модели ящиков) — это `Art/`, не код; эпик T-CARGO-VIS-01 использует заглушечные префабы (cube + colored material)

---

## 8. Следующий шаг

**Жду от пользователя:**

1. Подтверждение плана (общее «ок» / «согласовано» / правки).
2. Решение про порядок эпиков: «1→2→3→4» (UI-first) или «4→3→1→2» (NPC-first)?
3. Какие эпики **точно делаем**, какие — откладываем / отменяем?
4. Когда **стартуем первый эпик** — на этой неделе / в следующей сессии / после какого-то события?

**Без явной команды не начинаю.** По AGENTS.md «1-2 тикета за сессию, останавливаемся после summary».

---

## 9. Связанные документы

- [CARGO_DIAGNOSIS_2026-06-17.md](CARGO_DIAGNOSIS_2026-06-17.md) — что есть
- [CARGO_REFACTOR_PLAN_2026-06-17.md](CARGO_REFACTOR_PLAN_2026-06-17.md) — что сделано
- `docs/Markets/README.md` — контекст Trade v2
- `docs/Markets/Resources_exchanger/` — паттерн для cargo manager (T-CARGO-UI-02)
- `docs/NPC_others_peacfull/pc_ship/03_V2_ARCHITECTURE.md` — NpcShipCargoManifest hook
- `docs/NPC_others_peacfull/pc_ship/05_ROADMAP.md` — M-NS-V2 (cargo+market+autopilot) — параллельный план
- `docs/Character/Character-menu/10_DESIGN.md` — структура таба «Корабль» в CharacterWindow
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` — текущая заглушка
- `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` — payload (cargoUsed/cargoMax)
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — единый источник cargo правды
- `Assets/_Project/Trade/Scripts/Dto/MarketSnapshotDto.cs` — `ShipCargoDto` формат
