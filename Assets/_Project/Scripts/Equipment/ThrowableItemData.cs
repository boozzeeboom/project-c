// Project C: Thrown Combat — Phase T1
// ThrowableItemData: ScriptableObject для метательных предметов (гранаты, мины, заряды).
// Наследует ItemData. Используется как расходуемый предмет (НЕ экипируется в WeaponMain).
//
// Design: docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md
//
// Поля:
//   explosionRadius — радиус AOE (метры)
//   damageDice      — dice для взрыва
//   baseDamage      — flat damage (без dice)
//   damageType      — Explosive / Antigrav
//   critModifier    — для antigrav (g-волна нестабильна)
//   fuseTimeSec     — задержка до взрыва после броска (0 = мгновенно)
//   throwRange      — максимальная дистанция броска
//   aoeFormula      — формула AOE (Sphere для гранаты, Box для мины)

using UnityEngine;
using ProjectC.Combat.Core;
using ProjectC.Items;
using ProjectC.Skills;

namespace ProjectC.Equipment
{
    [CreateAssetMenu(fileName = "Throwable_", menuName = "Project C/Equipment/Throwable", order = 13)]
    public class ThrowableItemData : ItemData, ProjectC.Combat.ICombatDamageProvider
    {
        [Header("Explosion")]
        [Tooltip("Радиус AOE в метрах (Sphere).")]
        [Range(0.5f, 20f)] public float explosionRadius = 3f;

        [Header("ERPR Damage")]
        [Tooltip("Damage dice (d4-d20).")]
        public DamageDice damageDice = DamageDice.d10;

        [Tooltip("Базовый урон (без dice).")]
        [Range(0, 50)] public int baseDamage = 5;

        [Tooltip("Damage type. Explosive=обычный взрыв, Antigrav=g-волна.")]
        public DamageType damageType = DamageType.Explosive;

        [Tooltip("Crit modifier (1d100 + critMod >= 100 -> crit ×2). Антиграв-взрывчатка имеет +10-15.")]
        [Range(-50, 50)] public int critModifier = 0;

        [Header("Throwing")]
        [Tooltip("Максимальная дистанция броска (метры).")]
        [Range(1f, 100f)] public float throwRange = 25f;

        [Tooltip("Задержка до взрыва после броска (сек). 0 = мгновенный взрыв при попадании.")]
        [Range(0f, 10f)] public float fuseTimeSec = 0f;

        [Header("AOE")]
        [Tooltip("Формула AOE. Sphere = круговой взрыв, Box = прямоугольная зона (мина).")]
        public ProjectC.Skills.AoeFormula aoeFormula = ProjectC.Skills.AoeFormula.Sphere;

        // === ICombatDamageProvider ===

        ProjectC.Combat.Core.DamageDice ProjectC.Combat.ICombatDamageProvider.GetDamageDice() => damageDice;
        int ProjectC.Combat.ICombatDamageProvider.GetBaseDamage() => baseDamage;
        int ProjectC.Combat.ICombatDamageProvider.GetCritModifier() => critModifier;
        float ProjectC.Combat.ICombatDamageProvider.GetRange() => throwRange;
        ProjectC.Combat.Core.DamageType ProjectC.Combat.ICombatDamageProvider.GetDamageType() => damageType;
        string ProjectC.Combat.ICombatDamageProvider.GetDisplayName() => itemName;

        // === OnValidate: auto-set itemType ===
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (itemType != ItemType.Equipment)
            {
                itemType = ItemType.Equipment;
            }
            if (equipSlot == EquipSlot.None)
            {
                equipSlot = EquipSlot.WeaponMain;
            }
        }
#endif
    }
}
