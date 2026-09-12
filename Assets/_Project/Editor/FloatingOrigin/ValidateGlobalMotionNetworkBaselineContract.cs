using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the dormant T-FO06AZ NetworkBaseline contract.
    /// It does not inspect or mutate NGO objects, runtime state, adapters or scenes.
    /// </summary>
    public static class ValidateGlobalMotionNetworkBaselineContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Network Baseline Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06AZ] NetworkBaseline contract: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06AZ] NetworkBaseline contract: {report.passed} pure checks PASS / {report.failed} FAIL; observation-only evidence remains separate from restorable NGO state.");
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

            GlobalMotionNetworkBaselineIdentity Identity() => new GlobalMotionNetworkBaselineIdentity(
                sessionId: 71, networkObjectId: 94, spawnGeneration: 3, ownerClientId: 0, ownershipGeneration: 2);

            GlobalMotionNetworkBaselineEvidence Ready(
                GlobalMotionNetworkBaselineStateClassification classification = GlobalMotionNetworkBaselineStateClassification.ObservationOnly,
                bool restoreCapabilityClaimed = false,
                bool restoreCaptured = false,
                bool restoreApplied = false,
                bool restoreValidated = false,
                bool restoreSucceeded = false) => new GlobalMotionNetworkBaselineEvidence(
                    participantId: "network-object:94",
                    identity: Identity(),
                    bindingSessionId: 71,
                    bindingNetworkObjectId: 94,
                    bindingSpawnGeneration: 3,
                    bindingAuthorityGeneration: 2,
                    bindingDiscontinuityGeneration: 4,
                    controlRevision: 9,
                    baselineSequence: 12,
                    networkTick: 120,
                    hasPreviousNetworkTick: true,
                    previousNetworkTick: 119,
                    hasPreviousSequence: true,
                    previousSequence: 11,
                    acknowledgedControlRevision: 9,
                    acknowledgedSequence: 12,
                    baselineObserved: true,
                    ownershipObserved: true,
                    spawnLifetimeObserved: true,
                    participantIdentityBound: true,
                    tickContinuityVerified: true,
                    sequenceContinuityVerified: true,
                    acknowledgementObserved: true,
                    acknowledgementLineageVerified: true,
                    classification: classification,
                    restoreCapabilityClaimed: restoreCapabilityClaimed,
                    restoreStateCaptured: restoreCaptured,
                    restoreStateApplied: restoreApplied,
                    restoreStateValidated: restoreValidated,
                    restoreStateSucceeded: restoreSucceeded);

            Check("Complete observation-only evidence validates", () =>
            {
                var evidence = Ready();
                Require(GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error), error);
                Require(GlobalMotionNetworkBaselineContract.IsReady(evidence), "observation_evidence_rejected");
                Require(GlobalMotionNetworkBaselineContract.IsObservationOnly(evidence), "observation_classification_lost");
                Require(!GlobalMotionNetworkBaselineContract.CanClaimFullTransaction(evidence), "observation_claimed_restorable");
            });

            Check("NetworkObject identity is required", () =>
            {
                var evidence = ReadyWith(identity: new GlobalMotionNetworkBaselineIdentity(0, 94, 3, 0, 2));
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "network_baseline_identity_invalid", error);
            });

            Check("Binding lifetime must match session/object/spawn identity", () =>
            {
                var evidence = ReadyWith(bindingSpawnGeneration: 4);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "baseline_binding_lifetime_mismatch", error);
            });

            Check("Authority generation is required", () =>
            {
                var evidence = ReadyWith(bindingAuthorityGeneration: 0);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "baseline_authority_generation_required", error);
            });

            Check("Discontinuity generation is required", () =>
            {
                var evidence = ReadyWith(bindingDiscontinuityGeneration: 0);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "baseline_discontinuity_generation_required", error);
            });

            Check("Control revision is required", () =>
            {
                var evidence = ReadyWith(controlRevision: 0);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "baseline_control_revision_required", error);
            });

            Check("Network tick regression fails closed", () =>
            {
                var evidence = ReadyWith(networkTick: 119, previousNetworkTick: 119);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "network_tick_regressed", error);
            });

            Check("Unverified tick continuity fails closed", () =>
            {
                var evidence = ReadyWith(tickContinuityVerified: false);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "network_tick_continuity_unverified", error);
            });

            Check("Unverified sequence continuity fails closed", () =>
            {
                var evidence = ReadyWith(sequenceContinuityVerified: false);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "baseline_sequence_continuity_unverified", error);
            });

            Check("Ownership observation is required", () =>
            {
                var evidence = ReadyWith(ownershipObserved: false);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "ownership_observation_missing", error);
            });

            Check("Spawn lifetime observation is required", () =>
            {
                var evidence = ReadyWith(spawnLifetimeObserved: false);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "spawn_lifetime_observation_missing", error);
            });

            Check("Participant identity binding is required", () =>
            {
                var evidence = ReadyWith(participantIdentityBound: false);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "participant_identity_not_bound", error);
            });

            Check("Acknowledgement lineage is required", () =>
            {
                var evidence = ReadyWith(acknowledgementLineageVerified: false);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "baseline_acknowledgement_lineage_unverified", error);
            });

            Check("Acknowledgement revision cannot exceed control revision", () =>
            {
                var evidence = ReadyWith(acknowledgedControlRevision: 10);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "baseline_acknowledgement_revision_invalid", error);
            });

            Check("Observation-only evidence cannot claim restore capability", () =>
            {
                var evidence = ReadyWith(restoreCapabilityClaimed: true);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "observation_only_claims_restore_state", error);
            });

            Check("Restorable classification requires explicit capability", () =>
            {
                var evidence = ReadyWith(classification: GlobalMotionNetworkBaselineStateClassification.Restorable);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "restorable_capability_not_declared", error);
            });

            Check("Restorable classification requires complete restore evidence", () =>
            {
                var evidence = ReadyWith(classification: GlobalMotionNetworkBaselineStateClassification.Restorable, restoreCapabilityClaimed: true);
                Require(!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error) && error == "restorable_state_evidence_incomplete", error);
            });

            Check("Complete restorable evidence is distinguishable", () =>
            {
                var evidence = ReadyWith(
                    classification: GlobalMotionNetworkBaselineStateClassification.Restorable,
                    restoreCapabilityClaimed: true,
                    restoreCaptured: true,
                    restoreApplied: true,
                    restoreValidated: true,
                    restoreSucceeded: true);
                Require(GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error), error);
                Require(GlobalMotionNetworkBaselineContract.CanClaimFullTransaction(evidence), "restorable_evidence_rejected");
                Require(!GlobalMotionNetworkBaselineContract.IsObservationOnly(evidence), "restorable_marked_observation_only");
            });

            Check("Validation does not mutate evidence", () =>
            {
                var evidence = Ready();
                Require(GlobalMotionNetworkBaselineContract.TryValidate(evidence, out string error), error);
                Require(evidence.Identity.NetworkObjectId == 94 && evidence.Identity.SpawnGeneration == 3 &&
                    evidence.ControlRevision == 9 && evidence.BaselineSequence == 12 &&
                    evidence.AcknowledgedSequence == 12 && evidence.Classification == GlobalMotionNetworkBaselineStateClassification.ObservationOnly,
                    "evidence_mutated");
            });

            return report;

            GlobalMotionNetworkBaselineEvidence ReadyWith(
                string participantId = null,
                GlobalMotionNetworkBaselineIdentity? identity = null,
                ulong? bindingSessionId = null,
                ulong? bindingNetworkObjectId = null,
                ulong? bindingSpawnGeneration = null,
                ulong? bindingAuthorityGeneration = null,
                ulong? bindingDiscontinuityGeneration = null,
                ulong? controlRevision = null,
                uint? baselineSequence = null,
                ulong? networkTick = null,
                ulong? previousNetworkTick = null,
                bool? tickContinuityVerified = null,
                bool? sequenceContinuityVerified = null,
                ulong? acknowledgedControlRevision = null,
                uint? acknowledgedSequence = null,
                bool? ownershipObserved = null,
                bool? spawnLifetimeObserved = null,
                bool? participantIdentityBound = null,
                bool? acknowledgementLineageVerified = null,
                GlobalMotionNetworkBaselineStateClassification? classification = null,
                bool? restoreCapabilityClaimed = null,
                bool? restoreCaptured = null,
                bool? restoreApplied = null,
                bool? restoreValidated = null,
                bool? restoreSucceeded = null)
            {
                var baseEvidence = Ready(
                    classification ?? GlobalMotionNetworkBaselineStateClassification.ObservationOnly,
                    restoreCapabilityClaimed ?? false,
                    restoreCaptured ?? false,
                    restoreApplied ?? false,
                    restoreValidated ?? false,
                    restoreSucceeded ?? false);
                return new GlobalMotionNetworkBaselineEvidence(
                    participantId ?? baseEvidence.ParticipantId,
                    identity ?? baseEvidence.Identity,
                    bindingSessionId ?? baseEvidence.BindingSessionId,
                    bindingNetworkObjectId ?? baseEvidence.BindingNetworkObjectId,
                    bindingSpawnGeneration ?? baseEvidence.BindingSpawnGeneration,
                    bindingAuthorityGeneration ?? baseEvidence.BindingAuthorityGeneration,
                    bindingDiscontinuityGeneration ?? baseEvidence.BindingDiscontinuityGeneration,
                    controlRevision ?? baseEvidence.ControlRevision,
                    baselineSequence ?? baseEvidence.BaselineSequence,
                    networkTick ?? baseEvidence.NetworkTick,
                    true,
                    previousNetworkTick ?? baseEvidence.PreviousNetworkTick,
                    true,
                    baseEvidence.PreviousSequence,
                    acknowledgedControlRevision ?? baseEvidence.AcknowledgedControlRevision,
                    acknowledgedSequence ?? baseEvidence.AcknowledgedSequence,
                    true,
                    ownershipObserved ?? baseEvidence.OwnershipObserved,
                    spawnLifetimeObserved ?? baseEvidence.SpawnLifetimeObserved,
                    participantIdentityBound ?? baseEvidence.ParticipantIdentityBound,
                    tickContinuityVerified ?? baseEvidence.TickContinuityVerified,
                    sequenceContinuityVerified ?? baseEvidence.SequenceContinuityVerified,
                    true,
                    acknowledgementLineageVerified ?? baseEvidence.AcknowledgementLineageVerified,
                    classification ?? baseEvidence.Classification,
                    restoreCapabilityClaimed ?? baseEvidence.RestoreCapabilityClaimed,
                    restoreCaptured ?? baseEvidence.RestoreStateCaptured,
                    restoreApplied ?? baseEvidence.RestoreStateApplied,
                    restoreValidated ?? baseEvidence.RestoreStateValidated,
                    restoreSucceeded ?? baseEvidence.RestoreStateSucceeded);
            }
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}
