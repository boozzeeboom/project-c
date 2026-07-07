// Project C: Real-Time Combat Engine — T-RTC04 + T-CB03
// ICombatDamageProvider: универсальный интерфейс для любого предмета, способного наносить урон.
//
// Реализуется WeaponItemData и ThrowableItemData (уже имеют все нужные поля).
// Используется в PlayerAttacker.RebuildSources (вместо is WeaponItemData hard check)
// и в WeaponDamageSource (вместо прямого ref на WeaponItemData).
//
// Design: рефакторинг item-type-system (см. ITERATIONS.md).

using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    public interface ICombatDamageProvider
    {
        DamageDice GetDamageDice();
        int GetBaseDamage();
        int GetCritModifier();
        float GetRange();
        DamageType GetDamageType();
        string GetDisplayName();
    }
}
