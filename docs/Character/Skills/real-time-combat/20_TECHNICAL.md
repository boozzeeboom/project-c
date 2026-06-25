# Technical — NGO RPC, server-authoritative, hooks

> **Дата:** 2026-06-25 (v0.3)
> **Базируется на:** `10_DESIGN.md` (архитектура), `Battle/01_ANALYSIS.md §1.4-1.6` (NetworkManager, WorldEventBus, NGO 2.x pattern)
> **Подход:** server-authoritative, NGO 2.x RPC, scene-placed в BootstrapScene, переиспользуем существующие NGO-паттерны из Battle/Equipment/Skills.

---

## 1. Scene placement

### 1.1 CombatServer (NetworkBehaviour)

**Файл (новый):** `Assets/_Project/Scripts/Combat/Network/CombatServer.cs`
**Namespace:** `ProjectC.Combat`
**Сцена:** `Assets/_Project/Scenes/BootstrapScene.unity` — рядом с другими серверами.

**GameObject:** `[CombatServer]`
- `NetworkObject` (NGO 2.x, scene-placed)
- `NetworkBehaviour` (этот скрипт)
- `Transform` (root, как у других серверов)

**Регистрация в `NetworkManagerController`:** добавить в `Awake()` (по аналогии с `CreateStatsClientState` / `CreateSkillsClientState` / `CreateEquipmentClientState`).

```csharp
private void CreateCombatClientState() {
    if (CombatClientState.Instance == null) {
        var go = new GameObject("[CombatClientState]");
        DontDestroyOnLoad(go);
        go.AddComponent<CombatClientState>();
    }
}
```

### 1.2 Scene-placed vs spawned

**CombatServer — scene-placed**, как и остальные. Singleton через `Instance`, auto-spawn через NGO.

**Регистрация IAttacker/IDamageTarget** — **в runtime**, на OnNetworkSpawn конкретных GameObject'ов (PlayerAttacker, NpcAttacker, Phase 3 — ShipAttacker). Не scene-placed.

---

## 2. NGO RPC (client ↔ server)

### 2.1 Client → Server RPCs

```csharp
public class CombatServer : NetworkBehaviour {
    public static CombatServer Instance { get; private set; }
    
    // === Основная атака ===
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestAttackRpc(ulong targetId, ulong sourceId, RpcParams rpcParams = default) {
        ulong attackerId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(attackerId)) return;
        ResolveAttack(attackerId, targetId, sourceId);
    }
    
    // === Skill-based attack (Phase 2) ===
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestSkillRpc(ulong targetId, string skillId, RpcParams rpcParams = default) {
        ulong attackerId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(attackerId)) return;
        ResolveSkill(attackerId, targetId, skillId);
    }
    
    // === Defend (стойка) ===
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestDefendRpc(RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        SetDefendingStance(clientId, true);
    }
    
    // === Stop defending ===
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestStopDefendRpc(RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        SetDefendingStance(clientId, false);
    }
    
    // === PvP duel (Phase 2) ===
    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RequestDuelRpc(ulong opponentId, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        SendDuelInvite(clientId, opponentId);
    }
    
    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RespondDuelRpc(ulong duelId, bool accept, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        if (accept) AcceptDuel(duelId);
        else DeclineDuel(duelId);
    }
}
```

### 2.2 Server → Client TargetRPCs (multicast)

```csharp
// Multicast to all clients in scene (default for combat)
[Rpc(SendTo.SpecifiedInParams)]
public void AttackLandedTargetRpc(DamageResultDto dto, RpcParams rpcParams) {
    CombatClientState.Instance.HandleAttackLanded(dto);
}

[Rpc(SendTo.SpecifiedInParams)]
public void DamageDealtTargetRpc(DamageResultDto dto, RpcParams rpcParams) {
    CombatClientState.Instance.HandleDamageDealt(dto);
}

[Rpc(SendTo.SpecifiedInParams)]
public void EntityKilledTargetRpc(DamageResultDto dto, RpcParams rpcParams) {
    CombatClientState.Instance.HandleEntityKilled(dto);
}

[Rpc(SendTo.SpecifiedInParams)]
public void OutOfRangeTargetRpc(ulong clientId, RpcParams rpcParams) {
    CombatClientState.Instance.HandleOutOfRange();
}

[Rpc(SendTo.SpecifiedInParams)]
public void AttackErrorTargetRpc(ulong clientId, AttackErrorCode code, RpcParams rpcParams) {
    CombatClientState.Instance.HandleError(code);
}
```

**Паттерн `SendTo.SpecifiedInParams`:** отправляем конкретному client (или списку). Multicast через `RpcTarget.Everyone` для combat events.

### 2.3 DTO (server ↔ client)

```csharp
[Serializable]
public struct DamageResultDto : INetworkSerializable {
    public int baseAttack;
    public float locMult;
    public float critMult;
    public float skillMult;
    public float hitChance;
    public int preDefenseDamage;
    public int effectiveDefense;
    public int finalDamage;
    public bool isCrit;
    public bool isHit;
    public byte hitLocation;       // 0=Limbs, 1=Torso, 2=Head (Phase 3)
    public byte damageType;        // 0=Physical, 1=Ballistic, 2=Antigrav, 3=Explosive, 4=Mesium
    public ulong attackerId;
    public ulong targetId;
    public ulong sourceId;
    public Vector3 attackerPosition;
    public Vector3 targetPosition;
    
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter {
        s.SerializeValue(ref baseAttack);
        s.SerializeValue(ref locMult);
        s.SerializeValue(ref critMult);
        s.SerializeValue(ref skillMult);
        s.SerializeValue(ref hitChance);
        s.SerializeValue(ref preDefenseDamage);
        s.SerializeValue(ref effectiveDefense);
        s.SerializeValue(ref finalDamage);
        s.SerializeValue(ref isCrit);
        s.SerializeValue(ref isHit);
        s.SerializeValue(ref hitLocation);
        s.SerializeValue(ref damageType);
        s.SerializeValue(ref attackerId);
        s.SerializeValue(ref targetId);
        s.SerializeValue(ref sourceId);
        s.SerializeValue(ref attackerPosition);
        s.SerializeValue(ref targetPosition);
    }
    
    public static DamageResultDto FromResult(DamageResult result) {
        return new DamageResultDto {
            baseAttack = result.baseAttack,
            locMult = result.locMult,
            critMult = result.critMult,
            skillMult = result.skillMult,
            hitChance = result.hitChance,
            preDefenseDamage = result.preDefenseDamage,
            effectiveDefense = result.effectiveDefense,
            finalDamage = result.finalDamage,
            isCrit = result.isCrit,
            isHit = result.isHit,
            hitLocation = result.hitLocation,
            damageType = (byte)result.damageType,
            attackerId = result.attackerId,
            targetId = result.targetId,
            sourceId = result.sourceId,
            attackerPosition = result.attackerPosition,
            targetPosition = result.targetPosition,
        };
    }
}

public enum AttackErrorCode : byte {
    OutOfRange = 0,
    OnCooldown = 1,
    InvalidTarget = 2,
    InvalidSource = 3,
    NotEnoughSeconds = 4,
    RateLimit = 5,
    AlreadyDead = 6,
}
```

**Вердикт:** `INetworkSerializable` + `BufferSerializer` (NGO 2.x pattern). Аналогично `SkillsSnapshotDto`.

---

## 3. Server-authoritative flow (anti-cheat)

### 3.1 Принцип: только сервер кидает кубики

**❌ ПЛОХО (cheatable):**
```csharp
[Rpc(SendTo.Server)]
public void RequestDamageRpc(int amount) {
    target.ApplyDamage(amount);  // клиент указал урон!
}
```

**✅ ХОРОШО (server-authoritative):**
```csharp
[Rpc(SendTo.Server)]
public void RequestAttackRpc(ulong targetId, ulong sourceId) {
    // Валидация: target существует, alive, in range, etc.
    var result = DamageCalculator.Calculate(attacker, target, source);  // SERVER кидает кубики
    target.ApplyDamage(result, attackerId);  // SERVER применяет
    BroadcastAttackLanded(result);  // SERVER бродкастит
}
```

**Клиент НИКОГДА не вычисляет damage. Только рисует UI после получения server-подтверждения.**

### 3.2 Client-prediction (Phase 2, опционально)

**Проблема:** на 100ms ping клиент видит "атаку началась, но damage ещё не пришёл" — lag.

**Решение (Phase 2):** клиент **предсказывает** damage (используя local RNG с тем же seed), рисует floating number сразу. Когда server присылает authoritative result — корректирует (если не совпало).

**MVP:** **без предсказания**. Клиент ждёт server-подтверждения. Лаг 100ms приемлем для MVP.

### 3.3 Multiplayer sync (пеший)

```csharp
public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId) {
    if (!_attackers.TryGetValue(attackerId, out var attacker)) return;
    if (!_targets.TryGetValue(targetId, out var target)) return;
    if (!target.IsAlive()) return;
    
    var source = attacker.GetDamageSource(sourceId);
    if (source == null) return;
    
    // Cooldown
    float now = Time.unscaledTime;
    if (!attacker.CanAttack(source, now)) {
        SendErrorToClient(attackerId, AttackErrorCode.OnCooldown);
        return;
    }
    
    // Range
    var rangePolicy = GetRangePolicy(source);
    if (!rangePolicy.IsInRange(attacker, target, source)) {
        SendErrorToClient(attackerId, AttackErrorCode.OutOfRange);
        return;
    }
    
    // Hit chance
    float hitChance = rangePolicy.CalculateHitChance(attacker, target, source);
    
    // Calculate damage
    var result = DamageCalculator.Calculate(attacker, target, source);
    result.hitChance = hitChance;
    
    // Set cooldown
    attacker.SetCooldown(source, now + source.GetCooldownSeconds());
    
    // Apply damage (server-side, authoritative)
    if (result.isHit) {
        target.ApplyDamage(result, attackerId);
    }
    
    // Broadcast to all clients (multicast)
    BroadcastAttackLanded(result);
    
    // WorldEvent (для подписчиков)
    WorldEventBus.Publish(new AttackLandedEvent { result = result });
    if (result.isHit) {
        WorldEventBus.Publish(new DamageDealtEvent { result = result });
    }
    if (!target.IsAlive()) {
        WorldEventBus.Publish(new EntityKilledEvent { result = result });
    }
}

private void BroadcastAttackLanded(DamageResult result) {
    var dto = DamageResultDto.FromResult(result);
    var rpcParams = new RpcParams {
        Send = new RpcSendParams { Target = RpcTarget.Everyone }
    };
    AttackLandedTargetRpc(dto, rpcParams);
}
```

**Вердикт:** server-authoritative, **все клиенты** получают broadcast (для visual + audio sync).

---

## 4. State management

### 4.1 CombatServer registries

```csharp
public class CombatServer : NetworkBehaviour {
    private Dictionary<ulong, IAttacker> _attackers = new();
    private Dictionary<ulong, IDamageTarget> _targets = new();
    private Dictionary<ulong, List<IDamageSource>> _sources = new();  // attackerId → list of sources
    
    // Register/unregister (called by PlayerAttacker/NpcAttacker on spawn/despawn)
    public void RegisterAttacker(ulong id, IAttacker attacker) {
        _attackers[id] = attacker;
    }
    public void RegisterTarget(ulong id, IDamageTarget target) {
        _targets[id] = target;
    }
    public void UnregisterAttacker(ulong id) {
        _attackers.Remove(id);
        _sources.Remove(id);
    }
    public void UnregisterTarget(ulong id) {
        _targets.Remove(id);
    }
}
```

### 4.2 Per-attacker cooldowns

```csharp
public class CooldownTracker {
    // Per (attackerId, sourceId) → readyTime
    private Dictionary<(ulong, ulong), float> _cooldowns = new();
    
    public bool IsReady(ulong attackerId, ulong sourceId, float now) {
        return !_cooldowns.TryGetValue((attackerId, sourceId), out var ready) || now >= ready;
    }
    
    public void SetCooldown(ulong attackerId, ulong sourceId, float until) {
        _cooldowns[(attackerId, sourceId)] = until;
    }
}
```

**Где хранить:** в `CombatServer` (POCO, server-side). **Не** реплицируется на клиентов (сервер сам считает).

### 4.3 Per-attacker defending stance

```csharp
// Per clientId → isDefending (true/false)
private Dictionary<ulong, bool> _defending = new();

public bool IsDefending(ulong clientId) {
    return _defending.TryGetValue(clientId, out var d) && d;
}

public void SetDefendingStance(ulong clientId, bool defending) {
    _defending[clientId] = defending;
    // Broadcast stance change (для UI)
    DefendingStanceChangedTargetRpc(clientId, defending, /* RpcTarget.Group */);
}
```

**Эффект:** `IDamageTarget.GetArmorDefense()` может учитывать стойку (+50% defense на ход). **MVP:** стойка = ×1.5 defense на 2 сек.

### 4.4 Health state (NetworkVariable on PlayerTarget)

```csharp
public class PlayerTarget : NetworkBehaviour, IDamageTarget {
    [SerializeField] private NetworkVariable<int> _currentHp = new(20);
    [SerializeField] private NetworkVariable<int> _maxHp = new(20);
    
    public int GetCurrentHp() => _currentHp.Value;
    public int GetMaxHp() => _maxHp.Value;
    
    public void ApplyDamage(DamageResult result, ulong attackerClientId) {
        if (!IsServer) return;
        int newHp = Mathf.Max(0, _currentHp.Value - result.finalDamage);
        _currentHp.Value = newHp;
    }
}
```

**NGO автоматически реплицирует** `NetworkVariable<int>` на все клиенты. UI читает `OnValueChanged`.

**То же для `NpcTarget`** (подтверждено 2.8) — `_currentHp` реплицируется через `NetworkVariable<int>`. Разница: NPC не имеет `OwnerClientId` (server-owned), поэтому `NetworkVariable` читается всеми клиентами через `SceneManager.Singleton.OnSceneEvent` или `BroadcastAttackLanded`.

---

## 5. Determinism vs anti-cheat

### 5.1 Сервер — истина, клиент — UX

| Аспект | Сервер | Клиент |
|---|---|---|
| **Damage calculation** | ✅ (authoritative) | ❌ (никогда) |
| **Hit chance roll** | ✅ (server) | ❌ |
| **Crit roll** | ✅ (server) | ❌ |
| **Cooldown tracking** | ✅ (server) | ❌ (но клиент предсказывает для UX) |
| **HP** | ✅ (NetworkVariable) | ✅ (replicated) |
| **Animation/sound** | ❌ (нет сервер-анимации) | ✅ (trigger при server-подтверждении) |
| **UI damage numbers** | ❌ (нет) | ✅ (рисует по server-данным) |

### 5.2 Network lag handling (MVP — без prediction)

```csharp
// Client side (в PlayerAttacker / InputHandler):
public void OnAttackPressed(ulong targetId, ulong sourceId) {
    // 1. Проверяем cooldown локально (UI feedback)
    if (LocalCooldownTracker.IsReady(...)) {
        // 2. Отправляем RPC
        CombatServer.Instance.RequestAttackRpc(targetId, sourceId);
        // 3. Ждём AttackLandedTargetRpc
        // 4. UI: floating damage number, hit flash, etc.
    } else {
        // UI: "On cooldown" toast
    }
}
```

**Lag = ~100ms** (100ms между нажатием и видимым уроном). Приемлемо для MVP. Phase 2: client prediction.

### 5.3 Anti-cheat: target switching

**Проблема:** игрок нажимает ЛКМ → target уже мёртв (только что убит другим игроком).

**Решение:**
```csharp
public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId) {
    if (!_targets.TryGetValue(targetId, out var target)) {
        SendError(attackerId, AttackErrorCode.InvalidTarget);
        return;
    }
    if (!target.IsAlive()) {
        SendError(attackerId, AttackErrorCode.AlreadyDead);
        return;
    }
    // ... rest of flow
}
```

**Клиент получает error → UI: "Target already dead"** (Phase 2).

### 5.4 Anti-cheat: distance spoofing

**Проблема:** игрок «подкручивает» позицию, чтобы бить издалека.

**Решение:** все distance checks на **сервере** (`rangePolicy.IsInRange` в `ResolveAttack`). Клиент не может обмануть.

### 5.5 Anti-cheat: cooldown bypass

**Проблема:** игрок спамит атаки быстрее, чем cooldown.

**Решение:** `attacker.CanAttack(source, now)` проверяется на сервере. `RateLimit` (10 ops/sec) защищает от RPC-spam.

---

## 6. Cooldown / timing

### 6.1 Server tick rate

```csharp
public class CombatServer : NetworkBehaviour {
    [SerializeField] private float _serverTickInterval = 1f / 30f;  // 30 Hz
    private float _lastTick = 0f;
    
    private void FixedUpdate() {
        if (!IsServer) return;
        float now = Time.fixedTime;
        if (now - _lastTick < _serverTickInterval) return;
        _lastTick = now;
        
        // Server tick: проверяем expired cooldowns, expired defending stances, etc.
        // (не критично — проверки по требованию)
    }
}
```

**Вердикт:** 30 Hz = 33ms. Достаточно для combat. Cooldowns проверяются **по требованию** (lazy, при ResolveAttack), не через tick.

### 6.2 Default cooldowns (server-side config)

Из `WeaponDamageSource.GetCooldownSeconds()`:
- d4/d6 (кинжал, меч) → 1.0 сек
- d8/d10 (копьё, двуручник) → 1.5 сек
- d12/d20 (мезиевое) → 2.5 сек

**Вердикт:** мелкое оружие быстрее, тяжёлое — медленнее. Дизайнер-конфигурируемо через `CombatConfig.baseMeleeCooldown` / `baseRangedCooldown`.

### 6.3 Defending stance duration

**MVP:** стойка длится **2 секунды** (или до следующей атаки/движения). Дизайнер-конфигурируемо.

---

## 7. Integration с существующими подсистемами

### 7.1 SkillsWorld (T-P12, opt-in)

**Движок читает:**
- `SkillsWorld.GetLearnedSkills(clientId)` — **после T-CB01..T-CB09** (MVP+1).
- Через `IDamageSource.GetSkillMultiplier(attackerId)` — навыки дают бонус (например, `HeavySwing` ×1.2, `CriticalStrike` ×1.5, `DodgeRoll` — снижение получаемого урона).

**MVP (T-RTC01..T-RTC10):** `GetSkillMultiplier` возвращает **1.0** (навыки не подключены). Движок работает.

**После T-CB01..T-CB09 (MVP+1):** `GetSkillMultiplier` интегрируется с `SkillsWorld`:
```csharp
public float GetSkillMultiplier(ulong attackerId) {
    var learned = SkillsWorld.Instance.GetLearnedSkills(attackerId);
    float mult = 1.0f;
    foreach (var skillId in learned) {
        var skill = SkillsWorld.Instance.GetSkillConfig(skillId);
        if (skill == null) continue;
        foreach (var eff in skill.effects) {
            // StatMod type с multiplier > 0 → skillMult (без cap, 2.18)
            if (eff.type == SkillEffect.Type.StatMod && eff.multiplier > 0) {
                mult *= eff.multiplier;
            }
        }
    }
    return mult;
}
```

### 7.2 StatsWorld (T-P03)

**Движок читает:**
- `StatsWorld.GetOrCreateStats(clientId)` → STR/DEX/INT/tiers.
- STR → damage bonus.
- DEX → hit chance modifier.

```csharp
public int GetStrength() => StatsWorld.Instance.GetOrCreateStats(_clientId).strengthTier * 5 + 10;
public int GetDexterity() => StatsWorld.Instance.GetOrCreateStats(_clientId).dexterityTier * 5 + 10;
public int GetIntelligence() => StatsWorld.Instance.GetOrCreateStats(_clientId).intelligenceTier * 5 + 10;
```

**Вердикт:** готов. STR/DEX читаются в `PlayerAttacker.GetStrength/GetDexterity`.

### 7.3 EquipmentWorld (T-P09)

**Движок читает:**
- `EquipmentWorld.GetEquipment(clientId)` → equipped weapon.
- `InventoryWorld.GetItemDataById(itemId)` → `WeaponItemData` (после T-CB03) или `ItemData` (до).

```csharp
// В PlayerAttacker.RebuildSources():
var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
if (equip.TryGetItemId(EquipSlot.WeaponMain, out var mainId)) {
    var data = InventoryWorld.Instance.GetItemDataById(mainId);
    if (data is WeaponItemData weapon) {
        _activeSources.Add(new WeaponDamageSource(weapon, mainId));
    } else {
        // До T-CB03: создаём дефолтный IDamageSource из ItemData
        // damageDice = d6, baseDamage = 1, critMod = 0
        _activeSources.Add(new DefaultDamageSource(mainId));
    }
}
```

**MVP:** `DefaultDamageSource` — fallback до T-CB03.

### 7.4 WorldEventBus

**Движок публикует (4 новых events):**
- `AttackStartedEvent` — игрок/NPC/Ship начал атаку (в `ResolveAttack`, после валидации).
- `AttackLandedEvent` — атака достигла цели (hit/miss).
- `DamageDealtEvent` — урон нанесён (только если hit).
- `EntityKilledEvent` — HP = 0, сущность уничтожена.

**Подписки (MVP+1):**
- `QuestServer` — отслеживает kills для квестов.
- `StatsServer` — начисляет XP за combat.
- `NpcAttacker` AI — реагирует на атаки.

### 7.5 NetworkManagerController

**Регистрация CombatClientState:**
```csharp
private void CreateCombatClientState() {
    if (CombatClientState.Instance == null) {
        var go = new GameObject("[CombatClientState]");
        DontDestroyOnLoad(go);
        go.AddComponent<CombatClientState>();
    }
}
```

**Вызывается в `Awake()`** (как и для других ClientState'ов).

---

## 8. Tick (server loop)

### 8.1 FixedUpdate в CombatServer

```csharp
public class CombatServer : NetworkBehaviour {
    private void FixedUpdate() {
        if (!IsServer) return;
        // Tick active entities (regen HP, expire stances, etc.)
        TickDefendingStances();
        // Cooldowns lazy (при ResolveAttack)
    }
    
    private void TickDefendingStances() {
        float now = Time.unscaledTime;
        var toRemove = new List<ulong>();
        foreach (var (clientId, expires) in _defendingExpiries) {
            if (now >= expires) toRemove.Add(clientId);
        }
        foreach (var clientId in toRemove) {
            _defending.Remove(clientId);
            _defendingExpiries.Remove(clientId);
            DefendingStanceExpiredTargetRpc(clientId, /* RpcTarget.Group */);
        }
    }
}
```

### 8.2 Regen (Phase 2)

**MVP:** без regen. После боя — wait или respawn. **Phase 2:** HP regen (1%/сек, configurable).

---

## 9. Error handling

### 9.1 Типы ошибок

| Ошибка | Код | Действие |
|---|---|---|
| `OutOfRange` | 0 | UI: "Out of range" toast |
| `OnCooldown` | 1 | UI: "Wait" toast (с timer) |
| `InvalidTarget` | 2 | UI: "Invalid target" |
| `InvalidSource` | 3 | UI: "Invalid weapon" (баг, report) |
| `NotEnoughSeconds` | 4 | (Phase 2, для turn-based) |
| `RateLimit` | 5 | UI: "Slow down" |
| `AlreadyDead` | 6 | UI: "Target is dead" |

### 9.2 Error DTO + RPC

```csharp
[Rpc(SendTo.SpecifiedInParams)]
public void AttackErrorTargetRpc(ulong clientId, byte errorCode, RpcParams rpcParams) {
    CombatClientState.Instance.HandleError((AttackErrorCode)errorCode);
}
```

**Клиент** получает error → UI toast.

---

## 10. Persistence

### 10.1 Что сохраняем

```csharp
// В CharacterSaveData (расширение):
[Serializable]
public class CombatSave {
    public int totalKills;        // NPC убито (для ачивок)
    public int totalDamageDealt;
    public int totalDamageTaken;
    public int highestSingleHit;
    public int currentKillStreak;  // подряд без смерти
}
```

**Триггеры save:**
- `EntityKilledEvent` → save (раз в 30 сек, batched).
- `OnDisconnect` → save.
- `OnNetworkDespawn` → save (server shutdown).

### 10.2 НЕ сохраняем in-flight combat state

HP, stance, cooldowns — **не сериализуются**. Disconnect = respawn с full HP (или partial, designer config).

---

## 11. Performance & scalability

### 11.1 Один сервер — 100 игроков в combat

**Расчёт:** 100 игроков × 1 атака/сек × 100 bytes (DamageResultDto) = 10 KB/сек. NGO 2.x справится (есть 64 KB/s на 1 клиента).

**Вердикт:** для MVP — ок. Phase 3 — оптимизация (delta-sync, AreaOfInterest).

### 11.2 CombatServer perf: O(n) per ResolveAttack

**Сложность:**
- `Dictionary.TryGetValue` — O(1).
- `Vector3.Distance` — O(1).
- `DamageCalculator.Calculate` — O(1) (несколько Random.Range + Math).
- `BroadcastAttackLanded` — O(n) где n = число клиентов. **NGO 2.x** оптимизирует (RPC target group).

**Вердикт:** для MVP — ok. Phase 3 — AreaOfInterest (бродкаст только ближайшим).

### 11.3 Tick rate: 30 Hz

**Server FixedUpdate** — 30 Hz (33ms). Достаточно для combat. Cooldowns проверяются lazy (при ResolveAttack).

---

## 12. Hooks для ship combat (anti-restrictive)

### 12.1 Что добавится в Phase 3 (без изменений в CombatServer)

```csharp
// Новый файл: Assets/_Project/Scripts/Combat/Implementations/ShipAttacker.cs
public class ShipAttacker : NetworkBehaviour, IAttacker {
    private ShipController _ship;
    private List<Turret> _turrets;
    public Vector3 GetPosition() => _ship.transform.position;
    public int GetStrength() => /* ship armor */ 50;
    public int GetDexterity() => /* pilot */ 10;
    public IReadOnlyList<IDamageSource> GetActiveDamageSources() => _turrets.Cast<IDamageSource>().ToList();
    // ... остальные методы ...
}

// Новый файл: ShipTarget.cs (аналогично)
// Новый файл: Turret.cs (IDamageSource)
// Новый файл: ShipRangePolicy.cs (IRangePolicy)
```

**CombatServer.ResolveAttack(attackerId, targetId, sourceId) — БЕЗ ИЗМЕНЕНИЙ.** Работает с `IAttacker/IDamageTarget/IDamageSource`, не знает о Player/Ship.

### 12.2 Turret → IDamageSource (FUTURE)

```csharp
public class Turret : MonoBehaviour, IDamageSource {
    [SerializeField] private TurretConfig _config;
    private float _lastFireTime;
    
    public ulong GetSourceId() => (ulong)GetInstanceID();
    public DamageType GetDamageType() => _config.damageType;  // Ballistic
    public DamageDice GetDamageDice() => _config.damageDice;  // d20 (крупнее)
    public int GetBaseDamage() => _config.baseDamage;
    public int GetCritModifier() => _config.critModifier;
    public float GetRange() => _config.range;  // 100-1000м
    public float GetCooldownSeconds() => _config.cooldownSeconds;
    public float GetSkillMultiplier(ulong attackerId) => /* pilot skill */ 1.0f;
    public string GetDisplayName() => _config.turretName;
}
```

**Anti-restrictive:** `Turret` — это просто `IDamageSource`. CombatServer не знает, что это турель.

### 12.3 ShipRangePolicy (FUTURE)

```csharp
public class ShipRangePolicy : IRangePolicy {
    public bool IsInRange(IAttacker a, IDamageTarget t, IDamageSource s) {
        return Distance(a, t) <= s.GetRange();  // 100-1000м
    }
    public bool RequiresLineOfSight => true;  // Phase 3: турели нужна прямая видимость
    public float CalculateHitChance(IAttacker a, IDamageTarget t, IDamageSource s) {
        // Учитывает маневренность, дистанцию, угол
        return 0.5f;  // Phase 3
    }
}
```

### 12.4 Ship vs Ship combat flow (FUTURE, без изменений)

```csharp
// В ShipController (Phase 3, new method):
[Rpc(SendTo.Server, RequireOwnership = false)]
public void RequestTurretFireRpc(ulong enemyShipId, ulong turretId, RpcParams rpcParams = default) {
    ulong pilotId = rpcParams.Receive.SenderClientId;
    if (!RateLimit(pilotId)) return;
    CombatServer.Instance.RequestAttackRpc(NetworkObjectId, enemyShipId, turretId);
    // ^^^^^ Точно тот же RPC, что и для пешего.
}
```

**CombatServer**:
```csharp
public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId) {
    // ... same code ...
    var attacker = _attackers[attackerId];  // ShipAttacker
    var target = _targets[targetId];  // ShipTarget
    var source = attacker.GetDamageSource(sourceId);  // Turret
    var result = DamageCalculator.Calculate(attacker, target, source);  // ERPR-формула
    // ...
}
```

**Вердикт:** **0 изменений** в `CombatServer`, `DamageCalculator`, интерфейсах. Только новые классы-реализации.

---

## 13. Hooks для навыков (T-CB01..T-CB09, MVP+1)

### 13.1 `WeaponDamageSource.GetSkillMultiplier` (T-CB07 hook)

```csharp
public float GetSkillMultiplier(ulong attackerId) {
    // MVP (T-RTC01..T-RTC10): всегда 1.0
    // MVP+1 (T-CB01..T-CB09): интеграция с SkillsWorld
    return 1.0f;
}
```

**После T-CB07:**
```csharp
public float GetSkillMultiplier(ulong attackerId) {
    var learned = SkillsWorld.Instance.GetLearnedSkills(attackerId);
    float mult = 1.0f;
    foreach (var skillId in learned) {
        var skill = SkillsWorld.Instance.GetSkillConfig(skillId);
        if (skill == null) continue;
        foreach (var eff in skill.effects) {
            if (eff.type == SkillEffect.Type.StatMod && eff.multiplier > 0) {
                mult *= eff.multiplier;  // БЕЗ CAP (per 2.18)
            }
        }
    }
    return mult;
}
```

### 13.2 Defense buff (от навыков)

```csharp
// В PlayerTarget.GetArmorDefense():
public int GetArmorDefense() {
    int baseArmor = /* sum from ClothingItemData.armorDefense */;
    
    // Skill bonus: defense_master_defender ×1.2 (из Battle/20_SKILL_TREES.md §5.1)
    var learned = SkillsWorld.Instance.GetLearnedSkills(_clientId);
    float skillMult = 1.0f;
    foreach (var skillId in learned) {
        var skill = SkillsWorld.Instance.GetSkillConfig(skillId);
        if (skill == null) continue;
        // Если навык типа "defense_mult" — применить
    }
    
    return Mathf.RoundToInt(baseArmor * skillMult);
}
```

### 13.3 Dodge / crit (от навыков)

**Phase 2:** навыки типа `DodgeRoll` снижают hitChance на X%. Навыки типа `PrecisionStrike` увеличивают crit chance. Combat-движок предоставляет `IDamageSource.GetSkillMultiplier` + `IDamageTarget.GetDefenseModifier(attacker, source)` hooks.

---

## 14. Что НЕ делаем (явные запреты)

- ❌ Не делаем ship combat (Phase 3, отложено).
- ❌ Не делаем NPC-AI для open world (отдельная подсистема).
- ❌ Не делаем PvP duel flow (T-RTC11..T-RTC15, Phase 2).
- ❌ Не делаем UI damage numbers (T-RTC10, Phase 2).
- ❌ Не делаем line-of-sight (Phase 2).
- ❌ Не делаем anti-cheat beyond server-authoritative (Phase 3).
- ❌ Не делаем client prediction (Phase 2).
- ❌ Не делаем turn-based (`turn-based-battles/`, parking).
- ❌ Не пишем код в этой сессии.
