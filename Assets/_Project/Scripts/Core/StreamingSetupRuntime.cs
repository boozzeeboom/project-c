using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace ProjectC.Core
{
    /// <summary>
    /// Runtime настройка для World Streaming в основной сцене.
    /// Исправляет проблемы с FloatingOriginMP если сцена не была настроена через PrepareMainScene.
    /// 
    /// Добавьте этот компонент на пустой объект в сцене если FloatingOriginMP не работает.
    /// </summary>
    public class StreamingSetupRuntime : MonoBehaviour
    {
// [WARNINGS] autoFindStreaming is reserved for future use
        
        [Header("WorldRoot")]
        [Tooltip("Имя объекта WorldRoot (пустой = используется по умолчанию)")]
        [SerializeField] private string worldRootName = "WorldRoot";
        
        [Tooltip("Автоматически собрать world objects под WorldRoot")]
        [SerializeField] private bool autoOrganizeWorldRoot = true;
        
        [Header("World Object Names")]
        [Tooltip("TradeZones НЕ включен! TradeZones — корень сцены с камерой игрока.")]
        [SerializeField] private string[] worldObjectNames = new string[]
        {
            "Mountains",
            "Clouds",
            "Farms",
            "World",
            "Massif",
            "Peak",
            "CloudLayer",
            "Farm",
            "ChunksContainer",
            "Platforms"
            // TradeZones ИСКЛЮЧЁН — там камера!
        };
        
        [Header("FloatingOriginMP")]
        [Tooltip("Автоматически настроить FloatingOriginMP")]
        [SerializeField] private bool autoSetupFloatingOrigin = true;
        
        [Tooltip("Порог сдвига мира (units)")]
        [SerializeField] private float threshold = 100000f;
        
        [Tooltip("Округление сдвига")]
        [SerializeField] private float shiftRounding = 10000f;
        
        [Header("StreamingTest")]
        [Tooltip("Добавить StreamingTest компонент")]
        [SerializeField] private bool addStreamingTest = true;
        
        private void Awake()
        {
            Debug.Log("[StreamingSetupRuntime] Initializing...");
            
            if (autoOrganizeWorldRoot)
            {
                OrganizeWorldRoot();
            }
            
            if (autoSetupFloatingOrigin)
            {
                SetupFloatingOrigin();
            }
            
            if (addStreamingTest)
            {
                AddStreamingTest();
            }
            
            Debug.Log("[StreamingSetupRuntime] Initialization complete!");
        }
        
        /// <summary>
        /// Найти или создать WorldRoot и поместить туда все world objects.
        /// </summary>
        private void OrganizeWorldRoot()
        {
            // Найти WorldRoot
            GameObject worldRoot = GameObject.Find(worldRootName);
            
            if (worldRoot == null)
            {
                // Попробуем найти любой из world objects
                foreach (var name in worldObjectNames)
                {
                    GameObject existing = GameObject.Find(name);
                    if (existing != null)
                    {
                        worldRoot = existing;
                        worldRoot.name = worldRootName;
                        Debug.Log($"[StreamingSetupRuntime] Using '{name}' as WorldRoot");
                        break;
                    }
                }
            }
            
            if (worldRoot == null)
            {
                worldRoot = new GameObject(worldRootName);
                Debug.Log("[StreamingSetupRuntime] Created new WorldRoot");
            }
            
            // Убеждаемся что WorldRoot на.origin
            worldRoot.transform.position = Vector3.zero;
            
            // Получаем все root-объекты сцены (не имеют parent)
            Scene scene = worldRoot.scene;
            var rootObjects = scene.GetRootGameObjects();
            
            int childrenMoved = 0;
            foreach (var rootObj in rootObjects)
            {
                // Пропускаем WorldRoot и системные объекты
                if (rootObj == worldRoot) continue;
                if (rootObj.name == "EventSystem") continue;
                if (rootObj.name.Contains("NetworkManager")) continue;
                if (rootObj.name.Contains("StreamingTest")) continue;
                if (rootObj.name.Contains("StreamingSetup")) continue;
                
                // Проверяем является ли объект объектом мира
                string objName = rootObj.name;
                bool isWorldObject = false;
                foreach (var name in worldObjectNames)
                {
                    if (objName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                        objName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    {
                        isWorldObject = true;
                        break;
                    }
                }
                
                // Также проверяем дочерние объекты - если внутри есть Mountains/Clouds/etc
                bool hasWorldChild = false;
                foreach (var name in worldObjectNames)
                {
                    if (rootObj.transform.Find(name) != null)
                    {
                        hasWorldChild = true;
                        break;
                    }
                }
                
                if (isWorldObject || hasWorldChild)
                {
                    rootObj.transform.SetParent(worldRoot.transform);
                    Debug.Log($"[StreamingSetupRuntime] Moved '{rootObj.name}' under WorldRoot (isWorld={isWorldObject}, hasWorldChild={hasWorldChild})");
                    childrenMoved++;
                }
            }
            
            Debug.Log($"[StreamingSetupRuntime] WorldRoot organized: {worldRoot.transform.childCount} children, {childrenMoved} moved from root");
        }
        
        /// <summary>
        /// Настроить FloatingOriginMP на Main Camera.
        /// </summary>
        private void SetupFloatingOrigin()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[StreamingSetupRuntime] Main Camera not found!");
                return;
            }
            
            // Найти FloatingOriginMP
            var foType = System.Type.GetType("ProjectC.World.Streaming.FloatingOriginMP, Assembly-CSharp");
            if (foType == null)
            {
                Debug.LogWarning("[StreamingSetupRuntime] FloatingOriginMP type not found!");
                return;
            }
            
            Component fo = mainCamera.GetComponent(foType);
            
            if (fo == null)
            {
                fo = mainCamera.gameObject.AddComponent(foType);
                Debug.Log("[StreamingSetupRuntime] Added FloatingOriginMP to Main Camera");
            }
            else
            {
                Debug.Log("[StreamingSetupRuntime] FloatingOriginMP already exists on Main Camera");
            }
            
            // Настроить properties через reflection (только в Editor)
            #if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(fo);
            
            var thresholdProp = so.FindProperty("threshold");
            if (thresholdProp != null)
                thresholdProp.floatValue = threshold;
            
            var shiftRoundingProp = so.FindProperty("shiftRounding");
            if (shiftRoundingProp != null)
                shiftRoundingProp.floatValue = shiftRounding;
            
            var worldRootNamesProp = so.FindProperty("worldRootNames");
            if (worldRootNamesProp != null)
            {
                worldRootNamesProp.ClearArray();
                foreach (var name in worldObjectNames)
                {
                    worldRootNamesProp.InsertArrayElementAtIndex(worldRootNamesProp.arraySize);
                    worldRootNamesProp.GetArrayElementAtIndex(worldRootNamesProp.arraySize - 1).stringValue = name;
                }
                // Добавляем стандартные имена
                worldRootNamesProp.InsertArrayElementAtIndex(worldRootNamesProp.arraySize);
                worldRootNamesProp.GetArrayElementAtIndex(worldRootNamesProp.arraySize - 1).stringValue = worldRootName;
            }
            
            var showDebugLogsProp = so.FindProperty("showDebugLogs");
            if (showDebugLogsProp != null)
                showDebugLogsProp.boolValue = true;
            
            var showDebugHUDProp = so.FindProperty("showDebugHUD");
            if (showDebugHUDProp != null)
                showDebugHUDProp.boolValue = true;
            
            so.ApplyModifiedProperties();
            #endif
            
            Debug.Log($"[StreamingSetupRuntime] FloatingOriginMP configured: threshold={threshold}, worldRootNames={worldObjectNames.Length + 1}");
        }
        
        /// <summary>
        /// Добавить StreamingTest компонент.
        /// </summary>
        private void AddStreamingTest()
        {
            // Найти StreamingTest
            var testType = System.Type.GetType("ProjectC.World.StreamingTest, Assembly-CSharp");
            if (testType == null)
            {
                Debug.LogWarning("[StreamingSetupRuntime] StreamingTest type not found!");
                return;
            }
            
            // Найти или создать объект
            GameObject testObj = GameObject.Find("StreamingTest");
            if (testObj == null)
            {
                testObj = new GameObject("StreamingTest");
                Debug.Log("[StreamingSetupRuntime] Created StreamingTest object");
            }
            
            Component test = testObj.GetComponent(testType);
            if (test == null)
            {
                test = testObj.AddComponent(testType);
                Debug.Log("[StreamingSetupRuntime] Added StreamingTest component");
            }
            
            // Настроить (только в Editor)
            #if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(test);
            
            var useLocalPlayerProp = so.FindProperty("useLocalPlayerPosition");
            if (useLocalPlayerProp != null)
                useLocalPlayerProp.boolValue = true;
            
            var teleportPlayerProp = so.FindProperty("teleportPlayer");
            if (teleportPlayerProp != null)
                teleportPlayerProp.boolValue = true;
            
            // Найти WorldStreamingManager
            var wsmType = System.Type.GetType("ProjectC.World.WorldStreamingManager, Assembly-CSharp");
            if (wsmType != null)
            {
                var wsmObj = GameObject.Find("WorldStreamingManager");
                if (wsmObj != null)
                {
                    var wsm = wsmObj.GetComponent(wsmType);
                    if (wsm != null)
                    {
                        var streamingManagerProp = so.FindProperty("streamingManager");
                        if (streamingManagerProp != null)
                            streamingManagerProp.objectReferenceValue = wsm;
                    }
                }
            }
            
            so.ApplyModifiedProperties();
            #endif
            
            Debug.Log("[StreamingSetupRuntime] StreamingTest configured");
        }
        
        /// <summary>
        /// Вызвать из консоли для отладки.
        /// </summary>
        [ContextMenu("Force Reinitialize")]
        public void ForceReinitialize()
        {
            Awake();
        }
    }
}
