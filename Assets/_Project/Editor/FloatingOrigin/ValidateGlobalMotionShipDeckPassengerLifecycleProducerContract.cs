using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    public static class ValidateGlobalMotionShipDeckPassengerLifecycleProducerContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Passenger Lifecycle Producer Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CG] Passenger lifecycle producer contract: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CG] Passenger lifecycle producer contract: {report.passed} pure checks PASS / {report.failed} FAIL; no runtime producer bound.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            Check("Ship registration requires reviewed lifetime identity", () =>
            {
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                    17UL, 2UL, true, true, out var receipt, out string error), error);
                Require(receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered, "ship_registered_phase_missing");
                Require(receipt.Ordinal == 0, "initial_ordinal_invalid");
            });

            Check("Passenger attach creates explicit generation receipt", () =>
            {
                Register(out var current);
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                    current, "Lyra", "DeckNav", 3UL, out var next, out string error), error);
                Require(next.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached, "attached_phase_missing");
                Require(next.AttachmentGeneration == 3UL, "attachment_generation_missing");
            });

            Check("Attachment generation must be monotonic", () =>
            {
                Register(out var registered);
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                    registered, "Lyra", "DeckNav", 3UL, out var attached, out string error), error);
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryDetachPassenger(
                    attached, out var detached, out error), error);
                Require(!GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                    detached, "Lyra", "DeckNav", 3UL, out _, out error) &&
                    error == "attachment_generation_not_monotonic", error);
            });

            Check("Detach preserves reviewed passenger lineage", () =>
            {
                Register(out var registered);
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                    registered, "Lyra", "DeckNav", 3UL, out var attached, out string error), error);
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryDetachPassenger(
                    attached, out var next, out error), error);
                Require(next.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached &&
                    next.PassengerId == "Lyra" && next.AttachmentGeneration == 3UL, "detach_lineage_missing");
            });

            Check("Reattach requires a newer generation", () =>
            {
                Register(out var registered);
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                    registered, "Lyra", "DeckNav", 3UL, out var attached, out string error), error);
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryDetachPassenger(
                    attached, out var detached, out error), error);
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryAttachPassenger(
                    detached, "Lyra", "DeckNav", 4UL, out var next, out error), error);
                Require(next.AttachmentGeneration == 4UL, "reattach_generation_invalid");
            });

            Check("Ship invalidation is terminal", () =>
            {
                Register(out var current);
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryInvalidateShip(
                    current, out var next, out string error), error);
                Require(next.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated, "invalidated_phase_missing");
                Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryValidate(next, out error), error);
                Require(!GlobalMotionShipDeckPassengerLifecycleProducerContract.TryInvalidateShip(
                    next, out _, out error) && error == "ship_lifecycle_already_invalidated", error);
            });

            Check("Missing ship identity fails closed", () =>
            {
                Require(!GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                    0UL, 2UL, true, true, out _, out string error) &&
                    error == "ship_network_object_id_required", error);
            });

            Check("Ownership must remain server and protocol owned", () =>
            {
                Require(!GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                    17UL, 2UL, false, true, out _, out string error) &&
                    error == "server_owned_lifecycle_required", error);
                Require(!GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                    17UL, 2UL, true, false, out _, out error) &&
                    error == "protocol_owned_lifecycle_required", error);
            });

            return report;
        }

        private static void Register(out GlobalMotionShipDeckPassengerLifecycleReceipt receipt)
        {
            Require(GlobalMotionShipDeckPassengerLifecycleProducerContract.TryRegisterShip(
                17UL, 2UL, true, true, out receipt, out string error), error);
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
