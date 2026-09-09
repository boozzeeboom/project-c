using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Read-only source/prefab/open-scene census. Writes only plain reports under docs/.
    /// Heuristic candidates are NOT semantic approval or proof of runtime activation.
    /// Never opens/saves scenes, applies prefab edits, changes components, or starts Play Mode.
    /// </summary>
    public static class FloatingOriginMigrationCensus
    {
        private const string OutputDirectory = "docs/world/floatingorigin";

        private sealed class Rule
        {
            public readonly string Category;
            public readonly Regex Pattern;
            public Rule(string category, string pattern)
            {
                Category = category;
                Pattern = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
            }
        }

        private static readonly Rule[] SourceRules =
        {
            new Rule("spatial-rpc-signature", @"\b(?:public|private|protected|internal)\s+(?:(?:static|virtual|override|async)\s+)*\w+\s+\w*Rpc\s*\([^;{}]*?\b(?:Vector[234]|Quaternion|GlobalPosition)\b[^;{}]*?\)"),
            new Rule("network-variable", @"\bNetworkVariable\s*<[^;\n]+"),
            new Rule("network-serialization", @"\b(?:INetworkSerializable|SerializeValue|NetworkSerialize)\b[^\n]*"),
            new Rule("physics-query", @"\b(?:Physics|Physics2D)\s*\.\s*\w+[^\n]*"),
            new Rule("physics-scene", @"\b(?:PhysicsScene|LocalPhysicsMode|GetPhysicsScene|Simulate)\b[^\n]*"),
            new Rule("navigation", @"\b(?:NavMesh|NavMeshAgent|NavMeshSurface|NavMeshDataInstance|NavMeshLink)\b[^\n]*"),
            new Rule("spatial-member", @"\b(?:public|private|protected|internal)\s+(?:(?:static|readonly|const)\s+)*(?:Vector[234]|Bounds|Ray|NavMeshPath)\??\s+\w+[^\n]*"),
            new Rule("position-access", @"\b\w+\.(?:position|localPosition|destination|nextPosition|worldCenterOfMass)\b[^\n]*"),
            new Rule("point-conversion", @"\b(?:TransformPoint|InverseTransformPoint|WorldToScreenPoint|ScreenPointToRay|SetPositionAndRotation)\s*\([^\n]*"),
            new Rule("lifecycle-placement", @"\b(?:Instantiate|DontDestroyOnLoad|LoadSceneAsync|MoveGameObjectToScene|SpawnAsPlayerObject|TrySetParent|TryRemoveParent)\s*\([^\n]*"),
            new Rule("world-space-effects", @"\b(?:TrailRenderer|LineRenderer|ParticleSystemSimulationSpace|simulationSpace|useWorldSpace|SetPositions|GetParticles|SetParticles)\b[^\n]*"),
            new Rule("legacy-origin", @"\b(?:FloatingOriginMP|WorldSceneManager|WorldStreamingManager|TotalOffset|OnWorldShifted)\b[^\n]*")
        };

        private static readonly Rule ShaderRule = new Rule("shader-position-candidate",
            @"\b(?:worldPos|positionWS|WorldSpace|_WorldSpaceCameraPos|TransformObjectToWorld|GetAbsolutePositionWS)\w*\b[^\n]*");

        [Serializable]
        public sealed class Result
        {
            public int sourceFiles;
            public int shaderFiles;
            public int sourceCandidates;
            public int prefabsScanned;
            public int spatialPrefabObjects;
            public int loadedScenes;
            public int loadedSpatialObjects;
            public string[] errors;
            public string[] reports;
        }

        [MenuItem("ProjectC/World/Floating Origin/Write Migration Census")]
        public static void Execute()
        {
            Result result = Run();
            Debug.Log("[T-FO03] Census only, NOT migration approval: " + JsonUtility.ToJson(result));
        }

        public static Result Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Census requires stable Edit Mode.");

            var errors = new List<string>();
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var sources = new StringBuilder("asset_path,line,category,evidence\n");
            var prefabs = new StringBuilder("asset_path,object_path,active_self,components,network_transform,mesh_local_max_abs\n");
            var scenes = new StringBuilder("scene_path,object_path,active_self,components,network_transform,mesh_local_max_abs\n");
            var result = new Result();
            string[] paths = AssetDatabase.GetAllAssetPaths().OrderBy(p => p, StringComparer.Ordinal).ToArray();

            foreach (string path in paths)
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                bool cs = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                bool shader = path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".cginc", StringComparison.OrdinalIgnoreCase);
                if (!cs && !shader) continue;
                try
                {
                    string text = File.ReadAllText(path);
                    if (cs) result.sourceFiles++; else result.shaderFiles++;
                    var lineStarts = new List<int> { 0 };
                    for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lineStarts.Add(i + 1);
                    foreach (Rule rule in cs ? SourceRules : new[] { ShaderRule })
                    {
                        foreach (Match match in rule.Pattern.Matches(text))
                        {
                            int index = lineStarts.BinarySearch(match.Index);
                            int line = index >= 0 ? index + 1 : ~index;
                            string evidence = Regex.Replace(match.Value, @"\s+", " ").Trim();
                            sources.Append(Csv(path)).Append(',').Append(line).Append(',').Append(Csv(rule.Category))
                                .Append(',').Append(Csv(evidence)).Append('\n');
                            result.sourceCandidates++;
                            counts.TryGetValue(rule.Category, out int n);
                            counts[rule.Category] = n + 1;
                        }
                    }
                }
                catch (Exception e) { errors.Add(path + ": " + e.GetType().Name + ": " + e.Message); }
            }

            // AssetDatabase APIs read prefab assets, never scene YAML or prefab text.
            foreach (string path in paths.Where(p => p.StartsWith("Assets/_Project/", StringComparison.Ordinal) &&
                p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) { errors.Add(path + ": prefab could not be loaded"); continue; }
                    result.prefabsScanned++;
                    foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                        if (AppendSpatialObject(prefabs, path, t)) result.spatialPrefabObjects++;
                }
                catch (Exception e) { errors.Add(path + ": " + e.GetType().Name + ": " + e.Message); }
            }

            // Only already open scenes. Closed WorldScenes are explicitly NOT covered.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                result.loadedScenes++;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        if (AppendSpatialObject(scenes, scene.path, t)) result.loadedSpatialObjects++;
            }

            result.errors = errors.ToArray();
            result.reports = new[]
            {
                OutputDirectory + "/03_SOURCE_CANDIDATES.csv",
                OutputDirectory + "/03_PREFAB_CANDIDATES.csv",
                OutputDirectory + "/03_OPEN_SCENE_CANDIDATES.csv",
                OutputDirectory + "/03_CENSUS_SUMMARY.md"
            };
            var summary = new StringBuilder("# T-FO03 — generated migration census\n\n");
            summary.Append("Date: ").Append(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(".\n\n");
            summary.Append("**Status: CANDIDATES ONLY — NOT semantic coverage, migration readiness, runtime testing, or jitter acceptance.**\n\n");
            summary.Append("```json\n").Append(JsonUtility.ToJson(result, true)).Append("\n```\n\n");
            summary.Append("## Candidate categories\n\n| Category | Matches |\n|---|---:|\n");
            foreach (var entry in counts) summary.Append('|').Append(entry.Key).Append('|').Append(entry.Value).Append("|\n");
            summary.Append("\n## Scope and limitations\n\n");
            summary.Append("- Sources: every imported .cs/.shader/.hlsl/.cginc under Assets, including third-party, Editor, inactive legacy and foundation code. Packages are not part of this source scan.\n");
            summary.Append("- Matching is lexical, includes comments/strings, can duplicate a line under different categories, and is NOT a semantic compiler or call graph. A zero match never proves safety.\n");
            summary.Append("- Prefabs: all .prefab under Assets/_Project via AssetDatabase; nested/inactive objects included. Component records are asset configurations, not runtime authority or lifecycle evidence.\n");
            summary.Append("- Scenes: already loaded scenes only; no scene is opened or saved. Closed WorldScenes, build-only content, Addressables, runtime-created and pooled objects require a later explicit coverage pass.\n");
            summary.Append("- Mesh column is the largest absolute component of local mesh BOUNDS on that object, not a vertex scan, not world bounds and not a measured defect. Empty means no mesh inspected on that object.\n");
            summary.Append("- No object/scene/prefab/meta/package is modified. Reports overwrite only the four named plain-text census files.\n");
            summary.Append("- Every spatial RPC/DTO, persisted target, cache, shader and physics query must still be classified global / frame-local / parent-local / nav-local / direction, with sender/receiver and lifecycle evidence.\n");
            Directory.CreateDirectory(OutputDirectory);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(result.reports[0], sources.ToString(), utf8);
            File.WriteAllText(result.reports[1], prefabs.ToString(), utf8);
            File.WriteAllText(result.reports[2], scenes.ToString(), utf8);
            File.WriteAllText(result.reports[3], summary.ToString(), utf8);
            return result;
        }

        private static bool AppendSpatialObject(StringBuilder output, string path, Transform t)
        {
            Component[] components = t.GetComponents<Component>();
            bool relevant = components.Any(c => c == null || c is NetworkObject || c is NetworkBehaviour ||
                c is Rigidbody || c is Collider || c is Camera || c is ParticleSystem || c is LineRenderer ||
                c is TrailRenderer || c is MeshFilter || c is SkinnedMeshRenderer ||
                c.GetType().Name.StartsWith("NavMesh", StringComparison.Ordinal) ||
                (c is MonoBehaviour && c.GetType().Namespace != null && c.GetType().Namespace.StartsWith("ProjectC", StringComparison.Ordinal)));
            if (!relevant) return false;
            string names = string.Join(";", components.Select(c => c == null ? "MISSING_SCRIPT" : c.GetType().FullName));
            var nt = t.GetComponent<NetworkTransform>();
            string network = nt == null ? "" : "authority=" + nt.AuthorityMode + ";local=" + nt.InLocalSpace +
                ";interpolate=" + nt.Interpolate + ";half=" + nt.UseHalfFloatPrecision;
            // Unity's missing-component wrappers require overloaded == null, not ?. / ??.
            var filter = t.GetComponent<MeshFilter>();
            var skinned = t.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null && skinned != null) mesh = skinned.sharedMesh;
            string localMax = "";
            if (mesh != null)
            {
                Vector3 min = mesh.bounds.min;
                Vector3 max = mesh.bounds.max;
                double largest = new[] { Math.Abs(min.x), Math.Abs(min.y), Math.Abs(min.z), Math.Abs(max.x), Math.Abs(max.y), Math.Abs(max.z) }.Max();
                localMax = largest.ToString("R", CultureInfo.InvariantCulture);
            }
            output.Append(Csv(path)).Append(',').Append(Csv(GetPath(t))).Append(',').Append(t.gameObject.activeSelf ? "true" : "false")
                .Append(',').Append(Csv(names)).Append(',').Append(Csv(network)).Append(',').Append(localMax).Append('\n');
            return true;
        }

        private static string GetPath(Transform t)
        {
            var parts = new Stack<string>();
            for (Transform current = t; current != null; current = current.parent)
                parts.Push(current.name + "[" + current.GetSiblingIndex() + "]");
            return string.Join("/", parts);
        }

        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
