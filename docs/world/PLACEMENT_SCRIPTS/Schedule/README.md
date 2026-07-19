# NPC Ship Schedule Overview

## Обзор системы

NpcShipSchedule — ScriptableObject-расписания мирных NPC-кораблей. Каждый SO описывает маршруты, параметры движения (Gaussian shaping) и cargo-trade стратегию.

### Где лежат SO

`Assets/_Project/Resources/PeacefulShip/NpcShipSchedule_*.asset` — 6 файлов.

### Как корабли привязываются к расписаниям

1. `NpcShipController` (NetworkBehaviour на GameObject корабля) имеет поле `schedule` — ссылка на SO.
2. При спавне NpcShipController регистрирует корабль в `NpcShipWorld` с этим schedule.
3. NpcShipWorld ведёт FSM каждого NPC, переключая маршруты из schedule.

### Типы расписаний

| ScheduleType | Описание |
|---|---|
| RoundTrip | A→B→A→B... (один leg чередуется туда-обратно) |
| Loop | A→B→C→A→B→C... (круговой обход) |
| RandomFromPool | Случайный выбор leg при каждом переходе |

### Маршрут (NpcShipRoute)

Один leg: `fromLocationId` → `toLocationId`, с параметрами dwell (стоянка), flight (полёт), preferredShipClass.

### Cargo Trade (NpcCargoTradeListConfig)

Настройки: что NPC покупает на станциях (`buyItems[]`), продаёт ли по прибытии (`sellAllOnArrival`), лимиты по слотам/весу (`maxLoadSlots`, `maxLoadWeightKg`).

---

## Инструмент: NpcShipScheduleOverviewWindow

**Где:** `Tools > Project C > NPC Ship Schedule Overview`

### Возможности

- **Вкладка 1 — Все расписания:** таблица всех SO. Клик по ▶ разворачивает inline-редактор: identity, traffic shaping, routes с полями каждого leg. Кнопки Add/Remove Route. Изменения сохраняются в SO через SerializedObject + SetDirty.
- **Вкладка 2 — Корабли по сценам:** обход всех `WorldScene_*`, поиск `NpcShipController`. Выпадающий список для назначения schedule любому кораблю. При выборе — сцена открывается аддитивно, schedule записывается через SerializedProperty, сцена сохраняется и закрывается.
- **Вкладка 3 — Cargo Trade:** редактируемые поля behaviour-флагов (toggles), лимитов (maxLoadSlots, maxLoadWeightKg), список buyItems с inline-редактированием itemId, desiredQuantity, sellOnArrival, maxKeepQuantity. Кнопки Add/Remove Buy Item.

### Код

`Assets/_Project/Editor/Tools/NpcShipScheduleOverviewWindow.cs`

---

*Создано: 2026-07-04*
