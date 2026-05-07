# Cloud Generator 3D Visualizer

3D-визуализатор облаков для проекта Project C (TheGravity).

## Что это

Веб-визуализатор, который процедурно генерирует облака по заданным параметрам с помощью Three.js. Параметры можно менять в реальном времени и экспортировать результат в C# код для интеграции в Unity-проект.

## Быстрый старт

1. Открыть `web/visualizer3d.html` в браузере
2. Нажать **+ Add Layer** для добавления слоя
3. Кликнуть на слой для раскрытия параметров
4. Настроить архетип и параметры
5. **Generate** — пересобрать облако
6. **Export** — экспортировать C# код

## Управление

**Десктоп:**
- Зажать и двигать мышью — вращение камеры
- Колёсико — зум

**Мобильные:**
- Свайп (1 палец) — вращение камеры
- Пинч (2 пальца) — зум
- Двойной тап — сброс камеры
- Кнопка Parameters — свернуть/развернуть панель параметров

## Архетипы слоёв

| Архетип | Описание | Параметры |
|---------|----------|-----------|
| **Sphere** | Каскадная сфера — равномерное обрастание | Cloud Size, Parent Count, Ellipsoid Y/XZ, Cascade Depth, Bumps/Level, Child Ratio, Size Variation, **Size Min/Max** |
| **Column** | Вертикальная башня — этажи сфер | Height, Base/Top Radius, Floors, Rings/Floor, Wobble, **Size Min/Max** |
| **Platform** | Плоский диск — XZ распространение | Width, Depth, Center Thickness, Interior Density, **Size Min/Max** |
| **Tree** | Древовидная структура — ветвление | Max Depth, Branch Elongation, Angle, Probability, Trunk Up Bias, **Size Min/Max** |

**Общие параметры для всех:** Seed, Density, Jitter, Clustering, Y Offset, Size Min/Max

## Структура слоёв

Слои рендерятся снизу вверх. Y Offset задаёт смещение каждого слоя.

```
┌─────────────────────────────┐
│     Layer 3: Platform        │  ← Y Offset: 120
├─────────────────────────────┤
│     Layer 2: Column          │  ← Y Offset: 50
├─────────────────────────────┤
│     Layer 1: Sphere          │  ← Y Offset: 0
└─────────────────────────────┘
```

## Технологии

- Three.js r128 (CDN)
- Pure JavaScript (FBM + Perlin noise + Worley)
- Single HTML file, no build step

## Файлы

- `web/visualizer3d.html` — основной файл визуализатора
- `ALGORITHM.md` — документация алгоритма v4
- `ARCHITECTURE.md` — документация архитектуры v5
- `FIXES.md` — история исправлений

## История изменений

### v5.4 — Size Controls (Size Min/Max, Jitter, Clustering)
- **Size Min / Size Max** — унифицированная система контроля размера сфер для всех архетипов
  - Параметр `sizeRange: { min, max }` добавлен в `getDefaultLayer()` для всех архетипов
  - UI: слайдеры Size Min / Size Max в панели каждого архетипа
  - Column: `baseRadius * sizeBase * 0.08` — напрямую управляет радиусом сфер
  - Platform: `sizeBase * (0.3 + 0.7*(1-dist*0.8)) * jitterFactor`
  - Sphere: `sizeBase * sizeMult` где `sizeBase = minRadius + noise * (sizeMax - minRadius)`
  - Tree: `sizeBase * taperRatio * thicknessFalloff` для детей, `sizeRange.min` для корня
- **Исправлен `updateLayerField`** — переписан для поддержки любой вложенности через точку (`sizeRange.min`, `columnParams.height` и т.д.)
- **`updateLayerArchetype`** — сохранение `sizeRange` при смене архетипа
- **`generate()`** — добавлен try-catch с alert для видимости ошибок
- **Jitter** усилен во всех архетипах (множитель ×2 на position offsets)
- **Tree jitter** — применяется к углам веток (`lateralAngle`) и позиции детей
- **Clustering** — используется в Platform для jitterFactor и density calculation

### v5.3 — Size Controls Fix
- Возврат к версии 5.3 как baseline
- Начальное исправление багов с size controls

### v5.2 — Flat Bottom, Clone, Drag & Drop, Undo/Redo
- **Flat bottom profile** — Condensation Level: сферы ниже заданного Y cutoff удаляются (полезно для кучевых облаков с плоским низом)
- **Clone/copy layer** — кнопка &#10697; рядом с X клонирует слой со всеми параметрами (+10 к Y offset)
- **Drag & drop reorder** — перетаскивание слоёв за &#9776; меняет порядок в стеке
- **Undo/redo** — история до 20 шагов, кнопки &#8630; / &#8631; в панели слоёв
- **Condensation control** — слайдер в секции Chaos каждого слоя

### v5.1 — UI Layer Editor
- **Убран старый UI** — табы Shape/Cascade/Chaos удалены
- **Layer-based UI** — каждый слой имеет свои настройки
- **Expandable layers** — клик на слой раскрывает параметры
- **Archetype selector** — выбор типа слоя в dropdown
- **Auto-expand first layer** — первый добавленный слой сразу раскрыт
- **Fixed mobile layout** — исправлены баги с flex и overflow
- **Multi-layer C# export** — экспорт поддерживает все слои

### v5.0 — Multi-Layer Architecture
- **Archetype dispatcher** — separate generators per archetype: sphere, column, platform, tree
- **Layer stacking** — multiple layers stacked vertically from bottom to top
- **Column archetype** — vertical stacking of floor rings (cumulonimbus tower)
- **Platform archetype** — flat horizontal disk with XZ density
- **Tree archetype** — directional branching with trunk and lateral limbs
- Architecture documented in `ARCHITECTURE.md`

### v4.0 — Cascade all fixes applied
- **Per-child size noise** — siblings at same level have different sizes (Fix 1)
- **Phi-modulation** — child ratio uses golden ratio, no 45% cap (Fix 2)
- **Parent count + ellipsoidXYZ** — multiple parents, shape control (Fix 3)
- **Jitter + Worley clustering + variable bumps** — no more virus pattern (Fix 4)

### v3.0 — Cascade (hierarchical sphere placement)
- **Fibonacci sphere sampling** — uniformly distributed points on sphere surface
- **Noise-driven bump generation** — FBM + Worley determine bump size/existence
- **Recursive cascade** — bumps on parent surface → sub-bumps on children
