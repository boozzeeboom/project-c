using UnityEngine;
using ProjectC.World.Core;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Математическая модель горного профиля (V2).
    /// Определяет как радиус горы меняется с высотой.
    /// 
    /// Формула: r(h) = R_base * (1 - h/H)^exponent + shoulder(h)
    /// 
    /// ADR-0001: Power-Law Cone Profile
    /// - exponent < 1.0: convex (volcanic/dome — пологие склоны)
    /// - exponent = 1.0: straight cone
    /// - exponent > 1.0: concave (tectonic — крутые склоны, острая вершина)
    /// 
    /// Shoulder Bulge: Gaussian bump на mid-height для характерного "горного плеча"
    /// </summary>
    [System.Serializable]
    public class MountainProfile
    {
        [Header("Profile Type")]
        public PeakShapeType shapeType;

        [Header("Power-Law Exponent")]
        [Tooltip("Форма профиля: <1.0 convex, =1.0 straight, >1.0 concave")]
        [Range(0.3f, 2.0f)]
        public float exponent = 1.0f;

        [Header("Shoulder Bulge (горное плечо)")]
        [Tooltip("Центр плеча на normalized height (0..1)")]
        [Range(0.2f, 0.6f)]
        public float shoulderCenter = 0.35f;

        [Tooltip("Ширина плеча (Gaussian sigma)")]
        [Range(0.05f, 0.3f)]
        public float shoulderWidth = 0.2f;

        [Tooltip("Амплитуда плеча (% от baseRadius)")]
        [Range(0.0f, 0.25f)]
        public float shoulderAmplitude = 0.15f;

        [Header("Large-Scale Noise (silhouette)")]
        [Tooltip("Амплитуда шума силуэта (% от baseRadius)")]
        [Range(0.0f, 0.2f)]
        public float largeNoiseAmplitude = 0.1f;

        [Tooltip("Частота шума силуэта")]
        [Range(1.0f, 6.0f)]
        public float largeNoiseFrequency = 3.0f;

        [Tooltip("Octaves для FBM")]
        [Range(1, 8)]
        public int largeNoiseOctaves = 4;

        [Header("Small-Scale Noise (surface detail)")]
        [Tooltip("Амплитуда детального шума (% от baseRadius)")]
        [Range(0.0f, 0.08f)]
        public float smallNoiseAmplitude = 0.03f;

        [Tooltip("Частота детального шума")]
        [Range(2.0f, 12.0f)]
        public float smallNoiseFrequency = 8.0f;

        [Tooltip("Octaves для FBM")]
        [Range(1, 8)]
        public int smallNoiseOctaves = 6;

        [Header("Ridge Noise (Tectonic only)")]
        [Tooltip("Включить ridge noise (carves linear ridges)")]
        public bool useRidgeNoise = false;

        [Tooltip("Амплитуда ridge noise (% от baseRadius)")]
        [Range(0.0f, 0.15f)]
        public float ridgeNoiseAmplitude = 0.08f;

        [Tooltip("Частота ridge noise")]
        [Range(2.0f, 10.0f)]
        public float ridgeNoiseFrequency = 5.0f;

        [Header("Asymmetry")]
        [Tooltip("Максимальная асимметрия (% от baseRadius)")]
        [Range(0.0f, 0.25f)]
        public float asymmetryAmount = 0.1f;

        [Header("Volcanic Crater")]
        [Tooltip("Создать кратер на вершине (Volcanic type)")]
        public bool hasCrater = false;

        [Tooltip("Радиус кратера (% от baseRadius)")]
        [Range(0.05f, 0.25f)]
        public float craterRadius = 0.15f;

        [Tooltip("Глубина кратера (% от meshHeight)")]
        [Range(0.02f, 0.15f)]
        public float craterDepth = 0.08f;

        /// <summary>
        /// Вычислить радиус на заданной normalized height с учётом shoulder bulge.
        /// </summary>
        /// <param name="normalizedHeight">0 (база) .. 1 (вершина)</param>
        /// <param name="baseRadius">Радиус основания</param>
        /// <returns>Радиус на заданной высоте</returns>
        public float GetRadiusAtHeight(float normalizedHeight, float baseRadius)
        {
            // Clamp normalizedHeight
            normalizedHeight = Mathf.Clamp01(normalizedHeight);

            // Power-law cone: r(h) = R_base * (1 - h)^exponent
            float radiusFactor = Mathf.Pow(1f - normalizedHeight, exponent);
            float radius = baseRadius * radiusFactor;

            // Shoulder bulge: Gaussian bump
            if (shoulderAmplitude > 0f)
            {
                float gaussian = Mathf.Exp(
                    -Mathf.Pow(normalizedHeight - shoulderCenter, 2f) / 
                    (2f * shoulderWidth * shoulderWidth)
                );
                float shoulderOffset = gaussian * shoulderAmplitude * baseRadius;
                radius += shoulderOffset;
            }

            return radius;
        }

        /// <summary>
        /// Создать стандартный preset для заданного типа формы.
        /// </summary>
        public static MountainProfile CreatePreset(PeakShapeType shapeType)
        {
            var profile = new MountainProfile { shapeType = shapeType };

            switch (shapeType)
            {
                case PeakShapeType.Tectonic:
                    // Крутые склоны, острая вершина, ridge noise
                    profile.exponent = 1.4f;
                    profile.shoulderCenter = 0.35f;
                    profile.shoulderWidth = 0.18f;
                    profile.shoulderAmplitude = 0.12f;
                    profile.largeNoiseAmplitude = 0.1f;
                    profile.largeNoiseFrequency = 3.0f;
                    profile.largeNoiseOctaves = 4;
                    profile.smallNoiseAmplitude = 0.03f;
                    profile.smallNoiseFrequency = 8.0f;
                    profile.smallNoiseOctaves = 6;
                    profile.useRidgeNoise = true;
                    profile.ridgeNoiseAmplitude = 0.08f;
                    profile.ridgeNoiseFrequency = 5.0f;
                    profile.asymmetryAmount = 0.12f;
                    profile.hasCrater = false;
                    break;

                case PeakShapeType.Volcanic:
                    // Пологие склоны, округлая вершина, кратер
                    profile.exponent = 0.65f;
                    profile.shoulderCenter = 0.45f;
                    profile.shoulderWidth = 0.22f;
                    profile.shoulderAmplitude = 0.08f;
                    profile.largeNoiseAmplitude = 0.06f;
                    profile.largeNoiseFrequency = 2.0f;
                    profile.largeNoiseOctaves = 3;
                    profile.smallNoiseAmplitude = 0.02f;
                    profile.smallNoiseFrequency = 6.0f;
                    profile.smallNoiseOctaves = 5;
                    profile.useRidgeNoise = false;
                    profile.asymmetryAmount = 0.06f;
                    profile.hasCrater = true;
                    profile.craterRadius = 0.15f;
                    profile.craterDepth = 0.08f;
                    break;

                case PeakShapeType.Dome:
                    // Очень пологий купол, широкий
                    profile.exponent = 0.5f;
                    profile.shoulderCenter = 0.5f;
                    profile.shoulderWidth = 0.25f;
                    profile.shoulderAmplitude = 0.05f;
                    profile.largeNoiseAmplitude = 0.05f;
                    profile.largeNoiseFrequency = 2.0f;
                    profile.largeNoiseOctaves = 3;
                    profile.smallNoiseAmplitude = 0.02f;
                    profile.smallNoiseFrequency = 5.0f;
                    profile.smallNoiseOctaves = 4;
                    profile.useRidgeNoise = false;
                    profile.asymmetryAmount = 0.05f;
                    profile.hasCrater = false;
                    break;

                case PeakShapeType.Isolated:
                    // Массивные плечи, одинокая громада
                    profile.exponent = 1.1f;
                    profile.shoulderCenter = 0.4f;
                    profile.shoulderWidth = 0.2f;
                    profile.shoulderAmplitude = 0.18f;
                    profile.largeNoiseAmplitude = 0.12f;
                    profile.largeNoiseFrequency = 3.5f;
                    profile.largeNoiseOctaves = 5;
                    profile.smallNoiseAmplitude = 0.04f;
                    profile.smallNoiseFrequency = 9.0f;
                    profile.smallNoiseOctaves = 7;
                    profile.useRidgeNoise = false;
                    profile.asymmetryAmount = 0.2f;
                    profile.hasCrater = false;
                    break;
            }

            return profile;
        }
    }
}
