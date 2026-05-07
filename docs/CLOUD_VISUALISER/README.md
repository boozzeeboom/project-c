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
| **Sphere** | Каскадная сфера — равномерное обрастание | Cloud Size, Parent Count, Ellipsoid Y/XZ, Cascade Depth, Bumps/Level, Child Ratio, Size Variation |
| **Column** | Вертикальная башня — этажи сфер | Height, Base/Top Radius, Floors, Rings/Floor, Wobble |
| **Platform** | Плоский диск — XZ распространение | Width, Depth, Center Thickness, Interior Density |
| **Tree** | Древовидная структура — ветвление | Max Depth, Branch Elongation, Angle, Probability, Trunk Up Bias |

**Общие параметры для всех:** Seed, Density, Jitter, Clustering, Y Offset

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
