# Iteration 3.8 — DEEP ANALYSIS: Offset Still Growing

**Дата:** 18.04.2026  
**Статус:** 🔴 КРИТИЧЕСКИЙ БАГ ВСЁ ЕЩЁ ПРИСУТСТВУЕТ  
**Предыдущие итерации:** I3.1 ✅ I3.2 ✅ I3.3 ✅ I3.4 ✅ I3.5 ✅ I3.6 ⚠️ I3.7 ⚠️

---

## 📋 Проблема

После телепортации на 1M+ и сдвига мира, цикличность продолжается. WorldRoot уходит в минус бесконечно:
```
WorldRoot NOW at: (-3150000, 0, -3150000)  // 1-й сдвиг
WorldRoot NOW at: (-3300000, 0, -3300000)  // 2-й сдвиг
WorldRoot NOW at: (-3450000, 0, -3450000)  // 3-й сдвиг
WorldRoot NOW at: (-3600000, 0, -3600000)  // и т.д.
```

---

## 🔍 Глубокий Анализ Логов

### Ключевые строки из лога:

```
[FloatingOriginMP] TradeZones NOW at: (0, 0, 0)
[FloatingOriginMP] _camera (FloatingOriginMP) NOW at: (150000, 500, 150000)
```

**Вопрос:** Почему TradeZones на (0,0,0), а камера `_camera` на `(150000, 500, 150000)`?

### Структура сцены (предполагаемая):

```
TradeZones (position: (0,0,0) после восстановления)
└── FloatingOriginMP (Camera) (position: ???)
    └── position = (150000, 500, 150000) — НО ПОЧЕМУ?
```

Если TradeZones на `(0,0,0)`, и камера — дочерний TradeZones, то камера должна быть рядом с origin, НЕ на `(150000, 500, 150000)`.

### Гипотеза 1: Камера — ЭТО TradeZones

Возможно, `FloatingOriginMP` назначен на TradeZone объект, а не на дочернюю камеру. Тогда:
- TradeZones восстанавливается на (0,0,0)
- `_camera` (который == TradeZones) остаётся на (150000, 500, 150000) — значит TradeZones НЕ на (0,0,0)!

Но лог показывает `TradeZones NOW at: (0, 0, 0)`.

### Гипотеза 2: Камера — отдельный объект

```
TradeZones (position: (0,0,0))
└── SomeCamera (position: (150000, 500, 150000))
    └── FloatingOriginMP (attached to this camera)
```

Если камера — дочерний TradeZones с большим локальным offset, то:
- TradeZones на (0,0,0)
- Камера на (0 + 150000, 0 + 500, 0 + 150000) = (150000, 500, 150000) — ЭТО ЛОКАЛЬНАЯ ПОЗИЦИЯ!

Если это так, то `GetWorldPosition()` использует `Camera.main` (который на TradeZones), и его мировая позиция — это `(0,0,0) + (150000, 500, 150000) = (150000, 500, 150000)`.

**ЭТО ПРОБЛЕМА!** Camera.main — это камера на TradeZones с локальным offset! Она НЕ сдвигается вместе с миром потому что TradeZones восстанавливается на (0,0,0), но камера остаётся на том же месте (дочерняя TradeZones).

---

## 🐛 Root Cause

### Проблема: Camera.main — это камера с локальным смещением

```
Scene Hierarchy:
TradeZones (position: (0,0,0))
└── SomeCameraName (local position: 150000, 500, 150000)
    └── FloatingOriginMP
```

1. After world shift, `TradeZones.position = (0,0,0)`
2. `SomeCameraName` (дочерний TradeZones) остаётся на `(150000, 500, 150000)` (локальная позиция)
3. `Camera.main` = `SomeCameraName` → позиция = `(150000, 500, 150000)` (мировая!)
4. `GetWorldPosition()` возвращает `(150000, 500, 150000)`
5. `ShouldUseFloatingOrigin()` → `150000 > 150000`? ДА (threshold!)
6. `RequestWorldShiftRpc()` → новый сдвиг!
7. Цикл повторяется бесконечно

### Почему наши фиксы не сработали?

1. **LateUpdate пропускается в ServerAuthority** — ✅ сработало, LateUpdate не вызывает сдвиг
2. **ShouldUseFloatingOrigin() использует GetWorldPosition()** — ⚠️ работает, но GetWorldPosition() возвращает неправильную позицию
3. **GetWorldPosition() ищет NetworkPlayer** — ⚠️ может не находить правильный объект

---

## 🔧 Решение (Варианты)

### Вариант 1: Игнорировать Camera.main полностью

В ServerAuthority режиме НЕ использовать `Camera.main` как источник позиции. Всегда искать NetworkPlayer.

### Вариант 2: Использовать `transform.position` игрока напрямую

В NetworkPlayer.UpdatePlayerChunkTracker() передавать `transform.position` (уже "локальная" позиция относительно TradeZones).

### Вариант 3: Проверить структуру сцены

В FloatingOriginMP.Awake() добавить логирование всей иерархии камеры:
```csharp
Camera cam = GetComponent<Camera>();
if (cam != null)
{
    Debug.Log($"Camera: {cam.name}, world pos: {cam.transform.position}");
    Debug.Log($"Camera parent: {cam.transform.parent?.name ?? "NONE"}");
    Debug.Log($"Camera local pos: {cam.transform.localPosition}");
}
```

---

## 📁 Требуется Информация

Для продолжения анализа нужно знать:

1. **Иерархия сцены:** Где находится FloatingOriginMP в сцене? (на каком объекте?)
2. **Камера:** Какая камера используется как `Camera.main`? Она на TradeZones?
3. **Локальная позиция камеры:** Какой локальный offset у камеры относительно TradeZones?

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 19:12 MSK  
**Следующий шаг:** Проверить структуру сцены, добавить логирование позиций камеры