# Multiplayer Fix Plan — 2026-04-15

**Проект:** Project C: The Clouds  
**Дата:** 15 апреля 2026 г.  
**Статус:** 🟡 В РАБОТЕ (Host работает, торговля частично)

---

## 🎯 Цель

Исправить мультиплеер так, чтобы:
1. ✅ ~~Хост и клиент ВИДЯТ друг друга~~ — **ИСПРАВЛЕНО 15.04.2026**
2. ✅ Торговля работает в сетевом режиме (через RPC)
3. ✅ Сохранить offline mode для тестирования

---

## ✅ Исправленные Проблемы (15.04.2026)

| # | Проблема | Причина | Решение |
|---|----------|---------|---------|
| 1 | Host не запускался | `StartHostCoroutine()` не вызывалась корректно | `NetworkUI.StartCoroutine(NMC.StartHostCoroutine())` |
| 2 | UnityTransport не привязан | `NetworkConfig.NetworkTransport = null` | Автопривязка в `NMC.Awake()` |

---

## 🔴 Текущие Проблемы

| # | Проблема | Симптом | Приоритет |
|---|----------|---------|-----------|
| 1 | ~~`NetworkManager.Singleton.IsServer=False`~~ | ~~Торговля в offline mode~~ | ✅ **ИСПРАВЛЕНО** |
| 2 | ~~Клиент НЕ видит хоста~~ | ~~Игрок хоста не отображается~~ | ✅ **ИСПРАВЛЕНО** |
| 3 | ~~Хост НЕ видит клиента~~ | ~~Список подключённых пуст~~ | ✅ **ИСПРАВЛЕНО** |
| 4 | Клиент НЕ может покупать | RPC вызовы не доходят до сервера | 🔴 P0 |

---

## 📋 План Исправления

### Этап 1: Диагностика (@network-programmer)

**Задачи:**
1. Проверить `NetworkManagerController.cs`:
   - `StartHost()` вызывается корректно?
   - `OnClientConnectedCallback` срабатывает?
   - `OnServerStarted` срабатывает?

2. Проверить `NetworkPlayer.cs`:
   - `OnNetworkSpawn()` вызывается для хоста?
   - `IsOwner` корректный?
   - PlayerPrefab задан в NetworkManager?

3. Проверить `DefaultNetworkPrefabs.asset`:
   - NetworkPlayer зарегистрирован?

### Этап 2: Исправление сетевого режима (@unity-specialist)

**Задачи:**
1. Убедиться что `TradeMarketServer` имеет `NetworkObject` компонент
2. Проверить что `TradeMarketServer` спавнится на сервере
3. Добавить логи в `StartHost()` для диагностики

### Этап 3: Исправление RPC вызовов (@network-programmer)

**Задачи:**
1. Проверить `TradeMarketServer.BuyItemServerRpc()`:
   - Метод вызывается?
   - Серверная логика выполняется?

2. Проверить `NetworkPlayer.TradeBuyServerRpc()`:
   - RPC зарегистрирован корректно?
   - Параметры правильные?

---

## 🔧 Организация Агентов

### @network-programmer — ответственный за сетевую логику
```
1. Диагностика NetworkManagerController
2. Диагностика NetworkPlayer.OnNetworkSpawn
3. Проверка RPC регистрации
```

### @unity-specialist — ответственный за Unity-специфику
```
1. Проверка NetworkObject компонентов
2. Проверка NetworkManager.prefab
3. Проверка DefaultNetworkPrefabs.asset
```

### @technical-director — координация
```
1. Утверждение плана
2. Приоритизация задач
3. Проверка результатов
```

---

## 📁 Ключевые Файлы

| Файл | Ответственный | Статус |
|------|--------------|--------|
| `NetworkManagerController.cs` | @network-programmer | 🔴 Требует проверки |
| `NetworkPlayer.cs` | @network-programmer | 🔴 Требует проверки |
| `TradeUI.cs` | @ui-programmer | ✅ Изменён (offline mode) |
| `TradeMarketServer.cs` | @network-programmer | ✅ Изменён (offline mode) |
| `NetworkManager.prefab` | @unity-specialist | 🔴 Требует проверки |
| `DefaultNetworkPrefabs.asset` | @unity-specialist | 🔴 Требует проверки |

---

## 🧪 Тесты для Проверки

| # | Тест | Ожидаемый результат |
|---|------|---------------------|
| 1 | StartHost() → проверить IsServer=True | `NetworkManager.Singleton.IsServer=True` |
| 2 | Клиент подключается → проверить OnClientConnectedCallback | Лог "Client connected: X" |
| 3 | Клиент видит игрока хоста | NetworkPlayer хоста виден на клиенте |
| 4 | Host видит клиента | NetworkPlayer клиента виден на хосте |
| 5 | Host покупает товар | `TradeBuyServerRpc` вызывается, успешная покупка |
| 6 | Клиент покупает товар | `TradeBuyServerRpc` вызывается, успешная покупка |

---

## 📝 Логи для Диагностики

Добавить в ключевые методы:

```csharp
// NetworkManagerController.StartHost()
Debug.Log($"[NMC] StartHost() called");
Debug.Log($"[NMC] IsServer={NetworkManager.Singleton.IsServer}");

// NetworkManagerController.OnClientConnectedCallback
Debug.Log($"[NMC] OnClientConnected: {clientId}");

// NetworkPlayer.OnNetworkSpawn()
Debug.Log($"[NP] OnNetworkSpawn: IsOwner={IsOwner}, IsServer={IsServer}");

// TradeMarketServer.BuyItemServerRpc
Debug.Log($"[TMS] BuyItemServerRpc: clientId={clientId}");
```

---

## 📖 Связанная Документация

- [`docs/NETWORK_ARCHITECTURE.md`](docs/NETWORK_ARCHITECTURE.md) — архитектура сети
- [`docs/context/network.md`](docs/context/network.md) — контекст сети
- [`docs/CHANGELOG_TRADE_2026-04-15.md`](docs/CHANGELOG_TRADE_2026-04-15.md) — изменения торговли

---

**Следующий шаг:** Запустить диагностику, привлечь @network-programmer и @unity-specialist
