# Iteration 3.14: Расследование проблемы с чанками

**Дата:** 18.04.2026, 21:18 MSK  
**Статус:** 🔍 В ПРОЦЕССЕ РАССЛЕДОВАНИЯ

---

## ✅ ИСПРАВЛЕНО: Бесконечный цикл сдвигов

### Результат тестирования:
```
После телепорта на 150 000:
- totalOffset = 300 000 (было 150 000 до сдвига)
- Консоль НЕ спамит новыми сдвигами
- ShouldUseFloatingOrigin() сработал корректно
```

---

## ❌ НОВАЯ ПРОБЛЕМА: Загрузка стартовых чанков

После телепортации игрок попадает в стартовые чанки вместо дальних.

### Лог:
```
[PlayerChunkTracker] Player 0 moved from Chunk(75, 75) to Chunk(0, -1)
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, -2) by server command
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, -1) by server command
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, 0) by server command
```

---

## 🔍 АНАЛИЗ ПОТОКА ДАННЫХ

### Телепортация:
```
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: teleported NetworkPlayer to (0.00, 5.00, 0.00), cache updated
```

### Проверка: Какую позицию получает PlayerChunkTracker?
```
В NetworkPlayer.UpdatePlayerChunkTracker():
  1. floatingOrigin.UpdateCachedPlayerPosition(transform.position, transform)
     → Кэш обновлён: (0, 5, 0)
  
  2. worldPosition = floatingOrigin.GetWorldPosition()
     → GetWorldPosition() возвращает _cachedPlayerPosition = (0, 5, 0)
  
  3. _playerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, worldPosition)
     → PlayerChunkTracker получает позицию (0, 5, 0)
```

### Вычисление чанка:
```
WorldChunkManager.GetChunkAtPosition((0, 5, 0)):
  gridX = floor(0 / 2000) = 0
  gridZ = floor(5 / 2000) = 0
  → ChunkId(0, 0)
```

### Ожидание vs Реальность:
```
Ожидание: Chunk(0, 0)
Реальность: Chunk(0, -1)
```

---

## ❓ ВОПРОС: Почему Chunk(0, -1)?

### Возможные причины:

#### 1. GetWorldPosition() возвращает неправильную позицию?
```
После телепорта _cachedPlayerPosition = (0, 5, 0)
GetWorldPosition() должен вернуть (0, 5, 0)
НО возможно возвращает что-то другое?
```

#### 2. ForceUpdatePlayerChunk() не использует позицию?
```
ForceUpdatePlayerChunk(clientId, position):
  position = (0, 5, 0) — правильно
  НО возможно использует старую позицию из _playerChunks?
```

#### 3. OnWorldShifted обновляет позицию повторно?
```
[FloatingOriginMP] RequestWorldShiftRpc вызывает OnWorldShifted
NetworkPlayer.OnWorldShifted получает offset и может менять позицию
```

---

## 📋 ИЗВЛЕЧЁННЫЕ ДАННЫЕ ИЗ ЛОГОВ

### Позиция после телепорта:
```
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: teleported NetworkPlayer to (0.00, 5.00, 0.00), cache updated
```

### OnWorldShifted вызывается ДВАЖДЫ:
```
[NetworkPlayer] OnWorldShifted: offset=(150000.00, 0.00, 150000.00), transform.position=(1.52, 0.58, -1.23), IsOwner=True
[NetworkPlayer] OnWorldShifted: коррекция сброшена, позиция=(1.52, 0.58, -1.23), cooldown=1s

[NetworkPlayer] OnWorldShifted: offset=(150000.00, 0.00, 150000.00), transform.position=(0.00, 5.00, 0.00), IsOwner=True
[NetworkPlayer] OnWorldShifted: коррекция сброшена, позиция=(0.00, 5.00, 0.00), cooldown=1s
```

### Изменение чанка:
```
[PlayerChunkTracker] Player 0 moved from Chunk(75, 75) to Chunk(0, -1)
```

### Загрузка чанков:
```
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, -2) by server command
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, -1) by server command
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, 0) by server command
```

---

## 🔍 ГИПОТЕЗА: Проблема в OnWorldShifted

### Наблюдение:
```
1. Player телепортирован на (0, 5, 0)
2. OnWorldShifted первый вызов: позиция = (1.52, 0.58, -1.23) — это ДО телепорта!
3. OnWorldShifted второй вызов: позиция = (0, 5, 0) — это ПОСЛЕ телепорта
```

### Возможная проблема:
```
В NetworkPlayer.OnWorldShifted():
  - Может применяться какая-то коррекция
  - Координаты (1.52, 0.58, -1.23) выглядят как смещение от старой позиции
```

### Проверить:
```
NetworkPlayer.OnWorldShifted() — какой код выполняется при получении offset?
```

---

## 🔧 ДОБАВЛЕННЫЕ ОТЛАДОЧНЫЕ ЛОГИ

### 1. NetworkPlayer.UpdatePlayerChunkTracker() — строка 467:
```csharp
Debug.Log($"[NetworkPlayer] ForceUpdatePlayerChunk: worldPosition={worldPosition}, transform.position={transform.position}");
_playerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, worldPosition);
```

### 2. PlayerChunkTracker.ForceUpdatePlayerChunk() — строка 340:
```csharp
Debug.Log($"[PlayerChunkTracker] ForceUpdatePlayerChunk: clientId={clientId}, position={position}");
```

---

## 📋 СЛЕДУЮЩИЕ ШАГИ РАССЛЕДОВАНИЯ

### Шаг 1: Протестировать с новыми логами
- [ ] Запустить игру
- [ ] Телепортироваться на 150 000
- [ ] Собрать логи: какая позиция передаётся в PlayerChunkTracker?

### Шаг 2: Проанализировать логи
- [ ] Если позиция (0, 5, 0) → проблема в GetChunkAtPosition()
- [ ] Если позиция отличается → проблема в GetWorldPosition() или UpdateCachedPlayerPosition()

### Шаг 3: Исправить проблему
- [ ] Если проблема в GetChunkAtPosition() — исправить формулу
- [ ] Если проблема в GetWorldPosition() — исправить кэш
- [ ] Если проблема в позиции игрока — проверить телепортацию

---

## 📁 СВЯЗАННЫЕ ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| `I3_14_TEST_RESULTS.md` | Результаты тестирования |
| `SOLUTION_ATTEMPTS_LOG.md` | Журнал попыток |
| `FloatingOriginMP.cs` | FloatingOrigin с кэшем |
| `NetworkPlayer.cs` | UpdatePlayerChunkTracker |
| `PlayerChunkTracker.cs` | ForceUpdatePlayerChunk |

---

**Обновлено:** 18.04.2026, 21:18 MSK  
**Автор:** Claude Code  
**Версия:** iteration_3_investigation_v1