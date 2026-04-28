#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.World.Scene;

namespace ProjectC.Editor
{
    public class WorldSceneGenerator : EditorWindow
    {
        private const int ROWS = 4;
        private const int COLS = 6;
        private const float SCENE_SIZE = 79999f;
        private const float GROUND_OFFSET = 0f;

        private string _outputPath = "Assets/_Project/Scenes/World";
        private bool _generateBoundaryColliders = true;
        private bool _generateGroundPlane = true;
        private bool _generateLabels = true;
        private bool _generateLighting = true;
        private bool _addToBuildSettings = true;

        private Material _groundMaterial;
        private string _materialPath = "Assets/_Project/Materials/World/WorldGroundMaterial.mat";

        [MenuItem("ProjectC/World/Generate World Scenes")]
        public static void ShowWindow()
        {
            var window = GetWindow<WorldSceneGenerator>("World Scene Generator");
            window.minSize = new Vector2(500, 500);
        }

        public void OnGUI()
        {
            GUILayout.Label("World Scene Generator (4x6 Grid)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);

            EditorGUILayout.Space();
            GUILayout.Label("Scene Content:", EditorStyles.boldLabel);
            _generateGroundPlane = EditorGUILayout.Toggle("Ground Plane", _generateGroundPlane);
            _generateBoundaryColliders = EditorGUILayout.Toggle("Boundary Colliders", _generateBoundaryColliders);
            _generateLabels = EditorGUILayout.Toggle("Scene Labels", _generateLabels);
            _generateLighting = EditorGUILayout.Toggle("Directional Light", _generateLighting);

            EditorGUILayout.Space();
            GUILayout.Label("Build Settings:", EditorStyles.boldLabel);
            _addToBuildSettings = EditorGUILayout.Toggle("Add to Build Settings", _addToBuildSettings);

            EditorGUILayout.Space();
            GUILayout.Label("Flight Altitude Range: Y = 1000 to 6500", EditorStyles.helpBox);
            EditorGUILayout.LabelField("Row 0: Equator (wraps left-right)");
            EditorGUILayout.LabelField("Row 1-2: Temperate bands");
            EditorGUILayout.LabelField("Row 3: Poles (blocked from row 0)");

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate All Scenes", GUILayout.Height(40)))
            {
                GenerateAllScenes();
            }

            if (GUILayout.Button("Generate SceneRegistry Only", GUILayout.Height(25)))
            {
                CreateSceneRegistry();
                EditorUtility.DisplayDialog("Complete", "SceneRegistry created.", "OK");
            }
        }

        private void GenerateAllScenes()
        {
            if (!EditorUtility.DisplayDialog("Generate World Scenes",
                $"This will create {ROWS * COLS} scenes in {_outputPath}.\nExisting scenes will be overwritten!",
                "Generate", "Cancel"))
            {
                return;
            }

            CreateOutputDirectory();
            CreateGroundMaterial();
            CreateSceneRegistry();

            List<string> scenePaths = new List<string>();

            for (int row = 0; row < ROWS; row++)
            {
                for (int col = 0; col < COLS; col++)
                {
                    string scenePath = $"{_outputPath}/WorldScene_{row}_{col}.unity";
                    scenePaths.Add(scenePath);

                    EditorUtility.DisplayProgressBar(
                        "Generating Scenes",
                        $"Creating scene {row},{col}...",
                        (float)(row * COLS + col) / (ROWS * COLS));

                    CreateScene(row, col, scenePath);
                }
            }

            EditorUtility.ClearProgressBar();

            if (_addToBuildSettings)
            {
                AddScenesToBuildSettings(scenePaths);
            }

            EditorUtility.DisplayDialog("Complete",
                $"{ROWS * COLS} scenes generated successfully!\nSaved to: {_outputPath}",
                "OK");

            AssetDatabase.Refresh();
        }

        private void CreateOutputDirectory()
        {
            if (!AssetDatabase.IsValidFolder(_outputPath))
            {
                string parentPath = System.IO.Path.GetDirectoryName(_outputPath);
                if (!AssetDatabase.IsValidFolder(parentPath))
                {
                    AssetDatabase.CreateFolder("Assets/_Project/Scenes", "World");
                }
                AssetDatabase.CreateFolder(parentPath, "World");
            }

            string materialDir = System.IO.Path.GetDirectoryName(_materialPath);
            if (!AssetDatabase.IsValidFolder(materialDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "World");
            }
        }

        private void CreateScene(int row, int col, string scenePath)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            GameObject worldRoot = new GameObject($"WorldRoot_{row}_{col}");
            worldRoot.transform.position = Vector3.zero;

            if (_generateLighting)
            {
                CreateDirectionalLight(worldRoot.transform, row, col);
            }

            if (_generateGroundPlane)
            {
                CreateGroundPlane(worldRoot.transform, row, col);
            }

            if (_generateLabels)
            {
                CreateSceneLabel(worldRoot.transform, row, col);
            }

            if (_generateBoundaryColliders)
            {
                CreateBoundaryColliders(worldRoot.transform, row, col);
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        private void CreateDirectionalLight(Transform parent, int row, int col)
        {
            GameObject lightObj = new GameObject("DirectionalLight");
            lightObj.transform.SetParent(parent);
            lightObj.transform.position = new Vector3(col * SCENE_SIZE + SCENE_SIZE / 2f, 100f, row * SCENE_SIZE + SCENE_SIZE / 2f);

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.05f;
            light.shadowNearPlane = 1f;
            light.shadows = LightShadows.Soft;

            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private void CreateGroundPlane(Transform parent, int row, int col)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = $"GroundPlane_{row}_{col}";
            ground.transform.SetParent(parent);

            float centerX = col * SCENE_SIZE + SCENE_SIZE / 2f;
            float centerZ = row * SCENE_SIZE + SCENE_SIZE / 2f;
            ground.transform.position = new Vector3(centerX, GROUND_OFFSET, centerZ);

            float scale = SCENE_SIZE / 10f;
            ground.transform.localScale = new Vector3(scale, 1f, scale);

            if (_groundMaterial != null)
            {
                Renderer renderer = ground.GetComponent<Renderer>();
                renderer.sharedMaterial = _groundMaterial;
            }

            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
        }

        private void CreateGroundMaterial()
        {
            if (_groundMaterial != null) return;

            _groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(_materialPath);
            if (_groundMaterial != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("HDRP/Lit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _groundMaterial = new Material(shader);

            Color groundColor = GetRowColor(0);
            _groundMaterial.color = groundColor;

            _groundMaterial.SetFloat("_Metallic", 0f);
            _groundMaterial.SetFloat("_Smoothness", 0.1f);

            string dir = System.IO.Path.GetDirectoryName(_materialPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "World");
            }

            AssetDatabase.CreateAsset(_groundMaterial, _materialPath);
            AssetDatabase.SaveAssets();
        }

        private Color GetRowColor(int row)
        {
            return row switch
            {
                0 => new Color(0.2f, 0.4f, 0.2f, 1f),
                1 => new Color(0.3f, 0.5f, 0.3f, 1f),
                2 => new Color(0.4f, 0.5f, 0.4f, 1f),
                3 => new Color(0.7f, 0.7f, 0.8f, 1f),
                _ => Color.gray
            };
        }

        private void CreateSceneLabel(Transform parent, int row, int col)
        {
            GameObject labelObj = new GameObject($"SceneLabel_{row}_{col}");
            labelObj.transform.SetParent(parent);

            float centerX = col * SCENE_SIZE + SCENE_SIZE / 2f;
            float centerZ = row * SCENE_SIZE + SCENE_SIZE / 2f;
            labelObj.transform.position = new Vector3(centerX, 50f, centerZ);

            TMPro.TextMeshPro textMesh = labelObj.AddComponent<TMPro.TextMeshPro>();
            textMesh.text = $"Scene {row},{col}\nWorld: ({centerX:F0}, {centerZ:F0})";
            textMesh.fontSize = 100f;
            textMesh.alignment = TMPro.TextAlignmentOptions.Center;
            textMesh.color = Color.white;

            labelObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void CreateBoundaryColliders(Transform parent, int row, int col)
        {
            GameObject boundaries = new GameObject($"Boundaries_{row}_{col}");
            boundaries.transform.SetParent(parent);
            boundaries.transform.position = Vector3.zero;

            float centerX = col * SCENE_SIZE + SCENE_SIZE / 2f;
            float centerZ = row * SCENE_SIZE + SCENE_SIZE / 2f;
            float halfSize = SCENE_SIZE / 2f;
            float thickness = 100f;
            float height = 1000f;

            CreateBoundaryCollider(boundaries.transform, "North",
                new Vector3(centerX, height / 2f, centerZ + halfSize),
                new Vector3(SCENE_SIZE + thickness * 2, height, thickness), row == ROWS - 1);

            CreateBoundaryCollider(boundaries.transform, "South",
                new Vector3(centerX, height / 2f, centerZ - halfSize),
                new Vector3(SCENE_SIZE + thickness * 2, height, thickness), row == 0);

            CreateBoundaryCollider(boundaries.transform, "East",
                new Vector3(centerX + halfSize, height / 2f, centerZ),
                new Vector3(thickness, height, SCENE_SIZE), false);

            CreateBoundaryCollider(boundaries.transform, "West",
                new Vector3(centerX - halfSize, height / 2f, centerZ),
                new Vector3(thickness, height, SCENE_SIZE), false);

            CreatePoleBlocker(boundaries.transform, "SouthPoleBlocker",
                new Vector3(centerX, height / 2f, centerZ - halfSize - thickness),
                new Vector3(SCENE_SIZE + thickness * 4, height, thickness * 2), row == 0);

            CreatePoleBlocker(boundaries.transform, "NorthPoleBlocker",
                new Vector3(centerX, height / 2f, centerZ + halfSize + thickness),
                new Vector3(SCENE_SIZE + thickness * 4, height, thickness * 2), row == ROWS - 1);
        }

        private void CreateBoundaryCollider(Transform parent, string name, Vector3 position, Vector3 size, bool isBlockedPole)
        {
            GameObject colliderObj = new GameObject(name);
            colliderObj.transform.SetParent(parent);
            colliderObj.transform.position = position;

            BoxCollider collider = colliderObj.AddComponent<BoxCollider>();
            collider.size = size;

            if (isBlockedPole)
            {
                collider.isTrigger = true;
                Rigidbody rb = colliderObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;

                colliderObj.AddComponent<PoleBlockerComponent>();
            }

            CreateBoundaryVisualization(colliderObj.transform, size, isBlockedPole ? Color.red : Color.yellow);
        }

        private void CreatePoleBlocker(Transform parent, string name, Vector3 position, Vector3 size, bool isActive)
        {
            if (!isActive) return;

            GameObject blockerObj = new GameObject(name);
            blockerObj.transform.SetParent(parent);
            blockerObj.transform.position = position;

            BoxCollider collider = blockerObj.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;

            Rigidbody rb = blockerObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            blockerObj.AddComponent<PoleBlockerComponent>();

            CreateBoundaryVisualization(blockerObj.transform, size, Color.red);
        }

        private void CreateBoundaryVisualization(Transform parent, Vector3 size, Color color)
        {
            GameObject vizObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vizObj.name = "BoundaryViz";
            vizObj.transform.SetParent(parent);
            vizObj.transform.localPosition = Vector3.zero;
            vizObj.transform.localRotation = Quaternion.identity;
            vizObj.transform.localScale = size;

            Renderer renderer = vizObj.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            Color c = color;
            c.a = color == Color.red ? 0.3f : 0.1f;
            mat.color = c;
            renderer.sharedMaterial = mat;

            UnityEngine.Object.DestroyImmediate(vizObj.GetComponent<Collider>());
        }

        private void CreateSceneRegistry()
        {
            string registryPath = "Assets/_Project/Data/Scene/SceneRegistry.asset";
            string registryDir = System.IO.Path.GetDirectoryName(registryPath);

            if (!AssetDatabase.IsValidFolder(registryDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Scene");
            }

            SceneRegistry registry = AssetDatabase.LoadAssetAtPath<SceneRegistry>(registryPath);

            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<SceneRegistry>();
                AssetDatabase.CreateAsset(registry, registryPath);
            }

            registry.GridRows = ROWS;
            registry.GridColumns = COLS;

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            Debug.Log($"[WorldSceneGenerator] SceneRegistry created at {registryPath}");
        }

        private void AddScenesToBuildSettings(List<string> scenePaths)
        {
            EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;
            List<EditorBuildSettingsScene> allScenes = new List<EditorBuildSettingsScene>(existingScenes);

            HashSet<string> existingPaths = new HashSet<string>();
            foreach (var s in existingScenes)
            {
                existingPaths.Add(s.path);
            }

            foreach (string path in scenePaths)
            {
                if (!existingPaths.Contains(path))
                {
                    allScenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = allScenes.ToArray();
        }

        [ContextMenu("Regenerate Material")]
        public void RegenerateMaterial()
        {
            if (_groundMaterial != null)
            {
                DestroyImmediate(_groundMaterial);
                _groundMaterial = null;
            }
            CreateGroundMaterial();
        }

        [ContextMenu("Regenerate All")]
        public void RegenerateAll()
        {
            GenerateAllScenes();
        }
    }

    public class PoleBlockerComponent : MonoBehaviour
    {
        [Tooltip("If true, player cannot cross this boundary")]
        public bool blocksTransition = true;
    }
}
#endif