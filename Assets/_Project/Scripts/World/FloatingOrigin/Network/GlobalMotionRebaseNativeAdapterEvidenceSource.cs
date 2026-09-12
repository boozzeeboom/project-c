using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06BG: explicit native-adapter evidence source for owner-reviewed targets.
    /// It constructs no incomplete adapter set: missing ShipDeckNav or NetworkBaseline coverage is rejected.
    /// The component is dormant until all five domains have concrete reviewed targets/adapters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionRebaseNativeAdapterEvidenceSource : MonoBehaviour,
        IGlobalMotionRebaseNativeAdapterEvidenceSource
    {
        [Header("Reviewed Transform/Rigidbody/Camera targets")]
        [SerializeField] private Transform _transformTarget;
        [SerializeField] private Rigidbody _rigidbodyTarget;
        [SerializeField] private SpringArmCamera _cameraTarget;
        [SerializeField] private string _cameraOwnerId = "PLAYER_FRAME";

        [Header("Reviewed remaining native adapter sources")]
        [SerializeField] private MonoBehaviour _shipDeckNavAdapterSource;
        [SerializeField] private MonoBehaviour _networkBaselineAdapterSource;

        public bool TryGetNativeAdapterSet(
            out GlobalMotionNativeAdapterSet adapters,
            out string error)
        {
            adapters = null;
            error = null;

            if (_transformTarget == null)
                return Reject("transform_target_missing", out error);
            if (_rigidbodyTarget == null)
                return Reject("rigidbody_target_missing", out error);
            if (_cameraTarget == null)
                return Reject("camera_target_missing", out error);
            if (string.IsNullOrWhiteSpace(_cameraOwnerId) || _cameraOwnerId.Trim() != _cameraOwnerId)
                return Reject("camera_owner_id_invalid", out error);
            if (!(_shipDeckNavAdapterSource is IGlobalMotionNativeAdapter shipDeckNavAdapter))
                return Reject("ship_deck_nav_native_adapter_source_missing", out error);
            if (!(_networkBaselineAdapterSource is IGlobalMotionNativeAdapter networkBaselineAdapter))
                return Reject("network_baseline_native_adapter_source_missing", out error);

            var candidate = new GlobalMotionNativeAdapterSet();
            if (!candidate.TryAdd(
                    new GlobalMotionTransformNativeAdapter("transform:" + _transformTarget.name, _transformTarget),
                    out error))
                return false;
            if (!candidate.TryAdd(
                    new GlobalMotionRigidbodyNativeAdapter("rigidbody:" + _rigidbodyTarget.name, _rigidbodyTarget),
                    out error))
                return false;
            if (!candidate.TryAdd(
                    new GlobalMotionCameraHistoryNativeAdapter("camera-history:" + _cameraTarget.name, _cameraOwnerId, _cameraTarget),
                    out error))
                return false;
            if (!candidate.TryAdd(shipDeckNavAdapter, out error))
                return false;
            if (!candidate.TryAdd(networkBaselineAdapter, out error))
                return false;
            if (!candidate.Seal(out error))
                return false;
            if (!candidate.TryValidateReady(out error))
                return false;

            adapters = candidate;
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
