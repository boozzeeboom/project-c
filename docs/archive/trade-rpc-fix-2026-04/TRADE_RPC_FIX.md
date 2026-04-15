# Trade RPC Fix — ClientRpcParams Solution

**Дата:** 15.04.2026 | **Статус:** Готово к имплементации

---

## Проблема

На хосте (`Host`) `ClientRpc` с `SendTo.Owner` ДУБЛИРУЕТСЯ на все `NetworkPlayer` с `IsOwner=True`.
На хосте только `NetworkPlayer(0)` имеет `IsOwner=True`, поэтому:
- RPC для `clientId=1` отправляется на `NetworkPlayer(0)` (локальный)
- `IsOwner` на `NetworkPlayer(0)` = `True` (для clientId=0)

---

## Решение

Использовать `ClientRpcParams` с явным `TargetClientIds` для отправки RPC ТОЛЬКО нужному клиенту.

---

## Изменения в коде

### 1. NetworkPlayer.cs — Обновлённый RPC

```csharp
// Assets/_Project/Scripts/Player/NetworkPlayer.cs

/// <summary>
/// Результат торговли — сервер отправляет конкретному клиенту.
/// 
/// FIX (15.04.2026): Используем ClientRpcParams с TargetClientIds для 
/// надёжной доставки на хосте.
/// </summary>
[ClientRpc]
public void TradeResultClientRpc(
    ulong targetClientId, 
    bool success, 
    string message, 
    float newCredits, 
    string itemId = "", 
    int itemQuantity = 0, 
    bool isPurchase = true,
    ClientRpcParams rpcParams = default)
{
    ulong localClientId = NetworkManager.Singleton.LocalClientId;
    Debug.Log($"[NetworkPlayer] TradeResultClientRpc: targetClientId={targetClientId}, localClientId={localClientId}, success={success}");
    
    // Фильтрация по targetClientId — ДОПОЛНИТЕЛЬНАЯ защита
    if (localClientId != targetClientId)
    {
        Debug.Log($"[NetworkPlayer] Этот клиент ({localClientId}) НЕ целевой ({targetClientId}), пропускаю");
        return;
    }
    
    if (TradeUI.Instance != null)
    {
        Debug.Log($"[NetworkPlayer] Вызываю OnTradeResult для клиента {targetClientId}");
        TradeUI.Instance.OnTradeResult(success, message, newCredits, itemId, itemQuantity, isPurchase);
    }
    else
    {
        Debug.LogWarning($"[NetworkPlayer] TradeUI.Instance == null!");
    }
}
```

### 2. TradeMarketServer.cs — Отправка с таргетом

```csharp
// Assets/_Project/Trade/Scripts/TradeMarketServer.cs

/// <summary>
/// Отправить результат торговли конкретному клиенту.
/// 
/// FIX (15.04.2026): Используем ClientRpcParams с TargetClientIds для 
/// надёжной доставки на хосте.
/// </summary>
private void SendTradeResultToClient(ulong clientId, bool success, string message,
    float newCredits, int newStock, int newCargoWeight, int newCargoVolume, int newCargoSlots,
    string itemId = "", int itemQuantity = 0, bool isPurchase = true)
{
    var nm = NetworkManager.Singleton;
    if (nm == null)
    {
        Debug.LogWarning("[TMS] NetworkManager.Singleton == null!");
        return;
    }
    
    Debug.Log($"[TMS] SendTradeResult: clientId={clientId}, success={success}, newCredits={newCredits}");
    
    // Находим NetworkPlayer для отправки RPC
    var player = FindPlayerNetworkPlayer(clientId);
    if (player == null)
    {
        Debug.LogError($"[TMS] FindPlayerNetworkPlayer({clientId}) вернул null!");
        return;
    }
    
    // СОЗДАЁМ ClientRpcParams С ЯВНЫМ ТАРГЕТОМ
    var rpcParams = new ClientRpcParams
    {
        Send = new ClientRpcSendParams
        {
            TargetClientIds = new ulong[] { clientId }
        }
    };
    
    Debug.Log($"[TMS] Отправляю TradeResultClientRpc клиенту {clientId} с TargetClientIds");
    player.TradeResultClientRpc(clientId, success, message, newCredits, 
        itemId, itemQuantity, isPurchase, rpcParams);
}
```

### 3. Проверка на хосте (дополнительная защита)

В `SendTradeResultToClient` можно добавить проверку для случая когда клиент=хост:

```csharp
// Дополнительная проверка: если clientId == LocalClientId на хосте,
// вызываем напрямую без RPC
if (nm.IsHost && clientId == nm.LocalClientId)
{
    Debug.Log($"[TMS] Отправляю результат напрямую (локальный клиент)");
    if (TradeUI.Instance != null)
    {
        TradeUI.Instance.OnTradeResult(success, message, newCredits, itemId, itemQuantity, isPurchase);
    }
    return;
}
```

---

## Диаграмма работы

```
БЕЗ ClientRpcParams:
┌─────────────────────────────────────────────────────────────────┐
│  TradeMarketServer → TradeResultClientRpc()                     │
│  [Rpc: SendTo.Owner]                                            │
│                                                                 │
│  → Хост получает на NetworkPlayer(0) (IsOwner=True)            │
│    └── IsOwner для clientId=0 → обрабатывает ✓                 │
│                                                                 │
│  → Клиент получает на NetworkPlayer(1) (IsOwner=True)          │
│    └── IsOwner для clientId=1 → НЕ обрабатывает ✗             │
│                                                                 │
│  ❌ Клиент НЕ получает результат!                               │
└─────────────────────────────────────────────────────────────────┘

С ClientRpcParams + TargetClientIds:
┌─────────────────────────────────────────────────────────────────┐
│  TradeMarketServer → TradeResultClientRpc(                      │
│      rpcParams: TargetClientIds = [1])                          │
│  [Rpc: SendTo.Owner] + TargetClientIds                          │
│                                                                 │
│  → Хост: проверяет TargetClientIds=[1] vs LocalClientId=0      │
│    └── [1] != [0] → НЕ получает                                │
│                                                                 │
│  → Клиент: проверяет TargetClientIds=[1] vs LocalClientId=1    │
│    └── [1] == [1] → ПОЛУЧАЕТ                                   │
│                                                                 │
│  ✅ Клиент получает результат!                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Тестирование

### Тест-кейс 1: Host покупает (должен работать)

```
1. Host открывает TradeUI
2. Host покупает mesium_canister_v01
3. Проверка: склад хоста обновился
4. Проверка: кредиты хоста уменьшились
```

### Тест-кейс 2: Client покупает (должен работать после фикса)

```
1. Host запускает игру как сервер
2. Client подключается
3. Client открывает TradeUI
4. Client покупает mesium_canister_v01
5. Проверка: склад клиента обновился
6. Проверка: кредиты клиента уменьшились
```

### Тест-кейс 3: Множественные операции

```
1. Client покупает item A x5
2. Client продаёт item B x3
3. Host покупает item C x2
4. Проверка: у каждого свои данные
```

---

## Логи для отладки

```
// Ожидаемые логи после фикса:

[TMS] SendTradeResult: clientId=1, success=True, newCredits=990
[TMS] FindPlayerNetworkPlayer(1): найден через ConnectedClients, OwnerClientId=1
[TMS] Отправляю TradeResultClientRpc клиенту 1 с TargetClientIds
[NetworkPlayer] TradeResultClientRpc: targetClientId=1, localClientId=1, success=True
[NetworkPlayer] Вызываю OnTradeResult для клиента 1
[TradeUI] OnTradeResult: success=True, credits=990, itemId=mesium_canister_v01, qty=1
```

---

## Связанные файлы

- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
- `Assets/_Project/Trade/Scripts/TradeMarketServer.cs`
- `Assets/_Project/Trade/Scripts/TradeUI.cs`
