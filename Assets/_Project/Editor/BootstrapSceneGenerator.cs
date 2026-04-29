#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectC.World.Scene;
using ProjectC.World.Streaming;
using ProjectC.World;
using ProjectC.Ship;
using ProjectC.Core;
using ProjectC.UI;
using ProjectC.Network;
using ProjectC.Player;

namespace ProjectC.Editor
{
    public class BootstrapSceneGenerator : EditorWindow
    {
        private const int ROWS = 4;
        private const int COLS = 6;
        private const float SCENE_SIZE = 79999f;
        private const string BOOTSTRAP_SCENE_PATH = "Assets/_Project/Scenes/BootstrapScene.unity";

        [MenuItem("ProjectC/World/Generate Bootstrap Scene")]
        public static void ShowWindow()
        {
            GetWindow<BootstrapSceneGenerator>("Bootstrap Scene Generator");
        }

        public void OnGUI()
        {
            GUILayout.Label("Bootstrap Scene Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label("Creates a persistent Bootstrap scene that loads world scenes additively.", EditorStyles.helpBox);

            EditorGUILayout.Space();
            GUILayout.Label("Contains:", EditorStyles.boldLabel);
            GUILayout.Label("• NetworkManager + ServerSceneManager");
            GUILayout.Label("• ClientSceneLoader (DontDestroyOnLoad)");
            GUILayout.Label("• WorldSceneManager + WorldStreamingManager");
            GUILayout.Label("• MainCamera + FloatingOriginMP");
            GUILayout.Label("• AltitudeCorridorSystem + CloudSystem");
            GUILayout.Label("• NetworkTestMenu (Host/Client/Server buttons)");

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Bootstrap Scene", GUILayout.Height(40)))
            {
                GenerateBootstrapScene();
            }

            EditorGUILayout.Space();
            GUILayout.Label("Workflow:", EditorStyles.helpBox);
            GUILayout.Label("1. Generate Bootstrap Scene (this)");
            GUILayout.Label("2. Generate 24 World Scenes (WorldSceneGenerator)");
            GUILayout.Label("3. Set BootstrapScene as first in Build Settings");
            GUILayout.Label("4. Play - use NetworkTestMenu to start Host/Client");
        }

        private void GenerateBootstrapScene()
        {
            if (!EditorUtility.DisplayDialog("Generate Bootstrap Scene",
                "This will create a Bootstrap scene with all runtime components.\nExisting scene will be overwritten!",
                "Generate", "Cancel"))
            {
                return;
            }

            string dir = System.IO.Path.GetDirectoryName(BOOTSTRAP_SCENE_PATH);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Bootstrap");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEventSystem();
            CreateNetworkManager();
            CreatePlayerSpawner();
            CreateSceneManagement();
            CreateCameraSystem();
            CreateWorldSystems();
            CreateNetworkTestMenu();

            EditorSceneManager.SaveScene(scene, BOOTSTRAP_SCENE_PATH);
            EditorSceneManager.CloseScene(scene, true);

            AddToBuildSettings();

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete",
                "Bootstrap scene created at:\n" + BOOTSTRAP_SCENE_PATH + "\n\n" +
                "Set this as the first scene in Build Settings.",
                "OK");
        }

        private void CreateEventSystem()
        {
            // FIX: Check if EventSystem already exists to avoid duplicates
            var existingEventSystem = UnityEngine.EventSystems.EventSystem.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (existingEventSystem != null)
            {
                Debug.LogWarning("[BootstrapSceneGenerator] EventSystem already exists, skipping creation");
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();

            // FIX: Use reflection to add InputSystemUIInputModule from Unity.InputSystem.UI assembly
            // This resolves "InvalidOperationException: You are trying to read Input using UnityEngine.Input class"
            var inputModuleType = GetTypeByName("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (inputModuleType != null)
            {
                eventSystem.AddComponent(inputModuleType);
            }
            else
            {
                Debug.LogWarning("[BootstrapSceneGenerator] InputSystemUIInputModule not found, using StandaloneInputModule as fallback");
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private static Type GetTypeByName(string typeName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName, false);
                if (type != null) return type;
            }
            return null;
        }

        private void CreateNetworkManager()
        {
            GameObject networkObj = new GameObject("NetworkManager");
            networkObj.transform.position = Vector3.zero;

            var networkManager = networkObj.AddComponent<Unity.Netcode.NetworkManager>();

            var transport = networkObj.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);

            var nmc = networkObj.AddComponent<NetworkManagerController>();
        }

private void CreatePlayerSpawner()
        {
            GameObject spawnerObj = new GameObject("PlayerSpawner");
            spawnerObj.transform.position = new Vector3(COLS * SCENE_SIZE / 2f, 3000f, ROWS * SCENE_SIZE / 2f);

            var networkObject = spawnerObj.AddComponent<NetworkObject>();
            var spawner = spawnerObj.AddComponent<NetworkPlayerSpawner>();

            var characterController = spawnerObj.AddComponent<CharacterController>();
            characterController.center = new Vector3(0, 1, 0);
            characterController.radius = 0.5f;
            characterController.height = 2f;

            spawnerObj.AddComponent<NetworkPlayer>();

            GameObject cameraObj = new GameObject("PlayerCamera");
            cameraObj.transform.SetParent(spawnerObj.transform);
            cameraObj.transform.localPosition = new Vector3(0, 1.5f, 0);
            cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();
        }

        private void CreateSceneManagement()
        {
            GameObject runtimeObj = new GameObject("Runtime");
            runtimeObj.transform.position = Vector3.zero;

            var clientLoader = runtimeObj.AddComponent<ClientSceneLoader>();
            var so = new SerializedObject(clientLoader);
            so.FindProperty("loadNeighbors").boolValue = true;
            so.FindProperty("unloadDistantScenes").boolValue = true;
            so.ApplyModifiedProperties();

            var serverSceneManager = runtimeObj.AddComponent<ServerSceneManager>();
            so = new SerializedObject(serverSceneManager);
            so.FindProperty("updateInterval").floatValue = 0.5f;
            so.ApplyModifiedProperties();

            // SceneTransitionCoordinator removed - ServerSceneManager sends RPCs directly to ClientSceneLoader
        }

        private void CreateCameraSystem()
        {
            GameObject cameraObj = new GameObject("MainCamera");
            cameraObj.transform.position = new Vector3(COLS * SCENE_SIZE / 2f, 3000f, ROWS * SCENE_SIZE / 2f);
            cameraObj.tag = "MainCamera";

            Camera cam = cameraObj.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.5f, 0.6f, 0.8f);
            cam.clearFlags = CameraClearFlags.Skybox;

            cameraObj.AddComponent<AudioListener>();

            // NOTE: FloatingOriginMP REMOVED - scene-based architecture doesn't need it
            // Per SCENE_ARCHITECTURE_DECISION.md: "Сцены 79,999 не требуют FloatingOriginMP внутри"
        }

        private void CreateWorldSystems()
        {
            // REMOVED: CreateWorldStreamingManager() - old chunk-based system
            // WorldChunkManager, ChunkLoader, ProceduralChunkGenerator, PlayerChunkTracker,
            // ChunkNetworkSpawner, WorldStreamingManager are DEPRECATED in scene-based architecture
            CreateAltitudeCorridorSystem();
            CreateCloudSystem();
            CreateWorldRoot();
        }

        // DEPRECATED: Scene-based architecture doesn't use chunk system
        // REMOVED: WorldChunkManager, ChunkLoader, ProceduralChunkGenerator,
        //          PlayerChunkTracker, ChunkNetworkSpawner, WorldStreamingManager
        // Kept for reference - delete after confirming scene-based system works
        private void CreateWorldStreamingManager_DELETED()
        {
            GameObject obj = new GameObject("WorldStreamingManager");
            obj.transform.position = Vector3.zero;

            obj.AddComponent<WorldChunkManager>();
            obj.AddComponent<ChunkLoader>();
            obj.AddComponent<ProceduralChunkGenerator>();
            obj.AddComponent<PlayerChunkTracker>();
            obj.AddComponent<ChunkNetworkSpawner>();
            obj.AddComponent<WorldStreamingManager>();

            var so = new SerializedObject(obj.GetComponent<WorldStreamingManager>());
            so.FindProperty("loadRadius").intValue = 2;
            so.FindProperty("unloadRadius").intValue = 3;
            so.FindProperty("updateInterval").floatValue = 0.5f;
            so.FindProperty("preloadLayers").intValue = 1;
            so.FindProperty("showDebugHUD").boolValue = true;
            so.ApplyModifiedProperties();

            var so2 = new SerializedObject(obj.GetComponent<PlayerChunkTracker>());
            so2.FindProperty("showDebugLogs").boolValue = true;
            so2.ApplyModifiedProperties();

            var so3 = new SerializedObject(obj.GetComponent<ChunkNetworkSpawner>());
            so3.FindProperty("showDebugLogs").boolValue = true;
            so3.ApplyModifiedProperties();
        }

        private void CreateAltitudeCorridorSystem()
        {
            GameObject obj = new GameObject("AltitudeCorridorSystem");
            obj.transform.position = Vector3.zero;

            var acs = obj.AddComponent<AltitudeCorridorSystem>();

            string[] corridorIds = { "veil_lower", "cloud_layer", "open_sky", "high_altitude", "global" };
            float[] minAlts = { 100f, 1000f, 3500f, 5000f, 0f };
            float[] maxAlts = { 1500f, 3500f, 5000f, 6500f, 99999f };
            bool[] isGlobal = { false, false, false, false, true };

            var corridorAssets = new List<AltitudeCorridorData>();

            for (int i = 0; i < corridorIds.Length; i++)
            {
                string path = $"Assets/_Project/Data/Ship/AltitudeCorridor_{corridorIds[i]}.asset";
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    AssetDatabase.CreateFolder("Assets/_Project/Data", "Ship");
                }

                AltitudeCorridorData existing = AssetDatabase.LoadAssetAtPath<AltitudeCorridorData>(path);
                if (existing != null)
                {
                    corridorAssets.Add(existing);
                    continue;
                }

                AltitudeCorridorData corridor = ScriptableObject.CreateInstance<AltitudeCorridorData>();
                corridor.corridorId = corridorIds[i];
                corridor.displayName = corridorIds[i].Replace("_", " ").ToUpper();
                corridor.minAltitude = minAlts[i];
                corridor.maxAltitude = maxAlts[i];
                corridor.warningMargin = (maxAlts[i] - minAlts[i]) * 0.1f;
                corridor.criticalUpperMargin = (maxAlts[i] - minAlts[i]) * 0.15f;
                corridor.isGlobal = isGlobal[i];

                AssetDatabase.CreateAsset(corridor, path);
                corridorAssets.Add(corridor);
            }

            AssetDatabase.SaveAssets();

            var so = new SerializedObject(acs);
            var corridorsProp = so.FindProperty("corridors");
            corridorsProp.ClearArray();
            for (int i = 0; i < corridorAssets.Count; i++)
            {
                corridorsProp.InsertArrayElementAtIndex(i);
                corridorsProp.GetArrayElementAtIndex(i).objectReferenceValue = corridorAssets[i];
            }
            so.ApplyModifiedProperties();
        }

        private void CreateCloudSystem()
        {
            GameObject obj = new GameObject("CloudSystem");
            obj.transform.position = Vector3.zero;

            var cloudSystem = obj.AddComponent<CloudSystem>();

            GameObject layer1 = new GameObject("LowerCloudLayer");
            layer1.transform.SetParent(obj.transform);
            layer1.transform.localPosition = new Vector3(COLS * SCENE_SIZE / 2f, 1500f, ROWS * SCENE_SIZE / 2f);

            var layer1Comp = layer1.AddComponent<CloudLayer>();

            GameObject layer2 = new GameObject("UpperCloudLayer");
            layer2.transform.SetParent(obj.transform);
            layer2.transform.localPosition = new Vector3(COLS * SCENE_SIZE / 2f, 3000f, ROWS * SCENE_SIZE / 2f);

            var layer2Comp = layer2.AddComponent<CloudLayer>();
        }

        private void CreateWorldRoot()
        {
            GameObject worldRoot = new GameObject("WorldRoot");
            worldRoot.transform.position = Vector3.zero;

            string[] subRoots = { "Mountains", "Clouds", "Farms", "TradeZones" }; // ChunksContainer REMOVED - chunk system deprecated
            foreach (var name in subRoots)
            {
                GameObject subRoot = new GameObject(name);
                subRoot.transform.SetParent(worldRoot.transform);
                subRoot.transform.position = Vector3.zero;
            }
        }

        private void CreateNetworkTestMenu()
        {
            Canvas canvasObj = CreateNetworkTestCanvas();

            // FIX: Create NetworkTestMenu component on canvas instead of using lambdas
            // Lambda listeners don't persist to scene files
            var menuHandler = canvasObj.gameObject.AddComponent<NetworkTestMenu>();

            CreateNetworkTestMenuContent(canvasObj.transform, menuHandler);
        }

        private Canvas CreateNetworkTestCanvas()
        {
            GameObject canvasObj = new GameObject("NetworkTestCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // FIX: EventSystem already created by CreateEventSystem() - do NOT create duplicate
            // Previously created duplicate EventSystem here causing "2 event systems" error

            return canvas;
        }

        private void CreateNetworkTestMenuContent(Transform canvasTransform, NetworkTestMenu menuHandler)
        {
            GameObject menuObj = new GameObject("NetworkTestMenu");
            menuObj.transform.SetParent(canvasTransform);
            menuObj.SetActive(true);

            var menuPanel = CreateMenuPanel(menuObj.transform);

            // FIX: NetworkTestMenu component handles button binding via Inspector references
            // Assign button references for serialization
            var nmc = UnityEngine.Object.FindAnyObjectByType<NetworkManagerController>();
            if (nmc == null)
            {
                Debug.LogWarning("[BootstrapSceneGenerator] NetworkManagerController not found in scene");
            }

            // Create buttons with proper persistent OnClick handlers via NetworkTestMenu
            var hostBtnObj = CreateButtonObject(menuPanel.transform, "Host", new Vector2(0, -20));
            var clientBtnObj = CreateButtonObject(menuPanel.transform, "Client", new Vector2(0, -70));
            var serverBtnObj = CreateButtonObject(menuPanel.transform, "Server", new Vector2(0, -120));
            var loadWorldBtnObj = CreateButtonObject(menuPanel.transform, "Load World [0,0]", new Vector2(0, -180));

            // FIX: Assign button references to NetworkTestMenu so Start() can wire up listeners
            menuHandler.hostButton = hostBtnObj.GetComponent<Button>();
            menuHandler.clientButton = clientBtnObj.GetComponent<Button>();
            menuHandler.serverButton = serverBtnObj.GetComponent<Button>();
        }

        private GameObject CreateMenuPanel(Transform parent)
        {
            GameObject panel = new GameObject("MenuPanel");
            panel.transform.SetParent(parent);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(300, 250);
            panel.AddComponent<CanvasRenderer>();
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 100);
            titleRect.sizeDelta = new Vector2(250, 30);
            var titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "ProjectC - Network Test";
            titleText.fontSize = 18;
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            titleText.color = Color.white;

            return panel;
        }

        private void CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(text);
            btnObj.transform.SetParent(parent.transform);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180, 40);

            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.4f, 0.8f, 1f);

            var button = btnObj.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            var textComp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 14;
            textComp.alignment = TMPro.TextAlignmentOptions.Center;
            textComp.color = Color.white;
        }

        private GameObject CreateButtonObject(Transform parent, string text, Vector2 position)
        {
            GameObject btnObj = new GameObject(text);
            btnObj.transform.SetParent(parent);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180, 40);

            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.4f, 0.8f, 1f);

            var button = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            var textComp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 14;
            textComp.alignment = TMPro.TextAlignmentOptions.Center;
            textComp.color = Color.white;

            return btnObj;
        }

        private void AddToBuildSettings()
        {
            EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;
            List<EditorBuildSettingsScene> allScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BOOTSTRAP_SCENE_PATH, true) };

            HashSet<string> existingPaths = new HashSet<string>();
            foreach (var s in existingScenes)
            {
                existingPaths.Add(s.path);
            }

            foreach (var s in existingScenes)
            {
                if (!existingPaths.Contains(s.path))
                {
                    allScenes.Add(s);
                }
            }

            EditorBuildSettings.scenes = allScenes.ToArray();
        }

        [ContextMenu("Regenerate Bootstrap Scene")]
        public void Regenerate()
        {
            GenerateBootstrapScene();
        }
    }
}
#endif