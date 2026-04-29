#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.World;
using ProjectC.World.Scene;
using ProjectC.World.Streaming;
using ProjectC.Player;
using ProjectC.Ship;
using ProjectC.Core;
using ProjectC.Network;

namespace ProjectC.Editor
{
    public class WorldSceneSetup : EditorWindow
    {
        private const int ROWS = 4;
        private const int COLS = 6;

        [MenuItem("ProjectC/World/Setup World Scenes Runtime")]
        public static void ShowWindow()
        {
            GetWindow<WorldSceneSetup>("World Scene Setup");
        }

        public void OnGUI()
        {
            GUILayout.Label("World Scene Runtime Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label("This adds runtime components to world scenes for testing:", EditorStyles.helpBox);

            if (GUILayout.Button("Add Runtime Setup to All Scenes", GUILayout.Height(40)))
            {
                SetupAllScenes();
            }

            if (GUILayout.Button("Setup Single Scene (Current)", GUILayout.Height(30)))
            {
                SetupCurrentScene();
            }
        }

        private void SetupAllScenes()
        {
            for (int row = 0; row < ROWS; row++)
            {
                for (int col = 0; col < COLS; col++)
                {
                    string scenePath = $"Assets/_Project/Scenes/World/WorldScene_{row}_{col}.unity";
                    SetupScene(scenePath);
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Complete", "All world scenes updated with runtime setup.", "OK");
        }

        private void SetupCurrentScene()
        {
            Scene currentScene = EditorSceneManager.GetActiveScene();
            string scenePath = currentScene.path;

            if (string.IsNullOrEmpty(scenePath))
            {
                EditorUtility.DisplayDialog("Error", "Please open a world scene first.", "OK");
                return;
            }

            SetupScene(scenePath);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(currentScene);
        }

        private void SetupScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            AddRuntimeObjects(scene);

            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);

            Debug.Log($"[WorldSceneSetup] Updated: {scenePath}");
        }

        private void AddRuntimeObjects(Scene scene)
        {
            GameObject runtimeObj = new GameObject("WorldRuntime");
            runtimeObj.transform.position = Vector3.zero;

            AddWorldSceneManager(runtimeObj.transform);
            AddClientSceneLoader(runtimeObj.transform);
            AddServerSceneManager(runtimeObj.transform);
            AddWorldStreamingManager(runtimeObj.transform);
            AddMainCamera(runtimeObj.transform);
            AddFloatingOriginMP(runtimeObj.transform);
            AddAltitudeCorridorSystem(runtimeObj.transform);
            AddCloudSystem(runtimeObj.transform);
        }

        private void AddWorldSceneManager(Transform parent)
        {
            GameObject obj = new GameObject("WorldSceneManager");
            obj.transform.SetParent(parent);
            obj.transform.localPosition = Vector3.zero;

            obj.AddComponent<WorldSceneManager>();
        }

        private void AddClientSceneLoader(Transform parent)
        {
            GameObject obj = new GameObject("ClientSceneLoader");
            obj.transform.SetParent(parent);
            obj.transform.localPosition = Vector3.zero;

            var loader = obj.AddComponent<ClientSceneLoader>();

            SerializedObject so = new SerializedObject(loader);
            so.FindProperty("loadNeighbors").boolValue = true;
            so.FindProperty("unloadDistantScenes").boolValue = true;
            so.ApplyModifiedProperties();
        }

        private void AddServerSceneManager(Transform parent)
        {
            GameObject obj = new GameObject("ServerSceneManager");
            obj.transform.SetParent(parent);
            obj.transform.localPosition = Vector3.zero;

            var ssm = obj.AddComponent<ServerSceneManager>();

            SerializedObject so = new SerializedObject(ssm);
            so.FindProperty("updateInterval").floatValue = 0.5f;
            so.ApplyModifiedProperties();
        }

        private void AddWorldStreamingManager(Transform parent)
        {
            GameObject obj = new GameObject("WorldStreamingManager");
            obj.transform.SetParent(parent);
            obj.transform.localPosition = Vector3.zero;

            var wst = obj.AddComponent<WorldStreamingManager>();

            SerializedObject so = new SerializedObject(wst);
            so.FindProperty("loadRadius").intValue = 2;
            so.FindProperty("unloadRadius").intValue = 3;
            so.FindProperty("updateInterval").floatValue = 0.5f;
            so.ApplyModifiedProperties();
        }

        private void AddMainCamera(Transform parent)
        {
            GameObject cameraObj = GameObject.Find("MainCamera");
            if (cameraObj == null)
            {
                cameraObj = new GameObject("MainCamera");
                cameraObj.AddComponent<Camera>();
                cameraObj.tag = "MainCamera";
            }
            // FIX: Only add AudioListener in Bootstrap scene, NOT in world scenes
            // World scenes are additive-loaded; AudioListener should only be in Bootstrap
            if (cameraObj.GetComponent<AudioListener>() == null)
            {
                // AudioListener intentionally NOT added here to prevent "2 audio listeners" error
                Debug.LogWarning("[WorldSceneSetup] AudioListener not added to world scene camera to avoid duplicate audio listener error");
            }
            cameraObj.transform.SetParent(parent);
            cameraObj.transform.localPosition = new Vector3(0, 3000, 0);
        }

        private void AddFloatingOriginMP(Transform parent)
        {
            GameObject cameraObj = GameObject.Find("MainCamera");
            if (cameraObj == null)
            {
                cameraObj = new GameObject("MainCamera");
                cameraObj.AddComponent<Camera>();
                cameraObj.tag = "MainCamera";
            }

            var fo = cameraObj.AddComponent<FloatingOriginMP>();

            SerializedObject so = new SerializedObject(fo);
            so.FindProperty("threshold").floatValue = 100000f;
            so.FindProperty("shiftRounding").floatValue = 10000f;
            so.FindProperty("showDebugLogs").boolValue = true;
            so.FindProperty("showDebugHUD").boolValue = true;

            var worldRootNamesProp = so.FindProperty("worldRootNames");
            if (worldRootNamesProp != null)
            {
                worldRootNamesProp.ClearArray();
                string[] names = { "WorldRoot", "Mountains", "Clouds", "World", "Massif", "Peak", "Boundaries" };
                for (int i = 0; i < names.Length; i++)
                {
                    worldRootNamesProp.InsertArrayElementAtIndex(i);
                    worldRootNamesProp.GetArrayElementAtIndex(i).stringValue = names[i];
                }
            }
            so.ApplyModifiedProperties();
        }

        private void AddAltitudeCorridorSystem(Transform parent)
        {
            GameObject obj = new GameObject("AltitudeCorridorSystem");
            obj.transform.SetParent(parent);
            obj.transform.localPosition = Vector3.zero;

            var acs = obj.AddComponent<AltitudeCorridorSystem>();
            SetupDefaultCorridors(acs);
        }

        private void SetupDefaultCorridors(AltitudeCorridorSystem system)
        {
            var corridorAssets = new List<AltitudeCorridorData>();

            corridorAssets.Add(CreateCorridorAsset("veil_lower", "Lower Veil Zone", 100f, 1500f, 500f, 1300f, 100f, 200f, false));
            corridorAssets.Add(CreateCorridorAsset("cloud_layer", "Cloud Layer", 1000f, 3500f, 1200f, 3200f, 200f, 300f, false));
            corridorAssets.Add(CreateCorridorAsset("open_sky", "Open Sky (3500-5000m)", 3500f, 5000f, 3700f, 4800f, 300f, 400f, false));
            corridorAssets.Add(CreateCorridorAsset("high_altitude", "High Altitude (5000-6500m)", 5000f, 6500f, 5200f, 6300f, 400f, 500f, false));
            corridorAssets.Add(CreateCorridorAsset("global", "Global Flight Zone", 0f, 99999f, 0f, 0f, 0f, 0f, true));

            SerializedObject so = new SerializedObject(system);
            var corridorsProp = so.FindProperty("corridors");
            corridorsProp.ClearArray();
            for (int i = 0; i < corridorAssets.Count; i++)
            {
                corridorsProp.InsertArrayElementAtIndex(i);
                corridorsProp.GetArrayElementAtIndex(i).objectReferenceValue = corridorAssets[i];
            }
            so.ApplyModifiedProperties();
        }

        private AltitudeCorridorData CreateCorridorAsset(string id, string name, float minAlt, float maxAlt, float warnLow, float warnHigh, float critLow, float critHigh, bool isGlobal)
        {
            string path = $"Assets/_Project/Data/Ship/AltitudeCorridor_{id}.asset";
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Ship");
            }

            AltitudeCorridorData existing = AssetDatabase.LoadAssetAtPath<AltitudeCorridorData>(path);
            if (existing != null)
            {
                existing.corridorId = id;
                existing.displayName = name;
                existing.minAltitude = minAlt;
                existing.maxAltitude = maxAlt;
                existing.warningMargin = warnLow;
                existing.criticalUpperMargin = critHigh;
                existing.isGlobal = isGlobal;
                existing.cityCenter = Vector3.zero;
                existing.cityRadius = 0f;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            AltitudeCorridorData corridor = ScriptableObject.CreateInstance<AltitudeCorridorData>();
            corridor.corridorId = id;
            corridor.displayName = name;
            corridor.minAltitude = minAlt;
            corridor.maxAltitude = maxAlt;
            corridor.warningMargin = warnLow;
            corridor.criticalUpperMargin = critHigh;
            corridor.isGlobal = isGlobal;
            corridor.cityCenter = Vector3.zero;
            corridor.cityRadius = 0f;

            AssetDatabase.CreateAsset(corridor, path);
            return corridor;
        }

        private void AddCloudSystem(Transform parent)
        {
            GameObject obj = new GameObject("CloudSystem");
            obj.transform.SetParent(parent);
            obj.transform.localPosition = Vector3.zero;

            var cloudSystem = obj.AddComponent<CloudSystem>();

            GameObject layer1 = new GameObject("LowerCloudLayer");
            layer1.transform.SetParent(obj.transform);
            layer1.transform.localPosition = new Vector3(0, 1500f, 0);

            var layer1Comp = layer1.AddComponent<CloudLayer>();

            GameObject layer2 = new GameObject("UpperCloudLayer");
            layer2.transform.SetParent(obj.transform);
            layer2.transform.localPosition = new Vector3(0, 3000f, 0);

            var layer2Comp = layer2.AddComponent<CloudLayer>();
        }

        [ContextMenu("Setup Current Scene Only")]
        public void SetupCurrentSceneContext()
        {
            SetupCurrentScene();
        }
    }
}
#endif