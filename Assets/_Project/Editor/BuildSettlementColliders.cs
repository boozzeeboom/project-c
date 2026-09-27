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
    ///   - include-режим (наоборот от исключений): поле includeTokens + отдельные кнопки
    ///     «только совпадения» — боксы только там, где имя содержит токен (напр. TABLE).
    ///     BuildAppend добавляет к существующей генерации, ничего не снося и не дублируя
    ///     (дедуп по суффиксу имени + позиции, 2 см); обычный Build по-прежнему пересобирает с нуля.
    ///   - Merge соседних боксов: склейка встык/внахлёст в пределах зазора mergeGap (м, world)
    ///     — из 1000 плиток пола делает 1 бокс; только внутри генерации и только одна
    ///     ориентация; режим MergeScope (раздельно / только горизонтальные / только
    ///     вертикальные / всё вместе) не даёт склеить пол со стеной в один бокс;
    ///     Dry Run показывает «было → стало» и добавленный воздух (м³).
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

            [Tooltip("Если не пусто — боксы строятся ТОЛЬКО для мешей, в имени которых есть один из токенов (через запятую, регистр не важен). Работает только через отдельные кнопки 'только совпадения'; обычный Build его игнорирует. Исключения excludeTokens внутри выборки тоже действуют.")]
            public string includeTokens = "";

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
            public int skippedInclude;
            public int skippedExists;
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
                      $"name={report.skippedName}, notIncluded={report.skippedInclude}, tiny={report.skippedTiny}), repaired={report.repaired}. " +
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

        /// <summary>Только посчитать в режиме добавления (без создания объектов).</summary>
        public static Report DryRunAppend(GameObject root, Settings s)
        {
            var report = new Report();
            if (root == null || s == null) return report;
            Transform existing = root.transform.Find(GeneratedSuffixRootName(root));
            Collect(root, s, null, report, true, null, existing, true);
            if (s.repairThinBoxes)
                report.repaired = CountThinBoxes(root, s, GeneratedSuffixRootName(root));
            return report;
        }

        /// <summary>
        /// Добавить боксы к существующей генерации, ничего не снося (наоборот от BuildFor,
        /// который пересобирает '<Root>_Colliders' с нуля). Повторный запуск идемпотентен:
        /// меши, для которых бокс уже есть (совпали имя + позиция), пропускаются (в логе alreadyExists).
        /// </summary>
        public static Report BuildAppend(GameObject root, Settings s)
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

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Append settlement colliders: {root.name}");

            // Корень генерации: переиспользовать существующий, а не сносить.
            Transform genRoot = root.transform.Find(genName);
            if (genRoot == null)
            {
                var go = new GameObject(genName);
                Undo.RegisterCreatedObjectUndo(go, "Append settlement colliders");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.layer = root.layer;
                go.isStatic = true;
                genRoot = go.transform;
            }

            // Переиспользовать уже созданные группы (иначе задвоятся одноимённые).
            var groups = new Dictionary<string, Transform>(StringComparer.Ordinal);
            if (s.groupByTopLevel)
                foreach (Transform child in genRoot)
                    if (child != null && !groups.ContainsKey(child.name))
                        groups[child.name] = child;

            Collect(root, s, genRoot, report, false, groups, genRoot, true, true);

            if (s.repairThinBoxes)
                report.repaired = RepairThinBoxes(root, s, genRoot);

            EditorUtility.SetDirty(genRoot);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = genRoot.gameObject;

            Debug.Log($"[SettlementColliders] APPEND '{root.name}': added={report.created}, " +
                      $"alreadyExists={report.skippedExists}, " +
                      $"skipped(noMesh={report.skippedNoMesh}, disabled={report.skippedDisabled}, " +
                      $"name={report.skippedName}, notIncluded={report.skippedInclude}, tiny={report.skippedTiny}), " +
                      $"repaired={report.repaired}. Иерархия: {genName}");
            return report;
        }

        private class ExistingHolder
        {
            public string name;
            public Vector3 pos;
            // Мировой OBB для теста «точка внутри» (нужен, чтобы append после Merge не дублировал:
            // склеенные MG_*-холдеры уже не несут суффикс исходного имени).
            public Vector3 obbCenter;
            public Vector3 obbAxis0, obbAxis1, obbAxis2;
            public Vector3 obbHalf;
        }

        private static List<ExistingHolder> CollectExistingHolders(Transform genRoot)
        {
            var list = new List<ExistingHolder>();
            var all = genRoot.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (var t in all)
            {
                if (t == null || t == genRoot) continue;
                var bc = t.GetComponent<BoxCollider>();
                if (bc == null) continue; // группы без боксов — не холдеры
                Quaternion r = t.rotation;
                Vector3 per = AbsVec(t.lossyScale);
                var e = new ExistingHolder
                {
                    name = t.name,
                    pos = t.position,
                    obbCenter = t.localToWorldMatrix.MultiplyPoint(bc.center),
                    obbAxis0 = r * Vector3.right,
                    obbAxis1 = r * Vector3.up,
                    obbAxis2 = r * Vector3.forward,
                    obbHalf = new Vector3(
                        Mathf.Abs(bc.size.x) * 0.5f * (Mathf.Abs(per.x) < Epsilon ? 1f : per.x),
                        Mathf.Abs(bc.size.y) * 0.5f * (Mathf.Abs(per.y) < Epsilon ? 1f : per.y),
                        Mathf.Abs(bc.size.z) * 0.5f * (Mathf.Abs(per.z) < Epsilon ? 1f : per.z))
                };
                list.Add(e);
            }
            return list;
        }

        /// <summary>Центр исходника внутри OBB холдера (грани ужаты на 1 см, чтобы тонкие боксы никого не «покрывали»).</summary>
        private static bool ObbContains(ExistingHolder e, Vector3 p)
        {
            const float shrink = 0.01f;
            Vector3 d = p - e.obbCenter;
            if (Mathf.Abs(Vector3.Dot(d, e.obbAxis0)) > e.obbHalf.x - shrink) return false;
            if (Mathf.Abs(Vector3.Dot(d, e.obbAxis1)) > e.obbHalf.y - shrink) return false;
            if (Mathf.Abs(Vector3.Dot(d, e.obbAxis2)) > e.obbHalf.z - shrink) return false;
            return true;
        }

        /// <summary>
        /// Бокс для исходника уже есть, если:
        ///   а) под генерацией лежит холдер с тем же суффиксом имени ('_'+имя исходника —
        ///      покрывает и старые BC_0000_Имя, и новые BC_HASH_Имя) примерно на той же позиции (2 см);
        ///   б) центр исходника внутри объёма существующего бокса (покрывает склеенные MG_*-боксы
        ///      после Merge; грани ужаты на 1 см, фильтры имён/размера при этом уже отработали выше,
        ///      так что чужая мелочь сюда не доходит).
        /// Позицию/объём сравниваем, т.к. одинаковые имена (два 'Table' в разных зданиях) встречаются.
        /// </summary>
        private static bool ExistsHolder(List<ExistingHolder> existing, GameObject source)
        {
            string suffix = "_" + SanitizeName(source.name);
            Vector3 p = source.transform.position;
            const float tolSq = 0.0004f; // (2 см)^2
            foreach (var e in existing)
            {
                if (!e.name.EndsWith(suffix, StringComparison.Ordinal)) continue;
                if ((e.pos - p).sqrMagnitude <= tolSq) return true;
            }
            foreach (var e in existing)
            {
                if (ObbContains(e, p)) return true;
            }
            return false;
        }

        /// <summary>Относительный путь исходника от корня ('Здание/Стол') — для стабильного имени холдера.</summary>
        private static string RelativePath(Transform root, Transform source)
        {
            if (source == root) return source.name;
            var parts = new List<string>(8);
            Transform cur = source;
            while (cur != null && cur != root)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        /// <summary>Детерминированный FNV-1a хэш строки (8 hex-символов). string.GetHashCode не годится — рандомизирован.</summary>
        private static string StableHash8(string value)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in value)
                {
                    h ^= c;
                    h *= 16777619u;
                }
                return h.ToString("X8");
            }
        }

        /// <summary>
        /// Стабильное имя холдера: один и тот же исходник всегда даёт одно имя,
        /// поэтому повторный append находит дубликат поиском по имени. Хэш пути различает
        /// одинаковые имена в разных ветках ('два Table' в разных зданиях).
        /// </summary>
        private static string HolderNameFor(Transform root, Transform source)
        {
            return "BC_" + StableHash8(RelativePath(root, source)) + "_" + SanitizeName(source.name);
        }

        // ================= MERGE соседних боксов =================
        //
        // Зачем: сегментированные поверхности (пол из 1000 плиток) дают по боксу на плитку.
        // Merge склеивает соседние боксы в один общий, игнорируя швы до mergeGap (м, world).
        // Правила слияния (консервативные — лишнего покрытия только на швах):
        //   - только внутри '<Root>_Colliders' (ручные боксы вне генерации не трогаем);
        //   - только одинаковая мировая ориентация (с допуском 0.5° на шум);
        //   - режим MergeScope: плиты (тонкая ось Y) и стены (тонкая ось X/Z) по умолч.
        //     сливаются только внутри своего класса; можно клеить только горизонтальные,
        //     только вертикальные или всё вместе;
        //   - вдоль оси слияния: зазор/нахлёст <= mergeGap; по двум другим осям грани
        //     совпадают с точностью align (иначе угол буквы Г не «зальём» воздухом).

        public class MergeReport
        {
            public int examined;   // боксов в скоупе
            public int before;     // == examined
            public int after;      // боксов после склейки
            public int clusters;   // сколько групп склеилось
            public float phantomM3; // добавленный «воздух» на швах (м³) — только положительная добавка
            public int skippedAxis; // отсеяно фильтром горизонтальные/вертикальные (не трогаем вообще)
            // Диагностика «почему не склеилось»:
            public int parents;      // разных родителей в скоупе
            public int oriGroups;    // (ориентация × класс) групп всего
            public int largestGroup; // размер самой большой группы
            public int lonely;       // боксов в группах < 2 (им не с кем сливаться)
            public string sample = ""; // пример ближайших соседей самой большой группы
        }

        /// <summary>
        /// Что клеить. Класс определяется тонкой осью бокса: Y — горизонтальный (плита пола),
        /// X/Z — вертикальный (стена). Split (по умолч.): плиты с плитами, стены со стенами.
        /// </summary>
        public enum MergeScope
        {
            SplitSlabsWalls = 0,
            HorizontalOnly = 1,
            VerticalOnly = 2,
            AllTogether = 3
        }

        private static string MergeScopeLabel(MergeScope s)
        {
            switch (s)
            {
                case MergeScope.HorizontalOnly: return "только горизонт";
                case MergeScope.VerticalOnly: return "только вертик";
                case MergeScope.AllTogether: return "всё вместе";
                default: return "плиты|стены";
            }
        }

        /// <summary>Допуск совпадения ориентаций при группировке (градусы). Шум float/импорта не должен раскидывать пол по одиночным группам.</summary>
        private const float MergeOriTolDeg = 0.5f;

        private class MBox
        {
            public Transform holder;
            public Vector3 center; // world
            public Vector3 half;   // world полуразмеры вдоль осей holder.rotation
        }

        private class MItem
        {
            public Vector3 min, max; // в координатах фрейма группы (отн. p0)
            public readonly List<MBox> members = new List<MBox>();
        }

        private class MergeCluster
        {
            public readonly List<MBox> members = new List<MBox>();
            public Vector3 p0, f0, f1, f2;
            public Quaternion rot;
            public Vector3 min, max;
            public Transform parent;
        }

        /// <summary>Только посчитать слияние (без изменения сцены).</summary>
        public static MergeReport DryRunMerge(GameObject root, float gapWorld, float alignWorld, string scopeCsv, MergeScope mergeScope)
        {
            var rep = new MergeReport();
            if (root == null) return rep;
            Transform gen = root.transform.Find(GeneratedSuffixRootName(root));
            if (gen == null) return rep;
            PlanMerge(gen, gapWorld, alignWorld, scopeCsv, mergeScope, rep);
            return rep;
        }

        /// <summary>
        /// Склеить соседние сгенерированные боксы. Идемпотентно: повторный запуск с тем же
        /// зазором почти ничего не меняет (склеенным боксам уже не с кем сливаться).
        /// </summary>
        public static MergeReport MergeBoxes(GameObject root, float gapWorld, float alignWorld, string scopeCsv, MergeScope mergeScope)
        {
            var rep = new MergeReport();
            if (root == null)
            {
                Debug.LogError("[SettlementColliders] Корень не задан.");
                return rep;
            }
            Transform gen = root.transform.Find(GeneratedSuffixRootName(root));
            if (gen == null)
            {
                Debug.LogError("[SettlementColliders] Merge: нет генерации '" +
                    GeneratedSuffixRootName(root) + "' — сначала Build.");
                return rep;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Merge settlement colliders: {root.name}");

            var clusters = PlanMerge(gen, gapWorld, alignWorld, scopeCsv, mergeScope, rep);
            foreach (var c in clusters)
                ApplyCluster(root, gen, c);

            EditorUtility.SetDirty(gen);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = gen.gameObject;

            Debug.Log($"[SettlementColliders] MERGE '{root.name}': {rep.before} → {rep.after} " +
                      $"(clusters={rep.clusters}, phantom={rep.phantomM3:F2} м³, " +
                      $"gap={Mathf.Max(0f, gapWorld):F2} м, align={Mathf.Max(0f, alignWorld):F3} м, " +
                      $"mode={MergeScopeLabel(mergeScope)}).");
            return rep;
        }

        private class OriGroup
        {
            public Quaternion rep;
            public int thinAxis; // -1 = без разделения; иначе 0/1/2 = тонкая ось боксов группы
            public readonly List<MBox> boxes = new List<MBox>();
        }

        /// <summary>
        /// Тонкая ось бокса в МИРОВЫХ осях (0=X, 1=Y, 2=Z): плиты тонкие по Y, стены по X/Z.
        /// Важно считать именно в мировых осях, а не в локальных осях холдера: у FBX-импорта
        /// рамка часто повёрнута (напр. rot 270,0,0 при скейле 150 — как ферма Primum),
        /// и локальный «тонкий Z» на деле является мировым Y (кейс S23_STORAGE_SHEET_TILE).
        /// </summary>
        private static int ThinAxis(MBox b)
        {
            Quaternion r = b.holder.rotation;
            Vector3 f0 = r * Vector3.right;
            Vector3 f1 = r * Vector3.up;
            Vector3 f2 = r * Vector3.forward;
            // Протяжённость бокса вдоль каждой мировой оси (точное развесовка по фрейму).
            float ex = Mathf.Abs(f0.x) * b.half.x + Mathf.Abs(f1.x) * b.half.y + Mathf.Abs(f2.x) * b.half.z;
            float ey = Mathf.Abs(f0.y) * b.half.x + Mathf.Abs(f1.y) * b.half.y + Mathf.Abs(f2.y) * b.half.z;
            float ez = Mathf.Abs(f0.z) * b.half.x + Mathf.Abs(f1.z) * b.half.y + Mathf.Abs(f2.z) * b.half.z;
            if (ex <= ey && ex <= ez) return 0;
            if (ey <= ez) return 1;
            return 2;
        }

        private static float BoxVolume(Vector3 min, Vector3 max)
        {
            Vector3 d = max - min;
            if (d.x <= 0f || d.y <= 0f || d.z <= 0f) return 0f;
            return d.x * d.y * d.z;
        }

        private static List<MergeCluster> PlanMerge(Transform gen, float gapWorld, float alignWorld, string scopeCsv, MergeScope mergeScope, MergeReport rep)
        {
            float gap = Mathf.Max(0f, gapWorld);
            float align = Mathf.Max(0f, alignWorld);
            string[] scope = SplitTokens(scopeCsv);
            var result = new List<MergeCluster>();

            // Собрать боксы генерации (группы без BoxCollider отсеются сами).
            // Фильтр горизонтальные/вертикальные: неподходящие не трогаем вообще (skippedAxis).
            var all = gen.GetComponentsInChildren<BoxCollider>(includeInactive: true);
            var boxes = new List<MBox>(all.Length);
            var thinOf = new List<int>(all.Length);
            foreach (var b in all)
            {
                if (b == null) continue;
                Transform h = b.transform;
                if (scope.Length > 0 && !MatchesAny(h.name.ToUpperInvariant(), scope)) continue;
                Vector3 per = AbsVec(h.lossyScale);
                var mb = new MBox
                {
                    holder = h,
                    center = h.localToWorldMatrix.MultiplyPoint(b.center),
                    half = new Vector3(
                        Mathf.Abs(b.size.x) * 0.5f * (Mathf.Abs(per.x) < Epsilon ? 1f : per.x),
                        Mathf.Abs(b.size.y) * 0.5f * (Mathf.Abs(per.y) < Epsilon ? 1f : per.y),
                        Mathf.Abs(b.size.z) * 0.5f * (Mathf.Abs(per.z) < Epsilon ? 1f : per.z))
                };
                int thin = ThinAxis(mb);
                if (mergeScope == MergeScope.HorizontalOnly && thin != 1) { rep.skippedAxis++; continue; }
                if (mergeScope == MergeScope.VerticalOnly && thin == 1) { rep.skippedAxis++; continue; }
                boxes.Add(mb);
                thinOf.Add(mergeScope == MergeScope.AllTogether ? -1 : thin);
            }

            rep.examined = boxes.Count;
            rep.before = boxes.Count;
            rep.after = boxes.Count;

            // Группировка: только ориентация (с допуском MergeOriTolDeg на шум), по всей
            // генерации сразу — FBX часто плоский (все меши прямо под корнем), и делить
            // склейку по родителям-группам тогда нельзя: каждая плитка оказалась бы одна.
            // Разные ориентации не сливаем никогда. Склеенный бокс кладём к родителю
            // первого участника.
            var oriGroups = new List<OriGroup>();
            var parentSet = new HashSet<Transform>();
            for (int i = 0; i < boxes.Count; i++)
            {
                MBox b = boxes[i];
                int thin = thinOf[i];
                parentSet.Add(b.holder.parent);
                Quaternion q = b.holder.rotation;
                OriGroup found = null;
                foreach (var g in oriGroups)
                {
                    if (g.thinAxis != thin) continue;
                    if (Quaternion.Angle(g.rep, q) <= MergeOriTolDeg) { found = g; break; }
                }
                if (found == null)
                {
                    found = new OriGroup { rep = q, thinAxis = thin };
                    oriGroups.Add(found);
                }
                found.boxes.Add(b);
            }

            rep.parents = parentSet.Count;
            List<MBox> biggest = null;
            foreach (var g in oriGroups)
            {
                if (biggest == null || g.boxes.Count > biggest.Count) biggest = g.boxes;
                if (g.boxes.Count > rep.largestGroup) rep.largestGroup = g.boxes.Count;
                if (g.boxes.Count < 2) rep.lonely += g.boxes.Count;
            }
            rep.oriGroups = oriGroups.Count;

            float phantom = 0f;
            int removed = 0;
            foreach (var g in oriGroups)
            {
                if (g.boxes.Count < 2) continue;
                var cluster = SweepGroup(g.boxes, gap, align, ref phantom);
                foreach (var c in cluster)
                {
                    if (c.members.Count < 2) continue;
                    result.Add(c);
                    removed += c.members.Count - 1;
                }
            }

            rep.clusters = result.Count;
            rep.after = rep.before - removed;
            rep.phantomM3 = phantom;
            if (result.Count == 0 && biggest != null && biggest.Count >= 2)
                rep.sample = NeighborSample(biggest);
            return result;
        }

        /// <summary>
        /// Диагностика для самой большой группы: берём первый бокс, ищем ближайшего соседа
        /// по +X и показываем зазор и несовпадение граней — видно, что мешает склейке.
        /// </summary>
        private static string NeighborSample(List<MBox> group)
        {
            Quaternion rot = group[0].holder.rotation;
            Vector3 f0 = rot * Vector3.right;
            Vector3 f1 = rot * Vector3.up;
            Vector3 f2 = rot * Vector3.forward;
            Vector3 p0 = group[0].center;

            Vector3 b0min = new Vector3(-group[0].half.x, -group[0].half.y, -group[0].half.z);
            Vector3 b0max = new Vector3(group[0].half.x, group[0].half.y, group[0].half.z);

            float bestGap = float.MaxValue;
            Vector3 bestMin = Vector3.zero, bestMax = Vector3.zero;
            string bestName = "";
            foreach (var b in group)
            {
                if (b == group[0]) continue;
                Vector3 d = b.center - p0;
                Vector3 omin = new Vector3(
                    Vector3.Dot(d, f0) - b.half.x,
                    Vector3.Dot(d, f1) - b.half.y,
                    Vector3.Dot(d, f2) - b.half.z);
                Vector3 omax = new Vector3(
                    Vector3.Dot(d, f0) + b.half.x,
                    Vector3.Dot(d, f1) + b.half.y,
                    Vector3.Dot(d, f2) + b.half.z);
                float gapX = omin.x - b0max.x;
                if (gapX < bestGap)
                {
                    bestGap = gapX;
                    bestMin = omin;
                    bestMax = omax;
                    bestName = b.holder.name;
                }
            }
            if (bestName == "") return "в группе один бокс";
            return string.Format("эталон vs '{0}': gapX={1:F3} м, dY=[{2:F3}, {3:F3}], dZ=[{4:F3}, {5:F3}] (м)",
                bestName, bestGap,
                bestMin.y - b0min.y, bestMax.y - b0max.y,
                bestMin.z - b0min.z, bestMax.z - b0max.z);
        }

        /// <summary>
        /// Жадное слияние группы одной ориентации: проходы по осям X/Y/Z со сканированием
        /// окна досягаемости и склейкой подходящих, пока счёт не перестанет уменьшаться.
        /// Внутри группы все боксы делят фрейм первого (та же матрица поворота —
        /// без перестановок осей и знаков).
        /// </summary>
        private static List<MergeCluster> SweepGroup(List<MBox> group, float gap, float align, ref float phantom)
        {
            Quaternion rot = group[0].holder.rotation;
            Vector3 f0 = rot * Vector3.right;
            Vector3 f1 = rot * Vector3.up;
            Vector3 f2 = rot * Vector3.forward;
            Vector3 p0 = group[0].center;

            var items = new List<MItem>(group.Count);
            foreach (var b in group)
            {
                float cx = Vector3.Dot(b.center - p0, f0);
                float cy = Vector3.Dot(b.center - p0, f1);
                float cz = Vector3.Dot(b.center - p0, f2);
                var it = new MItem();
                it.min = new Vector3(cx - b.half.x, cy - b.half.y, cz - b.half.z);
                it.max = new Vector3(cx + b.half.x, cy + b.half.y, cz + b.half.z);
                it.members.Add(b);
                items.Add(it);
            }

            bool changed = true;
            while (changed && items.Count > 1)
            {
                changed = false;
                for (int a = 0; a < 3 && items.Count > 1; a++)
                    items = MergePass(items, a, gap, align, ref phantom, ref changed);
            }

            var clusters = new List<MergeCluster>();
            foreach (var it in items)
            {
                if (it.members.Count < 2) continue;
                var c = new MergeCluster
                {
                    p0 = p0, f0 = f0, f1 = f1, f2 = f2, rot = rot,
                    min = it.min, max = it.max,
                    parent = it.members[0].holder.parent
                };
                c.members.AddRange(it.members);
                clusters.Add(c);
            }
            return clusters;
        }

        private static bool CanMerge(MItem a, MItem b, int axis, float gap, float align)
        {
            for (int j = 0; j < 3; j++)
            {
                if (j == axis) continue;
                if (Mathf.Abs(a.min[j] - b.min[j]) > align) return false;
                if (Mathf.Abs(a.max[j] - b.max[j]) > align) return false;
            }
            // b идёт после a по min[axis]: либо перекрытие, либо зазор в пределах gap.
            return b.min[axis] <= a.max[axis] + gap;
        }

        /// <summary>
        /// Один проход слияния вдоль оси. Для каждого бокса сканируем вперёд по сортировке
        /// всех, кто в пределах досягаемости (min &lt;= cur.max + gap), пропуская неподходящих:
        /// только соседи-подряд не годятся — ряды сетки чередуются (R01,R02,R01...), и жадное
        /// «подряд» тогда вообще ничего не клеит. Неподошедшие остаются живыми для своей очереди.
        /// </summary>
        private static List<MItem> MergePass(List<MItem> items, int axis, float gap, float align, ref float phantom, ref bool changed)
        {
            items.Sort((x, y) => x.min[axis].CompareTo(y.min[axis]));
            var consumed = new bool[items.Count];
            var next = new List<MItem>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (consumed[i]) continue;
                MItem cur = items[i];
                float curVol = BoxVolume(cur.min, cur.max);
                for (int j = i + 1; j < items.Count && items[j].min[axis] <= cur.max[axis] + gap; j++)
                {
                    if (consumed[j]) continue;
                    MItem cand = items[j];
                    if (!CanMerge(cur, cand, axis, gap, align)) continue;
                    Vector3 nMin = Vector3.Min(cur.min, cand.min);
                    Vector3 nMax = Vector3.Max(cur.max, cand.max);
                    // В отчёт — только положительный прирост («воздух» на швах), перекрытия дают 0.
                    phantom += Mathf.Max(0f, BoxVolume(nMin, nMax) - (curVol + BoxVolume(cand.min, cand.max)));
                    cur.min = nMin;
                    cur.max = nMax;
                    curVol = BoxVolume(nMin, nMax);
                    cur.members.AddRange(cand.members);
                    consumed[j] = true;
                    changed = true;
                }
                next.Add(cur);
            }
            return next;
        }

        private static void ApplyCluster(GameObject root, Transform gen, MergeCluster c)
        {
            Vector3 mid = (c.min + c.max) * 0.5f;
            Vector3 full = c.max - c.min;
            Vector3 worldCenter = c.p0 + c.f0 * mid.x + c.f1 * mid.y + c.f2 * mid.z;

            var names = new List<string>(c.members.Count);
            foreach (var m in c.members)
                names.Add(m.holder.name);
            names.Sort(StringComparer.Ordinal);
            string holderName = "MG_" + StableHash8(string.Join("+", names.ToArray())) + "_" + c.members.Count + "x";

            foreach (var m in c.members)
                Undo.DestroyObjectImmediate(m.holder.gameObject);

            var go = new GameObject(holderName);
            Undo.RegisterCreatedObjectUndo(go, "Merge settlement colliders");
            go.transform.SetParent(c.parent, false);
            go.layer = root.layer;
            go.isStatic = true;
            go.transform.position = worldCenter;
            go.transform.rotation = c.rot;
            go.transform.localScale = Vector3.one;

            // Точный center/size: 8 мировых углов объединённого OBB → локальные координаты холдера.
            var box = go.AddComponent<BoxCollider>();
            Vector3 h = full * 0.5f;
            Matrix4x4 w2l = go.transform.worldToLocalMatrix;
            Vector3 lMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 lMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 corner = worldCenter
                            + c.f0 * (sx * h.x) + c.f1 * (sy * h.y) + c.f2 * (sz * h.z);
                        Vector3 l = w2l.MultiplyPoint(corner);
                        lMin = Vector3.Min(lMin, l);
                        lMax = Vector3.Max(lMax, l);
                    }
            box.center = (lMin + lMax) * 0.5f;
            box.size = lMax - lMin;
            box.isTrigger = false;
            EditorUtility.SetDirty(go);
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
            Dictionary<string, Transform> groups = null,
            Transform existingRoot = null,
            bool skipExisting = false,
            bool stableNames = false)
        {
            string[] tokens = SplitTokens(s.excludeTokens);
            string[] include = SplitTokens(s.includeTokens);
            bool useInclude = include.Length > 0;

            List<ExistingHolder> existing = null;
            if (skipExisting && existingRoot != null)
                existing = CollectExistingHolders(existingRoot);

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

                // Include-фильтр (наоборот от исключений): если задан — берём только совпадения.
                if (useInclude && !MatchesAny(upper, include))
                {
                    report.skippedInclude++;
                    continue;
                }

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

                // Append-режим: уже закрытые меши не дублируем.
                if (skipExisting && existing != null && ExistsHolder(existing, go))
                {
                    report.skippedExists++;
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

                var holder = new GameObject(stableNames
                    ? HolderNameFor(root.transform, it.source.transform)
                    : $"BC_{index:0000}_{SanitizeName(it.source.name)}");
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
        private float mergeGap = 0.1f;
        private float mergeAlign = 0.02f;
        private string mergeTokens = "";
        private int mergeScopeIdx = 0;
        private static readonly string[] MergeScopeLabels =
        {
            "Плиты и стены (раздельно)",
            "Только горизонтальные",
            "Только вертикальные",
            "Всё вместе"
        };

        [MenuItem("Tools/ProjectC/Build Settlement Colliders...")]
        public static void Open()
        {
            var w = GetWindow<BuildSettlementCollidersWindow>("Settlement Colliders");
            w.minSize = new Vector2(380f, 660f);
            w.Show();
        }

        /// <summary>Копия настроек без include-фильтра — для обычного прохода по всем мешам.</summary>
        private static BuildSettlementColliders.Settings WithoutInclude(BuildSettlementColliders.Settings s)
        {
            return new BuildSettlementColliders.Settings
            {
                minWorldSize = s.minWorldSize,
                minThickness = s.minThickness,
                excludeTokens = s.excludeTokens,
                includeTokens = "",
                groupByTopLevel = s.groupByTopLevel,
                skipInactive = s.skipInactive,
                repairThinBoxes = s.repairThinBoxes
            };
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
                // Обычный режим: include-фильтр игнорируется, считаются все значимые меши.
                var r = BuildSettlementColliders.DryRun(root, WithoutInclude(settings));
                Debug.Log($"[SettlementColliders] DRY '{root.name}': created={r.created}, " +
                          $"noMesh={r.skippedNoMesh}, disabled={r.skippedDisabled}, " +
                          $"name={r.skippedName}, tiny={r.skippedTiny}, thinToRepair={r.repaired}.");
            }
            if (GUILayout.Button("Build (построить коллайдеры)", GUILayout.Height(34f)))
            {
                BuildSettlementColliders.BuildFor(root, WithoutInclude(settings));
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Только эти имена (через запятую)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Пусто = обычный режим выше. Напр.: TABLE, CHAIR — боксы только там, где имя содержит токен.", EditorStyles.miniLabel);
            settings.includeTokens = EditorGUILayout.TextArea(settings.includeTokens ?? "", GUILayout.MinHeight(36f));

            EditorGUI.BeginDisabledGroup(root == null || string.IsNullOrWhiteSpace(settings.includeTokens));
            if (GUILayout.Button("Dry Run (только совпадения)", GUILayout.Height(30f)))
            {
                var r = BuildSettlementColliders.DryRunAppend(root, settings);
                Debug.Log($"[SettlementColliders] DRY-APPEND '{root.name}' [{settings.includeTokens}]: toAdd={r.created}, " +
                          $"alreadyExists={r.skippedExists}, " +
                          $"noMesh={r.skippedNoMesh}, disabled={r.skippedDisabled}, " +
                          $"name={r.skippedName}, notIncluded={r.skippedInclude}, tiny={r.skippedTiny}, thinToRepair={r.repaired}.");
            }
            if (GUILayout.Button("Build (только совпадения, добавить)", GUILayout.Height(34f)))
            {
                BuildSettlementColliders.BuildAppend(root, settings);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Объединить соседние боксы", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Склеивает боксы встык/внахлёст в пределах зазора — из 1000 плиток пола делает 1. Только внутри генерации.", EditorStyles.miniLabel);
            mergeGap = EditorGUILayout.FloatField("Макс. зазор шва (м)", mergeGap);
            mergeAlign = EditorGUILayout.FloatField("Точность совпадения граней (м)", mergeAlign);
            mergeScopeIdx = EditorGUILayout.Popup("Что клеить", mergeScopeIdx, MergeScopeLabels);
            EditorGUILayout.LabelField("Только эти имена (пусто = все сгенерированные)");
            mergeTokens = EditorGUILayout.TextArea(mergeTokens ?? "", GUILayout.MinHeight(28f));

            EditorGUI.BeginDisabledGroup(root == null);
            if (GUILayout.Button("Dry Run (слияние)", GUILayout.Height(30f)))
            {
                var scope = (BuildSettlementColliders.MergeScope)mergeScopeIdx;
                var r = BuildSettlementColliders.DryRunMerge(root, mergeGap, mergeAlign, mergeTokens, scope);
                Debug.Log($"[SettlementColliders] DRY-MERGE '{root.name}': {r.before} → {r.after} " +
                          $"(clusters={r.clusters}, phantom={r.phantomM3:F2} м³, " +
                          $"gap={Mathf.Max(0f, mergeGap):F2} м, align={Mathf.Max(0f, mergeAlign):F3} м, " +
                          $"mode={MergeScopeLabels[mergeScopeIdx]}" +
                          (r.skippedAxis > 0 ? $", offAxis={r.skippedAxis}" : "") + "). " +
                          $"Группы: parents={r.parents}, oriGroups={r.oriGroups}, " +
                          $"largest={r.largestGroup}, lonely={r.lonely}."
                          + (string.IsNullOrEmpty(r.sample) ? "" : " Пример: " + r.sample));
            }
            if (GUILayout.Button("Merge (склеить соседние)", GUILayout.Height(34f)))
            {
                var scope = (BuildSettlementColliders.MergeScope)mergeScopeIdx;
                BuildSettlementColliders.MergeBoxes(root, mergeGap, mergeAlign, mergeTokens, scope);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Бокс на каждый значимый меш. Мелочь (болты) режется размером и именами. " +
                "Тонкие палубы утолщаются вниз до мин. толщины — топ заподлицо, корабли не проваливаются. " +
                "Повторный Build пересобирает '<Root>_Colliders' с нуля. " +
                "Режим 'только совпадения': в поле ниже укажи TABLE (или TABLE, CHAIR) — отдельный старт " +
                "ДОБАВИТ боксы только там, где имя содержит токен, существующие не трогает и не дублирует " +
                "(повторный запуск пропускает уже закрытые: alreadyExists); исключения сверху при этом тоже действуют.",
                MessageType.None);
        }
    }
}
