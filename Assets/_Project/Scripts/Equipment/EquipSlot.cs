// Project C: Character Progression — T-P07 (stub-forward-declare) → полная версия T-P08
// EquipSlot enum: 13 слотов (Head..WeaponOff, Module1..3) с gap для None.
// Design: docs/Character/05_CLOTHING_AND_MODULES.md §1.1, docs/Character/08_ROADMAP.md T-P08
//
// T-P07 stub-нужен: ClothingItemData/ModuleItemData ссылаются на EquipSlot. Полный EquipmentData
// (parallel arrays + SlotToIndex/IndexToSlot) придёт в T-P08 — там же будет EquipmentServer.

namespace ProjectC.Equipment
{
    /// <summary>
    /// Слот экипировки. None=0 (invalid), Head..WeaponOff=1..10, Module1..3=20..22 (gap для будущих).
    /// </summary>
    /// <remarks>
    /// Значения byte (1 байт) для compact DTO. Module1..3 имеют непоследовательные номера (20-22)
    /// чтобы оставить gap для будущих слотов между одеждой и модулями. SlotToIndex/IndexToSlot
    /// normalization в EquipmentData (T-P08).
    /// </remarks>
    public enum EquipSlot : byte
    {
        None        = 0,
        Head        = 1,
        Chest       = 2,
        Legs        = 3,
        Feet        = 4,
        Back        = 5,
        Hands       = 6,
        Accessory1  = 7,
        Accessory2  = 8,
        WeaponMain  = 9,
        WeaponOff   = 10,
        Module1     = 20,
        Module2     = 21,
        Module3     = 22,
    }
}
