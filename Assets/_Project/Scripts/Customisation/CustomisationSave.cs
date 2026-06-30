// Project C: Character Customisation — T-CUS-01 + T-CUS-09 + T-CUS-10
// CustomisationSave: JsonUtility-friendly DTO для persistence.
// Additive поле в CharacterSaveData — старые .json без этого поля загружаются с default (Male).
// Design: docs/Character/Customisation/02_DATA_MODEL.md §3
//
// Принципы:
//   - Все поля public (JsonUtility требует).
//   - Default values = Male preset, heightScale=1.0, white colors (текущее поведение).
//   - Color хранится как 4 float'а — JsonUtility не сериализует Color напрямую в некоторых версиях.
//   - Без Dictionary — JsonUtility их не сериализует. Используем массив SerializableStringColorPair.

using System;
using ProjectC.Equipment;  // EquipSlot (T-P07) — для ClothingColorOverrideSave.slot
using UnityEngine;

namespace ProjectC.Customisation
{
    /// <summary>
    /// JsonUtility-friendly DTO для character customisation. Default = Male preset (backward-compat).
    /// </summary>
    [Serializable]
    public class CustomisationSave
    {
        // === Body (L1) ===

        [Tooltip("Тип тела. 0=Male (default), 1=Female.")]
        public CharacterBodyType bodyType = CharacterBodyType.Male;

        [Tooltip("Пресет тела (для L2+). 0=Default (используется как есть, без override).")]
        public BodyPresetId presetId = BodyPresetId.Default;

        // === Proportions (L3 — слайдеры тела) ===

        [Range(0.8f, 1.2f)]
        [Tooltip("Общий масштаб по Y (рост). 1.0 = default. Применяется через Visual_Model.localScale.y.")]
        public float heightScale = 1.0f;

        [Range(0.7f, 1.3f)]
        [Tooltip("Масштаб по XZ (полнота/ширина). 1.0 = default. Применяется через Visual_Model.localScale.x/z.")]
        public float widthScale = 1.0f;

        // === Colors (L4 — покраска) ===

        [Tooltip("Цвет кожи (RGBA). Default = white (берётся material персонажа как есть).")]
        public float skinColorR = 1.0f;
        public float skinColorG = 1.0f;
        public float skinColorB = 1.0f;
        public float skinColorA = 1.0f;

        [Tooltip("Цвет волос (RGBA). Default = белый (берётся material hair mesh как есть).")]
        public float hairColorR = 1.0f;
        public float hairColorG = 1.0f;
        public float hairColorB = 1.0f;
        public float hairColorA = 1.0f;

        // === Hair style (L4+) ===

        [Tooltip("Стиль волос. 0=Bald (нет волос), 1=Short, ... См. HairStyleId.")]
        public HairStyleId hairStyle = HairStyleId.Short;

        // === Clothing color overrides (L4 — покраска экипировки) ===
        // Per-EquipSlot override — игрок перекрашивает надетую одежду.
        // Default = пустой массив (используется материал предмета как есть).
        // Ключ = EquipSlot.ToString() ("Head", "Chest", "WeaponMain", ...).

        [Tooltip("Per-EquipSlot color override. Max 13 элементов (по числу EquipSlot).")]
        public ClothingColorOverrideSave[] clothingColorOverrides = Array.Empty<ClothingColorOverrideSave>();

        // === Helpers ===

        public Color GetSkinColor() => new Color(skinColorR, skinColorG, skinColorB, skinColorA);
        public Color GetHairColor() => new Color(hairColorR, hairColorG, hairColorB, hairColorA);

        public void SetSkinColor(Color c) { skinColorR = c.r; skinColorG = c.g; skinColorB = c.b; skinColorA = c.a; }
        public void SetHairColor(Color c) { hairColorR = c.r; hairColorG = c.g; hairColorB = c.b; hairColorA = c.a; }
    }

    /// <summary>
    /// Per-EquipSlot color override для экипировки.
    /// Хранится в CustomisationSave.clothingColorOverrides[].
    /// </summary>
    [Serializable]
    public struct ClothingColorOverrideSave
    {
        public EquipSlot slot;  // см. ProjectC.Equipment.EquipSlot
        public float colorR;
        public float colorG;
        public float colorB;
        public float colorA;

        public Color GetColor() => new Color(colorR, colorG, colorB, colorA);
        public void SetColor(Color c) { colorR = c.r; colorG = c.g; colorB = c.b; colorA = c.a; }

        public static ClothingColorOverrideSave From(EquipSlot s, Color c) =>
            new ClothingColorOverrideSave { slot = s, colorR = c.r, colorG = c.g, colorB = c.b, colorA = c.a };
    }
}