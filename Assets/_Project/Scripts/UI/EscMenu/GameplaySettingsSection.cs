// Project C: Gameplay Settings Section (T-ESC03c)
// Страница настроек геймплея внутри EscMenu.
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Core;

namespace ProjectC.UI.EscMenu
{
    /// <summary>
    /// Страница «Геймплей»: чувствительность мыши, инвертировать Y, субтитры.
    /// Language — DEFERRED (нет инфраструктуры локализации).
    /// </summary>
    public static class GameplaySettingsSection
    {
        public static VisualElement Create()
        {
            var panel = new VisualElement();
            panel.style.flexDirection = FlexDirection.Column;

            // --- Чувствительность мыши ---
            panel.Add(SettingsWidgets.CreateSectionHeader("Управление"));

            panel.Add(SettingsWidgets.CreateSlider("Чувств. мыши", 0.1f, 10f,
                SettingsManager.MouseSensitivity,
                v => SettingsManager.SetMouseSensitivity(v)));

            // --- Инвертировать Y ---
            panel.Add(SettingsWidgets.CreateToggle("Инвертировать Y", SettingsManager.InvertY,
                v => SettingsManager.SetInvertY(v)));

            panel.Add(SettingsWidgets.CreateSlider("Чувств. зума", 0.5f, 15f,
                SettingsManager.CameraZoomSensitivity,
                v => SettingsManager.SetCameraZoomSensitivity(v)));

            // --- Субтитры ---
            panel.Add(SettingsWidgets.CreateSectionHeader("Доступность"));

            panel.Add(SettingsWidgets.CreateToggle("Субтитры", SettingsManager.Subtitles,
                v => SettingsManager.SetSubtitles(v)));

            // --- Language (LOC-02) ---
            panel.Add(SettingsWidgets.CreateSectionHeader("Язык / Language"));
            var localeChoices = new System.Collections.Generic.List<string>();
            foreach (var (code, name) in ProjectC.Localization.LocaleSelector.Locales)
                localeChoices.Add(name);
            var savedLocale = SettingsManager.Locale ?? "ru";
            var selectedIdx = 0;
            for (int i = 0; i < ProjectC.Localization.LocaleSelector.Locales.Length; i++)
            {
                if (ProjectC.Localization.LocaleSelector.Locales[i].code == savedLocale)
                {
                    selectedIdx = i;
                    break;
                }
            }
            panel.Add(SettingsWidgets.CreateDropdown("Язык", localeChoices, selectedIdx,
                idx => ProjectC.Localization.LocaleSelector.SetLocale(
                    ProjectC.Localization.LocaleSelector.Locales[idx].code)));

            return panel;
        }
    }
}
