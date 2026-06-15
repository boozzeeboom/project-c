// Project C: Character Progression — T-P02
// PlayerStats: server-side state + StatType enum (переехал сюда из T-P01 stub в StatsConfig.cs).
// Design: docs/Character/03_DATA_MODEL.md §1, docs/Character/04_STATS_PROGRESSION.md §1.2, §1.5
//
// Это [Serializable] struct, не class —
// (1) NetworkVariable / NetworkSerialize работает со struct напрямую;
// (2) копия struct дешёвая (16 байт: 3 floats + 3 ints + 3 floats + padding);
// (3) "по умолчанию" = нулевые значения = стартовый профиль игрока (Q1.1: 0/0/0).
//
// Helper: PlayerStatsRef — static class с `ref` returns, чтобы ApplyXp/Recompute в T-P05
// не дублировал switch (3 характеристики × 3 поля = 9 блоков).

using System;

namespace ProjectC.Stats
{
    /// <summary>
    /// Тип характеристики. Перенесён из T-P01 stub в StatsConfig.cs:21-31.
    /// Тот же namespace, те же значения (Strength=0, Dexterity=1, Intelligence=2) — миграция прозрачна.
    /// </summary>
    public enum StatType : byte
    {
        Strength     = 0,
        Dexterity    = 1,
        Intelligence = 2,
    }

    /// <summary>
    /// Server-side state игрока: XP в текущем тире + tier + cumulative total.
    /// Используется StatsWorld (POCO singleton) для хранения Dictionary&lt;ulong, PlayerStats&gt;.
    /// </summary>
    [Serializable]
    public struct PlayerStats
    {
        // === current XP in current tier ===
        public float strength;
        public float dexterity;
        public float intelligence;

        // === current tier (количество полных тиров) ===
        public int strengthTier;
        public int dexterityTier;
        public int intelligenceTier;

        // === cumulative XP (для UI "всего заработано") ===
        public float strengthTotalXp;
        public float dexterityTotalXp;
        public float intelligenceTotalXp;

        /// <summary>
        /// Стартовый профиль (Q1.1: 0/0/0). PlayerStats.Default == новый игрок.
        /// </summary>
        public static PlayerStats Default => new PlayerStats
        {
            strength = 0f, dexterity = 0f, intelligence = 0f,
            strengthTier = 0, dexterityTier = 0, intelligenceTier = 0,
            strengthTotalXp = 0f, dexterityTotalXp = 0f, intelligenceTotalXp = 0f,
        };
    }
}
