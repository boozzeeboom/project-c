using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the T-FO06BN protocol-owned NetworkBaseline capability boundary.
    /// It does not inspect or mutate NGO state, adapters, scenes or runtime objects.
    /// </summary>
    public static class ValidateGlobalMotionNetworkBaselineReversibleTransactionContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Network Baseline Reversible Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06BN] NetworkBaseline reversible contract: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06BN] NetworkBaseline reversible contract: {report.passed} pure checks PASS / {report.failed} FAIL; runtime adapter remains uninstalled.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try { action(); report.passed++; }
                catch (Exception exception) { report.failed++; report.failures.Add(name + ": " + exception.Message); }
            }

            GlobalMotionNetworkBaselineIdentity Identity() => new GlobalMotionNetworkBaselineIdentity(71, 94, 3, 0, 2);
            GlobalMotionNetworkBaselineEvidence RestorableEvidence() => new GlobalMotionNetworkBaselineEvidence(
                "network-object:94", Identity(), 71, 94, 3, 2, 4, 9, 12, 120,
                true, 119, true, 11, 9, 12, true, true, true, true, true, true, true, true,
                GlobalMotionNetworkBaselineStateClassification.Restorable, true, true, true, true, true);

            bool Build(
                GlobalMotionNetworkBaselineEvidence evidence,
                bool server = true,
                bool protocol = true,
                bool capture = true,
                bool apply = true,
                bool validate = true,
                bool restore = true,
                bool ownership = true,
                bool lifetime = true,
                bool generation = true,
                string transaction = "txn-01")
            {
                return GlobalMotionNetworkBaselineReversibleTransactionContract.TryBuildCapability(
                    evidence, server, protocol, capture, apply, validate, restore, ownership, lifetime, generation,
                    transaction, out _, out _);
            }

            Check("Complete protocol-owned capability validates", () =>
            {
                var evidence = RestorableEvidence();
                Require(Build(evidence), "capability_rejected");
                Require(GlobalMotionNetworkBaselineReversibleTransactionContract.TryBuildCapability(
                    evidence, true, true, true, true, true, true, true, true, true, "txn-01", out var capability, out string error), error);
                Require(capability.IsComplete, "capability_not_complete");
                Require(GlobalMotionNetworkBaselineReversibleTransactionContract.CanAuthorizeNativeBaselineAdapter(capability), "adapter_authorization_not_possible");
            });

            Check("Observation-only evidence cannot become reversible", () =>
            {
                var observation = new GlobalMotionNetworkBaselineEvidence(
                    "network-object:94", Identity(), 71, 94, 3, 2, 4, 9, 12, 120,
                    true, 119, true, 11, 9, 12, true, true, true, true, true, true, true, true,
                    GlobalMotionNetworkBaselineStateClassification.ObservationOnly, false, false, false, false, false);
                Require(!Build(observation), "observation_promoted");
            });

            Check("Server authority is mandatory", () => Require(!Build(RestorableEvidence(), server: false), "authority_gap_accepted"));
            Check("Protocol ownership is mandatory", () => Require(!Build(RestorableEvidence(), protocol: false), "protocol_gap_accepted"));
            Check("Transaction identity is mandatory", () => Require(!Build(RestorableEvidence(), transaction: " "), "transaction_identity_gap_accepted"));
            Check("Capture boundary is mandatory", () => Require(!Build(RestorableEvidence(), capture: false), "capture_gap_accepted"));
            Check("Apply boundary is mandatory", () => Require(!Build(RestorableEvidence(), apply: false), "apply_gap_accepted"));
            Check("Validate boundary is mandatory", () => Require(!Build(RestorableEvidence(), validate: false), "validate_gap_accepted"));
            Check("Restore boundary is mandatory", () => Require(!Build(RestorableEvidence(), restore: false), "restore_gap_accepted"));
            Check("Ownership restore is mandatory", () => Require(!Build(RestorableEvidence(), ownership: false), "ownership_restore_gap_accepted"));
            Check("Lifetime restore is mandatory", () => Require(!Build(RestorableEvidence(), lifetime: false), "lifetime_restore_gap_accepted"));
            Check("Baseline generation restore is mandatory", () => Require(!Build(RestorableEvidence(), generation: false), "generation_restore_gap_accepted"));

            Check("Receipt phase cannot start at None", () =>
            {
                var capability = BuildCapability();
                var receipt = new GlobalMotionNetworkBaselineReversibleReceipt(capability, GlobalMotionNetworkBaselineReversiblePhase.None, false, false, false, false);
                Require(!GlobalMotionNetworkBaselineReversibleTransactionContract.TryValidateReceipt(receipt, out string error) && error == "reversible_receipt_phase_invalid", error);
            });

            Check("Receipt requires capture before Applied", () =>
            {
                var capability = BuildCapability();
                var receipt = new GlobalMotionNetworkBaselineReversibleReceipt(capability, GlobalMotionNetworkBaselineReversiblePhase.Applied, false, true, false, false);
                Require(!GlobalMotionNetworkBaselineReversibleTransactionContract.TryValidateReceipt(receipt, out string error) && error == "reversible_capture_receipt_missing", error);
            });

            Check("Completed receipt requires all completed boundaries", () =>
            {
                var capability = BuildCapability();
                var receipt = new GlobalMotionNetworkBaselineReversibleReceipt(capability, GlobalMotionNetworkBaselineReversiblePhase.Completed, true, true, true, true);
                Require(GlobalMotionNetworkBaselineReversibleTransactionContract.TryValidateReceipt(receipt, out string error), error);
            });

            Check("Faulted receipt is rejected", () =>
            {
                var capability = BuildCapability();
                var receipt = new GlobalMotionNetworkBaselineReversibleReceipt(capability, GlobalMotionNetworkBaselineReversiblePhase.Faulted, true, true, false, false);
                Require(!GlobalMotionNetworkBaselineReversibleTransactionContract.TryValidateReceipt(receipt, out string error) && error == "reversible_receipt_phase_invalid", error);
            });

            return report;

            GlobalMotionNetworkBaselineReversibleCapability BuildCapability()
            {
                Require(GlobalMotionNetworkBaselineReversibleTransactionContract.TryBuildCapability(
                    RestorableEvidence(), true, true, true, true, true, true, true, true, true, "txn-01", out var capability, out string error), error);
                return capability;
            }
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
