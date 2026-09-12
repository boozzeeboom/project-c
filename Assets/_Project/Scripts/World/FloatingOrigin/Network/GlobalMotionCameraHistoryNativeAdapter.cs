using System;
using System.Collections.Generic;
using ProjectC.Core;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06AX: explicit CameraHistory-domain native adapter for one owner-reviewed camera.
    /// It captures and translates camera lag/collision history together with the camera pose.
    /// The adapter performs no discovery and is not installed in BootstrapScene automatically.
    /// </summary>
    public sealed class GlobalMotionCameraHistoryNativeAdapter : IGlobalMotionNativeAdapter
    {
        private readonly string _adapterId;
        private readonly string _ownerId;
        private readonly SpringArmCamera _target;
        private readonly Dictionary<Guid, SpringArmCamera.GlobalMotionCameraHistorySnapshot> _snapshots =
            new Dictionary<Guid, SpringArmCamera.GlobalMotionCameraHistorySnapshot>();

        public GlobalMotionCameraHistoryNativeAdapter(string adapterId, string ownerId, SpringArmCamera target)
        {
            if (string.IsNullOrWhiteSpace(adapterId))
                throw new ArgumentException("A stable adapter id is required.", nameof(adapterId));
            if (string.IsNullOrWhiteSpace(ownerId))
                throw new ArgumentException("A stable camera owner id is required.", nameof(ownerId));

            _adapterId = adapterId.Trim();
            _ownerId = ownerId.Trim();
            _target = target;
        }

        public GlobalMotionNativeAdapterDescriptor Descriptor => new GlobalMotionNativeAdapterDescriptor(
            _adapterId,
            UnityStateRollbackCoverage.CameraHistory,
            GlobalMotionNativeAdapterCapability.FullTransaction,
            _target != null,
            _target != null && _target.IsGlobalMotionCameraReady);

        public bool TryCapture(
            GlobalMotionRebaseRequest request,
            out UnityStateRollbackEvidence evidence,
            out string error)
        {
            evidence = default;
            if (!TryValidateRequest(request, out error)) return false;
            if (!_target.TryCaptureGlobalMotionCameraHistory(out var snapshot, out error)) return false;

            _snapshots[request.TransactionId] = snapshot;
            evidence = CreateEvidence(request, "camera-history:" + request.TransactionId.ToString("N"),
                UnityStateRollbackCoverage.CameraHistory,
                UnityStateRollbackCoverage.None,
                restoreAttempted: false,
                restoreSucceeded: false,
                participantIdentityRestored: false,
                reverseOrderVerified: false);
            return true;
        }

        public bool TryApply(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            if (!_snapshots.ContainsKey(request.TransactionId))
            {
                error = "camera_history_snapshot_missing:" + request.TransactionId;
                return false;
            }

            return _target.TryApplyGlobalMotionCameraTranslation(request.Plan.LocalTranslation, out error);
        }

        public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            return _target.TryValidateGlobalMotionCameraHistory(out error);
        }

        public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            return _target.TryValidateGlobalMotionCameraHistory(out error);
        }

        public bool TryRestore(
            GlobalMotionRebaseRequest request,
            UnityStateRollbackEvidence evidence,
            out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            if (string.IsNullOrWhiteSpace(evidence.TransactionId) ||
                !string.Equals(evidence.TransactionId, request.TransactionId.ToString("N"), StringComparison.Ordinal) ||
                !string.Equals(evidence.ParticipantId, _adapterId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(evidence.SnapshotId) ||
                (evidence.CapturedCoverage & UnityStateRollbackCoverage.CameraHistory) == 0)
            {
                error = "camera_history_rollback_identity_mismatch:" + _adapterId;
                return false;
            }
            if (!_snapshots.TryGetValue(request.TransactionId, out var snapshot))
            {
                error = "camera_history_snapshot_missing:" + request.TransactionId;
                return false;
            }

            if (!_target.TryRestoreGlobalMotionCameraHistory(snapshot, out error)) return false;
            _snapshots.Remove(request.TransactionId);
            return true;
        }

        private bool TryValidateRequest(GlobalMotionRebaseRequest request, out string error)
        {
            error = null;
            if (!request.IsValid)
            {
                error = "rebase_request_invalid";
                return false;
            }
            if (_target == null)
            {
                error = "camera_target_missing:" + _adapterId;
                return false;
            }
            if (!_target.IsGlobalMotionCameraReady)
            {
                error = "camera_target_not_ready:" + _adapterId;
                return false;
            }
            return true;
        }

        private UnityStateRollbackEvidence CreateEvidence(
            GlobalMotionRebaseRequest request,
            string snapshotId,
            UnityStateRollbackCoverage captured,
            UnityStateRollbackCoverage restored,
            bool restoreAttempted,
            bool restoreSucceeded,
            bool participantIdentityRestored,
            bool reverseOrderVerified)
        {
            return new UnityStateRollbackEvidence(
                request.TransactionId.ToString("N"),
                _adapterId,
                snapshotId + ":owner=" + _ownerId,
                request.FrameGeneration,
                request.FrameGeneration,
                UnityStateRollbackCoverage.CameraHistory,
                captured,
                restored,
                true,
                restoreAttempted,
                restoreSucceeded,
                participantIdentityRestored,
                reverseOrderVerified,
                0,
                1);
        }
    }
}
