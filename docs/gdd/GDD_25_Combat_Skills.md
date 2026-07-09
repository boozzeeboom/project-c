# ⚔ GDD 25: Combat & Skills System

**Версия:** v2.0 | **Последнее обновление:** 31 июля 2026 | **Статус:** ✅ MVP + Расширения (Ranged, Throwables, Targeting, VFX, Damage Numbers)

---

## 1. Описание системы

Real-time боевая система для пешего режима + **дальний бой (луки/арбалеты/ружья)** + **бросковые навыки (гранаты)** + система навыков (Skill Tree). Server-authoritative damage calculation, AOE формулы, **registry-based targeting (Q/E cycling + outline highlight)**, **VFX-инфраструктура (3 фазы)**, **World Space Damage Numbers**.

**Связанные подсистемы:**
- GDD_01_Core_Gameplay.md — Combat overview
- GDD_20_Progression_RPG.md — Skill Tree progression + Stats refactoring
- GDD_26_Character_Customisation.md — Equipment Visual (weapon prefab)
- `docs/Character/Skills/` — полная документация (Ranged/Throwables, Targeting, VFX, Damage Numbers)

**Тикеты:** T-RTC (Combat MVP), T-CB (SkillManager/SkillTreeWindow), T-RTC-R5 (Ranged projectiles), T-TGT01 (Targeting), T-DMGNUM01 (Damage Numbers), T-VFX00..02 (VFX), T-SKILL-REF-01 (Discipline refactoring), T-SKILL-03..06 (Persistence/Cooldown/Slots), T-WPN-01-REF-02 (Weapon unification)

---

## 2. Combat System

### 2.1 DamageCalculator

**Файл:** `Assets/_Project/Scripts/Combat/DamageCalculator.cs`
**Namespace:** `ProjectC.Combat`

**Формулы (server-authoritative, 5 формул):**

| Формула | Описание | Формула |
|---------|----------|---------|
| Hit/Miss | `(attacker.dex + weapon.accuracy) vs (defender.agi + armor.evasion)` | Если roll > threshold → miss |
| Critical | `(attacker.luck + weapon.critChance) * 0.01` | double damage on crit roll |
| Base Damage | `weapon.damage + (attacker.str * 0.5)` | flat damage before armor |
| Armor Reduction | `max(1, damage - armor.rating * 0.3)` | flat DR с min 1 |
| Skill Modifier | `damage * skillModifier.multiplier` | через SkillModifier chain |

**Поток вызова:**
1. Атакующий вызывает `DamageCalculator.DealDamage(attackerId, targetId, skillType)`
2. Calculator получает статы атакующего (`SkillManager.GetModifiers`)
3. Calculator получат статы цели (NpcStatConfig или NetworkPlayer stats)
4. Roll hit → miss? Roll crit → double?
5. Применяет base damage → armor reduction → skill modifiers
6. Возвращает `DamageResult(hit, crit, finalDamage, mitigatedAmount)`
7. damage deal через NetworkRPC (`NetworkCombat.DealDamageRpc`)

### 2.2 AOEHelper

**Файл:** `Assets/_Project/Scripts/Combat/AOEHelper.cs`
**Namespace:** `ProjectC.Combat`

**5 формул AOE:**

| Метод | Примитив | Входные параметры |
|-------|----------|-------------------|
| `SphereDamage` | Сфера | `origin, radius, maxTargets, damage(float, per-target)` |
| `BoxDamage` | Box (OBB) | `center, halfExtents, rotation, maxTargets, damage` |
| `CapsuleDamage` | Капсула | `point1, point2, radius, maxTargets, damage` |
| `ConeDamage` | Конус | `origin, direction, angle, radius, maxTargets, damage` |
| `RadialDamage` | Радиальная (in/out) | `origin, innerRadius, outerRadius, innerDamage, outerDamage` |

**Key design decision:** Pure C#, no Unity dependencies — используется и сервером и клиентом.

### 2.3 CombatTargeting (устарело → заменено на TargetLockService)

> **Устарело.** Заменено на `TargetLockService` + `TargetHighlightService` (см. §2.5).

### 2.4 Ranged & Throwables (июль 2026)

**Файлы:** `ProjectileVisual.cs`, `ThrowArcVisual.cs`, интеграция в `CombatServer.ResolveSkillCast`

| Подсистема | Оружие | Визуал | Статус |
|-----------|--------|--------|--------|
| **Ranged** | Bow, Crossbow, Pneumatic, MesiumRifle | `ProjectileVisual` — полёт стрелы/пули | ✅ |
| **Throwables** | Grenade Basic, Grenade Antigrav | `ThrowArcVisual` — дуга + взрыв | ✅ |
| **Bows/Crossbows** | Подкатегории + D100 dice + rangedMaxRange | Character-forward raycast | ✅ |
| **ThrowCount** | Потребление X гранат за каст | Проверка наличия в инвентаре | ✅ |

**Key decisions:**
- `RequestSkillCastAtPointRpc` + targetPoint AOE для throwables
- `GetThrowTarget` с приоритетом locked target (Q/E)
- Character-forward raycast (не camera-forward)

### 2.5 Target Highlight & Switching (T-TGT01, июль 2026)

**Файлы:** `TargetHighlightService.cs`, `TargetLockService.cs`
**Namespace:** `ProjectC.Combat`

| Функция | Описание |
|---------|----------|
| Outline highlight | URP inverted-hull shader (`TargetOutline.shader`), оранжевый материал |
| Q/E cycling | `TargetLockService.CycleNext/CyclePrev` — persistent lock |
| Obstruction check | Raycast attacker→target, redirect на obstruction или miss через стену |
| Sorting | ByDistance (ближайшая первая) — выбор в инспекторе |
| Unarmed fallback | sourceId=0 всегда присутствует (даже при наличии оружия) |

### 2.6 Damage Numbers (T-DMGNUM01, июль 2026)

**Файлы:** `DamageNumberService.cs`, `DamageNumberInstance.cs`, `DamageNumberConfig` (SO)

| Компонент | Описание |
|-----------|----------|
| World Space TMP | SDF-шрифт LiberationSans, цвета по 5 типам урона |
| Object Pool | Prewarm 10, expandable |
| Billboard | `ProjectC.UI.Billboard` (keepVertical=true) |
| Distance scaling | `scale *= (distance / 10м)` — унифицированный размер |
| AOE | Каждая цель → отдельный `AttackLandedTargetRpc` → N damage numbers |

**Flow:** `CombatClientState.OnDamageDealt` → `DamageNumberService.OnDamageDealt` → spawn из пула → float-up + fade (AnimationCurve) → возврат в пул.

### 2.7 VFX System (T-VFX00..02, июль 2026)

**3 фазы:**

| Фаза | Коммит | Содержание |
|------|--------|-----------|
| **Phase 0** | `8c1471f` | Data model: `VfxAttachPoint` enum + 11 VFX-полей в `SkillNodeConfig` + Editor |
| **Phase 1** | `2712819` | Runtime: `ISkillVfxProvider` + `SkillVfxService` (синглтон) + `VfxObjectPool` |
| **Phase 2** | `ad1f5bd` | 4 префаба: MuzzleFlash, Impact_Melee, Impact_Explosion, Projectile_Arrow |

**Архитектура:** `ISkillVfxProvider` — абстракция (3D ParticleSystem сейчас, 2D SpriteAnimation в будущем). `SkillVfxService` — единый сервис cast/projectile/impact. `VfxObjectPool` — `Dictionary<GameObject, Queue<GameObject>>`.

**Фиксы:** Self-healing singleton (lazy-init), NPC VFX унификация, player impact fix, robust pool.

---

## 3. Skill System

### 3.1 SkillManager

**Файл:** `Assets/_Project/Scripts/Skills/SkillManager.cs`
**Namespace:** `ProjectC.Skills`

**Server-only singleton.** Отслеживает изученные навыки per client, валидирует prerequisites.

**Методы:**
- `TryLearn(clientId, nodeId)` — проверяет SP и prereqs, добавляет в `_learnedSkills`
- `GetModifiers(clientId, skillType)` — возвращает `List<SkillModifier>` для DamageCalculator
- `HasSkill(clientId, nodeId)` — проверка наличия навыка

### 3.2 SkillNodeConfig (ScriptableObject)

**Путь:** `Assets/_Project/Data/Skills/SkillNodeConfig.asset` (27+ конфигов)

| Поле | Тип | Описание |
|------|-----|----------|
| `nodeName` | string | Отображаемое имя |
| `description` | string | Описание навыка |
| `icon` | Sprite | Иконка в UI |
| `spCost` | int | Очки навыков для изучения |
| `prerequisites` | SkillNodeConfig[] | Какие узлы должны быть изучены |
| `modifiers` | SkillModifier[] | Эффекты навыка (мультиплееры/флаты) |
| `skillType` | SkillType | Связь с анимациями |
| **Новые поля (июль 2026):** | | |
| `combatDiscipline` | CombatDiscipline | Melee/Ranged/Throwable/Social (4 значения) |
| `cooldownSeconds` | float | Настраиваемый кулдаун на навык |
| `throwCount` | int | Сколько гранат потребляется за каст |
| `strTierReq` / `dexTierReq` / `intTierReq` | int | Требования tier'ов для изучения |
| `aoeFormula` | AoeFormula | Тип AOE (5 значений) |
| `aoeSize` / `aoeConeAngleDeg` / `aoeWidth` | float | Параметры AOE |
| `vfxAttachPoint` | VfxAttachPoint | Точка крепления VFX |
| `castVfx` / `projectileVfx` / `impactVfx` | GameObject | VFX-префабы (3 слота) |
| `spriteAnimation` | SpriteAnimationAsset | Заглушка для 2D-анимации (future) |

### 3.3 SkillModifier

**Файл:** `Assets/_Project/Scripts/Skills/SkillModifier.cs`

| Поле | Тип | Описание |
|------|-----|----------|
| `targetSkill` | SkillType | Какой скилл модифицирует |
| `modifierType` | ModifierType | Multiplier или Flat |
| `value` | float | Значение (1.1 = +10%, 10 = +10 flat) |

### 3.4 Catalogs (ScriptableObject)

| Каталог | Путь | Контент |
|---------|------|---------|
| `WeaponCatalog` | `Data/Combat/WeaponCatalog.asset` | 9+ видов оружия (damage, range, attackSpeed, skillType) |
| `ArmorCatalog` | `Data/Combat/ArmorCatalog.asset` | 5+ видов брони (armor, weight, slot, evasion) |
| `TechniqueCatalog` | `Data/Combat/TechniqueCatalog.asset` | 13+ техник/заклинаний (skillType, damage, cooldown, spCost) |

### 3.5 SkillAnimationPlayer

**Файл:** `Assets/_Project/Scripts/Skills/SkillAnimationPlayer.cs`
**Namespace:** `ProjectC.Skills`

Runtime AnimatorOverrideController — загружает анимации по SkillType из `Resources.Load("Animations/Combat/{skillType}")`.

### 3.6 SkillTreeWindow (UI)

**Файл:** `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs`

UIDocument overlay-окно (по паттерну CharacterWindow: Clear+CloneTree+Add, Resources.Load fallback). Zoom/pan, node states (Locked/Available/Unlocked), tooltip на hover.

### 3.7 NetworkSkillTree

**Файл:** `Assets/_Project/Scripts/Network/NetworkSkillTree.cs`

`NetworkVariable<SkillTreeSnapshot>` — sync host→client.

| RPC | Направление | Назначение |
|-----|-------------|------------|
| `RequestLearnRpc(nodeId)` | Client→Server | Запрос на изучение навыка |
| `OnSkillTreeUpdated` | Event | Callback для клиента после broadcast |

---

## 4. Flow: полный цикл атаки (обновлён июль 2026)

### Melee / Unarmed:
1. **Target:** Q/E → TargetLockService → outline highlight (URP shader)
2. **Input:** Skill hotkey → SkillInputService.TryActivate → cooldown guard
3. **Network:** `CombatServer.ResolveAttack` (RPC) → obstruction check → DamageCalculator
4. **Client:** `CombatClientState.OnDamageDealt` → DamageNumberService + impact VFX

### Ranged (bow/crossbow/rifle):
1. **Target:** Q/E locked target priority → character-forward raycast
2. **Input:** Skill hotkey → SkillInputService → ResolveSkillCast (RPC)
3. **Visual:** ProjectileVisual (flight) → impact VFX + Damage Numbers

### Throwables (grenade):
1. **Target:** Q/E locked target → GetThrowTarget (приоритет locked → forward)
2. **Input:** Skill hotkey → throwCount проверка → RequestSkillCastAtPointRpc
3. **Visual:** ThrowArcVisual (дуга + взрыв) → AOE damage per target
4. **Consumption:** throwCount гранат списывается из инвентаря

### AOE (все типы):
- Registry-based target collection (не Physics.OverlapSphere)
- AoeRangePolicy с hitChance=1.0 для thrown skills
- Per-target: obstruction check → DamageCalculator → Damage Numbers

---

## 5. Weapon & Item Refactoring (T-WPN-01-REF-02, июль 2026)

**Проблема:** 3 несвязанные иерархии предметов (`ItemData`, `WeaponItemData`, `ThrowableItemData`), хардкод `is TypeCheck`.

**Решение:**
- `ICombatDamageProvider` — интерфейс боевого предмета
- `WeaponItemData + ThrowableItemData → единый класс` (2 иерархии вместо 3)
- `equipSlot` унифицирован в `ItemData` (базовый класс) + OnValidate auto-set
- Кросс-серверная связность через прямые вызовы (без reflection)

---

## 6. Skill Tree: Persistence, Cooldown, Slots (T-SKILL-03..06, июль 2026)

| Тикет | Функция | Статус |
|-------|---------|--------|
| T-SKILL-03 | Fix skill tree filters + save/load learned skills persistence | ✅ |
| T-SKILL-04 | throwCount consumption — проверка + потребление X гранат за каст | ✅ |
| T-SKILL-05 | Slot bindings persistence — save/load быстрых слотов между сессиями | ✅ |
| T-SKILL-06 | Configurable cooldown per skill (`SkillNodeConfig.cooldownSeconds`) | ✅ |
| T-SKILL-REF-01 | CustomEditor: discipline dropdown (4 значения), subtype filtered | ✅ |

---

## 7. Связанные документы

- [GDD_01_Core_Gameplay.md](GDD_01_Core_Gameplay.md) — combat overview
- [GDD_20_Progression_RPG.md](GDD_20_Progression_RPG.md) — skill tree progression + Stats refactoring
- [GDD_INDEX.md](GDD_INDEX.md) — общая навигация
- `docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md` — Ranged/Throwables design
- `docs/Character/Skills/real-time-combat/100_TARGET_HIGHLIGHT_AND_SWITCHING.md` — Targeting system
- `docs/Character/Skills/real-time-combat/110_DAMAGE_NUMBERS.md` — Damage Numbers
- `docs/Character/Skills/Battle/85_VFX_DESIGN.md` — VFX design (3 фазы)
- `docs/Character/Skills/ITERATIONS.md` — Weapon refactoring log
- `docs/MMO_Development_Plan.md` §1.15 — план Combat + Skills
- `docs/dev/retrospective_d1850f6c_to_HEAD.md` — полная ретроспектива (134 коммита)
