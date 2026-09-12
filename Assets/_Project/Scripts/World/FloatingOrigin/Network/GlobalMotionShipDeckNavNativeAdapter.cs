using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public interface IGlobalMotionShipDeckNavTransactionHost
    {
        bool TryGetCapability(
            out GlobalMotionRebaseShipDeckNavLifecycleEvidence capability,
            out string error);

        bool TryCapture(
            GlobalMotionRebaseRequest request,
            out UnityStateRollbackEvidence evidence,
            out string error);

        bool TryApply(GlobalMotionRebaseRequest request, out string error);
        bool TryRebuild(GlobalMotionRebaseRequest request, out string error);
        bool TryValidate(GlobalMotionRebaseRequest request, out string error);
        bool TryRestore(
            GlobalMotionRebaseRequest request,
            UnityStateRollbackEvidence evidence,
            out string error);
    }

    /// <summary>
    /// T-FO06BX: explicit ShipDeckNav adapter delegating transaction work to a reviewed host.
    /// The adapter declares the required shape but remains NativeReady=false until synchronous
    /// NavMesh rebuild, passenger snapshot and restore boundaries are proven by the host.
    /// </summary>
    public sealed class GlobalMotionShipDeckNavNativeAdapter : IGlobalMotionNativeAdapter
    {
        private readonly string _adapterId;
        private readonly IGlobalMotionShipDeckNavTransactionHost _host;

        public GlobalMotionShipDeckNavNativeAdapter(
            string adapterId,
            IGlobalMotionShipDeckNavTransactionHost host)
        {
            if (string.IsNullOrWhiteSpace(adapterId))
                throw new ArgumentException("A stable adapter id is required.", nameof(adapterId));
            _adapterId = adapterId.Trim();
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public GlobalMotionNativeAdapterDescriptor Descriptor
        {
            get
            {
                bool nativeReady = _host.TryGetCapability(out var capability, out _) &&
                    GlobalMotionRebaseShipDeckNavLifecycleContract.TryValidateReady(capability, out _);
                return new GlobalMotionNativeAdapterDescriptor(
                    _adapterId,
                    UnityStateRollbackCoverage.ShipDeckNav,
                    GlobalMotionNativeAdapterCapability.FullTransaction,
                    true,
                    nativeReady);
            }
        }

        public bool TryCapture(
            GlobalMotionRebaseRequest request,
            out UnityStateRollbackEvidence evidence,
            out string error)
        {
            if (!TryValidateRequest(request, out error))
            {
                evidence = default;
                return false;
            }
            return _host.TryCapture(request, out evidence, out error);
        }

        public bool TryApply(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error))
                return false;
            return _host.TryApply(request, out error);
        }

        public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error))
                return false;
            return _host.TryRebuild(request, out error);
        }

        public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error))
                return false;
            return _host.TryValidate(request, out error);
        }

        public bool TryRestore(
            GlobalMotionRebaseRequest request,
            UnityStateRollbackEvidence evidence,
            out string error)
        {
            if (!TryValidateRequest(request, out error))
                return false;
            if (!string.Equals(evidence.TransactionId, request.TransactionId.ToString("N"), StringComparison.Ordinal) ||
                !string.Equals(evidence.ParticipantId, _adapterId, StringComparison.Ordinal) ||
                (evidence.CapturedCoverage & UnityStateRollbackCoverage.ShipDeckNav) == 0)
            {
                error = "ship_deck_nav_rollback_identity_mismatch:" + _adapterId;
                return false;
            }
            return _host.TryRestore(request, evidence, out error);
        }

        private bool TryValidateRequest(GlobalMotionRebaseRequest request, out string error)
        {
            error = null;
            if (!request.IsValid)
            {
                error = "rebase_request_invalid";
                return false;
            }
            if (!_host.TryGetCapability(out var capability, out error))
                return false;
            if (!GlobalMotionRebaseShipDeckNavLifecycleContract.TryValidateReady(capability, out error))
            {
                error = "ship_deck_nav_lifecycle_not_ready:" + _adapterId + ":" + error;
                return false;
            }
            return true;
        }
    }
}