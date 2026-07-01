# ⚔ GDD 25: Combat & Skills System

**Версия:** v1.0 | **Последнее обновление:** 30 июня 2026 | **Статус:** ✅ MVP реализован

---

## 1. Описание системы

Real-time боевая система для пешего режима (punch/kick/block) + система навыков (Skill Tree). Server-authoritative damage calculation, AOE формулы, raycast-прицеливание.

**Связанные подсистемы:**
- GDD_01_Core_Gameplay.md §X.4 — Combat overview
- GDD_20_Progression_RPG.md §X.5 — Skill Tree progression
- GDD_26_Character_Customisation.md — Equipment Visual (weapon prefab)
- `docs/Character/Skills/20_IMPLEMENTATION.md` — полная документация

**Тикеты:** T-RTC (Combat MVP), T-CB-22 (SkillManager), T-CB-23 (SkillTreeWindow), T-CB-19 (WeaponItemData)

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

### 2.3 CombatTargeting

**Файл:** `Assets/_Project/Scripts/Combat/CombatTargeting.cs`
**Namespace:** `ProjectC.Combat`

| Функция | Описание |
|---------|----------|
| Raycast-прицеливание | С камеры, R-клавиша для переключения |
| Подсветка цели | Outline-эффект на target |
| Target cycle | R → next ближайший враг в радиусе |
| Clear target | R на пустом месте или повтор R на последней цели |

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

**Путь:** `Assets/_Project/Data/Skills/SkillNodeConfig.asset`

| Поле | Тип | Описание |
|------|-----|----------|
| `nodeName` | string | Отображаемое имя |
| `description` | string | Описание навыка |
| `icon` | Sprite | Иконка в UI |
| `spCost` | int | Очки навыков для изучения |
| `prerequisites` | SkillNodeConfig[] | Какие узлы должны быть изучены |
| `modifiers` | SkillModifier[] | Эффекты навыка (мультиплееры/флаты) |
| `skillType` | SkillType | Связь с анимациями (Punch/Kick/Block/Sword/...) |

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

## 4. Flow: полный цикл атаки

1. **Target:** R → CombatTargeting.raycast → outline на цели
2. **Input:** F (attack) → PlayerController.Attack()
3. **Network:** `NetworkCombat.RequestDealDamageRpc(targetId, skillType)`
4. **Server:** `SkillManager.GetModifiers(clientId, skillType)` → chain SkillModifier[]
5. **Server:** `DamageCalculator.DealDamage()` → hit/miss/crit/armor/skill
6. **Server:** `NpcBrain.TakeDamage(finalDamage)` или `NetworkPlayer.TakeDamage(finalDamage)`
7. **Network:** `ClientRpc` broadcast damage result
8. **Client:** UI floating damage number, health bar update, hit feedback

---

## 5. Связанные документы

- [GDD_01_Core_Gameplay.md](GDD_01_Core_Gameplay.md) §X.4 — combat overview
- [GDD_20_Progression_RPG.md](GDD_20_Progression_RPG.md) §X.5 — skill tree progression
- [GDD_INDEX.md](GDD_INDEX.md) — общая навигация
- `docs/Character/Skills/20_IMPLEMENTATION.md` — полная документация
- `docs/MMO_Development_Plan.md` §1.15 — план Combat + Skills
- `docs/dev/COMBAT_ENGINE_IMPL_PLAN.md` — исходный план имплементации
