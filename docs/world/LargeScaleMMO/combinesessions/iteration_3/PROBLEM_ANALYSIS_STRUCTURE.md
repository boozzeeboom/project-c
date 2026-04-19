# FloatingOriginMP — СТРУКТУРИРОВАННЫЙ АНАЛИЗ ПРОБЛЕМ (ITERATION 3)

**Дата:** 18.04.2026, 21:05 MSK  
**Статус:** 🔴 АКТИВНАЯ РАБОТА  

---

## 📁 СВЯЗАННЫЕ ДОКУМЕНТЫ

> **⚠️ ВАЖНО:** Перед принятием нового решения — прочитайте эти документы!

| Документ | Описание |
|----------|----------|
| `SOLUTION_ATTEMPTS_LOG.md` | Полный журнал попыток решения |
| `ARCHITECTURE_STRUCTURE.md` | Дерево решений и архитектурный анализ |
| `NEW_SOLUTION_ANALYSIS.md` | Новое решение: TeleportOwnerPlayerToOrigin() + обновить кэш |

---

## 🔄 НОВОЕ РЕШЕНИЕ (Попытка #9)

**Дата:** 18.04.2026, 21:15 MSK  
**Документ:** `NEW_SOLUTION_ANALYSIS.md`

### Корневая причина (новое открытие):
```
TeleportOwnerPlayerToOrigin() вызывается ПОСЛЕ вычисления distance в RequestWorldShiftRpc().
dist вычисляется с ОРИГИНАЛЬНОЙ позицией (150000), а игрок телепортируется ПОСЛЕ.
На следующий кадр: transform.position = (0, 5, 0), но кэш не обновлён!
```

### Решение:
```
1. TeleportOwnerPlayerToOrigin() телепортирует игрока на (0, 5, 0)
2. Вызываем UpdateCachedPlayerPosition((0, 5, 0)) — обновляем кэш
3. ShouldUseFloatingOrigin() использует кэш вместо transform.position
4. Кэш = (0, 5, 0), distance = 5 < 150000 → НЕ сдвигаем!
```

### Статус: 🔄 В РАБОТЕ

## 📋 СТРУКТУРА ПРОБЛЕМ

### 1. СИМПТОМЫ (из логов)
- [x] Бесконечный цикл сдвигов мира
- [x] totalOffset растёт экспоненциально (6M → 6.75M → 6.9M)
- [x] trueDist огромный (9M+) хотя игрок на ~150k
- [x] cameraPos НЕ меняется после сдвига
- [ ] Chunk oscillation на границе чанков

### 2. ИСТОРИЯ ИСПРАВЛЕНИЙ

| Iteration | Дата | Подход | Файл | Результат |
|-----------|------|--------|------|-----------|
| I31-I36 | 17.04 | GetWorldPosition() | FloatingOriginMP.cs | ⚠️ Стабилизировало, но не полностью |
| I31-I36 | 17.04 | PlayerChunkTracker integration | NetworkPlayer.cs | ✅ Работает |
| I37 | 18.04 | Убрать ServerAuthority из LateUpdate | FloatingOriginMP.cs | ⚠️ Offset всё ещё растёт |
| I38 | 18.04 | Distance от TradeZones вместо magnitude | FloatingOriginMP.cs | ❌ Равно magnitude |
| I38 | 18.04 | ThirdPersonCamera.position как референс | FloatingOriginMP.cs | ❌ Камера oscills |
| I39 | 18.04 | (playerPosition - totalOffset).magnitude | FloatingOriginMP.cs | ⚠️ Работает частично |
| I3.10 | 18.04 | TeleportOwnerPlayerToOrigin() | FloatingOriginMP.cs | ❌ Не реализовано |
| I3.11 | 18.04 | Добавить cooldown 0.5s | FloatingOriginMP.cs | ✅ Помогает, но не лечит |

### 3. СВОДКА: ЧТО РАБОТАЕТ / НЕ РАБОТАЕТ

| Решение | Статус | Почему |
|---------|--------|--------|
| Distance от TradeZones | ❌ | Равно magnitude (TradeZones=0) |
| ThirdPersonCamera.position | ❌ | Нестабильна, oscills |
| position.magnitude | ❌ | Не учитывает totalOffset |
| GetWorldPosition() через IsOwner | ❌ | Может выбрать ghost объект |
| LateUpdate → ApplyServerShift() | ❌ | Вызывает бесконечный рост offset |
| Cooldown 0.5s | ✅ | Ограничивает спам сдвигов |
| PlayerChunkTracker integration | ✅ | RPC работают корректно |
| (playerPosition - totalOffset).magnitude | ⚠️ | Работает частично |

---

## 🎯 ДЕРЕВО РЕШЕНИЙ

> **⚠️ Перед принятием нового решения — проверьте не было ли оно уже опробовано!**

### Решения которые НЕ работают (НЕ ПОВТОРЯТЬ!)

| Решение | Попытка | Почему не работает |
|---------|---------|-------------------|
| Distance от TradeZones | I38, I39 | Равно magnitude (TradeZones=0) |
| ThirdPersonCamera.position | I38 | Нестабильна, oscills |
| position.magnitude | I31-I36 | Не учитывает totalOffset |
| GetWorldPosition() через IsOwner | I38 | Может выбрать ghost объект |
| LateUpdate → ApplyServerShift() | I31-I36 | Вызывает бесконечный рост offset |

### Решения которые РАБОТАЮТ

| Решение | Эффект |
|---------|--------|
| Cooldown 0.5s | Ограничивает спам сдвигов |
| PlayerChunkTracker integration | RPC работают корректно |
| (playerPosition - totalOffset).magnitude | ⚠️ Работает частично — требует синхронизации |

---

## 🧪 ПЛАН ДЕЙСТВИЙ (НОВОЕ РЕШЕНИЕ)

### Вопросы для определения архитектуры:

1. [ ] Кто такой Player в системе координат?
2. [ ] Должен ли Player сдвигаться вместе с WorldRoot?
3. [ ] Какой объект должен быть "референсом" для проверки distance?

### Реализация:

4. [ ] После ApplyShiftToAllRoots() — обновить позицию игрока
5. [ ] Дождаться синхронизации NetworkTransform
6. [ ] Только после этого — проверять ShouldUseFloatingOrigin()

### Тестирование:

7. [ ] Телепортация на 1M+
8. [ ] Проверка: totalOffset останавливается
9. [ ] Проверка: chunk oscillation прекратился

---

## 📝 СЛЕДУЮЩИЕ ШАГИ

1. [ ] Прочитать NetworkPlayer.cs — понять как работает позиция игрока
2. [ ] Прочитать TradeZone сцену — понять структуру
3. [ ] Определить правильную архитектуру
4. [ ] Реализовать исправление
5. [ ] Протестировать

---

## 🔗 СВЯЗАННЫЕ ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| SESSION_END_I39.md | Последняя попытка исправления |
| ANALYSIS_I38_ROOT_CAUSE.md | Анализ корневой причины |
| ANALYSIS_I38_CORRECT_FIX.md | Предложенное исправление |

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 20:37 MSK

---

## 📋 ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ (ИЗ ПРОШЛЫХ СЕССИЙ)

### 3. КОРНЕВЫЕ ПРИЧИНЫ

#### ПРИЧИНА 1: Позиция игрока не меняется после сдвига
```
Перед сдвигом:
  cameraPos = (-149989, 501, 149997)
  totalOffset = (-6450000, 0, 6750000)
  
После сдвига:
  cameraPos = (-149989, 501, 149997) ← НЕ ИЗМЕНИЛАСЬ!
  totalOffset = (-6600000, 0, 6900000)
  
Следующий trueDist:
  trueDist = |-149989 - (-6600000), 501 - 0, 149997 - 6900000|
           = |6450011|, 501, |-6750003|
           = sqrt(6450011² + 501² + 6750003²) ≈ 9.2M
```

#### ПРИЧИНА 2: TradeZones остаётся на (0,0,0)
```
TradeZones всегда на (0,0,0)
Игрок на большой позиции
→ Расстояние между ними = позиция игрока ≈ 150k
→ Каждый сдвиг добавляет 150k к totalOffset
→ trueDist = cameraPos - totalOffset → растёт
```

#### ПРИЧИНА 3: Координаты имеют противоположные знаки
```
cameraPos.x = -149989 (отрицательный)
totalOffset.x = -6600000 (отрицательный)

truePos.x = cameraPos.x - totalOffset.x
           = -149989 - (-6600000)
           = +6450011 ← ПОЛОЖИТЕЛЬНЫЙ!

trueDist = sqrt(6.45M² + 0.5k² + 6.75M²) ≈ 9.2M
```

---

## 🔍 АРХИТЕКТУРНЫЕ ПРОБЛЕМЫ

### ПРОБЛЕМА А: Где должен быть игрок после сдвига?

**Текущая архитектура:**
1. TradeZones остаётся на (0,0,0)
2. WorldRoot сдвигается
3. Игрок остаётся на месте (НЕ сдвигается)

**Вопрос:** Где должен быть игрок после сдвига мира?

**Варианты:**
1. **Вариант A:** Игрок остаётся на большой позиции (~150k от TradeZones)
   - Тогда trueDist = 150k > threshold = 150k? = НЕТ! (< threshold, так как 150k не > 150k)
   - Но логи показывают что сдвиги продолжаются...

2. **Вариант B:** Игрок должен сдвинуться вместе с миром
   - Тогда позиция игрока = oldPos - offset
   - truePos = (oldPos - offset) - totalOffset = oldPos - (oldOffset + offset)
   - Это корректно!

3. **Вариант C:** Игрок должен телепортироваться рядом с TradeZones
   - После сдвига: player.position = ~0
   - truePos = ~0 - totalOffset = ~(-totalOffset)
   - Это тоже даёт огромное значение!

### ПРОБЛЕМА Б: Как работает NetworkTransform?

**В режиме Host (ServerAuthority):**
- Сервер управляет позицией игрока
- NetworkTransform синхронизирует позицию с клиентами
- После сдвига мира: сервер должен обновить позицию игрока!

**Вопрос:** Обновляется ли NetworkTransform после ApplyShiftToAllRoots?

**Ответ:** НЕТ! ApplyShiftToAllRoots двигает только WorldRoot, НЕ игрока!

### ПРОБЛЕМА В: Почему позиция игрока не меняется?

После сдвига мира (ApplyShiftToAllRoots):
1. TradeZones.restore(0,0,0) — TradeZones на 0
2. WorldRoot.position -= offset — WorldRoot сдвигается
3. Игрок остаётся на месте — НЕ сдвигается!

**Это правильно?** Зависит от архитектуры:

**Если игрок — часть TradeZones (не сдвигается):**
- TradeZones на (0,0,0)
- Игрок рядом с TradeZones
- Distance = small < threshold

**Если игрок — часть WorldRoot (сдвигается):**
- После сдвига: игрок.position = oldPos - offset
- truePos = (oldPos - offset) - totalOffset = oldPos - (oldOffset + offset)
- Это даёт позицию относительно TradeZones

**Текущая ситуация в логах:**
- WorldRoot NOW at: (6750000, 0, -7050000) — WorldRoot сдвинулся
- TradeZones NOW at: (0, 0, 0) — TradeZones на месте
- cameraPos: (-149989, 501, 149997) — игрок НЕ сдвинулся!

---

## 💡 ГИПОТЕЗЫ РЕШЕНИЯ

### Гипотеза 1: Игрок должен сдвигаться вместе с WorldRoot

**Логика:**
- После сдвига мира, игрок.position = oldPos - offset
- truePos = (oldPos - offset) - totalOffset = oldPos - (oldOffset + offset)
- После первого сдвига: truePos = oldPos - oldOffset - offset
- Если oldPos ≈ oldOffset (игрок был далеко), то truePos ≈ -offset

**Пример:**
```
Перед сдвигом:
  playerPos = 150000
  totalOffset = 0
  
После сдвига на 150000:
  playerPos = 150000 - 150000 = 0 (если сдвигаем!)
  totalOffset = 150000
  
truePos = 0 - 150000 = -150000
|truePos| = 150000 > threshold? ДА!
```

**Это не решает проблему!**

### Гипотеза 2: НЕ использовать totalOffset в проверке

**Логика:**
- После сдвига мира, позиция игрока меняется (через NetworkTransform)
- Проверяем расстояние ОТНОСИТЕЛЬНО TradeZones напрямую
- TradeZones всегда на (0,0,0)
- Distance = playerPos.magnitude

**Пример:**
```
После сдвига на 150000:
  TradeZones = (0,0,0)
  playerPos = 0 (придвигался к TradeZones)
  
Distance = |0| = 0 < threshold → НЕ сдвигаем!
```

**Требование:** Игрок должен сдвинуться к TradeZones после сдвига мира!

### Гипотеза 3: Изменить архитектуру сдвига

**Текущая архитектура:**
```
TradeZones (0,0,0) ← НЕ сдвигается
WorldRoot (сдвигается)
Player (НЕ сдвигается?)
```

**Новая архитектура:**
```
TradeZones (сдвигается ВМЕСТЕ с Player)
WorldRoot (НЕ сдвигается?)

ИЛИ

TradeZones (НЕ сдвигается)
WorldRoot (НЕ сдвигается)
Player (сдвигается ВМЕСТЕ с TradeZones)
```

---

## 🎯 ПЛАН ДЕЙСТВИЙ

### Шаг 1: Определить архитектуру

**Вопрос:** Кто такой TradeZones?
- Это корень сцены?
- Это точка отсчёта?
- Это местоположение игрока?

### Шаг 2: Определить что должно сдвигаться

**Вопрос:** Что такое "мир" для Floating Origin?
- Только визуальные объекты (горы, облака)?
- Игрок?
- TradeZones?

### Шаг 3: Реализовать правильную логику

**Вариант A:** TradeZones = точка отсчёта (0,0,0)
```
После сдвига:
  TradeZones = (0,0,0)
  Player = ~0 (рядом с TradeZones)
  WorldRoot сдвигается
  
Distance = |Player - TradeZones| = |Player| ≈ 0
→ НЕ сдвигаем!
```

**Вариант B:** TradeZones сдвигается вместе с игроком
```
После сдвига:
  TradeZones = oldPos (не двигается в local space)
  Player = ~0 (сдвинулся к TradeZones)
  WorldRoot НЕ сдвигается
  
Distance = |Player - TradeZones| ≈ 0
→ НЕ сдвигаем!
```

### Шаг 4: Реализовать в коде

**Требуемые изменения:**
1. После ApplyShiftToAllRoots — обновить позицию игрока
2. Или: после сдвига мира — телепортировать игрока к TradeZones

---

## 📝 СЛЕДУЮЩИЕ ШАГИ

1. [ ] Прочитать NetworkPlayer.cs — понять как работает позиция игрока
2. [ ] Прочитать TradeZone сцену — понять структуру
3. [ ] Определить правильную архитектуру
4. [ ] Реализовать исправление
5. [ ] Протестировать

---

## 🔗 СВЯЗАННЫЕ ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| SESSION_END_I39.md | Последняя попытка исправления |
| ANALYSIS_I38_ROOT_CAUSE.md | Анализ корневой причины |
| ANALYSIS_I38_CORRECT_FIX.md | Предложенное исправление |

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 20:37 MSK
