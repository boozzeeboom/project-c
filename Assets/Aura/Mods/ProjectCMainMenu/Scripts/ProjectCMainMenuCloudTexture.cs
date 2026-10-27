// ProjectC Main Menu — baked tileable cloud masks (src/clouds.js createCloudTexture).
//
// The WebGL mod bakes a DataTexture per veil layer from fbm masses plus a handful
// of seeded radial puffs. The same field is produced here into a Texture2D with
// Repeat wrapping, so the veil shader can tile it exactly like the original.
using UnityEngine;

namespace ProjectCMainMenu
{
    public static class ProjectCMainMenuCloudTexture
    {
        private static readonly int[] Periods = { 4, 8, 16, 32, 64 };

        public static Texture2D Create(int size, int seed, float coverage, float softness, int puffs, string name)
        {
            size = Mathf.Clamp(size <= 0 ? 192 : size, 64, 512);
            var rng = new ProjectCMainMenuPalette.Mulberry32(seed);

            var grids = MakeGrids(ref rng);
            var detail = MakeGrids(ref rng);

            var blobs = new Vector4[Mathf.Max(4, puffs)];
            for (var i = 0; i < blobs.Length; i++)
            {
                var x = rng.Next() * size;
                var y = rng.Next() * size;
                var radius = (0.10f + rng.Next() * 0.20f) * size;
                var weight = 0.6f + rng.Next() * 0.8f;
                blobs[i] = new Vector4(x, y, radius * radius, weight);
            }

            var half = size * 0.5f;
            var threshold = Mathf.Lerp(0.60f, 0.26f, Mathf.Clamp01(coverage));
            var edge = Mathf.Lerp(0.10f, 0.40f, Mathf.Clamp01(softness));

            var pixels = new Color32[size * size];
            var index = 0;
            for (var y = 0; y < size; y++)
            {
                var v = (float)y / size;
                for (var x = 0; x < size; x++)
                {
                    var u = (float)x / size;

                    var puff = 0f;
                    for (var b = 0; b < blobs.Length; b++)
                    {
                        var dx = Mathf.Abs(x - blobs[b].x);
                        if (dx > half) dx = size - dx;
                        var dy = Mathf.Abs(y - blobs[b].y);
                        if (dy > half) dy = size - dy;
                        var q = 1f - (dx * dx + dy * dy) / Mathf.Max(0.0001f, blobs[b].z);
                        if (q > 0f)
                        {
                            var g = q * q * blobs[b].w;
                            if (g > puff) puff = g;
                        }
                    }

                    var baseField = Fbm(grids, u, v, 4);
                    var det = Fbm(detail, u, v, 2);

                    var field = baseField * 0.60f + puff * 0.52f + det * 0.20f;
                    var d = (field - threshold) / Mathf.Max(0.0001f, edge);
                    d = Mathf.Clamp01(d);
                    d = d * d * (3f - 2f * d);
                    var wisp = Mathf.Clamp01(d * (0.70f + 0.62f * det));
                    var value = (byte)Mathf.Clamp(Mathf.RoundToInt(wisp * 255f), 0, 255);

                    pixels[index++] = new Color32(value, value, value, value);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = string.IsNullOrEmpty(name) ? "PCMM_Cloud_" + seed : name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static float[][] MakeGrids(ref ProjectCMainMenuPalette.Mulberry32 rng)
        {
            var grids = new float[Periods.Length][];
            for (var i = 0; i < Periods.Length; i++)
            {
                var p = Periods[i];
                var grid = new float[p * p];
                for (var k = 0; k < grid.Length; k++) grid[k] = rng.Next();
                grids[i] = grid;
            }
            return grids;
        }

        private static float SampleGrid(float[] grid, int period, float x, float y)
        {
            var xi = Mathf.Floor(x);
            var yi = Mathf.Floor(y);
            var x0 = (int)xi % period;
            if (x0 < 0) x0 += period;
            var y0 = (int)yi % period;
            if (y0 < 0) y0 += period;
            var x1 = (x0 + 1) % period;
            var y1 = (y0 + 1) % period;
            var fx = x - xi;
            var fy = y - yi;
            var sx = fx * fx * (3f - 2f * fx);
            var sy = fy * fy * (3f - 2f * fy);

            var a = grid[y0 * period + x0];
            var b = grid[y0 * period + x1];
            var c = grid[y1 * period + x0];
            var d = grid[y1 * period + x1];
            var ab = a + (b - a) * sx;
            var cd = c + (d - c) * sx;
            return ab + (cd - ab) * sy;
        }

        private static float Fbm(float[][] grids, float u, float v, int octaves)
        {
            var sum = 0f;
            var amp = 0.5f;
            var norm = 0f;
            for (var o = 0; o < octaves && o < Periods.Length; o++)
            {
                var p = Periods[o];
                sum += amp * SampleGrid(grids[o], p, u * p, v * p);
                norm += amp;
                amp *= 0.5f;
            }
            return norm > 0f ? sum / norm : 0f;
        }
    }
}
