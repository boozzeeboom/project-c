// ProjectC Main Menu — the procedural gradient ramps the ported interface needs.
//
// UI Toolkit has neither linear-gradient() nor radial-gradient(), so every rule of
// ui.css that used one is baked here into a small Texture2D sheet that
// ProjectCMainMenuWindow hands to style.backgroundImage (one shared cache,
// HideFlags.DontSave, deterministic output).
//
// Geometry strings are the CSS ones, read literally:
//   radial-gradient(<radiusX> <radiusY> at <centerX> <centerY>, tint, transparent <stop>)
// Every stop is a fraction of that radius, exactly like CSS. Tints are the mod's own
// tokens (src/ui.js applyPalette) — charcoal where the mod is charcoal, pale where it is
// pale. An earlier pass inverted the charcoal clouds and pills to pale ones; that, plus
// linear-space blending, is what turned the soft banks into hard white spots.
//
// Every texel's alpha goes through ProjectCMainMenuGamma.Alpha with the backdrop the
// layer really sits on and the opacity the element is authored at, so that the linear
// blend this project performs lands where the browser's sRGB blend did.
using System.Collections.Generic;
using UnityEngine;

namespace ProjectCMainMenu
{
    public static class ProjectCMainMenuRamps
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        // ---- the ui.js tokens these ramps reproduce --------------------------
        private static readonly Color VeilDark = ProjectCMainMenuPalette.VeilDark;     // --pc-scrim / --pc-track-2 / --pc-cloud-base
        private static readonly Color VeilMid = ProjectCMainMenuPalette.VeilMid;       // --pc-glass-deep
        private static readonly Color SkyHorizon = ProjectCMainMenuPalette.SkyHorizon; // --pc-cloud-rim
        private static readonly Color Ink = ProjectCMainMenuPalette.Ink;               // --pc-hair / --pc-track
        private static readonly Color Accent = ProjectCMainMenuPalette.Accent;         // --pc-accent / --pc-accent-soft
        private static readonly Color GasMid = ProjectCMainMenuPalette.GasMid;         // --pc-row
        private static readonly Color GasAccent = ProjectCMainMenuPalette.GasAccent;   // --pc-glow

        private const float Sky = ProjectCMainMenuGamma.Sky;
        private const float Scrim = ProjectCMainMenuGamma.Scrim;
        private const float CloudBody = ProjectCMainMenuGamma.Cloud;
        private const float Mist = ProjectCMainMenuGamma.Mist;

        private static int _serial;

        // ------------------------------------------------------------ buttons

        /// <summary>
        /// .pc-btn-cloud — radial-gradient(58% 122% at 46% 53%, --pc-cloud-base, transparent
        /// 76%); --pc-cloud-base is --pc-veil-dark at 0.34. The element runs at 0.88.
        /// </summary>
        public static Texture2D ButtonHalo() =>
            Elliptical("btn-halo", VeilDark, 0.34f, 0.58f, 1.22f, 0.46f, 0.53f, 0.76f, Scrim, 0.88f);

        /// <summary>
        /// .pc-btn-cloud::after — radial-gradient(50% 50%, --pc-cloud-rim, transparent 72%);
        /// --pc-cloud-rim is --pc-sky-horizon at 0.24, drawn at 0.55 inside the 0.88 cloud.
        /// </summary>
        public static Texture2D ButtonRim() =>
            Elliptical("btn-rim", SkyHorizon, 0.24f, 0.50f, 0.50f, 0.50f, 0.50f, 0.72f, CloudBody, 0.88f * 0.55f);

        // --------------------------------------------------------------- card

        /// <summary>.pc-card-cloud::after — the same pale rim, transparent 74%, at 0.5 inside the 0.93 bank.</summary>
        public static Texture2D CardRim() =>
            Elliptical("card-rim", SkyHorizon, 0.24f, 0.50f, 0.50f, 0.50f, 0.50f, 0.74f, CloudBody, 0.93f * 0.5f);

        /// <summary>
        /// .pc-card::before — three stacked radial-gradients:
        ///   radial-gradient(68% 66% at 42% 38%, --pc-glass-deep, transparent 80%),
        ///   radial-gradient(78% 74% at 62% 70%, --pc-scrim, transparent 84%),
        ///   radial-gradient(112% 112% at 50% 50%, --pc-glass-2, transparent 88%)
        /// inside a ~50% border-radius, softened by filter: blur(18px).
        ///
        /// The three are composited into one sheet. The rounded box and its blur are
        /// replaced by an elliptical fade baked into the sheet: a border-radius in UI Toolkit
        /// clips the background with a crisp edge, which is what drew the hard ellipse
        /// around the settings card.
        /// </summary>
        public static Texture2D CardMist() =>
            Stacked("card-mist",
                new[]
                {
                    // listed top-first, exactly like a CSS background shorthand
                    new RadialLayer(VeilMid, 0.74f, 0.68f, 0.66f, 0.42f, 0.38f, 0.80f),
                    new RadialLayer(VeilDark, 0.66f, 0.78f, 0.74f, 0.62f, 0.70f, 0.84f),
                    new RadialLayer(VeilDark, 0.62f, 1.12f, 1.12f, 0.50f, 0.50f, 0.88f),
                },
                Sky, 1f);

        /// <summary>
        /// .pc-close — radial-gradient(58% 58%, --pc-hair, transparent 78%). The pale ring is
        /// kept, but a charcoal base is composited under it: the cross is two 1.5 px bars of
        /// --pc-ink, and the card's top corner sits outside the mist's dense ellipse, so at day
        /// both bars land on the open sky and the pale ring gave them nothing to read against.
        /// The base reads 0.375 sRGB over the 0.80 sky and 0.242 over the 0.30 night mist, which
        /// is 5.63:1 and 9.58:1 for the bars — the same "small dark cloud" the buttons are made of.
        /// </summary>
        public static Texture2D CloseHalo() =>
            Stacked("close-halo",
                new[]
                {
                    new RadialLayer(Ink, 0.16f, 0.58f, 0.58f, 0.50f, 0.50f, 0.78f),
                    new RadialLayer(VeilDark, 0.70f, 0.62f, 0.62f, 0.50f, 0.50f, 0.86f),
                },
                Sky, 1f, 128, 0.86f);

        /// <summary>.pc-close:hover — radial-gradient(64% 64%, --pc-accent-soft, transparent
        /// 80%) over the same charcoal base, so the accent cross stays readable on the sky.</summary>
        public static Texture2D CloseHaloHover() =>
            Stacked("close-halo-hover",
                new[]
                {
                    new RadialLayer(Accent, 0.16f, 0.64f, 0.64f, 0.50f, 0.50f, 0.80f),
                    new RadialLayer(VeilDark, 0.70f, 0.62f, 0.62f, 0.50f, 0.50f, 0.86f),
                },
                Sky, 1f, 128, 0.86f);

        /// <summary>
        /// .pc-tab::after — radial-gradient(50% 100% at 50% 50%, --pc-accent, transparent 72%),
        /// shown at 0.5 on the active tab. Full accent, as in the mod: the gamma correction
        /// is what keeps it a smudge instead of a bar.
        /// </summary>
        public static Texture2D TabGlow() =>
            Elliptical("tab-glow", Accent, 1f, 0.50f, 1.00f, 0.50f, 0.50f, 0.72f, Mist, 0.5f);

        // ------------------------------------------------------------ chrome

        /// <summary>
        /// .pc-main::before — linear-gradient(to right, --pc-scrim, --pc-scrim-mid 42%,
        /// --pc-scrim-fade 84%): the dark bank the wordmark and the buttons sit on.
        /// </summary>
        public static Texture2D MainScrim() =>
            Horizontal("main-scrim",
                new[]
                {
                    WithAlpha(VeilDark, 0.66f), WithAlpha(VeilDark, 0.34f),
                    WithAlpha(VeilDark, 0f), WithAlpha(VeilDark, 0f),
                },
                new[] { 0f, 0.42f, 0.84f, 1f }, Sky, 1f);

        /// <summary>.pc-rule — linear-gradient(90deg, --pc-hair-strong, transparent).</summary>
        public static Texture2D TitleRule() =>
            Horizontal("title-rule",
                new[] { WithAlpha(Accent, 0.60f), WithAlpha(Accent, 0f) },
                new[] { 0f, 1f }, Scrim, 1f);

        /// <summary>
        /// .pc-card-head::after / .pc-tabs::after — linear-gradient(90deg, transparent,
        /// --pc-hair 24%, --pc-hair 76%, transparent), drawn at ~0.5.
        /// </summary>
        public static Texture2D Hairline() =>
            Horizontal("hairline",
                new[]
                {
                    WithAlpha(Ink, 0f), WithAlpha(Ink, 0.16f),
                    WithAlpha(Ink, 0.16f), WithAlpha(Ink, 0f),
                },
                new[] { 0f, 0.24f, 0.76f, 1f }, Mist, 0.5f);

        /// <summary>
        /// .pc-row — linear-gradient(90deg, --pc-row 0%, transparent 92%). The top and bottom
        /// edges get a short feather so stacked rows read as smoke, not as cut plates.
        /// </summary>
        public static Texture2D RowWash() =>
            Horizontal("row-wash",
                new[] { WithAlpha(GasMid, 0.20f), WithAlpha(GasMid, 0f) },
                new[] { 0f, 0.92f }, Mist, 1f, 128, 16, 0.22f);

        /// <summary>.pc-row::before (hover) — radial-gradient(60% 82% at 0% 50%, --pc-row, transparent 74%).</summary>
        public static Texture2D RowHover() =>
            Elliptical("row-hover", GasMid, 0.20f, 0.60f, 0.82f, 0f, 0.50f, 0.74f, Mist, 1f);

        // ---------------------------------------------------------- controls

        /// <summary>::-webkit-slider-runnable-track — linear-gradient(90deg, transparent, --pc-track 10%, --pc-track 90%, transparent).</summary>
        public static Texture2D RangeTrack() =>
            Horizontal("range-track",
                new[]
                {
                    WithAlpha(Ink, 0f), WithAlpha(Ink, 0.16f),
                    WithAlpha(Ink, 0.16f), WithAlpha(Ink, 0f),
                },
                new[] { 0f, 0.10f, 0.90f, 1f }, Mist, 1f);

        /// <summary>::-webkit-slider-thumb — radial-gradient(58% 58% at 42% 38%, --pc-ink 0%, --pc-accent 78%).</summary>
        public static Texture2D RangeThumb() =>
            RadialBlend("range-thumb", Ink, Accent, 0.58f, 0.58f, 0.42f, 0.38f, 0.78f);

        /// <summary>
        /// box-shadow: 0 0 16px --pc-glow, as a halo element 10px larger than the 12px thumb
        /// on every side; --pc-glow is --pc-gas-accent at 0.4.
        /// </summary>
        public static Texture2D ThumbGlow() =>
            Elliptical("thumb-glow", GasAccent, 0.40f, 0.50f, 0.50f, 0.50f, 0.50f, 1.00f, Mist, 1f);

        /// <summary>.pc-switch — radial-gradient(120% 160% at 30% 40%, --pc-track-2, transparent 86%).</summary>
        public static Texture2D SwitchOff() =>
            Elliptical("switch-off", VeilDark, 0.55f, 1.20f, 1.60f, 0.30f, 0.40f, 0.86f, Mist, 1f);

        /// <summary>.pc-switch[aria-checked=true] — radial-gradient(120% 160% at 34% 40%, --pc-accent-soft, transparent 88%).</summary>
        public static Texture2D SwitchOn() =>
            Elliptical("switch-on", Accent, 0.16f, 1.20f, 1.60f, 0.34f, 0.40f, 0.88f, Mist, 1f);

        /// <summary>box-shadow: 0 0 18px --pc-glow around the switch (an element 12px larger on every side).</summary>
        public static Texture2D SwitchGlow() =>
            Elliptical("switch-glow", GasAccent, 0.40f, 0.50f, 0.50f, 0.50f, 0.50f, 1.00f, Mist, 1f);

        /// <summary>.pc-select — radial-gradient(120% 150% at 26% 40%, --pc-track-2, transparent 88%).</summary>
        public static Texture2D SelectIdle() =>
            Elliptical("select-idle", VeilDark, 0.55f, 1.20f, 1.50f, 0.26f, 0.40f, 0.88f, Mist, 1f);

        /// <summary>.pc-select:hover — radial-gradient(120% 150% at 26% 40%, --pc-accent-soft, transparent 90%).</summary>
        public static Texture2D SelectHover() =>
            Elliptical("select-hover", Accent, 0.16f, 1.20f, 1.50f, 0.26f, 0.40f, 0.90f, Mist, 1f);

        /// <summary>box-shadow: 0 0 16px --pc-glow around the select on hover.</summary>
        public static Texture2D SelectGlow() =>
            Elliptical("select-glow", GasAccent, 0.40f, 0.50f, 0.50f, 0.50f, 0.50f, 1.00f, Mist, 1f);

        /// <summary>.pc-kbd — radial-gradient(120% 160% at 40% 40%, --pc-track-2, transparent 90%).</summary>
        public static Texture2D KeyCap() =>
            Elliptical("kbd", VeilDark, 0.55f, 1.20f, 1.60f, 0.40f, 0.40f, 0.90f, Mist, 1f);

        // ------------------------------------------------------------- engine

        /// <summary>radial-gradient(rx ry at cx cy, tint, transparent stop) as an RGBA32 sheet.</summary>
        private static Texture2D Elliptical(string key, Color tint, float alpha,
            float radiusX, float radiusY, float centerX, float centerY, float stop,
            float backdrop, float opacity, int size = 128)
        {
            var cacheKey = Key("ell", key, tint, alpha, radiusX, radiusY, centerX, centerY, stop, backdrop, opacity, size);
            if (Cache.TryGetValue(cacheKey, out var hit) && hit != null) return hit;

            var pixels = new Color32[size * size];
            var rgb = ToColor32(tint, 1f);
            var luma = ProjectCMainMenuGamma.Luma(tint);
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

                    // smoothstep instead of the CSS straight ramp: it stands in for the
                    // filter: blur() that softens every one of these boxes in the mod
                    var a = peak * (1f - ProjectCMainMenuPalette.SmoothStep(0f, edge, r));
                    a = ProjectCMainMenuGamma.Alpha(a, luma, backdrop, opacity);
                    pixels[o++] = new Color32(rgb.r, rgb.g, rgb.b, ToByte(a));
                }
            }

            return Store(cacheKey, pixels, size, size);
        }

        /// <summary>
        /// radial-gradient(rx ry at cx cy, inner 0%, outer stop) — an opaque two-colour
        /// radial (the slider thumb). The element's border-radius gives it its outline.
        /// </summary>
        private static Texture2D RadialBlend(string key, Color inner, Color outer,
            float radiusX, float radiusY, float centerX, float centerY, float stop, int size = 32)
        {
            var cacheKey = Key("blend", key, inner, outer.r + outer.g * 10f + outer.b * 100f,
                radiusX, radiusY, centerX, centerY, stop, 0f, 1f, size);
            if (Cache.TryGetValue(cacheKey, out var hit) && hit != null) return hit;

            var pixels = new Color32[size * size];
            var o = 0;
            for (var y = 0; y < size; y++)
            {
                var dy = ((y + 0.5f) / size - centerY) / Mathf.Max(1e-4f, radiusY);
                for (var x = 0; x < size; x++)
                {
                    var dx = ((x + 0.5f) / size - centerX) / Mathf.Max(1e-4f, radiusX);
                    var t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(1e-4f, stop));
                    pixels[o++] = ToColor32(Color.Lerp(inner, outer, t), 1f);
                }
            }

            return Store(cacheKey, pixels, size, size);
        }

        /// <summary>linear-gradient(to right, …) over an explicit stop table.</summary>
        private static Texture2D Horizontal(string key, Color[] colors, float[] stops,
            float backdrop, float opacity, int width = 128, int height = 8, float featherY = 0f)
        {
            var cacheKey = string.Concat("lin|", key, "|", width.ToString(), "x", height.ToString(),
                "|", F(backdrop), "|", F(opacity), "|", F(featherY));
            for (var i = 0; i < colors.Length; i++) cacheKey = string.Concat(cacheKey, "|", F(colors[i]));
            for (var i = 0; i < stops.Length; i++) cacheKey = string.Concat(cacheKey, "|", F(stops[i]));

            if (Cache.TryGetValue(cacheKey, out var hit) && hit != null) return hit;

            var pixels = new Color32[width * height];
            for (var x = 0; x < width; x++)
            {
                var color = SampleStops(colors, stops, (x + 0.5f) / width);
                var luma = ProjectCMainMenuGamma.Luma(color);

                for (var y = 0; y < height; y++)
                {
                    var a = color.a;
                    if (featherY > 0f)
                    {
                        var v = (y + 0.5f) / height;
                        a *= ProjectCMainMenuPalette.SmoothStep(0f, featherY, Mathf.Min(v, 1f - v));
                    }

                    // the stop table carries the alpha, so it has to survive into the texel —
                    // forcing it to 1 painted every horizontal ramp opaque
                    a = ProjectCMainMenuGamma.Alpha(a, luma, backdrop, opacity);
                    pixels[y * width + x] = ToColor32(color, a);
                }
            }

            return Store(cacheKey, pixels, width, height);
        }

        /// <summary>One radial-gradient layer of a stacked CSS background (top-first order).</summary>
        private readonly struct RadialLayer
        {
            public RadialLayer(Color tint, float alpha, float radiusX, float radiusY,
                float centerX, float centerY, float stop)
            {
                Tint = tint;
                Alpha = alpha;
                RadiusX = radiusX;
                RadiusY = radiusY;
                CenterX = centerX;
                CenterY = centerY;
                Stop = stop;
            }

            public readonly Color Tint;
            public readonly float Alpha;
            public readonly float RadiusX;
            public readonly float RadiusY;
            public readonly float CenterX;
            public readonly float CenterY;
            public readonly float Stop;
        }

        /// <summary>
        /// A CSS <c>background:</c> list of radial-gradients composited into one sheet, in
        /// the box's inscribed ellipse with a soft rim (the mod's ~50% border-radius plus
        /// its blur). The first layer is the topmost, exactly like the shorthand.
        /// </summary>
        private static Texture2D Stacked(string key, RadialLayer[] layers, float backdrop, float opacity,
            int size = 256, float rimStart = 0.62f)
        {
            var cacheKey = string.Concat("stack|", key, "|", size.ToString(), "|", F(rimStart),
                "|", F(backdrop), "|", F(opacity));
            for (var i = 0; i < layers.Length; i++)
            {
                var l = layers[i];
                cacheKey = string.Concat(cacheKey, "|", F(l.Tint), "|", F(l.Alpha),
                    "|", F(l.RadiusX), ",", F(l.RadiusY), "|", F(l.CenterX), ",", F(l.CenterY), "|", F(l.Stop));
            }

            if (Cache.TryGetValue(cacheKey, out var hit) && hit != null) return hit;

            var pixels = new Color32[size * size];
            var o = 0;
            for (var y = 0; y < size; y++)
            {
                var v = (y + 0.5f) / size;

                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;

                    // source-over, bottom layer first, straight alpha in the sRGB values
                    // the stylesheet declares (the browser composites them the same way)
                    var accA = 0f;
                    var accR = 0f;
                    var accG = 0f;
                    var accB = 0f;

                    for (var i = layers.Length - 1; i >= 0; i--)
                    {
                        var l = layers[i];
                        var dx = (u - l.CenterX) / Mathf.Max(1e-4f, l.RadiusX);
                        var dy = (v - l.CenterY) / Mathf.Max(1e-4f, l.RadiusY);
                        var r = Mathf.Sqrt(dx * dx + dy * dy);
                        var a = Mathf.Clamp01(l.Alpha) *
                                (1f - ProjectCMainMenuPalette.SmoothStep(0f, Mathf.Max(1e-4f, l.Stop), r));
                        if (a <= 0.0005f) continue;

                        var newA = a + accA * (1f - a);
                        if (newA <= 1e-5f) continue;

                        var under = accA * (1f - a) / newA;
                        var over = a / newA;
                        accR = l.Tint.r * over + accR * under;
                        accG = l.Tint.g * over + accG * under;
                        accB = l.Tint.b * over + accB * under;
                        accA = newA;
                    }

                    // the rounded box + blur(18px): gone at the inscribed ellipse
                    var ex = (u - 0.5f) * 2f;
                    var ey = (v - 0.5f) * 2f;
                    var rim = 1f - ProjectCMainMenuPalette.SmoothStep(rimStart, 1f, Mathf.Sqrt(ex * ex + ey * ey));

                    var color = new Color(accR, accG, accB, 1f);
                    var alpha = ProjectCMainMenuGamma.Alpha(accA * rim, ProjectCMainMenuGamma.Luma(color), backdrop, opacity);
                    pixels[o++] = ToColor32(color, alpha);
                }
            }

            return Store(cacheKey, pixels, size, size);
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

        private static string Key(string kind, string key, Color tint, float alpha,
            float rx, float ry, float cx, float cy, float stop, float backdrop, float opacity, int size)
        {
            return string.Concat(kind, "|", key, "|", F(tint), "|", F(alpha),
                "|", F(rx), ",", F(ry), "|", F(cx), ",", F(cy), "|", F(stop),
                "|", F(backdrop), "|", F(opacity), "|", size.ToString());
        }

        private static string F(float v) => v.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);

        private static string F(Color c) => string.Concat(F(c.r), ",", F(c.g), ",", F(c.b), ",", F(c.a));

        private static byte ToByte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

        private static Color32 ToColor32(Color color, float alpha)
        {
            return new Color32(ToByte(color.r), ToByte(color.g), ToByte(color.b), ToByte(alpha));
        }

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

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
