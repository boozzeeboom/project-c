using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Pure role/projection/driver-policy checks. Never instantiates a GameObject or simulates physics.</summary>
    public static class ValidateGlobalMotionApplication
    {
        [Serializable]
        public sealed class Report
        {
            public int passed;
            public int failed;
            public string[] checks;
            public string[] failures;
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Frame Pose Contracts")]
        public static void Execute()
        {
            var r = Run();
            if (r.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(r, true));
            Debug.Log("[T-FO04C] " + r.passed + " pure frame/pose checks passed. Unity drivers and gameplay remain untested.");
        }

        public static Report Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Requires stable Edit Mode.");
            var passed = new List<string>(); var failed = new List<string>();
            Check("Unconfigured and stopped controls have no application role", () =>
            {
                Require(!GlobalMotionApplication.TryResolveRole(default, true, 0, 0, out _));
                var c = Control(); c.IsActive = false; Require(!GlobalMotionApplication.TryResolveRole(c, true, 0, 0, out _));
            }, passed, failed);
            Check("Server authority does not depend on NGO object ownership", () =>
            {
                Require(GlobalMotionApplication.TryResolveRole(Control(), true, 0, 17, out var role));
                Require(role == MotionPoseRole.Authority);
            }, passed, failed);
            Check("Remote view of server authority is a client replica", () =>
            {
                Require(GlobalMotionApplication.TryResolveRole(Control(), false, 17, 17, out var role));
                Require(role == MotionPoseRole.ClientReplica);
            }, passed, failed);
            Check("Owner authority is resolved before publication is acknowledged", () =>
            {
                var c = Control(); c.Authority = GlobalMotionAuthority.Owner; c.PublisherClientId = 17;
                Require(GlobalMotionApplication.TryResolveRole(c, false, 17, 17, out var role)); Require(role == MotionPoseRole.Authority);
            }, passed, failed);
            Check("Host sees remote owner as server replica, not local authority", () =>
            {
                var c = Control(); c.Authority = GlobalMotionAuthority.Owner; c.PublisherClientId = 17;
                Require(GlobalMotionApplication.TryResolveRole(c, true, 0, 17, out var role)); Require(role == MotionPoseRole.ServerReplica);
            }, passed, failed);
            Check("Third-party client sees owner actor as client replica", () =>
            {
                var c = Control(); c.Authority = GlobalMotionAuthority.Owner; c.PublisherClientId = 17;
                Require(GlobalMotionApplication.TryResolveRole(c, false, 18, 17, out var role)); Require(role == MotionPoseRole.ClientReplica);
            }, passed, failed);
            Check("Server-owned Owner mode remains authoritative on host", () =>
            {
                var c = Control(); c.Authority = GlobalMotionAuthority.Owner;
                Require(GlobalMotionApplication.TryResolveRole(c, true, 0, 0, out var role)); Require(role == MotionPoseRole.Authority);
            }, passed, failed);
            Check("Ownership transition gap and invalid server publisher fail closed", () =>
            {
                var c = Control(); c.Authority = GlobalMotionAuthority.Owner; c.PublisherClientId = 17;
                Require(!GlobalMotionApplication.TryResolveRole(c, true, 0, 18, out _));
                c.Authority = GlobalMotionAuthority.Server; Require(!GlobalMotionApplication.TryResolveRole(c, true, 0, 17, out _));
            }, passed, failed);
            Check("Authority does not apply regular echo poses", () =>
            {
                Require(!GlobalMotionApplication.CanWrite(MotionPoseRole.Authority, false, false, false, false, false));
                Require(GlobalMotionApplication.CanWrite(MotionPoseRole.Authority, true, false, false, false, false));
            }, passed, failed);
            Check("Dynamic Rigidbody never accepts remote baseline or regular writes", () =>
            {
                Require(!GlobalMotionApplication.CanWrite(MotionPoseRole.ServerReplica, true, true, false, false, false));
                Require(!GlobalMotionApplication.CanWrite(MotionPoseRole.ClientReplica, false, true, false, false, false));
                Require(GlobalMotionApplication.CanWrite(MotionPoseRole.Authority, true, true, false, false, false));
            }, passed, failed);
            Check("Kinematic Rigidbody permits replica writes without changing authority", () =>
            {
                Require(GlobalMotionApplication.CanWrite(MotionPoseRole.ClientReplica, true, true, true, false, false));
                Require(GlobalMotionApplication.CanWrite(MotionPoseRole.ServerReplica, false, true, true, false, false));
            }, passed, failed);
            Check("Live NavMesh pose writer blocks teleports and remote movement", () =>
            {
                Require(!GlobalMotionApplication.CanWrite(MotionPoseRole.Authority, true, false, false, true, false));
                Require(!GlobalMotionApplication.CanWrite(MotionPoseRole.ClientReplica, true, false, false, true, true));
                Require(!GlobalMotionApplication.CanWrite(MotionPoseRole.ServerReplica, false, false, false, true, true));
            }, passed, failed);
            Check("Already matching authoritative NavMesh baseline can be acknowledged without movement", () =>
            {
                Require(GlobalMotionApplication.CanWrite(MotionPoseRole.Authority, true, false, false, true, true));
            }, passed, failed);
            Check("Unavailable role cannot write", () =>
            {
                Require(!GlobalMotionApplication.CanWrite(MotionPoseRole.Unavailable, true, false, true, false, true));
            }, passed, failed);
            Check("Unknown role is rejected by write and parent-driver policies", () =>
            {
                Require(!GlobalMotionApplication.CanWrite((MotionPoseRole)999, true, false, true, false, true));
                Require(!GlobalMotionApplication.CanUseParentTransform((MotionPoseRole)999, false));
            }, passed, failed);
            Check("Server physics replica rejects interpolated parent render pose", () =>
            {
                Require(!GlobalMotionApplication.CanUseParentTransform(MotionPoseRole.ServerReplica, true));
                Require(GlobalMotionApplication.CanUseParentTransform(MotionPoseRole.ServerReplica, false));
                Require(GlobalMotionApplication.CanUseParentTransform(MotionPoseRole.ClientReplica, true));
                Require(GlobalMotionApplication.CanUseParentTransform(MotionPoseRole.Authority, true));
            }, passed, failed);
            Check("World projection retains millimetres near 1e9 global metres", () =>
            {
                var p = Pose(WorldSnapshot(1000000000.001d)); var frame = new LocalCoordinateFrame(new GlobalPosition(1000000000d, 0d, 0d));
                Require(GlobalMotionApplication.TryWorldPlan(p, frame, out var plan)); Near(plan.Position.x, 0.001d, 0.000001d);
                Require(plan.IsValid && plan.Binding.Space == MotionCoordinateSpace.World);
            }, passed, failed);
            Check("Different frame origins project the same global point", () =>
            {
                var p = Pose(WorldSnapshot(60001d)); var a = new LocalCoordinateFrame(new GlobalPosition(60000d, 0d, 0d));
                var b = new LocalCoordinateFrame(new GlobalPosition(60256d, 0d, 0d));
                Require(GlobalMotionApplication.TryWorldPlan(p, a, out var pa)); Require(GlobalMotionApplication.TryWorldPlan(p, b, out var pb));
                Require(a.ToGlobal(pa.Position) == b.ToGlobal(pb.Position)); Near(pa.Position.x - pb.Position.x, 256d);
            }, passed, failed);
            Check("Unrepresented world point and invalid frame are not zero-position fallbacks", () =>
            {
                var p = Pose(WorldSnapshot(60000d));
                Require(!GlobalMotionApplication.TryWorldPlan(p, new LocalCoordinateFrame(GlobalPosition.Zero), out var plan));
                Require(!plan.IsValid); Require(!GlobalMotionApplication.TryWorldPlan(p, default, out _));
                Require(!GlobalMotionApplication.TryWorldPlan(default, new LocalCoordinateFrame(GlobalPosition.Zero), out _));
            }, passed, failed);
            Check("Parent projection respects rotation, non-uniform scale and translation", () =>
            {
                var parent = ParentBinding(); var rotation = Quaternion.Euler(0f, 90f, 0f);
                var matrix = Matrix4x4.TRS(new Vector3(10f, 3f, -4f), rotation, new Vector3(2f, 1f, 3f));
                var local = new Vector3(1f, 2f, 3f); var p = Pose(ParentSnapshot(local));
                Require(GlobalMotionApplication.TryParentPlan(p, new LocalCoordinateFrame(GlobalPosition.Zero), parent, matrix, rotation, out var plan));
                Near((plan.Position - matrix.MultiplyPoint3x4(local)).magnitude, 0d);
                Require(plan.ParentLocalPosition.Equals(local)); Near(Quaternion.Angle(plan.Rotation, rotation), 0d, 0.05d);
            }, passed, failed);
            Check("Parent-local rotation composes with parent world rotation", () =>
            {
                var s = ParentSnapshot(Vector3.one); s.Rotation = Quaternion.Euler(0f, 45f, 0f);
                var q = Quaternion.Euler(0f, 90f, 0f); var m = Matrix4x4.TRS(Vector3.zero, q, Vector3.one);
                Require(GlobalMotionApplication.TryParentPlan(Pose(s), new LocalCoordinateFrame(GlobalPosition.Zero), ParentBinding(), m, q, out var plan));
                Near(Quaternion.Angle(plan.Rotation, q * s.Rotation), 0d, 0.05d);
                Require(plan.Scale.Equals(s.Scale));
            }, passed, failed);
            Check("Parent session, id and lifetime must all match", () =>
            {
                var p = Pose(ParentSnapshot(Vector3.one)); var f = new LocalCoordinateFrame(GlobalPosition.Zero); var b = ParentBinding();
                b.SessionId++; Require(!GlobalMotionApplication.TryParentPlan(p, f, b, Matrix4x4.identity, Quaternion.identity, out _));
                b = ParentBinding(); b.NetworkObjectId++; Require(!GlobalMotionApplication.TryParentPlan(p, f, b, Matrix4x4.identity, Quaternion.identity, out _));
                b = ParentBinding(); b.SpawnGeneration++; Require(!GlobalMotionApplication.TryParentPlan(p, f, b, Matrix4x4.identity, Quaternion.identity, out _));
            }, passed, failed);
            Check("Parent point cannot be treated as world and vice versa", () =>
            {
                var f = new LocalCoordinateFrame(GlobalPosition.Zero);
                Require(!GlobalMotionApplication.TryWorldPlan(Pose(ParentSnapshot(Vector3.one)), f, out _));
                Require(!GlobalMotionApplication.TryParentPlan(Pose(WorldSnapshot(1d)), f, ParentBinding(), Matrix4x4.identity, Quaternion.identity, out _));
            }, passed, failed);
            Check("Parent matrix must be finite affine and non-singular", () =>
            {
                var p = Pose(ParentSnapshot(Vector3.one)); var f = new LocalCoordinateFrame(GlobalPosition.Zero); var b = ParentBinding();
                var m = Matrix4x4.identity; m.m03 = float.NaN; Require(!GlobalMotionApplication.TryParentPlan(p, f, b, m, Quaternion.identity, out _));
                m = Matrix4x4.zero; Require(!GlobalMotionApplication.TryParentPlan(p, f, b, m, Quaternion.identity, out _));
                m = Matrix4x4.identity; m.m30 = 1f; Require(!GlobalMotionApplication.TryParentPlan(p, f, b, m, Quaternion.identity, out _));
                Require(!GlobalMotionApplication.TryParentPlan(p, f, b, Matrix4x4.identity, default, out _));
            }, passed, failed);
            Check("Out-of-frame parent and resulting child point are rejected", () =>
            {
                var f = new LocalCoordinateFrame(GlobalPosition.Zero); var b = ParentBinding();
                var m = Matrix4x4.Translate(new Vector3(9000f, 0f, 0f));
                Require(!GlobalMotionApplication.TryParentPlan(Pose(ParentSnapshot(Vector3.one)), f, b, m, Quaternion.identity, out _));
                m = Matrix4x4.Translate(new Vector3(8190f, 0f, 0f));
                Require(!GlobalMotionApplication.TryParentPlan(Pose(ParentSnapshot(new Vector3(3f, 0f, 0f))), f, b, m, Quaternion.identity, out _));
            }, passed, failed);
            Check("Huge parent-local coordinates are rejected even when tiny scale hides their range", () =>
            {
                var s = ParentSnapshot(new Vector3(100000f, 0f, 0f)); var m = Matrix4x4.Scale(Vector3.one * 0.01f);
                Require(!GlobalMotionApplication.TryParentPlan(Pose(s), new LocalCoordinateFrame(GlobalPosition.Zero), ParentBinding(), m, Quaternion.identity, out _));
            }, passed, failed);
            Check("Initial baseline sequence seeds from server, including uint max", () =>
            {
                Require(GlobalMotionApplication.ResumeSequence(false, 0, uint.MaxValue) == uint.MaxValue);
                Require(GlobalMotionApplication.ResumeSequence(false, 100, 5) == 5);
            }, passed, failed);
            Check("Pause/resume does not rewind past a lagging server echo", () =>
            {
                Require(GlobalMotionApplication.ResumeSequence(true, 100, 95) == 100);
                Require(GlobalMotionApplication.ResumeSequence(true, 95, 100) == 100);
                Require(GlobalMotionApplication.ResumeSequence(true, 100, 100) == 100);
            }, passed, failed);
            Check("Resume sequence handles wrap and ambiguous half-range", () =>
            {
                Require(GlobalMotionApplication.ResumeSequence(true, 0, uint.MaxValue) == 0);
                Require(GlobalMotionApplication.ResumeSequence(true, uint.MaxValue, 0) == 0);
                Require(GlobalMotionApplication.ResumeSequence(true, 0x80000001u, 1) == 1);
            }, passed, failed);
            Check("1000 projected parent poses preserve small offsets", () =>
            {
                var frame = new LocalCoordinateFrame(new GlobalPosition(1000000000d, 0d, 1000000000d));
                var parent = ParentBinding();
                for (int i = 0; i < 1000; i++)
                {
                    var local = new Vector3(i * 0.001f, 2f, -3f); var q = Quaternion.Euler(0f, i % 360, 0f);
                    var m = Matrix4x4.TRS(new Vector3(20f, 2f, 10f), q, Vector3.one);
                    Require(GlobalMotionApplication.TryParentPlan(Pose(ParentSnapshot(local)), frame, parent, m, q, out var plan));
                    Near((plan.Position - m.MultiplyPoint3x4(local)).magnitude, 0d);
                    Require(frame.ToGlobal(plan.Position).IsFinite);
                }
            }, passed, failed);
            Check("Frame descriptor is immutable and adapter has NGO despawn lifecycle", () =>
            {
                foreach (var p in typeof(GlobalMotionFrame).GetProperties()) Require(p.SetMethod == null);
                Require(typeof(GlobalMotionPoseAdapter).IsSubclassOf(typeof(NetworkBehaviour)));
                Require(typeof(GlobalMotionPoseAdapter).GetMethod("OnNetworkDespawn").DeclaringType == typeof(GlobalMotionPoseAdapter));
                Require(typeof(GlobalMotionReplicator).GetMethod("RevokeBaselineAcknowledgement") != null);
            }, passed, failed);
            Check("Pump and adapter execution orders are explicitly separated", () =>
            {
                var worldOrder = (DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(GlobalMotionWorld), typeof(DefaultExecutionOrder));
                var adapterOrder = (DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(GlobalMotionPoseAdapter), typeof(DefaultExecutionOrder));
                Require(worldOrder.order < 0 && adapterOrder.order > 0);
            }, passed, failed);
            return new Report { passed = passed.Count, failed = failed.Count, checks = passed.ToArray(), failures = failed.ToArray() };
        }

        private static GlobalMotionControl Control()
        {
            return new GlobalMotionControl { HasStream = true, IsActive = true, Revision = 1,
                Authority = GlobalMotionAuthority.Server, PublisherClientId = 0, Baseline = WorldSnapshot(0d) };
        }
        private static MotionStreamBinding Binding() => new MotionStreamBinding
        { SessionId = 55, NetworkObjectId = 42, SpawnGeneration = 1, AuthorityGeneration = 1, DiscontinuityGeneration = 1 };
        private static MotionStreamBinding ParentBinding() => new MotionStreamBinding
        { SessionId = 55, NetworkObjectId = 7, SpawnGeneration = 9, AuthorityGeneration = 1, DiscontinuityGeneration = 1 };
        private static GlobalMotionSnapshot WorldSnapshot(double x) => GlobalMotionSnapshot.CreateWorld(Binding(), 0, 1d,
            new GlobalPosition(x, 0d, 0d), Quaternion.identity, Vector3.one);
        private static GlobalMotionSnapshot ParentSnapshot(Vector3 local)
        {
            var b = Binding(); b.Space = MotionCoordinateSpace.ParentLocal; b.ParentNetworkObjectId = 7; b.ParentSpawnGeneration = 9;
            return GlobalMotionSnapshot.CreateParentLocal(b, 0, 1d, local, Quaternion.identity, Vector3.one);
        }
        private static GlobalMotionPose Pose(GlobalMotionSnapshot snapshot)
        {
            var b = new GlobalMotionBuffer(); Require(b.BeginStream(snapshot.Binding, snapshot, out _));
            Require(b.TrySample(snapshot.SampleTime, out var pose)); return pose;
        }
        private static void Check(string name, Action body, List<string> passed, List<string> failed)
        { try { body(); passed.Add(name); } catch (Exception e) { failed.Add(name + ": " + e.GetType().Name + ": " + e.Message); } }
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
        private static void Near(double actual, double expected, double tolerance = 0.0001d)
        { if (double.IsNaN(actual) || double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance) throw new InvalidOperationException($"Expected {expected:R}, got {actual:R}"); }
    }
}
