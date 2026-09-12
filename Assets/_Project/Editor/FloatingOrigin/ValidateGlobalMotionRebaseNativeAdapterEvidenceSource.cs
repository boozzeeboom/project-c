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
    /// Pure Edit Mode validation for explicit native adapter source binding.
    /// It validates binding API behavior without registering adapters or invoking runtime state.
    /// </summary>
    public static class ValidateGlobalMotionRebaseNativeAdapterEvidenceSource
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Native Adapter Evidence Source Binding")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06BV] Native adapter evidence source binding: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            Debug.Log($"[T-FO06BV] Native adapter evidence source binding: {report.passed} pure checks PASS / {report.failed} FAIL; source remains dormant.");
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
                Check("Source component exists and starts unbound", () =>
                {
                    root = new GameObject("T-FO06BV_TestEvidenceSource");
                    var source = root.AddComponent<GlobalMotionRebaseNativeAdapterEvidenceSource>();
                    Require(source != null, "evidence_source_missing");
                    Require(!source.TryValidateSourceBindings(out string error) && error == "transform_target_missing", error);
                });

                Check("Binding rejects incomplete target set", () =>
                {
                    var source = root.GetComponent<GlobalMotionRebaseNativeAdapterEvidenceSource>();
                    Require(!source.TryConfigureSources(
                        root.transform,
                        null,
                        null,
                        "PLAYER_FRAME",
                        null,
                        null,
                        out string error) && error == "rigidbody_target_missing", error);
                });

                Check("Binding rejects missing ShipDeckNav source", () =>
                {
                    var source = root.GetComponent<GlobalMotionRebaseNativeAdapterEvidenceSource>();
                    var rigidbody = root.AddComponent<Rigidbody>();
                    cameraObject = new GameObject("T-FO06BV_Camera");
                    var camera = cameraObject.AddComponent<SpringArmCamera>();
                    Require(!source.TryConfigureSources(
                        root.transform,
                        rigidbody,
                        camera,
                        "PLAYER_FRAME",
                        null,
                        null,
                        out string error) && error == "ship_deck_nav_native_adapter_source_missing", error);
                });

                Check("Explicit binding succeeds for typed sources", () =>
                {
                    var source = root.GetComponent<GlobalMotionRebaseNativeAdapterEvidenceSource>();
                    shipAdapterObject = new GameObject("T-FO06BV_ShipDeckNavAdapter");
                    var shipAdapter = shipAdapterObject.AddComponent<StubNativeAdapter>();
                    var networkAdapter = root.AddComponent<GlobalMotionNetworkBaselineNativeAdapterSource>();
                    Require(source.TryConfigureSources(
                        root.transform,
                        root.GetComponent<Rigidbody>(),
                        cameraObject.GetComponent<SpringArmCamera>(),
                        "PLAYER_FRAME",
                        shipAdapter,
                        networkAdapter,
                        out string error), error);
                    Require(source.TryValidateSourceBindings(out error), error);
                });

                Check("Incomplete readiness remains fail closed", () =>
                {
                    var source = root.GetComponent<GlobalMotionRebaseNativeAdapterEvidenceSource>();
                    Require(!source.TryGetNativeAdapterSet(out _, out string error) &&
                        !string.IsNullOrWhiteSpace(error), "partial_adapter_set_accepted");
                });

                Check("Binding does not self-register", () =>
                {
                    var source = root.GetComponent<GlobalMotionRebaseNativeAdapterEvidenceSource>();
                    Require(source.TryValidateSourceBindings(out string error), error);
                    Require(root.GetComponent<GlobalMotionRebaseAdmissionEvidenceProvider>() == null,
                        "binding_created_provider");
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
                "ship-deck-nav:test",
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

            public bool TryApply(GlobalMotionRebaseRequest request, out string error)
            {
                error = "stub_not_runtime_bound";
                return false;
            }

            public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
            {
                error = "stub_not_runtime_bound";
                return false;
            }

            public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
            {
                error = "stub_not_runtime_bound";
                return false;
            }

            public bool TryRestore(GlobalMotionRebaseRequest request, UnityStateRollbackEvidence evidence, out string error)
            {
                error = "stub_not_runtime_bound";
                return false;
            }
        }
    }
}
