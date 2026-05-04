using UnityEngine;

namespace ProjectC.Core
{
    [System.Serializable]
    public class MeshEntry
    {
        public Mesh Mesh;
        [Range(1, 100)]
        public int Weight = 10;
    }

    public class NearCloudRenderer : MonoBehaviour
    {
        [Header("Settings")]
        public int CloudCount = 80;
        public float MinAltitude = 3000f;
        public float MaxAltitude = 5000f;
        public float CloudSize = 100f;
        public Material CloudMaterial;

        [Header("Mesh Variants")]
        public MeshEntry[] MeshEntries;

        [Header("Rotation Range (degrees)")]
        public Vector2 RotationX = new Vector2(0f, 0f);
        public Vector2 RotationY = new Vector2(0f, 360f);
        public Vector2 RotationZ = new Vector2(0f, 0f);

        private struct CloudData
        {
            public Matrix4x4 Matrix;
            public Vector3 Scale;
            public int MeshIndex;
        }

        private CloudData[] _clouds;
        private Mesh[] _meshes;
        private Material[] _instMaterials;
        private int _meshCount = 0;
        private int _currentCount = 0;

        private Vector3 _windDir = Vector3.right;
        private float _windSpeed = 0f;
        private System.Random _rng;

        public int ActiveCount => _currentCount;

        public void Initialize()
        {
            _rng = new System.Random(12345 + name.GetHashCode());

            _meshes = new Mesh[MeshEntries != null ? MeshEntries.Length : 1];
            _instMaterials = new Material[MeshEntries != null ? MeshEntries.Length : 1];

            if (MeshEntries != null && MeshEntries.Length > 0)
            {
                _meshCount = MeshEntries.Length;
                for (int i = 0; i < MeshEntries.Length; i++)
                {
                    if (MeshEntries[i].Mesh != null)
                    {
                        _meshes[i] = MeshEntries[i].Mesh;
                    }
                    else
                    {
                        _meshes[i] = CreateDefaultMesh();
                    }

                    if (CloudMaterial != null)
                    {
                        _instMaterials[i] = new Material(CloudMaterial);
                        _instMaterials[i].enableInstancing = true;
                    }
                }
            }
            else
            {
                _meshCount = 1;
                _meshes[0] = CreateDefaultMesh();
                if (CloudMaterial != null)
                {
                    _instMaterials[0] = new Material(CloudMaterial);
                    _instMaterials[0].enableInstancing = true;
                }
            }

            _clouds = new CloudData[CloudCount];

            Debug.Log($"[{name}] Initialized: count={CloudCount}, meshes={_meshCount}, alt={MinAltitude}-{MaxAltitude}");
        }

        public void Generate(Vector3 playerPos)
        {
            if (_clouds == null || _instMaterials == null) return;

            int totalWeight = 0;
            for (int i = 0; i < _meshCount; i++)
            {
                totalWeight += MeshEntries[i].Weight;
            }

            for (int i = 0; i < CloudCount; i++)
            {
                float angle = (float)(_rng.NextDouble() * Mathf.PI * 2f);
                float radius = (float)(_rng.NextDouble() * 5000f);

                float x = playerPos.x + Mathf.Cos(angle) * radius;
                float z = playerPos.z + Mathf.Sin(angle) * radius;
                float y = (float)(_rng.NextDouble() * (MaxAltitude - MinAltitude) + MinAltitude);

                Vector3 pos = new Vector3(x, y, z);
                float scale = CloudSize * (float)(_rng.NextDouble() * 1.0 + 0.5f);

                float rotX = Mathf.Lerp(RotationX.x, RotationX.y, (float)_rng.NextDouble());
                float rotY = Mathf.Lerp(RotationY.x, RotationY.y, (float)_rng.NextDouble());
                float rotZ = Mathf.Lerp(RotationZ.x, RotationZ.y, (float)_rng.NextDouble());
                Quaternion rot = Quaternion.Euler(rotX, rotY, rotZ);

                int meshIndex = SelectMeshByWeight(totalWeight);

                _clouds[i] = new CloudData
                {
                    Matrix = Matrix4x4.TRS(pos, rot, new Vector3(scale, scale * 0.6f, scale)),
                    Scale = new Vector3(scale, scale * 0.6f, scale),
                    MeshIndex = meshIndex
                };
            }

            _currentCount = CloudCount;
        }

        private int SelectMeshByWeight(int totalWeight)
        {
            if (_meshCount == 1) return 0;

            int r = _rng.Next(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < _meshCount; i++)
            {
                cumulative += MeshEntries[i].Weight;
                if (r < cumulative) return i;
            }
            return 0;
        }

        private void Update()
        {
            if (_currentCount == 0) return;

            Vector3 playerPos = GetPlayerPosition();
            Vector3 offset = _windDir * _windSpeed * Time.deltaTime;

            if (float.IsNaN(offset.x)) offset = Vector3.zero;

            for (int i = 0; i < _currentCount; i++)
            {
                Vector3 pos = _clouds[i].Matrix.GetColumn(3);
                pos += offset;

                if (Vector3.Distance(pos, playerPos) > 10000f)
                {
                    float angle = (float)(_rng.NextDouble() * Mathf.PI * 2);
                    float radius = (float)(_rng.NextDouble() * 4000f + 1000f);
                    float y = (float)(_rng.NextDouble() * (MaxAltitude - MinAltitude) + MinAltitude);

                    pos = new Vector3(
                        playerPos.x + Mathf.Cos(angle) * radius,
                        y,
                        playerPos.z + Mathf.Sin(angle) * radius
                    );

                    float rotX = Mathf.Lerp(RotationX.x, RotationX.y, (float)_rng.NextDouble());
                    float rotY = Mathf.Lerp(RotationY.x, RotationY.y, (float)_rng.NextDouble());
                    float rotZ = Mathf.Lerp(RotationZ.x, RotationZ.y, (float)_rng.NextDouble());
                    Quaternion rot = Quaternion.Euler(rotX, rotY, rotZ);

                    _clouds[i] = new CloudData
                    {
                        Matrix = Matrix4x4.TRS(pos, rot, _clouds[i].Scale),
                        Scale = _clouds[i].Scale,
                        MeshIndex = _clouds[i].MeshIndex
                    };
                }
                else
                {
                    var m = _clouds[i].Matrix;
                    m.SetColumn(3, pos);
                    _clouds[i] = new CloudData { Matrix = m, Scale = _clouds[i].Scale, MeshIndex = _clouds[i].MeshIndex };
                }
            }
        }

        private void LateUpdate()
        {
            if (_currentCount == 0) return;

            for (int m = 0; m < _meshCount; m++)
            {
                int count = 0;
                Matrix4x4[] matrices = new Matrix4x4[_currentCount];

                for (int i = 0; i < _currentCount; i++)
                {
                    if (_clouds[i].MeshIndex == m)
                    {
                        matrices[count++] = _clouds[i].Matrix;
                    }
                }

                if (count > 0 && _instMaterials[m] != null && _meshes[m] != null)
                {
                    Graphics.DrawMeshInstanced(
                        _meshes[m], 0, _instMaterials[m],
                        matrices, count,
                        null,
                        UnityEngine.Rendering.ShadowCastingMode.Off,
                        false
                    );
                }
            }
        }

        public void SetWind(Vector3 dir, float speed)
        {
            _windDir = dir;
            _windSpeed = speed;
        }

        private Mesh CreateDefaultMesh()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "CloudMesh";
            Bounds b = new Bounds(Vector3.zero, new Vector3(20000, 20000, 20000));
            mesh.bounds = b;
            Destroy(go);
            return mesh;
        }

        private void OnDestroy()
        {
            if (_instMaterials != null)
            {
                foreach (var mat in _instMaterials)
                {
                    if (mat != null) Destroy(mat);
                }
            }
        }

        private Vector3 GetPlayerPosition()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform.position : Vector3.zero;
        }
    }
}