#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor script to prepare a test scene for Phase 2 World Streaming.
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
        private bool _includeNetworkTestMenu = true;

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
            _includeNetworkTestMenu = EditorGUILayout.Toggle("Network Test Menu (Host/Client)", _includeNetworkTestMenu);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("This will create all necessary objects for testing Phase 2 World Streaming.", MessageType.Info);
            EditorGUILayout.Space();

            if (GUILayout.Button("Create Test Scene", GUILayout.Height(40)))
                CreateTestScene();
        }

        private void CreateTestScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                int choice = EditorUtility.DisplayDialogComplex("Unsaved Scene", "Save current scene?", "Save", "Don't Save", "Cancel");
                if (choice == 2) return;
                if (choice == 0) EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (_includeWorldStreaming) CreateWorldStreamingSystem();
            if (_includeNetworkManager) CreateNetworkManager();
            if (_includeFloatingOrigin) CreateFloatingOrigin();
            if (_includePlayer) CreateTestPlayer();

            CreateWorldRoot();
            CreateChunksContainer();
            CreateLighting();

            if (_includeNetworkTestMenu) CreateNetworkTestMenu();

            EditorSceneManager.MarkSceneDirty(newScene);

            string scenePath = $"Assets/_Project/Scenes/Test/{_sceneName}.unity";
            string directory = System.IO.Path.GetDirectoryName(scenePath);
            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);

            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Test Scene Created", $"Scene: {_sceneName}\nPath: {scenePath}", "OK");
            Debug.Log($"[PrepareTestScene] Test scene created: {scenePath}");
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

        private void CreateWorldStreamingSystem()
        {
            GameObject streamingObj = new GameObject("WorldStreamingManager");
            streamingObj.transform.position = Vector3.zero;

            AddComponentByName(streamingObj, "ProjectC.World.Streaming.WorldChunkManager");
            AddComponentByName(streamingObj, "ProjectC.World.Streaming.ChunkLoader");
            AddComponentByName(streamingObj, "ProjectC.World.Streaming.ProceduralChunkGenerator");
            AddComponentByName(streamingObj, "ProjectC.World.Streaming.PlayerChunkTracker");
            AddComponentByName(streamingObj, "ProjectC.World.Streaming.ChunkNetworkSpawner");
            AddComponentByName(streamingObj, "ProjectC.World.WorldStreamingManager");

            ConfigureComponent(streamingObj, "ProjectC.World.Streaming.PlayerChunkTracker", "showDebugLogs", true);
            ConfigureComponent(streamingObj, "ProjectC.World.Streaming.ChunkNetworkSpawner", "showDebugLogs", true);
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
                    Debug.Log($"[PrepareTestScene] Added {typeName}");
                else
                    Debug.LogWarning($"[PrepareTestScene] Failed to add {typeName}");
            }
            else
                Debug.LogError($"[PrepareTestScene] Type not found: {typeName}");
        }

        private void ConfigureComponent(GameObject obj, string typeName, string propertyName, object value)
        {
            Type componentType = GetTypeByName(typeName);
            if (componentType == null) return;

            var component = obj.GetComponent(componentType);
            if (component == null) return;

            SerializedObject so = new SerializedObject(component);
            var property = so.FindProperty(propertyName);
            if (property != null)
            {
                if (value is int) property.intValue = (int)value;
                else if (value is float) property.floatValue = (float)value;
                else if (value is bool) property.boolValue = (bool)value;
                so.ApplyModifiedProperties();
            }
        }

        private void CreateChunksContainer()
        {
            if (GameObject.Find("ChunksContainer") != null) return;
            GameObject container = new GameObject("ChunksContainer");
            container.transform.position = Vector3.zero;
        }

        private void CreateNetworkManager()
        {
            GameObject networkObj = new GameObject("NetworkManagerController");
            var nmc = networkObj.AddComponent<ProjectC.Core.NetworkManagerController>();
            
            Debug.Log("[PrepareTestScene] NetworkManagerController created");
        }

        private void CreateFloatingOrigin()
        {
            Camera mainCamera = FindAnyObjectByType<Camera>();
            if (mainCamera == null)
            {
                GameObject camObj = new GameObject("MainCamera");
                mainCamera = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            AddComponentByName(mainCamera.gameObject, "ProjectC.Core.ThirdPersonCamera");

            Type floatingOriginType = GetTypeByName("ProjectC.World.Streaming.FloatingOriginMP");
            if (floatingOriginType == null) return;

            if (mainCamera.GetComponent(floatingOriginType) == null)
                mainCamera.gameObject.AddComponent(floatingOriginType);

            var component = mainCamera.GetComponent(floatingOriginType);
            if (component != null)
            {
                SerializedObject floatingOriginSO = new SerializedObject(component);
                var prop = floatingOriginSO.FindProperty("showDebugLogs");
                if (prop != null) prop.boolValue = true;
                prop = floatingOriginSO.FindProperty("showDebugHUD");
                if (prop != null) prop.boolValue = true;
                floatingOriginSO.ApplyModifiedProperties();
            }
        }

        private void CreateTestPlayer()
        {
            GameObject playerObj = new GameObject("TestPlayer");
            playerObj.transform.position = new Vector3(0, 2, 0);

            var controller = playerObj.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0, 1, 0);

            var networkObject = playerObj.AddComponent<Unity.Netcode.NetworkObject>();
            networkObject.AutoObjectParentSync = false; // Scene objects don't auto-spawn
            
            // Add player spawner
            AddComponentByName(playerObj, "ProjectC.Network.NetworkPlayerSpawner");
            
            AddComponentByName(playerObj, "ProjectC.Player.PlayerController");
            CreateSimplePlayerPlaceholder(playerObj);
            ConfigureThirdPersonCamera(playerObj.transform);
        }

        private void ConfigureThirdPersonCamera(Transform playerTransform)
        {
            var tpc = FindAnyObjectByType<ProjectC.Core.ThirdPersonCamera>();
            if (tpc != null)
            {
                SerializedObject tpcSO = new SerializedObject(tpc);
                var targetProp = tpcSO.FindProperty("target");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = playerTransform;
                    tpcSO.ApplyModifiedProperties();
                }
            }
        }

        private void CreateSimplePlayerPlaceholder(GameObject parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(parent.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one * 0.5f;
            var col = body.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
        }

        private void CreateWorldRoot()
        {
            if (GameObject.Find("WorldRoot") != null) return;

            GameObject worldRoot = new GameObject("WorldRoot");
            worldRoot.transform.position = Vector3.zero;

            CreateWorldSubRoot(worldRoot, "Mountains");
            CreateWorldSubRoot(worldRoot, "Clouds");
            CreateWorldSubRoot(worldRoot, "Farms");
            CreateWorldSubRoot(worldRoot, "TradeZones");
        }

        private void CreateWorldSubRoot(GameObject parent, string name)
        {
            GameObject subRoot = new GameObject(name);
            subRoot.transform.SetParent(parent.transform);
            subRoot.transform.position = Vector3.zero;
        }

        private void CreateLighting()
        {
            if (FindAnyObjectByType<Light>() != null) return;

            GameObject lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        private void CreateNetworkTestMenu()
        {
            // Canvas
            GameObject canvasObj = new GameObject("NetworkTestCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // EventSystem
            GameObject eventSystemObj = new GameObject("EventSystem");
            var eventSystem = eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            
            // Use InputSystemUIInputModule if available (new Input System)
            var inputModuleType = GetTypeByName("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (inputModuleType != null)
            {
                eventSystemObj.AddComponent(inputModuleType);
            }
            else
            {
                // Fallback to old StandaloneInputModule
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // NetworkTestMenu
            GameObject menuObj = new GameObject("NetworkTestMenu");
            menuObj.transform.SetParent(canvasObj.transform);
            var networkMenu = menuObj.AddComponent<ProjectC.UI.NetworkTestMenu>();
            var menuType = typeof(ProjectC.UI.NetworkTestMenu);

            // Panel
            GameObject panel = new GameObject("MenuPanel");
            panel.transform.SetParent(menuObj.transform);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(400, 300);
            panel.AddComponent<CanvasRenderer>();
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 80);
            titleRect.sizeDelta = new Vector2(300, 30);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Network Test Menu";
            titleText.fontSize = 18;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;

            // Status
            GameObject statusObj = new GameObject("Status");
            statusObj.transform.SetParent(panel.transform);
            var statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.5f);
            statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0, 40);
            statusRect.sizeDelta = new Vector2(300, 25);
            var statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Select connection mode";
            statusText.fontSize = 14;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.gray;

            // Bind fields via reflection
            var statusField = menuType.GetField("statusText", BindingFlags.NonPublic | BindingFlags.Instance);
            if (statusField != null) statusField.SetValue(networkMenu, statusText);

            // Buttons - using NMC
            var nmc = FindAnyObjectByType<ProjectC.Core.NetworkManagerController>();
            
            CreateButton(panel, "Host", new Vector2(0, -20), () => {
                nmc?.StartHost();
                menuObj.SetActive(false);
            });

            CreateButton(panel, "Client", new Vector2(0, -70), () => {
                nmc?.ConnectToServer("127.0.0.1", 7777);
                menuObj.SetActive(false);
            });

            CreateButton(panel, "Server", new Vector2(0, -120), () => {
                nmc?.StartServer();
                menuObj.SetActive(false);
            });

            // Bind panel and buttons
            var panelField = menuType.GetField("menuPanel", BindingFlags.NonPublic | BindingFlags.Instance);
            if (panelField != null) panelField.SetValue(networkMenu, panel);

            var hostBtnField = menuType.GetField("hostButton", BindingFlags.NonPublic | BindingFlags.Instance);
            var clientBtnField = menuType.GetField("clientButton", BindingFlags.NonPublic | BindingFlags.Instance);
            var serverBtnField = menuType.GetField("serverButton", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (hostBtnField != null) hostBtnField.SetValue(networkMenu, panel.transform.Find("HostButton")?.GetComponent<Button>());
            if (clientBtnField != null) clientBtnField.SetValue(networkMenu, panel.transform.Find("ClientButton")?.GetComponent<Button>());
            if (serverBtnField != null) serverBtnField.SetValue(networkMenu, panel.transform.Find("ServerButton")?.GetComponent<Button>());

            // Smaller panel for 3 buttons
            panelRect.sizeDelta = new Vector2(250, 220);

            menuObj.SetActive(true);
            panel.SetActive(true);
        }

        private void CreateButton(GameObject parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(text + "Button");
            btnObj.transform.SetParent(parent.transform);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(150, 40);

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
            
            var textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 16;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = Color.white;
        }
    }
}
#endif