using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Player;

namespace ProjectC.World
{
    [Serializable]
    public struct HouseDiscoveryEntry
    {
        public string houseId;
        public int number;
        public string chunkId;
        public string city;
        public Vector3 worldPos;
        public double utcSeconds;
    }

    /// <summary>
    /// Discovery домов: открывает дома при подходе (15 м) и запоминает per-player.
    /// </summary>
    [DisallowMultipleComponent]
    public class HouseDiscovery : MonoBehaviour
    {
        public static HouseDiscovery Instance { get; private set; }

        public static void EnsureExists()
        {
            if (Instance != null || !Application.isPlaying) return;
            var go = new GameObject("HouseDiscovery");
            DontDestroyOnLoad(go);
            go.AddComponent<HouseDiscovery>();
        }

        [Header("Discovery")]
        [SerializeField] private float _discoveryRadius = 15f;

        [Header("Сейв")]
        [SerializeField] private float _saveIntervalSeconds = 30f;

        private readonly List<HouseDiscoveryEntry> _discovered = new List<HouseDiscoveryEntry>();
        private bool _loaded;
        private ulong _clientId;
        private bool _dirty;
        private float _lastSaveTime;
        private float _lastCheckTime;
        private const float CheckInterval = 0.5f;

        public IReadOnlyList<HouseDiscoveryEntry> Discovered => _discovered;
        public int Count => _discovered.Count;

        public static event Action<HouseDiscoveryEntry> OnHouseDiscovered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null && Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            EnsureLoaded();

            float now = Time.unscaledTime;
            if (now - _lastCheckTime >= CheckInterval)
            {
                _lastCheckTime = now;
                CheckDiscovery();
            }

            if (_dirty && now - _lastSaveTime >= _saveIntervalSeconds)
                Save();
        }

        private void OnApplicationQuit()
        {
            if (_dirty) Save();
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            _clientId = nm.LocalClientId;
            _loaded = true;
            _lastSaveTime = Time.unscaledTime;

            if (HouseDiscoveryStore.TryLoad(_clientId, out var data))
            {
                var markerMap = new Dictionary<string, HouseMarker>();
                foreach (var mm in FindObjectsByType<HouseMarker>())
                    if (mm != null && !string.IsNullOrEmpty(mm.houseId) && !markerMap.ContainsKey(mm.houseId))
                        markerMap[mm.houseId] = mm;

                _discovered.Clear();
                int skipped = 0;
                foreach (var d in data.houses)
                {
                    if (string.IsNullOrEmpty(d.houseId)) { skipped++; continue; }
                    if (!markerMap.TryGetValue(d.houseId, out var mk)) { skipped++; continue; }
                    _discovered.Add(new HouseDiscoveryEntry
                    {
                        houseId = d.houseId,
                        number = d.number,
                        chunkId = d.chunkId,
                        city = d.city,
                        worldPos = mk.MapPosition,
                        utcSeconds = d.utc,
                    });
                }
                
            }
        }

        private void CheckDiscovery()
        {
            if (!_loaded) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;

            NetworkPlayer localPlayer = null;
            var players = nm.SpawnManager?.PlayerObjects;
            if (players != null)
            {
                foreach (var no in players)
                {
                    if (no == null) continue;
                    var np = no.GetComponent<NetworkPlayer>();
                    if (np != null && np.IsOwner) { localPlayer = np; break; }
                }
            }
            if (localPlayer == null) return;

            Vector3 pos = localPlayer.transform.position;

            var markers = FindObjectsByType<HouseMarker>();
            
            
            

            foreach (var m in markers)
            {
                if (m == null) continue;
                bool already = false;
                foreach (var d in _discovered)
                {
                    if (d.houseId == m.houseId) { already = true; break; }
                }
                if (already) continue;

                float dist = Vector3.Distance(pos, m.MapPosition);
                if (dist <= _discoveryRadius)
                {
                    var entry = new HouseDiscoveryEntry
                    {
                        houseId = m.houseId,
                        number = m.number,
                        chunkId = m.chunkId,
                        city = m.city,
                        worldPos = m.MapPosition,
                        utcSeconds = ChartTrackRecorder.NowUtcSeconds(),
                    };
                    _discovered.Add(entry);
                    _dirty = true;
                    OnHouseDiscovered?.Invoke(entry);
                    
                }
            }
        }

        public void Save()
        {
            if (!_loaded) return;
            var data = new HouseDiscoveryData();
            foreach (var e in _discovered)
            {
                data.houses.Add(new HouseDiscoveryDto
                {
                    houseId = e.houseId,
                    number = e.number,
                    chunkId = e.chunkId,
                    city = e.city,
                    x = e.worldPos.x,
                    y = e.worldPos.y,
                    z = e.worldPos.z,
                    utc = e.utcSeconds,
                });
            }
            HouseDiscoveryStore.Save(_clientId, data);
            _dirty = false;
            _lastSaveTime = Time.unscaledTime;
        }

        public int ApplyRebaseTranslation(Vector3 translation)
        {
            for (int i = 0; i < _discovered.Count; i++)
            {
                var e = _discovered[i];
                e.worldPos += translation;
                _discovered[i] = e;
            }
            return _discovered.Count;
        }

        public bool IsDiscovered(string houseId)
        {
            foreach (var d in _discovered)
                if (d.houseId == houseId) return true;
            return false;
        }
    }
}