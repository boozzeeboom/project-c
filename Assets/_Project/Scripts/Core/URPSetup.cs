using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering.Universal;
#endif

namespace ProjectC.Core
{
    /// <summary>
    /// Помощник для настройки URP через встроенный конвертер Unity.
    /// Меню: ProjectC → Convert to URP (uses Unity's built-in wizard)
    /// </summary>
#if UNITY_EDITOR
    public static class URPSetupHelper
    {
        [MenuItem("ProjectC/Convert to URP (Recommended)")]
        public static void ConvertToURP()
        {
            // Используем встроенный конвертер Unity — он сам создаст все ассеты
            Debug.Log("[URPSetup] Запускаю встроенный конвертер Unity...");
            Debug.Log("[URPSetup] Откроется окно 'Universal RP Conversion' — нажмите 'Yes' или 'Convert'");
            
            // Вызываем встроенный конвертер
            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            if (pipeline != null)
            {
                Debug.Log($"[URPSetup] URP уже настроен: {pipeline.name}");
                return;
            }

            // Открываем Package Manager для установки URP через Wizard
            EditorApplication.ExecuteMenuItem("Window/Package Manager");
            
            Debug.Log("[URPSetup] Инструкция:");
            Debug.Log("1. В Package Manager найдите 'Universal RP'");
            Debug.Log("2. Нажмите Install");
            Debug.Log("3. После установки появится окно 'Convert to URP'");
            Debug.Log("4. Нажмите 'Yes, convert' — Unity сам создаст Pipeline Asset и Renderer");
            Debug.Log("5. Перезапустите Play Mode");
        }

        [MenuItem("ProjectC/Check URP Status")]
        public static void CheckURPStatus()
        {
            var currentPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            if (currentPipeline != null)
            {
                Debug.Log($"[URPSetup] ✅ URP активен: {currentPipeline.name}");
                Debug.Log($"[URPSetup] Pipeline Asset: {AssetDatabase.GetAssetPath(currentPipeline)}");
            }
            else
            {
                Debug.LogWarning("[URPSetup] ❌ URP НЕ активен!");
                Debug.Log("[URPSetup] Запустите: ProjectC → Convert to URP");
            }
        }

        [MenuItem("ProjectC/Apply URP Pipeline Asset Manually")]
        public static void ApplyURPManually()
        {
            // Ищем любой UniversalRenderPipelineAsset в проекте
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            
            if (guids.Length == 0)
            {
                Debug.LogError("[URPSetup] URP Pipeline Asset не найден! " +
                    "Сначала установите URP через ProjectC → Convert to URP");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            
            if (pipeline != null)
            {
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline;
                UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset = pipeline;
                EditorUtility.SetDirty(pipeline);
                AssetDatabase.SaveAssets();
                
                Debug.Log($"[URPSetup] ✅ Применён Pipeline Asset: {path}");
            }
        }
    }
#endif
}
