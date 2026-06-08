// T-Q07: QuestResultDto + ReputationResultDto — server → client operation results.
// Pattern: ContractResultDto (Trade). Lightweight: code + optional message.

using Unity.Netcode;

namespace ProjectC.Quests.Dto
{
    public enum QuestResultCode : byte
    {
        Ok = 0,
        NotFound = 1,
        InvalidState = 2,
        PrerequisitesNotMet = 3,
        InventoryFull = 4,
        RateLimit = 5,
        InternalError = 6,
        Discovered = 7,        // event-driven quest auto-discovered
    }

    public struct QuestResultDto : INetworkSerializable
    {
        public byte code;            // QuestResultCode
        public string questId;        // quest this result refers to
        public string message;        // human-readable (RU), may be empty

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref code);
            s.SerializeValue(ref questId);
            s.SerializeValue(ref message);
        }
    }

    public enum ReputationResultCode : byte
    {
        Ok = 0,
        NotFound = 1,
        RateLimit = 2,
        InternalError = 3,
    }

    public struct ReputationResultDto : INetworkSerializable
    {
        public byte code;
        public byte faction;
        public int newValue;
        public int delta;
        public string message;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref code);
            s.SerializeValue(ref faction);
            s.SerializeValue(ref newValue);
            s.SerializeValue(ref delta);
            s.SerializeValue(ref message);
        }
    }
}
