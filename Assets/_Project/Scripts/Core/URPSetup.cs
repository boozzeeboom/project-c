using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectC.Core
{
    /// <summary>
    /// Автоматически настраивает URP при первом запуске в редакторе.
    /// Меню: ProjectC → Setup URP Pipeline
    /// </summary>
#if UNITY_EDITOR
    public static class URPSetupEditor
    {
        [MenuItem("ProjectC/Setup URP Pipeline")]
        public static void SetupURP()
        {
            // Находим Pipeline Asset
            string assetPath = "Assets/_Project/Settings/URP_PipelineAsset.asset";
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(assetPath);

            if (pipelineAsset == null)
            {
                Debug.LogError($"[URPSetup] Не удалось найти {assetPath}. " +
                    "Убедитесь что URP пакет установлен (Packages/manifest.json).");
                return;
            }

            // Устанавливаем URP как активный рендер-пайплайн
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            // Сохраняем настройки
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();

            Debug.Log("[URPSetup] ✅ URP Pipeline установлен успешно!");
            Debug.Log("[URPSetup] Перезапустите Play Mode если материалы отображаются некорректно.");
        }

        [MenuItem("ProjectC/Check URP Status")]
        public static void CheckURPStatus()
        {
            var currentPipeline = GraphicsSettings.defaultRenderPipeline;
            if (currentPipeline != null)
            {
                Debug.Log($"[URPSetup] ✅ Текущий рендер-пайплайн: {currentPipeline.name}");
            }
            else
            {
                Debug.LogWarning("[URPSetup] ❌ Рендер-пайплайн НЕ настроен! " +
                    "Запустите: ProjectC → Setup URP Pipeline");
            }
        }
    }
#endif
}
