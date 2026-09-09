using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    /// <summary>Ephemeral evidence only. Never serialize these NGO/session/identity-lease fields as durable player identity.</summary>
    public sealed class GlobalPlayerCaptureEntry
    {
        public string PlayerId { get; }
        public Guid IdentityLease { get; }
        public ulong OwnerClientId { get; }
        public GlobalMotionSnapshot Sample { get; }
        public GlobalMotionAuthority Authority { get; }
        public ulong PublisherClientId { get; }
        public bool InShip { get; }
        public GlobalPlayerCaptureEntry(string playerId, Guid identityLease, ulong ownerClientId, GlobalMotionSnapshot sample, GlobalMotionAuthority authority, ulong publisherClientId, bool inShip)
        { PlayerId = playerId; IdentityLease = identityLease; OwnerClientId = ownerClientId; Sample = sample; Authority = authority; PublisherClientId = publisherClientId; InShip = inShip; }
    }
    public sealed class GlobalPlayerCaptureSnapshot
    {
        public Guid SourceId { get; }
        public ulong SessionId { get; }
        public ulong RunGeneration { get; }
        public double ServerTime { get; }
        public IReadOnlyList<GlobalPlayerCaptureEntry> Entries { get; }
        public GlobalPlayerCaptureSnapshot(Guid sourceId, ulong sessionId, ulong runGeneration, double serverTime, IReadOnlyList<GlobalPlayerCaptureEntry> entries)
        {
            if (entries == null || entries.Count > GlobalPlayerCheckpointSnapshot.MaxPlayers) throw new ArgumentException("Invalid capture collection.");
            var copy = new List<GlobalPlayerCaptureEntry>(entries.Count); foreach (var entry in entries) copy.Add(entry);
            SourceId = sourceId; SessionId = sessionId; RunGeneration = runGeneration; ServerTime = serverTime;
            Entries = new ReadOnlyCollection<GlobalPlayerCaptureEntry>(copy.ToArray());
        }
    }
    /// <summary>Trusted complete-roster source. Concrete NGO implementation is GlobalMotionPlayerCheckpointSource.</summary>
    public interface IGlobalPlayerCheckpointSource
    {
        bool TryCapture(double maximumSampleAge, out GlobalPlayerCaptureSnapshot snapshot, out string error);
        bool IsCurrent(GlobalPlayerCaptureSnapshot snapshot, double maximumSampleAge, out string error);
    }
    public static class GlobalPlayerCapturePolicy
    {
        public const double MaximumSampleAge = 60d;
        public static bool Validate(GlobalPlayerCaptureSnapshot snapshot, double maxAge, out string error)
        {
            error = null;
            if (!GlobalPosition.IsFiniteValue(maxAge) || maxAge <= 0d || maxAge > MaximumSampleAge) return PositionJsonSafety.Fail("explicit_bounded_freshness_required", out error);
            if (snapshot == null || snapshot.SourceId == Guid.Empty || snapshot.SessionId == 0 || snapshot.RunGeneration == 0 ||
                !GlobalPosition.IsFiniteValue(snapshot.ServerTime) || snapshot.ServerTime < 0d || snapshot.Entries.Count == 0)
                return PositionJsonSafety.Fail("active_server_scope_and_nonempty_roster_required", out error);
            var players = new HashSet<string>(StringComparer.Ordinal); var owners = new HashSet<ulong>(); var objects = new HashSet<ulong>(); var leases = new HashSet<Guid>();
            foreach (var entry in snapshot.Entries)
            {
                if (entry == null || !GlobalPlayerPositionRecord.IsValidIdentity(entry.PlayerId) || entry.IdentityLease == Guid.Empty ||
                    !players.Add(entry.PlayerId) || !owners.Add(entry.OwnerClientId) || !objects.Add(entry.Sample.Binding.NetworkObjectId) || !leases.Add(entry.IdentityLease))
                    return PositionJsonSafety.Fail("invalid_or_duplicate_capture_identity", out error);
                var sample = entry.Sample;
                if (!sample.TryValidate(out _) || sample.Binding.SessionId != snapshot.SessionId) return PositionJsonSafety.Fail("invalid_or_cross_session_sample", out error);
                if (entry.InShip || sample.Binding.Space != MotionCoordinateSpace.World) return PositionJsonSafety.Fail("ship_or_parent_local_checkpoint_bridge_missing", out error);
                if (sample.SampleTime > snapshot.ServerTime || snapshot.ServerTime - sample.SampleTime > maxAge) return PositionJsonSafety.Fail("accepted_sample_not_fresh", out error);
                if (entry.Authority == GlobalMotionAuthority.Owner)
                { if (entry.PublisherClientId != entry.OwnerClientId) return PositionJsonSafety.Fail("owner_publisher_mismatch", out error); }
                else if (entry.Authority != GlobalMotionAuthority.Server || entry.PublisherClientId != Unity.Netcode.NetworkManager.ServerClientId)
                    return PositionJsonSafety.Fail("server_authority_or_publisher_invalid", out error);
            }
            return true;
        }
        public static bool SameActorLifetime(MotionStreamBinding expected, MotionStreamBinding current) => expected.IsValid && current.IsValid &&
            expected.SessionId == current.SessionId && expected.NetworkObjectId == current.NetworkObjectId && expected.SpawnGeneration == current.SpawnGeneration;
        public static bool SameEvidence(GlobalPlayerCaptureSnapshot captured, GlobalPlayerCaptureSnapshot current, double maxAge, out string error)
        {
            if (!Validate(captured, maxAge, out error) || !Validate(current, maxAge, out error)) return false;
            if (captured.SourceId != current.SourceId || captured.SessionId != current.SessionId || captured.RunGeneration != current.RunGeneration ||
                current.ServerTime < captured.ServerTime || captured.Entries.Count != current.Entries.Count)
                return PositionJsonSafety.Fail("capture_scope_or_roster_changed", out error);
            var map = new Dictionary<string, GlobalPlayerCaptureEntry>(StringComparer.Ordinal); foreach (var e in current.Entries) map.Add(e.PlayerId, e);
            foreach (var old in captured.Entries)
            {
                if (!map.TryGetValue(old.PlayerId, out var now) || old.IdentityLease != now.IdentityLease || old.OwnerClientId != now.OwnerClientId || old.Authority != now.Authority ||
                    old.PublisherClientId != now.PublisherClientId || old.InShip != now.InShip || !SameAcceptedSample(old.Sample, now.Sample))
                    return PositionJsonSafety.Fail("capture_identity_binding_or_accepted_sample_changed", out error);
            }
            return true;
        }
        /// <summary>A genuine already-captured sample may be followed by normal motion, but not by a new identity/authority/teleport/space or an expired capture.</summary>
        public static bool CanPublishCaptured(GlobalPlayerCaptureSnapshot captured, GlobalPlayerCaptureSnapshot current, double maxAge, out string error)
        {
            if (!Validate(captured, maxAge, out error) || !Validate(current, maxAge, out error)) return false;
            if (captured.SourceId != current.SourceId || captured.SessionId != current.SessionId || captured.RunGeneration != current.RunGeneration ||
                current.ServerTime < captured.ServerTime || captured.Entries.Count != current.Entries.Count)
                return PositionJsonSafety.Fail("capture_scope_or_roster_changed", out error);
            var map = new Dictionary<string, GlobalPlayerCaptureEntry>(StringComparer.Ordinal); foreach (var e in current.Entries) map.Add(e.PlayerId, e);
            foreach (var old in captured.Entries)
            {
                if (!map.TryGetValue(old.PlayerId, out var now) || old.IdentityLease != now.IdentityLease || old.OwnerClientId != now.OwnerClientId || old.Authority != now.Authority ||
                    old.PublisherClientId != now.PublisherClientId || old.Sample.Binding != now.Sample.Binding || current.ServerTime - old.Sample.SampleTime > maxAge)
                    return PositionJsonSafety.Fail("capture_identity_binding_changed_or_capture_expired", out error);
                bool same = old.Sample.Sequence == now.Sample.Sequence;
                if (same ? !SameAcceptedSample(old.Sample, now.Sample) : !GlobalMotionBuffer.IsNewerSequence(now.Sample.Sequence, old.Sample.Sequence) || now.Sample.SampleTime < old.Sample.SampleTime)
                    return PositionJsonSafety.Fail("accepted_sample_rewritten_or_regressed", out error);
            }
            return true;
        }
        public static bool SameAcceptedSample(GlobalMotionSnapshot a, GlobalMotionSnapshot b) => a.TryValidate(out _) && b.TryValidate(out _) && a.Version == b.Version && a.Binding == b.Binding && a.Sequence == b.Sequence &&
            a.SampleTime.Equals(b.SampleTime) && a.WorldPosition == b.WorldPosition && a.ParentLocalPosition.Equals(b.ParentLocalPosition) && a.Rotation.Equals(b.Rotation) && a.Scale.Equals(b.Scale);
    }
}
