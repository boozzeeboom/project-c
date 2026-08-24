// T-Q01: FactionId promotion from ProjectC.World.Npc.NpcFaction.
// Legacy compatibility enum for the whole project. Numeric values are preserved
// exactly so existing serialized data and saves stay valid. New factions must not
// be added here; create a FactionDefinition asset with a unique factionKey/wireId.
//
// See: docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.1
//      docs/NPC_quests/09_OPEN_QUESTIONS.md §A3 (Both: per-faction + per-NPC)

namespace ProjectC.Factions
{
    /// <summary>
    /// Legacy faction identifier for Project C. Used only for compatibility with
    /// old serialized data, old saves, and fallback paths. The authoritative identity
    /// of a faction is carried by its FactionDefinition asset.
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
        Neutral = 11,          // Neutral - unaffiliated
        // === T-FACTION-UNIFY: новые значения из NpcFaction ===
        Bandits = 12,          // было NpcFaction_bandits (factionId="bandits")
        Cultists = 13,         // было NpcFaction_cultists (factionId="cultists")
        Guards = 14,           // было NpcFaction_guards (factionId="guards")
        Villagers = 15         // было NpcFaction_villagers (factionId="villagers")
    }
}
