using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06BX: concrete component source for the ShipDeckNav adapter seam.
    /// It binds only to the protocol host and never self-registers in the native adapter set.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GlobalMotionShipDeckNavProtocolHost))]
    public sealed class GlobalMotionShipDeckNavNativeAdapterSource : MonoBehaviour, IGlobalMotionNativeAdapter
    {
        [SerializeField] private GlobalMotionShipDeckNavProtocolHost _host;
        [SerializeField] private string _adapterId = "ship-deck-nav";

        public bool IsBound => ResolveHost() != null;

        public GlobalMotionNativeAdapterDescriptor Descriptor =>
            new GlobalMotionShipDeckNavNativeAdapter(ResolveAdapterId(), ResolveHost()).Descriptor;

        public bool TryCapture(
            GlobalMotionRebaseRequest request,
            out UnityStateRollbackEvidence evidence,
            out string error) =>
            ResolveAdapter().TryCapture(request, out evidence, out error);

        public bool TryApply(GlobalMotionRebaseRequest request, out string error) =>
            ResolveAdapter().TryApply(request, out error);

        public bool TryRebuild(GlobalMotionRebaseRequest request, out string error) =>
            ResolveAdapter().TryRebuild(request, out error);

        public bool TryValidate(GlobalMotionRebaseRequest request, out string error) =>
            ResolveAdapter().TryValidate(request, out error);

        public bool TryRestore(
            GlobalMotionRebaseRequest request,
            UnityStateRollbackEvidence evidence,
            out string error) =>
            ResolveAdapter().TryRestore(request, evidence, out error);

        private GlobalMotionShipDeckNavProtocolHost ResolveHost()
        {
            if (_host == null)
                _host = GetComponent<GlobalMotionShipDeckNavProtocolHost>();
            return _host;
        }

        private GlobalMotionShipDeckNavNativeAdapter ResolveAdapter()
        {
            var host = ResolveHost();
            if (host == null)
                throw new MissingComponentException("GlobalMotionShipDeckNavProtocolHost is required.");
            return new GlobalMotionShipDeckNavNativeAdapter(ResolveAdapterId(), host);
        }

        private string ResolveAdapterId()
        {
            return string.IsNullOrWhiteSpace(_adapterId) ? "ship-deck-nav" : _adapterId.Trim();
        }
    }
}