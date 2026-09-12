using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Immutable aggregate of reviewed user-controlled runtime evidence.
    /// It is a gate result only; it does not create readiness, publish a manifest or mutate Unity state.
    /// </summary>
    public readonly struct GlobalMotionRebaseUserControlledEvidenceReview
    {
        public string ManifestDigest { get; }
        public string SessionIdentity { get; }
        public int ParticipantCount { get; }
        public int ObservedPeerCount { get; }
        public int AdapterCount { get; }
        public Guid CaptureId { get; }
        public ulong FrameGeneration { get; }
        public bool IsReady => !string.IsNullOrWhiteSpace(ManifestDigest) &&
            !string.IsNullOrWhiteSpace(SessionIdentity) &&
            ParticipantCount > 0 && ObservedPeerCount > 0 && AdapterCount > 0 &&
            CaptureId != Guid.Empty && FrameGeneration > 0;

        internal GlobalMotionRebaseUserControlledEvidenceReview(
            string manifestDigest,
            string sessionIdentity,
            int participantCount,
            int observedPeerCount,
            int adapterCount,
            Guid captureId,
            ulong frameGeneration)
        {
            ManifestDigest = manifestDigest;
            SessionIdentity = sessionIdentity;
            ParticipantCount = participantCount;
            ObservedPeerCount = observedPeerCount;
            AdapterCount = adapterCount;
            CaptureId = captureId;
            FrameGeneration = frameGeneration;
        }
    }

    /// <summary>
    /// Aggregate fail-closed review for the next real runtime gate.
    /// It combines existing pure validators but performs no discovery, publication or Unity mutation.
    /// </summary>
    public static class GlobalMotionRebaseUserControlledEvidenceReviewGate
    {
        public static bool TryReview(
            GlobalMotionRebaseParticipantManifest manifest,
            GlobalMotionNativeAdapterSet adapters,
            GlobalMotionRebaseRuntimeReadinessEvidence readinessEvidence,
            GlobalMotionRebaseLiveManifestSessionEvidence sessionEvidence,
            string expectedSessionIdentity,
            string expectedPublisherIdentity,
            GlobalMotionRebaseRuntimeProofEvidence runtimeProof,
            UnityStateRollbackEvidence rollbackEvidence,
            out GlobalMotionRebaseUserControlledEvidenceReview review,
            out string error)
        {
            review = default;
            error = null;

            if (!GlobalMotionRebaseLiveManifestSessionEvidenceGate.TryValidate(
                    manifest,
                    sessionEvidence,
                    expectedSessionIdentity,
                    expectedPublisherIdentity,
                    out error))
                return false;

            if (!GlobalMotionRebaseRuntimeReadinessGate.TryValidate(
                    manifest,
                    adapters,
                    readinessEvidence,
                    out error))
                return false;

            if (!GlobalMotionRebaseRuntimeProofContract.TryValidate(runtimeProof, out error))
                return false;
            if (!UnityStateRollbackContract.TryValidate(rollbackEvidence, out error))
                return false;
            if (!string.Equals(runtimeProof.ParticipantId, rollbackEvidence.ParticipantId, StringComparison.Ordinal))
                return Reject("runtime_proof_rollback_participant_mismatch", out error);
            if (runtimeProof.FrameGeneration != rollbackEvidence.TargetFrameGeneration)
                return Reject("runtime_proof_rollback_frame_mismatch", out error);
            if (rollbackEvidence.ParticipantCount != manifest.Count)
                return Reject("rollback_manifest_participant_count_mismatch", out error);

            review = new GlobalMotionRebaseUserControlledEvidenceReview(
                sessionEvidence.Receipt.ManifestDigest,
                sessionEvidence.SessionIdentity,
                manifest.Count,
                sessionEvidence.ObservedPeerCount,
                adapters.Count,
                runtimeProof.CaptureId,
                runtimeProof.FrameGeneration);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
