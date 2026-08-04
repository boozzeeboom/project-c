# Итерации Cloud System 3.0 — Changelog

> **Итоговый статус:** `STATUS.md`

---

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
