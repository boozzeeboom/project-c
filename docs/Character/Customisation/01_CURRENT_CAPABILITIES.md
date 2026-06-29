# Current Capabilities — что в проекте уже есть

> **Дата:** 2026-06-30
> **Метод:** прямой анализ кода + grep + read_file + search_files по `Assets/_Project/`, `Assets/Kevin Iglesias/`, `docs/Character/`.
> **Цель:** показать, какие готовые точки расширения у нас уже есть — чтобы кастомизация была additive, а не breaking.

---

## 1. Сетевой персонаж (NetworkPlayer.prefab)

**Файл:** `Assets/_Project/Prefabs/NetworkPlayer.prefab`

### 1.1 Что уже есть в префабе (компоненты на root)

| Компонент | MonoBehaviour | Назначение |
|---|---|---|
| `NetworkObject` | Unity.Netcode | Сетевая идентичность |
| `NetworkTransform` | Unity.Netcode.Components | Репликация transform |
| `CharacterController` | UnityEngine | Физика + движение (height=1.8, center=(0,0.9,0)) |
| `NetworkPlayer` | `ProjectC.Player` | Главный скрипт — input, movement, boarding, combat reg |
| `PlayerInputReader` | `ProjectC.Player` | Читает WASD/Space/Shift/F/E через Keyboard.current |
| `CharacterEquipmentVisualApplier` | `ProjectC.Player` | **УЖЕ добавлен в префаб** — visual swap для одежды по EquipSlot |

### 1.2 Структура префаба

```
NetworkPlayer (root, NetworkObject, CharacterController)
└── Visual_Model (child, FBX-imported HumanM_Model)
    └── Animator (runtimeAnimatorController = PlayerAnimation_Default override → PlayerAnimation)
        └── SkinnedMeshRenderer(s) — humanoid bones
```

**Источник:** `Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx`

`Visual_Model` — это экземпляр `HumanM_Model.fbx` с Animator-ом. Setup через `Tools/ProjectC/Player/Setup NetworkPlayer Visual` (см. `Assets/_Project/Editor/SetupPlayerVisual.cs`).

### 1.3 Точки расширения (что уже можно хукать)

| Что хотим добавить | Куда подцепить |
|---|---|
| Новый компонент `CharacterCustomisationApplier` | На root `NetworkPlayer` рядом с `CharacterEquipmentVisualApplier` (AddComponent идемпотентно) |
| Runtime mesh swap | Получить ссылку на `Visual_Model` (child), swap SkinnedMeshRenderer.sharedMesh |
| Animator Controller swap | Получить Animator на `Visual_Model`, менять `runtimeAnimatorController` |
| Подписка на событие "выбор персонажа изменён" | Через новый `CustomisationClientState` (singleton по аналогии с `EquipmentClientState`) |
| Persistence | Расширить `CharacterSaveData.customisation` (additive, см. §3) |

---

## 2. Animator Controller — готов к M↔F подмене

**Файл:** `Assets/_Project/Animations/PlayerAnimation.controller` (1988 строк, 24 states)

### 2.1 Структура стейт-машины (base layer)

```
[Base Layer]
├── Idle
├── Walk_BlendTree (8-directional: Forward, ForwardLeft, ForwardRight, Left, Right, BackwardLeft, BackwardRight, Backward)
├── Run_BlendTree (8-directional)
├── Sprint_BlendTree (5-directional: Forward, ForwardLeft, ForwardRight, Left, Right)
├── Jump (trigger)
├── Fall (state)
├── Land (state)
├── TurnLeft (bool)
├── TurnRight (bool)
├── Attack1H (trigger)
├── CombatIdle
├── Damage (trigger)
├── Death (trigger)
├── Skill (AnyState → Skill по триггеру "SkillPlay")
├── Mine (trigger MinePlay)
├── Lumber (trigger LumberPlay)
└── Gather (trigger GatherPlay)
```

### 2.2 Animator Parameters

| Параметр | Тип | Назначение |
|---|---|---|
| `Speed` | float | Скорость движения (для blend trees) |
| `MoveX` | float | Strafe компонента |
| `MoveY` | float | Forward компонента |
| `IsGrounded` | bool | На земле ли |
| `Jump` | trigger | Прыжок |
| `InCombat` | bool | Combat-mode |
| `Attack` | trigger | Bare-fist attack |
| `Damage` | trigger | Получение урона |
| `Death` | trigger | Смерть |
| `TurnLeft` / `TurnRight` | bool | Поворот на месте |
| `SkillPlay` | trigger | Active skill cast (T-INP-08) |
| `MinePlay` / `LumberPlay` / `GatherPlay` | trigger | Resource gathering |

### 2.3 AnimatorOverrideController — уже используется

**Файл:** `Assets/_Project/Animations/PlayerAnimation_Default.overrideController`

```yaml
m_Controller: {fileID: 9100000, guid: d8dd6e8045cc805469ad29f83e18e202, type: 2}  # → PlayerAnimation.controller
m_Clips: []  # пустой, не подменяет
```

Это **заготовка** для AnimatorOverrideController. Уже подключён к Animator на `Visual_Model`. **M↔F подмена делается через создание второго AnimatorOverrideController** с F-клипами, и runtime-swap `Animator.runtimeAnimatorController`.

### 2.4 SkillAnimationPlayer (T-INP-08) — уже data-driven

**Файл:** `Assets/_Project/Scripts/Skills/SkillAnimationPlayer.cs`

- Использует `SkillNodeConfig.attackClip` (AnimationClip) — подменяет motion в state "Skill" через свой `AnimatorOverrideController`.
- Подменяет контроллер в `Play()` → восстанавливает в `Restore()`.
- Кеширует override controllers по InstanceID клипа.

**Следствие для кастомизации:** SkillAnimationPlayer работает с **motion placeholder** в state "Skill". Если мы подменим `runtimeAnimatorController` на F-версию через `CharacterCustomisationApplier`, Skill клипы продолжат работать (потому что state "Skill" есть в обеих версиях controller-а). **Skill pipeline не требует изменений**.

---

## 3. Persistence — готова к расширению

**Файлы:**
- `Assets/_Project/Scripts/Stats/Persistence/CharacterSaveData.cs`
- `Assets/_Project/Scripts/Stats/Persistence/JsonCharacterDataRepository.cs`

### 3.1 Текущая структура

```csharp
[Serializable]
public class CharacterSaveData {
    public PlayerStatsSave stats = new();
    public EquipmentSave equipment = new();
    public SkillsSave skills = new();
}
```

### 3.2 Что нужно добавить (additive)

```csharp
[Serializable]
public class CharacterSaveData {
    public PlayerStatsSave stats = new();
    public EquipmentSave equipment = new();
    public SkillsSave skills = new();
    // NEW: Customisation (additive, default = male preset)
    public CustomisationSave customisation = new();
}
```

`JsonUtility.FromJson` **игнорирует отсутствующие поля** → старые `.json` файлы загружаются с `customisation = new CustomisationSave()` (default = Male). Backward-compat гарантирована.

### 3.3 Persistence flow (текущий)

- `StatsServer` (NetworkBehaviour, scene-placed, BootstrapScene) авторитативен для stats.
- `JsonCharacterDataRepository` — default ICharacterDataRepository impl, per-clientId файл `Application.persistentDataPath/Character/character_<clientId>.json`.
- `TryLoad(clientId, out CharacterSaveData)` → `JsonUtility.FromJson`.
- `Save(clientId, data)` → atomic write (tmp + Move pattern).

**Рекомендация для кастомизации:** CustomisationSave лежит в **той же** CharacterSaveData. Persistence через тот же StatsServer flow (или новый CustomisationServer если хотим отделить). См. `05_PHASES_ROADMAP.md` §3.

---

## 4. Equipment Visual — паттерн для кастомизации

**Файлы:**
- `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs` (291 строка, **уже реализован** Phase 2)
- `Assets/_Project/Scripts/Equipment/Visual/EquipSlotToBone.cs`
- `Assets/_Project/Editor/SetupEquipmentVisualApplier.cs`

### 4.1 Что уже работает

`CharacterEquipmentVisualApplier` — MonoBehaviour на `NetworkPlayer.prefab`. Делает:
1. Подписывается на `EquipmentClientState.OnEquipmentUpdated`.
2. Для каждого `EquipSlot` — diff snapshot vs `_currentItems`.
3. `SpawnVisual(slot, item)` — `Instantiate(item.visualPrefab)`, parent к `EquipSlotToBone.TryGetBoneTransform(slot, override, animator, out bone)`, localPosition/Scale по `attachPositionOffset/attachScale`.
4. `DestroyVisual(slot)` — `Destroy(go)` и убрать из `_spawnedVisuals`.

### 4.2 Что это даёт кастомизации

**Готовый шаблон** для `CharacterCustomisationApplier`:
- Singleton-подписка (`OnCustomisationUpdated` event на новом `CustomisationClientState`).
- Diff-логика snapshot vs current.
- Spawn/destroy по "slot" (для кастомизации slot = Male/Female/BodyPreset1/etc).
- Apply offsets/scales из data.

**Разница:** Equipment Visual спавнит **внешний** prefab (одежда/оружие) — её не надо прятать под базовое тело. Customisation Visual **меняет само тело** (mesh swap на Visual_Model) или **применяет blend shapes / colors к существующему телу**.

---

## 5. Kevin Iglesias FREE pack — что в нём есть

**Путь:** `Assets/Kevin Iglesias/Human Animations/`

### 5.1 Модели (Models/)

| Файл | Описание |
|---|---|
| `Models/HumanM_Model.fbx` | **M персонаж**, humanoid rig, FBX. Уже используется в NetworkPlayer.prefab (см. SetupPlayerVisual.cs:15). |
| `Models/HumanF_Model.fbx` | **F персонаж**, humanoid rig, FBX. **Доступен, не используется.** Готов к подключению. |
| `Models/Avatar Masks/Arms/Human Arms Mask.mask` | Avatar mask для верхних конечностей (для layered blending) |
| `Models/Avatar Masks/Arms/Human Arm Left Mask.mask` / `Right Mask` | По отдельности |
| `Models/Avatar Masks/Hands/Human Hand Left Mask.mask` / `Right Mask` | Руки отдельно |

### 5.2 Анимации — Male и Female

**Структура (одинаковая для обоих полов):**

```
Animations/
├── {Male,Female}/
│   ├── Combat/        (1H, 2H, Polearm, Shield, Thrown)
│   ├── Idles/         (Idle01, Idle02 + transitions)
│   ├── Movement/      (Walk 8-dir, Run 8-dir, Sprint 5-dir, StrafeRun 6-dir, Jump, Turn L/R, Fall, Land)
│   ├── Social/        (Talk01)
│   └── Work/          (Farming, Fishing, Gathering, Hammering, Mining)
└── Masked Poses/      (ObjectGrip, WeaponHold — для IK setup)
```

**Все locomotion клипы для F существуют** — это значит AnimatorController можно подменить без правок стейт-машины.

**Имена клипов — те же самые, отличается только префикс:**
- `HumanM@Walk01_Forward` ↔ `HumanF@Walk01_Forward`
- `HumanM@Run01_Forward` ↔ `HumanF@Run01_Forward`
- `HumanM@Idle01` ↔ `HumanF@Idle01`
- и т.д.

### 5.3 Что **НЕ входит** в FREE-пак

- ❌ Blend shapes (рост/полнота/лицо) — нужна PRO-версия или своя mesh-morph подсистема.
- ❌ Facial morphs — нужна UMA 2 / Morph3D / CC3.
- ❌ Color variants — модели идут с одним материалом, color customization — через `MaterialPropertyBlock` на runtime.

**Следствие:** L1 (М↔Ж переключение) реализуем прямо сейчас. L3/L5 (лицо/тело слайдеры) — нужны дополнительные ассеты или SDK.

---

## 6. UI — CharacterWindow готов к расширению

**Файлы:**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (3451 строк, 6 top-level табов)
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` (162 строки)
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` (1036 строк)

### 6.1 Существующие табы

```
[ПЕРСОНАЖ] [КОРАБЛЬ] [РЕПУТАЦИЯ] [КОНТРАКТЫ] [ИНВЕНТАРЬ] [КВЕСТЫ]
```

Таб "ПЕРСОНАЖ" внутри уже разбит на под-секции:
- Row 1: Одежда | Модули
- Row 2: Характеристики | Боевые скиллы | Социальные скиллы

### 6.2 Паттерн для добавления таба "ВНЕШНОСТЬ"

**Вариант A: Sub-tab внутри "ПРОГРЕССИЯ"** (рекомендую для L1-L2).
**Вариант B: Новый top-level таб "ВНЕШНОСТЬ"** (UX-выбор, когда L3-L5 дойдут).

`CharacterWindow.SwitchTab(string tab)` уже принимает строковый ключ. Добавить `"customisation"` ветку — тривиально. Подробнее — в `05_PHASES_ROADMAP.md` §5.

---

## 7. ItemData — готов под cosmetic-расширения

**Файл:** `Assets/_Project/Scripts/Core/ItemType.cs` (76 строк)

### 7.1 Что есть сейчас

```csharp
public class ItemData : ScriptableObject {
    public string itemName;
    public ItemType itemType;
    public string description;
    public Sprite icon;
    public int maxStack = 1;
    public float weightKg = 0.1f;
    // Phase 1 Equipment Visual:
    public GameObject visualPrefab;
    public HumanBodyBones attachBoneOverride = HumanBodyBones.LastBone;
    public Vector3 attachPositionOffset;
    public Vector3 attachRotationOffset;
    public Vector3 attachScale;
}
```

### 7.2 Что можно добавить (additive, для L4 — Покраска одежды)

- `Color tintColor` (default white) — для окраски экипировки.
- `MaterialOverride materialOverride` (optional) — для спец-материалов.

Это **необязательные** поля. Дизайнер может не заполнять → старая логика работает.

---

## 8. Что мы НЕ нашли (gap-анализ)

| Нет | Что делать |
|---|---|
| `CustomisationData` SO | Создать новый ScriptableObject (фаза L1) |
| `CustomisationSave` DTO | Создать новый [Serializable] класс (фаза L1) |
| `CustomisationClientState` | Создать новый MonoBehaviour-singleton по аналогии с `EquipmentClientState` (фаза L1) |
| `CustomisationServer` (опционально) | Создать новый NetworkBehaviour для replication (фаза L2, для multiplayer sync) |
| `CharacterCustomisationApplier` | Создать новый MonoBehaviour по аналогии с `CharacterEquipmentVisualApplier` (фаза L1) |
| Таб "ВНЕШНОСТЬ" в CharacterWindow | Добавить UXML sub-tab + обработчик в CharacterWindow.cs (фаза L1) |
| `CharacterSaveData.customisation` | Расширить CharacterSaveData (фаза L1, additive) |
| `HumanF_Override.overrideController` | Создать AnimatorOverrideController с F-клипами (фаза L1) |
| `PlayerAnimation_F.controller` (опционально) | Если нужны state-specific override-ы для F (фаза L1) |

**Всё это — новые файлы.** Никаких изменений в существующих подсистемах кроме additive-полей в `CharacterSaveData` и `ItemData`.

---

## 9. Сводка: что reuse, что create

| Категория | Reuse | Create |
|---|---|---|
| Animator infrastructure | `PlayerAnimation.controller`, `PlayerAnimation_Default.overrideController` | `PlayerAnimation_Female.overrideController` |
| Visual swap pattern | `CharacterEquipmentVisualApplier`, `EquipSlotToBone` | `CharacterCustomisationApplier` |
| Persistence | `CharacterSaveData`, `JsonCharacterDataRepository` | `CustomisationSave` (additive) |
| State management | `EquipmentClientState` pattern | `CustomisationClientState` (parallel) |
| UI | `CharacterWindow.cs`, `CharacterWindow.uxml`, `CharacterWindow.uss` | Sub-tab "ВНЕШНОСТЬ" + handler |
| Models | `HumanM_Model.fbx` уже в префабе | `HumanF_Model.fbx` — добавить в Resources |
| Animations | M-клипы уже подключены | F-клипы — добавить в AnimatorOverrideController |
| ItemData | текущие поля | (опционально L4) `tintColor`, `materialOverride` |
| Network | `NetworkBehaviour` pattern | (опционально L2) `NetworkVariable<CustomisationSnapshotDto>` |

**Итог: 90% работы — additive. Никакого риска regression для существующих подсистем.**