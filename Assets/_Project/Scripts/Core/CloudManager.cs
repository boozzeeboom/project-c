using UnityEngine;
using ProjectC.World.Clouds;

namespace ProjectC.Core
{
    public class CloudManager : MonoBehaviour
    {
        public static CloudManager Instance { get; private set; }

        [Header("Upper Layer (6000-8000m)")]
        public NearCloudRenderer UpperLayer;
        public int UpperCount = 80;
        public float UpperMinAlt = 6000f;
        public float UpperMaxAlt = 8000f;
        public float UpperMinCloudSize = 80f;
        public float UpperMaxCloudSize = 150f;
        public float UpperSizeRandomCoeff = 0.3f;
        [Tooltip("✦ BILLBOARD MODE: When true, clouds use camera-facing quads instead of 3D spheres. Eliminates the onion-layer (soap bubble) effect. Ideal for Upper layer where parallax is imperceptible.")]
        public bool UpperUseBillboardQuad = true;
        [Tooltip("Per-layer material override. If null, falls back to CloudMaterial below. Use this to give each layer a distinct look (e.g. wispy cirrus up top, dense stratus below).")]
        public Material UpperCloudMaterial;

        [Header("Middle Layer (3000-5000m)")]
        public NearCloudRenderer MiddleLayer;
        public int MiddleCount = 120;
        public float MiddleMinAlt = 3000f;
        public float MiddleMaxAlt = 5000f;
        public float MiddleMinCloudSize = 100f;
        public float MiddleMaxCloudSize = 200f;
        public float MiddleSizeRandomCoeff = 0.5f;
        [Tooltip("Per-layer material override. If null, falls back to CloudMaterial below.")]
        public Material MiddleCloudMaterial;

        [Header("Lower Layer (1500-3000m)")]
        public NearCloudRenderer LowerLayer;
        public int LowerCount = 80;
        public float LowerMinAlt = 1500f;
        public float LowerMaxAlt = 3000f;
        public float LowerMinCloudSize = 150f;
        public float LowerMaxCloudSize = 300f;
        public float LowerSizeRandomCoeff = 0.4f;
        [Tooltip("Per-layer material override. If null, falls back to CloudMaterial below.")]
        public Material LowerCloudMaterial;

        [Header("Distant (5000-15000m)")]
        public DistantCloudManager DistantManager;
        public int DistantCount = 140;
        public float DistantMinDist = 5000f;
        public float DistantMaxDist = 15000f;
        public float DistantMinSize = 500f;
        public float DistantMaxSize = 2000f;

        [Header("HorizonVeil (Phase 4a)")]
        public HorizonVeilRenderer HorizonVeil;
        public float BaseVeilHeight = 1200f;
        public float VeilThickness = 400f;

        [Header("Additional Veils (Server-controlled)")]
        public AdditionalVeilManager AdditionalVeilMgr;
        public int MaxAdditionalVeils = 10;

        [Header("Material")]
        public Material CloudMaterial;
        public Material DistantMaterial;

        [Header("Generation")]
        public float GenerationRadius = 5000f;

        [Header("Debug Logging")]
        public bool logInitialization = false;
        public bool logAltitudeOffset = false;
        public bool logRegeneration = false;
        public bool logCloudCount = false;

        private bool _initialized = false;
        private float _globalAltitudeOffset = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            Vector3 playerPos = GetPlayerPosition();
            if (logInitialization) Debug.Log($"[CloudManager] Init at playerPos={playerPos}");

            if (UpperLayer != null)
            {
                UpperLayer.CloudCount = UpperCount;
                UpperLayer.MinAltitude = UpperMinAlt;
                UpperLayer.MaxAltitude = UpperMaxAlt;
                UpperLayer.CloudMaterial = UpperCloudMaterial != null ? UpperCloudMaterial : CloudMaterial;
                UpperLayer.MinCloudSize = UpperMinCloudSize;
                UpperLayer.MaxCloudSize = UpperMaxCloudSize;
                UpperLayer.SizeRandomCoefficient = UpperSizeRandomCoeff;
                UpperLayer.UseBillboardQuad = UpperUseBillboardQuad;
                UpperLayer.Initialize();
                UpperLayer.Generate(playerPos);
            }

            if (MiddleLayer != null)
            {
                MiddleLayer.CloudCount = MiddleCount;
                MiddleLayer.MinAltitude = MiddleMinAlt;
                MiddleLayer.MaxAltitude = MiddleMaxAlt;
                MiddleLayer.CloudMaterial = MiddleCloudMaterial != null ? MiddleCloudMaterial : CloudMaterial;
                MiddleLayer.MinCloudSize = MiddleMinCloudSize;
                MiddleLayer.MaxCloudSize = MiddleMaxCloudSize;
                MiddleLayer.SizeRandomCoefficient = MiddleSizeRandomCoeff;
                MiddleLayer.Initialize();
                MiddleLayer.Generate(playerPos);
            }

            if (LowerLayer != null)
            {
                LowerLayer.CloudCount = LowerCount;
                LowerLayer.MinAltitude = LowerMinAlt;
                LowerLayer.MaxAltitude = LowerMaxAlt;
                LowerLayer.CloudMaterial = LowerCloudMaterial != null ? LowerCloudMaterial : CloudMaterial;
                LowerLayer.MinCloudSize = LowerMinCloudSize;
                LowerLayer.MaxCloudSize = LowerMaxCloudSize;
                LowerLayer.SizeRandomCoefficient = LowerSizeRandomCoeff;
                LowerLayer.Initialize();
                LowerLayer.Generate(playerPos);
            }

            if (DistantManager != null)
            {
                DistantManager.ImpostorCount = DistantCount;
                DistantManager.MinDistance = DistantMinDist;
                DistantManager.MaxDistance = DistantMaxDist;
                DistantManager.MinSize = DistantMinSize;
                DistantManager.MaxSize = DistantMaxSize;
                DistantManager.ImpostorMaterial = DistantMaterial != null ? DistantMaterial : CloudMaterial;
                DistantManager.Initialize();
                DistantManager.Generate(playerPos);
            }

            if (HorizonVeil != null)
            {
                HorizonVeil.BaseVeilHeight = BaseVeilHeight;
                HorizonVeil.Initialize();
            }

            if (AdditionalVeilMgr == null)
            {
                var existing = GetComponent<AdditionalVeilManager>();
                if (existing == null)
                {
                    AdditionalVeilMgr = gameObject.AddComponent<AdditionalVeilManager>();
                }
                else
                {
                    AdditionalVeilMgr = existing;
                }
            }

            _initialized = true;
            if (logInitialization) Debug.Log($"[CloudManager] Init complete. Upper={UpperCount}, Middle={MiddleCount}, Lower={LowerCount}, Distant={DistantCount}, HorizonVeil={HorizonVeil != null}");
        }

        private void Update()
        {
            if (!_initialized) return;

            if (WindManager.Instance != null)
            {
                Vector3 dir = WindManager.Instance.CurrentWindDirection;
                float speed = WindManager.Instance.CurrentWindSpeed;

                if (UpperLayer != null) UpperLayer.SetWind(dir, speed);
                if (MiddleLayer != null) MiddleLayer.SetWind(dir, speed);
                if (LowerLayer != null) LowerLayer.SetWind(dir, speed);
                if (DistantManager != null) DistantManager.SetWind(dir, speed);
            }
        }

        public void SetGlobalAltitudeOffset(float offset)
        {
            _globalAltitudeOffset = offset;
            if (HorizonVeil != null)
            {
                HorizonVeil.SetGlobalAltitudeOffset(offset);
            }
            if (logAltitudeOffset) Debug.Log($"[CloudManager] Global altitude offset set to {offset}");
        }

        public void TriggerVeilLightning()
        {
            if (HorizonVeil != null)
            {
                HorizonVeil.TriggerLightning();
            }
        }

        private Vector3 GetPlayerPosition()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform.position : Vector3.zero;
        }

        [ContextMenu("Regenerate All Clouds")]
        public void RegenerateAllClouds()
        {
            if (!_initialized) return;
            Vector3 pos = GetPlayerPosition();

            Vector3 dir = Vector3.right;
            float speed = 0f;
            if (WindManager.Instance != null)
            {
                dir = WindManager.Instance.CurrentWindDirection;
                speed = WindManager.Instance.CurrentWindSpeed;
            }

            if (UpperLayer != null) { UpperLayer.Generate(pos); UpperLayer.SetWind(dir, speed); }
            if (MiddleLayer != null) { MiddleLayer.Generate(pos); MiddleLayer.SetWind(dir, speed); }
            if (LowerLayer != null) { LowerLayer.Generate(pos); LowerLayer.SetWind(dir, speed); }
            if (DistantManager != null) { DistantManager.Generate(pos); DistantManager.SetWind(dir, speed); }

            if (logRegeneration) Debug.Log("[CloudManager] Regenerated all clouds");
        }

        public int GetTotalCloudCount()
        {
            int total = 0;
            if (UpperLayer != null) total += UpperLayer.ActiveCount;
            if (MiddleLayer != null) total += MiddleLayer.ActiveCount;
            if (LowerLayer != null) total += LowerLayer.ActiveCount;
            if (DistantManager != null) total += DistantManager.ActiveCount;
            return total;
        }

        [ContextMenu("Log Cloud Count")]
        private void LogCloudCount()
        {
            if (logCloudCount) Debug.Log($"[CloudManager] Total: {GetTotalCloudCount()} (U={UpperLayer?.ActiveCount ?? 0}, M={MiddleLayer?.ActiveCount ?? 0}, L={LowerLayer?.ActiveCount ?? 0}, D={DistantManager?.ActiveCount ?? 0})");
        }
    }
}