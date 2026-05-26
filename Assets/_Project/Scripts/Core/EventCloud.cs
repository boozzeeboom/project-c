using System.Collections.Generic;
using UnityEngine;
using ProjectC.CloudGenerator;

namespace ProjectC.Core
{
    public class EventCloud : MonoBehaviour
    {
        [Header("Event Cloud Settings")]
        [SerializeField] private string _eventCloudId = "";
        [SerializeField] private CloudLayerConfig _eventPattern;
        [SerializeField] private string _parentMeshResourcePath = "";

        [Header("Storm Settings")]
        [SerializeField] private float _intensity = 1f;
        [SerializeField] private bool _autoStart = false;

        [Header("References")]
        [SerializeField] private StormCloudGenerator _cloudGenerator;

        private uint _stormId;
        private bool _isSpawned;
        private List<GameObject> _sphereObjects = new List<GameObject>();

        public string EventCloudId => _eventCloudId;

        private void Awake()
        {
            if (_cloudGenerator == null)
            {
                _cloudGenerator = FindAnyObjectByType<StormCloudGenerator>();
            }
        }

        private void Start()
        {
            if (_autoStart)
            {
                SpawnEventCloud();
            }
        }

        public void SetEventId(string eventId)
        {
            _eventCloudId = eventId;
        }

        public void SetPattern(CloudLayerConfig pattern)
        {
            _eventPattern = pattern;
        }

        public void SetIntensity(float intensity)
        {
            _intensity = intensity;
        }

        public void SetParentMeshPath(string path)
        {
            _parentMeshResourcePath = path;
            if (_eventPattern != null)
            {
                _eventPattern.parentMeshPath = path;
            }
        }

        public void SpawnEventCloud()
        {
            if (_isSpawned)
            {
                Debug.LogWarning($"[EventCloud] EventCloud {gameObject.name} already spawned");
                return;
            }

            if (_eventPattern == null)
            {
                Debug.LogError($"[EventCloud] No event pattern set for EventCloud {gameObject.name}");
                return;
            }

            if (!string.IsNullOrEmpty(_parentMeshResourcePath))
            {
                _eventPattern.parentMeshPath = _parentMeshResourcePath;
            }

            if (_stormId == 0)
            {
                var networkObject = GetComponent<Unity.Netcode.NetworkObject>();
                _stormId = networkObject != null ? (uint)networkObject.NetworkObjectId : (uint)GetHashCode();
            }

            if (_stormId == 0)
            {
                _stormId = (uint)Random.Range(1, int.MaxValue);
            }

            _cloudGenerator.SpawnStorm(_stormId, transform.position, _eventPattern, _intensity);
            _isSpawned = true;

            Debug.Log($"[EventCloud] Spawned event cloud '{_eventCloudId}' at {transform.position} with pattern {_eventPattern.name}");
        }

        public void DespawnEventCloud()
        {
            if (!_isSpawned)
            {
                return;
            }

            _cloudGenerator.DespawnStorm(_stormId);
            _isSpawned = false;

            Debug.Log($"[EventCloud] Despawned event cloud '{_eventCloudId}'");
        }

        public void TriggerLightning()
        {
            Debug.Log($"[EventCloud] TriggerLightning called for '{_eventCloudId}'");
        }

        private void OnDestroy()
        {
            if (_isSpawned)
            {
                DespawnEventCloud();
            }
        }
    }
}