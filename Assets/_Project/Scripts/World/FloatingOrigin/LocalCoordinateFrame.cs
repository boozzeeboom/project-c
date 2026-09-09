using System;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin
{
    /// <summary>
    /// Immutable translation-only frame. One value per client or server simulation region;
    /// intentionally no global static origin. Rotation, velocity and scale are unchanged.
    /// A default-constructed frame is invalid, rather than silently meaning world origin.
    /// </summary>
    public readonly struct LocalCoordinateFrame
    {
        public const float DefaultMaxLocalCoordinate = 8192f;

        public GlobalPosition Origin { get; }
        public float MaxLocalCoordinate { get; }
        public bool IsValid => Origin.IsFinite && GlobalPosition.IsFiniteValue(MaxLocalCoordinate) && MaxLocalCoordinate > 0f;

        public LocalCoordinateFrame(GlobalPosition origin, float maxLocalCoordinate = DefaultMaxLocalCoordinate)
        {
            origin.EnsureFinite();
            if (!GlobalPosition.IsFiniteValue(maxLocalCoordinate) || maxLocalCoordinate <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxLocalCoordinate));
            Origin = origin;
            MaxLocalCoordinate = maxLocalCoordinate;
        }

        public bool ContainsLocal(Vector3 localPosition)
        {
            return IsValid && WithinLimit(localPosition.x) && WithinLimit(localPosition.y) && WithinLimit(localPosition.z);
        }

        /// <summary>
        /// A false result means invalid data or a point outside this simulation cube.
        /// Do not place an out-of-range entity at the zero out-value; stream/route it instead.
        /// The bound is a policy guard, not a guarantee of identical precision for every input.
        /// </summary>
        public bool TryToLocal(GlobalPosition position, out Vector3 localPosition)
        {
            localPosition = default;
            if (!IsValid || !position.IsFinite)
                return false;

            // IMPORTANT: subtract while BOTH operands are double, before the float cast.
            double x = position.X - Origin.X;
            double y = position.Y - Origin.Y;
            double z = position.Z - Origin.Z;
            if (!WithinLimit(x) || !WithinLimit(y) || !WithinLimit(z))
                return false;
            localPosition = new Vector3((float)x, (float)y, (float)z);
            return true;
        }

        public Vector3 ToLocal(GlobalPosition position)
        {
            EnsureValid();
            if (!TryToLocal(position, out Vector3 local))
                throw new ArgumentOutOfRangeException(nameof(position), "Point is invalid or outside this local frame.");
            return local;
        }

        public GlobalPosition ToGlobal(Vector3 localPosition)
        {
            EnsureValid();
            if (!ContainsLocal(localPosition))
                throw new ArgumentOutOfRangeException(nameof(localPosition), "Point is invalid or outside this local frame.");
            return Origin.Translated(localPosition);
        }

        public LocalCoordinateFrame WithOrigin(GlobalPosition origin)
        {
            EnsureValid();
            return new LocalCoordinateFrame(origin, MaxLocalCoordinate);
        }

        /// <summary>Converts a point between explicit frames without a global float intermediate.</summary>
        public bool TryConvertTo(Vector3 localPosition, LocalCoordinateFrame destination, out Vector3 result)
        {
            result = default;
            return ContainsLocal(localPosition) && destination.TryToLocal(ToGlobal(localPosition), out result);
        }

        private bool WithinLimit(double value)
        {
            return GlobalPosition.IsFiniteValue(value) && Math.Abs(value) <= MaxLocalCoordinate;
        }

        private void EnsureValid()
        {
            if (!IsValid)
                throw new InvalidOperationException("Construct a local frame explicitly before using it.");
        }
    }
}
