# GDD-22: Economy & Trading — Project C: The Clouds

**Версия:** 6.0 | **Дата:** 14 июля 2026 г. | **Статус:** ✅ V2 развёрнута + Contract→Quest bridge + ItemRegistry + Resources Exchanger
**Автор:** Малков Леонид Андреевич

---

## 1. Overview

Экономическая система Project C: The Clouds — **живой рынок «Дальнобойщики над облаками»**. Игроки торгуют между городами, выполняют контракты НП, занимаются контрабандой. Рынок динамически реагирует на действия **игроков** и **NPC-трейдеров**.

**Ключевые компоненты:**
- **Валюта:** Кредиты (CR)
- **Ресурсы:** 4+ типов с динамическими ценами
- **Торговля:** NPC-магазины, чёрный рынок
- **Контракты:** НП, мануфактуры, военные, контрабанда
- **Динамическая экономика:** Спрос/предложение, события, time-based decay
- **MarketTimeService:** Ускорение/замедление рынка (отладка/демо)
- **Resources Exchanger:** Конвертация pickable ↔ boxed (T-E01–T-E05)
- **Серверная авторитетность:** Вся экономика — на сервере

**Архитектура (v2, post-C1 cleanup):** Bootstrap + 24 World-сцены, server-authoritative, UI Toolkit.

**Связанные документы:** [GDD_25_Trade_Routes.md](GDD_25_Trade_Routes.md), [docs/Markets/](../Markets/)

---

## 2. Currency System

### Кредиты

| Параметр | Описание |
|----------|----------|
| Название | Кредиты Новой Цивилизации |
| Обозначение | CR |
| Тип | Основная валюта |
| Получение | Контракты, торговля, квесты, контрабанда |
| Расход | Заправка, ремонт, покупка ресурсов, улучшения, налоги |
| Хранение | IPlayerDataRepository (PlayerPrefsRepository / ServerFileRepository) |
| Максимум | [Запланировано] 9,999,999 CR |

### Заработок (design)

| Источник | Диапазон | Описание |
|----------|----------|----------|
| НП-контракт доставки | 50-500 CR | 30% от стоимости груза |
| Свободная торговля | Вариативно | Купи дёшево, продай дорого |
| Контрабанда | 200-2000 CR | Высокий риск, высокая награда |
| Военный контракт | 300-1500 CR | ×2-3 к стандартной цене |
| Квесты гильдий | 100-5000 CR | Зависит от ранга |
| Под расписку | 30% стоимости | Туториал-крючок, первые 2 часа |

---

## 3. Resources

### Базовые ресурсы (design)

| Ресурс | Тип | Редкость | Базовая цена | Вес/ед | Объём/ед |
|--------|-----|----------|-------------|--------|----------|
| **Мезий (канистра)** | Топливо | Обычный | 10 CR | 10 кг | 0.5 м³ |
| **Антигравий (слиток)** | Компонент | Необычный | 50 CR | 5 кг | 0.2 м³ |
| **МНП (контейнер)** | Медикамент | Редкий | 100 CR | 3 кг | 0.3 м³ |
| **Латекс (рулон)** | Технический | Обычный | 5 CR | 8 кг | 1.0 м³ |

### Реализация в коде

- **`TradeItemDefinition`** (ScriptableObject) — один asset на товар: itemId, displayName, icon, basePrice, weight, volume, slots, isDangerous, isFragile, isContraband, requiredFaction
- **`TradeDatabase`** (SO, `TradeItemDatabase.asset`) — список всех товаров, lookup по id/displayName/faction/contraband
- **`ItemRegistry`** (M14, singleton SO) — single source of truth для item IDs, 32 items, `id ↔ ItemData` mapping

---

## 4. Pricing Model — Динамическая Экономика

### Формула цены (реализована в PriceFormula.cs)

```
price(location, item) = base_price(item)
    × (1 + demand_factor - supply_factor)
    × reputation_discount
    × event_multiplier
    × route_multiplier
```

| Множитель | Диапазон | Описание |
|-----------|----------|----------|
| `base_price` | Фиксированная | Из TradeItemDefinition.basePrice |
| `demand_factor` | 0.0 … +1.5 | Растёт при покупках, затухает time-based |
| `supply_factor` | 0.0 … +1.5 | Растёт при продажах, затухает time-based |
| `reputation_discount` | 0.7 … 1.3 | Скидка/наценка от репутации (через Quests.FactionId) |
| `event_multiplier` | 0.5 … 3.0 | Глобальные события |
| `route_multiplier` | 0.8 … 2.5 | Статус маршрута |

### Price clamping: `min(base × 0.5, base × 5.0)`

### Time-based decay (v4.0)

```
factor(t) = factor(t0) × exp(-ln(2) × dt / halfLifeSeconds)
```

- half-life по умолчанию: 1800 секунд (30 минут)
- При `marketTimeMultiplier` > 1.0: частота тиков растёт, но decay идёт с той же скоростью по real-time

### MarketTimeService

| Параметр | Default | Диапазон |
|----------|---------|----------|
| baseTickIntervalSeconds | 300 | 1..3600 |
| marketTimeMultiplier | 1.0 | 0.1..100 |
| useWeatherFactor | false | bool |

### Влияние игроков на рынок

```
Покупка N единиц → demand_factor += N × 0.02
Продажа N единиц → supply_factor += N × 0.02
Максимум: ±1.5
Максимальная цена: ×5 от базовой (cap)
```

---

## 5. Trading System

### 5.1 Архитектура Trade v2 (post-C1 cleanup, -27913 LOC)

```
┌─────────────────────────────────────────────────────────────┐
│  КЛИЕНТ (per-client MonoBehaviour + UI Toolkit)             │
│  MarketWindow (UIDocument, UI Toolkit)                      │
│    • 4 таба: РЫНОК / СКЛАД+ТРЮМ / КОНТРАКТЫ / ОБМЕННИК    │
│    • читает MarketClientState.CurrentSnapshot               │
│    • RequestBuy/Sell/Load/Unload через MarketClientState    │
│                                                             │
│  MarketClientState (singleton, DontDestroyOnLoad)           │
│    • последний MarketSnapshotDto + TradeResultDto           │
│    • Per-ship cargo cache: CurrentShipCargos[shipId]        │
│                                                             │
│  ContractClientState (singleton)                            │
│    • последний ContractSnapshotDto + ContractResultDto      │
│                                                             │
│  ExchangeClientState (T-E01)                                │
│    • обменник pickable ↔ boxed                             │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  СЕРВЕР (NetworkBehaviour + server-only state)              │
│                                                             │
│  MarketServer (NetworkBehaviour, ×1, DontDestroyOnLoad)     │
│    • RPC: RequestBuy/Sell/Load/Unload/Subscribe             │
│    • Rate limit (maxOpsPerMinute, default 30)               │
│    • Position validation через MarketZoneRegistry           │
│    • Делегирует в TradeWorld.TryXxx()                       │
│                                                             │
│  ContractServer (NetworkBehaviour, ×1, BootstrapScene)      │
│    • RPC: RequestList/Accept/Complete/Fail/Available        │
│    • Zone validation через MarketZoneRegistry               │
│    • FixedUpdate: ContractWorld.Tick()                      │
│                                                             │
│  TradeWorld (POCO singleton, server-only)                   │
│    • _markets: Dictionary<locationId, MarketState>          │
│    • _npcTraders: List<MarketTrader>                        │
│    • _activeEvents: List<MarketEvent>                       │
│    • _cargoCache: Dictionary<shipId, CargoData>             │
│    • TryBuy / TrySell / TryLoadToShip / TryUnloadFromShip   │
│    • MarketTick(dtSeconds) — NPC, events, decay             │
│    • OnCargoChanged event                                   │
│                                                             │
│  ContractWorld (POCO singleton, server-only)                │
│    • _availableContracts / _playerContracts / _playerDebts  │
│    • TryAccept / TryComplete / TryFail                      │
│    • GenerateContractsForLocation()                         │
│                                                             │
│  MarketTimeService (MonoBehaviour, BootstrapScene)          │
│    • Tick timer → TradeWorld.MarketTick(dt)                 │
│    • OnMarketTick → MarketServer.BroadcastSnapshotsToAll()  │
│    • MarketTimeMultiplier (0.1x..100x)                      │
│                                                             │
│  MarketZone (×N, scene-placed в WorldScene_X_Z)             │
│    • SphereCollider для player detection                    │
│    • OverlapSphere для ship detection                       │
│    • _playersInZone / _shipsInZone (HashSet<ulong>)        │
│    • Регистрируется в MarketZoneRegistry по locationId      │
│                                                             │
│  ExchangeWorld (POCO, T-E01..E05)                           │
│    • ResourceExchangeResolver → ExchangeRateConfig SO       │
│    • Pack: 100 pickable → 1 box на склад                    │
│    • Unpack: 1 box → 100 pickable в инвентарь               │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 Multi-ship trading (v4.0)

- `MarketZone.tradeRadius` (5m) — вход в рынок
- `MarketZone.shipDockRadius` (30m) — корабли «у причала»
- Dropdown выбора корабля в UI (если 2+ корабля)
- Buy/Sell работают без корабля (на склад)

### 5.3 ScriptableObject (v2)

- **`MarketConfig`** (read-only SO) — один на локацию: items[], initialEvents[]
- **`TradeItemDefinition`** (SO) — один на товар: itemId, basePrice, weight, volume, flags
- **`TradeDatabase`** (SO) — реестр всех TradeItemDefinition
- **`ExchangeRateConfig`** (SO) — курсы обмена pickable↔boxed

### 5.4 Хранение данных

```
IPlayerDataRepository (interface):
  ├─ PlayerPrefsRepository (host, default)
  └─ ServerFileRepository (dedicated server, P1)

PD2_Credits_{clientId}                 — кредиты
PD2_Warehouse_{clientId}_{locationId}  — склад
PD2_Cargo_{shipNetworkObjectId}        — груз корабля
```

---

## 6. Contract System

### 6.1 Типы контрактов

| Тип контракта | Источник | Описание |
|---------------|----------|----------|
| **НП-доставка** | НП (доска) | Взять товар → доставить |
| **Под расписку** | НП (первые 2ч) | Получить товар бесплатно → доставить → 30% |
| **Мануфактура** | Агент мануфактуры | Эксклюзивная доставка |
| **Чёрный рынок** | Свободные торговцы | Контрабанда |
| **Военный** | Военный анклав | Доставка оружия/сопровождение |

### 6.2 ContractData

```
ContractData {
    contractId: string (генерируется)
    type: ContractType
    fromLocation: string
    toLocation: string
    item: TradeItemDefinition
    quantity: int
    baseReward: float
    debt: float (для «под расписку»)
    timeLimit: float (сек)
    state: ContractState (Available, Active, Completed, Failed)
    acceptedAt: float (timestamp)
}
```

### 6.3 Contract → Quest bridge

**ContractMetaBridge** (server-side singleton):
1. ContractServer публикует events в WorldEventBus
2. ContractMetaBridge подписан → QuestWorld.MarkContractAccepted/Completed/Failed
3. QuestTriggerService.Evaluate($"ContractCompleted:{contractId}")

---

## 7. Resources Exchanger (T-E01–T-E05)

### 7.1 Проблема

Две независимые системы предметов — pickable (инвентарь, int id, 1 кг) и boxed (склад, string id, 100 кг). Моста между ними не было.

### 7.2 Решение

4-я вкладка «Обменник» в MarketWindow. Pack: 100 pickable → 1 box на склад. Unpack: 1 box → 100 pickable в инвентарь.

### 7.3 Архитектура (Hybrid D)

```
ExchangeRateConfig (SO) → ResourceExchangeResolver (lookup) → ExchangeWorld (POCO, rollback) → ExchangeServer (RPC) → ExchangeClientState + MarketWindow tab
```

### 7.4 Ключевые решения

- **Zero-touch** — ни одна строка в InventoryWorld, TradeWorld, Crafting, Mining, Quests не менялась
- **Config-driven** — новая пара = запись в ExchangeRateConfig
- **MAX_SLOTS = 1000** — временно
- **PushPlayerSnapshot** — после операции зовём и InventoryServer, и MarketServer
- **4 базовых курса:** IronOre, CopperOre, Wood, Antigrav — 100:1

---

## 8. Economy Events

| Событие | Эффект | Длительность | Статус |
|---------|--------|-------------|--------|
| Дефицит мезия | Мезий ×2-3 | 3-5 тиков | ✅ Ручное создание |
| Бум антигравия | Антигравий ×0.5 | 4-6 тиков | ✅ |
| Блокада маршрута | Маршрут закрыт, цены ×2 | 2-4 тика | 🔴 |
| Налоговая проверка | Контрабанда ×2 риск | 1-2 тика | 🔴 |
| Фестиваль | Продовольствие ×0.5 | 2-3 тика | 🔴 |
| Эпидемия | МНП ×3, все ×1.2 | 3-5 тиков | 🔴 |
| Война гильдий | Военные ×2, маршруты блоки | 5-10 тиков | 🔴 |

> Реализована архитектура: `MarketEvent` POCO, `MarketState._activeEvents`, `TradeWorld.MarketTick()` применяет множители. Сами события пока создаются вручную (code/config).

---

## 9. Anti-Exploit Measures

| Мера | Описание | Статус |
|------|----------|--------|
| Серверная авторитетность | Все транзакции — на сервере | ✅ |
| Rate limit | Макс. 30 ops/min | ✅ |
| Position validation | Только в MarketZone | ✅ |
| Максимальная цена | ×5 от базовой | ✅ |
| Логирование | Аномальные транзакции | ✅ |

---

## 10. Формулы

| Формула | Описание |
|---------|----------|
| `price = base × (1 + demand - supply) × rep × event × route` | Цена товара |
| `profit = sell_price × 0.8 - buy_price - tax` | Прибыль (NPC-маржа 20%) |
| `tax = sell_price × 0.05` | Налог 5% |
| `contract_reward = base_price × quantity × 0.3 × distance × rep_bonus` | Награда за контракт |
| `debt = cargo_value × 1.5` | Долг при провале |
| `speed_penalty = 1.0 - (cargo_weight / max_capacity) × penalty_factor` | Влияние груза на скорость |
| `demand_change = quantity × 0.02` | Влияние покупки |
| `supply_change = quantity × 0.02` | Влияние продажи |
| `decay(t) = factor × exp(-ln(2) × dt / halfLife)` | Time-based затухание |
| `clamp price ∈ [base × 0.5, base × 5.0]` | Защита от runaway |
| `tickInterval = baseInterval / marketTimeMultiplier` | Частота тика |

---

## 11. Tuning Knobs

| Параметр | Default | Диапазон | Описание |
|----------|---------|----------|----------|
| base_mezium_price | 10 | 5..20 | Базовая цена мезия |
| base_antigrav_price | 50 | 25..100 | Базовая цена антигравия |
| trade_tax | 0.05 | 0.01..0.15 | Налог на торговлю |
| demand_change_per_unit | 0.02 | 0.005..0.05 | Влияние покупки |
| max_price_multiplier | 5.0 | 3.0..10.0 | Макс. множитель цены |
| debt_multiplier | 1.5 | 1.0..3.0 | Множитель долга |
| contraband_detect_base | 0.15 | 0.05..0.30 | Шанс обнаружения |
| base_tick_interval | 300 | 30..3600 | Базовый интервал тика (сек) |
| market_time_multiplier | 1.0 | 0.1..100 | Множитель скорости |
| demand_decay_half_life | 1800 | 60..86400 | Half-life спроса (сек) |
| use_weather_factor | false | bool | Включить time-of-day |
| npc_trader_count | 8 | 2..20 | Количество NPC-трейдеров |
| max_ops_per_minute | 30 | 0..200 | Rate limit |

---

## 12. Acceptance Criteria

| # | Критерий | Статус |
|---|----------|--------|
| 1 | Кредиты отображаются в UI | ✅ |
| 2 | Рынок каждой локации уникален | ✅ |
| 3 | NPC-торговец открывает магазин | ✅ |
| 4 | Динамические цены работают | ✅ |
| 5 | Покупка/продажа влияет на цены | ✅ |
| 6 | Контракт НП работает | ✅ |
| 7 | Система «под расписку» работает | ✅ |
| 8 | Контрабанда обнаруживается | 🔴 (post-MVP) |
| 9 | Репутация влияет на цены | 🟡 (T-Q15 интеграция) |
| 10 | Груз влияет на скорость | ✅ |
| 11 | P2P торговля между игроками | 🔴 (post-MVP) |
| 12 | Серверная валидация | ✅ |
| 13 | События рынка работают | 🟡 (ручное создание) |
| 14 | Маршрут блокируется | 🔴 (post-MVP) |
| 15 | Time multiplier ускоряет рынок | ✅ (MarketTimeService) |
| 16 | Time-based decay не зависит от частоты тиков | ✅ |
| 17 | Multi-ship UI dropdown | ✅ |
| 18 | Position validation (NotInZone) | ✅ |
| 19 | Ship validation (ShipNotInZone) | ✅ |
| 20 | **ItemRegistry** (single source of truth) | ✅ (M14) |
| 21 | **Contract → Quest bridge** | ✅ (T-X5+T-Q15) |
| 22 | **Resources Exchanger** | ✅ (T-E01–T-E05) |

---

## 13. Файлы (C#)

```
Trade/
├── Exchange/
│   ├── Config/ExchangeRateConfig.cs, ExchangeRateEntry.cs
│   ├── Core/ExchangeResult.cs, ExchangeWorld.cs, ResourceExchangeResolver.cs
│   └── Network/ExchangeServer.cs, ShipCargoServer.cs
├── Scripts/
│   ├── Client/
│   │   ├── ContractClientState.cs
│   │   ├── ExchangeClientState.cs
│   │   ├── MarketClientState.cs
│   │   ├── MarketInteractor.cs
│   │   ├── MarketWindow.cs
│   │   ├── ShipCargoClientState.cs
│   │   └── ShipCargoConsoleWindow.cs
│   ├── Config/MarketConfig.cs, MarketConfigCollector.cs, MarketItemConfig.cs
│   ├── ContractData.cs
│   ├── Core/
│   │   ├── CargoData.cs, ContractDebt.cs, ContractWorld.cs
│   │   ├── ContractWorldItemResolver.cs, DatabaseResolver.cs
│   │   ├── MarketEvent.cs, MarketItemState.cs, MarketState.cs
│   │   ├── MarketTrader.cs, ShipClass.cs
│   │   ├── TradeItemDefinitionResolver.cs, TradeResult.cs
│   │   ├── TradeWorld.cs, Warehouse.cs
│   ├── Dto/ (ContractDto, ContractResultCode, ContractResultDto, ContractSnapshotDto,
│   │        ExchangeResultDto, MarketSnapshotDto, ShipCargoResultDto, ShipSummaryDto,
│   │        TradeResultCode, TradeResultDto)
│   ├── Editor/MarketZoneMigrationTool.cs, TradeAssetGenerator.cs
│   ├── Network/
│   │   ├── ContractServer.cs
│   │   ├── MarketServer.cs
│   │   ├── MarketTimeService.cs
│   │   ├── MarketZone.cs, MarketZoneRegistry.cs
│   ├── Repository/IPlayerDataRepository.cs, PlayerPrefsRepository.cs, ServerFileRepository.cs
│   └── Service/PriceFormula.cs
├── TradeDatabase.cs
└── TradeItemDefinition.cs
```

---

*Документ создан для Project C: The Clouds.*
**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [GDD_23_Faction_Reputation.md](GDD_23_Faction_Reputation.md) | [GDD_25_Trade_Routes.md](GDD_25_Trade_Routes.md) | [`docs/Markets/`](../Markets/) | [`docs/NPC_quests/08_ROADMAP.md`](../NPC_quests/08_ROADMAP.md)
