using System;
using UnityEngine;

namespace ProjectC.World.Npc
{
    /// <summary>
    /// Factions available for NPCs in Project C. (v1 — to be deleted in T-Q19.)
    /// </summary>
    /// <remarks>
    /// Deprecated: use <see cref="ProjectC.Factions.FactionId"/> instead.
    /// Values are kept numerically identical so any existing serialized data
    /// (prefab/scene/SO references) keeps working until v1 is deleted in T-Q19.
    /// New code MUST reference <c>ProjectC.Factions.FactionId</c>.
    /// </remarks>
    [Obsolete("Use ProjectC.Factions.FactionId (T-Q01). NpcFaction is the v1 alias kept for backward-compat only and will be removed in T-Q19.")]
    public enum NpcFaction
    {
        None = 0,
        GuildOfThoughts = 1,      // Gildiya Mysley - scholarly, artifacts
        GuildOfCreation = 2,      // Gildiya Sozidaniya - engineering, modules
        GuildOfStrength = 3,      // Gildiya Sily - combat, security
        GuildOfSecrets = 4,       // Gildiya Tayn - exploration, reconnaissance
        GuildOfSuccess = 5,       // Gildiya Uspekha - trading, commerce
        Underground = 6,          // Podpolye - smugglers, contraband
        Resistance = 7,           // Soprotivleniye - freedom fighters
        FreeTraders = 8,          // Svobodnye Torgovtsy - neutral merchants
        SOL_Patrol = 9,           // SOL Patrol - hostile authority
        Pirates = 10,             // Pirates - hostile raiders
        Neutral = 11              // Neutral - unaffiliated
    }

    /// <summary>
    /// Type of dialogue node.
    /// </summary>
    public enum DialogueNodeType
    {
        Text,            // Simple text display
        QuestOffer,      // Offers a quest/contract
        QuestComplete,   // Completes an active quest
        Trade,           // Opens trade interface
        Service,         // Provides a service (repair, refuel)
        Goodbye          // Ends conversation
    }

    /// <summary>
    /// Represents a single dialogue option/choice.
    /// </summary>
    [Serializable]
    public class DialogueOption
    {
        [Tooltip("Display text for this option")]
        public string text = "Continue";

        [Tooltip("ID of the next dialogue node")]
        public string nextNodeId = "";

        [Tooltip("Required item ID to select this option (optional)")]
        public string requiredItemId = "";

        [Tooltip("Required reputation level with this NPC's faction")]
        public int requiredReputation = 0;

        [Tooltip("Item given to player when selecting this option")]
        public string rewardItemId = "";

        [Tooltip("Contract ID associated with this option (for quest offers)")]
        public string contractId = "";

        [Tooltip("Sound cue to play when this option is selected")]
        public string soundCue = "";

        /// <summary>
        /// Check if this option is available to the player.
        /// </summary>
        public bool IsAvailable()
        {
            // TODO: Check inventory for requiredItemId
            // TODO: Check reputation
            
            if (!string.IsNullOrEmpty(requiredItemId))
            {
                // Placeholder: In real implementation, check player inventory
                return true;
            }
            
            if (requiredReputation > 0)
            {
                // Placeholder: In real implementation, check faction reputation
                return true;
            }
            
            return true;
        }
    }

    /// <summary>
    /// Represents a single node in a dialogue tree.
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        [Tooltip("Unique identifier for this node")]
        public string nodeId = "";

        [Tooltip("Type of dialogue node")]
        public DialogueNodeType nodeType = DialogueNodeType.Text;

        [Header("Content")]
        [Tooltip("Main dialogue text (supports basic formatting)")]
        [TextArea(2, 5)]
        public string text = "";

        [Tooltip("NPC portrait emotion (for future use)")]
        public string portraitEmotion = "Neutral";

        [Header("Options")]
        [Tooltip("Available dialogue choices")]
        public DialogueOption[] options = new DialogueOption[0];

        [Header("Quest/Contract")]
        [Tooltip("Is this a quest-giving node?")]
        public bool isQuestGiver = false;

        [Tooltip("Contract ID if this node offers a contract")]
        public string contractId = "";

        [Header("Actions")]
        [Tooltip("Trigger an event when this node is shown")]
        public string triggerEvent = "";

        [Tooltip("Give this item ID when showing this node")]
        public string giveItemId = "";

        [Tooltip("Add this reputation amount to faction")]
        public int reputationGain = 0;
    }

    /// <summary>
    /// ScriptableObject containing NPC data.
    /// Create via: Right-click -> Create -> Project C -> NPC Data
    /// </summary>
    [CreateAssetMenu(fileName = "NewNpc", menuName = "Project C/NPC Data", order = 100)]
    public class NpcData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Unique identifier for this NPC")]
        public string npcId = "";

        [Tooltip("Display name shown to player")]
        public string displayName = "Unknown NPC";

        [Tooltip("NPC's faction")]
        public NpcFaction faction = NpcFaction.Neutral;

        [Tooltip("NPC's title/role")]
        public string title = "";

        [Tooltip("NPC's level (for combat scaling)")]
        public int level = 1;

        [Header("Visuals")]
        [Tooltip("NPC portrait for dialogue UI")]
        public Sprite portrait;

        [Tooltip("NPC prefab (if different from default)")]
        public GameObject prefab;

        [Header("Dialogue")]
        [Tooltip("Root dialogue node ID")]
        public string rootNodeId = "start";

        [Tooltip("All dialogue nodes for this NPC")]
        public DialogueNode[] dialogues = new DialogueNode[0];

        [Header("Services")]
        [Tooltip("Can this NPC trade items?")]
        public bool canTrade = false;

        [Tooltip("Can this NPC repair ships?")]
        public bool canRepair = false;

        [Tooltip("Can this NPC refuel ships?")]
        public bool canRefuel = false;

        [Tooltip("Can this NPC provide quests/contracts?")]
        public bool canGiveQuests = true;

        [Header("Settings")]
        [Tooltip("Interaction radius for this NPC")]
        public float interactionRadius = 3f;

        [Tooltip("Should this NPC be synchronized over network?")]
        public bool isNetworked = false;

        [Tooltip("Show greeting when player approaches")]
        public bool showGreeting = true;

        [Tooltip("Greeting text when player is nearby")]
        [TextArea(1, 2)]
        public string greetingText = "Greetings, traveler.";

        [Header("Audio")]
        [Tooltip("Voice line prefix for this NPC")]
        public string voicePrefix = "";

        /// <summary>
        /// Find a dialogue node by its ID.
        /// </summary>
        public DialogueNode GetNode(string nodeId)
        {
            for (int i = 0; i < dialogues.Length; i++)
            {
                if (dialogues[i].nodeId == nodeId)
                {
                    return dialogues[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Get the root dialogue node.
        /// </summary>
        public DialogueNode GetRootNode()
        {
            return GetNode(rootNodeId);
        }

        /// <summary>
        /// Get available options for a dialogue node based on player's state.
        /// </summary>
        public DialogueOption[] GetAvailableOptions(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return new DialogueOption[0];

            var available = new System.Collections.Generic.List<DialogueOption>();
            for (int i = 0; i < node.options.Length; i++)
            {
                if (node.options[i].IsAvailable())
                {
                    available.Add(node.options[i]);
                }
            }
            return available.ToArray();
        }
    }
}
