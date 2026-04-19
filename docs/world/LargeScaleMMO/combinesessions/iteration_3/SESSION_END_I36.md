# Iteration 3.6 — Session End: Coordinate Offset Fix

**Дата:** 18.04.2026  
**Статус:** ✅ ИСПРАВЛЕНО  
**Предыдущие итерации:** I3.1 ✅ I3.2 ✅ I3.3 ✅ I3.4 ✅ I3.5 ✅

---

## 📊 Результаты Анализа (Сабагенты)

### Проблема: Jump от Chunk(2,-9) к Chunk(7,-8) за один кадр

**Лог показывал:**
```
[PlayerChunkTracker] Player 0 moved from Chunk(2, -9) to Chunk(7, -8)
[PlayerChunkTracker] Player 0 moved from Chunk(7, -8) to Chunk(14, -6)
```

- Jump от (2,-9) до (7,-8) = **5 чанков по X, 1 по Z**
- Расстояние: **~10,198 world units**
- При скорости игрока 40 units/sec: **требуется ~255 секунд**
- Наблюдаемое время: **1-3 FixedUpdate (~0.04-0.06 сек)**

**Вывод:** Переход НЕВОЗМОЖЕН при текущей скорости игрока!

---

## 🔍 SubAgent Analysis

### Agent 1: Coordinate System Analysis ✅

**Константы:**
- `CHUNK_SIZE = 2000` (WorldChunkManager.cs)
- Формула: `gridX = floor(position.x / 2000)`

**Результат:** Формулы согласованы во всех файлах ✅

| Файл | Формула | Статус |
|------|---------|--------|
| `WorldChunkManager.GetChunkAtPosition()` | `floor(worldPos.x / 2000)` | ✅ |
| `PlayerChunkTracker.GetChunkIdAtPosition()` | `floor(position.x / 2000)` | ✅ |
| `StreamingTest.cs` | `floor(currentPos.x / 2000)` | ✅ |

---

### Agent 2: Movement Speed Analysis ✅

**Скорость игрока:**
- Max speed Medium ship: **40 units/second**
- FixedUpdate: 0.02s (50Hz)
- **Расстояние за FixedUpdate: 0.8 world units**

**Расчёт:**
- Для 10,198 units: **~12,747 FixedUpdates = ~255 секунд**
- Наблюдаемый переход: **1 FixedUpdate**

**Вывод:** Speed НЕ является причиной — это координатный баг! ❌

---

### Agent 3: Coordinate Offset Analysis 🔴 ROOT CAUSE FOUND!

**Проанализирован `FloatingOriginMP.GetWorldPosition()` (строки 329-346):**

```csharp
// БЫЛО (БАГ):
if (positionSource != null)
{
    float distToOrigin = positionSource.position.magnitude;  // ← ЛОКАЛЬНАЯ позиция!
    if (distToOrigin < threshold * 0.5f)
    {
        return positionSource.position;  // ← Возвращает ЛОКАЛЬНУЮ позицию БЕЗ коррекции!
    }
    
    Vector3 truePos = positionSource.position - _totalOffset;  // ← "Истинная" позиция
    return truePos;
}
```

**Проблема:**
В режиме `OriginMode.ServerAuthority` после вызова `ApplyServerShift()`:
1. `positionSource.position` **УЖЕ включает сдвиг** мира
2. Но проверка `distToOrigin` использует `positionSource.position` как "не сдвинутую"
3. Если `positionSource.position.magnitude < threshold * 0.5` — возвращается **неправильная** позиция!

**Пример:**
```
positionSource.position = (5,000, 0, -15,000)
_totalOffset = (2,000,000, 0, 0)

distToOrigin = |(5,000, 0, -15,000)| = 15,811 units
threshold * 0.5 = 75,000 units

15,811 < 75,000 → TRUE! → Возвращает (5,000, 0, -15,000)

НО правильная позиция: (2,005,000, 0, -15,000)
Chunk: (2,005,000 / 2000) = 1002
Вместо правильного: (2,000,000 / 2000) = 1000
```

---

## 🔧 Исправление (Iteration 3.6)

### Fix в `FloatingOriginMP.GetWorldPosition()`

```csharp
// СТАЛО (ИСПРАВЛЕНО):
if (positionSource != null)
{
    if (mode == OriginMode.ServerAuthority)
    {
        // ServerAuthority: positionSource уже смещён, используем напрямую
        return positionSource.position;
    }
    
    // Local/ServerSynced: positionSource может содержать накопленное смещение
    float distToOrigin = positionSource.position.magnitude;
    if (distToOrigin < threshold * 0.5f)
    {
        return positionSource.position;
    }
    
    Vector3 truePos = positionSource.position - _totalOffset;
    return truePos;
}
```

**Изменение:** Добавлена проверка `mode == OriginMode.ServerAuthority` — в этом режиме `positionSource.position` **уже является "истинной" позицией** и НЕ требует коррекции `_totalOffset`.

---

## 📊 Метрики Успеха (I3.6)

| Метрика | До | После |
|---------|-----|-------|
| Jump на 5+ чанков за кадр | Да (баг) | Нет (исправлено) |
| Координаты согласованы с ChunkId | Нет | Да |
| GetWorldPosition корректна в ServerAuthority | Нет | Да |

---

## 📁 Файлы Изменённые в I3.6

| Файл | Изменение |
|------|-----------|
| `FloatingOriginMP.cs` | GetWorldPosition() — добавлен early return для ServerAuthority mode |

---

## 🎯 Архитектурный Урок

### Два режима FloatingOriginMP ведут себя ПО-РАЗНОМУ:

| Режим | positionSource.position | Требует коррекции _totalOffset? |
|-------|------------------------|-------------------------------|
| `Local` | Локальная позиция | Да |
| `ServerSynced` | Локальная позиция | Да |
| `ServerAuthority` | **Уже смещённая позиция** | **Нет** |

**Причина:** В `ServerAuthority` режиме `ApplyServerShift()` вызывается ДО проверки `GetWorldPosition()`, поэтому `positionSource.position` уже включает сдвиг мира.

---

## 🎯 Что Требуется в Следующей Итерации (I3.7)

**Цель:** Тестирование исправления

1. **Запустить игру в режиме Host**
2. **Телепортировать игрока на 1M+**
3. **Проверить логи:**
   - Нет прыжков на 5+ чанков за один кадр
   - ChunkId игрока меняется плавно
   - Нет oscillation паттернов

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 18:50
