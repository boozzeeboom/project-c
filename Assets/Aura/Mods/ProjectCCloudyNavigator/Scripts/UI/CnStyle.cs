// Cloudy Navigator — общие приёмы вида.
//
// Палитра — та же, что в моде (src/palette.js): цвета берём из ProjectCMainMenuPalette, где они уже есть, остальное —
// ровно ключи PAL мода. Полупрозрачную заливку нельзя задавать сырым sRGB-цветом: проект в Linear, поэтому каждая
// такая заливка проходит через ProjectCMainMenuGamma.Alpha(alpha, tint, backdrop) с подложкой — вуалью окна.
// Шрифт — тело из ProjectCMainMenuFonts (SDF-атлас с паддингом 24): тот же приём, что в меню.
//
// Ширина текста для раскладки тросов (lines.js мода мерил её canvas.measureText) здесь — оценка по средней ширине
// глифа Inter: точного измерителя вне панели нет, а раскладке нужна только ширина «на глаз» для зазоров.
using ProjectCMainMenu;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Палитра, гамма-коррекция и мелкие фабрики элементов окна.</summary>
    public static class CnStyle
    {
        // ---- PAL мода (src/palette.js) ----
        public static readonly Color Veil = ProjectCMainMenuPalette.Hex("#070a09");       // veilDark
        public static readonly Color VeilMid = ProjectCMainMenuPalette.Hex("#121711");
        public static readonly Color VeilSoft = ProjectCMainMenuPalette.Hex("#1f2620");
        public static readonly Color VeilHi = ProjectCMainMenuPalette.Hex("#2b342c");
        public static readonly Color VeilEdge = ProjectCMainMenuPalette.Hex("#4a574c");
        public static readonly Color GasMid = ProjectCMainMenuPalette.Hex("#16412c");
        public static readonly Color GasBright = ProjectCMainMenuPalette.Hex("#2f7c50");
        public static readonly Color GasAccent = ProjectCMainMenuPalette.Hex("#54a373");
        public static readonly Color Ink = ProjectCMainMenuPalette.Hex("#eef3ef");
        public static readonly Color Muted = ProjectCMainMenuPalette.Hex("#a8b6ac");       // inkDim
        public static readonly Color Dim = ProjectCMainMenuPalette.Hex("#71806f");         // inkFaint
        public static readonly Color Accent = ProjectCMainMenuPalette.Hex("#78b891");
        public static readonly Color BrassDeep = ProjectCMainMenuPalette.Hex("#8d7a4e");   // PAL.brass
        public static readonly Color Brass = ProjectCMainMenuPalette.Hex("#b9915a");       // латунь бирок
        public static readonly Color Warn = ProjectCMainMenuPalette.Hex("#c98a4b");
        public static readonly Color DangerLine = ProjectCMainMenuPalette.Hex("#c25b45");  // PAL.danger
        public static readonly Color DangerInk = ProjectCMainMenuPalette.Hex("#e08a72");   // --danger-ink
        public static readonly Color Danger = ProjectCMainMenuPalette.Hex("#7a2420");

        /// <summary>Поверхность панели/карточки.</summary>
        public static readonly Color Panel = VeilSoft;

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

        /// <summary>Полупрозрачный цвет tint поверх подложки backdrop, исправленный под Linear.</summary>
        public static Color Alpha(Color tint, float alpha, Color backdrop)
        {
            float a = ProjectCMainMenuGamma.Alpha(alpha, ProjectCMainMenuGamma.Luma(tint),
                ProjectCMainMenuGamma.Luma(backdrop));
            return new Color(tint.r, tint.g, tint.b, a);
        }

        /// <summary>Тот же цвет с альфой поверх вуали окна — для заливок и волосков, которые ставит код.</summary>
        public static Color A(Color tint, float alpha) => Alpha(tint, alpha, Veil);

        /// <summary>Заливка элемента цветом мода rgba(tint, alpha) поверх вуали.</summary>
        public static void Fill(VisualElement element, Color tint, float alpha)
        {
            if (element == null) return;
            element.style.backgroundColor = A(tint, alpha);
        }

        public static Label Label(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList("cn-l");   // сброс отступов темы (CN_Window.uss)
            if (!string.IsNullOrEmpty(className)) label.AddToClassList(className);
            return label;
        }

        /// <summary>Подпись капсом: в USS нет text-transform, верхний регистр делает код.</summary>
        public static Label Cap(string text, string className = "cn-cap")
        {
            return Label(text == null ? string.Empty : text.ToUpperInvariant(), className);
        }

        public static VisualElement Box(string className)
        {
            var box = new VisualElement();
            if (!string.IsNullOrEmpty(className)) box.AddToClassList(className);
            return box;
        }

        public static VisualElement Row(string className)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            if (!string.IsNullOrEmpty(className)) row.AddToClassList(className);
            return row;
        }

        /// <summary>Служебный ребёнок без событий (ореол, волосок, заклёпка).</summary>
        public static VisualElement Deco(string className)
        {
            var deco = Box(className);
            deco.pickingMode = PickingMode.Ignore;
            return deco;
        }

        /// <summary>
        /// Оценка ширины строки шрифтом Inter (средний глиф ≈ 0.55 em, жирный ≈ 0.6 em) плюс разрядка.
        /// Нужна только раскладке тросов и бирок; если ошиблась на пару пикселей — подпись режется многоточием.
        /// </summary>
        public static float TextWidth(string text, float size, bool bold, float letterSpacing = 0f)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            float em = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ') em += 0.28f;
                else if (c == '·' || c == '.' || c == ',' || c == ':' || c == 'i' || c == 'l' || c == '|') em += 0.3f;
                else if (char.IsDigit(c)) em += 0.6f;
                else if (char.IsUpper(c)) em += 0.68f;
                else em += 0.56f;
            }

            return em * size * (bold ? 1.06f : 1f) + letterSpacing * text.Length;
        }

        /// <summary>
        /// Приборная бирка мода (.ln-act/.pin-btn): плитка 32 px, волосяная рамка-подложка, две заклёпки, капс с
        /// разрядкой, глагол слева и значение справа через волосок. Обработчик навешивает вызывающий.
        /// </summary>
        public static VisualElement Tag(string verb, string value = null, string modifier = null, float tilt = 0f)
        {
            var tag = Box("cn-tag");
            if (!string.IsNullOrEmpty(modifier)) tag.AddToClassList(modifier);

            var plate = Deco("cn-tag__plate");
            if (Mathf.Abs(tilt) > 0.01f) plate.style.rotate = new Rotate(new Angle(tilt, AngleUnit.Degree));
            Fill(plate, ProjectCMainMenuPalette.Hex("#090e0b"), 0.62f);
            tag.Add(plate);

            tag.Add(Rivet("cn-tag__riv--a"));
            tag.Add(Label(verb == null ? string.Empty : verb.ToUpperInvariant(), "cn-tag__v"));

            if (!string.IsNullOrEmpty(value))
            {
                tag.Add(Deco("cn-tag__sep"));
                tag.Add(Label(value.ToUpperInvariant(), "cn-tag__x"));
            }
            else
            {
                tag.AddToClassList("cn-tag--solo");
            }

            tag.Add(Rivet("cn-tag__riv--b"));
            return tag;
        }

        private static VisualElement Rivet(string side)
        {
            var riv = Deco("cn-tag__riv");
            riv.AddToClassList(side);
            return riv;
        }

        /// <summary>Ширина бирки по формуле мода: max(96, текст капсом 10.5 px + разрядка + отступы 36).</summary>
        public static float TagWidth(string verb, string value)
        {
            string s = (verb ?? string.Empty) + (string.IsNullOrEmpty(value) ? string.Empty : " · " + value);
            string up = s.ToUpperInvariant();
            return Mathf.Max(96f, TextWidth(up, 10.5f, true, 1.47f) + 36f);
        }
    }
}
