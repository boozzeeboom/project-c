// ProjectC Main Menu — the two typefaces, applied to the tree.
//
// PCMM_Menu.uss cannot set a font: USS has no font-family, and a TextElement inherits
// whatever the panel gives it. ProjectCMainMenuFonts carries the faces as an asset, and
// this file hands them to the elements once, when the tree is mounted:
//
//   * the body face goes on .pc-ui, so every label under it inherits it — including the
//     ghost labels ProjectCMainMenuTextBlower creates later, which live in the same
//     subtree;
//   * the display face goes on the two wordmark runs by name, because that is the only
//     place the interface changes typeface.
//
// Both are handed over as SDF font assets with a wide atlas padding (see
// ProjectCMainMenuFonts), falling back to the plain Font when the atlas cannot be built.
using UnityEngine;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace ProjectCMainMenu
{
    public sealed partial class ProjectCMainMenuWindow
    {
        private void ApplyFonts()
        {
            var fonts = ProjectCMainMenuFonts.Load();
            if (fonts == null || Ui == null) return;

            var body = ToDefinition(fonts.BodyAsset, fonts.Body);
            if (body.HasValue) Ui.style.unityFontDefinition = new StyleFontDefinition(body.Value);

            var display = ToDefinition(fonts.DisplayAsset, fonts.Display);
            if (!display.HasValue || Wordmark == null) return;

            Wordmark.style.unityFontDefinition = new StyleFontDefinition(display.Value);

            var hierarchy = Wordmark.hierarchy;
            for (var i = 0; i < hierarchy.childCount; i++)
            {
                if (hierarchy[i] is UnityEngine.UIElements.TextElement text) text.style.unityFontDefinition = new StyleFontDefinition(display.Value);
            }
        }

        private static FontDefinition? ToDefinition(FontAsset sdf, Font font)
        {
            if (sdf != null) return FontDefinition.FromSDFFont(sdf);
            return font == null ? (FontDefinition?)null : FontDefinition.FromFont(font);
        }
    }
}
