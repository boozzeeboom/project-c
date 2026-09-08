using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.AI;
using ProjectC.Ship;
using ProjectC.Quests;

namespace ProjectC.PeacefulShip.Crew
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ShipCrewSpawner : NetworkBehaviour
    {
        [Header("Crew data")]
        [SerializeField] private ShipCrewManifest manifest;
        [SerializeField] private Transform crewAnchors;

        [Header("Lifecycle")]
        [SerializeField] private bool spawnOnNetworkSpawn = true;
        [SerializeField] private bool debugLogs = false;

        private readonly Dictionary<string, NetworkObject> _spawnedByMemberId = new Dictionary<string, NetworkObject>();
        private NetworkObject _shipNetworkObject;
        private bool _spawnStarted;

        public ShipCrewManifest Manifest => manifest;
        public bool IsCrewSpawnStarted => _spawnStarted;
        public IReadOnlyDictionary<string, NetworkObject> SpawnedByMemberId => _spawnedByMemberId;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer)
            {
                enabled = false;
                return;
            }

            _shipNetworkObject = NetworkObject;
            if (spawnOnNetworkSpawn)
                StartCoroutine(SpawnWhenReady());
        }

        public override void OnNetworkDespawn()
        {
            _spawnedByMemberId.Clear();
            _shipNetworkObject = null;
            _spawnStarted = false;
            base.OnNetworkDespawn();
        }

        public void EnsureCrewSpawned()
        {
            if (!IsServer || _shipNetworkObject == null || !_shipNetworkObject.IsSpawned)
                return;
            if (manifest == null)
            {
                Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] manifest is null.", this);
                return;
            }

            if (crewAnchors == null)
                crewAnchors = transform.Find("CrewAnchors");

            if (crewAnchors == null)
            {
                Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] CrewAnchors is not assigned and was not found under ShipRoot.", this);
                return;
            }

            _spawnStarted = true;
            CleanupSpawnedMap();

            var existingByNpcId = FindExistingCrewByNpcId();
            if (manifest.members == null)
                return;

            foreach (var member in manifest.members)
            {
                if (!ValidateEntry(member))
                    continue;

                if (_spawnedByMemberId.ContainsKey(member.memberId))
                    continue;

                if (existingByNpcId.TryGetValue(member.npcDefinition.npcId, out var existing) && existing != null && existing.IsSpawned)
                {
                    _spawnedByMemberId[member.memberId] = existing;
                    AttachMemberToShipDeck(member.memberId, existing);
                    if (debugLogs)
                        Debug.Log($"[{nameof(ShipCrewSpawner)}:{name}] Reused existing crew member '{member.memberId}' ({member.npcDefinition.npcId}).", this);
                    continue;
                }

                SpawnMember(member);
            }
        }

        public bool TryGetSpawnedMember(string memberId, out NetworkObject networkObject)
        {
            CleanupSpawnedMap();
            return _spawnedByMemberId.TryGetValue(memberId, out networkObject) && networkObject != null && networkObject.IsSpawned;
        }

        private IEnumerator SpawnWhenReady()
        {
            yield return null;
            EnsureCrewSpawned();
        }

        private bool ValidateEntry(ShipCrewMemberEntry member)
        {
            if (member == null)
            {
                Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] Manifest contains a null member entry.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(member.memberId))
            {
                Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] Crew entry has no memberId.", this);
                return false;
            }

            if (member.npcDefinition == null || member.prefab == null)
            {
                if (member.required)
                    Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] Required member '{member.memberId}' is missing definition or prefab.", this);
                return false;
            }

            var controller = member.prefab.GetComponent<NpcController>();
            if (controller == null || controller.Definition != member.npcDefinition)
            {
                Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] Member '{member.memberId}' prefab/definition mismatch.", this);
                return false;
            }

            if (FindAnchor(member.spawnAnchorId) == null)
            {
                Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] Spawn anchor '{member.spawnAnchorId}' for '{member.memberId}' was not found.", this);
                return false;
            }

            return true;
        }

        private void SpawnMember(ShipCrewMemberEntry member)
        {
            var spawnAnchor = FindAnchor(member.spawnAnchorId);
            if (spawnAnchor == null)
                return;

            var instance = Instantiate(member.prefab, spawnAnchor.position, spawnAnchor.rotation);
            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] Prefab '{member.prefab.name}' has no NetworkObject; destroying instance.", this);
                Destroy(instance);
                return;
            }

            networkObject.Spawn(destroyWithScene: true);
            if (!networkObject.IsSpawned)
            {
                Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] NetworkObject.Spawn failed for '{member.memberId}'.", this);
                Destroy(instance);
                return;
            }

            AttachMemberToShipDeck(member.memberId, networkObject);

            _spawnedByMemberId[member.memberId] = networkObject;
            if (debugLogs)
                Debug.Log($"[{nameof(ShipCrewSpawner)}:{name}] Spawned '{member.memberId}' ({member.npcDefinition.npcId}) at anchor '{member.spawnAnchorId}'.", this);
        }

        private void AttachMemberToShipDeck(string memberId, NetworkObject networkObject)
        {
            if (networkObject == null || !networkObject.IsSpawned || _shipNetworkObject == null)
                return;

            var brain = networkObject.GetComponent<NpcBrain>();
            if (brain == null)
            {
                Debug.LogError($"[{nameof(ShipCrewSpawner)}:{name}] Crew member '{memberId}' has no NpcBrain; cannot attach it to the ship deck.", this);
                return;
            }

            var deckNav = _shipNetworkObject.GetComponent<ShipDeckNav>()
                ?? _shipNetworkObject.GetComponentInChildren<ShipDeckNav>(true);
            brain.AttachToShipDeck(_shipNetworkObject, deckNav);

            if (debugLogs)
                Debug.Log($"[{nameof(ShipCrewSpawner)}:{name}] Requested explicit ship-deck attachment for '{memberId}'.", this);
        }

        private Transform FindAnchor(string anchorId)
        {
            if (crewAnchors == null || string.IsNullOrWhiteSpace(anchorId))
                return null;

            var direct = crewAnchors.Find(anchorId);
            if (direct != null)
                return direct;

            var normalized = NormalizeAnchorId(anchorId);
            for (var i = 0; i < crewAnchors.childCount; i++)
            {
                var child = crewAnchors.GetChild(i);
                if (NormalizeAnchorId(child.name) == normalized)
                    return child;
            }

            return null;
        }

        private static string NormalizeAnchorId(string value)
        {
            return value.Replace("_", "").Replace("-", "").ToLowerInvariant();
        }

        private void CleanupSpawnedMap()
        {
            var stale = new List<string>();
            foreach (var pair in _spawnedByMemberId)
            {
                if (pair.Value == null || !pair.Value.IsSpawned)
                    stale.Add(pair.Key);
            }

            foreach (var key in stale)
                _spawnedByMemberId.Remove(key);
        }

        private Dictionary<string, NetworkObject> FindExistingCrewByNpcId()
        {
            var result = new Dictionary<string, NetworkObject>();
            var controllers = FindObjectsOfType<NpcController>(true);
            foreach (var controller in controllers)
            {
                if (controller == null || controller.transform.parent != transform || string.IsNullOrEmpty(controller.NpcId))
                    continue;

                var networkObject = controller.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned && !result.ContainsKey(controller.NpcId))
                    result.Add(controller.NpcId, networkObject);
            }

            return result;
        }
    }
}
