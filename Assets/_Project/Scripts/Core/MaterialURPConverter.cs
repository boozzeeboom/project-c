using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Автоматически заменяет Standard-материалы на URP при запуске.
    /// Вешается на любой объект в сцене (или вызывается из WorldGenerator).
    /// </summary>
    public class MaterialURPConverter : MonoBehaviour
    {
        [Header("URP материалы (назначить в Inspector)")]
        [Tooltip("URP материал для пиков/скал")]
        [SerializeField] private Material urpPeakMaterial;

        [Tooltip("URP материал для облаков")]
        [SerializeField] private Material urpCloudMaterial;

        [Tooltip("URP материал для персонажа")]
        [SerializeField] private Material urpCharacterMaterial;

        [Header("Настройки")]
        [Tooltip("Конвертировать при старте")]
        [SerializeField] private bool convertOnStart = true;

        private void Start()
        {
            if (convertOnStart)
            {
                ConvertMaterials();
            }
        }

        /// <summary>
        /// Заменить все Standard-материалы на URP в сцене
        /// </summary>
        public void ConvertMaterials()
        {
            ConvertAllRenderers();
            Debug.Log("[MaterialURPConverter] Конвертация материалов завершена.");
        }

        private void ConvertAllRenderers()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);

            foreach (var renderer in renderers)
            {
                // Пропускаем UI-элементы
                if (renderer.GetType().Name.Contains("UI")) continue;

                var materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;

                    string shaderName = materials[i].shader.name;

                    // Заменяем Standard на URP Lit
                    if (shaderName == "Standard")
                    {
                        if (urpPeakMaterial != null)
                        {
                            materials[i] = urpPeakMaterial;
                            changed = true;
                            Debug.Log($"[MaterialURPConverter] Заменён Standard на URP/Lit: {renderer.gameObject.name}");
                        }
                    }
                    // Заменяем старые облачные материалы
                    else if (materials[i].name.Contains("CloudMaterial") && urpCloudMaterial != null)
                    {
                        materials[i] = urpCloudMaterial;
                        changed = true;
                        Debug.Log($"[MaterialURPConverter] Заменён CloudMaterial на URP: {renderer.gameObject.name}");
                    }
                    // Заменяем character.mat
                    else if (materials[i].name == "character" && urpCharacterMaterial != null)
                    {
                        materials[i] = urpCharacterMaterial;
                        changed = true;
                        Debug.Log($"[MaterialURPConverter] Заменён character на URP: {renderer.gameObject.name}");
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        [ContextMenu("Convert Now")]
        public void ConvertNow()
        {
            ConvertMaterials();
        }
    }
}
