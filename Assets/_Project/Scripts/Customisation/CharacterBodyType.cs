// Project C: Character Customisation — T-CUS-01
// CharacterBodyType: пол персонажа. Affects base mesh (HumanM/HumanF) и AnimatorOverrideController.
// Не влияет на stats/skills/equipment pipeline/combat — orthogonal dimension.
// Design: docs/Character/Customisation/02_DATA_MODEL.md §2.1

namespace ProjectC.Customisation
{
    /// <summary>
    /// Пол персонажа. 0=Male (default, backward-compat), 1=Female.
    /// Используется в CustomisationSave + CustomisationSnapshotDto.
    /// </summary>
    public enum CharacterBodyType : byte
    {
        Male   = 0,
        Female = 1,
    }
}