using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ProjectC.World.Core;
using ProjectC.World.Generation;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor: генерация горных мешей из MountainMassif ассетов.
    /// Создаёт GameObject'ы с MountainMeshBuilder для каждого пика.
    ///
    /// Использование: Tools → Project C → Build All Mountain Meshes
    ///
    /// ПОДХОД A3 ФИНАЛ:
    /// - meshHeight = baseRadius * targetHRatio (НЕ от worldPosition.y!)
    /// - Основание на Y=0, XZ из worldPosition
    /// - Tectonic: h/r=1.5, Volcanic: h/r=1.2, Dome: h/r=0.8, Isolated: h/r=1.8
    /// </summary>
    public class MountainMassifBuilder : EditorWindow
    {
        private Vector2 _scrollPos;
        private bool _clearExisting = true;
        private Material _himalayanRock;
        private Material _alpineRock;
        private Material _africanRock;
        private Material _andeanRock;
        private Material _alaskanRock;
        private Material _snowMaterial;

        [MenuItem("Tools/Project C/Build All Mountain Meshes")]
        public static void ShowWindow()
        {
            GetWindow<MountainMassifBuilder>("Mountain Mesh Builder");
        }

        void OnEnable()
        {
            _himalayanRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_Himalayan.mat");
            _alpineRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_Alpine.mat");
            _africanRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_African.mat");
            _andeanRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_Andean.mat");
            _alaskanRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_Alaskan.mat");
            _snowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Snow_Generic.mat");
        }

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Mountain Mesh Builder (A3 Final)", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Генерирует 29 пиков из MountainMassif ассетов.\n" +
                "ПОДХОД A3: meshHeight = baseRadius * h/r ratio.\n" +
                "Tectonic: 1.5, Volcanic: 1.2, Dome: 0.8, Isolated: 1.8\n\n" +
                "Требования:\n" +
                "1. PeakData заполнены\n" +
                "2. Материалы скал созданы",
                MessageType.Info);

            EditorGUILayout.Space();

            _clearExisting = EditorGUILayout.Toggle("Удалить существующие горы", _clearExisting);

            EditorGUILayout.Space();

            _himalayanRock = (Material)EditorGUILayout.ObjectField("Himalayan", _himalayanRock, typeof(Material), false);
            _alpineRock = (Material)EditorGUILayout.ObjectField("Alpine", _alpineRock, typeof(Material), false);
            _africanRock = (Material)EditorGUILayout.ObjectField("African", _africanRock, typeof(Material), false);
            _andeanRock = (Material)EditorGUILayout.ObjectField("Andean", _andeanRock, typeof(Material), false);
            _alaskanRock = (Material)EditorGUILayout.ObjectField("Alaskan", _alaskanRock, typeof(Material), false);
            _snowMaterial = (Material)EditorGUILayout.ObjectField("Snow", _snowMaterial, typeof(Material), false);

            EditorGUILayout.Space();

            if (GUILayout.Button("Построить ВСЕ горы (29 пиков)", GUILayout.Height(40)))
            {
                BuildAllMountains();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Гималайский массив (8 пиков)"))
                BuildMassif("HimalayanMassif");

            if (GUILayout.Button("Альпийский массив (6 пиков)"))
                BuildMassif("AlpineMassif");

            if (GUILayout.Button("Африканский массив (4 пика)"))
                BuildMassif("AfricanMassif");

            if (GUILayout.Button("Андийский массив (6 пиков)"))
                BuildMassif("AndeanMassif");

            if (GUILayout.Button("Аляскинский массив (5 пиков)"))
                BuildMassif("AlaskanMassif");

            EditorGUILayout.EndScrollView();
        }

        private void BuildAllMountains()
        {
            if (_clearExisting)
            {
                ClearExistingMountains();
            }

            BuildMassif("HimalayanMassif");
            BuildMassif("AlpineMassif");
            BuildMassif("AfricanMassif");
            BuildMassif("AndeanMassif");
            BuildMassif("AlaskanMassif");

            EditorUtility.DisplayDialog("Готово!",
                "Все 29 горных мешей построены.",
                "OK");
        }

        private void ClearExistingMountains()
        {
            var mountainsRoot = GameObject.Find("Mountains");
            if (mountainsRoot != null)
            {
                for (int i = mountainsRoot.transform.childCount - 1; i >= 0; i--)
                {
                    var child = mountainsRoot.transform.GetChild(i).gameObject;
                    if (Application.isPlaying)
                        Object.Destroy(child);
                    else
                        Object.DestroyImmediate(child);
                }
                Debug.Log("[MountainMassifBuilder] Cleared existing mountains.");
            }
        }

        private void BuildMassif(string massifFileName)
        {
            var massif = FindMassif(massifFileName);
            if (massif == null)
            {
                Debug.LogError($"[MountainMassifBuilder] Massif not found: {massifFileName}");
                return;
            }

            if (massif.peaks == null || massif.peaks.Count == 0)
            {
                Debug.LogWarning($"[MountainMassifBuilder] {massif.displayName} has no peaks.");
                return;
            }

            var mountainsRoot = GameObject.Find("Mountains");
            if (mountainsRoot == null)
            {
                mountainsRoot = new GameObject("Mountains");
                Undo.RegisterCreatedObjectUndo(mountainsRoot, "Create Mountains root");
            }

            var massifRoot = new GameObject(massif.displayName);
            massifRoot.transform.SetParent(mountainsRoot.transform);
            Undo.RegisterCreatedObjectUndo(massifRoot, "Create massif root");

            Material rockMat = GetRockMaterialForMassif(massif);

            int builtCount = 0;
            foreach (var peak in massif.peaks)
            {
                if (peak == null) continue;
                BuildPeakGameObject(peak, massifRoot.transform, rockMat);
                builtCount++;
            }

            Debug.Log($"[MountainMassifBuilder] {massif.displayName}: {builtCount} peaks built.");
        }

        private void BuildPeakGameObject(PeakData peak, Transform parent, Material rockMaterial)
        {
            var peakGO = new GameObject(peak.displayName);
            peakGO.transform.SetParent(parent);

            var builder = peakGO.AddComponent<MountainMeshBuilder>();
            builder.peakData = peak;
            builder.rockMaterial = rockMaterial;
            builder.snowMaterial = _snowMaterial;
            builder.snowLineY = peak.snowLineY;
            builder.noiseWeight = 0.3f;

            Undo.RegisterCreatedObjectUndo(peakGO, $"Create {peak.displayName}");

            if (!Application.isPlaying)
            {
                BuildPeakMeshInEditor(builder, peak);
            }
        }

        private void BuildPeakMeshInEditor(MountainMeshBuilder builder, PeakData peak)
        {
            var meshFilter = builder.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = builder.gameObject.AddComponent<MeshFilter>();

            var meshRenderer = builder.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = builder.gameObject.AddComponent<MeshRenderer>();

            var capsuleCollider = builder.GetComponent<CapsuleCollider>();
            if (capsuleCollider == null)
                capsuleCollider = builder.gameObject.AddComponent<CapsuleCollider>();

            // meshHeight = baseRadius * targetHRatio (НЕ от worldPosition.y!)
            float meshHeight = CalculateMeshHeight(peak);

            Mesh mesh = GenerateMeshForPeak(peak, meshHeight, builder);

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = GetRockMaterialForPeak(peak);

            capsuleCollider.direction = 1;
            capsuleCollider.height = meshHeight * 0.9f;
            capsuleCollider.radius = peak.baseRadius * builder.colliderRadiusRatio;
            capsuleCollider.center = new Vector3(0, meshHeight * 0.45f, 0);
            capsuleCollider.isTrigger = false;

            // Основание на Y=0
            builder.transform.position = new Vector3(
                peak.worldPosition.x,
                0f,
                peak.worldPosition.z
            );

            Debug.Log($"[MountainMassifBuilder] {peak.displayName}: " +
                      $"baseY=0, topY={meshHeight:F1} | " +
                      $"meshH={meshHeight:F1}, r={peak.baseRadius:F0}, h/r={meshHeight / peak.baseRadius:F2} | " +
                      $"posXZ=({builder.transform.position.x:F0}, {builder.transform.position.z:F0}) | " +
                      $"{mesh.vertexCount}v, {mesh.triangles.Length / 3}t");
        }

        /// <summary>
        /// meshHeight = baseRadius * targetHRatio.
        /// </summary>
        private float CalculateMeshHeight(PeakData peak)
        {
            float hRatio = GetTargetHRatio(peak);
            return peak.baseRadius * hRatio;
        }

        private float GetTargetHRatio(PeakData peak)
        {
            float roleMultiplier = peak.role == PeakRole.MainCity ? 1.2f : 1.0f;

            return peak.shapeType switch
            {
                PeakShapeType.Tectonic => 1.5f * roleMultiplier,
                PeakShapeType.Volcanic => 1.2f * roleMultiplier,
                PeakShapeType.Dome => 0.8f * roleMultiplier,
                PeakShapeType.Isolated => 1.8f * roleMultiplier,
                _ => 1.5f
            };
        }

        private Mesh GenerateMeshForPeak(PeakData peak, float meshHeight, MountainMeshBuilder builder)
        {
            Mesh mesh = peak.shapeType switch
            {
                PeakShapeType.Tectonic => GenerateTectonicMesh(peak, meshHeight, builder),
                PeakShapeType.Volcanic => GenerateVolcanicMesh(peak, meshHeight, builder),
                PeakShapeType.Dome => GenerateDomeMesh(peak, meshHeight, builder),
                PeakShapeType.Isolated => GenerateIsolatedMesh(peak, meshHeight, builder),
                _ => GenerateTectonicMesh(peak, meshHeight, builder)
            };

            ApplyHeightProfile(mesh, peak.heightProfile);
            ApplyKeypointDeformation(mesh, peak);
            ApplyNoiseDisplacement(mesh, peak, builder);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        #region Mesh Generation

        private Mesh GenerateTectonicMesh(PeakData peak, float meshHeight, MountainMeshBuilder builder)
        {
            return GenerateCylinderMesh(builder.lod0Segments, builder.lod0Rings, peak.baseRadius, meshHeight);
        }

        private Mesh GenerateVolcanicMesh(PeakData peak, float meshHeight, MountainMeshBuilder builder)
        {
            Mesh mesh = GenerateEllipsoidMesh(builder.lod0Segments, builder.lod0Rings, peak.baseRadius, meshHeight);

            if (peak.hasCrater)
                ApplyCrater(mesh, peak.baseRadius * 0.15f, meshHeight * 0.08f);

            return mesh;
        }

        private Mesh GenerateDomeMesh(PeakData peak, float meshHeight, MountainMeshBuilder builder)
        {
            return GenerateDomeCapMesh(48, 12, peak.baseRadius, meshHeight);
        }

        private Mesh GenerateIsolatedMesh(PeakData peak, float meshHeight, MountainMeshBuilder builder)
        {
            return GenerateConeMesh(builder.lod0Segments, builder.lod0Rings, peak.baseRadius, meshHeight);
        }

        /// <summary>
        /// Цилиндр: пологий профиль с плато (НЕ точка на вершине).
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

                // Плавное сужение: 1.0 → 0.15
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

            return new Mesh { vertices = vertices, triangles = triangles, uv = uv, normals = normals };
        }

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

            return new Mesh { vertices = vertices, triangles = triangles, uv = uv, normals = normals };
        }

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

            return new Mesh { vertices = vertices, triangles = triangles, uv = uv, normals = normals };
        }

        /// <summary>
        /// Конус: линейное сужение с резкой вершиной.
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

                // Линейное сужение: 1.0 → 0.1
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

            return new Mesh { vertices = vertices, triangles = triangles, uv = uv, normals = normals };
        }

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

        private void ApplyNoiseDisplacement(Mesh mesh, PeakData peak, MountainMeshBuilder builder)
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

            float amplitude = baseRadius * 0.05f * builder.noiseWeight;
            bool useRidge = peak.shapeType == PeakShapeType.Tectonic;

            for (int i = 0; i < vertices.Length; i++)
            {
                float normalizedHeight = Mathf.Clamp01(vertices[i].y / peakHeight);

                float noiseValue = useRidge
                    ? NoiseUtils.RidgeNoise(
                        vertices[i].x * frequency / baseRadius,
                        vertices[i].z * frequency / baseRadius,
                        frequency: frequency * 0.1f,
                        octaves: builder.noiseOctaves)
                    : NoiseUtils.FBM(
                        vertices[i].x * frequency / baseRadius,
                        vertices[i].z * frequency / baseRadius,
                        frequency: frequency * 0.1f,
                        octaves: builder.noiseOctaves);

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

        #region Helpers

        private Material GetRockMaterialForMassif(MountainMassif massif)
        {
            return massif.displayName switch
            {
                "Himalayan" => _himalayanRock,
                "Alpine" => _alpineRock,
                "African" => _africanRock,
                "Andean" => _andeanRock,
                "Alaskan" => _alaskanRock,
                _ => _himalayanRock
            };
        }

        private Material GetRockMaterialForPeak(PeakData peak)
        {
            if (peak.rockColor != Color.grey)
            {
                // Найти ближайший массив по координатам
                var massifs = new[]
                {
                    new { name = "Himalayan", x = 0f, z = 0f, mat = _himalayanRock },
                    new { name = "Alpine", x = -1310f, z = 2810f, mat = _alpineRock },
                    new { name = "African", x = -1881f, z = -3010f, mat = _africanRock },
                    new { name = "Andean", x = -4176f, z = -2110f, mat = _andeanRock },
                    new { name = "Alaskan", x = 1255f, z = 4685f, mat = _alaskanRock }
                };

                float minDist = float.MaxValue;
                Material closestMat = _himalayanRock;

                foreach (var m in massifs)
                {
                    float dist = Mathf.Sqrt(
                        Mathf.Pow(peak.worldPosition.x - m.x, 2) +
                        Mathf.Pow(peak.worldPosition.z - m.z, 2)
                    );
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestMat = m.mat;
                    }
                }

                return closestMat;
            }

            return _himalayanRock;
        }

        private MountainMassif FindMassif(string fileName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:ScriptableObject {fileName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"{fileName}.asset"))
                    return AssetDatabase.LoadAssetAtPath<MountainMassif>(path);
            }

            string standardPath = $"Assets/_Project/Data/World/Massifs/{fileName}.asset";
            var massif = AssetDatabase.LoadAssetAtPath<MountainMassif>(standardPath);
            if (massif != null) return massif;

            Debug.LogError($"[MountainMassifBuilder] Cannot find: {fileName}");
            return null;
        }

        #endregion
    }
}
