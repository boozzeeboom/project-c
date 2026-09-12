using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure validation for the reviewed passenger attachment/lifetime generation contract.
    /// No NetworkObject, scene, server lifecycle or automatic discovery is used.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckPassengerGenerationContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Passenger Generation Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CD] Passenger generation contract: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            Debug.Log($"[T-FO06CD] Passenger generation contract: {report.passed} pure checks PASS / {report.failed} FAIL; source remains unbound.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            Check("Missing passenger identity is rejected", () =>
            {
                Require(!GlobalMotionShipDeckPassengerGenerationContract.TryValidate(default, out string error) &&
                    error == "passenger_id_required", error);
            });

            Check("Missing ship lifetime generation is rejected", () =>
            {
                var generation = new GlobalMotionShipDeckPassengerGeneration(
                    "passenger", 17UL, 0UL, 1UL, "DeckNav", true, true);
                Require(!GlobalMotionShipDeckPassengerGenerationContract.TryValidate(generation, out string error) &&
                    error == "ship_spawn_generation_required", error);
            });

            Check("Missing attachment generation is rejected", () =>
            {
                var generation = new GlobalMotionShipDeckPassengerGeneration(
                    "passenger", 17UL, 2UL, 0UL, "DeckNav", true, true);
                Require(!GlobalMotionShipDeckPassengerGenerationContract.TryValidate(generation, out string error) &&
                    error == "attachment_generation_required", error);
            });

            Check("Unreviewed ownership is rejected", () =>
            {
                var generation = new GlobalMotionShipDeckPassengerGeneration(
                    "passenger", 17UL, 2UL, 3UL, "DeckNav", false, true);
                Require(!GlobalMotionShipDeckPassengerGenerationContract.TryValidate(generation, out string error) &&
                    error == "server_owned_generation_required", error);
            });

            Check("Complete reviewed generation is accepted", () =>
            {
                var generation = new GlobalMotionShipDeckPassengerGeneration(
                    "passenger", 17UL, 2UL, 3UL, "DeckNav", true, true);
                Require(GlobalMotionShipDeckPassengerGenerationContract.TryValidate(generation, out string error), error);
            });

            return report;
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
