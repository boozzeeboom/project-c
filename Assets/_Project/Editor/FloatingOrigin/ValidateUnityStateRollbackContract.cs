using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for dormant Unity-state rollback evidence.
    /// </summary>
    public static class ValidateUnityStateRollbackContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Unity State Rollback Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06W] Unity-state rollback contract: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06W] Unity-state rollback contract: {report.passed} pure checks PASS / {report.failed} FAIL; native snapshot/restore remains unimplemented.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try
                {
                    action();
                    report.passed++;
                }
                catch (Exception exception)
                {
                    report.failed++;
                    report.failures.Add(name + ": " + exception.Message);
                }
            }

            const string transactionId = "rebase:tx-01";
            const string participantId = "SHIP_ROOT/03";
            const string snapshotId = "snapshot:tx-01:ship-root-03";
            const UnityStateRollbackCoverage all = UnityStateRollbackCoverage.All;

            UnityStateRollbackEvidence Ready() => new UnityStateRollbackEvidence(
                transactionId, participantId, snapshotId, 8, 8, all, all, all,
                snapshotIdentityCaptured: true,
                restoreAttempted: true,
                restoreSucceeded: true,
                participantIdentityRestored: true,
                reverseOrderVerified: true,
                rollbackOrdinal: 0,
                participantCount: 1);

            Check("Complete evidence validates", () =>
            {
                var evidence = Ready();
                Require(UnityStateRollbackContract.TryValidate(evidence, out string error), error);
                Require(UnityStateRollbackContract.IsReady(evidence), "complete_evidence_rejected");
            });

            Check("Missing transaction identity fails closed", () =>
            {
                var evidence = ReadyWith(transactionIdOverride: "");
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "transaction_id_required", error);
            });

            Check("Missing participant identity fails closed", () =>
            {
                var evidence = ReadyWith(participantIdOverride: "");
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "participant_id_required", error);
            });

            Check("Missing snapshot identity fails closed", () =>
            {
                var evidence = ReadyWith(snapshotIdOverride: "");
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "snapshot_id_required", error);
            });

            Check("Missing target generation fails closed", () =>
            {
                var evidence = ReadyWith(targetFrameGeneration: 0);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "target_frame_generation_required", error);
            });

            Check("Stale snapshot generation fails closed", () =>
            {
                var evidence = ReadyWith(snapshotFrameGeneration: 7);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "snapshot_generation_stale", error);
            });

            Check("Empty required coverage fails closed", () =>
            {
                var evidence = ReadyWith(requiredCoverage: UnityStateRollbackCoverage.None);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "required_rollback_coverage_missing", error);
            });

            Check("Unsupported required coverage fails closed", () =>
            {
                var unsupported = (UnityStateRollbackCoverage)(1 << 5);
                var evidence = ReadyWith(requiredCoverage: unsupported);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "required_rollback_coverage_unsupported", error);
            });

            Check("Captured coverage outside required scope fails closed", () =>
            {
                var evidence = ReadyWith(requiredCoverage: UnityStateRollbackCoverage.Transform,
                    capturedCoverage: UnityStateRollbackCoverage.Transform | UnityStateRollbackCoverage.Rigidbody);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "captured_coverage_outside_required", error);
            });

            Check("Missing captured coverage reports deterministic token", () =>
            {
                var evidence = ReadyWith(capturedCoverage: UnityStateRollbackCoverage.Transform);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "rollback_capture_incomplete:rigidbody", error);
            });

            Check("Missing participant count fails closed", () =>
            {
                var evidence = ReadyWith(participantCount: 0);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "rollback_participant_count_required", error);
            });

            Check("Invalid rollback ordinal fails closed", () =>
            {
                var evidence = ReadyWith(rollbackOrdinal: 1, participantCount: 1);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "rollback_ordinal_invalid", error);
            });

            Check("Missing snapshot identity fails closed", () =>
            {
                var evidence = ReadyWith(snapshotIdentityCaptured: false);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "rollback_snapshot_identity_missing", error);
            });

            Check("Rollback not attempted fails closed", () =>
            {
                var evidence = ReadyWith(restoreAttempted: false);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "rollback_not_attempted", error);
            });

            Check("Rollback failure fails closed", () =>
            {
                var evidence = ReadyWith(restoreSucceeded: false);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "rollback_failed", error);
            });

            Check("Participant identity mismatch fails closed", () =>
            {
                var evidence = ReadyWith(participantIdentityRestored: false);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "rollback_identity_mismatch:SHIP_ROOT/03", error);
            });

            Check("Rollback order fails closed", () =>
            {
                var evidence = ReadyWith(reverseOrderVerified: false);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "rollback_order_unverified", error);
            });

            Check("Restored coverage outside required scope fails closed", () =>
            {
                var evidence = ReadyWith(requiredCoverage: UnityStateRollbackCoverage.Transform,
                    capturedCoverage: UnityStateRollbackCoverage.Transform,
                    restoredCoverage: UnityStateRollbackCoverage.Transform | UnityStateRollbackCoverage.Rigidbody);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "restored_coverage_outside_required", error);
            });

            Check("Missing restored coverage reports deterministic token", () =>
            {
                var evidence = ReadyWith(restoredCoverage: UnityStateRollbackCoverage.Transform);
                Require(!UnityStateRollbackContract.TryValidate(evidence, out string error) && error == "rollback_restore_incomplete:rigidbody", error);
            });

            Check("Validation does not mutate evidence", () =>
            {
                var evidence = Ready();
                Require(UnityStateRollbackContract.TryValidate(evidence, out string error), error);
                Require(evidence.TransactionId == transactionId && evidence.ParticipantId == participantId &&
                    evidence.SnapshotId == snapshotId && evidence.TargetFrameGeneration == 8 &&
                    evidence.SnapshotFrameGeneration == 8 && evidence.RequiredCoverage == all &&
                    evidence.CapturedCoverage == all && evidence.RestoredCoverage == all &&
                    evidence.ReverseOrderVerified, "evidence_mutated");
            });

            return report;

            UnityStateRollbackEvidence ReadyWith(
                string transactionIdOverride = null,
                string participantIdOverride = null,
                string snapshotIdOverride = null,
                ulong? targetFrameGeneration = null,
                ulong? snapshotFrameGeneration = null,
                UnityStateRollbackCoverage? requiredCoverage = null,
                UnityStateRollbackCoverage? capturedCoverage = null,
                UnityStateRollbackCoverage? restoredCoverage = null,
                bool? snapshotIdentityCaptured = null,
                bool? restoreAttempted = null,
                bool? restoreSucceeded = null,
                bool? participantIdentityRestored = null,
                bool? reverseOrderVerified = null,
                int? rollbackOrdinal = null,
                int? participantCount = null)
            {
                return new UnityStateRollbackEvidence(
                    transactionIdOverride ?? transactionId,
                    participantIdOverride ?? participantId,
                    snapshotIdOverride ?? snapshotId,
                    targetFrameGeneration ?? 8,
                    snapshotFrameGeneration ?? 8,
                    requiredCoverage ?? all,
                    capturedCoverage ?? all,
                    restoredCoverage ?? all,
                    snapshotIdentityCaptured ?? true,
                    restoreAttempted ?? true,
                    restoreSucceeded ?? true,
                    participantIdentityRestored ?? true,
                    reverseOrderVerified ?? true,
                    rollbackOrdinal ?? 0,
                    participantCount ?? 1);
            }
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}