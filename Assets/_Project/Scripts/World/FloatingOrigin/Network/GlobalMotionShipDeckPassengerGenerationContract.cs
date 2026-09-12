using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Reviewed server/protocol provenance required to bind a passenger snapshot to one ship lifetime
    /// and one explicit attachment generation. No runtime producer is implied by this value object.
    /// </summary>
    public readonly struct GlobalMotionShipDeckPassengerGeneration
    {
        public string PassengerId { get; }
        public ulong ShipNetworkObjectId { get; }
        public ulong ShipSpawnGeneration { get; }
        public ulong AttachmentGeneration { get; }
        public string DeckNavId { get; }
        public bool ServerOwned { get; }
        public bool ProtocolOwned { get; }

        public GlobalMotionShipDeckPassengerGeneration(
            string passengerId,
            ulong shipNetworkObjectId,
            ulong shipSpawnGeneration,
            ulong attachmentGeneration,
            string deckNavId,
            bool serverOwned,
            bool protocolOwned)
        {
            PassengerId = passengerId;
            ShipNetworkObjectId = shipNetworkObjectId;
            ShipSpawnGeneration = shipSpawnGeneration;
            AttachmentGeneration = attachmentGeneration;
            DeckNavId = deckNavId;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
        }
    }

    /// <summary>
    /// Explicit source seam for a future server-owned attachment/lifetime ledger.
    /// Automatic discovery and inference from NetworkObject.IsSpawned are prohibited.
    /// </summary>
    public interface IGlobalMotionShipDeckPassengerGenerationSource
    {
        bool TryGetGeneration(
            string passengerId,
            out GlobalMotionShipDeckPassengerGeneration generation,
            out string error);
    }

    public static class GlobalMotionShipDeckPassengerGenerationContract
    {
        public static bool TryValidate(
            GlobalMotionShipDeckPassengerGeneration generation,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(generation.PassengerId))
                return Reject("passenger_id_required", out error);
            if (generation.ShipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (generation.ShipSpawnGeneration == 0)
                return Reject("ship_spawn_generation_required", out error);
            if (generation.AttachmentGeneration == 0)
                return Reject("attachment_generation_required", out error);
            if (string.IsNullOrWhiteSpace(generation.DeckNavId))
                return Reject("deck_nav_id_required", out error);
            if (!generation.ServerOwned)
                return Reject("server_owned_generation_required", out error);
            if (!generation.ProtocolOwned)
                return Reject("protocol_owned_generation_required", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
