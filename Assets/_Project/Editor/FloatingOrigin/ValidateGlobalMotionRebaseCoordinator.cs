using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// T-FO06N implementation coverage for the dormant rebase coordinator.
    /// Pure in-memory checks only: no scenes, GameObjects, physics, NavMesh, NGO or Play Mode.
    /// </summary>
    public static class ValidateGlobalMotionRebaseCoordinator
    {
        [Serializable]
        public sealed class Report
        {
            public int passed;
            public int failed;
            public string[] checks;
            public string[] failures;
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Rebase Coordinator")]
        public static void Execute()
        {
            var report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException(JsonUtility.ToJson(report, true));

            Debug.Log($"[T-FO06N] Rebase coordinator: {report.passed} pure checks PASS / {report.failed} FAIL; runtime apply remains blocked.");
        }

        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stable Edit Mode required.");

            var passed = new List<string>();
            var failed = new List<string>();

            void Check(string name, Action action)
            {
                try
                {
                    action();
                    passed.Add(name);
                }
                catch (Exception exception)
                {
                    failed.Add(name + ": " + exception.GetType().Name + ": " + exception.Message);
                }
            }

            Check("Request rejects default identity, generation, plan, count and digest", () =>
            {
                var request = default(GlobalMotionRebaseRequest);
                Require(!request.IsValid);
                Require(!request.TryValidate(out var error) && error == "transaction_id_required");

                request = GlobalMotionRebaseRequest.Create(0, Plan(), 1, "digest");
                Require(!request.TryValidate(out error) && error == "frame_generation_required");

                request = GlobalMotionRebaseRequest.Create(1, default, 1, "digest");
                Require(!request.TryValidate(out error) && error == "valid_origin_rebase_plan_required");

                request = GlobalMotionRebaseRequest.Create(1, Plan(), 0, "digest");
                Require(!request.TryValidate(out error) && error == "expected_participant_count_required");

                request = GlobalMotionRebaseRequest.Create(1, Plan(), 1, " ");
                Require(!request.TryValidate(out error) && error == "participant_manifest_digest_required");
            });

            Check("Valid request preserves transaction identity and immutable fields", () =>
            {
                var request = GlobalMotionRebaseRequest.Create(7, Plan(), 2, "digest-a");
                Require(request.IsValid && request.TransactionId != Guid.Empty && request.FrameGeneration == 7);
                Require(request.ExpectedParticipantCount == 2 && request.ParticipantManifestDigest == "digest-a");
                Require(typeof(GlobalMotionRebaseRequest).GetProperty(nameof(GlobalMotionRebaseRequest.TransactionId)).CanWrite == false);
            });

            Check("Participant set rejects null, missing identity and duplicates", () =>
            {
                var set = new GlobalMotionRebaseParticipantSet();
                Require(!set.TryAdd(null, out var error) && error == "participant_missing");
                Require(!set.TryAdd(new FakeParticipant("", true), out error) && error == "participant_id_required");
                Require(set.TryAdd(new FakeParticipant("A", true), out error));
                Require(!set.TryAdd(new FakeParticipant("A", true), out error) && error == "duplicate_participant_id:A");
            });

            Check("Participant set requires non-empty seal and rejects mutation after seal", () =>
            {
                var empty = new GlobalMotionRebaseParticipantSet();
                Require(!empty.Seal(out var error) && error == "participant_set_empty");

                var set = new GlobalMotionRebaseParticipantSet();
                Require(set.TryAdd(new FakeParticipant("A", true), out error));
                Require(set.Seal(out error) && set.IsSealed);
                Require(!set.TryAdd(new FakeParticipant("B", true), out error) && error == "participant_set_already_sealed");
                Require(!set.Seal(out error) && error == "participant_set_already_sealed");
            });

            Check("Closed-world validation rejects unsealed, count-mismatched and stale sets", () =>
            {
                var request = GlobalMotionRebaseRequest.Create(1, Plan(), 1, "digest");
                var unsealed = new GlobalMotionRebaseParticipantSet();
                Require(unsealed.TryAdd(new FakeParticipant("A", true), out _));
                Require(!unsealed.TryValidateClosedWorld(request, out var error) && error == "participant_set_must_be_sealed");

                var countMismatch = new GlobalMotionRebaseParticipantSet();
                Require(countMismatch.TryAdd(new FakeParticipant("A", true), out _));
                Require(countMismatch.Seal(out _));
                var mismatchRequest = GlobalMotionRebaseRequest.Create(1, Plan(), 2, "digest");
                Require(!countMismatch.TryValidateClosedWorld(mismatchRequest, out error) && error.StartsWith("participant_count_mismatch:", StringComparison.Ordinal));

                var stale = new GlobalMotionRebaseParticipantSet();
                Require(stale.TryAdd(new FakeParticipant("A", false), out _));
                Require(stale.Seal(out _));
                Require(!stale.TryValidateClosedWorld(request, out error) && error == "stale_participant:A");
            });

            Check("Successful preparation freezes, preflights, captures and stops at Captured", () =>
            {
                var gate = new FakeGate();
                var first = new FakeParticipant("A", true);
                var second = new FakeParticipant("B", true);
                var set = Set(first, second);
                var coordinator = new GlobalMotionRebaseCoordinator(gate);
                var request = GlobalMotionRebaseRequest.Create(2, Plan(), 2, "digest");

                Require(coordinator.TryPrepare(request, set, out var error), error);
                Require(coordinator.Phase == GlobalMotionRebasePhase.Captured);
                Require(coordinator.ActiveTransactionId == request.TransactionId);
                Require(coordinator.CapturedParticipantCount == 2);
                Require(gate.FreezeCalls == 1 && gate.ReleaseCalls == 0);
                Require(first.PreflightCalls == 1 && second.PreflightCalls == 1);
                Require(first.CaptureCalls == 1 && second.CaptureCalls == 1);
                Require(!typeof(UnityEngine.Object).IsAssignableFrom(typeof(GlobalMotionRebaseCoordinator)));
            });

            Check("Snapshot identity mismatch aborts before retaining a foreign snapshot", () =>
            {
                var gate = new FakeGate();
                var participant = new FakeParticipant("A", true) { SnapshotId = "FOREIGN" };
                var coordinator = new GlobalMotionRebaseCoordinator(gate);
                Require(coordinator.TryPrepare(GlobalMotionRebaseRequest.Create(1, Plan(), 1, "digest"), Set(participant), out var error) == false);
                Require(error.StartsWith("participant_snapshot_identity_mismatch:A", StringComparison.Ordinal));
                Require(coordinator.Phase == GlobalMotionRebasePhase.Aborted && coordinator.CapturedParticipantCount == 0);
                Require(gate.FreezeCalls == 1 && gate.ReleaseCalls == 1);
            });

            Check("Reverse-order rollback restores captured participants before release", () =>
            {
                var gate = new FakeGate();
                var first = new FakeParticipant("A", true);
                var second = new FakeParticipant("B", true);
                var coordinator = new GlobalMotionRebaseCoordinator(gate);
                Require(coordinator.TryPrepare(GlobalMotionRebaseRequest.Create(1, Plan(), 2, "digest"), Set(first, second), out _));
                Require(coordinator.TryAbort(out var error), error);
                Require(coordinator.Phase == GlobalMotionRebasePhase.Aborted);
                Require(first.RestoreCalls == 1 && second.RestoreCalls == 1);
                Require(string.Join(",", coordinatorRestoreOrder(first, second)) == "B,A");
                Require(gate.ReleaseCalls == 1);
            });

            Check("Preflight failure rolls back nothing but still releases a successful freeze", () =>
            {
                var gate = new FakeGate();
                var first = new FakeParticipant("A", true);
                var second = new FakeParticipant("B", true) { PreflightResult = false, PreflightError = "not_ready" };
                var coordinator = new GlobalMotionRebaseCoordinator(gate);
                Require(!coordinator.TryPrepare(GlobalMotionRebaseRequest.Create(1, Plan(), 2, "digest"), Set(first, second), out var error));
                Require(error.StartsWith("participant_preflight_refused:B:not_ready", StringComparison.Ordinal));
                Require(coordinator.Phase == GlobalMotionRebasePhase.Aborted);
                Require(first.RestoreCalls == 0 && second.RestoreCalls == 0 && gate.ReleaseCalls == 1);
            });

            Check("Capture failure restores already-captured participants in reverse order", () =>
            {
                var gate = new FakeGate();
                var first = new FakeParticipant("A", true);
                var second = new FakeParticipant("B", true) { CaptureResult = false, CaptureError = "capture_failed" };
                var coordinator = new GlobalMotionRebaseCoordinator(gate);
                Require(!coordinator.TryPrepare(GlobalMotionRebaseRequest.Create(1, Plan(), 2, "digest"), Set(first, second), out var error));
                Require(error.StartsWith("participant_capture_refused:B:capture_failed", StringComparison.Ordinal));
                Require(coordinator.Phase == GlobalMotionRebasePhase.Aborted && coordinator.CapturedParticipantCount == 1);
                Require(first.RestoreCalls == 1 && second.RestoreCalls == 0 && gate.ReleaseCalls == 1);
            });

            Check("Freeze refusal aborts without attempting release", () =>
            {
                var gate = new FakeGate { FreezeResult = false, FreezeError = "busy" };
                var participant = new FakeParticipant("A", true);
                var coordinator = new GlobalMotionRebaseCoordinator(gate);
                Require(!coordinator.TryPrepare(GlobalMotionRebaseRequest.Create(1, Plan(), 1, "digest"), Set(participant), out var error));
                Require(error.StartsWith("freeze_gate_refused:busy", StringComparison.Ordinal));
                Require(coordinator.Phase == GlobalMotionRebasePhase.Aborted && gate.ReleaseCalls == 0 && participant.PreflightCalls == 0);
            });

            Check("Restore failure and release failure fault closed", () =>
            {
                var restoreGate = new FakeGate();
                var restoreFailure = new FakeParticipant("A", true) { RestoreResult = false, RestoreError = "restore_failed" };
                var restoreCoordinator = new GlobalMotionRebaseCoordinator(restoreGate);
                Require(restoreCoordinator.TryPrepare(GlobalMotionRebaseRequest.Create(1, Plan(), 1, "digest"), Set(restoreFailure), out _));
                Require(!restoreCoordinator.TryAbort(out var restoreError));
                Require(restoreCoordinator.Phase == GlobalMotionRebasePhase.Faulted && restoreError.Contains("restore:A:restore_failed"));

                var releaseGate = new FakeGate { ReleaseResult = false, ReleaseError = "release_failed" };
                var releaseCoordinator = new GlobalMotionRebaseCoordinator(releaseGate);
                Require(releaseCoordinator.TryPrepare(GlobalMotionRebaseRequest.Create(1, Plan(), 1, "digest"), Set(new FakeParticipant("A", true)), out _));
                Require(!releaseCoordinator.TryAbort(out var releaseError));
                Require(releaseCoordinator.Phase == GlobalMotionRebasePhase.Faulted && releaseError.Contains("release:release_failed"));
            });

            Check("Abort/reset lifecycle rejects duplicate active work and permits a clean retry", () =>
            {
                var gate = new FakeGate();
                var coordinator = new GlobalMotionRebaseCoordinator(gate);
                var request = GlobalMotionRebaseRequest.Create(1, Plan(), 1, "digest");
                Require(coordinator.TryPrepare(request, Set(new FakeParticipant("A", true)), out _));
                Require(!coordinator.TryPrepare(request, Set(new FakeParticipant("B", true)), out var error) && error.StartsWith("coordinator_not_idle:", StringComparison.Ordinal));
                Require(coordinator.TryAbort(out _));
                coordinator.Reset();
                Require(coordinator.Phase == GlobalMotionRebasePhase.Idle && coordinator.CapturedParticipantCount == 0);
                Require(coordinator.TryPrepare(GlobalMotionRebaseRequest.Create(2, Plan(), 1, "digest"), Set(new FakeParticipant("C", true)), out _));
            });

            return new Report
            {
                passed = passed.Count,
                failed = failed.Count,
                checks = passed.ToArray(),
                failures = failed.ToArray()
            };
        }

        private static GlobalMotionRebaseParticipantSet Set(params FakeParticipant[] participants)
        {
            var set = new GlobalMotionRebaseParticipantSet();
            foreach (var participant in participants)
                Require(set.TryAdd(participant, out var error), error);
            return set;
        }

        private static OriginRebasePlan Plan()
        {
            var frame = new LocalCoordinateFrame(GlobalPosition.Zero);
            Require(OriginRebasePlan.TryCreate(frame, new Vector3(4096f, 0f, 0f), 256f, 256f, out var plan));
            return plan;
        }

        private static string[] coordinatorRestoreOrder(FakeParticipant first, FakeParticipant second)
        {
            return new[] { second.RestoreOrder, first.RestoreOrder };
        }

        private static void Require(bool condition, string reason = null)
        {
            if (!condition)
                throw new InvalidOperationException(reason ?? "Assertion failed.");
        }

        private sealed class FakeGate : IGlobalMotionRebaseGate
        {
            public bool FreezeResult = true;
            public bool ReleaseResult = true;
            public string FreezeError = "freeze_refused";
            public string ReleaseError = "release_refused";
            public int FreezeCalls;
            public int ReleaseCalls;

            public bool TryFreeze(GlobalMotionRebaseRequest request, out string error)
            {
                FreezeCalls++;
                error = FreezeResult ? null : FreezeError;
                return FreezeResult;
            }

            public bool TryRelease(GlobalMotionRebaseRequest request, out string error)
            {
                ReleaseCalls++;
                error = ReleaseResult ? null : ReleaseError;
                return ReleaseResult;
            }
        }

        private sealed class FakeParticipant : IGlobalMotionRebaseParticipant
        {
            private int _restoreSequence;

            public string ParticipantId { get; }
            public GlobalMotionRebaseParticipantKind Kind => GlobalMotionRebaseParticipantKind.NetworkGameplayRoot;
            public bool IsCurrent { get; }
            public bool PreflightResult = true;
            public bool CaptureResult = true;
            public bool RestoreResult = true;
            public string PreflightError = "preflight_refused";
            public string CaptureError = "capture_refused";
            public string RestoreError = "restore_refused";
            public string SnapshotId;
            public int PreflightCalls;
            public int CaptureCalls;
            public int RestoreCalls;
            public string RestoreOrder;

            public FakeParticipant(string participantId, bool isCurrent)
            {
                ParticipantId = participantId;
                IsCurrent = isCurrent;
            }

            public bool TryPreflight(GlobalMotionRebaseRequest request, out string error)
            {
                PreflightCalls++;
                error = PreflightResult ? null : PreflightError;
                return PreflightResult;
            }

            public bool TryCapture(GlobalMotionRebaseRequest request, out IGlobalMotionRebaseSnapshot snapshot, out string error)
            {
                CaptureCalls++;
                error = CaptureResult ? null : CaptureError;
                snapshot = CaptureResult ? new FakeSnapshot(SnapshotId ?? ParticipantId) : null;
                return CaptureResult;
            }

            public bool TryApply(GlobalMotionRebaseRequest request, out string error)
            {
                error = "not_implemented_in_validation_slice";
                return false;
            }

            public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
            {
                error = "not_implemented_in_validation_slice";
                return false;
            }

            public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
            {
                error = "not_implemented_in_validation_slice";
                return false;
            }

            public bool TryRestore(GlobalMotionRebaseRequest request, IGlobalMotionRebaseSnapshot snapshot, out string error)
            {
                RestoreCalls++;
                RestoreOrder = ParticipantId;
                error = RestoreResult ? null : RestoreError;
                return RestoreResult;
            }
        }

        private sealed class FakeSnapshot : IGlobalMotionRebaseSnapshot
        {
            public string ParticipantId { get; }

            public FakeSnapshot(string participantId)
            {
                ParticipantId = participantId;
            }
        }
    }
}
