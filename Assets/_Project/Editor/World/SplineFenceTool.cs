using System.Text;
using ProjectC.World.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Editor-инструмент генеративного забора: спайн → меш-секции + бокс-коллайдеры.
    ///
    /// Правила, на которых всё держится:
    ///   * геометрия строится в ЛОКАЛЬНЫХ координатах корня забора (floating origin);
    ///   * меши сохраняются ассетами в Assets/_Project/Generated/Fences/&lt;id&gt;/;
    ///   * пересборка идемпотентна: своё сносится по маркеру FenceSection, чужое не трогается;
    ///   * масштаб корня обязан быть 1 — оба процедурных графа работают в object space.
    /// </summary>
    public static class SplineFenceTool
    {
        private const string GeneratedRoot = "Assets/_Project/Generated/Fences";
        private const string ColliderSuffix = "_COLLIDER";
        private const string TypeFolder = "Assets/_Project/Data/World/Fences";

        // ============================================================
        // Меню
        // ============================================================

        [MenuItem("Tools/ProjectC/World/Spline Fence/1. Создать забор из выделенного сплайна", false, 881)]
        public static void CreateForSelection()
        {
            SplineContainer container = FindContainerInSelection();
            if (container == null)
            {
                Debug.LogError("[Fence] Выделите объект со SplineContainer (узлы рисуются инструментом Splines).");
                return;
            }

            var fence = container.GetComponent<SplineFence>();
            if (fence == null)
            {
                fence = Undo.AddComponent<SplineFence>(container.gameObject);
                Debug.Log($"[Fence] Добавлен SplineFence на «{container.name}».");
            }

            if (fence.type == null)
                fence.type = FindDefaultType();

            if (fence.type == null)
            {
                Debug.LogError($"[Fence] Нет ни одного FenceTypeDefinition в проекте. " +
                               $"Создайте ассет в {TypeFolder} (Create → ProjectC → World → Fence Type).");
                return;
            }

            Debug.Log(Rebuild(fence));
            Selection.activeGameObject = container.gameObject;
        }

        [MenuItem("Tools/ProjectC/World/Spline Fence/2. Пересобрать выделенные", false, 882)]
        public static void RebuildSelected()
        {
            var report = new StringBuilder();
            int count = 0;

            foreach (GameObject go in Selection.gameObjects)
            {
                var fence = go.GetComponent<SplineFence>();
                if (fence == null) fence = go.GetComponentInParent<SplineFence>();
                if (fence == null) continue;

                report.AppendLine(Rebuild(fence));
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[Fence] В выделении нет объектов со SplineFence.");
                return;
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/ProjectC/World/Spline Fence/3. Сухой прогон (без записи в сцену)", false, 883)]
        public static void DryRunSelected()
        {
            var report = new StringBuilder();
            int count = 0;

            foreach (GameObject go in Selection.gameObjects)
            {
                var fence = go.GetComponent<SplineFence>();
                if (fence == null) fence = go.GetComponentInParent<SplineFence>();
                if (fence == null) continue;

                report.AppendLine(Rebuild(fence, true));
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[Fence] В выделении нет объектов со SplineFence.");
                return;
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/ProjectC/World/Spline Fence/5. Снести сгенерированное", false, 885)]
        public static void RemoveSelected()
        {
            int count = 0;

            foreach (GameObject go in Selection.gameObjects)
            {
                var fence = go.GetComponent<SplineFence>();
                if (fence == null) fence = go.GetComponentInParent<SplineFence>();
                if (fence == null) continue;

                RemoveGenerated(fence);
                DeleteGeneratedFolder(GeneratedFolder(fence));
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[Fence] В выделении нет объектов со SplineFence.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Fence] Сгенерированное снесено у {count} забор(ов).");
        }

        // ============================================================
        // Пересборка
        // ============================================================

        /// <summary>
        /// Строит забор. dryRun = true — только путь и отчёт, без объектов, ассетов и Undo.
        /// </summary>
        public static string Rebuild(SplineFence fence, bool dryRun = false)
        {
            var sb = new StringBuilder();

            if (fence == null) return "[Fence] Нет SplineFence.";

            if (fence.type == null)
                return $"[Fence] {fence.name}: не задан FenceTypeDefinition — пересборка невозможна.";

            if (fence.Container == null)
                return $"[Fence] {fence.name}: нет SplineContainer.";

            Vector3 scale = fence.transform.lossyScale;
            bool badScale = Mathf.Abs(scale.x - 1f) > 1e-3f ||
                            Mathf.Abs(scale.y - 1f) > 1e-3f ||
                            Mathf.Abs(scale.z - 1f) > 1e-3f;

            if (badScale)
                sb.AppendLine($"[Fence] ⚠ {fence.name}: масштаб корня {scale.x:F3}/{scale.y:F3}/{scale.z:F3} ≠ 1 — " +
                              "процедурные графы (object space) поплывут, коллайдеры разъедутся с мешем.");

            FencePath path = fence.BuildPath();
            FenceBuildOutput output = FenceStyleBuilder.Build(path, fence.type);

            sb.AppendLine($"[Fence] {fence.name} → {fence.type.name}: прогонов {path.runs.Count}, " +
                          $"источник {path.TotalRunLength:F2} м, секций {output.sections.Count}, " +
                          $"квадов {output.TotalQuads}, треугольников {output.TotalTriangles}, " +
                          $"бокс-коллайдеров {output.colliders.Count}");

            for (int i = 0; i < output.warnings.Count; i++)
                sb.AppendLine($"[Fence] ⚠ {output.warnings[i]}");

            if (dryRun)
            {
                output.ReleaseMeshes();
                return sb.ToString();
            }

            if (output.sections.Count == 0)
            {
                output.ReleaseMeshes();
                sb.AppendLine("[Fence] Секций нет — сцена не изменена.");
                return sb.ToString();
            }

            string id = SanitizeName(fence.EffectiveId);
            string folder = $"{GeneratedRoot}/{id}";

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Пересборка забора «{fence.name}»");

            RemoveGenerated(fence);
            DeleteGeneratedFolder(folder);

            EnsureFolder(folder);

            for (int i = 0; i < output.sections.Count; i++)
            {
                FenceSectionMesh section = output.sections[i];
                Mesh mesh = section.mesh;
                if (mesh == null) continue;

                string baseName = SanitizeName(section.name);
                string assetPath = $"{folder}/{baseName}.asset";

                mesh.name = baseName;
                AssetDatabase.CreateAsset(mesh, assetPath);

                var go = new GameObject(baseName);
                Undo.RegisterCreatedObjectUndo(go, "Fence section");

                go.transform.SetParent(fence.transform, false);
                go.transform.localPosition = section.localPosition;
                go.transform.localRotation = section.localRotation;
                go.transform.localScale = Vector3.one;
                go.layer = fence.gameObject.layer;

                go.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = section.materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                go.AddComponent<FenceSection>().owner = fence;
            }

            var colliderRoot = new GameObject(id + ColliderSuffix);
            Undo.RegisterCreatedObjectUndo(colliderRoot, "Fence colliders");

            colliderRoot.transform.SetParent(fence.transform, false);
            colliderRoot.transform.localPosition = Vector3.zero;
            colliderRoot.transform.localRotation = Quaternion.identity;
            colliderRoot.transform.localScale = Vector3.one;
            colliderRoot.layer = fence.colliderLayer;
            colliderRoot.AddComponent<FenceSection>().owner = fence;

            for (int i = 0; i < output.colliders.Count; i++)
                CreateCollider(output.colliders[i], colliderRoot.transform, fence.colliderLayer);

            Undo.CollapseUndoOperations(undoGroup);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(fence.gameObject.scene);

            sb.AppendLine($"[Fence] Пересобрано: {folder}");
            return sb.ToString();
        }

        // ============================================================
        // Коллайдеры
        // ============================================================

        private static void CreateCollider(FenceBoxSpec spec, Transform parent, int layer)
        {
            var go = new GameObject(spec.name);
            Undo.RegisterCreatedObjectUndo(go, "Fence collider");

            go.transform.SetParent(parent, false);
            go.transform.localPosition = spec.center;
            go.transform.localScale = Vector3.one;
            go.layer = layer;

            // Тот же базис, что у FenceMeshBuilder.AddBox: az = cross(ax, ay), затем ay ⟂ az.
            Vector3 ax = spec.axisX.sqrMagnitude > 1e-8f ? spec.axisX.normalized : Vector3.right;
            Vector3 az = Vector3.Cross(ax, spec.axisY);
            if (az.sqrMagnitude < 1e-8f) az = Vector3.forward;
            az.Normalize();

            Vector3 ay = Vector3.Cross(az, ax);
            if (ay.sqrMagnitude < 1e-8f) ay = Vector3.up;
            ay.Normalize();

            // LookRotation(forward = az, up = ay) даёт local X = cross(ay, az) = ax.
            go.transform.localRotation = Quaternion.LookRotation(az, ay);

            var box = go.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(Mathf.Abs(spec.size.x), Mathf.Abs(spec.size.y), Mathf.Abs(spec.size.z));
        }

        // ============================================================
        // Снос и папки
        // ============================================================

        private static void RemoveGenerated(SplineFence fence)
        {
            Transform root = fence.transform;
            string colliderName = SanitizeName(fence.EffectiveId) + ColliderSuffix;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);

                var marker = child.GetComponent<FenceSection>();
                if (marker == null && child.name != colliderName) continue;

                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static string GeneratedFolder(SplineFence fence)
        {
            return $"{GeneratedRoot}/{SanitizeName(fence.EffectiveId)}";
        }

        private static void DeleteGeneratedFolder(string folder)
        {
            // AssetDatabase.DeleteAsset удаляет папку рекурсивно — осиротевших мешей не остаётся
            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // ============================================================
        // Выбор
        // ============================================================

        private static SplineContainer FindContainerInSelection()
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                var container = go.GetComponent<SplineContainer>();
                if (container != null) return container;

                container = go.GetComponentInParent<SplineContainer>();
                if (container != null) return container;
            }

            return null;
        }

        private static FenceTypeDefinition FindDefaultType()
        {
            var direct = AssetDatabase.LoadAssetAtPath<FenceTypeDefinition>($"{TypeFolder}/FT_Fence_Bar_Steel.asset");
            if (direct != null) return direct;

            string[] guids = AssetDatabase.FindAssets("t:FenceTypeDefinition");
            if (guids.Length == 0) return null;

            return AssetDatabase.LoadAssetAtPath<FenceTypeDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Fence";

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }

            return sb.ToString();
        }
    }
}
