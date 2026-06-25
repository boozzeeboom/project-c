# Technical — фактическая реализация Real-Time Combat Engine (v0.1.4, 2026-06-25)

> **Статус:** ✅ MVP реализован. Этот документ описывает **то, что есть в коде** (не дизайн).
> **Отличия от дизайна (`10_DESIGN.md`):** race condition фиксы (push-down + second-chance), `EnsureUnarmedFallback` v0.1.3, corpse delay v0.1.4, `NpcAttacker` стал `NetworkBehaviour` (был `MonoBehaviour`).
> **Ключевая идея:** server-authoritative, NGO 2.x RPC, scene-placed `[CombatServer]` в `BootstrapScene` (singleton), push-down + pull-up registration для race-safety.

---

## 1. Scene placement

### 1.1 `[CombatServer]` GameObject

| Свойство | Значение |
|---|---|
| Path | `Assets/_Project/Scenes/BootstrapScene.unity` |
| Имя | `[CombatServer]` |
| Компоненты | `Transform` (root) + `NetworkObject` + `CombatServer` |
| Spawn | scene-placed через `ScenePlacedObjectSpawner` (re-spawn при StartHost) |
| Singleton | `CombatServer.Instance` устанавливается в `OnNetworkSpawn` (server-only) |

**Создание через execute_code (Edit Mode):**
```csharp
var go = new GameObject("[CombatServer]");
var netObj = go.AddComponent<Unity.Netcode.NetworkObject>();
var cs = go.AddComponent<ProjectC.Combat.CombatServer>();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
```

### 1.2 `NPC_TestEnemy` GameObject

| Свойство | Значение |
|---|---|
| Path | `Assets/_Project/Scenes/World/WorldScene_0_0.unity` |
| Имя | `NPC_TestEnemy` |
| Position | `(30, 0, 0)` относительно `WorldRoot_0_0` = `(40030, 2502, 40030)` (в мировых координатах) |
| Компоненты | `Transform` + `NetworkObject` + `NpcAttacker` + `NpcTarget` |
| Дочерний | `VisualMarker` (Capsule primitive, красный URP Lit, scale=0.8×1×0.8, pos +1м Y) |

**Создание через execute_code (Edit Mode):** см. `50_IMPL_CHANGELOG.md §3.1`.

### 1.3 `CombatClientState` — программный singleton

Создаётся в `NetworkManagerController.CreateCombatClientState()` (add-only), как **root GameObject** с `DontDestroyOnLoad` (паттерн идентичен `MarketClientState`, `ContractClientState` и т.п.). Если в сцене уже есть root-инстанс — skip; иначе создать новый root, чтобы пережить streaming сцен.

---

## 2. Registration flow (race-safe, v0.1.2)

### 2.1 Проблема

В NGO 2.x порядок `OnNetworkSpawn` у scene-placed NetworkObjects **не гарантирован**. Ситуация:
- `NetworkPlayer.OnNetworkSpawn` срабатывает **раньше** `CombatServer.OnNetworkSpawn` → `CombatServer.Instance==null` в момент `RegisterWithCombatServer()` → `AddComponent` пропускался (ранний return в v0.0) → `PlayerAttacker/Target` не создавались.
- `PlayerAttacker.ClientId == 0` для host player (server's own client), и мой skip `if (id == 0) continue;` отфильтровывал host.

### 2.2 Решение (v0.1.2)

**Двухсторонняя защита:**

#### Pull-up (NetworkBehaviour.OnNetworkSpawn → Register)
- `PlayerAttacker : NetworkBehaviour` + `OnNetworkSpawn` override → если `CombatServer.Instance != null` → `RegisterAttacker(_clientId, this)`.
- `PlayerTarget : NetworkBehaviour` (был уже) + `OnNetworkSpawn` override → то же.
- `NpcAttacker : NetworkBehaviour` (v0.1: был MonoBehaviour) + `OnNetworkSpawn` override.
- `NpcTarget : NetworkBehaviour` (был уже) + `OnNetworkSpawn` override (с fallback-init HP если `_targetId==0`).
- `OnNetworkDespawn` override у всех четырёх → `Unregister` в CombatServer.

#### Push-down (CombatServer.OnNetworkSpawn → RecoverExistingEntities)
- После `Instance = this` в `CombatServer.OnNetworkSpawn` → `RecoverExistingEntities()`.
- `FindObjectsByType<PlayerAttacker/PlayerTarget/NpcAttacker/NpcTarget>(FindObjectsSortMode.None)` → для каждого, если `!_attackers.ContainsKey(id)` → `Register`.
- Для **Player** — `if (id == 0) continue;` **УБРАН** (0 = валидный clientId для host).
- Для **NPC** — `if (id == 0) continue;` **ОСТАВЛЕН** (0 = не инициализирован).
- Логирует каждое действие: `[CombatServer] RecoverExistingEntities: registered PlayerAttacker id=0` и т.п.

#### Second-chance (v0.1.2)
- В `CombatServer.OnNetworkSpawn` после `RecoverExistingEntities()` → `Invoke(nameof(RecoverExistingEntities), 1.0f)`.
- Через 1 сек повторный find. На случай, если Player NetworkObject spawned'ится ПОЗЖЕ CombatServer (push-down в OnNetworkSpawn его не ловит, а pull-up ещё не сработал).

### 2.3 Хронология (факт из Play Mode #5)

```
[71]  NetworkPlayer.RegisterWithCombatServer: components added (PlayerAttacker/Target), 
       but CombatServer.Instance==null — push-down will catch up. clientId=0
[87]  CombatServer.OnNetworkSpawn: Instance set, IsServer=True.
[88]  RecoverExistingEntities: registered PlayerAttacker id=0   ← push-down
[89]  RecoverExistingEntities: registered PlayerTarget id=0      ← push-down
[90]  RecoverExistingEntities done: attackers=1, targets=1
[228] Registered attacker id=140956 (NpcAttacker)                ← pull-up NpcAttacker
[229] NpcTarget.OnNetworkSpawn fallback-init: HP=20              ← pull-up NpcTarget
[230] Registered target id=45 (NpcTarget)                        ← уже был через push-down? нет, race.
[243] RecoverExistingEntities done: attackers=2, targets=2       ← second-chance
```

**Итог:** `attackers=2 (Player id=0 + Npc id=140956), targets=2 (Player id=0 + Npc id=45)`. Готово к бою.

---

## 3. ERPR Damage Formula (факт, в `DamageCalculator.cs`)

```csharp
public static DamageResult Calculate(
    IAttacker attacker,
    IDamageTarget defender,
    IDamageSource source,
    IRangePolicy rangePolicy,
    object skill = null)  // MVP: всегда null
{
    // 1. Base attack: roll dN + base + STR
    int roll = source.GetDamageDice().Roll();  // Random.Range(1, N+1)
    int baseAttack = roll + source.GetBaseDamage() + attacker.GetStrength();

    // 2. Hit chance (from range policy)
    float hitChance = rangePolicy.CalculateHitChance(attacker, defender, source);
    bool isHit = Random.value < hitChance;

    if (!isHit) return DamageResult.Miss(...);

    // 3. Hit location — ОТКЛЮЧЕН в real-time (per 2.17)
    float locMult = 1.0f;
    byte hitLocation = 1;  // Torso (default)

    // 4. Crit (1d100 + critMod >= 100 → ×2)
    int critRoll = Random.Range(1, 101);
    bool isCrit = (critRoll + source.GetCritModifier()) >= 100;
    float critMult = isCrit ? 2.0f : 1.0f;

    // 5. Skill multiplier (от навыков, opt-in, БЕЗ CAP per 2.18)
    float skillMult = source.GetSkillMultiplier(attackerId);

    // 6. Pre-defense damage
    int preDefense = Mathf.RoundToInt(baseAttack * locMult * critMult * skillMult);

    // 7. Defense (armor × typeMultiplier)
    int totalArmor = defender.GetArmorDefense();
    float armorMult = source.GetDamageType().ArmorMultiplier();
    int effectiveDefense = Mathf.RoundToInt(totalArmor * armorMult);

    // 8. Final
    int final = Mathf.Max(0, preDefense - effectiveDefense);

    return new DamageResult { /* fill all fields */ };
}
```

**Constants:**
- `BaseCritThreshold = 100`
- `CritMultiplier = 2.0f`
- HitLocation: `locMult = 1.0`, `hitLocation = 1` (Torso) — отключён per 2.17
- SkillMult: без cap per 2.18

**DamageType → armor multiplier** (per `DamageTypeExtensions.ArmorMultiplier`):
| Type | Multiplier |
|---|---|
| Physical | 1.0 |
| Ballistic | 1.0 |
| Antigrav | 0.5 (g-волна частично игнорирует броню) |
| Explosive | 0.7 |
| Mesium | 0.0 (токсин не блокируется) |

**HitChance formulas** (per 2.1):

`MeleeRangePolicy.CalculateHitChance`:
- `distMod = clamp01(1 - (dist - 1.5) / 2)` (1.0 на dist≤1.5м, 0 на dist≥3.5м)
- `dexMod = clamp01(0.85 + (DEX - 10) * 0.015)` (0.85 на DEX 10, 0.925 на DEX 20)
- `baseMelee = 0.85`
- `hitChance = clamp01(0.85 * distMod * dexMod)`

`RangedRangePolicy.CalculateHitChance`:
- `distMod = clamp01(1 - dist / maxRange)` (1.0 на dist=0, 0 на dist=maxRange)
- `dexMod = clamp01(0.85 + (DEX - 10) * 0.015)`
- `baseRanged = 0.75`
- `hitChance = clamp01(0.75 * distMod * dexMod)`

---

## 4. Server-authoritative flow (факт)

### 4.1 Server → Client (multicast)

| RPC | Target | Назначение |
|---|---|---|
| `RequestAttackRpc(ulong targetId, ulong sourceId, RpcParams)` | `SendTo.Server, RequireOwnership=true` | client → server, начать атаку |
| `RequestSkillRpc(ulong targetId, string skillId, RpcParams)` | `SendTo.Server, RequireOwnership=true` | Phase 2: skill-based attack |
| `RequestDefendRpc(RpcParams)` | `SendTo.Server, RequireOwnership=true` | Phase 2: defend stance |
| `AttackLandedTargetRpc(DamageResultDto dto, RpcParams)` | `SendTo.SpecifiedInParams` (Everyone) | broadcast результат |
| `EntityKilledTargetRpc(DamageResultDto dto, RpcParams)` | `SendTo.SpecifiedInParams` (Everyone) | broadcast смерти |
| `AttackErrorTargetRpc(ulong clientId, string code, RpcParams)` | `SendTo.SpecifiedInParams` (Single) | error → конкретный client |

### 4.2 `DamageResultDto` (INetworkSerializable)

Содержит все поля `DamageResult` (см. `Core/DamageResult.cs`) + `static FromResult(in DamageResult)` конвертер. Сериализуется через `BufferSerializer<T>` (стандарт NGO 2.x).

### 4.3 ResolveAttack (server-side, факт в `CombatServer.cs`)

```csharp
public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId) {
    if (!_attackers.TryGetValue(attackerId, out var attacker)) return;
    if (!_targets.TryGetValue(targetId, out var target)) return;
    if (!target.IsAlive()) { SendErrorToClient(attackerId, "AlreadyDead"); return; }
    if (!attacker.IsAlive()) return;

    var source = attacker.GetDamageSource(sourceId);
    if (source == null) { SendErrorToClient(attackerId, "InvalidSource"); return; }

    float now = Time.unscaledTime;
    if (!attacker.CanAttack(source, now)) { SendErrorToClient(attackerId, "OnCooldown"); return; }

    IRangePolicy rangePolicy = source.GetRange() < 3.0f
        ? (IRangePolicy)new MeleeRangePolicy()
        : new RangedRangePolicy();

    if (!rangePolicy.IsInRange(attacker, target, source)) { SendErrorToClient(attackerId, "OutOfRange"); return; }

    var result = DamageCalculator.Calculate(attacker, target, source, rangePolicy);
    attacker.SetCooldown(source, now + source.GetCooldownSeconds());

    if (result.isHit) target.ApplyDamage(result, attackerId);

    var dto = DamageResultDto.FromResult(result);
    var rpcParams = new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Everyone } };
    AttackLandedTargetRpc(dto, rpcParams);

    WorldEventBus.Publish(new AttackLandedEvent { PlayerId = attackerId, Result = result });
    if (result.isHit) WorldEventBus.Publish(new DamageDealtEvent { PlayerId = attackerId, Result = result });
    if (result.isHit && !target.IsAlive()) {
        WorldEventBus.Publish(new EntityKilledEvent { PlayerId = attackerId, Result = result });
        EntityKilledTargetRpc(dto, rpcParams);
    }
}
```

### 4.4 Anti-cheat (server-authoritative)

| Угроза | Защита | Где |
|---|---|---|
| Подмена damage | Server rolls dice, client только рисует UI | `DamageCalculator.Calculate` на server |
| Distance spoofing | Все distance checks на server | `IRangePolicy.IsInRange` в `ResolveAttack` |
| Cooldown bypass | `attacker.CanAttack(source, now)` на server | `CombatServer.IsCooldownReady` (централизованно per 2.3) |
| Target switching на мёртвом NPC | `target.IsAlive()` check | `ResolveAttack` |
| RPC spam | `RateLimit` 10 ops/sec per client | `CombatServer.RateLimit` |
| Weapon spoofing | `GetDamageSource(sourceId)` — server ищет в реальном списке | `attacker.GetDamageSource` |

---

## 5. Cooldown (централизованно per 2.3)

`CombatServer` хранит `Dictionary<(ulong attackerId, ulong sourceId), float> _cooldowns` (readyTime в `Time.unscaledTime`).

- `IAttacker.CanAttack(source, now)` → `CombatServer.IsCooldownReady(_clientId, source.GetSourceId(), now)`.
- `IAttacker.SetCooldown(source, until)` → `CombatServer.SetCooldown(_clientId, source.GetSourceId(), until)`.
- `PlayerAttacker` — passthrough в CombatServer.
- `NpcAttacker` — per-component `float _lastAttackTime` (NPC-враги малочисленны, не конкурируют за cooldown-таблицу).

**Default cooldowns** (в `DefaultDamageSource` / `WeaponDamageSource`):
- `d4/d6` → 1.0s
- `d8/d10` → 1.5s
- `d12/d20` → 2.5s

---

## 6. Range policy (auto-select per source)

В `CombatServer.ResolveAttack`:
```csharp
IRangePolicy rangePolicy = source.GetRange() < 3.0f
    ? (IRangePolicy)new MeleeRangePolicy()
    : new RangedRangePolicy();
```

**MVP:** hardcoded threshold 3.0м. После T-CB03 — designer-конфигурируемо через `CombatConfig`.

---

## 7. HP state (NetworkVariable)

`PlayerTarget` и `NpcTarget` используют `NetworkVariable<int> _currentHp` + `NetworkVariable<int> _maxHp`. NGO автоматически реплицирует на все клиенты. UI подписывается на `OnValueChanged` (когда будет T-RTC10).

**MVP default:**
- `PlayerTarget._currentHp = 20` (default NetworkVariable init)
- `NpcTarget._currentHp = _data.maxHp` (default 30, после v0.1.4 NpcTarget.OnNetworkSpawn fallback-init из `_data` → 20 для `Npc_Goblin.asset`)

---

## 8. State management

### 8.1 Registries

```csharp
private readonly Dictionary<ulong, IAttacker> _attackers = new();
private readonly Dictionary<ulong, IDamageTarget> _targets = new();
```

Заполняются через:
- `RegisterAttacker(id, attacker)` / `RegisterTarget(id, target)` — explicit (pull-up)
- `RecoverExistingEntities()` — push-down (OnNetworkSpawn + Invoke second-chance)
- `UnregisterAttacker(id)` / `UnregisterTarget(id)` — при despawn

### 8.2 Cooldown + Rate limit

- `_cooldowns` — per (attackerId, sourceId)
- `_nextAllowedTime` — per clientId, 10 ops/sec

---

## 9. Tick (server loop)

**НЕТ explicit tick.** Все проверки **lazy** (при ResolveAttack):
- Cooldown: `Time.unscaledTime` против `_cooldowns[(id, srcId)]`.
- Defending stances: не реализовано (Phase 2).
- HP regen: не реализовано (Phase 2).

`FixedUpdate` в `CombatServer` отсутствует (только `OnNetworkSpawn/Despawn/RecoverExistingEntities/Invoke`).

---

## 10. Error handling (факт)

`AttackErrorCode` enum — **НЕ реализован** в коде. Используются **string константы** для простоты MVP:

| Код | Триггер | Действие |
|---|---|---|
| `"AlreadyDead"` | `target.IsAlive() == false` | UI toast (Phase 2) |
| `"OnCooldown"` | `attacker.CanAttack == false` | UI toast |
| `"OutOfRange"` | `rangePolicy.IsInRange == false` | UI toast |
| `"InvalidSource"` | `GetDamageSource(sourceId) == null` | UI toast |
| `"InvalidTarget"` | `_targets[targetId] == null` | (нет error code, return) |

`AttackErrorTargetRpc(ulong clientId, string code, RpcParams)` → `CombatClientState.HandleError(code)` → `OnAttackError` event + Debug.Log.

---

## 11. Persistence

**НЕ реализовано.** Disconnect = respawn with full HP (designer config). HP, stance, cooldowns не сериализуются. После Phase 2 — `CharacterSaveData` extension (отдельный тикет).

---

## 12. Performance & scalability

- `Dictionary.TryGetValue` — O(1)
- `Vector3.Distance` — O(1)
- `DamageCalculator.Calculate` — O(1) (Random + Math)
- `BroadcastAttackLanded` — O(n) где n = число клиентов. NGO 2.x RPC overhead.

**MVP:** 100 игроков × 1 атака/сек × ~50 bytes DamageResultDto = 5 KB/сек → OK. Phase 3 — AreaOfInterest.

---

## 13. Integration с существующими подсистемами (add-only)

### 13.1 `Assets/_Project/Core/WorldEvent.cs`

Добавлены 4 event-класса в конец файла (add-only, не трогаем существующие):
- `AttackStartedEvent { ulong AttackerId; ulong TargetId; ulong SourceId; }`
- `AttackLandedEvent { DamageResult Result; }`
- `DamageDealtEvent { DamageResult Result; }`
- `EntityKilledEvent { DamageResult Result; }`

`WorldEvent` base class уже имеет `PlayerId` и `TimestampUnix`.

### 13.2 `Assets/_Project/Scripts/Core/NetworkManagerController.cs`

Добавлены (add-only):
- Вызов `CreateCombatClientState()` в `Awake()` (после `CreateDockingClientState()`).
- Метод `CreateCombatClientState()` (паттерн идентичен другим `Create*ClientState`).

### 13.3 `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

Добавлены (add-only):
- `using ProjectC.Combat;`
- Вызов `RegisterWithCombatServer()` в `OnNetworkSpawn` (после всех других setup).
- Метод `RegisterWithCombatServer()` (v0.1.1: без раннего return).
- Метод `UnregisterFromCombatServer()`.
- Метод `DebugAttackNearestNpc()` (временный, для verify).
- K-key handler в `Update()` (debug, можно удалить после T-RTC10).

**БЕЗ изменений:** `SpawnCamera`, `SpawnInventory`, `OnNetworkDespawn` кроме добавленного `UnregisterFromCombatServer()` в самом конце.

### 13.4 `Assets/_Project/Resources/Combat/`

Созданы 2 SO assets (через `execute_code` Edit Mode):
- `CombatConfig_Default.asset` — дефолтные значения, не подключён к CombatServer (hardcoded).
- `Npc_Goblin.asset` — displayName="Goblin Test", maxHp=20, STR/DEX=10, INT=8, d6, base=2, range=2м, cooldown=1.5s.

### 13.5 Scene edits

- `BootstrapScene.unity`: добавлен `[CombatServer]` GameObject (NetworkObject + CombatServer).
- `WorldScene_0_0.unity`: добавлен `NPC_TestEnemy` GameObject (NetworkObject + NpcAttacker + NpcTarget + VisualMarker capsule child).

---

## 14. Hooks для будущего

### 14.1 Ship combat (Phase 3) — anti-restrictive

Новые классы, **0 изменений в `CombatServer.cs`**:
- `ShipAttacker : NetworkBehaviour, IAttacker` — `GetPosition` → `ShipController.transform.position`, `GetActiveDamageSources` → список турелей.
- `ShipTarget : NetworkBehaviour, IDamageTarget` — `GetArmorDefense` → `armorHull + armorShield`.
- `Turret : NetworkBehaviour, IDamageSource` — d20, Ballistic, range 100-1000м, cooldown 2-5s.
- `ShipRangePolicy : IRangePolicy` — line-of-sight (raycast), ship маневренность.

`CombatServer.ResolveAttack(shipId, enemyShipId, turretId)` — **тот же код**, что и для пешего.

### 14.2 Skills (T-CB01..09, MVP+1)

`IDamageSource.GetSkillMultiplier(attackerId)` — hook готов. После T-CB01..09:
```csharp
public float GetSkillMultiplier(ulong attackerId) {
    var learned = SkillsWorld.Instance.GetLearnedSkills(attackerId);
    float mult = 1.0f;
    foreach (var skillId in learned) {
        var skill = SkillsWorld.Instance.GetSkillConfig(skillId);
        if (skill == null) continue;
        foreach (var eff in skill.effects) {
            if (eff.type == SkillEffect.Type.StatMod && eff.multiplier > 0) {
                mult *= eff.multiplier;
            }
        }
    }
    return mult;
}
```

Подробнее: `60_NEXT_STEPS_T-CB01.md`.

### 14.3 Armor (после T-CB06, `armorDefense` поле в `ClothingItemData`)

`PlayerTarget.GetArmorDefense()`:
```csharp
public int GetArmorDefense() {
    int total = 0;
    var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
    foreach (var slot in new[] { EquipSlot.Head, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Feet, EquipSlot.Back }) {
        if (equip.TryGetItemId(slot, out var itemId)) {
            var data = InventoryWorld.Instance.GetItemDefinition(itemId);
            if (data is ClothingItemData c) total += c.armorDefense;
        }
    }
    return total;
}
```

Сейчас возвращает 0 (TODO комментарий).

---

## 15. Что НЕ делаем

- ❌ UI damage numbers (T-RTC10, Phase 2).
- ❌ Line-of-sight (Phase 2).
- ❌ Client prediction (Phase 2).
- ❌ PvP duel flow (T-RTC11..15, Phase 2).
- ❌ Ship combat (T-RTC16..20, Phase 3).
- ❌ NPC-AI (отдельная подсистема).
- ❌ `WeaponItemData` (T-CB03) — MVP с `DefaultDamageSource` fallback.
- ❌ `armorDefense` поле в `ClothingItemData` (T-CB06) — MVP `GetArmorDefense() => 0`.
- ❌ `CombatConfig` runtime hookup — hardcoded defaults.
- ❌ Persistence (HP, cooldowns).
- ❌ Respawn (NPC corpse удаляется через 3 сек, без respawn).
