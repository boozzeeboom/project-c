using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Pure fail-closed admission gate for future live participant-manifest publication.
    /// This policy is not connected to runtime discovery or frame mutation.
    /// </summary>
    public readonly struct GlobalMotionRebaseParticipantAdmissionEvidence
    {
        public bool IdentityReviewed { get; }
        public bool ExplicitPolicyReviewed { get; }
        public bool CatalogSpatial { get; }
        public bool RuntimeAdapterReady { get; }
        public bool RuntimeProofComplete { get; }
        public bool RollbackReady { get; }

        public GlobalMotionRebaseParticipantAdmissionEvidence(
            bool identityReviewed,
            bool explicitPolicyReviewed,
            bool catalogSpatial,
            bool runtimeAdapterReady,
            bool runtimeProofComplete,
            bool rollbackReady)
        {
            IdentityReviewed = identityReviewed;
            ExplicitPolicyReviewed = explicitPolicyReviewed;
            CatalogSpatial = catalogSpatial;
            RuntimeAdapterReady = runtimeAdapterReady;
            RuntimeProofComplete = runtimeProofComplete;
            RollbackReady = rollbackReady;
        }
    }

    public static class GlobalMotionRebaseParticipantAdmissionPolicy
    {
        public static bool TryAdmit(
            GlobalMotionRebaseManifestEntry entry,
            GlobalMotionRebaseParticipantAdmissionEvidence evidence,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(entry.ParticipantId))
                return Reject("participant_id_missing", out error);
            if (!evidence.IdentityReviewed)
                return Reject("identity_not_reviewed:" + entry.ParticipantId, out error);
            if (!evidence.ExplicitPolicyReviewed)
                return Reject("explicit_policy_not_reviewed:" + entry.ParticipantId, out error);
            if (!evidence.CatalogSpatial)
                return Reject("catalog_spatial_false:" + entry.ParticipantId, out error);
            if (!evidence.RuntimeAdapterReady)
                return Reject("runtime_adapter_not_ready:" + entry.ParticipantId, out error);
            if (!evidence.RuntimeProofComplete)
                return Reject("runtime_proof_incomplete:" + entry.ParticipantId, out error);
            if (!evidence.RollbackReady)
                return Reject("rollback_not_ready:" + entry.ParticipantId, out error);

            error = string.Empty;
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
