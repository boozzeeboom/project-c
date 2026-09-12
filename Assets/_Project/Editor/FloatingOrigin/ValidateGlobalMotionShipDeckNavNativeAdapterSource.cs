using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;
using ProjectC.Ship;
using ProjectC.AI;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the dormant ShipDeckNav adapter source.
    /// It verifies host binding and fail-closed readiness without registering NavMesh data.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckNavNativeAdapterSource
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeckNav Native Adapter Source")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06BX] ShipDeckNav adapter source: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            Debug.Log($"[T-FO06BX] ShipDeckNav adapter source: {report.passed} pure checks PASS / {report.failed} FAIL; lifecycle remains fail-closed.");
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
                    gameObject = new GameObject("T-FO06BX_TestShipDeckNavAdapterSource");
                    var source = gameObject.AddComponent<GlobalMotionShipDeckNavNativeAdapterSource>();
                    Require(source != null, "adapter_source_missing");
                    Require(source.IsBound, "adapter_source_not_bound_to_host");
                    Require(gameObject.GetComponent<GlobalMotionShipDeckNavProtocolHost>() != null, "protocol_host_dependency_missing");
                    Require(gameObject.GetComponent<ShipDeckNav>() != null, "ship_deck_nav_dependency_missing");
                });

                Check("Descriptor declares only dormant ShipDeckNav coverage", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionShipDeckNavNativeAdapterSource>();
                    var descriptor = source.Descriptor;
                    Require(descriptor.IsValid, "adapter_descriptor_invalid");
                    Require(descriptor.Coverage == UnityStateRollbackCoverage.ShipDeckNav, "adapter_coverage_invalid");
                    Require(descriptor.Capabilities == GlobalMotionNativeAdapterCapability.FullTransaction, "adapter_capabilities_invalid");
                    Require(descriptor.IsCurrent, "adapter_not_current");
                    Require(!descriptor.NativeReady, "adapter_reported_native_ready");
                });

                Check("Reviewed passenger binding remains explicit", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionShipDeckNavProtocolHost>();
                    Require(!host.TryConfigureReviewedPassengers(Array.Empty<NpcBrain>(), out string error) &&
                        error == "passenger_reviewed_sources_required", error);
                });

                Check("ShipDeckNav snapshot API validates transaction identity", () =>
                {
                    var deck = gameObject.GetComponent<ShipDeckNav>();
                    Require(!deck.TryCaptureFloatingOriginSnapshot(" ", out _, out string error) &&
                        error == "transaction_id_required", error);
                });

                Check("Invalid request remains fail closed", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionShipDeckNavNativeAdapterSource>();
                    Require(!source.TryCapture(default, out _, out string error) && error == "rebase_request_invalid", error);
                });

                Check("Capture remains blocked by lifecycle readiness", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionShipDeckNavNativeAdapterSource>();
                    Require(!source.TryCapture(CreateRequest(), out _, out string error) &&
                        error.StartsWith("ship_deck_nav_lifecycle_not_ready:", StringComparison.Ordinal), error);
                });

                Check("Rebuild remains blocked by lifecycle readiness", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionShipDeckNavNativeAdapterSource>();
                    Require(!source.TryRebuild(CreateRequest(), out string error) &&
                        error.StartsWith("ship_deck_nav_lifecycle_not_ready:", StringComparison.Ordinal), error);
                });

                Check("Source does not self-register or open readiness", () =>
                {
                    var source = gameObject.GetComponent<GlobalMotionShipDeckNavNativeAdapterSource>();
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