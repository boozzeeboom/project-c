# Iteration 3.1 — Session Start Prompt

## Цель Сессии

**Исправить Chunk Oscillation** — позиция игрока oscills между двумя объектами при стоянии на месте.

---

## Проблема

### Симптомы
```
[PlayerChunkTracker] Player 0 moved from Chunk(0, 0) to Chunk(1, -1)
[PlayerChunkTracker] Player 0 moved from Chunk(1, -1) to Chunk(2, -1)
[PlayerChunkTracker] Player 0 moved from Chunk(2, -1) to Chunk(8, -2)
[PlayerChunkTracker] Player 0 moved from Chunk(8, -2) to Chunk(13, -3)
```

Игрок oscills между чанками при стоянии на месте.

### Анализ Логов

```
[NetworkPlayer] UpdatePlayerChunkTracker: transform.pos=(30375, 503, -6050), GetWorldPos=(30375, 503, -6050)  ✓ MATCH
[NetworkPlayer] UpdatePlayerChunkTracker: transform.pos=(12925, 60, 28199), GetWorldPos=(30375, 503, -6050)   ✗ OSCILLATION!
```

- `transform.position` oscills между правильной позицией `(30375, 503, -6050)` и неправильной `(12925, 60, 28199)`
- `GetWorldPosition()` стабилен — всегда возвращает `(30375, 503, -6050)`
- Неправильная позиция `(12925, 60, 28199)` — это КАКОЙ-ТО ДРУГОЙ объект

### Известно

1. **FloatingOriginMP.GetWorldPosition()** — стабилен, использует правильную позицию
2. **FloatingOriginMP.LateUpdate()** — работает правильно, threshold < 100k
3. **NetworkPlayer.UpdatePlayerChunkTracker()** — получает `transform.position` которое oscills

---

## Гипотезы Корневой Причины

### Гипотеза 1: NetworkPlayer использует неправильный источник позиции

В `NetworkPlayer.UpdatePlayerChunkTracker()`:
```csharp
Vector3 worldPosition;
var floatingOrigin = FloatingOriginMP.Instance;
if (floatingOrigin != null)
{
    worldPosition = floatingOrigin.GetWorldPosition();  // ← ЭТО СТАБИЛЬНО
}
// ...
_playerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, worldPosition);
```

**ПРОБЛЕМА:** Код вызывает `GetWorldPosition()` но затем **НЕ ИСПОЛЬЗУЕТ ЕГО** в логах!

Лог показывает:
```
UpdatePlayerChunkTracker: transform.pos=..., GetWorldPos=...
```

Это означает что **логируется transform.position**, но возможно `worldPosition` используется правильно. Нужно проверить.

### Гипотеза 2: FloatingOriginMP.GetWorldPosition() выбирает неправильный NetworkObject

В `FloatingOriginMP.GetWorldPosition()`:
```csharp
// 2. NetworkPlayer — ПРИОРИТЕТ!
var networkPlayers = FindObjectsByType<Unity.Netcode.NetworkObject>();
foreach (var netObj in networkPlayers)
{
    // Ищем NetworkPlayer с IsOwner=true И позицией далеко от origin (>10000)
    if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner)
    {
        Vector3 pos = netObj.transform.position;
        if (pos.magnitude > 10000)
        {
            return pos;  // ← ВОЗВРАЩАЕТ transform.position!
        }
    }
}
```

**ПРОБЛЕМА:** 
- `IsOwner` проверяется в КОНТЕКСТЕ вызывающего
- На ХОСТЕ: `IsOwner=true` может быть у ДВУХ объектов (ghost + real)
- `FindObjectsByType` НЕ детерминирован по порядку
- Выбирается ПЕРВЫЙ найденный объект — не обязательно настоящий игрок

### Гипотеза 3: Два источника позиции в одной системе

```
FloatingOriginMP.GetWorldPosition()  -->  FloatingOriginMP.LateUpdate()  -->  сдвиг мира
FloatingOriginMP.GetWorldPosition()  -->  NetworkPlayer.UpdatePlayerChunkTracker()  -->  chunk loading

НУЖНО: ЕДИНЫЙ источник позиции для всех систем
```

---

## Файлы для Анализа

### Обязательно прочитать:

1. **`Assets/_Project/Scripts/Player/NetworkPlayer.cs`**
   - Строки 420-480: `UpdatePlayerChunkTracker()`
   - Проверить: используется ли `worldPosition` или `transform.position`

2. **`Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`**
   - Строки 270-360: `GetWorldPosition()`
   - Строки 450-500: `LateUpdate()`
   - Проверить: какой источник позиции используется

3. **`Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs`**
   - Строки 200-250: `UpdatePlayerChunk()`
   - Строки 370-400: `ForceUpdatePlayerChunk()`
   - Проверить: какую позицию получает

### Для понимания контекста:

4. **`docs/world/LargeScaleMMO/combinesessions/iteration_3/SESSION_END_v2.md`** — предыдущий анализ

---

## План Анализа (для сабагентов)

### Subagent 1: Network Programmer

**Задача:** Проанализировать NetworkObject выбор в FloatingOriginMP

```
Прочитай FloatingOriginMP.GetWorldPosition() и ответь:
1. Как выбирается NetworkObject? (FindObjectsByType + IsOwner)
2. Почему IsOwner может быть нестабильным на хосте?
3. Как сделать выбор более надёжным?
4. Предложи具体的 код изменения
```

### Subagent 2: Gameplay Programmer

**Задача:** Проанализировать NetworkPlayer.UpdatePlayerChunkTracker()

```
Прочитай NetworkPlayer.UpdatePlayerChunkTracker() и ответь:
1. Какую позицию использует метод? (worldPosition из GetWorldPosition или transform.position?)
2. Почему лог показывает transform.pos а не worldPosition?
3. Используется ли позиция из GetWorldPosition() для обновления PlayerChunkTracker?
4. Предложи具体的 код изменения
```

### Subagent 3: Unity Specialist

**Задача:** Проанализировать архитектурную проблему

```
Проанализируй архитектуру:
1. Почему есть ДВА источника позиции? (GetWorldPosition и transform.position)
2. Как согласовать эти источники?
3. Должен ли FloatingOriginMP управлять ВСЕЙ позицией или только сдвигом мира?
4. Предложи архитектурное решение
```

---

## Ожидаемый Результат

1. **Точная локализация ошибки** — в каком файле и в какой строке
2. **Объяснение почему** — почему это вызывает oscillation
3. **Конкретное исправление** — готовый код для замены
4. **План тестирования** — как убедиться что исправление работает

---

## Ключевой Вопрос

**Почему `transform.position` oscills, если `GetWorldPosition()` стабилен?**

Возможные ответы:
1. `GetWorldPosition()` вызывается на ОБЪЕКТЕ который уже oscills
2. `GetWorldPosition()` кэширует позицию и не обновляется
3. `NetworkPlayer.UpdatePlayerChunkTracker()` НЕ использует `worldPosition` из `GetWorldPosition()`
4. `transform.position` — это ДРУГОЙ transform, не тот который использует GetWorldPosition()

**Ответ на этот вопрос = решение проблемы.**

---

## Метрики Успеха

После исправления, при стоянии на месте 10 секунд:
- ✅ Ноль логов "Player X moved from Chunk"
- ✅ PlayerChunkTracker получает стабильную позицию
- ✅ Chunk loading/unloading вызывается только при РЕАЛЬНОМ движении
