# 🧬 Complete v2 NPC — Глубокий Анализ (v2, исправленный)

> **Дата:** 2026-07-29
> **Основание:** чтение реальных .cs файлов (NpcBrain 1114 строк, NpcTarget 257 строк, NpcAttacker 334 строки, NpcController 130 строк, NpcSpawner, NpcSocialBrain)

---

## 1. ЧТО ЕСТЬ НА САМОМ ДЕЛЕ

### 1.1 AI/Combat NPC (то что спавнится через SPAWN_TEST/NpcSpawner)

```
GameObject (создаётся NpcSpawner'ом из префаба)
├─ NetworkObject
├─ NpcBrain : NetworkBehaviour    ← FSM: Idle/Chase/Attack/Dead + BehaviorType
├─ NpcTarget : NetworkBehaviour   ← HP, смерть, лут, OnHpChanged event
├─ NpcAttacker : NetworkBehaviour ← наносит урон через CombatServer
├─ NpcSocialBrain : MonoBehaviour ← faction, patrol, flee, grudge, emotion
├─ NavMeshAgent
└─ Visual (дочерний)
```

**NpcBrain.BehaviorType** — три режима, работают прямо сейчас:
- **Aggressive** — агрится по proximity (aggroRange=10м)
- **Passive** — стоит мирно. При получении урона считает cumulativeDamage%. Когда ≥ aggroHpThreshold (25%) ИЛИ hits/60с ≥ maxHitsPerMinute (3) → `_isAggrod=true` → Chase → Attack. **Уже реализовано.**
- **Neutral** — никогда не атакует

**OnHpChanged в NpcBrain (строка 251-296):**
- Уже записывает обидчика в `_socialBrain.RecordPlayerHit()` (GrudgeTable)
- Уже трекает damage для Passive→Aggro перехода
- **НЕ вызывает** QuestWorld.ModifyNpcAttitude ← вот единственный missing link

**OnKilled в NpcTarget (строка 136-141):**
- Спавнит лут (NpcLootPickup с credits/items)
- `Destroy(gameObject, 3.0f)` ← жёсткий Destroy, нет респавна
- **НЕ вызывает** QuestWorld.ModifyNpcAttitude ← второй missing link

### 1.2 Quest/Dialogue NPC (Mira в WorldScene_0_0)

```
[Mira] GameObject
├─ NpcController : MonoBehaviour  ← триггер, NpcDefinition ref, Cube-визуал
├─ CapsuleCollider (isTrigger)
└─ TextMeshPro (имя над головой)
```

**NpcController**
- **MonoBehaviour** (не NetworkBehaviour)
- **НЕТ** NetworkObject
- **НЕТ** NpcBrain, NpcTarget, NpcAttacker
- E-key взаимодействие: `NetworkPlayer.TryInteractNearestNpc()` → `FindObjectsByType<NpcController>()` → `QuestServer.RequestTalkToNpcRpc(npc.NpcId)`

### 1.3 Что такое SPAWN_TEST.prefab

```
SPAWN_TEST (корень)
├─ NetworkObject
├─ NavMeshSurface (для bake NavMesh)
├─ NpcSpawner : NetworkBehaviour
│   └─ _config: NpcSpawner_Default.asset
└─ Дочерние объекты (спавнер префабов)
```

Это **НЕ** префаб NPC. Это GameObject со спавнером, который создаёт NPC из префаба `NpcSpawnerConfig.npcPrefab`. Префаб конкретного NPC (например `Npc_Goblin.prefab`) лежит отдельно и содержит NpcBrain+NpcTarget+NpcAttacker.

---

## 2. РАЗБОР ПАТТЕРНА ПОЛЬЗОВАТЕЛЯ

> НПС пассивный, можно поговорить, даёт квесты, при атаке отвечает, при убийстве респавн по таймеру/событию/никогда, отношение портится, при плохом отношении — враждебный.

### 2.1 Что УЖЕ работает

| Требование | Статус | Где |
|------------|--------|-----|
| Пассивный NPC, стоит на месте | ✅ | NpcBrain.BehaviorType.Passive — не агрится по proximity |
| Поговорить (E-key) | ✅ | NpcController + QuestServer.RequestTalkToNpcRpc |
| Диалог с опциями | ✅ | DialogWindow + DialogTree + typewriter |
| Взять квест | ✅ | DialogueAction.OfferQuest → QuestWorld.TryAccept |
| Ответная атака при ударе | ✅ | NpcBrain.OnNpcHpChanged → _isAggrod → Chase → Attack |
| Порог агрессии (hpThreshold) | ✅ | `_aggroHpThreshold = 25%` |
| Порог агрессии (hits) | ✅ | `_maxHitsPerMinute = 3` |
| Смерть и лут | ✅ | NpcTarget.OnKilled → loot + Destroy через 3с |
| NpcAttitude (отношение к NPC) | ✅ | QuestWorld.ModifyNpcAttitude / GetNpcAttitude |
| Cross-faction влияние | ✅ | NpcDefinition.attitudeLinks в ModifyNpcAttitude |
| Фракции (AI) | ✅ | NpcSocialBrain.faction (NpcFaction SO) |
| Grudge-память | ✅ | NpcSocialBrain.enableGrudgeMemory → GrudgeTable |

### 2.2 Что НЕ работает (конкретные gap'ы)

| # | Gap | Где именно | Что сделать |
|---|-----|-----------|-------------|
| **G1** | Урон по NPC не меняет NpcAttitude | NpcBrain.OnNpcHpChanged (строка 251) — не вызывает ModifyNpcAttitude | Добавить вызов |
| **G2** | Смерть NPC не меняет NpcAttitude | NpcTarget.OnKilled (строка 147) — не вызывает ModifyNpcAttitude | Добавить вызов |
| **G3** | NpcAttitudeChanged не влияет на BehaviorType | Никто не подписан на WorldEventBus.NpcAttitudeChangedEvent для смены BehaviorType | Добавить подписку |
| **G4** | На [Mira] нет NetworkObject | Нельзя повесить NetworkBehaviour (NpcBrain и т.д.) | Добавить NetworkObject |
| **G5** | NpcTarget.Destroy — финальный | `Destroy(gameObject, 3.0f)` навсегда удаляет scene-placed NPC | Добавить респавн-логику |
| **G6** | Квестовые ассеты утеряны | DEEP_AUDIT: 0 FactionDefinition, 0 NpcDefinition, 0 QuestDefinition .asset файлов | Восстановить из CSV или git |

---

## 3. КАК ЭТО ЧИНИТСЯ (минимальными изменениями)

### 3.1 Gap G1: Урон → NpcAttitude

В `NpcBrain.OnNpcHpChanged` (файл `Assets/_Project/Scripts/AI/NpcBrain.cs`, строка 251), после строки 274 (`}`) добавить:

```csharp
// T-CNPC-01: урон от игрока → портим NpcAttitude
if (_socialBrain != null && _socialBrain.faction != null)
{
    // Найти кто атаковал (ближайший игрок)
    var pt = FindNearestPlayerTarget(aggroRange * 3f) as PlayerTarget;
    if (pt != null && NetworkManager.Singleton != null)
    {
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (c?.PlayerObject == null) continue;
            var cpt = c.PlayerObject.GetComponent<PlayerTarget>();
            if (cpt == pt && QuestWorld.Instance != null)
            {
                string npcId = _socialBrain.faction.name; // или другой ID
                QuestWorld.Instance.ModifyNpcAttitude(c.ClientId, npcId, -2);
                break;
            }
        }
    }
}
```

### 3.2 Gap G2: Смерть → NpcAttitude

Вызывать ModifyNpcAttitude с большим штрафом при смерти.

### 3.3 Gap G3: NpcAttitude → BehaviorType

Подписаться на `WorldEventBus.NpcAttitudeChangedEvent`. Если значение < порога → `NpcBrain.ApplySpawnerBehavior(Aggressive)`.

### 3.4 Gap G4: NetworkObject на Mira

Добавить `NetworkObject` компонент на GameObject [Mira]. Зарегистрировать через `ScenePlacedObjectSpawner` (как все объекты в BootstrapScene). Затем повесить `NpcBrain` (Passive), `NpcTarget`, `NpcAttacker`. Всё.

### 3.5 Gap G5: Респавн

Два пути:
- **Простой:** убрать Destroy из NpcTarget.OnKilled, вместо него — отключить визуал, коллайдер, AI, и через N секунд включить обратно с полным HP.
- **Существующий:** использовать NpcSpawner в режиме `FiniteCycle` + `ISpawnRestartTrigger` — уже написано, работает для спавна врагов. Адаптировать для scene-placed NPC.

---

## 4. ОТВЕТ НА ВОПРОС: «что мешает накинуть скрипты на 1 объект?»

**Ничего.** Технически можно прямо сейчас:

1. Открыть `WorldScene_0_0`
2. Найти [Mira]
3. Добавить `NetworkObject`
4. Добавить `NpcBrain` → поставить BehaviorType = Passive
5. Добавить `NpcTarget` → указать NpcCombatData
6. Добавить `NpcAttacker` → указать NpcCombatData
7. Добавить `NpcSocialBrain` → указать NpcFaction
8. Добавить `NavMeshAgent`

Mira будет: стоять, говорить через E, получать урон, агриться после 25% HP и давать сдачи.

**Единственное, что реально нужно дописать:**
- 3-5 строк в `NpcBrain.OnNpcHpChanged` → ModifyNpcAttitude
- 3-5 строк где-то → подписка на NpcAttitudeChanged → смена BehaviorType
- Логика респавна (вместо Destroy)

**Это не архитектурная задача. Это точечные правки.**

---

## 5. ПЛАН РЕАЛИЗАЦИИ (реальный, без лишнего)

| # | Что | Файлы | Минут |
|---|-----|-------|-------|
| 1 | Добавить ModifyNpcAttitude в NpcBrain.OnNpcHpChanged | NpcBrain.cs (+5 строк) | 15 |
| 2 | Добавить ModifyNpcAttitude в NpcTarget.OnKilled | NpcTarget.cs (+5 строк) | 10 |
| 3 | Подписка NpcAttitudeChanged → ApplySpawnerBehavior(Aggressive) | NpcBrain.cs или новый микро-компонент (+20 строк) | 20 |
| 4 | Респавн вместо Destroy | NpcTarget.cs (замена Destroy на disable+coroutine) | 30 |
| 5 | Собрать префаб NPC_Quest (NetworkObject + все компоненты) | Editor | 20 |
| 6 | Заменить [Mira] в сцене на новый префаб | WorldScene_0_0 | 10 |
| 7 | Восстановить квестовые ассеты | CSV/Editor | 30 |

**Итого: ~2.5 часов**

---

## 6. ФАЙЛЫ

### Изменяемые

| Файл | Что | Строк |
|------|-----|-------|
| `AI/NpcBrain.cs` | + ModifyNpcAttitude в OnNpcHpChanged | +5 |
| `Combat/NpcTarget.cs` | + ModifyNpcAttitude в OnKilled; замена Destroy на респавн | +10 |
| Сцена `WorldScene_0_0.unity` | Замена Mira на новый префаб | — |

### Новые (опционально)

| Файл | Роль |
|------|------|
| `Prefabs/NPC/NPC_Quest.prefab` | Собранный префаб: NetworkObject + все компоненты |
| `AI/NpcAttitudeBridge.cs` | Альтернатива правкам в NpcBrain — отдельный компонент-подписчик |

---

## 7. ЧТО БЫЛО НЕВЕРНО В ПЕРВОМ АНАЛИЗЕ

| Было сказано | Почему неверно |
|--------------|----------------|
| «NpcController должен стать NetworkBehaviour» | Не должен. NpcController может остаться MonoBehaviour на том же GO, где NetworkBehaviours |
| «Нужен NpcController v2 с нуля» | Не нужен. Старый работает, достаточно добавить компоненты рядом |
| «AttitudeCombatBridge — отдельный компонент на 80 строк» | Можно, но проще добавить 5 строк в существующий NpcBrain |
| «Нужна унификация фракций» | Мост NpcFaction↔FactionId полезен, но не блокирует — можно использовать npcId строкой |
| «12-14 часов на P0» | ~2.5 часа реальной работы |

---

*Анализ переписан после перечитывания реального кода.*
