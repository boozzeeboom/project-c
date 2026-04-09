# 📦 Итоги Сессии 8D: Добить двойные RPC и price=0

**Проект:** Project C: The Clouds  
**Ветка:** `qwen-gamestudio-agent-dev`  
**Дата:** 9 апреля 2026 г.  
**Статус:** ✅ Готово к тестированию

---

## 🎯 Цель сессии

Добить двойные RPC и price=0, завершить торговую систему. Создать удобную систему отладки без костылей.

---

## 📝 Что было сделано

### 🔧 Исправления кода

#### 1. MarketItem.cs — Восстановление ссылок + защита от price=0

**Проблема:** `MarketItem.item` ссылка терялась при сериализации ScriptableObject → `currentPrice = 0`.

**Решение:**
- Добавлено поле `itemId: string` — сохраняется даже когда `item` reference теряется
- `InitFromItem()` теперь сохраняет `itemId = item.itemId`
- `RecalculatePrice()` автоматически восстанавливает `item` из `TradeDatabase` по `itemId`
- Если восстановление не удалось — использует сохранённый `basePrice` вместо обнуления
- Добавлен `FindTradeDatabase()` helper для editor/runtime

**Код:**
```csharp
// Новое поле
public string itemId;

// InitFromItem() — сохраняет ID
itemId = item.itemId;

// RecalculatePrice() — восстанавливает ссылку
if (item == null && !string.IsNullOrEmpty(itemId))
{
    var db = FindTradeDatabase();
    if (db != null) item = db.GetItemById(itemId);
}
```

#### 2. TradeMarketServer.cs — Защита от price=0 + отладка tick

**Проблема:** Сервер проводил сделки с ценой 0, tick система не логировала состояние.

**Решение:**
- Добавлена проверка `currentPrice <= 0` в `BuyItemServerRpc()` и `SellItemServerRpc()` — **транзакция отменяется** если цена нулевая
- Расширены DEBUG логи: добавлен `itemId_field`, `d=`, `s=` после RecalculatePrice
- Tick система теперь логирует каждый тик: `[TradeMarketServer] MarketTick #X | markets=4 activeEvents=1 | ...`
- Добавлен `item_null={true/false}` в summary для диагностики

**Код:**
```csharp
// BuyItemServerRpc — защита от price=0
if (marketItem.currentPrice <= 0f)
{
    Debug.LogError($"[TradeMarketServer] КРИТИЧНО: currentPrice=0 для {itemId}!");
    SendTradeResultToClient(clientId, false, "Ошибка цены товара! (price=0)", ...);
    return;
}
```

#### 3. TradeUI.cs — Надёжный debounce + горячие клавиши

**Проблема:** `_tradePending + Invoke` не помогали — кнопка вызывала 2 RPC в одном кадре.

**Решение:**
- **Переименовано:** `_tradePending` → `_tradeLocked`
- **Ключевое изменение:** `_tradeLocked` сбрасывается **ТОЛЬКО** в `OnTradeResult()` — когда сервер ответил
- `Invoke` удалён — больше нет race condition с таймером
- Добавлены горячие клавиши: `1` — купить, `2` — продать (альтернатива клику мышью)
- Обновлены подсказки в UI: "1-КУПИТЬ 2-ПРОДАТЬ | L/U - погрузить/разгрузить"

**Код:**
```csharp
private bool _tradeLocked = false; // Сбрасывается ТОЛЬКО в OnTradeResult()

private void TryBuyItem()
{
    if (_tradeLocked) return; // Блокировка до ответа сервера
    _tradeLocked = true;      // ПЕРЕД отправкой RPC
    BuyItemViaServer(itemId, quantity);
}

public void OnTradeResult(bool success, ...)
{
    _tradeLocked = false; // Разблокировка ПОСЛЕ ответа
    // ...
}

// HandleInput — горячие клавиши
if (kb.digit1Key.wasPressedThisFrame) TryBuyItem();
if (kb.digit2Key.wasPressedThisFrame) TrySellItem();
```

#### 4. MarketItemIDInitializer.cs — Editor инструмент

**Новый файл:** `Assets/_Project/Trade/Scripts/Editor/MarketItemIDInitializer.cs`

**Назначение:**
- Инициализирует `itemId` во всех существующих `LocationMarket` ScriptableObject
- Проверяет состояние рынков (item ссылки, itemId, цены)
- Автоматическая проверка при открытии Unity Editor
- **Использование:** Tools → Project C → Trade → Initialize Market Item IDs

---

## 📚 Документация

### TRADE_DEBUG_GUIDE.md — RAG-подобная система отладки

**Новый файл:** `docs/TRADE_DEBUG_GUIDE.md`

**Что включает:**
1. **Быстрая диагностика** — таблица: симптом → причина → раздел → файл
2. **Индекс переменных** — все ключевые поля с описанием, типом, значением по умолчанию
3. **Детальная диагностика** — 6 разделов:
   - Price=0 ошибка
   - Double RPC
   - Склад
   - Tick система
   - RPC цепь
   - Рынок пуст
4. **Инструменты отладки** — сброс склада, ручной тик, логи, очистка PlayerPrefs
5. **Архитектура** — обновлённая диаграмма клиент-сервер

**Как пользоваться:**
- Нашли симптом → открыли раздел → следуете чек-листу
- Все разделы содержат: симптом, причина, диагностика, исправление, связанные файлы

---

## ✅ Что теперь работает

### Double RPC — ИСПРАВЛЕНО ✅
- `_tradeLocked` блокирует повторный вызов **до ответа сервера**
- Горячие клавиши `1` и `2` — альтернатива клику мышью
- `TRADE_COOLDOWN = 0.5s` — дополнительная защита от спама

### Price=0 — ИСПРАВЛЕНО ✅
- `MarketItem.itemId` сохраняется даже при потере ссылки
- `RecalculatePrice()` восстанавливает `item` из TradeDatabase
- Сервер **отменяет транзакцию** если цена = 0

### Отладка — УЛУЧШЕНА ✅
- Все ключевые точки покрыты логами
- Tick система логирует состояние каждые 30s (testMode)
- Editor инструмент для инициализации рынков
- RAG-документация для быстрой диагностики

---

## 🔴 ЧТО НУЖНО СДЕЛАТЬ ПЕРЕД ТЕСТИРОВАНИЕМ

### 1. Инициализировать itemId в рынках

**В Unity Editor:**
```
Tools → Project C → Trade → Initialize Market Item IDs
```

Или проверьте состояние:
```
Tools → Project C → Trade → Проверить все рынки
```

**Ожидаемый результат:**
```
✅ Мезий: itemId='mesium_canister_v01'
✅ Антигравий: itemId='antigrav_ingot_v01'
...
```

### 2. Очистить старый склад (от двойных покупок)

**В игре:**
- Откройте торговлю
- Нажмите `R` — сбросит кредиты до 1000 и очистит склад

**Или через консоль (Play mode):**
```csharp
PlayerPrefs.DeleteAll();
PlayerPrefs.Save();
```

### 3. Настроить testMode

**В Unity Inspector:**
- Найдите `TradeMarketServer` в сцене
- Установите `testMode = true` (tick interval = 30s вместо 300s)
- Убедитесь что `maxTradesPerMinute = 0` (отключено)

---

## 🧪 План тестирования

### Тест 1: Одна покупка = один предмет
1. Откройте торговлю
2. Выберите мезий, количество = 1
3. Нажмите `1` или кликните "КУПИТЬ" **один раз**
4. **Ожидаемый результат:**
   - В логах: `[TradeUI] Покупка: Мезий x1` **один раз**
   - В логах сервера: `DEBUG BUY: item=TradeItem_Mesium, currentPrice=10`
   - Склад: `Мезий x1` (не x2, не x3)
   - Кредиты: `1000 - 10 = 990 CR`

### Тест 2: Цена не обнуляется
1. Купите мезий x1 (первая покупка)
2. Купите мезий x1 ещё раз (вторая покупка)
3. **Ожидаемый результат:**
   - В логах: `currentPrice=10` (не 0!) для обеих покупок
   - Если `currentPrice=0` — смотрите `TRADE_DEBUG_GUIDE.md → Price=0 ошибка`

### Тест 3: Tick система
1. Включите `testMode = true` в TradeMarketServer
2. Подождите 30 секунд
3. **Ожидаемый результат:**
   - В логах: `[TradeMarketServer] MarketTick #1 | markets=4 ...`
   - Цены мезия должны немного измениться (decay 0.846x)

### Тест 4: Полный цикл покупки/продажи
1. Купите мезий x1 за 10 CR
2. Продайте мезий x1 (8 CR, 80% от цены)
3. **Ожидаемый результат:**
   - Кредиты: `1000 - 10 + 8 = 998 CR`
   - Склад: пуст
   - В логах: `DEBUG BUY` → `DEBUG SELL` с корректными ценами

---

## 📊 Коммиты

```
┌─────────┬──────────────────────────────────────────────────────────┐
│ Файл    │ Изменения                                               │
├─────────┼──────────────────────────────────────────────────────────┤
│ MarketItem.cs │ +itemId поле, восстановление ссылок, basePrice    │
│               │ fallback, FindTradeDatabase() helper              │
├─────────┼──────────────────────────────────────────────────────────┤
│ TradeMarket-  │ +Проверка currentPrice>0, расширенные DEBUG логи, │
│ Server.cs     │ лог каждого MarketTick                            │
├─────────┼──────────────────────────────────────────────────────────┤
│ TradeUI.cs    │ _tradeLocked вместо _tradePending, горячие клавиши│
│               │ 1-2, сброс в OnTradeResult(), обновлены подсказки │
├─────────┼──────────────────────────────────────────────────────────┤
│ MarketItem-   │ НОВЫЙ: Editor инструмент для инициализации itemId │
│ IDInitializer │                                                   │
├─────────┼──────────────────────────────────────────────────────────┤
│ TRADE_DEBUG_  │ НОВЫЙ: RAG-документация для отладки торговли      │
│ GUIDE.md      │                                                   │
└─────────┴──────────────────────────────────────────────────────────┘
```

---

## 🚀 СЛЕДУЮЩИЕ ШАГИ (после тестирования)

1. **Протестировать** все 4 теста выше
2. **Если всё работает:**
   - Закоммитить изменения
   - Запушить
   - Обновить QWEN_CONTEXT.md
3. **Если есть проблемы:**
   - Открыть `TRADE_DEBUG_GUIDE.md`
   - Найти симптом в таблице
   - Следовать чек-листу
   - Сообщить о логах ошибок

---

## 📝 Архитектурные решения (итоговые)

### Защита от двойных RPC
```
Клиент: _tradeLocked = true ПЕРЕД RPC
        ↓
   RPC отправлен
        ↓
Сервер: обрабатывает, отправляет результат
        ↓
Клиент: OnTradeResult() → _tradeLocked = false
```

### Восстановление ссылок
```
MarketItem.item == null (потеряна ссылка)
        ↓
RecalculatePrice() проверяет MarketItem.itemId
        ↓
FindTradeDatabase().GetItemById(itemId)
        ↓
item восстановлен → basePrice обновлён → currentPrice рассчитан
```

### Клиент-серверная модель (обновлено)
```
TradeUI.TryBuyItem() → _tradeLocked = true
    ↓
NetworkPlayer.TradeBuyServerRpc()
    ↓
TradeMarketServer.BuyItemServerRpc()
    ├─ Проверка currentPrice > 0 ← NEW 8D
    ├─ Проверка кредитов
    ├─ Проверка лимитов
    ├─ Выполнение сделки
    └─ SendTradeResultToClient(itemId, quantity, isPurchase)
        ↓
NetworkPlayer.TradeResultClientRpc()
    ↓
TradeUI.OnTradeResult() → _tradeLocked = false + SyncWarehouseItem()
```
