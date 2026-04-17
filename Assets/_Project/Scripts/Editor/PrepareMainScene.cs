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
    /// Editor script to prepare the main scene (ProjectC_1) for World Streaming Phase 2.
    /// Adds all necessary components and configures FloatingOriginMP.
    /// </summary>
    public class PrepareMainScene : EditorWindow
    {
        [MenuItem("ProjectC/Prepare Main Scene (Phase 2)")]
        public static void ShowWindow()
        {
            GetWindow<PrepareMainScene>("Prepare Main Scene");
        }

        [MenuItem("ProjectC/Prepare Main Scene (Phase 2)", true)]
        public static bool ShowWindowValidation()
        {
            return Application.isPlaying == false;
        }

        private bool _configureWorldStreaming = true;
        private bool _configureFloatingOrigin = true;
        private bool _addWorldRoot = true;
        private bool _configureStreamingTest = true;
        private bool _addDebugSettings = true;

        // WorldRoot settings
        // TradeZones ИСКЛЮЧЁН — там камера игрока!
        private string[] _worldRootNames = new string[]
        {
            "WorldRoot",
            "Mountains",
            "Clouds",
            "Farms",
            "World",
            "ChunksContainer",
            "Platforms",
            "CloudLayer"
        };

        private void OnGUI()
        {
            titleContent = new GUIContent("Prepare Main Scene");
            GUILayout.Label("Phase 2: Main Scene World Streaming Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _configureWorldStreaming = EditorGUILayout.Toggle("Configure World Streaming", _configureWorldStreaming);
            _configureFloatingOrigin = EditorGUILayout.Toggle("Configure Floating Origin MP", _configureFloatingOrigin);
            _addWorldRoot = EditorGUILayout.Toggle("Ensure WorldRoot Exists", _addWorldRoot);
            _configureStreamingTest = EditorGUILayout.Toggle("Add StreamingTest Component", _configureStreamingTest);
            _addDebugSettings = EditorGUILayout.Toggle("Enable Debug Settings", _addDebugSettings);

            EditorGUILayout.Space();
            
            GUILayout.Label("WorldRoot Objects to Track:", EditorStyles.boldLabel);
            foreach (var name in _worldRootNames)
            {
                EditorGUILayout.LabelField("  - " + name);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This will configure the current scene for World Streaming:\n" +
                "- Adds WorldStreamingManager components if missing\n" +
                "- Configures FloatingOriginMP with correct world roots\n" +
                "- Ensures WorldRoot object exists\n" +
                "- Adds StreamingTest for F-key controls",
                MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Apply to Current Scene", GUILayout.Height(40)))
                ApplyToCurrentScene();
        }

        private void ApplyToCurrentScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Error", "Please save the scene first!", "OK");
                return;
            }

            if (_addWorldRoot)
                EnsureWorldRootExists();

            if (_configureWorldStreaming)
                ConfigureWorldStreaming();

            if (_configureFloatingOrigin)
                ConfigureFloatingOrigin();

            if (_configureStreamingTest)
                AddStreamingTest();

            if (_addDebugSettings)
                ApplyDebugSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[PrepareMainScene] Scene configured: {scene.name}");
            EditorUtility.DisplayDialog("Success", $"Scene '{scene.name}' has been configured for World Streaming Phase 2.", "OK");
        }

        private static Type GetTypeByName(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName);
                    if (type != null) return type;
                    type = assembly.GetType("ProjectC." + typeName);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        private void EnsureWorldRootExists()
        {
            // Find or create WorldRoot
            GameObject worldRoot = GameObject.Find("WorldRoot");
            
            if (worldRoot == null)
            {
                // Try to find Mountains, Clouds, etc.
                foreach (var name in _worldRootNames)
                {
                    GameObject existing = GameObject.Find(name);
                    if (existing != null)
                    {
                        // Use existing object as WorldRoot
                        worldRoot = existing;
                        worldRoot.name = "WorldRoot"; // Rename to standard name
                        Debug.Log($"[PrepareMainScene] Using '{name}' as WorldRoot");
                        break;
                    }
                }
            }

            if (worldRoot == null)
            {
                worldRoot = new GameObject("WorldRoot");
                Debug.Log("[PrepareMainScene] Created new WorldRoot");
            }

            // Ensure it's at origin
            worldRoot.transform.position = Vector3.zero;

            // Make Mountains, Clouds, Farms children of WorldRoot
            string[] childNames = { "Mountains", "Clouds", "Farms", "TradeZones" };
            foreach (var childName in childNames)
            {
                GameObject child = GameObject.Find(childName);
                if (child != null && child.transform.parent != worldRoot.transform)
                {
                    child.transform.SetParent(worldRoot.transform);
                    Debug.Log($"[PrepareMainScene] Made '{childName}' child of WorldRoot");
                }
            }
        }

        private void ConfigureWorldStreaming()
        {
            // Find or create WorldStreamingManager object
            GameObject streamingObj = GameObject.Find("WorldStreamingManager");
            if (streamingObj == null)
            {
                streamingObj = new GameObject("WorldStreamingManager");
                Debug.Log("[PrepareMainScene] Created WorldStreamingManager object");
            }

            // Add required components if missing
            AddComponentIfMissing(streamingObj, "ProjectC.World.Streaming.WorldChunkManager");
            AddComponentIfMissing(streamingObj, "ProjectC.World.Streaming.ChunkLoader");
            AddComponentIfMissing(streamingObj, "ProjectC.World.Streaming.ProceduralChunkGenerator");
            AddComponentIfMissing(streamingObj, "ProjectC.World.Streaming.PlayerChunkTracker");
            AddComponentIfMissing(streamingObj, "ProjectC.World.Streaming.ChunkNetworkSpawner");
            AddComponentIfMissing(streamingObj, "ProjectC.World.WorldStreamingManager");

            Debug.Log("[PrepareMainScene] World Streaming components configured");
        }

        private void ConfigureFloatingOrigin()
        {
            // Find or create FloatingOriginMP
            GameObject foObj = FindOrCreateFloatingOrigin();

            // Ensure it has Camera component
            Camera cam = foObj.GetComponent<Camera>();
            if (cam == null)
            {
                // Find main camera
                Camera mainCam = Camera.main;
                if (mainCam != null && mainCam.gameObject != foObj)
                {
                    // Add FloatingOriginMP to main camera
                    DestroyImmediate(foObj, true);
                    foObj = mainCam.gameObject;
                    Debug.Log("[PrepareMainScene] Moving FloatingOriginMP to Main Camera");
                }
                else
                {
                    cam = foObj.AddComponent<Camera>();
                    Debug.Log("[PrepareMainScene] Added Camera component to FloatingOriginMP");
                }
            }

            // Configure FloatingOriginMP
            var foType = GetTypeByName("ProjectC.World.Streaming.FloatingOriginMP");
            if (foType != null)
            {
                var fo = foObj.GetComponent(foType);
                if (fo != null)
                {
                    // Set mode to Local for now (can be changed for multiplayer)
                    SerializedObject serializedObject = new SerializedObject(fo);
                    
                    SerializedProperty thresholdProp = serializedObject.FindProperty("threshold");
                    if (thresholdProp != null)
                        thresholdProp.floatValue = 100000f;

                    SerializedProperty shiftRoundingProp = serializedObject.FindProperty("shiftRounding");
                    if (shiftRoundingProp != null)
                        shiftRoundingProp.floatValue = 10000f;

                    SerializedProperty worldRootNamesProp = serializedObject.FindProperty("worldRootNames");
                    if (worldRootNamesProp != null)
                    {
                        worldRootNamesProp.ClearArray();
                        foreach (var name in _worldRootNames)
                        {
                            worldRootNamesProp.InsertArrayElementAtIndex(worldRootNamesProp.arraySize);
                            worldRootNamesProp.GetArrayElementAtIndex(worldRootNamesProp.arraySize - 1).stringValue = name;
                        }
                    }

                    SerializedProperty showDebugLogsProp = serializedObject.FindProperty("showDebugLogs");
                    if (showDebugLogsProp != null)
                        showDebugLogsProp.boolValue = true;

                    serializedObject.ApplyModifiedProperties();
                    
                    Debug.Log("[PrepareMainScene] FloatingOriginMP configured with threshold=100000, worldRootNames");
                }
            }
        }

        private GameObject FindOrCreateFloatingOrigin()
        {
            // First try to find existing
            var foType = GetTypeByName("ProjectC.World.Streaming.FloatingOriginMP");
            if (foType != null)
            {
                var existing = UnityEngine.Object.FindAnyObjectByType(foType);
                if (existing != null)
                    return ((Component)existing).gameObject;
            }

            // Find Main Camera
            GameObject mainCam = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCam != null)
            {
                AddComponentIfMissing(mainCam, "ProjectC.World.Streaming.FloatingOriginMP");
                return mainCam;
            }

            // Create new with camera
            GameObject foObj = new GameObject("FloatingOriginMP");
            foObj.AddComponent<Camera>();
            AddComponentByName(foObj, "ProjectC.World.Streaming.FloatingOriginMP");
            Debug.Log("[PrepareMainScene] Created new FloatingOriginMP object");

            return foObj;
        }

        private void AddStreamingTest()
        {
            // Find or create StreamingTest object
            GameObject testObj = GameObject.Find("StreamingTest");
            if (testObj == null)
            {
                testObj = new GameObject("StreamingTest");
                Debug.Log("[PrepareMainScene] Created StreamingTest object");
            }

            // Add component
            AddComponentIfMissing(testObj, "ProjectC.World.StreamingTest");

            // Configure
            var testType = GetTypeByName("ProjectC.World.StreamingTest");
            if (testType != null)
            {
                var test = testObj.GetComponent(testType);
                if (test != null)
                {
                    SerializedObject serializedObject = new SerializedObject(test);
                    
                    // Enable local player tracking
                    SerializedProperty useLocalPlayerProp = serializedObject.FindProperty("useLocalPlayerPosition");
                    if (useLocalPlayerProp != null)
                        useLocalPlayerProp.boolValue = true;

                    // Enable player teleport
                    SerializedProperty teleportPlayerProp = serializedObject.FindProperty("teleportPlayer");
                    if (teleportPlayerProp != null)
                        teleportPlayerProp.boolValue = true;

                    // Find WorldStreamingManager reference
                    GameObject wsmObj = GameObject.Find("WorldStreamingManager");
                    if (wsmObj != null)
                    {
                        SerializedProperty streamingManagerProp = serializedObject.FindProperty("streamingManager");
                        if (streamingManagerProp != null)
                        {
                            var wsmType = GetTypeByName("ProjectC.World.WorldStreamingManager");
                            if (wsmType != null)
                            {
                                var wsm = wsmObj.GetComponent(wsmType);
                                if (wsm != null)
                                    streamingManagerProp.objectReferenceValue = wsm;
                            }
                        }
                    }

                    serializedObject.ApplyModifiedProperties();
                    Debug.Log("[PrepareMainScene] StreamingTest configured");
                }
            }
        }

        private void AddComponentIfMissing(GameObject obj, string typeName)
        {
            Type componentType = GetTypeByName(typeName);
            if (componentType == null)
            {
                Debug.LogError($"[PrepareMainScene] Type not found: {typeName}");
                return;
            }

            var existing = obj.GetComponent(componentType);
            if (existing == null)
            {
                var component = obj.AddComponent(componentType);
                if (component != null)
                    Debug.Log($"[PrepareMainScene] Added {typeName} to {obj.name}");
            }
        }

        private void AddComponentByName(GameObject obj, string typeName)
        {
            Type componentType = GetTypeByName(typeName);
            if (componentType != null)
            {
                var component = obj.AddComponent(componentType);
                if (component != null)
                    Debug.Log($"[PrepareMainScene] Added {typeName}");
            }
            else
                Debug.LogError($"[PrepareMainScene] Type not found: {typeName}");
        }

        private void ApplyDebugSettings()
        {
            // Enable debug on WorldStreamingManager
            GameObject wsmObj = GameObject.Find("WorldStreamingManager");
            if (wsmObj != null)
            {
                var wsmType = GetTypeByName("ProjectC.World.WorldStreamingManager");
                if (wsmType != null)
                {
                    var wsm = wsmObj.GetComponent(wsmType);
                    if (wsm != null)
                    {
                        SerializedObject so = new SerializedObject(wsm);
                        SerializedProperty prop = so.FindProperty("showDebugHUD");
                        if (prop != null)
                            prop.boolValue = true;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            // Enable debug on FloatingOriginMP
            var foType = GetTypeByName("ProjectC.World.Streaming.FloatingOriginMP");
            if (foType != null)
            {
                var foObj = FindOrCreateFloatingOrigin();
                var fo = foObj.GetComponent(foType);
                if (fo != null)
                {
                    SerializedObject so = new SerializedObject(fo);
                    SerializedProperty prop = so.FindProperty("showDebugLogs");
                    if (prop != null)
                        prop.boolValue = true;
                    prop = so.FindProperty("showDebugHUD");
                    if (prop != null)
                        prop.boolValue = true;
                    so.ApplyModifiedProperties();
                }
            }

            // Enable debug on PlayerChunkTracker
            if (wsmObj != null)
            {
                var pctType = GetTypeByName("ProjectC.World.Streaming.PlayerChunkTracker");
                if (pctType != null)
                {
                    var pct = wsmObj.GetComponent(pctType);
                    if (pct != null)
                    {
                        SerializedObject so = new SerializedObject(pct);
                        SerializedProperty prop = so.FindProperty("showDebugLogs");
                        if (prop != null)
                            prop.boolValue = true;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            Debug.Log("[PrepareMainScene] Debug settings applied");
        }

        /// <summary>
        /// Quick fix for FloatingOriginMP - finds all world objects and parents them to WorldRoot.
        /// Call this from menu or from code.
        /// </summary>
        [MenuItem("ProjectC/Fix WorldRoot Hierarchy")]
        public static void FixWorldRootHierarchy()
        {
            // Find or create WorldRoot
            GameObject worldRoot = GameObject.Find("WorldRoot");
            if (worldRoot == null)
            {
                worldRoot = new GameObject("WorldRoot");
                Debug.Log("[PrepareMainScene] Created new WorldRoot");
            }

            worldRoot.transform.position = Vector3.zero;

            // Objects that should be children of WorldRoot
            string[] objectNames = { "Mountains", "Clouds", "Farms", "TradeZones", "World", "WorldRoot", "ChunksContainer" };

            foreach (var name in objectNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null && obj != worldRoot && obj.transform.parent != worldRoot.transform)
                {
                    obj.transform.SetParent(worldRoot.transform);
                    Debug.Log($"[PrepareMainScene] Made '{name}' child of WorldRoot");
                }
            }

            // Mark scene dirty
            Scene scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
