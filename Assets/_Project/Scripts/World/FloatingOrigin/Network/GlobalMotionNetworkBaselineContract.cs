using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionNetworkBaselineStateClassification : byte
    {
        ObservationOnly = 1,
        Restorable = 2
    }

    /// <summary>
    /// Pure identity envelope for one NGO NetworkObject lifetime and ownership epoch.
    /// It records protocol identity only; it does not inspect or mutate NGO state.
    /// </summary>
    public readonly struct GlobalMotionNetworkBaselineIdentity
    {
        public ulong SessionId { get; }
        public ulong NetworkObjectId { get; }
        public ulong SpawnGeneration { get; }
        public ulong OwnerClientId { get; }
        public ulong OwnershipGeneration { get; }

        public GlobalMotionNetworkBaselineIdentity(
            ulong sessionId,
            ulong networkObjectId,
            ulong spawnGeneration,
            ulong ownerClientId,
            ulong ownershipGeneration)
        {
            SessionId = sessionId;
            NetworkObjectId = networkObjectId;
            SpawnGeneration = spawnGeneration;
            OwnerClientId = ownerClientId;
            OwnershipGeneration = ownershipGeneration;
        }

        public bool IsValid => SessionId != 0 && SpawnGeneration != 0 && OwnershipGeneration != 0;

        public bool MatchesLifetime(GlobalMotionNetworkBaselineIdentity other)
        {
            return SessionId == other.SessionId &&
                NetworkObjectId == other.NetworkObjectId &&
                SpawnGeneration == other.SpawnGeneration;
        }

        public bool MatchesOwnership(GlobalMotionNetworkBaselineIdentity other)
        {
            return MatchesLifetime(other) &&
                OwnerClientId == other.OwnerClientId &&
                OwnershipGeneration == other.OwnershipGeneration;
        }
    }

    /// <summary>
    /// Pure evidence boundary for NGO baseline identity, continuity and acknowledgement lineage.
    /// It does not restore ownership/lifetime state and never claims that observation is rollback-capable.
    /// </summary>
    public readonly struct GlobalMotionNetworkBaselineEvidence
    {
        public string ParticipantId { get; }
        public GlobalMotionNetworkBaselineIdentity Identity { get; }
        public ulong BindingSessionId { get; }
        public ulong BindingNetworkObjectId { get; }
        public ulong BindingSpawnGeneration { get; }
        public ulong BindingAuthorityGeneration { get; }
        public ulong BindingDiscontinuityGeneration { get; }
        public ulong ControlRevision { get; }
        public uint BaselineSequence { get; }
        public ulong NetworkTick { get; }
        public bool HasPreviousNetworkTick { get; }
        public ulong PreviousNetworkTick { get; }
        public bool HasPreviousSequence { get; }
        public uint PreviousSequence { get; }
        public ulong AcknowledgedControlRevision { get; }
        public uint AcknowledgedSequence { get; }
        public bool BaselineObserved { get; }
        public bool OwnershipObserved { get; }
        public bool SpawnLifetimeObserved { get; }
        public bool ParticipantIdentityBound { get; }
        public bool TickContinuityVerified { get; }
        public bool SequenceContinuityVerified { get; }
        public bool AcknowledgementObserved { get; }
        public bool AcknowledgementLineageVerified { get; }
        public GlobalMotionNetworkBaselineStateClassification Classification { get; }
        public bool RestoreCapabilityClaimed { get; }
        public bool RestoreStateCaptured { get; }
        public bool RestoreStateApplied { get; }
        public bool RestoreStateValidated { get; }
        public bool RestoreStateSucceeded { get; }

        public GlobalMotionNetworkBaselineEvidence(
            string participantId,
            GlobalMotionNetworkBaselineIdentity identity,
            ulong bindingSessionId,
            ulong bindingNetworkObjectId,
            ulong bindingSpawnGeneration,
            ulong bindingAuthorityGeneration,
            ulong bindingDiscontinuityGeneration,
            ulong controlRevision,
            uint baselineSequence,
            ulong networkTick,
            bool hasPreviousNetworkTick,
            ulong previousNetworkTick,
            bool hasPreviousSequence,
            uint previousSequence,
            ulong acknowledgedControlRevision,
            uint acknowledgedSequence,
            bool baselineObserved,
            bool ownershipObserved,
            bool spawnLifetimeObserved,
            bool participantIdentityBound,
            bool tickContinuityVerified,
            bool sequenceContinuityVerified,
            bool acknowledgementObserved,
            bool acknowledgementLineageVerified,
            GlobalMotionNetworkBaselineStateClassification classification,
            bool restoreCapabilityClaimed,
            bool restoreStateCaptured,
            bool restoreStateApplied,
            bool restoreStateValidated,
            bool restoreStateSucceeded)
        {
            ParticipantId = participantId;
            Identity = identity;
            BindingSessionId = bindingSessionId;
            BindingNetworkObjectId = bindingNetworkObjectId;
            BindingSpawnGeneration = bindingSpawnGeneration;
            BindingAuthorityGeneration = bindingAuthorityGeneration;
            BindingDiscontinuityGeneration = bindingDiscontinuityGeneration;
            ControlRevision = controlRevision;
            BaselineSequence = baselineSequence;
            NetworkTick = networkTick;
            HasPreviousNetworkTick = hasPreviousNetworkTick;
            PreviousNetworkTick = previousNetworkTick;
            HasPreviousSequence = hasPreviousSequence;
            PreviousSequence = previousSequence;
            AcknowledgedControlRevision = acknowledgedControlRevision;
            AcknowledgedSequence = acknowledgedSequence;
            BaselineObserved = baselineObserved;
            OwnershipObserved = ownershipObserved;
            SpawnLifetimeObserved = spawnLifetimeObserved;
            ParticipantIdentityBound = participantIdentityBound;
            TickContinuityVerified = tickContinuityVerified;
            SequenceContinuityVerified = sequenceContinuityVerified;
            AcknowledgementObserved = acknowledgementObserved;
            AcknowledgementLineageVerified = acknowledgementLineageVerified;
            Classification = classification;
            RestoreCapabilityClaimed = restoreCapabilityClaimed;
            RestoreStateCaptured = restoreStateCaptured;
            RestoreStateApplied = restoreStateApplied;
            RestoreStateValidated = restoreStateValidated;
            RestoreStateSucceeded = restoreStateSucceeded;
        }
    }

    public static class GlobalMotionNetworkBaselineContract
    {
        public static bool TryValidate(GlobalMotionNetworkBaselineEvidence evidence, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(evidence.ParticipantId))
                return Reject("participant_id_required", out error);
            if (!evidence.Identity.IsValid)
                return Reject("network_baseline_identity_invalid", out error);
            if (evidence.BindingSessionId != evidence.Identity.SessionId ||
                evidence.BindingNetworkObjectId != evidence.Identity.NetworkObjectId ||
                evidence.BindingSpawnGeneration != evidence.Identity.SpawnGeneration)
                return Reject("baseline_binding_lifetime_mismatch", out error);
            if (evidence.BindingAuthorityGeneration == 0)
                return Reject("baseline_authority_generation_required", out error);
            if (evidence.BindingDiscontinuityGeneration == 0)
                return Reject("baseline_discontinuity_generation_required", out error);
            if (evidence.ControlRevision == 0)
                return Reject("baseline_control_revision_required", out error);
            if (evidence.NetworkTick < 1)
                return Reject("baseline_network_tick_required", out error);
            if (evidence.HasPreviousNetworkTick && evidence.NetworkTick <= evidence.PreviousNetworkTick)
                return Reject("network_tick_regressed", out error);
            if (evidence.HasPreviousSequence && !evidence.SequenceContinuityVerified)
                return Reject("baseline_sequence_continuity_unverified", out error);
            if (evidence.HasPreviousNetworkTick && !evidence.TickContinuityVerified)
                return Reject("network_tick_continuity_unverified", out error);
            if (!evidence.BaselineObserved)
                return Reject("baseline_observation_missing", out error);
            if (!evidence.OwnershipObserved)
                return Reject("ownership_observation_missing", out error);
            if (!evidence.SpawnLifetimeObserved)
                return Reject("spawn_lifetime_observation_missing", out error);
            if (!evidence.ParticipantIdentityBound)
                return Reject("participant_identity_not_bound", out error);
            if (!evidence.AcknowledgementObserved)
                return Reject("baseline_acknowledgement_missing", out error);
            if (!evidence.AcknowledgementLineageVerified)
                return Reject("baseline_acknowledgement_lineage_unverified", out error);
            if (evidence.AcknowledgedControlRevision == 0 || evidence.AcknowledgedControlRevision > evidence.ControlRevision)
                return Reject("baseline_acknowledgement_revision_invalid", out error);
            if (evidence.AcknowledgedSequence != evidence.BaselineSequence && !evidence.SequenceContinuityVerified)
                return Reject("baseline_acknowledgement_sequence_unrelated", out error);

            switch (evidence.Classification)
            {
                case GlobalMotionNetworkBaselineStateClassification.ObservationOnly:
                    if (evidence.RestoreCapabilityClaimed || evidence.RestoreStateCaptured || evidence.RestoreStateApplied ||
                        evidence.RestoreStateValidated || evidence.RestoreStateSucceeded)
                        return Reject("observation_only_claims_restore_state", out error);
                    return true;

                case GlobalMotionNetworkBaselineStateClassification.Restorable:
                    if (!evidence.RestoreCapabilityClaimed)
                        return Reject("restorable_capability_not_declared", out error);
                    if (!evidence.RestoreStateCaptured || !evidence.RestoreStateApplied ||
                        !evidence.RestoreStateValidated || !evidence.RestoreStateSucceeded)
                        return Reject("restorable_state_evidence_incomplete", out error);
                    return true;

                default:
                    return Reject("baseline_state_classification_invalid", out error);
            }
        }

        public static bool IsReady(GlobalMotionNetworkBaselineEvidence evidence)
        {
            return TryValidate(evidence, out _);
        }

        public static bool IsObservationOnly(GlobalMotionNetworkBaselineEvidence evidence)
        {
            return evidence.Classification == GlobalMotionNetworkBaselineStateClassification.ObservationOnly &&
                TryValidate(evidence, out _);
        }

        public static bool CanClaimFullTransaction(GlobalMotionNetworkBaselineEvidence evidence)
        {
            return evidence.Classification == GlobalMotionNetworkBaselineStateClassification.Restorable &&
                TryValidate(evidence, out _);
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
