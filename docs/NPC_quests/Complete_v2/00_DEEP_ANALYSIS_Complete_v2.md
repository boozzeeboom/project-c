# 🧬 Complete v2 NPC — Глубокий Анализ

> **Дата:** 2026-07-29
> **Цель:** Синтез квестовой NPC-системы и AI-боевой системы для создания полноценного пассивного NPC с диалогом, квестами, боевым ответом, репутацией и респавном.

---

## 1. ДВА МИРА: Что есть сейчас

Проект содержит **две независимые NPC-системы**, которые никак не связаны:

### 1.1 Мир A — AI / Combat NPC (спавн-враги)

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `NpcBrain` | `AI/NpcBrain.cs` | FSM: Idle → Chase → Attack → Dead. `BehaviorType`: Aggressive / Passive / Neutral |
| `NpcSocialBrain` | `AI/NpcSocialBrain.cs` | Социальное поведение: Patrol, Flee, Grudge, Emotion, VocalCues, Group |
| `NpcAttacker` | `Combat/Implementations/NpcAttacker.cs` | `IAttacker` — наносит урон через CombatServer |
| `NpcTarget` | `Combat/Implementations/NpcTarget.cs` | `IDamageTarget` — принимает урон, HP, смерть, лут |
| `NpcSpawner` | `AI/NpcSpawner.cs` | Server-side спавнер: радиус, лимиты, чанки, циклы (Finite/FiniteCycle/Infinite) |
| `NpcSpawnerConfig` | `AI/NpcSpawnerConfig.cs` | SO: 10 foldout-групп — всё от спавна до faction/loot |
| `NpcFaction` (AI) | `AI/NpcFaction.cs` | SO: factionId + отношения (Hostile/Neutral/Allied) — **НЕ связано** с квестами |
| `GrudgeTable` | `AI/GrudgeTable.cs` | Память обидчиков: playerId → timestamp |
| `NpcEmotion` | `AI/NpcEmotion.cs` | 6 состояний: Calm/Alert/Fear/Anger/Despair/Victory |
| `NpcPersonalityConfig` | `AI/NpcPersonalityConfig.cs` | 5 traits: courage/aggression/loyalty/recklessness/mercy |
| `NpcGroupController` | `AI/NpcGroupController.cs` | Групповая координация: alarm, leader election |

**Префаб:** `SPAWN_TEST.prefab` → NetworkObject + NavMeshSurface + NpcSpawner.

**Поведение Passive (T-NPC-14):** NPC с `BehaviorType.Passive` стоит мирно, атакует только после удара игрока при:
- cumulativeDamage% ≥ aggroHpThreshold (default 25%)
- ИЛИ hits за 60с ≥ maxHitsPerMinute (default 3)

**Поведение Neutral:** Никогда не атакует.

### 1.2 Мир B — Quest / Dialogue NPC (квестодатели)

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `NpcController` | `Quests/NpcController.cs` | **MonoBehaviour** (НЕ NetworkBehaviour). Trigger collider, NpcDefinition ref, E-key interaction |
| `NpcDefinition` | `Quests/Npcs/NpcDefinition.cs` | SO: npcId, displayName, faction (FactionId), portrait, prefab, questOffers, questTurnIns, attitudeLinks |
| `QuestServer` | `Quests/Network/QuestServer.cs` | **NetworkBehaviour** — server hub: RPCs, FireDialogAction, snapshots |
| `QuestWorld` | `Quests/Core/QuestWorld.cs` | POCO singleton: quest state, reputation, npcAttitude, persistence |
| `DialogTree` | `Quests/Dialogue/DialogTree.cs` | SO: граф диалога с условиями и действиями |
| `FactionId` | `Quests/Factions/FactionId.cs` | enum: 12 lore-значений (GuildOfThoughts, Pirates, Neutral...) |
| `NpcAttitude` | `Quests/Factions/NpcAttitude.cs` | struct: per-player, per-NPC personal relationship (-100..+200) |
| `ReputationClientState` | `Reputation/` | Client singleton для faction reputation |
| `NpcAttitudeClientState` | `Reputation/NpcAttitudeClientState.cs` | Client singleton для per-NPC attitude |
| `QuestClientState` | `Quests/Client/QuestClientState.cs` | Client singleton: snapshot, dialog step, toast events |

**Пример:** [Mira] в `WorldScene_0_0` — `NpcController` + `CapsuleCollider` (isTrigger) + `NpcDefinition` (mira_01, faction=GuildOfThoughts). **НЕТ** NpcBrain, NpcTarget, NpcAttacker.

### 1.3 Ключевое различие

| Характеристика | AI NPC (враги) | Quest NPC (квестодатели) |
|----------------|----------------|-------------------------|
| Базовый класс | `NetworkBehaviour` | `MonoBehaviour` |
| Спавн | Динамический (NpcSpawner) | Scene-placed (ручная расстановка) |
| AI/FSM | NpcBrain + NpcSocialBrain | Нет |
| Combat | NpcAttacker + NpcTarget | Нет |
| Диалоги/Квесты | Нет | Да (через QuestServer) |
| Репутация | GrudgeTable (временная) | NpcAttitude (персистентная, -100..+200) |
| Респавн | Через NpcSpawner (циклы) | Нет |
| Фракции | NpcFaction SO (AI) | FactionId enum (Quests) |

---

## 2. АРХИТЕКТУРНЫЙ РАЗРЫВ

### 2.1 Точки расхождения

```
┌──────────────────────────────────────────────────────────────────┐
│                      AI NPC (Combat World)                       │
│                                                                  │
│  NpcSpawner ──► NpcBrain (FSM) ──► NpcAttacker ──► CombatServer │
│                  │   ├─ BehaviorType                             │
│                  │   └─ GrudgeTable                              │
│                  └─► NpcSocialBrain                              │
│                       ├─ NpcEmotion                              │
│                       ├─ NpcFaction (AI)                         │
│                       └─ PersonalityConfig                       │
│                                                                  │
│  ❌ НЕТ связи с QuestServer/NpcAttitude/FactionId               │
│  ❌ НЕТ диалогов/квестов                                        │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                    Quest NPC (Story World)                        │
│                                                                  │
│  NpcController ──► E-key ──► QuestServer ──► QuestWorld         │
│  (MonoBehaviour)              ├─ DialogTree                     │
│                               ├─ QuestDefinition                 │
│                               ├─ FactionId (lore)                │
│                               ├─ NpcAttitude (-100..+200)        │
│                               └─ Reputation                      │
│                                                                  │
│  ❌ НЕТ NpcBrain/NpcTarget/NpcAttacker                          │
│  ❌ НЕ может получать урон                                      │
│  ❌ НЕТ респавна                                                │
└──────────────────────────────────────────────────────────────────┘
```

### 2.2 Дублирование концепций

| Концепция | AI-система | Quest-система | Проблема |
|-----------|-----------|---------------|----------|
| **Фракции** | `NpcFaction` SO (AI) | `FactionId` enum (Quests) | Две разные системы, несовместимы |
| **Память обид** | `GrudgeTable` (временная, до 300с) | `NpcAttitude` (персистентная) | Grudge не влияет на NpcAttitude |
| **«Свой/чужой»** | `NpcFaction.GetRelation(other)` | `QuestWorld.GetReputation` | AI не читает квестовую репутацию |
| **Агрессия** | `BehaviorType.Passive/Aggressive` | Нет | Квестовые NPC не имеют behaviour |

---

## 3. ЦЕЛЕВОЙ ПАТТЕРН (Complete v2 NPC)

Требуемый паттерн от пользователя:

```
┌─────────────────────────────────────────────────────────────────┐
│                   Complete v2 NPC (единый)                       │
│                                                                  │
│  [Стоит в мире] ── E ──► Диалог + Квесты                       │
│  [Атакован] ──► Отвечает (Passive → Aggressive после порога)    │
│  [Убит] ──► Респавн через N сек / событие / никогда             │
│  [Атака/Убийство] ──► NpcAttitude -N (портится отношение)       │
│  [NpcAttitude < порог] ──► Становится Hostile (агрессивный)     │
│  [Cross-faction] ──► Страдает репутация связанных фракций       │
└─────────────────────────────────────────────────────────────────┘
```

### 3.1 Разбор паттерна по слоям

| Слой | Что нужно | Где взять (база) | Что добавить |
|------|-----------|-----------------|--------------|
| **Диалог/Квесты** | E-key, DialogTree, QuestDefinition | ✅ Готово (QuestServer + NpcController) | — |
| **Принять урон** | IDamageTarget, HP, смерть | ✅ NpcTarget (AI) | Повесить на квестового NPC |
| **Ответная атака** | BehaviorType.Passive → агрится после удара | ✅ NpcBrain.Passive (T-NPC-14) | Подключить к квестовому NPC |
| **Репутация при атаке** | NpcAttitude -delta при получении урона | ✅ QuestWorld.ModifyNpcAttitude | Хук: NpcTarget.OnHpChanged → QuestWorld |
| **Репутация при убийстве** | NpcAttitude -N при смерти NPC | ✅ QuestWorld.ModifyNpcAttitude | Хук: NpcTarget.OnKilled → QuestWorld |
| **Cross-faction** | attitudeLinks (NPC→фракция) | ✅ NpcDefinition.attitudeLinks | Уже работает в ModifyNpcAttitude |
| **Hostile-порог** | NpcAttitude < X → BehaviorType.Aggressive | ⚠️ Частично | Нужен мост: QuestWorld → NpcBrain |
| **Респавн** | Таймер / событие / никогда | ✅ NpcSpawner (FiniteCycle + SpawnRestartTrigger) | Адаптировать под scene-placed NPC |
| **Визуал** | Модель + анимации | ⚠️ Cube placeholder | Заменить на HumanM_Model |

---

## 4. ЧТО У НАС ЕСТЬ (инвентаризация работающих систем)

### 4.1 ✅ Полностью работает

| Система | Статус | Комментарий |
|---------|--------|-------------|
| **E-key → диалог** | ✅ | NpcController + QuestServer.RequestTalkToNpcRpc |
| **DialogWindow** | ✅ | UI Toolkit: typewriter, F-skip, опции, портрет, NpcAttitude badge |
| **DialogTree + условия** | ✅ | DialogueCondition: HasItem, ReputationAtLeast, NpcAttitudeAtLeast, QuestStateEquals... |
| **Квесты (accept/complete)** | ✅ | QuestWorld.TryAccept/TryTurnIn + stage transitions + rewards |
| **Награды** | ✅ | GiveCredits, AddReputation, AddNpcAttitude, GiveItem, TakeItem |
| **NpcAttitude** | ✅ | Per-player, per-NPC, персистентная, -100..+200 |
| **Reputation (faction)** | ✅ | Per-player, per-faction, персистентная |
| **Cross-faction influence** | ✅ | ModifyNpcAttitude → attitudeLinks → ModifyReputation(silent) |
| **WorldEventBus** | ✅ | NpcAttitudeChangedEvent, ReputationChangedEvent, ItemAdded/Removed... |
| **Toast-уведомления** | ✅ | "💚 mira_01 +5", "💰 +200 CR" |
| **QuestTracker HUD** | ✅ | Отслеживание активного квеста |
| **CharacterWindow (Quests tab)** | ✅ | Active/Completed/Failed/Discovered |
| **Persistence** | ✅ | JsonQuestStateRepository |

### 4.2 ✅ Работает в AI-системе

| Система | Статус | Комментарий |
|---------|--------|-------------|
| **NpcBrain FSM** | ✅ | Idle/Chase/Attack/Dead + NavMeshAgent |
| **BehaviorType.Passive** | ✅ | Стоит мирно, агрится после порога урона |
| **NpcAttacker** | ✅ | Наносит урон через CombatServer |
| **NpcTarget** | ✅ | Принимает урон, HP, смерть, лут, OnHpChanged event |
| **NpcSpawner** | ✅ | Спавн по радиусу, лимитам, чанкам |
| **Spawn Cycle Control** | ✅ | Finite/FiniteCycle/Infinite + ISpawnRestartTrigger |
| **NpcSocialBrain** | ✅ | Patrol, Flee, Grudge, Emotion, Group |
| **GrudgeTable** | ✅ | Память обидчиков (playerId → timestamp) |
| **NpcFaction (AI)** | ✅ | SO с отношениями Hostile/Neutral/Allied |

---

## 5. ЧЕГО НЕ ХВАТАЕТ (Gap Analysis)

### 5.1 🔴 КРИТИЧЕСКИЕ (без них паттерн не работает)

| # | Gap | Почему критично | Что нужно сделать |
|---|-----|-----------------|-------------------|
| **G1** | **Квестовый NPC не имеет NpcTarget** | Его нельзя атаковать, у него нет HP | Добавить NpcTarget + NpcCombatData на квестовый префаб |
| **G2** | **Квестовый NPC не имеет NpcBrain** | Он не может дать сдачи (Passive→Aggressive) | Добавить NpcBrain с BehaviorType.Passive |
| **G3** | **Нет моста: Combat → NpcAttitude** | При атаке/убийстве не портится отношение | NpcTarget.OnHpChanged → QuestWorld.ModifyNpcAttitude |
| **G4** | **Нет моста: NpcAttitude → Behavior** | Низкое отношение не делает NPC враждебным | NpcAttitudeChangedEvent → NpcBrain.BehaviorType |
| **G5** | **NpcController — MonoBehaviour** | Не может быть сетевым (NetworkBehaviour) | Перевести на NetworkBehaviour ИЛИ создать гибрид |
| **G6** | **Нет респавна для scene-placed NPC** | Убитый квестовый NPC исчезает навсегда | SpawnRestart-система для scene-placed NPC |

### 5.2 🟡 ВАЖНЫЕ (улучшают качество)

| # | Gap | Почему важно | Что нужно сделать |
|---|-----|-------------|-------------------|
| **G7** | **Две системы фракций** | `NpcFaction` (AI) vs `FactionId` (Quests) — несовместимы | Унифицировать или построить мост |
| **G8** | **GrudgeTable не влияет на NpcAttitude** | Память обид — временная, не персистентная | Конвертировать grudge → NpcAttitude penalty |
| **G9** | **Нет инспектора для респавна** | Дизайнер не может настроить респавн квестового NPC | Поля на NpcDefinition или NpcController |
| **G10** | **Визуал — Cube placeholder** | Квестовые NPC выглядят как белые кубы | Заменить на HumanM_Model + анимации |
| **G11** | **Нет анимаций для квестовых NPC** | Idle/Walk/Talk анимации отсутствуют | NpcAnimatorController (уже спроектирован в 70_NPC_ENEMIES.md) |

### 5.3 🟢 КОСМЕТИЧЕСКИЕ

| # | Gap | Комментарий |
|---|-----|-------------|
| **G12** | Toast показывает "mira_01" вместо "Mira" | M15.1 — нужен displayName lookup |
| **G13** | Нет визуального feedback при атаке NPC | HP bar, hit flash |
| **G14** | Нет звуковых cue | Attack grunt, death scream |

---

## 6. ЦЕЛЕВОЙ ПАЙПЛАЙН (что создаём, что настраиваем)

### 6.1 Общая архитектура Complete v2 NPC

```
                        ┌──────────────────────────────────────┐
                        │         NpcController (NEW v2)        │
                        │    NetworkBehaviour + INetworkObject  │
                        │                                       │
                        │  [Inspector]                          │
                        │  ├─ NpcDefinition (npcId, faction...) │
                        │  ├─ Quest интеграция (диалоги)        │
                        │  ├─ Combat интеграция (HP, урон)      │
                        │  ├─ Respawn Config (mode, timer...)   │
                        │  └─ Hostility Threshold (NpcAttitude) │
                        │                                       │
                        │  ┌─────────────┐  ┌────────────────┐  │
                        │  │  NpcBrain   │  │   NpcTarget    │  │
                        │  │  (Passive)  │  │  (HP + Death)  │  │
                        │  └──────┬──────┘  └───────┬────────┘  │
                        │         │                  │          │
                        │         │  OnHpChanged ────┤          │
                        │         │  │               │          │
                        │         ▼  ▼               ▼          │
                        │  ┌─────────────────────────────────┐  │
                        │  │     AttitudeCombatBridge (NEW)   │  │
                        │  │  OnHpChanged(delta)             │  │
                        │  │    → QuestWorld.ModifyNpcAttitude│  │
                        │  │  OnKilled(killerId)              │  │
                        │  │    → QuestWorld.ModifyNpcAttitude│  │
                        │  │  OnNpcAttitudeChanged(att)       │  │
                        │  │    → if att < threshold:         │  │
                        │  │       NpcBrain.Aggressive        │  │
                        │  └─────────────────────────────────┘  │
                        │                                       │
                        │  ┌─────────────────────────────────┐  │
                        │  │    NpcRespawnController (NEW)    │  │
                        │  │  mode: None / Timer / Event      │  │
                        │  │  timerSeconds: 300               │  │
                        │  │  restartTriggers: [...]          │  │
                        │  │  OnDeath → Despawn/Cycle         │  │
                        │  └─────────────────────────────────┘  │
                        └──────────────────────────────────────┘
```

### 6.2 Шаг за шагом: что создаём

#### Фаза 0 — Префаб (один раз)

**Создать:** `Assets/_Project/Prefabs/NPC/NPC_Quest.prefab`

```
NPC_Quest (root)
├─ NetworkObject
├─ NpcController (NEW v2: NetworkBehaviour)
│   ├─ _npcDefinition: NpcDefinition
│   └─ _respawnConfig: NpcRespawnConfig
├─ NpcBrain (BehaviorType = Passive)
│   ├─ aggroRange = 10
│   ├─ aggroHpThreshold = 0.25
│   └─ maxHitsPerMinute = 3
├─ NpcTarget
│   ├─ _data: NpcCombatData
│   └─ _lootTable: ...
├─ NpcAttacker
│   └─ _data: NpcCombatData
├─ NpcSocialBrain
│   ├─ faction: NpcFaction SO
│   ├─ enableGrudgeMemory = true
│   └─ personalityConfig: ...
├─ AttitudeCombatBridge (NEW)
│   ├─ _npcId: string
│   ├─ _hostilityThreshold: -50
│   └─ _attitudeDeltaOnHit: -2
├─ NpcRespawnController (NEW)
│   ├─ _mode: None / Timer / Event
│   ├─ _respawnDelaySeconds: 300
│   └─ _restartTriggers: List<MonoBehaviour>
├─ NavMeshAgent
├─ CharacterController (или только NavMeshAgent)
├─ Visual (child)
│   ├─ HumanM_Model (MeshFilter + MeshRenderer)
│   ├─ Animator (NpcAnimatorController)
│   └─ NpcVisualApplier
└─ VisualMarker (debug)
```

#### Фаза 1 — Боевой мост (AttitudeCombatBridge)

**Новый файл:** `Assets/_Project/Scripts/AI/AttitudeCombatBridge.cs`

```csharp
public class AttitudeCombatBridge : NetworkBehaviour
{
    [SerializeField] private string _npcId;                    // "mira_01"
    [SerializeField] private int _hostilityThreshold = -50;     // NpcAttitude < -50 → Aggressive
    [SerializeField] private int _attitudeDeltaOnHit = -2;      // -2 за каждое попадание
    [SerializeField] private int _attitudeDeltaOnKill = -50;    // -50 за убийство
    [SerializeField] private bool _broadcastToFaction = true;   // портить репутацию фракции
    
    private NpcTarget _target;
    private NpcBrain _brain;
    
    void OnNetworkSpawn()
    {
        if (!IsServer) { enabled = false; return; }
        _target = GetComponent<NpcTarget>();
        _brain = GetComponent<NpcBrain>();
        if (_target != null) _target.OnHpChanged += OnHpChanged;
        
        // Подписаться на изменения NpcAttitude
        WorldEventBus.Subscribe<NpcAttitudeChangedEvent>(OnNpcAttitudeChanged);
    }
    
    void OnHpChanged(int newHp, int deltaHp)
    {
        if (deltaHp <= 0) return;
        // Кто атаковал? Ищем через CombatServer или через _brain.LastAggressor
        ulong attackerId = ResolveAttackerId();
        if (attackerId == 0) return;
        
        QuestWorld.Instance?.ModifyNpcAttitude(attackerId, _npcId, _attitudeDeltaOnHit);
    }
    
    // На OnKilled (вызывается из NpcTarget)
    public void OnKilledByPlayer(ulong killerId)
    {
        QuestWorld.Instance?.ModifyNpcAttitude(killerId, _npcId, _attitudeDeltaOnKill);
    }
    
    void OnNpcAttitudeChanged(NpcAttitudeChangedEvent ev)
    {
        if (ev.NpcId != _npcId) return;
        if (ev.NewValue < _hostilityThreshold)
        {
            _brain?.ApplySpawnerBehavior(BehaviorType.Aggressive, 0, 0);
        }
    }
}
```

#### Фаза 2 — Респавн (NpcRespawnController)

**Новый файл:** `Assets/_Project/Scripts/AI/NpcRespawnController.cs`

Использует существующую систему `ISpawnRestartTrigger` + `SpawnRestartTimer` / `SpawnRestartUnityEvent` / `SpawnRestartTriggerZone`.

```csharp
public enum NpcRespawnMode { None, Timer, Event }

public class NpcRespawnController : NetworkBehaviour
{
    [SerializeField] private NpcRespawnMode _mode = NpcRespawnMode.None;
    [SerializeField] private float _timerSeconds = 300f;
    [SerializeField] private List<MonoBehaviour> _restartTriggers;
    
    private NpcTarget _target;
    private NetworkObject _netObj;
    private Vector3 _originPosition;
    private Quaternion _originRotation;
    private bool _isDead;
    private float _deathTime;
    
    void OnNetworkSpawn()
    {
        if (!IsServer) { enabled = false; return; }
        _target = GetComponent<NpcTarget>();
        _netObj = GetComponent<NetworkObject>();
        _originPosition = transform.position;
        _originRotation = transform.rotation;
    }
    
    void Update()
    {
        if (!_isDead) return;
        
        switch (_mode)
        {
            case NpcRespawnMode.None: return;
            case NpcRespawnMode.Timer:
                if (Time.time - _deathTime >= _timerSeconds)
                    Respawn();
                break;
            case NpcRespawnMode.Event:
                if (CheckRestartTriggers())
                    Respawn();
                break;
        }
    }
    
    public void OnNpcKilled()
    {
        _isDead = true;
        _deathTime = Time.time;
        // Despawn через N секунд на клиентах
        StartCoroutine(DespawnCorpse());
    }
    
    void Respawn()
    {
        _isDead = false;
        // Восстановить HP, позицию, состояние
        transform.SetPositionAndRotation(_originPosition, _originRotation);
        var nt = GetComponent<NpcTarget>();
        nt?.ResetHp();
        var nb = GetComponent<NpcBrain>();
        nb?.ResetState();
        _netObj.Spawn(); // или просто включить (если не деспавнили)
    }
}
```

#### Фаза 3 — Инспектор для дизайнера

Добавить на `NpcController` (v2) поля:

```
[NpcController]
├─ NpcDefinition           ← существующее
├─ Respawn Mode: None | Timer | Event
├─ Respawn Timer (sec)     ← если Timer
├─ Restart Triggers        ← если Event (перетащить GameObject'ы)
├─ Hostility Threshold     ← -50 (NpcAttitude)
├─ Attitude Delta On Hit   ← -2
├─ Attitude Delta On Kill  ← -50
```

### 6.3 Что НЕ создаём (переиспользуем готовое)

| Компонент | Статус | Где взять |
|-----------|--------|-----------|
| NpcBrain | ✅ Готов | `AI/NpcBrain.cs` — вешаем на префаб |
| NpcTarget | ✅ Готов | `Combat/Implementations/NpcTarget.cs` |
| NpcAttacker | ✅ Готов | `Combat/Implementations/NpcAttacker.cs` |
| NpcSocialBrain | ✅ Готов | `AI/NpcSocialBrain.cs` |
| GrudgeTable | ✅ Готов | `AI/GrudgeTable.cs` (уже в NpcSocialBrain) |
| NpcAnimatorController | 📝 Спроектирован | `70_NPC_ENEMIES.md` §2.4-2.5 |
| NpcVisualConfig | ✅ Готов | `AI/NpcVisualConfig.cs` |
| NpcSpawner (для респавна) | ✅ Готов | Переиспользуем ISpawnRestartTrigger |

---

## 7. УНИФИКАЦИЯ ФРАКЦИЙ (G7)

### Проблема

| Система | Тип | Значения |
|---------|-----|----------|
| `FactionId` (Quests) | enum | GuildOfThoughts, GuildOfCreation, ... Pirates, Neutral (12 значений) |
| `NpcFaction` (AI) | ScriptableObject | factionId + relation matrix (Hostile/Neutral/Allied) |

### Решение: мост через factionId

```csharp
// В NpcFaction (AI):
public class NpcFaction : ScriptableObject
{
    public string factionId;  // "GuildOfThoughts" — матчится с FactionId enum name
    public FactionRelation defaultRelation;
    // ...
    
    // Новое: получить квестовый FactionId
    public FactionId GetQuestFactionId()
    {
        return Enum.TryParse<FactionId>(factionId, out var result) 
            ? result : FactionId.Neutral;
    }
}
```

**Правило:** `NpcFaction.factionId` (string) = `FactionId` enum name. Дизайнер выбирает из dropdown.

---

## 8. ПЛАН РЕАЛИЗАЦИИ (тикеты)

### P0 — Минимальный Complete v2 NPC (~12-14 часов)

| # | Тикет | Что | Файлы | ~Часов |
|---|-------|-----|-------|--------|
| **T-CNPC-01** | AttitudeCombatBridge | Мост: NpcTarget.OnHpChanged → ModifyNpcAttitude; OnNpcAttitudeChanged → NpcBrain.BehaviorType | `AttitudeCombatBridge.cs` (NEW) | 2 |
| **T-CNPC-02** | NpcRespawnController | Респавн для scene-placed NPC: None/Timer/Event + ISpawnRestartTrigger | `NpcRespawnController.cs` (NEW), `NpcRespawnMode.cs` (NEW) | 2 |
| **T-CNPC-03** | NpcController v2 (NetworkBehaviour) | Переработка NpcController в NetworkBehaviour + интеграция NpcBrain/NpcTarget/NpcAttacker | `NpcController.cs` (REWRITE) | 3 |
| **T-CNPC-04** | Префаб NPC_Quest | Создание префаба: NetworkObject + NpcController v2 + NpcBrain + NpcTarget + NpcAttacker + NpcSocialBrain + NavMeshAgent + Visual | `NPC_Quest.prefab` (NEW), сцена | 2 |
| **T-CNPC-05** | Визуал: Модель + Анимации | Замена Cube на HumanM_Model + NpcAnimatorController | Префаб, `NpcAnimatorController.controller` (NEW) | 2 |
| **T-CNPC-06** | Инспектор NpcController v2 | Поля: Respawn, Hostility Threshold, Attitude Delta | `NpcControllerEditor.cs` (NEW) | 1 |
| **T-CNPC-07** | Интеграция с Mira | Замена старого [Mira] на новый NPC_Quest в WorldScene_0_0 | Сцена | 0.5 |
| **T-CNPC-08** | Play Mode verify | Полный тест: E→диалог, атака→ответ, убийство→NpcAttitude↓→Hostile, респавн | — | 1 |

### P1 — Полировка (~6-8 часов)

| # | Тикет | Что | ~Часов |
|---|-------|-----|--------|
| **T-CNPC-09** | Унификация фракций | Мост NpcFaction (AI) ↔ FactionId (Quests) | 2 |
| **T-CNPC-10** | GrudgeTable → NpcAttitude | Конвертация временной обиды в персистентное отношение | 1.5 |
| **T-CNPC-11** | Cross-faction при атаке | attitudeLinks применяются при боевых штрафах | 1 (уже работает) |
| **T-CNPC-12** | Визуальный feedback | HP bar над головой, hit flash | 2 |
| **T-CNPC-13** | NpcSpawnerConfig для квестовых NPC | Пресет Passive + диалоговые настройки | 0.5 |

### P2 — Контент (~4-6 часов)

| # | Тикет | Что | ~Часов |
|---|-------|-----|--------|
| **T-CNPC-14** | Восстановить квестовые ассеты | FactionDefinition, NpcDefinition, QuestDefinition .asset файлы | 2 |
| **T-CNPC-15** | Производственные NPC (5-10) | Создать NPC с диалогами, квестами, фракциями | 3 |
| **T-CNPC-16** | Баланс hostility | Настроить пороги для каждой фракции | 1 |

---

## 9. ЧТО ДЕЛАТЬ ПРЯМО СЕЙЧАС

### Рекомендованный порядок:

1. **T-CNPC-01: AttitudeCombatBridge** — ключевой новый код. Без него нет связи Combat → Репутация.
2. **T-CNPC-03: NpcController v2 (NetworkBehaviour)** — переработка с нуля, интегрирует всё.
3. **T-CNPC-04 + T-CNPC-05: Префаб + Визуал** — собираем GameObject.
4. **T-CNPC-02: NpcRespawnController** — добавляем респавн.
5. **T-CNPC-06: Инспектор** — удобный Editor.
6. **T-CNPC-07: Интеграция с Mira** — заменяем старого NPC на нового.
7. **T-CNPC-08: Play Mode verify** — тестируем.

---

## 10. ФАЙЛЫ (полный список)

### Новые (~7 файлов)

| Файл | Назначение |
|------|------------|
| `Scripts/AI/AttitudeCombatBridge.cs` | Мост Combat → NpcAttitude → Behavior |
| `Scripts/AI/NpcRespawnController.cs` | Респавн: None/Timer/Event |
| `Scripts/AI/NpcRespawnMode.cs` | Enum для режимов респавна |
| `Prefabs/NPC/NPC_Quest.prefab` | Префаб Complete v2 NPC |
| `Animation/AI/NpcAnimatorController.controller` | Контроллер анимаций NPC |
| `Editor/NpcControllerEditor.cs` | Кастомный инспектор |
| `Scripts/Quests/NpcController.cs` | REWRITE: NetworkBehaviour + все интеграции |

### Изменяемые (~3 файла)

| Файл | Что |
|------|-----|
| `AI/NpcBrain.cs` | + public API для смены BehaviorType извне (если нет) |
| `AI/NpcTarget.cs` | + OnKilled callback для AttitudeCombatBridge |
| `AI/NpcFaction.cs` | + GetQuestFactionId() мост |

### Не трогаем (используем как есть)

| Файл | Почему |
|------|--------|
| `Quests/Network/QuestServer.cs` | Полностью работает |
| `Quests/Core/QuestWorld.cs` | ModifyNpcAttitude уже есть |
| `AI/NpcSocialBrain.cs` | Полностью работает |
| `AI/NpcSpawnerConfig.cs` | Только читаем |
| `Quests/UI/DialogWindow.cs` | Полностью работает |
| `Combat/Implementations/NpcAttacker.cs` | Полностью работает |

---

## 11. РИСКИ

| # | Риск | Вероятность | Митигация |
|---|------|------------|-----------|
| 1 | NpcController v2 ломает существующих NPC в сцене | Средняя | Создать новый префаб, старую сцену не трогать до verify |
| 2 | NetworkBehaviour для квестовых NPC требует ScenePlacedObjectSpawner | Высокая | Следовать паттерну как для QuestServer/MarketServer |
| 3 | AttitudeCombatBridge не может найти attackerId клиента | Средняя | NpcTarget.OnHpChanged уже хранит attackerClientId (из ApplyDamage) |
| 4 | Два Animator конфликтуют (pitfall из 70_NPC_ENEMIES.md §11) | Средняя | Следовать чек-листу из §11: отключить Animator на HumanM_Model |
| 5 | Квестовые ассеты утеряны (DEEP_AUDIT 2026-07-13) | Высокая | Восстановить через CSV-импорт или создать заново |

---

## 12. ИТОГ

**У нас есть 80% кода для Complete v2 NPC.** Не хватает только моста между двумя мирами — боевым и квестовым. Ключевые новые компоненты:

- `AttitudeCombatBridge` — связывает урон с репутацией и репутацию с поведением
- `NpcRespawnController` — даёт дизайнеру контроль над респавном
- `NpcController v2` — превращает MonoBehaviour в полноценный NetworkBehaviour с AI

**Оценка:** 12-14 часов на P0 (работающий Complete v2 NPC с Мирой), + 6-8 часов на P1 (полировка).

---

*Анализ проведён на основе:*
- `70_NPC_ENEMIES.md` + `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md` (AI/Combat)
- `01_CURRENT_STATE_AUDIT.md` + `02_V2_ARCHITECTURE.md` + `08_ROADMAP.md` (Quests)
- `DEEP_AUDIT_2026-07-13.md` (Quests audit)
- `SPAWN_TEST.prefab` (AI prefab)
- `NpcController.cs`, `NpcBrain.cs`, `NpcTarget.cs`, `QuestWorld.cs`, `NpcAttitude.cs` (production code)
