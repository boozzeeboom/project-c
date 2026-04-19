# Iteration 3.14: Результаты тестирования

**Дата:** 18.04.2026, 21:15 MSK  
**Статус:** ⚠️ ЧАСТИЧНО РАБОТАЕТ — новая проблема обнаружена

---

## ✅ ЧТО РАБОТАЕТ

### 1. Бесконечный цикл сдвигов ОСТАНОВЛЕН
```
После телепорта на 150 000:
- totalOffset = 300 000 (было 150 000 до сдвига)
- Консоль НЕ спамит новыми сдвигами
- ShouldUseFloatingOrigin() сработал корректно
```

### 2. Телепортация работает
```
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: teleported NetworkPlayer to (0.00, 5.00, 0.00), cache updated
```
- Игрок телепортирован на (0, 5, 0)
- Кэш обновлён

### 3. TradeZones остаётся на месте
```
[FloatingOriginMP] TradeZones NOW at: (0, 0, 0)
[FloatingOriginMP] TradeZones restored: 1/1
```

### 4. WorldRoot сдвигается корректно
```
[FloatingOriginMP] WorldRoot NOW at: (-300000, 0, -300000)
```

---

## ❌ НОВАЯ ПРОБЛЕМА: Загрузка стартовых чанков

### Наблюдение:
После сдвига мира и телепорта, игрок попадает в стартовые чанки вместо дальних.

### Лог:
```
[PlayerChunkTracker] Player 0 moved from Chunk(75, 75) to Chunk(0, -1)
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, -2) by server command
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, -1) by server command
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, 0) by server command
```

### Анализ:
```
Игрок телепортирован на (0, 5, 0)
При chunk size = 2000:
  chunkX = floor(0 / 2000) = 0
  chunkZ = floor(0 / 2000) = 0
  
НО в логах показывает Chunk(0, -1) — это НЕВЕРНО!
```

### Возможная причина:
```
NetworkPlayer.UpdatePlayerChunkTracker() передаёт позицию в мировых координатах.
После сдвига мира позиция игрока в мировых координатах:
  transform.position = (0, 5, 0) — это ЛОКАЛЬНАЯ позиция относительно TradeZones
  НО PlayerChunkTracker использует её как МИРОВУЮ координату
  
Если WorldRoot сдвинут на (-300000, 0, -300000):
  Мировая позиция игрока = (0, 5, 0) - (-300000, 0, -300000) = (300000, 5, 300000)
  
chunkX = floor(300000 / 2000) = 150
chunkZ = floor(300000 / 2000) = 150
  
НО лог показывает Chunk(0, -1) — что-то не так!
```

### Альтернативная причина:
```
Возможно PlayerChunkTracker вычисляет чанк по-другому.
Проверить формулу в PlayerChunkTracker.UpdatePlayerChunk()
```

---

## 📋 ЛОГ ТЕЛЕПОРТАЦИИ (ключевые строки)

### Сдвиг мира:
```
[FloatingOriginMP] RequestWorldShiftRpc: cameraPos=(150000, 503, 150000), dist=212133, threshold=100000
[FloatingOriginMP] RequestWorldShiftRpc: SERVER processing - cameraPos=(150000, 503, 150000), offset=(150000, 0, 150000)
[FloatingOriginMP] Found 1 TradeZones in scene: TradeZones@(0, 0, 0)
[FloatingOriginMP] WorldRoot NOW at: (-300000, 0, -300000)
```

### Телепортация:
```
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: teleported NetworkPlayer to (0.00, 5.00, 0.00), cache updated
```

### OnWorldShifted (дважды):
```
[NetworkPlayer] OnWorldShifted: offset=(150000.00, 0.00, 150000.00), transform.position=(1.52, 0.58, -1.23), IsOwner=True
[NetworkPlayer] OnWorldShifted: коррекция сброшена, позиция=(1.52, 0.58, -1.23), cooldown=1s
[NetworkPlayer] OnWorldShifted: offset=(150000.00, 0.00, 150000.00), transform.position=(0.00, 5.00, 0.00), IsOwner=True
[NetworkPlayer] OnWorldShifted: коррекция сброшена, позиция=(0.00, 5.00, 0.00), cooldown=1s
```

### Загрузка чанков:
```
[PlayerChunkTracker] Player 0 moved from Chunk(75, 75) to Chunk(0, -1)
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, -2) by server command
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, -1) by server command
[PlayerChunkTracker] [Client] Loading chunk Chunk(-2, 0) by server command
```

---

## 🔍 ВОПРОСЫ ДЛЯ АНАЛИЗА

### Вопрос 1: Почему Chunk(0, -1)?
```
Игрок на (0, 5, 0)
Если chunk size = 2000:
  chunkX = floor(0 / 2000) = 0
  chunkZ = floor(0 / 2000) = 0
  
Ожидаем: Chunk(0, 0)
Факт: Chunk(0, -1)

Это означает что PlayerChunkTracker использует другую формулу!
```

### Вопрос 2: Почему загружаются чанки (-2, -2), (-2, -1), (-2, 0)?
```
LoadChunksForClient() загружает чанки вокруг текущего.
Если текущий чанк = (0, -1), то соседние:
  (-1, -2), (-1, -1), (-1, 0)
  (0, -2), (0, -1), (0, 0)
  (1, -2), (1, -1), (1, 0)

НО загружаются только (-2, -2), (-2, -1), (-2, 0) — это не соседние чанки!
Это указывает на ошибку в PlayerChunkTracker.UpdatePlayerChunk()
```

### Вопрос 3: Использует ли PlayerChunkTracker правильную позицию?
```
В NetworkPlayer.UpdatePlayerChunkTracker():
  worldPosition = floatingOrigin.GetWorldPosition();
  
Но после сдвига мира и телепорта:
  transform.position = (0, 5, 0) — локальная позиция
  GetWorldPosition() может возвращать неправильное значение!
```

---

## 🔜 СЛЕДУЮЩИЕ ШАГИ

### Шаг 1: Проверить PlayerChunkTracker.UpdatePlayerChunk()
- [ ] Какую позицию получает?
- [ ] Как вычисляет chunk?
- [ ] Почему показывает Chunk(0, -1) вместо Chunk(0, 0)?

### Шаг 2: Проверить NetworkPlayer.UpdatePlayerChunkTracker()
- [ ] Какую позицию передаёт в PlayerChunkTracker?
- [ ] Использует ли GetWorldPosition() или transform.position?

### Шаг 3: Исправить формулу вычисления чанка
- [ ] Если игрок на (0, 5, 0), ожидаем Chunk(0, 0)
- [ ] Если игрок на (300000, 5, 300000), ожидаем Chunk(150, 150)

### Шаг 4: Проверить LoadChunksForClient()
- [ ] Почему загружаются чанки (-2, -2), (-2, -1), (-2, 0)?
- [ ] Это радиус загрузки или ошибка?

---

## 📁 СВЯЗАННЫЕ ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| `SOLUTION_ATTEMPTS_LOG.md` | Журнал всех попыток |
| `PROBLEM_ANALYSIS_STRUCTURE.md` | Анализ структуры проблем |
| `NEW_SOLUTION_ANALYSIS.md` | Анализ решения I3.14 |

---

**Обновлено:** 18.04.2026, 21:15 MSK  
**Автор:** Claude Code  
**Версия:** iteration_3_test_v1