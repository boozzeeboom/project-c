using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using ProjectC.Player;
using ProjectC.World.FloatingOrigin.Network;

/// <summary>
/// T-FO06B verification: read-only. Confirms the pilot prefab satisfies the Spatial contract and that
/// nothing else moved. Protected baselines are compared against the committed 06A snapshot, not constants.
/// </summary>
public static class VerifyFo06BPilotPrefab
{
    private const string Canonical = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
    private const string Pilot = "Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab";
    private const string Baseline = "docs/world/floatingorigin/06A_PILOT_SCOPE_BASELINE.json";

    [Serializable] public sealed class ComponentRow { public string objectPath, type; public int index; public bool enabled; }
    [Serializable] public sealed class FlagRow { public string name; public bool value; }
    [Serializable] public sealed class Report
    {
        public string date, unityVersion, ngoVersion, canonicalPath, pilotPath, verdict;
        public bool autoRegistrationEnabled, pilotRegistered, pilotSelectedAsPlayerPrefab, runtimeTested;
        public string selectedPlayerPrefabInScene, registryPath, layoutContract, layoutFeatures;
        public long pilotHash, canonicalHash; public int registryEntries;
        public List<ComponentRow> pilotRootComponents = new List<ComponentRow>();
        public List<string> pilotNetworkBehaviourOrder = new List<string>();
        public List<FlagRow> pilotNetworkObjectFlags = new List<FlagRow>();
        public List<string> unchangedProtectedFiles = new List<string>();
        public List<string> integrityChecks = new List<string>();
        public List<string> limitations = new List<string>();
        public List<string> remainingBlockers = new List<string>();
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

        var canonical = AssetDatabase.LoadAssetAtPath<GameObject>(Canonical);
        var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(Pilot);
        if (canonical == null || pilot == null) throw new InvalidOperationException("Canonical or pilot prefab missing.");

        var layout = GlobalMotionPrefabInspector.Inspect(pilot, GlobalPrefabRole.Spatial);
        string layoutError = GlobalMotionNetworkContract.ValidateLayout(layout);

        var report = new Report
        {
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            unityVersion = Application.unityVersion,
            ngoVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(NetworkManager).Assembly).version,
            canonicalPath = Canonical, pilotPath = Pilot, registryPath = registryPath,
            autoRegistrationEnabled = auto, registryEntries = registry.PrefabList.Count,
            layoutContract = layoutError ?? "PASS", layoutFeatures = layout.features.ToString(),
            pilotHash = layout.prefabHash, canonicalHash = new NetworkPrefab { Prefab = canonical }.SourcePrefabGlobalObjectIdHash,
            verdict = "Pilot player prefab authored and contract-valid; pilot NOT selected, NOT registered, global mode still off"
        };
        if (layoutError != null) throw new InvalidOperationException("Pilot layout rejected: " + layoutError);
        if (report.pilotHash == 0 || report.pilotHash == report.canonicalHash) throw new InvalidOperationException("Pilot hash invalid or identical to canonical.");

        var components = pilot.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component == null) throw new InvalidOperationException("Missing script on pilot root.");
            report.pilotRootComponents.Add(new ComponentRow { objectPath = pilot.name, index = i, type = component.GetType().FullName, enabled = Enabled(component) });
        }
        foreach (var behaviour in pilot.GetComponentsInChildren<NetworkBehaviour>(true))
            report.pilotNetworkBehaviourOrder.Add(behaviour.GetType().FullName + "|" + (behaviour.enabled ? "1" : "0"));

        var networkObject = pilot.GetComponent<NetworkObject>();
        var flags = new SerializedObject(networkObject);
        foreach (string name in new[] { "SynchronizeTransform", "AutoObjectParentSync", "SceneMigrationSynchronization", "ActiveSceneSynchronization", "DontDestroyWithOwner" })
        {
            var flag = flags.FindProperty(name);
            if (flag == null) throw new InvalidOperationException("Flag not found: " + name);
            if (flag.boolValue) throw new InvalidOperationException("Flag must be false for the pilot: " + name);
            report.pilotNetworkObjectFlags.Add(new FlagRow { name = name, value = flag.boolValue });
        }
        report.integrityChecks.Add("Pilot Spatial layout contract PASS with baked PlayerAttacker/PlayerTarget, replicator, opt-in adapter and no stock writers");

        if (pilot.GetComponentsInChildren<NetworkTransform>(true).Length != 0) throw new InvalidOperationException("Stock NetworkTransform present.");
        var controller = pilot.GetComponent<CharacterController>();
        if (controller == null || !controller.enabled) throw new InvalidOperationException("Root CharacterController missing.");
        foreach (var collider in pilot.GetComponentsInChildren<Collider>(true))
            if (collider != controller) throw new InvalidOperationException("Collider outside root controller.");
        if (pilot.GetComponent<NetworkPlayer>() == null) throw new InvalidOperationException("NetworkPlayer missing.");
        report.integrityChecks.Add("Root CharacterController only, no stock transform/body/joint/nav/2D writers");

        // Canonical player and its CharacterController geometry must remain the legacy configuration.
        if (canonical.GetComponentsInChildren<NetworkTransform>(true).Length == 0)
            throw new InvalidOperationException("Canonical player unexpectedly lost its NetworkTransform.");
        if (canonical.GetComponent<GlobalMotionReplicator>() != null || canonical.GetComponent<GlobalMotionPoseAdapter>() != null)
            throw new InvalidOperationException("Canonical player unexpectedly gained global components.");
        report.integrityChecks.Add("Canonical player still legacy: stock NetworkTransform present, no global components");

        // Protected bytes are compared with the previously committed 06A evidence.
        var baseline = ReadBaselineHashes();
        foreach (var pair in baseline)
        {
            if (!File.Exists(pair.Key)) throw new InvalidOperationException("Protected file missing: " + pair.Key);
            if (Hash(pair.Key) != pair.Value) throw new InvalidOperationException("Protected file changed since 06A: " + pair.Key);
            report.unchangedProtectedFiles.Add(pair.Key);
        }
        if (report.unchangedProtectedFiles.Count < 8) throw new InvalidOperationException("Baseline evidence incomplete.");
        report.integrityChecks.Add("All " + report.unchangedProtectedFiles.Count + " protected files byte-identical to the committed 06A baseline");

        foreach (var entry in registry.PrefabList)
            if (entry != null && entry.Prefab != null && AssetDatabase.GetAssetPath(entry.Prefab) == Pilot) report.pilotRegistered = true;
        if (report.pilotRegistered) throw new InvalidOperationException("Pilot prefab is registered; isolation broken.");
        if (auto) throw new InvalidOperationException("Automatic registration is enabled again.");
        report.integrityChecks.Add("Automatic registration off; registry still " + report.registryEntries + " entries and pilot unregistered");

        // Read the actual scene selection instead of relying on a runtime-only singleton.
        report.selectedPlayerPrefabInScene = "no_loaded_networkmanager";
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var manager in root.GetComponentsInChildren<NetworkManager>(true))
                    report.selectedPlayerPrefabInScene = AssetDatabase.GetAssetPath(manager.NetworkConfig.PlayerPrefab);
        }
        report.pilotSelectedAsPlayerPrefab = report.selectedPlayerPrefabInScene == Pilot;
        if (report.pilotSelectedAsPlayerPrefab) throw new InvalidOperationException("Pilot must not be selected yet.");
        if (report.selectedPlayerPrefabInScene != Canonical && report.selectedPlayerPrefabInScene != "no_loaded_networkmanager")
            throw new InvalidOperationException("Unexpected selected PlayerPrefab: " + report.selectedPlayerPrefabInScene);
        report.integrityChecks.Add("Scene selection still the canonical player: " + report.selectedPlayerPrefabInScene);

        report.remainingBlockers.Add("Pilot is not registered in any NetworkPrefabsList; a real session requires an effective registry that matches a classified profile catalog exactly.");
        report.remainingBlockers.Add("GlobalMotionNetworkProfile, reviewed scene catalog/digest, prepared frames, markers and native scene executor do not exist yet.");
        report.remainingBlockers.Add("Bootstrap still holds enabled legacy ShipPositionServer/PlayerPositionServer, an active ClientSceneLoader and 3 unresolved missing scripts.");
        report.remainingBlockers.Add("WorldScene_0_0 hierarchy remains uninspected; trusted account issuer and explicit store ownership remain unconfirmed.");
        report.limitations.Add("Independent copy, not a prefab variant: canonical visual/gameplay edits will NOT propagate and must be re-applied deliberately.");
        report.limitations.Add("Owner-only SkillInputService/SkillAnimationPlayer/SkillAnimationEventPassthrough are plain MonoBehaviours, so runtime addition does not change NetworkBehaviour ordering; their gameplay behaviour on the pilot is untested.");
        report.limitations.Add("No Play Mode, physics, network session, screenshots, builds, auth or save access; contract validation is Edit Mode metadata only.");

        string json = EncodeJson(report);
        File.WriteAllText("docs/world/floatingorigin/06B_PILOT_PLAYER_PREFAB.json", json + "\n", new UTF8Encoding(false));

        return "Pilot prefab verified\nLayout=" + report.layoutContract + " features=" + report.layoutFeatures +
            "\nNetworkBehaviours=" + report.pilotNetworkBehaviourOrder.Count + "; NO flags all false" +
            "\nAuto-registration=" + auto + "; registry=" + report.registryEntries + "; pilotRegistered=" + report.pilotRegistered +
            "\nSelected PlayerPrefab=" + report.selectedPlayerPrefabInScene +
            "\n" + string.Join("\n", report.integrityChecks);
    }

    private static Dictionary<string, string> ReadBaselineHashes()
    {
        if (!File.Exists(Baseline)) throw new InvalidOperationException("Committed 06A baseline missing: " + Baseline);
        var linq = FindType("Newtonsoft.Json.Linq.JObject");
        var parsed = linq.GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { File.ReadAllText(Baseline) });
        var token = FindType("Newtonsoft.Json.Linq.JToken");
        var item = token.GetMethod("get_Item", new[] { typeof(object) });
        var files = item.Invoke(parsed, new object[] { "protectedFiles" }) as System.Collections.IEnumerable;
        if (files == null) throw new InvalidOperationException("Baseline has no protectedFiles.");
        var result = new Dictionary<string, string>();
        foreach (var row in files)
        {
            string path = item.Invoke(row, new object[] { "path" }).ToString();
            string sha = item.Invoke(row, new object[] { "sha256" }).ToString();
            if (!result.ContainsKey(path)) result.Add(path, sha);
        }
        return result;
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
    private static bool Enabled(Component c) => c != null && (!(c is Behaviour b) || b.enabled) && (!(c is Collider col) || col.enabled);
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
    }
}
