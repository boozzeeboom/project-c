# 📦 Итоги Сессии 8C: Фикс торговой системы

**Проект:** Project C: The Clouds
**Ветка:** `qwen-gamestudio-agent-dev`
**Дата:** 9 апреля 2026 г.
**Статус:** ✅ Коммиты готовы, ПУШ НЕ ВЫПОЛНЕН

---

## 🎯 Цель сессии

Исправить критическую проблему: `TradeUI.BuyItemViaServer()` → fallback на локальную покупку → сервер не видит товар → контракт не сдаётся.

---

## 📝 Что было сделано

### Коммит 1: `e87f8e6` — fix(session 8C): убрать fallback на локальную покупку
**Проблема:** `BuyItemViaServer()` падал в `BuyItemLocal()` если `_player.IsOwner == false`.
**Решение:**
- Убран `IsOwner` check — RPC `[SendTo.Server]` всегда работает
- Убран fallback на `BuyItemLocal()` — все операции только через сервер
- Добавлена `SyncWarehouseItem()` — клиентский склад обновляется из `OnTradeResult`
- Расширен `TradeResultClientRpc(itemId, quantity, isPurchase)` — сервер передаёт данные о товаре
- `BuyItemLocal/SellItemLocal` закомментированы

### Коммит 2: `0428109` — fix(session 8C): ленивый поиск NetworkPlayer
**Проблема:** `_player = FindAnyObjectByType<NetworkPlayer>()` в `Awake()` возвращал null — NetworkObject ещё не заспавнен.
**Решение:** Свойство `Player` с ленивым поиском при первом обращении.

### Коммит 3: `03ef26d` — fix(session 8C): отладочные логи + rate limit
- Включён `Debug.Log` в `LogTransaction`
- Добавлен лог при rate limit блокировке
- `maxTradesPerMinute` 10 → 30

### Коммит 4: `acde03c` — fix(session 8C): RemoveFromWarehouse
**Проблема:** При продаже сервер удалял товар, но клиентский TradeUI продолжал показывать.
**Решение:** `RemoveFromWarehouse()` вызывается из `OnTradeResult` при `!isPurchase`.

### Коммит 5: `ed8b517` — fix(session 8C): единый PlayerTradeStorage
**Проблема:** TradeUI использовал `FindObjectOfType<PlayerTradeStorage>` (сцена), сервер — компонент на NetworkPlayer. Два разных хранилища → "призрачные" предметы.
**Решение:** `GetPlayerStorageFromNetworkPlayer()` — клиент и сервер работают с одним хранилищем.
**Дополнительно:** `LoadAllMarkets()` вызывает `InitItems() + RecalculatePrices()` (фикс цены 0 CR).

### Коммит 6: `926dfb8` — fix(session 8C): аудит торговой системы — 4 исправления
1. **Двойной RPC** — debounce 0.5s + проверка EventSystem
2. **Rate limit отключён** — `maxTradesPerMinute: 0` в коде и prefab
3. **Price = 0 CR null guard** — проверка `marketItem.item == null` в Buy/Sell RPC
4. **Prefab serialized value** — `maxTradesPerMinute: 10 → 0`

---

## 🔴 НЕРЕШЁННЫЕ ПРОБЛЕМЫ (обнаружены при тестировании)

### 1. Двойные RPC (покупка/продажа x2 за один клик)
**Симптом:** Каждый клик по кнопке вызывает 2 RPC вызова.
**Причина:** `HandleInput()` (Enter key) + `Button.onClick` (Unity EventSystem) срабатывают одновременно. Debounce 0.5s НЕ помогает — вызовы происходят в разных кадрах.
**Что нужно:** Полностью разделить клавиатурный и мышечный ввод, или убрать Enter из `HandleInput()`.

### 2. Price = 0 CR (качели цен сломаны)
**Симптом:** Первый buy = 85 CR, второй = 0 CR. Первая продажа = 68 CR, вторая = 0 CR.
**Причина:** После первой сделки `marketItem.currentPrice` на сервере становится 0. Вероятные причины:
- Tick система (MarketTick → DecaySupplyAndDemand → RecalculatePrices) обнуляет цену
- `MarketItem.item` reference теряется на сервере после RPC
- `LocationMarket` ScriptableObject не правильно инициализируется
**Что нужно:** Проверить `marketItem.item` в момент покупки, посмотреть что происходит с ценой после каждого тика.

### 3. Склад показывает "призрачные" товары (22 шт вместо реального кол-ва)
**Симптом:** `RemoveFromWarehouse: antigrav_ingot_v01 x1 (было: 22)` — на складе 22 предмета, хотя игрок купил 1-2.
**Причина:** Двойные RPC удваивают покупки → склад накапливает лишнее. Fix #1 решит эту проблему.

---

## 📐 Архитектурные решения

### Клиент-серверная модель торговли (итоговая)

```
┌─────────────────────────────────────────────────────────┐
│                    КЛИЕНТ (TradeUI)                      │
│  - UI торговли (Canvas, кнопки, текст)                   │
│  - playerStorage = GetPlayerStorageFromNetworkPlayer()   │
│    → берёт компонент с NetworkPlayer (тот же что сервер)  │
│  - BuyItemViaServer() → NetworkPlayer.TradeBuyServerRpc()│
│  - OnTradeResult() → SyncWarehouseItem/RemoveFromWarehouse│
└────────────────────────┬────────────────────────────────┘
                         │ RPC [SendTo.Server]
                         ▼
┌─────────────────────────────────────────────────────────┐
│              СЕРВЕР (TradeMarketServer)                   │
│  - LoadAllMarkets() → ScriptableObject LocationMarket    │
│  - BuyItemServerRpc():                                   │
│    1. Rate limit (отключён для отладки)                  │
│    2. Проверка рынка, товара, стока                      │
│    3. Проверка кредитов (PlayerCreditsManager)           │
│    4. Проверка лимитов склада                            │
│    5. Списать кредиты, обновить рынок, добавить товар    │
│    6. playerStorage.Save()                               │
│    7. SendTradeResultToClient(itemId, qty, isPurchase)   │
│    8. SendMarketUpdateClientRpc (обновить UI цены)       │
└─────────────────────────────────────────────────────────┘
```

### Что было изменено (файлы)

| Файл | Изменения |
|------|-----------|
| `TradeUI.cs` | Убран fallback, lazy Player, GetPlayerStorageFromNetworkPlayer(), SyncWarehouseItem(), RemoveFromWarehouse(), debounce |
| `TradeMarketServer.cs` | InitItems+RecalculatePrices в LoadAllMarkets, Save() после покупки/продажи, null guard, rate limit = 0 |
| `NetworkPlayer.cs` | TradeResultClientRpc расширен параметрами |
| `MarketItem.cs` | Warning при item == null, currentPrice = 0 |
| `TradeMarketServer.prefab` | maxTradesPerMinute: 10 → 0 |

---

## 🚀 СЛЕДУЮЩИЕ ШАГИ (Сессия 8D)

1. **Исправить двойные RPC** — убрать Enter из HandleInput для торговли, оставить только кнопки
2. **Починить цену = 0** — добавить подробный лог в `RecalculatePrice()`, проверить что `item` не null
3. **Проверить Tick систему** — возможно она обнуляет цены на сервере
4. **Протестировать полный цикл:** покупка → склад → погрузка → полёт → сдача контракта

---

## 📊 Коммиты

```
┌─────────┬──────────────────────────────────────────────────────┐
│ Хеш     │ Описание                                             │
├─────────┼──────────────────────────────────────────────────────┤
│ 926dfb8 │ fix(session 8C): аудит — debounce, rate limit, null  │
│ ed8b517 │ fix(session 8C): единый PlayerTradeStorage           │
│ acde03c │ fix(session 8C): RemoveFromWarehouse                 │
│ 03ef26d │ fix(session 8C): отладочные логи + rate limit        │
│ 0428109 │ fix(session 8C): ленивый поиск NetworkPlayer         │
│ e87f8e6 │ fix(session 8C): убрать fallback на локальную покупку│
└─────────┴──────────────────────────────────────────────────────┘
```

**ВАЖНО:** Коммиты НЕ запушены! Требуется тестирование и fix двойных RPC + price=0 перед пушем.
