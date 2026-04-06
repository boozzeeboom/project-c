using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering.Universal;
#endif

namespace ProjectC.Core
{
    /// <summary>
    /// Автоматически настраивает URP при первом запуске в редакторе.
    /// Удаляется после настройки (или можно вызвать вручную через меню).
    /// </summary>
#if UNITY_EDITOR
    public static class URPSetupEditor
    {
        [MenuItem("ProjectC/Setup URP Pipeline")]
        public static void SetupURP()
        {
            // Находим Pipeline Asset
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/_Project/Settings/URP_PipelineAsset.asset");

            if (pipelineAsset == null)
            {
                Debug.LogError("[URPSetup] Не удалось найти URP_PipelineAsset.asset. " +
                    "Убедитесь что URP пакет установлен.");
                return;
            }

            // Устанавливаем URP как активный рендер-пайплайн
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            // Настройки качества - используем URP asset
            var qualitySettings = QualitySettings.GetQualityLevel();
            Debug.Log($"[URPSetup] URP настроен! Текущий уровень качества: {qualitySettings}");

            // Сохраняем настройки
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();

            Debug.Log("[URPSetup] ✅ URP Pipeline установлен успешно!");
            Debug.Log("[URPSetup] Перезапустите редактор если материалы отображаются некорректно.");
        }

        [MenuItem("ProjectC/Check URP Status")]
        public static void CheckURPStatus()
        {
            var currentPipeline = GraphicsSettings.defaultRenderPipeline;
            if (currentPipeline != null)
            {
                Debug.Log($"[URPSetup] Текущий рендер-пайплайн: {currentPipeline.name}");
            }
            else
            {
                Debug.LogWarning("[URPSetup] Рендер-пайплайн НЕ настроен! " +
                    "Запустите ProjectC/Setup URP Pipeline");
            }
        }
    }
#endif
}
