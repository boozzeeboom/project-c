using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;
using ProjectC.World.FloatingOrigin.Persistence;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Pure session policies/scheduling plus real C/B against memory storage. Does NOT exercise the native coordinator, auth, GUI or NGO.</summary>
    public static class ValidateGlobalSessionOrchestration
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }
        [MenuItem("ProjectC/World/Floating Origin/Validate Session Orchestration Policies")]
        public static void Execute() { var r = Run(); if (r.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(r, true)); Debug.Log($"[T-FO05E] {r.passed} pure checks PASS; native orchestration/auth/UI UNTESTED."); }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var ok = new List<string>(); var bad = new List<string>();
            void Check(string n, Action a) { try { a(); ok.Add(n); } catch (Exception e) { bad.Add(n + ": " + e.GetType().Name + ": " + e.Message); } }
            Check("Trusted peer request retains explicit identity and spatial policy", () => { var p = Peer(); Require(p.PlayerId == "account-a" && p.FrameId == 9 && p.FirstSpawn.Value.X == 56500.000000123 && ReferenceEquals(p.OwnerRules, Rules)); });
            Check("No first-spawn remains absent rather than default origin", () => { var p = new GlobalSessionPeerPlan("account-a", 9, Quaternion.identity, Vector3.one, Rules, null); Require(!p.FirstSpawn.HasValue); });
            Check("Malformed identity rejected", () => { foreach (var id in new[] { "", " ", "\n" }) Throws(() => new GlobalSessionPeerPlan(id, 9, Quaternion.identity, Vector3.one, Rules, null)); });
            Check("Invalid frame ID rejected", () => { foreach (int id in new[] { 0, -1 }) Throws(() => new GlobalSessionPeerPlan("account-a", id, Quaternion.identity, Vector3.one, Rules, null)); });
            Check("Invalid rotation/scale rejected", () => { Throws(() => new GlobalSessionPeerPlan("account-a", 9, default, Vector3.one, Rules, null)); foreach (var scale in new[] { Vector3.zero, new Vector3(-1,1,1), new Vector3(float.NaN,1,1) }) Throws(() => new GlobalSessionPeerPlan("account-a", 9, Quaternion.identity, scale, Rules, null)); });
            Check("Nonfinite first-spawn rejected", () => { var p = GlobalPosition.Zero; p.X = double.NaN; Throws(() => new GlobalSessionPeerPlan("account-a", 9, Quaternion.identity, Vector3.one, Rules, p)); p.X = double.PositiveInfinity; Throws(() => new GlobalSessionPeerPlan("account-a", 9, Quaternion.identity, Vector3.one, Rules, p)); });
            Check("Remote owner requires explicit admission rules", () => { var p = new GlobalSessionPeerPlan("account-a", 9, Quaternion.identity, Vector3.one, null, null); Require(p.CanServe(0) && !p.CanServe(1)); Require(Peer().CanServe(1)); });
            Check("Peer request is immutable", () => { foreach (var p in typeof(GlobalSessionPeerPlan).GetProperties()) Require(!p.CanWrite); });
            Check("Explicit timing preserved", () => { var t = Timing(); Require(t.CheckpointInterval == 5 && t.SampleAge == 2 && t.PlanLifetime == 30 && t.RetryDelay == 1); });
            Check("Checkpoint interval is bounded", () => { foreach (double v in new[] { 0d, -1d, 3601d, double.NaN, double.PositiveInfinity }) Throws(() => new GlobalSessionTiming(v, 2, 30, 0.1)); new GlobalSessionTiming(1, 2, 30, 0.1); new GlobalSessionTiming(3600, 2, 30, 1); });
            Check("Sample freshness is explicit and bounded", () => { foreach (double v in new[] { 0d, -1d, 61d, double.NaN, double.PositiveInfinity }) Throws(() => new GlobalSessionTiming(5, v, 30, 1)); new GlobalSessionTiming(5, 60, 30, 1); });
            Check("Spawn plan lifetime is bounded", () => { foreach (double v in new[] { 0d, -1d, 61d, double.NaN, double.PositiveInfinity }) Throws(() => new GlobalSessionTiming(5, 2, v, 1)); new GlobalSessionTiming(5, 2, 60, 1); });
            Check("Deferred retry has a positive bounded delay", () => { foreach (double v in new[] { 0d, 0.01d, 6d, double.NaN, double.PositiveInfinity }) Throws(() => new GlobalSessionTiming(5, 2, 30, v)); new GlobalSessionTiming(5, 2, 30, 0.1); });
            Check("Host requires explicitly supplied identity policy", () => Require(!Start(GlobalMotionStartRole.Host, 0, true, CheckpointStoreStatus.Ready, false)));
            Check("Host Ready and Empty repositories permitted with explicit identity", () => { Require(Start(GlobalMotionStartRole.Host, 0, true, CheckpointStoreStatus.Ready, true)); Require(Start(GlobalMotionStartRole.Host, 0, true, CheckpointStoreStatus.Empty, true)); });
            Check("Dedicated server requires store but no fabricated local player", () => { Require(Start(GlobalMotionStartRole.Server, 0, true, CheckpointStoreStatus.Ready, false)); Require(!Start(GlobalMotionStartRole.Server, 0, false, CheckpointStoreStatus.Empty, false)); });
            Check("Client does not require a local server repository", () => Require(Start(GlobalMotionStartRole.Client, 0, false, CheckpointStoreStatus.Blocked, false)));
            Check("Every nonzero legacy-instance count blocks all roles", () => { foreach (var role in new[] { GlobalMotionStartRole.Host, GlobalMotionStartRole.Server, GlobalMotionStartRole.Client }) foreach (int count in new[] { 1, 2, -1 }) Require(!Start(role, count, true, CheckpointStoreStatus.Ready, true)); });
            Check("Pending RecoveryAvailable Blocked never become fresh empty server store", () => { foreach (var status in new[] { CheckpointStoreStatus.Pending, CheckpointStoreStatus.RecoveryAvailable, CheckpointStoreStatus.Blocked, (CheckpointStoreStatus)99 }) Require(!Start(GlobalMotionStartRole.Server, 0, true, status, false)); });
            Check("Invalid start role fails closed", () => Require(!Start((GlobalMotionStartRole)99, 0, true, CheckpointStoreStatus.Ready, true)));
            Check("Role matrix permits only exact Host Server and Client combinations", () => { foreach (var role in new[] { GlobalMotionStartRole.Host, GlobalMotionStartRole.Server, GlobalMotionStartRole.Client }) for (int mask = 0; mask < 8; mask++) { bool s = (mask & 1) != 0, c = (mask & 2) != 0, h = (mask & 4) != 0; int expected = role == GlobalMotionStartRole.Host ? 7 : role == GlobalMotionStartRole.Server ? 1 : 2; Require(GlobalSessionPersistencePolicy.MatchesRole(role, s, c, h) == (mask == expected)); } Require(!GlobalSessionPersistencePolicy.MatchesRole((GlobalMotionStartRole)99, true, true, true)); });
            Check("Current receipt requires exact opaque peer reference and session token", () => { var id = Guid.NewGuid(); var peer = new object(); Require(GlobalSessionPersistencePolicy.IsCurrentPeerReceipt(id, id, peer, peer)); });
            Check("Prior-session auth result cannot bind a new session", () => { var peer = new object(); Require(!GlobalSessionPersistencePolicy.IsCurrentPeerReceipt(Guid.NewGuid(), Guid.NewGuid(), peer, peer)); Require(!GlobalSessionPersistencePolicy.IsCurrentPeerReceipt(Guid.Empty, Guid.Empty, peer, peer)); });
            Check("Reused client ID with another peer instance is not the same receipt", () => { var id = Guid.NewGuid(); Require(!GlobalSessionPersistencePolicy.IsCurrentPeerReceipt(id, id, new object(), new object())); });
            Check("Missing current/submitted peer rejected", () => { var id = Guid.NewGuid(); Require(!GlobalSessionPersistencePolicy.IsCurrentPeerReceipt(id, id, null, null)); Require(!GlobalSessionPersistencePolicy.IsCurrentPeerReceipt(id, id, new object(), null)); });
            Check("Schedule does not write immediately at session start", () => { var s = Schedule(); Require(s.NextAttempt == 105 && !s.IsDue(100) && !s.IsDue(104.999)); });
            Check("Schedule becomes due exactly at interval boundary", () => { var s = Schedule(); Require(s.IsDue(105)); });
            Check("Applied checkpoint schedules next interval", () => { var s = Schedule(); Require(s.IsDue(105)); s.RecordApplied(105.5); Require(!s.IsDue(106) && s.NextAttempt == 110.5); });
            Check("Deferred capture retries with backoff without latching write fault", () => { var s = Schedule(); s.RecordDeferred(105); Require(s.NextAttempt == 106 && !s.IsFaulted && !s.IsDue(105.9) && s.IsDue(106)); });
            Check("Write failure latches and future time does not auto-retry", () => { var s = Schedule(); s.RecordFailure(); Require(s.IsFaulted && !s.IsDue(1000)); });
            Check("RecordApplied cannot clear an existing write fault", () => { var s = Schedule(); s.RecordFailure(); s.RecordApplied(105); Require(s.IsFaulted && !s.IsDue(106)); });
            Check("Only explicit retry unlatches a normal write fault", () => { var s = Schedule(); s.RecordFailure(); Require(s.RetryExplicitly(106) && !s.IsFaulted && s.IsDue(106)); });
            Check("Monotonic clock regression faults schedule", () => { var s = Schedule(); Require(!s.IsDue(99) && s.IsFaulted && !s.IsDue(105)); });
            Check("Explicit retry cannot use a regressed clock", () => { var s = Schedule(); s.IsDue(105); Require(!s.RetryExplicitly(104) && s.IsFaulted); });
            Check("NaN/infinite time rejected", () => { foreach (double time in new[] { -1d, double.NaN, double.PositiveInfinity }) { Throws(() => new GlobalCheckpointSchedule(Timing(), time)); var s = Schedule(); Require(!s.IsDue(time) && s.IsFaulted); } });
            Check("Deferred capture cannot clear a fault", () => { var s = Schedule(); s.RecordFailure(); s.RecordDeferred(105); Require(s.IsFaulted); });
            Check("Checkpoint success means only Applied transaction", () => { foreach (CheckpointTransactionStatus status in Enum.GetValues(typeof(CheckpointTransactionStatus))) Require(GlobalSessionPersistencePolicy.IsSuccessfulCommit(status) == (status == CheckpointTransactionStatus.Applied)); });
            Check("Normal stop requires Applied or no live players", () => { foreach (GlobalSessionSaveStatus status in Enum.GetValues(typeof(GlobalSessionSaveStatus))) Require(GlobalSessionPersistencePolicy.MayStop(status, false) == (status == GlobalSessionSaveStatus.Applied || status == GlobalSessionSaveStatus.NoLivePlayers)); });
            Check("Explicit abandonment is distinct and permits stopping after failure", () => { Require(GlobalSessionPersistencePolicy.MayStop(GlobalSessionSaveStatus.Blocked, true)); Require(GlobalSessionPersistencePolicy.MayStop(GlobalSessionSaveStatus.Abandoned, true)); Require(!GlobalSessionPersistencePolicy.MayStop(GlobalSessionSaveStatus.Abandoned, false)); });
            Check("No fake final-save success on Deferred or initial None", () => { Require(!GlobalSessionPersistencePolicy.MayStop(GlobalSessionSaveStatus.Deferred, false)); Require(!GlobalSessionPersistencePolicy.MayStop(GlobalSessionSaveStatus.None, false)); });
            Check("Scheduled real C/B memory checkpoint preserves exact global point", () => { var f = new Fixture(); Require(f.Schedule.IsDue(105)); var result = f.Save(200); Require(result == GlobalSessionSaveStatus.Applied && f.Store.Primary != null && f.Repository.Inspect().Snapshot.Players[0].Position == f.Source.Current.Entries[0].Sample.WorldPosition); });
            Check("Periodic checkpoint keeps existing offline records", () => { var f = new Fixture(); Applied(f.Repository.TryCommit(f.Repository.Inspect(), new[] { new GlobalPlayerPositionRecord("offline", GlobalPosition.Zero, false, "", 1) })); Require(f.Save(200) == GlobalSessionSaveStatus.Applied && f.Repository.Inspect().Snapshot.Players.Count == 2); });
            Check("Unready source defers capture with no staging", () => { var f = new Fixture(); f.Source.Ready = false; Require(f.Save(200) == GlobalSessionSaveStatus.Deferred && f.Store.Stages == 0 && !f.Schedule.IsFaulted && !GlobalSessionPersistencePolicy.MayStop(GlobalSessionSaveStatus.Deferred, false)); });
            Check("Temporary source readiness can recover on next explicit attempt", () => { var f = new Fixture(); f.Source.Ready = false; Require(f.Save(200) == GlobalSessionSaveStatus.Deferred); f.Source.Ready = true; Require(f.Save(201) == GlobalSessionSaveStatus.Applied); });
            Check("Future stored UTC is not silently clamped backward", () => { var f = new Fixture(); Applied(f.Repository.TryCommit(f.Repository.Inspect(), new[] { new GlobalPlayerPositionRecord("account-a", GlobalPosition.Zero, false, "", 300) })); Require(f.Save(200) == GlobalSessionSaveStatus.Deferred && f.Repository.Inspect().Snapshot.Players[0].SavedAtUnix == 300); });
            Check("Partial staging latches fault and blocks normal stop", () => { var f = new Fixture(); f.Store.FailStage = true; var result = f.Save(200); Require(result == GlobalSessionSaveStatus.Blocked && f.Schedule.IsFaulted && !GlobalSessionPersistencePolicy.MayStop(result, false) && f.Store.Pending != null); });
            Check("Native publication error is never assumed to be successful save", () => { var f = new Fixture(); f.Store.FailPublish = true; Require(f.Save(200) == GlobalSessionSaveStatus.Blocked && f.Schedule.IsFaulted && f.Store.Primary == null); });
            Check("Throw after verified publication remains Applied", () => { var f = new Fixture(); f.Store.ThrowAfterPublish = true; Require(f.Save(200) == GlobalSessionSaveStatus.Applied && !f.Schedule.IsFaulted && f.Store.Primary != null); });
            Check("Explicit retry alone cannot remove Pending", () => { var f = new Fixture(); f.Store.FailStage = true; Require(f.Save(200) == GlobalSessionSaveStatus.Blocked); f.Store.FailStage = false; Require(f.Schedule.RetryExplicitly(106)); Require(f.Save(201) == GlobalSessionSaveStatus.Blocked && f.Store.Pending != null); });
            Check("Explicit quarantine plus retry can recover a failed pending attempt", () => { var f = new Fixture(); f.Store.FailStage = true; Require(f.Save(200) == GlobalSessionSaveStatus.Blocked); f.Store.FailStage = false; Applied(f.Repository.TryQuarantinePending(f.Repository.Inspect())); Require(f.Schedule.RetryExplicitly(106)); Require(f.Save(201) == GlobalSessionSaveStatus.Applied); });
            Check("Store CAS conflict from an intervening write is not silently retried", () => { var f = new Fixture(); Require(f.Capture.TryPrepare(f.Repository.Inspect(), 200, 2, out var batch, out _)); Applied(f.Repository.TryCommit(f.Repository.Inspect(), new[] { new GlobalPlayerPositionRecord("offline", GlobalPosition.Zero, false, "", 1) })); var result = f.Capture.TryCommit(batch); Require(result.Status == CheckpointTransactionStatus.Conflict && !GlobalSessionPersistencePolicy.IsSuccessfulCommit(result.Status)); });
            Check("Source identity lost after staging retains evidence and blocks final stop", () => { var f = new Fixture(); f.Store.AfterStage = () => f.Source.Ready = false; Require(f.Save(200) == GlobalSessionSaveStatus.Blocked && f.Repository.Inspect().Status == CheckpointStoreStatus.Pending); });
            Check("Blocked/RecoveryAvailable store is not replaced with empty defaults", () => { var f = new Fixture(); f.Store.Primary = new byte[] { 3 }; Require(f.Save(200) == GlobalSessionSaveStatus.Blocked && f.Store.Stages == 0); });
            Check("Client role policy never requires fabricated player identity or server disk", () => Require(Start(GlobalMotionStartRole.Client, 0, false, CheckpointStoreStatus.Pending, false)));
            Check("Compiled NMC hooks preserve required global startup and explicit abandon API", () => { var type = typeof(ProjectC.Core.NetworkManagerController); Require(type.GetField("_globalSessionCoordinator", BindingFlags.Instance | BindingFlags.NonPublic) != null && type.GetMethod("DisconnectWithoutGlobalCheckpoint") != null && type.GetMethod("ShutdownForMainMenuWithoutGlobalCheckpoint") != null); });
            Check("Blocked main-menu exit has a distinct callback rather than false success", () => { var method = typeof(ProjectC.Core.NetworkManagerController).GetMethod("ShutdownForMainMenu"); Require(method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType == typeof(Action<string>)); Require(typeof(ProjectC.UI.EscMenu.EscMenuWindow).GetMethod("OnExitToMenuBlocked", BindingFlags.Instance | BindingFlags.NonPublic) != null); });
            Check("Concrete native session coordinator and D confirmed-player fence exist uninstantiated", () => { Require(typeof(MonoBehaviour).IsAssignableFrom(typeof(GlobalMotionSessionCoordinator))); Require(typeof(GlobalMotionSessionCoordinator).GetMethod("SubmitVerifiedPeer").GetParameters()[0].ParameterType == typeof(Guid)); Require(typeof(GlobalMotionCheckpointSpawnSource).GetMethod("TryGetConfirmedPlayer") != null && typeof(GlobalMotionPlayerBootstrap).GetMethod("UsesSource") != null); });
            Check("No session token or scheduling fields were added to persisted player data", () => { Require(typeof(GlobalPlayerPositionRecord).GetProperty("SessionToken") == null && typeof(GlobalPlayerPositionRecord).GetProperty("FrameId") == null && typeof(GlobalMotionSpawnSeed).GetField("SessionToken") == null); });
            return new Report { passed = ok.Count, failed = bad.Count, checks = ok.ToArray(), failures = bad.ToArray() };
        }
        private static readonly Func<GlobalMotionSnapshot, bool> Rules = _ => true;
        private static GlobalSessionTiming Timing() => new GlobalSessionTiming(5, 2, 30, 1);
        private static GlobalCheckpointSchedule Schedule() => new GlobalCheckpointSchedule(Timing(), 100);
        private static GlobalSessionPeerPlan Peer() => new GlobalSessionPeerPlan("account-a", 9, Quaternion.identity, Vector3.one, Rules, new GlobalPosition(56500.000000123, 2, 3));
        private static bool Start(GlobalMotionStartRole role, int legacy, bool repo, CheckpointStoreStatus status, bool host) => GlobalSessionPersistencePolicy.ValidateStart(role, legacy, repo, status, host, out _);
        private static void Applied(CheckpointTransactionResult r) => Require(r.Status == CheckpointTransactionStatus.Applied, r.Error);
        private sealed class Fixture
        {
            public readonly Source Source = new Source(); public readonly MemoryStorage Store = new MemoryStorage(); public readonly GlobalCheckpointSchedule Schedule = ValidateGlobalSessionOrchestration.Schedule();
            public readonly GlobalPlayerCheckpointRepository Repository; public readonly GlobalPlayerCheckpointCapture Capture;
            public Fixture() { Repository = new GlobalPlayerCheckpointRepository("world-a", Store); Capture = new GlobalPlayerCheckpointCapture(Source, Repository); }
            public GlobalSessionSaveStatus Save(long utc)
            {
                if (Schedule.IsFaulted) return GlobalSessionSaveStatus.Blocked;
                var o = Repository.Inspect(); if (o.Status != CheckpointStoreStatus.Ready && o.Status != CheckpointStoreStatus.Empty) { Schedule.RecordFailure(); return GlobalSessionSaveStatus.Blocked; }
                if (!Capture.TryPrepare(o, utc, 2, out var batch, out _)) { Schedule.RecordDeferred(106); return GlobalSessionSaveStatus.Deferred; }
                var result = Capture.TryCommit(batch); if (!GlobalSessionPersistencePolicy.IsSuccessfulCommit(result.Status)) { Schedule.RecordFailure(); return GlobalSessionSaveStatus.Blocked; }
                Schedule.RecordApplied(106); return GlobalSessionSaveStatus.Applied;
            }
        }
        private sealed class Source : IGlobalPlayerCheckpointSource
        {
            public bool Ready = true; public readonly GlobalPlayerCaptureSnapshot Current;
            public Source()
            {
                var b = new MotionStreamBinding { SessionId = 7, NetworkObjectId = 0, SpawnGeneration = 1, AuthorityGeneration = 1, DiscontinuityGeneration = 1, Space = MotionCoordinateSpace.World };
                var sample = GlobalMotionSnapshot.CreateWorld(b, 1, 100, new GlobalPosition(56500.000000123, 2, 3), Quaternion.identity, Vector3.one);
                Current = new GlobalPlayerCaptureSnapshot(Guid.NewGuid(), 7, 2, 100.5, new[] { new GlobalPlayerCaptureEntry("account-a", Guid.NewGuid(), 1, sample, GlobalMotionAuthority.Owner, 1, false) });
            }
            public bool TryCapture(double max, out GlobalPlayerCaptureSnapshot s, out string error) { s = Ready ? Current : null; error = Ready ? null : "fixture_source_not_ready"; return Ready; }
            public bool IsCurrent(GlobalPlayerCaptureSnapshot s, double max, out string error) { error = "fixture_identity_changed"; return Ready && ReferenceEquals(s, Current) && GlobalPlayerCapturePolicy.CanPublishCaptured(s, Current, max, out error); }
        }
        private sealed class MemoryStorage : IGlobalPlayerCheckpointStorage
        {
            public byte[] Primary, Backup, Pending; public int Stages; public bool FailStage, FailPublish, ThrowAfterPublish; public Action AfterStage;
            private bool _held; private readonly Dictionary<string, byte[]> _q = new Dictionary<string, byte[]>();
            public IDisposable AcquireLease() { if (_held) throw new IOException("lease_held"); _held = true; return new Release(this); }
            public byte[] Read(CheckpointSlot slot, int max) { Guard(); var b = slot == CheckpointSlot.Primary ? Primary : slot == CheckpointSlot.Backup ? Backup : Pending; if (b != null && b.Length > max) throw new IOException("oversized"); return Copy(b); }
            public void WritePendingNew(byte[] bytes) { Guard(); if (Pending != null) throw new IOException("pending_exists"); Stages++; Pending = FailStage ? new byte[] { 1 } : Copy(bytes); if (FailStage) throw new IOException("partial_stage"); AfterStage?.Invoke(); }
            public void PublishPending(bool replace, string q) { Guard(); if (FailPublish) throw new IOException("publish_failed"); if (Pending == null || (Primary != null) != replace) throw new IOException("invalid_publish"); if (replace) { if (q == null) Backup = Copy(Primary); else _q.Add(q, Copy(Primary)); } Primary = Pending; Pending = null; if (ThrowAfterPublish) throw new IOException("after_publish"); }
            public void QuarantinePending(string q) { Guard(); _q.Add(q, Copy(Pending)); Pending = null; }
            public byte[] ReadQuarantine(string q, int max) { Guard(); return _q.TryGetValue(q, out var b) ? Copy(b) : null; }
            private static byte[] Copy(byte[] b) => b == null ? null : (byte[])b.Clone();
            private void Guard() { if (!_held) throw new InvalidOperationException("lease_required"); }
            private sealed class Release : IDisposable { private MemoryStorage _s; public Release(MemoryStorage s) { _s = s; } public void Dispose() { if (_s != null) _s._held = false; _s = null; } }
        }
        private static void Throws(Action action) { bool thrown = false; try { action(); } catch (Exception) { thrown = true; } Require(thrown); }
        private static void Require(bool condition, string reason = null) { if (!condition) throw new InvalidOperationException(reason ?? "Assertion failed."); }
    }
}
