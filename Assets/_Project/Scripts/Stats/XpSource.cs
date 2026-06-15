// Project C: Character Progression — T-P01
// XpSource enum: источник XP для StatsServer.
// HARDCODED mapping source → stat — см. 03_DATA_MODEL.md §1.1 / 04_STATS_PROGRESSION.md §2.1
// (Q10.3: пользователь сказал "не нужна вариативность" — нет per-source targetStat field).

namespace ProjectC.Stats
{
    /// <summary>
    /// Источник XP. Порядок = display order в инспекторе. Не переставлять без миграции сохранений.
    /// Используется в StatsConfig.GetStatFor / GetBaseXp, и в WorldEventBus payload-логике StatsServer.
    /// </summary>
    public enum XpSource : byte
    {
        Mining         = 0,
        Crafting       = 1,
        Exchange       = 2,
        Market         = 3,
        QuestAccepted  = 4,
        QuestCompleted = 5,
        Dialog         = 6,
        Jump           = 7,
        Walk           = 8,
        Pilot          = 9,
    }
}
