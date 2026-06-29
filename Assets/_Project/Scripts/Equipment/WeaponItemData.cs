// Project C: Real-Time Combat Engine — T-CB03
// WeaponItemData: ScriptableObject для combat-оружия с ERPR-полями (damageDice/baseDamage/critModifier/range/damageType).
// Design: docs/Character/Skills/real-time-combat/60_NEXT_STEPS_T-CB01.md §2.
//
// Наследует ItemData. Используется Combat-движком через IDamageSource adapter (WeaponDamageSource).
// До T-CB03 — DefaultDamageSource (d6, base=1, critMod=0, range=2м, type=Physical, cd=1s).
//
// Структура полей:
//  - itemType = Equipment (наследуется от ItemData, set в OnValidate)
//  - weaponClass: Sword/Dagger/Spear/Mace/Crossbow/Pneumatic/AntigravBlade/MesiumRifle
//  - damageType: Physical/Ballistic/Antigrav/Explosive/Mesium
//  - damageDice: d4-d20 (ERPR roll)
//  - baseDamage: flat bonus (без dice, без модификаторов)
//  - critModifier: 1d100 + critMod >= 100 → crit ×2
//  - range: метры (<3 = melee, ≥3 = ranged — auto-select в CombatServer.ResolveAttack)
//  - requiredProficiency: min skill для использования (T-CB06 gate, опционально)
//  - minTier: min INT tier для использования
//
// OnValidate (Editor-only): auto-set defaults по weaponClass (per answer 2.14).

using UnityEngine;
using ProjectC.Combat.Core;
using ProjectC.Items;  // ItemData (базовый класс)
using ProjectC.Skills;

namespace ProjectC.Equipment
{
    /// <summary>
    /// Класс оружия (ERPR §3.5). Designer выбирает в инспекторе — OnValidate ставит defaults.
    /// </summary>
    public enum WeaponClass : byte
    {
        Sword = 0,           // d8, base=2, range=2.0м, Physical
        Dagger = 1,          // d4, base=1, range=1.5м, Physical
        Spear = 2,           // d10, base=3, range=3.0м, Physical
        Mace = 3,            // d8, base=3, range=2.0м, Physical
        Crossbow = 4,        // d10, base=4, range=30м, Ballistic
        Pneumatic = 5,       // d8, base=3, range=50м, Ballistic
        AntigravBlade = 6,   // d8, base=3, critMod=+10, range=2.0м, Antigrav
        MesiumRifle = 7,     // d10, base=5, range=50м, Mesium
    }

    /// <summary>
    /// T-INP-09: битовая маска WeaponClass для требования навыка к типу оружия.
    /// None = 0 = «без ограничения» (backward-compat для всех 27 существующих SkillNodeConfig .asset).
    /// AnyWeapon = все 8 бит = «любое оружие» (навык требует хоть что-то в WeaponMain или WeaponOff).
    /// Композиция: MeleeOrRanged = Sword|Dagger|Spear|Mace|Crossbow|Pneumatic (OR-семантика бесплатно).
    /// 8 значений WeaponClass → ushort (16 бит) — запас на будущие GreatSword/AntigravHammer/...
    /// </summary>
    [System.Flags]
    public enum WeaponClassMask : ushort
    {
        None         = 0,
        Sword        = 1 << WeaponClass.Sword,        // 1
        Dagger       = 1 << WeaponClass.Dagger,       // 2
        Spear        = 1 << WeaponClass.Spear,        // 4
        Mace         = 1 << WeaponClass.Mace,         // 8
        Crossbow     = 1 << WeaponClass.Crossbow,     // 16
        Pneumatic    = 1 << WeaponClass.Pneumatic,    // 32
        AntigravBlade= 1 << WeaponClass.AntigravBlade,// 64
        MesiumRifle  = 1 << WeaponClass.MesiumRifle,  // 128
        // Convenience aliases (designer-friendly в Inspector через [Flags] dropdown)
        AnyMelee     = Sword | Dagger | Spear | Mace,
        AnyRanged    = Crossbow | Pneumatic,
        AnyWeapon    = AnyMelee | AnyRanged | AntigravBlade | MesiumRifle, // 255
    }

    [CreateAssetMenu(fileName = "Weapon_", menuName = "Project C/Equipment/Weapon", order = 12)]
    public class WeaponItemData : ItemData
    {
        [Header("Weapon class")]
        [Tooltip("Sword / Dagger / Spear / Mace / Crossbow / Pneumatic / AntigravBlade / MesiumRifle. " +
                 "OnValidate ставит defaults автоматически.")]
        public WeaponClass weaponClass = WeaponClass.Sword;

        [Header("ERPR-пакет (T-CB03, см. Battle/10_DESIGN.md §3.1)")]
        [Tooltip("Damage dice. d4-d20 (ERPR §3.1).")]
        public ProjectC.Combat.Core.DamageDice damageDice = ProjectC.Combat.Core.DamageDice.d6;

        [Tooltip("Базовый урон (без dice, без модификаторов). Формула: " +
                 "final = (STR + 1dN + base) × crit × skillMult − defense.")]
        [Range(0, 50)] public int baseDamage = 1;

        [Tooltip("Crit modifier: 1d100 + critMod >= 100 → crit ×2.")]
        [Range(-50, 50)] public int critModifier = 0;

        [Header("Range (Combat-движок)")]
        [Tooltip("Range в метрах. <3м = melee (MeleeRangePolicy), ≥3м = ranged (RangedRangePolicy).")]
        [Range(0.5f, 200f)] public float range = 2.0f;

        [Header("Damage type (Combat-движок)")]
        [Tooltip("Physical / Ballistic / Antigrav / Explosive / Mesium. " +
                 "Antigrav: armor × 0.5; Mesium: armor × 0.")]
        public ProjectC.Combat.Core.DamageType damageType = ProjectC.Combat.Core.DamageType.Physical;

        [Header("Proficiency gate (T-CB06, optional)")]
        [Tooltip("Минимальный навык для использования (proficiency gate). " +
                 "Если null — gate отсутствует (default для MVP).")]
        public SkillNodeConfig requiredProficiency;

        [Header("Min INT tier")]
        [Range(0, 10)] public int minTier = 0;

        // === OnValidate: auto-set defaults per weaponClass (per answer 2.14) ===

#if UNITY_EDITOR
        private void OnValidate()
        {
            // itemType = Equipment (наследуется от ItemData)
            if (itemType != ItemType.Equipment)
            {
                itemType = ItemType.Equipment;
            }

            // Auto-set defaults по weaponClass (только если пользователь не правил руками)
            // Используем типичные паттерны: sword = balanced, dagger = fast/weak, spear = reach,
            // crossbow = ranged, antigrav = crit-focused, mesium = ignores armor.
            switch (weaponClass)
            {
                case WeaponClass.Sword:
                    if (damageDice == DamageDice.d6) damageDice = DamageDice.d8;
                    if (baseDamage == 1) baseDamage = 2;
                    if (range == 2.0f) range = 2.0f;  // explicit
                    damageType = DamageType.Physical;
                    break;
                case WeaponClass.Dagger:
                    if (damageDice == DamageDice.d6) damageDice = DamageDice.d4;
                    if (baseDamage == 1) baseDamage = 1;
                    if (range == 2.0f) range = 1.5f;
                    damageType = DamageType.Physical;
                    break;
                case WeaponClass.Spear:
                    if (damageDice == DamageDice.d6) damageDice = DamageDice.d10;
                    if (baseDamage == 1) baseDamage = 3;
                    if (range == 2.0f) range = 3.0f;
                    damageType = DamageType.Physical;
                    break;
                case WeaponClass.Mace:
                    if (damageDice == DamageDice.d6) damageDice = DamageDice.d8;
                    if (baseDamage == 1) baseDamage = 3;
                    if (range == 2.0f) range = 2.0f;
                    damageType = DamageType.Physical;
                    break;
                case WeaponClass.Crossbow:
                    if (damageDice == DamageDice.d6) damageDice = DamageDice.d10;
                    if (baseDamage == 1) baseDamage = 4;
                    if (range == 2.0f) range = 30.0f;
                    damageType = DamageType.Ballistic;
                    break;
                case WeaponClass.Pneumatic:
                    if (damageDice == DamageDice.d6) damageDice = DamageDice.d8;
                    if (baseDamage == 1) baseDamage = 3;
                    if (range == 2.0f) range = 50.0f;
                    damageType = DamageType.Ballistic;
                    break;
                case WeaponClass.AntigravBlade:
                    if (damageDice == DamageDice.d6) damageDice = DamageDice.d8;
                    if (baseDamage == 1) baseDamage = 3;
                    if (critModifier == 0) critModifier = 10;
                    if (range == 2.0f) range = 2.0f;
                    damageType = DamageType.Antigrav;
                    break;
                case WeaponClass.MesiumRifle:
                    if (damageDice == DamageDice.d6) damageDice = DamageDice.d10;
                    if (baseDamage == 1) baseDamage = 5;
                    if (range == 2.0f) range = 50.0f;
                    damageType = DamageType.Mesium;
                    break;
            }
        }
#endif
    }
}
