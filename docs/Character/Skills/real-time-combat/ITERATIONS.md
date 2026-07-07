# Итерации реализации — Ranged & Throwables

## Итерация от 2026-07-20

**Задача:** Реализация дальнего боя и бросковых навыков (Ranged + Thrown/Explosive)  
**Коммит:** `3d27cf0` — feat: дальний бой и бросковые навыки (фазы R1-R3, T1-T3)

**Изменения:**
- `Assets/_Project/Scripts/Combat/Client/ProjectileVisual.cs` — NEW: визуал полёта стрелы
- `Assets/_Project/Scripts/Combat/Client/ThrowArcVisual.cs` — NEW: визуал броска гранаты + взрыв
- `Assets/_Project/Scripts/Equipment/ThrowableItemData.cs` — NEW: SO для метательных предметов
- `Assets/_Project/Scripts/Combat/Client/CombatClientState.cs` — MOD: хук ProjectileVisual в HandleAttackLanded
- `Assets/_Project/Scripts/Combat/Network/CombatServer.cs` — MOD: RequestSkillCastAtPointRpc + targetPoint AOE
- `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` — MOD: регистрация ThrowableItemData
- `Assets/_Project/Scripts/Skills/SkillInputService.cs` — MOD: throw flow + FindThrowTargetPoint
- `Assets/_Project/Resources/Items/Weapons/Weapon_Crossbow.asset` — NEW
- `Assets/_Project/Resources/Items/Weapons/Weapon_Pneumatic.asset` — NEW
- `Assets/_Project/Resources/Items/Weapons/Weapon_MesiumRifle.asset` — NEW
- `Assets/_Project/Resources/Items/Throwables/Throwable_Grenade_Basic.asset` — NEW
- `Assets/_Project/Resources/Items/Throwables/Throwable_Grenade_Antigrav.asset` — NEW
- `Assets/_Project/Resources/Skills/Skill_Ranged_BasicBow.asset` — MOD: WeaponProficiency + StatMod
- `Assets/_Project/Resources/Skills/Skill_Ranged_CrossbowMastery.asset` — MOD: StatMod
- `Assets/_Project/Resources/Skills/Skill_Ranged_QuickReload.asset` — MOD: WeaponTechnique + StatMod
- `docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md` — NEW: документация
