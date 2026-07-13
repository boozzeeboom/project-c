// Project C: Settings Widgets Factory (T-ESC02)
// Переиспользуемые UI-компоненты для страниц настроек.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.UI.Client;

namespace ProjectC.UI.EscMenu
{
    /// <summary>
    /// Фабрика виджетов настроек: Slider, Toggle, Dropdown, SectionHeader.
    /// Каждый виджет — готовый VisualElement со стилями и обработчиками.
    /// </summary>
    public static class SettingsWidgets
    {
        private const string USS_CLASS_WIDGET_ROW = "esc-setting-row";
        private const string USS_CLASS_WIDGET_LABEL = "esc-setting-label";
        private const string USS_CLASS_WIDGET_SLIDER = "esc-setting-slider";
        private const string USS_CLASS_WIDGET_TOGGLE = "esc-setting-toggle";
        private const string USS_CLASS_WIDGET_DROPDOWN = "esc-setting-dropdown";
        private const string USS_CLASS_SECTION_HEADER = "esc-section-header";

        // ===== Section Header =====

        /// <summary>Заголовок секции с разделителем.</summary>
        public static VisualElement CreateSectionHeader(string title)
        {
            var header = new Label(title);
            header.AddToClassList(USS_CLASS_SECTION_HEADER);
            return header;
        }

        // ===== Slider =====

        /// <summary>Слайдер с лейблом и значением.</summary>
        public static VisualElement CreateSlider(string label, float min, float max, float initial,
            Action<float> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList(USS_CLASS_WIDGET_ROW);

            // Label
            var labelEl = new Label(label);
            labelEl.AddToClassList(USS_CLASS_WIDGET_LABEL);
            row.Add(labelEl);

            // Slider
            var slider = new Slider(min, max);
            slider.AddToClassList(USS_CLASS_WIDGET_SLIDER);
            slider.value = initial;
            row.Add(slider);

            // Value label
            var valueLabel = new Label(FormatSliderValue(initial, min, max));
            valueLabel.name = "esc-setting-value";
            valueLabel.AddToClassList(USS_CLASS_WIDGET_LABEL);
            valueLabel.style.width = 48;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = FormatSliderValue(evt.newValue, min, max);
                onChange?.Invoke(evt.newValue);
            });

            return row;
        }

        private static string FormatSliderValue(float value, float min, float max)
        {
            if (max <= 1f) return $"{value:P0}";
            if (max <= 100f && min >= 0f) return $"{value:F0}";
            return $"{value:F1}";
        }

        // ===== Toggle =====

        /// <summary>Переключатель с лейблом.</summary>
        public static VisualElement CreateToggle(string label, bool initial,
            Action<bool> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList(USS_CLASS_WIDGET_ROW);

            var labelEl = new Label(label);
            labelEl.AddToClassList(USS_CLASS_WIDGET_LABEL);
            row.Add(labelEl);

            var toggle = new Toggle();
            toggle.AddToClassList(USS_CLASS_WIDGET_TOGGLE);
            toggle.value = initial;
            row.Add(toggle);

            toggle.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));

            return row;
        }

        // ===== Dropdown =====

        /// <summary>Выпадающий список с лейблом (использует CustomDropdown).</summary>
        public static VisualElement CreateDropdown(string label, List<string> choices,
            int selectedIndex, Action<int> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList(USS_CLASS_WIDGET_ROW);

            var labelEl = new Label(label);
            labelEl.AddToClassList(USS_CLASS_WIDGET_LABEL);
            row.Add(labelEl);

            var dropdown = new CustomDropdown();
            dropdown.AddToClassList(USS_CLASS_WIDGET_DROPDOWN);
            dropdown.SetChoices(choices, selectedIndex >= 0 ? selectedIndex : 0);
            dropdown.OnSelectionChanged += onChange;
            row.Add(dropdown);

            return row;
        }
    }
}
