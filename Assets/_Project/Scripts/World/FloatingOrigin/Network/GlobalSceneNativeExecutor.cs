using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Initial loaded-scene executor only: inactive static content, inactive NON-spatial in-scene services and
    /// reviewed Unmanaged sources that are deliberately left exactly as authored.
    /// No dynamic bodies/nav, spatial scene actors, replacements/exclusions, DDOL migration or streaming transaction.
    /// Never attached or activated automatically. Real prepared-content source remains mandatory in the player bootstrap.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-19500)]
    public sealed class GlobalSceneNativeExecutor : MonoBehaviour
    {
        private sealed class Node
        {
            public GlobalScenePlannedEntry Entry;
            public GlobalSceneSourceMarker Marker;
            public NetworkObject Network;
            public Transform Parent;
            public UnityEngine.SceneManagement.Scene Scene;
            public GlobalMotionSpawnFrame Frame;
            public Vector3 BeforePosition, BeforeScale, LocalTarget;
            public Quaternion BeforeRotation;
            public bool WasActive, Activate, Positioned, Activated, Recorded, Retired;
            public bool Unmanaged;
            public GlobalSceneReceiptToken Receipt;
        }
        private sealed class Preparation
        {
            public GlobalScenePlan Plan;
            public readonly List<Node> Nodes = new List<Node>();
            public readonly Dictionary<string, Node> BySource = new Dictionary<string, Node>(StringComparer.Ordinal);
            public readonly Dictionary<string, UnityEngine.SceneManagement.Scene> Scenes = new Dictionary<string, UnityEngine.SceneManagement.Scene>(StringComparer.Ordinal);
        }
        private NetworkManager _manager;
        private GlobalMotionWorld _world;
        private Preparation _prepared;
        private GlobalSceneLifecycleLedger _ledger;
        private GlobalMotionSession _receiptIssuer;
        private readonly Dictionary<string, GlobalSceneLoadTicket> _loads = new Dictionary<string, GlobalSceneLoadTicket>(StringComparer.Ordinal);
        private bool _installed, _networkRan, _busy, _faulted, _retiring;
        private bool _lastLoggedCanAccept;
        private int _lastLoggedRecordedCount = -1;
        private int _lastLoggedPendingCount = -1;
        private double _lastReadinessLogAt = double.NegativeInfinity;

        private double _startedAt;
        public bool HasPreparedPlacement => _installed && !_faulted;
        public bool CanAcceptScenePeer { get; private set; }

        public bool ValidatePreparation(NetworkManager manager, GlobalMotionNetworkProfile profile, IReadOnlyList<GlobalMotionSpawnFrame> frames, out string error)
        {
            try
            {
                if (!ValidateCatalogedDdolRoots(profile, out var restoreError))
                {
                    error = "scene_preparation:" + restoreError;
                    return false;
                }
                BuildPreparation(manager, profile, frames);
                error = null;
                return true;
            }
            catch (Exception e) { error = "scene_preparation:" + e.Message; return false; }
        }
        private Preparation BuildPreparation(NetworkManager manager, GlobalMotionNetworkProfile profile, IReadOnlyList<GlobalMotionSpawnFrame> frames)
        {
            if (_installed || !isActiveAndEnabled || manager == null || manager != NetworkManager.Singleton || manager.gameObject != gameObject || manager.IsListening || manager.ShutdownInProgress ||
                !manager.NetworkConfig.EnableSceneManagement || profile == null || profile.SceneCatalog == null || frames == null)
                throw new InvalidOperationException("stopped_singleton_scene_management_and_catalog_required");
            if (!GlobalSceneCatalogCompiler.TryCompile(profile.SceneCatalog.Data, out var plan, out var error) || !GlobalSceneCatalogCompiler.TryMatchDigest(plan, profile.SceneLayoutDigest, out _))
                throw new InvalidOperationException(error ?? "catalog_digest_mismatch");
            var result = new Preparation { Plan = plan };
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            var scenePathsByGuid = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var source in profile.SceneCatalog.Data.scenes)
            {
                paths.Add(source.assetPath, source.sceneGuid);
                scenePathsByGuid.Add(source.sceneGuid, source.assetPath);
            }
            var frameMap = new Dictionary<int, GlobalMotionSpawnFrame>();
            foreach (var frame in frames) { if (frame == null || !frame.IsValid) throw new InvalidOperationException("invalid_prepared_frame"); frameMap.Add(frame.Id, frame); }
            var candidates = new HashSet<GameObject>();
            int liveMarkerCount = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                // DDOL is audited separately below through its cataloged source markers. It is not an authored
                // scene instance and must not be treated as an unknown scene in the closed-world scene set.
                if (IsDontDestroyOnLoadScene(scene)) continue;
                if (!scene.isLoaded || string.IsNullOrEmpty(scene.path) || !paths.TryGetValue(scene.path, out string guid))
                    throw new InvalidOperationException("loaded_scene_not_in_saved_catalog_scope:" + DescribeScene(scene));
                result.Scenes.Add(guid, scene);
                foreach (var root in scene.GetRootGameObjects())
                {
                    // The catalog covers every authored marker, including marker-only descendants without a
                    // NetworkObject ancestor. Collect the complete loaded-scene marker set; unknown runtime
                    // markers remain fail-closed through the catalog binding and extra-marker checks below.
                    foreach (var marker in root.GetComponentsInChildren<GlobalSceneSourceMarker>(true))
                        if (candidates.Add(marker.gameObject)) liveMarkerCount++;
                }
            }
            // Bootstrap services may deliberately persist in DDOL. They remain catalog-bound, but are not moved
            // back into the authored scene and are accepted only under the explicit PersistentBootstrapService ownership.
            foreach (var marker in Resources.FindObjectsOfTypeAll<GlobalSceneSourceMarker>())
            {
                if (marker == null || !IsDontDestroyOnLoadScene(marker.gameObject.scene) ||
                    !plan.TryGet(marker.SourceId, out var persistentEntry) || !persistentEntry.IsPersistentBootstrapService)
                    continue;
                if (!scenePathsByGuid.TryGetValue(persistentEntry.SceneGuid, out var authoredPath) || !result.Scenes.ContainsKey(persistentEntry.SceneGuid))
                    throw new InvalidOperationException("persistent_bootstrap_service_scene_not_loaded:" + marker.name + ";path=" + authoredPath);
                if (candidates.Add(marker.gameObject)) liveMarkerCount++;
            }
            foreach (var go in candidates)
            {
                var marker = go.GetComponent<GlobalSceneSourceMarker>();
                if (marker == null || !plan.TryGet(marker.SourceId, out var entry) || !result.Scenes.TryGetValue(entry.SceneGuid, out var authoredScene) || result.BySource.ContainsKey(marker.SourceId))
                    throw new InvalidOperationException("missing_duplicate_wrong_scene_baked_source_marker:" + go.name);
                bool persistentDdol = IsDontDestroyOnLoadScene(go.scene);
                if (persistentDdol != entry.IsPersistentBootstrapService)
                    throw new InvalidOperationException("persistent_bootstrap_service_runtime_location_mismatch:" + go.name);
                var scene = persistentDdol ? go.scene : authoredScene;
                if (!persistentDdol && go.scene != authoredScene)
                    throw new InvalidOperationException("missing_duplicate_wrong_scene_baked_source_marker:" + go.name);
                var no = go.GetComponent<NetworkObject>();
                string ownershipIssue = GlobalSceneExecutionPolicy.ValidateOwnership(new GlobalSceneOwnershipFacts
                {
                    Ownership = entry.Ownership,
                    NetworkObject = no != null,
                    HasRigidbody = go.GetComponentsInChildren<Rigidbody>(true).Length != 0,
                    ScenePath = scenePathsByGuid[entry.SceneGuid]
                });
                if (ownershipIssue != null) throw new InvalidOperationException(ownershipIssue + ":" + go.name);
                frameMap.TryGetValue(marker.FrameId, out var frame);
                bool frameValid = frame != null && frame.Scene == scene && frame.Physics.Equals(scene.GetPhysicsScene());
                if (!entry.IsManaged)
                {
                    // Identity is still bound and verified; the source itself is left untouched.
                    if (entry.Spatial || marker.FrameId != 0) throw new InvalidOperationException("unmanaged_source_must_be_nonspatial_and_frameless:" + go.name);
                    var unmanagedNode = new Node { Entry = entry, Marker = marker, Network = no, Scene = scene,
                        Parent = go.transform.parent, WasActive = go.activeSelf, Activate = false, Unmanaged = true };
                    result.BySource.Add(entry.SourceId, unmanagedNode);
                    result.Nodes.Add(unmanagedNode);
                    continue;
                }
                string issue = GlobalSceneExecutionPolicy.Validate(new GlobalSceneExecutionFacts { Treatment = entry.Treatment, Spatial = entry.Spatial, NetworkObject = no != null,
                    ActiveSelf = go.activeSelf, ActiveInHierarchy = go.activeInHierarchy, ActivateWhenReady = marker.ActivateWhenReady, FrameValid = frameValid, UnsupportedNative = UnsupportedOwnedSubtree(go, entry.Spatial) });
                if (issue != null || (!entry.Spatial && marker.FrameId != 0)) throw new InvalidOperationException(issue ?? "nonspatial_frame_must_be_zero");
                if (no != null && (!no.enabled || !no.InScenePlaced || no.IsSpawned || no.NetworkManager != manager || no.SynchronizeTransform || no.AutoObjectParentSync || no.ActiveSceneSynchronization || no.SceneMigrationSynchronization || no.DontDestroyWithOwner))
                    throw new InvalidOperationException("unsupported_scene_network_identity_sync_or_lifetime");
                var node = new Node { Entry = entry, Marker = marker, Network = no, Scene = scene, Parent = go.transform.parent, Frame = frame,
                    WasActive = go.activeSelf, Activate = marker.ActivateWhenReady, BeforePosition = go.transform.localPosition, BeforeRotation = go.transform.localRotation, BeforeScale = go.transform.localScale };
                if (entry.Spatial && (entry.PoseKind != GlobalScenePoseKind.World || !frame.Coordinates.TryToLocal(entry.WorldPosition, out node.LocalTarget)))
                    throw new InvalidOperationException("invalid_explicit_static_world_pose");
                result.BySource.Add(entry.SourceId, node);
            }
            if (liveMarkerCount != plan.SpawnOrder.Count || result.BySource.Count != plan.SpawnOrder.Count)
                throw new InvalidOperationException("catalog_markers_bound_mismatch:catalog=" + plan.SpawnOrder.Count + ";markers=" + liveMarkerCount + ";bound=" + result.BySource.Count);
            foreach (var entry in plan.SpawnOrder)
            {
                if (!result.Scenes.ContainsKey(entry.SceneGuid)) continue;
                if (!result.BySource.TryGetValue(entry.SourceId, out var node))
                    throw new InvalidOperationException("catalog_source_not_bound:" + entry.SourceId + ":catalog=" + plan.SpawnOrder.Count + ";markers=" + liveMarkerCount + ";bound=" + result.BySource.Count);
                var parent = node.Parent;
                while (parent != null && parent.GetComponent<GlobalSceneSourceMarker>() == null) parent = parent.parent;
                string parentId = parent == null ? "" : parent.GetComponent<GlobalSceneSourceMarker>().SourceId;
                if (parentId != entry.ParentSourceId) throw new InvalidOperationException("authored_parent_binding_mismatch");
                if (!entry.IsManaged) continue; // Reviewed as untouched: never placed, activated, spawned or retired.
                if (entry.IsNetwork || entry.Spatial)
                    for (var ancestor = node.Parent; ancestor != null; ancestor = ancestor.parent)
                    {
                        var activation = ancestor.GetComponent<GlobalSceneSourceMarker>();
                        if (!ancestor.gameObject.activeSelf && (activation == null || !activation.ActivateWhenReady))
                            throw new InvalidOperationException("required_source_has_ancestor_that_will_remain_inactive");
                    }
                result.Nodes.Add(node);
            }
            // Includes DDOL and inactive objects. No unknown/foreign/dynamic network object may hide from this boundary.
            foreach (var no in UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Reviewed Unmanaged sources are intentionally untouched by this executor and may be unassigned before NGO starts.
                var bound = no.GetComponent<GlobalSceneSourceMarker>();
                bool catalogBound = false;
                bool unmanaged = false;
                if (bound != null && result.BySource.TryGetValue(bound.SourceId, out var boundNode))
                {
                    catalogBound = true;
                    unmanaged = boundNode.Unmanaged;
                }
                bool candidate = candidates.Contains(no.gameObject);
                bool unassignedReviewedUnmanaged = unmanaged && no.NetworkManager == null && !no.IsSpawned;
                if (!candidate || no.IsSpawned || (no.NetworkManager != manager && !unassignedReviewedUnmanaged))
                    throw new InvalidOperationException(DescribeUncontrolledNetworkSource(no, bound, catalogBound, unmanaged, candidate, manager));
                // Unmanaged sources are reviewed to stay as authored, so their active state is not ours to require.
                if (!unmanaged && (no.gameObject.activeSelf || no.gameObject.activeInHierarchy))
                    throw new InvalidOperationException("uncontrolled_network_source_before_native_sweep:active_state;" + DescribeNetworkSource(no, bound, catalogBound, unmanaged, candidate, manager));
            }
            foreach (var marker in UnityEngine.Object.FindObjectsByType<GlobalSceneSourceMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!candidates.Contains(marker.gameObject)) throw new InvalidOperationException("extra_baked_marker_outside_authored_roots_or_network_objects");
            return result;
        }
        private static string DescribeUncontrolledNetworkSource(NetworkObject no, GlobalSceneSourceMarker marker, bool catalogBound,
            bool unmanaged, bool candidate, NetworkManager expectedManager)
        {
            return "uncontrolled_network_source_before_native_sweep:" + DescribeNetworkSource(no, marker, catalogBound, unmanaged, candidate, expectedManager);
        }

        private static string DescribeNetworkSource(NetworkObject no, GlobalSceneSourceMarker marker, bool catalogBound,
            bool unmanaged, bool candidate, NetworkManager expectedManager)
        {
            var actualManager = no.NetworkManager;
            return "name=" + no.name +
                ";sourceId=" + (marker == null ? "<none>" : marker.SourceId) +
                ";catalogBound=" + catalogBound +
                ";unmanaged=" + unmanaged +
                ";candidate=" + candidate +
                ";networkManager=" + (actualManager == null ? "<null>" : actualManager.name) +
                ";expectedManager=" + (expectedManager == null ? "<null>" : expectedManager.name) +
                ";isSpawned=" + no.IsSpawned +
                ";activeSelf=" + no.gameObject.activeSelf +
                ";activeInHierarchy=" + no.gameObject.activeInHierarchy +
                ";enabled=" + no.enabled +
                ";inScenePlaced=" + no.InScenePlaced +
                ";synchronizeTransform=" + no.SynchronizeTransform +
                ";autoObjectParentSync=" + no.AutoObjectParentSync +
                ";scene=" + no.gameObject.scene.path;
        }

        private static bool UnsupportedOwnedSubtree(GameObject source, bool spatial)
        {
            var stack = new Stack<Transform>(); stack.Push(source.transform);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t != source.transform && t.GetComponent<GlobalSceneSourceMarker>() != null) continue;
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null || c is Rigidbody || c is Joint || c is CharacterController || c is NavMeshAgent || c is Rigidbody2D || c is Collider2D ||
                        c is NetworkTransform || c is IGlobalMotionActorParticipant || c is GlobalMotionReplicator || c is GlobalMotionPoseAdapter) return true;
                    if (spatial && !(c is Transform || c is MeshFilter || c is Renderer || c is Collider || c is GlobalSceneSourceMarker)) return true;
                    if (!spatial && (c is Renderer || c is Collider || c is Camera || c is Light || c is Animator || c is ParticleSystem)) return true;
                }
                for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
            }
            return false;
        }
        public void PrepareBeforeNetworkStart(NetworkManager manager, GlobalMotionNetworkProfile profile, IReadOnlyList<GlobalMotionSpawnFrame> frames)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Native preparation requires user-started Play Mode or player runtime.");
            if (!ValidateCatalogedDdolRoots(profile, out var restoreError)) throw new InvalidOperationException("scene_preparation:" + restoreError);
            _prepared = BuildPreparation(manager, profile, frames); _manager = manager; _world = manager.GetComponent<GlobalMotionWorld>();
            if (_world == null) throw new InvalidOperationException("Motion world missing.");
            _installed = true; _faulted = false; _networkRan = false; _retiring = false; CanAcceptScenePeer = false;
            try
            {
                foreach (var node in _prepared.Nodes)
                {
                    if (!node.Entry.Spatial) continue;
                    RequireIdentity(node);
                    if (node.Marker.gameObject.activeInHierarchy) throw new InvalidOperationException("Source became active before placement.");
                    node.Positioned = true;
                    node.Marker.transform.SetPositionAndRotation(node.LocalTarget, node.Entry.Rotation);
                    node.Marker.transform.localScale = node.Entry.Scale;
                }
                manager.OnServerStarted += Started; manager.OnClientStarted += Started;
            }
            catch { Release(); throw; }
        }
        private void Started()
        {
            if (!_installed || _manager == null || _busy || _faulted) return;
            _networkRan = true; _busy = true;
            try
            {
                if (_ledger == null)
                {
                    // Local bookkeeping lifetime, NOT a wire authority token or a fake NGO owner identity.
                    _startedAt = Time.realtimeSinceStartupAsDouble;
                    _receiptIssuer = GlobalMotionSession.CreateRandom(); _ledger = new GlobalSceneLifecycleLedger(_prepared.Plan, _receiptIssuer.SessionId);
                    foreach (var guid in _prepared.Scenes.Keys)
                    {
                        if (!_ledger.TryBeginLoad(guid, out var ticket)) throw new InvalidOperationException("Initial scene load ticket refused.");
                        _loads.Add(guid, ticket);
                    }
                    if (_manager.IsServer)
                    {
                        // Reuse the client's already prepared scene instances, not Single-mode replacement of their handles.
                        _manager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
                        if (_manager.SceneManager.ClientSynchronizationMode != LoadSceneMode.Additive) throw new InvalidOperationException("Additive synchronization required.");
                    }
                    else
                    {
                        // Client PopulateScenePlacedObjects filters isActiveAndEnabled even when enumerating inactive objects.
                        // Activate AFTER StartClient (no server sweep on this peer), BEFORE incoming scene synchronization.
                        foreach (var node in _prepared.Nodes)
                        {
                            RequireIdentity(node);
                            if (node.Activate && !node.Marker.gameObject.activeSelf) { node.Activated = true; node.Marker.gameObject.SetActive(true); }
                            if (!_installed || _manager == null || _manager.ShutdownInProgress) return;
                            RequireIdentity(node);
                        }
                    }
                }
            }
            catch (Exception e) { Fault(e); }
            finally { _busy = false; }
            Advance();
        }
        private void Update() { if (_installed && _networkRan && !_retiring) Advance(); }
        private bool InitialPreparedSceneSetIsIntact(out string error)
        {
            error = null;
            if (_prepared == null || _prepared.Scenes.Count == 0)
            { error = "preparation_missing"; return false; }
            foreach (var expected in _prepared.Scenes)
                if (!expected.Value.IsValid() || !expected.Value.isLoaded)
                { error = "prepared_scene_unloaded:guid=" + expected.Key + ";" + DescribeScene(expected.Value); return false; }

            int authoredLoadedSceneCount = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                // DDOL is intentionally outside the authored scene set. Its cataloged markers are checked by
                // ValidateCatalogedDdolRoots and by the marker/network census, not by scene-handle identity.
                if (IsDontDestroyOnLoadScene(scene)) continue;
                authoredLoadedSceneCount++;
                // Do not silently accept pathless, pending or unknown scenes. A matching path alone
                // also cannot prove identity: an additive duplicate/reload has a different handle.
                if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                { error = "invalid_or_pending_scene:" + DescribeScene(scene); return false; }
                bool isPrepared = false;
                foreach (var prepared in _prepared.Scenes.Values)
                    if (prepared == scene) { isPrepared = true; break; }
                if (!isPrepared)
                { error = "unexpected_scene_instance:" + DescribeScene(scene); return false; }
            }
            if (authoredLoadedSceneCount != _prepared.Scenes.Count)
            { error = "scene_count_mismatch:expected=" + _prepared.Scenes.Count + ";actualAuthored=" + authoredLoadedSceneCount; return false; }
            return true;
        }
        private static string DescribeScene(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid()) return "handle=" + scene.handle + ",invalid";
            return "name=" + scene.name + ",path=" + scene.path + ",handle=" + scene.handle + ",loaded=" + scene.isLoaded;
        }
        private string DescribeSceneSets()
        {
            var expected = new List<string>();
            if (_prepared != null)
                foreach (var entry in _prepared.Scenes) expected.Add(entry.Key + ":" + DescribeScene(entry.Value));
            var actual = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++) actual.Add(DescribeScene(SceneManager.GetSceneAt(i)));
            return ";expected=[" + string.Join(" | ", expected) + "];actual=[" + string.Join(" | ", actual) + "]";
        }
        private void Advance()
        {
            if (_busy || _faulted || _manager == null || !_manager.IsListening || _manager.ShutdownInProgress || _world == null || !_world.IsRunning) return;
            _busy = true;
            try
            {
                if (!GlobalMotionNetworkStartup.IsInstalled(_manager))
                    throw new InvalidOperationException("startup_lease_missing" + DescribeSceneSets());
                if (_manager != NetworkManager.Singleton)
                    throw new InvalidOperationException("startup_manager_replaced:prepared=" + _manager.name + ";singleton=" +
                        (NetworkManager.Singleton == null ? "<null>" : NetworkManager.Singleton.name) + DescribeSceneSets());
                if (!InitialPreparedSceneSetIsIntact(out var sceneError))
                    throw new InvalidOperationException("initial_scene_set_changed:" + sceneError + DescribeSceneSets());
                foreach (var node in _prepared.Nodes)
                {
                    if (node.Retired) continue;
                    RequireIdentity(node);
                    if (node.Unmanaged) continue;
                    if (node.Recorded)
                    {
                        if (node.Network != null && !node.Network.IsSpawned) { CanAcceptScenePeer = false; continue; }
                        continue;
                    }
                    if (node.Entry.ParentSourceId.Length != 0)
                    {
                        var parentNode = _prepared.BySource[node.Entry.ParentSourceId];
                        if (!parentNode.Unmanaged && !parentNode.Recorded) continue;
                    }
                    if (node.Unmanaged)
                    {
                        if (node.Entry.RequiresNetworkLifecycle)
                        {
                            if (node.Network == null) throw new InvalidOperationException("Scene-owned network gameplay source lost NetworkObject.");
                            if (node.Network.NetworkManager != null && node.Network.NetworkManager != _manager)
                                throw new InvalidOperationException("Scene-owned network gameplay source belongs to a foreign manager.");
                            if (!node.Network.IsSpawned) continue;
                            if (node.Network.NetworkManager != _manager) continue;
                            var issued = _receiptIssuer.Allocate(node.Network.NetworkObjectId);
                            var gameplayLifetime = new GlobalSceneInstanceLifetime(issued.SessionId, issued.NetworkObjectId, issued.SpawnGeneration);
                            if (!_ledger.TryRecordLive(_loads[node.Entry.SceneGuid], node.Entry.SourceId, 0, gameplayLifetime, out node.Receipt, out var gameplayError))
                                throw new InvalidOperationException(gameplayError);
                        }
                        else
                        {
                            if (!_ledger.TryRecordLive(_loads[node.Entry.SceneGuid], node.Entry.SourceId, 0, default, out node.Receipt, out var unmanagedError))
                                throw new InvalidOperationException(unmanagedError);
                        }
                        node.Recorded = true;
                        continue;
                    }
                    if (node.Network != null && !_manager.IsServer && !node.Network.IsSpawned) continue;
                    if (node.Activate && !node.Marker.gameObject.activeSelf) { node.Activated = true; node.Marker.gameObject.SetActive(true); }
                    if (!_installed || _manager == null || _manager.ShutdownInProgress) return;
                    if (node.Network != null && _manager.IsServer)
                    {
                        if (node.Network.IsSpawned) throw new InvalidOperationException("Scene object spawned outside its executor.");
                        node.Network.Spawn(destroyWithScene: true);
                        if (!_installed || _manager == null || _manager.ShutdownInProgress) return;
                        if (!node.Network.IsSpawned || node.Network.NetworkManager != _manager) throw new InvalidOperationException("Native scene spawn failed.");
                    }
                    RequireIdentity(node);
                    var lifetime = default(GlobalSceneInstanceLifetime);
                    if (node.Network != null)
                    {
                        var issued = _receiptIssuer.Allocate(node.Network.NetworkObjectId);
                        lifetime = new GlobalSceneInstanceLifetime(issued.SessionId, issued.NetworkObjectId, issued.SpawnGeneration);
                    }
                    if (!_ledger.TryRecordLive(_loads[node.Entry.SceneGuid], node.Entry.SourceId, node.Entry.Spatial ? node.Marker.FrameId : 0, lifetime, out node.Receipt, out var error))
                        throw new InvalidOperationException(error);
                    node.Recorded = true;
                }
                bool ready = !_retiring;
                int recordedCount = 0;
                int pendingCount = 0;
                int unspawnedCount = 0;
                int retiredCount = 0;
                foreach (var node in _prepared.Nodes)
                {
                    if (node.Unmanaged)
                    {
                        recordedCount++;
                        continue;
                    }

                    if (node.Recorded) recordedCount++;
                    if (!node.Recorded) pendingCount++;
                    if (node.Retired) retiredCount++;
                    if (node.Network != null && !node.Network.IsSpawned) unspawnedCount++;
                    if (!node.Recorded || node.Retired || (node.Network != null && !node.Network.IsSpawned)) ready = false;
                }
                CanAcceptScenePeer = ready;
                LogReadiness(ready, recordedCount, pendingCount, unspawnedCount, retiredCount);
                if (!ready && Time.realtimeSinceStartupAsDouble - _startedAt > 60d) throw new InvalidOperationException("Initial scene receipts timed out.");
            }
            catch (Exception e) { Fault(e); }
            finally { _busy = false; }
        }
        /// <summary>Local initial-scope retirement. Client caller must wait for NGO despawns; caller coordinates scene unload on ALL peers.</summary>
        public bool TryRetireScene(string guid, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(guid) || !_installed || !_networkRan || _faulted || _busy || _manager == null || !_manager.IsListening || _manager.ShutdownInProgress || !_loads.TryGetValue(guid, out var ticket))
            { error = "retirement_unavailable"; return false; }
            // Never deactivate external persistent infrastructure or forget dynamic player/scene additions.
            var scene = _prepared.Scenes[guid];
            // Includes reviewed Unmanaged roots: they are accounted for by the catalog even though nothing manages them.
            var roots = new HashSet<GameObject>(); foreach (var node in _prepared.BySource.Values) if (node.Scene == scene && node.Parent == null) roots.Add(node.Marker.gameObject);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!roots.Contains(root)) { error = "unmanaged_runtime_root_blocks_retirement"; return false; }
                foreach (var no in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    var marker = no.GetComponent<GlobalSceneSourceMarker>();
                    if (marker == null || !_prepared.BySource.TryGetValue(marker.SourceId, out var bound) || bound.Network != no)
                    { error = "unmanaged_runtime_network_descendant_blocks_retirement"; return false; }
                }
            }
            foreach (var node in _prepared.Nodes)
                if (node.Scene == scene && (!node.Recorded ||
                    (node.Entry.RequiresNetworkLifecycle && node.Network != null && node.Network.IsSpawned) ||
                    (!node.Unmanaged && (!node.Activate || (node.Network != null && !_manager.IsServer && node.Network.IsSpawned)))))
                { error = "persistent_unaccounted_or_still_spawned_client_source"; return false; }
            _busy = true; _retiring = true; CanAcceptScenePeer = false;
            try
            {
                foreach (string id in _ledger.GetRetireOrder(ticket))
                {
                    var node = _prepared.BySource[id]; RequireIdentity(node);
                    if (!_ledger.CanRecordRetired(node.Receipt, out error)) throw new InvalidOperationException(error);
                    if (node.Unmanaged)
                    {
                        if (!_ledger.TryRecordRetired(node.Receipt, out error)) throw new InvalidOperationException(error);
                        node.Retired = true;
                        continue;
                    }
                    if (node.Network != null && _manager.IsServer && node.Network.IsSpawned) node.Network.Despawn(destroy: false);
                    if (!_installed || _manager == null || _manager.ShutdownInProgress) { error = "retirement_interrupted_by_shutdown"; return false; }
                    if (node.Network != null && node.Network.IsSpawned) throw new InvalidOperationException("Native object remained spawned.");
                    node.Marker.gameObject.SetActive(false);
                    if (!_installed || _manager == null || _manager.ShutdownInProgress) { error = "retirement_interrupted_by_shutdown"; return false; }
                    RequireIdentity(node);
                    if (!_ledger.TryRecordRetired(node.Receipt, out error)) throw new InvalidOperationException(error);
                    node.Retired = true;
                }
                if (!_ledger.TryEndLoad(ticket)) throw new InvalidOperationException("Unaccounted scene sources remain.");
                _loads.Remove(guid); return true;
            }
            catch (Exception e) { error = e.Message; Fault(e); return false; }
            finally { _busy = false; }
        }
        private static void RequireIdentity(Node node)
        {
            if (node.Marker == null || node.Marker.SourceId != node.Entry.SourceId || node.Marker.transform.parent != node.Parent || node.Marker.gameObject.scene != node.Scene ||
                node.Marker.ActivateWhenReady != node.Activate || (node.Entry.IsNetwork && node.Network == null) ||
                (node.Entry.Spatial && (node.Frame == null || !node.Frame.IsValid || node.Marker.FrameId != node.Frame.Id || node.Frame.Scene != node.Scene)))
                throw new InvalidOperationException("Bound source/frame/scene/parent identity changed.");
        }
        private void LogReadiness(bool ready, int recordedCount, int pendingCount, int unspawnedCount, int retiredCount)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool changed = ready != _lastLoggedCanAccept || recordedCount != _lastLoggedRecordedCount || pendingCount != _lastLoggedPendingCount;
            if (!changed && now - _lastReadinessLogAt < 1d) return;
            _lastLoggedCanAccept = ready;
            _lastLoggedRecordedCount = recordedCount;
            _lastLoggedPendingCount = pendingCount;
            _lastReadinessLogAt = now;
            Debug.Log("[T-FO06G] Native scene readiness: ready=" + ready + ";recorded=" + recordedCount + ";pending=" + pendingCount +
                ";unspawned=" + unspawnedCount + ";retired=" + retiredCount + ";nodes=" + (_prepared == null ? 0 : _prepared.Nodes.Count) +
                ";blocker=" + DescribeReadinessBlocker(), this);
        }
        private string DescribeReadinessBlocker()
        {
            if (_prepared == null) return "<no-preparation>";
            foreach (var node in _prepared.Nodes)
            {
                if (node.Unmanaged) continue;
                bool unspawned = node.Network != null && !node.Network.IsSpawned;
                if (node.Recorded && !node.Retired && !unspawned) continue;
                bool parentRecorded = true;
                if (!string.IsNullOrEmpty(node.Entry.ParentSourceId))
                {
                    parentRecorded = _prepared.BySource.TryGetValue(node.Entry.ParentSourceId, out var parent) &&
                        (parent.Recorded || parent.Unmanaged);
                }
                return "source=" + node.Entry.SourceId + ";scene=" + (node.Marker == null ? "<invalid>" : node.Marker.gameObject.scene.path) + ";marker=" +
                    (node.Marker == null ? "<null>" : node.Marker.name) + ";parent=" +
                    (string.IsNullOrEmpty(node.Entry.ParentSourceId) ? "<root>" : node.Entry.ParentSourceId) +
                    ";parentRecorded=" + parentRecorded + ";recorded=" + node.Recorded + ";retired=" + node.Retired +
                    ";unmanaged=" + node.Unmanaged + ";network=" + (node.Network == null ? "<null>" : node.Network.name) +
                    ";spawned=" + (node.Network != null && node.Network.IsSpawned);
            }
            return "<none>";
        }

        private void Fault(Exception error)
        {
            _faulted = true; CanAcceptScenePeer = false;
            if (_ledger != null) foreach (var ticket in _loads.Values) _ledger.MarkFaulted(ticket);
            Debug.LogException(error, this);
            if (_manager != null && _manager.IsListening && !_manager.ShutdownInProgress) _manager.Shutdown();
        }
        public void Release()
        {
            CanAcceptScenePeer = false;
            if (_manager != null) { _manager.OnServerStarted -= Started; _manager.OnClientStarted -= Started; }
            if (_prepared != null)
                for (int i = _prepared.Nodes.Count - 1; i >= 0; i--)
                {
                    var node = _prepared.Nodes[i]; if (node.Marker == null) continue;
                    try
                    {
                        RequireIdentity(node);
                        if (node.Positioned && GlobalSceneExecutionPolicy.CanRestorePreparation(_networkRan, !node.Marker.gameObject.activeInHierarchy, true))
                        { node.Marker.transform.localPosition = node.BeforePosition; node.Marker.transform.localRotation = node.BeforeRotation; node.Marker.transform.localScale = node.BeforeScale; }
                        else if (_networkRan && node.Activated && (node.Network == null || !node.Network.IsSpawned)) node.Marker.gameObject.SetActive(false);
                    }
                    catch (Exception e) { Debug.LogException(e, this); }
                }
            _installed = false; _prepared = null; _ledger = null; _loads.Clear(); _manager = null; _world = null; _receiptIssuer = null;
        }
        private void OnDisable() { if (_installed && _manager != null && _manager.IsListening) Fault(new InvalidOperationException("Scene executor disabled.")); }
        private void OnDestroy() { if (_installed && _manager != null && _manager.IsListening) Fault(new InvalidOperationException("Scene executor destroyed.")); Release(); }


        private static bool IsDontDestroyOnLoadScene(UnityEngine.SceneManagement.Scene scene)
        {
            // Unity exposes the DDOL pseudo-scene without an asset path; its name is empty in some editor/runtime versions.
            return scene.IsValid() &&
                (string.IsNullOrEmpty(scene.path) || string.Equals(scene.path, "DontDestroyOnLoad", StringComparison.Ordinal)) &&
                (string.IsNullOrEmpty(scene.name) || string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal));
        }

        private static bool ValidateCatalogedDdolRoots(GlobalMotionNetworkProfile profile, out string error)
        {
            error = null;
            if (profile == null || profile.SceneCatalog == null || profile.SceneCatalog.Data == null)
            {
                error = "profile_or_scene_catalog_missing";
                return false;
            }

            var loadedScenesByGuid = new Dictionary<string, UnityEngine.SceneManagement.Scene>(StringComparer.Ordinal);
            var sourceSceneById = new Dictionary<string, string>(StringComparer.Ordinal);
            var entryBySourceId = new Dictionary<string, GlobalSceneEntry>(StringComparer.Ordinal);
            foreach (var scene in profile.SceneCatalog.Data.scenes)
            {
                if (scene == null || string.IsNullOrEmpty(scene.sceneGuid) || string.IsNullOrEmpty(scene.assetPath)) continue;
                var loaded = SceneManager.GetSceneByPath(scene.assetPath);
                if (loaded.IsValid() && loaded.isLoaded) loadedScenesByGuid[scene.sceneGuid] = loaded;
            }
            foreach (var entry in profile.SceneCatalog.Data.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.sourceId) || string.IsNullOrEmpty(entry.sceneGuid)) continue;
                if (sourceSceneById.TryGetValue(entry.sourceId, out var previousGuid) && previousGuid != entry.sceneGuid)
                {
                    error = "catalog_source_spans_scene_guids:" + entry.sourceId;
                    return false;
                }
                sourceSceneById[entry.sourceId] = entry.sceneGuid;
                entryBySourceId[entry.sourceId] = entry;
            }

            var roots = new Dictionary<GameObject, UnityEngine.SceneManagement.Scene>();
            foreach (var marker in Resources.FindObjectsOfTypeAll<GlobalSceneSourceMarker>())
            {
                if (marker == null || !IsDontDestroyOnLoadScene(marker.gameObject.scene)) continue;
                if (!sourceSceneById.TryGetValue(marker.SourceId, out var sceneGuid)) continue;
                if (!entryBySourceId.TryGetValue(marker.SourceId, out var entry) || entry.ownership != GlobalSceneOwnership.PersistentBootstrapService)
                {
                    error = "cataloged_source_in_ddol;restoration_forbidden;sourceId=" + marker.SourceId + ";ownership=" +
                        (entry == null ? GlobalSceneOwnership.Unspecified.ToString() : entry.ownership.ToString());
                    return false;
                }
                if (!loadedScenesByGuid.TryGetValue(sceneGuid, out var targetScene))
                {
                    error = "cataloged_ddol_source_scene_not_loaded;sourceId=" + marker.SourceId + ";sceneGuid=" + sceneGuid;
                    return false;
                }

                var root = marker.transform.root.gameObject;
                if (!IsDontDestroyOnLoadScene(root.scene)) continue;
                if (roots.TryGetValue(root, out var previousScene) && previousScene != targetScene)
                {
                    error = "ddol_root_contains_multiple_cataloged_scene_guids;root=" + root.name +
                        ";firstScene=" + previousScene.path + ";secondScene=" + targetScene.path;
                    return false;
                }
                roots[root] = targetScene;
            }

            return true;
        }
}
}
