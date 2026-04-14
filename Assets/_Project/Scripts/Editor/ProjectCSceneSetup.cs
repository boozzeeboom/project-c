using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ProjectC.World;
using ProjectC.World.Streaming;
using ProjectC.World.Core;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor utility to setup ProjectC scene with required systems.
    /// Run this once to setup WorldStreamingManager, lighting, etc.
    /// </summary>
    public class ProjectCSceneSetup : EditorWindow
    {
        [MenuItem("Tools/ProjectC/Setup Scene")]
        public static void ShowWindow()
        {
            GetWindow<ProjectCSceneSetup>("ProjectC Scene Setup");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("ProjectC Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "This utility will setup the scene with:\n" +
                "- World Streaming Manager\n" +
                "- Directional Light\n" +
                "- FloatingOriginMP on main camera\n" +
                "Run this after opening ProjectC_1.unity scene.",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Setup Scene", GUILayout.Height(40)))
            {
                SetupScene();
            }
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Add World Streaming Manager Only", GUILayout.Height(30)))
            {
                AddWorldStreamingManager();
            }
            
            if (GUILayout.Button("Add Directional Light Only", GUILayout.Height(30)))
            {
                AddDirectionalLight();
            }
        }

        [MenuItem("Tools/ProjectC/Auto-Setup Scene")]
        public static void AutoSetup()
        {
            SetupScene();
        }

        private static void SetupScene()
        {
            Debug.Log("[ProjectC Scene Setup] Starting scene setup...");
            
            AddWorldStreamingManager();
            AddDirectionalLight();
            SetupMainCamera();
            
            Debug.Log("[ProjectC Scene Setup] Scene setup complete!");
            EditorUtility.DisplayDialog("ProjectC Scene Setup", "Scene setup complete! Check Console for details.", "OK");
        }

        private static void AddWorldStreamingManager()
        {
            // Check if already exists
            var existingManager = Object.FindAnyObjectByType<WorldStreamingManager>();
            if (existingManager != null)
            {
                Debug.Log("[ProjectC Scene Setup] WorldStreamingManager already exists, skipping.");
                return;
            }

            // Create WorldStreamingManager GameObject
            GameObject managerObj = new GameObject("WorldStreamingManager");
            var manager = managerObj.AddComponent<WorldStreamingManager>();
            
            // Add required streaming components (AutoFindComponents will wire them up at runtime)
            managerObj.AddComponent<WorldChunkManager>();
            managerObj.AddComponent<ChunkLoader>();
            managerObj.AddComponent<ProceduralChunkGenerator>();
            
            // Load WorldData and assign via SerializedObject (private field workaround)
            var worldData = LoadWorldData();
            if (worldData == null)
            {
                Debug.LogWarning("[ProjectC Scene Setup] WorldData not found! WorldStreamingManager will have null reference. " +
                    "Create a WorldData asset via Create → Project C → World Data.");
            }
            else
            {
                // Assign through SerializedObject since field is private [SerializeField]
                var so = new SerializedObject(manager);
                var worldDataProp = so.FindProperty("worldData");
                if (worldDataProp != null)
                {
                    worldDataProp.objectReferenceValue = worldData;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[ProjectC Scene Setup] WorldData loaded: {worldData.massifs.Count} massifs");
                }
            }
            
            Debug.Log("[ProjectC Scene Setup] WorldStreamingManager created. Components will auto-wire on Play.");
        }

        private static void AddDirectionalLight()
        {
            // Check if directional light named "Sun" already exists
            var allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (var existingLight in allLights)
            {
                if (existingLight.type == LightType.Directional && existingLight.name.Contains("Sun"))
                {
                    Debug.Log("[ProjectC Scene Setup] Directional light already exists, skipping.");
                    return;
                }
            }

            // Create Directional Light
            GameObject lightObj = new GameObject("Sun");
            lightObj.transform.position = new Vector3(0, 3000, 0);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            
            Light sunLight = lightObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.95f, 0.8f); // Warm sunlight
            sunLight.intensity = 1.2f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowResolution = (UnityEngine.Rendering.LightShadowResolution)UnityEngine.ShadowResolution.High;
            sunLight.shadowBias = 0.05f;
            sunLight.shadowNearPlane = 0.5f;
            
            // URP Additional Light Data
            var urpLightData = lightObj.AddComponent<UniversalAdditionalLightData>();
            
            Debug.Log("[ProjectC Scene Setup] Directional light 'Sun' created.");
        }

        private static void SetupMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[ProjectC Scene Setup] No main camera found! Make sure there's a camera tagged as MainCamera.");
                return;
            }

            // Add FloatingOriginMP if not exists
            var floatingOrigin = mainCamera.GetComponent<FloatingOriginMP>();
            if (floatingOrigin == null)
            {
                floatingOrigin = mainCamera.gameObject.AddComponent<FloatingOriginMP>();
                floatingOrigin.threshold = 100000f;
                floatingOrigin.shiftRounding = 10000f;
                floatingOrigin.showDebugLogs = false;
                floatingOrigin.showDebugHUD = false;
                
                Debug.Log("[ProjectC Scene Setup] FloatingOriginMP added to main camera.");
            }
            else
            {
                Debug.Log("[ProjectC Scene Setup] FloatingOriginMP already exists on main camera.");
            }

            // Configure camera for large world
            mainCamera.farClipPlane = 1000000f;
            mainCamera.nearClipPlane = 0.5f;
            
            Debug.Log("[ProjectC Scene Setup] Main camera configured for large world.");
        }

        private static WorldData LoadWorldData()
        {
            // Try to load from Resources
            WorldData data = Resources.Load<WorldData>("World/WorldData");
            
            // Try to find in Assets via AssetDatabase
            if (data == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:WorldData");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    data = AssetDatabase.LoadAssetAtPath<WorldData>(path);
                }
            }
            
            return data;
        }
    }
}
