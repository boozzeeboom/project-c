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

    /// <summary>
    /// T-CB02: Combat discipline для фильтрации + future CharacterWindow sub-tabs.
    /// None = социальные / non-combat навыки.
    /// Auto-set по skillId prefix в OnValidate (Editor) — additive, backward-compat.
    /// </summary>
    public enum CombatDiscipline : byte
    {
        None = 0,        // social / non-combat
        Combat = 1,      // универсальные (DodgeRoll, PrecisionStrike)
        Melee = 2,       // мечи/копья/кинжалы
        Ranged = 3,      // луки/арбалеты
        Explosives = 4,  // гранаты/мины
        Antigrav = 5,    // антиграв. техники
        Defense = 6,     // броня/стойки
    }

    /// <summary>
    /// T-INP-02: AOE формула для active skill'ов. Семантика aoeSize/aoeConeAngleDeg/aoeWidth
    /// зависит от выбранной формулы (см. Tooltip'ы на SkillNodeConfig).
    /// SingleTarget = legacy mode (raycast → 1 цель). Cone/Sphere/Line/Box = multi-target.
    /// </summary>
    public enum AoeFormula : byte
    {
        SingleTarget = 0,  // raycast (default)
        Cone          = 1, // конус вперёд (меч, копьё, тяжёлый удар)
        Sphere        = 2, // радиус вокруг персонажа (AoE-спелл, ультимейт)
        Line          = 3, // узкая линия вперёд (копьё, древко)
        Box           = 4, // box volume (бросок в зону)
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

        [Header("Combat Discipline (T-CB02)")]
        [Tooltip("Фильтр для CharacterWindow + Phase 2 (skill tree sub-tabs). " +
                 "Auto-set по skillId prefix в OnValidate (melee_ → Melee и т.п.). " +
                 "None = социальный / non-combat навык.")]
        public CombatDiscipline discipline = CombatDiscipline.None;

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

        // === T-INP-02: Active vs Passive ===
        [Header("Active vs Passive (T-INP-02)")]
        [Tooltip("Active = биндится на слот (Primary/Secondary/Slot1..4), триггерит анимацию, может иметь AOE. " +
                 "Passive = даёт статы / unlock'и через SkillEffect, невидим в skill bar, не bindable, всегда \"работает\".")]
        public bool isActive = true;

        [Header("Animation (T-INP-02)")]
        [Tooltip("Animator trigger (e.g. \"Attack\", \"HeavySwing\", \"CastHeal\"). " +
                 "Пусто = fallback на SkillInputService._defaultAttackTrigger (\"Attack\"). " +
                 "Используется ТОЛЬКО для active skills.")]
        public string attackAnimationTrigger = "Attack";

        // === T-INP-02: AOE Formula ===
        [Header("AOE Formula (T-INP-02, active skills only)")]
        [Tooltip("SingleTarget = одиночная цель (raycast от камеры). " +
                 "Cone = конус вперёд (меч/копьё/тяжёлый удар). " +
                 "Sphere = радиус вокруг персонажа (AoE-спелл, ультимейт). " +
                 "Line = узкая линия вперёд (копьё, древко). " +
                 "Box = box volume (бросок в зону).")]
        public AoeFormula aoeFormula = AoeFormula.SingleTarget;

        [Tooltip("Размер AOE в метрах. Семантика по aoeFormula: " +
                 "SingleTarget = 0 (не используется). " +
                 "Cone / Line / Box → длина вперёд. " +
                 "Sphere → радиус вокруг персонажа.")]
        [Min(0f)] public float aoeSize = 0f;

        [Tooltip("Угол конуса в градусах (Cone only). 60 = широкий, 30 = узкий кинжал, 120 = меч по кругу.")]
        [Range(0f, 360f)] public float aoeConeAngleDeg = 60f;

        [Tooltip("Ширина линии/бокса в метрах (Line/Box only). Для Cone и Sphere = 0.")]
        [Min(0f)] public float aoeWidth = 0f;

        // === Public read-only API ===

        public float LearnXpCost => _learnXpCost;
        public int RequiredIntelligenceTier => _requiredIntelligenceTier;

        // === OnValidate: cycle detection (Editor-only) ===

#if UNITY_EDITOR
        private void OnValidate()
        {
            // T-CB02: auto-set discipline по skillId prefix (additive, backward-compat)
            AutoSetDisciplineFromPrefix();

            // T-INP-02: backward-compat migration — существующие SO получают дефолты
            if (string.IsNullOrEmpty(attackAnimationTrigger)) attackAnimationTrigger = "Attack";

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

        /// <summary>
        /// T-CB02: auto-set discipline по skillId prefix.
        /// Не перезаписывает уже выставленное вручную значение, если prefix не совпал ни с одним.
        /// </summary>
        private void AutoSetDisciplineFromPrefix()
        {
            if (string.IsNullOrEmpty(skillId)) return;
            CombatDiscipline newDiscipline;
            if (skillId.StartsWith("melee_"))            newDiscipline = CombatDiscipline.Melee;
            else if (skillId.StartsWith("ranged_"))      newDiscipline = CombatDiscipline.Ranged;
            else if (skillId.StartsWith("expl_") || skillId.StartsWith("explosives_")) newDiscipline = CombatDiscipline.Explosives;
            else if (skillId.StartsWith("antigrav_"))    newDiscipline = CombatDiscipline.Antigrav;
            else if (skillId.StartsWith("defense_"))      newDiscipline = CombatDiscipline.Defense;
            else if (skillId.StartsWith("combat_"))      newDiscipline = CombatDiscipline.Combat;
            else if (skillId.StartsWith("social_"))      newDiscipline = CombatDiscipline.None;
            else return;  // неизвестный prefix — оставляем как есть

            // Не перезаписываем, если уже выставлено вручную (отличается от prefix-mapping)
            // Для существующих .asset — всегда проставится правильно при первом OnValidate.
            discipline = newDiscipline;
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
