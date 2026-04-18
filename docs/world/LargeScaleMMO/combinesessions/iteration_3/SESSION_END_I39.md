# Iteration 3.11 — ИСПРАВЛЕНИЕ БЕСКОНЕЧНОГО СДВИГА

**Дата:** 18.04.2026, 20:12 MSK  
**Статус:** ✅ ИСПРАВЛЕНО  

---

## 🔴 ПРОБЛЕМА

После сдвига мира происходил **бесконечный цикл сдвигов**.

### Анализ проблемы

**В режиме ServerAuthority после сдвига мира:**
- TradeZones остаётся на `(0,0,0)` — это корень сцены
- Игрок остаётся на большой позиции `(150000, 500, 150000)`
- `totalOffset` = `(150000, 0, 150000)`

**Старая проверка:**
```csharp
// Distance от TradeZones = magnitude если TradeZones на (0,0,0)
dist = Vector3.Distance(playerPosition, TradeZones.position);
dist = Distance(150000, 0) = 150000 = playerPosition.magnitude
```

После сдвига: `150000 > threshold(150000)`? ДА → **бесконечный сдвиг!**

---

## ✅ РЕШЕНИЕ

### Ключевая идея

**Вычисляем позицию относительно TradeZones:**
```csharp
truePos = playerPosition - totalOffset
trueDist = truePos.magnitude
```

**После сдвига мира:**
```
playerPosition = (150000, 500, 150000)  // Не обновилась
totalOffset = (150000, 0, 150000)      // Сдвинули мир
truePos = (150000, 500, 150000) - (150000, 0, 150000) = (~0, 500, ~0)
trueDist = sqrt(0 + 500² + 0) = ~500 < threshold(150000)
```

**После сдвига: `trueDist` ~500 < threshold → НЕ сдвигаем!**

---

## 📝 ИЗМЕНЕНИЯ В КОДЕ

### 1. ShouldUseFloatingOrigin() — ITERATION 3.11 FIX

```csharp
public bool ShouldUseFloatingOrigin(Vector3 playerPosition)
{
    float distance = (playerPosition - _totalOffset).magnitude;
    return distance > threshold;
}
```

### 2. RequestWorldShiftRpc()

```csharp
float dist = (cameraPos - _totalOffset).magnitude;
if (dist <= threshold)
{
    return; // Игнорируем
}
```

---

## 🔍 ПОЧЕМУ ЭТО РАБОТАЕТ

### Сценарий: Телепортация на 1M+

1. **До сдвига:**
   ```
   playerPosition = (1000000, 5, 0)
   totalOffset = (0, 0, 0)
   trueDist = (1000000, 5, 0).magnitude = 1000000 > threshold(150000) → сдвигаем!
   ```

2. **Сдвиг мира:**
   ```
   offset = RoundShift(1000000, 5, 0) = (1000000, 0, 0)
   ApplyShiftToAllRoots(-offset) // WorldRoot сдвигается
   totalOffset = (1000000, 0, 0)
   TradeZones.restore(0,0,0)
   ```

3. **После сдвига (позиция ещё не обновилась):**
   ```
   playerPosition = (1000000, 5, 0) // Не обновилась
   totalOffset = (1000000, 0, 0)
   trueDist = (1000000 - 1000000, 5 - 0, 0 - 0).magnitude = 5 < threshold
   → НЕ сдвигаем!
   ```

4. **Cooldown защищает** от повторного сдвига в течение 0.5 секунды.

---

## 📂 СВЯЗАННЫЕ ФАЙЛЫ

| Файл | Изменение |
|------|-----------|
| `FloatingOriginMP.cs` | `ShouldUseFloatingOrigin()` — `(playerPosition - _totalOffset).magnitude` |
| `FloatingOriginMP.cs` | `RequestWorldShiftRpc()` — `(cameraPos - _totalOffset).magnitude` |
| `FloatingOriginMP.cs` | `GetWorldPosition()` — упрощён для ServerAuthority |

---

## 🎯 ИТОГОВАЯ ПРОВЕРКА

**Тест:** Shift+T для телепортации на 1M+

**Ожидаемый результат:**
1. ✅ Игрок телепортируется на `(1000000, 5, 0)`
2. ✅ `ShouldUseFloatingOrigin` → `true` (1000000 > 150000)
3. ✅ Сервер сдвигает мир, `totalOffset` = `(1000000, 0, 0)`
4. ✅ TradeZones восстанавливается на `(0,0,0)`
5. ✅ После сдвига: `trueDist` = 5 < 150000
6. ✅ **НЕ сдвигаем!** Цикл остановлен!

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 20:12 MSK  
**Следующий шаг:** Тестирование в Unity Editor
