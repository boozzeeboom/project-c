using UnityEngine;

namespace ProjectC.Core
{
    [CreateAssetMenu(fileName = "NewTempFilterConfig", menuName = "ProjectC/DayNight/TemperatureFilterConfig")]
    public class TemperatureFilterConfig : ScriptableObject
    {
        [Header("Cold (<= coldThreshold)")]
        public float coldThreshold = 0f;
        public Color coldOverlayColor = new Color(0.3f, 0.4f, 0.6f);
        public float coldSaturationBoost = 0.1f;
        public float coldValueOffset = -0.1f;

        [Header("Hot (>= hotThreshold)")]
        public float hotThreshold = 25f;
        public Color hotOverlayColor = new Color(0.6f, 0.3f, 0.1f);
        public float hotSaturationBoost = 0.1f;
        public float hotValueOffset = 0.05f;

        [Header("Blending")]
        public AnimationCurve blendCurve = AnimationCurve.Linear(0, 0, 1, 1);
    }
}
