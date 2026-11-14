using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Настройки извлечения дорожки из сплайна.
    ///
    /// Отличие от FencePath: дорожка НЕ рвётся на углах. Забор — это отдельные секции между столбами,
    /// а покрытие дороги непрерывно, поэтому 90° здесь решается miter-стыком (как в дорожных
    /// генераторах), а не разрывом прогона.
    /// </summary>
    public struct RoadPathSettings
    {
        /// <summary>Допустимая стрелка прогиба хорды на изгибе, м. Управляет тесселяцией.</summary>
        public float maxDeviation;

        /// <summary>Максимальный шаг кадров вдоль дорожки, м.</summary>
        public float maxStep;

        /// <summary>Минимальный шаг кадров, м — защита от бесконечного дробления.</summary>
        public float minStep;

        /// <summary>Половина ширины покрытия, м. Задаёт вынос краёв.</summary>
        public float halfWidth;

        /// <summary>Во сколько полуширин максимум может вытянуться miter-угол. Больше — фаска + warning.</summary>
        public float miterLimit;

        /// <summary>Излом между соседними сегментами выше этого угла считается «угловым».</summary>
        public float sharpAngleDeg;

        /// <summary>Предел числа кадров (защита от патологического сплайна).</summary>
        public int maxFrames;

        /// <summary>Локальный «верх» корня (для floating origin и повёрнутых контейнеров).</summary>
        public Vector3 up;

        public static RoadPathSettings Default => new()
        {
            maxDeviation = 0.01f,
            maxStep = 2f,
            minStep = 0.05f,
            halfWidth = 2f,
            miterLimit = 3f,
            sharpAngleDeg = 20f,
            maxFrames = 20000,
            up = Vector3.up
        };
    }

    /// <summary>Один кадр дорожки: точка осевой линии, направления и готовые точки краёв покрытия.</summary>
    public struct RoadFrame
    {
        /// <summary>Точка осевой линии в ЛОКАЛЬНЫХ координатах корня.</summary>
        public Vector3 position;

        /// <summary>Направление движения по горизонтали (в плоскости up).</summary>
        public Vector3 forward;

        /// <summary>= cross(up, forward), единичный.</summary>
        public Vector3 right;

        /// <summary>Край покрытия слева от направления движения.</summary>
        public Vector3 leftEdge;

        /// <summary>Край покрытия справа от направления движения.</summary>
        public Vector3 rightEdge;

        /// <summary>1/cos(половины угла излома): 1 на прямой, 1.41 на 90°, упирается в miterLimit.</summary>
        public float miterScale;

        /// <summary>Угол излома в этом кадре между входящим и исходящим сегментом, градусы.</summary>
        public float turnDeg;

        /// <summary>Погонный метр от начала сплайна.</summary>
        public float distance;
    }

    /// <summary>
    /// Сплайн → непрерывная лента кадров с miter-стыками на углах. Чистая геометрия, без материалов,
    /// мешей и сцены. Всё в ЛОКАЛЬНЫХ координатах SplineContainer — мировые координаты в меш попадать
    /// не должны (floating origin).
    /// </summary>
    public sealed class RoadPath
    {
        public float splineLength;
        public bool closed;

        /// <summary>Локальный «верх», в плоскости которого считаются выносы краёв.</summary>
        public Vector3 up = Vector3.up;

        public readonly List<RoadFrame> frames = new();
        public readonly List<string> warnings = new();

        public bool IsValid => frames.Count >= 2 && SegmentCount >= 1;

        /// <summary>Число сегментов ленты: замкнутый сплайн замыкается последним кадром на первый.</summary>
        public int SegmentCount => frames.Count < 2 ? 0 : (closed ? frames.Count : frames.Count - 1);

        public float Length => frames.Count > 0 ? frames[frames.Count - 1].distance : 0f;

        // ============================================================
        // Public API
        // ============================================================

        public static RoadPath Build(SplineContainer container, RoadPathSettings settings)
        {
            if (container == null || container.Spline == null)
                return Build((Spline)null, settings);

            return Build(container.Spline, settings);
        }

        public static RoadPath Build(Spline spline, RoadPathSettings settings)
        {
            var path = new RoadPath
            {
                up = settings.up.sqrMagnitude > 1e-6f ? settings.up.normalized : Vector3.up
            };

            if (spline == null || spline.Count < 2)
            {
                path.warnings.Add("Сплайн пуст или содержит меньше двух узлов — путь не построен.");
                return path;
            }

            path.closed = spline.Closed;
            path.splineLength = spline.GetLength();

            if (path.splineLength < 0.05f)
            {
                path.warnings.Add($"Длина сплайна {path.splineLength:F3} м — слишком мала.");
                return path;
            }

            List<float> distances = BuildDistances(spline, path, settings);

            for (int i = 0; i < distances.Count; i++)
            {
                path.frames.Add(new RoadFrame
                {
                    position = PositionAt(spline, path.splineLength, distances[i], path.closed),
                    distance = distances[i]
                });
            }

            // первый проход нужен только чтобы узнать углы излома — предупреждения соберёт второй
            path.RecomputeFrames(settings, false);
            path.PruneSharpCorners(settings);
            path.RecomputeFrames(settings, true);

            return path;
        }

        /// <summary>
        /// Пересчитывает направления, miter и точки краёв по УЖЕ стоящим позициям кадров.
        /// Вызывается после привязки к поверхности: Y кадров поменялся — края обязаны поехать за ним.
        /// </summary>
        public void RecomputeFrames(RoadPathSettings settings, bool addWarnings = true)
        {
            int n = frames.Count;
            if (n < 2) return;

            Vector3 localUp = settings.up.sqrMagnitude > 1e-6f ? settings.up.normalized : Vector3.up;
            up = localUp;

            float halfWidth = Mathf.Max(0.001f, settings.halfWidth);
            float limit = Mathf.Max(1f, settings.miterLimit);

            bool clampWarned = false;
            bool radiusWarned = false;

            for (int i = 0; i < n; i++)
            {
                RoadFrame f = frames[i];
                Vector3 p = f.position;

                bool hasPrev = closed || i > 0;
                bool hasNext = closed || i < n - 1;

                Vector3 prevPosition = hasPrev ? frames[(i - 1 + n) % n].position : p;
                Vector3 nextPosition = hasNext ? frames[(i + 1) % n].position : p;

                Vector3 dPrev = hasPrev ? Flatten(p - prevPosition, localUp) : Vector3.zero;
                Vector3 dNext = hasNext ? Flatten(nextPosition - p, localUp) : Vector3.zero;

                if (dPrev.sqrMagnitude < 1e-10f) dPrev = dNext;
                if (dNext.sqrMagnitude < 1e-10f) dNext = dPrev;

                if (dPrev.sqrMagnitude < 1e-10f)
                {
                    // кадры совпали (сплайн вырожден или вертикален) — берём хоть какое-то направление
                    dPrev = Flatten(Vector3.forward, localUp);
                    if (dPrev.sqrMagnitude < 1e-8f) dPrev = Flatten(Vector3.right, localUp);
                    if (dPrev.sqrMagnitude < 1e-8f) dPrev = Vector3.right;
                    dNext = dPrev;

                    if (addWarnings && !clampWarned)
                    {
                        clampWarned = true;
                        warnings.Add($"Кадр @ {f.distance:F2} м: направление определить не удалось " +
                                     "(сплайн вертикальный или кадры совпали).");
                    }
                }

                Vector3 fPrev = dPrev.normalized;
                Vector3 fNext = dNext.normalized;

                Vector3 rightPrev = Vector3.Cross(localUp, fPrev);
                Vector3 rightNext = Vector3.Cross(localUp, fNext);
                rightPrev.Normalize();
                rightNext.Normalize();

                float turn = hasPrev && hasNext ? Vector3.Angle(fPrev, fNext) : 0f;

                Vector3 miter;
                float scale;

                if (!hasPrev)
                {
                    miter = rightNext;
                    scale = 1f;
                }
                else if (!hasNext)
                {
                    miter = rightPrev;
                    scale = 1f;
                }
                else
                {
                    Vector3 sum = rightPrev + rightNext;

                    if (sum.sqrMagnitude < 1e-6f)
                    {
                        // ровно 180°: биссектрисы нет, край уходит по одному из направлений
                        miter = rightNext;
                        scale = limit;
                    }
                    else
                    {
                        miter = sum.normalized;
                        float cos = Vector3.Dot(miter, rightNext);
                        scale = cos > 1e-3f ? 1f / cos : limit;
                    }

                    if (scale > limit)
                    {
                        scale = limit;

                        if (addWarnings && !clampWarned)
                        {
                            clampWarned = true;
                            warnings.Add($"Излом {turn:F0}° @ {f.distance:F2} м вытянул бы край сильнее " +
                                         $"miterLimit {limit:F1} — угол подрезан фаской.");
                        }
                    }
                }

                f.forward = hasNext ? fNext : fPrev;
                f.right = hasNext ? rightNext : rightPrev;
                f.miterScale = scale;
                f.turnDeg = turn;

                Vector3 offset = miter * (halfWidth * scale);
                f.leftEdge = p - offset;
                f.rightEdge = p + offset;

                frames[i] = f;

                // Радиус меньше полуширины: вынос внутреннего края больше радиуса — край выворачивается.
                if (addWarnings && !radiusWarned && turn > 0.01f)
                {
                    float step = hasPrev
                        ? Mathf.Abs(f.distance - frames[(i - 1 + n) % n].distance)
                        : Mathf.Abs(frames[(i + 1) % n].distance - f.distance);

                    if (step > 1e-4f)
                    {
                        float radius = step / (turn * Mathf.Deg2Rad);

                        if (radius < halfWidth)
                        {
                            radiusWarned = true;
                            warnings.Add($"Радиус {radius:F2} м < полуширины {halfWidth:F2} м @ {f.distance:F2} м — " +
                                         "внутренний край покрытия загибается. Уменьшите ширину или увеличьте радиус.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Кадр на заданном погонном метре (линейная интерполяция между кадрами).
        /// За пределами открытого пути — кламп по торцам, у замкнутого — переход через шов.
        /// </summary>
        public RoadFrame Sample(float distance)
        {
            int n = frames.Count;
            if (n == 0) return default;
            if (n == 1) return frames[0];

            float d = closed ? WrapDistance(distance, splineLength) : Mathf.Clamp(distance, 0f, frames[n - 1].distance);

            // шов замкнутого сплайна: от последнего кадра до первого через длину
            if (closed && (d >= frames[n - 1].distance || frames[n - 1].distance <= 0f))
            {
                float a = frames[n - 1].distance;
                float b = Mathf.Max(a + 1e-4f, splineLength);
                return Interpolate(frames[n - 1], frames[0], Mathf.InverseLerp(a, b, d), d);
            }

            if (d <= frames[0].distance) return Interpolate(frames[0], frames[0], 0f, d);

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (frames[mid].distance <= d) lo = mid;
                else hi = mid;
            }

            RoadFrame fa = frames[lo];
            RoadFrame fb = frames[hi];
            float k = Mathf.InverseLerp(fa.distance, fb.distance, d);

            return Interpolate(fa, fb, k, d);
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[RoadPath] длина сплайна {splineLength:F2} м, closed={closed}, " +
                          $"кадров {frames.Count}, сегментов {SegmentCount}");

            float minStep = float.MaxValue, maxStepSeen = 0f;
            int sharp = 0;

            for (int i = 1; i < frames.Count; i++)
            {
                float step = frames[i].distance - frames[i - 1].distance;
                minStep = Mathf.Min(minStep, step);
                maxStepSeen = Mathf.Max(maxStepSeen, step);
            }

            for (int i = 0; i < frames.Count; i++)
                if (frames[i].turnDeg > 20f) sharp++;

            sb.AppendLine($"  шаг {minStep:F3}…{maxStepSeen:F3} м, изломов > 20°: {sharp}");

            for (int i = 0; i < warnings.Count; i++)
                sb.AppendLine($"  ⚠ {warnings[i]}");

            return sb.ToString();
        }

        // ============================================================
        // Внутренние шаги
        // ============================================================

        private static RoadFrame Interpolate(RoadFrame a, RoadFrame b, float k, float distance)
        {
            return new RoadFrame
            {
                position = Vector3.Lerp(a.position, b.position, k),
                forward = SlerpDirection(a.forward, b.forward, k),
                right = SlerpDirection(a.right, b.right, k),
                leftEdge = Vector3.Lerp(a.leftEdge, b.leftEdge, k),
                rightEdge = Vector3.Lerp(a.rightEdge, b.rightEdge, k),
                miterScale = Mathf.Lerp(a.miterScale, b.miterScale, k),
                turnDeg = Mathf.Lerp(a.turnDeg, b.turnDeg, k),
                distance = distance
            };
        }

        private static Vector3 SlerpDirection(Vector3 a, Vector3 b, float k)
        {
            if (a.sqrMagnitude < 1e-10f) return b;
            if (b.sqrMagnitude < 1e-10f) return a;

            Vector3 v = Vector3.Slerp(a, b, k);
            return v.sqrMagnitude < 1e-8f ? a : v.normalized;
        }

        /// <summary>
        /// Равномерная сетка по maxStep + дробление, пока стрелка прогиба хорды больше maxDeviation.
        /// </summary>
        private static List<float> BuildDistances(Spline spline, RoadPath path, RoadPathSettings s)
        {
            float length = path.splineLength;
            int limit = Mathf.Max(2, s.maxFrames);

            int span = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(0.05f, s.maxStep)), 1, Mathf.Max(1, limit / 2));

            var list = new List<float>(span + 2);

            if (path.closed)
            {
                // у замкнутого шов — обычный сегмент, дублирующий кадр в конце не нужен
                for (int i = 0; i < span; i++) list.Add(length * i / span);
            }
            else
            {
                for (int i = 0; i <= span; i++) list.Add(length * i / span);
            }

            float deviation = Mathf.Max(0.001f, s.maxDeviation);
            float minStep = Mathf.Max(0.01f, s.minStep);

            for (int pass = 0; pass < 16; pass++)
            {
                bool inserted = false;

                for (int i = list.Count - 2; i >= 0; i--)
                {
                    if (list.Count >= limit)
                    {
                        path.warnings.Add($"Лимит кадров {limit} — тесселяция урезана, увеличьте maxStep.");
                        return list;
                    }

                    if (!NeedsSplit(spline, length, path.closed, list[i], list[i + 1], deviation, minStep)) continue;

                    list.Insert(i + 1, (list[i] + list[i + 1]) * 0.5f);
                    inserted = true;
                }

                if (path.closed && list.Count >= 2 && list.Count < limit)
                {
                    float a = list[list.Count - 1];
                    float b = length + list[0];

                    if (NeedsSplit(spline, length, true, a, b, deviation, minStep))
                    {
                        list.Add((a + length) * 0.5f);
                        inserted = true;
                    }
                }

                if (!inserted) break;
            }

            return list;
        }

        private static bool NeedsSplit(Spline spline, float length, bool closed, float da, float db,
                                       float maxDeviation, float minStep)
        {
            if (db - da < minStep * 2f) return false;

            Vector3 pa = PositionAt(spline, length, da, closed);
            Vector3 pb = PositionAt(spline, length, db, closed);
            Vector3 pm = PositionAt(spline, length, (da + db) * 0.5f, closed);

            Vector3 chord = pb - pa;
            float chordLength = chord.magnitude;

            // хорда выродилась (петля вернулась в ту же точку) — делить обязательно
            if (chordLength < 1e-5f) return true;

            Vector3 rel = pm - pa;
            Vector3 perpendicular = rel - chord * (Vector3.Dot(rel, chord) / (chordLength * chordLength));
            return perpendicular.magnitude > maxDeviation;
        }

        /// <summary>
        /// Убирает кадры, слишком близкие к угловому: miter-выступ шире оставшегося отрезка — и квад
        /// на углу выворачивается «бабочкой». Условие h ≥ halfWidth·k гарантирует выпуклость.
        /// </summary>
        private void PruneSharpCorners(RoadPathSettings s)
        {
            if (frames.Count < 3) return;

            float angle = Mathf.Clamp(s.sharpAngleDeg, 1f, 179f);

            if (closed)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i].turnDeg <= angle) continue;

                    warnings.Add("Замкнутый сплайн с изломом: miter на шве не подрезается — проверьте форму " +
                                 "кривой, у замкнутого покрытия углы должны быть скруглены.");
                    return;
                }

                return;
            }

            var sharp = new List<int>();

            for (int i = 1; i < frames.Count - 1; i++)
                if (frames[i].turnDeg > angle) sharp.Add(i);

            if (sharp.Count == 0) return;

            var keep = new bool[frames.Count];
            for (int i = 0; i < keep.Length; i++) keep[i] = true;

            for (int k = 0; k < sharp.Count; k++)
            {
                int i = sharp[k];
                float minNeighbor = Mathf.Max(s.minStep, s.halfWidth * frames[i].miterScale);

                for (int j = i - 1; j >= 0 && frames[i].distance - frames[j].distance < minNeighbor; j--)
                {
                    if (sharp.Contains(j)) break;
                    keep[j] = false;
                }

                for (int j = i + 1; j < frames.Count && frames[j].distance - frames[i].distance < minNeighbor; j++)
                {
                    if (sharp.Contains(j)) break;
                    keep[j] = false;
                }
            }

            int surviving = 0;
            for (int i = 0; i < keep.Length; i++)
                if (keep[i]) surviving++;

            if (surviving < 2)
            {
                warnings.Add("Углы стоят слишком тесно: подрезать кадры под miter не из чего — " +
                             "покрытие на углах может вывернуться.");
                return;
            }

            var pruned = new List<RoadFrame>(surviving);
            for (int i = 0; i < frames.Count; i++)
                if (keep[i]) pruned.Add(frames[i]);

            frames.Clear();
            frames.AddRange(pruned);
        }

        // ============================================================
        // Утилиты
        // ============================================================

        private static Vector3 Flatten(Vector3 v, Vector3 up)
        {
            return v - up * Vector3.Dot(v, up);
        }

        private static Vector3 PositionAt(Spline spline, float length, float distance, bool closed)
        {
            if (length < 1e-6f) return Vector3.zero;

            float d = closed ? WrapDistance(distance, length) : Mathf.Clamp(distance, 0f, length);
            float3 p = SplineUtility.EvaluatePosition(spline, d / length);
            return new Vector3(p.x, p.y, p.z);
        }

        private static float WrapDistance(float distance, float length)
        {
            if (length < 1e-6f) return 0f;
            float d = distance % length;
            return d < 0f ? d + length : d;
        }
    }
}
