using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure validation for the dormant runtime-proof evidence contract.
    /// </summary>
    public static class ValidateGlobalMotionRebaseRuntimeProofContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Runtime Proof Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06T] Runtime proof contract: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06T] Runtime proof contract: {report.passed} pure checks PASS / {report.failed} FAIL; probes and live admission remain unimplemented.");
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

            const string participantId = "SHIP_ROOT/01";
            const ulong frameGeneration = 7;
            var captureId = new Guid("11111111-2222-3333-4444-555555555555");
            const GlobalMotionRebaseRuntimeProofRequirement all = GlobalMotionRebaseRuntimeProofRequirement.All;

            GlobalMotionRebaseRuntimeProofEvidence Complete() =>
                new GlobalMotionRebaseRuntimeProofEvidence(captureId, participantId, frameGeneration, all, all, "playmode:t-fo06t:ship-root-01");

            Check("Complete proof validates", () =>
            {
                Require(GlobalMotionRebaseRuntimeProofContract.TryValidate(Complete(), out string error), error);
                Require(GlobalMotionRebaseRuntimeProofContract.IsComplete(Complete()), "complete_proof_rejected");
            });

            Check("Missing capture identity fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseRuntimeProofEvidence(Guid.Empty, participantId, frameGeneration, all, all, "ref");
                Require(!GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error) && error == "capture_id_required", error);
            });

            Check("Missing participant identity fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseRuntimeProofEvidence(captureId, "", frameGeneration, all, all, "ref");
                Require(!GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error) && error == "participant_id_required", error);
            });

            Check("Missing frame generation fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseRuntimeProofEvidence(captureId, participantId, 0, all, all, "ref");
                Require(!GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error) && error == "frame_generation_required", error);
            });

            Check("Missing evidence reference fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseRuntimeProofEvidence(captureId, participantId, frameGeneration, all, all, " ");
                Require(!GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error) && error == "evidence_reference_required", error);
            });

            Check("Empty required proof fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseRuntimeProofEvidence(captureId, participantId, frameGeneration,
                    GlobalMotionRebaseRuntimeProofRequirement.None, GlobalMotionRebaseRuntimeProofRequirement.None, "ref");
                Require(!GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error) && error == "required_proof_missing", error);
            });

            Check("Unsupported required proof fails closed", () =>
            {
                var unsupported = (GlobalMotionRebaseRuntimeProofRequirement)(1 << 7);
                var evidence = new GlobalMotionRebaseRuntimeProofEvidence(captureId, participantId, frameGeneration, unsupported, unsupported, "ref");
                Require(!GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error) && error == "required_proof_contains_unsupported_flag", error);
            });

            Check("Verified proof outside required scope fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseRuntimeProofEvidence(captureId, participantId, frameGeneration,
                    GlobalMotionRebaseRuntimeProofRequirement.CameraOwnershipHistory,
                    GlobalMotionRebaseRuntimeProofRequirement.CameraOwnershipHistory | GlobalMotionRebaseRuntimeProofRequirement.PhysicsOrdering,
                    "ref");
                Require(!GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error) && error == "verified_proof_not_required:physics_ordering", error);
            });

            Check("Incomplete proof reports deterministic first missing requirement", () =>
            {
                var required = GlobalMotionRebaseRuntimeProofRequirement.CameraOwnershipHistory |
                    GlobalMotionRebaseRuntimeProofRequirement.NetworkTickOrdering;
                var evidence = new GlobalMotionRebaseRuntimeProofEvidence(captureId, participantId, frameGeneration,
                    required, GlobalMotionRebaseRuntimeProofRequirement.CameraOwnershipHistory, "ref");
                Require(!GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error) && error == "proof_incomplete:network_tick_ordering", error);
            });

            Check("Independent proof requirements compose without implicit admission", () =>
            {
                var required = GlobalMotionRebaseRuntimeProofRequirement.CameraOwnershipHistory |
                    GlobalMotionRebaseRuntimeProofRequirement.ShipDeckNavRegistration |
                    GlobalMotionRebaseRuntimeProofRequirement.PassengerProvenance;
                var evidence = new GlobalMotionRebaseRuntimeProofEvidence(captureId, participantId, frameGeneration, required, required, "ref");
                Require(GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error), error);
                Require(evidence.Required == required && evidence.Verified == required, "proof_scope_changed");
            });

            Check("Validation does not mutate evidence", () =>
            {
                var evidence = Complete();
                Require(GlobalMotionRebaseRuntimeProofContract.TryValidate(evidence, out string error), error);
                Require(evidence.CaptureId == captureId && evidence.ParticipantId == participantId &&
                    evidence.FrameGeneration == frameGeneration && evidence.Required == all && evidence.Verified == all &&
                    evidence.EvidenceReference == "playmode:t-fo06t:ship-root-01", "evidence_mutated");
            });

            return report;
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}