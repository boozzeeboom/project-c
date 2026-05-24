using UnityEngine;

namespace ProjectC.Core
{
    public class TemperatureFilter : MonoBehaviour
    {
        public TemperatureFilterConfig config;
        
        public Color GetTemperatureOverlay(float temperature)
        {
            if (config == null) return Color.clear;
            
            float t = 0f;
            if (temperature <= config.coldThreshold)
            {
                t = 0f;
            }
            else if (temperature >= config.hotThreshold)
            {
                t = 1f;
            }
            else
            {
                t = Mathf.InverseLerp(config.coldThreshold, config.hotThreshold, temperature);
            }
            
            float blendFactor = config.blendCurve.Evaluate(t);
            
            Color overlay = Color.clear;
            if (temperature <= config.coldThreshold)
            {
                overlay = config.coldOverlayColor;
                overlay.a = blendFactor * (1f - Mathf.Abs(t));
            }
            else if (temperature >= config.hotThreshold)
            {
                overlay = config.hotOverlayColor;
                overlay.a = blendFactor * (t - 1f + 1f);
            }
            
            return overlay;
        }
    }
}
