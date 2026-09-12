using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the T-FO06BO protocol-owned NetworkBaseline adapter seam.
    /// The test host is an in-memory delegate; no NGO object or Unity state is accessed.
    /// </summary>
    public static class ValidateGlobalMotionNetworkBaselineNativeAdapter
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Network Baseline Native Adapter")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06BO] NetworkBaseline adapter: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06BO] NetworkBaseline adapter: {report.passed} pure checks PASS / {report.failed} FAIL; runtime host remains unbound.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            const string adapterId = "network-object:94";
            var identity = new GlobalMotionNetworkBaselineIdentity(71, 94, 3, 0, 2);
            var evidence = new GlobalMotionNetworkBaselineEvidence(
                adapterId, identity, 71, 94, 3, 2, 4, 9, 12, 120,
                true, 119, true, 11, 9, 12, true, true, true, true, true, true, true, true,
                GlobalMotionNetworkBaselineStateClassification.Restorable, true, true, true, true, true);
            Require(GlobalMotionNetworkBaselineReversibleTransactionContract.TryBuildCapability(
                evidence, true, true, true, true, true, true, true, true, true, "txn-01", out var capability, out string capabilityError), capabilityError);

            Check("Complete host capability reports native readiness", () =>
            {
                var host = new TestHost(capability);
                var adapter = new GlobalMotionNetworkBaselineNativeAdapter(adapterId, host);
                Require(adapter.Descriptor.Coverage == UnityStateRollbackCoverage.NetworkBaseline, "coverage_invalid");
                Require(adapter.Descriptor.Capabilities == GlobalMotionNativeAdapterCapability.FullTransaction, "capabilities_invalid");
                Require(adapter.Descriptor.NativeReady, "native_ready_not_reported");
            });

            Check("Missing capability keeps adapter fail closed", () =>
            {
                var adapter = new GlobalMotionNetworkBaselineNativeAdapter(adapterId, new TestHost(default));
                Require(!adapter.Descriptor.NativeReady, "missing_capability_reported_ready");
                Require(!adapter.TryValidate(CreateRequest(), out string error) && error == "network_baseline_reversible_capability_not_ready:" + adapterId, error);
            });

            Check("Participant identity is bound before delegation", () =>
            {
                var otherCapability = new GlobalMotionNetworkBaselineReversibleCapability(
                    "other", capability.TransactionId, capability.Identity, capability.BindingAuthorityGeneration,
                    capability.BindingDiscontinuityGeneration, true, true, true, true, true, true, true, true, true);
                var adapter = new GlobalMotionNetworkBaselineNativeAdapter(adapterId, new TestHost(otherCapability));
                Require(!adapter.TryValidate(CreateRequest(), out string error) && error == "network_baseline_participant_identity_mismatch:" + adapterId, error);
            });

            Check("Delegated capture preserves host evidence", () =>
            {
                var host = new TestHost(capability) { Evidence = CreateRollbackEvidence() };
                var adapter = new GlobalMotionNetworkBaselineNativeAdapter(adapterId, host);
                Require(adapter.TryCapture(CreateRequest(), out var actual, out string error), error);
                Require(actual.SnapshotId == host.Evidence.SnapshotId && host.CaptureCalls == 1, "capture_not_delegated");
            });

            Check("Rollback identity mismatch is rejected", () =>
            {
                var adapter = new GlobalMotionNetworkBaselineNativeAdapter(adapterId, new TestHost(capability));
                var request = CreateRequest();
                var wrong = new UnityStateRollbackEvidence(
                    request.TransactionId.ToString("N"), "other", "snapshot", 1, 1,
                    UnityStateRollbackCoverage.NetworkBaseline, UnityStateRollbackCoverage.NetworkBaseline,
                    UnityStateRollbackCoverage.NetworkBaseline, true, true, true, true, true, 0, 1);
                Require(!adapter.TryRestore(request, wrong, out string error) && error == "network_baseline_rollback_identity_mismatch:" + adapterId, error);
            });

            Check("Adapter does not self-register", () =>
            {
                var adapter = new GlobalMotionNetworkBaselineNativeAdapter(adapterId, new TestHost(capability));
                Require(adapter.Descriptor.AdapterId == adapterId, "adapter_identity_changed");
            });

            return report;

            GlobalMotionRebaseRequest CreateRequest()
            {
                var frame = new LocalCoordinateFrame(GlobalPosition.Zero);
                Require(OriginRebasePlan.TryCreate(frame, new UnityEngine.Vector3(10f, 0f, 0f), 1f, 1f, out var plan), "test_plan_not_created");
                return GlobalMotionRebaseRequest.Create(1, plan, 1, "digest");
            }

            UnityStateRollbackEvidence CreateRollbackEvidence()
            {
                var request = CreateRequest();
                return new UnityStateRollbackEvidence(
                    request.TransactionId.ToString("N"), adapterId, "snapshot", 1, 1,
                    UnityStateRollbackCoverage.NetworkBaseline, UnityStateRollbackCoverage.NetworkBaseline,
                    UnityStateRollbackCoverage.None, true, false, false, false, false, 0, 1);
            }
        }

        private sealed class TestHost : IGlobalMotionNetworkBaselineTransactionHost
        {
            private readonly GlobalMotionNetworkBaselineReversibleCapability _capability;
            public UnityStateRollbackEvidence Evidence;
            public int CaptureCalls { get; private set; }

            public TestHost(GlobalMotionNetworkBaselineReversibleCapability capability)
            {
                _capability = capability;
            }

            public bool TryGetCapability(out GlobalMotionNetworkBaselineReversibleCapability capability, out string error)
            {
                capability = _capability;
                error = null;
                return true;
            }

            public bool TryCapture(GlobalMotionRebaseRequest request, out UnityStateRollbackEvidence evidence, out string error)
            {
                CaptureCalls++;
                evidence = Evidence;
                error = null;
                return true;
            }

            public bool TryApply(GlobalMotionRebaseRequest request, out string error) { error = null; return true; }
            public bool TryRebuild(GlobalMotionRebaseRequest request, out string error) { error = null; return true; }
            public bool TryValidate(GlobalMotionRebaseRequest request, out string error) { error = null; return true; }
            public bool TryRestore(GlobalMotionRebaseRequest request, UnityStateRollbackEvidence evidence, out string error) { error = null; return true; }
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
