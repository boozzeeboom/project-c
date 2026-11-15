using System.Collections.Generic;
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

                Remove(fence);
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[Fence] В выделении нет объектов со SplineFence.");
                return;
            }

            Debug.Log($"[Fence] Сгенерированное снесено у {count} забор(ов).");
        }

        /// <summary>Сносит сгенерированное одного забора: объекты из сцены + папку мешей. Без диалогов.</summary>
        public static void Remove(SplineFence fence)
        {
            if (fence == null) return;

            RemoveGenerated(fence);
            DeleteGeneratedFolder(GeneratedFolder(fence));

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(fence.gameObject.scene);

            Debug.Log($"[Fence] «{fence.name}»: сгенерированное снесено.");
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

            FenceTypeDefinition type = fence.ResolveType(out bool transient);
            FencePath path;
            FenceBuildOutput output;

            try
            {
                path = fence.BuildPath(type);
                output = FenceStyleBuilder.Build(path, type);
            }
            finally
            {
                // Временная копия типа живёт только на время сборки (FenceTypeDefinition.ResolveFor).
                if (transient) Object.DestroyImmediate(type);
            }

            string hNote = fence.heightOverride > 0.0005f ? " (переопределено)" : "";
            string sNote = fence.sectionStepOverride > 0.0005f ? " (переопределено)" : "";

            sb.AppendLine($"[Fence] {fence.name} → {fence.type.name}: высота {fence.EffectiveHeight:F2} м{hNote}, " +
                          $"шаг секции {fence.EffectiveSectionStep:F2} м{sNote}, прогонов {path.runs.Count}, " +
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

            string id = ResolveGeneratedId(fence, true);
            string folder = $"{GeneratedRoot}/{id}";

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Пересборка забора «{fence.name}»");

            // 1. Сначала сносим СВОИ объекты. Пока MeshRenderer жив, его меш-ассет трогать нельзя:
            //    рендерер, оставшийся с уничтоженным мешем, валит culling-джоб Burst
            //    (GPU Resident Drawer / BatchRendererGroup) — это реальный краш редактора 2026-11-14.
            RemoveGenerated(fence);

            if (output.sections.Count == 0)
            {
                output.ReleaseMeshes();
                DeleteOrphanAssets(folder, null);

                Undo.CollapseUndoOperations(undoGroup);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(fence.gameObject.scene);

                sb.AppendLine("[Fence] Секций нет — сгенерированное снесено.");
                return sb.ToString();
            }

            EnsureFolder(folder);

            var pending = new List<PendingSection>(output.sections.Count);
            var createdAssets = new List<string>();
            int createdMeshes = 0, updatedMeshes = 0;

            for (int i = 0; i < output.sections.Count; i++)
            {
                FenceSectionMesh section = output.sections[i];
                Mesh built = section.mesh;
                if (built == null) continue;

                string baseName = SanitizeName(section.name);
                string assetPath = $"{folder}/{baseName}.asset";

                // 2. Существующий меш обновляем НА МЕСТЕ (объект и GUID живут), только
                //    недостающий создаём: иначе рендереры остались бы с уничтоженным мешем.
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

                if (mesh != null)
                {
                    CopyMeshData(mesh, built);
                    EditorUtility.SetDirty(mesh);
                    Object.DestroyImmediate(built);
                    updatedMeshes++;
                }
                else
                {
                    built.name = baseName;
                    AssetDatabase.CreateAsset(built, assetPath);
                    mesh = built;
                    createdAssets.Add(assetPath);
                    createdMeshes++;
                }

                pending.Add(new PendingSection
                {
                    name = baseName,
                    assetPath = assetPath,
                    mesh = mesh,
                    localPosition = section.localPosition,
                    localRotation = section.localRotation,
                    materials = section.materials
                });
            }

            // 3. Новые меш-ассеты дописывает импорт ПОСЛЕ этого колбэка (в логе: «[WorkerN] Imported …
            //    Fence_runX_sN.asset»). Рендерер, поставленный в том же кадре, попадает в
            //    BatchRendererGroup с ещё не импортированным мешем — именно так падает culling-джоб
            //    GPU Resident Drawer (краш 2026-11-15: `Key: EntityId is not present in the
            //    NativeParallelHashMap`, `CullingJob`, `burst_abort`). Поэтому импорт форсируем
            //    синхронно, а объекты ставим следующим кадром.
            for (int i = 0; i < createdAssets.Count; i++)
                AssetDatabase.ImportAsset(createdAssets[i], ImportAssetOptions.ForceSynchronousImport);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(fence.gameObject.scene);

            Undo.CollapseUndoOperations(undoGroup);

            EnqueueSpawn(fence, id, pending, output.colliders.ToArray());

            sb.AppendLine($"[Fence] Меши: обновлено на месте {updatedMeshes}, создано {createdMeshes}; " +
                          $"id «{id}»");
            sb.AppendLine($"[Fence] Пересобрано: {folder}");
            sb.AppendLine("[Fence] Секции и коллайдеры ставятся в сцену следующим кадром — после импорта мешей.");
            return sb.ToString();
        }

        // ============================================================
        // Постановка секций: следующим кадром, когда импорт мешей уже завершён
        // ============================================================

        /// <summary>Секция, чей меш-ассет записан и импортирован; объект появится в сцене в delayCall.</summary>
        private struct PendingSection
        {
            public string name;
            public string assetPath;
            public Mesh mesh;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Material[] materials;
        }

        private static void EnqueueSpawn(SplineFence fence, string id, List<PendingSection> sections, FenceBoxSpec[] colliders)
        {
            EditorApplication.delayCall += () => Spawn(fence, id, sections, colliders);
        }

        private static void Spawn(SplineFence fence, string id, List<PendingSection> sections, FenceBoxSpec[] colliders)
        {
            if (fence == null) return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Забор «{fence.name}»: секции");

            for (int i = 0; i < sections.Count; i++)
            {
                PendingSection section = sections[i];

                var go = new GameObject(section.name);
                Undo.RegisterCreatedObjectUndo(go, "Fence section");

                go.transform.SetParent(fence.transform, false);
                go.transform.localPosition = section.localPosition;
                go.transform.localRotation = section.localRotation;
                go.transform.localScale = Vector3.one;
                go.layer = fence.gameObject.layer;

                go.AddComponent<MeshFilter>().sharedMesh = section.mesh;

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

            for (int i = 0; i < colliders.Length; i++)
                CreateCollider(colliders[i], colliderRoot.transform, fence.colliderLayer);

            // 4. Меши, которые больше не нужны этому забору, сносим ТОЛЬКО теперь — когда
            //    рендереры новой сборки уже стоят в сцене.
            var usedAssets = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sections.Count; i++) usedAssets.Add(sections[i].assetPath);

            DeleteOrphanAssets($"{GeneratedRoot}/{id}", usedAssets);

            Undo.CollapseUndoOperations(undoGroup);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(fence.gameObject.scene);

            Debug.Log($"[Fence] «{fence.name}»: в сцену добавлено секций {sections.Count}, " +
                      $"бокс-коллайдеров {colliders.Length}.");
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
        // Уникальный id и меш-ассеты
        // ============================================================

        /// <summary>
        /// Уникальный id сгенерированного: папка <c>Generated/Fences/&lt;id&gt;</c> — это «владение» забора.
        ///
        /// Зачем: два забора с одинаковым именем получали один id, второй пересобирал ту же папку
        /// и удалял меши первого. Рендерер с уничтоженным мешем валит culling-джоб Burst на GPU
        /// Resident Drawer — краш редактора 2026-11-14 (см. §14 дизайн-ноты).
        ///
        /// Кто получает базовый id: сначала забор с уже проставленным <c>fenceId</c>, затем с меньшим
        /// instanceID. Остальным выдаётся суффикс <c>_2</c>, <c>_3</c>… и он ЗАПИСЫВАЕТСЯ в
        /// <c>fence.fenceId</c>, чтобы дальше id был стабилен между сессиями.
        /// </summary>
        public static string ResolveGeneratedId(SplineFence fence, bool persist)
        {
            string baseId = SanitizeName(fence.EffectiveId);
            SplineFence[] all = AllFences();

            var group = new List<SplineFence>();
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(SanitizeName(all[i].EffectiveId), baseId, System.StringComparison.OrdinalIgnoreCase))
                    group.Add(all[i]);
            }

            group.Sort((a, b) =>
            {
                bool explicitA = !string.IsNullOrWhiteSpace(a.fenceId);
                bool explicitB = !string.IsNullOrWhiteSpace(b.fenceId);
                if (explicitA != explicitB) return explicitA ? -1 : 1;

                // Порядок в иерархии — стабильный между сессиями (в отличие от instanceID)
                return string.CompareOrdinal(HierarchyKey(a), HierarchyKey(b));
            });

            int suffix = Mathf.Max(0, group.IndexOf(fence)) + 1;
            string candidate = suffix <= 1 ? baseId : $"{baseId}_{suffix}";

            // чужой забор мог занять «Spline_2» руками
            while (IsIdTaken(candidate, fence, all))
                candidate = $"{baseId}_{++suffix}";

            if (persist && !string.Equals(candidate, SanitizeName(fence.EffectiveId), System.StringComparison.Ordinal))
            {
                Undo.RecordObject(fence, "Fence generated id");
                string previous = fence.fenceId;
                fence.fenceId = candidate;
                EditorUtility.SetDirty(fence);

                Debug.Log($"[Fence] «{fence.name}»: id «{baseId}» уже занят другим забором — " +
                          $"выдан «{candidate}» и записан в SplineFence.fenceId" +
                          (string.IsNullOrWhiteSpace(previous) ? "." : $" (было «{previous}»)."));
            }

            return candidate;
        }

        private static bool IsIdTaken(string id, SplineFence self, SplineFence[] all)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == self) continue;
                if (string.Equals(SanitizeName(all[i].EffectiveId), id, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Стабильный ключ порядка: имя сцены + путь sibling-индексов в иерархии.</summary>
        private static string HierarchyKey(SplineFence fence)
        {
            var key = new StringBuilder();
            Transform t = fence.transform;

            while (t != null)
            {
                key.Insert(0, "/" + t.GetSiblingIndex().ToString("D4"));
                t = t.parent;
            }

            key.Insert(0, fence.gameObject.scene.name);
            return key.ToString();
        }

        private static SplineFence[] AllFences()
        {
            return Object.FindObjectsByType<SplineFence>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Обновляет меш-ассет НА МЕСТЕ: объект и GUID сохраняются, поэтому ни сцена, ни Undo,
        /// ни GPU Resident Drawer не видят «уничтоженный меш» — именно это роняло редактор.
        /// </summary>
        private static void CopyMeshData(Mesh target, Mesh source)
        {
            target.Clear();
            target.indexFormat = source.indexFormat;

            target.SetVertices(source.vertices);
            target.SetNormals(source.normals);

            Vector2[] uv = source.uv;
            if (uv != null && uv.Length > 0) target.SetUVs(0, uv);

            target.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
                target.SetTriangles(source.GetTriangles(s), s, false);

            target.RecalculateBounds();
        }

        /// <summary>Сносит меши папки, которые текущая сборка не использовала (used = null — все).</summary>
        private static void DeleteOrphanAssets(string folder, HashSet<string> used)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;

            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { folder });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (used != null && used.Contains(path)) continue;

                AssetDatabase.DeleteAsset(path);
            }
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

        public static FenceTypeDefinition FindDefaultType()
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
