# Именная команда NPC на движущемся корабле — Этап 3: ship-local anchors

**Дата:** 2026-09-08
**Тикет:** `T-CREW-09`
**Статус:** завершён; runtime spawn и Play Mode не запускались.
**Предыдущий этап:** `05_IMPLEMENTATION_STAGE_02_CREW_MANIFEST_2026-09-08.md`

## Цель этапа

Добавить на prefab «Горгоны» стабильные дочерние точки для fixed crew spawn, pilot seat stand и MVP patrol. Все точки должны двигаться вместе с ShipRoot.

## Изменения в prefab

В `Assets/_Project/Prefabs/Ships/Горгона.prefab` создан дочерний объект:

- `CrewAnchors`

Внутри `CrewAnchors` созданы пять empty Transform:

| Anchor | Local position | Local rotation | Роль |
|---|---:|---:|---|
| `PilotSpawn` | `(0.00, 0.75, 5.00)` | `(0, 0, 0)` | начальная точка fixed pilot |
| `PilotSeatStand` | `(0.00, 0.75, 6.00)` | `(0, 0, 0)` | stand/seat binding пилота |
| `Patrol_01` | `(-3.00, 0.75, 4.00)` | `(0, 0, 0)` | первая точка patrol |
| `Patrol_02` | `(3.00, 0.75, 0.00)` | `(0, 0, 0)` | вторая точка patrol |
| `Patrol_03` | `(-3.00, 0.75, -4.00)` | `(0, 0, 0)` | третья точка patrol |

Все anchors имеют scale `(1, 1, 1)` и находятся непосредственно под `CrewAnchors`, который является дочерним объектом корня `Горгона`.

## Обоснование размещения

Статическая проверка существующего prefab подтвердила:

- `PilotSeat` находится в local position `(0.00, 1.18, 6.75)`;
- `Platform` имеет deck bounds, охватывающие выбранные patrol positions;
- выбранные patrol points лежат в центральной палубной зоне и не добавляют новые collider/renderer-компоненты.

Точки являются данными для будущего `ShipCrewSpawner` и deck activity adapter; они не запускают NPC самостоятельно.

## Что не изменялось

- геометрия, colliders и renderers существующего корабля;
- `PilotSeatController`;
- `NpcSpawner`;
- `NpcShipController`;
- `ShipDeckNav`;
- `ShipCrewManifest` asset.

## Проверки

- hierarchy `Горгона` подтверждает `CrewAnchors` и пять дочерних anchors;
- editor verification подтвердил parent `CrewAnchors`, local positions, zero rotation и unit scale всех пяти точек;
- проверка существующей геометрии подтвердила расположение `PilotSeat` и `Platform` до выбора координат;
- временные editor verification scripts удалены после проверки.

Runtime NavMesh sampling, движение по точкам, spawn и seat binding пока не проверялись.

## Acceptance

Этап закрыт: `CrewAnchors`, `PilotSpawn`, `PilotSeatStand` и три patrol точки созданы как ShipRoot-local transforms с устойчивыми именами и позициями.

## Следующий этап

**Этап 4 — `ShipCrewSpawner`:** добавить server-only компонент на корень «Горгоны», подключить manifest и anchors, реализовать exact prefab spawn, NGO parenting, runtime mapping и idempotency.
