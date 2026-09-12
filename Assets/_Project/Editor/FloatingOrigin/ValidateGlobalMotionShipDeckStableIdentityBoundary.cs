using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for T-FO06CV. It validates the reviewed identity boundary only;
    /// it does not inspect or mutate ShipController, ShipCrewManifest, scenes or runtime objects.
    /// </summary>
    public static class ValidateGlobalMotionShipDeckStableIdentityBoundary
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Stable Identity Boundary")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CV] ShipDeck stable identity boundary: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CV] ShipDeck stable identity boundary: {report.passed} pure checks PASS / {report.failed} FAIL; runtime identity persistence remains unverified.");
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

            Check("ShipController persistent identity is accepted only with explicit review", () =>
            {
                Require(GlobalMotionShipDeckStableIdentityBoundary.TryCreateFromShipControllerPersistentId(
                    "WorldScene_0_0/NPC_Ship_A", 17UL, true, true, true, true,
                    out var receipt, out string error), error);
                Require(receipt.StableShipId == "WorldScene_0_0/NPC_Ship_A" &&
                    receipt.Source == GlobalMotionShipDeckStableIdentitySource.ShipControllerPersistentId,
                    "persistent_identity_receipt_invalid");
            });

            Check("Transport identity remains supplemental", () =>
            {
                Require(GlobalMotionShipDeckStableIdentityBoundary.TryCreateFromShipControllerPersistentId(
                    "stable-ship-id", 17UL, true, true, true, true,
                    out var receipt, out string error), error);
                Require(receipt.StableShipId != receipt.ShipNetworkObjectId.ToString(),
                    "stable_identity_collapsed_to_network_object_id");
            });

            Check("Whitespace around persistent identity is normalized", () =>
            {
                Require(GlobalMotionShipDeckStableIdentityBoundary.TryCreateFromShipControllerPersistentId(
                    "  stable-ship-id  ", 17UL, true, true, true, true,
                    out var receipt, out string error), error);
                Require(receipt.StableShipId == "stable-ship-id", "identity_trim_failed");
            });

            Check("Missing persistent identity fails closed", () =>
            {
                Require(!GlobalMotionShipDeckStableIdentityBoundary.TryCreateFromShipControllerPersistentId(
                    "", 17UL, true, true, true, true, out _, out string error) &&
                    error == "ship_persistent_id_required", error);
            });

            Check("Missing transport identity fails closed", () =>
            {
                Require(!GlobalMotionShipDeckStableIdentityBoundary.TryCreateFromShipControllerPersistentId(
                    "stable-ship-id", 0UL, true, true, true, true, out _, out string error) &&
                    error == "ship_network_object_id_required", error);
            });

            Check("Owner review is mandatory", () =>
            {
                Require(!GlobalMotionShipDeckStableIdentityBoundary.TryCreateFromShipControllerPersistentId(
                    "stable-ship-id", 17UL, false, true, true, true, out _, out string error) &&
                    error == "stable_identity_owner_review_required", error);
            });

            Check("Terminal invalidation ownership is mandatory", () =>
            {
                Require(!GlobalMotionShipDeckStableIdentityBoundary.TryCreateFromShipControllerPersistentId(
                    "stable-ship-id", 17UL, true, true, true, false, out _, out string error) &&
                    error == "terminal_invalidation_owner_review_required", error);
            });

            Check("Manifest identity must match the ShipController identity", () =>
            {
                Require(GlobalMotionShipDeckStableIdentityBoundary.TryValidateManifestAlignment(
                    "stable-ship-id", "stable-ship-id", out string error), error);
                Require(!GlobalMotionShipDeckStableIdentityBoundary.TryValidateManifestAlignment(
                    "stable-ship-id", "other-ship-id", out error) &&
                    error == "ship_manifest_identity_mismatch", error);
            });

            Check("Manifest identity cannot be omitted", () =>
            {
                Require(!GlobalMotionShipDeckStableIdentityBoundary.TryValidateManifestAlignment(
                    "stable-ship-id", "", out string error) &&
                    error == "manifest_ship_id_required", error);
            });

            Check("Receipt validation rejects unsupported source", () =>
            {
                var receipt = new GlobalMotionShipDeckStableIdentityReceipt(
                    "stable-ship-id", 17UL,
                    GlobalMotionShipDeckStableIdentitySource.ShipCrewManifestId,
                    true, true, true, true);
                Require(!GlobalMotionShipDeckStableIdentityBoundary.TryValidate(receipt, out string error) &&
                    error == "unsupported_stable_identity_source", error);
            });

            Check("Ownership flags are preserved and validated", () =>
            {
                Require(GlobalMotionShipDeckStableIdentityBoundary.TryCreateFromShipControllerPersistentId(
                    "stable-ship-id", 17UL, true, true, true, true,
                    out var receipt, out string error), error);
                Require(GlobalMotionShipDeckStableIdentityBoundary.TryValidate(receipt, out error) &&
                    receipt.OwnerReviewed && receipt.ServerOwned && receipt.ProtocolOwned &&
                    receipt.TerminalInvalidationOwnerReviewed, error);
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
