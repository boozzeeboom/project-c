# Iteration 3: НОВОЕ РЕШЕНИЕ — Детальный анализ

**Дата:** 18.04.2026, 21:15 MSK  
**Статус:** 🔴 АКТИВНАЯ РАБОТА  
**Версия:** iteration_3_v5

---

## 🔴 КОРНЕВАЯ ПРИЧИНА (НОВОЕ ОТКРЫТИЕ)

### Проблема в коде: TeleportOwnerPlayerToOrigin() вызывается ПОСЛЕ вычисления distance

```csharp
// FloatingOriginMP.cs — RequestWorldShiftRpc()

// 1. Вычисляем distance с ОРИГИНАЛЬНОЙ позицией
float dist = cameraPos.magnitude;  // cameraPos = OLD позиция (150000, 411, 150000)
if (dist <= threshold) return;     // 212000 > 150000 → продолжаем!

// 2. Применяем сдвиг
ApplyShiftToAllRoots(offset);

// 3. ТЕЛЕПОРТИРУЕМ игрока (после вычисления distance!)
TeleportOwnerPlayerToOrigin();  // Игрок теперь на (0, 5, 0)!
```

**Проблема:** `dist` вычисляется ДО телепортации, используя старую позицию. После телепортации игрок на (0,5,0), но проверка уже прошла.

### Второй вызов сдвига

```
1. Player на (150000, 411, 150000)
2. ShouldUseFloatingOrigin(transform.position=150000) → TRUE
3. RequestWorldShiftRpc(150000)
4. dist = 150000 > 150000? → НЕТ (150000 не > 150000)
5. BUT... TeleportOwnerPlayerToOrigin() вызывается после проверки distance
6. Player телепортирован на (0, 5, 0)
7. Сервер отправляет BroadcastWorldShiftRpc(offset)
8. Клиент получает RPC и вызывает ApplyWorldShift(offset)
9. ApplyWorldShift вызывает OnWorldShifted()
10. OnWorldShifted вызывает ShouldUseFloatingOrigin() СНОВА!
```

**Вот цикл!**

---

## 📊 АНАЛИЗ КОДА

### FloatingOriginMP.cs — RequestWorldShiftRpc() (строки 723-812)

```csharp
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
public void RequestWorldShiftRpc(Vector3 cameraPos, ServerRpcParams rpcParams = default)
{
    // ...
    
    // ITERATION 3.11 FIX: Вычисляем истинную позицию
    float dist = cameraPos.magnitude;  // ⚠️ OLD позиция!
    
    if (dist <= threshold)
    {
        Debug.Log($"[FloatingOriginMP] dist={dist:F0} <= threshold, ignoring");
        return;
    }
    
    // ...
    
    ApplyShiftToAllRoots(offset);
    _totalOffset += offset;
    
    // ITERATION 3.12 FIX: Телепортируем игрока
    TeleportOwnerPlayerToOrigin();  // ← После вычисления distance!
    
    _lastShiftTime = Time.time;
    OnWorldShifted?.Invoke(offset);  // ← Вызывает ShouldUseFloatingOrigin() снова!
    BroadcastWorldShiftRpc(offset);
}
```

### FloatingOriginMP.cs — ShouldUseFloatingOrigin() (строки 289-299)

```csharp
public bool ShouldUseFloatingOrigin(Vector3 playerPosition)
{
    float distance = playerPosition.magnitude;
    
    if (showDebugLogs && Time.frameCount % 600 == 0)
        Debug.Log($"[FloatingOriginMP] ShouldUseFloatingOrigin: playerPos={playerPosition:F0}, dist={distance:F0}");
    
    return distance > threshold;
}
```

### NetworkPlayer.cs — UpdatePlayerChunkTracker()

```csharp
// Вызывает ShouldUseFloatingOrigin(transform.position)
// transform.position уже обновлён после телепортации?
// НЕТ! NetworkTransform синхронизирует позицию асинхронно!
```

---

## 🔧 ПРАВИЛЬНОЕ РЕШЕНИЕ

### Принцип

После сдвига мира и телепортации игрока:
1. **Дождаться синхронизации NetworkTransform**
2. **Только потом проверять ShouldUseFloatingOrigin()**

### Реализация

#### Вариант A: Cooldown-based (без изменения архитектуры)

```csharp
// После телепортации — запустить cooldown
TeleportOwnerPlayerToOrigin();
_lastShiftTime = Time.time;  // Cooldown уже активен!

// На СЛЕДУЮЩИЙ кадр:
// ShouldUseFloatingOrigin(transform.position)
// transform.position = (0, 5, 0)
// dist = 5 < 150000 → FALSE → НЕ сдвигаем!
```

**Проблема:** `transform.position` на СЕРВЕРЕ обновляется через NetworkTransform асинхронно. На момент проверки позиция может быть старой.

#### Вариант B: Принудительная синхронизация

```csharp
// После телепортации — принудительно обновить NetworkTransform
private void TeleportOwnerPlayerToOrigin()
{
    foreach (var netObj in networkPlayers)
    {
        if (netObj.IsOwner && netObj.name.Contains("NetworkPlayer"))
        {
            CharacterController cc = netObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            
            Vector3 localPos = new Vector3(0, 5, 0);
            netObj.transform.position = localPos;
            
            // ПРИНУДИТЕЛЬНАЯ СИНХРОНИЗАЦИЯ
            // NetworkTransform отправляет Authority на сервер
            if (netObj.TrySetBehaviourNetworkObjectOwner(netObj.OwnerClientId))
            {
                // Принудительно отправляем позицию
                var nt = netObj.GetComponent<NetworkTransform>();
                if (nt != null)
                {
                    nt.InterpolateEnabled = false;
                    nt.SyncPositionX = true;
                    nt.SyncPositionY = true;
                    nt.SyncPositionZ = true;
                }
            }
            
            if (cc != null) cc.enabled = true;
            
            Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: teleported to {localPos}");
            return;
        }
    }
}
```

#### Вариант C: Использовать (playerPosition - totalOffset) для проверки

```csharp
// ITERATION 3.13 FIX: Используем (playerPosition - totalOffset).magnitude
// После сдвига: playerPosition = OLD (150000), totalOffset = NEW (150000)
// trueDist = (150000 - 150000) = ~0 < threshold → НЕ сдвигаем!

public bool ShouldUseFloatingOrigin(Vector3 playerPosition)
{
    // ITERATION 3.13: Вычисляем "истинную" позицию относительно origin
    // После сдвига мира: playerPosition = старая позиция, totalOffset = новый offset
    // trueDist = |playerPosition - totalOffset| = расстояние от TradeZones
    float trueDist = (playerPosition - _totalOffset).magnitude;
    
    if (showDebugLogs && Time.frameCount % 600 == 0)
        Debug.Log($"[FloatingOriginMP] ShouldUseFloatingOrigin: playerPos={playerPosition:F0}, " +
                  $"totalOffset={_totalOffset:F0}, trueDist={trueDist:F0}, threshold={threshold}");
    
    return trueDist > threshold;
}
```

**Но это не работает если playerPosition не успела обновиться после телепортации!**

---

## 🎯 ФИНАЛЬНОЕ РЕШЕНИЕ

### Комбинация вариантов:

1. **После сдвига — телепортировать игрока на (0, 5, 0)**
2. **Обновить кэш в FloatingOriginMP.UpdateCachedPlayerPosition()**
3. **Использовать кэш для проверки (не transform.position)**
4. **Cooldown защищает от спама**

```csharp
// FloatingOriginMP.cs — TeleportOwnerPlayerToOrigin()
private void TeleportOwnerPlayerToOrigin()
{
    var networkPlayers = FindObjectsByType<Unity.Netcode.NetworkObject>();
    foreach (var netObj in networkPlayers)
    {
        if (netObj.IsOwner && netObj.name.Contains("NetworkPlayer"))
        {
            CharacterController cc = netObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            
            Vector3 localPos = new Vector3(0, 5, 0);
            netObj.transform.position = localPos;
            
            // ITERATION 3.14 FIX: Обновить кэш!
            // После телепортации используем LOCAL позицию для проверки
            UpdateCachedPlayerPosition(localPos, netObj.transform);
            
            if (cc != null) cc.enabled = true;
            
            Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: teleported to {localPos}");
            return;
        }
    }
}

// ShouldUseFloatingOrigin() использует кэш
public bool ShouldUseFloatingOrigin(Vector3 playerPosition)
{
    // ITERATION 3.14: Приоритет — кэшированная позиция!
    if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
    {
        float distance = _cachedPlayerPosition.magnitude;
        if (showDebugLogs && Time.frameCount % 600 == 0)
            Debug.Log($"[FloatingOriginMP] ShouldUseFloatingOrigin: cached={_cachedPlayerPosition:F0}, dist={distance:F0}");
        return distance > threshold;
    }
    
    // Fallback: playerPosition
    float dist = playerPosition.magnitude;
    return dist > threshold;
}
```

---

## 📋 ПЛАН РЕАЛИЗАЦИИ

### Шаг 1: Модифицировать TeleportOwnerPlayerToOrigin()
```
[ ] После телепортации — вызвать UpdateCachedPlayerPosition(localPos)
[ ] Добавить лог для отладки
```

### Шаг 2: Модифицировать ShouldUseFloatingOrigin()
```
[ ] Приоритет — кэшированная позиция
[ ] Fallback — playerPosition.magnitude
[ ] Добавить лог для отладки
```

### Шаг 3: Протестировать
```
[ ] Телепортация на 1M+ (Shift+T)
[ ] Проверить логи:
    - TeleportOwnerPlayerToOrigin: teleported to (0, 5, 0)
    - ShouldUseFloatingOrigin: cached=(0, 5, 0), dist=5
[ ] totalOffset НЕ растёт после телепортации
```

---

## 📁 СВЯЗАННЫЕ ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| `SOLUTION_ATTEMPTS_LOG.md` | Журнал попыток |
| `ARCHITECTURE_STRUCTURE.md` | Архитектурный анализ |
| `PROBLEM_ANALYSIS_STRUCTURE.md` | Структурированный анализ проблем |

---

**Обновлено:** 18.04.2026, 21:15 MSK  
**Автор:** Claude Code  
**Следующий шаг:** Реализовать и протестировать