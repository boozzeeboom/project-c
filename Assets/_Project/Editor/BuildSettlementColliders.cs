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
    ///   - некоробочные формы: круглые бары (токены capsuleTokens: PIPE/REBAR/TUBE) получают
    ///     CapsuleCollider вдоль длинной оси; кривые меши (токены sliceTokens) режутся slab'ами
    ///     с шагом sliceStep (сечения — по вершинам и треугольникам, дыр нет), соседние куски
    ///     с одинаковым сечением склеиваются в пределах sliceAdaptTol (прямые участки — 1 бокс,
    ///     сужения — детально, напр. нос Deck_Main_Bow); наклонные плоские покрываются
    ///     повёрнутыми боксами 1-в-1;
    ///   - режим корабля (shipMode): без флага static — '<Root>_Colliders' едет с Rigidbody
    ///     как compound-коллайдер (для поселений static дешевле, не включать);
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

            [Tooltip("Длина куска нарезки (м, world) вдоль длинной оси — шаг измерения. Пустые куски (дыры в геометрии) пропускаются. Нужен Read/Write Enabled у меша, иначе — обычный бокс.")]
            public float sliceStep = 1.0f;

            [Tooltip("Адаптив нарезки (м, world): соседние куски с сечением, совпадающим в пределах допуска, склеиваются в один (прямая корма — 1 бокс, сужение — детально). 0 — не склеивать, каждый непустой slab своим боксом.")]
            public float sliceAdaptTol = 0.05f;

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
            public int kind; // 0 = бокс, 1 = капсула, 2 = нарезанный на сегменты
            public List<SliceBox> slices; // только для kind == 2
        }

        private class SliceBox
        {
            public int bin; // индекс бина в равномерной сетке — стабилен между запусками
            public int sub; // индекс связной компоненты внутри slab'а (0..)
            public bool multi; // в slab'е несколько компонент — к имени добавится суффикс Cn
            public Vector3 min, max; // mesh-local AABB куска
        }

        /// <summary>Имя холдера куска: стабильно между запусками (бин + компонента).</summary>
        private static string SliceHolderName(string baseName, SliceBox sl)
        {
            string n = baseName + "_S" + sl.bin.ToString("00");
            if (sl.multi) n += "C" + sl.sub;
            return n;
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
        /// Нарезать меш вдоль локальной оси slab'ами. Внутри slab'а содержимое делится на
        /// связные компоненты (общие вершины треугольников): каждая — своим боксом.
        /// U-образная труба даст 2 бокса на стойки, пустота между ними НЕ заливается;
        /// сплошная плита — 1 бокс на slab. Источники точек: вершины внутри + клиппинг
        /// треугольников к slab'ам (длинные квады накрываются, дыр вдоль оси нет).
        /// Вдоль оси кусок растягивается на весь slab — куски стыкуются без щелей.
        /// Slab'ы без геометрии (проёмы) пропускаются. Null, если резать нечего.
        /// Бины и порядок компонент детерминированы — имена кусков стабильны.
        /// </summary>
        private static List<SliceBox> SlicePlan(Mesh mesh, int axis, float stepLocal, float tolWorld, Vector3 worldPerLocal)
        {
            if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0) return null;
            if (axis < 0 || axis > 2) axis = 0;
            Bounds b = mesh.bounds;
            float len = b.size[axis];
            if (len <= Epsilon || stepLocal <= Epsilon) return null;
            int k = Mathf.CeilToInt(len / stepLocal);
            if (k <= 1) return null;
            if (k > 256) k = 256; // защита от миллиметрового шага на стометровке (корабли: 108 м / 0.5 = 216)
            float step = len / k; // нормируем: ровные slab'ы ровно покрывают bounds

            Vector3[] verts;
            int[] tris;
            try { verts = mesh.vertices; tris = mesh.triangles; }
            catch { return null; } // нет Read/Write — резать нечем

            float minA = b.min[axis];
            float qeps = Mathf.Max(len, Epsilon) * 1e-4f; // квант сварки вершин (UV-швы и т.п.)
            var frags = new SlabFrag[k];
            for (int i = 0; i < k; i++) frags[i] = new SlabFrag();

            // 1) Вершины по бинам (одиночные элементы; совпадающие сварются по ключу).
            foreach (var v in verts)
            {
                int idx = Mathf.Clamp((int)((v[axis] - minA) / step), 0, k - 1);
                frags[idx].Add(Quant(v, qeps), v);
            }

            // 2) Треугольники: клиппинг к каждому накрываемому slab'у, кольцо — в union-find.
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= verts.Length || ib >= verts.Length || ic >= verts.Length)
                    continue;
                Vector3 a = verts[ia], d = verts[ib], e = verts[ic];
                float tmin = Mathf.Min(a[axis], Mathf.Min(d[axis], e[axis]));
                float tmax = Mathf.Max(a[axis], Mathf.Max(d[axis], e[axis]));
                int b0 = Mathf.Clamp((int)((tmin - minA) / step), 0, k - 1);
                int b1 = Mathf.Clamp((int)((tmax - minA) / step), 0, k - 1);
                for (int i = b0; i <= b1; i++)
                {
                    float lo = minA + i * step;
                    float hi = (i == k - 1) ? b.max[axis] : minA + (i + 1) * step;
                    var poly = ClipTriToSlab(a, d, e, axis, lo, hi);
                    if (poly.Count == 0) continue;
                    int prev = -1, first = -1;
                    foreach (var p in poly)
                    {
                        int id = frags[i].Add(Quant(p, qeps), p);
                        if (first < 0) first = id;
                        if (prev >= 0) frags[i].Union(prev, id);
                        prev = id;
                    }
                    if (poly.Count > 1 && prev >= 0 && first >= 0) frags[i].Union(prev, first); // замкнуть кольцо
                }
            }

            // 3) Компоненты slab'ов → куски (сортировка корней = стабильный порядок sub).
            var res = new List<SliceBox>();
            for (int i = 0; i < k; i++)
            {
                var boxes = frags[i].ExtractBoxes();
                if (boxes.Count == 0) continue; // дыра в геометрии — куска нет, и это правильно
                bool multi = boxes.Count > 1;
                boxes.Sort((x, y) =>
                {
                    int c = x.min.x.CompareTo(y.min.x);
                    if (c != 0) return c;
                    c = x.min.y.CompareTo(y.min.y);
                    if (c != 0) return c;
                    return x.min.z.CompareTo(y.min.z);
                });
                for (int sgi = 0; sgi < boxes.Count; sgi++)
                {
                    Vector3 mn = boxes[sgi].min, mx = boxes[sgi].max;
                    mn[axis] = minA + i * step; // растянуть на весь slab: стыки без щелей
                    mx[axis] = (i == k - 1) ? b.max[axis] : minA + (i + 1) * step;
                    res.Add(new SliceBox { bin = i, sub = sgi, multi = multi, min = mn, max = mx });
                }
            }
            if (res.Count <= 1) return null;
            // Адаптив: склеить подряд идущие куски с одинаковым сечением (прямые участки —
            // в 1 бокс, перегибы остаются подетально). Пустые бины разбивают серии (дыры не мостим).
            // Опорное сечение — первый кусок серии: суммарный дрейф ограничен допуском.
            if (tolWorld > 0f)
                res = CoalesceSlices(res, axis, tolWorld, worldPerLocal);
            if (res.Count <= 1) return null;
            return res;
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

        private static Vector3 Quant(Vector3 p, float q)
        {
            return new Vector3(Mathf.Round(p.x / q), Mathf.Round(p.y / q), Mathf.Round(p.z / q));
        }

        /// <summary>Фрагменты связности одного slab'а: union-find по квантованным точкам.</summary>
        private class SlabFrag
        {
            private readonly Dictionary<Vector3, int> index = new Dictionary<Vector3, int>();
            private readonly List<int> parent = new List<int>();
            private readonly List<Vector3> pts = new List<Vector3>();

            public int Add(Vector3 key, Vector3 p)
            {
                int id;
                if (!index.TryGetValue(key, out id))
                {
                    id = parent.Count;
                    index[key] = id;
                    parent.Add(id);
                    pts.Add(p);
                }
                return id;
            }

            public int Find(int a)
            {
                while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; }
                return a;
            }

            public void Union(int a, int b)
            {
                a = Find(a); b = Find(b);
                if (a == b) return;
                if (a > b) { int t = a; a = b; b = t; } // детерминированный корень
                parent[b] = a;
            }

            public struct BoxMinMax { public Vector3 min, max; }

            public List<BoxMinMax> ExtractBoxes()
            {
                var acc = new Dictionary<int, BoxMinMax>();
                for (int i = 0; i < pts.Count; i++)
                {
                    int r = Find(i);
                    BoxMinMax bm;
                    if (!acc.TryGetValue(r, out bm)) acc[r] = new BoxMinMax { min = pts[i], max = pts[i] };
                    else
                    {
                        bm.min = Vector3.Min(bm.min, pts[i]);
                        bm.max = Vector3.Max(bm.max, pts[i]);
                        acc[r] = bm;
                    }
                }
                return new List<BoxMinMax>(acc.Values);
            }
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
                    if (!f.sharedMesh.isReadable)
                    {
                        report.unreadSlices++; // нет Read/Write — падаем на обычный бокс ниже
                    }
                    else
                    {
                        // Ось — длиннейшая в ЛОКАЛЬНЫХ bounds (там же лежат вершины).
                        // Брать ось из мировых размеров нельзя: рамка повёрнута (deck: rot 270,90,0),
                        // индексы переставлены, и нарезка пойдёт поперёк сужения.
                        int longAxis = LongestAxis(f.sharedMesh.bounds.size);
                        float per = worldPerLocal[longAxis] < Epsilon ? 1f : worldPerLocal[longAxis];
                        slices = SlicePlan(f.sharedMesh, longAxis, s.sliceStep / per, Mathf.Max(0f, s.sliceAdaptTol), worldPerLocal);
                        if (slices != null) kind = 2;
                    }
                }

                // Append-режим: уже закрытые меши не дублируем.
                // Для капсул/нарезки generic-проверка по объёму не годится: старый одиночный
                // бокс покрывает меш целиком и навсегда блокировал бы смену вида. Поэтому:
                // точная сверка по детерминированным именам, предшественник сносится при создании,
                // а склеенные MG_*-боксы уважаем (не дублируем поверх мержа).
                if (skipExisting && existing != null)
                {
                    bool preciseNames = stableNames && existingNames != null && (kind == 1 || kind == 2);
                    if (preciseNames)
                    {
                        if (IsCoveredByMerged(existing, go))
                        {
                            report.skippedExists++;
                            continue;
                        }
                        if (kind == 1 && existingNames.Contains(HolderNameFor(root.transform, go.transform)))
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
                    string prefix = sliceBase + "_S";
                    var wanted = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var sl in slices)
                        wanted.Add(SliceHolderName(sliceBase, sl));
                    bool same = wanted.Count > 0;
                    if (same)
                    {
                        foreach (var w in wanted)
                            if (!existingNames.Contains(w)) { same = false; break; }
                    }
                    if (same)
                    {
                        foreach (var n in existingNames)
                        {
                            if (n.StartsWith(prefix, StringComparison.Ordinal) && !wanted.Contains(n))
                            {
                                same = false; // остались лишние старые куски — пересобрать
                                break;
                            }
                        }
                    }
                    if (same)
                    {
                        report.skippedExists++;
                        continue;
                    }
                }

                if (dryOnly)
                {
                    if (kind == 1) report.capsules++; // в dry-режиме capsules = "было бы создано капсул"
                    else if (kind == 2) { report.created += slices.Count; report.slicedMeshes++; }
                    else report.created++; // в dry-режиме created = "было бы создано"
                    continue;
                }

                items.Add(new Item { source = go, mesh = f.sharedMesh, renderer = r, worldSize = worldSize, kind = kind, slices = slices });
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
                        var sb = new Bounds();
                        sb.SetMinMax(sl.min, sl.max);
                        AddFittedBox(root, parent, sliceName, it, sb, s.minThickness, !s.shipMode);
                        index++;
                        report.created++;
                    }
                    report.slicedMeshes++;
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
        private BuildSettlementColliders.Settings settings = BuildSettlementColliders.DefaultSettings();
        private float mergeGap = 0.1f;
        private float mergeAlign = 0.02f;
        private string mergeTokens = "";
        private int mergeScopeIdx = 0;
        private Vector2 scroll;
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
                capsuleTokens = s.capsuleTokens,
                sliceTokens = s.sliceTokens,
                sliceStep = s.sliceStep,
                sliceAdaptTol = s.sliceAdaptTol,
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

            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Корни в Hierarchy (можно несколько через Shift)", EditorStyles.boldLabel);
            if (roots.Length == 0)
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

            EditorGUILayout.Space();
            settings.minWorldSize = EditorGUILayout.FloatField("Мин. размер меша (м)", settings.minWorldSize);
            settings.minThickness = EditorGUILayout.FloatField("Мин. толщина бокса (м)", settings.minThickness);
            EditorGUILayout.LabelField("Исключить по имени (через запятую)");
            settings.excludeTokens = EditorGUILayout.TextArea(settings.excludeTokens, GUILayout.MinHeight(54f));
            settings.groupByTopLevel = EditorGUILayout.Toggle("Группировать по зданиям", settings.groupByTopLevel);
            settings.skipInactive = EditorGUILayout.Toggle("Пропускать выключенные", settings.skipInactive);
            settings.repairThinBoxes = EditorGUILayout.Toggle("Чинить тонкие боксы", settings.repairThinBoxes);
            settings.shipMode = EditorGUILayout.Toggle("Режим корабля (не static)", settings.shipMode);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Некоробочные формы", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Капсулы — круглые бары (трубы, арматура) вдоль длинной оси. Нарезка — кривые меши кусками по вершинам.", EditorStyles.miniLabel);
            settings.capsuleTokens = EditorGUILayout.TextField("Капсулы по именам", settings.capsuleTokens ?? "");
            settings.sliceTokens = EditorGUILayout.TextField("Нарезать по именам", settings.sliceTokens ?? "");
            settings.sliceStep = EditorGUILayout.FloatField("Шаг нарезки (м)", settings.sliceStep);
            settings.sliceAdaptTol = EditorGUILayout.FloatField("Допуск слияния кусков (м)", settings.sliceAdaptTol);

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(roots.Length == 0);
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Только эти имена (через запятую)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Пусто = обычный режим выше. Напр.: TABLE, CHAIR — боксы только там, где имя содержит токен.", EditorStyles.miniLabel);
            settings.includeTokens = EditorGUILayout.TextArea(settings.includeTokens ?? "", GUILayout.MinHeight(36f));

            EditorGUI.BeginDisabledGroup(roots.Length == 0 || string.IsNullOrWhiteSpace(settings.includeTokens));
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Объединить соседние боксы", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Склеивает боксы встык/внахлёст в пределах зазора — из 1000 плиток пола делает 1. Только внутри генерации.", EditorStyles.miniLabel);
            mergeGap = EditorGUILayout.FloatField("Макс. зазор шва (м)", mergeGap);
            mergeAlign = EditorGUILayout.FloatField("Точность совпадения граней (м)", mergeAlign);
            mergeScopeIdx = EditorGUILayout.Popup("Что клеить", mergeScopeIdx, MergeScopeLabels);
            EditorGUILayout.LabelField("Только эти имена (пусто = все сгенерированные)");
            mergeTokens = EditorGUILayout.TextArea(mergeTokens ?? "", GUILayout.MinHeight(28f));

            EditorGUI.BeginDisabledGroup(roots.Length == 0);
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

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Бокс на каждый значимый меш. Мелочь (болты) режется размером и именами. " +
                "Тонкие палубы утолщаются вниз до мин. толщины — топ заподлицо, корабли не проваливаются. " +
                "Наклонные плоские (скаты, рампы) покрываются повёрнутыми боксами 1-в-1 — рамка копирует поворот. " +
                "Круглые бары (PIPE, REBAR) — капсулы вдоль длинной оси. Кривые меши — нарезка кусками по вершинам. " +
                "Сужающийся нос (Deck_Main_Bow) — нарезкой вдоль оси + режим корабля (не static), иначе углы бокса будут цеплять. " +
                "Повторный Build пересобирает '<Root>_Colliders' с нуля. " +
                "Режим 'только совпадения': в поле ниже укажи TABLE (или TABLE, CHAIR) — отдельный старт " +
                "ДОБАВИТ боксы только там, где имя содержит токен, существующие не трогает и не дублирует " +
                "(повторный запуск пропускает уже закрытые: alreadyExists); исключения сверху при этом тоже действуют.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }
    }
}
