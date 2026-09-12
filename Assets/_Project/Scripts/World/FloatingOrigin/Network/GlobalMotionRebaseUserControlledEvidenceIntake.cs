using System;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06BK: explicit user-controlled structured evidence intake.
    /// Evidence is accepted only through Submit after external review; no defaults are generated.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionRebaseUserControlledEvidenceIntake : MonoBehaviour,
        IGlobalMotionRebaseRuntimeProofEvidenceSource,
        IGlobalMotionRebaseRollbackEvidenceSource
    {
        private GlobalMotionRebaseRuntimeProofEvidence _runtimeProof;
        private UnityStateRollbackEvidence _rollbackEvidence;
        private string _captureReference;
        private bool _accepted;

        public bool IsAccepted => _accepted;
        public string CaptureReference => _captureReference;

        public bool TrySubmit(
            GlobalMotionRebaseRuntimeProofEvidence runtimeProof,
            UnityStateRollbackEvidence rollbackEvidence,
            string captureReference,
            bool userSupplied,
            out string error)
        {
            error = null;
            if (_accepted)
                return Reject("evidence_intake_already_accepted", out error);
            if (!userSupplied)
                return Reject("user_supplied_capture_required", out error);
            if (string.IsNullOrWhiteSpace(captureReference) || captureReference.Trim() != captureReference)
                return Reject("capture_reference_required", out error);
            if (!GlobalMotionRebaseRuntimeProofContract.TryValidate(runtimeProof, out error))
                return false;
            if (!UnityStateRollbackContract.TryValidate(rollbackEvidence, out error))
                return false;
            if (!string.Equals(runtimeProof.ParticipantId, rollbackEvidence.ParticipantId, StringComparison.Ordinal))
                return Reject("runtime_proof_rollback_participant_mismatch", out error);
            if (runtimeProof.FrameGeneration != rollbackEvidence.TargetFrameGeneration)
                return Reject("runtime_proof_rollback_frame_mismatch", out error);

            _runtimeProof = runtimeProof;
            _rollbackEvidence = rollbackEvidence;
            _captureReference = captureReference;
            _accepted = true;
            return true;
        }

        public void Clear()
        {
            _runtimeProof = default;
            _rollbackEvidence = default;
            _captureReference = null;
            _accepted = false;
        }

        public bool TryGetRuntimeProof(
            out GlobalMotionRebaseRuntimeProofEvidence evidence,
            out string error)
        {
            evidence = default;
            if (!_accepted)
                return Reject("structured_runtime_evidence_not_submitted", out error);
            evidence = _runtimeProof;
            error = null;
            return true;
        }

        public bool TryGetRollbackEvidence(
            out UnityStateRollbackEvidence evidence,
            out string error)
        {
            evidence = default;
            if (!_accepted)
                return Reject("structured_rollback_evidence_not_submitted", out error);
            evidence = _rollbackEvidence;
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
