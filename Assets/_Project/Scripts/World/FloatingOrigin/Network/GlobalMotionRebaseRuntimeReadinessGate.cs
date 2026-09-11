using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Runtime-independent readiness evidence required before native adapters may be connected to the driver.
    /// </summary>
    public readonly struct GlobalMotionRebaseRuntimeReadinessEvidence
    {
        public bool LiveManifestPublished { get; }
        public GlobalMotionRebaseParticipantAdmissionEvidence Admission { get; }

        public GlobalMotionRebaseRuntimeReadinessEvidence(
            bool liveManifestPublished,
            GlobalMotionRebaseParticipantAdmissionEvidence admission)
        {
            LiveManifestPublished = liveManifestPublished;
            Admission = admission;
        }
    }

    /// <summary>
    /// Fail-closed gate for connecting a sealed native adapter set to the runtime driver.
    /// It validates reviewed manifest coverage, per-entry admission evidence and adapter readiness,
    /// but never discovers participants or mutates Unity state.
    /// </summary>
    public static class GlobalMotionRebaseRuntimeReadinessGate
    {
        public static bool TryValidate(
            GlobalMotionRebaseParticipantManifest manifest,
            GlobalMotionNativeAdapterSet adapters,
            GlobalMotionRebaseRuntimeReadinessEvidence evidence,
            out string error)
        {
            error = null;
            if (manifest == null) return Reject("participant_manifest_missing", out error);
            if (!evidence.LiveManifestPublished) return Reject("live_manifest_not_published", out error);
            if (!manifest.TryValidateRequiredCoverage(out error)) return false;
            if (!adapters.TryValidateReady(out error)) return false;

            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                if (!GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit(
                    manifest.Entries[i], evidence.Admission, out error))
                    return false;
            }

            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
