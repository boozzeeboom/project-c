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

### 1. Двойные RPC (покупка/продажа x2 за один клик) — ИСПРАВЛЕНО
**Симптом:** Каждый клик по кнопке вызывал 2 RPC вызова.
**Причина:** Unity UI Button onClick + EventSystem могли сработать дважды в одном кадре. Time-based debounce не помогал — оба вызова были до установки `_lastTradeTime`.
**Решение (применено):**
- Boolean-флаг `_tradePending` — устанавливается ДО вызова RPC
- `Invoke(ResetTradePending, 0.3f)` — сброс через 300ms
- `EventSystem.SetSelectedGameObject(null)` — снимает выделение с кнопки
- Enter/Shift+Enter убраны из HandleInput() — покупка ТОЛЬКО кнопками мыши

### 2. Price = 0 CR (качели цен сломаны) — ДИАГНОСТИРОВАНО
**Симптом:** Первый buy = 10 CR (ok), второй = 0 CR. Продажа = 8 CR (ok), следующая = 0 CR.
**Debug показал:** `item=TradeItem_Mesium_v01, basePrice=10, currentPrice=10` — НОРМАЛЬНО при первой сделке.
Но после первой сделки `marketItem.currentPrice` на сервере становится 0.
**Гипотеза:** Tick система (MarketTick → DecaySupplyAndDemand → RecalculatePrices) обнуляет цену между сделками, т.к. `MarketItem.item` reference теряется при десериализации ScriptableObject.
**Статус:** Добавлен принудительный `RecalculatePrice()` перед каждой сделкой + подробный debug лог.

### 3. Склад показывает завышенное кол-во
**Симптом:** `RemoveFromWarehouse: mesium_canister_v01 x1 (было: 4)` — 4 вместо ожидаемых 1-2.
**Причина:** Двойные RPC удваивали покупки → склад накапливал лишнее. Fix #1 решит эту проблему.

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

1. **Протестировать фикс двойных RPC** — `_tradePending` + `Invoke` — должно работать
2. **Прочитать DEBUG BUY/SELL логи** — найти момент когда currentPrice становится 0
3. **Проверить Tick систему** — остановить tickInterval (testMode=false) и проверить влияет ли она на цену
4. **Если price=0 остаётся** — проверить `MarketItem.item` reference напрямую в ScriptableObject

---

## 📊 Коммиты

```
┌─────────┬──────────────────────────────────────────────────────┐
│ Хеш     │ Описание                                             │
├─────────┼──────────────────────────────────────────────────────┤
│ 67ac3ca │ fix(session 8C): убрать двойные RPC + debug цен      │
│ 7cf5bf1 │ docs: итоги сессии 8C + план сессии 8D               │
│ 926dfb8 │ fix(session 8C): аудит — debounce, rate limit, null  │
│ ed8b517 │ fix(session 8C): единый PlayerTradeStorage           │
│ acde03c │ fix(session 8C): RemoveFromWarehouse                 │
│ 03ef26d │ fix(session 8C): отладочные логи + rate limit        │
│ 0428109 │ fix(session 8C): ленивый поиск NetworkPlayer         │
│ e87f8e6 │ fix(session 8C): убрать fallback на локальную покупку│
└─────────┴──────────────────────────────────────────────────────┘
```

**ВАЖНО:** Коммиты НЕ запушены! Требуется тестирование.
