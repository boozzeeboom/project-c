using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;

public class TestStormSpawner : MonoBehaviour
{
    public CloudLayerConfig testPattern;
    public uint testStormId = 1;
    public Vector3 testPosition = new Vector3(0, 1200, 0);

    [Header("Random Spawn Settings")]
    public bool useRandomPosition = true;
    [Tooltip("Minimum distance from center to spawn storm (units)")]
    [SerializeField] private float spawnMinDistance = 5000f;
    [Tooltip("Maximum distance from center to spawn storm (units)")]
    [SerializeField] private float spawnMaxDistance = 35000f;
    [Tooltip("Base altitude for storm spawning")]
    [SerializeField] private float baseAltitude = 1200f;
    [Tooltip("Altitude variation (+/-)")]
    [SerializeField] private float altitudeVariation = 500f;

    private Keyboard _keyboard;

    void Awake()
    {
        _keyboard = Keyboard.current;
    }

    void Update()
    {
        if (_keyboard == null) return;

        if (_keyboard.tKey.wasPressedThisFrame)
        {
            var generator = FindAnyObjectByType<StormCloudGenerator>();
            if (generator != null)
            {
                var pattern = testPattern != null ? testPattern : generator.defaultStormPattern;
                if (pattern != null)
                {
                    Vector3 spawnPos = useRandomPosition
                        ? GetRandomStormPosition()
                        : testPosition;

                    Debug.Log($"[Test] Using pattern: {pattern.name}, archetype={pattern.archetype}");
                    generator.SpawnStorm(testStormId, spawnPos, pattern, 1f);
                    Debug.Log($"[Test] SpawnStorm called for storm {testStormId} at {spawnPos}");
                }
                else
                {
                    Debug.LogError("[Test] No pattern assigned! Set testPattern or assign defaultStormPattern on StormCloudGenerator");
                }
            }
            else
            {
                Debug.LogError("[Test] StormCloudGenerator not found in scene!");
            }
        }

        if (_keyboard.yKey.wasPressedThisFrame)
        {
            var generator = FindAnyObjectByType<StormCloudGenerator>();
            if (generator != null)
            {
                generator.DespawnStorm(testStormId);
                Debug.Log($"[Test] Despawned storm {testStormId}");
            }
        }

        if (_keyboard.uKey.wasPressedThisFrame)
        {
            var generator = FindAnyObjectByType<StormCloudGenerator>();
            if (generator != null)
            {
                generator.DespawnAll();
                Debug.Log($"[Test] Despawned ALL storms");
            }
        }
    }

    private Vector3 GetRandomStormPosition()
    {
        float angle = Random.value * Mathf.PI * 2f;
        float dist = Random.Range(spawnMinDistance, spawnMaxDistance);

        float x = Mathf.Cos(angle) * dist;
        float y = baseAltitude + Random.Range(-altitudeVariation, altitudeVariation);
        float z = Mathf.Sin(angle) * dist;

        return new Vector3(x, y, z);
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 100),
            "T - Spawn Storm\nY - Despawn Storm\nU - Despawn All\nRandom: " + useRandomPosition);
    }
}