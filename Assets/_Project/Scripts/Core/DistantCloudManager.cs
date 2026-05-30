using UnityEngine;

namespace ProjectC.Core
{
    public class DistantCloudManager : MonoBehaviour
    {
        [Header("Settings")]
        public int ImpostorCount = 140;
        public float MinDistance = 5000f;
        public float MaxDistance = 15000f;
        public float MinSize = 500f;
        public float MaxSize = 2000f;

        [Header("Textures")]
        public Texture2D[] Textures;

        [Header("Y Rotation Range (degrees around vertical axis)")]
        public Vector2 RotationRangeY = new Vector2(0f, 360f);

        [Header("Material")]
        public Material ImpostorMaterial;

        [Header("Debug Logging")]
        public bool logInitialization = false;

        private struct ImpostorData
        {
            public Matrix4x4 Matrix;
            public Vector3 Scale;
            public int TextureIndex;
        }

        private ImpostorData[] _impostors;
        private Mesh _mesh;
        private Material[] _instMaterials;
        private int _textureCount = 0;
        private int _currentCount = 0;
        private System.Random _rng;

        private Vector3 _windDir = Vector3.right;
        private float _windSpeed = 0f;

        public int ActiveCount => _currentCount;

        public void Initialize()
        {
            _rng = new System.Random(54321);

            _mesh = CreateQuadMesh();

            _textureCount = Textures != null && Textures.Length > 0 ? Textures.Length : 1;
            _instMaterials = new Material[_textureCount];

            for (int i = 0; i < _textureCount; i++)
            {
                if (ImpostorMaterial != null)
                {
                    _instMaterials[i] = new Material(ImpostorMaterial);
                    _instMaterials[i].enableInstancing = true;

                    if (i < Textures.Length && Textures[i] != null)
                    {
                        _instMaterials[i].mainTexture = Textures[i];
                        if (logInitialization) Debug.Log($"[DistantCloud] Set texture {i}: {_instMaterials[i].mainTexture} (size: {Textures[i].width}x{Textures[i].height}, format: {Textures[i].format})");
                    }
                }
                else
                {
                    if (logInitialization) Debug.LogWarning($"[DistantCloud] ImpostorMaterial is null at index {i}");
                }
            }

            _impostors = new ImpostorData[ImpostorCount];

            if (logInitialization) Debug.Log($"[{name}] Initialized: impostors={ImpostorCount}, textures={_textureCount}");
        }

        public void Generate(Vector3 playerPos)
        {
            if (_impostors == null || _instMaterials == null) return;

            for (int i = 0; i < ImpostorCount; i++)
            {
                float angle = (float)(_rng.NextDouble() * Mathf.PI * 2f);
                float dist = (float)(_rng.NextDouble() * (MaxDistance - MinDistance) + MinDistance);

                float x = playerPos.x + Mathf.Cos(angle) * dist;
                float z = playerPos.z + Mathf.Sin(angle) * dist;
                float y = playerPos.y + (float)(_rng.NextDouble() * 4000f + 2000f);

                Vector3 pos = new Vector3(x, y, z);

                float scaleX = (float)(_rng.NextDouble() * (MaxSize - MinSize) + MinSize);
                float scaleY = scaleX * (float)(_rng.NextDouble() * 0.5f + 0.3f);

                float rotY = Mathf.Lerp(RotationRangeY.x, RotationRangeY.y, (float)_rng.NextDouble());
                Quaternion rot = Quaternion.Euler(-90, rotY, 0);

                int texIndex = _textureCount > 1 ? _rng.Next(_textureCount) : 0;

                _impostors[i] = new ImpostorData
                {
                    Matrix = Matrix4x4.TRS(pos, rot, new Vector3(scaleX, scaleY, 1f)),
                    Scale = new Vector3(scaleX, scaleY, 1f),
                    TextureIndex = texIndex
                };
            }

            _currentCount = ImpostorCount;
        }

        private void Update()
        {
            if (_currentCount == 0) return;

            Vector3 playerPos = GetPlayerPosition();
            Vector3 offset = _windDir * _windSpeed * Time.deltaTime;

            if (float.IsNaN(offset.x)) offset = Vector3.zero;

            for (int i = 0; i < _currentCount; i++)
            {
                Vector3 pos = _impostors[i].Matrix.GetColumn(3);
                pos += offset;

                float distFromPlayer = Vector3.Distance(pos, playerPos);
                if (distFromPlayer > 18000f)
                {
                    float angle = (float)(_rng.NextDouble() * Mathf.PI * 2f);
                    float newDist = (float)(_rng.NextDouble() * (MaxDistance - MinDistance) + MinDistance);
                    pos = playerPos + new Vector3(
                        Mathf.Cos(angle) * newDist,
                        (float)(_rng.NextDouble() * 4000f + 2000f),
                        Mathf.Sin(angle) * newDist
                    );

                    float rotY = Mathf.Lerp(RotationRangeY.x, RotationRangeY.y, (float)_rng.NextDouble());
                    Quaternion rot = Quaternion.Euler(-90, rotY, 0);

                    _impostors[i] = new ImpostorData
                    {
                        Matrix = Matrix4x4.TRS(pos, rot, _impostors[i].Scale),
                        Scale = _impostors[i].Scale,
                        TextureIndex = _impostors[i].TextureIndex
                    };
                }
                else
                {
                    var m = _impostors[i].Matrix;
                    m.SetColumn(3, pos);
                    _impostors[i] = new ImpostorData { Matrix = m, Scale = _impostors[i].Scale, TextureIndex = _impostors[i].TextureIndex };
                }
            }
        }

        private void LateUpdate()
        {
            if (_currentCount == 0) return;

            for (int t = 0; t < _textureCount; t++)
            {
                int count = 0;
                Matrix4x4[] matrices = new Matrix4x4[_currentCount];

                for (int i = 0; i < _currentCount; i++)
                {
                    if (_impostors[i].TextureIndex == t)
                    {
                        matrices[count++] = _impostors[i].Matrix;
                    }
                }

                if (count > 0 && _instMaterials[t] != null && _mesh != null)
                {
                    Graphics.DrawMeshInstanced(
                        _mesh, 0, _instMaterials[t],
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

        private Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.name = "CloudQuad";

            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0)
            };

            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();

            mesh.bounds = new Bounds(Vector3.zero, new Vector3(40000, 40000, 40000));

            return mesh;
        }

        private Vector3 GetPlayerPosition()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform.position : Vector3.zero;
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
    }
}