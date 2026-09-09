using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;
using ProjectC.World.FloatingOrigin.Persistence;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Pure resolver/plan guard/storage observation checks. Never instantiate a scene, source MonoBehaviour or NetworkObject.</summary>
    public static class ValidateGlobalCheckpointSpawn
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }
        [MenuItem("ProjectC/World/Floating Origin/Validate Checkpoint Spawn Plans")]
        public static void Execute() { var r = Run(); if (r.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(r, true)); Debug.Log($"[T-FO05D] {r.passed} pure checks PASS; native factory/auth/frame operations UNTESTED."); }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var ok = new List<string>(); var bad = new List<string>();
            void Check(string n, Action a) { try { a(); ok.Add(n); } catch (Exception e) { bad.Add(n + ": " + e.GetType().Name + ": " + e.Message); } }
            Check("Restore exact saved global doubles into explicit prepared frame", () => { var c = new Case(); c.Save(Record()); var r = Resolve(c); Require(r.Kind == GlobalPlayerSpawnResolutionKind.RestoredCheckpoint && r.Plan.Position == Record().Position && r.Checkpoint.PlayerId == "account-a"); Require(r.Plan.TryProject(Frame(), 1, out var local) && Math.Abs(local.x - 0.000000123f) < 0.000000001f); });
            Check("No checkpoint is not an implicit origin fallback", () => { var c = new Case(); Require(!Try(c, null, out _, out _)); });
            Check("First spawn uses an explicitly provided global point", () => { var c = new Case(); Require(Try(c, new GlobalPosition(56501, 2, 3), out var r, out _)); Require(r.Kind == GlobalPlayerSpawnResolutionKind.ExplicitFirstSpawn && r.Checkpoint == null && r.Plan.Position.X == 56501); });
            Check("Explicit origin allowed only when explicitly requested and representable", () => { var c = new Case(); Require(GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 0, c.Repository.Inspect(), 9, new LocalCoordinateFrame(GlobalPosition.Zero), Quaternion.identity, Vector3.one, null, GlobalPosition.Zero, Guid.NewGuid(), out var r, out _) && r.Plan.Position == GlobalPosition.Zero); });
            Check("Saved checkpoint wins over supplied first-spawn point", () => { var c = new Case(); c.Save(Record()); Require(Try(c, new GlobalPosition(1e9, 1, 1), out var r, out _) && r.Kind == GlobalPlayerSpawnResolutionKind.RestoredCheckpoint && r.Plan.Position == Record().Position); });
            Check("Unknown player in Ready store needs explicit first-spawn", () => { var c = new Case(); c.Save(Record("offline")); Require(!Try(c, null, out _, out _)); Require(Try(c, new GlobalPosition(56500, 0, 0), out var r, out _) && r.Checkpoint == null); });
            Check("Stable identity match is ordinal and exact", () => { var c = new Case(); c.Save(Record("Account-A")); Require(!Try(c, null, out _, out _)); });
            Check("Missing or malformed persistent identity is refused", () => { var c = new Case(); foreach (var id in new[] { "", " ", "\n" }) Require(!GlobalPlayerSpawnPlanResolver.TryResolve(id, 1, c.Repository.Inspect(), 9, Frame(), Quaternion.identity, Vector3.one, Rules, Record().Position, Guid.NewGuid(), out _, out _)); });
            Check("Lease nonce must be nonempty", () => { var c = new Case(); Require(!GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, c.Repository.Inspect(), 9, Frame(), Quaternion.identity, Vector3.one, Rules, Record().Position, Guid.Empty, out _, out _)); });
            Check("Null observation is not Empty", () => Require(!GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, null, 9, Frame(), Quaternion.identity, Vector3.one, Rules, Record().Position, Guid.NewGuid(), out _, out _)));
            Check("Pending is not converted into first-spawn", () => { var c = new Case(); c.Store.Pending = new byte[] { 1 }; Require(!Try(c, Record().Position, out _, out _)); });
            Check("Corrupt primary is not converted into first-spawn", () => { var c = new Case(); c.Store.Primary = new byte[] { 1 }; Require(!Try(c, Record().Position, out _, out _)); });
            Check("Backup recovery remains explicit rather than restore source fallback", () => { var c = new Case(); c.Save(Record()); c.Save(Record("account-a", 201)); c.Store.Primary = new byte[] { 1 }; Require(c.Repository.Inspect().Status == CheckpointStoreStatus.RecoveryAvailable && !Try(c, Record().Position, out _, out _)); });
            Check("Ship affinity checkpoint cannot silently become on-foot first-spawn", () => { var c = new Case(); c.Save(new GlobalPlayerPositionRecord("account-a", Record().Position, true, "ship-a", 200)); Require(!Try(c, Record().Position, out _, out var e) && e.Contains("ship")); });
            Check("Offline ship-affinity record belonging to another player is untouched", () => { var c = new Case(); c.Save(new GlobalPlayerPositionRecord("offline", GlobalPosition.Zero, true, "ship-a", 200)); Require(Try(c, Record().Position, out var r, out _) && r.Checkpoint == null && c.Repository.Inspect().Snapshot.Players[0].InShip); });
            Check("Out-of-frame saved position refuses even with a valid first-spawn fallback", () => { var c = new Case(); c.Save(new GlobalPlayerPositionRecord("account-a", new GlobalPosition(1e9, 2, 3), false, "", 200)); Require(!Try(c, Record().Position, out _, out _)); });
            Check("Out-of-frame first-spawn refuses", () => { var c = new Case(); Require(!Try(c, new GlobalPosition(1e9, 0, 0), out _, out _)); });
            Check("NaN and infinity first-spawn refused", () => { var c = new Case(); var p = GlobalPosition.Zero; p.X = double.NaN; Require(!Try(c, p, out _, out _)); p.X = double.PositiveInfinity; Require(!Try(c, p, out _, out _)); });
            Check("Default frame is invalid rather than zero origin", () => { var c = new Case(); Require(!GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, c.Repository.Inspect(), 9, default, Quaternion.identity, Vector3.one, Rules, Record().Position, Guid.NewGuid(), out _, out _)); });
            Check("Nonpositive frame ID refused", () => { var c = new Case(); foreach (int id in new[] { 0, -1 }) Require(!GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, c.Repository.Inspect(), id, Frame(), Quaternion.identity, Vector3.one, Rules, Record().Position, Guid.NewGuid(), out _, out _)); });
            Check("Remote owner needs explicit game-rule validator", () => { var c = new Case(); Require(!GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, c.Repository.Inspect(), 9, Frame(), Quaternion.identity, Vector3.one, null, Record().Position, Guid.NewGuid(), out _, out _)); });
            Check("Actual Host owner ID zero follows existing G rule", () => { var c = new Case(); Require(GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 0, c.Repository.Inspect(), 9, Frame(), Quaternion.identity, Vector3.one, null, Record().Position, Guid.NewGuid(), out _, out _)); });
            Check("Resolver does not execute a pose admission validator while projecting", () => { var c = new Case(); int calls = 0; Func<GlobalMotionSnapshot, bool> rule = _ => { calls++; return false; }; Require(GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, c.Repository.Inspect(), 9, Frame(), Quaternion.identity, Vector3.one, rule, Record().Position, Guid.NewGuid(), out var r, out _) && calls == 0 && ReferenceEquals(r.Plan.OwnerRules, rule)); });
            Check("Zero quaternion refused rather than normalized or guessed", () => { var c = new Case(); Require(!GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, c.Repository.Inspect(), 9, Frame(), default, Vector3.one, Rules, Record().Position, Guid.NewGuid(), out _, out _)); });
            Check("Missing/negative/nonfinite scale refused", () => { var c = new Case(); foreach (var scale in new[] { Vector3.zero, new Vector3(-1, 1, 1), new Vector3(float.NaN, 1, 1), new Vector3(float.PositiveInfinity, 1, 1) }) Require(!GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, c.Repository.Inspect(), 9, Frame(), Quaternion.identity, scale, Rules, Record().Position, Guid.NewGuid(), out _, out _)); });
            Check("Explicit rotation and scale survive resolver exactly", () => { var c = new Case(); var rotation = new Quaternion(0, 0, 0, 1.0001f); var scale = new Vector3(1, 2, 3); Require(GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, c.Repository.Inspect(), 9, Frame(), rotation, scale, Rules, Record().Position, Guid.NewGuid(), out var r, out _)); var s = GlobalMotionSnapshot.CreateWorld(Binding(), 0, 1, r.Plan.Position, r.Plan.Rotation, r.Plan.Scale); Require(s.Rotation.Equals(rotation) && s.Scale.Equals(scale)); });
            Check("Far-world first-spawn retains submillimetre global precision", () => { var c = new Case(); var position = new GlobalPosition(56500.000000123, 1, 2); Require(Try(c, position, out var r, out _) && r.Plan.Position.X.Equals(position.X)); });
            Check("Resolver reads but never writes repository", () => { var c = new Case(); var before = c.Store.Stages; Require(Try(c, Record().Position, out _, out _)); Require(c.Store.Stages == before && c.Store.Publications == 0); });
            Check("Immutable resolution has no setter for identity/checkpoint/plan", () => { foreach (var p in typeof(GlobalPlayerSpawnResolution).GetProperties()) Require(!p.CanWrite); });
            Check("Same plan matches exact reservation payload", () => { var p = Plan(); Require(GlobalPlayerSpawnPlanResolver.SamePlan(p, p)); });
            Check("Another reservation cannot impersonate an identical pose", () => { var p = Plan(); Require(!GlobalPlayerSpawnPlanResolver.SamePlan(p, Plan(Guid.NewGuid()))); });
            Check("Default unreserved plans cannot pass checkpoint SamePlan", () => { var p = new GlobalMotionPlayerSpawnPlan(9, Record().Position, Quaternion.identity, Vector3.one, Rules); Require(!GlobalPlayerSpawnPlanResolver.SamePlan(p, p)); });
            Check("Frame, global point, rotation and scale mutations invalidate reservation", () => { var p = Plan(); Require(!GlobalPlayerSpawnPlanResolver.SamePlan(p, new GlobalMotionPlayerSpawnPlan(10, p.Position, p.Rotation, p.Scale, p.OwnerRules, p.ReservationId))); Require(!GlobalPlayerSpawnPlanResolver.SamePlan(p, new GlobalMotionPlayerSpawnPlan(9, GlobalPosition.Zero, p.Rotation, p.Scale, p.OwnerRules, p.ReservationId))); Require(!GlobalPlayerSpawnPlanResolver.SamePlan(p, new GlobalMotionPlayerSpawnPlan(9, p.Position, Quaternion.Euler(0, 1, 0), p.Scale, p.OwnerRules, p.ReservationId))); Require(!GlobalPlayerSpawnPlanResolver.SamePlan(p, new GlobalMotionPlayerSpawnPlan(9, p.Position, p.Rotation, Vector3.one * 2, p.OwnerRules, p.ReservationId))); });
            Check("Different rule delegate invalidates reservation even with same behavior", () => { var p = Plan(); Func<GlobalMotionSnapshot, bool> other = new Func<GlobalMotionSnapshot, bool>(Allow); Require(!ReferenceEquals(p.OwnerRules, other)); Require(!GlobalPlayerSpawnPlanResolver.SamePlan(p, new GlobalMotionPlayerSpawnPlan(9, p.Position, p.Rotation, p.Scale, other, p.ReservationId))); });
            Check("Lease accepts exact endpoints", () => { Require(GlobalPlayerSpawnPlanResolver.WithinLease(10, 20, 10)); Require(GlobalPlayerSpawnPlanResolver.WithinLease(10, 20, 20)); });
            Check("Lease expiry and time regression refused", () => { Require(!GlobalPlayerSpawnPlanResolver.WithinLease(10, 20, 20.001)); Require(!GlobalPlayerSpawnPlanResolver.WithinLease(10, 20, 9.99)); });
            Check("Unbounded/negative/invalid lease rejected", () => { Require(!GlobalPlayerSpawnPlanResolver.WithinLease(10, 10, 10)); Require(!GlobalPlayerSpawnPlanResolver.WithinLease(-1, 10, 1)); Require(!GlobalPlayerSpawnPlanResolver.WithinLease(10, 71, 10)); Require(!GlobalPlayerSpawnPlanResolver.WithinLease(10, 20, double.NaN)); Require(!GlobalPlayerSpawnPlanResolver.WithinLease(10, double.PositiveInfinity, 11)); });
            Check("Pending reservation may be cancelled exactly once by its own nonce", () => { var id = Guid.NewGuid(); Require(GlobalPlayerSpawnPlanResolver.CanCancel(false, id, id)); Require(!GlobalPlayerSpawnPlanResolver.CanCancel(false, id, Guid.NewGuid())); Require(!GlobalPlayerSpawnPlanResolver.CanCancel(false, Guid.Empty, Guid.Empty)); });
            Check("Late cancellation cannot detach confirmed player's capture identity", () => { var id = Guid.NewGuid(); Require(!GlobalPlayerSpawnPlanResolver.CanCancel(true, id, id)); });
            Check("Empty observation fence succeeds without staging", () => { var c = new Case(); Require(c.Repository.IsObservationCurrent(c.Repository.Inspect(), out _) && c.Store.Stages == 0); });
            Check("Ready observation fence succeeds", () => { var c = new Case(); c.Save(Record()); Require(c.Repository.IsObservationCurrent(c.Repository.Inspect(), out _)); });
            Check("Null or foreign observation rejected even for identical store bytes", () => { var c = new Case(); var other = new GlobalPlayerCheckpointRepository("world-a", c.Store); Require(!c.Repository.IsObservationCurrent(null, out _)); Require(!c.Repository.IsObservationCurrent(other.Inspect(), out _)); });
            Check("Store changed after prepare invalidates original observation", () => { var c = new Case(); var old = c.Repository.Inspect(); c.Save(Record()); Require(!c.Repository.IsObservationCurrent(old, out _)); });
            Check("Another commit with identical records still invalidates old lineage", () => { var c = new Case(); c.Save(Record()); var old = c.Repository.Inspect(); c.Save(Record()); Require(!c.Repository.IsObservationCurrent(old, out _)); });
            Check("Backup and pending bytes are included in restore fence", () => { var c = new Case(); c.Save(Record()); var old = c.Repository.Inspect(); c.Store.Backup = new byte[] { 1 }; Require(!c.Repository.IsObservationCurrent(old, out _)); c.Store.Backup = null; c.Store.Pending = new byte[] { 2 }; Require(!c.Repository.IsObservationCurrent(old, out _)); });
            Check("Unchanged Pending observation is still ineligible", () => { var c = new Case(); c.Store.Pending = new byte[] { 1 }; Require(!c.Repository.IsObservationCurrent(c.Repository.Inspect(), out _)); });
            Check("Read failure returns false and does not mutate payload", () => { var c = new Case(); var o = c.Repository.Inspect(); c.Store.FailRead = true; Require(!c.Repository.IsObservationCurrent(o, out _) && c.Store.Stages == 0); });
            Check("Read fence rejects reentrant lease acquisition", () => { var c = new Case(); var o = c.Repository.Inspect(); using (c.Store.AcquireLease()) Require(!c.Repository.IsObservationCurrent(o, out _)); });
            Check("Null source blocks validation and confirmation", () => { var p = Plan(); Require(!GlobalMotionSpawnPlanGuards.Validate(null, 1, p, out _)); Require(!GlobalMotionSpawnPlanGuards.Confirm(null, 1, p, null, out _)); });
            Check("Reserved plan cannot bypass a source guard", () => { var s = new PlainSource(); var p = Plan(); Require(!GlobalMotionSpawnPlanGuards.Validate(s, 1, p, out _)); Require(!GlobalMotionSpawnPlanGuards.Confirm(s, 1, p, null, out _)); });
            Check("Existing unreserved source compatibility remains explicit", () => { var s = new PlainSource(); var p = new GlobalMotionPlayerSpawnPlan(9, Record().Position, Quaternion.identity, Vector3.one, Rules); Require(GlobalMotionSpawnPlanGuards.Validate(s, 1, p, out _)); Require(GlobalMotionSpawnPlanGuards.Confirm(s, 1, p, null, out _)); });
            Check("Guard receives correct identity and exact prepared plan", () => { var s = new GuardSource(); var p = Plan(); Require(GlobalMotionSpawnPlanGuards.Validate(s, 3, p, out _) && s.Client == 3 && GlobalPlayerSpawnPlanResolver.SamePlan(p, s.Seen)); });
            Check("Guard rejection and exception are fail-closed", () => { var s = new GuardSource { Valid = false }; Require(!GlobalMotionSpawnPlanGuards.Validate(s, 1, Plan(), out _)); s.Throw = true; Require(!GlobalMotionSpawnPlanGuards.Validate(s, 1, Plan(), out var e) && e.Contains("guard")); });
            Check("Guard confirmation rejection and exception are fail-closed", () => { var s = new GuardSource { Confirmed = false }; Require(!GlobalMotionSpawnPlanGuards.Confirm(s, 1, Plan(), null, out _)); s.Throw = true; Require(!GlobalMotionSpawnPlanGuards.Confirm(s, 1, Plan(), null, out _)); });
            Check("Cancellation forwards only the requested reservation", () => { var s = new GuardSource(); var p = Plan(); GlobalMotionSpawnPlanGuards.Cancel(s, 3, p); Require(s.Client == 3 && s.Cancelled == p.ReservationId); GlobalMotionSpawnPlanGuards.Cancel(new PlainSource(), 3, p); });
            Check("Source replacement cannot validate old reservation in a different guard", () => { var p = Plan(); var s = new GuardSource { Expected = Plan(Guid.NewGuid()) }; Require(!GlobalMotionSpawnPlanGuards.Validate(s, 1, p, out _)); });
            Check("Readonly source plan can be revalidated before and after simulated Awake", () => { var s = new GuardSource(); var p = Plan(); Require(GlobalMotionSpawnPlanGuards.Validate(s, 1, p, out _)); s.Expected = Plan(Guid.NewGuid()); Require(!GlobalMotionSpawnPlanGuards.Validate(s, 1, p, out _)); });
            Check("Nonce does not leak into motion wire seed or saved record", () => { Require(typeof(GlobalMotionSpawnSeed).GetField("ReservationId") == null && typeof(GlobalPlayerPositionRecord).GetProperty("ReservationId") == null); var c = new Case(); c.Save(Record()); Require(GlobalPlayerCheckpointSnapshotCodec.TryEncode(c.Repository.Inspect().Snapshot, out var bytes, out _)); string json = Encoding.UTF8.GetString(bytes); Require(!json.Contains("ReservationId") && !json.Contains("FrameId") && !json.Contains("ClientId")); });
            Check("Concrete source implements G and its guarded lifecycle", () => { Require(typeof(IGlobalMotionPlayerSpawnSource).IsAssignableFrom(typeof(GlobalMotionCheckpointSpawnSource))); Require(typeof(IGlobalMotionPlayerSpawnPlanGuard).IsAssignableFrom(typeof(GlobalMotionCheckpointSpawnSource))); });
            Check("Compiled integration and retirement seams exist without native invocation", () => { Require(typeof(GlobalMotionCheckpointSpawnSource).GetMethod("ConfigureForStart") != null && typeof(GlobalMotionCheckpointSpawnSource).GetMethod("TryAuthorizeVerifiedConnection") != null && typeof(GlobalMotionCheckpointSpawnSource).GetMethod("ReleaseDisconnectedPlayer") != null && typeof(GlobalMotionPlayerCheckpointSource).GetMethod("TryRetire") != null); });
            return new Report { passed = ok.Count, failed = bad.Count, checks = ok.ToArray(), failures = bad.ToArray() };
        }
        private static readonly Func<GlobalMotionSnapshot, bool> Rules = Allow;
        private static bool Allow(GlobalMotionSnapshot value) => true;
        private static MotionStreamBinding Binding() => new MotionStreamBinding { SessionId = 7, NetworkObjectId = 0, SpawnGeneration = 1, AuthorityGeneration = 1, DiscontinuityGeneration = 1, Space = MotionCoordinateSpace.World };
        private static LocalCoordinateFrame Frame() => new LocalCoordinateFrame(new GlobalPosition(56500, 0, 0));
        private static GlobalPlayerPositionRecord Record(string id = "account-a", long at = 200) => new GlobalPlayerPositionRecord(id, new GlobalPosition(56500.000000123, 2, 3), false, "", at);
        private static GlobalMotionPlayerSpawnPlan Plan(Guid? id = null) => new GlobalMotionPlayerSpawnPlan(9, Record().Position, Quaternion.identity, Vector3.one, Rules, id ?? Guid.NewGuid());
        private static bool Try(Case c, GlobalPosition? first, out GlobalPlayerSpawnResolution result, out string error) => GlobalPlayerSpawnPlanResolver.TryResolve("account-a", 1, c.Repository.Inspect(), 9, Frame(), Quaternion.identity, Vector3.one, Rules, first, Guid.NewGuid(), out result, out error);
        private static GlobalPlayerSpawnResolution Resolve(Case c) { Require(Try(c, null, out var result, out var error), error); return result; }
        private class PlainSource : IGlobalMotionPlayerSpawnSource
        {
            public IReadOnlyList<GlobalMotionSpawnFrame> PreparedFrames => Array.Empty<GlobalMotionSpawnFrame>();
            public bool ValidatePreparedContent(GlobalMotionStartRole role, GlobalMotionNetworkProfile profile, out string error) { error = null; return true; }
            public bool TryGetPlayerPlan(ulong id, out GlobalMotionPlayerSpawnPlan plan) { plan = default; return false; }
            public bool TryGetReplicaFrame(GlobalMotionSpawnSeed seed, out int id) { id = 0; return false; }
        }
        private sealed class GuardSource : PlainSource, IGlobalMotionPlayerSpawnPlanGuard
        {
            public bool Valid = true, Confirmed = true, Throw; public ulong Client; public GlobalMotionPlayerSpawnPlan Seen, Expected; public Guid Cancelled;
            public bool ValidatePlayerPlan(ulong id, GlobalMotionPlayerSpawnPlan plan, out string error) { error = null; if (Throw) throw new InvalidOperationException(); Client = id; Seen = plan; return Valid && (Expected.ReservationId == Guid.Empty || GlobalPlayerSpawnPlanResolver.SamePlan(Expected, plan)); }
            public bool ConfirmPlayerSpawn(ulong id, GlobalMotionPlayerSpawnPlan plan, NetworkObject value, out string error) { error = null; if (Throw) throw new InvalidOperationException(); return Confirmed; }
            public void CancelPlayerPlan(ulong id, Guid reservation) { Client = id; Cancelled = reservation; }
            public void ReleaseDisconnectedPlayer(ulong id) { Client = id; }
        }
        private sealed class Case
        {
            public readonly MemoryStorage Store = new MemoryStorage(); public readonly GlobalPlayerCheckpointRepository Repository;
            public Case() { Repository = new GlobalPlayerCheckpointRepository("world-a", Store); }
            public void Save(params GlobalPlayerPositionRecord[] records) { var r = Repository.TryCommit(Repository.Inspect(), records); Require(r.Status == CheckpointTransactionStatus.Applied, r.Error); }
        }
        private sealed class MemoryStorage : IGlobalPlayerCheckpointStorage
        {
            public byte[] Primary, Backup, Pending; public bool FailRead; public int Stages, Publications;
            private bool _held; private readonly Dictionary<string, byte[]> _quarantine = new Dictionary<string, byte[]>();
            public IDisposable AcquireLease() { if (_held) throw new IOException("lease_held"); _held = true; return new Release(this); }
            public byte[] Read(CheckpointSlot slot, int max) { Guard(); if (FailRead) throw new IOException("read_failed"); var b = slot == CheckpointSlot.Primary ? Primary : slot == CheckpointSlot.Backup ? Backup : Pending; if (b != null && b.Length > max) throw new IOException("oversized"); return Clone(b); }
            public void WritePendingNew(byte[] b) { Guard(); if (Pending != null) throw new IOException("pending_exists"); Stages++; Pending = Clone(b); }
            public void PublishPending(bool replace, string q) { Guard(); if (Pending == null || (Primary != null) != replace) throw new IOException("bad_publish"); Publications++; if (replace) { if (q == null) Backup = Clone(Primary); else _quarantine.Add(q, Clone(Primary)); } Primary = Pending; Pending = null; }
            public void QuarantinePending(string q) { Guard(); _quarantine.Add(q, Clone(Pending)); Pending = null; }
            public byte[] ReadQuarantine(string q, int max) { Guard(); return _quarantine.TryGetValue(q, out var b) ? Clone(b) : null; }
            private static byte[] Clone(byte[] b) => b == null ? null : (byte[])b.Clone();
            private void Guard() { if (!_held) throw new InvalidOperationException("lease_required"); }
            private sealed class Release : IDisposable { private MemoryStorage _s; public Release(MemoryStorage s) { _s = s; } public void Dispose() { if (_s != null) _s._held = false; _s = null; } }
        }
        private static void Require(bool value, string message = null) { if (!value) throw new InvalidOperationException(message ?? "Assertion failed."); }
    }
}
