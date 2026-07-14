# Итерации реализации — Ranged & Throwables

## Итерация от 2026-07-31 (#5)

**Задача:** Self-damage fix — отключить урон себе от базовых атак и AOE-скиллов, добавить toggle Allow Self Damage в инспектор
**Коммит:** `643bb1e` — T-CB08: Self-damage fix

**Изменения:**
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` — поле `_allowSelfDamage` (default false) + свойство `AllowSelfDamage`
- `Assets/_Project/Scripts/Combat/Network/CombatServer.cs`:
  - `ResolveAttack` — guard `attackerId == targetId`
  - `CollectAoeTargetsFromRegistry` — параметры `attackerId`, `allowSelfDamage`; фильтр атакующего
  - `ResolveSkillCast` — проброс флагов
- `Assets/_Project/Editor/SkillNodeConfigEditor.cs` — toggle «Allow Self Damage» в инспекторе

## Итерация от 2026-07-31 (#4)

**Задача:** VFX Fix — NPC унификация + player impact + robust pool
**Коммиты:** `413a425` (self-healing Instance), `9cf2cdf` (NPC + impact + pool)

**Исправления:**
- `SkillVfxService.Instance` — lazy-init: если NMC не создал → auto-create
- `SkillVfxService.PlayImpactVfx` — обрабатывает config=null (generic sphere flash)
- `SkillInputService.TryActivate` — `onArrived` callback → `PlayImpactVfx` при приземлении
- `NpcBrain.ExecuteSkillAttack` — +`PlayProjectileVfx` для throwables, +`PlayCastVfx` перед анимацией
- `VfxObjectPool` — `Dictionary<GameObject,Queue<GameObject>>` вместо `EntityId.ToULong`, +null-warnings

**Результат:** VFX работают и для игрока и для NPC. Cast→projectile→impact полный цикл.

## Итерация от 2026-07-31 (#3)

**Задача:** VFX Phase 2 — Примитивные VFX-префабы + назначение на все навыки
**Документ:** `docs/Character/Skills/Battle/85_VFX_DESIGN.md`
**Коммит:** `ad1f5bd` — T-VFX02: Phase 2 — примитивные VFX-префабы

**Изменения:**
- `Assets/_Project/Editor/CreateVfxPrefabs.cs` — NEW: Editor-скрипт (меню Project C > VFX)
- `Assets/_Project/Editor/AssignVfxToSkills.cs` — NEW: Editor-скрипт назначения VFX
- `Assets/_Project/Resources/Vfx/PF_VFX_MuzzleFlash_Basic.prefab` — NEW: вспышка (8 частиц, конус)
- `Assets/_Project/Resources/Vfx/PF_VFX_Impact_Melee.prefab` — NEW: искры (12 частиц, fade)
- `Assets/_Project/Resources/Vfx/PF_VFX_Impact_Explosion.prefab` — NEW: взрыв (20 частиц + дым)
- `Assets/_Project/Resources/Vfx/PF_VFX_Projectile_Arrow.prefab` — NEW: стрела (stretch particles)
- 27 SkillNodeConfig .asset: VFX-поля заполнены по subtype

## Итерация от 2026-07-31 (#2)

**Задача:** VFX Phase 1 — Runtime инфраструктура (ISkillVfxProvider + SkillVfxService + Pool)
**Документ:** `docs/Character/Skills/Battle/85_VFX_DESIGN.md`
**Коммит:** `2712819` — T-VFX01: Phase 1 — VFX runtime infrastructure

**Изменения:**
- `Assets/_Project/Scripts/Skills/Vfx/ISkillVfxProvider.cs` — NEW: интерфейс абстракции
- `Assets/_Project/Scripts/Skills/Vfx/ParticleSystemVfxProvider.cs` — NEW: 3D-реализация
- `Assets/_Project/Scripts/Skills/Vfx/SkillVfxService.cs` — NEW: синглтон-сервис
- `Assets/_Project/Scripts/Skills/Vfx/VfxObjectPool.cs` — NEW: object pool
- `Assets/_Project/Scripts/Skills/Vfx/VfxBoneResolver.cs` — NEW: резолв костей
- `Assets/_Project/Scripts/Skills/Vfx/DamageTypeColors.cs` — NEW: маппинг DamageType→Color
- `Assets/_Project/Scripts/Skills/SkillInputService.cs` — +cast VFX, +VFX-провайдер в throw-пути
- `Assets/_Project/Scripts/Combat/Client/CombatClientState.cs` — +impact VFX
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — +CreateSkillVfxService()

## Итерация от 2026-07-31

**Задача:** VFX Phase 0 — Data Model: поля в SkillNodeConfig + Editor + SpriteAnimationAsset stub
**Документ:** `docs/Character/Skills/Battle/85_VFX_DESIGN.md`
**Коммит:** `8c1471f` — T-VFX00: Phase 0 — VFX data model

**Изменения:**
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` — +VfxAttachPoint enum, +11 VFX-полей (cast/projectile/impact/2D)
- `Assets/_Project/Editor/SkillNodeConfigEditor.cs` — +VFX-секции в инспекторе (Cast/Projectile/Impact/2D Future)
- `Assets/_Project/Scripts/Skills/Vfx/SpriteAnimationAsset.cs` — NEW: заглушка SO для 2D-анимации
- `docs/Character/Skills/Battle/85_VFX_DESIGN.md` — NEW: полный дизайн-документ (архитектура, фазы, 2D-ready)

## Итерация от 2026-07-28

**Задача:** Всплывающие цифры урона (World Space TMP) — для всех типов атак, AOE, критов
**Документ:** `docs/Character/Skills/real-time-combat/110_DAMAGE_NUMBERS.md`

### Коммит 1: `e81221a` — T-DMGNUM01: базовая реализация
- `Assets/_Project/Scripts/Combat/Config/DamageNumberConfig.cs` — NEW: SO-конфиг (цвета по 5 типам урона, размеры normal/crit, кривая фейда, смещение)
- `Assets/_Project/Resources/Combat/DamageNumberConfig_Default.asset` — NEW: дефолтный конфиг
- `Assets/_Project/Scripts/Combat/Client/DamageNumberInstance.cs` — NEW: компонент анимации (корутина float-up + fade)
- `Assets/_Project/Scripts/Combat/Client/DamageNumberService.cs` — NEW: client-side singleton + object pool (prewarm 10, expandable)
- `Assets/_Project/Resources/Prefabs/PF_DamageNumber.prefab` — NEW: World Space Canvas + TMP
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — MOD: +CreateDamageNumberService() по паттерну TargetLockService
- `Assets/_Editor/DamageNumberAssetsCreator.cs` — NEW: Editor-скрипт создания SO + префаба

### Коммит 2: `1b5ca18` — фикс deprecation warnings
- `DamageNumberService.cs`: FindObjectsByType(FindObjectsSortMode) → FindObjectsByType(FindObjectsInactive)

### Коммит 3: `1bdbba5` — billboard + унифицированный размер + пересоздан префаб
- `DamageNumberInstance.cs`: замена ручного LookAt на существующий `ProjectC.UI.Billboard` (keepVertical=true)
- `DamageNumberInstance.cs`: distance scaling — `scale *= (distance / 10м)` → размер одинаков на любом расстоянии
- `PF_DamageNumber.prefab` пересоздан: добавлен Billboard компонент, SDF-шрифт LiberationSans
- `DamageNumberAssetsCreator.cs`: обновлён для включения Billboard и SDF-шрифта в префаб

### Диагностика
- **CombatServer.Instance==null в Edit Mode** — штатное поведение. Сервер существует только в Play Mode при StartHost. SkillInputService.Update работает каждый кадр и логирует отсутствие сервера.
- **Где править цвета/шрифт:** `Assets/_Project/Resources/Combat/DamageNumberConfig_Default.asset` → Inspector

**Flow:**
1. CombatClientState.OnDamageDealt → DamageNumberService.OnDamageDealt
2. DamageNumberService: проверка `CombatConfig.showDamageNumbers` → поиск позиции цели по targetId → спавн из пула
3. DamageNumberInstance: Billboard (авто-камера через ThirdPersonCamera) + анимация (float-up + fade по AnimationCurve) + distance scaling → возврат в пул
4. AOE: каждая цель получает отдельный AttackLandedTargetRpc → OnDamageDealt дёргается N раз — без доп. логики
5. Глобальное отключение: `CombatConfig.showDamageNumbers = false`

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
- `4d90fb3`: `SkillInputService.GetThrowTarget` — throwables (гранаты) учитывают locked target (Q/E): цель в радиусе → точный бросок; цель дальше → бросок в её направлении на throwRange; без лока → forward fallback.

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
