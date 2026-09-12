using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06BU: concrete MonoBehaviour binding seam for the NetworkBaseline native adapter.
    /// It exposes the existing adapter through a serialized protocol host, but remains
    /// fail-closed while the host cannot provide a Restorable capability.
    /// No registration or provider binding occurs here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GlobalMotionNetworkBaselineProtocolHost))]
    public sealed class GlobalMotionNetworkBaselineNativeAdapterSource : MonoBehaviour,
        IGlobalMotionNativeAdapter
    {
        [SerializeField] private GlobalMotionNetworkBaselineProtocolHost _host;
        [SerializeField] private string _adapterId = "network-baseline:UNBOUND";

        private GlobalMotionNetworkBaselineNativeAdapter _adapter;

        public bool IsBound => ResolveHost() != null;

        private void Awake()
        {
            ResolveHost();
        }

        public GlobalMotionNativeAdapterDescriptor Descriptor
        {
            get
            {
                if (!TryGetAdapter(out var adapter, out _))
                    return default;
                return adapter.Descriptor;
            }
        }

        public bool TryCapture(
            GlobalMotionRebaseRequest request,
            out UnityStateRollbackEvidence evidence,
            out string error)
        {
            evidence = default;
            if (!TryGetAdapter(out var adapter, out error)) return false;
            return adapter.TryCapture(request, out evidence, out error);
        }

        public bool TryApply(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryGetAdapter(out var adapter, out error)) return false;
            return adapter.TryApply(request, out error);
        }

        public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryGetAdapter(out var adapter, out error)) return false;
            return adapter.TryRebuild(request, out error);
        }

        public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryGetAdapter(out var adapter, out error)) return false;
            return adapter.TryValidate(request, out error);
        }

        public bool TryRestore(
            GlobalMotionRebaseRequest request,
            UnityStateRollbackEvidence evidence,
            out string error)
        {
            if (!TryGetAdapter(out var adapter, out error)) return false;
            return adapter.TryRestore(request, evidence, out error);
        }

        private bool TryGetAdapter(
            out GlobalMotionNetworkBaselineNativeAdapter adapter,
            out string error)
        {
            adapter = null;
            error = null;
            var host = ResolveHost();
            if (host == null)
            {
                error = "network_baseline_protocol_host_missing";
                return false;
            }
            if (string.IsNullOrWhiteSpace(_adapterId) || _adapterId.Trim() != _adapterId)
            {
                error = "network_baseline_adapter_id_invalid";
                return false;
            }

            if (_adapter == null)
                _adapter = new GlobalMotionNetworkBaselineNativeAdapter(_adapterId, host);
            adapter = _adapter;
            return true;
        }

        private GlobalMotionNetworkBaselineProtocolHost ResolveHost()
        {
            if (_host == null)
                _host = GetComponent<GlobalMotionNetworkBaselineProtocolHost>();
            return _host;
        }
    }
}
