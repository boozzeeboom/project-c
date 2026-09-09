using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Persistence;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Fault-injected in-memory protocol model. Does not test native OS atomicity, power loss or actual user files.</summary>
    public static class ValidateGlobalCheckpointTransactions
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }
        [MenuItem("ProjectC/World/Floating Origin/Validate Checkpoint Transactions")]
        public static void Execute() { var r = Run(); if (r.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(r, true)); Debug.Log($"[T-FO05B] {r.passed} in-memory checks PASS; native storage UNTESTED."); }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var good = new List<string>(); var bad = new List<string>();
            void Check(string n, Action a) { try { a(); good.Add(n); } catch (Exception e) { bad.Add(n + ": " + e.GetType().Name + ": " + e.Message); } }
            Check("Frozen snapshot envelope v1 remains byte-compatible", () =>
            {
                const string golden = "{\"format\":\"projectc.global-player-snapshot\",\"schemaVersion\":1,\"storeId\":\"world-a\",\"revision\":1,\"commitId\":\"00000000000000000000000000000001\",\"parentCommitId\":\"\",\"records\":[],\"checksum\":\"e801262647ffe71a84b979c95d84d5a8513308a90bcb78bf91f9fd761806e155\"}";
                var bytes = Encoding.UTF8.GetBytes(golden); Require(Equal(bytes, Encode(Snapshot(Array.Empty<GlobalPlayerPositionRecord>()))) && Decode(bytes).Players.Count == 0);
            });
            Check("Snapshot codec preserves nested double checkpoint", () => { var p = P("a", 56500.000000123); Require(Decode(Encode(Snapshot(new[] { p }))).Players[0].Position == p.Position); });
            Check("Snapshot ordering is ordinal deterministic", () => Require(Equal(Encode(Snapshot(new[] { P("b"), P("a") })), Encode(Snapshot(new[] { P("a"), P("b") })))));
            Check("Duplicate player identity rejected", () => Throws(() => Snapshot(new[] { P("a"), P("a") })));
            Check("Null player and null snapshot collection rejected", () => { Throws(() => Snapshot(new GlobalPlayerPositionRecord[] { null })); Throws(() => Snapshot(null)); });
            Check("Invalid scope and revision rejected", () => { Throws(() => new GlobalPlayerCheckpointSnapshot("", 1, Id(1), "", Array.Empty<GlobalPlayerPositionRecord>())); Throws(() => new GlobalPlayerCheckpointSnapshot("world-a", 0, Id(1), "", Array.Empty<GlobalPlayerPositionRecord>())); });
            Check("Commit identity and lineage are explicit", () => { Throws(() => new GlobalPlayerCheckpointSnapshot("world-a", 1, new string('0', 32), "", Array.Empty<GlobalPlayerPositionRecord>())); Throws(() => new GlobalPlayerCheckpointSnapshot("world-a", 2, Id(1), Id(1), Array.Empty<GlobalPlayerPositionRecord>())); Throws(() => new GlobalPlayerCheckpointSnapshot("world-a", 2, Id(1), "", Array.Empty<GlobalPlayerPositionRecord>())); });
            Check("Wrong store cannot decode another store snapshot", () => Require(!GlobalPlayerCheckpointSnapshotCodec.TryDecode(Encode(Snapshot(new[] { P() })), "world-b", out var s, out _) && s == null));
            Check("Checksum catches revision or coordinate tampering", () => BadSnapshot(Replace(Encode(Snapshot(new[] { P() })), "\"revision\":1", "\"revision\":2")));
            Check("Future snapshot version is not interpreted as legacy", () => { var bytes = Replace(Encode(Snapshot(new[] { P() })), "\"schemaVersion\":1", "\"schemaVersion\":2"); Require(!GlobalPlayerCheckpointSnapshotCodec.TryDecode(bytes, "world-a", out _, out var e) && e == GlobalPlayerCheckpointSnapshotCodec.Unsupported); });
            Check("Unknown and duplicate envelope fields rejected", () => { var bytes = Encode(Snapshot(new[] { P() })); BadSnapshot(Insert(bytes, "\"extra\":1,")); BadSnapshot(Insert(bytes, "\"revision\":1,")); });
            Check("Missing records never become empty saved snapshot", () => BadSnapshot(Encoding.UTF8.GetBytes("{\"format\":\"projectc.global-player-snapshot\",\"schemaVersion\":1,\"storeId\":\"world-a\"}")));
            Check("Oversized snapshot and invalid UTF8 rejected", () => { BadSnapshot(new byte[GlobalPlayerCheckpointSnapshotCodec.MaxBytes + 1]); BadSnapshot(new byte[] { 0xff, 0xfe }); });
            Check("Deep/trailing JSON rejected", () => { BadSnapshot(Encoding.UTF8.GetBytes("{\"x\":[[[[0]]]]}")); BadSnapshot(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Encode(Snapshot(new[] { P() }))) + "{}")); });
            Check("Snapshot collection is immutable and input mutation isolated", () => { var list = new List<GlobalPlayerPositionRecord> { P() }; var s = Snapshot(list); list.Clear(); Require(s.Players.Count == 1); Throws(() => ((IList<GlobalPlayerPositionRecord>)s.Players).Clear()); });
            Check("Empty disk state differs from explicitly saved empty snapshot", () => { var c = new Case(); Require(c.R.Inspect().Status == CheckpointStoreStatus.Empty); Applied(c.R.TryCommit(c.R.Inspect(), Array.Empty<GlobalPlayerPositionRecord>())); var o = c.R.Inspect(); Require(o.Status == CheckpointStoreStatus.Ready && o.Snapshot.Players.Count == 0); });
            Check("First publication uses move and no fabricated backup", () => { var c = Ready(1); Require(c.S.LastReplace == false && c.S.Raw(CheckpointSlot.Backup) == null && c.R.Inspect().Snapshot.Revision == 1); });
            Check("Second publication preserves previous bytes as backup", () => { var c = Ready(1); var before = c.S.Raw(CheckpointSlot.Primary); Save(c, P(x: 2, time: 2)); Require(Equal(before, c.S.Raw(CheckpointSlot.Backup)) && c.S.LastReplace); });
            Check("Third publication rotates backup to immediate predecessor", () => { var c = Ready(2); var before = c.S.Raw(CheckpointSlot.Primary); Save(c, P(x: 3, time: 3)); var o = c.R.Inspect(); Require(o.Snapshot.Revision == 3 && Equal(before, c.S.Raw(CheckpointSlot.Backup)) && o.Snapshot.ParentCommitId == Decode(before).CommitId); });
            Check("Foreign repository observation rejected without write", () => { var c = new Case(); var other = new GlobalPlayerCheckpointRepository("world-a", c.S); Require(c.R.TryCommit(other.Inspect(), new[] { P() }).Status == CheckpointTransactionStatus.Conflict && c.S.Stages == 0); });
            Check("Stale head observation cannot overwrite newer snapshot", () => { var c = Ready(1); var old = c.R.Inspect(); Save(c, P(time: 2)); Require(c.R.TryCommit(old, new[] { P(time: 3) }).Status == CheckpointTransactionStatus.Conflict); });
            Check("Cooperative second writer conflicts with stale observation", () => { var c = Ready(1); var second = new GlobalPlayerCheckpointRepository("world-a", c.S); var old = second.Inspect(); Save(c, P(time: 2)); Require(second.TryCommit(old, new[] { P(time: 3) }).Status == CheckpointTransactionStatus.Conflict); });
            Check("Null observation rejected", () => { var c = new Case(); Require(c.R.TryCommit(null, new[] { P() }).Status == CheckpointTransactionStatus.Conflict && c.S.Stages == 0); });
            Check("No implicit removal of offline player checkpoints", () => { var c = new Case(); Save(c, P("a"), P("b")); var before = c.S.Raw(CheckpointSlot.Primary); Require(c.R.TryCommit(c.R.Inspect(), new[] { P("a") }).Status == CheckpointTransactionStatus.NotApplied && Equal(before, c.S.Raw(CheckpointSlot.Primary))); });
            Check("Explicit removal authorization permits intentional removal", () => { var c = new Case(); Save(c, P("a"), P("b")); Applied(c.R.TryCommit(c.R.Inspect(), new[] { P("a") }, true)); Require(c.R.Inspect().Snapshot.Players.Count == 1); });
            Check("Older checkpoint cannot replace newer player state", () => { var c = Ready(2); int stages = c.S.Stages; Require(c.R.TryCommit(c.R.Inspect(), new[] { P(time: 1) }).Status == CheckpointTransactionStatus.NotApplied && c.S.Stages == stages); });
            Check("Same timestamp second can contain a newer position", () => { var c = Ready(1); Save(c, P(x: 9, time: 1)); Require(c.R.Inspect().Snapshot.Players[0].Position.X == 9); });
            Check("Empty collection cannot silently clear existing players", () => { var c = Ready(1); Require(c.R.TryCommit(c.R.Inspect(), Array.Empty<GlobalPlayerPositionRecord>()).Status == CheckpointTransactionStatus.NotApplied); });
            Check("Invalid collection fails before creating pending file", () => { var c = new Case(); Require(c.R.TryCommit(c.R.Inspect(), null).Status == CheckpointTransactionStatus.NotApplied && c.S.Stages == 0); });
            Check("Foreign primary blocks initialization and overwrite", () => { var c = new Case(); c.S.Inject(CheckpointSlot.Primary, Encode(new GlobalPlayerCheckpointSnapshot("foreign", 1, Id(1), "", new[] { P() }))); var o = c.R.Inspect(); Require(o.Status == CheckpointStoreStatus.Blocked && o.Snapshot == null && c.R.TryCommit(o, new[] { P() }).Status == CheckpointTransactionStatus.NotApplied); });
            Check("Future primary cannot be downgraded to known backup", () => { var c = Ready(2); c.S.Inject(CheckpointSlot.Primary, Replace(c.S.Raw(CheckpointSlot.Primary), "\"schemaVersion\":1", "\"schemaVersion\":2")); var o = c.R.Inspect(); Require(o.Status == CheckpointStoreStatus.Blocked && o.RecoveryCandidate == null && c.R.TryRecoverBackup(o).Status == CheckpointTransactionStatus.NotApplied); });
            Check("Legacy file is never treated as an empty global store", () => { var c = new Case(); c.S.Inject(CheckpointSlot.Primary, Encoding.UTF8.GetBytes("{\"ships\":[],\"players\":[]}")); Require(c.R.Inspect().Status == CheckpointStoreStatus.Blocked); });
            Check("Corrupt primary exposes only explicit recovery candidate", () => { var c = Ready(2); c.S.Inject(CheckpointSlot.Primary, new byte[] { 0 }); var o = c.R.Inspect(); Require(o.Status == CheckpointStoreStatus.RecoveryAvailable && o.Snapshot == null && o.RecoveryCandidate.Revision == 1 && c.S.Stages == 2); });
            Check("Missing primary with backup does not become Empty", () => { var c = Ready(2); c.S.Inject(CheckpointSlot.Primary, null); Require(c.R.Inspect().Status == CheckpointStoreStatus.RecoveryAvailable); });
            Check("Corrupt backup blocks writes even with readable primary", () => { var c = Ready(2); c.S.Inject(CheckpointSlot.Backup, new byte[] { 0 }); Require(c.R.Inspect().Status == CheckpointStoreStatus.Blocked); });
            Check("Mismatched valid backup lineage blocks writes", () => { var c = Ready(2); c.S.Inject(CheckpointSlot.Backup, Encode(Snapshot(new[] { P() }))); Require(c.R.Inspect().Status == CheckpointStoreStatus.Blocked); });
            Check("Revision greater than one requires retained backup", () => { var c = Ready(2); c.S.Inject(CheckpointSlot.Backup, null); Require(c.R.Inspect().Status == CheckpointStoreStatus.Blocked); });
            Check("Valid pending is not automatically promoted or exposed", () => { var c = Ready(1); var before = c.S.Raw(CheckpointSlot.Primary); c.S.Inject(CheckpointSlot.Pending, before); var o = c.R.Inspect(); Require(o.Status == CheckpointStoreStatus.Pending && o.Snapshot == null && Equal(before, c.S.Raw(CheckpointSlot.Primary))); });
            Check("Pending blocks normal commit", () => { var c = Ready(1); c.S.Inject(CheckpointSlot.Pending, new byte[] { 1, 2 }); Require(c.R.TryCommit(c.R.Inspect(), new[] { P(time: 2) }).Status == CheckpointTransactionStatus.NotApplied); });
            Check("Partial pending is quarantined without changing primary/backup", () => { var c = Ready(2); var head = c.S.Raw(CheckpointSlot.Primary); var backup = c.S.Raw(CheckpointSlot.Backup); var partial = new byte[] { 1, 2, 0, 255 }; c.S.Inject(CheckpointSlot.Pending, partial); var r = c.R.TryQuarantinePending(c.R.Inspect()); Applied(r); Require(Equal(c.S.Quarantined(r.QuarantineId), partial) && Equal(head, c.S.Raw(CheckpointSlot.Primary)) && Equal(backup, c.S.Raw(CheckpointSlot.Backup))); });
            Check("Quarantine stale observation conflicts", () => { var c = Ready(1); c.S.Inject(CheckpointSlot.Pending, new byte[] { 1 }); var old = c.R.Inspect(); c.S.Inject(CheckpointSlot.Pending, new byte[] { 2 }); Require(c.R.TryQuarantinePending(old).Status == CheckpointTransactionStatus.Conflict); });
            Check("No pending cleanup is a no-op, not a hidden write", () => { var c = Ready(1); Require(c.R.TryQuarantinePending(c.R.Inspect()).Status == CheckpointTransactionStatus.NotApplied && c.S.QuarantineCalls == 0); });
            Check("After quarantine a fresh observation is required", () => { var c = Ready(1); c.S.Inject(CheckpointSlot.Pending, new byte[] { 1 }); var old = c.R.Inspect(); Applied(c.R.TryQuarantinePending(old)); Require(c.R.TryCommit(old, new[] { P(time: 2) }).Status == CheckpointTransactionStatus.Conflict); Save(c, P(time: 2)); });
            Check("Recovery preserves corrupt primary bytes and known-good backup", () => { var c = Ready(2); var backup = c.S.Raw(CheckpointSlot.Backup); var broken = new byte[] { 0, 1, 2 }; c.S.Inject(CheckpointSlot.Primary, broken); var r = c.R.TryRecoverBackup(c.R.Inspect()); Applied(r); Require(Equal(backup, c.S.Raw(CheckpointSlot.Backup)) && Equal(broken, c.S.Quarantined(r.QuarantineId)) && r.Observation.Snapshot.ParentCommitId == Decode(backup).CommitId); });
            Check("Recovery from missing primary keeps backup untouched", () => { var c = Ready(2); var backup = c.S.Raw(CheckpointSlot.Backup); c.S.Inject(CheckpointSlot.Primary, null); var r = c.R.TryRecoverBackup(c.R.Inspect()); Applied(r); Require(r.QuarantineId == null && Equal(backup, c.S.Raw(CheckpointSlot.Backup))); });
            Check("Healthy primary cannot be rolled back by recovery API", () => { var c = Ready(2); Require(c.R.TryRecoverBackup(c.R.Inspect()).Status == CheckpointTransactionStatus.NotApplied); });
            Check("Failed recovery never rotates corruption into good backup", () => { var c = Ready(2); var backup = c.S.Raw(CheckpointSlot.Backup); c.S.Inject(CheckpointSlot.Primary, new byte[] { 0 }); var o = c.R.Inspect(); c.S.Fault = Fault.StagePartial; Require(c.R.TryRecoverBackup(o).Status == CheckpointTransactionStatus.NotApplied && Equal(backup, c.S.Raw(CheckpointSlot.Backup))); });
            Check("Recovery mints fresh commit preventing same-revision ABA", () => { var c = new Case(); Save(c, P()); Save(c, P()); var old = c.R.Inspect(); c.S.Inject(CheckpointSlot.Primary, new byte[] { 0 }); var recovered = c.R.TryRecoverBackup(c.R.Inspect()); Applied(recovered); Require(old.Snapshot.Revision == recovered.Observation.Snapshot.Revision && old.Snapshot.CommitId != recovered.Observation.Snapshot.CommitId && c.R.TryCommit(old, new[] { P() }).Status == CheckpointTransactionStatus.Conflict); });
            Check("Failure before staging leaves old snapshot intact", () => Failure(Fault.StageBefore, CheckpointTransactionStatus.NotApplied, false));
            Check("Partial staging retains pending and leaves old snapshot intact", () => Failure(Fault.StagePartial, CheckpointTransactionStatus.NotApplied, true));
            Check("Read-back catches corrupt staged bytes before publication", () => { var c = Ready(1); var o = c.R.Inspect(); int publications = c.S.Publications; c.S.Fault = Fault.StageCorrupt; Require(c.R.TryCommit(o, new[] { P(time: 2) }).Status == CheckpointTransactionStatus.NotApplied && c.S.Publications == publications); });
            Check("Failure before native replace retains old data and pending", () => Failure(Fault.PublishBefore, CheckpointTransactionStatus.NotApplied, true));
            Check("Unsupported native replace has no destructive fallback", () => Failure(Fault.UnsupportedReplace, CheckpointTransactionStatus.NotApplied, true));
            Check("Throw after publication is verified as Applied", () => { var c = Ready(1); var o = c.R.Inspect(); c.S.Fault = Fault.PublishAfter; var r = c.R.TryCommit(o, new[] { P(time: 2) }); Applied(r); Require(r.Error != null && r.Observation.Snapshot.Revision == 2); });
            Check("Published head with broken backup reports recovery required", () => { var c = Ready(1); var o = c.R.Inspect(); c.S.Fault = Fault.BreakBackup; Require(c.R.TryCommit(o, new[] { P(time: 2) }).Status == CheckpointTransactionStatus.RecoveryRequired); });
            Check("Damaged published primary reports recovery required", () => { var c = Ready(1); var o = c.R.Inspect(); c.S.Fault = Fault.BreakPrimary; Require(c.R.TryCommit(o, new[] { P(time: 2) }).Status == CheckpointTransactionStatus.RecoveryRequired); });
            Check("Lost read access after publish is Indeterminate, never assumed rollback", () => { var c = Ready(1); var o = c.R.Inspect(); c.S.Fault = Fault.DenyReadAfterPublish; Require(c.R.TryCommit(o, new[] { P(time: 2) }).Status == CheckpointTransactionStatus.Indeterminate); c.S.ReadDenied = false; Require(c.R.Inspect().Snapshot.Revision == 2); Require(c.R.TryCommit(o, new[] { P(time: 3) }).Status == CheckpointTransactionStatus.Conflict); });
            Check("Read error is Blocked with unusable observation, not Empty", () => { var c = new Case(); c.S.ReadDenied = true; var o = c.R.Inspect(); Require(o.Status == CheckpointStoreStatus.Blocked && c.R.TryCommit(o, new[] { P() }).Status == CheckpointTransactionStatus.Conflict && c.S.Stages == 0); });
            Check("Lease denial blocks writer without touching payloads", () => { var c = Ready(1); var o = c.R.Inspect(); c.S.Fault = Fault.DenyLease; Require(c.R.TryCommit(o, new[] { P(time: 2) }).Status == CheckpointTransactionStatus.Unavailable); });
            Check("Reentrant writer cannot acquire already-owned storage lease", () => { var c = Ready(1); var o = c.R.Inspect(); CheckpointTransactionResult nested = null; c.S.AfterStage = () => nested = c.R.TryCommit(o, new[] { P(time: 5) }); Applied(c.R.TryCommit(o, new[] { P(time: 2) })); Require(nested.Status == CheckpointTransactionStatus.Unavailable); });
            Check("Quarantine failure before move retains exact pending bytes", () => QuarantineFault(Fault.QuarantineBefore, CheckpointTransactionStatus.NotApplied));
            Check("Quarantine exception after move is verified Applied", () => QuarantineFault(Fault.QuarantineAfter, CheckpointTransactionStatus.Applied));
            Check("Missing quarantine copy never reports successful cleanup", () => QuarantineFault(Fault.DropQuarantine, CheckpointTransactionStatus.RecoveryRequired));
            Check("External primary mutation after staging is detected", () => { var c = Ready(1); var o = c.R.Inspect(); c.S.AfterStage = () => c.S.Inject(CheckpointSlot.Primary, new byte[] { 0 }); Require(c.R.TryCommit(o, new[] { P(time: 2) }).Status == CheckpointTransactionStatus.RecoveryRequired); });
            Check("Snapshot revision exhaustion rejects before stage", () => { var c = new Case(); c.S.Inject(CheckpointSlot.Backup, Encode(new GlobalPlayerCheckpointSnapshot("world-a", long.MaxValue - 1, Id(2), Id(1), new[] { P() }))); c.S.Inject(CheckpointSlot.Primary, Encode(new GlobalPlayerCheckpointSnapshot("world-a", long.MaxValue, Id(3), Id(2), new[] { P() }))); Require(c.R.TryCommit(c.R.Inspect(), new[] { P(time: 2) }).Status == CheckpointTransactionStatus.NotApplied && c.S.Stages == 0); });
            Check("Memory storage rejects operations without lease", () => { var s = new MemoryStorage(); Throws(() => s.WritePendingNew(new byte[] { 1 })); Throws(() => s.Read(CheckpointSlot.Primary, 100)); });
            Check("Directory adapter rejects unspecified relative UNC or root paths without IO", () => { foreach (var path in new[] { "", ".", "relative", "\\\\server\\store", Path.GetPathRoot(Path.GetFullPath(".")) }) Throws(() => new DirectoryGlobalPlayerCheckpointStorage(path)); });
            Check("No Unity lifecycle or default user-save path needed", () => Require(!typeof(UnityEngine.Object).IsAssignableFrom(typeof(GlobalPlayerCheckpointRepository)) && !typeof(UnityEngine.Object).IsAssignableFrom(typeof(DirectoryGlobalPlayerCheckpointStorage))));
            return new Report { passed = good.Count, failed = bad.Count, checks = good.ToArray(), failures = bad.ToArray() };
        }
        private static string Id(int value) => value.ToString("x32");
        private static GlobalPlayerPositionRecord P(string id = "a", double x = 1, long time = 1) => new GlobalPlayerPositionRecord(id, new GlobalPosition(x, 2, 3), false, "", time);
        private static GlobalPlayerCheckpointSnapshot Snapshot(IReadOnlyList<GlobalPlayerPositionRecord> p) => new GlobalPlayerCheckpointSnapshot("world-a", 1, Id(1), "", p);
        private static byte[] Encode(GlobalPlayerCheckpointSnapshot s) { Require(GlobalPlayerCheckpointSnapshotCodec.TryEncode(s, out var b, out var e), e); return b; }
        private static GlobalPlayerCheckpointSnapshot Decode(byte[] b) { Require(GlobalPlayerCheckpointSnapshotCodec.TryDecode(b, "world-a", out var s, out var e), e); return s; }
        private static void BadSnapshot(byte[] b) { Require(!GlobalPlayerCheckpointSnapshotCodec.TryDecode(b, "world-a", out var s, out var e) && s == null && e != null); }
        private static byte[] Replace(byte[] b, string oldValue, string newValue) { string text = Encoding.UTF8.GetString(b), changed = text.Replace(oldValue, newValue); Require(changed != text); return Encoding.UTF8.GetBytes(changed); }
        private static byte[] Insert(byte[] b, string fields) => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Insert(1, fields));
        private static bool Equal(byte[] a, byte[] b) { if (a == null || b == null) return a == b; if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static void Applied(CheckpointTransactionResult r) => Require(r.Status == CheckpointTransactionStatus.Applied, r.Status + ": " + r.Error);
        private static void Save(Case c, params GlobalPlayerPositionRecord[] players) => Applied(c.R.TryCommit(c.R.Inspect(), players));
        private static Case Ready(int count) { var c = new Case(); for (int i = 1; i <= count; i++) Save(c, P(time: i, x: i)); return c; }
        private static void Failure(Fault fault, CheckpointTransactionStatus expected, bool pending)
        { var c = Ready(1); var head = c.S.Raw(CheckpointSlot.Primary); var o = c.R.Inspect(); c.S.Fault = fault; var r = c.R.TryCommit(o, new[] { P(time: 2) }); Require(r.Status == expected && Equal(head, c.S.Raw(CheckpointSlot.Primary)) && (c.S.Raw(CheckpointSlot.Pending) != null) == pending, r.Status + ": " + r.Error); }
        private static void QuarantineFault(Fault fault, CheckpointTransactionStatus expected)
        { var c = Ready(1); c.S.Inject(CheckpointSlot.Pending, new byte[] { 1, 2 }); var o = c.R.Inspect(); c.S.Fault = fault; Require(c.R.TryQuarantinePending(o).Status == expected); }
        private sealed class Case { public readonly MemoryStorage S = new MemoryStorage(); public readonly GlobalPlayerCheckpointRepository R; public Case() { R = new GlobalPlayerCheckpointRepository("world-a", S); } }
        private enum Fault { None, DenyLease, StageBefore, StagePartial, StageCorrupt, PublishBefore, UnsupportedReplace, PublishAfter, BreakBackup, BreakPrimary, DenyReadAfterPublish, QuarantineBefore, QuarantineAfter, DropQuarantine }
        private sealed class MemoryStorage : IGlobalPlayerCheckpointStorage
        {
            private readonly Dictionary<CheckpointSlot, byte[]> _files = new Dictionary<CheckpointSlot, byte[]>();
            private readonly Dictionary<string, byte[]> _quarantine = new Dictionary<string, byte[]>();
            private bool _held;
            public Fault Fault;
            public bool ReadDenied, LastReplace;
            public int Stages, Publications, QuarantineCalls;
            public Action AfterStage;
            public IDisposable AcquireLease() { if (_held || Fault == Fault.DenyLease) throw new IOException("lease_denied"); _held = true; return new Release(this); }
            public byte[] Read(CheckpointSlot slot, int maximumBytes) { Guard(); if (ReadDenied) throw new IOException("read_denied"); var b = Raw(slot); if (b != null && b.Length > maximumBytes) throw new IOException("size_limit"); return b; }
            public void WritePendingNew(byte[] b)
            {
                Guard(); Stages++; if (_files.ContainsKey(CheckpointSlot.Pending)) throw new IOException("pending_exists");
                if (Fault == Fault.StageBefore) throw new IOException("stage_before");
                if (Fault == Fault.StagePartial) { Inject(CheckpointSlot.Pending, new byte[] { 0, 1 }); throw new IOException("disk_full"); }
                Inject(CheckpointSlot.Pending, Fault == Fault.StageCorrupt ? new byte[] { 0 } : b); AfterStage?.Invoke();
            }
            public void PublishPending(bool replacePrimary, string quarantineId)
            {
                Guard(); Publications++; LastReplace = replacePrimary;
                if (Fault == Fault.PublishBefore) throw new IOException("before_publish");
                if (Fault == Fault.UnsupportedReplace) throw new PlatformNotSupportedException();
                if (!_files.ContainsKey(CheckpointSlot.Pending) || _files.ContainsKey(CheckpointSlot.Primary) != replacePrimary) throw new IOException("publication_precondition");
                if (replacePrimary) { if (quarantineId == null) Inject(CheckpointSlot.Backup, Raw(CheckpointSlot.Primary)); else _quarantine.Add(quarantineId, Raw(CheckpointSlot.Primary)); }
                Inject(CheckpointSlot.Primary, Raw(CheckpointSlot.Pending)); Inject(CheckpointSlot.Pending, null);
                if (Fault == Fault.BreakBackup) Inject(CheckpointSlot.Backup, new byte[] { 0 });
                if (Fault == Fault.BreakPrimary) Inject(CheckpointSlot.Primary, new byte[] { 0 });
                if (Fault == Fault.DenyReadAfterPublish) ReadDenied = true;
                if (Fault == Fault.PublishAfter) throw new IOException("ack_lost");
            }
            public void QuarantinePending(string id)
            {
                Guard(); QuarantineCalls++; if (Fault == Fault.QuarantineBefore) throw new IOException("before_quarantine");
                if (!_files.ContainsKey(CheckpointSlot.Pending)) throw new IOException("pending_missing");
                if (Fault != Fault.DropQuarantine) _quarantine.Add(id, Raw(CheckpointSlot.Pending));
                Inject(CheckpointSlot.Pending, null); if (Fault == Fault.QuarantineAfter) throw new IOException("after_quarantine");
            }
            public byte[] ReadQuarantine(string id, int maximumBytes) { Guard(); if (ReadDenied) throw new IOException("read_denied"); var b = Quarantined(id); if (b != null && b.Length > maximumBytes) throw new IOException("size_limit"); return b; }
            public byte[] Raw(CheckpointSlot slot) => _files.TryGetValue(slot, out var b) ? (byte[])b.Clone() : null;
            public byte[] Quarantined(string id) => id != null && _quarantine.TryGetValue(id, out var b) ? (byte[])b.Clone() : null;
            public void Inject(CheckpointSlot slot, byte[] b) { if (b == null) _files.Remove(slot); else _files[slot] = (byte[])b.Clone(); }
            private void Guard() { if (!_held) throw new InvalidOperationException("lease_required"); }
            private sealed class Release : IDisposable { private MemoryStorage _s; public Release(MemoryStorage s) { _s = s; } public void Dispose() { if (_s == null) return; _s._held = false; _s = null; } }
        }
        private static void Throws(Action a) { bool threw = false; try { a(); } catch (Exception) { threw = true; } Require(threw); }
        private static void Require(bool value, string error = null) { if (!value) throw new InvalidOperationException(error ?? "Assertion failed."); }
    }
}
