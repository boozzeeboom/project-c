#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using WindZoneData = ProjectC.Ship.WindZoneData;
using WindZone = ProjectC.Ship.WindZone;
using WindProfile = ProjectC.Ship.WindProfile;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor утилита для создания тестовых зон ветра.
    /// Menu: Tools → Project C → Create Wind Zone Test Scene
    /// </summary>
    public static class CreateWindZoneTestScene
    {
        [MenuItem("Tools/Project C/Create Wind Zone Test Scene")]
        public static void CreateTestScene()
        {
            string dataPath = "Assets/_Project/Data/WindZones";
            
            // Создаём директорию если нет
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            // 1. Constant Wind Zone
            CreateWindZone(
                zoneId: "constant_test",
                displayName: "Constant Wind Test",
                windDirection: new Vector3(-1f, 0f, 1f),
                windForce: 50f,
                windVariation: 0.1f,
                profile: WindProfile.Constant,
                gustInterval: 2f,
                shearGradient: 0.1f,
                position: new Vector3(0f, 3000f, 0f),
                size: new Vector3(30f, 15f, 30f),
                dataPath: dataPath
            );

            // 2. Gust Wind Zone
            CreateWindZone(
                zoneId: "gust_test",
                displayName: "Gust Wind Test",
                windDirection: Vector3.forward,
                windForce: 80f,
                windVariation: 0.3f,
                profile: WindProfile.Gust,
                gustInterval: 2f,
                shearGradient: 0.1f,
                position: new Vector3(50f, 3000f, 0f),
                size: new Vector3(30f, 15f, 30f),
                dataPath: dataPath
            );

            // 3. Shear Wind Zone
            CreateWindZone(
                zoneId: "shear_test",
                displayName: "Shear Wind Test",
                windDirection: new Vector3(1f, 0.2f, 0f),
                windForce: 40f,
                windVariation: 0.2f,
                profile: WindProfile.Shear,
                gustInterval: 2f,
                shearGradient: 0.15f,
                position: new Vector3(-50f, 3000f, 0f),
                size: new Vector3(30f, 20f, 30f),
                dataPath: dataPath
            );

            Debug.Log("[CreateWindZoneTestScene] Created 3 wind zones for testing!");
            EditorUtility.DisplayDialog("Wind Zones Created", 
                "Created 3 test wind zones:\n\n" +
                "1. Constant Wind (NW, 50N)\n" +
                "2. Gust Wind (Forward, 80N, varies)\n" +
                "3. Shear Wind (Up+East, 40N, height-based)\n\n" +
                "Data saved to: Assets/_Project/Data/WindZones/", 
                "OK");
        }

        private static void CreateWindZone(
            string zoneId,
            string displayName,
            Vector3 windDirection,
            float windForce,
            float windVariation,
            WindProfile profile,
            float gustInterval,
            float shearGradient,
            Vector3 position,
            Vector3 size,
            string dataPath)
        {
            // Создаём ScriptableObject
            WindZoneData windData = ScriptableObject.CreateInstance<WindZoneData>();
            windData.zoneId = zoneId;
            windData.displayName = displayName;
            windData.windDirection = windDirection;
            windData.windForce = windForce;
            windData.windVariation = windVariation;
            windData.profile = profile;
            windData.gustInterval = gustInterval;
            windData.shearGradient = shearGradient;

            // Сохраняем как .asset
            string assetPath = $"{dataPath}/{displayName}.asset";
            
            // Если файл уже существует — удаляем (перезаписываем)
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), assetPath)))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            
            AssetDatabase.CreateAsset(windData, assetPath);
            AssetDatabase.SaveAssets();

            // Создаём GameObject
            GameObject zoneGO = new GameObject(displayName);
            zoneGO.transform.position = position;

            // Добавляем BoxCollider
            BoxCollider collider = zoneGO.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;

            // Добавляем WindZone
            WindZone windZone = zoneGO.AddComponent<WindZone>();
            windZone.windData = windData;
        }
    }
}
#endif
