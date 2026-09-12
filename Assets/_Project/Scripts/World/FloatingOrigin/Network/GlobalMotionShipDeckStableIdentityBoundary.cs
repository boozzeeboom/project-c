using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionShipDeckStableIdentitySource : byte
    {
        None = 0,
        ShipControllerPersistentId = 1,
        ShipCrewManifestId = 2
    }

    /// <summary>
    /// T-FO06CV: reviewed stable identity boundary for ShipDeck lifecycle ingress.
    /// The identity is caller-supplied and must remain distinct from NetworkObjectId.
    /// </summary>
    public readonly struct GlobalMotionShipDeckStableIdentityReceipt
    {
        public string StableShipId { get; }
        public ulong ShipNetworkObjectId { get; }
        public GlobalMotionShipDeckStableIdentitySource Source { get; }
        public bool OwnerReviewed { get; }
        public bool ServerOwned { get; }
        public bool ProtocolOwned { get; }
        public bool TerminalInvalidationOwnerReviewed { get; }

        public GlobalMotionShipDeckStableIdentityReceipt(
            string stableShipId,
            ulong shipNetworkObjectId,
            GlobalMotionShipDeckStableIdentitySource source,
            bool ownerReviewed,
            bool serverOwned,
            bool protocolOwned,
            bool terminalInvalidationOwnerReviewed)
        {
            StableShipId = stableShipId;
            ShipNetworkObjectId = shipNetworkObjectId;
            Source = source;
            OwnerReviewed = ownerReviewed;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
            TerminalInvalidationOwnerReviewed = terminalInvalidationOwnerReviewed;
        }
    }

    public static class GlobalMotionShipDeckStableIdentityBoundary
    {
        public static bool TryCreateFromShipControllerPersistentId(
            string shipPersistentId,
            ulong shipNetworkObjectId,
            bool ownerReviewed,
            bool serverOwned,
            bool protocolOwned,
            bool terminalInvalidationOwnerReviewed,
            out GlobalMotionShipDeckStableIdentityReceipt receipt,
            out string error)
        {
            receipt = default;
            if (string.IsNullOrWhiteSpace(shipPersistentId))
                return Reject("ship_persistent_id_required", out error);
            if (shipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (!ownerReviewed)
                return Reject("stable_identity_owner_review_required", out error);
            if (!serverOwned)
                return Reject("stable_identity_server_ownership_required", out error);
            if (!protocolOwned)
                return Reject("stable_identity_protocol_ownership_required", out error);
            if (!terminalInvalidationOwnerReviewed)
                return Reject("terminal_invalidation_owner_review_required", out error);

            receipt = new GlobalMotionShipDeckStableIdentityReceipt(
                shipPersistentId.Trim(),
                shipNetworkObjectId,
                GlobalMotionShipDeckStableIdentitySource.ShipControllerPersistentId,
                ownerReviewed,
                serverOwned,
                protocolOwned,
                terminalInvalidationOwnerReviewed);
            error = null;
            return true;
        }

        public static bool TryValidate(
            GlobalMotionShipDeckStableIdentityReceipt receipt,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(receipt.StableShipId))
                return Reject("ship_persistent_id_required", out error);
            if (receipt.ShipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (receipt.Source != GlobalMotionShipDeckStableIdentitySource.ShipControllerPersistentId)
                return Reject("unsupported_stable_identity_source", out error);
            if (!receipt.OwnerReviewed)
                return Reject("stable_identity_owner_review_required", out error);
            if (!receipt.ServerOwned)
                return Reject("stable_identity_server_ownership_required", out error);
            if (!receipt.ProtocolOwned)
                return Reject("stable_identity_protocol_ownership_required", out error);
            if (!receipt.TerminalInvalidationOwnerReviewed)
                return Reject("terminal_invalidation_owner_review_required", out error);
            return true;
        }

        public static bool TryValidateManifestAlignment(
            string shipPersistentId,
            string manifestShipId,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(shipPersistentId))
                return Reject("ship_persistent_id_required", out error);
            if (string.IsNullOrWhiteSpace(manifestShipId))
                return Reject("manifest_ship_id_required", out error);
            if (!string.Equals(shipPersistentId.Trim(), manifestShipId.Trim(), StringComparison.Ordinal))
                return Reject("ship_manifest_identity_mismatch", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
