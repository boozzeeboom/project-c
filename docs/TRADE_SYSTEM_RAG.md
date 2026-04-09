# 📦 Торговая Система — Итоговая RAG-документация

**Проект:** Project C: The Clouds
**Версия:** 1.0 (Сессия 8F — 10 апреля 2026)
**Ветка:** `qwen-gamestudio-agent-dev`
**Статус:** ✅ Базовая торговля работает, готовится к MMO

---

## 📋 Как пользоваться этим документом

Это **RAG (Retrieval-Augmented Generation)** документ — источник истины для ИИ-агентов и разработчиков.

1. **Архитектура** — как устроена система (слои, потоки данных)
2. **Ключевые файлы** — что где искать
3. **Потоки данных** — как работает покупка/продажа/контракты
4. **Известные проблемы** — что сломается и как чинить
5. **Архитектурные риски** — что нужно рефакторить перед MMO
6. **Формулы** — математика экономики
7. **Сетевая модель** — RPC, синхронизация, валидация

---

## 🏗️ АРХИТЕКТУРА (3 слоя)

```
┌─────────────────────────────────────────────────────────────┐
│                    КЛИЕНТ (Unity Player)                     │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ TradeUI.cs — UI торговли (Canvas, кнопки, текст)      │  │
│  │  - _tradeLocked: bool — защита от двойных RPC         │  │
│  │  - playerStorage: PlayerTradeStorage (с NetworkPlayer)│  │
│  │  - currentMarket: LocationMarket (ScriptableObject)   │  │
│  └──────────────────────┬────────────────────────────────┘  │
│                         │ RPC вызов                         │
│  ┌──────────────────────▼────────────────────────────────┐  │
│  │ NetworkPlayer.cs — RPC прокси                         │  │
│  │  - TradeBuyServerRpc() → TradeMarketServer            │  │
│  │  - TradeResultClientRpc() ← TradeMarketServer         │  │
│  └───────────────────────────────────────────────────────┘  │
└────────────────────────┬────────────────────────────────────┘
                         │ Unity Netcode (UDP)
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                    СЕРВЕР (Authoritative)                    │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ TradeMarketServer.cs — главный менеджер рынка         │  │
│  │  - _markets: Dictionary<string, LocationMarket>       │  │
│  │  - BuyItemServerRpc() / SellItemServerRpc()           │  │
│  │  - MarketTick() каждые 30s-300s                       │  │
│  └──────────────────────┬────────────────────────────────┘  │
│                         │                                   │
│  ┌──────────────────────▼────────────────────────────────┐  │
│  │ PlayerDataStore.cs — единый источник данных игрока    │  │
│  │  - GetCredits(clientId) → общие для всех локаций      │  │
│  │  - GetWarehouse(clientId, locationId) → склад по_loc  │  │
│  │  - Кэш в памяти + PlayerPrefs (P0: заменить на БД)   │  │
│  └──────────────────────┬────────────────────────────────┘  │
│                         │                                   │
│  ┌──────────────────────▼────────────────────────────────┐  │
│  │ PlayerTradeStorage.cs — промежуточный склад           │  │
│  │  - BuyItem/SellItem — локальная логика               │  │
│  │  - LoadToShip/UnloadFromShip — перемещение в трюм    │  │
│  │  - Save() → PlayerDataStore.SetWarehouse()            │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ ContractSystem.cs — контракты НП                     │  │
│  │  - Accept/Complete/Fail контракты                    │  │
│  │  - Проверка груза через CargoSystem                   │  │
│  │  - Награда → PlayerDataStore.ModifyCredits()          │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 📁 КЛЮЧЕВЫЕ ФАЙЛЫ

### Торговая система (Assets/_Project/Trade/Scripts/)

| Файл | Назначение | Строк | Статус |
|------|-----------|-------|--------|
| `TradeMarketServer.cs` | Серверный менеджер рынка, Buy/Sell RPC, Tick | ~1000 | ✅ Работает |
| `TradeUI.cs` | Клиентский UI торговли | ~1200 | ✅ Работает |
| `PlayerDataStore.cs` | Единый источник: кредиты + склады | ~150 | ✅ Работает |
| `PlayerTradeStorage.cs` | Промежуточный склад, Buy/Sell/Load/Unload | ~300 | ✅ Работает |
| `MarketItem.cs` | Данные товара в рынке (price, demand, supply) | ~100 | ✅ Работает |
| `LocationMarket.cs` | ScriptableObject рынка локации | ~100 | ✅ Работает |
| `TradeItemDefinition.cs` | ScriptableObject товара | ~50 | ✅ Работает |
| `TradeDatabase.cs` | Контейнер всех товаров | ~50 | ✅ Работает |
| `CargoSystem.cs` | Груз корабля (вес/объём/слоты) | ~200 | ✅ Работает |
| `ContractSystem.cs` | Система контрактов НП | ~400 | ✅ Работает |
| `ContractBoardUI.cs` | UI доски контрактов | ~200 | ✅ Работает |
| `NPCTrader.cs` | NPC-трейдер (абстракция) | ~100 | ✅ Работает |
| `MarketEvent.cs` | Глобальное событие рынка | ~50 | ✅ Работает |
| `PlayerDebt.cs` | Долги игрока | ~100 | ✅ Работает |
| `TradeTrigger.cs` | Триггер зоны торговли | ~50 | ✅ Работает |
| `ContractTrigger.cs` | Триггер зоны контрактов | ~50 | ✅ Работает |
| `AutoTradeZone.cs` | Авто-открытие торговли | ~50 | ✅ Работает |

### Сетевая часть (Assets/_Project/Scripts/)

| Файл | Назначение | Статус |
|------|-----------|--------|
| `NetworkPlayer.cs` | RPC прокси: TradeBuy/Sell → TradeMarketServer | ✅ Работает |
| `ShipController.cs` | Корабль с CargoSystem | ✅ Работает |

### Документация (docs/)

| Файл | Назначение |
|------|-----------|
| `TRADE_SYSTEM_RAG.md` | ⭐ ЭТОТ ФАЙЛ — источник истины |
| `TRADE_DEBUG_GUIDE.md` | Отладка торговой системы (симптомы → решения) |
| `QWENTRADING8SESSION.md` | План 8 сессий торговли |
| `QWENTRADING8D_SESSION.md` | Итоги сессии 8D (double RPC, price=0) |
| `QWENTRADING8E_SESSION.md` | Итоги сессии 8E (clamp, валидация) |
| `gdd/GDD_22_Economy_Trading.md` | GDD экономики |
| `gdd/GDD_25_Trade_Routes.md` | GDD торговых маршрутов |
| `gdd/GDD_23_Faction_Reputation.md` | GDD репутации фракций |

---

## 🔄 ПОТОКИ ДАННЫХ

### Покупка товара (полный цикл)

```
1. Игрок открывает TradeUI (E в зоне торговли)
   → TradeUI.OpenTrade(market)
   → playerStorage.LoadFromPlayerDataStore(clientId)
   → PlayerDataStore.GetCredits(clientId) → из PlayerPrefs/PD_Credits_{clientId}
   → PlayerDataStore.GetWarehouse(clientId, locationId) → из PlayerPrefs

2. Игрок выбирает товар, нажимает "КУПИТЬ"
   → TradeUI.TryBuyItem() → проверка _tradeLocked
   → TradeUI.BuyItemViaServer(itemId, quantity)
   → NetworkPlayer.TradeBuyServerRpc(itemId, quantity, locationId)
      [RPC: Client → Server, SendTo.Server]

3. Сервер обрабатывает (TradeMarketServer.BuyItemServerRpc):
   а) Валидация: quantity > 0, locationId не пустой
   б) Rate limit (если включён)
   в) Проверка рынка: _markets.ContainsKey(locationId)
   г) Проверка товара: market.GetItem(itemId) != null
   д) Проверка стока: marketItem.availableStock >= quantity
   е) RecalculatePrice() → проверка currentPrice > 0
   ж) Проверка кредитов: PlayerDataStore.GetCredits(clientId) >= totalCost
   з) Проверка лимитов склада: вес, объём, типы

4. Сервер выполняет сделку:
   а) PlayerDataStore.ModifyCredits(clientId, -totalCost)
   б) marketItem.availableStock -= quantity
   в) market.UpdateDemand(itemId, quantity * 0.02f)
   г) playerStorage.warehouse.Add/Update(item, quantity)
   д) playerStorage.Save() → PlayerDataStore.SetWarehouse()

5. Сервер отправляет результат:
   → SendTradeResultToClient(clientId, success, message, newCredits, ...)
   → NetworkPlayer.TradeResultClientRpc(success, message, newCredits, itemId, qty, isPurchase)
      [RPC: Server → Client, SendTo.Owner]

6. Клиент обновляет UI:
   → TradeUI.OnTradeResult()
   → _tradeLocked = false
   → playerStorage.LoadFromPlayerDataStore(clientId) ← загрузка актуальных данных
   → UpdateDisplays() + RenderItems()

7. Сервер обновляет рынок у всех клиентов:
   → SendMarketUpdateClientRpc(locationId, itemIds, prices, stocks, demands, supplies)
   → TradeUI.SendMarketUpdateClientRpc() → парсинг CSV → обновление MarketItem
   → RenderItems()
```

### Сдача контракта (полный цикл)

```
1. Игрок открывает доску контрактов (E у NPC-агента)
   → ContractBoardUI.OpenBoard()
   → Запрос сервера: ContractBoardUI.RequestContractsServerRpc()

2. Игрок принимает контракт
   → ContractBoardUI.AcceptContract(contractId)
   → ContractSystem.AcceptContractServerRpc(contractId, clientId)
   → Сервер: contract.Activate(clientId)
   → Клиент: ContractListClientRpc(contracts)

3. Игрок загружает товар на склад (если контракт "под расписку")
   → ContractSystem.AddContractItem(itemId, quantity)
   → playerStorage.warehouse.Add(contractItem)
   → playerStorage.Save()

4. Игрок летит в целевую локацию

5. Игрок сдаёт контракт
   → ContractBoardUI.SubmitContract(contractId)
   → ContractSystem.CompleteContractServerRpc(contractId, clientId)

6. Сервер проверяет:
   а) Контракт существует и активен
   б) cargoSystem.GetItemQuantity(itemId) >= requiredQuantity
   в) Таймер не истёк

7. Сервер выполняет:
   а) cargoSystem.RemoveCargo(itemId, quantity)
   б) PlayerDataStore.ModifyCredits(clientId, reward)
   в) contract.Complete()
   г) Лог транзакции

8. Клиент получает результат:
   → ContractBoardUI.OnContractResult(success, message, reward)
   → Обновление UI: кредиты из PlayerDataStore.GetCredits(clientId)
```

---

## 🔴 ИЗВЕСТНЫЕ ПРОБЛЕМЫ

### P0 — КРИТИЧНЫЕ (блокер для MMO)

| Проблема | Симптом | Влияние | Решение |
|----------|---------|---------|---------|
| **PlayerPrefs для данных** | Dedicated Server не работает | Данные теряются при рестарте сервера | Заменить на `IPlayerDataRepository` + SQLite/PostgreSQL |
| **FindAnyObjectByType** | Ненадёжно в мультиплеере | TradeUI может найти чужой NetworkPlayer | `PlayerRegistry` — словарь `ulong → NetworkPlayer` |
| **ScriptableObject state** | Рынок сбрасывается при рестарте | availableStock/demand/supply теряются | Разделить MarketConfig (SO) + MarketState (БД) |

### P1 — ВЫСОКИЕ (эксплойты)

| Проблема | Симптом | Влияние | Решение |
|----------|---------|---------|---------|
| **Нет проверки позиции** | Покупка из любой локации | Игрок покупает в дешёвой локации не находясь там | Добавить `player.currentLocationId == locationId` в RPC |
| **Quantity overflow** | `quantity * price` может overflow | Потенциальный эксплойт | Clamp quantity до 9999 |
| **Rate limit отключён** | `maxTradesPerMinute = 0` | DDoS торговой системы | Включить по умолчанию (30/min) |
| **CargoSystem не привязан к игроку** | `FindObjectsByType<ShipController>` берёт первый | Сдача чужого груза | Словарь `clientId → CargoSystem` на сервере |

### P2 — СРЕДНИЕ (техдолг)

| Проблема | Симптом | Влияние | Решение |
|----------|---------|---------|---------|
| **Дублирование FindPlayerNetworkPlayer** | 2+ реализации в разных файлах | Разное поведение, баги | Единый `PlayerRegistry` |
| **Editor-код в runtime** | `#if UNITY_EDITOR` в скриптах | IL2CPP stripping issues | Вынести в `TradeAssetLoader` |
| **TradeUI программный** | 1200 строк `new GameObject()` | Неподдерживаемо | UXML/UI Toolkit |
| **CSV сериализация рынка** | 5 Split на каждый ClientRpc | Хрупко, неэффективно | JSON или binary |

### P3 — НИЗКИЕ (полировка)

| Проблема | Влияние | Когда чинить |
|----------|---------|-------------|
| Audit log в память | Теряется при рестарте | При подключении БД |
| ContractData mutable | Потенциальная потеря изменений | При рефакторинге |
| Legacy Font в TradeUI | Deprecated в Unity 6 | При переходе на UI Toolkit |

---

## 🧮 ФОРМУЛЫ

### Цена товара

```
price = base_price × (1 + demand_factor - supply_factor) × event_multiplier × route_multiplier
```

| Параметр | Диапазон | Описание |
|----------|----------|----------|
| `base_price` | Фиксированная | Из TradeItemDefinition |
| `demand_factor` | 0.0 … 1.5 | Растёт при покупках (+0.02 за единицу) |
| `supply_factor` | 0.0 … 1.5 | Растёт при продажах (+0.02 за единицу) |
| `event_multiplier` | 0.5 … 3.0 | Глобальные события |
| `route_multiplier` | 0.8 … 2.5 | Статус маршрута |

### Влияние игрока на рынок

```
# Покупка N единиц:
demand_factor += N × 0.02

# Продажа N единиц:
supply_factor += N × 0.02

# Максимум: ±1.5
```

### Затухание (каждый тик)

```
demand_factor *= 0.92  # -8% в тик
supply_factor *= 0.92
# Пассивная регенерация стока: +8% к базовому стоку
```

### Награда за контракт

```
reward = base_price × quantity × 0.3 × distance_multiplier × reputation_bonus

distance_multiplier = 1.0 + (distance_km / 100) × 0.5
reputation_bonus = 1.0 + (rep_NP / 100) × 0.2
```

### Долг при провале "под расписку"

```
debt = cargo_value × 1.5
debt_decay = 1% в день
```

### Влияние груза на скорость корабля

```
speed_multiplier = 1.0 - (cargo_weight / max_capacity) × penalty_factor

penalty_factor:
  Лёгкий:    0.05
  Средний:   0.08
  Тяжёлый I: 0.10
  Тяжёлый II: 0.12

# Перегруз (>100%): дополнительный штраф -20% за каждые 10% сверх лимита
```

---

## 🌐 СЕТЕВАЯ МОДЕЛЬ

### RPC вызовы торговли

| RPC | Тип | Откуда → Куда | Что делает |
|-----|-----|---------------|------------|
| `TradeBuyServerRpc` | ServerRpc | Client → Server | Запрос покупки товара |
| `TradeSellServerRpc` | ServerRpc | Client → Server | Запрос продажи товара |
| `TradeResultClientRpc` | ClientRpc | Server → Client (Owner) | Результат сделки |
| `SendMarketUpdateClientRpc` | ClientRpc | Server → All Clients | Обновление цен рынка |
| `RequestContractsServerRpc` | ServerRpc | Client → Server | Запрос списка контрактов |
| `AcceptContractServerRpc` | ServerRpc | Client → Server | Принятие контракта |
| `CompleteContractServerRpc` | ServerRpc | Client → Server | Сдача контракта |
| `ContractListClientRpc` | ClientRpc | Server → Client | Список доступных контрактов |

### Валидация на сервере

Каждый ServerRpc проходит проверки:

**BuyItem:**
1. `quantity > 0` (защита от эксплойта)
2. `locationId` не пустой
3. Rate limit (если включён)
4. Рынок существует
5. Товар существует (`marketItem.item != null`)
6. Сток достаточен
7. `currentPrice > 0` (защита от stale данных)
8. Кредиты достаточны
9. Лимиты склада (вес, объём, типы)

**SellItem:**
1. `quantity > 0`
2. `locationId` не пустой
3. Rate limit
4. Рынок существует
5. Товар существует
6. Товар есть у игрока на складе
7. `currentPrice > 0`

**CompleteContract:**
1. Контракт существует
2. Контракт активен
3. Игрок — владелец контракта
4. Груз достаточен в CargoSystem
5. Таймер не истёк

### Синхронизация данных

| Данные | Частота | Метод | Направление |
|--------|---------|-------|-------------|
| Цены рынка | Каждый тик | ClientRpc | Server → All |
| Результат сделки | По событию | ClientRpc | Server → Owner |
| Контракты | По запросу | ClientRpc | Server → Owner |
| Ввод покупки/продажи | По действию | ServerRpc | Client → Server |

---

## 🗂️ ХРАНЕНИЕ ДАННЫХ

### Текущее (PlayerPrefs) — P0: заменить

```
# Кредиты (ОБЩИЕ для всех локаций)
PD_Credits_{clientId}

# Склад (привязан к локации)
PD_Warehouse_{clientId}_{locationId}
→ JSON: {"items": [{"itemId": "mesium_canister_v01", "quantity": 5}]}
```

### Целевое (БД)

```sql
-- Кредиты
CREATE TABLE player_credits (
    client_id BIGINT PRIMARY KEY,
    credits FLOAT NOT NULL DEFAULT 1000,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Склады
CREATE TABLE player_warehouses (
    client_id BIGINT,
    location_id VARCHAR(50),
    item_id VARCHAR(50),
    quantity INT,
    PRIMARY KEY (client_id, location_id, item_id)
);

-- Рынки (состояние)
CREATE TABLE market_state (
    location_id VARCHAR(50),
    item_id VARCHAR(50),
    demand_factor FLOAT DEFAULT 0,
    supply_factor FLOAT DEFAULT 0,
    available_stock INT,
    PRIMARY KEY (location_id, item_id)
);
```

---

## 🔍 ОТЛАДКА

### Быстрая диагностика

| Симптом | Причина | Раздел в TRADE_DEBUG_GUIDE.md |
|---------|---------|------------------------------|
| `currentPrice = 0` | MarketItem.item reference потерян | PRICE=0 ОШИБКА |
| Покупка x2 за клик | Double RPC | DOUBLE RPC |
| Склад показывает лишнее | Накопилось от двойных покупок | СКЛАД |
| Tick не работает | MarketTick не вызывается | TICK СИСТЕМА |
| RPC не доходит | NetworkPlayer не найден | RPC ЦЕПЬ |
| Рынок пустой | ScriptableObject не загружен | РЫНОК ПУСТ |

### Ключевые логи

```
# Клиент
[TradeUI] Покупка: {item} x{qty}
[TradeUI] OnTradeResult: success={bool}, credits={n}

# Сервер
[TradeMarketServer] DEBUG BUY: item={name}, price={n}, stock={n}
[TradeMarketServer] BUY: newCredits={n}
[TradeMarketServer] MarketTick #{n} | markets={n}
[PDS] GetWarehouse: key={key}, items={n}
[PTS] Save: loc={loc}, items={n}
```

### Инструменты

- `R` в окне торговли — сброс склада (отладка)
- Inspector → TradeMarketServer →右键 → "Вызвать MarketTick вручную"
- `testMode = true` на TradeMarketServer → tickInterval = 30s (вместо 300s)
- `PlayerPrefs.DeleteAll()` в консоли Unity (Play mode) — полный сброс

---

## 📊 СТАТУС СЕССИЙ

| Сессия | Что сделано | Статус |
|--------|-------------|--------|
| 1 | TradeItemDefinition, TradeDatabase | ✅ |
| 2 | CargoSystem (груз корабля) | ✅ |
| 3 | LocationMarket (рынки локаций) | ✅ |
| 4 | TradeUI (интерфейс) | ✅ |
| 5 | Серверная торговля (NGO RPC) | ✅ |
| 6 | Tick-система + NPC-трейдеры | ✅ |
| 7 | ContractSystem (контракты НП) | ✅ |
| 8 | Интеграция + полировка | ✅ |
| 8B | Чистка логов, диагностика | ✅ |
| 8C | Фикс сдачи контрактов из склада | ✅ |
| 8D | Double RPC + price=0 fix | ✅ |
| 8E | Clamp факторов + валидация | ✅ |
| 8F | PlayerDataStore — единый источник | ✅ |
| **9** | **Документация + ревью архитектуры** | **🔄 В процессе** |

---

## 🎯 ПРИОРИТЕТЫ РЕФАКТОРИНГА (из ревью technical-director)

| Приоритет | Задача | Причина | Сессия |
|-----------|--------|---------|--------|
| **P0** | `IPlayerDataRepository` вместо PlayerPrefs | Dedicated Server не работает | 10 |
| **P0** | `PlayerRegistry` вместо FindAnyObjectByType | Ненадёжно в мультиплеере | 10 |
| **P1** | MarketConfig + MarketState разделение | Состояние рынка теряется | 10 |
| **P1** | Валидация позиции в RPC | Эксплойт — покупка из другой локации | 10 |
| **P1** | Clamp quantity + rate limit | DDoS эксплойт | 10 |
| **P2** | Вынести Editor-код в loader | IL2CPP stripping | 11+ |
| **P2** | TradeUI → UXML/UI Toolkit | 1200 строк неподдерживаемого кода | 11+ |
| **P2** | Унифицировать FindPlayerNetworkPlayer | Дублирование | 10 |
| **P3** | Audit log транзакций в файл/БД | Нет истории для расследования читов | 11+ |

---

## 🔗 СВЯЗАННЫЕ ДОКУМЕНТЫ

- [TRADE_DEBUG_GUIDE.md](TRADE_DEBUG_GUIDE.md) — отладка (симптомы → решения)
- [QWENTRADING8SESSION.md](QWENTRADING8SESSION.md) — план 8 сессий
- [NETWORK_ARCHITECTURE.md](NETWORK_ARCHITECTURE.md) — сетевая архитектура
- [gdd/GDD_22_Economy_Trading.md](gdd/GDD_22_Economy_Trading.md) — GDD экономики
- [gdd/GDD_25_Trade_Routes.md](gdd/GDD_25_Trade_Routes.md) — GDD маршрутов
- [gdd/GDD_23_Faction_Reputation.md](gdd/GDD_23_Faction_Reputation.md) — GDD репутации
- [QWEN_CONTEXT.md](QWEN_CONTEXT.md) — стартовый файл проекта

---

**Дата создания:** 10 апреля 2026 г.
**Автор:** Qwen Code (technical-director + network-programmer review)
**Версия:** 1.0
