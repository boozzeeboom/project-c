# Iteration 3.8 — ROOT CAUSE ANALYSIS & FIX

**Дата:** 18.04.2026  
**Статус:** 🔴 КРИТИЧЕСКИЙ БАГ — ПРИЧИНА НАЙДЕНА

---

## 🔴 ROOT CAUSE: GetWorldPosition() в ServerAuthority режиме

### Лог показывает:
```
TradeZones NOW at: (0, 0, 0)          ← TradeZones восстановлен
_worldRoots NOW at: (-2400000, ...)    ← WorldRoot сдвинут
```

### Что происходит:

1. **TradeZones ВОССТАНАВЛИВАЕТСЯ на (0,0,0)** — строки 1110-1118 в ApplyShiftToAllRoots()
2. **TradeZones НЕ двигается** — он в excludeFromShift
3. **Но TradeZones ИМЕЕТ ДОЧЕРНИЙ ОБЪЕКТ с локальной позицией (150000, 500, 150000)!**

### Иерархия TradeZones:
```
TradeZones (position: 0,0,0)
└── FloatingOriginMP Camera (local position: 150000, 500, 150000) ← ПРОБЛЕМА!
    └── Camera component
```

### ПОДТВЕРЖДЕНИЕ из кода (строки 491-512):
```csharp
_camera = GetComponent<Camera>();  // Находит Camera НА FloatingOriginMP GameObject
_camera = Camera.main;              // Fallback если GetComponent == null
```

**FloatingOriginMP — дочерний TradeZones с локальным offset!**

### НО ЛОГ ПОКАЗЫВАЕТ:
```
TradeZones NOW at: (0, 0, 0)     ← TradeZones.GameObject на (0,0,0)
_camera (...) NOW at: (150000, ...) ← КАМЕРА на巨大ной позиции!
```

**Это значит: TradeZones.GameObject != FloatingOriginMP.parent!**

### ИСТИННАЯ ИЕРАРХИЯ:
```
Scene Root
├── TradeZones (position: 0,0,0)           ← Один TradeZones
├── FloatingOriginMP (position: 150000, ...) ← ОТДЕЛЬНЫЙ ОБЪЕКТ!
│   └── Camera
├── NetworkPlayer (position: ???)
└── TradeZone (position: 150000, ...)     ← ДРУГОЙ TradeZone!
```

**FLOATINGORIGINMP — НЕ дочерний TradeZones! Это ОТДЕЛЬНЫЙ root-level объект!**

---

## 🔄 Цикл работает так:

1. `RequestWorldShiftRpc(transform.position=150000)` → сервер сдвигает мир
2. `ApplyShiftToAllRoots(offset)` → TradeZones ВОССТАНАВЛИВАЕТСЯ на (0,0,0)
3. **FloatingOriginMP.transform.position = (150000, ...) — НЕ сдвигается!**
4. `GetWorldPosition()` в ServerAuthority ищет NetworkPlayer
5. **Находит дочерний объект или получает позицию через GetCachedPlayerPosition**
6. **Позиция всё ещё ~150000** — потому что игрок движется в мире
7. `ShouldUseFloatingOrigin(150000)` → TRUE
8. → Новый сдвиг → бесконечный цикл

---

## ✅ ФИНАЛЬНЫЙ ФИКС

### Проблема: GetWorldPosition() в ServerAuthority возвращает позицию, которая НЕ соответствует TradeZones

### Решение: Использовать ПОЗИЦИЮ TRADEZONES как референс для проверки

**НЕПРАВИЛЬНО:**
```csharp
// ShouldUseFloatingOrigin использует playerPosition.magnitude
return playerPosition.magnitude > threshold;  // threshold=150000
```

**ПРАВИЛЬНО:**
```csharp
// ShouldUseFloatingOrigin использует РАССТОЯНИЕ ОТ TRADEZONES
float distanceFromTradeZones = Vector3.Distance(playerPosition, tradeZonesPosition);
return distanceFromTradeZones > threshold;
```

---

## 📋 КОНКРЕТНЫЕ ИЗМЕНЕНИЯ

### 1. FloatingOriginMP.cs — ShouldUseFloatingOrigin()

**БЫЛО (строки 224-230):**
```csharp
public bool ShouldUseFloatingOrigin(Vector3 playerPosition)
{
    return playerPosition.magnitude > threshold;
}
```

**СТАЛО:**
```csharp
public bool ShouldUseFloatingOrigin(Vector3 playerPosition)
{
    // ITERATION 3.9 FIX: Используем РАССТОЯНИЕ ОТ TRADEZONES вместо magnitude!
    // После сдвига мира TradeZones находится на (0,0,0), 
    // поэтому magnitude будет ~= позиции игрока.
    // НО если TradeZones был на большой позиции — magnitude будет неправильным!
    
    // Находим TradeZones для расчёта расстояния
    GameObject tradeZones = GameObject.Find("TradeZones");
    if (tradeZones != null)
    {
        float distance = Vector3.Distance(playerPosition, tradeZones.transform.position);
        return distance > threshold;
    }
    
    // Fallback: используем magnitude если TradeZones не найден
    return playerPosition.magnitude > threshold;
}
```

### 2. FloatingOriginMP.cs — RequestWorldShiftRpc()

**БЫЛО (строки 679-685):**
```csharp
float dist = cameraPos.magnitude;
if (dist <= threshold)
{
    Debug.Log($"[FloatingOriginMP] RequestWorldShiftRpc: dist={dist:F0} <= threshold={threshold:F0}, ignoring");
    return;
}
```

**СТАЛО:**
```csharp
// ITERATION 3.9 FIX: Используем РАССТОЯНИЕ ОТ TRADEZONES
GameObject tradeZones = GameObject.Find("TradeZones");
float dist;
if (tradeZones != null)
{
    dist = Vector3.Distance(cameraPos, tradeZones.transform.position);
}
else
{
    dist = cameraPos.magnitude;
}

if (dist <= threshold)
{
    Debug.Log($"[FloatingOriginMP] RequestWorldShiftRpc: dist={dist:F0} <= threshold={threshold:F0}, ignoring");
    return;
}
```

### 3. FloatingOriginMP.cs — GetWorldPosition() в ServerAuthority

**БЫЛО (строки 339-364):**
```csharp
if (mode == OriginMode.ServerAuthority)
{
    var serverAuthPlayers = FindObjectsByType<Unity.Netcode.NetworkObject>();
    foreach (var netObj in serverAuthPlayers)
    {
        if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner)
        {
            Vector3 pos = netObj.transform.position;
            if (pos.magnitude > 100) // Игрок рядом с origin
            {
                return pos;
            }
        }
    }
    // Fallback: используем тег Player
    GameObject authPlayerByTag = GameObject.FindGameObjectWithTag("Player");
    if (authPlayerByTag != null)
    {
        return authPlayerByTag.transform.position;
    }
}
```

**СТАЛО:**
```csharp
if (mode == OriginMode.ServerAuthority)
{
    // ITERATION 3.9 FIX: Приоритет — IsOwner + ближе всего к TradeZones
    var serverAuthPlayers = FindObjectsByType<Unity.Netcode.NetworkObject>();
    Transform bestPlayer = null;
    float bestDistance = float.MaxValue;
    
    foreach (var netObj in serverAuthPlayers)
    {
        if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner)
        {
            Vector3 pos = netObj.transform.position;
            
            // Находим TradeZones для расчёта расстояния
            GameObject tradeZones = GameObject.Find("TradeZones");
            float distance = float.MaxValue;
            if (tradeZones != null)
            {
                distance = Vector3.Distance(pos, tradeZones.transform.position);
            }
            else
            {
                distance = pos.magnitude;
            }
            
            // Выбираем игрока который ближе всего к TradeZones
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPlayer = netObj.transform;
            }
        }
    }
    
    if (bestPlayer != null)
    {
        return bestPlayer.position;
    }
    
    // Fallback: тег Player
    GameObject authPlayerByTag = GameObject.FindGameObjectWithTag("Player");
    if (authPlayerByTag != null)
    {
        return authPlayerByTag.transform.position;
    }
}
```

---

## 📋 TODO LIST

- [ ] 1. Добавить ShouldUseFloatingOrigin() с проверкой расстояния от TradeZones
- [ ] 2. Обновить RequestWorldShiftRpc() с проверкой расстояния от TradeZones
- [ ] 3. Обновить GetWorldPosition() в ServerAuthority — выбирать игрока ближе всего к TradeZones
- [ ] 4. Протестировать сдвиг мира после телепортации на 1M+

---

**Автор:** Claude Code + Subagents Analysis  
**Дата:** 18.04.2026, 19:28 MSK
