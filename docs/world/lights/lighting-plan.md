# Lighting Plan — Project C: World Illumination

> **Дата анализа**: 2026-09-08
> **Версия**: 1.1 — утверждённый рабочий план
> **Рендер-пайплайн**: URP 17.5.0, Forward; HDR отключён в `ProjectC_URP.asset`

## Статус утверждения — 2026-09-08

**Решение:** план утверждён как рабочий, но реализация выполняется через пилотные сцены и контрольные ворота. Массовый bake всех 24 `WorldScene_X_Y` до визуальной приёмки пилота запрещён.

### Подтверждено в проекте

- Проект использует Unity `6000.5.2f1` и URP `17.5.0` (`ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`).
- В проекте присутствуют 24 сцены `Assets/_Project/Scenes/World/WorldScene_X_Y.unity`.
- `Assets/_Project/Settings/ProjectC_URP.asset` имеет `m_AdditionalLightsRenderingMode: 2`, что соответствует `Per Vertex`, лимит дополнительных источников — `4` на объект.
- В URP Asset отключены `m_ReflectionProbeBlending` и `m_ReflectionProbeBoxProjection`.
- В WorldScene-файлах `m_LightingSettings: {fileID: 0}`; определения `LightProbeGroup` и `ReflectionProbe` в просмотренном наборе сцен не обнаружены.
- Три профиля Day/Night/Twilight существуют: `DayVolumeProfile.asset`, `NightVolumeProfile.asset`, `TwilightVolumeProfile.asset`.

### Обязательные корректировки исходного текста

1. **P1.1 остаётся первым изменением:** `Additional Lights` переводятся с `Per Vertex` на `Per Pixel`; после изменения нужен отдельный визуальный и GPU-baseline.
2. **P1.2 больше не обещает автоматическое обновление baked GI при смене дня/ночи.** Baked lighting не является runtime-реактивным; для текущего phase-driven Sun/Moon bake рассматривается только после подтверждения совместимости с системой Day/Night.
3. **P1.2 выполняется сначала только как пилот `WorldScene_0_0`.** Полный bake 24 сцен — отдельное решение после приёмки пилота.
4. **P1.3 зависит от результата пилота P1.2.** Light Probe Groups расставляются после появления валидных lighting data и проверяются на корабле/NPC в тестовой сцене.
5. **P2.5 выполняется после P1.1 и после инвентаризации металлических материалов.** Reflection Probe создаётся сначала в пилотной зоне; глобальная проба `50000×50000×10000` не принимается без профилирования памяти и визуальной проверки.
6. **P3.6 и P3.7 идут после baseline.** Emissive и Volume-настройки не смешиваются с системными изменениями в одном коммите.

### Утверждённая последовательность этапов

- **Stage 0 / T-LIGHT01:** аудит и утверждение плана, фиксация baseline и контрольных ворот.
- **Stage 1 / T-LIGHT02:** P1.1 — `Additional Lights: Per Pixel`.
- **Stage 2 / T-LIGHT03:** P1.2 — LightingSettings и пилотный bake только для `WorldScene_0_0` — инфраструктура создана, bake заблокирован результатом `0 lightmaps`.
- **Stage 3 / T-LIGHT04:** P1.3 — Light Probe Groups в пилотной сцене.
- **Stage 4 / T-LIGHT05:** P2.4 — локальные Point/Spot Lights в пилотных локациях — realtime-пилот фонарных столбов выполнен.
- **Stage 5 / T-LIGHT06:** P2.5 — Reflection Probes и включение blending/box projection.
- **Stage 6 / T-LIGHT07:** P3.6 — emissive-материалы.
- **Stage 7 / T-LIGHT08:** P3.7 — настройка Day/Twilight/Night Volume Profiles — ночная экспозиция скорректирована, полный tuning ещё не закрыт.

Каждый этап имеет отдельный коммит, документ результата и проверку `git diff --check`. Никакие несвязанные изменения рабочего дерева в этап не включаются.

### Контрольные ворота

- До P1.2: зафиксировать текущий визуальный baseline в `WorldScene_0_0`.
- После каждого изменения URP/Lighting: проверить отсутствие compile/import ошибок и сохранить сцену через Unity Editor.
- После P1.2–P1.3: проверить динамический корабль и NPC в световой зоне; baked data не считать доказательством runtime day/night.
- До перехода к P2: проверить стоимость дополнительных источников при лимите `4` lights per object.
- До финального утверждения P3: выполнить ручной Play Mode-прогон и скриншоты; автоматическая проверка без визуальной приёмки недостаточна.

### Выполнение Stage 1 — 2026-09-08

- **T-LIGHT02 завершён:** `Additional Lights` переведены с `Per Vertex` на `Per Pixel` в `Assets/_Project/Settings/ProjectC_URP.asset` через Unity MCP.
- Проверка `pipeline_get_settings` подтвердила `m_AdditionalLightsRenderingMode: PerPixel`.
- Проверка компиляции: `No compile errors`.
- В Unity Console ошибок нет; присутствуют только ранее существовавшие предупреждения/сообщения, не связанные с изменением URP Asset.

### Выполнение Stage 2 — 2026-09-08

- Создан `Assets/_Project/Settings/LightingSettings_World.asset`.
- Для `WorldScene_0_0` назначены: Progressive GPU, Baked GI, Realtime GI off, Mixed Bake Mode `IndirectOnly`, 25 texels/unit, AO off, 2 bounces.
- Пилотный bake запущен и проверен.
- Результат: `lightmapCount: 0`; bake не считается валидным.
- Причина по live-инвентаризации: `WorldScene_0_0` содержит 0 источников света; Bootstrap содержит только `Sun` и `Moon`, оба `Realtime`, и не содержит статической геометрии для GI.
- В `WorldScene_0_0` найдено 7413 mesh renderers, но ни один renderer не имел собственного флага `Contribute GI`.
- Generated lighting data очищены; пустой bake не сохраняется.

### Текущий статус выполнения

**Stage 2 / T-LIGHT03 заблокирован после создания инфраструктуры.** Нельзя переходить к Light Probe Groups или массовому bake, пока не принято решение по источникам GI и корректной маркировке статической геометрии. Sun/Moon и DayNightController не изменялись.

### Выполнение Stage 4 / T-LIGHT05 — 2026-09-08

- В `WorldScene_0_0` найдены 11 объектов `MD2_Lamp_01_Housing`–`MD2_Lamp_11_Housing` внутри `gorod port_3_3_unity_1`.
- В каждый housing добавлен дочерний `*_PointLight`.
- Параметры: Point, Realtime, цвет `#FFB070`, intensity `2.0`, range `15 m`, shadows off.
- Все 11 источников включены и сохранены в сцене.
- Это независимый realtime-пилот; bake и Light Probe Groups для него не требуются.
- Визуальная приёмка выполняется ручным Play Mode-прогоном пользователя.

### Коррекция ночного Volume — 2026-09-08

- В `NightVolumeProfile.asset` обнаружена экстремальная экспозиция `postExposure = -7`.
- Это значение значительно подавляло результат realtime Point Lights ночью.
- Значение исправлено на `-0.8`, соответствующее утверждённому направлению плана.
- Синий `colorFilter`, контраст, saturation, Bloom и temperature overlay пока не изменялись.
- Для применения runtime-копии профиля требуется новый запуск Play Mode.

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
Cel-shaded / comic-book стилизация: существующие cloud shaders, Borderlands-style EdgeDetection, мягкие градиенты и VeilRaymarch glow-layer.

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
