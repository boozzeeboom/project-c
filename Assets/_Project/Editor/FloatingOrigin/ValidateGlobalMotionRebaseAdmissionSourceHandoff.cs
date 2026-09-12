using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.Core;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the typed source handoff into admission evidence.
    /// It proves binding only; admission remains blocked by incomplete native readiness/evidence.
    /// </summary>
    public static class ValidateGlobalMotionRebaseAdmissionSourceHandoff
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Admission Source Handoff")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06BW] Admission source handoff: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            Debug.Log($"[T-FO06BW] Admission source handoff: {report.passed} pure checks PASS / {report.failed} FAIL; admission remains fail-closed.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            GameObject root = null;
            GameObject cameraObject = null;
            GameObject shipAdapterObject = null;
            try
            {
                root = new GameObject("T-FO06BW_AdmissionHandoff");
                var provider = root.AddComponent<GlobalMotionRebaseAdmissionEvidenceProvider>();
                var nativeEvidenceSource = root.AddComponent<GlobalMotionRebaseNativeAdapterEvidenceSource>();
                var networkAdapterSource = root.AddComponent<GlobalMotionNetworkBaselineNativeAdapterSource>();
                var rigidbody = root.AddComponent<Rigidbody>();
                cameraObject = new GameObject("T-FO06BW_Camera");
                var camera = cameraObject.AddComponent<SpringArmCamera>();
                shipAdapterObject = new GameObject("T-FO06BW_ShipDeckNavAdapter");
                var shipAdapter = shipAdapterObject.AddComponent<StubNativeAdapter>();
                var reviewedSource = root.AddComponent<StubReviewedAdmissionSource>();
                var proofSource = root.AddComponent<StubRuntimeProofSource>();
                var rollbackSource = root.AddComponent<StubRollbackSource>();

                Check("Concrete sources bind to typed components", () =>
                {
                    Require(nativeEvidenceSource.TryConfigureSources(
                        root.transform,
                        rigidbody,
                        camera,
                        "PLAYER_FRAME",
                        shipAdapter,
                        networkAdapterSource,
                        out string error), error);
                    Require(nativeEvidenceSource.TryValidateSourceBindings(out error), error);
                });

                Check("Provider accepts all four typed source contracts", () =>
                {
                    Require(provider.TryConfigureSources(
                        reviewedSource,
                        nativeEvidenceSource,
                        proofSource,
                        rollbackSource,
                        out string error), error);
                    Require(provider.TryValidateSourceBindings(out error), error);
                });

                Check("Provider does not synthesize admission evidence", () =>
                {
                    Require(!provider.TryGetAdmissionEvidence(out _, out string error) &&
                        error == "reviewed_evidence_not_submitted", error);
                });

                Check("Native adapter readiness remains blocked", () =>
                {
                    Require(!nativeEvidenceSource.TryGetNativeAdapterSet(out _, out string error) &&
                        !string.IsNullOrWhiteSpace(error), "native_adapter_readiness_opened");
                });

                Check("NetworkBaseline source remains non-restorable", () =>
                {
                    var descriptor = networkAdapterSource.Descriptor;
                    Require(descriptor.IsValid && !descriptor.NativeReady, "network_baseline_native_ready");
                });

                Check("Handoff does not self-register or alter runtime", () =>
                {
                    Require(root.GetComponent<GlobalMotionRebaseLiveManifestRuntimeBridge>() == null,
                        "handoff_created_manifest_bridge");
                    Require(!provider.TryGetAdmissionEvidence(out _, out string error) &&
                        error == "reviewed_evidence_not_submitted", error);
                });
            }
            finally
            {
                if (shipAdapterObject != null) UnityEngine.Object.DestroyImmediate(shipAdapterObject);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }

            return report;
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error ?? "assertion_failed");
        }

        private sealed class StubNativeAdapter : MonoBehaviour, IGlobalMotionNativeAdapter
        {
            public GlobalMotionNativeAdapterDescriptor Descriptor => new GlobalMotionNativeAdapterDescriptor(
                "ship-deck-nav:handoff-test",
                UnityStateRollbackCoverage.ShipDeckNav,
                GlobalMotionNativeAdapterCapability.FullTransaction,
                true,
                true);

            public bool TryCapture(GlobalMotionRebaseRequest request, out UnityStateRollbackEvidence evidence, out string error)
            {
                evidence = default;
                error = "stub_not_runtime_bound";
                return false;
            }

            public bool TryApply(GlobalMotionRebaseRequest request, out string error) { error = "stub_not_runtime_bound"; return false; }
            public bool TryRebuild(GlobalMotionRebaseRequest request, out string error) { error = "stub_not_runtime_bound"; return false; }
            public bool TryValidate(GlobalMotionRebaseRequest request, out string error) { error = "stub_not_runtime_bound"; return false; }
            public bool TryRestore(GlobalMotionRebaseRequest request, UnityStateRollbackEvidence evidence, out string error) { error = "stub_not_runtime_bound"; return false; }
        }

        private sealed class StubReviewedAdmissionSource : MonoBehaviour, IGlobalMotionRebaseReviewedAdmissionEvidenceSource
        {
            public bool TryGetReviewedAdmissionEvidence(out GlobalMotionRebaseParticipantAdmissionEvidence evidence, out string error)
            {
                evidence = default;
                error = "reviewed_evidence_not_submitted";
                return false;
            }
        }

        private sealed class StubRuntimeProofSource : MonoBehaviour, IGlobalMotionRebaseRuntimeProofEvidenceSource
        {
            public bool TryGetRuntimeProof(out GlobalMotionRebaseRuntimeProofEvidence evidence, out string error)
            {
                evidence = default;
                error = "runtime_proof_not_submitted";
                return false;
            }
        }

        private sealed class StubRollbackSource : MonoBehaviour, IGlobalMotionRebaseRollbackEvidenceSource
        {
            public bool TryGetRollbackEvidence(out UnityStateRollbackEvidence evidence, out string error)
            {
                evidence = default;
                error = "rollback_evidence_not_submitted";
                return false;
            }
        }
    }
}
