# Technical — NGO RPC, server-authoritative, state machine

> **Дата:** 2026-06-25
> **Базируется на:** `10_DESIGN.md` (архитектура), `Battle/10_DESIGN.md §1.2-1.4` (NGO 2.x pattern), `Battle/01_ANALYSIS.md §1.4-1.6` (NetworkManager, WorldEventBus)
> **Подход:** server-authoritative, NGO 2.x RPC, scene-placed в BootstrapScene, переиспользуем существующие NGO-паттерны из Battle/.

---

## 1. Scene placement

### 1.1 TurnBasedBattleServer (NetworkBehaviour)

**Файл (новый):** `Assets/_Project/Scripts/TurnBased/Network/TurnBasedBattleServer.cs`
**Namespace:** `ProjectC.TurnBased`
**Сцена:** `Assets/_Project/Scenes/BootstrapScene.unity` — рядом с `[GatheringServer]`, `[CraftingServer]`, `[ExchangeServer]`, `[MarketServer]`, `[QuestServer]`, `[StatsServer]`, `[EquipmentServer]`, `[SkillsServer]`.

**GameObject:** `[TurnBasedBattleServer]`
- `NetworkObject` (NGO 2.x, scene-placed)
- `NetworkBehaviour` (этот скрипт)
- `Transform` (root, как у других серверов)

**Регистрация в `NetworkManagerController`:** добавить в `Awake()` (по аналогии с `CreateStatsClientState` / `CreateSkillsClientState`).

### 1.2 Scene-placed vs spawned

**Все TB-серверы — scene-placed**, как и остальные. Это даёт:
- Singleton через `Instance` (как `SkillsServer.Instance`).
- Авто-спавн через NGO при старте хоста.
- Persistent между сценами (через `DontDestroyOnLoad` в `NetworkManagerController`).

**TB-инстансы (один бой) — server-spawned** NetworkObject:
- `TurnBasedBattleInstance` сериализуется в `NetworkObject` (если нужно) или держится в `TurnBasedBattle` (POCO) dictionary.
- Для MVP — **POCO** (не NetworkObject), server-side state. Достаточно для синхронизации через TargetRPC.

---

## 2. NGO RPC (client ↔ server)

### 2.1 Client → Server RPCs

```csharp
public class TurnBasedBattleServer : NetworkBehaviour {
    public static TurnBasedBattleServer Instance { get; private set; }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RequestStartPvEBattleRpc(string dungeonConfigId, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        StartPvEBattle(clientId, dungeonConfigId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RequestStartPvPDuelRpc(ulong opponentId, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        SendDuelInvite(clientId, opponentId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RespondDuelInviteRpc(ulong battleId, bool accept, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        if (accept) AcceptDuel(battleId);
        else DeclineDuel(battleId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void SubmitActionRpc(ulong battleId, ActionType action, int targetIdOrCoord, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        ProcessAction(battleId, clientId, action, targetIdOrCoord);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void EndTurnRpc(ulong battleId, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        ProcessEndTurn(battleId, clientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void SurrenderRpc(ulong battleId, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        ProcessSurrender(battleId, clientId);
    }
}
```

### 2.2 Server → Client TargetRPCs

```csharp
[Rpc(SendTo.SpecifiedInParams)]
public void BattleStartedTargetRpc(ulong battleId, BattleStartDto dto, RpcParams rpcParams) {
    // dto.participants, dto.gridSize, dto.yourTurnIndex
    TurnBasedBattleClientState.Instance.HandleBattleStarted(dto);
}

[Rpc(SendTo.SpecifiedInParams)]
public void TurnStartedTargetRpc(ulong battleId, TurnStartDto dto, RpcParams rpcParams) {
    TurnBasedBattleClientState.Instance.HandleTurnStarted(dto);
}

[Rpc(SendTo.SpecifiedInParams)]
public void ActionResultTargetRpc(ulong battleId, ActionResultDto dto, RpcParams rpcParams) {
    TurnBasedBattleClientState.Instance.HandleActionResult(dto);
}

[Rpc(SendTo.SpecifiedInParams)]
public void BattleEndedTargetRpc(ulong battleId, BattleEndDto dto, RpcParams rpcParams) {
    TurnBasedBattleClientState.Instance.HandleBattleEnded(dto);
}

[Rpc(SendTo.SpecifiedInParams)]
public void DuelInviteTargetRpc(ulong battleId, ulong fromClientId, RpcParams rpcParams) {
    // Show invite UI on target
    TurnBasedBattleClientState.Instance.HandleDuelInvite(battleId, fromClientId);
}
```

**Паттерн `SendTo.SpecifiedInParams`:** отправляем конкретному client (или списку). Используем для мультикастных нотификаций (все участники боя получают ActionResult).

### 2.3 DTO (server ↔ client)

```csharp
[Serializable]
public struct BattleStartDto : INetworkSerializable {
    public ulong battleId;
    public ulong localPlayerId;
    public int gridWidth;
    public int gridHeight;
    public ParticipantDto[] participants;  // 1..N (player + NPCs + other players)
    public int yourTurnIndex;              // кто ходит сейчас

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter {
        s.SerializeValue(ref battleId);
        s.SerializeValue(ref localPlayerId);
        s.SerializeValue(ref gridWidth);
        s.SerializeValue(ref gridHeight);
        // ... array serialize
    }
}

[Serializable]
public struct TurnStartDto : INetworkSerializable {
    public ulong battleId;
    public ulong currentParticipantId;  // кто ходит
    public int currentSeconds;
    public int currentRound;
}

[Serializable]
public struct ActionResultDto : INetworkSerializable {
    public ulong battleId;
    public ulong attackerId;
    public ulong defenderId;
    public ActionType actionType;          // Attack, Move, Defend, Skill
    public int hpDelta;                    // -12 = damage
    public int newDefenderHp;
    public int baseAttack;
    public float locMult;
    public float critMult;
    public float skillMult;
    public int preDefenseDamage;
    public int effectiveDefense;
    public int finalDamage;
    public bool isCrit;
    public byte hitLocation;               // 0=Limbs, 1=Torso, 2=Head
    public Vector2Int newDefenderPos;      // после movement
}

[Serializable]
public struct BattleEndDto : INetworkSerializable {
    public ulong battleId;
    public byte result;                    // 0=Defeat, 1=Victory, 2=Escape, 3=Surrender
    public ulong[] survivingPlayerIds;
    public int xpLoss;
    public LootDrop[] loot;
}

[Serializable]
public struct ParticipantDto : INetworkSerializable {
    public ulong clientIdOrNpcId;
    public bool isNpc;
    public string displayName;
    public int maxHp;
    public int currentHp;
    public int maxSeconds;
    public int currentSeconds;
    public int strength;
    public int dexterity;
    public int weaponItemId;     // 0 = без оружия
    public int[] armorItemIds;   // массив
    public Vector2Int position;
}
```

**Вердикт:** `INetworkSerializable` + `BufferSerializer` (NGO 2.x pattern). Аналогично `SkillsSnapshotDto` (`06_SKILL_TREE.md`).

---

## 3. Server-authoritative flow

### 3.1 Sequence: PvE-данж

```
1. CLIENT: Player approaches dungeon entrance
2. CLIENT: TurnBasedBattleServer.RequestStartPvEBattleRpc("dungeon_goblin_ruins_rank1")
3. SERVER: TurnBasedBattleServer.OnRequestStartPvEBattle:
     - Validate: player is in correct zone (DungeonConfig.zoneCheck)
     - Create TurnBasedBattleInstance (load DungeonConfig)
     - Spawn NPCs (NetworkObject, server-side, in dungeon scene)
     - Initialize BattleGrid (10x10), place player + NPCs
     - Compute TurnOrder (по DEX)
     - Send BattleStartedTargetRpc(playerClientId, BattleStartDto)
     - Send TurnStartedTargetRpc(playerClientId, TurnStartDto)
4. CLIENT: TurnBasedBattleClientState.HandleBattleStarted
     - Open TurnBasedBattleWindow
     - Render grid
     - Highlight current player (P)
     - Enable action buttons
5. PLAYER clicks "Attack" → highlight attackable targets
6. PLAYER clicks Goblin1 → SubmitActionRpc(battleId, ActionType.Attack, goblin1Id)
7. SERVER: TurnBasedBattleServer.OnSubmitAction:
     - Validate: 
       - Is it player's turn? (turnOrder.Current == playerId)
       - Has enough seconds? (currentSeconds >= attackCost)
       - Target in range? (Vector2Int.Distance <= weapon.range / 2)
       - Target alive? (currentHp > 0)
     - Spend seconds
     - Call DamageCalculator.Calculate(player, goblin, skill)
     - Apply: goblin.currentHp -= result.finalDamage
     - Send ActionResultTargetRpc(allParticipants, ActionResultDto)
     - If goblin.currentHp == 0:
       - HandleNpcDeath (drop loot, mark for removal)
     - If player's currentSeconds == 0: NextTurn
     - Send TurnStartedTargetRpc(nextParticipant, TurnStartDto)
8. CLIENT: Render damage number on goblin
     - Update HP bar
     - If goblin dead: remove from grid, show loot popup
     - If next turn is NPC: show "Enemy turn" message
9. NPC AI: TurnBasedAI.Decide(goblin) → attack/move/flee → SubmitActionRpc internally
10. Repeat until victory/defeat/escape
11. SERVER: BattleEnded → Send BattleEndedTargetRpc
12. CLIENT: Show result screen (XP loss, loot, etc.) → close TB window
```

### 3.2 Anti-cheat: server-only RNG

```csharp
// ❌ ПЛОХО: client кидает кубик
public void ClientRollDamageRpc() {
    int roll = UnityEngine.Random.Range(1, 7);  // d6
    // → клиент может подменить roll!
}

// ✅ ХОРОШО: server кидает кубик
[Rpc(SendTo.Server)]
public void SubmitActionRpc(ulong battleId, ActionType action, int targetId) {
    var result = DamageCalculator.Calculate(attacker, defender);  // server-side
    SendActionResultTargetRpc(allParticipants, result);
}
```

**Кубики (`Random.Range` для dN, d4, d100) — ТОЛЬКО на сервере.** Клиент видит результат, но не считает.

### 3.3 Multiplayer sync (4-8 игроков)

```csharp
// Multiplayer TB: последовательная обработка действий
public void ProcessAction(ulong battleId, ulong clientId, ActionType action, int target) {
    var battle = TurnBasedBattle.Instance.GetBattle(battleId);
    if (battle == null) return;

    // 1. Validate (server-authoritative)
    if (!ValidateAction(battle, clientId, action, target)) {
        SendError(clientId, "Invalid action");
        return;
    }

    // 2. Apply (server-side)
    ApplyAction(battle, clientId, action, target);

    // 3. Broadcast to all participants (multicast)
    var allParticipants = battle.participants.Select(p => p.clientIdOrNpcId).ToArray();
    foreach (var participantId in allParticipants) {
        if (participantId == 0) continue;  // NPC
        var rpcParams = new RpcParams {
            Send = new RpcSendParams { Target = RpcTarget.Single(participantId, RpcTargetUse.Temp) }
        };
        BattleStartedTargetRpc(battleId, ..., rpcParams);
    }
}
```

**Вердикт:** в TB-инстансе все участники получают ВСЕ события (полная синхронизация). Optimistic client prediction — Phase 3 (оптимизация).

---

## 4. State machine

### 4.1 TurnBasedBattle.Instance lifecycle

```csharp
public class TurnBasedBattle {
    public static TurnBasedBattle Instance { get; private set; }
    private Dictionary<ulong, TurnBasedBattleInstance> _battles = new();

    public void Initialize() {
        if (Instance != null) return;
        Instance = this;
    }

    public ulong CreateBattle(List<TurnBasedParticipant> participants, BattleConfig config) {
        ulong battleId = NextId();
        var battle = new TurnBasedBattleInstance(battleId, participants, config);
        battle.Start();
        _battles[battleId] = battle;
        return battleId;
    }

    public TurnBasedBattleInstance GetBattle(ulong battleId) {
        return _battles.TryGetValue(battleId, out var b) ? b : null;
    }

    public void EndBattle(ulong battleId, BattleResult result) {
        if (!_battles.TryGetValue(battleId, out var battle)) return;
        battle.End(result);
        _battles.Remove(battleId);
    }
}
```

### 4.2 TurnBasedBattleInstance state

```
                  ┌─────────────┐
                  │ NotStarted  │
                  └──────┬──────┘
                         │ CreateBattle() + Start()
                         ▼
                  ┌─────────────┐
                  │   Setup     │  (размещение участников)
                  └──────┬──────┘
                         │ StartBattle()
                         ▼
                  ┌─────────────┐  ┌──────────────┐
        ┌────────│   Active    │◄─┤   Paused     │
        │        └──────┬──────┘  │  (ожидание)   │
        │               │          └──────────────┘
        │ Round end     │
        │ new round     │ End (victory/defeat/escape)
        └───────────────┘
                         │
                         ▼
                  ┌─────────────┐
                  │   Ended     │  (отправляем BattleEndedTargetRpc, удаляем)
                  └─────────────┘
```

### 4.3 Rate limit

```csharp
private readonly Dictionary<ulong, float> _nextAllowedTimePerClient = new();

private bool RateLimit(ulong clientId) {
    float now = Time.unscaledTime;
    if (_nextAllowedTimePerClient.TryGetValue(clientId, out var next) && now < next) {
        SendError(clientId, "Rate limit");
        return false;
    }
    _nextAllowedTimePerClient[clientId] = now + 1f / 5f;  // 5 ops/sec
    return true;
}
```

---

## 5. Persistence (T-TB14)

### 5.1 Что сохраняем

```csharp
// В CharacterSaveData (расширение):
[Serializable]
public class TurnBasedBattleSave {
    public int totalBattlesWon;
    public int totalBattlesLost;
    public int totalBattlesEscaped;
    public int totalDamageDealt;
    public int totalDamageTaken;
    public int totalKills;  // NPC убито
    public string[] completedDungeons;  // dungeonConfigId
    public int currentStreak;  // побед подряд
}
```

### 5.2 Триггеры save

- **OnBattleEnded** — сохраняем счётчики.
- **OnDisconnect** — если игрок в бою, отменяем бой (см. `30_SCENARIOS.md §3.4` — без XP loss, но считаем как escape).
- **Periodic (каждые 5 мин)** — backup.

### 5.3 НЕ сохраняем in-flight battle state

Если игрок disconnect в середине боя → бой **отменяется**, `battle.state = Ended`, всем участникам `BattleEndedTargetRpc(result=Escape)`. In-flight state не сериализуется (избегаем сложной логики восстановления).

---

## 6. Tick (server loop)

### 6.1 FixedUpdate в TurnBasedBattleServer

```csharp
public class TurnBasedBattleServer : NetworkBehaviour {
    private void FixedUpdate() {
        if (!IsServer) return;
        // Tick active battles (timeout, NPC AI if taking too long, etc.)
        var activeBattles = TurnBasedBattle.Instance.GetActiveBattles();
        foreach (var battle in activeBattles) {
            battle.Tick();
        }
    }
}

public class TurnBasedBattleInstance {
    private float _npcAiTimeout = 0f;
    private const float NPC_AI_TIMEOUT_SECONDS = 5f;

    public void Tick() {
        if (state != BattleState.Active) return;
        // NPC AI: если NPC «застрял» (не сделал ход за 5 сек) → рандомное действие
        if (turnOrder.Current?.isNpc == true) {
            _npcAiTimeout += Time.fixedDeltaTime;
            if (_npcAiTimeout > NPC_AI_TIMEOUT_SECONDS) {
                var fallbackDecision = new ActionDecision {
                    action = ActionType.Move,
                    target = null,
                    moveTarget = new Vector2Int(
                        UnityEngine.Random.Range(0, grid.width),
                        UnityEngine.Random.Range(0, grid.height)
                    ),
                    reason = "AI timeout fallback",
                };
                ApplyDecision(turnOrder.Current, fallbackDecision);
                NextTurn();
                _npcAiTimeout = 0f;
            }
        } else {
            _npcAiTimeout = 0f;
        }
    }
}
```

---

## 7. Error handling

### 7.1 Типы ошибок

| Ошибка | Действие |
|---|---|
| `INVALID_TURN` (not your turn) | Send error → client toast |
| `INSUFFICIENT_SECONDS` | Send error → client toast |
| `OUT_OF_RANGE` | Send error → client toast |
| `TARGET_DEAD` | Send error → client refresh |
| `INVALID_BATTLE_ID` | Send error → client close window |
| `AI_TIMEOUT` (server) | Auto-resolve with fallback |
| `DISCONNECT_DURING_BATTLE` | Battle ended, no XP loss (escape) |

### 7.2 Error DTO

```csharp
[Serializable]
public struct BattleErrorDto : INetworkSerializable {
    public ulong battleId;
    public byte errorCode;
    public string message;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter {
        s.SerializeValue(ref battleId);
        s.SerializeValue(ref errorCode);
        // message: фиксированная длина или null-terminated
    }
}

[Rpc(SendTo.SpecifiedInParams)]
public void BattleErrorTargetRpc(ulong battleId, BattleErrorDto dto, RpcParams rpcParams) {
    TurnBasedBattleClientState.Instance.HandleError(dto);
}
```

---

## 8. Integration с существующими подсистемами

### 8.1 SkillsWorld (T-P12)

**TB читает:**
- `SkillsWorld.GetLearnedSkills(clientId)` → bool HasSkill(skillId) для проверки proficiency.
- Пример: `melee_basic_sword` → `WeaponProficiencyUnlock("sword")` → можно надеть `Weapon_Sword`.

**TB НЕ пишет:**
- Не публикует `SkillEffect` events. Просто читает learned.

### 8.2 StatsWorld (T-P03)

**TB читает:**
- `StatsWorld.GetOrCreateStats(clientId)` → strength, dexterity, intelligence, tiers.
- `dexterity` → инициатива.
- `strength` → damage modifier.
- `intelligenceTier` → gate навыков (не используется в TB напрямую, но при `RequestStartBattleRpc` проверяется).

**TB пишет (через StatsServer.Instance.ApplyXpDirect):**
- При смерти в TB → -20% XP (текущий dominant stat).

### 8.3 EquipmentWorld (T-P09)

**TB читает:**
- `EquipmentWorld.GetEquipment(clientId)` → equipped weapon + armor.
- `WeaponItemData` (после T-CB03) → `damageDice`, `baseDamage`, `critModifier`, `range`.
- `ClothingItemData` (после T-CB06) → `armorDefense`.

**TB НЕ пишет:**
- Не модифицирует equipment (для MVP).

### 8.4 InventoryWorld (Items/Core)

**TB пишет (через InventoryWorld.Instance.AddItemDirect):**
- Лут после победы над NPC → инвентарь игрока.

### 8.5 WorldEventBus

**TB публикует (новые events):**
- `BattleStartedEvent` — при старте.
- `BattleEndedEvent` — при конце.
- `TurnStartedEvent` — в начале каждого хода.
- `ActionResultEvent` — после каждого действия.
- `NpcKilledEvent` — NPC убит (для ачивок).
- `DuelInviteEvent` — приглашение на дуэль.

**TB подписывается:**
- (Опционально) `PlayerJumpedEvent` — не имеет смысла в TB (игрок в TB-сцене, не в open world).
- (Опционально) `CustomEvent` — для админ-команд.

### 8.6 NetworkManagerController (NetworkManagerController)

**В `Awake()` (T-TB14):**
```csharp
private void CreateTurnBasedBattleClientState() {
    if (TurnBasedBattleClientState.Instance == null) {
        var go = new GameObject("[TurnBasedBattleClientState]");
        DontDestroyOnLoad(go);
        go.AddComponent<TurnBasedBattleClientState>();
    }
}
```

---

## 9. Тестирование (unit tests, не Play Mode)

### 9.1 Что тестируем (EditMode)

| Что | Как |
|---|---|
| `DamageCalculator.Calculate` (стат.) | известные inputs → known outputs |
| `TurnOrder.Recompute` | 3 участника с разным DEX → known order |
| `BattleGrid.MovementCost` | разные terrain types |
| `TurnBasedAI.Decide` | 3 сценария (flee, attack, move closer) |
| `TurnBasedBattleInstance.EndBattle` | victory/defeat/escape → правильный result |
| `HitLocation.GetMultiplier` | Limbs=0.5, Torso=1, Head=2 |

### 9.2 Что тестируем (Play Mode, пользователь)

- ✅ Создать данж, войти, провести бой, выйти.
- ✅ Атака мечом → правильный damage.
- ✅ Crit (1d100+critMod>=100) — трудно воспроизвести, но debug-флаг `enableCrit=true` в DungeonConfig.
- ✅ Hit location (1d4) — аналогично.
- ✅ Death → respawn + XP loss.
- ✅ PvP-дуэль 1v1.
- ✅ Boss-enкаунтер (TB-only).

---

## 10. Performance & scalability

### 10.1 Один серверный инстанс — много боёв

**В текущей архитектуре:** `TurnBasedBattle.Instance` — singleton, держит `Dictionary<ulong, TurnBasedBattleInstance>`. Каждый инстанс — отдельный TB-бой.

**Оценка:** 100 активных TB-инстансов × 4 участника × 3 действия в раунде = 1200 actions/turn. На сервере с 1 Hz tick — 1200 RPC/сек. NGO 2.x справится (есть 64 KB/s на 1 клиента, 1200 actions ≈ 30 KB/s).

**Вердикт:** для MVP (один сервер, 100 активных TB) — ок. Для MMO-sandbox (10+ серверов, 1000+ TB) — нужна шардированная архитектура (Phase 3).

### 10.2 NPC AI: O(n) per tick

**Текущая AI:** rule-based, 3 правила. O(n) где n = число участников. Для 10 NPC = 10 проверок/ход. При 1 Hz tick = 10 ops/сек. Ничтожно.

### 10.3 Damage formula: O(1) per action

`DamageCalculator.Calculate` — несколько `Random.Range`, `Mathf.RoundToInt`. < 1 мс. Ничтожно.

---

## 11. Что НЕ делаем (явные запреты)

- ❌ Не трогаем `Battle/` (только переиспользуем).
- ❌ Не делаем real-time combat (отдельная подсистема).
- ❌ Не делаем сложный AI (pathfinding, ML, тактика).
- ❌ Не делаем анимации (3D-отдел).
- ❌ Не делаем sound (audio-отдел).
- ❌ Не делаем voice-chat.
- ❌ Не делаем replay.
- ❌ Не делаем server-spawn для TB-инстансов (POCO достаточно).
- ❌ Не делаем optimistic client prediction (Phase 3).
- ❌ Не пишем код в этой сессии.
