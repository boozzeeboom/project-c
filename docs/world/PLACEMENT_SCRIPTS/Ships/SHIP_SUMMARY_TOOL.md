# Ship Summary Tool — сводный редактор кораблей

## Назначение

EditorWindow для массового просмотра, сравнения и редактирования всех кораблей проекта.
Сканирует префабы в `Assets/_Project/Prefabs/Ships/`, показывает сводную таблицу
с ключевыми параметрами, позволяет редактировать выбранный корабль и применять
массовые изменения к группе кораблей.

## Как использовать

1. Меню: `Tools → Project C → Ship Summary`
2. Окно показывает таблицу всех кораблей с основными параметрами
3. **Клик по строке** — выделяет корабль, снизу открывается детальная панель со всеми foldout-секциями
4. **Ctrl+Click** — мультивыделение, снизу открывается панель Batch Edit
5. В детальной панели можно редактировать любое поле — изменения сразу сохраняются в префаб
6. В Batch Edit: ввести значение → «Apply to Selected» — применится ко всем выделенным
7. Кнопка «Rescan» перечитывает все префабы (нужно после ручных изменений)

## Сводная таблица (колонки)

| Колонка | Источник | Описание |
|---------|----------|----------|
| Name | имя префаба | Человекочитаемое имя |
| Class | ShipController.shipFlightClass | Light / Medium / Heavy / HeavyII |
| Thrust | ShipController.thrustForce | Сила тяги |
| MaxSpd | ShipController.maxSpeed | Макс. скорость |
| Yaw | ShipController.yawForce | Сила рыскания |
| Pitch | ShipController.pitchForce | Сила тангажа |
| Vert | ShipController.verticalForce | Сила вертикального движения |
| Mass× | ShipController.massMultiplier | Множитель массы |
| FuelMax | ShipFuelSystem.maxFuel | Макс. ёмкость топлива |
| Fuel/s | ShipFuelSystem.fuelConsumptionRate | Расход топлива/сек |
| HP | ShipDamageConfig.GetMaxHull() | HP корпуса по классу |
| Power | ShipModuleManager.availablePower | Доступная энергия модулей |
| WindExp | ShipController.windExposure | Экспозиция к ветру |

Сортировка: клик по заголовку колонки. Повторный клик — обратный порядок.

## Детальная панель

Один foldout на компонент — каждый показывает **все** serialized-поля через `SerializedObject.GetIterator()`:

- **🚀 ShipController** — все поля: shipFlightClass, thrustForce, maxSpeed, yaw/pitch/verticalForce,
  antiGravity, yawSmoothTime..thrustSmoothTime, yawDecayTime, pitchDecayTime,
  massMultiplier, massLight..massHeavyII, shipConstraints, linearDrag, angularDrag,
  pitchStabForce, rollStabForce, maxPitchAngle, autoStabilize,
  corridorSystem, windInfluence, windExposure, windDecayTime,
  _globalWindEnabled, _globalWindForceScale, _globalWindVerticalFactor,
  baseMaxCargoSlots/Weight/Volume, baseCargoPenaltyFactor,
  moduleManager, meziyActivator, fuelSystem, meziyVisual,
  _customDisplayName, _keyItemData, _debugLog, _showLegacyMeziyHud

- **🤖 NpcShipController** — все поля: schedule, npcInstanceId, npcThrustMult, npcYawMult,
  npcArrivalToleranceMeters, antiGravityBoostDuration, antiGravityBoostValue,
  debugMode, avoidSeparateSpeed/Time, avoidStopTime, avoidBackOffSpeed/Time, avoidTimeout

- **⛽ ShipFuelSystem** — все поля: currentFuel, maxFuel, fuelConsumptionRate, fuelRegenRate,
  startEngineConsumption, idleConsumptionRate, atmosphericRefuelRate,
  thrustPenaltyDuringRefuel, speedPenaltyDuringRefuel

- **🔌 ShipModuleManager** — все поля: slots, availablePower

- **🛡️ ShipHull** — все поля: _damageConfig, _debugLog + readonly Resolved HP

- **📍 NpcProximityZone** — все поля: awarenessRadius, avoidanceRadius, clearHysteresis, drawGizmos

Кнопка «Select Prefab in Project» — пингует префаб в Project-окне.

## Batch Edit

Доступные для массового изменения поля:
- Flight Class (enum)
- Thrust Force, Max Speed, Yaw/Pitch/Vertical Force
- Mass Multiplier
- Max Fuel, Fuel Consumption Rate
- Available Power
- Wind Exposure
- Linear Drag, Angular Drag

Каждое поле: ввести значение → «Apply to Selected» → применяется ко всем выделенным префабам.

## Архитектура

```
ShipSummaryWindow : EditorWindow
├── Rescan()              — сканирует папку Ships, читает все компоненты через SerializedObject
├── ReadShipFromPrefab()  — читает все поля с префаба в ShipSummaryEntry
├── DrawTable()           — сводная таблица (сортировка, мультивыделение)
├── DrawDetailPanel()     — детальная панель с foldout-секциями
├── DrawBatchPanel()      — панель массового редактирования
└── BatchApplyFloat/Enum  — применение изменений к группе префабов
```

**Ключевое решение:** все чтение/запись идёт через `SerializedObject` от компонентов на префабе
(без инстанцирования в сцену). `AssetDatabase.LoadAssetAtPath<GameObject>(path)` + `new SerializedObject(component)`.

**ShipSummaryEntry** — структура данных, кеширующая все значимые поля одного корабля
для быстрого отображения в таблице без повторного парсинга префабов на каждом кадре.

## Связанные файлы

- `Assets/_Project/Editor/ShipSummaryWindow.cs` — реализация
- `Assets/_Project/Editor/ShipPresetCreator.cs` — создание новых кораблей
- `Assets/_Project/Scripts/Player/Editor/ShipControllerEditor.cs` — custom editor для ShipController
- `docs/world/PLACEMENT_SCRIPTS/Ships/README.md` — документация ShipPresetCreator

## История изменений

- **v1.0** (2026-07): Создание. Сводная таблица, детальная панель, batch edit.
