# CLOUD_system — Implementation Plan v0.3

**Версия:** 0.3 (Critical Corrections) | **Дата:** 3 мая 2026 | **Status:** 🔴 Planning

---

## Critical Changes from v0.2

| Issue | v0.2 | v0.3 |
|-------|------|------|
| Sky Dome | As cloud layer | Sky renderer only |
| Distant impostors | Follow camera | Fixed world positions |
| Cloud count | 150 | ~390 near + 140 distant |
| Generation | Per-scene | Player-centered recycling |
| Layer type | Distance-based | Altitude-based |

---

## Architecture Summary

```
BootstrapScene.unity (Never unloaded)
├── WindManager                          ← Central wind, receives server RPC
├── ServerWeatherController (NetworkBehaviour)
├── ServerStormManager (NetworkBehaviour)
├── StormController visuals (5, DontDestroyOnLoad)
└── CloudManager (DontDestroyOnLoad)
    ├── NearCloudRenderer Upper (6000-8000m, 80 clouds)
    ├── NearCloudRenderer Middle (3000-5000m, 120 clouds)
    ├── NearCloudRenderer Lower (1500-3000m, 80 clouds)
    └── DistantCloudManager (140 impostors at world positions)
```

---

## Phase 1: Wind System + CloudManager

**Duration:** Week 1
**Goal:** Server-authoritative wind, all clients synchronized
**Test:** Two clients — clouds move in same direction

### 1.1 Create WindManager

```csharp
// WindManager.cs
public class WindManager : MonoBehaviour
{
    public static WindManager Instance { get; private set; }

    [Header("Current Wind State")]
    public Vector3 CurrentWindDirection = Vector3.right;
    public float CurrentWindSpeed = 5f;

    [Header("Interpolation")]
    [SerializeField] private float _interpolationSpeed = 0.5f;

    private Vector3 _targetDirection;
    private float _targetSpeed;

    public void ApplyWindUpdate(Vector3 direction, float speed)
    {
        _targetDirection = direction.normalized;
        _targetSpeed = speed;
    }

    private void Update()
    {
        // Smooth interpolation
        CurrentWindDirection = Vector3.Lerp(
            CurrentWindDirection,
            _targetDirection,
            _interpolationSpeed * Time.deltaTime
        );
        CurrentWindSpeed = Mathf.Lerp(
            CurrentWindSpeed,
            _targetSpeed,
            _interpolationSpeed * Time.deltaTime
        );
    }
}
```

### 1.2 Create ServerWeatherController

```csharp
// ServerWeatherController.cs
public class ServerWeatherController : NetworkBehaviour
{
    [SerializeField] private Vector3 _windDirection = Vector3.right;
    [SerializeField] private float _windSpeed = 5f;
    [SerializeField] private float _broadcastInterval = 2f;

    private float _timer = 0f;

    private void Update()
    {
        if (!IsServer) return;

        _timer += Time.deltaTime;
        if (_timer >= _broadcastInterval)
        {
            BroadcastWindClientRpc(_windDirection, _windSpeed);
            _timer = 0f;
        }
    }

    [ClientRpc]
    private void BroadcastWindClientRpc(Vector3 direction, float speed)
    {
        WindManager.Instance?.ApplyWindUpdate(direction, speed);
    }
}
```

### 1.3 Refactor CloudManager

```csharp
// CloudManager.cs (replaces CloudSystem)
public class CloudManager : MonoBehaviour
{
    public static CloudManager Instance { get; private set; }

    [Header("References")]
    public NearCloudRenderer upperLayer;
    public NearCloudRenderer middleLayer;
    public NearCloudRenderer lowerLayer;
    public DistantCloudManager distantManager;

    [Header("Settings")]
    [SerializeField] private int _upperCloudCount = 80;
    [SerializeField] private int _middleCloudCount = 120;
    [SerializeField] private int _lowerCloudCount = 80;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Generate initial clouds around player
        RegenerateAllClouds();

        // Subscribe to wind updates
        WindManager.Instance.OnWindUpdated += HandleWindUpdate;
    }

    void HandleWindUpdate(Vector3 direction, float speed)
    {
        upperLayer?.SetWind(direction, speed);
        middleLayer?.SetWind(direction, speed);
        lowerLayer?.SetWind(direction, speed);
        distantManager?.SetWind(direction, speed);
    }

    [ContextMenu("Regenerate All Clouds")]
    public void RegenerateAllClouds()
    {
        Vector3 playerPos = GetPlayerPosition();

        upperLayer?.Generate(playerPos, _upperCloudCount, 6000f, 8000f);
        middleLayer?.Generate(playerPos, _middleCloudCount, 3000f, 5000f);
        lowerLayer?.Generate(playerPos, _lowerCloudCount, 1500f, 3000f);
        distantManager?.Generate();
    }

    Vector3 GetPlayerPosition()
    {
        var ps = GameObject.FindGameObjectWithTag("Player");
        return ps?.transform.position ?? Vector3.zero;
    }
}
```

### 1.4 Test Plan

```
TEST 1: Wind Sync Single Client
1. Run as Host
2. Log wind direction
3. Expected: WindManager receives from server

TEST 2: Wind Sync Two Clients
1. Host + Client
2. Video record both
3. Expected: Same wind direction visible

TEST 3: Wind Change Propagation
1. From host, change wind direction
2. Expected: Both clients update within 5 seconds
```

### 1.5 Success Criteria

- [ ] WindManager exists and receives server broadcasts
- [ ] All cloud systems (upper/middle/lower/distant) receive wind updates
- [ ] Both clients see same wind direction

---

## Phase 2: Instanced Near Clouds

**Duration:** Week 1-2
**Goal:** 280 near clouds (80+120+80) with GPU instancing
**Test:** Clouds around player, recycle at 15km

### 2.1 Create NearCloudRenderer

```csharp
// NearCloudRenderer.cs
public class NearCloudRenderer : MonoBehaviour
{
    [Header("Layer Settings")]
    public float minAltitude = 3000f;
    public float maxAltitude = 5000f;

    [Header("Cloud Settings")]
    [SerializeField] private int _cloudCount = 100;
    [SerializeField] private float _cloudSize = 100f;
    [SerializeField] private Material _cloudMaterial;

    [Header("Wind")]
    private Vector3 _windDirection = Vector3.right;
    private float _windSpeed = 5f;

    // Instancing data
    private Matrix4x4[] _matrices;
    private Vector4[] _colors; // Per-instance color
    private Vector3[] _velocities; // Per-cloud morph offset
    private const int MAX_CLOUDS = 150;

    private Mesh _cloudMesh;
    private int _activeCount = 0;

    private void Start()
    {
        // Use low-poly sphere or custom cloud mesh
        _cloudMesh = CreateCloudMesh();
        _matrices = new Matrix4x4[MAX_CLOUDS];
        _colors = new Vector4[MAX_CLOUDS];
        _velocities = new Vector3[MAX_CLOUDS];
    }

    public void Generate(Vector3 playerPos, int count, float minAlt, float maxAlt)
    {
        minAltitude = minAlt;
        maxAltitude = maxAlt;
        _cloudCount = count;

        // Generate cloud positions around player
        for (int i = 0; i < _cloudCount; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Random.value) * 5000f; // 5km

            Vector3 pos = playerPos + new Vector3(
                Mathf.Cos(angle) * radius,
                Random.Range(minAltitude, maxAltitude),
                Mathf.Sin(angle) * radius
            );

            float scale = _cloudSize * Random.Range(0.5f, 1.5f);

            _matrices[i] = Matrix4x4.TRS(pos, Quaternion.Euler(0, Random.value * 360f, 0),
                new Vector3(scale, scale * 0.6f, scale));

            _colors[i] = new Vector4(1, 1, 1, Random.Range(0.3f, 0.7f));
            _velocities[i] = Random.insideUnitSphere * 0.1f;
        }

        _activeCount = _cloudCount;
    }

    private void Update()
    {
        if (_activeCount == 0) return;

        // Apply wind movement to matrices
        Vector3 windOffset = _windDirection * _windSpeed * Time.deltaTime;

        for (int i = 0; i < _activeCount; i++)
        {
            var pos = _matrices[i].GetColumn(3);
            pos += windOffset;

            // Recycle if too far from player
            if (Vector3.Distance(pos, GetPlayerPosition()) > 15000f)
            {
                // Recycle to opposite side
                Vector3 playerPos = GetPlayerPosition();
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Random.Range(8000f, 12000f);
                pos = playerPos + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Random.Range(minAltitude, maxAltitude),
                    Mathf.Sin(angle) * radius
                );
            }

            _matrices[i].SetColumn(3, pos);
        }

        // Draw instanced
        Graphics.DrawMeshInstanced(_cloudMesh, 0, _cloudMaterial,
            _matrices, _activeCount, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
    }

    public void SetWind(Vector3 direction, float speed)
    {
        _windDirection = direction;
        _windSpeed = speed;
    }

    Vector3 GetPlayerPosition()
    {
        var ps = GameObject.FindGameObjectWithTag("Player");
        return ps?.transform.position ?? Vector3.zero;
    }

    Mesh CreateCloudMesh()
    {
        // Low-poly sphere or custom cloud shape
        var mesh = UnityEngine.MeshGenerator.CreateSphere(4); // Low detail
        return mesh;
    }
}
```

### 2.2 Create CloudMaterial with Instancing

```
Inspector Settings:
- Enable GPU Instancing: CHECKED
- Set _LightInfluence = 0.4
- Configure rim glow etc.
```

### 2.3 Test Plan

```
TEST 1: Cloud Generation
1. Run scene
2. Count active clouds (should be 280)
3. Expected: 280 clouds around player

TEST 2: Wind Movement
1. Set wind direction
2. Expected: All clouds move in wind direction

TEST 3: Cloud Recycling
1. Wait for cloud to drift 15km
2. Expected: Cloud repositions, no pop

TEST 4: Performance
1. Profile GPU
2. Expected: <1.5ms for near clouds
```

### 2.4 Success Criteria

- [ ] 280 near clouds generated around player
- [ ] GPU instancing working (single draw call per layer)
- [ ] Clouds move with wind direction
- [ ] Clouds recycle at 15km, reposition to 8-12km
- [ ] Performance <1.5ms GPU

---

## Phase 3: Distant Impostors at World Positions

**Duration:** Week 2
**Goal:** 140 distant billboards at fixed world positions
**Test:** Same impostors visible to all clients

### 3.1 Create DistantCloudManager

```csharp
// DistantCloudManager.cs
public class DistantCloudManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _impostorCount = 140;
    [SerializeField] private float _minDistance = 5000f;  // 5km
    [SerializeField] private float _maxDistance = 15000f; // 15km
    [SerializeField] private Material _impostorMaterial;

    [Header("Wind")]
    private Vector3 _windDirection = Vector3.right;
    private float _windSpeed = 5f;

    private Matrix4x4[] _matrices;
    private Mesh _quadMesh;
    private float _worldOriginX = 0f;
    private float _worldOriginZ = 0f;

    private void Start()
    {
        _quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        _matrices = new Matrix4x4[_impostorCount];

        // Generate at WORLD positions (ring around WORLD ORIGIN, not player)
        GenerateImpostors();
    }

    void GenerateImpostors()
    {
        // Distribute impostors in a ring around world origin
        // This ensures ALL players see the same impostors

        for (int i = 0; i < _impostorCount; i++)
        {
            float angle = (float)i / _impostorCount * Mathf.PI * 2f;
            float dist = Random.Range(_minDistance, _maxDistance);

            // Position relative to world origin (0,0,0)
            Vector3 pos = new Vector3(
                Mathf.Cos(angle) * dist,
                Random.Range(2000f, 7000f),  // Various altitudes
                Mathf.Sin(angle) * dist
            );

            float scaleX = Random.Range(500f, 2000f);
            float scaleY = scaleX * Random.Range(0.3f, 0.8f);

            Quaternion rot = Quaternion.Euler(-90f, Random.value * 360f, 0f);
            _matrices[i] = Matrix4x4.TRS(pos, rot, new Vector3(scaleX, scaleY, 1f));
        }
    }

    private void Update()
    {
        if (_impostorCount == 0) return;

        // Apply wind movement (world positions move with wind)
        Vector3 windOffset = _windDirection * _windSpeed * Time.deltaTime;

        for (int i = 0; i < _impostorCount; i++)
        {
            var pos = _matrices[i].GetColumn(3);
            pos += windOffset;

            // Wrap around world origin
            float distFromOrigin = pos.magnitude;
            if (distFromOrigin > 20000f)
            {
                pos = -pos.normalized * Random.Range(5000f, 10000f);
            }

            _matrices[i].SetColumn(3, pos);
        }

        // Draw instanced
        Graphics.DrawMeshInstanced(_quadMesh, 0, _impostorMaterial,
            _matrices, _impostorCount, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
    }

    public void SetWind(Vector3 direction, float speed)
    {
        _windDirection = direction;
        _windSpeed = speed;
    }
}
```

### 3.2 Test Plan

```
TEST 1: Impostor Visibility
1. Run Host + Client
2. Both observe distant clouds
3. Log positions
4. Expected: Same world positions on both

TEST 2: Wind Sync
1. Change wind direction on server
2. Expected: Both clients see impostors moving same direction

TEST 3: All Players See Same Clouds
1. Player A at (40000, 3000, 40000)
2. Player B at (120000, 3000, 40000) — 80km away
3. Both see same distant impostors at same world positions
```

### 3.3 Success Criteria

- [ ] 140 distant impostors at fixed world positions
- [ ] All clients see same impostors
- [ ] Impostors move with wind for all clients
- [ ] <0.5ms GPU for distant layer

---

## Phase 4: Storm Authority

**Duration:** Week 2-3
**Goal:** 5 server-authoritative storms at world positions
**Test:** Storms at same positions for all clients

### 4.1 Create ServerStormManager

```csharp
// ServerStormManager.cs
public class ServerStormManager : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _maxStorms = 5;
    [SerializeField] private GameObject _stormPrefab;

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

    private Vector3 _windDirection = Vector3.right;
    private float _windSpeed = 5f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnInitialStorms();
        }
    }

    void SpawnInitialStorms()
    {
        for (int i = 0; i < _maxStorms; i++)
        {
            SpawnStorm();
        }
    }

    void SpawnStorm()
    {
        // Random world position
        float angle = Random.value * Mathf.PI * 2f;
        float dist = Random.Range(5000f, 20000f);
        Vector3 pos = new Vector3(
            Mathf.Cos(angle) * dist,
            1200f, // veil height base
            Mathf.Sin(angle) * dist
        );

        _activeStorms.Add(new StormData
        {
            Id = _nextId++,
            WorldPosition = pos,
            Intensity = Random.Range(0.7f, 1f),
            LightningActive = false,
            TimeSinceLightning = Random.Range(0f, 30f)
        });

        // Notify clients
        StormSpawnClientRpc(_activeStorms[_activeStorms.Count - 1].Id, pos,
            _activeStorms[_activeStorms.Count - 1].Intensity);
    }

    private void Update()
    {
        if (!IsServer) return;

        // Move storms with wind
        for (int i = 0; i < _activeStorms.Count; i++)
        {
            var storm = _activeStorms[i];
            storm.WorldPosition += _windDirection * _windSpeed * Time.deltaTime;
            storm.TimeSinceLightning += Time.deltaTime;

            // Trigger lightning randomly
            if (storm.TimeSinceLightning > Random.Range(10f, 30f))
            {
                storm.LightningActive = true;
                storm.TimeSinceLightning = 0f;
            }
            else
            {
                storm.LightningActive = false;
            }

            _activeStorms[i] = storm;
        }

        // Periodic sync (0.5 Hz)
        if (Time.frameCount % 120 == 0)
        {
            SyncStormStatesClientRpc(
                _activeStorms.Select(s => s.Id).ToArray(),
                _activeStorms.Select(s => s.WorldPosition).ToArray(),
                _activeStorms.Select(s => s.Intensity).ToArray(),
                _activeStorms.Select(s => s.LightningActive).ToArray()
            );
        }
    }

    [ClientRpc]
    void StormSpawnClientRpc(ushort id, Vector3 pos, float intensity) { }

    [ClientRpc]
    void SyncStormStatesClientRpc(ushort[] ids, Vector3[] positions,
        float[] intensities, bool[] lightnings) { }

    public void SetWind(Vector3 direction, float speed)
    {
        _windDirection = direction;
        _windSpeed = speed;
    }
}
```

### 4.2 Create StormController (Client Visual)

```csharp
// StormController.cs
public class StormController : MonoBehaviour
{
    public ushort StormId { get; private set; }

    private Vector3 _worldPosition;
    private float _intensity;
    private bool _lightningActive;

    [SerializeField] private Material _stormMaterial;
    [SerializeField] private ParticleSystem _lightningVFX;

    public void Initialize(ushort id, Vector3 worldPos, float intensity)
    {
        StormId = id;
        _worldPosition = worldPos;
        _intensity = intensity;
        transform.position = worldPos;
    }

    public void UpdateState(Vector3 worldPos, float intensity, bool lightning)
    {
        _worldPosition = worldPos;
        _intensity = intensity;

        // Smooth position interpolation
        transform.position = Vector3.Lerp(transform.position, worldPos, 0.1f);

        if (lightning && !_lightningActive)
        {
            TriggerLightning();
        }

        _lightningActive = lightning;
    }

    void TriggerLightning()
    {
        if (_lightningVFX != null)
        {
            _lightningVFX.Emit(10);
        }

        // Flash material
        StartCoroutine(LightningFlashEffect());
    }

    IEnumerator LightningFlashEffect()
    {
        if (_stormMaterial != null)
        {
            _stormMaterial.SetFloat("_LightningFlash", 1f);
            yield return new WaitForSeconds(0.1f);
            _stormMaterial.SetFloat("_LightningFlash", 0f);
        }
    }

    void Update()
    {
        // Hide if too far from player
        float dist = Vector3.Distance(transform.position, GetPlayerPosition());
        gameObject.SetActive(dist < 50000f); // 50km visibility
    }

    Vector3 GetPlayerPosition()
    {
        var ps = GameObject.FindGameObjectWithTag("Player");
        return ps?.transform.position ?? Vector3.zero;
    }
}
```

### 4.3 Test Plan

```
TEST 1: Storm Spawn
1. Run as Host
2. Log storm positions
3. Expected: 5 storms at world positions

TEST 2: Multi-Client
1. Host + Client
2. Both observe storms
3. Expected: Same positions (within 10m)

TEST 3: Storm Movement
1. Record storm position at T=0
2. Wait 30 seconds
3. Expected: Storm moved with wind

TEST 4: Lightning
1. Near storm
2. Observe lightning
3. Expected: Purple VFX + material flash
```

### 4.4 Success Criteria

- [ ] 5 storms spawned at server positions
- [ ] All clients see storms at same world positions
- [ ] Storms move with wind
- [ ] Lightning triggers with VFX + material flash
- [ ] Storms hidden if >50km from player

---

## Phase 5: Shader Improvements

**Duration:** Week 3
**Goal:** CloudGhibli shader improvements for "tasty" visuals
**Test:** Visual quality improvement

### 5.1 Required Shader Changes

```hlsl
// CloudGhibli.shader — ADD:

// 1. Lighting (currently Unlit — WRONG)
_LightInfluence ("Lighting Response", Range(0,1)) = 0.3

// 2. Day/Night Tint
_TintColor1 ("Tint Color 1", Color) = (1,1,1,1)
_TintColor2 ("Tint Color 2", Color) = (1,1,1,1)
_TintBlend ("Tint Blend", Range(0,1)) = 0.0

// 3. Storm Lightning
_LightningFlash ("Lightning Flash", Float) = 0.0

// 4. Extra noise octave (line 97-98 add 3rd sample)
// Currently 2 octaves, need 3
```

### 5.2 Test Plan

```
TEST 1: Day/Night Transition
1. Speed up time of day
2. Expected: Cloud colors transition through palette

TEST 2: Rim Glow with Sun
1. Rotate sun direction
2. Expected: Rim glow changes with sun position

TEST 3: Lightning Flash
1. Trigger lightning via storm
2. Expected: Storm material flashes white briefly
```

### 5.3 Success Criteria

- [ ] 3 noise octaves (not 2)
- [ ] Light influence parameter works
- [ ] Day/night tint blending works
- [ ] Lightning flash parameter works

---

## Phase 6: Polish

**Duration:** Week 4
**Goal:** URP Volume, VFX, final integration
**Test:** Visual appeal, performance

### 6.1 Tasks

- URP Volume with Bloom (threshold 0.8, intensity 0.7)
- Fog configuration
- Lightning VFX Graph
- Full performance profiling

### 6.2 Test Plan

```
TEST 1: Full Integration
1. Run full scene
2. All systems working together
3. Expected: 60 FPS

TEST 2: Visual Quality
1. Screenshot
2. Expected: "Tasty" clouds, not flat primitives

TEST 3: Multi-Client Full Test
1. Host + 2 Clients
2. All synchronized
3. Expected: Same wind, same storms, same clouds
```

---

## Timeline

```
Week 1:   Phase 1 (Wind + CloudManager) + Phase 2 start
Week 2:   Phase 2 complete + Phase 3 (Distant Impostors)
Week 3:   Phase 4 (Storms) + Phase 5 (Shader)
Week 4:   Phase 6 (Polish)
Week 5+:  Iteration, optimization
```

---

**Status:** 🔴 v0.3 — Corrected architecture, ready for implementation