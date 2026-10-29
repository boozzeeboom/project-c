// Cloudy Navigator — общие приёмы вида (шаг 4).
//
// Палитра — та же, что в моде (04 §2): цвета берём из ProjectCMainMenuPalette, а не заводим вторую копию.
// Полупрозрачную заливку нельзя задавать сырым sRGB-цветом: проект в Linear, поэтому каждая такая заливка
// проходит через ProjectCMainMenuGamma.Alpha(alpha, tint, backdrop) с подложкой, указанной вызывающим кодом.
// Шрифт — тело из ProjectCMainMenuFonts (SDF-атлас с паддингом 24): тот же приём, что в меню, классы не правятся.
using ProjectCMainMenu;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Палитра, гамма-коррекция и мелкие фабрики элементов окна.</summary>
    public static class CnStyle
    {
        public static readonly Color Ink = ProjectCMainMenuPalette.Ink;
        public static readonly Color Accent = ProjectCMainMenuPalette.Accent;
        public static readonly Color Muted = ProjectCMainMenuPalette.Hex("#a8b6ac");
        public static readonly Color Dim = ProjectCMainMenuPalette.Hex("#71806f");
        public static readonly Color Brass = ProjectCMainMenuPalette.Hex("#b9915a");
        public static readonly Color Danger = ProjectCMainMenuPalette.Hex("#7a2420");

        /// <summary>Тёмная вуаль, на которой стоит всё окно (в моде — облачный фон сцены).</summary>
        public static readonly Color Veil = ProjectCMainMenuPalette.VeilDark;

        /// <summary>Поверхность панели/карточки.</summary>
        public static readonly Color Panel = ProjectCMainMenuPalette.VeilSoft;

        /// <summary>Шрифт тела на корень: в USS он не наследуется из ресурсов проекта, поэтому ставим кодом.</summary>
        public static void ApplyFont(VisualElement root)
        {
            if (root == null) return;

            var fonts = ProjectCMainMenuFonts.Load();
            if (fonts == null) return;

            var asset = fonts.BodyAsset;
            if (asset == null) return;

            root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(asset));
        }

        /// <summary>Полупрозрачная заливка цвета tint поверх подложки backdrop, исправленная под Linear.</summary>
        public static Color Alpha(Color tint, float alpha, Color backdrop)
        {
            float a = ProjectCMainMenuGamma.Alpha(alpha, ProjectCMainMenuGamma.Luma(tint),
                ProjectCMainMenuGamma.Luma(backdrop));
            return new Color(tint.r, tint.g, tint.b, a);
        }

        public static Label Label(string text, string className)
        {
            var label = new Label(text);
            if (!string.IsNullOrEmpty(className)) label.AddToClassList(className);
            return label;
        }

        /// <summary>Подпись капсом: в USS нет text-transform, верхний регистр делает код (04 §1).</summary>
        public static Label Cap(string text)
        {
            return Label(text == null ? string.Empty : text.ToUpperInvariant(), "cn-cap");
        }

        public static VisualElement Hair()
        {
            var hair = new VisualElement();
            hair.AddToClassList("cn-hair");
            return hair;
        }

        public static VisualElement Row(string className)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            if (!string.IsNullOrEmpty(className)) row.AddToClassList(className);
            return row;
        }

        public static VisualElement Spacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("cn-credits__spacer");
            return spacer;
        }

        /// <summary>
        /// Бирка действия (приборная бирка мода): высота 28 в полосе 32, капс 10 px, letter-spacing .14em.
        /// Верхний регистр делает код — в USS text-transform нет (04 §1). Обработчик навешивает вызывающий.
        /// </summary>
        public static VisualElement Tag(string caption, string modifier = null)
        {
            var tag = new VisualElement();
            tag.AddToClassList("cn-tag");
            if (!string.IsNullOrEmpty(modifier)) tag.AddToClassList(modifier);
            tag.Add(Label(caption == null ? string.Empty : caption.ToUpperInvariant(), "cn-tag__text"));
            return tag;
        }

        /// <summary>
        /// Плитка предмета в доке. blocked — визуальная пометка «сейчас нельзя» (нет курса или нет полной упаковки);
        /// причину всё равно называет само действие, поэтому обработчик ставится всегда.
        /// </summary>
        public static VisualElement Chip(string caption, string tooltip, bool blocked)
        {
            var chip = new VisualElement();
            chip.AddToClassList("cn-chip");
            if (blocked) chip.AddToClassList("cn-chip--blocked");
            if (!string.IsNullOrEmpty(tooltip)) chip.tooltip = tooltip;
            chip.Add(Label(caption, "cn-chip__text"));
            return chip;
        }
    }
}
