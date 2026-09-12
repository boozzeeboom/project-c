using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06AV: explicit Transform-domain native adapter for one owner-reviewed root.
    /// It performs no discovery and is not installed in BootstrapScene automatically.
    /// </summary>
    public sealed class GlobalMotionTransformNativeAdapter : IGlobalMotionNativeAdapter
    {
        private readonly string _adapterId;
        private readonly Transform _target;
        private readonly Dictionary<Guid, Snapshot> _snapshots = new Dictionary<Guid, Snapshot>();

        private readonly struct Snapshot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 LocalScale;

            public Snapshot(Vector3 position, Quaternion rotation, Vector3 localScale)
            {
                Position = position;
                Rotation = rotation;
                LocalScale = localScale;
            }
        }

        public GlobalMotionTransformNativeAdapter(string adapterId, Transform target)
        {
            if (string.IsNullOrWhiteSpace(adapterId))
                throw new ArgumentException("A stable adapter id is required.", nameof(adapterId));

            _adapterId = adapterId.Trim();
            _target = target;
        }

        public GlobalMotionNativeAdapterDescriptor Descriptor => new GlobalMotionNativeAdapterDescriptor(
            _adapterId,
            UnityStateRollbackCoverage.Transform,
            GlobalMotionNativeAdapterCapability.FullTransaction,
            _target != null,
            _target != null && _target.gameObject.activeInHierarchy);

        public bool TryCapture(
            GlobalMotionRebaseRequest request,
            out UnityStateRollbackEvidence evidence,
            out string error)
        {
            evidence = default;
            if (!TryValidateRequest(request, out error)) return false;

            var snapshot = new Snapshot(_target.position, _target.rotation, _target.localScale);
            _snapshots[request.TransactionId] = snapshot;
            evidence = CreateEvidence(request, "transform:" + request.TransactionId.ToString("N"),
                captured: UnityStateRollbackCoverage.Transform,
                restored: UnityStateRollbackCoverage.None,
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
                error = "transform_snapshot_missing:" + request.TransactionId;
                return false;
            }

            _target.position += request.Plan.LocalTranslation;
            return IsFiniteTransform(out error);
        }

        public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            error = null;
            return true;
        }

        public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            return IsFiniteTransform(out error);
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
                (evidence.CapturedCoverage & UnityStateRollbackCoverage.Transform) == 0)
            {
                error = "transform_rollback_identity_mismatch:" + _adapterId;
                return false;
            }
            if (!_snapshots.TryGetValue(request.TransactionId, out var snapshot))
            {
                error = "transform_snapshot_missing:" + request.TransactionId;
                return false;
            }

            _target.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
            _target.localScale = snapshot.LocalScale;
            _snapshots.Remove(request.TransactionId);
            return IsFiniteTransform(out error);
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
                error = "transform_target_missing:" + _adapterId;
                return false;
            }
            if (!_target.gameObject.activeInHierarchy)
            {
                error = "transform_target_inactive:" + _adapterId;
                return false;
            }
            return true;
        }

        private bool IsFiniteTransform(out string error)
        {
            error = null;
            if (!IsFinite(_target.position) || !IsFinite(_target.rotation) || !IsFinite(_target.localScale))
            {
                error = "transform_state_not_finite:" + _adapterId;
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
                snapshotId,
                request.FrameGeneration,
                request.FrameGeneration,
                UnityStateRollbackCoverage.Transform,
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

        private static bool IsFinite(Vector3 value) =>
            GlobalPosition.IsFiniteValue(value.x) &&
            GlobalPosition.IsFiniteValue(value.y) &&
            GlobalPosition.IsFiniteValue(value.z);

        private static bool IsFinite(Quaternion value) =>
            GlobalPosition.IsFiniteValue(value.x) &&
            GlobalPosition.IsFiniteValue(value.y) &&
            GlobalPosition.IsFiniteValue(value.z) &&
            GlobalPosition.IsFiniteValue(value.w);
    }
}
