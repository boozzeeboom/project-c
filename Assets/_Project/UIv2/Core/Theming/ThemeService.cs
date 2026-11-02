using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Core.Theming
{
    public class ThemeService : ProjectC.UIv2.Core.UIFramework.IThemeService
    {
        public ThemeData Current { get; private set; }
        public event Action<ThemeData> Changed;

        public void Apply(ThemeData theme, VisualElement root)
        {
            Current = theme;
            if (root != null && theme != null && theme.themeStyleSheet != null)
            {
                if (!root.styleSheets.Contains(theme.themeStyleSheet))
                    root.styleSheets.Add(theme.themeStyleSheet);
            }
            Changed?.Invoke(theme);
        }

        public void Apply(string themeId, VisualElement root)
        {
            // Placeholder: resolve theme by id from resources/registry in M1.x
            Debug.Log($"[ThemeService] Apply theme '{themeId}' (resolver not implemented yet)");
        }
    }
}
