using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    [CreateAssetMenu(menuName = "ProjectC/World/Global Motion Network Profile")]
    public sealed class GlobalMotionNetworkProfile : ScriptableObject
    {
        [Serializable]
        public sealed class PrefabEntry
        {
            public GameObject prefab;
            public GlobalPrefabRole role;
        }
        [SerializeField] private bool _enforceGlobalContracts = false;
        [SerializeField, Tooltip("SHA256 of the reviewed closed-scene layout manifest. Not a runtime-readiness certificate.")]
        private string _sceneLayoutDigest = "";
        [SerializeField] private GlobalMotionSceneCatalog _sceneCatalog;
        [SerializeField] private PrefabEntry[] _prefabs = Array.Empty<PrefabEntry>();
        [SerializeField, Tooltip("Exact prefab registry required by this profile. When set, it replaces " +
            "NetworkConfig.Prefabs.NetworkPrefabsLists for the global start only and is restored on release. " +
            "Leave empty to use the scene configuration unchanged. Does not modify any scene or list asset.")]
        private NetworkPrefabsList[] _registryLists = Array.Empty<NetworkPrefabsList>();
        public bool EnforceGlobalContracts => _enforceGlobalContracts;
        public string SceneLayoutDigest => _sceneLayoutDigest;
        public GlobalMotionSceneCatalog SceneCatalog => _sceneCatalog;
        public PrefabEntry[] Prefabs => _prefabs;
        public NetworkPrefabsList[] RegistryLists => _registryLists;
    }
}
