# Iteration 1 Session Prompt: Fix FloatingOriginMP Jitter & Integration

**Цель:** Исправить критические баги FloatingOriginMP и определить зоны ответственности.

**Длительность:** 1-2 сессии

**Критерий приёмки:** 
> F6 телепорт работает без jitter. Console показывает корректную позицию.
> FloatingOrigin и ChunkLoader НЕ конфликтуют.

---

## 📋 Задачи

### 1.1 Исправить GetWorldPosition() — Jitter Fix
**Файл:** `FloatingOriginMP.cs` строки ~500-600

**Текущий код (баг):**
```csharp
Vector3 GetWorldPosition() {
    return positionSource.position - _totalOffset;  // Двойной расчёт!
}
```

**Новый код:**
```csharp
Vector3 GetWorldPosition() {
    if (positionSource == null) return transform.position;
    
    float distToOrigin = positionSource.position.magnitude;
    // Если близко к origin — уже локальная позиция
    if (distToOrigin < threshold * 0.5f) {
        return positionSource.position;
    }
    
    return positionSource.position - _totalOffset;
}
```

### 1.2 Добавить ShouldUseFloatingOrigin() — Зоны ответственности
**Файл:** `FloatingOriginMP.cs`

```csharp
public bool ShouldUseFloatingOrigin() {
    if (positionSource == null) return false;
    return positionSource.position.magnitude > 150000f;
}
```

### 1.3 Добавить события для синхронизации
**Файл:** `FloatingOriginMP.cs`

```csharp
public System.Action<Vector3> OnFloatingOriginTriggered;
public System.Action OnFloatingOriginCleared;
```

---

## 🔍 Перед началом

Прочитать:
- `docs/world/LargeScaleMMO/CURRENT_STATE.md` — глубокий анализ проблем
- `docs/world/LargeScaleMMO/01_Architecture_Plan.md` — архитектура FloatingOriginMP
- `docs/world/LargeScaleMMO/ITERATION_PLAN.md` — план итераций

---

## 📝 Шаги выполнения

1. Открыть `FloatingOriginMP.cs`
2. Найти метод `GetWorldPosition()` (строки ~500-600)
3. Добавить проверку на близость к origin
4. Добавить метод `ShouldUseFloatingOrigin()`
5. Добавить события `OnFloatingOriginTriggered` и `OnFloatingOriginCleared`
6. Протестировать F6 телепортацию

---

## ✅ Тестирование

1. Запустить Play Mode
2. Нажать F6 → телепортация к (-250000, 500, -250000)
3. Проверить Console:
   - `positionSource=(?)`
   - `totalOffset=(-250000, 0, -250000)`  
   - `truePos=(-250000, ?, ?)` ← Должно быть правильно
4. Если jitter есть → вернуться к шагу 3

---

## 📊 Ожидаемые результаты

| Метрика | До | После |
|---------|-----|-------|
| Jitter после F6 | Да | Нет |
| Console logs | Неправильные координаты | Правильные координаты |
| Плавность движения | Дрожание | Гладкое |

---

**Автор:** Claude Code  
**Дата:** 18.04.2026  
**Статус:** Нужно выполнить