// Project C: Real-Time Combat Engine — T-RTC04
// DefaultDamageSource — fallback IDamageSource для экипированного оружия до T-CB03
// (WeaponItemData с полями damageDice/baseDamage/critModifier ещё не реализован).
// Design: docs/Character/Skills/real-time-combat/30_PITFALLS.md §1.5.

using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    /// <summary>
    /// Hard-coded default: <c>d6, base=1, critMod=0, range=2m, type=Physical, cooldown=1.0s</c>.
    /// Используется в <c>PlayerAttacker.RebuildSources()</c> как fallback, когда
    /// <c>InventoryWorld.GetItemDefinition(itemId)</c> возвращает НЕ <c>WeaponItemData</c>
    /// (т.е. предмет не оружие, или T-CB03 ещё не сделан).
    /// </summary>
    /// <remarks>
    /// После T-CB03 заменяется на <c>WeaponDamageSource</c> с реальными полями из WeaponItemData.
    /// </remarks>
    public sealed class DefaultDamageSource : IDamageSource
    {
        private readonly ulong _sourceId;
        private readonly string _displayName;

        public DefaultDamageSource(ulong sourceId = 0, string displayName = "Default")
        {
            _sourceId = sourceId;
            _displayName = displayName;
        }

        public ulong GetSourceId() => _sourceId;
        public DamageType GetDamageType() => DamageType.Physical;
        public DamageDice GetDamageDice() => DamageDice.d6;
        public int GetBaseDamage() => 1;
        public int GetCritModifier() => 0;
        public float GetRange() => 2.0f;
        public float GetCooldownSeconds() => 1.0f;
        public float GetSkillMultiplier(ulong attackerId) => 1.0f;  // MVP: навыки не подключены
        public string GetDisplayName() => _displayName;
    }
}
