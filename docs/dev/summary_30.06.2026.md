# Сводка разработки — 30.06.2026
## Проект C: The Clouds — версия 0.0.35
> Период: 23 июня – 30 июня 2026 · 135 коммитов · ~43 900 строк добавлено · 82 .md-файла в docs/Character/

---

## 1. Общая статистика недели

| Метрика | Значение |
|---|---|
| Коммитов | **135** |
| Файлов изменено | 409 |
| Строк добавлено | ~43 921 |
| Строк удалено | ~1 215 |
| Документация Character | 82 .md файла, 32 723 строки |
| Новая кодовая база | `Assets/_Project/Scripts/Combat/` (24+ файла), `Customisation/` (8 файлов), `AI/` (5 файлов) |
| Веха | Docker MVP v0.0.30 → **v0.0.35** (текущая) |
| Прежняя версия | v0.0.29 (NPC Ships round-trip начат) |

---

## 2. Основные направления разработки

### 2.1. Боевой движок (Real-Time Combat) — T-RTC → T-CB

**Дата начала:** 25.06.2026  
**Статус:** ✅ MVP (T-RTC01-09) + Phase 2-4 (T-CB02-07, T-INP01-09)

**Что сделано:**

**Core combat framework (24+ файла):**
- `DamageCalculator.cs` — универсальный калькулятор урона с поддержкой:
  - Физического, магического, антиграва типов урона
  - Hit/Miss/Crit на основе бросков d8
  - Armor reduction с множителями для типов урона
  - Skill multiplier от выученных скилов
  - Cooldown между атаками
- Интерфейсы: `IAttacker`, `IDamageTarget`, `IDamageSource`, `IRangePolicy`
- Имплементации: `PlayerAttacker`, `PlayerTarget`, `NpcAttacker`, `NpcTarget`, `WeaponDamageSource`, `MeleeRangePolicy`, `RangedRangePolicy`
- `CombatServer.cs` — серверный обработчик боя через NGO RPCs
- `CombatClientState.cs` — клиентское состояние боя
- `DamageResultDto.cs` — DTO для передачи результата удара
- `CombatConfig.cs` + 3 ScriptableObject конфига
- `TargetingService.cs` — AOE + Raycast таргетинг (T-RTC10)

**Skill-система:**
- `SkillNodeConfig.cs` — 6 новых полей: AoeFormula (5 значений), isActive, attackAnimationTrigger, aoeSize, aoeConeAngleDeg, aoeWidth + OnValidate миграция
- `CombatDiscipline` enum с авто-установкой по prefix skillId
- `ApplySkillEffects` runtime handler
- `WeaponClassCatalog`, `ArmorClassCatalog`, `WeaponTechniqueCatalog`
- 27+ Skill ScriptableObject по 5 дисциплинам
- `SkillInputService` — хотслот-биндинги с приоритетом, зависимость от оружия (T-INP-09)

**UI Skill Tree:**
- `SkillTreeWindow` — полный граф скилов с zoom/pan, active/passive badges, AOE info
- Колонка slot overview (T-CB UI)
- Кнопка [ИЗУЧИТЬ НАВЫК]

**Skill Animation:**
- `SkillAnimationPlayer.cs` (435 строк) — runtime анимация навыков через AnimatorOverrideController
- `SkillAnimationEventPassthrough.cs` — проброс AnimationEvent в NetworkPlayer
- `SkillAnimationEventTool.cs` (Editor, 345 строк) — редактор событий анимаций
- Набор Combat-анимаций (Male/*.fbx)

### 2.2. Базовая модель персонажа + Анимации

**Дата начала:** 25.06.2026  
**Статус:** ✅ v0.5.1 (Blend Tree directional movement + combat animations)

**Что сделано:**
- `PlayerAnimation.controller` — Blend Tree с directional movement (MoveX, MoveY)
- Combat-анимации: idle/walk/run/attack/death для персонажа и NPC
- `FixPlayerVisual.cs` (Editor) — быстрая настройка визуала
- `SetupPlayerVisual.cs` (Editor, 214 строк) — полная настройка модели на NetworkPlayer.prefab
- Фикс v0.5.1: MoveX=0, MoveY=1 при движении → BlendTree всегда выбирает Forward клип
- Женская модель: `PlayerAnimation_Female.overrideController` (77 строк)
- `SetupFemaleAnimationOverride.cs` (Editor, 121 строка)
- Mining-анимация с топором (T-G09, axe swing)

### 2.3. Customisation (Кастомизация персонажа) — T-CUS

**Дата начала:** 30.06.2026 (документация — 30.06 01:12, код — 30.06 09:51-15:07)  
**Статус:** ✅ L1 + L3 + L4 skin (T-CUS-01..09)

**Документация (7 файлов, ~2700 строк, 145 KB):**
- `00_OVERVIEW.md` — TL;DR + 5 уровней L1..L5 + принципы
- `01_CURRENT_CAPABILITIES.md` — точки расширения в проекте
- `02_DATA_MODEL.md` — CustomisationSave + DTO + ClientState + Applier
- `03_LEVELS_OF_CUSTOMISATION.md` — детально по уровням с трудоёмкостью
- `04_MALE_FEMALE_SWAP.md` — глубокий разбор переключения M↔F
- `05_PHASES_ROADMAP.md` — T-CUS-01..12 в 3 milestone'ах
- `CHANGELOG.md`

**Код (15 файлов, ~500+ строк):**
- Data model: `CharacterBodyType` enum, `BodyPresetId` enum (6 типов), `HairStyleId` enum
- `CustomisationSave.cs` — JsonUtility DTO: bodyType, presetId, heighScale, widthScale, skin/hair colors, clothingColorOverrides
- `CustomisationSnapshotDto.cs` — стриминговая версия
- `CustomisationClientState.cs` — singleton с `OnCustomisationUpdated` event
- `CustomisationWindow.cs` — overlay UI по паттерну SkillTreeWindow
- `CharacterCustomisationApplier.cs` — визуальный апплаер (тело, пресеты, цвета кожи/волос, одежды)
- `SetupCharacterCustomisationApplier.cs` (Editor, 157 строк)

**Баги (исправлены):**
- Bug #1: Стартовый персонаж мелкий и тёмно-серый — domain reload сбрасывал CurrentSnapshot в struct default (heightScale=0). Фикс: OnEnable применяет snapshot с корректным default height/width=1.
- Base model fix: корректные default-значения (T-CUS-09)

### 2.4. Equipment Visual (Визуал экипировки) — T-EV

**Дата начала:** 29.06.2026  
**Статус:** ⏳ Phase 2 done, Phase 3 pending

**Документация (4 файла, 1287 строк):**
- `docs/Character/EquipmentVisual/00_DESIGN.md`, `01_DATA_MODEL.md`, `02_CHARACTER_APPLIER.md`, `03_PHASES.md`

**Код:**
- `CharacterEquipmentVisualApplier.cs` (292 строк) — апплаер визуала оружия/брони
- `EquipSlotToBone.cs` (81 строка) — маппинг слотов экипировки на кости скелета
- `SetupEquipmentVisualAssets.cs` (Editor, 287 строк) — создание ассетов
- **TICKET-EV-002 ЗАКРЫТ**: equip не работал из-за N callback'ов → N `RequestEquipRpc` → rate limit. Фикс: userData-based unregister в InventoryTab.cs
- **TICKET-EV-001** downgraded до косметики (reflection-RPC работает через полный NGO pipeline)
- **TICKET-EV-003** в pending (sword pickup не блокирует Phase 2)

### 2.5. NPC Enemy System — T-NPC

**Дата начала:** 26.06.2026  
**Статус:** ✅ P0 (full FSM + spawner) + P1 (loot) + P2 (visual config + animations v0.1)

**Что сделано:**
- `NpcBrain.cs` (296 строк) — FSM: Idle→Chase→Attack→Dead, NavMeshAgent, server-side Update
- `NpcSpawner.cs` (204 строки) — server-side спавнер: surface validation, rate-limit, leash cleanup
- `NpcSpawnerConfig.cs` (44 строки) — ScriptableObject: radius, max alive, interval, chance
- `Npc_Goblin.prefab` — root: NetworkObject + NetworkTransform + CharacterController + NavMeshAgent + NpcAttacker + NpcTarget + NpcBrain; child: HumanM_Model + Animator
- `NpcAnimatorController.controller` — 5 states (Idle/Walk/Run/Attack/Death), 4 parameters, 9 transitions
- PlaceholderClips (Idle, Walk, Run, Attack, Death) — для замены на Kevin Iglesias FBX
- **T-NPC-14:** Passive / Aggressive / Neutral поведение через NpcSpawnerConfig + NpcBrain
- T-NPC P1: NpcLootPickup, InteractableManager.RegisterNpcLoot, CollectNpcLootServerRpc
- T-NPC P2: Visual Config (anti-restrictive NPC visual)
- NPC animations v0.1: idle-run-walk + blend tree

### 2.6. Input System (Полный ребинд клавиш) — Phase 1-2.5

**Дата начала:** 27.06.2026 (вечер)  
**Статус:** ✅ Phase 2.5 (полный rebind + Esc-меню + save/load)

**Что было (legacy):**
- PlayerInputReader — мёртвый компонент, events не подписаны
- NetworkPlayer.Update — хардкод всех клавиш (Keyboard.current.wKey.isPressed и т.д.)
- SkillInputService — слоты есть, но никто не триггерит (только ЛКМ + K)
- CharacterWindow/SkillTreeWindow/CraftingWindow/DialogWindow — каждое само ловит Esc

**Что сделано:**
- `InputBindingsConfig.cs` — единый ScriptableObject на 31 бинд:
  - Movement: W/A/S/D/Space/Shift
  - Ship: W/S/A/D/E/Q/Shift
  - Interaction: E (interact), F (mode), P (character), T (comm)
  - Skills: Q/E/R/1/2/3/4 (slots 0-6)
  - UI: Esc, I (inventory), M (map), B (build), L (log)
- `InputBindingsRuntime.cs` — runtime управление биндами
- `EscMenu` — полное меню с KeybindingsWindow, RebindPromptWindow
- Персистентность через PlayerPrefs: [СОХР] / [ЗАГР] / [СБРОС]
- 8+ fixed BUG-001: CharacterWindow перехватывал Esc, не давая меню открыться
- Full flow: One-click equip → rate limit fix → stable

### 2.7. NPC Peaceful Ships (Мирные корабли/баржи) — T-NS M3

**Дата начала:** 23.06.2026  
**Статус:** ✅ M3.2.15 (Docking MVP — полный round-trip)

**Этапы:**
- **M2 refactor — movement**: barge/diagonal/course-correction, PD-controller altitude
- **M3.0 → M3.1**: 3 новых файла (NavChecks, NavMode, NavTarget), M2_FSM_DIAGNOSIS.md (456 строк)
- **M3.1 fixes (1-11)**: 11 итераций: от «впервые полная FSM-цепочка» до прямого Rigidbody control
- **M3.2 (rewrite)**: прямой Rigidbody control (crane/manipulator: lift→turn→thrust→turn→lift)
- **M3.2 fixes (1-15)**: 15 итераций — isKinematic, collision detection, rotation reset, schedule advance, pad assignment

**Достижение:** NPC Ships выполняют полный цикл: Docked→Lifting→Yawing→Cruising→Holding→Berthing с возвратом по обратному маршруту.

**Документация:** POSTMORTEM_MOVEMENT_2026-06-23.md (194 строки), FAIL_m2_antigravity_pd.md (75 строк), RETROSPECTIVE (223 строки)

### 2.8. Mining (Добыча ресурсов) — T-G08/T-G09

**Дата:** 29-30.06.2026
- Ресурсные ноды: CopperVein, IronVein, PlantHerb с дополнительными параметрами
- Mining-анимация (Standing Melee Attack Downward) интегрирована в PlayerAnimation.controller
- `ResourceNodeConfig.cs` + editing в BootstrapScene

### 2.9. Docking System — v0.0.30

**Дата:** 25.06.2026  
**Веха:** v0.0.30 (Docking MVP + NPC Ships round-trip)  
**Документация:** summary post + dev plan + GDD + README update, RETROSPECTIVE

---

## 3. Архитектура новых подсистем

### 3.1. Combat System — файловая структура

```
Assets/_Project/Scripts/Combat/
├── Core/
│   ├── DamageResult.cs          — struct результата удара
│   ├── DamageType.cs            — enum: Physical, Magic, Explosive, Antigrav, True
│   ├── IAttacker.cs             — интерфейс атакующего
│   ├── IDamageSource.cs         — интерфейс источника урона
│   ├── IDamageTarget.cs         — интерфейс цели
│   └── IRangePolicy.cs          — интерфейс проверки дистанции
├── Client/
│   └── CombatClientState.cs     — клиентское состояние боя
├── Config/
│   └── CombatConfig.cs          — ScriptableObject конфиг
├── Implementations/
│   ├── DefaultDamageSource.cs   — дефолтный источник урона
│   ├── MeleeRangePolicy.cs      — проверка melee-дистанции
│   ├── NpcAttacker.cs           — атакующий NPC
│   ├── NpcCombatData.cs         — боевые данные NPC (Npc_Goblin.asset)
│   ├── NpcTarget.cs             — цель-NPC (умирает, дропает лут)
│   ├── PlayerAttacker.cs        — атакующий игрок
│   ├── PlayerTarget.cs          — цель-игрок
│   ├── RangedRangePolicy.cs     — проверка ranged-дистанции
│   └── WeaponDamageSource.cs    — урон от оружия
├── Network/
│   ├── CombatServer.cs          — NGO серверный обработчик
│   └── DamageResultDto.cs       — DTO для RPC
└── DamageCalculator.cs          — универсальный калькулятор
```

### 3.2. Customisation — файловая структура

```
Assets/_Project/Scripts/Customisation/
├── CharacterBodyType.cs         — enum Male/Female
├── BodyPresetId.cs              — enum Default/Athletic/Heavy/Slim/Elder/Young
├── HairStyleId.cs               — enum Bald/Short
├── CustomisationSave.cs         — [Serializable] JsonUtility DTO
├── CustomisationClientState.cs  — MonoBehaviour singleton
├── Dto/
│   └── CustomisationSnapshotDto.cs — struct + ClothingColorOverrideDto
└── UI/
    └── CustomisationWindow.cs   — full-screen overlay
```

### 3.3. NPC Enemy — файловая структура

```
Assets/_Project/Scripts/AI/
├── NpcBrain.cs                  — FSM (Idle→Chase→Attack→Dead)
├── NpcSpawner.cs                — server-side спавнер
├── NpcSpawnerConfig.cs          — ScriptableObject конфиг
└── NpcLootPickup.cs             — лут с NPC
```

---

## 4. Статистика по тикетам

| Тикет | Название | Статус | Файлов | Строк |
|---|---|---|---|---|
| T-RTC01-09 | Combat MVP | ✅ | 24+ | ~2000 |
| T-CB02 | CombatDiscipline | ✅ | 3 | ~50 |
| T-CB03 | WeaponItemData | ✅ | 7 | ~430 |
| T-CB05 | WeaponClassCatalog | ✅ | 3 | ~150 |
| T-CB06 | Armor defense | ✅ | 2 | ~40 |
| T-CB07 | ApplySkillEffects | ✅ | 3 | ~100 |
| T-CB08 | 31 Skill .assets | ✅ | 31 | batch |
| T-CB UI | SkillTreeWindow | ✅ | 5 | ~300 |
| T-INP-02 | Active/Passive + AOE | ✅ | 1+27 SO | ~50+ |
| T-INP-03 | AOE execution | ✅ | 3 | ~415 |
| T-INP-04 | Animation hooks | ✅ | в T-INP-03 | ~100 |
| T-INP-05 | UI badges + AOE info | ✅ | 2 | ~90 |
| T-INP-06 | AOE debug viz | ✅ | 3 | ~440 |
| T-INP-07 | Slot priority | ✅ | 2 | ~50 |
| T-INP-08 | Skill Animation | ✅ | 6 | ~700 |
| T-INP-09 | Weapon dependence | ✅ | 4 | ~100 |
| T-NPC-01..14 | NPC Enemy system | ✅ P0-P2 | 8 | ~800 |
| T-EV-02 | Equipment Visual Phase 2 | ✅ | 7 | ~980 |
| T-G08 | ResourceNodes | ✅ | 4 | ~315 |
| T-G09 | Mining animation | ✅ | 6 | ~524 |
| T-CUS-01..09 | Customisation L1+L3+L4 | ✅ | 15 | ~850 |
| T-NS M3.1..3.2 | NPC Ships docking | ✅ | 10 | ~2000 |

---

## 5. Технические достижения

### 5.1. Формула урона (DamageCalculator)
```
baseAttack  = d8-бросок + weaponAttack
defense     = sum(armorDefense всех слотов)
typeMult    = DamageType-множитель (Antigrav ×0.5 к броне)
skillMult   = WeaponDamageSource.GetSkillMultiplier() — чтение выученных скилов
finalDamage = Mathf.Max(1, baseAttack * skillMult - defense * typeMult)
hit/miss/crit: d8 бросок → 1=miss, 8=crit (×1.5)
```

### 5.2. AOE система (T-INP-02/03)
- 5 типов: SingleTarget, Sphere, Cone, Box, Line
- Size/angle для каждого типа
- Работает через TargetingService с физическим Overlap-ом
- Спектральная визуализация через SkillAoeDebugVisualizer

### 5.3. Skill Animation Pipeline (T-INP-08)
```
SkillInputService.SlotPressed()
  → проверка isActive, cooldown, weapon equip
  → SkillAnimationPlayer.PlaySkill(skillId)
    → AnimatorOverrideController[skillAnimationTrigger] = overrideClip
    → NetworkPlayer.Animator.SetTrigger(skillAnimationTrigger)
    → AnimationEvent → SkillAnimationEventPassthrough
      → CombatServer.RequestExecuteSkill(skillId)
```

### 5.4. Customisation Pipeline (T-CUS)
```
CustomisationWindow (UI)
  → CustomisationSave (DTO)
  → CustomisationClientState.ApplyCustomisationSnapshot()
  → CharacterCustomisationApplier.Apply(bodyType, presetId, colors...)
    → SkinnedMeshRenderer sharedMesh по типу тела
    → MaterialPropertyBlock._Color для кожи/волос/одежды
    → Animator.runtimeAnimatorController = female override (если Female)
    → _visualRoot.localScale = (heightScale, heightScale, widthScale)
```

### 5.5. NPC Ships Docking FSM — финальная цепочка
```
Docked → Lifting (взлёт, velocity reset)
  → Yawing (разворот к направлению, killVerticalVelocity)
    → Cruising (полёт по маршруту, PD-controller)
      → Holding (ожидание в зоне станции)
        → Berthing (посадка к назначенному паду)
          → Docked (DwellTime=5s, isKinematic)
            → schedule advance → Lifting (обратный маршрут)
```

---

## 6. Open Issues / Known Blocker'ы

| # | Тикет | Проблема | Статус |
|---|---|---|---|
| 1 | T-CB06/07/08 | Play Mode тест требует NPC-AI подсистемы | known blocker |
| 2 | T-EV-001 | Reflection-RPC нестабилен (косметика) | downgraded |
| 3 | T-EV-003 | Sword pickup визуал | pending |
| 4 | T-CUS L2 | Hair, Face, Accessories | следующий этап |
| 5 | T-CUS L5 | Inventory save/load snapshot | дальняя перспектива |
| 6 | Equipment Visual Phase 3 | Hands/Shoulder slots | pending |

---

## 7. Краткое резюме по неделям

```
23.06.2026 — NPC Ships M2 refactor, движение барж
24.06.2026 — NPC Ships M3.1-3.2: полный rewrite, 15+ фиксов, docking FSM
25.06.2026 — v0.0.30: Docking MVP done. Боевой движок: план, T-RTC01-09 (24 файла)
              Базовая модель + Blend Tree анимации
26.06.2026 — T-CB: Weapon/armor/skills. T-NPC: P0-P2 (FSM, spawner, prefab)
              NPC animations v0.1. BlendTree v0.5
27.06.2026 — SkillTreeWindow UI (graph/zoom/pan). Input System Phase 1-2.5
              EscMenu, key rebinding, save/load
28.06.2026 — T-CB Phase 2-4: AOE, combat discipline, animation hooks
              Pass 1-3.5: T-INP-02..05 merged
              Skill Animation Player T-INP-08
29.06.2026 — Equipment Visual Phase 2 (T-EV). T-NPC-14 (passive/aggressive)
              Mining T-G08/09. Skill Animation финальный
30.06.2026 — Customisation L1+L3+L4: полный цикл (доки → код → UI → багфиксы)
              Текущая версия: v0.0.35
```

---

## 8. Ключевые документы (references)

| Документ | Описание | Расположение |
|---|---|---|
| COMBAT_ENGINE_IMPL_PLAN.md | План имплементации боевого движка | `docs/dev/` |
| SKILLS_ROADMAP_T-CB.md | Roadmap скиллов | `docs/dev/` |
| INP08_ANIMATOR_CLIP_PIPELINE.md | Skill Animation Pipeline | `docs/dev/` |
| INP06_AOE_DEBUG_VISUALIZATION.md | AOE визуализация | `docs/dev/` |
| SKILLS_NEXT_STEPS_T-CB_LOG.md | Лог шагов T-CB | `docs/dev/` |
| EquipmentVisual_BUGS_TICKETS.md | Баги Equipment Visual | `docs/dev/` |
| 2026-06-26-combat-animations-design.md | Дизайн боевых анимаций | `docs/dev/` |
| 2026-06-26-npc-p2-visual-config.md | NPC Visual Config | `docs/dev/` |
| 2026-06-23-npc-ship-movement-refactor.md | NPC Ships M2 refactor | `docs/dev/` |
| POSTMORTEM_MOVEMENT_2026-06-23.md | Постмортем NPC Ships | `docs/NPC_others_peacfull/pc_ship/` |
| M2_FSM_DIAGNOSIS.md | Диагностика FSM NPC Ships | `docs/NPC_others_peacfull/pc_ship/` |
| Customisation docs (7 файлов) | Полная документация customisation | `docs/Character/Customisation/` |
| Battle docs (12 файлов) | Боевая система — анализ, дизайн, имплементация | `docs/Character/Skills/Battle/` |
| Real-time combat docs (10 файлов) | РТК — анализ, дизайн, сценарии, питфоллы | `docs/Character/Skills/real-time-combat/` |
| Equipment Visual docs (4 файла) | Визуал экипировки | `docs/Character/EquipmentVisual/` |
| Input System docs (7 файлов) | Input migration, bindings, keybind | `docs/Character/input-system/` |

---

*Составлено: 30.06.2026 15:07 UTC+5*
*Следующая сводка: после Character Customization L5 / Equipment Visual Phase 3 / NPC Combat integration*
