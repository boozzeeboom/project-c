// MARKET-ID-REFACTOR: Editor migration tool — назначает MarketConfig на MarketZone в сценах.
// Запуск: Tools > ProjectC > Trade > Migrate MarketZones to MarketConfig refs
// Читает СТАРОЕ поле "locationId" (string) из сериализованных данных MarketZone,
// находит MarketConfig с таким же locationId, и назначает его в новое поле "_marketConfig".

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.Trade.Network;
using ProjectC.Trade.Config;

namespace ProjectC.Trade.Editor
{
    public static class MarketZoneMigrationTool
    {
        [MenuItem("Tools/ProjectC/Trade/Migrate MarketZones to MarketConfig refs")]
        public static void Migrate()
        {
            var allConfigGuids = AssetDatabase.FindAssets("t:MarketConfig");
            var configByLocId = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var guid in allConfigGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<MarketConfig>(path);
                if (cfg == null || string.IsNullOrEmpty(cfg.locationId)) continue;
                var key = MarketConfigCollector.NormalizeLocationId(cfg.locationId);
                if (!configByLocId.ContainsKey(key))
                    configByLocId[key] = path;
            }

            int migrated = 0, failed = 0;

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    var zones = root.GetComponentsInChildren<MarketZone>(true);
                    foreach (var zone in zones)
                    {
                        var so = new SerializedObject(zone);

                        // Новое поле _marketConfig
                        var cfgProp = so.FindProperty("_marketConfig");
                        if (cfgProp == null) continue;
                        if (cfgProp.objectReferenceValue != null) continue; // уже назначен

                        // Старое поле locationId (string) — всё ещё в сериализованных данных
                        var oldLocProp = so.FindProperty("locationId");
                        string oldLocId = oldLocProp != null ? oldLocProp.stringValue : "";
                        if (string.IsNullOrEmpty(oldLocId))
                        {
                            // Пробуем по имени GameObject
                            oldLocId = zone.gameObject.name;
                        }

                        var normLoc = MarketConfigCollector.NormalizeLocationId(oldLocId);
                        if (configByLocId.TryGetValue(normLoc, out var cfgPath))
                        {
                            var cfg = AssetDatabase.LoadAssetAtPath<MarketConfig>(cfgPath);
                            cfgProp.objectReferenceValue = cfg;
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(zone);
                            migrated++;
                            Debug.Log($"[MarketZoneMigration] {zone.gameObject.name}: assigned MarketConfig '{cfg.locationId}' (old locationId='{oldLocId}')");
                        }
                        else
                        {
                            failed++;
                            Debug.LogWarning($"[MarketZoneMigration] {zone.gameObject.name}: no MarketConfig found for old locationId='{oldLocId}'");
                        }
                    }
                }
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[MarketZoneMigration] Done: {migrated} migrated, {failed} unresolved");
        }

        [MenuItem("Tools/ProjectC/Trade/Add MarketConfig_Primium_test to MarketServer")]
        public static void AddPrimiumTestToMarketServer()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<MarketConfig>(
                "Assets/_Project/Trade/Data/Markets/MarketConfig_Primium_test.asset");
            if (cfg == null)
            {
                Debug.LogError("[AddPrimiumTest] MarketConfig_Primium_test.asset not found");
                return;
            }

            // Find MarketServer in BootstrapScene
            var bootstrapPath = "Assets/_Project/Scenes/BootstrapScene.unity";
            var bootstrapScene = EditorSceneManager.OpenScene(bootstrapPath, OpenSceneMode.Additive);
            var marketServers = Object.FindObjectsByType<ProjectC.Trade.Network.MarketServer>(
                FindObjectsInactive.Include);

            foreach (var ms in marketServers)
            {
                var so = new SerializedObject(ms);
                var listProp = so.FindProperty("marketConfigs");
                if (listProp == null) continue;

                // Check if already present
                bool alreadyThere = false;
                for (int i = 0; i < listProp.arraySize; i++)
                {
                    if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == cfg)
                    {
                        alreadyThere = true;
                        break;
                    }
                }
                if (alreadyThere)
                {
                    Debug.Log("[AddPrimiumTest] MarketConfig_Primium_test already in MarketServer list");
                    continue;
                }

                listProp.arraySize++;
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = cfg;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ms);
                Debug.Log("[AddPrimiumTest] Added MarketConfig_Primium_test to MarketServer.marketConfigs");
            }

            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[AddPrimiumTest] Done — saved BootstrapScene");
        }
    }
}
#endif
