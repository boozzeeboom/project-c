using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Origin-independent wire value. Rotation uses Binding.Space; Scale is local scale.
    /// No origin, live Transform or physics state is accessed during serialization.
    /// This codec does not authenticate senders or replace the NGO connection handshake.
    /// </summary>
    [Serializable]
    public struct GlobalMotionSnapshot : INetworkSerializable
    {
        public const ushort CurrentVersion = 1;
        public ushort Version;
        public MotionStreamBinding Binding;
        public uint Sequence;
        public double SampleTime;
        public GlobalPosition WorldPosition;
        public Vector3 ParentLocalPosition;
        public Quaternion Rotation;
        public Vector3 Scale;

        public static GlobalMotionSnapshot CreateWorld(MotionStreamBinding binding, uint sequence, double sampleTime,
            GlobalPosition position, Quaternion rotation, Vector3 scale)
        {
            if (binding.Space != MotionCoordinateSpace.World) throw new ArgumentException("Expected world binding.", nameof(binding));
            var value = new GlobalMotionSnapshot
            {
                Version = CurrentVersion, Binding = binding, Sequence = sequence, SampleTime = sampleTime,
                WorldPosition = position, Rotation = rotation, Scale = scale
            };
            value.EnsureValid();
            return value;
        }

        public static GlobalMotionSnapshot CreateParentLocal(MotionStreamBinding binding, uint sequence, double sampleTime,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            if (binding.Space != MotionCoordinateSpace.ParentLocal) throw new ArgumentException("Expected parent-local binding.", nameof(binding));
            var value = new GlobalMotionSnapshot
            {
                Version = CurrentVersion, Binding = binding, Sequence = sequence, SampleTime = sampleTime,
                ParentLocalPosition = localPosition, Rotation = localRotation, Scale = localScale
            };
            value.EnsureValid();
            return value;
        }

        public bool TryValidate(out MotionRejectReason reason)
        {
            reason = MotionRejectReason.UnsupportedVersion;
            if (Version != CurrentVersion) return false;
            reason = MotionRejectReason.InvalidBinding;
            if (!Binding.IsValid) return false;
            reason = MotionRejectReason.InvalidSnapshot;
            if (!GlobalPosition.IsFiniteValue(SampleTime) || SampleTime < 0d || !Finite(Scale) || !UnitRotation(Rotation)) return false;
            if (Binding.Space == MotionCoordinateSpace.World)
            {
                if (!WorldPosition.IsFinite || !ParentLocalPosition.Equals(Vector3.zero)) return false;
            }
            else if (!Finite(ParentLocalPosition) || WorldPosition != GlobalPosition.Zero) return false;
            reason = MotionRejectReason.None;
            return true;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter) EnsureValid();
            var value = this;
            serializer.SerializeValue(ref value.Version);
            if (value.Version != CurrentVersion) throw new InvalidOperationException("Unsupported motion protocol version.");
            value.Binding.NetworkSerialize(serializer);
            serializer.SerializeValue(ref value.Sequence);
            serializer.SerializeValue(ref value.SampleTime);
            if (value.Binding.Space == MotionCoordinateSpace.World)
            {
                value.WorldPosition.NetworkSerialize(serializer);
                value.ParentLocalPosition = Vector3.zero;
            }
            else
            {
                serializer.SerializeValue(ref value.ParentLocalPosition);
                value.WorldPosition = GlobalPosition.Zero;
            }
            serializer.SerializeValue(ref value.Rotation);
            serializer.SerializeValue(ref value.Scale);
            if (serializer.IsReader)
            {
                value.EnsureValid();
                this = value;
            }
        }

        private void EnsureValid()
        {
            if (!TryValidate(out MotionRejectReason reason))
                throw new InvalidOperationException("Invalid motion snapshot: " + reason);
        }

        internal static bool Finite(Vector3 value) => GlobalPosition.IsFiniteValue(value.x) &&
            GlobalPosition.IsFiniteValue(value.y) && GlobalPosition.IsFiniteValue(value.z);

        internal static bool UnitRotation(Quaternion value)
        {
            double norm = (double)value.x * value.x + (double)value.y * value.y +
                (double)value.z * value.z + (double)value.w * value.w;
            return GlobalPosition.IsFiniteValue(norm) && Math.Abs(norm - 1d) <= 0.002d;
        }
    }
}
