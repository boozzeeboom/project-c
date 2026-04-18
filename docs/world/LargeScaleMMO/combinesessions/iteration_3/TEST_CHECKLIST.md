# Iteration 3: Test Checklist

**Дата:** 18.04.2026  
**Статус:** Готово к тестированию

---

## 🎯 Критерии приёмки

| # | Критерий | Метод проверки |
|---|----------|----------------|
| 1 | NetworkPlayer обновляет PlayerChunkTracker | Console: `[PlayerChunkTracker] Player X moved from chunk A to chunk B` |
| 2 | RPC LoadChunk отправляется при смене чанка | Console: `[PlayerChunkTracker] Loading chunk X,Y by server command` |
| 3 | RPC UnloadChunk отправляется при выходе из радиуса | Console: `[PlayerChunkTracker] Unloading chunk X,Y by server command` |

---

## 🧪 Тестирование в Host режиме

### Шаг 1: Запуск
1. Открыть сцену `ProjectC_1`
2. Play Mode → Start as Host
3. Дождаться загрузки игрока

### Шаг 2: Проверка интеграции
1. Открыть Console
2. Найти строку: `[PlayerChunkTracker] Client 0 connected at chunk X,Y`
3. **Ожидаемый результат:** Чанк соответствует позиции игрока (0,0) если игрок near origin

### Шаг 3: Перемещение
1. WASD перемещение по миру
2. Проверить Console каждые 0.5 секунды
3. **Ожидаемый результат:** `[PlayerChunkTracker] Player 0 moved from X,Y to Z,W`

### Шаг 4: RPC проверка
1. Включить `showDebugLogs = true` в PlayerChunkTracker
2. Переместиться на расстояние > 1 чанка (2000 units)
3. **Ожидаемый результат:** 
   - `LoadChunkClientRpc sent to client 0 for chunk Z,W`
   - `[Client] Loading chunk Z,W by server command`

### Шаг 5: Выгрузка чанков
1. Продолжить движение в одном направлении
2. **Ожидаемый результат:**
   - `UnloadChunkClientRpc sent to client 0 for chunk X,Y`
   - `[Client] Unloading chunk X,Y by server command`

---

## 🔍 Debug-параметры

### PlayerChunkTracker
```csharp
[Header("Debug")]
[SerializeField] private bool showDebugLogs = true;  // Включить логирование
```

### NetworkPlayer (Inspector)
```csharp
[Header("Chunk Streaming (Iteration 3)")]
PlayerChunkTracker _playerChunkTracker   // Назначить вручную или auto-find
float chunkTrackerUpdateInterval = 0.25f // Интервал обновления
```

---

## 📊 Ожидаемые результаты

| Метрика | До (I2) | После (I3) |
|---------|---------|------------|
| Player → ChunkTracker update | Indirect (coroutine) | Direct (FixedUpdate) |
| Update frequency | 0.5s | 0.25s (configurable) |
| Смена чанка → RPC | Unreliable | Reliable |
| Chunks loaded per client | ~5 | ~9 (radius 2) |

---

## ⚠️ Возможные проблемы

### Проблема 1: PlayerChunkTracker не найден
```
[NetworkPlayer] PlayerChunkTracker not found
```
**Решение:** Проверить что PlayerChunkTracker существует на сцене

### Проблема 2: RPC не вызывается
```
[PlayerChunkTracker] Client 0 chunk unchanged
```
**Решение:** Проверить `loadRadius` (должен быть > 0)

### Проблема 3: Chunks не загружаются
```
[Client] Loading chunk X,Y by server command
...но чанк не появляется...
```
**Решение:** Проверить WorldStreamingManager существует

---

**Автор:** Claude Code  
**Дата:** 18.04.2026