using UnityEngine;
using ProjectC.World.Core;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Runtime component для генерации горных мешей V2 (ADR-0001).
    /// Заменяет MountainMeshBuilder (V1).
    /// 
    /// Использование:
    /// 1. Добавить компонент на GameObject
    /// 2. Assign PeakData
    /// 3. Assign materials
    /// 4. Вызвать BuildPeakMesh() или дождать Start()
    /// 
    /// Отличия от V1:
    /// - Использует MountainMeshGenerator (Power-Law Cone + noise)
    /// - MeshCollider вместо CapsuleCollider
    /// - Явные meshHeight/baseRadius (НЕ формулы!)
    /// - MountainProfile для типа формы
    /// </summary>
    public class MountainMeshBuilderV2 : MonoBehaviour
    {
        [Header("Peak Data")]
        public PeakData peakData;

        [Header("LOD Settings")]
        public int lod0Segments = 64;
        public int lod0Rings = 24;
        public int lod1Segments = 32;
        public int lod1Rings = 12;

        [Header("Materials")]
        public Material rockMaterial;
        public Material snowMaterial;
        public float snowLineY = 50f;

        [Header("Debug")]
        [Tooltip("Показать debug info в Start()")]
        public bool showDebugInfo = true;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;

        void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshCollider = GetComponent<MeshCollider>();

            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (_meshCollider == null)
                _meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        void Start()
        {
            if (peakData != null)
            {
                BuildPeakMesh();
            }
        }

        /// <summary>
        /// Построить меш пика из PeakData (V2).
        /// </summary>
        public void BuildPeakMesh()
        {
            if (peakData == null)
            {
                Debug.LogWarning("[MountainMeshBuilderV2] PeakData is null.");
                return;
            }

            // 1. Вычислить размеры (явные значения, НЕ формулы V1!)
            float meshHeight = MountainMeshGenerator.CalculateMeshHeight(peakData);
            float baseRadius = MountainMeshGenerator.CalculateBaseRadius(peakData, meshHeight);

            // 2. Создать MountainProfile для типа формы
            MountainProfile profile = MountainProfile.CreatePreset(peakData.shapeType);

            // Если peakData.hasCrater, включить crater в profile
            if (peakData.hasCrater)
            {
                profile.hasCrater = true;
            }

            // 3. Generate mesh (LOD0)
            Mesh mesh = MountainMeshGenerator.GenerateMountainMesh(
                profile,
                meshHeight,
                baseRadius,
                lod0Segments,
                lod0Rings,
                seed: peakData.displayName.GetHashCode());

            // 4. Assign mesh
            _meshFilter.sharedMesh = mesh;

            // 5. Materials
            AssignMaterials(peakData, meshHeight);

            // 6. MeshCollider (упрощённый меш, convex=false)
            SetupCollider(profile, meshHeight, baseRadius);

            // 7. Позиция: основание на Y=0, XZ из worldPosition
            transform.position = new Vector3(
                peakData.worldPosition.x,
                0f,
                peakData.worldPosition.z
            );

            // 8. Debug info
            if (showDebugInfo)
            {
                Debug.Log($"[MountainMeshBuilderV2] {peakData.displayName} " +
                          $"({peakData.shapeType}) | baseY=0, topY={meshHeight:F1} | " +
                          $"meshH={meshHeight:F1}, baseR={baseRadius:F1}, h/r={meshHeight / baseRadius:F2} | " +
                          $"posXZ=({transform.position.x:F0}, {transform.position.z:F0}) | " +
                          $"{mesh.vertexCount}v, {mesh.triangles.Length / 3}t");
            }
        }

        #region Materials & Collider

        private void AssignMaterials(PeakData peak, float meshHeight)
        {
            float actualSnowLine = peak.snowLineY > 0 ? peak.snowLineY : snowLineY;
            bool hasSnow = peak.hasSnowCap || peak.worldPosition.y > actualSnowLine;

            if (hasSnow && snowMaterial != null && rockMaterial != null)
            {
                // TODO: Dual material assignment (rock + snow by height)
                // For now, just use rock material
                _meshRenderer.sharedMaterial = rockMaterial;
            }
            else if (rockMaterial != null)
            {
                _meshRenderer.sharedMaterial = rockMaterial;
            }
        }

        private void SetupCollider(MountainProfile profile, float meshHeight, float baseRadius)
        {
            // Generate simplified mesh for collider (16 segments, 8 rings)
            Mesh colliderMesh = MountainMeshGenerator.GenerateColliderMesh(
                profile,
                meshHeight,
                baseRadius);

            // MeshCollider with convex=false — точное соответствие форме
            _meshCollider.sharedMesh = colliderMesh;
            _meshCollider.convex = false;
            _meshCollider.isTrigger = false;
        }

        #endregion

        #region LOD Support

        /// <summary>
        /// Сгенерировать LOD1 mesh (меньше деталей).
        /// </summary>
        public Mesh GenerateLOD1Mesh()
        {
            if (peakData == null) return null;

            float meshHeight = MountainMeshGenerator.CalculateMeshHeight(peakData);
            float baseRadius = MountainMeshGenerator.CalculateBaseRadius(peakData, meshHeight);
            MountainProfile profile = MountainProfile.CreatePreset(peakData.shapeType);

            return MountainMeshGenerator.GenerateMountainMesh(
                profile,
                meshHeight,
                baseRadius,
                lod1Segments,
                lod1Rings,
                seed: peakData.displayName.GetHashCode());
        }

        #endregion
    }
}
