using System;
using System.Collections.Generic;

namespace CloudGenerator
{
    /// <summary>
    /// Cloud shape generator using Worley + Perlin noise stack.
    /// Pure C# — no Unity dependencies.
    /// 
    /// Based on Horizon Zero Dawn Volumetric Cloudscapes approach:
    /// - Perlin for base mass
    /// - Worley for erosion/billowy detail
    /// - Multiple FBM layers for natural variation
    /// </summary>
    public static class CloudMath
    {
        // ============ NOISE CORE ============

        static int[] Perm = new int[512];

        static CloudMath()
        {
            // Initialize permutation table with seed
            int[] basePerm = new int[256];
            for (int i = 0; i < 256; i++) basePerm[i] = i;
            Shuffle(basePerm, 1337);
            for (int i = 0; i < 512; i++) Perm[i] = basePerm[i & 255];
        }

        static void Shuffle(int[] arr, int seed)
        {
            Random rng = new Random(seed);
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }

        static int Hash(int x, int y, int z)
        {
            return Perm[(Perm[(Perm[x & 255] + y) & 255] + z) & 255];
        }

        /// <summary>
        /// Fade function: 6t^5 - 15t^4 + 10t^3
        /// </summary>
        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static float Grad3D(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        /// <summary>
        /// 3D Perlin noise — returns [-1, 1]
        /// </summary>
        public static float Perlin3D(float x, float y, float z)
        {
            int xi = (int)Math.Floor(x) & 255;
            int yi = (int)Math.Floor(y) & 255;
            int zi = (int)Math.Floor(z) & 255;

            float xf = x - (float)Math.Floor(x);
            float yf = y - (float)Math.Floor(y);
            float zf = z - (float)Math.Floor(z);

            float u = Fade(xf), v = Fade(yf), w = Fade(zf);

            int aaa = Hash(xi, yi, zi);
            int aba = Hash(xi, yi + 1, zi);
            int aab = Hash(xi, yi, zi + 1);
            int abb = Hash(xi, yi + 1, zi + 1);
            int baa = Hash(xi + 1, yi, zi);
            int bba = Hash(xi + 1, yi + 1, zi);
            int bab = Hash(xi + 1, yi, zi + 1);
            int bbb = Hash(xi + 1, yi + 1, zi + 1);

            float x1 = Lerp(Grad3D(aaa, xf, yf, zf), Grad3D(baa, xf - 1, yf, zf), u);
            float x2 = Lerp(Grad3D(aba, xf, yf - 1, zf), Grad3D(bba, xf - 1, yf - 1, zf), u);
            float y1 = Lerp(x1, x2, v);

            x1 = Lerp(Grad3D(aab, xf, yf, zf - 1), Grad3D(bab, xf - 1, yf, zf - 1), u);
            x2 = Lerp(Grad3D(abb, xf, yf - 1, zf - 1), Grad3D(bbb, xf - 1, yf - 1, zf - 1), u);
            float y2 = Lerp(x1, x2, v);

            return Lerp(y1, y2, w);
        }

        /// <summary>
        /// Fractal Brownian Motion — layered noise for natural cloud patterns
        /// </summary>
        public static float FBM3D(float x, float y, float z, int octaves = 4, float persistence = 0.5f, float lacunarity = 2.0f)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                value += amplitude * Perlin3D(x * frequency, y * frequency, z * frequency);
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return value / maxValue;
        }

        // ============ WORLEY NOISE ============

        struct WorleyPoint
        {
            public float x, y, z;
        }

        /// <summary>
        /// Worley noise (cellular/Voronoi) — returns distance to nearest feature point
        /// Creates the "cell-like" structure of clouds
        /// </summary>
        public static float Worley3D(float x, float y, float z, int seed = 0)
        {
            int xi = (int)Math.Floor(x);
            int yi = (int)Math.Floor(y);
            int zi = (int)Math.Floor(z);

            float minDist = float.MaxValue;

            // Check 3x3x3 neighborhood
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int cx = xi + dx;
                        int cy = yi + dy;
                        int cz = zi + dz;

                        // Generate deterministic random point in cell
                        float px = cx + Hash(cx, cy, cz, seed) * 0.001f;
                        float py = cy + Hash(cx + 100, cy, cz, seed) * 0.001f;
                        float pz = cz + Hash(cx, cy + 100, cz, seed) * 0.001f;

                        float d = (px - x) * (px - x) + (py - y) * (py - y) + (pz - z) * (pz - z);
                        if (d < minDist) minDist = d;
                    }
                }
            }

            return (float)Math.Sqrt(minDist);
        }

        /// <summary>
        /// Inverted Worley — returns 1 - nearest distance
        /// Gives billowy, fluffy shapes (like cloud tops)
        /// </summary>
        public static float WorleyInverted3D(float x, float y, float z, int seed = 0)
        {
            return 1f - Worley3D(x, y, z, seed);
        }

        // ============ PERLIN-WORLEY COMBO ============

        /// <summary>
        /// Perlin-Worley composite noise
        /// Perlin gives base mass, Worley erodes edges for billowy detail
        /// Based on Horizon Zero Dawn approach
        /// </summary>
        public static float PerlinWorley3D(float x, float y, float z, int seed = 0)
        {
            // Large scale Perlin for base shape
            float perlin = FBM3D(x * 0.5f, y * 0.5f, z * 0.5f, 4, 0.5f, 2.0f);
            perlin = (perlin + 1f) * 0.5f; // remap to [0,1]

            // Worley at multiple scales for erosion
            float worley1 = Worley3D(x * 2f, y * 2f, z * 2f, seed);
            float worley2 = Worley3D(x * 4f, y * 4f, z * 4f, seed + 1) * 0.5f;
            float worley = 1f - (worley1 + worley2) * 0.5f; // inverted

            // Erode perlin with worley
            float result = perlin * worley;

            return result;
        }

        // ============ CLOUD SPHERE ============

        public struct CloudSphere
        {
            public float x, y, z;  // position (local to cloud pivot)
            public float radius;
            public float density;   // 0-1 opacity
        }

        // ============ CLOUD GENERATION ============

        /// <summary>
        /// Generate cloud shape as cluster of spheres.
        /// Uses Worley noise for natural cell-like cloud structure.
        /// </summary>
        /// <param name="cloudSize">Overall cloud diameter</param>
        /// <param name="coverage">How much sky is filled (0-1)</param>
        /// <param name="density">Base density threshold</param>
        /// <param name="turbulence">Wind turbulence intensity (0-1)</param>
        /// <param name="seed">Random seed for reproducibility</param>
        /// <param name="humidity">Sphere size multiplier (0-1, higher = bigger)</param>
        /// <param name="erosion">How much Worley erodes cloud edges (0-1)</param>
        /// <param name="stormIntensity">Storm mode (0-1): vertical stretch, darker</param>
        /// <param name="windX">Wind direction X</param>
        /// <param name="windZ">Wind direction Z</param>
        public static List<CloudSphere> GenerateCloud(
            float cloudSize = 100f,
            float coverage = 0.6f,
            float density = 0.5f,
            float turbulence = 0.3f,
            int seed = 42,
            float humidity = 0.6f,
            float erosion = 0.5f,
            float stormIntensity = 0f,
            float windX = 0f,
            float windZ = 0f
        )
        {
            var spheres = new List<CloudSphere>();

            // Grid for sphere placement
            float cellSize = cloudSize * 0.2f;
            int gridSize = (int)Math.Ceiling(cloudSize / cellSize) + 2;
            int halfGrid = gridSize / 2;

            for (int ix = -halfGrid; ix <= halfGrid; ix++)
            {
                for (int iy = -halfGrid / 2; iy <= halfGrid / 2; iy++)
                {
                    for (int iz = -halfGrid; iz <= halfGrid; iz++)
                    {
                        // Cell center position
                        float cx = ix * cellSize;
                        float cy = iy * cellSize * 0.6f; // flattened vertically
                        float cz = iz * cellSize;

                        // Normalized position for noise sampling
                        float nx = cx / cloudSize;
                        float ny = cy / cloudSize;
                        float nz = cz / cloudSize;

                        // Apply wind distortion
                        float windOffset = turbulence * 0.15f;
                        float distX = cx + windX * ny * windOffset * cloudSize;
                        float distZ = cz + windZ * ny * windOffset * cloudSize;

                        // Perlin-Worley composite noise for cloud shape
                        float noiseValue = PerlinWorley3D(
                            nx * 2.0f + seed * 0.01f,
                            ny * 2.0f,
                            nz * 2.0f,
                            seed
                        );

                        // Apply erosion
                        float eroded = noiseValue * (1f - erosion * 0.5f);

                        // Height profile: clouds are fuller at top, wispy at bottom
                        float heightFactor = 1f - (float)Math.Pow(Math.Abs(ny - 0.3f), 2f) * 0.8f;
                        eroded *= heightFactor;

                        // Density threshold based on coverage
                        float threshold = 1f - coverage;
                        if (eroded < threshold) continue;

                        // Normalized local density
                        float localDensity = (eroded - threshold) / (1f - threshold);
                        localDensity *= density;

                        // Storm modification: vertical stretch + darker
                        float verticalStretch = 1f + stormIntensity * 0.8f;
                        float baseRadius = cellSize * 0.5f * (humidity * 0.6f + 0.4f);

                        // Radius variation using Worley
                        float radiusNoise = Worley3D(nx * 3f, ny * 3f, nz * 3f, seed + 100);
                        float radius = baseRadius * (0.8f + (1f - radiusNoise) * 0.4f);

                        // Storm: tighter, darker spheres
                        if (stormIntensity > 0f)
                        {
                            radius *= (1f - stormIntensity * 0.3f);
                            localDensity *= (1f + stormIntensity * 0.4f);
                        }

                        // Skip low-density spheres
                        if (localDensity < 0.1f) continue;

                        spheres.Add(new CloudSphere
                        {
                            x = distX,
                            y = cy * verticalStretch,
                            z = distZ,
                            radius = radius,
                            density = Math.Clamp(localDensity, 0f, 1f)
                        });
                    }
                }
            }

            return spheres;
        }

        /// <summary>
        /// Generate storm clouds — denser, darker, more vertical
        /// </summary>
        public static List<CloudSphere> GenerateStormCloud(
            float cloudSize = 100f,
            float intensity = 0.7f,
            int seed = 42
        )
        {
            return GenerateCloud(
                cloudSize: cloudSize,
                coverage: 0.8f,
                density: 0.8f,
                turbulence: 0.6f,
                seed: seed,
                humidity: 0.8f,
                erosion: 0.3f,
                stormIntensity: intensity,
                windX: 1f,
                windZ: 0.5f
            );
        }

        /// <summary>
        /// Generate wispy cirrus-like clouds
        /// </summary>
        public static List<CloudSphere> GenerateCirrus(
            float cloudSize = 100f,
            int seed = 42
        )
        {
            return GenerateCloud(
                cloudSize: cloudSize,
                coverage: 0.3f,
                density: 0.3f,
                turbulence: 0.7f,
                seed: seed,
                humidity: 0.2f,
                erosion: 0.7f,
                stormIntensity: 0f
            );
        }

        static int Hash(int x, int y, int z, int seed)
        {
            int n = x + y * 57 + z * 131 + seed * 311;
            n = (n << 13) ^ n;
            return (n * (n * n * 15731 + 789221) + 1376312589) & 0x7FFFFFFF;
        }
    }
}