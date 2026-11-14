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

        /// <summary>Путь в локальных координатах корня забора (обязательно для floating origin).</summary>
        public FencePath BuildPath()
        {
            FencePathSettings settings = type != null ? type.ToPathSettings() : FencePathSettings.Default;
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
    }
}
