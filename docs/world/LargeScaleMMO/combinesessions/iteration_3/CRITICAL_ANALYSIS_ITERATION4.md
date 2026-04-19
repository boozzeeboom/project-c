# CRITICAL ANALYSIS: Iteration 4 — Root Cause & Solution

**Дата:** 19.04.2026, 01:13 MSK  
**Статус:** 🔴 КРИТИЧЕСКИЕ БАГИ ОБНАРУЖЕНЫ  
**Фокус:** 
1. Мир сдвигает к игроку при 100,000 — игрок не может отдалиться
2. Чанки грузятся вокруг стартовой локации вместо вокруг игрока

---

## 🔴 ПРОБЛЕМА 1: Мир сдвигает к игроку

### Корневая причина

**В ServerSynced режиме (multiplayer client):**

1. **Сервер** получает `RequestWorldShiftRpc(cameraPos)` от клиента
2. **Сервер** проверяет `ShouldUseFloatingOrigin(cameraPos)` 
3. **Сервер** вызывает `TeleportOwnerPlayerToOrigin()` — игрок телепортируется на (0, 5, 0)
4. **Сервер** отправляет `BroadcastWorldShiftRpc(offset)` всем клиентам
5. **Клиент** получает RPC и вызывает `ApplyWorldShift(offset)`

**НО! На клиенте ApplyWorldShift() НЕ телепортирует игрока!**

```csharp
// FloatingOriginMP.cs — ApplyWorldShift() (строки 1119-1154)
public void ApplyWorldShift(Vector3 offset)
{
    // ...
    ApplyShiftToAllRoots(offset);  // ← Сдвигает мир
    _totalOffset += offset;
    _shiftCount++;
    _lastShiftTime = Time.time;
    OnWorldShifted?.Invoke(offset);  // ← Уведомляет подписчиков
    
    // ❌ НЕТ ТЕЛЕПОРТАЦИИ ИГРОКА НА КЛИЕНТЕ!
}
```

**Результат:**
- На сервере: игрок на (0, 5, 0) — правильно
- На клиенте: игрок на (150000, 0, 150000) — НЕПРАВИЛЬНО!
- `GetWorldPosition()` на клиенте возвращает 150000
- Следующий кадр: `ShouldUseFloatingOrigin(150000)` → TRUE → новый сдвиг
- **БЕСКОНЕЧНЫЙ ЦИКЛ СДВИГОВ**

---

### Последовательность событий при сдвиге

```
СЕРВЕР:
┌─────────────────────────────────────────────────────────────┐
│ RequestWorldShiftRpc(cameraPos=150000)                      │
│     ↓                                                      │
│ ShouldUseFloatingOrigin(150000) → TRUE                     │
│     ↓                                                      │
│ TeleportOwnerPlayerToOrigin() → player=(0,5,0)             │
│     ↓                                                      │
│ ApplyShiftToAllRoots(offset) → мир сдвинут                  │
│     ↓                                                      │
│ BroadcastWorldShiftRpc(offset) → отправлен                   │
└─────────────────────────────────────────────────────────────┘

КЛИЕНТ:
┌─────────────────────────────────────────────────────────────┐
│ ApplyWorldShift(offset)                                      │
│     ↓                                                       │
│ ApplyShiftToAllRoots(offset) → мир сдвинут                   │
│     ↓                                                       │
│ ❌ ИГРОК ОСТАЁТСЯ НА 150000!                               │
│     ↓                                                       │
│ OnWorldShifted → NetworkPlayer обновляет кэш: 150000        │
│     ↓                                                       │
│ Следующий кадр: GetWorldPosition() = 150000                 │
│     ↓                                                       │
│ ShouldUseFloatingOrigin(150000) → TRUE                      │
│     ↓                                                       │
│ RequestWorldShiftRpc(150000) → новый сдвиг!                 │
│     ↓                                                       │
│ БЕСКОНЕЧНЫЙ ЦИКЛ                                            │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔴 ПРОБЛЕМА 2: Чанки грузятся вокруг стартовой локации

### Корневая причина

**Вторая проблема вытекает из первой:**

1. После сдвига мира на клиенте игрок остаётся на 150000
2. `_cachedPlayerPosition = 150000` (неправильный кэш)
3. `GetWorldPosition()` возвращает `_cachedPlayerPosition = 150000`
4. `WorldStreamingManager.UpdateStreaming()` вызывает `LoadChunksAroundPlayer(150000)`
5. `GetChunkAtPosition(150000)` вычисляет чанк для позиции 150000

**НО!**
- После сдвига мира WorldRoot сдвинут, TradeZones на (0,0,0)
- Мир "вернулся" к origin, но игрок "остался" на 150000
- Чанки генерируются вокруг origin, а игрок на 150000 видит пустоту

**Почему загружаются (-3, -2):**
```
1. GetWorldPosition() возвращает _cachedPlayerPosition = 150000
2. Но WorldRoot уже сдвинут!
3. GetChunkAtPosition(150000) вычисляет для "сдвинутого" мира
4. Результат: чанк (-3, -2) вместо (50, 50)
```

**Или альтернативная причина:**
```
1. GetWorldPosition() возвращает позицию ThirdPersonCamera
2. ThirdPersonCamera спавнится рядом с TradeZones (origin)
3. Camera.position ≈ (0, 0, 0)
4. GetChunkAtPosition(0,0,0) = Chunk(-3, -2)
```

---

## ✅ РЕШЕНИЕ

### Fix 1: ApplyWorldShift на клиенте должен телепортировать игрока

```csharp
// FloatingOriginMP.cs — ApplyWorldShift()
public void ApplyWorldShift(Vector3 offset)
{
    // ... существующий код ...
    
    ApplyShiftToAllRoots(offset);
    _totalOffset += offset;
    _shiftCount++;
    _lastShiftTime = Time.time;
    
    // ✅ ITERATION 4 FIX: Телепортируем игрока на клиенте!
    if (mode == OriginMode.ServerSynced)
    {
        TeleportOwnerPlayerToOrigin();
    }
    
    OnWorldShifted?.Invoke(offset);
}
```

### Fix 2: Добавить проверку в ShouldUseFloatingOrigin для ServerSynced

```csharp
// FloatingOriginMP.cs — ShouldUseFloatingOrigin()
public bool ShouldUseFloatingOrigin(Vector3 playerPosition)
{
    // В ServerSynced режиме проверяем через _totalOffset
    // Если _totalOffset > 0, мир уже сдвинут, значит игрок рядом с TradeZones
    if (mode == OriginMode.ServerSynced)
    {
        // Если мир уже сдвинут (_totalOffset ≠ 0), игрок рядом с TradeZones
        if (_totalOffset.magnitude > 100f)
        {
            // Игрок рядом с TradeZones — НЕ нужен ещё один сдвиг
            return false;
        }
    }
    
    // Оригинальная проверка
    if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
    {
        float cachedDistance = _cachedPlayerPosition.magnitude;
        return cachedDistance > threshold;
    }
    
    return playerPosition.magnitude > threshold;
}
```

### Fix 3: В ServerSynced режиме использовать _totalOffset для расчёта позиции

```csharp
// FloatingOriginMP.cs — GetWorldPosition()
public Vector3 GetWorldPosition()
{
    // В ServerSynced режиме после сдвига:
    // positionSource.position = игрок в "сдвинутом" мире
    // _totalOffset = накопленный сдвиг
    // Результат = реальная позиция в мире
    
    if (mode == OriginMode.ServerSynced)
    {
        if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
        {
            // Кэшированная позиция — возвращаем
            return _cachedPlayerPosition;
        }
        
        // Fallback: positionSource - _totalOffset
        if (positionSource != null)
        {
            return positionSource.position - _totalOffset;
        }
    }
    
    // ... остальной код ...
}
```

---

## 📋 ПЛАН ИСПРАВЛЕНИЙ

| Step | File | Fix | Priority |
|------|------|-----|----------|
| 1 | FloatingOriginMP.cs | TeleportOwnerPlayerToOrigin() в ApplyWorldShift() | CRITICAL |
| 2 | FloatingOriginMP.cs | ShouldUseFloatingOrigin() проверка _totalOffset | HIGH |
| 3 | FloatingOriginMP.cs | GetWorldPosition() исправить ServerSynced | HIGH |
| 4 | Test | Проверить что сдвиг не повторяется | CRITICAL |

---

## 🔍 ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА

### Почему ServerSynced LateUpdate пропускается?

```csharp
// FloatingOriginMP.cs — LateUpdate() (строка 714)
if (mode == OriginMode.ServerSynced) return;  // ← Пропускаем ВЕСЬ LateUpdate!
```

**Это правильно!** В ServerSynced режиме:
- Сервер управляет сдвигом через RequestWorldShiftRpc
- Клиент только принимает сдвиг через BroadcastWorldShiftRpc
- НЕ должно быть локального сдвига на клиенте

**НО проблема:** Принятый сдвиг не телепортирует игрока!

---

## 📁 ФАЙЛЫ ДЛЯ ИЗМЕНЕНИЯ

1. **FloatingOriginMP.cs**
   - `ApplyWorldShift()` — добавить телепортацию для ServerSynced
   - `ShouldUseFloatingOrigin()` — добавить проверку _totalOffset
   - `GetWorldPosition()` — исправить расчёт для ServerSynced

---

**Автор:** Claude Code (Subagent Analysis)  
**Дата:** 19.04.2026, 01:13 MSK  
**Следующий шаг:** Реализовать исправления и протестировать
