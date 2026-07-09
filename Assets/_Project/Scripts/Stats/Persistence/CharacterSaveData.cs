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
using ProjectC.Customisation;  // T-CUS-01: additive — CustomisationSave секция

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
        // T-P12: skills persistence (learned skill IDs). Полная версия в T-P13 (StatsServer owns persistence).
        public SkillsSave skills = new SkillsSave();
        // T-CUS-01: Customisation (additive — body type/preset/proportions/colors/hair/clothing overrides).
        // Backward-compat: старые .json без этого поля загружаются с default = Male, identity colors.
        public CustomisationSave customisation = new CustomisationSave();
    }

    /// <summary>
    /// PlayerStatsSave: P1 refactor — 3 StatBuckets вместо 9 flat fields.
    /// JsonUtility сериализует массив [Serializable] структур.
    /// Индексы: [0]=Strength, [1]=Dexterity, [2]=Intelligence.
    /// </summary>
    [Serializable]
    public class PlayerStatsSave
    {
        public StatBucket[] buckets = new StatBucket[3];

        public static PlayerStatsSave FromPlayerStats(PlayerStats s) => new PlayerStatsSave
        {
            buckets = new StatBucket[]
            {
                s.strength,
                s.dexterity,
                s.intelligence,
            },
        };

        public PlayerStats ToPlayerStats() => new PlayerStats
        {
            strength    = buckets != null && buckets.Length > 0 ? buckets[0] : StatBucket.Default,
            dexterity   = buckets != null && buckets.Length > 1 ? buckets[1] : StatBucket.Default,
            intelligence = buckets != null && buckets.Length > 2 ? buckets[2] : StatBucket.Default,
        };
    }
}
