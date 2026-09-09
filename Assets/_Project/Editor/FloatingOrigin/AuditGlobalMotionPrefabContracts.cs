using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Persistent-asset inspection only. Does not instantiate, alter or save a scene/prefab.</summary>
    public static class AuditGlobalMotionPrefabContracts
    {
        public const string ReportPath = "docs/world/floatingorigin/04E_PREFAB_CONTRACT_AUDIT.json";
        [Serializable] public sealed class Entry
        { public string path; public uint hash; public string features; public int behaviourCount; public string[] orderedBehaviours; public string spatialPreflightIssue; }
        [Serializable] public sealed class Report
        {
            public string date; public string scope; public int prefabListAssets; public int candidatePrefabs;
            public int optInCandidates; public int profileAssets; public int loadedControllersWithGlobalProfile;
            public int loadedOptInAdapters; public Entry[] entries; public string[] warnings;
        }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var candidates = new SortedDictionary<string, GameObject>(StringComparer.Ordinal);
            var warnings = new List<string>();
            void Add(GameObject go)
            {
                if (go == null) return;
                string path = AssetDatabase.GetAssetPath(go);
                if (!string.IsNullOrEmpty(path) && EditorUtility.IsPersistent(go)) candidates[path] = go;
            }
            var lists = AssetDatabase.FindAssets("t:NetworkPrefabsList", new[] { "Assets" });
            foreach (var guid in lists)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
                if (list == null) { warnings.Add("Unreadable list: " + path); continue; }
                foreach (var prefab in list.PrefabList)
                {
                    if (prefab == null) { warnings.Add("Null entry: " + path); continue; }
                    if (prefab.Override != NetworkPrefabOverride.None) warnings.Add("Override requires later explicit ABI support: " + path);
                    Add(prefab.Prefab); Add(prefab.SourcePrefabToOverride); Add(prefab.OverridingTargetPrefab);
                }
            }
            Add(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/NetworkPlayer.prefab"));
            foreach (var manager in UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (manager.NetworkConfig == null) continue;
                Add(manager.NetworkConfig.PlayerPrefab);
                foreach (var entry in manager.NetworkConfig.Prefabs.Prefabs) if (entry != null) Add(entry.Prefab);
            }
            var result = new List<Entry>(); int optIn = 0;
            foreach (var pair in candidates)
            {
                var d = GlobalMotionPrefabInspector.Inspect(pair.Value, GlobalPrefabRole.Spatial);
                if ((d.features & GlobalPrefabFeatures.CoordinatesRequired) != 0) optIn++;
                result.Add(new Entry { path = pair.Key, hash = d.prefabHash, features = d.features.ToString(), behaviourCount = d.orderedBehaviours.Length,
                    orderedBehaviours = d.orderedBehaviours, spatialPreflightIssue = GlobalMotionNetworkContract.ValidateLayout(d) ?? "none" });
            }
            int profilesAssigned = 0;
            var field = typeof(ProjectC.Core.NetworkManagerController).GetField("_globalMotionProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var controller in UnityEngine.Object.FindObjectsByType<ProjectC.Core.NetworkManagerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (field != null && field.GetValue(controller) is GlobalMotionNetworkProfile profile && profile.EnforceGlobalContracts) profilesAssigned++;
            return new Report
            {
                date = DateTime.Now.ToString("yyyy-MM-dd"),
                scope = "Candidate prefab union from NetworkPrefabsList assets under Assets, explicit NetworkPlayer and persistent references in loaded NetworkManagers. Spatial-role preflight is hypothetical, not an automatic classification. Loaded-scene opt-in counts do not prove unloaded-scene coverage.",
                prefabListAssets = lists.Length, candidatePrefabs = candidates.Count, optInCandidates = optIn,
                profileAssets = AssetDatabase.FindAssets("t:GlobalMotionNetworkProfile", new[] { "Assets" }).Length,
                loadedControllersWithGlobalProfile = profilesAssigned,
                loadedOptInAdapters = UnityEngine.Object.FindObjectsByType<GlobalMotionPoseAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(a => a.CoordinatesRequired),
                entries = result.ToArray(), warnings = warnings.ToArray()
            };
        }
        [MenuItem("ProjectC/World/Floating Origin/Write Readonly Prefab Contract Audit")]
        public static void WriteReport()
        {
            var report = Run();
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true) + "\n", new System.Text.UTF8Encoding(false));
            Debug.Log("[T-FO04E] Read-only prefab audit saved to " + ReportPath + "; no scene/prefab changes.");
        }
    }
}
