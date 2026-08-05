// Project C: Audio Settings Section (T-ESC03b)
// Страница настроек звука внутри EscMenu.
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Core;
using ProjectC.Localization;

namespace ProjectC.UI.EscMenu
{
    /// <summary>
    /// Страница «Звук»: общая громкость через AudioListener.volume.
    /// Остальные каналы (Музыка/Эффекты/Голос/UI) — placeholder до AudioMixer.
    /// </summary>
    public static class AudioSettingsSection
    {
        public static VisualElement Create()
        {
            var panel = new VisualElement();
            panel.style.flexDirection = FlexDirection.Column;

            // --- Общая громкость ---
            panel.Add(SettingsWidgets.CreateSectionHeader("ui.esc_menu.section.volume"));

            panel.Add(SettingsWidgets.CreateSlider("ui.esc_menu.label.master_volume", 0f, 1f,
                SettingsManager.MasterVolume,
                v => SettingsManager.SetMasterVolume(v)));

            // --- Placeholder каналы ---
            panel.Add(SettingsWidgets.CreateSectionHeader("ui.esc_menu.section.channels"));
            panel.Add(MakePlaceholderSlider("ui.esc_menu.label.music"));
            panel.Add(MakePlaceholderSlider("ui.esc_menu.label.effects"));
            panel.Add(MakePlaceholderSlider("ui.esc_menu.label.voice"));
            panel.Add(MakePlaceholderSlider("ui.esc_menu.label.ui"));

            var note = new Label("ui.esc_menu.audio_mixer_note");
            Loc.Bind(note, "ui.esc_menu.audio_mixer_note");
            note.style.color = new Color(0.4f, 0.4f, 0.4f);
            note.style.fontSize = 11;
            note.style.marginTop = 8;
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);

            return panel;
        }

        private static VisualElement MakePlaceholderSlider(string label)
        {
            return SettingsWidgets.CreateSlider(label, 0f, 1f, 1f, _ => { });
        }
    }
}
