# Audit — Current State & Next Steps (Skills/Battle)

> **Дата:** 2026-06-26 (сессия ревью после `docs/Character/Skills/Battle/` v0.3, до внесения изменений)
> **Подсистема:** Character Progression → Skill Tree → Combat (continuation of `Battle/` v0.3)
> **Связанные документы:**
> - `docs/Character/Skills/Battle/00_README.md` — манифест Battle-навыков (v0.3, проектирование)
> - `docs/Character/Skills/Battle/10_DESIGN.md` — целевой дизайн (T-CB01..T-CB09)
> - `docs/Character/Skills/Battle/20_SKILL_TREES.md` — целевые 5 дисциплин
> - `docs/Character/Skills/Battle/30_PITFALLS_AND_OPEN_QUESTIONS.md` — открытые вопросы дизайна
> - `docs/Character/06_SKILL_TREE.md` — базовый skill tree (T-P11..T-P14, реализован)
> - `docs/Character/Skills/real-time-combat/` — Real-Time Combat Engine (реализован)
> **Статус:** 🔵 Аудит (read-only). Никаких правок кода в этой сессии.
> **Цель документа:** зафиксировать фактическое состояние подсистемы (что есть в коде, чего нет) + открытые вопросы, которые нужно решить в **разных сессиях** по одному за раз. Документ — входная точка для будущих T-CB* тикетов.

---

## TL;DR

Реальное состояние кода **значительно отличается** от того, что заявлено в `Battle/10_DESIGN.md` (v0.3, 2026-06-25):

- **Реализовано:** Real-Time Combat Engine целиком (`CombatServer`, `PlayerAttacker/Target`, `NpcAttacker/Target`, `WeaponDamageSource`, `DamageCalculator`, range policies), `WeaponItemData` с ERPR-полями, 30 skill .asset по 5 дисциплинам + social, CharacterWindow с 3-state ListView (LEARNED/AVAILABLE/LOCKED).
- **НЕ реализовано (расхождение с дизайном v0.3):** `SkillEffect.Type` НЕ расширен (нет 5 новых типов `WeaponProficiencyUnlock` и т.д.), `SkillNodeConfig.CombatDiscipline` НЕТ, `SkillsServer.ApplySkillEffects` — no-op, `EquipmentServer.TryEquip` НЕ принимает `WeaponItemData`, `ExplosiveItemData` НЕТ, `WeaponClassCatalog/ArmorClassCatalog` НЕТ, `ClothingItemData.armorDefense` НЕ подтверждено, CharacterWindow НЕ фильтрует по дисциплинам.
- **Debug-временный код:** K-attack живёт хардкодом в `NetworkPlayer.Update:576-584` → `DebugAttackNearestNpc()`. Помечен "ВРЕМЕННЫЙ код — только для verify CombatEngine в Play Mode. Удалить в Phase 2".
- **Input layer для навыков отсутствует целиком:** `PlayerInputReader` имеет только WASD/Shift/Space/E/F/T/mouse-delta events. Mouse 0/1, Q/E/R, 1-4 свободны, но не подключены ни к чему.

**Ключевой принцип следующих сессий:** "не ломать, доделывать" — Combat Engine и skills pipeline работают, достраиваем каркас (SkillInputService, input layer, расширения SkillEffect/SkillNodeConfig) маленькими тикетами, каждый проверяемый в Play Mode отдельно.

---

## 1. Фактическое состояние кода (vs Battle/10_DESIGN.md)

### 1.1 Что реально работает

| Компонент | Файлы | Проверено |
|---|---|---|
| **Combat Engine (real-time, server-authoritative)** | `Assets/_Project/Scripts/Combat/Network/CombatServer.cs` + `Core/{DamageType,DamageDice,DamageResult,IAttacker,IDamageSource,IDamageTarget,IRangePolicy}.cs` + `Implementations/{PlayerAttacker,PlayerTarget,NpcAttacker,NpcTarget,WeaponDamageSource,DefaultDamageSource,MeleeRangePolicy,RangedRangePolicy}.cs` + `Network/DamageResultDto.cs` + `Client/CombatClientState.cs` + `Config/CombatConfig.cs` | ✅ `OnNetworkSpawn` + push-down регистрация + second-chance recovery через 1 сек (T-RTC06 v0.1.2), ERPR-формула в DamageCalculator, cooldown централизован в CombatServer (per answer 2.3) |
| **WeaponItemData с ERPR-полями** | `Assets/_Project/Scripts/Equipment/WeaponItemData.cs` | ✅ поля: `weaponClass`, `damageDice` (d4..d20), `baseDamage`, `critModifier`, `range`, `damageType` (Physical/Ballistic/Antigrav/Explosive/Mesium), `requiredProficiency`, `minTier`. `OnValidate` auto-set defaults по `weaponClass` (T-CB03 done). `ProjectC.Combat.Core.DamageDice` enum живёт в Combat/Core, не в Equipment. |
| **Skills system (M3: T-P11..T-P14)** | `Assets/_Project/Scripts/Skills/{SkillNodeConfig,SkillEffect,SkillsConfig,SkillsWorld,SkillsServer,SkillsClientState}.cs` + `Dto/SkillsDto.cs` | ✅ `RequestLearnSkillRpc`/`RequestForgetSkillRpc` через NGO 2.x Rpc с reflection-stub pattern для ReceiveSkillResultTargetRpc/ReceiveSkillsSnapshotTargetRpc (см. lessons в `project-c-character-progression-tickets` SKILL). 5-step `TryLearnSkill` + Q3.4 `TryForgetSkill` (free respec, no XP refund). |
| **30 skill .asset по 5 веткам** | `Assets/_Project/Resources/Skills/Skill_*.asset` (4 combat placeholder + 22 дисциплинарных + 4 social) | ✅ Реальные .asset есть: Melee (BasicSword, HeavySwing, PrecisionStrike, DaggerMastery, SpearReach, DualWield), Ranged (BasicBow, CrossbowMastery, QuickReload), Explosives (BasicBomb, Grenade, Mine), Antigrav (BasicPulse, Aura, Shield), Defense (BasicArmor, HeavyArmor, Master, AntigravShield), Combat (BasicStrike, DodgeRoll, HeavySwing, PrecisionStrike), Social (BasicTalk, Barter, Persuasion, Leadership). **ВСЕ используют только `type: 0` (StatMod)** с эффектами вида "+STR 0×0.2" или "+DEX 0×0.15". Связь с оружием/бронированием НЕ реализована. |
| **CharacterWindow 3-state skill rows** | `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (T-P14) | ✅ SkillRow struct + 2 List<SkillRow> caches (`_skillsCombatCache`, `_skillsSocialCache`) + 2 ListView. 3-state pattern: `LEARNED` (✅ green) / `AVAILABLE` (○ yellow) / `LOCKED` (✕ red). USS classes `.skill-row-learned/available/locked`. `MakeSkillRow` + `BindSkillRow` + `SubscribeSkills/UnsubscribeSkills` (lazy pattern). |
| **Player Animator Controller** | `Assets/_Project/Animations/PlayerAnimation.controller` + `PlayerAnimation_Default.overrideController` | ⚠️ Контроллер есть, используется на NetworkPlayer.prefab. Trigger `"Attack"` объявлен (см. `NetworkPlayer.cs:583` — `_animator.SetTrigger("Attack")`). Реальный `.anim` clip — `Assets/_Project/Animation/AI/PlaceholderClips/Attack.anim` (placeholder). Полноценные Attack-циклы (swing/projectile/parry) — **отсутствуют**, используется один placeholder для всех weapon types. |
| **NPC Animator Controller** | `Assets/_Project/Animation/AI/NpcAnimatorController.controller` | ✅ Полноценный с BlendTree для directional locomotion + T-NPC-13 параметры MoveX/MoveY. |

### 1.2 Что НЕ реализовано (расхождение с Battle/10_DESIGN.md v0.3)

| Что | Где должно быть | Где реально | Статус |
|---|---|---|---|
| `SkillEffect.Type.WeaponProficiencyUnlock` (3) | `Assets/_Project/Scripts/Skills/SkillEffect.cs` | enum: StatMod=0, AbilityUnlock=1, PassiveEffect=2 — **и всё** | ❌ **T-CB01 не сделан** |
| `SkillEffect.Type.ArmorProficiencyUnlock` (4) | там же | нет | ❌ |
| `SkillEffect.Type.WeaponTechniqueUnlock` (5) | там же | нет | ❌ |
| `SkillEffect.Type.ExplosiveRecipeUnlock` (6) | там же | нет | ❌ |
| `SkillEffect.Type.AntigravTechniqueUnlock` (7) | там же | нет | ❌ |
| `SkillNodeConfig.CombatDiscipline` enum | `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` | нет поля | ❌ **T-CB02 не сделан** — фильтр в UI невозможен |
| `SkillsServer.ApplySkillEffects` обработчик новых типов | `Assets/_Project/Scripts/Skills/SkillsServer.cs` | **no-op** (тело метода отсутствует, см. `SkillsServer.cs:121` — TryLearnSkill не вызывает эффекты; StatMod читается через `SkillsSnapshotDto` в `StatsServer.RecomputeAndSendSnapshot`) | ❌ **T-CB07 не сделан** |
| `EquipmentServer.TryEquip` принимает `WeaponItemData` + proficiency gate | `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` | нужно проверить — код ниже не читал, но `PlayerAttacker.RebuildSources` уже работает с `WeaponItemData` (см. `PlayerAttacker.cs:108-115`), значит weapon экипируется; **proficiency gate не вызывается** | ⚠️ **T-CB06 не сделан или частично** |
| `ClothingItemData.armorDefense` поле | `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` | нужно проверить | ⚠️ не подтверждено |
| `ExplosiveItemData` SO (T-CB04) | `Assets/_Project/Scripts/Equipment/` | нет файла | ❌ |
| `WeaponClassCatalog/ArmorClassCatalog` lookup SO (T-CB05) | `Assets/_Project/Scripts/Equipment/` | нет файла | ❌ |
| `CharacterWindow` фильтр по CombatDiscipline (T-CB09) | `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | фильтра нет; skills отсортированы только по Combat/Social category | ❌ |
| Targeting через raycast/UI | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | hardcoded "найти ближайший NpcTarget в 15м" в `DebugAttackNearestNpc()` | ❌ (debug-stub, см. §1.3) |
| Skill activation по клавишам (skill slots 1-4, ЛКМ, ПКМ) | input layer | **отсутствует целиком** | ❌ |
| SkillInputService (каркас "нажата кнопка → выполнить навык") | новый файл | нет | ❌ |

### 1.3 Debug-временный код, который надо выпиливать

**Файл:** `Assets/_Project/Scripts/Player/NetworkPlayer.cs:574-584`

```csharp
// T-RTC06 (DEBUG): K — debug-attack nearest NPC. ВРЕМЕННЫЙ (только для verify).
// Найти ближайший NpcTarget в радиусе 5м → RequestAttackRpc.
if (Keyboard.current.kKey.wasPressedThisFrame
    && NetworkManager.Singleton != null
    && IsSpawned
    && CombatServer.Instance != null)
{
    DebugAttackNearestNpc();
    // T-NPC-12: Attack animation trigger (пока на K, потом на реальную кнопку).
    if (_animator != null) _animator.SetTrigger("Attack");
}
```

**Метод:** `NetworkPlayer.cs:288-315` — `DebugAttackNearestNpc()`, помечен комментарием "ВРЕМЕННЫЙ код — только для verify CombatEngine в Play Mode. Удалить в Phase 2 (когда будет нормальный targeting через raycast + UI)."

**Содержит:**
- Жёсткий поиск ближайшего `NpcTarget` в радиусе 15м (T-CB03: увеличено с 5м).
- Жёсткий `sourceId = 0UL` (Unarmed fallback).
- Прямой вызов `CombatServer.Instance.RequestAttackRpc(targetId, 0UL)`.
- Локальный `_animator.SetTrigger("Attack")` — НЕ через Skill-каркас.

**Также:** в `NetworkPlayer.cs:622-637` есть anim.SetBool("InCombat", true) если экипирован `WeaponMain` — это условный visual для Animator (BlendTree combat idle vs unarmed idle).

### 1.4 Input layer (что есть / чего нет)

**`Assets/_Project/Scripts/Player/PlayerInputReader.cs`:**

| Input | Source | Status |
|---|---|---|
| WASD | `Keyboard.current.{w,s,a,d}Key.isPressed` | ✅ legacy direct, не Input Actions |
| Shift (run) | `Keyboard.current.{left,right}ShiftKey.isPressed` | ✅ legacy direct |
| Space (jump) | `Keyboard.current.spaceKey.wasPressedThisFrame` | ✅ legacy direct |
| E (interact) | `Keyboard.current.eKey.wasPressedThisFrame` | ✅ legacy direct |
| F (mode switch) | `Keyboard.current.fKey.wasPressedThisFrame` | ✅ legacy direct |
| T (comm panel) | `Keyboard.current.tKey.wasPressedThisFrame` | ✅ legacy direct |
| Mouse delta | `Mouse.current.delta.ReadValue()` | ✅ legacy direct |
| **Mouse 0 (ЛКМ, primary attack)** | — | ❌ не подключен нигде |
| **Mouse 1 (ПКМ, secondary)** | — | ❌ |
| **Q** | — | ❌ |
| **R** | — | ❌ |
| **1, 2, 3, 4 (skill slots)** | — | ❌ |
| **Tab (lock target)** | — | ❌ |
| **X (sheathe weapon)** | — | ❌ |

Все клавиши читаются через **legacy `Keyboard.current.*.isPressed`** (не через Unity Input Actions). Это нарушает правила проекта ("Не вызывать `Input.GetAxis()` / `Keyboard.current.*.isPressed` напрямую из gameplay кода — use `PlayerInputReader` or new `InputActionReference`"). **Однако PlayerInputReader сам использует legacy** — то есть legacy в legacy.

**В NetworkPlayer.Update:** F, T, P, K читаются напрямую (parallel к PlayerInputReader), не через event'ы. F/T/P/K обрабатываются **в NetworkPlayer**, не в PlayerInputReader. Это конфликт архитектуры, но работает.

---

## 2. Ответы на 3 ключевых вопроса (для будущих сессий)

### 2.1 "Как переделать клавишу с K на ЛКМ, не сломав анимации/циклы?"

**K-attack сейчас размазан по 2 точкам и напрямую зависит от Animator trigger:**

```csharp
// NetworkPlayer.cs:576-584
if (Keyboard.current.kKey.wasPressedThisFrame && ...) {
    DebugAttackNearestNpc();                 // 1) Поиск цели → RPC
    if (_animator != null) _animator.SetTrigger("Attack");  // 2) Локальная анимация
}
```

Animator Controller `PlayerAnimation.controller` уже имеет **trigger `"Attack"`**. Сейчас он "один на все оружия" — для MVP это ок, в Phase 2 добавим per-skill/per-weapon triggers (`AttackSword`, `AttackBow` и т.д.). **Сама архитектура Animator независима от клавиш** — это правильно: контроллер знает только триггеры.

**Минимально-инвазивная схема (без поломки):**

```
              ┌──────────────────────────────────────┐
   input ──►  │ PlayerInputReader (или прямой input) │
              └────────────┬─────────────────────────┘
                           │ OnAttackPressed event
                           ▼
              ┌──────────────────────────────────────┐
              │ NetworkPlayer.HandleAttackInput()    │  ← ЕДИНСТВЕННАЯ точка
              │  - if (Time-elapsed < attackCD) ret  │    "что значит атака"
              │  - FindTarget (raycast/lock/UI/null) │
              │  - PlayLocalAttackAnim()             │
              │  - serverRpc.TryUseSkill(id, tgt)    │
              └──────────────────────────────────────┘
```

**Принцип:** всю логику "нажали кнопку = запустили RPC + триггернули анимацию" выносим в **один метод-хэндлер** (например, `NetworkPlayer.HandleAttackInput(SkillId, AttackSource)`). Все события ввода (Keyboard.K, Mouse.Left, Gamepad.RT) подвешиваем к одному event'у `OnAttackPressed`. Тогда:
- Animator Controller **не меняется** — триггер `Attack` остаётся, мы его просто вызываем из handler'а.
- Чтобы переключить K→ЛКМ, перенаправляем **один event** в `PlayerInputReader` или `NetworkPlayer.Update`.
- При добавлении Q/R/1-4 как skill-slots — handler расширяется параметром `SkillInputSlot`.

**Разделение ответственности (на будущее):**
- **Animator Controller** = "что показываем". Универсальные триггеры: `Attack`, `Block`, `Cast`, `Jump`. Никакой привязки к клавишам.
- **SkillNodeConfig SO** = "что умеем". Поля: `attackAnimationTrigger` (string) + `inputSlot` (enum).
- **SkillInputService** = "что нажали → что триггерим → что отправляем на сервер". Одна точка для всех навыков.

### 2.2 "На какие клавиши назначать навыки?"

**Сначала — что уже занято (из `NetworkPlayer.Update` + `PlayerInputReader`):**

| Клавиша | Действие | Приоритет |
|---|---|---|
| WASD | движение (пешее) | 🔴 святое |
| W/S/A/D/Q/E/Shift | движение (корабль) | 🔴 святое |
| Space | прыжок | 🔴 святое |
| Shift | бег | 🔴 святое |
| E | interact (pickup/chest/door/gather/door-toggle через F) | 🔴 святое (НЕ менять) |
| F | mode switch (board/exit ship) + gather + crafting + door | 🔴 святое (НЕ менять) |
| T | comm panel (только когда пилотирует корабль) | 🟡 свободна на суше |
| P | character window | 🔴 святое (НЕ менять) |
| **K** | **debug-attack nearest NPC** (временный) | 🟡 **выпилить → перевести в skill slot** |
| **ЛКМ (Mouse 0)** | свободна | 🟢 **главный кандидат на primary attack** |
| **ПКМ (Mouse 1)** | свободна | 🟢 secondary / aim / parry |
| **Q / R / 1-4** | свободны | 🟢 **skill slots (после рефакторинга)** |
| **X** | свободна | 🟡 sheathe weapon |
| **Tab** | inventory wheel (legacy stub, не реализован v2) | 🟢 lock-target |

**Рекомендуемая раскладка для Project C (пехота, MVP+1):**

| Slot | Клавиша | Действие | Skill (пример) |
|---|---|---|---|
| **Primary attack** | **ЛКМ (Mouse 0)** | Базовая атака экипированным оружием | берёт `WeaponMain.damageDice` + проверяет proficiency |
| **Secondary** | **ПКМ (Mouse 1)** | Блок / парирование / прицеливание | `melee_parry` (если learned) |
| **Skill 1** | **1** | Skill из slot 1 (favorite или последний learned) | первое из дерева |
| **Skill 2** | **2** | Skill из slot 2 | второе |
| **Skill 3** | **3** | Skill из slot 3 | третье |
| **Skill 4** | **4** | Skill из slot 4 | четвёртое |
| **Sheathe weapon** | **X** | Убрать оружие (animator InCombat=false) | — |
| **Lock target** | **Tab** | Lock на ближайшем NPC (для ranged/aim) | — |
| **Heavy attack modifier** | **Shift+ЛКМ** | Сильная атака (если learned HeavySwing/PrecisionStrike) | — |

**Где хранить биндинги (рекомендация):**
- **Не хардкод** в `NetworkPlayer.Update` (как сейчас с K).
- В **ScriptableObject `InputBindingsConfig`** (per-character или глобально, как решите), читается `PlayerInputReader`'ом.
- `PlayerInputReader` эмитит `OnPrimaryAttackPressed`, `OnSecondaryPressed`, `OnSkill1Pressed`..`OnSkill4Pressed`, `OnLockTargetPressed`, `OnSheatheWeaponPressed`.
- `SkillNodeConfig` получает поле `inputSlot` (enum `SkillInputSlot { None, Primary, Secondary, Slot1, Slot2, Slot3, Slot4 }`), по которому UI привязывает иконку скилла к клавише.

**Альтернативы раскладки (открыто для решения, см. O-1):**
- **MOBA-style:** Q/W/E/R для 4 skill slots (под большой палец). ЛКМ — primary attack. 1-4 не используются.
- **WASD-mouse:** ЛКМ attack, Shift+ЛКМ heavy, 1-4 для skill slots, Q/E для secondary/movement abilities.
- **Минималистичная (текущая, но debug):** только K = attack. Не масштабируется.

### 2.3 "Как учить навыки? Где увидеть дерево? Какой UI?"

**УЧИТЬ — уже работает.** Через `SkillsServer.RequestLearnSkillRpc` (NGO 2.x, owner-only, rate limit 5 ops/sec). Серверная сторона: `_world.TryLearnSkill(clientId, skillId, ...)` — 5-step validation (skill exists / not learned / prereqs met / INT tier sufficient / XP cost via `StatsServer.ApplyXpDirect`). Клиент получает `ReceiveSkillResultTargetRpc` (reflection-stub) и обновляет `SkillsClientState`. `CharacterWindow` подписан на `OnSkillsUpdated` → пересчитывает строки.

**В CharacterWindow (`Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`, T-P14) уже есть:**
- 3-state SkillRow (LEARNED ✅ / AVAILABLE ○ / LOCKED ✕).
- 2 ListView: combat и social.
- USS classes `.skill-row-learned/available/locked` (green/yellow/red tint).

**Чего НЕ хватает в UI (полировка):**

| Что | Где | Нужно |
|---|---|---|
| **Кнопка "Изучить"** навыка | в строке ListView | сейчас нет — добавить `VisualElement + ClickEvent` (см. AGENTS § про `SkillRow` из `project-c-character-progression-tickets` SKILL), шлёт `RequestLearnSkillRpc(skillId)`. Сервер вернёт `SkillResultDto` через `ReceiveSkillResultTargetRpc` → тост/звук |
| **Кнопка "Забыть"** | на LEARNED строках | Q3.4: free respec, шлёт `RequestForgetSkillRpc(skillId)` |
| **Стоимость в XP** | в строке | `LearnXpCost` поле уже есть в SO, просто отобразить (например: "XP: 100 / 250") |
| **Описание эффектов** | tooltip / expansion | сейчас только `displayName` + `description`. Нужно рендерить `effects[]` (StatMod +X, ×Y и т.п.). Например: "+5 STR (additive), ×1.2 STR (multiplicative)" |
| **Дерево (Painter2D / listview граф)** | **отсутствует** | T-P19 запланирован, **не сделан**. Альтернатива для MVP — **табы по дисциплинам** (см. ниже) |
| **Drag-to-slot** для Skill1-4 | отсутствует | после рефакторинга InputBindings — добавить drag-and-drop (skill row → slot bar) |
| **Фильтр по CombatDiscipline** | отсутствует | **нужен T-CB02** (поле в SkillNodeConfig) |

**Про "увидеть дерево" — самое важное.**

Сейчас в CharacterWindow все 30 навыков показаны в одном списке. Это плохо: 22 боевых ноды вперемешку с 4 social. Нужен **минимум один уровень группировки**. Самый быстрый вариант без Painter2D:

**Схема UI "Combat → 5 дисциплин → ноды" (3-уровневая без графа):**

```
[P — CharacterWindow]
└── [SUB-TAB: НАВЫКИ]                         (уже есть)
    ├── Фильтр-чип: [ВСЕ] [⚔ Melee] [🏹 Ranged] [💣 Explosives] [🌌 Antigrav] [🛡 Defense] [💬 Social]
    ├── Skill slot bar (после рефакторинга биндингов):
    │     [ЛКМ] [ПКМ] [1] [2] [3] [4]        ← пустые ячейки, drag сюда из списка
    └── Skill list (ListView, отфильтрованный по чипу):
          ☐ ⚔ melee_basic_sword    [STR+1]   [Изучить]
          ☑ ⚔ melee_heavy_swing    [STR+5×1.2] [Забыть]
          ☐ ⚔ melee_basic_dagger    [DEX+2]   [Изучить]
          ...
```

**Реализация (после T-CB02 добавит `CombatDiscipline` поле):**
1. Добавить в UXML horizontal-чипы фильтра (одна строка USS).
2. В `CharacterWindow.cs`: при клике на чип → `RebuildSkillsListView(discipline)`. По умолчанию `CombatDiscipline.None` = "Все".
3. Каждый skill row имеет discipline-tag-class (`.skill-row-melee`, `.skill-row-ranged` и т.п.) — USS подкрашивает.
4. Без T-CB02 — fallback на substring match по `skillId` (`melee_*`, `ranged_*` и т.п.). Работает, но хрупко.

**Позже (Phase 2)** — заменить ListView на Painter2D граф с линиями prereq. Приятно глазу, но не блокирует геймплей. См. T-P19 в roadmap.

---

## 3. Открытые вопросы (по сессиям)

Каждый вопрос = отдельная сессия для решения. Не пытаемся закрыть все за раз.

### O-1. Утвердить раскладку клавиш для навыков

**Контекст:** §2.2 — три альтернативы (MOBA-style QWER / WASD-mouse 1-4 / минималистичная K-only).

**Варианты:**

| Variant | Primary | Secondary | Skill slots | Pros | Cons |
|---|---|---|---|---|---|
| **A. WASD-mouse** | ЛКМ | ПКМ | 1, 2, 3, 4 | Стандарт для RPG/MMO; легко запомнить; мышь свободна | 1-4 далеко от WASD; тяжело нажимать в движении |
| **B. MOBA-style** | ЛКМ | ПКМ | Q, W, E, R | Под большой палец; быстро; scale до 4+ skill slots | Q/E заняты в Ship-режиме (need to remap); less RPG-feel |
| **C. Гибрид** | ЛКМ | ПКМ | Q, E, R, F | F = mode switch (боевой режим сам отдаёт skill slot); 3 видимых + 1 скрытый | Конфликт F-свич; сложно документировать |

**Зависит от:** общей раскладки (что уже занято), фидбэка от playtest.

**Что нужно от user:** выбор варианта A/B/C или своя комбинация.

**Тикет для реализации:** отдельная сессия после O-1. Включает: `InputBindingsConfig` SO, расширение `PlayerInputReader` (новые events), drag-and-drop в CharacterWindow (skill → slot bar).

---

### O-2. Добавить `CombatDiscipline` поле в `SkillNodeConfig` (T-CB02)

**Контекст:** §1.2 — поле отсутствует, фильтр в CharacterWindow сделать нельзя.

**Объём работы:** ~30 минут (enum + поле + OnValidate default + проверить компиляцию).

**Зависимости:** никаких (additive, backward-compat: `discipline = None` для существующих 8 .asset).

**Открытое решение:** какие именно значения enum?
- По дизайну `Battle/10_DESIGN.md §2.1`: `None=0, Melee=1, Ranged=2, Explosives=3, Antigrav=4, Defense=5`.
- Альтернатива: добавить `Social=99` (для фильтрации в общем списке). Не критично — есть уже `SkillCategory.Social`.

**Рекомендация:** точно по дизайну (5 значений + None).

**После O-2:** автоматически делается §2.3 (фильтр-чипы в CharacterWindow, ~1-1.5ч).

---

### O-3. Расширить `SkillEffect.Type` (T-CB01)

**Контекст:** §1.2 — нет 5 новых типов. Без них "изучил меч, но экипировать не могу" невозможно реализовать.

**Объём работы:** ~30 минут (только enum).

**Варианты расширения:**

| Variant | Новые Type | Поведение |
|---|---|---|
| **A. Минимум (по дизайну)** | WeaponProficiencyUnlock, ArmorProficiencyUnlock, WeaponTechniqueUnlock, ExplosiveRecipeUnlock, AntigravTechniqueUnlock (5 штук) | enum-only, runtime handler — в T-CB07 (отдельная сессия) |
| **B. С обработчиками** | то же + `ApplySkillEffects` в `SkillsServer` с switch по типу | больше работы, но "proficiency gate" начинает работать сразу |

**Рекомендация:** начать с **A** (только enum). Skill .asset пока используют `type: 0` (StatMod), новые коды не задействованы — обратная совместимость сохраняется. Handler — отдельная сессия (O-4).

**После O-3 (variant A):** можно начинать T-CB06 (EquipmentServer.TryEquip принимает WeaponItemData + проверка proficiency — warning-only на первом этапе).

---

### O-4. Реализовать `ApplySkillEffects` runtime handler (T-CB07)

**Контекст:** §1.2 — `SkillsServer` после `TryLearnSkill` отправляет `Learned(skillId)`, но **никаких runtime-эффектов не применяет**. StatMod читается через DTO в `StatsServer.RecomputeAndSendSnapshot` (это работает). Все остальные типы — no-op.

**Открытое решение:** scope обработчика.
- **Variant A (минимальный):** добавить switch в `ApplySkillEffects`, для каждого нового Type — лог `Debug.Log($"[SkillsServer] Effect applied: {type} '{stringParam}' for client={clientId}")`. Без реальных эффектов, но UI-side фильтры и проверки могут на это опереться.
- **Variant B (proficiency gate):** то же + при `WeaponProficiencyUnlock` добавлять в `_world._proficiencies[clientId][stringParam] = true`. `EquipmentServer.TryEquip` читает из этого списка и проверяет.
- **Variant C (полный):** Variant B + `WeaponTechniqueUnlock` → `_techniques[clientId][stringParam] = true`. CombatServer читает при ResolveAttack. Explosive/Antigrav — deferred.

**Зависимости:**
- O-3 (Type enum).
- O-5 (proficiency gate в EquipmentServer).

**Рекомендация:** начать с **Variant A** (logs only) в сессии O-4, потом **Variant B** в отдельной сессии как proof-of-concept для proficiency gate.

---

### O-5. `EquipmentServer.TryEquip` принимает `WeaponItemData` + proficiency gate (T-CB06)

**Контекст:** §1.2 — нужно проверить фактический код `EquipmentServer.cs`. Известно: `PlayerAttacker.RebuildSources` уже работает с `WeaponItemData` (`PlayerAttacker.cs:108-115`), значит weapon экипируется; **proficiency gate не вызывается**.

**Объём работы:** ~1-2 часа.

**Варианты:**

| Variant | Поведение |
|---|---|
| **A. Просто принимать Weapon** | В `TryEquip` добавить ветку `if (itemData is WeaponItemData w) { check slot == WeaponMain/Off; }`. Без proficiency gate. |
| **B. + Proficiency gate (hard)** | Variant A + если `w.requiredProficiency != null` → проверить, что player learned этот skill. Иначе `reason = "Нужен навык: {skillName}"`. |
| **C. + Proficiency gate (soft)** | Variant A + проверка, но failure только warning в Console (не блокирует equip). |

**Рекомендация:** начать с **A + warning-only** в сессии O-5 (как в Battle/10_DESIGN §5.2 "warning gate"). Hard gate (Variant B) — после O-4 (runtime handler).

**Также в этой сессии:**
- Проверить/добавить `ClothingItemData.armorDefense` (если отсутствует).

---

### O-6. Targeting — заменить "nearest NpcTarget в 15м" на raycast от камеры

**Контекст:** `NetworkPlayer.cs:288-315` — `DebugAttackNearestNpc()`, помечен "ВРЕМЕННЫЙ код, удалить в Phase 2".

**Объём работы:** ~1-2 часа (raycast из `Camera.main.transform.position + forward * maxRange` → первый hit с `NpcTarget` или `PlayerTarget`).

**Зависимости:** O-1 (раскладка — куда вешать ЛКМ).

**Рекомендация:** отдельная сессия после O-1 + после O-7 (SkillInputService каркас).

**Lock target (Tab) — отдельная подзадача:** для ranged/aim нужно держать target на дистанции. Это отдельный UI + state machine (target lock release on death / out-of-range / explicit press). Phase 2.

---

### O-7. SkillInputService (каркас "нажата кнопка → выполнить навык")

**Контекст:** §2.1 — единая точка входа для всех навыков.

**Объём работы:** ~2-3 часа (новый singleton-сервис).

**Архитектура:**

```csharp
public class SkillInputService : MonoBehaviour {
    // Singleton или компонент на NetworkPlayer
    public static SkillInputService Instance { get; private set; }

    // SkillId → inputSlot mapping (из SkillNodeConfig.inputSlot)
    private Dictionary<SkillInputSlot, string> _slotToSkillId = new();

    // TryActivate вызывается из NetworkPlayer.HandleAttackInput или напрямую из input events
    public bool TryActivate(SkillInputSlot slot, IDamageTarget target = null) {
        if (!_slotToSkillId.TryGetValue(slot, out var skillId)) {
            Debug.LogWarning($"[SkillInputService] No skill bound to slot {slot}");
            return false;
        }

        var skillConfig = SkillsWorld.Instance?.GetSkillConfig(skillId);
        if (skillConfig == null) return false;

        // 1) Cooldown check (централизованно в CombatServer, как для оружия)
        if (Time.unscaledTime < _cooldowns[slot]) return false;

        // 2) Send RPC server-side
        NetworkPlayer.Local.RequestUseSkillRpc(skillId, target?.GetTargetId() ?? 0);

        // 3) Local animation
        var trigger = !string.IsNullOrEmpty(skillConfig.attackAnimationTrigger)
            ? skillConfig.attackAnimationTrigger : "Attack";
        _animator.SetTrigger(trigger);

        // 4) Set local cooldown
        _cooldowns[slot] = Time.unscaledTime + skillConfig.cooldownSeconds;

        return true;
    }
}
```

**Зависимости:** O-1 (slot→SkillId биндинги), O-2 (SkillNodeConfig.inputSlot поле), O-3 (Type enum, для фильтрации).

**Рекомендация:** это **первая сессия** из серии полировки — каркас без него всё остальное копится.

---

### O-8. (Отдельный вопрос) — Рефакторить `PlayerInputReader` legacy → Input Actions?

**Контекст:** §1.4 — PlayerInputReader использует `Keyboard.current.*.isPressed` напрямую. Параллельно NetworkPlayer.Update тоже читает клавиши напрямую (F, T, P, K). Нарушает AGENTS.md правила.

**Объём работы:** ~3-4 часа (создать `InputActions.inputactions` asset, переписать `PlayerInputReader`, убрать хардкод из NetworkPlayer.Update).

**Открытое решение:** рефакторить сейчас или оставить legacy для скорости?

**Pros рефакторинга:**
- Единая точка истины для всех клавиш.
- Готово к gamepad (Xbox/PS контроллер).
- Можно делать rebinding в UI.

**Pros "оставить как есть":**
- Работает, не ломаем.
- Изоляция рефакторинга от Battle-полировки.

**Рекомендация:** **отдельная сессия**, не блокирует Battle-работу. Сначала закрываем O-1..O-7 (battle UI + skill system), потом O-8 как "техдолг".

---

## 4. Что предлагаю делать следующим (минимальный полезный набор за одну сессию)

Если идём по принципу "не ломать, доделывать" и **не хотим решать все O-1..O-7 сразу**, я предлагаю на **первую сессию** такой скоуп:

| # | Шаг | Объём | Что проверяем в Play Mode |
|---|---|---|---|
| 1 | **Создать `SkillInputService`** (новый singleton) | 2-3ч | Unit: TryActivate возвращает false для пустого slot |
| 2 | **Перенести K-attack** из `NetworkPlayer.Update:576-584` в вызов `SkillInputService.TryActivate(SkillInputSlot.Primary, targetFinderDelegate)` | 30м | Console: `K-attack: targetId=..., dist=...м` без изменений |
| 3 | **Добавить ЛКМ** как primary attack в `NetworkPlayer.Update` (`Mouse.current.leftButton.wasPressedThisFrame` → `SkillInputService.TryActivate(Primary)`) | 30м | ЛКМ и K дают одинаковый результат |
| 4 | **Добавить `OnAttackPressed` event** в `PlayerInputReader` (для будущего) — **не трогая legacy** | 30м | — |
| 5 | **Расширить `SkillEffect.Type`** (O-3, Variant A — только enum) | 30м | Компиляция чистая, существующие .asset работают как раньше |
| 6 | **Расширить `EquipmentServer.TryEquip`** (O-5, Variant A + warning-only proficiency) | 1-2ч | Equip меча работает; warning в Console если нет proficiency |
| 7 | **Combat фильтр в CharacterWindow** (после O-2 — substring fallback; без O-2 — substring match по `skillId`) | 1-1.5ч | UI: 5 чипов + фильтрация работает |
| 8 | **Документ:** создать `docs/dev/SKILLS_NEXT_STEPS_T-CB_LOG.md` (changelog сессий) | 15м | — |

**Итого ~7-10ч** за одну длинную сессию или 2 средних. Каждый шаг проверяем отдельно.

**Что НЕ делаем в этой сессии:**
- ❌ Полный рефакторинг Input System (O-8).
- ❌ Painter2D skill tree (T-P19) — Phase 2.
- ❌ ExplosiveItemData / WeaponClassCatalog (T-CB04/05) — отдельные сессии.
- ❌ Drag-and-drop skill → slot bar — после O-1 + SkillInputService.

---

## 5. Порядок сессий (roadmap)

| Сессия | Тема | Tickets | Зависимости |
|---|---|---|---|
| **#1 (эта)** | Аудит | — | — |
| **#2** | SkillInputService + ЛКМ как primary + K-fallback | T-INP-01, T-INP-02, T-INP-03 | O-1 (хотя бы черновик), O-2 |
| **#3** | SkillEffect.Type расширение (T-CB01, Variant A) | T-CB01 | — |
| **#4** | CombatDiscipline поле в SkillNodeConfig (T-CB02) | T-CB02 | — |
| **#5** | CharacterWindow combat фильтр (по discipline) | T-CB09 | T-CB02 |
| **#6** | EquipmentServer.TryEquip + WeaponItemData + warning-only proficiency | T-CB06 (variant A+C) | T-CB01 (enum) |
| **#7** | ApplySkillEffects runtime handler (T-CB07, Variant A logs + B proficiency) | T-CB07 | T-CB01, T-CB06 |
| **#8** | Targeting: raycast вместо "nearest NpcTarget в 15м" | T-RTC10 (новый) | T-INP-* |
| **#9** | Удалить debug-K-attack код (`DebugAttackNearestNpc`) | cleanup | T-INP-* |
| **#10** | Skill tree Painter2D UI (T-P19) | T-P19 | — |
| **#11** | Input System рефакторинг (legacy → Input Actions) | T-INP-REFAC | T-INP-* |
| **#12+** | ExplosiveItemData, WeaponClassCatalog, ArmorClassCatalog | T-CB04, T-CB05 | T-CB01, T-CB02 |
| **Phase 2** | PvP duel, NPC-AI, ship combat | T-RTC11..T-RTC20 | T-RTC* + T-CB* |

**Ключевое:** сессии #2..#7 — это **MVP+1 polish** для боевых навыков. Сессии #8..#11 — техдолг и UX-полировка. Сессии #12+ — расширения.

---

## 6. Open Questions Index (резюме)

| ID | Вопрос | Сессия | Блокер? |
|---|---|---|---|
| **O-1** | Раскладка клавиш (A/B/C) | #2 | 🔴 да, без неё SkillInputService проектировать нельзя |
| **O-2** | CombatDiscipline в SkillNodeConfig (T-CB02) | #4 | 🟡 для фильтра UI; можно fallback substring |
| **O-3** | SkillEffect.Type расширение (T-CB01) | #3 | 🟡 для proficiency gate |
| **O-4** | ApplySkillEffects runtime handler (T-CB07) | #7 | 🟡 nice-to-have, не блокирует |
| **O-5** | EquipmentServer.TryEquip + WeaponItemData + proficiency (T-CB06) | #6 | 🟡 для weapon-equip flow |
| **O-6** | Targeting raycast | #8 | 🔴 для ranged/aim |
| **O-7** | SkillInputService каркас | #2 | 🔴 да, без него вся input-архитектура расплывается |
| **O-8** | Input System рефакторинг | #11 | ⚪ техдолг, не блокирует |

**Минимальный скоуп для первой содержательной сессии (#2):** решить O-1 + сделать O-7 + ЛКМ.

---

## 7. Файлы для следующих сессий (быстрый reference)

| Что | Path |
|---|---|
| Combat engine hub | `Assets/_Project/Scripts/Combat/Network/CombatServer.cs` |
| Player combat adapter | `Assets/_Project/Scripts/Combat/Implementations/PlayerAttacker.cs` |
| Weapon SO | `Assets/_Project/Scripts/Equipment/WeaponItemData.cs` |
| Equipment server (TryEquip) | `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` |
| Skills server (ApplySkillEffects) | `Assets/_Project/Scripts/Skills/SkillsServer.cs` |
| Skills config | `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` |
| Skills effect types | `Assets/_Project/Scripts/Skills/SkillEffect.cs` |
| Skill assets | `Assets/_Project/Resources/Skills/Skill_*.asset` |
| Character window UI | `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` |
| NetworkPlayer (K-attack + Update) | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` |
| Player input (legacy) | `Assets/_Project/Scripts/Player/PlayerInputReader.cs` |
| Player animator | `Assets/_Project/Animations/PlayerAnimation.controller` |

---

## 8. Диагностические команды (если что-то сломается)

```bash
# === Compile check ===
# Open Unity Editor → Console → 0 errors expected

# === Проверить типы SkillEffect ===
grep -n "public enum Type" Assets/_Project/Scripts/Skills/SkillEffect.cs
# Ожидаем 7 значений: StatMod=0, AbilityUnlock=1, PassiveEffect=2, 
#                      WeaponProficiencyUnlock=3, ArmorProficiencyUnlock=4,
#                      WeaponTechniqueUnlock=5, ExplosiveRecipeUnlock=6,
#                      AntigravTechniqueUnlock=7

# === Проверить CombatDiscipline ===
grep -n "CombatDiscipline" Assets/_Project/Scripts/Skills/SkillNodeConfig.cs
# Если пусто — O-2 не сделано

# === Проверить K-attack debug код ===
grep -n "DebugAttackNearestNpc\|K-attack\|kKey" Assets/_Project/Scripts/Player/NetworkPlayer.cs
# Если есть — это временный код, должен быть выпилен в сессии #9

# === Проверить EquipmentServer.TryEquip + WeaponItemData ===
grep -n "WeaponItemData\|weaponClass" Assets/_Project/Scripts/Equipment/EquipmentServer.cs
# Если пусто — O-5 не сделано

# === Проверить ApplySkillEffects ===
grep -n "ApplySkillEffects\|case SkillEffect.Type" Assets/_Project/Scripts/Skills/SkillsServer.cs
# Если пусто — O-4 не сделано

# === Список skill assets ===
ls Assets/_Project/Resources/Skills/Skill_*.asset | wc -l
# Ожидаем 30 (4 combat + 22 дисциплинарных + 4 social)
```

---

## 9. История изменений документа

| Дата | Автор | Изменения |
|---|---|---|
| 2026-06-26 | Mavis (аудит) | Первая версия. Read-only аудит состояния. Зафиксированы O-1..O-8. Roadmap сессий #1..#12+. |
| 2026-06-28 | Mavis (отчёт) | §7 добавлен: SkillTreeWindow overlay + 2D граф навыков реализованы. §4, §5 обновлены статусы. |

---

## 7. Реализовано (сессии #4-5) — SkillTreeWindow overlay + 2D граф навыков

> **Сессии:** #4 (SkillTreeWindow), #5 (2D граф)
> **Дизайн:** `Battle/60_SKILL_TREE_WINDOW_DESIGN.md`, `Battle/70_SKILL_TREE_2D_GRAPH.md`
> **Дата:** 2026-06-28

### 7.1 SkillTreeWindow — полноценное overlay-окно для всех навыков

**Файлы:**

| Файл | LOC | Назначение |
|---|---|---|
| `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs` | ~640 | Instance-синглтон, Show/Hide, EnsureBuilt, 4 фикса UI_TOOLKIT_GUIDE, Esc-handler, RefreshAllSkillsList, OnSkillSelected, UpdateDetailPanel, InitFilterChips, InitSearchField, InitActionButtons |
| `Assets/_Project/Resources/UI/SkillTreeWindow.uxml` | ~70 | Layout: top (title+6 chips+search) + middle (left scroll-list + right detail panel) + bottom (close) |
| `Assets/_Project/UI/Resources/UI/SkillTreePanelSettings.asset` | — | PanelSettings (копия CharacterPanelSettings, sortingOrder=300) |
| `Assets/_Project/Resources/UI/SkillTreeWindow.uss` | ~300 | Стили: chips/active, search, scroll, detail panel, btn-learn/forget/close |

**Детали реализации:**
- **Стандартный UIDocument singleton** (как CharacterWindow), auto-spawn через `NetworkManagerController.CreateSkillTreeWindow()`
- **6 chip-фильтров** (Все/Melee/Ranged/Explosives/Antigrav/Defense) — фильтрация по subtree prefix
- **Поиск** по skillId, displayName, effect type name, floatValue, multiplier
- **Детальная панель** при клике: name, description, effects (formatted как `+STR+2×1.15`), cost, INT tier, prereq (кто нужен), dependents (кого откроет)
- **Кнопки Изучить/Забыть** — inline toggle display через C#, не USS `!important`
- **Авто-позиционирование**: USS `left: 50%; translate: -50% 0; width: 760px`
- **Combat-only кнопка** в CharacterWindow: `[ИЗУЧИТЬ НАВЫК]` открывает SkillTreeWindow
- **CharacterWindow combat-блок** очищен: только LEARNED навыки без action-кнопок

**UI_TOOLKIT_GUIDE фиксы применены:**
- `pickingMode=Position/Ignore` (FIX 1)
- `Cursor.lockState` переключение (FIX 2)
- `MarkDirtyRepaint` + `schedule.Execute(...).StartingIn(50)` (FIX 3)
- `CloneTree()` + `Clear()` + `Add(_rootContainer)` (FIX 4)
- Esc-handler ДО NetworkManager guard

### 7.2 2D граф навыков (Painter2D)

**Архитектура:**

```
SkillTreeWindow
├── ScrollView ("tree-canvas-scroll")     ← native scroll + pan
│   └── VisualElement ("tree-content")     ← 2000×2000 px canvas
│       ├── [Node: BasicSword]            ← position: absolute по treeX/treeY
│       ├── [Node: GreatSword]            ← с state-рамкой (зел/жёлт/сер)
│       ├── [Node: ...]
│       └── generateVisualContent         ← Painter2D линии prereq→навык
```

**Ключевые особенности:**
- **Canvas 2000×2000 px** с абсолютным позиционированием узлов по `treeX/treeY` (scale ×2.5 + padding)
- **Узлы** = VisualElement 160×36, state-цвета рамки (зелёный ✅ / жёлтый ○ / серый ✕)
- **Линии** через `generateVisualContent` → `ctx.painter2D` (Unity 6 API):
  - lineWidth=2, цвет по state родительского навыка
  - Стрелка от prereq → skill (MoveTo → LineTo)
  - Рисуются относительно resolvedStyle узлов
- **Filter + Search** скрывают несоответствующие узлы и их рёбра
- **Pan/Scroll**: через ScrollView (native UI Toolkit)
- **Клик на узел** → SelectSkill(skillId) → обновление детальной панели
- **Выбранный узел**: выделен классом `.tree-node-selected` (яркая рамка 3px)

**Стили узлов:**

```css
.tree-node { position: absolute; width: 160px; min-height: 36px; ... }
.tree-node-learned    { border-color: rgb(80, 200, 120); }
.tree-node-available  { border-color: rgb(180, 200, 60); }
.tree-node-locked     { border-color: rgba(100, 100, 110, 0.5); }
.tree-node-selected   { border-color: rgb(100, 180, 255); border-width: 3px; }
```

### 7.3 Что было исправлено/улучшено по ходу

| № | Проблема | Фикс | Дата |
|---|---|---|---|
| 1 | `_rootContainer` full-screen синий фон закрывал весь экран | Убран `_rootContainer.style.backgroundColor` (был debug fallback) | 2026-06-28 |
| 2 | `right: 0` / `bottom: 0` из `EnsureBuilt()` конфликтовали с `left: 50%` | Оставлено как есть (USS `translate` переопределяет) | 2026-06-28 |
| 3 | Размер окна был нестабильным | USS `width: 760px` + `left: 50%; translate: -50% 0` | 2026-06-28 |

### 7.4 Открыто / не реализовано

- ❌ `SkillNodeConfig.CombatDiscipline` поле (T-CB02) — фильтр по substring prefix
- ❌ `SkillEffect.Type` runtime handler (T-CB07) — enum расширен, handler no-op
- ❌ Drag-to-slot (skill → slot bar) — Phase 2
- ❌ Toasts на learn/forget — пока Debug.Log
- ❌ Полноценная анимация переходов между узлами — Phase 2
- ❌ Узлы с иконками дисциплин — сейчас только текст

### 7.5 Текущее состояние в Play Mode

1. P → CharacterWindow → "Изученные боевые навыки" (только LEARNED) + кнопка `[ИЗУЧИТЬ НАВЫК]`
2. Клик → открывается SkillTreeWindow (760px centered, cursor unlock)
3. 6 chip-фильтров + поиск + граф слева + детальная панель справа
4. Клик на узел → детали
5. Изучить/Забыть — работают через reflection RPC

---

**Update to §4 (Next steps — originally from audit §4):**

Из §4 «Что предлагаю делать следующим»:
- ✅ Шаги #1-4 (SkillInputService + ЛКМ) — сделаны в сессии #2
- ✅ Шаг #5 (SkillEffect.Type расширение) — сделано (T-CB01)
- ✅ Шаг #6 (EquipmentServer warning) — сделано
- ✅ Шаг #7 (Combat фильтр + SkillTreeWindow) — сделано (+ 2D граф)
- ✅ Шаг #8 (документ LOG) — сделан

**Что теперь предлагаю делать вместо §4:**

| # | Шаг | Почему |
|---|---|---|
| 1 | Добавить `SkillNodeConfig.CombatDiscipline` поле (T-CB02) | Фильтр будет точным, а не по substring |
| 2 | Toasts на learn/forget | UX без Debug.Log |
| 3 | `ApplySkillEffects` runtime handler для новых Type (T-CB07) | Proficiency gate начнёт работать |
| 4 | Raycast targeting вместо nearest NpcTarget | Для ranged/aim |
| 5 | Удалить `DebugAttackNearestNpc` | Чистка tech-debt |

---

**Update to §5 (Roadmap status):**

| Сессия | Тема | Статус |
|---|---|---|
| #1 (аудит) | Аудит | ✅ DONE |
| #2 | SkillInputService + ЛКМ + T-CB* | ✅ DONE |
| #3 | Battle Skills UI (CharacterWindow) | ✅ DONE |
| #4 | SkillTreeWindow overlay | ✅ DONE |
| #5 | 2D граф навыков (Painter2D) | ✅ DONE |