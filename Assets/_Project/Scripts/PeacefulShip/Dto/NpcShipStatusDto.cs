// T-NS07: NpcShipStatusDto — DTO для push-уведомления об изменении статуса NPC-корабля.
// Pattern: DockingDto (Docking/Dto/DockingDto.cs) — INetworkSerializable struct.

using Unity.Netcode;

namespace ProjectC.PeacefulShip.Dto
{
    /// <summary>
    /// DTO отправляемая клиенту при изменении NpcShipStatus.
    /// Используется NpcShipClientState для обновления UI/HUD.
    /// </summary>
    public struct NpcShipStatusDto : INetworkSerializable
    {
        public ulong npcInstanceId;
        public byte statusRaw;             // NpcShipStatus (byte для NetworkSerialize)
        public string currentStationId;
        public float timestamp;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref npcInstanceId);
            serializer.SerializeValue(ref statusRaw);
            serializer.SerializeValue(ref currentStationId);
            serializer.SerializeValue(ref timestamp);
        }
    }
}