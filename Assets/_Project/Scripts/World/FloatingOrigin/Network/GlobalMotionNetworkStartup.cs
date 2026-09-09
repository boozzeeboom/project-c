using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionStartRole { Client, Host, Server }

    /// <summary>
    /// Future concrete frame/content/player/parent coordinator. No implementation is installed by T-FO04E.
    /// ValidateNetworkStart is READ-ONLY: the external coordinator must already have prepared scene coverage,
    /// frames and native writers. Validation must not load scenes, freeze actors or start networking.
    /// PeerConnected schedules global-aware placement/spawn; approval never auto-creates a legacy player.
    /// </summary>
    public interface IGlobalMotionSpawnBootstrap
    {
        bool ValidateNetworkStart(NetworkManager manager, GlobalMotionStartRole role, GlobalMotionNetworkProfile profile, out string error);
        void PeerConnected(NetworkManager manager, ulong clientId);
        void PeerDisconnected(NetworkManager manager, ulong clientId);
    }

    /// <summary>Per-manager compatibility lease. Never replaces existing auth/payload, never changes the NGO config hash fields.</summary>
    public sealed class GlobalMotionNetworkStartup
    {
        private static readonly Dictionary<NetworkManager, GlobalMotionNetworkStartup> Sessions = new Dictionary<NetworkManager, GlobalMotionNetworkStartup>();
        private readonly NetworkManager _manager;
        private readonly GlobalMotionNetworkProfile _profile;
        private readonly MonoBehaviour _bootstrapObject;
        private readonly IGlobalMotionSpawnBootstrap _bootstrap;
        private readonly NetworkConfig _config;
        private readonly byte[] _hello;
        private readonly byte[] _previousPayload;
        private readonly List<NetworkPrefabsList> _previousRegistry;
        private readonly List<NetworkPrefabsList> _installedRegistry;
        private readonly GameObject _previousPlayerPrefab;
        private readonly GameObject _installedPlayerPrefab;
        private readonly bool _server;
        private readonly Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> _approval;
        private readonly HashSet<ulong> _approved = new HashSet<ulong>();
        private readonly HashSet<ulong> _notified = new HashSet<ulong>();
        private bool _bootstrapInstalled;
        public bool IsDisposed { get; private set; }

        private GlobalMotionNetworkStartup(NetworkManager manager, GlobalMotionNetworkProfile profile, MonoBehaviour bootstrap,
            GlobalMotionStartRole role, byte[] hello, List<NetworkPrefabsList> previousRegistry, List<NetworkPrefabsList> installedRegistry,
            GameObject previousPlayerPrefab, GameObject installedPlayerPrefab)
        {
            _manager = manager; _profile = profile; _bootstrapObject = bootstrap; _bootstrap = (IGlobalMotionSpawnBootstrap)bootstrap;
            _config = manager.NetworkConfig; _hello = hello; _previousPayload = _config.ConnectionData;
            _previousRegistry = previousRegistry; _installedRegistry = installedRegistry;
            _previousPlayerPrefab = previousPlayerPrefab; _installedPlayerPrefab = installedPlayerPrefab;
            _server = role != GlobalMotionStartRole.Client; _approval = Approve;
        }

        /// <summary>
        /// Applies the profile's declared registry to the live config only. Scene assets and prefab lists are
        /// never written; the previous list instance is returned so release can put it back verbatim.
        /// </summary>
        private static bool TryApplyProfileRegistry(NetworkManager manager, GlobalMotionNetworkProfile profile,
            out List<NetworkPrefabsList> previous, out List<NetworkPrefabsList> installed, out string error)
        {
            previous = null; installed = null; error = null;
            // A missing or disabled profile is reported by the hello builder, not silently overridden here.
            if (profile == null || !profile.EnforceGlobalContracts) return true;
            error = GlobalMotionNetworkContract.ValidateRegistryComposition(profile.RegistryLists, out var replacement);
            if (error != null) return false;
            if (replacement == null) return true;
            var prefabs = manager.NetworkConfig.Prefabs;
            if (prefabs == null) { error = "network_prefab_configuration_missing"; return false; }
            previous = prefabs.NetworkPrefabsLists;
            prefabs.NetworkPrefabsLists = replacement;
            installed = replacement;
            // The aggregated prefab view is a cache rebuilt from the lists; without this it keeps the scene registry.
            prefabs.Initialize();
            return true;
        }

        private static bool TryApplyProfilePlayerPrefab(NetworkManager manager, GlobalMotionNetworkProfile profile,
            out GameObject previous, out GameObject installed, out string error)
        {
            previous = null; installed = null; error = null;
            if (profile == null || !profile.EnforceGlobalContracts || profile.Prefabs == null || profile.Prefabs.Length == 0) return true;
            var spatial = new List<GameObject>();
            foreach (var entry in profile.Prefabs)
                if (entry != null && entry.prefab != null && entry.role == GlobalPrefabRole.Spatial) spatial.Add(entry.prefab);
            if (spatial.Count != 1) { error = "global_profile_requires_one_spatial_player_prefab_for_pilot"; return false; }
            var candidate = spatial[0];
            if (candidate.GetComponent<NetworkObject>() == null) { error = "global_player_prefab_missing_network_object"; return false; }
            previous = manager.NetworkConfig.PlayerPrefab;
            installed = candidate;
            manager.NetworkConfig.PlayerPrefab = candidate;
            return true;
        }
        private static void RestorePlayerPrefab(NetworkManager manager, GameObject previous, GameObject installed)
        {
            if (manager == null || manager.NetworkConfig == null || installed == null) return;
            if (ReferenceEquals(manager.NetworkConfig.PlayerPrefab, installed)) manager.NetworkConfig.PlayerPrefab = previous;
        }
        private static void RestoreRegistry(NetworkManager manager, List<NetworkPrefabsList> previous, List<NetworkPrefabsList> installed)
        {
            if (installed == null || manager == null || manager.NetworkConfig == null) return;
            var prefabs = manager.NetworkConfig.Prefabs;
            if (prefabs == null || !ReferenceEquals(prefabs.NetworkPrefabsLists, installed)) return;
            prefabs.NetworkPrefabsLists = previous ?? new List<NetworkPrefabsList>();
            prefabs.Initialize();
        }
        public static bool IsInstalled(NetworkManager manager) => manager != null && Sessions.TryGetValue(manager, out var gate) && !gate.IsDisposed;

        public static bool TryPrepare(NetworkManager manager, GlobalMotionNetworkProfile profile, MonoBehaviour bootstrap,
            GlobalMotionStartRole role, out GlobalMotionNetworkStartup gate, out string error)
        {
            gate = null; error = null;
            if (manager == null || manager.NetworkConfig == null || manager.IsListening || manager.ShutdownInProgress) { error = "network_must_be_fully_stopped"; return false; }
            if (role != GlobalMotionStartRole.Client && role != GlobalMotionStartRole.Host && role != GlobalMotionStartRole.Server)
            { error = "invalid_start_role"; return false; }
            if (IsInstalled(manager)) { error = "global_startup_already_prepared"; return false; }
            if (manager.ConnectionApprovalCallback != null || (manager.NetworkConfig.ConnectionData != null && manager.NetworkConfig.ConnectionData.Length != 0))
            { error = "existing_approval_or_payload_requires_explicit_auth_composition"; return false; }
            if (bootstrap == null || !bootstrap.isActiveAndEnabled || bootstrap.gameObject != manager.gameObject || !(bootstrap is IGlobalMotionSpawnBootstrap provider))
            { error = "prepared_global_spawn_parent_bootstrap_missing"; return false; }
            if (!(provider is IGlobalMotionSceneAdmission)) { error = "native_scene_admission_contract_missing"; return false; }
            var world = manager.GetComponent<GlobalMotionWorld>();
            if (world == null || !world.isActiveAndEnabled) { error = "global_motion_world_missing"; return false; }
            // T-FO06F: the global path may require a different registry than the scene's legacy one.
            // Applied to the live config only, before hello and the ownership hash, and restored on any exit.
            if (!TryApplyProfileRegistry(manager, profile, out var previousRegistry, out var installedRegistry, out error))
                return false;
            if (!TryApplyProfilePlayerPrefab(manager, profile, out var previousPlayerPrefab, out var installedPlayerPrefab, out error))
            {
                RestoreRegistry(manager, previousRegistry, installedRegistry);
                return false;
            }
            bool registryOwnedByGate = false;
            bool playerPrefabOwnedByGate = false;
            try
            {
                if (!GlobalMotionPrefabInspector.TryBuildHello(manager.NetworkConfig, profile, false, out var hello, out error)) return false;
                var originalConfig = manager.NetworkConfig;
                var originalTransport = originalConfig.NetworkTransport;
                var originalPayload = originalConfig.ConnectionData;
                // cache:false does not initialize or change NGO's cached config hash.
                ulong originalConfigHash = originalConfig.GetConfig(false);
                if (!provider.ValidateNetworkStart(manager, role, profile, out error)) return false;
                if (manager.IsListening || manager.ShutdownInProgress || manager.ConnectionApprovalCallback != null ||
                    !ReferenceEquals(manager.NetworkConfig, originalConfig) ||
                    !ReferenceEquals(originalConfig.NetworkTransport, originalTransport) ||
                    !ReferenceEquals(originalConfig.ConnectionData, originalPayload) || originalConfig.GetConfig(false) != originalConfigHash)
                { error = "bootstrap_changed_network_start_ownership"; return false; }
                if (!GlobalMotionPrefabInspector.TryBuildHello(manager.NetworkConfig, profile, false, out var after, out error) ||
                    !GlobalMotionNetworkContract.ValidateHello(after, hello, out error)) return false;
                gate = new GlobalMotionNetworkStartup(manager, profile, bootstrap, role, hello, previousRegistry, installedRegistry,
                    previousPlayerPrefab, installedPlayerPrefab);
                registryOwnedByGate = true; playerPrefabOwnedByGate = true;
                manager.NetworkConfig.ConnectionData = hello;
                if (gate._server) manager.ConnectionApprovalCallback = gate._approval;
                manager.OnClientConnectedCallback += gate.Connected;
                manager.OnClientDisconnectCallback += gate.Disconnected;
                manager.OnServerStopped += gate.Stopped;
                manager.OnClientStopped += gate.Stopped;
                Sessions.Add(manager, gate);
                if (provider is IGlobalMotionSpawnBootstrapLifecycle lifecycle)
                {
                    gate._bootstrapInstalled = true; // Release also covers a partially installed handler.
                    lifecycle.InstallNetworkStart(manager, role, profile);
                    if (manager.IsListening || manager.ShutdownInProgress || !gate.LocalContractUnchanged(out error))
                    {
                        error = error ?? "bootstrap_started_network_during_install";
                        gate.CancelBeforeStart(); gate = null; return false;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                gate?.CancelBeforeStart(); gate = null; error = "global_startup_prepare_failed:" + e.GetType().Name; return false;
            }
            finally
            {
                // Once the gate exists it owns restoration through Release; avoid restoring twice.
                if (!registryOwnedByGate) RestoreRegistry(manager, previousRegistry, installedRegistry);
                if (!playerPrefabOwnedByGate) RestorePlayerPrefab(manager, previousPlayerPrefab, installedPlayerPrefab);
            }
        }

        private bool LocalContractUnchanged(out string error)
        {
            error = null;
            if (IsDisposed || _bootstrapObject == null || !_bootstrapObject.isActiveAndEnabled || _manager == null ||
                !ReferenceEquals(_manager.NetworkConfig, _config) || !ReferenceEquals(_config.ConnectionData, _hello) ||
                (_server && _manager.ConnectionApprovalCallback != _approval))
            { error = "global_startup_owner_unavailable"; return false; }
            return GlobalMotionPrefabInspector.TryBuildHello(_config, _profile, _manager.IsListening, out var current, out error) &&
                GlobalMotionNetworkContract.ValidateHello(current, _hello, out error);
        }
        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = false; response.CreatePlayerObject = false; response.Pending = false;
            response.PlayerPrefabHash = null; response.Position = null; response.Rotation = null;
            try
            {
                if (!LocalContractUnchanged(out var error) || !GlobalMotionNetworkContract.ValidateHello(request.Payload, _hello, out error))
                { response.Reason = "global-motion:" + error; return; }
                bool hostLocal = _manager.IsHost && request.ClientNetworkId == NetworkManager.ServerClientId;
                if (!(_bootstrap is IGlobalMotionSceneAdmission scenes) || !GlobalSceneExecutionPolicy.CanAcceptPeer(hostLocal, scenes.CanAcceptScenePeer))
                { response.Reason = "global-motion:initial_scene_content_not_ready_retry"; return; }
                _approved.Add(request.ClientNetworkId);
                response.Approved = true; response.Reason = string.Empty;
            }
            catch (Exception e) { response.Reason = "global-motion:approval_failed:" + e.GetType().Name; }
        }
        private void Connected(ulong clientId)
        {
            if (IsDisposed || _manager == null || (!_server && clientId != _manager.LocalClientId) || _notified.Contains(clientId)) return;
            try
            {
                if (!LocalContractUnchanged(out var error) || (_server && !_approved.Remove(clientId)))
                { RejectConnected(clientId, error ?? "approval_missing"); return; }
                if (!_notified.Add(clientId)) return;
                _bootstrap.PeerConnected(_manager, clientId);
            }
            catch (Exception e) { RejectConnected(clientId, "spawn_bootstrap_failed:" + e.GetType().Name); }
        }
        private void RejectConnected(ulong clientId, string reason)
        {
            if (_manager == null) return;
            if (_server && clientId != NetworkManager.ServerClientId) _manager.DisconnectClient(clientId, "global-motion:" + reason);
            else _manager.Shutdown(); // Host approval cannot be rejected by NGO's own host path.
            Debug.LogError("[T-FO04E] Global connection stopped: " + reason);
        }
        private void Disconnected(ulong clientId)
        {
            _approved.Remove(clientId);
            if (!_notified.Remove(clientId) || _bootstrapObject == null) return;
            try { _bootstrap.PeerDisconnected(_manager, clientId); }
            catch (Exception e) { Debug.LogException(e); }
        }
        private void Stopped(bool wasHost) => Release();
        public void CancelBeforeStart()
        {
            if (_manager != null && _manager.IsListening) return;
            Release();
        }
        private void Release()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            if (_bootstrapInstalled)
            {
                _bootstrapInstalled = false;
                try { (_bootstrap as IGlobalMotionSpawnBootstrapLifecycle)?.ReleaseNetworkStart(_manager); }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (_manager != null)
            {
                if (_manager.ConnectionApprovalCallback == _approval) _manager.ConnectionApprovalCallback = null;
                if (ReferenceEquals(_config.ConnectionData, _hello)) _config.ConnectionData = _previousPayload;
                RestorePlayerPrefab(_manager, _previousPlayerPrefab, _installedPlayerPrefab);
                RestoreRegistry(_manager, _previousRegistry, _installedRegistry);
                _manager.OnClientConnectedCallback -= Connected; _manager.OnClientDisconnectCallback -= Disconnected;
                _manager.OnServerStopped -= Stopped; _manager.OnClientStopped -= Stopped;
            }
            if (!ReferenceEquals(_manager, null) && Sessions.TryGetValue(_manager, out var current) && ReferenceEquals(current, this)) Sessions.Remove(_manager);
            _approved.Clear(); _notified.Clear();
        }
    }
}
