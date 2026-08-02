# CLOUD_system 3.0 — Detailed Implementation Steps

**На основе:** `CLOUD_OCEAN_MEDIUM_IMPLEMENTATION_PLAN.md`
**Дата:** 2026-08-02
**Статус:** 🟡 Ready for Implementation

---

## Фаза 1 — Визуальное ядро (задачи 1.1–1.7)

---

### 1.1 — HLSL-порт CloudMath.cs → `CloudNoise.hlsl`

**Путь:** `Assets/_Project/Shaders/Clouds/CloudNoise.hlsl`

**Вход:** `Assets/CloudGenerator/CloudGenerator_v7.0/CloudGenerator_v7.0/CloudMath.cs`
- `Hash3(int,int,int,int)` → `uint Hash3(uint3, uint)`
- `Fade3(double)` → `float Fade3(float)`
- `Grad3(int,double,double,double)` → `float Grad3(uint,float3)`
- `Perlin3D(double,double,double,int)` → `float Perlin3D(float3, uint seed)`
- `Fbm(...)` → `float Fbm(float3, int octaves, float persistence, float lacunarity, uint seed)`
- `Worley3D(double,double,double,double,int)` → `float Worley3D(float3 p, float freq, uint seed)` — **2 версии**: low-freq + high-freq (разные freq: 4 и 16)
- `InvertedWorley(...)` → `float InvertedWorley(float3, float, uint)`

**Создать:**
1. `Assets/_Project/Shaders/Clouds/CloudNoise.hlsl` — include-файл со всеми функциями
2. `Assets/_Project/Shaders/Clouds/CloudCommon.hlsl` — общие хелперы (remap, height profile, фазовые функции)

**Приёмка:** визуальное сравнение C# vs HLSL через bake-текстуру (см. 1.2).

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
   - Тайлинг через `fract(pos / size)` — бесшовный
2. `Assets/_Project/Scripts/World/Clouds/CloudNoiseBaker.cs` — Editor-скрипт:
   - `[MenuItem("ProjectC/Clouds/Bake 3D Noise Texture")]`
   - Создаёт `RenderTexture.descriptor` 128³ RGBAHalf
   - Диспатчит compute shader
   - `AssetDatabase.CreateAsset(texture3D, "Assets/_Project/Data/Clouds/CloudNoise3D.asset")`

**Приёмка:** сгенерированная `CloudNoise3D.asset` без швов при тайлинге (проверить просмотром срезов).

---

### 1.3 — `VolumetricCloudsRenderFeature` + `VolumetricClouds.shader` (скелет)

**Создать:**
1. `Assets/_Project/Scripts/Rendering/VolumetricCloudsRenderFeature.cs`
   - Копирует паттерн `EdgeDetectionRenderFeature.cs`
   - `RenderPassEvent.AfterOpaques` (или `BeforeRenderingTransparents` — уточнить)
   - Fullscreen triangle pass (как EdgeDetection: `GetFullScreenTriangleVertexPosition`)
   - Параметры в инспекторе:
     - `CloudNoise3D` (Texture3D reference)
     - `_CloudBottomY` / `_CloudTopY` (float)
     - `_RaymarchSteps` (int, 32–64)
     - `_MaxRayDistance` (float, 5000)
     - `_DensityMultiplier` (float)
     - `_WindOffset` (Vector3) — читается из `WindManager`
     - Ghibli-рампы: `_DayRampTop/Mid/Bot`, `_SunsetRampTop/Mid/Bot` (Color)

2. `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader`
   - `Shader "Hidden/ProjectC/VolumetricClouds"`
   - Fullscreen pass (как EdgeDetection.shader: `GetFullScreenTriangleVertexPosition`)
   - `#include "CloudNoise.hlsl"` + `#include "CloudCommon.hlsl"`
   - На этом этапе: **только плотность (ч/б)**, без освещения
   - Реконструкция луча: `UNITY_MATRIX_I_P` + `UNITY_MATRIX_I_V` (паттерн из VeilRaymarch.shader)
   - Функция `density(p)` = shapeNoise × heightProfile × windScroll
   - Early-exit: при `transmittance < 0.01`

**Подключение:**
- Добавить `VolumetricCloudsRenderFeature` в `ProjectC_URP_Renderer.asset` через Inspector (или кодом)

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
   - `_CameraMotionVectors` — из URP (или ручной расчёт через `UNITY_MATRIX_I_VP`)
   - Репроекция: `float2 historyUV = uv - motionVector.xy`
   - Blend: `lerp(current, history, 0.9)` (90% история, 10% новый)
   - Фоллбек при дисавкклюжене: проверка глубины history vs current

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

- **URP Render Graph:** EdgeDetectionRenderFeature использует RenderGraph API (`RecordRenderGraph`). Нужно сохранить этот паттерн.
- **RenderPassEvent:** `AfterOpaques` vs `BeforeTransparents` — зависит от того, должны ли облака перекрывать полупрозрачные объекты. По умолчанию `AfterOpaques`.
- **Texture3D в URP:** может потребоваться `#pragma enable_d3d11_debug_symbols` или специфичные настройки импорта. Проверить поддержку `TEXTURE3D` в URP 17.
- **Blue Noise:** если 64×64 текстура недоступна — сгенерировать через `CloudNoiseBaker` или взять из `Packages/com.unity.render-pipelines.core/Runtime/Textures/BlueNoise64`.
- **Temporal reprojection:** требует motion vectors. В URP есть `MotionVectors` pass; проверить, включены ли в рендерере.
- **GDD-14 рампы:** уточнить точные цвета из `GDD_14_Visual_Art_Pipeline.md`.
