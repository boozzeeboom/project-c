using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Pure parent identity, projection and metadata guards. Does not mock or create Unity physics objects.</summary>
    public static class ValidateGlobalMotionHierarchy
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }
        [MenuItem("ProjectC/World/Floating Origin/Validate Custom Hierarchy Contracts")]
        public static void Execute()
        {
            var report = Run();
            if (report.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(report, true));
            Debug.Log("[T-FO04F] " + report.passed + " pure hierarchy checks passed. Native parenting untested.");
        }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var passed = new List<string>(); var failed = new List<string>();
            void Check(string name, Action action) { try { action(); passed.Add(name); } catch (Exception e) { failed.Add(name + ": " + e.Message); } }
            bool Match(MotionStreamBinding c, MotionStreamBinding p) => GlobalMotionHierarchy.ParentMatches(c, p, true, true, true, false);
            Check("Ready exact parent identity accepted", () => Require(Match(Child(), Parent())));
            Check("Unready parent waits", () => Require(!GlobalMotionHierarchy.ParentMatches(Child(), Parent(), false, true, true, false)));
            Check("Cross-frame parent rejected", () => Require(!GlobalMotionHierarchy.ParentMatches(Child(), Parent(), true, false, true, false)));
            Check("Cross-scene attachment rejected even in same physics frame", () => Require(!GlobalMotionHierarchy.ParentMatches(Child(), Parent(), true, true, false, false)));
            Check("Prospective hierarchy cycle rejected", () => Require(!GlobalMotionHierarchy.ParentMatches(Child(), Parent(), true, true, true, true)));
            Check("Foreign session rejected", () => { var p = Parent(); p.SessionId++; Require(!Match(Child(), p)); });
            Check("Wrong parent object rejected", () => { var p = Parent(); p.NetworkObjectId++; Require(!Match(Child(), p)); });
            Check("Pooled parent generation rejected", () => { var p = Parent(); p.SpawnGeneration++; Require(!Match(Child(), p)); });
            Check("Invalid default parent or child rejected", () => { Require(!Match(default, Parent())); Require(!Match(Child(), default)); });
            Check("World binding is not parent-local", () => { var c = Child(); c.Space = MotionCoordinateSpace.World; c.ParentNetworkObjectId = 0; c.ParentSpawnGeneration = 0; Require(!Match(c, Parent())); });
            Check("Self parenting rejected", () => { var c = Child(); c.ParentNetworkObjectId = c.NetworkObjectId; var p = Parent(); p.NetworkObjectId = c.NetworkObjectId; Require(!Match(c, p)); });
            Check("Parent network id zero is valid and not confused with no parent", () => { var p = Parent(); p.NetworkObjectId = 0; var c = Child(); c.ParentNetworkObjectId = 0; Require(Match(c, p)); });
            Check("Parent authority generation can advance within same lifetime", () => { var p = Parent(); p.AuthorityGeneration++; Require(Match(Child(), p)); });
            Check("Parent discontinuity can advance within same lifetime", () => { var p = Parent(); p.DiscontinuityGeneration++; Require(Match(Child(), p)); });
            Check("Readiness arrival does not require changing child binding", () => { var c = Child(); Require(!GlobalMotionHierarchy.ParentMatches(c, Parent(), false, true, true, false)); Require(Match(c, Parent())); });
            Check("1000 reused parent ids reject their previous lifetime", () => { var p = Parent(); var c = Child(); for (int i = 0; i < 1000; i++) { p.SpawnGeneration++; Require(!Match(c, p)); c.ParentSpawnGeneration = p.SpawnGeneration; Require(Match(c, p)); } });
            Check("Known roles allow prepared transform hierarchy changes", () => { foreach (var r in Roles()) Require(GlobalMotionHierarchy.CanChangeHierarchy(r, true, false, false, false)); });
            Check("Unknown role rejected", () => { Require(!GlobalMotionHierarchy.CanChangeHierarchy(MotionPoseRole.Unavailable, true, false, false, false)); Require(!GlobalMotionHierarchy.CanChangeHierarchy((MotionPoseRole)999, true, false, false, false)); });
            Check("Native stock sync forbids custom hierarchy", () => { foreach (var r in Roles()) Require(!GlobalMotionHierarchy.CanChangeHierarchy(r, false, false, false, false)); });
            Check("Rigidbody and joint hierarchy changes rejected", () => { foreach (var r in Roles()) Require(!GlobalMotionHierarchy.CanChangeHierarchy(r, true, true, false, false)); });
            Check("Enabled NavMeshAgent rejected even if pose flags are externally disabled", () => { foreach (var r in Roles()) Require(!GlobalMotionHierarchy.CanChangeHierarchy(r, true, false, true, false)); });
            Check("Unsupported colliders cannot become compound bodies silently", () => { foreach (var r in Roles()) Require(!GlobalMotionHierarchy.CanChangeHierarchy(r, true, false, false, true)); });
            Check("Server authority cannot use interpolated parent render pose", () => Require(!GlobalMotionHierarchy.CanUseParentPose(MotionPoseRole.Authority, true, true)));
            Check("Server replica cannot use interpolated parent render pose", () => Require(!GlobalMotionHierarchy.CanUseParentPose(MotionPoseRole.ServerReplica, true, true)));
            Check("Client view may use interpolated parent render pose", () => Require(GlobalMotionHierarchy.CanUseParentPose(MotionPoseRole.ClientReplica, false, true)));
            Check("Client owner retains prior parent-render policy", () => Require(GlobalMotionHierarchy.CanUseParentPose(MotionPoseRole.Authority, false, true)));
            Check("Noninterpolated parent pose usable by valid roles", () => { foreach (var r in Roles()) Require(GlobalMotionHierarchy.CanUseParentPose(r, true, false)); });
            Check("Unknown parent projection role rejected", () => Require(!GlobalMotionHierarchy.CanUseParentPose((MotionPoseRole)999, false, false)));
            Check("Attach then detach allocate discontinuities without changing child lifetime", () =>
            {
                var session = new GlobalMotionSession(71); var child = session.Allocate(4); var parent = session.Allocate(9);
                var attached = session.NextDiscontinuity(child, MotionCoordinateSpace.ParentLocal, 9, parent.SpawnGeneration);
                var detached = session.NextDiscontinuity(attached, MotionCoordinateSpace.World);
                Require(attached.SpawnGeneration == child.SpawnGeneration && detached.SpawnGeneration == child.SpawnGeneration);
                Require(attached.DiscontinuityGeneration > child.DiscontinuityGeneration && detached.DiscontinuityGeneration > attached.DiscontinuityGeneration);
                Require(detached.ParentNetworkObjectId == 0 && detached.ParentSpawnGeneration == 0 && Match(attached, parent));
            });
            Check("Parent-local projection works at a distant global origin", () =>
            {
                var frame = Frame(); var pose = Pose(); var rotation = Quaternion.Euler(0, 35, 0);
                var matrix = Matrix4x4.TRS(new Vector3(40, 20, 10), rotation, Vector3.one);
                Require(GlobalMotionApplication.TryParentPlan(pose, frame, Parent(), matrix, rotation, out var plan));
                Require((plan.Position - matrix.MultiplyPoint3x4(pose.ParentLocalPosition)).sqrMagnitude < 1e-8f);
                Require(frame.ToGlobal(plan.Position).X > 999999000d);
            });
            Check("Independent client origins produce the same global child location", () =>
            {
                var a = Frame(); var b = new LocalCoordinateFrame(a.Origin.Translated(new Vector3(1000, 0, 0)));
                var pose = Pose(); var rot = Quaternion.Euler(0, 35, 0);
                Require(GlobalMotionApplication.TryParentPlan(pose, a, Parent(), Matrix4x4.TRS(new Vector3(40, 20, 10), rot, Vector3.one), rot, out var pa));
                Require(GlobalMotionApplication.TryParentPlan(pose, b, Parent(), Matrix4x4.TRS(new Vector3(-960, 20, 10), rot, Vector3.one), rot, out var pb));
                var ga = a.ToGlobal(pa.Position); var gb = b.ToGlobal(pb.Position);
                Require(Math.Abs(ga.X - gb.X) < 0.001d && Math.Abs(ga.Y - gb.Y) < 0.001d && Math.Abs(ga.Z - gb.Z) < 0.001d);
            });
            Check("Detach uses explicit global position rather than old parent-local vector", () =>
            {
                var frame = Frame(); var rot = Quaternion.Euler(0, 35, 0);
                Require(GlobalMotionApplication.TryParentPlan(Pose(), frame, Parent(), Matrix4x4.TRS(new Vector3(40, 20, 10), rot, Vector3.one), rot, out var parentPlan));
                var binding = new GlobalMotionSession(71).NextDiscontinuity(Child(), MotionCoordinateSpace.World);
                var snapshot = GlobalMotionSnapshot.CreateWorld(binding, 0, 0, frame.ToGlobal(parentPlan.Position), parentPlan.Rotation, Vector3.one);
                Require(GlobalMotionApplication.TryWorldPlan(Pose(snapshot), frame, out var worldPlan));
                Require((worldPlan.Position - parentPlan.Position).sqrMagnitude < 1e-8f && Quaternion.Angle(worldPlan.Rotation, parentPlan.Rotation) < 0.001f);
                Require((worldPlan.Position - Pose().ParentLocalPosition).sqrMagnitude > 10f);
            });
            Check("Parent matrix outside frame rejected before hierarchy mutation", () => Require(!GlobalMotionApplication.TryParentPlan(Pose(), Frame(), Parent(), Matrix4x4.Translate(new Vector3(9000, 0, 0)), Quaternion.identity, out _)));
            Check("Singular parent matrix rejected", () => Require(!GlobalMotionApplication.TryParentPlan(Pose(), Frame(), Parent(), Matrix4x4.Scale(Vector3.zero), Quaternion.identity, out _)));
            Check("Pure projection rejects wrong parent lifetime", () => { var p = Parent(); p.SpawnGeneration++; Require(!GlobalMotionApplication.TryParentPlan(Pose(), Frame(), p, Matrix4x4.identity, Quaternion.identity, out _)); });
            Check("Hierarchy-capable global protocol differs from E and legacy", () => Require(GlobalMotionNetworkContract.ProtocolVersion >= 0xF002 && GlobalMotionNetworkContract.IsReservedProtocolVersion(GlobalMotionNetworkContract.ProtocolVersion)));
            Check("Old and current global protocol versions stay reserved", () => { Require(GlobalMotionNetworkContract.IsReservedProtocolVersion(0xF001)); Require(GlobalMotionNetworkContract.IsReservedProtocolVersion(0xF002)); Require(GlobalMotionNetworkContract.IsReservedProtocolVersion(0xF0FF)); Require(!GlobalMotionNetworkContract.IsReservedProtocolVersion(0)); Require(!GlobalMotionNetworkContract.IsReservedProtocolVersion(77)); });
            Check("E hello rejected by F handshake", () => { var digest = new byte[32]; digest[0] = 1; var expected = GlobalMotionNetworkContract.CreateHello(digest, digest); var old = (byte[])expected.Clone(); old[6] = 1; old[7] = 0xF0; Require(!GlobalMotionNetworkContract.ValidateHello(old, expected, out _)); });
            Check("Transport exposes pre-publication baseline preflight", () => { foreach (var name in new[] { "ActivateWorldServer", "ActivateParentLocalServer" }) { var p = typeof(GlobalMotionReplicator).GetMethod(name).GetParameters(); Require(p[p.Length - 1].ParameterType == typeof(Func<GlobalMotionControl, bool>)); } });
            Check("Adapter compiled hierarchy and preflight hooks exist", () => { var t = typeof(GlobalMotionPoseAdapter); Require(t.GetMethod("CanPrepareControl", BindingFlags.NonPublic | BindingFlags.Instance) != null); Require(t.GetMethod("TryPlanHierarchy", BindingFlags.NonPublic | BindingFlags.Instance) != null); });
            return new Report { passed = passed.Count, failed = failed.Count, checks = passed.ToArray(), failures = failed.ToArray() };
        }
        private static MotionPoseRole[] Roles() => new[] { MotionPoseRole.Authority, MotionPoseRole.ServerReplica, MotionPoseRole.ClientReplica };
        private static MotionStreamBinding Parent() => new MotionStreamBinding { SessionId = 71, NetworkObjectId = 9, SpawnGeneration = 2, AuthorityGeneration = 1, DiscontinuityGeneration = 1 };
        private static MotionStreamBinding Child() => new MotionStreamBinding { SessionId = 71, NetworkObjectId = 4, SpawnGeneration = 3, AuthorityGeneration = 1, DiscontinuityGeneration = 2, Space = MotionCoordinateSpace.ParentLocal, ParentNetworkObjectId = 9, ParentSpawnGeneration = 2 };
        private static LocalCoordinateFrame Frame() => new LocalCoordinateFrame(new GlobalPosition(1e9, 2e9, -1e9));
        private static GlobalMotionPose Pose() => Pose(GlobalMotionSnapshot.CreateParentLocal(Child(), 0, 0, new Vector3(1.25f, 2.5f, -4), Quaternion.identity, Vector3.one));
        private static GlobalMotionPose Pose(GlobalMotionSnapshot snapshot)
        {
            var buffer = new GlobalMotionBuffer();
            Require(buffer.BeginStream(snapshot.Binding, snapshot, out _));
            Require(buffer.TrySample(snapshot.SampleTime, out var pose));
            return pose;
        }
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
    }
}
