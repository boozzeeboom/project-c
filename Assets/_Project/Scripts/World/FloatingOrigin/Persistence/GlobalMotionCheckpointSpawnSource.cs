using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Player;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    public sealed class GlobalPlayerConnectionIdentity
    {
        internal Guid Source { get; }
        public Guid LeaseId { get; }
        public string PlayerId { get; }
        public ulong ClientId { get; }
        internal GlobalPlayerConnectionIdentity(Guid source, string playerId, ulong clientId)
        { Source = source; LeaseId = Guid.NewGuid(); PlayerId = playerId; ClientId = clientId; }
    }
    /// <summary>
    /// Concrete source G for an explicitly prepared, fixed set of frames. NO automatic identity, frame loading, native disk path or default spawn.
    /// Configure before network start; trusted auth assigns actual connected peers AFTER World starts; prepare plans after G registers frames.
    /// Remains dormant until explicitly configured and assigned to GlobalMotionPlayerBootstrap. Current gameplay is not configured by this class.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionCheckpointSpawnSource : MonoBehaviour, IGlobalMotionPlayerSpawnSource, IGlobalMotionPlayerSpawnPlanGuard
    {
        private sealed class Entry
        {
            public GlobalPlayerConnectionIdentity Identity;
            public NetworkClient Client;
            public ulong Session, Run;
            public GlobalPlayerSpawnResolution Resolution;
            public CheckpointStoreObservation Storage;
            public GlobalMotionFrame Frame;
            public double Issued, Deadline;
            public bool Completed;
            public GlobalPlayerIdentityLease CaptureLease;
        }
        private NetworkManager _manager;
        private GlobalMotionWorld _world;
        private GlobalPlayerCheckpointRepository _repository;
        private GlobalMotionPlayerCheckpointSource _capture;
        private Guid _configuration;
        private int _threadId, _replicaFrame;
        private bool _configured, _busy, _retire;
        private IReadOnlyList<GlobalMotionSpawnFrame> _frames = Array.Empty<GlobalMotionSpawnFrame>();
        private readonly Dictionary<ulong, Entry> _entries = new Dictionary<ulong, Entry>();
        public IReadOnlyList<GlobalMotionSpawnFrame> PreparedFrames => _frames;
        public GlobalMotionPlayerCheckpointSource CheckpointSource => _configured ? _capture : null;
        private void OnEnable() { _threadId = Thread.CurrentThread.ManagedThreadId; }
        private void OnDisable() => RequestRetirement();
        private void OnDestroy() => RequestRetirement();
        private void OnStopped(bool wasHost) => RequestRetirement();
        private void RequestRetirement() { _configured = false; _retire = true; if (!_busy) Clear(); }
        private void Clear()
        {
            if (_manager != null) { _manager.OnServerStopped -= OnStopped; _manager.OnClientStopped -= OnStopped; }
            var capture = _capture; _capture = null; _entries.Clear(); _frames = Array.Empty<GlobalMotionSpawnFrame>();
            _configured = false; _retire = false; _manager = null; _world = null; _repository = null; _configuration = Guid.Empty;
            // Retirement may be raised from an external capture's readiness callback. Invalidate it now; cleanup is deferred inside C until the callback unwinds.
            if (capture != null && !capture.TryRetire()) Debug.LogError("Checkpoint capture retirement requested off its owner thread.", this);
        }
        private void End() { _busy = false; if (_retire) Clear(); }
        private bool Enter(out string error)
        {
            error = null;
            if (_threadId == 0 || Thread.CurrentThread.ManagedThreadId != _threadId || _busy || !_configured || !isActiveAndEnabled)
                return PositionJsonSafety.Fail("source_unconfigured_disabled_reentrant_or_wrong_thread", out error);
            _busy = true; return true;
        }
        public bool ConfigureForStart(IReadOnlyList<GlobalMotionSpawnFrame> frames, int explicitReplicaFrameId, GlobalPlayerCheckpointRepository serverRepository, out string error)
        {
            error = null;
            if (_threadId == 0 || Thread.CurrentThread.ManagedThreadId != _threadId || _busy || !isActiveAndEnabled)
                return PositionJsonSafety.Fail("configure_on_enabled_source_thread", out error);
            var manager = GetComponent<NetworkManager>(); var world = GetComponent<GlobalMotionWorld>();
            if (manager == null || manager.IsListening || manager.ShutdownInProgress || manager != NetworkManager.Singleton || world == null || !world.IsOnWorldThread)
                return PositionJsonSafety.Fail("configure_before_network_start_on_world_manager", out error);
            if (frames == null || frames.Count == 0 || frames.Count > 32) return PositionJsonSafety.Fail("explicit_prepared_frames_required", out error);
            var ids = new HashSet<int>(); var physics = new HashSet<PhysicsScene>(); var copy = new List<GlobalMotionSpawnFrame>();
            foreach (var frame in frames)
            {
                if (frame == null || !frame.IsValid || !ids.Add(frame.Id) || !physics.Add(frame.Physics)) return PositionJsonSafety.Fail("invalid_duplicate_or_unloaded_prepared_frame", out error);
                copy.Add(frame);
            }
            if (!ids.Contains(explicitReplicaFrameId)) return PositionJsonSafety.Fail("explicit_local_replica_frame_required", out error);
            Clear(); _manager = manager; _world = world; _repository = serverRepository; _replicaFrame = explicitReplicaFrameId;
            _frames = new ReadOnlyCollection<GlobalMotionSpawnFrame>(copy.ToArray()); _configuration = Guid.NewGuid();
            _capture = serverRepository == null ? null : new GlobalMotionPlayerCheckpointSource(world);
            manager.OnServerStopped += OnStopped; manager.OnClientStopped += OnStopped; _configured = true; return true;
        }
        public bool ValidatePreparedContent(GlobalMotionStartRole role, GlobalMotionNetworkProfile profile, out string error)
        {
            error = null;
            if (!_configured || _threadId != Thread.CurrentThread.ManagedThreadId || !isActiveAndEnabled || _manager == null || _manager.IsListening || _world == null ||
                _manager != NetworkManager.Singleton || (role != GlobalMotionStartRole.Client && (_repository == null || _capture == null)))
                return PositionJsonSafety.Fail("configured_prepared_source_and_server_repository_required", out error);
            if (role != GlobalMotionStartRole.Client && role != GlobalMotionStartRole.Host && role != GlobalMotionStartRole.Server) return PositionJsonSafety.Fail("invalid_start_role", out error);
            foreach (var f in _frames) if (f == null || !f.IsValid) return PositionJsonSafety.Fail("prepared_scene_lost", out error);
            var executor = GetComponent<GlobalSceneNativeExecutor>();
            if (executor == null || !executor.isActiveAndEnabled || profile == null) return PositionJsonSafety.Fail("reviewed_native_scene_executor_and_profile_required", out error);
            return executor.ValidatePreparation(_manager, profile, _frames, out error);
        }
        public bool TryAuthorizeVerifiedConnection(ulong clientId, string persistentPlayerId, out GlobalPlayerConnectionIdentity identity, out string error)
        {
            identity = null; if (!Enter(out error)) return false;
            try
            {
                if (!GlobalPlayerPositionRecord.IsValidIdentity(persistentPlayerId) || !Scope(out ulong session, out ulong run) ||
                    !_manager.ConnectedClients.TryGetValue(clientId, out var client) || client == null || client.ClientId != clientId || client.PlayerObject != null)
                    return PositionJsonSafety.Fail("verified_identity_and_connected_unspawned_player_required", out error);
                var stale = new List<ulong>(); foreach (var pair in _entries) if (!ConnectionCurrent(pair.Value, false)) stale.Add(pair.Key);
                foreach (ulong key in stale) { var old = _entries[key]; if (old.CaptureLease != null) _capture?.TryUnbind(old.CaptureLease); _entries.Remove(key); }
                if (_entries.TryGetValue(clientId, out var existing))
                {
                    if (existing.Identity.PlayerId != persistentPlayerId) return PositionJsonSafety.Fail("live_identity_reassignment_forbidden", out error);
                    identity = existing.Identity; return true;
                }
                if (_entries.Count >= GlobalPlayerCheckpointSnapshot.MaxPlayers) return PositionJsonSafety.Fail("identity_capacity", out error);
                foreach (var e in _entries.Values) if (e.Identity.PlayerId == persistentPlayerId) return PositionJsonSafety.Fail("duplicate_live_persistent_identity", out error);
                identity = new GlobalPlayerConnectionIdentity(_configuration, persistentPlayerId, clientId);
                _entries.Add(clientId, new Entry { Identity = identity, Client = client, Session = session, Run = run }); return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("authorize_connection:" + e.GetType().Name, out error); }
            finally { End(); }
        }
        public bool TryPreparePlayerPlan(GlobalPlayerConnectionIdentity identity, int preparedFrameId, Quaternion explicitRotation, Vector3 explicitScale,
            Func<GlobalMotionSnapshot, bool> ownerRules, GlobalPosition? explicitFirstSpawn, double lifetimeSeconds, out GlobalPlayerSpawnResolution resolution, out string error)
        {
            resolution = null; if (!Enter(out error)) return false;
            try
            {
                if (!FindIdentity(identity, out var entry) || !ConnectionCurrent(entry, true) || entry.Completed || _repository == null)
                    return PositionJsonSafety.Fail("current_pre_spawn_identity_required", out error);
                if (!GlobalPosition.IsFiniteValue(lifetimeSeconds) || lifetimeSeconds <= 0 || lifetimeSeconds > 60 || !_world.TryGetFrame(preparedFrameId, out var frame) || !FramePrepared(frame))
                    return PositionJsonSafety.Fail("registered_prepared_frame_and_bounded_plan_lifetime_required", out error);
                var observation = _repository.Inspect();
                if (!GlobalPlayerSpawnPlanResolver.TryResolve(identity.PlayerId, identity.ClientId, observation, frame.Id, frame.Coordinates, explicitRotation, explicitScale,
                    ownerRules, explicitFirstSpawn, Guid.NewGuid(), out var prepared, out error)) return false;
                if (!_repository.IsObservationCurrent(observation, out error) || !ConnectionCurrent(entry, true) || !_world.IsCurrent(frame) || !FramePrepared(frame)) return false;
                double now = Time.realtimeSinceStartupAsDouble;
                entry.Resolution = prepared; entry.Storage = observation; entry.Frame = frame; entry.Issued = now; entry.Deadline = now + lifetimeSeconds;
                resolution = prepared; return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("prepare_spawn_plan:" + e.GetType().Name, out error); }
            finally { End(); }
        }
        public bool TryGetPlayerPlan(ulong approvedClientId, out GlobalMotionPlayerSpawnPlan plan)
        {
            plan = default; if (!Enter(out _)) return false;
            try
            {
                if (!_entries.TryGetValue(approvedClientId, out var entry) || entry.Resolution == null || !ValidateEntry(entry, entry.Resolution.Plan, null, out _)) return false;
                plan = entry.Resolution.Plan; return true;
            }
            finally { End(); }
        }
        public bool ValidatePlayerPlan(ulong clientId, GlobalMotionPlayerSpawnPlan plan, out string error)
        {
            if (!Enter(out error)) return false;
            try { return _entries.TryGetValue(clientId, out var entry) && ValidateEntry(entry, plan, null, out error); }
            finally { End(); }
        }
        public bool ConfirmPlayerSpawn(ulong clientId, GlobalMotionPlayerSpawnPlan plan, NetworkObject player, out string error)
        {
            if (!Enter(out error)) return false;
            try
            {
                if (_capture == null || _repository == null || player == null || !player.IsSpawned || !player.IsPlayerObject || player.OwnerClientId != clientId || player.NetworkManager != _manager ||
                    !_entries.TryGetValue(clientId, out var entry) || !ValidateEntry(entry, plan, player, out error)) return false;
                var networkPlayer = player.GetComponent<NetworkPlayer>(); var transport = player.GetComponent<GlobalMotionReplicator>();
                if (networkPlayer == null || transport == null || !transport.TryReadServerAcceptedMotion(out var accepted, out _, out _) ||
                    accepted.Binding.Space != MotionCoordinateSpace.World || accepted.WorldPosition != plan.Position || !accepted.Rotation.Equals(plan.Rotation) || !accepted.Scale.Equals(plan.Scale))
                    return PositionJsonSafety.Fail("spawned_player_does_not_match_reserved_global_pose", out error);
                if (!_capture.TryBindVerifiedIdentity(entry.Identity.PlayerId, networkPlayer, out var captureLease, out error)) return false;
                entry.CaptureLease = captureLease;
                if (!ValidateEntry(entry, plan, player, out error)) { _capture.TryUnbind(captureLease); entry.CaptureLease = null; return false; }
                entry.Completed = true; return true;
            }
            finally { End(); }
        }
        public void CancelPlayerPlan(ulong clientId, Guid reservationId)
        {
            if (!Enter(out _)) return;
            try
            {
                if (!_entries.TryGetValue(clientId, out var entry) || entry.Resolution == null || !GlobalPlayerSpawnPlanResolver.CanCancel(entry.Completed, entry.Resolution.Plan.ReservationId, reservationId)) return;
                if (entry.CaptureLease != null) _capture?.TryUnbind(entry.CaptureLease);
                entry.CaptureLease = null; entry.Resolution = null; entry.Storage = null; entry.Frame = null; entry.Completed = false;
            }
            finally { End(); }
        }
        public void ReleaseDisconnectedPlayer(ulong clientId)
        {
            if (!Enter(out _)) return;
            try
            {
                if (!_entries.TryGetValue(clientId, out var entry)) return;
                // A delayed ID-only notification must not revoke a still-live/new connection. If NGO hasn't retired its registry entry yet, the next authorize sweep cleans it.
                if (_manager != null && _manager.IsListening && _manager.ConnectedClients.TryGetValue(clientId, out var actual) && ReferenceEquals(actual, entry.Client)) return;
                if (entry.CaptureLease != null) _capture?.TryUnbind(entry.CaptureLease);
                _entries.Remove(clientId);
            }
            finally { End(); }
        }
        public bool TryGetReplicaFrame(GlobalMotionSpawnSeed seed, out int localFrameId)
        {
            localFrameId = 0; if (!Enter(out _)) return false;
            try
            {
                if (_manager == null || !_manager.IsListening || _manager.IsServer || _manager.ShutdownInProgress || _manager != NetworkManager.Singleton ||
                    _world == null || !_world.IsRunning || !GlobalMotionNetworkStartup.IsInstalled(_manager) || !seed.IsValid) return false;
                foreach (var frame in _frames)
                    if (frame.Id == _replicaFrame && frame.IsValid && _world.TryGetFrame(frame.Id, out var registered) && FramePrepared(registered) && frame.Coordinates.TryToLocal(seed.Position, out _))
                    { localFrameId = frame.Id; return true; }
                return false; // Never pick nearest frame or synthesize a new origin to make a seed fit.
            }
            finally { End(); }
        }
        private bool ValidateEntry(Entry entry, GlobalMotionPlayerSpawnPlan plan, NetworkObject expectedPlayer, out string error)
        {
            error = null;
            if (_repository == null || _capture == null || entry.Completed || entry.Resolution == null || !GlobalPlayerSpawnPlanResolver.SamePlan(entry.Resolution.Plan, plan) || !ConnectionCurrent(entry, expectedPlayer == null) ||
                (expectedPlayer != null && entry.Client.PlayerObject != expectedPlayer) || !_world.IsCurrent(entry.Frame) || !FramePrepared(entry.Frame) ||
                !GlobalPlayerSpawnPlanResolver.WithinLease(entry.Issued, entry.Deadline, Time.realtimeSinceStartupAsDouble) || !plan.TryProject(entry.Frame.Coordinates, entry.Identity.ClientId, out _))
                return PositionJsonSafety.Fail("spawn_reservation_scope_frame_or_deadline_changed", out error);
            if (!_repository.IsObservationCurrent(entry.Storage, out error)) return false;
            // Storage adapters are trusted but may fail/re-enter in tests: recheck the live side after I/O too.
            return ConnectionCurrent(entry, expectedPlayer == null) && (expectedPlayer == null || entry.Client.PlayerObject == expectedPlayer) &&
                _world.IsCurrent(entry.Frame) && FramePrepared(entry.Frame) && GlobalPlayerSpawnPlanResolver.WithinLease(entry.Issued, entry.Deadline, Time.realtimeSinceStartupAsDouble);
        }
        private bool Scope(out ulong session, out ulong run)
        { session = 0; run = 0; return _configured && _repository != null && _world != null && _world.TryGetServerCheckpointScope(out session, out run) && _world.Manager == _manager; }
        private bool ConnectionCurrent(Entry entry, bool requireUnspawned) => entry != null && entry.Identity.Source == _configuration && Scope(out ulong session, out ulong run) &&
            session == entry.Session && run == entry.Run && _manager.ConnectedClients.TryGetValue(entry.Identity.ClientId, out var current) && ReferenceEquals(current, entry.Client) &&
            current.ClientId == entry.Identity.ClientId && (!requireUnspawned || current.PlayerObject == null);
        private bool FindIdentity(GlobalPlayerConnectionIdentity identity, out Entry entry)
        { entry = null; return identity != null && identity.Source == _configuration && _entries.TryGetValue(identity.ClientId, out entry) && ReferenceEquals(identity, entry.Identity); }
        private bool FramePrepared(GlobalMotionFrame actual)
        {
            if (actual == null || !_world.IsCurrent(actual)) return false;
            foreach (var f in _frames) if (f.Id == actual.Id) return f.IsValid && f.Coordinates.Origin == actual.Coordinates.Origin &&
                f.Coordinates.MaxLocalCoordinate == actual.Coordinates.MaxLocalCoordinate && f.Physics.Equals(actual.Physics);
            return false;
        }
    }
}
