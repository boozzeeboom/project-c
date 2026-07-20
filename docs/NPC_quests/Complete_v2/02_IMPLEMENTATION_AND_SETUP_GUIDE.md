# 🔧 NPC Complete v2 — Реализация и Инструкция по настройке

> **Дата:** 2026-07-20
> **Тикет:** T-CNPC-01
> **Коммиты:** `f27c857` → `fe83428` → `09cc727`

---

## 1. ЧТО СДЕЛАНО — СВОДКА

### 1.1 Код (2 файла, ~78 строк)

| Файл | Изменение | Строк |
|------|-----------|-------|
| `NpcBrain.cs` | + `_npcId`, `_hostilityThreshold`(-50), `_respawnEnabled`/`_respawnDelaySeconds`/`_maxRespawns`; авто-кэш npcId из NpcController; `ModifyNpcAttitude(-2)` при ударе; подписка на `NpcAttitudeChangedEvent` → смена BehaviorType; `OnNpcDeath` + `RespawnCoroutine` | +70 |
| `NpcTarget.cs` | + `OnKilledEvent` (public); + `ResetHealth()`; замена `Destroy(gameObject, 3f)` → `NpcBrain.OnNpcDeath()` с fallback | +8 |

### 1.2 Ассеты

| Файл | Описание |
|------|----------|
| `Assets/_Project/Resources/Combat/NpcCombatData_Mira.asset` | Новый SO: HP=500, базовые боевые статы |

### 1.3 Сцена — [Mira] в WorldScene_0_0

| Компонент | Назначение |
|-----------|------------|
| `CapsuleCollider` (trigger) | E-key interaction зона |
| `NpcController` → `Mira.asset` | Квесты/диалог (npcId=`mira_01`) |
| `NetworkObject` | NGO спавн |
| `CharacterController` | Физическое тело (как у Goblin) |
| `NavMeshAgent` | AI-навигация |
| `NpcBrain` (Passive) | AI FSM + hostilityThreshold=-50 + respawn |
| `NpcTarget` → `NpcCombatData_Mira` | HP=500 + смерть |
| `NpcAttacker` → `NpcCombatData_Mira` | Нанесение урона |
| `NpcSocialBrain` → `NpcFaction_villagers` | Фракция, grudge, flee, surrender |
| `NetworkTransform` | Сетевая синхронизация позиции |
| `Visual/HumanM_Model` | Модель + Animator (`NpcAnimator_Goblin`) |

---

## 2. ПОЛНЫЙ DATA FLOW

```
Player атакует Mira
       │
       ▼
CombatServer.ResolveAttack()
       │
       ▼
NpcTarget.ApplyDamage(damageResult, attackerClientId)
       │
       ├──► _currentHp.Value -= damage
       ├──► OnHpChanged?.Invoke(newHp, deltaHp)
       │        │
       │        ▼
       │   NpcBrain.OnNpcHpChanged(newHp, deltaHp)
       │       ├── RecordPlayerHit(clientId) → GrudgeTable
       │       ├── [G1] QuestWorld.ModifyNpcAttitude(clientId, _npcId, -2) ← ③
       │       └── thresholdReached → _isAggrod → EnterChase()
       │
       └──► newHp == 0:
                ├── OnKilled(attackerClientId) → SpawnLootPickup
                ├── [G2/G9] OnKilledEvent?.Invoke(attackerClientId)
                └── [G4] NpcBrain.OnNpcDeath(attackerClientId)
                         ├── ModifyNpcAttitude(clientId, _npcId, -20)
                         └── StartCoroutine(RespawnCoroutine)
                                  ├── Disable: agent, collider, renderers
                                  ├── WaitForSeconds(30s)
                                  ├── ResetHealth() + ResetAggroState()
                                  └── Enable + EnterIdle()

Когда NpcAttitude падает < hostilityThreshold(-50):
       │
       ▼
WorldEventBus.NpcAttitudeChangedEvent (NpcId="mira_01", NewValue, Delta)
       │
       ▼
NpcBrain.OnNpcAttitudeChanged(ev) ← [G3]
       ├── if ev.NpcId != _npcId → return
       ├── if ev.NewValue < _hostilityThreshold → ApplySpawnerBehavior(Aggressive)
       └── if ev.NewValue >= _hostilityThreshold → ApplySpawnerBehavior(Passive)
```

---

## 3. ИНСТРУКЦИЯ: КАК НАСТРОИТЬ NPC-ОБЪЕКТ

### 3.1 Чеклист компонентов

Для **квестового NPC** (пассивный + диалог + квесты + боевая система + репутация):

```
GameObject "[NPC_Name]"
├── 🔲 CapsuleCollider (isTrigger=true)          ← E-key interaction
├── 🔲 NpcController → NpcDefinition SO           ← quest/dialog
├── 🔲 NetworkObject                              ← NGO spawn
├── 🔲 CharacterController (h=2, r=0.4)          ← physics body
├── 🔲 NavMeshAgent (speed=3.5, ang=360)          ← AI navigation
├── 🔲 NpcBrain (BehaviorType=Passive)            ← AI FSM
├── 🔲 NpcTarget → NpcCombatData SO               ← HP + death
├── 🔲 NpcAttacker → NpcCombatData SO              ← damage dealing
├── 🔲 NpcSocialBrain → NpcFaction SO              ← faction, grudge
├── 🔲 NetworkTransform (Server authority)         ← network sync
└── 🔲 Visual/HumanM_Model (Animator)              ← 3D model
```

Для **чисто боевого NPC** (без квестов — как Goblin):

```
GameObject "[NPC_Name]"
├── 🔲 NetworkObject
├── 🔲 CharacterController (h=2, r=0.4)
├── 🔲 NavMeshAgent
├── 🔲 NpcBrain (BehaviorType=Aggressive)
├── 🔲 NpcTarget → NpcCombatData SO
├── 🔲 NpcAttacker → NpcCombatData SO
├── 🔲 NpcSocialBrain → NpcFaction SO
├── 🔲 NetworkTransform
└── 🔲 Visual/HumanM_Model (Animator)
```

### 3.2 Пошаговая инструкция (на примере Mira)

#### Шаг 1: Создать NpcDefinition SO

```
Assets → Create → ProjectC → NPC Definition
```

| Поле | Значение |
|------|----------|
| `npcId` | `mira_01` (уникальный, не менять после релиза) |
| `displayName` | `Мира Тихоступ` |
| `faction` | `GuildOfThoughts` |
| `defaultDialogTree` | ссылка на DialogTree |
| `questOffers` | массив QuestId |
| `services` | `Trade` (или что нужно) |
| `interactionRadius` | `3` |

#### Шаг 2: Создать NpcCombatData SO

```
Assets → Create → Project C → Combat → NPC Combat Data
```

| Поле | Значение |
|------|----------|
| `displayName` | `Mira Tihostup` |
| `maxHp` | `500` |
| `strengthTier` | `0` |
| `dexterityTier` | `0` |
| `intelligenceTier` | `0` |
| `damageType` | `Physical` |
| `damageDice` | `d6` |
| `range` | `2.0` |
| `cooldownSeconds` | `1.5` |

#### Шаг 3: Создать GameObject в сцене

1. `GameObject → Create Empty` → назвать `[Mira]`
2. Добавить компоненты по чеклисту (см. §3.1)
3. Настроить:

```yaml
CapsuleCollider:
  isTrigger: true
  radius: 2.5
  height: 3
  center: (0, 1, 0)

NpcController:
  definition: Mira.asset
  interactionDistance: 2.5

CharacterController:
  height: 2
  radius: 0.4
  center: (0, 1, 0)

NavMeshAgent:
  speed: 3.5
  angularSpeed: 360
  stoppingDistance: 2.25

NpcBrain:
  _behaviorType: Passive          # индекс 1
  _hostilityThreshold: -50
  _respawnEnabled: true
  _respawnDelaySeconds: 30
  _attacker: NPC/[Mira]           # ссылка на свой NpcAttacker
  _target: NPC/[Mira]             # ссылка на свой NpcTarget

NpcTarget:
  _data: NpcCombatData_Mira.asset

NpcAttacker:
  _data: NpcCombatData_Mira.asset

NpcSocialBrain:
  faction: NpcFaction_villagers.asset
```

#### Шаг 4: Добавить визуальную модель

1. Создать child `Visual` (empty GameObject)
2. Под `Visual` перетащить `Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx`
3. На HumanM_Model:
   - Добавить `Animator` (если нет)
   - `runtimeAnimatorController` = `Assets/_Project/Animation/AI/NpcAnimator_Goblin.overrideController`
   - `applyRootMotion` = `false`
   - `localPosition` = `(0, 0, 0)`, `localScale` = `(1, 1, 1)`

#### Шаг 5: Верификация в Editor

```
1. Play → Start Host
2. Подойти к Mira → нажать E → открылся диалог
3. Ударить Mira → Console: [QuestWorld] ModifyNpcAttitude player=... npc=mira_01 delta=-2
4. Нанести >25% HP → Mira переходит в Chase/Attack
5. Убить Mira → Console: [NpcBrain:mira_01] ... + ModifyNpcAttitude(-20)
6. Ждать 30с → Mira респавнится (пассивная)
7. Убить несколько раз → NpcAttitude < -50 → Mira агрессивна при подходе
```

### 3.3 Ключевые поля NpcBrain (новые, T-CNPC-01)

| Поле | Тип | Default | Описание |
|------|-----|---------|----------|
| `_npcId` | string | `""` | Авто-заполняется из NpcController.Definition.npcId в OnNetworkSpawn |
| `_hostilityThreshold` | int | `-50` | Порог NpcAttitude для авто-агрессии (Passive→Aggressive) |
| `_respawnEnabled` | bool | `true` | Включить респавн после смерти |
| `_respawnDelaySeconds` | float | `30` | Задержка респавна |
| `_maxRespawns` | int | `0` | Лимит респавнов (0 = безлимитно) |

---

## 4. КЛЮЧЕВЫЕ ФРАГМЕНТЫ КОДА

### 4.1 NpcBrain.OnNpcHpChanged — вызов ModifyNpcAttitude

```csharp
// Строки после RecordPlayerHit:
if (cpt == pt)
{
    _socialBrain.RecordPlayerHit(c.ClientId);
    // T-CNPC-01: портим отношение при ударе
    if (!string.IsNullOrEmpty(_npcId) && QuestWorld.Instance != null)
        QuestWorld.Instance.ModifyNpcAttitude(c.ClientId, _npcId, -2);
    break;
}
```

### 4.2 NpcBrain.OnNpcAttitudeChanged — смена BehaviorType

```csharp
private void OnNpcAttitudeChanged(NpcAttitudeChangedEvent ev)
{
    if (!IsServer) return;
    if (ev.NpcId != _npcId) return;
    if (ev.NewValue < _hostilityThreshold)
        ApplySpawnerBehavior(BehaviorType.Aggressive, _aggroHpThreshold, _maxHitsPerMinute);
    else if (_behaviorType == BehaviorType.Aggressive)
        ApplySpawnerBehavior(BehaviorType.Passive, _aggroHpThreshold, _maxHitsPerMinute);
}
```

### 4.3 NpcBrain.OnNpcDeath — респавн

```csharp
public void OnNpcDeath(ulong attackerClientId)
{
    if (!IsServer) return;
    if (!string.IsNullOrEmpty(_npcId) && QuestWorld.Instance != null)
        QuestWorld.Instance.ModifyNpcAttitude(attackerClientId, _npcId, -20);
    if (!_respawnEnabled) return;
    if (_maxRespawns > 0 && _respawnCount >= _maxRespawns) return;
    StartCoroutine(RespawnCoroutine());
}
```

### 4.4 NpcTarget.ApplyDamage — вместо Destroy

```csharp
if (newHp == 0)
{
    OnKilled(attackerClientId);
    var brain = GetComponent<ProjectC.AI.NpcBrain>();
    if (brain != null)
        brain.OnNpcDeath(attackerClientId);  // респавн
    else
        Destroy(gameObject, 3.0f);           // fallback для старых врагов
}
```

---

## 5. КОММИТЫ

| Хеш | Описание |
|-----|----------|
| `f27c857` | T-CNPC-01: интеграция AI+Quest через репутацию — NpcBrain (+70 строк), NpcTarget (+8 строк), сцена [Mira], NpcCombatData_Mira SO |
| `fe83428` | T-CNPC-01: [Mira] — CharacterController + HumanM_Model visual + Animator |
| `1ea1471` / `09cc727` | T-CNPC-01: документирование итераций |

---

## 6. ПРОВЕРКА ПАТТЕРНА

> «НПС должен быть пассивным, с ним можно поговорить, у него можно взять квесты, если его атаковать — отвечает, при убийстве респавн по таймеру, отношение портится, при плохом отношении — враждебный.»

| Требование | Статус | Реализация |
|------------|--------|------------|
| Пассивный | ✅ | NpcBrain.BehaviorType=Passive |
| Поговорить (E-key) | ✅ | NpcController trigger + CapsuleCollider |
| Дать квест | ✅ | NpcDefinition.questOffers → DialogTree |
| Ответная атака при ударе | ✅ | OnNpcHpChanged → _isAggrod → EnterChase |
| Смерть + респавн | ✅ | OnNpcDeath → RespawnCoroutine (30s) |
| NpcAttitude падает при атаке (-2) | ✅ | ModifyNpcAttitude(clientId, npcId, -2) |
| NpcAttitude падает при убийстве (-20) | ✅ | ModifyNpcAttitude(clientId, npcId, -20) |
| Враждебность при NpcAttitude < -50 | ✅ | OnNpcAttitudeChanged → ApplySpawnerBehavior(Aggressive) |
