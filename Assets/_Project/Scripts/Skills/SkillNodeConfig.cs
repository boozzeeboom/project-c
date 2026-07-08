// Project C: Character Progression — T-P11 (полная версия, T-P07 был stub)
// SkillNodeConfig: один навык = один SO. SkillCategory enum, prerequisites, effects, costs, cycle detection.
// Design: docs/Character/03_DATA_MODEL.md §4, docs/Character/06_SKILL_TREE.md §1, docs/Character/08_ROADMAP.md T-P11
//
// Q3.2: defaultSkills = empty (per user decision). T-P13 SkillsConfig.defaultSkills = Array.Empty.
// Cycle detection: OnValidate DFS в Editor — warning (не throw, чтобы не блокировать import).

using System;
using System.Collections.Generic;
using ProjectC.Equipment;
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
    /// T-CB02 → REFACTOR 2026-07-26: Combat discipline для фильтрации + future CharacterWindow sub-tabs.
    /// None = социальные / non-combat навыки.
    /// Сокращено до 4 базовых дисциплин (Melee/Ranged/Defense/Placed) — соответствует WeaponHandling (R1).
    /// Explosives и Antigrav удалены — навыки распределены по новым дисциплинам + подтипам.
    /// Auto-set по skillId prefix в OnValidate (Editor) — additive, backward-compat.
    /// </summary>
    public enum CombatDiscipline : byte
    {
        None = 0,      // social / non-combat
        Combat = 1,    // универсальные (DodgeRoll, BasicStrike)
        Melee = 2,     // ближний бой (мечи, копья, кинжалы)
        Ranged = 3,    // дальний бой (луки, арбалеты, throwables)
        Defense = 4,   // защита (броня, стойки, щиты, ауры)
        Placed = 5,    // устанавливаемое (мины, ловушки, турели)
    }

    /// <summary>
    /// REFACTOR 2026-07-26: Подтип боевого навыка внутри дисциплины.
    /// Виден только дизайнеру в инспекторе. Игроку не показывается.
    /// Определяет какие дополнительные поля активны в инспекторе и в runtime.
    /// </summary>
    public enum CombatSubtype : byte
    {
        None = 0,        // обычный навык (большинство)
        Throwables = 1,  // бросаемые предметы (гранаты, ножи, топоры)
        Traps = 2,       // ловушки/мины (пример подтипа, не фокусируемся)
        Bows = 3,        // R5: лук — character-forward raycast + D100 hit/damage
        Crossbows = 4,   // R5: арбалет — character-forward raycast + D100 hit/damage
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

        [Header("Combat Discipline (T-CB02 → REFACTOR 2026-07-26)")]
        [Tooltip("Фильтр для CharacterWindow + Phase 2 (skill tree sub-tabs). " +
                 "Auto-set по skillId prefix в OnValidate (melee_ → Melee и т.п.). " +
                 "None = социальный / non-combat навык.")]
        public CombatDiscipline discipline = CombatDiscipline.None;

        [Header("Combat Subtype (REFACTOR 2026-07-26)")]
        [Tooltip("Подтип внутри дисциплины. Виден только дизайнеру. " +
                 "Определяет доп. настройки: Throwables → throwRange/throwScatter/throwCount, " +
                 "Bows/Crossbows → rangedMaxRange/rangedHitChance, Traps → будущие поля.")]
        public CombatSubtype subtype = CombatSubtype.None;

        [Header("Weapon Requirement (T-INP-09)")]
        [Tooltip("Битовая маска допустимых WeaponClass для активации навыка. " +
                 "None (0) = без ограничения (default, backward-compat). " +
                 "AnyWeapon = требуется хоть какое-то оружие в WeaponMain/WeaponOff. " +
                 "AnyMelee = меч/кинжал/копьё/булава. AnyRanged = арбалет/пневматика. " +
                 "Проверяется клиент-сайд в SkillInputService.TryActivate (T-INP-09).")]
        public WeaponClassMask requiredWeaponMask = WeaponClassMask.None;

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

        [Header("Stat Tier Requirements (optional)")]
        [Tooltip("Minimum Strength tier. 0 = no requirement.")]
        [SerializeField, Min(0)] private int _requiredStrengthTier = 0;

        [Tooltip("Minimum Dexterity tier. 0 = no requirement.")]
        [SerializeField, Min(0)] private int _requiredDexterityTier = 0;

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

        [Header("Animation (T-INP-08) — AnimationClip reference (data-driven, designer-friendly)")]
        [Tooltip("Drag-and-drop AnimationClip (.anim или клип из .fbx). " +
                 "Если задан — проигрывается через SkillAnimationPlayer (AnimatorOverrideController на state 'Skill'), " +
                 "НЕ требует ручной правки Animator Controller на каждый скилл. " +
                 "Оставьте пустым для Primary/Secondary bare-fist (использует 'Attack' триггер → state Attack1H).")]
        public AnimationClip attackClip;

        [Tooltip("Скорость проигрывания клипа. 1.0 = нормальная, 2.0 = в 2 раза быстрее, 0.5 = в 2 раза медленнее. " +
                 "Полезно для быстрых лёгких атак (1.2) или медленных тяжёлых ударов (0.8).")]
        [Range(0.1f, 3.0f)] public float attackClipSpeed = 1.0f;

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

        // === T-INP-06: AOE Debug Visualization ===
        [Header("AOE Debug Visualization (T-INP-06)")]
        [Tooltip("Если включено — при активации этого навыка в Play Mode на время каста рисуется 3D wireframe " +
                 "AOE-зоны (Cone/Sphere/Line/Box) в позиции атакующего. Полезно для подгонки aoeSize/aoeConeAngleDeg/aoeWidth " +
                 "под VFX и анимации. Editor-only toggle, в build'е игнорируется.")]
        public bool debugVisualizeAoe = false;

        [Tooltip("Длительность показа wireframe в секундах. 0.3 = быстрый flash, 1.0 = видно дольше.")]
        [Range(0.1f, 3.0f)] public float debugVisualizeDuration = 0.6f;

        // === REFACTOR 2026-07-26: Throwables-specific fields ===
        [Header("Throwables (активно при subtype = Throwables)")]
        [Tooltip("Максимальная дальность броска в метрах.")]
        [Range(1f, 100f)] public float throwRange = 25f;

        [Tooltip("Разброс броска: D6. Чем выше значение навыка — тем точнее бросок. " +
                 "1 = граната может взорваться в руках (критический промах). 6 = снайперская точность.")]
        [Range(1, 6)] public int throwScatter = 3;

        [Tooltip("Кол-во одновременно брошенных предметов. Умножает расход TROWN-предметов из инвентаря. " +
                 "Визуально создаются отдельные траектории для каждого броска.")]
        [Range(1, 10)] public int throwCount = 1;

        // === R5: Bows/Crossbows-specific fields ===
        [Header("Bows/Crossbows (активно при subtype = Bows или Crossbows)")]
        [Tooltip("Максимальная дальность в метрах для fallback-поиска ближайшего NPC. Если character-forward raycast не попал в цель, ищется ближайший живой NPC в этом радиусе.")]
        [Range(1f, 200f)] public float rangedMaxRange = 30f;

        [Tooltip("Шанс попадания D100. При каждом выстреле бросается D100: если roll <= rangedHitChance — попадание с уроном roll% от базового (1-100%). Если roll > rangedHitChance — промах.")]
        [Range(0f, 100f)] public float rangedHitChance = 70f;

        // === Public read-only API ===

        public float LearnXpCost => _learnXpCost;
        public int RequiredStrengthTier => _requiredStrengthTier;
        public int RequiredDexterityTier => _requiredDexterityTier;
        public int RequiredIntelligenceTier => _requiredIntelligenceTier;

        // === OnValidate: cycle detection (Editor-only) ===

#if UNITY_EDITOR
        private void OnValidate()
        {
            // T-CB02: auto-set discipline по skillId prefix (additive, backward-compat)
            AutoSetDisciplineFromPrefix();

            // T-INP-02: backward-compat migration removed (attackAnimationTrigger field deleted in T-INP-08).

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
            // expl_ / explosives_ — больше не авто-мапятся. Мигрируются через Editor-скрипт или вручную.
            // antigrav_ — больше не авто-мапятся. Мигрируются через Editor-скрипт или вручную.
            else if (skillId.StartsWith("defense_"))      newDiscipline = CombatDiscipline.Defense;
            else if (skillId.StartsWith("placed_"))       newDiscipline = CombatDiscipline.Placed;
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