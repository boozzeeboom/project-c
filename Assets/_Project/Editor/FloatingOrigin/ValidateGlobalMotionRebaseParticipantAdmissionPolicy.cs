using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure validation for the fail-closed participant admission policy.
    /// </summary>
    public static class ValidateGlobalMotionRebaseParticipantAdmissionPolicy
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Participant Admission Policy")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06S] Admission policy: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06S] Admission policy: {report.passed} pure checks PASS / {report.failed} FAIL; live manifest binding remains unimplemented.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try
                {
                    action();
                    report.passed++;
                }
                catch (Exception exception)
                {
                    report.failed++;
                    report.failures.Add(name + ": " + exception.Message);
                }
            }

            GlobalMotionRebaseManifestEntry entry = new GlobalMotionRebaseManifestEntry(
                "SHIP_ROOT/01",
                GlobalMotionRebaseParticipantKind.ShipRoot,
                "global:ship-root-01",
                1);

            GlobalMotionRebaseParticipantAdmissionEvidence Ready() =>
                new GlobalMotionRebaseParticipantAdmissionEvidence(true, true, true, true, true, true);

            Check("Ready evidence admits a reviewed participant", () =>
            {
                Require(GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(entry, Ready(), out string error), error);
            });

            Check("Missing identity review fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(false, true, true, true, true, true);
                Require(!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(entry, evidence, out string error) && error == "identity_not_reviewed:SHIP_ROOT/01", error);
            });

            Check("Missing explicit policy review fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(true, false, true, true, true, true);
                Require(!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(entry, evidence, out string error) && error == "explicit_policy_not_reviewed:SHIP_ROOT/01", error);
            });

            Check("Non-spatial catalog entries fail closed", () =>
            {
                var evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(true, true, false, true, true, true);
                Require(!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(entry, evidence, out string error) && error == "catalog_spatial_false:SHIP_ROOT/01", error);
            });

            Check("Missing runtime adapter fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(true, true, true, false, true, true);
                Require(!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(entry, evidence, out string error) && error == "runtime_adapter_not_ready:SHIP_ROOT/01", error);
            });

            Check("Incomplete runtime proof fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(true, true, true, true, false, true);
                Require(!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(entry, evidence, out string error) && error == "runtime_proof_incomplete:SHIP_ROOT/01", error);
            });

            Check("Missing rollback readiness fails closed", () =>
            {
                var evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(true, true, true, true, true, false);
                Require(!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(entry, evidence, out string error) && error == "rollback_not_ready:SHIP_ROOT/01", error);
            });

            Check("Failure precedence is deterministic", () =>
            {
                var evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(false, false, false, false, false, false);
                Require(!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(entry, evidence, out string error) && error == "identity_not_reviewed:SHIP_ROOT/01", error);
            });

            Check("Network gameplay roots also require every gate", () =>
            {
                var networkEntry = new GlobalMotionRebaseManifestEntry(
                    "NETWORK_GAMEPLAY_ROOT/test",
                    GlobalMotionRebaseParticipantKind.NetworkGameplayRoot,
                    "global:network-test",
                    1);
                var evidence = new GlobalMotionRebaseParticipantAdmissionEvidence(true, true, true, true, true, false);
                Require(!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(networkEntry, evidence, out string error) && error == "rollback_not_ready:NETWORK_GAMEPLAY_ROOT/test", error);
            });

            Check("Admission does not mutate evidence or entry identity", () =>
            {
                var evidence = Ready();
                Require(entry.ParticipantId == "SHIP_ROOT/01" && entry.SourceIdentity == "global:ship-root-01" && evidence.CatalogSpatial, "identity_or_evidence_mutated");
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
