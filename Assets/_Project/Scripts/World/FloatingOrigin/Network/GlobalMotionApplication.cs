using System;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum MotionPoseRole { Unavailable, Authority, ServerReplica, ClientReplica }

    /// <summary>Role is resolved without CanPublish, which is intentionally false before baseline acknowledgement.</summary>
    public static class GlobalMotionApplication
    {
        public static bool TryResolveRole(GlobalMotionControl control, bool isServer, ulong localClientId,
            ulong currentOwnerId, out MotionPoseRole role)
        {
            role = MotionPoseRole.Unavailable;
            if (!control.HasStream || !control.IsActive || !control.IsValid) return false;
            if (control.Authority == GlobalMotionAuthority.Server)
            {
                if (control.PublisherClientId != Unity.Netcode.NetworkManager.ServerClientId) return false;
                role = isServer ? MotionPoseRole.Authority : MotionPoseRole.ClientReplica;
                return true;
            }
            if (control.PublisherClientId != currentOwnerId) return false;
            role = control.PublisherClientId == localClientId ? MotionPoseRole.Authority :
                isServer ? MotionPoseRole.ServerReplica : MotionPoseRole.ClientReplica;
            return true;
        }

        public static uint ResumeSequence(bool hasPublished, uint localSequence, uint serverSequence)
        {
            return hasPublished && GlobalMotionBuffer.IsNewerSequence(localSequence, serverSequence) ? localSequence : serverSequence;
        }

        public static bool CanWrite(MotionPoseRole role, bool baseline, bool hasBody, bool isKinematic,
            bool navWritesPose, bool alreadyMatches)
        {
            if (!KnownRole(role) || (!baseline && role == MotionPoseRole.Authority)) return false;
            if (hasBody && !isKinematic && (role != MotionPoseRole.Authority || !baseline)) return false;
            return !navWritesPose || (baseline && role == MotionPoseRole.Authority && alreadyMatches);
        }

        /// <summary>A server physics replica must not consume an interpolated parent's render pose.</summary>
        public static bool CanUseParentTransform(MotionPoseRole role, bool interpolatedParentBody)
        {
            return KnownRole(role) && !(role == MotionPoseRole.ServerReplica && interpolatedParentBody);
        }
        private static bool KnownRole(MotionPoseRole role) => role == MotionPoseRole.Authority ||
            role == MotionPoseRole.ServerReplica || role == MotionPoseRole.ClientReplica;

        public static bool TryWorldPlan(GlobalMotionPose pose, LocalCoordinateFrame frame, out MotionUnityPose plan)
        {
            plan = default;
            if (!ValidPose(pose) || !pose.TryProjectWorld(frame, out Vector3 position)) return false;
            plan = new MotionUnityPose(pose, position, pose.Rotation.normalized);
            return true;
        }

        public static bool TryParentPlan(GlobalMotionPose pose, LocalCoordinateFrame frame, MotionStreamBinding parentBinding,
            Matrix4x4 parentLocalToUnity, Quaternion parentWorldRotation, out MotionUnityPose plan)
        {
            plan = default;
            if (!ValidPose(pose) || !parentBinding.IsValid || !GlobalMotionSnapshot.UnitRotation(parentWorldRotation) ||
                !pose.TryGetParentLocalPosition(parentBinding.SessionId, parentBinding.NetworkObjectId,
                    parentBinding.SpawnGeneration, out Vector3 local) || !frame.ContainsLocal(local)) return false;
            for (int i = 0; i < 16; i++) if (!GlobalPosition.IsFiniteValue(parentLocalToUnity[i])) return false;
            if (parentLocalToUnity.m30 != 0f || parentLocalToUnity.m31 != 0f || parentLocalToUnity.m32 != 0f || parentLocalToUnity.m33 != 1f) return false;
            double determinant = parentLocalToUnity.determinant;
            if (!GlobalPosition.IsFiniteValue(determinant) || Math.Abs(determinant) < 1e-12d) return false;
            if (!frame.ContainsLocal(new Vector3(parentLocalToUnity.m03, parentLocalToUnity.m13, parentLocalToUnity.m23))) return false;
            Vector3 position = parentLocalToUnity.MultiplyPoint3x4(local);
            if (!frame.ContainsLocal(position)) return false;
            plan = new MotionUnityPose(pose, position, (parentWorldRotation * pose.Rotation).normalized);
            return true;
        }

        private static bool ValidPose(GlobalMotionPose pose) => pose.Binding.IsValid && pose.WorldPosition.IsFinite &&
            GlobalMotionSnapshot.Finite(pose.ParentLocalPosition) && GlobalMotionSnapshot.UnitRotation(pose.Rotation) &&
            GlobalMotionSnapshot.Finite(pose.Scale);
    }

    /// <summary>Fully projected pose in one Unity/physics frame; not an on-wire or global position.</summary>
    public readonly struct MotionUnityPose
    {
        public MotionStreamBinding Binding { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 ParentLocalPosition { get; }
        public Quaternion ParentLocalRotation { get; }
        public Vector3 Scale { get; }
        public bool IsValid => Binding.IsValid;
        internal MotionUnityPose(GlobalMotionPose pose, Vector3 position, Quaternion rotation)
        {
            Binding = pose.Binding; Position = position; Rotation = rotation;
            ParentLocalPosition = pose.ParentLocalPosition; ParentLocalRotation = pose.Rotation.normalized; Scale = pose.Scale;
        }
    }
}
