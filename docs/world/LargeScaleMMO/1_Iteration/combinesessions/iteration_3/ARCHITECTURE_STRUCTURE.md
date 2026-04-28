# Iteration 3: Структурированный анализ архитектуры

**Дата создания:** 18.04.2026, 21:05 MSK  
**Статус:** АКТИВНАЯ ИТЕРАЦИЯ  
**Цель:** Создать структурированный документ для избежания повторения провальных решений

---

## 📐 АРХИТЕКТУРА FLOATINGORIGIN

### Иерархия объектов сцены

```
Scene Root (Корень сцены Unity)
│
└── TradeZones (GameObject, position: (0,0,0))
    │
    ├── FloatingOriginMP (Script) ← ОРИГИНАЛЬНЫЙ компонент
    │   └── Camera ← ЛОКАЛЬНАЯ камера для референса
    │
    ├── WorldRoot (GameObject) ← СДВИГАЕТСЯ
    │   ├── Mountains/
    │   ├── Clouds/
    │   └── Environment objects
    │
    ├── SpawnPoints/
    ├── NPCSpawners/
    └── ...
```

### Ключевые объекты

| Объект | Поведение при сдвиге | Роль |
|--------|---------------------|------|
| TradeZones | НЕ сдвигается, restore(0,0,0) | Точка отсчёта (origin) |
| WorldRoot | СДВИГАЕТСЯ (position -= offset) | Визуальный мир |
| FloatingOriginMP | Остаётся на TradeZones | Управление сдвигом |
| ThirdPersonCamera | СПАВНИТСЯ из префаба, НЕ на TradeZones | Персональная камета игрока |
| NetworkPlayer | НЕ сдвигается? | Игрок в мире |

### Вопросы архитектуры

1. **Кто такой Player в системе координат?**
   - Если Player — дочерний TradeZones (не сдвигается) → его позиция = мировые координаты
   - Если Player — дочерний WorldRoot (сдвигается) → его позиция = локальные координаты

2. **Что такое "Camera" в FloatingOriginMP?**
   - Это Camera на TradeZones? (локальная, не двигается)
   - Или ThirdPersonCamera? (спавнится из префаба)

3. **Кто должен быть референсом для проверки distance?**
   - TradeZones (0,0,0)?
   - ThirdPersonCamera.position?
   - NetworkPlayer.position?

---

## 🔄 ПРОЦЕСС СДВИГА МИРА

### Текущий алгоритм (ApplyServerShift)

```
1. RequestWorldShiftRpc(cameraPos)
   └── Проверка: (cameraPos - totalOffset).magnitude > threshold?
   
2. CalculateShift(cameraPos)
   └── offset = RoundShift(cameraPos)
   
3. ApplyShiftToAllRoots(offset)
   ├── TradeZones.restore(0,0,0)
   ├── WorldRoot.position -= offset
   └── excludeFromShift объекты НЕ двигаются
   
4. totalOffset += offset
   
5. OnWorldShifted() для всех подписчиков
   └── NetworkPlayer.UpdatePosition()
```

### Проблема

После шага 3:
- TradeZones = (0,0,0)
- WorldRoot сдвинулся
- **cameraPos НЕ изменилась** (она в excludeFromShift или это ThirdPersonCamera)
- **playerPos НЕ изменилась**

Результат:
```
trueDist = cameraPos - totalOffset = огромное значение
→ ShouldUseFloatingOrigin() = TRUE
→ Новый сдвиг → бесконечный цикл
```

---

## 🎯 ДЕРЕВО РЕШЕНИЙ

### Проблема 1: Бесконечный сдвиг мира

```
Вопрос: Почему trueDist огромный после сдвига?
│
├── cameraPos НЕ меняется после сдвига
│   │
│   ├── Почему: cameraPos в excludeFromShift?
│   │   │
│   │   ├── ДА: Камера не двигается, позиция = старая
│   │   │   └── Решение: НЕ использовать эту камеру как референс
│   │   │
│   │   └── НЕТ: Камера двигается вместе с чем-то
│   │       └── Решение: Определить что это за камера
│   │
│   └── Почему: NetworkTransform не синхронизирует сдвиг?
│       │
│       └── Решение: После сдвига — обновить NetworkTransform сервера
│
└── totalOffset растёт неправильно
    │
    ├── Причина: Проверка distance неправильная
    │   │
    │   └── Решение: Использовать правильную формулу
    │       ├── Если Player сдвигается с WorldRoot → playerPos.magnitude
    │       └── Если Player НЕ сдвигается → (playerPos - TradeZones).magnitude
    │
    └── Причина: Проверка происходит ДО обновления позиции
        │
        └── Решение: Проверять ПОСЛЕ синхронизации NetworkTransform
```

### Проблема 2: Chunk Oscillation

```
Вопрос: Почему игрок oscills между чанками?
│
├── transform.position нестабильна
│   │
│   ├── Причина: FloatingOriginMP сдвигает мир
│   │   │
│   │   └── Решение: Использовать GetWorldPosition() вместо transform.position
│   │
│   └── Причина: GetWorldPosition() выбирает ghost объект
│       │
│       └── Решение: Искать по IsOwner + конкретное имя объекта
│
└── Проверка границы чанка нестабильна
    │
    └── Решение: Добавить hysteresis в PlayerChunkTracker
```

---

## 📋 РЕШЕНИЯ КОТОРЫЕ НЕ РАБОТАЮТ

### 1. ❌ Distance от TradeZones
```csharp
// НЕ РАБОТАЕТ
float distance = Vector3.Distance(playerPosition, tradeZones.transform.position);
```
**Почему:** TradeZones всегда на (0,0,0) → distance = magnitude
**Попытка:** I38, I39
**Результат:** Равно magnitude — не решает проблему

### 2. ❌ ThirdPersonCamera.position
```csharp
// НЕ РАБОТАЕТ
return cam.position.magnitude > threshold;
```
**Почему:** ThirdPersonCamera — персональная для игрока, нестабильна
**Попытка:** I38
**Результат:** Oscillation между позициями

### 3. ❌ position.magnitude (без учёта offset)
```csharp
// НЕ РАБОТАЕТ
return playerPosition.magnitude > threshold;
```
**Почему:** Не учитывает что игрок мог сдвинуться с миром
**Попытка:** I31-I36
**Результат:** Неправильная проверка после сдвига

### 4. ❌ LateUpdate → ApplyServerShift() (ServerAuthority)
```csharp
// НЕ РАБОТАЕТ
void LateUpdate() {
    if (mode == OriginMode.ServerAuthority) {
        ApplyServerShift(cameraWorldPos); // БЕСКОНЕЧНЫЙ ЦИКЛ!
    }
}
```
**Почему:** LateUpdate вызывается после сдвига, видит старую позицию
**Попытка:** I31-I36
**Результат:** Offset растёт экспоненциально

### 5. ❌ GetWorldPosition() через IsOwner
```csharp
// НЕ РАБОТАЕТ
foreach (var netObj in serverAuthPlayers) {
    if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner) {
        return netObj.transform.position; // Может быть ghost!
    }
}
```
**Почему:** IsOwner проверяется в контексте вызывающего, ghost тоже имеет IsOwner
**Попытка:** I38
**Результат:** Выбирается неправильный объект

---

## ✅ РЕШЕНИЯ КОТОРЫЕ РАБОТАЮТ

### 1. ✅ Cooldown между сдвигами
```csharp
// РАБОТАЕТ (временно)
if (Time.time - _lastShiftTime < _shiftCooldown) {
    return; // Игнорируем запрос
}
_lastShiftTime = Time.time;
```
**Эффект:** Ограничивает частоту сдвигов
**Статус:** Работает как временная мера

### 2. ✅ PlayerChunkTracker интеграция
```csharp
// РАБОТАЕТ
_playerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, worldPosition);
```
**Эффект:** Сервер отправляет RPC при смене чанка
**Статус:** Успешно работает

### 3. ✅ (playerPosition - totalOffset).magnitude
```csharp
// РАБОТАЕТ (если правильно реализовано)
float trueDist = (playerPosition - _totalOffset).magnitude;
if (trueDist <= threshold) return;
```
**Эффект:** Вычисляет позицию относительно origin после сдвигов
**Статус:** Работает частично — требует синхронизации позиции

---

## 🔜 ПЛАН РЕШЕНИЯ

### Фаза 1: Определить архитектуру

```
[ ] 1. Определить: Player сдвигается с WorldRoot или нет?
[ ] 2. Определить: Какая камера используется как референс?
[ ] 3. Определить: Как синхронизировать позицию после сдвига?
```

### Фаза 2: Реализовать правильный сдвиг

```
[ ] 4. После ApplyShiftToAllRoots() — обновить позицию игрока
[ ] 5. Дождаться синхронизации NetworkTransform
[ ] 6. Только после этого — проверять ShouldUseFloatingOrigin()
```

### Фаза 3: Протестировать

```
[ ] 7. Телепортация на 1M+
[ ] 8. Проверка: totalOffset останавливается
[ ] 9. Проверка: chunk oscillation прекратился
```

---

## 📁 СВЯЗАННЫЕ ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| SOLUTION_ATTEMPTS_LOG.md | Журнал всех попыток решения |
| PROBLEM_ANALYSIS_STRUCTURE.md | Анализ текущей проблемы |
| SESSION_END_v2.md | Анализ oscillation |
| SESSION_END_I39.md | Фикс с (playerPosition - totalOffset) |

---

**Обновлено:** 18.04.2026, 21:05 MSK  
**Автор:** Claude Code  
**Версия:** iteration_3_v3