// Project C: Character Progression — T-P12
// SkillsWorld: POCO singleton — server-side per-player learned skills state.
// Design: docs/Character/06_SKILL_TREE.md §3, docs/Character/08_ROADMAP.md T-P12
//
// Pattern: копия StatsWorld (T-P03) + EquipmentWorld (T-P09) для per-player state storage.
//
// Public API:
//   - LoadAllSkills(SkillsConfig): Resources.LoadAll<SkillNodeConfig> → Dictionary<skillId, config>
//   - GrantDefaultSkills(clientId, SkillsConfig): per Q3.2 = no-op (defaultSkills = empty)
//   - GetLearnedSkillIds(clientId): HashSet<string>
//   - TryLearnSkill(clientId, skillId, out reason): 5-step validation
//       1. Skill exists?
//       2. Already learned? (no-op deny)
//       3. Prerequisites met?
//       4. Intelligence tier sufficient? (StatsWorld.IntelligenceTier check)
//       5. XP cost sufficient? → spend via StatsServer.ApplyXpDirect
//   - TryForgetSkill(clientId, skillId, out reason): Q3.4 free respec, XP NOT refunded
//   - BuildSaveData/LoadPlayer: persistence interface

using System.Collections.Generic;
using ProjectC.Stats.Persistence;
using UnityEngine;

namespace ProjectC.Skills
{
    public class SkillsWorld
    {
        public static SkillsWorld Instance { get; private set; }

        private Dictionary<string, SkillNodeConfig> _skillsById = new Dictionary<string, SkillNodeConfig>();
        private Dictionary<ulong, HashSet<string>> _learnedPerPlayer = new Dictionary<ulong, HashSet<string>>();

        // T-KNOWLEDGE-V2: known (but not learned) skill IDs per player
        private Dictionary<ulong, HashSet<string>> _knownPerPlayer = new Dictionary<ulong, HashSet<string>>();
=======


        public SkillsWorld()
        {
            if (Instance != null)
            {
                Debug.LogWarning("[SkillsWorld] Replacing existing instance.");
            }
            Instance = this;
        }

        public static void Reset() => Instance = null;

        // === Skills registry ===

        public void LoadAllSkills(SkillsConfig config)
        {
            _skillsById.Clear();
            if (config == null)
            {
                Debug.LogWarning("[SkillsWorld] SkillsConfig is null — no skills loaded");
                return;
            }
            var allSkills = Resources.LoadAll<SkillNodeConfig>(config.SkillsResourcesPath);
            foreach (var skill in allSkills)
            {
                if (skill == null) continue;
                if (string.IsNullOrEmpty(skill.skillId))
                {
                    Debug.LogError($"[SkillsWorld] Skill '{skill.name}' has empty skillId — skipping.");
                    continue;
                }
                _skillsById[skill.skillId] = skill;
            }
            Debug.Log($"[SkillsWorld] Loaded {_skillsById.Count} skills from Resources/{config.SkillsResourcesPath}/");
        }

        public int SkillCount => _skillsById.Count;

        public bool TryGetSkill(string skillId, out SkillNodeConfig skill)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                skill = null;
                return false;
            }
            return _skillsById.TryGetValue(skillId, out skill);
        }

        // === Per-player state ===

        public HashSet<string> GetLearnedSkillIds(ulong clientId)
        {
            if (!_learnedPerPlayer.TryGetValue(clientId, out var learned))
            {
                learned = new HashSet<string>();
                _learnedPerPlayer[clientId] = learned;
            }
            return learned;
        }

        public void GrantDefaultSkills(ulong clientId, SkillsConfig config)
        {
            // Q3.2: defaultSkills = empty by default. No-op.
            // Если designer добавит starter skills в .asset — они применятся здесь.
            if (config == null) return;
            if (config.defaultSkills == null || config.defaultSkills.Length == 0) return;
            var learned = GetLearnedSkillIds(clientId);
            foreach (var skill in config.defaultSkills)
            {
                if (skill != null) learned.Add(skill.skillId);
            }
        }

        // === T-KNOWLEDGE-V2: Known skills (knowledge, not learned) ===

        public HashSet<string> GetKnownSkillIds(ulong clientId)
        {
            if (!_knownPerPlayer.TryGetValue(clientId, out var known))
            {
                known = new HashSet<string>();
                _knownPerPlayer[clientId] = known;
            }
            return known;
        }

        public bool IsSkillKnown(ulong clientId, string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            // Implicitly known if learned
            if (GetLearnedSkillIds(clientId).Contains(skillId)) return true;
            // Explicitly known
            return GetKnownSkillIds(clientId).Contains(skillId);
        }

        /// <summary>
        /// T-KNOWLEDGE-V2: открыть знание о навыке (клиент увидит его в SkillTreeWindow).
        /// Если unlockType = None — виден всегда (known = true по умолчанию).
        /// </summary>
        public bool UnlockSkillKnowledge(ulong clientId, string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            if (!TryGetSkill(skillId, out var skill)) return false;

            var known = GetKnownSkillIds(clientId);
            if (known.Contains(skillId)) return false; // уже известно

            known.Add(skillId);
            Debug.Log($"[SkillsWorld] Knowledge unlocked: player={clientId} skill='{skill.displayName ?? skillId}'");
            return true;
        }

        /// <summary>
        /// T-KNOWLEDGE-V2: после изучения навыка X — открыть знание о всех навыках S,
        /// у которых knowledgeUnlockType == LearnFirst и S.prerequisites содержит X.
        /// Обход по прямым ссылкам конфигов (prerequisites — SkillNodeConfig[]).
        /// </summary>
        public void AutoOnSkillLearned(ulong clientId, string learnedSkillId)
        {
            if (string.IsNullOrEmpty(learnedSkillId)) return;
            int unlocked = 0;
            foreach (var kv in _skillsById)
            {
                var skill = kv.Value;
                if (skill.knowledgeUnlockType != KnowledgeUnlockType.LearnFirst) continue;
                if (skill.prerequisites == null) continue;

                foreach (var prereq in skill.prerequisites)
                {
                    if (prereq != null && prereq.skillId == learnedSkillId)
                    {
                        if (UnlockSkillKnowledge(clientId, skill.skillId))
                            unlocked++;
                        break;
                    }
                }
            }
            if (unlocked > 0 && Debug.isDebugBuild)
                Debug.Log($"[SkillsWorld] AutoOnSkillLearned: player={clientId} learned='{learnedSkillId}' → unlocked {unlocked} skills via LearnFirst");
        }

        // === TryLearnSkill (5-step per roadmap §3.3) ===
=======


        public bool TryLearnSkill(ulong clientId, string skillId, out string reason)
        {
            reason = "";

            // 1. Skill exists?
            if (!TryGetSkill(skillId, out var skill))
            {
                reason = "Навык не найден";
                return false;
            }

            var learned = GetLearnedSkillIds(clientId);

            // 2. Already learned?
            if (learned.Contains(skillId))
            {
                reason = "Навык уже изучен";
                return false;
            }

            // 3. Prerequisites met?
            if (skill.prerequisites != null)
            {
                foreach (var prereq in skill.prerequisites)
                {
                    if (prereq != null && !learned.Contains(prereq.skillId))
                    {
                        reason = $"Требуется: {prereq.displayName ?? prereq.skillId}";
                        return false;
                    }
                }
            }

            // 4. Stat tier requirements? (StatsWorld) — STR, DEX, INT.
            var statsNullable = ProjectC.Stats.StatsWorld.Instance?.GetOrCreateStats(clientId);
            var stats = statsNullable.GetValueOrDefault();
            if (statsNullable.HasValue)
            {
                if (ProjectC.Stats.PlayerStats.GetTier(stats, ProjectC.Stats.StatType.Strength) < skill.RequiredStrengthTier)
                {
                    reason = $"Требуется Сила тир {skill.RequiredStrengthTier}+";
                    return false;
                }
                if (ProjectC.Stats.PlayerStats.GetTier(stats, ProjectC.Stats.StatType.Dexterity) < skill.RequiredDexterityTier)
                {
                    reason = $"Требуется Ловкость тир {skill.RequiredDexterityTier}+";
                    return false;
                }
                if (ProjectC.Stats.PlayerStats.GetTier(stats, ProjectC.Stats.StatType.Intelligence) < skill.RequiredIntelligenceTier)
                {
                    reason = $"Требуется Интеллект тир {skill.RequiredIntelligenceTier}+";
                    return false;
                }
            }

            // 5. XP cost (spend from Intelligence pool)? → StatsServer.ApplyXpDirect
            if (skill.LearnXpCost > 0)
            {
                if (!statsNullable.HasValue)
                {
                    reason = "Неизвестна статистика";
                    return false;
                }
                if (ProjectC.Stats.PlayerStats.GetXp(stats, ProjectC.Stats.StatType.Intelligence) < skill.LearnXpCost)
                {
                    reason = $"Не хватает XP (нужно {skill.LearnXpCost:F0})";
                    return false;
                }
                // R5: прямой вызов StatsServer.ApplyXpDirect (без reflection)
                var ss = ProjectC.Stats.StatsServer.Instance;
                if (ss != null)
                {
                    if (!ss.ApplyXpDirect(clientId, ProjectC.Stats.StatType.Intelligence, -skill.LearnXpCost, out var xpReason))
                    {
                        reason = xpReason ?? "Не удалось потратить XP";
                        return false;
                    }
                }
            }

            // All checks passed
            learned.Add(skillId);
            Debug.Log($"[SkillsWorld] Player {clientId} learned skill '{skill.displayName ?? skillId}' (XP cost: {skill.LearnXpCost})");

            // T-KNOWLEDGE-V2: auto-unlock knowledge of skills with LearnFirst prerequisite = this skill
            AutoOnSkillLearned(clientId, skillId);

            return true;
=======

        }

        // === TryForgetSkill (Q3.4 free respec) ===

        public bool TryForgetSkill(ulong clientId, string skillId, out string reason)
        {
            reason = "";
            if (!TryGetSkill(skillId, out var skill))
            {
                reason = "Навык не найден";
                return false;
            }
            var learned = GetLearnedSkillIds(clientId);
            if (!learned.Contains(skillId))
            {
                reason = "Навык не изучен";
                return false;
            }
            learned.Remove(skillId);
            // Q3.4: XP НЕ возвращается (user decision: "без денежных потерь", но XP — не деньги)
            Debug.Log($"[SkillsWorld] Player {clientId} forgot skill '{skill.displayName ?? skillId}' (XP not refunded)");
            return true;
        }

        // === Persistence ===

        public SkillsSave BuildSaveData(ulong clientId)
        {
            var learned = GetLearnedSkillIds(clientId);
            var known = GetKnownSkillIds(clientId);
            var save = new SkillsSave
            {
                learnedSkillIds = new List<string>(learned).ToArray(),
                knownSkillIds = known.Count > 0 ? new List<string>(known).ToArray() : null,
            };
            Debug.Log($"[SkillsWorld.BuildSaveData] client={clientId} learnedCount={learned.Count} knownCount={known.Count} ids=[{string.Join(",", learned)}]");
            return save;
        }
=======


        public void LoadPlayer(ulong clientId, CharacterSaveData data)
        {
            if (data == null || data.skills == null)
            {
                Debug.Log($"[SkillsWorld.LoadPlayer] client={clientId} SKIP: data={data != null} skills={data?.skills != null}");
                return;
            }
            var learned = GetLearnedSkillIds(clientId);
            learned.Clear();
            if (data.skills.learnedSkillIds != null)
            {
                foreach (var id in data.skills.learnedSkillIds)
                    if (!string.IsNullOrEmpty(id)) learned.Add(id);
                Debug.Log($"[SkillsWorld.LoadPlayer] client={clientId} loaded {learned.Count} skills: [{string.Join(",", learned)}]");
            }
            else
            {
                Debug.Log($"[SkillsWorld.LoadPlayer] client={clientId} data.skills.learnedSkillIds is null");
            }

            // T-KNOWLEDGE-V2: restore known skill IDs (null = backward compat: empty set)
            var known = GetKnownSkillIds(clientId);
            known.Clear();
            if (data.skills.knownSkillIds != null)
            {
                foreach (var id in data.skills.knownSkillIds)
                    if (!string.IsNullOrEmpty(id)) known.Add(id);
                Debug.Log($"[SkillsWorld.LoadPlayer] client={clientId} loaded {known.Count} known skills");
            }
        }

        public void RemovePlayer(ulong clientId)
        {
            _learnedPerPlayer.Remove(clientId);
            _knownPerPlayer.Remove(clientId);
        }
=======


        /// <summary>
        /// P7 fix: sum additive StatMod bonuses from learned skills for combat path.
        /// Iterates all learned skills, accumulates floatValue per StatType.
        /// </summary>
        public void GetStatModBonuses(ulong clientId, out float bonusStr, out float bonusDex, out float bonusInt)
        {
            bonusStr = 0f; bonusDex = 0f; bonusInt = 0f;
            var learnedIds = GetLearnedSkillIds(clientId);
            if (learnedIds == null || learnedIds.Count == 0) return;

            foreach (var skillId in learnedIds)
            {
                if (!TryGetSkill(skillId, out var skill) || skill.effects == null) continue;
                foreach (var eff in skill.effects)
                {
                    if (eff.type != SkillEffect.Type.StatMod || eff.floatValue == 0f) continue;
                    switch (eff.statType)
                    {
                        case ProjectC.Stats.StatType.Strength:     bonusStr += eff.floatValue; break;
                        case ProjectC.Stats.StatType.Dexterity:    bonusDex += eff.floatValue; break;
                        case ProjectC.Stats.StatType.Intelligence: bonusInt += eff.floatValue; break;
                    }
                }
            }
        }
    }
}
