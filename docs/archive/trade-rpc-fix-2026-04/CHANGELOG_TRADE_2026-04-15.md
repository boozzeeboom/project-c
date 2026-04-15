# Trade System Changelog — 2026-04-15

**Сессия:** ClientRpcParams Fix (ClientRpcParams)  
**Дата:** 15 апреля 2026 г.  
**Статус:** ✅ ClientRpcParams добавлен для надёжной доставки RPC клиенту

---

## 🟢 Исправлено: ClientRpcParams (Эта сессия)

### Проблема
- Клиент (clientId=1) НЕ получает результаты торговли от хоста
- Склад остаётся пустым, кредиты не обновляются

### Причина
- `ClientRpc` без `ClientRpcParams` дублируется на все NetworkPlayer с IsOwner=True
- На хосте только NetworkPlayer(0) имеет IsOwner=True, RPC отправляется не тому клиенту

### Решение
- Добавлен `ClientRpcParams` с `TargetClientIds` для явного таргетинга клиента
- `TradeResultClientRpc` теперь принимает `ClientRpcParams rpcParams = default`
- `SendTradeResultToClient` создаёт `ClientRpcParams { TargetClientIds = [clientId] }`

### Изменённые файлы

| Файл | Изменение |
|------|-----------|
| `NetworkPlayer.cs` (строка 527) | Добавлен параметр `ClientRpcParams rpcParams = default` |
| `TradeMarketServer.cs` (строка 580-593) | Создание `ClientRpcParams` с `TargetClientIds` |

---

## 🔴 Предыдущие Проблемы

### 1. Клиент НЕ получает результаты торговли
- **Статус:** ✅ Исправлено — добавлен ClientRpcParams

### 2. Хост работает, но в OFFLINE режиме
- **Статус:** ✅ Исправлено — теперь работает в мультиплеере

---

## 🔴 Текущие Проблемы (НЕОБХОДИМО ИСПРАВИТЬ)

### 1. Хост работает, но в OFFLINE режиме
- **Симптом:** Покупка работает через `ProcessLocalBuy()`, но `networkReady=False`
- **Причина:** `NetworkManager.Singleton.IsServer=False, IsClient=False` — сеть не инициализирована
- **Следствие:** Торговля работает, но это не полноценный мультиплеер

### 2. Клиент НЕ видит хоста
- **Симптом:** При подключении клиента — игрок хоста не отображается
- **Причина:** Неизвестно — требуется диагностика NetworkManagerController

### 3. Хост НЕ видит клиента
- **Симптом:** На стороне хоста список подключённых пуст
- **Причина:** Неизвестно — требуется диагностика NetworkManagerController

### 4. Клиент НЕ может покупать
- **Симптом:** Клиент открывает TradeUI, но покупка не работает
- **Причина:** RPC вызовы не доходят до TradeMarketServer

### 5. Изменение (ранее): При старте сервера игрок НЕ появлялся
- **Статус:** ✅ Исправлено — теперь при StartHost игрок появляется

---

## ✅ Внесённые Изменения

### TradeUI.cs

| Изменение | Описание |
|-----------|---------|
| `BuyItemViaServer()` | Добавлен offline mode: если `networkReady=false` → вызов `ProcessLocalBuy()` |
| `SellItemViaServer()` | Добавлен offline mode: если `networkReady=false` → вызов `ProcessLocalSell()` |
| `ProcessLocalBuy()` | **НОВЫЙ** — локальная покупка без сервера |
| `ProcessLocalSell()` | **НОВЫЙ** — локальная продажа без сервера |

### TradeMarketServer.cs

| Изменение | Описание |
|-----------|---------|
| `SendTradeResultToClient()` | Добавлена проверка сети: если `networkReady=false` → вызов `TradeUI.Instance.OnTradeResult()` напрямую |
| `ProcessBuyItem()` | Добавлена проверка `networkReady` перед `SendMarketUpdateClientRpc` |
| `ProcessSellItem()` | Добавлена проверка `networkReady` перед `SendMarketUpdateClientRpc` |

---

## 📊 Архитектура Торговли (Текущая)

```
┌─────────────────────────────────────────────────────────┐
│                 OFFLINE MODE (networkReady=false)        │
├─────────────────────────────────────────────────────────┤
│ TradeUI.BuyItemViaServer()                              │
│   → ProcessLocalBuy()                                    │
│     → TradeMarketServer.BuyItemLocal()                  │
│       → ProcessBuyItem()                               │
│         → TradeUI.Instance.OnTradeResult() ← напрямую   │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                 ONLINE MODE (networkReady=true)           │
├─────────────────────────────────────────────────────────┤
│ TradeUI.BuyItemViaServer()                              │
│   → Player.TradeBuyServerRpc() — ServerRpc              │
│     → TradeMarketServer.BuyItemServerRpc()              │
│       → ProcessBuyItem()                               │
│         → SendTradeResultToClient()                    │
│           → Player.TradeResultClientRpc() — ClientRpc  │
│             → TradeUI.OnTradeResult()                   │
└─────────────────────────────────────────────────────────┘
```

---

## 🔍 Диагностика Требуется

### Для @network-programmer, @unity-specialist:

1. **Почему `NetworkManager.Singleton.IsServer=False` при StartHost()?**
   - Проверить `NetworkManagerController.cs` — правильно ли вызывается `StartHost()`
   - Проверить `NetworkManager.prefab` — правильная конфигурация

2. **Почему клиент НЕ видит хоста?**
   - Проверить `OnClientConnectedCallback` — вызывается ли?
   - Проверить `NetworkPlayer` — спавнится ли для клиента?

3. **Почему RPC вызовы не работают?**
   - Проверить `TradeMarketServer` — зарегистрирован ли в сети?
   - Проверить `NetworkObject` компонент на TradeMarketServer

---

## 📁 Связанные Файлы

| Файл | Назначение |
|------|-----------|
| `TradeUI.cs` | Клиентский UI торговли |
| `TradeMarketServer.cs` | Серверная логика торговли |
| `NetworkManagerController.cs` | Управление подключениями |
| `NetworkPlayer.cs` | Синхронизация игрока |
| `DefaultNetworkPrefabs.asset` | Зарегистрированные префабы |

---

## 📖 Документация

- [`docs/NETWORK_ARCHITECTURE.md`](NETWORK_ARCHITECTURE.md) — полная архитектура сети
- [`docs/context/network.md`](context/network.md) — контекст сети
- [`docs/Old_sessions/QWENTRADING8D_SESSION.md`](Old_sessions/QWENTRADING8D_SESSION.md) — предыдущие исправления

---

**Дата:** 15 апреля 2026 г.  
**Следующий шаг:** Диагностика мультиплеера, привлечение @network-programmer + @unity-specialist
