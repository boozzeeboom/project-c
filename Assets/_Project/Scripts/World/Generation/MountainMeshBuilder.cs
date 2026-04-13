using UnityEngine;
using ProjectC.World.Core;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Runtime-генерация горных мешей из PeakData.
    /// 4 типа форм: Tectonic, Volcanic, Dome, Isolated.
    /// FBM noise, AnimationCurve height profile, keypoint deformation.
    /// CapsuleCollider для коллизий (НЕ MeshCollider!).
    ///
    /// СИСТЕМА КООРДИНАТ (A3 финал):
    /// - МИР: X,Z = scaled units (1:2000), Y = scaled units (meters / 100)
    /// - МЕШ: основание на Y=0, вершина на meshHeight
    /// - meshHeight = baseRadius * targetHRatio (НЕ от worldPosition.y!)
    ///
    /// ПРОПОРЦИИ:
    /// - Tectonic: h/r = 1.5 (острые, но не иглы)
    /// - Volcanic: h/r = 1.2 (пологие вулканы)
    /// - Dome: h/r = 0.8 (куполообразные)
    /// - Isolated: h/r = 1.8 (одинокие громады)
    ///
    /// ПРИМЕР Эверест:
    /// - worldPosition = (0, 88.48, 0)
    /// - baseRadius = 300, targetHRatio = 1.5
    /// - meshHeight = 300 * 1.5 = 450 units
    /// - gameObjectPosition = (0, 0, 0)
    /// - h/r = 1.5 — нормальная гора
    /// </summary>
    public class MountainMeshBuilder : MonoBehaviour
    {
        [Header("Peak Data")]
        public PeakData peakData;

        [Header("LOD Settings")]
        public int lod0Segments = 64;
        public int lod0Rings = 24;
        public int lod1Segments = 32;
        public int lod1Rings = 12;

        [Header("Noise Settings")]
        [Range(0f, 1f)]
        public float noiseWeight = 0.3f;
        public float noiseFrequency = 8f;
        public int noiseOctaves = 6;

        [Header("Materials")]
        public Material rockMaterial;
        public Material snowMaterial;
        public float snowLineY = 50f;

        [Header("Collider")]
        [Range(0.5f, 1f)]
        public float colliderRadiusRatio = 0.8f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private CapsuleCollider _capsuleCollider;

        void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _capsuleCollider = GetComponent<CapsuleCollider>();

            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (_capsuleCollider == null)
                _capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
        }

        void Start()
        {
            if (peakData != null)
            {
                BuildPeakMesh();
            }
        }

        /// <summary>
        /// Построить меш пика из PeakData.
        /// </summary>
        public void BuildPeakMesh()
        {
            if (peakData == null)
            {
                Debug.LogWarning("[MountainMeshBuilder] PeakData is null.");
                return;
            }

            // 1. Высота меша от радиуса и типа формы (НЕ от worldPosition.y!)
            float meshHeight = CalculateMeshHeight(peakData);

            // 2. Базовый меш
            Mesh mesh = GenerateBaseMesh(peakData, meshHeight);

            // 3. Height profile
            ApplyHeightProfile(mesh, peakData.heightProfile);

            // 4. Keypoint deformation
            ApplyKeypointDeformation(mesh, peakData);

            // 5. FBM noise
            ApplyNoiseDisplacement(mesh, peakData);

            // 6. Normals
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // 7. Assign mesh
            _meshFilter.mesh = mesh;

            // 8. Materials
            AssignMaterials(peakData);

            // 9. Collider
            SetupCollider(peakData, meshHeight);

            // 10. Позиция: основание на Y=0, XZ из worldPosition
            transform.position = new Vector3(
                peakData.worldPosition.x,
                0f,
                peakData.worldPosition.z
            );

            float topWorldY = meshHeight;

            Debug.Log($"[MountainMeshBuilder] {peakData.displayName} " +
                      $"({peakData.shapeType}) | baseY=0, topY={topWorldY:F1} | " +
                      $"meshH={meshHeight:F1}, r={peakData.baseRadius:F0}, h/r={meshHeight / peakData.baseRadius:F2} | " +
                      $"posXZ=({transform.position.x:F0}, {transform.position.z:F0}) | " +
                      $"{mesh.vertexCount}v, {mesh.triangles.Length / 3}t");
        }

        /// <summary>
        /// Высота меша = baseRadius * targetHRatio.
        /// НЕ зависит от worldPosition.y!
        /// </summary>
        private float CalculateMeshHeight(PeakData peak)
        {
            float hRatio = GetTargetHRatio(peak);
            return peak.baseRadius * hRatio;
        }

        /// <summary>
        /// Целевой h/r ratio по типу формы.
        /// </summary>
        private float GetTargetHRatio(PeakData peak)
        {
            // Корректировка по роли: главные пики чуть выше
            float roleMultiplier = peak.role == PeakRole.MainCity ? 1.2f : 1.0f;

            return peak.shapeType switch
            {
                PeakShapeType.Tectonic => 1.5f * roleMultiplier,   // Острые тектонические
                PeakShapeType.Volcanic => 1.2f * roleMultiplier,   // Пологие вулканы
                PeakShapeType.Dome => 0.8f * roleMultiplier,        // Купола
                PeakShapeType.Isolated => 1.8f * roleMultiplier,   // Одинокие громады
                _ => 1.5f
            };
        }

        #region Base Mesh Generation

        private Mesh GenerateBaseMesh(PeakData peak, float meshHeight)
        {
            return peak.shapeType switch
            {
                PeakShapeType.Tectonic => GenerateTectonicMesh(peak, meshHeight),
                PeakShapeType.Volcanic => GenerateVolcanicMesh(peak, meshHeight),
                PeakShapeType.Dome => GenerateDomeMesh(peak, meshHeight),
                PeakShapeType.Isolated => GenerateIsolatedMesh(peak, meshHeight),
                _ => GenerateTectonicMesh(peak, meshHeight)
            };
        }

        /// <summary>
        /// Tectonic: цилиндр с острыми гранями, ridge noise.
        /// Новый radiusFactor: пологий профиль с плато, НЕ точка на вершине.
        /// </summary>
        private Mesh GenerateTectonicMesh(PeakData peak, float meshHeight)
        {
            int segments = lod0Segments;
            int rings = lod0Rings;
            float radius = peak.baseRadius;

            return GenerateCylinderMesh(segments, rings, radius, meshHeight);
        }

        /// <summary>
        /// Volcanic: вытянутая сфера с кратером.
        /// </summary>
        private Mesh GenerateVolcanicMesh(PeakData peak, float meshHeight)
        {
            int segments = lod0Segments;
            int rings = lod0Rings;
            float radius = peak.baseRadius;

            Mesh mesh = GenerateEllipsoidMesh(segments, rings, radius, meshHeight);

            if (peak.hasCrater)
            {
                ApplyCrater(mesh, radius * 0.15f, meshHeight * 0.08f);
            }

            return mesh;
        }

        /// <summary>
        /// Dome: пологий купол.
        /// </summary>
        private Mesh GenerateDomeMesh(PeakData peak, float meshHeight)
        {
            int segments = 48;
            int rings = 12;
            float radius = peak.baseRadius;

            return GenerateDomeCapMesh(segments, rings, radius, meshHeight);
        }

        /// <summary>
        /// Isolated: конус с широкой базой, крутая вершина.
        /// Новый radiusFactor: линейное сужение с резкой вершиной.
        /// </summary>
        private Mesh GenerateIsolatedMesh(PeakData peak, float meshHeight)
        {
            int segments = lod0Segments;
            int rings = lod0Rings;
            float radius = peak.baseRadius;

            return GenerateConeMesh(segments, rings, radius, meshHeight);
        }

        #endregion

        #region Mesh Primitives

        /// <summary>
        /// Цилиндр с пологим профилем (НЕ сходится в точку).
        /// radiusFactor: 1.0 → 0.15 (острая, но не игла)
        /// </summary>
        private Mesh GenerateCylinderMesh(int segments, int rings, float baseRadius, float height)
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
                float t = (float)ring / rings;
                float y = t * height;

                // НОВЫЙ профиль: пологое сужение с плато
                // t=0: 1.0, t=0.5: 0.61, t=1.0: 0.15
                float radiusFactor = Mathf.Lerp(1f, 0.15f, Mathf.Pow(t, 0.5f));
                float radius = baseRadius * radiusFactor;

                for (int seg = 0; seg <= segments; seg++)
                {
                    int index = ring * (segments + 1) + seg;
                    float angle = seg * angleStep;

                    vertices[index] = new Vector3(
                        Mathf.Cos(angle) * radius,
                        y,
                        Mathf.Sin(angle) * radius
                    );

                    normals[index] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
                    uv[index] = new Vector2((float)seg / segments, t);
                }
            }

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

        /// <summary>
        /// Эллипсоид (полусфера).
        /// </summary>
        private Mesh GenerateEllipsoidMesh(int segments, int rings, float baseRadius, float height)
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
                float t = (float)ring / rings;
                float phi = t * Mathf.PI;

                float y = Mathf.Cos(phi) * (height * 0.5f) + (height * 0.5f);
                float radiusAtY = Mathf.Sin(phi) * baseRadius;

                for (int seg = 0; seg <= segments; seg++)
                {
                    int index = ring * (segments + 1) + seg;
                    float theta = seg * angleStep;

                    vertices[index] = new Vector3(
                        Mathf.Cos(theta) * radiusAtY,
                        y,
                        Mathf.Sin(theta) * radiusAtY
                    );

                    normals[index] = new Vector3(
                        Mathf.Cos(theta) * Mathf.Sin(phi),
                        Mathf.Cos(phi),
                        Mathf.Sin(theta) * Mathf.Sin(phi)
                    ).normalized;

                    uv[index] = new Vector2((float)seg / segments, t);
                }
            }

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

        /// <summary>
        /// Купол — только верхняя полусфера.
        /// </summary>
        private Mesh GenerateDomeCapMesh(int segments, int rings, float baseRadius, float height)
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
                float t = (float)ring / rings;
                float phi = t * (Mathf.PI * 0.5f);

                float y = Mathf.Sin(phi) * (height * 0.6f);
                float radiusAtY = Mathf.Cos(phi) * baseRadius;

                for (int seg = 0; seg <= segments; seg++)
                {
                    int index = ring * (segments + 1) + seg;
                    float theta = seg * angleStep;

                    vertices[index] = new Vector3(
                        Mathf.Cos(theta) * radiusAtY,
                        y,
                        Mathf.Sin(theta) * radiusAtY
                    );

                    normals[index] = new Vector3(
                        Mathf.Cos(theta) * Mathf.Cos(phi),
                        Mathf.Sin(phi),
                        Mathf.Sin(theta) * Mathf.Cos(phi)
                    ).normalized;

                    uv[index] = new Vector2((float)seg / segments, t);
                }
            }

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

        /// <summary>
        /// Конус: линейное сужение с резкой вершиной.
        /// radiusFactor: 1.0 → 0.1
        /// </summary>
        private Mesh GenerateConeMesh(int segments, int rings, float baseRadius, float height)
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
                float t = (float)ring / rings;
                float y = t * height;

                // НОВЫЙ профиль: линейное сужение с резкой вершиной
                // t=0: 1.0, t=0.5: 0.525, t=1.0: 0.1
                float radiusFactor = Mathf.Lerp(1f, 0.1f, t * t);
                float radius = baseRadius * radiusFactor;

                for (int seg = 0; seg <= segments; seg++)
                {
                    int index = ring * (segments + 1) + seg;
                    float angle = seg * angleStep;

                    vertices[index] = new Vector3(
                        Mathf.Cos(angle) * radius,
                        y,
                        Mathf.Sin(angle) * radius
                    );

                    float normalAngle = Mathf.Atan2(baseRadius, height);
                    normals[index] = new Vector3(
                        Mathf.Cos(angle) * Mathf.Cos(normalAngle),
                        Mathf.Sin(normalAngle),
                        Mathf.Sin(angle) * Mathf.Cos(normalAngle)
                    ).normalized;

                    uv[index] = new Vector2((float)seg / segments, t);
                }
            }

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

        #region Mesh Deformation

        private void ApplyHeightProfile(Mesh mesh, AnimationCurve profile)
        {
            if (profile == null || profile.length == 0)
                return;

            Vector3[] vertices = mesh.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                float peakHeight = mesh.bounds.size.y;
                float normalizedRadius = peakHeight > 0
                    ? Mathf.Clamp01(vertices[i].y / peakHeight)
                    : 0f;

                float heightMultiplier = profile.Evaluate(normalizedRadius);
                vertices[i].y *= heightMultiplier;
            }

            mesh.vertices = vertices;
        }

        private void ApplyKeypointDeformation(Mesh mesh, PeakData peak)
        {
            if (peak.keypoints == null || peak.keypoints.Count == 0)
                return;

            Vector3[] vertices = mesh.vertices;
            float peakHeight = mesh.bounds.size.y;
            float baseRadius = peak.baseRadius;

            foreach (var kp in peak.keypoints)
            {
                float targetRadius = kp.normalizedRadius * baseRadius;
                float targetHeight = kp.normalizedHeight * peakHeight;
                float weight = kp.noiseWeight;

                for (int i = 0; i < vertices.Length; i++)
                {
                    float vertexRadius = new Vector2(vertices[i].x, vertices[i].z).magnitude;
                    float vertexHeight = vertices[i].y;

                    float distRadius = Mathf.Abs(vertexRadius - targetRadius);
                    float distHeight = Mathf.Abs(vertexHeight - targetHeight);

                    float radiusInfluence = Mathf.Clamp01(1f - (distRadius / (baseRadius * 0.3f)));
                    float heightInfluence = Mathf.Clamp01(1f - (distHeight / (peakHeight * 0.2f)));
                    float influence = radiusInfluence * heightInfluence;

                    float displacement = weight * influence * (peakHeight * 0.05f);
                    vertices[i].y += displacement;
                }
            }

            mesh.vertices = vertices;
        }

        private void ApplyNoiseDisplacement(Mesh mesh, PeakData peak)
        {
            Vector3[] vertices = mesh.vertices;
            float baseRadius = peak.baseRadius;
            float peakHeight = mesh.bounds.size.y;

            float frequency = peak.shapeType switch
            {
                PeakShapeType.Tectonic => 8f,
                PeakShapeType.Volcanic => 4f,
                PeakShapeType.Dome => 3f,
                PeakShapeType.Isolated => 6f,
                _ => 6f
            };

            float amplitude = peak.baseRadius * 0.05f * noiseWeight;
            bool useRidge = peak.shapeType == PeakShapeType.Tectonic;

            for (int i = 0; i < vertices.Length; i++)
            {
                float normalizedHeight = Mathf.Clamp01(vertices[i].y / peakHeight);

                float noiseValue = useRidge
                    ? NoiseUtils.RidgeNoise(
                        vertices[i].x * frequency / baseRadius,
                        vertices[i].z * frequency / baseRadius,
                        frequency: frequency * 0.1f,
                        octaves: noiseOctaves)
                    : NoiseUtils.FBM(
                        vertices[i].x * frequency / baseRadius,
                        vertices[i].z * frequency / baseRadius,
                        frequency: frequency * 0.1f,
                        octaves: noiseOctaves);

                float heightFactor = Mathf.Sin(normalizedHeight * Mathf.PI);
                float displacement = noiseValue * amplitude * heightFactor;

                Vector3 normal = mesh.normals.Length > i ? mesh.normals[i] : Vector3.up;
                vertices[i] += normal * displacement;
            }

            mesh.vertices = vertices;
        }

        private void ApplyCrater(Mesh mesh, float craterRadius, float craterDepth)
        {
            Vector3[] vertices = mesh.vertices;
            float peakHeight = mesh.bounds.size.y;

            for (int i = 0; i < vertices.Length; i++)
            {
                float distanceFromTop = peakHeight - vertices[i].y;
                float radialDistance = new Vector2(vertices[i].x, vertices[i].z).magnitude;

                if (distanceFromTop < craterDepth * 3f && radialDistance < craterRadius * 2f)
                {
                    float craterFactor = Mathf.Clamp01(1f - (radialDistance / craterRadius));
                    float depthFactor = Mathf.Clamp01(1f - (distanceFromTop / craterDepth));
                    float depression = craterFactor * depthFactor * craterDepth;

                    vertices[i].y -= depression;
                }
            }

            mesh.vertices = vertices;
        }

        #endregion

        #region Materials & Collider

        private void AssignMaterials(PeakData peak)
        {
            float actualSnowLine = peak.snowLineY > 0 ? peak.snowLineY : snowLineY;
            bool hasSnow = peak.hasSnowCap || peak.worldPosition.y > actualSnowLine;

            if (hasSnow && snowMaterial != null && rockMaterial != null)
            {
                _meshRenderer.material = rockMaterial;
            }
            else if (rockMaterial != null)
            {
                _meshRenderer.material = rockMaterial;
            }
        }

        private void SetupCollider(PeakData peak, float meshHeight)
        {
            _capsuleCollider.direction = 1;
            _capsuleCollider.height = meshHeight * 0.9f;
            _capsuleCollider.radius = peak.baseRadius * colliderRadiusRatio;
            _capsuleCollider.center = new Vector3(0, meshHeight * 0.45f, 0);
            _capsuleCollider.isTrigger = false;
        }

        #endregion

        #region LOD Support

        public Mesh GenerateLOD1Mesh()
        {
            if (peakData == null) return null;

            float meshHeight = CalculateMeshHeight(peakData);

            int savedSegments = lod0Segments;
            int savedRings = lod0Rings;

            lod0Segments = lod1Segments;
            lod0Rings = lod1Rings;

            Mesh lod1Mesh = GenerateBaseMesh(peakData, meshHeight);
            ApplyHeightProfile(lod1Mesh, peakData.heightProfile);
            ApplyNoiseDisplacement(lod1Mesh, peakData);
            lod1Mesh.RecalculateNormals();
            lod1Mesh.RecalculateBounds();

            lod0Segments = savedSegments;
            lod0Rings = savedRings;

            return lod1Mesh;
        }

        #endregion
    }
}
