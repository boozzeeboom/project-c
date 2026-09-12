using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    [Flags]
    public enum GlobalMotionShipDeckLifecycleIngressMask : byte
    {
        None = 0,
        ShipRegistered = 1 << 0,
        PassengerAttached = 1 << 1,
        PassengerDetached = 1 << 2,
        ShipInvalidated = 1 << 3
    }

    public enum GlobalMotionShipDeckLifecycleCallerRole : byte
    {
        None = 0,
        NpcShipController = 1,
        ShipCrewSpawner = 2,
        NpcBrain = 3,
        ShipDeckNav = 4
    }

    /// <summary>
    /// T-FO06CU: explicit owner-reviewed caller boundary receipt. It records which runtime seam is
    /// allowed to submit which lifecycle facts, but does not discover callers or invoke gameplay code.
    /// </summary>
    public readonly struct GlobalMotionShipDeckLifecycleCallerBindingReceipt
    {
        public GlobalMotionShipDeckLifecycleCallerRole CallerRole { get; }
        public string StableShipId { get; }
        public ulong ShipNetworkObjectId { get; }
        public GlobalMotionShipDeckLifecycleIngressMask IngressMask { get; }
        public bool SupportsTerminalInvalidation { get; }
        public bool OwnerReviewed { get; }
        public bool ServerOwned { get; }
        public bool ProtocolOwned { get; }

        public GlobalMotionShipDeckLifecycleCallerBindingReceipt(
            GlobalMotionShipDeckLifecycleCallerRole callerRole,
            string stableShipId,
            ulong shipNetworkObjectId,
            GlobalMotionShipDeckLifecycleIngressMask ingressMask,
            bool supportsTerminalInvalidation,
            bool ownerReviewed,
            bool serverOwned,
            bool protocolOwned)
        {
            CallerRole = callerRole;
            StableShipId = stableShipId;
            ShipNetworkObjectId = shipNetworkObjectId;
            IngressMask = ingressMask;
            SupportsTerminalInvalidation = supportsTerminalInvalidation;
            OwnerReviewed = ownerReviewed;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
        }
    }

    public static class GlobalMotionShipDeckLifecycleCallerBindingContract
    {
        public static bool TryCreate(
            GlobalMotionShipDeckLifecycleCallerRole callerRole,
            string stableShipId,
            ulong shipNetworkObjectId,
            GlobalMotionShipDeckLifecycleIngressMask ingressMask,
            bool supportsTerminalInvalidation,
            bool ownerReviewed,
            bool serverOwned,
            bool protocolOwned,
            out GlobalMotionShipDeckLifecycleCallerBindingReceipt receipt,
            out string error)
        {
            receipt = default;
            if (callerRole == GlobalMotionShipDeckLifecycleCallerRole.None)
                return Reject("caller_role_required", out error);
            if (string.IsNullOrWhiteSpace(stableShipId))
                return Reject("stable_ship_id_required", out error);
            if (shipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (ingressMask == GlobalMotionShipDeckLifecycleIngressMask.None)
                return Reject("caller_ingress_required", out error);
            if (!supportsTerminalInvalidation)
                return Reject("terminal_invalidation_support_required", out error);
            if (!ownerReviewed)
                return Reject("owner_review_required", out error);
            if (!serverOwned)
                return Reject("server_owned_caller_required", out error);
            if (!protocolOwned)
                return Reject("protocol_owned_caller_required", out error);

            receipt = new GlobalMotionShipDeckLifecycleCallerBindingReceipt(
                callerRole,
                stableShipId.Trim(),
                shipNetworkObjectId,
                ingressMask,
                supportsTerminalInvalidation,
                ownerReviewed,
                serverOwned,
                protocolOwned);
            error = null;
            return true;
        }

        public static bool TryValidate(
            GlobalMotionShipDeckLifecycleCallerBindingReceipt receipt,
            out string error)
        {
            error = null;
            if (receipt.CallerRole == GlobalMotionShipDeckLifecycleCallerRole.None)
                return Reject("caller_role_required", out error);
            if (string.IsNullOrWhiteSpace(receipt.StableShipId))
                return Reject("stable_ship_id_required", out error);
            if (receipt.ShipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (receipt.IngressMask == GlobalMotionShipDeckLifecycleIngressMask.None)
                return Reject("caller_ingress_required", out error);
            if (!receipt.SupportsTerminalInvalidation)
                return Reject("terminal_invalidation_support_required", out error);
            if (!receipt.OwnerReviewed)
                return Reject("owner_review_required", out error);
            if (!receipt.ServerOwned)
                return Reject("server_owned_caller_required", out error);
            if (!receipt.ProtocolOwned)
                return Reject("protocol_owned_caller_required", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
