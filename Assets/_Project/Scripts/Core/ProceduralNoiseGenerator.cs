using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Генерирует процедурные noise-текстуры для шейдера CloudGhibli.
    /// Создаётся один раз при старте и кешируется.
    /// </summary>
    public static class ProceduralNoiseGenerator
    {
        private static Texture2D _noiseTexture1;
        private static Texture2D _noiseTexture2;

        /// <summary>
        /// Получить noise-текстуру #1 (крупные формы)
        /// </summary>
        public static Texture2D GetNoiseTexture1()
        {
            if (_noiseTexture1 == null)
            {
                _noiseTexture1 = GenerateNoiseTexture(512, 512, seed: 42, frequency: 4, octaves: 4);
            }
            return _noiseTexture1;
        }

        /// <summary>
        /// Получить noise-текстуру #2 (мелкие детали)
        /// </summary>
        public static Texture2D GetNoiseTexture2()
        {
            if (_noiseTexture2 == null)
            {
                _noiseTexture2 = GenerateNoiseTexture(512, 512, seed: 137, frequency: 8, octaves: 6);
            }
            return _noiseTexture2;
        }

        /// <summary>
        /// Сгенерировать procedural noise текстуру (Fractal Brownian Motion)
        /// </summary>
        private static Texture2D GenerateNoiseTexture(int width, int height, int seed, float frequency, int octaves)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "ProceduralNoise"
            };

            Random.InitState(seed);

            // Генерируем случайные градиенты для Perlin-like шума
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / width;
                    float ny = (float)y / height;

                    float value = FractalBrownianMotion(nx, ny, frequency, octaves, seed);

                    // Нормализуем в 0..1
                    value = value * 0.5f + 0.5f;
                    value = Mathf.Clamp01(value);

                    pixels[y * width + x] = new Color(value, value, value, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Fractal Brownian Motion —叠加 multiple octaves of noise
        /// </summary>
        private static float FractalBrownianMotion(float x, float y, float frequency, int octaves, int seed)
        {
            float value = 0f;
            float amplitude = 0.5f;
            float lacunarity = 2f;

            for (int i = 0; i < octaves; i++)
            {
                value += amplitude * PerlinNoise(x * frequency, y * frequency, seed + i * 1000);
                frequency *= lacunarity;
                amplitude *= 0.5f;
            }

            return value;
        }

        /// <summary>
        /// Простой Perlin-like шум (value noise с интерполяцией)
        /// </summary>
        private static float PerlinNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;

            // Smooth interpolation
            float u = SmoothStep(xf);
            float v = SmoothStep(yf);

            // Hash для случайных значений
            float n00 = Hash(xi, yi, seed);
            float n10 = Hash(xi + 1, yi, seed);
            float n01 = Hash(xi, yi + 1, seed);
            float n11 = Hash(xi + 1, yi + 1, seed);

            // Bilinear interpolation
            float nx0 = Mathf.Lerp(n00, n10, u);
            float nx1 = Mathf.Lerp(n01, n11, u);

            return Mathf.Lerp(nx0, nx1, v) * 2f - 1f; // -1..1
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Простой hash для value noise
        /// </summary>
        private static float Hash(int x, int y, int seed)
        {
            int h = seed + x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        /// <summary>
        /// Очистить кешированные текстуры (для перегенерации)
        /// </summary>
        public static void ClearCache()
        {
            if (_noiseTexture1 != null)
            {
                Object.Destroy(_noiseTexture1);
                _noiseTexture1 = null;
            }
            if (_noiseTexture2 != null)
            {
                Object.Destroy(_noiseTexture2);
                _noiseTexture2 = null;
            }
        }
    }
}
