# CHANGELOG: ClientRpc Fix (Сессия 8I) — 2026-04-15

## Проблема
ClientRpc с `ClientRpcParams` не срабатывал на хосте — RPC уходил в сеть, но локальный клиент не получал обновление TradeUI.

## Решение (Сессия 8I)

### NetworkPlayer.cs
Упрощён `TradeResultClientRpc` — убран `ClientRpcParams`. Проверка `IsOwner` перенесена в сам метод:

```csharp
[ClientRpc]
public void TradeResultClientRpc(bool success, string message, float newCredits, 
    string itemId = "", int itemQuantity = 0, bool isPurchase = true)
{
    // На хосте IsOwner всегда true для своего объекта
    if (IsOwner && TradeUI.Instance != null)
    {
        TradeUI.Instance.OnTradeResult(success, message, newCredits, itemId, itemQuantity, isPurchase);
    }
}
```

### TradeMarketServer.cs
Простой вызов без `ClientRpcParams`:

```csharp
player.TradeResultClientRpc(success, message, newCredits, itemId, itemQuantity, isPurchase);
```

### TradeUI.cs
Автоматическое переключение на вкладку СКЛАД после покупки:

```csharp
if (isPurchase && !string.IsNullOrEmpty(itemId) && itemQuantity > 0)
{
    Debug.Log($"[TradeUI] OnTradeResult: покупка успешна! Переключаюсь на склад...");
    _showWarehouseTab = true;
    RenderItems();
    UpdateDisplays();
    ShowMessage($"КУПЛЕНО! Нажмите T для просмотра склада");
}
```

## Результат тестирования (Host)
```
[TradeMarketServer] BUY | Client:0 | antigrav_ingot_v01 x1 | SUCCESS | За 45 CR
[NetworkPlayer] TradeResultClientRpc: IsOwner=True, success=True
[NetworkPlayer] Вызываю OnTradeResult на владельце
[TradeUI] OnTradeResult: success=True, newCredits=384,2501
[TradeUI] SyncWarehouseItem: itemDef=OK
[TradeUI] OnTradeResult: после Save(), склад.Count=2
[TradeUI] OnTradeResult: покупка успешна! Переключаюсь на склад...
```

## Файлы изменены
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
- `Assets/_Project/Trade/Scripts/TradeMarketServer.cs`
- `Assets/_Project/Trade/Scripts/TradeUI.cs`

## Сессия 8J (15.04.2026)
**Проблема:** `FindAnyObjectByType<NetworkPlayer>()` возвращал НЕ своего игрока (первый в сцене вместо своего).

**Решение (TradeUI.cs):**
```csharp
private NetworkPlayer Player
{
    get
    {
        if (_player == null || !_player.IsOwner)
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            foreach (var p in players)
            {
                if (p.IsOwner)
                {
                    _player = p;
                    break;
                }
            }
        }
        return _player;
    }
}
```

## Сессия 8K (15.04.2026)
**Проблема:** `TradeResultClientRpc` приходил с `IsOwner=False` на клиенте — `FindPlayerNetworkPlayer` возвращал НЕ объект этого клиента.

**Решение (TradeMarketServer.cs):**
```csharp
private NetworkPlayer FindPlayerNetworkPlayer(ulong clientId)
{
    // Используем NetworkManager.ConnectedClients для надёжного поиска
    var nm = NetworkManager.Singleton;
    if (nm != null && nm.ConnectedClients.TryGetValue(clientId, out var client))
    {
        var np = client.PlayerObject?.GetComponent<NetworkPlayer>();
        if (np != null)
            return np;
    }
    
    // Fallback: ищем вручную
    var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
    foreach (var player in players)
    {
        if (player.OwnerClientId == clientId)
            return player;
    }
    return null;
}
```
