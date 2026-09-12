using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Pure intent for a future serial runtime connection installation.
    /// It records explicit user control but does not install a driver or invoke adapters.
    /// </summary>
    public readonly struct GlobalMotionRebaseRuntimeInstallationIntent
    {
        public Guid InstallationId { get; }
        public string ManifestDigest { get; }
        public string SessionIdentity { get; }
        public int AdapterCount { get; }
        public string Reason { get; }
        public bool IsUserControlled => true;
        public bool IsValid => InstallationId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(ManifestDigest) &&
            !string.IsNullOrWhiteSpace(SessionIdentity) &&
            AdapterCount > 0 &&
            !string.IsNullOrWhiteSpace(Reason);

        internal GlobalMotionRebaseRuntimeInstallationIntent(
            Guid installationId,
            string manifestDigest,
            string sessionIdentity,
            int adapterCount,
            string reason)
        {
            InstallationId = installationId;
            ManifestDigest = manifestDigest;
            SessionIdentity = sessionIdentity;
            AdapterCount = adapterCount;
            Reason = reason;
        }
    }

    /// <summary>
    /// Final pure boundary before a separately reviewed runtime installation step.
    /// Automatic triggers and empty reasons are rejected; no runtime state is changed.
    /// </summary>
    public static class GlobalMotionRebaseRuntimeInstallationBoundary
    {
        public static bool TryCreateUserControlledIntent(
            GlobalMotionRebaseRuntimeConnectionAuthorization authorization,
            string reason,
            out GlobalMotionRebaseRuntimeInstallationIntent intent,
            out string error)
        {
            intent = default;
            error = null;
            if (!authorization.IsValid)
                return Reject("connection_authorization_invalid", out error);
            if (string.IsNullOrWhiteSpace(reason))
                return Reject("installation_reason_missing", out error);

            intent = new GlobalMotionRebaseRuntimeInstallationIntent(
                Guid.NewGuid(),
                authorization.ManifestDigest,
                authorization.SessionIdentity,
                authorization.AdapterCount,
                reason.Trim());
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
