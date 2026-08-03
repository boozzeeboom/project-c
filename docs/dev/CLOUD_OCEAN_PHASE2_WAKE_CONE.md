# CLOUD_OCEAN_PHASE2_WAKE_CONE — design note (T-CLOUD02)

> Working note (docs/dev/). Additive record, do not merge into other files.

## Date
2026-08-03

## Problem
Phase 2.2 («корабль разрезает облака») визуально даёт не «след за кораблём», а
«расходится над кораблём» — одиночный сплат ставится в позицию корабля
(`ShipWakeCloudCutter.Update` → `LocalDensityBuffer.SplatDensity(pos, ...)`),
буфер торроидальный и следует за кораблём → вырез остаётся вокруг корабля,
плотность копится в центре окна (CPU mirror: `center=33…40`), и по лучу камеры
проецируется как разрыв **над** кораблём.

## Decision
Заменить одиночный сплат в позицию корабля на **кильватерный конус позади**:
серия гауссовых сплатов вдоль вектора движения **за** кораблём
(`pos - dir * spacing * i`), радиус растёт с дистанцией (`r = CutRadius * (1 + growth * i)`).
Это даёт классический кильватер: облака расходятся за кормой конусом.

## Change
- `Assets/_Project/Scripts/World/Clouds/ShipWakeCloudCutter.cs`:
  - `Update()` — вместо одного сплата в `ShipTransform.position` — цикл из `ConeSegments`
    сплатов позади, с `ConeSpacing` шагом и `ConeRadiusGrowth` ростом радиуса.
  - Новые serialized-поля: `ConeSegments`, `ConeSpacing`, `ConeRadiusGrowth` (Header "Wake Cone").
  - Сохранены `CutRadius`, `CutAmount`, `MinSpeed`, `SplatInterval` (инспектор не ломается).
- `Assets/_Project/Scripts/World/Clouds/LocalDensityBuffer.cs`:
  - `_splatQueue` 16 → 64 (конус генерит до 8 сплатов за тик, старый лимит дропал сплаты с warning).

## Not changed
- Compute shader, raymarch shader, RenderFeature, ассеты — не трогаем.
- `_LocalDensityInfluence`, `CloudTopY` и пр. — не трогаем.

## Verification (user)
1. Unity → Play Mode → BootstrapScene → полёт кораблём сквозь слой облаков.
2. Ожидание: облака расходятся **позади корабля** конусом (расширяется кзади),
   а не разрыв над кораблём.
3. Console: нет `[LocalDensityBuffer] Splat queue full` (было бы при лимите 16).
4. B&W debug (DebugDensityDirect): след-конус за кораблём в Pass 0.
