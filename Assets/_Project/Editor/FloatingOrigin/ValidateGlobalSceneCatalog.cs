using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Pure authored-catalog and receipt-ledger tests; no GameObjects, scene loads, physics or networking.</summary>
    public static class ValidateGlobalSceneCatalog
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }
        private const string GuidA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Root = GuidA + ":1:0", Child = GuidA + ":2:0";
        [MenuItem("ProjectC/World/Floating Origin/Validate Scene Catalog Contracts")]
        public static void Execute() { var r = Run(); if (r.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(r, true)); Debug.Log($"[T-FO04H] {r.passed} pure checks passed; native lifecycle untested."); }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var ok = new List<string>(); var bad = new List<string>();
            void Check(string name, Action run) { try { run(); ok.Add(name); } catch (Exception e) { bad.Add(name + ": " + e.GetType().Name + ": " + e.Message); } }
            Check("Complete declared fixture compiles", () => Require(Plan().SpawnOrder.Count == 2));
            Check("Null and empty catalogs fail", () => { Require(!GlobalSceneCatalogCompiler.TryCompile(null, out _, out _)); Require(!GlobalSceneCatalogCompiler.TryCompile(new GlobalSceneCatalogData(), out _, out _)); });
            Check("Unknown schema fails", () => Refused(d => d.schemaVersion++));
            Check("Missing expected scene fails", () => Refused(d => d.scenes = Array.Empty<GlobalSceneSource>()));
            Check("Duplicate expected GUID fails", () => Refused(d => d.expectedSceneGuids = new[] { GuidA, GuidA }));
            Check("Malformed and uppercase GUID rejected", () => { Require(!GlobalSceneCatalogCompiler.IsHex(GuidA.ToUpperInvariant(), 32)); Require(!GlobalSceneCatalogCompiler.IsHex(new string('0', 32), 32)); });
            Check("Uninspected scene fails closed", () => Refused(d => d.scenes[0].inspectedComplete = false));
            Check("Dirty scene fails closed", () => Refused(d => d.scenes[0].saved = false));
            Check("Missing dependency stamp fails", () => Refused(d => d.scenes[0].dependencyHash = ""));
            Check("Unsafe scene path rejected", () => Refused(d => d.scenes[0].assetPath = "Assets/../Outside.unity"));
            Check("Unreviewed decision fails", () => Refused(d => d.entries[0].treatment = GlobalSceneTreatment.Unreviewed));
            Check("Missing review note fails", () => Refused(d => d.entries[0].reviewNote = " "));
            Check("Missing observed entry fails", () => Refused(d => d.entries = new[] { d.entries[0] }));
            Check("Extra unknown entry fails", () => Refused(d => { var list = new List<GlobalSceneEntry>(d.entries); list.Add(new GlobalSceneEntry()); d.entries = list.ToArray(); }));
            Check("Duplicate observation fails", () => Refused(d => d.scenes[0].observations[1] = d.scenes[0].observations[0]));
            Check("Duplicate review entry fails", () => Refused(d => d.entries[1] = d.entries[0]));
            Check("Missing structure hash fails", () => Refused(d => d.scenes[0].observations[0].layoutHash = ""));
            Check("Stable source IDs require scene GUID and canonical nonzero authored ID", () => { Require(GlobalSceneCatalogCompiler.IsSourceId(Root, GuidA)); Require(!GlobalSceneCatalogCompiler.IsSourceId(GuidA + ":01:0", GuidA)); Require(!GlobalSceneCatalogCompiler.IsSourceId(GuidA + ":0:0", GuidA)); Require(!GlobalSceneCatalogCompiler.IsSourceId("1", GuidA)); });
            Check("Preserving an observed NetworkObject as ordinary content fails", () => Refused(d => d.entries[0].treatment = GlobalSceneTreatment.PreserveContent));
            Check("Claiming scene NetworkObject on ordinary root fails", () => Refused(d => d.scenes[0].observations[0].isNetworkObject = false));
            Check("Replacement requires prefab hash", () => Refused(d => d.entries[0].treatment = GlobalSceneTreatment.ReplaceWithNetworkPrefab));
            Check("Nonreplacement cannot carry prefab hash", () => Refused(d => d.entries[0].replacementPrefabHash = 44));
            Check("Authored initial parent cannot silently be changed", () => Refused(d => d.entries[1].parentSourceId = ""));
            Check("Missing observed parent fails", () => Refused(d => { d.scenes[0].observations[1].parentSourceId = GuidA + ":99:0"; d.entries[1].parentSourceId = GuidA + ":99:0"; }));
            Check("Cycle fails iteratively", () => Refused(d => { d.scenes[0].observations[0].isRoot = false; d.scenes[0].observations[0].parentSourceId = Child; d.entries[0].parentSourceId = Child; d.entries[0].poseKind = GlobalScenePoseKind.ParentLocal; d.entries[0].worldPosition = GlobalPosition.Zero; }));
            Check("Parent-local requires network parent", () => Refused(d => { d.scenes[0].observations[0].isNetworkObject = false; d.entries[0].treatment = GlobalSceneTreatment.PreserveContent; }));
            Check("Excluded ancestor cannot leave active authored descendants", () => Refused(d => Exclude(d.entries[0])));
            Check("Replacing root requires explicit old-descendant exclusion", () => Refused(d => { d.entries[0].treatment = GlobalSceneTreatment.ReplaceWithNetworkPrefab; d.entries[0].replacementPrefabHash = 44; }));
            Check("World under network parent is not implicit parenting", () => Refused(d => { d.entries[1].poseKind = GlobalScenePoseKind.World; d.entries[1].parentLocalPosition = Vector3.zero; }));
            Check("Nonfinite position and scale fail", () => { Refused(d => d.entries[0].worldPosition = new GlobalPosition { X = double.NaN }); Refused(d => d.entries[0].scale = Vector3.zero); });
            Check("Unmarked spatial pose fails", () => Refused(d => d.entries[0].poseKind = GlobalScenePoseKind.None));
            Check("Nonspatial data cannot hide a world coordinate", () => Refused(d => d.entries[0].spatial = false));
            Check("Parent before child and retirement reverse", () => { var p = Plan(); Require(p.SpawnOrder[0].SourceId == Root && p.SpawnOrder[1].SourceId == Child && p.RetireOrder[0].SourceId == Child); });
            Check("Array order does not affect digest", () => { var a = Data(); var b = Data(); Array.Reverse(b.entries); Array.Reverse(b.scenes[0].observations); Require(Hex(Compile(a).CopyDigest()) == Hex(Compile(b).CopyDigest())); });
            Check("Digest is culture independent", () => { string original = Hex(Plan().CopyDigest()); var previous = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); Require(Hex(Plan().CopyDigest()) == original); } finally { CultureInfo.CurrentCulture = previous; } });
            Check("Pose review stamp and layout affect digest", () => { var baseline = Hex(Plan().CopyDigest()); foreach (Action<GlobalSceneCatalogData> edit in new Action<GlobalSceneCatalogData>[] { d => d.entries[0].worldPosition = new GlobalPosition(56501, 10, 0), d => d.entries[0].reviewNote += " reviewed", d => d.scenes[0].dependencyHash = new string('e', 32), d => d.scenes[0].observations[0].layoutHash = new string('e', 64) }) { var d = Data(); edit(d); Require(Hex(Compile(d).CopyDigest()) != baseline); } });
            Check("Compiled plan and digest are immutable snapshots", () => { var d = Data(); var p = Compile(d); var digest = Hex(p.CopyDigest()); d.entries[0].worldPosition = GlobalPosition.Zero; d.entries[0].parentSourceId = Child; var bytes = p.CopyDigest(); bytes[0] ^= 255; Require(p.SpawnOrder[0].WorldPosition.X == 56500 && p.SpawnOrder[0].ParentSourceId == "" && Hex(p.CopyDigest()) == digest); });
            Check("Declared digest must equal compiled catalog", () => { var p = Plan(); Require(GlobalSceneCatalogCompiler.TryMatchDigest(p, Hex(p.CopyDigest()), out _)); Require(!GlobalSceneCatalogCompiler.TryMatchDigest(p, new string('e', 64), out _)); });
            Check("Unknown scene load rejected", () => { var l = Ledger(); Require(!l.TryBeginLoad(new string('b', 32), out _)); });
            Check("Duplicate scene load rejected", () => { var l = Ledger(); Require(l.TryBeginLoad(GuidA, out _) && !l.TryBeginLoad(GuidA, out _)); });
            Check("Different ledger cannot accept old local tickets", () => { var a = Ledger(); var b = Ledger(); a.TryBeginLoad(GuidA, out var t); b.TryBeginLoad(GuidA, out _); Require(!b.IsCurrent(t)); });
            Check("Child registration awaits parent", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); Require(!l.TryRecordLive(t, Child, 1, Life(2), out _, out _)); });
            Check("Parent frame mismatch rejected", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); Live(l, t, Root, 1, Life(1)); Require(!l.TryRecordLive(t, Child, 2, Life(2), out _, out _)); });
            Check("Network identity must be real same-session lifetime", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); Require(!l.TryRecordLive(t, Root, 1, default, out _, out _)); Require(!l.TryRecordLive(t, Root, 1, new GlobalSceneInstanceLifetime(99, 1, 1), out _, out _)); });
            Check("Actual object ID zero is accepted", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); Live(l, t, Root, 1, Life(0)); });
            Check("Duplicate live network identity rejected", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); Live(l, t, Root, 1, Life(1)); Require(!l.TryRecordLive(t, Child, 1, Life(1), out _, out _)); });
            Check("Coverage requires every source", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); Require(!l.AllSourcesAccountedFor(t)); Live(l, t, Root, 1, Life(1)); Require(!l.AllSourcesAccountedFor(t)); Live(l, t, Child, 1, Life(2)); Require(l.AllSourcesAccountedFor(t)); });
            Check("Live child blocks parent retirement and unload", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); var r = Live(l, t, Root, 1, Life(1)); Live(l, t, Child, 1, Life(2)); Require(!l.TryRecordRetired(r, out _) && !l.TryEndLoad(t)); Require(l.GetRetireOrder(t)[0] == Child); });
            Check("Child-first cleanup permits unload and rejects duplicate completions", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); var r = Live(l, t, Root, 1, Life(1)); var c = Live(l, t, Child, 1, Life(2)); Require(l.TryRecordRetired(c, out _) && !l.TryRecordRetired(c, out _) && l.TryRecordRetired(r, out _) && l.TryEndLoad(t) && !l.IsCurrent(t)); });
            Check("Unseen source blocks silent end-load", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); Require(!l.TryEndLoad(t)); });
            Check("Reused lifetime rejected, fresh generation rejects stale retirement token", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); var old = Live(l, t, Root, 1, Life(1)); Require(l.TryRecordRetired(old, out _)); Require(!l.TryRecordLive(t, Root, 1, Life(1), out _, out _)); Live(l, t, Root, 1, Life(1, 2)); Require(!l.TryRecordRetired(old, out _)); });
            Check("New load generation rejects old callbacks", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var old); var r = Live(l, old, Root, 1, Life(1)); var c = Live(l, old, Child, 1, Life(2)); l.TryRecordRetired(c, out _); l.TryRecordRetired(r, out _); Require(l.TryEndLoad(old)); l.TryBeginLoad(GuidA, out var fresh); Require(fresh.LoadGeneration != old.LoadGeneration && !l.TryRecordLive(old, Root, 1, Life(3), out _, out _)); });
            Check("Fault blocks admission and coverage but permits cleanup", () => { var l = Ledger(); l.TryBeginLoad(GuidA, out var t); var r = Live(l, t, Root, 1, Life(1)); Require(l.MarkFaulted(t)); Require(!l.TryRecordLive(t, Child, 1, Life(2), out _, out _) && !l.AllSourcesAccountedFor(t) && l.TryRecordRetired(r, out _)); });
            Check("Excluded descendants are explicitly acknowledged child-first", () => { var d = Data(); foreach (var e in d.entries) Exclude(e); var l = new GlobalSceneLifecycleLedger(Compile(d), 7); l.TryBeginLoad(GuidA, out var t); Require(!l.TryRecordExclusion(t, Root)); Require(l.TryRecordExclusion(t, Child) && l.TryRecordExclusion(t, Root) && l.AllSourcesAccountedFor(t) && l.TryEndLoad(t)); });
            Check("Ordinary content also has stale-retirement protection", () => { var d = Data(); d.entries = new[] { d.entries[0] }; d.scenes[0].observations = new[] { d.scenes[0].observations[0] }; d.scenes[0].observations[0].isNetworkObject = false; d.entries[0].treatment = GlobalSceneTreatment.PreserveContent; var l = new GlobalSceneLifecycleLedger(Compile(d), 7); l.TryBeginLoad(GuidA, out var t); var old = Live(l, t, Root, 1, default); l.TryRecordRetired(old, out _); Live(l, t, Root, 1, default); Require(!l.TryRecordRetired(old, out _)); });
            Check("H handshake rejects G asserted-hash protocol", () => { Require(GlobalMotionNetworkContract.ProtocolVersion == 0xF004); var digest = new byte[32]; digest[0] = 1; var current = GlobalMotionNetworkContract.CreateHello(digest, digest); var old = (byte[])current.Clone(); old[6] = 3; old[7] = 0xf0; Require(!GlobalMotionNetworkContract.ValidateHello(old, current, out _)); });
            return new Report { passed = ok.Count, failed = bad.Count, checks = ok.ToArray(), failures = bad.ToArray() };
        }
        private static GlobalSceneCatalogData Data()
        {
            return new GlobalSceneCatalogData { expectedSceneGuids = new[] { GuidA }, scenes = new[] { new GlobalSceneSource { sceneGuid = GuidA, assetPath = "Assets/Fixture.unity", dependencyHash = new string('c', 32), inspectedComplete = true, saved = true,
                observations = new[] { new GlobalSceneObservation { sourceId = Root, isRoot = true, isNetworkObject = true, layoutHash = new string('d', 64) }, new GlobalSceneObservation { sourceId = Child, parentSourceId = Root, isNetworkObject = true, layoutHash = new string('d', 64) } } } },
                entries = new[] { new GlobalSceneEntry { sceneGuid = GuidA, sourceId = Root, reviewNote = "pure fixture", treatment = GlobalSceneTreatment.SceneNetworkObject, spatial = true, poseKind = GlobalScenePoseKind.World, worldPosition = new GlobalPosition(56500, 10, 0) },
                    new GlobalSceneEntry { sceneGuid = GuidA, sourceId = Child, parentSourceId = Root, reviewNote = "pure fixture", treatment = GlobalSceneTreatment.SceneNetworkObject, spatial = true, poseKind = GlobalScenePoseKind.ParentLocal, parentLocalPosition = Vector3.one } } };
        }
        private static void Exclude(GlobalSceneEntry e) { e.treatment = GlobalSceneTreatment.Exclude; e.poseKind = GlobalScenePoseKind.None; e.worldPosition = GlobalPosition.Zero; e.parentLocalPosition = Vector3.zero; }
        private static GlobalScenePlan Compile(GlobalSceneCatalogData d) { if (!GlobalSceneCatalogCompiler.TryCompile(d, out var p, out var e)) throw new InvalidOperationException(e); return p; }
        private static GlobalScenePlan Plan() => Compile(Data());
        private static GlobalSceneLifecycleLedger Ledger() => new GlobalSceneLifecycleLedger(Plan(), 7);
        private static GlobalSceneInstanceLifetime Life(ulong id, ulong generation = 1) => new GlobalSceneInstanceLifetime(7, id, generation);
        private static GlobalSceneReceiptToken Live(GlobalSceneLifecycleLedger l, GlobalSceneLoadTicket t, string source, int frame, GlobalSceneInstanceLifetime life) { if (!l.TryRecordLive(t, source, frame, life, out var token, out var error)) throw new InvalidOperationException(error); return token; }
        private static void Refused(Action<GlobalSceneCatalogData> edit) { var d = Data(); edit(d); Require(!GlobalSceneCatalogCompiler.TryCompile(d, out _, out _)); }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
    }
}
