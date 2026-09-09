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
    /// <summary>T-FO04B: pure control/admission/session checks, real NGO serialization, no spawned objects or Play Mode.</summary>
    public static class ValidateGlobalMotionTransport
    {
        [Serializable]
        public sealed class Report
        {
            public int passed;
            public int failed;
            public string[] checks;
            public string[] failures;
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Motion Transport Contracts")]
        public static void Execute()
        {
            var r = Run();
            if (r.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(r, true));
            Debug.Log("[T-FO04B] " + r.passed + " contract checks passed. Real Host/Client transport remains untested.");
        }

        public static Report Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Requires stable Edit Mode.");
            var passed = new List<string>(); var failed = new List<string>();
            Check("Session id zero rejected; random token nonzero", () =>
            {
                Expect<ArgumentOutOfRangeException>(() => new GlobalMotionSession(0));
                Require(GlobalMotionSession.CreateRandom().SessionId != 0);
            }, passed, failed);
            Check("Spawn generations separate reused NetworkObjectId", () =>
            {
                var session = new GlobalMotionSession(55); var a = session.Allocate(0); var b = session.Allocate(0);
                Require(a.IsValid && b.IsValid && a.NetworkObjectId == 0 && b.SpawnGeneration > a.SpawnGeneration);
            }, passed, failed);
            Check("Authority/discontinuity issuance preserves lifetime and validates parent", () =>
            {
                var s = new GlobalMotionSession(55); var a = s.Allocate(42);
                var b = s.NextDiscontinuity(a, MotionCoordinateSpace.ParentLocal, 7, 9);
                var c = s.NextAuthority(b);
                Require(b.SpawnGeneration == a.SpawnGeneration && b.DiscontinuityGeneration > a.DiscontinuityGeneration);
                Require(c.AuthorityGeneration > b.AuthorityGeneration && c.ParentSpawnGeneration == 9);
                Expect<ArgumentException>(() => s.NextDiscontinuity(a, MotionCoordinateSpace.ParentLocal, 42, 1));
                var other = new GlobalMotionSession(56); Expect<ArgumentException>(() => other.NextAuthority(a));
            }, passed, failed);
            Check("Generation counters fail instead of wrapping", () =>
            {
                var s = new GlobalMotionSession(55); var b = s.Allocate(42); b.AuthorityGeneration = ulong.MaxValue;
                Expect<OverflowException>(() => s.NextAuthority(b)); b = s.Allocate(42); b.DiscontinuityGeneration = ulong.MaxValue;
                Expect<OverflowException>(() => s.NextDiscontinuity(b, MotionCoordinateSpace.World));
            }, passed, failed);
            Check("Default initial sync round trip is 3 bytes and clears reused control", () =>
            {
                using var w = new FastBufferWriter(512, Allocator.Temp); w.WriteNetworkSerializable(default(GlobalMotionControl));
                Require(w.Length == 3); using var r = new FastBufferReader(w, Allocator.Temp);
                var target = Control(); r.ReadNetworkSerializableInPlace(ref target); Require(!target.HasStream && target.Revision == 0);
            }, passed, failed);
            Check("Active world control round trip is 144 bytes", () =>
            {
                var c = Control(); using var w = new FastBufferWriter(512, Allocator.Temp); w.WriteNetworkSerializable(c);
                Require(w.Length == 144); using var r = new FastBufferReader(w, Allocator.Temp);
                r.ReadNetworkSerializable(out GlobalMotionControl copy); Equal(c, copy);
            }, passed, failed);
            Check("All truncated active controls leave prior state unchanged", () =>
            {
                var c = Control(); using var w = new FastBufferWriter(512, Allocator.Temp); w.WriteNetworkSerializable(c);
                for (int n = 0; n < w.Length; n++)
                {
                    using var r = new FastBufferReader(w, Allocator.Temp, n);
                    var original = c; original.Revision = 77; var target = original;
                    Expect<OverflowException>(() => r.ReadNetworkSerializableInPlace(ref target)); Equal(original, target);
                }
            }, passed, failed);
            Check("Control codec rejects unknown version and invalid authority", () =>
            {
                using var w = new FastBufferWriter(512, Allocator.Temp); w.WriteValueSafe((ushort)999);
                using var r = new FastBufferReader(w, Allocator.Temp); var before = Control(); var target = before;
                Expect<InvalidOperationException>(() => r.ReadNetworkSerializableInPlace(ref target)); Equal(before, target);
                var bad = before; bad.Authority = (GlobalMotionAuthority)99; Require(!bad.IsValid);
                using var outBuffer = new FastBufferWriter(512, Allocator.Temp);
                Expect<InvalidOperationException>(() => outBuffer.WriteNetworkSerializable(bad)); Require(outBuffer.Length == 0);
            }, passed, failed);
            Check("Control authenticated sender and expected object enforced", () =>
            {
                var receiver = new GlobalMotionControlReceiver(); var c = Control();
                Require(!receiver.TryApply(c, 17, 0, 42)); Require(!receiver.TryApply(c, 0, 0, 99));
                Require(!receiver.Control.HasStream && receiver.BufferedCount == 0);
                c.PublisherClientId = 17; Require(!receiver.TryApply(c, 0, 0, 42)); // Server authority cannot claim a remote publisher.
            }, passed, failed);
            Check("Empty and duplicate initial controls cannot clear current stream", () =>
            {
                var r = Receiver(); Require(!r.TryApply(default, 0, 0, 42)); Require(!r.TryApply(Control(), 0, 0, 42));
                Require(r.Control.IsActive && r.BufferedCount == 1);
            }, passed, failed);
            Check("Wrong-server motion and unsolicited new epochs rejected", () =>
            {
                var r = Receiver(); var c = Control(); var p = Sample(c.Baseline.Binding, 1, 10.1d, 1d);
                Require(!r.TryAdd(p, 17, 0)); p.Binding.DiscontinuityGeneration++;
                Require(!r.TryAdd(p, 0, 0)); Require(r.Control.Baseline.Sequence == 0);
            }, passed, failed);
            Check("Reliable keyframe recovers lost final unreliable pose", () =>
            {
                var r = Receiver(); var c = Control(); c.Revision = 2; c.Baseline = Sample(c.Baseline.Binding, 1, 10.1d, 100d);
                Require(r.TryApply(c, 0, 0, 42)); Require(r.TrySample(20d, out var pose)); Near(pose.WorldPosition.X, 100d);
            }, passed, failed);
            Check("Lagging reliable keyframe keeps newer buffered motion", () =>
            {
                var r = Receiver(); var c = Control(); Require(r.TryAdd(Sample(c.Baseline.Binding, 3, 10.3d, 300d), 0, 0));
                c.Revision = 2; c.Baseline = Sample(c.Baseline.Binding, 1, 10.1d, 100d);
                Require(r.TryApply(c, 0, 0, 42)); Require(r.Control.Revision == 2 && r.Control.Baseline.Sequence == 3);
                Require(r.TrySample(20d, out var pose)); Near(pose.WorldPosition.X, 300d);
            }, passed, failed);
            Check("Stop tombstone rejects motion and same-epoch restart", () =>
            {
                var r = Receiver(); var c = Control(); c.Revision = 2; c.IsActive = false;
                Require(r.TryApply(c, 0, 0, 42)); Require(!r.TrySample(20d, out _));
                Require(!r.TryAdd(Sample(c.Baseline.Binding, 2, 10.2d, 2d), 0, 0));
                c.Revision = 3; c.IsActive = true; Require(!r.TryApply(c, 0, 0, 42));
                c.Baseline.Binding.DiscontinuityGeneration++; Require(r.TryApply(c, 0, 0, 42));
                Require(r.Control.IsActive && r.BufferedCount == 1);
            }, passed, failed);
            Check("Publisher switch requires authority, not only discontinuity, generation", () =>
            {
                var r = Receiver(); var c = Control(); c.Revision = 2; c.Authority = GlobalMotionAuthority.Owner; c.PublisherClientId = 17;
                c.Baseline.Binding.DiscontinuityGeneration++; Require(!r.TryApply(c, 0, 0, 42));
                c.Baseline.Binding.AuthorityGeneration++; Require(r.TryApply(c, 0, 0, 42));
                Require(r.Control.PublisherClientId == 17);
            }, passed, failed);
            Check("Older epoch cannot return with a larger control revision", () =>
            {
                var r = Receiver(); var next = Control(); next.Revision = 2; next.Baseline.Binding.AuthorityGeneration++;
                Require(r.TryApply(next, 0, 0, 42)); var old = Control(); old.Revision = 999;
                Require(!r.TryApply(old, 0, 0, 42)); Require(r.Control.Baseline.Binding.AuthorityGeneration == 2);
            }, passed, failed);
            Check("Despawn Reset permits new session/lifetime, not stale old motion", () =>
            {
                var r = Receiver(); var c = Control(); c.Revision = 2; c.Baseline.Binding.SessionId++;
                Require(!r.TryApply(c, 0, 0, 42)); r.Reset(); Require(r.TryApply(c, 0, 0, 42));
                Require(!r.TryAdd(Sample(Control().Baseline.Binding, 999, 10.1d, 99d), 0, 0));
            }, passed, failed);
            Check("Admission cannot run before Begin or after Stop", () =>
            {
                var g = new GlobalMotionAdmission(); var s = Sample(Control().Baseline.Binding, 1, 10d, 1d);
                Require(!g.TryAccept(17, s, 10d, null, out _, out var why) && why == MotionAdmissionReject.NotActive);
                g.Begin(17, Control().Baseline, 10d); g.Stop(); Require(!g.TryAccept(17, s, 10d, null, out _, out _));
            }, passed, failed);
            Check("Wrong publisher and wrong binding cannot change accepted pose", () =>
            {
                var g = Gate(); var s = Sample(Control().Baseline.Binding, 1, 10.1d, 9d);
                Require(!g.TryAccept(18, s, 10.1d, null, out _, out var why) && why == MotionAdmissionReject.WrongSender);
                Require(!g.TryAccept(0, s, 10.1d, null, out _, out _)); s.Binding.AuthorityGeneration++;
                Require(!g.TryAccept(17, s, 10.1d, null, out _, out why) && why == MotionAdmissionReject.WrongBinding);
                Require(g.Latest.Sequence == 0);
            }, passed, failed);
            Check("Server replaces source clock for accepted broadcast", () =>
            {
                var g = Gate(); var s = Sample(Control().Baseline.Binding, 1, 9.8d, 1000000000.001d);
                Require(g.TryAccept(17, s, 10.1d, null, out var accepted, out _));
                Require(accepted.SampleTime == 10.1d && accepted.WorldPosition == s.WorldPosition && accepted.Sequence == s.Sequence);
            }, passed, failed);
            Check("Stale/future/nonfinite source time does not consume sequence", () =>
            {
                var g = Gate(); var s = Sample(Control().Baseline.Binding, 1, 8d, 1d);
                Require(!g.TryAccept(17, s, 10d, null, out _, out var why) && why == MotionAdmissionReject.TimeWindow);
                s.SampleTime = 10.3d; Require(!g.TryAccept(17, s, 10d, null, out _, out _));
                s.SampleTime = double.NaN; Require(!g.TryAccept(17, s, 10d, null, out _, out _));
                s.SampleTime = 10d; Require(g.TryAccept(17, s, 10d, null, out _, out _));
            }, passed, failed);
            Check("Source time regression and receive time regression rejected separately", () =>
            {
                var g = Gate(); var b = Control().Baseline.Binding;
                Require(g.TryAccept(17, Sample(b, 1, 10d, 1d), 10d, null, out _, out _));
                Require(!g.TryAccept(17, Sample(b, 2, 9.9d, 2d), 10.1d, null, out _, out var why) && why == MotionAdmissionReject.SourceTimeRegression);
                Require(!g.TryAccept(17, Sample(b, 2, 10.1d, 2d), 9.9d, null, out _, out why) && why == MotionAdmissionReject.ReceiveTimeRegression);
                Require(g.TryAccept(17, Sample(b, 2, 10.1d, 2d), 10.1d, null, out _, out _));
            }, passed, failed);
            Check("Rate bucket bounds accepted burst then refills", () =>
            {
                var g = new GlobalMotionAdmission(1d, 0.25d, 1d, 2); var c = Control(); g.Begin(17, c.Baseline, 10d);
                Require(g.TryAccept(17, Sample(c.Baseline.Binding, 1, 10d, 1d), 10d, null, out _, out _));
                Require(g.TryAccept(17, Sample(c.Baseline.Binding, 2, 10d, 2d), 10d, null, out _, out _));
                Require(!g.TryAccept(17, Sample(c.Baseline.Binding, 3, 10d, 3d), 10d, null, out _, out var why) && why == MotionAdmissionReject.RateLimited);
                Require(g.TryAccept(17, Sample(c.Baseline.Binding, 3, 11d, 3d), 11d, null, out _, out _));
            }, passed, failed);
            Check("Rejected game rules spend budget but never commit sequence", () =>
            {
                var g = new GlobalMotionAdmission(1d, 0.25d, 1d, 2); var c = Control(); g.Begin(17, c.Baseline, 10d);
                int calls = 0; Func<GlobalMotionSnapshot, bool> rule = p => { calls++; return false; };
                var s = Sample(c.Baseline.Binding, 1, 10d, 1d);
                Require(!g.TryAccept(17, s, 10d, rule, out _, out _)); Require(!g.TryAccept(17, s, 10d, rule, out _, out _));
                Require(!g.TryAccept(17, s, 10d, rule, out _, out var why) && why == MotionAdmissionReject.RateLimited);
                Require(calls == 2 && g.Latest.Sequence == 0);
                s.SampleTime = 11d; Require(g.TryAccept(17, s, 11d, p => true, out _, out _));
            }, passed, failed);
            Check("Throwing game validator cannot partially accept a sample", () =>
            {
                var g = Gate(); var s = Sample(Control().Baseline.Binding, 1, 10d, 1d);
                Expect<InvalidOperationException>(() => g.TryAccept(17, s, 10d, p => throw new InvalidOperationException("test"), out _, out _));
                Require(g.Latest.Sequence == 0);
            }, passed, failed);
            Check("Lifecycle reentry from validator cannot publish the retired binding", () =>
            {
                var g = Gate(); var s = Sample(Control().Baseline.Binding, 1, 10d, 1d);
                Require(!g.TryAccept(17, s, 10d, p => { g.Stop(); return true; }, out _, out _));
                Require(g.Latest.Sequence == 0);
                g = Gate(); var next = Control().Baseline; next.Binding.AuthorityGeneration++;
                Require(!g.TryAccept(17, s, 10d, p => { g.Begin(18, next, 10d); return true; }, out _, out _));
                Require(g.Latest.Binding == next.Binding && g.Latest.Sequence == 0);
            }, passed, failed);
            Check("Admission invalid configuration and baseline fail closed", () =>
            {
                Expect<ArgumentOutOfRangeException>(() => new GlobalMotionAdmission(samplesPerSecond: 0d));
                Expect<ArgumentOutOfRangeException>(() => new GlobalMotionAdmission(burst: 257));
                var g = Gate(); var invalid = Control().Baseline; invalid.SampleTime = 100d;
                Expect<ArgumentException>(() => g.Begin(18, invalid, 10d)); Require(g.Latest.Sequence == 0);
            }, passed, failed);
            Check("Compiled adapter declares real NGO RPCs and lifecycle overrides", () =>
            {
                var t = typeof(GlobalMotionReplicator); Require(t.IsSubclassOf(typeof(NetworkBehaviour)));
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                string[] names = { "ReceiveControlRpc", "ReceiveMotionRpc", "SubmitOwnerMotionRpc" };
                foreach (string name in names) Require(t.GetMethod(name, flags).GetCustomAttributes(typeof(RpcAttribute), false).Length == 1);
                Require(t.GetMethod("OnSynchronize", flags).DeclaringType == t);
                Require(t.GetMethod("OnOwnershipChanged", flags).DeclaringType == t);
            }, passed, failed);
            Check("Ownership stop and explicit fresh reactivation preserve epoch ordering", () =>
            {
                var r = Receiver(); var stopped = Control(); stopped.Revision = 2; stopped.IsActive = false;
                stopped.Authority = GlobalMotionAuthority.Owner; stopped.PublisherClientId = 17;
                stopped.Baseline.Binding.AuthorityGeneration++;
                Require(r.TryApply(stopped, 0, 0, 42)); Require(!r.Control.IsActive);
                var active = stopped; active.Revision = 3; active.IsActive = true;
                Require(!r.TryApply(active, 0, 0, 42)); active.Baseline.Binding.DiscontinuityGeneration++;
                Require(r.TryApply(active, 0, 0, 42)); Require(r.Control.PublisherClientId == 17 && r.Control.IsActive);
            }, passed, failed);
            Check("Recursive admission is rejected without corrupting the outer sample", () =>
            {
                var g = Gate(); var s = Sample(Control().Baseline.Binding, 1, 10d, 1d);
                bool nested = true; MotionAdmissionReject rejection = MotionAdmissionReject.None;
                Require(g.TryAccept(17, s, 10d, p =>
                {
                    nested = g.TryAccept(17, p, 10d, null, out _, out rejection); return true;
                }, out var accepted, out _));
                Require(!nested && rejection == MotionAdmissionReject.ReentrantValidation && accepted.Sequence == 1);
            }, passed, failed);
            Check("Regressing keyframe time is rejected without poisoning its control revision", () =>
            {
                var r = Receiver(); var c = Control(); c.Revision = 2;
                c.Baseline = Sample(c.Baseline.Binding, 1, 9d, 1d);
                Require(!r.TryApply(c, 0, 0, 42)); Require(r.Control.Revision == 1);
                c.Baseline.SampleTime = 10.1d; Require(r.TryApply(c, 0, 0, 42)); Require(r.Control.Revision == 2);
            }, passed, failed);
            Check("Parent-local control serializes as 132 bytes", () =>
            {
                var c = Control(); var b = c.Baseline.Binding; b.Space = MotionCoordinateSpace.ParentLocal;
                b.ParentNetworkObjectId = 7; b.ParentSpawnGeneration = 3;
                c.Baseline = GlobalMotionSnapshot.CreateParentLocal(b, 0, 10d, new Vector3(1f, 2f, 3f), Quaternion.identity, Vector3.one);
                using var w = new FastBufferWriter(512, Allocator.Temp); w.WriteNetworkSerializable(c); Require(w.Length == 132);
                using var r = new FastBufferReader(w, Allocator.Temp); r.ReadNetworkSerializable(out GlobalMotionControl copy); Equal(c, copy);
            }, passed, failed);
            return new Report { passed = passed.Count, failed = failed.Count, checks = passed.ToArray(), failures = failed.ToArray() };
        }

        private static GlobalMotionControl Control()
        {
            var b = new GlobalMotionSession(55).Allocate(42);
            return new GlobalMotionControl { HasStream = true, IsActive = true, Revision = 1, Authority = GlobalMotionAuthority.Server,
                PublisherClientId = 0, Baseline = Sample(b, 0, 10d, 0d) };
        }
        private static GlobalMotionSnapshot Sample(MotionStreamBinding b, uint seq, double time, double x) =>
            GlobalMotionSnapshot.CreateWorld(b, seq, time, new GlobalPosition(x, 0d, 0d), Quaternion.identity, Vector3.one);
        private static GlobalMotionControlReceiver Receiver()
        {
            var r = new GlobalMotionControlReceiver(); Require(r.TryApply(Control(), 0, 0, 42)); return r;
        }
        private static GlobalMotionAdmission Gate()
        {
            var g = new GlobalMotionAdmission(); g.Begin(17, Control().Baseline, 10d); return g;
        }
        private static void Equal(GlobalMotionControl a, GlobalMotionControl b)
        {
            Require(a.HasStream == b.HasStream && a.IsActive == b.IsActive && a.Revision == b.Revision && a.Authority == b.Authority &&
                a.PublisherClientId == b.PublisherClientId && a.Baseline.Version == b.Baseline.Version && a.Baseline.Binding == b.Baseline.Binding &&
                a.Baseline.Sequence == b.Baseline.Sequence && a.Baseline.SampleTime == b.Baseline.SampleTime && a.Baseline.WorldPosition == b.Baseline.WorldPosition &&
                a.Baseline.ParentLocalPosition.Equals(b.Baseline.ParentLocalPosition) && a.Baseline.Rotation.Equals(b.Baseline.Rotation) && a.Baseline.Scale.Equals(b.Baseline.Scale));
        }
        private static void Check(string name, Action body, List<string> passed, List<string> failed)
        { try { body(); passed.Add(name); } catch (Exception e) { failed.Add(name + ": " + e.GetType().Name + ": " + e.Message); } }
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
        private static void Near(double actual, double expected)
        { if (double.IsNaN(actual) || Math.Abs(actual - expected) > 0.000001d) throw new InvalidOperationException($"Expected {expected:R}, got {actual:R}"); }
        private static void Expect<T>(Action body) where T : Exception
        { try { body(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    }
}
