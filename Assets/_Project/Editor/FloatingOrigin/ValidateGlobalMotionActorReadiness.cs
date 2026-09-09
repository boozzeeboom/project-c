using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Pure actor-state and compiled-contract checks. No GameObject, scene, physics or Play Mode.</summary>
    public static class ValidateGlobalMotionActorReadiness
    {
        [Serializable]
        public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }

        [MenuItem("ProjectC/World/Floating Origin/Validate Actor Readiness Contracts")]
        public static void Execute()
        {
            var report = Run();
            if (report.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(report, true));
            Debug.Log($"[T-FO04D] {report.passed} pure actor-state/contract checks passed; native/gameplay behavior untested.");
        }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Requires stable Edit Mode.");
            var passed = new List<string>(); var failed = new List<string>();
            void Check(string name, Action action)
            { try { action(); passed.Add(name); } catch (Exception e) { failed.Add(name + ": " + e.GetType().Name + ": " + e.Message); } }

            Check("Legacy default permits existing movement without adapter", () =>
            {
                var s = new GlobalMotionActorState(); Require(!s.CoordinatesRequired);
                Require(s.AllowsSimulation(false, false)); Require(s.AllowsSimulation(false, true));
            });
            Check("Opt-in false does not depend on unused adapter readiness", () =>
            {
                var s = new GlobalMotionActorState(); s.ObserveRequirement(false);
                Require(s.AllowsSimulation(true, false)); Require(s.AllowsSimulation(true, true));
            });
            Check("Required mode blocks missing adapter even if a stale ready flag exists", () =>
            {
                var s = Required(); Require(!s.AllowsSimulation(false, false)); Require(!s.AllowsSimulation(false, true));
            });
            Check("Required mode blocks unbound/disabled/unready representation", () =>
            {
                var s = Required(); Require(!s.AllowsSimulation(true, false)); Require(s.AllowsSimulation(true, true));
            });
            Check("Mode latch cannot fall back to legacy after requirement is removed", () =>
            {
                var s = Required(); s.ObserveRequirement(false);
                Require(s.CoordinatesRequired && !s.AllowsSimulation(false, false));
            });
            Check("Native approval before a baseline is rejected", () =>
            {
                var s = Required(); Require(!s.ConfirmNativePrepared(Binding())); Require(!s.NativePreparedFor(Binding()));
            });
            Check("First baseline identifies a new lifetime and awaits native approval", () =>
            {
                var s = Required(); Require(s.RecordBaseline(Binding())); Require(s.AppliedBinding == Binding());
                Require(!s.NativePreparedFor(Binding()));
            });
            Check("Applied baseline cannot silently become legacy", () =>
            {
                var s = new GlobalMotionActorState(); s.RecordBaseline(Binding()); s.ObserveRequirement(false);
                Require(s.CoordinatesRequired);
            });
            Check("Exact binding can be explicitly approved", () =>
            {
                var s = Placed(); Require(s.ConfirmNativePrepared(Binding())); Require(s.NativePreparedFor(Binding()));
            });
            Check("Repeated exact baseline preserves existing approval", () =>
            {
                var s = Placed(); s.ConfirmNativePrepared(Binding()); Require(!s.RecordBaseline(Binding()));
                Require(s.NativePreparedFor(Binding()));
            });
            Check("Teleport discontinuity revokes native approval but does not reset home lifetime", () =>
            {
                var s = Approved(); var b = Binding(); b.DiscontinuityGeneration++;
                Require(!s.RecordBaseline(b)); Require(!s.NativePreparedFor(b)); Require(!s.NativePreparedFor(Binding()));
            });
            Check("Authority handoff requires fresh approval", () =>
            {
                var s = Approved(); var b = Binding(); b.AuthorityGeneration++;
                Require(!s.RecordBaseline(b)); Require(!s.ConfirmNativePrepared(Binding())); Require(s.ConfirmNativePrepared(b));
            });
            Check("Parent handoff requires fresh approval and preserves actor lifetime", () =>
            {
                var s = Approved(); var b = Binding(); b.DiscontinuityGeneration++;
                b.Space = MotionCoordinateSpace.ParentLocal; b.ParentNetworkObjectId = 9; b.ParentSpawnGeneration = 7;
                Require(!s.RecordBaseline(b)); Require(!s.NativePreparedFor(b)); Require(s.ConfirmNativePrepared(b));
            });
            Check("New spawn generation identifies a new home lifetime", () =>
            {
                var s = Approved(); var b = Binding(); b.SpawnGeneration++;
                Require(s.RecordBaseline(b)); Require(!s.NativePreparedFor(b));
            });
            Check("New server session identifies a new lifetime", () =>
            {
                var s = Approved(); var b = Binding(); b.SessionId++;
                Require(s.RecordBaseline(b)); Require(!s.ConfirmNativePrepared(Binding()));
            });
            Check("New network object identifies a new lifetime", () =>
            {
                var s = Approved(); var b = Binding(); b.NetworkObjectId++;
                Require(s.RecordBaseline(b)); Require(!s.ConfirmNativePrepared(Binding()));
            });
            Check("Despawn reset clears approval without dropping the mode requirement", () =>
            {
                var s = Approved(); s.ResetLifetime();
                Require(s.CoordinatesRequired && !s.AppliedBinding.IsValid);
                Require(!s.NativePreparedFor(Binding()) && !s.ConfirmNativePrepared(Binding()));
                Require(!s.AllowsSimulation(false, false));
            });
            Check("Reset legacy state remains legacy", () =>
            {
                var s = new GlobalMotionActorState(); s.ResetLifetime();
                Require(!s.CoordinatesRequired && s.AllowsSimulation(false, false));
            });
            Check("Invalid baseline cannot mutate a previously approved state", () =>
            {
                var s = Approved(); bool threw = false;
                try { s.RecordBaseline(default); } catch (ArgumentException) { threw = true; }
                Require(threw && s.AppliedBinding == Binding() && s.NativePreparedFor(Binding()));
            });
            Check("Foreign native approval cannot overwrite good approval", () =>
            {
                var s = Approved(); var b = Binding(); b.SessionId++;
                Require(!s.ConfirmNativePrepared(b)); Require(s.NativePreparedFor(Binding()));
                Require(!s.ConfirmNativePrepared(default)); Require(s.NativePreparedFor(Binding()));
            });
            Check("All generation and parent identity mismatches reject stale confirmation", () =>
            {
                var s = Approved(); var original = Binding();
                for (int i = 0; i < 5; i++)
                {
                    var b = original;
                    if (i == 0) b.AuthorityGeneration++; else if (i == 1) b.DiscontinuityGeneration++;
                    else if (i == 2) b.SpawnGeneration++; else if (i == 3) b.SessionId++;
                    else { b.Space = MotionCoordinateSpace.ParentLocal; b.ParentNetworkObjectId = 9; b.ParentSpawnGeneration = 7; }
                    Require(!s.ConfirmNativePrepared(b));
                }
                Require(s.NativePreparedFor(original));
            });
            Check("1000 pool lifetimes never inherit an old native approval", () =>
            {
                var s = Required(); var b = Binding();
                for (int i = 0; i < 1000; i++)
                {
                    var previous = b; b.SpawnGeneration++;
                    s.ResetLifetime(); Require(s.RecordBaseline(b));
                    Require(!s.ConfirmNativePrepared(previous)); Require(s.ConfirmNativePrepared(b));
                    Require(s.NativePreparedFor(b));
                }
            });
            Check("All three compiled actor controllers implement the baseline contract", () =>
            {
                foreach (var t in new[] { typeof(ProjectC.Player.NetworkPlayer), typeof(ProjectC.Player.ShipController), typeof(ProjectC.AI.NpcBrain) })
                {
                    Require(typeof(IGlobalMotionActorParticipant).IsAssignableFrom(t));
                    Require(t.GetInterfaceMap(typeof(IGlobalMotionActorParticipant)).TargetMethods.Length == 3);
                    Require(t.GetProperty("CanSimulateInCurrentCoordinates") != null);
                }
            });
            Check("Native preparation APIs require an explicit binding", () =>
            {
                var ship = typeof(ProjectC.Player.ShipController).GetMethod("ConfirmGlobalPhysicsPrepared");
                var npc = typeof(ProjectC.AI.NpcBrain).GetMethod("ConfirmGlobalNavigationPrepared");
                Require(ship != null && ship.ReturnType == typeof(bool) && ship.GetParameters()[0].ParameterType == typeof(MotionStreamBinding));
                Require(npc != null && npc.ReturnType == typeof(bool) && npc.GetParameters()[0].ParameterType == typeof(MotionStreamBinding));
            });
            Check("Adapter exposes distinct placed and ready contracts plus serialized opt-in", () =>
            {
                var t = typeof(GlobalMotionPoseAdapter);
                Require(t.GetProperty("IsBaselinePlaced") != null && t.GetProperty("IsBaselineReady") != null);
                var f = t.GetField("_coordinatesRequired", BindingFlags.NonPublic | BindingFlags.Instance);
                Require(f != null && f.FieldType == typeof(bool) && f.IsDefined(typeof(SerializeField), false));
            });
            Check("Skill baseline hook and observer gate are compiled", () =>
            {
                Require(typeof(ProjectC.Skills.SkillAnimationPlayer).GetMethod("OnGlobalMotionBaseline") != null);
                Require(typeof(ProjectC.Player.ShipController).GetProperty("CanObserveInCurrentCoordinates") != null);
            });
            return new Report { passed = passed.Count, failed = failed.Count, checks = passed.ToArray(), failures = failed.ToArray() };
        }
        private static GlobalMotionActorState Required()
        { var s = new GlobalMotionActorState(); s.ObserveRequirement(true); return s; }
        private static GlobalMotionActorState Placed()
        { var s = Required(); s.RecordBaseline(Binding()); return s; }
        private static GlobalMotionActorState Approved()
        { var s = Placed(); s.ConfirmNativePrepared(Binding()); return s; }
        private static MotionStreamBinding Binding() => new MotionStreamBinding
        { SessionId = 71, NetworkObjectId = 4, SpawnGeneration = 2, AuthorityGeneration = 1, DiscontinuityGeneration = 1 };
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
    }
}
