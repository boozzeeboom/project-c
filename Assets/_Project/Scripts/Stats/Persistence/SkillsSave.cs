// Project C: Character Progression — T-P12 (STUB) — T-P13 расширит
// SkillsSave: parallel DTO к learned skill IDs + cooldowns. JsonUtility-friendly.
// T-P12: только skills (для T-P13 stats + equipment уже в CharacterSaveData).

using System;
using ProjectC.Equipment;
using ProjectC.Stats;

namespace ProjectC.Stats.Persistence
{
    [Serializable]
    public class SkillsSave
    {
        public string[] learnedSkillIds = Array.Empty<string>();
        // T-P12: NPC dialog cooldowns TBD (Q1.4 unique-event уже в StatsWorld, но timestamps персистить для сохранения между сессиями)
    }
}
