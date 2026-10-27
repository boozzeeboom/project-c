// ProjectC Main Menu — the procedural gradient ramps the ported interface needs.
//
// UI Toolkit has neither linear-gradient() nor radial-gradient(), and the port left
// the replacement as a TODO comment on every rule that used one
// (docs/UI/MainMenu/02_smoke_menu_port.md:246-248 records that the generator was
// deferred to "шаг 7" and was never written). The visible result was a menu whose
// buttons, settings card, hairlines and tab glow drew nothing at all — a flat sheet
// of text over the atmosphere.
//
// This file bakes the same gradients into small Texture2D sheets that
// ProjectCMainMenuWindow hands to style.backgroundImage, following exactly the same
// policy as ProjectCMainMenuSmokeTextures: one shared cache, HideFlags.DontSave, and
// deterministic output so two builds never look different.
//
// Geometry strings are the CSS ones, read literally:
//   radial-gradient(<radiusX> <radiusY> at <centerX> <centerY>, tint, transparent <stop>)
// Every stop is a fraction of that radius, exactly like CSS.
using System.Collections.Generic;
using UnityEngine;

namespace ProjectCMainMenu
{
    public static class ProjectCMainMenuRamps
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        // ---- the ui.css values these ramps reproduce -----------------------
        // --pc-cloud-rim / --pc-hair / --pc-ink are all the pale paper tone; the
        // interface sits on a dark atmosphere, so the light end of every ramp has to
        // stay pale or it simply disappears into the veil.
        private static readonly Color MistPale = Hex("#e9f2ec");
        private static readonly Color InkPale = ProjectCMainMenuPalette.Ink;        // #eef3ef
        private static readonly Color VeilDark = ProjectCMainMenuPalette.VeilDark;  // #070a09
        private static readonly Color AccentGas = ProjectCMainMenuPalette.Accent;   // #78b891
        private static readonly Color RowGas = Hex("#16412c");
        private static readonly Color GlassDeep = Hex("#121711");

        private static int _serial;

        // ------------------------------------------------------------ buttons

        /// <summary>
        /// radial-gradient(58% 122% at 46% 53%, --pc-cloud-base, transparent 76%),
        /// except the base tone is the pale one: the CSS cloud was charcoal because
        /// the web menu sat on a bright sky, while this menu sits on the veiled
        /// atmosphere, where a charcoal cloud is invisible. The pale wash is the
        /// light background the buttons were missing; the charcoal smoke sheets stay
        /// on top of it, so the button still reads on a bright day phase.
        /// </summary>
        public static Texture2D ButtonHalo() =>
            Elliptical("btn-halo", MistPale, 0.26f, 0.58f, 1.22f, 0.46f, 0.53f, 0.76f);

        /// <summary>radial-gradient(50% 50% at 50% 50%, --pc-cloud-rim, transparent 72%).</summary>
        public static Texture2D ButtonRim() =>
            Elliptical("btn-rim", InkPale, 0.30f, 0.50f, 0.50f, 0.50f, 0.50f, 0.72f);

        // --------------------------------------------------------------- card

        /// <summary>radial-gradient(50% 50%, --pc-cloud-rim, transparent 74%).</summary>
        public static Texture2D CardRim() =>
            Elliptical("card-rim", InkPale, 0.26f, 0.50f, 0.50f, 0.50f, 0.50f, 0.74f);

        /// <summary>
        /// The three stacked gradients of .pc-card__mist (--pc-glass-deep, --pc-scrim,
        /// --pc-glass-2) collapsed into one radial: the densest is kept and the falloff
        /// is stretched past the box so no edge of the card is ever a straight line.
        /// </summary>
        public static Texture2D CardMist() =>
            Elliptical("card-mist", GlassDeep, 0.74f, 0.62f, 0.62f, 0.50f, 0.50f, 1.15f);

        /// <summary>radial-gradient(58% 58% at 50% 50%, --pc-hair, transparent 78%).</summary>
        public static Texture2D CloseHalo() =>
            Elliptical("close-halo", InkPale, 0.17f, 0.58f, 0.58f, 0.50f, 0.50f, 0.78f);

        /// <summary>
        /// The soft smudge under the active tab: radial-gradient(50% 100% at 50% 50%,
        /// --pc-accent, transparent 72%). The peak alpha is deliberately low — the
        /// element already runs at half opacity, and anything brighter reads as the
        /// hard bar this replaces rather than as gas-green smoke.
        /// </summary>
        public static Texture2D TabGlow() =>
            Elliptical("tab-glow", AccentGas, 0.34f, 0.50f, 1.00f, 0.50f, 0.50f, 0.80f);

        // ------------------------------------------------------------ chrome

        /// <summary>
        /// linear-gradient(to right, --pc-scrim, --pc-scrim-mid 42%, --pc-scrim-fade
        /// 84%) — the dark bank the wordmark and the buttons sit on.
        /// </summary>
        public static Texture2D MainScrim() =>
            Horizontal("main-scrim",
                new[]
                {
                    WithAlpha(VeilDark, 0.66f), WithAlpha(VeilDark, 0.34f),
                    WithAlpha(VeilDark, 0f), WithAlpha(VeilDark, 0f),
                },
                new[] { 0f, 0.42f, 0.84f, 1f });

        /// <summary>linear-gradient(90deg, --pc-hair-strong, transparent) — the rule under the wordmark.</summary>
        public static Texture2D TitleRule() =>
            Horizontal("title-rule",
                new[] { WithAlpha(AccentGas, 0.60f), WithAlpha(AccentGas, 0f) },
                new[] { 0f, 1f });

        /// <summary>
        /// linear-gradient(90deg, transparent, --pc-hair 24%, --pc-hair 76%,
        /// transparent) — the divider that is dense in the middle and gone at the ends.
        /// </summary>
        public static Texture2D Hairline() =>
            Horizontal("hairline",
                new[]
                {
                    WithAlpha(InkPale, 0f), WithAlpha(InkPale, 0.16f),
                    WithAlpha(InkPale, 0.16f), WithAlpha(InkPale, 0f),
                },
                new[] { 0f, 0.24f, 0.76f, 1f });

        /// <summary>linear-gradient(90deg, --pc-row 0%, transparent 92%) — the row plate.</summary>
        public static Texture2D RowWash() =>
            Horizontal("row-wash",
                new[] { WithAlpha(RowGas, 0.20f), WithAlpha(RowGas, 0.20f), WithAlpha(RowGas, 0f) },
                new[] { 0f, 0.08f, 0.92f });

        /// <summary>linear-gradient(90deg, transparent, --pc-track 10%, --pc-track 90%, transparent).</summary>
        public static Texture2D RangeTrack() =>
            Horizontal("range-track",
                new[]
                {
                    WithAlpha(InkPale, 0f), WithAlpha(InkPale, 0.16f),
                    WithAlpha(InkPale, 0.16f), WithAlpha(InkPale, 0f),
                },
                new[] { 0f, 0.10f, 0.90f, 1f });

        /// <summary>
        /// radial-gradient(120% 160% at 30% 40%, --pc-track-2, transparent 86%), with
        /// the track tone inverted to the pale one for the same reason as the button
        /// halo: a charcoal pill is invisible on the veiled atmosphere, which is what
        /// made the switches, selects and key caps look like bare text.
        /// </summary>
        public static Texture2D SoftPill(float alpha) =>
            Elliptical("pill", InkPale, alpha, 1.10f, 1.60f, 0.30f, 0.40f, 0.92f);

        // ------------------------------------------------------------- engine

        /// <summary>radial-gradient(rx ry at cx cy, tint, transparent stop) as an RGBA32 sheet.</summary>
        private static Texture2D Elliptical(string key, Color tint, float alpha,
            float radiusX, float radiusY, float centerX, float centerY, float stop, int size = 128)
        {
            var cacheKey = string.Concat("ell|", key,
                "|", tint.r.ToString("F4"), ",", tint.g.ToString("F4"), ",", tint.b.ToString("F4"),
                "|", alpha.ToString("F4"),
                "|", radiusX.ToString("F4"), ",", radiusY.ToString("F4"),
                "|", centerX.ToString("F4"), ",", centerY.ToString("F4"),
                "|", stop.ToString("F4"), "|", size.ToString());

            if (Cache.TryGetValue(cacheKey, out var hit) && hit != null) return hit;

            var pixels = new Color32[size * size];
            var rgb = ToColor32(tint, 1f);
            var peak = Mathf.Clamp01(alpha);
            var invRx = 1f / Mathf.Max(1e-4f, radiusX);
            var invRy = 1f / Mathf.Max(1e-4f, radiusY);
            var edge = Mathf.Max(1e-4f, stop);

            var o = 0;
            for (var y = 0; y < size; y++)
            {
                var dy = ((y + 0.5f) / size - centerY) * invRy;
                var dy2 = dy * dy;

                for (var x = 0; x < size; x++)
                {
                    var dx = ((x + 0.5f) / size - centerX) * invRx;
                    var r = Mathf.Sqrt(dx * dx + dy2);

                    // smoothstep instead of the CSS straight ramp: the sheet is softer
                    // than a linear gradient on its own, so the result does not depend
                    // on the static filter: blur() being cheap on this renderer.
                    var falloff = 1f - ProjectCMainMenuPalette.SmoothStep(0f, edge, r);
                    var a = (byte)Mathf.RoundToInt(Mathf.Clamp01(falloff * peak) * 255f);
                    pixels[o++] = new Color32(rgb.r, rgb.g, rgb.b, a);
                }
            }

            return Store(cacheKey, pixels, size, size);
        }

        /// <summary>linear-gradient(to right, …) over an explicit stop table.</summary>
        private static Texture2D Horizontal(string key, Color[] colors, float[] stops,
            int width = 128, int height = 8)
        {
            var cacheKey = string.Concat("lin|", key, "|", width.ToString(), "x", height.ToString());
            for (var i = 0; i < colors.Length; i++)
            {
                cacheKey = string.Concat(cacheKey, "|",
                    colors[i].r.ToString("F3"), ",", colors[i].g.ToString("F3"), ",",
                    colors[i].b.ToString("F3"), ",", colors[i].a.ToString("F3"));
            }

            for (var i = 0; i < stops.Length; i++)
            {
                cacheKey = string.Concat(cacheKey, "|", stops[i].ToString("F3"));
            }

            if (Cache.TryGetValue(cacheKey, out var hit) && hit != null) return hit;

            var pixels = new Color32[width * height];
            for (var x = 0; x < width; x++)
            {
                var sample = ToColor32(SampleStops(colors, stops, (x + 0.5f) / width), 1f);
                for (var y = 0; y < height; y++) pixels[y * width + x] = sample;
            }

            return Store(cacheKey, pixels, width, height);
        }

        private static Color SampleStops(Color[] colors, float[] stops, float u)
        {
            if (u <= stops[0]) return colors[0];

            for (var i = 1; i < stops.Length; i++)
            {
                if (u > stops[i]) continue;
                var span = Mathf.Max(1e-6f, stops[i] - stops[i - 1]);
                return Color.Lerp(colors[i - 1], colors[i], (u - stops[i - 1]) / span);
            }

            return colors[colors.Length - 1];
        }

        private static Texture2D Store(string key, Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PCMM_Ramp_" + (++_serial).ToString("D3"),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Cache[key] = texture;
            return texture;
        }

        private static Color32 ToColor32(Color color, float alpha)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
        }

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static Color Hex(string hex) => ProjectCMainMenuPalette.Hex(hex);

        /// <summary>Drops every baked sheet; the window calls this when the menu is torn down.</summary>
        public static void DisposeAll()
        {
            foreach (var pair in Cache)
            {
                if (pair.Value != null) Object.Destroy(pair.Value);
            }

            Cache.Clear();
        }
    }
}
