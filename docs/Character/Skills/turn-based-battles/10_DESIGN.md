# Design — архитектура turn-based battles

> **Дата:** 2026-06-25
> **Базируется на:** `Battle/ERPR_collaboration.md` (ERPR-пакет), `Battle/10_DESIGN.md §7` (damage-формула), `Battle/20_SKILL_TREES.md` (навыки), `01_ANALYSIS.md` (что есть)
> **Подход:** server-authoritative, NGO 2.x, отдельный UIDocument для UI, переиспользуем навыки и формулу из `Battle/`.

---

## 1. Высокоуровневая архитектура

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    TURN-BASED BATTLES SUBSYSTEM (NEW)                        │
│                                                                             │
│  ┌────────────────────────┐    ┌────────────────────────┐                    │
│  │ TurnBasedBattle (POCO) │    │ TurnBasedBattleInstance │                   │
│  │ server-side singleton  │    │ (один бой: 1 инстанс)   │                   │
│  │ dict<battleId, inst>  │    │ - сетка 8x8/10x10       │                   │
│  │ - CreateBattle()       │    │ - участники (1v1, 1vN) │                   │
│  │ - EndBattle()          │    │ - очередь ходов (init) │                   │
│  │ - Tick() (server tick) │    │ - currentTurn, currentRound             │
│  └────────────┬───────────┘    │ - per-participant HP, AP, position         │
│               │                 └────────────┬───────────┘                   │
│               │                              │                               │
│               ▼                              ▼                               │
│  ┌──────────────────────────────────────────────────────────────────┐       │
│  │  TurnBasedBattleServer (NetworkBehaviour, scene-placed)          │       │
│  │  - RPC hub (T-TB05/T-TB06)                                       │       │
│  │  - NetworkVariable<battleId>, NetworkVariable<turnIdx>           │       │
│  │  - Per-client subscription (через SkillsWorld/StatsWorld)         │       │
│  └──────────────────────────────────────────────────────────────────┘       │
│               │                                                             │
│               ▼                                                             │
│  ┌────────────────────────┐    ┌────────────────────────┐                    │
│  │ TurnBasedBattleClientState│ │ TurnBasedBattleWindow   │                  │
│  │ (singleton)              │ │ (UIDocument, отдельно)  │                  │
│  │ OnBattleStarted         │ │ - сетка (VisualElement) │                  │
│  │ OnTurnStarted           │ │ - кнопки (атака/движ/...)│                  │
│  │ OnActionResult          │ │ - лог (последние 5 дей) │                  │
│  │ OnBattleEnded           │ │ - статы (HP, AP)         │                  │
│  └────────────────────────┘    └────────────────────────┘                    │
│                                                                             │
│  ┌────────────────────────┐    ┌────────────────────────┐                    │
│  │ TurnBasedAI (rule-based)│   │ DamageCalculator (static)│                   │
│  │ (server-side)           │   │ - ERPR-формула (Battle/ §7)             │
│  │ - attack if in range    │   │ - используется TB + RT  │                   │
│  │ - move closer if not    │   └────────────────────────┘                    │
│  │ - flee if HP<10         │                                                │
│  └────────────────────────┘                                                 │
│                                                                             │
│  ┌────────────────────────┐    ┌────────────────────────┐                    │
│  │ DungeonConfig (SO)     │    │ DuelConfig (SO)        │                    │
│  │ - участники-NPC        │    │ - правила дуэли        │                    │
│  │ - лут                  │    │ - ставка (credits)     │                    │
│  │ - сложность (rank)     │    │ - XP loss (yes/no)     │                    │
│  └────────────────────────┘    └────────────────────────┘                    │
│                                                                             │
│  ┌────────────────────────┐                                                 │
│  │ TurnBasedBattleZone     │  (GameObject в WorldScene_X_Z)                │
│  │ - trigger входа в данж  │  - server-side NetworkObject (spawn-on-demand) │
│  │ - ссылка на DungeonConfig                                                 │
│  └────────────────────────┘                                                 │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
                    ┌─────────────────────────────┐
                    │ Переиспользует из Battle/:    │
                    │ - SkillNodeConfig (T-P11)     │
                    │ - WeaponItemData (T-CB03)     │
                    │ - ClothingItemData.armorDefense (T-CB06) │
                    │ - DamageCalculator (T-TB07)   │
                    │ - SkillsWorld/StatsWorld/EquipmentWorld (POCO) │
                    └─────────────────────────────┘
```

---

## 2. Поле боя — сетка

### 2.1 Структура

**Файл (новый):** `Assets/_Project/Scripts/TurnBased/Grid/`
**Namespace:** `ProjectC.TurnBased`

```csharp
public enum CellType : byte {
    Empty = 0,        // проходимо
    Wall = 1,         // непроходимо
    DifficultTerrain = 2,  // проходимо, но +1 сек на movement
    Hazard = 3,       // при входе: 1d4 урона (Antigrav)
    Exit = 4,         // для побега
}

[Serializable]
public struct GridCell {
    public CellType type;
    public ulong occupantClientId;  // 0 = пусто
    public ulong occupantNpcId;     // 0 = пусто
}

public class BattleGrid {
    public int width;   // 8 или 10
    public int height;
    public GridCell[,] cells;

    public Vector2Int GetPosition(ulong clientIdOrNpcId) { /* ... */ }
    public bool IsEmpty(Vector2Int pos) { /* ... */ }
    public bool CanMove(Vector2Int from, Vector2Int to) { /* ... */ }
    public float MovementCost(Vector2Int from, Vector2Int to) { /* seconds = 1 base */ }
    public List<Vector2Int> GetNeighbors(Vector2Int pos, int radius = 1) { /* 4-directional, no diagonals */ }
}
```

### 2.2 Размер по сценарию

| Сценарий | Размер | Участники |
|---|---|---|
| Соло-данж (PvE) | 10x10 | 1 игрок + 1-3 NPC |
| Босс-enкаунтер (TB-only) | 10x10 | 1 игрок + 1 босс (или 1 игрок + 1 босс + 2 приспешника) |
| PvP-дуэль 1v1 | 6x6 | 2 игрока |
| Фракционный ивент | 12x12 | 4-8 игроков + 5-10 NPC |

### 2.3 Движение по прямым (без диагоналей) — копия ERPR §1.1

**Правило:** из клетки (x, y) можно перейти в (x±1, y) или (x, y±1). **Диагонали запрещены** (для квадратной сетки).

**Стоимость:** `1 секунда` за перемещение (см. `§3`).

**Пример (как в ERPR стр.6):**
```
5 . . . . . . . . . .
. . . . . . . . . . .
. . . X . . . X . .  ← два NPC
. . . . . . . . . . .
. . . . . P . . . . .  ← игрок
. . . . . . . . . . .
. . . . . . . . . . .
. . . . . . . . . . .
. . . . . . . . . . .
. . . . . . . . . . .
. . . . . . . . . . .
```
Игрок (P) может перейти в любую из 4 соседних клеток. NPC-1 и NPC-2 на расстоянии 3 и 5 клеток.

### 2.4 Поле в `BattleGrid` constructor

```csharp
public BattleGrid(int width, int height) {
    this.width = width;
    this.height = height;
    cells = new GridCell[width, height];
    // Default: все клетки Empty
}

public void SetCell(Vector2Int pos, CellType type) { /* ... */ }
public void PlaceUnit(ulong clientIdOrNpcId, Vector2Int pos) { /* ... */ }
public void MoveUnit(ulong clientIdOrNpcId, Vector2Int to) { /* ... */ }
```

---

## 3. 3 секунды на ход (ERPR §1.1.3)

### 3.1 Стат (на участника)

```csharp
public class TurnBasedParticipant {
    public ulong clientIdOrNpcId;   // player or NPC
    public bool isNpc;
    public int maxHp;
    public int currentHp;
    public int maxSeconds;   // 3 (constant)
    public int currentSeconds;  // 0..3

    // Combat stats (для damage-формулы)
    public WeaponItemData equippedWeapon;
    public List<ClothingItemData> equippedArmor;
    public int strength;
    public int dexterity;
}
```

### 3.2 Стоимость действий (ERPR §1.1.3)

| Действие | Секунды |
|---|---|
| **Перемещение** (1 клетка) | 1 сек |
| **Атака ближнего боя** (меч, копьё, антиграв-клинок) | 1-3 сек (по типу оружия) |
| **Атака дальнего боя** (арбалет, пневматика, мезиевое) | 2-3 сек (перезарядка) |
| **Навык** (skillMult, special) | 1-3 сек (по навыку) |
| **Защита** (стойка, parry) | 1 сек (техника на этот ход) |
| **Бег** (2 клетки, на 1 сек дороже) | 2 сек |
| **Отдых** (+1 сек, до max) | 0 сек |
| **Конец хода** (досрочно) | — (оставшиеся секунды сгорают) |

**Пример (меч, d6, base 3, STR 10):**
- Игрок: `currentSeconds = 3`
- Действие 1: `Move east (1 sec) → currentSeconds = 2`
- Действие 2: `Attack NPC (2 sec) → currentSeconds = 0`
- Ход заканчивается (currentSeconds = 0, остальные секунды сгорают).

### 3.3 Проверка на сервере

```csharp
public bool CanAfford(TurnBasedParticipant p, int seconds) {
    return p.currentSeconds >= seconds;
}

public void Spend(TurnBasedParticipant p, int seconds) {
    p.currentSeconds = Mathf.Max(0, p.currentSeconds - seconds);
}

public void EndTurn(TurnBasedParticipant p) {
    p.currentSeconds = 0;  // остальные секунды сгорают
}
```

---

## 4. Инициатива по DEX (ERPR §1.2)

### 4.1 Алгоритм

```csharp
public class TurnOrder {
    private List<TurnBasedParticipant> _sorted = new();

    public void Recompute(List<TurnBasedParticipant> all) {
        _sorted = all.OrderByDescending(p => p.dexterity)
                     .ThenBy(p => p.clientIdOrNpcId)  // tie-break (детерминированно)
                     .ToList();
    }

    public TurnBasedParticipant Current { get; set; }
    public List<TurnBasedParticipant> Queue { get; set; }

    public void Next() {
        if (Queue.Count == 0) {
            // Round finished
            Queue = new List<TurnBasedParticipant>(_sorted);
            Queue.ForEach(p => p.currentSeconds = p.maxSeconds);  // restore AP
        }
        Current = Queue[0];
        Queue.RemoveAt(0);
    }
}
```

**Пример (3 участника):**
- Игрок: DEX 10
- NPC-гоблин 1: DEX 8
- NPC-гоблин 2: DEX 6

**Очередь:** `Игрок → Гоблин1 → Гоблин2 → Игрок → Гоблин1 → ...` (цикл).

**Tie-break:** при равном DEX (например, два игрока с DEX 10) — `clientId` решает (детерминированно).

### 4.2 Первый раунд

```csharp
public void StartBattle(List<TurnBasedParticipant> all) {
    // Init HP, AP
    all.ForEach(p => {
        p.currentHp = p.maxHp;
        p.currentSeconds = p.maxSeconds;
    });
    // Compute order
    _sorted = all.OrderByDescending(p => p.dexterity)
                 .ThenBy(p => p.clientIdOrNpcId)
                 .ToList();
    // Queue
    Queue = new List<TurnBasedParticipant>(_sorted);
    Next();  // first turn
}
```

---

## 5. Damage-формула (ERPR, детальная)

> **Ссылка:** `Battle/10_DESIGN.md §7` (полная формула). Здесь — реализация в TB.

### 5.1 Реализация `DamageCalculator` (static class)

**Файл (новый):** `Assets/_Project/Scripts/Combat/DamageCalculator.cs`
**Namespace:** `ProjectC.Combat`

```csharp
public static class DamageCalculator {
    public struct DamageResult {
        public int baseAttack;
        public float locMult;
        public float critMult;
        public float skillMult;
        public int preDefenseDamage;
        public int effectiveDefense;
        public int finalDamage;
        public HitLocation hitLocation;
        public bool isCrit;
    }

    public static DamageResult Calculate(
        TurnBasedParticipant attacker,
        TurnBasedParticipant defender,
        SkillNodeConfig skill = null,
        bool enableHitLocation = true,
        bool enableCrit = true
    ) {
        var weapon = attacker.equippedWeapon;
        if (weapon == null) return default;  // без оружия — урон 0

        // === ERPR формула ===
        // 1. Base attack: roll dN + base + STR
        int roll = weapon.damageDice.Roll();
        int baseAttack = roll + weapon.baseDamage + attacker.strength;

        // 2. Hit location (1d4 → mult)
        int locRoll = enableHitLocation ? UnityEngine.Random.Range(1, 5) : 3;  // 3 = Torso = ×1
        HitLocation loc = locRoll switch {
            1 or 2 => HitLocation.Limbs,
            3 => HitLocation.Torso,
            4 => HitLocation.Head,
            _ => HitLocation.Torso,
        };
        float locMult = HitLocationExtensions.GetMultiplier(loc);

        // 3. Crit (1d100 + critMod >= 100 → ×2)
        int critRoll = enableCrit ? UnityEngine.Random.Range(1, 101) : 99;  // 99 = no crit
        bool isCrit = (critRoll + weapon.critModifier) >= 100;
        float critMult = isCrit ? 2.0f : 1.0f;

        // 4. Skill multiplier
        float skillMult = 1.0f;
        if (skill != null) {
            foreach (var eff in skill.effects) {
                if (eff.type == SkillEffect.Type.StatMod && eff.multiplier > 0) {
                    skillMult *= eff.multiplier;
                }
            }
            // (Phase 2) hit-location bias from WeaponTechniqueUnlock
        }

        // 5. Pre-defense damage
        int preDefense = Mathf.RoundToInt(baseAttack * locMult * critMult * skillMult);

        // 6. Defense (sum armorDefense × typeMultiplier)
        int totalArmor = 0;
        foreach (var armor in defender.equippedArmor) {
            totalArmor += armor.armorDefense;
        }
        float armorMult = weapon.damageType switch {
            DamageType.Physical or DamageType.Ballistic => 1.0f,
            DamageType.Antigrav => 0.5f,
            DamageType.Explosive => 0.7f,
            DamageType.Mesium => 0.0f,
            _ => 1.0f,
        };
        int effectiveDefense = Mathf.RoundToInt(totalArmor * armorMult);

        // 7. Final
        int final = Mathf.Max(0, preDefense - effectiveDefense);

        return new DamageResult {
            baseAttack = baseAttack,
            locMult = locMult,
            critMult = critMult,
            skillMult = skillMult,
            preDefenseDamage = preDefense,
            effectiveDefense = effectiveDefense,
            finalDamage = final,
            hitLocation = loc,
            isCrit = isCrit,
        };
    }
}

public enum HitLocation : byte { Limbs = 0, Torso = 1, Head = 2 }

public static class HitLocationExtensions {
    public static float GetMultiplier(HitLocation loc) => loc switch {
        HitLocation.Limbs => 0.5f,
        HitLocation.Torso => 1.0f,
        HitLocation.Head => 2.0f,
        _ => 1.0f,
    };
}
```

### 5.2 Использование в TB

```csharp
public class TurnBasedBattleInstance {
    public void ResolveAttack(TurnBasedParticipant attacker, TurnBasedParticipant defender, SkillNodeConfig skill) {
        // Validate
        if (!CanAfford(attacker, attacker.equippedWeapon.GetActionCost())) {
            SendError("Not enough seconds");
            return;
        }
        // Distance check
        var dist = Vector2Int.Distance(attacker.position, defender.position);
        if (dist > attacker.equippedWeapon.range / 2.0f) {  // 2m = 1 cell
            SendError("Out of range");
            return;
        }
        // Spend seconds
        Spend(attacker, attacker.equippedWeapon.GetActionCost());

        // Calculate damage
        var result = DamageCalculator.Calculate(attacker, defender, skill);

        // Apply
        defender.currentHp = Mathf.Max(0, defender.currentHp - result.finalDamage);

        // Notify
        WorldEventBus.Publish(new ActionResultEvent {
            attackerId = attacker.clientIdOrNpcId,
            defenderId = defender.clientIdOrNpcId,
            result = result,
        });

        // Check death
        if (defender.currentHp == 0) {
            HandleDeath(defender);
        }
    }
}
```

---

## 6. AI для NPC (rule-based)

**Файл (новый):** `Assets/_Project/Scripts/TurnBased/AI/TurnBasedAI.cs`
**Namespace:** `ProjectC.TurnBased.AI`

### 6.1 Простая rule-based AI (3 приоритета)

```csharp
public class TurnBasedAI {
    public ActionDecision Decide(TurnBasedParticipant npc, BattleState state) {
        // Rule 1: Flee if HP<25%
        if (npc.currentHp < npc.maxHp * 0.25f) {
            return new ActionDecision {
                action = ActionType.Move,
                target = FindFurthestFromPlayers(npc, state),
                reason = "Flee (HP<25%)",
            };
        }

        // Rule 2: Attack if in range
        var nearest = FindNearestPlayer(npc, state);
        if (nearest != null) {
            float dist = Vector2Int.Distance(npc.position, nearest.position);
            float weaponRange = npc.equippedWeapon.range / 2.0f;
            if (dist <= weaponRange) {
                return new ActionDecision {
                    action = ActionType.Attack,
                    target = nearest,
                    reason = $"Attack (dist={dist})",
                };
            }
        }

        // Rule 3: Move closer
        return new ActionDecision {
            action = ActionType.Move,
            target = MoveTowards(npc, nearest, 5 /* cells max */),
            reason = "Move closer",
        };
    }
}

public class ActionDecision {
    public ActionType action;
    public TurnBasedParticipant target;
    public Vector2Int moveTarget;
    public string reason;
}
```

### 6.2 Aggression levels (per NPC)

```csharp
public enum NpcAggression : byte {
    Passive = 0,      // не атакует, flee
    Defensive = 1,    // атакует только если в радиусе 1
    Normal = 2,       // атакует в радиусе оружия (default)
    Aggressive = 3,   // преследует активно
    Berserk = 4,      // бьётся до смерти, no flee
}
```

**Примеры:**
- `Goblin_Worker` (рядовой NPC) → Normal
- `Goblin_Chief` (босс данжа) → Berserk
- `Bandit_Scavenger` (враждебный нейтрал) → Aggressive

### 6.3 Что НЕ делаем в AI (открытый вопрос)

- ❌ Pathfinding (A*) — в MVP используем `MoveTowards` (поиск ближайшей клетки к цели).
- ❌ Cover/tactics — нет.
- ❌ Synergy (NPC помогают друг другу) — нет.
- ❌ Retreat-with-heal — нет (нет heal-механики в MVP).
- ❌ ML/AI — Phase 3.

---

## 7. Death / XP loss (ERPR §2.3)

### 7.1 Правила

| Сценарий | Эффект на проигравшем | Эффект на победившем |
|---|---|---|
| **PvE-данж** (игрок vs NPC) | Если игрок HP=0 → respawn в safe zone + **20% XP loss** (текущий tier) | NPC «умирает» → лут добавляется в инвентарь |
| **PvP-дуэль** | **Permadeath** (consent-based) или 20% XP loss | Победитель: credits + honor |
| **Boss-enкаунтер** | Respawn + 20% XP loss + квест-флаг не выполнен | Босс «умирает» → legendary loot + квест-флаг выполнен |
| **Фракционный ивент** | Respawn в зоне + 0% XP loss (командный ивент, не штрафуем) | Top-3 по урону: награды |

### 7.2 XP loss (PvE)

```csharp
public static void ApplyDeathPenalty(ulong clientId) {
    var stats = StatsWorld.Instance.GetOrCreateStats(clientId);
    // 20% от currentXp в текущем тире
    float xpLoss = stats.strength * 0.2f;  // для текущего dominant stat
    StatsServer.Instance.ApplyXpDirect(clientId, StatType.Strength, -xpLoss);
    Debug.Log($"[TurnBased] Player {clientId} died in TB. XP loss: {xpLoss} STR");
}
```

### 7.3 Respawn

```csharp
public void HandleDeath(TurnBasedParticipant p) {
    if (!p.isNpc) {
        // Player
        ApplyDeathPenalty(p.clientIdOrNpcId);
        SendBattleEndedTargetRpc(p.clientIdOrNpcId, BattleResult.Defeat);
        // Respawn через 5 сек
        RespawnPlayer(p.clientIdOrNpcId, delaySeconds: 5);
    } else {
        // NPC
        WorldEventBus.Publish(new NpcKilledEvent {
            npcId = p.clientIdOrNpcId,
            killerId = GetCurrentAttacker(),
        });
        DropLoot(p);
    }
}
```

---

## 8. UI: TurnBasedBattleWindow

**Файл (новый):** `Assets/_Project/Scripts/UI/Client/TurnBasedBattleWindow.cs`
**Файл (новый):** `Assets/_Project/UI/Resources/UI/TurnBasedBattleWindow.uxml`
**Файл (новый):** `Assets/_Project/UI/Resources/UI/TurnBasedBattleWindow.uss`
**Namespace:** `ProjectC.UI.Client`

### 8.1 Структура UI

```
┌──────────────────────────────────────────────────────────┐
│  ⚔️ БОЙ: Гоблины в руинах                                │
├──────────────────────────────────────────────────────────┤
│  Ход: Игрок (1/3 сек) | Раунд 2                         │
│  HP: [███░░] 15/20  AP: [███] 3/3                       │
├──────────────────────────────────────────────────────────┤
│  Сетка (10x10):                                          │
│  G . . . . . . . . .                                     │
│  . . . . . . . . . .                                     │
│  . . . . . . . G . .  ← два гоблина                     │
│  . . . . . . . . . .                                     │
│  . . . . . P . . . .  ← игрок                            │
│  . . . . . . . . . .                                     │
│  . . . . . . . . . .                                     │
│  . . . . . . . . . .                                     │
│  . . . . . . . . . .                                     │
│  . . . . . . . . . .                                     │
├──────────────────────────────────────────────────────────┤
│  Действия:                                               │
│  [⚔️ Атака (2 сек)] [🏃 Двинуться (1 сек)]               │
│  [🛡️ Защита (1 сек)] [✨ Навык (1-3 сек)]                 │
│  [⏭️ Конец хода]                                        │
├──────────────────────────────────────────────────────────┤
│  Лог боя:                                                │
│  > Раунд 2: Игрок атакует Гоблин1 (меч d6+3+STR10=17)  │
│  > Hit Location: Torso (×1.0)                           │
│  > Crit: no (87+0<100)                                  │
│  > Гоблин1: -12 HP (5/20)                               │
└──────────────────────────────────────────────────────────┘
```

### 8.2 UXML структура

```xml
<ui:VisualElement name="tb-battle-root" class="tb-root">
  <ui:Label name="tb-title" class="tb-title" text="⚔️ БОЙ" />
  
  <ui:VisualElement class="tb-status-bar">
    <ui:Label name="tb-turn-info" class="tb-turn-info" />
    <ui:VisualElement class="tb-hp-bar">
      <ui:Label name="tb-hp-text" />
      <ui:VisualElement name="tb-hp-fill" class="tb-hp-fill" />
    </ui:VisualElement>
    <ui:VisualElement class="tb-ap-bar">
      <ui:Label name="tb-ap-text" />
      <ui:VisualElement name="tb-ap-fill" class="tb-ap-fill" />
    </ui:VisualElement>
  </ui:VisualElement>
  
  <ui:VisualElement name="tb-grid" class="tb-grid" />
  
  <ui:VisualElement class="tb-actions">
    <ui:Button name="btn-attack" text="⚔️ Атака" class="tb-action-btn" />
    <ui:Button name="btn-move" text="🏃 Двинуться" class="tb-action-btn" />
    <ui:Button name="btn-defend" text="🛡️ Защита" class="tb-action-btn" />
    <ui:Button name="btn-skill" text="✨ Навык" class="tb-action-btn" />
    <ui:Button name="btn-end-turn" text="⏭️ Конец хода" class="tb-action-btn" />
  </ui:VisualElement>
  
  <ui:ScrollView name="tb-log" class="tb-log">
    <ui:Label name="tb-log-content" />
  </ui:ScrollView>
</ui:VisualElement>
```

### 8.3 Стилизация (USS)

```css
.tb-root {
  position: absolute;
  width: 800px;
  height: 600px;
  background-color: rgba(20, 30, 50, 0.95);
  border-radius: 8px;
  padding: 12px;
}
.tb-grid {
  flex-direction: column;
  width: 400px;
  height: 400px;
  border: 1px solid rgb(80, 100, 130);
}
.tb-grid-row { flex-direction: row; height: 10%; }
.tb-grid-cell {
  flex: 1;
  border: 1px solid rgba(80, 100, 130, 0.3);
  background-color: rgba(40, 50, 70, 0.5);
}
.tb-grid-cell.occupied-player { background-color: rgba(80, 150, 200, 0.7); }
.tb-grid-cell.occupied-npc { background-color: rgba(200, 80, 80, 0.7); }
.tb-grid-cell.highlighted { background-color: rgba(255, 220, 130, 0.4); }  /* подсветка доступных клеток */
.tb-action-btn {
  flex: 1;
  height: 36px;
  margin: 4px;
  background-color: rgba(60, 80, 120, 0.6);
  color: white;
  border-radius: 4px;
}
.tb-action-btn:disabled {
  opacity: 0.4;
  background-color: rgba(60, 60, 60, 0.6);
}
.tb-log { max-height: 100px; }
.tb-log-line { color: rgb(200, 200, 200); font-size: 10px; }
```

### 8.4 Open TurnBasedBattleWindow

```csharp
public class TurnBasedBattleWindow : MonoBehaviour {
    private UIDocument _doc;
    private VisualElement _root;
    private Button _btnAttack, _btnMove, _btnDefend, _btnSkill, _btnEndTurn;
    private VisualElement _grid;
    private Label _turnInfo, _hpText, _apText;
    private ScrollView _log;

    void OnEnable() {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;
        _grid = _root.Q<VisualElement>("tb-grid");
        // ... wire up buttons
        TurnBasedBattleClientState.Instance.OnTurnStarted += HandleTurnStarted;
        TurnBasedBattleClientState.Instance.OnActionResult += HandleActionResult;
        TurnBasedBattleClientState.Instance.OnBattleEnded += HandleBattleEnded;
    }

    void OnDisable() { /* unsubscribe */ }
}
```

---

## 9. State machine TB

```csharp
public enum BattleState : byte {
    NotStarted = 0,
    Setup = 1,           // размещение участников
    Active = 2,          // бой идёт
    Paused = 3,          // пауза (например, ожидание PvP accept)
    Ended = 4,           // victory/defeat/escape
}

public class TurnBasedBattleInstance {
    public BattleState state;
    public ulong battleId;
    public BattleGrid grid;
    public List<TurnBasedParticipant> participants;
    public TurnOrder turnOrder;
    public int currentRound;
    public ulong localPlayerId;
    public bool isLocalPlayerTurn => turnOrder.Current?.clientIdOrNpcId == localPlayerId;

    public void Start() {
        state = BattleState.Setup;
        // place participants
        state = BattleState.Active;
        turnOrder.StartBattle(participants);
    }

    public void End(BattleResult result) {
        state = BattleState.Ended;
        WorldEventBus.Publish(new BattleEndedEvent { battleId, result });
    }
}
```

---

## 10. Что НЕ делаем (явные запреты)

- ❌ Не трогаем `GDD_*.md` (read-only).
- ❌ Не переписываем `Battle/` (только дополняем, переиспользуем).
- ❌ Не вводим магию (lore).
- ❌ Не делаем TB в открытом мире.
- ❌ Не делаем TB заменой real-time combat.
- ❌ Не делаем сложный AI (pathfinding, ML).
- ❌ Не делаем анимации (3D-отдел).
- ❌ Не делаем sound (audio-отдел).
- ❌ Не делаем voice-chat.
- ❌ Не делаем replay.
- ❌ Не пишем код в этой сессии.
