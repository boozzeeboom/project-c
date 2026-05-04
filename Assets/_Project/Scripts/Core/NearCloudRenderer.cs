using UnityEngine;

namespace ProjectC.Core
{
    public class NearCloudRenderer : MonoBehaviour
    {
        [Header("Settings")]
        public int CloudCount = 80;
        public float MinAltitude = 3000f;
        public float MaxAltitude = 5000f;
        public float CloudSize = 100f;
        public Material CloudMaterial;

        private Matrix4x4[] _matrices;
        private Mesh _mesh;
        private Material _instMaterial;
        private int _currentCount = 0;

        private Vector3 _windDir = Vector3.right;
        private float _windSpeed = 0f;
        private System.Random _rng;

        public int ActiveCount => _currentCount;

        public void Initialize()
        {
            _rng = new System.Random(12345 + name.GetHashCode());
            _matrices = new Matrix4x4[CloudCount];

            _mesh = CreateMesh();

            if (CloudMaterial != null)
            {
                _instMaterial = new Material(CloudMaterial);
                _instMaterial.enableInstancing = true;
            }

            Debug.Log($"[{name}] Initialized: count={CloudCount}, alt={MinAltitude}-{MaxAltitude}");
        }

        public void Generate(Vector3 playerPos)
        {
            if (_matrices == null || _instMaterial == null) return;

            for (int i = 0; i < CloudCount; i++)
            {
                float angle = (float)(_rng.NextDouble() * Mathf.PI * 2f);
                float radius = (float)(_rng.NextDouble() * 5000f);

                float x = playerPos.x + Mathf.Cos(angle) * radius;
                float z = playerPos.z + Mathf.Sin(angle) * radius;
                float y = (float)(_rng.NextDouble() * (MaxAltitude - MinAltitude) + MinAltitude);

                Vector3 pos = new Vector3(x, y, z);
                float scale = CloudSize * (float)(_rng.NextDouble() * 1.0 + 0.5f);
                Quaternion rot = Quaternion.Euler(0, (float)(_rng.NextDouble() * 360f), 0);

                _matrices[i] = Matrix4x4.TRS(pos, rot, new Vector3(scale, scale * 0.6f, scale));
            }

            _currentCount = CloudCount;
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

        private Mesh CreateMesh()
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
            if (_instMaterial != null) Destroy(_instMaterial);
        }

        private Vector3 GetPlayerPosition()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform.position : Vector3.zero;
        }
    }
}