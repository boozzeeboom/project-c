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
    public class TestSceneGenerator : EditorWindow
    {
        private const int ROWS = 4;
        private const int COLS = 6;
        private const float SCENE_SIZE = 79999f;
        private const string TEST_SCENE_PATH = "Assets/_Project/Scenes/Test/WorldTestScene.unity";

        [MenuItem("ProjectC/World/Generate Test Scene")]
        public static void ShowWindow()
        {
            GetWindow<TestSceneGenerator>("World Test Scene");
        }

        public void OnGUI()
        {
            GUILayout.Label("World Test Scene Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label("Creates a single test scene for testing world streaming and scene transitions.", EditorStyles.helpBox);

            if (GUILayout.Button("Generate Test Scene", GUILayout.Height(40)))
            {
                GenerateTestScene();
            }
        }

        private void GenerateTestScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes/Test"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Test");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);

            CreateTestSceneContent();

            EditorSceneManager.SaveScene(scene, TEST_SCENE_PATH);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete", "Test scene created at:\n" + TEST_SCENE_PATH, "OK");
        }

        private void CreateTestSceneContent()
        {
            CreateEventSystem();
            CreateWorldData();
            CreateSceneRegistry();
            CreateWorldSceneManager();
            CreateClientSceneLoader();
            CreateServerSceneManager();
            CreateSceneTransitionCoordinator();
            CreateWorldStreamingManager();
            CreateMainCamera();
            CreateFloatingOriginMP();
            CreateAltitudeCorridorSystem();
            CreateCloudSystem();
            CreatePlayerSpawnPoint();
            CreateStreamingSetup();
        }

        private void CreateEventSystem()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private void CreateWorldData()
        {
            string path = "Assets/_Project/Data/World/WorldData.asset";
            string dir = System.IO.Path.GetDirectoryName(path);

            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data", "World");
            }

            ProjectC.World.Core.WorldData existing = AssetDatabase.LoadAssetAtPath<ProjectC.World.Core.WorldData>(path);
            if (existing != null) return;

            ProjectC.World.Core.WorldData worldData = ScriptableObject.CreateInstance<ProjectC.World.Core.WorldData>();
            worldData.heightScale = 0.01f;
            worldData.distanceScale = 0.0005f;
            worldData.veilHeight = 1200f;
            worldData.veilColor = new Color(0.176f, 0.106f, 0.306f, 1f);
            worldData.veilFogDensity = 0.003f;

            AssetDatabase.CreateAsset(worldData, path);
            AssetDatabase.SaveAssets();
        }

        private void CreateSceneRegistry()
        {
            string path = "Assets/_Project/Data/Scene/SceneRegistry.asset";
            string dir = System.IO.Path.GetDirectoryName(path);

            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Scene");
            }

            SceneRegistry existing = AssetDatabase.LoadAssetAtPath<SceneRegistry>(path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<SceneRegistry>();
                AssetDatabase.CreateAsset(existing, path);
            }

            existing.GridColumns = COLS;
            existing.GridRows = ROWS;
            existing.SceneNamePrefix = "WorldScene_";

            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
        }

        private void CreateWorldSceneManager()
        {
            GameObject obj = new GameObject("WorldSceneManager");
            obj.transform.position = Vector3.zero;

            var wsm = obj.AddComponent<WorldSceneManager>();
        }

        private void CreateClientSceneLoader()
        {
            GameObject obj = new GameObject("ClientSceneLoader");
            obj.transform.position = Vector3.zero;

            var loader = obj.AddComponent<ClientSceneLoader>();

            SerializedObject so = new SerializedObject(loader);
            so.FindProperty("loadNeighbors").boolValue = true;
            so.FindProperty("unloadDistantScenes").boolValue = true;
            so.ApplyModifiedProperties();
        }

        private void CreateServerSceneManager()
        {
            GameObject obj = new GameObject("ServerSceneManager");
            obj.transform.position = Vector3.zero;

            var ssm = obj.AddComponent<ServerSceneManager>();

            SerializedObject so = new SerializedObject(ssm);
            so.FindProperty("updateInterval").floatValue = 0.5f;
            so.ApplyModifiedProperties();
        }

        private void CreateSceneTransitionCoordinator()
        {
            GameObject obj = new GameObject("SceneTransitionCoordinator");
            obj.transform.position = Vector3.zero;

            obj.AddComponent<SceneTransitionCoordinator>();
            var networkObject = obj.AddComponent<NetworkObject>();
        }

        private void CreateWorldStreamingManager()
        {
            GameObject obj = new GameObject("WorldStreamingManager");
            obj.transform.position = Vector3.zero;

            var wst = obj.AddComponent<WorldStreamingManager>();

            SerializedObject so = new SerializedObject(wst);
            so.FindProperty("loadRadius").intValue = 2;
            so.FindProperty("unloadRadius").intValue = 3;
            so.FindProperty("updateInterval").floatValue = 0.5f;
            so.ApplyModifiedProperties();
        }

        private void CreateMainCamera()
        {
            GameObject cameraObj = new GameObject("MainCamera");
            cameraObj.transform.position = new Vector3(COLS * SCENE_SIZE / 2f, 3000f, ROWS * SCENE_SIZE / 2f);
            cameraObj.tag = "MainCamera";

            Camera cam = cameraObj.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.5f, 0.6f, 0.8f);
            cam.clearFlags = CameraClearFlags.Skybox;

            cameraObj.AddComponent<AudioListener>();
        }

        private void CreateFloatingOriginMP()
        {
            GameObject cameraObj = GameObject.Find("MainCamera");
            if (cameraObj == null) return;

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
                string[] names = { "WorldRoot", "Mountains", "Clouds", "Ground", "Boundaries" };
                for (int i = 0; i < names.Length; i++)
                {
                    worldRootNamesProp.InsertArrayElementAtIndex(i);
                    worldRootNamesProp.GetArrayElementAtIndex(i).stringValue = names[i];
                }
            }
            so.ApplyModifiedProperties();
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

            SerializedObject so = new SerializedObject(acs);
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
            layer1.transform.localPosition = new Vector3(0, 1500f, 0);

            var layer1Comp = layer1.AddComponent<CloudLayer>();

            GameObject layer2 = new GameObject("UpperCloudLayer");
            layer2.transform.SetParent(obj.transform);
            layer2.transform.localPosition = new Vector3(0, 3000f, 0);

            var layer2Comp = layer2.AddComponent<CloudLayer>();
        }

        private void CreatePlayerSpawnPoint()
        {
            GameObject spawnObj = new GameObject("PlayerSpawn");
            spawnObj.transform.position = new Vector3(COLS * SCENE_SIZE / 2f, 3000f, ROWS * SCENE_SIZE / 2f);
            spawnObj.tag = "Player";

            var spawner = spawnObj.AddComponent<NetworkPlayerSpawner>();
            var networkObject = spawnObj.AddComponent<NetworkObject>();

            CreatePlayerObject(spawnObj.transform);
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

        private void CreateStreamingSetup()
        {
            GameObject obj = new GameObject("StreamingSetupRuntime");
            obj.transform.position = Vector3.zero;

            var setup = obj.AddComponent<StreamingSetupRuntime>();
        }

        [ContextMenu("Regenerate Test Scene")]
        public void Regenerate()
        {
            GenerateTestScene();
        }
    }
}
#endif