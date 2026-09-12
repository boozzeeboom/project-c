using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Protocol-owned producer seam for the NetworkBaseline native adapter.
    /// The host owns NGO state and must provide the reviewed reversible capability before
    /// this adapter reports NativeReady. No discovery or BootstrapScene installation occurs here.
    /// </summary>
    public interface IGlobalMotionNetworkBaselineTransactionHost
    {
        bool TryGetCapability(
            out GlobalMotionNetworkBaselineReversibleCapability capability,
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
    /// T-FO06BO: explicit NetworkBaseline adapter delegating all NGO ownership/lifetime work
    /// to a reviewed protocol-owned transaction host. It remains dormant until a real host is supplied.
    /// </summary>
    public sealed class GlobalMotionNetworkBaselineNativeAdapter : IGlobalMotionNativeAdapter
    {
        private readonly string _adapterId;
        private readonly IGlobalMotionNetworkBaselineTransactionHost _host;

        public GlobalMotionNetworkBaselineNativeAdapter(
            string adapterId,
            IGlobalMotionNetworkBaselineTransactionHost host)
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
                    GlobalMotionNetworkBaselineReversibleTransactionContract.CanAuthorizeNativeBaselineAdapter(capability);
                return new GlobalMotionNativeAdapterDescriptor(
                    _adapterId,
                    UnityStateRollbackCoverage.NetworkBaseline,
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
            evidence = default;
            if (!TryValidateRequest(request, out error)) return false;
            return _host.TryCapture(request, out evidence, out error);
        }

        public bool TryApply(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            return _host.TryApply(request, out error);
        }

        public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            return _host.TryRebuild(request, out error);
        }

        public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            return _host.TryValidate(request, out error);
        }

        public bool TryRestore(
            GlobalMotionRebaseRequest request,
            UnityStateRollbackEvidence evidence,
            out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            if (!string.Equals(evidence.TransactionId, request.TransactionId.ToString("N"), StringComparison.Ordinal) ||
                !string.Equals(evidence.ParticipantId, _adapterId, StringComparison.Ordinal) ||
                (evidence.CapturedCoverage & UnityStateRollbackCoverage.NetworkBaseline) == 0)
            {
                error = "network_baseline_rollback_identity_mismatch:" + _adapterId;
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
            if (!GlobalMotionNetworkBaselineReversibleTransactionContract.CanAuthorizeNativeBaselineAdapter(capability))
            {
                error = "network_baseline_reversible_capability_not_ready:" + _adapterId;
                return false;
            }
            if (!string.Equals(capability.ParticipantId, _adapterId, StringComparison.Ordinal))
            {
                error = "network_baseline_participant_identity_mismatch:" + _adapterId;
                return false;
            }
            return true;
        }
    }
}
