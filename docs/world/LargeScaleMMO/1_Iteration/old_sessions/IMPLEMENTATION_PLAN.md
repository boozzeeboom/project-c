# FloatingOriginMP — Полная реализация по плану

## Согласно 01_Architecture_Plan.md

### План реализации

#### Шаг 1: ServerRpc — RequestWorldShiftRpc

```csharp
[ServerRpc(RequireOwnership = false)]
public void RequestWorldShiftRpc(Vector3 cameraPos, ServerRpcParams rpcParams = default)
{
    // Проверяем что запрос от авторитетного источника
    if (!IsServer) return;
    
    // Проверяем threshold
    if (cameraPos.magnitude > threshold)
    {
        // Вычисляем offset с округлением
        Vector3 offset = RoundShift(cameraPos);
        
        // Применяем сдвиг на сервере
        ApplyShiftToAllRoots(offset);
        
        // Рассылаем всем клиентам
        BroadcastWorldShiftRpc(offset);
    }
}
```

#### Шаг 2: ClientRpc — BroadcastWorldShiftRpc

```csharp
[ClientRpc]
private void BroadcastWorldShiftRpc(Vector3 offset, ClientRpcParams rpcParams = default)
{
    // Защита от loop
    if (IsServer) return;
    
    // Применяем сдвиг
    ApplyShiftToAllRoots(offset);
    
    // Оповещаем подписчиков
    OnWorldShifted?.Invoke(offset);
}
```

#### Шаг 3: Интеграция с LateUpdate

```csharp
private void LateUpdate()
{
    if (mode == OriginMode.ServerAuthority)
    {
        // Сервер инициирует сдвиг
        // ДО NetworkTransform снимает позицию!
        Vector3 cameraPos = GetWorldPosition();
        
        if (cameraPos.magnitude > threshold)
        {
            RequestWorldShiftRpc(cameraPos);
        }
        
        return; // Не применяем сдвиг локально
    }
    
    if (mode == OriginMode.ServerSynced)
    {
        // Ждём сдвига от сервера через RPC
        return;
    }
    
    // Local режим — оставшийся код
}
```

#### Шаг 4: Синхронизация с NetworkTransform

```csharp
// В OnWorldShifted
public static event Action<Vector3> OnWorldShifted;

// NetworkPlayer подписывается
void OnWorldShifted(Vector3 offset)
{
    // Игрок должен остаться на месте относительно TradeZones
    // NetworkTransform получит новую позицию от сервера
}
```

## ✅ РЕАЛИЗОВАНО (18.04.2026)

- [x] 1. Добавить RequestWorldShiftRpc
- [x] 2. Добавить BroadcastWorldShiftRpc  
- [x] 3. Интегрировать с LateUpdate
- [ ] 4. Добавить синхронизацию с NetworkTransform
- [ ] 5. Протестировать

## Изменения в FloatingOriginMP.cs

### Новые методы:

```csharp
// ServerRpc: клиент → сервер запрос сдвига
[ServerRpc(RequireOwnership = false)]
public void RequestWorldShiftRpc(Vector3 cameraPos, ServerRpcParams rpcParams = default)

// ClientRpc: сервер → все клиенты сдвиг мира
[ClientRpc]
private void BroadcastWorldShiftRpc(Vector3 offset, ClientRpcParams rpcParams = default)

// Применение сдвига на сервере
private void ApplyServerShift(Vector3 cameraWorldPos)

// Применение сдвига локально
private void ApplyLocalShift(Vector3 cameraWorldPos)
```

### Изменения в LateUpdate:

```csharp
void LateUpdate() {
    if (mode == OriginMode.ServerAuthority) {
        if (IsServer) {
            ApplyServerShift(cameraWorldPos); // Сервер сам сдвигает и рассылает
        } else {
            RequestWorldShiftRpc(cameraWorldPos); // Клиент просит сервер
        }
    } else if (mode == OriginMode.Local) {
        ApplyLocalShift(cameraWorldPos); // Локальный сдвиг
    }
    // ServerSynced: ждём сдвига от сервера
}
```
