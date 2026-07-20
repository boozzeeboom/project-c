// NpcWorldInspectorData — data structures for NpcWorldInspectorWindow scan cache.
// Captures all NPC-related game objects from WorldScene_* files into serializable entries.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Editor.Tools
{
    /// <summary>Category of NPC found in a scene.</summary>
    public enum NpcEntryType : byte
    {
        Unknown = 0,
        Quest,    // NpcController → NpcDefinition SO
        AI,       // NpcBrain (combat NPC)
        Spawner,  // NpcSpawner → NpcSpawnerConfig SO
        Ship      // NpcShipController → NpcShipSchedule SO
    }

    /// <summary>Serializable snapshot of one NPC game object in a scene.</summary>
    [Serializable]
    public class NpcEntry
    {
        // ── Scene identity ──
        public string sceneName;
        public string scenePath;
        public string goName;
        public string goPath;       // hierarchy path under scene root
        public Vector3 position;

        public NpcEntryType entryType;

        // ── Quest NPC (NpcController) ──
        public string questNpcId;              // from NpcDefinition.npcId
        public string questDisplayName;
        public string questFaction;            // FactionId enum name
        public string questDialogTreePath;     // Asset path to DialogTree
        public string questOffers;             // comma-separated questIds
        public string questTurnIns;            // comma-separated questIds
        public string questServices;           // NpcService flags string
        public float  questInteractionRadius;
        public string questGreetingText;
        public string questVoicePrefix;
        public string questDefinitionPath;     // Asset path to NpcDefinition SO
        public string questAttitudeLinks;      // summary: "Faction:±delta;..."
        public int    questAttitudeMin;
        public int    questAttitudeMax;

        // ── AI/Combat NPC (NpcBrain) ──
        public string aiBehaviorType;          // NpcBrain.BehaviorType enum name
        public bool   aiSocialEnabled;
        public string aiCombatDataPath;        // Asset path to NpcCombatData SO
        public string aiFaction;               // from NpcSocialBrain if present
        public string aiMarketConfigPath;      // from MarketZone if present
        public float  aiAggroRange;
        public float  aiLeashRange;
        public bool   aiHasSocialBrain;
        public string aiIdleActivity;          // NpcIdleActivity enum
        public bool   aiCanFlee;
        public string aiSkillSetPath;          // from NpcAttacker._skillSet

        // ── Spawner (NpcSpawner) ──
        public string spawnerConfigPath;       // Asset path to NpcSpawnerConfig SO
        public string spawnerPrefabName;       // npcPrefab name
        public string spawnerPrefabPath;       // Asset path to prefab
        public string spawnerBehaviorType;     // from config
        public bool   spawnerSocialEnabled;
        public string spawnerFaction;          // from config.faction
        public string spawnerSpawnMode;        // SpawnMode enum
        public int    spawnerMaxAlive;
        public int    spawnerTotalLimit;
        public float  spawnerActivationRadius;
        public string spawnerLootTablePath;    // from config.lootTable
        public string spawnerLootPrefabPath;
        public bool   spawnerAutoPopulateChunks;
        public int    spawnerPatrolMarkerCount;
        public string spawnerVisualConfigPath; // from config.visualConfig

        // ── Ship (NpcShipController) ──
        public string shipScheduleId;
        public string shipScheduleDisplayName;
        public string shipSchedulePath;        // Asset path to NpcShipSchedule SO
        public string shipScheduleType;        // ScheduleType enum
        public int    shipRouteCount;
        public string shipRouteSummary;        // "from→to, from→to..."
        public string shipCargoSummary;        // "N items" or "—"
        public int    shipCargoItemCount;
        public bool   shipHasCargo;
        public string shipFlightClass;
        public float  shipNpcThrustMult;
        public float  shipNpcYawMult;
        public bool   shipOverrideSpeeds;
        public string shipLocationId;          // current dock station locationId if docked

        // ── Helpers ──
        public string TypeLabel => entryType switch
        {
            NpcEntryType.Quest   => "🎯 Quest NPC",
            NpcEntryType.AI      => "⚔ Combat NPC",
            NpcEntryType.Spawner => "🔄 Spawner",
            NpcEntryType.Ship    => "🚢 Ship",
            _ => "?"
        };

        public string FactionLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(questFaction) && questFaction != "None")
                    return questFaction;
                if (!string.IsNullOrEmpty(aiFaction) && aiFaction != "None")
                    return aiFaction;
                if (!string.IsNullOrEmpty(spawnerFaction) && spawnerFaction != "None")
                    return spawnerFaction;
                return "—";
            }
        }

        public string BehaviorLabel => entryType switch
        {
            NpcEntryType.AI      => aiBehaviorType ?? "—",
            NpcEntryType.Spawner => spawnerBehaviorType ?? "—",
            _ => "—"
        };

        public string QuestSummary
        {
            get
            {
                if (entryType == NpcEntryType.Quest)
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(questOffers)) parts.Add($"offers:[{questOffers}]");
                    if (!string.IsNullOrEmpty(questTurnIns)) parts.Add($"turnIn:[{questTurnIns}]");
                    return parts.Count > 0 ? string.Join(" ", parts) : "—";
                }
                return "—";
            }
        }

        public string ConnectionLabel
        {
            get
            {
                return entryType switch
                {
                    NpcEntryType.AI when !string.IsNullOrEmpty(aiMarketConfigPath)
                        => $"Market: {System.IO.Path.GetFileNameWithoutExtension(aiMarketConfigPath)}",
                    NpcEntryType.Ship when !string.IsNullOrEmpty(shipScheduleId)
                        => $"Schedule: {shipScheduleId}",
                    NpcEntryType.Spawner when !string.IsNullOrEmpty(spawnerPrefabName)
                        => $"Prefab: {spawnerPrefabName}",
                    NpcEntryType.Quest when !string.IsNullOrEmpty(questDialogTreePath)
                        => $"Dialog: {System.IO.Path.GetFileNameWithoutExtension(questDialogTreePath)}",
                    _ => "—"
                };
            }
        }
    }

    /// <summary>Full result of one scan across all WorldScene_* files.</summary>
    [Serializable]
    public class SceneNpcScanResult
    {
        public List<NpcEntry> entries = new List<NpcEntry>();
        public int sceneCount;
        public DateTime scanTime;

        public int QuestCount   => entries.FindAll(e => e.entryType == NpcEntryType.Quest).Count;
        public int AICount      => entries.FindAll(e => e.entryType == NpcEntryType.AI).Count;
        public int SpawnerCount => entries.FindAll(e => e.entryType == NpcEntryType.Spawner).Count;
        public int ShipCount    => entries.FindAll(e => e.entryType == NpcEntryType.Ship).Count;

        public HashSet<string> ReferencedNpcIds
        {
            get
            {
                var set = new HashSet<string>();
                foreach (var e in entries)
                    if (!string.IsNullOrEmpty(e.questNpcId))
                        set.Add(e.questNpcId);
                return set;
            }
        }
    }

    /// <summary>Snapshot of a FactionDefinition asset for the Factions tab.</summary>
    [Serializable]
    public class FactionEntry
    {
        public string assetPath;
        public string assetName;           // e.g. "GuildOfSecrets"
        public FactionId factionId;
        public string displayName;
        public Color color;
        public string loreDescription;
        public FactionAttitude defaultAttitude;
        public FactionRelation defaultCombatRelation;
        public ReputationTier[] reputationThresholds;
        public List<FactionCombatRelationEntry> combatRelations;
        public int npcCount;              // how many scene NPCs reference this faction
    }

    /// <summary>Serializable mirror of FactionCombatRelation (since struct can't be directly stored).</summary>
    [Serializable]
    public class FactionCombatRelationEntry
    {
        public FactionId targetFaction;
        public FactionRelation relation;
    }

    /// <summary>Result of scanning all FactionDefinition assets.</summary>
    [Serializable]
    public class FactionScanResult
    {
        public List<FactionEntry> factions = new List<FactionEntry>();
        public DateTime scanTime;
        public int factionCount => factions.Count;
    }
}
#endif
