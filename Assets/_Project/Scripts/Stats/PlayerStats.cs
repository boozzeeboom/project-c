// Project C: Character Progression — P1 refactor
// PlayerStats: StatBucket × 3 + static ref accessors (merge of old PlayerStatsRef).
//
// Design: docs/Character/03_DATA_MODEL.md §1, docs/Character/04_STATS_PROGRESSION.md §1.2
// Session: P1 (13_SESSION_CONTINUATION.md)

using System;

namespace ProjectC.Stats
{
    /// <summary>
    /// Тип характеристики (Strength=0, Dexterity=1, Intelligence=2).
    /// </summary>
    public enum StatType : byte
    {
        Strength     = 0,
        Dexterity    = 1,
        Intelligence = 2,
    }

    /// <summary>
    /// Per-stat data bucket: XP in current tier + tier level + cumulative total XP.
    /// P1: replaces 3×3 flat fields with typed grouping.
    /// </summary>
    [Serializable]
    public struct StatBucket
    {
        public float xp;
        public int tier;
        public float totalXp;

        public static StatBucket Default => new StatBucket { xp = 0f, tier = 0, totalXp = 0f };
    }

    /// <summary>
    /// Server-side state игрока: 3 StatBuckets indexed by StatType.
    /// P1 refactor: flat fields → StatBucket grouping.
    ///
    /// Static ref accessors (merge of old PlayerStatsRef) avoid CS8170.
    /// Usage:
    ///   ref float xp = ref PlayerStats.Xp(ref stats, StatType.Strength);
    ///   ref int tier = ref PlayerStats.Tier(ref stats, StatType.Dexterity);
    ///   StatBucket bucket = stats[StatType.Intelligence];
    /// </summary>
    [Serializable]
    public struct PlayerStats
    {
        // --- Buckets (one per StatType enum value) ---
        public StatBucket strength;
        public StatBucket dexterity;
        public StatBucket intelligence;

        /// <summary>Read-only copy of a StatBucket by stat type.</summary>
        public StatBucket this[StatType stat]
        {
            get
            {
                switch (stat)
                {
                    case StatType.Strength:     return strength;
                    case StatType.Dexterity:    return dexterity;
                    case StatType.Intelligence: return intelligence;
                    default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown StatType");
                }
            }
        }

        // === Static ref accessors (same pattern as old PlayerStatsRef, now on PlayerStats itself) ===

        /// <summary>Ref to current XP field in the stat's bucket.</summary>
        public static ref float Xp(ref PlayerStats stats, StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:     return ref stats.strength.xp;
                case StatType.Dexterity:    return ref stats.dexterity.xp;
                case StatType.Intelligence: return ref stats.intelligence.xp;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown StatType");
            }
        }

        /// <summary>Ref to tier field in the stat's bucket.</summary>
        public static ref int Tier(ref PlayerStats stats, StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:     return ref stats.strength.tier;
                case StatType.Dexterity:    return ref stats.dexterity.tier;
                case StatType.Intelligence: return ref stats.intelligence.tier;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown StatType");
            }
        }

        /// <summary>Ref to cumulative total XP field in the stat's bucket.</summary>
        public static ref float TotalXp(ref PlayerStats stats, StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:     return ref stats.strength.totalXp;
                case StatType.Dexterity:    return ref stats.dexterity.totalXp;
                case StatType.Intelligence: return ref stats.intelligence.totalXp;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown StatType");
            }
        }

        /// <summary>Ref to the entire StatBucket for a given stat type.</summary>
        public static ref StatBucket GetBucket(ref PlayerStats stats, StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:     return ref stats.strength;
                case StatType.Dexterity:    return ref stats.dexterity;
                case StatType.Intelligence: return ref stats.intelligence;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown StatType");
            }
        }

        // === Read-only convenience ===

        public static float GetXp(in PlayerStats stats, StatType stat) => stats[stat].xp;
        public static int GetTier(in PlayerStats stats, StatType stat) => stats[stat].tier;
        public static float GetTotalXp(in PlayerStats stats, StatType stat) => stats[stat].totalXp;

        // === Helpers ===

        /// <summary>Стартовый профиль (Q1.1: 0/0/0).</summary>
        public static PlayerStats Default => default;

        /// <summary>tier * 5 + 10: tier0=10, tier1=15, tier2=20, ...</summary>
        public static int StatsToFlat(int tier) => tier * 5 + 10;
    }
}
