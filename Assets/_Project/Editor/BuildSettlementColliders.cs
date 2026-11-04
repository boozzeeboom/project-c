using System;
using System.Collections.Generic;
using Unity.Collections;
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
    ///   - некоробочные формы: круглые бары (токены capsuleTokens: PIPE/REBAR/TUBE) получают
    ///     CapsuleCollider вдоль длинной оси; кривые меши (токены sliceTokens) режутся slab'ами
    ///     с шагом sliceStep (сечение сканируется лучом по треугольникам, знак входа/выхода —
    ///     поэтому проёмы и полости внутри контура остаются ПУСТЫМИ — бокс не заливает дырку),
    ///     соседние куски с одинаковым сечением склеиваются в пределах sliceAdaptTol (прямые
    ///     участки — 1 бокс, сужения — детально, напр. нос Deck_Main_Bow); наклонные плоские
    ///     покрываются повёрнутыми боксами 1-в-1;
    ///   - наклонные серии нарезки: коробочный кусок, стоящий под углом к осям меша (стена под
    ///     углом в плане, скат), осевая нарезка превращает в «лесенку» из N кусков; подряд
    ///     идущие куски одного трека заменяются ОДНИМ повёрнутым боксом (ориентация — по
    ///     нормалям граней), если он не выступает за лесенку дальше допуска ни в одном slab'е;
    ///   - режим корабля (shipMode): без флага static — '<Root>_Colliders' едет с Rigidbody
    ///     как compound-коллайдер (для поселений static дешевле, не включать);
    ///   - точечная нарезка (BuildSlicesDirect): режет всё выделенное с шагом sliceStep,
    ///     имена копировать не надо — фильтры имён/размера игнорируются;
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

            [Tooltip("Подстроки имён (через запятую): круглые бары (трубы, арматура) получают CapsuleCollider вдоль длинной оси вместо бокса — точное прилегание без воздуха по углам. Только вытянутые (длина >= 2 диаметров), короткие цилиндры остаются боксами. Точна при uniform-скейле (фермы: 150 uniform — ок).")]
            public string capsuleTokens = "PIPE,REBAR,TUBE";

            [Tooltip("Подстроки имён (через запятую): кривые/гнутые меши (арки, гнутые листы, U-трубы) режутся на сегменты вдоль длинной оси. Внутри slab'а связные части идут отдельными боксами — полость U-трубы не заливается. Пусто = не резать.")]
            public string sliceTokens = "";

            [Tooltip("Длина куска нарезки (м, world) вдоль длинной оси — шаг измерения. Пустые куски (дыры в геометрии) и проёмы внутри контура пропускаются: куски строятся по фактическому веществу. Read/Write у меша не требуется.")]
            public float sliceStep = 1.0f;

            [Tooltip("Адаптив нарезки (м, world): соседние куски с сечением, совпадающим в пределах допуска, склеиваются в один (прямая корма — 1 бокс, сужение — детально). 0 — не склеивать, каждый непустой slab своим боксом.")]
            public float sliceAdaptTol = 0.05f;

            [Tooltip("Только осевые куски: не заменять наклонные серии нарезки повёрнутым боксом (прежнее поведение — стена под углом режется лесенкой с шагом).")]
            public bool sliceAxisOnly = false;

            [Tooltip("Допуск воздуха наклонных боксов (м, world): вместо лесенки мелких осевых кусков slab наклонной/скрученной детали кроется повёрнутым боксом, если лишний объём только по толщине (в среднем не больше допуска) и без пустых окон (проёмы не заливаются). 0 — только без лишнего объёма.")]
            public float sliceAirTol = 1.0f;

            [Tooltip("Режим корабля / движущегося объекта: холдеры, группы и корень генерации НЕ помечаются static, иерархия '<Root>_Colliders' едет вместе с Rigidbody как compound-коллайдер. Для поселений (неподвижных) оставить выключенным — static дешевле для PhysX.")]
            public bool shipMode = false;

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
            public int capsules;
            public int slicedMeshes;
            public int unreadSlices;
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
            // В режиме корабля — не static: едет с Rigidbody как compound.
            bool markStatic = !s.shipMode;
            var genRoot = new GameObject(genName);
            Undo.RegisterCreatedObjectUndo(genRoot, "Build settlement colliders");
            genRoot.transform.SetParent(root.transform, false);
            genRoot.transform.localPosition = Vector3.zero;
            genRoot.transform.localRotation = Quaternion.identity;
            genRoot.transform.localScale = Vector3.one;
            genRoot.layer = root.layer;
            genRoot.isStatic = markStatic;

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

            Debug.Log($"[SettlementColliders] '{root.name}': created={report.created}, capsules={report.capsules}, " +
                      $"slicedMeshes={report.slicedMeshes} (unread={report.unreadSlices}), " +
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

        /// <summary>
        /// Фактический трансформ генерации для Merge/Dry Run. После Build выделение уходит на
        /// сам сгенерированный корень ('<Root>_Colliders'), и если его же отдать в Merge, слепое
        /// 'root.name + _Colliders' ищет несуществующий 'ADM_DRUM_Colliders_Colliders'.
        /// Поэтому: сначала по имени, а если его нет и корень уже кончается на _Colliders —
        /// работаем с ним самим.
        /// </summary>
        private static Transform ResolveGeneratedRoot(GameObject root, out string genName)
        {
            genName = null;
            if (root == null) return null;
            genName = GeneratedSuffixRootName(root);
            Transform found = root.transform.Find(genName);
            if (found != null) return found;
            if (root.name.EndsWith(GeneratedSuffix, StringComparison.Ordinal))
            {
                genName = root.name;
                return root.transform;
            }
            // Выделена группа ВНУТРИ генерации (напр. 'Root' от groupByTopLevel) —
            // клеим внутри неё, а не ищем несуществующий 'Root_Colliders'.
            for (Transform p = root.transform.parent; p != null; p = p.parent)
            {
                if (p.name.EndsWith(GeneratedSuffix, StringComparison.Ordinal))
                {
                    genName = root.name;
                    return root.transform;
                }
            }
            return null;
        }

        /// <summary>Только посчитать в режиме добавления (без создания объектов).</summary>
        public static Report DryRunAppend(GameObject root, Settings s)
        {
            var report = new Report();
            if (root == null || s == null) return report;
            Transform existing = root.transform.Find(GeneratedSuffixRootName(root));
            Collect(root, s, null, report, true, null, existing, true, true);
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
            // Флаг static подтягиваем под текущий режим (могли переключить в/из shipMode).
            bool markStatic = !s.shipMode;
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
                go.isStatic = markStatic;
                genRoot = go.transform;
            }
            else if (genRoot.gameObject.isStatic != markStatic)
            {
                Undo.RecordObject(genRoot.gameObject, "Append settlement colliders");
                genRoot.gameObject.isStatic = markStatic;
            }

            // Переиспользовать уже созданные группы (иначе задвоятся одноимённые).
            var groups = new Dictionary<string, Transform>(StringComparer.Ordinal);
            if (s.groupByTopLevel)
                foreach (Transform child in genRoot)
                {
                    if (child == null || groups.ContainsKey(child.name)) continue;
                    groups[child.name] = child;
                    if (child.gameObject.isStatic != markStatic)
                    {
                        Undo.RecordObject(child.gameObject, "Append settlement colliders");
                        child.gameObject.isStatic = markStatic;
                    }
                }

            Collect(root, s, genRoot, report, false, groups, genRoot, true, true);

            if (s.repairThinBoxes)
                report.repaired = RepairThinBoxes(root, s, genRoot);

            EditorUtility.SetDirty(genRoot);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = genRoot.gameObject;

            Debug.Log($"[SettlementColliders] APPEND '{root.name}': added={report.created}, capsules={report.capsules}, " +
                      $"slicedMeshes={report.slicedMeshes}, " +
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
            // Капсульные холдеры: отрезок цилиндр. части + мировой радиус.
            public bool isCapsule;
            public Vector3 capA, capB;
            public float capR;
        }

        private static List<ExistingHolder> CollectExistingHolders(Transform genRoot)
        {
            var list = new List<ExistingHolder>();
            var all = genRoot.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (var t in all)
            {
                if (t == null || t == genRoot) continue;
                var bc = t.GetComponent<BoxCollider>();
                var cc = t.GetComponent<CapsuleCollider>();
                if (bc == null && cc == null) continue; // группы без коллайдеров — не холдеры
                Quaternion r = t.rotation;
                Vector3 per = AbsVec(t.lossyScale);
                var e = new ExistingHolder { name = t.name, pos = t.position };
                if (cc != null)
                {
                    Vector3 dir = r * DirVector(cc.direction);
                    float sDir = DirScale(per, cc.direction);
                    float sPerp = Mathf.Max(PerpScaleA(per, cc.direction), PerpScaleB(per, cc.direction));
                    if (sDir < Epsilon) sDir = 1f;
                    if (sPerp < Epsilon) sPerp = 1f;
                    Vector3 c = t.localToWorldMatrix.MultiplyPoint(cc.center);
                    float halfCyl = Mathf.Max(0f, cc.height * 0.5f - cc.radius) * sDir;
                    e.isCapsule = true;
                    e.capA = c - dir * halfCyl;
                    e.capB = c + dir * halfCyl;
                    e.capR = cc.radius * sPerp;
                }
                else
                {
                    e.obbCenter = t.localToWorldMatrix.MultiplyPoint(bc.center);
                    e.obbAxis0 = r * Vector3.right;
                    e.obbAxis1 = r * Vector3.up;
                    e.obbAxis2 = r * Vector3.forward;
                    e.obbHalf = new Vector3(
                        Mathf.Abs(bc.size.x) * 0.5f * (Mathf.Abs(per.x) < Epsilon ? 1f : per.x),
                        Mathf.Abs(bc.size.y) * 0.5f * (Mathf.Abs(per.y) < Epsilon ? 1f : per.y),
                        Mathf.Abs(bc.size.z) * 0.5f * (Mathf.Abs(per.z) < Epsilon ? 1f : per.z));
                }
                list.Add(e);
            }
            return list;
        }

        private static Vector3 DirVector(int dir)
        {
            if (dir == 0) return Vector3.right;
            if (dir == 1) return Vector3.up;
            return Vector3.forward;
        }

        private static float DirScale(Vector3 per, int dir)
        {
            if (dir == 0) return per.x;
            if (dir == 1) return per.y;
            return per.z;
        }

        private static float PerpScaleA(Vector3 per, int dir)
        {
            if (dir == 0) return per.y;
            return per.x;
        }

        private static float PerpScaleB(Vector3 per, int dir)
        {
            if (dir == 2) return per.y;
            return per.z;
        }

        /// <summary>Точка внутри капсулы (грани ужаты на 1 см, как у боксов).</summary>
        private static bool CapsuleContains(ExistingHolder e, Vector3 p)
        {
            Vector3 ab = e.capB - e.capA;
            float lenSq = ab.sqrMagnitude;
            float t = 0f;
            if (lenSq > Epsilon) t = Mathf.Clamp01(Vector3.Dot(p - e.capA, ab) / lenSq);
            Vector3 closest = e.capA + ab * t;
            float rr = e.capR - 0.01f;
            if (rr <= 0f) return false;
            return (p - closest).sqrMagnitude <= rr * rr;
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

        /// <summary>Центр исходника внутри склеенного MG_*-бокса (грани ужаты на 1 см).</summary>
        private static bool IsCoveredByMerged(List<ExistingHolder> existing, GameObject source)
        {
            Vector3 p = source.transform.position;
            foreach (var e in existing)
            {
                if (e.isCapsule) continue;
                if (!e.name.StartsWith("MG_", StringComparison.Ordinal)) continue;
                if (ObbContains(e, p)) return true;
            }
            return false;
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
                if (e.isCapsule)
                {
                    if (CapsuleContains(e, p)) return true;
                }
                else if (ObbContains(e, p)) return true;
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
            string genName;
            Transform gen = ResolveGeneratedRoot(root, out genName);
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
            string genName;
            Transform gen = ResolveGeneratedRoot(root, out genName);
            if (gen == null)
            {
                Debug.LogError("[SettlementColliders] Merge: нет генерации '" + genName +
                    "' — сначала Build.");
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
            go.isStatic = gen.gameObject.isStatic; // наследовать режим: static-поселение или корабль
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
            public int kind; // 0 = бокс, 1 = капсула, 2 = нарезанный на сегменты, 3 = повёрнутый бокс
            public List<SliceBox> slices; // только для kind == 2
            public SliceBox hull; // только для kind == 3
        }

        private class SliceBox
        {
            public int bin; // индекс бина в равномерной сетке — стабилен между запусками
            public int sub; // индекс связной компоненты внутри slab'а (0..)
            public bool multi; // в slab'е несколько компонент — к имени добавится суффикс Cn
            public Vector3 min, max; // mesh-local AABB куска
            // Наклонная серия: один повёрнутый бокс вместо лесенки (суффикс R в имени).
            public bool oriented;
            public Vector3 obbCenterLocal;      // центр в mesh-local
            public Vector3 obbAxisX, obbAxisY;  // оси бокса в масштабированном local (|lossyScale|), единичные
            public Vector3 obbSize;             // размеры в метрах
            // Геометрия повёрнутого куска slab'а (масштабированный local) — для склейки серий.
            public List<Vector3> gPts;
            public List<Vector3> gN;
            public List<float> gA;
        }

        /// <summary>Имя холдера куска: стабильно между запусками (бин + компонента).</summary>
        private static string SliceHolderName(string baseName, SliceBox sl)
        {
            string n = baseName + "_S" + sl.bin.ToString("00");
            if (sl.multi) n += "C" + sl.sub;
            if (sl.oriented) n += "R";
            return n;
        }

        /// <summary>
        /// Набор имён кусков совпал 1-в-1 с существующими? (смена шага/допуска даёт другое
        /// множество — тогда purge + пересборка при создании).
        /// </summary>
        private static bool SliceSetMatches(HashSet<string> existingNames, string sliceBase, List<SliceBox> slices)
        {
            if (existingNames == null || slices == null || slices.Count == 0) return false;
            string prefix = sliceBase + "_S";
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sl in slices)
                wanted.Add(SliceHolderName(sliceBase, sl));
            foreach (var w in wanted)
                if (!existingNames.Contains(w)) return false;
            foreach (var n in existingNames)
                if (n.StartsWith(prefix, StringComparison.Ordinal) && !wanted.Contains(n))
                    return false; // остались лишние старые куски — пересобрать
            return true;
        }

        /// <summary>
        /// Точечная нарезка: только посчитать для корня (без создания). Имена не нужны —
        /// режется всё выделенное (фильтры имён/размера игнорируются, это явный выбор).
        /// </summary>
        public static Report DryRunSlicesDirect(GameObject root, Settings s)
        {
            var report = new Report();
            if (root == null || s == null) return report;
            if (s.sliceStep <= 0f) return report;
            Transform existing = root.transform.Find(GeneratedSuffixRootName(root));
            List<ExistingHolder> holders = null;
            HashSet<string> existingNames = null;
            if (existing != null)
            {
                holders = CollectExistingHolders(existing);
                existingNames = new HashSet<string>(StringComparer.Ordinal);
                var allT = existing.GetComponentsInChildren<Transform>(includeInactive: true);
                foreach (var t in allT)
                    if (t != null && t != existing) existingNames.Add(t.name);
            }
            CollectSlicesDirect(root, s, null, report, true, holders, existingNames);
            return report;
        }

        /// <summary>
        /// Точечная нарезка выделенного: каждый меш под корнем режется slab'ами с шагом
        /// sliceStep (fallback — одиночный бокс). Добавляет к генерации, существующие куски
        /// не дублирует, старые сносит (purge). Фильтры имён/размера игнорируются.
        /// </summary>
        public static Report BuildSlicesDirect(GameObject root, Settings s)
        {
            var report = new Report();
            if (root == null)
            {
                Debug.LogError("[SettlementColliders] Корень не задан.");
                return report;
            }
            if (s == null) s = DefaultSettings();
            if (s.sliceStep <= 0f)
            {
                Debug.LogError("[SettlementColliders] Шаг нарезки должен быть > 0.");
                return report;
            }

            string genName = GeneratedSuffixRootName(root);
            bool markStatic = !s.shipMode;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Slice settlement: {root.name}");

            Transform genRoot = root.transform.Find(genName);
            if (genRoot == null)
            {
                var go = new GameObject(genName);
                Undo.RegisterCreatedObjectUndo(go, "Slice settlement");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.layer = root.layer;
                go.isStatic = markStatic;
                genRoot = go.transform;
            }
            else if (genRoot.gameObject.isStatic != markStatic)
            {
                Undo.RecordObject(genRoot.gameObject, "Slice settlement");
                genRoot.gameObject.isStatic = markStatic;
            }

            var groups = new Dictionary<string, Transform>(StringComparer.Ordinal);
            if (s.groupByTopLevel)
                foreach (Transform child in genRoot)
                {
                    if (child == null || groups.ContainsKey(child.name)) continue;
                    groups[child.name] = child;
                    if (child.gameObject.isStatic != markStatic)
                    {
                        Undo.RecordObject(child.gameObject, "Slice settlement");
                        child.gameObject.isStatic = markStatic;
                    }
                }

            List<ExistingHolder> holders = CollectExistingHolders(genRoot);
            var existingNames = new HashSet<string>(StringComparer.Ordinal);
            var allT = genRoot.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (var t in allT)
                if (t != null && t != genRoot) existingNames.Add(t.name);

            CollectSlicesDirect(root, s, genRoot, report, false, holders, existingNames, groups);

            EditorUtility.SetDirty(genRoot);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = genRoot.gameObject;

            Debug.Log($"[SettlementColliders] SLICE '{root.name}': boxes={report.created}, " +
                      $"slicedMeshes={report.slicedMeshes} (unread={report.unreadSlices}), " +
                      $"alreadyExists={report.skippedExists}, " +
                      $"skipped(noMesh={report.skippedNoMesh}, disabled={report.skippedDisabled}).");
            return report;
        }

        private static void CollectSlicesDirect(GameObject root, Settings s, Transform genRoot,
            Report report, bool dryOnly, List<ExistingHolder> holders, HashSet<string> existingNames,
            Dictionary<string, Transform> groups = null)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            foreach (var f in filters)
            {
                if (f == null) continue;
                GameObject go = f.gameObject;

                // Свою генерацию не трогаем.
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

                Vector3 worldPerLocal = AbsVec(go.transform.lossyScale);
                List<SliceBox> slices = null;
                // Read/Write у импортированных FBX обычно выключен — это не повод строить габаритный
                // бокс: данные всё равно читаются через MeshDataArray. dataUnread — только полностью
                // недоступный меш.
                slices = SlicePlanBest(f.sharedMesh, s.sliceStep,
                    Mathf.Max(0f, s.sliceAdaptTol), worldPerLocal, out bool unreadMesh, !s.sliceAxisOnly, Mathf.Max(0f, s.sliceAirTol));
                if (unreadMesh) report.unreadSlices++;

                string baseName = HolderNameFor(root.transform, go.transform);
                var it = new Item { source = go, mesh = f.sharedMesh, renderer = r };

                if (slices != null)
                {
                    if (holders != null && existingNames != null && SliceSetMatches(existingNames, baseName, slices))
                    {
                        report.skippedExists++;
                        continue;
                    }
                    if (dryOnly)
                    {
                        report.created += slices.Count;
                        report.slicedMeshes++;
                        continue;
                    }
                    Transform parent = ResolveGroup(root, genRoot, groups, s, go);
                    DestroyPredecessorHolders(genRoot, go, baseName);
                    foreach (var sl in slices)
                    {
                        AddSliceBox(root, parent, SliceHolderName(baseName, sl), it, sl, s.minThickness, !s.shipMode);
                        report.created++;
                    }
                    report.slicedMeshes++;
                }
                else
                {
                    if (holders != null && ExistsHolder(holders, go))
                    {
                        report.skippedExists++;
                        continue;
                    }
                    if (dryOnly)
                    {
                        report.created++;
                        continue;
                    }
                    SliceBox hull = s.sliceAxisOnly ? null : OrientedHullBox(f.sharedMesh, worldPerLocal);
                    if (hull != null && existingNames != null && existingNames.Contains(baseName))
                    {
                        report.skippedExists++;
                        continue;
                    }
                    Transform parent = ResolveGroup(root, genRoot, groups, s, go);
                    if (hull != null)
                    {
                        DestroyPredecessorHolders(genRoot, go, baseName);
                        AddObliqueBox(root, parent, baseName, it, hull, s.minThickness, !s.shipMode);
                    }
                    else
                        AddFittedBox(root, parent, baseName, it, f.sharedMesh.bounds, s.minThickness, !s.shipMode);
                    report.created++;
                }
            }
        }

        /// <summary>
        /// Нарезка по фильтрам — только посчитать. В отличие от DryRunSlicesDirect режет не «всё
        /// выделенное», а только меши, прошедшие настройки: includeTokens / excludeTokens /
        /// minWorldSize / capsuleTokens / sliceTokens.
        /// </summary>
        public static Report DryRunSlicesFiltered(GameObject root, Settings s)
        {
            var report = new Report();
            if (root == null || s == null) return report;
            CollectSlicesFiltered(root, s, report, true, FindOldGeneration(root));
            return report;
        }

        /// <summary>
        /// Нарезка по фильтрам — построить. Каждый холдер кладётся РЕБЁНКОМ своего исходного меша
        /// (identity local TRS), а не в '&lt;Root&gt;_Colliders'. Фильтры действуют как в обычном Build:
        /// исключения по именам, include-выборка, минимальный размер. capsuleTokens — капсула
        /// вместо нарезки; sliceTokens (если заданы) — режутся только совпадения, остальное
        /// одиночным боксом. Повторный запуск идемпотентен: холдеры этого меша сносятся заново.
        /// </summary>
        public static Report BuildSlicesFiltered(GameObject root, Settings s)
        {
            var report = new Report();
            if (root == null)
            {
                Debug.LogError("[SettlementColliders] Корень не задан.");
                return report;
            }
            if (s == null) s = DefaultSettings();
            if (s.sliceStep <= 0f)
            {
                Debug.LogError("[SettlementColliders] Шаг нарезки должен быть > 0.");
                return report;
            }
            if (root.name.EndsWith(GeneratedSuffix, StringComparison.Ordinal))
            {
                Debug.LogWarning($"[SettlementColliders] '{root.name}' — корень генерации, выбери исходник.");
                return report;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Slice by filter: {root.name}");

            CollectSlicesFiltered(root, s, report, false, FindOldGeneration(root));

            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[SettlementColliders] SLICE-FILTER '{root.name}': boxes={report.created}, " +
                      $"capsules={report.capsules}, slicedMeshes={report.slicedMeshes} (unread={report.unreadSlices}), " +
                      $"skipped(noMesh={report.skippedNoMesh}, disabled={report.skippedDisabled}, " +
                      $"name={report.skippedName}, notIncluded={report.skippedInclude}, tiny={report.skippedTiny}). " +
                      "Холдеры — дети исходных мешей.");
            if (report.skippedNameSample.Count > 0)
                Debug.Log("[SettlementColliders] Примеры пропущенных по имени: " +
                          string.Join(", ", report.skippedNameSample.ToArray()));
            return report;
        }

        /// <summary>Старая генерация '&lt;Root&gt;_Colliders' — её меши не трогаем.</summary>
        private static Transform FindOldGeneration(GameObject root)
        {
            return root == null ? null : root.transform.Find(GeneratedSuffixRootName(root));
        }

        /// <summary>
        /// Общий проход нарезки по фильтрам: обход всего поддерева корня, применение настроек и
        /// создание холдеров детьми исходных мешей (parent = трансформ меша).
        /// </summary>
        private static void CollectSlicesFiltered(GameObject root, Settings s, Report report, bool dryOnly, Transform skipSubtree)
        {
            string[] exclude = SplitTokens(s.excludeTokens);
            string[] include = SplitTokens(s.includeTokens);
            bool useInclude = include.Length > 0;
            string[] capsuleToks = SplitTokens(s.capsuleTokens);
            string[] sliceToks = SplitTokens(s.sliceTokens);
            bool markStatic = !s.shipMode;

            var filters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            foreach (var f in filters)
            {
                if (f == null) continue;
                GameObject go = f.gameObject;

                // Чужая (старая) генерация — не исходник.
                if (skipSubtree != null && (go == skipSubtree.gameObject || go.transform.IsChildOf(skipSubtree)))
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

                if (useInclude && !MatchesAny(upper, include))
                {
                    report.skippedInclude++;
                    continue;
                }

                if (MatchesAny(upper, exclude))
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

                Vector3 worldPerLocal = AbsVec(go.transform.lossyScale);
                var it = new Item { source = go, mesh = f.sharedMesh, renderer = r, worldSize = worldSize };
                string baseName = HolderNameFor(root.transform, go.transform);

                // Круглые бары — капсула вдоль длинной оси, нарезка им не нужна.
                if (ShouldCapsule(f.sharedMesh, worldPerLocal, capsuleToks, upper))
                {
                    if (dryOnly)
                    {
                        report.capsules++;
                        continue;
                    }
                    PurgeChildHolders(go.transform, baseName);
                    AddCapsule(root, go.transform, baseName, it, markStatic);
                    report.capsules++;
                    continue;
                }

                // Нарезка: sliceTokens пусто = режем всё принятое фильтром, иначе только совпадения.
                List<SliceBox> slices = null;
                if (sliceToks.Length == 0 || MatchesAny(upper, sliceToks))
                {
                    slices = SlicePlanBest(f.sharedMesh, s.sliceStep,
                        Mathf.Max(0f, s.sliceAdaptTol), worldPerLocal, out bool unreadMesh, !s.sliceAxisOnly, Mathf.Max(0f, s.sliceAirTol));
                    if (unreadMesh) report.unreadSlices++;
                }

                if (slices != null && slices.Count > 0)
                {
                    if (dryOnly)
                    {
                        report.created += slices.Count;
                        report.slicedMeshes++;
                        continue;
                    }
                    PurgeChildHolders(go.transform, baseName);
                    foreach (var sl in slices)
                    {
                        AddSliceBox(root, go.transform, SliceHolderName(baseName, sl), it, sl, s.minThickness, markStatic);
                        report.created++;
                    }
                    report.slicedMeshes++;
                }
                else
                {
                    // Меш без данных для нарезки (или не в sliceTokens) — честный одиночный бокс.
                    if (dryOnly)
                    {
                        report.created++;
                        continue;
                    }
                    PurgeChildHolders(go.transform, baseName);
                    SliceBox hull = s.sliceAxisOnly ? null : OrientedHullBox(f.sharedMesh, worldPerLocal);
                    if (hull != null)
                        AddObliqueBox(root, go.transform, baseName, it, hull, s.minThickness, markStatic);
                    else
                        AddFittedBox(root, go.transform, baseName, it, f.sharedMesh.bounds, s.minThickness, markStatic);
                    report.created++;
                }
            }
        }

        /// <summary>
        /// Снести холдеры-дети этого меша от прошлого запуска: одиночный бокс/капсулу по имени
        /// и куски '&lt;base&gt;_S##'. Чужие объекты под мешем не трогаем.
        /// </summary>
        private static void PurgeChildHolders(Transform source, string baseName)
        {
            if (source == null || string.IsNullOrEmpty(baseName)) return;
            string slicePrefix = baseName + "_S";
            var doomed = new List<Transform>(4);
            for (int i = 0; i < source.childCount; i++)
            {
                Transform c = source.GetChild(i);
                if (c == null) continue;
                if (c.name == UnitFrameName)
                {
                    // Наклонные куски лежат в рамке единичного масштаба под мешем.
                    for (int j = 0; j < c.childCount; j++)
                    {
                        Transform g = c.GetChild(j);
                        if (g != null && (g.name == baseName || g.name.StartsWith(slicePrefix, StringComparison.Ordinal)))
                            doomed.Add(g);
                    }
                    continue;
                }
                if (c.name == baseName || c.name.StartsWith(slicePrefix, StringComparison.Ordinal))
                    doomed.Add(c);
            }
            foreach (var t in doomed)
                if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        /// <summary>Группа для холдера (переиспользовать/создать), либо сам genRoot.</summary>
        private static Transform ResolveGroup(GameObject root, Transform genRoot,
            Dictionary<string, Transform> groups, Settings s, GameObject source)
        {
            Transform parent = genRoot;
            if (s.groupByTopLevel && groups != null)
            {
                string groupName = SanitizeName(TopLevelChildName(root.transform, source.transform));
                if (!groups.TryGetValue(groupName, out parent))
                {
                    var g = new GameObject(groupName);
                    Undo.RegisterCreatedObjectUndo(g, "Slice settlement group");
                    g.transform.SetParent(genRoot, false);
                    g.transform.localPosition = Vector3.zero;
                    g.transform.localRotation = Quaternion.identity;
                    g.transform.localScale = Vector3.one;
                    g.layer = root.layer;
                    g.isStatic = !s.shipMode;
                    parent = g.transform;
                    groups[groupName] = parent;
                }
            }
            return parent;
        }

        /// <summary>
        /// Круглый бар под капсулу? Только вытянутые вдоль одной оси (длина >= 2 диаметров
        /// в мире) — короткие цилиндры/диски точнее сидят в боксе.
        /// </summary>
        private static bool ShouldCapsule(Mesh mesh, Vector3 worldPerLocal, string[] capsuleToks, string upperName)
        {
            if (capsuleToks.Length == 0 || !MatchesAny(upperName, capsuleToks)) return false;
            Vector3 local = mesh.bounds.size;
            Vector3 world = new Vector3(local.x * worldPerLocal.x, local.y * worldPerLocal.y, local.z * worldPerLocal.z);
            float longest = Mathf.Max(world.x, Mathf.Max(world.y, world.z));
            float cross = Mathf.Min(world.x, Mathf.Min(world.y, world.z));
            return longest >= 2f * cross && cross > Epsilon;
        }

        /// <summary>
        /// Данные меша для нарезки. Read/Write у импортированных FBX обычно выключен, но в редакторе
        /// вершины и индексы всё равно доступны через MeshDataArray (нативная выборка, без копии
        /// в Mesh.vertices), поэтому флаг isReadable больше не запрещает нарезку.
        /// Индексы собираются по ВСЕМ треугольным сабмешам: Mesh.triangles отдаёт только нулевой.
        /// false — данных нет совсем (тогда нарезка невозможна и остаётся габаритный бокс).
        /// </summary>
        /// <summary>Данные меша общие с другими редакторскими инструментами (см. StairCollidersTool).</summary>
        internal static bool TryReadMeshData(Mesh mesh, out Vector3[] verts, out int[] tris)
        {
            verts = null;
            tris = null;
            if (mesh == null || mesh.vertexCount == 0) return false;

            if (mesh.isReadable)
            {
                try
                {
                    verts = mesh.vertices;
                    tris = mesh.triangles;
                }
                catch { verts = null; tris = null; }
                if (verts != null && verts.Length > 0)
                {
                    if (tris == null) tris = Array.Empty<int>();
                    return true;
                }
                verts = null;
                tris = null;
            }

            try
            {
                using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    if (data.Length == 0) return false;
                    Mesh.MeshData d = data[0];
                    int vc = d.vertexCount;
                    if (vc == 0) return false;

                    var vb = new NativeArray<Vector3>(vc, Allocator.Temp);
                    try { d.GetVertices(vb); verts = vb.ToArray(); }
                    finally { vb.Dispose(); }

                    int total = 0;
                    for (int s = 0; s < d.subMeshCount; s++)
                    {
                        var sm = d.GetSubMesh(s);
                        if (sm.topology == MeshTopology.Triangles) total += (int)sm.indexCount;
                    }

                    tris = new int[total];
                    int off = 0;
                    for (int s = 0; s < d.subMeshCount; s++)
                    {
                        var sm = d.GetSubMesh(s);
                        int cnt = (int)sm.indexCount;
                        if (sm.topology != MeshTopology.Triangles || cnt <= 0) continue;
                        var ib = new NativeArray<int>(cnt, Allocator.Temp);
                        try
                        {
                            d.GetIndices(ib, s);
                            Array.Copy(ib.ToArray(), 0, tris, off, cnt);
                        }
                        finally { ib.Dispose(); }
                        off += cnt;
                    }

                    return verts.Length > 0;
                }
            }
            catch
            {
                verts = null;
                tris = null;
                return false;
            }
        }

        /// <summary>
        /// Нарезать меш вдоль локальной оси slab'ами. Материал внутри slab'а ищется сканированием
        /// сечения: строки по оси c1, интервалы вещества по оси c2 (луч вдоль c2, знак пересечения
        /// по ориентации треугольника). Поэтому ПОЛОСТИ внутри контура (квадратные проёмы, окна,
        /// шахты, внутренние дворы) остаются пустыми — куски строятся по фактическому веществу,
        /// а не по габариту контура (раньше замкнутый контур вокруг проёма давал один сплошной
        /// бокс на всю деталь).
        /// Открытые/плоские меши и битые оболочки, где вещество не определяется (луч мимо, нет
        /// выхода из материала), падают на габарит сечения — прежнее поведение.
        /// Вдоль оси кусок растягивается на весь slab — куски стыкуются без щелей.
        /// Границы slab'ов берутся не только по равномерной сетке, но и по реальным координатам
        /// вершин вдоль оси: кромка проёма (её координаты есть в меше) иначе попадает ВНУТРЬ slab'а
        /// и кусок перекрывает проём на целый шаг (или, наоборот, оставляет полосу без коллайдера).
        /// Slab'ы без геометрии (проёмы) пропускаются. Null, если резать нечего.
        /// Бины и порядок компонент детерминированы — имена кусков стабильны.
        /// </summary>
        private static List<SliceBox> SlicePlan(Mesh mesh, int axis, float stepLocal, float tolWorld, Vector3 worldPerLocal, out bool dataUnread, bool allowOblique = true, float airTol = 0f)
        {
            dataUnread = false;
            if (mesh == null || mesh.vertexCount == 0) return null;
            if (axis < 0 || axis > 2) axis = 0;
            Bounds b = mesh.bounds;
            float len = b.size[axis];
            if (len <= Epsilon || stepLocal <= Epsilon) return null;
            if (Mathf.CeilToInt(len / stepLocal) <= 1) return null;

            Vector3[] verts;
            int[] tris;
            if (!TryReadMeshData(mesh, out verts, out tris))
            {
                dataUnread = true; // данных нет совсем — резать нечем
                return null;
            }
            return SlicePlanCore(verts, tris, b, axis, stepLocal, tolWorld, worldPerLocal, allowOblique, airTol);
        }

        /// <summary>
        /// Лучший план нарезки меша: осевой (по локальным осям меша) против нарезки в собственной
        /// рамке детали. Деталь, наклонённая сразу по двум осям (скат кровли, наклонное стекло),
        /// в осях меша даёт сетку из десятков кусков на каждый slab — в своей рамке это ряд
        /// повёрнутых полос. Берётся план с меньшим числом боксов; при равенстве — осевой.
        /// stepWorld — шаг в метрах.
        /// </summary>
        private static List<SliceBox> SlicePlanBest(Mesh mesh, float stepWorld, float tolWorld, Vector3 wpl,
            out bool dataUnread, bool allowOblique, float airTol)
        {
            dataUnread = false;
            if (mesh == null) return null;
            int longAxis = LongestAxis(mesh.bounds.size);
            float per = wpl[longAxis] < Epsilon ? 1f : wpl[longAxis];
            var best = SlicePlan(mesh, longAxis, stepWorld / per, tolWorld, wpl, out dataUnread, allowOblique, airTol);
            if (!allowOblique || dataUnread || stepWorld <= Epsilon) return best;
            if (best == null || best.Count <= 2) return best; // и так один-два бокса — рамки не помогут

            Vector3[] verts;
            int[] tris;
            if (!TryReadMeshData(mesh, out verts, out tris)) return best;
            if (TiltedFraction(verts, tris, wpl) < ObliqueMinTiltFrac) return best; // деталь в осях меша
            var frames = MeshFrames(verts, tris, wpl, mesh.vertexCount <= FrameTryVertLimit ? 3 : 1);
            if (frames.Count == 0) return best;

            int bestCount = best != null ? best.Count : int.MaxValue;
            var fv = new Vector3[verts.Length];
            foreach (var fr in frames)
            {
                Bounds fb = new Bounds();
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = Vector3.Scale(verts[i], wpl);
                    fv[i] = new Vector3(Vector3.Dot(p, fr.u), Vector3.Dot(p, fr.v), Vector3.Dot(p, fr.w));
                    if (i == 0) fb = new Bounds(fv[i], Vector3.zero); else fb.Encapsulate(fv[i]);
                }
                // Две длинные оси рамки: какая даст меньше боксов (трапеция кровли — поперёк).
                Vector3 sz = fb.size;
                int a0 = LongestAxis(sz);
                int a1 = -1;
                for (int a = 0; a < 3; a++)
                    if (a != a0 && (a1 < 0 || sz[a] > sz[a1])) a1 = a;
                foreach (int ax in new[] { a0, a1 })
                {
                    if (sz[ax] <= stepWorld) continue;
                    var plan = SlicePlanCore(fv, tris, fb, ax, stepWorld, tolWorld, Vector3.one, true, airTol);
                    if (plan == null || plan.Count >= bestCount) continue;
                    best = ToMeshFrame(plan, fr, wpl);
                    bestCount = plan.Count;
                }
            }
            return best;
        }

        private const int FrameTryVertLimit = 4000; // больше вершин — пробуем только одну рамку
        private const float ObliqueMinTiltFrac = 0.05f; // доля площади наклонных граней: меньше — без поворотов (фаски)

        /// <summary>Наклонная грань — нормаль не вдоль оси меша (больше ObliqueMinAngleDeg).</summary>
        private static bool IsTiltedNormal(Vector3 n)
        {
            return MaxAbsComp(n) < Mathf.Cos(ObliqueMinAngleDeg * Mathf.Deg2Rad);
        }

        /// <summary>Доля площади наклонных граней (масштабированный local). Деталь в осях меша — повороты не нужны.</summary>
        private static float TiltedFraction(Vector3[] verts, int[] tris, Vector3 wpl)
        {
            float total = 0f, tilt = 0f;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= verts.Length || ib >= verts.Length || ic >= verts.Length) continue;
                Vector3 a = Vector3.Scale(verts[ia], wpl);
                Vector3 n = Vector3.Cross(Vector3.Scale(verts[ib], wpl) - a, Vector3.Scale(verts[ic], wpl) - a);
                float ar = n.magnitude;
                if (ar < 1e-12f) continue;
                total += ar;
                if (IsTiltedNormal(n / ar)) tilt += ar;
            }
            return total > 0f ? tilt / total : 0f;
        }

        private struct MeshFrame
        {
            public Vector3 u, v, w; // ортонормированные оси в масштабированном local (|lossyScale|)
        }

        /// <summary>
        /// Собственные рамки детали по нормалям граней (площадь-взвешенные кластеры): n1 — главная
        /// грань, n2 — следующая неколлинеарная. Осевые (совпадающие с осями меша) отбрасываются.
        /// Сортировка — по объёму OBB, первая рамка — минимального объёма.
        /// </summary>
        private static List<MeshFrame> MeshFrames(Vector3[] verts, int[] tris, Vector3 wpl, int maxFrames)
        {
            var cd = new List<Vector3>(8);
            var ca = new List<float>(8);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= verts.Length || ib >= verts.Length || ic >= verts.Length) continue;
                Vector3 a = Vector3.Scale(verts[ia], wpl);
                Vector3 n = Vector3.Cross(Vector3.Scale(verts[ib], wpl) - a, Vector3.Scale(verts[ic], wpl) - a);
                float ar = n.magnitude * 0.5f;
                if (ar < 1e-12f) continue;
                AddNormalCluster(cd, ca, n.normalized, ar);
            }
            var res = new List<MeshFrame>();
            if (cd.Count == 0) return res;
            var order = new List<int>(cd.Count);
            for (int c = 0; c < cd.Count; c++) order.Add(c);
            order.Sort((x, y) => ca[y].CompareTo(ca[x]));

            var cand2 = new List<Vector3>(7);
            for (int q = 0; q < order.Count && q < 4; q++) cand2.Add(cd[order[q]].normalized);
            cand2.Add(Vector3.right);
            cand2.Add(Vector3.up);
            cand2.Add(Vector3.forward);

            float minCos = Mathf.Cos(ObliqueMinAngleDeg * Mathf.Deg2Rad);
            var vols = new List<float>();
            for (int a = 0; a < order.Count && a < 3; a++)
            {
                Vector3 n1 = cd[order[a]].normalized;
                foreach (var raw in cand2)
                {
                    Vector3 n2 = raw - n1 * Vector3.Dot(raw, n1);
                    if (n2.sqrMagnitude < 0.05f) continue;
                    n2.Normalize();
                    var fr = new MeshFrame { u = n1, v = n2, w = Vector3.Cross(n1, n2) };
                    if (MaxAbsComp(fr.u) >= minCos && MaxAbsComp(fr.v) >= minCos && MaxAbsComp(fr.w) >= minCos) continue;
                    bool dup = false;
                    foreach (var o in res)
                        if (SameFrame(o, fr)) { dup = true; break; }
                    if (dup) continue;
                    Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    Vector3 mx = -mn;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 p = Vector3.Scale(verts[i], wpl);
                        Vector3 f = new Vector3(Vector3.Dot(p, fr.u), Vector3.Dot(p, fr.v), Vector3.Dot(p, fr.w));
                        mn = Vector3.Min(mn, f);
                        mx = Vector3.Max(mx, f);
                    }
                    Vector3 d = mx - mn;
                    res.Add(fr);
                    vols.Add(d.x * d.y * d.z);
                }
            }
            // По объёму (простая вставка — кандидатов единицы).
            for (int i = 1; i < res.Count; i++)
                for (int j = i; j > 0 && vols[j] < vols[j - 1]; j--)
                {
                    float tv = vols[j]; vols[j] = vols[j - 1]; vols[j - 1] = tv;
                    var tf = res[j]; res[j] = res[j - 1]; res[j - 1] = tf;
                }
            if (res.Count > maxFrames) res.RemoveRange(maxFrames, res.Count - maxFrames);
            return res;
        }

        private static void AddNormalCluster(List<Vector3> cd, List<float> ca, Vector3 n, float a)
        {
            for (int c = 0; c < cd.Count; c++)
            {
                float dt = Vector3.Dot(n, cd[c].normalized);
                if (Mathf.Abs(dt) >= ObliqueNormalCos)
                {
                    cd[c] += (dt < 0f ? -n : n) * a;
                    ca[c] += a;
                    return;
                }
            }
            cd.Add(n * a);
            ca.Add(a);
        }

        /// <summary>Рамки совпадают с точностью до перестановки и знака осей.</summary>
        private static bool SameFrame(MeshFrame a, MeshFrame b)
        {
            Vector3[] x = { a.u, a.v, a.w };
            Vector3[] y = { b.u, b.v, b.w };
            foreach (var p in x)
            {
                bool hit = false;
                foreach (var q in y)
                    if (Mathf.Abs(Vector3.Dot(p, q)) >= ObliqueNormalCos) { hit = true; break; }
                if (!hit) return false;
            }
            return true;
        }

        /// <summary>Куски, посчитанные в рамке детали (метры), → повёрнутые куски в mesh-local.</summary>
        private static List<SliceBox> ToMeshFrame(List<SliceBox> plan, MeshFrame fr, Vector3 wpl)
        {
            var res = new List<SliceBox>(plan.Count);
            foreach (var sl in plan)
            {
                Vector3 cF, ax, ay, size;
                if (sl.oriented)
                {
                    cF = sl.obbCenterLocal;
                    ax = sl.obbAxisX;
                    ay = sl.obbAxisY;
                    size = sl.obbSize;
                }
                else
                {
                    cF = (sl.min + sl.max) * 0.5f;
                    ax = Vector3.right;
                    ay = Vector3.up;
                    size = sl.max - sl.min;
                }
                Vector3 cS = fr.u * cF.x + fr.v * cF.y + fr.w * cF.z;
                Vector3 axS = (fr.u * ax.x + fr.v * ax.y + fr.w * ax.z).normalized;
                Vector3 ayS = (fr.u * ay.x + fr.v * ay.y + fr.w * ay.z).normalized;
                Vector3 cL = new Vector3(
                    wpl.x < Epsilon ? cS.x : cS.x / wpl.x,
                    wpl.y < Epsilon ? cS.y : cS.y / wpl.y,
                    wpl.z < Epsilon ? cS.z : cS.z / wpl.z);
                res.Add(new SliceBox
                {
                    bin = sl.bin,
                    sub = sl.sub,
                    multi = sl.multi,
                    min = cL,
                    max = cL,
                    oriented = true,
                    obbCenterLocal = cL,
                    obbAxisX = axS,
                    obbAxisY = ayS,
                    obbSize = size
                });
            }
            return res;
        }

        /// <summary>
        /// Одиночный повёрнутый бокс по собственной рамке детали вместо AABB в осях меша — если
        /// деталь стоит под углом и рамка заметно (ObbGainMin) экономнее по объёму. Null —
        /// оставить обычный бокс (осевая деталь, нет данных или выигрыша).
        /// </summary>
        private static SliceBox OrientedHullBox(Mesh mesh, Vector3 wpl)
        {
            if (mesh == null || mesh.vertexCount == 0) return null;
            Vector3[] verts;
            int[] tris;
            if (!TryReadMeshData(mesh, out verts, out tris)) return null;
            if (TiltedFraction(verts, tris, wpl) < ObliqueMinTiltFrac) return null;
            var frames = MeshFrames(verts, tris, wpl, 1);
            if (frames.Count == 0) return null;
            MeshFrame fr = frames[0];

            Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mx = -mn;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = Vector3.Scale(verts[i], wpl);
                Vector3 f = new Vector3(Vector3.Dot(p, fr.u), Vector3.Dot(p, fr.v), Vector3.Dot(p, fr.w));
                mn = Vector3.Min(mn, f);
                mx = Vector3.Max(mx, f);
            }
            Vector3 d = mx - mn;
            Vector3 ab = Vector3.Scale(mesh.bounds.size, wpl);
            float volObb = d.x * d.y * d.z;
            float volAabb = ab.x * ab.y * ab.z;
            if (!(volObb < volAabb * (1f - ObbGainMin))) return null;

            var plan = new List<SliceBox>(1)
            {
                new SliceBox { bin = 0, min = mn, max = mx }
            };
            return ToMeshFrame(plan, fr, wpl)[0];
        }

        private const float ObbGainMin = 0.15f; // повёрнутый одиночный бокс — только если на 15 % меньше AABB
        private const float SlabObbSlack = 0.05f; // повёрнутый бокс slab'а не больше суммы осевых кусков +5 %

        private const int SlabSplitDepth = 3;        // до 8 повёрнутых боксов на slab
        private const float SlabSplitGain = 0.3f;    // делим, только если два бокса на 30 % меньше одного
        private const int SlabSplitCandidates = 24;  // позиций разреза на уровень
        private const int SlabSplitMaxPolys = 400;   // тяжелее — без деления (только один бокс)
        private const float SlabMinTiltFrac = 0.1f;  // slab без заметных наклонных граней — не поворачиваем

        /// <summary>Кусок грани внутри slab'а (масштабированный local): полигон и нормаль исходного треугольника.</summary>
        private class SPoly
        {
            public List<Vector3> p;
            public Vector3 n;
        }

        private class ObbFit
        {
            public Vector3 u, v, w, mn, mx;
            public float vol;
            public List<SPoly> polys;
        }

        private static float PolyArea(List<Vector3> p)
        {
            float ar = 0f;
            for (int q = 1; q + 1 < p.Count; q++)
                ar += Vector3.Cross(p[q] - p[0], p[q + 1] - p[0]).magnitude * 0.5f;
            return ar;
        }

        /// <summary>OBB минимального объёма по рамкам из нормалей граней (+ оси меша).</summary>
        private static ObbFit FitPolys(List<SPoly> polys)
        {
            var cd = new List<Vector3>(8);
            var ca = new List<float>(8);
            foreach (var sp in polys)
            {
                float ar = PolyArea(sp.p);
                if (ar > 0f) AddNormalCluster(cd, ca, sp.n, ar);
            }
            var order = new List<int>(cd.Count);
            for (int c = 0; c < cd.Count; c++) order.Add(c);
            order.Sort((x, y) => ca[y].CompareTo(ca[x]));
            var n1s = new List<Vector3>(3);
            for (int q = 0; q < order.Count && q < 2; q++) n1s.Add(cd[order[q]].normalized);
            n1s.Add(Vector3.right);
            var cand2 = new List<Vector3>(7);
            for (int q = 0; q < order.Count && q < 4; q++) cand2.Add(cd[order[q]].normalized);
            cand2.Add(Vector3.right);
            cand2.Add(Vector3.up);
            cand2.Add(Vector3.forward);

            ObbFit best = null;
            foreach (var n1 in n1s)
            {
                foreach (var raw in cand2)
                {
                    Vector3 n2 = raw - n1 * Vector3.Dot(raw, n1);
                    if (n2.sqrMagnitude < 0.05f) continue;
                    n2.Normalize();
                    Vector3 n3 = Vector3.Cross(n1, n2);
                    Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    Vector3 mx = -mn;
                    foreach (var sp in polys)
                        foreach (var p in sp.p)
                        {
                            Vector3 f = new Vector3(Vector3.Dot(p, n1), Vector3.Dot(p, n2), Vector3.Dot(p, n3));
                            mn = Vector3.Min(mn, f);
                            mx = Vector3.Max(mx, f);
                        }
                    if (mn.x > mx.x) continue;
                    Vector3 d = mx - mn;
                    float vol = d.x * d.y * d.z;
                    if (best == null || vol < best.vol)
                        best = new ObbFit { u = n1, v = n2, w = n3, mn = mn, mx = mx, vol = vol, polys = polys };
                }
            }
            return best;
        }

        /// <summary>Разрезать полигоны плоскостью dot(p, dir) = s.</summary>
        private static void SplitPolys(List<SPoly> polys, Vector3 dir, float s, List<SPoly> lo, List<SPoly> hi)
        {
            foreach (var sp in polys)
            {
                var a = ClipPolyPlane(sp.p, dir, s, true);
                var b = ClipPolyPlane(sp.p, dir, s, false);
                if (a.Count >= 3) lo.Add(new SPoly { p = a, n = sp.n });
                if (b.Count >= 3) hi.Add(new SPoly { p = b, n = sp.n });
            }
        }

        private static List<Vector3> ClipPolyPlane(List<Vector3> poly, Vector3 dir, float s, bool keepLess)
        {
            var out_ = new List<Vector3>(poly.Count + 1);
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 cur = poly[i], prev = poly[(i + n - 1) % n];
                float dc = Vector3.Dot(cur, dir) - s, dp = Vector3.Dot(prev, dir) - s;
                bool curIn = keepLess ? dc <= 0f : dc >= 0f;
                bool prevIn = keepLess ? dp <= 0f : dp >= 0f;
                if (curIn != prevIn)
                {
                    float t = Mathf.Abs(dc - dp) < 1e-12f ? 0f : dp / (dp - dc);
                    out_.Add(Vector3.Lerp(prev, cur, Mathf.Clamp01(t)));
                }
                if (curIn) out_.Add(cur);
            }
            return out_;
        }

        /// <summary>
        /// Рекурсивное деление геометрии slab'а на повёрнутые боксы: гнутая/скрученная
        /// пластина делится по сгибу (позиция разреза — на вершинах, вдоль длинной оси OBB),
        /// пока деление даёт выигрыш объёма SlabSplitGain.
        /// </summary>
        private static void SplitFit(List<SPoly> polys, int depth, List<ObbFit> outFits)
        {
            ObbFit parent = FitPolys(polys);
            if (parent == null) return;
            if (depth >= SlabSplitDepth || polys.Count > SlabSplitMaxPolys) { outFits.Add(parent); return; }

            Vector3 ext = parent.mx - parent.mn;
            Vector3 dir = parent.u;
            float lo = parent.mn.x, len = ext.x;
            if (ext.y >= ext.x && ext.y >= ext.z) { dir = parent.v; lo = parent.mn.y; len = ext.y; }
            else if (ext.z >= ext.x && ext.z >= ext.y) { dir = parent.w; lo = parent.mn.z; len = ext.z; }
            if (len <= Epsilon) { outFits.Add(parent); return; }

            var cuts = new List<float>(64);
            foreach (var sp in polys)
                foreach (var p in sp.p)
                {
                    float s = Vector3.Dot(p, dir);
                    if (s > lo + len * 0.05f && s < lo + len * 0.95f) cuts.Add(s);
                }
            cuts.Sort();
            var uniq = new List<float>(cuts.Count);
            foreach (float c in cuts)
                if (uniq.Count == 0 || c - uniq[uniq.Count - 1] > len * 1e-3f) uniq.Add(c);
            if (uniq.Count > SlabSplitCandidates)
            {
                var sampled = new List<float>(SlabSplitCandidates);
                for (int i = 0; i < SlabSplitCandidates; i++)
                    sampled.Add(uniq[i * (uniq.Count - 1) / (SlabSplitCandidates - 1)]);
                uniq = sampled;
            }
            for (int i = 1; i < 4; i++) uniq.Add(lo + len * i * 0.25f); // вершин может не быть внутри

            float bestCost = float.MaxValue;
            List<SPoly> bestL = null, bestR = null;
            foreach (float s in uniq)
            {
                var L = new List<SPoly>(polys.Count);
                var R = new List<SPoly>(polys.Count);
                SplitPolys(polys, dir, s, L, R);
                if (L.Count == 0 || R.Count == 0) continue;
                ObbFit fl = FitPolys(L), fr = FitPolys(R);
                if (fl == null || fr == null) continue;
                float cost = fl.vol + fr.vol;
                if (cost < bestCost) { bestCost = cost; bestL = L; bestR = R; }
            }
            if (bestL == null || bestCost > parent.vol * (1f - SlabSplitGain)) { outFits.Add(parent); return; }
            SplitFit(bestL, depth + 1, outFits);
            SplitFit(bestR, depth + 1, outFits);
        }

        /// <summary>
        /// Повёрнутые боксы на геометрию slab'а вместо осевых кусков pieces. Принимаются, если
        /// боксов меньше и объём не больше суммы кусков (+SlabObbSlack), либо боксов столько же, но
        /// объём меньше на ObbGainMin. Осевой результат (все рамки вдоль осей меша) — null.
        /// </summary>
        private static List<SliceBox> SlabObb(List<SPoly> polys, Vector3 wpl, List<SliceBox> pieces, int bin, float airTol)
        {
            float sum = 0f;
            foreach (var p in pieces)
            {
                Vector3 d = Vector3.Scale(p.max - p.min, wpl);
                sum += Mathf.Max(0f, d.x) * Mathf.Max(0f, d.y) * Mathf.Max(0f, d.z);
            }
            if (sum <= 0f || polys.Count == 0) return null;

            var fits = new List<ObbFit>(4);
            SplitFit(polys, 0, fits);
            if (fits.Count == 0) return null;
            float vol = 0f;
            bool anyOblique = false;
            float minCos = Mathf.Cos(ObliqueMinAngleDeg * Mathf.Deg2Rad);
            foreach (var f in fits)
            {
                vol += f.vol;
                if (MaxAbsComp(f.u) < minCos || MaxAbsComp(f.v) < minCos || MaxAbsComp(f.w) < minCos) anyOblique = true;
            }
            if (!anyOblique) return null;
            bool ok = fits.Count < pieces.Count
                ? vol <= sum * (1f + SlabObbSlack)
                : fits.Count == pieces.Count && vol <= sum * (1f - ObbGainMin);
            // Лесенка мелких кусков точнее по объёму, но их десятки: повёрнутый бокс допускается и с
            // лишним объёмом, если воздух только по толщине (в среднем ≤ airTol) и в плане бокса нет
            // пустых окон — проёмы и дыры так не заливаются.
            if (!ok && airTol > 0f && fits.Count * 2 <= pieces.Count)
            {
                ok = true;
                foreach (var f in fits)
                    if (!CoverageOk(f, airTol)) { ok = false; break; }
            }
            if (!ok) return null;

            var res = new List<SliceBox>(fits.Count);
            foreach (var f in fits)
            {
                Vector3 lmin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 lmax = -lmin;
                var gp = new List<Vector3>(16);
                var gn = new List<Vector3>(f.polys.Count);
                var ga = new List<float>(f.polys.Count);
                foreach (var sp in f.polys)
                {
                    foreach (var p in sp.p)
                    {
                        Vector3 l = new Vector3(
                            wpl.x < Epsilon ? p.x : p.x / wpl.x,
                            wpl.y < Epsilon ? p.y : p.y / wpl.y,
                            wpl.z < Epsilon ? p.z : p.z / wpl.z);
                        lmin = Vector3.Min(lmin, l);
                        lmax = Vector3.Max(lmax, l);
                    }
                    gp.AddRange(sp.p);
                    float ar = PolyArea(sp.p);
                    if (ar > 0f) { gn.Add(sp.n); ga.Add(ar); }
                }
                Vector3 midS = f.u * ((f.mn.x + f.mx.x) * 0.5f) + f.v * ((f.mn.y + f.mx.y) * 0.5f) + f.w * ((f.mn.z + f.mx.z) * 0.5f);
                res.Add(new SliceBox
                {
                    bin = bin,
                    min = lmin,
                    max = lmax,
                    oriented = true,
                    obbCenterLocal = new Vector3(
                        wpl.x < Epsilon ? midS.x : midS.x / wpl.x,
                        wpl.y < Epsilon ? midS.y : midS.y / wpl.y,
                        wpl.z < Epsilon ? midS.z : midS.z / wpl.z),
                    obbAxisX = f.u,
                    obbAxisY = f.v,
                    obbSize = f.mx - f.mn,
                    gPts = gp,
                    gN = gn,
                    gA = ga
                });
            }
            return res;
        }

        private const int CoverSamplesLong = 48;   // сэмплов вдоль длинной оси бокса
        private const int CoverSamplesMid = 5;     // вдоль средней

        /// <summary>
        /// Проверка повёрнутого бокса по столбикам вдоль его оси толщины (u — нормаль главной
        /// грани): столбик должен задевать геометрию. Пустой столбик внутри ряда (с материалом
        /// по обе стороны) = проём/дыра → нельзя. Пусто с краёв рядов (скошенная кромка) и воздух
        /// по толщине (толщина бокса минус размах попаданий) в среднем не больше airTol метра.
        /// </summary>
        private static bool CoverageOk(ObbFit f, float airTol)
        {
            Vector3 ext = f.mx - f.mn;
            Vector3[] ax = { f.u, f.v, f.w };
            const int t = 0;
            int l = 1, m = 2;
            if (ext[m] > ext[l]) { l = 2; m = 1; }
            Vector3 dt = ax[t], dl = ax[l], dm = ax[m];
            float thick = ext[t];
            if (ext[l] <= Epsilon || ext[m] <= Epsilon) return false;

            int nl = CoverSamplesLong, nm = CoverSamplesMid;
            float airSum = 0f;
            int hitCount = 0, endEmpty = 0;
            bool rowsSeen = false, gapRow = false;
            var hit = new bool[nl];
            for (int j = 0; j < nm; j++)
            {
                float sm = f.mn[m] + ext[m] * (j + 0.5f) / nm;
                for (int i = 0; i < nl; i++)
                {
                    float sl = f.mn[l] + ext[l] * (i + 0.5f) / nl;
                    Vector3 org = dl * sl + dm * sm + dt * (f.mn[t] - 1f);
                    float hMin = float.MaxValue, hMax = float.MinValue;
                    foreach (var sp in f.polys)
                    {
                        var p = sp.p;
                        for (int q = 1; q + 1 < p.Count; q++)
                        {
                            float dist;
                            if (!RayTriAxis(p[0], p[q], p[q + 1], org, dt, out dist)) continue;
                            if (dist < hMin) hMin = dist;
                            if (dist > hMax) hMax = dist;
                        }
                    }
                    hit[i] = hMin <= hMax;
                    if (hit[i])
                    {
                        airSum += Mathf.Max(0f, thick - (hMax - hMin));
                        hitCount++;
                    }
                }
                int first = -1, last = -1;
                for (int i = 0; i < nl; i++) if (hit[i]) { if (first < 0) first = i; last = i; }
                if (first < 0) { endEmpty += nl; if (rowsSeen) gapRow = true; continue; } // пустой ряд
                if (gapRow) return false;                                       // пустой ряд между заполненными — щель
                rowsSeen = true;
                for (int i = first; i <= last; i++) if (!hit[i]) return false;  // дыра внутри ряда
                endEmpty += first + (nl - 1 - last);
            }
            if (hitCount == 0) return false;
            // Пустота с краёв: средняя глубина по средней оси (площадь пустого / длина).
            float emptyDepth = endEmpty * (ext[m] / nm) / nl;
            if (emptyDepth > airTol) return false;
            return airSum / hitCount <= airTol;
        }

        private static List<SliceBox> SlicePlanCore(Vector3[] verts, int[] tris, Bounds b, int axis, float stepLocal,
            float tolWorld, Vector3 worldPerLocal, bool allowOblique, float airTol = 0f)
        {
            float len = b.size[axis];
            if (len <= Epsilon || stepLocal <= Epsilon) return null;
            int k = Mathf.CeilToInt(len / stepLocal);
            if (k <= 1) return null;
            if (k > 256) k = 256; // защита от миллиметрового шага на стометровке (корабли: 108 м / 0.5 = 216)
            float step = len / k; // нормируем: ровные slab'ы ровно покрывают bounds
            // Деталь в осях меша (наклонных граней практически нет) — повороты не ищем, это дорого.
            if (allowOblique && TiltedFraction(verts, tris, worldPerLocal) < ObliqueMinTiltFrac) allowOblique = false;

            int c1 = (axis + 1) % 3, c2 = (axis + 2) % 3;
            float minA = b.min[axis];
            float qeps = Mathf.Max(len, Epsilon) * 1e-4f; // квант склейки вершин/пересечений

            // Границы slab'ов: равномерная сетка + реальные координаты вершин по оси реза.
            // Кромки проёмов и уступов в меше всегда лежат на координатах вершин, поэтому такой
            // набор делает край проёма точным, а не «плюс-минус шаг». Если координат слишком
            // много (кривые меши), остаётся только равномерная сетка.
            var edges = new List<float>(Mathf.Min(k + verts.Length + 2, MaxSlabs + 2)) { minA };
            for (int i = 1; i < k; i++) edges.Add(minA + i * step);
            edges.Add(b.max[axis]);
            if (k + verts.Length + 2 <= MaxSlabs + 2)
            {
                for (int v = 0; v < verts.Length; v++) edges.Add(verts[v][axis]);
                edges.Sort();
                float gap = Mathf.Max(qeps, len * 5e-4f);
                int w = 1;
                for (int r = 1; r < edges.Count; r++)
                {
                    if (edges[r] < minA) continue;
                    if (edges[r] - edges[w - 1] <= gap) continue;
                    edges[w++] = edges[r];
                }
                edges.RemoveRange(w, edges.Count - w);
                bool hasMax = edges.Count > 0 && edges[edges.Count - 1] >= b.max[axis] - gap;
                if (hasMax) edges[edges.Count - 1] = b.max[axis];
                else edges.Add(b.max[axis]);
            }
            if (edges.Count <= 2) return null;

            // Треугольники с габаритами: по c1 — отсев строк, по оси нарезки — отсев slab'ов.
            int triCount = tris.Length / 3;
            var tA = new Vector3[triCount];
            var tB = new Vector3[triCount];
            var tC = new Vector3[triCount];
            var tC1Lo = new float[triCount];
            var tC1Hi = new float[triCount];
            var tALo = new float[triCount];
            var tAHi = new float[triCount];
            for (int t = 0; t < triCount; t++)
            {
                tC1Lo[t] = 1f; tC1Hi[t] = -1f; // маркер невалидного треугольника
                int ia = tris[t * 3], ib = tris[t * 3 + 1], ic = tris[t * 3 + 2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= verts.Length || ib >= verts.Length || ic >= verts.Length)
                    continue;
                Vector3 ta = verts[ia], tb = verts[ib], tc = verts[ic];
                tA[t] = ta; tB[t] = tb; tC[t] = tc;
                tC1Lo[t] = Mathf.Min(ta[c1], Mathf.Min(tb[c1], tc[c1]));
                tC1Hi[t] = Mathf.Max(ta[c1], Mathf.Max(tb[c1], tc[c1]));
                tALo[t] = Mathf.Min(ta[axis], Mathf.Min(tb[axis], tc[axis]));
                tAHi[t] = Mathf.Max(ta[axis], Mathf.Max(tb[axis], tc[axis]));
            }

            // Уникальные координаты c1 по вершинам: по ним ставятся строки (тонкая стенка тоньше
            // шага иначе проскочит между строками) и подтягиваются границы кусков к реальным граням.
            var raw = new List<float>(verts.Length);
            for (int v = 0; v < verts.Length; v++) raw.Add(verts[v][c1]);
            raw.Sort();
            var coords = new List<float>(raw.Count);
            for (int v = 0; v < raw.Count; v++)
                if (coords.Count == 0 || raw[v] - coords[coords.Count - 1] > qeps)
                    coords.Add(raw[v]);

            float tolC2 = worldPerLocal[c2] < Epsilon ? 1f : Mathf.Max(tolWorld, qeps * 8f) / worldPerLocal[c2];
            float rayFrom = b.min[c2] - Mathf.Max(1f, b.size[c2]); // луч начинается за габаритом

            var res = new List<SliceBox>();
            var pts = new List<Vector3>(96);
            var slabPolys = new List<SPoly>(32);
            var slabBoxes = new List<SliceBox>(8);
            var rows = new List<float>(64);
            var rowSpans = new List<List<Vector2>>(64);
            var cross = new List<Vector2>(32);

            for (int i = 0; i < edges.Count - 1; i++)
            {
                float lo = edges[i];
                float hi = edges[i + 1];
                float mid = (lo + hi) * 0.5f;

                // 1) Сечение slab'а (клиппинг треугольников) → точный габарит по c1/c2.
                pts.Clear();
                slabPolys.Clear();
                float slabArea = 0f, slabTilt = 0f;
                for (int t = 0; t < triCount; t++)
                {
                    if (tC1Lo[t] > tC1Hi[t]) continue;
                    if (tAHi[t] < lo - qeps || tALo[t] > hi + qeps) continue;
                    var poly = ClipTriToSlab(tA[t], tB[t], tC[t], axis, lo, hi);
                    if (poly.Count > 0) pts.AddRange(poly);
                    if (allowOblique && poly.Count >= 3)
                    {
                        Vector3 sa = Vector3.Scale(tA[t], worldPerLocal);
                        Vector3 nn = Vector3.Cross(Vector3.Scale(tB[t], worldPerLocal) - sa, Vector3.Scale(tC[t], worldPerLocal) - sa);
                        float nl = nn.magnitude;
                        if (nl > 1e-12f)
                        {
                            var sp = new List<Vector3>(poly.Count);
                            foreach (var pp in poly) sp.Add(Vector3.Scale(pp, worldPerLocal));
                            float ar = PolyArea(sp);
                            slabArea += ar;
                            if (IsTiltedNormal(nn / nl)) slabTilt += ar;
                            slabPolys.Add(new SPoly { p = sp, n = nn / nl });
                        }
                    }
                }
                if (pts.Count == 0) continue; // дыра в геометрии — куска нет, и это правильно

                float c1min = float.MaxValue, c1max = float.MinValue;
                float c2min = float.MaxValue, c2max = float.MinValue;
                foreach (var p in pts)
                {
                    if (p[c1] < c1min) c1min = p[c1];
                    if (p[c1] > c1max) c1max = p[c1];
                    if (p[c2] < c2min) c2min = p[c2];
                    if (p[c2] > c2max) c2max = p[c2];
                }

                slabBoxes.Clear();
                bool flat = (c1max - c1min) <= qeps || (c2max - c2min) <= qeps;
                if (!flat)
                {
                    BuildRows(coords, c1min, c1max, step, qeps, rows);

                    // 2) Интервалы вещества по c2 для каждой строки c1 (луч + знак входа/выхода).
                    rowSpans.Clear();
                    for (int r = 0; r < rows.Count; r++)
                    {
                        float u = rows[r];
                        Vector3 org = Vector3.zero, dir = Vector3.zero;
                        org[axis] = mid; org[c1] = u; org[c2] = rayFrom;
                        dir[c2] = 1f;
                        cross.Clear();
                        for (int t = 0; t < triCount; t++)
                        {
                            if (tC1Lo[t] > tC1Hi[t]) continue;
                            if (u < tC1Lo[t] - qeps || u > tC1Hi[t] + qeps) continue;
                            Vector3 nrm = Vector3.Cross(tB[t] - tA[t], tC[t] - tA[t]);
                            float nd = Vector3.Dot(nrm, dir);
                            if (nd > -1e-9f && nd < 1e-9f) continue; // грань параллельна лучу
                            float dist;
                            if (!RayTriAxis(tA[t], tB[t], tC[t], org, dir, out dist)) continue;
                            // x — координата вдоль c2, y — знак: +1 вход в материал, -1 выход.
                            cross.Add(new Vector2(rayFrom + dist, nd < 0f ? 1f : -1f));
                        }

                        var spans = new List<Vector2>(4);
                        if (cross.Count > 0)
                        {
                            cross.Sort((p, q) => p.x.CompareTo(q.x));
                            // Совпавшие пересечения суммируем ПО ЗНАКУ: внутренняя стенка союза
                            // боксов (выход одного + вход соседнего) взаимно гасится и не рвёт
                            // материал, а дублированная двусторонняя грань гасится целиком
                            // (такой меш уйдёт в габаритный фолбэк).
                            int depth = 0;
                            float startT = 0f;
                            int q = 0;
                            while (q < cross.Count)
                            {
                                float tv = cross[q].x;
                                int sgn = 0;
                                while (q < cross.Count && cross[q].x - tv <= qeps) { sgn += (int)cross[q].y; q++; }
                                if (sgn == 0) continue; // грани погасились — границы материала нет
                                bool wasIn = depth != 0;
                                depth += sgn;
                                bool isIn = depth != 0;
                                if (!wasIn && isIn) startT = tv;
                                else if (wasIn && !isIn && tv - startT > qeps) spans.Add(new Vector2(startT, tv));
                            }
                        }
                        rowSpans.Add(spans); // пустой список = полость на этой строке
                    }

                    // 3) Подряд идущие строки с одинаковым набором интервалов → куски.
                    int r0 = 0;
                    while (r0 < rows.Count)
                    {
                        var refSpans = rowSpans[r0];
                        int r1 = r0;
                        for (int q = r0 + 1; q < rows.Count; q++)
                        {
                            if (!SpansMatch(rowSpans[q], refSpans, tolC2)) break;
                            r1 = q;
                        }
                        if (refSpans.Count > 0)
                        {
                            float loC1 = (r0 == 0) ? c1min : RowBoundary(rows[r0 - 1], rows[r0], coords);
                            float hiC1 = (r1 == rows.Count - 1) ? c1max : RowBoundary(rows[r1], rows[r1 + 1], coords);
                            if (hiC1 - loC1 > qeps)
                            {
                                foreach (var sp in refSpans)
                                {
                                    if (sp.y - sp.x <= qeps) continue;
                                    slabBoxes.Add(MakeBox(axis, lo, hi, c1, loC1, hiC1, c2, sp.x, sp.y, i));
                                }
                            }
                        }
                        r0 = r1 + 1;
                    }
                }

                // Пустой или переусложнённый slab — габарит сечения (прежнее поведение).
                if (slabBoxes.Count == 0 || slabBoxes.Count > MaxRectsPerSlab)
                {
                    slabBoxes.Clear();
                    slabBoxes.Add(MakeBox(axis, lo, hi, c1, c1min, c1max, c2, c2min, c2max, i));
                }

                // Наклонный slab (скрученная/наклонная пластина): один повёрнутый бокс по
                // геометрии slab'а вместо сетки осевых кусков — если он не больше их суммы (проёмы
                // и разнесённые компоненты так не заливаются).
                if (allowOblique && slabPolys.Count > 0 && slabTilt >= SlabMinTiltFrac * slabArea)
                {
                    var ob = SlabObb(slabPolys, worldPerLocal, slabBoxes, i, airTol);
                    if (ob != null)
                    {
                        slabBoxes.Clear();
                        slabBoxes.AddRange(ob);
                    }
                }

                // Сортировка = стабильный порядок sub в именах кусков.
                slabBoxes.Sort((x, y) =>
                {
                    int c = x.min.x.CompareTo(y.min.x);
                    if (c != 0) return c;
                    c = x.min.y.CompareTo(y.min.y);
                    if (c != 0) return c;
                    return x.min.z.CompareTo(y.min.z);
                });
                bool multi = slabBoxes.Count > 1;
                for (int sgi = 0; sgi < slabBoxes.Count; sgi++)
                {
                    slabBoxes[sgi].sub = sgi;
                    slabBoxes[sgi].multi = multi;
                    res.Add(slabBoxes[sgi]);
                }
            }

            if (res.Count <= 1) return null;
            // Наклонные серии: лесенка из кусков, повторяющая коробочную деталь под углом к осям
            // меша, заменяется одним повёрнутым боксом. Остаток идёт в обычный адаптив.
            List<SliceBox> oblique = null;
            if (allowOblique && res.Count >= MinObliqueRun)
                res = ExtractObliqueRuns(res, axis, tA, tB, tC, tALo, tAHi, tC1Lo, tC1Hi, triCount,
                    worldPerLocal, qeps, step * worldPerLocal[axis], tolWorld, out oblique);
            // Адаптив: склеить подряд идущие куски с одинаковым сечением (прямые участки —
            // в 1 бокс, перегибы остаются подетально). Пустые бины разбивают серии (дыры не мостим).
            // Опорное сечение — первый кусок серии: суммарный дрейф ограничен допуском.
            if (tolWorld > 0f && res.Count > 1)
                res = CoalesceSlices(res, axis, tolWorld, worldPerLocal);
            if (oblique != null) res.AddRange(oblique);
            if (res.Count == 0) return null;
            if (res.Count == 1 && !res[0].oriented) return null; // один осевой кусок = габаритный бокс
            return res;
        }

        // ================= НАКЛОННЫЕ серии нарезки =================
        //
        // Осевая нарезка не умеет поворачивать куски: стена, стоящая под углом в плане, даёт
        // по куску на каждый шаг (лесенка). Здесь куски, которые идут подряд по бинам и
        // перекрываются по сечению (трек), заменяются одним OBB, если он:
        //   - заметно повёрнут относительно осей меша (осевые серии склеит CoalesceSlices);
        //   - по объёму не больше лесенки (+ObliqueVolSlack);
        //   - в каждом slab'е трека не выступает за фактическую геометрию slab'а больше допуска
        //     (max(ObliqueMinTol, 2·sliceAdaptTol) или ObliqueRelTol от размера куска — так
        //     проходит скошенный низ стены, но не «съедается» соседний прямой участок);
        //   - вдоль оси нарезки не вылезает за трек дальше полушага.
        // Ориентация — по нормалям граней (площадь-взвешенные кластеры), из кандидатов берётся
        // фрейм минимального объёма. Всё считается в масштабированном local (|lossyScale|), чтобы
        // повёрнутый бокс был прямоугольным и в мире при неравномерном масштабе.

        private const int MinObliqueRun = 3;          // короче — выигрыша нет
        private const float ObliqueMinTol = 0.1f;     // м, минимальный допуск выступа
        private const float ObliqueRelTol = 0.08f;    // доля размера куска (скошенный низ/верх)
        private const float ObliqueVolSlack = 0.10f;  // OBB не больше лесенки +10 % (скошенный низ/верх)
        private const float ObliqueMinAngleDeg = 1f;  // меньше — это осевая серия
        private const float ObliqueNormalCos = 0.9986f; // 3° — одна грань для кластера нормалей
        private const string UnitFrameName = "BC_UnitFrame";

        private class SlabGeom
        {
            public readonly List<Vector3> pts = new List<Vector3>(32);  // масштабированный local
            public readonly List<Vector3> nrm = new List<Vector3>(16);
            public readonly List<float> area = new List<float>(16);
            public Vector3 min, max; // AABB фактической геометрии slab'а, масштабированный local
            public float vol;        // объём куска лесенки (м³)
        }

        private static List<SliceBox> ExtractObliqueRuns(List<SliceBox> bins, int axis,
            Vector3[] tA, Vector3[] tB, Vector3[] tC, float[] tALo, float[] tAHi, float[] tC1Lo, float[] tC1Hi,
            int triCount, Vector3 wpl, float qeps, float stepWorld, float tolWorld, out List<SliceBox> oblique)
        {
            oblique = null;
            int c1 = (axis + 1) % 3, c2 = (axis + 2) % 3;

            // 1) Треки: кусок продлевает трек, если он в строго следующем бине и перекрывается
            // с последним куском трека по обеим осям сечения.
            var tracks = new List<List<SliceBox>>();
            var open = new List<List<SliceBox>>();
            foreach (var box in bins)
            {
                for (int r = open.Count - 1; r >= 0; r--)
                {
                    var o = open[r];
                    if (o[o.Count - 1].bin < box.bin - 1) { tracks.Add(o); open.RemoveAt(r); }
                }
                // Несколько претендентов (повёрнутые куски гнутой пластины перекрываются по AABB) —
                // берём трек с близкой ориентацией, затем с большим перекрытием.
                List<SliceBox> host = null;
                float hostScore = float.MinValue;
                foreach (var o in open)
                {
                    SliceBox last = o[o.Count - 1];
                    if (last.bin != box.bin - 1) continue;
                    if (!RangesOverlap(last, box, c1, qeps) || !RangesOverlap(last, box, c2, qeps)) continue;
                    float score = OverlapArea(last, box, c1, c2);
                    if (last.oriented && box.oriented)
                        score += 1e6f * Mathf.Abs(Vector3.Dot(Vector3.Cross(last.obbAxisX, last.obbAxisY),
                            Vector3.Cross(box.obbAxisX, box.obbAxisY)));
                    if (host == null || score > hostScore) { host = o; hostScore = score; }
                }
                if (host != null) host.Add(box);
                else open.Add(new List<SliceBox> { box });
            }
            tracks.AddRange(open);

            // 2) Жадно: от начала трека растим серию, пока OBB проходит проверки.
            float tolAbs = Mathf.Max(ObliqueMinTol, tolWorld * 2f);
            float axisTol = Mathf.Max(tolAbs, stepWorld * 0.5f);
            var replaced = new HashSet<SliceBox>();
            foreach (var tr in tracks)
            {
                if (tr.Count < MinObliqueRun) continue;
                var geo = new SlabGeom[tr.Count];
                for (int k = 0; k < tr.Count; k++)
                    geo[k] = GatherSlabGeom(tr[k], axis, c1, tA, tB, tC, tALo, tAHi, tC1Lo, tC1Hi, triCount, wpl, qeps,
                        Mathf.Max(tolAbs, stepWorld));

                int i = 0;
                while (i + MinObliqueRun <= tr.Count)
                {
                    SliceBox best = null;
                    int bestJ = -1;
                    for (int j = i + MinObliqueRun - 1; j < tr.Count; j++)
                    {
                        SliceBox fit = FitObliqueRun(tr, geo, i, j, axis, wpl, tolAbs, axisTol);
                        if (fit == null) break;
                        best = fit;
                        bestJ = j;
                    }
                    if (best == null) { i++; continue; }
                    if (oblique == null) oblique = new List<SliceBox>();
                    oblique.Add(best);
                    for (int k = i; k <= bestJ; k++) replaced.Add(tr[k]);
                    i = bestJ + 1;
                }
            }

            if (oblique == null) return bins;
            var rest = new List<SliceBox>(bins.Count);
            foreach (var b in bins)
                if (!replaced.Contains(b)) rest.Add(b);
            return rest;
        }

        private static float OverlapArea(SliceBox a, SliceBox b, int c1, int c2)
        {
            float o1 = Mathf.Max(0f, Mathf.Min(a.max[c1], b.max[c1]) - Mathf.Max(a.min[c1], b.min[c1]));
            float o2 = Mathf.Max(0f, Mathf.Min(a.max[c2], b.max[c2]) - Mathf.Max(a.min[c2], b.min[c2]));
            return o1 * o2;
        }

        private static bool RangesOverlap(SliceBox a, SliceBox b, int d, float eps)
        {
            return a.min[d] <= b.max[d] + eps && b.min[d] <= a.max[d] + eps;
        }

        /// <summary>
        /// Геометрия куска лесенки в масштабированном local: треугольники в границах slab'а по оси
        /// нарезки и в AABB куска, расширенном по сечению на marginWorld. Кусок лесенки строится
        /// по сечению в середине slab'а, поэтому у наклонной детали вещество у краёв slab'а
        /// выходит за его AABB; запас по сечению сохраняет эти края, но не захватывает
        /// дальние компоненты (соседняя ножка U-трубы).
        /// </summary>
        private static SlabGeom GatherSlabGeom(SliceBox m, int axis, int c1,
            Vector3[] tA, Vector3[] tB, Vector3[] tC, float[] tALo, float[] tAHi, float[] tC1Lo, float[] tC1Hi,
            int triCount, Vector3 wpl, float qeps, float marginWorld)
        {
            var g = new SlabGeom();
            Vector3 d = Vector3.Scale(m.max - m.min, wpl);
            g.vol = m.oriented
                ? m.obbSize.x * m.obbSize.y * m.obbSize.z
                : Mathf.Max(0f, d.x) * Mathf.Max(0f, d.y) * Mathf.Max(0f, d.z);
            if (m.gPts != null && m.gPts.Count > 0)
            {
                // Повёрнутый кусок делённого slab'а: своя геометрия, без соседних кусков.
                g.pts.AddRange(m.gPts);
                g.nrm.AddRange(m.gN);
                g.area.AddRange(m.gA);
                g.min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                g.max = -g.min;
                foreach (var p in m.gPts) { g.min = Vector3.Min(g.min, p); g.max = Vector3.Max(g.max, p); }
                return g;
            }

            Vector3 lo = m.min, hi = m.max;
            for (int a = 0; a < 3; a++)
            {
                if (a == axis) continue;
                float mg = wpl[a] < Epsilon ? marginWorld : marginWorld / wpl[a];
                lo[a] -= mg;
                hi[a] += mg;
            }

            g.min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            g.max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int t = 0; t < triCount; t++)
            {
                if (tC1Lo[t] > tC1Hi[t]) continue;
                if (tAHi[t] < lo[axis] - qeps || tALo[t] > hi[axis] + qeps) continue;
                if (tC1Hi[t] < lo[c1] || tC1Lo[t] > hi[c1]) continue;
                var poly = new List<Vector3>(6) { tA[t], tB[t], tC[t] };
                for (int a = 0; a < 3 && poly.Count > 0; a++)
                {
                    float e = a == axis ? 0f : qeps;
                    poly = ClipPolyAxis(poly, a, hi[a] + e, true);
                    if (poly.Count == 0) break;
                    poly = ClipPolyAxis(poly, a, lo[a] - e, false);
                }
                if (poly.Count < 3) continue;

                for (int q = 0; q < poly.Count; q++) poly[q] = Vector3.Scale(poly[q], wpl);
                Vector3 n = Vector3.Cross(Vector3.Scale(tB[t] - tA[t], wpl), Vector3.Scale(tC[t] - tA[t], wpl));
                float nl = n.magnitude;
                if (nl < 1e-12f) continue;
                n /= nl;
                float ar = 0f;
                for (int q = 1; q + 1 < poly.Count; q++)
                    ar += Vector3.Cross(poly[q] - poly[0], poly[q + 1] - poly[0]).magnitude * 0.5f;
                g.pts.AddRange(poly);
                foreach (var p in poly) { g.min = Vector3.Min(g.min, p); g.max = Vector3.Max(g.max, p); }
                if (ar > 0f)
                {
                    g.nrm.Add(n);
                    g.area.Add(ar);
                }
            }
            if (g.pts.Count == 0)
            {
                g.min = Vector3.Scale(m.min, wpl);
                g.max = Vector3.Scale(m.max, wpl);
            }
            return g;
        }

        /// <summary>OBB для кусков tr[i..j] либо null, если повёрнутый бокс хуже лесенки.</summary>
        private static SliceBox FitObliqueRun(List<SliceBox> tr, SlabGeom[] geo, int i, int j, int axis,
            Vector3 wpl, float tolAbs, float axisTol)
        {
            int c1 = (axis + 1) % 3, c2 = (axis + 2) % 3;

            // Кластеры нормалей (без знака), площадь-взвешенные.
            var cd = new List<Vector3>(8);
            var ca = new List<float>(8);
            float vStair = 0f;
            int npts = 0;
            for (int k = i; k <= j; k++)
            {
                SlabGeom g = geo[k];
                vStair += g.vol;
                npts += g.pts.Count;
                for (int q = 0; q < g.nrm.Count; q++)
                {
                    Vector3 n = g.nrm[q];
                    float a = g.area[q];
                    int hit = -1;
                    for (int c = 0; c < cd.Count; c++)
                    {
                        float dt = Vector3.Dot(n, cd[c].normalized);
                        if (Mathf.Abs(dt) >= ObliqueNormalCos)
                        {
                            hit = c;
                            if (dt < 0f) n = -n;
                            break;
                        }
                    }
                    if (hit < 0) { cd.Add(n * a); ca.Add(a); }
                    else { cd[hit] += n * a; ca[hit] += a; }
                }
            }
            if (npts < 4 || cd.Count == 0) return null;

            var order = new List<int>(cd.Count);
            for (int c = 0; c < cd.Count; c++) order.Add(c);
            order.Sort((x, y) => ca[y].CompareTo(ca[x]));

            var cand2 = new List<Vector3>(7);
            for (int q = 0; q < order.Count && q < 4; q++) cand2.Add(cd[order[q]].normalized);
            cand2.Add(Vector3.right);
            cand2.Add(Vector3.up);
            cand2.Add(Vector3.forward);

            float bestVol = float.MaxValue;
            Vector3 bu = Vector3.right, bv = Vector3.up, bw = Vector3.forward;
            Vector3 bmin = Vector3.zero, bmax = Vector3.zero;
            for (int a = 0; a < order.Count && a < 2; a++)
            {
                Vector3 n1 = cd[order[a]].normalized;
                foreach (var raw in cand2)
                {
                    Vector3 n2 = raw - n1 * Vector3.Dot(raw, n1);
                    if (n2.sqrMagnitude < 0.05f) continue; // почти параллельно n1
                    n2.Normalize();
                    Vector3 n3 = Vector3.Cross(n1, n2);
                    Vector3 mn, mx;
                    ProjectRange(geo, i, j, n1, n2, n3, out mn, out mx);
                    Vector3 d = mx - mn;
                    float vol = d.x * d.y * d.z;
                    if (vol < bestVol)
                    {
                        bestVol = vol;
                        bu = n1; bv = n2; bw = n3;
                        bmin = mn; bmax = mx;
                    }
                }
            }
            if (bestVol == float.MaxValue) return null;

            // Осевой фрейм — работа CoalesceSlices, тут не трогаем.
            float minCos = Mathf.Cos(ObliqueMinAngleDeg * Mathf.Deg2Rad);
            if (MaxAbsComp(bu) >= minCos && MaxAbsComp(bv) >= minCos && MaxAbsComp(bw) >= minCos) return null;
            if (bestVol > vStair * (1f + ObliqueVolSlack) + 1e-6f) return null;

            var corners = new Vector3[8];
            for (int idx = 0; idx < 8; idx++)
            {
                corners[idx] = bu * ((idx & 4) != 0 ? bmax.x : bmin.x)
                             + bv * ((idx & 2) != 0 ? bmax.y : bmin.y)
                             + bw * ((idx & 1) != 0 ? bmax.z : bmin.z);
            }

            // Вдоль оси нарезки — не дальше полушага за трек.
            float aLo = float.MaxValue, aHi = float.MinValue;
            foreach (var c in corners)
            {
                if (c[axis] < aLo) aLo = c[axis];
                if (c[axis] > aHi) aHi = c[axis];
            }
            if (geo[i].min[axis] - aLo > axisTol || aHi - geo[j].max[axis] > axisTol) return null;

            // В каждом slab'е — не выступать за кусок лесенки больше допуска.
            for (int k = i; k <= j; k++)
            {
                SlabGeom g = geo[k];
                Vector3 smin, smax;
                if (!ObbSlabRange(corners, axis, g.min[axis], g.max[axis], out smin, out smax)) return null;
                if (!WithinAllow(g, smin, smax, c1, tolAbs) || !WithinAllow(g, smin, smax, c2, tolAbs)) return null;
            }

            Vector3 midS = bu * ((bmin.x + bmax.x) * 0.5f)
                         + bv * ((bmin.y + bmax.y) * 0.5f)
                         + bw * ((bmin.z + bmax.z) * 0.5f);
            var res = new SliceBox
            {
                bin = tr[i].bin,
                sub = tr[i].sub,
                multi = tr[i].multi,
                min = tr[i].min,
                max = tr[i].max,
                oriented = true,
                obbCenterLocal = new Vector3(
                    wpl.x < Epsilon ? midS.x : midS.x / wpl.x,
                    wpl.y < Epsilon ? midS.y : midS.y / wpl.y,
                    wpl.z < Epsilon ? midS.z : midS.z / wpl.z),
                obbAxisX = bu,
                obbAxisY = bv,
                obbSize = bmax - bmin
            };
            for (int k = i + 1; k <= j; k++)
            {
                res.min = Vector3.Min(res.min, tr[k].min);
                res.max = Vector3.Max(res.max, tr[k].max);
            }
            return res;
        }

        private static bool WithinAllow(SlabGeom g, Vector3 smin, Vector3 smax, int d, float tolAbs)
        {
            float allow = Mathf.Max(tolAbs, ObliqueRelTol * (g.max[d] - g.min[d]));
            return g.min[d] - smin[d] <= allow && smax[d] - g.max[d] <= allow;
        }

        private static float MaxAbsComp(Vector3 v)
        {
            return Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
        }

        private static void ProjectRange(SlabGeom[] geo, int i, int j, Vector3 u, Vector3 v, Vector3 w,
            out Vector3 mn, out Vector3 mx)
        {
            mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int k = i; k <= j; k++)
            {
                var pts = geo[k].pts;
                for (int q = 0; q < pts.Count; q++)
                {
                    Vector3 p = pts[q];
                    float x = Vector3.Dot(p, u), y = Vector3.Dot(p, v), z = Vector3.Dot(p, w);
                    if (x < mn.x) mn.x = x; if (x > mx.x) mx.x = x;
                    if (y < mn.y) mn.y = y; if (y > mx.y) mx.y = y;
                    if (z < mn.z) mn.z = z; if (z > mx.z) mx.z = z;
                }
            }
        }

        /// <summary>AABB части OBB между плоскостями lo..hi по оси нарезки (углы + пересечения 12 рёбер).</summary>
        private static bool ObbSlabRange(Vector3[] corners, int axis, float lo, float hi, out Vector3 mn, out Vector3 mx)
        {
            mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;
            for (int idx = 0; idx < 8; idx++)
            {
                Vector3 c = corners[idx];
                if (c[axis] >= lo && c[axis] <= hi) { mn = Vector3.Min(mn, c); mx = Vector3.Max(mx, c); any = true; }
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    if ((idx & bit) != 0) continue;
                    Vector3 o = corners[idx | bit];
                    for (int pl = 0; pl < 2; pl++)
                    {
                        float v = pl == 0 ? lo : hi;
                        float da = c[axis] - v, db = o[axis] - v;
                        if (da * db >= 0f) continue;
                        Vector3 x = Vector3.Lerp(c, o, da / (da - db));
                        mn = Vector3.Min(mn, x);
                        mx = Vector3.Max(mx, x);
                        any = true;
                    }
                }
            }
            return any;
        }

        /// <summary>
        /// Адаптив: склеить куски с одинаковым сечением в серии. Каждый кусок ищет СВОЮ
        /// открытую серию (совпадение сечения с эталоном + строго следующий бин), а не соседа
        /// по списку: ножки U-трубы лежат вперемешку [L,R,L,R...] и соседним сравнением
        /// никогда не склеились бы. Пустые бины разбивают серии (дыры не мостим).
        /// Эталон сечения — первый кусок серии: суммарный дрейф ограничен допуском.
        /// </summary>
        private static List<SliceBox> CoalesceSlices(List<SliceBox> bins, int axis, float tolWorld, Vector3 worldPerLocal)
        {
            int c1 = (axis + 1) % 3, c2 = (axis + 2) % 3;
            float t1 = worldPerLocal[c1] < Epsilon ? 1f : tolWorld / worldPerLocal[c1];
            float t2 = worldPerLocal[c2] < Epsilon ? 1f : tolWorld / worldPerLocal[c2];
            var out_ = new List<SliceBox>();
            var open = new List<SliceBox>();
            var openLastBin = new List<int>();
            var openRefMin = new List<Vector3>();
            var openRefMax = new List<Vector3>();
            foreach (var box in bins) // bins идут по возрастанию bin (построение послабово)
            {
                if (box.oriented) { out_.Add(box); continue; } // повёрнутые куски — не по AABB
                bool extended = false;
                for (int r = 0; r < open.Count; r++)
                {
                    if (openLastBin[r] != box.bin - 1) continue; // только строго следующий бин
                    if (!CrossMatch(box, openRefMin[r], openRefMax[r], c1, c2, t1, t2)) continue;
                    SliceBox run = open[r];
                    run.min = Vector3.Min(run.min, box.min);
                    run.max = Vector3.Max(run.max, box.max);
                    openLastBin[r] = box.bin;
                    extended = true;
                    break;
                }
                if (!extended)
                {
                    // Серии, которым уже не продлиться (разрыв бинов), — в вывод.
                    for (int r = open.Count - 1; r >= 0; r--)
                    {
                        if (openLastBin[r] >= box.bin - 1) continue;
                        out_.Add(open[r]);
                        open.RemoveAt(r);
                        openLastBin.RemoveAt(r);
                        openRefMin.RemoveAt(r);
                        openRefMax.RemoveAt(r);
                    }
                    open.Add(box);
                    openLastBin.Add(box.bin);
                    openRefMin.Add(box.min);
                    openRefMax.Add(box.max);
                }
            }
            foreach (var run in open) out_.Add(run);
            return out_;
        }

        private static bool CrossMatch(SliceBox box, Vector3 refMin, Vector3 refMax,
            int c1, int c2, float t1, float t2)
        {
            return Mathf.Abs(box.min[c1] - refMin[c1]) <= t1
                && Mathf.Abs(box.max[c1] - refMax[c1]) <= t1
                && Mathf.Abs(box.min[c2] - refMin[c2]) <= t2
                && Mathf.Abs(box.max[c2] - refMax[c2]) <= t2;
        }

        // Предохранители сканирования сечения: кривой меш не должен плодить коллайдеры.
        private const int MaxRectsPerSlab = 16;   // полос в slab'е больше — габаритный бокс
        private const int MaxSlabs = 384;         // больше slab'ов (сетка + координаты вершин) — только сетка
        private const int MaxRowsFromCoords = 16; // сколько граней c1 добавлять строками
        private const int MaxRows = 64;           // предел строк на slab

        /// <summary>Кусок: диапазон по оси нарезки + интервал по c1 + интервал по c2.</summary>
        private static SliceBox MakeBox(int axis, float aLo, float aHi,
            int c1, float c1Lo, float c1Hi, int c2, float c2Lo, float c2Hi, int bin)
        {
            Vector3 mn = Vector3.zero, mx = Vector3.zero;
            mn[axis] = aLo; mx[axis] = aHi;
            mn[c1] = c1Lo; mx[c1] = c1Hi;
            mn[c2] = c2Lo; mx[c2] = c2Hi;
            return new SliceBox { bin = bin, sub = 0, multi = false, min = mn, max = mx };
        }

        /// <summary>
        /// Пересечение треугольника с лучом (o, d) — Мёллер–Трумбор. t — расстояние вдоль d
        /// (только впереди луча). Промах/параллельность плоскости → false.
        /// </summary>
        private static bool RayTriAxis(Vector3 a, Vector3 vb, Vector3 vc, Vector3 o, Vector3 d, out float t)
        {
            t = 0f;
            Vector3 e1 = vb - a, e2 = vc - a;
            Vector3 p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (det > -1e-9f && det < 1e-9f) return false; // луч параллелен плоскости
            float inv = 1f / det;
            Vector3 tv = o - a;
            float uu = Vector3.Dot(tv, p) * inv;
            if (uu < 0f || uu > 1f) return false;
            Vector3 q = Vector3.Cross(tv, e1);
            float vv = Vector3.Dot(d, q) * inv;
            if (vv < 0f || uu + vv > 1f) return false;
            float dist = Vector3.Dot(e2, q) * inv;
            if (dist <= 1e-6f) return false;
            t = dist;
            return true;
        }

        /// <summary>
        /// Строки сканирования по c1: равномерная сетка с шагом нарезки + фактические координаты
        /// граней со сдвигом внутрь (стенка тоньше шага иначе проскочит между строками).
        /// Переполнение (плотная сетка вершин) → остаётся только равномерная сетка.
        /// </summary>
        private static void BuildRows(List<float> coords, float lo, float hi, float step, float qeps, List<float> outRows)
        {
            outRows.Clear();
            float span = hi - lo;
            if (span <= qeps) return;
            int n = Mathf.Clamp(Mathf.CeilToInt(span / step), 1, MaxRows);
            float jitter = Mathf.Clamp(span * 1e-3f, qeps * 4f, 0.25f * step);

            int added = 0;
            for (int i = 0; i < coords.Count && added < MaxRowsFromCoords; i++)
            {
                float c = coords[i];
                if (c <= lo + qeps || c >= hi - qeps) continue;
                outRows.Add(c - jitter);
                outRows.Add(c + jitter);
                added++;
            }
            for (int j = 0; j < n; j++)
                outRows.Add(lo + (j + 0.5f) * span / n);

            if (outRows.Count > MaxRows)
            {
                outRows.Clear();
                for (int j = 0; j < n; j++)
                    outRows.Add(lo + (j + 0.5f) * span / n);
                return;
            }
            outRows.Sort();
            int w = 1;
            for (int i = 1; i < outRows.Count; i++)
                if (outRows[i] - outRows[w - 1] > qeps) outRows[w++] = outRows[i];
            if (w < outRows.Count) outRows.RemoveRange(w, outRows.Count - w);
        }

        /// <summary>
        /// Граница между двумя строками: если между ними лежит реальная грань — она (точные
        /// стенки проёмов), иначе середина интервала.
        /// </summary>
        private static float RowBoundary(float a, float b, List<float> coords)
        {
            float mid = (a + b) * 0.5f;
            int i = coords.BinarySearch(mid);
            if (i < 0) i = ~i;
            float best = mid, bestD = float.MaxValue;
            int from = Mathf.Max(0, i - 8), to = Mathf.Min(coords.Count, i + 8);
            for (int q = from; q < to; q++)
            {
                float c = coords[q];
                if (c <= a || c >= b) continue;
                float d = Mathf.Abs(c - mid);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>Совпадают ли наборы интервалов у двух строк (в пределах допуска).</summary>
        private static bool SpansMatch(List<Vector2> a, List<Vector2> b, float tol)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (Mathf.Abs(a[i].x - b[i].x) > tol || Mathf.Abs(a[i].y - b[i].y) > tol) return false;
            return true;
        }

        /// <summary>Клиппинг треугольника к slab'у [lo,hi] вдоль оси (Сазерленд-Ходжман, 2 прохода).</summary>
        private static List<Vector3> ClipTriToSlab(Vector3 a, Vector3 b, Vector3 c, int axis, float lo, float hi)
        {
            var poly = new List<Vector3>(5);
            poly.Add(a); poly.Add(b); poly.Add(c);
            poly = ClipPolyAxis(poly, axis, hi, true);
            if (poly.Count == 0) return poly;
            poly = ClipPolyAxis(poly, axis, lo, false);
            return poly;
        }

        private static List<Vector3> ClipPolyAxis(List<Vector3> poly, int axis, float edge, bool keepLess)
        {
            var out_ = new List<Vector3>(poly.Count + 1);
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 cur = poly[i], prev = poly[(i + n - 1) % n];
                bool curIn = keepLess ? cur[axis] <= edge : cur[axis] >= edge;
                bool prevIn = keepLess ? prev[axis] <= edge : prev[axis] >= edge;
                if (curIn)
                {
                    if (!prevIn) out_.Add(IntersectAxis(prev, cur, axis, edge));
                    out_.Add(cur);
                }
                else if (prevIn) out_.Add(IntersectAxis(prev, cur, axis, edge));
            }
            return out_;
        }

        private static Vector3 IntersectAxis(Vector3 p, Vector3 q, int axis, float x)
        {
            float cp = p[axis], cq = q[axis];
            float t = Mathf.Abs(cq - cp) < Epsilon ? 0f : (x - cp) / (cq - cp);
            return Vector3.Lerp(p, q, Mathf.Clamp01(t));
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
            string[] capsuleToks = SplitTokens(s.capsuleTokens);
            string[] sliceToks = SplitTokens(s.sliceTokens);

            List<ExistingHolder> existing = null;
            HashSet<string> existingNames = null;
            if (skipExisting && existingRoot != null)
            {
                existing = CollectExistingHolders(existingRoot);
                existingNames = new HashSet<string>(StringComparer.Ordinal);
                var allT = existingRoot.GetComponentsInChildren<Transform>(includeInactive: true);
                foreach (var t in allT)
                    if (t != null && t != existingRoot) existingNames.Add(t.name);
            }

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

                // Вид коллайдера: бокс / капсула (круглые бары) / нарезка (кривые меши).
                Vector3 worldPerLocal = AbsVec(go.transform.lossyScale);
                int kind = 0;
                List<SliceBox> slices = null;
                if (ShouldCapsule(f.sharedMesh, worldPerLocal, capsuleToks, upper))
                {
                    kind = 1;
                }
                else if (sliceToks.Length > 0 && MatchesAny(upper, sliceToks))
                {
                    // Read/Write у импортированных FBX обычно выключен; редактор данные всё равно
                    // читает (MeshDataArray), поэтому нарезка от этого флага больше не зависит.
                    // Ось — длиннейшая в ЛОКАЛЬНЫХ bounds (там же лежат вершины).
                    // Брать ось из мировых размеров нельзя: рамка повёрнута (deck: rot 270,90,0),
                    // индексы переставлены, и нарезка пойдёт поперёк сужения.
                    slices = SlicePlanBest(f.sharedMesh, s.sliceStep,
                        Mathf.Max(0f, s.sliceAdaptTol), worldPerLocal, out bool unreadSlice, !s.sliceAxisOnly, Mathf.Max(0f, s.sliceAirTol));
                    if (unreadSlice) report.unreadSlices++; // данных нет совсем — обычный бокс ниже
                    if (slices != null) kind = 2;
                }

                // Простой бокс детали под углом (скат, наклонное стекло) — повёрнутый по её рамке,
                // а не AABB в осях меша (тот заливает воздухом весь габарит наклона).
                SliceBox hull = null;
                if (kind == 0 && !s.sliceAxisOnly)
                {
                    hull = OrientedHullBox(f.sharedMesh, worldPerLocal);
                    if (hull != null) kind = 3;
                }

                // Append-режим: уже закрытые меши не дублируем.
                // Для капсул/нарезки generic-проверка по объёму не годится: старый одиночный
                // бокс покрывает меш целиком и навсегда блокировал бы смену вида. Поэтому:
                // точная сверка по детерминированным именам, предшественник сносится при создании,
                // а склеенные MG_*-боксы уважаем (не дублируем поверх мержа).
                if (skipExisting && existing != null)
                {
                    bool preciseNames = stableNames && existingNames != null && (kind == 1 || kind == 2 || kind == 3);
                    if (preciseNames)
                    {
                        if (IsCoveredByMerged(existing, go))
                        {
                            report.skippedExists++;
                            continue;
                        }
                        if ((kind == 1 || kind == 3) && existingNames.Contains(HolderNameFor(root.transform, go.transform)))
                        {
                            report.skippedExists++;
                            continue;
                        }
                        // kind == 2: точная сверка по именам кусков — ниже.
                    }
                    else if (ExistsHolder(existing, go))
                    {
                        report.skippedExists++;
                        continue;
                    }
                }

                // Нарезанные куски имеют детерминированные имена — сверяем множества целиком:
                // пропуск только если набор кусков совпал 1-в-1 (смена шага/допуска даёт другое
                // множество — тогда purge + пересборка при создании).
                // (Обычные боксы/капсулы покрыты суффиксом+позицией и объёмом в ExistsHolder.)
                string sliceBase = null;
                if (kind == 2 && stableNames && existingNames != null)
                {
                    sliceBase = HolderNameFor(root.transform, go.transform);
                    if (SliceSetMatches(existingNames, sliceBase, slices))
                    {
                        report.skippedExists++;
                        continue;
                    }
                }

                if (dryOnly)
                {
                    if (kind == 1) report.capsules++; // в dry-режиме capsules = "было бы создано капсул"
                    else if (kind == 2) { report.created += slices.Count; report.slicedMeshes++; }
                    else if (kind == 3) report.created++;
                    else report.created++; // в dry-режиме created = "было бы создано"
                    continue;
                }

                items.Add(new Item { source = go, mesh = f.sharedMesh, renderer = r, worldSize = worldSize, kind = kind, slices = slices, hull = hull });
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
                        g.isStatic = !s.shipMode;
                        parent = g.transform;
                        groups[groupName] = parent;
                    }
                }

                string baseName = stableNames
                    ? HolderNameFor(root.transform, it.source.transform)
                    : $"BC_{index:0000}_{SanitizeName(it.source.name)}";

                if (it.kind == 1)
                {
                    DestroyPredecessorHolders(genRoot, it.source, null);
                    AddCapsule(root, parent, baseName, it, !s.shipMode);
                    index++;
                    report.capsules++;
                }
                else if (it.kind == 2)
                {
                    DestroyPredecessorHolders(genRoot, it.source, stableNames ? baseName : null);
                    foreach (var sl in it.slices)
                    {
                        // Старые куски этого меша уже снесены выше (purge); чужие префиксы не пересекаются.
                        string sliceName = SliceHolderName(baseName, sl);
                        AddSliceBox(root, parent, sliceName, it, sl, s.minThickness, !s.shipMode);
                        index++;
                        report.created++;
                    }
                    report.slicedMeshes++;
                }
                else if (it.kind == 3)
                {
                    DestroyPredecessorHolders(genRoot, it.source, null);
                    AddObliqueBox(root, parent, baseName, it, it.hull, s.minThickness, !s.shipMode);
                    index++;
                    report.created++;
                }
                else
                {
                    AddFittedBox(root, parent, baseName, it, it.mesh.bounds, s.minThickness, !s.shipMode);
                    index++;
                    report.created++;
                }
            }
        }

        /// <summary>
        /// Снести прямого предшественника исходника — одиночный бокс/капсулу с тем же суффиксом
        /// имени ('_'+имя) на той же позиции (2 см), чтобы смена вида (бокс→капсула/нарезка)
        /// не дублировала, а заменяла. Плюс снести старые куски 'База_S##' этого же меша
        /// (допуск/шаг могли смениться — имена бинов другие). Куски чужих мешей (другой хэш
        /// в имени) и склеенные MG_*-боксы не трогаем.
        /// </summary>
        private static void DestroyPredecessorHolders(Transform genRoot, GameObject source, string sliceBase)
        {
            if (genRoot == null || source == null) return;
            string suffix = "_" + SanitizeName(source.name);
            string slicePrefix = string.IsNullOrEmpty(sliceBase) ? null : sliceBase + "_S";
            Vector3 p = source.transform.position;
            const float tolSq = 0.0004f; // (2 см)^2
            var colliders = genRoot.GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (var c in colliders)
            {
                if (c == null) continue;
                if (!(c is BoxCollider) && !(c is CapsuleCollider)) continue;
                Transform t = c.transform;
                if (t == genRoot) continue;
                if (slicePrefix != null && t.name.StartsWith(slicePrefix, StringComparison.Ordinal))
                {
                    Undo.DestroyObjectImmediate(t.gameObject);
                    continue;
                }
                if (!t.name.EndsWith(suffix, StringComparison.Ordinal)) continue;
                if ((t.position - p).sqrMagnitude > tolSq) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
            }
        }

        /// <summary>Индекс самой длинной оси вектора (0/1/2).</summary>
        private static int LongestAxis(Vector3 v)
        {
            if (v.x >= v.y && v.x >= v.z) return 0;
            if (v.y >= v.z) return 1;
            return 2;
        }

        /// <summary>Рамка холдера = мировая рамка исходника (оффсет коллайдера не удваивается).</summary>
        private static Transform CreateHolderFrame(GameObject root, Transform parent, string name, GameObject source, bool markStatic)
        {
            var holder = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(holder, "Create settlement collider");
            holder.layer = root.layer;
            holder.isStatic = markStatic;
            holder.transform.SetParent(parent, true);
            holder.transform.SetPositionAndRotation(
                source.transform.position,
                source.transform.rotation);
            holder.transform.localScale = Divide(source.transform.lossyScale, parent.lossyScale);
            return holder.transform;
        }

        private static void AddFittedBox(GameObject root, Transform parent, string name, Item it, Bounds localBounds, float minThickness, bool markStatic)
        {
            Transform hf = CreateHolderFrame(root, parent, name, it.source, markStatic);
            var box = hf.gameObject.AddComponent<BoxCollider>();
            Vector3 center;
            Vector3 size;
            FitBox(localBounds, AbsVec(it.source.transform.lossyScale),
                hf, minThickness, out center, out size);
            box.center = center;
            box.size = size;
            box.isTrigger = false;
        }

        /// <summary>Кусок нарезки: осевой — в рамке исходника, наклонный — повёрнутым боксом.</summary>
        private static void AddSliceBox(GameObject root, Transform parent, string name, Item it, SliceBox sl, float minThickness, bool markStatic)
        {
            if (sl.oriented)
            {
                AddObliqueBox(root, parent, name, it, sl, minThickness, markStatic);
                return;
            }
            var sb = new Bounds();
            sb.SetMinMax(sl.min, sl.max);
            AddFittedBox(root, parent, name, it, sb, minThickness, markStatic);
        }

        /// <summary>
        /// Повёрнутый бокс наклонной серии. Холдер — с единичным мировым масштабом: при
        /// неравномерном масштабе родителя поворот внутри его рамки дал бы перекос, поэтому
        /// такой холдер кладётся в рамку '<see cref="UnitFrameName"/>' под тем же родителем.
        /// </summary>
        private static void AddObliqueBox(GameObject root, Transform parent, string name, Item it, SliceBox sl, float minThickness, bool markStatic)
        {
            Transform src = it.source.transform;
            Vector3 ls = src.lossyScale;
            Vector3 sgn = new Vector3(ls.x < 0f ? -1f : 1f, ls.y < 0f ? -1f : 1f, ls.z < 0f ? -1f : 1f);
            Vector3 wx = (src.rotation * Vector3.Scale(sgn, sl.obbAxisX)).normalized;
            Vector3 wy = (src.rotation * Vector3.Scale(sgn, sl.obbAxisY)).normalized;
            Vector3 wz = Vector3.Cross(wx, wy).normalized;
            wy = Vector3.Cross(wz, wx).normalized;
            Quaternion rot = Quaternion.LookRotation(wz, wy);

            Transform frame = UnitScaleFrame(root, parent, markStatic);
            var holder = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(holder, "Create settlement collider");
            holder.layer = root.layer;
            holder.isStatic = markStatic;
            holder.transform.SetParent(frame, false);
            holder.transform.SetPositionAndRotation(src.TransformPoint(sl.obbCenterLocal), rot);
            float k = Mathf.Abs(frame.lossyScale.x);
            holder.transform.localScale = Vector3.one * (k < Epsilon ? 1f : 1f / k);

            var box = holder.AddComponent<BoxCollider>();
            Vector3 center;
            Vector3 size;
            FitBox(new Bounds(Vector3.zero, sl.obbSize), Vector3.one, holder.transform, minThickness, out center, out size);
            box.center = center;
            box.size = size;
            box.isTrigger = false;
        }

        /// <summary>Родитель с равномерным масштабом — он сам, иначе дочерняя рамка с мировым масштабом 1.</summary>
        private static Transform UnitScaleFrame(GameObject root, Transform parent, bool markStatic)
        {
            Vector3 ls = parent.lossyScale;
            float mx = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            float mn = Mathf.Min(ls.x, Mathf.Min(ls.y, ls.z));
            if (mn > Epsilon && mx - mn <= mx * 1e-4f) return parent;

            Transform f = parent.Find(UnitFrameName);
            if (f == null)
            {
                var go = new GameObject(UnitFrameName);
                Undo.RegisterCreatedObjectUndo(go, "Create settlement collider frame");
                go.layer = root.layer;
                go.isStatic = markStatic;
                f = go.transform;
                f.SetParent(parent, false);
            }
            else
            {
                Undo.RecordObject(f, "Settlement collider frame");
            }
            f.localPosition = Vector3.zero;
            f.localRotation = Quaternion.identity;
            f.localScale = new Vector3(
                Mathf.Abs(ls.x) < Epsilon ? 1f : 1f / ls.x,
                Mathf.Abs(ls.y) < Epsilon ? 1f : 1f / ls.y,
                Mathf.Abs(ls.z) < Epsilon ? 1f : 1f / ls.z);
            return f;
        }

        private static void AddCapsule(GameObject root, Transform parent, string name, Item it, bool markStatic)
        {
            Transform hf = CreateHolderFrame(root, parent, name, it.source, markStatic);
            Bounds b = it.mesh.bounds;
            int dir = LongestAxis(b.size);
            float cross = dir == 0 ? Mathf.Min(b.size.y, b.size.z)
                : dir == 1 ? Mathf.Min(b.size.x, b.size.z) : Mathf.Min(b.size.x, b.size.y);
            var cap = hf.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = dir;
            cap.center = b.center;
            cap.radius = cross * 0.5f;
            cap.height = Mathf.Max(b.size[dir], cross); // высота обязана быть >= 2r; ShouldCapsule это почти гарантирует
            cap.isTrigger = false;
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
        private const string PrefsKey = "ProjectC.SettlementColliders.Window";

        private BuildSettlementColliders.Settings settings = BuildSettlementColliders.DefaultSettings();
        private float mergeGap = 0.1f;
        private float mergeAlign = 0.02f;
        private string mergeTokens = "";
        private int mergeScopeIdx = 0;
        private bool exactConvex;
        private float stairThickness = StairCollidersTool.DefaultThickness;
        private Vector2 scroll;

        // Состояние секций (фолдауты). Переживает перекомпиляцию/закрытие окна через EditorPrefs.
        private bool foldFilters = true;
        private bool foldShapes;
        private bool foldBuild = true;
        private bool foldSlice = true;
        private bool foldExact;
        private bool foldStairs;
        private bool foldCollect;
        private bool foldMerge;

        private static GUIStyle sectionStyle;

        private static readonly string[] MergeScopeLabels =
        {
            "Плиты и стены (раздельно)",
            "Только горизонтальные",
            "Только вертикальные",
            "Всё вместе"
        };

        /// <summary>Что сохраняем между сессиями. Этим же пользуются хоткеи, когда окно закрыто.</summary>
        [Serializable]
        private class Persisted
        {
            public BuildSettlementColliders.Settings settings = BuildSettlementColliders.DefaultSettings();
            public float mergeGap = 0.1f;
            public float mergeAlign = 0.02f;
            public string mergeTokens = "";
            public int mergeScopeIdx;
            public bool exactConvex;
            public float stairThickness = StairCollidersTool.DefaultThickness;
            public bool foldFilters = true;
            public bool foldShapes;
            public bool foldBuild = true;
            public bool foldSlice = true;
            public bool foldExact;
            public bool foldStairs;
            public bool foldCollect;
            public bool foldMerge;
        }

        private static GUIStyle SectionStyle
        {
            get
            {
                if (sectionStyle == null)
                    sectionStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
                return sectionStyle;
            }
        }

        /// <summary>Заголовок-фолдаут секции. Возвращает «раскрыта ли» и двигает indent.</summary>
        private static bool BeginSection(ref bool state, string title)
        {
            EditorGUILayout.Space(6f);
            state = EditorGUILayout.Foldout(state, title, true, SectionStyle);
            if (state) EditorGUI.indentLevel++;
            return state;
        }

        private static void EndSection(bool open)
        {
            if (open) EditorGUI.indentLevel--;
        }

        private void OnEnable()
        {
            Persisted p = LoadPersisted();
            if (p.settings != null) settings = p.settings;
            mergeGap = p.mergeGap;
            mergeAlign = p.mergeAlign;
            mergeTokens = p.mergeTokens ?? "";
            mergeScopeIdx = Mathf.Clamp(p.mergeScopeIdx, 0, MergeScopeLabels.Length - 1);
            exactConvex = p.exactConvex;
            stairThickness = p.stairThickness;
            foldFilters = p.foldFilters;
            foldShapes = p.foldShapes;
            foldBuild = p.foldBuild;
            foldSlice = p.foldSlice;
            foldExact = p.foldExact;
            foldStairs = p.foldStairs;
            foldCollect = p.foldCollect;
            foldMerge = p.foldMerge;
        }

        private void OnDisable()
        {
            SavePersisted();
        }

        private void SavePersisted()
        {
            var p = new Persisted
            {
                settings = settings,
                mergeGap = mergeGap,
                mergeAlign = mergeAlign,
                mergeTokens = mergeTokens,
                mergeScopeIdx = mergeScopeIdx,
                exactConvex = exactConvex,
                stairThickness = stairThickness,
                foldFilters = foldFilters,
                foldShapes = foldShapes,
                foldBuild = foldBuild,
                foldSlice = foldSlice,
                foldExact = foldExact,
                foldStairs = foldStairs,
                foldCollect = foldCollect,
                foldMerge = foldMerge
            };
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(p));
        }

        private static Persisted LoadPersisted()
        {
            string json = EditorPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var p = JsonUtility.FromJson<Persisted>(json);
                    if (p != null)
                    {
                        if (p.settings == null) p.settings = BuildSettlementColliders.DefaultSettings();
                        return p;
                    }
                }
                catch (Exception)
                {
                    // Битый json (сменилась схема) — молча падаем на дефолты.
                }
            }
            return new Persisted();
        }

        [MenuItem("Tools/ProjectC/Build Settlement Colliders...", false, 90)]
        public static void Open()
        {
            var w = GetWindow<BuildSettlementCollidersWindow>("Settlement Colliders");
            w.minSize = new Vector2(380f, 660f);
            w.Show();
        }

        /// <summary>
        /// Хоткей: обычная сборка боксов по выделению (Ctrl/Cmd + Alt + B).
        /// Настройки — последние сохранённые окном; include-фильтр игнорируется (обычный проход).
        /// </summary>
        [MenuItem("Tools/ProjectC/Build Boxes on Selection %&b", false, 100)]
        private static void HotkeyBuildBoxes()
        {
            var roots = Selection.gameObjects;
            if (roots == null || roots.Length == 0)
            {
                Debug.LogWarning("[SettlementColliders] Хоткей: ничего не выделено — выдели корень в Hierarchy.");
                return;
            }
            Persisted p = LoadPersisted();
            var s = WithoutInclude(p.settings);
            foreach (var root in roots)
            {
                if (root == null) continue;
                BuildSettlementColliders.BuildFor(root, s);
            }
        }

        /// <summary>
        /// Хоткей: нарезка выделения с шагом sliceStep (Ctrl/Cmd + Alt + S). Фильтры игнорируются —
        /// режется всё выделенное (тот же смысл, что у кнопки «НАРЕЗАТЬ С ШАГОМ»).
        /// </summary>
        [MenuItem("Tools/ProjectC/Slice Selection by Step %&s", false, 101)]
        private static void HotkeySliceSelection()
        {
            var roots = Selection.gameObjects;
            if (roots == null || roots.Length == 0)
            {
                Debug.LogWarning("[SettlementColliders] Хоткей: ничего не выделено — выдели корень в Hierarchy.");
                return;
            }
            Persisted p = LoadPersisted();
            foreach (var root in roots)
            {
                if (root == null) continue;
                if (root.name.EndsWith(BuildSettlementColliders.GeneratedSuffix))
                {
                    Debug.LogWarning($"[SettlementColliders] '{root.name}' — корень генерации, выбери исходник.");
                    continue;
                }
                BuildSettlementColliders.BuildSlicesDirect(root, p.settings);
            }
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
                capsuleTokens = s.capsuleTokens,
                sliceTokens = s.sliceTokens,
                sliceStep = s.sliceStep,
                sliceAdaptTol = s.sliceAdaptTol,
                sliceAxisOnly = s.sliceAxisOnly,
                sliceAirTol = s.sliceAirTol,
                shipMode = s.shipMode,
                groupByTopLevel = s.groupByTopLevel,
                skipInactive = s.skipInactive,
                repairThinBoxes = s.repairThinBoxes
            };
        }

        private void OnGUI()
        {
            // Shift-выделение: прогоняем каждый корень по очереди (логи в Console — по каждому).
            GameObject[] roots = Selection.gameObjects;
            if (roots == null) roots = new GameObject[0];
            bool hasRoots = roots.Length > 0;

            scroll = EditorGUILayout.BeginScrollView(scroll);

            // ── Шапка: что выделено (всегда на виду) ─────────────────────────────────
            EditorGUILayout.LabelField("Выделение в Hierarchy (можно несколько через Shift)", EditorStyles.boldLabel);
            if (!hasRoots)
            {
                EditorGUILayout.HelpBox("Выдели корневой GameObject поселения (например Project-C_primum_farm_x_1_2_GameReady), затем вернись сюда. Через Shift можно выбрать несколько — тулза прогонит каждый по очереди.", MessageType.Info);
            }
            else if (roots.Length == 1 && roots[0] != null)
            {
                int meshCount = roots[0].GetComponentsInChildren<MeshFilter>(true).Length;
                EditorGUILayout.LabelField($"Корень: {roots[0].name} (MeshFilter: {meshCount})");
            }
            else
            {
                int total = 0;
                foreach (var r in roots)
                    if (r != null) total += r.GetComponentsInChildren<MeshFilter>(true).Length;
                int show = Mathf.Min(5, roots.Length);
                var names = new List<string>(show);
                for (int i = 0; i < show; i++) names.Add(roots[i] != null ? roots[i].name : "?");
                string more = roots.Length > show ? $" +{roots.Length - show}…" : "";
                EditorGUILayout.LabelField($"Корней: {roots.Length} ({string.Join(", ", names.ToArray())}{more}), MeshFilter всего: {total}");
            }

            // ── 1. Пороги и фильтры ─────────────────────────────────────────────────
            bool openFilters = BeginSection(ref foldFilters, "1. Пороги и фильтры");
            if (openFilters)
            {
                settings.minWorldSize = EditorGUILayout.FloatField("Мин. размер меша (м)", settings.minWorldSize);
                settings.minThickness = EditorGUILayout.FloatField("Мин. толщина бокса (м)", settings.minThickness);
                settings.groupByTopLevel = EditorGUILayout.Toggle("Группировать по зданиям", settings.groupByTopLevel);
                settings.skipInactive = EditorGUILayout.Toggle("Пропускать выключенные", settings.skipInactive);
                settings.repairThinBoxes = EditorGUILayout.Toggle("Чинить тонкие боксы", settings.repairThinBoxes);
                settings.shipMode = EditorGUILayout.Toggle("Режим корабля (не static)", settings.shipMode);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Исключить по имени (через запятую)", EditorStyles.boldLabel);
                settings.excludeTokens = EditorGUILayout.TextArea(settings.excludeTokens ?? "", GUILayout.MinHeight(54f));

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Только эти имена (через запятую)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Пусто = фильтра по именам нет. Напр.: TABLE, CHAIR. Действует на «только совпадения» и на нарезку по фильтру.", EditorStyles.miniLabel);
                settings.includeTokens = EditorGUILayout.TextArea(settings.includeTokens ?? "", GUILayout.MinHeight(36f));
            }
            EndSection(openFilters);

            // ── 2. Некоробочные формы ───────────────────────────────────────────────
            bool openShapes = BeginSection(ref foldShapes, "2. Некоробочные формы (капсулы / нарезка)");
            if (openShapes)
            {
                EditorGUILayout.LabelField("Капсулы — круглые бары (трубы, арматура) вдоль длинной оси. Нарезка — кривые меши кусками по вершинам.", EditorStyles.miniLabel);
                settings.capsuleTokens = EditorGUILayout.TextField("Капсулы по именам", settings.capsuleTokens ?? "");
                settings.sliceTokens = EditorGUILayout.TextField("Нарезать только эти имена", settings.sliceTokens ?? "");
                settings.sliceStep = EditorGUILayout.FloatField("Шаг нарезки (м)", settings.sliceStep);
                settings.sliceAdaptTol = EditorGUILayout.FloatField("Допуск слияния кусков (м)", settings.sliceAdaptTol);
                settings.sliceAxisOnly = EditorGUILayout.ToggleLeft("Только осевые куски (без наклонных боксов)", settings.sliceAxisOnly);
                EditorGUI.BeginDisabledGroup(settings.sliceAxisOnly);
                settings.sliceAirTol = EditorGUILayout.FloatField("Допуск воздуха наклонных (м)", settings.sliceAirTol);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.LabelField("Выкл. (по умолч.): лесенка нарезки у коробочной детали под углом заменяется одним повёрнутым боксом (суффикс R).", EditorStyles.miniLabel);
            }
            EndSection(openShapes);

            // ── 3. Сборка боксов ────────────────────────────────────────────────────
            bool openBuild = BeginSection(ref foldBuild, "3. Сборка боксов (<Root>_Colliders)");
            if (openBuild)
            {
                EditorGUI.BeginDisabledGroup(!hasRoots);
                if (GUILayout.Button("Dry Run (только посчитать)", GUILayout.Height(30f)))
                {
                    // Обычный режим: include-фильтр игнорируется, считаются все значимые меши.
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
                        var r = BuildSettlementColliders.DryRun(root, WithoutInclude(settings));
                        Debug.Log($"[SettlementColliders] DRY '{root.name}': created={r.created}, capsules={r.capsules}, " +
                                  $"slicedMeshes={r.slicedMeshes} (unread={r.unreadSlices}), " +
                                  $"noMesh={r.skippedNoMesh}, disabled={r.skippedDisabled}, " +
                                  $"name={r.skippedName}, tiny={r.skippedTiny}, thinToRepair={r.repaired}.");
                    }
                }
                if (GUILayout.Button("Build (построить коллайдеры)", GUILayout.Height(34f)))
                {
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
                        BuildSettlementColliders.BuildFor(root, WithoutInclude(settings));
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Добавить только совпадения include-фильтра (раздел 1)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Существующие боксы не дублируются (alreadyExists), исключения тоже действуют.", EditorStyles.miniLabel);
                EditorGUI.BeginDisabledGroup(!hasRoots || string.IsNullOrWhiteSpace(settings.includeTokens));
                if (GUILayout.Button("Dry Run (только совпадения)", GUILayout.Height(30f)))
                {
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
                        var r = BuildSettlementColliders.DryRunAppend(root, settings);
                        Debug.Log($"[SettlementColliders] DRY-APPEND '{root.name}' [{settings.includeTokens}]: toAdd={r.created}, " +
                                  $"capsules={r.capsules}, slicedMeshes={r.slicedMeshes}, " +
                                  $"alreadyExists={r.skippedExists}, " +
                                  $"noMesh={r.skippedNoMesh}, disabled={r.skippedDisabled}, " +
                                  $"name={r.skippedName}, notIncluded={r.skippedInclude}, tiny={r.skippedTiny}, thinToRepair={r.repaired}.");
                    }
                }
                if (GUILayout.Button("Build (только совпадения, добавить)", GUILayout.Height(34f)))
                {
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
                        BuildSettlementColliders.BuildAppend(root, settings);
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            EndSection(openBuild);

            // ── 4. Нарезка по шагу ────────────────────────────────────────────────
            bool openSlice = BeginSection(ref foldSlice, "4. Нарезка по шагу");
            if (openSlice)
            {
                EditorGUILayout.LabelField($"Шаг: {settings.sliceStep:F2} м, допуск: {settings.sliceAdaptTol:F2} м (меняются в разделе 2).", EditorStyles.miniLabel);

                EditorGUILayout.LabelField("Всё выделенное — фильтры игнорируются", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Режет всё выделенное с шагом выше. Имена копировать не надо.", EditorStyles.miniLabel);
                EditorGUI.BeginDisabledGroup(!hasRoots);
                if (GUILayout.Button("Dry Run (нарезка)", GUILayout.Height(28f)))
                {
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
                        if (root.name.EndsWith(BuildSettlementColliders.GeneratedSuffix))
                        {
                            Debug.LogWarning($"[SettlementColliders] '{root.name}' — корень генерации, выбери исходник.");
                            continue;
                        }
                        var r = BuildSettlementColliders.DryRunSlicesDirect(root, settings);
                        Debug.Log($"[SettlementColliders] DRY-SLICE '{root.name}': boxes={r.created}, " +
                                  $"slicedMeshes={r.slicedMeshes} (unread={r.unreadSlices}), " +
                                  $"alreadyExists={r.skippedExists}, " +
                                  $"noMesh={r.skippedNoMesh}, disabled={r.skippedDisabled}.");
                    }
                }
                if (GUILayout.Button("НАРЕЗАТЬ С ШАГОМ", GUILayout.Height(30f)))
                {
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
                        if (root.name.EndsWith(BuildSettlementColliders.GeneratedSuffix))
                        {
                            Debug.LogWarning($"[SettlementColliders] '{root.name}' — корень генерации, выбери исходник.");
                            continue;
                        }
                        BuildSettlementColliders.BuildSlicesDirect(root, settings);
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("По фильтрам — холдеры под исходными мешами", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Обходит всё поддерево корня и применяет разделы 1–2: include / exclude / мин. размер. Круглые бары из capsuleTokens — капсула; нарезка — по sliceTokens (пусто = режем всё принятое). Каждый холдер становится ребёнком своего меша, без '<Root>_Colliders'.", EditorStyles.miniLabel);
                EditorGUI.BeginDisabledGroup(!hasRoots);
                if (GUILayout.Button("Dry Run (по фильтру)", GUILayout.Height(28f)))
                {
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
                        var r = BuildSettlementColliders.DryRunSlicesFiltered(root, settings);
                        Debug.Log($"[SettlementColliders] DRY-SLICE-FILTER '{root.name}': boxes={r.created}, " +
                                  $"capsules={r.capsules}, slicedMeshes={r.slicedMeshes} (unread={r.unreadSlices}), " +
                                  $"noMesh={r.skippedNoMesh}, disabled={r.skippedDisabled}, " +
                                  $"name={r.skippedName}, notIncluded={r.skippedInclude}, tiny={r.skippedTiny}.");
                    }
                }
                if (GUILayout.Button("НАРЕЗАТЬ С ШАГОМ ПО ФИЛЬТРУ", GUILayout.Height(30f)))
                {
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
                        BuildSettlementColliders.BuildSlicesFiltered(root, settings);
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            EndSection(openSlice);

            // ── 5. Точный меш ────────────────────────────────────────────────────────
            bool openExact = BeginSection(ref foldExact, "5. Точный меш (MeshCollider)");
            if (openExact)
            {
                EditorGUILayout.LabelField("Выделенному объекту (полусфера, арка, лестница) ставится отдельный холдер '<Имя>_MC' с MeshCollider на том же меше. Дерево не обходится, фильтры игнорируются.", EditorStyles.miniLabel);
                exactConvex = EditorGUILayout.ToggleLeft(
                    $"Выпуклый (convex=true) — для кораблей / под Rigidbody, до {ExactMeshColliderTool.ConvexTriangleLimit} тр.",
                    exactConvex);
                if (GUILayout.Button("ТОЧНЫЙ МЕШ (MeshCollider)", GUILayout.Height(34f)))
                    ExactMeshColliderTool.BuildForSelection(exactConvex);
                if (GUILayout.Button("Снести точные меши (выделение)", GUILayout.Height(22f)))
                    ExactMeshColliderTool.RemoveForSelection();
            }
            EndSection(openExact);

            // ── 6. Лестницы ──────────────────────────────────────────────────────────
            bool openStairs = BeginSection(ref foldStairs, "6. Лестницы (STAIR)");
            if (openStairs)
            {
                EditorGUILayout.LabelField("1) один марш → один наклонный бокс. 2) две ступени → наклонный бокс между ними. 3) по ступеням → ряд блоков на каждую проступь. Дети не обходятся.", EditorStyles.miniLabel);
                stairThickness = EditorGUILayout.FloatField("Толщина бокса марша (м)", stairThickness);
                if (GUILayout.Button("ЛЕСТНИЦА 1 — марш → наклонный бокс", GUILayout.Height(28f)))
                {
                    StairCollidersTool.Thickness = stairThickness;
                    StairCollidersTool.BuildFlightRamp();
                }
                if (GUILayout.Button("ЛЕСТНИЦА 2 — две ступени → наклонный бокс", GUILayout.Height(28f)))
                {
                    StairCollidersTool.Thickness = stairThickness;
                    StairCollidersTool.BuildBetweenTwoSteps();
                }
                if (GUILayout.Button("ЛЕСТНИЦА 3 — по ступеням → ряд блоков", GUILayout.Height(28f)))
                {
                    StairCollidersTool.Thickness = stairThickness;
                    StairCollidersTool.BuildPerStepBlocks();
                }
                if (GUILayout.Button("Снести STAIR (выделение)", GUILayout.Height(22f)))
                    StairCollidersTool.RemoveForSelection();
            }
            EndSection(openStairs);

            // ── 7. Собрать коллайдеры в группу ──────────────────────────────────────
            bool openCollect = BeginSection(ref foldCollect, "7. Собрать коллайдеры в группу");
            if (openCollect)
            {
                EditorGUILayout.LabelField("Выдели главный FBX: все коллайдерные ветки (холдеры _MC / _STAIR / BC_* / *_Generated) со всех дочерних объектов переезжают в '<Имя>_COLLIDER' под этот FBX. Мировые позиции сохраняются, арт не трогается.", EditorStyles.miniLabel);
                if (GUILayout.Button($"СОБРАТЬ В <ИМЯ>{CollectCollidersTool.GroupSuffix} — по выделению", GUILayout.Height(30f)))
                    CollectCollidersTool.CollectForSelection();
            }
            EndSection(openCollect);

            // ── 8. Слияние соседних боксов ──────────────────────────────────────────
            bool openMerge = BeginSection(ref foldMerge, "8. Объединить соседние боксы");
            if (openMerge)
            {
                EditorGUILayout.LabelField("Склеивает боксы встык/внахлёст в пределах зазора — из 1000 плиток пола делает 1. Только внутри генерации.", EditorStyles.miniLabel);
                mergeGap = EditorGUILayout.FloatField("Макс. зазор шва (м)", mergeGap);
                mergeAlign = EditorGUILayout.FloatField("Точность совпадения граней (м)", mergeAlign);
                mergeScopeIdx = EditorGUILayout.Popup("Что клеить", mergeScopeIdx, MergeScopeLabels);
                EditorGUILayout.LabelField("Только эти имена (пусто = все сгенерированные)");
                mergeTokens = EditorGUILayout.TextArea(mergeTokens ?? "", GUILayout.MinHeight(28f));

                EditorGUI.BeginDisabledGroup(!hasRoots);
                if (GUILayout.Button("Dry Run (слияние)", GUILayout.Height(30f)))
                {
                    var scope = (BuildSettlementColliders.MergeScope)mergeScopeIdx;
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
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
                }
                if (GUILayout.Button("Merge (склеить соседние)", GUILayout.Height(34f)))
                {
                    var scope = (BuildSettlementColliders.MergeScope)mergeScopeIdx;
                    foreach (var root in roots)
                    {
                        if (root == null) continue;
                        BuildSettlementColliders.MergeBoxes(root, mergeGap, mergeAlign, mergeTokens, scope);
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            EndSection(openMerge);

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "Бокс на каждый значимый меш. Мелочь (болты) режется размером и именами. " +
                "Тонкие палубы утолщаются вниз до мин. толщины — топ заподлицо, корабли не проваливаются. " +
                "Наклонные плоские (скаты, рампы) покрываются повёрнутыми боксами 1-в-1 — рамка копирует поворот. " +
                "Круглые бары (PIPE, REBAR) — капсулы вдоль длинной оси. Кривые меши — нарезка кусками по вершинам. " +
                "Сужающийся нос (Deck_Main_Bow) — нарезкой вдоль оси + режим корабля (не static), иначе углы бокса будут цеплять. " +
                "Повторный Build пересобирает '<Root>_Colliders' с нуля. " +
                "Режим 'только совпадения': в разделе 1 укажи TABLE (или TABLE, CHAIR) — отдельный старт " +
                "ДОБАВИТ боксы только там, где имя содержит токен, существующие не трогает и не дублирует. " +
                "Нарезка по фильтру кладёт холдеры детьми исходных мешей (без '<Root>_Colliders').\n\n" +
                "Хоткеи: Ctrl+Alt+B — Build боксов по выделению, Ctrl+Alt+S — нарезка выделения с шагом. " +
                "Берут последние сохранённые настройки окна.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }
    }
}
