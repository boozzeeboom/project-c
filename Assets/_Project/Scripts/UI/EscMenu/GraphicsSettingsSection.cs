// Project C: Graphics Settings Section (T-ESC03a)
// Страница настроек графики внутри EscMenu.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Core;
using ProjectC.Localization;

namespace ProjectC.UI.EscMenu
{
    /// <summary>
    /// Страница «Графика»: качество, разрешение, полный экран, VSync, сглаживание.
    /// </summary>
    public static class GraphicsSettingsSection
    {
        public static VisualElement Create()
        {
            var panel = new VisualElement();
            panel.style.flexDirection = FlexDirection.Column;

            // --- Качество ---
            panel.Add(SettingsWidgets.CreateSectionHeader("ui.esc_menu.section.quality"));

            var qualityLevels = QualitySettings.names;
            var qualityChoices = new List<string>(qualityLevels);
            var qualityIndex = SettingsManager.QualityLevel;
            if (qualityIndex >= qualityChoices.Count) qualityIndex = qualityChoices.Count - 1;

            panel.Add(SettingsWidgets.CreateDropdown("ui.esc_menu.label.quality", qualityChoices, qualityIndex,
                idx => SettingsManager.SetQualityLevel(idx)));

            // --- Разрешение ---
            panel.Add(SettingsWidgets.CreateSectionHeader("ui.esc_menu.section.screen"));

            var resolutions = Screen.resolutions;
            var resChoices = new List<string>();
            int currentResIdx = 0;
            var currentRes = Screen.currentResolution;
            for (int i = 0; i < resolutions.Length; i++)
            {
                var r = resolutions[i];
                resChoices.Add($"{r.width}×{r.height} @ {r.refreshRateRatio.value:F0}Hz");
                if (r.width == currentRes.width && r.height == currentRes.height)
                    currentResIdx = i;
            }
            if (resChoices.Count == 0) resChoices.Add($"{currentRes.width}×{currentRes.height}");

            panel.Add(SettingsWidgets.CreateDropdown("ui.esc_menu.label.resolution", resChoices, currentResIdx,
                idx =>
                {
                    if (idx >= 0 && idx < resolutions.Length)
                    {
                        var r = resolutions[idx];
                        SettingsManager.SetResolution(r.width, r.height,
                            SettingsManager.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                    }
                }));

            // --- Полный экран ---
            panel.Add(SettingsWidgets.CreateToggle("ui.esc_menu.label.fullscreen", SettingsManager.Fullscreen,
                v => SettingsManager.SetFullscreen(v)));

            // --- VSync ---
            panel.Add(SettingsWidgets.CreateToggle("ui.esc_menu.label.vsync", SettingsManager.VSync,
                v => SettingsManager.SetVSync(v)));

            // --- Сглаживание ---
            var aaChoices = new List<string> {
                ProjectC.Localization.Loc.Get("ui.esc_menu.aa.off"),
                ProjectC.Localization.Loc.Get("ui.esc_menu.aa.2x"),
                ProjectC.Localization.Loc.Get("ui.esc_menu.aa.4x"),
                ProjectC.Localization.Loc.Get("ui.esc_menu.aa.8x")
            };
            int aaIdx = SettingsManager.AntiAliasing switch
            {
                2 => 1,
                4 => 2,
                8 => 3,
                _ => 0
            };
            panel.Add(SettingsWidgets.CreateDropdown("ui.esc_menu.label.antialiasing", aaChoices, aaIdx,
                idx =>
                {
                    int aa = idx switch { 1 => 2, 2 => 4, 3 => 8, _ => 0 };
                    SettingsManager.SetAntiAliasing(aa);
                }));

            return panel;
        }
    }
}
