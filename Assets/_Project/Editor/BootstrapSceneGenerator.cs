#if UNITY_EDITOR
using System.Collections.Generic;
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

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            CreateEventSystem();
            CreateNetworkManager();
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
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
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

            var coordinator = runtimeObj.AddComponent<SceneTransitionCoordinator>();
            var networkObject = runtimeObj.AddComponent<NetworkObject>();
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

            var fo = cameraObj.AddComponent<FloatingOriginMP>();
            var so = new SerializedObject(fo);
            so.FindProperty("threshold").floatValue = 150000f;
            so.FindProperty("shiftRounding").floatValue = 10000f;
            so.FindProperty("showDebugLogs").boolValue = true;
            so.FindProperty("showDebugHUD").boolValue = true;

            var worldRootNamesProp = so.FindProperty("worldRootNames");
            if (worldRootNamesProp != null)
            {
                worldRootNamesProp.ClearArray();
                string[] names = { "WorldRoot", "Mountains", "Clouds", "Ground", "Boundaries", "Runtime" };
                for (int i = 0; i < names.Length; i++)
                {
                    worldRootNamesProp.InsertArrayElementAtIndex(i);
                    worldRootNamesProp.GetArrayElementAtIndex(i).stringValue = names[i];
                }
            }
            so.ApplyModifiedProperties();
        }

        private void CreateWorldSystems()
        {
            CreateWorldStreamingManager();
            CreateAltitudeCorridorSystem();
            CreateCloudSystem();
            CreateWorldRoot();
        }

        private void CreateWorldStreamingManager()
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

            string[] subRoots = { "Mountains", "Clouds", "Farms", "TradeZones", "ChunksContainer" };
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
            CreateNetworkTestMenuContent(canvasObj.transform);
        }

        private Canvas CreateNetworkTestCanvas()
        {
            GameObject canvasObj = new GameObject("NetworkTestCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            return canvas;
        }

        private void CreateNetworkTestMenuContent(Transform canvasTransform)
        {
            GameObject menuObj = new GameObject("NetworkTestMenu");
            menuObj.transform.SetParent(canvasTransform);
            menuObj.SetActive(true);

            var menuPanel = CreateMenuPanel(menuObj.transform);

            var nmc = Object.FindFirstObjectByType<NetworkManagerController>();
            if (nmc == null)
            {
                Debug.LogWarning("[BootstrapSceneGenerator] NetworkManagerController not found in scene");
            }

            CreateButton(menuPanel.transform, "Host", new Vector2(0, -20), () => {
                if (nmc != null) nmc.StartHost();
                menuObj.SetActive(false);
            });

            CreateButton(menuPanel.transform, "Client", new Vector2(0, -70), () => {
                if (nmc != null) nmc.ConnectToServer("127.0.0.1", 7777);
                menuObj.SetActive(false);
            });

            CreateButton(menuPanel.transform, "Server", new Vector2(0, -120), () => {
                if (nmc != null) nmc.StartServer();
                menuObj.SetActive(false);
            });

            CreateButton(menuPanel.transform, "Load World [0,0]", new Vector2(0, -180), () => {
                var loader = Object.FindFirstObjectByType<ClientSceneLoader>();
                if (loader != null)
                {
                    loader.LoadInitialScene(new SceneID(0, 0));
                }
                menuObj.SetActive(false);
            });
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