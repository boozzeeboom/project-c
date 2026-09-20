// Project C: View Distance Config (T-LOD01)
// ScriptableObject-менеджер значений дальности прорисовки.
// ВСЕ ЧИСЛА ПРАВЯТСЯ ЗДЕСЬ (инспектор), НЕ в коде: точных замеров пока нет,
// после тестов калибруем пресеты без перекомпиляции.
// Ассет: Assets/_Project/Resources/Config/ViewDistanceConfig.asset (Resources — чтобы static-апplier мог подхватить).
// Нет ассета — используются кодовые дефолты DefaultFor (те же стартовые числа из docs/world/optimization).
using System;
using UnityEngine;

namespace ProjectC.World
{
    [CreateAssetMenu(menuName = "Project C/View Distance Config", fileName = "ViewDistanceConfig")]
    public class ViewDistanceConfig : ScriptableObject
    {
        [Serializable]
        public struct Preset
        {
            [Tooltip("Дальняя плоскость камер (м). Сцена 80 км: 30K/60K/120K вместо старого 1M.")]
            public float cameraFar;
            [Tooltip("Смещение LOD (QualitySettings.lodBias).")]
            public float lodBias;
            [Tooltip("Детализация геометрии террейна. Текущий вид = 200 (см. docs/world/terrain/README).")]
            public float terrainPixelError;
            [Tooltip("Дистанция детальной раскраски террейна (м). Дальше — basemap-фон.")]
            public float terrainBasemapDistance;
            [Tooltip("Дальность теней (QualitySettings.shadowDistance, м). Главный рычаг FPS.")]
            public float shadowDistance;
            [Tooltip("Дальше этой дистанции (м) мелкие детали (руины) визуально скрываются.")]
            public float detailCullDistance;
        }

        [Header("Близкая — максимум FPS")]
        public Preset near = new Preset
        {
            cameraFar = 30000f,
            lodBias = 0.7f,
            terrainPixelError = 300f,
            terrainBasemapDistance = 12000f,
            shadowDistance = 500f,
            detailCullDistance = 4000f
        };

        [Header("Средняя — баланс (дефолт, = текущий вид)")]
        public Preset medium = new Preset
        {
            cameraFar = 60000f,
            lodBias = 1.0f,
            terrainPixelError = 200f,
            terrainBasemapDistance = 20000f,
            shadowDistance = 1500f,
            detailCullDistance = 8000f
        };

        [Header("Дальняя — кинематографично")]
        public Preset far = new Preset
        {
            cameraFar = 120000f,
            lodBias = 1.3f,
            terrainPixelError = 50f,
            terrainBasemapDistance = 80000f,
            shadowDistance = 4000f,
            detailCullDistance = 12000f
        };

        /// <summary>Пресет по значению (Ultra сводится к Far — резерв Phase 5).</summary>
        public Preset Get(ProjectC.Core.ViewDistance v)
        {
            switch (v)
            {
                case ProjectC.Core.ViewDistance.Near: return near;
                case ProjectC.Core.ViewDistance.Far: return far;
                case ProjectC.Core.ViewDistance.Ultra: return far;
                default: return medium;
            }
        }

        /// <summary>Кодовые дефолты — fallback, если ассет конфига не найден. Те же числа, что выше.</summary>
        public static Preset DefaultFor(ProjectC.Core.ViewDistance v)
        {
            switch (v)
            {
                case ProjectC.Core.ViewDistance.Near:
                    return new Preset { cameraFar = 30000f, lodBias = 0.7f, terrainPixelError = 300f, terrainBasemapDistance = 12000f, shadowDistance = 500f, detailCullDistance = 4000f };
                case ProjectC.Core.ViewDistance.Far:
                case ProjectC.Core.ViewDistance.Ultra:
                    return new Preset { cameraFar = 120000f, lodBias = 1.3f, terrainPixelError = 50f, terrainBasemapDistance = 80000f, shadowDistance = 4000f, detailCullDistance = 12000f };
                default:
                    return new Preset { cameraFar = 60000f, lodBias = 1.0f, terrainPixelError = 200f, terrainBasemapDistance = 20000f, shadowDistance = 1500f, detailCullDistance = 8000f };
            }
        }
    }
}
