# Iteration 3.6 — BUG FOUND: Infinite Server Shift

**Дата:** 18.04.2026  
**Статус:** 🔴 КРИТИЧЕСКИЙ БАГ ОБНАРУЖЕН  
**Предыдущие итерации:** I3.1 ✅ I3.2 ✅ I3.3 ✅ I3.4 ✅ I3.5 ✅

---

## 🔴 Критический Баг: Бесконечный Рост Offset

### Симптомы в логах:

```
[FloatingOriginMP] SERVER SHIFT: offset=(150000.00, 0.00, 150000.00), cameraPos=(150000.00, 503.28, 150000.00)
[FloatingOriginMP] SERVER SHIFT complete: totalOffset=(300000.00, 0.00, 300000.00)

[FloatingOriginMP] SERVER SHIFT: offset=(150000.00, 0.00, 150000.00), cameraPos=(150000.00, 503.28, 150000.00)
[FloatingOriginMP] SERVER SHIFT complete: totalOffset=(450000.00, 0.00, 450000.00)

[FloatingOriginMP] SERVER SHIFT: offset=(150000.00, 0.00, 150000.00), cameraPos=(150000.00, 503.28, 150000.00)
[FloatingOriginMP] SERVER SHIFT complete: totalOffset=(600000.00, 0.00, 600000.00)

... повтор бесконечно
```

**`totalOffset` растёт бесконечно!**

---

## 🔍 Root Cause Analysis

### Проблема: `positionSource` — это камера на TradeZones

**Иерархия сцены:**
```
TradeZones (GameObject)
├── FloatingOriginMP (Camera)
│   └── position = (150000, 500, 150000) ← НЕ сдвигается!
└── ThirdPersonCamera (Camera)
```

**Что происходит при `ApplyServerShift()`:**

1. `ApplyShiftToAllRoots()` сдвигает WorldRoot на `-offset`
2. TradeZones **НЕ сдвигается** (восстанавливается на `(0,0,0)`)
3. Камера (дочерняя TradeZones) — её `position` **остаётся `(150000, 500, 150000)`**

**Результат:**
```
cameraPos = (150000, 500, 150000)
|cameraPos| = 212,132 > threshold (150,000)

В СЛЕДУЮЩИЙ КАДР:
LateUpdate() вызывает GetWorldPosition() → получает (150000, 500, 150000)
distFromOrigin = 212,132 > threshold → вызывает ApplyServerShift() СНОВА!

totalOffset = 300000 → 450000 → 600000 → 750000 → ... (бесконечно)
```

---

## 🔧 Почему камера не сдвигается?

### FloatingOriginMP.ApplyShiftToAllRoots():

```csharp
// СНАЧАЛА найдём ВСЕ TradeZones в сцене
List<Transform> allTradeZones = new List<Transform>();
// ... поиск TradeZones ...

// Запоминаем позиции ДО сдвига
Dictionary<Transform, Vector3> tradeZonesPositions = new Dictionary<Transform, Vector3>();
foreach (var tz in allTradeZones)
{
    tradeZonesPositions[tz] = tz.position;
}

// Сдвигаем world roots
foreach (var root in _worldRoots)
{
    root.position -= offset;
}

// ВОССТАНАВЛИВАЕМ TradeZones на их оригинальные позиции
foreach (var tz in allTradeZones)
{
    if (tradeZonesPositions.TryGetValue(tz, out Vector3 originalPos))
    {
        tz.position = originalPos;  // ← TradeZones возвращается на (0,0,0)
    }
}
```

**TradeZones восстанавливается на `(0,0,0)`, а значит и камера на ней тоже!**

---

## 🎯 Решение

### Вариант 1: Использовать NetworkPlayer как positionSource

**Вместо камеры использовать NetworkPlayer transform:**

```csharp
// NetworkPlayer — дочерний TradeZones, но его позиция правильно сдвигается
// через Parent.position изменения
```

**Нужно:** В NetworkPlayer.UpdatePlayerChunkTracker() использовать `transform.position` напрямую (без GetWorldPosition).

### Вариант 2: Не вызывать LateUpdate в ServerAuthority

В режиме `ServerAuthority` LateUpdate **НЕ должен** вызывать GetWorldPosition() и проверять threshold — это делает сервер в FixedUpdate через PlayerChunkTracker.

**Фикс:**
```csharp
void LateUpdate()
{
    // Пропускаем ВЕСЬ LateUpdate в ServerAuthority режиме
    // Сервер управляет сдвигом через PlayerChunkTracker
    if (mode == OriginMode.ServerAuthority)
    {
        return;
    }
    
    // Остальной код...
}
```

---

## 📁 Документы для Следующей Итерации (I3.7)

| Документ | Описание |
|----------|---------|
| `SESSION_END_I36_BUG.md` | Этот документ — детали бага |
| `SESSION_PROMPT_I37.md` | План исправления |

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 18:56  
**Следующий шаг:** Реализовать Вариант 2 (отключить LateUpdate в ServerAuthority)
