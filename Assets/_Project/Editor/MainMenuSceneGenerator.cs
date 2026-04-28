#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.Core;
using ProjectC.UI;
using ProjectC.Player;
using ProjectC.Network;

namespace ProjectC.Editor
{
    public class MainMenuSceneGenerator : EditorWindow
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/MainMenu.unity";

        [MenuItem("ProjectC/World/Generate Main Menu Scene")]
        public static void ShowWindow()
        {
            GetWindow<MainMenuSceneGenerator>("Main Menu Scene");
        }

        public void OnGUI()
        {
            GUILayout.Label("Main Menu Scene Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label("Creates bootstrap scene with NetworkManager and player spawner:", EditorStyles.helpBox);

            if (GUILayout.Button("Generate Main Menu Scene", GUILayout.Height(40)))
            {
                GenerateMainMenuScene();
            }
        }

        private void GenerateMainMenuScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "MainMenu");
            }

            Scene existingScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);

            CreateBootstrapObjects();

            EditorSceneManager.SaveScene(existingScene, MAIN_SCENE_PATH);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete", "Main Menu scene created at:\n" + MAIN_SCENE_PATH, "OK");
        }

        private void CreateBootstrapObjects()
        {
            CreateEventSystem();
            CreateCanvas();
            CreateNetworkManager();
            CreatePlayerSpawner();
            CreateUIManager();
            CreateMainMenuUI();
        }

        private void CreateEventSystem()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private void CreateCanvas()
        {
            GameObject canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        private void CreateNetworkManager()
        {
            GameObject nmObj = new GameObject("NetworkManager");
            var networkManager = nmObj.AddComponent<Unity.Netcode.NetworkManager>();

            GameObject nmcObj = new GameObject("NetworkManagerController");
            nmcObj.transform.SetParent(nmObj.transform);
            var nmc = nmcObj.AddComponent<NetworkManagerController>();
        }

        private void CreatePlayerSpawner()
        {
            GameObject spawnerObj = new GameObject("PlayerSpawner");
            spawnerObj.transform.position = Vector3.zero;
            var spawner = spawnerObj.AddComponent<NetworkPlayerSpawner>();
            var networkObject = spawnerObj.AddComponent<NetworkObject>();

            CreatePlayerObject(spawnerObj.transform);
        }

        private void CreatePlayerObject(Transform parent)
        {
            GameObject playerObj = new GameObject("Player");
            playerObj.transform.SetParent(parent);
            playerObj.transform.localPosition = Vector3.zero;

            var networkObject = playerObj.AddComponent<NetworkObject>();

            var characterController = playerObj.AddComponent<CharacterController>();
            characterController.center = new Vector3(0, 1, 0);
            characterController.radius = 0.5f;
            characterController.height = 2f;

            playerObj.AddComponent<NetworkPlayer>();

            GameObject cameraObj = new GameObject("PlayerCamera");
            cameraObj.transform.SetParent(playerObj.transform);
            cameraObj.transform.localPosition = new Vector3(0, 1.5f, 0);
            var camera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();

            playerObj.SetActive(false);
        }

        private void CreateUIManager()
        {
            GameObject uiObj = new GameObject("UIManager");
            uiObj.transform.position = Vector3.zero;
            var uiManager = uiObj.AddComponent<UIManager>();
        }

        private void CreateMainMenuUI()
        {
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj == null) return;

            GameObject menuPanel = new GameObject("MainMenuPanel");
            menuPanel.transform.SetParent(canvasObj.transform);
            menuPanel.transform.localPosition = Vector3.zero;

            var rectTransform = menuPanel.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            var image = menuPanel.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0, 0, 0, 0.8f);

            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(menuPanel.transform);

            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.7f);
            titleRect.sizeDelta = new Vector2(400, 100);
            titleRect.anchoredPosition = Vector2.zero;

            var titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "ProjectC";
            titleText.fontSize = 72;
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            titleText.color = Color.white;

            GameObject startButton = CreateButton("StartButton", "Start Game", new Vector2(0.5f, 0.4f));
            startButton.transform.SetParent(menuPanel.transform);

            GameObject hostButton = CreateButton("HostButton", "Host Game", new Vector2(0.5f, 0.3f));
            hostButton.transform.SetParent(menuPanel.transform);

            GameObject optionsButton = CreateButton("OptionsButton", "Options", new Vector2(0.5f, 0.2f));
            optionsButton.transform.SetParent(menuPanel.transform);
        }

        private GameObject CreateButton(string name, string text, Vector2 anchorPos)
        {
            GameObject buttonObj = new GameObject(name);
            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorPos;
            rect.anchorMax = anchorPos;
            rect.sizeDelta = new Vector2(200, 50);
            rect.anchoredPosition = Vector2.zero;

            var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.2f, 0.4f, 1f);

            var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.5f, 1f);
            colors.pressedColor = new Color(0.1f, 0.1f, 0.3f, 1f);
            button.colors = colors;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            var tmpText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.fontSize = 24;
            tmpText.alignment = TMPro.TextAlignmentOptions.Center;
            tmpText.color = Color.white;

            return buttonObj;
        }

        [ContextMenu("Regenerate Main Menu")]
        public void Regenerate()
        {
            GenerateMainMenuScene();
        }
    }
}
#endif