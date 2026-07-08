// Project C: Real-Time Combat Engine — T-NPC-S18
// SocialRoleConfig: ScriptableObject-пресет социальной роли NPC.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §9
//          + 02_SOCIAL_HUMAN_BEHAVIOR.md §2.5.2

using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// T-NPC-S18: Пресет социальной роли.
    /// Комбинирует personality + idle activity + reaction pattern для быстрой настройки NPC.
    /// Используется NpcSpawnerConfig при спавне.
    /// </summary>
    [CreateAssetMenu(fileName = "SocialRole_", menuName = "Project C/AI/Social Role")]
    public class SocialRoleConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Название роли (Guard, Civilian, Merchant, Thug, Leader).")]
        public string roleName = "Civilian";

        [TextArea(2, 5)]
        [Tooltip("Краткое описание роли для дизайнеров.")]
        public string description;

        [Header("Behavior Preset")]
        [Tooltip("Idle-активность по умолчанию.")]
        public NpcIdleActivity defaultIdleActivity = NpcIdleActivity.StandStill;

        [Tooltip("Пресет personality (courage/aggression/loyalty/recklessness/mercy).")]
        public NpcPersonalityConfig personalityPreset;

        [Tooltip("Может ли NPC убегать.")]
        public bool canFlee = true;

        [Tooltip("Порог HP для бегства (доля 0..1).")]
        [Range(0f, 1f)] public float fleeHpThreshold = 0.25f;

        [Tooltip("Стражник — реагирует на Alarm немедленным Chase (а не Investigate).")]
        public bool isGuard = false;

        [Tooltip("Лидер группы — может быть назначен лидером при групповом спавне.")]
        public bool isLeader = false;

        [Tooltip("Может ли NPC сдаться (Surrender).")]
        public bool canSurrender = true;

        [Tooltip("Порог HP для сдачи.")]
        [Range(0f, 1f)] public float surrenderHpThreshold = 0.10f;

        [Header("Combat Modifiers")]
        [Tooltip("Множитель к aggroRange (1.0 = без изменений).")]
        [Range(0.5f, 2f)] public float aggroRangeMultiplier = 1.0f;

        [Tooltip("Множитель к attackRange.")]
        [Range(0.5f, 2f)] public float attackRangeMultiplier = 1.0f;

        // --- Factory Methods ---

        /// <summary>Применить роль к NpcSocialBrain (вызывается при спавне).</summary>
        public void ApplyTo(NpcSocialBrain brain)
        {
            if (brain == null) return;

            brain.idleActivity = defaultIdleActivity;
            brain.canFlee = canFlee;
            brain.fleeHpThreshold = fleeHpThreshold;
            brain.isGuard = isGuard;
            brain.canSurrender = canSurrender;
            brain.surrenderHpThreshold = surrenderHpThreshold;

            if (personalityPreset != null)
                brain.personalityConfig = personalityPreset;

            // Aggro/attack multipliers через NpcBrain.
            if (brain._brain != null)
            {
                brain._brain.aggroRange *= aggroRangeMultiplier;
                brain._brain.attackRange *= attackRangeMultiplier;
            }
        }

        // --- Стандартные пресеты (для Resources.Load) ---

        public static SocialRoleConfig CreateGuardPreset()
        {
            var preset = CreateInstance<SocialRoleConfig>();
            preset.roleName = "Guard";
            preset.description = "Стражник: патрулирует широкую зону, Chase + Alarm, никогда не flee.";
            preset.defaultIdleActivity = NpcIdleActivity.Patrol;
            preset.canFlee = false;
            preset.isGuard = true;
            preset.isLeader = false;
            preset.canSurrender = false;
            preset.aggroRangeMultiplier = 1.2f;
            return preset;
        }

        public static SocialRoleConfig CreateCivilianPreset()
        {
            var preset = CreateInstance<SocialRoleConfig>();
            preset.roleName = "Civilian";
            preset.description = "Мирный: Wander/Socialize, Flee + Alarm, низкий порог страха.";
            preset.defaultIdleActivity = NpcIdleActivity.Wander;
            preset.canFlee = true;
            preset.fleeHpThreshold = 0.5f;
            preset.isGuard = false;
            preset.isLeader = false;
            preset.canSurrender = true;
            preset.surrenderHpThreshold = 0.2f;
            preset.aggroRangeMultiplier = 0.8f;
            return preset;
        }

        public static SocialRoleConfig CreateMerchantPreset()
        {
            var preset = CreateInstance<SocialRoleConfig>();
            preset.roleName = "Merchant";
            preset.description = "Торговец: Sit/StandStill, Flee к guards, высокий mercy.";
            preset.defaultIdleActivity = NpcIdleActivity.StandStill;
            preset.canFlee = true;
            preset.fleeHpThreshold = 0.3f;
            preset.isGuard = false;
            preset.isLeader = false;
            preset.canSurrender = true;
            preset.surrenderHpThreshold = 0.3f;
            preset.aggroRangeMultiplier = 0.5f;
            return preset;
        }

        public static SocialRoleConfig CreateThugPreset()
        {
            var preset = CreateInstance<SocialRoleConfig>();
            preset.roleName = "Thug";
            preset.description = "Бандит: Patrol (узкая зона), Chase + Warning, flee только без союзников.";
            preset.defaultIdleActivity = NpcIdleActivity.Patrol;
            preset.canFlee = true;
            preset.fleeHpThreshold = 0.15f;
            preset.isGuard = false;
            preset.isLeader = false;
            preset.canSurrender = false;
            preset.aggroRangeMultiplier = 1.0f;
            return preset;
        }

        public static SocialRoleConfig CreateLeaderPreset()
        {
            var preset = CreateInstance<SocialRoleConfig>();
            preset.roleName = "Leader";
            preset.description = "Лидер: StandStill (центр), Command → группа Chase, никогда не flee.";
            preset.defaultIdleActivity = NpcIdleActivity.StandStill;
            preset.canFlee = false;
            preset.isGuard = false;
            preset.isLeader = true;
            preset.canSurrender = false;
            preset.aggroRangeMultiplier = 1.3f;
            return preset;
        }
    }
}
