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
        public Material ImpostorMaterial;

        private Matrix4x4[] _matrices;
        private Mesh _mesh;
        private Material _instMaterial;
        private int _currentCount = 0;
        private System.Random _rng;

        private Vector3 _windDir = Vector3.right;
        private float _windSpeed = 0f;

        public int ActiveCount => _currentCount;

        public void Initialize()
        {
            _rng = new System.Random(54321);
            _matrices = new Matrix4x4[ImpostorCount];

            _mesh = CreateQuadMesh();

            if (ImpostorMaterial != null)
            {
                _instMaterial = new Material(ImpostorMaterial);
                _instMaterial.enableInstancing = true;
            }

            Debug.Log($"[{name}] Initialized: impostors={ImpostorCount}");
        }

        public void Generate(Vector3 playerPos)
        {
            if (_matrices == null || _instMaterial == null) return;

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

                Quaternion rot = Quaternion.Euler(-90, 0, 0);

                _matrices[i] = Matrix4x4.TRS(pos, rot, new Vector3(scaleX, scaleY, 1f));
            }

            _currentCount = ImpostorCount;
            Debug.Log($"[{name}] Generated {_currentCount} impostors around player");
        }

        private void Update()
        {
            if (_currentCount == 0) return;

            Vector3 playerPos = GetPlayerPosition();
            Vector3 offset = _windDir * _windSpeed * Time.deltaTime;

            if (float.IsNaN(offset.x)) offset = Vector3.zero;

            for (int i = 0; i < _currentCount; i++)
            {
                Vector3 pos = _matrices[i].GetColumn(3);
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
                }

                _matrices[i].SetColumn(3, pos);
            }
        }

        private void LateUpdate()
        {
            if (_currentCount == 0 || _instMaterial == null || _mesh == null) return;

            Graphics.DrawMeshInstanced(
                _mesh, 0, _instMaterial,
                _matrices, _currentCount,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false
            );
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
            if (_instMaterial != null) Destroy(_instMaterial);
        }
    }
}