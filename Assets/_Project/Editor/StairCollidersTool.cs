using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Секция «ЛЕСТНИЦЫ»: коллайдеры для маршей. Три независимых способа, все работают по
    /// ВЫДЕЛЕНИЮ (дети выделенных объектов не обходятся):
    ///
    ///   1) <b>Марш → 1 наклонный бокс.</b> Выделен один меш марша. Ступени распознаются по
    ///      верхним горизонтальным граням, по их уровням МНК-прямая даёт уклон марша, в эту
    ///      плоскость ставится один наклонный BoxCollider длиной в марш и шириной в ступень.
    ///      Дёшево для PhysX: 1 AABB вместо N треугольников, годится и игроку
    ///      (CharacterController), и кораблям.
    ///
    ///   2) <b>2 ступени → наклонный бокс.</b> Выделены первая и последняя ступень (два
    ///      объекта). Наклонный бокс строится между их верхними площадками: уклон = между
    ///      центрами верхов, ширина = по объединению обеих ступеней. Точность зависит от того,
    ///      насколько равномерны промежуточные ступени (неравномерный марш даст отклонение до
    ///      одного подступенка), зато не нужно, чтобы ступени были отдельными объектами.
    ///
    ///   3) <b>По ступеням → ряд блоков.</b> Выделен один меш марша. Ступени группируются по
    ///      высоте, и на КАЖДОМ уровне ставится свой горизонтальный блок по фактическому
    ///      пятну ступени (ширина × глубина из геометрии), верх блока заподлицо с проступью,
    ///      толщина = до уровня ступени ниже. Это «нарезка», но шаг задаёт сама геометрия —
    ///      количеств блоков = количеству ступеней.
    ///
    /// Пространство и точность:
    ///   - анализ идёт в мировых координатах (верх = Vector3.up), результат переводится в
    ///     систему холдера; боксы-дети получают мировой масштаб 1, поэтому их size — в МЕТРАХ;
    ///   - холдер <c>&lt;Имя&gt;_STAIR</c> — ДОЧЕРНИЙ объект исходника с единичными local TRS, т.е.
    ///     на мировой рамке исходника (как боксы BuildSettlementColliders), поэтому переимпорт FBX
    ///     и перемещение города (Floating Origin) не разъезжаются с визуалом, а в иерархии
    ///     коллайдер лежит ровно под тем мешем, для которого его собрали;
    ///   - два пути чтения геометрии: точные вершины/индексы (если Read/Write включён или меш
    ///     сгенерирован) и СКАН лучами по временному MeshCollider для FBX с выключенным
    ///     Read/Write — в этой версии Unity данные такого меша недоступны вообще
    ///     (mesh.vertices = 0, AcquireReadOnlyMeshData бросает InvalidOperationException),
    ///     а кукать MeshCollider из него можно, поэтому форма снимается лучами.
    ///
    /// Идемпотентно: холдер того же имени пересобирается целиком, дублей не остаётся.
    /// </summary>
    public static class StairCollidersTool
    {
        /// <summary>Суффикс объекта-холдера рядом с исходником.</summary>
        public const string HolderSuffix = "_STAIR";

        /// <summary>Толщина бокса марша по умолчанию (м), как StairThickness у BASE.</summary>
        public const float DefaultThickness = 0.8f;

        /// <summary>Допуск группировки ступеней по высоте (м). Подступенок 0.17–0.19 м, запас 2x.</summary>
        private const float LevelTolerance = 0.06f;

        /// <summary>Минимальная |n.y| горизонтальной грани, чтобы считать её проступью.</summary>
        private const float UpNormalMinY = 0.90f;

        /// <summary>Отсев мусорных уровней (м²).</summary>
        private const float MinLevelArea = 0.02f;

        /// <summary>Запас вниз по маршу (м), закрывает шов у пола.</summary>
        private const float PadLow = 0.10f;

        /// <summary>Минимальная глубина пятна ступени (м) — мельче в блоки не берём.</summary>
        private const float MinStepFootprint = 0.02f;

        /// <summary>Толщина марша, настраивается из окна (м).</summary>
        public static float Thickness = DefaultThickness;

        // ------------------------------------------------------------ меню

        [MenuItem("Tools/ProjectC/Colliders/Лестница 1 — марш → наклонный бокс", false, 33)]
        public static void MenuFlightRamp() => BuildFlightRamp();

        [MenuItem("Tools/ProjectC/Colliders/Лестница 2 — две ступени → наклонный бокс", false, 34)]
        public static void MenuBetweenSteps() => BuildBetweenTwoSteps();

        [MenuItem("Tools/ProjectC/Colliders/Лестница 3 — по ступеням → ряд блоков", false, 35)]
        public static void MenuPerStep() => BuildPerStepBlocks();

        [MenuItem("Tools/ProjectC/Colliders/Снести STAIR — по выделению", false, 36)]
        public static void MenuRemove() => RemoveForSelection();

        // ------------------------------------------------------------ данные анализа

        private struct Face
        {
            public Vector3 A, B, C;
            public Vector3 Center;
            public float Area;
            public float Y;       // высота центра
            public float TopY;    // максимальная вершина грани
        }

        private struct Level
        {
            public float Height;              // верх ступени, мир
            public float Area;                // суммарная площадь граней уровня
            public float MinU, MaxU;          // пятно в осях марша
            public float MinW, MaxW;
        }

        private sealed class Flight
        {
            public Transform Src;
            public Vector3 U;                 // подъём (горизонтальная единичная)
            public Vector3 V;                 // поперёк марша (горизонтальная единичная)
            public float Slope;               // dH/dU
            public float PlaneOffset;         // H(U) = Slope*U + PlaneOffset
            public float MinU, MaxU, MinW, MaxW;
            public List<Level> Levels = new List<Level>();
            public bool DownNormals;
            public string Note;
        }

        // ------------------------------------------------------------ 1) марш → 1 наклонный бокс

        public static void BuildFlightRamp()
        {
            if (!RequireSelection(out GameObject[] selection)) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Stair ramp collider");

            var log = new StringBuilder();
            int built = 0, failed = 0;

            foreach (GameObject go in selection)
            {
                if (go == null) continue;

                string why;
                Flight f = Analyze(go.transform, out why);
                if (f == null)
                {
                    failed++;
                    log.AppendLine($"  '{go.name}': {why}");
                    continue;
                }

                Transform holder = RebuildHolder(go.transform, go.name + HolderSuffix, out bool rebuilt);

                float length = (f.MaxU - f.MinU + PadLow) * Mathf.Sqrt(1f + f.Slope * f.Slope);
                float width = f.MaxW - f.MinW;
                float thickness = Mathf.Max(0.05f, Thickness);

                Vector3 u = f.U;
                Vector3 runDir = (u + Vector3.up * f.Slope).normalized;
                Vector3 upNormal = (-u * f.Slope + Vector3.up).normalized;
                Vector3 across = Vector3.Cross(Vector3.up, u).normalized;

                float uCenter = (f.MinU - PadLow + f.MaxU) * 0.5f;
                float wCenter = (f.MinW + f.MaxW) * 0.5f;
                Vector3 surface = u * uCenter + f.V * wCenter + Vector3.up * (f.Slope * uCenter + f.PlaneOffset);

                AddBox(holder, go, "RAMPS", 0, surface - upNormal * (thickness * 0.5f),
                    Quaternion.LookRotation(across, upNormal), new Vector3(length, thickness, width));

                float angle = Mathf.Atan(Mathf.Abs(f.Slope)) * Mathf.Rad2Deg;
                log.AppendLine($"  '{go.name}': 1 наклонный бокс, уклон {angle:F1}°, длина {length:F2} м, " +
                               $"ширина {width:F2} м, толщина {thickness:F2} м, ступеней {f.Levels.Count}" +
                               $"{(f.DownNormals ? " (нормали инвертированы)" : "")}" +
                               $"{(rebuilt ? ", холдер пересобран" : "")}.{f.Note}");
                built++;
            }

            Finish(log, "марш → 1 наклонный бокс", built, failed);
        }

        // ------------------------------------------------------------ 2) две ступени → наклонный бокс

        public static void BuildBetweenTwoSteps()
        {
            if (!RequireSelection(out GameObject[] selection)) return;

            var withMesh = new List<GameObject>();
            foreach (GameObject go in selection)
                if (go != null && go.GetComponent<MeshFilter>() != null &&
                    go.GetComponent<MeshFilter>().sharedMesh != null)
                    withMesh.Add(go);

            if (withMesh.Count != 2)
            {
                Debug.LogError($"[StairTool] Для режима «2 ступени» нужно выделить РОВНО 2 объекта с мешем " +
                               $"(первая и последняя ступень), сейчас подходящих: {withMesh.Count}.");
                return;
            }

            Bounds a = WorldAabb(withMesh[0]);
            Bounds b = WorldAabb(withMesh[1]);
            GameObject lowGo = a.center.y <= b.center.y ? withMesh[0] : withMesh[1];
            GameObject hiGo = a.center.y <= b.center.y ? withMesh[1] : withMesh[0];

            Vector3[] wvLow = WorldVerts(lowGo.transform) ?? ScannedVerts(lowGo.transform);
            Vector3[] wvHigh = WorldVerts(hiGo.transform) ?? ScannedVerts(hiGo.transform);
            if (wvLow == null || wvHigh == null)
            {
                Debug.LogError("[StairTool] Не удалось получить геометрию одной из ступеней — режим 2 невозможен.");
                return;
            }

            // Верх ступени берём по фактическим вершинам (AABB повёрнутой ступени завышает габарит).
            Vector3 topLow, topHigh;
            if (!TopPoint(wvLow, out topLow) || !TopPoint(wvHigh, out topHigh))
            {
                Debug.LogError("[StairTool] Не удалось определить верх ступени.");
                return;
            }

            Vector3 delta = new Vector3(topHigh.x - topLow.x, 0f, topHigh.z - topLow.z);
            if (delta.magnitude < 0.05f)
            {
                Debug.LogError("[StairTool] Ступени стоят друг над другом (нет горизонтального смещения) — " +
                               "наклонную плоскость построить нельзя. Выбери первую и последнюю ступень марша.");
                return;
            }

            Vector3 u = delta.normalized;
            Vector3 v = Vector3.Cross(Vector3.up, u).normalized;

            float du = Vector3.Dot(topHigh - topLow, u);
            if (Mathf.Abs(du) < 1e-3f)
            {
                Debug.LogError("[StairTool] Нулевой прогон между ступенями — проверь выделение.");
                return;
            }
            float slope = (topHigh.y - topLow.y) / du;
            float planeOffset = topLow.y - slope * Vector3.Dot(topLow, u);

            // Габарит по осям марша: объединение вершин обеих ступеней.
            float minU = float.MaxValue, maxU = float.MinValue, minW = float.MaxValue, maxW = float.MinValue;
            AccumulateVerts(wvLow, u, v, ref minU, ref maxU, ref minW, ref maxW);
            AccumulateVerts(wvHigh, u, v, ref minU, ref maxU, ref minW, ref maxW);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Stair ramp collider (2 steps)");
            Transform holder = RebuildHolder(lowGo.transform, lowGo.name + HolderSuffix, out bool rebuilt);

            float length = (maxU - minU) * Mathf.Sqrt(1f + slope * slope);
            float width = maxW - minW;
            float thickness = Mathf.Max(0.05f, Thickness);

            Vector3 runDir = (u + Vector3.up * slope).normalized;
            Vector3 upNormal = (-u * slope + Vector3.up).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, u).normalized;
            float uCenter = (minU + maxU) * 0.5f;
            float wCenter = (minW + maxW) * 0.5f;
            Vector3 surface = u * uCenter + v * wCenter + Vector3.up * (slope * uCenter + planeOffset);

            AddBox(holder, lowGo, "RAMPS", 0, surface - upNormal * (thickness * 0.5f),
                Quaternion.LookRotation(across, upNormal), new Vector3(length, thickness, width));

            float angle = Mathf.Atan(Mathf.Abs(slope)) * Mathf.Rad2Deg;
            Debug.Log($"[StairTool] 2 ступени → наклонный бокс: уклон {angle:F1}°, длина {length:F2} м, " +
                      $"ширина {width:F2} м, толщина {thickness:F2} м, " +
                      $"между '{lowGo.name}' и '{(lowGo == withMesh[0] ? withMesh[1].name : withMesh[0].name)}'" +
                      $"{(rebuilt ? ", холдер пересобран" : "")}.");
        }

        // ------------------------------------------------------------ 3) по ступеням → ряд блоков

        public static void BuildPerStepBlocks()
        {
            if (!RequireSelection(out GameObject[] selection)) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Stair step blocks");

            var log = new StringBuilder();
            int built = 0, failed = 0;

            foreach (GameObject go in selection)
            {
                if (go == null) continue;

                string why;
                Flight f = Analyze(go.transform, out why);
                if (f == null)
                {
                    failed++;
                    log.AppendLine($"  '{go.name}': {why}");
                    continue;
                }

                Transform holder = RebuildHolder(go.transform, go.name + HolderSuffix, out bool rebuilt);

                int idx = 0, skipped = 0;
                float prevTop = float.NaN;
                float minTop = float.MaxValue, maxTop = float.MinValue;

                foreach (Level lv in f.Levels)
                {
                    float run = lv.MaxU - lv.MinU;
                    float wide = lv.MaxW - lv.MinW;
                    if (lv.Area < MinLevelArea || run < MinStepFootprint || wide < MinStepFootprint)
                    {
                        skipped++;
                        continue;
                    }

                    float th;
                    if (float.IsNaN(prevTop)) th = Mathf.Max(0.15f, Thickness);
                    else th = Mathf.Clamp(lv.Height - prevTop + 0.03f, 0.15f, Mathf.Max(0.15f, Thickness));

                    float uCenter = (lv.MinU + lv.MaxU) * 0.5f;
                    float wCenter = (lv.MinW + lv.MaxW) * 0.5f;
                    Vector3 center = f.U * uCenter + f.V * wCenter +
                                     Vector3.up * (lv.Height - th * 0.5f);

                    AddBox(holder, go, "STEP", idx,
                        center,
                        Quaternion.LookRotation(f.V, Vector3.up),
                        new Vector3(run, th, wide));

                    minTop = Mathf.Min(minTop, lv.Height);
                    maxTop = Mathf.Max(maxTop, lv.Height);
                    prevTop = lv.Height;
                    idx++;
                }

                if (idx == 0)
                {
                    failed++;
                    log.AppendLine($"  '{go.name}': пятна ступеней не найдены (пропущено уровней: {skipped}).");
                    continue;
                }

                log.AppendLine($"  '{go.name}': {idx} блоков по ступеням (пропущено уровней: {skipped}), " +
                               $"низ {minTop:F2} м, верх {maxTop:F2} м, подъём {maxTop - minTop:F2} м" +
                               $"{(rebuilt ? ", холдер пересобран" : "")}.");
                built++;
            }

            Finish(log, "по ступеням → ряд блоков", built, failed);
        }

        // ------------------------------------------------------------ снос

        public static void RemoveForSelection()
        {
            if (!RequireSelection(out GameObject[] selection)) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Remove stair collider");

            int removed = 0, none = 0;
            foreach (GameObject go in selection)
            {
                if (go == null) continue;
                Transform holder = FindHolder(go.transform);
                if (holder == null) { none++; continue; }
                Undo.DestroyObjectImmediate(holder.gameObject);
                removed++;
            }

            Debug.Log($"[StairTool] снесено STAIR-холдеров: {removed}, без холдера: {none}.");
        }

        // ------------------------------------------------------------ анализ марша

        /// <summary>
        /// Разбор меша марша: верхние грани → уровни ступеней → оси марша, уклон (МНК) и
        /// габариты пятна. Всё в мире. Если ступени не нашлись по нормалям «вверх», пробуем
        /// инвертированные нормали (у части FBX-мешей обмотка обратная).
        /// </summary>
        private static Flight Analyze(Transform src, out string why)
        {
            why = null;

            var mf = src.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { why = "нет MeshFilter/меша"; return null; }

            Mesh mesh = mf.sharedMesh;
            Vector3[] lv;
            int[] tris;

            // Путь 1 — геометрия доступна (Read/Write включён либо меш сгенерирован):
            // точные вершины и треугольники.
            if (BuildSettlementColliders.TryReadMeshData(mesh, out lv, out tris) &&
                lv != null && lv.Length > 0 && tris != null && tris.Length >= 9)
            {
                Matrix4x4 m = src.localToWorldMatrix;
                var w = new Vector3[lv.Length];
                for (int i = 0; i < lv.Length; i++) w[i] = m.MultiplyPoint3x4(lv[i]);

                for (int pass = 0; pass < 2; pass++)
                {
                    bool down = pass == 1;
                    List<Face> faces = CollectUpFaces(w, tris, down);
                    if (faces.Count < 2) continue;

                    List<List<Face>> groups = ClusterLevels(faces);
                    if (groups.Count < 2) continue;

                    Flight f = BuildFlight(src, groups, down, 0f);
                    if (f != null) return f;
                }
            }

            // Путь 2 — FBX с Read/Write = Off: данные вершин недоступны вообще
            // (и mesh.vertices = 0, и AcquireReadOnlyMeshData бросает InvalidOperationException).
            // Берём форму поверхности лучами по временному MeshCollider: он кукается из того же меша
            // без Read/Write, а лучи дают те же проступи/уклон.
            float pad;
            List<Face> scan = ScanSurface(src, out pad);
            if (scan != null && scan.Count >= 2)
            {
                List<List<Face>> groups = ClusterLevels(scan);
                if (groups.Count >= 2)
                {
                    Flight f = BuildFlight(src, groups, false, pad);
                    if (f != null)
                    {
                        f.Note = $" [скан поверхности шагом {pad * 2f:F3} м: меш без Read/Write]";
                        return f;
                    }
                }
            }

            why = "ступени не распознаны (нужно 2+ горизонтальных уровня проступей)";
            return null;
        }

        /// <summary>
        /// Скан поверхности временным MeshCollider: сетка вертикальных лучей по мировому габариту,
        /// попадания группируются по высоте так же, как верхние грани при точном пути.
        /// Возвращает «псевдо-грани» (вырожденные треугольники в точке попадания) и половину шага сетки
        /// в <paramref name="pad"/> — на неё расширяются пятна, чтобы кромка не съедалась дискретизацией.
        ///
        /// Луч пускается ПО САМОМУ КОЛЛАЙДЕРУ (<see cref="Collider.Raycast"/>), а не через
        /// Physics.RaycastNonAlloc по всему миру: у лучей, идущих от габарита вниз, под маршем всегда
        /// лежат Terrain_0_0, BC_*_ADM_PLINTH, BC_*_ADM_F1_FLOOR и DECK, а порядок результатов
        /// NonAlloc не определён — при буфере в 4 записи нужный коллайдер в него просто не попадал
        /// и скан возвращал 0 попаданий (именно так отваливались реальные марши ADMIN).
        /// </summary>
        private static List<Face> ScanSurface(Transform src, out float pad)
        {
            pad = 0f;

            var mf = src.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;

            var rd = src.GetComponent<Renderer>();
            Bounds b = rd != null ? rd.bounds : new Bounds(src.position, Vector3.one * 2f);
            if (b.size.x <= 0f || b.size.z <= 0f || b.size.y <= 0f) return null;

            var tmp = new GameObject("__stair_scan__");
            try
            {
                tmp.layer = src.gameObject.layer;
                tmp.transform.SetPositionAndRotation(src.position, src.rotation);
                tmp.transform.localScale = src.lossyScale;

                var mc = tmp.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                Physics.SyncTransforms();

                float step = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) / 64f, 0.01f, 0.05f);
                double cells = ((double)b.size.x / step + 1.0) * ((double)b.size.z / step + 1.0);
                if (cells > 200000.0) step = Mathf.Sqrt((float)((double)b.size.x * b.size.z / 200000.0));
                pad = step * 0.5f;

                var res = new List<Face>();
                float topY = b.max.y + 1f;
                float dist = b.size.y + 4f;

                for (float x = b.min.x + step * 0.5f; x <= b.max.x; x += step)
                {
                    for (float z = b.min.z + step * 0.5f; z <= b.max.z; z += step)
                    {
                        RaycastHit hit;
                        if (!mc.Raycast(new Ray(new Vector3(x, topY, z), Vector3.down), out hit, dist)) continue;

                        Vector3 p = hit.point;
                        res.Add(new Face
                        {
                            A = p, B = p, C = p,
                            Center = p,
                            Area = step * step,
                            Y = p.y,
                            TopY = p.y
                        });
                    }
                }

                return res.Count >= 2 ? res : null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tmp);
            }
        }

        private static List<Face> CollectUpFaces(Vector3[] w, int[] tris, bool down)
        {
            var list = new List<Face>();
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 A = w[tris[i]], B = w[tris[i + 1]], C = w[tris[i + 2]];
                Vector3 cr = Vector3.Cross(B - A, C - A);
                float len = cr.magnitude;
                if (len < 1e-7f) continue;
                float ny = cr.y / len;
                if (down ? ny > -UpNormalMinY : ny < UpNormalMinY) continue;

                list.Add(new Face
                {
                    A = A, B = B, C = C,
                    Center = (A + B + C) / 3f,
                    Area = len * 0.5f,
                    Y = (A.y + B.y + C.y) / 3f,
                    TopY = Mathf.Max(A.y, Mathf.Max(B.y, C.y))
                });
            }
            return list;
        }

        /// <summary>Группировка проступей по высоте: соседние в пределах LevelTolerance — один уровень.</summary>
        private static List<List<Face>> ClusterLevels(List<Face> faces)
        {
            var sorted = new List<Face>(faces);
            sorted.Sort((x, y) => x.Y.CompareTo(y.Y));

            var groups = new List<List<Face>>();
            List<Face> cur = null;
            float curY = float.NaN;

            foreach (Face f in sorted)
            {
                if (cur == null || Mathf.Abs(f.Y - curY) > LevelTolerance)
                {
                    cur = new List<Face>();
                    groups.Add(cur);
                    curY = f.Y;
                }
                cur.Add(f);
            }
            return groups;
        }

        private static Flight BuildFlight(Transform src, List<List<Face>> groups, bool down, float pad)
        {
            // 1) высота и горизонтальный центр каждого уровня (по площади граней)
            var heights = new float[groups.Count];
            var cx = new float[groups.Count];
            var cz = new float[groups.Count];

            for (int i = 0; i < groups.Count; i++)
            {
                float aSum = 0f, ySum = 0f, xSum = 0f, zSum = 0f;
                foreach (Face f in groups[i])
                {
                    aSum += f.Area;
                    ySum += f.Y * f.Area;
                    xSum += f.Center.x * f.Area;
                    zSum += f.Center.z * f.Area;
                }
                if (aSum < 1e-9f) return null;
                heights[i] = ySum / aSum;
                cx[i] = xSum / aSum;
                cz[i] = zSum / aSum;
            }

            // 2) ось подъёма: от нижнего уровня к верхнему (надёжнее PCA на коротких маршах)
            Vector3 d = new Vector3(cx[cx.Length - 1] - cx[0], 0f, cz[cx.Length - 1] - cz[0]);
            Vector3 u;
            string note = "";

            if (d.magnitude < 0.05f)
            {
                // Марш-«поворот» или вертикальная раскладка: берём главную ось облака центров.
                u = PrincipalHorizontalAxis(cx, cz);
                note = " (ось по облаку уровней: центры совпали по горизонтали)";
            }
            else u = d.normalized;

            if (u.sqrMagnitude < 1e-8f) return null;
            Vector3 v = Vector3.Cross(Vector3.up, u).normalized;

            // 3) уклон МНК по уровням: H = Slope*U + PlaneOffset
            float wSum = 0f, uMean = 0f, hMean = 0f;
            var uArr = new float[groups.Count];
            for (int i = 0; i < groups.Count; i++)
            {
                float a = 0f;
                foreach (Face f in groups[i]) a += f.Area;
                uArr[i] = u.x * cx[i] + u.z * cz[i];
                wSum += a;
                uMean += uArr[i] * a;
                hMean += heights[i] * a;
            }
            if (wSum < 1e-9f) return null;
            uMean /= wSum;
            hMean /= wSum;

            float num = 0f, den = 0f;
            for (int i = 0; i < groups.Count; i++)
            {
                float a = 0f;
                foreach (Face f in groups[i]) a += f.Area;
                float du = uArr[i] - uMean;
                num += a * du * (heights[i] - hMean);
                den += a * du * du;
            }

            if (den < 1e-6f) return null;   // все уровни на одной высоте — это не марш
            float slope = num / den;

            var fl = new Flight
            {
                Src = src,
                U = u,
                V = v,
                Slope = slope,
                PlaneOffset = hMean - slope * uMean,
                DownNormals = down,
                Note = note,
                MinU = float.MaxValue,
                MaxU = float.MinValue,
                MinW = float.MaxValue,
                MaxW = float.MinValue
            };

            // 4) пятно каждого уровня и общий габарит
            for (int i = 0; i < groups.Count; i++)
            {
                float minU = float.MaxValue, maxU = float.MinValue, minW = float.MaxValue, maxW = float.MinValue;
                float area = 0f;

                foreach (Face f in groups[i])
                {
                    Vector3[] p = { f.A, f.B, f.C };
                    foreach (Vector3 pt in p)
                    {
                        float pu = Vector3.Dot(pt, u);
                        float pw = Vector3.Dot(pt, v);
                        if (pu < minU) minU = pu;
                        if (pu > maxU) maxU = pu;
                        if (pw < minW) minW = pw;
                        if (pw > maxW) maxW = pw;
                    }
                    area += f.Area;
                }

                fl.Levels.Add(new Level
                {
                    Height = heights[i],
                    Area = area,
                    MinU = minU - pad,
                    MaxU = maxU + pad,
                    MinW = minW - pad,
                    MaxW = maxW + pad
                });

                if (minU - pad < fl.MinU) fl.MinU = minU - pad;
                if (maxU + pad > fl.MaxU) fl.MaxU = maxU + pad;
                if (minW - pad < fl.MinW) fl.MinW = minW - pad;
                if (maxW + pad > fl.MaxW) fl.MaxW = maxW + pad;
            }

            if (fl.MinU > fl.MaxU || fl.MinW > fl.MaxW) return null;
            return fl;
        }

        private static Vector3 PrincipalHorizontalAxis(float[] cx, float[] cz)
        {
            float mx = 0f, mz = 0f;
            for (int i = 0; i < cx.Length; i++) { mx += cx[i]; mz += cz[i]; }
            mx /= cx.Length;
            mz /= cz.Length;

            float sxx = 0f, szz = 0f, sxz = 0f;
            for (int i = 0; i < cx.Length; i++)
            {
                float dx = cx[i] - mx, dz = cz[i] - mz;
                sxx += dx * dx;
                szz += dz * dz;
                sxz += dx * dz;
            }

            // собственный вектор наибольшего собственного значения 2x2
            float tr = sxx + szz;
            float det = sxx * szz - sxz * sxz;
            float l1 = tr * 0.5f + Mathf.Sqrt(Mathf.Max(0f, tr * tr * 0.25f - det));
            float ax = sxz, az = l1 - sxx;
            if (Mathf.Abs(ax) + Mathf.Abs(az) < 1e-6f) { ax = l1 - szz; az = sxz; }
            var v = new Vector3(ax, 0f, az);
            return v.sqrMagnitude < 1e-12f ? Vector3.right : v.normalized;
        }

        private static void AccumulateVerts(Vector3[] wv, Vector3 u, Vector3 v,
            ref float minU, ref float maxU, ref float minW, ref float maxW)
        {
            foreach (Vector3 p in wv)
            {
                float pu = Vector3.Dot(p, u);
                float pw = Vector3.Dot(p, v);
                if (pu < minU) minU = pu;
                if (pu > maxU) maxU = pu;
                if (pw < minW) minW = pw;
                if (pw > maxW) maxW = pw;
            }
        }

        /// <summary>Мировые вершины меша (читаются и на non-readable FBX-мешах).</summary>
        private static Vector3[] WorldVerts(Transform t)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;

            Vector3[] lv;
            int[] tris;
            if (!BuildSettlementColliders.TryReadMeshData(mf.sharedMesh, out lv, out tris)) return null;
            if (lv == null || lv.Length == 0) return null;

            Matrix4x4 m = t.localToWorldMatrix;
            var w = new Vector3[lv.Length];
            for (int i = 0; i < lv.Length; i++) w[i] = m.MultiplyPoint3x4(lv[i]);
            return w;
        }

        /// <summary>
        /// Фолбэк для Read/Write = Off: точки попаданий вертикальных лучей по временному MeshCollider.
        /// По ним есть и верх ступени, и её пятно в плане.
        /// </summary>
        private static Vector3[] ScannedVerts(Transform t)
        {
            float pad;
            List<Face> faces = ScanSurface(t, out pad);
            if (faces == null || faces.Count == 0) return null;

            var arr = new Vector3[faces.Count];
            for (int i = 0; i < faces.Count; i++) arr[i] = faces[i].Center;
            return arr;
        }

        /// <summary>
        /// Верхняя площадка ступени: высота самой верхней вершины и горизонтальный центр пятна
        /// этой вершины (а не центр AABB — у повёрнутой ступени они расходятся).
        /// </summary>
        private static bool TopPoint(Vector3[] wv, out Vector3 top)
        {
            top = Vector3.zero;
            if (wv == null || wv.Length == 0) return false;

            float maxY = float.MinValue;
            foreach (Vector3 p in wv) if (p.y > maxY) maxY = p.y;

            const float eps = 2e-3f;
            int count = 0;
            double sx = 0, sz = 0;
            foreach (Vector3 p in wv)
            {
                if (p.y < maxY - eps) continue;
                sx += p.x; sz += p.z; count++;
            }
            if (count == 0)
            {
                double ax = 0, az = 0;
                foreach (Vector3 p in wv) { ax += p.x; az += p.z; }
                top = new Vector3((float)(ax / wv.Length), maxY, (float)(az / wv.Length));
                return true;
            }

            top = new Vector3((float)(sx / count), maxY, (float)(sz / count));
            return true;
        }

        private static Bounds WorldAabb(GameObject go)
        {
            var rd = go.GetComponent<Renderer>();
            if (rd != null) return rd.bounds;

            var mf = go.GetComponent<MeshFilter>();
            Bounds lb = mf.sharedMesh.bounds;
            Vector3 c = go.transform.TransformPoint(lb.center);
            Vector3 e = lb.extents;
            Vector3 sx = go.transform.TransformVector(new Vector3(e.x, 0f, 0f));
            Vector3 sy = go.transform.TransformVector(new Vector3(0f, e.y, 0f));
            Vector3 sz = go.transform.TransformVector(new Vector3(0f, 0f, e.z));
            Vector3 ext = new Vector3(
                Mathf.Abs(sx.x) + Mathf.Abs(sy.x) + Mathf.Abs(sz.x),
                Mathf.Abs(sx.y) + Mathf.Abs(sy.y) + Mathf.Abs(sz.y),
                Mathf.Abs(sx.z) + Mathf.Abs(sy.z) + Mathf.Abs(sz.z));
            return new Bounds(c, ext * 2f);
        }

        // ------------------------------------------------------------ холдеры и боксы

        /// <summary>Холдер = мировая рамка исходника, но ДОЧЕРНИЙ объект (как боксы BuildSettlementColliders).</summary>
        private static Transform RebuildHolder(Transform src, string name, out bool rebuilt)
        {
            rebuilt = false;

            Transform existing = FindHolder(src);
            if (existing != null && existing.name != name)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                existing = null;
            }
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                rebuilt = true;
            }

            var holder = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(holder, "Create stair collider");
            holder.layer = src.gameObject.layer;
            holder.isStatic = src.gameObject.isStatic;

            Transform ht = holder.transform;
            ht.SetParent(src, true);              // под выбранный меш, а не под главный FBX
            ht.localPosition = Vector3.zero;
            ht.localRotation = Quaternion.identity;
            ht.localScale = Vector3.one;

            Vector3 s = ht.lossyScale;
            if (Mathf.Abs(s.x - s.y) > 1e-3f * Mathf.Max(1f, s.y) ||
                Mathf.Abs(s.x - s.z) > 1e-3f * Mathf.Max(1f, s.z))
                Debug.LogWarning($"[StairTool] '{src.name}': немасштабный масштаб ({s}) — " +
                                 $"повёрнутые боксы могут исказиться; для точного результата масштаб нужен равномерный.");

            return ht;
        }

        private static void AddBox(Transform holder, GameObject src, string tag, int index,
            Vector3 worldCenter, Quaternion worldRotation, Vector3 sizeMeters)
        {
            string nm = tag == "RAMPS"
                ? holder.name + "_RAMP"
                : $"{holder.name}_{tag}_{index + 1:00}";

            var b = new GameObject(nm);
            Undo.RegisterCreatedObjectUndo(b, "Create stair box");
            b.layer = src.layer;
            b.isStatic = src.isStatic;

            Transform bt = b.transform;
            bt.SetParent(holder, false);
            bt.localScale = Divide(Vector3.one, holder.lossyScale);   // мировой масштаб 1 → size в метрах
            bt.SetPositionAndRotation(worldCenter, worldRotation);

            var bc = b.AddComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = new Vector3(Mathf.Max(0.01f, sizeMeters.x),
                                  Mathf.Max(0.01f, sizeMeters.y),
                                  Mathf.Max(0.01f, sizeMeters.z));
            bc.isTrigger = false;
        }

        /// <summary>
        /// Холдер по имени &lt;Имя&gt;_STAIR: сначала ДОЧЕРНИЙ (текущая схема), затем СОСЕД (прежние сборки),
        /// затем корень сцены. Если выделили сам холдер — считаем, что нашли его.
        /// </summary>
        private static Transform FindHolder(Transform src)
        {
            if (src == null) return null;
            string name = src.name.EndsWith(HolderSuffix) ? src.name : src.name + HolderSuffix;

            Transform child = src.Find(name);
            if (child != null) return child;

            Transform parent = src.parent;
            if (parent != null)
            {
                Transform sibling = parent.Find(name);
                if (sibling != null) return sibling;
            }

            return FindInScene(src, name);
        }

        /// <summary>
        /// Последний рубеж: холдер уже унесён в <c>&lt;Имя&gt;_COLLIDER</c> или в другую ветку сцены.
        /// Ищем по имени И МИРОВОЙ ПОЗИЦИИ: по одному имени нельзя — одинаковых имён в городе много,
        /// и инструмент снёс бы чужой холдер.
        /// </summary>
        private static Transform FindInScene(Transform src, string name)
        {
            var scene = src.gameObject.scene;
            if (!scene.IsValid()) return null;

            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t != src && t.name == name && (t.position - src.position).sqrMagnitude < 1e-6f)
                        return t;

            return null;
        }

        // ------------------------------------------------------------ мелочи

        private static bool RequireSelection(out GameObject[] selection)
        {
            selection = Selection.gameObjects;
            if (selection != null && selection.Length > 0) return true;

            Debug.LogError("[StairTool] Ничего не выделено. Выдели меш марша (режимы 1 и 3) " +
                           "или первую и последнюю ступень (режим 2).");
            return false;
        }

        private static void Finish(StringBuilder log, string mode, int built, int failed)
        {
            var msg = new StringBuilder();
            msg.AppendLine($"[StairTool] {mode}: объектов собрано={built}, не распознано={failed}.");
            if (log.Length > 0) msg.Append(log);
            if (built > 0) Debug.Log(msg.ToString());
            else Debug.LogError(msg.ToString());
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
