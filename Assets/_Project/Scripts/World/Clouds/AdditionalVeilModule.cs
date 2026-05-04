using UnityEngine;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// AdditionalVeilModule — server-controlled pluggable veil piece.
    /// Used for event-driven veil coverage (e.g., city поглощено завесой).
    ///
    /// Key features:
    /// - Receives position (XYZ) and altitude from server
    /// - Server can spawn/despawn via NetworkBehaviour
    /// - Independent from main veil (which is client-only)
    /// - Visual can differ from main veil (event-specific colors/effects)
    /// </summary>
    public class AdditionalVeilModule : MonoBehaviour
    {
        [Header("Module Settings")]
        [Tooltip("Unique ID for this veil module (assigned by server)")]
        public ushort ModuleId;

        [Tooltip("Is this module currently active?")]
        public bool IsActive = true;

        [Header("Position (set by server)")]
        [Tooltip("World position center of this veil module")]
        public Vector3 WorldPosition;

        [Tooltip("Module radius (spherical coverage)")]
        public float ModuleRadius = 5000f;

        [Tooltip("Altitude offset from base veil")]
        public float AltitudeOffset = 0f;

        [Header("Visual Settings")]
        [Tooltip("Color override (if different from main veil)")]
        public Color ModuleColor = new Color(0.1f, 0.08f, 0.2f, 0.9f);

        [Tooltip("Density multiplier")]
        public float DensityMultiplier = 1.5f;

        [Tooltip("Custom lightning chance (0-1)")]
        public float LightningChance = 0.1f;

        [Header("Material")]
        [SerializeField] private Material _moduleMaterial;

        private GameObject _veilSphere;
        private MeshRenderer _meshRenderer;
        private ParticleSystem _lightningVFX;
        private float _lightningTimer = 0f;
        private float _nextLightning = 30f;

        private static readonly int ColorID = Shader.PropertyToID("_VeilColor");
        private static readonly int DensityID = Shader.PropertyToID("_VeilDensity");

        public void Initialize(ushort moduleId, Vector3 worldPos, float radius, float altOffset)
        {
            ModuleId = moduleId;
            WorldPosition = worldPos;
            ModuleRadius = radius;
            AltitudeOffset = altOffset;

            transform.position = worldPos;
            transform.localScale = Vector3.one * radius * 2f;

            SetupVisuals();
        }

        public void UpdateModule(Vector3 worldPos, float radius, float altOffset)
        {
            WorldPosition = worldPos;
            ModuleRadius = radius;
            AltitudeOffset = altOffset;

            transform.position = worldPos;
            transform.localScale = Vector3.one * radius * 2f;
        }

        public void Activate()
        {
            IsActive = true;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            IsActive = false;
            gameObject.SetActive(false);
        }

        private void SetupVisuals()
        {
            if (_moduleMaterial == null)
            {
                _moduleMaterial = new Material(Shader.Find("Project C/Clouds/VeilShader"));
            }

            _moduleMaterial.SetColor(ColorID, ModuleColor);
            _moduleMaterial.SetFloat(DensityID, DensityMultiplier);

            _veilSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _veilSphere.name = $"AdditionalVeil_{ModuleId}";
            _veilSphere.transform.SetParent(transform);
            _veilSphere.transform.localPosition = Vector3.zero;
            _veilSphere.transform.localScale = Vector3.one * 2f;

            _meshRenderer = _veilSphere.GetComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _moduleMaterial;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            var col = _veilSphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetupLightning();
        }

        private void SetupLightning()
        {
            GameObject lightningObj = new GameObject($"VeilLightning_{ModuleId}");
            lightningObj.transform.SetParent(transform);

            _lightningVFX = lightningObj.AddComponent<ParticleSystem>();

            var main = _lightningVFX.main;
            main.loop = true;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.startColor = new Color(0.7f, 0.4f, 1f, 1f);
            main.maxParticles = 5;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _lightningVFX.emission;
            emission.rateOverTime = 0f;

            var shape = _lightningVFX.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = ModuleRadius * 0.8f;

            var renderer = _lightningVFX.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private void Update()
        {
            if (!IsActive) return;

            _lightningTimer += Time.deltaTime;

            if (_lightningTimer >= _nextLightning)
            {
                if (Random.value < LightningChance)
                {
                    TriggerLightning();
                }

                _lightningTimer = 0f;
                _nextLightning = Random.Range(15f, 45f);
            }

            UpdateMaterialEffects();
        }

        private void TriggerLightning()
        {
            if (_lightningVFX != null)
            {
                int count = Random.Range(1, 4);
                _lightningVFX.Emit(count);
            }

            if (_moduleMaterial != null)
            {
                _moduleMaterial.SetFloat("_LightningIntensity", 1f);
            }
        }

        private float _lightningIntensity = 0f;

        private void UpdateMaterialEffects()
        {
            if (_moduleMaterial == null) return;

            if (_lightningIntensity > 0.01f)
            {
                _lightningIntensity = Mathf.Lerp(_lightningIntensity, 0f, Time.deltaTime * 4f);
                _moduleMaterial.SetFloat("_LightningIntensity", _lightningIntensity);
            }
        }

        public void SetModuleColor(Color color)
        {
            ModuleColor = color;
            if (_moduleMaterial != null)
            {
                _moduleMaterial.SetColor(ColorID, color);
            }
        }

        public void SetDensity(float density)
        {
            DensityMultiplier = density;
            if (_moduleMaterial != null)
            {
                _moduleMaterial.SetFloat(DensityID, density);
            }
        }
    }

    /// <summary>
    /// Server-side manager for additional veil modules.
    /// Handles spawning/despawning and state synchronization to clients.
    /// </summary>
    public class AdditionalVeilManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int _maxModules = 10;
        [SerializeField] private GameObject _modulePrefab;

        private System.Collections.Generic.List<AdditionalVeilModule> _activeModules
            = new System.Collections.Generic.List<AdditionalVeilModule>();

        public AdditionalVeilModule SpawnModule(Vector3 worldPos, float radius, float altOffset)
        {
            if (_activeModules.Count >= _maxModules)
            {
                Debug.LogWarning("[AdditionalVeilManager] Max modules reached!");
                return null;
            }

            var module = Instantiate(_modulePrefab, worldPos, Quaternion.identity, transform);
            var component = module.GetComponent<AdditionalVeilModule>();

            if (component == null)
            {
                component = module.AddComponent<AdditionalVeilModule>();
            }

            ushort id = (ushort)_activeModules.Count;
            component.Initialize(id, worldPos, radius, altOffset);

            _activeModules.Add(component);

            Debug.Log($"[AdditionalVeilManager] Spawned module {id} at {worldPos}");
            return component;
        }

        public void DespawnModule(ushort moduleId)
        {
            var module = _activeModules.Find(m => m.ModuleId == moduleId);
            if (module != null)
            {
                module.Deactivate();
                _activeModules.Remove(module);
                Destroy(module.gameObject);
                Debug.Log($"[AdditionalVeilManager] Despawned module {moduleId}");
            }
        }

        public void UpdateModulePosition(ushort moduleId, Vector3 worldPos, float radius, float altOffset)
        {
            var module = _activeModules.Find(m => m.ModuleId == moduleId);
            if (module != null)
            {
                module.UpdateModule(worldPos, radius, altOffset);
            }
        }
    }
}
