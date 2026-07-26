# T-CAM14: Deep Audit — Устранение остаточной тряски

> **Дата:** 2026-07-26
> **Исполнитель:** Aura (Project C agent)
> **Статус:** ✅ Complete

---

## История контекста (что было ДО)

20+ коммитов исправляли дёрганье камеры:

| Коммит | Что делали | Итог |
|--------|-----------|------|
| T-CAM07 | Исключили слой цели из SphereCast | ❌ Сломало коллизию → T-CAM11 откатил |
| T-CAM08 | Выключили Lag + Adaptive | ❌ T-CAM10 восстановил |
| T-CAM09 | Выпилили всё до скелета | База для перестройки |
| T-CAM10 | Восстановили с правильной архитектурой | Lag 0.15s / Smooth 0.04s |
| T-CAM12 | +Цепной SphereCast + minDist clamp | smoothTime 0.04→0.06 |
| T-CAM13 | +Dead-zone 3mm + fall lag accel | smoothTime 0.06→0.08 |
| T-CAM05v2 | Near-clip перенесён на финальную позицию | Улучшение, но не решение |
| T-CAM05 exp | SmoothDamp→exp-Lerp + mouse dead-zone | ✅ |
| T-JITTER10/11 | Обнаружен Animator как источник jitter | skinnedMotionVectors=false |

**После всего этого — тряска при приближении к объектам сохранилась.**

---

## Аудит: 3 найденные проблемы

### A. Двойной near-clip constraint (ResolveCollision + SmoothPosition)

```
Каждый кадр при приближении к стене:
  ResolveCollision → SphereCast → позиция у стены (0.7m от lookTarget)
  SmoothPosition → exp-Lerp тянет к resolvedPos
  near-clip constraint → distToLook < 1.1m → HARD PUSH позиции
  transform.position = push'd позиция
Следующий кадр:
  ResolveCollision → снова позиция у стены (0.7m)
  ↑ цикл push-Lerp-push на частоте кадров
```

ResolveCollision — authority по геометрии, НО не знает про near-clip.
SmoothPosition — authority по сглаживанию, НО делает near-clip push постфактум.
Два источника истины для одного свойства (минимальная дистанция) → осцилляция.

**Решение:** near-clip constraint перенесён в ResolveCollision (`ClampNearClip` на всех return-путях). SmoothPosition теперь чистый exp-Lerp без побочных эффектов.

### B. Adaptive Distance: используется базовая дистанция вместо _targetDistance

```csharp
// БЫЛО (баг):
float desiredDist = _isShip ? shipDistance : distance; // всегда 5 или 18!
float ratio = actualDist / desiredDist;

// Если _targetDistance уже уменьшен до 3m адаптивной системой,
// ratio = actualDist / 5.0 — всегда < 0.7 → восстановление НИКОГДА не начнётся.
```

**Решение:** `desiredDist = _targetDistance` — используется текущая цель, а не базовая константа.

### C. positionSmoothTime 0.08s — ложный «фикс стабильности»

T-CAM10 задумывал: Lag 0.15s / Smooth 0.04s = **3.75×** (гарантированно без резонанса).
T-CAM12→T-CAM13 поднимали smoothTime (0.04→0.06→0.08) пытаясь убрать тряску.
Но реальная причина была в near-clip double-constraint (проблема A).

При 0.08s: Lag 0.15s / Smooth 0.08s = **1.875×** — фильтры конфликтуют.

**Решение:** `positionSmoothTime = 0.04f` — возврат к архитектуре T-CAM10.

---

## Изменения в коде

### SpringArmCamera.cs

1. **ResolveCollision**: добавлен `ClampNearClip()` на всех 4 return-путях. Это единый авторитетный источник минимальной дистанции.

2. **SmoothPosition**: убран блок near-clip constraint (строки 427-434 были). Теперь только exp-Lerp + recovery clamp.

3. **UpdateAdaptiveDistance**: `float desiredDist = _targetDistance` (вместо `_isShip ? shipDistance : distance`).

4. **positionSmoothTime**: `0.08f → 0.04f`.

5. **ClampNearClip**: новый static helper — выталкивает позицию если она ближе minDist к lookTarget.

---

## Пайплайн после T-CAM14

```
ReadInput → UpdateModeTransition → [minDist clamp на _currentDistance]
→ UpdateLag → ComputeDesiredPosition
→ ResolveCollision(+chain-cast +AntiPop +nearClip) → ЕДИНЫЙ АВТОРИТЕТ ПО ПОЗИЦИИ
→ UpdateAdaptiveDistance → SmoothPosition(только exp-Lerp) → UpdateLookAt
```

Архитектурный принцип: **ResolveCollision — единственный источник истины для позиции камеры.** Никакая другая стадия пайплайна не модифицирует позицию по геометрическим причинам.

---

## Параметры инспектора (актуальные)

| Группа | Параметр | Значение |
|--------|----------|----------|
| Collision | sphereCastRadius | 0.4 |
| Collision | wallOffset | 0.3 |
| Anti-Pop | antiPopTime | 0.2 |
| Recovery | recoverySpeed | 10 |
| Recovery | recoveryRatio | 0.4 |
| Lag | lagEnabled | true |
| Lag | lagHorizontalTime | 0.15 |
| Lag | lagVerticalTime | 0.05 |
| Lag | dynamicLagEnabled | true |
| Adaptive | adaptiveDistanceEnabled | true |
| Adaptive | adaptiveThreshold | 0.7 |
| Adaptive | adaptiveDelay | 0.5 |
| Adaptive | adaptiveSpeed | 3 |
| Adaptive | adaptiveRecoverySpeed | 2 |
| Smoothing | **positionSmoothTime** | **0.04** ← T-CAM14 |
| Smoothing | modeSwitchSmoothTime | 0.5 |
