// T-Q07: ReputationSnapshotDto + NpcAttitudeSnapshotDto + ReputationEntryDto + NpcAttitudeEntryDto.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.6.

using Unity.Netcode;

namespace ProjectC.Quests.Dto
{
    /// <summary>Full reputation state для player: все FactionId → value.</summary>
    public struct ReputationSnapshotDto : INetworkSerializable
    {
        public ReputationEntryDto[] entries;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            int len = entries?.Length ?? 0;
            s.SerializeValue(ref len);
            if (s.IsReader) entries = len > 0 ? new ReputationEntryDto[len] : null;
            for (int i = 0; i < len; i++)
            {
                var e = entries != null ? entries[i] : default;
                e.NetworkSerialize(s);
                if (entries != null) entries[i] = e;
            }
        }
    }

    public struct ReputationEntryDto : INetworkSerializable
    {
        public byte faction;         // FactionId (по T-Q01 sparse-friendly: 0..11)
        public int value;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref faction);
            s.SerializeValue(ref value);
        }
    }

    public struct NpcAttitudeSnapshotDto : INetworkSerializable
    {
        public NpcAttitudeEntryDto[] entries;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            int len = entries?.Length ?? 0;
            s.SerializeValue(ref len);
            if (s.IsReader) entries = len > 0 ? new NpcAttitudeEntryDto[len] : null;
            for (int i = 0; i < len; i++)
            {
                var e = entries != null ? entries[i] : default;
                e.NetworkSerialize(s);
                if (entries != null) entries[i] = e;
            }
        }
    }

    public struct NpcAttitudeEntryDto : INetworkSerializable
    {
        public string npcId;
        public int value;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref npcId);
            s.SerializeValue(ref value);
        }
    }
}
