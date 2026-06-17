# Composite Ship Architecture — Summary

**Дата:** 17 июня 2026
**Статус:** Phase 0–1 реализованы ✅ | Phase 3 (Door) готов ⬜

---

## Что такое составной корабль

Раньше корабль = 1 куб с `ShipController`, `Rigidbody`, `BoxCollider`. Теперь корабль — иерархия GameObjects: палуба, место пилота, дверь, двигатели — каждый отдельный child с собственным коллайдером/моделью. `Rigidbody` ОДИН на корне — вся конструкция движется как единое целое.

```
Ship_Root (GameObject) — Rigidbody, NetworkObject, ShipController
├── PilotSeat — PilotSeatController, ShipRootReference, BoxCollider(trigger)
├── Door — DoorController, ShipRootReference, BoxCollider(trigger)
├── Engine_Left — ModuleSlot, ShipRootReference
├── Engine_Right — ModuleSlot, ShipRootReference
└── (любые другие части)
```

## Ключевые компоненты

### ShipRootReference (Phase 0)
Маркер на любой части корабля. В `Awake` находит корневой `ShipController`, `Rigidbody`, `NetworkObject` через `transform.root.GetComponent<T>()`.  
**Ставится** на каждый дочерний GameObject корабля.

### ShipComponentLocator (Phase 0)
Статический хелпер `FindShipController(GameObject)`. Единый путь поиска для внешних систем (WindZone, NetworkPlayer).  
**Порядок:** ShipRootReference → GetComponent → GetComponentInParent → GetComponentInChildren.

### PilotSeatController (Phase 1)
Триггерное место пилота. `_controller.enabled = false` при посадке, игрок остаётся видимым (стоит в кресле).  
**Камера** переключает target на корень корабля (`ShipRoot`), distance = 18, height = 6.

### DoorController (Phase 3)
Локальная анимация slide (Lerp localPosition). E-key открывает/закрывает. Опционально MetaRequirement для замка.  
**MVP:** без сети. Каждый клиент проигрывает анимацию сам.

## Что изменилось в существующих скриптах

| Файл | Изменение | Почему |
|---|---|---|
| `ShipController.cs` | + `ShipRoot => transform.root` | Доступ к корню с любой дочки |
| `NetworkPlayer.cs` | Парентинг игрока к `ShipRoot` при посадке | Без парентинга — оверлап коллайдеров |
| `NetworkPlayer.cs` | Игрок НЕ пропадает (renderer не отключаются) | Дизайн: стоит у штурвала |
| `ThirdPersonCamera.cs` | + `SetTargetMode(target, isShip)` | Атомарное переключение |
| `InteractableManager.cs` | `FindNearestShip` ищет PilotSeat коллайдер | Чёткая зона посадки |

## Как добавить новую часть корабля

1. Создать дочерний GameObject внутри `Ship_Root`
2. Добавить нужный коллайдер (BoxCollider/SphereCollider)
3. Добавить `ShipRootReference` (маркер)
4. Добавить свой компонент (уже существующий или новый)
5. Если свой — ссылаться на `ShipRootReference.ShipController` для доступа к корню

Внешние системы (WindZone, NetworkPlayer, зоны урона) найдут корабль через `GetComponentInParent<ShipRootReference>()` или `ShipComponentLocator.FindShipController()`.

## Связь с подсистемами

| Подсистема | Статус | Как работает |
|---|---|---|
| **ModuleSlot** | ✅ Готов | `ShipModuleManager.GetComponentsInChildren<ModuleSlot>()` — ищет в детях |
| **WindZone** | ✅ Готов | `GetComponentInParent<ShipController>()` на триггере — находит корень |
| **MeziyModuleActivator** | ⏳ Phase 4 | Сейчас serialized ссылка. Нужен `GetComponentsInChildren<MeziyNozzle>()` |
| **MetaRequirement** | ✅ Готов | На любом дочернем GameObject. Ships пропущены через фильтр `mr.GetComponent<ShipController>()` |
| **CargoSystem** | ❌ Не существует | Создаётся с нуля когда понадобится |

## What's next

- **Phase 3:** DoorController (сделан, нужна расстановка в сцене)
- **Phase 4:** MeziyNozzle — визуал выхлопа как отдельные объекты
- **Phase 5:** Множественные места пилотов (multi-crew)
- **Phase 6:** Network-синхронизированные элементы (дверь через NetworkVariable)
