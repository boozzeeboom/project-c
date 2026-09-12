using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06CW: explicit owner-reviewed publication bridge from ShipController persistent identity
    /// to the dormant lifecycle producer binding. It does not discover callers or attach itself to
    /// OnNetworkSpawn/OnNetworkDespawn; callers must invoke the typed methods explicitly.
    /// </summary>
    public sealed class GlobalMotionShipDeckIdentityPublicationBridge : IGlobalMotionShipDeckPassengerGenerationSource
    {
        private readonly GlobalMotionShipDeckPassengerLifecycleProducerBinding _binding;
        private GlobalMotionShipDeckStableIdentityReceipt _identity;
        private GlobalMotionShipDeckLifecycleCallerBindingReceipt _caller;
        private bool _published;

        public GlobalMotionShipDeckIdentityPublicationBridge(
            GlobalMotionShipDeckPassengerLifecycleProducerBinding binding)
        {
            _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        }

        public bool IsPublished => _published;
        public string StableShipId => _published ? _identity.StableShipId : null;
        public ulong ShipNetworkObjectId => _published ? _identity.ShipNetworkObjectId : 0UL;
        public GlobalMotionShipDeckStableIdentityReceipt Identity => _identity;
        public GlobalMotionShipDeckLifecycleCallerBindingReceipt CallerBinding => _caller;

        public bool TryPublishShipIdentity(
            string shipPersistentId,
            string manifestShipId,
            ulong shipNetworkObjectId,
            GlobalMotionShipDeckLifecycleCallerRole callerRole,
            GlobalMotionShipDeckLifecycleIngressMask ingressMask,
            bool ownerReviewed,
            bool serverOwned,
            bool protocolOwned,
            bool terminalInvalidationOwnerReviewed,
            out string error)
        {
            error = null;
            if (_published)
                return Reject("identity_publication_already_completed", out error);
            if (callerRole != GlobalMotionShipDeckLifecycleCallerRole.NpcShipController)
                return Reject("identity_publication_caller_role_invalid", out error);
            if ((ingressMask & GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered) == 0 ||
                (ingressMask & GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated) == 0)
                return Reject("identity_publication_ship_lifecycle_ingress_required", out error);

            if (!GlobalMotionShipDeckStableIdentityBoundary.TryCreateFromShipControllerPersistentId(
                    shipPersistentId,
                    shipNetworkObjectId,
                    ownerReviewed,
                    serverOwned,
                    protocolOwned,
                    terminalInvalidationOwnerReviewed,
                    out var identity,
                    out error))
                return false;
            if (!GlobalMotionShipDeckStableIdentityBoundary.TryValidateManifestAlignment(
                    identity.StableShipId, manifestShipId, out error))
                return false;
            if (!GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    callerRole,
                    identity.StableShipId,
                    identity.ShipNetworkObjectId,
                    ingressMask,
                    true,
                    ownerReviewed,
                    serverOwned,
                    protocolOwned,
                    out var caller,
                    out error))
                return false;

            _identity = identity;
            _caller = caller;
            _published = true;
            error = null;
            return true;
        }

        public bool TryRegisterPublishedShip(
            out GlobalMotionShipDeckPassengerLifecycleReceipt receipt,
            out string error)
        {
            receipt = default;
            if (!TryValidatePublished(out error))
                return false;
            return _binding.TryRegisterShipServerAuthorized(
                _identity.StableShipId,
                _identity.ShipNetworkObjectId,
                _identity.ServerOwned,
                _identity.ProtocolOwned,
                out receipt,
                out error);
        }

        public bool TryInvalidatePublishedShip(
            string invalidationReason,
            out GlobalMotionShipDeckPassengerLifecycleReceipt receipt,
            out string error)
        {
            receipt = default;
            if (!TryValidatePublished(out error))
                return false;
            return _binding.TryInvalidateShipServerAuthorized(
                _identity.StableShipId,
                _identity.ShipNetworkObjectId,
                _binding.ShipLifetimeGeneration,
                invalidationReason,
                _identity.ServerOwned,
                _identity.ProtocolOwned,
                out receipt,
                out error);
        }

        public bool TryGetGeneration(
            string passengerId,
            out GlobalMotionShipDeckPassengerGeneration generation,
            out string error)
        {
            return _binding.TryGetGeneration(passengerId, out generation, out error);
        }

        private bool TryValidatePublished(out string error)
        {
            error = null;
            if (!_published)
                return Reject("identity_publication_required", out error);
            if (!GlobalMotionShipDeckStableIdentityBoundary.TryValidate(_identity, out error))
                return false;
            if (!GlobalMotionShipDeckLifecycleCallerBindingContract.TryValidate(_caller, out error))
                return false;
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
