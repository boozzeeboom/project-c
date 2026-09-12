using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the concrete NetworkBaseline adapter binding seam.
    /// It verifies component binding and fail-closed descriptor/operation behavior only.
    /// </summary>
    public static class ValidateGlobalMotionNetworkBaselineNativeAdapterSource
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Network Baseline Native Adapter Source")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06BU] NetworkBaseline adapter source: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            Debug.Log($"[T-FO06BU] NetworkBaseline adapter source: {report.passed} pure checks PASS / {report.failed} FAIL; binding seam remains fail-closed.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            GameObject gameObject = null;
            try
            {
                Check("Concrete source binds protocol host", () =>
                {
                    gameObject = new GameObject("T-FO06BU_TestAdapterSource");
                    var source = gameObject.AddComponent<GlobalMotionNetworkBaselineNativeAdapterSource>();
                    Require(source != null, "adapter_source_missing");
                    Require(source.IsBound, "adapter_source_not_bound_to_host");
                    Require(gameObject.GetComponent<GlobalMotionNetworkBaselineProtocolHost>() != null, "protocol_host_dependency_missing");
                    Require(gameObject.GetComponent<GlobalMotionReplicator>() != null, "replicator_dependency_missing");
                });

                Check("Descriptor declares only dormant NetworkBaseline coverage", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionNetworkBaselineNativeAdapterSource>();
                    var descriptor = source.Descriptor;
                    Require(descriptor.IsValid, "adapter_descriptor_invalid");
                    Require(descriptor.Coverage == UnityStateRollbackCoverage.NetworkBaseline, "adapter_coverage_invalid");
                    Require(descriptor.Capabilities == GlobalMotionNativeAdapterCapability.FullTransaction, "adapter_capabilities_invalid");
                    Require(descriptor.IsCurrent, "adapter_not_current");
                    Require(!descriptor.NativeReady, "adapter_reported_native_ready");
                });

                Check("Invalid request remains fail closed", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionNetworkBaselineNativeAdapterSource>();
                    Require(!source.TryCapture(default, out _, out string error) && error == "rebase_request_invalid", error);
                });

                Check("Apply remains blocked by host capability", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionNetworkBaselineNativeAdapterSource>();
                    Require(!source.TryApply(CreateRequest(), out string error) &&
                        error == "network_baseline_accepted_server_state_unavailable", error);
                });

                Check("Rebuild remains blocked by host capability", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionNetworkBaselineNativeAdapterSource>();
                    Require(!source.TryRebuild(CreateRequest(), out string error) &&
                        error == "network_baseline_accepted_server_state_unavailable", error);
                });

                Check("Source does not self-register or open readiness", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionNetworkBaselineNativeAdapterSource>();
                    Require(source.IsBound && !source.Descriptor.NativeReady, "adapter_source_opened_readiness");
                });
            }
            finally
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            }

            return report;

            GlobalMotionRebaseRequest CreateRequest()
            {
                var frame = new LocalCoordinateFrame(GlobalPosition.Zero);
                Require(OriginRebasePlan.TryCreate(frame, new Vector3(10f, 0f, 0f), 1f, 1f, out var plan), "test_plan_not_created");
                return GlobalMotionRebaseRequest.Create(1, plan, 1, "digest");
            }
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
