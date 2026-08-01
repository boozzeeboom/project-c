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
        // T-P13: learned (выученные) навыки
        public string[] learnedSkillIds = Array.Empty<string>();

        // T-KNOWLEDGE-V2: известные навыки (не выученные, а просто «знаю что существует»)
        // null для старых сейвов = пустой массив (backward compat)
        public string[] knownSkillIds = null;

        // T-P12: NPC dialog cooldowns TBD
=======
 (Q1.4 unique-event уже в StatsWorld, но timestamps персистить для сохранения между сессиями)
    }
}
