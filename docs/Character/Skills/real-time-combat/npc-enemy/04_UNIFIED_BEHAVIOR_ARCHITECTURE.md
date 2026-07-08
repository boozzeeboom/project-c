# 04 — Unified NPC Behavior Architecture: синтез двух анализов

> **Дата:** 2026-07-15
> **Статус:** ✅ Дизайн утверждён | ✅ Phase 1 | ✅ Phase 2 | ✅ Phase 3 | ✅ Phase 4
> **Источники:**
> - `02_SOCIAL_HUMAN_BEHAVIOR.md` — модульная архитектура, 4 слоя поведения, ~44-62 ч
> - `03_SOCIAL_HUMAN_BEHAVIOR_ANALYSIS.md` — соц-псих theories, personality, emotion, ~37.5 ч
> - `NpcBrain.cs` (v0.3, 712 строк), `NpcSpawner.cs`, `NpcSpawnerConfig.cs`
> - `../70_NPC_ENEMIES.md` (базовая архитектура NPC), `../10_DESIGN.md` (combat engine)

---

## 0. Сверка: что общего, что разного, что берём

### 0.1 Консенсус (оба анализа совпадают)

| Пункт | Позиция | Берём? |
|---|---|---|
| Нужен Patrol (waypoints) | ✅ Оба | ✅ Да — P0 |
| Нужен Flee (бегство) | ✅ Оба | ✅ Да — P0 |
| Нужен Alarm / Call for Help | ✅ Оба | ✅ Да — P0 |
| Нужен Group coordination | ✅ Оба: NpcGroupController | ✅ Да — P1 |
| Нужна Threat assessment | ✅ Оба | ✅ Да — P1 |
| Нужны эмоции/мораль | ✅ Оба (разная детализация) | ✅ Да — P2 |
| Нужна реакция на смерть союзника | ✅ Оба | ✅ Да — P1 |
| Server-authoritative всё | ✅ Оба | ✅ Да — без изменений |
| NpcSpawnerConfig расширяется | ✅ Оба | ✅ Да |

### 0.2 Конфликты и решения

| Конфликт | Позиция 02 | Позиция 03 | **Решение (синтез)** |
|---|---|---|---|
| **Архитектура** | Рефакторить NpcBrain на модули | Add-only, partial class | **03 для Phase 1** (companion `NpcSocialBrain` + partial class). **02 как цель Phase 3** — если модулей станет >5, провести рефакторинг. |
| **Эмоции** | Morale FSM: Confident→Shaken→Panicked | NpcEmotion: Calm/Alert/Fear/Anger/Despair/Victory | **Берём 03** — 6 эмоций богаче и лучше ложатся на триггеры. 02-мораль станет частью: Fear↔Anger переход зависит от courage. |
| **Личность** | Нет (отложено в Social Roles) | NpcPersonalityConfig: 5 traits | **Берём 03** — traits влияют на пороги эмоций. Добавляем personality weight на каждый behaviour-триггер. |
| **Idle-активности** | Только Patrol | 8 типов (StandStill..Sleep) | **Берём 03** — 8 типов. 02-Patrol = один из них. Остальные — Phase 2. |
| **Триггеры** | Неявные (внутри модулей) | 7 явных: AllyInCombat, AllyKilled, ... | **Берём 03** — explicit triggers с приоритетами. Они становятся входом для 02-модулей в будущем. |
| **Vocal cues** | Внутри AlarmModule | 5 cue-типов с эффектами | **Берём 03** — детальная система. В 02 это растворилось в AlarmModule. |
| **Post-combat** | Не затронут | Wounded/Heal/Reinforcement | **Берём 03** — важный missing piece. 02 этого не касался. |
| **Теор. база** | Нет | 6 соц-псих теорий | **Берём 03** — отличное обоснование дизайна. Добавляет глубину. |

### 0.3 Уникальные сильные стороны каждого

**Что берём из 02 (чего нет в 03):**
- **4-слойная модель** (рефлексы→тактика→эмоции→социум) — чёткая архитектурная рамка
- **Приоритетная арбитражная система** (Priority-based decision arbitration) — решает конфликт решений
- **CoverModule** — детальное проектирование укрытий
- **Social Roles** (Guard/Civilian/Merchant/Thug/Leader) — готовые пресеты поведения
- **Faction System** (NpcFaction SO, relations) — база для межфракционных отношений
- **Vengeance Memory** — persistence обидчика между спавнами

**Что берём из 03 (чего нет в 02):**
- **6 социально-психологических теорий** — теоретический фундамент
- **NpcEmotion (6 состояний)** — богаче чем 3-state Morale
- **NpcPersonalityConfig (5 traits)** — параметризация индивидуальности
- **7 Social Triggers** — явные, с приоритетами
- **5 Vocal Cues** — с gameplay-эффектами
- **8 Idle Activities** — включая Socialize, Work, Sit, Sleep
- **Post-combat behavior** — Wounded, Heal, Reinforcement
- **Grudge memory** — конкретная реализация через `_grudgeTable`

---

## 1. Архитектурное решение (синтез)

### 1.1 Выбранная архитектура: Composition-first

```
                    ┌──────────────────────┐
                    │    NpcBrain.cs        │  ← НЕ ТРОГАЕМ (712 строк, работает)
                    │  Core FSM:            │
                    │  Idle/Chase/Attack/   │
                    │  Dead                 │
                    │  + NavMeshAgent       │
                    │  + Platform carry     │
                    └──────────┬───────────┘
                               │ GetComponent<> доступ
                               ▼
┌──────────────────────────────────────────────────────┐
│              NpcSocialBrain.cs  (NEW)                 │
│  Companion MonoBehaviour на том же GameObject.        │
│                                                      │
│  Читает NpcBrain (state, aggroTarget, HP).           │
│  Пишет в NpcBrain через API:                         │
│    NpcBrain.ForceChase(target)                       │
│    NpcBrain.ForceFlee(origin)                        │
│    NpcBrain.OverrideState(state)                     │
│                                                      │
│  Содержит:                                           │
│  ├── NpcEmotionState emotion (Calm/Alert/Fear/...)   │
│  ├── NpcMoraleData morale (courage-based)            │
│  ├── SocialTrigger[] activeTriggers                  │
│  ├── NpcIdleActivity currentIdleActivity             │
│  ├── GrudgeTable _grudges (playerId→timestamp)       │
│  └── NpcPersonalityConfig personality                │
│                                                      │
│  ┌────────────┐  ┌──────────┐  ┌───────────────┐    │
│  │ PatrolCtrl │  │ FleeCtrl │  │ AlarmCtrl     │    │
│  │ (waypoints)│  │ (escape) │  │ (vocal cues)  │    │
│  └────────────┘  └──────────┘  └───────────────┘    │
│                                                      │
│  SocialTick():                                       │
│    1. UpdateEmotion()                                │
│    2. EvaluateTriggers()                             │
│    3. ResolveActivity()                              │
│    4. PushStateToBrain()                             │
└──────────────────────────────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────┐
│           NpcGroupController.cs  (NEW)                │
│  Один на группу. Server-side.                         │
│                                                      │
│  List<NpcSocialBrain> members;                       │
│  NpcSocialBrain leader;                              │
│                                                      │
│  BroadcastAlarm(origin, radius);                     │
│  OnMemberAggrod(member, target);                     │
│  OnMemberKilled(member, killerId);                   │
│  OrderRetreat(fallbackPoint);                        │
└──────────────────────────────────────────────────────┘
```

### 1.2 Почему НЕ рефакторим NpcBrain сейчас (а 02 предлагал)

| Аргумент за рефакторинг (02) | Контраргумент (почему откладываем) |
|---|---|
| NpcBrain растёт, станет 2000+ строк | 03-add-only позволяет доставить ценность в 2× быстрее |
| Модули тестируются изолированно | NpcSocialBrain уже изолирует новую логику |
| Чистая архитектура | Рефакторинг FSM = риск сломать deck-nav + platform-carry + combat |

**Решение:** Phase 1-2 строим через NpcSocialBrain (composition). Если после Phase 2 в NpcBrain + NpcSocialBrain суммарно >1500 строк И модулей >5 — проводим рефакторинг (план есть в 02 §3.2-3.5). На практике, NpcSocialBrain забирает всю новую сложность, NpcBrain остаётся тонким FSM-ядром.

### 1.3 API, которое добавляем в NpcBrain (add-only)

```csharp
// NpcBrain.cs — добавить БЕЗ изменения существующих полей/методов:

[Header("Social Brain (T-NPC-S01)")]
[SerializeField] private bool _socialEnabled = true;

// NEW: экспозиция для NpcSocialBrain (читает aggroTarget и состояние)
public IDamageTarget CurrentAggroTarget => _aggroTarget;   // <-- поле _aggroTarget приватное, нужен геттер
public BrainState CurrentState => _state;                   // уже есть

// NEW: флаг блокировки — Tick() НЕ перезаписывает _aggroTarget,
// пока NpcSocialBrain управляет целью (см. §1.3.1).
private bool _socialOverrideLock;

// NEW public API для NpcSocialBrain:
public void ForceChaseTarget(IDamageTarget target)
{
    _aggroTarget = target;
    _socialOverrideLock = true;
    if (_deckNavActive)
    {
        // DeckNav-aware: ставим destination в proxy-агент, а не в главный NavMeshAgent
        // (главный заморожен на время platform carry).
        EnsureProxy();
        if (_proxyAgent != null)
        {
            Vector3 tgtLocal = _deckNav.WorldToDeckLocal(target.GetPosition());
            _proxyAgent.SetDestination(_deckNav.DeckLocalToNav(tgtLocal));
            _proxyAgent.isStopped = false;
        }
    }
    EnterChase();
}

public void ForceFlee(Vector3 fromPosition)
{
    _socialOverrideLock = true;
    /* реализация — Phase 1 */
}

public void OverrideIdleActivity(NpcIdleActivity activity) { /* новый метод */ }
public bool IsSocialEnabled => _socialEnabled;

// NEW: SocialTick hook — вызывается НЕ каждый Tick(), а с reduced rate (~0.5с),
// чтобы FindObjects в социальных триггерах не спамил каждый AI-тик (0.1с).
// Флаг _socialOverrideLock очищается, если NpcSocialBrain не обновил его за socialCooldown.
private void SocialTick()
{
    if (!_socialEnabled) return;
    if (_socialBrain == null) return;
    _socialBrain.Tick(this);  // делегирует компаньону
}
private NpcSocialBrain _socialBrain;

// В OnNetworkSpawn добавить:
if (_socialEnabled) _socialBrain = GetComponent<NpcSocialBrain>();
```

> **╰─ Важно:** без `_socialOverrideLock` основной Tick() (строка 517-523 NpcBrain.cs) перезатрёт `_aggroTarget`, который поставил NpcSocialBrain. Без deckNav-aware `ForceChaseTarget` сломает pursuit на движущихся кораблях — агент стоит на месте, а палуба уезжает.

---

## 2. Emotion + Personality System (03 — берём полностью)

### 2.1 NpcEmotion — 6 состояний

```csharp
public enum NpcEmotion
{
    Calm,       // базовое. Idle/Patrol.
    Alert,      // заметил угрозу. Не агрится, но насторожен.
    Fear,       // HP низкий / смерть союзника. → Flee.
    Anger,      // агрился / мстят. → Chase + damage buff.
    Despair,    // HP < 10% + outnumbered. → Surrender (Phase 3).
    Victory,    // только что убил цель. Taunt → поиск новой.
}
```

### 2.2 NpcPersonalityConfig (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "NpcPersonality_", menuName = "Project C/AI/Npc Personality")]
public class NpcPersonalityConfig : ScriptableObject
{
    [Range(0f,1f)] public float courage = 0.7f;       // 0=трус, 1=храбрец
    [Range(0f,1f)] public float aggression = 0.6f;    // 0=избегает, 1=ищет бой
    [Range(0f,1f)] public float loyalty = 0.8f;       // 0=бросит группу, 1=умрёт за группу
    [Range(0f,1f)] public float recklessness = 0.3f;  // 0=осторожен, 1=лезет на рожон
    [Range(0f,1f)] public float mercy = 0.2f;         // 0=добивает, 1=принимает сдачу
}
```

**Влияние traits на поведение:**

| Trait | Влияние |
|---|---|
| `courage` | Стартовое `morale`. Порог `Fear`: courage < 0.4 → Fear при HP<50%; courage > 0.8 → Fear только при HP<15%. |
| `aggression` | Скорость перехода Alert→Anger. При aggression > 0.7: пропускает Warning phase. |
| `loyalty` | Сила реакции на AllyKilled: loyalty > 0.7 → rage buff; loyalty < 0.3 → игнорирует. |
| `recklessness` | Готовность атаковать при Outnumbered: recklessness > 0.7 → игнорирует численное меньшинство. |
| `mercy` | Шанс принять Surrender: mercy > 0.6 → не добивает surrendered врага. |

### 2.3 Примеры personality для фракций (из 03 §6.2)

| Фракция | courage | aggression | loyalty | recklessness | mercy |
|---|---|---|---|---|---|
| Бандит-новичок | 0.4 | 0.7 | 0.3 | 0.6 | 0.1 |
| Бандит-ветеран | 0.8 | 0.8 | 0.6 | 0.4 | 0.1 |
| Гильдейский страж | 0.7 | 0.4 | 0.9 | 0.2 | 0.5 |
| Фанатик-сектант | 0.9 | 0.9 | 0.9 | 0.8 | 0.0 |
| Торговец | 0.3 | 0.2 | 0.5 | 0.1 | 0.8 |
| Пират-капитан | 0.9 | 0.8 | 0.5 | 0.6 | 0.2 |

---

## 3. NpcMoraleData — расчёт (03 §3.4, дополнен 02 §2.4)

```csharp
[System.Serializable]
public struct NpcMoraleData
{
    public float current;     // 0..1, старт = personality.courage
    public float baseValue;   // personality.courage

    // Модификаторы (пересчитываются каждый SocialTick):
    // - Наблюдение смерти союзника: -0.2 (stackable, min 0.05)
    // - Получение урона: -0.05 × (deltaHp / maxHp)
    // - Outnumbered: -0.15
    // - Смерть лидера: -0.3 (вся группа)
    // + Лидер рядом: +0.1
    // + Reinforcement рядом: +0.15
    // + Убил цель (Victory): +0.3
    // + Успешный отход: +0.1

    public bool ShouldFlee => current < (1f - personality.courage) * 0.5f;
    public bool ShouldSurrender => current < 0.15f && hpPercent < 0.15f;
    public float DamageMultiplier => Mathf.Lerp(0.5f, 1.2f, current);
    public float SpeedMultiplier => current < 0.5f ? 1.3f : 1.0f;
}
```

---

## 4. 7 Social Triggers (03 §3.2 — берём полностью)

Система приоритетов из 02 §3.3 адаптирована под триггеры 03:

| # | Триггер | Приоритет | Условие | Эффект |
|---|---|---|---|---|
| T1 | **AllyKilled** | 10 (высший) | Союзник умер в `allyDeathRadius` | Emotion → Anger/Fear; forced aggro на killer |
| T2 | **LeaderAggrod** | 9 | Лидер группы в Chase/Attack | Следовать за лидером → Chase на ту же цель |
| T3 | **AllyInCombat** | 8 | Союзник в 15м в Chase/Attack | Переход Alert → Chase на цель союзника |
| T4 | **GrudgeTrigger** | 7 | Игрок ранее атаковал этого NPC | Persistent aggro, игнорирует Warning |
| T5 | **TerritoryViolation** | 6 | Игрок вошёл в trigger-зону | Alert, потом Chase (даже для Passive) |
| T6 | **Outnumbered** | 5 (модификатор) | EnemyCount > AllyCount × 1.5 | Понижает morale; влияет на Fear/Flee |
| T7 | **ReinforcementNearby** | 4 (модификатор) | Союзники в 30м | Повышает morale; снижает flee chance |

Trigger evaluation — каждый SocialTick:

```csharp
void EvaluateTriggers()
{
    // Очистить неактивные
    _activeTriggers.Clear();

    // Проверить все 7 по порядку приоритета
    if (CheckAllyKilled(out var killerId))      // T1
        _activeTriggers.Add(new SocialTrigger(T1, killerId));
    else if (CheckLeaderAggrod(out var target))  // T2
        _activeTriggers.Add(new SocialTrigger(T2, target));
    else if (CheckAllyInCombat(out target))      // T3
        _activeTriggers.Add(new SocialTrigger(T3, target));
    // ... T4-T7

    // Применить триггер с наивысшим приоритетом
    ResolveActiveTriggers();
}
```

---

## 5. Idle Activities — 8 типов (03 §3.6 — берём полностью)

```csharp
public enum NpcIdleActivity
{
    StandStill,    // текущее поведение (default)
    Patrol,        // waypoints: Loop / PingPong / Random
    LookAround,    // head-tracking анимация
    Socialize,     // взаимодействие с другим NPC (жесты, диалог)
    Work,          // имитация работы (anim)
    Sit,           // сидит на chair/box
    Sleep,         // лежит, не реагирует на proximity
    Wander,        // случайные движения в небольшом радиусе от spawn
}
```

**Anti-restrictive:** если patrol waypoints пусты → `StandStill` (backward compat).

**Настройка через NpcSpawnerConfig:**
```csharp
public NpcIdleActivity defaultIdleActivity = NpcIdleActivity.StandStill;
public PatrolPattern patrolPattern = PatrolPattern.Loop;
public Vector3[] patrolWaypoints;  // ручная расстановка
public float idleAtWaypointSec = 3f;
public float wanderRadius = 8f;  // для Wander
```

---

## 6. Vocal Cues (03 §3.5 — берём полностью)

| Cue | Триггер | Gameplay-эффект |
|---|---|---|
| **AlertCall** | Переход Alert→Chase | Привлекает NPC в 15м (AllyInCombat) |
| **DeathScream** | HP=0 | Триггерит AllyKilled в 20м |
| **Taunt** | Только что атаковал (Victory emotion) | Дебафф цели (Phase 2) |
| **FearCry** | Fear emotion | Понижает morale союзников (−0.05) |
| **VictoryRoar** | Убил цель | Повышает morale союзников (+0.1), понижает врагам (−0.05) |

**Реализация:** Animator trigger (`SetTrigger("AlertCall")`) + NpcGroupController.Broadcast.

---

## 7. NpcGroupController (консенсус 02+03)

### 7.1 Создание групп

```csharp
// NpcSpawner — при спавне нескольких NPC в groupSpawnRadius:
public float groupSpawnRadius = 25f;
public bool assignGroupOnSpawn = true;
```

1. Spawner спавнит NPC в одном радиусе
2. Создаёт `NpcGroupController` как отдельный NetworkBehaviour
3. Назначает лидера (первый заспавненный или highest-HP)
4. Все NPC в группе получают ссылку на контроллер

### 7.2 API NpcGroupController

```csharp
public class NpcGroupController : NetworkBehaviour
{
    public List<NpcSocialBrain> members;
    public NpcSocialBrain leader;

    public int AliveCount => members.Count(m => !m.IsDead);

    // Вызывается NpcSocialBrain'ом при AllyInCombat
    public void BroadcastAlarm(NpcSocialBrain source, IDamageTarget target, float radius);

    // Вызывается при AllyKilled
    public void OnMemberKilled(NpcSocialBrain victim, ulong killerId);

    // Лидер приказывает отступить
    public void OrderRetreat(Vector3 fallbackPoint);

    // Назначить нового лидера (если старый умер)
    public void ElectNewLeader();
}
```

### 7.3 Group Tactics (Phase 2 — из 02 §2.3)

- **FormationLine**: выстроиться в линию
- **FormationFlank**: 1-2 обходят с флангов
- **FocusFire**: вся группа атакует одну цель

---

## 8. Post-Combat Behavior (03 §3.7 — берём)

После выхода из боя (aggroTarget == null, emotion == Calm):

| Состояние | Условие | Поведение |
|---|---|---|
| **Wounded** | HP < 60% после боя | Идёт к spawnPoint, не агрится 10-20 сек |
| **Heal** | HP < 40% + есть heal item | Анимация лечения, HP regen 1-5/sec |
| **CallReinforcement** | AllDead nearby + есть союзники в 50м | Бежит за подмогой |
| **ResumeActivity** | HP > 60% или timeout | Возврат к idle activity |

---

## 9. Social Roles — пресеты (из 02 §2.5.2)

Комбинируют personality + idle activity + reaction pattern:

| Role | Idle | Reaction | Flee | Personality Preset |
|---|---|---|---|---|
| **Guard** | Patrol (широкая зона) | Chase + Alarm | Никогда | courage=0.7, loyalty=0.9 |
| **Civilian** | Wander / Socialize | Flee + Alarm | HP<50% | courage=0.3, aggression=0.1 |
| **Merchant** | Sit / StandStill | Flee к guards | HP<30% | courage=0.3, mercy=0.8 |
| **Thug** | Patrol (узкая зона) | Chase + Warning | HP<15%, без союзников | courage=0.5, aggression=0.7 |
| **Leader** | StandStill (центр) | Command → группа Chase | Никогда | courage=0.8, loyalty=0.9 |

```csharp
// SocialRoleConfig.cs (NEW)
[CreateAssetMenu(fileName = "SocialRole_", menuName = "Project C/AI/Social Role")]
public class SocialRoleConfig : ScriptableObject
{
    public string roleName;
    public NpcIdleActivity defaultIdleActivity;
    public NpcPersonalityConfig personalityPreset;  // ссылка на .asset
    public bool canFlee;
    public float fleeHpThreshold;
    public bool isGuard;        // реагирует на Alarm → Chase, а не Investigate
    public bool isLeader;       // может быть лидером группы
}
```

---

## 10. Фазы реализации (объединённый план)

### Phase 1 — «Живой NPC» (P0, ~10 часов) ✅ **ВЫПОЛНЕНО** (коммиты bcc3795, 0219850)

| Тикет | Название | Источник | Оценка |
|---|---|---|---|
| **T-NPC-S01** | NpcSocialBrain (companion component) | 03 §4.2 + 02 §3 | 2 ч |
| **T-NPC-S02** | NpcBrain API: ForceChase, ForceFlee, SocialTick hook | 03 §4.2 | 1 ч |
| **T-NPC-S03** | Patrol: waypoints + Wander + LookAround | 03 §3.6 + 02 §2.2.1 | 2.5 ч |
| **T-NPC-S04** | Flee: conditions + escape-to-allies + return | 03 §3.4 + 02 §2.2.3 | 2.5 ч |
| **T-NPC-S05** | Grudge memory + GrudgeTrigger | 03 §3.2 | 1 ч |
| **T-NPC-S06** | NpcSpawnerConfig extension (новые поля) | 03 §4.3 | 0.5 ч |

**После Phase 1:** NPC ходят дозором, убегают, помнят обидчика. Минимальные изменения в NpcBrain.

### Phase 2 — «Социальная группа» (P1, ~14 часов)

| Тикет | Название | Источник | Оценка |
|---|---|---|---|
| **T-NPC-S07** | NpcEmotionState + NpcPersonalityConfig | 03 §3.1 + §6.1 | 2.5 ч |
| **T-NPC-S08** | NpcMoraleData + расчёт | 03 §3.4 + 02 §2.4.1 | 2 ч |
| **T-NPC-S09** | 7 Social Triggers evaluation | 03 §3.2 | 3 ч |
| **T-NPC-S10** | NpcGroupController (базовый: alarm + leader) | 03 §3.3 + 02 §2.3.3 | 3 ч |
| **T-NPC-S11** | Vocal Cues (AlertCall, DeathScream) | 03 §3.5 | 1.5 ч |
| **T-NPC-S12** | AllyDeath reaction: rage/fear + alarm cascade | 03 §3.2 + 02 §2.4.2 | 2 ч |

**После Phase 2:** NPC координируются, эмоционально реагируют, зовут на помощь, оплакивают союзников.

### Phase 3 — «Глубина» (P2, ~16 часов)

| Тикет | Название | Источник | Оценка |
|---|---|---|---|
| **T-NPC-S13** | ThreatAssessment: odds evaluation | 02 §2.3.1 | 2 ч |
| **T-NPC-S14** | CoverModule: поиск и использование укрытий | 02 §2.3.2 | 4 ч |
| **T-NPC-S15** | Group Tactics: formation, flanking, focus fire | 02 §2.3.3 | 3.5 ч |
| **T-NPC-S16** | Surrender: drop weapon + interaction | 03 §8.2 + 02 §2.4.3 | 3 ч |
| **T-NPC-S17** | Post-combat: Wounded/Heal/CallReinforcement | 03 §3.7 | 2 ч |
| **T-NPC-S18** | SocialRoleConfig: пресеты + инспектор | 02 §2.5.2 | 1.5 ч |

**После Phase 3:** Полноценное социальное поведение.

### Phase 4 — «Социум» (P3, post-MVP, ~14 часов)

| Тикет | Название | Источник | Оценка |
|---|---|---|---|
| **T-NPC-S19** | FactionSystem: NpcFaction SO + relations | 02 §2.5.1 | 5 ч |
| **T-NPC-S20** | VengeanceMemory: persistence между спавнами | 02 §2.5.3 | 3 ч |
| **T-NPC-S21** | Full Idle Activities: Socialize, Work, Sit, Sleep | 03 §3.6 | 3 ч |
| **T-NPC-S22** | (опционально) Рефакторинг на модули 02 §3.2 | 02 §3.2-3.5 | 3 ч |

---

## 11. NpcSpawnerConfig — финальная версия (add-only)

```csharp
// NpcSpawnerConfig.cs — добавить поля:

[Header("Social Behavior (T-NPC-S01+)")]
public bool socialEnabled = true;

[Header("Personality")]
public NpcPersonalityConfig personalityConfig;

[Header("Idle Activity")]
public NpcIdleActivity defaultIdleActivity = NpcIdleActivity.StandStill;
public PatrolPattern patrolPattern = PatrolPattern.Loop;
public Vector3[] patrolWaypoints;
public float idleAtWaypointSec = 3f;
public float wanderRadius = 8f;

[Header("Flee")]
public bool canFlee = true;
public float fleeHpThreshold = 0.25f;
public float fleeAllySeekRadius = 30f;

[Header("Alarm")]
public float alarmRadius = 15f;
public float allyDeathRadius = 20f;
public bool isGuard = false;

[Header("Group")]
public bool assignGroupOnSpawn = true;
public float groupSpawnRadius = 25f;

[Header("Memory")]
public bool enableGrudgeMemory = true;
public float grudgeDurationSec = 300f;  // 5 минут

[Header("Personality Override")]
public bool overridePersonality;       // если true — personalityConfig переопределяет префаб
```

---

## 12. Файлы (финальный список)

### Новые (~14 файлов)

```
Assets/_Project/Scripts/AI/
├── NpcBrain.cs                         (изменён: API + SocialTick hook, +30 строк)
├── NpcSocialBrain.cs                   (NEW: companion component, ~400 строк)
├── NpcEmotionState.cs                  (NEW: enum + transition logic)
├── NpcMoraleData.cs                    (NEW: struct + расчёт)
├── NpcPersonalityConfig.cs             (NEW: SO, 5 traits)
├── SocialTrigger.cs                    (NEW: enum + evaluation)
├── NpcIdleActivity.cs                  (NEW: enum + activity controller)
├── NpcVocalCue.cs                      (NEW: enum + cue dispatcher)
├── NpcGroupController.cs               (NEW: group hub, ~200 строк)
├── SocialRoleConfig.cs                 (NEW: SO, role presets)
├── GrudgeTable.cs                      (NEW: playerId→timestamp dictionary)
│
├── NpcSpawnerConfig.cs                 (изменён: новые поля, +30 строк)
│
└── Resources/AI/
    ├── NpcPersonality_BanditNovice.asset
    ├── NpcPersonality_Guard.asset
    ├── NpcPersonality_Merchant.asset
    ├── SocialRole_Guard.asset
    ├── SocialRole_Civilian.asset
    └── SocialRole_Thug.asset
```

> **╰─ Замечание по путям:** в проекте ScriptableObjects обычно лежат в `Assets/_Project/Data/AI/`. Однако `Resources/AI/` выбран осознанно — для runtime-загрузки `Resources.LoadAll<NpcPersonalityConfig>()` без ручного связывания. Если в будущем понадобится Editor-only загрузка (инспектор, превью) — использовать `Assets/_Project/Data/AI/` и алиас через `Resources` symlink. **Не-Resources SO** (например `NpcSpawnerConfig`) продолжают лежать в `Assets/_Project/Data/AI/`.

---

## 13. Anti-restrictive принципы (общие для всей системы)

1. **`socialEnabled = false`** → всё новое поведение отключается. NPC работает как сейчас.
2. **`NpcSocialBrain == null`** → NpcBrain не падает, SocialTick — no-op.
3. **Patrol waypoints пусты** → activity = StandStill (backward compat).
4. **`NpcGroupController` не назначен** → NPC действует одиночно (без ошибок).
5. **`personalityConfig == null`** → используются дефолты (courage=0.7, aggression=0.6, ...).
6. **Все новые поля в NpcSpawnerConfig опциональны** — если не заданы, спавнер не передаёт override.

---

## 14. Риски и митигация

| Риск | Оценка | Митигация |
|---|---|---|
| NpcSocialBrain + NpcBrain дублируют поиск целей | Средний | NpcSocialBrain читает `_aggroTarget` из NpcBrain, свой `FindTargets` — только для социальных триггеров |
| AllyInCombat спамит FindObjects каждый тик | Высокий | Кэш группы в NpcGroupController. Сканирование — раз в 1-2 сек, не каждый SocialTick |
| Flee ломает leash | Низкий | Flee имеет свой `fleeLeash = 80м` и timeout 15 сек |
| Vocal cues без озвучки выглядят странно | Низкий | Все cues — только animation triggers. Звук добавляется отдельно (audio-команда) |
| SocialTick на tickRate=10 (0.1с) спамит FindObjects | Высокий | SocialTick вызывать с reduced rate (каждый 5-й AI-тик ≈ 0.5с). Кэш группы в NpcGroupController для исключения FindObjects вне группы |
| Tick() перезатирает _aggroTarget после ForceChase | Высокий | Флаг `_socialOverrideLock` блокирует перезапись `_aggroTarget` в Tick(), пока NpcSocialBrain активен |
| Personality traits взаимодействуют непредсказуемо | Средний | Тесты с крайними значениями (0/1) перед PR |

---

## 15. Чек-лист перед началом реализации

- [x] `NpcBrain.cs` закоммичен (baseline) — v0.4 с API
- [x] `NpcSocialBrain` создан (~400 строк, Patrol/Flee/Grudge)
- [x] `NpcSpawnerConfig` расширен (+35 строк add-only)
- [ ] Создан 1 `.asset` `NpcPersonality_BanditNovice`
- [x] Дефолтные значения → backward compat (socialEnabled=true на префабе, false в конфиге = старый FSM)
- [x] `socialEnabled = false` → NPC ведёт себя как сейчас (SPAWN_TEST ✅)

---

## 16. Итог: что именно идёт в реализацию

**Прямо сейчас (Phase 1, ~10 ч):**

1. **NpcSocialBrain** — companion component на NPC-префабе
2. **NpcBrain API** — `ForceChase()`, `ForceFlee()`, `SocialTick` hook (add-only)
3. **Patrol** — waypoints + Wander + LookAround
4. **Flee** — HP-порог + бегство к союзникам
5. **Grudge memory** — `_grudgeTable` + persistent aggro
6. **NpcSpawnerConfig** — новые поля (add-only)

**Ключевое архитектурное решение принято:** composition-first (03), с опцией на модульный рефакторинг в Phase 4 (02).

---

*Документ создан: 2026-07-15. Синтез 02_SOCIAL_HUMAN_BEHAVIOR.md + 03_SOCIAL_HUMAN_BEHAVIOR_ANALYSIS.md.*
*Это целевой документ для реализации. Все решения мотивированы и не противоречат друг другу.*
