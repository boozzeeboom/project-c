using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Aggregated fail-closed status for one reviewed manifest/adapter/session combination.
    /// It is an evidence result, not permission to mutate Unity state.
    /// </summary>
    public readonly struct GlobalMotionRebaseReadinessBundle
    {
        public string ManifestDigest { get; }
        public string SessionIdentity { get; }
        public int ParticipantCount { get; }
        public bool ManifestSessionProven { get; }
        public bool NativeAdaptersReady { get; }
        public bool RuntimeReady { get; }
        public string FailureReason { get; }

        private GlobalMotionRebaseReadinessBundle(
            string manifestDigest,
            string sessionIdentity,
            int participantCount,
            bool manifestSessionProven,
            bool nativeAdaptersReady,
            bool runtimeReady,
            string failureReason)
        {
            ManifestDigest = manifestDigest;
            SessionIdentity = sessionIdentity;
            ParticipantCount = participantCount;
            ManifestSessionProven = manifestSessionProven;
            NativeAdaptersReady = nativeAdaptersReady;
            RuntimeReady = runtimeReady;
            FailureReason = failureReason;
        }

        public bool IsReady => RuntimeReady && string.IsNullOrEmpty(FailureReason);

        public static GlobalMotionRebaseReadinessBundle Blocked(string reason)
        {
            return new GlobalMotionRebaseReadinessBundle(
                null, null, 0, false, false, false,
                string.IsNullOrWhiteSpace(reason) ? "readiness_blocked" : reason);
        }

        internal static GlobalMotionRebaseReadinessBundle Ready(
            GlobalMotionRebaseParticipantManifest manifest,
            GlobalMotionRebaseLiveManifestSessionEvidence sessionEvidence)
        {
            return new GlobalMotionRebaseReadinessBundle(
                manifest.Digest,
                sessionEvidence.SessionIdentity,
                manifest.Count,
                true,
                true,
                true,
                null);
        }
    }

    /// <summary>
    /// One ordered validator for the currently independent readiness gates.
    /// It never discovers participants, invokes adapters or connects the runtime driver.
    /// </summary>
    public static class GlobalMotionRebaseReadinessBundleBuilder
    {
        public static GlobalMotionRebaseReadinessBundle Build(
            GlobalMotionRebaseParticipantManifest manifest,
            GlobalMotionNativeAdapterSet adapters,
            GlobalMotionRebaseRuntimeReadinessEvidence readinessEvidence,
            GlobalMotionRebaseLiveManifestSessionEvidence sessionEvidence,
            string expectedSessionIdentity,
            string expectedPublisherIdentity)
        {
            if (manifest == null) return GlobalMotionRebaseReadinessBundle.Blocked("participant_manifest_missing");
            if (!GlobalMotionRebaseLiveManifestSessionEvidenceGate.TryValidate(
                    manifest,
                    sessionEvidence,
                    expectedSessionIdentity,
                    expectedPublisherIdentity,
                    out var error))
                return GlobalMotionRebaseReadinessBundle.Blocked("manifest_session:" + error);

            if (!GlobalMotionRebaseRuntimeReadinessGate.TryValidate(
                    manifest, adapters, readinessEvidence, out error))
                return GlobalMotionRebaseReadinessBundle.Blocked("runtime_readiness:" + error);

            return GlobalMotionRebaseReadinessBundle.Ready(manifest, sessionEvidence);
        }
    }
}
