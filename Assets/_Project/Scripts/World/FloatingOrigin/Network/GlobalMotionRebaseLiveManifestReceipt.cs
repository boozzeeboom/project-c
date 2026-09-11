using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Immutable evidence that a reviewed participant manifest was eligible for publication.
    /// Issuing this value does not publish anything to a live peer or discover Unity objects.
    /// </summary>
    public readonly struct GlobalMotionRebaseLiveManifestReceipt
    {
        public string ManifestDigest { get; }
        public string PublisherIdentity { get; }
        public ulong SessionGeneration { get; }
        public ulong PublicationGeneration { get; }
        public int EntryCount { get; }

        public GlobalMotionRebaseLiveManifestReceipt(
            string manifestDigest,
            string publisherIdentity,
            ulong sessionGeneration,
            ulong publicationGeneration,
            int entryCount)
        {
            ManifestDigest = manifestDigest;
            PublisherIdentity = publisherIdentity;
            SessionGeneration = sessionGeneration;
            PublicationGeneration = publicationGeneration;
            EntryCount = entryCount;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(ManifestDigest) &&
            !string.IsNullOrWhiteSpace(PublisherIdentity) &&
            SessionGeneration > 0 && PublicationGeneration > 0 && EntryCount > 0;
    }

    /// <summary>
    /// Pure evidence source for a future live-manifest publisher.
    /// It requires every manifest entry to pass the existing admission policy.
    /// </summary>
    public static class GlobalMotionRebaseLiveManifestReceiptSource
    {
        public static bool TryIssue(
            GlobalMotionRebaseParticipantManifest manifest,
            GlobalMotionRebaseParticipantAdmissionEvidence admission,
            string publisherIdentity,
            ulong sessionGeneration,
            ulong publicationGeneration,
            out GlobalMotionRebaseLiveManifestReceipt receipt,
            out string error)
        {
            receipt = default;
            error = null;
            if (manifest == null) return Reject("participant_manifest_missing", out error);
            if (!manifest.TryValidateRequiredCoverage(out error)) return false;
            if (string.IsNullOrWhiteSpace(publisherIdentity) || publisherIdentity.Trim() != publisherIdentity)
                return Reject("publisher_identity_required", out error);
            if (sessionGeneration == 0) return Reject("session_generation_required", out error);
            if (publicationGeneration == 0) return Reject("publication_generation_required", out error);

            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                if (!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(
                    manifest.Entries[i], admission, out error))
                    return false;
            }

            receipt = new GlobalMotionRebaseLiveManifestReceipt(
                manifest.Digest,
                publisherIdentity,
                sessionGeneration,
                publicationGeneration,
                manifest.Count);
            return true;
        }

        public static bool TryValidate(
            GlobalMotionRebaseParticipantManifest manifest,
            GlobalMotionRebaseLiveManifestReceipt receipt,
            string expectedPublisherIdentity,
            out string error)
        {
            error = null;
            if (manifest == null) return Reject("participant_manifest_missing", out error);
            if (!receipt.IsValid) return Reject("live_manifest_receipt_invalid", out error);
            if (!manifest.HasDigest(receipt.ManifestDigest)) return Reject("manifest_digest_mismatch", out error);
            if (manifest.Count != receipt.EntryCount) return Reject("manifest_entry_count_mismatch", out error);
            if (!string.IsNullOrWhiteSpace(expectedPublisherIdentity) &&
                !string.Equals(expectedPublisherIdentity, receipt.PublisherIdentity, StringComparison.Ordinal))
                return Reject("manifest_publisher_mismatch", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
