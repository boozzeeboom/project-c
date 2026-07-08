# Анализ расширения поведенческой модели NPC: социальные паттерны «человека социального»

> **Сессия:** 2026-07-08
> **Базируется на:** `NpcBrain.cs` (FSM v0.3), `70_NPC_ENEMIES.md` (дизайн NPC), `02_LORE.md` (лор-база), `10_DESIGN.md` (архитектура движка)
> **Статус:** Research-only. Код не пишется.
>
> **Запрос пользователя:** текущее поведение NPC (Aggressive/Passive/Neutral) примитивно. Нужен глубокий анализ и ресерч на тему расширения — более реалистичные паттерны для «человека социального».

---

## 0. Контекст: файл `02_SOCIAL_HUMAN_BEHAVIOR.md`

Файл `docs/Character/Skills/real-time-combat/npc-enemy/02_SOCIAL_HUMAN_BEHAVIOR.md` **не найден на диске** (поиск `*SOCIAL*`, `*02_SOCIAL*`, `SOCIAL_HUMAN_*` — 0 результатов во всех директориях проекта). Пользователь ссылается на существование отчёта — вероятно, он был утерян при перестройке документации или пребывает в другой ветке.

Настоящий документ — независимый анализ. Сопоставление с утерянным отчётом невозможно, поэтому §7 содержит заключение без сравнения.

---

## 1. Текущее состояние: что есть сейчас

### 1.1 `NpcBrain.cs` — Finite State Machine

```
[Idle]   ←─── player in aggroRange (10m) AND aggrod ───→ [Chase]
[Chase]  ←─── dist <= attackRange (2.5m) ──────────────→ [Attack]
[Chase]  ←─── dist > leashRange (40m) ─────────────────→ [Idle] (return to spawn)
[Attack] ←─── cooldown + dist <= attackRange ──────────→ [Attack]

[Any]    ←─── HP <= 0 ──────────────────────────────────→ [Dead]
```

### 1.2 3 behavior type (T-NPC-14)

| Тип | Что делает | Когда агрится |
|---|---|---|
| **Aggressive** | Бежит к игроку | По proximity (aggroRange) |
| **Passive** | Стоит мирно | Только после удара (cumulative HP% >= 25% ИЛИ 3 hits/min) |
| **Neutral** | Никогда не атакует | — |

### 1.3 Что НЕ реализовано (gaps)

- ❌ **Группирование** — NPC не координируются, не помогают друг другу
- ❌ **Flee / отступление** — нет, NPC бьётся до смерти
- ❌ **Страх / мораль** — нет, NPC не боятся смерти
- ❌ **Коммуникация** — нет жестов, криков, сигналов
- ❌ **Faction-система** — нет фракций (все враги — «одинаковые красные точки»)
- ❌ **Реакция на смерть союзника** — нет, труп игнорируется
- ❌ **Emotional state** — нет состояний (ярость, страх, удивление)
- ❌ **Patrol / idle-activity** — нет, Idle = стоять на месте
- ❌ **Surrender / mercy** — нет, NPC не сдаётся
- ❌ **Territorial awareness** — нет, leash привязан к spawnPoint, не к территории
- ❌ **Memory** — NPC не помнит игрока после возврата в Idle

---

## 2. Теоретическая база: социально-психологические модели для NPC

### 2.1 Теория социальной идентичности (Tajfel & Turner, 1979)

**Суть:** люди определяют себя через групповую принадлежность. «Свои» получают привилегии, «чужие» — дискриминацию. При угрозе группе — солидарность растёт.

**Применение к NPC:**
- NPC одной фракции помогают друг другу (базовое группирование — уже частично в `70_NPC_ENEMIES.md §2.3`)
- **Внутригрупповой фаворитизм:** NPC-члены одной гильдии защищают друг друга
- **Внешнегрупповая враждебность:** NPC разных фракций могут быть нейтральны или враждебны друг другу
- **Эскалация при угрозе:** чем больше союзников ранено/убито, тем яростнее сражаются оставшиеся

### 2.2 Эффект свидетеля (Darley & Latané, 1968)

**Суть:** чем больше людей вокруг, тем меньше вероятность, что конкретный человек вмешается — «диффузия ответственности».

**Применение к NPC:**
- Одиночный NPC → высокая готовность к атаке
- NPC в группе (3+) → ниже готовность к индивидуальной атаке, но выше к координированной
- **Триггер:** если один NPC в группе атакован — остальные могут замешкаться (0.5-1.5 сек) перед реакцией
- Если атакован **лидер** группы → мгновенная реакция всех (эффект сломался)

### 2.3 Теория управления страхом смерти (Greenberg et al., 1986)

**Суть:** осознание смертности усиливает как агрессию (к «чужим»), так и просоциальное поведение (к «своим»).

**Применение к NPC:**
- NPC при HP < 30% (при смерти):
  - Вариант A: **отчаянная атака** (+damage buff, +speed) — «смерти нет, есть только честь»
  - Вариант B: **бегство** — страх смерти берёт верх
  - Выбор зависит от personality trait: Courage (0..1)
- Наблюдение смерти союзника → триггер «осознания смертности» для остальных NPC

### 2.4 Теория справедливости / retaliation (Adams, 1965)

**Суть:** люди стремятся к справедливости. Если кто-то причинил ущерб — жертва стремится к возмездию.

**Применение к NPC:**
- **Tracking retaliaton:** NPC запоминает, кто его ударил (не просто «игрок рядом», а конкретный `clientId`)
- **Retaliation priority:** если NPC атакован двумя игроками, выбирает того, кто нанёс больше урона
- **Grudge memory:** NPC помнит обидчика даже после возврата в Idle (persistent aggro на конкретного игрока)

### 2.5 Иерархия доминирования (Chase et al., 2002)

**Суть:** в группах формируется иерархия. Альфа-особи получают приоритетный доступ к ресурсам.

**Применение к NPC:**
- В группе NPC один — **лидер** (высший HP или специальный флаг)
- Лидер атакует первым, остальные следуют за ним
- При смерти лидера — **борьба за лидерство** среди оставшихся (2-3 сек «растерянности» или immediate rage)
- **Формирование:** можно назначать лидера случайно при спавне группы или по `NpcSpawnerConfig`

### 2.6 Reciprocal altruism (Trivers, 1971)

**Суть:** «ты мне — я тебе». Кооперация на взаимовыгодных условиях.

**Применение к NPC:**
- NPC может **помочь другому NPC** (не только атакой, но и отвлечением/флангом)
- После боя NPC «запоминает», помог ли игрок NPC (например, спас от падения) → понижение агрессии
- **Лимитация:** не приоритет для MVP, но архитектурный hook для future

---

## 3. Предлагаемые расширения поведенческой модели

### 3.1 Emotion System: базовый аффективный слой

```csharp
// Новая структура: NpcEmotionState
public enum NpcEmotion {
    Calm,       // базовое состояние
    Alert,      // заметил игрока, но не агрессивен
    Fear,       // HP низкий или рядом смерть союзника
    Anger,      // агрессия (накопленный урон)
    Despair,    // окружён, HP=0 (surrender?)
    Victory,    // только что убил цель
}
```

| Emotion | Trigger | Effect on behavior |
|---|---|---|
| **Calm** | Idle, нет угрозы | Default idle activity (patrol, look around, etc.) |
| **Alert** | Игрок в visualRange (2× aggroRange) | Head-tracking, замедление патруля, vocal cue |
| **Fear** | HP < 30% | Higher flee chance, lower attack accuracy |
| **Fear** | Nearby ally death | Hesitation (0.5s delay before next action) |
| **Anger** | Aggrod + damage taken | +5% damage, -10% accuracy (rage tax) |
| **Anger** | Ally killed nearby | Forced aggro on killer (override leash) |
| **Despair** | HP < 10% + outnumbered | 30% chance to surrender |
| **Victory** | Target killed | 2s taunt animation, then search for next target |

### 3.2 Социальные триггеры (новые state transition conditions)

Текущие триггеры: `aggroRange` (proximity) + `cumulativeDamage` (passive) + `aggroHpThreshold` (passive).

**Новые триггеры:**

| Триггер | Суть | Условие срабатывания | Приоритет над существующими |
|---|---|---|---|
| **AllyInCombat** | Союзник NPC атакован | `FindAlliesInRadius(15m).Any(a => a.IsAggrod)` | Выше proximity (союзник зовёт на помощь) |
| **AllyKilled** | Союзник умер рядом | `FindAlliesInRadius(20m).Any(a => a.LastDeathTime > now - 5s)` | Самый высокий (месть) |
| **LeaderAggrod** | Лидер группы в Chase | `GroupLeader?.IsAggrod == true` | Выше proximity (следовать за лидером) |
| **Outnumbered** | Игроков > NPC × 1.5 | `PlayerCount > AllyCount * 1.5` | Понижает порог fear, повышает flee |
| **ReinforcementNearby** | Дружественные NPC в 30м | `FriendliesInRadius(30m).Count > 0` | Повышает courage, ниже flee chance |
| **TerritoryViolation** | Игрок вошёл в запретную зону | Триггер-зона (вроде области с `territoryId`) | Работает даже для Passive NPC |
| **GrudgeTrigger** | Игрок ранее атаковал NPC | `_grudgeTable.Contains(attackerId)` | Persistent aggro на конкретного игрока |

### 3.3 Group coordination layer (выше существующего FSM)

Текущий код не группирует NPC. `70_NPC_ENEMIES.md §2.3` упоминает группирование («NPC одного типа помогают друг другу») — это **не реализовано** в `NpcBrain.cs`.

**Предлагаемая архитектура:**

```
[NpcBrain]          ← per-NPC FSM (существующий)
     │
     ▼
[NpcGroupController] ← один на группу (сервер-side hub)
     │
     ▼
[GroupFormation]    ← formation, roles (lead/scout/fighter)
     │
     ▼
[GroupTactical]     ← flanking, retreat, surround
```

**NpcGroupController** (минимальный):

```csharp
public class NpcGroupController {
    public List<NpcBrain> Members;
    public NpcBrain Leader;
    public Vector3 GroupCenter;
    public int AliveCount => Members.Count(m => m.CurrentState != NpcBrainState.Dead);
    
    public void OnMemberAggrod(NpcBrain member, IDamageTarget target);
    public void OnMemberKilled(NpcBrain member, ulong killerId);
    public void BroadcastAggro(IDamageTarget target); // вся группа агрится
    public void OrderRetreat(Vector3 fallbackPoint);
}
```

**MVP для группирования (без формаций):**
1. `NpcSpawner` при спавне нескольких NPC в одном радиусе назначает им `groupId` (Guid)
2. `NpcBrain` через `NpcGroupController` узнаёт о состоянии союзников
3. `AllyInCombat` триггер: если один NPC атакован → вся группа в радиусе 15м агрится
4. `AllyKilled` триггер: rage buff для оставшихся

### 3.4 Fear / Morale system

```csharp
[System.Serializable]
public class NpcMoraleData {
    public float baseCourage = 0.7f;     // 0..1, чем выше — тем смелее
    public float morale = 1.0f;          // current morale multiplier
    public float fearDecayRate = 0.1f;   // per second, восстанавливается после выхода из боя
    
    // Модификаторы
    private int _alliesAlive;
    private int _alliesDeadInRadius;
    private float _hpPercent;
    private bool _outnumbered;
    private bool _leaderAlive;
    
    public bool ShouldFlee => morale < 0.3f && _hpPercent < 0.5f;
    public bool ShouldSurrender => morale < 0.15f && _hpPercent < 0.15f;
    public float AttackDamageMultiplier => Mathf.Lerp(0.5f, 1.2f, morale);
    public float MoveSpeedMultiplier => morale < 0.5f ? 1.3f : 1.0f; // бегут быстрее в страхе
}
```

**Факторы, снижающие morale:**
- Наблюдение смерти союзника: `-0.2` (stackable, но не ниже 0.1)
- Получение урона: `-0.05 × (damage / maxHp)`
- Аутнамбред: `-0.15`
- Смерть лидера: `-0.3` (все члены группы)

**Факторы, повышающие morale:**
- Лидер рядом: `+0.1`
- Reinforcement рядом: `+0.15`
- Победа (убил цель): `+0.3`
- Успешный отход: `+0.1`

### 3.5 Communication / Vocal cues (server-authoritative, animation-driven)

NPC не кричат текстом — это анимация + лог на сервере.

| Cue | Когда | Gameplay effect |
|---|---|---|
| **AlertCall** | NPC переходит в Chase | Привлекает NPC в 15м (триггер AllyInCombat) |
| **DeathScream** | NPC умирает | Triggers AllyKilled у NPC в 20м |
| **Taunt** | NPC только что атаковал | Дебафф target (опционально, Phase 2) |
| **FearCry** | NPC в Fear (HP < 30%) | Понижает morale союзников (отрицательная обратная связь) |
| **VictoryRoar** | NPC убил цель | Повышает morale союзников, снижает morale врагов |

**Реализация:** Animation trigger (`SetTrigger("AlertCall")`) + сервер-side `NpcGroupController.BroadcastAllyInCombat()` в радиусе.

### 3.6 Territorial behavior / Patrol

**Проблема:** Idle = стоять на месте. NPC не похожи на людей.

**Предлагаемое расширение:**

```csharp
// Новое поведение для Idle
public enum NpcIdleActivity {
    StandStill,    // текущее (MVP)
    Patrol,        // ходит по маршруту (waypoints из NpcSpawnerConfig)
    LookAround,    // осматривается (head animation)
    Socialize,     // взаимодействует с другим NPC (стоять рядом, жестикулировать)
    Work,          // имитация работы (анимация)
    Sit,           // сидит на стуле/коробке
    Sleep,         // спит (лежит, не реагирует на proximity, только на damage)
}
```

**Patrol waypoints:** массив `Vector3[]` в `NpcSpawnerConfig` или auto-generated вокруг спавна.

**Anti-restrictive:** если patrol waypoints пусты — `StandStill` (backward compat).

### 3.7 Post-combat behavior

**Текущее:** после возврата в Idle (leash) — NPC стоит, забывает игрока.

**Предлагаемое:**
- **Wounded state:** если NPC выжил после боя (HP < 60%) — идёт в укрытие, не агрится 10-20 секунд
- **Heal attempt:** NPC может «лечиться» через медикамент-анимацию (HP regen 1-5/сек, Phase 2)
- **Call for reinforcement:** после боя NPC может побежать за подмогой (новый триггер для NPC в 50м)
- **Loot body:** NPC может подобрать лут союзника (blackbox, морально серый)

---

## 4. Архитектурная интеграция (как не сломать существующее)

### 4.1 Принцип: add-only, не refactor

Текущий `NpcBrain.cs` работает. Новая система — **надстройка**, не замена.

```
NpcBrain.cs          ← stays as-is (base FSM)
NpcBrain_Extended.cs ← partial class (new file, adds emotion + morale + social triggers)
NpcGroupController.cs ← new file (server-side group hub)
NpcMoraleSystem.cs   ← new file (morale/fear calculations)
NpcEmotionState.cs   ← new file (enum + state machine)
NpcIdleActivity.cs   ← new file (patrol, socialize, etc.)
```

**Anti-restrictive:** если `NpcGroupController` не назначен — NPC работает как сейчас (single).

### 4.2 Extension points в существующем `NpcBrain.cs`

**Что нужно добавить в `NpcBrain.cs` (add-only):**

```csharp
// В класс NpcBrain (add-only, не меняя существующие поля)

[Header("Social Behavior (Phase 2)")]
[SerializeField] private bool _socialEnabled = false;           // master toggle
[SerializeField] private float _allyDetectionRadius = 15f;     // для AllyInCombat
[SerializeField] private float _allyDeathRadius = 20f;         // для AllyKilled
[SerializeField] private NpcPersonality _personality = new NpcPersonality();

// Новый тик (опционально, включается через _socialEnabled)
private void SocialTick() {
    if (!_socialEnabled) return;
    // 1. Emotion update (morale, fear, anger)
    // 2. Social trigger check (AllyInCombat, AllyKilled, Outnumbered)
    // 3. GroupController sync
}
```

**Где вызывать `SocialTick()`:** внутри существующего `Tick()` — после основного state switch, перед `UpdateAnimator()`.

```csharp
private void Tick() {
    // ... существующий switch (_state) ...
    
    // Новое: SocialTick (опционально)
    if (_socialEnabled) SocialTick();
    
    UpdateAnimator();
}
```

### 4.3 NpcSpawnerConfig extension

```csharp
// Добавить в NpcSpawnerConfig (add-only)

[Header("Social Behavior (Phase 2)")]
public bool socialEnabled = false;
public float allyDetectionRadius = 15f;
public bool assignGroupOnSpawn = true;           // группировать NPC, заспавненных рядом
public float groupSpawnRadius = 25f;             // NPC в этом радиусе = одна группа
public NpcPersonalityConfig personalityConfig;   // override personality (optional)
```

### 4.4 Server-authoritative

Всё остаётся server-only (`if (!IsServer) return;`):
- Emotion decisions — на сервере
- Morale calculations — на сервере
- Group assignment — на сервере
- На клиент идёт только результат (через `NetworkVariable<NpcEmotionState>` или animation trigger RPC)

---

## 5. Приоритизация (что делать в какой последовательности)

### Phase 1 — MVP social (3-4 сессии)

| # | Что | Тикеты | Трудозатраты |
|---|---|---|---|
| P1.1 | **AllyInCombat триггер** | `T-NPC-S01` | ~2 ч |
| P1.2 | **AllyKilled триггер** (rage buff) | `T-NPC-S02` | ~1.5 ч |
| P1.3 | **Базовый NpcGroupController** (без формаций) | `T-NPC-S03` | ~3 ч |
| P1.4 | **Idle-активности: Patrol** (waypoints) | `T-NPC-S04` | ~2 ч |
| P1.5 | **Grudge memory** (запоминание обидчика) | `T-NPC-S05` | ~1 ч |

**Итого Phase 1:** ~9.5 ч (1-2 сессии).

### Phase 2 — Emotion + Morale (3-4 сессии)

| # | Что | Тикеты | Трудозатраты |
|---|---|---|---|
| P2.1 | **NpcEmotionState** + переходы | `T-NPC-S06` | ~3 ч |
| P2.2 | **NpcMoraleSystem** (расчёт) | `T-NPC-S07` | ~3 ч |
| P2.3 | **Fear → Flee** (новый state) | `T-NPC-S08` | ~3 ч |
| P2.4 | **Vocal cues** (AlertCall, DeathScream) | `T-NPC-S09` | ~2 ч |
| P2.5 | **Idle-активности: socialize, look around** | `T-NPC-S10` | ~2 ч |

**Итого Phase 2:** ~13 ч (2 сессии).

### Phase 3 — Advanced (post-MVP)

| # | Что | Тикеты | Трудозатраты |
|---|---|---|---|
| P3.1 | **Surrender / mercy** | `T-NPC-S11` | ~2 ч |
| P3.2 | **Group formation (flanking, surround)** | `T-NPC-S12` | ~4 ч |
| P3.3 | **Heal / wounded retreat** | `T-NPC-S13` | ~3 ч |
| P3.4 | **Call for reinforcement** (дальний) | `T-NPC-S14` | ~3 ч |
| P3.5 | **Personality system** (traits per NPC) | `T-NPC-S15` | ~3 ч |

**Итого Phase 3:** ~15 ч (2-3 сессии).

---

## 6. Дизайнерская настройка (новые ScriptableObject)

### 6.1 `NpcPersonalityConfig`

```csharp
[CreateAssetMenu(fileName = "NpcPersonality_", menuName = "Project C/AI/Npc Personality")]
public class NpcPersonalityConfig : ScriptableObject {
    [Range(0f, 1f)] public float courage = 0.7f;         // 0 = трус, 1 = храбрец
    [Range(0f, 1f)] public float aggression = 0.6f;      // 0 = избегает боя, 1 = ищет бой
    [Range(0f, 1f)] public float loyalty = 0.8f;         // 0 = бросит группу, 1 = умрёт за группу
    [Range(0f, 1f)] public float recklessness = 0.3f;    // 0 = осторожен, 1 = лезет на рожон
    [Range(0f, 1f)] public float mercy = 0.2f;           // 0 = добивает, 1 = принимает сдачу
}
```

**Влияние на поведение:**
- `courage` → стартовое `morale`, порог flee
- `aggression` → скорость входа в Chase, склонность к преследованию
- `loyalty` → влияние `AllyKilled` на эмоции, willingness to retreat
- `recklessness` → частота атак, готовность врываться в толпу
- `mercy` → шанс принять сдачу/отпустить врага

### 6.2 Примеры personality для фракций

| Фракция | courage | aggression | loyalty | recklessness | mercy |
|---|---|---|---|---|---|
| **Бандит-новичок** | 0.4 | 0.7 | 0.3 | 0.6 | 0.1 |
| **Бандит-ветеран** | 0.8 | 0.8 | 0.6 | 0.4 | 0.1 |
| **Гильдейский страж** | 0.7 | 0.4 | 0.9 | 0.2 | 0.5 |
| **Фанатик-сектант** | 0.9 | 0.9 | 0.9 | 0.8 | 0.0 |
| **Торговец (самооборона)** | 0.3 | 0.2 | 0.5 | 0.1 | 0.8 |
| **Пират-капитан** | 0.9 | 0.8 | 0.5 | 0.6 | 0.2 |

---

## 7. Сопоставление с утерянным отчётом `02_SOCIAL_HUMAN_BEHAVIOR.md`

**Файл не найден.** Поиск по всему проекту (`grep -r "SOCIAL"`, `find *SOCIAL*`, `find *02_SOCIAL*`) дал 0 результатов.

Возможные причины отсутствия:
1. Файл был создан в ветке, которая не смержена в `main`
2. Файл был удалён при реорганизации документации
3. Файл находится во внешней директории (например, `docs/gdd/` или `docs/PeacefulShip/`)
4. Файл никогда не существовал (ошибка памяти пользователя)

**Рекомендация:** проверить git log для восстановления:

```bash
git log --all --oneline -- '**/02_SOCIAL_HUMAN_BEHAVIOR*'
git log --all --oneline --diff-filter=D -- '**/SOCIAL_HUMAN*'
```

Если файл найдётся — сравнить выводы настоящего документа с тем отчётом и выпустить сопоставительный меморандум.

---

## 8. Выводы и рекомендации

### 8.1 Что можно внедрить прямо сейчас (minimal code change)

1. **AllyInCombat триггер** — минимальное изменение в `NpcBrain.Tick()`: при переходе в Chase проверить, есть ли союзные NPC в радиусе в `Chase/Attack`. ~30 строк кода.
2. **Grudge memory** — дополнить `_aggroTarget` логику: если `_aggroTarget == null` и есть `_grudgeTargets` — вернуться к самому старому обидчику. ~20 строк.
3. **Patrol waypoints** — массив `Vector3[]` + `_waypointIndex` в `NpcBrain`. ~50 строк.

### 8.2 Что отложить

- **Dynamic group formation** (flanking, surround) — требует `NpcGroupController`, `NavMesh` pathfinding для формаций. ~300+ строк.
- **Personality system** (5 traits) — интересно, но низкий ROI на MVP. ~200 строк + 10 .asset файлов.
- **Heal / wounded retreat** — требует анимации + visual feedback.

### 8.3 Ключевое архитектурное решение

**НЕ** переписывать `NpcBrain.cs`. Добавить слой социального поведения **через partial class** или **через композицию**:

```
Вариант A (частичный класс):
NpcBrain.cs            ← base (как есть)
NpcBrain.Social.cs     ← новый файл, partial class NpcBrain { SocialTick, ... }

Вариант B (композиция):
NpcBrain.cs            ← base (как есть)
NpcSocialComponent.cs  ← новый MonoBehaviour, подключается к тому же GameObject
                        через GetComponent<NpcBrain>() для чтения состояния
```

**Рекомендация: Вариант A** (partial class) — меньше overhead, прямой доступ к private полям без геттеров, не меняет иерархию GameObject.

### 8.4 Связь с лором

В `02_LORE.md` установлено: 5 Гильдий, бандиты, пираты. Новые социальные паттерны позволяют **стилистически различать** фракции через personality:
- **Гильдейцы:** высокая loyalty, высокая mercy, низкая recklessness → обороняются группой, не добивают
- **Бандиты:** низкая loyalty, низкая mercy, высокая aggression → сражаются до конца, не помогают друг другу
- **Пираты:** высокая recklessness, средняя courage → любят риск, но паникуют при потерях

Это согласуется с лором и не требует изменений в `docs/WORLD_LORE_BOOK.md`.

### 8.5 Risk assessment

| Риск | Вероятность | Влияние | Митигация |
|---|---|---|---|
| SocialTick увеличивает CPU на сервере | Medium | Низкое | Tick throttling (10 Hz как существующий) |
| AllyInCombat спамит FindObjects по всем NPC | High | Среднее | Кэш группы через NpcGroupController, не per-tick поиск |
| Flee ломает leash (NPC убегает за 40м) | Low | Среднее | Flee имеет свой leash (например, 80м) и timeout |
| Partial class усложняет читаемость | Low | Низкое | .Social.cs — convention, все social методы в одном файле |

---

## 9. Что дальше

1. ✅ **Эта сессия:** анализ + дизайн (research done)
2. ⏸ **Следующая сессия:** если пользователь утверждает — начать с `T-NPC-S01` (AllyInCombat) + `T-NPC-S05` (Grudge memory) — минимальные изменения, максимальный ROI
3. ⏸ **Per-ship bake для Phase 2 навигации** — остаётся открытым (T-CREW-02/03)
4. ❓ **Восстановление `02_SOCIAL_HUMAN_BEHAVIOR.md`** — рекомендуется проверить git log

---

*Документ создан: 2026-07-08. Код не писался — только анализ и дизайн.*
