using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    public static class ValidateGlobalMotionShipDeckPassengerLifecycleBindingHandoffContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Passenger Lifecycle Binding Handoff Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CI] Passenger lifecycle binding handoff: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CI] Passenger lifecycle binding handoff: {report.passed} pure checks PASS / {report.failed} FAIL; host handoff remains explicit and dormant.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            Check("Valid binding handoff is accepted", () =>
            {
                BuildBinding(out var binding);
                Require(GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.TryValidate(
                    binding, 2, 9UL, out string error), error);
            });

            Check("Reviewed passenger count is required", () =>
            {
                BuildBinding(out var binding);
                Require(!GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.TryValidate(
                    binding, 0, 9UL, out string error) &&
                    error == "reviewed_passenger_count_required", error);
            });

            Check("Passenger count mismatch fails closed", () =>
            {
                BuildBinding(out var binding);
                Require(!GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.TryValidate(
                    binding, 1, 9UL, out string error) &&
                    error == "reviewed_passenger_count_mismatch", error);
            });

            Check("Reviewed binding generation is required", () =>
            {
                BuildBinding(out var binding);
                Require(!GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.TryValidate(
                    binding, 2, 0UL, out string error) &&
                    error == "reviewed_binding_generation_required", error);
            });

            Check("Binding generation mismatch fails closed", () =>
            {
                BuildBinding(out var binding);
                Require(!GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.TryValidate(
                    binding, 2, 10UL, out string error) &&
                    error == "reviewed_binding_generation_mismatch", error);
            });

            Check("Binding identity remains validated", () =>
            {
                var invalid = new GlobalMotionShipDeckPassengerLifecycleBindingReceipt(
                    0UL, 2UL, 9UL, 2, true, true);
                Require(!GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.TryValidate(
                    invalid, 2, 9UL, out string error) &&
                    error == "ship_network_object_id_required", error);
            });

            Check("Ownership remains server and protocol owned", () =>
            {
                var invalid = new GlobalMotionShipDeckPassengerLifecycleBindingReceipt(
                    17UL, 2UL, 9UL, 2, false, true);
                Require(!GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.TryValidate(
                    invalid, 2, 9UL, out string error) &&
                    error == "server_owned_binding_required", error);
            });

            Check("Handoff does not alter binding identity", () =>
            {
                BuildBinding(out var binding);
                Require(GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.TryValidate(
                    binding, binding.PassengerCount, binding.BindingGeneration, out string error), error);
                Require(binding.ShipNetworkObjectId == 17UL && binding.ShipSpawnGeneration == 2UL,
                    "binding_identity_changed");
            });

            return report;
        }

        private static void BuildBinding(out GlobalMotionShipDeckPassengerLifecycleBindingReceipt binding)
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
            Require(GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryBindActivePassengers(
                9UL, new[] { first, second }, out binding, out error), error);
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
