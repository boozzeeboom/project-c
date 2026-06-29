// Project C: Equipment Visual System — Phase 1 (2026-06-29)
// SetupEquipmentVisualAssets: создаёт 3 stand-in визуальных префаба (cone/capsule) для
// тестирования Equipment Visual System. Дизайнер потом заменит на нормальные меши.
//
// Где:
//   Tools → ProjectC → Equipment → Create Stand-in Visual Prefabs
//
// Что делает:
//   1. Создаёт папку Assets/_Project/Resources/Visuals/Equipment/ если её нет.
//   2. Создаёт 3 префаба:
//      - Visual_Helmet_Cone.prefab  (конус — для шлемов)
//      - Visual_Blade_Capsule.prefab (длинная капсула — для оружия)
//      - Visual_Boots_SmallCapsule.prefab (маленькие капсулы — для ботинок)
//   3. Создаёт default URP material в той же папке (если нет).
//   4. Подключает созданные префабы к 3 существующим ClothingItemData / WeaponItemData .asset:
//      - Clothing_WorkerHelmet.asset       → Visual_Helmet_Cone.prefab
//      - Weapon_AntigravBlade.asset        → Visual_Blade_Capsule.prefab
//      - (boots asset — designer назначит вручную, см. InventoryItemsTest scene)
//
// Идемпотентен: повторный запуск пересоздаёт префабы и перепривязывает.
// НЕ трогает другие .asset (только 2 указанных).
//
// Design: docs/Character/EquipmentVisual/03_PHASES.md §Phase 1.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Editor
{
    public static class SetupEquipmentVisualAssets
    {
        private const string OutputDir = "Assets/_Project/Resources/Visuals/Equipment";
        private const string MaterialPath = OutputDir + "/Mat_DefaultEquipment.mat";
        private const string HelmetPrefabPath = OutputDir + "/Visual_Helmet_Cone.prefab";
        private const string BladePrefabPath = OutputDir + "/Visual_Blade_Capsule.prefab";
        private const string BootsPrefabPath = OutputDir + "/Visual_Boots_SmallCapsule.prefab";

        private const string WorkerHelmetAssetPath = "Assets/_Project/Resources/Items/Clothing/Clothing_WorkerHelmet.asset";
        private const string AntigravBladeAssetPath = "Assets/_Project/Resources/Items/Weapons/Weapon_AntigravBlade.asset";

        [MenuItem("Tools/ProjectC/Equipment/Create Stand-in Visual Prefabs")]
        public static void CreateStandInVisuals()
        {
            // 1. Ensure folder exists.
            EnsureFolder(OutputDir);

            // 2. Create shared material (URP Lit, color = light gray).
            Material sharedMat = EnsureMaterial(MaterialPath, new Color(0.7f, 0.7f, 0.7f, 1f));

            // 3. Create 3 stand-in prefabs.
            CreateHelmetConePrefab(HelmetPrefabPath, sharedMat);
            CreateBladeCapsulePrefab(BladePrefabPath, sharedMat);
            CreateBootsCapsulePrefab(BootsPrefabPath, sharedMat);

            // 4. Wire up 2 existing assets to their visualPrefab.
            int wired = 0;
            wired += WireItemVisualPrefab(WorkerHelmetAssetPath, HelmetPrefabPath);
            wired += WireItemVisualPrefab(AntigravBladeAssetPath, BladePrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SetupEquipmentVisualAssets] DONE. 3 prefabs created in '{OutputDir}'. {wired} item assets wired to visualPrefab. " +
                      $"Designer: check .asset files in Inspector → 'Visual (Equipment Visual System — Phase 1)' → visualPrefab field.");
        }

        // === Folder ===

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            // Создать по уровням (Unity не умеет рекурсивно).
            string[] parts = assetPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        // === Material ===

        private static Material EnsureMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            // Use URP Lit shader (project uses URP 17.0.3 per AGENTS.md).
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[SetupEquipmentVisualAssets] URP Lit shader not found, falling back to Standard.");
                shader = Shader.Find("Standard");
            }

            var mat = new Material(shader);
            mat.color = color;
            // URP Lit uses _BaseColor; Standard uses _Color. URP Lit shader has both, but _BaseColor is canonical.
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // === Prefab builders ===

        private static void CreateHelmetConePrefab(string path, Material mat)
        {
            // UnityEngine.PrimitiveType не имеет "Cone" — используем Cylinder
            // (цилиндр как stand-in для шлема; designer потом заменит на нормальный меш).
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            try
            {
                go.name = "Visual_Helmet_Cone";
                // Cylinder default scale (1,1,1) = ~2m tall, ~1m diameter. Scale down for helmet.
                // Шлем узкий + низкий → плоский диск сверху.
                go.transform.localScale = new Vector3(0.35f, 0.15f, 0.35f);
                AssignMaterial(go, mat);
                // Remove default collider (visual only).
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[SetupEquipmentVisualAssets] Created helmet prefab: {path}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void CreateBladeCapsulePrefab(string path, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                go.name = "Visual_Blade_Capsule";
                // Capsule: thin + long (как клинок).
                go.transform.localScale = new Vector3(0.05f, 0.6f, 0.05f);
                AssignMaterial(go, mat);
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[SetupEquipmentVisualAssets] Created blade prefab: {path}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void CreateBootsCapsulePrefab(string path, Material mat)
        {
            // Boots = parent with 2 child capsules (left + right boot).
            var root = new GameObject("Visual_Boots_SmallCapsule");
            try
            {
                // Two child capsules.
                for (int i = 0; i < 2; i++)
                {
                    var boot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    boot.name = (i == 0) ? "Boot_Left" : "Boot_Right";
                    boot.transform.SetParent(root.transform);
                    boot.transform.localScale = new Vector3(0.15f, 0.08f, 0.25f);
                    // Position below root, slightly offset to sides (для обеих ног).
                    boot.transform.localPosition = (i == 0)
                        ? new Vector3(-0.08f, -0.4f, 0f)
                        : new Vector3(0.08f, -0.4f, 0f);
                    AssignMaterial(boot, mat);
                    var col = boot.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[SetupEquipmentVisualAssets] Created boots prefab: {path}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssignMaterial(GameObject go, Material mat)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            // sharedMaterials — без instance leak, по аналогии с NpcVisualApplier.
            renderer.sharedMaterials = new[] { mat };
        }

        // === Wire up existing assets ===

        private static int WireItemVisualPrefab(string assetPath, string prefabPath)
        {
            var item = AssetDatabase.LoadAssetAtPath<ProjectC.Items.ItemData>(assetPath);
            if (item == null)
            {
                Debug.LogWarning($"[SetupEquipmentVisualAssets] ItemData asset not found: {assetPath}. Skipped.");
                return 0;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[SetupEquipmentVisualAssets] Prefab not found: {prefabPath}. Skipped.");
                return 0;
            }
            if (item.visualPrefab == prefab)
            {
                Debug.Log($"[SetupEquipmentVisualAssets] {item.itemName}: visualPrefab already wired to '{prefab.name}'. Skipped.");
                return 0;
            }
            item.visualPrefab = prefab;
            EditorUtility.SetDirty(item);
            Debug.Log($"[SetupEquipmentVisualAssets] {item.itemName}: wired visualPrefab → '{prefab.name}'.");
            return 1;
        }

        // ============================================================
        // Phase 2 (2026-06-29): Add CharacterEquipmentVisualApplier
        // to NetworkPlayer.prefab. Идемпотентен.
        // ============================================================

        private const string NetworkPlayerPrefabPath = "Assets/_Project/Prefabs/NetworkPlayer.prefab";

        [MenuItem("Tools/ProjectC/Equipment/Add VisualApplier to NetworkPlayer")]
        public static void AddVisualApplierToNetworkPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SetupEquipmentVisualAssets] Prefab not found: {NetworkPlayerPrefabPath}");
                return;
            }

            // LoadPrefabContents — единственный безопасный способ редактировать prefab через script.
            var contents = PrefabUtility.LoadPrefabContents(NetworkPlayerPrefabPath);
            try
            {
                if (contents.GetComponent<ProjectC.Player.CharacterEquipmentVisualApplier>() != null)
                {
                    Debug.Log($"[SetupEquipmentVisualAssets] CharacterEquipmentVisualApplier already present on '{contents.name}'. Skipped.");
                    return;
                }

                var component = contents.AddComponent<ProjectC.Player.CharacterEquipmentVisualApplier>();

                // Авто-wire animator: ищем Visual_Model/Animator в детях.
                var animators = contents.GetComponentsInChildren<Animator>(true);
                foreach (var a in animators)
                {
                    if (a != null && a.runtimeAnimatorController != null)
                    {
                        // SerializedObject — чтобы попасть в private SerializeField через Unity API.
                        var so = new SerializedObject(component);
                        var prop = so.FindProperty("_animator");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = a;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                        break;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(contents, NetworkPlayerPrefabPath);
                Debug.Log($"[SetupEquipmentVisualAssets] Added CharacterEquipmentVisualApplier to '{contents.name}'. Animator auto-wired if found.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}