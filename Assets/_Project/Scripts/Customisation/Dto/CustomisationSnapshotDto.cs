// Project C: Character Customisation — T-CUS-02
// CustomisationSnapshotDto: in-memory snapshot текущего customisation игрока.
// Variant A (client-only persistence): не передаётся по сети, используется только клиентом
// для ApplyOnApplier и UI bindings.
// Design: docs/Character/Customisation/02_DATA_MODEL.md §4
//
// Связь с CustomisationSave:
//   - CustomisationSave = persistence DTO (JsonUtility, public fields, default values).
//   - CustomisationSnapshotDto = runtime snapshot (struct, compact, для Apply methods + UI).
//   - Маппинг: CustomisationMappers.FromSave(CustomisationSave) → CustomisationSnapshotDto.

using System;
using UnityEngine;
using ProjectC.Equipment;

namespace ProjectC.Customisation.Dto
{
    /// <summary>
    /// Runtime snapshot customisation игрока. Применяется CharacterCustomisationApplier.
    /// </summary>
    [Serializable]
    public struct CustomisationSnapshotDto
    {
        public CharacterBodyType bodyType;
        public BodyPresetId presetId;

        // L3
        public float heightScale;
        public float widthScale;

        // L4 — цвета
        public float skinColorR, skinColorG, skinColorB, skinColorA;
        public float hairColorR, hairColorG, hairColorB, hairColorA;

        // L4
        public HairStyleId hairStyle;

        // L4 — per-EquipSlot overrides для одежды
        public ClothingColorOverrideDto[] clothingOverrides;

        public Color GetSkinColor() => new Color(skinColorR, skinColorG, skinColorB, skinColorA);
        public Color GetHairColor() => new Color(hairColorR, hairColorG, hairColorB, hairColorA);
    }

    /// <summary>
    /// Per-EquipSlot color override для экипировки.
    /// </summary>
    [Serializable]
    public struct ClothingColorOverrideDto
    {
        public EquipSlot slot;
        public float colorR, colorG, colorB, colorA;

        public Color GetColor() => new Color(colorR, colorG, colorB, colorA);

        public static ClothingColorOverrideDto FromSave(ClothingColorOverrideSave s) =>
            new ClothingColorOverrideDto { slot = s.slot, colorR = s.colorR, colorG = s.colorG, colorB = s.colorB, colorA = s.colorA };
    }
}