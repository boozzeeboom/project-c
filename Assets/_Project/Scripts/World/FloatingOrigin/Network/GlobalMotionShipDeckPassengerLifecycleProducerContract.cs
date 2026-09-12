using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionShipDeckPassengerLifecyclePhase : byte
    {
        None = 0,
        ShipRegistered = 1,
        PassengerAttached = 2,
        PassengerDetached = 3,
        ShipInvalidated = 4
    }

    /// <summary>
    /// Immutable protocol-owned receipt for a reviewed ship lifetime/passenger attachment lifecycle.
    /// It is a producer contract only: it does not inspect NGO or mutate runtime objects.
    /// </summary>
    public readonly struct GlobalMotionShipDeckPassengerLifecycleReceipt
    {
        public string ShipId { get; }
        public ulong ShipNetworkObjectId { get; }
        public ulong ShipSpawnGeneration { get; }
        public ulong ShipLifetimeGeneration { get; }
        public string PassengerId { get; }
        public ulong AttachmentGeneration { get; }
        public string DeckNavId { get; }
        public GlobalMotionShipDeckPassengerLifecyclePhase Phase { get; }
        public int Ordinal { get; }
        public ulong CoordinatorLedgerOrdinal { get; }
        public string InvalidationReason { get; }
        public bool ServerOwned { get; }
        public bool ProtocolOwned { get; }

        public GlobalMotionShipDeckPassengerLifecycleReceipt(
            ulong shipNetworkObjectId,
            ulong shipSpawnGeneration,
            string passengerId,
            ulong attachmentGeneration,
            string deckNavId,
            GlobalMotionShipDeckPassengerLifecyclePhase phase,
            int ordinal,
            bool serverOwned,
            bool protocolOwned)
        {
            ShipId = null;
            ShipNetworkObjectId = shipNetworkObjectId;
            ShipSpawnGeneration = shipSpawnGeneration;
            ShipLifetimeGeneration = shipSpawnGeneration;
            PassengerId = passengerId;
            AttachmentGeneration = attachmentGeneration;
            DeckNavId = deckNavId;
            Phase = phase;
            Ordinal = ordinal;
            CoordinatorLedgerOrdinal = 0UL;
            InvalidationReason = null;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
        }

        public GlobalMotionShipDeckPassengerLifecycleReceipt(
            string shipId,
            ulong shipNetworkObjectId,
            ulong shipLifetimeGeneration,
            string passengerId,
            ulong attachmentGeneration,
            string deckNavId,
            GlobalMotionShipDeckPassengerLifecyclePhase phase,
            ulong coordinatorLedgerOrdinal,
            string invalidationReason,
            bool serverOwned,
            bool protocolOwned)
        {
            ShipId = shipId;
            ShipNetworkObjectId = shipNetworkObjectId;
            ShipSpawnGeneration = shipLifetimeGeneration;
            ShipLifetimeGeneration = shipLifetimeGeneration;
            PassengerId = passengerId;
            AttachmentGeneration = attachmentGeneration;
            DeckNavId = deckNavId;
            Phase = phase;
            Ordinal = 0;
            CoordinatorLedgerOrdinal = coordinatorLedgerOrdinal;
            InvalidationReason = invalidationReason;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
        }
    }

    public static class GlobalMotionShipDeckPassengerLifecycleProducerContract
    {
        public static bool TryRegisterShip(
            ulong shipNetworkObjectId,
            ulong shipSpawnGeneration,
            bool serverOwned,
            bool protocolOwned,
            out GlobalMotionShipDeckPassengerLifecycleReceipt receipt,
            out string error)
        {
            receipt = default;
            if (shipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (shipSpawnGeneration == 0)
                return Reject("ship_spawn_generation_required", out error);
            if (!serverOwned)
                return Reject("server_owned_lifecycle_required", out error);
            if (!protocolOwned)
                return Reject("protocol_owned_lifecycle_required", out error);

            receipt = Create(shipNetworkObjectId, shipSpawnGeneration, null, 0, null,
                GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered, 0,
                serverOwned, protocolOwned);
            error = null;
            return true;
        }

        public static bool TryAttachPassenger(
            GlobalMotionShipDeckPassengerLifecycleReceipt current,
            string passengerId,
            string deckNavId,
            ulong attachmentGeneration,
            out GlobalMotionShipDeckPassengerLifecycleReceipt next,
            out string error)
        {
            next = default;
            if (!TryValidate(current, out error))
                return false;
            if (current.Phase != GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered &&
                current.Phase != GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached)
                return Reject("passenger_attach_phase_invalid", out error);
            if (string.IsNullOrWhiteSpace(passengerId))
                return Reject("passenger_id_required", out error);
            if (string.IsNullOrWhiteSpace(deckNavId))
                return Reject("deck_nav_id_required", out error);
            if (attachmentGeneration == 0 || attachmentGeneration <= current.AttachmentGeneration)
                return Reject("attachment_generation_not_monotonic", out error);

            next = Create(current.ShipNetworkObjectId, current.ShipSpawnGeneration,
                passengerId, attachmentGeneration, deckNavId,
                GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached,
                current.Ordinal + 1, current.ServerOwned, current.ProtocolOwned);
            error = null;
            return true;
        }

        public static bool TryDetachPassenger(
            GlobalMotionShipDeckPassengerLifecycleReceipt current,
            out GlobalMotionShipDeckPassengerLifecycleReceipt next,
            out string error)
        {
            next = default;
            if (!TryValidate(current, out error))
                return false;
            if (current.Phase != GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached)
                return Reject("passenger_detach_phase_invalid", out error);

            next = Create(current.ShipNetworkObjectId, current.ShipSpawnGeneration,
                current.PassengerId, current.AttachmentGeneration, current.DeckNavId,
                GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached,
                current.Ordinal + 1, current.ServerOwned, current.ProtocolOwned);
            error = null;
            return true;
        }

        public static bool TryInvalidateShip(
            GlobalMotionShipDeckPassengerLifecycleReceipt current,
            out GlobalMotionShipDeckPassengerLifecycleReceipt next,
            out string error)
        {
            next = default;
            if (!TryValidate(current, out error))
                return false;
            if (current.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated)
                return Reject("ship_lifecycle_already_invalidated", out error);

            next = Create(current.ShipNetworkObjectId, current.ShipSpawnGeneration,
                current.PassengerId, current.AttachmentGeneration, current.DeckNavId,
                GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated,
                current.Ordinal + 1, current.ServerOwned, current.ProtocolOwned);
            error = null;
            return true;
        }

        public static bool TryValidate(
            GlobalMotionShipDeckPassengerLifecycleReceipt receipt,
            out string error)
        {
            error = null;
            if (receipt.ShipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (receipt.ShipSpawnGeneration == 0)
                return Reject("ship_spawn_generation_required", out error);
            if (receipt.Ordinal < 0)
                return Reject("lifecycle_ordinal_invalid", out error);
            if (!receipt.ServerOwned)
                return Reject("server_owned_lifecycle_required", out error);
            if (!receipt.ProtocolOwned)
                return Reject("protocol_owned_lifecycle_required", out error);
            if (receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.None)
                return Reject("lifecycle_phase_required", out error);
            if (receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered)
            {
                if (!string.IsNullOrEmpty(receipt.PassengerId) || receipt.AttachmentGeneration != 0 ||
                    !string.IsNullOrEmpty(receipt.DeckNavId))
                    return Reject("unbound_ship_receipt_contains_passenger_state", out error);
                return true;
            }
            if (receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated &&
                string.IsNullOrEmpty(receipt.PassengerId) && receipt.AttachmentGeneration == 0 &&
                string.IsNullOrEmpty(receipt.DeckNavId))
                return true;
            if (string.IsNullOrWhiteSpace(receipt.PassengerId))
                return Reject("passenger_id_required", out error);
            if (receipt.AttachmentGeneration == 0)
                return Reject("attachment_generation_required", out error);
            if (string.IsNullOrWhiteSpace(receipt.DeckNavId))
                return Reject("deck_nav_id_required", out error);
            return true;
        }

        private static GlobalMotionShipDeckPassengerLifecycleReceipt Create(
            ulong shipNetworkObjectId,
            ulong shipSpawnGeneration,
            string passengerId,
            ulong attachmentGeneration,
            string deckNavId,
            GlobalMotionShipDeckPassengerLifecyclePhase phase,
            int ordinal,
            bool serverOwned,
            bool protocolOwned)
        {
            return new GlobalMotionShipDeckPassengerLifecycleReceipt(
                shipNetworkObjectId, shipSpawnGeneration, passengerId,
                attachmentGeneration, deckNavId, phase, ordinal,
                serverOwned, protocolOwned);
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
