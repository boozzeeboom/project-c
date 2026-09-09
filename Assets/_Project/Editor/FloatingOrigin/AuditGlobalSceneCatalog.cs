using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Read-only authoring snapshot. Never loads/saves scenes, invents global poses, or auto-approves roots.</summary>
    public static class AuditGlobalSceneCatalog
    {
        [Serializable] public sealed class Report
        {
            public string scope = "Assets/_Project/Scenes + enabled build scenes + loaded saved-path scenes; not proof of every runtime loading path";
            public int sceneCandidates, loadedInspected, uninspected, dirtyScenes, observations, catalogAssets, validCatalogAssets;
            public GlobalSceneCatalogData draft;
            public string[] warnings, errors;
        }
        [MenuItem("ProjectC/World/Floating Origin/Audit Scene Catalog Candidates (Read Only)")]
        public static void Execute()
        {
            var report = Run();
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/world/floatingorigin");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "04H_SCENE_CATALOG_AUDIT.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log($"[T-FO04H] scene candidates={report.sceneCandidates}, uninspected={report.uninspected}, unreviewed entries={report.observations}. No scenes changed.");
        }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var result = new Report(); var warnings = new List<string>(); var errors = new List<string>();
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) paths.Add(scene.path);
            var loaded = new Dictionary<string, UnityEngine.SceneManagement.Scene>(StringComparer.Ordinal);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && !string.IsNullOrEmpty(scene.path)) { paths.Add(scene.path); loaded[scene.path] = scene; }
                else if (scene.isLoaded) warnings.Add("Loaded unsaved-path scene cannot enter catalog: " + scene.name);
            }
            var guids = new List<string>(); var sources = new List<GlobalSceneSource>(); var reviews = new List<GlobalSceneEntry>();
            foreach (string path in paths)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!GlobalSceneCatalogCompiler.IsHex(guid, 32)) { errors.Add("Invalid/missing scene asset GUID: " + path); continue; }
                var source = new GlobalSceneSource { sceneGuid = guid, assetPath = path, dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString() };
                guids.Add(guid); sources.Add(source);
                if (!loaded.TryGetValue(path, out var scene)) { result.uninspected++; warnings.Add("UNINSPECTED: " + path); continue; }
                int before = errors.Count;
                var candidates = new HashSet<GameObject>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    candidates.Add(root);
                    foreach (var no in root.GetComponentsInChildren<NetworkObject>(true)) candidates.Add(no.gameObject);
                }
                var ids = new Dictionary<GameObject, string>();
                foreach (var candidate in candidates)
                {
                    string id = SourceId(candidate);
                    if (!GlobalSceneCatalogCompiler.IsSourceId(id, guid)) errors.Add("Unsaved/transient authored identity in " + path + ": " + candidate.name);
                    else ids.Add(candidate, id);
                }
                var observations = new List<GlobalSceneObservation>();
                foreach (var pair in ids)
                {
                    var parent = pair.Key.transform.parent;
                    while (parent != null && !candidates.Contains(parent.gameObject)) parent = parent.parent;
                    string parentId = "";
                    if (parent != null && !ids.TryGetValue(parent.gameObject, out parentId)) { errors.Add("Missing observed ancestor: " + pair.Value); continue; }
                    string hash = LayoutHash(pair.Key, out bool missing);
                    if (missing) errors.Add("Missing script/component in observed subtree: " + pair.Value + " (" + HierarchyPath(pair.Key.transform) + ")");
                    observations.Add(new GlobalSceneObservation { sourceId = pair.Value, parentSourceId = parentId,
                        isRoot = pair.Key.transform.parent == null, isNetworkObject = pair.Key.GetComponent<NetworkObject>() != null, layoutHash = hash });
                    // NO conversion from a legacy Transform to GlobalPosition. Human/native semantic review supplies explicit poses.
                    reviews.Add(new GlobalSceneEntry { sceneGuid = guid, sourceId = pair.Value, parentSourceId = parentId, treatment = GlobalSceneTreatment.Unreviewed });
                }
                observations.Sort((a, b) => string.CompareOrdinal(a.sourceId, b.sourceId)); source.observations = observations.ToArray();
                source.inspectedComplete = errors.Count == before; source.saved = !scene.isDirty;
                result.loadedInspected++; result.observations += observations.Count;
                if (scene.isDirty) { result.dirtyScenes++; warnings.Add("DIRTY: dependency hash is for saved disk content, not this live scene: " + path); }
            }
            guids.Sort(StringComparer.Ordinal); sources.Sort((a, b) => string.CompareOrdinal(a.sceneGuid, b.sceneGuid)); reviews.Sort((a, b) => string.CompareOrdinal(a.sourceId, b.sourceId));
            result.sceneCandidates = sources.Count; result.draft = new GlobalSceneCatalogData { expectedSceneGuids = guids.ToArray(), scenes = sources.ToArray(), entries = reviews.ToArray() };
            foreach (string guid in AssetDatabase.FindAssets("t:GlobalMotionSceneCatalog", new[] { "Assets" }))
            {
                result.catalogAssets++; string path = AssetDatabase.GUIDToAssetPath(guid); var catalog = AssetDatabase.LoadAssetAtPath<GlobalMotionSceneCatalog>(path);
                bool valid = catalog != null && GlobalSceneCatalogCompiler.TryCompile(catalog.Data, out _, out _);
                if (valid) foreach (var source in catalog.Data.scenes)
                {
                    if (AssetDatabase.AssetPathToGUID(source.assetPath) != source.sceneGuid || AssetDatabase.GetAssetDependencyHash(source.assetPath).ToString() != source.dependencyHash ||
                        (loaded.TryGetValue(source.assetPath, out var scene) && scene.isDirty)) { valid = false; break; }
                    if (loaded.ContainsKey(source.assetPath))
                    {
                        var observed = sources.Find(s => s.sceneGuid == source.sceneGuid);
                        if (observed == null || !observed.inspectedComplete || !SameObservations(source.observations, observed.observations)) { valid = false; break; }
                    }
                }
                if (valid) result.validCatalogAssets++; else warnings.Add("Catalog invalid/stale/unreviewed: " + path);
            }
            result.warnings = warnings.ToArray(); result.errors = errors.ToArray(); return result;
        }
        private static string HierarchyPath(Transform value)
        {
            var parts = new List<string>(); for (var t = value; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse(); return string.Join("/", parts);
        }
        private static bool SameObservations(GlobalSceneObservation[] stored, GlobalSceneObservation[] live)
        {
            if (stored.Length != live.Length) return false;
            var map = new Dictionary<string, GlobalSceneObservation>(StringComparer.Ordinal); foreach (var o in live) map.Add(o.sourceId, o);
            foreach (var o in stored)
                if (!map.TryGetValue(o.sourceId, out var v) || o.parentSourceId != v.parentSourceId || o.layoutHash != v.layoutHash || o.isRoot != v.isRoot || o.isNetworkObject != v.isNetworkObject) return false;
            return true;
        }
        private static string SourceId(GameObject value)
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(value);
            return id.assetGUID.ToString() + ":" + id.targetObjectId.ToString(CultureInfo.InvariantCulture) + ":" + id.targetPrefabId.ToString(CultureInfo.InvariantCulture);
        }
        private static string LayoutHash(GameObject root, out bool missing)
        {
            missing = false;
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true);
            writer.Write("PCFO_SCENE_STRUCTURE_V1");
            // Transform traversal includes inactive descendants. Hash includes all component slots; not a semantic review of their writers.
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                writer.Write(SourceId(node.gameObject)); writer.Write(node.GetSiblingIndex()); writer.Write(node.name); writer.Write(node.gameObject.activeSelf);
                writer.Write(node.gameObject.layer); writer.Write(node.gameObject.tag); writer.Write(node.gameObject.isStatic);
                Vector(writer, node.localPosition); Vector(writer, node.localScale);
                var q = node.localRotation; writer.Write(q.x); writer.Write(q.y); writer.Write(q.z); writer.Write(q.w);
                var components = node.GetComponents<Component>(); writer.Write(components.Length);
                foreach (var component in components)
                {
                    if (component == null) { writer.Write("<missing>"); missing = true; continue; }
                    writer.Write(component.GetType().Assembly.GetName().Name + ":" + component.GetType().FullName);
                    if (component is Behaviour behaviour) writer.Write(behaviour.enabled);
                    else if (component is Renderer renderer) writer.Write(renderer.enabled);
                    else if (component is Collider collider) writer.Write(collider.enabled);
                    if (component is NetworkObject no) { writer.Write(no.SynchronizeTransform); writer.Write(no.AutoObjectParentSync); writer.Write(no.ActiveSceneSynchronization); writer.Write(no.SceneMigrationSynchronization); }
                }
            }
            writer.Flush(); using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
        }
        private static void Vector(BinaryWriter writer, Vector3 p) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
    }
}
