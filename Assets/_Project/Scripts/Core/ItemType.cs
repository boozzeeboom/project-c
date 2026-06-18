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
    }
}
