// Project C: применение пользовательских тумблеров графики (Esc → Настройки → Графика → Эффекты).
// Связывает SettingsManager (ProjectC.Core) с живыми объектами рендера.
// Все методы null-safe: объектов/сцены с фокусом может не быть (Bootstrap, меню).
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ProjectC.Core;

namespace ProjectC.Rendering
{
    /// <summary>
    /// Применяет DepthOfField/EdgeDetection из SettingsManager к рантайм-объектам.
    /// TemperatureFilter применяется самим DayNightController (гард в ApplyTemperatureFilter).
    /// Мировых Vector3 не хранит — хук Floating Origin не нужен.
    /// </summary>
    public static class GraphicsEffectsApplier
    {
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (_subscribed) return;
            _subscribed = true;
            SettingsManager.OnDepthOfFieldChanged += ApplyDepthOfField;
            SettingsManager.OnEdgeDetectionChanged += ApplyEdgeDetection;
            ApplyAll();
        }

        /// <summary>Применить все тумблеры (вызывать после спавна камер/мира тоже можно).</summary>
        public static void ApplyAll()
        {
            ApplyDepthOfField(SettingsManager.DepthOfField);
            ApplyEdgeDetection(SettingsManager.EdgeDetection);
        }

        public static void ApplyDepthOfField(bool enabled)
        {
            // Bokeh-автофокус: сам гасит Volume weight.
            var gaze = Object.FindAnyObjectByType<GazeAutofocus>(FindObjectsInactive.Include);
            if (gaze != null) gaze.SetFocusEnabled(enabled);

            // Far-pass: флаг + контроллер целиком.
            foreach (var f in FindFeatures<DistantFocusRenderFeature>())
                f.Active = enabled;
            var far = Object.FindAnyObjectByType<FarFocusController>(FindObjectsInactive.Include);
            if (far != null) far.enabled = enabled;
        }

        public static void ApplyEdgeDetection(bool enabled)
        {
            foreach (var f in FindFeatures<EdgeDetectionRenderFeature>())
                f.SetActive(enabled);
        }

        private static System.Collections.Generic.List<T> FindFeatures<T>()
            where T : ScriptableRendererFeature
        {
            var result = new System.Collections.Generic.List<T>();
            // Инстансы фич — суб-ассеты renderer-data; YAML ассета руками не трогаем.
            var datas = Resources.FindObjectsOfTypeAll<ScriptableRendererData>();
            foreach (var d in datas)
            {
                if (d == null || d.rendererFeatures == null) continue;
                foreach (var f in d.rendererFeatures)
                    if (f is T typed && !result.Contains(typed))
                        result.Add(typed);
            }
            return result;
        }
    }
}
