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

        // === TryLearnSkill (5-step per roadmap §3.3) ===

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
                if (stats.strengthTier < skill.RequiredStrengthTier)
                {
                    reason = $"Требуется Сила тир {skill.RequiredStrengthTier}+";
                    return false;
                }
                if (stats.dexterityTier < skill.RequiredDexterityTier)
                {
                    reason = $"Требуется Ловкость тир {skill.RequiredDexterityTier}+";
                    return false;
                }
                if (stats.intelligenceTier < skill.RequiredIntelligenceTier)
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
                if (stats.intelligence < skill.LearnXpCost)
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
            return true;
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
            return new SkillsSave
            {
                learnedSkillIds = new List<string>(GetLearnedSkillIds(clientId)).ToArray(),
            };
        }

        public void LoadPlayer(ulong clientId, CharacterSaveData data)
        {
            if (data == null || data.skills == null) return;
            var learned = GetLearnedSkillIds(clientId);
            learned.Clear();
            if (data.skills.learnedSkillIds != null)
            {
                foreach (var id in data.skills.learnedSkillIds) learned.Add(id);
            }
        }

        public void RemovePlayer(ulong clientId) => _learnedPerPlayer.Remove(clientId);
    }
}
