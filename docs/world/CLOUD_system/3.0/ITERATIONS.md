# Итерации Cloud System 3.0 — Changelog

> **Итоговый статус:** `STATUS.md`

---

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
