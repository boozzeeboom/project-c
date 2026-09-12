using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Supplies a closed-world manifest and admission evidence from an owner-reviewed runtime source.
    /// The source must discover and validate real participants; the bridge never invents entries or readiness.
    /// </summary>
    public interface IGlobalMotionRebaseLiveManifestRuntimeSource
    {
        bool TryBuildManifest(
            out GlobalMotionRebaseParticipantManifest manifest,
            out GlobalMotionRebaseParticipantAdmissionEvidence admission,
            out string error);
    }

    /// <summary>
    /// T-FO06BC runtime bridge for live manifest publication and peer digest/count agreement.
    /// This bridge publishes evidence only. It does not install native adapters, authorize the runtime driver,
    /// invoke rebase phases or mutate world state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionRebaseLiveManifestRuntimeBridge : NetworkBehaviour
    {
        [SerializeField] private MonoBehaviour _sourceBehaviour;
        [SerializeField] private string _publisherIdentity = "ProjectC/Server";

        private readonly NetworkVariable<FixedString128Bytes> _manifestDigest =
            new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> _publisherIdentityNetwork =
            new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> _sessionIdentity =
            new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _sessionGeneration =
            new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _publicationGeneration =
            new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _entryCount =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _observedPeerCount =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _serverAccepted =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _peerDigestMatched =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _entryCountMatched =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _noManifestDrift =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly HashSet<ulong> _acknowledgedClients = new HashSet<ulong>();
        private GlobalMotionRebaseParticipantManifest _manifest;
        private GlobalMotionRebaseLiveManifestReceipt _receipt;
        private GlobalMotionRebaseParticipantAdmissionEvidence _admission;
        private bool _published;
        private bool _sourceRejected;
        private string _lastError;
        private ulong _nextPublicationGeneration;

        public bool IsPublished => _published && _receipt.IsValid;
        public bool IsPeerAgreementReady => IsPublished && _serverAccepted.Value && _peerDigestMatched.Value &&
            _entryCountMatched.Value && _noManifestDrift.Value && _observedPeerCount.Value > 0;
        public string LastError => _lastError;
        public int ObservedPeerCount => _observedPeerCount.Value;
        public string ManifestDigest => _manifestDigest.Value.ToString();
        public int EntryCount => _entryCount.Value;

        private IGlobalMotionRebaseLiveManifestRuntimeSource Source =>
            _sourceBehaviour as IGlobalMotionRebaseLiveManifestRuntimeSource;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                PublishServerManifest();
                return;
            }

            _manifestDigest.OnValueChanged += OnManifestChanged;
            _entryCount.OnValueChanged += OnEntryCountChanged;
            TryAcknowledgePublishedManifest();
        }

        public override void OnNetworkDespawn()
        {
            _manifestDigest.OnValueChanged -= OnManifestChanged;
            _entryCount.OnValueChanged -= OnEntryCountChanged;
            _acknowledgedClients.Clear();
            _manifest = null;
            _receipt = default;
            _published = false;
            base.OnNetworkDespawn();
        }

        public bool TryGetSessionEvidence(out GlobalMotionRebaseLiveManifestSessionEvidence evidence, out string error)
        {
            evidence = default;
            error = null;
            if (!IsPublished) return Reject("live_manifest_not_published", out error);

            evidence = new GlobalMotionRebaseLiveManifestSessionEvidence(
                _receipt,
                _sessionIdentity.Value.ToString(),
                _published,
                _serverAccepted.Value,
                _peerDigestMatched.Value,
                _entryCountMatched.Value,
                _noManifestDrift.Value,
                _observedPeerCount.Value);
            return GlobalMotionRebaseLiveManifestSessionEvidenceGate.TryValidate(
                _manifest,
                evidence,
                _sessionIdentity.Value.ToString(),
                _publisherIdentityNetwork.Value.ToString(),
                out error);
        }

        private void PublishServerManifest()
        {
            _lastError = null;
            _sourceRejected = false;
            if (Source == null)
            {
                RejectSource("runtime_manifest_source_missing");
                return;
            }
            if (string.IsNullOrWhiteSpace(_publisherIdentity) || _publisherIdentity.Trim() != _publisherIdentity)
            {
                RejectSource("publisher_identity_invalid");
                return;
            }
            if (!Source.TryBuildManifest(out _manifest, out _admission, out var sourceError) || _manifest == null)
            {
                RejectSource(sourceError ?? "runtime_manifest_source_rejected");
                return;
            }

            ulong sessionGeneration = CreateGeneration();
            ulong publicationGeneration = ++_nextPublicationGeneration;
            if (!GlobalMotionRebaseLiveManifestReceiptSource.TryIssue(
                    _manifest,
                    _admission,
                    _publisherIdentity,
                    sessionGeneration,
                    publicationGeneration,
                    out _receipt,
                    out var receiptError))
            {
                RejectSource(receiptError ?? "live_manifest_receipt_rejected");
                return;
            }

            _manifestDigest.Value = new FixedString128Bytes(_receipt.ManifestDigest);
            _publisherIdentityNetwork.Value = new FixedString128Bytes(_receipt.PublisherIdentity);
            _sessionGeneration.Value = _receipt.SessionGeneration;
            _publicationGeneration.Value = _receipt.PublicationGeneration;
            _sessionIdentity.Value = new FixedString128Bytes(BuildSessionIdentity(_receipt));
            _entryCount.Value = _receipt.EntryCount;
            _serverAccepted.Value = true;
            _noManifestDrift.Value = true;
            _acknowledgedClients.Clear();
            MarkAcknowledged(NetworkManager.ServerClientId, _receipt.ManifestDigest, _receipt.EntryCount);
            _published = true;
            EmitEvidence("Published", "digest=" + _receipt.ManifestDigest + ";entries=" + _receipt.EntryCount +
                ";session=" + _sessionIdentity.Value + ";publication=" + _receipt.PublicationGeneration);
        }

        private void OnManifestChanged(FixedString128Bytes previous, FixedString128Bytes current)
        {
            TryAcknowledgePublishedManifest();
        }

        private void OnEntryCountChanged(int previous, int current)
        {
            TryAcknowledgePublishedManifest();
        }

        private void TryAcknowledgePublishedManifest()
        {
            if (IsServer || !IsSpawned || string.IsNullOrWhiteSpace(_manifestDigest.Value.ToString()) || _entryCount.Value <= 0)
                return;
            AcknowledgeManifestServerRpc(
                _manifestDigest.Value,
                _entryCount.Value,
                _sessionGeneration.Value,
                _publicationGeneration.Value);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void AcknowledgeManifestServerRpc(
            FixedString128Bytes digest,
            int entryCount,
            ulong sessionGeneration,
            ulong publicationGeneration,
            RpcParams rpcParams = default)
        {
            if (!IsServer || !_published || _receipt.SessionGeneration != sessionGeneration ||
                _receipt.PublicationGeneration != publicationGeneration)
                return;
            MarkAcknowledged(rpcParams.Receive.SenderClientId, digest.ToString(), entryCount);
        }

        private void MarkAcknowledged(ulong clientId, string digest, int entryCount)
        {
            if (!_published && clientId != NetworkManager.ServerClientId) return;
            bool digestMatches = string.Equals(digest, _receipt.ManifestDigest, StringComparison.Ordinal);
            bool countMatches = entryCount == _receipt.EntryCount;
            if (!digestMatches || !countMatches)
            {
                _noManifestDrift.Value = false;
                EmitEvidence("PeerRejected", "client=" + clientId + ";digestMatch=" + digestMatches + ";countMatch=" + countMatches);
                return;
            }

            _acknowledgedClients.Add(clientId);
            _observedPeerCount.Value = _acknowledgedClients.Count;
            _peerDigestMatched.Value = true;
            _entryCountMatched.Value = true;
            _noManifestDrift.Value = true;
            EmitEvidence("PeerAccepted", "client=" + clientId + ";peers=" + _observedPeerCount.Value +
                ";digest=" + _receipt.ManifestDigest + ";entries=" + _receipt.EntryCount);
        }

        private void RejectSource(string error)
        {
            _sourceRejected = true;
            _lastError = error;
            _published = false;
            _serverAccepted.Value = false;
            _peerDigestMatched.Value = false;
            _entryCountMatched.Value = false;
            _noManifestDrift.Value = false;
            Debug.LogError("[T-FO06BC] Live manifest publication rejected: " + error, this);
        }

        private static ulong CreateGeneration()
        {
            long ticks = DateTime.UtcNow.Ticks;
            return ticks > 0 ? (ulong)ticks : 1UL;
        }

        private static string BuildSessionIdentity(GlobalMotionRebaseLiveManifestReceipt receipt)
        {
            return receipt.PublisherIdentity + "/session/" + receipt.SessionGeneration;
        }

        private void EmitEvidence(string phase, string detail)
        {
            Debug.Log("[T-FO06BC] manifest." + phase + ";" + detail, this);
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
