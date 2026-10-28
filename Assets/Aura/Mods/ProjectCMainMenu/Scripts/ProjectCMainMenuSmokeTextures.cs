// ProjectC Main Menu — the procedural smoke sheets, ported 1:1 from the mod's
// src/smoke.js (smokeImage / smokePuff / smokeWisp).
//
// The web menu builds these once as canvas data-URLs and feeds them to CSS as
// background images. Unity cannot take a data-URL, so the same maths is baked
// into Texture2D assets at runtime and handed to UI Toolkit through
// StyleBackground. Patterns, tints and seeds are identical to the mod, so the
// smoke reads the same.
using System.Collections.Generic;
using UnityEngine;

namespace ProjectCMainMenu
{
    public static class ProjectCMainMenuSmokeTextures
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        // The tints the interface uses (ui.js applyPalette).
        private static readonly Color32 LightTint = new Color32(233, 242, 236, 255);
        private static readonly Color32 DarkTint = new Color32(9, 15, 11, 255);
        private static readonly Color32 WispTint = new Color32(138, 150, 142, 255);
        private static readonly Color32 CloudTint = new Color32(22, 28, 23, 255);

        private struct Field
        {
            public int CellsX;
            public int CellsY;
            public float[] Grid;
        }

        // Every sheet's alpha is baked for linear-space blending (ProjectCMainMenuGamma):
        // the backdrop is what the sheet is drawn over, the opacity the typical element
        // opacity it is drawn at (the transition puffs peak at ~0.46-0.5).

        /// <summary>Pale mist sheet — the transition puffs (mod: smokeImage tint 233,242,236).</summary>
        public static Texture2D Light => Image(LightTint, 4211, 144, ProjectCMainMenuGamma.Scrim, 0.5f);

        /// <summary>Charcoal smoke sheet — the shadow side (mod: smokeImage tint 9,15,11).</summary>
        public static Texture2D Dark => Image(DarkTint, 9109, 144, ProjectCMainMenuGamma.Sky, 0.5f);

        /// <summary>Horizontally tileable bank of mist (mod: smokeWisp tint 138,150,142).</summary>
        public static Texture2D Wisp => WispSheet(WispTint, 7781);

        /// <summary>
        /// The cloud a button is made of (mod: --pc-smoke-cloud = smokePuff tint 22,28,23,
        /// size 160), drawn over the menu scrim at ~0.88 × 0.3-0.72.
        /// </summary>
        public static Texture2D Cloud => Puff(CloudTint, 5521, 160, ProjectCMainMenuGamma.Scrim, 0.40f);

        /// <summary>
        /// The same --pc-smoke-cloud puff for the settings bank (.pc-card-cloud i), drawn
        /// over the open sky at ~0.93 × 0.24-0.64. The mod uses the charcoal puff here too —
        /// not the pale high-frequency smokeImage sheet, which is what made the bank read
        /// as blotchy white circles.
        /// </summary>
        public static Texture2D CardCloud => Puff(CloudTint, 5521, 160, ProjectCMainMenuGamma.Sky, 0.45f);

        public static void DisposeAll()
        {
            foreach (var pair in Cache)
            {
                if (pair.Value != null) Object.Destroy(pair.Value);
            }

            Cache.Clear();
        }

        // ---------------------------------------------------------------- fields

        private static Field BuildField(ref ProjectCMainMenuPalette.Mulberry32 rng, int cellsX, int cellsY)
        {
            var grid = new float[cellsX * cellsY];
            for (var i = 0; i < grid.Length; i++) grid[i] = rng.Next();
            return new Field { CellsX = cellsX, CellsY = cellsY, Grid = grid };
        }

        private static float Sample(in Field field, float u, float v)
        {
            var x = u * field.CellsX;
            var y = v * field.CellsY;
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);

            var x0 = xi % field.CellsX;
            if (x0 < 0) x0 += field.CellsX;
            var y0 = yi % field.CellsY;
            if (y0 < 0) y0 += field.CellsY;

            var x1 = (x0 + 1) % field.CellsX;
            var y1 = (y0 + 1) % field.CellsY;

            var fx = x - xi;
            var fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            var a = field.Grid[y0 * field.CellsX + x0];
            var b = field.Grid[y0 * field.CellsX + x1];
            var c = field.Grid[y1 * field.CellsX + x0];
            var d = field.Grid[y1 * field.CellsX + x1];

            var ab = a + (b - a) * fx;
            var cd = c + (d - c) * fx;
            return ab + (cd - ab) * fy;
        }

        // --------------------------------------------------------------- sheets

        /// <summary>smokeImage(): soft round blob, radial falloff, ragged rim.</summary>
        public static Texture2D Image(Color32 tint, int seed, int size = 144,
            float backdrop = -1f, float opacity = 1f)
        {
            var key = $"img|{tint.r},{tint.g},{tint.b}|{seed}|{size}|{Mathf.RoundToInt(backdrop * 1000f)}|{Mathf.RoundToInt(opacity * 1000f)}";
            var luma = ProjectCMainMenuGamma.Luma(tint);
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            var rng = new ProjectCMainMenuPalette.Mulberry32(seed);
            var f0 = BuildField(ref rng, 5, 5);
            var f1 = BuildField(ref rng, 11, 11);
            var f2 = BuildField(ref rng, 23, 23);

            var pixels = new Color32[size * size];
            var o = 0;

            for (var y = 0; y < size; y++)
            {
                var v = (y + 0.5f) / size;
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    var f = Sample(f0, u, v) * 0.5f + Sample(f1, u, v) * 0.31f + Sample(f2, u, v) * 0.19f;

                    var dx = u - 0.5f;
                    var dy = v - 0.5f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy) * 2f + (f - 0.5f) * 0.46f;
                    var fall = 1f - ProjectCMainMenuPalette.SmoothStep(0.34f, 1f, r);

                    var a = ProjectCMainMenuPalette.SmoothStep(0.36f, 0.72f, f) * fall;
                    a = a * a * (3f - 2f * a);
                    if (backdrop >= 0f) a = ProjectCMainMenuGamma.Alpha(a, luma, backdrop, opacity);

                    pixels[o++] = new Color32(tint.r, tint.g, tint.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }

            return Store(key, pixels, size, size, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
        }

        /// <summary>smokePuff(): one low-frequency lump, lumpy rim, no speckle.</summary>
        public static Texture2D Puff(Color32 tint, int seed, int size = 160,
            float backdrop = -1f, float opacity = 1f)
        {
            var key = $"puff|{tint.r},{tint.g},{tint.b}|{seed}|{size}|{Mathf.RoundToInt(backdrop * 1000f)}|{Mathf.RoundToInt(opacity * 1000f)}";
            var luma = ProjectCMainMenuGamma.Luma(tint);
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            var rng = new ProjectCMainMenuPalette.Mulberry32(seed);
            var g0 = BuildField(ref rng, 3, 3);
            var g1 = BuildField(ref rng, 6, 6);

            var pixels = new Color32[size * size];
            var o = 0;

            for (var y = 0; y < size; y++)
            {
                var v = (y + 0.5f) / size;
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    var f = Sample(g0, u, v) * 0.66f + Sample(g1, u, v) * 0.34f;

                    var dx = u - 0.5f;
                    var dy = v - 0.5f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy) * 2f + (f - 0.5f) * 0.44f;
                    var fall = 1f - ProjectCMainMenuPalette.SmoothStep(0.16f, 1f, r);

                    var a = Mathf.Pow(Mathf.Clamp01(fall), 1.3f) * (0.58f + 0.42f * f);
                    if (backdrop >= 0f) a = ProjectCMainMenuGamma.Alpha(a, luma, backdrop, opacity);

                    pixels[o++] = new Color32(tint.r, tint.g, tint.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }

            return Store(key, pixels, size, size, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
        }

        /// <summary>smokeWisp(): seamless left-to-right bank, transparent top and bottom.</summary>
        public static Texture2D WispSheet(Color32 tint, int seed, int width = 256, int height = 96)
        {
            var key = $"wisp|{tint.r},{tint.g},{tint.b}|{seed}|{width}x{height}";
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            var rng = new ProjectCMainMenuPalette.Mulberry32(seed);
            var f0 = BuildField(ref rng, 6, Mathf.Max(2, Mathf.RoundToInt(6 / 2f)));
            var f1 = BuildField(ref rng, 13, Mathf.Max(2, Mathf.RoundToInt(13 / 2f)));
            var f2 = BuildField(ref rng, 29, Mathf.Max(2, Mathf.RoundToInt(29 / 2f)));

            var pixels = new Color32[width * height];
            var o = 0;

            for (var y = 0; y < height; y++)
            {
                var v = (y + 0.5f) / height;
                var band = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 1.35f);

                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) / width;
                    var f = Sample(f0, u, v) * 0.5f + Sample(f1, u, v) * 0.32f + Sample(f2, u, v) * 0.18f;

                    var a = ProjectCMainMenuPalette.SmoothStep(0.30f, 0.86f, f) * band;
                    a = a * a * (3f - 2f * a) * 0.9f;

                    pixels[o++] = new Color32(tint.r, tint.g, tint.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }

            return Store(key, pixels, width, height, TextureWrapMode.Repeat, TextureWrapMode.Clamp);
        }

        // --------------------------------------------------------------- helpers

        private static Texture2D Store(string key, Color32[] pixels, int width, int height,
            TextureWrapMode wrapU, TextureWrapMode wrapV)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PCMM_" + key.Replace('|', '_').Replace(',', '_'),
                filterMode = FilterMode.Bilinear,
                wrapModeU = wrapU,
                wrapModeV = wrapV,
                hideFlags = HideFlags.DontSave,
            };

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Cache[key] = texture;
            return texture;
        }
    }
}
