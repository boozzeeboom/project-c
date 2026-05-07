# Cloud Generator 3D Visualizer

3D-визуализатор облаков для проекта Project C (TheGravity).

## Что это

Веб-визуализатор, который процедурно генерирует облака по заданным параметрам с помощью Three.js. Параметры можно менять в реальном времени и экспортировать результат в C# код для интеграции в Unity-проект.

## Параметры

| Параметр | Описание | Диапазон |
|----------|----------|----------|
| Cloud Size | Размер области генерации | 20–200 |
| Density | Плотность облаков | 0.00–0.95 |
| Turbulence | Турбулентность формы | 0.00–1.00 |
| Humidity | Влажность (влияет на размер сфер) | 0.10–1.00 |
| Storm | Интенсивность шторма | 0.00–1.00 |
| Seed | Сид генерации | 0–999 |

## Управление

**Десктоп:**
- Зажать и двигать мышью — вращение камеры
- Колёсико — зум

**Мобильные:**
- Свайп (1 палец) — вращение камеры
- Пинч (2 пальца) — зум
- Двойной тап — сброс камеры
- Кнопка Parameters — свернуть/развернуть панель параметров

## Технологии

- Three.js r128 (CDN)
- Pure JavaScript (FBM + Perlin noise)
- Single HTML file, no build step

## Файлы

- `web/visualizer3d.html` — основной файл визуализатора

## История изменений

### v4.0 — Cascade all fixes applied
- **Per-child size noise** — siblings at same level have different sizes (Fix 1)
- **Phi-modulation** — child ratio uses golden ratio, no 45% cap (Fix 2)
- **Parent count + ellipsoidXYZ** — multiple parents, shape control (Fix 3)
- **Jitter + Worley clustering + variable bumps** — no more virus pattern (Fix 4)
- All fixes documented in `FIXES.md`, algorithm in `ALGORITHM.md`

### v3.0 — Cascade (hierarchical sphere placement)
- **Fibonacci sphere sampling** — uniformly distributed points on sphere surface
- **Noise-driven bump generation** — FBM + Worley determine bump size/existence
- **Recursive cascade** — bumps on parent surface → sub-bumps on children
- **NOT volume fill** — spheres are "bumps" on surface, not fill
- **Child/parent ratio** controls fluffiness (~25-40% = fluffy cauliflower)
- **Cascade Depth** — how many levels of recursive detail
- **Bumps per Level** — how many child spheres per parent (12-128)
- Math documented in `RESEARCH_CASCADE.md`