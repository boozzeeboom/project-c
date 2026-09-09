using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>T-FO04A: real NGO codec and pure receive-buffer checks, no Play Mode or scene mutation.</summary>
    public static class ValidateGlobalMotionProtocol
    {
        [Serializable]
        public sealed class Report
        {
            public int passed;
            public int failed;
            public string[] checks;
            public string[] failures;
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Motion Protocol")]
        public static void Execute()
        {
            Report result = Run();
            if (result.failed > 0) throw new InvalidOperationException(JsonUtility.ToJson(result, true));
            Debug.Log("[T-FO04A] Motion protocol: " + result.passed + " checks passed. Transport and gameplay untested.");
        }

        public static Report Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Requires stable Edit Mode.");
            var passed = new List<string>();
            var failed = new List<string>();
            Check("World codec exact doubles and 123-byte payload", () =>
            {
                var value = Make(WorldBinding(), 12, 1.25d, 1000000000.001d);
                var copy = RoundTrip(value, 123);
                Equal(value, copy);
            }, passed, failed);
            Check("Parent codec and 111-byte payload", () =>
            {
                var value = Make(ParentBinding(), 18, 1.5d, 0.125d);
                Equal(value, RoundTrip(value, 111));
            }, passed, failed);
            Check("In-place decoding clears the unused coordinate branch", () =>
            {
                var world = Make(WorldBinding(), 1, 1d, 1000000000d);
                var parent = Make(ParentBinding(), 2, 2d, 2d);
                using var w1 = new FastBufferWriter(256, Allocator.Temp);
                w1.WriteNetworkSerializable(parent);
                using var r1 = new FastBufferReader(w1, Allocator.Temp);
                var reused = world;
                r1.ReadNetworkSerializableInPlace(ref reused);
                Equal(parent, reused);
                using var w2 = new FastBufferWriter(256, Allocator.Temp);
                w2.WriteNetworkSerializable(world);
                using var r2 = new FastBufferReader(w2, Allocator.Temp);
                r2.ReadNetworkSerializableInPlace(ref reused);
                Equal(world, reused);
            }, passed, failed);
            Check("Every truncated world payload leaves in-place target unchanged", () =>
            {
                var incoming = Make(WorldBinding(), 2, 2d, 1000000000d);
                using var writer = new FastBufferWriter(256, Allocator.Temp);
                writer.WriteNetworkSerializable(incoming);
                for (int length = 0; length < writer.Length; length++)
                {
                    using var reader = new FastBufferReader(writer, Allocator.Temp, length);
                    var previous = Make(ParentBinding(), 71, 3d, 0.125d);
                    var target = previous;
                    Expect<OverflowException>(() => reader.ReadNetworkSerializableInPlace(ref target));
                    Equal(previous, target);
                }
            }, passed, failed);
            Check("Unknown protocol rejected before mutation", () =>
            {
                var invalid = Make(WorldBinding(), 1, 1d, 1d);
                invalid.Version = 99;
                Require(!invalid.TryValidate(out var reason) && reason == MotionRejectReason.UnsupportedVersion);
                RejectWire(invalid);
                using var writer = new FastBufferWriter(256, Allocator.Temp);
                Expect<InvalidOperationException>(() => writer.WriteNetworkSerializable(invalid));
                Require(writer.Length == 0);
            }, passed, failed);
            Check("Binding validity and legal zero NetworkObjectId", () =>
            {
                var b = WorldBinding(); b.NetworkObjectId = 0; Require(b.IsValid);
                b = ParentBinding(); b.ParentNetworkObjectId = 0; Require(b.IsValid);
                b = WorldBinding(); b.SessionId = 0; Require(!b.IsValid);
                b = WorldBinding(); b.SpawnGeneration = 0; Require(!b.IsValid);
                b = WorldBinding(); b.AuthorityGeneration = 0; Require(!b.IsValid);
                b = WorldBinding(); b.DiscontinuityGeneration = 0; Require(!b.IsValid);
                b = WorldBinding(); b.ParentSpawnGeneration = 1; Require(!b.IsValid);
                b = ParentBinding(); b.ParentSpawnGeneration = 0; Require(!b.IsValid);
                b = ParentBinding(); b.ParentNetworkObjectId = b.NetworkObjectId; Require(!b.IsValid);
            }, passed, failed);
            Check("Unknown coordinate space rejected on the actual wire", () =>
            {
                var value = Make(WorldBinding(), 1, 1d, 1d);
                value.Binding.Space = (MotionCoordinateSpace)99;
                RejectWire(value);
            }, passed, failed);
            Check("Non-finite time, position, scale and quaternion rejected", () =>
            {
                var value = Make(WorldBinding(), 1, 1d, 1d);
                value.SampleTime = double.NaN; RejectWire(value);
                value.SampleTime = -1d; RejectWire(value);
                value = Make(WorldBinding(), 1, 1d, 1d); value.WorldPosition.X = double.PositiveInfinity; RejectWire(value);
                value = Make(ParentBinding(), 1, 1d, 1d); value.ParentLocalPosition.y = float.NaN; RejectWire(value);
                value = Make(WorldBinding(), 1, 1d, 1d); value.Scale.z = float.PositiveInfinity; RejectWire(value);
                value = Make(WorldBinding(), 1, 1d, 1d); value.Rotation = default; RejectWire(value);
                value = Make(WorldBinding(), 1, 1d, 1d); value.Rotation.x = float.NaN; RejectWire(value);
            }, passed, failed);
            Check("Noncanonical unused branch rejected, even a tiny value", () =>
            {
                var value = Make(WorldBinding(), 1, 1d, 1d);
                value.ParentLocalPosition = new Vector3(0.0000001f, 0f, 0f);
                Require(!value.TryValidate(out _));
                value = Make(ParentBinding(), 1, 1d, 1d);
                value.WorldPosition = new GlobalPosition(0.0000001d, 0d, 0d);
                Require(!value.TryValidate(out _));
            }, passed, failed);
            Check("Motion cannot initialize an unbound buffer", () =>
            {
                var buffer = new GlobalMotionBuffer();
                Require(!buffer.TryAdd(Make(WorldBinding(), 1, 1d, 1d), out var r) && r == MotionRejectReason.NotBound);
                Require(!buffer.TrySample(1d, out _) && !buffer.IsBound && buffer.Count == 0);
            }, passed, failed);
            Check("Invalid baseline cannot bind or replace valid state", () =>
            {
                var buffer = NewBuffer(); var original = buffer.Binding;
                var newer = original; newer.DiscontinuityGeneration++;
                var invalid = Make(newer, 1, 2d, 99d); invalid.Rotation = default;
                Require(!buffer.BeginStream(newer, invalid, out _));
                Require(buffer.Binding == original && buffer.Count == 1);
                var wrong = Make(original, 2, 2d, 2d);
                Require(!buffer.BeginStream(newer, wrong, out var r) && r == MotionRejectReason.WrongBinding);
            }, passed, failed);
            Check("Baseline clamps both ends without extrapolation", () =>
            {
                var buffer = NewBuffer();
                Require(buffer.TrySample(-100d, out var first)); Near(first.WorldPosition.X, 0d);
                Require(buffer.TrySample(100d, out var last)); Near(last.WorldPosition.X, 0d);
            }, passed, failed);
            Check("Duplicate, older and reordered sequence do not change state", () =>
            {
                var buffer = NewBuffer();
                Require(buffer.TryAdd(Make(WorldBinding(), 3, 1.2d, 3d), out _));
                Require(!buffer.TryAdd(Make(WorldBinding(), 3, 1.3d, 300d), out var r) && r == MotionRejectReason.OldSequence);
                Require(!buffer.TryAdd(Make(WorldBinding(), 2, 1.1d, 200d), out _));
                Require(buffer.Count == 2); Require(buffer.TrySample(2d, out var p)); Near(p.WorldPosition.X, 3d);
            }, passed, failed);
            Check("uint sequence wraps; half-range ambiguity rejected", () =>
            {
                var b = WorldBinding(); var buffer = new GlobalMotionBuffer();
                Require(buffer.BeginStream(b, Make(b, uint.MaxValue - 1, 1d, 1d), out _));
                Require(buffer.TryAdd(Make(b, uint.MaxValue, 1.1d, 2d), out _));
                Require(buffer.TryAdd(Make(b, 0, 1.2d, 3d), out _));
                Require(buffer.TryAdd(Make(b, 1, 1.3d, 4d), out _));
                Require(!buffer.TryAdd(Make(b, uint.MaxValue, 1.4d, 99d), out _));
                Require(!buffer.TryAdd(Make(b, 0x80000001u, 1.5d, 99d), out _));
            }, passed, failed);
            Check("Rejected time regression does not consume sequence", () =>
            {
                var buffer = NewBuffer();
                Require(!buffer.TryAdd(Make(WorldBinding(), 2, 0.5d, 99d), out var r) && r == MotionRejectReason.TimeRegression);
                Require(buffer.TryAdd(Make(WorldBinding(), 2, 1.1d, 2d), out _));
                Require(buffer.Count == 2);
            }, passed, failed);
            Check("Equal timestamp replaces newest value without zero interval", () =>
            {
                var buffer = NewBuffer();
                Require(buffer.TryAdd(Make(WorldBinding(), 2, 1d, 2d), out _));
                Require(buffer.Count == 1); Require(buffer.TrySample(1d, out var p)); Near(p.WorldPosition.X, 2d);
            }, passed, failed);
            Check("All foreign identity and generation fields rejected", () =>
            {
                var buffer = NewBuffer(); var variants = new List<MotionStreamBinding>();
                var b = WorldBinding(); b.SessionId++; variants.Add(b);
                b = WorldBinding(); b.NetworkObjectId++; variants.Add(b);
                b = WorldBinding(); b.SpawnGeneration++; variants.Add(b);
                b = WorldBinding(); b.AuthorityGeneration++; variants.Add(b);
                b = WorldBinding(); b.DiscontinuityGeneration++; variants.Add(b);
                variants.Add(ParentBinding());
                foreach (var wrong in variants)
                    Require(!buffer.TryAdd(Make(wrong, 2, 1.1d, 99d), out var r) && r == MotionRejectReason.WrongBinding);
                Require(buffer.Count == 1 && buffer.Binding == WorldBinding());
            }, passed, failed);
            Check("Approved teleport baseline clears history; old packets stay rejected", () =>
            {
                var buffer = NewBuffer(); var old = WorldBinding();
                Require(buffer.TryAdd(Make(old, 2, 1.1d, 2d), out _));
                var next = old; next.DiscontinuityGeneration++;
                Require(buffer.BeginStream(next, Make(next, 1, 1.2d, 60000d), out _));
                Require(buffer.Count == 1); Require(buffer.TrySample(1.15d, out var p)); Near(p.WorldPosition.X, 60000d);
                Require(!buffer.TryAdd(Make(old, 999, 2d, 3d), out _));
                Require(!buffer.BeginStream(old, Make(old, 1000, 2.1d, 4d), out var r) && r == MotionRejectReason.StaleBinding);
            }, passed, failed);
            Check("Approved authority handoff allows new sequence and time epoch", () =>
            {
                var buffer = NewBuffer(); var old = WorldBinding(); var next = old; next.AuthorityGeneration++;
                Require(buffer.BeginStream(next, Make(next, 0, 0d, 9d), out _));
                Require(!buffer.TryAdd(Make(old, 999, 10d, 99d), out _));
                Require(!buffer.BeginStream(old, Make(old, 999, 10d, 99d), out _));
                Require(buffer.Count == 1);
            }, passed, failed);
            Check("Object ID reuse requires newer spawn lifetime", () =>
            {
                var buffer = NewBuffer(); var old = WorldBinding(); var next = old; next.SpawnGeneration++;
                Require(buffer.BeginStream(next, Make(next, 0, 0d, 8d), out _));
                Require(!buffer.BeginStream(old, Make(old, 999, 2d, 99d), out _));
                Require(!buffer.TryAdd(Make(old, 1000, 2.1d, 99d), out _));
            }, passed, failed);
            Check("Repeated old initial sync cannot clear newer samples", () =>
            {
                var buffer = NewBuffer(); var b = WorldBinding();
                Require(buffer.TryAdd(Make(b, 2, 1.1d, 2d), out _));
                Require(!buffer.BeginStream(b, Make(b, 1, 1d, 0d), out _));
                Require(buffer.Count == 2); Require(buffer.TrySample(2d, out var p)); Near(p.WorldPosition.X, 2d);
            }, passed, failed);
            Check("New session or object requires explicit EndStream", () =>
            {
                var buffer = NewBuffer(); var next = WorldBinding(); next.SessionId++;
                Require(!buffer.BeginStream(next, Make(next, 1, 1d, 5d), out _));
                buffer.EndStream();
                Require(!buffer.IsBound && buffer.Count == 0 && !buffer.TrySample(1d, out _));
                Require(buffer.BeginStream(next, Make(next, 1, 1d, 5d), out _));
                Require(!buffer.TryAdd(Make(WorldBinding(), 999, 2d, 99d), out _));
                next.NetworkObjectId++;
                Require(!buffer.BeginStream(next, Make(next, 1, 1d, 5d), out _));
            }, passed, failed);
            Check("Parent-space switch needs a new discontinuity binding", () =>
            {
                var buffer = NewBuffer(); var next = ParentBinding();
                Require(!buffer.BeginStream(next, Make(next, 1, 1d, 1d), out _));
                next.DiscontinuityGeneration++;
                Require(buffer.BeginStream(next, Make(next, 1, 1d, 1d), out _));
                Require(buffer.Count == 1); Require(buffer.TrySample(1d, out var p));
                Require(!p.TryProjectWorld(new LocalCoordinateFrame(GlobalPosition.Zero), out _));
            }, passed, failed);
            Check("Bounded ring discards only oldest samples", () =>
            {
                var buffer = NewBuffer(3);
                for (uint i = 2; i <= 10; i++) Require(buffer.TryAdd(Make(WorldBinding(), i, 1d + i * 0.01d, i), out _));
                Require(buffer.Count == 3 && buffer.Capacity == 3);
                Require(buffer.TrySample(-1d, out var first)); Near(first.WorldPosition.X, 8d);
                Require(buffer.TrySample(99d, out var last)); Near(last.WorldPosition.X, 10d);
            }, passed, failed);
            Check("Millimetre interpolation at global 1e9 metres", () =>
            {
                var b = WorldBinding(); var buffer = new GlobalMotionBuffer();
                Require(buffer.BeginStream(b, Make(b, 1, 1d, 1000000000.001d), out _));
                Require(buffer.TryAdd(Make(b, 2, 1.2d, 1000000000.003d), out _));
                Require(buffer.TrySample(1.1d, out var p));
                var frame = new LocalCoordinateFrame(new GlobalPosition(1000000000d, 0d, 0d));
                Require(p.TryProjectWorld(frame, out var local)); Near(local.x, 0.002d, 0.000001d);
            }, passed, failed);
            Check("Rebase changes projection, not snapshots or global position", () =>
            {
                var b = WorldBinding(); var buffer = new GlobalMotionBuffer();
                Require(buffer.BeginStream(b, Make(b, 1, 1d, 60000d), out _));
                Require(buffer.TryAdd(Make(b, 2, 1.2d, 60002d), out _));
                Require(buffer.TrySample(1.1d, out var p));
                var a = new LocalCoordinateFrame(new GlobalPosition(59904d, 0d, 0d));
                var after = new LocalCoordinateFrame(new GlobalPosition(60160d, 0d, 0d));
                Require(p.TryProjectWorld(a, out var pa)); Require(p.TryProjectWorld(after, out var pb));
                Require(a.ToGlobal(pa) == after.ToGlobal(pb));
                Require(buffer.Count == 2 && buffer.Binding == b);
                Require(buffer.TryAdd(Make(b, 3, 1.3d, 60003d), out _));
            }, passed, failed);
            Check("Two distant client frames remain independent", () =>
            {
                var ba = WorldBinding(); var bb = ba; bb.NetworkObjectId++;
                var a = new GlobalMotionBuffer(); var b = new GlobalMotionBuffer();
                Require(a.BeginStream(ba, Make(ba, 1, 1d, 60000d), out _));
                Require(b.BeginStream(bb, Make(bb, 1, 1d, -60000d), out _));
                Require(a.TrySample(1d, out var pa)); Require(b.TrySample(1d, out var pb));
                Require(pa.TryProjectWorld(new LocalCoordinateFrame(new GlobalPosition(60000d, 0d, 0d)), out var la));
                Require(pb.TryProjectWorld(new LocalCoordinateFrame(new GlobalPosition(-60000d, 0d, 0d)), out var lb));
                Require(la == Vector3.zero && lb == Vector3.zero);
            }, passed, failed);
            Check("Parent-local sampling requires exact session, ID and lifetime", () =>
            {
                var binding = ParentBinding(); var buffer = new GlobalMotionBuffer();
                Require(buffer.BeginStream(binding, Make(binding, 1, 1d, 1d), out _));
                Require(buffer.TryAdd(Make(binding, 2, 1.2d, 3d), out _));
                Require(buffer.TrySample(1.1d, out var p));
                Require(p.TryGetParentLocalPosition(binding.SessionId, binding.ParentNetworkObjectId, binding.ParentSpawnGeneration, out var local));
                Near(local.x, 2d);
                Require(!p.TryGetParentLocalPosition(binding.SessionId + 1, binding.ParentNetworkObjectId, binding.ParentSpawnGeneration, out _));
                Require(!p.TryGetParentLocalPosition(binding.SessionId, binding.ParentNetworkObjectId + 1, binding.ParentSpawnGeneration, out _));
                Require(!p.TryGetParentLocalPosition(binding.SessionId, binding.ParentNetworkObjectId, binding.ParentSpawnGeneration + 1, out _));
                Require(!p.TryProjectWorld(new LocalCoordinateFrame(GlobalPosition.Zero), out _));
            }, passed, failed);
            Check("Rotation and local scale interpolate in their declared space", () =>
            {
                var b = WorldBinding(); var a = Make(b, 1, 1d, 0d); var z = Make(b, 2, 1.2d, 0d);
                z.Rotation = Quaternion.Euler(0f, 90f, 0f); z.Scale = new Vector3(3f, -1f, 0f);
                var buffer = new GlobalMotionBuffer(); Require(buffer.BeginStream(b, a, out _)); Require(buffer.TryAdd(z, out _));
                Require(buffer.TrySample(1.1d, out var p));
                Near(Quaternion.Angle(p.Rotation, Quaternion.Euler(0f, 45f, 0f)), 0d, 0.05d);
                Near(p.Scale.x, 2d); Near(p.Scale.y, 0d); Near(p.Scale.z, 0.5d);
            }, passed, failed);
            Check("Long packet gap holds instead of blending across missing motion", () =>
            {
                var buffer = NewBuffer(); Require(buffer.TryAdd(Make(WorldBinding(), 2, 5d, 100d), out _));
                Require(buffer.TrySample(4d, out var held)); Near(held.WorldPosition.X, 0d);
                Require(buffer.TrySample(5d, out var next)); Near(next.WorldPosition.X, 100d);
            }, passed, failed);
            Check("Invalid sample time, default pose and out-of-range projection fail closed", () =>
            {
                var buffer = NewBuffer(); Require(!buffer.TrySample(double.NaN, out _)); Require(!buffer.TrySample(double.PositiveInfinity, out _));
                GlobalMotionPose empty = default;
                Require(!empty.TryProjectWorld(new LocalCoordinateFrame(GlobalPosition.Zero), out _));
                Require(buffer.TrySample(1d, out var p));
                Require(!p.TryProjectWorld(new LocalCoordinateFrame(new GlobalPosition(1000000d, 0d, 0d)), out _));
            }, passed, failed);
            Check("Storage configuration is bounded", () =>
            {
                Expect<ArgumentOutOfRangeException>(() => new GlobalMotionBuffer(1));
                Expect<ArgumentOutOfRangeException>(() => new GlobalMotionBuffer(257));
                Expect<ArgumentOutOfRangeException>(() => new GlobalMotionBuffer(32, double.NaN));
                Expect<ArgumentOutOfRangeException>(() => new GlobalMotionBuffer(32, 0d));
            }, passed, failed);
            Check("2000 receive/sample cycles remain bounded and reject delayed duplicates", () =>
            {
                var buffer = NewBuffer(8);
                for (uint i = 2; i < 2002; i++)
                {
                    var value = Make(WorldBinding(), i, 1d + i * 0.01d, 60000d + i * 0.125d);
                    Require(buffer.TryAdd(value, out _));
                    Require(!buffer.TryAdd(value, out _));
                    Require(buffer.Count <= 8); Require(buffer.TrySample(value.SampleTime, out var p));
                    Near(p.WorldPosition.X, value.WorldPosition.X);
                }
            }, passed, failed);
            return new Report { passed = passed.Count, failed = failed.Count, checks = passed.ToArray(), failures = failed.ToArray() };
        }

        private static MotionStreamBinding WorldBinding() => new MotionStreamBinding
        {
            SessionId = 11, NetworkObjectId = 42, SpawnGeneration = 1,
            AuthorityGeneration = 1, DiscontinuityGeneration = 1, Space = MotionCoordinateSpace.World
        };

        private static MotionStreamBinding ParentBinding()
        {
            var b = WorldBinding(); b.Space = MotionCoordinateSpace.ParentLocal;
            b.ParentNetworkObjectId = 5; b.ParentSpawnGeneration = 6; return b;
        }

        private static GlobalMotionSnapshot Make(MotionStreamBinding b, uint sequence, double time, double x)
        {
            return b.Space == MotionCoordinateSpace.World
                ? GlobalMotionSnapshot.CreateWorld(b, sequence, time, new GlobalPosition(x, 0d, 0d), Quaternion.identity, Vector3.one)
                : GlobalMotionSnapshot.CreateParentLocal(b, sequence, time, new Vector3((float)x, 0f, 0f), Quaternion.identity, Vector3.one);
        }

        private static GlobalMotionBuffer NewBuffer(int capacity = 32)
        {
            var b = WorldBinding(); var buffer = new GlobalMotionBuffer(capacity);
            Require(buffer.BeginStream(b, Make(b, 1, 1d, 0d), out _)); return buffer;
        }

        private static GlobalMotionSnapshot RoundTrip(GlobalMotionSnapshot value, int bytes)
        {
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteNetworkSerializable(value); Require(writer.Length == bytes);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out GlobalMotionSnapshot copy); Require(reader.Position == bytes); return copy;
        }

        // Intentionally bypass outbound validation to exercise malformed incoming payloads.
        private static void RejectWire(GlobalMotionSnapshot value)
        {
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteValueSafe(value.Version);
            writer.WriteValueSafe(value.Binding.SessionId); writer.WriteValueSafe(value.Binding.NetworkObjectId);
            writer.WriteValueSafe(value.Binding.SpawnGeneration); writer.WriteValueSafe(value.Binding.AuthorityGeneration);
            writer.WriteValueSafe(value.Binding.DiscontinuityGeneration); writer.WriteValueSafe((byte)value.Binding.Space);
            writer.WriteValueSafe(value.Binding.ParentNetworkObjectId); writer.WriteValueSafe(value.Binding.ParentSpawnGeneration);
            writer.WriteValueSafe(value.Sequence); writer.WriteValueSafe(value.SampleTime);
            if (value.Binding.Space == MotionCoordinateSpace.World)
            {
                writer.WriteValueSafe(value.WorldPosition.X); writer.WriteValueSafe(value.WorldPosition.Y); writer.WriteValueSafe(value.WorldPosition.Z);
            }
            else writer.WriteValueSafe(value.ParentLocalPosition);
            writer.WriteValueSafe(value.Rotation); writer.WriteValueSafe(value.Scale);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            var original = Make(WorldBinding(), 900, 20d, 7d); var target = original;
            bool threw = false;
            try { reader.ReadNetworkSerializableInPlace(ref target); }
            catch (InvalidOperationException) { threw = true; }
            catch (ArgumentOutOfRangeException) { threw = true; }
            Require(threw); Equal(original, target);
        }

        private static void Equal(GlobalMotionSnapshot a, GlobalMotionSnapshot b)
        {
            Require(a.Version == b.Version && a.Binding == b.Binding && a.Sequence == b.Sequence && a.SampleTime == b.SampleTime &&
                a.WorldPosition == b.WorldPosition && a.ParentLocalPosition.Equals(b.ParentLocalPosition) &&
                a.Rotation.Equals(b.Rotation) && a.Scale.Equals(b.Scale));
        }

        private static void Check(string name, Action body, List<string> passed, List<string> failed)
        {
            try { body(); passed.Add(name); }
            catch (Exception e) { failed.Add(name + ": " + e.GetType().Name + ": " + e.Message); }
        }
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
        private static void Near(double actual, double expected, double tolerance = 0.000001d)
        {
            if (double.IsNaN(actual) || double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance)
                throw new InvalidOperationException($"Expected {expected:R}, got {actual:R}, tolerance {tolerance:R}.");
        }
        private static void Expect<T>(Action body) where T : Exception
        {
            try { body(); } catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }
    }
}
