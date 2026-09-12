using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for T-FO06CS. It exercises the explicit producer/bridge without
    /// Unity objects, NGO callbacks, scene discovery, host binding or Play Mode.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckPassengerLifecycleProducerBridge
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Lifecycle Producer Bridge")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CS] ShipDeck lifecycle producer bridge: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CS] ShipDeck lifecycle producer bridge: {report.passed} pure checks PASS / {report.failed} FAIL; runtime caller binding remains dormant.");
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

            Check("ShipRegistered ingress returns accepted lossless mapping", () =>
            {
                var producer = new GlobalMotionShipDeckPassengerLifecycleProducerBridge();
                Require(producer.TryRegisterShipServerAuthorized(
                    "ship-a", 17UL, true, true, out var mapping, out string error), error);
                Require(mapping.ShipId == "ship-a" && mapping.ShipNetworkObjectId == 17UL &&
                    mapping.ShipLifetimeGeneration == 1UL && mapping.CoordinatorLedgerOrdinal == 1UL &&
                    mapping.LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered &&
                    mapping.ServerOwned && mapping.ProtocolOwned && string.IsNullOrEmpty(mapping.PassengerId),
                    "registration_mapping_not_lossless");
            });

            Check("PassengerAttached ingress receives coordinator-owned generation", () =>
            {
                var producer = Register(out var registered);
                Require(producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL,
                    "DeckNav", true, true, out var mapping, out string error), error);
                Require(mapping.LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached &&
                    mapping.PassengerId == "Lyra" && mapping.PassengerAttachmentGeneration == 1UL &&
                    mapping.CoordinatorLedgerOrdinal == 2UL, "attachment_mapping_lineage_invalid");
                Require(producer.TryGetGeneration("Lyra", out var generation, out error), error);
                Require(generation.ShipSpawnGeneration == registered.ShipLifetimeGeneration &&
                    generation.AttachmentGeneration == mapping.PassengerAttachmentGeneration &&
                    generation.DeckNavId == "DeckNav", "coordinator_generation_view_invalid");
            });

            Check("PassengerDetached ingress closes the exact active epoch", () =>
            {
                var producer = Register(out var registered);
                Require(producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL,
                    "DeckNav", true, true, out var attached, out string error), error);
                Require(producer.TryDetachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra",
                    attached.PassengerAttachmentGeneration, "DeckNav", true, true,
                    out var detached, out error), error);
                Require(detached.LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached &&
                    detached.PassengerAttachmentGeneration == attached.PassengerAttachmentGeneration &&
                    detached.CoordinatorLedgerOrdinal == 3UL, "detach_mapping_lineage_invalid");
                Require(!producer.TryGetGeneration("Lyra", out _, out error) &&
                    error == "passenger_not_active", error);
            });

            Check("ShipInvalidated ingress is terminal and mapped with reason", () =>
            {
                var producer = Register(out var registered);
                Require(producer.TryInvalidateShipServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "despawn",
                    true, true, out var mapping, out string error), error);
                Require(mapping.LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated &&
                    mapping.InvalidationReason == "despawn" && mapping.CoordinatorLedgerOrdinal == 2UL &&
                    string.IsNullOrEmpty(mapping.PassengerId) && mapping.PassengerAttachmentGeneration == 0UL,
                    "invalidation_mapping_lineage_invalid");
                var downstream = mapping.ToDownstreamReceipt();
                Require(downstream.ShipId == mapping.ShipId &&
                    downstream.CoordinatorLedgerOrdinal == mapping.CoordinatorLedgerOrdinal &&
                    downstream.InvalidationReason == mapping.InvalidationReason,
                    "downstream_mapping_not_lossless");
            });

            Check("Coordinator, not caller, owns generations and ledger ordinal", () =>
            {
                var producer = Register(out var first);
                Require(first.ShipLifetimeGeneration == 1UL && first.CoordinatorLedgerOrdinal == 1UL,
                    "first_coordinator_lineage_invalid");
                Require(producer.TryInvalidateShipServerAuthorized(
                    "ship-a", 17UL, first.ShipLifetimeGeneration, "despawn", true, true,
                    out var terminal, out string error), error);
                Require(producer.TryRegisterShipServerAuthorized(
                    "ship-a", 17UL, true, true, out var second, out error), error);
                Require(second.ShipLifetimeGeneration > first.ShipLifetimeGeneration &&
                    second.CoordinatorLedgerOrdinal == terminal.CoordinatorLedgerOrdinal + 1UL,
                    "coordinator_did_not_advance_lineage");
            });

            Check("Reattach receives a never-reused coordinator attachment generation", () =>
            {
                var producer = Register(out var registered);
                Require(producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL,
                    "DeckNav", true, true, out var first, out string error), error);
                Require(producer.TryDetachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra",
                    first.PassengerAttachmentGeneration, "DeckNav", true, true,
                    out var detached, out error), error);
                Require(producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra",
                    detached.PassengerAttachmentGeneration, "DeckNav", true, true,
                    out var second, out error), error);
                Require(second.PassengerAttachmentGeneration > first.PassengerAttachmentGeneration &&
                    second.CoordinatorLedgerOrdinal == 4UL, "reattach_generation_invalid");
            });

            Check("Missing ownership is rejected before coordinator state changes", () =>
            {
                var producer = new GlobalMotionShipDeckPassengerLifecycleProducerBridge();
                Require(!producer.TryRegisterShipServerAuthorized(
                    "ship-a", 17UL, false, true, out var mapping, out string error) &&
                    error == "server_authorization_required" && mapping.CoordinatorLedgerOrdinal == 0UL, error);
                Require(!producer.TryRegisterShipServerAuthorized(
                    "ship-a", 17UL, true, false, out mapping, out error) &&
                    error == "protocol_ownership_required" && mapping.CoordinatorLedgerOrdinal == 0UL, error);
                Require(producer.TryRegisterShipServerAuthorized(
                    "ship-a", 17UL, true, true, out mapping, out error), error);
                Require(mapping.ShipLifetimeGeneration == 1UL, "rejected_fact_consumed_generation");
            });

            Check("Missing stable and transport identities fail closed", () =>
            {
                var producer = new GlobalMotionShipDeckPassengerLifecycleProducerBridge();
                Require(!producer.TryRegisterShipServerAuthorized(
                    "", 17UL, true, true, out _, out string error) && error == "ship_id_required", error);
                Require(!producer.TryRegisterShipServerAuthorized(
                    "ship-a", 0UL, true, true, out _, out error) &&
                    error == "ship_network_object_id_required", error);
                Require(producer.TryRegisterShipServerAuthorized(
                    "ship-a", 17UL, true, true, out var registered, out error), error);
                Require(!producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "", 0UL,
                    "DeckNav", true, true, out _, out error) && error == "passenger_id_required", error);
                Require(!producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL,
                    "", true, true, out _, out error) && error == "deck_nav_id_required", error);
            });

            Check("Invalid lineage and out-of-order facts fail closed", () =>
            {
                var producer = new GlobalMotionShipDeckPassengerLifecycleProducerBridge();
                Require(!producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, 1UL, "Lyra", 0UL, "DeckNav", true, true,
                    out var mapping, out string error) && error == "ship_not_registered" &&
                    mapping.CoordinatorLedgerOrdinal == 0UL, error);
                Require(producer.TryRegisterShipServerAuthorized(
                    "ship-a", 17UL, true, true, out var registered, out error), error);
                Require(!producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration + 1UL, "Lyra", 0UL,
                    "DeckNav", true, true, out _, out error) &&
                    error == "stale_ship_lifetime_generation", error);
                Require(!producer.TryDetachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 1UL,
                    "DeckNav", true, true, out _, out error) &&
                    error == "passenger_not_active", error);
            });

            Check("Duplicate, stale and mismatched active facts fail closed", () =>
            {
                var producer = Register(out var registered);
                Require(producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL,
                    "DeckNav", true, true, out var attached, out string error), error);
                Require(!producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL,
                    "DeckNav", true, true, out var mapping, out error) &&
                    error == "duplicate_active_passenger_attachment" &&
                    mapping.CoordinatorLedgerOrdinal == 0UL, error);
                Require(!producer.TryDetachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra",
                    attached.PassengerAttachmentGeneration + 1UL, "DeckNav", true, true,
                    out _, out error) && error == "stale_attachment_generation", error);
                Require(!producer.TryDetachPassengerServerAuthorized(
                    "ship-a", 18UL, registered.ShipLifetimeGeneration, "Lyra",
                    attached.PassengerAttachmentGeneration, "DeckNav", true, true,
                    out _, out error) && error == "ship_network_identity_mismatch", error);
            });

            Check("Post-invalidation facts are rejected and do not emit mappings", () =>
            {
                var producer = Register(out var registered);
                Require(producer.TryInvalidateShipServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "despawn",
                    true, true, out _, out string error), error);
                Require(!producer.TryAttachPassengerServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "Lyra", 0UL,
                    "DeckNav", true, true, out var mapping, out error) &&
                    error == "ship_lifecycle_invalidated" && mapping.CoordinatorLedgerOrdinal == 0UL, error);
                Require(!producer.TryInvalidateShipServerAuthorized(
                    "ship-a", 17UL, registered.ShipLifetimeGeneration, "duplicate",
                    true, true, out mapping, out error) &&
                    error == "ship_lifecycle_invalidated" && mapping.CoordinatorLedgerOrdinal == 0UL, error);
            });

            return report;
        }

        private static GlobalMotionShipDeckPassengerLifecycleProducerBridge Register(
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping)
        {
            var producer = new GlobalMotionShipDeckPassengerLifecycleProducerBridge();
            Require(producer.TryRegisterShipServerAuthorized(
                "ship-a", 17UL, true, true, out mapping, out string error), error);
            return producer;
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
