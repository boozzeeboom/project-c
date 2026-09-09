using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Netcode;
using ProjectC.Player;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    /// <summary>Explicit trusted identity assignment to one actual NGO connection/player lifetime; not authentication itself.</summary>
    public sealed class GlobalPlayerIdentityLease
    {
        internal Guid SourceId { get; }
        public Guid Id { get; }
        public string PlayerId { get; }
        public ulong ClientId { get; }
        internal GlobalPlayerIdentityLease(Guid sourceId, string playerId, ulong clientId)
        { SourceId = sourceId; Id = Guid.NewGuid(); PlayerId = playerId; ClientId = clientId; }
    }
    /// <summary>
    /// Concrete main-thread NGO reader, dormant until explicitly created by the server coordinator.
    /// Checkpoint position comes ONLY from latest accepted motion, never display interpolation/GetEffectivePosition/Transform.
    /// Native readiness getters can inspect native pose; those checks do not supply persisted coordinates.
    /// </summary>
    public sealed class GlobalMotionPlayerCheckpointSource : IGlobalPlayerCheckpointSource, IDisposable
    {
        private sealed class Registration
        {
            public NetworkClient Client;
            public NetworkObject Object;
            public NetworkPlayer Player;
            public GlobalMotionPoseAdapter Adapter;
            public GlobalPlayerIdentityLease Lease;
            public MotionStreamBinding Lifetime;
            public ulong Session, Run;
        }
        private readonly GlobalMotionWorld _world;
        private readonly Guid _sourceId = Guid.NewGuid();
        private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<ulong, Registration> _registrations = new Dictionary<ulong, Registration>();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<GlobalPlayerCaptureSnapshot, object> _issued = new System.Runtime.CompilerServices.ConditionalWeakTable<GlobalPlayerCaptureSnapshot, object>();
        private bool _busy, _disposed;
        public GlobalMotionPlayerCheckpointSource(GlobalMotionWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.IsOnWorldThread) throw new InvalidOperationException("Create checkpoint source on the initialized world's Unity thread.");
            _world = world;
        }

        public bool TryBindVerifiedIdentity(string playerId, NetworkPlayer player, out GlobalPlayerIdentityLease lease, out string error)
        {
            lease = null; error = null;
            if (!Available(out error) || !GlobalPlayerPositionRecord.IsValidIdentity(playerId)) return PositionJsonSafety.Fail(error ?? "explicit_verified_persistent_identity_required", out error);
            _busy = true;
            try
            {
                if (!Scope(out var manager, out ulong session, out ulong run) || player == null || !player.IsSpawned ||
                    !manager.ConnectedClients.TryGetValue(player.OwnerClientId, out var client) || client.PlayerObject != player.NetworkObject)
                    return PositionJsonSafety.Fail("current_connected_player_required", out error);
                var stale = new List<ulong>();
                foreach (var pair in _registrations) if (!CurrentRegistration(pair.Value, manager, session, run)) stale.Add(pair.Key);
                foreach (ulong id in stale) _registrations.Remove(id);
                if (_registrations.TryGetValue(player.OwnerClientId, out var existing))
                {
                    if (existing.Player == player && existing.Lease.PlayerId == playerId) { lease = existing.Lease; return true; }
                    return PositionJsonSafety.Fail("live_identity_must_be_explicitly_unbound_before_reassignment", out error);
                }
                foreach (var registration in _registrations.Values)
                    if (registration.Lease.PlayerId == playerId) return PositionJsonSafety.Fail("persistent_identity_already_has_live_player", out error);
                if (_registrations.Count >= GlobalPlayerCheckpointSnapshot.MaxPlayers) return PositionJsonSafety.Fail("identity_registry_limit", out error);
                var adapter = player.GetComponent<GlobalMotionPoseAdapter>();
                var proposed = new Registration { Client = client, Object = player.NetworkObject, Player = player, Adapter = adapter, Session = session, Run = run };
                if (!ReadPlayer(proposed, manager, session, run, false, out var sample, out _, out _, out error)) return false;
                proposed.Lifetime = sample.Binding; proposed.Lease = new GlobalPlayerIdentityLease(_sourceId, playerId, player.OwnerClientId);
                if (!ScopeMatches(manager, session, run)) return PositionJsonSafety.Fail("server_scope_changed_during_identity_binding", out error);
                _registrations.Add(player.OwnerClientId, proposed); lease = proposed.Lease; return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("identity_binding:" + e.GetType().Name, out error); }
            finally { _busy = false; }
        }
        public bool TryUnbind(GlobalPlayerIdentityLease lease)
        {
            if (_disposed || _busy || Thread.CurrentThread.ManagedThreadId != _threadId || lease == null || lease.SourceId != _sourceId ||
                !_registrations.TryGetValue(lease.ClientId, out var registration) || !ReferenceEquals(registration.Lease, lease)) return false;
            return _registrations.Remove(lease.ClientId);
        }
        public bool TryCapture(double maximumSampleAge, out GlobalPlayerCaptureSnapshot snapshot, out string error)
        {
            snapshot = null; error = null;
            if (!Available(out error)) return false;
            _busy = true;
            try
            {
                if (!Scope(out var manager, out ulong session, out ulong run)) return PositionJsonSafety.Fail("running_prepared_server_scope_required", out error);
                var roster = new List<NetworkClient>(manager.ConnectedClients.Count);
                foreach (var pair in manager.ConnectedClients)
                {
                    if (pair.Value == null || pair.Value.ClientId != pair.Key || pair.Value.PlayerObject == null)
                        return PositionJsonSafety.Fail("connected_player_roster_incomplete", out error);
                    roster.Add(pair.Value);
                }
                if (roster.Count == 0 || roster.Count > GlobalPlayerCheckpointSnapshot.MaxPlayers) return PositionJsonSafety.Fail("no_active_players_or_roster_limit", out error);
                var entries = new List<GlobalPlayerCaptureEntry>(roster.Count);
                foreach (var client in roster)
                {
                    if (!_registrations.TryGetValue(client.ClientId, out var registration) || !ReferenceEquals(client, registration.Client) || !CurrentRegistration(registration, manager, session, run))
                        return PositionJsonSafety.Fail("verified_identity_mapping_missing_or_stale", out error);
                    if (!ReadPlayer(registration, manager, session, run, true, out var sample, out var authority, out ulong publisher, out error)) return false;
                    entries.Add(new GlobalPlayerCaptureEntry(registration.Lease.PlayerId, registration.Lease.Id, client.ClientId, sample, authority, publisher, false));
                }
                if (!ScopeMatches(manager, session, run) || manager.ConnectedClients.Count != roster.Count) return PositionJsonSafety.Fail("server_scope_or_roster_changed_during_capture", out error);
                foreach (var client in roster)
                    if (!manager.ConnectedClients.TryGetValue(client.ClientId, out var current) || !ReferenceEquals(client, current) ||
                        !_registrations.TryGetValue(client.ClientId, out var registration) || !CurrentRegistration(registration, manager, session, run))
                        return PositionJsonSafety.Fail("connection_or_player_replaced_during_capture", out error);
                // Final pass invokes no participant callbacks: a later player's readiness check must not invalidate an earlier sample unnoticed.
                foreach (var entry in entries)
                {
                    var r = _registrations[entry.OwnerClientId];
                    if (r.Adapter == null || r.Adapter.World != _world || !_world.IsCurrent(r.Adapter.Frame) || !_world.TryGetActor(r.Object.NetworkObjectId, out var bound) || bound != r.Adapter ||
                        !r.Player.isActiveAndEnabled || r.Player.IsInShip || r.Player.CurrentShip != null || !r.Adapter.Transport.TryReadServerAcceptedMotion(out var final, out var authority, out ulong publisher) ||
                        authority != entry.Authority || publisher != entry.PublisherClientId || !GlobalPlayerCapturePolicy.SameAcceptedSample(entry.Sample, final))
                        return PositionJsonSafety.Fail("actor_readiness_or_sample_changed_during_roster_capture", out error);
                }
                var candidate = new GlobalPlayerCaptureSnapshot(_sourceId, session, run, manager.ServerTime.Time, entries);
                if (!GlobalPlayerCapturePolicy.Validate(candidate, maximumSampleAge, out error)) return false;
                _issued.Add(candidate, new object()); snapshot = candidate; return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("native_checkpoint_capture:" + e.GetType().Name, out error); }
            finally { _busy = false; }
        }
        public bool IsCurrent(GlobalPlayerCaptureSnapshot snapshot, double maximumSampleAge, out string error)
        {
            error = null;
            if (snapshot == null || snapshot.SourceId != _sourceId || !_issued.TryGetValue(snapshot, out _)) return PositionJsonSafety.Fail("foreign_or_unissued_capture", out error);
            return TryCapture(maximumSampleAge, out var current, out error) && GlobalPlayerCapturePolicy.CanPublishCaptured(snapshot, current, maximumSampleAge, out error);
        }
        private bool ReadPlayer(Registration r, NetworkManager manager, ulong session, ulong run, bool checkLifetime,
            out GlobalMotionSnapshot sample, out GlobalMotionAuthority authority, out ulong publisher, out string error)
        {
            sample = default; authority = default; publisher = 0; error = null;
            if (r.Player == null || !r.Player.IsSpawned || !r.Player.isActiveAndEnabled || r.Object == null || !r.Object.IsPlayerObject || r.Object != r.Player.NetworkObject ||
                r.Object.NetworkManager != manager || r.Object.OwnerClientId != r.Client.ClientId || r.Client.PlayerObject != r.Object ||
                !manager.ConnectedClients.TryGetValue(r.Client.ClientId, out var client) || !ReferenceEquals(client, r.Client))
                return PositionJsonSafety.Fail("player_connection_object_not_current", out error);
            if (r.Player.IsInShip || r.Player.CurrentShip != null) return PositionJsonSafety.Fail("ship_checkpoint_bridge_missing", out error);
            var adapter = r.Adapter;
            if (adapter == null || !adapter.CoordinatesRequired || adapter.World != _world || !_world.IsCurrent(adapter.Frame) || adapter.Transport == null ||
                adapter.Transport.NetworkObject != r.Object || !_world.TryGetActor(r.Object.NetworkObjectId, out var current) || current != adapter || !adapter.IsBaselineReady)
                return PositionJsonSafety.Fail("global_player_native_readiness_missing", out error);
            if (!ScopeMatches(manager, session, run) || !adapter.Transport.TryReadServerAcceptedMotion(out sample, out authority, out publisher) || sample.Binding.SessionId != session ||
                (checkLifetime && !GlobalPlayerCapturePolicy.SameActorLifetime(r.Lifetime, sample.Binding)) || sample.Binding.Space != MotionCoordinateSpace.World)
                return PositionJsonSafety.Fail("accepted_world_sample_or_player_lifetime_missing", out error);
            // Readiness participants may invoke game callbacks: repeat reference/lifetime checks after them.
            if (r.Object == null || r.Client.PlayerObject != r.Object || r.Object.OwnerClientId != r.Client.ClientId || !r.Object.IsSpawned ||
                !manager.ConnectedClients.TryGetValue(r.Client.ClientId, out client) || !ReferenceEquals(client, r.Client) || r.Player.IsInShip || r.Player.CurrentShip != null ||
                adapter.World != _world || !_world.IsCurrent(adapter.Frame) || !_world.TryGetActor(r.Object.NetworkObjectId, out current) || current != adapter)
                return PositionJsonSafety.Fail("player_changed_during_native_readiness_check", out error);
            return true;
        }
        private bool CurrentRegistration(Registration r, NetworkManager manager, ulong session, ulong run)
        {
            return r.Session == session && r.Run == run && r.Player != null && r.Object != null && r.Object.IsSpawned && r.Object.NetworkManager == manager &&
                r.Player.NetworkObject == r.Object && r.Object.OwnerClientId == r.Client.ClientId && r.Client.PlayerObject == r.Object &&
                manager.ConnectedClients.TryGetValue(r.Client.ClientId, out var actual) && ReferenceEquals(actual, r.Client) && r.Adapter != null && r.Adapter.Transport != null &&
                r.Adapter.Transport.Control.HasStream && GlobalPlayerCapturePolicy.SameActorLifetime(r.Lifetime, r.Adapter.Transport.Control.Baseline.Binding);
        }
        private bool Scope(out NetworkManager manager, out ulong session, out ulong run)
        {
            manager = null; session = 0; run = 0;
            if (_world == null || !_world.TryGetServerCheckpointScope(out session, out run)) return false;
            manager = _world.Manager; return manager != null;
        }
        private bool ScopeMatches(NetworkManager manager, ulong session, ulong run) => Scope(out var now, out ulong currentSession, out ulong currentRun) && now == manager && currentSession == session && currentRun == run;
        private bool Available(out string error)
        {
            error = null;
            if (_disposed || _busy || Thread.CurrentThread.ManagedThreadId != _threadId) return PositionJsonSafety.Fail("capture_source_disposed_reentrant_or_wrong_thread", out error);
            return true;
        }
        public void Dispose()
        {
            if (_busy || Thread.CurrentThread.ManagedThreadId != _threadId) throw new InvalidOperationException("Dispose source on owning thread outside capture.");
            _disposed = true; _registrations.Clear();
        }
    }
}
