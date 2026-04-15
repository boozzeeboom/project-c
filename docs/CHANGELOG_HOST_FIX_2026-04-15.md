# Host Fix — 2026-04-15

## Дата
15 апреля 2026 г.

## Проблема
Кнопка "Host" в `NetworkUI` не вызывала `StartHost()` в `NetworkManagerController`. Клиенты не видели хоста.

## Корневая причина
`NetworkUI.OnStartHostClicked()` вызывала `networkManagerController.StartHost()`, но:
1. `StartHost()` запускал корутину через `StartHostCoroutine()` внутри NMC
2. Корутина не выполнялась корректно из-за проблем с порядком вызова

## Решение

### Изменение 1: NetworkUI.cs
```csharp
// БЫЛО:
networkManagerController.StartHost();

// СТАЛО:
StartCoroutine(networkManagerController.StartHostCoroutine());
```

### Изменение 2: NetworkManagerController.cs
```csharp
// StartHostCoroutine() сделан public
public IEnumerator StartHostCoroutine()
{
    // Защита от конфликта порта
    if (networkManager.IsListening)
    {
        Debug.LogWarning("[Network] Already listening! Shutting down first...");
        networkManager.Shutdown();
        yield return new WaitForSecondsRealtime(0.25f);
    }

    try
    {
        Debug.Log("[NMC] StartHost() called, starting host...");
        networkManager.StartHost();
        
        Debug.Log($"[NMC] After StartHost: IsServer={networkManager.IsServer}, IsClient={networkManager.IsClient}, IsHost={networkManager.IsHost}, IsListening={networkManager.IsListening}");
        
        UpdateStatus("Хост запущен");
    }
    catch (Exception ex)
    {
        Debug.LogError($"[Network] Failed to start host: {ex.Message}");
        UpdateStatus("Ошибка запуска хоста - порт занят?");
    }
}
```

### Изменение 3: NetworkManagerController.cs — Awake()
```csharp
// Автоматическая проверка и добавление UnityTransport
var transport = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
if (transport == null)
{
    Debug.LogWarning("[NMC] UnityTransport not found. Adding...");
    transport = gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
}

// Привязка транспорта к NetworkConfig
if (networkManager.NetworkConfig.NetworkTransport == null)
{
    networkManager.NetworkConfig.NetworkTransport = transport;
}
```

## Добавленная диагностика
- `[NMC] Awake() called`
- `[NMC] NetworkManager component found!`
- `[NMC] UnityTransport found!`
- `[NMC] Start() - NetworkManager.Singleton=SET`
- `[NMC] StartHost() called, starting host...`
- `[NMC] After StartHost: IsServer=?, IsClient=?, IsHost=?, IsListening=?`
- `[NMC] HandleClientConnected: clientId=?, IsServer=?, IsClient=?`
- `[NMC] ConnectedClients.Count=?`

## Файлы изменены
| Файл | Изменения |
|------|-----------|
| `Assets/_Project/Scripts/UI/NetworkUI.cs` | Прямой вызов StartHostCoroutine() |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | StartHostCoroutine public, автопривязка UnityTransport |

## Результат
✅ Host запускается успешно
✅ Клиенты подключаются к хосту
✅ `ConnectedClients.Count` увеличивается
✅ RPC вызовы проходят (`BuyItemServerRpc`)

## Статус
🟢 **ИСПРАВЛЕНО**

---

**Следующий шаг:** Проверить почему клиент не может покупать/продавать на рынке (TradeMarketServer).

---

## ✅ Исправление Trade RPC — 15.04.2026

### Проблема
Клиент с `clientId=1` покупал товар, но:
- `SenderClientId` был `0` (сервер), а не `1` (клиент)
- Деньги списывались у сервера, а не у клиента
- Товар не попадал на склад клиента

### Причина
Цепочка вызова:
```
Клиент (id=1) → NetworkPlayer.TradeBuyServerRpc() → TradeMarketServer.BuyItemServerRpc()
```

Но `TradeBuyServerRpc` вызывала `BuyItemServerRpc` **БЕЗ** `rpcParams`:
```csharp
// БЫЛО:
TradeMarketServer.Instance.BuyItemServerRpc(itemId, quantity, locationId);
```

Поэтому `rpcParams.Receive.SenderClientId` был `0` (сервер).

### Решение

**1. NetworkPlayer.cs — передать rpcParams:**
```csharp
// СТАЛО:
TradeMarketServer.Instance.BuyItemServerRpc(itemId, quantity, locationId, rpcParams);
```

**2. TradeMarketServer.cs — RequireOwnership = false:**
```csharp
[ServerRpc(RequireOwnership = false)]
public void BuyItemServerRpc(...)
```

### Файлы изменены
| Файл | Изменения |
|------|----------|
| `NetworkPlayer.cs` | `BuyItemServerRpc` и `SellItemServerRpc` передают `rpcParams` |
| `TradeMarketServer.cs` | `BuyItemServerRpc` и `SellItemServerRpc` имеют `RequireOwnership = false` |

### Ожидаемый результат
- Клиент (id=1) покупает → `SenderClientId = 1` ✅
- Деньги списываются у клиента ✅
- Товар попадает на склад клиента ✅

---

## ✅ Исправление Trade RPC v2 — 15.04.2026

### Проблема
`rpcParams` теряется при вызове через другой `NetworkBehaviour`:
```
Клиент (id=1) → TradeBuyServerRpc() на NetworkPlayer → BuyItemServerRpc() на TradeMarketServer
                                                              ↑
                                               rpcParams = default (SenderClientId = 0)
```

### Решение: передача OwnerClientId как параметр
```csharp
// TradeMarketServer.cs — новый параметр senderClientId
[Rpc(SendTo.Server)]
public void BuyItemServerRpc(string itemId, int quantity, string locationId, ulong senderClientId)
{
    ulong clientId = IsServer ? senderClientId : NetworkManager.Singleton.LocalClientId;
    ProcessBuyItem(itemId, quantity, locationId, clientId);
}

// NetworkPlayer.cs — передаём свой OwnerClientId
TradeMarketServer.Instance.BuyItemServerRpc(itemId, qty, locId, OwnerClientId);
```

### Логи после исправления
```
[TMS] BuyItemServerRpc: clientId=1  ← правильный клиент!
[TMS] GetPlayerCredits: clientId=1, credits=?  ← деньги клиента
```

### Файлы изменены
| Файл | Изменения |
|------|----------|
| `TradeMarketServer.cs` | `BuyItemServerRpc` и `SellItemServerRpc` — параметр `ulong senderClientId` |
| `NetworkPlayer.cs` | Передаёт `OwnerClientId` в вызовах |

---

## ✅ PlayerPrefs Bug Fix — 15.04.2026 (Сессия 8G)

### Проблема
Кредиты клиента НЕ обновлялись после покупки/продажи. Клиент видел старые кредиты.

### Корневая причина
**PlayerPrefs — локальное хранилище каждой машины!**

Цепочка:
1. Сервер обрабатывает покупку → `SetCredits(clientId, newCredits)` → записывает в **свои** PlayerPrefs
2. Сервер отправляет `TradeResultClientRpc(newCredits)` клиенту
3. Клиент получает `newCredits`, но затем вызывает `LoadFromPlayerDataStore()`
4. `LoadFromPlayerDataStore()` читает из **клиентских** PlayerPrefs (которые НИКОГДА не обновлялись сервером!)
5. Клиентские кредиты остаются старыми

```csharp
// TradeUI.cs — OnTradeResult()
playerStorage.LoadFromPlayerDataStore(clientId); // ← ПЕРЕЗАТИРАЕТ newCredits!
playerStorage.credits = newCredits; // ← Эта строка ничего не даёт
```

### Решение
Убрать вызов `LoadFromPlayerDataStore()` в `OnTradeResult()`. Сервер уже отправил актуальные `newCredits` — используем их напрямую.

```csharp
// TradeUI.cs — OnTradeResult() — ИСПРАВЛЕНО
if (playerStorage != null)
{
    // СЕССИЯ 8G FIX: НЕ вызываем LoadFromPlayerDataStore()!
    // PlayerPrefs локальны для каждой машины
    playerStorage.credits = newCredits;
    playerStorage.Save();
    UpdateDisplays();
    RenderItems();
}
```

### Логи после исправления
```
// До: кредиты не обновлялись
[TradeUI] OnTradeResult: success=True, newCredits=950, но UI показывал 1000

// После: кредиты корректны
[TradeUI] OnTradeResult: success=True, newCredits=950, UI показывает 950 ✅
```

### Файлы изменены
| Файл | Изменения |
|------|----------|
| `Assets/_Project/Trade/Scripts/TradeUI.cs` | Убран `LoadFromPlayerDataStore()` в `OnTradeResult()` (строка ~1063) |

### Статус
🟢 **ИСПРАВЛЕНО** — кредиты клиента корректно обновляются после покупки/продажи

---

## ✅ Warehouse Sync Fix — 15.04.2026 (Сессия 8G)

### Проблема
После покупки/продажи **клиент** не видел купленные/проданные товары на вкладке склада. **Хост** видел, клиент — нет.

### Корневая причина
`OnTradeResult` получал `itemId` и `itemQuantity` от сервера, но **не использовал** их для обновления клиентского склада!

```csharp
// БЫЛО: OnTradeResult()
playerStorage.credits = newCredits; // Кредиты обновлялись
// itemId и itemQuantity — игнорировались! Склад не обновлялся.
```

### Решение
Добавить вызов `SyncWarehouseItem`/`RemoveFromWarehouse` в `OnTradeResult`:

```csharp
// TradeUI.cs — OnTradeResult() — ИСПРАВЛЕНО
if (!string.IsNullOrEmpty(itemId) && itemQuantity > 0)
{
    if (isPurchase)
    {
        // Купили — добавляем товар на склад
        SyncWarehouseItem(itemId, itemQuantity);
    }
    else
    {
        // Продали — удаляем товар со склада
        RemoveFromWarehouse(itemId, itemQuantity);
    }
}
```

### Файлы изменены
| Файл | Изменения |
|------|----------|
| `Assets/_Project/Trade/Scripts/TradeUI.cs` | Добавлены вызовы `SyncWarehouseItem`/`RemoveFromWarehouse` в `OnTradeResult()` |

### Статус
🟢 **ИСПРАВЛЕНО** — клиент теперь видит купленные/проданные товары на складе
