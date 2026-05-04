using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Uses VeilRaymarch.shader via OnRenderImage for full-screen raymarch.
    /// Simple integration - just add to camera.
    ///
    /// VeilRaymarch.shader does screen-space raymarch:
    /// - Reconstructs ray per-pixel from camera matrices
    /// - Intersects with veil layer (Y=800-1200)
    /// - Accumulates color via Beer-Lambert
    /// - Creates "клубящаяся завеса со своими впадинами каньонами"
    /// </summary>
    public class VeilRaymarchBlit : MonoBehaviour
    {
        [Header("Material")]
        public Material VeilMaterial;

        [Header("Veil Layer")]
        public float VeilBottom = 800f;
        public float VeilTop = 1200f;

        [Header("Raymarch Settings")]
        public int RaymarchSteps = 12;
        public float MaxDistance = 8000f;

        [Header("Noise")]
        public float NoiseScale = 0.002f;
        public float NoiseSpeed = 0.01f;
        public int NoiseOctaves = 3;
        public float DensityMultiplier = 1f;

        [Header("Color")]
        public Color VeilColor = new Color(0.176f, 0.106f, 0.306f, 1f);
        public Color LightningColor = new Color(0.7f, 0.4f, 1f, 1f);

        [Header("Debug")]
        public bool DebugLog = false;

        private Material _instanceMaterial;
        private float _lightningIntensity = 0f;
        private float _lightningTimer = 0f;
        private float _nextLightningTime = 30f;

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

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (VeilMaterial == null)
            {
                Shader shader = Shader.Find("Project C/Clouds/VeilRaymarch");
                if (shader != null)
                {
                    VeilMaterial = new Material(shader);
                    Log("Created VeilRaymarch material");
                }
                else
                {
                    LogError("VeilRaymarch shader not found!");
                    return;
                }
            }

            _instanceMaterial = new Material(VeilMaterial);
            UpdateMaterialProperties();

            Log("Initialized VeilRaymarchBlit");
        }

        private void UpdateMaterialProperties()
        {
            if (_instanceMaterial == null) return;

            _instanceMaterial.SetColor(Property_VeilColor, VeilColor);
            _instanceMaterial.SetColor(Property_LightningColor, LightningColor);
            _instanceMaterial.SetFloat(Property_NoiseScale, NoiseScale);
            _instanceMaterial.SetFloat(Property_NoiseSpeed, NoiseSpeed);
            _instanceMaterial.SetInt(Property_NoiseOctaves, NoiseOctaves);
            _instanceMaterial.SetFloat(Property_VeilBottom, VeilBottom);
            _instanceMaterial.SetFloat(Property_VeilTop, VeilTop);
            _instanceMaterial.SetInt(Property_RaymarchSteps, RaymarchSteps);
            _instanceMaterial.SetFloat(Property_RaymarchMaxDist, MaxDistance);
            _instanceMaterial.SetFloat(Property_DensityMultiplier, DensityMultiplier);
        }

        private void OnPreRender()
        {
            UpdateUniforms();
        }

        private void UpdateUniforms()
        {
            if (_instanceMaterial == null) return;

            float windX = 0f;
            float windZ = 0f;

            if (WindManager.Instance != null)
            {
                Vector3 windDir = WindManager.Instance.CurrentWindDirection;
                float windSpeed = WindManager.Instance.CurrentWindSpeed;
                windX = windDir.x * windSpeed * NoiseSpeed;
                windZ = windDir.z * windSpeed * NoiseSpeed;
            }

            _instanceMaterial.SetFloat(Property_WindX, windX);
            _instanceMaterial.SetFloat(Property_WindZ, windZ);
            _instanceMaterial.SetFloat(Property_LightningIntensity, _lightningIntensity);
        }

        private void Update()
        {
            UpdateLightning();
        }

        private void UpdateLightning()
        {
            _lightningTimer += Time.deltaTime;

            if (_lightningTimer >= _nextLightningTime)
            {
                _lightningIntensity = 1f;
                _lightningTimer = 0f;
                _nextLightningTime = Random.Range(20f, 60f);
                Log("Lightning flash!");
            }

            _lightningIntensity = Mathf.Lerp(_lightningIntensity, 0f, Time.deltaTime * 4f);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_instanceMaterial != null)
            {
                Graphics.Blit(source, destination, _instanceMaterial);
            }
            else
            {
                Graphics.Blit(source, destination);
            }
        }

        public void TriggerLightning()
        {
            _lightningIntensity = 1f;
            _lightningTimer = 0f;
            Log("Lightning triggered!");
        }

        public void SetGlobalAltitudeOffset(float offset)
        {
            VeilBottom = 800f + offset;
            VeilTop = 1200f + offset;
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetFloat(Property_VeilBottom, VeilBottom);
                _instanceMaterial.SetFloat(Property_VeilTop, VeilTop);
            }
            Log($"Altitude offset set: Bottom={VeilBottom}, Top={VeilTop}");
        }

        private void OnDestroy()
        {
            if (_instanceMaterial != null)
            {
                Destroy(_instanceMaterial);
            }
        }

        private void Log(string msg)
        {
            if (DebugLog) Debug.Log($"[VeilRaymarchBlit] {msg}");
        }

        private void LogError(string msg)
        {
            Debug.LogError($"[VeilRaymarchBlit] {msg}");
        }
    }
}