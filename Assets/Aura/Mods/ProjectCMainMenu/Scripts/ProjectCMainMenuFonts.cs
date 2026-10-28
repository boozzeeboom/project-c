// ProjectC Main Menu — the two faces the menu is set in.
//
// The mod asks for "Inter", "Segoe UI", "Helvetica Neue", Arial, sans-serif
// (src/palette.js FONT / FONT_DISPLAY) — system faces a player build cannot rely on.
// The project's own faces are referenced from this asset instead: it lives in a
// Resources folder and holds the two Font assets, which keeps them alive in a player
// build without a scene or prefab edit.
//
//   Body    — every label in the menu;
//   Display — the PROJECT / C wordmark only.
//
// UI Toolkit draws a plain Font through a signed-distance-field atlas it creates on the
// fly with 9 px of padding at 90 pt. A text-shadow can never blur further than that
// padding: at 13-15 px type it is ~1.4 px, so every soft halo of the stylesheet collapsed
// into a tight dark contour around the glyphs. BodyAsset / DisplayAsset build the atlas
// with more room (AtlasPadding at the same sampling size), so the shadows stay soft.
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace ProjectCMainMenu
{
    [CreateAssetMenu(menuName = "ProjectC/Main Menu Fonts", fileName = "PCMM_Fonts")]
    public sealed class ProjectCMainMenuFonts : ScriptableObject
    {
        [Tooltip("Every label of the menu.")]
        public Font Body;

        [Tooltip("The PROJECT / C wordmark only.")]
        public Font Display;

        private const string ResourcePath = "PCMM_Fonts";
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 24;
        private const int AtlasSize = 1024;

        private static ProjectCMainMenuFonts _cached;

        [System.NonSerialized] private FontAsset _bodyAsset;
        [System.NonSerialized] private FontAsset _displayAsset;

        /// <summary>The menu's faces, or null when the asset has not been created.</summary>
        public static ProjectCMainMenuFonts Load()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<ProjectCMainMenuFonts>(ResourcePath);
            return _cached;
        }

        /// <summary>The body face as a dynamic SDF atlas with soft-shadow padding (null if unavailable).</summary>
        public FontAsset BodyAsset => Alive(_bodyAsset) ? _bodyAsset : _bodyAsset = Build(Body);

        /// <summary>The display face as a dynamic SDF atlas with soft-shadow padding (null if unavailable).</summary>
        public FontAsset DisplayAsset => Alive(_displayAsset) ? _displayAsset : _displayAsset = Build(Display);

        /// <summary>
        /// The atlas is a runtime object cached on this asset, and this asset outlives a Play
        /// session when domain reload is off. Unity tears the atlas' material and textures
        /// down when Play stops, while the FontAsset itself survives; handing that husk to the
        /// next panel threw MissingReferenceException inside the repaint (TextUtilities
        /// .GetTextCoreSettingsForElement -> Material.mainTexture), which aborted the whole
        /// panel and left the second Play session without a menu. So the cache is only reused
        /// when every part of it is still there.
        /// </summary>
        private static bool Alive(FontAsset asset)
        {
            if (asset == null || asset.material == null) return false;

            var textures = asset.atlasTextures;
            if (textures == null || textures.Length == 0) return false;
            for (var i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null) return false;
            }

            return true;
        }

        private static FontAsset Build(Font font)
        {
            if (font == null) return null;

            FontAsset asset = null;
            try
            {
                asset = FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding,
                    GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PCMM] font atlas for '" + font.name + "' could not be built: " + e.Message);
            }

            if (asset == null) return null;

            asset.name = "PCMM " + font.name + " SDF";
            asset.hideFlags = HideFlags.DontSave;
            if (asset.material != null) asset.material.hideFlags = HideFlags.DontSave;
            var textures = asset.atlasTextures;
            if (textures != null)
            {
                for (var i = 0; i < textures.Length; i++)
                {
                    if (textures[i] != null) textures[i].hideFlags = HideFlags.DontSave;
                }
            }

            return asset;
        }
    }
}
