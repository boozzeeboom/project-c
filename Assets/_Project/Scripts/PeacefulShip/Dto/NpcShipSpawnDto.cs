// T-NS07: NpcShipSpawnDto — DTO для инициализации NPC-корабля на клиенте.
// Pattern: DockingDto (Docking/Dto/DockingDto.cs) — INetworkSerializable struct.

using Unity.Netcode;

namespace ProjectC.PeacefulShip.Dto
{
    /// <summary>
    /// DTO отправляемая клиенту при OnNetworkSpawn NPC-корабля.
    /// Позволяет NpcShipClientState построить список видимых NPC без прямого доступа к NetworkBehaviour.
    /// </summary>
    public struct NpcShipSpawnDto : INetworkSerializable
    {
        public ulong npcInstanceId;
        public ulong shipNetworkObjectId;
        public string displayName;
        public string scheduleId;
        public byte statusRaw;         // NpcShipStatus (byte для NetworkSerialize)
        public int capacitySlots;      // v2 hook (M1: 0)
        public float capacityWeight;   // v2 hook (M1: 0)

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref npcInstanceId);
            serializer.SerializeValue(ref shipNetworkObjectId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref scheduleId);
            serializer.SerializeValue(ref statusRaw);
            serializer.SerializeValue(ref capacitySlots);
            serializer.SerializeValue(ref capacityWeight);
        }
    }
}