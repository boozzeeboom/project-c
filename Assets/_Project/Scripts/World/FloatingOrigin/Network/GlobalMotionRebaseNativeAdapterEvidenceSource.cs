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

        public bool TryConfigureSources(
            Transform transformTarget,
            Rigidbody rigidbodyTarget,
            SpringArmCamera cameraTarget,
            string cameraOwnerId,
            MonoBehaviour shipDeckNavAdapterSource,
            MonoBehaviour networkBaselineAdapterSource,
            out string error)
        {
            if (!ValidateTargets(
                    transformTarget,
                    rigidbodyTarget,
                    cameraTarget,
                    cameraOwnerId,
                    shipDeckNavAdapterSource,
                    networkBaselineAdapterSource,
                    out error))
                return false;

            _transformTarget = transformTarget;
            _rigidbodyTarget = rigidbodyTarget;
            _cameraTarget = cameraTarget;
            _cameraOwnerId = cameraOwnerId;
            _shipDeckNavAdapterSource = shipDeckNavAdapterSource;
            _networkBaselineAdapterSource = networkBaselineAdapterSource;
            return true;
        }

        public bool TryValidateSourceBindings(out string error)
        {
            return ValidateTargets(
                _transformTarget,
                _rigidbodyTarget,
                _cameraTarget,
                _cameraOwnerId,
                _shipDeckNavAdapterSource,
                _networkBaselineAdapterSource,
                out error);
        }

        public bool TryGetNativeAdapterSet(
            out GlobalMotionNativeAdapterSet adapters,
            out string error)
        {
            adapters = null;
            error = null;

            if (!TryValidateSourceBindings(out error))
                return false;
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

        private static bool ValidateTargets(
            Transform transformTarget,
            Rigidbody rigidbodyTarget,
            SpringArmCamera cameraTarget,
            string cameraOwnerId,
            MonoBehaviour shipDeckNavAdapterSource,
            MonoBehaviour networkBaselineAdapterSource,
            out string error)
        {
            if (transformTarget == null)
                return Reject("transform_target_missing", out error);
            if (rigidbodyTarget == null)
                return Reject("rigidbody_target_missing", out error);
            if (cameraTarget == null)
                return Reject("camera_target_missing", out error);
            if (string.IsNullOrWhiteSpace(cameraOwnerId) || cameraOwnerId.Trim() != cameraOwnerId)
                return Reject("camera_owner_id_invalid", out error);
            if (!(shipDeckNavAdapterSource is IGlobalMotionNativeAdapter))
                return Reject("ship_deck_nav_native_adapter_source_missing", out error);
            if (!(networkBaselineAdapterSource is IGlobalMotionNativeAdapter))
                return Reject("network_baseline_native_adapter_source_missing", out error);

            error = null;
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
