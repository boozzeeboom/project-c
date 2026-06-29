# Equipment Visual — Data Model

> Точные .cs-сигнатуры, поля, типы. Companion к `00_DESIGN.md`.

---

## 1. ItemData — добавление visualPrefab (Phase 1)

**Файл:** `Assets/_Project/Scripts/Core/ItemType.cs`

**Изменение:** только аддитивное — новое поле в существующий `ItemData` базовый класс.

```csharp
[CreateAssetMenu(fileName = "NewItem", menuName = "Project C/Item Data", order = 1)]
public class ItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    [TextArea]
    public string description;
    public Sprite icon;

    // === existing fields (Phase 6) ===
    [Header("Stack & Weight (Phase 6)")]
    public int   maxStack = 1;
    public float weightKg = 0.1f;

    // === NEW: Phase 1 — visualPrefab ===
    [Header("Visual (Phase 1 — Equipment Visual System)")]
    [Tooltip("3D-меш/prefab для отображения предмета в мире (PickupItem) и на персонаже " +
             "при экипировке (CharacterEquipmentVisualApplier). " +
             "Если null — предмет отображается только иконкой (старое поведение, " +
             "не требует никаких изменений существующих ассетов).")]
    public GameObject visualPrefab;

    // === NEW: Phase 2 — attach override (см. §3) ===
    [Header("Attach to Character (Phase 2 — optional)")]
    [Tooltip("Какая кость персонажа для прикрепления visualPrefab при экипировке. " +
             "None = используется default маппинг по EquipSlot (см. EquipSlotToBone.cs). " +
             "Override для специфичных случаев: двуручный меч, шляпа-цилиндр и т.п.")]
    public HumanBodyBones attachBoneOverride = HumanBodyBones.LastBone; // sentinel "use default"

    [Tooltip("Локальный offset от кости к прикреплённому visualPrefab (в local space кости).")]
    public Vector3 attachPositionOffset = Vector3.zero;

    [Tooltip("Локальное вращение visualPrefab относительно кости (Euler degrees).")]
    public Vector3 attachRotationOffset = Vector3.zero;

    [Tooltip("Локальный масштаб visualPrefab. (1,1,1) = без изменений. Полезно если меш " +
             "импортирован в неправильном масштабе (например, cm vs m).")]
    public Vector3 attachScale = Vector3.one;
}
```

### Почему именно так

| Поле | Решение | Обоснование |
|---|---|---|
| `visualPrefab` (GameObject) | а не `Mesh` / `MeshFilter` | Designer может положить любой GameObject — со SkinnedMeshRenderer, с анимациями, с nested объектами. Готовая prefab-сцена. |
| `visualPrefab` nullable | default = null | Существующие ~500 ассетов останутся без изменений, без warning'ов. Anti-restrictive: opt-in visual. |
| `attachBoneOverride = HumanBodyBones.LastBone` | sentinel "use default" | `HumanBodyBones.LastBone = 54` — на 1 больше максимального значения (RightEyeLid = 53). Чёткий sentinel, нет bool. |
| `attachPositionOffset` | Vector3, не Quaternion+Vector3 | Designer-friendly в Inspector. |
| `attachScale` (Vector3) | а не float | Случай нетипичного масштабирования (щит больше с одной стороны). |

---

## 2. EquipSlotToBone — таблица маппинга (Phase 2)

**Новый файл:** `Assets/_Project/Scripts/Equipment/Visual/EquipSlotToBone.cs`

```csharp
// Project C: Equipment Visual System — Phase 2
// EquipSlotToBone: единый маппинг EquipSlot → HumanBodyBones.
// По аналогии с NpcVisualApplier (T-NPC-05) — explicit, не GetComponentInChildren.
//
// Дизайн: см. docs/Character/EquipmentVisual/00_DESIGN.md §3.3

using UnityEngine;

namespace ProjectC.Equipment.Visual
{
    /// <summary>
    /// Phase 2: единая таблица маппинга EquipSlot → HumanBodyBones для персонажа-гуманоида M.
    /// Humanoid skeleton (Kevin Iglesias HumanM_Model) — стандартный Unity HumanBodyBones.
    /// </summary>
    public static class EquipSlotToBone
    {
        /// <summary>
        /// Получить Transform кости Animator'а для указанного EquipSlot.
        /// Если Animator не humanoid или кость не найдена — возвращает false.
        /// </summary>
        /// <param name="slot">EquipSlot (Head/Chest/WeaponMain/...)</param>
        /// <param name="animator">Animator персонажа (humanoid обязателен)</param>
        /// <param name="bone">out: Transform кости, null если не найдена</param>
        /// <returns>true если кость найдена и валидна</returns>
        public static bool TryGetBoneTransform(EquipSlot slot, Animator animator, out Transform bone)
        {
            bone = null;
            if (animator == null || !animator.isHuman) return false;

            bone = slot switch
            {
                EquipSlot.Head       => animator.GetBoneTransform(HumanBodyBones.Head),
                EquipSlot.Chest      => animator.GetBoneTransform(HumanBodyBones.Spine),     // верх спины
                EquipSlot.Legs       => animator.GetBoneTransform(HumanBodyBones.Hips),     // штаны крепятся к бёдрам
                EquipSlot.Feet       => animator.GetBoneTransform(HumanBodyBones.LeftFoot),  // основная нога; визуал симметричный
                EquipSlot.Back       => animator.GetBoneTransform(HumanBodyBones.Spine),     // плащ за спиной — offset back
                EquipSlot.Hands      => animator.GetBoneTransform(HumanBodyBones.LeftHand), // перчатки на любую руку (меш симметричный)
                EquipSlot.Accessory1 => animator.GetBoneTransform(HumanBodyBones.Spine),     // кольцо в UI — но визуально decorative
                EquipSlot.Accessory2 => animator.GetBoneTransform(HumanBodyBones.Spine),
                EquipSlot.WeaponMain => animator.GetBoneTransform(HumanBodyBones.RightHand), // основное оружие — правая рука
                EquipSlot.WeaponOff  => animator.GetBoneTransform(HumanBodyBones.LeftHand),  // парное / щит — левая рука
                EquipSlot.Module1    => animator.GetBoneTransform(HumanBodyBones.Spine),     // имплант 1
                EquipSlot.Module2    => animator.GetBoneTransform(HumanBodyBones.Spine),
                EquipSlot.Module3    => animator.GetBoneTransform(HumanBodyBones.Spine),
                _ => null,
            };
            return bone != null;
        }

        /// <summary>
        /// Получить bone с учётом per-item override (Phase 2.2 поля ItemData.attachBoneOverride).
        /// Если override == LastBone → используется default для EquipSlot.
        /// </summary>
        public static bool TryGetBoneTransformWithOverride(
            EquipSlot slot,
            HumanBodyBones overrideBone,
            Animator animator,
            out Transform bone)
        {
            bone = null;
            if (animator == null || !animator.isHuman) return false;

            // Если override задан — используем его.
            if (overrideBone != HumanBodyBones.LastBone)
            {
                bone = animator.GetBoneTransform(overrideBone);
                return bone != null;
            }

            // Иначе — default маппинг по слоту.
            return TryGetBoneTransform(slot, animator, out bone);
        }
    }
}
```

---

## 3. CharacterEquipmentVisualApplier (Phase 2)

**Новый файл:** `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs`

Полный код — в `02_CHARACTER_APPLIER.md`. Здесь только поля и контракт:

```csharp
namespace ProjectC.Player
{
    /// <summary>
    /// Применяет ItemData.visualPrefab к персонажу на основе EquipmentSnapshotDto.
    /// Спавнит/уничтожает GameObject под каждый slot, parent к HumanBodyBones.
    /// Аналог NpcVisualApplier (T-NPC-05) но для equipment slots.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterEquipmentVisualApplier : MonoBehaviour
    {
        // === Inspector ===

        [Tooltip("Animator с humanoid rig (должен быть на Visual_Model или root с SkinnedMesh).")]
        [SerializeField] private Animator _animator;

        [Tooltip("Имя дочернего объекта 'Visual' (для совместимости с паттерном NpcVisualApplier). " +
                 "Если пусто — ищем Animator на этом GO или в детях.")]
        [SerializeField] private string _animatorSearchRoot = "";

        [Tooltip("Показывать warning при отсутствии visualPrefab или кости.")]
        [SerializeField] private bool _logWarnings = true;

        // === Runtime state ===

        // Slot → spawned visual GameObject (null = slot empty или без визуала).
        private readonly Dictionary<EquipSlot, GameObject> _spawnedVisuals = new();

        // Slot → ItemData, чтобы знать какой визуал закреплён (для diff).
        private readonly Dictionary<EquipSlot, ItemData> _currentItems = new();

        private EquipmentClientState _clientState;

        // === Lifecycle ===

        private void OnEnable()
        {
            _clientState = EquipmentClientState.Instance;
            if (_clientState != null) _clientState.OnEquipmentUpdated += OnEquipmentUpdated;
        }

        private void OnDisable()
        {
            if (_clientState != null) _clientState.OnEquipmentUpdated -= OnEquipmentUpdated;
            DestroyAllVisuals();
        }

        private void OnEquipmentUpdated(EquipmentSnapshotDto snapshot)
        {
            // Diff: для каждого EquipSlot — сравнить snapshot vs _currentItems.
            // - В snapshot есть, в _currentItems нет или другой → spawn
            // - В snapshot нет, в _currentItems был → destroy
            // Подробности — в 02_CHARACTER_APPLIER.md §3.
        }
    }
}
```

### Поля для отслеживания

| Поле | Тип | Назначение |
|---|---|---|
| `_spawnedVisuals` | `Dictionary<EquipSlot, GameObject>` | Что сейчас висит на персонаже по слотам. Ключ — slot, value — root GO visualPrefab. |
| `_currentItems` | `Dictionary<EquipSlot, ItemData>` | Какой ItemData сейчас экипирован (для diff'а snapshot vs current). |
| `_clientState` | `EquipmentClientState` | Singleton, на который подписываемся. |

---

## 4. Тестовые ассеты (Phase 1.3)

Создадим 2-3 минимальных visualPrefab для проверки. Положим в:
`Assets/_Project/Resources/Visuals/Equipment/`

| Имя | Назначение | Содержание |
|---|---|---|
| `Visual_Helmet_Cone.prefab` | Stand-in для "Рабочая каска" (seed helmet, Head) | Один GameObject с MeshFilter (cone или sphere) + MeshRenderer (Material default URP). localScale = 0.3. |
| `Visual_Blade_Capsule.prefab` | Stand-in для "Антиграв-клинок" (AntigravBlade) | Capsule (mesh + material) localScale (0.05, 0.6, 0.05). |
| `Visual_Boots_SmallCapsule.prefab` | Stand-in для ботинок (Feet) | Два маленьких capsule'а на dummy root. |

**Зачем stand-in:** MVP не блокируется на художнике. Дизайнер позже заменит на нормальные меши.

---

## 5. Что НЕ идёт в data model

| Идея | Почему нет |
|---|---|
| `SkinnedMeshRenderer[] bodyMeshes` | Уже есть внутри visualPrefab. Не дублируем. |
| `Material override per item` | Уже есть внутри visualPrefab. |
| `Bone weights` (привязка скина к скелету) | Только для SkinnedMeshRenderer в visualPrefab. Настраивается в FBX. |
| `Animation clip override` | Не нужно — персонаж использует свой Animator. VisualPrefab статичен. |
| `Physics collider на visualPrefab` | Опционально — designer сам добавит если нужно. |

---

## 6. Совместимость с существующими системами

| Система | Взаимодействие | Действие |
|---|---|---|
| `PickupItem` (Phase 1.5) | Если `visualPrefab != null` → spawn как child PickupItem вместо SpriteRenderer | Дополняем опционально |
| `EquipmentServer` | Не трогаем — он работает с itemId/slot, без визуала | Без изменений |
| `EquipmentClientState` | Используем существующее событие `OnEquipmentUpdated` как hook | Без изменений |
| `NpcVisualApplier` | Независимая подсистема для NPC. Не конфликтует — у нас свой компонент на Player | Без изменений |
| `SkillInputService` / combat | Использует WeaponItemData (ItemData). VisualPrefab опционален, не влияет на combat | Без изменений |
| `StatsServer` | Stat bonuses считаются из `ClothingItemData`, не из visualPrefab | Без изменений |

---

## 7. Структура файлов после реализации

```
Assets/_Project/
├── Scripts/
│   ├── Core/
│   │   └── ItemType.cs                     [+ visualPrefab + attach* поля]
│   ├── Core/
│   │   └── PickupItem.cs                   [Phase 1.5: опциональный spawn visualPrefab]
│   ├── Equipment/
│   │   ├── Visual/
│   │   │   ├── EquipSlotToBone.cs          [NEW — Phase 2.1]
│   │   │   └── (будущее: EquipVisualAttachData.cs если выделим)
│   │   ├── ClothingItemData.cs             [без изменений — наследует visualPrefab от ItemData]
│   │   ├── ModuleItemData.cs               [без изменений]
│   │   └── WeaponItemData.cs               [без изменений]
│   └── Player/
│       ├── NetworkPlayer.cs                [без изменений]
│       └── CharacterEquipmentVisualApplier.cs  [NEW — Phase 2.3]
└── Resources/
    └── Visuals/
        └── Equipment/
            ├── Visual_Helmet_Cone.prefab   [NEW — Phase 1.3]
            ├── Visual_Blade_Capsule.prefab [NEW — Phase 1.3]
            └── Visual_Boots_SmallCapsule.prefab  [NEW — Phase 1.3]

docs/Character/
└── EquipmentVisual/
    ├── 00_DESIGN.md                        ✅ (этот документ)
    ├── 01_DATA_MODEL.md                    ✅ (этот документ)
    ├── 02_CHARACTER_APPLIER.md             [Phase 2]
    └── 03_PHASES.md                        [Phase 2]
```