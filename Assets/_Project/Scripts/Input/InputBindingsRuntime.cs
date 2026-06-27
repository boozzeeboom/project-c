// Project C: Input System — Phase 2.3
// InputBindingsRuntime: runtime singleton + rebind + PlayerPrefs persistence.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Input
{
    public class InputBindingsRuntime : MonoBehaviour
    {
        public static InputBindingsRuntime Instance { get; private set; }

        /// <summary>
        /// Runtime-копия config (изменяется в Play Mode).
        /// </summary>
        [Header("Runtime Config (копия для редактирования)")]
        public InputBindingsConfig Config;

        /// <summary>
        /// Дефолтный config (для Reset).
        /// </summary>
        [System.NonSerialized] public InputBindingsConfig DefaultConfig;

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

            // 1) Загружаем дефолтный config из Resources.
            Config = Resources.Load<InputBindingsConfig>("InputBindingsConfig");
            if (Config == null)
            {
                Debug.LogError("[InputBindingsRuntime] InputBindingsConfig.asset not found in Resources/.");
                return;
            }
            // Сохраняем оригинал для Reset.
            DefaultConfig = Instantiate(Config);
            DefaultConfig.name = Config.name + "_Default";

            // 2) Загружаем сохранённые overrides из PlayerPrefs.
            Load();

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[InputBindingsRuntime] Loaded: {Config.combatSkills?.Count ?? 0} skill + {Config.actions?.Count ?? 0} action bindings (persisted)");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ===== Action Binding Rebind (Phase 2.2) =====

        /// <summary>
        /// Переназначить action binding на новую клавишу.
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
                    Config.actions[i] = a;
                    Save();
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
                    Save();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Сбросить все бинды к дефолтным значениям.
        /// </summary>
        public void ResetToDefaults()
        {
            if (DefaultConfig == null) return;
            if (Config.actions != null && DefaultConfig.actions != null)
            {
                Config.actions.Clear();
                Config.actions.AddRange(DefaultConfig.actions);
            }
            if (Config.combatSkills != null && DefaultConfig.combatSkills != null)
            {
                Config.combatSkills.Clear();
                Config.combatSkills.AddRange(DefaultConfig.combatSkills);
            }
            Save();
            Debug.Log("[InputBindingsRuntime] Reset to defaults");
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
            foreach (var b in Config.combatSkills)
                if (b.slot == slot) return b.displayName;
            return slot.ToString();
        }

        // ===== PlayerPrefs Persistence (Phase 2.3) =====

        private const string PREFS_KEY = "ProjectC.InputBindings.v1";

        [Serializable]
        private class ActionOverride
        {
            public InputBindingsConfig.GameAction action;
            public Key key;
            public int mouseButtonRaw;
        }

        [Serializable]
        private class SkillOverride
        {
            public ProjectC.Skills.SkillInputSlot slot;
            public int mouseButtonRaw;
            public Key modifier;
            public Key fallbackKey;
        }

        [Serializable]
        private class BindingsData
        {
            public List<ActionOverride> actions = new();
            public List<SkillOverride> combatSkills = new();
        }

        /// <summary>
        /// Сохранить текущие бинды в PlayerPrefs.
        /// </summary>
        public void Save()
        {
            if (Config == null) return;
            var data = new BindingsData();
            if (Config.actions != null)
            {
                foreach (var a in Config.actions)
                {
                    data.actions.Add(new ActionOverride { action = a.action, key = a.key, mouseButtonRaw = a.mouseButtonRaw });
                }
            }
            if (Config.combatSkills != null)
            {
                foreach (var b in Config.combatSkills)
                {
                    data.combatSkills.Add(new SkillOverride { slot = b.slot, mouseButtonRaw = b.mouseButtonRaw, modifier = b.modifier, fallbackKey = b.fallbackKey });
                }
            }
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PREFS_KEY, json);
            PlayerPrefs.Save();
            if (Debug.isDebugBuild) Debug.Log($"[InputBindingsRuntime] Saved {data.actions.Count} action + {data.combatSkills.Count} skill bindings");
        }

        /// <summary>
        /// Загрузить бинды из PlayerPrefs.
        /// </summary>
        public void Load()
        {
            if (Config == null) return;
            if (!PlayerPrefs.HasKey(PREFS_KEY)) return;

            string json = PlayerPrefs.GetString(PREFS_KEY);
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var data = JsonUtility.FromJson<BindingsData>(json);
                if (data == null) return;

                if (data.actions != null && Config.actions != null)
                {
                    foreach (var ov in data.actions)
                    {
                        for (int i = 0; i < Config.actions.Count; i++)
                        {
                            if (Config.actions[i].action == ov.action)
                            {
                                var a = Config.actions[i];
                                a.key = ov.key;
                                a.mouseButtonRaw = ov.mouseButtonRaw;
                                Config.actions[i] = a;
                                break;
                            }
                        }
                    }
                }
                if (data.combatSkills != null && Config.combatSkills != null)
                {
                    foreach (var ov in data.combatSkills)
                    {
                        for (int i = 0; i < Config.combatSkills.Count; i++)
                        {
                            if (Config.combatSkills[i].slot == ov.slot)
                            {
                                var b = Config.combatSkills[i];
                                b.mouseButtonRaw = ov.mouseButtonRaw;
                                b.modifier = ov.modifier;
                                b.fallbackKey = ov.fallbackKey;
                                Config.combatSkills[i] = b;
                                break;
                            }
                        }
                    }
                }
                if (Debug.isDebugBuild) Debug.Log($"[InputBindingsRuntime] Loaded overrides: {data.actions?.Count ?? 0} action + {data.combatSkills?.Count ?? 0} skill");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputBindingsRuntime] Failed to load bindings: {e.Message}");
                PlayerPrefs.DeleteKey(PREFS_KEY);
            }
        }
    }
}

