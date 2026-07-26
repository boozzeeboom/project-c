# T-CAM10: Восстановление Camera Lag + Anti-Pop + Adaptive Distance + Wall Recovery

> **Дата:** 2026-07-26
> **Исполнитель:** Aura (Project C agent)
> **Статус:** ✅ Complete

---

## Что произошло (ретроспектива)

### T-CAM05: полная реализация (~550 строк)
Все системы: Lag, Adaptive, Anti-Pop, Recovery, Occlusion, FOV, Auto-Center.

### T-CAM06..08: серия «фиксов дёрганья»
Каждая итерация отключала одну систему:
- T-CAM06: исправлена формула лага (линейная → экспоненциальная), но SmoothTime поднят до 0.2s
- T-CAM07: lag отключён (`lagEnabled = false`), auto-center отключён
- T-CAM08: adaptive distance отключён, SmoothTime снижен до 0.08s

### T-CAM09: выпиливание до скелета
Оставлен только базовый пайплайн: ReadInput → ModeTransition → ComputeDesired → ResolveCollision (без anti-pop) → SmoothPosition (без recovery) → LookAt

### Корневая причина дёрганья
Lag (0.15s) и SmoothDamp (0.12s) работали с **близкими временны́ми константами**. Это создавало систему второго порядка с tendency к oscillation/overshoot при быстрых движениях.

## Архитектурное решение (T-CAM10)

```
target.position → [Lag: 0.15s XZ, 0.05s Y] → _lagTargetPos → [орбита] → desiredPos
    → [SphereCast + AntiPop: 0.2s] → resolvedPos
    → [Adaptive Distance: подстройка _targetDistance]
    → [SmoothDamp: 0.04s + Wall Recovery] → transform.position
```

**Ключевое правило:** Lag и SmoothDamp в **разных временны́х масштабах**:
- Lag = инерция/«чувство веса» (0.15s)
- SmoothDamp = технический anti-jitter фильтр (0.04s, в 3.75× быстрее)

Когда временны́е константы разнесены на порядок — фильтры не конфликтуют.

### Особые случаи

| Случай | Поведение |
|--------|-----------|
| **Корабль** | Lag отключён (`_isShip → skip UpdateLag`). Корабль большой и быстрый — камера следует мгновенно |
| **Телепорт** | Если `_lagTargetPos` дальше 100м от target — мгновенный снап |
| **Высокая скорость** | MaxLagDist clamp (10m) + dynamicLagEnabled: при беге lag уменьшается до 30% |
| **У стены** | Anti-pop: выход из коллизии задерживается на 0.2s. Камера не дёргается туда-сюда |
| **Узкое пространство** | Adaptive Distance: после 0.5s постоянной коллизии `_targetDistance` плавно уменьшается |
| **Выход из-за стены** | Wall Recovery: SmoothTime в 3× короче + maxSpeed 10 m/s |

---

## Сводка параметров инспектора

| Группа | Параметр | Значение | Назначение |
|--------|----------|----------|------------|
| Collision | `sphereCastRadius` | 0.4 | Радиус сферы коллизии |
| Collision | `wallOffset` | 0.3 | Отступ от стены |
| Anti-Pop | `antiPopTime` | 0.2 | Гистерезис выхода из коллизии |
| Recovery | `recoverySpeed` | 10 | Max скорость отъезда (m/s) |
| Recovery | `recoveryRatio` | 0.4 | Порог срабатывания |
| Lag | `lagEnabled` | true | Вкл/выкл инерцию |
| Lag | `lagHorizontalTime` | 0.15 | Инерция XZ (walk) |
| Lag | `lagVerticalTime` | 0.05 | Инерция Y (walk) |
| Lag | `dynamicLagEnabled` | true | Меньше лага при беге |
| Adaptive | `adaptiveDistanceEnabled` | true | Авто-дистанция |
| Adaptive | `adaptiveThreshold` | 0.7 | Порог срабатывания |
| Adaptive | `adaptiveDelay` | 0.5 | Задержка перед уменьшением |
| Adaptive | `adaptiveSpeed` | 3 | Скорость уменьшения |
| Adaptive | `adaptiveRecoverySpeed` | 2 | Скорость восстановления |
| Smoothing | `positionSmoothTime` | 0.04 | Anti-jitter фильтр |

---

## LateUpdate pipeline (8 шагов)

```
ReadInput → UpdateModeTransition → UpdateLag → ComputeDesiredPosition
→ ResolveCollision → UpdateAdaptiveDistance → SmoothPosition → UpdateLookAt
```

---

## Git

Коммит: см. CHANGELOG.md
