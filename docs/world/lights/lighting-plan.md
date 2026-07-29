# Lighting Plan — Project C: World Illumination

> **Дата анализа**: 2025-07-16  
> **Версия**: 1.0  
> **Рендер-пайплайн**: URP 17.5.0, Forward, HDR off, MSAA off

---

## Текущее состояние

### Источники света
| Объект | Тип | Режим | Интенсивность | Цвет | Тени |
|---|---|---|---|---|---|
| `Sun` | Directional | Realtime | 0.15 × фаза | phase-driven | 2048px, 2 cascades |
| `Moon` | Directional | Realtime | 1.0 × фаза | ~(0.68, 0.85, 0.84) | 2048px, 2 cascades |

### Система день/ночь
- **DayNightController** с 5 фазами: Morning, Midday, Evening, Twilight, Night
- 3 Volume Profile (Day/Night/Twilight): Bloom + Vignette + ColorAdjustments
- Смешивание Skybox_Day ↔ Skybox_Night
- Fog + TemperatureFilter

### Визуальный стиль
Cel-shaded / Ghibli-стилизация: CloudGhibli-шейдеры, Borderlands-style EdgeDetection, VeilRaymarch glow-layer.

### Что отсутствует (критические пробелы)
- ❌ Нет Light Probes / Light Probe Groups
- ❌ Нет Reflection Probes
- ❌ Нет локальных источников света (point/spot)
- ❌ Нет LightingSettings (невозможно запекать GI)
- ❌ Additional Lights = Per Vertex (низкое качество для локальных источников)
- ❌ Нет emissive-материалов для окон/деталей

---

## План реализации (приоритеты P1–P3)

### 🔴 P1 — Критические системные изменения

#### 1. Переключить Additional Lights → Per Pixel

**Файл**: `Assets/_Project/Settings/ProjectC_URP.asset`

**Текущее**: `m_AdditionalLightsRenderingMode = Per Vertex`

**Нужно**: `Per Pixel`

**Почему**: Per Vertex даёт освещение только в вершинах — на крупных поверхностях (корабли, платформы) локальные источники будут выглядеть как «кляксы». Per Pixel даст нормальное затенение.

**Риски**: незначительное падение производительности. При 4-8 point lights на сцену — незаметно.

---

#### 2. Создать LightingSettings и запечь Indirect GI для статической геометрии

**Шаги**:
1. Создать `Assets/_Project/Settings/LightingSettings_World.asset` (Window → Rendering → Lighting → New Lighting Settings)
2. Настроить:
   - **Mixed Lighting**: `Baked Indirect`
   - **Lightmapper**: Progressive GPU
   - **Lightmap Resolution**: 20-30 texels/unit (низкое, т.к. стилизация)
   - **Compress Lightmaps**: On
   - **Ambient Occlusion**: Off (стилизация не требует)
3. Пометить статические острова как `Static` (флаг `Contribute GI`)
4. Запечь GI для каждой WorldScene_X_Y

**Объём**: только статическая геометрия островов/ландшафта.  
**Исключено**: облака, корабли, pickup-ы, NPC — они динамические.

**Ожидаемый результат**:
- Мягкие indirect-отскоки от поверхности островов
- Корабль, подлетая к острову, получает ambient цвет от его поверхности
- Визуальная связность сцены (острова не выглядят «оторванными»)

---

#### 3. Расставить Light Probe Groups

**Шаги**:
1. Создать GameObject → Light → Light Probe Group
2. Разместить в ключевых точках каждого WorldScene_X_Y:
   - Центр каждого острова/платформы
   - Док-станции
   - Входы в пещеры
   - Ключевые NPC-локации
   - По вертикали: на высоте полёта корабля (500–3000m)
3. Редактировать позиции проб через Edit Probes в инспекторе

**Количество**: ~10-20 Light Probe Group на всю карту (по 1-2 на WorldScene)

**Ожидаемый результат**:
- Динамические объекты (корабли, персонажи) получают корректное ambient-освещение
- При смене дня/ночи пробы автоматически обновляются (Baked Indirect зависит от directional light)

---

### 🟡 P2 — Локальные источники и отражения

#### 4. Добавить локальные Point/Spot Lights

| Локация | Тип | Режим | Цвет | Радиус | Интенсивность |
|---|---|---|---|---|---|
| DockStation_Primium | Point | Mixed | Тёплый оранж (#FFB070) | 15m | 2.0 |
| DockStation_TestZone | Point | Mixed | Тёплый оранж | 15m | 2.0 |
| Пещера | Point × 3 | Mixed | Холодный синий (#8090FF) | 8m | 1.5 |
| Вход в пещеру | Spot | Mixed | Тёплый | 20m / 30° | 3.0 |
| Фермы (Primum_farms) | Point × 2 | Mixed | Тёплый жёлтый (#FFD080) | 12m | 1.5 |
| Крафт-станция (CraftingStation_Table) | Point | Mixed | Нейтральный белый | 6m | 1.0 |
| Корабль игрока (Ship_Light_root) | Point × 2 | Realtime | Тёплый | 8m | 2.0 |
| Сундуки (Chest_North, Chest_East) | Point | Realtime | Слабый золотой | 3m | 0.5 |

**Mixed-режим** для статических локаций (даёт baked indirect + realtime direct).  
**Realtime** для корабля и pickup-ов (двигаются).

**Важно**: после переключения Additional Lights на Per Pixel (P1.1) — проверить визуальное качество.

---

#### 5. Добавить Reflection Probes

| Проба | Позиция | Тип | Размер |
|---|---|---|---|
| SkyProbe | Центр мира, высота 5000m | Realtime (Every Frame) | 50000×50000×10000 |
| Island_Primum | Центр Primum | Baked | 500×500×300 |
| Island_Secund | Центр Secund | Baked | 500×500×300 |
| CaveProbe | Центр пещеры | Baked | 200×200×100 |

**Настройки URP**: включить `m_ReflectionProbeBlending = true`, `m_ReflectionProbeBoxProjection = true`

**Ожидаемый результат**: металлические/глянцевые поверхности кораблей и предметов получают отражения окружения вместо «чёрной дыры».

---

### 🟢 P3 — Атмосферные детали (без дополнительных Light-объектов)

#### 6. Emissive материалы

Добавить emissive-канал на существующие материалы:

| Объект | Эффект |
|---|---|
| Окна зданий (Primum/Secund/Tertius) | Слабое свечение тёплым/холодным |
| Кристаллы (Pickup_TimeCrystal) | Пульсирующее emissive-свечение |
| Панели на док-станциях | Слабое индикаторное свечение |
| Руны/магические объекты | Синее emissive-свечение |

**Почему не Point Lights**: emissive даёт атмосферу без нагрузки на lighting-систему. Для стилизованного рендера — идеально.

---

#### 7. Настройка Volume Profiles (DayVolume / NightVolume / TwilightVolume)

**Текущее**: все 3 профиля содержат Bloom + Vignette + ColorAdjustments.

**Предлагаемые правки**:

| Эффект | Day | Twilight | Night |
|---|---|---|---|
| **Bloom** | Threshold 0.9, Intensity 0.3 | Threshold 0.7, Intensity 0.5 | Threshold 0.6, Intensity 0.7 |
| **Vignette** | Intensity 0.2, черный | Intensity 0.3, черный | Intensity 0.4, тёмно-синий |
| **ColorAdjustments** | Saturation +5, Exposure 0 | Saturation -10, Exposure -0.5 | Saturation -20, Exposure -0.8 |
| **LiftGammaGain** | — | Gain: лёгкий оранж | Gain: синий, Lift: тёмно-синий |
| **ShadowsMidtonesHighlights** | — | — | Shadows: синий оттенок |

**Ожидаемый результат**: более выраженные переходы между фазами, ночная сцена не просто «тёмная», а с холодным синим оттенком.

---

## Очерёдность исполнения

```
P1.1 ──► P1.2 ──► P1.3 ──► P2.4 ──► P2.5 ──► P3.6 ──► P3.7
(20m)    (1h)     (1h)     (2h)     (30m)    (1h)     (30m)
```

Общая оценка: **~6 часов**

---

## Что НЕ делаем (out of scope)

- Не трогаем Sun intensity / DayNightController логику
- Не добавляем Area Lights (URP 17.5 не поддерживает эффективно)
- Не переходим на Probe Volumes (избыточно для cel-shaded стиля)
- Не включаем HDR (сломает текущий тонемаппинг и пост-эффекты)
- Не меняем shadow resolution (2 cascades × 2048px — достаточно)
