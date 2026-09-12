using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for T-FO06CW. It exercises explicit identity publication and
    /// typed registration/invalidation only; no gameplay caller is automatically bound.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckIdentityPublicationBridge
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Identity Publication Bridge")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CW] ShipDeck identity publication bridge: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CW] ShipDeck identity publication bridge: {report.passed} pure checks PASS / {report.failed} FAIL; runtime caller binding remains dormant.");
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

            Check("Explicit publication stores persistent identity and caller provenance", () =>
            {
                var bridge = CreateBridge();
                Require(bridge.TryPublishShipIdentity(
                    "stable-ship-id", "stable-ship-id", 17UL,
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out string error), error);
                Require(bridge.IsPublished && bridge.StableShipId == "stable-ship-id" &&
                    bridge.ShipNetworkObjectId == 17UL && bridge.CallerBinding.OwnerReviewed,
                    "publication_state_invalid");
            });

            Check("Published identity registers through the explicit lifecycle binding", () =>
            {
                var bridge = CreateBridge();
                Require(bridge.TryPublishShipIdentity(
                    "stable-ship-id", "stable-ship-id", 17UL,
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out string error), error);
                Require(bridge.TryRegisterPublishedShip(out var receipt, out error), error);
                Require(receipt.ShipId == "stable-ship-id" && receipt.ShipLifetimeGeneration == 1UL &&
                    receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered,
                    "published_registration_invalid");
            });

            Check("Published identity invalidates through the same stable identity", () =>
            {
                var bridge = CreateBridgeAndRegister();
                Require(bridge.TryInvalidatePublishedShip("despawn", out var receipt, out string error), error);
                Require(receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated &&
                    receipt.ShipId == "stable-ship-id" && receipt.InvalidationReason == "despawn",
                    "published_invalidation_invalid");
            });

            Check("Publication is one-shot", () =>
            {
                var bridge = CreateBridge();
                Require(bridge.TryPublishShipIdentity(
                    "stable-ship-id", "stable-ship-id", 17UL,
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out string error), error);
                Require(!bridge.TryPublishShipIdentity(
                    "other-id", "other-id", 18UL,
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out error) &&
                    error == "identity_publication_already_completed", error);
            });

            Check("Manifest mismatch is rejected before publication", () =>
            {
                var bridge = CreateBridge();
                Require(!bridge.TryPublishShipIdentity(
                    "stable-ship-id", "other-id", 17UL,
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out string error) &&
                    error == "ship_manifest_identity_mismatch" && !bridge.IsPublished, error);
            });

            Check("Non-controller callers cannot publish ship identity", () =>
            {
                var bridge = CreateBridge();
                Require(!bridge.TryPublishShipIdentity(
                    "stable-ship-id", "stable-ship-id", 17UL,
                    GlobalMotionShipDeckLifecycleCallerRole.ShipCrewSpawner,
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerAttached |
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerDetached,
                    true, true, true, true, out string error) &&
                    error == "identity_publication_caller_role_invalid", error);
            });

            Check("Ship registration and invalidation ingress are both mandatory", () =>
            {
                var bridge = CreateBridge();
                Require(!bridge.TryPublishShipIdentity(
                    "stable-ship-id", "stable-ship-id", 17UL,
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered,
                    true, true, true, true, out string error) &&
                    error == "identity_publication_ship_lifecycle_ingress_required", error);
            });

            Check("Missing publication blocks registration and invalidation", () =>
            {
                var bridge = CreateBridge();
                Require(!bridge.TryRegisterPublishedShip(out _, out string error) &&
                    error == "identity_publication_required", error);
                Require(!bridge.TryInvalidatePublishedShip("despawn", out _, out error) &&
                    error == "identity_publication_required", error);
            });

            Check("Ownership rejection does not publish identity", () =>
            {
                var bridge = CreateBridge();
                Require(!bridge.TryPublishShipIdentity(
                    "stable-ship-id", "stable-ship-id", 17UL,
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, false, true, true, out string error) &&
                    error == "stable_identity_server_ownership_required" && !bridge.IsPublished, error);
            });

            Check("NetworkObjectId remains supplemental in published receipt", () =>
            {
                var bridge = CreateBridge();
                Require(bridge.TryPublishShipIdentity(
                    "stable-ship-id", "stable-ship-id", 17UL,
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out string error), error);
                Require(bridge.StableShipId != bridge.ShipNetworkObjectId.ToString(),
                    "published_identity_collapsed_to_transport_id");
            });

            Check("Generation source delegates after explicit publication", () =>
            {
                var bridge = CreateBridgeAndRegister();
                Require(!bridge.TryGetGeneration("Lyra", out _, out string error) &&
                    error == "passenger_not_active", error);
            });

            return report;
        }

        private static GlobalMotionShipDeckIdentityPublicationBridge CreateBridge()
        {
            var binding = new GlobalMotionShipDeckPassengerLifecycleProducerBinding(
                new GlobalMotionShipDeckPassengerLifecycleProducerBridge());
            return new GlobalMotionShipDeckIdentityPublicationBridge(binding);
        }

        private static GlobalMotionShipDeckIdentityPublicationBridge CreateBridgeAndRegister()
        {
            var bridge = CreateBridge();
            Require(bridge.TryPublishShipIdentity(
                "stable-ship-id", "stable-ship-id", 17UL,
                GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                true, true, true, true, out string error), error);
            Require(bridge.TryRegisterPublishedShip(out _, out error), error);
            return bridge;
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
