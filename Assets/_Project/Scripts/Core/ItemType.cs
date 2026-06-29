using UnityEngine;

namespace ProjectC.Items
{
    public enum ItemType
    {
        Resources = 0,
        Equipment = 1,
        Food = 2,
        Fuel = 3,
        Antigrav = 4,
        Meziy = 5,
        Medical = 6,
        Tech = 7,
        // R2-SHIP-KEY-003 (T-KEY-01, Q1, 2026-06-18): отдельный тип для ключей кораблей.
        // Позволяет чётко фильтровать KeyRods в PickupItem / Inventory UI.
        // ВНИМАНИЕ: не переименовывать и не удалять — instance-id слой (T-KEY-02)
        // рассчитывает на это значение для slot extension.
        Key = 8
    }

    [CreateAssetMenu(fileName = "NewItem", menuName = "Project C/Item Data", order = 1)]
    public class ItemData : ScriptableObject
    {
        public string itemName;
        public ItemType itemType;
        [TextArea]
        public string description;
        public Sprite icon;

        // Phase 6 (INVENTORY_V2_REFACTOR.md): доп-поля для stack + weight.
        // maxStack = 1 значит non-stackable (по умолчанию, обратная совместимость с
        // существующими Item_Type1..8). weightKg — для будущего cargo system.
        [Header("Stack & Weight (Phase 6)")]
        [Tooltip("Сколько таких предметов может быть в одном слоте. 1 = non-stackable.")]
        public int   maxStack = 1;
        [Tooltip("Вес одного предмета (кг). Используется в будущей cargo-системе.")]
        public float weightKg = 0.1f;

        // === Equipment Visual System (Phase 1, 2026-06-29) ===
        // Опциональные поля для 3D-визуала предмета. Default = null/zero означает
        // старое поведение: предмет отображается только иконкой (Sprite).
        // Additive-only: существующие ~500 .asset'ов остаются без изменений.
        //
        // Phase 1: visualPrefab используется designer'ом для создания world-pickup префабов
        //   (дизайнер собирает сцену-пикап из этого префаба + PickupItem компонент).
        // Phase 2: CharacterEquipmentVisualApplier (Assets/_Project/Scripts/Player/) спавнит
        //   visualPrefab как child кости скелета персонажа M при экипировке.
        // Design: docs/Character/EquipmentVisual/00_DESIGN.md, 01_DATA_MODEL.md §1
        [Header("Visual (Equipment Visual System — Phase 1)")]
        [Tooltip("3D-меш/prefab для отображения предмета в мире и на персонаже при экипировке. " +
                 "Если null — предмет отображается только иконкой (старое поведение, " +
                 "никаких изменений не требуется). Дизайнер собирает world-pickup префаб " +
                 "из этого visualPrefab + PickupItem компонент (см. SetupEquipmentVisualAssets.cs).")]
        public GameObject visualPrefab;

        // === Attach to Character (Phase 2) ===
        // Per-item override для случаев, когда default маппинг EquipSlot → кость не подходит.
        // LastBone (54) — sentinel "использовать default маппинг". См. EquipSlotToBone.cs.
        [Header("Attach to Character (Phase 2 — optional)")]
        [Tooltip("Кость для прикрепления visualPrefab при экипировке. " +
                 "LastBone (default) = использовать default маппинг по EquipSlot " +
                 "(см. EquipSlotToBone.cs). Override для специфичных случаев: " +
                 "двуручный меч, шляпа-цилиндр, аксессуар с конкретным расположением.")]
        public HumanBodyBones attachBoneOverride = HumanBodyBones.LastBone;

        [Tooltip("Локальный offset от кости к прикреплённому visualPrefab (в local space кости).")]
        public Vector3 attachPositionOffset = Vector3.zero;

        [Tooltip("Локальное вращение visualPrefab относительно кости (Euler degrees).")]
        public Vector3 attachRotationOffset = Vector3.zero;

        [Tooltip("Локальный масштаб visualPrefab. (1,1,1) = без изменений. Полезно если меш " +
                 "импортирован в неправильном масштабе (например, cm vs m).")]
        public Vector3 attachScale = Vector3.one;
    }
}