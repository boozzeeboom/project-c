// Project C: Character Progression — T-P13
// SkillsClientState: client-side projection learned skills. Singleton MonoBehaviour.
// Design: docs/Character/06_SKILL_TREE.md, docs/Character/08_ROADMAP.md T-P13
//
// Pattern: копия StatsClientState (T-P04) — event-архитектура для UI (CharacterWindow T-P14).
// Events:
//   - OnSkillsUpdated: новый snapshot пришёл (learned skill IDs set)
//   - OnSkillResult: ack/deny от TryLearnSkill/TryForgetSkill (toast)

using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Skills.Dto;

namespace ProjectC.Skills
{
    /// <summary>
    /// Client-side projection of server skills state. Singleton, auto-spawned в NMC (T-P13).
    /// </summary>
    public class SkillsClientState : MonoBehaviour
    {
        public static SkillsClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // ============ State ============
        /// <summary>Current set of learned skill IDs (mirrors server state).</summary>
        public HashSet<string> CurrentSkills { get; private set; } = new HashSet<string>();

        // R4: client-side skill config cache (избегает Resources.LoadAll на каждый клик)
        private readonly Dictionary<string, SkillNodeConfig> _skillConfigCache = new Dictionary<string, SkillNodeConfig>();

        // ============ Events для UI ============
        /// <summary>Data event: новый snapshot пришёл. UI вызывает RefreshDisplay.</summary>
        public event Action<HashSet<string>> OnSkillsUpdated;

        /// <summary>Notification event: learn/forget ack/deny. UI показывает toast.</summary>
        public event Action<SkillResultDto> OnSkillResult;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
                LoadSkillConfigs();  // R4: pre-load all skill configs once
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>R4: загрузить все SkillNodeConfig из Resources один раз при старте.</summary>
        private void LoadSkillConfigs()
        {
            _skillConfigCache.Clear();
            var allSkills = Resources.LoadAll<SkillNodeConfig>("Skills");
            foreach (var s in allSkills)
            {
                if (s == null || string.IsNullOrEmpty(s.skillId)) continue;
                _skillConfigCache[s.skillId] = s;
            }
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillsClientState/R4] Cached {_skillConfigCache.Count} skill configs");
            }
        }

        /// <summary>R4: получить SkillNodeConfig по skillId из кэша (без Resources.LoadAll).</summary>
        public bool TryGetSkillConfig(string skillId, out SkillNodeConfig config)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                config = null;
                return false;
            }
            return _skillConfigCache.TryGetValue(skillId, out config);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Server → client handler. Вызывается из NetworkPlayer.ReceiveSkillsSnapshotTargetRpc.
        /// </summary>
        public void OnSkillsSnapshotReceived(SkillsSnapshotDto snapshot)
        {
            CurrentSkills = snapshot.learnedSkillIds != null
                ? new HashSet<string>(snapshot.learnedSkillIds)
                : new HashSet<string>();
            OnSkillsUpdated?.Invoke(CurrentSkills);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillsClientState] OnSkillsSnapshotReceived: {CurrentSkills.Count} skills learned");
            }
        }

        /// <summary>
        /// Server → client handler. Вызывается из NetworkPlayer.ReceiveSkillResultTargetRpc.
        /// </summary>
        public void OnSkillResultReceived(SkillResultDto result)
        {
            OnSkillResult?.Invoke(result);
            if (Debug.isDebugBuild)
            {
                string msg = result.code switch
                {
                    SkillResultCode.Learned   => $"✅ Изучено: {result.skillId}",
                    SkillResultCode.Forgotten => $"✅ Забыто: {result.skillId}",
                    SkillResultCode.Denied    => $"❌ {result.reason}",
                    _ => $"? unknown code={result.code}",
                };
                Debug.Log($"[SkillsClientState] OnSkillResultReceived: {msg}");
            }
        }

        /// <summary>Convenience: clear state (scene reload без DontDestroyOnLoad).</summary>
        public void ClearState()
        {
            CurrentSkills.Clear();
        }
    }
}
