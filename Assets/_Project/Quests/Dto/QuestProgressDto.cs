// T-Q07: QuestProgressDto — single quest progress entry.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.6.

using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Quests.Dto
{
    /// <summary>Per-quest progress: id, state, current stage, objective progress.</summary>
    public struct QuestProgressDto : INetworkSerializable
    {
        public string questId;
        public string displayName;
        public byte state;            // QuestState enum
        public string currentStageId;
        public bool isTracked;        // quest marked as tracked в UI
        public ObjectiveProgressDto[] objectives;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref questId);
            s.SerializeValue(ref displayName);
            s.SerializeValue(ref state);
            s.SerializeValue(ref currentStageId);
            s.SerializeValue(ref isTracked);
            SerializeObjArray(ref objectives, s);
        }

        private static void SerializeObjArray<T>(ref ObjectiveProgressDto[] arr, BufferSerializer<T> s) where T : IReaderWriter
        {
            int len = arr?.Length ?? 0;
            s.SerializeValue(ref len);
            if (s.IsReader) arr = len > 0 ? new ObjectiveProgressDto[len] : null;
            for (int i = 0; i < len; i++)
            {
                var item = arr != null && i < arr.Length ? arr[i] : default;
                item.NetworkSerialize(s);
                if (arr != null) arr[i] = item;
            }
        }
    }

    /// <summary>Single objective progress: id, description, counter, required quantity.</summary>
    public struct ObjectiveProgressDto : INetworkSerializable
    {
        public string objectiveId;
        public string description;
        public bool completed;
        public int currentValue;     // T-Q21: HaveItem qty / ReachLocation distance / ReputationAtLeast value
        public int requiredQuantity; // T-Q21: 1 если не указано (1 медная руда, 1 NPC talk)

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref objectiveId);
            s.SerializeValue(ref description);
            s.SerializeValue(ref completed);
            s.SerializeValue(ref currentValue);
            s.SerializeValue(ref requiredQuantity);
        }
    }
}
