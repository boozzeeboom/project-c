# Итерации Cloud System 3.0 — Changelog

> **Итоговый статус:** `STATUS.md`

---

## T-CLOUD48 — 2026-11-19
**Статус:** реализовано, визуальная приёмка, F8/F9 и GPU-замеры — NOT RUN.
Шестой этап Clouds v4: временная стабилизация volumetric clouds. Pass `VolumetricClouds_History` перестал быть заглушкой: два history-буфера ping-pong (ARGBHalf, размер как у `cloudRT` — 0.5 при `HalfResRender`) импортируются в RenderGraph; проход репроецирует середину облачного слоя через `_PrevViewProj` в UV прошлого кадра, семплирует историю bilinear, сжимает её 3×3 neighborhood clamp текущего кадра и смешивает `lerp(current, clampedHistory, _TemporalBlend)`. Никакого Catmull-Rom нет — базовый clamp + lerp. Новые настройки: `TemporalBlend = 0.85`, `TemporalTeleportResetDistance = 100`. Обязательные сбросы истории (на кадре сброса `_TemporalBlend` принудительно 0, шейдер не трогает историю): изменение `CumulativeRebaseOffset`/`CumulativeRebaseFrame` (F8, F9, авторебейз), скачок позиции камеры больше `TemporalTeleportResetDistance` (телепорт/респавн), перевыделение history-RT (смена размера/формата экрана), выключенный temporal на прошлом кадре. Порядок важен: сброс выполняется до `ApplyProperties`, иначе кадр после F8 смешался бы со старой матрицей. Composite всегда читает history-write при включённом temporal, иначе `cloudRT`. Через anklebreaker MCP подтверждены загрузка новой C#-сборки, Console — 0 errors, `Shader error = 0` в `Logs/Editor.log`; `Temporal=True; Blend=0.85; TeleportReset=100`, пользовательские Lighting/Shape/Detail/Stable и HalfRes/DepthAware сохранены. `ProjectC_URP_Renderer.asset` не включается в коммит этапа.

## T-CLOUD47 — 2026-11-19
**Статус:** реализовано, визуальная приёмка, F8/F9 и GPU-замеры — NOT RUN.
Пятый этап Clouds v4: raymarch переведён с прямой записи в `colorTarget` на отдельную RGBA16F cloud-текстуру. При `HalfResRender = true` используется масштаб 0.5 по каждой оси, при выключении — full-res через тот же composite для сравнения. Полноразмерный Pass 2 выполняет четырёхточечный bilateral upscale с пространственными весами и относительной eye-depth; при нулевом весе используется ближайший сэмпл. `DepthAwareUpscale = true`, `DepthUpscaleSigma = 0.03`; при отключении depth-aware остаётся обычный bilinear. Footprint T-CLOUD46 учитывает удвоенный угловой размер half-res пикселя. Separate alpha blend сохраняет прежнюю direct-blend модель цвета. Temporal Pass 3 не изменён и остаётся заглушкой. Через anklebreaker MCP подтверждены загрузка новой C#-сборки, Play Mode выключен, Console — 0 errors, `ShaderHasError = false`; параметры сохранены. `ProjectC_URP_Renderer.asset` не включается в коммит этапа.

## T-CLOUD46 — 2026-11-19
**Статус:** принято пользователем — «оставляем»; F8/F9 не подтверждены.
Четвёртый этап Clouds v4: у `CloudNoise3D` включён полный mip-chain; уровень MIP рассчитывается из мирового footprint пикселя и дистанции луча. Mid/fine детали получают согласованные +2/+4 LOD, light march использует тот же footprint без fine. Добавлен статический IGN по экранному пикселю для смещения стартового сэмпла; покадровый golden-ratio jitter намеренно отложен до реализации temporal, чтобы не вносить мерцание. Добавлены `CloudStableV4` и `StableV4Strength = 0.65`. Через anklebreaker MCP выполнены импорт и пересборка текстуры: Play Mode выключен, компиляция завершена, Console — 0 errors; mip 1 размером 64×64 успешно прочитан через GPU copy (4096 пикселей). Текущие Lighting/Shape/Detail настройки не изменялись; `ProjectC_URP_Renderer.asset` не включается в коммит этапа. GPU-замеры — NOT RUN.

## T-CLOUD45 — 2026-11-19
**Статус:** реализовано, визуальная приёмка и F8/F9 — NOT RUN.
Третий этап Clouds v4: двухстадийная mid→fine эрозия существующими GBA-каналами 128³ noise-текстуры, early-out перед fine при плотности ≤0.003 и консервативный remap слабого ореола. Camera/debug лучи получают обе стадии; light march — только mid, без fine и дополнительной резкости. Эрозия применяется к нормированной плотности слоя до `DensityMultiplier`, поэтому сила эффекта не зависит от множителя слоя. StormDensity, LocalDensity, bounds и FO-контракт не изменены. Добавлены `CloudDetailV4` и `DetailV4Strength = 0.55`. Проверено через anklebreaker MCP: Play Mode выключен, компиляция завершена, Console — 0 errors, шейдер найден. Текущая ручная настройка формы пользователя сохранена; `ProjectC_URP_Renderer.asset` не включается в коммит этапа.

## T-FO09T — 2026-11-19
**Статус:** реализовано, runtime и визуальная F8/F9 — NOT RUN.
Исправлен вертикальный Floating Origin для volumetric clouds и штормов. Authored-границы в инспекторе сохраняются без изменений; в raymarch передаются `BottomY/TopY + CumulativeRebaseOffset.y`. Те же runtime-границы используются штормовым шейдером, тестовым спавном, маркерами и gizmos. На втором клиенте `CumulativeRebaseOffset` теперь обновляется из success/rollback broadcast до сдвига локальных участников. Raymarch, LocalDensity и центры ячеек остаются в rebased world space. Проверено через anklebreaker MCP: Play Mode выключен, компиляция завершена, Console — 0 errors. Текущие настройки v4 не изменялись.

## T-CLOUD44 — 2026-11-19
**Статус:** реализовано, визуальная приёмка NOT RUN.
Второй этап Clouds v4: coverage-дом с сужением кверху, перистый низ через переворот только Worley-деталей и постепенное разделение шумовых смещений слоёв. Базовый Perlin, границы слоёв, StormDensity и LocalDensity не изменены. Добавлены `CloudShapeV4` и `ShapeV4Strength = 0.65`; принятое пользователем `LightingV4Strength = 0.70` сохранено. Проверено через anklebreaker MCP: Play Mode выключен, компиляция завершена, Console — 0 errors, параметры и ссылка на шейдер сохранены. Runtime, визуальная проверка, F8/F9 и GPU-замеры — NOT RUN.

## T-CLOUD43 — 2026-11-19
**Статус:** принято пользователем — «визуально лучше стало»; F8/F9 не подтверждены.
Первый этап Clouds v4 по `Fishypants/cloudscape`: двулучевая HG-фаза, трёхчленный Beer–Lambert, powder и crease. Формулы адаптированы к метровой шкале ProjectC и ненормированной HG из `CloudCommon.hlsl`, а не скопированы с километровыми коэффициентами. Добавлены `CloudLightingV4` и `LightingV4Strength` для сравнения с v3 без перезапуска; после ручной настройки сохранено значение 0.70. Проверено через anklebreaker MCP: Play Mode выключен, компиляция завершена, Console — 0 errors, ссылка на `Hidden/ProjectC/VolumetricClouds` сохранена. GPU-замеры — NOT RUN.

## T-CLOUD41 — 2026-08-04
**Коммит:** `4cc74700`  
Штормовые облака иммунны к displacement (корабельный след не продавливает насквозь).

## T-CLOUD40 — 2026-08-04
**Коммит:** `c1284e85`  
Anti-banding: vertical noise/warp параметры. Убран vEnvelope, мягкие fade через StormEdgeSoftness.

## T-CLOUD39 — 2026-08-04
**Коммит:** `fbc91eea`  
Procedural storm form (abs-Perlin FBM вместо texture Worley). EditorPrefs save/load. Кастомный инспектор. Вертикальный шум + warp против слоистости.

## T-CLOUD38 — 2026-08-04
Удаление `_Storm*` из шейдерных Properties → фикс material shadowing. Переписан StormDensity с авто-масштабированием cellSize.

## T-CLOUD37d → T-CLOUD35 — 2026-08-04
StormDensity: cellular FBM, domain warp, color injection. Базовая визуализация штормов.

## T-CLOUD33 → T-CLOUD28 — 2026-08-04
Debug-позиционирование: маркеры, гизмо, поиск игрока по тегу.

## T-CLOUD12 — 2026-08-04
StormCellDirector + переписан StormLightningVfx. Начало фазы 2.4.

## Phase 1–2.3 — 2026-08-02 → 2026-08-04
Визуальное ядро, LocalDensityBuffer, displacement, конденсационные следы.  
*Детали: `IMPLEMENTATION_LOG.md`*
