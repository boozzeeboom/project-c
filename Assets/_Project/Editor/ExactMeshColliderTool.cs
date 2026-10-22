using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Точный коллайдер «по геометрии» для ОДНОГО выделенного объекта (полусфера, арка,
    /// лестница, балясина — то, что боксом не описать).
    ///
    /// Почему не <see cref="BuildSettlementColliders"/>: тот инструмент приближает форму
    /// боксами/капсулами/нарезкой и обходит дерево целиком. Здесь задача обратная — взять
    /// конкретный меш, который пользователь выделил, и получить коллайдер, СОВПАДАЮЩИЙ с ним
    /// треугольник в треугольник. Дети не обходятся, фильтры имён/размера не применяются.
    ///
    /// Как получается точность без перепекания:
    ///   - <c>MeshCollider.sharedMesh = исходный меш</c>. PhysX кукает сам меш-ассет, вершины
    ///     читать не нужно, поэтому Read/Write = Off у FBX не мешает (проверено на
    ///     ADMIN_FOR_COLLIDER: 2758 мешей, все нечитаемые, кукаются);
    ///   - холдер — СОСЕД исходника (тот же parent) с точно такими же
    ///     localPosition/localRotation/localScale, поэтому мировая рамка совпадает 1:1 при
    ///     любом масштабе и повороте родителя (у FBX здесь localScale = 100);
    ///   - сам объект не трогается: MeshCollider висит на отдельном GameObject
    ///     <c>&lt;Имя&gt;_MC</c>, чтобы коллайдер можно было снести/пересобрать не задев арт.
    ///
    /// Ограничения PhysX, из-за которых есть выпуклый режим:
    ///   - concave (convex = false) нельзя на non-kinematic Rigidbody → для кораблей
    ///     используйте выпуклый режим;
    ///   - выпуклый MeshCollider ограничен 255 треугольниками — более сложные меши
    ///     пропускаются с предупреждением (иначе PhysX молча не сварит коллайдер).
    ///
    /// Идемпотентно: повторное нажатие на тот же объект не плодит дубли — холдер с тем же
    /// мешем, режимом и рамкой пропускается, изменённый пересобирается.
    /// </summary>
    public static class ExactMeshColliderTool
    {
        /// <summary>Суффикс объекта-холдера рядом с исходником.</summary>
        public const string HolderSuffix = "_MC";

        /// <summary>Жёсткий лимит Unity на число треугольников выпуклого MeshCollider.</summary>
        public const int ConvexTriangleLimit = 255;

        [MenuItem("Tools/ProjectC/Colliders/Точный MeshCollider — по выделению", false, 30)]
        public static void BuildConcaveForSelection() => BuildForSelection(false);

        [MenuItem("Tools/ProjectC/Colliders/Точный MeshCollider — выпуклый (корабли)", false, 31)]
        public static void BuildConvexForSelection() => BuildForSelection(true);

        [MenuItem("Tools/ProjectC/Colliders/Снести точные MeshCollider — по выделению", false, 32)]
        public static void RemoveForSelection()
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogError($"[ExactMesh] Ничего не выделено — выдели объект (или его холдер {HolderSuffix}).");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Remove exact mesh collider");

            int removed = 0, none = 0;
            foreach (GameObject go in selection)
            {
                if (go == null) continue;
                Transform holder = FindHolder(go.transform);
                if (holder == null) { none++; continue; }
                Undo.DestroyObjectImmediate(holder.gameObject);
                removed++;
            }

            Debug.Log($"[ExactMesh] снесено холдеров: {removed}, без холдера: {none}.");
        }

        /// <summary>
        /// Построить точный MeshCollider для каждого выделенного объекта, у которого есть
        /// MeshFilter. Дети выделенных объектов НЕ обходятся — это точечный инструмент.
        /// </summary>
        public static void BuildForSelection(bool convex)
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogError($"[ExactMesh] Ничего не выделено. Выдели объект с мешем (например полусферу) и нажми снова.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build exact mesh collider");

            var log = new StringBuilder();
            int created = 0, rebuilt = 0, skipped = 0, noMesh = 0, tooComplex = 0, onSelf = 0;
            long tris = 0;

            foreach (GameObject go in selection)
            {
                if (go == null) continue;

                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    noMesh++;
                    log.AppendLine($"  '{go.name}': нет MeshFilter/меша — пропуск.");
                    continue;
                }

                Mesh mesh = mf.sharedMesh;
                int meshTris = TriangleCount(mesh);
                if (meshTris == 0)
                {
                    noMesh++;
                    log.AppendLine($"  '{go.name}': меш без треугольников — пропуск.");
                    continue;
                }

                if (convex && meshTris > ConvexTriangleLimit)
                {
                    tooComplex++;
                    log.AppendLine($"  '{go.name}': {meshTris} тр. — выпуклый MeshCollider в Unity " +
                                   $"ограничен {ConvexTriangleLimit} треугольниками, пропуск.");
                    continue;
                }

                if (go.GetComponent<MeshCollider>() != null)
                {
                    onSelf++;
                    log.AppendLine($"  '{go.name}': MeshCollider уже висит на самом объекте — холдер не нужен.");
                    continue;
                }

                if (!convex && go.GetComponentInParent<Rigidbody>() != null)
                    Debug.LogWarning($"[ExactMesh] '{go.name}' под non-kinematic Rigidbody: " +
                                     $"concave MeshCollider там не работает — нужен выпуклый режим или пропуск.");

                Transform src = go.transform;
                Transform holder = FindHolder(src);
                bool existed = holder != null;

                if (existed)
                {
                    var old = holder.GetComponent<MeshCollider>();
                    if (old != null && old.sharedMesh == mesh && old.convex == convex && SameLocal(holder, src))
                    {
                        skipped++;
                        log.AppendLine($"  '{go.name}': точный холдер уже на месте — пропуск.");
                        continue;
                    }
                    Undo.DestroyObjectImmediate(holder.gameObject);
                }

                var h = new GameObject(go.name + HolderSuffix);
                Undo.RegisterCreatedObjectUndo(h, "Create exact mesh collider");
                h.layer = go.layer;
                h.isStatic = go.isStatic;

                Transform ht = h.transform;
                ht.SetParent(src.parent, false);
                ht.localPosition = src.localPosition;
                ht.localRotation = src.localRotation;
                ht.localScale = src.localScale;

                var mc = h.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = convex;
                mc.isTrigger = false;
                mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                                  | MeshColliderCookingOptions.EnableMeshCleaning
                                  | MeshColliderCookingOptions.WeldColocatedVertices
                                  | MeshColliderCookingOptions.UseFastMidphase;

                EditorSceneManager.MarkSceneDirty(go.scene);

                if (existed) rebuilt++; else created++;
                tris += meshTris;
                log.AppendLine($"  '{go.name}': {(existed ? "пересобран" : "создан")} " +
                               $"{go.name}{HolderSuffix} ({meshTris} тр., convex={convex}).");
            }

            Debug.Log($"[ExactMesh] режим {(convex ? "ВЫПУКЛЫЙ" : "точный concave")}: " +
                      $"создано={created}, пересобрано={rebuilt}, уже было={skipped}, " +
                      $"на самом объекте={onSelf}, нет меша={noMesh}, сложнее лимита={tooComplex}, " +
                      $"треугольников в коллайдерах={tris}.\n" + log);
        }

        /// <summary>Холдер-сосед с именем &lt;Имя&gt;_MC (для корневых объектов — поиск по корням сцены).</summary>
        private static Transform FindHolder(Transform src)
        {
            string name = src.name + HolderSuffix;

            // Выделили сам холдер — считаем, что нашли.
            if (src.name.EndsWith(HolderSuffix)) name = src.name;

            Transform parent = src.parent;
            if (parent != null) return parent.Find(name);

            var scene = src.gameObject.scene;
            if (!scene.IsValid()) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
        }

        private static bool SameLocal(Transform a, Transform b)
        {
            return (a.localPosition - b.localPosition).sqrMagnitude < 1e-10f
                && Quaternion.Angle(a.localRotation, b.localRotation) < 0.01f
                && (a.localScale - b.localScale).sqrMagnitude < 1e-10f;
        }

        /// <summary>Число треугольников без чтения вершин (работает и на non-readable FBX-мешах).</summary>
        private static int TriangleCount(Mesh m)
        {
            int t = 0;
            for (int s = 0; s < m.subMeshCount; s++) t += (int)(m.GetIndexCount(s) / 3);
            return t;
        }
    }
}
