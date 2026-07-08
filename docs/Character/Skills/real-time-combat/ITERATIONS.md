# Итерации реализации — Ranged & Throwables

## Итерация от 2026-07-27

**Задача:** Подсвечивание цели (outline), переключение целей Q/E, obstruction check  
**Коммит:** `e303a68` — T-TGT01: подсвечивание цели (outline), переключение Q/E, obstruction check  
**Документ:** `docs/Character/Skills/real-time-combat/100_TARGET_HIGHLIGHT_AND_SWITCHING.md`

**Изменения:**
- `Assets/_Project/Shaders/TargetOutline.shader` — NEW: URP inverted-hull outline shader
- `Assets/_Project/Resources/Materials/M_TargetOutline.mat` — NEW: материал outline (оранжевый)
- `Assets/_Project/Scripts/Combat/Client/TargetHighlightService.cs` — NEW: client-side singleton для outline highlighting
- `Assets/_Project/Scripts/Combat/Client/TargetLockService.cs` — NEW: persistent target lock + Q/E cycling
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — add CreateTargetHighlightService + CreateTargetLockService
- `Assets/_Project/Scripts/Input/InputBindingsConfig.cs` — add targetPrevKey(Q)/targetNextKey(E)
- `Assets/_Project/Scripts/Skills/SkillInputService.cs` — highlight on target found, Q/E polling, locked target priority
- `Assets/_Project/Scripts/Combat/Network/CombatServer.cs` — obstruction check: SingleTarget redirect + AOE per-target

**Flow:**
1. Q/E (пеший режим) → TargetLockService.CycleNext/CyclePrev → lock + infinite outline
2. Любой скилл (ЛКМ/ПКМ/Ctrl/Shift) → TryActivate → locked target priority → RPC
3. Сервер → raycast attacker→target → redirect на obstruction или miss через стену

**Фиксы (post-implementation):**
- `003557b`: `PlayerAttacker.EnsureUnarmedFallback` — sourceId=0 всегда присутствует, даже при наличии оружия. Primary/Secondary без скилла больше не падают с InvalidSource.
- `edc9db4`: `CombatServer.ResolveAttack` — obstruction check добавлен для unarmed/melee атак (ранее был только в ResolveSkillCast).
- `7da14f5`: `SkillInputService.TryActivate` — cooldown guard пропускается при skipAnimation=true (повторный вызов из FireImpactRpc). Primary со скиллами с attackClip теперь наносит урон.

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

---

## Итерация от 2026-07-25 (Grenade System Bugfix & Refactor — v0.6)

**Задача:** Исправление и отладка системы гранат: направление броска, AOE debug, damage source из инвентаря, расходование

**Коммит:** `f00b222` — T-GRN01: grenade system bugfix — throw direction, AOE debug, damage source, consumption

**Изменения:**
- `Assets/_Project/Scripts/Skills/SkillInputService.cs` — MOD: `FindThrowTargetPoint` character-forward raycast, AOE debug at targetPoint для thrown, `GetActiveThrowableRange` из InventoryWorld
- `Assets/_Project/Scripts/Combat/Network/CombatServer.cs` — MOD: `ResolveThrowableSourceFromInventory` (WeaponDamageSource из инвентаря вместо Unarmed), `ConsumeThrowableFromInventory` (RemoveItems после каста)
- `docs/Character/Skills/real-time-combat/50_IMPL_CHANGELOG.md` — MOD: +v0.6 entry
- `docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md` — MOD: обновление статусов (4 ✅)

---

## Итерация от 2026-07-26 (R5: Ranged Projectile Fix)

**Задача:** Починить дальнюю атаку стрелковым оружием (луки, арбалеты, ружья) — навыки не наносили урона из-за sourceId=0 в RequestAttackRpc.

**Коммит:** `5d17c0e` — T-RTC-R5: ranged projectile fix — навыки луков/арбалетов/ружей работают через ResolveSkillCast

**Корневая причина:**
- Ranged single-target скиллы уходили через `RequestAttackRpc(targetId, 0UL)` → `GetDamageSource(0)` возвращал null → `InvalidSource`
- В отличие от throwables, у ranged не было своего routing'а с резолвингом damage source
- `EnsureUnarmedFallback()` добавляет sourceId=0 только если нет оружия — с экипированным арбалетом sourceId=0 не матчился

**Изменения:**
- `Assets/_Project/Scripts/Skills/SkillInputService.cs` — MOD: +`isRangedProjectile` detection (discipline==Ranged, subtype!=Throwables, SingleTarget, hasBind); routing через `RequestSkillCastRpc(skillId, targetId, weaponSourceId)`; +`ResolveEquippedWeaponSourceId()` helper для получения itemId из EquipmentClientState snapshot
- `Assets/_Project/Scripts/Combat/Network/CombatServer.cs` — MOD: +`isRangedSingleTarget` detection и debug logging в `ResolveSkillCast`; контекстный warning "NO target found (SingleTarget)" вместо "NO targets in AOE"
=======
- `docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md` — MOD: +запись в истории изменений

---

## Итерация от 2026-07-27 (R5: Ranged Projectile Fix v2)

**Задача:** Ranged projectile fix v2: замена роутинга с RequestAttackRpc на RequestSkillCastRpc с резолвингом equipped weapon sourceId.

**Коммит:** `695656f` — T-RTC-R5: ranged projectile fix

**Изменения:**
- `SkillInputService.cs` — +`isRangedProjectile` detection, routing через `RequestSkillCastRpc`, +`ResolveEquippedWeaponSourceId()`
- `CombatServer.cs` — +`isRangedSingleTarget` detection + debug logging; контекстный warning «NO target found (SingleTarget)»

---

## Итерация от 2026-07-27 (R5: RebuildSources Fallback)

**Задача:** Fix race condition — экипировка может загрузиться позже чем RebuildSources → InvalidSource.

**Коммит:** `7698458` — T-RTC-R5-fix: RebuildSources fallback при race condition загрузки экипировки

**Изменения:**
- `CombatServer.cs` — в `ResolveSkillCast`: если `source==null && sourceId!=0` → повторный `RebuildSources()` + перезапрос `GetDamageSource(sourceId)`. Попутно -154 строки (удалены устаревшие XML-комментарии, упрощены однострочники)

---

## Итерация от 2026-07-28 (R5: Character-forward Targeting)

**Задача:** Замена camera-forward raycast на character-forward raycast для прицеливания.

**Коммит:** `acf95df` — T-RTC-R5-targeting: character-forward raycast вместо camera-forward

**Причина:** TPS камера сверху-сзади смотрит в пол, character-forward направлен туда куда персонаж целится.

**Изменения:**
- `NetworkPlayer.cs` — `TryGetTargetFromCamera(cam, ...)` → `TryGetTarget(transform.position + up*1.5f, transform.forward, 30f)`

---

## Итерация от 2026-07-28 (R5: Bows/Crossbows Subtypes + D100)

**Задача:** Добавить подкатегории Bows/Crossbows в CombatSubtype, D100 hit/damage механику, поля rangedMaxRange/rangedHitChance.

**Коммит:** `b537677` — T-RTC-R5-bows-crossbows: подкатегории Bows/Crossbows + D100 + rangedMaxRange

**Изменения:**
- `SkillNodeConfig.cs` — enum `CombatSubtype`: +`Bows=3`, +`Crossbows=4`. Поля: +`rangedMaxRange` (1–200m), +`rangedHitChance` (0–100%)
- `SkillInputService.cs` — +`FindNearestNpcInRange(rangedMaxRange)` — fallback когда raycast не нашёл цель
- `CombatServer.cs` — +D100: `d100Roll <= hitChance` → `damage *= d100Roll/100`; `d100Roll > hitChance` → `isHit=false`

---

## Итерация от 2026-07-30 (R5: Fix Custom Editor)

**Задача:** Починить переключение discipline в кастом инспекторе (сломалось после добавления Bows/Crossbows).

**Коммит:** `a79ea91` — T-RTC-R5-fix: fix custom editor discipline switching + stale subtype arrays

**Изменения:**
- `SkillNodeConfig.cs` — `OnValidate` → `AutoSetDisciplineFromPrefix` только при `discipline==None`
- `SkillNodeConfigEditor.cs` — `SubtypesRanged` +Bows/Crossbows; секция Bows/Crossbows Settings; сброс subtype при смене discipline

---

## Итерация от 2026-07-30 (Configurable Cooldown)

**Задача:** Заменить хардкод 0.5f cooldown на настраиваемое поле в SkillNodeConfig.

**Коммит:** `6f871e7` — T-SKILL-06: configurable cooldown per skill

**Изменения:**
- `SkillNodeConfig.cs` — поле `cooldownSeconds` (float, 0.5f default, Range 0.1–30)
- `SkillInputService.cs` — `0.5f` → `skillConfig.cooldownSeconds` / `skillConfig?.cooldownSeconds ?? 0.5f`
- `SkillNodeConfigEditor.cs` — `PropertyField` в секции Active vs Passive
======= REPLACE
