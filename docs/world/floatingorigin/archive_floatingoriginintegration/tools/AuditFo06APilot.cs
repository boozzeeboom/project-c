using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Reflection;

using ProjectC.Player;
using ProjectC.World.FloatingOrigin.Network;

public static class AuditFo06APilot
{
    [Serializable] public sealed class FileRow { public string path, guid, sha256; public long bytes; public bool dirty; }
    [Serializable] public sealed class ComponentRow { public string objectPath, type; public int index; public bool enabled; }
    [Serializable] public sealed class ReferenceRow { public string component, property, assetPath, guid; }
    [Serializable] public sealed class SceneRow { public string path; public bool loaded, dirty, preview; public int roots; public List<string> missingComponentObjects = new List<string>(); public List<ComponentRow> relevant = new List<ComponentRow>(); }
    [Serializable] public sealed class ManagerRow { public string path, selectedPlayerPrefab, globalProfile, globalBootstrap, globalSessionCoordinator; public List<string> prefabLists = new List<string>(); }
    [Serializable] public sealed class RegistryRow { public string path; public bool autoGenerate, isDefault, dirty; public int count; public List<string> prefabs = new List<string>(); }
    [Serializable] public sealed class PlayerRow
    {
        public string path, assetType; public int childCount; public List<ComponentRow> components = new List<ComponentRow>(); public List<ReferenceRow> references = new List<ReferenceRow>();
        public List<string> networkBehaviourOrder = new List<string>(); public List<string> blockers = new List<string>();
        public bool autoParentSync, synchronizeTransform, activeSceneSync, sceneMigrationSync, dontDestroyWithOwner;
        public float height, radius, stepOffset, slopeLimit, skinWidth; public float[] center;
    }
    [Serializable] public sealed class Report
    {
        public string date, unityVersion, ngoVersion, verdict;
        public bool scenesOpened, previewSceneCreated, assetsChanged, runtimeTested, canCreateUnregisteredVariantWithoutPolicyChange;
        public List<FileRow> protectedFiles = new List<FileRow>(); public List<SceneRow> loadedScenes = new List<SceneRow>();
        public List<ManagerRow> managers = new List<ManagerRow>(); public PlayerRow player; public RegistryRow registry;
        public List<string> worldDependencies = new List<string>(); public List<string> runtimeCreationCandidates = new List<string>();
        public List<string> blockers = new List<string>(); public List<string> limitations = new List<string>(); public List<string> integrityChecks = new List<string>();
    }
    public static string Execute()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
        const string playerPath = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
        const string bootstrapPath = "Assets/_Project/Scenes/BootstrapScene.unity";
        const string worldPath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
        Type settingsType = null;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { settingsType = a.GetType("Unity.Netcode.Editor.Configuration.NetcodeForGameObjectsProjectSettings"); if (settingsType != null) break; }
        if (settingsType == null) throw new InvalidOperationException("Live NGO editor settings type unavailable.");
        var settings = settingsType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
        string registryPath = (string)settingsType.GetField("NetworkPrefabsPath").GetValue(settings);
        bool autoSetting = (bool)settingsType.GetField("GenerateDefaultNetworkPrefabs").GetValue(settings);
        string sceneToken = LoadedToken();
        var registry = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(registryPath);
        if (registry == null) throw new InvalidOperationException("Actual configured default registry is missing.");
        string registryBefore = EditorJsonUtility.ToJson(registry);
        bool registryDirty = EditorUtility.IsDirty(registry), auto = autoSetting;
        var report = new Report { date = DateTime.Now.ToString("yyyy-MM-dd"), unityVersion = Application.unityVersion,
            ngoVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(NetworkManager).Assembly).version,
            verdict = "BLOCKED: registration policy and dirty/uninspected native scene scope require explicit decisions", canCreateUnregisteredVariantWithoutPolicyChange = !auto };
        foreach (string path in new[] { playerPath, bootstrapPath, worldPath, registryPath }) { report.protectedFiles.Add(Fingerprint(path)); report.protectedFiles.Add(Fingerprint(path + ".meta")); }
        report.registry = new RegistryRow { path = registryPath, autoGenerate = auto, dirty = registryDirty, count = registry.PrefabList.Count,
            isDefault = new SerializedObject(registry).FindProperty("IsDefault").boolValue };
        foreach (var p in registry.PrefabList) report.registry.prefabs.Add(AssetDatabase.GetAssetPath(p.Prefab));
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        if (player == null) throw new InvalidOperationException("Confirmed player asset missing.");
        var row = new PlayerRow { path = playerPath, assetType = PrefabUtility.GetPrefabAssetType(player).ToString(), childCount = player.transform.childCount };
        foreach (var tr in player.GetComponentsInChildren<Transform>(true))
        {
            var components = tr.GetComponents<Component>();
            for (int i=0;i<components.Length;i++)
            {
                var c = components[i]; row.components.Add(new ComponentRow { objectPath = Path(tr), index = i, type = c == null ? "MISSING_COMPONENT_UNRESOLVED" : c.GetType().FullName, enabled = Enabled(c) });
                if (c == null) { row.blockers.Add("missing component:" + Path(tr)); continue; }
                if (c is NetworkBehaviour) row.networkBehaviourOrder.Add(Path(tr) + ":" + c.GetType().FullName);
                var so = new SerializedObject(c); var property = so.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                    { string asset = AssetDatabase.GetAssetPath(property.objectReferenceValue); if (!string.IsNullOrEmpty(asset)) row.references.Add(new ReferenceRow { component = c.GetType().FullName, property = property.propertyPath, assetPath = asset, guid = AssetDatabase.AssetPathToGUID(asset) }); }
            }
        }
        var no = player.GetComponent<NetworkObject>(); var cc = player.GetComponent<CharacterController>();
        if (no == null) row.blockers.Add("missing root NetworkObject");
        else { row.autoParentSync = no.AutoObjectParentSync; row.synchronizeTransform = no.SynchronizeTransform; row.activeSceneSync = no.ActiveSceneSynchronization; row.sceneMigrationSync = no.SceneMigrationSynchronization; row.dontDestroyWithOwner = no.DontDestroyWithOwner; }
        if (cc == null || !cc.enabled) row.blockers.Add("missing or disabled root CharacterController");
        else { row.height = cc.height; row.radius = cc.radius; row.center = new[] { cc.center.x, cc.center.y, cc.center.z }; row.stepOffset = cc.stepOffset; row.skinWidth = cc.skinWidth; row.slopeLimit = cc.slopeLimit; }
        var np = player.GetComponent<NetworkPlayer>(); if (np == null || !np.enabled) row.blockers.Add("missing/disabled root NetworkPlayer");
        if (player.GetComponent<GlobalMotionReplicator>() == null) row.blockers.Add("missing GlobalMotionReplicator");
        var adapter = player.GetComponent<GlobalMotionPoseAdapter>(); if (adapter == null || !adapter.CoordinatesRequired) row.blockers.Add("missing explicit opt-in GlobalMotionPoseAdapter");
        if (player.GetComponentsInChildren<NetworkTransform>(true).Length > 0) row.blockers.Add("stock NetworkTransform present");
        if (player.GetComponentsInChildren<Rigidbody>(true).Length > 0 || player.GetComponentsInChildren<Joint>(true).Length > 0 || player.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).Length > 0 || player.GetComponentsInChildren<Collider2D>(true).Length > 0 || player.GetComponentsInChildren<Rigidbody2D>(true).Length > 0) row.blockers.Add("native body/joint/nav/2D factory restriction");
        foreach (var collider in player.GetComponentsInChildren<Collider>(true)) if (collider != cc) row.blockers.Add("collider outside root CharacterController:" + Path(collider.transform));
        foreach (var script in player.GetComponents<MonoBehaviour>()) if (script is IGlobalMotionActorParticipant && !(script is NetworkPlayer)) row.blockers.Add("additional prefab participant:" + script.GetType().FullName);
        if (no != null && (no.AutoObjectParentSync || no.SynchronizeTransform || no.ActiveSceneSynchronization || no.SceneMigrationSynchronization || no.DontDestroyWithOwner)) row.blockers.Add("NetworkObject flags require explicit global-only overrides");
        report.player = row;
        for (int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene = SceneManager.GetSceneAt(i); var sr = new SceneRow { path = scene.path, loaded = scene.isLoaded, dirty = scene.isDirty, roots = scene.rootCount, preview = UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene) };
            if (scene.isLoaded)
                foreach (var root in scene.GetRootGameObjects()) foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    var components = tr.GetComponents<Component>();
                    for (int j=0;j<components.Length;j++)
                    {
                        var c=components[j]; if (c==null) { sr.missingComponentObjects.Add(Path(tr)+"|componentIndex="+j+"|identity=UNRESOLVED"); continue; }
                        string n=c.GetType().FullName;
                        if (n.Contains("FloatingOrigin") || n.Contains("ShipPosition") || n.Contains("ScenePlacedObjectSpawner") || n.Contains("ClientSceneLoader")) sr.relevant.Add(new ComponentRow { objectPath=Path(tr),type=n,index=j,enabled=Enabled(c) });
                    }
                    var controller=tr.GetComponent<ProjectC.Core.NetworkManagerController>(); var manager=tr.GetComponent<NetworkManager>();
                    if (controller!=null || manager!=null)
                    {
                        var mr=new ManagerRow { path=Path(tr), selectedPlayerPrefab=manager==null ? "UNCONFIRMED_NO_LOADED_NETWORKMANAGER" : AssetDatabase.GetAssetPath(manager.NetworkConfig.PlayerPrefab) };
                        if (controller!=null) { var so=new SerializedObject(controller); mr.globalProfile=Reference(so,"_globalMotionProfile"); mr.globalBootstrap=Reference(so,"_globalSpawnBootstrap"); mr.globalSessionCoordinator=Reference(so,"_globalSessionCoordinator"); }
                        if (manager!=null) foreach (var list in manager.NetworkConfig.Prefabs.NetworkPrefabsLists) mr.prefabLists.Add(AssetDatabase.GetAssetPath(list));
                        report.managers.Add(mr);
                    }
                }
            report.loadedScenes.Add(sr);
        }
        report.worldDependencies.AddRange(AssetDatabase.GetDependencies(worldPath, false));
        string nmc = File.ReadAllText("Assets/_Project/Scripts/Core/NetworkManagerController.cs");
        var lines = nmc.Split('\n'); for(int i=0;i<lines.Length;i++) if (Regex.IsMatch(lines[i], @"go\.AddComponent<|gameObject\.AddComponent<")) report.runtimeCreationCandidates.Add("NetworkManagerController.cs:"+(i+1)+":"+lines[i].Trim());
        report.blockers.AddRange(row.blockers);
        if (auto) report.blockers.Add("NGO NetworkPrefabProcessor auto-registers every imported root-NetworkObject prefab regardless of folder; an unassigned global-only variant is not registration-isolated.");
        foreach(var scene in report.loadedScenes) { if(scene.dirty)report.blockers.Add("Unsaved loaded scene: "+scene.path); if(scene.missingComponentObjects.Count>0) report.blockers.Add("Unresolved missing component(s): "+scene.path); }
        bool worldLoaded=false; foreach(var scene in report.loadedScenes)if(scene.path==worldPath && scene.loaded)worldLoaded=true;
        if(!worldLoaded) report.blockers.Add("WorldScene_0_0 hierarchy/native pose/collider/nav/marker coverage UNINSPECTED; dependency metadata is not a scene audit.");
        report.limitations.Add("No default registry setting/list change, no prefab creation or variant, no scene opening/preview/saving, no Play Mode/physics/screenshots/auth/save-file access.");
        report.limitations.Add("Runtime-created DDOL client services and NetworkPlayer OnSpawn additions require separate semantic/native coverage; component absence in Edit Mode is not runtime absence.");
        report.limitations.Add("Missing script identities remain unresolved; GameObject names do not establish the missing C# class.");
        if(LoadedToken()!=sceneToken)throw new InvalidOperationException("Loaded scene/dirty/hierarchy state changed during read-only audit.");
        report.integrityChecks.Add("Loaded scene set, dirty flags and hierarchy/component fingerprint unchanged");
        if((bool)settingsType.GetField("GenerateDefaultNetworkPrefabs").GetValue(settings)!=auto || EditorJsonUtility.ToJson(registry)!=registryBefore || EditorUtility.IsDirty(registry)!=registryDirty)throw new InvalidOperationException("Default registry/settings changed during audit.");
        report.integrityChecks.Add("Auto-generation setting, exact serialized registry and dirty flag unchanged");
        foreach(var f in report.protectedFiles) if(Fingerprint(f.path).sha256!=f.sha256)throw new InvalidOperationException("Protected bytes changed: "+f.path);
        report.integrityChecks.Add("Canonical player, Bootstrap, WorldScene_0_0, default registry and their meta bytes unchanged");
        // Dynamically compiled nested DTOs are not fully known to Unity's native serializer.
        // Json.NET preserves all nested baseline/evidence fields; assert them before publishing.
        string json = EncodeJson(report);
        var convertType = JsonType("Newtonsoft.Json.JsonConvert");
        var roundTrip = (Report)convertType.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })
            .Invoke(null, new object[] { json, typeof(Report) });
        if (roundTrip == null || roundTrip.protectedFiles.Count != report.protectedFiles.Count ||
            roundTrip.loadedScenes.Count != report.loadedScenes.Count || roundTrip.managers.Count != report.managers.Count ||
            roundTrip.player == null || roundTrip.player.blockers.Count != row.blockers.Count ||
            roundTrip.registry == null || roundTrip.registry.count != registry.PrefabList.Count)
            throw new InvalidOperationException("Incomplete pilot report schema; report not published.");
        Directory.CreateDirectory("docs/world/floatingorigin");
        File.WriteAllText("docs/world/floatingorigin/06A_PILOT_SCOPE_BASELINE.json", json + "\n", new UTF8Encoding(false));
        return "Pilot preflight BLOCKED\n"+"Canonical player blockers="+row.blockers.Count+"; default registry="+registry.PrefabList.Count+"; autoGenerate="+auto+"\n"+
            "Loaded scenes="+report.loadedScenes.Count+"; WorldScene loaded="+worldLoaded+"; runtime AddComponent lexical candidates="+report.runtimeCreationCandidates.Count+"\n"+
            string.Join("\n",report.integrityChecks)+"\nManagers="+EncodeJson(report.managers);
    }
    private static Type JsonType(string name)
    {
        // The editor also loads a Localization-private Json.NET copy with identical public type names.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == "Newtonsoft.Json") return assembly.GetType(name, true);
        throw new InvalidOperationException("Canonical Json.NET assembly is not loaded.");
    }
    private static string EncodeJson(object value)
    {
        var format = JsonType("Newtonsoft.Json.Formatting");
        return (string)JsonType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject", new[] { typeof(object), format })
            .Invoke(null, new[] { value, Enum.ToObject(format, 1) });
    }
    private static FileRow Fingerprint(string path) { byte[] b=File.ReadAllBytes(path); var asset=AssetDatabase.LoadMainAssetAtPath(path); return new FileRow { path=path,guid=AssetDatabase.AssetPathToGUID(path),bytes=b.LongLength,sha256=Hash(b),dirty=asset!=null && EditorUtility.IsDirty(asset) }; }
    private static string Hash(byte[] b) { using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant(); }
    private static bool Enabled(Component c) => c!=null && (!(c is Behaviour b) || b.enabled) && (!(c is Collider collider) || collider.enabled);
    private static string Path(Transform t) { var s=t.name+"["+t.GetSiblingIndex()+"]"; while(t.parent!=null){t=t.parent;s=t.name+"["+t.GetSiblingIndex()+"]/"+s;}return s; }
    private static string Reference(SerializedObject so,string name) { var p=so.FindProperty(name); if(p==null)return "FIELD_MISSING"; var o=p.objectReferenceValue; return o==null ? "UNASSIGNED" : AssetDatabase.GetAssetPath(o)+"|"+o.name; }
    private static string NativeIdentity(UnityEngine.Object value)
    {
        // Reflection keeps this audit compatible with both the EntityId API and older Unity editors.
        var method = typeof(UnityEngine.Object).GetMethod("GetEntityId", BindingFlags.Public | BindingFlags.Instance)
            ?? typeof(UnityEngine.Object).GetMethod("GetInstanceID", BindingFlags.Public | BindingFlags.Instance);
        if (method == null) throw new InvalidOperationException("No native object identity API available.");
        return method.Invoke(value, null).ToString();
    }
    private static string LoadedToken()
    {
        var b = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            b.Append(scene.handle.ToString()).Append(scene.path).Append(scene.isLoaded).Append(scene.isDirty);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    b.Append(NativeIdentity(tr)).Append(Path(tr)).Append(tr.localPosition.ToString("R"))
                        .Append(tr.localRotation.ToString("R")).Append(tr.localScale.ToString("R")).Append(tr.gameObject.activeSelf);
                    foreach (var component in tr.GetComponents<Component>())
                        b.Append(component == null ? "missing" : component.GetType().FullName + ":" + NativeIdentity(component) + ":" + Enabled(component));
                }
        }
        return Hash(Encoding.UTF8.GetBytes(b.ToString()));
    }
}
