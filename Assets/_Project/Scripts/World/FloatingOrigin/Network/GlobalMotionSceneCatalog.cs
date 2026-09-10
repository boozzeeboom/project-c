using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Reviewed disposition of an authored scene source. <see cref="Unmanaged"/> is an explicit, reviewed
    /// decision that the global system does not place, activate, spawn or retire this source: it stays exactly
    /// as authored. It is NOT silent exclusion — the entry still requires a review note and identity binding.
    /// </summary>
    public enum GlobalSceneTreatment : byte { Unreviewed, PreserveContent, SceneNetworkObject, ReplaceWithNetworkPrefab, Exclude, Unmanaged }
    public enum GlobalScenePoseKind : byte { None, World, ParentLocal }
    public enum GlobalSceneOwnership : byte { Unspecified, AuthoredSceneContent, BootstrapService, SceneOwnedNetworkGameplay, ShipOrRigidbodyRoot }

    [Serializable]
    public sealed class GlobalSceneObservation
    {
        public string sourceId = ""; // scene GUID : GlobalObjectId.targetObjectId : targetPrefabId, never runtime NGO id.
        public string parentSourceId = "";
        public bool isRoot, isNetworkObject;
        public string layoutHash = "";
    }
    [Serializable]
    public sealed class GlobalSceneSource
    {
        public string sceneGuid = "", assetPath = "", dependencyHash = "";
        public bool inspectedComplete, saved;
        public GlobalSceneObservation[] observations = Array.Empty<GlobalSceneObservation>();
    }
    [Serializable]
    public sealed class GlobalSceneEntry
    {
        public string sceneGuid = "", sourceId = "", reviewNote = "";
        public GlobalSceneTreatment treatment;
        public GlobalSceneOwnership ownership;
        public bool spatial;
        public uint replacementPrefabHash;
        public GlobalScenePoseKind poseKind;
        public GlobalPosition worldPosition;
        public Vector3 parentLocalPosition;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public string parentSourceId = "";
    }
    [Serializable]
    public sealed class GlobalSceneCatalogData
    {
        public int schemaVersion = 1;
        public string[] expectedSceneGuids = Array.Empty<string>();
        public GlobalSceneSource[] scenes = Array.Empty<GlobalSceneSource>();
        public GlobalSceneEntry[] entries = Array.Empty<GlobalSceneEntry>();
    }
    /// <summary>Reviewed authored-root/network-object catalog only, NOT a native prepared-world or runtime census certificate.</summary>
    [CreateAssetMenu(menuName = "ProjectC/World/Global Motion Scene Catalog")]
    public sealed class GlobalMotionSceneCatalog : ScriptableObject
    {
        [SerializeField] private GlobalSceneCatalogData _data = new GlobalSceneCatalogData();
        public GlobalSceneCatalogData Data => _data;
    }

    /// <summary>Immutable copy; later edits of serialized catalog arrays cannot mutate a running lifecycle plan.</summary>
    public sealed class GlobalScenePlannedEntry
    {
        public string SceneGuid { get; }
        public string SourceId { get; }
        public GlobalSceneTreatment Treatment { get; }
        public GlobalSceneOwnership Ownership { get; }
        public bool Spatial { get; }
        public bool IsNetwork => Treatment == GlobalSceneTreatment.SceneNetworkObject || Treatment == GlobalSceneTreatment.ReplaceWithNetworkPrefab;
        public bool RequiresNetworkLifecycle => Ownership == GlobalSceneOwnership.SceneOwnedNetworkGameplay;
        /// <summary>False for reviewed sources the executor deliberately leaves untouched.</summary>
        public bool IsManaged => Treatment != GlobalSceneTreatment.Unmanaged;
        public uint ReplacementPrefabHash { get; }
        public GlobalScenePoseKind PoseKind { get; }
        public GlobalPosition WorldPosition { get; }
        public Vector3 ParentLocalPosition { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
        public string ParentSourceId { get; }
        internal GlobalScenePlannedEntry(GlobalSceneEntry entry)
        {
            SceneGuid = entry.sceneGuid; SourceId = entry.sourceId; Treatment = entry.treatment; Ownership = entry.ownership; Spatial = entry.spatial;
            ReplacementPrefabHash = entry.replacementPrefabHash; PoseKind = entry.poseKind; WorldPosition = entry.worldPosition;
            ParentLocalPosition = entry.parentLocalPosition; Rotation = entry.rotation; Scale = entry.scale; ParentSourceId = entry.parentSourceId;
        }
    }
    public sealed class GlobalScenePlan
    {
        private readonly Dictionary<string, GlobalScenePlannedEntry> _entries;
        private readonly HashSet<string> _scenes;
        private readonly byte[] _digest;
        public ReadOnlyCollection<GlobalScenePlannedEntry> SpawnOrder { get; }
        public ReadOnlyCollection<GlobalScenePlannedEntry> RetireOrder { get; }
        public byte[] CopyDigest() => (byte[])_digest.Clone();
        public bool ContainsScene(string guid) => guid != null && _scenes.Contains(guid);
        public bool TryGet(string id, out GlobalScenePlannedEntry entry) { entry = null; return id != null && _entries.TryGetValue(id, out entry); }
        internal GlobalScenePlan(List<GlobalSceneEntry> ordered, IEnumerable<string> scenes, byte[] digest)
        {
            _entries = new Dictionary<string, GlobalScenePlannedEntry>(StringComparer.Ordinal); _scenes = new HashSet<string>(scenes, StringComparer.Ordinal);
            var list = new List<GlobalScenePlannedEntry>();
            foreach (var entry in ordered) { var value = new GlobalScenePlannedEntry(entry); _entries.Add(value.SourceId, value); list.Add(value); }
            SpawnOrder = list.AsReadOnly(); var reverse = new List<GlobalScenePlannedEntry>(list); reverse.Reverse(); RetireOrder = reverse.AsReadOnly();
            _digest = (byte[])digest.Clone();
        }
    }
}
