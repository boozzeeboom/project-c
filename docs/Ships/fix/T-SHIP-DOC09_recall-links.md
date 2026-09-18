# T-SHIP-DOC09 — перепроверка: Recall vs Persist / Dock / Ownership

> Тикет: `T-SHIP-DOC09`. Дата: 2026-09-18. Статус: **ПОДТВЕРЖДЕНО**.
> Scope: только перепроверка + пометка в доке. Кода нет.

## Что проверяли

Утверждение ревью (§10 несостыковок): `Modul_system/02 §6 Recall (22.07)` —
телепорт `rb.position=pad + _frozenByNoPilot=false + TryModifyCredits` —
без связи с `ShipPositionServer` persistence, `IsDocked/postUndockGrace` (Damage)
и без описанной серверной проверки владения.

## Факт (сверено чтением кода и дока)

- Код `ShipController.RecallShipToPadServerRpc:2381-2422` (верифицирован в ревью):
  `padPosition` и `cost` — от клиента (`:2382`); проверки владения **нет**;
  пад из реестра не валидируется; `cost=0` принимается; списание до телепорта —
  да (`:2388-2400`), но цифру диктует клиент; телепорт через `_rb.position` (`:2409`).
- Док `02 §6.2 шаг 5` / `§6.3`: клиент ищет пад (`FindObjectsByType<DockingPadTriggerBox>`,
  nearest) и шлёт `padPosition` — сервер доверяет. Владение/валидация пада не описаны.
- Связи отсутствуют: `ShipPositionServer` persistence (телепорт мимо сейва —
  следующий restore вернёт старую позицию?); `IsDocked` — `ExitDocked()` вызывается (`:2403`),
  но `postUndockGrace` (Damage, 3 сек) после телепорта не упомянут;
  `IsOwnerOfShip` — ни в доке, ни в коде.
- Дропдаун «свой корабль» (`§6.2 шаг 2`) — клиентский фильтр, сервер не сверяет.

## Решение

Статус ПОДТВЕРЖДЕНО. Фикс docs-only: баннер в `02 §6.1`
(«владение/пад/Persist/Grace не закрыты — кандидат в `T-SHIP-FIX04`»).
Кодовый фикс (ownership + серверный cost + пад из реестра + Persist-хук) —
НЕ этот тикет (уйдёт в `T-SHIP-FIX04`).

## Проверка

- Читать `02 §6.1`: баннер на месте.
- Compile: не требуется (docs only).
