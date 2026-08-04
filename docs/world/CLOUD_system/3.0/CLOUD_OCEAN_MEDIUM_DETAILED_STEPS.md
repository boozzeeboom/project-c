# CLOUD_system 3.0 — Detailed Implementation Steps

**На основе:** `CLOUD_OCEAN_MEDIUM_IMPLEMENTATION_PLAN.md`


---

## Фаза 1 — Визуальное ядро (задачи 1.1–1.7)

---

### 1.1 — HLSL-порт CloudMath.cs → `CloudNoise.hlsl`

**Путь:** `Assets/_Project/Shaders/Clouds/CloudNoise.hlsl`

**Вход (ВАЖНО):** `Assets/CloudGenerator/CloudGenerator_v7.0/CloudGenerator_v7.0/CloudMath.cs` — double/hash-seeded вариант.
⚠️ НЕ `src/CloudMath.cs` — это старый float/perm-table вариант (фиксированный seed 1337, функции без seed/freq
параметров). Подписи ниже есть только в v7.0. (Общий план §3.2 ссылается на `src/CloudMath.cs` — поправить там.)

- `Hash3(int,int,int,int)` → `uint Hash3(uint3, uint)` — **порт на uint-арифметику**: C# использует `long` (64-бит) + double-mod; в HLSL 64-бит недоступен → multiply-shift uint-хэш (напр. `u = x*374761393u ^ y*668265263u ^ z*2147483647u; u = (u ^ (u>>13)) * 1274126177u`). Сохранить семантику переполнения int32 как в C# unchecked (HLSL int оборачивается так же)
- `Fade3(double)` → `float Fade3(float)`
- `Grad3(int,double,double,double)` → `float Grad3(uint,float3)`
- `Perlin3D(double,double,double,int)` → `float Perlin3D(float3, uint seed)`
- `Fbm(...)` → `float Fbm(float3, int octaves, float persistence, float lacunarity, uint seed)`
- `Worley3D(double,double,double,double,int)` → `float Worley3D(float3 p, float freq, uint seed)` — **2 версии**: low-freq + high-freq (разные freq: 4 и 16)
- `InvertedWorley(...)` → `float InvertedWorley(float3, float, uint)`

**Точность:** v7.0 — double, HLSL — float32 → **бит-точного совпадения с C# не будет и не нужно**. Приёмка — статистическая (см. ниже), не покадровая.

**Создать:**
1. `Assets/_Project/Shaders/Clouds/CloudNoise.hlsl` — include-файл со всеми функциями (float32, uint-хэш)
2. `Assets/_Project/Shaders/Clouds/CloudCommon.hlsl` — общие хелперы (remap, height profile, фазовые функции)

**Приёмка:** статистическое сравнение C# v7.0 vs HLSL через две bake-текстуры (см. 1.2):
mean abs error < 1e-2 по срезам; точного совпадения не ждать (double→float).

---

### 1.2 — Бейк 3D Worley-текстуры

**Создать:**
1. `Assets/_Project/Shaders/Clouds/BakeCloudNoise.compute` — compute shader, который:
   - Диспатчит 128³ или 256³ тредов
   - Записывает в `RWTexture3D<float4>`:
     - R = Perlin FBM (base mass)
     - G = Worley low-freq (freq=4)
     - B = Worley high-freq (freq=16)  
     - A = Inverted Worley (erosion)
   - **Remap в [0,1] перед записью в UNORM** (Perlin возвращает [-1,1]): `channel * 0.5 + 0.5`
   - **Бесшовность — периодическим хэшем, НЕ fract:** `fract()` при сэмплинге даёт повторение, но НЕ бесшовность — непериодический шум даёт швы на границах тайла. Внутри noise-функций применять `mod(cellIndex, texSize)` к индексам ячеек хэша → шум становится периодическим с периодом texSize. Приёмка: срезы на границе тайла совпадают (первый и последний слой/строка идентичны)
2. `Assets/_Project/Scripts/World/Clouds/CloudNoiseBaker.cs` — Editor-скрипт (namespace `ProjectC.World.Clouds`):
   - `[MenuItem("ProjectC/Clouds/Bake 3D Noise Texture")]`
   - Создаёт `RenderTexture.descriptor` 128³ **RGBA8 UNORM** (совпадает с общим планом §3.2; RGBAHalf — только если позже появится бандинг; 256³ RGBA8 = 64 МБ — не увлекаться)
   - Диспатчит compute shader
   - **Readback GPU→CPU:** `AsyncGPUReadback` (или `RenderTexture.active` + `ReadPixels` послойно) → `Texture3D` 128³
   - `AssetDatabase.CreateAsset(texture3D, "Assets/_Project/Data/Clouds/CloudNoise3D.asset")`
   - Настройки импорта: `wrapMode = Repeat`, `filterMode = Trilinear`
   - (Опционально, для приёмки 1.1) второй MenuItem: bake той же текстуры из C# v7.0 `CloudMath` → сравнение срезов, mean abs error

**Приёмка:** сгенерированная `CloudNoise3D.asset` без швов при тайлинге (сравнить срезы на противоположных гранях тайла; проверить просмотром срезов).

---

### 1.3 — `VolumetricCloudsRenderFeature` + `VolumetricClouds.shader` (скелет)

**Создать:**
1. `Assets/_Project/Scripts/Rendering/VolumetricCloudsRenderFeature.cs` (namespace `ProjectC.Rendering`)
   - Копирует паттерн `EdgeDetectionRenderFeature.cs` (он лежит в `Scripts/Core/`, но новый subsystem — в `Scripts/Rendering/`; namespace тот же)
   - `RenderPassEvent.AfterRenderingOpaques` (или `BeforeRenderingTransparents` — уточнить)
   - Fullscreen triangle pass (как EdgeDetection: `SV_VertexID` + `DrawProcedural` / `GetFullScreenTriangleVertexPosition`)
   - Параметры в инспекторе:
     - `CloudNoise3D` (Texture3D reference)
     - `_CloudBottomY` / `_CloudTopY` (float)
     - `_RaymarchSteps` (int, 32–64)
     - `_MaxRayDistance` (float, 5000)
     - `_DensityMultiplier` (float)
     - `_WindOffset` (Vector3) — читается из `WindManager.Instance.CurrentWindDirection` (⚠️ null-guard: в сценах без WindManager не падать; можно подписаться на `WindManager.OnWindUpdated`)
     - Ghibli-рампы: `_DayRampTop/Mid/Bot`, `_SunsetRampTop/Mid/Bot` (Color)

2. `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader`
   - `Shader "Hidden/ProjectC/VolumetricClouds"`
   - Fullscreen pass (как EdgeDetection.shader: `SV_VertexID`, `Cull Off / ZWrite Off / ZTest Always`)
   - `#include "CloudNoise.hlsl"` + `#include "CloudCommon.hlsl"`
   - На этом этапе: **только плотность (ч/б)**, без освещения
   - Реконструкция луча: `UNITY_MATRIX_I_P` + `UNITY_MATRIX_I_V` + `_WorldSpaceCameraPos` (паттерн из VeilRaymarch.shader, строки 245–257)
   - **Slab intersection (обязательно):** пересечь луч с горизонтальными плоскостями Y=CloudBottomY/CloudTopY → tMin/tMax (паттерн VeilRaymarch.shader, строки 263–296). Без этого каждый пиксель маршит полные `_MaxRayDistance`
   - Функция `density(p)` = shapeNoise × heightProfile × windScroll
   - Early-exit: при `transmittance < 0.01`

**Подключение:**
- Добавить `VolumetricCloudsRenderFeature` в `ProjectC_URP_Renderer.asset` через Inspector (рядом с EdgeDetectionRenderFeature — там уже добавлен, паттерн проверен)

**Приёмка:** 0 ошибок компиляции, ч/б плотность видна в Game View на высоте 800–2000.

---

### 1.4 — Реймарч с height profile + coverage + wind

**Доработать:** `VolumetricClouds.shader`

```
density(p) = shapeFBM(p)          // Perlin+Worley из CloudNoise3D
           × heightProfile(p.y)    // Градиент: 0 у CloudBottomY, пик в середине, 0 у CloudTopY
           × coverageMap(p.xz)    // Процедурный или из текстуры (на старте: константа 1)
           + windScroll(t)         // UV-оффсет от WindManager
```

- `heightProfile(y)`: `smoothstep(bottom, bottom+gradient, y) * (1 - smoothstep(top-gradient, top, y))` с пиком ~0.3 по диапазону
- Wind: `_WindOffset` обновляется каждый кадр из C# → `cmd.SetGlobalVector`
- Coverage: на старте — `float coverageMap(float2 xz) { return 1.0; }` (равномерное покрытие)

**Приёмка:** слой облаков реалистичной формы на высоте 800–2000, движется по ветру.

---

### 1.5 — Light marching + HG + multi-scatter + Ghibli ramps

**Доработать:** `VolumetricClouds.shader`

Добавить в raymarch loop (после накопления плотности):

```hlsl
// Light marching: 4-6 шагов к солнцу
float lightTransmittance = 1.0;
for (int j = 0; j < LIGHT_STEPS; j++) {
    float3 lightPos = samplePos + sunDir * (j + 0.5) * lightStepSize;
    float lightDensity = Density(lightPos);
    lightTransmittance *= exp(-lightDensity * lightStepSize * lightAbsorption);
}

// HG phase function: Mie scattering
float hg = HG(rayDir, sunDir, 0.7); // g=0.7 forward scattering

// Multi-scatter approximation
float ms = pow(lightTransmittance, 0.5); // энергосберегающая аппроксимация

// Ghibli ramp (day/sunset)
float rampBlend = saturate(sunDir.y * 2.0); // 0=закат, 1=день
float3 cloudColor = lerp(sunsetRamp, dayRamp, rampBlend);
// Где ramp — градиент по высоте: top/mid/bot цвета

// Ambient + silver lining
float3 ambient = cloudColor * 0.15;
float silverLining = pow(1.0 - abs(dot(rayDir, sunDir)), 8.0) * 0.3;

float3 lighting = cloudColor * hg * ms * lightTransmittance + ambient + silverLining;
```

- `_SunDirection` — глобальный вектор из DayNight-системы или `RenderSettings.sun`
- GDD-14 рампы:
  - День: `#FFFFFF` (top) → `#D4E6F1` (mid) → `#A9CCE3` (bot)
  - Закат: `#FFFFFF` (top) → `#FFB6C1` (mid) → `#CD5C5C` (bot)

**Приёмка:** облака цветные, соответствуют GDD-14 дневным/закатным рампам. Солнечный свет виден как направленное освещение.

---

### 1.6 — Half-res + blue-noise дизеринг + temporal reprojection

**Доработать:** `VolumetricCloudsRenderFeature.cs` + `VolumetricClouds.shader`

1. **Half-res рендер:**
   - В RenderFeature создаётся `RT _CloudRT` половинного разрешения (`Screen.width/2, Screen.height/2`)
   - Основной raymarch-пас рендерит в `_CloudRT`
   - Второй пас (upsample + composite) блендит `_CloudRT` → `activeColorTexture`

2. **Blue-noise дизеринг:**
   - `Assets/_Project/Textures/BlueNoise64.png` — текстура 64×64 blue noise
   - В шейдере: `float dither = BlueNoise[i % 64][j % 64] - 0.5`
   - Смещение начальной точки луча: `tMin += dither * stepSize`
   - Убирает полосатость при малом количестве шагов

3. **Temporal reprojection:**
   - `_CloudHistoryRT` — предыдущий кадр
   - **Motion vectors: НЕ рассчитывать на встроенный URP pass** — в URP 17 camera motion vectors из коробки нет (проверить `MotionVectorRenderPass`, если есть — ок). Надёжный путь (стандарт для кастомного volumetrics):
     - В RenderFeature (C#) кэшировать `prevViewProj = currentViewProj` каждый кадр (`GL.GetGPUProjectionMatrix` + `camera.worldToCameraMatrix`)
     - Передавать `_PrevViewProj` uniform'ом в шейдер
     - Репроекция: `clipPosPrev = mul(_PrevViewProj, float4(worldPos, 1))` → `historyUV = clipPosPrev.xy / clipPosPrev.w * 0.5 + 0.5`
   - Blend: `lerp(current, history, 0.9)` (90% история, 10% новый)
   - Фоллбек при дисавкклюжене: проверка глубины history vs current (и clamp UV к экрану)

**Приёмка:** нет бандинга, нет мерцания при движении камеры. Качество сопоставимо с full-res но за полцены.

---

### 1.7 — Перф-замер

**Создать:**
1. `Assets/_Project/Scripts/World/Clouds/CloudPerfMonitor.cs` — компонент-диагностика:
   - `Profiler.BeginSample/EndSample` в RenderFeature
   - Вывод в Editor UI или оверлей: GPU мс на VolumetricClouds
   - Замер на 1080p: шаги 32/48/64, half-res on/off, temporal on/off

**Результат:** таблица в `docs/world/CLOUD_system/3.0/PERF_RESULTS.md`

| Конфигурация | 1080p mid-GPU | 1440p |
|---|---|---|
| 32 steps, half-res, temporal on | ? ms | ? ms |
| 48 steps, half-res, temporal on | ? ms | ? ms |
| 64 steps, full-res, temporal off | ? ms | ? ms |

**Цель:** ≤3 мс на 1080p mid-GPU (48 steps, half-res, temporal on).

---

## Структура файлов (что создать)

```
Assets/_Project/
├── Shaders/Clouds/
│   ├── CloudNoise.hlsl          # 1.1 — HLSL-порт CloudMath
│   ├── CloudCommon.hlsl         # 1.1 — хелперы
│   ├── VolumetricClouds.shader  # 1.3 — основной шейдер
│   └── BakeCloudNoise.compute   # 1.2 — бейк-компьют
├── Scripts/
│   ├── Rendering/
│   │   └── VolumetricCloudsRenderFeature.cs  # 1.3
│   └── World/Clouds/
│       ├── CloudNoiseBaker.cs    # 1.2
│       └── CloudPerfMonitor.cs   # 1.7
├── Data/Clouds/
│   └── CloudNoise3D.asset       # 1.2 — сгенерированная текстура
└── Textures/
    └── BlueNoise64.png          # 1.6 — текстура дизеринга
```

---

## Порядок выполнения

1. **1.1 CloudNoise.hlsl** — фундамент, всё остальное от него зависит
2. **1.2 BakeCloudNoise.compute + CloudNoiseBaker.cs** — проверяет корректность 1.1
3. **1.3 VolumetricCloudsRenderFeature.cs + VolumetricClouds.shader (скелет)** — первый видимый результат
4. **1.4 Height profile + wind** — форма и движение
5. **1.5 Light marching + Ghibli ramps** — цвет
6. **1.6 Half-res + dither + temporal** — качество
7. **1.7 Перф-замер** — валидация бюджета

---

## Риски и заметки

- **Источник шума:** порт идёт из `Assets/CloudGenerator/CloudGenerator_v7.0/.../CloudMath.cs`, НЕ из `src/CloudMath.cs` (в общем плане §3.2 ссылка на `src/` — поправить, иначе возьмут не тот файл).
- **URP Render Graph:** EdgeDetectionRenderFeature использует RenderGraph API (`RecordRenderGraph`). Нужно сохранить этот паттерн.
- **RenderPassEvent:** `AfterOpaques` vs `BeforeTransparents` — зависит от того, должны ли облака перекрывать полупрозрачные объекты. По умолчанию `AfterOpaques`.
- **Texture3D в URP:** `TEXTURE3D`/`SAMPLER3D` поддерживаются в URP 17 (DX11/Vulkan) — отдельная настройка не нужна. Проверить `#pragma target` в compute (Shader Model 5.0).
- **Float precision на больших координатах (новое):** сцены 80 000×80 000, world-координаты ломают float32 при сэмплинге шума далеко от начала координат. Сэмплить шум в **camera-relative пространстве**: `samplePos -= floor(cameraPos / tileSize) * tileSize` (шум тайлится → бесшовно). Обязательно для реймарча на высоте слоя вдали от origin.
- **Blue Noise:** если 64×64 текстура недоступна — сгенерировать через `CloudNoiseBaker` или взять из `Packages/com.unity.render-pipelines.core/Runtime/Textures/BlueNoise64`.
- **Temporal reprojection:** motion vectors — ручной кэш предыдущей VP-матрицы в C# (см. 1.6); встроенный URP camera motion vector pass не гарантирован.
- **GDD-14 рампы:** в GDD-14 только базовые цвета `#FFFFFF` (день) / `#FFB6C1` (закат). Тройки top/mid/bot в 1.5 — **предложение**, расширяющее GDD; подтвердить точные цвета у дизайнера перед Фазой 1.5.
- **WindManager:** в тестовых сценах может отсутствовать — RenderFeature должен null-guard'ить `WindManager.Instance` (или подписаться на `OnWindUpdated`).
