namespace ProjectC.World.FloatingOrigin.Network
{
    public interface IGlobalMotionRebaseNetworkBaselineEvidenceSource
    {
        bool TryGetNetworkBaselineEvidence(
            out GlobalMotionNetworkBaselineEvidence evidence,
            out string error);
    }

    /// <summary>
    /// Runtime-independent boundary for promoting NGO baseline evidence into a future
    /// NetworkBaseline native adapter. It rejects observation-only evidence and does not
    /// inspect, capture, restore or mutate NGO state.
    /// </summary>
    public readonly struct GlobalMotionRebaseNetworkBaselineAdapterBoundaryEvidence
    {
        public string ParticipantId { get; }
        public GlobalMotionNetworkBaselineIdentity Identity { get; }
        public ulong BindingAuthorityGeneration { get; }
        public ulong BindingDiscontinuityGeneration { get; }
        public bool ProtocolOwned { get; }
        public bool CaptureBoundaryDefined { get; }
        public bool RestoreBoundaryDefined { get; }
        public bool OwnershipRestoreDefined { get; }
        public bool LifetimeRestoreDefined { get; }
        public bool IsRestorable => ProtocolOwned && CaptureBoundaryDefined &&
            RestoreBoundaryDefined && OwnershipRestoreDefined && LifetimeRestoreDefined;

        internal GlobalMotionRebaseNetworkBaselineAdapterBoundaryEvidence(
            string participantId,
            GlobalMotionNetworkBaselineIdentity identity,
            ulong bindingAuthorityGeneration,
            ulong bindingDiscontinuityGeneration,
            bool protocolOwned,
            bool captureBoundaryDefined,
            bool restoreBoundaryDefined,
            bool ownershipRestoreDefined,
            bool lifetimeRestoreDefined)
        {
            ParticipantId = participantId;
            Identity = identity;
            BindingAuthorityGeneration = bindingAuthorityGeneration;
            BindingDiscontinuityGeneration = bindingDiscontinuityGeneration;
            ProtocolOwned = protocolOwned;
            CaptureBoundaryDefined = captureBoundaryDefined;
            RestoreBoundaryDefined = restoreBoundaryDefined;
            OwnershipRestoreDefined = ownershipRestoreDefined;
            LifetimeRestoreDefined = lifetimeRestoreDefined;
        }
    }

    public static class GlobalMotionRebaseNetworkBaselineAdapterBoundary
    {
        public static bool TryBuildRestorableBoundary(
            GlobalMotionNetworkBaselineEvidence evidence,
            bool protocolOwned,
            bool captureBoundaryDefined,
            bool restoreBoundaryDefined,
            bool ownershipRestoreDefined,
            bool lifetimeRestoreDefined,
            out GlobalMotionRebaseNetworkBaselineAdapterBoundaryEvidence boundary,
            out string error)
        {
            boundary = default;
            error = null;

            if (!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out error))
                return false;
            if (!GlobalMotionNetworkBaselineContract.CanClaimFullTransaction(evidence))
                return Reject("network_baseline_evidence_not_restorable", out error);
            if (!protocolOwned)
                return Reject("network_baseline_protocol_ownership_missing", out error);
            if (!captureBoundaryDefined)
                return Reject("network_baseline_capture_boundary_missing", out error);
            if (!restoreBoundaryDefined)
                return Reject("network_baseline_restore_boundary_missing", out error);
            if (!ownershipRestoreDefined)
                return Reject("network_baseline_ownership_restore_missing", out error);
            if (!lifetimeRestoreDefined)
                return Reject("network_baseline_lifetime_restore_missing", out error);

            boundary = new GlobalMotionRebaseNetworkBaselineAdapterBoundaryEvidence(
                evidence.ParticipantId,
                evidence.Identity,
                evidence.BindingAuthorityGeneration,
                evidence.BindingDiscontinuityGeneration,
                protocolOwned,
                captureBoundaryDefined,
                restoreBoundaryDefined,
                ownershipRestoreDefined,
                lifetimeRestoreDefined);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
