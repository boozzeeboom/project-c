# 📦 План Сессии 8E: Оставшиеся ошибки торговли

**Проект:** Project C: The Clouds  
**Ветка:** `qwen-gamestudio-agent-dev`  
**Дата:** 9 апреля 2026 г.  
**Статус:** 🔴 Запланировано

---

## 🎯 Цель сессии

Исправить оставшиеся ошибки торговой системы, которые были отложены в сессии 8D.

---

## 🔴 Проблемы для исправления

### 1. Clamp demandFactor/supplyFactor при загрузке рынка

**Симптом:** В логах `demandFactor=20` для антигравия. Это приводит к аномальным ценам.

**Причина:** В inspector ScriptableObject рынка `demandFactor` может быть установлен вручную > 1.5. `Clamp(0, 1.5)` работает только при `UpdateDemand()`.

**Решение:**
- В `LocationMarket.OnEnable()` или `InitItems()` добавить clamp всех факторов
- В `MarketItem.OnValidate()` добавить clamp при изменении в inspector

**Код:**
```csharp
// LocationMarket.cs — OnEnable()
public void OnEnable()
{
    InitItems();
    // Clamp факторов при загрузке
    foreach (var mi in items)
    {
        if (mi != null)
        {
            mi.demandFactor = Mathf.Clamp(mi.demandFactor, 0f, 1.5f);
            mi.supplyFactor = Mathf.Clamp(mi.supplyFactor, 0f, 1.5f);
        }
    }
    RecalculatePrices();
}
```

**Файлы:** `LocationMarket.cs`

---

### 2. Валидация quantity > 0 в RPC

**Симптом:** Теоретически можно отправить `quantity=0` или `quantity=-1`.

**Причина:** Нет проверки в `BuyItemServerRpc/SellItemServerRpc`.

**Решение:**
- Добавить проверку в начало обоих RPC

**Код:**
```csharp
// TradeMarketServer.cs — BuyItemServerRpc/SellItemServerRpc
if (quantity <= 0)
{
    LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "quantity <= 0");
    SendTradeResultToClient(clientId, false, "Неверное количество!", 0f, 0, 0, 0, 0);
    return;
}
```

**Файлы:** `TradeMarketServer.cs`

---

### 3. Валидация locationId != null/empty в RPC

**Симптом:** Если `locationId` пустой, сервер не найдёт рынок и вернёт ошибку.

**Причина:** Клиент может отправить пустой `locationId`.

**Решение:**
- Добавить проверку в начало обоих RPC

**Код:**
```csharp
if (string.IsNullOrEmpty(locationId))
{
    LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "locationId пустой");
    SendTradeResultToClient(clientId, false, "Локация не указана!", 0f, 0, 0, 0, 0);
    return;
}
```

**Файлы:** `TradeMarketServer.cs`

---

### 4. Проверка работы в Host+Client режиме

**Симптом:** Сессия 8D тестировалась только в Host режиме. Не проверено что клиент видит правильные данные при подключении к отдельному серверу.

**Причина:** `PlayerPrefs` используются для хранения склада. При Host+Client клиент и сервер — разные процессы с разными PlayerPrefs.

**Решение:**
- Протестировать: запустить Server (dedicated) → запустить Client → купить → проверить что клиент видит товар
- Если проблема — добавить передачу данных склада через ClientRpc при подключении

**Файлы:** `TradeMarketServer.cs`, `TradeUI.cs`

---

### 5. Лог при каждом Save/Load для отладки

**Симптом:** Сложно отследить рассинхронизацию данных склада.

**Решение:**
- Добавить `Debug.Log` в `PlayerTradeStorage.Save()` и `Load()`
- Показывать locationId, количество товаров, кредиты

**Код:**
```csharp
// PlayerTradeStorage.cs — Save()
public void Save()
{
    string locKey = string.IsNullOrEmpty(currentLocationId) ? "global" : currentLocationId.ToLower();
    Debug.Log($"[PTS] Save: loc={locKey}, credits={credits:F0}, items={warehouse.Count}");
    // ...
}

// PlayerTradeStorage.cs — Load()
public void Load()
{
    string locKey = string.IsNullOrEmpty(currentLocationId) ? "global" : currentLocationId.ToLower();
    Debug.Log($"[PTS] Load: loc={locKey}");
    // ...
    Debug.Log($"[PTS] Loaded: credits={credits:F0}, items={warehouse.Count}");
}
```

**Файлы:** `PlayerTradeStorage.cs`

---

## 📋 План выполнения

1. Clamp факторов при загрузке
2. Валидация quantity и locationId
3. Логи Save/Load
4. Тест Host+Client
5. Документация

---

## 🔗 Связанные документы

- [QWENTRADING8D_SESSION.md](QWENTRADING8D_SESSION.md) — итоги сессии 8D
- [TRADE_DEBUG_GUIDE.md](TRADE_DEBUG_GUIDE.md) — руководство по отладке
- [QWENTRADING8SESSION.md](QWENTRADING8SESSION.md) — исходный план торговли
