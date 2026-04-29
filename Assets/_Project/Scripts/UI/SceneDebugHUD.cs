using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectC.World.Scene;

namespace ProjectC.UI
{
    public class SceneDebugHUD : MonoBehaviour
    {
        [Header("UI References (auto-create if not assigned)")]
        [SerializeField] private TextMeshProUGUI gridText;
        [SerializeField] private TextMeshProUGUI playerInfoText;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("Settings")]
        [SerializeField] private bool showGridOnUpdate = true;
        [SerializeField] private int updateIntervalMs = 200;

        private float _lastUpdateTime;
        private Canvas _canvas;
        private RectTransform _panel;
        private ClientSceneLoader _clientLoader;
        private SceneRegistry _sceneRegistry;

        private void Start()
        {
            if (gridText == null)
            {
                CreateUI();
            }

            _clientLoader = FindAnyObjectByType<ClientSceneLoader>();

            _sceneRegistry = Resources.Load<SceneRegistry>("Scene/SceneRegistry");
            if (_sceneRegistry == null)
                _sceneRegistry = Resources.Load<SceneRegistry>("SceneRegistry");

            DontDestroyOnLoad(gameObject);
        }

        private void CreateUI()
        {
            var canvasObj = new GameObject("SceneDebugCanvas");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            var panelObj = new GameObject("DebugPanel");
            panelObj.transform.SetParent(canvasObj.transform);
            _panel = panelObj.AddComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0, 1);
            _panel.anchorMax = new Vector2(0, 1);
            _panel.pivot = new Vector2(0, 1);
            _panel.anchoredPosition = new Vector2(10, -10);
            _panel.sizeDelta = new Vector2(350, 400);

            var panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);

            var gridObj = new GameObject("GridText");
            gridObj.transform.SetParent(_panel);
            var gridRt = gridObj.AddComponent<RectTransform>();
            gridRt.anchorMin = Vector2.zero;
            gridRt.anchorMax = Vector2.one;
            gridRt.sizeDelta = Vector2.zero;
            gridRt.offsetMin = new Vector2(10, -10);
            gridRt.offsetMax = new Vector2(-10, -150);

            gridText = gridObj.AddComponent<TextMeshProUGUI>();
            gridText.fontSize = 14;
            gridText.color = Color.white;
            gridText.text = "Scene Grid:\nInitializing...";

            var playerObj = new GameObject("PlayerInfoText");
            playerObj.transform.SetParent(_panel);
            var playerRt = playerObj.AddComponent<RectTransform>();
            playerRt.anchorMin = new Vector2(0, 0);
            playerRt.anchorMax = new Vector2(1, 0);
            playerRt.sizeDelta = new Vector2(0, 60);
            playerRt.anchoredPosition = new Vector2(0, 30);

            playerInfoText = playerObj.AddComponent<TextMeshProUGUI>();
            playerInfoText.fontSize = 14;
            playerInfoText.color = Color.cyan;
            playerInfoText.text = "Player: --";

            var statsObj = new GameObject("StatsText");
            statsObj.transform.SetParent(_panel);
            var statsRt = statsObj.AddComponent<RectTransform>();
            statsRt.anchorMin = new Vector2(0, 0);
            statsRt.anchorMax = new Vector2(1, 0);
            statsRt.sizeDelta = new Vector2(0, 60);
            statsRt.anchoredPosition = new Vector2(0, 0);

            statsText = statsObj.AddComponent<TextMeshProUGUI>();
            statsText.fontSize = 12;
            statsText.color = Color.yellow;
            statsText.text = "Loaded: 0\nLoading: 0";

            _panel.SetParent(_canvas.transform, false);
            canvasObj.transform.SetParent(transform);
        }

        private void Update()
        {
            if (!showGridOnUpdate) return;
            if (Time.time * 1000 - _lastUpdateTime < updateIntervalMs) return;
            _lastUpdateTime = Time.time * 1000;

            UpdateGridDisplay();
            UpdatePlayerInfo();
            UpdateStats();
        }

        private void UpdatePlayerInfo()
        {
            if (playerInfoText == null) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                playerInfoText.text = "Player: NOT FOUND";
                return;
            }

            Vector3 pos = player.transform.position;
            var sceneId = SceneID.FromWorldPosition(pos);
            playerInfoText.text = $"Player: {pos.x:F0}, {pos.z:F0}\nScene: {sceneId.GridX},{sceneId.GridZ}";
        }

        private void UpdateGridDisplay()
        {
            if (gridText == null || _sceneRegistry == null) return;

            int cols = _sceneRegistry.GridColumns;
            int rows = _sceneRegistry.GridRows;
            var loadedSet = GetLoadedScenes();

            string grid = "=== SCENE GRID ===\n";
            grid += $"+{new string('-', cols * 4)}+\n";

            for (int z = rows - 1; z >= 0; z--)
            {
                grid += "|";
                for (int x = 0; x < cols; x++)
                {
                    var id = new SceneID(x, z);
                    bool isLoaded = loadedSet != null && loadedSet.Contains(id);
                    string cell = isLoaded ? "[X]" : "[ ]";
                    grid += cell;
                }
                grid += "|\n";
            }

            grid += $"+{new string('-', cols * 4)}+\n";
            gridText.text = grid;
        }

        private void UpdateStats()
        {
            if (statsText == null) return;

            var loadedSet = GetLoadedScenes();
            var loadingSet = GetLoadingScenes();

            int loaded = loadedSet?.Count ?? 0;
            int loading = loadingSet?.Count ?? 0;

            string playerScene = "unknown";
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var sid = SceneID.FromWorldPosition(player.transform.position);
                playerScene = $"{sid.GridX},{sid.GridZ}";
            }

            statsText.text = $"Player Scene: {playerScene}\nLoaded: {loaded}  Loading: {loading}";
        }

        private HashSet<SceneID> GetLoadedScenes()
        {
            if (_clientLoader == null) return null;

            var field = typeof(ClientSceneLoader).GetField("_loadedScenes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return field?.GetValue(_clientLoader) as HashSet<SceneID>;
        }

        private HashSet<SceneID> GetLoadingScenes()
        {
            if (_clientLoader == null) return null;

            var field = typeof(ClientSceneLoader).GetField("_loadingScenes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return field?.GetValue(_clientLoader) as HashSet<SceneID>;
        }
    }
}