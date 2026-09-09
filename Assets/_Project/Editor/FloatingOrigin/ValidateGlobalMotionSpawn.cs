using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Pure DTO, queue, latch and compiled-seam checks. No GameObjects, networking, physics, scenes or screenshots.</summary>
    public static class ValidateGlobalMotionSpawn
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }
        [MenuItem("ProjectC/World/Floating Origin/Validate Initial Spawn Contracts")]
        public static void Execute()
        {
            var report = Run();
            if (report.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(report, true));
            Debug.Log($"[T-FO04G] {report.passed} pure initial-spawn checks passed; runtime untested.");
        }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var passed = new List<string>(); var failed = new List<string>();
            void Check(string name, Action action) { try { action(); passed.Add(name); } catch (Exception e) { failed.Add(name + ": " + e.GetType().Name + ": " + e.Message); } }
            Check("Default seed cannot become origin spawn", () => Require(!default(GlobalMotionSpawnSeed).IsValid));
            Check("Seed version required", () => { var s = Seed(); s.Version++; Require(!s.IsValid); });
            Check("Seed session and lifetime required", () => { var s = Seed(); s.SessionId = 0; Require(!s.IsValid); s = Seed(); s.SpawnGeneration = 0; Require(!s.IsValid); });
            Check("Seed rejects nonunit rotation", () => { var s = Seed(); s.Rotation = default; Require(!s.IsValid); });
            Check("Seed rejects negative zero and NaN scale", () => { foreach (float x in new[] { -1f, 0f, float.NaN, float.PositiveInfinity }) { var s = Seed(); s.Scale.x = x; Require(!s.IsValid); } });
            Check("Exact seed identity matches", () => Require(Seed().Matches(Binding(), 4)));
            Check("Seed wrong owner rejected", () => Require(!Seed().Matches(Binding(), 5)));
            Check("Seed wrong lifetime session object rejected", () => { for (int i = 0; i < 3; i++) { var b = Binding(); if (i == 0) b.SessionId++; else if (i == 1) b.SpawnGeneration++; else b.NetworkObjectId++; Require(!Seed().Matches(b, 4)); } });
            Check("Seed is placement input not authority or discontinuity authorization", () => { var b = Binding(); b.AuthorityGeneration++; b.DiscontinuityGeneration++; Require(Seed().Matches(b, 4)); });
            Check("Binary seed roundtrip preserves far double coordinates and identity", () => { var s = Seed(); var target = default(GlobalMotionSpawnSeed); Read(Bytes(s), ref target); Require(target.IsValid && target.Position == s.Position && target.Rotation == s.Rotation && target.Scale == s.Scale && target.Matches(Binding(), 4)); });
            Check("Seed writer rejects invalid input before output", () => { using var w = new FastBufferWriter(128, Allocator.Temp); bool threw = false; try { w.WriteNetworkSerializable(default(GlobalMotionSpawnSeed)); } catch (InvalidOperationException) { threw = true; } Require(threw && w.Position == 0); });
            Check("Truncated seed read is atomic", () => { var bytes = Bytes(Seed()); Array.Resize(ref bytes, 13); var target = Seed(); bool threw = false; try { Read(bytes, ref target); } catch { threw = true; } Require(threw && target.Position == Seed().Position && target.Matches(Binding(), 4)); });
            Check("Unknown wire version read is atomic", () => { var bytes = Bytes(Seed()); bytes[0] = 2; var target = Seed(); bool threw = false; try { Read(bytes, ref target); } catch { threw = true; } Require(threw && target.Matches(Binding(), 4)); });
            Check("Same global seed projects into two independent frames", () => { var s = Seed(); var a = new LocalCoordinateFrame(new GlobalPosition(56500, 10, -30000)); var b = new LocalCoordinateFrame(new GlobalPosition(56510, 10, -30000)); Require(a.TryToLocal(s.Position, out var x)); Require(b.TryToLocal(s.Position, out var y)); Require(x.x == 0.125f && y.x == -9.875f && a.ToGlobal(x) == b.ToGlobal(y)); });
            Check("Invalid frame never projects a plan", () => Require(!Plan().TryProject(default, 4, out _)));
            Check("Out of range plan is refused rather than zero spawn", () => { var f = new LocalCoordinateFrame(GlobalPosition.Zero); Require(!Plan().TryProject(f, 4, out _)); });
            Check("Explicit origin zero remains a valid decision", () => { var p = new GlobalMotionPlayerSpawnPlan(1, GlobalPosition.Zero, Quaternion.identity, Vector3.one, _ => true); Require(p.TryProject(new LocalCoordinateFrame(GlobalPosition.Zero), 4, out var v) && v == Vector3.zero); });
            Check("Remote owner requires game-specific rules", () => { var p = new GlobalMotionPlayerSpawnPlan(1, Seed().Position, Quaternion.identity, Vector3.one, null); Require(!p.TryProject(Frame(), 4, out _)); Require(p.TryProject(Frame(), NetworkManager.ServerClientId, out _)); });
            Check("Invalid plan frame rotation or scale rejected", () => { Require(!default(GlobalMotionPlayerSpawnPlan).TryProject(Frame(), 4, out _)); var p = new GlobalMotionPlayerSpawnPlan(1, Seed().Position, default, Vector3.one, _ => true); Require(!p.TryProject(Frame(), 4, out _)); p = new GlobalMotionPlayerSpawnPlan(1, Seed().Position, Quaternion.identity, Vector3.zero, _ => true); Require(!p.TryProject(Frame(), 4, out _)); });
            Check("Unloaded scene descriptor is invalid", () => Require(!new GlobalMotionSpawnFrame(1, Frame(), default).IsValid));
            Check("Queue cannot accept before explicit session", () => Require(!new GlobalMotionSpawnQueue().TryAdd(4, out _)));
            Check("Queue supports real host id zero", () => { var q = Queue(); Require(q.TryAdd(0, out var t) && t.IsValid && t.ClientId == 0 && q.IsCurrent(t)); });
            Check("Duplicate connection does not create another ticket", () => { var q = Queue(); Require(q.TryAdd(4, out var t)); Require(!q.TryAdd(4, out _) && q.Count == 1 && q.IsCurrent(t)); });
            Check("Queue capacity is bounded", () => { var q = new GlobalMotionSpawnQueue(1); q.Begin(); Require(q.TryAdd(4, out _)); Require(!q.TryAdd(5, out _) && q.Count == 1); });
            Check("Canceled ticket cannot complete replacement connection", () => { var q = Queue(); q.TryAdd(4, out var old); q.Cancel(4); q.TryAdd(4, out var fresh); Require(!old.Equals(fresh) && !q.Complete(old) && q.IsCurrent(fresh)); });
            Check("New session retires old tickets", () => { var q = Queue(); q.TryAdd(4, out var old); q.Begin(); q.TryAdd(4, out var fresh); Require(old.Epoch != fresh.Epoch && !q.IsCurrent(old) && q.IsCurrent(fresh)); });
            Check("Complete is one shot", () => { var q = Queue(); q.TryAdd(4, out var t); Require(q.Complete(t) && !q.Complete(t) && q.Count == 0); });
            Check("Clear invalidates all pending tickets", () => { var q = Queue(); q.TryAdd(4, out var t); q.Clear(); Require(q.Count == 0 && !q.IsCurrent(t)); });
            Check("Work snapshot survives cancellation without reviving ticket", () => { var q = Queue(); q.TryAdd(4, out _); var work = new List<GlobalMotionSpawnTicket>(); q.CopyTo(work); q.Cancel(4); Require(work.Count == 1 && !q.IsCurrent(work[0])); });
            Check("1000 reconnects cannot complete stale ticket", () => { var q = Queue(); for (int i = 0; i < 1000; i++) { q.TryAdd(4, out var old); q.Cancel(4); q.TryAdd(4, out var fresh); Require(!q.Complete(old) && q.Complete(fresh)); } });
            Check("Initial latch starts blocked", () => { var l = new GlobalMotionSpawnLatch(); Require(!l.Released && !l.TryRelease(Binding())); });
            Check("Placement alone does not release simulation", () => { var l = new GlobalMotionSpawnLatch(); l.Record(Binding()); Require(!l.Released); });
            Check("Only exact applied baseline releases initial latch", () => { var l = new GlobalMotionSpawnLatch(); l.Record(Binding()); var b = Binding(); b.DiscontinuityGeneration++; Require(!l.TryRelease(b) && !l.Released && l.TryRelease(Binding()) && l.Released); });
            Check("Repeated baseline preserves initial release", () => { var l = Released(); l.Record(Binding()); Require(l.Released && l.TryRelease(Binding())); });
            Check("Later authority or parent changes do not repeat initial controller activation", () => { var l = Released(); var b = Binding(); b.AuthorityGeneration++; b.DiscontinuityGeneration++; b.Space = MotionCoordinateSpace.ParentLocal; b.ParentNetworkObjectId = 77; b.ParentSpawnGeneration = 8; l.Record(b); Require(l.Released && !l.TryRelease(Binding())); });
            Check("New session object or spawn lifetime re-arms initial hold", () => { for (int i = 0; i < 3; i++) { var l = Released(); var b = Binding(); if (i == 0) b.SessionId++; else if (i == 1) b.NetworkObjectId++; else b.SpawnGeneration++; l.Record(b); Require(!l.Released && !l.TryRelease(Binding()) && l.TryRelease(b)); } });
            Check("Despawn reset clears initial release", () => { var l = Released(); l.Reset(); Require(!l.Released && !l.Applied.IsValid && !l.TryRelease(Binding())); });
            Check("Invalid record preserves good latch", () => { var l = Released(); bool threw = false; try { l.Record(default); } catch (ArgumentException) { threw = true; } Require(threw && l.Released && l.Applied == Binding()); });
            Check("Concrete bootstrap exposes startup and cleanup contracts", () => { Require(typeof(IGlobalMotionSpawnBootstrap).IsAssignableFrom(typeof(GlobalMotionPlayerBootstrap))); Require(typeof(IGlobalMotionSpawnBootstrapLifecycle).IsAssignableFrom(typeof(GlobalMotionPlayerBootstrap))); });
            Check("Prepared content source is explicit serialized dependency", () => { var f = typeof(GlobalMotionPlayerBootstrap).GetField("_sourceBehaviour", BindingFlags.Instance | BindingFlags.NonPublic); Require(f != null && f.IsDefined(typeof(SerializeField), false)); });
            Check("Installed NGO supports typed pre-instantiation payload", () => Require(typeof(INetworkPrefabInstanceHandler).IsAssignableFrom(typeof(NetworkPrefabInstanceHandlerWithData<GlobalMotionSpawnSeed>))));
            Check("Post-spawn compiled hook is protected", () => { var m = typeof(GlobalMotionReplicator).GetMethod("OnNetworkPostSpawn", BindingFlags.Instance | BindingFlags.NonPublic); Require(m != null && m.IsFamily); });
            Check("Player has separate initial hold/release without using input-state API", () => { foreach (string name in new[] { "PrepareGlobalInitialSpawn", "ReleaseGlobalInitialSpawn" }) Require(typeof(ProjectC.Player.NetworkPlayer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) != null); });
            Check("G protocol rejects F hello and keeps legacy protocol outside reserved range", () => { Require(GlobalMotionNetworkContract.ProtocolVersion == 0xF003 && !GlobalMotionNetworkContract.IsReservedProtocolVersion(0)); var digest = new byte[32]; digest[0] = 1; var current = GlobalMotionNetworkContract.CreateHello(digest, digest); var old = (byte[])current.Clone(); old[6] = 2; old[7] = 0xF0; Require(!GlobalMotionNetworkContract.ValidateHello(old, current, out _)); });
            return new Report { passed = passed.Count, failed = failed.Count, checks = passed.ToArray(), failures = failed.ToArray() };
        }
        private static MotionStreamBinding Binding() => new MotionStreamBinding { SessionId = 71, NetworkObjectId = 8, SpawnGeneration = 2, AuthorityGeneration = 1, DiscontinuityGeneration = 1 };
        private static GlobalMotionSpawnSeed Seed() => new GlobalMotionSpawnSeed { Version = 1, SessionId = 71, ObjectId = 8, SpawnGeneration = 2, OwnerId = 4, Position = new GlobalPosition(56500.125, 10.25, -30000.5), Rotation = Quaternion.identity, Scale = Vector3.one };
        private static LocalCoordinateFrame Frame() => new LocalCoordinateFrame(new GlobalPosition(56500, 10, -30000));
        private static GlobalMotionPlayerSpawnPlan Plan() => new GlobalMotionPlayerSpawnPlan(1, Seed().Position, Seed().Rotation, Seed().Scale, _ => true);
        private static GlobalMotionSpawnQueue Queue() { var q = new GlobalMotionSpawnQueue(); q.Begin(); return q; }
        private static GlobalMotionSpawnLatch Released() { var l = new GlobalMotionSpawnLatch(); l.Record(Binding()); l.TryRelease(Binding()); return l; }
        private static byte[] Bytes(GlobalMotionSpawnSeed value) { using var writer = new FastBufferWriter(128, Allocator.Temp); writer.WriteNetworkSerializable(value); return writer.ToArray(); }
        private static void Read(byte[] bytes, ref GlobalMotionSpawnSeed value) { using var reader = new FastBufferReader(bytes, Allocator.Temp); reader.ReadNetworkSerializableInPlace(ref value); }
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
    }
}
