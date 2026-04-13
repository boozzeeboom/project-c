using UnityEngine;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Утилиты для генерации процедурного шума (FBM, Simplex-like).
    /// Используется MountainMeshBuilder для создания естественных форм гор.
    /// </summary>
    public static class NoiseUtils
    {
        // Perlin noise seed
        private static int _seed = 42;

        /// <summary>
        /// Установить seed для воспроизводимости генерации.
        /// </summary>
        public static void SetSeed(int seed)
        {
            _seed = seed;
        }

        /// <summary>
        /// 2D Perlin noise с seed offset.
        /// </summary>
        public static float Perlin2D(float x, float y)
        {
            return Mathf.PerlinNoise(x + _seed, y + _seed);
        }

        /// <summary>
        /// Fractal Brownian Motion (FBM) —叠加多个 октав Perlin noise.
        /// 
        /// Параметры:
        /// - frequency: базовая частота шума (выше = более детализированный)
        /// - octaves: количество октав (больше = более детализированный, но дороже)
        /// - lacunarity: множитель частоты на октаву (обычно 2.0)
        /// - persistence: множитель амплитуды на октаву (обычно 0.5)
        /// 
        /// Возвращает: значение в диапазоне [-1, 1]
        /// </summary>
        public static float FBM(
            float x,
            float y,
            float frequency = 1f,
            int octaves = 6,
            float lacunarity = 2f,
            float persistence = 0.5f)
        {
            float value = 0f;
            float amplitude = 1f;
            float maxAmplitude = 0f;

            float currentX = x * frequency;
            float currentY = y * frequency;

            for (int i = 0; i < octaves; i++)
            {
                value += (Perlin2D(currentX, currentY) - 0.5f) * amplitude;
                maxAmplitude += amplitude;

                currentX *= lacunarity;
                currentY *= lacunarity;
                amplitude *= persistence;
            }

            return value / maxAmplitude; // Нормализация к [-1, 1]
        }

        /// <summary>
        /// Ridge noise — создаёт "гребни" и "линейные" структуры.
        /// Идеально для тектонических горных хребтов.
        /// 
        /// Формула: 1 - 2 * |Perlin(x,y)|
        /// Возвращает: [0, 1] где 1 = гребень, 0 = впадина
        /// </summary>
        public static float RidgeNoise(
            float x,
            float y,
            float frequency = 1f,
            int octaves = 6,
            float lacunarity = 2f,
            float persistence = 0.5f)
        {
            float value = 0f;
            float amplitude = 1f;
            float maxAmplitude = 0f;

            float currentX = x * frequency;
            float currentY = y * frequency;

            for (int i = 0; i < octaves; i++)
            {
                float n = Perlin2D(currentX, currentY) - 0.5f;
                n = Mathf.Abs(n) * 2f;          // [0, 1]
                n = 1f - n;                      // Инверсия: гребни = 1
                n *= n;                          // Усиление контраста

                value += n * amplitude;
                maxAmplitude += amplitude;

                currentX *= lacunarity;
                currentY *= lacunarity;
                amplitude *= persistence;
            }

            return value / maxAmplitude;
        }

        /// <summary>
        /// 3D Perlin noise (через 2D projection + height offset).
        /// Полезно для генерации объёмных форм.
        /// </summary>
        public static float Perlin3D(float x, float y, float z)
        {
            // Approximation через 2D slices
            float xy = Perlin2D(x, y);
            float yz = Perlin2D(y + 31.7f, z + 17.3f);
            float zx = Perlin2D(z + 53.1f, x + 7.9f);

            return (xy + yz + zx) / 3f;
        }

        /// <summary>
        /// Turbulence — FBM с абсолютными значениями.
        /// Создаёт "неровные" поверхности, как реальные скалы.
        /// </summary>
        public static float Turbulence(
            float x,
            float y,
            float frequency = 1f,
            int octaves = 6,
            float lacunarity = 2f,
            float persistence = 0.5f)
        {
            float value = 0f;
            float amplitude = 1f;
            float maxAmplitude = 0f;

            float currentX = x * frequency;
            float currentY = y * frequency;

            for (int i = 0; i < octaves; i++)
            {
                float n = Perlin2D(currentX, currentY) - 0.5f;
                value += Mathf.Abs(n) * amplitude;
                maxAmplitude += amplitude;

                currentX *= lacunarity;
                currentY *= lacunarity;
                amplitude *= persistence;
            }

            return value / maxAmplitude;
        }

        /// <summary>
        /// Clamp noise к заданному диапазону.
        /// </summary>
        public static float ClampNoise(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Remap noise из [-1, 1] в [0, 1].
        /// </summary>
        public static float Remap01(float value)
        {
            return (value + 1f) * 0.5f;
        }

        /// <summary>
        /// Generate 2D heightfield для отладки/визуализации.
        /// Возвращает массив значений height[x, z].
        /// </summary>
        public static float[,] GenerateHeightfield(
            int width,
            int height,
            float scale = 0.1f,
            int octaves = 6,
            float persistence = 0.5f,
            float lacunarity = 2f)
        {
            float[,] heightfield = new float[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    heightfield[x, z] = FBM(
                        x * scale,
                        z * scale,
                        frequency: 1f,
                        octaves: octaves,
                        lacunarity: lacunarity,
                        persistence: persistence
                    );
                }
            }

            return heightfield;
        }
    }
}
