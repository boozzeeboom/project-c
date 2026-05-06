using UnityEngine;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// Client-side storm visual controller.
    /// Receives state updates from ServerStormManager and renders storm visuals.
    /// </summary>
    public class StormController : MonoBehaviour
    {
        public ushort StormId { get; private set; }

        private Vector3 _targetPosition;
        private float _intensity;
        private bool _lightningActive;

        [Header("Visual Settings")]
        [SerializeField] private Material _stormMaterial;
        [SerializeField] private ParticleSystem _lightningVFX;
        [SerializeField] private float _stormScale = 500f;

        [Header("Visibility")]
        [SerializeField] private float _visibilityDistance = 50000f;

        private Renderer _stormRenderer;
        private static readonly int LightningFlashProperty = Shader.PropertyToID("_LightningFlash");

        public static Dictionary<ushort, StormController> ClientControllers { get; } = new Dictionary<ushort, StormController>();

        private void Awake()
        {
            _stormRenderer = GetComponent<Renderer>();

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            meshFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (_stormMaterial == null && _stormRenderer != null)
            {
                _stormMaterial = _stormRenderer.material;
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
            transform.localScale = Vector3.one * _stormScale * intensity;

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
            transform.localScale = Vector3.one * _stormScale * _intensity;

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