// ProjectC Main Menu — gamma-space blending, emulated in a linear-space project.
//
// The web mod is gamma-space from end to end: three.js ShaderMaterials write the raw
// sRGB values of src/palette.js, and the browser blends every translucent CSS layer in
// sRGB. This project renders in Linear colour space, where the GPU blends in linear
// light. The same alpha then means something else:
//
//   * a pale layer over a dark one becomes far stronger — #e9f2ec at 0.26 over black is
//     0.26 on screen in the browser and ~0.55 here (the "hard white spot");
//   * a dark layer over a bright one becomes far weaker — #070a09 at 0.6 over the day
//     sky is 0.37 in the browser and ~0.62 here (the "overexposed" background).
//
// Alpha() returns the alpha that, blended in linear light, lands on the value the
// browser would have produced by blending in sRGB. It is exact for the backdrop it is
// given and degrades gently for others; every caller passes the backdrop the layer
// actually sits on (the sky, the menu scrim, the card mist). PCMM_Gamma.hlsl carries the
// same formula for the atmosphere shaders.
using UnityEngine;

namespace ProjectCMainMenu
{
    public static class ProjectCMainMenuGamma
    {
        // ---- the backdrops the interface layers sit on (sRGB luminance) ---------
        /// <summary>The day sky behind the left scrim and the card bank.</summary>
        public const float Sky = 0.80f;

        /// <summary>The menu column: the sky seen through the charcoal scrim.</summary>
        public const float Scrim = 0.42f;

        /// <summary>A charcoal cloud body — what a pale rim sits on.</summary>
        public const float Cloud = 0.30f;

        /// <summary>The dark mist inside the settings card — what every control sits on.</summary>
        public const float Mist = 0.07f;

        /// <summary>True when blending happens in linear light and needs the correction.</summary>
        public static bool IsLinear => QualitySettings.activeColorSpace == ColorSpace.Linear;

        public static float ToLinear(float c)
        {
            c = Mathf.Clamp01(c);
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>Relative luminance of an sRGB colour, kept in sRGB (perceptual) units.</summary>
        public static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// The alpha that reproduces, in linear blending, a gamma-space blend of a layer
        /// of luminance <paramref name="tint"/> at <paramref name="alpha"/> over
        /// <paramref name="backdrop"/> (all sRGB units).
        /// </summary>
        public static float Alpha(float alpha, float tint, float backdrop)
        {
            alpha = Mathf.Clamp01(alpha);
            if (!IsLinear) return alpha;

            var lb = ToLinear(backdrop);
            var lc = ToLinear(tint);
            var d = lc - lb;
            if (Mathf.Abs(d) < 1e-3f) return alpha;

            var g = Mathf.Lerp(backdrop, tint, alpha);
            return Mathf.Clamp01((ToLinear(g) - lb) / d);
        }

        /// <summary>
        /// Alpha() for a texture that is drawn at an element opacity: the texel is baked so
        /// that texel × opacity lands on the corrected value at the authored opacity.
        /// </summary>
        public static float Alpha(float alpha, float tint, float backdrop, float elementOpacity)
        {
            var op = Mathf.Clamp(elementOpacity, 0.01f, 1f);
            return Mathf.Clamp01(Alpha(alpha * op, tint, backdrop) / op);
        }
    }
}
