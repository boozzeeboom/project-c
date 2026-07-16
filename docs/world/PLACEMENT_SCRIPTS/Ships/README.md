# ShipPresetCreator — Универсальный создатель кораблей

## Назначение
Editor-тул для создания универсального префаба корабля, совместимого как с игроком, так и с NPC.
Дизайнер выбирает класс и имя — получает готовый префаб со всеми компонентами и ассетами.
Остаётся только заменить визуал (MainVisual) на модель.

## Как использовать
1. Меню: `Tools → Project C → Create Ship Preset`
2. Выбрать класс: Light / Medium / Heavy / HeavyII
3. Ввести имя (станет именем префаба и отображаемым именем корабля)
4. Нажать «Создать префаб корабля»

## Что создаётся

### Префаб: `Assets/_Project/Prefabs/Ships/{ShipName}.prefab`
Универсальная структура (root scale = 1; MainVisual — дочерний объект):

```
{ShipName}
├── Transform (scale: 1,1,1) + Tag=Ship
├── Rigidbody (mass по классу, drag=0.4/8, Interpolate)
├── NetworkObject
├── ShipController (все flight-параметры класса)
├── NetworkTransform (Owner authority)
├── ShipFuelSystem (ёмкость/расход по классу)
├── ShipModuleManager (availablePower по классу)
├── MeziyModuleActivator (overheat=10s, cooldown=15s)
├── ShipHull (HP по классу, ссылка на ShipDamageConfig)
├── ShipModuleVisualApplier
├── ShipOwnershipRequirement
├── ShipRootReference
├── ShipInputReader
├── NpcShipController (пустой schedule, thrustMult=0.6, yawMult=0.4)
├── NpcProximityZone (awareness=400, avoidance=80)
├── ShipDeckNav (NavMeshSurface asset)
│
├── MainVisual (Cube + MeshRenderer + BoxCollider + ShipRootReference)
├── Platform (Cube + BoxCollider + ShipRootReference)
├── PilotSeat (Cube + BoxCollider(trigger) + PilotSeatController + ShipRootReference)
├── Door (Cube + BoxCollider(trigger) + DoorController + ShipPartShake)
├── DeckNavSurface (NavMeshSurface)
├── ShipCargoVisual (ShipCargoVisual + BoxCollider(trigger))
├── Exchanger (Sphere + SphereCollider(trigger) + ShipCargoConsole)
├── Slot_Engine (ModuleSlot(Engine) + EngineThrusterVisual + ShipPartShake)
│   ├── RotationAnchor
│   └── EngineVisuals
│       ├── Cylinder
│       └── Cube
├── Slot_MODULE_LIFT_ENH (ModuleSlot: Propulsion)
├── Slot_MODULE_MEZIY_PITCH (ModuleSlot: Special)
├── Slot_MODULE_MEZIY_ROLL (ModuleSlot: Special)
├── Slot_MODULE_MEZIY_THRUST (ModuleSlot: Special)
├── Slot_MODULE_MEZIY_YAW (ModuleSlot: Special)
├── Slot_MODULE_PITCH_ENH (ModuleSlot: Utility)
├── Slot_MODULE_ROLL (ModuleSlot: Utility)
├── Slot_MODULE_YAW_ENH (ModuleSlot: Utility)
└── Slot_cargo (ModuleSlot: Utility)
```

### Ассеты (создаются автоматически, если не существуют):

| Ассет | Путь |
|---|---|
| Ключ корабля | `Resources/Items/Key_{class}_ship.asset` |
| Конфиг повреждений | `Resources/Ship_hull/ShipDamage{class}.asset` |
| Пустое расписание NPC | `Resources/PeacefulShip/NpcShipSchedule_{Class}_Default.asset` |
| NavMesh для палубы | `Prefabs/Ships/NavMesh-DeckNavSurface_{Class}/NavMesh-DeckNavSurface.asset` |

## Пресеты параметров по классам

| Параметр | Light | Medium | Heavy | HeavyII |
|---|---|---|---|---|
| **Источник** | Ship_Light_root (сцена) | Интерполяция | Интерполяция | NPC_Ship_HeavyII_03 (сцена) |
| thrustForce | 5000 | 4000 | 5500 | 6500 |
| maxSpeed | 5000 | 400 | 70 | 100 |
| yawForce | 70000 | 25 | 200000 | 500000 |
| pitchForce | 25 | 20 | 15 | 0 |
| verticalForce | 7000 | 120 | 800 | 1200 |
| yawSmoothTime | 0.25 | 0.3 | 0.5 | 0.7 |
| pitchSmoothTime | 0.6 | 0.7 | 0.9 | 1.1 |
| liftSmoothTime | 0.8 | 1.0 | 1.2 | 1.5 |
| thrustSmoothTime | 0.2 | 0.3 | 0.4 | 0.5 |
| yawDecayTime | 0.8 | 1.0 | 1.5 | 2.0 |
| windExposure | 1.2 | 1.0 | 0.7 | 0.5 |
| massMultiplier | 15 | 10 | 10 | 10 |
| fuelMax | 50 | 100 | 200 | 300 |
| fuelConsumption | 0.5 | 0.8 | 1.2 | 1.5 |
| hullHP | 100 | 200 | 400 | 600 |
| modulePower | 100 | 200 | 300 | 400 |
| visualScale | (6,1,12) | (8,1.5,15) | (11,1.5,19) | (13.3,1,22) |

## Ключевые архитектурные решения

1. **Паттерн визуала от NPC-корабля**: корень scale=(1,1,1), вся геометрия в дочернем `MainVisual`.
   Это позволяет менять модель без аффекта на физику и коллизии.
   
2. **ShipController.ApplyShipClass()**: в Awake перезаписывает `yawSmoothTime`, `pitchSmoothTime`, 
   `liftSmoothTime`, `thrustSmoothTime`, `yawDecayTime`, `windExposure`, `rb.mass`. 
   Поля `thrustForce`, `maxSpeed`, `yawForce`, `pitchForce`, `verticalForce` — editable per-instance 
   (НЕ перезаписываются в ApplyShipClass).

3. **ShipModuleManager.Initialize()**: в Awake авто-обнаруживает все ModuleSlot через 
   `GetComponentsInChildren<ModuleSlot>(true)`. Поэтому слоты не нужно вручную линковать в инспекторе.

4. **ShipOwnershipRequirement**: авто-добавляется в ShipController.Awake() если отсутствует. 
   В префабе добавляем явно для чистоты.

5. **NPC-компоненты**: NpcShipController, NpcProximityZone, ShipDeckNav — висят на корне, 
   но активируются только когда корабль используется как NPC (EnableNpcPilot).

## Что делать дизайнеру после создания
1. Заменить `MainVisual` (куб) на реальную 3D-модель корабля
2. Настроить материал и цвет
3. Подвинуть `PilotSeat`, `Door`, `Exchanger`, `Slot_Engine` под модель
4. Настроить `NpcShipSchedule` (назначить реальное расписание вместо пустого)
5. При необходимости — скорректировать flight-параметры в ShipController

## История изменений
- **v1.0** (2026-07): Создание. Анализ Ship_Light_root и NPC_Ship_HeavyII_03.
