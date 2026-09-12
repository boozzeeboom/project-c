using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Pure authorization evidence for a future serial runtime connection.
    /// It binds one previously reviewed connection evidence token to the exact current
    /// readiness bundle and sealed adapter set without installing or invoking anything.
    /// </summary>
    public readonly struct GlobalMotionRebaseRuntimeConnectionAuthorization
    {
        public string ManifestDigest { get; }
        public string SessionIdentity { get; }
        public int AdapterCount { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ManifestDigest) &&
            !string.IsNullOrWhiteSpace(SessionIdentity) && AdapterCount > 0;

        internal GlobalMotionRebaseRuntimeConnectionAuthorization(
            string manifestDigest,
            string sessionIdentity,
            int adapterCount)
        {
            ManifestDigest = manifestDigest;
            SessionIdentity = sessionIdentity;
            AdapterCount = adapterCount;
        }
    }

    /// <summary>
    /// Fail-closed binding gate before a future runtime installation step.
    /// It requires exact evidence identity/count agreement and performs no Unity mutation.
    /// </summary>
    public static class GlobalMotionRebaseRuntimeConnectionAuthorizationGate
    {
        public static bool TryAuthorize(
            GlobalMotionRebaseRuntimeConnectionEvidence evidence,
            GlobalMotionRebaseReadinessBundle readiness,
            GlobalMotionNativeAdapterSet adapters,
            out GlobalMotionRebaseRuntimeConnectionAuthorization authorization,
            out string error)
        {
            authorization = default;
            error = null;

            if (!evidence.IsValid)
                return Reject("connection_evidence_invalid", out error);
            if (!readiness.IsReady)
                return Reject(
                    "readiness_bundle_not_ready:" +
                    (readiness.FailureReason ?? "unknown"),
                    out error);
            if (adapters == null)
                return Reject("native_adapter_set_missing", out error);
            if (!adapters.TryValidateReady(out error))
                return false;
            if (!string.Equals(evidence.ManifestDigest, readiness.ManifestDigest, StringComparison.Ordinal))
                return Reject("connection_manifest_digest_mismatch", out error);
            if (!string.Equals(evidence.SessionIdentity, readiness.SessionIdentity, StringComparison.Ordinal))
                return Reject("connection_session_identity_mismatch", out error);
            if (evidence.AdapterCount != adapters.Count)
                return Reject("connection_adapter_count_mismatch", out error);

            authorization = new GlobalMotionRebaseRuntimeConnectionAuthorization(
                readiness.ManifestDigest,
                readiness.SessionIdentity,
                adapters.Count);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
