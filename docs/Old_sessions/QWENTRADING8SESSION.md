# 📦 Система Торговли — План Реализации (8 Сессий)

**Проект:** Project C: The Clouds
**Ветка:** `qwen-gamestudio-agent-dev`
**Статус:** ✅ ЗАВЕРШЕНО (все 8 сессий + 8E фикс кредитов)
**Дата создания:** 6 апреля 2026 г.
**Дата завершения:** 9 апреля 2026 г.

---

## 📐 Как пользоваться этим файлом

1. **Каждая сессия = отдельный запуск.** Скопируй блок «КОМАНДА ДЛЯ ЗАПУСКА» и отправь мне.
2. **Сессии идут строго по порядку** (зависимости указаны в диаграмме).
3. **После каждой сессии:** тест в Unity → коммит → пуш → переход к следующей.
4. **Агенты подключаются автоматически** по протоколу из `.qwenencode/agents/`.
5. **Ничего не кодим без плана** — каждая сессия начинается с прочтения GDD-документов.

---

## 📊 ДИАГРАММА ЗАВИСИМОСТЕЙ

```
Сессия 1: TradeItemDefinition (ScriptableObject товаров)
    ↓
Сессия 2: CargoSystem          Сессия 3: LocationMarket
    ↓                              ↓
         Сессия 4: TradeUI
              ↓
    Сессия 5: Серверная торговля (NGO RPC)
         ↓              ↓
Сессия 6: Tick      Сессия 7: Contracts
         ↓              ↓
    СЕССИЯ 8: ИНТЕГРАЦИЯ + ПОЛИРОВКА
```

**Параллельно можно:** Сессия 2 и 3 | Сессия 6 и 7

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 1: ScriptableObject товаров + структура данных
# ═══════════════════════════════════════════════════════

**Агенты:** `@economy-designer` + `@unity-specialist` + `@game-designer`

**GDD для чтения:**
- `docs/gdd/GDD_25_Trade_Routes.md` — секции 4 (Груз), 8.3 (ScriptableObject)
- `docs/gdd/GDD_22_Economy_Trading.md` — секция 3 (Resources), секция 11 (Тех. архитектура)

**Цель:** Создать систему ScriptableObject-ов для определения всех товаров.

### Задачи:

1. **Создать папки:**
   ```
   Assets/_Project/Trade/Data/
   Assets/_Project/Trade/Data/Items/
   Assets/_Project/Trade/Data/Markets/
   Assets/_Project/Trade/Scripts/
   Assets/_Project/Trade/Icons/
   ```

2. **Создать `TradeItemDefinition.cs`** — ScriptableObject для каждого товара:
   ```
   Поля:
   - string itemId
   - string displayName
   - Sprite icon
   - float basePrice (CR)
   - float weight (кг за единицу)
   - float volume (м³ за единицу)
   - int slots (слотов за единицу)
   - bool isDangerous (мезий — протечка при столкновении)
   - bool isFragile (двигатели, МНП — повреждение при столкновении)
   - bool isContraband (нелегальный товар)
   - Faction requiredFaction (кто может продавать, null = все)
   ```

3. **Создать `TradeDatabase.cs`** — ScriptableObject-контейнер всех товаров:
   ```
   Поля:
   - List<TradeItemDefinition> allItems
   Методы:
   - TradeItemDefinition GetItemById(string id)
   - TradeItemDefinition GetItemByDisplayName(string name)
   - List<TradeItemDefinition> GetItemsByFaction(Faction f)
   - List<TradeItemDefinition> GetContrabandItems()
   ```

4. **Создать первые 2 ScriptableObject-а (для теста):**
   - `TradeItem_Mesium_v01.asset` — Мезий (канистра): 10 CR, 10 кг, 0.5 м³, isDangerous=true
   - `TradeItem_Antigrav_v01.asset` — Антигравий (слиток): 50 CR, 5 кг, 0.2 м³

5. **Создать `TradeItemDatabase.asset`** — база данных, подключить 2 предмета.

### Что НЕ делаем:
- ❌ Не трогаем UI
- ❌ Не трогаем сеть
- ❌ Не трогаем корабль
- ❌ Не создаём все 8 предметов (только 2 для теста)

### Критерий завершения:
- ✅ TradeItemDefinition создаётся через CreateAssetMenu
- ✅ Все поля видны в инспекторе
- ✅ TradeDatabase видит все подключённые предметы
- ✅ GetItemById() возвращает правильный предмет
- ✅ Коммит и пуш

### КОМАНДА ДЛЯ ЗАПУСКА:
```
Начинаем Сессию 1: ScriptableObject товаров. Прочитай docs/gdd/GDD_25_Trade_Routes.md (секции 4 и 8.3) и docs/gdd/GDD_22_Economy_Trading.md (секция 3). Подключи @economy-designer и @unity-specialist. Создай TradeItemDefinition.cs, TradeDatabase.cs, и 2 тестовых ScriptableObject-а (Мезий, Антигравий).
```

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 2: CargoSystem — груз корабля
# ═══════════════════════════════════════════════════════

**Агенты:** `@gameplay-programmer` + `@unity-specialist`

**GDD для чтения:**
- `docs/gdd/GDD_25_Trade_Routes.md` — секции 4 (Груз и корабли), 8.2 (CargoSystem), 9 (Edge Cases), 10 (Формулы)
- `docs/gdd/GDD_22_Economy_Trading.md` — секция 7 (Cargo & Transport)

**Зависит от:** Сессия 1 (TradeItemDefinition)

**Цель:** Добавить систему груза корабля — отдельно от личного инвентаря.

### Задачи:

1. **Создать `CargoSystem.cs`** — компонент корабля (NetworkBehaviour):
   ```
   Поля:
   - List<CargoItem> cargo — список грузов
   - int maxSlots — из ShipController (по классу корабля)
   - float maxWeight — грузоподъёмность
   - float maxVolume — объём трюма

   CargoItem:
   - TradeItemDefinition item
   - int quantity

   Методы:
   - bool AddCargo(TradeItemDefinition item, int quantity)
   - bool RemoveCargo(string itemId, int quantity)
   - float CurrentWeight => cargo.Sum(x => x.weight * x.quantity)
   - float CurrentVolume => cargo.Sum(x => x.volume * x.quantity)
   - int UsedSlots => cargo.Sum(x => x.slots)
   - float GetSpeedPenalty() — формула из GDD_25 секция 4.4
   - bool CheckLeakOnCollision() — 5% шанс для isDangerous
   - bool CheckFragileDamageOnCollision() — 10% шанс для isFragile
   ```

2. **Интегрировать в `ShipController.cs`:**
   - Добавить ссылку на CargoSystem
   - Применять `GetSpeedPenalty()` к тяге корабля
   - Проверять `CheckLeakOnCollision()` при столкновении

3. **Обновить CargoSystem для 4 классов кораблей:**
   ```
   Лёгкий:    4 слота,  100 кг,  3 м³
   Средний:   10 слотов, 500 кг, 12 м³
   Тяжёлый I: 20 слотов, 2000 кг, 40 м³
   Тяжёлый II: 30 слотов, 5000 кг, 80 м³
   ```

4. **Создать тестовый скрипт** (Editor или Runtime):
   - Загрузить 5 канистр мезия → проверить вес
   - Проверить что скорость упала
   - Проверить протечку при столкновении

### Что НЕ делаем:
- ❌ Не трогаем сеть (пока локальный)
- ❌ Не делаем UI
- ❌ Не делаем покупку/продажу

### Критерий завершения:
- ✅ CargoSystem добавлен на префаб корабля
- ✅ AddCargo/RemoveCargo работают
- ✅ GetSpeedPenalty() влияет на скорость (проверить в Unity)
- ✅ CheckLeakOnCollision() — 5% шанс протечки
- ✅ Тестовый скрипт подтверждает расчёты
- ✅ Коммит и пуш

### КОМАНДА ДЛЯ ЗАПУСКА:
```
Начинаем Сессию 2: CargoSystem. Прочитай docs/gdd/GDD_25_Trade_Routes.md (секции 4, 8.2, 9, 10). Подключи @gameplay-programmer и @unity-specialist. Создай CargoSystem.cs, интегрируй в ShipController.cs, добавь тестовый скрипт. Зависит от Сессии 1 (TradeItemDefinition уже существует).
```

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 3: LocationMarket — данные рынка
# ═══════════════════════════════════════════════════════

**Агенты:** `@economy-designer` + `@unity-specialist`

**GDD для чтения:**
- `docs/gdd/GDD_25_Trade_Routes.md` — секции 3 (Маршруты), 5 (Динамическая экономика), 8.1 (TradeMarketServer)
- `docs/gdd/GDD_22_Economy_Trading.md` — секция 4 (Pricing Model), секция 5 (Trading System)

**Зависит от:** Сессия 1 (TradeItemDefinition)

**Цель:** Создать ScriptableObject-ы рынка для каждой локации с динамическими ценами.

### Задачи:

1. **Создать `LocationMarket.cs`** — ScriptableObject рынка локации:
   ```
   Поля:
   - string locationId (primium, secundus, tertius, quartus)
   - string locationName
   - List<MarketItem> items

   Методы:
   - void RecalculatePrices() — формула из GDD_22 секция 4
   - float GetPrice(string itemId)
   - int GetStock(string itemId)
   - void UpdateDemand(string itemId, float delta)
   - void UpdateSupply(string itemId, float delta)
   ```

2. **Создать `MarketItem.cs`** — данные предмета в рынке (Serializable):
   ```
   Поля:
   - TradeItemDefinition item (ссылка)
   - float basePrice (из item)
   - float currentPrice (вычисляется)
   - int availableStock (сколько доступно NPC)
   - float demandFactor (0.0 … 1.5)
   - float supplyFactor (0.0 … 1.5)
   ```

3. **Создать 4 ScriptableObject-а рынков:**
   - `Market_Primium_v01.asset` — столица, мезий дёшево, антигравий дёшево
   - `Market_Secundus_v01.asset` — военная база, мезий средне, броня дёшево
   - `Market_Tertius_v01.asset` — торговый хаб, латекс дёшево, всё средне
   - `Market_Quartus_v01.asset` — научный центр, МНП дёшево, мезий дорого

4. **Заполнить начальными данными:**
   - Мезий: basePrice=10, demand=0, supply=0.5 (Примум), stock=100
   - Антигравий: basePrice=50, demand=0.2, supply=0.3, stock=50

### Что НЕ делаем:
- ❌ Не делаем tick-систему
- ❌ Не делаем NPC-трейдеров
- ❌ Не делаем UI

### Критерий завершения:
- ✅ LocationMarket создаётся через CreateAssetMenu
- ✅ 4 рынка созданы с разными ценами
- ✅ RecalculatePrices() считает правильно по формуле
- ✅ В редакторе видно: мезий в Примум = 10 CR, в Квартус = 15 CR
- ✅ Коммит и пуш

### КОМАНДА ДЛЯ ЗАПУСКА:
```
Начинаем Сессию 3: LocationMarket. Прочитай docs/gdd/GDD_25_Trade_Routes.md (секции 3, 5, 8.1) и docs/gdd/GDD_22_Economy_Trading.md (секция 4). Подключи @economy-designer и @unity-specialist. Создай LocationMarket.cs, MarketItem.cs, и 4 ScriptableObject-а рынков. Зависит от Сессии 1.
```

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 4: TradeUI — интерфейс торговли
# ═══════════════════════════════════════════════════════

**Агенты:** `@ui-programmer` + `@unity-specialist`

**GDD для чтения:**
- `docs/gdd/GDD_25_Trade_Routes.md` — секция 8.2 (клиентская часть, TradeUI)
- `docs/gdd/GDD_13_UI_UX_System.md` — общие правила UI

**Зависит от:** Сессия 2 (CargoSystem), Сессия 3 (LocationMarket)

**Цель:** Создать интерфейс торговли — покупка/продажа товаров у NPC.

### Задачи:

1. **Создать `TradeUI.cs`** — клиентский UI торговли:
   ```
   Поля:
   - LocationMarket currentMarket
   - CanvasGroup panel
   - GameObject tradePanelPrefab

   Методы:
   - void OpenTrade(LocationMarket market) — показать панель
   - void CloseTrade() — скрыть панель
   - void RenderItems() — отобразить список товаров
   - void BuyItem(string itemId, int quantity) — пока локально (без ServerRpc)
   - void SellItem(string itemId, int quantity) — пока локально
   - void UpdateCreditsDisplay() — показать баланс
   ```

2. **Создать Canvas-префаб TradePanel:**
   - Список товаров (текстовый список, пока без иконок)
   - Для каждого товара: название, цена, доступность
   - Поле ввода количества
   - Кнопки «Купить» / «Продать»
   - Отображение: кредиты, текущий груз, вес, объём
   - Кнопка «Закрыть»

3. **Создать `TradeTrigger.cs`** — триггер для открытия торговли:
   ```
   Поля:
   - LocationMarket market
   - string npcName

   Методы:
   - void OnTriggerEnter(Collider other) — игрок вошёл
   - void OnInteract() — игрок нажал E, открыть TradeUI
   ```

4. **Разместить 1 тестовый триггер** в сцене (Примум):
   - Привязать к Market_Primium
   - Подойти → нажать E → открыть TradePanel
   - Купить мезий → проверить что кредиты списались, груз появился

### Что НЕ делаем:
- ❌ Не делаем сеть (ServerRpc) — локальный тест
- ❌ Не делаем анимации
- ❌ Не делаем красивый дизайн (функционал важнее)
- ❌ Не делаем иконки (placeholder — текст)

### Критерий завершения:
- ✅ TradePanel открывается по E в триггер-зоне
- ✅ Список товаров с ценами отображается
- ✅ Кнопка «Купить» → кредиты списались, груз в CargoSystem
- ✅ Кнопка «Продать» → кредиты начислились, груз убран
- ✅ Работает локально (без сети)
- ✅ Коммит и пуш

### КОМАНДА ДЛЯ ЗАПУСКА:
```
Начинаем Сессию 4: TradeUI. Прочитай docs/gdd/GDD_25_Trade_Routes.md (секция 8.2). Подключи @ui-programmer и @unity-specialist. Создай TradeUI.cs, TradePanel Canvas, TradeTrigger.cs. Зависит от Сессии 2 и 3.
```

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 5: Серверная торговля (NGO RPC)
# ═══════════════════════════════════════════════════════

**Агенты:** `@network-programmer` + `@unity-specialist`

**GDD для чтения:**
- `docs/gdd/GDD_25_Trade_Routes.md` — секции 8.1 (TradeMarketServer), 8.2 (клиентская часть), 8.4 (сетевая синхронизация)
- `docs/NETWORK_ARCHITECTURE.md` — текущая сетевая архитектура
- `docs/gdd/GDD_12_Network_Multiplayer.md` — правила NGO

**Зависит от:** Сессия 4 (TradeUI), Сессия 2 (CargoSystem), Сессия 3 (LocationMarket)

**Цель:** Перенести торговлю на сервер (authoritative), добавить NGO RPC.

### Задачи:

1. **Создать `TradeMarketServer.cs`** — серверный менеджер рынка (NetworkBehaviour):
   ```
   Поля:
   - Dictionary<string, LocationMarket> markets

   ServerRpc:
   - BuyItemServerRpc(string itemId, int quantity, string locationId, ulong clientId)
     → Проверить кредиты клиента
     → Проверить слоты/вес груза
     → Списать кредиты
     → Добавить груз
     → Обновить demandFactor
     → Отправить результат клиенту

   - SellItemServerRpc(string itemId, int quantity, string locationId, ulong clientId)
     → Проверить груз клиента
     → Убрать груз
     → Начислить кредиты
     → Обновить supplyFactor
     → Отправить результат клиенту

   ClientRpc:
   - UpdateMarketDataClientRpc(string locationId, MarketDataSnapshot snapshot)
     → Обновить UI клиента
   ```

2. **Обновить `TradeUI.cs`:**
   - Заменить локальные BuyItem/SellItem на ServerRpc вызовы
   - Добавить обработку результата (успех/ошибка)
   - Добавить отображение ошибок (нет кредитов, нет слотов)

3. **Обновить `CargoSystem.cs`:**
   - Сделать NetworkBehaviour (если ещё не)
   - Cargo данные — на сервере, клиент только отображает
   - Добавить синхронизацию груза (NetworkVariable или ClientRpc)

4. **Серверная валидация:**
   - Проверка кредита (сервер хранит баланс игрока)
   - Проверка слотов/веса
   - Логирование всех транзакций
   - Лимит: 10 сделок в минуту

### Что НЕ делаем:
- ❌ Не делаем tick-систему с NPC-трейдерами
- ❌ Не делаем глобальные события
- ❌ Не делаем динамическое изменение цен (только от игроков)

### Критерий завершения:
- ✅ TradeMarketServer принимает ServerRpc вызовы
- ✅ Валидация на сервере: нет кредитов → отказ
- ✅ Host + Client: клиент покупает → сервер обрабатывает → груз у клиента
- ✅ MarketData обновляется у всех клиентов
- ✅ Логирование транзакций
- ✅ Коммит и пуш

### КОМАНДА ДЛЯ ЗАПУСКА:
```
Начинаем Сессию 5: Серверная торговля. Прочитай docs/gdd/GDD_25_Trade_Routes.md (секции 8.1, 8.2, 8.4) и docs/NETWORK_ARCHITECTURE.md. Подключи @network-programmer и @unity-specialist. Создай TradeMarketServer.cs, обнови TradeUI.cs на ServerRpc, обнови CargoSystem.cs для сети. Зависит от Сессии 2, 3, 4.
```

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 6: Tick-система + динамическая экономика
# ═══════════════════════════════════════════════════════

**Агенты:** `@network-programmer` + `@economy-designer`

**GDD для чтения:**
- `docs/gdd/GDD_25_Trade_Routes.md` — секции 5.2 (Tick-система), 5.3 (NPC-трейдеры), 5.4 (Влияние игроков), 5.5 (Глобальные события)
- `docs/gdd/GDD_22_Economy_Trading.md` — секция 5 (Dynamic Economy)

**Зависит от:** Сессия 5 (серверная торговля)

**Цель:** Добавить динамическое обновление рынка — tick-система, NPC-трейдеры, события.

### Задачи:

1. **Добавить `MarketTick()` в `TradeMarketServer.cs`:**
   ```
   void FixedUpdate() {
     _tickTimer += Time.fixedDeltaTime;
     if (_tickTimer >= _tickInterval) {
       MarketTick();
       _tickTimer = 0f;
     }
   }

   void MarketTick() {
     // 1. NPC-трейдеры перемещают товары
     ProcessNPCTrades();

     // 2. Затухание спроса/предложения
     DecaySupplyAndDemand(); // factor *= 0.95

     // 3. Обновление событий
     UpdateEvents();

     // 4. Пересчёт цен
     RecalculatePrices();

     // 5. Отправка обновлений клиентам
     SendMarketUpdateToClients();
   }
   ```

2. **NPC-трейдеры (базовые):**
   ```
   NPCTrader:
   - string route (откуда → куда)
   - string itemId
   - int quantityPerTick
   - void Trade() → UpdateSupply/Demand на обоих концах

   4 NPC для старта:
   - НП-конвой: Примум → Тертиус, мезий, 20 ед.
   - НП-конвой: Приму → Секунд, антигравий, 10 ед.
   - Мануфактура: Тертиус → Квартус, латекс, 15 ед.
   - Независимый: случайный маршрут, разное
   ```

3. **Влияние игроков на рынок:**
   - Покупка → `demandFactor += quantity × 0.02`
   - Продажа → `supplyFactor += quantity × 0.02`
   - Максимум: ±1.5

4. **Глобальные события (2 для теста):**
   ```
   MarketEvent:
   - string eventId
   - string itemId
   - float priceMultiplier
   - int durationTicks
   - bool isActive

   События для теста:
   - «Дефицит мезия»: мезий ×2.0, 3 тика
   - «Бум антигравия»: антигравий ×0.5, 4 тика
   ```

### Что НЕ делаем:
- ❌ Не делаем все 7 событий (только 2)
- ❌ Не делаем карту маршрутов
- ❌ Не делаем блокаду маршрутов

### Критерий завершения:
- ✅ MarketTick() вызывается каждые 5 мин (Host)
- ✅ Затухание работает: demand/supply уменьшаются на 5% в тик
- ✅ NPC-трейдеры обновляют supply/demand
- ✅ Массовая скупка мезия → цена выросла → через тик упала
- ✅ Событие «Дефицит мезия» → цена ×2
- ✅ Обновление цен у всех клиентов
- ✅ Коммит и пуш

### КОМАНДА ДЛЯ ЗАПУСКА:
```
Начинаем Сессию 6: Tick-система + динамическая экономика. Прочитай docs/gdd/GDD_25_Trade_Routes.md (секции 5.2-5.5). Подключи @network-programmer и @economy-designer. Добавь MarketTick() в TradeMarketServer, NPC-трейдеров, влияние игроков, 2 глобальных события. Зависит от Сессии 5.
```

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 7: ContractSystem — контракты НП
# ═══════════════════════════════════════════════════════

**Агенты:** `@gameplay-programmer` + `@network-programmer`

**GDD для чтения:**
- `docs/gdd/GDD_25_Trade_Routes.md` — секции 6 (Контрактная система), 6.2 (Под расписку), 6.3 (Награды)
- `docs/gdd/GDD_22_Economy_Trading.md` — секция 5.2 (Контракты)
- `docs/gdd/GDD_23_Faction_Reputation.md` — секция 7.1 (Торговые репутации)

**Зависит от:** Сессия 5 (серверная торговля)

**Цель:** Добавить систему контрактов НП — взять груз → доставить → получить оплату.

### Задачи:

1. **Создать `ContractSystem.cs`** — менеджер контрактов (NetworkBehaviour):
   ```
   ContractData:
   - string contractId
   - string type (standard, urgent, receipt)
   - string itemId
   - int quantity
   - string fromLocation
   - string toLocation
   - float reward
   - float timeLimit
   - float timeRemaining
   - ulong playerId

   Методы:
   - ContractData CreateContract(type, item, quantity, from, to)
   - bool AcceptContract(ContractData contract, ulong playerId)
   - bool CompleteContract(string contractId, ulong playerId)
   - bool FailContract(string contractId, ulong playerId)
   - float CalculateReward(ContractData contract)
   ```

2. **Система «под расписку» (туториал):**
   - NPC даёт товар бесплатно (первые 2 часа геймплея)
   - Таймер на доставку
   - Не доставил → `debt = cargoValue × 1.5`
   - Репутация НП: -30

3. **Создать `PlayerDebt.cs`** — долги игрока:
   ```
   Поля:
   - ulong playerId
   - float currentDebt
   - float lastDebtUpdateTime
   - float debtInterestRate (1% в день)

   Методы:
   - void AddDebt(float amount)
   - void PayDebt(float amount)
   - void UpdateDebtOverTime() — затухание 1% в день
   - float GetDebtPenalty() — ограничение контрактов
   ```

4. **NPC-агент НП (доска контрактов):**
   - 3 типа контрактов для старта: стандартная, срочная (×1.5), под расписку
   - UI доски контрактов (простой список)
   - ServerRpc: AcceptContract, CompleteContract

### Что НЕ делаем:
- ❌ Не делаем мануфактуры
- ❌ Не делаем чёрный рынок
- ❌ Не делаем военные контракты
- ❌ Не делаем сложные цепочки квестов

### Критерий завершения:
- ✅ ContractSystem создаёт контракты
- ✅ AcceptContract → груз загружен, таймер запущен
- ✅ CompleteContract → награда начислена, репутация +15
- ✅ FailContract → долг начислен, репутация -30
- ✅ PlayerDebt хранит долг, обновляет со временем
- ✅ NPC-агент с 3 типами контрактов
- ✅ Host + Client: сервер авторитетен
- ✅ Коммит и пуш

### КОМАНДА ДЛЯ ЗАПУСКА:
```
Начинаем Сессию 7: ContractSystem. Прочитай docs/gdd/GDD_25_Trade_Routes.md (секции 6, 6.2, 6.3) и docs/gdd/GDD_23_Faction_Reputation.md (секция 7.1). Подключи @gameplay-programmer и @network-programmer. Создай ContractSystem.cs, PlayerDebt.cs, NPC-агент НП с 3 типами контрактов. Зависит от Сессии 5.
```

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 8: Интеграция + полировка
# ═══════════════════════════════════════════════════════

**Агенты:** `@game-designer` + `@gameplay-programmer` + `@unity-specialist` + `@ui-programmer`

**GDD для чтения:**
- Все GDD_22, GDD_23, GDD_25 — финальная проверка
- `docs/gdd/GDD_01_Core_Gameplay.md` — Core Loop интеграция

**Зависит от:** Все предыдущие сессии (1-7)

**Цель:** Связать все системы в единый цикл, полировка, балансировка.

### Задачи:

1. **Интеграция полного цикла:**
   - Подойти к NPC → выбрать контракт → загрузить груз → лететь → сдать → получить награду
   - Проверить что все системы работают вместе:
     - CargoSystem ↔ ShipController (влияние груза на скорость)
     - TradeUI ↔ TradeMarketServer (серверная валидация)
     - ContractSystem ↔ PlayerDebt (долг при провале)
     - LocationMarket ↔ Tick-система (динамические цены)

2. **UI-полировка:**
   - Иконки товаров (placeholder — цветные квадраты 64×64)
   - Улучшенный TradePanel (скролл, категории)
   - Отображение долга в HUD
   - Панель текущего груза корабля (вес/объём/слоты)
   - ControlHintsUI обновлён (новые клавиши для торговли)

3. **Балансировка (первая):**
   - Проверить формулы на практике
   - Настроить цены, штрафы, таймеры
   - Убедиться что «под расписку» работает как туториал
   - Проверить что debt не ломает геймплей

4. **Обновить документацию:**
   - `docs/QWEN_CONTEXT.md` — текущий статус торговли
   - `docs/CHANGELOG.md` — добавить версию
   - `docs/STEP_BY_STEP_DEVELOPMENT.md` — история шагов

### Критерий завершения:
- ✅ Полный цикл работает: контракт → загрузка → полёт → сдача → награда
- ✅ Host + Client: все системы синхронизированы
- ✅ UI читаемый и функциональный
- ✅ Формулы сбалансированы (первая итерация)
- ✅ Документация обновлена
- ✅ Коммит и пуш
- ✅ Тег версии: `v0.0.14-trade-system`

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 8E: Фикс синхронизации кредитов
# ═══════════════════════════════════════════════════════

**Агенты:** `@technical-director`

**Дата:** 9 апреля 2026 г.

**Проблема:** Кредиты хранились в двух местах — `PlayerCreditsManager` (NetworkVariable) и `TradeMarketServer._playerCredits` (Dictionary). Контракты начисляли в `PlayerCreditsManager`, а торговля использовала Dictionary — данные не синхронизировались. Сдача контрактов не добавляла кредитов.

**Решение:**
1. `TradeMarketServer` — единый авторитетный источник кредитов (Dictionary + PlayerPrefs)
2. `ContractSystem.CompleteContractServerRpc` → `TradeMarketServer.SetPlayerCreditsStatic`
3. `ContractBoardUI.OnContractResult` и `TradeUI.OnContractResult` → обновляют кредиты UI из `TradeMarketServer`
4. Удалён `FindPlayerCredits` из TradeMarketServer и ContractSystem
5. `FindPlayerStorage` синхронизирует credits из Dictionary вместо PlayerCreditsManager

**Коммиты:**
- `c680a46` fix(session 8E): contract credits sync — единый источник TradeMarketServer
- `f661c4a` fix(session 8E): add using Unity.Netcode to TradeUI.cs for NetworkManager

**Результат:** ✅ Сдача контрактов начисляет кредиты, покупка списывает корректно

---

# ═══════════════════════════════════════════════════════
# ПОСЛЕ 8 СЕССИЙ — ЧТО ДАЛЬШЕ
# ═══════════════════════════════════════════════════════

## ✅ Готово после 8 сессий:
- ✅ Базовая торговля (купить/продать)
- ✅ Груз корабля влияет на скорость
- ✅ Динамические цены (спрос/предложение)
- ✅ Tick-система + NPC-трейдеры
- ✅ Контракты НП + система «под расписку»
- ✅ Сетевая синхронизация (Host + Client)
- ✅ Долговая система

## ⏳ Отложено на следующие фазы:
- ⏳ Мануфактуры (4 фракции: Аврора, Титан, Гермес, Прометей)
- ⏳ Свободные торговцы (чёрный рынок, контрабанда)
- ⏳ Военные анклавы (опасные маршруты, ×2-3 награда)
- ⏳ Карта маршрутов (визуализация, блокады)
- ⏳ P2P торговля между игроками
- ⏳ Контрабанда (стелс-механика, обход СОЛ)
- ⏳ Все 7 глобальных событий
- ⏳ Визуал: 3D модели грузов, NPC-торговцы, торговые посты
- ⏳ Аудио: звуки торговли, двигатели, шаги NPC
- ⏳ Репутация мануфактур → скидки, эксклюзивные контракты

---

**Связанные документы:**
- [GDD_22_Economy_Trading.md](docs/gdd/GDD_22_Economy_Trading.md)
- [GDD_23_Faction_Reputation.md](docs/gdd/GDD_23_Faction_Reputation.md)
- [GDD_25_Trade_Routes.md](docs/gdd/GDD_25_Trade_Routes.md)
- [MMO_Development_Plan.md](docs/MMO_Development_Plan.md)

---

# ═══════════════════════════════════════════════════════
# СЕССИЯ 9: Документация + ревью архитектуры
# ═══════════════════════════════════════════════════════

**Агенты:** `@technical-director` + `@network-programmer`

**Дата:** 10 апреля 2026 г.

**Цель:** Провести ревью архитектуры торговой системы, обновить документацию, создать RAG-подобный документ для будущих сессий.

### Что сделано:

1. **Ревью архитектуры (technical-director):**
   - P0: PlayerPrefs → заменить на IPlayerDataRepository + БД
   - P0: FindAnyObjectByType → PlayerRegistry словарь
   - P0: ScriptableObject state → разделить MarketConfig + MarketState
   - P1: Валидация позиции в RPC
   - P1: Clamp quantity + включить rate limiting
   - P2: Вынести Editor-код, TradeUI → UXML, унифицировать FindPlayerNetworkPlayer

2. **Создан TRADE_SYSTEM_RAG.md:**
   - Полная архитектура (3 слоя: клиент → RPC → сервер)
   - Потоки данных (покупка, продажа, контракты)
   - Формулы экономики
   - Сетевая модель (RPC таблица, валидация)
   - Известные проблемы с приоритетами
   - Приоритеты рефакторинга

3. **Обновлены GDD документы:**
   - GDD_22_Economy_Trading.md → v3.0
   - Техническая архитектура приведена в соответствие с кодом
   - Acceptance Criteria обновлены (10/14 ✅, 4 отложено)
   - Добавлена цепочка RPC покупки
   - Добавлена секция хранения данных (PlayerDataStore)

4. **Обновлён QWEN_CONTEXT.md:**
   - Статус торговой системы: Сессии 1-8F ЗАВЕРШЕНЫ
   - Добавлены P0/P1 проблемы
   - Обновлена навигация по документации
   - Добавлены ссылки на RAG документы

### Коммиты:
- `docs(session 9): TRADE_SYSTEM_RAG.md — итоговая RAG документация`
- `docs(session 9): update GDD_22 v3.0 — архитектура, acceptance criteria`
- `docs(session 9): update QWEN_CONTEXT.md — статус торговли, проблемы`

### Результат:
- ✅ Полная документация торговой системы
- ✅ Архитектурное ревью с приоритетами
- ✅ Готовый план для Сессии 10 (рефакторинг P0/P1 проблем)
- ✅ RAG документация для ИИ-агентов
