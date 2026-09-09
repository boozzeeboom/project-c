using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Core.ShipPosition;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    /// <summary>
    /// Explicit opt-in session orchestration. Never installs auth/ConnectionData, picks a save directory, loads scenes or fabricates IDs.
    /// Configure with real prepared content/repository and a trusted Host policy; remote auth submits session-bound actual NetworkClient receipts.
    /// Current legacy game has neither this component nor its configuration installed.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class GlobalMotionSessionCoordinator : MonoBehaviour
    {
        private sealed class Peer
        {
            public NetworkClient Client;
            public GlobalSessionPeerPlan Request;
            public GlobalPlayerConnectionIdentity Identity;
            public GlobalPlayerSpawnResolution Resolution;
            public NetworkObject Player;
        }
        private int _thread;
        private bool _busy, _retire, _stopRequested;
        private Guid _token;
        private ulong _session, _run;
        private NetworkManager _manager;
        private GlobalMotionWorld _world;
        private GlobalMotionCheckpointSpawnSource _source;
        private GlobalPlayerCheckpointRepository _repository;
        private GlobalPlayerCheckpointCapture _capture;
        private GlobalCheckpointSchedule _schedule;
        private GlobalSessionTiming _timing;
        private GlobalSessionPeerPlan _host;
        private GlobalMotionStartRole _role;
        private int _replicaFrame;
        private IReadOnlyList<GlobalMotionSpawnFrame> _frames = Array.Empty<GlobalMotionSpawnFrame>();
        private readonly Dictionary<ulong, Peer> _peers = new Dictionary<ulong, Peer>();
        public GlobalSessionPhase Phase { get; private set; }
        public Guid SessionToken => _token;
        public GlobalSessionSaveStatus LastSaveStatus { get; private set; }
        public string LastError { get; private set; }
        public CheckpointTransactionResult LastTransaction { get; private set; }
        public bool LastStopWasUnplanned { get; private set; }
        public bool OwnsSession(NetworkManager manager) => _manager == manager && (Phase == GlobalSessionPhase.Prepared || Phase == GlobalSessionPhase.Running || Phase == GlobalSessionPhase.Stopping || Phase == GlobalSessionPhase.Faulted);
        private void OnEnable() { _thread = Thread.CurrentThread.ManagedThreadId; }
        private void OnDisable()
        {
            if (_manager != null && _manager.IsListening && OwnsSession(_manager)) EmergencyStop("session_coordinator_disabled");
            Retire();
        }
        private void OnDestroy() => Retire();
        private void Stopped(bool wasHost) { LastStopWasUnplanned = !_stopRequested; Retire(); }
        private void Retire() { Phase = GlobalSessionPhase.Stopped; _retire = true; if (!_busy) Clear(); }
        private void Clear()
        {
            if (_manager != null) { _manager.OnClientConnectedCallback -= Connected; _manager.OnServerStopped -= Stopped; _manager.OnClientStopped -= Stopped; }
            _peers.Clear(); _capture = null; _schedule = null; _repository = null; _source = null; _world = null; _manager = null;
            _frames = Array.Empty<GlobalMotionSpawnFrame>(); _host = null; _timing = null; _token = Guid.Empty; _session = 0; _run = 0; _retire = false;
        }
        private bool Enter(out string error)
        {
            error = null;
            if (_thread == 0 || _thread != Thread.CurrentThread.ManagedThreadId || _busy || !isActiveAndEnabled) return PositionJsonSafety.Fail("session_wrong_thread_disabled_or_reentrant", out error);
            _busy = true; return true;
        }
        private void End() { _busy = false; if (_retire) Clear(); }
        public bool ConfigurePreparedSession(IReadOnlyList<GlobalMotionSpawnFrame> frames, int replicaFrameId, GlobalPlayerCheckpointRepository repository,
            GlobalSessionTiming timing, GlobalSessionPeerPlan explicitTrustedHostPlan, out string error)
        {
            if (!Enter(out error)) return false;
            try
            {
                var manager = GetComponent<NetworkManager>(); var world = GetComponent<GlobalMotionWorld>(); var source = GetComponent<GlobalMotionCheckpointSpawnSource>();
                if (manager == null || manager != NetworkManager.Singleton || manager.IsListening || manager.ShutdownInProgress || world == null || !world.IsOnWorldThread ||
                    source == null || !source.isActiveAndEnabled || timing == null || OwnsSession(manager)) return PositionJsonSafety.Fail("stopped_manager_world_source_and_explicit_timing_required", out error);
                if (frames == null || frames.Count == 0 || frames.Count > 32) return PositionJsonSafety.Fail("prepared_frames_required", out error);
                var copy = new List<GlobalMotionSpawnFrame>(); var ids = new HashSet<int>(); var physics = new HashSet<PhysicsScene>();
                foreach (var f in frames) { if (f == null || !f.IsValid || !ids.Add(f.Id) || !physics.Add(f.Physics)) return PositionJsonSafety.Fail("invalid_prepared_frames", out error); copy.Add(f); }
                if (!ids.Contains(replicaFrameId) || (explicitTrustedHostPlan != null && !ids.Contains(explicitTrustedHostPlan.FrameId))) return PositionJsonSafety.Fail("explicit_route_not_in_prepared_frames", out error);
                Clear(); _manager = manager; _world = world; _source = source; _repository = repository; _timing = timing; _host = explicitTrustedHostPlan;
                _frames = new ReadOnlyCollection<GlobalMotionSpawnFrame>(copy.ToArray()); _replicaFrame = replicaFrameId; _token = Guid.NewGuid(); _stopRequested = false;
                LastSaveStatus = GlobalSessionSaveStatus.None; LastTransaction = null; LastError = null; Phase = GlobalSessionPhase.Configured; return true;
            }
            finally { End(); }
        }
        public bool PrepareNetworkStart(NetworkManager manager, MonoBehaviour bootstrap, GlobalMotionStartRole role, out string error)
        {
            if (!Enter(out error)) return false;
            try
            {
                if (Phase != GlobalSessionPhase.Configured || manager != _manager || manager.IsListening || manager.ShutdownInProgress ||
                    !(bootstrap is GlobalMotionPlayerBootstrap playerBootstrap) || bootstrap.gameObject != gameObject || !bootstrap.isActiveAndEnabled || !playerBootstrap.UsesSource(_source))
                    return PositionJsonSafety.Fail("fresh_session_configuration_and_matching_G_source_required", out error);
                if (role != GlobalMotionStartRole.Client && role != GlobalMotionStartRole.Host && role != GlobalMotionStartRole.Server) return PositionJsonSafety.Fail("invalid_role", out error);
                int legacy = CountLegacyPositionServices();
                if (legacy != 0) return PositionJsonSafety.Fail("legacy_position_services_must_be_absent_not_just_disabled", out error);
                var storeStatus = CheckpointStoreStatus.Empty;
                if (role != GlobalMotionStartRole.Client && _repository != null) storeStatus = _repository.Inspect().Status;
                if (!GlobalSessionPersistencePolicy.ValidateStart(role, legacy, _repository != null, storeStatus, _host != null, out error)) return false;
                if (!_source.ConfigureForStart(_frames, _replicaFrame, role == GlobalMotionStartRole.Client ? null : _repository, out error)) return false;
                _role = role; _manager.OnClientConnectedCallback += Connected; _manager.OnServerStopped += Stopped; _manager.OnClientStopped += Stopped;
                Phase = GlobalSessionPhase.Prepared; return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("session_prepare:" + e.GetType().Name, out error); }
            finally { End(); }
        }
        public bool CancelBeforeStart()
        {
            if (!Enter(out _)) return false;
            try
            {
                if (_manager != null && (_manager.IsListening || _manager.ShutdownInProgress)) return false;
                if (_source != null && !_source.CancelConfigurationBeforeStart()) { LastError = "source_preparation_cancel_refused"; Phase = GlobalSessionPhase.Faulted; return false; }
                Retire(); return true;
            }
            finally { End(); }
        }
        private void Connected(ulong id)
        {
            // Host may connect before World.Started. Identity is queued, not resolved against an unstarted World.
            if (_role != GlobalMotionStartRole.Host || id != NetworkManager.ServerClientId || _host == null || _manager == null) return;
            if (_manager.ConnectedClients.TryGetValue(id, out var client) && !SubmitVerifiedPeer(_token, client, _host, out var error)) EmergencyStop(error);
        }
        public bool SubmitVerifiedPeer(Guid expectedSessionToken, NetworkClient actualPeer, GlobalSessionPeerPlan trustedRequest, out string error)
        {
            if (!Enter(out error)) return false;
            try
            {
                if (expectedSessionToken == Guid.Empty || expectedSessionToken != _token || (Phase != GlobalSessionPhase.Prepared && Phase != GlobalSessionPhase.Running) ||
                    _role == GlobalMotionStartRole.Client || _manager == null || !_manager.IsListening || !_manager.IsServer || _manager.ShutdownInProgress || !GlobalMotionNetworkStartup.IsInstalled(_manager) ||
                    actualPeer == null || trustedRequest == null || !trustedRequest.CanServe(actualPeer.ClientId) || !_manager.ConnectedClients.TryGetValue(actualPeer.ClientId, out var current) ||
                    !GlobalSessionPersistencePolicy.IsCurrentPeerReceipt(_token, expectedSessionToken, current, actualPeer) || actualPeer.PlayerObject != null)
                    return PositionJsonSafety.Fail("verified_current_connection_receipt_required", out error);
                if (_role == GlobalMotionStartRole.Host && actualPeer.ClientId == NetworkManager.ServerClientId && !ReferenceEquals(trustedRequest, _host)) return PositionJsonSafety.Fail("host_policy_cannot_be_reassigned", out error);
                bool route = false; foreach (var f in _frames) if (f.Id == trustedRequest.FrameId) route = true;
                if (!route) return PositionJsonSafety.Fail("unprepared_explicit_peer_route", out error);
                RemoveDisconnected();
                if (_peers.TryGetValue(actualPeer.ClientId, out var existing))
                    return ReferenceEquals(existing.Client, actualPeer) && ReferenceEquals(existing.Request, trustedRequest) || PositionJsonSafety.Fail("live_peer_policy_reassignment_forbidden", out error);
                foreach (var peer in _peers.Values) if (peer.Request.PlayerId == trustedRequest.PlayerId) return PositionJsonSafety.Fail("persistent_identity_already_assigned", out error);
                if (_peers.Count >= 256) return PositionJsonSafety.Fail("session_peer_capacity", out error);
                _peers.Add(actualPeer.ClientId, new Peer { Client = actualPeer, Request = trustedRequest }); return true;
            }
            finally { End(); }
        }
        private void Update()
        {
            if (Phase != GlobalSessionPhase.Prepared && Phase != GlobalSessionPhase.Running) return;
            if (!Enter(out _)) return;
            try
            {
                if (_manager == null || !_manager.IsListening || _manager.ShutdownInProgress) return;
                if (!GlobalSessionPersistencePolicy.MatchesRole(_role, _manager.IsServer, _manager.IsClient, _manager.IsHost)) { EmergencyStop("prepared_role_does_not_match_started_network"); return; }
                if (_manager != NetworkManager.Singleton || !GlobalMotionNetworkStartup.IsInstalled(_manager) || _world == null || !_world.isActiveAndEnabled || _source == null || !_source.isActiveAndEnabled)
                { EmergencyStop("session_runtime_ownership_lost"); return; }
                if (_role == GlobalMotionStartRole.Client) { if (_world.IsRunning) Phase = GlobalSessionPhase.Running; return; }
                if (!_world.TryGetServerCheckpointScope(out ulong session, out ulong run)) return;
                if (_session == 0)
                {
                    _session = session; _run = run; _capture = new GlobalPlayerCheckpointCapture(_source.CheckpointSource, _repository);
                    _schedule = new GlobalCheckpointSchedule(_timing, Time.realtimeSinceStartupAsDouble); Phase = GlobalSessionPhase.Running;
                }
                else if (_session != session || _run != run) { EmergencyStop("server_world_lifetime_changed"); return; }
                RemoveDisconnected();
                // Host connect can precede World startup/registry publication. Use only the already configured trusted Host policy, never an ID derived from zero.
                if (_role == GlobalMotionStartRole.Host && _host != null && !_peers.ContainsKey(NetworkManager.ServerClientId) &&
                    _manager.ConnectedClients.TryGetValue(NetworkManager.ServerClientId, out var hostPeer) && hostPeer.PlayerObject == null)
                {
                    foreach (var peer in _peers.Values) if (peer.Request.PlayerId == _host.PlayerId) { EmergencyStop("host_identity_already_assigned"); return; }
                    _peers.Add(NetworkManager.ServerClientId, new Peer { Client = hostPeer, Request = _host });
                }
                int budget = 4;
                foreach (var peer in new List<Peer>(_peers.Values))
                {
                    if (peer.Resolution != null || budget <= 0) continue;
                    if (!PeerCurrent(peer)) continue;
                    if (!_world.TryGetFrame(peer.Request.FrameId, out _)) continue; // G has not registered this prepared frame yet; no storage retry.
                    budget--;
                    if (peer.Identity == null && !_source.TryAuthorizeVerifiedConnection(peer.Client.ClientId, peer.Request.PlayerId, out peer.Identity, out var bindError))
                    { RejectPeer(peer.Client.ClientId, bindError); return; }
                    if (!_source.TryPreparePlayerPlan(peer.Identity, peer.Request.FrameId, peer.Request.Rotation, peer.Request.Scale, peer.Request.OwnerRules,
                        peer.Request.FirstSpawn, _timing.PlanLifetime, out peer.Resolution, out var planError)) { RejectPeer(peer.Client.ClientId, planError); return; }
                }
                if (_schedule.IsDue(Time.realtimeSinceStartupAsDouble)) SaveCurrent(false);
                else if (_schedule.IsFaulted && LastSaveStatus != GlobalSessionSaveStatus.Blocked) SaveResult(GlobalSessionSaveStatus.Blocked, "checkpoint_clock_or_schedule_fault");
            }
            catch (Exception e) { EmergencyStop("session_update:" + e.GetType().Name); }
            finally { End(); }
        }
        public GlobalSessionSaveStatus TryCheckpointNow(bool explicitRetryAfterFailure, out string error)
        {
            if (!Enter(out error)) return GlobalSessionSaveStatus.Blocked;
            try { var result = SaveCurrent(explicitRetryAfterFailure); error = LastError; return result; }
            finally { End(); }
        }
        private GlobalSessionSaveStatus SaveCurrent(bool explicitRetry)
        {
            if (Phase != GlobalSessionPhase.Running || _manager == null || !_manager.IsServer || !_manager.IsListening || _manager.ShutdownInProgress || _schedule == null ||
                !_world.TryGetServerCheckpointScope(out ulong session, out ulong run) || session != _session || run != _run)
                return SaveResult(GlobalSessionSaveStatus.Blocked, "running_server_scope_required");
            double now = Time.realtimeSinceStartupAsDouble;
            if (explicitRetry && !_schedule.RetryExplicitly(now)) return SaveResult(GlobalSessionSaveStatus.Blocked, "checkpoint_clock_regressed");
            if (_schedule.IsFaulted) return SaveResult(GlobalSessionSaveStatus.Blocked, LastError ?? "checkpoint_fault_requires_explicit_retry");
            if (_manager.ConnectedClients.Count == 0) { _schedule.RecordDeferred(now); return SaveResult(GlobalSessionSaveStatus.NoLivePlayers, "no_live_players_previous_checkpoints_retained"); }
            if (!FullRosterConfirmed()) { _schedule.RecordDeferred(now); return SaveResult(GlobalSessionSaveStatus.Deferred, "complete_verified_ready_roster_required"); }
            if (CountLegacyPositionServices() != 0) { _schedule.RecordFailure(); return SaveResult(GlobalSessionSaveStatus.Blocked, "legacy_position_service_appeared"); }
            var observation = _repository.Inspect();
            if (observation.Status != CheckpointStoreStatus.Ready && observation.Status != CheckpointStoreStatus.Empty)
            { _schedule.RecordFailure(); return SaveResult(GlobalSessionSaveStatus.Blocked, "store_requires_explicit_review_or_recovery"); }
            if (!_capture.TryPrepare(observation, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), _timing.SampleAge, out var batch, out var error))
            { _schedule.RecordDeferred(now); return SaveResult(GlobalSessionSaveStatus.Deferred, error); }
            LastTransaction = _capture.TryCommit(batch);
            if (!GlobalSessionPersistencePolicy.IsSuccessfulCommit(LastTransaction.Status))
            { _schedule.RecordFailure(); return SaveResult(GlobalSessionSaveStatus.Blocked, LastTransaction.Status + ":" + LastTransaction.Error); }
            _schedule.RecordApplied(Time.realtimeSinceStartupAsDouble); return SaveResult(GlobalSessionSaveStatus.Applied, null);
        }
        public bool PrepareStop(bool explicitAbandonCheckpoint, out string error)
        {
            if (!Enter(out error)) return false;
            try
            {
                if (_manager == null || !OwnsSession(_manager)) return PositionJsonSafety.Fail("session_stop_owner_missing", out error);
                GlobalSessionSaveStatus saved;
                if (explicitAbandonCheckpoint) saved = SaveResult(GlobalSessionSaveStatus.Abandoned, "explicit_stop_without_final_checkpoint");
                else if (_role == GlobalMotionStartRole.Client || !AnyLivePlayer()) saved = SaveResult(GlobalSessionSaveStatus.NoLivePlayers, "no_live_player_final_capture_possible_previous_checkpoints_retained");
                else saved = SaveCurrent(false);
                if (!GlobalSessionPersistencePolicy.MayStop(saved, explicitAbandonCheckpoint)) { error = LastError; return false; }
                if (_retire || Phase == GlobalSessionPhase.Stopped || _manager == null) return PositionJsonSafety.Fail("session_retired_during_stop_preparation", out error);
                _stopRequested = true; Phase = GlobalSessionPhase.Stopping; error = LastError; return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("prepare_stop:" + e.GetType().Name, out error); }
            finally { End(); }
        }
        private bool AnyLivePlayer()
        { if (_manager == null || !_manager.IsServer) return false; foreach (var peer in _manager.ConnectedClients.Values) if (peer.PlayerObject != null && peer.PlayerObject.IsSpawned) return true; return false; }
        private bool FullRosterConfirmed()
        {
            if (_source == null || _peers.Count != _manager.ConnectedClients.Count) return false;
            foreach (var pair in _manager.ConnectedClients)
            {
                if (!_peers.TryGetValue(pair.Key, out var peer) || !ReferenceEquals(pair.Value, peer.Client) || peer.Identity == null || peer.Resolution == null ||
                    !_source.TryGetConfirmedPlayer(peer.Identity, peer.Resolution.Plan.ReservationId, out var player)) return false;
                if (peer.Player != null && peer.Player != player) return false; peer.Player = player;
            }
            return true;
        }
        private bool PeerCurrent(Peer peer) => _manager.ConnectedClients.TryGetValue(peer.Client.ClientId, out var actual) && ReferenceEquals(actual, peer.Client);
        private void RemoveDisconnected()
        { var removed = new List<ulong>(); foreach (var pair in _peers) if (!PeerCurrent(pair.Value)) removed.Add(pair.Key); foreach (ulong id in removed) _peers.Remove(id); }
        private void RejectPeer(ulong id, string error)
        {
            LastError = error;
            if (id == NetworkManager.ServerClientId) { EmergencyStop(error); return; }
            _manager.DisconnectClient(id, "global-session:spawn_policy_unavailable");
        }
        private void EmergencyStop(string error)
        {
            LastError = error; LastStopWasUnplanned = true; Phase = GlobalSessionPhase.Faulted;
            var manager = _manager; if (manager != null && manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
        }
        private GlobalSessionSaveStatus SaveResult(GlobalSessionSaveStatus status, string error) { LastSaveStatus = status; LastError = error; return status; }
        private static int CountLegacyPositionServices() => UnityEngine.Object.FindObjectsByType<ShipPositionServer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length +
            UnityEngine.Object.FindObjectsByType<PlayerPositionServer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    }
}
