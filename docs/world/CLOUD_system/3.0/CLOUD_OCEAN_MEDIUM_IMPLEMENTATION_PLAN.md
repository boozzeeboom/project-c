# CLOUD_system 3.0 — «Cloud Ocean Medium» — Implementation Plan

**Версия:** 3.0 (Cloud Ocean Medium) | **Дата:** 2026-08-02 | **Status:** 🟢 Фаза 1 (Визуальное ядро) — в работе, рендер-конвейер жив
**Автор:** Mavis (по решению сессии 2026-08-01)
**Направление:** НОВОЕ. Не продолжение 1.0/2.0 (mesh/billboard/Veil) — те анализы вели «в лор», а не в ядро. 3.0 документирует ядро визуала мира.

---

## 0. TL;DR

У игры **нет террейна** — игрок живёт ВНУТРИ облачной среды. Значит облака — не эффект и не «решение чтобы попасть в лор», а **сам мир**: пол под ногами, пространство полёта, погода, ресурс, угроза. 3.0 заменяет архитектуру «объектов с шейдером» на архитектуру **рендера среды** (participating medium): один volumetric raymarch-рендерер на весь мир + mutable 3D-поле плотности для интерактивности.

**Ключевые ингредиенты (которых нет в текущем коде):**
1. Бейкнутая 3D-текстура Worley + градиентный Perlin (сейчас — value noise `Hash31` вживую, бандится)
2. Light marching (4–6 шагов к солнцу, HG-фаза, multi-scatter аппроксимация) — сейчас только Beer-Lambert поглощение, солнца нет
3. Half-res + blue-noise дизеринг + temporal reprojection — сейчас полный рес, 12–24 шага
4. Mutable 3D-буфер плотности (compute) — сейчас облака пассивны, только UV-оффсет ветра
5. VFX Graph 17.5.0 (уже в manifest, **0 .vfx файлов в проекте**) — следы, молнии, дождь, ближние клубы

**Уже есть и переиспользуется:** `src/CloudMath.cs` (Perlin+Worley FBM логика → HLSL-порт), `WindManager`, `CloudClimateTinter`, `EdgeDetectionRenderFeature.cs` (шаблон Renderer Feature), URP Renderer `ProjectC_URP_Renderer.asset`.

---

## 1. Видение: облака = среда, а не эффект

```
ВЕСЬ визуальный мир = единое поле плотности ρ(x,y,z,t)
        │
        ├── Ниже Y=1200           → «облачное море» — ПОЛ мира (реймарч-слой)
        ├── Вокруг игрока (1–3 км) → клубы, сквозь которые летишь (локальный 3D-буфер)
        ├── Горизонт              → дальние кучевые (та же функция, другой масштаб)
        └── Завеса (Y≈12)         → опасная НИЖНЯЯ ГРАНИЦА среды (геймплей сохраняется)
```

- Один реймарчер на всё — как в Flight Simulator 2020: та же система, разные регионы плотности.
- Урок snowflow_demo: **всё одним полем** (террейн + снег + персонаж из одного noise-стека, один draw call). Мы делали наоборот — 4 параллельных эксперимента вместо одного медиума.
- Интерактивность = **API записи в буфер плотности**, а не чудо: корабль режет облака → сплат; гроза → движущаяся ячейка плотности; сбор мезия → плотность = ресурс.

## 2. Дизайн-выбор (ОТКРЫТ до Фазы 1)

| Вопрос | Вариант A: Comic-book стилизованный | Вариант B: Физически-правдоподобный |
|---|---|---|
| Шейдинг | Цветовые рампы день/закат (GDD-14: `#FFFFFF` → `#FFB6C1`), rim, мягкий свет | Beer-Lambert + HG + multi-scatter, «как snowflow» |
| Соответствие пилляру «Sci-Fi + западные комиксы» | ✅ | ⚠️ конфликт |
| Технология | Одна и та же | Одна и та же |

**Дефолт: Вариант A (Comic-book)** — соответствует новому пилляру и GDD-14. Технология не меняется от выбора; отличаются только шейдинг-рампы и требования к читаемости силуэта.

## 3. Архитектура

### 3.1 Рендер: один URP Renderer Feature

```
VolumetricCloudsRenderFeature (по шаблону EdgeDetectionRenderFeature.cs)
└── ScriptableRenderPass, RenderPassEvent.AfterOpaques / BeforeTransparents (уточнить при реализации)
    └── Fullscreen-пас: для каждого пикселя
        ├── Реконструкция луча из камеры (UNITY_MATRIX_I_V / I_P — паттерн уже есть в VeilRaymarch.shader)
        ├── Шаг по лучу (32–48 шагов, адаптивный early-exit при transmittance < 0.01)
        ├── density(p) = shapeNoise(FBM: Perlin base mass + Worley erosion)
        │                × heightProfile(Y) × coverageMap(x,z) × windScroll(t)
        │                − localDisturbance(3D-буфер, см. 3.3)
        ├── Lighting: transmittance по Beer-Lambert + 4–6 light steps
        │              + multi-scatter аппроксимация pow(transmittance, k)
        │              + HG phase function + ambient + silver lining (rim)
        └── Output: half-res RT + blue-noise дизеринг + temporal reprojection
```

### 3.2 Данные (бейк при старте/загрузке сцены)

| Данные | Формат | Источник |
|---|---|---|
| 3D Worley noise (tileable) | 128³–256³ RGBA8, каналы: low-freq Worley + high-freq Worley + Perlin | HLSL-порт `src/CloudMath.cs`, бейк через compute → `Texture3D` |
| Coverage map (где облака, где прояснения) | 2D текстура или процедурный domain-warp | Процедурно (FBM) или бейк из `src/CloudGenerator.cs` v6.0 (детерминированный, Sphere/Column/Platform) |
| Weather cells | CPU-список ячеек (позиция, радиус, тип) | `WindManager` + новое `WeatherCellManager` |

### 3.3 Интерактивность: mutable 3D-поле плотности

```
LocalDensityBuffer (compute)
├── 3D RenderTexture 96³–128³, покрытие ~1–3 км вокруг игрока
│   (размер тексла 10–25 м — уточнить при прототипе)
├── Ping-pong, 1 dispatch/frame: адвекция ветром + релаксация + splat'ы
├── API: SplatDensity(worldPos, radius, amount)  — корабль режет облака,
│        молния прожигает, гроза уплотняет, мезий-харвест читает
└── Читается raymarch-пасом как вычитание/сложение плотности
```

- Тороидальная адресация (как в snowflow: `fract(worldXZ / size)`) — окно следует за игроком без копирования буфера.
- **MMO-граница:** возмущения — клиент-локальные по умолчанию (GDD-02 требование «Most clouds client-side only»). События, влияющие на геймплей (шторм, дыра от бомбы, сбор мезия) — server-authoritative через NGO RPC/NetworkVariable → клиент применяет сплат. Полная синхронизация 3D-поля НЕ планируется.

### 3.4 Завеса (геймплей сохраняется, рендер меняется)

- Текущий геймплей Завесы (warning trigger, молнии, ядовитая зона) **не ломаем** — `VeilSystem.cs` остаётся как геймплей-слой.
- Визуально Завеса (Y≈12) становится **нижней границей среды**: та же функция плотности, более тёмный регион + молнии. Старые Veil-рендереры (`VeilRaymarchBlit`, `VeilRaymarchMeshController`, `Cumulonimbus*`) — **кандидаты на выпиливание** после того, как 3.0 покроет их сценарии (отдельный тикет, не в этой фазе).

### 3.5 Погода и VFX

- Погодные ячейки: движение/морфинг от `WindManager` (уже читает серверный ветер 0.5 Hz).
- VFX Graph 17.5.0 (уже в manifest): конденсационные следы кораблей, молнии, дождевые завесы, ближние клубы вокруг игрока. **Первый .vfx ассет в проекте.**

## 4. Фазы реализации

### Фаза 1 — Визуальное ядро (2–4 недели)

**Цель:** облака выглядят как облака. `VolumetricCloudsRenderFeature` + полный light-marching стек.

| # | Задача | Выход | Приёмка |
|---|---|---|---|
| 1.1 | HLSL-порт `src/CloudMath.cs` (Perlin gradient + Worley) | `CloudNoise.hlsl` | Сравнение с C#-эталоном (web-визуализатор) |
| 1.2 | Бейк 3D Worley-текстуры (compute → Texture3D) | `BakeCloudNoise.compute` + asset/RT | Тайлинг без швов |
| 1.3 | `VolumetricCloudsRenderFeature` (копия шаблона EdgeDetection) | Fullscreen-пас | 0 ошибок компиляции, ч/б плотность |
| 1.4 | Реймарч с height profile + coverage + wind | Плотность слоя | Реалистичная форма слоя |
| 1.5 | Light marching (4–6 шагов) + HG + multi-scatter | Цвет облаков | Дневные/закатные рампы (GDD-14) |
| 1.6 | Half-res + blue-noise + temporal reprojection | Качество | Нет бандинга, нет мерцания при движении |
| 1.7 | Перф-замер | Таблица FPS/мс | ≤3 мс на 1080p mid-GPU |

### Фаза 2 — Интерактивность (4–6 недель)

**Цель:** игрок взаимодействует со средой.

| # | Задача | Выход | Приёмка |
|---|---|---|---|
| 2.1 | `LocalDensityBuffer` (compute, ping-pong, тор) | 3D RT 96³–128³ | Ветер адвектит, следы затухают |
| 2.2 | `SplatDensity` API + демо (корабль режет облака) | Разрез виден, зарастает | Игровой тест в Play Mode |
| 2.3 | VFX Graph: конденсационные следы | Первый .vfx | След тянется за кораблём |
| 2.4 | VFX Graph: молнии в грозовых ячейках | .vfx | Синхрон с событиями |
| 2.5 | Мезий-харвест (чтение плотности как ресурса) | API | Лура-кейс работает |
| 2.6 | Перф-замер | Таблица | Суммарно ≤4–5 мс |

### Фаза 3 — Интеграция в мир (3–4 недели)

**Цель:** облака = мир, а не отдельная фича.

| # | Задача | Выход | Приёмка |
|---|---|---|---|
| 3.1 | Облачное море как пол (слой ниже Y=1200) | Пол виден с любой высоты | Горизонт затянут (требование GDD-02) |
| 3.2 | Завеса как нижняя граница среды | Единая среда | Геймплей Завесы не сломан |
| 3.3 | Погодные ячейки от `WindManager` | Движущиеся грозы | Совпадает с серверным ветром |
| 3.4 | Сетевые shared-возмущения (RPC/NetworkVariable) | События применяются клиентами | 2 клиента видят шторм одинаково |
| 3.5 | Выпиливание старых Veil-рендереров | Чистка | Сценарии покрыты 3.0 |
| 3.6 | Перф-аудит полного кадра | Профайлер | Бюджет Stage 2.5 |

**Итого: 9–14 недель** до «облака = ядро». Каждая фаза проверяется отдельно, код — после утверждения дизайн-ноты.

## 5. Перф-бюджет (цель)

| Статья | 1080p mid-GPU | 1440p |
|---|---|---|
| VolumetricCloudsRenderFeature (half-res + temporal) | 1.5–3.0 мс | 2.5–4.5 мс |
| LocalDensityBuffer (compute) | 0.2–0.5 мс | 0.2–0.5 мс |
| VFX Graph (следы/молнии/дождь) | 0.3–1.0 мс | 0.3–1.0 мс |
| **Итого облака** | **≤4–5 мс** | ≤6 мс |

## 6. Риски

| Риск | Митигация |
|---|---|
| Перф: реймарч на слабых GPU | Half-res + temporal + adaptive steps (early-exit), LOD по расстоянию |
| Бандинг/мерцание | Blue-noise + temporal reprojection (стандарт индустрии) |
| Стиль: физический реализм vs comic-book стилизация | Дизайн-выбор зафиксирован в §2: Вариант A |
| MMO: рассинхрон возмущений | Клиент-локальные по умолчанию, события server-authoritative |
| Сломанный геймплей Завесы | VeilSystem остаётся геймплей-слоем; рендереры выпиливаются отдельным тикетом после покрытия |
| Unity: OnRenderImage устарел | Только Renderer Feature / Render Graph (URP 17) |

## 7. Что НЕ входит в 3.0

- HDRP-миграция (рассматривается только если пилляр сменится на реализм)
- Полная синхронизация 3D-поля между клиентами
- Asset-store системы (Enviro/Azure/COZY) — референс-прототип возможен, ядро — своё
- Физика облаков (плотность → физика полёта) — отдельный тикет, после 3.0

## Приложение A — Фаза 1: что сделано дополнительно (2026-08-02)

### A.1 Статус задач Фазы 1

| # | Задача | Статус | Примечание |
|---|---|---|---|
| 1.1 | HLSL-порт Perlin/Worley | ✅ | `CloudNoise.hlsl` — `Perlin3D` (ветка period=0 корректна), `Worley3D`, `Fbm`; сигнатуры сверены с C#-эталоном |
| 1.2 | Бейк 3D-шума | ✅ (с фиксом) | **Баг 0xCD**: 8 МБ данных `CloudNoise3D.asset` = 0xCD, `m_ImageContentsHash`=0, `m_StreamData size:0` → пиксели не записаны, density=0. Фикс `CloudNoiseBaker.cs`: 12-арг `Graphics.CopyTexture` (в Unity 6000.4.1f1 только 4 перегрузки; срез 3D-текстуры — через `srcElement`; 8-арг с `srcSlice` не существует) |
| 1.3 | Renderer Feature | ✅ | `VolumetricCloudsRenderFeature`, RenderGraph API (`RecordRenderGraph`), шаблон `EdgeDetectionRenderFeature.cs` |
| 1.4 | Реймарч + height profile + coverage + wind | ✅ | Полоса **800–2000** (решение пользователя: оставить); coverage — процедурный 2D FBM по XZ (`CloudCoverage2D`); ветер — `WindManager` |
| 1.5 | Light marching + HG + multi-scatter + рампы | ✅ | 6 light steps, `HG(g=0.7)`, `MultiScatterApprox`, цветовые рампы день/закат (выбор §2: Вариант A) |
| 1.6 | Half-res + blue-noise + temporal | 🔄 переработано | MRT-композит → single-target + ping-pong история (см. A.4) |
| 1.7 | Перф-замер | ⏳ | Открыт — после подтверждения видимости |

### A.2 Диагностический путь «почему ничего не видно»

1. **Pass 1 рендерит облака — доказано рантаймом** (DIAG2, редакторный тест вне пайплайна): `alphaMin=0, alphaMax=1, alphaMean=0.13, nonBlack=13.9%` — реймарч, плотность, геометрия лучей корректны.
2. **Событие пасса** (`RenderPassEvent`): `AfterRenderingOpaques`=300 < `BeforeRenderingSkybox`=350 → skybox рисовался **поверх** облаков и затирал их звёздным куполом («звёздное небо вместо облаков»). Перенесено на `BeforeRenderingTransparents`=450.
3. **Бинарный тест** (`DebugDensityDirect`): Pass 0 (B&W плотность) напрямую в цвет камеры — **виден контрастно** ⇒ пасс исполняется, реймарч жив в реальном пайплайне; теряется именно композит.
4. **Убийца композита — MRT**: Pass 2 писал MRT (colorTarget + cloudFinal) c `Blend 0 SrcAlpha OneMinusSrcAlpha` / `Blend 1 One Zero` — в реальном RenderGraph-пайплайне результат не доходил до экрана. Рабочий эталон Edge Detection — **один таргет**, `Blend SrcAlpha OneMinusSrcAlpha`.

### A.3 Изолированный тест конвейера (вне RenderGraph)

Воспроизведение Pass A→B→C на обычных RenderTexture с синтетической камерой (строго вниз с Y=2500, temporal 0.9, незаполненная история): `alphaMean=1.0`, 100% покрытие, **NaN=0** — шейдер-логика доказанно корректна; ломалась именно RenderGraph-интеграция.

### A.4 Архитектурные изменения

1. **Композит — single-target**: Pass 2, `Blend SrcAlpha OneMinusSrcAlpha`, `SV_Target0` → colorTarget (структурно как Edge Detection).
2. **История — ping-pong из 2 RT** (`_CloudHistoryA/B`, свап по `_historyIdx`): Pass B читает RT прошлого кадра, Pass C пишет в другой. Причина: RenderGraph запрещает read+write одной текстуры в одном пассе (грабли №3 в `rendergraph-volumetric-clouds-pitfalls.md`); старая MRT-схема (`cloudFinal` + `AddCopyPass`) убрана.
3. **Pass 3 (новый)** — raw result → history RT, `Blend One Zero` (RT не очищается RenderGraph'ом). Тот же фрагмент `CompositeClouds` (вынесен в общий HLSLINCLUDE).
4. **Первый кадр**: `_TemporalBlend=0`, пока нет валидного `_PrevViewProj` — иначе `lerp(current, history=0, 0.9)` даёт еле видные 0.1×current.
5. **Debug-тумблер** `DebugDensityDirect` (Inspector рендерера) — бинарный тест, оставлен для будущих сессий (по умолчанию выкл).

### A.5 Уроки (для Фазы 2)

- Облака обязаны рендериться **после** skybox (`BeforeRenderingTransparents`=450); иначе купол затирает слой.
- MRT-бленд (`Blend 0/Blend 1`) в RenderGraph-пассе ненадёжен; **single-target + отдельный пасс на историю** — рабочая схема.
- Половина «невидимости» — пайплайн-интеграция, не шейдер: изолированный тест вне RenderGraph + бинарный debug-тумблер дают ответ за один плейтест.
- В half-res пассе `_ScreenParams` неверен — нужен явный `_CloudTargetSize` (уже в коде).
- Бленд-состояние композита сверять с рабочим эталоном `EdgeDetectionRenderFeature` (та же путаница была при его создании — урок пользователя).

### A.6 Проверка

- Компиляция: Unity → Console → 0 ошибок (2 предупреждения сторонние: `EscMenuStyles` USS, кастомный toolbar-элемент).
- Плейтест: Play → Y=2500, взгляд вниз → облачная завеса 800–2000; спуск в полосу (~1200–1500) → облака вокруг.
- Если снова пусто: Inspector → `ProjectC_URP_Renderer` → `VolumetricCloudsRenderFeature` → Debug → **DebugDensityDirect** = true (бинарный тест).

## 8. История

| Дата | Сессия | Изменения |
|---|---|---|
| 2026-08-02 | Анализ snowflow_demo + рефрейм «облака = среда» | Создан план 3.0. Решение: Путь 3 «Cloud Ocean Medium», 3 фазы, дизайн-выбор стиля открыт |
| 2026-08-02 | Фаза 1 — реализация + дебаг видимости | См. Приложение A: 1.1–1.6 готовы (с фиксами), 1.7 открыт; композит переведён с MRT на single-target + ping-pong история |
| 2026-08-02 | Диагностика: материал → шейдер | См. Приложение B: цепочка mat.SetFloat→шейдер сломана для половины свойств; фикс: Shader.SetGlobal* + удаление из Properties; cloudRT/history отключены (Pass 1 напрямую в экран) |

## Приложение B — Диагностика «mat.SetFloat не доходит до шейдера» (2026-08-02)

### B.1 Контекст

После A.4 облака были «едва заметны», слайдеры Density/Opacity/ColorIntensity не влияли на картинку. Дебаг-режим (Pass 0, B&W) работал контрастно, но Pass 1 (цветной реймарч) давал почти прозрачный результат.

### B.2 Диагностическая цепочка

| Шаг | Тест | Результат | Вывод |
|---|---|---|---|
| 1 | Pass 2 = сплошной зелёный (без cloudRT) | ✅ Экран позеленел | Pass 2 исполняется, Blend работает |
| 2 | Pass 1 = сплошной белый (без реймарча) | ✅ Экран побелел | Pass 1 шейдер исполняется, вершинный шейдер жив |
| 3 | Pass 1 = world ray direction как RGB | ✅ Градиент (синий вперёд, зелёный вверх, розовый вниз) | `_Cloud_InvProj`/`_Cloud_ViewToWorld` доходят, `GetWorldRay` корректен |
| 4 | Pass 1 = slab hit test (зелёный/красный) | ✅ Зелёный внизу, красный вверху | `RaySlabIntersection` работает, `_CloudBottomY`/`_CloudTopY` доходят |
| 5 | Pass 1 = coverage/heightFade/density в одной точке | R=есть, G=много, B=мало | Плотность низкая — либо `_DensityMultiplier` не доходит, либо шум слабый |
| 6 | Pass 1 = реймарч с хардкодом Density×8, Opacity=5 | ✅ Облака видны (плоские, ч/б, без depth-test) | Реймарч-цикл работает; проблема в доставке uniform'ов из C# |
| 7 | Pass 1 = реймарч с хардкодом Density×3, Opacity=2 + depth-test | ✅ Облака видны, не перекрывают геометрию | Depth-test работает (`SampleSceneDepth` + `_CameraDepthTexture`) |
| 8 | Переход на `Shader.SetGlobal*` + удаление из Properties | ⏳ Тестируется | Гипотеза: Properties-слот материала «тенит» глобальные uniform'ы |

### B.3 Корневые причины и фиксы

**Проблема 1: `mat.SetFloat`/`mat.SetColor` не доходят до шейдера для части свойств.**
- `_CloudBottomY`, `_CloudTopY`, `_HeightEdgeSoftness`, `_CoverageScale/Threshold` — работают через `mat.SetFloat`
- `_DensityMultiplier`, `_LightAbsorption`, `_CloudOpacity`, `_CloudColorIntensity`, цветовые рампы (`_DayRamp*`, `_SunsetRamp*`) — НЕ работают
- **Фикс:** удалены из Properties-блока шейдера; передаются через `Shader.SetGlobalFloat`/`Shader.SetGlobalColor` в `ApplyProperties`

**Проблема 2: `replace_in_file` оставляет `=======` маркеры конфликтов в .shader и .cs файлах.**
- **Фикс:** все правки шейдера и C# — через `write_to_file` (полная перезапись)

**Проблема 3: cloudRT + Pass 2/3 композит не проверены (отключены для диагностики).**
- Pass 1 сейчас рендерит напрямую в colorTarget (`Blend SrcAlpha OneMinusSrcAlpha`)
- `VolumetricCloudsPass.RecordRenderGraph` — упрощён до одного пасса
- half-res, temporal reprojection, история — отключены, будут возвращены после подтверждения визуала

### B.4 Архитектурные изменения (относительно A.4)

1. **Матрицы:** `UNITY_MATRIX_I_P`/`UNITY_MATRIX_I_V` → `_Cloud_InvProj`/`_Cloud_ViewToWorld` (устанавливаются через `cmd.SetGlobalMatrix` в RenderFunc — в RenderGraph-пассах Unity их не выставляет автоматически)
2. **Properties шейдера:** убраны `_DensityMultiplier`, `_LightAbsorption`, `_CloudOpacity`, `_CloudColorIntensity`, `_DayRampTop/Mid/Bot`, `_SunsetRampTop/Mid/Bot` — теперь global-only через `Shader.SetGlobal*`
3. **Pass 1 Blend:** `Blend One Zero` → `Blend SrcAlpha OneMinusSrcAlpha` (прозрачность вне слаба)
4. **Depth-test:** добавлен в Pass 1 (не рисует облака над геометрией выше `_CloudTopY`)
5. **cloudRT/history:** отключены; Pass 1 → colorTarget напрямую

### B.5 Текущие визуальные проблемы (НЕ решены)

1. **Облака выглядят плоско** — alpha быстро насыщается до 0.99 (один шаг), нет объёма
2. **«Вырезанные куски»** — coverage threshold создаёт бинарные области; между «дырками» нет плавных переходов плотности
3. **Нет плотного слоя сверху** — при взгляде вниз облака = плоскость, а не толстый слой
4. **При спуске внутрь** — всё красится в один тон (alpha=0.99 на первом же шаге, цвет усредняется)
5. **Просвечивает skydome** — в дырках coverage видна обратная сторона неба вместо плотной облачной массы

**Причина:** текущая формула накопления `accumulated.a += stepAbsorption * _CloudOpacity` с `stepAbsorption ≈ 1.0` (плотность × 3 × 0.12 × 3 ≈ высокая) насыщает alpha мгновенно. Нужна перенастройка:
- Уменьшить density boost (сейчас ×3 в `CloudDensity`)
- Увеличить количество шагов с ненулевым вкладом (сейчас early-exit при `accumulated.a >= 0.99`)
- Рассмотреть soft-пороги coverage вместо `smoothstep`
- Возможно — вернуть multi-layer подход (несколько октав с разной плотностью)

### B.6 Следующие шаги

1. Подтвердить что `Shader.SetGlobal*` фикс работает (цветные облака, слайдеры реагируют)
2. Настроить формулу накопления для объёмности
3. Вернуть cloudRT + half-res + temporal reprojection
4. Сравнить с оригинальным VeilRaymarchController — зачем заменяли?
