using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;

public class TestStormSpawner : MonoBehaviour
{
    public CloudLayerConfig testPattern;
    public uint testStormId = 1;
    public Vector3 testPosition = new Vector3(0, 1200, 0);

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
                    Debug.Log($"[Test] Using pattern: {pattern.name}, archetype={pattern.archetype}");
                    generator.SpawnStorm(testStormId, testPosition, pattern, 1f);
                    Debug.Log($"[Test] SpawnStorm called for storm {testStormId}");
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

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 80),
            "T - Spawn Storm\nY - Despawn Storm\nU - Despawn All");
    }
}