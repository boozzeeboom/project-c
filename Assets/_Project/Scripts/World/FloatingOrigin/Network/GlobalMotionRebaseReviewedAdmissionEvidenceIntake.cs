using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06BM: explicit owner-reviewed admission evidence intake.
    /// It stores no readiness by default and accepts evidence only through an explicit review submission.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionRebaseReviewedAdmissionEvidenceIntake : MonoBehaviour,
        IGlobalMotionRebaseReviewedAdmissionEvidenceSource
    {
        private GlobalMotionRebaseParticipantAdmissionEvidence _evidence;
        private bool _accepted;

        public bool IsAccepted => _accepted;

        public bool TrySubmit(
            GlobalMotionRebaseParticipantAdmissionEvidence evidence,
            bool ownerReviewed,
            out string error)
        {
            error = null;
            if (_accepted)
                return Reject("reviewed_admission_already_accepted", out error);
            if (!ownerReviewed)
                return Reject("owner_review_required", out error);
            if (!evidence.IdentityReviewed)
                return Reject("identity_not_reviewed", out error);
            if (!evidence.ExplicitPolicyReviewed)
                return Reject("explicit_policy_not_reviewed", out error);
            if (!evidence.CatalogSpatial)
                return Reject("catalog_spatial_not_reviewed", out error);

            _evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(
                identityReviewed: true,
                explicitPolicyReviewed: true,
                catalogSpatial: true,
                runtimeAdapterReady: false,
                runtimeProofComplete: false,
                rollbackReady: false);
            _accepted = true;
            return true;
        }

        public void Clear()
        {
            _evidence = default;
            _accepted = false;
        }

        public bool TryGetReviewedAdmissionEvidence(
            out GlobalMotionRebaseParticipantAdmissionEvidence evidence,
            out string error)
        {
            evidence = default;
            if (!_accepted)
                return Reject("reviewed_admission_not_submitted", out error);
            evidence = _evidence;
            error = null;
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
