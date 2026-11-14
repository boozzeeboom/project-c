using UnityEngine;

namespace ProjectC.World.Generation
{
    /// <summary>Тип забора. Пилот — два типа (дизайн-нота §2).</summary>
    public enum FenceStyle
    {
        /// <summary>Прутковый: сваренные прутки на двух балках между столбами.</summary>
        Bar = 0,

        /// <summary>Кирпичный: кладка блоками, панель + столб + шапка.</summary>
        Masonry = 1
    }

    /// <summary>
    /// Описание типа забора. Один SO = один тип; переключение типа в сцене — смена ссылки
    /// в SplineFence и пересборка (§8: никакого пересоздания спайна).
    ///
    /// Материалы — существующие процедуральные графы проекта (M_PC_Steel_v2, M_PC_Brick, M_PC_Concrete).
    /// Оба графа работают в object space, поэтому масштаб корня забора обязан быть 1.
    /// </summary>
    [CreateAssetMenu(fileName = "FT_Fence_New", menuName = "ProjectC/World/Fence Type", order = 400)]
    public class FenceTypeDefinition : ScriptableObject
    {
        [Header("Стиль")]
        public FenceStyle style = FenceStyle.Bar;

        [Header("Материалы")]
        [Tooltip("Полотно: прутки/балки либо блоки кладки.")]
        public Material primaryMaterial;

        [Tooltip("Столбы и шапка. Если пусто — всё полотно идёт одним сабмешем.")]
        public Material secondaryMaterial;

        [Header("Габариты")]
        [Tooltip("Опорная высота типа, м. Masonry — высота полотна (стены). Bar — ТОЛЬКО номинал, от которого " +
                 "считается переопределение высоты на SplineFence: балки и прутки заданы абсолютными Y " +
                 "(railTopY/barTopY/postHeight), поэтому смена этого числа геометрию Bar не меняет.")]
        public float height = 1.9f;

        [Tooltip("Толщина полотна для коллайдера, м.")]
        public float thickness = 0.06f;

        [Tooltip("Высота бокс-коллайдера панели, м.")]
        public float colliderHeight = 1.9f;

        [Header("Столбы")]
        public float postSize = 0.06f;
        public float postHeight = 1.95f;
        public float postSpacing = 2.5f;

        [Header("Адаптивность (§5)")]
        [Tooltip("Допустимая стрелка прогиба на изгибе, м. Управляет шагом тесселяции.")]
        public float maxDeviation = 0.01f;

        [Tooltip("Излом выше этого угла рвёт прогон и ставит угловой столб.")]
        public float sharpAngleDeg = 25f;

        [Tooltip("Минимальный радиус изгиба, м. 0 = не проверять.")]
        public float minRadius = 0f;

        [Header("Прутковый: балки")]
        public float railSize = 0.04f;
        public float railBottomY = 0.12f;
        public float railTopY = 1.84f;
        public bool midRail = false;
        public float midRailY = 0.95f;

        [Header("Прутковый: прутки")]
        public float barSize = 0.012f;
        public float barSpacing = 0.12f;
        public float barBottomY = 0.18f;
        public float barTopY = 1.8f;

        [Header("Кирпичный: кладка (§7)")]
        [Tooltip("Масштаб кладки материала M_PC_Brick (_Brick_Scale). Модуль = 1 / scale ≈ 0.172 м.")]
        public float brickScale = 5.8f;

        [Tooltip("Допустимая стрелка прогиба хорды панели, м. Больше — ставится лишний столб.")]
        public float panelChordError = 0.02f;

        [Tooltip("Панели в одной меш-секции расходятся по направлению не больше чем на этот угол, °. " +
                 "От него зависит, насколько ровно ляжет кладка (object space).")]
        public float sectionAlignDeg = 1f;

        [Header("Кирпичный: шапка и оголовки")]
        public float copingHeight = 0.06f;

        [Tooltip("Свес шапки за грань стены с каждой стороны, м.")]
        public float copingOverhang = 0.025f;

        [Tooltip("Сторона оголовка столба, м.")]
        public float capSize = 0.45f;

        public float capHeight = 0.06f;

        // ============================================================
        // Переопределения конкретного забора
        // ============================================================

        /// <summary>Границы высоты, м. Ниже 0.3 — уже не забор, выше 8 — уже стена.</summary>
        public const float MinHeight = 0.3f;
        public const float MaxHeight = 8f;

        /// <summary>Границы шага секции (расстояния между столбами), м.</summary>
        public const float MinSectionStep = 0.2f;
        public const float MaxSectionStep = 12f;

        /// <summary>
        /// Тип с переопределениями ОДНОГО забора (<see cref="SplineFence.heightOverride"/> /
        /// <see cref="SplineFence.sectionStepOverride"/>).
        ///
        /// Если переопределять нечего (0 или совпадает с типом) — возвращает сам ассет. Иначе — временную
        /// копию с <see cref="HideFlags.HideAndDontSave"/>: ассет типа общий на все заборы, и править его
        /// ради одного забора нельзя. Копию обязан уничтожить вызывающий (`Object.DestroyImmediate`).
        /// </summary>
        public FenceTypeDefinition ResolveFor(float heightMeters, float stepMeters)
        {
            bool changeHeight = heightMeters > 0.0005f && Mathf.Abs(heightMeters - height) > 0.0005f;
            bool changeStep = stepMeters > 0.0005f && Mathf.Abs(stepMeters - postSpacing) > 0.0005f;

            if (!changeHeight && !changeStep) return this;

            FenceTypeDefinition copy = Instantiate(this);
            copy.name = name + " (переопределение)";
            copy.hideFlags = HideFlags.HideAndDontSave;

            if (changeStep)
                copy.postSpacing = Mathf.Clamp(stepMeters, MinSectionStep, MaxSectionStep);

            if (changeHeight)
                copy.ScaleHeight(Mathf.Clamp(heightMeters, MinHeight, MaxHeight));

            return copy;
        }

        /// <summary>
        /// Тянет вертикальные размеры пропорционально новой высоте: k = target / height.
        ///
        /// Масштабируются только высоты и Y-координаты (полотно, столбы, балки, прутки, высота коллайдера).
        /// Сечения и толщины — postSize, railSize, barSize, barSpacing, thickness, copingHeight, capHeight,
        /// capSize — остаются как есть: это профиль, а не габарит. Забор 1.4 м не должен получить балку 3 см.
        /// </summary>
        private void ScaleHeight(float targetHeight)
        {
            float k = targetHeight / Mathf.Max(0.05f, height);

            height = targetHeight;
            colliderHeight *= k;
            postHeight *= k;

            railBottomY *= k;
            railTopY *= k;
            midRailY *= k;

            barBottomY *= k;
            barTopY *= k;
        }

        /// <summary>Геометрические параметры извлечения пути из этого типа.</summary>
        public FencePathSettings ToPathSettings()
        {
            FencePathSettings s = FencePathSettings.Default;

            s.maxDeviation = Mathf.Max(0.001f, maxDeviation);
            s.sharpAngleDeg = Mathf.Clamp(sharpAngleDeg, 1f, 179f);
            s.minRadius = Mathf.Max(0f, minRadius);
            s.maxStep = Mathf.Max(0.1f, postSpacing);

            // Кладка режется miter'ом по СВОЕМУ столбу (0.38), поэтому полусторона столба —
            // именно postSize/2: торец полотна должен упереться в грань столба, а не висеть в воздухе.
            s.postHalfSize = Mathf.Max(0f, postSize) * 0.5f;

            return s;
        }

        private void OnValidate()
        {
            postSpacing = Mathf.Max(0.2f, postSpacing);
            postSize = Mathf.Max(0.005f, postSize);
            postHeight = Mathf.Max(0.2f, postHeight);
            thickness = Mathf.Max(0.005f, thickness);
            barSpacing = Mathf.Max(0.02f, barSpacing);
            barSize = Mathf.Max(0.002f, barSize);
            railSize = Mathf.Max(0.005f, railSize);

            // прутки должны попадать между балками
            barBottomY = Mathf.Clamp(barBottomY, railBottomY, Mathf.Max(railBottomY, railTopY));
            barTopY = Mathf.Clamp(barTopY, barBottomY, Mathf.Max(barBottomY, railTopY));

            // кирпичный блок
            brickScale = Mathf.Clamp(brickScale, 0.1f, 100f);
            panelChordError = Mathf.Max(0.002f, panelChordError);
            sectionAlignDeg = Mathf.Clamp(sectionAlignDeg, 0.05f, 45f);
            copingHeight = Mathf.Max(0f, copingHeight);
            copingOverhang = Mathf.Max(0f, copingOverhang);
            capHeight = Mathf.Max(0f, capHeight);
            capSize = Mathf.Max(postSize, capSize);
        }
    }
}
