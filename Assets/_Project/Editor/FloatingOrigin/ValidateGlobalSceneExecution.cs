using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Pure policy/receipt/compiled-seam checks only. Does not exercise NativeExecutor or create Unity objects.</summary>
    public static class ValidateGlobalSceneExecution
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }
        [MenuItem("ProjectC/World/Floating Origin/Validate Native Scene Execution Contracts")]
        public static void Execute() { var r = Run(); if (r.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(r, true)); Debug.Log($"[T-FO04I] {r.passed} pure checks passed; native behavior untested."); }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var passed = new List<string>(); var failed = new List<string>();
            void Check(string name, Action action) { try { action(); passed.Add(name); } catch (Exception e) { failed.Add(name + ": " + e.GetType().Name + ": " + e.Message); } }
            Check("Staged static content accepted", () => Require(GlobalSceneExecutionPolicy.Validate(Static()) == null));
            Check("Inactive nonspatial network service accepted", () => Require(GlobalSceneExecutionPolicy.Validate(Service()) == null));
            Check("Active nonspatial preserved infrastructure need not be disabled", () => Require(GlobalSceneExecutionPolicy.Validate(new GlobalSceneExecutionFacts { Treatment = GlobalSceneTreatment.PreserveContent, ActiveSelf = true, ActiveInHierarchy = true }) == null));
            Check("Unknown treatment rejected", () => Require(GlobalSceneExecutionPolicy.Validate(default) != null));
            Check("Replacement requires future executor", () => { var f = Static(); f.Treatment = GlobalSceneTreatment.ReplaceWithNetworkPrefab; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Exclusion requires future executor", () => { var f = Static(); f.Treatment = GlobalSceneTreatment.Exclude; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Network identity cannot be hidden as content", () => { var f = Service(); f.Treatment = GlobalSceneTreatment.PreserveContent; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("SceneNetworkObject treatment needs actual NetworkObject", () => { var f = Service(); f.NetworkObject = false; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Spatial scene network actor blocked pending bridge", () => { var f = Service(); f.Spatial = true; f.FrameValid = true; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Unsupported native or writer facts reject static and service", () => { var a = Static(); a.UnsupportedNative = true; var b = Service(); b.UnsupportedNative = true; Require(GlobalSceneExecutionPolicy.Validate(a) != null && GlobalSceneExecutionPolicy.Validate(b) != null); });
            Check("Static content requires explicit valid frame", () => { var f = Static(); f.FrameValid = false; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Already active spatial source cannot be moved as preparation", () => { var f = Static(); f.ActiveInHierarchy = true; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Static source needs explicit activation intent", () => { var f = Static(); f.ActivateWhenReady = false; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Network source must be self inactive, not merely hidden under parent", () => { var f = Service(); f.ActiveSelf = true; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Active network hierarchy rejected", () => { var f = Service(); f.ActiveInHierarchy = true; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Network source requires explicit activation intent", () => { var f = Service(); f.ActivateWhenReady = false; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("Remote peers cannot join before scene spawn", () => Require(!GlobalSceneExecutionPolicy.CanAcceptPeer(false, false)));
            Check("Remote peer can join after scene spawn receipts", () => Require(GlobalSceneExecutionPolicy.CanAcceptPeer(false, true)));
            Check("Actual host local peer does not deadlock pre-Started approval", () => Require(GlobalSceneExecutionPolicy.CanAcceptPeer(true, false) && GlobalSceneExecutionPolicy.CanAcceptPeer(true, true)));
            Check("Prestart inactive unchanged placement can be restored", () => Require(GlobalSceneExecutionPolicy.CanRestorePreparation(false, true, true)));
            Check("No transform rollback after network started", () => Require(!GlobalSceneExecutionPolicy.CanRestorePreparation(true, true, true)));
            Check("No rollback of now-active source", () => Require(!GlobalSceneExecutionPolicy.CanRestorePreparation(false, false, true)));
            Check("No rollback against changed source identity", () => Require(!GlobalSceneExecutionPolicy.CanRestorePreparation(false, true, false)));
            Check("Retirement preflight does not consume receipt", () => { Fixture(out var l, out var ticket, out var root, out _); l.TryRecordLive(ticket, root, 0, new GlobalSceneInstanceLifetime(7, 0, 1), out var token, out _); Require(l.CanRecordRetired(token, out _) && l.CanRecordRetired(token, out _) && l.TryRecordRetired(token, out _) && !l.CanRecordRetired(token, out _)); });
            Check("Live child rejects parent preflight before native mutation", () => { Fixture(out var l, out var t, out var root, out var child); l.TryRecordLive(t, root, 0, new GlobalSceneInstanceLifetime(7, 1, 1), out var r, out _); l.TryRecordLive(t, child, 0, new GlobalSceneInstanceLifetime(7, 2, 1), out var c, out _); Require(!l.CanRecordRetired(r, out _) && l.CanRecordRetired(c, out _) && l.TryRecordRetired(c, out _) && l.CanRecordRetired(r, out _)); });
            Check("Preflight cannot retire default token", () => { Fixture(out var l, out _, out _, out _); Require(!l.CanRecordRetired(default, out _)); });
            Check("Baked marker is not a NetworkBehaviour or runtime GUID resolver", () => { Require(!typeof(NetworkBehaviour).IsAssignableFrom(typeof(GlobalSceneSourceMarker))); foreach (string field in new[] { "_sourceId", "_frameId", "_activateWhenReady" }) Require(typeof(GlobalSceneSourceMarker).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).IsDefined(typeof(SerializeField), false)); });
            Check("Executor exposes explicit prepared operation seams", () => { foreach (string name in new[] { "ValidatePreparation", "PrepareBeforeNetworkStart", "TryRetireScene", "Release" }) Require(typeof(GlobalSceneNativeExecutor).GetMethod(name) != null); });
            Check("Player bootstrap participates in native scene admission", () => Require(typeof(IGlobalMotionSceneAdmission).IsAssignableFrom(typeof(GlobalMotionPlayerBootstrap))));
            Check("Neither marker nor executor runs automatically in Edit Mode", () => { foreach (var t in new[] { typeof(GlobalSceneSourceMarker), typeof(GlobalSceneNativeExecutor) }) Require(!t.IsDefined(typeof(ExecuteAlways), true) && !t.IsDefined(typeof(ExecuteInEditMode), true)); });
            Check("Reviewed unmanaged source is explicit and untouched", () => { var f = new GlobalSceneExecutionFacts { Treatment = GlobalSceneTreatment.Unmanaged, Spatial = false, NetworkObject = false, ActiveSelf = true, ActiveInHierarchy = true, UnsupportedNative = true }; Require(GlobalSceneExecutionPolicy.Validate(f) == null); f.Spatial = true; Require(GlobalSceneExecutionPolicy.Validate(f) != null); });
            Check("I protocol rejects H and legacy is not reserved", () => { Require(GlobalMotionNetworkContract.ProtocolVersion == 0xF006 && !GlobalMotionNetworkContract.IsReservedProtocolVersion(0)); var d = new byte[32]; d[0] = 1; var current = GlobalMotionNetworkContract.CreateHello(d, d); var previous = (byte[])current.Clone(); previous[6] = 5; previous[7] = 0xf0; Require(!GlobalMotionNetworkContract.ValidateHello(previous, current, out _)); });
            return new Report { passed = passed.Count, failed = failed.Count, checks = passed.ToArray(), failures = failed.ToArray() };
        }
        private static GlobalSceneExecutionFacts Static() => new GlobalSceneExecutionFacts { Treatment = GlobalSceneTreatment.PreserveContent, Spatial = true, ActivateWhenReady = true, FrameValid = true };
        private static GlobalSceneExecutionFacts Service() => new GlobalSceneExecutionFacts { Treatment = GlobalSceneTreatment.SceneNetworkObject, NetworkObject = true, ActivateWhenReady = true };
        private static void Fixture(out GlobalSceneLifecycleLedger ledger, out GlobalSceneLoadTicket ticket, out string root, out string child)
        {
            const string guid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"; root = guid + ":1:0"; child = guid + ":2:0";
            var data = new GlobalSceneCatalogData { expectedSceneGuids = new[] { guid }, scenes = new[] { new GlobalSceneSource { sceneGuid = guid, assetPath = "Assets/Fixture.unity", saved = true, inspectedComplete = true, dependencyHash = new string('b', 32), observations = new[] {
                new GlobalSceneObservation { sourceId = root, isRoot = true, isNetworkObject = true, layoutHash = new string('c', 64) },
                new GlobalSceneObservation { sourceId = child, parentSourceId = root, isNetworkObject = true, layoutHash = new string('c', 64) } } } }, entries = new[] {
                new GlobalSceneEntry { sceneGuid = guid, sourceId = root, treatment = GlobalSceneTreatment.SceneNetworkObject, reviewNote = "pure fixture" },
                new GlobalSceneEntry { sceneGuid = guid, sourceId = child, parentSourceId = root, treatment = GlobalSceneTreatment.SceneNetworkObject, reviewNote = "pure fixture" } } };
            Require(GlobalSceneCatalogCompiler.TryCompile(data, out var plan, out _)); ledger = new GlobalSceneLifecycleLedger(plan, 7); Require(ledger.TryBeginLoad(guid, out ticket));
        }
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
    }
}
