using System;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    public interface IGlobalMotionRebaseReviewedAdmissionEvidenceSource
    {
        bool TryGetReviewedAdmissionEvidence(
            out GlobalMotionRebaseParticipantAdmissionEvidence evidence,
            out string error);
    }

    public interface IGlobalMotionRebaseNativeAdapterEvidenceSource
    {
        bool TryGetNativeAdapterSet(
            out GlobalMotionNativeAdapterSet adapters,
            out string error);
    }

    public interface IGlobalMotionRebaseRuntimeProofEvidenceSource
    {
        bool TryGetRuntimeProof(
            out GlobalMotionRebaseRuntimeProofEvidence evidence,
            out string error);
    }

    public interface IGlobalMotionRebaseRollbackEvidenceSource
    {
        bool TryGetRollbackEvidence(
            out UnityStateRollbackEvidence evidence,
            out string error);
    }

    /// <summary>
    /// T-FO06BF: fail-closed aggregation boundary for provenance-bearing admission evidence.
    /// It accepts evidence only from explicit source contracts and never fabricates readiness.
    /// The component is dormant until all source contracts have concrete reviewed producers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionRebaseAdmissionEvidenceProvider : MonoBehaviour,
        IGlobalMotionRebaseRuntimeAdmissionEvidenceSource
    {
        [SerializeField] private MonoBehaviour _reviewedAdmissionSource;
        [SerializeField] private MonoBehaviour _nativeAdapterSource;
        [SerializeField] private MonoBehaviour _runtimeProofSource;
        [SerializeField] private MonoBehaviour _rollbackSource;

        public bool TryGetAdmissionEvidence(
            out GlobalMotionRebaseParticipantAdmissionEvidence evidence,
            out string error)
        {
            evidence = default;
            error = null;

            if (!(_reviewedAdmissionSource is IGlobalMotionRebaseReviewedAdmissionEvidenceSource reviewedSource))
                return Reject("reviewed_admission_source_missing", out error);
            if (!(_nativeAdapterSource is IGlobalMotionRebaseNativeAdapterEvidenceSource adapterSource))
                return Reject("native_adapter_evidence_source_missing", out error);
            if (!(_runtimeProofSource is IGlobalMotionRebaseRuntimeProofEvidenceSource proofSource))
                return Reject("runtime_proof_evidence_source_missing", out error);
            if (!(_rollbackSource is IGlobalMotionRebaseRollbackEvidenceSource rollbackSource))
                return Reject("rollback_evidence_source_missing", out error);

            if (!reviewedSource.TryGetReviewedAdmissionEvidence(out var reviewedEvidence, out error))
                return false;
            if (!reviewedEvidence.IdentityReviewed || !reviewedEvidence.ExplicitPolicyReviewed || !reviewedEvidence.CatalogSpatial)
                return Reject("reviewed_admission_evidence_incomplete", out error);

            if (!adapterSource.TryGetNativeAdapterSet(out var adapters, out error) || adapters == null)
                return false;
            if (!adapters.TryValidateReady(out error))
                return false;

            if (!proofSource.TryGetRuntimeProof(out var runtimeProof, out error))
                return false;
            if (!GlobalMotionRebaseRuntimeProofContract.TryValidate(runtimeProof, out error))
                return false;

            if (!rollbackSource.TryGetRollbackEvidence(out var rollbackEvidence, out error))
                return false;
            if (!UnityStateRollbackContract.TryValidate(rollbackEvidence, out error))
                return false;
            if (!string.Equals(runtimeProof.ParticipantId, rollbackEvidence.ParticipantId, StringComparison.Ordinal))
                return Reject("runtime_proof_rollback_participant_mismatch", out error);
            if (runtimeProof.FrameGeneration != rollbackEvidence.TargetFrameGeneration)
                return Reject("runtime_proof_rollback_frame_mismatch", out error);

            evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(
                reviewedEvidence.IdentityReviewed,
                reviewedEvidence.ExplicitPolicyReviewed,
                reviewedEvidence.CatalogSpatial,
                true,
                true,
                true);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
