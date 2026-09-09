using System;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin
{
    /// <summary>
    /// An origin-independent point, in metres. Never implicitly converts to Vector3.
    /// T-FO02: a coordinate contract, not an active world-shifting component.
    /// Public fields support Unity/JSON serialization; validate deserialized values.
    /// </summary>
    [Serializable]
    public struct GlobalPosition : IEquatable<GlobalPosition>, INetworkSerializable
    {
        public double X;
        public double Y;
        public double Z;

        public GlobalPosition(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
            if (!IsFinite)
                throw new ArgumentOutOfRangeException(nameof(x), "Global coordinates must be finite.");
        }

        public static GlobalPosition Zero => default;
        public bool IsFinite => IsFiniteValue(X) && IsFiniteValue(Y) && IsFiniteValue(Z);

        /// <summary>
        /// Explicit import of an existing ABSOLUTE float position (old save/authored data).
        /// This does not restore precision already lost in the source float.
        /// Do not pass a rebased Transform.position here: use its frame.ToGlobal instead.
        /// </summary>
        public static GlobalPosition FromLegacyAbsolute(Vector3 position)
        {
            return new GlobalPosition(position.x, position.y, position.z);
        }

        public GlobalPosition Translated(Vector3 displacement)
        {
            EnsureFinite();
            return new GlobalPosition(X + (double)displacement.x,
                Y + (double)displacement.y, Z + (double)displacement.z);
        }

        /// <summary>Global distance calculation; may return infinity for extreme finite inputs.</summary>
        public double SquaredDistanceTo(GlobalPosition other)
        {
            EnsureFinite();
            other.EnsureFinite();
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public void EnsureFinite()
        {
            if (!IsFinite)
                throw new InvalidOperationException("Invalid global position: NaN or infinity.");
        }

        /// <summary>
        /// Three doubles, in XYZ order. Protocol/version/authority validation belongs to
        /// the enclosing message. No scene, Transform or origin is mutated by decoding.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter)
                EnsureFinite();

            // Decode into temporaries: a truncated/invalid message must not partly update this point.
            double x = X;
            double y = Y;
            double z = Z;
            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref z);
            if (serializer.IsReader)
                this = new GlobalPosition(x, y, z);
        }

        public bool Equals(GlobalPosition other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is GlobalPosition other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = hash * 397 ^ Y.GetHashCode();
                return hash * 397 ^ Z.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Global({0:R}, {1:R}, {2:R})", X, Y, Z);
        }

        public static bool operator ==(GlobalPosition left, GlobalPosition right) => left.Equals(right);
        public static bool operator !=(GlobalPosition left, GlobalPosition right) => !left.Equals(right);
        internal static bool IsFiniteValue(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
