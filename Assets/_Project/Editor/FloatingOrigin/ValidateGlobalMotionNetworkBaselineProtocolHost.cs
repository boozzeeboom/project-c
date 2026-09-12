using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the first concrete NetworkBaseline protocol host.
    /// It verifies fail-closed behavior without starting NGO or modifying project assets.
    /// </summary>
    public static class ValidateGlobalMotionNetworkBaselineProtocolHost
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Network Baseline Protocol Host")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06BT] NetworkBaseline protocol host: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            Debug.Log($"[T-FO06BT] NetworkBaseline protocol host: {report.passed} pure checks PASS / {report.failed} FAIL; host is concrete but fail-closed.");
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
                Check("Concrete host component exists", () =>
                {
                    gameObject = new GameObject("T-FO06BT_TestHost");
                    var host = gameObject.AddComponent<GlobalMotionNetworkBaselineProtocolHost>();
                    Require(host != null, "host_component_missing");
                    Require(host.IsBound, "host_not_bound_to_replicator_component");
                    Require(!host.IsRestorable, "incomplete_host_reported_restorable");
                });

                Check("Unspawned host keeps capability fail closed", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionNetworkBaselineProtocolHost>();
                    Require(!host.TryGetCapability(out _, out string error) &&
                        error == "network_baseline_accepted_server_state_unavailable", error);
                });

                Check("Invalid request cannot be captured", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionNetworkBaselineProtocolHost>();
                    Require(!host.TryCapture(default, out _, out string error) && error == "transaction_id_required", error);
                });

                Check("Apply remains fail closed", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionNetworkBaselineProtocolHost>();
                    Require(!host.TryApply(CreateRequest(), out string error) &&
                        error == "network_baseline_protocol_host_apply_not_reviewed", error);
                });

                Check("Rebuild remains fail closed", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionNetworkBaselineProtocolHost>();
                    Require(!host.TryRebuild(CreateRequest(), out string error) &&
                        error == "network_baseline_protocol_host_rebuild_not_reviewed", error);
                });

                Check("Validation requires capture identity", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionNetworkBaselineProtocolHost>();
                    Require(!host.TryValidate(CreateRequest(), out string error) &&
                        error == "network_baseline_capture_required", error);
                });

                Check("Restore remains fail closed", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionNetworkBaselineProtocolHost>();
                    Require(!host.TryRestore(CreateRequest(), default, out string error) &&
                        error == "network_baseline_capture_identity_mismatch", error);
                });

                Check("Host does not self-register", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionNetworkBaselineProtocolHost>();
                    Require(host.IsBound && !host.IsRestorable, "host_registration_or_readiness_changed");
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
