using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Сборка ВСЕХ коллайдеров поддерева в одну группу <c>&lt;Имя FBX&gt;_COLLIDER</c>.
    ///
    /// Зачем: FBX приходит с множеством дочерних FBX, и по каждому ребёнку работают точечно
    /// (Точный MeshCollider, Лестница, боксы BuildSettlementColliders). В результате коллайдеры
    /// рассыпаны по всему дереву. К концу работы нужен один объект, который можно утащить
    /// в нужную сцену/папку целиком — он и создаётся этой кнопкой.
    ///
    /// Правила:
    ///   - обрабатывается КАЖДЫЙ выделенный объект как корень: группа создаётся дочерней
    ///     у него и называется <c>&lt;имя&gt;_COLLIDER</c> (повторный запуск переиспользует её);
    ///   - переносятся только «коллайдерные ветки»: поддеревья, внутри которых НЕТ ни одного
    ///     MeshFilter/Renderer (т.е. холдеры _MC, _STAIR, BC_*, <c>*_Generated</c> и прочие
    ///     контейнеры). Арт-объекты с мешами/рендерерами не трогаются никогда, даже если на них
    ///     самих висит коллайдер — такой коллайдер принадлежит мешу и остаётся на месте;
    ///   - найдя коллайдерную ветку, инструмент не спускается внутрь неё: она переезжает целиком,
    ///     со своей внутренней структурой (RAMPS, STEP_01, MG_* и т.п.);
    ///   - мировые позиция/поворот/масштаб сохраняются, поэтому размеры BoxCollider в метрах
    ///     (они заданы через localScale детей) не меняются;
    ///   - всё пишется в одну Undo-операцию.
    /// </summary>
    public static class CollectCollidersTool
    {
        /// <summary>Суффикс группы, в которую собираются коллайдеры.</summary>
        public const string GroupSuffix = "_COLLIDER";

        [MenuItem("Tools/ProjectC/Colliders/Собрать все коллайдеры в <Имя>_COLLIDER", false, 37)]
        public static void CollectForSelection()
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogError("[CollectColliders] Ничего не выделено. Выдели главный FBX — " +
                               "коллайдеры со всех его дочерних объектов переедут в '<Имя>_COLLIDER'.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Collect colliders");

            var log = new StringBuilder();
            int rootsDone = 0, movedTotal = 0, colliderTotal = 0, reusedGroups = 0;

            foreach (GameObject go in selection)
            {
                if (go == null) continue;
                Transform root = go.transform;

                if (root.name.EndsWith(GroupSuffix) && root.parent != null &&
                    root.name == root.parent.name + GroupSuffix)
                {
                    log.AppendLine($"  '{root.name}': это уже группа коллайдеров своего родителя — пропуск.");
                    continue;
                }

                bool reused;
                Transform group = EnsureGroup(root, out reused);
                if (reused) reusedGroups++;

                var branches = new List<Transform>();
                CollectBranches(root, group, branches);

                int moved = 0, colliders = 0;
                foreach (Transform branch in branches)
                {
                    if (branch == null || branch == group) continue;
                    if (!branch.IsChildOf(root)) continue;   // уехал вместе с родительской ветвью
                    colliders += branch.GetComponentsInChildren<Collider>(true).Length;
                    MovePreservingWorld(branch, group);
                    moved++;
                }

                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

                rootsDone++;
                movedTotal += moved;
                colliderTotal += colliders;
                log.AppendLine($"  '{root.name}': группа '{group.name}'{(reused ? " (уже была)" : " (создана)")}, " +
                               $"перенесено ветвей={moved}, коллайдеров={colliders}" +
                               (moved == 0 ? " — переносить было нечего" : "") + ".");
            }

            Debug.Log($"[CollectColliders] корней обработано={rootsDone} (групп переиспользовано={reusedGroups}), " +
                      $"ветвей перенесено={movedTotal}, коллайдеров={colliderTotal}.\n{log}");
        }

        // ------------------------------------------------------------ группа

        /// <summary>Группа-приёмник: дочерний объект корня с именем <c>&lt;корень&gt;_COLLIDER</c>.</summary>
        private static Transform EnsureGroup(Transform root, out bool reused)
        {
            string want = root.name + GroupSuffix;

            Transform existing = root.Find(want);
            if (existing != null)
            {
                reused = true;
                return existing;
            }

            reused = false;

            var g = new GameObject(want);
            Undo.RegisterCreatedObjectUndo(g, "Create collider group");
            g.layer = root.gameObject.layer;
            g.isStatic = false;                       // группа — только контейнер

            Transform gt = g.transform;
            gt.SetParent(root, false);
            gt.localPosition = Vector3.zero;
            gt.localRotation = Quaternion.identity;
            gt.localScale = Vector3.one;
            return gt;
        }

        // ------------------------------------------------------------ поиск ветвей

        private static void CollectBranches(Transform parent, Transform group, List<Transform> found)
        {
            foreach (Transform child in parent)
            {
                if (child == group) continue;

                if (IsColliderBranch(child))
                {
                    found.Add(child);
                    continue;                 // внутрь не спускаемся: ветка переезжает целиком
                }

                CollectBranches(child, group, found);
            }
        }

        /// <summary>
        /// «Коллайдерная ветка» = поддерево без единого MeshFilter/Renderer, но с коллайдерами.
        /// Арт внутри такой ветки означал бы, что это не холдер, а обычный объект сцены, —
        /// такие не переносим.
        /// </summary>
        private static bool IsColliderBranch(Transform t)
        {
            if (t.GetComponent<MeshFilter>() != null) return false;
            if (t.GetComponent<Renderer>() != null) return false;
            if (t.GetComponentInChildren<MeshFilter>(true) != null) return false;
            if (t.GetComponentInChildren<Renderer>(true) != null) return false;
            return t.GetComponentInChildren<Collider>(true) != null;
        }

        // ------------------------------------------------------------ перенос

        /// <summary>Смена родителя с сохранением мировой рамки (иначе «уедут» и позиция, и размеры боксов).</summary>
        private static void MovePreservingWorld(Transform t, Transform newParent)
        {
            Vector3 pos = t.position;
            Quaternion rot = t.rotation;
            Vector3 worldScale = t.lossyScale;

            Undo.RecordObject(t, "Collect colliders");
            Undo.SetTransformParent(t, newParent, true, "Collect colliders");

            t.SetPositionAndRotation(pos, rot);
            t.localScale = Divide(worldScale, newParent.lossyScale);
        }

        private static Vector3 Divide(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Mathf.Approximately(b.x, 0f) ? a.x : a.x / b.x,
                Mathf.Approximately(b.y, 0f) ? a.y : a.y / b.y,
                Mathf.Approximately(b.z, 0f) ? a.z : a.z / b.z);
        }
    }
}
