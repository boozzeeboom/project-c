using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectC.World.FloatingOrigin.Network
{
    [Flags]
    public enum GlobalMotionNativeAdapterCapability : byte
    {
        None = 0,
        Capture = 1 << 0,
        Apply = 1 << 1,
        Rebuild = 1 << 2,
        Validate = 1 << 3,
        Restore = 1 << 4,
        FullTransaction = Capture | Apply | Rebuild | Validate | Restore
    }

    /// <summary>
    /// Runtime-independent descriptor for one native Unity state adapter.
    /// The descriptor proves declared coverage only; it does not capture or mutate Unity state.
    /// </summary>
    public readonly struct GlobalMotionNativeAdapterDescriptor
    {
        public string AdapterId { get; }
        public UnityStateRollbackCoverage Coverage { get; }
        public GlobalMotionNativeAdapterCapability Capabilities { get; }
        public bool IsCurrent { get; }
        public bool NativeReady { get; }

        public GlobalMotionNativeAdapterDescriptor(
            string adapterId,
            UnityStateRollbackCoverage coverage,
            GlobalMotionNativeAdapterCapability capabilities,
            bool isCurrent,
            bool nativeReady)
        {
            AdapterId = adapterId;
            Coverage = coverage;
            Capabilities = capabilities;
            IsCurrent = isCurrent;
            NativeReady = nativeReady;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(AdapterId) &&
            Coverage != UnityStateRollbackCoverage.None &&
            Capabilities != GlobalMotionNativeAdapterCapability.None;
    }

    /// <summary>
    /// Concrete-adapter seam for Transform/Rigidbody/ShipDeckNav/camera/network baseline.
    /// Implementations are deliberately deferred until participant admission and live manifest gates pass.
    /// </summary>
    public interface IGlobalMotionNativeAdapter
    {
        GlobalMotionNativeAdapterDescriptor Descriptor { get; }
        bool TryCapture(GlobalMotionRebaseRequest request, out UnityStateRollbackEvidence evidence, out string error);
        bool TryApply(GlobalMotionRebaseRequest request, out string error);
        bool TryRebuild(GlobalMotionRebaseRequest request, out string error);
        bool TryValidate(GlobalMotionRebaseRequest request, out string error);
        bool TryRestore(GlobalMotionRebaseRequest request, UnityStateRollbackEvidence evidence, out string error);
    }

    /// <summary>
    /// Closed-world native adapter set. It validates declared coverage and capabilities only.
    /// It never discovers adapters and never invokes Unity APIs.
    /// </summary>
    public sealed class GlobalMotionNativeAdapterSet
    {
        private readonly List<IGlobalMotionNativeAdapter> _items = new List<IGlobalMotionNativeAdapter>();
        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);
        private bool _sealed;

        public IReadOnlyList<IGlobalMotionNativeAdapter> Items => new ReadOnlyCollection<IGlobalMotionNativeAdapter>(_items);
        public bool IsSealed => _sealed;
        public int Count => _items.Count;

        public bool TryAdd(IGlobalMotionNativeAdapter adapter, out string error)
        {
            error = null;
            if (_sealed) { error = "native_adapter_set_already_sealed"; return false; }
            if (adapter == null) { error = "native_adapter_missing"; return false; }
            var descriptor = adapter.Descriptor;
            if (!descriptor.IsValid) { error = "native_adapter_descriptor_invalid"; return false; }
            if (!_ids.Add(descriptor.AdapterId)) { error = "duplicate_native_adapter_id:" + descriptor.AdapterId; return false; }
            _items.Add(adapter);
            return true;
        }

        public bool Seal(out string error)
        {
            error = null;
            if (_sealed) { error = "native_adapter_set_already_sealed"; return false; }
            if (_items.Count == 0) { error = "native_adapter_set_empty"; return false; }
            _sealed = true;
            return true;
        }

        public bool TryValidateReady(out string error)
        {
            error = null;
            if (!_sealed) { error = "native_adapter_set_must_be_sealed"; return false; }

            UnityStateRollbackCoverage coverage = UnityStateRollbackCoverage.None;
            for (int i = 0; i < _items.Count; i++)
            {
                var adapter = _items[i];
                if (adapter == null) { error = "native_adapter_missing:index=" + i; return false; }
                var descriptor = adapter.Descriptor;
                if (!descriptor.IsValid) { error = "native_adapter_descriptor_invalid:index=" + i; return false; }
                if (!descriptor.IsCurrent) { error = "native_adapter_stale:" + descriptor.AdapterId; return false; }
                if (!descriptor.NativeReady) { error = "native_adapter_not_ready:" + descriptor.AdapterId; return false; }
                if ((descriptor.Capabilities & GlobalMotionNativeAdapterCapability.FullTransaction) != GlobalMotionNativeAdapterCapability.FullTransaction)
                {
                    error = "native_adapter_capabilities_incomplete:" + descriptor.AdapterId;
                    return false;
                }
                if ((coverage & descriptor.Coverage) != UnityStateRollbackCoverage.None)
                {
                    error = "native_adapter_coverage_overlap:" + descriptor.AdapterId;
                    return false;
                }
                coverage |= descriptor.Coverage;
            }

            if (coverage != UnityStateRollbackCoverage.All)
            {
                error = "native_adapter_coverage_incomplete:" + ToCoverageToken(UnityStateRollbackCoverage.All & ~coverage);
                return false;
            }
            return true;
        }

        private static string ToCoverageToken(UnityStateRollbackCoverage coverage)
        {
            if ((coverage & UnityStateRollbackCoverage.Transform) != 0) return "transform";
            if ((coverage & UnityStateRollbackCoverage.Rigidbody) != 0) return "rigidbody";
            if ((coverage & UnityStateRollbackCoverage.ShipDeckNav) != 0) return "ship_deck_nav";
            if ((coverage & UnityStateRollbackCoverage.CameraHistory) != 0) return "camera_history";
            if ((coverage & UnityStateRollbackCoverage.NetworkBaseline) != 0) return "network_baseline";
            return "unknown";
        }
    }
}
