# Cargo System — что осталось доделать (сводный план, 2026-07-02)

> **Автор:** Mavis (Mavis)
> **Назначение:** сводный план оставшейся работы по cargo-подсистеме после T-CARGO-01..06.
> **Связано с:** [CARGO_DIAGNOSIS_2026-06-17.md](CARGO_DIAGNOSIS_2026-06-17.md) (диагноз), [CARGO_REFACTOR_PLAN_2026-06-17.md](CARGO_REFACTOR_PLAN_2026-06-17.md) (что уже сделано)
> **Статус:** 📋 План на согласование пользователем. **Делать НЕ начинаю** — нужна явная команда «погнали» по конкретному эпику.

---

## TL;DR

Серверная cargo-логика (Trade v2 + T-CARGO-06) **готова и протестирована**: CargoData POCO, TradeWorld._cargoCache, IPlayerDataRepository, NetworkVariable<float> cargoPenalty, ShipCollisionDamageConfig, OnCargoChanged event, ShipTelemetryState.cargoUsed/cargoMax — всё это **уже работает**.

**Что осталось — 4 эпика UI/визуала/расширения:**

1. **T-CARGO-UI-01: детальный список груза игрока** ✅ **СДЕЛАНО 2026-07-02** — в CharacterWindow, таб «Корабль»: `CargoDetailDto[]` в `ShipTelemetryState`, push через NetworkVariable (5 Hz), рендер `RenderCargoDetail()` в `MyShipsTab`, фикс `cargoMax=0`, 2-колоночная вёрстка (cargo + модули). См. [CARGO_UI_01_DESIGN_2026-07-02.md](CARGO_UI_01_DESIGN_2026-07-02.md).
2. **T-CARGO-UI-02: cargo manager (Exchanger-стиль консоль) на корабле** ✅ **СДЕЛАНО 2026-07-03** — UI-окно для просмотра/правки cargo в любой момент (без рынка), по аналогии с ResourcesExchanger. Включает: ShipCargoConsoleWindow (UI Toolkit), ShipCargoServer (RPC-хаб), обменный курс через ResourceExchangeResolver, qty-кнопки min/-10/-1/+1/+10/max.
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
| `MyShipsTab` (CharacterWindow таб «Корабль») — детальный список + 2-колонки | ✅ T-CARGO-UI-01 | `Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` |
| `NpcShipCargoManifest` (hook, пустой) | ⏳ M1-пустышка | `Scripts/PeacefulShip/Core/NpcShipCargoManifest.cs` |
| `ExchangerTab` (4-я вкладка MarketWindow) | ✅ | `Trade/Scripts/Client/MarketWindow.cs` + `Trade/Exchange/*` |

**Итог:** server-side логика и telemetry-проекция cargo — **полностью готовы**. UI-проекция — **частично** (только в зоне рынка). NPC-cargo — **только hook**.

---

## 2. Что НЕ доделано (4 эпика)

### 2.1 T-CARGO-UI-01: детальный список груза игрока в CharacterWindow ✅

**Статус: СДЕЛАНО 2026-07-02** (подробности в [CARGO_UI_01_DESIGN_2026-07-02.md](CARGO_UI_01_DESIGN_2026-07-02.md)).

**Что реализовано:**
- Расширен `ShipTelemetryState` — добавлен `CargoDetailDto[] cargoDetail` (массив до 32 items: itemId, displayName, quantity, unitWeight, flags byte [dangerous/fragile]).
- Push-подход: данные синхронизируются через существующий NetworkVariable (5 Hz), без новых RPC.
- `ShipController.UpdateTelemetryState`: фикс бага `cargoMax=0` (теперь через `ShipCargoRegistry.GetEffectiveLimits`), `cargoUsed = ComputeTotalSlots` (не `Items.Count`).
- `MyShipsTab.RenderCargoDetail()` — рендер списка в ScrollView с строками: name + quantity × weight, warning-цвет для dangerous/fragile.
- 2-колоночная вёрстка (cargo слева, modules справа) с гибкими ScrollView.
- Throttle `ShipTelemetryStateEqualsApprox` расширен для учёта `cargoDetail`.

**Архитектурные решения:** D19-D25 (см. дизайн-док).

**Оценка (факт):** ~2.5 ч. **Блокируется:** ничем.

---

### 2.2 T-CARGO-UI-02: cargo manager — Exchanger-стиль консоль на корабле ✅

**Статус: СДЕЛАНО 2026-07-03.**

**Реализовано:**
- `ShipCargoConsoleWindow` — UI Toolkit окно (полноэкранный backdrop + панель сверху, как CharacterWindow). Левая панель = инвентарь игрока, правая = трюм корабля. Кнопки «→ В трюм» / «← Из трюма». Закрытие: ✕ кнопка + ESC.
- `ShipCargoServer` — NetworkBehaviour (BootstrapScene), принимает RPC от клиента: `RequestStoreToCargoRpc` / `RequestRetrieveFromCargoRpc`.
- **Обменный курс обязателен.** Обе операции идут через `ResourceExchangeResolver` + `ExchangeRateConfig` (DefaultExchangeRate.asset): 100 pickable-слитков = 1 cargo-ящик. Без курса операция отклоняется. Прямой 1:1 перенос исключён.
- **Упаковка (StoreToCargo):** `FindRateForItemName(itemName)` → удалить `rate.inventoryQty × qty` из инвентаря → добавить `rate.warehouseQty × qty` ящиков (`rate.warehouseItemId`) в `CargoData`.
- **Распаковка (RetrieveFromCargo):** `FindRateForWarehouseItem(cargoItemId)` → удалить `rate.warehouseQty × qty` из трюма → добавить `rate.inventoryQty × qty` предметов в инвентарь.
- **Qty-кнопки:** min/-10/-1/лейбл/+1/+10/max для каждой панели (как MarketWindow). Qty = число «паков» (rate-юнитов). MAX = floor(count / rate.qty).
- `TradeWorld.NotifyCargoChanged(shipNetId)` — публичный метод для внешних систем, мутирующих CargoData напрямую. Без него ShipController не обновляет `NetworkVariable<ShipTelemetryState>` и клиент не видит изменений трюма.
- Интерактивный объект `ShipCargoConsole` (MonoBehaviour) на дочернем GO корабля + `SphereCollider` (IsTrigger). `InteractableManager` регистрирует консоли, `NetworkPlayer` вызывает `TryInteractNearestShipCargoConsole()` по клавише F.
- `ShipCargoClientState` — клиентский синглтон приёма результата (OnResultReceived). `ShipCargoResultDto` — DTO результата.
- Курсор: при открытии разблокируется (`CursorLockMode.None`), при закрытии возвращается в Locked (если сеть активна).
- Телеметрия трюма: подписка на `ShipTelemetryClientState.OnShipStateChanged` для мгновенного обновления UI после операций.

**Архитектурное решение:** вместо изобретения нового 1:1 обменника — полный реюз существующего `ResourceExchangeResolver` + `ExchangeRateConfig`. Это предотвращает эксплойт «распаковал 1 ящик на рынке → получил 100 слитков → положил 100 слитков в трюм как 100 ящиков».

**Новые файлы (11):**

| Файл | Назначение |
|------|-----------|
| `Trade/Exchange/Network/ShipCargoServer.cs` | NetworkBehaviour, RPC-хаб (Store/Retrieve) |
| `Trade/Scripts/Client/ShipCargoConsoleWindow.cs` | UI Toolkit окно (канон) |
| `Trade/Scripts/Client/ShipCargoClientState.cs` | Клиентская проекция результата |
| `Trade/Scripts/Dto/ShipCargoResultDto.cs` | DTO результата |
| `Scripts/Ship/Cargo/ShipCargoConsole.cs` | Interactable-компонент на корабле |
| `UI/ShipCargoConsoleWindow.uxml` | UXML разметка (backdrop + 2 панели + qty-строки) |
| `UI/ShipCargoConsoleWindow.uss` | Стили (!important, qty-кнопки) |
| `Trade/Resources/UI/ShipCargoPanelSettings.asset` | PanelSettings (копия MarketPanelSettings) |

**Изменённые файлы (5):**

| Файл | Изменение |
|------|-----------|
| `Trade/Scripts/Core/TradeWorld.cs` | +`NotifyCargoChanged(ulong)` публичный метод |
| `Scripts/Player/InteractableManager.cs` | +`_shipCargoConsoles` список + Register/Unregister/FindNearest |
| `Scripts/Player/NetworkPlayer.cs` | +`TryInteractNearestShipCargoConsole()` в F-цепочке + `ReceiveShipCargoResultTargetRpc` |
| `Scripts/Network/NetworkManagerController.cs` | +`CreateShipCargoClientState()` при коннекте |
| `Scenes/BootstrapScene.unity` | +`[ShipCargoConsoleWindow]` GO (UIDocument + скрипт) + `[ShipCargoServer]` GO (NetworkObject + ExchangeRateConfig) |

**Что осталось настроить вручную:**
- На каждый корабль (префаб) повесить дочерний GO с `ShipCargoConsole` + `SphereCollider` (IsTrigger, радиус ~3м).

**Триггер сессии:** когда пользователь явно скажет «делаем cargo manager на корабле» / «T-CARGO-UI-02» / «открыть груз в полёте».

**Оценка (факт):** ~8-10 ч (4 тикета + отладка). **Блокируется:** ничем. **Зависимости:** T-CARGO-UI-01 желателен, но не блок.

---

### 2.3 T-CARGO-VIS-01: 3D визуал наполнения трюма (ящики/блоки) ✅

**Статус: СДЕЛАНО 2026-07-02.** См. [CARGO_VIS_01_DESIGN_2026-07-02.md](CARGO_VIS_01_DESIGN_2026-07-02.md).

**Что реализовано:**
- `ShipCargoVisual` (MonoBehaviour) — client-side компонент, вешается на дочерний GO корабля.
- Подписка на `ShipTelemetryClientState.OnShipStateChanged` — реагирует на изменения `cargoUsed`.
- Grid-размещение ящиков внутри `BoxCollider` (`_spawnZone`), снизу вверх.
- Object pool: инкрементальное обновление (Δ), без Destroy/Instantiate на каждый tick.
- Массив `_boxPrefabs[]` — случайный выбор визуала на каждый ящик.
- Overflow-индикатор: красный мигающий ящик при `cargoUsed > _maxVisibleBoxes`.
- Ленивая подписка: ждёт `ShipTelemetryClientState.Instance` (NGO инициализацию).

**Новые файлы (1):**
| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Scripts/Ship/Cargo/ShipCargoVisual.cs` | MonoBehaviour: grid-спавн, object pool, overflow |

**Изменённые файлы (0):** Additive-only, существующий код не тронут.

**Что осталось настроить вручную:**
- На каждый корабль добавить дочерний GO `ShipCargoVisual` с `BoxCollider` (IsTrigger) и `ShipCargoVisual` компонентом.
- В инспекторе: перетащить `_spawnZone` (BoxCollider), заполнить `_boxPrefabs[]` (префаб ящика).
- Создать префаб ящика (`Assets/_Project/Prefabs/Cargo/Box_Default.prefab`).

**Известные баги (fixed):**
- ~~shipNetId=0 — NGO не инициализирован на момент Awake~~ → ленивый ре-резолв в `TrySubscribe()`
- ~~Ящики вне коллайдера~~ → убрана двойная `TransformPoint`/`InverseTransformPoint`, используется `_spawnZone.center`/`size` напрямую

**Оценка (факт):** ~3 ч. **Блокируется:** ничем.

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
| **1** | T-CARGO-UI-01 (список items) | ✅ 2026-07-02 | — | «давай UI трюма» |
| **2** | T-CARGO-UI-02 (cargo manager) | ✅ 2026-07-03 | — | «открыть груз в полёте» |
| **3** | T-CARGO-VIS-01 (3D ящики) | ✅ 2026-07-02 | — | «ящики на палубе» |
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
| T-CARGO-UI-02 ✅ | `ShipCargoConsoleWindow.uxml/uss/cs`, `ShipCargoServer.cs`, `ShipCargoResultDto.cs`, `ShipCargoClientState.cs`, `ShipCargoConsole.cs` (interactable) | `TradeWorld.cs` (+NotifyCargoChanged), `InteractableManager.cs`, `NetworkPlayer.cs`, `NetworkManagerController.cs`, `BootstrapScene.unity` |
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
