using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// HorizonVeil — volumetric curtain that follows player.
    /// Uses simple mesh-based approach like NearCloudRenderer/DistantCloudManager.
    ///
    /// Key insight from CLOUDENGINE:
    /// - 2D noise extruded with height gradient creates "volumetric" look
    /// - Large plane at veil height (Y=800-1200) covers horizon
    /// - Player tracking via GameObject.FindGameObjectWithTag("Player") like other clouds
    ///
    /// FIXED from previous version:
    /// - Uses FindGameObjectWithTag("Player") NOT Camera.main (Camera.main broken with FloatingOrigin)
    /// - Follows player correctly via Update()
    /// - SetGlobalAltitudeOffset no longer resets to (0,0,0)
    /// </summary>
    public class HorizonVeilRenderer : MonoBehaviour
    {
        [Header("Material")]
        public Material VeilMaterial;

        [Header("Veil Position")]
        public float BaseVeilHeight = 1200f;
        public float VeilSize = 200000f;
        public float GlobalAltitudeOffset = 0f;

        [Header("Curtain Settings")]
        public float CurtainBottom = 800f;
        public float CurtainTop = 1200f;
        [Range(0, 2)]
        public float CurtainDensity = 1.0f;

        [Header("Noise Settings")]
        public float NoiseScale = 0.01f;
        public float NoiseSpeed = 0.1f;
        [Range(1, 6)]
        public int NoiseOctaves = 4;

        [Header("Color (Purple-Green Dark)")]
        public Color VeilColor = new Color(0.176f, 0.106f, 0.306f, 1f);
        public Color LightningColor = new Color(0.7f, 0.4f, 1f, 1f);

        [Header("Depth Fade")]
        public float FadeStart = 1000f;
        public float FadeEnd = 8000f;

        [Header("Debug")]
        public bool DebugLog = false;

        private GameObject _veilPlane;
        private MeshRenderer _meshRenderer;
        private Material _instanceMaterial;
        private bool _initialized = false;
        private Transform _playerTransform;

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

            if (DebugLog) Debug.Log($"[HorizonVeilRenderer] Initialized at Y={GetVeilY()}");
        }

        private void CreateVeilPlane()
        {
            Mesh mesh = new Mesh();
            mesh.name = "VeilPlaneMesh";

            float halfSize = VeilSize * 0.5f;

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

            _veilPlane = new GameObject("VeilPlane");
            _veilPlane.transform.SetParent(transform);
            _veilPlane.transform.localPosition = Vector3.zero;
            _veilPlane.transform.localRotation = Quaternion.identity;
            _veilPlane.transform.localScale = Vector3.one;

            MeshFilter meshFilter = _veilPlane.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            _meshRenderer = _veilPlane.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            if (DebugLog) Debug.Log($"[HorizonVeilRenderer] Created plane size={VeilSize}");
        }

        private void SetupMaterial()
        {
            if (VeilMaterial != null)
            {
                _instanceMaterial = new Material(VeilMaterial);
            }
            else
            {
                Shader shader = Shader.Find("Project C/Clouds/VeilCurtain");
                if (shader == null) shader = Shader.Find("Project C/Clouds/VeilShader");
                if (shader != null)
                {
                    _instanceMaterial = new Material(shader);
                }
                else
                {
                    Debug.LogError("[HorizonVeilRenderer] No veil shader found!");
                    return;
                }
            }

            _instanceMaterial.color = VeilColor;
            _instanceMaterial.SetColor("_VeilColor", VeilColor);
            _instanceMaterial.SetColor("_LightningColor", LightningColor);
            _instanceMaterial.SetFloat("_LightningIntensity", 0f);
            _instanceMaterial.SetFloat("_NoiseScale", NoiseScale);
            _instanceMaterial.SetFloat("_NoiseSpeed", NoiseSpeed);
            _instanceMaterial.SetInt("_NoiseOctaves", NoiseOctaves);
            _instanceMaterial.SetFloat("_FogDistance", FadeEnd);
            _instanceMaterial.SetColor("_FogColor", new Color(0.05f, 0.05f, 0.08f, 1f));

            _meshRenderer.sharedMaterial = _instanceMaterial;
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

            Vector3 playerPos = GetPlayerPosition();
            float veilY = GetVeilY();

            Vector3 targetPos = new Vector3(playerPos.x, veilY, playerPos.z);
            _veilPlane.transform.position = targetPos;

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

            // Shader uniforms for VeilShader
            _instanceMaterial.SetColor("_VeilColor", VeilColor);
            _instanceMaterial.SetColor("_LightningColor", LightningColor);
            _instanceMaterial.SetFloat("_LightningIntensity", _lightningIntensity);
            _instanceMaterial.SetFloat("_NoiseScale", NoiseScale);
            _instanceMaterial.SetFloat("_NoiseSpeed", NoiseSpeed);
            _instanceMaterial.SetInt("_NoiseOctaves", NoiseOctaves);
            _instanceMaterial.SetFloat("_FogDistance", FadeEnd);
            _instanceMaterial.SetColor("_FogColor", new Color(0.05f, 0.05f, 0.08f, 1f));
        }

        private float _lightningTimer = 0f;
        private float _nextLightningTime = 30f;
        private float _lightningIntensity = 0f;

        private void UpdateLightning()
        {
            _lightningTimer += Time.deltaTime;

            if (_lightningTimer >= _nextLightningTime)
            {
                _lightningIntensity = 1f;
                _lightningTimer = 0f;
                _nextLightningTime = Random.Range(20f, 60f);
                if (DebugLog) Debug.Log("[HorizonVeilRenderer] Lightning!");
            }

            _lightningIntensity = Mathf.Lerp(_lightningIntensity, 0f, Time.deltaTime * 4f);

            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetFloat("_LightningIntensity", _lightningIntensity);
            }
        }

        public void SetGlobalAltitudeOffset(float offset)
        {
            GlobalAltitudeOffset = offset;
            if (DebugLog) Debug.Log($"[HorizonVeilRenderer] Altitude offset set to {offset}, BaseVeilHeight now {BaseVeilHeight + offset}");

            // FIXED: Don't reset to (0,0,0) - just update altitude
            // Position will be corrected in next Update() with proper player position
        }

        public void TriggerLightning()
        {
            _lightningIntensity = 1f;
            _lightningTimer = 0f;
            if (DebugLog) Debug.Log("[HorizonVeilRenderer] Lightning triggered!");
        }

        public Vector2 GetVeilHeightRange()
        {
            float bottom = GetVeilY();
            return new Vector2(bottom, bottom + (CurtainTop - CurtainBottom));
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