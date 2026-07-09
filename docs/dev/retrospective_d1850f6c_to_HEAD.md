# Ретроспектива: d1850f6c → HEAD

> **Диапазон:** `d1850f6cec6911d968225fac81c7640d50544978` → текущий HEAD  
> **Коммитов:** 134  
> **Период:** ~2026-07-07 — 2026-07-31 (~24 дня)  
> **Тикет-префиксы:** T-WPN, T-GRN, T-SKILL, T-RTC, T-TGT, T-DMGNUM, T-VFX, T-STAT, T-NPC, T-CRAFT, T-MINE, T-QAUDIT, T-UI, T-INV, T-INP  

---

## 1. Общая статистика

| Метрика | Значение |
|---|---|
| Коммитов | **134** |
| Основных тикет-эпиков | **17** |
| Phases в NPC Behavior | **4** (S01-S21) |
| Phase-ов в VFX | **3** (Phase 0-2) |
| Phase-ов в Stats Refactoring | **5** (T-STAT01..05) |
| Аудитов (Quest/Crafting/Mining/Stats) | **6** |
| Документов создано/обновлено | **30+** |
| Ключевых архитектурных решений | **10+** |

---

## 2. Поэпическое саммари

### 2.1. Дальний бой и бросковые навыки (Ranged + Throwables)
**Коммиты:** `064843e` — `055ff89`  
**Документы:** `docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md`

- Фазы R1-R3 (ranged: bow/crossbow/pneumatic/rifle) и T1-T3 (thrown: grenade/antigrav)
- `ProjectileVisual.cs` — визуал полёта стрелы
- `ThrowArcVisual.cs` — визуал броска гранаты + взрыв
- `ThrowableItemData.cs` — новый SO-тип (позже объединён с WeaponItemData)
- Новые предметы: Weapon_Crossbow, Weapon_Pneumatic, Weapon_MesiumRifle, Throwable_Grenade_Basic, Throwable_Grenade_Antigrav
- `RequestSkillCastAtPointRpc` + targetPoint AOE в CombatServer
- Фикс: crossbow itemType=Equipment + grenade INT tier=0

### 2.2. Рефакторинг оружия: унификация иерархии предметов (T-WPN-01-REF-02)
**Коммиты:** `ac4e8a7` — `8391461` (основные) + `ae35a27` (доки)  
**Документы:** `docs/Character/Skills/ITERATIONS.md`, `docs/Character/Skills/AUDIT_2026-07-24_ITEM_WEAPON_REFACTOR.md`

**Что сделано:**
- `ICombatDamageProvider` — интерфейс боевого предмета (убран хардкод `is WeaponItemData`)
- `WeaponItemData + ThrowableItemData → единый класс` (ThrowableItemData удалён, поля перенесены)
- `equipSlot` унифицирован: добавлен в ItemData (базовый класс) + OnValidate auto-set
- 2 иерархии предметов вместо 3 (ItemData + WeaponItemData; ClothingItemData пока отдельно)
- Инвентарь: equipSlot в InventoryTab без хардкода
- Кросс-серверная связность: EquipmentServer/EquipmentWorld/SkillsServer/SkillsWorld через прямые вызовы вместо reflection

**Фиксы (3 post-merge):**
1. `_itemCache` заполняется из ItemRegistry на клиенте (не только через InventoryWorld)
2. ID-коллизия: `_itemDatabase.Count + 1` → `while (_itemDatabase.ContainsKey(newId)) newId++`
3. `itemName` в снапшоте DTO вместо lookup по кэшу (расcинхрон динамических ID)

### 2.3. STR/DEX/INT tier requirements + grenade bugfixes (T-GRN01)
**Коммиты:** `0ccadd2` — `586232d`

- `STR/DEX/INT tier requirements` для изучения скиллов (SkillNodeConfig)
- Гранты: fix throw direction, AOE debug, damage source, consumption
- Фикс: equipSlot в WeaponItemData/ThrowableItemData + InventoryTab без хардкода

### 2.4. Рефакторинг боевых дисциплин и инспектора навыков (T-SKILL-REF-01)
**Коммиты:** `f6cfac5` — `757c90f` (13 коммитов)  
**Документы:** `docs/Character/Skills/Battle/` (60-70_SKILL_TREE_DESIGN, 100_SKILL_REFACTOR_PLAN)

**Phases:**
- **Phase A+B:** Рефакторинг боевых дисциплин — CombatDiscipline enum, Social vs Combat разделение
- **Phase C+D+E:** CustomEditor для SkillNodeConfig, SkillTreeWindow filter fix, runtime subtype-based throw
- CustomEditor: social hides combat fields, discipline dropdown (4 values: Melee/Ranged/Throwable/Social), subtype filtered per discipline

**Фиксы (8 штук):**
- Throwables: skip equipment slots, search inventory directly; use player inventory not item database
- AOE: registry-based target collection вместо Physics.OverlapSphere
- AoeRangePolicy с hitChance=1.0 для thrown skills
- Inventory snapshot push после throwable consumption
- Всегда consume throwable (не только on hit)
- AOE debug logging (per-target distance check) — unconditional
- Damage logs — unconditional (isHit/finalDamage/hpAfter per target)

### 2.5. Skill Tree: persistence, throwCount, slot bindings, cooldown (T-SKILL-03/04/05/06)
**Коммиты:** `aa1fa32` — `7e9ac31`

- **T-SKILL-03:** Fix skill tree filters + save/load learned skills persistence
- **T-SKILL-04:** throwCount consumption — проверка наличия + потребление X гранат за каст
- **T-SKILL-05:** Slot bindings persistence — save/load быстрых слотов между сессиями
- **T-SKILL-06:** Configurable cooldown per skill (SkillNodeConfig.cooldownSeconds)
- Документирование: session #6, #7, #8 changelogs + R5 bows/crossbows iterations

### 2.6. Ranged projectile fix + bows/crossbows (T-RTC-R5)
**Коммиты:** `695656f` — `a79ea91`

- Навыки луков/арбалетов/ружей работают через ResolveSkillCast
- RebuildSources fallback при race condition загрузки экипировки
- Character-forward raycast вместо camera-forward
- Подкатегории Bows/Crossbows + D100 dice + rangedMaxRange
- Fix custom editor discipline switching + stale subtype arrays

### 2.7. Target Highlight & Switching (T-TGT01)
**Коммиты:** `e303a68` — `9a29d2d` (7 коммитов)  
**Документы:** `docs/Character/Skills/real-time-combat/100_TARGET_HIGHLIGHT_AND_SWITCHING.md`

- TargetOutline.shader (URP inverted-hull) + M_TargetOutline.mat (оранжевый)
- TargetHighlightService — client-side singleton для outline highlighting
- TargetLockService — persistent target lock + Q/E cycling
- InputBindingsConfig: targetPrevKey(Q) / targetNextKey(E)
- Obstruction check: SingleTarget redirect + AOE per-target на сервере
- Сортировка целей Q/E — режим ByDistance (ближайшая первая) + выбор в инспекторе

**Фиксы:**
- Unarmed fallback (sourceId=0) всегда присутствует, даже при наличии оружия
- Obstruction check для ResolveAttack (unarmed/melee)
- FireImpactRpc bypass cooldown при skipAnimation=true
- Throwables — GetThrowTarget с приоритетом locked target (Q/E)

### 2.8. Damage Numbers (T-DMGNUM01)
**Коммиты:** `e81221a` — `e851eab` (4 коммита)  
**Документы:** `docs/Character/Skills/real-time-combat/110_DAMAGE_NUMBERS.md`

- World Space TMP для всех атак, навыков, AOE и критов
- DamageNumberConfig SO (цвета по 5 типам урона, размеры normal/crit, кривая фейда)
- DamageNumberService — client-side singleton + object pool (prewarm 10, expandable)
- DamageNumberInstance — корутина float-up + fade + Billboard + distance scaling
- PF_DamageNumber.prefab (World Space Canvas + TMP + SDF-шрифт LiberationSans)
- Фикс: FindObjectsByType deprecation (FindObjectsSortMode → FindObjectsInactive)
- Документация: commit history, диагностика, инструкция по настройке

### 2.9. VFX System (T-VFX00..T-VFX02)
**Коммиты:** `8c1471f` — `bc60ffa` (7 коммитов)  
**Документы:** `docs/Character/Skills/Battle/85_VFX_DESIGN.md`

**Phase 0 — Data Model:**
- SkillNodeConfig: +VfxAttachPoint enum, +11 VFX-полей (cast/projectile/impact/2D)
- SkillNodeConfigEditor: +VFX-секции в инспекторе
- SpriteAnimationAsset (заглушка для 2D)

**Phase 1 — Runtime Infrastructure:**
- ISkillVfxProvider + ParticleSystemVfxProvider + SkillVfxService (синглтон)
- VfxObjectPool + VfxBoneResolver + DamageTypeColors
- Интеграция в SkillInputService, CombatClientState, NetworkManagerController

**Phase 2 — Примитивные префабы:**
- PF_VFX_MuzzleFlash_Basic (8 частиц, конус)
- PF_VFX_Impact_Melee (12 частиц, fade), PF_VFX_Impact_Explosion (20 частиц + дым)
- PF_VFX_Projectile_Arrow (stretch particles)
- 27 SkillNodeConfig .asset заполнены VFX-полями по subtype
- CreateVfxPrefabs.cs + AssignVfxToSkills.cs (Editor-скрипты)

**Фиксы (v2):**
- SkillVfxService self-healing singleton (lazy-init при отсутствии NMC)
- Generic impact fallback (config=null → sphere flash)
- NPC VFX: +PlayProjectileVfx для throwables, +PlayCastVfx перед анимацией
- Player impact fix: `onArrived` callback → PlayImpactVfx
- Robust pool: `Dictionary<GameObject, Queue<GameObject>>` вместо EntityId.ToULong

### 2.10. Stats Architecture Audit & Refactoring (T-STAT01..T-STAT05)
**Коммиты:** `857f442` — `20c26cf` (11 коммитов)  
**Документы:** `docs/Character/11_STATS_ARCHITECTURE_AUDIT.md`, `12_STATS_ARCHITECTURE_AUDIT_V2.md`, `13_SESSION_CONTINUATION.md`, `14_PLAYTESTS_STATS_AUDIT.md`

**T-STAT01 — Архитектурный аудит:**
- Выявлено 7 структурных проблем (P0-P10):
  - P0: Equip/skill stat bonuses не влияют на combat
  - P1: Hardcoded stat explosion — 3 стата × N полей дублируются в 21 файле
  - P2: PlayerStatsRef — workaround для hardcoded полей
  - P3: NPC-статы — полностью дублированная система
  - P4: StatsConfig overload (4 responsibilities)
  - P5-P10: GatheringServer bypass, formula inconsistency, dead data и др.

**T-STATS02 — P0/P5/P6/P7/P10 (6 проблем):**
- Combat: PlayerAttacker использует effective stats (tier + equip + skill bonuses)
- PlayerStats.StatsToFlat(tier) — public static
- StatsServer: effective stat = StatsToFlat(tier) + bonus
- GatheringServer: StatType.Strength → ss.GetStatFor(XpSource.Mining)
- DamageResultDto: +diceRoll/strengthContrib/baseContrib поля

**T-STATS03 — P4 StatsConfig Split:**
- StatsConfig разделён на 3 SO: ExperienceConfig, StatSourceMapConfig, StatDebugConfig

**P1 — PlayerStats flat struct → StatBucket:**
- StatBucket struct + static ref accessors на PlayerStats
- PlayerStatsRef.cs удалён (методы перенесены в PlayerStats)
- CharacterSaveData: 9 плоских полей → StatBucket[3]
- 7 файлов изменено

**P8 — Equipment multipliers:**
- EquipmentWorld.GetEquipStatBonuses с 6 out-параметрами (flat + mult)
- effective = (StatsToFlat(tier) + flatBonus) * (1.0 + sumMultipliers)

**T-STATS04 — P3/P9 Unify Player/NPC formula:**
- NpcCombatData: flat int (1..30) → tier (0..20) с [FormerlySerializedAs]
- Единая формула StatsToFlat(tier) = tier * 5 + 10

**T-STATS05 — StatsServer config wiring + full playtest guide:**

### 2.11. NPC Unified Behavior Architecture (T-NPC-S00..S21)
**Коммиты:** `17f6abd` — `3a4afb8` (25+ коммитов)  
**Документы:** `docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md`, `05_CODE_REVIEW_FINDINGS.md`

**Phase 0 — Анализ и проектирование (T-NPC-S00):**
- Полный анализ социального поведения NPC
- Проектирование архитектуры: NpcSocialBrain, Patrol, Flee, Grudge

**Phase 1 — Базовая архитектура (S01-S06):**
- NpcSocialBrain — центральный контроллер поведения
- PatrolSystem: waypoint-based патрулирование
- GrudgeSystem: агрессия к обидчикам
- NpcSpawner: применение конфигов ДО Spawn() (OnNetworkSpawn видит NpcSocialBrain)
- Patrol waypoint markers: Transform[] на NpcSpawner вместо ручных координат

**Phase 2 — Эмоции и социальные триггеры (S07-S12):**
- EmotionSystem: эмоциональные состояния NPC
- MoralitySystem: моральные ограничения поведения
- SocialTriggerSystem: реакции на социальные события
- GroupController: групповое поведение
- VocalCueSystem: голосовые реплики NPC

**Phase 3 — Боевые тактики (S13-S18):**
- ThreatAssessment: оценка угроз
- CoverSystem: использование укрытий
- GroupTactics: групповая тактика боя
- Surrender: капитуляция при низком HP
- PostCombat: поведение после боя
- SocialRole: ролевое поведение

**Phase 4 — Фракции и полный idle (S19-S21):**
- FactionSystem: фракционная принадлежность и отношение
- VengeanceMemory: память о врагах между сессиями
- Full Idle Activities: расширенные idle-активности

**Фиксы Phase 3/4 (6 штук):**
- NPC-vs-NPC hostile faction combat
- NpcAttacker.Target = null → IsAlive()=false → урон не проходил
- InvalidSource: _defaultSource=null у NpcAttacker
- MissingReferenceException на destroyed NpcTarget
- Target-lock: не переключать цель посреди боя + post-combat guard пауза

**Код-ревью Unified NPC Behavior:**
- 3 P0, 2 P1, 3 P2 найдено
- Исправления: группы, vengeance, vocal cues, grudge, patrol timeout
- P2 fix: статические реестры AllBrains/AllCoverPoints/AllSitPoints вместо FindObjectsByType
- Фикс HasAnimatorParam: не крашимся если Animator не имеет VocalCue триггеров

### 2.12. NPC Skills (T-NPC-SKILL01..05)
**Коммиты:** `c4e0c24` — `d22ecbc` (4 коммита)  
**Документы:** в ITERATIONS.md и плане навыков

**Phase A — Data Layer:**
- NpcSkillSet — ScriptableObject с набором навыков NPC
- NpcSkillOverride — переопределение отдельных навыков

**Phases B-E — Assignment:**
- Multi-source NpcAttacker (скиллы из NpcSkillSet + экипировки)
- NpcBrain: skill selection с учётом ситуации
- NpcSpawnerConfig: интеграция навыков в конфигурацию спавнера
- Example asset: NpcSkillSet_Goblin

**Оставшиеся задачи (§8):**
- Skill-стейт в аниматоре (deferred)
- HP% фильтр для выбора навыков
- NpcSkillSetEditor
- AOE для NPC-скиллов

### 2.13. NPC Spawn/Loot (T-NPC-11/12)
**Коммиты:** `b16db62` — `3e3bdae` (6 коммитов)

**T-NPC-11 — Spawn Cycle Control:**
- Конечные волны спавна (не только Infinite)
- Перезапуск спавна через ISpawnRestartTrigger
- Фикс: ApplyConfig не перезаписывает _spawnMode/_totalSpawnLimit при значениях по умолчанию

**T-NPC-12 — Loot Config:**
- Визуал дропа и лут-таблица в инспекторе спавнера
- GenerateLoot(): items попадают в NpcLootPickup и доставляются через InventoryServer

### 2.14. NPC AOE / Throwables / Ranged + Editor (T-NPC-AOE01)
**Коммиты:** `3eed335` — `1cc7a6a` (4 коммита)

- NPC AOE, Throwables, Ranged: end-to-end работа
- Debug visualization: визуализация AOE-зон
- NpcSkillSet_Goblin: +Grenade(Throwable) +BasicBow(Ranged) +HeavySwing AOE
- GetEffectiveAttackRange: по скилам вместо фиксированного attackRange
- NpcSkillSetEditor: кастомный инспектор с preview/override-индикацией/effective

### 2.15. Крафтинг: аудит и исправление (T-CRAFT-AUDIT + T-CRAFT01)
**Коммиты:** `86b7641` — `64d3067` (7 коммитов)  
**Документы:** `docs/Crafting_system/AUDIT_2026-07-09.md`, `docs/Crafting_system/ITERATIONS.md`

- Глубокий аудит Crafting System: баги, дыры, техдолги
- Исправлено 5 критических багов (B1-B5) и 7 техдолгов (T1-T7)
- 12/12 пунктов выполнено по плану аудита
- L1: публикация CraftingCompletedEvent в WorldEventBus
- План тестирования задокументирован

### 2.16. Майнинг: аудит и исправление (T-MINING-AUDIT + T-MINE01)
**Коммиты:** `d8c525f` — `398e343` (3 коммита)  
**Документы:** `docs/Mining/AUDIT_2026-07-11.md`, `docs/Mining/AUDIT_2026-07-12_DEEP.md`

- CRITICAL fixes: disconnect handler, WorldEventBus XP path, copy-paste XP Quantity bug
- Phase 2: prefab, Tree.asset, StatsConfig deprecation, документация

### 2.17. Квесты: двойной аудит (T-QAUDIT)
**Коммиты:** `13f3c7f` — `f84a53e` (4 коммита)  
**Документы:** `docs/NPC_quests/DEEP_AUDIT_2026-07-09.md`, `docs/NPC_quests/DEEP_AUDIT_2026-07-13.md`

- Глубокий аудит системы квестов (архитектура, стабы, дублирование, интеграции)
- Повторный комбинированный аудит
- Критическое открытие: квестовые ассеты (FactionDefinition, NpcDefinition, QuestDefinition) утеряны — GUIDs в QuestDatabase висят в никуда
- Задокументирована диагностика и план восстановления

### 2.18. UI/UX Improvements

| Тикет | Коммиты | Что сделано |
|---|---|---|
| **T-UI03** | `d815235` | USS и CS0618 варнинги в CharacterWindow |
| **T-UI04** | `354e3d2` — `c34899a` (5 коммитов) | Переработка блока характеристик: фикс полосок (strength вместо effectiveStrength), цвета per-stat, tier-рамки, горизонтальный layout, цифры 8→11px |
| **T-UI04 Dialog** | `aa2a1ec` — `37492fa` | DialogWindow: текст NPC всегда виден, кнопки квестов прокручиваются; fix 85vh → 520px (vh не поддерживается Unity USS) |
| **T-INV12** | `3a130a5` | Кнопка «БРОСИТЬ» в CharacterWindow → вкладка Инвентарь |
| **T-INP15** | `d3430f9` — `7d55710` | Перенос подбора предметов с E на F с высшим приоритетом |

---

## 3. Ключевые архитектурные решения

| # | Решение | Контекст |
|---|---|---|
| 1 | **Унификация WeaponItemData + ThrowableItemData** | 3 иерархии предметов → 2. Убран хардкод `is TypeCheck` через ICombatDamageProvider |
| 2 | **Registry-based target collection** | AOE использует реестр вместо Physics.OverlapSphere — детерминировано в сетевой игре |
| 3 | **Статические реестры для NPC** | AllBrains/AllCoverPoints/AllSitPoints вместо FindObjectsByType — производительность |
| 4 | **Единая формула статов Player/NPC** | StatsToFlat(tier) = tier * 5 + 10 — унификация двух систем |
| 5 | **StatBucket вместо flat struct** | PlayerStats: 3×3 поля → StatBucket[3] — масштабируемость |
| 6 | **StatsConfig → 3 SO** | Разделение responsibilities: ExperienceConfig, StatSourceMapConfig, StatDebugConfig |
| 7 | **Equipment multipliers в effective stats** | effective = (StatsToFlat(tier) + flatBonus) * (1.0 + sumMultipliers) |
| 8 | **WorldEventBus для крафта** | CraftingCompletedEvent — слабосвязанная интеграция с другими системами |
| 9 | **ISkillVfxProvider абстракция** | 3D ParticleSystem сейчас, 2D sprite animation в будущем — без переписывания сервиса |
| 10 | **NpcSocialBrain** | Центральный контроллер поведения NPC: Patrol → Flee → Combat → Social — унифицированный стейт-менеджмент |

---

## 4. Системы, охваченные работой

```
✅ Ranged Combat       — полный цикл (bow/crossbow/pneumatic/rifle + projectile visual)
✅ Throwables          — grenade/antigrav + throw arc + consumption + AOE
✅ Weapon Refactoring  — унификация иерархии предметов
✅ Skill Tree          — persistence, cooldown, slot bindings, tier requirements
✅ Targeting           — Q/E cycling, outline highlight, obstruction check
✅ Damage Numbers      — World Space TMP, pool, billboard, distance scaling
✅ VFX System          — 3 фазы (data → runtime → prefabs) + NPC унификация
✅ Stats Architecture  — аудит → 5 этапов рефакторинга (P0-P10)
✅ NPC Behavior        — 4 фазы (21 тикет), код-ревью, исправления
✅ NPC Skills          — data layer + assignment + editor
✅ NPC Spawn/Loot      — wave control + loot config
✅ NPC AOE/Ranged      — end-to-end + debug viz + editor
✅ Crafting            — аудит + 12/12 fixes
✅ Mining              — аудит + критичные fixes
✅ Quests              — двойной аудит + диагностика
✅ UI/UX               — CharacterWindow, DialogWindow, Inventory, Input
```

---

## 5. Связанные документы

### Архитектура / Дизайн
| Документ | Система |
|---|---|
| `docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md` | Ranged/Throwables design |
| `docs/Character/Skills/real-time-combat/100_TARGET_HIGHLIGHT_AND_SWITCHING.md` | Targeting system |
| `docs/Character/Skills/real-time-combat/110_DAMAGE_NUMBERS.md` | Damage numbers |
| `docs/Character/Skills/Battle/85_VFX_DESIGN.md` | VFX design |
| `docs/Character/Skills/Battle/100_SKILL_REFACTOR_PLAN.md` | Skill refactoring plan |
| `docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md` | NPC behavior architecture |
| `docs/Character/Skills/real-time-combat/npc-enemy/05_CODE_REVIEW_FINDINGS.md` | NPC code review |

### Аудиты
| Документ | Система |
|---|---|
| `docs/Character/11_STATS_ARCHITECTURE_AUDIT.md` | Stats audit v1 |
| `docs/Character/12_STATS_ARCHITECTURE_AUDIT_V2.md` | Stats audit v2 |
| `docs/Character/13_SESSION_CONTINUATION.md` | Stats session continuation |
| `docs/Character/14_PLAYTESTS_STATS_AUDIT.md` | Stats playtests |
| `docs/Crafting_system/AUDIT_2026-07-09.md` | Crafting audit |
| `docs/Mining/AUDIT_2026-07-11.md` + `AUDIT_2026-07-12_DEEP.md` | Mining audits |
| `docs/NPC_quests/DEEP_AUDIT_2026-07-09.md` + `DEEP_AUDIT_2026-07-13.md` | Quest audits |
| `docs/Character/Skills/AUDIT_2026-07-24_ITEM_WEAPON_REFACTOR.md` | Weapon refactor audit |

### Итерационные логи
| Документ |
|---|
| `docs/dev/ITERATIONS.md` |
| `docs/Character/Skills/ITERATIONS.md` |
| `docs/Character/Skills/real-time-combat/ITERATIONS.md` |
| `docs/Crafting_system/ITERATIONS.md` |
| `docs/NPC_quests/ITERATIONS.md` |

### Сводки / Сводные документы
| Документ |
|---|
| `docs/dev/summary_30.06.2026.md` |
| `docs/dev/summary_01-06_july_2026.md` |
| `docs/dev/summary_05.07.2026.md` |
| `docs/Character/CHANGELOG.md` |
| `docs/Crafting_system/99_CHANGELOG.md` |
| `docs/Mining/99_CHANGELOG.md` |

---

## 6. Общее резюме

За 134 коммита (~24 дня) реализован масштабный пласт боевой системы: от ranged/throwables до NPC AI с фракциями и эмоциями. Проведён сквозной архитектурный аудит статов с поэтапным исправлением 10 проблем (P0-P10). Унифицирована иерархия предметов (3→2 типа). Добавлена VFX-инфраструктура с заделом на 2D. Полностью переработана система поведения NPC (4 фазы, 21 тикет). Проведены аудиты трёх смежных систем (крафт, майнинг, квесты) с исправлением критических багов. UI блока характеристик полностью переработан.

**Ключевой паттерн работы:** каждый эпик сопровождается документированием в ITERATIONS.md и профильных .md-файлах. Аудиты предшествуют исправлениям, фиксы идут короткими итерациями с пост-фикс документацией.
