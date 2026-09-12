using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06BT: first concrete protocol-owned host implementation.
    /// It captures the live accepted baseline, but remains fail-closed until ownership,
    /// lifetime and baseline-generation restore are implemented by the reviewed NGO protocol.
    /// No automatic registration or scene installation occurs here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GlobalMotionReplicator))]
    public sealed class GlobalMotionNetworkBaselineProtocolHost : MonoBehaviour, IGlobalMotionNetworkBaselineTransactionHost
    {
        [SerializeField] private GlobalMotionReplicator _replicator;
        private string _capturedTransactionId;
        private GlobalMotionNetworkBaselineIdentity _capturedIdentity;
        private bool _hasCapture;

        public bool IsBound => ResolveReplicator() != null;
        public bool IsRestorable => false;

        private void Awake()
        {
            _replicator = ResolveReplicator();
        }

        public bool TryGetCapability(
            out GlobalMotionNetworkBaselineReversibleCapability capability,
            out string error)
        {
            capability = default;
            error = null;
            if (!TryReadObservation(out var evidence, out error)) return false;

            capability = new GlobalMotionNetworkBaselineReversibleCapability(
                evidence.ParticipantId,
                "protocol-host-observation-only",
                evidence.Identity,
                evidence.BindingAuthorityGeneration,
                evidence.BindingDiscontinuityGeneration,
                true,
                true,
                true,
                true,
                true,
                false,
                false,
                false,
                false);
            error = "network_baseline_restore_boundaries_missing";
            return true;
        }

        public bool TryCapture(
            GlobalMotionRebaseRequest request,
            out UnityStateRollbackEvidence evidence,
            out string error)
        {
            evidence = default;
            if (!request.TryValidate(out error)) return false;
            if (!TryReadObservation(out var baseline, out error)) return false;

            string participantId = baseline.ParticipantId;
            string snapshotId = "network-baseline:" + baseline.Identity.NetworkObjectId + ":" + baseline.Identity.SpawnGeneration;
            evidence = new UnityStateRollbackEvidence(
                request.TransactionId.ToString("N"),
                participantId,
                snapshotId,
                request.FrameGeneration,
                request.FrameGeneration,
                UnityStateRollbackCoverage.NetworkBaseline,
                UnityStateRollbackCoverage.NetworkBaseline,
                UnityStateRollbackCoverage.None,
                true,
                false,
                false,
                true,
                false,
                0,
                request.ExpectedParticipantCount);
            _capturedTransactionId = evidence.TransactionId;
            _capturedIdentity = baseline.Identity;
            _hasCapture = true;
            return true;
        }

        public bool TryApply(GlobalMotionRebaseRequest request, out string error)
        {
            error = "network_baseline_protocol_host_apply_not_reviewed";
            return false;
        }

        public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
        {
            error = "network_baseline_protocol_host_rebuild_not_reviewed";
            return false;
        }

        public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
        {
            error = null;
            if (!request.TryValidate(out error)) return false;
            if (!_hasCapture || !string.Equals(_capturedTransactionId, request.TransactionId.ToString("N"), StringComparison.Ordinal))
            {
                error = "network_baseline_capture_required";
                return false;
            }
            if (!TryReadObservation(out var current, out error)) return false;
            if (!current.Identity.MatchesLifetime(_capturedIdentity))
            {
                error = "network_baseline_lifetime_changed_since_capture";
                return false;
            }
            error = "network_baseline_restore_not_proven";
            return false;
        }

        public bool TryRestore(
            GlobalMotionRebaseRequest request,
            UnityStateRollbackEvidence evidence,
            out string error)
        {
            error = null;
            if (!request.TryValidate(out error)) return false;
            if (!_hasCapture || !string.Equals(_capturedTransactionId, request.TransactionId.ToString("N"), StringComparison.Ordinal) ||
                !string.Equals(evidence.TransactionId, request.TransactionId.ToString("N"), StringComparison.Ordinal) ||
                !evidence.SnapshotIdentityCaptured ||
                (evidence.CapturedCoverage & UnityStateRollbackCoverage.NetworkBaseline) == 0)
            {
                error = "network_baseline_capture_identity_mismatch";
                return false;
            }
            error = "network_baseline_ownership_lifetime_restore_not_implemented";
            return false;
        }

        private GlobalMotionReplicator ResolveReplicator()
        {
            if (_replicator == null) _replicator = GetComponent<GlobalMotionReplicator>();
            return _replicator;
        }

        
private bool TryReadObservation(out GlobalMotionNetworkBaselineEvidence evidence, out string error)
        {
            evidence = default;
            error = null;
            var replicator = ResolveReplicator();
            if (replicator == null) { error = "network_baseline_replicator_missing"; return false; }
            if (!replicator.IsServer || !replicator.IsSpawned || replicator.NetworkManager == null ||
                !replicator.NetworkManager.IsListening || !replicator.TryReadServerAcceptedMotion(out var snapshot, out _, out _))
            {
                error = "network_baseline_accepted_server_state_unavailable";
                return false;
            }

            var binding = snapshot.Binding;
            var identity = new GlobalMotionNetworkBaselineIdentity(
                binding.SessionId,
                binding.NetworkObjectId,
                binding.SpawnGeneration,
                replicator.OwnerClientId,
                binding.AuthorityGeneration);
            evidence = new GlobalMotionNetworkBaselineEvidence(
                "network-object:" + binding.NetworkObjectId,
                identity,
                binding.SessionId,
                binding.NetworkObjectId,
                binding.SpawnGeneration,
                binding.AuthorityGeneration,
                binding.DiscontinuityGeneration,
                replicator.Control.Revision,
                snapshot.Sequence,
                (ulong)Math.Max(1, replicator.NetworkManager.ServerTime.Tick),
                false,
                0,
                false,
                0,
                replicator.Control.Revision,
                snapshot.Sequence,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                GlobalMotionNetworkBaselineStateClassification.ObservationOnly,
                false,
                false,
                false,
                false,
                false);
            return GlobalMotionNetworkBaselineContract.TryValidate(evidence, out error);
        }
    }
}
