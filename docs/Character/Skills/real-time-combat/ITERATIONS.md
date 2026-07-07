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

## Итерация от 2026-07-20 (refactor #2)

**Задача:** Рефакторинг системы типов предметов — убрать хардкод `is WeaponItemData` / `is ThrowableItemData`
**Коммит:** `f5cd28f` — refactor: ICombatDamageProvider — убран хардкод is WeaponItemData

**Изменения:**
- `Assets/_Project/Scripts/Combat/Core/ICombatDamageProvider.cs` — NEW: интерфейс боевого предмета
- `Assets/_Project/Scripts/Equipment/WeaponItemData.cs` — MOD: implements ICombatDamageProvider
- `Assets/_Project/Scripts/Equipment/ThrowableItemData.cs` — MOD: implements ICombatDamageProvider
- `Assets/_Project/Scripts/Combat/Implementations/WeaponDamageSource.cs` — MOD: принимает ICombatDamageProvider вместо WeaponItemData
- `Assets/_Project/Scripts/Combat/Implementations/PlayerAttacker.cs` — MOD: `is ICombatDamageProvider` вместо `is WeaponItemData`
- `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` — MOD: TryEquip принимает любой предмет с equipSlot≠None (убран хардкод deny по типу)
- `Assets/_Project/Scripts/Core/ItemType.cs` — MOD: equipSlot в базовом ItemData
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/InventoryTab.cs` — MOD: слот читается из ItemData.equipSlot

**Заключение: рефакторинг неправильный и неполный.**

Проблемы, оставшиеся после рефакторинга:
1. **Три несвязанные иерархии предметов остались.** `ItemData` (581 шт.), `WeaponItemData` (7 шт.), `ThrowableItemData` (2 шт.) по-прежнему не унифицированы — интерфейс ICombatDamageProvider лишь частично скрывает различия, но не решает корневую проблему: базовый `ItemData` (например `Item_Equipment_Hunting_Crossbow`) НЕ может быть оружием без смены Script-типа на `WeaponItemData` в инспекторе.
2. **equipSlot добавлен в ItemData, но ClothingItemData/ModuleItemData используют свой `slot`** — два поля с одинаковым смыслом, рассинхрон.
3. **EquipmentWorld.TryEquip всё ещё делает `is ClothingItemData` / `is ModuleItemData`** — хардкод типов не убран полностью, только для оружия заменён на `equipSlot`.
4. **WeaponDamageSource.GetCooldownSeconds() всё ещё привязан к DamageDice enum** — метательные предметы с другими характеристиками не имеют своей логики кулдауна.
5. **Предмет `Crossbow` в папке Throwables** — ошибка размещения, не исправлена.
6. **Рендж-атаки по-прежнему не работают end-to-end** — проблема не в типах, а в цепочке Skill → CombatServer → DamageSource → визуал.

Что должно быть сделано правильно:
- Единый тип предмета с опциональными combat-полями (не три разных SO-типа)
- equipSlot — одно поле, унифицированное для всех (убрать ClothingItemData.slot)
- Полный отказ от `is TypeCheck` — вся логика через виртуальные методы / интерфейсы
- End-to-end трассировка ranged-атаки: Skill → WeaponDamageSource → RangedRangePolicy → ProjectileVisual

---

## Итерация от 2026-07-24 (T-WPN-01-REF-02 R1-R5)

**Задача:** Унификация оружия — WeaponItemData + ThrowableItemData → единый класс
**Коммит:** `ac4e8a7` — T-WPN-01-REF-02: Унификация оружия

**Изменения:**
- `WeaponItemData.cs` — MOD: +WeaponHandling enum, +WeaponClass.Throwable, +throw-поля, R3 equipSlot fix
- `ThrowableItemData.cs` — 🗑 DEL (заменён на WeaponItemData)
- `InventoryWorld.cs` — MOD: R2 публичные методы (GetItemId, IsItemRegistered, RegisterIfMissing)
- `EquipmentServer.cs` — MOD: R2 без reflection, R5 прямые вызовы
- `EquipmentWorld.cs` — MOD: R5 прямой вызов SkillsWorld
- `SkillsServer.cs` — MOD: R5 прямые вызовы StatsServer/EquipmentWorld
- `SkillsWorld.cs` — MOD: R5 прямой вызов StatsServer.ApplyXpDirect
- `SkillsClientState.cs` — MOD: R4 кэш навыков
- `SkillInputService.cs` — MOD: R4 кэш, +Throwable в DescribeMaskShort
- `ICombatDamageProvider.cs` — MOD: комментарий
- `WeaponClassCatalog.cs` — MOD: +Throwable entry
- 4 `.asset` конвертированы `Throwables/` → `Weapons/`
- 4 старых `.asset` удалены из `Throwables/`

**Результат:** 2 иерархии вместо 3 (остались `ItemData` + `WeaponItemData`, `ClothingItemData` пока отдельно).

---

## Итерация от 2026-07-24 (Fix 1: client _itemCache)

**Коммит:** `0698138` — fix: клиентский _itemCache заполняется из ItemRegistry

**Проблема:** на чистом клиенте `InventoryWorld.Instance = null` → `_itemCache` пуст → `GetCachedDefinition` всегда null → UI показывает неправильные имена.

**Fix:** `_itemCache` заполняется из `ItemRegistry.asset` (Resources SO, доступен на обеих сторонах).

---

## Итерация от 2026-07-24 (Fix 2: ID-коллизия)

**Коммит:** `e2ad10c` — fix: ID-коллизия в GetOrRegisterItemId/RegisterIfMissing

**Проблема:** `_itemDatabase.Count + 1` коллидировал с существующими ID из ItemRegistry. Сервер перезаписывал запись, клиентский кэш сохранял старую.

**Fix:** поиск следующего свободного ID (`while ContainsKey`), поиск по `itemName` как fallback.

---

## Итерация от 2026-07-24 (Fix 3: itemName в DTO)

**Коммит:** `8391461` — fix: itemName в снапшоте DTO вместо lookup по кэшу

**Проблема:** сервер динамически присваивает ID, клиентский кэш (ItemRegistry) не знает о них → UI показывает случайные имена.

**Fix:** `itemName` добавлен в `InventoryItemDto`. Сервер заполняет в `BuildSnapshot`. `InventoryTab` использует `first.itemName` напрямую.
