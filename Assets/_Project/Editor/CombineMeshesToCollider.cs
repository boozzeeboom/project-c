using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Собрать все дочерние MeshFilter в один комбинированный меш
    /// и засунуть его в MeshCollider (и опционально в MeshFilter) на выбранном корне.
    ///
    /// Зачем: на импортированных из Blender схемах (типа "Primum / gorod port")
    /// 100+ дочерних объектов, и у ВСЕХ только MeshFilter+MeshRenderer, без коллизий.
    /// Повесить MeshCollider на каждый — дорого и бессмысленно (особенно convex=false).
    /// CharacterController в PlayerController требует ОДНОГО невыпуклого mesh-коллайдера,
    /// и лучше — комбинированного, чем 143 отдельных.
    ///
    /// Использование:
    ///   1) Выделить в Hierarchy целевой корень (например "gorod port").
    ///   2) Меню: Tools/ProjectC/Combine Children Meshes → Collider (и MeshFilter)
    ///   3) Скрипт пройдёт по всем детям рекурсивно, возьмёт их sharedMesh,
    ///      склеит в один меш с учётом мировых трансформов, сохранит ассет
    ///      в Assets/_Project/Generated/ и прицепит к корню.
    ///
    /// Опции:
    ///   - RemoveRenderersOnChildren: true → удаляет MeshFilter+MeshRenderer с детей,
    ///     чтобы не было двойного рендера (корневой сам всё отрисует).
    ///     false → оставляет рендер детей как был (для инкрементальной работы).
    /// </summary>
    public static class CombineMeshesToCollider
    {
        private const string MenuPath = "Tools/ProjectC/Combine Children Meshes → Collider";
        private const string GeneratedFolder = "Assets/_Project/Generated";

        [MenuItem(MenuPath)]
        public static void CombineForSelection()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Combine Meshes",
                    "Выдели корневой GameObject в Hierarchy (например 'gorod port'), затем запусти снова.",
                    "OK");
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Combine Meshes → Collider",
                $"Корень: {root.name}\n" +
                $"Удалить рендер с дочерних объектов после объединения?\n\n" +
                $"YES  — один общий меш на корне, дети только для будущих правок\n" +
                $"NO   — оставить рендер детей как был, добавить комб.меш ТОЛЬКО в коллайдер",
                "Yes (один меш на корне)",
                "No (только коллайдер)",
                "Cancel");

            if (choice == 2) return;
            bool removeChildRenderers = (choice == 0);

            CombineInto(root, removeChildRenderers);
        }

        public static void CombineInto(GameObject root, bool removeChildRenderers)
        {
            if (root == null) return;

            // 1) Собрать все MeshFilter с детей (включая неактивные — но пропустить отключённые mesh)
            var filters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            var combine = new List<CombineInstance>(filters.Length);

            // КРИТИЧНО: считать мировую матрицу ОТНОСИТЕЛЬНО КОРНЯ, чтобы получить
            // меш в ЛОКАЛЬНОМ пространстве корня. Иначе меш улетит в мировые координаты
            // и не совпадёт с визуальным положением.
            Matrix4x4 rootWorldToLocal = root.transform.worldToLocalMatrix;

            int usedCount = 0;
            int skippedNoMesh = 0;
            for (int i = 0; i < filters.Length; i++)
            {
                var f = filters[i];
                if (f == null) continue;
                if (f.sharedMesh == null)
                {
                    skippedNoMesh++;
                    continue;
                }

                combine.Add(new CombineInstance
                {
                    mesh = f.sharedMesh,
                    transform = rootWorldToLocal * f.transform.localToWorldMatrix,
                    subMeshIndex = 0,
                    lightmapScaleOffset = Vector4.one
                });
                usedCount++;
            }

            if (combine.Count == 0)
            {
                Debug.LogError($"[CombineMeshes] У '{root.name}' нет дочерних мешей для объединения. " +
                               $"Найдено MeshFilter'ов: {filters.Length}, с мешем: {usedCount}, без: {skippedNoMesh}.");
                return;
            }

            // 2) Склеить в один меш
            var combined = new Mesh
            {
                name = $"{root.name}_Combined",
                indexFormat = (combine.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32
                                                     : UnityEngine.Rendering.IndexFormat.UInt16
            };
            combined.CombineMeshes(combine.ToArray(), mergeSubMeshes: true, useMatrices: true, hasLightmapData: false);

            // Bounding box нужен MeshCollider'у — иначе PhysX будет думать, что меш в нуле.
            combined.RecalculateBounds();
            combined.RecalculateNormals();

            // 3) Сохранить как ассет (чтобы пережил перезапуск редактора)
            EnsureFolder(GeneratedFolder);
            string assetPath = $"{GeneratedFolder}/{root.name}_Combined.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                // Перезаписать — Mesh это ScriptableObject-like
                existing.Clear();
                existing.vertices = combined.vertices;
                existing.normals = combined.normals;
                existing.uv = combined.uv;
                existing.triangles = combined.triangles;
                existing.RecalculateBounds();
                existing.RecalculateNormals();
                EditorUtility.SetDirty(existing);
                combined = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(combined, assetPath);
            }
            AssetDatabase.SaveAssets();

            // 4) Прицепить к корню
            if (removeChildRenderers)
            {
                // Удаляем рендер с детей, ставим общий MeshFilter+MeshRenderer на корень
                foreach (var f in filters)
                {
                    if (f == null || f.gameObject == root) continue;
                    var r = f.GetComponent<MeshRenderer>();
                    if (r != null) Object.DestroyImmediate(r);
                    Object.DestroyImmediate(f);
                }

                var rootFilter = root.GetComponent<MeshFilter>();
                if (rootFilter == null) rootFilter = root.AddComponent<MeshFilter>();
                rootFilter.sharedMesh = combined;

                if (root.GetComponent<MeshRenderer>() == null)
                {
                    var rr = root.AddComponent<MeshRenderer>();
                    // Наследовать материал от первого попавшегося ребёнка
                    var firstChildRenderer = root.GetComponentInChildren<MeshRenderer>(includeInactive: true);
                    if (firstChildRenderer != null && firstChildRenderer.sharedMaterial != null)
                        rr.sharedMaterial = firstChildRenderer.sharedMaterial;
                }
            }

            var col = root.GetComponent<MeshCollider>();
            if (col == null) col = root.AddComponent<MeshCollider>();
            col.sharedMesh = combined;
            col.convex = false; // CharacterController работает с convex=false; физика (Rigidbody) нет, но для прототипа ОК
            col.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                                 | MeshColliderCookingOptions.EnableMeshCleaning
                                 | MeshColliderCookingOptions.WeldColocatedVertices
                                 | MeshColliderCookingOptions.UseFastMidphase;

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log($"[CombineMeshes] OK: '{root.name}' → 1 меш, {combine.Count} детей склеено. " +
                      $"Ассет: {assetPath}. Коллизия включена (convex=false).");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            // Создать промежуточные папки
            var parts = folder.Split('/');
            string acc = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = acc + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(acc, parts[i]);
                acc = next;
            }
        }
    }
}
