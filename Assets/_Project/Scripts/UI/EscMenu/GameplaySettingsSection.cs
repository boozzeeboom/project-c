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

            // --- Субтитры ---
            panel.Add(SettingsWidgets.CreateSectionHeader("Доступность"));

            panel.Add(SettingsWidgets.CreateToggle("Субтитры", SettingsManager.Subtitles,
                v => SettingsManager.SetSubtitles(v)));

            // --- Language: DEFERRED ---
            var note = new Label("Выбор языка будет доступен после внедрения локализации.");
            note.style.color = new Color(0.4f, 0.4f, 0.4f);
            note.style.fontSize = 11;
            note.style.marginTop = 8;
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);

            return panel;
        }
    }
}
