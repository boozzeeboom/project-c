using Unity.Netcode;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace ProjectC.Core
{
    /// <summary>
    /// Server-authoritative storm manager.
    /// Manages 5 storms at world-space positions, synchronized to all clients.
    /// Storms move with wind and trigger lightning effects.
    /// </summary>
    public class ServerStormManager : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int _maxStorms = 5;
        [Tooltip("Minimum distance from center to spawn storm (units)")]
        [SerializeField] private float _spawnMinDistance = 5000f;
        [Tooltip("Maximum distance from center to spawn storm (units)")]
        [SerializeField] private float _spawnMaxDistance = 35000f;

        [Header("Movement")]
        [SerializeField] private float _baseAltitude = 1200f;
        [SerializeField] private float _altitudeVariation = 500f;

        [Header("Lightning")]
        [SerializeField] private float _lightningIntervalMin = 10f;
        [SerializeField] private float _lightningIntervalMax = 30f;

        [Header("Sync")]
        [SerializeField] private float _syncInterval = 2f;

        [Header("Storm Patterns")]
        [SerializeField] private CloudLayerConfig[] _stormPatterns = new CloudLayerConfig[0];
        [SerializeField] private bool _useRandomPattern = true;

        [Header("Spawn Prefab")]
        [SerializeField] private GameObject _stormControllerPrefab;

        [Header("Debug Logging")]
        [SerializeField] private bool _logSpawn = false;
        [SerializeField] private bool _logStormSpawn = false;
        [SerializeField] private bool _logEventCloud = false;

        private struct StormData
        {
            public ushort Id;
            public Vector3 WorldPosition;
            public float Intensity;
            public bool LightningActive;
            public float TimeSinceLightning;
            public string PatternGUID;
        }

        private List<StormData> _activeStorms = new List<StormData>();
        private ushort _nextId = 0;
        private float _syncTimer = 0f;
        private float _globalStormIntensity = 0f;

        public static ServerStormManager Instance { get; private set; }

        public event System.Action<ushort, Vector3, float, string> OnEventCloudSpawnRequested;
        public event System.Action<ushort> OnEventCloudRemoveRequested;
        public event System.Action<float> OnGlobalStormIntensityChanged;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Instance = this;
                SpawnInitialStorms();
                if (_logSpawn) Debug.Log($"[ServerStormManager] Server spawned, {Instance._maxStorms} storms active");
            }
            else
            {
                enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                Instance = null;
            }
        }

        private void SpawnInitialStorms()
        {
            for (int i = 0; i < _maxStorms; i++)
            {
                SpawnStorm();
            }
        }

        private void SpawnStorm()
        {
            float angle = Random.value * Mathf.PI * 2f;
            float dist = Random.Range(_spawnMinDistance, _spawnMaxDistance);

            Vector3 pos = new Vector3(
                Mathf.Cos(angle) * dist,
                _baseAltitude + Random.Range(-_altitudeVariation, _altitudeVariation),
                Mathf.Sin(angle) * dist
            );

            string patternGuid = "";
            if (_stormPatterns != null && _stormPatterns.Length > 0)
            {
                int patternIndex = _useRandomPattern ? Random.Range(0, _stormPatterns.Length) : 0;
                var pattern = _stormPatterns[patternIndex];
                patternGuid = GetAssetGuid(pattern);
            }

            StormData storm = new StormData
            {
                Id = _nextId++,
                WorldPosition = pos,
                Intensity = Random.Range(0.7f, 1f),
                LightningActive = false,
                TimeSinceLightning = Random.Range(0f, _lightningIntervalMax),
                PatternGUID = patternGuid
            };

            _activeStorms.Add(storm);

            StormSpawnClientRpc(storm.Id, storm.WorldPosition, storm.Intensity, storm.PatternGUID);
        }

        private string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null) return "";
            string path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return "";
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            return guid;
        }

        private void Update()
        {
            if (!IsServer) return;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return;
            }

            _syncTimer += Time.deltaTime;

            Vector3 windDir = Vector3.right;
            float windSpeed = 0f;

            if (WindManager.Instance != null)
            {
                windDir = WindManager.Instance.CurrentWindDirection;
                windSpeed = WindManager.Instance.CurrentWindSpeed;
            }

            for (int i = 0; i < _activeStorms.Count; i++)
            {
                var storm = _activeStorms[i];

                storm.WorldPosition += windDir * windSpeed * Time.deltaTime;
                storm.TimeSinceLightning += Time.deltaTime;

                bool shouldLightning = storm.TimeSinceLightning > Random.Range(_lightningIntervalMin, _lightningIntervalMax);

                if (shouldLightning && !storm.LightningActive)
                {
                    storm.LightningActive = true;
                    TriggerLightningClientRpc(storm.Id);
                }
                else if (!shouldLightning)
                {
                    storm.LightningActive = false;
                }

                if (storm.TimeSinceLightning > _lightningIntervalMax)
                {
                    storm.TimeSinceLightning = 0f;
                }

                _activeStorms[i] = storm;
            }

            if (_syncTimer >= _syncInterval)
            {
                _globalStormIntensity = CalculateGlobalStormIntensity();
                OnGlobalStormIntensityChanged?.Invoke(_globalStormIntensity);

                SyncStormStatesClientRpc(
                    _activeStorms.Select(s => s.Id).ToArray(),
                    _activeStorms.Select(s => s.WorldPosition).ToArray(),
                    _activeStorms.Select(s => s.Intensity).ToArray()
                );
                _syncTimer = 0f;
            }
        }

        private float CalculateGlobalStormIntensity()
        {
            if (_activeStorms.Count == 0) return 0f;

            float totalIntensity = 0f;
            foreach (var storm in _activeStorms)
            {
                totalIntensity += storm.Intensity;
            }

            float avgIntensity = totalIntensity / _maxStorms;
            return Mathf.Clamp01(avgIntensity);
        }

        [ClientRpc]
        private void StormSpawnClientRpc(ushort id, Vector3 worldPosition, float intensity, string patternGUID)
        {
            if (_logStormSpawn) Debug.Log($"[StormSpawnClientRpc] id={id}, worldPos={worldPosition}, intensity={intensity}, patternGUID={patternGUID}");

            if (StormController.ClientControllers.TryGetValue(id, out var controller))
            {
                if (_logStormSpawn) Debug.Log($"[StormSpawnClientRpc] Found controller {controller.gameObject.name}, calling Initialize");
                controller.Initialize(id, worldPosition, intensity, patternGUID);
            }
            else if (_stormControllerPrefab != null)
            {
                var go = Instantiate(_stormControllerPrefab, worldPosition, Quaternion.identity);
                if (go.TryGetComponent<StormController>(out var newController))
                {
                    newController.Initialize(id, worldPosition, intensity, patternGUID);
                }
                if (_logStormSpawn) Debug.Log($"[StormSpawnClientRpc] Spawned new controller for storm {id}");
            }
            else
            {
                if (_logStormSpawn) Debug.LogWarning($"[StormSpawnClientRpc] No controller found for id={id} and no prefab set. Registered: {StormController.ClientControllers.Count}");
            }
        }

        [ClientRpc]
        private void TriggerLightningClientRpc(ushort stormId)
        {
            if (IsServer) return;

            if (StormController.ClientControllers.TryGetValue(stormId, out var controller))
            {
                controller.TriggerLightning();
            }
        }

        [ClientRpc]
        private void SyncStormStatesClientRpc(ushort[] ids, Vector3[] positions, float[] intensities)
        {
            if (IsServer) return;

            for (int i = 0; i < ids.Length; i++)
            {
                if (StormController.ClientControllers.TryGetValue(ids[i], out var controller))
                {
                    controller.UpdateState(positions[i], intensities[i]);
                }
            }

            _globalStormIntensity = CalculateGlobalStormIntensity();
            GlobalStormEvents.BroadcastStormIntensity(_globalStormIntensity);
        }

        public void TriggerEventCloud(Vector3 position, CloudLayerConfig pattern, float intensity, string eventId)
        {
            if (!IsServer) return;

            string patternGuid = GetAssetGuid(pattern);

            StormData storm = new StormData
            {
                Id = _nextId++,
                WorldPosition = position,
                Intensity = intensity,
                LightningActive = false,
                TimeSinceLightning = 0f,
                PatternGUID = patternGuid
            };

            _activeStorms.Add(storm);

            EventCloudSpawnClientRpc(storm.Id, storm.WorldPosition, storm.Intensity, storm.PatternGUID, eventId);

            OnEventCloudSpawnRequested?.Invoke(storm.Id, position, intensity, patternGuid);
        }

        [ClientRpc]
        private void EventCloudSpawnClientRpc(ushort id, Vector3 worldPosition, float intensity, string patternGUID, string eventId)
        {
            if (_logEventCloud) Debug.Log($"[EventCloudSpawnClientRpc] id={id}, eventId={eventId}, pos={worldPosition}");

            if (StormController.ClientControllers.TryGetValue(id, out var controller))
            {
                controller.Initialize(id, worldPosition, intensity, patternGUID);
            }
            else if (_stormControllerPrefab != null)
            {
                var go = Instantiate(_stormControllerPrefab, worldPosition, Quaternion.identity);
                if (go.TryGetComponent<StormController>(out var newController))
                {
                    newController.Initialize(id, worldPosition, intensity, patternGUID);
                }
            }
        }

        public void RemoveEventCloud(ushort stormId)
        {
            if (!IsServer) return;

            int index = _activeStorms.FindIndex(s => s.Id == stormId);
            if (index >= 0)
            {
                _activeStorms.RemoveAt(index);
                EventCloudRemoveClientRpc(stormId);
                OnEventCloudRemoveRequested?.Invoke(stormId);
            }
        }

        [ClientRpc]
        private void EventCloudRemoveClientRpc(ushort stormId)
        {
            if (StormController.ClientControllers.TryGetValue(stormId, out var controller))
            {
                controller.Despawn();
            }
        }

        public float GetStormIntensityAtPosition(Vector3 playerPosition)
        {
            if (_activeStorms.Count == 0) return 0f;

            float maxIntensity = 0f;
            float proximityRadius = 5000f;

            foreach (var storm in _activeStorms)
            {
                float dist = Vector3.Distance(playerPosition, storm.WorldPosition);
                if (dist < proximityRadius)
                {
                    float proximityFactor = 1f - (dist / proximityRadius);
                    float effectiveIntensity = storm.Intensity * proximityFactor;
                    maxIntensity = Mathf.Max(maxIntensity, effectiveIntensity);
                }
            }

            return Mathf.Clamp01(maxIntensity);
        }

        public float GlobalStormIntensity => _globalStormIntensity;
    }
}