# 02 — Социальное поведение NPC: анализ и план расширения

> **Дата:** 2026-07-15
> **Статус:** 📝 Ресёрч + дизайн, код НЕ написан
> **Контекст:** NpcBrain (v0.3, T-NPC-01/14) реализует базовую FSM: Idle/Chase/Attack/Dead + три типа поведения (Aggressive/Passive/Neutral). Поведение NPC примитивное — NPC либо бежит за игроком, либо стоит. Нужен глубокий анализ и проектирование реалистичных паттернов для «человека социального».
> **Связанные документы:** `../70_NPC_ENEMIES.md` (базовая архитектура), `../10_DESIGN.md` (combat engine), `01_CREW_ON_MOVING_SHIP.md` (deck navigation)

---

## 1. Текущее состояние — что есть сейчас

### 1.1 NpcBrain FSM (712 строк, `Assets/_Project/Scripts/AI/NpcBrain.cs`)

```
[Idle] ──player in AggroRange──▶ [Chase] ──dist <= AttackRange──▶ [Attack]
                                      ▲                                    │
                                      └──dist > attackRange*1.3────────────┘
[Chase] ──dist > LeashRange──▶ [Idle] (return to spawnPoint)
[Any] ──HP <= 0──▶ [Dead]
```

**Текущие BrainState:** `Idle`, `Chase`, `Attack`, `Dead` — всего 4 состояния.

### 1.2 BehaviorType (T-NPC-14)

| Тип | Поведение | Где используется |
|---|---|---|
| `Aggressive` | Агро по proximity (aggroRange=10м) → Chase → Attack | Враги (гоблины, бандиты) |
| `Passive` | Стоит. Агро ТОЛЬКО после урона от игрока при: cumulativeDmg% ≥ aggroHpThreshold ИЛИ hits/60s ≥ maxHitsPerMinute | Квестовые NPC |
| `Neutral` | Никогда не агрится | Декорации |

### 1.3 Что умеет NPC сейчас (факт из кода)

| Возможность | Статус | Детали |
|---|---|---|
| **Movement** | ✅ NavMeshAgent | Server-authoritative, репликация через NetworkTransform |
| **Chase** | ✅ | `_agent.SetDestination(targetPos)` каждый тик |
| **Attack** | ✅ | `CombatServer.Instance.ResolveAttack(...)` + animator.SetTrigger("Attack") |
| **Leash (возврат)** | ✅ | При distFromSpawn > leashRange → возврат через `_agent.SetDestination(_spawnPoint)` |
| **Death + Loot** | ✅ | Death animation + NpcLootPickup (credits) + Destroy(3s) |
| **Deck navigation** | ✅ | Прокси-агент для движущихся палуб корабля |
| **Platform carry** | ✅ | Parent к кораблю + carry формулы |
| **Visual override** | ✅ | NpcVisualConfig через спавнер |
| **Spawn per-chunk** | ✅ | Интеграция с ChunkLoader (T-NPC-09) |
| **Passive aggro** | ✅ | По cumulative damage или hits/minute |

### 1.4 Чего НЕТ (критический разрыв для «социального человека»)

| Отсутствует | Почему это важно |
|---|---|
| **Patrol** | NPC стоит на spawnPoint как столб. Нет движения по территории. |
| **Flee** | NPC никогда не убегает, даже при 5% HP против 3 игроков. |
| **Alert/Investigate** | Нет реакции на звуки боя, крики союзников. Только прямой line-of-sight. |
| **Групповая координация** | Каждый NPC действует изолированно. Атакуют вразнобой, не помогают друг другу. |
| **Cover-seeking** | Под огнём — стоит и принимает урон. |
| **Threat assessment** | Не оценивает odds перед атакой. Бежит 1v5. |
| **Surrender** | Дерётся до смерти, даже при 1 HP. |
| **Реакция на смерть союзника** | Стоит рядом с трупом, никак не реагирует. |
| **Звуковое обнаружение** | Нет hearing radius. Только визуальный aggroRange. |
| **Faction awareness** | Все NPC видят только Player. Не различают friend/foe среди NPC. |
| **Эмоциональное состояние** | Нет страха, ярости, морали. Всегда одинаковое поведение. |
| **Территориальное предупреждение** | Атакует молча. Нет «Эй, стой!» перед атакой. |
| **Дневной/ночной цикл** | Поведение одинаково в любое время. |

---

## 2. Ресёрч: реалистичные паттерны для «человека социального»

### 2.1 Модель: четыре слоя поведения

Поведение социального человека раскладывается на 4 слоя, от базовых инстинктов до сложной социальной динамики:

```
┌─────────────────────────────────────────────────┐
│  Layer 4: СОЦИАЛЬНАЯ ДИНАМИКА                   │
│  Faction, reputation, vengeance memory,          │
│  social hierarchy (leader/follower)              │
├─────────────────────────────────────────────────┤
│  Layer 3: ЭМОЦИИ / МОРАЛЬ                       │
│  Fear, confidence, morale, surrender threshold,  │
│  reaction to ally death                          │
├─────────────────────────────────────────────────┤
│  Layer 2: ТАКТИКА                                │
│  Threat assessment, cover usage, flanking,       │
│  tactical retreat, group formation               │
├─────────────────────────────────────────────────┤
│  Layer 1: БАЗОВЫЕ РЕФЛЕКСЫ                      │
│  Patrol, investigate, alert, flee, hide,         │
│  call for help, territory warning                │
└─────────────────────────────────────────────────┘
```

### 2.2 Layer 1: Базовые рефлексы (MVP расширения)

#### 2.2.1 Patrol (дозор)

NPC ходит по waypoints с вариациями:
- **Loop**: A→B→C→A (циклический обход)
- **PingPong**: A→B→C→B→A (туда-обратно)
- **Random**: случайный выбор следующей точки из пула
- **IdleIntervals**: остановки на N секунд в каждой точке (смотрит по сторонам)

**Параметры:** `patrolSpeed` (медленнее chase), `idleAtWaypointSec`, `waypointRadius` (допуск).

#### 2.2.2 Investigate (расследование)

При обнаружении подозрительного (звук, крик, труп):
1. **Alert** — остановка, поворот к источнику
2. **Approach** — осторожное приближение (медленная скорость, scanCone 60°)
3. **Investigate** — подойти к источнику, задержаться на 3-5 сек
4. **Return** — если ничего не найдено → возврат к патрулю

**Параметры:** `investigateSpeed`, `investigateDuration`, `investigateRange`.

#### 2.2.3 Flee (бегство)

Условия (любое из):
- HP < fleeHpThreshold (default 25%)
- alliesAlive < enemiesVisible (численное меньшинство)
- allyDiedNearby (смерть союзника в радиусе fleeOnAllyDeathRadius)
- morale < fleeMoraleThreshold

**Куда бежать:**
1. К ближайшим союзникам (seekAllies)
2. К fortifiedPosition (точка сбора, заданная дизайнером)
3. AwayFromDanger (наивный вектор «от угрозы»)

#### 2.2.4 Call for Help / Alarm

При обнаружении врага:
- Shout → все NPC своей фракции в радиусе `alarmRadius` переходят в Investigate
- Если NPC — «стражник» (guard role) → сразу Chase на источник тревоги
- Alarm propagation: затухает по расстоянию, не бесконечная цепочка

**Параметры:** `alarmRadius`, `alarmCooldown`, `guardRole` (bool).

#### 2.2.5 Territorial Warning

Перед атакой:
1. Если игрок на `warningDistance` (дальше attackRange, ближе aggroRange) → shout/gesture
2. Если игрок продолжает приближаться → Attack
3. Длительность warning: `warningDuration` (1-3 сек)
4. Не применяется если NPC уже в бою

### 2.3 Layer 2: Тактика (среднесрочное расширение)

#### 2.3.1 Threat Assessment

Перед входом в Chase NPC оценивает:
```
threatScore = Σ(enemyStrength) / Σ(allyStrength)
```
- `threatScore < 0.5`: уверен → Chase
- `0.5 ≤ threatScore < 1.5`: осторожен → Investigate сначала, потом Chase
- `threatScore ≥ 1.5`: боится → Flee или CallForHelp

**EnemyStrength** = f(level, HP%, visibleCount)
**AllyStrength** = all faction-mates in `coordinationRadius`

#### 2.3.2 Cover System

Под огнём (ranged атака):
1. Ищет ближайшую cover point в радиусе `coverSeekRadius`
2. Бежит к ней (sprint)
3. Выглядывает / стреляет из-за укрытия
4. Меняет укрытие если текущая позиция под обстрелом > N сек

**Cover points:** могут быть:
- Автоматические (raycast-Wall detection)
- Ручные (дизайнер расставляет `CoverPoint` маркеры)

#### 2.3.3 Group Formation

При наличии ≥3 союзников одной фракции в `formationRadius`:
- **FormationLine**: выстроиться в линию
- **FormationCircle**: окружить цель
- **FormationFlank**: 1-2 обходят с флангов, остальные front

**Реализация:** NpcGroupCoordinator (общий компонент на группу, server-side).

### 2.4 Layer 3: Эмоции / Мораль (позднее расширение)

#### 2.4.1 Morale State Machine

```
[Confident] ──allyDied──▶ [Shaken] ──allyDied──▶ [Panicked]
     ▲                        │                      │
     │  ──enemyDied──         │  ──timeout──         │  ──timeout──
     └────────────────────────┘                      │
                                                     ▼
                                              [Flee / Surrender]
```

- **Confident**: обычное поведение
- **Shaken**: attackSpeed −15%, чаще проверяет threatAssessment
- **Panicked**: только Flee или Surrender

#### 2.4.2 Реакция на смерть союзника

| Отношение к погибшему | Реакция |
|---|---|
| **Friend** (тот же squad) | Shaken + shout alarm + возможен rage-buff (+20% dmg на 10s) |
| **Ally** (та же faction) | Shaken |
| **Leader** (squad leader) | Panic + mass flee для всей группы |
| **Stranger** (другая faction) | Нет реакции |

#### 2.4.3 Surrender

Условия: HP < surrenderHpThreshold (default 10%) И нет союзников в радиусе 20м.

Поведение:
1. Drop weapon (визуально: переход в surrender anim)
2. Отключение агро
3. Переход в состояние `Surrendered` (не атакует, можно взаимодействовать)
4. Через N секунд: либо игрок убивает, либо NPC убегает

### 2.5 Layer 4: Социальная динамика (будущее)

#### 2.5.1 Faction System

```csharp
public enum FactionRelation { Allied, Neutral, Hostile }

public class NpcFaction : ScriptableObject {
    public string factionId;          // "bandits", "guards", "villagers"
    public Dictionary<string, FactionRelation> relations;
}
```

- NPC одной фракции помогают друг другу
- Отношения фракций задаются дизайнером
- Возможна смена отношений во время игры

#### 2.5.2 Social Roles

| Role | Патруль | Реакция на врага | Flee/Surrender |
|---|---|---|---|
| **Guard** | Активный, широкая зона | Chase + Alarm | Никогда не flee |
| **Civilian** | Минимальный, узкая зона | Flee + Alarm | При 50% HP |
| **Merchant** | Стоит у лавки | Flee к guards | При 30% HP |
| **Thug** | Территориальный, агрессивный | Chase + Warning | При 15% HP, без союзников |
| **Leader** | Центральная позиция | Command → союзники Chase | Никогда, но surrender возможен |

#### 2.5.3 Vengeance Memory

NPC запоминает игрока, убившего его союзника:
- `vengeanceTargetId` хранится N минут
- При повторной встрече → мгновенный Aggro (игнорирует warning phase)
- При убийстве vengeance-target → buff всей группе («отомстили!»)

---

## 3. Архитектурное решение: как это встроить в код

### 3.1 Проблема текущей архитектуры

`NpcBrain.cs` уже 712 строк. Если добавлять patrol, flee, investigate, cover, morale прямо в него — получится 2000+ строк монолита. Это:

- Трудно тестировать
- Трудно расширять аддитивно
- Сложно комбинировать (например: civilian с patrol, но без cover)

### 3.2 Решение: Modular Behavior Architecture

Разделить на два уровня:

```
NpcBrain (core FSM, ~300 строк после рефакторинга)
  ├── управляет: BrainState transition, NavMeshAgent, Animator
  ├── принимает решения: от BehaviorModules
  └── НЕ содержит конкретные поведения

NpcBehaviorModule (abstract base, подключается как компонент)
  ├── PatrolModule
  ├── InvestigateModule
  ├── FleeModule
  ├── AlarmModule
  ├── ThreatAssessmentModule
  ├── CoverModule
  ├── MoraleModule
  └── SocialRoleModule
```

**Принцип:** каждый модуль — независимый `MonoBehaviour`, который:
1. Читает состояние мира (сенсоры)
2. Предлагает `BehaviorDecision` в NpcBrain
3. NpcBrain агрегирует решения (приоритеты) → переход в новое BrainState

### 3.3 Flow принятия решения (каждый тик)

```
┌─ Tick ─────────────────────────────────────────┐
│                                                 │
│  ┌─────────────┐   ┌──────────────┐            │
│  │ MoraleModule│   │ ThreatModule │  ...       │
│  └──────┬──────┘   └──────┬───────┘            │
│         │ decision        │ decision            │
│         ▼                 ▼                    │
│  ┌──────────────────────────────┐              │
│  │      NpcBrain.Arbitrate()    │              │
│  │  priority:                   │              │
│  │   1. Dead (highest)          │              │
│  │   2. Flee (morale)           │              │
│  │   3. Attack (in range)       │              │
│  │   4. Chase (aggro)           │              │
│  │   5. Investigate (alarm)     │              │
│  │   6. Patrol                  │              │
│  │   7. Idle (lowest)           │              │
│  └──────────────┬───────────────┘              │
│                 ▼                               │
│         Execute(state)                         │
└─────────────────────────────────────────────────┘
```

### 3.4 Интерфейс BehaviorModule

```csharp
public abstract class NpcBehaviorModule : MonoBehaviour
{
    // Вызывается NpcBrain при OnNetworkSpawn
    public virtual void Initialize(NpcBrain brain) { }

    // Приоритет: выше = важнее. 0 = не предлагает решение.
    public abstract int Priority { get; }

    // Возвращает решение или null если нечего предложить.
    // NpcBrain вызывает у ВСЕХ модулей, затем выбирает с наивысшим приоритетом.
    public abstract BehaviorDecision Evaluate(NpcSensorData sensorData);
}

public struct BehaviorDecision
{
    public BrainState targetState;
    public Vector3? moveTarget;      // куда идти (для Chase/Patrol/Flee)
    public IDamageTarget aggroTarget; // кого атаковать
    public float speedMultiplier;    // 1.0 = обычная скорость
    public string reason;            // для дебага: "[FleeModule] HP below 25%"
}

public struct NpcSensorData
{
    public Vector3 position;
    public Vector3 spawnPoint;
    public BrainState currentState;
    public BehaviorType behaviorType;
    public float hpPercent;
    public List<IDamageTarget> visibleTargets;   // в aggroRange
    public List<NpcBrain> nearbyAllies;           // в coordinationRadius
    public List<NpcBrain> nearbyDeadAllies;       // трупы союзников
    public bool alarmReceived;                    // флаг от AlarmModule
    public Vector3? alarmOrigin;                  // источник тревоги
    public bool underRangedFire;                  // под обстрелом
    public int allyCount;
    public int enemyCount;
    public float moraleLevel;                     // от MoraleModule
}
```

### 3.5 Состав NpcBrain после рефакторинга

```csharp
[RequireComponent(typeof(NavMeshAgent))]
public class NpcBrain : NetworkBehaviour
{
    // --- core ---
    private NavMeshAgent _agent;
    private Animator _animator;
    private BrainState _state;
    private Vector3 _spawnPoint;

    // --- modules (auto-discovered) ---
    private List<NpcBehaviorModule> _modules = new();

    // --- sensors (shared data for modules) ---
    private NpcSensorData _sensorData;

    // --- deck/platform carry (без изменений) ---

    void OnNetworkSpawn()
    {
        // Сбор модулей
        GetComponents(_modules);
        foreach (var m in _modules) m.Initialize(this);
    }

    void Tick()
    {
        // 1) Собрать сенсорные данные
        GatherSensorData();

        // 2) Запросить решения у всех модулей
        BehaviorDecision? best = null;
        foreach (var m in _modules)
        {
            var d = m.Evaluate(_sensorData);
            if (d.targetState != _state && (best == null || m.Priority > best.Value.priority))
                best = d;
        }

        // 3) Применить решение
        if (best.HasValue)
            TransitionTo(best.Value);
    }
}
```

### 3.6 Альтернатива (без рефакторинга NpcBrain)

Если рефакторинг NpcBrain сейчас нежелателен, модули можно добавить **аддитивно**:

- `NpcBrain` остаётся как есть
- Новые компоненты (`PatrolBehaviour`, `FleeBehaviour`, `AlarmBehaviour`) подписываются на NpcBrain через события или прямые вызовы
- NpcBrain выставляет API: `ForceChase(target)`, `ForceFlee(target)`, `OverrideState(state)`

**Плюсы:** 0 изменений в NpcBrain.
**Минусы:** race conditions между модулями, труднее дебажить, NpcBrain продолжит расти.

**Рекомендация:** рефакторинг NpcBrain под модули. Он уже 712 строк, дальше будет хуже.

---

## 4. Фазы внедрения (план)

### Phase 1: Базовые рефлексы (T-NPC-20..23, ~12-16 часов)

| Тикет | Название | Что делает | Оценка |
|---|---|---|---|
| **T-NPC-20** | Модульная архитектура | Рефакторинг NpcBrain: вынос FSM в core, BehaviorModule interface, NpcSensorData | 4-5 ч |
| **T-NPC-21** | PatrolModule | Waypoint-based patrol: Loop/PingPong/Random, idle intervals | 3-4 ч |
| **T-NPC-22** | FleeModule | Бегство при низком HP + поиск союзников/убежища | 3-4 ч |
| **T-NPC-23** | AlarmModule | Alarm propagation + Investigate state | 2-3 ч |

**После Phase 1:** NPC ходят дозором, убегают при низком HP, поднимают тревогу.

### Phase 2: Тактика (T-NPC-24..26, ~10-14 часов)

| Тикет | Название | Что делает | Оценка |
|---|---|---|---|
| **T-NPC-24** | ThreatAssessmentModule | Оценка odds перед боем, influence на Chase/Flee | 3-4 ч |
| **T-NPC-25** | CoverModule | Поиск укрытий, движение между ними под обстрелом | 4-5 ч |
| **T-NPC-26** | GroupCoordinator | Group formation, flanking, shared aggro target | 3-5 ч |

**После Phase 2:** NPC оценивают силы, прячутся за укрытия, координируются в группе.

### Phase 3: Эмоции и Мораль (T-NPC-27..29, ~8-12 часов)

| Тикет | Название | Что делает | Оценка |
|---|---|---|---|
| **T-NPC-27** | MoraleModule | Morale FSM (Confident/Shaken/Panicked), модификаторы к решениям | 3-4 ч |
| **T-NPC-28** | AllyDeathReaction | Реакция на смерть союзника: fear/rage, alarm cascade | 2-3 ч |
| **T-NPC-29** | SurrenderModule | Drop weapon + surrender state при критическом HP без союзников | 3-5 ч |

**После Phase 3:** NPC эмоционально реагируют, могут сдаться, мораль влияет на бой.

### Phase 4: Социальная динамика (T-NPC-30..32, ~14-20 часов, post-MVP)

| Тикет | Название | Что делает | Оценка |
|---|---|---|---|
| **T-NPC-30** | FactionSystem | NpcFaction SO, faction relations, friendly-fire awareness | 5-7 ч |
| **T-NPC-31** | SocialRoles | Guard/Civilian/Merchant/Thug/Leader — разные профили поведения | 4-6 ч |
| **T-NPC-32** | VengeanceMemory | Запоминание убийцы союзника, vengeance buff/debuff | 5-7 ч |

**После Phase 4:** Полноценное социальное поведение: фракции, роли, вендетта.

---

## 5. Приоритеты и MVP

### Минимальный набор для «социального человека» (Phase 1 + выборочно Phase 2)

| Приоритет | Что | Почему |
|---|---|---|
| **P0** | PatrolModule | NPC перестают быть статичными столбами |
| **P0** | FleeModule | Самосохранение — базовый инстинкт |
| **P1** | AlarmModule | «Позвать на помощь» — ключевое социальное поведение |
| **P1** | ThreatAssessmentModule | Не бежать 1v5 — рациональность |
| **P2** | GroupCoordinator | Групповые формации, фланги |
| **P2** | MoraleModule | Эмоциональная глубина |

### Что НЕ входит в MVP (оставляем на будущее)

- CoverModule (сложная геометрия, требует CoverPoint-разметки)
- SurrenderModule (требует surrender-анимаций и interaction-системы)
- FactionSystem (полноценная factions — отдельная подсистема)
- SocialRoles (надстройка над FactionSystem)
- VengeanceMemory (требует persistence между спавнами)

---

## 6. Новые BrainState (после расширения)

Текущие 4 состояния расширяются до 8:

```csharp
public enum BrainState
{
    Idle,           // стоит на месте (fallback)
    Patrol,         // движется по waypoints (NEW)
    Investigate,    // идёт к источнику тревоги (NEW)
    Alert,          // остановился, смотрит на угрозу, warning-shout (NEW)
    Chase,          // преследование цели
    Attack,         // атака вблизи
    Flee,           // бегство к союзникам / от опасности (NEW)
    Surrendered,    // сдался, не атакует (NEW, Phase 3)
    Dead,           // мёртв
}
```

Transition map (упрощённо):

```
[Idle] ──patrol waypoints──▶ [Patrol] ──enemy detected──▶ [Alert] ──warning timeout──▶ [Chase]
[Patrol] ──alarm received──▶ [Investigate] ──enemy found──▶ [Alert]
[Alert] ──threatAssessment:hopeless──▶ [Flee]
[Chase] ──dist≤attackRange──▶ [Attack]
[Attack] ──dist>attackRange──▶ [Chase]
[Attack] ──HP<fleeThreshold──▶ [Flee]
[Flee] ──safe distance──▶ [Idle] (или возврат в [Patrol])
[Any] ──HP≤0──▶ [Dead]
```

---

## 7. NpcSensorData — источники данных

### 7.1 Vision (существующий + расширенный)

Сейчас: `FindNearestPlayerTarget(aggroRange)` — только players, только по дистанции.

Нужно:
- **Faction-filtered**: видеть NPC других фракций как потенциальные цели
- **Cone-based**: периферийное зрение (180°+) vs фокусное (60°)
- **Occlusion**: line-of-sight через `Physics.Raycast`

### 7.2 Hearing (новый)

```csharp
[Header("Hearing")]
public float hearingRadius = 25f;
public LayerMask hearingOcclusionMask;  // стены блокируют звук

// События, которые NPC «слышит»:
// - AttackSound (combat nearby)
// - DeathSound (ally/enemy died)
// - AlarmShout (ally called for help)
```

Реализация: NpcBrain подписывается на WorldEvent (AttackLanded, EntityKilled) и фильтрует по hearingRadius.

### 7.3 Ally detection (новый)

```csharp
// NpcBrain кэширует nearby allies в _sensorData.nearbyAllies
// Метод: FindObjectsByType<NpcBrain> + faction filter + distance check
// Частота обновления: не каждый тик, а раз в 1-2 сек (дорого)
```

---

## 8. Конфигурация через NpcSpawnerConfig (расширение)

Добавить в `NpcSpawnerConfig`:

```csharp
[Header("Social Role (T-NPC-31)")]
public NpcSocialRole socialRole = NpcSocialRole.Thug;

[Header("Patrol (T-NPC-21)")]
public bool enablePatrol = true;
public PatrolPattern patrolPattern = PatrolPattern.Loop;
public Transform[] patrolWaypoints;  // ручная расстановка

[Header("Flee (T-NPC-22)")]
public float fleeHpThreshold = 0.25f;     // 25% HP
public float fleeOnAllyDeathRadius = 15f;
public bool canFlee = true;

[Header("Alarm (T-NPC-23)")]
public float alarmRadius = 30f;
public bool isGuard = false;  // guards → Chase вместо Investigate

[Header("Morale (T-NPC-27)")]
public float baseMorale = 1.0f;         // 0=трус, 1=норма, 2=берсерк
public float moraleLossOnAllyDeath = 0.3f;
public float moraleRegenPerSecond = 0.02f;

[Header("Faction (T-NPC-30)")]
public string factionId;  // ссылка на NpcFaction asset
```

---

## 9. Файлы, которые появятся

### Новые (~12 файлов)

```
Assets/_Project/Scripts/AI/
├── Modules/
│   ├── NpcBehaviorModule.cs          (abstract base)
│   ├── NpcSensorData.cs              (struct)
│   ├── PatrolModule.cs               (T-NPC-21)
│   ├── FleeModule.cs                 (T-NPC-22)
│   ├── AlarmModule.cs                (T-NPC-23)
│   ├── ThreatAssessmentModule.cs     (T-NPC-24)
│   ├── CoverModule.cs                (T-NPC-25)
│   ├── GroupCoordinator.cs           (T-NPC-26)
│   ├── MoraleModule.cs               (T-NPC-27)
│   └── SocialRoleConfig.cs           (SO, T-NPC-31)
├── Faction/
│   ├── NpcFaction.cs                 (SO, T-NPC-30)
│   └── FactionRelation.cs            (enum)
└── NpcBrain.cs                       (рефакторинг)
```

### Изменённые

- `Assets/_Project/Scripts/AI/NpcBrain.cs` — рефакторинг под модули
- `Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs` — новые поля конфигурации

---

## 10. Открытые вопросы

### Q1: Модули как компоненты или ScriptableObject?

**Вариант A (компоненты):** каждый модуль — MonoBehaviour на том же GO.
- ✅ Простота: `GetComponents<NpcBehaviorModule>()`
- ✅ Инспектор: видно какие модули назначены
- ❌ Много компонентов на одном GO

**Вариант B (ScriptableObject):** модули как `.asset` файлы.
- ✅ Можно переиспользовать между префабами
- ❌ Сложнее с `GetComponent`, нужна инъекция

**Рекомендация:** компоненты (вариант A). NpcBrain и так требует те же компоненты (NavMeshAgent, NetworkObject). 5-7 дополнительных компонентов — не проблема.

### Q2: Как NpcBrain выбирает между конфликтующими решениями?

Приоритетная система (см. §3.3). Модуль с наивысшим `Priority` побеждает. Dead = infinity, Flee > Attack, и т.д.

### Q3: GroupCoordinator — на каждом NPC или отдельный групповой объект?

Отдельный `GroupCoordinator : NetworkBehaviour` на leader-NPC (или выделенный GO). Он назначает цели группе. Групповые NPC подчиняются через `GroupCoordinator.GetCommand(npcId)`.

### Q4: Нужен ли Hearing system сейчас?

Не для MVP Phase 1. Но AlarmModule (§2.2.4) требует хотя бы базового hearing. Можно упростить: alarm идёт через прямое оповещение (NpcBrain → NpcBrain в радиусе), без симуляции звука.

### Q5: Анимации для новых состояний?

- **Patrol**: использует существующий `Speed` параметр (walk speed)
- **Investigate**: walk speed + развороты к источнику
- **Flee**: run speed (выше чем chase)
- **Alert**: idle pose + поворот к угрозе
- **Surrender**: требует новый surrender idle clip (Phase 3)

Ничего блокирующего для Phase 1-2.

---

## 11. Заключение

Текущая система NPC-поведения — это **боевой автомат**: вижу → бегу → бью → умираю. Для «человека социального» этого недостаточно на 3-х уровнях:

1. **Индивидуальный**: нет самосохранения, нет дозора, нет любопытства
2. **Групповой**: нет координации, нет взаимопомощи, нет тревоги
3. **Эмоциональный**: нет страха, нет ярости, нет сдачи в плен

**Ключевое архитектурное решение** — модульная система NpcBehaviorModule, где каждый аспект поведения (патруль, бегство, тревога, тактика, мораль) выносится в отдельный компонент, а NpcBrain выполняет роль координатора-арбитра.

**MVP (Phase 1)**: Patrol + Flee + Alarm + модульная архитектура. ~12-16 часов. Это даёт немедленный качественный скачок в восприятии NPC — они перестанут быть «столбами» и начнут вести себя как живые существа: ходить, бояться, звать на помощь.
