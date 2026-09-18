# T-SHIP-DOC03 — перепроверка: 28 (выкинуть World) vs P1-факт

> Тикет: `T-SHIP-DOC03`. Дата: 2026-09-18. Статус: **ПОДТВЕРЖДЕНО**.
> Scope: только перепроверка + пометки в доке. Кода нет.

## Что проверяли

Утверждение ревью (§3 несостыковок): `28_KEY_ARCHITECTURE_REVIEW.md §§5/7`
предлагает выкинуть `KeyRodInstanceWorld` → `KeyRegistry/KeyInstance (itemId=123 общий)`,
13ч; а P1 (`SHIP_REFACTOR_PLAN_2026-07-21` + `ITERATIONS.md` P1) сделал наоборот.

## Факт на текущем main (сверено чтением)

- `28_` — ревью Mavis от 2026-06-19 (до P1). Предложение: 4 файла
  (`KeyRegistry/KeyInstance/KeyInstanceRepository` + UI-миграция), Phase D п.1–3 —
  **удалить `KeyRodInstanceWorld.cs`, `KeyRodInstance.cs`, `KeyRodInstanceRepository.cs`**,
  п.5–6 — удалить `ShipOwnershipRequirement.cs`, `ShipOwnershipRegistry.cs`.
- P1-факт (2026-07-21, `ITERATIONS.md`: `-1139/+651`, 7 файлов удалено):
  `KeyRodInstanceWorld` **оставлен** как SSOT; удалены только обёртки
  (`ShipKeyBinding/Server/ClientState/Toast`, `ShipOwnershipRegistry`, `KeyRodInstanceBinding`);
  `ShipOwnershipRequirement` **оставлен**; `MetaRequirement*` оставлены.
  То есть Phase D из `28 §7` выполнен ровно наоборот по пп.1–3,5.
- Следствие: метрики `28 §6` («11 файлов, 5 правд, 3 reflection») невалидны после P1;
  `§§4.3/9/10` (drop bug T-KEY-09, «полный рефакторинг 13ч») — исторический срез до P1.
- Файлов `KeyRegistry.cs` / `KeyInstance.cs` в `Assets/` нет (предложение не реализовано).

## Решение

Статус ПОДТВЕРЖДЕНО. Фикс docs-only: шапка + баннеры на `§§5/6/7/9/10`
(«предложение не реализовано, P1 пошёл противоположным путём — World оставлен SSOT;
читать как историю, не как план»). Текст не переписываем.

## Проверка

- Читать `28_KEY_ARCHITECTURE_REVIEW.md`: баннеры на месте, `§§1–4/8` (анализ проблемы) без баннера.
- Compile: не требуется (docs only).
