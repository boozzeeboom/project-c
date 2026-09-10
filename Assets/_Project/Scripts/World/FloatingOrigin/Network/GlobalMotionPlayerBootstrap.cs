using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using ProjectC.Player;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Explicit player-only bootstrap. Requires a real prepared-content/persistence source; has no default origin/spawn.
    /// Not installed on current assets. Native bodies/nav, general world spawning, AOI and streaming are NOT implemented here.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-19000)]
    public sealed class GlobalMotionPlayerBootstrap : MonoBehaviour, IGlobalMotionSpawnBootstrap, IGlobalMotionSpawnBootstrapLifecycle, IGlobalMotionSceneAdmission
    {
        [SerializeField] private MonoBehaviour _sourceBehaviour;
        private IGlobalMotionPlayerSpawnSource Source => _sourceBehaviour as IGlobalMotionPlayerSpawnSource;
        public bool UsesSource(IGlobalMotionPlayerSpawnSource source) => source != null && ReferenceEquals(Source, source);
        private NetworkManager _manager;
        private GlobalMotionWorld _world;
        private GlobalSceneNativeExecutor _sceneExecutor;
        public bool CanAcceptScenePeer => _active && _sceneExecutor != null && _sceneExecutor.CanAcceptScenePeer;
        private GameObject _prefab;
        private PlayerHandler _handler;
        private bool _active, _handlerRegistered;
        private readonly Dictionary<int, GlobalMotionSpawnFrame> _frames = new Dictionary<int, GlobalMotionSpawnFrame>();
        private readonly List<int> _ownedFrames = new List<int>();
        private readonly Dictionary<int, GlobalMotionFrame> _frameLeases = new Dictionary<int, GlobalMotionFrame>();
        private readonly GlobalMotionSpawnQueue _queue = new GlobalMotionSpawnQueue();
        private readonly List<GlobalMotionSpawnTicket> _work = new List<GlobalMotionSpawnTicket>();
        private readonly Dictionary<ulong, double> _deadlines = new Dictionary<ulong, double>();
        private double _lastBootstrapWaitLogAt = double.NegativeInfinity;
        private string _lastBootstrapWaitState;

        private readonly HashSet<ulong> _connected = new HashSet<ulong>();
        private readonly Dictionary<GlobalMotionReplicator, Placement> _instances = new Dictionary<GlobalMotionReplicator, Placement>();
        private readonly List<GlobalMotionReplicator> _instanceWork = new List<GlobalMotionReplicator>();
        private const double WaitSeconds = 60d;
        private const int PerFrameBudget = 4;
        private sealed class Placement
        {
            public GlobalMotionSpawnFrame Frame;
            public GlobalMotionPlayerSpawnPlan Plan;
            public GlobalMotionSpawnTicket Ticket;
            public GlobalMotionSpawnSeed Seed;
            public bool ServerFactory, Ready, Failed;
            public double Deadline;
        }
        private sealed class PlayerHandler : NetworkPrefabInstanceHandlerWithData<GlobalMotionSpawnSeed>
        {
            private readonly GlobalMotionPlayerBootstrap _owner;
            public PlayerHandler(GlobalMotionPlayerBootstrap owner) { _owner = owner; }
            public override NetworkObject Instantiate(ulong ownerClientId, Vector3 ignoredPosition, Quaternion ignoredRotation, GlobalMotionSpawnSeed seed)
                => _owner.CreateReplica(ownerClientId, seed);
            public override void Destroy(NetworkObject value)
            { if (value != null) UnityEngine.Object.Destroy(value.gameObject); }
        }

        public bool ValidateNetworkStart(NetworkManager manager, GlobalMotionStartRole role, GlobalMotionNetworkProfile profile, out string error)
        {
            error = null;
            if (_active || manager == null || manager != NetworkManager.Singleton || manager.gameObject != gameObject ||
                _sourceBehaviour == null || !_sourceBehaviour.isActiveAndEnabled || Source == null)
            { error = "explicit_prepared_spawn_source_and_singleton_manager_required"; return false; }
            if (!Source.ValidatePreparedContent(role, profile, out error)) return false;
            var definitions = Source.PreparedFrames;
            if (definitions == null || definitions.Count == 0 || definitions.Count > 32) { error = "invalid_prepared_frame_count"; return false; }
            var ids = new HashSet<int>(); var physics = new HashSet<PhysicsScene>();
            foreach (var frame in definitions)
                if (frame == null || !frame.IsValid || !ids.Add(frame.Id) || !physics.Add(frame.Physics))
                { error = "invalid_duplicate_or_unloaded_prepared_frame"; return false; }
            // Legacy loading interprets local Transform positions as large-world positions. It must be retired externally.
            foreach (var loader in UnityEngine.Object.FindObjectsByType<ProjectC.World.Scene.ClientSceneLoader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (loader.isActiveAndEnabled) { error = "legacy_scene_loader_must_be_retired_by_content_bridge"; return false; }
            var prefab = manager.NetworkConfig.PlayerPrefab;
            if (prefab == null || prefab.GetComponent<NetworkPlayer>() == null || !prefab.GetComponent<NetworkPlayer>().enabled ||
                prefab.GetComponent<CharacterController>() == null || !prefab.GetComponent<CharacterController>().enabled)
            { error = "global_player_prefab_required"; return false; }
            error = GlobalMotionNetworkContract.ValidateLayout(GlobalMotionPrefabInspector.Inspect(prefab, GlobalPrefabRole.Spatial));
            if (error != null) return false;
            var no = prefab.GetComponent<NetworkObject>();
            if (no.ActiveSceneSynchronization || no.SceneMigrationSynchronization || no.DontDestroyWithOwner)
            { error = "player_scene_migration_or_orphan_lifetime_not_supported"; return false; }
            if (prefab.GetComponentsInChildren<NetworkTransform>(true).Length != 0 || prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Joint>(true).Length != 0 || prefab.GetComponentsInChildren<NavMeshAgent>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Collider2D>(true).Length != 0 || prefab.GetComponentsInChildren<Rigidbody2D>(true).Length != 0)
            { error = "player_factory_requires_no_stock_transform_body_joint_or_nav"; return false; }
            // This slice guarantees synchronous first world-baseline placement, not arbitrary native readiness workflows.
            foreach (var script in prefab.GetComponents<MonoBehaviour>())
                if (script is IGlobalMotionActorParticipant && !(script is NetworkPlayer))
                { error = "additional_player_participants_require_extended_spawn_coordinator"; return false; }
            var controller = prefab.GetComponent<CharacterController>();
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
                if (collider != controller) { error = "player_factory_requires_root_controller_only"; return false; }
            var scenes = GetComponent<GlobalSceneNativeExecutor>();
            if (scenes == null || !scenes.isActiveAndEnabled) { error = "prepared_native_scene_executor_missing"; return false; }
            return scenes.ValidatePreparation(manager, profile, definitions, out error);
        }

        public void InstallNetworkStart(NetworkManager manager, GlobalMotionStartRole role, GlobalMotionNetworkProfile profile)
        {
            if (!ValidateNetworkStart(manager, role, profile, out var error)) throw new InvalidOperationException(error);
            _manager = manager; _world = GetComponent<GlobalMotionWorld>(); _prefab = manager.NetworkConfig.PlayerPrefab;
            if (_world == null || !_world.AttachManagerForStartup(manager)) throw new InvalidOperationException("Motion world manager unavailable.");
            _frames.Clear(); foreach (var frame in Source.PreparedFrames) _frames.Add(frame.Id, frame);
            _sceneExecutor = GetComponent<GlobalSceneNativeExecutor>();
            _sceneExecutor.PrepareBeforeNetworkStart(manager, profile, Source.PreparedFrames);
            _handler = new PlayerHandler(this);
            // Add fails without replacing somebody else's handler. Runtime replacement of our exclusive registration is unsupported.
            if (!manager.PrefabHandler.AddHandler(_prefab, _handler)) throw new InvalidOperationException("Player prefab handler already owned.");
            _handlerRegistered = true; _queue.Begin(); _active = true;
        }
        public void ReleaseNetworkStart(NetworkManager manager)
        {
            _active = false; _queue.Clear(); _connected.Clear(); _deadlines.Clear();
            if (_handlerRegistered && manager != null && _prefab != null) manager.PrefabHandler.RemoveHandler(_prefab);
            _handlerRegistered = false; _handler = null;
            foreach (var entry in _instances)
                if (entry.Key != null && !entry.Key.IsSpawned) Destroy(entry.Key.gameObject);
            _instances.Clear();
            if (_world != null) foreach (int id in _ownedFrames) _world.UnregisterFrame(id);
            if (_sceneExecutor != null) _sceneExecutor.Release();
            _sceneExecutor = null;
            _ownedFrames.Clear(); _frameLeases.Clear(); _frames.Clear(); _prefab = null; _manager = null; _world = null;
        }
public void PeerConnected(NetworkManager manager, ulong clientId)
        {
            if (!_active || manager != _manager) throw new InvalidOperationException("Wrong bootstrap lease.");
            if (!manager.IsServer || !_connected.Add(clientId)) return;
            if (!_queue.TryAdd(clientId, out _)) { FailPeer(clientId, "spawn_queue_full"); return; }
            _deadlines[clientId] = Time.realtimeSinceStartupAsDouble + WaitSeconds;
            Debug.Log("[T-FO06G] PeerConnected queued: client=" + clientId + ";deadline=" + WaitSeconds + "s;worldRunning=" +
                (_world != null && _world.IsRunning) + ";scenePrepared=" + (_sceneExecutor != null && _sceneExecutor.HasPreparedPlacement) +
                ";sceneReady=" + CanAcceptScenePeer, this);
            // Host callback may precede OnServerStarted. Processing waits for World.IsRunning, never registers during Validate.
        }
        public void PeerDisconnected(NetworkManager manager, ulong clientId)
        {
            if (manager != _manager) return;
            _connected.Remove(clientId); _queue.Cancel(clientId); _deadlines.Remove(clientId);
            if (Source is IGlobalMotionPlayerSpawnPlanGuard guard)
                try { guard.ReleaseDisconnectedPlayer(clientId); } catch (Exception e) { FailSession("source_disconnect_cleanup_failed"); Debug.LogException(e, this); }
        }

        private bool EnsureFrames()
        {
            if (!_active || _manager == null || !_manager.IsListening || _manager.ShutdownInProgress || _world == null || !_world.IsRunning) return false;
            if (_manager != NetworkManager.Singleton) throw new InvalidOperationException("Singleton manager changed during bootstrap lease.");
            foreach (var definition in _frames.Values)
            {
                if (!definition.IsValid) throw new InvalidOperationException("Prepared frame scene was lost.");
                if (_frameLeases.TryGetValue(definition.Id, out var lease) && !_world.IsCurrent(lease))
                    throw new InvalidOperationException("Prepared frame lease was retired; restart requires fresh preparation.");
                if (!_world.TryGetFrame(definition.Id, out var frame))
                {
                    if (!_world.RegisterFrame(definition.Id, definition.Coordinates, definition.Physics)) throw new InvalidOperationException("Frame registration conflict.");
                    _ownedFrames.Add(definition.Id);
                    _world.TryGetFrame(definition.Id, out frame);
                }
                else if (frame.Coordinates.Origin != definition.Coordinates.Origin || frame.Coordinates.MaxLocalCoordinate != definition.Coordinates.MaxLocalCoordinate || !frame.Physics.Equals(definition.Physics))
                    throw new InvalidOperationException("Prepared frame changed; no implicit rebase permitted.");
                _frameLeases[definition.Id] = frame;
            }
            return true;
        }
private void Update()
        {
            if (!_active || _manager == null || _manager.ShutdownInProgress) return;
            try
            {
                if (!GlobalMotionNetworkStartup.IsInstalled(_manager) || _sourceBehaviour == null || !_sourceBehaviour.isActiveAndEnabled || _world == null || !_world.isActiveAndEnabled)
                { FailSession("spawn_lease_world_or_source_lost"); return; }
                if (_sceneExecutor == null || !_sceneExecutor.HasPreparedPlacement) { FailSession("native_scene_preparation_lost"); return; }
                if (_world == null || !_world.IsRunning)
                {
                    LogBootstrapWait("world_not_running");
                    return;
                }
                if (!EnsureFrames())
                {
                    LogBootstrapWait("frames_not_ready");
                    return;
                }
                if (_manager.IsServer && !CanAcceptScenePeer)
                {
                    CheckQueuedDeadlines();
                    LogBootstrapWait("scene_admission_wait;canAccept=" + CanAcceptScenePeer + ";prepared=" + _sceneExecutor.HasPreparedPlacement);
                    return;
                }
                if (_manager.IsServer)
                {
                    _queue.CopyTo(_work); int budget = PerFrameBudget;
                    foreach (var ticket in _work)
                    {
                        if (!_queue.IsCurrent(ticket)) continue;
                        if (!_manager.ConnectedClients.ContainsKey(ticket.ClientId)) { PeerDisconnected(_manager, ticket.ClientId); continue; }
                        if (!_deadlines.TryGetValue(ticket.ClientId, out var deadline) || Time.realtimeSinceStartupAsDouble > deadline) { FailPeer(ticket.ClientId, "spawn_plan_timeout"); continue; }
                        if (budget <= 0) break;
                        if (!Source.TryGetPlayerPlan(ticket.ClientId, out var plan))
                        {
                            LogBootstrapWait("spawn_plan_unavailable;client=" + ticket.ClientId);
                            continue;
                        }
                        Debug.Log("[T-FO06G] Spawn plan ready: client=" + ticket.ClientId + ";frame=" + plan.FrameId + ";position=" + plan.Position, this);
                        if (!_queue.IsCurrent(ticket)) continue;
                        budget--; SpawnPlayer(ticket, plan);
                    }
                }
                _instanceWork.Clear(); _instanceWork.AddRange(_instances.Keys);
                foreach (var actor in _instanceWork)
                {
                    if (actor == null) { _instances.Remove(actor); continue; }
                    if (!_instances.TryGetValue(actor, out var record) || record.Failed || record.Ready || !actor.IsSpawned) continue;
                    if (Time.realtimeSinceStartupAsDouble > record.Deadline) { FailSession("initial_baseline_timeout"); return; }
                    CompletePlacement(actor, record);
                }
            }
            catch (Exception e) { FailSession("spawn_update:" + e.GetType().Name); Debug.LogException(e, this); }
        }

        private void CheckQueuedDeadlines()
        {
            _queue.CopyTo(_work);
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (var ticket in _work)
                if (_queue.IsCurrent(ticket) && (!_deadlines.TryGetValue(ticket.ClientId, out var deadline) || now > deadline))
                    FailPeer(ticket.ClientId, "spawn_scene_admission_timeout");
        }

        private void LogBootstrapWait(string state)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (string.Equals(_lastBootstrapWaitState, state, StringComparison.Ordinal) && now - _lastBootstrapWaitLogAt < 1d) return;
            _lastBootstrapWaitState = state;
            _lastBootstrapWaitLogAt = now;
            Debug.LogWarning("[T-FO06G] Bootstrap waiting: " + state + ";queue=" + _work.Count + ";worldRunning=" +
                (_world != null && _world.IsRunning) + ";scenePrepared=" + (_sceneExecutor != null && _sceneExecutor.HasPreparedPlacement) +
                ";sceneReady=" + CanAcceptScenePeer, this);
        }
        private void SpawnPlayer(GlobalMotionSpawnTicket ticket, GlobalMotionPlayerSpawnPlan plan)
        {
            Debug.Log("[T-FO06G] SpawnPlayer entered: client=" + ticket.ClientId + ";frame=" + plan.FrameId + ";position=" + plan.Position, this);
            if (_manager == null || !_manager.IsListening || _manager.ShutdownInProgress || _manager != NetworkManager.Singleton ||
                !_queue.IsCurrent(ticket) || !_manager.ConnectedClients.TryGetValue(ticket.ClientId, out var client)) return;
            if (client.PlayerObject != null) { FailPeer(ticket.ClientId, "player_already_exists"); return; }
            if (!_frames.TryGetValue(plan.FrameId, out var frame) || !plan.TryProject(frame.Coordinates, ticket.ClientId, out var local))
            { FailPeer(ticket.ClientId, "invalid_explicit_player_plan"); return; }
            var source = Source;
            if (!GlobalMotionSpawnPlanGuards.Validate(source, ticket.ClientId, plan, out var guardError))
            { GlobalMotionSpawnPlanGuards.Cancel(source, ticket.ClientId, plan); FailPeer(ticket.ClientId, guardError ?? "spawn_plan_guard_refused"); return; }
            var record = new Placement { Frame = frame, Plan = plan, Ticket = ticket, ServerFactory = true, Deadline = Time.realtimeSinceStartupAsDouble + WaitSeconds };
            NetworkObject value = null;
            try
            {
                value = CreateInstance(record, local, plan.Rotation, plan.Scale);
                // Awake/OnEnable may change peer, content or source. Revalidate the original reservation before NGO publication.
                if (!_queue.IsCurrent(ticket) || !ReferenceEquals(Source, source) || !_manager.ConnectedClients.TryGetValue(ticket.ClientId, out var current) ||
                    !ReferenceEquals(current, client) || current.PlayerObject != null || !GlobalMotionSpawnPlanGuards.Validate(source, ticket.ClientId, plan, out guardError))
                    throw new InvalidOperationException(guardError ?? "spawn_plan_changed_during_instantiation");
                value.SpawnAsPlayerObject(ticket.ClientId, false);
                Debug.Log("[T-FO06G] SpawnAsPlayerObject called: client=" + ticket.ClientId + ";object=" + value.name, this);
                if (!value.IsSpawned || record.Failed || !record.Ready || !_queue.IsCurrent(ticket) || !ReferenceEquals(Source, source) ||
                    !_manager.ConnectedClients.TryGetValue(ticket.ClientId, out current) || !ReferenceEquals(current, client) || current.PlayerObject != value)
                    throw new InvalidOperationException("Player spawn did not complete initial placement.");
                if (!GlobalMotionSpawnPlanGuards.Confirm(source, ticket.ClientId, plan, value, out guardError) || !ReferenceEquals(Source, source) || !_queue.Complete(ticket))
                    throw new InvalidOperationException(guardError ?? "spawn_identity_confirmation_failed");
                _deadlines.Remove(ticket.ClientId);
            }
            catch (Exception e)
            {
                GlobalMotionSpawnPlanGuards.Cancel(source, ticket.ClientId, plan);
                if (value != null && !value.IsSpawned) DestroyUnspawned(value);
                else if (value != null && _manager != null && _manager.IsListening && !_manager.ShutdownInProgress) value.Despawn(true);
                FailSession("player_spawn_failed:" + e.GetType().Name); Debug.LogException(e, this);
            }
        }
        private NetworkObject CreateReplica(ulong ownerId, GlobalMotionSpawnSeed seed)
        {
            try
            {
                if (!_active || _manager == null || _manager.IsServer || _sceneExecutor == null || !_sceneExecutor.HasPreparedPlacement || !EnsureFrames() || !seed.IsValid || seed.OwnerId != ownerId ||
                    !Source.TryGetReplicaFrame(seed, out int frameId) || !_frames.TryGetValue(frameId, out var frame) || !frame.Coordinates.TryToLocal(seed.Position, out var local))
                    throw new InvalidOperationException("No prepared local frame for explicit spawn seed.");
                return CreateInstance(new Placement { Frame = frame, Seed = seed, Deadline = Time.realtimeSinceStartupAsDouble + WaitSeconds }, local, seed.Rotation, seed.Scale);
            }
            catch (Exception e) { FailSession("replica_factory:" + e.GetType().Name); Debug.LogException(e, this); return null; }
        }
        private NetworkObject CreateInstance(Placement record, Vector3 local, Quaternion rotation, Vector3 scale)
        {
            GameObject staging = null, instance = null;
            GlobalMotionReplicator transport = null;
            try
            {
                staging = new GameObject("GlobalSpawn_InactiveStaging"); staging.SetActive(false);
                SceneManager.MoveGameObjectToScene(staging, record.Frame.Scene);
                instance = Instantiate(_prefab, staging.transform, false);
                instance.SetActive(false); instance.transform.SetParent(null, false);
                SceneManager.MoveGameObjectToScene(instance, record.Frame.Scene);
                instance.transform.SetPositionAndRotation(local, rotation); instance.transform.localScale = scale;
                var player = instance.GetComponent<NetworkPlayer>(); player.PrepareGlobalInitialSpawn();
                transport = instance.GetComponent<GlobalMotionReplicator>(); _instances.Add(transport, record);
                instance.SetActive(true); // Correct scene/local pose and disabled controller BEFORE Awake/OnEnable.
                return instance.GetComponent<NetworkObject>();
            }
            catch { if (!ReferenceEquals(transport, null)) _instances.Remove(transport); if (instance != null) Destroy(instance); throw; }
            finally { if (staging != null) Destroy(staging); }
        }
        private void DestroyUnspawned(NetworkObject value)
        {
            if (value == null || value.IsSpawned) return;
            var transport = value.GetComponent<GlobalMotionReplicator>();
            if (!ReferenceEquals(transport, null)) _instances.Remove(transport);
            Destroy(value.gameObject);
        }
        private void OnActorPostSpawn(GlobalMotionReplicator actor)
        {
            Debug.Log("[T-FO06G] OnActorPostSpawn entered: object=" + (actor == null ? "<null>" : actor.name), this);
            if (!_active || actor.NetworkManager != _manager || actor.GetComponent<NetworkPlayer>() == null) return;
            if (!_instances.TryGetValue(actor, out var record)) { FailSession("player_spawn_bypassed_global_factory"); return; }
            try
            {
                if (!actor.NetworkObject.IsPlayerObject || !EnsureFrames()) throw new InvalidOperationException("Player post-spawn prerequisites unavailable.");
                var adapter = actor.GetComponent<GlobalMotionPoseAdapter>();
                if (!adapter.Bind(_world, record.Frame.Id)) throw new InvalidOperationException("Initial adapter bind failed.");
                if (record.ServerFactory)
                {
                    if (!_queue.IsCurrent(record.Ticket) || actor.OwnerClientId != record.Ticket.ClientId ||
                        !_world.StartWorldStream(adapter, GlobalMotionAuthority.Owner, record.Plan.Position, record.Plan.Rotation, record.Plan.Scale, record.Plan.OwnerRules))
                        throw new InvalidOperationException("Initial global stream refused.");
                }
                CompletePlacement(actor, record);
                // NGO calls OnNetworkPostSpawn before SendSpawnCallForObject. The initial control/seed are now available to serialization.
                if (record.ServerFactory && (!record.Ready || !RefreshSeed(actor))) throw new InvalidOperationException("Initial seed unavailable.");
            }
            catch (Exception e) { record.Failed = true; FailSession("post_spawn:" + e.GetType().Name); Debug.LogException(e, this); }
        }
        private void CompletePlacement(GlobalMotionReplicator actor, Placement record)
        {
            var adapter = actor.GetComponent<GlobalMotionPoseAdapter>();
            adapter.PrepareBaseline();
            if (!adapter.IsBaselinePlaced)
            {
                Debug.LogWarning("[T-FO06G] Baseline not ready: object=" + actor.name + ";status=" + adapter.Status, this);
                return;
            }
            var binding = actor.Control.Baseline.Binding;
            if (!record.ServerFactory && !record.Seed.Matches(binding, actor.OwnerClientId)) throw new InvalidOperationException("Initial seed lifetime mismatch.");
            if (!actor.GetComponent<NetworkPlayer>().ReleaseGlobalInitialSpawn(binding)) throw new InvalidOperationException("Initial controller gate refused.");
            record.Ready = adapter.PrepareBaseline();
            Debug.Log("[T-FO06G] CompletePlacement finished: object=" + actor.name + ";ready=" + record.Ready + ";baseline=" + adapter.IsBaselineReady, this);
        }
        private bool RefreshSeed(GlobalMotionReplicator actor)
        {
            if (!_active || _manager == null || !_manager.IsServer || !actor.IsSpawned || !_instances.TryGetValue(actor, out var record) || !record.Ready) return false;
            var adapter = actor.GetComponent<GlobalMotionPoseAdapter>();
            if (!adapter.IsBaselineReady || !adapter.TryCaptureWorld(out var position, out var rotation, out var scale)) return false;
            var binding = actor.Control.Baseline.Binding;
            var seed = new GlobalMotionSpawnSeed { Version = GlobalMotionSpawnSeed.CurrentVersion, SessionId = binding.SessionId,
                ObjectId = binding.NetworkObjectId, SpawnGeneration = binding.SpawnGeneration, OwnerId = actor.OwnerClientId, Position = position, Rotation = rotation, Scale = scale };
            if (!seed.IsValid) return false;
            _manager.PrefabHandler.SetInstantiationData(actor.NetworkObject, seed); return true;
        }
        internal static void NotifyPostSpawn(GlobalMotionReplicator actor)
        { if (actor != null && actor.NetworkManager != null) actor.NetworkManager.GetComponent<GlobalMotionPlayerBootstrap>()?.OnActorPostSpawn(actor); }
        internal static void NotifyDespawn(GlobalMotionReplicator actor)
        { if (actor != null && actor.NetworkManager != null) actor.NetworkManager.GetComponent<GlobalMotionPlayerBootstrap>()?._instances.Remove(actor); }
        internal static void RefreshSpawnSeed(GlobalMotionReplicator actor)
        { if (actor != null && actor.NetworkManager != null) actor.NetworkManager.GetComponent<GlobalMotionPlayerBootstrap>()?.RefreshSeed(actor); }
        private void FailPeer(ulong clientId, string reason)
        {
            _queue.Cancel(clientId); _deadlines.Remove(clientId); _connected.Remove(clientId);
            if (_manager != null && _manager.IsServer && clientId != NetworkManager.ServerClientId) _manager.DisconnectClient(clientId, "global-spawn:" + reason);
            else FailSession(reason);
        }
        private void FailSession(string reason)
        { Debug.LogError("[T-FO04G] " + reason, this); if (_manager != null && _manager.IsListening && !_manager.ShutdownInProgress) _manager.Shutdown(); }
        private void OnDisable()
        { if (_active && _manager != null && _manager.IsListening) FailSession("bootstrap_disabled"); }
        private void OnDestroy()
        {
            var manager = _manager;
            if (_active && manager != null && manager.IsListening) FailSession("bootstrap_destroyed");
            ReleaseNetworkStart(manager);
        }
    }
}
