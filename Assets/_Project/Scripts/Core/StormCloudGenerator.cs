using UnityEngine;

namespace ProjectC.Core
{
    [System.Serializable]
    public class CloudMeshEntry
    {
        public Mesh Mesh;
        [Range(1, 100)]
        public int Weight = 10;
    }

    public class StormCloudGenerator : MonoBehaviour
    {
        [Header("Column Settings")]
        [Range(400f, 3000f)]
        public float MinHeight = 400f;
        [Range(400f, 3000f)]
        public float MaxHeight = 3000f;
        public int LayerCount = 5;
        public float ColumnRadius = 200f;
        public float PuffSpread = 1.5f;

        [Header("Main Puffs (Column)")]
        public CloudMeshEntry[] MainMeshes;
        public Vector2 MainPuffsPerLayer = new Vector2(3f, 6f);

        [Header("Offshoot Puffs (Smaller)")]
        public CloudMeshEntry[] OffshootMeshes;
        public Vector2 OffshootPuffsPerLayer = new Vector2(2f, 5f);
        public float OffshootScale = 0.4f;

        [Header("Rotation Range")]
        public Vector2 RotationX = new Vector2(0f, 30f);
        public Vector2 RotationY = new Vector2(0f, 360f);
        public Vector2 RotationZ = new Vector2(0f, 30f);

        [Header("Wind")]
        public float WindInfluence = 0.3f;

        private struct PuffData
        {
            public Matrix4x4 Matrix;
            public Vector3 BasePosition;
            public float YOffset;
            public int MeshIndex;
            public float Scale;
        }

        private PuffData[] _mainPuffs;
        private PuffData[] _offshootPuffs;
        private Mesh[] _mainMeshes;
        private Mesh[] _offshootMeshes;
        private Material[] _mainMaterials;
        private Material[] _offshootMaterials;
        private int _mainCount = 0;
        private int _offshootCount = 0;
        private int _mainMeshCount = 0;
        private int _offshootMeshCount = 0;

        private Vector3 _windDir = Vector3.right;
        private float _windSpeed = 0f;
        private System.Random _rng;

        public void Initialize(Material cloudMaterial)
        {
            Debug.Log($"[StormCloudGenerator] Initialize START for {name}");
            try
            {
            _rng = new System.Random(67890 + name.GetHashCode());

            int mainLen = MainMeshes != null ? MainMeshes.Length : 0;
            int offshootLen = OffshootMeshes != null ? OffshootMeshes.Length : 0;
            Debug.Log($"[StormCloudGenerator] mainLen={mainLen}, offshootLen={offshootLen}");

            _mainMeshCount = mainLen > 0 ? mainLen : 1;
            _offshootMeshCount = offshootLen > 0 ? offshootLen : 1;
            Debug.Log($"[StormCloudGenerator] _mainMeshCount={_mainMeshCount}, _offshootMeshCount={_offshootMeshCount}");

            _mainMeshes = new Mesh[_mainMeshCount];
            _offshootMeshes = new Mesh[_offshootMeshCount];
            _mainMaterials = new Material[_mainMeshCount];
            _offshootMaterials = new Material[_offshootMeshCount];
            Debug.Log($"[StormCloudGenerator] Meshes/materials arrays created");

            if (mainLen > 0)
            {
                for (int i = 0; i < mainLen; i++)
                {
                    Debug.Log($"[StormCloudGenerator] MainMeshes[{i}] = {(MainMeshes[i] == null ? "NULL" : "OK")}");
                    Debug.Log($"[StormCloudGenerator] Setting main mesh {i}");
                    _mainMeshes[i] = MainMeshes[i].Mesh != null ? MainMeshes[i].Mesh : CreateDefaultMesh();
                    if (cloudMaterial != null)
                    {
                        _mainMaterials[i] = new Material(cloudMaterial);
                        _mainMaterials[i].enableInstancing = true;
                    }
                }
            }
            else
            {
                Debug.Log($"[StormCloudGenerator] Setting default main mesh");
                _mainMeshes[0] = CreateDefaultMesh();
                if (cloudMaterial != null)
                {
                    _mainMaterials[0] = new Material(cloudMaterial);
                    _mainMaterials[0].enableInstancing = true;
                }
            }

            if (offshootLen > 0)
            {
                for (int i = 0; i < offshootLen; i++)
                {
                    Debug.Log($"[StormCloudGenerator] Setting offshoot mesh {i}");
                    _offshootMeshes[i] = OffshootMeshes[i].Mesh != null ? OffshootMeshes[i].Mesh : CreateDefaultMesh();
                    if (cloudMaterial != null)
                    {
                        _offshootMaterials[i] = new Material(cloudMaterial);
                        _offshootMaterials[i].enableInstancing = true;
                    }
                }
            }
            else
            {
                Debug.Log($"[StormCloudGenerator] Setting default offshoot mesh");
                _offshootMeshes[0] = CreateDefaultMesh();
                if (cloudMaterial != null)
                {
                    _offshootMaterials[0] = new Material(cloudMaterial);
                    _offshootMaterials[0].enableInstancing = true;
                }
            }

            Debug.Log($"[StormCloudGenerator] About to call GeneratePuffs");
            GeneratePuffs();
            Debug.Log($"[StormCloudGenerator] GeneratePuffs returned");

            Debug.Log($"[StormCloudGenerator] {name} initialized: mainPuffs={_mainCount}, offshootPuffs={_offshootCount}, mainMeshes={_mainMeshCount}, offshootMeshes={_offshootMeshCount}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StormCloudGenerator] Initialize exception: {e}");
            }
        }

        private int InitializeMeshes(CloudMeshEntry[] entries, Mesh[] meshes, Material[] materials, Material baseMaterial, bool isMain)
        {
            int count = 1;
            if (entries != null && entries.Length > 0)
            {
                count = entries.Length;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Mesh != null)
                    {
                        meshes[i] = entries[i].Mesh;
                    }
                    else
                    {
                        meshes[i] = CreateDefaultMesh();
                    }

                    if (baseMaterial != null)
                    {
                        materials[i] = new Material(baseMaterial);
                        materials[i].enableInstancing = true;
                    }
                }
            }
            else
            {
                meshes[0] = CreateDefaultMesh();
                if (baseMaterial != null)
                {
                    materials[0] = new Material(baseMaterial);
                    materials[0].enableInstancing = true;
                }
            }
            return count;
        }

        public void GeneratePuffs()
        {
            Debug.Log($"[StormCloudGenerator] GeneratePuffs START");
            try
            {
            float totalHeight = Random.Range(MinHeight, MaxHeight);
            int layerCountSafe = LayerCount > 1 ? LayerCount : 1;

            Debug.Log($"[StormCloudGenerator] LayerCount={LayerCount}, layerCountSafe={layerCountSafe}, totalHeight={totalHeight}");
            Debug.Log($"[StormCloudGenerator] MainPuffsPerLayer={MainPuffsPerLayer}, OffshootPuffsPerLayer={OffshootPuffsPerLayer}");

            _mainPuffs = new PuffData[LayerCount * 10];
            _offshootPuffs = new PuffData[LayerCount * 10];

            Debug.Log($"[StormCloudGenerator] Arrays created: main size={LayerCount * 10}, offshoot size={LayerCount * 10}");

            try
            {
            int mainIdx = 0;
            int offshootIdx = 0;
            float prevY = 0f;

            for (int layer = 0; layer < layerCountSafe; layer++)
            {
                Debug.Log($"[StormCloudGenerator] Layer {layer} START");
                float yRatio = layerCountSafe > 1 ? (float)layer / (layerCountSafe - 1) : 0f;
                float layerY = Mathf.Lerp(0, totalHeight, yRatio);
                float radiusAtLayer = ColumnRadius * (1f + yRatio * 0.5f);
                float puffScale = Mathf.Lerp(0.6f, 1f, yRatio);

                int mainInLayer = Mathf.RoundToInt(Random.Range(MainPuffsPerLayer.x, MainPuffsPerLayer.y));
                int offshootInLayer = Mathf.RoundToInt(Random.Range(OffshootPuffsPerLayer.x, OffshootPuffsPerLayer.y));

                Debug.Log($"[StormCloudGenerator] Layer {layer}: mainInLayer={mainInLayer}, offshootInLayer={offshootInLayer}, layerY={layerY}");

                for (int i = 0; i < mainInLayer; i++)
                {
                    if (mainIdx >= _mainPuffs.Length)
                    {
                        Debug.LogWarning($"[StormCloudGenerator] mainIdx={mainIdx} >= _mainPuffs.Length={_mainPuffs.Length}");
                        break;
                    }
                    float angle = (float)(_rng.NextDouble() * Mathf.PI * 2f);
                    float radius = (float)(_rng.NextDouble() * radiusAtLayer);
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;

                    int meshIdx = SelectMeshByWeight(MainMeshes, _mainMeshCount);

                    _mainPuffs[mainIdx++] = new PuffData
                    {
                        BasePosition = new Vector3(x, layerY, z),
                        YOffset = layerY,
                        MeshIndex = meshIdx,
                        Scale = puffScale
                    };
                }
                Debug.Log($"[StormCloudGenerator] Layer {layer}: main loop done, mainIdx={mainIdx}");

                for (int i = 0; i < offshootInLayer; i++)
                {
                    if (offshootIdx >= _offshootPuffs.Length)
                    {
                        Debug.LogWarning($"[StormCloudGenerator] offshootIdx={offshootIdx} >= _offshootPuffs.Length={_offshootPuffs.Length}");
                        break;
                    }
                    float angle = (float)(_rng.NextDouble() * Mathf.PI * 2f);
                    float radius = (float)(_rng.NextDouble() * radiusAtLayer * PuffSpread);
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;

                    int meshIdx = SelectMeshByWeight(OffshootMeshes, _offshootMeshCount);

                    _offshootPuffs[offshootIdx++] = new PuffData
                    {
                        BasePosition = new Vector3(x, layerY, z),
                        YOffset = layerY,
                        MeshIndex = meshIdx,
                        Scale = puffScale * OffshootScale
                    };
                }
                Debug.Log($"[StormCloudGenerator] Layer {layer}: offshoot loop done, offshootIdx={offshootIdx}");

                prevY = layerY;
            }

            _mainCount = mainIdx;
            _offshootCount = offshootIdx;
            Debug.Log($"[StormCloudGenerator] GeneratePuffs END: mainCount={mainIdx}, offshootCount={offshootIdx}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StormCloudGenerator] Inner exception: {e}");
            }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StormCloudGenerator] GeneratePuffs exception: {e}");
            }
        }

        private int SelectMeshByWeight(CloudMeshEntry[] entries, int meshCount)
        {
            Debug.Log($"[StormCloudGenerator] SelectMeshByWeight: entries={(entries == null ? "null" : entries.Length.ToString())}, meshCount={meshCount}");
            if (entries == null || entries.Length == 0) return 0;
            if (meshCount <= 0) return 0;

            int totalWeight = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                Debug.Log($"[StormCloudGenerator] Weight[{i}]={entries[i].Weight}, Mesh={entries[i].Mesh}");
                totalWeight += entries[i].Weight;
            }

            if (totalWeight <= 0) return 0;

            int r = _rng.Next(totalWeight);
            Debug.Log($"[StormCloudGenerator] Random r={r}, totalWeight={totalWeight}");
            int cumulative = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                cumulative += entries[i].Weight;
                Debug.Log($"[StormCloudGenerator] Cumulative[{i}]={cumulative}, r={r}, returning {i}");
                if (r < cumulative) return i;
            }
            return 0;
        }

        public void SetWind(Vector3 dir, float speed)
        {
            _windDir = dir;
            _windSpeed = speed;
        }

        private void Update()
        {
            if (_mainCount == 0) return;

            Vector3 offset = _windDir * _windSpeed * Time.deltaTime * WindInfluence;
            if (float.IsNaN(offset.x)) offset = Vector3.zero;

            for (int i = 0; i < _mainCount; i++)
            {
                var m = Matrix4x4.identity;
                Vector3 pos = _mainPuffs[i].BasePosition + offset;
                float scale = ColumnRadius * _mainPuffs[i].Scale;
                m.SetColumn(3, pos);
                m = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scale, scale * 0.7f, scale));
                _mainPuffs[i].Matrix = m;
            }

            for (int i = 0; i < _offshootCount; i++)
            {
                var m = Matrix4x4.identity;
                Vector3 pos = _offshootPuffs[i].BasePosition + offset;
                float scale = ColumnRadius * _offshootPuffs[i].Scale;
                m = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scale, scale * 0.7f, scale));
                _offshootPuffs[i].Matrix = m;
            }
        }

        private void LateUpdate()
        {
            if (_mainCount == 0) return;

            DrawPuffs(_mainPuffs, _mainCount, _mainMeshes, _mainMaterials, _mainMeshCount);
            DrawPuffs(_offshootPuffs, _offshootCount, _offshootMeshes, _offshootMaterials, _offshootMeshCount);
        }

        private void DrawPuffs(PuffData[] puffs, int count, Mesh[] meshes, Material[] materials, int meshCount)
        {
            for (int m = 0; m < meshCount; m++)
            {
                int drawCount = 0;
                Matrix4x4[] matrices = new Matrix4x4[count];

                for (int i = 0; i < count; i++)
                {
                    if (puffs[i].MeshIndex == m)
                    {
                        matrices[drawCount++] = puffs[i].Matrix;
                    }
                }

                if (drawCount > 0 && materials[m] != null && meshes[m] != null)
                {
                    Graphics.DrawMeshInstanced(
                        meshes[m], 0, materials[m],
                        matrices, drawCount,
                        null,
                        UnityEngine.Rendering.ShadowCastingMode.Off,
                        false
                    );
                }
            }
        }

        private Mesh CreateDefaultMesh()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "StormMesh";
            Bounds b = new Bounds(Vector3.zero, new Vector3(20000, 20000, 20000));
            mesh.bounds = b;
            Destroy(go);
            return mesh;
        }

        private void OnDestroy()
        {
            if (_mainMaterials != null)
            {
                foreach (var mat in _mainMaterials)
                    if (mat != null) Destroy(mat);
            }
            if (_offshootMaterials != null)
            {
                foreach (var mat in _offshootMaterials)
                    if (mat != null) Destroy(mat);
            }
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)(_rng.NextDouble() * (max - min));
        }
    }
}