using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Controls VeilRaymarchMesh shader on a plane that follows player.
    /// Uses volumetric raymarch with CLOUDENGINE-style lighting (normals, cel-shading, rim).
    ///
    /// Works with plane mesh, no camera attachment needed.
    /// Follows player in XZ, positioned at veil height (Y=800-1200).
    /// </summary>
    public class VeilRaymarchMeshController : MonoBehaviour
    {
        [Header("Material")]
        public Material VeilMaterial;

        [Header("Veil Position")]
        public float BaseVeilHeight = 1200f;
        public float GlobalAltitudeOffset = 0f;

        [Header("Plane Settings")]
        public float PlaneSize = 200000f;

        [Header("Raymarch Settings")]
        public int RaymarchSteps = 24;
        public float MaxDistance = 8000f;

        [Header("Noise")]
        public float NoiseScale = 0.002f;
        public float NoiseSpeed = 0.01f;
        public int NoiseOctaves = 3;
        public float DensityMultiplier = 1f;

        [Header("Wind")]
        public float WindX = 0.01f;
        public float WindZ = 0.003f;

        [Header("Lighting (CLOUDENGINE style)")]
        public float DayFactor = 0.5f;
        public float RimPower = 3f;
        public float RimIntensity = 0.5f;

        [Header("Color")]
        public Color VeilColor = new Color(0.176f, 0.106f, 0.306f, 1f);
        public Color LightningColor = new Color(0.7f, 0.4f, 1f, 1f);

        [Header("Debug")]
        public bool DebugLog = false;

        private GameObject _veilPlane;
        private MeshRenderer _meshRenderer;
        private Material _instanceMaterial;
        private bool _initialized = false;
        private Transform _playerTransform;

        private static readonly int Property_VeilColor = Shader.PropertyToID("_VeilColor");
        private static readonly int Property_LightningColor = Shader.PropertyToID("_LightningColor");
        private static readonly int Property_LightningIntensity = Shader.PropertyToID("_LightningIntensity");
        private static readonly int Property_NoiseScale = Shader.PropertyToID("_NoiseScale");
        private static readonly int Property_NoiseSpeed = Shader.PropertyToID("_NoiseSpeed");
        private static readonly int Property_NoiseOctaves = Shader.PropertyToID("_NoiseOctaves");
        private static readonly int Property_VeilBottom = Shader.PropertyToID("_VeilBottom");
        private static readonly int Property_VeilTop = Shader.PropertyToID("_VeilTop");
        private static readonly int Property_RaymarchSteps = Shader.PropertyToID("_RaymarchSteps");
        private static readonly int Property_RaymarchMaxDist = Shader.PropertyToID("_RaymarchMaxDist");
        private static readonly int Property_WindX = Shader.PropertyToID("_WindX");
        private static readonly int Property_WindZ = Shader.PropertyToID("_WindZ");
        private static readonly int Property_DensityMultiplier = Shader.PropertyToID("_DensityMultiplier");
        private static readonly int Property_DayFactor = Shader.PropertyToID("_DayFactor");
        private static readonly int Property_RimPower = Shader.PropertyToID("_RimPower");
        private static readonly int Property_RimIntensity = Shader.PropertyToID("_RimIntensity");

        void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;

            CreateVeilPlane();
            SetupMaterial();

            DontDestroyOnLoad(gameObject);
            _initialized = true;

            if (DebugLog) Debug.Log($"[VeilRaymarchMeshController] Initialized at Y={GetVeilY()}");
        }

        private void CreateVeilPlane()
        {
            Mesh mesh = new Mesh();
            mesh.name = "VeilRaymarchPlane";

            float halfSize = PlaneSize * 0.5f;

            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(-halfSize, 0, -halfSize);
            vertices[1] = new Vector3(halfSize, 0, -halfSize);
            vertices[2] = new Vector3(halfSize, 0, halfSize);
            vertices[3] = new Vector3(-halfSize, 0, halfSize);

            Vector2[] uv = new Vector2[4];
            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(1, 0);
            uv[2] = new Vector2(1, 1);
            uv[3] = new Vector2(0, 1);

            int[] triangles = new int[6];
            triangles[0] = 0; triangles[1] = 2; triangles[2] = 1;
            triangles[3] = 0; triangles[4] = 3; triangles[5] = 2;

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _veilPlane = new GameObject("VeilRaymarchPlane");
            _veilPlane.transform.SetParent(transform);
            _veilPlane.transform.localPosition = Vector3.zero;
            _veilPlane.transform.localRotation = Quaternion.identity;
            _veilPlane.transform.localScale = Vector3.one;

            MeshFilter meshFilter = _veilPlane.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            _meshRenderer = _veilPlane.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
        }

        private void SetupMaterial()
        {
            if (VeilMaterial != null)
            {
                _instanceMaterial = new Material(VeilMaterial);
            }
            else
            {
                Shader shader = Shader.Find("Project C/Clouds/VeilRaymarchMesh");
                if (shader != null)
                {
                    _instanceMaterial = new Material(shader);
                    if (DebugLog) Debug.Log("Created material with VeilRaymarchMesh shader");
                }
                else
                {
                    Debug.LogError("[VeilRaymarchMeshController] VeilRaymarchMesh shader not found!");
                    return;
                }
            }

            UpdateMaterialProperties();
            _meshRenderer.sharedMaterial = _instanceMaterial;
        }

        private void UpdateMaterialProperties()
        {
            if (_instanceMaterial == null) return;

            _instanceMaterial.SetColor(Property_VeilColor, VeilColor);
            _instanceMaterial.SetColor(Property_LightningColor, LightningColor);
            _instanceMaterial.SetFloat(Property_NoiseScale, NoiseScale);
            _instanceMaterial.SetFloat(Property_NoiseSpeed, NoiseSpeed);
            _instanceMaterial.SetInt(Property_NoiseOctaves, NoiseOctaves);
            _instanceMaterial.SetFloat(Property_DensityMultiplier, DensityMultiplier);
            _instanceMaterial.SetFloat(Property_WindX, WindX);
            _instanceMaterial.SetFloat(Property_WindZ, WindZ);
            _instanceMaterial.SetInt(Property_RaymarchSteps, RaymarchSteps);
            _instanceMaterial.SetFloat(Property_RaymarchMaxDist, MaxDistance);
            _instanceMaterial.SetFloat(Property_DayFactor, DayFactor);
            _instanceMaterial.SetFloat(Property_RimPower, RimPower);
            _instanceMaterial.SetFloat(Property_RimIntensity, RimIntensity);
        }

        private float GetVeilY()
        {
            return BaseVeilHeight + GlobalAltitudeOffset;
        }

        void Update()
        {
            if (!_initialized)
            {
                Initialize();
                return;
            }

            if (_veilPlane == null) return;

            // Follow player in XZ
            Vector3 playerPos = GetPlayerPosition();
            float veilY = GetVeilY();
            _veilPlane.transform.position = new Vector3(playerPos.x, veilY, playerPos.z);

            UpdateMaterialUniforms();
            UpdateLightning();
        }

        private Vector3 GetPlayerPosition()
        {
            if (_playerTransform != null)
            {
                return _playerTransform.position;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                return player.transform.position;
            }

            return Vector3.zero;
        }

        private void UpdateMaterialUniforms()
        {
            if (_instanceMaterial == null) return;

            float veilY = GetVeilY();
            _instanceMaterial.SetFloat(Property_VeilBottom, veilY);
            _instanceMaterial.SetFloat(Property_VeilTop, veilY + 400f);
        }

        private float _lightningIntensity = 0f;
        private float _lightningTimer = 0f;
        private float _nextLightningTime = 30f;

        private void UpdateLightning()
        {
            _lightningTimer += Time.deltaTime;

            if (_lightningTimer >= _nextLightningTime)
            {
                _lightningIntensity = 1f;
                _lightningTimer = 0f;
                _nextLightningTime = Random.Range(20f, 60f);
                if (DebugLog) Debug.Log("[VeilRaymarchMeshController] Lightning!");
            }

            _lightningIntensity = Mathf.Lerp(_lightningIntensity, 0f, Time.deltaTime * 4f);

            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetFloat(Property_LightningIntensity, _lightningIntensity);
            }
        }

        public void SetGlobalAltitudeOffset(float offset)
        {
            GlobalAltitudeOffset = offset;
            if (DebugLog) Debug.Log($"[VeilRaymarchMeshController] Altitude offset: {offset}");
        }

        public void TriggerLightning()
        {
            _lightningIntensity = 1f;
            _lightningTimer = 0f;
        }

        void OnDestroy()
        {
            if (_instanceMaterial != null)
            {
                Destroy(_instanceMaterial);
            }
        }

        public void RefreshPlayerTransform()
        {
            _playerTransform = null;
        }
    }
}