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

            // --- Дальность прорисовки (T-LOD01) ---
            // Ключи локализации ui.esc_menu.section.view_distance / label.view_distance /
            // view_distance.near|medium|far / view_distance.ultra_hint —
            // отдельным LOC-проходом в UI_Table; до тех пор русские литералы (RU-fallback).
            // Choices — через Loc.Get(key, ruLiteral): подхватят перевод автоматически.
            panel.Add(SettingsWidgets.CreateSectionHeader("Дальность прорисовки"));

            var vdChoices = new List<string> {
                ProjectC.Localization.Loc.Get("ui.esc_menu.view_distance.near", "Близкая — максимум FPS"),
                ProjectC.Localization.Loc.Get("ui.esc_menu.view_distance.medium", "Средняя — баланс"),
                ProjectC.Localization.Loc.Get("ui.esc_menu.view_distance.far", "Дальняя — кинематографично")
            };
            int vdIndex = Mathf.Clamp((int)SettingsManager.ViewDistance, 0, vdChoices.Count - 1);
            panel.Add(SettingsWidgets.CreateDropdown("Дальность прорисовки", vdChoices, vdIndex,
                idx =>
                {
                    SettingsManager.SetViewDistance((ViewDistance)idx);
                    ProjectC.World.ViewDistanceApplier.ApplyAll(); // напрямую, как Эффекты: не зависим от подписок
                }));

            // Ultra зарезервирован (межрегиональный фон, сцен 2+ нет) — в dropdown не добавляем,
            // только хинт. Разблокировка — Phase 5 (см. docs/world/optimization).
            var ultraHint = new Label("Ультра (межрегиональный фон) — появится с новыми сценами");
            ultraHint.style.fontSize = 12;
            ultraHint.style.opacity = 0.7f;
            panel.Add(ultraHint);

            // --- Эффекты (постобработка) ---
            // Ключи локализации ui.esc_menu.section.effects / label.dof|edge|tempfilter —
            // отдельным LOC-проходом; до тех пор русские литералы (RU-fallback).
            panel.Add(SettingsWidgets.CreateSectionHeader("Эффекты"));

            panel.Add(SettingsWidgets.CreateToggle("Глубина резкости (фокус)",
                SettingsManager.DepthOfField,
                v =>
                {
                    SettingsManager.SetDepthOfField(v);
                    ProjectC.Rendering.GraphicsEffectsApplier.ApplyDepthOfField(v);
                }));

            panel.Add(SettingsWidgets.CreateToggle("Контурный едж",
                SettingsManager.EdgeDetection,
                v =>
                {
                    SettingsManager.SetEdgeDetection(v);
                    ProjectC.Rendering.GraphicsEffectsApplier.ApplyEdgeDetection(v);
                }));

            panel.Add(SettingsWidgets.CreateToggle("Температура день/ночь",
                SettingsManager.TemperatureFilter,
                v => SettingsManager.SetTemperatureFilter(v)));

            return panel;
        }
    }
}
