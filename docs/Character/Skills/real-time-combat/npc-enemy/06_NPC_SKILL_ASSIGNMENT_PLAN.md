# План: Назначение скилов игрока на NPC с оверрайдами

> **Дата:** 2026-07-27
> **Статус:** ✅ Реализовано (Фазы A–E)
> **Коммит:** `7ebfb9a`
> **Контекст:** у NPC сейчас один тип атаки (NpcCombatData с одним NpcDefaultDamageSource). Нужно дать возможность назначать SkillNodeConfig (скилы игрока) с кастомными оверрайдами (cooldown, анимация, damage и т.п.)

---

## 1. Текущая архитектура (что есть)

### 1.1 NPC-сторона

```
NpcSpawnerConfig (SO)
  └─ npcPrefab → Npc_Goblin.prefab
                    ├─ NpcCombatData (SO) — stats + ОДИН дефолтный weapon (damageType/Dice/baseDamage/cooldown/range)
                    ├─ NpcAttacker : IAttacker
                    │    └─ NpcDefaultDamageSource (hardcoded) — оборачивает NpcCombatData
                    └─ NpcBrain (FSM)
                         └─ TryAttack() → CombatServer.ResolveAttack(attackerId, targetId, sourceId=attackerId)
                         └─ _animator.SetTrigger("Attack") — ХАРДКОД
```

**Проблемы:**
- `NpcAttacker.GetActiveDamageSources()` всегда возвращает ровно 1 `NpcDefaultDamageSource`
- `NpcBrain.TryAttack()` всегда дёргает `SetTrigger("Attack")` — нет per-skill анимации
- Нет концепта «у NPC может быть несколько скилов»
- Нет оверрайда параметров скила под конкретного NPC

### 1.2 Игрок-сторона

```
SkillNodeConfig (SO)
  ├─ skillId, displayName
  ├─ discipline, subtype
  ├─ isActive, cooldownSeconds
  ├─ attackClip, attackClipSpeed   ← анимация
  ├─ aoeFormula, aoeSize, aoeConeAngleDeg, aoeWidth
  ├─ throwRange, throwScatter, throwCount  (subtype=Throwables)
  ├─ rangedMaxRange, rangedHitChance        (subtype=Bows/Crossbows)
  └─ effects[] (статы, unlock'и)

SkillInputService (client-side)
  └─ TryActivate(slot)
       ├─ Читает SkillNodeConfig по skillId
       ├─ Проверяет cooldown (локальный)
       ├─ Проигрывает skill.attackClip через SkillAnimationPlayer
       └─ Отправляет CombatServer.RequestAttackRpc(attackerId, targetId, sourceId=0)
```

### 1.3 CombatServer.ResolveAttack (общий для всех)

`CombatServer.ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId)` работает через `IAttacker`:
1. Находит `IAttacker` по `attackerId`
2. Вызывает `attacker.GetDamageSource(sourceId)` → получает `IDamageSource`
3. Читает damageType, damageDice, baseDamage, range из source
4. Делает distance check, hit roll, damage calc
5. Применяет damage к `IDamageTarget`

Это **уже работает** для NPC — `NpcAttacker` имплементирует `IAttacker`. Но у него всегда один source.

---

## 2. Целевая архитектура

```
NpcSpawnerConfig (SO)
  └─ npcPrefab → Npc_Goblin.prefab
  └─ npcSkillSet: NpcSkillSet (SO)           ← НОВОЕ
       ├─ skills[]
       │    ├─ skillConfig: SkillNodeConfig   ← ссылка на тот же SO что у игрока
       │    ├─ overrideCooldown: float?        ← переопределить кулдаун
       │    ├─ overrideAnimation: AnimationClip? ← кастомная анимация для NPC
       │    ├─ overrideDamageDice: DamageDice?
       │    ├─ overrideBaseDamage: int?
       │    ├─ overrideRange: float?
       │    ├─ priority: int                  ← вес для случайного выбора (0-100)
       │    └─ minHpPercent / maxHpPercent    ← при каких HP% доступен скилл
       └─ selectionMode: RandomWeighted | RoundRobin | PriorityFirst
```

### 2.1 NpcSkillOverride (новый SO или Serializable-структура)

Вынесем в **Serializable struct** внутри `NpcSkillSet`, НЕ отдельный SO:

```csharp
[Serializable]
public struct NpcSkillOverride
{
    [Tooltip("Ссылка на SkillNodeConfig (тот же .asset что у игрока).")]
    public SkillNodeConfig skillConfig;

    [Header("Overrides (оставьте 0/null чтобы использовать default из SkillNodeConfig)")]
    public float overrideCooldown;      // 0 = использовать skillConfig.cooldownSeconds
    public AnimationClip overrideAnimation;
    public DamageDice overrideDamageDice; // None = использовать из skill-логики
    public int overrideBaseDamage;       // 0 = использовать default
    public float overrideRange;          // 0 = использовать default

    [Header("AI Selection")]
    [Range(0, 100)] public int priority;
    [Range(0f, 1f)] public float minHpPercent;
    [Range(0f, 1f)] public float maxHpPercent;
}
```

### 2.2 NpcSkillSet (новый SO)

```csharp
[CreateAssetMenu(fileName = "NpcSkillSet_", menuName = "Project C/AI/NPC Skill Set")]
public class NpcSkillSet : ScriptableObject
{
    public enum SelectionMode { RandomWeighted, RoundRobin, PriorityFirst }

    public SelectionMode selectionMode = SelectionMode.RandomWeighted;
    public NpcSkillOverride[] skills = Array.Empty<NpcSkillOverride>();
    public SkillNodeConfig defaultAttack;   // fallback если skills пуст (= backward compat)
}
```

### 2.3 NpcAttacker — замена NpcDefaultDamageSource на SkillDamageSource

Вместо одного `NpcDefaultDamageSource`, `NpcAttacker` получает **массив** `NpcSkillDamageSource`:

```csharp
// Каждый скилл → свой IDamageSource с параметрами из SkillNodeConfig + оверрайдами
private sealed class NpcSkillDamageSource : IDamageSource
{
    // Берёт базовые параметры из SkillNodeConfig,
    // но оверрайдит их полями из NpcSkillOverride (если != 0/null)
    public ulong GetSourceId() => ...; // index в массиве skills
    public DamageType GetDamageType() => ...;
    public DamageDice GetDamageDice() => overrideDice ?? skillConfig.overridden;
    public int GetBaseDamage() => overrideDamage > 0 ? overrideDamage : skillConfig.baseValue;
    public float GetRange() => overrideRange > 0 ? overrideRange : skillConfig.range;
    public float GetCooldownSeconds() => overrideCooldown > 0 ? overrideCooldown : skillConfig.cooldownSeconds;
    ...
}
```

`GetActiveDamageSources()` возвращает массив всех `NpcSkillDamageSource` (по числу скилов в `NpcSkillSet`).

### 2.4 NpcBrain — выбор скилла

`NpcBrain.TryAttack()` перестаёт быть хардкодным:

```csharp
private void TryAttack()
{
    // 1. Выбрать скилл по selectionMode
    var skill = PickSkill(_skillSet); // RandomWeighted / RoundRobin / PriorityFirst
    if (skill == null) { /* fallback: current NpcCombatData behavior */ return; }

    // 2. IDamageSource для выбранного скилла
    ulong sourceId = (ulong)skill.index; // индекс в массиве

    // 3. Атака через CombatServer
    CombatServer.Instance.ResolveAttack(attackerId, targetId, sourceId);

    // 4. Анимация: кастомная или из skill.attackClip
    AnimationClip clip = skill.overrideAnimation != null
        ? skill.overrideAnimation
        : skill.skillConfig.attackClip;

    PlaySkillAnimation(clip, skill.skillConfig.attackClipSpeed);
}
```

### 2.5 NpcAnimator — per-skill animation

Текущий `_animator.SetTrigger("Attack")` заменяется на механизм `SkillAnimationPlayer` (тот же что у игрока) или аналог:
- `NpcBrain` получает ссылку на `SkillAnimationPlayer` (или упрощённый `NpcSkillAnimationPlayer`)
- При выборе скилла — `Play(clip, speed)`
- Без клипа — fallback на `SetTrigger("Attack")`

---

## 3. Фазы реализации

### Фаза A: NpcSkillSet + NpcSkillOverride (Data layer)

**Новые файлы:**
- `Assets/_Project/Scripts/AI/NpcSkillSet.cs` — ScriptableObject с `NpcSkillOverride[]` + `SelectionMode`
- Структура `NpcSkillOverride` — внутри того же файла (Serializable struct)

**A1.** Создать `NpcSkillSet : ScriptableObject`:
- `selectionMode` enum
- `skills[]` массив `NpcSkillOverride`
- `defaultAttack` fallback

**A2.** `NpcSkillOverride` struct:
- `skillConfig`, `overrideCooldown`, `overrideAnimation`, `overrideDamageDice`, `overrideBaseDamage`, `overrideRange`
- `priority`, `minHpPercent`, `maxHpPercent`

**A3.** Создать Editor-скрипт `NpcSkillSetEditor.cs` для удобного UI (список скилов с preview параметров).

> **Риск:** низкий. Только data-типы, не затрагивает runtime.

---

### Фаза B: NpcAttacker — multi-source refactor

**Изменяемые файлы:**
- `Assets/_Project/Scripts/Combat/Implementations/NpcAttacker.cs`

**B1.** Добавить поле `_skillSet: NpcSkillSet` (сериализованное, назначается на префабе или через спавнер).

**B2.** Заменить `NpcDefaultDamageSource` на массив `NpcSkillDamageSource[]`:
- В `Initialize()` / `OnNetworkSpawn()`: читать `_skillSet.skills`, создавать по одному `NpcSkillDamageSource` на каждый entry.

**B3.** `GetActiveDamageSources()` → возвращает все `NpcSkillDamageSource`.

**B4.** `GetDamageSource(ulong sourceId)` → `sourceId = index` в массиве (если 0 — fallback на default).

**B5.** `CanAttack(source, now)` и `SetCooldown(source, until)` — per-source cooldown (словарь `Dictionary<ulong, float>`).

**B6.** Backward-compat: если `_skillSet == null` → fallback на текущий `NpcDefaultDamageSource` (ничего не ломается).

> **Риск:** средний. Меняет ядро NpcAttacker. Нужно тщательное тестирование.

---

### Фаза C: NpcBrain — skill selection + per-skill animation

**Изменяемые файлы:**
- `Assets/_Project/Scripts/AI/NpcBrain.cs`

**C1.** `TryAttack()` — заменить хардкод:
- Вызвать `PickSkill()` (RandomWeighted / RoundRobin / PriorityFirst с учётом HP%)
- Передать `sourceId` в `CombatServer.ResolveAttack`

**C2.** `PickSkill()`:
- Фильтровать по `minHpPercent`/`maxHpPercent`
- RandomWeighted: сумма весов `priority`, случайный выбор
- RoundRobin: следующий по очереди
- PriorityFirst: первый доступный с `priority > X`

**C3.** Анимация:
- Найти `SkillAnimationPlayer` (или создать лёгкий `NpcSkillAnimationPlayer`)
- `Play(clip, speed)` → если нет клипа → fallback `SetTrigger("Attack")`

**C4.** Fallback: если `_skillSet == null` или `skills` пуст → поведение как сейчас (backward compat).

> **Риск:** средний. FSM ядро меняется точечно. Backward-compat гарантирован.

---

### Фаза D: NpcSpawnerConfig — подключение NpcSkillSet

**Изменяемые файлы:**
- `Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs`
- `Assets/_Project/Scripts/AI/NpcSpawner.cs` (если есть)

**D1.** Добавить поле `npcSkillSet: NpcSkillSet` в `NpcSpawnerConfig`.

**D2.** В `NpcSpawner` после `Instantiate(prefab)`:
- `npcAttacker.SetSkillSet(config.npcSkillSet)` — применение скиллов из конфига спавнера
- Это **НЕ подтирает** префаб — префаб может иметь свой `_skillSet` по умолчанию, спавнер оверрайдит

**D3.** Создать `Assets/_Project/Resources/AI/NpcSkillSet_Goblin.asset` — пример с 2-3 скилами для гоблина.

> **Риск:** низкий. Add-only поля.

---

### Фаза E: Интеграция и тестирование

**E1.** Создать `NpcSkillSet_Goblin.asset` с:
- `skillConfig = Skill_Melee_BasicSword` (игровой скилл) + `overrideAnimation = HumanM@Attack02` + `overrideCooldown = 1.2f`
- `skillConfig = Skill_Combat_HeavySwing` + `overrideAnimation = HumanM@Attack03` + `overrideCooldown = 3.0f` + `priority = 30` (реже)

**E2.** Привязать `NpcSkillSet_Goblin` к `NpcSpawner_Default.npcSkillSet`.

**E3.** Play Mode тест:
- Goblin спавнится → атакует разными скилами
- Видна разная анимация
- Разный cooldown соблюдается
- При удалении `npcSkillSet` — fallback на старый `NpcDefaultDamageSource`

---

## 4. Порядок выполнения

| # | Фаза | Файлы | Риск | Зависит от |
|---|------|-------|------|------------|
| 1 | **A** — NpcSkillSet + NpcSkillOverride | Новые: `NpcSkillSet.cs` | 🟢 Низкий | — |
| 2 | **B** — NpcAttacker multi-source | `NpcAttacker.cs` | 🟡 Средний | A |
| 3 | **C** — NpcBrain skill selection + anim | `NpcBrain.cs` | 🟡 Средний | B |
| 4 | **D** — NpcSpawnerConfig + интеграция | `NpcSpawnerConfig.cs`, `NpcSpawner.cs` | 🟢 Низкий | C |
| 5 | **E** — .asset + Play Mode тест | Новый `NpcSkillSet_Goblin.asset` | 🟢 Низкий | D |

---

## 5. Ответы на ключевые вопросы

### Q1: Как скилл игрока (SkillNodeConfig) работает у NPC без SkillInputService?

`SkillInputService` — это UI/input-слой (нажатие кнопки → RPC). У NPC он не нужен. `CombatServer.ResolveAttack` работает **через IAttacker/IDamageSource** — это общий код. Всё что нужно: `NpcAttacker` должен предоставить `IDamageSource`, параметры которого считаны из `SkillNodeConfig` (с оверрайдами). Это и делается в Фазе B.

### Q2: Что с AOE (Cone/Sphere/Line/Box)?

AOE-логика находится в `CombatServer.ResolveAttack` (или рядом), она читает `aoeFormula`/`aoeSize` из... стоп, она читает из `IDamageSource`? Нет — текущий `IDamageSource` интерфейс не имеет AOE-полей. AOE определяется через `SkillNodeConfig` на клиенте.

**Решение для NPC:** `NpcSkillDamageSource` нужно расширить — либо:
- (A) Добавить AOE-поля в `IDamageSource` интерфейс (дорого, ломает всех)
- (B) `CombatServer.ResolveAttack` получает доп. информацию через отдельный канал

**Рекомендация:** для MVP — AOE скилы NPC работают через тот же flow что у игрока. Если это требует рефакторинга `IDamageSource` — это отдельный тикет, выходящий за рамки данного плана. В первом приближении NPC-скилы с AOE «пробрасывают» свои параметры через расширенный `NpcSkillDamageSource`.

### Q3: Нужен ли NPC отдельный AnimatorController или используется тот же что у игрока?

**Текущий:** `NpcAnimator_Goblin.overrideController` (на префабе).
**План:** анимация скилла проигрывается через `SkillAnimationPlayer.Play(clip, speed)` — тот же что у игрока. Он использует `AnimatorOverrideController` на стейт "Skill". `NpcAnimator_Goblin` должен иметь стейт "Skill" в Base Layer.

Если стейта "Skill" нет — fallback на `SetTrigger("Attack")` (backward compat).

### Q4: Что с AnimationClip для NPC если у игрока другой риг?

NPC (HumanM_Model) и игрок — оба используют Kevin Iglesias Humanoid риг. Анимации **совместимы** через Humanoid Avatar. `attackClip` из `SkillNodeConfig` можно использовать как есть. Но дизайнер может захотеть **другую** анимацию для NPC-версии скилла → поле `overrideAnimation` в `NpcSkillOverride`.

### Q5: Как NPC выбирает какой скилл использовать?

Через `selectionMode` в `NpcSkillSet`:
- **RandomWeighted** (default): случайный выбор с весом `priority`
- **RoundRobin**: по очереди, чтобы скиллы чередовались
- **PriorityFirst**: всегда скилл с максимальным priority, если доступен

Плюс фильтр по HP%: `minHpPercent`/`maxHpPercent` позволяют задать «этот скилл — только когда HP < 30%» (отчаянная атака).

---

## 6. Файлы

### Новые:
- `Assets/_Project/Scripts/AI/NpcSkillSet.cs` — SO + struct
- `Assets/_Project/Editor/NpcSkillSetEditor.cs` — кастомный инспектор (опционально)
- `Assets/_Project/Resources/AI/NpcSkillSet_Goblin.asset` — пример

### Изменяемые:
- `Assets/_Project/Scripts/Combat/Implementations/NpcAttacker.cs`
- `Assets/_Project/Scripts/AI/NpcBrain.cs`
- `Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs`
- `Assets/_Project/Scripts/AI/NpcSpawner.cs`

---

## 7. Что НЕ входит в этот план (out of scope)

- **AOE для NPC-скилов:** требует расширения `IDamageSource` или отдельного канала — отдельный тикет
- **Throwable-скилы NPC (гранаты):** NPC не имеют инвентаря — отдельная подсистема
- **Skill-дерево для NPC:** NPC не «изучают» скилы — они получают их от `NpcSkillSet`
- **Визуальные эффекты скилов (VFX):** текущий `SkillNodeConfig` не имеет VFX-полей — отдельный тикет
