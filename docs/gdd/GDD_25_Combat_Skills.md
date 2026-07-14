# ⚔ GDD 25: Combat & Skills System

**Версия:** v2.1 | **Последнее обновление:** 14 июля 2026 | **Статус:** ✅ MVP + Расширения (Ranged, Throwables, Targeting, VFX, Damage Numbers, Skill Tree)
**Автор:** Малков Леонид Андреевич

---

## 1. Описание системы

Real-time боевая система для пешего режима + **дальний бой (луки/арбалеты/ружья)** + **бросковые навыки (гранаты)** + система навыков (Skill Tree). Server-authoritative damage calculation по **ERPR формуле**, registry-based targeting (Q/E cycling + outline highlight URP шейдер), **VFX-инфраструктура (3 фазы: cast/projectile/impact)**, **World Space Damage Numbers**.

**Связанные подсистемы:**
- GDD_01_Core_Gameplay.md — Combat overview
- GDD_20_Progression_RPG.md — Skill Tree progression + Stats refactoring
- GDD_26_Character_Customisation.md — Equipment Visual (weapon prefab)
- `docs/Character/Skills/` — полная документация (Ranged/Throwables, Targeting, VFX, Damage Numbers)

**Тикеты:** T-RTC (Combat MVP), T-CB (SkillManager/SkillTreeWindow), T-RTC-R5 (Ranged projectiles), T-TGT01 (Targeting), T-DMGNUM01 (Damage Numbers), T-VFX00..02 (VFX), T-SKILL-03..06 (Persistence/Cooldown/Slots), T-WPN-01-REF-02 (Weapon unification), T-P11..T-P13 (Skills progression)

---

## 2. Combat System

### 2.1 DamageCalculator

**Файл:** `Assets/_Project/Scripts/Combat/DamageCalculator.cs`
**Namespace:** `ProjectC.Combat`

**Формула ERPR (server-authoritative, dice rolls ТОЛЬКО на сервере):**

```
final = max(0, (1dN + base + STR) × locMult × critMult × skillMult) − effectiveDefense
```

| Компонент | Описание | Формула |
|-----------|----------|---------|
| Base Attack | Бросок дайса + базовый урон + сила | `roll(1dN) + source.GetBaseDamage() + attacker.GetStrength()` |
| Hit/Miss | Проверка через IRangePolicy | `rangePolicy.CalculateHitChance(attacker, defender, source)` → hitChance (0.0–1.0) |
| Crit | 1d100 + critMod ≥ 100 → ×2.0 | `Random.Range(1,101) + source.GetCritModifier() >= 100 → critMult = 2.0` |
| Skill Mult | Модификатор навыков (opt-in) | `source.GetSkillMultiplier(attackerId)` — MVP = 1.0 |
| Armor Reduction | Защита × тип-множитель | `effectiveDefense = armorDefense × typeMultiplier` |
| Location Mult | Отключён в real-time (per 2.17) | `locMult = 1.0` |

**DamageType Armor Multipliers:**

| DamageType | Physical | Ballistic | Antigrav | Explosive | Mesium |
|------------|----------|-----------|----------|-----------|--------|
| ArmorMult  | 1.0      | 1.0       | 0.5      | 0.7       | 0.0 (ignores armor) |

**Поток вызова:**
1. Атакующий вызывает `DamageCalculator.Calculate(attacker, defender, source, rangePolicy, skill)`
2. Calculator использует интерфейсы: `IAttacker` (статы атакующего), `IDamageTarget` (защита цели), `IDamageSource` (параметры оружия/навыка), `IRangePolicy` (hitChance)
3. Реализации: `PlayerAttacker`, `NpcAttacker`, `PlayerTarget`, `NpcTarget`, `WeaponDamageSource`, `DefaultDamageSource`, `MeleeRangePolicy`, `RangedRangePolicy`, `AoeRangePolicy`
4. Roll hit → miss? Roll crit → double?
5. Применяет base → armor reduction → skill modifiers
6. Возвращает `DamageResult(hit, crit, finalDamage, mitigatedAmount, ...)`
7. Damage передаётся через `CombatServer.ResolveAttack`/`CombatServer.ResolveSkillCast` (RPC)

### 2.2 IRangePolicy & Реализации

**Файлы:** `Assets/_Project/Scripts/Combat/Core/IRangePolicy.cs`, `Implementations/MeleeRangePolicy.cs`, `RangedRangePolicy.cs`, `AoeRangePolicy.cs`

| Реализация | Описание | hitChance |
|------------|----------|-----------|
| `MeleeRangePolicy` | Ближний бой (melee weapons) | ~0.9 (высокий) |
| `RangedRangePolicy` | Дальний бой (луки/арбалеты/ружья) | Зависит от дистанции, дальности оружия |
| `AoeRangePolicy` | AOE навыки (взрывы, конусы) | hitChance=1.0 для thrown skills |

### 2.3 Targeting System (T-TGT01)

**Устаревшее:** `CombatTargeting` — заменено на `TargetLockService` + `TargetHighlightService`.

**Файлы:** `Assets/_Project/Scripts/Combat/Client/TargetLockService.cs`, `TargetHighlightService.cs`
**Namespace:** `ProjectC.Combat`

| Функция | Описание |
|---------|----------|
| **Q/E cycling** | `TargetLockService.CycleNext()` / `CyclePrev()` — persistent lock, сортировка по дистанции |
| **Outline highlight** | URP inverted-hull shader (`TargetOutline.shader`), оранжевый материал |
| **Obstruction check** | Raycast attacker→target, redirect на obstruction или miss через стену |
| **Sorting** | ByDistance (ближайшая первая) — выбор в инспекторе |
| **Unarmed fallback** | sourceId=0 всегда присутствует (даже при наличии оружия) |
| **Registry-based** | `TargetingService` (Core) — сбор целей через реестр, не `Physics.OverlapSphere` |

### 2.4 Ranged & Throwables

**Файлы:** `Assets/_Project/Scripts/Combat/Client/ProjectileVisual.cs`, `ThrowArcVisual.cs`
**Namespace:** `ProjectC.Combat`

| Подсистема | Оружие | Визуал | Статус |
|-----------|--------|--------|--------|
| **Ranged** | Bow, Crossbow, Pneumatic, MesiumRifle | `ProjectileVisual` — полёт снаряда | ✅ |
| **Throwables** | Grenade Basic, Grenade Antigrav | `ThrowArcVisual` — дуга + взрыв | ✅ |
| **Bows/Crossbows** | Character-forward raycast + D100 hit/damage | `rangedMaxRange`, `rangedHitChance` | ✅ |
| **ThrowCount** | Потребление X гранат за каст | Проверка наличия в инвентаре | ✅ |

**Key decisions:**
- `RequestSkillCastAtPointRpc` + targetPoint AOE для throwables
- `GetThrowTarget` с приоритетом locked target (Q/E)
- Character-forward raycast (не camera-forward)

### 2.5 Damage Numbers (T-DMGNUM01)

**Файлы:** `Assets/_Project/Scripts/Combat/Client/DamageNumberService.cs`, `DamageNumberInstance.cs`, `Config/DamageNumberConfig.cs`

| Компонент | Описание |
|-----------|----------|
| World Space TMP | SDF-шрифт LiberationSans, цвета по 5 типам урона (`DamageTypeColors.cs`) |
| Object Pool | Prewarm 10, expandable (`VfxObjectPool`) |
| Billboard | `ProjectC.UI.Billboard` (keepVertical=true) |
| Distance scaling | `scale *= (distance / 10м)` — унифицированный размер |
| AOE | Каждая цель → отдельный `AttackLandedTargetRpc` → N damage numbers |

**Flow:** `CombatClientState.OnDamageDealt` → `DamageNumberService.OnDamageDealt` → spawn из пула → float-up + fade (AnimationCurve) → возврат в пул.

### 2.6 VFX System (T-VFX00..02)

**Файлы:** `Assets/_Project/Scripts/Skills/Vfx/`
**Namespace:** `ProjectC.Skills`

**Архитектура:**
| Компонент | Описание |
|-----------|----------|
| `ISkillVfxProvider` | Абстракция (3D ParticleSystem сейчас, 2D SpriteAnimation в будущем) |
| `SkillVfxService` | Единый сервис cast/projectile/impact (синглтон) |
| `ParticleSystemVfxProvider` | Реализация для ParticleSystem |
| `VfxObjectPool` | `Dictionary<GameObject, Queue<GameObject>>` |
| `VfxBoneResolver` | Маппинг VfxAttachPoint → bone трансформ |
| `SpriteAnimationAsset` | Заглушка для 2D-анимации (Phase 3) |

**3 фазы VFX:**
| Фаза | Коммит | Содержание |
|------|--------|-----------|
| **Phase 0** | `8c1471f` | Data model: `VfxAttachPoint` enum + 11 VFX-полей в `SkillNodeConfig` |
| **Phase 1** | `2712819` | Runtime: `ISkillVfxProvider` + `SkillVfxService` + `VfxObjectPool` |
| **Phase 2** | `ad1f5bd` | 4 префаба: MuzzleFlash, Impact_Melee, Impact_Explosion, Projectile_Arrow |

**VfxAttachPoint:**
| Точка | Bone |
|-------|------|
| `WeaponMain` | RightHand |
| `WeaponOff` | LeftHand |
| `Chest` | Spine |
| `Head` | Head |
| `Root` | Корень (ноги) |

---

## 3. Skill System

### 3.1 SkillsServer & SkillsWorld

**Файлы:** `Assets/_Project/Scripts/Skills/SkillsServer.cs`, `SkillsWorld.cs`
**Namespace:** `ProjectC.Skills`

**SkillsServer** — NetworkBehaviour RPC hub. Scene-placed в BootstrapScene.

| RPC | Направление | Назначение |
|-----|-------------|------------|
| `RequestLearnSkillRpc(skillId)` | Client→Server | Запрос на изучение навыка |
| `RequestForgetSkillRpc(skillId)` | Client→Server | Запрос на забывание навыка (Q3.4 free respec) |
| `ReceiveSkillResultTargetRpc(result)` | Server→Client | Результат learn/forget |
| `ReceiveSkillsSnapshotTargetRpc(snapshot)` | Server→Client | Полный snapshot навыков |

**SkillsWorld** — POCO singleton (server-side per-player state).

**Методы:**
- `LoadAllSkills(SkillsConfig)` — загрузка 27+ SkillNodeConfig из Resources
- `TryLearnSkill(clientId, skillId, out reason)` — 5-step validation (существует? уже изучен? prereqs? INT tier? XP cost?)
- `TryForgetSkill(clientId, skillId, out reason)` — free respec, XP NOT refunded
- `GetLearnedSkillIds(clientId)` — все выученные навыки игрока
- `BuildSaveData/LoadPlayer` — persistence

### 3.2 SkillsClientState

**Файл:** `Assets/_Project/Scripts/Skills/SkillsClientState.cs`

Client-side singleton. Хранит локальный кэш skill tree snapshot, предоставляет события для UI.

### 3.3 SkillNodeConfig (ScriptableObject)

**Путь:** `Assets/_Project/Data/Skills/` (27+ конфигов)

**Поля:**

| Поле | Тип | Описание |
|------|-----|----------|
| `skillId` | string | Стабильный ключ (напр. `melee_basic_strike`) |
| `displayName` | string | Отображаемое имя |
| `description` | string | Описание навыка |
| `icon` | Sprite | Иконка в UI |
| `category` | SkillCategory | Social (0) / Combat (1) |
| `discipline` | CombatDiscipline | None/Combat/Melee/Ranged/Defense/Placed |
| `subtype` | CombatSubtype | None/Throwables/Traps/Bows/Crossbows |
| `prerequisites` | SkillNodeConfig[] | DAG предварительных навыков |
| `effects` | SkillEffect[] | Эффекты (StatMod, AbilityUnlock, PassiveEffect...) |
| `learnXpCost` | float | XP стоимость изучения |
| `requiredStrengthTier` | int | Требование STR tier |
| `requiredDexterityTier` | int | Требование DEX tier |
| `requiredIntelligenceTier` | int | Требование INT tier |
| `requiredWeaponMask` | WeaponClassMask | Требуемый класс оружия |
| `isActive` | bool | Active (bindable) vs Passive |
| `cooldownSeconds` | float | Кулдаун (0.1–30 сек) |
| `attackClip` | AnimationClip | Анимация навыка |
| `attackClipSpeed` | float | Скорость анимации (0.1–3.0) |
| `aoeFormula` | AoeFormula | SingleTarget/Cone/Sphere/Line/Box |
| `aoeSize` | float | Размер AOE (м) |
| `aoeConeAngleDeg` | float | Угол конуса (0–360°) |
| `aoeWidth` | float | Ширина линии/бокса |
| `throwRange` | float | Дальность броска (throwables) |
| `throwScatter` | int | Разброс броска D6 |
| `throwCount` | int | Кол-во гранат за каст |
| `rangedMaxRange` | float | Макс. дальность (луки/арбалеты) |
| `rangedHitChance` | float | Шанс попадания D100 (bows/crossbows) |
| `castVfxPrefab` | GameObject | VFX каста |
| `castSpawnPoint` | VfxAttachPoint | Точка спавна каста |
| `castVfxDuration` | float | Длительность каста |
| `projectileVfxPrefab` | GameObject | VFX снаряда |
| `projectileSpeed` | float | Скорость снаряда |
| `projectileArcHeight` | float | Высота дуги |
| `impactVfxPrefab` | GameObject | VFX попадания |
| `impactScaleByDamage` | bool | Масштаб по урону |
| `impactColorByDamageType` | bool | Окраска по типу урона |
| `twoDVfxAnimation` | SpriteAnimationAsset | 2D анимация (future) |

**Enums:**
| Enum | Значения |
|------|----------|
| `SkillCategory` | Social, Combat |
| `CombatDiscipline` | None, Combat, Melee, Ranged, Defense, Placed |
| `CombatSubtype` | None, Throwables, Traps, Bows, Crossbows |
| `AoeFormula` | SingleTarget, Cone, Sphere, Line, Box |
| `VfxAttachPoint` | WeaponMain, WeaponOff, Chest, Head, Root |

### 3.4 SkillEffect

**Файл:** `Assets/_Project/Scripts/Skills/SkillEffect.cs`

```csharp
public struct SkillEffect
{
    public enum Type : byte {
        StatMod        = 0,  // +X к STR/DEX/INT
        AbilityUnlock  = 1,  // открывает ability ID
        PassiveEffect  = 2,  // generic passive
        // T-CB01:
        WeaponProficiencyUnlock  = 3,
        ArmorProficiencyUnlock   = 4,
        TechniqueUnlock          = 5,
        ExplosiveRecipeUnlock    = 6,
        AntigravTechniqueUnlock  = 7,
    }
    public Type effectType;
    public StatType targetStat;      // для StatMod
    public float additive;           // +X
    public float multiplicative;     // ×Y
    public string stringParam;       // ability ID / weapon class / etc.
}
```

### 3.5 Catalogs (ScriptableObject)

| Каталог | Путь | Контент |
|---------|------|---------|
| `WeaponClassCatalog` | `Data/Combat/WeaponClassCatalog.asset` | 9+ классов оружия (damage, range, class type) |
| `ArmorClassCatalog` | `Data/Combat/ArmorClassCatalog.asset` | 5+ классов брони (armor, weight, slot) |
| `WeaponTechniqueCatalog` | `Data/Combat/WeaponTechniqueCatalog.asset` | 13+ техник/заклинаний (skillType, damage, cooldown) |

### 3.6 SkillAnimationPlayer

**Файл:** `Assets/_Project/Scripts/Skills/SkillAnimationPlayer.cs`
**Namespace:** `ProjectC.Skills`

Runtime AnimatorOverrideController — загружает анимации по `SkillNodeConfig.attackClip`. Поддержка `attackClipSpeed` для вариативной скорости.

### 3.7 SkillTreeWindow (UI)

**Файл:** `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs`

UIDocument overlay-окно (по паттерну CharacterWindow: Clear+CloneTree+Add, Resources.Load fallback). Zoom/pan, node states (Locked/Available/Unlocked), tooltip на hover. Синхронизация через `SkillsClientState`.

### 3.8 SkillInputService

**Файл:** `Assets/_Project/Scripts/Skills/SkillInputService.cs`

Обрабатывает ввод с клавиш 1-9 (skill slots). Проверяет cooldown, weapon requirement, анимацию. Активирует навык через RPC.

---

## 4. Flow: полный цикл атаки

### Melee / Unarmed:
1. **Target:** Q/E → TargetLockService → outline highlight (URP shader)
2. **Input:** Skill hotkey → SkillInputService.TryActivate → cooldown + weapon mask guard
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
- Registry-based target collection (через `TargetingService`, не `Physics.OverlapSphere`)
- `AoeRangePolicy` с hitChance=1.0 для thrown skills
- Per-target: obstruction check → DamageCalculator → Damage Numbers

---

## 5. Weapon & Item Refactoring (T-WPN-01-REF-02)

**Проблема:** 3 несвязанные иерархии предметов (`ItemData`, `WeaponItemData`, `ThrowableItemData`), хардкод `is TypeCheck`.

**Решение:**
- `ICombatDamageProvider` — интерфейс боевого предмета
- `ThrowableItemData` упразднён, всё в `WeaponItemData`
- `equipSlot` унифицирован в `ItemData` (базовый класс) + OnValidate auto-set
- `WeaponDamageSource` — адаптер между `ICombatDamageProvider` и `IDamageSource`
- Кросс-серверная связность через прямые вызовы (без reflection)

---

## 6. Skill Tree: Persistence, Cooldown, Slots (T-SKILL-03..06)

| Тикет | Функция | Статус |
|-------|---------|--------|
| T-SKILL-03 | Fix skill tree filters + save/load learned skills persistence | ✅ |
| T-SKILL-04 | throwCount consumption — проверка + потребление X гранат за каст | ✅ |
| T-SKILL-05 | Slot bindings persistence — save/load быстрых слотов между сессиями | ✅ |
| T-SKILL-06 | Configurable cooldown per skill (`SkillNodeConfig.cooldownSeconds`) | ✅ |
| T-SKILL-REF-01 | CustomEditor: discipline dropdown (4 значения), subtype filtered | ✅ |
| T-P11 | SkillNodeConfig полная версия (effects, циклы, stat tiers) | ✅ |
| T-P12 | SkillsWorld — per-player state + persistence | ✅ |
| T-P13 | SkillsServer — RPC hub + rate limit | ✅ |

**Cooldown system:** Реализован через `SkillNodeConfig.cooldownSeconds`. Проверяется в `SkillInputService.TryActivate`. Per-skill, per-player.

**Skill slots:** 1-9 hotkeys. Bind через UI (drag skill node → slot). Persistence через T-SKILL-05.

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
