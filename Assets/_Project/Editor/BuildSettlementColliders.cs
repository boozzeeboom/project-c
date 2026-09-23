using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Универсальный builder BoxCollider'ов для поселений из FBX (фермы, дворы, блок-ауты).
    ///
    /// Зачем: закинул FBX на сцену → выделил корень → один клик → по одному BoxCollider
    /// на каждый значимый меш внутри. Боксы одинаково годятся игроку (CharacterController)
    /// и кораблям (Rigidbody): статичные боксы дешёвые для PhysX и не требуют варки,
    /// в отличие от одного гигантского concave-меша на всё поселение.
    ///
    /// Что умеет:
    ///   - рекурсивный обход всех MeshFilter под корнем (включая вложенные группы зданий);
    ///   - фильтр мелочи: пропуск по токенам имени (болты, провода, лампы...) и по мировому
    ///     размеру (max стороны world AABB < minWorldSize);
    ///   - мин. толщина minThickness (world, метры) по каждой тонкой оси с расширением ВНИЗ —
    ///     топ коллайдера остаётся заподлицо с визуалом, корабли не туннелят сквозь палубы
    ///     (класс ошибки MD2_Deck_Blockout);
    ///   - коллайдеры живут в отдельной иерархии "<Root>_Colliders/<Группа>/..." в сцене,
    ///     а не на самих FBX-узлах: переживает переимпорт FBX, перезапуск идемпотентен;
    ///   - Dry Run: посчитать created/skipped без создания объектов (подбор порогов для 2к мешей).
    ///
    /// Floating origin: коллайдеры — обычные дети корня сцены, хранят только локальные
    /// center/size, мировых позиций не кэшируют — едут со сдвигом бесплатно (зелёная зона).
    /// Использование: Tools → ProjectC → Build Settlement Colliders... → Dry Run → Build.
    /// </summary>
    public static class BuildSettlementColliders
    {
        public const string GeneratedSuffix = "_Colliders";
        private const float Epsilon = 1e-4f;

        [Serializable]
        public class Settings
        {
            [Tooltip("Меши с max стороной world AABB меньше этого (м) пропускаются (болты и т.п.).")]
            public float minWorldSize = 0.25f;

            [Tooltip("Мин. толщина коллайдера в мире (м) по каждой оси. Лечит проваливание кораблей сквозь палубы.")]
            public float minThickness = 0.5f;

            [Tooltip("Подстроки имён (через запятую, регистр не важен): такие меши пропускаются.")]
            public string excludeTokens =
                "BOLT,SCREW,RIVET,NUT,WASHER,PIN,WIRE,CABLE,HOSE,PIPE,RAIL," +
                "LAMP,LIGHT,GLOW,GLASS,WINDOW,DECAL,MARKER,REFERENCE,TRIGGER,VOLUME," +
                "LOD1,LOD2,LOD3";

            [Tooltip("Группировать боксы по прямому ребёнку корня (обычно отдельное здание).")]
            public bool groupByTopLevel = true;

            [Tooltip("Пропускать GameObject'ы, выключенные в иерархии (вместе с детьми).")]
            public bool skipInactive = true;

            [Tooltip("Заодно утолщить уже существующие тонкие BoxCollider внутри корня (город не трогаем — проход только внутри выбранного корня).")]
            public bool repairThinBoxes = true;
        }

        public class Report
        {
            public int created;
            public int skippedNoMesh;
            public int skippedDisabled;
            public int skippedName;
            public int skippedTiny;
            public int repaired;
            public readonly List<string> skippedNameSample = new List<string>();
        }

        public static Settings DefaultSettings()
        {
            return new Settings();
        }

        /// <summary>Только посчитать (без создания объектов) — для подбора порогов.</summary>
        public static Report DryRun(GameObject root, Settings s)
        {
            var report = new Report();
            if (root == null || s == null) return report;
            Collect(root, s, null, report, true);
            if (s.repairThinBoxes)
                report.repaired = CountThinBoxes(root, s, GeneratedSuffixRootName(root));
            return report;
        }

        /// <summary>Полная сборка: снести старую генерацию и построить заново.</summary>
        public static Report BuildFor(GameObject root, Settings s)
        {
            var report = new Report();
            if (root == null)
            {
                Debug.LogError("[SettlementColliders] Корень не задан.");
                return report;
            }
            if (s == null) s = DefaultSettings();
            if (s.minThickness < Epsilon) s.minThickness = Epsilon;
            if (s.minWorldSize < 0f) s.minWorldSize = 0f;

            string genName = GeneratedSuffixRootName(root);

            // Группируем всё создание в одну Undo-операцию (иначе 2к боксов = 2к Ctrl+Z).
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Build settlement colliders: {root.name}");

            // 1) Снести предыдущую генерацию (идемпотентность).
            Transform old = root.transform.Find(genName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            // 2) Корень генерации: identity-локально под корнем сцены/FBX.
            var genRoot = new GameObject(genName);
            Undo.RegisterCreatedObjectUndo(genRoot, "Build settlement colliders");
            genRoot.transform.SetParent(root.transform, false);
            genRoot.transform.localPosition = Vector3.zero;
            genRoot.transform.localRotation = Quaternion.identity;
            genRoot.transform.localScale = Vector3.one;
            genRoot.layer = root.layer;
            genRoot.isStatic = true;

            var groups = new Dictionary<string, Transform>(StringComparer.Ordinal);

            // 3) Собрать и построить.
            Collect(root, s, genRoot.transform, report, false, groups);

            // 4) Repair-проход по pre-existing тонким боксам (вне нашей генерации).
            if (s.repairThinBoxes)
                report.repaired = RepairThinBoxes(root, s, genRoot.transform);

            EditorUtility.SetDirty(genRoot);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = genRoot;

            Debug.Log($"[SettlementColliders] '{root.name}': created={report.created}, " +
                      $"skipped(noMesh={report.skippedNoMesh}, disabled={report.skippedDisabled}, " +
                      $"name={report.skippedName}, tiny={report.skippedTiny}), repaired={report.repaired}. " +
                      $"Иерархия: {genName}");
            if (report.skippedNameSample.Count > 0)
                Debug.Log("[SettlementColliders] Примеры пропущенных по имени: " +
                          string.Join(", ", report.skippedNameSample.ToArray()));
            return report;
        }

        private static string GeneratedSuffixRootName(GameObject root)
        {
            return root.name + GeneratedSuffix;
        }

        private class Item
        {
            public GameObject source;
            public Mesh mesh;
            public MeshRenderer renderer;
            public Vector3 worldSize;
        }

        private static void Collect(
            GameObject root,
            Settings s,
            Transform genRoot,
            Report report,
            bool dryOnly,
            Dictionary<string, Transform> groups = null)
        {
            string[] tokens = SplitTokens(s.excludeTokens);

            var filters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            var items = new List<Item>(filters.Length);

            foreach (var f in filters)
            {
                if (f == null) continue;
                GameObject go = f.gameObject;

                // Свою генерацию не обрабатываем (при rebuild её уже нет, но на всякий случай).
                if (genRoot != null && (go == genRoot.gameObject || go.transform.IsChildOf(genRoot)))
                    continue;

                if (f.sharedMesh == null)
                {
                    report.skippedNoMesh++;
                    continue;
                }

                if (s.skipInactive && !go.activeInHierarchy)
                {
                    report.skippedDisabled++;
                    continue;
                }

                var r = go.GetComponent<MeshRenderer>();
                if (r != null && !r.enabled)
                {
                    report.skippedDisabled++;
                    continue;
                }

                string upper = go.name.ToUpperInvariant();
                if (MatchesAny(upper, tokens))
                {
                    report.skippedName++;
                    if (report.skippedNameSample.Count < 10)
                        report.skippedNameSample.Add(go.name);
                    continue;
                }

                Vector3 worldSize = r != null
                    ? r.bounds.size
                    : ScaledSize(f.sharedMesh.bounds.size, AbsVec(go.transform.lossyScale));

                if (MaxComponent(worldSize) < s.minWorldSize)
                {
                    report.skippedTiny++;
                    continue;
                }

                if (dryOnly)
                {
                    report.created++; // в dry-режиме created = "было бы создано"
                    continue;
                }

                items.Add(new Item { source = go, mesh = f.sharedMesh, renderer = r, worldSize = worldSize });
            }

            if (dryOnly) return;

            int index = 0;
            foreach (var it in items)
            {
                Transform parent = genRoot;
                if (s.groupByTopLevel)
                {
                    string groupName = SanitizeName(TopLevelChildName(root.transform, it.source.transform));
                    if (!groups.TryGetValue(groupName, out parent))
                    {
                        var g = new GameObject(groupName);
                        Undo.RegisterCreatedObjectUndo(g, "Create settlement collider group");
                        g.transform.SetParent(genRoot, false);
                        g.transform.localPosition = Vector3.zero;
                        g.transform.localRotation = Quaternion.identity;
                        g.transform.localScale = Vector3.one;
                        g.layer = root.layer;
                        g.isStatic = true;
                        parent = g.transform;
                        groups[groupName] = parent;
                    }
                }

                var holder = new GameObject($"BC_{index:0000}_{SanitizeName(it.source.name)}");
                Undo.RegisterCreatedObjectUndo(holder, "Create settlement box collider");
                holder.layer = root.layer;
                holder.isStatic = true;
                holder.transform.SetParent(parent, true);
                // Рамка холдера = мировая рамка исходника: оффсет box.center не удваивается.
                holder.transform.SetPositionAndRotation(
                    it.source.transform.position,
                    it.source.transform.rotation);
                holder.transform.localScale = Divide(it.source.transform.lossyScale, parent.lossyScale);

                var box = holder.AddComponent<BoxCollider>();
                Vector3 center;
                Vector3 size;
                FitBox(it.mesh.bounds, AbsVec(it.source.transform.lossyScale),
                    holder.transform, s.minThickness, out center, out size);
                box.center = center;
                box.size = size;
                box.isTrigger = false;

                index++;
                report.created++;
            }
        }

        /// <summary>
        /// Вписать BoxCollider по mesh-local bounds; тонкие оси расширить до minThicknessWorld,
        /// расширяя вниз (топ заподлицо с визуалом). holderFrame уже выставлен в мировую рамку исходника.
        /// </summary>
        private static void FitBox(Bounds meshBounds, Vector3 worldPerLocal, Transform holderFrame,
            float minThicknessWorld, out Vector3 center, out Vector3 size)
        {
            center = meshBounds.center;
            size = meshBounds.size;

            Vector3 localDown = holderFrame.worldToLocalMatrix.MultiplyVector(Vector3.down);
            if (localDown.sqrMagnitude < Epsilon)
                localDown = Vector3.down;
            else
                localDown.Normalize();

            for (int a = 0; a < 3; a++)
            {
                float per = Mathf.Abs(worldPerLocal[a]) < Epsilon ? 1f : Mathf.Abs(worldPerLocal[a]);
                float world = size[a] * per;
                if (world < minThicknessWorld)
                {
                    float newLocal = minThicknessWorld / per;
                    float delta = newLocal - size[a];
                    size[a] = newLocal;
                    center[a] += localDown[a] * delta * 0.5f;
                }
                if (size[a] < Epsilon) size[a] = Epsilon;
            }
        }

        private static int CountThinBoxes(GameObject root, Settings s, string genName)
        {
            int n = 0;
            var boxes = root.GetComponentsInChildren<BoxCollider>(includeInactive: true);
            foreach (var b in boxes)
            {
                if (b == null) continue;
                if (IsUnderName(b.transform, genName)) continue;
                if (IsThin(b, s.minThickness)) n++;
            }
            return n;
        }

        private static int RepairThinBoxes(GameObject root, Settings s, Transform genRoot)
        {
            int n = 0;
            var boxes = root.GetComponentsInChildren<BoxCollider>(includeInactive: true);
            foreach (var b in boxes)
            {
                if (b == null) continue;
                if (b.transform == genRoot || b.transform.IsChildOf(genRoot)) continue;
                if (!IsThin(b, s.minThickness)) continue;

                Undo.RecordObject(b, "Repair thin settlement collider");
                Vector3 worldPerLocal = AbsVec(b.transform.lossyScale);
                Vector3 localDown = b.transform.worldToLocalMatrix.MultiplyVector(Vector3.down);
                if (localDown.sqrMagnitude < Epsilon)
                    localDown = Vector3.down;
                else
                    localDown.Normalize();

                Vector3 center = b.center;
                Vector3 size = b.size;
                for (int a = 0; a < 3; a++)
                {
                    float per = Mathf.Abs(worldPerLocal[a]) < Epsilon ? 1f : Mathf.Abs(worldPerLocal[a]);
                    if (size[a] * per < s.minThickness)
                    {
                        float newLocal = s.minThickness / per;
                        float delta = newLocal - size[a];
                        size[a] = newLocal;
                        center[a] += localDown[a] * delta * 0.5f;
                    }
                }
                b.center = center;
                b.size = size;
                EditorUtility.SetDirty(b);
                n++;
            }
            if (n > 0)
                Debug.Log($"[SettlementColliders] Repair: утолщено тонких боксов: {n} (топ оставлен на месте).");
            return n;
        }

        private static bool IsThin(BoxCollider b, float minThicknessWorld)
        {
            Vector3 per = AbsVec(b.transform.lossyScale);
            for (int a = 0; a < 3; a++)
            {
                float p = Mathf.Abs(per[a]) < Epsilon ? 1f : Mathf.Abs(per[a]);
                if (b.size[a] * p < minThicknessWorld) return true;
            }
            return false;
        }

        private static bool IsUnderName(Transform t, string objectName)
        {
            while (t != null)
            {
                if (t.name == objectName) return true;
                t = t.parent;
            }
            return false;
        }

        private static string TopLevelChildName(Transform root, Transform source)
        {
            if (source == root) return "Root";
            Transform cur = source;
            while (cur != null && cur.parent != root)
                cur = cur.parent;
            return cur != null ? cur.name : "Root";
        }

        private static string[] SplitTokens(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return new string[0];
            var parts = csv.Split(new[] { ',', ';', ' ', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim().ToUpperInvariant();
            return parts;
        }

        private static bool MatchesAny(string upperName, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]) && upperName.Contains(tokens[i]))
                    return true;
            }
            return false;
        }

        private static float MaxComponent(Vector3 v)
        {
            return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
        }

        private static Vector3 AbsVec(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static Vector3 ScaledSize(Vector3 local, Vector3 scaleAbs)
        {
            return new Vector3(local.x * scaleAbs.x, local.y * scaleAbs.y, local.z * scaleAbs.z);
        }

        private static Vector3 Divide(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Abs(divisor.x) < Epsilon ? 1f : value.x / divisor.x,
                Mathf.Abs(divisor.y) < Epsilon ? 1f : value.y / divisor.y,
                Mathf.Abs(divisor.z) < Epsilon ? 1f : value.z / divisor.z);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Mesh";
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c == '/' || c == '\\' || c == ':' || c == ' ' || c == '.' || c == '-')
                    chars[i] = '_';
            }
            string result = new string(chars);
            return result.Length > 64 ? result.Substring(0, 64) : result;
        }
    }

    /// <summary>
    /// Окно: Tools → ProjectC → Build Settlement Colliders...
    /// Выдели корень FBX в Hierarchy, подбери пороги через Dry Run, жми Build.
    /// </summary>
    public class BuildSettlementCollidersWindow : EditorWindow
    {
        private BuildSettlementColliders.Settings settings = BuildSettlementColliders.DefaultSettings();

        [MenuItem("Tools/ProjectC/Build Settlement Colliders...")]
        public static void Open()
        {
            var w = GetWindow<BuildSettlementCollidersWindow>("Settlement Colliders");
            w.minSize = new Vector2(380f, 360f);
            w.Show();
        }

        private void OnGUI()
        {
            GameObject root = Selection.activeGameObject;

            EditorGUILayout.LabelField("Корень FBX в Hierarchy", EditorStyles.boldLabel);
            if (root == null)
            {
                EditorGUILayout.HelpBox("Выдели корневой GameObject поселения (например Project-C_primum_farm_x_1_2_GameReady), затем вернись сюда.", MessageType.Info);
            }
            else
            {
                int meshCount = root.GetComponentsInChildren<MeshFilter>(true).Length;
                EditorGUILayout.LabelField($"Корень: {root.name} (MeshFilter: {meshCount})");
            }

            EditorGUILayout.Space();
            settings.minWorldSize = EditorGUILayout.FloatField("Мин. размер меша (м)", settings.minWorldSize);
            settings.minThickness = EditorGUILayout.FloatField("Мин. толщина бокса (м)", settings.minThickness);
            EditorGUILayout.LabelField("Исключить по имени (через запятую)");
            settings.excludeTokens = EditorGUILayout.TextArea(settings.excludeTokens, GUILayout.MinHeight(54f));
            settings.groupByTopLevel = EditorGUILayout.Toggle("Группировать по зданиям", settings.groupByTopLevel);
            settings.skipInactive = EditorGUILayout.Toggle("Пропускать выключенные", settings.skipInactive);
            settings.repairThinBoxes = EditorGUILayout.Toggle("Чинить тонкие боксы", settings.repairThinBoxes);

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(root == null);
            if (GUILayout.Button("Dry Run (только посчитать)", GUILayout.Height(30f)))
            {
                var r = BuildSettlementColliders.DryRun(root, settings);
                Debug.Log($"[SettlementColliders] DRY '{root.name}': created={r.created}, " +
                          $"noMesh={r.skippedNoMesh}, disabled={r.skippedDisabled}, " +
                          $"name={r.skippedName}, tiny={r.skippedTiny}, thinToRepair={r.repaired}.");
            }
            if (GUILayout.Button("Build (построить коллайдеры)", GUILayout.Height(34f)))
            {
                BuildSettlementColliders.BuildFor(root, settings);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Бокс на каждый значимый меш. Мелочь (болты) режется размером и именами. " +
                "Тонкие палубы утолщаются вниз до мин. толщины — топ заподлицо, корабли не проваливаются. " +
                "Повторный Build пересобирает '<Root>_Colliders' с нуля.",
                MessageType.None);
        }
    }
}
