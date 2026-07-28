# Earth Curvature — Post-Mortem

**Дата:** 2026-07-28
**Задача:** Визуальное искривление горизонта на высоте 2000+ м (сферичность земли / эффект линзы)
**Результат:** ❌ Не работает — откат на `6f2d13d`

---

## Попытка 1: PaniniProjection через Volume (priority=300)

**Файлы:** `EarthCurvatureEffect.cs` (v1), компонент на `DayNightController`

**Архитектура:**
- `EarthCurvatureEffect.Awake()` создаёт дочерний `Volume` (priority=300) с `PaniniProjection`
- `Update()` читает `Camera.main.transform.position.y`, плавно меняет `panini.distance`

**Почему не сработало:**
1. `ThirdPersonCamera.prefab` имел тег `Untagged` → `Camera.main` = null → высота всегда 0
2. Даже после фикса тега (`MainCamera`): Volume stack показывал `distance=0, cropToFit=1` — значит наш Volume не перекрывал DayNightController blend-профили
3. DayNightController создаёт runtime-копии VolumeProfile'ов (day/twilight/night, priority=100) + Temperature (priority=200). Вероятно, в каком-то из профилей был PaniniProjection с default-значениями, который перехватывал управление

**Вывод:** Volume-система URP + DayNightController с runtime-профилями — слишком хрупкая комбинация. Надёжно заинжектить оверрайд поверх динамических профилей не удалось.

---

## Попытка 2: FullScreenPassRendererFeature + Barrel Distortion Shader

**Файлы:** `EarthCurvature.shader`, `EarthCurvature.mat`, `FullScreenPassRendererFeature` в URP Renderer, `EarthCurvatureEffect.cs` (v2)

**Архитектура:**
- `EarthCurvatureEffect.Update()` → `Shader.SetGlobalFloat("_EarthCurvatureStrength", t)`
- `FullScreenPassRendererFeature` (AfterRenderingPostProcessing) → blit с `EarthCurvature.mat`
- Шейдер: бочкообразная дисторсия `uv → uv + strength × delta × r²`

**Почему не сработало:**
1. **Версия 1 шейдера:** использовал `TransformObjectToHClip` для vertex — несовместимо с `DrawProcedural(3)`, который использует `FullScreenPassRendererFeature`
2. **Версия 2 шейдера:** использовал `GetFullScreenTriangleVertexPosition` — функция не существует в URP 17
3. **Версия 3 шейдера:** ручной `SV_VertexID` → позиция треугольника. Скомпилировался, но визуально эффекта нет

**Гипотезы о причине отказа v3:**
- `_EarthCurvatureStrength` global может не доходить до шейдера при использовании MaterialPropertyBlock (RenderFeature использует PropertyBlock для `_BlitTexture`, что может изолировать материал от global-свойств)
- `_BlitScaleBias` (масштаб/смещение UV при dynamic resolution) не используется в шейдере — при нестандартном разрешении UV могут быть смещены
- Возможен конфликт с RenderGraph API (URP 17) — `FullScreenPassRendererFeature` использует RenderGraph, и сам факт применения может быть незаметен
- Сцена: нет террейна/граунда. Есть veil и плавающие острова. Бочкообразная дисторсия от центра экрана может быть субъективно незаметна на таком контенте

---

## Попытка 3: Увеличение maxStrength до 1.0

Просто снятие Range-ограничения. Не помогло — проблема не в силе эффекта, а в том что шейдер не применяется.

---

## Ключевые выводы

1. **Volume-система URP ненадёжна** при наличии динамических runtime-профилей (DayNightController). Priority-хаки не гарантируют победу оверрайда.

2. **FullScreenPassRendererFeature + кастомный шейдер** — правильное направление, но требует:
   - Шейдер, совместимый с `DrawProcedural(3)` и MaterialPropertyBlock
   - Использование `_BlitScaleBias` для корректных UV
   - Прямой `material.SetFloat` вместо `Shader.SetGlobalFloat` (чтобы обойти изоляцию PropertyBlock)

3. **Специфика сцены:** отсутствие горизонтальной поверхности затрудняет визуальную оценку. Эффект нужно тестировать на тестовой сцене с явной линией горизонта.

---

## Идеи на будущее

- **Прямой `material.SetFloat`** вместо global — проверить, доходит ли параметр
- **RenderFeature с собственным MaterialPropertyBlock** — полный контроль над параметрами
- **Lens Distortion (Volume)** — попробовать вместо PaniniProjection, он визуально заметнее
- **ShaderGraph Fullscreen Shader** — генерирует гарантированно совместимый шейдер
- **Тестовая сцена** с контрастным горизонтом для отладки
- **RenderPipelineManager.endContextRendering** callback — альтернатива RenderFeature
