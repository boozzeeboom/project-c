# Trade System Multiplayer Test Guide — 2026-04-15

**Проект:** Project C: The Clouds  
**Дата:** 15 апреля 2026 г.  
**Статус:** 🔴 Требуется тестирование

---

## 🎯 Цель Тестирования

Проверить что:
1. ✅ Offline mode работает (без сети)
2. ✅ Host+Client режим работает (сеть активна)
3. ✅ RPC вызовы проходят корректно

---

## 📋 Подготовка к Тесту

### 1. Откройте проект в Unity

```bash
cd c:\UNITY_PROJECTS\ProjectC_client
code .
```

### 2. Запустите два Unity Editor

**Окно 1 (Хост):**
- Play Mode
- Нажмите **Host** в NetworkUI

**Окно 2 (Клиент):**
- Play Mode  
- Нажмите **Client** и подключитесь к `127.0.0.1:7777`

---

## 🔍 Ожидаемые Логи

### Хост — StartHost()

```
[NMC] StartHost() called, starting host...
[NMC] After StartHost: IsServer=True, IsClient=True, IsHost=True, IsListening=True
```

### Клиент — ConnectToServer()

```
[NMC] StartClient() called
[NMC] After StartClient: IsServer=False, IsClient=True, IsListening=False
```

### Хост — HandleClientConnected()

```
[NMC] HandleClientConnected: clientId=1, IsServer=True, IsClient=True
[NMC] ConnectedClients.Count=2
```

### TradeUI — BuyItemViaServer()

```
[TradeUI] BuyItemViaServer: networkReady=True, IsServer=True, IsHost=True, IsClient=True
```

### TradeMarketServer — BuyItemServerRpc()

```
[TMS] BuyItemServerRpc: clientId=0, IsServer=True, IsHost=True, IsClient=True
```

---

## ⚠️ Проблемы и Решения

### Проблема 1: `IsServer=False` после StartHost()

**Симптом:**
```
[NMC] After StartHost: IsServer=False, IsClient=False, IsHost=False, IsListening=False
```

**Причина:** `NetworkManager.Singleton` возвращает null

**Решение:**
1. Проверить что `NetworkManager.prefab` существует на сцене
2. Проверить что `NetworkManagerController` имеет ссылку на `NetworkManager`

---

### Проблема 2: Клиент не видит хоста

**Симптом:**
```
[NMC] HandleClientConnected: clientId=1, IsServer=True, IsClient=True
[NMC] ConnectedClients.Count=1
```

**Причина:** Хост не видит клиента в `ConnectedClients`

**Решение:**
1. Проверить `OnClientConnectedCallback` подписку
2. Проверить `NetworkPlayer` — спавнится ли для клиента?

---

### Проблема 3: TradeUI в offline mode

**Симптом:**
```
[TradeUI] BuyItemViaServer: networkReady=False, IsServer=False, IsClient=False
```

**Причина:** `NetworkManager.Singleton.IsServer=False`

**Решение:**
1. Запустить как Host, а не как Server
2. Или проверить почему `NetworkManager.Singleton` возвращает неверное состояние

---

## 🧪 Тесты

### Тест 0: Проверка NMC (БЕЗ НАЖАТИЯ Host)

**Цель:** Проверить что `[NMC]` логи появляются

1. Запустить Play Mode (НЕ нажимать Host)
2. Проверить Console на наличие `[NMC]` логов

**Ожидаемые логи (без нажатия Host):**
```
[NMC] Awake() called
[NMC] NetworkManager component found!
[NMC] Awake complete. NetworkManager.Singleton=SET
```

**Если этих логов НЕТ:**
- `NetworkManagerController` не инициализирован
- Проверить что `NetworkManagerController` существует на сцене
- Проверить что `NetworkUI` компонент включен

---

### Тест 1: Offline Mode (без сети)

1. Запустить Play Mode без подключения к сети
2. Открыть TradeUI
3. Попробовать купить товар
4. **Ожидаемо:** Покупка работает через `ProcessLocalBuy()`
5. **Ожидаемо:** Лог `[TradeUI] BuyItemViaServer: networkReady=False`

### Тест 2: Host Mode

1. Запустить Play Mode
2. Нажать **Host**
3. Проверить логи StartHost
4. **Ожидаемо:** `IsServer=True, IsHost=True`
5. Открыть TradeUI
6. Попробовать купить товар
7. **Ожидаемо:** `networkReady=True`, покупка через `TradeMarketServer.Instance.BuyItemLocal()`

### Тест 3: Host + Client (мультиплеер)

1. Запустить два Unity Editor
2. Окно 1: Нажать **Host**
3. Окно 2: Нажать **Client**, подключиться к `127.0.0.1:7777`
4. Проверить логи HandleClientConnected в обоих окнах
5. Окно 1 (Хост): Открыть TradeUI, купить товар
6. **Ожидаемо:** `BuyItemServerRpc` вызывается, покупка проходит
7. Окно 2 (Клиент): Открыть TradeUI, купить товар
8. **Ожидаемо:** RPC доходит до сервера, покупка проходит

---

## 📝 Логи для Копирования

Скопируйте эти логи после теста:

```log
=== HOST ===
[NMC] StartHost() called
[NMC] After StartHost: IsServer=?, IsClient=?, IsHost=?, IsListening=?
[NMC] HandleClientConnected: clientId=?, IsServer=?, IsClient=?
[NMC] ConnectedClients.Count=?
[TradeUI] BuyItemViaServer: networkReady=?, IsServer=?, IsHost=?, IsClient=?
[TMS] BuyItemServerRpc: clientId=?, IsServer=?, IsHost=?, IsClient=?

=== CLIENT ===
[NMC] StartClient() called
[NMC] After StartClient: IsServer=?, IsClient=?, IsListening=?
[NMC] HandleClientConnected: clientId=?, IsServer=?, IsClient=?
[NMC] ConnectedClients.Count=?
[TradeUI] BuyItemViaServer: networkReady=?, IsServer=?, IsHost=?, IsClient=?
[TMS] BuyItemServerRpc: clientId=?, IsServer=?, IsHost=?, IsClient=?
```

---

## 📁 Изменённые Файлы

| Файл | Изменения |
|------|-----------|
| `NetworkManagerController.cs` | +Диагностика в StartHost, HandleClientConnected, ConnectToServer |
| `TradeMarketServer.cs` | +Диагностика в BuyItemServerRpc |
| `TradeUI.cs` | Уже имеет диагностику в BuyItemViaServer |
| `docs/CHANGELOG_TRADE_2026-04-15.md` | НОВЫЙ — документация изменений |
| `docs/world/LargeScaleMMO/MULTIPLAYER_FIX_PLAN_2026-04-15.md` | НОВЫЙ — план исправления |

---

**Дата:** 15 апреля 2026 г.  
**Следующий шаг:** Запустить тесты, предоставить логи для анализа
