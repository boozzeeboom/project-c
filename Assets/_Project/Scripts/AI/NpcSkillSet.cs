// Project C: Real-Time Combat Engine — T-NPC-SKILL-01
// NpcSkillSet: ScriptableObject с набором скилов NPC + оверрайдами.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/06_NPC_SKILL_ASSIGNMENT_PLAN.md
//
// Позволяет назначить игровые SkillNodeConfig на NPC с кастомными параметрами
// (cooldown, animation, damage, range). Backward-compat: если skillSet == null,
// NpcAttacker использует старый NpcDefaultDamageSource из NpcCombatData.

using System;
using UnityEngine;
using ProjectC.Skills;
using ProjectC.Combat.Core;

namespace ProjectC.AI
{
    /// <summary>
    /// Оверрайд параметров скила для конкретного NPC-типа.
    /// Все поля опциональны: 0/null = использовать значение из SkillNodeConfig.
    /// </summary>
    [Serializable]
    public struct NpcSkillOverride
    {
        [Tooltip("Ссылка на SkillNodeConfig (тот же .asset что у игрока).")]
        public SkillNodeConfig skillConfig;

        [Header("Overrides (0/null = использовать default из SkillNodeConfig)")]
        [Tooltip("Переопределить кулдаун в секундах. 0 = использовать skillConfig.cooldownSeconds.")]
        [Min(0f)] public float overrideCooldown;

        [Tooltip("Кастомная анимация для NPC-версии скилла. null = использовать skillConfig.attackClip.")]
        public AnimationClip overrideAnimation;

        [Tooltip("Скорость проигрывания overrideAnimation. 0 = использовать skillConfig.attackClipSpeed.")]
        [Range(0.1f, 3f)] public float overrideAnimationSpeed;

        [Tooltip("Переопределить кость урона. None = использовать из skill-логики.")]
        public DamageDice overrideDamageDice;

        [Tooltip("Переопределить базовый урон. 0 = использовать из skill-логики.")]
        [Min(0)] public int overrideBaseDamage;

        [Tooltip("Переопределить дальность атаки в метрах. 0 = использовать из skill-логики.")]
        [Min(0f)] public float overrideRange;

        [Header("AI Selection")]
        [Tooltip("Вес для случайного выбора (RandomWeighted) или порядок для RoundRobin. 0 = скилл отключён.")]
        [Range(0, 100)] public int priority;

        [Tooltip("Минимальный % HP (0..1), при котором скилл доступен. 0 = всегда доступен.")]
        [Range(0f, 1f)] public float minHpPercent;

        [Tooltip("Максимальный % HP (0..1), при котором скилл доступен. 1 = всегда доступен.")]
        [Range(0f, 1f)] public float maxHpPercent;

        /// <summary>Валиден ли этот оверрайд (есть ссылка на skillConfig).</summary>
        public bool IsValid => skillConfig != null;
    }

    /// <summary>
    /// Набор скилов для NPC с оверрайдами. Назначается на NpcAttacker (через префаб или спавнер).
    /// Один SO может быть расшарен между несколькими NPC-типами.
    /// </summary>
    [CreateAssetMenu(fileName = "NpcSkillSet_", menuName = "Project C/AI/NPC Skill Set")]
    public class NpcSkillSet : ScriptableObject
    {
        public enum SelectionMode : byte
        {
            /// <summary>Случайный выбор с весом priority.</summary>
            RandomWeighted = 0,

            /// <summary>По очереди (Round-Robin).</summary>
            RoundRobin = 1,

            /// <summary>Всегда скилл с максимальным priority, если доступен.</summary>
            PriorityFirst = 2,
        }

        [Header("Selection")]
        [Tooltip("Как NPC выбирает скилл из списка.")]
        public SelectionMode selectionMode = SelectionMode.RandomWeighted;

        [Header("Skills")]
        [Tooltip("Массив скилов с оверрайдами. Порядок важен для RoundRobin.")]
        public NpcSkillOverride[] skills = Array.Empty<NpcSkillOverride>();

        [Header("Fallback")]
        [Tooltip("Если skills пуст или все отфильтрованы по HP — использовать этот скилл. " +
                 "Если null — fallback на NpcDefaultDamageSource из NpcCombatData.")]
        public SkillNodeConfig defaultAttack;

        /// <summary>Кол-во валидных скилов (с ненулевым priority и валидным skillConfig).</summary>
        public int ValidSkillCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < skills.Length; i++)
                    if (skills[i].IsValid && skills[i].priority > 0)
                        count++;
                return count;
            }
        }

        /// <summary>
        /// Отфильтровать скилы, доступные при текущем % HP.
        /// Возвращает массив индексов в skills[].
        /// </summary>
        public int[] GetAvailableSkillIndices(float hpPercent)
        {
            int count = 0;
            for (int i = 0; i < skills.Length; i++)
            {
                var s = skills[i];
                if (!s.IsValid || s.priority <= 0) continue;
                if (hpPercent < s.minHpPercent || hpPercent > s.maxHpPercent) continue;
                count++;
            }

            int[] result = new int[count];
            int idx = 0;
            for (int i = 0; i < skills.Length; i++)
            {
                var s = skills[i];
                if (!s.IsValid || s.priority <= 0) continue;
                if (hpPercent < s.minHpPercent || hpPercent > s.maxHpPercent) continue;
                result[idx++] = i;
            }
            return result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Гарантируем maxHpPercent ≥ minHpPercent
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i].maxHpPercent < skills[i].minHpPercent)
                    skills[i].maxHpPercent = skills[i].minHpPercent;
            }
        }
#endif
    }
}
