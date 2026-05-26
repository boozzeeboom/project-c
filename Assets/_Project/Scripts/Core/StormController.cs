using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

namespace ProjectC.Core
{
    public class StormController : MonoBehaviour
    {
        public ushort StormId { get; private set; }
        public bool IsLightningActive => _lightningActive;

        private Vector3 _targetPosition;
        private float _intensity;
        private bool _lightningActive;
        private string _currentPatternGUID;

        [Header("Visual Settings")]
        [SerializeField] private Material _stormMaterial;
        [SerializeField] private ParticleSystem _lightningVFX;

        [Header("Storm Generator")]
        [SerializeField] private StormCloudGenerator _cloudGenerator;

        [Header("Pattern Assets")]
        [SerializeField] private CloudLayerConfig[] _availablePatterns = new CloudLayerConfig[0];

        [Header("Visibility")]
        [Tooltip("Storm is hidden if player is further than this distance")]
        [SerializeField] private float _visibilityDistance = 100000f;

        private static readonly int LightningFlashProperty = Shader.PropertyToID("_LightningFlash");

        public static Dictionary<ushort, StormController> ClientControllers { get; } = new Dictionary<ushort, StormController>();

        private void Awake()
        {
            Debug.Log($"[StormController] Awake on {gameObject.name}");

            if (_cloudGenerator == null)
            {
                _cloudGenerator = FindAnyObjectByType<StormCloudGenerator>();
                Debug.Log($"[StormController] Found generator: {_cloudGenerator}");
            }

            if (_stormMaterial == null)
            {
                var renderer = GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    _stormMaterial = renderer.sharedMaterial;
                    Debug.Log($"[StormController] Got material from renderer: {_stormMaterial.name}");
                }
            }

            if (gameObject.name.StartsWith("Storm_"))
            {
                string idStr = gameObject.name.Substring(6);
                if (ushort.TryParse(idStr, out ushort id))
                {
                    StormId = id;
                    ClientControllers[id] = this;
                }
            }
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
            ClientControllers.Remove(StormId);
        }

        public void Initialize(ushort id, Vector3 worldPos, float intensity, string patternGUID = "")
        {
            if (StormId != 0 && StormId != id)
            {
                ClientControllers.Remove(StormId);
            }

            StormId = id;
            _targetPosition = worldPos;
            _intensity = intensity;
            _currentPatternGUID = patternGUID;

            if (transform.position != worldPos)
            {
                transform.position = worldPos;
            }

            ClientControllers[id] = this;

            gameObject.name = $"Storm_{id}";

            var pattern = LoadPatternByGUID(patternGUID);
            if (pattern == null)
            {
                pattern = _cloudGenerator != null ? _cloudGenerator.defaultStormPattern : null;
            }

            if (_cloudGenerator != null && pattern != null)
            {
                _cloudGenerator.SpawnStorm(id, worldPos, pattern, intensity, gameObject);
            }

            Debug.Log($"[StormController] Initialized storm {id} at {worldPos}, pattern={pattern?.name ?? "NULL"}");
        }

        private CloudLayerConfig LoadPatternByGUID(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;

#if UNITY_EDITOR
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            var asset = AssetDatabase.LoadAssetAtPath<CloudLayerConfig>(path);
            return asset;
#else
            return null;
#endif
        }

        public void UpdateState(Vector3 worldPos, float intensity)
        {
            _targetPosition = worldPos;
            _intensity = intensity;
        }

        public void TriggerLightning()
        {
            _lightningActive = true;

            if (_lightningVFX != null)
            {
                _lightningVFX.Emit(Random.Range(5, 15));
            }

            StartCoroutine(LightningFlashEffect());
        }

        public void Despawn()
        {
            if (_cloudGenerator != null)
            {
                _cloudGenerator.DespawnStorm(StormId);
            }
            Destroy(gameObject);
        }

        private System.Collections.IEnumerator LightningFlashEffect()
        {
            if (_stormMaterial != null)
            {
                _stormMaterial.SetFloat(LightningFlashProperty, 1f);
                yield return new WaitForSeconds(0.1f);
                _stormMaterial.SetFloat(LightningFlashProperty, 0f);
            }
            _lightningActive = false;
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, 0.1f);

            float dist = Vector3.Distance(transform.position, GetPlayerPosition());
            gameObject.SetActive(dist < _visibilityDistance);
        }

        private Vector3 GetPlayerPosition()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform.position : Vector3.zero;
        }
    }
}