using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    [Flags]
    public enum UnityStateRollbackCoverage : byte
    {
        None = 0,
        Transform = 1 << 0,
        Rigidbody = 1 << 1,
        ShipDeckNav = 1 << 2,
        CameraHistory = 1 << 3,
        NetworkBaseline = 1 << 4,
        All = Transform | Rigidbody | ShipDeckNav | CameraHistory | NetworkBaseline
    }

    /// <summary>
    /// Runtime-independent evidence envelope for one participant rollback snapshot.
    /// It does not capture or restore Unity state.
    /// </summary>
    public readonly struct UnityStateRollbackEvidence
    {
        public string TransactionId { get; }
        public string ParticipantId { get; }
        public string SnapshotId { get; }
        public ulong TargetFrameGeneration { get; }
        public ulong SnapshotFrameGeneration { get; }
        public UnityStateRollbackCoverage RequiredCoverage { get; }
        public UnityStateRollbackCoverage CapturedCoverage { get; }
        public UnityStateRollbackCoverage RestoredCoverage { get; }
        public bool SnapshotIdentityCaptured { get; }
        public bool RestoreAttempted { get; }
        public bool RestoreSucceeded { get; }
        public bool ParticipantIdentityRestored { get; }
        public bool ReverseOrderVerified { get; }
        public int RollbackOrdinal { get; }
        public int ParticipantCount { get; }

        public UnityStateRollbackEvidence(
            string transactionId,
            string participantId,
            string snapshotId,
            ulong targetFrameGeneration,
            ulong snapshotFrameGeneration,
            UnityStateRollbackCoverage requiredCoverage,
            UnityStateRollbackCoverage capturedCoverage,
            UnityStateRollbackCoverage restoredCoverage,
            bool snapshotIdentityCaptured,
            bool restoreAttempted,
            bool restoreSucceeded,
            bool participantIdentityRestored,
            bool reverseOrderVerified,
            int rollbackOrdinal,
            int participantCount)
        {
            TransactionId = transactionId;
            ParticipantId = participantId;
            SnapshotId = snapshotId;
            TargetFrameGeneration = targetFrameGeneration;
            SnapshotFrameGeneration = snapshotFrameGeneration;
            RequiredCoverage = requiredCoverage;
            CapturedCoverage = capturedCoverage;
            RestoredCoverage = restoredCoverage;
            SnapshotIdentityCaptured = snapshotIdentityCaptured;
            RestoreAttempted = restoreAttempted;
            RestoreSucceeded = restoreSucceeded;
            ParticipantIdentityRestored = participantIdentityRestored;
            ReverseOrderVerified = reverseOrderVerified;
            RollbackOrdinal = rollbackOrdinal;
            ParticipantCount = participantCount;
        }
    }

    public static class UnityStateRollbackContract
    {
        private static readonly UnityStateRollbackCoverage[] OrderedCoverage =
        {
            UnityStateRollbackCoverage.Transform,
            UnityStateRollbackCoverage.Rigidbody,
            UnityStateRollbackCoverage.ShipDeckNav,
            UnityStateRollbackCoverage.CameraHistory,
            UnityStateRollbackCoverage.NetworkBaseline
        };

        public static bool TryValidate(UnityStateRollbackEvidence evidence, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(evidence.TransactionId))
                return Reject("transaction_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.ParticipantId))
                return Reject("participant_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.SnapshotId))
                return Reject("snapshot_id_required", out error);
            if (evidence.TargetFrameGeneration == 0)
                return Reject("target_frame_generation_required", out error);
            if (evidence.SnapshotFrameGeneration == 0)
                return Reject("snapshot_frame_generation_required", out error);
            if (evidence.SnapshotFrameGeneration != evidence.TargetFrameGeneration)
                return Reject("snapshot_generation_stale", out error);
            if (evidence.ParticipantCount <= 0)
                return Reject("rollback_participant_count_required", out error);
            if (evidence.RollbackOrdinal < 0 || evidence.RollbackOrdinal >= evidence.ParticipantCount)
                return Reject("rollback_ordinal_invalid", out error);

            byte required = (byte)evidence.RequiredCoverage;
            byte captured = (byte)evidence.CapturedCoverage;
            byte restored = (byte)evidence.RestoredCoverage;
            byte supported = (byte)UnityStateRollbackCoverage.All;
            if ((required & ~supported) != 0)
                return Reject("required_rollback_coverage_unsupported", out error);
            if (required == 0)
                return Reject("required_rollback_coverage_missing", out error);
            if ((captured & ~supported) != 0)
                return Reject("captured_rollback_coverage_unsupported", out error);
            if ((captured & ~required) != 0)
                return Reject("captured_coverage_outside_required", out error);
            if ((restored & ~supported) != 0)
                return Reject("restored_rollback_coverage_unsupported", out error);
            if ((restored & ~required) != 0)
                return Reject("restored_coverage_outside_required", out error);
            if (!evidence.SnapshotIdentityCaptured)
                return Reject("rollback_snapshot_identity_missing", out error);

            byte missingCapture = (byte)(required & ~captured);
            if (missingCapture != 0)
                return Reject("rollback_capture_incomplete:" + FirstToken(missingCapture), out error);
            if (!evidence.RestoreAttempted)
                return Reject("rollback_not_attempted", out error);
            if (!evidence.RestoreSucceeded)
                return Reject("rollback_failed", out error);
            if (!evidence.ParticipantIdentityRestored)
                return Reject("rollback_identity_mismatch:" + evidence.ParticipantId, out error);
            if (!evidence.ReverseOrderVerified)
                return Reject("rollback_order_unverified", out error);

            byte missingRestore = (byte)(required & ~restored);
            if (missingRestore != 0)
                return Reject("rollback_restore_incomplete:" + FirstToken(missingRestore), out error);

            return true;
        }

        public static bool IsReady(UnityStateRollbackEvidence evidence)
        {
            return TryValidate(evidence, out _);
        }

        private static string FirstToken(byte mask)
        {
            for (int i = 0; i < OrderedCoverage.Length; i++)
            {
                UnityStateRollbackCoverage coverage = OrderedCoverage[i];
                if ((((byte)coverage) & mask) != 0)
                    return ToToken(coverage);
            }
            return "unknown";
        }

        private static string ToToken(UnityStateRollbackCoverage coverage)
        {
            switch (coverage)
            {
                case UnityStateRollbackCoverage.Transform: return "transform";
                case UnityStateRollbackCoverage.Rigidbody: return "rigidbody";
                case UnityStateRollbackCoverage.ShipDeckNav: return "ship_deck_nav";
                case UnityStateRollbackCoverage.CameraHistory: return "camera_history";
                case UnityStateRollbackCoverage.NetworkBaseline: return "network_baseline";
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