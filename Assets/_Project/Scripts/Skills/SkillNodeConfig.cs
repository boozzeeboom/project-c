// Project C: Character Progression — T-P11 (полная версия, T-P07 был stub)
// SkillNodeConfig: один навык = один SO. SkillCategory enum, prerequisites, effects, costs, cycle detection.
// Design: docs/Character/03_DATA_MODEL.md §4, docs/Character/06_SKILL_TREE.md §1, docs/Character/08_ROADMAP.md T-P11
//
// Q3.2: defaultSkills = empty (per user decision). T-P13 SkillsConfig.defaultSkills = Array.Empty.
// Cycle detection: OnValidate DFS в Editor — warning (не throw, чтобы не блокировать import).

using System;
using System.Collections.Generic;
using ProjectC.Stats;
using UnityEngine;

namespace ProjectC.Skills
{
    public enum SkillCategory : byte
    {
        Social = 0,
        Combat = 1,
    }

    [CreateAssetMenu(fileName = "Skill_", menuName = "Project C/Skill Node", order = 13)]
    public class SkillNodeConfig : ScriptableObject
    {
        [Header("Identity")]
        public string skillId;          // "social_basic_talk" — stable key
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("Category (display + future combat/social split)")]
        [Tooltip("Display-only. Runtime effects НЕ зависят от category (combat навык может дать +INT для tactical).")]
        public SkillCategory category = SkillCategory.Social;

        [Header("Prerequisites (DAG, no cycles)")]
        [Tooltip("Все указанные skills должны быть изучены для learn этого. Cycle detection в OnValidate (Editor).")]
        public SkillNodeConfig[] prerequisites = Array.Empty<SkillNodeConfig>();

        [Header("Effects (applied when learned)")]
        [Tooltip("SkillEffect[] — additive/multiplicative stat bonuses + ability/passive unlocks.")]
        public SkillEffect[] effects = Array.Empty<SkillEffect>();

        [Header("XP Cost to Learn")]
        [Tooltip("XP spent from Intelligence pool (per SkillsWorld.TryLearnSkill → StatsServer.ApplyXpDirect). " +
                 "0 = free (starter skill).")]
        [SerializeField, Min(0f)] private float _learnXpCost = 50f;

        [Header("Tier Requirement (optional)")]
        [Tooltip("Minimum Intelligence tier. 0 = no requirement.")]
        [SerializeField, Min(0)] private int _requiredIntelligenceTier = 0;

        [Header("UI Layout (for skill tree visualization)")]
        [Tooltip("Position X в skill tree layout (pixels, для будущего Painter2D view в T-P19).")]
        public int treeX;

        [Tooltip("Position Y в skill tree layout (pixels).")]
        public int treeY;

        // === Public read-only API ===

        public float LearnXpCost => _learnXpCost;
        public int RequiredIntelligenceTier => _requiredIntelligenceTier;

        // === OnValidate: cycle detection (Editor-only) ===

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (prerequisites == null || prerequisites.Length == 0) return;
            var visited = new HashSet<SkillNodeConfig>();
            var recursionStack = new HashSet<SkillNodeConfig>();
            if (HasCycle(this, visited, recursionStack))
            {
                Debug.LogWarning(
                    $"[SkillNodeConfig] Cycle detected in prerequisites for '{skillId}'. " +
                    "Remove one of the edges. Cycles will cause infinite recursion in TryLearnSkill.",
                    this);
            }
        }

        private static bool HasCycle(SkillNodeConfig node, HashSet<SkillNodeConfig> visited, HashSet<SkillNodeConfig> stack)
        {
            if (stack.Contains(node)) return true;        // cycle found
            if (visited.Contains(node)) return false;      // already explored, no cycle through this
            visited.Add(node);
            stack.Add(node);
            if (node.prerequisites != null)
            {
                foreach (var p in node.prerequisites)
                {
                    if (p != null && HasCycle(p, visited, stack)) return true;
                }
            }
            stack.Remove(node);
            return false;
        }
#endif
    }
}
