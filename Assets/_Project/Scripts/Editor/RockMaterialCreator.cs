using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor-скрипт для создания URP материалов скал и снега.
    /// Материалы НЕЛЬЗЯ создавать через runtime C# — только через Editor.
    /// 
    /// Использование: Tools → Project C → Create Rock Materials
    /// </summary>
    public class RockMaterialCreator : EditorWindow
    {
        private const string MaterialsPath = "Assets/_Project/Materials/World";

        [MenuItem("Tools/Project C/Create Rock Materials")]
        public static void ShowWindow()
        {
            // Сразу создать материалы
            CreateAllRockMaterials();
        }

        public static void CreateAllRockMaterials()
        {
            // Убедиться что папка существует
            EnsureFolderExists("Assets/_Project");
            EnsureFolderExists("Assets/_Project/Materials");
            EnsureFolderExists("Assets/_Project/Materials/World");

            int created = 0;

            // Himalayan Rock — тёмно-серый гранит
            if (CreateRockMaterial("Rock_Himalayan", new Color(0.35f, 0.35f, 0.35f), "Гималайский гранит"))
                created++;

            // Alpine Rock — светло-серый известняк
            if (CreateRockMaterial("Rock_Alpine", new Color(0.54f, 0.54f, 0.48f), "Альпийский известняк"))
                created++;

            // African Rock — красноватый вулканический
            if (CreateRockMaterial("Rock_African", new Color(0.48f, 0.35f, 0.29f), "Африканский вулканический"))
                created++;

            // Andean Rock — коричнево-серый
            if (CreateRockMaterial("Rock_Andean", new Color(0.42f, 0.35f, 0.29f), "Андийский сухой"))
                created++;

            // Alaskan Rock — тёмный базальт
            if (CreateRockMaterial("Rock_Alaskan", new Color(0.29f, 0.29f, 0.29f), "Аляскинский базальт"))
                created++;

            // Snow Generic — голубоватый снег
            if (CreateSnowMaterial("Snow_Generic", new Color(0.94f, 0.94f, 0.96f), "Обычный снег"))
                created++;

            EditorUtility.DisplayDialog("Готово!",
                $"Создано {created} материалов в {MaterialsPath}",
                "OK");
        }

        private static bool CreateRockMaterial(string name, Color baseColor, string description)
        {
            string fullPath = $"{MaterialsPath}/{name}.mat";

            // Проверить существует ли уже
            var existing = AssetDatabase.LoadAssetAtPath<Material>(fullPath);
            if (existing != null)
            {
                Debug.Log($"[RockMaterialCreator] {name} уже существует, обновляю.");
                UpdateMaterial(existing, baseColor);
                return false;
            }

            // Создать материал с URP Lit шейдером
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = name,
                color = baseColor
            };

            // Настроить URP Lit параметры
            material.SetFloat("_Metallic", 0.1f);
            material.SetFloat("_Smoothness", 0.3f);
            material.SetFloat("_OcclusionStrength", 1f);

            // Сохранить ассет
            AssetDatabase.CreateAsset(material, fullPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RockMaterialCreator] Created {name}: {baseColor} — {description}");
            return true;
        }

        private static bool CreateSnowMaterial(string name, Color baseColor, string description)
        {
            string fullPath = $"{MaterialsPath}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(fullPath);
            if (existing != null)
            {
                Debug.Log($"[RockMaterialCreator] {name} уже существует, обновляю.");
                UpdateMaterial(existing, baseColor);
                return false;
            }

            // Снег — Unlit для яркости
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                name = name,
                color = baseColor
            };

            AssetDatabase.CreateAsset(material, fullPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RockMaterialCreator] Created {name}: {baseColor} — {description}");
            return true;
        }

        private static void UpdateMaterial(Material material, Color baseColor)
        {
            material.color = baseColor;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolderExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path);
                string folder = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
