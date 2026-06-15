// Project C: Character Progression — T-P02
// PlayerStatsRef: static helper с `ref` returns — анти-копипаст для ApplyXp/Recompute.
// Design: docs/Character/04_STATS_PROGRESSION.md §1.5
//
// Без этого helper'а ApplyXp в T-P05 выглядел бы так (9 блоков копипаста):
//   switch (stat) {
//       case StatType.Strength: ref to stats.strengthTier
//       case StatType.Dexterity: ref to stats.dexterityTier
//       case StatType.Intelligence: ref to stats.intelligenceTier
//   }
// С helper'ом — одна строка: `ref int tier = ref PlayerStatsRef.GetTierRef(ref stats, stat);`.
//
// `ref` returns C# 7.0+, никаких опасностей: возвращаемый ref указывает на поле struct
// по адресу вызывающего, GC не вовлечён, lifetime = до выхода из scope вызывающего.

using System;

namespace ProjectC.Stats
{
    /// <summary>
    /// Static helper: ref-access к полям PlayerStats по StatType.
    /// Позволяет T-P05 (StatsServer.ApplyXp) писать в одно место вместо switch'а × 3 поля.
    /// </summary>
    public static class PlayerStatsRef
    {
        public static ref float GetXpRef(ref PlayerStats stats, StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:     return ref stats.strength;
                case StatType.Dexterity:    return ref stats.dexterity;
                case StatType.Intelligence: return ref stats.intelligence;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown StatType");
            }
        }

        public static ref int GetTierRef(ref PlayerStats stats, StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:     return ref stats.strengthTier;
                case StatType.Dexterity:    return ref stats.dexterityTier;
                case StatType.Intelligence: return ref stats.intelligenceTier;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown StatType");
            }
        }

        public static ref float GetTotalXpRef(ref PlayerStats stats, StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:     return ref stats.strengthTotalXp;
                case StatType.Dexterity:    return ref stats.dexterityTotalXp;
                case StatType.Intelligence: return ref stats.intelligenceTotalXp;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown StatType");
            }
        }
    }
}
