using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// User/runtime-supplied evidence that a manifest receipt was observed in one concrete session.
    /// This value is an admission record only; it does not publish or discover participants.
    /// </summary>
    public readonly struct GlobalMotionRebaseLiveManifestSessionEvidence
    {
        public GlobalMotionRebaseLiveManifestReceipt Receipt { get; }
        public string SessionIdentity { get; }
        public bool PublisherObserved { get; }
        public bool ServerAccepted { get; }
        public bool PeerDigestMatched { get; }
        public bool EntryCountMatched { get; }
        public bool NoManifestDrift { get; }
        public int ObservedPeerCount { get; }

        public GlobalMotionRebaseLiveManifestSessionEvidence(
            GlobalMotionRebaseLiveManifestReceipt receipt,
            string sessionIdentity,
            bool publisherObserved,
            bool serverAccepted,
            bool peerDigestMatched,
            bool entryCountMatched,
            bool noManifestDrift,
            int observedPeerCount)
        {
            Receipt = receipt;
            SessionIdentity = sessionIdentity;
            PublisherObserved = publisherObserved;
            ServerAccepted = serverAccepted;
            PeerDigestMatched = peerDigestMatched;
            EntryCountMatched = entryCountMatched;
            NoManifestDrift = noManifestDrift;
            ObservedPeerCount = observedPeerCount;
        }
    }

    /// <summary>
    /// Fail-closed validator for a live manifest session observation.
    /// It requires explicit runtime evidence and never treats a pure receipt as live publication.
    /// </summary>
    public static class GlobalMotionRebaseLiveManifestSessionEvidenceGate
    {
        public static bool TryValidate(
            GlobalMotionRebaseParticipantManifest manifest,
            GlobalMotionRebaseLiveManifestSessionEvidence evidence,
            string expectedSessionIdentity,
            string expectedPublisherIdentity,
            out string error)
        {
            error = null;
            if (manifest == null) return Reject("participant_manifest_missing", out error);
            if (!evidence.Receipt.IsValid) return Reject("live_manifest_receipt_invalid", out error);
            if (!GlobalMotionRebaseLiveManifestReceiptSource.TryValidate(
                    manifest, evidence.Receipt, expectedPublisherIdentity, out error))
                return false;
            if (string.IsNullOrWhiteSpace(evidence.SessionIdentity))
                return Reject("session_identity_required", out error);
            if (!string.IsNullOrWhiteSpace(expectedSessionIdentity) &&
                !string.Equals(expectedSessionIdentity, evidence.SessionIdentity, StringComparison.Ordinal))
                return Reject("session_identity_mismatch", out error);
            if (!evidence.PublisherObserved) return Reject("manifest_publisher_observation_missing", out error);
            if (!evidence.ServerAccepted) return Reject("manifest_server_acceptance_missing", out error);
            if (!evidence.PeerDigestMatched) return Reject("manifest_peer_digest_mismatch", out error);
            if (!evidence.EntryCountMatched) return Reject("manifest_peer_entry_count_mismatch", out error);
            if (!evidence.NoManifestDrift) return Reject("manifest_drift_detected", out error);
            if (evidence.ObservedPeerCount <= 0) return Reject("manifest_observed_peer_count_required", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
