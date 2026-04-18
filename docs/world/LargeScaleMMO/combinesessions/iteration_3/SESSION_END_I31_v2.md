# Iteration 3.1 — Session End Report v2

**Дата:** 18.04.2026, 17:36  
**Статус:** ❌ Исправление НЕ сработало — oscillation продолжается  
**Причина:** Старая корутина всё ещё работает параллельно

---

## Анализ тестовых логов

### Наблюдение 1: Oscillation продолжается

```
[PlayerChunkTracker] Player 0 moved from Chunk(7, 5) to Chunk(8, 0)
[PlayerChunkTracker] Player 0 moved from Chunk(8, 0) to Chunk(7, 5)
[PlayerChunkTracker] Player 0 moved from Chunk(7, 5) to Chunk(8, 6)
[PlayerChunkTracker] Player 0 moved from Chunk(8, 6) to Chunk(10, 0)
[PlayerChunkTracker] Player 0 moved from Chunk(10, 0) to Chunk(8, 6)
[PlayerChunkTracker] Player 0 moved from Chunk(8, 6) to Chunk(10, 0)
```

**Chunk(10, 0) и Chunk(8, 6) — это НЕ соседние чанки!**
- Chunk size = 2000 units
- Chunk(10, 0) = x: 20000-22000, z: 0-2000
- Chunk(8, 6) = x: 16000-18000, z: 12000-14000
- Расстояние между центрами ≈ 6000 units!

Это означает что **позиция oscills** между двумя далёкими точками.

### Наблюдение 2: FloatingOriginMP позиция стабильна

```
[FloatingOriginMP] Debug: mode=ServerAuthority, cameraWorldPos=(20789, -339, 300), dist=20794
```

`cameraWorldPos` стабильна, НЕ меняется между кадрами.

### Наблюдение 3: ДВА источника обновления

В логах видно **ДВА пути** вызова `UpdatePlayerChunk`:

1. **Из `NetworkPlayer.FixedUpdate()`** (наш исправленный метод):
```
NetworkPlayer.UpdatePlayerChunkTracker()
    → PlayerChunkTracker.ForceUpdatePlayerChunk()
        → PlayerChunkTracker.UpdatePlayerChunk()
```

2. **Из `PlayerChunkTracker.Update()`** (старая корутина):
```
PlayerChunkTracker.Update()
    → PlayerChunkTracker.UpdatePlayerChunk()  // ← БЕЗ ForceUpdatePlayerChunk!
```

**КРИТИЧЕСКАЯ ПРОБЛЕМА:** Корутина в `PlayerChunkTracker.Update()` вызывает `UpdatePlayerChunk()` напрямую, используя **старую позицию** из `transform.position`, а НЕ нашу кэшированную позицию!

---

## Корень проблемы

### Код корутины в PlayerChunkTracker.Update()

В `PlayerChunkTracker.cs` строки ~100-130:

```csharp
private IEnumerator UpdatePlayerChunksCoroutine()
{
    while (true)
    {
        yield return new WaitForSeconds(0.5f);
        
        // Пытается найти NetworkPlayer через FindObjectsByType
        // ИСПОЛЬЗУЕТ transform.position напрямую, НЕ GetWorldPosition()!
        var networkPlayers = FindObjectsByType<NetworkPlayer>();
        foreach (var np in networkPlayers)
        {
            var chunkId = _chunkManager.GetChunkAtPosition(np.transform.position);  // ← OSCILLATING!
            // ...
        }
    }
}
```

**Эта корутина:**
1. Работает параллельно с `NetworkPlayer.FixedUpdate()`
2. Использует `np.transform.position` напрямую
3. НЕ использует `GetWorldPosition()` или наш кэш!
4. Вызывает `UpdatePlayerChunk()` который НЕ проходит через `ForceUpdatePlayerChunk()` и его hysteresis

### Почему кэш не помог

Кэш в `FloatingOriginMP` работает, но:
1. `UpdatePlayerChunkTracker()` использует `GetWorldPosition()` → правильно
2. НО `PlayerChunkTracker.Update()` вызывает `UpdatePlayerChunk()` напрямую → использует `np.transform.position` → OSCILLATES!

---

## Решение (для следующей итерации)

### Вариант A: Отключить корутину полностью

В `PlayerChunkTracker.Start()`:
```csharp
void Start()
{
    // Отключаем корутину — пусть NetworkPlayer управляет обновлением
    // StopAllCoroutines(); // ← Раскомментировать!
}
```

### Вариант B: Заставить корутину использовать GetWorldPosition()

В `PlayerChunkTracker.Update()`:
```csharp
// Вместо:
var chunkId = _chunkManager.GetChunkAtPosition(np.transform.position);

// Использовать:
var fo = FloatingOriginMP.Instance;
Vector3 worldPos = fo != null ? fo.GetWorldPosition() : np.transform.position;
var chunkId = _chunkManager.GetChunkAtPosition(worldPos);
```

### Вариант C: Добавить hysteresis в UpdatePlayerChunk()

Добавить проверку на близость к границе чанка во ВСЕ точки входа:
```csharp
private void UpdatePlayerChunk(ulong clientId, Vector3 position)
{
    // Проверка hysteresis (как в ForceUpdatePlayerChunk)
    // ...
}
```

---

## Архитектурная проблема

```
PlayerChunkTracker
    │
    ├── Update() [COROUTINE] --> UpdatePlayerChunk() --> np.transform.position (OSCILLATES!)
    │       ↑
    │       └─ FindObjectsByType<NetworkPlayer>() (unreliable!)
    │
    └── ForceUpdatePlayerChunk() --> UpdatePlayerChunk() --> worldPosition (STABLE)
            ↑
            └─ NetworkPlayer.FixedUpdate() --> GetWorldPosition() (uses cache)
```

**ДВА параллельных источника позиции** — это архитектурная ошибка.

---

## Рекомендация для Iteration 3.2

1. **Проанализировать корутину** в `PlayerChunkTracker.Update()` и `UpdatePlayerChunksCoroutine()`
2. **Отключить корутину** или переписать её на использование кэша
3. **Добавить hysteresis** во все точки входа `UpdatePlayerChunk()`
4. **Протестировать** с одним источником позиции

---

## Файлы для анализа (сабагенты)

### Network Programmer
```
Прочитать PlayerChunkTracker.cs строки 100-150:
1. Найти корутину UpdatePlayerChunksCoroutine()
2. Проверить использует ли она GetWorldPosition() или transform.position
3. Предложить конкретное исправление
```

### Gameplay Programmer
```
Прочитать PlayerChunkTracker.cs строки 200-280:
1. Найти все вызовы UpdatePlayerChunk()
2. Проверить какие используют hysteresis
3. Предложить унифицированное решение
```

### Unity Specialist
```
Проанализировать архитектуру:
1. Почему есть два источника обновления? (FixedUpdate + корутина)
2. Как согласовать их?
3. Нужна ли корутина вообще?
```

---

## Следующий шаг

**Iteration 3.2: Исправить дублирующий источник обновления**

1. Отключить корутину `UpdatePlayerChunksCoroutine()`
2. Полностью перейти на обновление из `NetworkPlayer.FixedUpdate()`
3. Протестировать oscillation

---

**Автор:** Claude Code  
**Завершение:** 18.04.2026, 17:36
