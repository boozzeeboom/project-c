using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06CS: explicit server/protocol-owned ingress bridge for the ShipDeck lifecycle coordinator.
    /// It owns exactly one coordinator instance and accepts only caller-supplied protocol identities,
    /// supplemental transport identity and explicit ownership authorization. It never discovers or
    /// infers lifecycle facts from Unity/NGO objects, callback order or local host state.
    /// </summary>
    public sealed class GlobalMotionShipDeckPassengerLifecycleProducerBridge : IGlobalMotionShipDeckPassengerGenerationSource
    {
        private readonly GlobalMotionShipDeckPassengerLifecycleCoordinator _coordinator =
            new GlobalMotionShipDeckPassengerLifecycleCoordinator();

        /// <summary>
        /// Accepts an explicit server-authorized registration for a stable protocol ShipId.
        /// The coordinator, not the caller, allocates the ship lifetime generation and ledger ordinal.
        /// </summary>
        public bool TryRegisterShipServerAuthorized(
            string shipId,
            ulong shipNetworkObjectId,
            bool serverAuthorized,
            bool protocolOwned,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping,
            out string error)
        {
            return TryAccept(
                GlobalMotionShipDeckPassengerLifecycleFact.CreateShipRegistered(
                    shipId, shipNetworkObjectId, serverAuthorized, protocolOwned),
                out mapping,
                out error);
        }

        /// <summary>
        /// Accepts an explicit completed passenger attachment. Expected lineage is asserted by the
        /// caller; the coordinator allocates the new attachment generation and ledger ordinal.
        /// </summary>
        public bool TryAttachPassengerServerAuthorized(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string passengerId,
            ulong previousAttachmentGeneration,
            string deckNavId,
            bool serverAuthorized,
            bool protocolOwned,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping,
            out string error)
        {
            return TryAccept(
                GlobalMotionShipDeckPassengerLifecycleFact.CreatePassengerAttached(
                    shipId,
                    shipNetworkObjectId,
                    expectedShipLifetimeGeneration,
                    passengerId,
                    previousAttachmentGeneration,
                    deckNavId,
                    serverAuthorized,
                    protocolOwned),
                out mapping,
                out error);
        }

        /// <summary>
        /// Accepts an explicit completed detach for the exact active attachment generation.
        /// No new attachment generation is synthesized for a detach.
        /// </summary>
        public bool TryDetachPassengerServerAuthorized(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string passengerId,
            ulong attachmentGeneration,
            string deckNavId,
            bool serverAuthorized,
            bool protocolOwned,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping,
            out string error)
        {
            return TryAccept(
                GlobalMotionShipDeckPassengerLifecycleFact.CreatePassengerDetached(
                    shipId,
                    shipNetworkObjectId,
                    expectedShipLifetimeGeneration,
                    passengerId,
                    attachmentGeneration,
                    deckNavId,
                    serverAuthorized,
                    protocolOwned),
                out mapping,
                out error);
        }

        /// <summary>
        /// Accepts the explicit terminal invalidation for the matching ship lifetime.
        /// The coordinator closes active passenger epochs and rejects all later facts.
        /// </summary>
        public bool TryInvalidateShipServerAuthorized(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string invalidationReason,
            bool serverAuthorized,
            bool protocolOwned,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping,
            out string error)
        {
            return TryAccept(
                GlobalMotionShipDeckPassengerLifecycleFact.CreateShipInvalidated(
                    shipId,
                    shipNetworkObjectId,
                    expectedShipLifetimeGeneration,
                    invalidationReason,
                    serverAuthorized,
                    protocolOwned),
                out mapping,
                out error);
        }

        /// <summary>
        /// Exposes only the coordinator's accepted active generation view to downstream consumers.
        /// No Unity/NGO state is consulted.
        /// </summary>
        public bool TryGetGeneration(
            string passengerId,
            out GlobalMotionShipDeckPassengerGeneration generation,
            out string error)
        {
            return _coordinator.TryGetGeneration(passengerId, out generation, out error);
        }

        private bool TryAccept(
            GlobalMotionShipDeckPassengerLifecycleFact fact,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping,
            out string error)
        {
            mapping = default;
            if (!GlobalMotionShipDeckPassengerLifecycleCoordinator.TryValidateFact(fact, out error))
                return false;
            if (!_coordinator.TryAccept(fact, out var receipt, out error))
                return false;
            if (!GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryMap(
                receipt, out mapping, out error))
            {
                mapping = default;
                error = "accepted_coordinator_receipt_mapping_invalid:" + error;
                return false;
            }
            return true;
        }
    }
}
