using System;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Bounded single-stream receive state machine. No GameObjects, callbacks, automatic
    /// origin changes or network authority decisions. Use only on one thread.
    /// BeginStream must come from an authenticated lifecycle/initial-sync path, not an
    /// arbitrary motion packet. A newer packet epoch alone NEVER changes the binding.
    /// </summary>
    public sealed class GlobalMotionBuffer
    {
        private readonly GlobalMotionSnapshot[] _samples;
        private readonly double _maxInterpolationGap;
        private int _head;
        private int _count;
        private bool _bound;
        private MotionStreamBinding _binding;

        public int Count => _count;
        public int Capacity => _samples.Length;
        public bool IsBound => _bound;
        public MotionStreamBinding Binding => _binding;

        public GlobalMotionBuffer(int capacity = 32, double maxInterpolationGap = 0.5d)
        {
            if (capacity < 2 || capacity > 256) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!GlobalPosition.IsFiniteValue(maxInterpolationGap) || maxInterpolationGap <= 0d)
                throw new ArgumentOutOfRangeException(nameof(maxInterpolationGap));
            _samples = new GlobalMotionSnapshot[capacity];
            _maxInterpolationGap = maxInterpolationGap;
        }

        /// <summary>
        /// Atomically starts/changes a TRUSTED stream with a valid baseline. An identical
        /// binding uses normal sequence checks and does not clear history. Within one
        /// lifetime, authority or discontinuity generation must advance for a rebind.
        /// EndStream is mandatory before changing session/object identity.
        /// </summary>
        public bool BeginStream(MotionStreamBinding approvedBinding, GlobalMotionSnapshot baseline, out MotionRejectReason reason)
        {
            reason = MotionRejectReason.InvalidBinding;
            if (!approvedBinding.IsValid) return false;
            if (!baseline.TryValidate(out reason)) return false;
            reason = MotionRejectReason.WrongBinding;
            if (baseline.Binding != approvedBinding) return false;
            if (_bound)
            {
                if (approvedBinding == _binding) return TryAdd(baseline, out reason);
                if (approvedBinding.SessionId != _binding.SessionId || approvedBinding.NetworkObjectId != _binding.NetworkObjectId)
                    return false;
                reason = MotionRejectReason.StaleBinding;
                if (approvedBinding.SpawnGeneration < _binding.SpawnGeneration) return false;
                if (approvedBinding.SpawnGeneration == _binding.SpawnGeneration)
                {
                    if (approvedBinding.AuthorityGeneration < _binding.AuthorityGeneration) return false;
                    if (approvedBinding.AuthorityGeneration == _binding.AuthorityGeneration &&
                        approvedBinding.DiscontinuityGeneration <= _binding.DiscontinuityGeneration) return false;
                }
            }
            _binding = approvedBinding;
            _bound = true;
            _head = 0;
            _count = 1;
            _samples[0] = baseline;
            reason = MotionRejectReason.None;
            return true;
        }

        public void EndStream()
        {
            _bound = false;
            _binding = default;
            _head = 0;
            _count = 0;
            Array.Clear(_samples, 0, _samples.Length);
        }

        public bool TryAdd(GlobalMotionSnapshot snapshot, out MotionRejectReason reason)
        {
            reason = MotionRejectReason.NotBound;
            if (!_bound) return false;
            if (!snapshot.TryValidate(out reason)) return false;
            reason = MotionRejectReason.WrongBinding;
            if (snapshot.Binding != _binding) return false;
            GlobalMotionSnapshot latest = At(_count - 1);
            reason = MotionRejectReason.OldSequence;
            if (!IsNewerSequence(snapshot.Sequence, latest.Sequence)) return false;
            reason = MotionRejectReason.TimeRegression;
            if (snapshot.SampleTime < latest.SampleTime) return false;
            if (snapshot.SampleTime == latest.SampleTime)
                _samples[(_head + _count - 1) % Capacity] = snapshot;
            else if (_count < Capacity)
                _samples[(_head + _count++) % Capacity] = snapshot;
            else
            {
                _samples[_head] = snapshot;
                _head = (_head + 1) % Capacity;
            }
            reason = MotionRejectReason.None;
            return true;
        }

        /// <summary>
        /// Pure sampling in the server timeline. Caller chooses render delay. No
        /// extrapolation: clamp ends; hold the older pose across an excessive time gap.
        /// </summary>
        public bool TrySample(double renderTime, out GlobalMotionPose pose)
        {
            pose = default;
            if (!_bound || _count == 0 || !GlobalPosition.IsFiniteValue(renderTime)) return false;
            GlobalMotionSnapshot first = At(0);
            GlobalMotionSnapshot last = At(_count - 1);
            if (renderTime <= first.SampleTime) { pose = new GlobalMotionPose(first); return true; }
            if (renderTime >= last.SampleTime) { pose = new GlobalMotionPose(last); return true; }
            for (int i = 1; i < _count; i++)
            {
                GlobalMotionSnapshot b = At(i);
                if (renderTime > b.SampleTime) continue;
                GlobalMotionSnapshot a = At(i - 1);
                if (renderTime == b.SampleTime) { pose = new GlobalMotionPose(b); return true; }
                double interval = b.SampleTime - a.SampleTime;
                if (interval > _maxInterpolationGap) { pose = new GlobalMotionPose(a); return true; }
                double t = (renderTime - a.SampleTime) / interval;
                var sample = a;
                if (_binding.Space == MotionCoordinateSpace.World)
                {
                    double x = Lerp(a.WorldPosition.X, b.WorldPosition.X, t);
                    double y = Lerp(a.WorldPosition.Y, b.WorldPosition.Y, t);
                    double z = Lerp(a.WorldPosition.Z, b.WorldPosition.Z, t);
                    if (!GlobalPosition.IsFiniteValue(x) || !GlobalPosition.IsFiniteValue(y) || !GlobalPosition.IsFiniteValue(z)) return false;
                    sample.WorldPosition = new GlobalPosition(x, y, z);
                }
                else sample.ParentLocalPosition = Lerp(a.ParentLocalPosition, b.ParentLocalPosition, t);
                sample.Rotation = Quaternion.SlerpUnclamped(a.Rotation.normalized, b.Rotation.normalized, (float)t);
                sample.Scale = Lerp(a.Scale, b.Scale, t);
                if (!sample.TryValidate(out _)) return false;
                pose = new GlobalMotionPose(sample);
                return true;
            }
            return false;
        }

        /// <summary>Half-range ordering. Exactly half the uint range is ambiguous and rejected.</summary>
        public static bool IsNewerSequence(uint incoming, uint current)
        {
            uint delta = unchecked(incoming - current);
            return delta != 0 && delta < 0x80000000u;
        }

        private GlobalMotionSnapshot At(int index) => _samples[(_head + index) % Capacity];

        private static double Lerp(double a, double b, double t)
        {
            double delta = b - a;
            return GlobalPosition.IsFiniteValue(delta) ? a + delta * t : a * (1d - t) + b * t;
        }

        private static Vector3 Lerp(Vector3 a, Vector3 b, double t)
        {
            return new Vector3((float)Lerp(a.x, b.x, t), (float)Lerp(a.y, b.y, t), (float)Lerp(a.z, b.z, t));
        }
    }
}
