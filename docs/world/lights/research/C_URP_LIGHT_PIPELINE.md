# Подотчёт C — Unity URP 17.5: световой пайплайн и много point lights

**Сабагент:** general (web) · **Дата:** 2026-11-01 · **Задача:** web-ресерч по URP 17.5 и много точечных источников.

## 1. Forward vs Forward+ vs Deferred

- **Forward**: один проход на объект; главный свет + до N дополнительных на объект (N задаётся в URP Asset, `Additional Lights > Per Object Limit`; в проекте 4; максимум обычно 8). Доп. света сортируются по интенсивности в пивоте объекта; если на объект влияет больше N, берутся только топ-N. Лимиты на камеру: mobile/WebGL 32 доп., desktop/console 256. Поддерживает per-vertex additional lights, MSAA, camera stacking.
- **Forward+**: убирает per-object лимит — света шейдятся по тайлам/кластерам, объект может быть затронут любым числом видимых светов. Лимит на камеру остаётся: до 256 на desktop/console, 32 на mobile; Forward+ учитывает main light и additional lights одинаково (практически «один слот меньше»). Принудительно per-pixel для доп. светов; игнорирует `Per Object Limit`, blending reflection probes всегда включён. Лучший выбор для сцен с большим числом point/spot, особенно при GPU Resident Drawer.
- **Deferred / Deferred+**: для непрозрачных — фактически неограниченное число светов на объект; для прозрачных/forward-шейдеров сохраняется forward-лимит (9 в Forward-пути). Лимит на камеру такой же (до 257 с main light на desktop). Цена: G-buffer проходы, больше памяти/полосы, нет MSAA, хуже с Rendering Layers/terrain blending; Mixed/Subtractive/Shadowmask дают лишнюю нагрузку на GPU.

Источники: docs.unity.com URP rendering paths comparison; light limits in URP; URP 14.0 forward-plus manual.

## 2. Engine-level culling светов

- `ScriptableRenderContext.Cull` выполняет frustum culling камеры и формирует `CullingResults.visibleLights` — массив видимых светов, отсортированный по дистанции до камеры.
- URP строит per-object списки светов: job куллит света по объектам и сортирует их по интенсивности в пивоте объекта; индексы хранятся в light index map (`GetLightIndexMap`/`SetLightIndexMap`).
- Per-camera лимит: если видимых светов больше лимита (256/32), Unity отключает часть светов; это может отключить shadow-casting light и вызвать flicker теней.
- Range определяет затухание; фактическая видимость ограничивается frustum камеры и per-camera/per-object лимитами.
- `LightData.maxPerObjectAdditionalLightsCount` влияет только на Forward.

Источники: URP shadows troubleshooting; SRP Culling (URPLearn); LightData API 17.5.

## 3. Стоимость point light (CPU + GPU)

- **CPU:** culling светов по камере и per-object classification; больше видимых светов → больше работы в `Cull`/jobs, больше пересчётов индексов; влияет на батчинг (разные наборы per-object light indices разбивают SRP/инстанс-батчи); частое вкл/выкл светов вокруг игрока вызывает churn.
- **GPU:** в URP освещение считается в одном проходе на объект; стоимость растёт с числом светов, влияющих на пиксель/объект; point light — attenuation по дистанции; без теней основная нагрузка — shading; тени от point light особенно дороги (6 граней куба), но у нас они отключены; overdraw умножает стоимость.

Источники: Unity performance optimization high-end graphics; reduce draw calls in URP.

## 4. LightRenderMode в URP

- В URP свойство `Render Mode` (Auto / Important / Not Important) **не даёт смешивать** per-pixel и per-vertex света на уровне отдельных ламп: URP применяет режим Additional Lights ко всем дополнительным светам сразу (Per Pixel или Per Vertex в URP Asset).
- Issue Tracker: «URP Lighting doesn't support different RenderModes»; рекомендация — Forward+ для многих светов.
- В Forward path `Important` может влиять на приоритет при ограничении per-object, но не гарантирует per-pixel для конкретной лампы.
- На практике: URP Asset Additional Lights = Per Pixel (качество) или Per Vertex (скорость), плюс Forward+ для снятия per-object лимита.

Источники: Issue Tracker 15065; Light.renderMode API.

## 5. Light probes vs realtime point lights

- Light probes хранят baked indirect/diffuse освещение для динамических объектов; дешевле realtime-светов, но не дают динамических теней/self-shadowing и реагируют на baked/Mixed света.
- Realtime point lights нужны для динамических локальных акцентов (фонари игрока, интерактивные источники).
- В Unity 6 APV (Adaptive Probe Volumes) даёт более качественное per-pixel indirect, streaming и сценарии вкл/выкл.
- Практика: статическое окружение → baked/Mixed + probes/APV; realtime point lights — только там, где нужно динамическое освещение; уменьшать range/intensity и отключать тени.

Источники: LightProbes manual; Unity 6 GI blog; ARM light probes.

## 6. GPU Resident Drawer / GPU occlusion и света

- GRD работает только с Forward+/Deferred+, compute-capable GPU, SRP Batcher; использует BatchRendererGroup/инстансинг, снижает draw calls.
- GPU occlusion culling использует depth (камеры и света) для отсечения объектов; особенно полезно при большом числе одинаковых mesh и окклюзии.
- Взаимодействие со светами: GRD/occlusion уменьшают работу CPU/GPU по объектам, но **не уменьшают число светов** и per-object light classification; light count всё равно важен.
- Известный баг с `Culling Mask` и shadow casters при GRD исправлен в Unity 6/7.

Источники: URP gpu-resident-drawer manual; GPU culling profiling; Issue Tracker 9149.

## 7. Встроенные ручки: Culling Mask, Range, Intensity, Render Mode, тени

- `Light.cullingMask` (legacy Layers): исключает объекты по слоям; снижает число объектов, на которые влияет свет.
- `Rendering Layers` (URP): более гибкая фильтрация свет→рендерер; в Forward влияние на производительность незначительно, но при >8/16/24 слоях растёт из-за доп. каналов.
- `Range`: главный рычаг — делайте радиус минимально достаточным; attenuation плавно падает к нулю.
- `Intensity`: влияет на сортировку светов по объектам; слишком много одинаково ярких светов ухудшает предсказуемость top-N.
- `Render Mode`: Auto/Important/Not Important; в URP не даёт per-light vertex/pixel, но может влиять на приоритет.
- Тени: отключать для point lights (особенно на mobile); если нужны — низкая resolution, малый distance, shadowmask только где критично.
- Дополнительно: `Light.enabled` для дистанционного вкл/выкл; `Light.mode` (Realtime/Mixed/Baked); APV/probes для окружения.

## Практические рекомендации

1. Переключить рендер-путь на **Forward+** (или Deferred+), чтобы снять per-object лимит 4 и уменьшить разрывы батчей.
2. Сократить `Range` и использовать `Culling Mask`/Rendering Layers, чтобы света не влияли на дальние/ненужные объекты.
3. Ввести гистерезис при вкл/выкл: включать свет по дистанции с запасом, выключать с задержкой; менять `enabled`/`intensity`, а не мгновенно тогглить.
4. Статическое окружение перевести на baked/Mixed + light probes/APV; realtime point lights оставить только для динамических акцентов.
5. Тени у point lights держать выключенными (уже так).
6. Включить **SRP Batcher**, **GPU Resident Drawer** и **GPU occlusion** (только Forward+/Deferred+) для снижения draw calls/overdraw.
7. При необходимости ограничить число видимых светов через URP Config package (`MAX_VISIBLE_LIGHT_COUNT_*`).
8. Проверить, что материалы совместимы с SRP Batcher/GRD (без MaterialPropertyBlock, ≤128 материалов).
9. Профилировать: Profiler, Frame Debugger; следить за `visibleLights.Length` и per-object light indices.

*Источник: подотчёт сабагента C.*