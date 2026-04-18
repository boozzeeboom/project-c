# Iteration 1: Session Start

**Дата:** 18.04.2026, 10:21 AM (UTC+5)  
**Версия:** v0.0.18-combined-sessions  
**Статус:** Started

---

## 📋 Analysis Summary

### Проблема из CURRENT_STATE.md

**Симптомы:**
- F6 телепортация вызывает jitter
- Console показывает неправильные координаты:
  ```
  positionSource=(-249998, 503, -250000)
  totalOffset=(-250000, 0, -250000)
  truePos=(2, 503, 0)  ← НЕПРАВИЛЬНО! Должно быть -250000
  ```

**Причина:**
- После телепортации `positionSource.position` УЖЕ включает сдвиг WorldRoot
- Код повторно вычитает `_totalOffset` → двойной расчёт

### Анализ FloatingOriginMP.cs

**Строки 216-307:** `GetWorldPosition()` 
- Имеет проверку `IsUnderWorldRoot()`
- НО не проверяет близость к origin перед вычитанием offset

---

## 📝 Tasks для выполнения

### Task 1.1: Исправить GetWorldPosition()
**Файл:** `FloatingOriginMP.cs` строки ~500-600

**Текущий код (баг в строках 232-235):**
```csharp
Vector3 truePos = positionSource.position - _totalOffset;
```

**Новый код (добавить проверку близости):**
```csharp
float distToOrigin = positionSource.position.magnitude;
if (distToOrigin < threshold * 0.5f) {
    return positionSource.position;  // Не вычитать offset!
}
Vector3 truePos = positionSource.position - _totalOffset;
```

### Task 1.2: Добавить ShouldUseFloatingOrigin()
**Файл:** `FloatingOriginMP.cs`

```csharp
public bool ShouldUseFloatingOrigin() {
    if (positionSource == null) return false;
    return positionSource.position.magnitude > 150000f;
}
```

### Task 1.3: Добавить события синхронизации
**Файл:** `FloatingOriginMP.cs`

```csharp
public System.Action<Vector3> OnFloatingOriginTriggered;
public System.Action OnFloatingOriginCleared;
```

---

## 🔍 Files to Study

1. `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs` - GetWorldPosition() lines 216-307
2. `docs/world/LargeScaleMMO/CURRENT_STATE.md` - Deep analysis
3. `docs/world/LargeScaleMMO/ITERATION_PLAN.md` - Fix code

---

## 📊 Expected Results

| Metric | Before | After |
|--------|--------|-------|
| Jitter after F6 | Yes | No |
| Console logs | Wrong coords | Correct coords |
| Motion smoothness | Trembling | Smooth |

---

## 📁 Output Files

After completion, this session will create:
- `docs/world/LargeScaleMMO/combinesessions/iteration_1/SESSION_REPORT.md`
- `docs/world/LargeScaleMMO/combinesessions/iteration_1/TEST_RESULTS.md`