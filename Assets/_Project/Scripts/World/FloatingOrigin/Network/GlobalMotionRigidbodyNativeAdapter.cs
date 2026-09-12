using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06AW: explicit Rigidbody-domain native adapter for one owner-reviewed body.
    /// It performs no discovery and is not installed in BootstrapScene automatically.
    /// </summary>
    public sealed class GlobalMotionRigidbodyNativeAdapter : IGlobalMotionNativeAdapter
    {
        private readonly string _adapterId;
        private readonly Rigidbody _target;
        private readonly Dictionary<Guid, Snapshot> _snapshots = new Dictionary<Guid, Snapshot>();

        private readonly struct Snapshot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 LinearVelocity;
            public readonly Vector3 AngularVelocity;

            public Snapshot(Rigidbody target)
            {
                Position = target.position;
                Rotation = target.rotation;
                LinearVelocity = target.linearVelocity;
                AngularVelocity = target.angularVelocity;
            }
        }

        public GlobalMotionRigidbodyNativeAdapter(string adapterId, Rigidbody target)
        {
            if (string.IsNullOrWhiteSpace(adapterId))
                throw new ArgumentException("A stable adapter id is required.", nameof(adapterId));

            _adapterId = adapterId.Trim();
            _target = target;
        }

        public GlobalMotionNativeAdapterDescriptor Descriptor => new GlobalMotionNativeAdapterDescriptor(
            _adapterId,
            UnityStateRollbackCoverage.Rigidbody,
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

            _snapshots[request.TransactionId] = new Snapshot(_target);
            evidence = new UnityStateRollbackEvidence(
                request.TransactionId.ToString("N"),
                _adapterId,
                "rigidbody:" + request.TransactionId.ToString("N"),
                request.FrameGeneration,
                request.FrameGeneration,
                UnityStateRollbackCoverage.Rigidbody,
                UnityStateRollbackCoverage.Rigidbody,
                UnityStateRollbackCoverage.None,
                true,
                false,
                false,
                false,
                false,
                0,
                1);
            return true;
        }

        public bool TryApply(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            if (!_snapshots.ContainsKey(request.TransactionId))
            {
                error = "rigidbody_snapshot_missing:" + request.TransactionId;
                return false;
            }

            _target.position += request.Plan.LocalTranslation;
            return IsFiniteState(out error);
        }

        public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            Physics.SyncTransforms();
            return IsFiniteState(out error);
        }

        public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error)) return false;
            return IsFiniteState(out error);
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
                (evidence.CapturedCoverage & UnityStateRollbackCoverage.Rigidbody) == 0)
            {
                error = "rigidbody_rollback_identity_mismatch:" + _adapterId;
                return false;
            }
            if (!_snapshots.TryGetValue(request.TransactionId, out var snapshot))
            {
                error = "rigidbody_snapshot_missing:" + request.TransactionId;
                return false;
            }

            _target.position = snapshot.Position;
            _target.rotation = snapshot.Rotation;
            _target.linearVelocity = snapshot.LinearVelocity;
            _target.angularVelocity = snapshot.AngularVelocity;
            _snapshots.Remove(request.TransactionId);
            return IsFiniteState(out error);
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
                error = "rigidbody_target_missing:" + _adapterId;
                return false;
            }
            if (!_target.gameObject.activeInHierarchy)
            {
                error = "rigidbody_target_inactive:" + _adapterId;
                return false;
            }
            return true;
        }

        private bool IsFiniteState(out string error)
        {
            error = null;
            if (!IsFinite(_target.position) || !IsFinite(_target.rotation) ||
                !IsFinite(_target.linearVelocity) || !IsFinite(_target.angularVelocity))
            {
                error = "rigidbody_state_not_finite:" + _adapterId;
                return false;
            }
            return true;
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
