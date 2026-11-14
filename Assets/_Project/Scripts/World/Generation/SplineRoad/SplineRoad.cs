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

        [Header("Сгенерированное")]
        [Tooltip("Имя группы сгенерированного и папки мешей. Пусто — берётся имя объекта.")]
        public string roadId = "";

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

            if (snapToSurface)
            {
                snapMisses = SnapToSurface(path, settings);
                path.RecomputeFrames(settings, false);
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
        private int SnapToSurface(RoadPath path, RoadPathSettings settings)
        {
            int misses = 0;
            float distance = Mathf.Max(0f, surfaceProbeUp) + Mathf.Max(0f, surfaceProbeDown);

            for (int i = 0; i < path.frames.Count; i++)
            {
                RoadFrame f = path.frames[i];

                Vector3 world = transform.TransformPoint(f.position);
                Vector3 origin = world + Vector3.up * surfaceProbeUp;

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Max(0.01f, distance),
                                    surfaceMask, QueryTriggerInteraction.Ignore))
                {
                    Vector3 target = hit.point + Vector3.up * surfaceOffset;
                    f.position = transform.InverseTransformPoint(target);
                }
                else
                {
                    misses++;
                }

                path.frames[i] = f;
            }

            return misses;
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
        }
    }
}
