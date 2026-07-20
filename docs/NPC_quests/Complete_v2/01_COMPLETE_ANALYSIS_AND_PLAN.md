# 🔴 NPC Complete v2 — Полный Анализ + Merged Pipeline

> **Дата:** 2026-07-20
> **Основание:** чтение реального кода (53 файла из 4 подсистем) + существующий `00_ARCH_ANALYSIS_v2_CORRECTED.md` + `00_DEEP_ANALYSIS_Complete_v2.md`
> **Цель:** единый документ, сводящий оба анализа, добавляющий пропущенное, дающий детальный pipeline реализации.

---

## 1. КАК СООТНОСЯТСЯ ДВА АНАЛИЗА

### 1.1 Общие точки (полное совпадение)

| Позиция | ARCH_ANALYSIS | Мой анализ | Статус |
|---------|--------------|------------|--------|
| G1: OnNpcHpChanged не вызывает ModifyNpcAttitude | ✅ | ✅ | Совпадает |
| G2: OnKilled не вызывает ModifyNpcAttitude | ✅ | ✅ | Совпадает |
| G3: Нет подписки на NpcAttitudeChanged → смена BehaviorType | ✅ | ✅ | Совпадает |
| G4: NpcTarget.Destroy — финальный, нет респавна | ✅ | ✅ | Совпадает |
| G5: На [Mira] нет NetworkObject | ✅ | ✅ | Совпадает |
| NpcController не нужно переписывать на NetworkBehaviour | ✅ | ✅ | Совпадает |
| ~40 строк нового кода, 0 новых файлов (базово) | ✅ | ✅ | Совпадает |
| Архитектурное решение: добавить компоненты на тот же GO | ✅ | ✅ | Совпадает |

### 1.2 Что ARCH_ANALYSIS НЕ ЗАМЕТИЛ (дополнения)

| # | Что пропущено | Почему важно | Где |
|---|--------------|-------------|-----|
| **G6** | **NpcBrain не знает свой `npcId`** | Без npcId нельзя вызвать `QuestWorld.ModifyNpcAttitude`. Нужно поле + инициализация. | NpcBrain.cs: нет `_npcId` поля |
| **G7** | **Порог автоматической враждебности** | Когда NpcAttitude падает ниже -X, Passive NPC должен стать Aggressive **автоматически**, без повторной атаки. Нужна конфигурация `hostilityThreshold` в NpcDefinition. | Нигде |
| **G8** | **NpcFaction (старый) vs FactionId (новый) — нет моста** | NpcSocialBrain использует `NpcFaction SO` (старая система), а QuestWorld.ModifyNpcAttitude использует `string npcId` + `FactionId`. Нельзя просто вызвать ModifyNpcAttitude без конвертации. | Две разных системы типов |
| **G9** | **OnKilled — private, нет события смерти** | Нельзя подписаться на смерть NPC извне (для quest triggers, репутации, эффектов). Нужен `public event Action<ulong> OnKilledEvent`. | NpcTarget.cs:147 |
| **G10** | **Не описан механизм респавна для scene-placed** | ARCH_ANALYSIS говорит "заменить Destroy на disable+coroutine", но не уточняет: как восстановить HP, визуал, AI состояния, коллайдер, агента. Нужна полная спецификация. | NpcTarget.cs:140 |
| **G11** | **NpcBrain не импортирует QuestWorld** | В NpcBrain.cs нет `using ProjectC.Quests;`. Если добавить вызов ModifyNpcAttitude — нужно добавить using и проверку `QuestWorld.Instance != null`. | NpcBrain.cs: imports |
| **G12** | **Нет префаба NPC_Quest (шаблон)** | Сейчас нет готового префаба, который объединяет NpcController + NpcBrain + NpcTarget + NpcAttacker + NetworkObject. Каждый раз собирать вручную — ошибкоопасно. | Нет префаба |

### 1.3 Что ARCH_ANALYSIS НЕ ДОГОВОРИЛ (уточнения)

| Позиция | ARCH_ANALYSIS | Уточнение |
|---------|--------------|-----------|
| Фикс G1 | "+3 строки" | Реально +5-7 строк: `_npcId` поле (1), using QuestWorld (1), проверка Instance (1), вызов ModifyNpcAttitude (2), break (1) |
| Фикс G2 | "+5 строк" | Реально: OnKilledEvent (1 строка), вызов event в OnKilled (1), подписка из NpcBrain (3) + вызов ModifyNpcAttitude в подписке (4) = ~9 строк в 2х файлах |
| Фикс G3 | "+12 строк" | Cогласен, но нужно добавить `hostilityThreshold` поле в NpcBrain (1 строка) |
| Фикс G4 | "+15 строк" | Согласен, но нужно добавить `NpcRespawnConfig` (таймер, условие, флаг) — либо отдельный компонент, либо поля в NpcBrain |
| "AttitudeCombatBridge не нужен" | Верно — логика в NpcBrain | Но нужно добавить `_npcId` поле, иначе NpcBrain не сможет вызвать ModifyNpcAttitude |

---

## 2. ПОЛНАЯ КАРТИНА ПОДСИСТЕМ

### 2.1 Задействованные файлы (проверено в коде)

```
AI/                                                        (ProjectC.AI)
├── NpcBrain.cs             1114 строк  ← AI FSM + BehaviorType + OnHpChanged
├── NpcSocialBrain.cs        934 строк  ← faction, grudge, patrol, flee, surrender
├── NpcSpawner.cs                      ← спавн NPC из префаба
├── NpcSpawnerConfig.cs                ← SO-конфиг спавнера
└── Editor/NpcSpawnerConfigEditor.cs   ← кастомный редактор (10 foldout групп)

Combat/                                                    (ProjectC.Combat)
├── Implementations/
│   ├── NpcTarget.cs         257 строк  ← HP, смерть, лут, OnHpChanged
│   ├── NpcAttacker.cs       334 строк  ← наносит урон
│   ├── NpcCombatData.cs                ← SO с данными NPC для боя
│   ├── PlayerTarget.cs                 ← IDamageTarget для игрока
│   └── PlayerAttacker.cs               ← IAttacker для игрока
├── Core/
│   ├── IDamageTarget.cs      ← интерфейс (ApplyDamage, OnHpChanged?)
│   └── IAttacker.cs          ← интерфейс
├── Network/CombatServer.cs            ← серверный арбитр боёв
└── DamageCalculator.cs                ← расчёт урона

Quests/                                                    (ProjectC.Quests)
├── NpcController.cs         130 строк  ← MonoBehaviour: trigger + NpcDefinition ref
├── Npcs/NpcDefinition.cs    123 строк  ← SO: npcId, questOffers, attitudeLinks, etc.
├── Factions/
│   ├── FactionDefinition.cs  107 строк  ← SO: factionId, tiers, color
│   ├── FactionId.cs          enum        ← None=0, GuildOfThoughts, etc.
│   └── NpcAttitude.cs        55 строк   ← struct: npcId + value (-100..+200)
├── Core/QuestWorld.cs      1350 строк  ← POCO: quests, reputation, npcAttitude, dialog
├── Network/QuestServer.cs              ← NetworkBehaviour: RPCs
├── Dialogue/
│   ├── DialogTree.cs                   ← SO
│   ├── DialogueNode.cs                 ← POCO
│   ├── DialogueAction.cs               ← OfferQuest, AddReputation, etc.
│   └── DialogueCondition.cs            ← HasItem, QuestStateEquals, etc.
├── Triggers/
│   ├── QuestTriggerService.cs          ← event bus обработчик триггеров
│   ├── ConcreteTriggers.cs             ← TalkedToNpcTrigger, HaveItemTrigger и т.д.
│   └── IQuestTrigger.cs                ← интерфейс
├── UI/DialogWindow.cs                  ← UI Toolkit окно диалога
├── Quests/QuestDefinition.cs           ← SO: stages, objectives, rewards
└── Persistence/                        ← IQuestStateRepository + JSON

Core/                                                      (ProjectC.Core)
└── WorldEventBus.cs         ← static: Publish<T> / Subscribe<T>
    └── WorldEvent.cs        ← base class: NpcAttitudeChangedEvent, ReputationChangedEvent, etc.
```

### 2.2 Что уже работает (проверено по коду)

| Возможность | Файл | Строки |
|-------------|------|--------|
| Passive NPC (не агрится сам) | NpcBrain.cs | 276: `if (_behaviorType != BehaviorType.Passive) return;` |
| Порог агрессии (25% HP) | NpcBrain.cs | 90-91, 285-288 |
| Порог агрессии (3 hits/60s) | NpcBrain.cs | 95, 286 |
| Поиск атакующего + clientId | NpcBrain.cs | 258-274 |
| GrudgeTable запись | NpcBrain.cs | 270 |
| E-key → диалог | NpcController.cs + QuestServer | — |
| Диалог + выдача квестов | DialogTree + DialogueAction.OfferQuest | — |
| NpcAttitude система | QuestWorld.cs | 282-323 |
| Cross-faction influence | QuestWorld.cs | 299-311 |
| NpcAttitudeChangedEvent | QuestWorld.cs | 313 |
| Modular NpcSpawner (конфиг через SO) | NpcSpawner.cs + NpcSpawnerConfig.cs | — |
| Кастомный редактор NpcSpawnerConfig | NpcSpawnerConfigEditor.cs | — |

---

## 3. ПОЛНЫЙ ПАЙПЛАЙН NPC (ДЕТАЛЬНЫЙ)

### 3.1 Схема NPC в runtime

```
GameObject "MiraQuestNPC" (scene-placed)
│
├── NetworkObject                         ← [ДОБАВИТЬ] для NetworkBehaviours
│
├── NpcController : MonoBehaviour         ← [УЖЕ ЕСТЬ] trigger + NpcDefinition ref
│   └── NpcDefinition SO (npcId="mira_01", faction=GuildOfThoughts)
│
├── NpcBrain : NetworkBehaviour           ← [ДОБАВИТЬ] AI FSM
│   ├── BehaviorType = Passive
│   ├── aggroHpThreshold = 25%
│   ├── _npcId = "mira_01"               ← [G6: НОВОЕ ПОЛЕ]
│   └── hostilityThreshold = -50         ← [G7: НОВОЕ ПОЛЕ]
│
├── NpcTarget : NetworkBehaviour          ← [ДОБАВИТЬ] HP + смерть
│   ├── NpcCombatData SO (HP=500)
│   └── OnKilledEvent                    ← [G9: НОВЫЙ EVENT]
│
├── NpcAttacker : NetworkBehaviour        ← [ДОБАВИТЬ] наносит урон
│   └── NpcSkillSet (melee + maybe 1 skill)
│
├── NpcSocialBrain : MonoBehaviour        ← [ДОБАВИТЬ] faction, grudge
│   └── NpcFaction SO (GuildOfThoughts)
│
├── NavMeshAgent                          ← [ДОБАВИТЬ] для движения при атаке
│
└── Visual (mesh + animator)             ← [ЗАМЕНИТЬ] Cube на модель
```

### 3.2 Data flow: атака → репутация → враждебность

```
Player атакует Mira
       │
       ▼
CombatServer.ResolveAttack()
       │
       ▼
NpcTarget.ApplyDamage(damageResult, attackerClientId)
       │   ▲ attackerClientId = ulong clientId  [УЖЕ ЕСТЬ: NpcTarget.cs:94]
       │
       ├──► _currentHp.Value -= damage
       │
       ├──► OnHpChanged?.Invoke(newHp, deltaHp)
       │        │
       │        ▼
       │   NpcBrain.OnNpcHpChanged(newHp, deltaHp)
       │       ├── [УЖЕ] FindNearestPlayerTarget() → clientId → RecordPlayerHit()
       │       ├── [ДОБАВИТЬ G1] QuestWorld.ModifyNpcAttitude(clientId, _npcId, -2)
       │       └── [УЖЕ] thresholdReached → _isAggrod → EnterChase()
       │
       └──► если newHp == 0:
                ├── OnKilled(attackerClientId)   ← private [G9]
                ├── [ДОБАВИТЬ G2] OnKilledEvent?.Invoke(attackerClientId)
                └── [УЖЕ] SpawnLootPickup(attackerClientId)

Когда NpcAttitude падает < hostilityThreshold:
       │
       ▼
WorldEventBus.NpcAttitudeChangedEvent (NpcId, NewValue, Delta)
       │
       ▼
NpcBrain.OnNpcAttitudeChanged(ev)
       │ [ДОБАВИТЬ G3]
       ├── if (ev.NpcId != _npcId) return
       ├── if (ev.NewValue < _hostilityThreshold)
       │       → _behaviorType = Aggressive
       │       → _socialBrain.faction.SetAttitude(hostile)
       └── if (ev.NewValue >= _hostilityThreshold && _behaviorType != Passive)
               → _behaviorType = Passive
               → ResetAggroState()
```

### 3.3 Respawn flow (для scene-placed NPC)

```
NpcTarget.OnKilled()
       │
       ├──► [G4: ЗАМЕНИТЬ] Destroy(gameObject, 3.0f)
       │
       ▼
NpcBrain.OnNpcDeath(attackerClientId)     ← новый метод
       │
       ├──► _state = BrainState.Dead
       ├──► Disable: агент, коллайдер, визуал, AI
       ├──► StartRespawnCoroutine()
       │
       ▼
RespawnCoroutine:
       ├── ждать _respawnDelay (из конфига, e.g. 30s / 300s / -1 = never)
       ├── ✅ если _respawnCondition != None:
       │       ждать пока условие выполнено (например: игрок ушёл из зоны)
       ├──► Reset: full HP, _state = Idle, enable всё
       ├──► ResetAggroState()
       └──► NpcBrain.EnterIdle()
```

```
Конфиг респавна (в NpcBrain + NpcDefinition):
  respawnMode: enum { None, Timer, TimerOrCondition, OnlyCondition, Never }
  respawnDelaySeconds: 30 (по умолчанию)
  respawnCondition: enum { None, PlayerOutOfRange, ZoneEvent, CustomEvent }
  maxRespawns: 0 = unlimited (для scene-placed NPC)
```

---

## 4. ДЕТАЛЬНЫЙ ПЛАН РЕАЛИЗАЦИИ

### 4.1 Этап 1: Инфраструктура (изменения в коде)

| Шаг | Файл | Изменение | Строк |
|-----|------|-----------|-------|
| **1.1** | `NpcBrain.cs` | + поле `_npcId` (string), + `_hostilityThreshold` (int, default -50), + `_respawnConfig` (struct с параметрами) | +8 |
| **1.2** | `NpcBrain.cs` | + using `ProjectC.Quests` | +1 |
| **1.3** | `NpcBrain.cs` | В `OnNpcHpChanged` после RecordPlayerHit → `QuestWorld.Instance?.ModifyNpcAttitude(c.ClientId, _npcId, -2)` | +3 |
| **1.4** | `NpcBrain.cs` | В `OnNetworkSpawn` → подписка на `WorldEventBus.Subscribe<NpcAttitudeChangedEvent>` + смена BehaviorType | +15 |
| **1.5** | `NpcBrain.cs` | В `OnNetworkSpawn` → кэшировать `_npcId` из `GetComponent<NpcController>()?.Definition?.npcId` | +3 |
| **1.6** | `NpcTarget.cs` | + `public event Action<ulong> OnKilledEvent` | +1 |
| **1.7** | `NpcTarget.cs` | В `OnKilled` — добавить `OnKilledEvent?.Invoke(attackerClientId)` | +1 |
| **1.8** | `NpcTarget.cs` | Заменить `Destroy(gameObject, 3.0f)` на вызов респавн-логики | +3 |
| **1.9** | `NpcBrain.cs` | + метод `OnNpcDeath(ulong attackerClientId)` + корутина респавна | +25 |
| **1.10** | `NpcBrain.cs` | В `OnNpcDeath` — вызов `ModifyNpcAttitude` с большим штрафом (-20) | +3 |

**Итого этап 1:** ~63 строки, 2 изменённых файла

### 4.2 Этап 2: Префаб + Сцена

| Шаг | Что | Где | Описание |
|-----|-----|-----|----------|
| **2.1** | Создать префаб `Assets/_Project/Prefabs/NPC/NPC_Quest.prefab` | Editor | GameObject с: NetworkObject, NpcController, NpcBrain(Passive), NpcTarget, NpcAttacker, NpcSocialBrain, NavMeshAgent, Visual |
| **2.2** | Заменить [Mira] в WorldScene_0_0 | Scene | Удалить старый, вставить новый префаб, настроить NpcDefinition ref |
| **2.3** | Настроить NpcDefinition SO для Mira | Editor | Создать `Npc_mira_01.asset` с faction=GuildOfThoughts, questOffers=[...], dialogTree |
| **2.4** | Зарегистрировать через ScenePlacedObjectSpawner | BootstrapScene | Убедиться что NetworkObject спавнится при StartHost |

### 4.3 Этап 3: Верификация

| Шаг | Что | Команда/Действие |
|-----|-----|------------------|
| **3.1** | Компиляция | Открыть Unity → Console → 0 errors |
| **3.2** | Host + Play | Запустить WorldScene_0_0 → Start Host |
| **3.3** | E-key → диалог | Подойти к Mira → E → DialogWindow |
| **3.4** | Квест | Взять квест → проверить QuestTracker |
| **3.5** | Атака → репутация | Ударить Mira → Console: ModifyNpcAttitude log |
| **3.6** | Агро | Нанести >25% HP → Mira переходит в Chase/Attack |
| **3.7** | Смерть | Убить Mira → Console: OnKilledEvent + ModifyNpcAttitude(-20) |
| **3.8** | Респавн | Через 30с → Mira появляется снова, пассивная |
| **3.9** | Враждебность | Несколько раз убить → NpcAttitude < -50 → Mira агрессивна при подходе |

---

## 5. ЧТО НЕ ВХОДИТ В ЭТОТ ПЛАН (отложено)

| Фича | Почему не сейчас | Зависит от |
|------|-----------------|------------|
| **Полная репутация UI** (CharacterWindow таб) | Уже есть базовый, детали — отдельный UI тикет | T-Q11 |
| **GraphView для диалогов** | Отдельный редакторский тикет | T-Q09b |
| **NpcFaction ↔ FactionId мост** | Не блокирует — можно использовать npcId строкой | T-X1 |
| **Alarm/Group combat** | Социальные фичи, не блокируют репутацию | — |
| **Инвентарь персистенс** | T-X0 отдельный тикет | T-X0 |
| **Multi-language локализация** | Не сейчас | — |

---

## 6. РИСКИ И ПИТФОЛЛЫ

### 6.1 Технические риски

| Риск | Вероятность | Митигация |
|------|-------------|-----------|
| `QuestWorld.Instance == null` на момент вызова | Средняя | Добавить null-проверку + лог |
| NpcBrain не знает npcId при спавне | Низкая | Кэшировать из NpcController в OnNetworkSpawn |
| Респавн сбрасывает NetworkObject состояние | Средняя | Использовать disable/enable, не Destroy/Instantiate |
| WorldEventBus не инициализирован | Низкая | Добавить проверку |
| NpcFaction vs FactionId конфликт | Средняя | Использовать npcId как строку (уже работает) |

### 6.2 Архитектурные решения (зафиксированы)

1. **NpcController остаётся MonoBehaviour** — не переписываем
2. **NetworkBehaviours добавляются на тот же GameObject**
3. **npcId как строка** — единый ключ для обеих систем (AI + Quests)
4. **Респавн через disable/enable** — не через NpcSpawner (хотя можно и через него)
5. **ModifyNpcAttitude вызывается из NpcBrain напрямую** — без отдельного bridge-компонента

---

## 7. ПРОВЕРКА ПАТТЕРНА ПОЛЬЗОВАТЕЛЯ

> "НПС должен быть пассивным, с ним можно поговорить, у него можно взять квесты, если его атаковать — отвечает, при убийстве респавн по таймеру/событию/никогда, отношение портится, при плохом отношении — враждебный."

| Требование | Статус после реализации | Как работает |
|------------|------------------------|--------------|
| Пассивный, стоит на месте | ✅ | BehaviorType.Passive — не агрится по proximity |
| Поговорить (E-key) | ✅ | NpcController trigger → NetworkPlayer.TryInteractNearestNpc |
| Дать квест | ✅ | DialogTree → DialogueAction.OfferQuest → QuestWorld.TryOffer |
| Ответная атака при ударе | ✅ | OnNpcHpChanged → _isAggrod → EnterChase |
| Смерть и респавн | ✅ | OnKilled → disable → timer → enable |
| NpcAttitude падает при атаке | ✅ G1 | ModifyNpcAttitude(clientId, npcId, -2) |
| NpcAttitude падает при убийстве | ✅ G2 | ModifyNpcAttitude(clientId, npcId, -20) |
| Враждебность при низком отношении | ✅ G3 | Подписка на NpcAttitudeChangedEvent → _behaviorType = Aggressive |

---

## 8. ДЕЛЬТА С ARCH_ANALYSIS (конкретные расхождения)

| Аспект | ARCH_ANALYSIS | Этот документ |
|--------|--------------|---------------|
| Строк кода | ~40 | ~63 (с учётом респавн-логики + hostingThreshold + event) |
| Файлов | 0 новых | 0 новых (совпадает) |
| NpcBrain поля | не указаны | `_npcId`, `_hostilityThreshold`, `_respawnConfig` |
| OnKilled | "сделать internal" | "добавить public event" (чище, меньше связности) |
| Респавн | "disable+coroutine" (общо) | Полная спецификация: таймер, условие, макс-респавны |
| Faction mismatch | не упомянут | Задокументирован (мост не нужен, используем npcId строкой) |
| Quest и Combat подсистемы | описаны раздельно | Показана интеграция на одной схеме |

---

## 9. ИТОГ

**Сейчас** у нас есть:
- ✅ Полноценная боевая система (NpcBrain + NpcTarget + NpcAttacker + CombatServer)
- ✅ Полноценная квестовая система (NpcController + QuestWorld + DialogTree + FactionDefinition)
- ✅ WorldEventBus для связи между системами
- ✅ NpcAttitude + ModifyNpcAttitude с cross-faction influence
- ✅ Респавн через NpcSpawner (для спавненных врагов)

**Не хватает:**
- 🔴 **63 строки кода** в 2 файлах для интеграции AI+Quest через репутацию
- 🔴 **1 префаб** NPC_Quest с комбинированными компонентами
- 🔴 **Замена [Mira]** в сцене на новый префаб

**Сводка изменений:**
- `NpcBrain.cs`: +58 строк (поле npcId, ModifyNpcAttitude вызов, подписка на event, респавн-логика)
- `NpcTarget.cs`: +5 строк (public OnKilledEvent, вызов event, триггер респавна)
- **0 новых .cs файлов**
- **1 новый префаб** (сборка в редакторе)
- **~2-3 часа работы** включая настройку ассетов

---

## 10. ПРИЛОЖЕНИЕ: КЛЮЧЕВЫЕ СТРОКИ КОДА

### 10.1 Планируемые изменения в NpcBrain.cs

```csharp
// 1. Новые поля (ряом с существующими BehaviorType полями, ~строка 84)
[Header("Quest/Reputation (T-CNPC-01)")]
[Tooltip("NPC id для системы репутации. Автоматически из NpcController, если пусто.")]
[SerializeField] private string _npcId = "";
[Tooltip("Порог NpcAttitude, при котором NPC становится Aggressive. -50 = враждебность.")]
[Range(-100, 200)] [SerializeField] private int _hostilityThreshold = -50;

[Header("Respawn (scene-placed NPC)")]
[SerializeField] private bool _respawnEnabled = true;
[SerializeField] private float _respawnDelaySeconds = 30f;
[SerializeField] private int _maxRespawns = 0; // 0 = unlimited

// 2. В OnNetworkSpawn (после строка 240):
// T-CNPC-01: кэшируем npcId из NpcController
if (string.IsNullOrEmpty(_npcId))
{
    var ctrl = GetComponent<ProjectC.Quests.NpcController>();
    if (ctrl != null && ctrl.Definition != null)
        _npcId = ctrl.Definition.npcId;
}
// T-CNPC-01: подписка на NpcAttitudeChangedEvent
WorldEventBus.Subscribe<NpcAttitudeChangedEvent>(OnNpcAttitudeChanged);

// 3. Новый метод (~после строки 296):
private void OnNpcAttitudeChanged(ProjectC.Core.NpcAttitudeChangedEvent ev)
{
    if (!IsServer) return;
    if (ev.NpcId != _npcId) return;
    if (ev.NewValue < _hostilityThreshold)
    {
        if (_behaviorType != BehaviorType.Aggressive)
        {
            ApplySpawnerBehavior(BehaviorType.Aggressive, _aggroHpThreshold, _maxHitsPerMinute);
            if (_debugLog) Debug.Log($"[NpcBrain:{_npcId}] Hostile: attitude={ev.NewValue} < threshold={_hostilityThreshold}");
        }
    }
    else if (_behaviorType == BehaviorType.Aggressive)
    {
        ApplySpawnerBehavior(BehaviorType.Passive, _aggroHpThreshold, _maxHitsPerMinute);
        if (_debugLog) Debug.Log($"[NpcBrain:{_npcId}] Forgave: attitude={ev.NewValue} >= threshold={_hostilityThreshold}");
    }
}

// 4. Модифицированный OnNpcHpChanged (после строки 270):
if (cpt == pt) 
{ 
    _socialBrain.RecordPlayerHit(c.ClientId);
    // T-CNPC-01: портим отношение при ударе
    if (!string.IsNullOrEmpty(_npcId) && ProjectC.Quests.QuestWorld.Instance != null)
        ProjectC.Quests.QuestWorld.Instance.ModifyNpcAttitude(c.ClientId, _npcId, -2);
    break; 
}

// 5. Новый метод респавна:
private void OnNpcDeath(ulong attackerClientId)
{
    if (!IsServer) return;
    // T-CNPC-01: большой штраф к отношению при убийстве
    if (!string.IsNullOrEmpty(_npcId) && ProjectC.Quests.QuestWorld.Instance != null)
        ProjectC.Quests.QuestWorld.Instance.ModifyNpcAttitude(attackerClientId, _npcId, -20);
    
    if (!_respawnEnabled) return;
    StartCoroutine(RespawnCoroutine());
}

private System.Collections.IEnumerator RespawnCoroutine()
{
    // Disable: AI, agent, collider, visual
    _state = BrainState.Dead;
    if (_agent != null) _agent.enabled = false;
    var col = GetComponent<Collider>();
    if (col != null) col.enabled = false;
    // Скрыть визуал (disable renderers)
    foreach (var r in GetComponentsInChildren<Renderer>())
        r.enabled = false;
    
    // Wait
    yield return new WaitForSeconds(_respawnDelaySeconds);
    
    // Reset
    if (_target != null) _target.ResetHealth();
    ResetAggroState();
    _aggroTarget = null;
    _state = BrainState.Idle;
    if (_agent != null) _agent.enabled = true;
    if (col != null) col.enabled = true;
    foreach (var r in GetComponentsInChildren<Renderer>())
        r.enabled = true;
    
    EnterIdle();
}
```

### 10.2 Планируемые изменения в NpcTarget.cs

```csharp
// Новое событие (рядом с OnHpChanged, строка 31):
/// <summary>T-CNPC-01: событие смерти NPC. Параметр: attackerClientId.</summary>
public event Action<ulong> OnKilledEvent;

// В OnKilled (после строки 147):
private void OnKilled(ulong attackerClientId)
{
    // ... existing code (death anim + loot) ...
    
    // T-CNPC-01: fire death event
    OnKilledEvent?.Invoke(attackerClientId);
}

// В ApplyDamage, замена Destroy на респавн (строка 140):
if (newHp == 0)
{
    if (_debugLog) Debug.Log($"[NpcTarget] npc={_targetId} killed.");
    OnKilled(attackerClientId);
    // T-CNPC-01: вместо Destroy — уведомляем NpcBrain о смерти (через event или прямой вызов)
    var brain = GetComponentInParent<ProjectC.AI.NpcBrain>();
    if (brain != null)
        brain.OnNpcDeath(attackerClientId);
    else
        Destroy(gameObject, 3.0f); // fallback для старых врагов без респавна
}
```

---

*Документ создан после полного перечитывания: NpcBrain.cs (1114 строк), NpcTarget.cs (257 строк), NpcAttacker.cs (334 строки), NpcController.cs (130 строк), NpcDefinition.cs (123 строки), FactionDefinition.cs (107 строк), NpcAttitude.cs (55 строк), QuestWorld.cs (1350 строк), NpcSocialBrain.cs (934 строки), SPAWN_TEST.prefab, 02_V2_ARCHITECTURE.md, 09_OPEN_QUESTIONS.md, 11_NPC_SPAWNER_CONFIG_EDITOR.md, docs/Character/Skills/real-time-combat/ (17+ файлов).*
