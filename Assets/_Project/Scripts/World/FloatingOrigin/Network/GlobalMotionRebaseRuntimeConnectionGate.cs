using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Evidence token for a reviewed connection between a ready bundle and a sealed native adapter set.
    /// Creating it does not connect a driver, invoke adapters or mutate Unity state.
    /// </summary>
    public readonly struct GlobalMotionRebaseRuntimeConnectionEvidence
    {
        public string ManifestDigest { get; }
        public string SessionIdentity { get; }
        public int AdapterCount { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ManifestDigest) &&
            !string.IsNullOrWhiteSpace(SessionIdentity) && AdapterCount > 0;

        internal GlobalMotionRebaseRuntimeConnectionEvidence(
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
    /// Pure gate before a future runtime-driver connection. It returns evidence only and
    /// never invokes adapters or mutates Unity state.
    /// </summary>
    public static class GlobalMotionRebaseRuntimeConnectionGate
    {
        public static bool TryBuildEvidence(
            GlobalMotionRebaseReadinessBundle readiness,
            GlobalMotionNativeAdapterSet adapters,
            out GlobalMotionRebaseRuntimeConnectionEvidence evidence,
            out string error)
        {
            evidence = default;
            error = null;

            if (!readiness.IsReady)
                return Reject(
                    "readiness_bundle_not_ready:" +
                    (readiness.FailureReason ?? "unknown"),
                    out error);

            if (adapters == null)
                return Reject("native_adapter_set_missing", out error);

            if (!adapters.TryValidateReady(out error))
                return false;

            if (string.IsNullOrWhiteSpace(readiness.ManifestDigest))
                return Reject("readiness_manifest_digest_missing", out error);

            if (string.IsNullOrWhiteSpace(readiness.SessionIdentity))
                return Reject("readiness_session_identity_missing", out error);

            evidence = new GlobalMotionRebaseRuntimeConnectionEvidence(
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
