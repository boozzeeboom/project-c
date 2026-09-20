// Project C: применение пресета дальности прорисовки (T-LOD01)
// Static-апplier по образцу GraphicsEffectsApplier: null-safe, правок сцен не требует,
// сцены с фокусом может не быть (Bootstrap, меню).
// Владеет: camera.far, QualitySettings.lodBias/shadowDistance,
// Terrain_0_0.heightmapPixelError/basemapDistance.
// НЕ владеет и никогда не выключает: сам террейн (L2-фон всегда видим), чанки стриминга.
// Мировых Vector3 не хранит — хук Floating Origin не нужен.
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.Core;

namespace ProjectC.World
{
    public static class ViewDistanceApplier
    {
        private const string CONFIG_RESOURCE_PATH = "Config/ViewDistanceConfig";
        private const string TERRAIN_OBJECT_NAME = "Terrain_0_0";
        private const string CULLER_HOST_NAME = "ViewDistanceRuntime";

        private static bool _subscribed;
        private static ViewDistanceConfig _config;
        private static bool _configTried;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Без domain reload (быстрый вход в Play) статики переживают сессию:
            // сбрасываем флаг, иначе Init пропустит повторную подписку и тумблер меню ничего не применит.
            _subscribed = false;
            _configTried = false;
            _config = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (_subscribed) return;
            _subscribed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SettingsManager.OnViewDistanceChanged += OnViewDistanceChanged;
            SettingsManager.OnQualityLevelChanged += OnQualityLevelChanged;
            EnsureCullerHost();
            ApplyAll();
        }

        /// <summary>Активный пресет: из конфига, иначе кодовые дефолты. Ultra сводится к Far.</summary>
        public static ViewDistanceConfig.Preset GetPreset(ViewDistance v)
        {
            var cfg = LoadConfig();
            if (cfg != null) return cfg.Get(v);
            return ViewDistanceConfig.DefaultFor(v);
        }

        /// <summary>Far для камер (вызывать из Awake камер, в т.ч. runtime-спавна).</summary>
        public static float ResolveCameraFar()
        {
            return GetPreset(SettingsManager.ViewDistance).cameraFar;
        }

        /// <summary>Множитель плотности тумана для DayNightController (владение плотностью — у фаз дня).</summary>
        public static float GetFogScale()
        {
            return GetPreset(SettingsManager.ViewDistance).fogScale;
        }

        /// <summary>Применить пресет ко всем живым объектам (можно вызывать после спавна камер/мира).</summary>
        public static void ApplyAll()
        {
            var v = SettingsManager.ViewDistance;
            if (v == ViewDistance.Ultra)
                Debug.Log("[ViewDistance] Ultra зарезервирован (сцен 2+ нет) — применён Far. Разблокировка — Phase 5.");
            var p = GetPreset(v);

            QualitySettings.lodBias = p.lodBias;
            QualitySettings.shadowDistance = p.shadowDistance;

            int camCount = 0;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam == null || !cam.isActiveAndEnabled) continue;
                if (cam.cameraType != CameraType.Game) continue;
                cam.farClipPlane = p.cameraFar;
                camCount++;
            }

            var terrain = FindTerrain();
            if (terrain != null)
            {
                terrain.heightmapPixelError = p.terrainPixelError;
                terrain.basemapDistance = p.terrainBasemapDistance;
            }

            Debug.Log($"[ViewDistance] Пресет {v}: far={p.cameraFar:F0}, lodBias={p.lodBias}, shadow={p.shadowDistance:F0}, " +
                      $"terrainPixelError={p.terrainPixelError}, basemap={p.terrainBasemapDistance}, detailCull={p.detailCullDistance}, fogScale={p.fogScale} " +
                      $"| камер: {camCount}, террейн: {(terrain != null ? terrain.name : "NULL")}");
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, LoadSceneMode m)
        {
            EnsureCullerHost();
            ApplyAll();
        }

        private static void OnViewDistanceChanged(ViewDistance v)
        {
            ApplyAll();
        }

        private static void OnQualityLevelChanged(int _)
        {
            // SetQualityLevel(applyExpensiveChanges:true) сбрасывает lodBias/shadowDistance
            // из ассета качества — возвращаем значения пресета дальности.
            ApplyAll();
        }

        private static Terrain FindTerrain()
        {
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains == null || terrains.Length == 0) return null;
            foreach (var t in terrains)
                if (t != null && t.name == TERRAIN_OBJECT_NAME) return t;
            foreach (var t in terrains)
                if (t != null && t.isActiveAndEnabled) return t;
            return null;
        }

        private static ViewDistanceConfig LoadConfig()
        {
            if (_configTried) return _config;
            _configTried = true;
            _config = Resources.Load<ViewDistanceConfig>(CONFIG_RESOURCE_PATH);
            if (_config == null)
                Debug.Log("[ViewDistance] Конфиг в Resources не найден — кодовые дефолты. Создайте через Project C → View Distance Config.");
            return _config;
        }

        private static void EnsureCullerHost()
        {
            if (Object.FindAnyObjectByType<DetailDistanceCuller>() != null) return;
            var host = GameObject.Find(CULLER_HOST_NAME);
            if (host == null)
            {
                host = new GameObject(CULLER_HOST_NAME);
                Object.DontDestroyOnLoad(host);
            }
            if (host.GetComponent<DetailDistanceCuller>() == null)
                host.AddComponent<DetailDistanceCuller>();
        }
    }
}
