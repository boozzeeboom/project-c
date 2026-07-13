// Project C: Character Progression — T-P02
// StatsSnapshotDto: server → client sync payload.
// INetworkSerializable struct — для TargetRPC через NetworkPlayer (T-P05).
// Design: docs/Character/02_V2_ARCHITECTURE.md §2.4, docs/Character/03_DATA_MODEL.md §1.x
//
// Содержит всё что нужно UI для отображения:
//   - 3 current XP (для progress bar fill width)
//   - 3 tiers (для tier class в UI: tier-low/mid/high/master)
//   - 3 XpForNextTier (computed на сервере из StatsConfig — клиент не считает формулу)
//   - 3 totalXp (для "всего заработано")
// Итого: 12 полей. NGO 2.x null-string pitfall (#22 в unity-mcp-orchestrator) —
// stringParam'ов нет, все примитивы, риска NRE нет.

using System;
using Unity.Netcode;

namespace ProjectC.Stats.Dto
{
    [Serializable]
    public struct StatsSnapshotDto : INetworkSerializable, IEquatable<StatsSnapshotDto>
    {
        // === current XP in current tier (3 floats) ===
        public float strength;
        public float dexterity;
        public float intelligence;

        // === current tier (3 ints) ===
        public int strengthTier;
        public int dexterityTier;
        public int intelligenceTier;

        // === XP required для следующего tier (3 floats, computed на сервере) ===
        public float strengthXpForNextTier;
        public float dexterityXpForNextTier;
        public float intelligenceXpForNextTier;

        // === cumulative total XP (3 floats) ===
        public float strengthTotalXp;
        public float dexterityTotalXp;
        public float intelligenceTotalXp;

        // SESSION 2: effective stats = base + equip bonuses (для UI).
        // Серверная сторона заполняет effective, base остаётся в strength/dexterity/intelligence.
        public float effectiveStrength;
        public float effectiveDexterity;
        public float effectiveIntelligence;

        // T-HP01: health fields (server-computed, sent to client for UI)
        public int currentHp;
        public int maxHp;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref strength);
            serializer.SerializeValue(ref dexterity);
            serializer.SerializeValue(ref intelligence);

            serializer.SerializeValue(ref strengthTier);
            serializer.SerializeValue(ref dexterityTier);
            serializer.SerializeValue(ref intelligenceTier);

            serializer.SerializeValue(ref strengthXpForNextTier);
            serializer.SerializeValue(ref dexterityXpForNextTier);
            serializer.SerializeValue(ref intelligenceXpForNextTier);

            serializer.SerializeValue(ref strengthTotalXp);
            serializer.SerializeValue(ref dexterityTotalXp);
            serializer.SerializeValue(ref intelligenceTotalXp);

            serializer.SerializeValue(ref effectiveStrength);
            serializer.SerializeValue(ref effectiveDexterity);
            serializer.SerializeValue(ref effectiveIntelligence);

            serializer.SerializeValue(ref currentHp);
            serializer.SerializeValue(ref maxHp);
        }

        public bool Equals(StatsSnapshotDto other)
        {
            return strength == other.strength
                && dexterity == other.dexterity
                && intelligence == other.intelligence
                && strengthTier == other.strengthTier
                && dexterityTier == other.dexterityTier
                && intelligenceTier == other.intelligenceTier
                && strengthXpForNextTier == other.strengthXpForNextTier
                && dexterityXpForNextTier == other.dexterityXpForNextTier
                && intelligenceXpForNextTier == other.intelligenceXpForNextTier
                && strengthTotalXp == other.strengthTotalXp
                && dexterityTotalXp == other.dexterityTotalXp
                && intelligenceTotalXp == other.intelligenceTotalXp
                && currentHp == other.currentHp
                && maxHp == other.maxHp;
        }

        public override bool Equals(object obj) => obj is StatsSnapshotDto o && Equals(o);

        public override int GetHashCode()
        {
            // HashCode.Combine принимает максимум 8 аргументов. Делаем ручной mix:
            // unchecked — арифметика wrap'ится, NaN значения корректно комбинируются.
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + strength.GetHashCode();
                hash = hash * 31 + dexterity.GetHashCode();
                hash = hash * 31 + intelligence.GetHashCode();
                hash = hash * 31 + strengthTier;
                hash = hash * 31 + dexterityTier;
                hash = hash * 31 + intelligenceTier;
                hash = hash * 31 + strengthXpForNextTier.GetHashCode();
                hash = hash * 31 + dexterityXpForNextTier.GetHashCode();
                hash = hash * 31 + intelligenceXpForNextTier.GetHashCode();
                hash = hash * 31 + strengthTotalXp.GetHashCode();
                hash = hash * 31 + dexterityTotalXp.GetHashCode();
                hash = hash * 31 + intelligenceTotalXp.GetHashCode();
                hash = hash * 31 + currentHp;
                hash = hash * 31 + maxHp;
                return hash;
            }
        }

        public static bool operator ==(StatsSnapshotDto a, StatsSnapshotDto b) => a.Equals(b);
        public static bool operator !=(StatsSnapshotDto a, StatsSnapshotDto b) => !a.Equals(b);
    }
}
