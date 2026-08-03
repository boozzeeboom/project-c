# CLOUD_system 3.0 — Detailed Implementation Steps: Фаза 2 + Фаза 3

**На основе:** `CLOUD_OCEAN_MEDIUM_IMPLEMENTATION_PLAN.md` (§4)
**Дата:** 2026-08-02
**Статус:** 🟡 Ready for Implementation (Фаза 1 завершена, верификация — пользователем)

> Этот файл — sibling к `CLOUD_OCEAN_MEDIUM_DETAILED_STEPS.md` (тот покрывает Фазу 1).
> Все сигнатуры ниже сверены с кодом Фазы 1 (2026-08-02): `VolumetricCloudsRenderFeature.cs`,
> `VolumetricClouds.shader`, `CloudCommon.hlsl`, `WindManager.cs`, `StormController.cs`,
> `GlobalStormEvents.cs`, `VeilSystem.cs`. Если строка «дрейфует» — сверять с кодом, не с этим файлом.

---

## Сводка зависимостей

```
Фаза 2 (интерактивность)
  2.1 LocalDensityBuffer (compute, тор)        ← фундамент, от него всё
  2.2 SplatDensity API + демо «корабль режет»  ← 2.1 + правка CloudDensity в шейдере
  2.3 VFX: конденсационные следы               ← первый .vfx (VFX Graph 17.5.0 в manifest)
  2.4 VFX: молнии в грозовых ячейках           ← StormController.TriggerLightning()
  2.5 Мезий-харвест (плотность = ресурс)       ← 2.1 (чтение) + MeziyModuleActivator
  2.6 Перф-замер                                ← CloudPerfMonitor + таблица

Фаза 3 (интеграция в мир)
  3.1 Облачное море как пол                    ← расширение слоя вниз + горизонт
  3.3 Погодные ячейки от WindManager           ← WeatherCellManager (данные)
  3.2 Завеса как нижняя граница среды          ← 3.3 (ячейки) + тёмный регион
  3.4 Сетевые shared-возмущения                ← 2.2 (сплаты) + 3.3 (ячейки) + NGO
  3.5 Выпиливание старых Veil-рендереров       ← после 3.2 (покрытие сценариев)
  3.6 Перф-аудит полного кадра                 ← 2.6 + весь кадр
```

**Сквозные правила Фазы 2–3:**
- Все новые C#-классы — один класс на файл (Unity 6 генерирует один MonoScript на файл).
- Namespace: `ProjectC.World.Clouds` для новых облачных компонентов, `ProjectC.Rendering` — RenderFeature.
- После любого `create_script`/`script_apply_edits` → `refresh_unity` (force, compile, wait) → `read_console`.
- VFX Graph 17.5.0 (manifest) — НЕ трогать версию. Создание .vfx — через MCP `manage_vfx` или вручную (инструмент VFX Graph в Unity), НЕ из .cs.

---

## ФАЗА 2 — Интерактивность (4–6 недель)

---

### 2.1 — `LocalDensityBuffer` (compute, ping-pong, тор)

**Цель:** mutable 3D-поле плотности вокруг игрока; ветер адвектит, возмущения затухают.

**Создать:**

1. `Assets/_Project/Scripts/World/Clouds/LocalDensityBuffer.cs` (namespace `ProjectC.World.Clouds`)

```csharp
// Сигнатура (скелет)
public class LocalDensityBuffer : MonoBehaviour
{
    public static LocalDensityBuffer Instance { get; private set; }

    [Header("Buffer")]
    [Range(48, 128)] public int Resolution = 96;      // 96³–128³ (план §3.3)
    [Range(10f, 50f)] public float TexelSize = 20f;   // тексл 10–25 м → покрытие 1–3 км
    [Header("Advection")]
    public float AdvectionStrength = 0.5f;            // насколько ветер двигает поле
    public float RelaxationRate = 0.05f;              // затухание в секунду
    [Header("Splat")]
    public float MaxSplatRadius = 150f;

    // — Toroidal window: центр буфера следует за игроком, адресация fract()
    private RenderTexture _densityA;                  // ping
    private RenderTexture _densityB;                  // pong
    private ComputeShader _compute;
    private int _kernelAdvect;                        // AdvectAndRelax
    private int _kernelSplat;                         // ApplySplats

    public Vector3 Center;                            // worldPos центра окна (обновлять каждый кадр)
    public RenderTexture GetDensityRT();              // → для передачи в raymarch-шейдер
    public void SplatDensity(Vector3 worldPos, float radius, float amount);
    public void Clear();
    public float SampleDensity(Vector3 worldPos);     // 2.5 — CPU-зеркало (см. ниже)
}
```

   - `Awake`: singleton (как `WindManager.Instance`), создать RT:
     `RenderTextureDescriptor` `dimension = Tex3D`, `volumeDepth = Resolution`,
     `RenderTextureFormat.R16F`, `enableRandomWrite = true` (формат R16F — плотность один канал; RGBA16F не нужен).
   - **Ping-pong:** 2 RT, каждый кадр `kernelAdvect` читает `_DensityPrev`, пишет `_DensityNext`, затем swap ссылок.
   - **Тороидальная адресация:** центр окна = позиция игрока (`Center = NetworkPlayer/камера`);
     в compute `worldToUvw = (worldPos - Center) / (Resolution * TexelSize) + 0.5; uvw = frac(uvw);`
     — окно следует за игроком БЕЗ копирования данных (паттерн snowflow из плана §3.3).
   - `Update`: `Center = игрок.transform.position` (или камеры); 1 dispatch/frame.

2. `Assets/_Project/Shaders/Clouds/LocalDensity.compute`

```hlsl
// kernel AdvectAndRelax
//   Читает _DensityPrev (RWTexture3D<float>), пишет _DensityNext.
//   uvw = (id - 0.5) / Res            // локальные координаты ячейки
//   worldPos = Center + (uvw - 0.5) * (Res * TexelSize)
//   windOffset = _WindDirection * _AdvectionStrength * (TexelSize * Res / Res)   // сдвиг в текслах
//   sample = _DensityPrev.SampleLevel(sampler, frac(uvw + windOffset), 0)        // адвекция
//   _DensityNext[id] = max(0, sample - _RelaxationRate * _DeltaTime)             // релаксация

// kernel ApplySplats
//   Читает StructuredBuffer<SplatData> _Splats (count = _SplatCount), идемпотентно
//   добавляет/вычитает гауссово облако в каждый сплат:
//   for each splat: d = length(worldPos - splat.center);
//                   amount *= exp(-d² / (2 * splat.radius²));
//   _DensityNext[id] += amount;  (может быть отрицательным → max(0, ...))
```

   - `SplatData` (C#-структура): `Vector3 center; float radius; float amount;` — передаётся через `StructuredBuffer`, не через глобальные — сплатов за кадр несколько.
   - Ветер: читать `WindManager.Instance.CurrentWindDirection/CurrentWindSpeed` (null-guard), писать в compute `_WindDirection`/`_DeltaTime`.

**Приёмка:** в сцене есть объект с `LocalDensityBuffer`; в Play Mode видно (через debug-срез или `SplatDensity` тестовым вызовом), что поле живёт: сплат затухает за ~1–2 с, ветер двигает пятно.

---

### 2.2 — `SplatDensity` API + демо «корабль режет облака»

**Цель:** API записи работает end-to-end: корабль режет облака, разрез виден в raymarch и зарастает.

**Доработать:**

1. `VolumetricClouds.shader` — `CloudDensity` (сейчас: `float CloudDensity(float3 worldPos, float3 cameraPos, float coverage)`).
   Добавить вычитание локальной плотности:

```hlsl
TEXTURE3D(_LocalDensityRT);
SAMPLER(sampler_LocalDensityRT);
float3 _LocalDensityCenter;
float  _LocalDensitySize;        // Resolution * TexelSize (мир)
float  _LocalDensityInfluence;   // 0..2, масштаб влияния (дефолт 1)

float SampleLocalDensity(float3 worldPos)
{
    float3 uvw = (worldPos - _LocalDensityCenter) / _LocalDensitySize + 0.5;
    uvw = frac(uvw);                                   // тор
    return SAMPLE_TEXTURE3D_LOD(_LocalDensityRT, sampler_LocalDensityRT, uvw, 0).r;
}
// внутри CloudDensity, после shape*heightFade*coverage*_DensityMultiplier:
//   float local = SampleLocalDensity(worldPos);
//   density = max(0.0, density - local * _LocalDensityInfluence);
```

2. `VolumetricCloudsRenderFeature.cs`:
   - Поле `[Header("Phase 2.1: Local Density")] public LocalDensityBuffer LocalDensity;`
     (сериализованная ссылка; если null — шейдер работает без локального поля, старый путь).
   - В `ApplyProperties`: если `LocalDensity != null` →
     `mat.SetTexture(LocalDensityRTId, LocalDensity.GetDensityRT());`
     `mat.SetVector(LocalDensityCenterId, LocalDensity.Center);`
     `mat.SetFloat(LocalDensitySizeId, LocalDensity.Resolution * LocalDensity.TexelSize);`
   - Новые `Shader.PropertyToID`: `_LocalDensityRT`, `_LocalDensityCenter`, `_LocalDensitySize`, `_LocalDensityInfluence`.

3. Демо-компонент `ShipWakeCloudCutter.cs` (namespace `ProjectC.World.Clouds`):
   - Ссылки: `ShipController` + `LocalDensityBuffer`.
   - `Update`: если `!ship.IsDocked && скорость > порога` → `LocalDensity.Instance.SplatDensity(shipPos, radius: 30f, amount: -0.4f)` — вычитание (разрез).
   - Троттлинг: не каждый кадр, раз в 0.1 с (иначе сплат-шторм).

**Приёмка:** Play Mode: пролёт сквозь слой (Y≈1200–1500) оставляет видимый разрез; разрез зарастает за 1–2 с (релаксация).

---

### 2.2 Статус отладки (2026-08-03)

**🟡 Пайплайн работает end-to-end, но визуального разреза нет.** Все логические звенья проверены:

#### Что реализовано (код)

| Компонент | Статус |
|---|---|
| `LocalDensityBuffer.cs` (ComputeShader, ping-pong, тор, CPU mirror) | ✅ Работает |
| `LocalDensity.compute` (AdvectAndRelax + ApplySplats) | ✅ Работает |
| `ShipWakeCloudCutter.cs` (сплаты при движении корабля) | ✅ Работает |
| `VolumetricCloudsRenderFeature.cs` (читает Instance, передаёт RT в шейдер) | ✅ Работает |
| `VolumetricClouds.shader` — `SampleLocalDensity()` + вычитание в `CloudDensity()` | ✅ Работает |
| `_LocalDensityRT/center/size/influence` в ShaderLab Properties + `mat.Set*` | ✅ Работает |

#### Хронология отладки

1. **CPU mirror readback** (каждые 2 с): значения `center=33…40`, `max=33…40`, `centerWorld` у корабля, `splatQueue=0`.
   → Плотность **накапливается** в центре, сплаты обрабатываются.

2. **Лог `[VolClouds] Dens=...`** (каждые 120 кадров): `LocalDensity OK: RT=True size=1920`.
   → `LocalDensityBuffer.Instance` и `GetDensityRT()` живы в `ApplyProperties`.

3. **Шейдер:** `SampleLocalDensity` сэмплирует `_LocalDensityRT` с UVW = `(worldPos - center)/size + 0.5`.
   `CloudDensity` делает `density = max(0, density - local * influence)`.
   RT добавлен в ShaderLab Properties (`[HideInInspector] _LocalDensityRT ... 3D`), используется `mat.SetTexture` (не `Shader.SetGlobalTexture` — потому что без Properties TEXTURE3D не биндится в URP).

4. **Лог `Create()` + `AddRenderPasses()`**: `Create()` вызывается, `AddRenderPasses` → `Dens=` лог появляется.
   → RenderFeature гарантированно исполняется.

5. **`DebugDensityDirect = true`**: B&W проход показывает **белую область у корабля**.
   → RT доходит до шейдера, сэмплируется, значения ненулевые.

6. **Увеличены `CutAmount = 1.0`, `LocalDensityInfluence = 2.0`**: визуального разреза по-прежнему нет.

#### Разрыв: B&W показывает плотность, цветной проход — нет разреза

Оба прохода (`VolumetricClouds_BW` — Pass 0, `VolumetricClouds_Color` — Pass 1)
вызывают **одну и ту же** `CloudDensity()` с **одним и тем же** `_LocalDensityRT`.
B&W подтверждает, что RT сэмплируется корректно.

**Гипотеза:** в цветном проходе плотность обнуляется у корабля (как и задумано),
но визуально разрез не заметен — возможно, из-за крупного шага рэймарча
(30 шагов / 20000 юнитов = ~667 юнитов/шаг), и разрез попадает в 1–2 шага,
а остальной столб облака перекрывает дыру. Либо `_LocalDensityInfluence` сбрасывается
в 0 в цветном проходе (маловероятно — один материал, один `ApplyProperties`).

**Следующие шаги для диагностики:**
- Проверить `_LocalDensityInfluence` непосредственно в шейдере (вывести как цвет).
- Уменьшить `MaxRayDistance` до 3000 и увеличить `RaymarchSteps` до 64 — чтобы
  разрез занимал больше шагов.
- Проверить blending: `Blend SrcAlpha OneMinusSrcAlpha` — если accumulated.a=0,
  пиксель должен стать прозрачным и показать небо. Возможно, небо за облаками
  тоже облачное (нет синего фона).

---

### 2.2 Статус отладки — дополнение 2026-08-03 (вечер): кильватерный конус

**Задача:** симптом «область над кораблём расходится» → сделать расхождение за кораблём конусом.

#### Что исследовано (Mavis-сессия, Play Mode)

| Факт | Значение |
|---|---|
| `LocalDensityBuffer.Instance` в Play Mode | ✅ ALIVE (лог `LocalDensity OK: RT=True size=1920`) |
| RT | 96×96×96, RFloat, окно = Res×TexelSize = **1920** юнитов |
| `Center` (торроидальное окно) | = `FollowTarget.position` = позиция корабля `Ship_Light_root` |
| Корабль в полёте | Y≈2222 (внутри слоя 100–2500) |
| CPU mirror | `center=33…40` — плотность **копится в одной точке** (центр окна) |
| Лог `LocalDensity: NULL` | артефакт Edit Mode: `Instance` не создаётся вне Play (в полёте лог `OK`) |
| Камера | MainCamera на Y=3000 (вне слоя); игровая ThirdPersonCamera_0 у корабля |

**Ключевой вывод:** сплат пишется в позицию корабля, а центр торроидального окна
следует за кораблём (`FollowTarget`) → вырез всегда **вокруг корабля**, а не след
позади. Плотность копится в центре (33–40), и по лучу камеры это выглядит как
разрыв **над** кораблём. Самого «следа за кораблём» в данных нет.

#### Что сделано (код)

1. `Assets/_Project/Scripts/World/Clouds/ShipWakeCloudCutter.cs` — вместо одиночного
   сплата в позицию корабля — **кильватерный конус позади**: серия сплатов
   `pos - dir * (step * i)`, радиус растёт кзади.
   Новые поля (Header «Wake Cone»): `ConeSegments=8`, `ConeSpacing=0.5 × CutRadius`,
   `ConeRadiusGrowth=0.25`. Старые поля (`CutRadius/CutAmount/MinSpeed/SplatInterval`)
   сохранены — инспектор не ломается.
2. `Assets/_Project/Scripts/World/Clouds/LocalDensityBuffer.cs` — `_splatQueue` 16 → 64
   (конус генерит до 8 сплатов за тик; лимит 16 дропал бы сплаты с warning).
3. Дизайн-ноут: `docs/dev/CLOUD_OCEAN_PHASE2_WAKE_CONE.md`.

#### Результат

🔴 **Визуально НЕ видно НИЧЕГО** — ни конуса, ни разреза, ни следов за кораблём.
Пайплайн данных жив (Instance, RT, сплаты, логи — всё OK), но raymarch-проход
не показывает результат. `CutAmount=1.0`, `LocalDensityInfluence=2.0` — без эффекта.

#### Решение

**Отладку фазы 2.2 останавливаем (указание пользователя). Больше не кодим.**
Открытые гипотезы фиксируются для будущей сессии (не реализовывать без нового запроса):

- **Шаг рэймарча слишком крупный:** 30 шагов / 20000 юнитов ≈ 667 юнитов/шаг;
  узкий след (радиус ~30–80) попадает в 1–2 шага и теряется в столбе облака.
  → проверить `MaxRayDistance` 20000 → 3000 и `RaymarchSteps` 30 → 48–64.
- **Blending/фон:** проверить `Blend SrcAlpha OneMinusSrcAlpha`; если `accumulated.a≈0`,
  пиксель должен показать небо — но возможно «небо» за облаками тоже облачное
  (нет чистого фона для контраста).
- **`_LocalDensityInfluence` в шейдере:** вывести как цвет и убедиться, что не
  сбрасывается в 0 между Pass 0 и Pass 1.
- **B&W vs Color:** оба прохода зовут одну `CloudDensity()`, B&W плотность видит
  (`DebugDensityDirect=true` — белая область у корабля), цветной — нет. Причина
  расхождения остаётся невыясненной.

---
### 2.3 — VFX Graph: конденсационные следы (первый .vfx)

**Цель:** первый .vfx в проекте; след тянется за кораблём.

**Создать:**

1. `Assets/_Project/VFX/Contrail.vfx` — через MCP `manage_vfx` или вручную (Window → Visual Effects → Graph).
   - Граф: Spawn (по времени) → Initialize (позиция = спавн-точка за кораблём) → Update (движение назад + лёгкий дрейф по ветру) → Output Particle Quad/Strip.
   - Свойства графа, которые дёргает C#: `Emit` (bool), `SpawnPos` (Vector3), `WindVector` (Vector3).
   - Текстура частиц: существующий `Cloud_Noise1.png` (`Assets/_Project/Art/Textures/`) как soft-спрайт.

2. `Assets/_Project/Scripts/Ship/ShipContrailVfx.cs` (namespace `ProjectC.Ship` — след принадлежит кораблю):
   - Поля: `VisualEffect _vfx; ShipController _ship; float MinSpeed = 5f;`
   - `Update`:
     `_vfx.SetBool("Emit", !_ship.IsDocked && currentSpeed > MinSpeed);`
     `_vfx.SetVector3("SpawnPos", точка позади корабля);`
     `_vfx.SetVector3("WindVector", WindManager.Instance != null ? WindManager.Instance.CurrentWindDirection * CurrentWindSpeed : Vector3.zero);`
   - Скорость брать из `ShipTelemetryState` (событие `OnTelemetryStateChanged` уже есть в ShipController, строка 917) или `ship.Rigidbody.velocity.magnitude`.

**Приёмка:** полёт на корабле → белый след тянется и сносится ветром; на стоянке/доке следа нет.

---

### 2.4 — VFX Graph: молнии в грозовых ячейках

**Цель:** молния визуально синхронна с событиями шторма.

**Создать:**

1. `Assets/_Project/VFX/LightningBolt.vfx`:
   - Молния = кривая (Spline/Line Renderer-подобный граф) + вспышка освещения вокруг.
   - Метод запуска: `Play()` на графе; параметры: `StartPos`, `EndPos` (от верха ячейки к низу), `Seed`.

2. `Assets/_Project/Scripts/World/Clouds/StormLightningVfx.cs` (namespace `ProjectC.World.Clouds`):
   - Поле: `VisualEffect _vfx;`
   - Подписка на существующий API штормов (НЕ создавать новый шторм-менеджер):
     - `StormController.TriggerLightning()` — уже есть (строка 135 `StormController.cs`);
       подписаться через `StormController.ClientControllers` (строка 38) или событие-обёртку.
     - `GlobalStormEvents.OnStormIntensityChanged` (строка 7) — менять интенсивность вспышек/частоту.
   - `OnLightningTriggered(StormController storm)`: `_vfx.SetVector3("StartPos", storm позиция + высота); _vfx.SetVector3("EndPos", storm позиция); _vfx.SetFloat("Seed", Random.value); _vfx.Play();`
   - null-guard: `VisualEffect` может отсутствовать в тестовых сценах.

**Приёмка:** в сцене со `StormController` молния VFX появляется синхронно с `TriggerLightning()`; интенсивность меняется от `GlobalStormEvents`.

---

### 2.5 — Мезий-харвест (плотность = ресурс)

**Цель:** «Лура-кейс» — сбор мезия из плотной области облаков работает (клиентский прототип; серверная авторизация — 3.4).

**Создать:**

1. CPU-зеркало плотности в `LocalDensityBuffer.cs` (добавить к 2.1):
   - `private readonly float[] _cpuDensity;` (Res³ массив).
   - `SplatDensity(worldPos, radius, amount)` — ДОПОЛНИТЕЛЬНО пишет в `_cpuDensity` (та же гауссова формула) — CPU-сторона зеркалит GPU без readback.
   - `public float SampleDensity(Vector3 worldPos)` — читает `_cpuDensity` (тор-адресация, билинейно по 8 соседям).
   - Релаксация CPU — в `Update` тем же `RelaxationRate` (дёшево: Res³ float, 96³ ≈ 0.9 МБ).

2. `Assets/_Project/Scripts/World/Clouds/MeziyHarvestProbe.cs` (namespace `ProjectC.World.Clouds`):
   - Каждые 0.5 с: `float d = LocalDensityBuffer.Instance.SampleDensity(transform.position);`
   - Если `d > _harvestThreshold` → инкремент счётчика собранного «сырого мезия» (Debug.Log + счетчик; реальная выдача — через Trade/Inventory подсистему отдельным тикетом).
   - Точка интеграции с существующим: `Assets/_Project/Scripts/Ship/MeziyModuleActivator.cs` (модули MEZIY_THRUST/ROLL/... уже есть в Data/Modules) — харвест-зонд вешать на корабль рядом с активатором.

**Приёмка:** подлёт к плотному пятну (создать `SplatDensity(+amount)` тестом) → счётчик мезия растёт; в разреженной зоне — нет.

---

### 2.6 — Перф-замер

**Доработать:** `CloudPerfMonitor.cs` уже есть (CustomSampler + FrameTimingManager), но:
- FrameTimingManager требует Dynamic Resolution в настройках URP (иначе 0 тиков) — проверить; если пусто — перейти на замер через `ProfilingSampler` RenderGraph пассов (уже есть: `PassName = "VolumetricClouds"`) в Profiler.
- Добавить конфигурационные прогоны: steps 32/48/64 × half-res on/off × temporal on/off × LocalDensity on/off.

**Результат:** таблица в `docs/world/CLOUD_system/3.0/PERF_RESULTS.md` (конфиг | 1080p | 1440p).
**Цель (план §5):** суммарно облака ≤4–5 мс (LocalDensityBuffer 0.2–0.5 мс, VFX 0.3–1.0 мс).

---

## ФАЗА 3 — Интеграция в мир (3–4 недели)

---

### 3.1 — Облачное море как пол (слой ниже Y=1200)

**Цель:** «пол» виден с любой высоты, горизонт затянут (GDD-02).

**Доработать:** `VolumetricCloudsRenderFeature.cs` + `VolumetricClouds.shader` — расширение существующего слоя, НЕ второй реймарчер:

1. В `VolumetricCloudsRenderFeature`: `CloudBottomY` уже 800 (полоса 800–2000 — решение пользователя).
   - Добавить `[Header("Phase 3.1: Horizon")] [Range(20000f, 100000f)] public float MaxRayDistanceHorizon = 40000f;`
     — для лучей, уходящих к горизонту (угол к горизонту < 5°), увеличивать `_MaxRayDistance` (иначе горизонт обрывается на 5 км).
   - Шейдер: в `frag` Pass 1 выбрать `maxDist = abs(rayDir.y) < sin(5°) ? _MaxRayDistanceHorizon : _MaxRayDistance;`
2. Плотность нижней кромки: `HeightProfileSimple` уже даёт 0 у `CloudBottomY` — для «моря» нижняя кромка должна быть плотнее:
   - Новый параметр `[Range(0f, 1f)] public float BottomDensityBoost = 0.3f;` → `density += boost * (1 - smoothstep(CloudBottomY, CloudBottomY + 500, y))`.

**Приёмка:** с Y=5000 взгляд к горизонту → облачная масса до самого горизонта, без обрыва; с Y=900 — «пол» под кораблём.

---

### 3.3 — Погодные ячейки от `WindManager`

**Цель:** движущиеся грозы/прояснения, согласованные с серверным ветром (WindManager читает ServerWeatherController).

**Создать:**

1. `Assets/_Project/Scripts/World/Clouds/WeatherCellManager.cs` (namespace `ProjectC.World.Clouds`):
   - Singleton (как WindManager).
   - `public struct WeatherCell { public Vector3 Position; public float Radius; public int Type; /* 0=ясно,1=шторм,2=дождь */ public float Intensity; }`
   - `public List<WeatherCell> Cells;`
   - `Update`: двигать ячейки `Position += WindManager.Instance.CurrentWindDirection * CurrentWindSpeed * Time.deltaTime;` + морфинг интенсивности.
   - API: `AddCell(...)`, `RemoveCell(...)`, `GetCells()`.

2. Передача в шейдер (без StructuredBuffer на старте — массив float4):
   - `VolumetricCloudsRenderFeature.ApplyProperties`: `mat.SetVectorArray("_WeatherCells", ...)` (до 16 ячеек, `float4 = pos.xyz + type`).
   - Шейдер `CloudCoverage2D`: умножать coverage на `WeatherCellInfluence(xz)`: внутри ячейки шторма → `coverage` вверх и `density` вверх; ячейка ясна → `coverage` вниз.

**Приёмка:** добавленная ячейка шторма движется по ветру; облачность вокруг неё растёт; ячейка ясна даёт дыру.

---

### 3.2 — Завеса как нижняя граница среды

**Цель:** единая среда; геймплей Завесы (`VeilSystem.cs`) НЕ ломается.

**Правила (AGENTS.md/план §3.4):**
- ❌ НЕ менять `VeilSystem.cs` геймплей-логику (warning trigger, ядовитая зона, молнии-триггеры).
- ✅ Визуально: нижняя граница среды = тёмный регион в том же raymarch.

**Доработать:** `VolumetricClouds.shader`:
1. Расширить нижнюю границу слоя: `_CloudBottomY` на уровне завесы не трогаем (800 — решение пользователя);
   добавить ОТДЕЛЬНУЮ тёмную полосу в нижней части реймарча:
   `density += _VeilDarkening * smoothstep(veilY + fade, veilY, worldPos.y)` (veilY≈12, параметр `[Header("Phase 3.2")] public float VeilDarkeningY = 12f;` + `Color VeilTint = #2d1b4e` — цвет из `CloudClimateTinter.purpleVeil`).
2. Молнии Завесы: остаются на `VeilSystem.lightningParticles` (ParticleSystem) — VFX-молнии (2.4) подключаются к StormController, не к VeilSystem (защита геймплея).

**Приёмка:** спуск к Y≈12 — облачная среда темнеет к низу (визуально «дно мира»); `VeilSystem` предупреждения/триггеры работают как раньше.

---

### 3.4 — Сетевые shared-возмущения (NGO RPC / NetworkVariable)

**Цель:** события (шторм, дыра от бомбы, сбор мезия) применяются одинаково на всех клиентах.

**Создать** (два класса, два файла — один класс на файл):

1. `Assets/_Project/Scripts/Network/CloudDisturbanceServer.cs` (namespace `ProjectC.Network`) — `NetworkBehaviour`, scene-placed (в `BootstrapScene`; спавнится через `ScenePlacedObjectSpawner` — иначе `IsSpawned == false` и RPC → NRE, см. AGENTS.md).

```csharp
public class CloudDisturbanceServer : NetworkBehaviour
{
    // server-authoritative список активных штормов/дыр — реплицируется
    private readonly NetworkList<DisturbanceDto> _active = new();   // NGO 2.x NetworkList

    [Rpc(SendTo.Server)]
    public void RequestDisturbanceRpc(DisturbanceDto dto, RpcParams rpcParams = default);

    [Rpc(SendTo.ClientsAndHost)]
    public void BroadcastDisturbanceRpc(DisturbanceDto dto, RpcParams rpcParams = default);

    // клиенты вызывают BroadcastDisturbanceRpc → применяют сплат в LocalDensityBuffer
}
```

   - `DisturbanceDto` : `INetworkSerializable` — `Vector3 center; float radius; float amount; byte kind;`
     ⚠️ все string-поля инициализировать `""` (NRE-ловушка NGO).
   - Валидация на сервере: дистанция от источника ≤ лимита, частота (троттлинг) — против спама RPC.

2. `Assets/_Project/Scripts/Network/CloudDisturbanceClient.cs` (namespace `ProjectC.Network`):
   - Локальная ссылка на `LocalDensityBuffer`.
   - `OnBroadcastDisturbance(DisturbanceDto dto)`: `LocalDensityBuffer.Instance.SplatDensity(dto.center, dto.radius, dto.amount);`
   - ММО-граница (план §3.3): возмущения клиент-локальные по умолчанию; server-authoritative ТОЛЬКО геймплей-события (шторм, бомба, харвест). Полная синхронизация 3D-поля НЕ планируется.

**Приёмка (Host+Client):** на хосте вызываем `RequestDisturbanceRpc` → оба клиента применяют одинаковый сплат в своих LocalDensityBuffer; поле не расходится (визуально один и тот же разрез).

---

### 3.5 — Выпиливание старых Veil-рендереров

**Цель:** чистка после покрытия сценариев 3.0.

**Кандидаты (из `Assets/_Project/Scripts/World/Clouds/`):**
- `VeilRaymarchBlit.cs`, `VeilRaymarchMeshController.cs`, `HorizonVeilRenderer.cs`, `CumulonimbusCloud.cs`, `AdditionalVeilModule.cs`.

**Порядок (ОБЯЗАТЕЛЬНО):**
1. До выпиливания — чек-лист покрытия: каждый сценарий старого рендерера воспроизводится через 3.0 (слой 800–2000, 3.1 горизонт, 3.2 завеса, 3.3 ячейки).
2. Удалять по одному файлу за раз; после каждого — `refresh_unity` + `read_console`; проверить ссылки в сценах/префабах (`CloudSystem.prefab`, `VeilRaymarch.mat`, `Veilblit.mat` — удаляются вместе).
3. `VeilSystem.cs` (геймплей) — ОСТАЁТСЯ.

**Приёмка:** сценарии Veil покрыты 3.0; 0 ошибок компиляции; в сценах нет missing scripts.

---

### 3.6 — Перф-аудит полного кадра

**Доработать:** расширить `CloudPerfMonitor` (или отдельный `CloudPerfAudit`) — замер полного кадра:
- Profiler: кадр целиком + секции VolumetricClouds / LocalDensityBuffer / VFX / VeilSystem / остальное.
- Сравнение с бюджетом Stage 2.5.

**Результат:** `docs/world/CLOUD_system/3.0/PERF_RESULTS.md` — полная таблица.
**Цель (план §5):** облака ≤4–5 мс на 1080p mid-GPU.

---

## Порядок выполнения

**Фаза 2:**
1. **2.1 LocalDensityBuffer** — фундамент (compute + C# + тор).
2. **2.2 SplatDensity + демо** — проверяет 2.1 end-to-end через шейдер.
3. **2.3 VFX следы** — первый .vfx (параллельно с 2.4 можно).
4. **2.4 VFX молнии** — интеграция со StormController.
5. **2.5 Мезий-харвест** — CPU-зеркало + зонд.
6. **2.6 Перф** — таблица.

**Фаза 3:**
1. **3.1 Горизонт/пол** — расширение слоя.
2. **3.3 Погодные ячейки** — данные для 3.2 и 3.4.
3. **3.2 Завеса как граница** — после ячеек.
4. **3.4 Сеть** — после 2.2 (сплаты) и 3.3 (ячейки).
5. **3.5 Выпиливание старых рендереров** — после 3.2 (покрытие).
6. **3.6 Перф-аудит** — финал.

---

## Риски и заметки

- **LocalDensityBuffer + RenderGraph:** 3D RT импортировать в RenderGraph через `renderGraph.ImportTexture(rt3d)`; НЕ писать в неё из raster-пасса (только compute). Читать в raymarch — через глобальную текстуру, как `_CloudNoise3D`.
- **R16F на мобильных/старых GPU:** если banding в локальном поле — перейти на RGBA16F (план §3.2 позволял RGBAHalf при бандинге).
- **Сплат-шторм:** троттлить `SplatDensity` (не чаще 10/с) — иначе compute-очередь захлёбывается.
- **VFX Graph:** .vfx создаётся инструментом Unity (MCP `manage_vfx` или вручную). НЕ пытаться писать .vfx как текст.
- **NGO 2.x:** `[Rpc(SendTo.X)]` (не deprecated `[ServerRpc]`/`[ClientRpc]`); scene-placed NetworkObject в BootstrapScene — спавнится через `ScenePlacedObjectSpawner` (иначе `IsSpawned == false` → NRE в `__endSendRpc`). Один класс на файл.
- **`CloudPerfMonitor` FrameTimingManager:** требует Dynamic Resolution в URP; иначе 0 тиков — запасной путь Profiler/ProfilingSampler.
- **GDD-14:** рампы — предложения (план §1.5); перед 3.2 цвет завесы `#2d1b4e` взят из `CloudClimateTinter.purpleVeil` — подтвердить у дизайнера.
- **Стиль кода:** `[SerializeField] private` для inspector-полей, `_camelCase` поля, комментарии RU, namespace по папке.
