using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Sampled display pose, not a packet to retransmit. Never resolves a missing parent
    /// by interpreting parent-local coordinates as global coordinates.
    /// </summary>
    public readonly struct GlobalMotionPose
    {
        public MotionStreamBinding Binding { get; }
        public GlobalPosition WorldPosition { get; }
        public Vector3 ParentLocalPosition { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }

        internal GlobalMotionPose(GlobalMotionSnapshot snapshot)
        {
            Binding = snapshot.Binding;
            WorldPosition = snapshot.WorldPosition;
            ParentLocalPosition = snapshot.ParentLocalPosition;
            Rotation = snapshot.Rotation.normalized;
            Scale = snapshot.Scale;
        }

        public bool TryProjectWorld(LocalCoordinateFrame frame, out Vector3 position)
        {
            position = default;
            return Binding.IsValid && Binding.Space == MotionCoordinateSpace.World &&
                frame.TryToLocal(WorldPosition, out position);
        }

        public bool TryGetParentLocalPosition(ulong sessionId, ulong parentObjectId, ulong parentSpawnGeneration,
            out Vector3 position)
        {
            position = default;
            if (!Binding.IsValid || Binding.Space != MotionCoordinateSpace.ParentLocal || Binding.SessionId != sessionId ||
                Binding.ParentNetworkObjectId != parentObjectId || Binding.ParentSpawnGeneration != parentSpawnGeneration)
                return false;
            position = ParentLocalPosition;
            return true;
        }
    }
}
