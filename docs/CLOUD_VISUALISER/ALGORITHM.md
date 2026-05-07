# Cloud Generator v4.0 — Cascade with All Fixes

## Концепция

Не заполнение объёма сферами, а **иерархическое дерево сфер** где каждая сфера — "шишка" на поверхности родителя. Финальный результат: cauliflower-like cloud structure.

```
Корневая сфера (-ы)
  └── Шишки на её поверхности (child spheres) ← РАЗНЫЕ размеры благодаря per-child noise
        └── Шишки на поверхности каждой шишки (grandchild spheres) ← фи-подобное масштабирование
              └── ...каскад с jitter и clustering
```

## Ключевые отличия от v3.0

| v3.0 | v4.0 |
|------|------|
| Все дети на уровне N одинаковые | Каждый ребёнок получает уникальный noise → разный размер |
| Ratio залочен на 45% | Ratio до 200%, φ-модуляция |
| 1 родительская сфера | parentCount (1-12) + ellipsoidXYZ |
| Fibonacci даёт "вирус"-паттерн | Jitter + Worley clustering + variable bumps |

## Алгоритм

### Шаг 1 — Родительские сферы
- `parentCount` сфер в центре
- Каждая масштабируется через `ellipsoidX/Y/Z`
- Для cumulus: `ellipsoidY=0.50` (плоское дно), `ellipsoidXZ=1.20` (широкое)

### Шаг 2 — Fibonacci ellipsoid surface sampling
Для каждой родительской сферы семплируем точки на её поверхности (включая ellipsoid scaling):
```javascript
const surfacePoints = fibonacciSphere(cx, cy, cz, rx, ry, rz, numPoints);
```

### Шаг 3 — Jitter (chaos source #1)
Каждая surface point получает случайное смещение через noise → разрушает идеальную регулярность:
```javascript
jitX = perlin3D(pt.x*15, seed) × jitterStrength
jitteredPt = { x: pt.x + jitX×radius, ... }
```

### Шаг 4 — Worley clustering (chaos source #2)
Worley noise определяет КЛАСТЕРЫ — внутри кластера плотность высокая, между кластерами — sparse:
```javascript
worleyVal = invertedWorley(jitteredPt × freq);
if (worleyVal > threshold) createBump(); // dense cluster
else if (worleyVal > 0.75) createBump() with 15% chance; // sparse between
```

### Шаг 5 — Bump size (Fix 1: per-child size variation)
Каждая дочерняя сфера получает собственный noise размера:
```javascript
sizeNoise = perlin3D(childPos × freq, seed + uniqueId); // независимый noise
sizeMult = 0.2 + sizeNoise × sizeVariation + effectiveRatio;
childRadius = parent.radius × sizeMult;
// Sibling-сферы на одном уровне имеют РАЗНЫЕ размеры
```

### Шаг 6 — Phi-ratio scaling (Fix 2)
```javascript
phiNoise = perlin3D(childPos × freq, seed + 100); // возмущение φ
effectiveRatio = (PHI + (phiNoise - 0.5) × 0.6) × (childRatio / 100) × 0.4;
// Некоторые дети будут больше родителя (следуя φ-прогрессии)
```

### Шаг 7 — Variable bumps (chaos source #3)
Каждая родительская сфера получает РАЗНОЕ количество точек для семплирования:
```javascript
actualBumps = bumpsPerLevel × (0.4 + noiseForThisSphere × 0.8);
// Некоторые сферы получат 20 шишек, другие 80
```

### Шаг 8 — Рекурсия с probabilistic termination
```javascript
continueChance = max(0.15, 0.8 - depth × 0.2);
if (random < continueChance || depth < 2) generateBumpsOnSphere(child);
```

## Параметры

### Cloud Shape
| Параметр | Что делает | Диапазон |
|----------|-------------|----------|
| Cloud Size | Размер облака | 20–200 |
| Parent Count | Кол-во родительских сфер | 1–12 |
| Ellipsoid Y | Вертикальное масштабирование (меньше = плоское дно) | 20–150 (×0.01) |
| Ellipsoid XZ | Горизонтальное масштабирование | 50–150 (×0.01) |

### Cascade
| Параметр | Что делает | Диапазон |
|----------|-------------|----------|
| Cascade Depth | Глубина каскада | 1–5 |
| Bumps per Level | Базовое кол-во шишек на сферу | 12–128 |
| Child Ratio | Базовое соотношение размера ребёнка к родителю | 10–200% |
| Size Variation | Насколько siblings различаются по размеру | 0.0–1.0 |

### Chaos
| Параметр | Что делает | Диапазон |
|----------|-------------|----------|
| Jitter | Смещение точек семплирования на поверхности | 0–50 (×0.01) |
| Clustering | Worley clustering — создаёт кластеры шишек | 0–100% |
| Density | Порог noise — какая доля точек становится шишками | 5–95% |

## Что получилось vs v3.0

**Было (v3.0):**
- Все дочки 2го уровня одинаковые (линейный cascade)
- Ratio залочен на 45%
- 1 сфера-родитель, форма не контролируется
- Легко получить "вирус" — математически правильный Fibonacci pattern

**Стало (v4.0):**
- Per-child size noise → siblings разные
- Ratio до 200% + φ-модуляция
- parentCount (1-12) + ellipsoidXYZ shape control
- Jitter + clustering + variable bumps → organic cauliflower look

## Математика размеров

```
Depth 0:  N_parent spheres (radius = baseRadius)
Depth 1:  ~N_parent × actualBumps × density = X spheres
          each: parent.radius × (0.2 + perChildNoise × SV + φ × ratio × 0.4)
Depth 2:  X × actualBumps × density = ~Y spheres
          each: depth1.radius × (same formula)
...
```

## C#-экспорт

```csharp
List<CloudSphere> spheres = GenerateCascadeCloud(
    cloudSize, density, seed,
    cascadeDepth, bumpsPerLevel, childRatio, sizeVariation,
    jitter, clustering,
    parentCount, ellipsoidY, ellipsoidXZ
);
```

## Known issues / TODO

- [ ] Flat bottom profile (condensation level) — пока не реализовано
- [ ] Storm mode (cumulonimbus: column + anvil) — нужно добавить
- [ ] Wind animation (curl noise для анимации) — для будущего