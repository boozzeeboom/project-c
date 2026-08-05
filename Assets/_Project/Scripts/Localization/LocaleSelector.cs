// Project C: Locale Selector (LOC-02)
// Wraps LocalizationSettings.SelectedLocale with persistence via SettingsManager.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace ProjectC.Localization
{
    /// <summary>
    /// Manages locale switching. Reads/writes SettingsManager.Locale.
    /// Bootstrap: call LoadSaved() before first UI.
    /// </summary>
    public static class LocaleSelector
    {
        private static readonly string[] SupportedCodes = { "ru", "zh", "en", "es", "de", "fr", "pt", "ja", "hi" };

        public static readonly (string code, string nativeName)[] Locales = new[]
        {
            ("ru", "Русский"),
            ("zh", "中文"),
            ("en", "English"),
            ("es", "Español"),
            ("de", "Deutsch"),
            ("fr", "Français"),
            ("pt", "Português"),
            ("ja", "日本語"),
            ("hi", "हिन्दी"),
        };

        /// <summary>
        /// Set locale by code. Persists to SettingsManager and switches immediately.
        /// </summary>
        public static void SetLocale(string code)
        {
            code = code.ToLowerInvariant();
            Debug.Log($"[LocaleSelector] Setting locale to: {code}");

            // Find the locale asset
            var availableLocales = LocalizationSettings.AvailableLocales;
            if (availableLocales == null)
            {
                Debug.LogError("[LocaleSelector] AvailableLocales is null — LocalizationSettings not configured.");
                return;
            }

            Locale targetLocale = null;
            foreach (var loc in availableLocales.Locales)
            {
                if (loc.Identifier.Code.Equals(code, System.StringComparison.OrdinalIgnoreCase))
                {
                    targetLocale = loc;
                    break;
                }
            }

            if (targetLocale == null)
            {
                Debug.LogError($"[LocaleSelector] Locale '{code}' not found in AvailableLocales.");
                return;
            }

            if (LocalizationSettings.SelectedLocale == targetLocale)
            {
                Debug.Log($"[LocaleSelector] Locale '{code}' is already selected.");
                return;
            }

            LocalizationSettings.SelectedLocale = targetLocale;
            PlayerPrefs.SetString("Settings.Locale", code);
            PlayerPrefs.Save();
            Debug.Log($"[LocaleSelector] Locale set to: {code}");
        }

        /// <summary>
        /// Load saved locale or default to "ru". Call in bootstrap before first UI.
        /// </summary>
        public static void LoadSaved()
        {
            var saved = PlayerPrefs.GetString("Settings.Locale", "ru");
            if (!IsSupported(saved))
            {
                Debug.LogWarning($"[LocaleSelector] Saved locale '{saved}' is not supported, falling back to 'ru'.");
                saved = "ru";
            }

            Debug.Log($"[LocaleSelector] Loading saved locale: {saved}");
            SetLocale(saved);
        }

        private static bool IsSupported(string code)
        {
            foreach (var c in SupportedCodes)
                if (c.Equals(code, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
