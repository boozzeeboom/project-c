# T-SHIP-DOC07 — перепроверка: L1 visual done / не начат

> Тикет: `T-SHIP-DOC07`. Дата: 2026-09-18. Статус: **ПОДТВЕРЖДЕНО (дока устарела, код готов)**.
> Scope: только перепроверка + пометка в доке. Кода нет.

## Что проверяли

Утверждение ревью (§8 несостыковок): `customisation/00_SUMMARY.md (04.07)` — `D visualPrefab ❌`,
`SHIP_REFACTOR P4` — `done`, `ITERATIONS 01-06.07` — `Module Visual Preview (Editor) done`,
`Modul_system/01 (19.07)` про `visualPrefab` молчит.

## Факт на текущем main (сверено grep по коду)

L1 **реализован** (P4, 2026-07-21):
- `ShipModule.cs:125-127` — `[Header("Visual (L1 — module visualPrefab)")] public GameObject visualPrefab`
  (+ offsets/attachAxis).
- `ShipModuleVisualApplier.cs` — runtime спавн/удаление (`:81` проверка слота,
  `:108` `Instantiate(module.visualPrefab, parent)`), подписка на `OnModuleChanged`.
- `ModuleSlotEditor.cs` — Editor-preview (`:49-60,110-129`, кнопка «▶ Preview»).
- `02_ENGINE_VISUAL_ANALYSIS_AND_PLAN.md §2.4` (строки 117–129): статус «✅ Полностью готов».
- `SHIP_REFACTOR_PLAN` P4 — `✅ Complete (2026-07-21)`; `T-ENG02`-анализ опирается на applier как на готовый.
- Устарело: `customisation/00_SUMMARY.md` (аналитика от 04.07, **до** P4 21.07) —
  строки 17 (`D ❌ Не начато`), 36 (`Нет visualPrefab (TODO)`), 51–52 (gap-анализ:
  поля + applier «не хватает», Critical). `Modul_system/01` про visual молчит,
  но это пробел освещения, не отсутствие фичи.

## Решение

Статус ПОДТВЕРЖДЕНО. Фикс docs-only: шапка-баннер в `customisation/00_SUMMARY.md`
(«L1-статусы D/§1.1/§1.2 устарели — P4 21.07 реализовал; читать как предисторию»),
текст не переписываем. Остаток L1 (пул вместо Instantiate/Destroy, client-only guard,
colliders off) — НЕ этот тикет (уйдёт в `T-SHIP-FIX14`).

## Проверка

- Читать шапку `00_SUMMARY.md`: баннер на месте.
- Compile: не требуется (docs only).
