# CLOUD Rendering — Deep Analysis: NEAR CLOUD Mesh Overlap

**Дата:** 2 июня 2026 | **Версия:** 2.0 / Stage 2.5 | **Status:** 🟡 Analysis Complete — Awaiting Decision
**Автор:** Mavis (Mavis) + Lead Programmer review
**Связано с:** [ADR-Cloud-001-Rendering-Architecture.md](../1.0/ADR-Cloud-001-Rendering-Architecture.md) (Accepted, 3 мая 2026)

---

## Executive Summary

**Проблема не баг, а архитектурное ограничение** mesh-based + alpha-blend подхода, который сейчас используется в `NearCloudRenderer`. Mesh+alpha **фундаментально не может** дать "слияние" пуфов, потому что альфа-блендинг работает в screen-space попиксельно, а не в 3D, и `Cull Off` + `ZWrite Off` показывают "внутренности" каждого меша.

**Прямой ответ на вопрос "можно ли морфить меши?":** формально да, технически **неправильный инструмент** — даёт твёрдую поверхность вместо пушистого облака, и поверх всё равно придётся накладывать прозрачность (мыльный пузырь возвращается). Runtime CSG/marching cubes на 280+ движущихся пуфах в кадре — дорого и не решает union в 2D-визуале.

**Прямой ответ на гипотезу "полностью непрозрачный шейдер?":** нет. Opaque + ZWrite = 2 непрозрачных шара, где внутренний **перекрывает** внешний (он ближе к камере). Объединённого силуэта не получится, мягкость потеряется.

**Два метода реально решают задачу:**

| # | Метод | Решает union? | Стоимость | Подходит для |
|---|---|---|---|---|
| **A** | Global screen-space raymarch (fullscreen quad, density = Σ puff_i) | **Да — математически** | ~1-2 мс / 200 пуфов, 1 drawcall | Lower + Middle (NEAR) |
| **C** | Screen-space billboard metaballs (camera-facing quad per puff) | **Да в 2D** (нет понятия "внутри") | ~0.1-0.3 мс / 300 quads | Все слои (особенно Upper, Middle) |

**Рекомендация (совпадает с ADR-Cloud-001):** гибрид — Lower → raymarch, Middle → billboards, Upper → billboards. Cumulonimbus → A + VFX Graph.

**Phase 0 (без кода):** подтвердить решение с командой. Далее — Phase 1 (5 мин, win): заменить mesh = quad в `MeshEntries` для Upper-слоя, посмотреть результат. Phase 2 (1-2 недели): global raymarch для Lower.

---

## 1. Problem Statement

### 1.1 Что наблюдает пользователь

> "Сейчас NEAR CLOUD работают нормально, но мне не нравится как выглядит итоговое перекрытие мешей. Когда у нас есть 2 меша и 1 соединяется с другим — мы видим просто 2 меша с прозрачностью и 1 в другом, еще хуже когда 1 меш прям в другом находится — мы видим и 1 и внутри другой. Это не свойственно облакам — больше схоже с мыльными пузырями."

### 1.2 Желаемое поведение

- **Случай A:** пуф 1 полностью внутри пуфа 2 → видно **только внешний пуф 2**, без следов внутреннего.
- **Случай B:** пуф 1 частично пересекается с пуфом 2 → видно **объединённую форму**, без внутренних перегородок.
- Мягкость/полупрозрачность должна сохраниться (это "облачность" ассета).

### 1.3 Можно ли решить на уровне мешей?

| Mesh-level подход | Можно? | Решает? | Почему нет |
|---|---|---|---|
| **CSG union (boolean operations)** | Да, есть библиотеки (ProBuilder CSG, ParagonCSG, manifold3d) | ❌ | Получаем твёрдый меш, не облако. Надо заново лепить шум, fresnel, полупрозрачность → возврат к исходной проблеме |
| **Marching Cubes per-frame (CPU)** | Да, 3-5 мс CPU | ❌ | То же: твёрдая изо-поверхность, теряем мягкость, + пересборка на каждом wind-step |
| **GPU Marching Cubes (compute, 64³)** | Да, ~1-2 мс compute | ❌ | Тот же результат: полигональный меш. Надо накладывать noise + transparent → soap bubble |
| **Runtime mesh morphing (blend shapes)** | Технически да | ❌ | Blend shapes интерполируют **вершины** одной формы в другую. Не объединяют две произвольные формы. Не масштабируется на 280+ пуфов |

**Вывод:** на уровне мешей задача **не решается**. Меш — это твёрдая поверхность, а облако — это поле плотности. Разные абстракции.

---

## 2. Root Cause — почему текущий код даёт "soap bubble"

### 2.1 Текущая реализация

**`NearCloudRenderer.cs`** (`Assets/_Project/Scripts/Core/`):
- 3 экземпляра: Upper (80), Middle (120), Lower (80) — итого ~280 пуфов.
- `Graphics.DrawMeshInstanced` — каждый пуф это 3D меш (default: sphere, варианты через `MeshEntries[]`).
- Per-frame: ветер двигает матрицы, рециклинг пуфов в 10 км от игрока.

**`CloudInstanced.shader`** (`Assets/_Project/Art/Shaders/`, используется в `TEST_NEAR_INSTANCED.mat`):

```hlsl
Tags { "RenderType"="Transparent" "Queue"="Transparent" }
Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off
Cull Off                  // <-- оба sides рендерятся
...
alpha = alphaBase * combinedNoise * edgeFade;   // центр прозрачный
```

### 2.2 Три причины "soap bubble"

1. **Alpha blend работает в screen-space, не в 3D.**
   - Каждый пиксель = `mix(backdrop, A_front, A_alpha)`, потом `mix(..., B_front, B_alpha)`.
   - На пиксель получаем **4 наложенных полупрозрачных слоя** (backdrop + A_front + A_back + B_front + B_back), а не 1 объединённую форму.
   - Surface-level stacking ≠ shape-level union.

2. **`Cull Off` + `ZWrite Off` = видны внутренности.**
   - У сферы есть near-side и back-side. Оба рендерятся.
   - Без ZWrite они не отсекают друг друга — видно всю "луковицу".
   - Вот тебе и мыльный пузырь.

3. **`alphaBase ≈ 0.346` + edge-fade = see-through center.**
   - Центр пуфа полупрозрачный. Дизайн под "облако на фоне неба" — нормально.
   - Для "облако-пересекает-облако" — катастрофа: видно всё что за ним (включая внутренности другого пуфа).

### 2.3 Почему "просто увеличить alpha" не поможет

- `alpha = 0.95` + ZWrite Off → видно front + back of outer + front of inner = 3 непрозрачных слоя, всё ещё луковица.
- `alpha = 1.0` + ZWrite On → 2 непрозрачных шара. Внутренний пуф **перекрывает** внешний (он ближе к камере). Силуэт "объединённой формы" не получится. Мягкость потеряна.

### 2.4 Почему opaque + ZWrite не даёт union

Это **стандартное заблуждение** — "если шарик непрозрачный, то union выглядит правильно". На самом деле:
- 2 непрозрачных шара → 2 непрозрачных силуэта, наложенных в Z.
- Тот, что ближе к камере, полностью перекрывает дальний.
- Если хочется, чтобы дальний **не мешал** — нужна Z-логика "спрятать всё, что внутри другого". Это вручную реализованный CSG, не масштабируется.

---

## 3. Все рассмотренные подходы

| # | Подход | Суть | Решает union? | Перф (1080p, 200 пуфов) | MMO-scale fit |
|---|---|---|---|---|---|
| **A** | **Global screen-space raymarch** | Fullscreen quad, raymarch через `density(p) = Σ metaball_i(p)`, alpha по Beer-Lambert | **Да** | 1-2 мс, 1 drawcall | **Lower+Middle** ✅ |
| **B** | **Per-puff raymarch** | AABB-бокс + локальный raymarch FBM-поля для каждого пуфа | ❌ Каждый пуф независим → то же наложение | 5-10 мс, N drawcall | ❌ |
| **C** | **Screen-space billboard metaballs** | Camera-facing quad per puff, мягкий радиальный альфа | **Да в 2D** (нет "внутри" у плоскости) | 0.1-0.3 мс, 1 instanced | **Все слои** ✅ |
| **D** | **Mesh CSG (boolean union)** | Объединяем меши пуфов в один твёрдый | Силуэт да, мягкость нет | 3-5 мс CPU/frame | ❌ для динамики |
| **E** | **GPU Marching Cubes (compute)** | Извлечение полигональной изо-поверхности | Силуэт да, мягкость нет | 1-2 мс compute, 64³ | Возможно, избыточно |
| **F** | **Alpha-to-Coverage + MSAA** | Dithered alpha, отбрасывает фрагменты | ❌ Внутренний пуф проступает шумом | Бесплатно | ❌ |
| **G** | **Cull Front + ZWrite** | Только back-faces, ZWrite прячет inner | Inner исчезает, **но видно внутренности outer** ("пустая скорлупа") | Бесплатно | ❌ |
| **H** | **Soft particles (depth fade)** | Альфа затухает у depth-buffer пересечений | Косметика, не решает union | Бесплатно | Хак |

**Только A и C реально решают задачу.**

---

## 4. Подход A: Global Screen-Space Raymarch (рекомендован)

### 4.1 Почему это единственный метод, который решает задачу математически

В фрагментном шейдере для каждого пикселя:
1. Строим луч `ro + t·rd` от камеры через пиксель.
2. Шагаем по лучу. В каждой точке считаем `density(p) = Σ_i smoothSphere(p, center_i, radius_i)` — сумма вкладов всех пуфов.
3. Когда density > threshold — луч вошёл в облако. Аккумулируем прозрачность по Beer-Lambert: `alpha = 1 - exp(-density * stepSize * k)`.
4. Когда alpha насытился — break (early exit).

**На сценариях пользователя:**

- **Пуф A внутри пуфа B:** луч входит там, где density = A+B. Силуэт = там, где density > threshold. Внутренний A **повышает плотность** в области B. Визуально — единая форма B, возможно чуть плотнее в зоне A. **Внутренней структуры A не видно.** ✅
- **Два пуфа частично пересекаются:** density суммируется в зоне пересечения. Силуэт = объединённая капля без внутренних перегородок. ✅

### 4.2 Технические нюансы под URP

- **`ScriptableRenderFeature` + `ScriptableRenderPass`**, инжектится после opaques, до transparents.
  - `RenderPassEvent.BeforeRenderingTransparents` (рисуется до UI, после opaques мира).
  - Или `AfterRenderingSkybox` (если хочешь рисовать до всех opaques, как горизонт).
- **Данные пуфов:** `StructuredBuffer<float4>` (xyz = center, w = radius) + опционально второй буфер для цвета/плотности. Cap на среднебюджетной GPU: 256–512 пуфов на проход.
- **Lighting:** wrapped NdotL (тёплый сверху, холодный снизу) + FBM-detail. Можно подсмотреть в `VeilRaymarch.shader` — он уже умеет noise + FBM + scroll + lightning.
- **Ветер:** скроллить координаты noise на `_WindDir * _WindSpeed * _Time.y` — естественное движение без обновления данных пуфов.
- **Tипa absorption:** упрощённо `transmittance = exp(-density * stepSize * k)`, финальный цвет = `albedo * sunLight * (1 - transmittance) + ambient`.
- **Adaptive step count:** ранний exit при `transmittance < 0.01` → среднее 16-20 шагов вместо 32.

### 4.3 Стоимость (rough estimate, RTX 3060 / RX 6600, 1080p)

| Шаги | Пуфов | Стоимость |
|---|---|---|
| 32 | 200 | 0.8-1.5 мс |
| 24 | 128 | 0.5-0.8 мс |
| 16 (adaptive) | 200 | 0.4-0.7 мс |

Укладывается в бюджет < 3 мс из ADR-Cloud-001.

### 4.4 Сильные / слабые стороны

**Сильные:**
- Индустриальный стандарт: MSFS, HZD, No Man's Sky, BotW. И наш `VeilRaymarch` уже использует эту технику.
- **1 drawcall** на весь lower layer (заменяет 80+ `DrawMeshInstanced` вызовов).
- Тот же шейдер для storm/cumulus (другая noise-функция + extra density).
- Точно соответствует ADR-Cloud-001.

**Слабые:**
- Без shadow map нет 3D-теней от солнца (решается cheap fake-shadow через self-sample в направлении sun в точке входа).
- На low-end GPU может не влезть → нужен LOD/fallback.
- Сложнее шейдер, итеративная GPU-оптимизация.

### 4.5 Prior art в проекте

**`VeilRaymarch.shader`** (`Assets/_Project/Shaders/`) — уже работающий пример:
- Full-screen volumetric raymarch (для горизонтной завесы).
- 8-16 raymarch steps, FBM noise, Beer-Lambert absorption, height gradient.
- Управляется через `VeilRaymarchBlit.cs` (`Assets/_Project/Scripts/World/Clouds/`).
- Используется через `VeilSystem.cs` + `VeilRaymarchMeshController.cs`.

**Это готовый skeleton.** Стоит рефакторить: вынести общий raymarch framework, переиспользовать FBM + lighting + scroll для cloud volume. Дублировать шейдер не нужно.

---

## 5. Подход C: Screen-Space Billboard Metaballs

### 5.1 Идея

Каждый пуф = camera-facing quad. В фрагментном шейдере — мягкий радиальный альфа-градиент (1 - smoothstep) + FBM noise + rim. Per-instance: position, radius, color через `MaterialPropertyBlock` или instanced properties.

### 5.2 Почему union возникает "бесплатно"

- В 2D screen space **нет понятия "внутри"** — у плоского квадрата нет объёма, только 2D форма.
- Два перекрывающихся билборда через alpha-blend = **визуально объединённая клякса**. Внутреннего разделения нет.
- Один билборд под другим — не "внутри", а "за", и его просто не видно.

**Решает ровно 95% проблемы "soap bubble"** для cluster overlap.

### 5.3 Технические нюансы

- Quad всегда смотрит в камеру: в vertex shader строим billboard из 2 треугольников (4 вершины), используя обратную `viewMatrix` камеры. URP 17 поддерживает это нативно.
- Шейдер: можно переиспользовать **существующий** `CloudInstanced.shader`, поменяв только mesh (sphere → quad) и убрав vertex displacement (он не нужен на плоскости).
- 1 instanced drawcall, до 1023 пуфов на batch (или больше через `RenderMeshIndirect`).

### 5.4 Стоимость

280 quads, 1 instanced drawcall, ~0.1-0.3 мс GPU. Ничтожно.

### 5.5 Сильные / слабые стороны

**Сильные:**
- Очень дёшево. URP-native. Не ломает существующий пайплайн.
- Полностью решает cluster-overlap проблему.
- Масштабируется на тысячи пуфов.
- Можно сделать **за час** (заменить mesh = quad в `MeshEntries`).

**Слабые:**
- Нет 3D-параллакса — cluster выглядит "как нарисованный" при облёте.
- Нет внутренней 3D-структуры (можно имитировать parallax-mapping, но фейк).
- Fly-through не работает: облако "выскакивает" в камеру как плоскость.

### 5.6 Где подходит

- **Upper (6000-8000 м):** параллакс не виден на той дистанции, идеально.
- **Middle (3000-5000 м):** приемлемый компромисс, можно потом апгрейднуть до A.
- **Lower (1500-3000 м):** для дешёвого fallback / low-end preset.
- **Distant (5000-15000 м):** уже используется impostor-подход, можно унифицировать.

---

## 6. Гибридная архитектура (рекомендация)

Точно соответствует **ADR-Cloud-001** (Accepted, 3 мая 2026).

| Слой | Высота | Подход | Обоснование |
|---|---|---|---|
| **Upper** | 6000-8000 м | **C — billboard metaballs** | На той дистанции параллакс не виден, юзер видит плоскую красивую плёнку. Дёшево. |
| **Middle** | 3000-5000 м | **C — billboard + soft-3D parallax** (опц.) | Компромисс: дёшево, но живее чем pure 2D. Можно позже апгрейднуть до A. |
| **Lower** | 1500-3000 м (**NEAR**) | **A — global raymarch volume** | Героический слой, юзер в нём летает. Нужна настоящая 3D-объёмность и правильное слияние пуфов. |
| **Cumulonimbus / Storms** | 2000-6000 м | **A + VFX Graph** для lightning | По ADR. |

**Результат:**
- **Правильный визуал** там, где юзер это видит (lower — объёмно, мягко, пуфы сливаются).
- **Дёшево там, где разницу не отличить** (upper — спрайты).
- **Бюджет < 3 мс** на весь cloud pipeline (из ADR).

---

## 7. Phased Migration Plan

### Phase 0: Подтверждение решения (5 мин, без кода)
- Проговорить с командой: ADR-001 + данный анализ = план.
- Решить: начинаем с billboard quick-win (фаза 1) или сразу raymarch (фаза 2).

### Phase 1: Billboard quick-win для Upper (1-2 дня)
- **Scope:** заменить mesh = quad в `MeshEntries` для Upper-слоя.
- **Что меняется:** только asset reference в инспекторе, шейдер тот же.
- **Что НЕ меняется:** `NearCloudRenderer.cs`, материал, шейдер.
- **Цель:** визуально подтвердить, что 2D-union работает (95% проблемы решено).
- **Verification:**
  1. Open Unity → Console → 0 errors.
  2. Play Mode → подлететь к Upper-облакам, посмотреть overlap.
  3. Сравнить с Middle/Lower (которые пока mesh-based) — видна разница.

### Phase 2: Global raymarch для Lower (1-2 недели)
- **Скелет:**
  - `NearCloudRaymarchFeature.cs` — `ScriptableRenderFeature`.
  - `NearCloudRaymarchPass.cs` — `ScriptableRenderPass` с fullscreen quad.
  - `NearCloudRaymarch.shader` — raymarch, 12 шагов, hard-sphere density (без шума сначала), no lighting.
- **Что переиспользуем:** `VeilRaymarch.shader` (FBM, lighting, scroll) + `VeilSystem.cs` (pass registration).
- **Что НЕ меняется:** `NearCloudRenderer.cs` (он продолжает жить для Middle/Upper), `CloudManager.cs`.
- **Шаги:**
  1. Hard-sphere density (density = Σ max(0, 1 - dist/radius)). Подтвердить union.
  2. FBM noise внутри каждой сферы (variation плотности).
  3. Lighting (wrapped NdotL, warm top / cool bottom).
  4. Ambient + atmospheric blending.
  5. Wind scroll.
  6. Self-shadow в направлении солнца (fake shadow).
- **Verification:**
  1. Compile check (0 errors).
  2. Play Mode → подлететь к Lower-слою. 2 пуфа внутри друг друга → виден только outer.
  3. Частично пересекающиеся пуфы → единый силуэт.
  4. Wind дует → noise scroll работает.
  5. Frame time (Window → Analysis → Profiler) → raymarch pass < 1.5 мс.

### Phase 3: Adaptive LOD / fallback
- Low preset → billboards для всех слоёв.
- Mid preset → 16 raymarch steps для Lower.
- High preset → 32 steps + self-shadow для Lower.
- Editor menu: `[CloudManager] SetQuality(preset)`.

### Phase 4: Migration Middle → A (опционально)
- Если бюджет позволяет, заменить Middle billboard → raymarch (тот же feature, только другой буфер пуфов).

---

## 8. URP-Specific Gotchas

### 8.1 Render injection point
- `BeforeRenderingTransparents` — после мира, до UI. **Стандартный выбор для cloud volume.**
- `AfterRenderingSkybox` — до всех opaques. Если хочется, чтобы облака были "за" миром (как дальний горизонт). **Не наш случай** — мы хотим видеть облака перед миром.

### 8.2 Fog blending
- Raymarch volume должен правильно смешиваться с URP-туманом (sky color, distance fog).
- Иначе облака будут "инопланетянами" — не сливаются с горизонтом.
- Решение: в фрагменте читать `unity_FogColor` (или `mix(unity_FogColor, cloudColor, alpha)`) и блендить финальный результат с fog по расстоянию.
- `VeilRaymarch` это уже решает — переиспользовать.

### 8.3 StructuredBuffer cap
- 256-512 пуфов на проход (зависит от GPU). У нас lower+middle ≈ 200 → влезает с запасом.
- Если пуфов больше — tile по XZ-плоскости, несколько проходов с разными `cbuffer` (start index + count).

### 8.4 Wind consistency
- Сервер-authoritative wind → клиент рендерит, скроллит noise UVs в шейдере.
- **Не нужно обновлять матрицы пуфов per-frame** — ветер уже встроен в noise.
- Это снижает CPU cost и количество network events.

### 8.5 Scene streaming
- При 24-scene streaming надо пересобирать список пуфов на enter/exit scene.
- `CloudManager.RegenerateAllClouds()` уже умеет — расширяй.
- Raymarch pass подписывается на `OnEnable/OnDisable` scene callback, перезагружает `StructuredBuffer`.

### 8.6 Cumulonimbus / storm integration
- Шейдер raymarch параметризуется (fbm octaves, density multiplier) — storm использует те же пуфы + extra density.
- Lightning → инжектится как отдельный SDF-импульс, проще чем на mesh-облаках.

### 8.7 Cull mode для billboards
- Quad → `Cull Off` (плоскость, обе стороны).
- Для raymarch fullscreen quad → `Cull Off` тоже (чтобы работало на любой flip).

---

## 9. Связь с существующими документами и кодом

### 9.1 Связанные документы
- [`ADR-Cloud-001-Rendering-Architecture.md`](../1.0/ADR-Cloud-001-Rendering-Architecture.md) — принятое решение (3 мая 2026), нижний/средний/верхний слои. **Данный анализ валидирует и конкретизирует ADR.** Никаких противоречий.
- [`CLOUD_VISUAL_DESIGN.md`](../1.0/CLOUD_VISUAL_DESIGN.md) — визуальный язык (Sci-Fi + Ghibli, мягкие облака, rim glow, morph). Сохраняется.
- [`CLOUD_TECHNICAL_SUMMARY.md`](../1.0/CLOUD_TECHNICAL_SUMMARY.md) — v0.4 архитектура (3 layers, ~280 пуфов). Не меняется.
- [`CLOUD_IMPLEMENTATION_PLAN.md`](../1.0/CLOUD_IMPLEMENTATION_PLAN.md) — v0.4 phased plan. Будет обновлён после Phase 1.

### 9.2 Связанный код
- `Assets/_Project/Scripts/Core/NearCloudRenderer.cs` — текущая реализация (mesh-based). **Сохраняется** для Middle/Upper. **Phase 2 заменит** Lower через raymarch pass.
- `Assets/_Project/Scripts/Core/CloudManager.cs` — оркестратор. Расширяется для raymarch pass registration.
- `Assets/_Project/Scripts/World/Clouds/VeilSystem.cs` + `VeilRaymarchBlit.cs` + `VeilRaymarchMeshController.cs` — приоритетная reference implementation для raymarch.
- `Assets/_Project/Art/Shaders/CloudInstanced.shader` — текущий cloud шейдер. Сохраняется для Middle/Upper (Phase 1 переиспользуется для billboard).
- `Assets/_Project/Shaders/VeilRaymarch.shader` — reference для raymarch подхода. **Будет рефакториться** в общий framework.

### 9.3 Что НЕ меняется
- `WindManager.cs` — сервер-authoritative wind. Уже работает.
- Сетевая модель (server-authoritative, ~58 B/s) — не меняется.
- Scene streaming — не меняется (только расширяется).
- Cumulonimbus / StormController — не меняется в Phase 1-2.

---

## 10. Open Questions / Decisions Needed

| # | Вопрос | Рекомендация |
|---|---|---|
| 1 | Начинаем с Phase 1 (billboard) или сразу Phase 2 (raymarch)? | **Phase 1 → Phase 2** (быстрый визуальный win, потом правильное решение). |
| 2 | Middle оставляем billboard (Phase 1) или сразу raymarch? | Billboard в Phase 1, raymarch — Phase 4 (опционально). |
| 3 | Quality preset structure? | Low (всё billboard), Mid (Lower raymarch 16 steps), High (Lower raymarch 32 steps + self-shadow). |
| 4 | Self-shadow в raymarch — нужно ли? | Да, но как fake-shadow (single sample в направлении sun), не real shadow map. Дёшево, даёт правильный свет. |
| 5 | Как унифицировать VeilRaymarch и будущий CloudRaymarch? | Вынести общий HLSL include: `RaymarchUtils.hlsl` с FBM, lighting, absorption, scroll. Дублировать шейдер не надо. |
| 6 | Запас на тестирование? | Phase 1: 1-2 дня (с визуальной приёмкой). Phase 2: 1-2 недели (с GPU-профилированием). |

---

## 11. Риски

| Риск | Вероятность | Митигация |
|---|---|---|
| GPU-перфоманс хуже оценки | Средняя | Adaptive step count, LOD, fallback на billboards для low-end |
| Lighting выглядит "плоско" на raymarch | Низкая | FBM detail + Henyey-Greenstein phase function, wrapped NdotL |
| Fog-blending выглядит неестественно | Низкая | Переиспользовать формулу из `VeilRaymarch.shader` |
| Wind scroll не консистентен с server | Низкая | Server wind уже синхронизирован; добавить scroll-параметр в MaterialPropertyBlock |
| Phase 1 (billboard) ухудшит parallax | Низкая для Upper, средняя для Middle | Это acceptable trade-off для прототипа; Phase 4 улучшит |

---

## 12. Метрики успеха

| Метрика | Текущее | Цель (после Phase 2) |
|---|---|---|
| Cluster overlap визуал | "Soap bubble" — видно 2+ поверхности | Единый силуэт |
| Inner puff визуал | Виден отдельно | Не виден (поглощён outer) |
| Cloud GPU time | ~0.3-0.5 мс (instanced) | < 1.5 мс (Lower raymarch) + 0.2 мс (Middle+Upper billboard) |
| Drawcalls (clouds) | 3-9 (instanced) | 1-2 (1 raymarch + 1 instanced billboard) |
| Softness / fluff factor | Средняя (alpha-blend) | Высокая (volumetric) |
| Fly-through experience | Mesh pop-in | Плавный gradient вход/выход |

---

## 13. Заключение

**Mesh+alpha не виноват как код — он виноват как архитектурный выбор.** Заменив на raymarch (для NEAR) и billboards (для остального), мы:
- Решаем визуальную проблему юзера (union + inner-hidden).
- Снижаем drawcalls (3-9 → 1-2).
- Снижаем mesh count на 80 (Lower: 80 мешей → 1 fullscreen quad).
- Сохраняем мягкость / пушистость (volumetric nature).
- Укладываемся в < 3 мс GPU budget из ADR-001.
- Переиспользуем существующий `VeilRaymarch` framework.

**Главное решение, которое нужно принять:** согласиться с гибридным подходом A+C и начать с Phase 1 (быстрый billboard win) перед Phase 2 (raymarch для Lower).

---

**Автор:** Mavis (Mavis) | **Дата:** 2 июня 2026 | **Готов к ревью Lead Programmer / Technical Director**
