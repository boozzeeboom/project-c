#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectC.World.Core;
using ProjectC.World.Streaming;
using ProjectC.World; // Для WorldStreamingManager, StreamingTest

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor Window для навигации по пику мира в Scene View.
    /// Меню: Tools → Project C → World → Scene Navigator
    /// </summary>
    public class SceneNavigatorWindow : EditorWindow
    {
        private Vector2 scrollPos;
        private Vector2 inputCoords = Vector2.zero;
        private WorldData worldData;
        private bool showCoords = true;
        private bool showPeaks = true;

        // Захардкоженные основные пики для навигации
        private static readonly PeakNavigationPoint[] hardcodedPeaks = new PeakNavigationPoint[]
        {
            new PeakNavigationPoint("Everest", new Vector3(0, 0, 0)),
            new PeakNavigationPoint("K2", new Vector3(500, 0, 300)),
            new PeakNavigationPoint("Kilimanjaro", new Vector3(-300, 0, -500)),
            new PeakNavigationPoint("Mont Blanc", new Vector3(200, 0, -200)),
            new PeakNavigationPoint("Aconcagua", new Vector3(-400, 0, 400)),
            new PeakNavigationPoint("Denali", new Vector3(600, 0, -100)),
        };

        [MenuItem("Tools/Project C/World/Scene Navigator")]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneNavigatorWindow>("Scene Navigator");
            window.minSize = new Vector2(300, 400);
        }

        private void OnEnable()
        {
            // Пробуем найти WorldData в проекте
            TryFindWorldData();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawWorldDataSection();
            EditorGUILayout.Space();
            DrawCoordinateInputSection();
            EditorGUILayout.Space();
            DrawPeaksListSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawWorldDataSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("World Data", EditorStyles.boldLabel);

            worldData = (WorldData)EditorGUILayout.ObjectField("World Data", worldData, typeof(WorldData), false);

            if (worldData == null)
            {
                EditorGUILayout.HelpBox("WorldData не назначена. Используются захардкоженные пики.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField($"Массивов: {worldData.massifs?.Count ?? 0}");
                EditorGUILayout.LabelField($"Границы: X[{worldData.worldMinX}..{worldData.worldMaxX}], Z[{worldData.worldMinZ}..{worldData.worldMaxZ}]");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCoordinateInputSection()
        {
            EditorGUILayout.BeginVertical("box");
            showCoords = EditorGUILayout.Foldout(showCoords, "Произвольные координаты", true);

            if (showCoords)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("X", GUILayout.Width(20));
                inputCoords.x = EditorGUILayout.FloatField(inputCoords.x);
                EditorGUILayout.LabelField("Z", GUILayout.Width(20));
                inputCoords.y = EditorGUILayout.FloatField(inputCoords.y);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Переместить камеру"))
                {
                    TeleportToCoordinates(inputCoords.x, inputCoords.y);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPeaksListSection()
        {
            EditorGUILayout.BeginVertical("box");
            showPeaks = EditorGUILayout.Foldout(showPeaks, "Список пиков", true);

            if (showPeaks)
            {
                var peaks = GetPeakNavigationPoints();

                if (peaks.Length == 0)
                {
                    EditorGUILayout.HelpBox("Нет доступных пиков для навигации.", MessageType.Info);
                }
                else
                {
                    foreach (var peak in peaks)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(peak.Name, GUILayout.Width(150));
                        EditorGUILayout.LabelField($"({peak.Position.x:F0}, {peak.Position.z:F0})", GUILayout.Width(100));

                        if (GUILayout.Button("Переместить", GUILayout.Width(100)))
                        {
                            TeleportToCoordinates(peak.Position.x, peak.Position.z);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Получить список пиков для навигации (из WorldData или захардкоженные).
        /// </summary>
        private PeakNavigationPoint[] GetPeakNavigationPoints()
        {
            if (worldData != null && worldData.massifs != null && worldData.massifs.Count > 0)
            {
                var peaks = new List<PeakNavigationPoint>();

                foreach (var massif in worldData.massifs)
                {
                    if (massif == null || massif.peaks == null) continue;

                    foreach (var peak in massif.peaks)
                    {
                        if (peak == null) continue;

                        string name = string.IsNullOrEmpty(peak.displayName) ? peak.peakId : peak.displayName;
                        peaks.Add(new PeakNavigationPoint(name, peak.worldPosition));
                    }
                }

                if (peaks.Count > 0)
                    return peaks.ToArray();
            }

            return hardcodedPeaks;
        }

        /// <summary>
        /// Телепортировать камеру Scene View к заданным XZ координатам.
        /// </summary>
        private void TeleportToCoordinates(float x, float z)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                EditorUtility.DisplayDialog("Scene Navigator", "Нет активной Scene View.", "OK");
                return;
            }

            // Определяем Y позицию — пробуем получить высоту из WorldData или используем дефолт
            float y = GetApproximateHeight(x, z);

            Vector3 position = new Vector3(x, y, z);
            Quaternion rotation = sceneView.rotation;
            float size = sceneView.size;

            sceneView.LookAtDirect(position, rotation, size);
            sceneView.Repaint();

            Debug.Log($"[Scene Navigator] Камера перемещена к ({x:F0}, {y:F0}, {z:F0})");
        }

        /// <summary>
        /// Получить приблизительную высоту Y для XZ координат.
        /// </summary>
        private float GetApproximateHeight(float x, float z)
        {
            if (worldData != null && worldData.massifs != null)
            {
                foreach (var massif in worldData.massifs)
                {
                    if (massif == null || massif.peaks == null) continue;

                    foreach (var peak in massif.peaks)
                    {
                        if (peak == null) continue;

                        Vector3 pos = peak.worldPosition;
                        float dx = x - pos.x;
                        float dz = z - pos.z;
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);

                        if (dist < peak.baseRadius * 2)
                        {
                            // Внутри радиуса пика — возвращаем высоту пика
                            return pos.y;
                        }
                    }
                }
            }

            // Дефолтная высота
            return 100f;
        }

        /// <summary>
        /// Попытаться найти WorldData в проекте.
        /// </summary>
        private void TryFindWorldData()
        {
            string[] guids = AssetDatabase.FindAssets("t:WorldData");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                worldData = AssetDatabase.LoadAssetAtPath<WorldData>(path);
            }
        }

        private struct PeakNavigationPoint
        {
            public string Name;
            public Vector3 Position;

            public PeakNavigationPoint(string name, Vector3 position)
            {
                Name = name;
                Position = position;
            }
        }
    }

    /// <summary>
    /// Chunk Visualizer — отображение сетки чанков в Scene View через Gizmos.
    /// Интеграция с WorldChunkManager для отображения состояний чанков.
    /// </summary>
    public static class ChunkVisualizer
    {
        // Настройки визуализации
        private static bool s_showChunkGrid = true;
        private static int s_gridSize = 5;
        private static int s_displayRadius = 3;

        // Цвета для состояний чанков
        private static readonly Color ColorLoaded = new Color(0.2f, 0.8f, 0.2f, 0.5f);     // Зелёный
        private static readonly Color ColorLoading = new Color(0.9f, 0.9f, 0.2f, 0.5f);    // Жёлтый
        private static readonly Color ColorUnloaded = new Color(0.5f, 0.5f, 0.5f, 0.3f);   // Серый
        private static readonly Color ColorUnloading = new Color(0.9f, 0.2f, 0.2f, 0.5f);  // Красный
        private static readonly Color ColorGridLine = new Color(0.3f, 0.3f, 0.3f, 0.4f);   // Серый для линий

        [MenuItem("Tools/Project C/World/Toggle Chunk Grid")]
        public static void ToggleChunkGrid()
        {
            s_showChunkGrid = !s_showChunkGrid;
            EditorUtility.DisplayDialog(
                "Chunk Visualizer",
                $"Chunk Grid: {(s_showChunkGrid ? "ON" : "OFF")}",
                "OK"
            );
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Project C/World/Toggle Chunk Grid", true)]
        private static bool ValidateToggleChunkGrid()
        {
            return true;
        }

        /// <summary>
        /// Вызывается из MonoBehaviour.OnDrawGizmosSelected или OnDrawGizmos
        /// для отрисовки сетки чанков.
        /// </summary>
        public static void DrawChunkGrid()
        {
            if (!s_showChunkGrid)
                return;

            // Пробуем получить WorldChunkManager из сцены
            var chunkManager = Object.FindAnyObjectByType<WorldChunkManager>();
            if (chunkManager == null)
            {
                DrawFallbackGrid();
                return;
            }

            DrawChunkGridFromManager(chunkManager);
        }

        /// <summary>
        /// Отрисовка сетки чанков на основе данных из WorldChunkManager.
        /// </summary>
        private static void DrawChunkGridFromManager(WorldChunkManager manager)
        {
            var allChunks = manager.GetAllChunks();
            if (allChunks == null || allChunks.Count == 0)
                return;

            // Определяем центр на основе позиции камеры
            var sceneView = SceneView.lastActiveSceneView;
            Vector3 center = sceneView != null ? sceneView.camera.transform.position : Vector3.zero;
            ChunkId centerChunk = manager.GetChunkAtPosition(center);

            foreach (var chunk in allChunks)
            {
                // Проверяем, входит ли чанк в радиус отображения
                int dx = Mathf.Abs(chunk.Id.GridX - centerChunk.GridX);
                int dz = Mathf.Abs(chunk.Id.GridZ - centerChunk.GridZ);

                if (dx > s_displayRadius || dz > s_displayRadius)
                    continue;

                // Выбираем цвет по состоянию
                Color chunkColor = GetColorForState(chunk.State);

                // Рисуем bounds чанка
                DrawChunkBounds(chunk.WorldBounds, chunkColor);

                // Рисуем label с ID чанка
                DrawChunkLabel(chunk);
            }
        }

        /// <summary>
        /// Резервная отрисовка сетки, если WorldChunkManager недоступен.
        /// </summary>
        private static void DrawFallbackGrid()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return;

            Vector3 center = sceneView.camera.transform.position;
            int chunkSize = WorldChunkManager.ChunkSize;

            // Вычисляем центральный чанк
            int centerX = Mathf.FloorToInt(center.x / chunkSize);
            int centerZ = Mathf.FloorToInt(center.z / chunkSize);

            int halfSize = Mathf.Min(s_gridSize, s_displayRadius) / 2;

            for (int x = centerX - halfSize; x <= centerX + halfSize; x++)
            {
                for (int z = centerZ - halfSize; z <= centerZ + halfSize; z++)
                {
                    float minX = x * chunkSize;
                    float minZ = z * chunkSize;
                    float maxX = minX + chunkSize;
                    float maxZ = minZ + chunkSize;

                    Vector3 chunkCenter = new Vector3(
                        (minX + maxX) * 0.5f,
                        0f,
                        (minZ + maxZ) * 0.5f
                    );

                    Bounds bounds = new Bounds(chunkCenter, new Vector3(chunkSize, 1000f, chunkSize));

                    // Рисуем серый прямоугольник для unloaded чанка
                    DrawChunkBounds(bounds, ColorUnloaded);

                    // Label с координатами чанка
                    Handles.Label(
                        new Vector3(chunkCenter.x, 10f, chunkCenter.z),
                        $"Chunk({x}, {z})"
                    );
                }
            }
        }

        /// <summary>
        /// Получить цвет для состояния чанка.
        /// </summary>
        private static Color GetColorForState(ChunkState state)
        {
            switch (state)
            {
                case ChunkState.Loaded:
                    return ColorLoaded;
                case ChunkState.Loading:
                    return ColorLoading;
                case ChunkState.Unloaded:
                    return ColorUnloaded;
                case ChunkState.Unloading:
                    return ColorUnloading;
                default:
                    return ColorUnloaded;
            }
        }

        /// <summary>
        /// Нарисовать границы чанка как wireframe cube.
        /// </summary>
        private static void DrawChunkBounds(Bounds bounds, Color color)
        {
            Color oldColor = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.color = oldColor;
        }

        /// <summary>
        /// Нарисовать label с информацией о чанке.
        /// </summary>
        private static void DrawChunkLabel(WorldChunk chunk)
        {
            Vector3 labelPos = new Vector3(
                chunk.WorldBounds.center.x,
                10f,
                chunk.WorldBounds.center.z
            );

            string stateSymbol = GetStateSymbol(chunk.State);
            string label = $"[{chunk.Id.GridX}, {chunk.Id.GridZ}] {stateSymbol}";

            if (chunk.Peaks != null && chunk.Peaks.Count > 0)
            {
                label += $" ({chunk.Peaks.Count} peaks)";
            }

            Handles.Label(labelPos, label);
        }

        /// <summary>
        /// Получить символ для состояния чанка.
        /// </summary>
        private static string GetStateSymbol(ChunkState state)
        {
            switch (state)
            {
                case ChunkState.Loaded:    return "[L]";
                case ChunkState.Loading:   return "[>]";
                case ChunkState.Unloaded:  return "[ ]";
                case ChunkState.Unloading: return "[<]";
                default:                   return "[?]";
            }
        }

        // ---- Настройки визуализации через Editor Window ----

        [MenuItem("Tools/Project C/World/Chunk Visualizer Settings")]
        public static void ShowSettingsWindow()
        {
            var window = EditorWindow.GetWindow<ChunkVisualizerSettings>("Chunk Viz Settings");
            window.minSize = new Vector2(250, 150);
        }
    }

    /// <summary>
    /// Editor Window для настроек Chunk Visualizer.
    /// </summary>
    public class ChunkVisualizerSettings : EditorWindow
    {
        private void OnGUI()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Chunk Visualizer Settings", EditorStyles.boldLabel);

            bool showGrid = IsChunkGridVisible();
            bool newShowGrid = EditorGUILayout.Toggle("Show Chunk Grid", showGrid);
            if (newShowGrid != showGrid)
            {
                ChunkVisualizer.ToggleChunkGrid();
            }

            EditorGUILayout.Space();

            int gridSize = GetGridSize();
            int newGridSize = EditorGUILayout.IntSlider("Grid Size", gridSize, 3, 20);
            if (newGridSize != gridSize)
            {
                SetGridSize(newGridSize);
            }

            int displayRadius = GetDisplayRadius();
            int newDisplayRadius = EditorGUILayout.IntSlider("Display Radius", displayRadius, 1, 10);
            if (newDisplayRadius != displayRadius)
            {
                SetDisplayRadius(newDisplayRadius);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            if (GUILayout.Button("Repaint Scene View"))
            {
                SceneView.RepaintAll();
            }
        }

        private bool IsChunkGridVisible()
        {
            // Используем reflection для доступа к приватному полю
            var field = typeof(ChunkVisualizer).GetField("s_showChunkGrid",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return field != null && (bool)field.GetValue(null);
        }

        private int GetGridSize()
        {
            var field = typeof(ChunkVisualizer).GetField("s_gridSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return field != null ? (int)field.GetValue(null) : 5;
        }

        private void SetGridSize(int value)
        {
            var field = typeof(ChunkVisualizer).GetField("s_gridSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (field != null) field.SetValue(null, value);
        }

        private int GetDisplayRadius()
        {
            var field = typeof(ChunkVisualizer).GetField("s_displayRadius",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return field != null ? (int)field.GetValue(null) : 3;
        }

        private void SetDisplayRadius(int value)
        {
            var field = typeof(ChunkVisualizer).GetField("s_displayRadius",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (field != null) field.SetValue(null, value);
        }
    }

    /// <summary>
    /// MonoBehaviour-компонент для отрисовки Gizmos чанков.
    /// Добавьте этот компонент на GameObject с WorldChunkManager.
    /// </summary>
    public class ChunkGizmoRenderer : MonoBehaviour
    {
        private void OnDrawGizmosSelected()
        {
            ChunkVisualizer.DrawChunkGrid();
        }
    }
    
    /// <summary>
    /// Setup World Streaming System — создаёт все необходимые компоненты в сцене.
    /// Меню: Tools → Project C → World → Setup Streaming
    /// Компоненты найдут друг друга через AutoFindComponents() при старте.
    /// </summary>
    public static class StreamingSetup
    {
        [MenuItem("Tools/Project C/World/Setup Streaming")]
        public static void SetupStreamingSystem()
        {
            // Создаём корневой объект для всей системы стриминга
            GameObject streamingRoot = GameObject.Find("WorldStreaming");
            if (streamingRoot == null)
            {
                streamingRoot = new GameObject("WorldStreaming");
                Undo.RegisterCreatedObjectUndo(streamingRoot, "Create WorldStreaming Root");
                Debug.Log("[StreamingSetup] ✅ Created 'WorldStreaming' root object");
            }
            
            // 1. WorldChunkManager
            if (streamingRoot.GetComponent<WorldChunkManager>() == null)
            {
                streamingRoot.AddComponent<WorldChunkManager>();
                Debug.Log("[StreamingSetup] ✅ Added WorldChunkManager");
            }
            
            // 2. ProceduralChunkGenerator
            if (streamingRoot.GetComponent<ProceduralChunkGenerator>() == null)
            {
                streamingRoot.AddComponent<ProceduralChunkGenerator>();
                Debug.Log("[StreamingSetup] ✅ Added ProceduralChunkGenerator");
            }
            
            // 3. ChunkLoader
            if (streamingRoot.GetComponent<ChunkLoader>() == null)
            {
                streamingRoot.AddComponent<ChunkLoader>();
                Debug.Log("[StreamingSetup] ✅ Added ChunkLoader");
            }
            
            // 4. FloatingOriginMP — оставляем на WorldStreaming root (он найдёт камеру в runtime)
            if (streamingRoot.GetComponent<FloatingOriginMP>() == null)
            {
                streamingRoot.AddComponent<FloatingOriginMP>();
                Debug.Log("[StreamingSetup] ✅ Added FloatingOriginMP to WorldStreaming root");
            }
            
            // 5. WorldStreamingManager — найти или создать
            WorldStreamingManager streamingManager = Object.FindAnyObjectByType<WorldStreamingManager>();
            if (streamingManager == null)
            {
                GameObject smObj = new GameObject("WorldStreamingManager");
                Undo.RegisterCreatedObjectUndo(smObj, "Create WorldStreamingManager");
                streamingManager = smObj.AddComponent<WorldStreamingManager>();
                Debug.Log("[StreamingSetup] ✅ Created WorldStreamingManager");
            }
            
            // 6. StreamingTest добавится автоматически через StreamingTestAutoRunner при Play
            Debug.Log("[StreamingSetup] ℹ️ StreamingTest will be added automatically when Play is pressed");
            
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            EditorUtility.DisplayDialog("Setup Complete", 
                "✅ World Streaming System setup complete!\n\nComponents will auto-link on Play.\nPress Play and use F5-F10 to test.", "OK");
            
            Selection.activeGameObject = streamingManager != null ? streamingManager.gameObject : streamingRoot;
        }
        
        [MenuItem("Tools/Project C/World/Remove Streaming System")]
        public static void RemoveStreamingSystem()
        {
            if (!EditorUtility.DisplayDialog("Remove", 
                "Remove all streaming components?", "Yes", "Cancel"))
            {
                return;
            }
            
            foreach (var obj in Object.FindObjectsByType<WorldChunkManager>(FindObjectsInactive.Exclude))
                Undo.DestroyObjectImmediate(obj);
            foreach (var obj in Object.FindObjectsByType<ProceduralChunkGenerator>(FindObjectsInactive.Exclude))
                Undo.DestroyObjectImmediate(obj);
            foreach (var obj in Object.FindObjectsByType<ChunkLoader>(FindObjectsInactive.Exclude))
                Undo.DestroyObjectImmediate(obj);
            foreach (var obj in Object.FindObjectsByType<FloatingOriginMP>(FindObjectsInactive.Exclude))
                Undo.DestroyObjectImmediate(obj);
            foreach (var obj in Object.FindObjectsByType<StreamingTest>(FindObjectsInactive.Exclude))
                Undo.DestroyObjectImmediate(obj);
            foreach (var obj in Object.FindObjectsByType<StreamingTest_AutoRun>(FindObjectsInactive.Exclude))
                Undo.DestroyObjectImmediate(obj);
            
            GameObject root = GameObject.Find("WorldStreaming");
            if (root != null)
                Undo.DestroyObjectImmediate(root);
            
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[StreamingSetup] 🗑️ Removed");
        }
    }
}
#endif
