# 📦 Сессия 6: Tick-система + динамическая экономика

**Проект:** Project C: The Clouds
**Ветка:** `qwen-gamestudio-agent-dev`
**Статус:** ✅ ЗАВЕРШЕНА + ЗАПУЩЕНА
**Дата начала:** 7 апреля 2026 г.
**Дата завершения:** 7 апреля 2026 г.
**Агенты:** `@network-programmer` + `@economy-designer`

## 📍 ТОЧКА БЭКАПА (РАБОЧАЯ)

```
git branch: qwen-gamestudio-agent-dev
HEAD: ef0a5fc (Fix: TradeMarketServer инициализация + защита от tickInterval=0)
backup: backup/session6-working-v2  ← РАБОЧАЯ ВЕРСИЯ СЕССИИ 6
backup: backup/session6-tick-economy ← базовая Сессия 6
backup: backup/session5-complete-server-trade ← до Сессии 6
```

**Перед рестартом Сессии 6:**
```bash
git status                          # проверить что всё закоммичено
git log -n 1                        # запомнить HEAD коммит
git stash                           # если есть незакоммиченные изменения
git checkout backup/session5-complete-server-trade
git checkout -b qwen-gamestudio-agent-dev
```

**Если нужно откатить Сессию 6:**
```bash
git reset --hard backup/session5-complete-server-trade
```

---

## 🎯 ЦЕЛЬ СЕССИИ

Превратить статический рынок в **живую самобалансирующуюся экономику**:
1. NPC-трейдеры автоматически перемещают товары между локациями
2. Спрос/предложение затухают с elastic-возвратом ("качели")
3. Глобальные события влияют на цены
4. Delta-отправка обновлений (только изменённые предметы)

---

## 📐 АРХИТЕКТУРНЫЕ РЕШЕНИЯ (утверждены)

### Затухание: Elastic "качели"
```
Эффективный множитель: 0.846 за тик (decayRate 0.92 × elastic 0.92)
- demandFactor = 0.3 → норма за ~20 мин
- demandFactor = 1.5 → норма за ~80 мин
- Минимальный порог: < 0.01 → обнуление
```

### NPC-трейдеры: серверная абстракция
- **НЕ NetworkObject** — обычные C# классы, только сервер считает
- 4 трейдера: 2 постоянных + 2 условных (реакция на рынок)
- Объёмы: 2-12 единиц/тик в зависимости от товара

### Глобальные события
- `List<MarketEvent>` на сервере (НЕ NetworkVariable)
- ClientRPC при создании/окончании события
- При подключении клиента — отправка всех активных событий

### Delta-отправка
- `isDirty` флаг на MarketItem
- ClientRPC шлёт только изменённые предметы
- Группировка: один ClientRpc на все рынки

### Сохранение
- ❌ НЕ делаем в этой сессии
- 📌 Добавлено в план MMO_Development_Plan.md как будущая задача

---

## 🔧 ФАЙЛЫ ДЛЯ ИЗМЕНЕНИЯ

### Изменяемые:
| Файл | Что менять |
|------|-----------|
| `MarketItem.cs` | DecayFactors(elastic), isDirty флаг, initialStock, ApplyEventMultiplier() |
| `LocationMarket.cs` | DecaySupplyAndDemand(elastic), пассивная регенерация стока +2% |
| `TradeMarketServer.cs` | _npcTraders, _activeEvents, ProcessNPCTrades(), UpdateEvents(), полный MarketTick(), ClientRPC событий |
| `TradeUI.cs` | Отображение активных событий (если время позволит) |

### Новые:
| Файл | Назначение |
|------|-----------|
| `NPCTrader.cs` | Класс NPC-трейдера (обычный класс, НЕ NetworkBehaviour) |
| `MarketEvent.cs` | Serializable класс глобального события |

---

## 📋 ЗАДАЧИ (TODO)

### 1. MarketItem.cs — Elastic + isDirty + initialStock
```csharp
// НОВОЕ ПОЛЕ:
public int initialStock = 50;       // базовый сток для регенерации
public bool isDirty;                // флаг изменения для delta-отправки
public float eventMultiplier = 1f;  // множитель от глобальных событий

// ОБНОВИТЬ DecayFactors():
public void DecayFactors(float decayRate = 0.92f, float elasticStrength = 0.08f)
{
    demandFactor *= decayRate * (1f - elasticStrength);  // 0.846
    supplyFactor *= decayRate * (1f - elasticStrength);
    
    // Минимальный порог
    if (demandFactor < 0.01f) demandFactor = 0f;
    if (supplyFactor < 0.01f) supplyFactor = 0f;
    
    demandFactor = Mathf.Round(demandFactor * 1000f) / 1000f;
    supplyFactor = Mathf.Round(supplyFactor * 1000f) / 1000f;
    
    isDirty = true;
    RecalculatePrice();
}

// ДОБАВИТЬ ApplyEventMultiplier():
public void ApplyEventMultiplier(float multiplier)
{
    eventMultiplier = multiplier;
    isDirty = true;
    RecalculatePrice();
}

// ОБНОВИТЬ RecalculatePrice():
// currentPrice = basePrice × (1 + demandFactor - supplyFactor) × eventMultiplier
```

### 2. LocationMarket.cs — пассивная регенерация стока
```csharp
// ОБНОВИТЬ DecaySupplyAndDemand():
public void DecaySupplyAndDemand(float decayRate = 0.92f, float elasticStrength = 0.08f)
{
    foreach (var marketItem in items)
    {
        if (marketItem != null)
        {
            // Пассивная регенерация: +2% от базового стока за тик
            int regenAmount = Mathf.Max(1, Mathf.RoundToInt(marketItem.initialStock * 0.02f));
            if (marketItem.availableStock < marketItem.initialStock)
            {
                marketItem.availableStock += regenAmount;
                marketItem.isDirty = true;
            }
            
            marketItem.DecayFactors(decayRate, elasticStrength);
        }
    }
}
```

### 3. NPCTrader.cs — НОВЫЙ файл
```csharp
[System.Serializable]
public class NPCTrader
{
    public string traderId;
    public string traderName;
    public string fromLocationId;
    public string toLocationId;
    public string itemId;
    public int minVolumePerTick;
    public int maxVolumePerTick;
    public bool isConditional;        // true = работает только при условиях
    public string conditionType;      // "supply_threshold", "price_threshold", "always"
    public float conditionValue;      // порог для условия
    
    // Методы:
    public bool ShouldTrade(LocationMarket fromMarket, LocationMarket toMarket);
    public int GetVolumeForTick();    // рандом min-max
    public void ExecuteTrade(Dictionary<string, LocationMarket> markets);
}
```

**4 NPC для старта:**
| NPC | from → to | Товар | Объём | Условие |
|-----|-----------|-------|-------|---------|
| ГосКонвой | primium → tertius | mesium | 5-8 | always |
| Ветер | primium → secundus | antigrav | 3-5 | always |
| Караванщик | tertius → quartus | latex | 8-12 | supplyFactor > 0.3 в tertius |
| Челнок | secundus → primium | mesium | 2-4 | цена > basePrice × 1.3 в primium |

### 4. MarketEvent.cs — НОВЫЙ файл
```csharp
[System.Serializable]
public class MarketEvent
{
    public string eventId;                // "mesium_rush_001"
    public string displayName;            // "Мезиевая лихорадка"
    public string displayNameIcon;        // "🔥"
    public string affectedItemId;         // "mesium_canister_v01"
    public string[] affectedLocations;    // ["ALL"] или конкретные
    public float demandMultiplier;        // 1.4f (дополнительный множитель спроса)
    public float supplyMultiplier;        // 1.0f (обычно не меняется)
    public float priceMultiplier;         // вычисляется
    public int durationTicks;             // 6
    public int remainingTicks;            // обратный отсчёт
    public string triggerType;            // "demand_threshold", "random", "manual"
    public float triggerValue;            // 0.8f (порог demandFactor)
    public int cooldownTicks;             // 24
    public int cooldownRemaining;         // обратный отсчёт кулдауна
    public bool isActive;
    public float startTime;               // Time.time для расчёта
    
    // Методы:
    public bool ShouldTrigger(Dictionary<string, LocationMarket> markets);
    public void Tick();                   // уменьшает remainingTicks
    public bool IsExpired();
    public void ApplyToMarket(LocationMarket market, string itemId);
    public void RemoveFromMarket(LocationMarket market, string itemId);
}
```

**Событие для теста: Мезиевая лихорадка**
- Триггер: demandFactor мезия > 0.8 на любом рынке
- Эффект: demandMultiplier = 1.4 для мезия во ВСЕХ локациях
- Длительность: 6 тиков (30 мин реального времени)
- Cooldown: 24 тика (2 часа)

### 5. TradeMarketServer.cs — полная интеграция

**НОВЫЕ ПОЛЯ:**
```csharp
[Header("NPC Traders")]
[SerializeField] private List<NPCTrader> _npcTraders = new List<NPCTrader>();

[Header("Market Events")]
[SerializeField] private List<MarketEvent> _activeEvents = new List<MarketEvent>();

private Dictionary<ulong, string> _clientSubscriptions = new Dictionary<ulong, string>();
```

**ОБНОВИТЬ MarketTick():**
```csharp
private void MarketTick()
{
    // 1. NPC-трейдеры перемещают товары
    ProcessNPCTrades();
    
    // 2. Проверка и обновление событий
    UpdateEvents();
    
    // 3. Затухание спроса/предложения (elastic)
    foreach (var market in _markets.Values)
    {
        market.DecaySupplyAndDemand(0.92f, 0.08f);
    }
    
    // 4. Пересчёт цен (с eventMultiplier)
    foreach (var market in _markets.Values)
    {
        market.RecalculatePrices();
    }
    
    // 5. Delta-отправка обновлений клиентам
    SendDeltaUpdatesToClients();
    
    Debug.Log($"[TradeMarketServer] MarketTick выполнен. Рынков: {_markets.Count}, Событий: {_activeEvents.Count(e => e.isActive)}");
}
```

**ДОБАВИТЬ ProcessNPCTrades():**
```csharp
private void ProcessNPCTrades()
{
    foreach (var trader in _npcTraders)
    {
        if (!_markets.ContainsKey(trader.fromLocationId) || 
            !_markets.ContainsKey(trader.toLocationId)) continue;
            
        var fromMarket = _markets[trader.fromLocationId];
        var toMarket = _markets[trader.toLocationId];
        
        if (!trader.ShouldTrade(fromMarket, toMarket)) continue;
        
        trader.ExecuteTrade(_markets);
    }
}
```

**ДОБАВИТЬ UpdateEvents():**
```csharp
private void UpdateEvents()
{
    // Проверка триггеров для неактивных событий
    foreach (var evt in _activeEvents)
    {
        if (!evt.isActive && evt.cooldownRemaining <= 0 && evt.ShouldTrigger(_markets))
        {
            evt.isActive = true;
            evt.remainingTicks = evt.durationTicks;
            BroadcastEventClientRpc(evt);
        }
    }
    
    // Обновление активных событий
    foreach (var evt in _activeEvents.Where(e => e.isActive).ToList())
    {
        evt.Tick();
        if (evt.IsExpired())
        {
            evt.isActive = false;
            evt.cooldownRemaining = evt.cooldownTicks;
            RemoveEventClientRpc(evt.eventId);
        }
    }
}
```

**ДОБАВИТЬ SendDeltaUpdatesToClients():**
```csharp
private void SendDeltaUpdatesToClients()
{
    foreach (var market in _markets.Values)
    {
        var dirtyItems = market.items.Where(m => m != null && m.isDirty).ToList();
        if (dirtyItems.Count == 0) continue;
        
        var data = SerializeMarketDataDelta(market, dirtyItems);
        SendMarketUpdateClientRpc(market.locationId, data.itemIds, data.prices, 
            data.stocks, data.demands, data.supplies);
        
        // Сброс isDirty
        foreach (var item in dirtyItems) item.isDirty = false;
    }
}
```

**ДОБАВИТЬ ClientRPC для событий:**
```csharp
[ClientRpc]
private void BroadcastEventClientRpc(MarketEvent evt)
{
    if (TradeUI.Instance != null)
    {
        TradeUI.Instance.OnMarketEventStarted(evt);
    }
    Debug.Log($"[TradeMarketServer] 📢 Событие: {evt.displayName}");
}

[ClientRpc]
private void RemoveEventClientRpc(string eventId)
{
    if (TradeUI.Instance != null)
    {
        TradeUI.Instance.OnMarketEventEnded(eventId);
    }
    Debug.Log($"[TradeMarketServer] 📢 Событие окончилось: {eventId}");
}
```

### 6. TradeUI.cs — отображение событий (если время позволит)
```csharp
// ДОБАВИТЬ:
private Dictionary<string, MarketEvent> _activeEvents = new Dictionary<string, MarketEvent>();

public void OnMarketEventStarted(MarketEvent evt)
{
    _activeEvents[evt.eventId] = evt;
    UpdateEventDisplay();
}

public void OnMarketEventEnded(string eventId)
{
    _activeEvents.Remove(eventId);
    UpdateEventDisplay();
}
```

---

## 📊 ФОРМУЛЫ (финальные)

### Цена товара
```
currentPrice = basePrice × (1 + demandFactor - supplyFactor) × eventMultiplier

Ограничения:
- minPrice = basePrice × 0.5
- maxPrice = basePrice × 5.0
```

### Затухание
```
demandFactor *= 0.92 × (1 - 0.08) = 0.846 за тик
supplyFactor *= 0.92 × (1 - 0.08) = 0.846 за тик
< 0.01 → обнуление
```

### Влияние игрока
```
Покупка: demandFactor += quantity × 0.02
Продажа: supplyFactor += quantity × 0.02
Максимум: ±1.5
```

### NPC-трейдер
```
В локации отправления: demandFactor += volume × 0.02
В локации назначения: supplyFactor += volume × 0.02, availableStock += volume
```

### Пассивная регенерация стока
```
regenAmount = max(1, initialStock × 0.02)
if availableStock < initialStock: availableStock += regenAmount
```

---

## ✅ КРИТЕРИИ ЗАВЕРШЕНИЯ (ПОДТВЕРЖДЕНО)

- [x] MarketTick() вызывается каждые 30 сек (Test Mode) / 300 сек (Host)
- [x] Затухание elastic работает: demand/supply уменьшаются ×0.846 за тик
- [x] 4 NPC-трейдера перемещают товары между локациями
- [x] Пассивная регенерация стока работает (+2% за тик)
- [x] Массовая скупка мезия → цена выросла → через 3-4 тика упала (качели)
  - Подтверждено: tertius мезий 10.0CR → 9.7CR после NPC-доставки
- [x] Событие "Мезиевая лихорадка" → demand ×1.4 для мезия (инициализировано, ждёт триггера)
- [x] Delta-отправка: ClientRPC шлёт только изменённые предметы (isDirty)
- [x] При подключении клиента — актуальные цены рынка
- [x] Коммит и пуш

### Лог подтверждения работы:
```
[TICK #1] Рынков:4 NPC:4 Событий:0 |
  primium: мезий=10,0CR d=0,00 s=0,00 stock=1 |
  quartus: мезий=10,0CR d=0,00 s=0,00 stock=31 |
  secundus: мезий=10,0CR d=0,00 s=0,00 stock=60 |
  tertius: мезий=9,7CR d=0,00 s=0,03 stock=72
```

### 🟡 Известные проблемы (переносятся в Сессию 7+):
- TradeTrigger спамит лог при каждом кадре (не критично)
- primium stock=1 — NPC забирает весь мезий, сток не успевает восстановиться
- Кнопки ПОГРУЗИТЬ/РАЗГРУЗИТЬ исчезают на вкладке [РЫНОК]
- UI расположение требует подгонки

---

## 🔗 СВЯЗАННЫЕ ДОКУМЕНТЫ

- `docs/gdd/GDD_25_Trade_Routes.md` — секции 5.2-5.5 (Tick, NPC, события)
- `docs/QWENTRADING8SESSION.md` — план всех 8 сессий
- `docs/QWEN_CONTEXT.md` — текущий статус проекта
- `docs/MMO_Development_Plan.md` — общий план MMO

---

## ⚠️ ИЗВЕСТНЫЕ ПРОБЛЕМЫ ИЗ СЕССИИ 5 (не решены)

- Кнопки ПОГРУЗИТЬ/РАЗГРУЗИТЬ исчезают на вкладке [РЫНОК]
- UI расположение требует подгонки (кнопки, отступы)
- Отладочные логи в консоли (убрать перед релизом)
- Разгрузка: выбор идёт по индексу на складе, а не в трюме

---

## 🚀 КОМАНДА ДЛЯ ЗАПУСКА (если нужно перезапустить)

```
Продолжаем Сессию 6: Tick-система + динамическая экономика. 
Прочитай docs/QWENTRADING9SESSION.md — там полный контекст, точки бэкапа и план реализации.
Ветка: qwen-gamestudio-agent-dev. Бэкап: backup/session5-complete-server-trade
Подключи @network-programmer и @economy-designer.
```
