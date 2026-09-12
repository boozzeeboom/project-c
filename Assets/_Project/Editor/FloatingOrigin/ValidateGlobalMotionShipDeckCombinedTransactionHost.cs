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
            GameObject unboundHostObject = null;
            GameObject passengerA = null;
            GameObject passengerB = null;
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

                Check("Reviewed lifecycle binding hands off explicitly", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionShipDeckCombinedTransactionHost>();
                    passengerA = new GameObject("T-FO06CB_TestPassengerA");
                    passengerB = new GameObject("T-FO06CB_TestPassengerB");
                    var firstPassenger = passengerA.AddComponent<NpcBrain>();
                    var secondPassenger = passengerB.AddComponent<NpcBrain>();
                    Require(host.TryConfigureReviewedPassengers(
                        new[] { firstPassenger, secondPassenger }, out string error), error);
                    GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                        17UL, 2UL, true, true, out var registered, out error);
                    Require(error == null, error);
                    GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                        registered, "Lyra", "DeckNav", 3UL, out var firstReceipt, out error);
                    Require(error == null, error);
                    GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                        registered, "Bram", "DeckNav", 4UL, out var secondReceipt, out error);
                    Require(error == null, error);
                    Require(host.TryConfigureReviewedPassengerLifecycleBinding(
                        host.ReviewedPassengerBindingGeneration,
                        new[] { firstReceipt, secondReceipt }, out error), error);
                    Require(host.HasReviewedPassengerLifecycleBinding, "lifecycle_binding_not_stored");
                    Require(host.TryValidateReviewedPassengerLifecycleBinding(out error), error);

                    var snapshot = new GlobalMotionShipDeckCombinedSnapshot(
                        "T-FO06CJ_snapshot",
                        1UL,
                        host.ReviewedPassengerBindingGeneration,
                        new GlobalMotionShipDeckPassengerLifecycleBindingReceipt(
                            17UL, 2UL, host.ReviewedPassengerBindingGeneration, 2, true, true),
                        new ShipDeckNavFloatingOriginSnapshot(
                            "T-FO06CJ_snapshot", false, Vector3.zero, Vector3.zero, false, 1UL),
                        Array.Empty<GlobalMotionNpcShipDeckSnapshot>());
                    Require(snapshot.LifecycleBinding.ShipNetworkObjectId == 17UL &&
                        snapshot.LifecycleBinding.ShipSpawnGeneration == 2UL &&
                        snapshot.LifecycleBinding.BindingGeneration == host.ReviewedPassengerBindingGeneration &&
                        snapshot.LifecycleBinding.PassengerCount == 2 &&
                        snapshot.LifecycleBinding.ServerOwned &&
                        snapshot.LifecycleBinding.ProtocolOwned,
                        "snapshot_lifecycle_binding_identity_not_carried");
                });

                Check("Capture requires reviewed lifecycle binding", () =>
                {
                    unboundHostObject = new GameObject("T-FO06CJ_TestUnboundHost");
                    var unboundHost = unboundHostObject.AddComponent<GlobalMotionShipDeckCombinedTransactionHost>();
                    var reviewedPassengers = new[]
                    {
                        passengerA.GetComponent<NpcBrain>(),
                        passengerB.GetComponent<NpcBrain>()
                    };
                    Require(unboundHost.TryConfigureReviewedPassengers(reviewedPassengers, out string configureError), configureError);
                    Require(!unboundHost.TryCapture("T-FO06CJ_capture", 1UL, out _, out string error) &&
                        error == "reviewed_lifecycle_binding_required", error);
                });

                Check("Lifecycle binding generation mismatch remains rejected", () =>
                {
                    var host = gameObject.GetComponent<GlobalMotionShipDeckCombinedTransactionHost>();
                    GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                        17UL, 2UL, true, true, out var registered, out string error);
                    Require(error == null, error);
                    GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                        registered, "Lyra", "DeckNav", 3UL, out var firstReceipt, out error);
                    Require(error == null, error);
                    GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                        registered, "Bram", "DeckNav", 4UL, out var secondReceipt, out error);
                    Require(error == null, error);
                    Require(!host.TryConfigureReviewedPassengerLifecycleBinding(
                        host.ReviewedPassengerBindingGeneration + 1UL,
                        new[] { firstReceipt, secondReceipt }, out error) &&
                        error == "reviewed_binding_generation_mismatch", error);
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
                if (passengerA != null)
                    UnityEngine.Object.DestroyImmediate(passengerA);
                if (passengerB != null)
                    UnityEngine.Object.DestroyImmediate(passengerB);
                if (unboundHostObject != null)
                    UnityEngine.Object.DestroyImmediate(unboundHostObject);
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