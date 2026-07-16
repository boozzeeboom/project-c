# NpcShipController & NpcShipSchedule — Custom Editors

> **Дата:** 2026-07-07 | **Тикет:** NPC-Editor-T01  
> **Файлы:**
> - `Assets/_Project/Scripts/PeacefulShip/Editor/NpcShipControllerEditor.cs`
> - `Assets/_Project/Scripts/PeacefulShip/Editor/NpcShipScheduleEditor.cs`
> - `Assets/_Project/Scripts/PeacefulShip/Editor/NpcShipRouteDrawer.cs`

## Назначение

Три Editor-скрипта, улучшающие UX редактирования NPC-кораблей:
1. **NpcShipControllerEditor** — фолдауты, inline-просмотр Schedule, кнопка «Create New Schedule»
2. **NpcShipScheduleEditor** — фолдауты, сканирование станций из сцены, валидация
3. **NpcShipRouteDrawer** — PropertyDrawer: dropdown LocationId из сцены вместо raw strings

---

## 1. NpcShipControllerEditor

`[CustomEditor(typeof(NpcShipController))]`

### Что делает:
- Группирует 20+ полей в **5 фолдаутов**:
  - **Schedule & Identity** — поле Schedule SO, кнопки ⊕/◎, inline-просмотр, Duplicate
  - **Movement (server-only)** — npcThrustMult, npcYawMult, npcArrivalToleranceMeters
  - **Anti-gravity Boost (Q8)** — duration + value
  - **Ship-to-Ship Avoidance (M3.2)** — 6 параметров манёвра (свёрнут по умолчанию)
  - **Debug** — debugMode (свёрнут по умолчанию)

### Кнопки в Schedule-секции:
| Кнопка | Действие |
|--------|----------|
| **⊕** | Создать новый `NpcShipSchedule` SO. Открывает SaveFilePanel → создаёт .asset с `scheduleId` на основе имени корабля, одним пустым route, dwell=60/90. Автоматически назначает на контроллер. |
| **◎** | Ping — подсветить текущий Schedule в Project-окне. |
| **📝 Edit Schedule** | Открыть Schedule в Inspector (выделить ассет). |
| **📋 Duplicate** | Клонировать текущий Schedule через SaveFilePanel → новый ассет → назначить на контроллер. |

### Inline Schedule readout:
- Если Schedule назначен — показывает (read-only) scheduleId, displayName, type, кол-во routes, dwell range, кол-во cargo items.
- Поля не редактируются inline (чтобы избежать путаницы: редактировать Schedule нужно через его собственный Editor).

---

## 2. NpcShipScheduleEditor

`[CustomEditor(typeof(NpcShipSchedule))]`

### Что делает:
- **5 фолдаутов**:
  - **Identity** — scheduleId, displayName, scheduleType
  - **Routes** — массив NpcShipRoute[] (с кастомным PropertyDrawer — см. ниже)
  - **Traffic Shaping (Gaussian)** — meanArrivalIntervalSec, arrivalIntervalStdDev, minArrivalSpacingSec
  - **NPC Behavior** — minDwellTimeSec, maxDwellTimeSec (+ warning если max < min)
  - **Cargo Trade** — cargoTrade (NpcCargoTradeListConfig) с вложенными buyItems

### Toolbar:
- Показывает имя ассета
- Кнопка **🔍 Scan Scene** — сканирует все открытые сцены на `DockStationController`, кеширует LocationId. Эти ID используются в dropdown'ах `NpcShipRouteDrawer`.

### Валидация (в реальном времени):
- Если routes пуст → warning
- Для каждого route проверяет, что `fromLocationId`/`toLocationId` есть среди LocationId станций в сцене → error если нет
- Если maxDwellTimeSec < minDwellTimeSec → warning

---

## 3. NpcShipRouteDrawer

`[CustomPropertyDrawer(typeof(NpcShipRoute))]`

### Что делает:
- Заменяет стандартный вывод struct на компактный layout с dropdown'ами для location ID.
- **From / To** — выпадающие списки, populated из `DockStationController.LocationId` найденных в сцене.
- **Dwell / Flight** — два float поля в строке.
- **Ship Class / Demand** — два enum/поля в строке.
- **Warning** — если ID не найден в сцене, показывает красный HelpBox под полями.

### Кеширование:
- Сканирование сцены (`FindObjectsByType<DockStationController>`) кешируется на 3 секунды, чтобы не дёргать поиск на каждом кадре Editor GUI.

---

## Workflow: создание NPC-корабля

1. Разместить на сцене GameObject с `NetworkObject` + `ShipController` + `NpcShipController`.
2. Добавить `NpcProximityZone` (опционально — для ship-to-ship avoidance).
3. Выбрать `NpcShipController` в Inspector.
4. Нажать **⊕** рядом с полем Schedule → выбрать путь → создан `NpcShipSchedule_*.asset`.
5. Открыть созданный Schedule (кнопка **📝 Edit Schedule**).
6. Нажать **🔍 Scan Scene** чтобы подтянуть LocationId станций.
7. В Routes → выбрать From/To из выпадающих списков (вместо ручного ввода ID).
8. Настроить Dwell, Flight, Ship Class.
9. При необходимости — настроить Cargo Trade (buyItems).

---

## Советы по сложным маршрутам

- **Loop-маршрут:** поставьте `scheduleType = Loop`, добавьте несколько route'ов (A→B, B→C, C→A). NPC будет циклически обходить все.
- **RoundTrip:** один route A→B — NPC летит туда-обратно.
- **RandomFromPool:** несколько route'ов — на каждом леге выбирается случайный.
- **Cargo Trade:** для курьера — `sellAllOnArrival = true`, `buyConfiguredItemsAfterSell = true`, заполните buyItems. Для пассажирского NPC — оставьте `cargoTrade.buyItems` пустым.

---

## Случайный Dwell (M3.2.15)

Каждый route теперь поддерживает **рандомизацию длительности стоянки**:

| Поле | Описание | Пример |
|------|----------|--------|
| `dwellTimeSec` | Базовая (гарантированная) длительность | 120 |
| `dwellRandomAddMinSec` | Минимум добавочного случайного времени | 10 |
| `dwellRandomAddMaxSec` | Максимум добавочного случайного времени | 10000 |

**Формула:** `dwell = clamp(dwellTimeSec + Random(minAdd, maxAdd), minDwellTimeSec, maxDwellTimeSec)`

**Пример:** `dwellTimeSec=120, randomAddMin=10, randomAddMax=10000, minDwell=60, maxDwell=3600`
→ фактический dwell = `120 + Random(10, 10000)` = от 130с до 10120с (но не больше 3600 из-за clamp).

**Backward compat:** оба поля `dwellRandomAdd*Sec` = 0 по умолчанию → поведение как раньше.

**Где вычисляется:** `NpcShipController.ResolveDwellTime()` — вызывается один раз при `SetMode(Docked)`.

---

## Связанные файлы

| Файл | Роль |
|------|------|
| `NpcShipController.cs` | NetworkBehaviour на корне NPC-корабля |
| `NpcShipSchedule.cs` | ScriptableObject с маршрутами |
| `NpcShipRoute.cs` | Struct одного leg'а маршрута |
| `NpcCargoTradeConfig.cs` | Конфигурация cargo trade (buyItems и т.д.) |
| `DockStationController.cs` | NetworkBehaviour станции (источник LocationId) |
| `DockingZoneRegistry.cs` | Статический реестр stationId → DockStationController |
| `DockStationControllerEditor.cs` | Референс-паттерн (inline SO + Duplicate) |
