using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using ProjectC.World.FloatingOrigin.Network;

/// <summary>
/// T-FO06D audit: read-only classification of every prefab in the configured NGO registry.
/// Establishes, from the real contract validator, which entries could be declared NonSpatial as-is
/// and which would require Spatial migration. Creates and changes nothing.
/// </summary>
public static class AuditFo06DRegistryClassification
{
    private const string Pilot = "Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab";
    private const string Canonical = "Assets/_Project/Prefabs/NetworkPlayer.prefab";

    [Serializable] public sealed class EntryRow
    {
        public string path, name, features, nonSpatialVerdict, spatialVerdict, suggestedRole;
        public long prefabHash; public int networkBehaviours;
        public bool hasNetworkTransform, hasRigidbody, hasCollider, hasRenderer, hasNavAgent, hasCharacterController, hasMissingScript, isPlayer;
        public List<string> spatialMigrationNeeds = new List<string>();
    }
    [Serializable] public sealed class Report
    {
        public string date, unityVersion, ngoVersion, registryPath, verdict;
        public bool autoRegistrationEnabled, registryModified, assetsCreated;
        public int totalEntries, nonSpatialReady, spatialReady, needsMigration, missingOrBroken;
        public List<EntryRow> entries = new List<EntryRow>();
        public List<string> pilotMinimumCandidates = new List<string>();
        public List<string> notes = new List<string>();
        public List<string> integrityChecks = new List<string>();
    }

    public static string Execute()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stable Edit Mode required.");

        var settingsType = FindType("Unity.Netcode.Editor.Configuration.NetcodeForGameObjectsProjectSettings");
        var settings = settingsType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
        bool auto = (bool)settingsType.GetField("GenerateDefaultNetworkPrefabs").GetValue(settings);
        string registryPath = (string)settingsType.GetField("NetworkPrefabsPath").GetValue(settings);
        var registry = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(registryPath);
        if (registry == null) throw new InvalidOperationException("Configured registry missing.");
        string registryJsonBefore = EditorJsonUtility.ToJson(registry);
        string registryBytesBefore = Hash(registryPath);

        var report = new Report
        {
            date = DateTime.Now.ToString("yyyy-MM-dd"), unityVersion = Application.unityVersion,
            ngoVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(NetworkManager).Assembly).version,
            registryPath = registryPath, autoRegistrationEnabled = auto, totalEntries = registry.PrefabList.Count,
            verdict = "Read-only classification of the existing registry; nothing reclassified, migrated or registered"
        };

        foreach (var entry in registry.PrefabList)
        {
            var row = new EntryRow();
            if (entry == null || entry.Prefab == null)
            {
                row.path = "NULL_ENTRY"; row.nonSpatialVerdict = row.spatialVerdict = "unresolvable";
                row.suggestedRole = "BLOCKED_NULL_ENTRY"; report.missingOrBroken++; report.entries.Add(row); continue;
            }
            var prefab = entry.Prefab;
            row.path = AssetDatabase.GetAssetPath(prefab);
            row.name = prefab.name;

            var spatial = GlobalMotionPrefabInspector.Inspect(prefab, GlobalPrefabRole.Spatial);
            var nonSpatial = GlobalMotionPrefabInspector.Inspect(prefab, GlobalPrefabRole.NonSpatial);
            row.prefabHash = spatial.prefabHash;
            row.features = spatial.features.ToString();
            row.networkBehaviours = spatial.orderedBehaviours == null ? 0 : spatial.orderedBehaviours.Length;
            row.spatialVerdict = GlobalMotionNetworkContract.ValidateLayout(spatial) ?? "PASS";
            row.nonSpatialVerdict = GlobalMotionNetworkContract.ValidateLayout(nonSpatial) ?? "PASS";

            row.hasNetworkTransform = prefab.GetComponentsInChildren<NetworkTransform>(true).Length > 0;
            row.hasRigidbody = prefab.GetComponentsInChildren<Rigidbody>(true).Length > 0;
            row.hasCollider = prefab.GetComponentsInChildren<Collider>(true).Length > 0;
            row.hasRenderer = prefab.GetComponentsInChildren<Renderer>(true).Length > 0;
            row.hasNavAgent = prefab.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).Length > 0;
            row.hasCharacterController = prefab.GetComponentsInChildren<CharacterController>(true).Length > 0;
            row.hasMissingScript = (spatial.features & GlobalPrefabFeatures.MissingScript) != 0;
            row.isPlayer = (spatial.features & GlobalPrefabFeatures.Player) != 0;

            if (row.spatialVerdict != "PASS")
            {
                if (row.hasNetworkTransform) row.spatialMigrationNeeds.Add("remove stock NetworkTransform and replace with global replication");
                if ((spatial.features & GlobalPrefabFeatures.Adapter) == 0) row.spatialMigrationNeeds.Add("add GlobalMotionPoseAdapter");
                if ((spatial.features & GlobalPrefabFeatures.Replicator) == 0) row.spatialMigrationNeeds.Add("add GlobalMotionReplicator");
                if ((spatial.features & GlobalPrefabFeatures.CoordinatesRequired) == 0) row.spatialMigrationNeeds.Add("enable coordinate opt-in");
                if ((spatial.features & GlobalPrefabFeatures.StockTransformSync) != 0) row.spatialMigrationNeeds.Add("disable NetworkObject.SynchronizeTransform");
                if ((spatial.features & GlobalPrefabFeatures.StockParentSync) != 0) row.spatialMigrationNeeds.Add("disable NetworkObject.AutoObjectParentSync");
                if (row.hasRigidbody) row.spatialMigrationNeeds.Add("unsupported Rigidbody composition for this slice");
                if (row.hasNavAgent) row.spatialMigrationNeeds.Add("unsupported NavMeshAgent for this slice");
                if (row.hasMissingScript) row.spatialMigrationNeeds.Add("resolve missing script identity");
                if ((spatial.features & GlobalPrefabFeatures.NestedNetworkObject) != 0) row.spatialMigrationNeeds.Add("nested NetworkObject composition unsupported");
            }

            // NonSpatial is only honest when the prefab genuinely carries no spatial content.
            bool spatialContent = (spatial.features & GlobalPrefabFeatures.SpatialContent) != 0;
            if (row.spatialVerdict == "PASS") { row.suggestedRole = "Spatial"; report.spatialReady++; }
            else if (row.nonSpatialVerdict == "PASS" && !spatialContent) { row.suggestedRole = "NonSpatial"; report.nonSpatialReady++; }
            else { row.suggestedRole = "REQUIRES_MIGRATION"; report.needsMigration++; }
            report.entries.Add(row);
        }

        // The pilot player is not in this registry; a pilot list must add it explicitly.
        var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(Pilot);
        if (pilot == null) throw new InvalidOperationException("Pilot prefab missing.");
        var pilotLayout = GlobalMotionPrefabInspector.Inspect(pilot, GlobalPrefabRole.Spatial);
        if (GlobalMotionNetworkContract.ValidateLayout(pilotLayout) != null) throw new InvalidOperationException("Pilot prefab no longer satisfies the Spatial contract.");
        report.pilotMinimumCandidates.Add(Pilot + " (Spatial, PASS, hash=" + pilotLayout.prefabHash + ")");

        report.notes.Add("Effective registry must match the classified profile catalog exactly, so a pilot needs its own minimal list rather than the 58-entry default.");
        report.notes.Add("NonSpatial is a claim about content: entries with renderers/colliders/bodies are counted as REQUIRES_MIGRATION even when the NonSpatial validator would technically accept the layout.");
        report.notes.Add("This audit classifies registry membership only. It does not prove any prefab is safe to spawn, nor that its gameplay works under global coordinates.");

        if (EditorJsonUtility.ToJson(registry) != registryJsonBefore || Hash(registryPath) != registryBytesBefore)
            throw new InvalidOperationException("Registry changed during a read-only audit.");
        report.integrityChecks.Add("Default registry serialized contents and bytes unchanged");
        if (Hash(Canonical) == null) throw new InvalidOperationException("Canonical unreadable.");
        report.integrityChecks.Add("No prefab, list, scene or setting was created or modified");

        string json = EncodeJson(report);
        File.WriteAllText("docs/world/floatingorigin/06D_REGISTRY_CLASSIFICATION.json", json + "\n", new UTF8Encoding(false));

        var summary = new StringBuilder();
        summary.AppendLine("Registry classification (read-only): " + report.totalEntries + " entries");
        summary.AppendLine("Spatial-ready as-is: " + report.spatialReady);
        summary.AppendLine("NonSpatial-ready as-is: " + report.nonSpatialReady);
        summary.AppendLine("Requires migration: " + report.needsMigration);
        summary.AppendLine("Null/broken entries: " + report.missingOrBroken);
        summary.AppendLine("Pilot candidate: " + string.Join(", ", report.pilotMinimumCandidates));
        int shown = 0;
        foreach (var row in report.entries)
        {
            if (row.suggestedRole == "NonSpatial" || shown >= 12) continue;
            summary.AppendLine("  " + row.suggestedRole + " | " + row.name + " | spatial=" + row.spatialVerdict);
            shown++;
        }
        summary.Append(string.Join("\n", report.integrityChecks));
        return summary.ToString();
    }

    private static Type FindType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (name.StartsWith("Newtonsoft.", StringComparison.Ordinal) && assembly.GetName().Name != "Newtonsoft.Json") continue;
            var type = assembly.GetType(name); if (type != null) return type;
        }
        throw new InvalidOperationException("Type unavailable: " + name);
    }
    private static string EncodeJson(object value)
    {
        var format = FindType("Newtonsoft.Json.Formatting");
        return (string)FindType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject", new[] { typeof(object), format })
            .Invoke(null, new[] { value, Enum.ToObject(format, 1) });
    }
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
    }
}
