using System;
using System.Collections.Generic;
using ProjectC.AI;
using ProjectC.Ship;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the dormant T-FO06CB combined ShipDeckNav/passenger host.
    /// No server lifecycle, NavMesh registration, scene mutation or Play Mode is executed.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckCombinedTransactionHost
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Combined ShipDeck Transaction Host")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CB] Combined host: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            Debug.Log($"[T-FO06CB] Combined host: {report.passed} pure checks PASS / {report.failed} FAIL; runtime remains fail-closed.");
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
                Check("Host binds required ShipDeckNav", () =>
                {
                    gameObject = new GameObject("T-FO06CB_TestCombinedHost");
                    var host = gameObject.AddComponent<GlobalMotionShipDeckCombinedTransactionHost>();
                    Require(host != null && host.IsBound, "combined_host_not_bound");
                    Require(gameObject.GetComponent<ShipDeckNav>() != null, "ship_deck_nav_dependency_missing");
                });

                Check("Empty reviewed passengers remain rejected", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionShipDeckCombinedTransactionHost>();
                    Require(!host.TryConfigureReviewedPassengers(Array.Empty<NpcBrain>(), out string error) &&
                        error == "passenger_reviewed_sources_required", error);
                });

                Check("Null reviewed passenger remains rejected", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionShipDeckCombinedTransactionHost>();
                    Require(!host.TryConfigureReviewedPassengers(new NpcBrain[] { null }, out string error) &&
                        error == "passenger_reviewed_source_missing:index=0", error);
                });

                Check("Capture validates transaction identity before runtime authority", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionShipDeckCombinedTransactionHost>();
                    Require(!host.TryCapture(" ", 1UL, out _, out string error) &&
                        error == "transaction_id_required", error);
                });

                Check("Null snapshot validation remains fail closed", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionShipDeckCombinedTransactionHost>();
                    Require(!host.TryValidate(null, out string error) &&
                        error == "combined_snapshot_missing", error);
                });

                Check("Null snapshot restore remains fail closed", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionShipDeckCombinedTransactionHost>();
                    Require(!host.TryRestore(null, out string error) &&
                        error == "combined_snapshot_missing", error);
                });

                Check("Transaction result requires lineage", () =>
                {
                    Require(!GlobalMotionShipDeckCombinedTransactionContract.TryValidate(default, out string error) &&
                        error == "transaction_id_required", error);
                });
            }
            finally
            {
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }

            return report;
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
