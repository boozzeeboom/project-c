using UnityEngine;
using UnityEngine.Splines;

namespace ProjectC.World.Generation
{
    /// <summary>Что рисовать: только покрытие или покрытие с бордюрами.</summary>
    public enum RoadSurface
    {
        /// <summary>Одна лента-плейн. Самый дешёвый вариант для пилотирования.</summary>
        Plane = 0,

        /// <summary>Лента-плейн + приподнятые бордюры по обоим краям (второй сабмеш).</summary>
        PlaneWithCurbs = 1
    }

    /// <summary>
    /// Дорожка по сплайну для полётов: параметры лежат прямо на компоненте (ширина, материалы,
    /// коллайдеры, привязка к поверхности) — правятся в инспекторе без ассетов.
    ///
    /// Сам ничего не строит: геометрию собирает editor-инструмент (SplineRoadTool), меши лежат
    /// ассетами в Assets/_Project/Generated/Roads/&lt;id&gt;/, игра не тратит кадры на генерацию.
    ///
    /// Геометрия строится в ЛОКАЛЬНЫХ координатах корня (floating origin). Масштаб корня обязан быть 1.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [DisallowMultipleComponent]
    public sealed class SplineRoad : MonoBehaviour
    {
        [Header("Тип дорожки")]
        [Tooltip("Plane — только покрытие. PlaneWithCurbs — покрытие + бордюры по обоим краям.")]
        public RoadSurface surface = RoadSurface.PlaneWithCurbs;

        [Tooltip("Индекс сплайна внутри SplineContainer.")]
        public int splineIndex;

        [Tooltip("Подъём покрытия над линией сплайна (или над найденной поверхностью), м.")]
        public float groundOffset;

        [Header("Материалы")]
        [Tooltip("Покрытие. Пилот: M_PC_Cobble / M_PC_Concrete — оба процедурные, работают в object space.")]
        public Material roadMaterial;

        [Tooltip("Бордюры. Пусто — бордюры пойдут тем же материалом, что покрытие.")]
        public Material curbMaterial;

        [Header("Геометрия")]
        [Tooltip("Ширина покрытия, м.")]
        public float width = 4f;

        [Tooltip("Максимальный шаг кадров вдоль дорожки, м. Меньше — ровнее дуги и больше треугольников.")]
        public float maxStep = 2f;

        [Tooltip("Допустимая стрелка прогиба хорды на изгибе, м.")]
        public float maxDeviation = 0.01f;

        [Tooltip("Предел miter-вытягивания на углу, в полуширинах. 3 ≈ углы до 140°. Больше — фаска.")]
        public float miterLimit = 3f;

        [Header("Бордюры")]
        [Tooltip("Ширина бордюра, м. Лежит внутри покрытия, поэтому не расширяет дорожку.")]
        public float curbWidth = 0.25f;

        [Tooltip("Высота бордюра над покрытием, м.")]
        public float curbHeight = 0.15f;

        [Header("Коллайдеры")]
        [Tooltip("Создавать коллайдеры по дорожке. Выключено — покрытие чисто визуальное.")]
        public bool generateColliders = true;

        [Tooltip("Слой сгенерированных коллайдеров.")]
        public int colliderLayer;

        [Tooltip("Толщина плиты-коллайдера, м. Верх плиты стоит ровно на поверхности покрытия.")]
        public float colliderThickness = 0.4f;

        [Tooltip("Шаг коллайдер-плит вдоль дорожки, м.")]
        public float colliderStep = 2f;

        [Header("Привязка к поверхности")]
        [Tooltip("Луч вниз из каждого кадра: Y покрытия берётся с ближайшей поверхности, а не со сплайна. " +
                 "Именно это не даёт дорожке «летать» над рельефом.")]
        public bool snapToSurface = true;

        [Tooltip("Маска поверхностей для привязки. Слой коллайдеров дорожки сюда попадать не должен.")]
        public LayerMask surfaceMask = ~0;

        [Tooltip("Высота старта луча над кадром, м.")]
        public float surfaceProbeUp = 20f;

        [Tooltip("Глубина луча ниже кадра, м.")]
        public float surfaceProbeDown = 60f;

        [Tooltip("Зазор между найденной поверхностью и покрытием, м.")]
        public float surfaceOffset = 0.02f;

        [Tooltip("Сколько точек поперёк ширины зондировать (1…9). Берётся САМАЯ ВЫСОКАЯ из них: иначе на " +
                 "поперечном уклоне половина покрытия уходит под землю. 1 — только осевая линия.")]
        public int surfaceSamples = 3;

        [Tooltip("Допустимое расхождение покрытия с рельефом между кадрами, м. Больше — в пролёт вставляется " +
                 "дополнительный кадр, и покрытие ложится по рельефу, а не режет его хордой.")]
        public float surfaceTolerance = 0.01f;

        [Tooltip("Предел числа кадров, добавленных под рельеф. Защита от патологического рельефа.")]
        public int surfaceMaxSplits = 400;

        [Header("Сгенерированное")]
        [Tooltip("Имя группы сгенерированного и папки мешей. Пусто — берётся имя объекта.")]
        public string roadId = "";

        /// <summary>Сколько кадров последняя сборка добавила под рельеф (для отчёта, не сериализуется).</summary>
        [System.NonSerialized] public int lastSurfaceSplits;

        public string EffectiveId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(roadId)) return roadId.Trim();
                return string.IsNullOrWhiteSpace(name) ? "Road" : name;
            }
        }

        public SplineContainer Container => GetComponent<SplineContainer>();

        public Spline ActiveSpline
        {
            get
            {
                SplineContainer container = Container;
                if (container == null) return null;

                if (splineIndex >= 0 && splineIndex < container.Splines.Count)
                    return container.Splines[splineIndex];

                return container.Spline;
            }
        }

        /// <summary>Локальный вектор мирового «вверх» — чтобы покрытие оставалось горизонтальным при повёрнутом корне.</summary>
        public Vector3 LocalUp
        {
            get
            {
                Vector3 up = transform.InverseTransformDirection(Vector3.up);
                return up.sqrMagnitude < 1e-8f ? Vector3.up : up.normalized;
            }
        }

        public RoadPathSettings ToPathSettings()
        {
            RoadPathSettings s = RoadPathSettings.Default;

            s.maxDeviation = Mathf.Max(0.001f, maxDeviation);
            s.maxStep = Mathf.Max(0.1f, maxStep);
            s.minStep = Mathf.Min(0.05f, s.maxStep * 0.5f);
            s.halfWidth = Mathf.Max(0.05f, width * 0.5f);
            s.miterLimit = Mathf.Clamp(miterLimit, 1f, 10f);
            s.up = LocalUp;

            return s;
        }

        public RoadGeometrySettings ToGeometrySettings()
        {
            RoadGeometrySettings g = RoadGeometrySettings.Default;

            g.surface = surface;
            g.width = width;
            g.curbWidth = curbWidth;
            g.curbHeight = curbHeight;
            g.roadMaterial = roadMaterial;
            g.curbMaterial = curbMaterial;
            g.generateColliders = generateColliders;
            g.colliderThickness = colliderThickness;
            g.colliderStep = colliderStep;

            return g;
        }

        /// <summary>
        /// Путь в локальных координатах корня. snapMisses — сколько кадров не нашли поверхность
        /// (они остались на линии сплайна).
        /// </summary>
        public RoadPath BuildPath(out int snapMisses)
        {
            snapMisses = 0;

            RoadPathSettings settings = ToPathSettings();
            RoadPath path = RoadPath.Build(ActiveSpline, settings);

            if (!path.IsValid) return path;

            lastSurfaceSplits = 0;

            if (snapToSurface)
            {
                snapMisses = SnapToSurface(path);
                path.RecomputeFrames(settings, false);

                // Кадры стоят редко (maxStep), а рельеф между ними гуляет — хорда режет бугры и висит над
                // ямами. Добиваем пролёты, где расхождение больше surfaceTolerance.
                lastSurfaceSplits = RefineToSurface(path, settings, out int refineMisses);
                snapMisses += refineMisses;

                if (lastSurfaceSplits > 0) path.RecomputeFrames(settings, false);
            }

            if (Mathf.Abs(groundOffset) > 1e-4f)
            {
                for (int i = 0; i < path.frames.Count; i++)
                {
                    RoadFrame f = path.frames[i];
                    f.position += settings.up * groundOffset;
                    path.frames[i] = f;
                }

                path.RecomputeFrames(settings, true);
            }

            return path;
        }

        /// <summary>Луч вниз из каждого кадра → Y берётся с ближайшей поверхности.</summary>
        private int SnapToSurface(RoadPath path)
        {
            int misses = 0;

            for (int i = 0; i < path.frames.Count; i++)
            {
                RoadFrame f = path.frames[i];
                Vector3 world = transform.TransformPoint(f.position);

                if (TryProbeSurface(f.position, f.right, out float surfaceY))
                    f.position = transform.InverseTransformPoint(new Vector3(world.x, surfaceY, world.z));
                else
                    misses++;

                path.frames[i] = f;
            }

            return misses;
        }

        /// <summary>
        /// Самая ВЫСОКАЯ точка поверхности поперёк ширины кадра. Луч всегда вертикальный, XZ кадра не меняется —
        /// план дорожки остаётся ровно по сплайну, а из поверхности берётся только Y.
        ///
        /// Почему поперёк: осевая линия даёт высоту только середины, и на любом поперечном уклоне половина
        /// покрытия уезжает под землю. Берём верхнюю точку профиля — покрытие ложится на поверхность.
        /// </summary>
        private bool TryProbeSurface(Vector3 localPosition, Vector3 localRight, out float surfaceY)
        {
            surfaceY = 0f;

            Vector3 world = transform.TransformPoint(localPosition);
            Vector3 axis = localRight.sqrMagnitude > 1e-8f
                ? transform.TransformDirection(localRight.normalized)
                : transform.TransformDirection(Vector3.right);

            int samples = Mathf.Clamp(surfaceSamples, 1, 9);
            float halfWidth = Mathf.Max(0.05f, width * 0.5f);
            float rayLength = Mathf.Max(0.01f, surfaceProbeUp + surfaceProbeDown);
            Vector3 origin = world + Vector3.up * Mathf.Max(0f, surfaceProbeUp);

            bool hitAny = false;
            float best = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = samples == 1 ? 0f : -1f + 2f * i / (samples - 1f);
                Vector3 probe = origin + axis * (t * halfWidth);

                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, rayLength,
                                     surfaceMask, QueryTriggerInteraction.Ignore))
                    continue;

                if (!hitAny || hit.point.y > best)
                {
                    best = hit.point.y;
                    hitAny = true;
                }
            }

            if (!hitAny) return false;

            surfaceY = best + surfaceOffset;
            return true;
        }

        /// <summary>
        /// Доводит покрытие до рельефа МЕЖДУ кадрами. Если середина пролёта уходит от поверхности глубже
        /// surfaceTolerance (вдавилась в бугор или висит над ямой) — в пролёт вставляется кадр на поверхности.
        /// Это и есть «примыкание»: дорожка ложится по рельефу, а не режет его прямыми.
        /// </summary>
        private int RefineToSurface(RoadPath path, RoadPathSettings settings, out int misses)
        {
            misses = 0;

            int budget = Mathf.Clamp(surfaceMaxSplits, 0, 5000);
            if (budget <= 0 || path.frames.Count < 2) return 0;

            float tolerance = Mathf.Max(0.001f, surfaceTolerance);
            float sharpAngle = Mathf.Clamp(settings.sharpAngleDeg, 1f, 179f);
            int inserted = 0;

            for (int pass = 0; pass < 6 && budget > 0; pass++)
            {
                bool added = false;
                int n = path.frames.Count;

                for (int i = n - 1; i >= 0; i--)
                {
                    if (budget <= 0) break;
                    if (!path.closed && i >= n - 1) continue;

                    int j = (i + 1) % n;
                    RoadFrame a = path.frames[i];
                    RoadFrame b = path.frames[j];

                    // у самого угла кадры подрезаны под miter — лишний кадр рядом сломает выпуклость квада
                    if (a.turnDeg > sharpAngle || b.turnDeg > sharpAngle) continue;

                    float midDistance = j == 0
                        ? a.distance + (path.splineLength - a.distance) * 0.5f
                        : (a.distance + b.distance) * 0.5f;

                    if (midDistance <= a.distance + 1e-4f) continue;

                    RoadFrame mid = path.Sample(midDistance);
                    Vector3 worldMid = transform.TransformPoint(mid.position);

                    if (!TryProbeSurface(mid.position, mid.right, out float surfaceY))
                    {
                        misses++;
                        continue;
                    }

                    Vector3 chord = (a.position + b.position) * 0.5f;
                    float chordY = transform.TransformPoint(chord).y;

                    if (Mathf.Abs(surfaceY - chordY) <= tolerance) continue;

                    mid.position = transform.InverseTransformPoint(new Vector3(worldMid.x, surfaceY, worldMid.z));
                    mid.distance = midDistance;

                    path.frames.Insert(j == 0 ? n : j, mid);

                    inserted++;
                    budget--;
                    added = true;
                }

                if (!added) break;
            }

            return inserted;
        }

        private void OnValidate()
        {
            width = Mathf.Clamp(width, 0.2f, 60f);
            maxStep = Mathf.Clamp(maxStep, 0.1f, 20f);
            maxDeviation = Mathf.Clamp(maxDeviation, 0.001f, 1f);
            miterLimit = Mathf.Clamp(miterLimit, 1f, 10f);

            curbWidth = Mathf.Clamp(curbWidth, 0f, Mathf.Max(0f, width * 0.5f));
            curbHeight = Mathf.Clamp(curbHeight, 0f, 2f);

            colliderThickness = Mathf.Clamp(colliderThickness, 0.02f, 5f);
            colliderStep = Mathf.Clamp(colliderStep, 0.5f, 20f);
            colliderLayer = Mathf.Clamp(colliderLayer, 0, 31);

            surfaceProbeUp = Mathf.Clamp(surfaceProbeUp, 0f, 500f);
            surfaceProbeDown = Mathf.Clamp(surfaceProbeDown, 0f, 1000f);
            surfaceOffset = Mathf.Clamp(surfaceOffset, -5f, 5f);
            surfaceSamples = Mathf.Clamp(surfaceSamples, 1, 9);
            surfaceTolerance = Mathf.Clamp(surfaceTolerance, 0.001f, 1f);
            surfaceMaxSplits = Mathf.Clamp(surfaceMaxSplits, 0, 5000);
        }
    }
}
