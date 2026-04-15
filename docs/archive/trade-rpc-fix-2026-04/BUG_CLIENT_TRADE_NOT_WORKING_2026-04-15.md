# BUG: Client не получает результаты торговли (15.04.2026)

## Статус: НЕ РЕШЕНО

## Описание проблемы
При подключении клиента (clientId=1) к хосту (clientId=0):
1. Клиент может покупать и продавать ✓
2. Сервер корректно обрабатывает операции ✓
3. **Клиент НЕ получает результаты торговли** — склад остаётся пустым, кредиты не обновляются ✗

## Попытки исправления

### Сессия 8I — Упрощение ClientRpc
**Идея:** Убрать ClientRpcParams, использовать IsOwner внутри RPC.

```csharp
[ClientRpc]
public void TradeResultClientRpc(...)
{
    if (IsOwner && TradeUI.Instance != null)
        TradeUI.Instance.OnTradeResult(...);
}
```

**Результат:** Работает на хосте (IsOwner=True), НЕ работает для удалённого клиента (IsOwner=False).

---

### Сессия 8J — Поиск своего NetworkPlayer в TradeUI
**Идея:** `FindAnyObjectByType<NetworkPlayer>()` возвращает первого в сцене, а не своего.

**Было:**
```csharp
_player = FindAnyObjectByType<NetworkPlayer>();
```

**Стало:**
```csharp
var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
foreach (var p in players)
{
    if (p.IsOwner)
    {
        _player = p;
        break;
    }
}
```

**Результат:** Клиент теперь вызывает RPC от своего имени, но сервер всё равно отправляет результат НЕ тому клиенту.

---

### Сессия 8K — Надёжный поиск через ConnectedClients
**Идея:** `FindPlayerNetworkPlayer` использует `FindObjectsByType` который работает некорректно на хосте.

**Было:**
```csharp
private NetworkPlayer FindPlayerNetworkPlayer(ulong clientId)
{
    var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
    foreach (var player in players)
    {
        if (player.OwnerClientId == clientId)
            return player;
    }
    return null;
}
```

**Стало:**
```csharp
private NetworkPlayer FindPlayerNetworkPlayer(ulong clientId)
{
    var nm = NetworkManager.Singleton;
    if (nm != null && nm.ConnectedClients.TryGetValue(clientId, out var client))
    {
        var np = client.PlayerObject?.GetComponent<NetworkPlayer>();
        if (np != null)
            return np;
    }
    
    // Fallback
    var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
    foreach (var player in players)
    {
        if (player.OwnerClientId == clientId)
            return player;
    }
    return null;
}
```

**Результат:** НЕ РЕШЕНО — проблема остаётся.

---

## Логи проблемы

```
[TMS] BuyItemServerRpc: clientId=1, IsServer=True, IsHost=True
[TMS] BUY | Client:1 | mesium_canister_v01 x1 | SUCCESS | За 10 CR
[TMS] SendTradeResult: clientId=1, success=True, newCredits=793, itemId=mesium_canister_v01, qty=1
[TMS] FindPlayerNetworkPlayer(1): найден через ConnectedClients, OwnerClientId=1
[TMS] TradeResultClientRpc отправлен клиенту 1
[NetworkPlayer] TradeResultClientRpc: IsOwner=False, success=True
[NetworkPlayer] Этот клиент НЕ владелец, пропускаю
```

**Анализ:**
- Сервер НАХОДИТ правильный объект (OwnerClientId=1)
- RPC ОТПРАВЛЯЕТСЯ правильному клиенту
- НО на клиенте `IsOwner=False` — значит RPC пришёл на НЕПРАВИЛЬНЫЙ объект NetworkPlayer!

---

## Корневая причина (гипотеза)

Проблема в том, что `ClientRpc` отправляется на `NetworkBehaviour`, но на клиенте может быть **НЕСКОЛЬКО** объектов `NetworkPlayer` с разными `OwnerClientId`.

Когда сервер вызывает:
```csharp
player.TradeResultClientRpc(...) // player = NetworkPlayer с OwnerClientId=1
```

На клиенте (хосте) этот RPC обрабатывается объектом с `OwnerClientId=0` (так как это ЛОКАЛЬНЫЙ клиент на хосте).

**Почему:** На хосте `IsOwner` проверяется относительно ЛОКАЛЬНОГО клиента (0), а не того клиента которому предназначался RPC.

---

## Возможные решения (не проверены)

### Вариант 1: Использовать ClientRpcParams с явным таргетом
```csharp
var rpcParams = new ClientRpcParams
{
    Send = new ClientRpcSendParams
    {
        TargetClientIds = new ulong[] { clientId }
    }
};
player.TradeResultClientRpc(..., rpcParams);
```

**Проблема:** Ранее это не работало на хосте.

---

### Вариант 2: Не использовать ClientRpc на хосте вообще
Если клиент=хост, вызывать `OnTradeResult` напрямую:
```csharp
if (clientId == NetworkManager.Singleton.LocalClientId)
{
    TradeUI.Instance?.OnTradeResult(...);
}
else
{
    player.TradeResultClientRpc(...);
}
```

---

### Вариант 3: Отправлять RPC всем и фильтровать по clientId в параметрах
```csharp
[ClientRpc]
public void TradeResultClientRpc(ulong targetClientId, ...)
{
    if (NetworkManager.Singleton.LocalClientId != targetClientId)
        return;
    
    TradeUI.Instance.OnTradeResult(...);
}
```

---

## Файлы involved

- `Assets/_Project/Trade/Scripts/TradeMarketServer.cs`
- `Assets/_Project/Trade/Scripts/TradeUI.cs`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

## Сессия 8L (15.04.2026) — РЕШЕНИЕ
**Идея:** Не полагаться на `IsOwner` на клиенте, а передавать `targetClientId` в параметрах RPC.

**NetworkPlayer.cs:**
```csharp
[ClientRpc]
public void TradeResultClientRpc(ulong targetClientId, bool success, string message, 
    float newCredits, string itemId = "", int itemQuantity = 0, bool isPurchase = true)
{
    ulong localClientId = NetworkManager.Singleton.LocalClientId;
    Debug.Log($"[NetworkPlayer] TradeResultClientRpc: targetClientId={targetClientId}, localClientId={localClientId}");
    
    // Проверяем targetClientId, а не IsOwner
    if (localClientId != targetClientId)
    {
        Debug.Log($"[NetworkPlayer] Этот клиент ({localClientId}) НЕ целевой ({targetClientId}), пропускаю");
        return;
    }
    
    // Этот клиент — целевой, показываем результат
    TradeUI.Instance?.OnTradeResult(success, message, newCredits, itemId, itemQuantity, isPurchase);
}
```

**TradeMarketServer.cs:**
```csharp
var player = FindPlayerNetworkPlayer(clientId);
if (player != null)
{
    // Передаём clientId в параметрах
    player.TradeResultClientRpc(clientId, success, message, newCredits, itemId, itemQuantity, isPurchase);
}
```

**Почему это работает:** На хосте `IsOwner` проверяется относительно ЛОКАЛЬНОГО клиента (0), 
но теперь мы проверяем `localClientId != targetClientId` — это РАВЕНСТВО, которое работает 
корректно независимо от того, кто вызывал RPC.

## Статус: НЕ РЕШЕНО (Сессия 8L также не помогла)

### Итог
Все попытки (8I-8L) не решили проблему. Корневая причина — архитектурная:
- `TradeMarketServer` находится на **отдельном** объекте (Singleton), а не на NetworkPlayer
- `ClientRpc` от TradeMarketServer приходит на ВСЕ NetworkPlayer, а не только на нужного клиента
- Фильтрация по `targetClientId` или `IsOwner` не работает т.к. на хосте эти проверки ненадёжны

### Возможные решения для следующей сессии:
1. **Перенести** `SendTradeResultToClient` логику в `NetworkPlayer` компонент игрока
2. **Использовать** `ClientRpcParams` с `TargetClientIds` правильно (нужно разобраться почему не работает)
3. **Вызывать** `OnTradeResult` напрямую без RPC если `clientId == LocalClientId`
