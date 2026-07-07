# Ranged Combat & Throwables — Implementation

> **Дата:** 2026-07-20  
> **Статус:** ✅ Реализовано (Phase R1-R3, T1-T3)  
> **Базовый дизайн:** `docs/Character/Skills/Battle/20_SKILL_TREES.md`

---

## TL;DR

Реализованы: дальний бой (projectile visual), бросковые навыки (ThrowArcVisual + target-point AOE), новые типы предметов (ThrowableItemData).

Добавленные файлы (новые): 5  
Изменённые файлы (patch): 3  
Создано .asset: 5

---

## 1. Созданные Assets

### 1.1 Дальнобойное оружие (`Resources/Items/Weapons/`)

| Файл | Класс | Damage | Range | DamageType |
|---|---|---|---|---|
| `Weapon_Crossbow.asset` | Crossbow | d10+4 | 30м | Ballistic |
| `Weapon_Pneumatic.asset` | Pneumatic | d8+3 | 50м | Ballistic |
| `Weapon_MesiumRifle.asset` | MesiumRifle | d10+5 | 50м | Mesium |

### 1.2 Метательное (`Resources/Items/Throwables/`)

| Файл | Тип | Damage | Radius | DamageType |
|---|---|---|---|---|
| `Throwable_Grenade_Basic.asset` | Grenade | d10+5 | 3м (Sphere) | Explosive |
| `Throwable_Grenade_Antigrav.asset` | Antigrav Grenade | d8+4 | 3м (Sphere) | Antigrav (crit+15) |

---

## 2. Новые C# файлы

### 2.1 `ThrowableItemData.cs` — ScriptableObject для метательных предметов

```
Assets/_Project/Scripts/Equipment/ThrowableItemData.cs
```

Наследует `ItemData`. Поля: `explosionRadius`, `damageDice`, `baseDamage`, `damageType`, `critModifier`, `fuseTimeSec`, `throwRange`, `aoeFormula`.

Регистрируется в `EquipmentServer.RegisterEquipmentAssets` через папку `Items/Throwables`.

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

### 3.4 `EquipmentServer.cs` — ThrowableItemData регистрация

- Добавлена папка `Items/Throwables` в `equipFolders[]`
- Добавлена загрузка `ThrowableItemData` через `Resources.LoadAll`

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

- ❌ **ExplosiveItemData** (T-CB04) не нужен — заменён на `ThrowableItemData`
- ❌ **Aim preview arc** (дуга до броска) — Phase 2 UX
- ❌ **Спавн гранаты как предмета в мире** — нужно создать префаб с `PickupItem` + `ThrowableItemData`
- ❌ **Расходуемость**: сейчас гранаты бесконечны. Нужен inventory-check в TryActivate
- ❌ **Визуальный префаб** для оружия (3D модель арбалета в руке) — `visualPrefab` уже есть в `ItemData`
- ❌ **Звуки** выстрела/взрыва — отдельная задача

---

## 7. История изменений

| Дата | Автор | Изменения |
|---|---|---|
| 2026-07-20 | Aura | Фазы R1-R3, T1-T3: ranged weapons, projectile/throw visuals, target-point AOE |
