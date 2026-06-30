// Project C: Character Customisation — T-CUS-01
// BodyPresetId: пресет тела (для L2+). Зарезервировано на будущее — в L1 не используется,
// CustomisationSave.presetId = Default. L2 добавит Athletic/Heavy/Slim/Elder/Young.
// Design: docs/Character/Customisation/02_DATA_MODEL.md §2.2

namespace ProjectC.Customisation
{
    /// <summary>
    /// Пресет тела (для L2+). 0=Default (no override, используется текущий mesh).
    /// </summary>
    public enum BodyPresetId : byte
    {
        Default  = 0,
        Athletic = 1,
        Heavy    = 2,
        Slim     = 3,
        Elder    = 4,
        Young    = 5,
    }
}