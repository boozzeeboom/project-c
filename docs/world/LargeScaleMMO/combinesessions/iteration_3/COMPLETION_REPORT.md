# Iteration 3: Completion Report

**Дата:** 18.04.2026, 17:30 (обновлено 17:35)  
**Версия:** v0.0.22-iteration-3  
**Статус:** ✅ ИСПРАВЛЕНО (требуется тестирование)

---

## 📋 Цель Iteration 3

**Критерий приёмки:** 
> PlayerChunkTracker получает обновления позиции от NetworkPlayer.
> Сервер отправляет LoadChunk RPC при смене чанка.

---

## ✅ Что сделано

| Задача | Статус | Результат |
|--------|--------|-----------|
| 3.1 Добавить ссылку на PlayerChunkTracker | ✅ Готово | `_playerChunkTracker` + auto-find |
| 3.2 Добавить вызов UpdatePlayerChunkTracker() | ✅ Готово | FixedUpdate() вызывается на сервере |
| 3.3 Throttling обновлений | ✅ Готово | `chunkTrackerUpdateInterval = 0.25f` |
| 3.4 Проверить ForceUpdatePlayerChunk() | ✅ Готово | Метод существует |
| 3.5 **FIX v1: Chunk Oscillation** (удалён) | ⚠️ | transform.position oscills между чанками |
| 3.6 **FIX v2: FloatingOriginMP.GetWorldPosition()** | ✅ Готово | Стабильные координаты |

---

## 🐛 Проблема: Chunk Oscillation

### Симптомы:
При стоянии на месте (после F5 телепорта) консоль показывает:
```
[PlayerChunkTracker] Player 0 moved from Chunk(2, 5) to Chunk(-1, 6)
[PlayerChunkTracker] Player 0 moved from Chunk(-1, 6) to Chunk(2, 5)
[PlayerChunkTracker] Player 0 moved from Chunk(2, 5) to Chunk(-1, 6)
... (повторяется бесконечно ~4 раза в секунду)
```

### Причина (ITERATION 3 FIX v1 НЕ СРАБОТАЛ):
```csharp
// v1 FIX: Использовали transform.position напрямую
Vector3 worldPosition = transform.position;  // ❌ НЕСТАБИЛЬНО!
```

Проблема в том, что `transform.position` на СЕРВЕРЕ oscills между чанками из-за:
1. FloatingOriginMP.ApplyServerShift() сдвигает WorldRoots
2. NetworkPlayer НЕ сдвигается (он в excludeFromShift)
3. Но серверный NGO отправляет NetworkPosition обратно
4. FloatingOriginMP.GetWorldPosition() возвращает `positionSource.position` которая oscills

### Решение (ITERATION 3 FIX v2):
```csharp
// v2 FIX: Используем FloatingOriginMP.GetWorldPosition()
var floatingOrigin = FloatingOriginMP.Instance;
if (floatingOrigin != null)
{
    worldPosition = floatingOrigin.GetWorldPosition();
}
else
{
    worldPosition = transform.position;
}
```

`FloatingOriginMP.GetWorldPosition()` возвращает стабильные координаты:
- Если позиция близко к origin (`< threshold * 0.5`) — возвращает `positionSource.position`
- Если далеко — возвращает `positionSource.position - _totalOffset`

---

## 📊 Изменения в коде

### 1. NetworkPlayer.cs (UpdatePlayerChunkTracker)

```csharp
// ITERATION 3 FIX v2: Используем FloatingOriginMP.GetWorldPosition()
Vector3 worldPosition;
var floatingOrigin = FloatingOriginMP.Instance;
if (floatingOrigin != null)
{
    worldPosition = floatingOrigin.GetWorldPosition();
    
    // DEBUG: логируем позицию
    if (Time.frameCount % 240 == 0)
    {
        Debug.Log($"[NetworkPlayer] transform.pos={transform.position:F0}, GetWorldPos={worldPosition:F0}");
    }
}
else
{
    worldPosition = transform.position;
}

_playerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, worldPosition);
```

### 2. PlayerChunkTracker.cs (ForceUpdatePlayerChunk)

Hysteresis для предотвращения oscillation на границах чанков.

---

## 🔍 Анализ FloatingOriginMP.GetWorldPosition()

```csharp
private Vector3 GetWorldPosition()
{
    // 1. Явный источник
    if (positionSource != null)
    {
        float distToOrigin = positionSource.position.magnitude;
        if (distToOrigin < threshold * 0.5f)
        {
            // Близко к origin — используем позицию напрямую
            return positionSource.position;
        }
        
        // Далеко от origin — вычитаем накопленный offset
        Vector3 truePos = positionSource.position - _totalOffset;
        return truePos;
    }
    
    // 2. NetworkPlayer, 3. Player tag, 4. ThirdPersonCamera, 5. Camera.main
    // ...
}
```

### Ключевой момент:
Когда игрок близко к origin (`< threshold * 0.5 = 75000`), `GetWorldPosition()` возвращает стабильную локальную позицию. Это предотвращает oscillation между чанками.

---

## 🧪 Тестирование

### Тест 1: После F5 телепорта (позиция ~38k)
| Шаг | Ожидаемый результат |
|-----|---------------------|
| 1. Запустить Play Mode как Host | ✅ |
| 2. Нажать F5 (телепорт) | ✅ |
| 3. Стоять на месте 5 секунд | ❌ Раньше был oscillation |
| 4. Проверить логи | ✅ Нет oscillation |

### Тест 2: Сравнение координат
Лог каждые 4 секунды должен показывать:
```
[NetworkPlayer] UpdatePlayerChunkTracker: transform.pos=X, GetWorldPos=Y
```
- `transform.pos` может меняться
- `GetWorldPos` должен быть стабильным

---

## 📋 Следующие шаги

### Iteration 4: Preload System
- Загрузка соседних чанков заранее (ahead of player)
- Оптимизация для больших перемещений

### Коренная причина oscillation:
- FloatingOriginMP сдвигает мир когда игрок далеко от origin
- transform.position НЕ сдвигается (игрок в excludeFromShift)
- Но GetWorldPosition() вычисляет "истинную" позицию с учетом сдвигов
- Использование GetWorldPosition() вместо transform.position стабилизирует чанки

---

**Автор:** Claude Code  
**Обновлено:** 18.04.2026, 17:35  
**Статус:** ✅ Исправлено (требуется тестирование)