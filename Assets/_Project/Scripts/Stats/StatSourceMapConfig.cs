// Project C: Character Progression — T-P01 refactor (P4)
// StatSourceMapConfig: XpSource → StatType mapping.
// Вынесен из StatsConfig согласно аудиту 12_STATS_ARCHITECTURE_AUDIT_V2.md §4, Q2.6.
//
// Позволяет иметь разные mapping для PvP-зон, туториалов без дублирования всего SO.

using UnityEngine;

namespace ProjectC.Stats
{
    [CreateAssetMenu(fileName = "StatSourceMapConfig", menuName = "Project C/Stats/Stat Source Map Config", order = 12)]
    public class StatSourceMapConfig : ScriptableObject
    {
        /// <summary>HARDCODED source → stat mapping. При необходимости вариативности — заменить на SerializeField per-source.</summary>
        public StatType GetStatFor(XpSource source) => source switch
        {
            XpSource.Mining         => StatType.Strength,
            XpSource.Walk           => StatType.Dexterity,
            XpSource.Jump           => StatType.Dexterity,
            XpSource.Pilot          => StatType.Intelligence,
            XpSource.Crafting       => StatType.Intelligence,
            XpSource.Exchange       => StatType.Intelligence,
            XpSource.Market         => StatType.Intelligence,
            XpSource.QuestAccepted  => StatType.Intelligence,
            XpSource.QuestCompleted => StatType.Intelligence,
            XpSource.Dialog         => StatType.Intelligence,
            _                       => StatType.Intelligence,
        };
    }
}
