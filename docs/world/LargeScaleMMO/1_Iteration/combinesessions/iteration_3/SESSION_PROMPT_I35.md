# Iteration 3.5 — Session Prompt: Oscillation Fix

**Дата:** 18.04.2026  
**Статус:** ⏳ ГОТОВ К РЕАЛИЗАЦИИ  
**Предыдущие итерации:** I3.1 ✅ I3.2 ✅ I3.3 ✅ I3.4 ⚠️

---

## 📊 Контекст: Что Было Сделано

### I3.4: Graceful Degradation + RPC Reorder Protection ✅
- Убран спам `Debug.LogWarning` в ChunkLoader
- Добавлена защита от RPC reorder в WorldStreamingManager
- **НО:** Обнаружена новая проблема — oscillation

---

## 🔴 Проблема для Решения (I3.5)

### Oscillation: Бесконечный цикл смены чанков

**Симптомы в логах:**
```
[PlayerChunkTracker] Player 0 moved from Chunk(-1, 2) to Chunk(2, 1)
[PlayerChunkTracker] Player 0 moved from Chunk(2, 1) to Chunk(-1, 2)
[PlayerChunkTracker] Player 0 moved from Chunk(-1, 2) to Chunk(2, 1)
...повтор бесконечно
```

### Root Cause:

В `FloatingOriginMP.GetWorldPosition()` (строка 312):
```csharp
if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
{
    return _cachedPlayerPosition; // ❌ БЕЗ вычитания _totalOffset!
}
```

**Проблема:** `_cachedPlayerPosition` не корректируется при сдвиге мира через `_totalOffset`. Кэш хранит старую позицию, и система думает что игрок далеко от origin — вызывая бесконечные сдвиги.

### Цепочка oscillation:
```
1. Player в позиции (4,000,000, 0, 0)
2. _totalOffset сдвигается на (2,000,000, 0, 0)
3. Кэш хранит старую позицию БЕЗ коррекции
4. GetWorldPosition() возвращает старую позицию
5. Система видит что позиция далеко → сдвигает мир СНОВА
6. goto 2 (повтор бесконечно)
```

---

## 🎯 План Реализации

### Вариант 1: Не использовать кэш в ServerAuthority mode (Рекомендуемый)

**Логика:** В ServerAuthority режиме источник позиции уже известен (positionSource), поэтому кэш не нужен.

```csharp
public Vector3 GetWorldPosition()
{
    // В ServerAuthority режиме НЕ используем кэш — он устаревает после сдвига
    // positionSource уже содержит правильную позицию
    if (mode != OriginMode.ServerAuthority)
    {
        if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
        {
            return _cachedPlayerPosition;
        }
    }
    
    // Дальше существующая логика с positionSource...
}
```

### Вариант 2: Корректировать кэш при сдвиге мира

**Логика:** При вызове `ApplyWorldShift()` обнулять `_hasCachedPlayerPosition`.

```csharp
public void ApplyWorldShift(Vector3 offset)
{
    // ...существующий код...
    
    // I3.5 FIX: Сбросить кэш чтобы получить свежую позицию
    _hasCachedPlayerPosition = false;
    _cachedPlayerPosition = Vector3.zero;
    _cachedPlayerTransform = null;
    
    // ...существующий код...
}
```

### Вариант 3: Проверять валидность кэша

**Логика:** Проверять что кэш не старше последнего сдвига.

```csharp
public Vector3 GetWorldPosition()
{
    if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
    {
        // Проверяем что с момента последнего обновления не было сдвига
        if (_cachedPlayerPosition.magnitude > threshold * 0.5f)
        {
            // Позиция далеко — возможно кэш устарел
            // Используем positionSource напрямую
            _hasCachedPlayerPosition = false;
        }
        else
        {
            return _cachedPlayerPosition;
        }
    }
    
    // ...существующая логика...
}
```

---

## 📁 Файлы для Изменения

| Файл | Метод | Изменение |
|------|-------|-----------|
| `FloatingOriginMP.cs` | `GetWorldPosition()` | Не использовать кэш в ServerAuthority mode |
| `FloatingOriginMP.cs` | `ApplyWorldShift()` | Сбросить кэш после сдвига |

---

## ✅ Метрики Успеха

После исправления:
- [ ] Ноль бесконечных циклов "Chunk A → Chunk B → Chunk A"
- [ ] ChunkId игрока стабилен при неподвижном игроке
- [ ] FloatingOrigin корректно сдвигает мир без oscillation

---

## ⚠️ User Instructions

**Требования:**
1. ✅ Писать код
2. ✅ Тестировать в Unity
3. ✅ Документировать изменения

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 18:18  
**Документы:**
- `SESSION_END_I34.md` — итоги I3.4
- `FloatingOriginMP.cs` — файл для изменения
