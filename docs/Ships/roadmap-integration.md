# Composite Ship — Roadmap Интеграции

**Цель:** Сделать корабль "летающей баржей" — составной конструкцией, а не 1 кубом.
**Принцип:** Ты расставляешь объекты в сцене. Я говорю какие скрипты, какую иерархию и что в инспекторе назначить.

---

## ▸ Phase 0: Фундамент (обязательно, без этого никуда)

### 0.1 Создать ShipRootReference.cs
```csharp
// Assets/_Project/Scripts/Ship/ShipRootReference.cs
// Маркер на любой части корабля → быстрый поиск корня
```
- **Куда ставить:** На каждый GameObject, который часть корабля (место пилота, дверь, двигатель)
- **Что делает в Awake:** `transform.root.GetComponent<ShipController>()` — находит корневой ShipController
- **Иерархия:** Не меняет, просто компонент

### 0.2 Создать ShipComponentLocator.cs
```csharp
// Assets/_Project/Scripts/Ship/ShipComponentLocator.cs
// Static helper — единый способ найти ShipController из любой части корабля
```
- **Замена** разрозненных `GetComponentInParent/InChildren` в WindZone и др.
- **Порядок поиска:** ShipRootReference → ShipController на родителе → ShipController на себе → ShipController в детях

### 0.3 Исправить ShipController.cs
```csharp
// Добавить:
// public Transform ShipRoot => transform.root;
// Rigidbody поиск — оставить GetComponent<Rigidbody>() (ShipController на корне)
```
- **Ничего не ломает.** ShipController остаётся на корне корабля (как сейчас)

---

## ▸ Phase 1: Место пилота (MVP — 1 сессия)

### 1.1 Создать PilotSeatController.cs
```csharp
// Assets/_Project/Scripts/Ship/PilotSeatController.cs
// MonoBehaviour (не NetworkBehaviour). Триггерная зона места пилота.
// Ссылается на ShipController через ShipRootReference.
// F-key в NetworkPlayer находит PilotSeatController вместо ShipController.
```

### 1.2 Исправить NetworkPlayer.cs
- `FindNearestShip()` → `FindNearestPilotSeat()`
  - Ищет `PilotSeatController[]` в радиусе
  - Берёт `seat.ShipController` для посадки
  - Это минимальное изменение — всё остальное (RPC, _inShip) остаётся как есть
- `SpawnCamera()` — при посадке камера переключает target на `ship.ShipRoot` вместо `transform`

### 1.3 Исправить ThirdPersonCamera.cs
- Добавить `SetTargetMode(Transform target, bool isShip)` — меняет и цель, и distance/height
- distance/height пока остаются жёсткими (18/6). Потом сделаем динамические.

### 1.4 Твоя иерархия (Phase 1)

```
Ship_Root (GameObject)                    ← как сейчас: ShipController, Rigidbody, NetworkObject, BoxCollider
└── PilotSeat (GameObject)                ← НОВЫЙ, дочерний к Ship_Root
    ├── BoxCollider (IsTrigger = true)    ← зона входа (поставь размер ~2×2×2)
    ├── PilotSeatController.cs            ← ссылка пойдёт на ShipRootReference
    └── ShipRootReference.cs              ← найдёт ShipController на корне
```

**Как проверить:**
1. Открыть WorldScene_0_0
2. Создать Ship_Root из текущего префаба
3. Добавить PilotSeat как дочерний GameObject
4. Настроить PilotSeatController + ShipRootReference
5. Play Mode: подойти к PilotSeat → F → сесть за штурвал
6. Камера отдаляется на 18, видно весь корпус
7. Игрок не исчезает — анимация (пока заглушка)

---

## ▸ Phase 2: Площадка (палуба)

### 2.1 Создать ShipRootReference.cs (уже есть из Phase 0)

### 2.2 Твоя иерархия (Phase 2)

```
Ship_Root
├── PilotSeat (из Phase 1)
├── Deck (GameObject)                     ← НОВЫЙ
│   └── BoxCollider (IsTrigger = false)   ← твёрдая палуба, размер как у корабля
└── ...
```

**Важно:** 
- Дочерний BoxCollider (Deck) + корневой Rigidbody = нормально. Rigidbody собирает все коллайдеры детей.
- Никаких новых скриптов не нужно. Просто коллайдер.

**Как проверить:**
1. Войти в корабль (F на PilotSeat)
2. Игрок стоит на Deck (он child корабля)
3. Корабль летит, игрок стоит — всё движется как единое целое
4. Выйти из корабля → игрок отделяется

---

## ▸ Phase 3: Дверь

### 3.1 Создать DoorController.cs
```csharp
// Assets/_Project/Scripts/Ship/DoorController.cs
// MonoBehaviour (для MVP — локальная анимация).
// slide в сторону, без сети.
// E-key открывает/закрывает (через MetaRequirement если заперта).
```

### 3.2 Твоя иерархия (Phase 3)

```
Ship_Root
├── PilotSeat
├── Deck
├── Door (GameObject)                     ← НОВЫЙ
│   ├── BoxCollider (IsTrigger = true)    ← зона взаимодействия
│   ├── Model_Door (дочерний, 3D модель)   ← анимируется сдвиг
│   ├── DoorController.cs
│   ├── ShipRootReference.cs
│   └── MetaRequirement.cs               ← опционально (если дверь заперта)
└── ...
```

**Как проверить:**
1. Подойти к двери → E → дверь открывается (slide вбок)
2. E ещё раз → закрывается
3. Без ключа (если настроен MetaRequirement) → toast "Нужен ключ"

---

## ▸ Phase 4: Meziy-двигатели как отдельные объекты

### 4.1 Создать MeziyNozzle.cs
```csharp
// Assets/_Project/Scripts/Ship/MeziyNozzle.cs
// Компонент на каждом двигателе. Управляет визуалом выхлопа.
// MeziyModuleActivator (на корне) ищет все MeziyNozzle в детях.
```

### 4.2 Исправить MeziyModuleActivator.cs
- Вместо serialized `meziyVisual` → `GetComponentsInChildren<MeziyNozzle>()`
- При активации — вызывает `nozzle.Activate()/Deactivate()` на каждом

### 4.3 Твоя иерархия (Phase 4)

```
Ship_Root
├── PilotSeat
├── Deck
├── Door
├── Meziy_Thruster_Left (GameObject)      ← НОВЫЙ
│   ├── ModuleSlot.cs                     ← слот модуля (type=Propulsion)
│   ├── MeziyNozzle.cs                   ← визуал выхлопа
│   ├── ShipRootReference.cs
│   └── (3D модель двигателя)
├── Meziy_Thruster_Right (GameObject)     ← НОВЫЙ
│   ├── ModuleSlot.cs
│   ├── MeziyNozzle.cs
│   ├── ShipRootReference.cs
│   └── (3D модель двигателя)
└── ShipModuleManager.cs (на корне, ищет ModuleSlot в детях — уже работает)
```

---

## ▸ Сводка: что создаём (код)

| # | Файл | Размер | Статус |
|---|---|---|---|
| 0.1 | `ShipRootReference.cs` | ~15 строк | **Phase 0 — без этого никуда** |
| 0.2 | `ShipComponentLocator.cs` | ~25 строк | **Phase 0 — без этого никуда** |
| 1.1 | `PilotSeatController.cs` | ~40 строк | **Phase 1** |
| 3.1 | `DoorController.cs` | ~60 строк | **Phase 3** |
| 4.1 | `MeziyNozzle.cs` | ~30 строк | **Phase 4** |

**Что меняем (существующий код):**
| Файл | Изменение |
|---|---|
| `ShipController.cs` | + `ShipRoot => transform.root` (1 строка) |
| `NetworkPlayer.cs` | `FindNearestShip` → `FindNearestPilotSeat` (~8 строк) |
| `NetworkPlayer.cs` | Camera target switch при посадке (~3 строки) |
| `ThirdPersonCamera.cs` | `SetTargetMode(target, isShip)` (+5 строк) |
| `MeziyModuleActivator.cs` | Поиск `MeziyNozzle` в детях (+10 строк) |

---

## ▸ Что НЕ делаем (сознательно)

| Не делаем | Почему |
|---|---|
| **Не добавляем NetworkObject на дочерние объекты** | Лишняя сложность для MVP. Вся логика через корневой NetworkObject. |
| **Не синхронизируем дверь по сети** | Каждый клиент проигрывает анимацию локально. Работает. |
| **Не переносим ShipController на место пилота** | Ломает Rigidbody, WindZone, всё. ShipController на корне. |
| **CargoSystem (T-CARGO-01..06, июль 2026)** | ✅ Готово как часть Trade v2 (CargoData POCO + TradeWorld._cargoCache + OnCargoChanged event + ShipCargoRegistry). См. `docs/Ships/cargo_system/CARGO_REFACTOR_PLAN_2026-06-17.md` и `SHIP_REFACTOR_PLAN_2026-07-21.md` P2. |
| **Не создаём .meta / .asmdef** | Запрещено AGENTS.md. |
| **Не трогаем BootstrapScene/ScenePlacedObjectSpawner** | Не требует изменений. |
| **Не трогаем AltitudeCorridorSystem** | Сценовый singleton, ничего не знает о структуре корабля. |

---

## ▸ Проверка совместимости с AGENTS.md

| Правило AGENTS.md | Соблюдение |
|---|---|
| `НЕ добавлять второй NetworkManager` | ✅ Не трогаем BootstrapScene |
| `НЕ переносить NetworkManager между сценами` | ✅ |
| `НЕ рефакторить ClientSceneLoader на NetworkSceneManager` | ✅ |
| `НЕ разворачивать WorldSceneManager/ServerSceneManager` | ✅ |
| `НЕ удалять ScenePlacedObjectSpawner` | ✅ |
| `Namespace: ProjectC.<Subsystem>` | ✅ ShipRootReference → `ProjectC.Ship` |
| `Private fields _camelCase` | ✅ |
| `НЕ писать .meta` | ✅ |
| `НЕ писать .asmdef` | ✅ |
| `Код только в Assets/_Project/Scripts/` | ✅ |

---

**Roadmap готов.** Когда скажешь — начинаем с Phase 0 (2 файла) или сразу Phase 1 (3 файла + правки). Ты расставляешь объекты в иерархии, я объясняю что куда и в инспекторе.
