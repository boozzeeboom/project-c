using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectC.Core
{
    public class TemperatureFilter : MonoBehaviour
    {
        public TemperatureFilterConfig config;

        private Volume _temperatureVolume;
        private ColorAdjustments _colorAdjustments;
        private float _currentTemperature = 20f;

        void Start()
        {
            InitializeTemperatureVolume();
        }

        private void InitializeTemperatureVolume()
        {
            _temperatureVolume = gameObject.GetComponent<Volume>();
            if (_temperatureVolume == null)
            {
                _temperatureVolume = gameObject.AddComponent<Volume>();
            }
            _temperatureVolume.priority = 100;
            _temperatureVolume.isGlobal = true;

            var profile = _temperatureVolume.profile;
            if (profile == null)
            {
                profile = Instantiate(new VolumeProfile());
                _temperatureVolume.profile = profile;
            }

            if (!profile.TryGet<ColorAdjustments>(out _colorAdjustments))
            {
                _colorAdjustments = profile.Add<ColorAdjustments>();
            }

            _colorAdjustments.colorFilter.Override(Color.white);
            _colorAdjustments.saturation.Override(0f);
            _colorAdjustments.postExposure.Override(0f);
        }

        public void Apply(float temperature)
        {
            if (config == null || _colorAdjustments == null) return;

            _currentTemperature = temperature;

            if (temperature > config.coldThreshold && temperature < config.hotThreshold)
            {
                _colorAdjustments.colorFilter.Override(Color.white);
                _colorAdjustments.saturation.Override(0f);
                return;
            }

            float coldBlend = 0f;
            float warmBlend = 0f;

            if (temperature <= config.coldThreshold)
            {
                coldBlend = 1f;
            }
            else if (temperature >= config.hotThreshold)
            {
                warmBlend = 1f;
            }

            Color filterColor = Color.Lerp(Color.white, config.coldOverlayColor, coldBlend * 0.3f);
            filterColor = Color.Lerp(filterColor, config.hotOverlayColor, warmBlend * 0.3f);

            float saturation = -coldBlend * 5f + warmBlend * 3f;

            _colorAdjustments.colorFilter.Override(filterColor);
            _colorAdjustments.saturation.Override(saturation);
        }

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
