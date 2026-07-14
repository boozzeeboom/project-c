# Engine Visual System — Анализ подсистем и план реализации

**Дата:** 2026-07-14
**Статус:** 🟢 Завершено (2026-07-14)
**Коммит:** `67d008a`
**Тикет:** T-ENG02 (замена T-ENG01)
**Постмортем:** `T-ENG01_ShipEngineVisual_PostMortem.md`

---

## Содержание

1. [Что пошло не так в T-ENG01](#1-что-пошло-не-так-в-t-eng01)
2. [Глубокий анализ затронутых подсистем](#2-глубокий-анализ-затронутых-подсистем)
3. [Новый подход: компонентная архитектура](#3-новый-подход-компонентная-архитектура)
4. [План реализации (по этапам)](#4-план-реализации-по-этапам)
5. [Карта зависимостей](#5-карта-зависимостей)
6. [Проверочный чеклист](#6-проверочный-чеклист)
7. [Что НЕ делаем](#7-что-не-делаем)

---

## 1. Что пошло не так в T-ENG01

### 1.1 Диагноз из постмортема (верное)

| Проблема | Вердикт |
|---|---|
| `GlobalObjectIdHash` перегенерация при сохранении BootstrapScene | 🔴 **Критическая** — сломала RPC-маршрутизацию NGO. Все 18 NetworkObject'ов сменили хеши. |
| `_shipWindMultiplier: 1 → 10` случайно | 🟡 **Побочная** — ветер стал ×10. |
| `ApplyDeflection` — прямая `transform.localRotation` в `Update()` | 🟡 **Потенциальная** — конфликт с Rigidbody если слот на объекте с RB. |

### 1.2 Корневая причина (что НЕ было сказано в постмортеме)

**Недостаточный анализ codebase перед реализацией.**

T-ENG01 создал `ShipEngineVisual` как standalone-компонент, который:
- Не использовал существующий `ShipModuleVisualApplier` (уже готов! L1 визуал)
- Не использовал `ShipRootReference` / `ShipComponentLocator` для доступа к `ShipController`
- Пытался управлять `transform.localRotation` вручную, хотя вся физика корабля — через `Rigidbody.AddForce/AddTorque` в `FixedUpdate`
- Был повешен на сам слот (который может быть на Rigidbody-объекте)

**Вывод:** Нужно не чинить старый `ShipEngineVisual`, а спроектировать новую архитектуру, **опираясь на уже существующие паттерны** проекта.

### 1.3 Что уже есть (и мы не используем)

| Существующий код | Почему не был использован |
|---|---|
| `ShipModuleVisualApplier.cs` (L1, 196 строк) | Не знали о его существовании |
| `ShipRootReference.cs` | Не знали |
| `ShipModuleManager.slots` (список слотов) | Не использовали |
| `ShipController.ApplyServerInput` (server-only) | Пытались делать на клиенте |
| `ShipTelemetryState` (NetworkVariable для HUD) | Не использовали |

---

## 2. Глубокий анализ затронутых подсистем

### 2.1 SlotType enum — где добавлять Engine

**Файл:** `Assets/_Project/Scripts/Ship/ModuleSlot.cs`

```csharp
public enum SlotType
{
    Propulsion,   // Слот для модулей движения
    Utility,      // Слот для утилит
    Special       // Слот для специальных модулей
}
```

**Нам нужно добавить:** `Engine`

> ⚠️ **Важно:** `SlotType` кастится к `ModuleType` через `(ModuleType)slotType` в `ModuleSlot.ValidateCompatibility()`. Добавление `Engine` в `SlotType` потребует **одновременного** добавления `Engine` в `ModuleType` (тот же порядковый номер!).

### 2.2 ModuleType enum — зеркальное отражение

**Файл:** `Assets/_Project/Scripts/Ship/ShipModule.cs`

```csharp
public enum ModuleType
{
    Propulsion,   // 0
    Utility,      // 1
    Special       // 2
}
```

**Нужно добавить:** `Engine` (на позиции 3, ИЛИ между Propulsion и Utility — решить)

**Дилемма порядка:**
- Если вставить `Engine` между `Propulsion` (0) и `Utility` (1) → `(int)SlotType.Engine != (int)ModuleType.Engine` — сломается приведение в `ModuleSlot.ValidateCompatibility()`
- Если добавить `Engine` в конец обоих enum'ов (позиция 3) — прозрачно

**Решение:** Добавить `Engine` **в конец** обоих enum'ов, позиция 3.

### 2.3 ShipModule (ScriptableObject) — visual поля уже есть!

**Файл:** `Assets/_Project/Scripts/Ship/ShipModule.cs` (строки 124-144)

```csharp
[Header("Visual (L1 — module visualPrefab)")]
public GameObject visualPrefab;
public string visualSocketPath = "";
public Vector3 attachPositionOffset = Vector3.zero;
public Vector3 attachRotationOffset = Vector3.zero;
public Vector3 attachScale = Vector3.one;
public ModuleAttachAxis attachAxis = ModuleAttachAxis.Slot;
public ModuleColliderMode colliderMode = ModuleColliderMode.None;
```

**Статус:** ✅ Поля уже есть. Новые модули типа `Engine` смогут их использовать.

**Что НЕ хватает:**
- Полей для настройки **двигателя** как такового (угол отклонения, вращение лопастей) — их не будет в `ShipModule`, они будут в новом `EngineThrusterVisual` компоненте на слоте (см. §3).

### 2.4 ShipModuleVisualApplier — уже спавнит визуал

**Файл:** `Assets/_Project/Scripts/Ship/ShipModuleVisualApplier.cs`
**Статус:** ✅ Полностью готов. Подписан на `ShipModuleServer.OnModuleChanged`, спавнит `visualPrefab` под ModuleSlot.transform.

**Как работает:**
1. `Awake()` — кеширует `ShipModuleManager`
2. `OnNetworkSpawn()` — подписывается `ShipModuleServer.OnModuleChanged += OnModuleChanged`
3. `ApplyAllFromManager()` — итерирует `manager.slots`, для каждого занятого слота с `visualPrefab` → спавнит/заменяет визуал
4. Визуал парентится к `slot.transform` (с опциональным `visualSocketPath`)

**Как это относится к Engine:**
- Когда на Engine-слот будет установлен модуль с `visualPrefab` — `ShipModuleVisualApplier` **автоматически** заспавнит prefab.
- На этом prefab'е или на самом слоте будет висеть `EngineThrusterVisual` для анимации.

### 2.5 ShipController — цепочка тяги (server-authoritative)

**Файл:** `Assets/_Project/Scripts/Player/ShipController.cs`

**Цепочка ввода → тяга:**

```
ShipInputReader (клиент)
  → SendShipInput(thrust, yaw, pitch, vertical, boost)
    → [Rpc] SubmitShipInputRpc (сервер)
      → _sumThrust += thrust; _sumYaw += yaw;
        → FixedUpdate:
          1. avgThrust = _sumThrust / n
          2. ApplyModuleModifiers() → _moduleThrustMult
          3. targetThrust = avgThrust * thrustForce * _moduleThrustMult * hullSpeedMult
          4. _currentThrust = SmoothDamp(_currentThrust, targetThrust, ...)
          5. ApplyThrustForce(_currentThrust)
            → _rb.AddForce(transform.forward * currentThrust * cargoPenalty, ForceMode.Force)
```

**Где живёт `currentThrust`:** приватное поле `_currentThrust` (line 429) — доступно через reflection только на сервере.

**Доступность с клиента:** `ShipTelemetryState` (NetworkVariable, обновляется ~5 Гц) содержит `fuelNormalized`, НО **не содержит** `thrustNormalized`.

**Что нужно для Engine Visual:**
- `currentThrust` → клиенту нужно знать текущую тягу (нормализованную 0..1)
- Текущий yaw input → для угла отклонения

**Пути получения этих данных на клиенте:**
1. **Через NetworkVariable.** Добавить `thrustNormalized` в `ShipTelemetryState` — сервер пишет, все клиенты читают. _Надёжно, но 5 Гц может быть недостаточно для smooth-анимации._
2. **Локальная аппроксимация.** `EngineThrusterVisual` на клиенте аппроксимирует thrust из `rb.linearVelocity.magnitude` / `maxSpeed`. _Неточное._
3. **ClientRpc с высокой частотой.** Слишком дорого.
4. **Локальный InputState на клиенте.** Если пилот — локальный игрок, можно читать `ShipInputReader.IsActionHeld(ShipThrustForward)` напрямую и аппроксимировать smooth thrust локально. _Для Engine Visual — самый лёгкий путь._ Проверено в `PlayerInputReader.cs`.

**Решение для Engine Visual:** Гибридный подход (см. §3.3).

### 2.6 ShipTelemetryState — что передаётся HUD

**Файл:** `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs`

```csharp
public struct ShipTelemetryState : INetworkSerializable, IEquatable<ShipTelemetryState>
{
    public ulong shipNetworkObjectId;
    public float fuelNormalized;
    // ... (нет thrustNormalized, нет yawNormalized)
}
```

**Добавить:** `thrustNormalized` (byte 0-255 для экономии bandwidth).

### 2.7 Иерархия корабля (Ship_Light_root)

На основе анализа Composite Ship Summary и ShipRootReference:

```
Ship_Root (NetworkObject, Rigidbody, ShipController, ShipModuleManager, ShipModuleServer)
├── Engine_Left                ← ModuleSlot (SlotType.Propulsion)
│   └── (сюда спавнится visualPrefab модуля через ShipModuleVisualApplier)
├── Engine_Right               ← ModuleSlot (SlotType.Propulsion)
│   └── (сюда спавнится visualPrefab модуля)
├── Engine_Meziy               ← ModuleSlot (SlotType.Special)
├── PilotSeat                  ← PilotSeatController
├── Door                       ← DoorController
└── (другие слоты)
```

**Новая иерархия с Engine-слотами:**

```
Ship_Root
├── Slot_Engine_Left           ← ModuleSlot (SlotType.Engine)
│   └── Propeller_Left         ← GameObject (назначается в EngineThrusterVisual.propellerObject)
│       └── (меш лопастей)
├── Slot_Engine_Right          ← ModuleSlot (SlotType.Engine)
│   └── Propeller_Right
├── Engine_Left                ← ModuleSlot (SlotType.Propulsion) — остаётся как legacy
├── Engine_Right               ← ModuleSlot (SlotType.Propulsion) — остаётся как legacy
└── ...
```

**Важно:** Новые `Slot_Engine_*` слоты — это **новые дочерние GameObject'ы** на руте корабля, не замена существующим `Engine_Left/Right`. Они стоят рядом.

---

## 3. Новый подход: компонентная архитектура

### 3.1 EngineThrusterVisual — стержневой компонент

**Назначение:** Компонент на `ModuleSlot` (или его дочернем объекте) типа `Engine`, отвечающий за визуальную анимацию двигателя: вращение лопастей + отклонение в направлении поворота.

**Паттерн:** Чистый клиентский визуал. Никаких RPC. Никакой мутации Rigidbody.

**Где висит:** На `Slot_Engine_Left`/`Slot_Engine_Right` (на самом `ModuleSlot`), **или** на дочернем `Visual` объекте.

```csharp
// T-ENG02: EngineThrusterVisual — визуальный компонент двигателя
// Размещается на ModuleSlot GameObject (тип Engine) или его дочернем объекте.
// Использует ShipRootReference для доступа к ShipController.
// Никаких RPC, никакой модификации Rigidbody. Client-side only.

namespace ProjectC.Ship.Engine
{
    public class EngineThrusterVisual : MonoBehaviour
    {
        [Header("Propeller")]
        [Tooltip("3D объект лопастей (дочерний от этого Transform). Вращается вокруг локальной оси Z.")]
        [SerializeField] private Transform _propeller;

        [Tooltip("Скорость вращения на полной тяге (об/сек). Отрицательное = обратное вращение.")]
        [SerializeField] private float _maxRpm = 10f;

        [Tooltip("Ось вращения лопастей в локальном пространстве propeller-объекта.")]
        [SerializeField] private Vector3 _rotationAxis = Vector3.forward;

        [Header("Deflection (поворот двигателя)")]
        [Tooltip("Максимальный угол отклонения при полном yaw (градусы). 0 = не отклоняется. Отрицательное = инверсия.")]
        [SerializeField] private float _maxDeflectionAngle = 40f;

        [Tooltip("Плавность следования отклонения (сек).")]
        [SerializeField] private float _deflectionSmoothTime = 0.3f;

        [Header("Dependencies")]
        [Tooltip("ShipRootReference на этой или родительской части корабля. Авто-поиск если null.")]
        [SerializeField] private ShipRootReference _rootRef;

        // Приватное состояние
        private ShipController _shipController;  // кеш ShipController
        private float _currentAngle;             // SmoothDamp state for deflection
        private float _angleVelocity;            // SmoothDamp velocity
        private float _currentRpm;               // текущая RPM (плавно следует за thrust)

        // ============================================================
        // Настройки по умолчанию — вынесены в инспектор
        // ============================================================
        // Если _maxDeflectionAngle = 0 — отклонение отключено (жёсткая фиксация)
        // Если _maxRpm = 0 — вращение отключено
    }
}
```

### 3.2 Как EngineThrusterVisual получает данные о тяге

Варианты (выбрать на этапе проектирования):

| Вариант | Плюсы | Минусы |
|---|---|---|
| **A. ShipTelemetryState** (NetworkVariable + `thrustNormalized`) | Единый источник правды, работает для всех клиентов | 5 Гц throttle — может быть дёргано, 0.2с latency |
| **B. ShipInputReader.IsActionHeld (локально)** + аппроксимация | Мгновенно, без лага | Работает только когда игрок — пилот. Не для remote-клиентов |
| **C. ShipController._currentThrust через public getter** (если добавить) | Авторитативно | Требует NetworkVariable для remote-клиентов |
| **D. Гибрид: B для пилота + A для наблюдателей** | Лучший UX | Сложнее реализация |

**Рекомендация:** Начать с **B** (локальный ввод + аппроксимация) — это покрывает worst-case (пилот всегда видит свой двигатель). Добавить `thrustNormalized` в `ShipTelemetryState` отдельным тикетом для remote-наблюдателей.

### 3.3 Алгоритм работы

```
EngineThrusterVisual.Update():
  1. Получить ShipController через ShipRootReference
     - Если null → return (нет корабля)

  2. Получить локальный thrustNormalized:
     - Если сеть не готова → return
     - Если этот клиент — пилот (IsOwner или _pilots.Contains):
       - Через ShipInputReader: held? + аппроксимация smooth ramp
     - Иначе (наблюдатель):
       - Через ShipTelemetryState.thrustNormalized (когда добавим)

  3. Propeller rotation:
     - _currentRpm = SmoothDamp(_currentRpm, thrustNormalized * _maxRpm, ...)
     - _propeller.Rotate(_rotationAxis, _currentRpm * 360 * dt, Space.Self)

  4. Deflection (yaw):
     - Получить yaw input (через ShipInputReader или telemetry)
     - targetAngle = yawInput * _maxDeflectionAngle
     - _currentAngle = SmoothDamp(_currentAngle, targetAngle, ref _angleVelocity, _deflectionSmoothTime)
     - transform.localRotation = Quaternion.Euler(0, _currentAngle, 0)  // поворот вокруг локальной Y

  5. Всё. Никакой модификации Rigidbody. Никаких RPC.
```

### 3.4 Определение «пилот ли я?»

ShipController уже хранит `_pilots: HashSet<ulong>` (line 419). Нужен публичный метод:
```csharp
public bool IsLocalPlayerPiloting()
{
    // Проверить NetworkManager.Singleton.LocalClientId
    // Проверить _pilots.Contains или _netEngineRunning
}
```

**Проще:** Добавить публичный `bool IsPilotedByLocalClient { get; }` в ShipController.

### 3.5 ShipInputReader — чтение текущего ввода

**Файл:** `Assets/_Project/Scripts/Player/ShipInputReader.cs`

```csharp
public class ShipInputReader : MonoBehaviour
{
    public float CurrentThrustNormalized { get; private set; } // 0..1
    public float CurrentYawNormalized { get; private set; }    // -1..1
    public float CurrentPitchNormalized { get; private set; }  // -1..1
}
```

**Надо проверить:** есть ли уже такие геттеры. Если нет — добавить.

---

## 4. План реализации (по этапам)

### Этап 0: Подготовка (не трогать BootstrapScene)

**Правила (из постмортема):**
1. ☠️ **НЕ сохранять BootstrapScene при любых изменениях.** Все тесты в WorldScene или TempScene.
2. ☠️ **НЕ менять GlobalObjectIdHash** через добавление компонентов на префабы NetworkObject в BootstrapScene.
3. ✅ Если нужно добавить компонент на корабль — добавлять **в WorldScene_0_0**, на уже заспавненный экземпляр.
4. ✅ Все изменения `ShipController.cs` — через git diff до/после, проверяя что не сломали существующее.

### Этап 1: SlotType.Engine + ModuleType.Engine

**Файлы:** `ModuleSlot.cs`, `ShipModule.cs`

1. Добавить `Engine` в конец `SlotType` enum
2. Добавить `Engine` в конец `ModuleType` enum (та же позиция, чтобы `(int)SlotType.Engine == (int)ModuleType.Engine`)

**Валидация:** Проверить `ModuleSlot.ValidateCompatibility()` — строка 90: `module.type == (ModuleType)slotType`. Если enum'ы синхронны — работает без изменений.

### Этап 2: EngineThrusterVisual.cs — создание компонента

**Новый файл:** `Assets/_Project/Scripts/Ship/Engine/EngineThrusterVisual.cs`
**Namespace:** `ProjectC.Ship.Engine`

1. Написать компонент с полями `_propeller`, `_maxRpm`, `_rotationAxis`, `_maxDeflectionAngle`, `_deflectionSmoothTime`
2. Публичный геттер `thrustNormalized` — получает через `ShipInputReader` если пилот, иначе через `ShipTelemetryState` (заглушка 0 для MVP)
3. `Update()` — вращение + отклонение, никакой модификации Rigidbody
4. Проверка `ShipRootReference` — если null, `GetComponentInParent<ShipRootReference>()` при старте

**Антипаттерны:**
- ❌ Не вызывать `_rb.AddForce / MoveRotation`
- ❌ Не модифицировать `transform` на объекте с Rigidbody
- ❌ Не отправлять RPC
- ❌ Не использовать `FindObjectOfType` в Update
- ✅ Использовать `ShipRootReference` или `GetComponentInParent`

### Этап 3: ShipInputReader — публичные геттеры

**Файл:** `Assets/_Project/Scripts/Player/ShipInputReader.cs`

1. Добавить публичные геттеры:
   - `float ThrustNormalized { get; }` (0..1)
   - `float YawNormalized { get; }` (-1..1)
   - `float PitchNormalized { get; }` (-1..1)

2. **Проверить** — не задублировать существующие методы. Если уже есть `GetThrust()` — использовать его.

### Этап 4: ShipTelemetryState — thrustNormalized (опционально)

**Файл:** `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs`

1. Добавить `byte thrustNormalized;` (0-255 → нормализовано 0..1)
2. Сериализация в `NetworkSerialize()`
3. `Equals()` — включить поле
4. ShipController.UpdateTelemetryState() — заполнять поле:
   ```csharp
   float norm = Mathf.Clamp01(Mathf.Abs(_currentThrust) / (thrustForce * _moduleThrustMult));
   telemetry.thrustNormalized = (byte)(norm * 255);
   ```

### Этап 5: Настройка в сцене

1. Открыть WorldScene_0_0 (НЕ BootstrapScene)
2. Найти Ship_Light_root (или тестовый корабль)
3. Добавить дочерние GameObject'ы:
   - `Slot_Engine_Left` → `ModuleSlot` (SlotType.Engine)
   - `Slot_Engine_Right` → `ModuleSlot` (SlotType.Engine)
4. На каждый слот:
   - Добавить `EngineThrusterVisual`
   - Назначить `_propeller` (дочерний меш)
5. Нарут корабля:
   - Убедиться что `ShipModuleVisualApplier` есть (если нет — добавить)
6. Создать `ShipModule` ScriptableObject:
   - `MODULE_ENGINE_BASIC.asset` (type=Engine, visualPrefab=префаб корпуса двигателя)
7. Проверить что `ShipModuleManager.Initialize()` находит новые слоты (line 42: `GetComponentsInChildren<ModuleSlot>` — да, найдёт)

### Этап 6: Проверка

1. Compile check — 0 errors
2. Play Mode в WorldScene_0_0
3. Зайти в PilotSeat → двигатель включён
4. W/S — лопасти вращаются, пропорционально тяге
5. A/D — двигатели отклоняются по yaw
6. Проверить что корабль движется (не сломали управление)
7. `git diff Assets/_Project/Scripts/Player/ShipController.cs` — изменений нет или минимальны

---

## 5. Карта зависимостей

```
SlotType.Engine ─────────────────────────► ModuleType.Engine (зеркально)
                                                │
                                                ▼
                                         ShipModule (SO)
                                         ├── type = Engine
                                         ├── thrustMultiplier
                                         └── visualPrefab ← ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┐
                                                │                                                 │
                                                ▼                                                 ▼
                                         ShipModuleManager.ModuleSlot (SlotType.Engine)    ShipModuleVisualApplier
                                         ├── installedModule (ShipModule)                  ├── OnModuleChanged
                                         └── EngineThrusterVisual ◄───────────────────────┤── Spawn/Destroy visualPrefab
                                              ├── _propeller (Transform)                   └── parent → slot.transform
                                              ├── _maxRpm
                                              ├── _maxDeflectionAngle
                                              └── ShipRootReference
                                                    │
                                                    ▼
                                              ShipController
                                              ├── _currentThrust (read via InputReader или telemetry)
                                              ├── _sumYaw → yaw input
                                              └── _engineRunning
```

**Ключевое:** `EngineThrusterVisual` **читает** данные из `ShipController` / `ShipInputReader`, но **не пишет** в них. `ShipModuleVisualApplier` управляет спавном visualPrefab'а. `EngineThrusterVisual` управляет анимацией.

---

## 6. Проверочный чеклист

### Pre-code
- [ ] Прочитаны все связанные файлы (✅)
- [ ] Понята цепочка ввода → thrust (✅)
- [ ] Определены точки интеграции (✅)
- [ ] `BootstrapScene` — залочена, НЕ трогаем

### Post-code
- [ ] `compile-clean` — 0 errors в Console
- [ ] `git diff BootstrapScene.unity` — пусто (не меняли)
- [ ] `git diff Assets/_Project/Scripts/Player/ShipController.cs` — пусто или только новый getter
- [ ] `ShipInputReader` — публичные геттеры работают
- [ ] `SlotType.Engine` и `ModuleType.Engine` синхронны
- [ ] `EngineThrusterVisual` не на GameObject с Rigidbody
- [ ] Корабль движется на W/S (тяга не сломана)
- [ ] Корабль поворачивается на A/D (yaw не сломан)
- [ ] Двигатель включается/выключается (E)
- [ ] Лопасти вращаются пропорционально тяге
- [ ] Отклонение двигателя следует yaw
- [ ] При `_maxDeflectionAngle = 0` — отклонения нет
- [ ] При отрицательном `_maxRpm` — обратное вращение

---

## 7. Что НЕ делаем

| Задача | Почему не сейчас |
|---|---|
| Перемещать существующие `Engine_Left/Right` слоты | Они уже используются другими модулями. Новые `Slot_Engine_*` — рядом. |
| Менять BootstrapScene | Критическая ошибка T-ENG01. Никогда. |
| Переписывать ShipModuleVisualApplier | Уже работает (L1). EngineThrusterVisual — дополнительный компонент. |
| Добавлять `thrustNormalized` в ShipTelemetryState | Опционально, отдельным тикетом после MVP. Локальный InputReader достаточно для пилота. |
| Multi-crew поддержка (несколько пилотов видят анимацию) | Отдельная задача. Сейчас только пилот видит вращение. |
| ObjectPool для visualPrefab | ShipModuleVisualApplier уже использует Destroy/Instantiate. Оптимизация потом. |
| Создавать SO-модули двигателей (MODULE_ENGINE_*) | Вручную, дизайнером, после того как компонент готов. |

---

## Приложение A: Связь с существующими документами

| Документ | Что говорит |
|---|---|
| `01_MODULE_VISUAL_WITHOUT_BONES.md` | ModuleSlot.transform = «кость» корабля. Подтверждает наш подход. |
| `00_SUMMARY.md` §3.1 | ShipModuleServer как точка расширения. |
| `T-ENG01_ShipEngineVisual_PostMortem.md` | Что сломалось и почему. Используем как anti-pattern. |
| `../../Modul_system/01_ARCHITECTURE.md` | Архитектура модульной системы. |
| `../../Modul_system/02_REPAIR_MANAGER.md` | Как модули устанавливаются в runtime. |

---

## Приложение B: План безопасных коммитов

| Коммит | Файлы | Риск |
|---|---|---|
| `1. SlotType + ModuleType` | `ModuleSlot.cs`, `ShipModule.cs` | 🟡 Enum ordinal change. Проверить касты. |
| `2. ShipInputReader getters` | `ShipInputReader.cs` | 🟢 Чистое добавление. |
| `3. EngineThrusterVisual` | Новый файл | 🟢 Не трогает существующий код. |
| `4. ShipTelemetryState (опц.)` | `ShipTelemetryState.cs`, `ShipController.cs` | 🟡 NetworkVariable payload изменение. |
| `5. Сцена настройки` | WorldScene_0_0 | 🟢 Не BootstrapScene. |

**Все коммиты проверять:** `git diff -- Assets/BootstrapScene.unity | grep GlobalObjectIdHash` — **должен быть пустым**.

---

*Документация: Mavis (Project C Agent), 2026-07-14*
*Анализ: T-ENG02 engine visual architecture*
