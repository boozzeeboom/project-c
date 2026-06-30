// Project C: Character Customisation — T-CUS-01 + T-CUS-09 + T-CUS-10
// HairStyleId: стиль волос (для L4+). Hair mesh подцепляется к кости Head по аналогии с
// Equipment Visual. 0=Bald (нет волос — скипаем spawn hair mesh).
// Design: docs/Character/Customisation/02_DATA_MODEL.md §2.3

namespace ProjectC.Customisation
{
    /// <summary>
    /// Стиль волос. Hair mesh подцепляется к кости Head через EquipSlotToBone.Head + spawn hair prefab.
    /// Bald = нет волос, hair mesh не спавнится.
    /// </summary>
    public enum HairStyleId : byte
    {
        Bald      = 0,
        Short     = 1,
        // Будет расширяться: Medium, Long, Ponytail, и т.п. когда дизайнер сделает mesh-ассеты.
    }
}