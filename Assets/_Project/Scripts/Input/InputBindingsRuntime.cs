// Project C: Input System — Phase 2.2
// InputBindingsRuntime: runtime singleton + rebind + persist.
// Phase 2.2: runtime rebind (in-memory, не сохраняется).
// Phase 2.3: PlayerPrefs persistence.

using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Input
{
    public class InputBindingsRuntime : MonoBehaviour
    {
        public static InputBindingsRuntime Instance { get; private set; }

        [Header("Runtime Config (копия для редактирования)")]
        public InputBindingsConfig Config;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("[InputBindingsRuntime]");
            DontDestroyOnLoad(go);
            go.AddComponent<InputBindingsRuntime>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InputBindingsRuntime] Replacing existing Instance (duplicate).");
            }
            Instance = this;

            Config = Resources.Load<InputBindingsConfig>("InputBindingsConfig");
            if (Config == null)
            {
                Debug.LogError("[InputBindingsRuntime] InputBindingsConfig.asset not found in Resources/.");
            }
            else if (Debug.isDebugBuild)
            {
                Debug.Log($"[InputBindingsRuntime] Loaded: {Config.combatSkills?.Count ?? 0} skill + {Config.actions?.Count ?? 0} action bindings");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ===== Action Binding Rebind (Phase 2.2) =====

        /// <summary>
        /// Переназначить action binding на новую клавишу.
        /// Изменения in-memory (не сохраняются между сессиями).
        /// </summary>
        public bool RebindAction(InputBindingsConfig.GameAction action, Key newKey, int newMouseButtonRaw)
        {
            if (Config == null || Config.actions == null) return false;

            for (int i = 0; i < Config.actions.Count; i++)
            {
                if (Config.actions[i].action == action)
                {
                    var a = Config.actions[i];
                    a.key = newKey;
                    a.mouseButtonRaw = newMouseButtonRaw;
                    Config.actions[i] = a; // struct → replace
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Переназначить skill binding (combat).
        /// </summary>
        public bool RebindSkill(ProjectC.Skills.SkillInputSlot slot, int mouseButtonRaw, Key modifier, Key fallbackKey)
        {
            if (Config == null || Config.combatSkills == null) return false;

            for (int i = 0; i < Config.combatSkills.Count; i++)
            {
                if (Config.combatSkills[i].slot == slot)
                {
                    var b = Config.combatSkills[i];
                    b.mouseButtonRaw = mouseButtonRaw;
                    b.modifier = modifier;
                    b.fallbackKey = fallbackKey;
                    Config.combatSkills[i] = b;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Найти displayName для action по GameAction.</summary>
        public string GetActionDisplayName(InputBindingsConfig.GameAction action)
        {
            if (Config == null || Config.actions == null) return action.ToString();
            foreach (var a in Config.actions)
                if (a.action == action) return a.displayName;
            return action.ToString();
        }

        /// <summary>Найти displayName для skill slot.</summary>
        public string GetSkillDisplayName(ProjectC.Skills.SkillInputSlot slot)
        {
            if (Config == null || Config.combatSkills == null) return slot.ToString();
            // Берем ПЕРВЫЙ бинд для этого слота (у слота может быть несколько биндов: мышь + клавиатура)
            foreach (var b in Config.combatSkills)
                if (b.slot == slot) return b.displayName;
            return slot.ToString();
        }
    }
}
