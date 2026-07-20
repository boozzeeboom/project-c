// T-Q18: POCO для JSON-сериализации quest state. Single file per player.
// All fields сериализуются через UnityEngine.JsonUtility (см. JsonQuestStateRepository).

using System;
using System.Collections.Generic;

namespace ProjectC.Quests.Persistence
{
    /// <summary>Per-quest instance state (subset of QuestInstance, save-friendly).</summary>
    [Serializable]
    public class QuestSaveEntry
    {
        public string questId = "";
        public byte state = 0; // QuestState enum
        public string currentStageId = "";
        public long acceptedAtUnix = 0;
        public long completedAtUnix = 0;
        public bool isTracked = false;
        public List<ObjectiveSaveEntry> objectiveProgress = new List<ObjectiveSaveEntry>();
    }

    [Serializable]
    public class ObjectiveSaveEntry
    {
        public string objectiveId = "";
        public int currentCount = 0;
        public bool completed = false;
    }

    [Serializable]
    public class FactionRepSaveEntry
    {
        public int factionId = 0; // FactionId enum
        public int value = 0;
    }

    [Serializable]
    public class NpcAttitudeSaveEntry
    {
        public string npcId = "";
        public int value = 0;
    }

    [Serializable]
    public class StringSetSaveEntry
    {
        public string setName = ""; // "eventsOccurred" / "contractsCompleted" / "contractsAccepted" / "npcTalkedTo" / "worldFlags"
        public List<string> values = new List<string>();
    }

    /// <summary>Full player state snapshot. Saved as one JSON file per clientId.</summary>
    [Serializable]
    public class QuestSaveData
    {
        public int version = 1;
        public long savedAtUnix = 0;

        public List<QuestSaveEntry> quests = new List<QuestSaveEntry>();
        public List<FactionRepSaveEntry> reputation = new List<FactionRepSaveEntry>();
        public List<NpcAttitudeSaveEntry> npcAttitude = new List<NpcAttitudeSaveEntry>();

        // Sets: eventsOccurred, contractsCompleted, contractsAccepted, npcTalkedTo, worldFlags.
        public List<StringSetSaveEntry> stringSets = new List<StringSetSaveEntry>();

        // === T-KNOW: Knowledge system ===
        public List<int> knownFactions = new List<int>();   // FactionId как int для JsonUtility
        public List<string> knownNpcs = new List<string>();
    }
}
