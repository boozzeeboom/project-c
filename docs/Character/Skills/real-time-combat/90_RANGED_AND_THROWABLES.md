# Ranged Combat & Throwables — Implementation

> **Дата:** 2026-07-20 (обновлено 2026-07-24)  
> **Статус:** ✅ Реализовано (Phase R1-R3, T1-T3) + ✅ Унификация оружия (T-WPN-01-REF-02)  
> **Базовый дизайн:** `docs/Character/Skills/Battle/20_SKILL_TREES.md`

---

## TL;DR

Реализованы: дальний бой (projectile visual), бросковые навыки (ThrowArcVisual + target-point AOE).

**⚠️ Обновление 2026-07-24:** `ThrowableItemData` удалён — всё оружие унифицировано в `WeaponItemData`. Гранаты теперь `WeaponItemData` с `handling=Thrown`, лежат в `Items/Weapons/`.

Добавленные файлы (новые): 5  
Изменённые файлы (patch): 3  
Создано .asset: 5 (позже мигрированы в Weapons/)

---

## 1. Созданные Assets

### 1.1 Дальнобойное оружие (`Resources/Items/Weapons/`)

| Файл | Класс | Damage | Range | DamageType |
|---|---|---|---|---|
| `Weapon_Crossbow.asset` | Crossbow | d10+4 | 30м | Ballistic |
| `Weapon_Pneumatic.asset` | Pneumatic | d8+3 | 50м | Ballistic |
| `Weapon_MesiumRifle.asset` | MesiumRifle | d10+5 | 50м | Mesium |

### 1.2 Метательное (`Resources/Items/Weapons/` — бывш. Throwables/)

| Файл | Тип | Damage | Radius | DamageType |
|---|---|---|---|---|
| `Weapon_Grenade_Basic.asset` | Grenade | d10+5 | 3м (Sphere) | Explosive |
| `Weapon_Grenade_Antigrav.asset` | Antigrav Grenade | d8+4 | 3м (Sphere) | Antigrav (crit+15) |

> ⚠️ **2026-07-24:** ThrowableItemData удалён. Гранаты — `WeaponItemData` с `handling=Thrown`, `weaponClass=Throwable`, `equipSlot=None`. Папка `Throwables/` удалена, все `.asset` в `Weapons/`.

---

## 2. Новые C# файлы

### 2.1 `ThrowableItemData.cs` — 🗑 УДАЛЁН (2026-07-24, T-WPN-01-REF-02)

Заменён на `WeaponItemData` с полями: `handling=Thrown`, `weaponClass=Throwable`, `explosionRadius`, `throwRange`, `fuseTimeSec`.

Все throwable-поля (`explosionRadius`, `damageDice`, `baseDamage`, `damageType`, `critModifier`, `fuseTimeSec`, `throwRange`, `aoeFormula`) теперь в `WeaponItemData`.

### 2.2 `ProjectileVisual.cs` — Визуал полёта стрелы/болта/пули

```
Assets/_Project/Scripts/Combat/Client/ProjectileVisual.cs
```

Client-side only. Спавнится при `Ballistic`/`Mesium` атаке через хук в `CombatClientState.HandleAttackLanded`. Простая интерполяция с arc-дугой. Destroy по прибытии.

### 2.3 `ThrowArcVisual.cs` — Визуал броска гранаты + взрыв

```
Assets/_Project/Scripts/Combat/Client/ThrowArcVisual.cs
```

Client-side only. Параболическая дуга полёта, сфера-граната, LineRenderer trail, explosion sphere (scale-up + fade-out).

---

## 3. Изменённые файлы

### 3.1 `CombatServer.cs` — Target-point AOE

- Добавлен `RequestSkillCastAtPointRpc(skillId, Vector3 targetPoint, sourceId)`
- `ResolveSkillCast` принимает опциональный `Vector3? targetPoint`
- Если targetPoint задан — AOE от точки броска (не от атакующего)
- Range-check пропускается для thrown-навыков (цели уже в радиусе от targetPoint)

### 3.2 `SkillInputService.cs` — Throw flow

- Детектит thrown-навыки (Active + Sphere/Box AOE + size > 0)
- При активации: находит targetPoint через camera raycast
- Спавнит `ThrowArcVisual` (client-side)
- Шлёт `RequestSkillCastAtPointRpc` (server-side)

### 3.3 `CombatClientState.cs` — Projectile hook

- При `HandleAttackLanded` для Ballistic/Mesium — спавнит `ProjectileVisual`

### 3.4 `EquipmentServer.cs` — ThrowableItemData регистрация (обновлено 2026-07-24)

- ~~Добавлена папка `Items/Throwables` в `equipFolders[]`~~ — удалено
- ~~Добавлена загрузка `ThrowableItemData` через `Resources.LoadAll`~~ — удалено
- `RegisterEquipmentAssets` переписан без reflection (R2): использует `InventoryWorld.Instance.RegisterIfMissing()`
- Гранаты (`handling=Thrown`) не экипируются: `equipSlot=None` (R3)

---

## 4. Архитектура Flow

### Дальний бой (арбалет/пневматика/мезиевая винтовка)

```
ЛКМ (Primary) → SkillInputService.TryActivate
    → weapon check (requiredWeaponMask)
    → target = raycast/hybrid
    → server.RequestAttackRpc(targetId, sourceId)
    → CombatServer.ResolveAttack(attacker, target, source)
        → RangedRangePolicy (distance check, hitChance по DEX)
        → DamageCalculator.Calculate (roll dice, crit, defense)
        → Broadcast AttackLandedTargetRpc
    → CombatClientState.HandleAttackLanded
        → ProjectileVisual.Fire (стрела летит attacker→target)
```

### Бросок (граната)

```
Slot1 (grenade skill) → SkillInputService.TryActivate
    → weapon check (requiredWeaponMask = None → pass)
    → thrown skill detected (Sphere + isActive)
    → targetPoint = camera raycast hit.point
    → ThrowArcVisual.Fire (парабола + взрыв VFX)
    → server.RequestSkillCastAtPointRpc(skillId, targetPoint, sourceId)
    → CombatServer.ResolveSkillCast(skillId, targetPoint)
        → AOE origin = targetPoint
        → CollectAoeTargets(Sphere, radius)
        → per-target DamageCalculator.Calculate
        → Broadcast AttackLandedTargetRpc per target
    → CombatClientState.HandleAttackLanded per target
        → (no projectile for explosion — ThrowArcVisual already handled)
```

---

## 5. Как тестировать в Play Mode

### 5.1 Проверка дальнего боя

1. Зайти в игру
2. P → SkillTreeWindow → вкладка Ranged → изучить "Базовый лук" (ranged_basic_bow)
3. Подобрать Crossbow из мира (или через seed/debug)
4. Навести камеру на NPC_TestEnemy
5. Нажать ЛКМ
6. **Ожидаемый результат**: 
   - ProjectileVisual (жёлтая стрела) летит от игрока к NPC
   - Console: `[DamageCalculator] ... type=Ballistic`
   - NPC получает урон

### 5.2 Проверка броска гранаты

1. P → SkillTreeWindow → вкладка Explosives → изучить "Граната" (expl_grenade)
2. Привязать гранату на слот (Slot1 = кнопка 1)
3. Навести камеру на группу NPC
4. Нажать 1
5. **Ожидаемый результат**:
   - ThrowArcVisual (оранжевая сфера) летит по параболе
   - При достижении — explosion sphere (оранжевый flash)
   - Console: `[DamageCalculator/AOE] ... skill='expl_grenade'`
   - Все NPC в радиусе получают урон

---

## 6. Открытые вопросы / Дальнейшие шаги

- ❌ **ExplosiveItemData** (T-CB04) не нужен — заменён на `WeaponItemData.Throwable`
- ✅ **Расходуемость** — v0.6: `ConsumeThrowableFromInventory()` в `CombatServer`, гранаты тратятся при успешном касте
- ✅ **Направление броска** — v0.6: character-forward raycast вместо camera-centric
- ✅ **AOE Debug позиция** — v0.6: для thrown навыков рисуется в targetPoint
- ✅ **DamageSource гранат** — v0.6: `ResolveThrowableSourceFromInventory()` создаёт `WeaponDamageSource` из инвентаря
- ❌ **Aim preview arc** (дуга до броска) — Phase 2 UX
- ❌ **Спавн гранаты как предмета в мире** — нужно создать префаб с `PickupItem` + `WeaponItemData`
- ❌ **Звуки** выстрела/взрыва — отдельная задача

---

## 6.5. Архитектурная проблема: навык ≠ источник урона для throwables

**Дата:** 2026-07-25  
**Обнаружено:** в ходе отладки v0.6 — гранаты тратятся некорректно, навык продолжает работать без гранат

### Суть проблемы

Текущая архитектура смешивает две несмешиваемые концепции:

1. **Навык** (`Skill_Explosives_Grenade`) — действие «бросок», активный скилл, привязан к слоту
2. **Предмет** (`Weapon_Grenade_Basic`) — граната в инвентаре, содержит damage-статы (d10+5, explosionRadius=3м)

Сейчас навык САМ наносит урон через `CombatServer.ResolveSkillCast → DamageCalculator`, читая статы из `WeaponDamageSource`. Это неправильно:

- **Навык не должен наносить урон.** Урон — свойство предмета (гранаты). Навык должен только инициировать бросок предмета.
- **`requiredWeaponMask` (T-INP-09) проверяет слоты оружия** (`WeaponMain`/`WeaponOff`), но гранаты лежат в инвентаре (`equipSlot=None`). Если поместить гранату в слот оружия — сломается основная боевая логика.
- **Нет pre-check наличия гранаты перед кастом.** `ConsumeThrowableFromInventory` срабатывает постфактум на сервере, но навык активируется клиентом без проверки инвентаря.

### Правильная архитектура (на будущий рефакторинг)

```
Навык «Бросок гранаты» (SkillNodeConfig)
  ├── isActive = true
  ├── aoeFormula = Sphere / Box
  ├── requiredWeaponMask = Throwable  ← проверять НЕ слоты, а инвентарь
  ├── САМ не наносит урон
  └── Триггерит: взять Throwable из инвентаря → бросить → предмет наносит урон

Предмет «Граната» (WeaponItemData)
  ├── weaponClass = Throwable
  ├── damageDice + baseDamage + explosionRadius  ← источник урона
  ├── расходуется из инвентаря
  └── при достижении targetPoint → AOE damage от имени предмета
```

### Что нужно изменить (следующий большой рефакторинг)

| # | Что | Текущее состояние | Целевое |
|---|-----|-------------------|---------|
| 1 | `requiredWeaponMask` проверка | Только `EquipmentClientState` (слоты) | + `InventoryWorld` поиск для Throwable |
| 2 | Pre-check гранат | Нет — навык активируется всегда | Клиент: перед RPC проверить наличие Throwable в инвентаре |
| 3 | Урон от навыка vs предмета | Навык наносит урон через DamageCalculator | Навык триггерит бросок, ПРЕДМЕТ наносит урон при приземлении |
| 4 | Расходование | Post-hoc `RemoveItems` на сервере | Pre-cast резервирование + commit на сервере |
| 5 | Активные бросковые навыки | Только `isActive` + Sphere/Box AOE | Отдельный `SkillCategory.Thrown` / `CombatDiscipline.Explosives` flow |

- ❌ **Визуальный префаб** для оружия (3D модель арбалета в руке) — `visualPrefab` уже есть в `ItemData`
- ❌ **Звуки** выстрела/взрыва — отдельная задача
- ❌ **Серверная валидация наличия throwable в инвентаре ДО каста** — сейчас `RemoveItems` фейлится молча если нет предмета

---

## 7. История изменений

| Дата | Автор | Изменения |
|---|---|---|
| 2026-07-20 | Aura | Фазы R1-R3, T1-T3: ranged weapons, projectile/throw visuals, target-point AOE |
| 2026-07-24 | Aura | T-WPN-01-REF-02: ThrowableItemData удалён, унификация в WeaponItemData. inventory DTO fix (itemName). ID-коллизия fix. |
| 2026-07-25 | Aura | v0.6: grenade bugfix — throw direction (character-forward), AOE debug at targetPoint, inventory damage source (d10+5), consumption |
| 2026-07-26 | Aura | R5: ranged projectile fix — skills (bows/crossbows/guns) route через ResolveSkillCast с equipped weapon sourceId, не через RequestAttackRpc(sourceId=0). SkillInputService.TryActivate: detection по discipline==Ranged + SingleTarget. CombatServer.ResolveSkillCast: debug logging для ranged single-target. |
======= REPLACE
