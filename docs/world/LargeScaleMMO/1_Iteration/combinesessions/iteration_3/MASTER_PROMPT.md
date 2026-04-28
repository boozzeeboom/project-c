# 🎯 MASTER PROMPT: Iteration 3 — Floating Origin + Chunk Streaming

**Версия:** 1.0  
**Дата:** 18.04.2026  
**Статус:** АКТИВНАЯ РАБОТА

---

## 🎯 ЦЕЛЬ

Исправить **бесконечный цикл сдвигов мира** и **chunk oscillation** в системе FloatingOriginMP для MMO игры.

---

## 📋 ТЕКУЩАЯ ПРОБЛЕМА

### Описание:
После телепортации игрока на большие координаты (150k+):
1. `totalOffset` продолжает расти бесконечно
2. Player position oscillates между ~350 и ~503 по Y
3. Загружаются стартовые чанки вместо дальних

### Ожидание:
```
После телепортации:
- totalOffset останавливается
- Player в позиции ~(0, 5, 0)
- Загружаются дальние чанки
```

### Реальность:
```
После телепортации:
- totalOffset растёт
- Player в позиции ~(500, 350-503, 300)
- Загружаются стартовые чанки
```

---

## 🔧 АРХИТЕКТУРА СИСТЕМЫ

### FloatingOriginMP
- Управляет сдвигом мира когда игрок > 150k от origin
- TradeZones НЕ сдвигается (всегда на 0,0,0)
- WorldRoot СДВИГАЕТСЯ
- После сдвига вызывается `TeleportOwnerPlayerToOrigin()` → игрок на (0,5,0)

### NetworkPlayer
- `UpdatePlayerChunkTracker()` вызывается из FixedUpdate()
- Обновляет кэш в FloatingOriginMP
- Вызывает `RequestWorldShiftRpc()` если нужно сдвинуть мир

### PlayerChunkTracker
- `ForceUpdatePlayerChunk(clientId, position)` обновляет чанк
- Вычисляет ChunkId через `GetChunkAtPosition(position)`

### Ключевые файлы:
```
Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs
Assets/_Project/Scripts/Player/NetworkPlayer.cs
Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs
Assets/_Project/Scripts/World/Streaming/WorldChunkManager.cs
```

---

## 📋 ЖУРНАЛ ПОПЫТОК (краткий)

| # | Подход | Результат |
|---|--------|-----------|
| 1-8 | Различные подходы к расчёту distance | ❌/⚠️ |
| 9 | TeleportOwnerPlayerToOrigin() + обновить кэш | ✅ Работает (бесконечный цикл остановлен) |
| 10 | DEBUG логи | 🔍 Обнаружена новая проблема: Y oscillation |

### Детали попыток:
См. `SOLUTION_ATTEMPTS_LOG.md` для полного журнала.

---

## 🚨 АКТИВНАЯ ПРОБЛЕМА

### Y Coordinate Oscillation
```
Лог показывает:
  Y = 352.55 → 503.00 → 349.70 → 503.00 → 346.24 → 503.00

Игрок oscills между ~350 и ~503 вместо того чтобы быть на ~(0, 5, 0)!
```

### Возможные причины:
1. **Client-side prediction восстанавливает старую позицию?**
2. **NetworkTransform перезаписывает позицию?**
3. **CharacterController.Move() возвращает игрока?**
4. **TeleportOwnerPlayerToOrigin() НЕ вызывается?**

---

## 📋 ПЛАН РАБОТЫ (для новой сессии)

### Шаг 1: Проверить вызов TeleportOwnerPlayerToOrigin()
```
Добавить лог в начало метода:
  Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: ВЫЗВАН!");
```

### Шаг 2: Проверить применение телепортации
```
Добавить лог после set position:
  Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: позиция={localPos}");
```

### Шаг 3: Проверить _hasServerPosition
```
В NetworkPlayer.FixedUpdate():
  После телепорта _hasServerPosition должна быть false!
  
Если true → клиентская коррекция восстанавливает старую позицию
```

### Шаг 4: Проверить CharacterController
```
В TeleportOwnerPlayerToOrigin():
  Проверить что CC.enabled = false ДО телепорта
  И CC.enabled = true ПОСЛЕ телепорта
```

### Шаг 5: Исправить проблему
```
Исправить код на основе данных из логов
```

---

## 📁 ДОКУМЕНТАЦИЯ

### Основные документы:
| Документ | Описание |
|----------|----------|
| `SOLUTION_ATTEMPTS_LOG.md` | Полный журнал попыток |
| `I3_14_DEBUG_LOG_ANALYSIS.md` | Анализ критической проблемы |
| `I3_14_INVESTIGATION.md` | Расследование проблемы с чанками |

### Архитектурные документы:
| Документ | Описание |
|----------|----------|
| `ARCHITECTURE_STRUCTURE.md` | Дерево решений |
| `PROBLEM_ANALYSIS_STRUCTURE.md` | Анализ структуры |

---

## 🔍 КАК ПРОВОДИТЬ ОТЛАДКУ

### 1. Добавляй логи СЕЛЕКТИВНО
```
- Только в критических точках
- Не спамь всю функцию
- Используй понятные имена переменных в логах
```

### 2. Фиксируй в ЛОГЕ:
```
- Что ожидалось
- Что произошло на самом деле
- Разницу между ними
```

### 3. Проверяй ОДНУ гипотезу за раз
```
- Выбери одну возможную причину
- Добавь лог для проверки
- Протестируй
- Переходи к следующей
```

### 4. НЕ повторяй одно и то же
```
- Перед новой попыткой проверяй SOLUTION_ATTEMPTS_LOG.md
- Если попытка уже была — пропусти её
- Если что-то не работает — не повторяй то же самое
```

---

## 🎯 ПРАВИЛА ДОКУМЕНТАЦИИ

### Что писать в LOG:
```
1. Дата и время
2. Что делал
3. Что ожидалось
4. Что произошло
5. Выводы
```

### Что НЕ писать:
```
- Огромные блоки кода (только ключевые строки)
- Длинные объяснения (краткость!)
- Спекуляции без данных
```

### Формат записи попытки:
```
### Попытка #N: [Краткое описание]
**Файлы:** [Список файлов]
**Логика:** [1-2 предложения]
**Изменения:** [Ключевые строки кода]
**Результат:** ✅/❌/⚠️ + краткое пояснение
```

---

## 🚀 БЫСТРЫЙ СТАРТ (для новой сессии)

### 1. Прочитай текущий статус:
```
docs/world/LargeScaleMMO/combinesessions/iteration_3/SOLUTION_ATTEMPTS_LOG.md
```

### 2. Прочитай активную проблему:
```
docs/world/LargeScaleMMO/combinesessions/iteration_3/I3_14_DEBUG_LOG_ANALYSIS.md
```

### 3. Определи следующий шаг:
```
Проверить: TeleportOwnerPlayerToOrigin вызывается?
ИЛИ
Проверить: _hasServerPosition восстанавливает позицию?
```

### 4. Добавь лог и протестируй

### 5. Зафиксируй результат в SOLUTION_ATTEMPTS_LOG.md

---

## 📞 КОНТАКТЫ (если нужно продолжить)

Ключевые методы для проверки:
- `FloatingOriginMP.TeleportOwnerPlayerToOrigin()`
- `NetworkPlayer.UpdatePlayerChunkTracker()`
- `PlayerChunkTracker.ForceUpdatePlayerChunk()`
- `NetworkPlayer.FixedUpdate()` (проверка _hasServerPosition)

---

**Обновлено:** 18.04.2026, 21:55 MSK  
**Автор:** Claude Code