using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for T-FO06CU. It validates the explicit caller boundary only;
    /// no runtime seam is discovered or mutated.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckLifecycleCallerBindingContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Lifecycle Caller Binding Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CU] ShipDeck caller binding contract: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CU] ShipDeck caller binding contract: {report.passed} pure checks PASS / {report.failed} FAIL; runtime caller binding remains blocked.");
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

            Check("Reviewed NpcShipController registration/invalidation boundary is accepted", () =>
            {
                Require(GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    "npc-ship:reviewed-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out var receipt, out string error), error);
                Require(receipt.CallerRole == GlobalMotionShipDeckLifecycleCallerRole.NpcShipController &&
                    receipt.StableShipId == "npc-ship:reviewed-id" &&
                    receipt.SupportsTerminalInvalidation, "controller_boundary_not_lossless");
            });

            Check("Reviewed ShipCrewSpawner attachment/detachment boundary is accepted", () =>
            {
                Require(GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.ShipCrewSpawner,
                    "npc-ship:reviewed-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerAttached |
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerDetached,
                    true, true, true, true, out var receipt, out string error), error);
                Require((receipt.IngressMask & GlobalMotionShipDeckLifecycleIngressMask.PassengerAttached) != 0 &&
                    (receipt.IngressMask & GlobalMotionShipDeckLifecycleIngressMask.PassengerDetached) != 0,
                    "crew_boundary_ingress_invalid");
            });

            Check("Stable protocol identity is distinct from transport identity", () =>
            {
                Require(GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    "stable-protocol-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out var receipt, out string error), error);
                Require(receipt.StableShipId != receipt.ShipNetworkObjectId.ToString(),
                    "stable_identity_was_reduced_to_transport_id");
            });

            Check("Missing owner review is rejected", () =>
            {
                Require(!GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    "stable-protocol-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, false, true, true, out _, out string error) &&
                    error == "owner_review_required", error);
            });

            Check("Missing terminal invalidation support is rejected", () =>
            {
                Require(!GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.ShipCrewSpawner,
                    "stable-protocol-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerAttached |
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerDetached,
                    false, true, true, true, out _, out string error) &&
                    error == "terminal_invalidation_support_required", error);
            });

            Check("Missing stable identity and transport identity are rejected", () =>
            {
                Require(!GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    "", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out _, out string error) &&
                    error == "stable_ship_id_required", error);
                Require(!GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    "stable-protocol-id", 0UL,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out _, out error) &&
                    error == "ship_network_object_id_required", error);
            });

            Check("Unsupported caller and empty ingress are rejected", () =>
            {
                Require(!GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.None,
                    "stable-protocol-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out _, out string error) &&
                    error == "caller_role_required", error);
                Require(!GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    "stable-protocol-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.None,
                    true, true, true, true, out _, out error) &&
                    error == "caller_ingress_required", error);
            });

            Check("Server and protocol ownership are mandatory", () =>
            {
                Require(!GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.NpcBrain,
                    "stable-protocol-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerAttached |
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerDetached,
                    true, true, false, true, out _, out string error) &&
                    error == "server_owned_caller_required", error);
                Require(!GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.NpcBrain,
                    "stable-protocol-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerAttached |
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerDetached,
                    true, true, true, false, out _, out error) &&
                    error == "protocol_owned_caller_required", error);
            });

            Check("Receipt validation is fail-closed for mutated ownership", () =>
            {
                Require(GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.ShipDeckNav,
                    "stable-protocol-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.PassengerAttached,
                    true, true, true, true, out var receipt, out string error), error);
                var invalid = new GlobalMotionShipDeckLifecycleCallerBindingReceipt(
                    receipt.CallerRole, receipt.StableShipId, receipt.ShipNetworkObjectId,
                    receipt.IngressMask, receipt.SupportsTerminalInvalidation,
                    receipt.OwnerReviewed, false, receipt.ProtocolOwned);
                Require(!GlobalMotionShipDeckLifecycleCallerBindingContract.TryValidate(invalid, out error) &&
                    error == "server_owned_caller_required", error);
            });

            Check("Contract does not manufacture generations or caller identity", () =>
            {
                Require(GlobalMotionShipDeckLifecycleCallerBindingContract.TryCreate(
                    GlobalMotionShipDeckLifecycleCallerRole.NpcShipController,
                    "stable-protocol-id", 17UL,
                    GlobalMotionShipDeckLifecycleIngressMask.ShipRegistered |
                    GlobalMotionShipDeckLifecycleIngressMask.ShipInvalidated,
                    true, true, true, true, out var receipt, out string error), error);
                Require(receipt.StableShipId == "stable-protocol-id" &&
                    receipt.ShipNetworkObjectId == 17UL,
                    "caller_contract_changed_supplied_identity");
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
