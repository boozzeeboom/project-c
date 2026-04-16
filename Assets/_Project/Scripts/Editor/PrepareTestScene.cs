#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor script to prepare a test scene for Phase 2 World Streaming.
    /// Run this in an empty scene to set up all necessary components.
    /// </summary>
    public class PrepareTestScene : EditorWindow
    {
        [MenuItem("ProjectC/Prepare Test Scene (Phase 2)")]
        public static void ShowWindow()
        {
            GetWindow<PrepareTestScene>("Prepare Test Scene");
        }

        [MenuItem("ProjectC/Prepare Test Scene (Phase 2)", true)]
        public static bool ShowWindowValidation()
        {
            return Application.isPlaying == false;
        }

        private string _sceneName = "ProjectC_ChunkTest_1";
        private bool _includePlayer = true;
        private bool _includeWorldStreaming = true;
        private bool _includeNetworkManager = true;
        private bool _includeFloatingOrigin = true;

        private void OnGUI()
        {
            titleContent = new GUIContent("Prepare Test Scene");

            GUILayout.Label("Phase 2: World Streaming Test Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _sceneName = EditorGUILayout.TextField("Scene Name", _sceneName);
            _includeWorldStreaming = EditorGUILayout.Toggle("World Streaming System", _includeWorldStreaming);
            _includeNetworkManager = EditorGUILayout.Toggle("Network Manager", _includeNetworkManager);
            _includeFloatingOrigin = EditorGUILayout.Toggle("Floating Origin MP", _includeFloatingOrigin);
            _includePlayer = EditorGUILayout.Toggle("Test Player", _includePlayer);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This will create all necessary objects and components for testing Phase 2 World Streaming.\n\n" +
                "Make sure you're in an empty scene before running!",
                MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Test Scene", GUILayout.Height(40)))
            {
                CreateTestScene();
            }
        }

        private void CreateTestScene()
        {
            // Check if we need to save current scene
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Unsaved Scene",
                    "Current scene has unsaved changes. Do you want to save it first?",
                    "Save", "Don't Save", "Cancel");

                if (choice == 2) return; // Cancel
                if (choice == 0) EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo(); // Save
            }

            // Create new empty scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create World Streaming System
            if (_includeWorldStreaming)
            {
                CreateWorldStreamingSystem();
            }

            // Create Network Manager
            if (_includeNetworkManager)
            {
                CreateNetworkManager();
            }

            // Create Floating Origin
            if (_includeFloatingOrigin)
            {
                CreateFloatingOrigin();
            }

            // Create Test Player
            if (_includePlayer)
            {
                CreateTestPlayer();
            }

            // Create World Root
            CreateWorldRoot();

            // Create Chunks Container
            CreateChunksContainer();

            // Create Lighting
            CreateLighting();

            // Mark scene as dirty so it can be saved
            EditorSceneManager.MarkSceneDirty(newScene);

            // Save scene
            string scenePath = $"Assets/_Project/Scenes/Test/{_sceneName}.unity";
            
            // Ensure directory exists
            string directory = System.IO.Path.GetDirectoryName(scenePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Test Scene Created",
                $"Test scene created successfully!\n\nScene: {_sceneName}\nPath: {scenePath}",
                "OK");

            Debug.Log($"[PrepareTestScene] Test scene created: {scenePath}");
        }

        /// <summary>
        /// Helper method to add component using reflection-based type lookup.
        /// </summary>
        private static Type GetTypeByName(string typeName)
        {
            // Try to find in all loaded assemblies
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName);
                    if (type != null)
                        return type;
                    
                    // Also try with ProjectC prefix
                    type = assembly.GetType("ProjectC." + typeName);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // Skip assemblies that can't be inspected
                }
            }
            return null;
        }

        private void CreateWorldStreamingSystem()
        {
            GameObject streamingObj = new GameObject("WorldStreamingManager");
            streamingObj.transform.position = Vector3.zero;

            // Add components using helper method
            AddComponentByName(streamingObj, "ProjectC.World.Streaming.WorldChunkManager");
            AddComponentByName(streamingObj, "ProjectC.World.Streaming.ChunkLoader");
            AddComponentByName(streamingObj, "ProjectC.World.Streaming.ProceduralChunkGenerator");
            AddComponentByName(streamingObj, "ProjectC.World.Streaming.PlayerChunkTracker");
            AddComponentByName(streamingObj, "ProjectC.World.Streaming.ChunkNetworkSpawner");
            AddComponentByName(streamingObj, "ProjectC.World.WorldStreamingManager");

            // Configure PlayerChunkTracker via SerializedObject
            ConfigureComponent(streamingObj, "ProjectC.World.Streaming.PlayerChunkTracker", "showDebugLogs", true);

            // Configure ChunkNetworkSpawner via SerializedObject
            ConfigureComponent(streamingObj, "ProjectC.World.Streaming.ChunkNetworkSpawner", "showDebugLogs", true);

            // Configure WorldStreamingManager
            ConfigureComponent(streamingObj, "ProjectC.World.WorldStreamingManager", "showDebugHUD", true);
            ConfigureComponent(streamingObj, "ProjectC.World.WorldStreamingManager", "preloadLayers", 1);
            ConfigureComponent(streamingObj, "ProjectC.World.WorldStreamingManager", "preloadDelay", 1f);
            ConfigureComponent(streamingObj, "ProjectC.World.WorldStreamingManager", "preloadChunkInterval", 0.3f);

            Debug.Log("[PrepareTestScene] World Streaming System created");
        }

        private void AddComponentByName(GameObject obj, string typeName)
        {
            Type componentType = GetTypeByName(typeName);
            if (componentType != null)
            {
                var component = obj.AddComponent(componentType);
                if (component != null)
                {
                    Debug.Log($"[PrepareTestScene] Added {typeName}");
                }
                else
                {
                    Debug.LogWarning($"[PrepareTestScene] Failed to add {typeName}");
                }
            }
            else
            {
                Debug.LogError($"[PrepareTestScene] Type not found: {typeName}");
            }
        }

        private void ConfigureComponent(GameObject obj, string typeName, string propertyName, object value)
        {
            Type componentType = GetTypeByName(typeName);
            if (componentType == null)
            {
                Debug.LogError($"[PrepareTestScene] Type not found for configuration: {typeName}");
                return;
            }

            var component = obj.GetComponent(componentType);
            if (component == null)
            {
                Debug.LogWarning($"[PrepareTestScene] Component not found for configuration: {typeName}");
                return;
            }

            SerializedObject so = new SerializedObject(component);
            var property = so.FindProperty(propertyName);
            if (property != null)
            {
                if (value is int)
                    property.intValue = (int)value;
                else if (value is float)
                    property.floatValue = (float)value;
                else if (value is bool)
                    property.boolValue = (bool)value;
                
                so.ApplyModifiedProperties();
                Debug.Log($"[PrepareTestScene] Configured {typeName}.{propertyName} = {value}");
            }
            else
            {
                Debug.LogWarning($"[PrepareTestScene] Property '{propertyName}' not found on {typeName}");
            }
        }

        private void CreateChunksContainer()
        {
            if (GameObject.Find("ChunksContainer") != null) return;
            
            GameObject container = new GameObject("ChunksContainer");
            container.transform.position = Vector3.zero;
            Debug.Log("[PrepareTestScene] ChunksContainer created");
        }

        private void CreateNetworkManager()
        {
            // Check if NetworkManager already exists
            if (FindAnyObjectByType<Unity.Netcode.NetworkManager>() != null)
            {
                Debug.Log("[PrepareTestScene] NetworkManager already exists, skipping...");
                return;
            }

            // Create NetworkManager object
            GameObject networkObj = new GameObject("NetworkManager");
            var networkManager = networkObj.AddComponent<Unity.Netcode.NetworkManager>();

            // Add NetworkTransport if needed
            var transport = networkObj.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            networkManager.NetworkConfig.NetworkTransport = transport;

            Debug.Log("[PrepareTestScene] NetworkManager created");
        }

        private void CreateFloatingOrigin()
        {
            // Find or create camera
            Camera mainCamera = FindAnyObjectByType<Camera>();
            if (mainCamera == null)
            {
                GameObject camObj = new GameObject("MainCamera");
                mainCamera = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            // Add ThirdPersonCamera for camera follow
            AddComponentByName(mainCamera.gameObject, "ProjectC.Core.ThirdPersonCamera");

            // Add FloatingOriginMP to camera using reflection
            Type floatingOriginType = GetTypeByName("ProjectC.World.Streaming.FloatingOriginMP");
            if (floatingOriginType == null)
            {
                Debug.LogError("[PrepareTestScene] FloatingOriginMP type not found!");
                return;
            }

            var existing = mainCamera.GetComponent(floatingOriginType);
            if (existing == null)
            {
                mainCamera.gameObject.AddComponent(floatingOriginType);
            }

            // Configure via SerializedObject
            var component = mainCamera.GetComponent(floatingOriginType);
            if (component != null)
            {
                SerializedObject floatingOriginSO = new SerializedObject(component);
                
                var foShowDebugLogsProp = floatingOriginSO.FindProperty("showDebugLogs");
                if (foShowDebugLogsProp != null) foShowDebugLogsProp.boolValue = true;
                
                var foShowDebugHUDProp = floatingOriginSO.FindProperty("showDebugHUD");
                if (foShowDebugHUDProp != null) foShowDebugHUDProp.boolValue = true;
                
                floatingOriginSO.ApplyModifiedProperties();
            }

            Debug.Log("[PrepareTestScene] FloatingOriginMP created on camera");
        }

        private void CreateTestPlayer()
        {
            // Create player object
            GameObject playerObj = new GameObject("TestPlayer");
            playerObj.transform.position = new Vector3(0, 2, 0);

            // Add CharacterController
            var controller = playerObj.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0, 1, 0);

            // Add NetworkObject
            var networkObject = playerObj.AddComponent<Unity.Netcode.NetworkObject>();

            // Add PlayerController for WASD movement
            AddComponentByName(playerObj, "ProjectC.Player.PlayerController");

            // Create simple placeholder player
            CreateSimplePlayerPlaceholder(playerObj);

            // Configure ThirdPersonCamera to follow this player
            ConfigureThirdPersonCamera(playerObj.transform);

            Debug.Log("[PrepareTestScene] Test Player created");
        }

        private void ConfigureThirdPersonCamera(Transform playerTransform)
        {
            // Find ThirdPersonCamera in scene
            var tpc = FindAnyObjectByType<ProjectC.Core.ThirdPersonCamera>();
            if (tpc != null)
            {
                SerializedObject tpcSO = new SerializedObject(tpc);
                var targetProp = tpcSO.FindProperty("target");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = playerTransform;
                    tpcSO.ApplyModifiedProperties();
                    Debug.Log("[PrepareTestScene] ThirdPersonCamera.target set to TestPlayer");
                }
                else
                {
                    Debug.LogWarning("[PrepareTestScene] 'target' property not found on ThirdPersonCamera");
                }
            }
            else
            {
                Debug.LogWarning("[PrepareTestScene] ThirdPersonCamera not found in scene");
            }
        }

        private void CreateSimplePlayerPlaceholder(GameObject parent)
        {
            // Create simple capsule for player body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(parent.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one * 0.5f;
            
            // Remove collider from body (CharacterController handles it)
            var col = body.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            Debug.Log("[PrepareTestScene] Created simple player placeholder");
        }

        private void CreateWorldRoot()
        {
            if (GameObject.Find("WorldRoot") != null)
            {
                Debug.Log("[PrepareTestScene] WorldRoot already exists, skipping...");
                return;
            }

            GameObject worldRoot = new GameObject("WorldRoot");
            worldRoot.transform.position = Vector3.zero;

            // Create sub-roots for different world elements
            CreateWorldSubRoot(worldRoot, "Mountains");
            CreateWorldSubRoot(worldRoot, "Clouds");
            CreateWorldSubRoot(worldRoot, "Farms");
            CreateWorldSubRoot(worldRoot, "TradeZones");

            Debug.Log("[PrepareTestScene] WorldRoot created with sub-roots");
        }

        private void CreateWorldSubRoot(GameObject parent, string name)
        {
            GameObject subRoot = new GameObject(name);
            subRoot.transform.SetParent(parent.transform);
            subRoot.transform.position = Vector3.zero;
        }

        private void CreateLighting()
        {
            // Check if light exists
            if (FindAnyObjectByType<Light>() != null)
            {
                return;
            }

            GameObject lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            Debug.Log("[PrepareTestScene] Lighting created");
        }
    }
}
#endif