// Project C: Audio Settings Section (T-ESC03b)
// Страница настроек звука внутри EscMenu.
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Core;

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
            panel.Add(SettingsWidgets.CreateSectionHeader("Громкость"));

            panel.Add(SettingsWidgets.CreateSlider("Общая", 0f, 1f,
                SettingsManager.MasterVolume,
                v => SettingsManager.SetMasterVolume(v)));

            // --- Placeholder каналы ---
            panel.Add(SettingsWidgets.CreateSectionHeader("Каналы (требуется AudioMixer)"));
            panel.Add(MakePlaceholderSlider("Музыка"));
            panel.Add(MakePlaceholderSlider("Эффекты"));
            panel.Add(MakePlaceholderSlider("Голос"));
            panel.Add(MakePlaceholderSlider("Интерфейс"));

            var note = new Label("Разделение каналов будет доступно после внедрения AudioMixer.");
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
