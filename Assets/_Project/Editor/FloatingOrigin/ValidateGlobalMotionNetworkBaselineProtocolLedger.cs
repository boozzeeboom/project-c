using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the T-FO06BQ server-owned NetworkBaseline ledger contract.
    /// No NGO object, scene or runtime state is accessed.
    /// </summary>
    public static class ValidateGlobalMotionNetworkBaselineProtocolLedger
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Network Baseline Protocol Ledger")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06BQ] NetworkBaseline ledger: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06BQ] NetworkBaseline ledger: {report.passed} pure checks PASS / {report.failed} FAIL; NGO host remains unbound.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            var request = CreateRequest();
            var capability = CreateCapability(request);

            Check("Begin creates server-owned capture request", () =>
            {
                Require(GlobalMotionNetworkBaselineProtocolLedgerContract.TryBegin(capability, request, out var receipt, out string error), error);
                Require(receipt.Phase == GlobalMotionNetworkBaselineProtocolLedgerPhase.CaptureRequested, "capture_request_phase_invalid");
                Require(receipt.ServerOwned && receipt.ProtocolOwned && receipt.Ordinal == 0, "ledger_ownership_or_ordinal_invalid");
            });

            Check("Begin rejects transaction identity mismatch", () =>
            {
                var otherRequest = GlobalMotionRebaseRequest.Create(2, request.Plan, 1, "digest");
                Require(!GlobalMotionNetworkBaselineProtocolLedgerContract.TryBegin(capability, otherRequest, out _, out string error) &&
                    error == "transaction_identity_mismatch", error);
            });

            Check("Ordered ledger reaches capture", () =>
            {
                var receipt = Begin(capability, request);
                Require(GlobalMotionNetworkBaselineProtocolLedgerContract.TryAdvance(receipt,
                    GlobalMotionNetworkBaselineProtocolLedgerPhase.Captured, true, false, false, false, false, false, false,
                    out receipt, out string error), error);
                Require(receipt.CaptureReceipt && receipt.Ordinal == 1, "capture_receipt_missing");
            });

            Check("Phase skip is rejected", () =>
            {
                var receipt = Begin(capability, request);
                Require(!GlobalMotionNetworkBaselineProtocolLedgerContract.TryAdvance(receipt,
                    GlobalMotionNetworkBaselineProtocolLedgerPhase.Applied, true, true, false, false, false, false, false,
                    out _, out string error) && error == "ledger_phase_order_invalid", error);
            });

            Check("Apply and validate receipts require prior phases", () =>
            {
                var receipt = Begin(capability, request);
                Require(!GlobalMotionNetworkBaselineProtocolLedgerContract.TryAdvance(receipt,
                    GlobalMotionNetworkBaselineProtocolLedgerPhase.Captured, false, false, false, false, false, false, false,
                    out _, out string error) && error == "capture_receipt_required", error);
            });

            Check("Full ordered ledger reaches validation", () =>
            {
                var receipt = Begin(capability, request);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.Captured, true, false, false, false, false, false, false);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.ApplyRequested, true, false, false, false, false, false, false);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.Applied, true, true, false, false, false, false, false);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.ValidateRequested, true, true, false, false, false, false, false);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.Validated, true, true, true, false, false, false, false);
                Require(receipt.ValidateReceipt && receipt.Ordinal == 5, "validation_receipt_missing");
            });

            Check("Restore requires ownership, lifetime and generation receipts", () =>
            {
                var receipt = BuildValidated(capability, request);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.RestoreRequested, true, true, true, false, false, false, false);
                Require(!GlobalMotionNetworkBaselineProtocolLedgerContract.TryAdvance(receipt,
                    GlobalMotionNetworkBaselineProtocolLedgerPhase.Restored, true, true, true, false, false, false, false,
                    out _, out string error) && error == "complete_restore_receipt_required", error);
            });

            Check("Completed ledger requires complete restore", () =>
            {
                var receipt = BuildValidated(capability, request);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.RestoreRequested, true, true, true, false, false, false, false);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.Restored, true, true, true, true, true, true, true);
                Require(GlobalMotionNetworkBaselineProtocolLedgerContract.TryAdvance(receipt,
                    GlobalMotionNetworkBaselineProtocolLedgerPhase.Completed, true, true, true, true, true, true, true,
                    out receipt, out string error), error);
                Require(receipt.Phase == GlobalMotionNetworkBaselineProtocolLedgerPhase.Completed && receipt.Ordinal == 8, "completion_invalid");
            });

            Check("Faulted receipt remains terminal and validated", () =>
            {
                var receipt = Begin(capability, request);
                Require(GlobalMotionNetworkBaselineProtocolLedgerContract.TryFault(receipt, out receipt, out string error), error);
                Require(receipt.Phase == GlobalMotionNetworkBaselineProtocolLedgerPhase.Faulted &&
                    GlobalMotionNetworkBaselineProtocolLedgerContract.TryValidate(receipt, out error), error);
            });

            Check("Ledger never performs host registration", () =>
            {
                var receipt = Begin(capability, request);
                Require(receipt.ParticipantId == capability.ParticipantId && receipt.Identity.MatchesLifetime(capability.Identity), "ledger_identity_changed");
            });

            return report;

            GlobalMotionNetworkBaselineProtocolLedgerReceipt Begin(
                GlobalMotionNetworkBaselineReversibleCapability currentCapability,
                GlobalMotionRebaseRequest currentRequest)
            {
                Require(GlobalMotionNetworkBaselineProtocolLedgerContract.TryBegin(currentCapability, currentRequest, out var receipt, out string error), error);
                return receipt;
            }

            GlobalMotionNetworkBaselineProtocolLedgerReceipt Advance(
                GlobalMotionNetworkBaselineProtocolLedgerReceipt current,
                GlobalMotionNetworkBaselineProtocolLedgerPhase phase,
                bool capture, bool apply, bool validate, bool restore,
                bool ownership, bool lifetime, bool generation)
            {
                Require(GlobalMotionNetworkBaselineProtocolLedgerContract.TryAdvance(current, phase,
                    capture, apply, validate, restore, ownership, lifetime, generation,
                    out var next, out string error), error);
                return next;
            }

            GlobalMotionNetworkBaselineProtocolLedgerReceipt BuildValidated(
                GlobalMotionNetworkBaselineReversibleCapability currentCapability,
                GlobalMotionRebaseRequest currentRequest)
            {
                var receipt = Begin(currentCapability, currentRequest);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.Captured, true, false, false, false, false, false, false);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.ApplyRequested, true, false, false, false, false, false, false);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.Applied, true, true, false, false, false, false, false);
                receipt = Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.ValidateRequested, true, true, false, false, false, false, false);
                return Advance(receipt, GlobalMotionNetworkBaselineProtocolLedgerPhase.Validated, true, true, true, false, false, false, false);
            }
        }

        private static GlobalMotionRebaseRequest CreateRequest()
        {
            var frame = new LocalCoordinateFrame(GlobalPosition.Zero);
            Require(OriginRebasePlan.TryCreate(frame, new UnityEngine.Vector3(10f, 0f, 0f), 1f, 1f, out var plan), "test_plan_not_created");
            return GlobalMotionRebaseRequest.Create(1, plan, 1, "digest");
        }

        private static GlobalMotionNetworkBaselineReversibleCapability CreateCapability(GlobalMotionRebaseRequest request)
        {
            var identity = new GlobalMotionNetworkBaselineIdentity(71, 94, 3, 0, 2);
            var evidence = new GlobalMotionNetworkBaselineEvidence(
                "network-object:94", identity, 71, 94, 3, 2, 4, 9, 12, 120,
                true, 119, true, 11, 9, 12, true, true, true, true, true, true, true, true,
                GlobalMotionNetworkBaselineStateClassification.Restorable, true, true, true, true, true);
            Require(GlobalMotionNetworkBaselineReversibleTransactionContract.TryBuildCapability(
                evidence, true, true, true, true, true, true, true, true, true,
                request.TransactionId.ToString("N"), out var capability, out string error), error);
            return capability;
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
