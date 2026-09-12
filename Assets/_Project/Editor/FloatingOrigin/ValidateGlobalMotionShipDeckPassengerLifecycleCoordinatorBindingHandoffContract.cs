using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for T-FO06CO coordinator provenance mapping/handoff.
    /// No Unity object, NGO callback, runtime producer or host binding is used.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Passenger Coordinator Handoff Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CO] Passenger coordinator handoff: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CO] Passenger coordinator handoff: {report.passed} pure checks PASS / {report.failed} FAIL; provenance handoff remains dormant.");
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

            Check("Complete coordinator sequence maps in explicit event order", () =>
            {
                BuildSequence(out _, out var receipts);
                Require(GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryCreateHandoff(
                    receipts, out var handoff, out string error), error);
                Require(GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryValidateHandoff(
                    handoff, out error), error);
                Require(handoff.Count == 5 &&
                    handoff.Mappings[0].LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered &&
                    handoff.Mappings[1].LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached &&
                    handoff.Mappings[2].LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached &&
                    handoff.Mappings[4].LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated,
                    "event_order_not_preserved");
            });

            Check("Stable identity and coordinator provenance are copied losslessly", () =>
            {
                BuildSequence(out _, out var receipts);
                Require(GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryCreateHandoff(
                    receipts, out var handoff, out string error), error);
                var mapping = handoff.Mappings[3];
                var downstream = handoff.DownstreamReceipts[3];
                Require(mapping.ShipId == "ship-a" && downstream.ShipId == "ship-a", "stable_ship_id_lost");
                Require(mapping.ShipNetworkObjectId == 17UL && downstream.ShipNetworkObjectId == 17UL,
                    "supplemental_network_identity_lost");
                Require(mapping.ShipLifetimeGeneration == 1UL && downstream.ShipLifetimeGeneration == 1UL &&
                    downstream.ShipSpawnGeneration == 1UL, "ship_lifetime_generation_lost");
                Require(mapping.PassengerAttachmentGeneration == 2UL &&
                    downstream.AttachmentGeneration == 2UL && mapping.DeckNavId == "DeckNav" &&
                    downstream.DeckNavId == "DeckNav", "attachment_provenance_lost");
                Require(mapping.CoordinatorLedgerOrdinal == 4UL &&
                    downstream.CoordinatorLedgerOrdinal == 4UL &&
                    downstream.Phase == mapping.LifecycleEventKind &&
                    downstream.ServerOwned && downstream.ProtocolOwned, "ledger_or_ownership_provenance_lost");
            });

            Check("Ledger ordinals are continuous and not callback-derived", () =>
            {
                BuildSequence(out _, out var receipts);
                Require(GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryCreateHandoff(
                    receipts, out var handoff, out string error), error);
                for (int i = 0; i < handoff.Mappings.Length; i++)
                    Require(handoff.Mappings[i].CoordinatorLedgerOrdinal == (ulong)(i + 1),
                        "ledger_continuity_lost:index=" + i);

                var reordered = (GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping[])handoff.Mappings.Clone();
                var first = reordered[0];
                reordered[0] = reordered[1];
                reordered[1] = first;
                var reorderedDownstream = (GlobalMotionShipDeckPassengerLifecycleReceipt[])handoff.DownstreamReceipts.Clone();
                var firstDownstream = reorderedDownstream[0];
                reorderedDownstream[0] = reorderedDownstream[1];
                reorderedDownstream[1] = firstDownstream;
                var invalid = new GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoff(
                    reordered, reorderedDownstream);
                Require(!GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryValidateHandoff(
                    invalid, out error) && error == "lifecycle_registration_required_first", error);
            });

            Check("Incomplete mappings fail closed without identity inference", () =>
            {
                var invalid = new GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping(
                    "", 17UL, 1UL, "Lyra", 1UL, "DeckNav", 2UL, true, true,
                    GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached, null);
                Require(!GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryValidateMapping(
                    invalid, out string error) && error == "ship_id_required", error);

                invalid = new GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping(
                    "ship-a", 17UL, 1UL, "Lyra", 0UL, "DeckNav", 2UL, true, true,
                    GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached, null);
                Require(!GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryValidateMapping(
                    invalid, out error) && error == "passenger_attachment_generation_required", error);
            });

            Check("Invalid coordinator receipt provenance is rejected", () =>
            {
                var invalid = new GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt(
                    "ship-a", 17UL, 1UL, "Lyra", 1UL, "DeckNav",
                    GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached,
                    0UL, null, true, true);
                Require(!GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryMap(
                    invalid, out _, out string error) && error == "ledger_ordinal_required", error);
            });

            Check("Stale, duplicate, out-of-order and post-invalidation events remain rejected", () =>
            {
                var coordinator = Register(out var registered);
                Require(!coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration + 1UL, "Lyra", 0UL),
                    out _, out string error) && error == "stale_ship_lifetime_generation", error);
                Require(coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL),
                    out var attached, out error), error);
                Require(!coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL),
                    out _, out error) && error == "duplicate_active_passenger_attachment", error);
                Require(!coordinator.TryAccept(Detached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Bram", 1UL),
                    out _, out error) && error == "passenger_not_active", error);
                Require(coordinator.TryAccept(Invalidated("ship-a", 17UL, registered.ShipLifetimeGeneration, "despawn"),
                    out _, out error), error);
                Require(!coordinator.TryAccept(Detached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", attached.AttachmentGeneration),
                    out _, out error) && error == "ship_lifecycle_invalidated", error);
            });

            Check("Terminal invalidation mapping preserves reason and closes lifecycle", () =>
            {
                BuildSequence(out _, out var receipts);
                var terminal = receipts[receipts.Length - 1];
                Require(GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryMap(
                    terminal, out var mapping, out string error), error);
                var downstream = mapping.ToDownstreamReceipt();
                Require(mapping.LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated &&
                    mapping.InvalidationReason == "despawn" && downstream.InvalidationReason == "despawn" &&
                    string.IsNullOrEmpty(downstream.PassengerId) && downstream.AttachmentGeneration == 0UL,
                    "terminal_invalidation_provenance_invalid");
            });

            return report;
        }

        private static void BuildSequence(
            out GlobalMotionShipDeckPassengerLifecycleCoordinator coordinator,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt[] receipts)
        {
            coordinator = Register(out var registered);
            Require(coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL),
                out var attached, out string error), error);
            Require(coordinator.TryAccept(Detached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", attached.AttachmentGeneration),
                out var detached, out error), error);
            Require(coordinator.TryAccept(Attached("ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", detached.AttachmentGeneration),
                out var reattached, out error), error);
            Require(coordinator.TryAccept(Invalidated("ship-a", 17UL, registered.ShipLifetimeGeneration, "despawn"),
                out var invalidated, out error), error);
            receipts = new[] { registered, attached, detached, reattached, invalidated };
        }

        private static GlobalMotionShipDeckPassengerLifecycleCoordinator Register(
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt)
        {
            var coordinator = new GlobalMotionShipDeckPassengerLifecycleCoordinator();
            Require(coordinator.TryAccept(Registered("ship-a", 17UL), out receipt, out string error), error);
            return coordinator;
        }

        private static GlobalMotionShipDeckPassengerLifecycleFact Registered(string shipId, ulong networkObjectId)
        {
            return GlobalMotionShipDeckPassengerLifecycleFact.CreateShipRegistered(shipId, networkObjectId, true, true);
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
