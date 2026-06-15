// Project C: Character Progression — T-P03 (stub-forward-declare)
// CharacterSaveData: parallel DTO для JsonUtility (JsonUtility НЕ сериализует Dictionary<,>).
// Design: docs/Character/03_DATA_MODEL.md §5.1, docs/Character/08_ROADMAP.md T-P06
//
// Это STUB — полная версия придёт в T-P06 (JsonCharacterDataRepository).
// Реально используемые поля (stats) уже здесь, чтобы T-P05 (StatsServer) мог
// вызывать BuildSaveData / LoadPlayer через эти типы.
//
// В T-P06 сюда добавятся: EquipmentSave (slotOccupied/slotItemIds), SkillsSave
// (learnedSkillIds/dialogCooldowns) — JsonUtility их сериализует как есть.

using System;

namespace ProjectC.Stats.Persistence
{
    /// <summary>
    /// T-P03 STUB — полная версия в T-P06. JsonUtility-friendly DTO для character_&lt;clientId&gt;.json.
    /// </summary>
    [Serializable]
    public class CharacterSaveData
    {
        public PlayerStatsSave stats = new PlayerStatsSave();
        // T-P09 STUB add — T-P06 (JsonCharacterDataRepository) уже имеет stats; equipment/skills придут в T-P09 расширении
        public EquipmentSave equipment = new EquipmentSave();
        // public SkillsSave skills = new SkillsSave();  // T-P13
    }

    /// <summary>
    /// PlayerStatsSave: parallel DTO к PlayerStats (3 floats + 3 ints + 3 floats).
    /// JsonUtility сериализует public fields.
    /// </summary>
    [Serializable]
    public class PlayerStatsSave
    {
        public float strength;
        public float dexterity;
        public float intelligence;

        public int strengthTier;
        public int dexterityTier;
        public int intelligenceTier;

        public float strengthTotalXp;
        public float dexterityTotalXp;
        public float intelligenceTotalXp;

        public static PlayerStatsSave FromPlayerStats(PlayerStats s) => new PlayerStatsSave
        {
            strength = s.strength,
            dexterity = s.dexterity,
            intelligence = s.intelligence,
            strengthTier = s.strengthTier,
            dexterityTier = s.dexterityTier,
            intelligenceTier = s.intelligenceTier,
            strengthTotalXp = s.strengthTotalXp,
            dexterityTotalXp = s.dexterityTotalXp,
            intelligenceTotalXp = s.intelligenceTotalXp,
        };

        public PlayerStats ToPlayerStats() => new PlayerStats
        {
            strength = strength,
            dexterity = dexterity,
            intelligence = intelligence,
            strengthTier = strengthTier,
            dexterityTier = dexterityTier,
            intelligenceTier = intelligenceTier,
            strengthTotalXp = strengthTotalXp,
            dexterityTotalXp = dexterityTotalXp,
            intelligenceTotalXp = intelligenceTotalXp,
        };
    }
}
