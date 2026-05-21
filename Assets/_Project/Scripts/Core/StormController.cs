using UnityEngine;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public class StormController : MonoBehaviour
    {
        public ushort StormId { get; private set; }

        private Vector3 _targetPosition;
        private float _intensity;
        private bool _lightningActive;

        [Header("Visual Settings")]
        [SerializeField] private Material _stormMaterial;
        [SerializeField] private ParticleSystem _lightningVFX;

        [Header("Storm Generator")]
        [SerializeField] private StormCloudGenerator _cloudGenerator;

        [Header("Visibility")]
        [SerializeField] private float _visibilityDistance = 50000f;

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

        public void Initialize(ushort id, Vector3 worldPos, float intensity)
        {
            if (StormId != 0 && StormId != id)
            {
                ClientControllers.Remove(StormId);
            }

            StormId = id;
            _targetPosition = worldPos;
            _intensity = intensity;
            transform.position = worldPos;

            ClientControllers[id] = this;

            gameObject.name = $"Storm_{id}";

            if (_cloudGenerator != null)
            {
                var pattern = _cloudGenerator.defaultStormPattern;
                if (pattern != null)
                {
                    _cloudGenerator.SpawnStorm(id, worldPos, pattern, intensity);
                }
            }

            Debug.Log($"[StormController] Initialized storm {id} at {worldPos}");
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