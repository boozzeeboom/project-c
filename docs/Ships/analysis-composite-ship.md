# Analysis: Composite Ship Architecture for Project C

**Дата:** 2026-06-17
**Версия:** 1.0 (аналитическая, не код)
**Цель:** Определить архитектуру перехода от монолитного корабля (1 GameObject + ShipController) к составному (летающая баржа с площадкой, местом пилота, дверьми, модулями).
**Метод:** Глубокий анализ кода, legacy docs, подсистем сабагентами.

---

## 1. Executive Summary

### Проблема
Сейчас `ShipController` — NetworkBehaviour на одном GameObject с Rigidbody. Весь корабль — 1 куб. Подсистемы (Meziy, Fuel, Modules) висят на нём же. Для "летающей баржи" нужно:
- Игрок ходит по палубе пока корабль летит
- Место пилота — отдельный объект с триггером
- Дверь — отдельный объект с анимацией
- Будущие модули — отдельные объекты с взаимодействием

### Ключевые находки
| Аспект | Статус | Детали |
|--------|--------|--------|
| **ShipModuleManager** | ✅ **Готов** | Уже ищет ModuleSlot через `GetComponentsInChildren` |
| **WindZone** | ✅ **Готов** | Уже ищет ShipController через `GetComponentInParent/InChildren` |
| **MetaRequirement** | ✅ **Готов** | NetworkBehaviour на любом GameObject, для замков/дверей |
| **ShipController+Rigidbody** | ⚠️ **Рефакторинг** | Нужен поиск `GetComponentInParent<Rigidbody>()` |
| **NetworkPlayer.FindNearestShip** | ⚠️ **Рефакторинг** | Искать по `ShipRootReference`, не по ShipController |
| **ThirdPersonCamera** | ⚠️ **Рефакторинг** | target → корень корабля при входе, динамическая дистанция |
| **Место пилота (PilotSeat)** | 🆕 **Новый компонент** | Отдельный child с триггером + ссылка на ShipController |
| **DoorController** | 🆕 **Новый компонент** | NetworkBehaviour + NetworkVariable, slide-анимация |
| **CargoSystem** | ❌ **Отсутствует** | Класс не найден, ShipClass enum существует но CargoSystem как скрипта нет |
| **MeziyModuleActivator** | ⚠️ **Рефакторинг** | Центральный → распределённый по двигателям |

---

## 2. Текущая Архитектура (как есть)

### Диаграмма текущего монолитного корабля
```
Ship_Light (GameObject)
├── NetworkObject (NGO)
├── Rigidbody (mass=1000, kinematic=false)
├── BoxCollider (8×1.5×4)
├── ShipController (NetworkBehaviour)
│   ├── _rb → GetComponent<Rigidbody>()
│   └── ссылки на подсистемы serialized
├── ShipModuleManager (MonoBehaviour)
│   └── GetComponentsInChildren<ModuleSlot>()
├── ShipFuelSystem (MonoBehaviour)
├── MeziyModuleActivator (MonoBehaviour)
│   └── MeziyVisual (отдельный компонент, не NetworkBehaviour)
├── ShipDebugHUD (MonoBehaviour) — авто-добавляется
├── MeziyStatusHUD (MonoBehaviour) — авто-добавляется
└── ModuleSlot[] (дочерние, опционально)
    └── ShipModule (ScriptableObject)
```

### Поток посадки (F-key)
```
NetworkPlayer.Update()
  → F pressed
  → FindNearestShip() — FindObjectsByType<ShipController>()
  → ShipKeyClientState.RequestCanBoard(shipNetId) — проверка ключа
  → SubmitSwitchModeRpc (ServerRpc)
    → ShipController.AddPilot(clientId)
      → _pilots.Add(clientId)
      → Input начал суммироваться в FixedUpdate
  → ThirdPersonCamera.SetShipMode(true)
    → distance=18, height=6
```

### Проблемные точки для составного корабля
1. **ShipController.Rigidbody** — `GetComponent()` не найдёт Rigidbody если ShipController на child
2. **FindNearestShip** — не найдёт корабль если ShipController не на корне
3. **Camera target** — следит за игроком, не за корпусом корабля
4. **ScenePlacedObjectSpawner** — спавнит только корневой NetworkObject
5. **WindZone.TriggerEnter** — уже ищет в children — готов

---

## 3. Предлагаемая Архитектура Составного Корабля (MVP)

### Иерархия GameObjects
```
Ship_Root (GameObject)             ← NetworkObject + Rigidbody + NetworkTransform
├── ShipController (NetworkBehaviour)  ← ПО-ПРЕЖНЕМУ на корне
│   └── _rb → GetComponent<Rigidbody>() (работает, мы на корне)
├── ShipModuleManager (MonoBehaviour)
├── ShipFuelSystem (MonoBehaviour)
│
├── Deck (GameObject)              ← Площадка для ходьбы
│   └── BoxCollider (твёрдый, размер палубы)
│
├── PilotSeat (GameObject)         ← Место пилота (триггер F)
│   ├── BoxCollider (IsTrigger)    ← зона посадки
│   ├── ShipRootReference          ← ссылка на Ship_Root
│   ├── PilotSeatController       ← новый компонент
│   │   └── ShipController → ShipRootReference.ShipController
│   └── MetaRequirement           ← опционально (проверка ключа)
│
├── Door (GameObject)             ← Дверь (триггер E)
│   ├── BoxCollider (IsTrigger)
│   ├── DoorController (NetworkBehaviour)  ← анимация slide
│   │   └── NetworkVariable<bool> isOpen
│   ├── ShipRootReference
│   └── MetaRequirement (опционально — запертая дверь)
│
└── ModuleSlot[] (GameObjects)     ← Слоты модулей (как сейчас)
    ├── ModuleSlot (MonoBehaviour)
    └── ShipRootReference
```

### Ключевое решение: **ShipController остаётся на корне**

**Почему:**
1. `ShipController.GetComponent<Rigidbody>()` продолжает работать
2. `WindZone.OnTriggerEnter` находит ShipController через `GetComponentInParent` (любой child → корень)
3. `NetworkPlayer.FindNearestShip` может искать `ShipRootReference` (легковесный компонент) вместо ShipController
4. `ScenePlacedObjectSpawner` спавнит 1 NetworkObject (корень) — все children NetworkBehaviour автоматически получают IsSpawned
5. Rigidbody один на всю конструкцию — корректная физика

---

## 4. Анализ Подсистем (результаты сабагентов)

### 4.1 ShipModuleManager — ✅ ГОТОВ
```csharp
// Уже ищет в иерархии:
var discoveredSlots = new List<ModuleSlot>(GetComponentsInChildren<ModuleSlot>(true));
```
**Вывод:** Не требует изменений. Слоты на дочерних объектах будут найдены.

### 4.2 ShipModule / ModuleSlot — ✅ ГОТОВЫ
- ShipModule (ScriptableObject) — данные
- ModuleSlot (MonoBehaviour) — уже отдельный компонент на child
- **Вывод:** Не требуют изменений

### 4.3 ShipFuelSystem — ✅ ГОТОВ (для MVP)
- Может оставаться на корне
- **Вывод:** Для MVP не требует изменений. В будущем — топливные баки как отдельные компоненты с `IShipPart`

### 4.4 MeziyModuleActivator — ⚠️ РЕФАКТОРИНГ
**Опции:**
- **A (центральный):** Один активатор на корне, ищет `MeziyNozzle` компоненты в детях → управляет всеми
- **B (распределённый):** Каждый двигатель = свой MeziyModuleActivator + свой MeziyVisual

**Рекомендация:** **A** для MVP (меньше изменений). **B** когда появятся модульные двигатели.
```csharp
// Паттерн для A:
public class MeziyModuleActivator : MonoBehaviour {
    private List<MeziyNozzle> _nozzles;
    void Awake() => _nozzles = new(GetComponentsInChildren<MeziyNozzle>());
}
```

### 4.5 WindZone — ✅ ПАТТЕРН ДЛЯ СТАНДАРТИЗАЦИИ
```csharp
// Уже ищет в иерархии:
var ship = other.GetComponent<ShipController>();
if (ship == null) ship = other.GetComponentInParent<ShipController>();
if (ship == null) ship = other.GetComponentInChildren<ShipController>();
```
**Вывод:** Готов. Паттерн для стандартизации — создать `ShipComponentLocator`.

### 4.6 AltitudeCorridorSystem — ✅ НЕ ТРЕБУЕТ ИЗМЕНЕНИЙ
Сценовый singleton, работает с позицией корабля. Без изменений.

### 4.7 MetaRequirement (для дверей/модулей) — ✅ ГОТОВ
**Применимость к составному кораблю:**
- **Дверь:** MetaRequirement на Door GameObject → E-key проверяет ключ → разрешает/блокирует анимацию
- **Модуль:** MetaRequirement на модульном слоте → доступ к установке/демонтажу
- **Место пилота:** MetaRequirement (если место требует отдельного ключа)

**Важно:** В `NetworkPlayer.TryInteractNearestMetaRequirement()` есть фильтр:
```csharp
if (mr.GetComponent<ShipController>() != null) continue; // skip ships
```
**Менять не нужно** — он пропускает ТОЛЬКО корневой ShipController. Двери и модули ShipController не имеют → обрабатываются через E.

---

## 5. Специфические Рекомендации по Скриптам

### 5.1 ShipController.cs (Assets/_Project/Scripts/Player/ShipController.cs)

| Что менять | Для чего | Как |
|---|---|---|
| `_rb = GetComponent<Rigidbody>()` | Поддержка Rigidbody на корне (или дополнительный fallback) | Оставить как есть (ShipController на корне → `GetComponent` работает) |
| `_pilots: HashSet<ulong>` → `Dictionary<ulong, PilotSeat>` | Multi-crew, учёт мест | Только для Phase 4+. Для MVP — оставить HashSet |
| `ShipRoot` property | Доступ к корню корабля из любого компонента | `Transform ShipRoot => transform.root` |
| Serialized ссылки | Оставить serialized, они валидны (компоненты на корне) | Без изменений |

### 5.2 NetworkPlayer.cs (Assets/_Project/Scripts/Player/NetworkPlayer.cs)

| Что менять | Для чего | Как |
|---|---|---|
| `FindNearestShip()` | Находить составной корабль по любой его части | Искать `ShipRootReference` вместо `ShipController`. Или искать ShipController через `FindObjectsByType<ShipController>()` (он по-прежнему на корне — найдётся) |
| Camera target при входе | Камера показывает весь корабль | Передавать камере `_currentShip.ShipRoot` вместо `transform` |
| `SubmitSwitchModeRpc(ulong seatNetworkId)` | Указывать конкретное место пилота | Расширить RPC параметром (или для MVP seatId = 0, первый свободный) |

### 5.3 ThirdPersonCamera.cs (Assets/_Project/Scripts/Core/ThirdPersonCamera.cs)

| Что менять | Для чего | Как |
|---|---|---|
| `SetTarget(Transform)` | Переключение на корень корабля | Новый метод `SetTargetMode(Transform, bool isShip)` |
| `shipDistance/shipHeight` → динамика | Авто-расчёт от размеров корабля | Вычислять Bounds всех Renderer/Collider в children корабля → distance = maxDimension * 2.5 |
| `LateUpdate` позиция | Центровка на корпусе, не на игроке | Использовать `target.position` + `Bounds.center` корабля для LookAt |

### 5.4 ScenePlacedObjectSpawner (BootstrapScene)

| Что менять | Для чего | Как |
|---|---|---|
| Спавн дочерних NetworkObject | Если Door или модули — отдельные NetworkObject в children | ScenePlacedObjectSpawner уже спавнит все `!IsSpawned` NetworkObject в сцене. Если дочерний объект — scene-placed, его спавн происходит автоматически при корневом. Если nested NetworkObject — надо проверить поведение NGO. |
| **Рекомендация:** | Упрощение | **Для MVP не делать дочерние объекты отдельными NetworkObject**. Анимация двери — локальная (каждый клиент проигрывает сам). |

---

## 6. НОВЫЕ Компоненты (нужно создать)

### 6.1 ShipRootReference (MonoBehaviour)
```csharp
namespace ProjectC.Ship {
    /// Простейший маркер-ссылка на корень корабля.
    /// Позволяет любой части корабля найти ShipController.
    [DefaultExecutionOrder(-100)]
    public class ShipRootReference : MonoBehaviour {
        public ShipController ShipController { get; private set; }
        public Rigidbody ShipRigidbody { get; private set; }
        public NetworkObject ShipNetworkObject { get; private set; }

        private void Awake() {
            Transform root = transform.root;
            ShipController = root.GetComponent<ShipController>();
            ShipRigidbody = root.GetComponent<Rigidbody>();
            ShipNetworkObject = root.GetComponent<NetworkObject>();
        }
    }
}
```
**Назначение:**
- Вешается на каждый дочерний объект (PilotSeat, Door, ModuleSlot)
- Даёт любому внешнему скрипту (WindZone, NetworkPlayer) найти ShipController через `GetComponentInParent<ShipRootReference>()`
- Замена цепочки `GetComponentInParent<ShipController>()` на единый интерфейс

### 6.2 PilotSeatController (MonoBehaviour)
```csharp
namespace ProjectC.Ship {
    /// Место пилота на составном корабле.
    /// Триггерная зона. При F → ShipController.AddPilot(clientId).
    public class PilotSeatController : MonoBehaviour {
        [SerializeField] private ShipRootReference shipRoot;
        private ShipController _shipController;
        private Collider _trigger;

        private void Awake() {
            if (shipRoot == null) shipRoot = GetComponentInParent<ShipRootReference>();
            _shipController = shipRoot?.ShipController;
            _trigger = GetComponent<Collider>();
            if (_trigger != null) _trigger.isTrigger = true;
        }

        public bool TryBoard(ulong clientId) {
            if (_shipController == null) return false;
            return _shipController.AddPilot(clientId, this);
        }
        
        public void Exit(ulong clientId) {
            _shipController?.RemovePilot(clientId);
        }
    }
}
```
**Назначение:**
- Отдельный компонент на GameObject-месте пилота
- F-key проверяет не ShipController напрямую, а PilotSeatController
- Позволяет множественные места пилотов (multi-crew)
- **Для MVP:** PilotSeatController на child = место пилота; ShipController на корне;

### 6.3 ShipComponentLocator (static helper)
```csharp
namespace ProjectC.Ship {
    /// Стандартизированный поиск ShipController из любой части корабля.
    /// Замена ручных GetComponentInParent/InChildren в WindZone и др.
    public static class ShipComponentLocator {
        public static ShipController FindShip(GameObject from) {
            // 1. Прямой поиск
            var sc = from.GetComponent<ShipController>();
            if (sc != null) return sc;
            // 2. Через ShipRootReference
            var ref_ = from.GetComponentInParent<ShipRootReference>();
            if (ref_ != null) return ref_.ShipController;
            // 3. Fallback через родителя
            sc = from.GetComponentInParent<ShipController>();
            if (sc != null) return sc;
            // 4. Fallback через детей (для прямых триггеров)
            sc = from.GetComponentInChildren<ShipController>();
            return sc;
        }
    }
}
```
**Назначение:**
- Единая точка поиска ShipController
- Замена разрозненных `GetComponent`/`GetComponentInParent`/`GetComponentInChildren` по всему проекту
- Сначала проверяет ShipRootReference (легковесный, быстрый)

### 6.4 DoorController (NetworkBehaviour — опционально для MVP)
```csharp
namespace ProjectC.Ship {
    /// Дверь на корабле с анимацией slide.
    /// Для MVP — локальная анимация (без NetworkVariable).
    public class DoorController : MonoBehaviour {
        [SerializeField] private Vector3 slideDirection = Vector3.right;
        [SerializeField] private float slideDistance = 2f;
        [SerializeField] private float slideSpeed = 1f;
        
        private Vector3 _closedPos;
        private Vector3 _openPos;
        private bool _isOpen = false;
        
        private void Awake() {
            _closedPos = transform.localPosition;
            _openPos = _closedPos + slideDirection.normalized * slideDistance;
        }
        
        public void Toggle() => StartCoroutine(AnimateSlide(_isOpen ? _closedPos : _openPos));
        
        private IEnumerator AnimateSlide(Vector3 target) {
            // Lerp localPosition
        }
    }
}
```
**Назначение:**
- Анимация открытия/закрытия
- **MVP:** локальная, без сети (все клиенты проигрывают свою анимацию)
- **Phase 2:** NetworkBehaviour + `NetworkVariable<bool> isOpen` для синхронизации

---

## 7. Паттерны для Стандартизации

### 7.1 Поиск частей корабля (уже есть)
```csharp
// ✅ ShipModuleManager — используем как шаблон
GetComponentsInChildren<ModuleSlot>(true)

// ✅ WindZone — используем как шаблон
component.GetComponentInParent<ShipController>()
```

### 7.2 Для будущего: интерфейс IShipPart
```csharp
public interface IShipPart {
    ShipController ShipController { get; }
}
```
Не реализовывать сейчас — дождаться Phase 4+.

### 7.3 E-взаимодействие на составном корабле
```csharp
// NetworkPlayer.Update() — текущая логика:
if (Keyboard.current.eKey.wasPressedThisFrame) {
    // Уже ищет MetaRequirement по всей сцене
    // фильтр: пропускает ShipController (корень корабля)
    // НО не пропускает Door, ModuleSlot (у них нет ShipController)
    TryInteractNearestMetaRequirement();
}
```
**Вывод:** E-key работает как есть — двери и модули с MetaRequirement будут находиться.

---

## 8. Network Architecture

### Корневой NetworkObject
- Ship_Root имеет `NetworkObject` + `NetworkTransform` (server-authoritative)
- ShipController — NetworkBehaviour на корне, работает как сейчас
- Все children — **НЕ имеют собственного NetworkObject** (для MVP)
- Rigidbody на корне → физика цельной конструкции

### Синхронизация анимаций (MVP)
- **Дверь:** каждый клиент проигрывает анимацию локально при триггере E
- **Вход/выход:** RPC как сейчас (`SubmitSwitchModeRpc`), но с параметром `seatNetworkId`
- **Позиция детей:** фиксирована относительно корня (transform локальные координаты)

### Будущая сетевая модель (Phase 4+)
- Поворотные турели/модули — отдельные NetworkObject с `NetworkTransform`
- Parent sync: прикрепление/открепление модулей
- Cargo: распределённый вес по палубе

---

## 9. MVP Roadmap (Фазы реализации)

### Phase 1: PilotSeat + ShipRootReference (оценка: 1 сессия)
**Цель:** Корабль остаётся монолитным, но имеет отдельное место пилота.

**Задачи:**
1. Создать `ShipRootReference` (MonoBehaviour, [DefaultExecutionOrder(-100)])
2. Создать `PilotSeatController` (триггер, ссылается на ShipController через ShipRootReference)
3. Создать `ShipComponentLocator` (static helper)
4. Исправить `NetworkPlayer.FindNearestShip()` — искать ShipRootReference или PilotSeatController
5. Исправить `NetworkPlayer.SubmitSwitchModeRpc` — передавать seatId (для MVP = 0)
6. Обновить `ThirdPersonCamera` — при входе target = корень корабля
7. **Иерархия (объяснить пользователю):**
   - Ship_Root (ShipController, Rigidbody, NetworkObject, как сейчас)
   - └── PilotSeat (PilotSeatController, ShipRootReference, BoxCollider-триггер)

### Phase 2: Door (оценка: 1 сессия)
**Цель:** На палубе есть дверь, которая открывается/закрывается.

**Задачи:**
1. Создать `DoorController` (анимация slide, локальная)
2. Если дверь заперта — настроить `MetaRequirement` на дверном GameObject
3. **Иерархия (объяснить пользователю):**
   - Ship_Root
   - ├── Deck (BoxCollider)
   - └── Door (DoorController, ShipRootReference, BoxCollider-триггер + опционально MetaRequirement)

### Phase 3: Module Refactor (оценка: 1-2 сессии)
**Цель:** Meziy-двигатели — отдельные объекты с визуалом.

**Задачи:**
1. Создать `MeziyNozzle` (MonoBehaviour, визуал выхлопа + ссылка на Slot)
2. `MeziyModuleActivator` → ищет `MeziyNozzle` в детях
3. **Иерархия (объяснить пользователю):**
   - Ship_Root
   - ├── Meziy_Thruster (MeziyNozzle, ModuleSlot, ShipRootReference)
   - └── Meziy_Thruster_2 (то же)

### Phase 4+: Multi-crew + Cargo + Modules (отдельные сессии)
- `ShipController._pilots` → `Dictionary<ulong, PilotSeatController>`
- CargoSystem (создать с нуля)
- Компоненты с `NetworkObject` для синхронизации

---

## 10. Риски и Gotcha

### 10.1 ScenePlacedObjectSpawner + nested NetworkObject
Если дочерний GameObject имеет свой `NetworkObject`, NGO может не заспавнить его автоматически. `ScenePlacedObjectSpawner` спавнит только NetworkObject верхнего уровня.

**Решение для MVP:** Не делать children отдельными NetworkObject. Вся логика — через корневой NetworkObject.

### 10.2 Rigidbody на корне + дочерние Collider
Несколько BoxCollider на children (палуба + дверь + модули) + один Rigidbody на корне = корректная физика. Rigidbody автоматически собирает все коллайдеры в children.

### 10.3 ThirdPersonCamera при большом корабле
Сейчас shipDistance=18f, shipHeight=6f жёстко зашиты. Для корабля 32×16×8 метров камера будет показывать только часть.

**Решение:** Вычислять distance динамически в `ShipRootReference.Awake()`:
```csharp
float maxDimension = Mathf.Max(size.x, size.y, size.z);
ShipController.shipCameraDistance = maxDimension * 2.5f;
```

### 10.4 Игрок становится child корабля (текущее поведение)
Сейчас игрок (`_controller.enabled = false`) становится не child, а просто отключается. При составном корабле:
- Если игрок на палубе и корабль движется, игрок должен стоять на палубе (физически)
- Вариант A: Игрок становится child корабля → двигается с ним (просто, работает)
- Вариант B: Игрок — отдельный Rigidbody, корабль его толкает (сложно, проблемы)

**Рекомендация для MVP:** Вариант A — при входе `transform.parent = shipRoot.transform`. При выходе `transform.parent = null`.

### 10.5 NetworkTransform и дочерние объекты
`NetworkTransform` на корне реплицирует только позицию/поворот корня. Дочерние объекты синхронизируются через локальные координаты автоматически (NGO не трогает локальные transform).

---

## 11. Что НЕ МЕНЯЕТСЯ (безопасно)

| Компонент | Не меняется | Почему |
|---|---|---|
| ShipController (на корне) | Логика полёта, _pilots, FixedUpdate | Остаётся на корне, Rigidbody доступен |
| ShipModuleManager | GetComponentsInChildren<ModuleSlot> | Уже ищет по всей иерархии |
| ShipFuelSystem | MonoBehaviour | Может остаться на корне |
| AltitudeCorridorSystem | Сценовый singleton | Не зависит от структуры корабля |
| WindZone | Триггер + поиск ShipController | Уже ищет в parent/children |
| MetaRequirementRegistry | Server-Client push | Не зависит от иерархии |
| ScenePlacedObjectSpawner | Спавн scene-placed NetworkObject | Корневой NetworkObject один |
| ShipKeyClientState | F-key проверка | Работает для места пилота |

---

## 12. Сводная таблица изменений по файлам

| Файл | Статус | Изменения |
|---|---|---|
| `ShipController.cs` | ⚠️ Минимальные | ShipRoot property; AddPilot(ulong, PilotSeat) overload (Phase 4) |
| `NetworkPlayer.cs` | ⚠️ Средние | FindNearestShip + PilotSeat; camera target switch; SubmitSwitchModeRpc + seatId |
| `ThirdPersonCamera.cs` | ⚠️ Средние | SetTargetMode (target + isShip); динамический distance |
| `**NEW** ShipRootReference.cs` | 🆕 Создать | Маркер на каждом child-объекте корабля |
| `**NEW** PilotSeatController.cs` | 🆕 Создать | Триггер места пилота |
| `**NEW** ShipComponentLocator.cs` | 🆕 Создать | Static helper для поиска ShipController |
| `**NEW** DoorController.cs` | 🆕 Создать | Анимация slide (локальная для MVP) |
| `WindZone.cs` | ♻️ Опционально | Может использовать ShipComponentLocator вместо ручного поиска |
| `MeziyModuleActivator.cs` | 🔄 Phase 3 | Поиск MeziyNozzle в детях вместо прямых ссылок |
| `ScenePlacedObjectSpawner.cs` | ❌ Без изменений | Пока не требует nested NetworkObject |

---

**Вывод:** Переход к составному кораблю возможен с минимальным рефакторингом (Phase 1: ~5 файлов, 1 сессия). Ключевые паттерны (GetComponentsInChildren, поиск ShipController через иерархию) уже заложены в legacy архитектуре. Основные изменения — новые компоненты-маркеры (ShipRootReference, PilotSeatController), а не модификация существующей логики полёта.
