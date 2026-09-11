using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    [Flags]
    public enum GlobalMotionRebaseRuntimeProofRequirement : byte
    {
        None = 0,
        CameraOwnershipHistory = 1 << 0,
        ShipDeckNavRegistration = 1 << 1,
        PassengerProvenance = 1 << 2,
        NetworkTickOrdering = 1 << 3,
        PhysicsOrdering = 1 << 4,
        NetworkBaselineContinuity = 1 << 5,
        UnityStateRollback = 1 << 6,
        All = CameraOwnershipHistory |
            ShipDeckNavRegistration |
            PassengerProvenance |
            NetworkTickOrdering |
            PhysicsOrdering |
            NetworkBaselineContinuity |
            UnityStateRollback
    }

    /// <summary>
    /// Immutable, runtime-independent envelope for explicit user-controlled Play Mode proof.
    /// It records what a future probe must demonstrate but does not collect or assert runtime state.
    /// </summary>
    public readonly struct GlobalMotionRebaseRuntimeProofEvidence
    {
        public Guid CaptureId { get; }
        public string ParticipantId { get; }
        public ulong FrameGeneration { get; }
        public GlobalMotionRebaseRuntimeProofRequirement Required { get; }
        public GlobalMotionRebaseRuntimeProofRequirement Verified { get; }
        public string EvidenceReference { get; }

        public GlobalMotionRebaseRuntimeProofEvidence(
            Guid captureId,
            string participantId,
            ulong frameGeneration,
            GlobalMotionRebaseRuntimeProofRequirement required,
            GlobalMotionRebaseRuntimeProofRequirement verified,
            string evidenceReference)
        {
            CaptureId = captureId;
            ParticipantId = participantId;
            FrameGeneration = frameGeneration;
            Required = required;
            Verified = verified;
            EvidenceReference = evidenceReference;
        }
    }

    public static class GlobalMotionRebaseRuntimeProofContract
    {
        private static readonly GlobalMotionRebaseRuntimeProofRequirement[] OrderedRequirements =
        {
            GlobalMotionRebaseRuntimeProofRequirement.CameraOwnershipHistory,
            GlobalMotionRebaseRuntimeProofRequirement.ShipDeckNavRegistration,
            GlobalMotionRebaseRuntimeProofRequirement.PassengerProvenance,
            GlobalMotionRebaseRuntimeProofRequirement.NetworkTickOrdering,
            GlobalMotionRebaseRuntimeProofRequirement.PhysicsOrdering,
            GlobalMotionRebaseRuntimeProofRequirement.NetworkBaselineContinuity,
            GlobalMotionRebaseRuntimeProofRequirement.UnityStateRollback
        };

        public static bool TryValidate(GlobalMotionRebaseRuntimeProofEvidence evidence, out string error)
        {
            error = null;
            if (evidence.CaptureId == Guid.Empty)
                return Reject("capture_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.ParticipantId))
                return Reject("participant_id_required", out error);
            if (evidence.FrameGeneration == 0)
                return Reject("frame_generation_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.EvidenceReference) || evidence.EvidenceReference.Trim() != evidence.EvidenceReference)
                return Reject("evidence_reference_required", out error);

            byte required = (byte)evidence.Required;
            byte verified = (byte)evidence.Verified;
            byte supported = (byte)GlobalMotionRebaseRuntimeProofRequirement.All;
            if ((required & ~supported) != 0)
                return Reject("required_proof_contains_unsupported_flag", out error);
            if (required == 0)
                return Reject("required_proof_missing", out error);
            if ((verified & ~supported) != 0)
                return Reject("verified_proof_contains_unsupported_flag", out error);
            if ((verified & ~required) != 0)
                return Reject("verified_proof_not_required:" + FirstToken((byte)(verified & ~required)), out error);

            byte missing = (byte)(required & ~verified);
            if (missing != 0)
                return Reject("proof_incomplete:" + FirstToken(missing), out error);

            return true;
        }

        public static bool IsComplete(GlobalMotionRebaseRuntimeProofEvidence evidence)
        {
            return TryValidate(evidence, out _);
        }

        private static string FirstToken(byte mask)
        {
            for (int i = 0; i < OrderedRequirements.Length; i++)
            {
                var requirement = OrderedRequirements[i];
                if ((((byte)requirement) & mask) != 0)
                    return ToToken(requirement);
            }
            return "unknown";
        }

        private static string ToToken(GlobalMotionRebaseRuntimeProofRequirement requirement)
        {
            switch (requirement)
            {
                case GlobalMotionRebaseRuntimeProofRequirement.CameraOwnershipHistory: return "camera_ownership_history";
                case GlobalMotionRebaseRuntimeProofRequirement.ShipDeckNavRegistration: return "ship_deck_nav_registration";
                case GlobalMotionRebaseRuntimeProofRequirement.PassengerProvenance: return "passenger_provenance";
                case GlobalMotionRebaseRuntimeProofRequirement.NetworkTickOrdering: return "network_tick_ordering";
                case GlobalMotionRebaseRuntimeProofRequirement.PhysicsOrdering: return "physics_ordering";
                case GlobalMotionRebaseRuntimeProofRequirement.NetworkBaselineContinuity: return "network_baseline_continuity";
                case GlobalMotionRebaseRuntimeProofRequirement.UnityStateRollback: return "unity_state_rollback";
                default: return "unknown";
            }
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}