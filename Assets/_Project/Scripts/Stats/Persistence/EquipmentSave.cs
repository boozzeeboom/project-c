// Project C: Character Progression — T-P09 (STUB-ADD) — полная версия в T-P06 расширении
// EquipmentSave: parallel DTO к EquipmentData (byte[13] + int[13]). JsonUtility-friendly.
//
// T-P09: добавлено как sub-stub чтобы EquipmentWorld мог ссылаться на него и CharacterSaveData.equipment.
// T-P06 (JsonCharacterDataRepository) уже сериализует весь CharacterSaveData — equipment/skills добавятся
// прозрачно. Существующие .json файлы (только stats) читаются с equipment = default (zero arrays).

using System;

namespace ProjectC.Stats.Persistence
{
    [Serializable]
    public class EquipmentSave
    {
        public byte[] slotOccupied = new byte[ProjectC.Equipment.EquipmentData.SLOT_COUNT];
        public int[] slotItemIds   = new int[ProjectC.Equipment.EquipmentData.SLOT_COUNT];
    }
}
