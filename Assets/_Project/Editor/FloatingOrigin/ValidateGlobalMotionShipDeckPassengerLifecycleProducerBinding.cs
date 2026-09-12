using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for T-FO06CT. It validates explicit binding state only; it does not
    /// discover gameplay objects, bind runtime callers, mutate scenes or enter Play Mode.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckPassengerLifecycleProducerBinding
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Lifecycle Producer Binding")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CT] ShipDeck lifecycle producer binding: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CT] ShipDeck lifecycle producer binding: {report.passed} pure checks PASS / {report.failed} FAIL; runtime caller binding remains dormant.");
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

            Check("Registration is forwarded and persisted as lossless downstream receipt", () =>
            {
                var binding = CreateBinding();
                Require(binding.TryRegisterShipServerAuthorized("ship-a", 17UL, true, true, out var receipt, out string error), error);
                Require(receipt.ShipId == "ship-a" && receipt.ShipLifetimeGeneration == 1UL &&
                    receipt.CoordinatorLedgerOrdinal == 1UL && binding.IsShipRegistered, "registration_state_invalid");
            });

            Check("Attachment is stored as active downstream receipt", () =>
            {
                var binding = RegisteredBinding();
                Require(binding.TryAttachPassengerServerAuthorized("ship-a", 17UL, binding.ShipLifetimeGeneration,
                    "Lyra", 0UL, "DeckNav", true, true, out var receipt, out string error), error);
                Require(binding.ActivePassengerCount == 1 && receipt.PassengerId == "Lyra" &&
                    receipt.AttachmentGeneration == 1UL, "active_attachment_state_invalid");
            });

            Check("Active binding is deterministic and validates through the source-binding contract", () =>
            {
                var binding = RegisteredBinding();
                Attach(binding, "Zed");
                Attach(binding, "Lyra");
                Require(binding.TryCreateActivePassengerBinding(9UL, out var lifecycleBinding,
                    out var receipts, out string error), error);
                Require(lifecycleBinding.BindingGeneration == 9UL && lifecycleBinding.PassengerCount == 2 &&
                    receipts[0].PassengerId == "Lyra" && receipts[1].PassengerId == "Zed",
                    "active_binding_order_or_identity_invalid");
            });

            Check("Detach removes only the exact active passenger epoch", () =>
            {
                var binding = RegisteredBinding();
                Attach(binding, "Lyra");
                Require(binding.TryDetachPassengerServerAuthorized("ship-a", 17UL, binding.ShipLifetimeGeneration,
                    "Lyra", 1UL, "DeckNav", true, true, out var receipt, out string error), error);
                Require(receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached &&
                    binding.ActivePassengerCount == 0, "detach_state_invalid");
            });

            Check("Reattach receives coordinator-owned never-reused generation", () =>
            {
                var binding = RegisteredBinding();
                Attach(binding, "Lyra");
                Require(binding.TryDetachPassengerServerAuthorized("ship-a", 17UL, binding.ShipLifetimeGeneration,
                    "Lyra", 1UL, "DeckNav", true, true, out var detached, out string error), error);
                Require(binding.TryAttachPassengerServerAuthorized("ship-a", 17UL, binding.ShipLifetimeGeneration,
                    "Lyra", detached.AttachmentGeneration, "DeckNav", true, true, out var attached, out error), error);
                Require(attached.AttachmentGeneration > detached.AttachmentGeneration && binding.ActivePassengerCount == 1,
                    "reattach_generation_invalid");
            });

            Check("Ship invalidation clears active state and blocks future binding", () =>
            {
                var binding = RegisteredBinding();
                Attach(binding, "Lyra");
                Require(binding.TryInvalidateShipServerAuthorized("ship-a", 17UL, binding.ShipLifetimeGeneration,
                    "despawn", true, true, out var receipt, out string error), error);
                Require(receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated &&
                    binding.IsShipInvalidated && binding.ActivePassengerCount == 0, "invalidation_state_invalid");
                Require(!binding.TryCreateActivePassengerBinding(1UL, out _, out _, out error) &&
                    error == "binding_ship_invalidated", error);
            });

            Check("Registration cannot be duplicated or performed after invalidation", () =>
            {
                var binding = RegisteredBinding();
                Require(!binding.TryRegisterShipServerAuthorized("ship-a", 17UL, true, true, out _, out string error) &&
                    error == "binding_ship_already_registered", error);
                Require(binding.TryInvalidateShipServerAuthorized("ship-a", 17UL, binding.ShipLifetimeGeneration,
                    "despawn", true, true, out _, out error), error);
                Require(!binding.TryRegisterShipServerAuthorized("ship-b", 18UL, true, true, out _, out error) &&
                    error == "binding_ship_invalidated", error);
            });

            Check("Stable ship identity and generation are required before attach", () =>
            {
                var binding = new GlobalMotionShipDeckPassengerLifecycleProducerBinding(
                    new GlobalMotionShipDeckPassengerLifecycleProducerBridge());
                Require(!binding.TryAttachPassengerServerAuthorized("ship-a", 17UL, 1UL, "Lyra", 0UL,
                    "DeckNav", true, true, out _, out string error) &&
                    error == "binding_ship_registration_required", error);
                Require(binding.TryRegisterShipServerAuthorized("ship-a", 17UL, true, true, out _, out error), error);
                Require(!binding.TryAttachPassengerServerAuthorized("ship-b", 17UL, 1UL, "Lyra", 0UL,
                    "DeckNav", true, true, out _, out error) && error == "binding_ship_id_mismatch", error);
                Require(!binding.TryAttachPassengerServerAuthorized("ship-a", 18UL, 1UL, "Lyra", 0UL,
                    "DeckNav", true, true, out _, out error) && error == "binding_ship_network_identity_mismatch", error);
            });

            Check("Ownership rejection does not mutate binding state", () =>
            {
                var binding = new GlobalMotionShipDeckPassengerLifecycleProducerBinding(
                    new GlobalMotionShipDeckPassengerLifecycleProducerBridge());
                Require(!binding.TryRegisterShipServerAuthorized("ship-a", 17UL, false, true, out _, out string error) &&
                    error == "server_authorization_required" && !binding.IsShipRegistered, error);
                Require(binding.TryRegisterShipServerAuthorized("ship-a", 17UL, true, true, out _, out error), error);
            });

            Check("Missing active passengers fails closed", () =>
            {
                var binding = RegisteredBinding();
                Require(!binding.TryCreateActivePassengerBinding(1UL, out _, out _, out string error) &&
                    error == "active_passenger_receipts_required", error);
            });

            Check("Caller-supplied identity cannot overwrite a bound ship", () =>
            {
                var binding = RegisteredBinding();
                Require(!binding.TryAttachPassengerServerAuthorized("ship-a", 17UL, binding.ShipLifetimeGeneration + 1UL,
                    "Lyra", 0UL, "DeckNav", true, true, out _, out string error) &&
                    error == "binding_ship_lifetime_generation_mismatch", error);
                Require(binding.ActivePassengerCount == 0, "rejected_lineage_mutated_state");
            });

            Check("Generation source delegates to the protocol producer", () =>
            {
                var binding = RegisteredBinding();
                Attach(binding, "Lyra");
                Require(binding.TryGetGeneration("Lyra", out var generation, out string error), error);
                Require(generation.ShipSpawnGeneration == binding.ShipLifetimeGeneration &&
                    generation.AttachmentGeneration == 1UL && generation.ProtocolOwned,
                    "delegated_generation_invalid");
            });

            return report;
        }

        private static GlobalMotionShipDeckPassengerLifecycleProducerBinding CreateBinding()
        {
            return new GlobalMotionShipDeckPassengerLifecycleProducerBinding(
                new GlobalMotionShipDeckPassengerLifecycleProducerBridge());
        }

        private static GlobalMotionShipDeckPassengerLifecycleProducerBinding RegisteredBinding()
        {
            var binding = CreateBinding();
            Require(binding.TryRegisterShipServerAuthorized("ship-a", 17UL, true, true, out _, out string error), error);
            return binding;
        }

        private static void Attach(GlobalMotionShipDeckPassengerLifecycleProducerBinding binding, string passengerId)
        {
            Require(binding.TryAttachPassengerServerAuthorized("ship-a", 17UL, binding.ShipLifetimeGeneration,
                passengerId, 0UL, "DeckNav", true, true, out _, out string error), error);
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
