# Ship Customization — Technical Analysis

> **Дата:** 2026-06-29
> **Контекст:** анализ применимости `Equipment Visual System` (Phase 1+2, 2026-06-29) к кастомизации кораблей.
> **Цель:** понять, какие части можно взять из Equipment Visual System as-is, что нужно адаптировать, и где требуется новая архитектура.
>
> **Reading prerequisites:** желательно прочитать `00_COMPOSITE_SHIP_SUMMARY.md`, `legacy/HOWTO_CREATE_SHIP.md` (как делать корабль), `Assets/_Project/Scripts/Ship/ShipModule.cs`, `ShipModuleManager.cs`, `ModuleSlot.cs`. И конечно `docs/Character/EquipmentVisual/00_DESIGN.md` (что именно мы делали с одеждой).

---

## TL;DR — главный вывод

**Частично применимо, но не 1:1.** Три разных слоя кастомизации корабля требуют разной глубины адаптации:

| Слой | Применимость Equipment Visual | Что нужно |
|---|---|---|
| **B. Module visualPrefab** (поле в SO) | ✅ Копируется as-is | Добавить `public GameObject visualPrefab;` в `ShipModule`. ~5 строк. |
| **B. Module Visual Applier** (runtime спавн меша) | ⚠️ Адаптация: parent не к кости, а к `ModuleSlot.transform` | Новый компонент `ShipModuleVisualApplier`. ~80 строк, проще чем Equipment. |
| **A. Server-authoritative module state** | ⚠️ Не существует — нужен новый хаб | Новый `ShipCustomizationServer : NetworkBehaviour` + `NetworkVariable<ShipCustomizationData>`. Тяжёлая часть. |
| **C. Slot composition** (новые части корабля) | ❌ Не относится к Equipment Visual | Отдельный большой тикет. |

**Рекомендуемый порядок реализации:** сначала **B.1** (поле в SO, ~5 минут), потом **A.1** (server-authoritative state, требует анализа co-op и ownership), потом **B.2** (applier, поверх replicated state). **С. Composition — отдельный тикет**, не в этом scope.

---

## 1. Текущая архитектура кораблей (что имеем)

### 1.1 Composite Ship (Phase 0–3, реализовано)

Из `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md`:

```
Ship_Root (GameObject) — Rigidbody, NetworkObject, ShipController
├── PilotSeat — PilotSeatController, ShipRootReference, BoxCollider(trigger)
├── Door — DoorController, ShipRootReference, BoxCollider(trigger)
├── Engine_Left — ModuleSlot, ShipRootReference
├── Engine_Right — ModuleSlot, ShipRootReference
└── (любые другие части)
```

**Ключевые компоненты:**
- `ShipController` (root) — управление движением (yaw, pitch, lift, thrust).
- `ShipRootReference` (маркер на каждой части) — даёт доступ к корню через `transform.root.GetComponent<ShipController>()`.
- `ShipComponentLocator` — статический хелпер поиска.
- `PilotSeatController`, `DoorController` — поведение отдельных частей.

**Все части — статические**, определяются в префабе корабля. Дизайнер вручную собирает иерархию.

### 1.2 Module System (Phase 4, реализовано)

Из `Assets/_Project/Scripts/Ship/`:

| Файл | Что |
|---|---|
| `ShipModule.cs` | SO с параметрами модуля: thrust/yaw/pitch/roll/lift множители, cargo бонусы, meziy эффекты. 141 строка. |
| `ModuleSlot.cs` | MonoBehaviour на конкретном GO в иерархии корабля. `slotType` (Propulsion/Utility/Special), `installedModule` (ссылка на SO). `InstallModule/RemoveModule/ValidateCompatibility`. |
| `ShipModuleManager.cs` | На корне корабля. `slots : List<ModuleSlot>`, `availablePower`, `currentPowerUsage`. Валидирует совместимость модулей между собой и с классом корабля. |

**Сценарий использования сейчас** (из кода):

```csharp
// Дизайнер в Editor:
//   1. Кладёт ModuleSlot MonoBehaviour на Engine_Left (slotType=Propulsion).
//   2. Перетаскивает ShipModule_Meziy.asset в поле installedModule.
//   3. ShipModuleManager.Initialize() находит слоты через GetComponentsInChildren.

// В рантайме:
//   - Корабль спавнится, все модули уже "установлены" статически.
//   - Нет UI для замены модулей игроком.
//   - Нет репликации module state (для co-op).
```

### 1.3 Что **отсутствует** для кастомизации игроком

| Что | Статус | Блокирует |
|---|---|---|
| UI для модулей (как `InventoryTab`/`CharacterWindow`) | ❌ Не существует | Игрок не может ставить/снимать модули |
| Server-authoritative module state | ❌ Не существует (всё локально на root) | Co-op / мультиплеер |
| `ShipModule.visualPrefab` поле | ❌ Не существует | Дизайнер не может подключить меш |
| `ShipModuleVisualApplier` runtime компонент | ❌ Не существует | Модуль не "появляется" визуально при установке |
| Slot composition (новые части корабля) | ❌ Не существует | Только статические части в префабе |
| `MyShipsTab` UI (по T-KEY08) | ⏳ Roadmap | UI хаб |

---

## 2. Сравнение с Equipment Visual System

### 2.1 Что у них общего (концептуально)

| Аспект | Equipment Visual (Character) | Ship Module (текущий) | Ship Module (целевой) |
|---|---|---|---|
| **Source data** | `ItemData : ScriptableObject` | `ShipModule : ScriptableObject` | Идентично |
| **Slot abstraction** | `EquipSlot` enum (13 значений) — централизованный | `ModuleSlot` MonoBehaviour на GO — instance-based | Двойственный: enum (по `slotType`) + instance в сцене |
| **State storage** | `EquipmentData` (parallel arrays, replicated) | `ModuleSlot.installedModule` (local reference) | Нужен replicated state по аналогии с `EquipmentData` |
| **Equip hook** | `[Rpc] RequestEquipRpc(itemId, slot)` server-authoritative | `ShipModuleManager.InstallModule(slot, module)` local | Нужен RPC: `[Rpc] RequestInstallModuleRpc(slotName, moduleId)` |
| **Trigger event** | `EquipmentClientState.OnEquipmentUpdated(snapshot)` | Нет | Нужен `ShipCustomizationClientState.OnCustomizationUpdated(snapshot)` |
| **Visual applier** | `CharacterEquipmentVisualApplier` subscribe event → diff → spawn/destroy | Нет | Нужен `ShipModuleVisualApplier` (похожий паттерн, проще) |
| **Per-item visual offset** | `attachPositionOffset/Rotation/Scale` на `ItemData` | Нет (меш — сам GO слота) | Нужно добавить в `ShipModule` (по аналогии) |
| **Bone attachment** | `Animator.GetBoneTransform(HumanBodyBones.X)` | Нет аналога (нет humanoid skeleton) | Не нужно — parent к `slot.transform` напрямую |

### 2.2 Что принципиально **другое** (нужна адаптация)

#### 2.2.1 Нет humanoid skeleton
- Equipment Visual parent'ит visualPrefab к `HumanBodyBones` (Head, RightHand, Spine…).
- У корабля **нет skeleton** — `transform.SetParent(slot.transform)` напрямую. Это **проще**.

#### 2.2.2 Slot — это **сам GO в сцене**, не значение enum
- `EquipSlot.Head` — индекс в массиве, абстракция.
- `ModuleSlot` — `MonoBehaviour`, **физически** существует в иерархии корабля. Designer вручную добавляет на GO (Engine_Left, Engine_Right…).
- Implication: `ShipCustomizationData` (replicated state) должен идентифицировать слоты по **имени** или **пути в иерархии** (не по enum index).

#### 2.2.3 State — local, не replicated
- `EquipmentData` живёт в `EquipmentWorld` (NetworkBehaviour replicated).
- `ShipModuleManager.slots` — обычный `List<ModuleSlot>` на root ship. NetworkBehaviour (`NetworkObject` есть, но список не реплицируется).
- Implication: чтобы игрок мог менять модули и другие игроки видели — нужен **отдельный реплицируемый state**.

#### 2.2.4 Module unequip не удаляет слот
- После `EquipmentData.TryUnequip(slot)` — slot пуст, но `EquipSlot.Head` всегда есть.
- После `ShipModuleManager.RemoveModule()` — слот жив (это GO в сцене), просто `installedModule = null`.
- Implication: `ShipModuleVisualApplier` должен уметь **destroy'ить visual** когда слот пустеет, но **не удалять сам слот**.

#### 2.2.5 Slot validation сложнее
- `EquipSlot` принимает любой `ItemData` с правильным subtype (Weapon/Clothing/Module).
- `ModuleSlot.slotType` ограничивает по `ModuleType` (Propulsion/Utility/Special). + `ShipModule.IsCompatibleWithClass(shipClass)` + `ValidateModuleCompatibility()` + `AreRequiredModulesInstalled()` + power budget.
- Implication: replicated state должен включать **class корабля** и **power budget** для валидации на сервере.

---

## 3. Архитектурные решения для Ship Customization

### 3.1 Фаза B.1 — `ShipModule.visualPrefab` (поле в SO)

**Что:** Добавить 5 полей в `ShipModule` по аналогии с `ItemData`:
```csharp
[Header("Visual (Equipment Visual System — analog)")]
public GameObject visualPrefab;
public HumanBodyBones attachBoneOverride = HumanBodyBones.LastBone;  // не нужно для корабля, но конвенция
public Vector3 attachPositionOffset = Vector3.zero;
public Vector3 attachRotationOffset = Vector3.zero;
public Vector3 attachScale = Vector3.one;
```

**Решение по `HumanBodyBones`:** для корабля **не нужен** bone override. Но ради единообразия с `ItemData` (общий паттерн) — можно оставить. `attachBoneOverride == LastBone` = no-op. Альтернатива — отдельный enum `ModuleVisualAnchor { Slot, SlotForward, SlotUp }`, но это **over-engineering для MVP**.

**Размер:** ~10 строк в `ShipModule.cs`. Существующие ~12 ассетов модулей не ломаются (default = null).

**Зависимость:** может быть сделан **сейчас**, не дожидаясь replicated state.

### 3.2 Фаза A.1 — Server-authoritative state

**Что:** Новый компонент `ShipCustomizationServer : NetworkBehaviour` (аналог `EquipmentServer`):
- `NetworkList<ShipSlotState>` где `ShipSlotState { string slotPath; int moduleId; }` (replicated).
- `[Rpc(SendTo.Server)] RequestInstallModuleRpc(string slotPath, int moduleId)`.
- `[Rpc(SendTo.Server)] RequestRemoveModuleRpc(string slotPath)`.
- Validation (power, compatibility) на сервере.
- `ShipCustomizationWorld` (аналог `EquipmentWorld`) — server-side state.
- `ShipCustomizationClientState : MonoBehaviour` (аналог `EquipmentClientState`) — client-side projection + `OnCustomizationUpdated` event.

**Сложность:** **высокая**. Это нетривиальный рефакторинг, потому что `ModuleSlot.installedModule` сейчас — **direct reference** на SO. Нужно:
1. Ввести понятие "moduleId" (из `ShipModule.moduleId` — он уже есть, string).
2. Сделать server-side lookup `moduleId → ShipModule` (по аналогии с `InventoryWorld._itemDatabase`).
3. `ModuleSlot` должен читать state не из `installedModule`, а из `ShipCustomizationClientState.CurrentSnapshot[slotPath]`.
4. **Backward compat:** существующие scene-placed `ModuleSlot.installedModule` в префабах должны работать без UI-driven state.

**Рекомендация:** **dual mode** — `ModuleSlot` имеет `bool useReplicatedState` поле:
- Если true — читает из `ShipCustomizationClientState`.
- Если false — работает как сейчас (для backward compat с scene-placed modules).

Это позволит ввести replicated state **постепенно**, не ломая существующие префабы.

**Размер:** большой. Это тикет на несколько сессий.

### 3.3 Фаза B.2 — `ShipModuleVisualApplier` (runtime spawn)

**Что:** Новый MonoBehaviour на корне корабля (рядом с `ShipModuleManager`). Подписывается на `ShipCustomizationClientState.OnCustomizationUpdated`, для каждого слота — diff vs `_currentItems`, spawn/destroy visualPrefab.

**Отличия от `CharacterEquipmentVisualApplier`:**
- Нет `Animator.isHuman` check — parent напрямую к `slot.transform`.
- Diff по **slotPath** (string), не по `EquipSlot` (enum).
- Visual destroy при unequip, но **slot GO остаётся** (см. §2.2.4).
- Per-ship applier (не per-character). На root ship + все дочерние ModuleSlot.

**Псевдокод:**
```csharp
public class ShipModuleVisualApplier : MonoBehaviour
{
    [SerializeField] private ShipModuleManager _manager;
    private readonly Dictionary<string, GameObject> _spawned = new();

    void OnEnable() { ShipCustomizationClientState.Instance.OnCustomizationUpdated += Apply; }
    void OnDisable() { /* unregister + destroy */ }

    void Apply(ShipCustomizationSnapshotDto snapshot)
    {
        foreach (var kvp in snapshot.slotStates)  // string slotPath → ShipSlotState
        {
            var slot = _manager.FindSlotByPath(kvp.Key);  // returns ModuleSlot
            if (slot == null) continue;
            var module = LookupModule(kvp.Value.moduleId);
            if (module == null || module.visualPrefab == null) { DestroyVisual(kvp.Key); continue; }
            SpawnOrUpdate(kvp.Key, slot, module);
        }
    }

    void SpawnOrUpdate(string path, ModuleSlot slot, ShipModule module)
    {
        if (_spawned.TryGetValue(path, out var existing) && existing != null)
        {
            // Module changed in same slot — destroy old, spawn new
            Destroy(existing);
        }
        var go = Instantiate(module.visualPrefab, slot.transform);
        go.transform.localPosition = module.attachPositionOffset;
        go.transform.localEulerAngles = module.attachRotationOffset;
        go.transform.localScale = module.attachScale;
        foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;  // visual only
        _spawned[path] = go;
    }
}
```

**Размер:** ~80–100 строк. **Проще**, чем `CharacterEquipmentVisualApplier` (~280 строк) — нет bone resolution, нет humanoid check.

**Зависимость:** требует **A.1** (replicated state) для работы в полную силу. Без A.1 — applier может работать в **read-only mode** на scene-placed `ModuleSlot.installedModule` (т.е. `ModuleSlot` сам читает `installedModule` и applier реагирует на изменение этого поля). Это даёт визуал даже без replicated state, но только для того что designer заложил в префабе.

### 3.4 Фаза C — Slot composition (отдельный тикет)

**Что:** Игрок добавляет/удаляет части корабля (крылья, пушки, броню) в runtime.

**Это НЕ относится к Equipment Visual System.** Тут нужны:
- Сериализация иерархии корабля (что-то типа `ShipPartDefinition[]`).
- "Ghost" placement preview перед подтверждением.
- Snap-to-attachment-point system.
- Physical constraints (no overlapping colliders).
- Network sync (server-authoritative).
- Возможно inventory для parts (как ItemData для одежды).

**Размер:** очень большой. Это **новая подсистема**, не заимствование из Equipment. Делать **отдельно**, после A.1 и B.1/B.2.

---

## 4. Что заимствуем из Equipment Visual System (concrete reuse)

### 4.1 Anti-restrictive паттерн

Везде где добавляем поле/компонент — default = null/no-op, существующие ассеты не ломаются. Это уже применено в `ItemData` и применяется в `ShipModule`.

### 4.2 `userData`-based callback unregister (T-EV-002 fix)

Если/когда будем делать UI для модулей (`MyShipsTab`) — сразу использовать паттерн из `InventoryTab.cs:640-654`, чтобы не повторять double-callback bug.

### 4.3 Diff-based spawn/destroy в VisualApplier

Идентичный паттерн: `_spawned[slot] → existing`, if `newItem != oldItem` → destroy + spawn. Меньше flicker, идемпотентно.

### 4.4 Editor script для auto-wire

`SetupEquipmentVisualAssets.cs` показал паттерн `[MenuItem]` для создания тестовых префабов + add-component-на-prefab. Тот же паттерн применим для ShipModuleVisualApplier на корне корабля.

### 4.5 Документация по phase-driven реализации

`docs/Character/EquipmentVisual/00_DESIGN.md` + `01_DATA_MODEL.md` + `02_CHARACTER_APPLIER.md` + `03_PHASES.md` — хороший шаблон. Адаптируем для Ship Customization:
- `docs/Ships/Customization/00_DESIGN.md`
- `docs/Ships/Customization/01_DATA_MODEL.md`
- `docs/Ships/Customization/02_VISUAL_APPLIER.md`
- `docs/Ships/Customization/03_PHASES.md`

---

## 5. Что НЕ заимствуем (различия, которые нельзя игнорировать)

| Что | Equipment Visual | Ship Customization |
|---|---|---|
| **Slot identification** | `EquipSlot` enum (compact, replicated) | `ModuleSlot` MonoBehaviour с unique `gameObject` + **path** (нужен для replicated state) |
| **State replication** | Уже есть (`EquipmentServer` + `EquipmentWorld` + `EquipmentClientState`) | **Нет** — нужно строить с нуля (`ShipCustomizationServer/World/ClientState`) |
| **Bone attachment** | `HumanBodyBones` (54 значения) | Не применимо, parent = `slot.transform` |
| **Constraint validation** | Просто (skill requirements, tier check) | Сложно (power, class, module compatibility, requirements) |
| **Multi-ship (instancing)** | Один персонаж на игрока | **Несколько кораблей** на сцене (другие игроки co-op) — applier должен работать для каждого корабля отдельно |

---

## 6. Рекомендуемый план реализации

### Шаг 1 — Сейчас, низкий риск, высокий value
**`ShipModule.visualPrefab` + 4 attach-поля** (по аналогии с `ItemData`).
- Файл: `Assets/_Project/Scripts/Ship/ShipModule.cs`
- Размер: ~10 строк.
- Зависимости: нет.
- Verification: compile clean + reflection smoke test.

### Шаг 2 — Ключевой архитектурный шаг
**Replicated state для модулей корабля.**
- Файлы (новые):
  - `Assets/_Project/Scripts/Ship/Customization/ShipCustomizationData.cs` (struct, parallel arrays by slotPath)
  - `Assets/_Project/Scripts/Ship/Customization/ShipCustomizationWorld.cs` (server-side state, аналог `EquipmentWorld`)
  - `Assets/_Project/Scripts/Ship/Customization/ShipCustomizationServer.cs` (NetworkBehaviour, RPCs)
  - `Assets/_Project/Scripts/Ship/Customization/ShipCustomizationClientState.cs` (client projection + events)
  - `Assets/_Project/Scripts/Ship/Customization/ShipCustomizationSnapshotDto.cs`
- Изменения: `ModuleSlot` получает `bool useReplicatedState` (default false = backward compat).
- Размер: большой (4-6 сессий).
- Блокирует: Шаг 3.

### Шаг 3 — Визуал
**`ShipModuleVisualApplier` MonoBehaviour.**
- Файл: `Assets/_Project/Scripts/Ship/Customization/ShipModuleVisualApplier.cs`
- Размер: ~80-100 строк.
- Зависимость: Шаг 2 (для full power, но может работать и в read-only mode на scene-placed modules).

### Шаг 4 — UI (отдельный roadmap item)
**`MyShipsTab` UI** (уже в T-KEY08).
- Без Шага 2 не имеет смысла (нечего UI отображать — всё статично).

### Шаг 5 — Slot composition (отдельный большой тикет)
**Runtime добавление/удаление частей корабля.**
- Не в scope Equipment Visual System.

---

## 7. Open questions (для будущей сессии перед стартом)

1. **Backward compat для `ModuleSlot.installedModule`** — оставляем как fallback или мигрируем всё на replicated state? **Предложение:** dual mode (`useReplicatedState` bool).
2. **Slot identification в replicated state** — slotPath (string) или slot GUID? **Предложение:** slotPath (читаемо, стабильно пока иерархия корабля не меняется в runtime).
3. **`ShipModule.moduleId`** — уже есть (string, уникальный). Использовать как primary key в replicated state? **Предложение:** да, минус один новый field.
4. **Power budget validation на сервере** — где хранить `availablePower`? Сейчас в `ShipModuleManager`. Нужно ли replicated? **Предложение:** local, не replicated (определяется классом корабля, не меняется в runtime).
5. **Co-op видят кастомизацию друг друга?** — для MVP достаточно local-only (как сейчас `NpcVisualConfig`). Мультиплеер sync — отдельная задача, требует ownership модели.
6. **MyShipsTab уже в roadmap (T-KEY08) — насколько далеко продвинулся?** Нужно прочитать `docs/Ships/Key-subsystem/26_TKEY08_MYSHIPS_TAB_PLAN.md` перед стартом.

---

## 8. Связанные документы

| Документ | Назначение |
|---|---|
| `docs/Character/EquipmentVisual/00_DESIGN.md` | Дизайн Equipment Visual System (источник паттернов) |
| `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` | Composite Ship архитектура (Phase 0-3) |
| `docs/Ships/legacy/HOWTO_CREATE_SHIP.md` | Практическая инструкция создания корабля |
| `docs/Ships/legacy/AGENTS_SHIP_SYSTEM_SUMMARY.md` | 3-уровневая система (Movement → Environment → Modules) |
| `docs/Ships/Key-subsystem/26_TKEY08_MYSHIPS_TAB_PLAN.md` | MyShipsTab UI план (нужно прочитать перед стартом Шага 4) |
| `Assets/_Project/Scripts/Ship/ShipModule.cs` | Текущий SO модуля |
| `Assets/_Project/Scripts/Ship/ShipModuleManager.cs` | Текущий менеджер модулей |
| `Assets/_Project/Scripts/Ship/ModuleSlot.cs` | Текущий слот модуля |
| `docs/dev/EquipmentVisual_BUGS_TICKETS.md` | Уроки с T-EV-002 (применить к MyShipsTab UI) |