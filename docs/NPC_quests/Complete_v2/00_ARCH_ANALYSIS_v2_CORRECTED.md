# 🔴 NPC Complete v2 — Исправленный Архитектурный Анализ

**Дата:** 2025-07-29 | **Директория:** `docs/NPC_quests/Complete_v2/`

---

## 1. РЕАЛЬНОЕ СОСТОЯНИЕ КОДА

### 1.1 NpcBrain (1114 строк, `Assets/_Project/Scripts/AI/NpcBrain.cs`)

```
Класс: public class NpcBrain : NetworkBehaviour
Зависимости: [RequireComponent(typeof(NavMeshAgent))]
```

| Свойство | Строки | Назначение |
|----------|--------|------------|
| `BehaviorType` (Aggressive/Passive/Neutral) | 63-73 | Режим поведения |
| `OnNpcHpChanged(int newHp, int deltaHp)` | **251-296** | **🔥 КЛЮЧЕВОЙ МЕТОД** |
| `FindNearestPlayerTarget(float range)` | 1026-1040 | Поиск ближайшего игрока |
| `FindNearestHostileTarget(float range)` | 1046-1081 | Поиск hostile (игроки + faction NPC) |
| `ForceChaseTarget(IDamageTarget)` | 506-523 | Social bridge API |
| `ForceFlee(Vector3)` | 525-552 | Social bridge API |
| `ApplySpawnerBehavior(...)` | 197-204 | Переопределение BehaviorType в рантайме |
| `_socialBrain.RecordPlayerHit(c.ClientId)` | **270** | ✅ Запись обидчика в GrudgeTable |
| `_isAggrod` / `_aggroDamageAccumulator` | 172-174 | Трекинг урона для Passive→Aggro |

**OnNpcHpChanged (строки 251-296) — подробно:**

```csharp
// Строки 258-274: УЖЕ работает поиск атакующего игрока + clientId:
if (_socialBrain != null && _socialBrain.enableGrudgeMemory)
{
    var nearestPlayer = FindNearestPlayerTarget(aggroRange * 3f);
    if (nearestPlayer is ProjectC.Combat.PlayerTarget pt)
    {
        if (NetworkManager.Singleton != null)
        {
            foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (c?.PlayerObject == null) continue;
                var cpt = c.PlayerObject.GetComponent<ProjectC.Combat.PlayerTarget>();
                if (cpt == pt) { _socialBrain.RecordPlayerHit(c.ClientId); break; }
                //                           ↑ c.ClientId известен здесь!
            }
        }
    }
}
// Строки 276-296: пассивный NPC → проверка порога агрессии
if (_behaviorType != BehaviorType.Passive) return;
// ... трекинг урона, _isAggrod = true при превышении порога
```

**Клиентский ID игрока-атакующего уже вычислен на строке 270.** Рядом с `RecordPlayerHit` нужно добавить вызов `ModifyNpcAttitude`.

### 1.2 NpcTarget (257 строк, `Assets/_Project/Scripts/Combat/Implementations/NpcTarget.cs`)

```csharp
public class NpcTarget : NetworkBehaviour, IDamageTarget
```

| Свойство | Строки | Назначение |
|----------|--------|------------|
| `OnHpChanged` event | 31 | `Action<int, int>` — (newHp, deltaHp) |
| `ApplyDamage(DamageResult, ulong attackerClientId)` | 94-142 | **attackerClientId известен** |
| `OnKilled(ulong attackerClientId)` | **147** | **private** — death handler |
| `Destroy(gameObject, 3.0f)` | **140** | 🔴 Жёсткий Destroy |
| `SpawnLootPickup(attackerClientId)` | 183-255 | Спавн лута (credits + items) |

**Проблема:** `OnKilled` — `private`. Нет внешнего события/колбэка для смерти NPC.

### 1.3 NpcAttacker (334 строки, `Assets/_Project/Scripts/Combat/Implementations/NpcAttacker.cs`)

Полноценный `IAttacker`. Навыки (SkillNodeConfig), кулдауны, мульти-сорс.

### 1.4 NpcController (130 строк, `Assets/_Project/Quests/NpcController.cs`)

```csharp
[DisallowMultipleComponent, RequireComponent(typeof(Collider))]
public class NpcController : MonoBehaviour  // ← НЕ NetworkBehaviour
```

- Trigger collider (`isTrigger`)
- `NpcDefinition definition` (npcId, displayName, factionId)
- Cube-визуал (плейсхолдер)
- **НЕТ** NetworkObject, NpcBrain, NpcTarget, NpcAttacker

### 1.5 QuestWorld.ModifyNpcAttitude (строки 282-323)

```csharp
public int ModifyNpcAttitude(ulong clientId, string npcId, int delta, 
    NpcDefinition npcDef = null, QuestDatabase database = null)
```

- Публикует `NpcAttitudeChangedEvent` (строка 313)
- Cross-faction influence (строки 301-311)
- ✅ Работает, используется в диалогах (QuestServer строка 1540)

---

## 2. ЧТО УЖЕ РАБОТАЕТ (проверено по коду)

| Возможность | Статус | Доказательство |
|-------------|--------|----------------|
| Passive NPC (не агрится сам) | ✅ | NpcBrain:276 — `if (_behaviorType != BehaviorType.Passive) return;` |
| E-key → диалог | ✅ | NetworkPlayer.TryInteractNearestNpc → FindObjectsByType<NpcController> |
| Диалоги + выдача квестов | ✅ | QuestServer.FireDialogAction → QuestWorld.TryOffer |
| NpcAttitude система | ✅ | QuestWorld.ModifyNpcAttitude + NpcAttitudeChangedEvent |
| Cross-faction influence | ✅ | QuestWorld:301-311 — attitudeLinks |
| Ответная атака при получении урона | ✅ | NpcBrain:285-295 — `_isAggrod = true` → EnterChase |
| Поиск атакующего игрока + clientId | ✅ | NpcBrain:260-270 — `FindNearestPlayerTarget` → foreach → `c.ClientId` |
| GrudgeTable (память обидчиков) | ✅ | NpcBrain:270 — `RecordPlayerHit(c.ClientId)` |
| Респавн (для спавнеров) | ✅ | NpcSpawner + ISpawnRestartTrigger |
| Боевая система (урон, криты, тип) | ✅ | CombatServer + PlayerAttacker + NpcAttacker |
| Навыки NPC (AOEs, Throwable, Ranged) | ✅ | NpcSkillSet + NpcSkillDamageSource |
| Движущиеся платформы | ✅ | NpcBrain:307-500 — ShipDeckNav carry |

---

## 3. ТОЧНЫЕ GAP'ы (5 штук)

### 🔴 Gap 1: OnNpcHpChanged не вызывает ModifyNpcAttitude

**Где:** `NpcBrain.cs`, строка 270 (после `RecordPlayerHit`)

**Что:** `clientId` УЖЕ известен. `RecordPlayerHit` УЖЕ вызывается. Но `ModifyNpcAttitude` — нет.

**Фикс (3 строки):**
```csharp
// После строки 270, внутри того же блока if:
if (cpt == pt) 
{ 
    _socialBrain.RecordPlayerHit(c.ClientId);
    // 🔥 добавить:
    if (QuestWorld.Instance != null)
        QuestWorld.Instance.ModifyNpcAttitude(c.ClientId, _socialBrain.faction.npcId, -2);
    break; 
}
```

Но нужно понять какой `npcId` использовать. В `NpcController` есть `definition.npcId`. В `NpcBrain` — пока нет. Нужно либо:
- Добавить `_npcId` поле в `NpcBrain` (1 строка + инициализация)
- Или использовать `_socialBrain.faction.name` как npcId

### 🔴 Gap 2: OnKilled не вызывает ModifyNpcAttitude

**Где:** `NpcTarget.cs`, строка 147 (`OnKilled`)

**Что:** `attackerClientId` известен, но метод приватный. Нужен либо:
- Сделать `OnKilled` публичным/внутренним
- Добавить `public event Action<ulong> OnKilledEvent`

**Фикс:** заменить `private void OnKilled` → `internal void OnKilled` ИЛИ добавить event.

### 🔴 Gap 3: Нет подписки на NpcAttitudeChanged → смена BehaviorType

**Где:** `NpcBrain.cs`, метод `OnNetworkSpawn` (строка 213)

**Фикс:** в OnNetworkSpawn добавить:
```csharp
if (_socialBrain != null && !string.IsNullOrEmpty(_npcId))
{
    WorldEventBus.Subscribe<NpcAttitudeChangedEvent>(ev =>
    {
        if (ev.NpcId != _npcId) return;
        if (ev.NewValue < _hostilityThreshold)
            _behaviorType = BehaviorType.Aggressive;
    });
}
```

### 🔴 Gap 4: NpcTarget.Destroy — финальный (нет респавна)

**Где:** `NpcTarget.cs`, строка 140 — `Destroy(gameObject, 3.0f)`

**Для scene-placed NPC (Mira):** нужен респавн вместо Destroy:
- Вариант А: заменить Destroy на disable+coroutine → через N секунд enable с полным HP
- Вариант Б: сделать отдельный `NpcRespawnController : NetworkBehaviour`

### 🔴 Gap 5: На [Mira] нет NetworkObject

**Где:** `WorldScene_0_0.unity`, GameObject [Mira]

**Что:** `NpcController : MonoBehaviour` не может жить с `NpcBrain : NetworkBehaviour` без NetworkObject.

**Фикс:** добавить `NetworkObject` на [Mira].

---

## 4. АРХИТЕКТУРНАЯ СХЕМА (реальная)

```
                   ┌─────────────────────────┐
                   │      NetworkPlayer       │
                   │  TryInteractNearestNpc() │
                   │  FindObjectsByType<>()   │
                   └───────────┬─────────────┘
                               │ E-key
                               ▼
                   ┌─────────────────────────┐
                   │     NpcController        │  ← MonoBehaviour (без Network)
                   │  NpcDefinition.npcId     │
                   │  trigger collider        │
                   └─────────────────────────┘
                               │
                              тот же GameObject
                               │
        ┌──────────────────────┼──────────────────────┐
        ▼                      ▼                      ▼
┌──────────────┐   ┌──────────────────┐   ┌──────────────────┐
│   NpcBrain   │   │    NpcTarget     │   │   NpcAttacker    │
│ NetworkBhvr  │   │   NetworkBhvr    │   │   NetworkBhvr    │
├──────────────┤   ├──────────────────┤   ├──────────────────┤
│ BehaviorType │◄──│ OnHpChanged evt  │   │ IAttacker        │
│ Passive=не   │   │ ApplyDamage()    │   │ GetDamageSource()│
│ агрится      │   │ OnKilled() priv  │   │ NpcSkillSet      │
│              │   │ Destroy(3f) 🔴   │   │                  │
│ OnHpChanged──│──►│                  │   │                  │
│ • ищет clientId (✅)                │   │                  │
│ • RecordPlayerHit (✅)              │   │                  │
│ • ModifyNpcAttitude? (🔴 G1)       │   │                  │
└──────┬───────┘   └──────────────────┘   └──────────────────┘
       │
       │ нужно добавить: QuestWorld.ModifyNpcAttitude(clientId, npcId, delta)
       ▼
┌──────────────────┐
│    QuestWorld    │
│ ModifyNpcAttitude│────► NpcAttitudeChangedEvent ──► ??? (G3: никто не слушает)
│ GetNpcAttitude   │
└──────────────────┘
```

---

## 5. ПЛАН РЕАЛИЗАЦИИ (реальный, без лишнего)

| Шаг | Файл | Что | Строк |
|-----|------|-----|-------|
| 1 | `NpcBrain.cs` | + поле `_npcId` (string), иниц в OnNetworkSpawn | +4 |
| 2 | `NpcBrain.cs` | В OnNpcHpChanged после RecordPlayerHit → ModifyNpcAttitude | +3 |
| 3 | `NpcTarget.cs` | `OnKilled` → internal + вызов ModifyNpcAttitude (или event) | +5 |
| 4 | `NpcBrain.cs` | В OnNetworkSpawn → подписка на NpcAttitudeChangedEvent | +12 |
| 5 | `NpcTarget.cs` | Замена `Destroy(gameObject, 3f)` на дизабл + корутина респавна | +15 |
| 6 | `WorldScene_0_0` | На [Mira]: добавить NetworkObject + NpcBrain + NpcTarget + NpcAttacker | 0 строк |

**Итого: ~40 строк нового кода, 0 новых файлов.**

### Что НЕ нужно делать:
- ❌ Переписывать NpcController на NetworkBehaviour
- ❌ Создавать "NpcController v2"
- ❌ Создавать отдельный "AttitudeCombatBridge" компонент
- ❌ Унифицировать NpcFaction и FactionId (пока)
- ❌ Менять архитектуру NpcBrain (всё уже на месте)

---

## 6. ПОЧЕМУ ПЕРВЫЙ АНАЛИЗ БЫЛ НЕВЕРЕН

| Было сказано | Ошибка | Реальность |
|--------------|--------|------------|
| «NpcTarget не имеет NpcBrain» | Перепутал направление | NpcBrain сам находит NpcTarget через GetComponent |
| «NpcController должен быть NetworkBehaviour» | Избыточно | Можно добавить NetworkBehaviours рядом |
| «Нужен NpcController v2» | Придумал лишнее | Старый NpcController работает |
| «AttitudeCombatBridge — новый компонент» | Не заметил OnNpcHpChanged | Логика уже может жить в существующем методе |
| «OnHpChanged не передаёт attackerClientId» | Невнимательно прочитал | Метод ApplyDamage() имеет параметр attackerClientId |
| «3 новых компонента, 80 строк» | Переоценил | 0 новых файлов, ~40 строк правок |

---

*Составлено после полного перечитывания: NpcBrain.cs (1114 строк), NpcTarget.cs (257 строк), NpcAttacker.cs (334 строки), NpcController.cs (130 строк), QuestWorld.cs, WorldEvent.cs*
