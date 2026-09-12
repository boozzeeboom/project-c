using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the dormant T-FO06CN protocol-owned lifecycle coordinator.
    /// No Unity object, NGO callback, scene, provider, adapter or runtime binding is used.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckPassengerLifecycleCoordinator
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Passenger Lifecycle Coordinator")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CN] Passenger lifecycle coordinator: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            Debug.Log($"[T-FO06CN] Passenger lifecycle coordinator: {report.passed} pure checks PASS / {report.failed} FAIL; coordinator remains dormant and unbound.");
        }

        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stable Edit Mode required.");

            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            Check("Ship registration allocates a coordinator-owned lifetime generation", () =>
            {
                var coordinator = new GlobalMotionShipDeckPassengerLifecycleCoordinator();
                Require(coordinator.TryAccept(Registered("ship-a", 17UL), out var receipt, out string error), error);
                Require(receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered &&
                    receipt.ShipLifetimeGeneration == 1UL && receipt.LedgerOrdinal == 1UL, "registration_lineage_invalid");
                Require(GlobalMotionShipDeckPassengerLifecycleCoordinator.TryValidateReceipt(receipt, out error), error);
            });

            Check("Attach issues an explicit passenger generation and source view", () =>
            {
                var coordinator = Register("ship-a", 17UL, out var registered);
                Require(coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL), out var attached, out string error), error);
                Require(attached.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached &&
                    attached.AttachmentGeneration == 1UL && attached.LedgerOrdinal == 2UL, "attach_lineage_invalid");
                Require(coordinator.TryGetGeneration("Lyra", out var generation, out error), error);
                Require(generation.ShipSpawnGeneration == registered.ShipLifetimeGeneration &&
                    generation.AttachmentGeneration == attached.AttachmentGeneration, "source_generation_mismatch");
            });

            Check("Detach requires and closes the exact active attachment generation", () =>
            {
                var coordinator = Register("ship-a", 17UL, out var registered);
                Require(coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL), out var attached, out string error), error);
                Require(coordinator.TryAccept(Detached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", attached.AttachmentGeneration), out var detached, out error), error);
                Require(detached.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached &&
                    detached.AttachmentGeneration == attached.AttachmentGeneration, "detach_lineage_invalid");
                Require(!coordinator.TryGetGeneration("Lyra", out _, out error) && error == "passenger_not_active", error);
            });

            Check("Reattach receives a strictly newer never-reused attachment generation", () =>
            {
                var coordinator = Register("ship-a", 17UL, out var registered);
                Require(coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL), out var first, out string error), error);
                Require(coordinator.TryAccept(Detached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", first.AttachmentGeneration), out _, out error), error);
                Require(coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", first.AttachmentGeneration), out var second, out error), error);
                Require(second.AttachmentGeneration > first.AttachmentGeneration, "attachment_generation_not_monotonic");
            });

            Check("Duplicate active attachment is rejected", () =>
            {
                var coordinator = Register("ship-a", 17UL, out var registered);
                Require(coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL), out _, out string error), error);
                Require(!coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL), out _, out error) &&
                    error == "duplicate_active_passenger_attachment", error);
            });

            Check("Stale, mismatched and out-of-order facts fail closed", () =>
            {
                var coordinator = Register("ship-a", 17UL, out var registered);
                Require(!coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration + 1UL, "Lyra", 0UL), out _, out string error) &&
                    error == "stale_ship_lifetime_generation", error);
                Require(!coordinator.TryAccept(Detached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 1UL), out _, out error) &&
                    error == "passenger_not_active", error);
                Require(!coordinator.TryAccept(Attached("ship-a", 18UL, registered.ShipLifetimeGeneration, "Lyra", 0UL), out _, out error) &&
                    error == "ship_network_identity_mismatch", error);
            });

            Check("Ship lifetime generations increase after terminal invalidation", () =>
            {
                var coordinator = Register("ship-a", 17UL, out var first);
                Require(coordinator.TryAccept(Invalidated("ship-a", 17UL, first.ShipLifetimeGeneration, "despawn"), out var invalidated, out string error), error);
                Require(!coordinator.TryAccept(Attached("ship-a", 17UL, first.ShipLifetimeGeneration, "Lyra", 0UL), out _, out error) &&
                    error == "ship_lifecycle_invalidated", error);
                Require(coordinator.TryAccept(Registered("ship-a", 17UL), out var second, out error), error);
                Require(second.ShipLifetimeGeneration > first.ShipLifetimeGeneration &&
                    invalidated.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated, "ship_generation_not_monotonic");
            });

            Check("Duplicate registration while active is rejected", () =>
            {
                var coordinator = new GlobalMotionShipDeckPassengerLifecycleCoordinator();
                Require(coordinator.TryAccept(Registered("ship-a", 17UL), out _, out string error), error);
                Require(!coordinator.TryAccept(Registered("ship-a", 17UL), out _, out error) &&
                    error == "duplicate_ship_registration", error);
            });

            Check("Invalidation is terminal and requires a reason", () =>
            {
                var coordinator = Register("ship-a", 17UL, out var registered);
                Require(!coordinator.TryAccept(Invalidated("ship-a", 17UL, registered.ShipLifetimeGeneration, ""), out _, out string error) &&
                    error == "invalidation_reason_required", error);
                Require(coordinator.TryAccept(Invalidated("ship-a", 17UL, registered.ShipLifetimeGeneration, "despawn"), out _, out error), error);
                Require(!coordinator.TryAccept(Invalidated("ship-a", 17UL, registered.ShipLifetimeGeneration, "duplicate"), out _, out error) &&
                    error == "ship_lifecycle_invalidated", error);
            });

            Check("Only explicit server-authorized protocol facts are accepted", () =>
            {
                var coordinator = new GlobalMotionShipDeckPassengerLifecycleCoordinator();
                Require(!coordinator.TryAccept(Registered("ship-a", 17UL, false, true), out _, out string error) &&
                    error == "server_authorization_required", error);
                Require(!coordinator.TryAccept(Registered("ship-a", 17UL, true, false), out _, out error) &&
                    error == "protocol_ownership_required", error);
            });

            Check("Stable identities and coordinator-owned registration generation are required", () =>
            {
                var invalidIdentity = new GlobalMotionShipDeckPassengerLifecycleFact(
                    GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered,
                    " ship-a", 17UL, 0UL, null, 0UL, null, null, true, true);
                Require(!coordinatorTryAccept(invalidIdentity, out string error) && error == "ship_id_required", error);

                var suppliedGeneration = new GlobalMotionShipDeckPassengerLifecycleFact(
                    GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered,
                    "ship-a", 17UL, 99UL, null, 0UL, null, null, true, true);
                Require(!coordinatorTryAccept(suppliedGeneration, out error) &&
                    error == "ship_registration_generation_must_be_coordinator_owned", error);

                bool coordinatorTryAccept(GlobalMotionShipDeckPassengerLifecycleFact fact, out string acceptanceError)
                {
                    var coordinator = new GlobalMotionShipDeckPassengerLifecycleCoordinator();
                    return coordinator.TryAccept(fact, out _, out acceptanceError);
                }
            });

            return report;
        }

        private static GlobalMotionShipDeckPassengerLifecycleCoordinator Register(
            string shipId,
            ulong networkObjectId,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt)
        {
            var coordinator = new GlobalMotionShipDeckPassengerLifecycleCoordinator();
            Require(coordinator.TryAccept(Registered(shipId, networkObjectId), out receipt, out string error), error);
            return coordinator;
        }

        private static GlobalMotionShipDeckPassengerLifecycleFact Registered(
            string shipId,
            ulong networkObjectId,
            bool serverAuthorized = true,
            bool protocolOwned = true)
        {
            return GlobalMotionShipDeckPassengerLifecycleFact.CreateShipRegistered(
                shipId, networkObjectId, serverAuthorized, protocolOwned);
        }

        private static GlobalMotionShipDeckPassengerLifecycleFact Attached(
            string shipId,
            ulong networkObjectId,
            ulong shipGeneration,
            string passengerId,
            ulong previousAttachmentGeneration)
        {
            return GlobalMotionShipDeckPassengerLifecycleFact.CreatePassengerAttached(
                shipId, networkObjectId, shipGeneration, passengerId,
                previousAttachmentGeneration, "DeckNav", true, true);
        }

        private static GlobalMotionShipDeckPassengerLifecycleFact Detached(
            string shipId,
            ulong networkObjectId,
            ulong shipGeneration,
            string passengerId,
            ulong attachmentGeneration)
        {
            return GlobalMotionShipDeckPassengerLifecycleFact.CreatePassengerDetached(
                shipId, networkObjectId, shipGeneration, passengerId,
                attachmentGeneration, "DeckNav", true, true);
        }

        private static GlobalMotionShipDeckPassengerLifecycleFact Invalidated(
            string shipId,
            ulong networkObjectId,
            ulong shipGeneration,
            string reason)
        {
            return GlobalMotionShipDeckPassengerLifecycleFact.CreateShipInvalidated(
                shipId, networkObjectId, shipGeneration, reason, true, true);
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}