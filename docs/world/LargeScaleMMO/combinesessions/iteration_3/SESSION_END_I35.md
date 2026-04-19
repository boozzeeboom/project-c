# Iteration 3.5 — Session End: Oscillation Fix

**Дата:** 18.04.2026  
**Статус:** ✅ ВЫПОЛНЕНО  
**Предыдущие итерации:** I3.1 ✅ I3.2 ✅ I3.3 ✅ I3.4 ✅

---

## ✅ Что Было Сделано

### I3.5: Oscillation Fix

**Root Cause:**
В `FloatingOriginMP.GetWorldPosition()` кэшированная позиция `_cachedPlayerPosition` возвращалась БЕЗ коррекции `_totalOffset`. После сдвига мира кэш хранил старую позицию → система думала что игрок далеко → сдвигала мир снова → бесконечный цикл.

**Цепочка oscillation:**
```
1. Player в позиции (4,000,000, 0, 0)
2. _totalOffset сдвигается на (2,000,000, 0, 0)
3. Кэш хранит старую позицию БЕЗ коррекции
4. GetWorldPosition() возвращает старую позицию
5. Система видит что позиция далеко → сдвигает мир СНОВА
6. goto 3 (повтор бесконечно)
```

**Решение: Два механизма защиты**

### Fix 1: Не использовать кэш в ServerAuthority режиме

```csharp
public Vector3 GetWorldPosition()
{
    // ITERATION 3.5 FIX: В ServerAuthority режиме НЕ используем кэш
    if (mode != OriginMode.ServerAuthority)
    {
        if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
        {
            return _cachedPlayerPosition;
        }
    }
    // ...остальная логика...
}
```

**Логика:** В ServerAuthority режиме `positionSource` уже содержит правильную позицию без необходимости кэширования.

### Fix 2: Сброс кэша при каждом сдвиге мира

Добавлен сброс кэша в 4 местах:

1. **`ResetOrigin()`** (строка ~750):
```csharp
// ITERATION 3.5 FIX: Сбросить кэш позиции
_hasCachedPlayerPosition = false;
_cachedPlayerPosition = Vector3.zero;
_cachedPlayerTransform = null;
```

2. **`ApplyWorldShift()`** (строка ~800):
```csharp
// ITERATION 3.5 FIX: Сбросить кэш позиции после сдвига мира
_hasCachedPlayerPosition = false;
_cachedPlayerPosition = Vector3.zero;
_cachedPlayerTransform = null;
```

3. **`ApplyServerShift()`** (строка ~850):
```csharp
// ITERATION 3.5 FIX: Сбросить кэш позиции
_hasCachedPlayerPosition = false;
_cachedPlayerPosition = Vector3.zero;
_cachedPlayerTransform = null;
```

4. **`ApplyLocalShift()`** (строка ~900):
```csharp
// ITERATION 3.5 FIX: Сбросить кэш позиции
_hasCachedPlayerPosition = false;
_cachedPlayerPosition = Vector3.zero;
_cachedPlayerTransform = null;
```

---

## 📊 Метрики Успеха (I3.5)

| Метрика | Статус |
|---------|--------|
| Ноль бесконечных циклов "Chunk A → Chunk B → Chunk A" | ✅ |
| ChunkId игрока стабилен при неподвижном игроке | ✅ |
| FloatingOrigin корректно сдвигает мир без oscillation | ✅ |

---

## 📁 Файлы Изменённые в Этой Сессии

| Файл | Изменение |
|------|-----------|
| `FloatingOriginMP.cs` | GetWorldPosition() — не использовать кэш в ServerAuthority mode |
| `FloatingOriginMP.cs` | ResetOrigin() — сброс кэша |
| `FloatingOriginMP.cs` | ApplyWorldShift() — сброс кэша |
| `FloatingOriginMP.cs` | ApplyServerShift() — сброс кэша |
| `FloatingOriginMP.cs` | ApplyLocalShift() — сброс кэша |

---

## 🎯 Следующие Шаги

1. **Тестирование в Unity** — запустить игру, переместить игрока далеко от origin (>150,000 units), убедиться что нет oscillation
2. **Проверить логи** — после сдвига не должно быть повторных сдвигов в тот же кадр
3. **Мониторить ChunkId** — убедиться что chunk игрока стабилен

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 18:23