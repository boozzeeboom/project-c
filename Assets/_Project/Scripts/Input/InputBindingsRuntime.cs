// Project C: Input System — Phase 1 (MVP)
// InputBindingsRuntime: singleton loader. Загружает InputBindingsConfig из Resources при старте.
//
// Phase 2: будет держать runtime-копию с возможностью UI rebind + сохранением в PlayerPrefs.

using UnityEngine;

namespace ProjectC.Input
{
    /// <summary>
    /// Runtime singleton с активным InputBindingsConfig.
    /// Phase 1: только загрузка из Resources. Phase 2: rebind + persist.
    /// </summary>
    public class InputBindingsRuntime : MonoBehaviour
    {
        public static InputBindingsRuntime Instance { get; private set; }

        public InputBindingsConfig Config { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InputBindingsRuntime] Replacing existing Instance (duplicate).");
            }
            Instance = this;

            // Загружаем asset из Resources (Phase 1 единственный источник правды).
            Config = Resources.Load<InputBindingsConfig>("InputBindingsConfig");
            if (Config == null)
            {
                Debug.LogError("[InputBindingsRuntime] InputBindingsConfig.asset not found in Resources/. Combat skills won't work. Create via 'Create → ProjectC → Input → InputBindingsConfig' menu.");
            }
            else if (Debug.isDebugBuild)
            {
                Debug.Log($"[InputBindingsRuntime] InputBindingsConfig loaded: {Config.combatSkills?.Count ?? 0} bindings");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
