using System.Collections.Generic;
using UnityEngine;
using ProjectC.CloudGenerator;

namespace ProjectC.Core
{
    public class StormCloudGenerator : MonoBehaviour
    {
        [Header("Pool Settings")]
        public int MaxActiveStorms = 5;

        [Header("Cloud Material")]
        public Material cloudMaterial;

        [Header("Default Storm Pattern")]
        public CloudLayerConfig defaultStormPattern;

        private Dictionary<uint, Storm> _activeStorms = new Dictionary<uint, Storm>();
        private Mesh _sphereMesh;

        public void Awake()
        {
            _sphereMesh = CreateDefaultSphereMesh();
            Debug.Log($"[StormCloudGenerator] Awake. Sphere mesh: {_sphereMesh.name}, defaultStormPattern: {defaultStormPattern}");
        }

        public void SpawnStorm(uint stormId, Vector3 position, CloudLayerConfig pattern, float intensity, GameObject existingRoot = null)
        {
            Debug.Log($"[StormCloudGenerator] SpawnStorm called: id={stormId}, pos={position}, pattern={pattern?.name}, intensity={intensity}");

            if (_activeStorms.Count >= MaxActiveStorms)
            {
                uint oldestId = 0;
                foreach (var kvp in _activeStorms)
                {
                    oldestId = kvp.Key;
                    break;
                }
                DespawnStorm(oldestId);
            }

            if (_activeStorms.ContainsKey(stormId))
            {
                DespawnStorm(stormId);
            }

            GameObject stormRoot = existingRoot != null
                ? existingRoot
                : new GameObject($"Storm_{stormId}");

            if (existingRoot == null)
            {
                stormRoot.transform.position = position;
            }
            else
            {
                Debug.Log($"[StormCloudGenerator] Using existing root at world pos {stormRoot.transform.position}");
            }

            var sphereContainer = new GameObject("SphereContainer");
            sphereContainer.transform.SetParent(stormRoot.transform);
            sphereContainer.transform.localPosition = Vector3.zero;
            Debug.Log($"[StormCloudGenerator] SphereContainer local pos: {sphereContainer.transform.localPosition}, world pos: {sphereContainer.transform.position}");

            var spheres = GenerateStormSpheres(pattern, Vector3.zero);
            Debug.Log($"[StormCloudGenerator] Generated {spheres?.Count ?? 0} spheres for storm {stormId}");

            if (spheres == null || spheres.Count == 0)
            {
                Debug.LogError($"[StormCloudGenerator] No spheres generated! Pattern: {pattern?.name}, Archetype: {pattern?.archetype}");
                if (existingRoot == null) Destroy(stormRoot);
                return;
            }

            foreach (var sphere in spheres)
            {
                CreateStormSphere(sphere, sphereContainer.transform, intensity);
            }

            var storm = new Storm
            {
                StormId = stormId,
                Root = stormRoot,
                SphereContainer = sphereContainer,
                Pattern = pattern,
                Intensity = intensity
            };

            _activeStorms[stormId] = storm;
            Debug.Log($"[StormCloudGenerator] Storm {stormId} spawned successfully with {spheres.Count} spheres");
        }

        public void DespawnStorm(uint stormId)
        {
            if (_activeStorms.TryGetValue(stormId, out var storm))
            {
                if (storm.Root != null)
                {
                    Destroy(storm.Root);
                }
                _activeStorms.Remove(stormId);
            }
        }

        public void DespawnAll()
        {
            foreach (var storm in _activeStorms.Values)
            {
                if (storm.Root != null)
                {
                    Destroy(storm.Root);
                }
            }
            _activeStorms.Clear();
        }

        private List<CloudSphere> GenerateStormSpheres(CloudLayerConfig config, Vector3 offset)
        {
            Debug.Log($"[StormCloudGenerator] GenerateStormSpheres: config={config?.name}, archetype={config?.archetype}, seed={config?.generatorSeed}");
            Debug.Log($"[StormCloudGenerator]   density={config?.density}, cloudSize={config?.cloudSize}");

            var layerConfig = new ProjectC.CloudGenerator.CloudLayerConfig
            {
                Archetype = config.archetype,
                Enabled = true,
                YOffset = 0f,
                Seed = config.generatorSeed,
                Density = config.density,
                Jitter = config.jitter,
                Clustering = config.clustering,
                PositionVariation = config.positionVariation,
                NoiseSalt = 0,
                CondensationLevel = -999,

                CloudSize = config.cloudSize,
                CascadeDepth = config.cascadeDepth,
                BumpsPerLevel = config.bumpsPerLevel,
                ChildRatio = config.childRatio,
                SizeVariation = config.sizeVariationGen,
                ParentCount = config.parentCount,
                EllipsoidY = config.ellipsoidY,
                EllipsoidXZ = config.ellipsoidXZ,
                MaxSphereCount = config.maxSphereCount,
                SphereCountScale = config.sphereCountScale,

                ParentMeshPath = config.parentMeshPath,
                ParentMeshScaleX = config.parentMeshScaleX,
                ParentMeshScaleY = config.parentMeshScaleY,
                ParentMeshScaleZ = config.parentMeshScaleZ,
                ParentMeshRotX = config.parentMeshRotX,
                ParentMeshRotY = config.parentMeshRotY,
                ParentMeshRotZ = config.parentMeshRotZ,
                ParentMeshOffsetX = config.parentMeshOffsetX,
                ParentMeshOffsetY = config.parentMeshOffsetY,
                ParentMeshOffsetZ = config.parentMeshOffsetZ,

                SizeRange = new ProjectC.CloudGenerator.SizeRange
                {
                    Min = config.sizeRange.Min,
                    Max = config.sizeRange.Max
                },
                ColumnParams = new ProjectC.CloudGenerator.ColumnParams
                {
                    Height = config.columnParams.Height,
                    BaseRadius = config.columnParams.BaseRadius,
                    TopRadius = config.columnParams.TopRadius,
                    Floors = config.columnParams.Floors,
                    RingsPerFloor = config.columnParams.RingsPerFloor,
                    Wobble = config.columnParams.Wobble
                },
                PlatformParams = new ProjectC.CloudGenerator.PlatformParams
                {
                    Width = config.platformParams.Width,
                    Depth = config.platformParams.Depth,
                    CenterThickness = config.platformParams.CenterThickness,
                    EdgeThickness = config.platformParams.EdgeThickness,
                    InteriorDensity = config.platformParams.InteriorDensity,
                    EdgeRings = config.platformParams.EdgeRings
                },
                TreeParams = new ProjectC.CloudGenerator.TreeParams
                {
                    BaseRadius = config.treeParams.BaseRadius,
                    MaxDepth = config.treeParams.MaxDepth,
                    BranchElongation = config.treeParams.BranchElongation,
                    TaperRatio = config.treeParams.TaperRatio,
                    BranchAngle = config.treeParams.BranchAngle,
                    BranchProbability = config.treeParams.BranchProbability,
                    TrunkUpBias = config.treeParams.TrunkUpBias,
                    LengthFalloff = config.treeParams.LengthFalloff,
                    ThicknessFalloff = config.treeParams.ThicknessFalloff
                }
            };

            var layers = new List<ProjectC.CloudGenerator.CloudLayerConfig> { layerConfig };
            var spheres = ProjectC.CloudGenerator.CloudGenerator.Generate(layers);

            Debug.Log($"[StormCloudGenerator] GenerateStormSpheres: {spheres.Count} spheres generated for pattern {config?.name}");

            foreach (var sphere in spheres)
            {
                sphere.X += offset.x;
                sphere.Y += offset.y;
                sphere.Z += offset.z;
            }

            return spheres;
        }

        private void CreateStormSphere(CloudSphere sphere, Transform container, float intensity)
        {
            var go = new GameObject($"StormSphere_{sphere.Depth}");
            go.transform.SetParent(container, false);
            go.transform.SetLocalPositionAndRotation(
                new Vector3(sphere.X, sphere.Y, sphere.Z),
                Quaternion.identity
            );

            go.AddComponent<MeshFilter>().mesh = _sphereMesh;

            var renderer = go.AddComponent<MeshRenderer>();
            var mat = cloudMaterial != null ? cloudMaterial : CreateDefaultMaterial();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float scale = sphere.Radius * 2f * intensity * 30f;
            go.transform.localScale = new Vector3(scale, scale, scale);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var physics = go.AddComponent<CloudSpherePhysics>();
            physics.Initialize(sphere.Radius * intensity);
        }

        private Mesh CreateDefaultSphereMesh()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "StormSphereMesh";
            Bounds b = new Bounds(Vector3.zero, new Vector3(20000, 20000, 20000));
            mesh.bounds = b;
            Destroy(go);
            return mesh;
        }

        private Material CreateDefaultMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(1f, 1f, 1f, 0.9f);
            return mat;
        }

        private void OnDestroy()
        {
            DespawnAll();
        }

        public class Storm
        {
            public uint StormId;
            public GameObject Root;
            public GameObject SphereContainer;
            public CloudLayerConfig Pattern;
            public float Intensity;
        }
    }
}