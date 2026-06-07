// T-Q01: NpcAttitude struct — per-player, per-NPC personal relationship value.
// Companion to per-faction reputation. See docs/NPC_quests/09_OPEN_QUESTIONS.md §G.
//
// Range: -100 (hostile) .. +200 (revered). Default = current factionRep for the
// NPC's faction. Drift is independent of factionRep once relationship is established.

using System;

namespace ProjectC.Factions
{
    /// <summary>
    /// Personal relationship between a single player and a single NPC.
    /// Distinct from <c>FactionReputation</c> — NPC can befriend or hate you
    /// independently of how their faction as a whole feels about you.
    /// </summary>
    /// <remarks>
    /// v1 scope: basic int value bound to an NPC id.
    /// Cross-faction influence (improving with NPC X hurting factionRep[Y]) is
    /// configured via <c>NpcDefinition.attitudeLinks[]</c> and applied by
    /// <c>QuestWorld.ModifyNpcAttitude</c> in T-Q13. MVA: store is live,
    /// cross-calc is MVP-stub (full formula → v2).
    /// </remarks>
    [Serializable]
    public readonly struct NpcAttitude : IEquatable<NpcAttitude>
    {
        /// <summary>Min allowed value (hostile). Used by <c>QuestWorld.ModifyNpcAttitude</c> clamp.</summary>
        public const int MinValue = -100;

        /// <summary>Max allowed value (revered). Chosen asymmetric vs. MinValue on purpose —
        /// positive relationship can be much stronger than negative, matching GDD-23 tone.</summary>
        public const int MaxValue = 200;

        public readonly string NpcId;
        public readonly int Value;

        public NpcAttitude(string npcId, int value)
        {
            NpcId = npcId ?? string.Empty;
            // Clamp at construction so stale persisted data can't escape the range.
            Value = value < MinValue ? MinValue : (value > MaxValue ? MaxValue : value);
        }

        public bool Equals(NpcAttitude other) => NpcId == other.NpcId && Value == other.Value;

        public override bool Equals(object obj) => obj is NpcAttitude other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(NpcId, Value);

        public static bool operator ==(NpcAttitude a, NpcAttitude b) => a.Equals(b);

        public static bool operator !=(NpcAttitude a, NpcAttitude b) => !a.Equals(b);

        public override string ToString() => $"NpcAttitude({NpcId}, {Value})";
    }
}
