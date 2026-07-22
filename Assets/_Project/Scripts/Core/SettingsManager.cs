// Project C: Settings Manager (T-ESC02)
// Статический Singleton. Хранит настройки, читает/пишет PlayerPrefs,
// применяет к QualitySettings/Screen/AudioListener при старте.
using System;
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Централизованное хранилище настроек игры.
    /// PlayerPrefs — persistent storage. ApplyAll() вызывается при старте.
    /// </summary>
    public static class SettingsManager
    {
        private const string KEY_MOUSE_SENSITIVITY = "Settings.MouseSensitivity";
        private const string KEY_INVERT_Y = "Settings.InvertY";
        private const string KEY_MASTER_VOLUME = "Settings.MasterVolume";
        private const string KEY_SUBTITLES = "Settings.Subtitles";
        private const string KEY_QUALITY_LEVEL = "Settings.QualityLevel";
        private const string KEY_FULLSCREEN = "Settings.Fullscreen";
        private const string KEY_VSYNC = "Settings.VSync";
        private const string KEY_ANTI_ALIASING = "Settings.AntiAliasing";
        private const string KEY_RESOLUTION = "Settings.Resolution";

#pragma warning disable CS0414
        private static bool _initialized = false;
#pragma warning restore CS0414

        // ===== Свойства =====

        public static float MouseSensitivity { get; private set; } = 3f;
        public static bool InvertY { get; private set; } = false;
        public static float MasterVolume { get; private set; } = 1f;
        public static bool Subtitles { get; private set; } = false;
        public static int QualityLevel { get; private set; } = 2; // Medium by default
        public static bool Fullscreen { get; private set; } = true;
        public static bool VSync { get; private set; } = true;
        public static int AntiAliasing { get; private set; } = 0; // Off

        // ===== События =====

        public static event Action<float> OnMouseSensitivityChanged;
        public static event Action<bool> OnInvertYChanged;
        public static event Action<float> OnMasterVolumeChanged;
        public static event Action<bool> OnSubtitlesChanged;

        // ===== Init =====

        static SettingsManager()
        {
            Load();
        }

        /// <summary>Применить все настройки к движку. Вызвать при старте игры.</summary>
        public static void ApplyAll()
        {
            Debug.Log("[SettingsManager] ApplyAll");

            QualitySettings.SetQualityLevel(QualityLevel, applyExpensiveChanges: true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            QualitySettings.antiAliasing = AntiAliasing;
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            AudioListener.volume = MasterVolume;

            _initialized = true;
        }

        // ===== Setters (сохраняют в PlayerPrefs + применяют при необходимости) =====

        public static void SetMouseSensitivity(float value)
        {
            value = Mathf.Clamp(value, 0.1f, 10f);
            if (Mathf.Approximately(MouseSensitivity, value)) return;
            MouseSensitivity = value;
            PlayerPrefs.SetFloat(KEY_MOUSE_SENSITIVITY, value);
            PlayerPrefs.Save();
            OnMouseSensitivityChanged?.Invoke(value);
        }

        public static void SetInvertY(bool value)
        {
            if (InvertY == value) return;
            InvertY = value;
            PlayerPrefs.SetInt(KEY_INVERT_Y, value ? 1 : 0);
            PlayerPrefs.Save();
            OnInvertYChanged?.Invoke(value);
        }

        public static void SetMasterVolume(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(MasterVolume, value)) return;
            MasterVolume = value;
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, value);
            PlayerPrefs.Save();
            OnMasterVolumeChanged?.Invoke(value);
        }

        public static void SetSubtitles(bool value)
        {
            if (Subtitles == value) return;
            Subtitles = value;
            PlayerPrefs.SetInt(KEY_SUBTITLES, value ? 1 : 0);
            PlayerPrefs.Save();
            OnSubtitlesChanged?.Invoke(value);
        }

        public static void SetQualityLevel(int index)
        {
            if (QualityLevel == index) return;
            QualityLevel = index;
            QualitySettings.SetQualityLevel(index, applyExpensiveChanges: true);
            PlayerPrefs.SetInt(KEY_QUALITY_LEVEL, index);
            PlayerPrefs.Save();
        }

        public static void SetFullscreen(bool value)
        {
            if (Fullscreen == value) return;
            Fullscreen = value;
            Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            PlayerPrefs.SetInt(KEY_FULLSCREEN, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetVSync(bool value)
        {
            if (VSync == value) return;
            VSync = value;
            QualitySettings.vSyncCount = value ? 1 : 0;
            PlayerPrefs.SetInt(KEY_VSYNC, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetAntiAliasing(int value)
        {
            if (AntiAliasing == value) return;
            AntiAliasing = value;
            QualitySettings.antiAliasing = value;
            PlayerPrefs.SetInt(KEY_ANTI_ALIASING, value);
            PlayerPrefs.Save();
        }

        public static void SetResolution(int width, int height, FullScreenMode mode)
        {
            Screen.SetResolution(width, height, mode);
            PlayerPrefs.SetString(KEY_RESOLUTION, $"{width}x{height}");
            PlayerPrefs.Save();
        }

        // ===== Load from PlayerPrefs =====

        public static void Load()
        {
            MouseSensitivity = PlayerPrefs.GetFloat(KEY_MOUSE_SENSITIVITY, 3f);
            InvertY = PlayerPrefs.GetInt(KEY_INVERT_Y, 0) == 1;
            MasterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, 1f);
            Subtitles = PlayerPrefs.GetInt(KEY_SUBTITLES, 0) == 1;
            QualityLevel = PlayerPrefs.GetInt(KEY_QUALITY_LEVEL, 2);
            Fullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, 1) == 1;
            VSync = PlayerPrefs.GetInt(KEY_VSYNC, 1) == 1;
            AntiAliasing = PlayerPrefs.GetInt(KEY_ANTI_ALIASING, 0);

            Debug.Log($"[SettingsManager] Loaded: sens={MouseSensitivity}, invY={InvertY}, vol={MasterVolume}, " +
                      $"qual={QualityLevel}, fs={Fullscreen}, vsync={VSync}, aa={AntiAliasing}");
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(KEY_MOUSE_SENSITIVITY, MouseSensitivity);
            PlayerPrefs.SetInt(KEY_INVERT_Y, InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, MasterVolume);
            PlayerPrefs.SetInt(KEY_SUBTITLES, Subtitles ? 1 : 0);
            PlayerPrefs.SetInt(KEY_QUALITY_LEVEL, QualityLevel);
            PlayerPrefs.SetInt(KEY_FULLSCREEN, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KEY_VSYNC, VSync ? 1 : 0);
            PlayerPrefs.SetInt(KEY_ANTI_ALIASING, AntiAliasing);
            PlayerPrefs.Save();
            Debug.Log("[SettingsManager] Saved");
        }
    }
}
