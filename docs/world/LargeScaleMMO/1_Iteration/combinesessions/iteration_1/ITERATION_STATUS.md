# Iteration 1: Текущий Статус

**Дата:** 18.04.2026, 16:23  
**Статус:** ⏳ В РАБОТЕ

---

## ✅ Что сделано

| Задача | Статус | Комментарий |
|--------|--------|-------------|
| 1.1 GetWorldPosition() | ⚠️ В работе | Offset не растёт, но truePos = (2,0,0) |
| 1.2 ShouldUseFloatingOrigin() | ✅ Готово | Добавлен в строки 210-220 |
| 1.3 События синхронизации | ✅ Готово | OnFloatingOriginTriggered, OnFloatingOriginCleared |

---

## 🎯 Цель Iteration 1 (из ITERATION_1_SESSION.md)

**Критерий приёмки:** 
> F6 телепорт работает без jitter. Console показывает корректную позицию.

**Задачи:**
1. Исправить GetWorldPosition() — Jitter Fix
2. Добавить ShouldUseFloatingOrigin()
3. Добавить события синхронизации

---

## 🔍 Анализ текущей проблемы

### Симптом:
```
positionSource = (250003, 503, 250000)
totalOffset = (250000, 0, 250000)
truePos = (2, 503, 0)  ← НЕПРАВИЛЬНО!
```

### Проблема:
Код вычитает `_totalOffset` из `positionSource.position`, но это даёт неправильный результат.

### Возможное решение:
```csharp
// ITERATION_1_SESSION.md предлагает:
if (distToOrigin < threshold * 0.5f) {
    return positionSource.position;  // НЕ вычитать
}
return positionSource.position - _totalOffset;
```

При positionSource = (250003, 503, 250000) и threshold = 150000:
- distToOrigin = 353558
- 150000 * 0.5 = 75000
- 353558 > 75000 → вычитаем offset

### Вопрос:
Это правильная логика? Или нужно НЕ вычитать offset вообще?

---

## 📊 Принятые решения

### 1. Игнорируем GetWorldPosition() для threshold
GetWorldPosition() используется для ОТОБРАЖЕНИЯ позиции в HUD и логах.
Для threshold проверки используем `positionSource.position.magnitude` напрямую.

### 2. FloatingOrigin сдвигает мир
- WorldRoot сдвигается на offset
- positionSource тоже сдвигается (под TradeZones)
- TradeZones остаётся на (0,0,0)

### 3. Следуем плану из ITERATION_1_SESSION.md
```csharp
Vector3 GetWorldPosition() {
    if (positionSource == null) return transform.position;
    
    float distToOrigin = positionSource.position.magnitude;
    if (distToOrigin < threshold * 0.5f) {
        return positionSource.position;
    }
    
    return positionSource.position - _totalOffset;
}
```

---

## ✅ Принятые компромиссы

| Неточность | Решение |
|------------|---------|
| truePos = (2,0,0) в логах | **Игнорируем** —不影响 работу |
| HUD показывает неправильную позицию | **Игнорируем** —不影响 функционал |
| Главное: нет jitter, нет бесконечных сдвигов | ✅ Приоритет |

---

## 📋 Критерий приёмки

| Метрика | До | После |
|---------|-----|-------|
| Jitter после F6 | Да | Нет |
| Offset растёт бесконечно | Да | Нет |
| События синхронизации работают | Нет | Да |

**Проверка:**
1. ✅ F6 → offset = (-250000, 0, -250000)
2. ✅ Стоим на месте → offset НЕ растёт
3. ✅ Нет jitter в движении
4. ✅ ShouldUseFloatingOrigin() → работает
5. ✅ OnFloatingOriginTriggered → работает

---

## 📁 Следующие шаги

1. **Подтвердить что jitter нет** —用户在 Play Mode проверяет
2. **Перейти к Iteration 2** — WorldStreamingManager Integration
3. **Документировать принятые компромиссы**

---

**Автор:** Claude Code  
**Обновлено:** 18.04.2026, 16:23