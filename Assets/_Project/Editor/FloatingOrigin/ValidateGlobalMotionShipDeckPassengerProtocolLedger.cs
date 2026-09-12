using System;
using System.Collections.Generic;
using ProjectC.World.FloatingOrigin.Network;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    public static class ValidateGlobalMotionShipDeckPassengerProtocolLedger
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeck Passenger Protocol Ledger")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06CE] Passenger protocol ledger: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");
            Debug.Log($"[T-FO06CE] Passenger protocol ledger: {report.passed} pure checks PASS / {report.failed} FAIL; runtime producer remains unbound.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            var generation = new GlobalMotionShipDeckPassengerGeneration(
                "Lyra", 17UL, 2UL, 3UL, "DeckNav", true, true);

            Check("Ledger begin requires reviewed generation", () =>
            {
                Require(GlobalMotionShipDeckPassengerProtocolLedgerContract.TryBegin(
                    "tx-1", 5UL, generation, out var receipt, out string error), error);
                Require(receipt.Phase == GlobalMotionShipDeckPassengerLedgerPhase.CaptureRequested, "capture_phase_missing");
                Require(receipt.Ordinal == 0, "initial_ordinal_invalid");
            });

            Check("Capture phase advances in order", () =>
            {
                GlobalMotionShipDeckPassengerProtocolLedgerContract.TryBegin("tx-1", 5UL, generation, out var current, out _);
                Require(GlobalMotionShipDeckPassengerProtocolLedgerContract.TryAdvance(
                    current, GlobalMotionShipDeckPassengerLedgerPhase.Captured,
                    true, false, false, false, false, out var next, out string error), error);
                Require(next.CaptureReceipt && next.Ordinal == 1, "capture_receipt_missing");
            });

            Check("Phase skips are rejected", () =>
            {
                GlobalMotionShipDeckPassengerProtocolLedgerContract.TryBegin("tx-1", 5UL, generation, out var current, out _);
                Require(!GlobalMotionShipDeckPassengerProtocolLedgerContract.TryAdvance(
                    current, GlobalMotionShipDeckPassengerLedgerPhase.Rebuilt,
                    true, true, false, false, false, out _, out string error) &&
                    error == "ledger_phase_order_invalid", error);
            });

            Check("Rollback can begin before completion", () =>
            {
                GlobalMotionShipDeckPassengerProtocolLedgerContract.TryBegin("tx-1", 5UL, generation, out var current, out _);
                Require(GlobalMotionShipDeckPassengerProtocolLedgerContract.TryAdvance(
                    current, GlobalMotionShipDeckPassengerLedgerPhase.RollbackRequested,
                    false, false, false, false, false, out var next, out string error), error);
                Require(next.Phase == GlobalMotionShipDeckPassengerLedgerPhase.RollbackRequested, "rollback_phase_missing");
            });

            Check("Rollback completion requires receipt", () =>
            {
                GlobalMotionShipDeckPassengerProtocolLedgerContract.TryBegin("tx-1", 5UL, generation, out var current, out _);
                GlobalMotionShipDeckPassengerProtocolLedgerContract.TryAdvance(
                    current, GlobalMotionShipDeckPassengerLedgerPhase.RollbackRequested,
                    false, false, false, false, false, out current, out _);
                Require(GlobalMotionShipDeckPassengerProtocolLedgerContract.TryAdvance(
                    current, GlobalMotionShipDeckPassengerLedgerPhase.RollbackCompleted,
                    false, false, false, false, true, out var next, out string error), error);
                Require(next.RollbackReceipt, "rollback_receipt_missing");
            });

            Check("Fault is terminal and validated", () =>
            {
                GlobalMotionShipDeckPassengerProtocolLedgerContract.TryBegin("tx-1", 5UL, generation, out var current, out _);
                Require(GlobalMotionShipDeckPassengerProtocolLedgerContract.TryFault(current, out var faulted, out string error), error);
                Require(faulted.Phase == GlobalMotionShipDeckPassengerLedgerPhase.Faulted, "fault_phase_missing");
                Require(GlobalMotionShipDeckPassengerProtocolLedgerContract.TryValidate(faulted, out error), error);
            });

            Check("Missing generation remains fail closed", () =>
            {
                Require(!GlobalMotionShipDeckPassengerProtocolLedgerContract.TryBegin(
                    "tx-1", 5UL, default, out _, out string error) &&
                    error == "passenger_id_required", error);
            });

            Check("Invalid transaction identity remains fail closed", () =>
            {
                Require(!GlobalMotionShipDeckPassengerProtocolLedgerContract.TryBegin(
                    " ", 5UL, generation, out _, out string error) &&
                    error == "transaction_id_required", error);
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
