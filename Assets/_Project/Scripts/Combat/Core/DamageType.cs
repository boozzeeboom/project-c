// Project C: Real-Time Combat Engine — T-RTC01
// DamageType + DamageDice enums + extensions (ERPR damage formula support).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §2.5,
//         docs/Character/Skills/Battle/10_DESIGN.md §3.1, §7.1.

using UnityEngine;

namespace ProjectC.Combat.Core
{
    /// <summary>
    /// 5 типов урона. Единый enum для пешего и корабельного боя (per answer 2.2).
    /// Лор: технологичные, без магии (Physical/Ballistic/Antigrav/Explosive/Mesium).
    /// </summary>
    public enum DamageType : byte
    {
        Physical  = 0,
        Ballistic = 1,
        Antigrav  = 2,
        Explosive = 3,
        Mesium    = 4,
    }

    /// <summary>
    /// Damage dice (ERPR). 1dN для формулы <c>baseAttack = roll + base + STR</c>.
    /// </summary>
    public enum DamageDice : byte
    {
        d4  = 4,
        d6  = 6,
        d8  = 8,
        d10 = 10,
        d12 = 12,
        d20 = 20,
    }

    /// <summary>Helpers для DamageDice (Roll + Average).</summary>
    public static class DamageDiceExtensions
    {
        /// <summary>Бросок 1..N включительно (UnityEngine.Random.Range inclusive).</summary>
        public static int Roll(this DamageDice dice) => Random.Range(1, (int)dice + 1);

        /// <summary>Среднее значение dice (для UI / damage preview).</summary>
        public static float Average(this DamageDice dice) => ((int)dice + 1) / 2f;
    }

    /// <summary>Helpers для DamageType — множитель armor при defense-расчёте.</summary>
    public static class DamageTypeExtensions
    {
        /// <summary>
        /// Множитель armor: Physical/Ballistic=1.0, Antigrav=0.5 (g-волна частично игнорирует броню),
        /// Explosive=0.7 (взрывная волна частично проходит), Mesium=0.0 (токсин не блокируется).
        /// </summary>
        /// <remarks>Из <c>docs/Character/Skills/Battle/10_DESIGN.md §4.1.1</c>.</remarks>
        public static float ArmorMultiplier(this DamageType type)
        {
            switch (type)
            {
                case DamageType.Physical:
                case DamageType.Ballistic:
                    return 1.0f;
                case DamageType.Antigrav:
                    return 0.5f;
                case DamageType.Explosive:
                    return 0.7f;
                case DamageType.Mesium:
                    return 0.0f;
                default:
                    return 1.0f;
            }
        }
    }
}
