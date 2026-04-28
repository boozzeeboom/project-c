# Iteration 3: Журнал попыток решения проблем

**Дата создания:** 18.04.2026, 21:00 MSK  
**Статус:** АКТИВНАЯ ИТЕРАЦИЯ  
**Проблема:** Бесконечный цикл сдвигов мира + chunk oscillation на границе

---

## 📊 СВОДКА ПОПЫТОК

| # | Дата | Подход | Файл | Результат |
|---|------|--------|------|-----------|
| 1 | 17.04 | GetWorldPosition() вместо transform.position | FloatingOriginMP.cs | ⚠️ Стабилизировало, но не полностью |
| 2 | 17.04 | Добавить ссылку на PlayerChunkTracker | NetworkPlayer.cs | ✅ Работает |
| 3 | 18.04 | Убрать ServerAuthority из LateUpdate | FloatingOriginMP.cs | ⚠️ Offset всё ещё растёт |
| 4 | 18.04 | Distance от TradeZones вместо magnitude | FloatingOriginMP.cs | ❌ Равно magnitude (TradeZones=0) |
| 5 | 18.04 | ThirdPersonCamera.position как референс | FloatingOriginMP.cs | ❌ Камера oscills |
| 6 | 18.04 | (playerPosition - totalOffset).magnitude | FloatingOriginMP.cs | ⚠️ Работает частично |
| 7 | 18.04 | TeleportOwnerPlayerToOrigin() | FloatingOriginMP.cs | ❌ Не реализовано |
| 8 | 18.04 | Добавить cooldown 0.5s | FloatingOriginMP.cs | ✅ Помогает, но не лечит |
| 9 | 18.04 | TeleportOwnerPlayerToOrigin() + обновить кэш | FloatingOriginMP.cs | ✅ РЕАЛИЗОВАНО |
| 10 | 18.04 | DEBUG: Добавить логи для трассировки позиции | NetworkPlayer.cs, PlayerChunkTracker.cs | ✅ Добавлено |
| 11 | 18.04 | Y coordinate oscillation — добавлены логи в TeleportOwnerPlayerToOrigin | FloatingOriginMP.cs, NetworkPlayer.cs | 🔄 В РАБОТЕ |

---

## 🔴 ПРОБЛЕМА (ROOT CAUSE)

### Описание
После сдвига мира происходят:
1. **Бесконечный цикл сдвигов** — totalOffset растёт экспоненциально
2. **Chunk oscillation** — игрок oscills между соседними чанками
3. **cameraPos не меняется** — позиция остаётся большой после сдвига

### Корневая причина
```
Архитектура FloatingOrigin:
- TradeZones (корень сцены) — НЕ сдвигается, всегда на (0,0,0)
- WorldRoot (дочерний TradeZones) — СДВИГАЕТСЯ
- Player (НЕ в excludeFromShift?) — ОСТАЁТСЯ на месте
- ThirdPersonCamera — oscills между позициями

После ApplyShiftToAllRoots():
- TradeZones.restore(0,0,0) — на месте
- WorldRoot.position -= offset — сдвинулся
- Player.position — НЕ сдвинулась!
- cameraPos = oldPosition — не обновилась

Результат:
- trueDist = cameraPos - totalOffset = огромное значение
- ShouldUseFloatingOrigin() = TRUE
- → Новый сдвиг → бесконечный цикл
```

### Архитектурный вопрос
**Кто такой Player в этой системе?**
- Если Player — часть WorldRoot (сдвигается) → проблема в том что он НЕ сдвигается
- Если Player — часть TradeZones (не сдвигается) → проблема в проверке distance

---

## 📝 ДЕТАЛЬНЫЙ ЖУРНАЛ ПОПЫТОК

### Попытка #1: GetWorldPosition() (17.04)
**Файлы:** FloatingOriginMP.cs, NetworkPlayer.cs  
**Описание:** Использовать FloatingOriginMP.GetWorldPosition() вместо transform.position для определения чанка  
**Изменения:**
```csharp
// NetworkPlayer.cs — UpdatePlayerChunkTracker()
var floatingOrigin = FloatingOriginMP.Instance;
if (floatingOrigin != null)
{
    worldPosition = floatingOrigin.GetWorldPosition();
}
```
**Результат:** ⚠️ GetWorldPosition стабилен, но transform.position oscills
**Причина провала:** GetWorldPosition иногда выбирает неправильный NetworkObject (ghost вместо real player)

---

### Попытка #2: Интеграция PlayerChunkTracker (17.04)
**Файлы:** NetworkPlayer.cs  
**Описание:** Добавить прямую ссылку на PlayerChunkTracker и вызов из FixedUpdate  
**Изменения:**
```csharp
_playerChunkTracker = FindFirstObjectByType<PlayerChunkTracker>();
// В FixedUpdate:
_playerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, worldPosition);
```
**Результат:** ✅ Работает — сервер отправляет RPC при смене чанка
**Статус:** Успешно, но oscillation остался

---

### Попытка #3: Убрать ServerAuthority из LateUpdate (18.04)
**Файлы:** FloatingOriginMP.cs  
**Описание:** LateUpdate вызывал ApplyServerShift() в ServerAuthority режиме, вызывая бесконечный рост offset  
**Изменения:**
```csharp
// LateUpdate — ИЗМЕНЕНО
if (distFromOrigin > threshold)
{
    if (mode == OriginMode.Local)
    {
        ApplyLocalShift(cameraWorldPos);
    }
    // ServerAuthority и ServerSynced: управляется из NetworkPlayer
}
```
**Результат:** ⚠️ LateUpdate больше не вызывает сдвиг, но offset всё ещё растёт из-за RequestWorldShiftRpc()
**Причина провала:** NetworkPlayer.UpdatePlayerChunkTracker() вызывает RequestWorldShiftRpc() слишком часто

---

### Попытка #4: Distance от TradeZones вместо magnitude (18.04)
**Файлы:** FloatingOriginMP.cs — ShouldUseFloatingOrigin(), RequestWorldShiftRpc()  
**Описание:** Использовать расстояние от TradeZones вместо magnitude  
**Изменения:**
```csharp
GameObject tradeZones = GameObject.Find("TradeZones");
float distance = Vector3.Distance(playerPosition, tradeZones.transform.position);
return distance > threshold;
```
**Результат:** ❌ Равно magnitude, потому что TradeZones на (0,0,0)
**Причина провала:** `Distance(a, 0) = |a| = magnitude` — это то же самое!

---

### Попытка #5: ThirdPersonCamera.position как референс (18.04)
**Файлы:** FloatingOriginMP.cs  
**Описание:** Использовать ThirdPersonCamera.position вместо TradeZones  
**Логика:**
```
1. ThirdPersonCamera спавнится из префаба
2. После сдвига мира ThirdPersonCamera остаётся рядом с игроком
3. ThirdPersonCamera.position.magnitude = расстояние от origin
```
**Изменения:**
```csharp
excludeFromShift = new string[] {
    "ThirdPersonCamera", // НЕ сдвигается!
    // ...
};
```
**Результат:** ❌ Камера oscills между позициями
**Причина провала:** ThirdPersonCamera — персональная для каждого игрока, её позиция нестабильна

---

### Попытка #6: (playerPosition - totalOffset).magnitude (18.04)
**Файлы:** FloatingOriginMP.cs — ShouldUseFloatingOrigin(), RequestWorldShiftRpc()  
**Описание:** Вычислять "истинную" позицию игрока относительно TradeZones  
**Изменения:**
```csharp
// ShouldUseFloatingOrigin()
float distance = (playerPosition - _totalOffset).magnitude;
return distance > threshold;

// RequestWorldShiftRpc()
float dist = (cameraPos - _totalOffset).magnitude;
if (dist <= threshold) return;
```
**Результат:** ⚠️ Работает частично — trueDist уменьшается, но cameraPos не меняется
**Причина провала:** После сдвига cameraPos остаётся на старой позиции, не обновляется

---

### Попытка #7: TeleportOwnerPlayerToOrigin() (18.04)
**Файлы:** FloatingOriginMP.cs  
**Описание:** Телепортировать игрока рядом с TradeZones после сдвига мира  
**Логика:**
```
После сдвига:
- TradeZones = (0,0,0)
- Player.position = oldPos - offset → должно быть рядом с TradeZones
- Но NetworkTransform не обновляет позицию автоматически!
```
**Результат:** ❌ Не реализовано
**Статус:** Требует дальнейшей проработки

---

### Попытка #8: Добавить cooldown (18.04)
**Файлы:** FloatingOriginMP.cs  
**Описание:** Cooldown 0.5s между сдвигами  
**Изменения:**
```csharp
// RequestWorldShiftRpc()
if (Time.time - _lastShiftTime < _shiftCooldown)
{
    Debug.Log($"[FloatingOriginMP] Cooldown active, ignoring shift request");
    return;
}
```
**Результат:** ✅ Помогает ограничить спам сдвигов
**Статус:** Работает как временная мера, но не решает корневую проблему

---

### Попытка #9: TeleportOwnerPlayerToOrigin() + обновить кэш (18.04, 21:15) ✅ РЕАЛИЗОВАНО
**Файлы:** FloatingOriginMP.cs  
**Описание:** После телепортации игрока — обновить кэш позиции. ShouldUseFloatingOrigin() использует кэш.  
**Документ:** `NEW_SOLUTION_ANALYSIS.md`  
**Логика:**
```
После сдвига:
1. TeleportOwnerPlayerToOrigin() телепортирует игрока на (0, 5, 0)
2. Вызываем UpdateCachedPlayerPosition((0, 5, 0)) — обновляем кэш
3. ShouldUseFloatingOrigin() использует кэш вместо transform.position
4. Кэш = (0, 5, 0), distance = 5 < 150000 → НЕ сдвигаем!
```
**Изменения:**
```csharp
// TeleportOwnerPlayerToOrigin() — ДОБАВЛЕНО
// ITERATION 3.14 FIX: Обновить кэш после телепортации!
UpdateCachedPlayerPosition(localPos, netObj.transform);

// ShouldUseFloatingOrigin() — ИЗМЕНЕНО
// ITERATION 3.14: Приоритет — кэшированная позиция!
if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
{
    float distance = _cachedPlayerPosition.magnitude;
    return distance > threshold;
}
```
**Результат:** ✅ КОД РЕАЛИЗОВАН  
**Статус:** ТРЕБУЕТСЯ ТЕСТИРОВАНИЕ

---

## 🎯 ТЕКУЩЕЕ СОСТОЯНИЕ (I3.14)

### Что работает:
- ✅ PlayerChunkTracker получает обновления от NetworkPlayer
- ✅ RPC LoadChunk/UnloadChunk отправляются при смене чанка
- ✅ Cooldown ограничивает частоту сдвигов
- ✅ TeleportOwnerPlayerToOrigin() телепортирует игрока на (0, 5, 0)
- ✅ ShouldUseFloatingOrigin() использует кэш вместо transform.position
- ✅ Кэш обновляется после телепортации

### Ожидаемый результат после I3.14:
```
После сдвига мира:
  1. TeleportOwnerPlayerToOrigin() телепортирует игрока на (0, 5, 0)
  2. UpdateCachedPlayerPosition((0, 5, 0)) обновляет кэш
  3. ShouldUseFloatingOrigin() использует кэш: dist = 5 < threshold(150000)
  4. → ShouldUseFloatingOrigin() = FALSE → НЕ сдвигаем!
```

### Что ТРЕБУЕТ ТЕСТИРОВАНИЯ:
- [ ] Телепортация на 1M+ — totalOffset останавливается?
- [ ] Chunk oscillation — прекратился?
- [ ] ShouldUseFloatingOrigin() использует кэш правильно?

---

## 📋 РЕШЕНИЯ КОТОРЫЕ НЕ НУЖНО ПОВТОРЯТЬ

| Решение | Почему не работает |
|---------|-------------------|
| Distance от TradeZones | Равно magnitude (TradeZones=0) |
| ThirdPersonCamera.position | Нестабильна, oscills |
| position.magnitude | Не учитывает totalOffset |
| GetWorldPosition() через IsOwner | Может выбрать ghost объект |
| LateUpdate → ApplyServerShift() | Вызывает бесконечный рост offset |

---

## 🔜 СЛЕДУЮЩИЕ ШАГИ

### Шаг 1: Определить архитектуру
- [ ] Кто такой Player в системе координат?
- [ ] Должен ли Player сдвигаться вместе с WorldRoot?
- [ ] Какой объект должен быть "референсом" для проверки distance?

### Шаг 2: Реализовать правильный референс
Варианты:
- **A:** Player сдвигается с WorldRoot → проверять playerPosition.magnitude
- **B:** Player НЕ сдвигается → проверять (playerPos - TradeZones).magnitude
- **C:** Teleport игрока после сдвига мира → проверять что игрок рядом с TradeZones

### Шаг 3: Протестировать
- [ ] Телепортация на 1M+
- [ ] Проверка что totalOffset останавливается
- [ ] Проверка что chunk oscillation прекратился

---

### Попытка #10: DEBUG логи для трассировки позиции (18.04, 21:40) ✅ ДОБАВЛЕНО
**Файлы:** NetworkPlayer.cs, PlayerChunkTracker.cs  
**Описание:** Добавить отладочные логи для понимания какой позиции передаются в PlayerChunkTracker  
**Логика:**
```
В NetworkPlayer.UpdatePlayerChunkTracker():
  Debug.Log($"[NetworkPlayer] ForceUpdatePlayerChunk: worldPosition={worldPosition}, transform.position={transform.position}");
  
В PlayerChunkTracker.ForceUpdatePlayerChunk():
  Debug.Log($"[PlayerChunkTracker] ForceUpdatePlayerChunk: clientId={clientId}, position={position}");
```
**Результат:** 🔍 Обнаружена НОВАЯ ПРОБЛЕМА!  
**Новая проблема:** Y coordinate oscillates между ~350 и ~503 вместо того чтобы быть на ~(0, 5, 0)!

---

### Попытка #11: Y coordinate oscillation — добавлены расширенные логи (18.04, 21:55) 🔄 В РАБОТЕ
**Файлы:** FloatingOriginMP.cs, NetworkPlayer.cs  
**Описание:** Расширенные логи в TeleportOwnerPlayerToOrigin() для понимания почему телепортация не применяется  
**Новая проблема:**
```
После телепортации игрок в позиции ~(500, 350-503, 300) вместо ~(0, 5, 0)!
Y координата oscills: 352.55 → 503.00 → 349.70 → 503.00 → 346.24 → 503.00

Это выглядит как колебание между клиентской и серверной позицией!
```

**Возможные причины:**
1. **Client-side prediction восстанавливает старую позицию** (_hasServerPosition = true?)
2. **NetworkTransform перезаписывает позицию**
3. **CharacterController.Move() возвращает игрока**
4. **TeleportOwnerPlayerToOrigin() НЕ вызывается**

**Добавленные логи:**
```csharp
// FloatingOriginMP.TeleportOwnerPlayerToOrigin():
Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: ВЫЗВАН!");
Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: НАЙДЕН владелец {netObj.name}, позиция ДО={netObj.transform.position}");
Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: CC.enabled={cc.enabled}");
Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: позиция УСТАНОВЛЕНА в {localPos}, фактически={netObj.transform.position}");

// NetworkPlayer.FixedUpdate():
if (_hasServerPosition)
    Debug.Log($"[NetworkPlayer] FixedUpdate: _hasServerPosition=TRUE, serverPos={_serverPosition}, transform.pos={transform.position}");
```

**Статус:** Ожидание результатов тестирования

---

## 📁 СВЯЗАННЫЕ ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| `ARCHITECTURE_STRUCTURE.md` | Дерево решений и архитектурный анализ |
| `PROBLEM_ANALYSIS_STRUCTURE.md` | Структурированный анализ проблем |
| `I3_14_DEBUG_LOG_ANALYSIS.md` | Анализ критической проблемы Y oscillation |
| `I3_14_INVESTIGATION.md` | Расследование проблемы с чанками |
| `MASTER_PROMPT.md` | Мастер-промпт для продолжения сессий |

---

**Обновлено:** 18.04.2026, 21:55 MSK  
**Автор:** Claude Code  
**Версия:** iteration_3_v6

**Добавлена попытка #11:** Y coordinate oscillation — расширенные логи  
**Новая проблема:** Игрок oscills между ~350 и ~503 по Y вместо ~(0, 5, 0)  
**Документы:** `I3_14_DEBUG_LOG_ANALYSIS.md`, `MASTER_PROMPT.md`
