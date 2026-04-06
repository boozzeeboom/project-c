#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace ProjectC.Editor
{
    /// <summary>
    /// Массовая конвертация материалов в URP.
    /// Меню: ProjectC → Upgrade Materials to URP
    /// 
    /// Конвертирует:
    /// - Все материалы в проекте со Standard шейдером
    /// - Материалы на объектах сцены
    /// - Материалы в префабах
    /// </summary>
    public static class MaterialURPUpgrader
    {
        [MenuItem("ProjectC/Upgrade Materials to URP")]
        public static void UpgradeAllMaterials()
        {
            int converted = 0;
            int skipped = 0;
            int errors = 0;

            // 1. Конвертируем все материалы в проекте
            string[] matGuids = AssetDatabase.FindAssets("t:Material");
            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Пропускаем материалы в папках пакетов
                if (path.Contains("Packages/") || path.Contains("Library/"))
                    continue;

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (TryConvertMaterial(mat))
                {
                    converted++;
                    Debug.Log($"[URP Upgrade] Конвертирован: {path}");
                }
                else
                {
                    skipped++;
                }
            }

            // 2. Конвертируем материалы на объектах сцены
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials == null) continue;

                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    var mat = renderer.sharedMaterials[i];
                    if (mat != null && TryConvertMaterial(mat))
                    {
                        converted++;
                        Debug.Log($"[URP Upgrade] Конвертирован на объекте {renderer.gameObject.name}: {mat.name}");
                    }
                }
            }

            // 3. Конвертируем материалы в префабах
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Packages/") || path.Contains("Library/"))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var prefabRenderers = prefab.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in prefabRenderers)
                {
                    if (renderer.sharedMaterials == null) continue;

                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        var mat = renderer.sharedMaterials[i];
                        if (mat != null && TryConvertMaterial(mat))
                        {
                            converted++;
                            EditorUtility.SetDirty(prefab);
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            
            Debug.Log($"[URP Upgrade] ✅ Готово! Конвертировано: {converted}, Пропущено: {skipped}, Ошибок: {errors}");
            Debug.Log("[URP Upgrade] Если материалы всё ещё розовые — проверьте что Shader правильно назначен в Inspector.");
        }

        [MenuItem("ProjectC/Check Materials Status")]
        public static void CheckMaterialsStatus()
        {
            string[] matGuids = AssetDatabase.FindAssets("t:Material");
            int standardCount = 0;
            int urpCount = 0;
            int otherCount = 0;

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Packages/") || path.Contains("Library/"))
                    continue;

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string shaderName = mat.shader.name;
                if (shaderName.Contains("Universal Render Pipeline"))
                    urpCount++;
                else if (shaderName == "Standard" || shaderName.Contains("Legacy Shaders"))
                    standardCount++;
                else
                    otherCount++;
            }

            Debug.Log($"[URP Check] URP материалы: {urpCount}");
            Debug.Log($"[URP Check] Standard материалы: {standardCount}");
            Debug.Log($"[URP Check] Другие материалы: {otherCount}");

            if (standardCount > 0)
            {
                Debug.LogWarning($"[URP Check] ⚠️ Найдено {standardCount} материалов со Standard шейдером! Запустите Upgrade Materials to URP.");
            }
            else
            {
                Debug.Log("[URP Check] ✅ Все материалы конвертированы в URP!");
            }
        }

        private static bool TryConvertMaterial(Material mat)
        {
            if (mat == null) return false;

            string shaderName = mat.shader.name;

            // Пропускаем уже URP материалы
            if (shaderName.Contains("Universal Render Pipeline"))
                return false;

            // Пропускаем материалы с кастомными шейдерами
            if (shaderName.Contains("ProjectC/"))
                return false;

            // Конвертируем Standard и Legacy шейдеры
            if (shaderName == "Standard" || shaderName.Contains("Legacy Shaders"))
            {
                // Определяем тип материала
                bool isTransparent = mat.HasProperty("_Mode") && mat.GetInt("_Mode") == 3;
                bool isParticle = mat.name.ToLower().Contains("particle") || mat.name.ToLower().Contains("cloud");

                if (isParticle || isTransparent)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                else
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                if (mat.shader == null)
                {
                    Debug.LogWarning($"[URP Upgrade] Не удалось найти URP шейдер для {mat.name}");
                    return false;
                }

                // Копируем основные свойства
                if (mat.HasProperty("_BaseColor") && mat.HasProperty("_Color"))
                {
                    mat.SetColor("_BaseColor", mat.GetColor("_Color"));
                }
                if (mat.HasProperty("_Smoothness") && mat.HasProperty("_Glossiness"))
                {
                    mat.SetFloat("_Smoothness", mat.GetFloat("_Glossiness"));
                }
                if (mat.HasProperty("_Metallic") && mat.HasProperty("_Metallic"))
                {
                    // Metallic остаётся как есть
                }
                if (mat.HasProperty("_MainTex") && mat.HasProperty("_BaseMap"))
                {
                    var tex = mat.GetTexture("_MainTex");
                    if (tex != null)
                    {
                        mat.SetTexture("_BaseMap", tex);
                    }
                }

                EditorUtility.SetDirty(mat);
                return true;
            }

            return false;
        }
    }
}
#endif
