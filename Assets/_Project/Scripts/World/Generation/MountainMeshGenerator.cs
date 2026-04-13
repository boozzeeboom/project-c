using UnityEngine;
using ProjectC.World.Core;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Runtime генерация горных мешей V2 (ADR-0001).
    /// 
    /// АЛГОРИТМ:
    /// 1. Power-Law Cone базовый меш: r(h) = R_base * (1 - h/H)^exponent
    /// 2. Shoulder Bulge: Gaussian bump на mid-height
    /// 3. Large-Scale Noise: FBM в полярных координатах (silhouette)
    /// 4. Small-Scale Noise: FBM в декартовых координатах (surface detail)
    /// 5. Ridge Noise: только Tectonic (carves linear ridges)
    /// 6. Asymmetry: angular variation
    /// 7. Crater depression: только Volcanic
    /// 8. Recalculate normals
    /// 
    /// ОТЛИЧИЯ ОТ V1:
    /// - НЕТ Cylinder/Ellipsoid/Dome primitives
    /// - НЕТ radiusFactor Lerp
    /// - НЕТ формулы meshHeight = baseRadius * hRatio
    /// + Power-Law Cone mathematical model
    /// + Shoulder Bulge для горного вида
    /// + 3-layer noise strategy
    /// + MeshCollider вместо CapsuleCollider
    /// 
    /// Использование: вызвать GenerateMountainMesh() с параметрами.
    /// </summary>
    public static class MountainMeshGenerator
    {
        /// <summary>
        /// Сгенерировать меш горы.
        /// </summary>
        /// <param name="profile">MountainProfile (presets для типа формы)</param>
        /// <param name="meshHeight">Высота меша (ЯВНОЕ значение, НЕ формула!)</param>
        /// <param name="baseRadius">Радиус основания (ЯВНОЕ значение)</param>
        /// <param name="segments">Количество сегментов по кругу (LOD0: 64, LOD1: 32)</param>
        /// <param name="rings">Количество колец по высоте (LOD0: 24, LOD1: 12)</param>
        /// <param name="seed">Seed для шума (уникальный для каждого пика)</param>
        /// <returns>Готовый Mesh</returns>
        public static Mesh GenerateMountainMesh(
            MountainProfile profile,
            float meshHeight,
            float baseRadius,
            int segments,
            int rings,
            int seed = 42)
        {
            // 1. Создать базовый Power-Law Cone меш
            Mesh mesh = GeneratePowerLawConeMesh(profile, meshHeight, baseRadius, segments, rings);

            // 2. Apply Large-Scale Noise (silhouette)
            ApplyLargeScaleNoise(mesh, profile, baseRadius, meshHeight, seed);

            // 3. Apply Small-Scale Noise (surface detail)
            ApplySmallScaleNoise(mesh, profile, baseRadius, meshHeight, seed + 1000);

            // 4. Apply Ridge Noise (Tectonic only)
            if (profile.useRidgeNoise)
            {
                ApplyRidgeNoise(mesh, profile, baseRadius, meshHeight, seed + 2000);
            }

            // 5. Apply Asymmetry
            ApplyAsymmetry(mesh, profile, baseRadius, meshHeight, seed + 3000);

            // 6. Apply Crater (Volcanic only)
            if (profile.hasCrater)
            {
                ApplyCrater(mesh, profile, baseRadius, meshHeight);
            }

            // 7. Recalculate normals и bounds
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Сгенерировать упрощённый меш для MeshCollider (меньше треугольников).
        /// </summary>
        public static Mesh GenerateColliderMesh(
            MountainProfile profile,
            float meshHeight,
            float baseRadius)
        {
            // Упрощённый меш: 16 segments, 8 rings
            return GenerateMountainMesh(profile, meshHeight, baseRadius, 16, 8, seed: 0);
        }

        #region Base Mesh Generation

        /// <summary>
        /// Создать базовый Power-Law Cone меш.
        /// r(h) = R_base * (1 - h/H)^exponent + shoulder(h)
        /// </summary>
        private static Mesh GeneratePowerLawConeMesh(
            MountainProfile profile,
            float meshHeight,
            float baseRadius,
            int segments,
            int rings)
        {
            int vertexCount = (segments + 1) * (rings + 1);
            int triangleCount = segments * rings * 2;

            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[triangleCount * 3];
            Vector2[] uv = new Vector2[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];

            float angleStep = (Mathf.PI * 2f) / segments;

            for (int ring = 0; ring <= rings; ring++)
            {
                float normalizedHeight = (float)ring / rings;
                float y = normalizedHeight * meshHeight;

                // Get radius from profile (power-law + shoulder)
                float radius = profile.GetRadiusAtHeight(normalizedHeight, baseRadius);

                for (int seg = 0; seg <= segments; seg++)
                {
                    int index = ring * (segments + 1) + seg;
                    float angle = seg * angleStep;

                    vertices[index] = new Vector3(
                        Mathf.Cos(angle) * radius,
                        y,
                        Mathf.Sin(angle) * radius
                    );

                    // Временные нормализи (будут пересчитаны позже)
                    normals[index] = Vector3.up;
                    uv[index] = new Vector2((float)seg / segments, normalizedHeight);
                }
            }

            // Generate triangles
            int triIndex = 0;
            for (int ring = 0; ring < rings; ring++)
            {
                for (int seg = 0; seg < segments; seg++)
                {
                    int current = ring * (segments + 1) + seg;
                    int next = current + segments + 1;

                    triangles[triIndex++] = current;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = current + 1;

                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = next + 1;
                }
            }

            return new Mesh
            {
                vertices = vertices,
                triangles = triangles,
                uv = uv,
                normals = normals
            };
        }

        #endregion

        #region Noise Application

        /// <summary>
        /// Large-Scale Noise: FBM в полярных координатах для silhouette variation.
        /// Создаёт ridges, spurs, general irregularity.
        /// Амплитуда пик на mid-height, ноль на вершине и базе.
        /// </summary>
        private static void ApplyLargeScaleNoise(
            Mesh mesh,
            MountainProfile profile,
            float baseRadius,
            float meshHeight,
            int seed)
        {
            Vector3[] vertices = mesh.vertices;
            float amplitude = profile.largeNoiseAmplitude * baseRadius;
            float frequency = profile.largeNoiseFrequency;

            if (amplitude < 0.001f) return; // Skip если амплитуда слишком маленькая

            // Set seed для воспроизводимости
            NoiseUtils.SetSeed(seed);

            for (int i = 0; i < vertices.Length; i++)
            {
                float normalizedHeight = vertices[i].y / meshHeight;

                // Height falloff: noise peak at mid-height, zero at top and base
                float heightFalloff = Mathf.Sin(normalizedHeight * Mathf.PI);

                // Polar coordinates
                float angle = Mathf.Atan2(vertices[i].z, vertices[i].x);
                float radius = new Vector2(vertices[i].x, vertices[i].z).magnitude;

                // FBM в полярных координатах
                float noiseValue = NoiseUtils.FBM(
                    angle * frequency,
                    normalizedHeight * frequency,
                    frequency: frequency,
                    octaves: profile.largeNoiseOctaves);

                // Apply displacement along radial direction
                float displacement = noiseValue * amplitude * heightFalloff;
                Vector2 horizontalPos = new Vector2(vertices[i].x, vertices[i].z);
                if (horizontalPos.sqrMagnitude > 0.001f)
                {
                    Vector2 horizontalDir = horizontalPos.normalized;
                    vertices[i].x += horizontalDir.x * displacement;
                    vertices[i].z += horizontalDir.y * displacement;
                }
            }

            mesh.vertices = vertices;
        }

        /// <summary>
        /// Small-Scale Noise: FBM в декартовых координатах для surface detail.
        /// Добавляет scree, rock faces, micro-detail.
        /// </summary>
        private static void ApplySmallScaleNoise(
            Mesh mesh,
            MountainProfile profile,
            float baseRadius,
            float meshHeight,
            int seed)
        {
            Vector3[] vertices = mesh.vertices;
            float amplitude = profile.smallNoiseAmplitude * baseRadius;
            float frequency = profile.smallNoiseFrequency;

            if (amplitude < 0.001f) return;

            // Set seed для воспроизводимости
            NoiseUtils.SetSeed(seed);

            for (int i = 0; i < vertices.Length; i++)
            {
                float normalizedHeight = vertices[i].y / meshHeight;
                float heightFalloff = Mathf.Sin(normalizedHeight * Mathf.PI);

                // FBM в декартовых координатах
                float noiseValue = NoiseUtils.FBM(
                    vertices[i].x * frequency / baseRadius,
                    vertices[i].z * frequency / baseRadius,
                    frequency: frequency,
                    octaves: profile.smallNoiseOctaves);

                // Apply displacement along normal (up for now)
                float displacement = noiseValue * amplitude * heightFalloff;
                vertices[i].y += displacement;
            }

            mesh.vertices = vertices;
        }

        /// <summary>
        /// Ridge Noise: только Tectonic.
        /// Carves linear ridge structures into the surface.
        /// Formula: ridge = 1 - 2*|Perlin - 0.5|
        /// </summary>
        private static void ApplyRidgeNoise(
            Mesh mesh,
            MountainProfile profile,
            float baseRadius,
            float meshHeight,
            int seed)
        {
            Vector3[] vertices = mesh.vertices;
            float amplitude = profile.ridgeNoiseAmplitude * baseRadius;
            float frequency = profile.ridgeNoiseFrequency;

            if (amplitude < 0.001f) return;

            // Set seed для воспроизводимости
            NoiseUtils.SetSeed(seed);

            for (int i = 0; i < vertices.Length; i++)
            {
                float normalizedHeight = vertices[i].y / meshHeight;
                float heightFalloff = Mathf.Sin(normalizedHeight * Mathf.PI);

                // Ridge noise
                float noiseValue = NoiseUtils.RidgeNoise(
                    vertices[i].x * frequency / baseRadius,
                    vertices[i].z * frequency / baseRadius,
                    frequency: frequency,
                    octaves: 4);

                // Ridge noise carves DOWN into the surface (negative displacement)
                float displacement = -noiseValue * amplitude * heightFalloff;
                vertices[i].y += displacement;
            }

            mesh.vertices = vertices;
        }

        #endregion

        #region Deformation

        /// <summary>
        /// Asymmetry: angular variation для уникальных силуэтов.
        /// Разная высота/радиус в разных направлениях.
        /// </summary>
        private static void ApplyAsymmetry(
            Mesh mesh,
            MountainProfile profile,
            float baseRadius,
            float meshHeight,
            int seed)
        {
            Vector3[] vertices = mesh.vertices;
            float asymmetry = profile.asymmetryAmount;

            if (asymmetry < 0.001f) return;

            // Простая асимметрия: sinusoidal variation по углу
            for (int i = 0; i < vertices.Length; i++)
            {
                float angle = Mathf.Atan2(vertices[i].z, vertices[i].x);
                float normalizedHeight = vertices[i].y / meshHeight;

                // Sinusoidal height variation
                float heightVariation = Mathf.Sin(angle * 2f + seed) * asymmetry * 0.5f;
                heightVariation += Mathf.Cos(angle * 3f + seed * 0.7f) * asymmetry * 0.3f;

                // Apply only at mid-to-upper heights
                float heightFalloff = Mathf.Sin(normalizedHeight * Mathf.PI);
                vertices[i].y *= (1f + heightVariation * heightFalloff);

                // Radial variation
                float radialVariation = Mathf.Cos(angle + seed * 1.3f) * asymmetry * 0.2f;
                Vector2 horizontalPos = new Vector2(vertices[i].x, vertices[i].z);
                if (horizontalPos.sqrMagnitude > 0.001f)
                {
                    Vector2 horizontalDir = horizontalPos.normalized;
                    float radialOffset = radialVariation * baseRadius * heightFalloff;
                    vertices[i].x += horizontalDir.x * radialOffset;
                    vertices[i].z += horizontalDir.y * radialOffset;
                }
            }

            mesh.vertices = vertices;
        }

        /// <summary>
        /// Crater depression: только Volcanic.
        /// Создаёт впадину на вершине.
        /// </summary>
        private static void ApplyCrater(
            Mesh mesh,
            MountainProfile profile,
            float baseRadius,
            float meshHeight)
        {
            Vector3[] vertices = mesh.vertices;
            float craterR = profile.craterRadius * baseRadius;
            float craterD = profile.craterDepth * meshHeight;

            for (int i = 0; i < vertices.Length; i++)
            {
                float distanceFromTop = meshHeight - vertices[i].y;
                float radialDistance = new Vector2(vertices[i].x, vertices[i].z).magnitude;

                // Crater affects vertices near top and within crater radius
                if (distanceFromTop < craterD * 3f && radialDistance < craterR * 2f)
                {
                    float craterFactor = Mathf.Clamp01(1f - (radialDistance / craterR));
                    float depthFactor = Mathf.Clamp01(1f - (distanceFromTop / (craterD * 2f)));
                    float depression = craterFactor * depthFactor * craterD;

                    vertices[i].y -= depression;
                }
            }

            mesh.vertices = vertices;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Вычислить meshHeight для пика на основе его role и importance.
        /// Эверест (MainCity) = 750, мелкие пики (Farm) = 380-420.
        /// </summary>
        public static float CalculateMeshHeight(PeakData peak)
        {
            // Базовая высота из realHeightMeters (scaled)
            // Используем эмпирический множитель чтобы получить 400-800 units
            float baseHeight = peak.worldPosition.y * 6.75f;

            // Корректировка по role
            float roleMultiplier = peak.role switch
            {
                PeakRole.MainCity => 1.2f,
                PeakRole.Military => 1.0f,
                PeakRole.Navigation => 0.95f,
                PeakRole.Farm => 0.85f,
                PeakRole.Abandoned => 0.9f,
                PeakRole.Secondary => 1.0f,
                _ => 1.0f
            };

            // Clamp к разумным пределам
            float meshHeight = Mathf.Clamp(baseHeight * roleMultiplier, 350f, 800f);

            // Если peakData имеет явное meshHeight (V2), использовать его
            if (peak.meshHeight > 10f)
            {
                meshHeight = peak.meshHeight;
            }

            return meshHeight;
        }

        /// <summary>
        /// Вычислить baseRadius для пика на основе meshHeight и target h/r ratio.
        /// </summary>
        public static float CalculateBaseRadius(PeakData peak, float meshHeight)
        {
            // Target h/r ratio по типу формы
            float targetHRatio = peak.shapeType switch
            {
                PeakShapeType.Tectonic => 1.8f,
                PeakShapeType.Volcanic => 1.45f,
                PeakShapeType.Dome => 1.55f,
                PeakShapeType.Isolated => 1.79f,
                _ => 1.7f
            };

            // baseRadius = meshHeight / h/r
            float baseRadius = meshHeight / targetHRatio;

            // Если peakData имеет явный baseRadius > 50, использовать его
            if (peak.baseRadius > 50f)
            {
                baseRadius = peak.baseRadius;
            }

            return baseRadius;
        }

        #endregion
    }
}
