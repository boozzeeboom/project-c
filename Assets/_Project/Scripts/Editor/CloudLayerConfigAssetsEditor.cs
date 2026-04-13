using UnityEngine;
using UnityEditor;
using System.IO;
using ProjectC.Core;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor-скрипт для автоматического создания ассетов облачных слоёв (сессия B3).
    /// Меню: Tools → Project C → Clouds → Create Cloud Layer Config Assets
    /// 
    /// Создаёт:
    /// - 3 CloudLayerConfig ассета (Upper, Middle, Lower) с параметрами в метрах
    /// - 3 материала облаков (Material_Cloud_Upper, Middle, Lower)
    /// </summary>
    public static class CloudLayerConfigAssetsEditor
    {
        private const string DATA_CLOUDS_PATH = "Assets/_Project/Data/Clouds";
        private const string MATERIALS_CLOUDS_PATH = "Assets/_Project/Materials/Clouds";

        #region Главное меню

        [MenuItem("Tools/Project C/Clouds/Create Cloud Layer Config Assets")]
        public static void CreateCloudLayerConfigAssets()
        {
            Debug.Log("=== [CloudLayerConfigAssetsEditor] Начинаю создание ассетов облачных слоёв ===");

            CreateFolders();
            CreateCloudMaterials();
            CreateCloudLayerConfigs();

            Debug.Log("=== [CloudLayerConfigAssetsEditor] ✅ Все ассеты облачных слоёв созданы! ===");
            Debug.Log("Теперь назначьте CloudLayerConfig ассеты на CloudSystem в Inspector");
        }

        #endregion

        #region Создание папок

        private static void CreateFolders()
        {
            CreateFolderIfNotExists(DATA_CLOUDS_PATH);
            CreateFolderIfNotExists(MATERIALS_CLOUDS_PATH);
        }

        private static void CreateFolderIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
                Debug.Log($"[CloudLayerConfigAssetsEditor] Создана папка: {path}");
            }
        }

        #endregion

        #region Создание материалов

        private static void CreateCloudMaterials()
        {
            CreateCloudMaterial("Material_Cloud_Upper", new Color(0.96f, 0.94f, 0.91f, 0.4f), "Upper");
            CreateCloudMaterial("Material_Cloud_Middle", new Color(0.83f, 0.82f, 0.78f, 0.6f), "Middle");
            CreateCloudMaterial("Material_Cloud_Lower", new Color(0.54f, 0.54f, 0.54f, 0.8f), "Lower");
        }

        private static void CreateCloudMaterial(string name, Color baseColor, string layerName)
        {
            string path = $"{MATERIALS_CLOUDS_PATH}/{name}.mat";

            // Проверяем, существует ли уже материал
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                Debug.LogWarning($"[CloudLayerConfigAssetsEditor] Материал уже существует: {path} — пропускаем");
                return;
            }

            // Попробовать найти CloudGhibli шейдер
            Shader ghibliShader = Shader.Find("ProjectC/CloudGhibli");
            Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit");
            
            Shader selectedShader = ghibliShader != null ? ghibliShader : fallbackShader;
            
            if (selectedShader == null)
            {
                Debug.LogError("[CloudLayerConfigAssetsEditor] Не найден ни ProjectC/CloudGhibli, ни URP Unlit шейдер!");
                return;
            }

            string shaderName = selectedShader.name;
            Material mat = new Material(selectedShader);
            mat.color = baseColor;

            // Настроить CloudGhibli специфичные параметры
            if (ghibliShader != null)
            {
                mat.SetColor("_RimColor", new Color(1f, 0.85f, 0.6f, 0.6f));
                mat.SetFloat("_RimPower", 2.0f);
                mat.SetFloat("_Softness", layerName == "Upper" ? 0.3f : layerName == "Middle" ? 0.4f : 0.5f);
                mat.SetFloat("_AlphaBase", baseColor.a);
                mat.SetFloat("_VertexDisplacement", 3.0f);
                mat.SetFloat("_NoiseScale", 1.0f);

                // Noise-текстуры из ProceduralNoiseGenerator
                mat.SetTexture("_NoiseTex", ProceduralNoiseGenerator.GetNoiseTexture1());
                mat.SetTexture("_NoiseTex2", ProceduralNoiseGenerator.GetNoiseTexture2());
            }

            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[CloudLayerConfigAssetsEditor] ✅ Создан материал: {path} (shader: {shaderName})");
        }

        #endregion

        #region Создание CloudLayerConfig ассетов

        private static void CreateCloudLayerConfigs()
        {
            CreateUpperConfig();
            CreateMiddleConfig();
            CreateLowerConfig();
        }

        private static void CreateUpperConfig()
        {
            string path = $"{DATA_CLOUDS_PATH}/CloudLayerConfig_Upper.asset";

            if (AssetDatabase.LoadAssetAtPath<CloudLayerConfig>(path) != null)
            {
                Debug.LogWarning($"[CloudLayerConfigAssetsEditor] Конфиг уже существует: {path} — пропускаем");
                return;
            }

            CloudLayerConfig config = ScriptableObject.CreateInstance<CloudLayerConfig>();
            config.layerType = CloudLayerType.Upper;
            config.minHeight = 7000f;
            config.maxHeight = 9000f;
            config.density = 0.3f;
            config.cloudSize = 150f;
            config.sizeVariation = 2.0f;
            config.moveSpeed = 0.5f;
            config.moveDirection = new Vector3(1f, 0f, 0f);
            config.animateMorph = true;
            config.morphSpeed = 0.3f;
            config.use2DPlanes = true;

            // Назначить материал
            string matPath = $"{MATERIALS_CLOUDS_PATH}/Material_Cloud_Upper.mat";
            config.cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            AssetDatabase.CreateAsset(config, path);
            Debug.Log($"[CloudLayerConfigAssetsEditor] ✅ Создан конфиг: {path} (Y=7000-9000м, density=0.3)");
        }

        private static void CreateMiddleConfig()
        {
            string path = $"{DATA_CLOUDS_PATH}/CloudLayerConfig_Middle.asset";

            if (AssetDatabase.LoadAssetAtPath<CloudLayerConfig>(path) != null)
            {
                Debug.LogWarning($"[CloudLayerConfigAssetsEditor] Конфиг уже существует: {path} — пропускаем");
                return;
            }

            CloudLayerConfig config = ScriptableObject.CreateInstance<CloudLayerConfig>();
            config.layerType = CloudLayerType.Middle;
            config.minHeight = 4000f;
            config.maxHeight = 7000f;
            config.density = 0.6f;
            config.cloudSize = 100f;
            config.sizeVariation = 2.0f;
            config.moveSpeed = 1.0f;
            config.moveDirection = new Vector3(1f, 0f, 0f);
            config.animateMorph = true;
            config.morphSpeed = 0.5f;
            config.use2DPlanes = false;

            // Назначить материал
            string matPath = $"{MATERIALS_CLOUDS_PATH}/Material_Cloud_Middle.mat";
            config.cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            AssetDatabase.CreateAsset(config, path);
            Debug.Log($"[CloudLayerConfigAssetsEditor] ✅ Создан конфиг: {path} (Y=4000-7000м, density=0.6)");
        }

        private static void CreateLowerConfig()
        {
            string path = $"{DATA_CLOUDS_PATH}/CloudLayerConfig_Lower.asset";

            if (AssetDatabase.LoadAssetAtPath<CloudLayerConfig>(path) != null)
            {
                Debug.LogWarning($"[CloudLayerConfigAssetsEditor] Конфиг уже существует: {path} — пропускаем");
                return;
            }

            CloudLayerConfig config = ScriptableObject.CreateInstance<CloudLayerConfig>();
            config.layerType = CloudLayerType.Lower;
            config.minHeight = 1500f;
            config.maxHeight = 4000f;
            config.density = 0.8f;
            config.cloudSize = 80f;
            config.sizeVariation = 1.5f;
            config.moveSpeed = 2.0f;
            config.moveDirection = new Vector3(1f, 0f, 0f);
            config.animateMorph = false;
            config.morphSpeed = 0.5f;
            config.use2DPlanes = false;

            // Назначить материал
            string matPath = $"{MATERIALS_CLOUDS_PATH}/Material_Cloud_Lower.mat";
            config.cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            AssetDatabase.CreateAsset(config, path);
            Debug.Log($"[CloudLayerConfigAssetsEditor] ✅ Создан конфиг: {path} (Y=1500-4000м, density=0.8)");
        }

        #endregion

        #region Удаление ассетов (для очистки)

        [MenuItem("Tools/Project C/Clouds/Delete Cloud Layer Config Assets")]
        public static void DeleteCloudLayerConfigAssets()
        {
            if (!EditorUtility.DisplayDialog("Delete Cloud Assets",
                "Удалить все ассеты облачных слоёв (конфиги + материалы)?\nЭто действие необратимо!",
                "Delete", "Cancel"))
            {
                return;
            }

            DeleteAssetIfExists($"{DATA_CLOUDS_PATH}/CloudLayerConfig_Upper.asset");
            DeleteAssetIfExists($"{DATA_CLOUDS_PATH}/CloudLayerConfig_Middle.asset");
            DeleteAssetIfExists($"{DATA_CLOUDS_PATH}/CloudLayerConfig_Lower.asset");

            DeleteAssetIfExists($"{MATERIALS_CLOUDS_PATH}/Material_Cloud_Upper.mat");
            DeleteAssetIfExists($"{MATERIALS_CLOUDS_PATH}/Material_Cloud_Middle.mat");
            DeleteAssetIfExists($"{MATERIALS_CLOUDS_PATH}/Material_Cloud_Lower.mat");

            AssetDatabase.Refresh();
            Debug.Log("=== [CloudLayerConfigAssetsEditor] Все ассеты облачных слоёв удалены ===");
        }

        private static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[CloudLayerConfigAssetsEditor] Удален: {path}");
            }
        }

        #endregion
    }
}
