using Unity.Netcode;
using UnityEngine;
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
        [SerializeField] private float _spawnMinDistance = 5000f;
        [SerializeField] private float _spawnMaxDistance = 20000f;

        [Header("Movement")]
        [SerializeField] private float _baseAltitude = 1200f;
        [SerializeField] private float _altitudeVariation = 500f;

        [Header("Lightning")]
        [SerializeField] private float _lightningIntervalMin = 10f;
        [SerializeField] private float _lightningIntervalMax = 30f;

        [Header("Sync")]
        [SerializeField] private float _syncInterval = 2f;

        private struct StormData
        {
            public ushort Id;
            public Vector3 WorldPosition;
            public float Intensity;
            public bool LightningActive;
            public float TimeSinceLightning;
        }

        private List<StormData> _activeStorms = new List<StormData>();
        private ushort _nextId = 0;
        private float _syncTimer = 0f;

        public static ServerStormManager Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Instance = this;
                SpawnInitialStorms();
                Debug.Log($"[ServerStormManager] Server spawned, {Instance._maxStorms} storms active");
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

            StormData storm = new StormData
            {
                Id = _nextId++,
                WorldPosition = pos,
                Intensity = Random.Range(0.7f, 1f),
                LightningActive = false,
                TimeSinceLightning = Random.Range(0f, _lightningIntervalMax)
            };

            _activeStorms.Add(storm);

            StormSpawnClientRpc(storm.Id, storm.WorldPosition, storm.Intensity);
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
                SyncStormStatesClientRpc(
                    _activeStorms.Select(s => s.Id).ToArray(),
                    _activeStorms.Select(s => s.WorldPosition).ToArray(),
                    _activeStorms.Select(s => s.Intensity).ToArray()
                );
                _syncTimer = 0f;
            }
        }

        [ClientRpc]
        private void StormSpawnClientRpc(ushort id, Vector3 worldPosition, float intensity)
        {
            Debug.Log($"[StormSpawnClientRpc] id={id}, worldPos={worldPosition}, intensity={intensity}");

            if (StormController.ClientControllers.TryGetValue(id, out var controller))
            {
                Debug.Log($"[StormSpawnClientRpc] Found controller {controller.gameObject.name}, calling Initialize");
                controller.Initialize(id, worldPosition, intensity);
            }
            else
            {
                Debug.LogWarning($"[StormSpawnClientRpc] No controller found for id={id}. Registered: {StormController.ClientControllers.Count}");
                foreach (var kvp in StormController.ClientControllers)
                {
                    Debug.Log($"  {kvp.Key}: {kvp.Value.gameObject.name}");
                }
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
        }
    }
}