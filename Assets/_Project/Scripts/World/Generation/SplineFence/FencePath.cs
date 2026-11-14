using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Классификация узла сплайна по углу излома.
    /// Всё считается по геометрии кривой, а НЕ по TangentMode узла:
    /// художник может нарисовать 90° и в AutoSmooth (получится дуга), и в Linear (получится слом).
    /// </summary>
    public enum FenceJointKind
    {
        /// <summary>Прямая: излом ниже порога smoothAngleDeg.</summary>
        Straight = 0,

        /// <summary>Плавный изгиб: полотно идёт по кадру без разрыва.</summary>
        Smooth = 1,

        /// <summary>Резкий угол: прогон рвётся, оба конца подрезаются miter'ом, ставится угловой столб.</summary>
        Sharp = 2,

        /// <summary>Шпилька/реверс (&gt; breakAngleDeg): путь рвётся без столба, в лог уходит предупреждение.</summary>
        Reversed = 3
    }

    /// <summary>Настройки извлечения пути. Значения по умолчанию — из дизайн-ноты docs/world/spline_assets/00_DESIGN_SplineFence.md §5.</summary>
    public struct FencePathSettings
    {
        /// <summary>Допустимая стрелка прогиба на изгибе, м. Управляет шагом тесселяции.</summary>
        public float maxDeviation;

        /// <summary>Порог «резкий угол», градусы (§5.2).</summary>
        public float sharpAngleDeg;

        /// <summary>Ниже этого угла узел считается прямой — шума в отчёте не даёт.</summary>
        public float smoothAngleDeg;

        /// <summary>Выше этого угла — шпилька: путь рвётся, столб не ставится.</summary>
        public float breakAngleDeg;

        /// <summary>База метрики излома, м (§5.1). Именно в метрах, а не в нормализованном t.</summary>
        public float bendProbeMeters;

        /// <summary>Максимальный шаг кадров, м. Обычно = шаг столбов.</summary>
        public float maxStep;

        /// <summary>Минимальный шаг кадров, м — защита от бесконечного цикла на тесных дугах.</summary>
        public float minStep;

        /// <summary>Полусторона углового столба h, м. Участвует в miter-подрезке (§5.3).</summary>
        public float postHalfSize;

        /// <summary>Зазор между торцом прогона и гранью столба, м.</summary>
        public float miterGap;

        /// <summary>Минимальный радиус изгиба для проверки, м. 0 = проверка выключена.</summary>
        public float minRadius;

        public static FencePathSettings Default => new()
        {
            maxDeviation = 0.01f,
            sharpAngleDeg = 25f,
            smoothAngleDeg = 2f,
            breakAngleDeg = 150f,
            bendProbeMeters = 0.05f,
            maxStep = 2.5f,
            minStep = 0.05f,
            postHalfSize = 0.03f,
            miterGap = 0.002f,
            minRadius = 0f
        };
    }

    /// <summary>Один кадр пути: точка + ортонормированный базис, вертикальный по мировому up.</summary>
    public struct FenceFrame
    {
        public Vector3 position;       // локальные координаты корня забора
        public Vector3 forward;        // касательная, спроецированная на горизонталь
        public Vector3 right;          // = cross(Vector3.up, forward)
        public Vector3 up;             // всегда Vector3.up — забор вертикален
        public float distance;         // погонный метр от начала прогона
        public float splineDistance;   // погонный метр от начала сплайна
    }

    /// <summary>Узел сплайна, для которого посчитан излом. Участвует в разрыве пути только при Sharp/Reversed.</summary>
    public sealed class FenceCorner
    {
        public int knotIndex;
        public float splineDistance;
        public Vector3 position;
        public FenceJointKind kind;

        /// <summary>Угол излома по метрике §5.1 (база bendProbeMeters), градусы.</summary>
        public float bendAngleDeg;

        /// <summary>Точный угол между направлениями прогонов до/после узла, градусы (не зависит от ε).</summary>
        public float cornerAngleDeg;

        /// <summary>Горизонтальная ось углового столба — биссектриса.</summary>
        public Vector3 bisector;

        /// <summary>Подрезка прогона от узла, м. 0 у неразрывающих узлов.</summary>
        public float trim;

        public int runBefore = -1;
        public int runAfter = -1;

        public bool BreaksPath => kind == FenceJointKind.Sharp || kind == FenceJointKind.Reversed;
    }

    /// <summary>Прогон — кусок сплайна между двумя разрывами (или концами). Хранит адаптивную полилинию кадров.</summary>
    public sealed class FenceRun
    {
        public int index;
        public bool closedLoop;

        public float startSplineDistance;
        public float endSplineDistance;
        public float startTrim;
        public float endTrim;

        public readonly List<FenceFrame> frames = new();

        public int FrameCount => frames.Count;

        /// <summary>Длина прогона по кадрам (после подрезки), м.</summary>
        public float Length => frames.Count >= 2 ? frames[frames.Count - 1].distance : 0f;

        public FenceFrame Start => frames.Count > 0 ? frames[0] : default;
        public FenceFrame End => frames.Count > 0 ? frames[frames.Count - 1] : default;

        /// <summary>Кадр на заданном погонном метре прогона (линейная интерполяция между кадрами).</summary>
        public FenceFrame Sample(float distance)
        {
            if (frames.Count == 0) return default;
            if (frames.Count == 1) return frames[0];
            if (distance <= frames[0].distance) return frames[0];
            if (distance >= frames[frames.Count - 1].distance) return frames[frames.Count - 1];

            int lo = 0, hi = frames.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (frames[mid].distance <= distance) lo = mid;
                else hi = mid;
            }

            FenceFrame a = frames[lo];
            FenceFrame b = frames[hi];
            float k = Mathf.InverseLerp(a.distance, b.distance, distance);

            Vector3 fwd = Vector3.Slerp(a.forward, b.forward, k);
            if (fwd.sqrMagnitude < 1e-8f) fwd = a.forward;

            return new FenceFrame
            {
                position = Vector3.Lerp(a.position, b.position, k),
                forward = fwd.normalized,
                right = Vector3.Slerp(a.right, b.right, k).normalized,
                up = Vector3.up,
                distance = distance,
                splineDistance = Mathf.Lerp(a.splineDistance, b.splineDistance, k)
            };
        }
    }

    /// <summary>
    /// Сплайн → кадры/прогоны/углы. Чистая геометрия, без материалов и мешей.
    /// Строится в ЛОКАЛЬНЫХ координатах SplineContainer (узлы сплайна локальны) — это обязательно
    /// для floating origin: мировые координаты в меш попадать не должны.
    /// </summary>
    public sealed class FencePath
    {
        public float splineLength;
        public bool closed;

        public readonly List<FenceRun> runs = new();
        public readonly List<FenceCorner> corners = new();
        public readonly List<string> warnings = new();

        public float TotalRunLength
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < runs.Count; i++) sum += runs[i].Length;
                return sum;
            }
        }

        // ============================================================
        // Public API
        // ============================================================

        public static FencePath Build(SplineContainer container, FencePathSettings settings)
        {
            if (container == null || container.Spline == null)
                return Build((Spline)null, settings);

            return Build(container.Spline, settings);
        }

        public static FencePath Build(Spline spline, FencePathSettings settings)
        {
            var path = new FencePath();

            if (spline == null || spline.Count < 2)
            {
                path.warnings.Add("Сплайн пуст или содержит меньше двух узлов — путь не построен.");
                return path;
            }

            path.closed = spline.Closed;
            path.splineLength = spline.GetLength();

            if (path.splineLength < 0.01f)
            {
                path.warnings.Add($"Длина сплайна {path.splineLength:F3} м — слишком мала.");
                return path;
            }

            // 1. Узлы → расстояния вдоль сплайна + класс излома
            CollectCorners(path, spline, settings);

            // 2. Разрывы → прогоны
            BuildRuns(path, spline, settings);

            // индексы нужны до сэмплирования — на них ссылаются предупреждения
            for (int i = 0; i < path.runs.Count; i++)
                path.runs[i].index = i;

            // 3. Кадры внутри прогонов
            for (int i = 0; i < path.runs.Count; i++)
                SampleRun(path, spline, path.runs[i], settings);

            // прогоны нулевой длины выкидываем, но предупреждение уже записали
            for (int i = path.runs.Count - 1; i >= 0; i--)
            {
                if (path.runs[i].Length < 1e-3f)
                    path.runs.RemoveAt(i);
            }

            for (int i = 0; i < path.runs.Count; i++)
                path.runs[i].index = i;

            return path;
        }

        /// <summary>Подрезка прогона miter'ом: проекция коробки столба на ось прогона (§5.3).</summary>
        public static float MiterTrim(float postSize, float angleBetweenRunsDeg)
        {
            if (postSize <= 0f) return 0f;

            float half = postSize * 0.5f;
            float alpha = Mathf.Clamp(angleBetweenRunsDeg, 0f, 180f) * 0.5f * Mathf.Deg2Rad;
            return half * (Mathf.Abs(Mathf.Cos(alpha)) + Mathf.Abs(Mathf.Sin(alpha)));
        }

        /// <summary>Угол излома в узле по метрике §5.1: хорды ±eps метров. Не зависит от длины сплайна, не даёт NaN.</summary>
        public static float BendAngleDeg(Spline spline, float distanceAtKnot, float eps)
        {
            if (spline == null || spline.Count < 2) return 0f;

            float length = spline.GetLength();
            if (length < 1e-4f) return 0f;

            eps = Mathf.Max(1e-3f, eps);

            float dIn = distanceAtKnot - eps;
            float dOut = distanceAtKnot + eps;

            if (spline.Closed)
            {
                dIn = WrapDistance(dIn, length);
                dOut = WrapDistance(dOut, length);
            }
            else
            {
                dIn = Mathf.Clamp(dIn, 0f, length);
                dOut = Mathf.Clamp(dOut, 0f, length);
            }

            Vector3 a = PositionAt(spline, length, dIn, spline.Closed);
            Vector3 c = PositionAt(spline, length, distanceAtKnot, spline.Closed);
            Vector3 b = PositionAt(spline, length, dOut, spline.Closed);

            Vector3 da = c - a;
            Vector3 db = b - c;
            if (da.sqrMagnitude < 1e-10f || db.sqrMagnitude < 1e-10f) return 0f;

            return Vector3.Angle(da, db);
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[FencePath] длина сплайна {splineLength:F2} м, closed={closed}, " +
                          $"прогонов={runs.Count}, углов={corners.Count}, суммарно {TotalRunLength:F2} м");

            for (int i = 0; i < corners.Count; i++)
            {
                var c = corners[i];
                sb.AppendLine($"  узел {c.knotIndex} @ {c.splineDistance:F2} м: излом {c.bendAngleDeg:F1}°, " +
                              $"поворот {c.cornerAngleDeg:F1}°, {c.kind}, trim {c.trim:F3} м, " +
                              $"runs {c.runBefore}→{c.runAfter}");
            }

            for (int i = 0; i < runs.Count; i++)
            {
                var r = runs[i];
                string steps = r.FrameCount >= 2
                    ? $"{r.frames[0].distance:F2}…{r.frames[r.FrameCount - 1].distance:F2}"
                    : "—";
                sb.AppendLine($"  прогон {i}: {r.Length:F2} м, кадров {r.FrameCount}, " +
                              $"trim {r.startTrim:F3}/{r.endTrim:F3}, closedLoop={r.closedLoop}, шаги {steps}");
            }

            for (int i = 0; i < warnings.Count; i++)
                sb.AppendLine($"  ⚠ {warnings[i]}");

            return sb.ToString();
        }

        // ============================================================
        // Шаг 1 — узлы и изломы
        // ============================================================

        private static void CollectCorners(FencePath path, Spline spline, FencePathSettings s)
        {
            int count = spline.Count;
            float length = path.splineLength;

            for (int i = 0; i < count; i++)
            {
                // Концы открытого сплайна — не углы, а торцы пути.
                bool isPathEnd = !path.closed && (i == 0 || i == count - 1);
                if (isPathEnd) continue;

                float d = spline.ConvertIndexUnit(i, PathIndexUnit.Knot, PathIndexUnit.Distance);
                d = Mathf.Clamp(d, 0f, length);

                float bend = BendAngleDeg(spline, d, s.bendProbeMeters);

                FenceJointKind kind;
                if (bend < s.smoothAngleDeg) kind = FenceJointKind.Straight;
                else if (bend <= s.sharpAngleDeg) kind = FenceJointKind.Smooth;
                else if (bend <= s.breakAngleDeg) kind = FenceJointKind.Sharp;
                else kind = FenceJointKind.Reversed;

                var corner = new FenceCorner
                {
                    knotIndex = i,
                    splineDistance = d,
                    position = PositionAt(spline, length, d, path.closed),
                    kind = kind,
                    bendAngleDeg = bend
                };

                if (corner.BreaksPath)
                {
                    FillCornerDirections(corner, spline, length, path.closed, s);

                    if (kind == FenceJointKind.Reversed)
                        path.warnings.Add($"Узел {i} @ {d:F2} м: излом {bend:F0}° > {s.breakAngleDeg:F0}° (шпилька) — " +
                                          "путь разорван, столб не ставится.");
                }

                path.corners.Add(corner);
            }
        }

        private static void FillCornerDirections(FenceCorner corner, Spline spline, float length, bool closed,
                                                 FencePathSettings s)
        {
            Vector3 a = HorizontalTangent(spline, length, corner.splineDistance - s.bendProbeMeters, closed, corner.position);
            Vector3 b = HorizontalTangent(spline, length, corner.splineDistance + s.bendProbeMeters, closed, corner.position);

            corner.cornerAngleDeg = (a.sqrMagnitude > 1e-8f && b.sqrMagnitude > 1e-8f)
                ? Vector3.Angle(a, b)
                : corner.bendAngleDeg;

            Vector3 bisector = a + b;
            if (bisector.sqrMagnitude < 1e-4f)
            {
                // ровно 180°: биссектрисы нет — ось столба берём перпендикулярно прогону
                bisector = Vector3.Cross(Vector3.up, a);
                if (bisector.sqrMagnitude < 1e-8f) bisector = Vector3.right;
            }

            corner.bisector = bisector.normalized;

            // trim = max по обоим прогонам: проекция коробки столба на ось прогона (§5.3)
            float trimA = TrimFor(corner.bisector, a, s.postHalfSize);
            float trimB = TrimFor(corner.bisector, b, s.postHalfSize);

            // У шпильки столба нет — подрезать нечем и незачем
            corner.trim = corner.kind == FenceJointKind.Reversed
                ? 0f
                : Mathf.Max(trimA, trimB) + s.miterGap;
        }

        private static float TrimFor(Vector3 pillarAxis, Vector3 runForward, float halfSize)
        {
            if (runForward.sqrMagnitude < 1e-8f) return halfSize;
            float alpha = Vector3.Angle(runForward, pillarAxis) * Mathf.Deg2Rad;
            return halfSize * (Mathf.Abs(Mathf.Cos(alpha)) + Mathf.Abs(Mathf.Sin(alpha)));
        }

        // ============================================================
        // Шаг 2 — прогоны
        // ============================================================

        private static void BuildRuns(FencePath path, Spline spline, FencePathSettings s)
        {
            var breaks = new List<FenceCorner>();
            for (int i = 0; i < path.corners.Count; i++)
                if (path.corners[i].BreaksPath) breaks.Add(path.corners[i]);

            breaks.Sort((x, y) => x.splineDistance.CompareTo(y.splineDistance));

            if (!path.closed)
            {
                // Границы: 0, разрывы, L
                var bounds = new List<(float d, FenceCorner corner)>(breaks.Count + 2);
                bounds.Add((0f, null));
                for (int i = 0; i < breaks.Count; i++) bounds.Add((breaks[i].splineDistance, breaks[i]));
                bounds.Add((path.splineLength, null));

                for (int i = 0; i < bounds.Count - 1; i++)
                {
                    FenceCorner left = bounds[i].corner;
                    FenceCorner right = bounds[i + 1].corner;

                    var run = new FenceRun
                    {
                        startSplineDistance = bounds[i].d,
                        endSplineDistance = bounds[i + 1].d,
                        startTrim = left?.trim ?? 0f,
                        endTrim = right?.trim ?? 0f
                    };

                    int runIndex = path.runs.Count;
                    path.runs.Add(run);

                    if (right != null) right.runBefore = runIndex;
                    if (left != null) left.runAfter = runIndex;
                }
            }
            else if (breaks.Count == 0)
            {
                // Замкнутый забор без углов: один замкнутый прогон
                var run = new FenceRun
                {
                    startSplineDistance = 0f,
                    endSplineDistance = path.splineLength,
                    closedLoop = true
                };
                path.runs.Add(run);
            }
            else
            {
                for (int i = 0; i < breaks.Count; i++)
                {
                    FenceCorner left = breaks[i];
                    FenceCorner right = breaks[(i + 1) % breaks.Count];

                    float start = left.splineDistance;
                    float end = right.splineDistance;
                    if (i == breaks.Count - 1) end += path.splineLength;   // замыкание через шов

                    var run = new FenceRun
                    {
                        startSplineDistance = start,
                        endSplineDistance = end,
                        startTrim = left.trim,
                        endTrim = right.trim
                    };

                    int runIndex = path.runs.Count;
                    path.runs.Add(run);

                    left.runAfter = runIndex;
                    right.runBefore = runIndex;
                }
            }

            for (int i = 0; i < path.runs.Count; i++)
            {
                var r = path.runs[i];
                float span = r.endSplineDistance - r.startSplineDistance;

                if (span - r.startTrim - r.endTrim <= 1e-3f)
                {
                    path.warnings.Add($"Прогон {i}: длина {span:F2} м не больше суммы подрезок " +
                                      $"({r.startTrim:F2} + {r.endTrim:F2}) — прогон пропущен.");
                }
            }
        }

        // ============================================================
        // Шаг 3 — кадры по прогону
        // ============================================================

        private static void SampleRun(FencePath path, Spline spline, FenceRun run, FencePathSettings s)
        {
            float length = path.splineLength;
            float fullSpan = run.endSplineDistance - run.startSplineDistance;
            if (fullSpan <= 1e-3f) return;

            // Рабочий участок после miter-подрезок
            run.startSplineDistance += run.startTrim;
            run.endSplineDistance -= run.endTrim;

            float span = run.endSplineDistance - run.startSplineDistance;
            if (span <= 1e-3f) return;

            // Пробы по 5 см: нужны и для радиуса, и для фолбэка направления
            float probeStep = Mathf.Max(0.01f, s.bendProbeMeters);
            int probeCount = Mathf.Clamp(Mathf.CeilToInt(span / probeStep) + 1, 3, 8192);
            float actualProbeStep = span / (probeCount - 1);

            var probePos = new Vector3[probeCount];
            for (int i = 0; i < probeCount; i++)
            {
                float d = run.startSplineDistance + actualProbeStep * i;
                probePos[i] = PositionAt(spline, length, d, path.closed);
            }

            // Локальный поворот на одну пробу, радианы
            var probeTurn = new float[probeCount];
            bool radiusWarned = false;

            for (int i = 1; i < probeCount - 1; i++)
            {
                Vector3 d0 = probePos[i] - probePos[i - 1];
                Vector3 d1 = probePos[i + 1] - probePos[i];
                if (d0.sqrMagnitude < 1e-10f || d1.sqrMagnitude < 1e-10f) continue;

                float turn = Vector3.Angle(d0, d1) * Mathf.Deg2Rad;
                probeTurn[i] = turn;

                if (s.minRadius > 0f && turn > 1e-5f)
                {
                    float radius = actualProbeStep / turn;
                    if (radius < s.minRadius && !radiusWarned)
                    {
                        radiusWarned = true;
                        float atDistance = run.startSplineDistance + actualProbeStep * i;
                        path.warnings.Add($"Прогон {run.index}: радиус {radius:F2} м < {s.minRadius:F2} м " +
                                          $"@ {atDistance:F2} м вдоль сплайна — полотно на внутренней стороне изгиба схлопнется.");
                    }
                }
            }

            // Кадры
            run.frames.Clear();
            run.frames.Add(MakeFrame(spline, path, run, 0f, s, Vector3.zero));

            float travelled = 0f;
            int guard = 0;

            while (travelled < span - 1e-4f)
            {
                float step = StepFromTurn(travelled, actualProbeStep, probeTurn, span, s);
                float next = Mathf.Min(travelled + step, span);

                run.frames.Add(MakeFrame(spline, path, run, next, s, run.frames[run.frames.Count - 1].forward));

                if (next <= travelled) break;      // защита от зацикливания
                travelled = next;

                if (++guard > 100000)
                {
                    path.warnings.Add($"Прогон {run.index}: превышен лимит кадров — тесселяция прервана.");
                    break;
                }
            }

            // Гарантируем кадр ровно в конце прогона
            if (run.frames.Count >= 1 && Mathf.Abs(run.frames[run.frames.Count - 1].distance - span) > 1e-3f)
                run.frames.Add(MakeFrame(spline, path, run, span, s, run.frames[run.frames.Count - 1].forward));
        }

        /// <summary>Шаг для текущего метра: из локального радиуса и допустимой стрелки прогиба (§4.3).</summary>
        private static float StepFromTurn(float localDistance, float probeStep, float[] probeTurn, float span,
                                          FencePathSettings s)
        {
            int idx = Mathf.Clamp(Mathf.RoundToInt(localDistance / probeStep), 1, probeTurn.Length - 2);
            float turn = probeTurn[idx];

            float step = s.maxStep;
            if (turn > 1e-5f)
            {
                float radius = probeStep / turn;
                step = Mathf.Sqrt(8f * Mathf.Max(1e-4f, s.maxDeviation) * radius);
            }

            step = Mathf.Clamp(step, Mathf.Max(1e-3f, s.minStep), Mathf.Max(s.minStep, s.maxStep));
            return Mathf.Min(step, span - localDistance);
        }

        private static FenceFrame MakeFrame(Spline spline, FencePath path, FenceRun run, float localDistance,
                                            FencePathSettings s, Vector3 fallbackForward)
        {
            float splineDistance = run.startSplineDistance + localDistance;
            Vector3 position = PositionAt(spline, path.splineLength, splineDistance, path.closed);

            Vector3 forward = HorizontalTangent(spline, path.splineLength, splineDistance, path.closed, position);

            if (forward.sqrMagnitude < 1e-8f)
            {
                // Касательная вырождена (вертикальный сплайн или нулевая касательная узла):
                // берём хорду вперёд, а если и её нет — направление предыдущего кадра.
                Vector3 ahead = PositionAt(spline, path.splineLength, splineDistance + s.bendProbeMeters, path.closed);
                Vector3 chord = ahead - position;
                chord.y = 0f;
                forward = chord.sqrMagnitude > 1e-8f ? chord : fallbackForward;

                if (forward.sqrMagnitude < 1e-8f)
                {
                    forward = Vector3.forward;
                    if (path.warnings.Count < 64)
                        path.warnings.Add($"Прогон {run.index} @ {splineDistance:F2} м: не удалось определить " +
                                          "направление кадра (сплайн вертикальный?) — взят Vector3.forward.");
                }
            }

            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 1e-8f) right = Vector3.right;
            right.Normalize();

            return new FenceFrame
            {
                position = position,
                forward = forward,
                right = right,
                up = Vector3.up,
                distance = localDistance,
                splineDistance = splineDistance
            };
        }

        // ============================================================
        // Утилиты
        // ============================================================

        /// <summary>Позиция на сплайне по погонному метру. Нормализованный t в пакете — это именно доля ДЛИНЫ.</summary>
        private static Vector3 PositionAt(Spline spline, float length, float distance, bool closed)
        {
            if (length < 1e-6f) return Vector3.zero;

            float d = closed ? WrapDistance(distance, length) : Mathf.Clamp(distance, 0f, length);
            float t = d / length;

            float3 p = SplineUtility.EvaluatePosition(spline, t);
            return new Vector3(p.x, p.y, p.z);
        }

        /// <summary>Касательная, спроецированная на горизонталь. Нулевая — фолбэк вызывающей стороны.</summary>
        private static Vector3 HorizontalTangent(Spline spline, float length, float distance, bool closed,
                                                 Vector3 fallbackPosition)
        {
            if (length < 1e-6f) return Vector3.zero;

            float d = closed ? WrapDistance(distance, length) : Mathf.Clamp(distance, 0f, length);
            float t = d / length;

            float3 tan = SplineUtility.EvaluateTangent(spline, t);
            Vector3 forward = new Vector3(tan.x, 0f, tan.z);

            if (forward.sqrMagnitude < 1e-10f)
            {
                // Нулевая касательная: разность позиций вокруг точки
                float eps = Mathf.Min(0.05f, length * 0.01f);
                float da = closed ? WrapDistance(d - eps, length) : Mathf.Clamp(d - eps, 0f, length);
                float db = closed ? WrapDistance(d + eps, length) : Mathf.Clamp(d + eps, 0f, length);

                float3 pa = SplineUtility.EvaluatePosition(spline, da / length);
                float3 pb = SplineUtility.EvaluatePosition(spline, db / length);

                Vector3 chord = new Vector3(pb.x - pa.x, 0f, pb.z - pa.z);
                return chord.sqrMagnitude > 1e-10f ? chord.normalized : Vector3.zero;
            }

            return forward.normalized;
        }

        private static float WrapDistance(float distance, float length)
        {
            if (length < 1e-6f) return 0f;
            float d = distance % length;
            return d < 0f ? d + length : d;
        }
    }
}
