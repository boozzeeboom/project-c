using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    public static class ValidateGlobalMotionShipDeckPassengerLifecycleSourceBindingContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Passenger Lifecycle Source Binding Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CH] Passenger lifecycle source binding: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CH] Passenger lifecycle source binding: {report.passed} pure checks PASS / {report.failed} FAIL; runtime binding remains unexecuted.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            Check("Active binding requires explicit binding generation", () =>
            {
                BuildAttachedReceipts(out var receipts);
                Require(GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryBindActivePassengers(
                    9UL, receipts, out var binding, out string error), error);
                Require(binding.BindingGeneration == 9UL && binding.PassengerCount == 2, "binding_identity_invalid");
            });

            Check("Active binding accepts only attached receipts", () =>
            {
                GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                    17UL, 2UL, true, true, out var registered, out _);
                GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                    registered, "Lyra", "DeckNav", 3UL, out var attached, out _);
                GlobalMotionShipDeckPassengerLifecycleProducerContract.TryDetachPassenger(
                    attached, out var detached, out _);
                Require(!GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryBindActivePassengers(
                    9UL, new[] { detached }, out _, out string error) &&
                    error == "active_passenger_receipt_required:index=0", error);
            });

            Check("Different ship lifetime generations are rejected", () =>
            {
                BuildAttachedReceipts(out var receipts);
                GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                    18UL, 4UL, true, true, out var registered, out _);
                GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                    registered, "Kael", "DeckNav", 5UL, out var other, out _);
                receipts[1] = other;
                Require(!GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryBindActivePassengers(
                    9UL, receipts, out _, out string error) &&
                    error == "ship_lifetime_mismatch:index=1", error);
            });

            Check("Duplicate passenger identity is rejected", () =>
            {
                BuildAttachedReceipts(out var receipts);
                receipts[1] = receipts[0];
                Require(!GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryBindActivePassengers(
                    9UL, receipts, out _, out string error) &&
                    error == "duplicate_passenger_id:index=1", error);
            });

            Check("Missing receipts fail closed", () =>
            {
                Require(!GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryBindActivePassengers(
                    9UL, null, out _, out string error) &&
                    error == "active_passenger_receipts_required", error);
            });

            Check("Missing binding generation fails closed", () =>
            {
                BuildAttachedReceipts(out var receipts);
                Require(!GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryBindActivePassengers(
                    0UL, receipts, out _, out string error) &&
                    error == "binding_generation_required", error);
            });

            Check("Binding receipt validation preserves ownership", () =>
            {
                BuildAttachedReceipts(out var receipts);
                GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryBindActivePassengers(
                    9UL, receipts, out var binding, out string error);
                Require(GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryValidate(binding, out error), error);
            });

            Check("Invalid binding ownership fails closed", () =>
            {
                var invalid = new GlobalMotionShipDeckPassengerLifecycleBindingReceipt(
                    17UL, 2UL, 9UL, 1, false, true);
                Require(!GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryValidate(
                    invalid, out string error) &&
                    error == "server_owned_binding_required", error);
            });

            return report;
        }

        private static void BuildAttachedReceipts(out GlobalMotionShipDeckPassengerLifecycleReceipt[] receipts)
        {
            GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                17UL, 2UL, true, true, out var registered, out string error);
            Require(error == null, error);
            GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                registered, "Lyra", "DeckNav", 3UL, out var first, out error);
            Require(error == null, error);
            GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                registered, "Bram", "DeckNav", 4UL, out var second, out error);
            Require(error == null, error);
            receipts = new[] { first, second };
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
