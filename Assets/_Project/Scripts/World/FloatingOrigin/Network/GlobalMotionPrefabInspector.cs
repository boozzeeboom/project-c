using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>Reads prefab metadata only. Never calls Initialize, AddComponent, SetActive, parenting or asset save.</summary>
    public static class GlobalMotionPrefabInspector
    {
        public static GlobalPrefabLayout Inspect(GameObject prefab, GlobalPrefabRole role)
        {
            var result = new GlobalPrefabLayout { role = role, orderedBehaviours = Array.Empty<string>() };
            if (prefab == null) return result;
            var root = prefab.GetComponent<NetworkObject>();
            if (root != null)
            {
                result.features |= GlobalPrefabFeatures.RootNetworkObject;
                // Public NGO accessor; GlobalObjectIdHash itself is internal in the installed package.
                result.prefabHash = new NetworkPrefab { Prefab = prefab }.SourcePrefabGlobalObjectIdHash;
                if (root.SynchronizeTransform) result.features |= GlobalPrefabFeatures.StockTransformSync;
                if (root.AutoObjectParentSync) result.features |= GlobalPrefabFeatures.StockParentSync;
            }
            if (prefab.GetComponentsInChildren<NetworkObject>(true).Length != 1) result.features |= GlobalPrefabFeatures.NestedNetworkObject;
            var adapter = prefab.GetComponent<GlobalMotionPoseAdapter>();
            var transport = prefab.GetComponent<GlobalMotionReplicator>();
            if (adapter != null && adapter.enabled) result.features |= GlobalPrefabFeatures.Adapter;
            if (adapter != null && adapter.CoordinatesRequired) result.features |= GlobalPrefabFeatures.CoordinatesRequired;
            if (transport != null && transport.enabled) result.features |= GlobalPrefabFeatures.Replicator;
            if (prefab.GetComponent<ProjectC.Player.NetworkPlayer>() != null) result.features |= GlobalPrefabFeatures.Player;
            if (prefab.GetComponent<ProjectC.Combat.PlayerAttacker>() != null) result.features |= GlobalPrefabFeatures.PlayerAttacker;
            if (prefab.GetComponent<ProjectC.Combat.PlayerTarget>() != null) result.features |= GlobalPrefabFeatures.PlayerTarget;
            if (prefab.GetComponentInChildren<Renderer>(true) != null || prefab.GetComponentInChildren<Collider>(true) != null ||
                prefab.GetComponentInChildren<Collider2D>(true) != null || prefab.GetComponentInChildren<Rigidbody>(true) != null ||
                prefab.GetComponentInChildren<Rigidbody2D>(true) != null || prefab.GetComponentInChildren<NavMeshAgent>(true) != null ||
                prefab.GetComponentInChildren<Camera>(true) != null || prefab.GetComponentInChildren<Light>(true) != null)
                result.features |= GlobalPrefabFeatures.SpatialContent;
            foreach (var script in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null) result.features |= GlobalPrefabFeatures.MissingScript;
                else if (script is IGlobalMotionActorParticipant || script is GlobalMotionPoseAdapter || script is GlobalMotionReplicator)
                    result.features |= GlobalPrefabFeatures.SpatialContent;
            }
            var ordered = new List<string>();
            foreach (var behaviour in prefab.GetComponentsInChildren<NetworkBehaviour>(true))
            {
                if (behaviour == null) { result.features |= GlobalPrefabFeatures.MissingScript; continue; }
                if (behaviour is NetworkTransform && behaviour.enabled) result.features |= GlobalPrefabFeatures.EnabledNetworkTransform;
                for (var type = behaviour.GetType(); type != null; type = type.BaseType)
                    if (type.FullName == "Unity.Netcode.Components.NetworkRigidbodyBase" && behaviour.enabled)
                        result.features |= GlobalPrefabFeatures.EnabledNetworkRigidbody;
                var path = new List<int>(); var t = behaviour.transform;
                while (t != null)
                {
                    if (!t.gameObject.activeSelf) result.features |= GlobalPrefabFeatures.InactiveBehaviourObject;
                    if (t == prefab.transform) break;
                    path.Add(t.GetSiblingIndex()); t = t.parent;
                }
                path.Reverse();
                string slot = path.Count == 0 ? "r" : string.Join("/", path);
                ordered.Add(slot + "|" + behaviour.GetType().Assembly.GetName().Name + ":" + behaviour.GetType().FullName + "|" + (behaviour.enabled ? "1" : "0"));
            }
            result.orderedBehaviours = ordered.ToArray();
            return result;
        }

        public static bool TryBuildHello(NetworkConfig config, GlobalMotionNetworkProfile profile, bool liveRegistry,
            out byte[] hello, out string error)
        {
            hello = null; error = null;
            if (config == null || profile == null || !profile.EnforceGlobalContracts) { error = "global_profile_missing_or_disabled"; return false; }
            if (config.ProtocolVersion != GlobalMotionNetworkContract.ProtocolVersion || !config.ConnectionApproval || !config.ForceSamePrefabs)
            { error = "configure_global_protocol_approval_and_fixed_prefab_catalog_before_start"; return false; }
            if (config.NetworkTopology != NetworkTopologyTypes.ClientServer) { error = "distributed_authority_not_supported"; return false; }
            if (!GlobalMotionNetworkContract.TryParseDigest(profile.SceneLayoutDigest, out var sceneDigest))
            { error = "reviewed_scene_layout_digest_missing"; return false; }
            if (config.PlayerPrefab == null || config.Prefabs == null) { error = "player_or_prefab_registry_missing"; return false; }
            if (profile.Prefabs == null || profile.Prefabs.Length == 0 || profile.Prefabs.Length > GlobalMotionNetworkContract.MaxPrefabs)
            { error = "classified_catalog_missing_or_too_large"; return false; }
            try
            {
                var actual = new Dictionary<uint, GameObject>();
                bool Add(NetworkPrefab entry)
                {
                    if (entry == null || entry.Override != NetworkPrefabOverride.None || entry.Prefab == null) return false;
                    uint hash = entry.SourcePrefabGlobalObjectIdHash;
                    if (hash == 0) return false;
                    if (actual.TryGetValue(hash, out var previous)) return previous == entry.Prefab;
                    actual.Add(hash, entry.Prefab); return actual.Count <= GlobalMotionNetworkContract.MaxPrefabs;
                }
                if (!liveRegistry)
                    foreach (var list in config.Prefabs.NetworkPrefabsLists)
                    {
                        if (list == null) { error = "null_prefab_list"; return false; }
                        foreach (var entry in list.PrefabList) if (!Add(entry)) { error = "invalid_or_overridden_prefab"; return false; }
                    }
                foreach (var entry in config.Prefabs.Prefabs)
                    if (!Add(entry)) { error = "invalid_or_overridden_prefab"; return false; }
                uint playerHash = new NetworkPrefab { Prefab = config.PlayerPrefab }.SourcePrefabGlobalObjectIdHash;
                if (!liveRegistry && !Add(new NetworkPrefab { Prefab = config.PlayerPrefab })) { error = "invalid_player_prefab"; return false; }
                if (!actual.TryGetValue(playerHash, out var registeredPlayer) || registeredPlayer != config.PlayerPrefab)
                { error = "player_missing_from_effective_registry"; return false; }
                if (actual.Count != profile.Prefabs.Length) { error = "registry_does_not_match_classified_catalog"; return false; }
                var layouts = new List<GlobalPrefabLayout>();
                var seen = new HashSet<uint>();
                foreach (var entry in profile.Prefabs)
                {
                    if (entry == null || entry.prefab == null) { error = "null_profile_entry"; return false; }
                    var layout = Inspect(entry.prefab, entry.role);
                    if (!seen.Add(layout.prefabHash) || !actual.TryGetValue(layout.prefabHash, out var registered) || registered != entry.prefab)
                    { error = "profile_entry_not_registered_or_duplicate"; return false; }
                    layouts.Add(layout);
                }
                if (!GlobalMotionNetworkContract.TryBuildDigest(layouts, playerHash, out var digest, out error)) return false;
                hello = GlobalMotionNetworkContract.CreateHello(digest, sceneDigest); return true;
            }
            catch (Exception e) { error = "prefab_preflight_failed:" + e.GetType().Name; return false; }
        }
    }
}
