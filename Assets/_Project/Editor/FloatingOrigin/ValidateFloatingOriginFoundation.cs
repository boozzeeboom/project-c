using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.Scene;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// T-FO02: deterministic in-memory checks. No Play Mode, scene/asset edits,
    /// NetworkManager, GameObject creation, save files, or physics simulation.
    /// Matches the project's existing MenuItem + Execute validation pattern.
    /// </summary>
    public static class ValidateFloatingOriginFoundation
    {
        [Serializable]
        public sealed class Report
        {
            public int passed;
            public int failed;
            public string[] checks;
            public string[] failures;
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Coordinate Foundation")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException(JsonUtility.ToJson(report, true));
            Debug.Log("[T-FO02] Coordinate foundation: " + report.passed + " checks passed. Not a runtime acceptance test.");
        }

        public static Report Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run foundation checks outside Play Mode.");

            var passed = new List<string>();
            var failed = new List<string>();
            Check("Zero is a valid global point", () => Require(GlobalPosition.Zero.IsFinite), passed, failed);
            Check("Reject non-finite construction", () =>
            {
                Expect<ArgumentOutOfRangeException>(() => new GlobalPosition(double.NaN, 0d, 0d));
                Expect<ArgumentOutOfRangeException>(() => new GlobalPosition(0d, double.PositiveInfinity, 0d));
                Expect<ArgumentOutOfRangeException>(() => new GlobalPosition(0d, 0d, double.NegativeInfinity));
            }, passed, failed);
            Check("Explicit legacy import retains only existing float precision", () =>
            {
                var legacy = new Vector3(39877.62f, 2502.17f, 40026.44f);
                var global = GlobalPosition.FromLegacyAbsolute(legacy);
                Require(global.X == (double)legacy.x && global.Y == (double)legacy.y && global.Z == (double)legacy.z);
                var frame = new LocalCoordinateFrame(new GlobalPosition(39936d, 2560d, 39936d));
                Require(frame.ToGlobal(frame.ToLocal(global)) == global);
            }, passed, failed);
            Check("Known default spawn uses unchanged 79999 scene grid", () =>
            {
                var center = GlobalGridCoordinates.GetSceneCenter(new SceneID(0, 0));
                Require(center == new GlobalPosition(39999.5d, 0d, 39999.5d));
                var spawn = center.Translated(new Vector3(0f, 3000f, 0f));
                var frame = new LocalCoordinateFrame(new GlobalPosition(39936d, 3072d, 39936d));
                Require(frame.ToLocal(spawn) == new Vector3(63.5f, -72f, 63.5f));
            }, passed, failed);
            Check("Subtract doubles BEFORE casting (1 billion metres)", () =>
            {
                var origin = new GlobalPosition(1000000000d, -1000000000d, 1000000000d);
                var p = new GlobalPosition(origin.X + 0.001d, origin.Y + 0.002d, origin.Z - 0.003d);
                Vector3 local = new LocalCoordinateFrame(origin).ToLocal(p);
                Near(local.x, 0.001d, 0.000001d);
                Near(local.y, 0.002d, 0.000001d);
                Near(local.z, -0.003d, 0.000001d);
                Require((float)p.X - (float)origin.X == 0f); // The incorrect implementation loses the displacement.
            }, passed, failed);
            Check("Independent frames project same global point", () =>
            {
                var p = new GlobalPosition(60000.125d, 3000.25d, -50000.5d);
                var a = new LocalCoordinateFrame(new GlobalPosition(60000d, 3000d, -50000d));
                var b = new LocalCoordinateFrame(new GlobalPosition(59000d, 2500d, -51000d));
                Require(a.ToGlobal(a.ToLocal(p)) == p && b.ToGlobal(b.ToLocal(p)) == p);
                Require(a.ToLocal(p) != b.ToLocal(p));
                Require(a.TryConvertTo(a.ToLocal(p), b, out Vector3 mapped) && mapped == b.ToLocal(p));
            }, passed, failed);
            Check("Far client frame is independent and rejects invisible distant point", () =>
            {
                var a = new LocalCoordinateFrame(new GlobalPosition(60000d, 3000d, 60000d));
                var b = new LocalCoordinateFrame(new GlobalPosition(-60000d, 3000d, -60000d));
                Require(a.ToLocal(a.Origin) == Vector3.zero && b.ToLocal(b.Origin) == Vector3.zero);
                Require(!a.TryToLocal(b.Origin, out _));
                Require(!a.TryConvertTo(Vector3.zero, b, out _));
            }, passed, failed);
            Check("Default frame fails closed", () =>
            {
                LocalCoordinateFrame invalid = default;
                Require(!invalid.IsValid && !invalid.TryToLocal(GlobalPosition.Zero, out _));
                Expect<InvalidOperationException>(() => invalid.ToGlobal(Vector3.zero));
            }, passed, failed);
            Check("Local bounds and finite validation", () =>
            {
                var frame = new LocalCoordinateFrame(GlobalPosition.Zero, 8192f);
                Require(frame.TryToLocal(new GlobalPosition(8192d, -8192d, 8192d), out _));
                Require(!frame.TryToLocal(new GlobalPosition(8192.001d, 0d, 0d), out _));
                Require(!frame.TryToLocal(new GlobalPosition { X = double.NaN }, out _));
                Expect<ArgumentOutOfRangeException>(() => frame.ToGlobal(new Vector3(float.NaN, 0f, 0f)));
                Expect<ArgumentOutOfRangeException>(() => new LocalCoordinateFrame(GlobalPosition.Zero, 0f));
                Expect<ArgumentOutOfRangeException>(() => new LocalCoordinateFrame(GlobalPosition.Zero, float.PositiveInfinity));
            }, passed, failed);
            Check("Global height survives vertical frame shift", () =>
            {
                var p = new GlobalPosition(0d, 8000.125d, 0d);
                var frame = new LocalCoordinateFrame(new GlobalPosition(0d, 7936d, 0d));
                Near(frame.ToLocal(p).y, 64.125d, 0d);
                Near(frame.ToGlobal(frame.ToLocal(p)).Y, 8000.125d, 0d);
            }, passed, failed);
            Check("Scene boundaries including negative indices", () =>
            {
                Require(GlobalGridCoordinates.TryGetScene(new GlobalPosition(79999d, 0d, -0.001d), out SceneID scene));
                Require(scene == new SceneID(1, -1));
                Require(!scene.IsValid); // Existing registry policy is NOT changed.
                Require(GlobalGridCoordinates.TryGetScene(new GlobalPosition(79998.999d, 0d, 0d), out scene));
                Require(scene == new SceneID(0, 0));
                Require(GlobalGridCoordinates.TryGetScene(new GlobalPosition(-79999d, 0d, 0d), out scene));
                Require(scene.GridX == -1);
            }, passed, failed);
            Check("Chunk boundaries retain 2000 metre grid", () =>
            {
                Require(GlobalGridCoordinates.TryGetChunk(new GlobalPosition(2000d, 50000d, -0.001d), out var chunk));
                Require(chunk.GridX == 1 && chunk.GridZ == -1);
            }, passed, failed);
            Check("Grid integer overflow and invalid input fail closed", () =>
            {
                Require(!GlobalGridCoordinates.TryGetIndex(((double)int.MaxValue + 1d) * 2000d, 2000d, out _));
                Require(!GlobalGridCoordinates.TryGetIndex(((double)int.MinValue - 1d) * 2000d, 2000d, out _));
                Require(!GlobalGridCoordinates.TryGetIndex(double.NaN, 2000d, out _));
                Require(!GlobalGridCoordinates.TryGetIndex(1d, 0d, out _));
                Require(GlobalGridCoordinates.TryGetIndex((double)int.MinValue * 2000d, 2000d, out int min) && min == int.MinValue);
                Require(GlobalGridCoordinates.TryGetIndex((double)int.MaxValue * 2000d, 2000d, out int max) && max == int.MaxValue);
                Require(GlobalGridCoordinates.GetSceneOrigin(new SceneID(1000000, 0)).X == 79999000000d);
            }, passed, failed);
            Check("Rebase preserves global point and does not mutate old frame", () =>
            {
                var frame = new LocalCoordinateFrame(new GlobalPosition(60000d, 3000d, -60000d));
                Vector3 focus = new Vector3(2100.125f, -2100.25f, 0.5f);
                GlobalPosition global = frame.ToGlobal(focus);
                Require(OriginRebasePlan.TryCreate(frame, focus, 2048f, 256f, out var plan));
                Require(plan.TryReproject(focus, out Vector3 shifted));
                Require(plan.After.ToGlobal(shifted) == global);
                Require(frame.Origin == new GlobalPosition(60000d, 3000d, -60000d));
                Require(shifted == focus + plan.LocalTranslation);
            }, passed, failed);
            Check("No shift inside threshold; invalid planning fails safely", () =>
            {
                var frame = new LocalCoordinateFrame(GlobalPosition.Zero);
                Require(!OriginRebasePlan.TryCreate(frame, new Vector3(2048f, 0f, 0f), 2048f, 256f, out _));
                Require(!OriginRebasePlan.TryCreate(frame, new Vector3(9000f, 0f, 0f), 2048f, 256f, out _));
                Expect<ArgumentOutOfRangeException>(() => OriginRebasePlan.TryCreate(frame, Vector3.zero, 0f, 256f, out _));
                Expect<ArgumentOutOfRangeException>(() => OriginRebasePlan.TryCreate(frame, Vector3.zero, 2048f, 4096f, out _));
            }, passed, failed);
            Check("10000 steps and repeated rebases keep global trajectory", () =>
            {
                var start = new GlobalPosition(1000000000d, 3000d, -1000000000d);
                var frame = new LocalCoordinateFrame(start);
                Vector3 local = Vector3.zero;
                int shifts = 0;
                for (int i = 1; i <= 10000; i++)
                {
                    local += new Vector3(128f, 0f, -64f);
                    if (OriginRebasePlan.TryCreate(frame, local, 2048f, 256f, out var plan))
                    {
                        Require(plan.TryReproject(local, out local));
                        frame = plan.After;
                        shifts++;
                    }
                    Require(frame.ToGlobal(local) == new GlobalPosition(start.X + i * 128d, start.Y, start.Z - i * 64d));
                }
                Require(shifts > 100);
            }, passed, failed);
            Check("2500 deterministic frame round trips", () =>
            {
                var random = new System.Random(1702);
                for (int i = 0; i < 2500; i++)
                {
                    var origin = new GlobalPosition((random.NextDouble() - 0.5d) * 200000000d,
                        (random.NextDouble() - 0.5d) * 200000000d, (random.NextDouble() - 0.5d) * 200000000d);
                    var frame = new LocalCoordinateFrame(origin);
                    var local = new Vector3((float)(random.NextDouble() * 6000d - 3000d),
                        (float)(random.NextDouble() * 6000d - 3000d), (float)(random.NextDouble() * 6000d - 3000d));
                    Vector3 copy = frame.ToLocal(frame.ToGlobal(local));
                    Near(copy.x, local.x, 0.00001d);
                    Near(copy.y, local.y, 0.00001d);
                    Near(copy.z, local.z, 0.00001d);
                }
            }, passed, failed);
            Check("Global equality and distance", () =>
            {
                var a = new GlobalPosition(1000000d, 2000d, -1000000d);
                var b = a.Translated(new Vector3(3f, 4f, 0f));
                Near(a.SquaredDistanceTo(b), 25d, 0d);
                Require(a.Equals(a) && a.GetHashCode() == new GlobalPosition(a.X, a.Y, a.Z).GetHashCode());
            }, passed, failed);
            Check("NGO exact double XYZ round trip; 24 byte value payload", () =>
            {
                var p = new GlobalPosition(1000000000.001d, -3000.125d, -999999999.999d);
                using var writer = new FastBufferWriter(64, Allocator.Temp);
                writer.WriteNetworkSerializable(p);
                Require(writer.Length == 24);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                reader.ReadNetworkSerializable(out GlobalPosition copy);
                Require(copy == p && reader.Position == 24);
            }, passed, failed);
            Check("NGO rejects non-finite incoming and outgoing points", () =>
            {
                using var writer = new FastBufferWriter(64, Allocator.Temp);
                writer.WriteValueSafe(double.NaN);
                writer.WriteValueSafe(0d);
                writer.WriteValueSafe(0d);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                Expect<ArgumentOutOfRangeException>(() => reader.ReadNetworkSerializable(out GlobalPosition _));
                var invalid = new GlobalPosition { Z = double.PositiveInfinity };
                using var output = new FastBufferWriter(64, Allocator.Temp);
                Expect<InvalidOperationException>(() => output.WriteNetworkSerializable(invalid));
                Require(output.Length == 0);
            }, passed, failed);
            Check("NGO rejects truncated payload", () =>
            {
                using var writer = new FastBufferWriter(64, Allocator.Temp);
                writer.WriteValueSafe(100d);
                writer.WriteValueSafe(200d);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                Expect<OverflowException>(() => reader.ReadNetworkSerializable(out GlobalPosition _));
            }, passed, failed);
            Check("JSON preserves new global double fields (not a save migration)", () =>
            {
                var p = new GlobalPosition(1000000000.001d, -3000.125d, -999999999.999d);
                string json = JsonUtility.ToJson(p);
                GlobalPosition copy = JsonUtility.FromJson<GlobalPosition>(json);
                Require(copy == p);
            }, passed, failed);

            return new Report { passed = passed.Count, failed = failed.Count, checks = passed.ToArray(), failures = failed.ToArray() };
        }

        private static void Check(string name, Action body, List<string> passed, List<string> failed)
        {
            try { body(); passed.Add(name); }
            catch (Exception e) { failed.Add(name + ": " + e.GetType().Name + ": " + e.Message); }
        }

        private static void Require(bool condition)
        {
            if (!condition) throw new InvalidOperationException("Assertion failed.");
        }

        private static void Near(double actual, double expected, double tolerance)
        {
            if (double.IsNaN(actual) || double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance)
                throw new InvalidOperationException($"Expected {expected:R}, got {actual:R}, tolerance {tolerance:R}.");
        }

        private static void Expect<T>(Action body) where T : Exception
        {
            try { body(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }
    }
}
