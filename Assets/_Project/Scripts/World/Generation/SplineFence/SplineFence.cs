using UnityEngine;
using UnityEngine.Splines;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Описание забора на объекте со SplineContainer: тип + ссылка на сплайн.
    /// Сам ничего не строит — геометрию собирает editor-инструмент (SplineFenceTool),
    /// чтобы игра не тратила кадры на генерацию и меши лежали ассетами.
    ///
    /// Переключение типа в сцене = смена type + «Пересобрать» (§8): спайн при этом не трогается.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [DisallowMultipleComponent]
    public sealed class SplineFence : MonoBehaviour
    {
        [Tooltip("Тип забора. Пилот: FT_Fence_Bar_Steel / FT_Fence_Brick_Block.")]
        public FenceTypeDefinition type;

        [Tooltip("Индекс сплайна внутри SplineContainer.")]
        public int splineIndex;

        [Tooltip("Смещение по Y от сплайна, м. Позволяет утопить или приподнять забор над линией.")]
        public float groundOffset;

        [Tooltip("Слой сгенерированных коллайдеров.")]
        public int colliderLayer;

        [Header("Переопределения (0 — взять из типа)")]
        [Tooltip("Высота забора, м. 0 — из FenceTypeDefinition. Пропорционально тянет полотно, столбы, " +
                 "балки, прутки и высоту коллайдера; профили (толщины, шаг прутков) не меняются.")]
        public float heightOverride;

        [Tooltip("Шаг секции = расстояние между столбами, м. 0 — из типа. На дуге кирпичный забор всё " +
                 "равно добавит столбов по panelChordError — это адаптивность, переопределение её не отменяет.")]
        public float sectionStepOverride;

        [Tooltip("Имя группы сгенерированного. Пусто — берётся имя объекта.")]
        public string fenceId = "";

        public string EffectiveId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(fenceId)) return fenceId.Trim();
                return string.IsNullOrWhiteSpace(name) ? "Fence" : name;
            }
        }

        public SplineContainer Container => GetComponent<SplineContainer>();

        public bool HasOverrides => heightOverride > 0.0005f || sectionStepOverride > 0.0005f;

        /// <summary>Высота, которая реально пойдёт в геометрию, м.</summary>
        public float EffectiveHeight =>
            heightOverride > 0.0005f ? heightOverride : (type != null ? type.height : 0f);

        /// <summary>Шаг секции (расстояние между столбами), который реально пойдёт в геометрию, м.</summary>
        public float EffectiveSectionStep =>
            sectionStepOverride > 0.0005f ? sectionStepOverride : (type != null ? type.postSpacing : 0f);

        /// <summary>
        /// Тип с применёнными переопределениями этого забора.
        /// transient == true → вернулась временная копия, её обязан уничтожить вызывающий
        /// (`Object.DestroyImmediate`); без переопределений возвращается сам ассет.
        /// </summary>
        public FenceTypeDefinition ResolveType(out bool transient)
        {
            transient = false;
            if (type == null) return null;

            FenceTypeDefinition resolved = type.ResolveFor(heightOverride, sectionStepOverride);
            transient = resolved != type;
            return resolved;
        }

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

        /// <summary>
        /// Путь в локальных координатах корня забора (обязательно для floating origin).
        /// effectiveType — результат <see cref="ResolveType"/>: шаг тесселяции берётся из postSpacing УЖЕ
        /// с переопределением, иначе кадры пути не сойдутся с раскладкой столбов.
        /// </summary>
        public FencePath BuildPath(FenceTypeDefinition effectiveType)
        {
            FencePathSettings settings = effectiveType != null ? effectiveType.ToPathSettings() : FencePathSettings.Default;
            FencePath path = FencePath.Build(ActiveSpline, settings);

            if (Mathf.Abs(groundOffset) > 1e-4f)
            {
                for (int r = 0; r < path.runs.Count; r++)
                {
                    var frames = path.runs[r].frames;
                    for (int i = 0; i < frames.Count; i++)
                    {
                        FenceFrame f = frames[i];
                        f.position += Vector3.up * groundOffset;
                        frames[i] = f;
                    }
                }
            }

            return path;
        }

        /// <summary>
        /// Переопределения держим в рабочих рамках: иначе высота 0.05 или шаг 200 м дают молчаливый мусор
        /// (вывернутые балки, бесконечный цикл раскладки). 0 = «брать из типа» остаётся нулём.
        /// </summary>
        private void OnValidate()
        {
            if (heightOverride > 0f)
                heightOverride = Mathf.Clamp(heightOverride, FenceTypeDefinition.MinHeight, FenceTypeDefinition.MaxHeight);

            if (sectionStepOverride > 0f)
                sectionStepOverride = Mathf.Clamp(sectionStepOverride,
                    FenceTypeDefinition.MinSectionStep, FenceTypeDefinition.MaxSectionStep);

            colliderLayer = Mathf.Clamp(colliderLayer, 0, 31);
        }
    }
}
