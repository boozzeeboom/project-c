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
    /// Editor-инструмент дорожки: сплайн → непрерывное покрытие (+ бордюры) + плиты-коллайдеры.
    ///
    /// Правила те же, что у SplineFenceTool:
    ///   * геометрия строится в ЛОКАЛЬНЫХ координатах корня (floating origin);
    ///   * меш сохраняется ассетом в Assets/_Project/Generated/Roads/&lt;id&gt;/ и обновляется НА МЕСТЕ
    ///     (GUID живёт: рендерер с уничтоженным мешем валит culling-джоб Burst);
    ///   * пересборка идемпотентна: своё сносится по маркеру RoadSection, чужое не трогается;
    ///   * масштаб корня обязан быть 1.
    /// </summary>
    public static class SplineRoadTool
    {
        private const string GeneratedRoot = "Assets/_Project/Generated/Roads";
        private const string ColliderSuffix = "_COLLIDER";
        private const string MeshSuffix = "_road";

        private const string DefaultRoadMaterial = "Assets/_Project/Materials/Cobble/M_PC_Cobble.mat";
        private const string DefaultCurbMaterial = "Assets/_Project/Materials/Concrete/M_PC_Concrete.mat";

        // ============================================================
        // Меню
        // ============================================================

        [MenuItem("Tools/ProjectC/World/Spline Road/1. Создать дорожку из выделенного сплайна", false, 891)]
        public static void CreateForSelection()
        {
            SplineContainer container = FindContainerInSelection();
            if (container == null)
            {
                Debug.LogError("[Road] Выделите объект со SplineContainer (узлы рисуются инструментом Splines).");
                return;
            }

            var road = container.GetComponent<SplineRoad>();
            if (road == null)
            {
                road = Undo.AddComponent<SplineRoad>(container.gameObject);
                Debug.Log($"[Road] Добавлен SplineRoad на «{container.name}».");
            }

            if (road.roadMaterial == null)
                road.roadMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultRoadMaterial);

            if (road.curbMaterial == null)
                road.curbMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultCurbMaterial);

            Debug.Log(Rebuild(road));
            Selection.activeGameObject = container.gameObject;
        }

        [MenuItem("Tools/ProjectC/World/Spline Road/2. Пересобрать выделенные", false, 892)]
        public static void RebuildSelected()
        {
            var report = new StringBuilder();
            int count = 0;

            foreach (GameObject go in Selection.gameObjects)
            {
                var road = go.GetComponent<SplineRoad>();
                if (road == null) road = go.GetComponentInParent<SplineRoad>();
                if (road == null) continue;

                report.AppendLine(Rebuild(road));
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[Road] В выделении нет объектов со SplineRoad.");
                return;
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/ProjectC/World/Spline Road/3. Сухой прогон (без записи в сцену)", false, 893)]
        public static void DryRunSelected()
        {
            var report = new StringBuilder();
            int count = 0;

            foreach (GameObject go in Selection.gameObjects)
            {
                var road = go.GetComponent<SplineRoad>();
                if (road == null) road = go.GetComponentInParent<SplineRoad>();
                if (road == null) continue;

                report.AppendLine(Rebuild(road, true));
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[Road] В выделении нет объектов со SplineRoad.");
                return;
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/ProjectC/World/Spline Road/5. Снести сгенерированное", false, 895)]
        public static void RemoveSelected()
        {
            int count = 0;

            foreach (GameObject go in Selection.gameObjects)
            {
                var road = go.GetComponent<SplineRoad>();
                if (road == null) road = go.GetComponentInParent<SplineRoad>();
                if (road == null) continue;

                Remove(road);
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[Road] В выделении нет объектов со SplineRoad.");
                return;
            }

            Debug.Log($"[Road] Сгенерированное снесено у {count} дорожек.");
        }

        /// <summary>Сносит сгенерированное одной дорожки: объекты из сцены + папку мешей. Без диалогов.</summary>
        public static void Remove(SplineRoad road)
        {
            if (road == null) return;

            RemoveGenerated(road);
            DeleteGeneratedFolder(GeneratedFolder(road));

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);

            Debug.Log($"[Road] «{road.name}»: сгенерированное снесено.");
        }

        // ============================================================
        // Пересборка
        // ============================================================

        /// <summary>
        /// Строит дорожку. dryRun = true — только путь и отчёт, без объектов, ассетов и Undo.
        /// </summary>
        public static string Rebuild(SplineRoad road, bool dryRun = false)
        {
            var sb = new StringBuilder();

            if (road == null) return "[Road] Нет SplineRoad.";

            if (road.Container == null)
                return $"[Road] {road.name}: нет SplineContainer.";

            if (road.ActiveSpline == null || road.ActiveSpline.Count < 2)
                return $"[Road] {road.name}: в контейнере нет сплайна с двумя узлами.";

            Vector3 scale = road.transform.lossyScale;
            bool badScale = Mathf.Abs(scale.x - 1f) > 1e-3f ||
                            Mathf.Abs(scale.y - 1f) > 1e-3f ||
                            Mathf.Abs(scale.z - 1f) > 1e-3f;

            if (badScale)
                sb.AppendLine($"[Road] ⚠ {road.name}: масштаб корня {scale.x:F3}/{scale.y:F3}/{scale.z:F3} ≠ 1 — " +
                              "ширина и коллайдеры разъедутся с покрытием. Ставьте 1/1/1.");

            // Свои объекты сносим ДО расчёта: иначе луч привязки к поверхности попадёт в собственные
            // плиты-коллайдеры. Меши-ассеты при этом не трогаются — они обновляются на месте ниже.
            if (!dryRun) RemoveGenerated(road);

            RoadPath path = road.BuildPath(out int snapMisses);
            RoadGeometrySettings geometry = road.ToGeometrySettings();
            RoadBuildOutput output = RoadMeshBuilder.Build(path, geometry);

            string snapNote = road.snapToSurface
                ? $", привязка к поверхности: промахов {snapMisses}"
                : ", привязка к поверхности выключена";

            sb.AppendLine($"[Road] {road.name} → {road.surface}, ширина {road.width:F2} м, " +
                          $"длина {path.Length:F2} м, кадров {path.frames.Count}, сегментов {path.SegmentCount}, " +
                          $"квадов {output.quads}, треугольников {output.triangles}, " +
                          $"коллайдеров {(road.generateColliders ? output.colliders.Count : 0)}" +
                          $"{(road.generateColliders ? "" : " (выключены)")}{snapNote}");

            if (road.snapToSurface && snapMisses > 0)
                sb.AppendLine($"[Road] ⚠ {snapMisses} из {path.frames.Count} кадров не нашли поверхность лучом " +
                              "(маска/глубина?) — там покрытие осталось на линии сплайна.");

            for (int i = 0; i < output.warnings.Count; i++)
                sb.AppendLine($"[Road] ⚠ {output.warnings[i]}");

            if (dryRun)
            {
                output.Release();
                return sb.ToString();
            }

            if (output.mesh == null)
            {
                output.Release();
                sb.AppendLine("[Road] Геометрия не построена — сцена не тронута.");
                return sb.ToString();
            }

            string id = ResolveGeneratedId(road, true);
            string folder = $"{GeneratedRoot}/{id}";

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Пересборка дорожки «{road.name}»");

            EnsureFolder(folder);

            string assetPath = $"{folder}/{id}{MeshSuffix}.asset";
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

            if (asset != null)
            {
                CopyMeshData(asset, output.mesh);
                EditorUtility.SetDirty(asset);
                Object.DestroyImmediate(output.mesh);
            }
            else
            {
                output.mesh.name = id + MeshSuffix;
                AssetDatabase.CreateAsset(output.mesh, assetPath);
                asset = output.mesh;
            }

            output.mesh = null;

            var go = new GameObject(id + MeshSuffix);
            Undo.RegisterCreatedObjectUndo(go, "Road mesh");

            go.transform.SetParent(road.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = road.gameObject.layer;

            go.AddComponent<MeshFilter>().sharedMesh = asset;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = output.materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            go.AddComponent<RoadSection>().owner = road;

            if (road.generateColliders && output.colliders.Count > 0)
            {
                var colliderRoot = new GameObject(id + ColliderSuffix);
                Undo.RegisterCreatedObjectUndo(colliderRoot, "Road colliders");

                colliderRoot.transform.SetParent(road.transform, false);
                colliderRoot.transform.localPosition = Vector3.zero;
                colliderRoot.transform.localRotation = Quaternion.identity;
                colliderRoot.transform.localScale = Vector3.one;
                colliderRoot.layer = road.colliderLayer;
                colliderRoot.AddComponent<RoadSection>().owner = road;

                for (int i = 0; i < output.colliders.Count; i++)
                    CreateCollider(output.colliders[i], colliderRoot.transform, road.colliderLayer);
            }

            DeleteOrphanAssets(folder, assetPath);

            Undo.CollapseUndoOperations(undoGroup);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);

            sb.AppendLine($"[Road] id «{id}», меш {assetPath}");
            return sb.ToString();
        }

        // ============================================================
        // Коллайдеры
        // ============================================================

        private static void CreateCollider(FenceBoxSpec spec, Transform parent, int layer)
        {
            var go = new GameObject(spec.name);
            Undo.RegisterCreatedObjectUndo(go, "Road collider");

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
        /// Уникальный id сгенерированного: папка <c>Generated/Roads/&lt;id&gt;</c> — это «владение» дорожки.
        /// Две дорожки с одинаковым именем получили бы одну папку и снесли меши друг друга.
        /// Кто получает базовый id: сначала дорожка с уже проставленным roadId, затем с более ранним
        /// положением в иерархии. Остальным выдаётся суффикс _2, _3… и он записывается в roadId.
        /// </summary>
        public static string ResolveGeneratedId(SplineRoad road, bool persist)
        {
            string baseId = SanitizeName(road.EffectiveId);
            SplineRoad[] all = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Include);

            var group = new List<SplineRoad>();
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(SanitizeName(all[i].EffectiveId), baseId, System.StringComparison.OrdinalIgnoreCase))
                    group.Add(all[i]);
            }

            group.Sort((a, b) =>
            {
                bool explicitA = !string.IsNullOrWhiteSpace(a.roadId);
                bool explicitB = !string.IsNullOrWhiteSpace(b.roadId);
                if (explicitA != explicitB) return explicitA ? -1 : 1;

                return string.CompareOrdinal(HierarchyKey(a), HierarchyKey(b));
            });

            int suffix = Mathf.Max(0, group.IndexOf(road)) + 1;
            string candidate = suffix <= 1 ? baseId : $"{baseId}_{suffix}";

            while (IsIdTaken(candidate, road, all))
                candidate = $"{baseId}_{++suffix}";

            if (persist && !string.Equals(candidate, SanitizeName(road.EffectiveId), System.StringComparison.Ordinal))
            {
                Undo.RecordObject(road, "Road generated id");
                string previous = road.roadId;
                road.roadId = candidate;
                EditorUtility.SetDirty(road);

                Debug.Log($"[Road] «{road.name}»: id «{baseId}» уже занят другой дорожкой — " +
                          $"выдан «{candidate}» и записан в SplineRoad.roadId" +
                          (string.IsNullOrWhiteSpace(previous) ? "." : $" (было «{previous}»)."));
            }

            return candidate;
        }

        private static bool IsIdTaken(string id, SplineRoad self, SplineRoad[] all)
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
        private static string HierarchyKey(SplineRoad road)
        {
            var key = new StringBuilder();
            Transform t = road.transform;

            while (t != null)
            {
                key.Insert(0, "/" + t.GetSiblingIndex().ToString("D4"));
                t = t.parent;
            }

            key.Insert(0, road.gameObject.scene.name);
            return key.ToString();
        }

        /// <summary>Обновляет меш-ассет НА МЕСТЕ: объект и GUID сохраняются.</summary>
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

        /// <summary>Сносит меши папки, которые текущая сборка не использовала (keep = null — все).</summary>
        private static void DeleteOrphanAssets(string folder, string keep)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;

            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { folder });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (keep != null && string.Equals(path, keep, System.StringComparison.OrdinalIgnoreCase)) continue;

                AssetDatabase.DeleteAsset(path);
            }
        }

        // ============================================================
        // Снос и папки
        // ============================================================

        private static void RemoveGenerated(SplineRoad road)
        {
            Transform root = road.transform;
            string colliderName = SanitizeName(road.EffectiveId) + ColliderSuffix;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);

                var marker = child.GetComponent<RoadSection>();
                if (marker == null && child.name != colliderName) continue;

                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static string GeneratedFolder(SplineRoad road)
        {
            return $"{GeneratedRoot}/{SanitizeName(road.EffectiveId)}";
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

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Road";

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
