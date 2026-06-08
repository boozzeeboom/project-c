// T-Q07: QuestSnapshotDto — full quest state snapshot for one player.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.6 (DTOs), pattern: ContractSnapshotDto (Trade).
//
// Server → Client. Sent by QuestServer in response to RequestRefreshQuestsRpc
// or after every state transition (T-Q15+).

using Unity.Netcode;

namespace ProjectC.Quests.Dto
{
    /// <summary>Full quest state snapshot: all known quests для player + active + completed.</summary>
    public struct QuestSnapshotDto : INetworkSerializable
    {
        /// <summary>All quest instances (Discovered + Offered + Active + Completed + Failed + TurnedIn).</summary>
        public QuestProgressDto[] quests;

        /// <summary>Discovered-only (для "new quest" notifications). T-Q15.</summary>
        public string[] newlyDiscoveredQuestIds;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            SerializeArray(ref quests, s);
            SerializeStringArray(ref newlyDiscoveredQuestIds, s);
        }

        private static void SerializeArray<T>(ref QuestProgressDto[] arr, BufferSerializer<T> s) where T : IReaderWriter
        {
            int len = arr?.Length ?? 0;
            s.SerializeValue(ref len);
            if (s.IsReader) arr = len > 0 ? new QuestProgressDto[len] : null;
            for (int i = 0; i < len; i++)
            {
                var item = arr != null && i < arr.Length ? arr[i] : default;
                item.NetworkSerialize(s);
                if (arr != null) arr[i] = item;
            }
        }

        private static void SerializeStringArray<T>(ref string[] arr, BufferSerializer<T> s) where T : IReaderWriter
        {
            int len = arr?.Length ?? 0;
            s.SerializeValue(ref len);
            if (s.IsReader) arr = len > 0 ? new string[len] : null;
            for (int i = 0; i < len; i++)
            {
                var v = arr != null ? arr[i] : "";
                s.SerializeValue(ref v);
                if (arr != null) arr[i] = v;
            }
        }
    }
}
