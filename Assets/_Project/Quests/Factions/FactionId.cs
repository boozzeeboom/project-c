// T-Q01: FactionId promotion from ProjectC.World.Npc.NpcFaction.
// Canonical faction enum for the whole project. The 12 lore values are preserved
// exactly (numeric values match NpcFaction so existing serialized data stays valid).
//
// See: docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.1
//      docs/NPC_quests/09_OPEN_QUESTIONS.md §A3 (Both: per-faction + per-NPC)

namespace ProjectC.Factions
{
    /// <summary>
    /// Faction identifier for Project C. Used by <see cref="ProjectC.Reputation"/>
    /// (per-player reputation), <see cref="ProjectC.Dialogue.NpcDefinition"/>
    /// (NPC ↔ faction binding), and <see cref="ProjectC.Quests.QuestDefinition"/>
    /// (faction-gated quest prerequisites).
    /// </summary>
    public enum FactionId
    {
        None = 0,
        GuildOfThoughts = 1,   // Gildiya Mysley - scholarly, artifacts
        GuildOfCreation = 2,   // Gildiya Sozidaniya - engineering, modules
        GuildOfStrength = 3,   // Gildiya Sily - combat, security
        GuildOfSecrets = 4,    // Gildiya Tayn - exploration, reconnaissance
        GuildOfSuccess = 5,    // Gildiya Uspekha - trading, commerce
        Underground = 6,       // Podpolye - smugglers, contraband
        Resistance = 7,        // Soprotivleniye - freedom fighters
        FreeTraders = 8,       // Svobodnye Torgovtsy - neutral merchants
        SOL_Patrol = 9,        // SOL Patrol - hostile authority
        Pirates = 10,          // Pirates - hostile raiders
        Neutral = 11           // Neutral - unaffiliated
    }
}
