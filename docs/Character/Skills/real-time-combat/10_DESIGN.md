# Design — архитектура Real-Time Combat Engine (anti-restrictive)

> **Дата:** 2026-06-25 (v0.3 design + v0.1.4 implementation status)
> **Базируется на:** `01_ANALYSIS.md` (gaps), `02_LORE.md` (лор-база), `Battle/10_DESIGN.md §7` (ERPR-формула)
> **Ключевой принцип:** **anti-restrictive**. Движок не знает, что есть «только пешие». Оперирует **абстракциями** (`IAttacker`, `IDamageTarget`, `IDamageSource`, `IRangePolicy`). Конкретные реализации — **композиция** + **стратегии**. Это позволяет добавить **ship combat в будущем** (Phase 3) **без рефакторинга ядра**.
>
> **⚠️ ВАЖНО:** этот документ описывает **архитектурный дизайн**. Фактическая реализация (v0.1.4) может отличаться в деталях — см. `20_TECHNICAL.md` (technical факт) и `50_IMPL_CHANGELOG.md` (что и почему изменилось).

### Status legend (v0.1.4)

| Секция | Status | Комментарий |
|---|---|---|
| §1 Высокоуровневая архитектура | ✅ | Реализовано (19 файлов + 2 SO + 2 scene edits) |
| §2.1 IAttacker | ✅ | Без изменений. `PlayerAttacker/NpcAttacker/ShipAttacker` реализуют |
| §2.2 IDamageTarget | ✅ | Без изменений. `PlayerTarget/NpcTarget/ShipTarget` реализуют |
| §2.3 IDamageSource | ✅ | `DefaultDamageSource` (MVP). `WeaponDamageSource` — после T-CB03 |
| §2.4 IRangePolicy | ✅ | `MeleeRangePolicy/RangedRangePolicy`. `ShipRangePolicy` — Phase 3 |
| §2.5 DamageResult | ✅ | Без изменений |
| §3.1 PlayerAttacker | ✅ | + race-safe v0.1.1 (NetworkBehaviour + self-register + unarmed fallback v0.1.3) |
| §3.2 PlayerTarget | ✅ | + race-safe v0.1.2, corpse delay N/A (player не corpse) |
| §3.3 NpcAttacker | ✅ | + v0.1: MonoBehaviour → NetworkBehaviour |
| §3.4 NpcTarget | ✅ | + v0.1.4: corpse delay 3s |
| §3.5 WeaponDamageSource | ⏸ | T-CB03 (after MVP+1). Сейчас `DefaultDamageSource` |
| §3.6/3.7 MeleeRangePolicy/RangedRangePolicy | ✅ | + hardcoded threshold 3м для auto-select (designer config — T-RTC09 follow-up) |
| §4 DamageCalculator | ✅ | Без изменений. ERPR-формула |
| §5 CombatServer | ✅ | + race-safe v0.1.2 (push-down + second-chance), + scene-placed в BootstrapScene |
| §6 CombatClientState | ✅ | + auto-create в `NetworkManagerController.CreateCombatClientState()` |
| §7 CombatConfig | ✅ | SO создан (`CombatConfig_Default.asset`), hardcoded defaults (T-RTC09 follow-up) |
| §8 WorldEvent | ✅ | 4 event-класса добавлены в `WorldEvent.cs` |
| §9 Lifecycle | ✅ | + self-register pattern в OnNetworkSpawn |
| §10 Ship combat hooks | 📝 | Только design (Phase 3) |
| §11 End-to-end сценарий | ✅ | Работает (см. `00_README.md §Play Mode verify`) |
| §12 Что НЕ делаем | ✅ | T-RTC10, ship, NPC-AI, TB, client-prediction — отложены |

---

## 1. Высокоуровневая архитектура [✅ реализовано как в дизайне, отличия в деталях]

```
┌─────────────────────────────────────────────────────────────────────────────┐
│            REAL-TIME COMBAT ENGINE (NEW, MVP)                                │
│                                                                             │
│  ┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐ │
│  │   IAttacker         │  │   IDamageTarget     │  │   IDamageSource     │ │
│  │   (interface)       │  │   (interface)       │  │   (interface)       │ │
│  │                     │  │                     │  │                     │ │
│  │ - GetPosition()     │  │ - GetPosition()     │  │ - DamageType        │ │
│  │ - GetStrength()     │  │ - GetCurrentHp()    │  │ - DamageDice        │ │
│  │ - GetDexterity()    │  │ - GetMaxHp()        │  │ - BaseDamage        │ │
│  │ - GetIntelligence() │  │ - GetArmorDefense() │  │ - CritModifier      │ │
│  │ - GetActiveDamageSrcs() │ - ApplyDamage()   │  │ - Range             │ │
│  │ - IsAlive()         │  │ - IsAlive()         │  │ - AttackSecondsCost │ │
│  │ - IsPlayer()        │  │ - IsPlayer()        │  │ - GetSkillMult()    │ │
│  └─────────────────────┘  └─────────────────────┘  └─────────────────────┘ │
│           ▲                      ▲                      ▲                │
│           │                      │                      │                │
│  ┌────────┴──────────────────────┴──────────────────────┴────────────┐  │
│  │                  Конкретные реализации (MVP)                          │  │
│  │                                                                       │  │
│  │  ┌────────────────────┐  ┌────────────────────┐  ┌─────────────┐  │  │
│  │  │ PlayerAttacker     │  │ PlayerTarget       │  │ Weapon-     │  │  │
│  │  │ (NetworkBehaviour, │  │ (NetworkBehaviour, │  │ DamageSource│  │  │
│  │  │  IAttacker)        │  │  IDamageTarget)    │  │ (IDamageSrc)│  │  │
│  │  └────────────────────┘  └────────────────────┘  └─────────────┘  │  │
│  │  ┌────────────────────┐  ┌────────────────────┐  ┌─────────────┐  │  │
│  │  │ NpcAttacker        │  │ NpcTarget          │  │ Explosion-  │  │  │
│  │  │ (MonoBehaviour,    │  │ (NetworkBehaviour, │  │ DamageSource│  │  │
│  │  │  IAttacker)        │  │  IDamageTarget)    │  │ (AoE, Phase2)│  │  │
│  │  └────────────────────┘  └────────────────────┘  └─────────────┘  │  │
│  │  ┌────────────────────┐  ┌────────────────────┐  ┌─────────────┐  │  │
│  │  │ ShipAttacker       │  │ ShipTarget         │  │ Turret-     │  │  │
│  │  │ (NetworkBehaviour, │  │ (NetworkBehaviour, │  │ DamageSource│  │  │
│  │  │  IAttacker)        │  │  IDamageTarget)    │  │ (Phase 3)   │  │  │
│  │  │ [FUTURE]           │  │ [FUTURE]           │  │              │  │  │
│  │  └────────────────────┘  └────────────────────┘  └─────────────┘  │  │
│  │                                                                       │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────┐     │
│  │              CombatServer (NetworkBehaviour, scene-placed)         │     │
│  │  - Dictionary<ulong, IAttacker> _attackers                         │     │
│  │  - Dictionary<ulong, IDamageTarget> _targets                       │     │
│  │  - Dictionary<ulong, List<IDamageSource>> _sources                 │     │
│  │  - RequestAttackRpc, RequestSkillRpc, RequestDefendRpc              │     │
│  │  - ResolveAttack(attackerId, targetId, sourceId)                   │     │
│  │  - Не знает, что атакует Player/Ship/Npc — generic                  │     │
│  └──────────────────────────────────────────────────────────────────┘     │
│                          │                                                │
│                          ▼                                                │
│  ┌──────────────────────────────────────────────────────────────────┐     │
│  │              DamageCalculator (static class)                        │     │
│  │  public static DamageResult Calculate(                              │     │
│  │      IAttacker attacker, IDamageTarget defender,                  │     │
│  │      IDamageSource source, SkillNodeConfig skill = null)         │     │
│  │  - Generic, не знает о Player/Ship                                │     │
│  │  - ERPR-формула (см. Battle/10_DESIGN.md §7)                      │     │
│  └──────────────────────────────────────────────────────────────────┘     │
│                          │                                                │
│                          ▼                                                │
│  ┌──────────────────────────────────────────────────────────────────┐     │
│  │              CombatClientState (singleton)                          │     │
│  │  - OnAttackStarted, OnAttackLanded, OnDamageDealt, OnEntityKilled  │     │
│  │  - Subscribe to WorldEvent (4 new events)                          │     │
│  │  - UI-нотификации (damage numbers, hit flash)                       │     │
│  └──────────────────────────────────────────────────────────────────┘     │
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────┐     │
│  │              CombatConfig (SO, server-side)                         │     │
│  │  - hitChance (base 0.95, modifier = DEX * 0.01)                   │     │
│  │  - critMultiplier (default 2.0)                                    │     │
│  │  - baseCritThreshold (default 100)                                │     │
│  │  - damageNumbers UI (Phase 2)                                    │     │
│  │  - serverTickRate (default 30 Hz)                                 │     │
│  └──────────────────────────────────────────────────────────────────┘     │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
                    ┌─────────────────────────────┐
                    │ Переиспользует:                  │
                    │ - SkillNodeConfig (T-P11, opt-in, MVP+1) │
                    │ - StatsWorld (T-P03) — STR/DEX/INT │
                    │ - EquipmentWorld (T-P09) — WeaponItemData (T-CB03) │
                    │ - WorldEventBus — 4 new events (T-RTC09) │
                    │ - DamageCalculator (T-RTC05) — ERPR-формула │
                    │ - ShipController (player-ship) [FUTURE] │
                    │ - NpcShipController (peaceful) [FUTURE] │
                    └─────────────────────────────┘
```

---

## 2. Core abstractions (4 интерфейса)

**Принцип:** интерфейсы **минимальны**, **generic**, **не знают** о конкретных сущностях. Расширение = новый класс, реализующий интерфейс. **0 изменений** в ядре.

### 2.1 `IAttacker` (что угодно, что может атаковать)

**Файл (новый):** `Assets/_Project/Scripts/Combat/Core/IAttacker.cs`
**Namespace:** `ProjectC.Combat.Core`

```csharp
public interface IAttacker {
    /// <summary>World position (для distance check).</summary>
    Vector3 GetPosition();
    
    /// <summary>STR (damage modifier). Default 10, if no stats.</summary>
    int GetStrength();
    
    /// <summary>DEX (hit chance, dodge). Default 10.</summary>
    int GetDexterity();
    
    /// <summary>INT (skill effectiveness, future). Default 10.</summary>
    int GetIntelligence();
    
    /// <summary>Список активных источников урона (меч + турели + ...).</summary>
    IReadOnlyList<IDamageSource> GetActiveDamageSources();
    
    /// <summary>Source by id (для RPC: "use source #N").</summary>
    IDamageSource GetDamageSource(ulong sourceId);
    
    /// <summary>Alive (HP > 0).</summary>
    bool IsAlive();
    
    /// <summary>Player? (для UI/toast/loss-penalties).</summary>
    bool IsPlayer();
    
    /// <summary>Cooldown check (между атаками).</summary>
    bool CanAttack(IDamageSource source, float now);
    
    /// <summary>Установить cooldown (после успешной атаки).</summary>
    void SetCooldown(IDamageSource source, float until);
}
```

**Реализации:**
- `PlayerAttacker` (MVP) — игрок.
- `NpcAttacker` (MVP) — NPC-враг.
- `ShipAttacker` (Phase 3) — корабль (player или NPC).

### 2.2 `IDamageTarget` (что угодно, что получает урон)

**Файл (новый):** `Assets/_Project/Scripts/Combat/Core/IDamageTarget.cs`

```csharp
public interface IDamageTarget {
    /// <summary>World position (для distance check).</summary>
    Vector3 GetPosition();
    
    /// <summary>Current HP.</summary>
    int GetCurrentHp();
    
    /// <summary>Max HP.</summary>
    int GetMaxHp();
    
    /// <summary>Defense (sum armorDefense, или armorHull+armorShield для ship).</summary>
    int GetArmorDefense();
    
    /// <summary>Применить урон. Server-side only.</summary>
    /// <param name="result">DamageResult с полной информацией.</param>
    /// <param name="attackerClientId">0 = NPC attacker.</param>
    void ApplyDamage(DamageResult result, ulong attackerClientId);
    
    /// <summary>Alive (HP > 0).</summary>
    bool IsAlive();
    
    /// <summary>Player? (для UI/toast/loss-penalties).</summary>
    bool IsPlayer();
    
    /// <summary>Display name (для UI).</summary>
    string GetDisplayName();
}
```

**Реализации:**
- `PlayerTarget` (MVP) — игрок.
- `NpcTarget` (MVP) — NPC-враг.
- `ShipTarget` (Phase 3) — корабль.

### 2.3 `IDamageSource` (что угодно, что наносит урон)

**Файл (новый):** `Assets/_Project/Scripts/Combat/Core/IDamageSource.cs`

```csharp
public interface IDamageSource {
    /// <summary>Stable id (для RPC, NetworkVariable).</summary>
    ulong GetSourceId();
    
    /// <summary>Damage type (Physical/Ballistic/Antigrav/Explosive/Mesium).</summary>
    DamageType GetDamageType();
    
    /// <summary>Damage dice (d4-d20, ERPR).</summary>
    DamageDice GetDamageDice();
    
    /// <summary>Базовая урон (без dice, без модификаторов).</summary>
    int GetBaseDamage();
    
    /// <summary>Crit modifier (1d100 + critMod >= 100 → crit).</summary>
    int GetCritModifier();
    
    /// <summary>Range в метрах.</summary>
    float GetRange();
    
    /// <summary>Cooldown между выстрелами (seconds).</summary>
    float GetCooldownSeconds();
    
    /// <summary>Skill multiplier (от навыков, opt-in). Default 1.0.</summary>
    /// <param name="attackerId">Для вычисления skillMult по навыкам атакующего.</param>
    float GetSkillMultiplier(ulong attackerId);
    
    /// <summary>Display name (для UI/log).</summary>
    string GetDisplayName();
}
```

**Реализации:**
- `WeaponDamageSource` (MVP) — `WeaponItemData` adapter.
- `ExplosionDamageSource` (Phase 2) — граната/мина.
- `TurretDamageSource` (Phase 3) — турель на корабле.
- `AntigravPulseDamageSource` (Phase 3) — g-волна от `AntigravTechniqueUnlock`.

### 2.4 `IRangePolicy` (стратегия distance check)

**Файл (новый):** `Assets/_Project/Scripts/Combat/Core/IRangePolicy.cs`

```csharp
public interface IRangePolicy {
    /// <summary>Target в радиусе источника?</summary>
    bool IsInRange(IAttacker attacker, IDamageTarget target, IDamageSource source);
    
    /// <summary>Distance в метрах.</summary>
    float Distance(IAttacker attacker, IDamageTarget target);
    
    /// <summary>Нужна ли line-of-sight? (Phase 2, MVP — false).</summary>
    bool RequiresLineOfSight { get; }
    
    /// <summary>Hit chance (0..1) на данной дистанции. Учитывает DEX, cover, и т.п.</summary>
    float CalculateHitChance(IAttacker attacker, IDamageTarget target, IDamageSource source);
}
```

**Реализации:**
- `MeleeRangePolicy` (MVP) — distance < `source.range`, hitChance 0.95.
- `RangedRangePolicy` (MVP) — distance < `source.range`, hitChance = base × (1 - dist/range) × DEX_modifier.
- `ShipRangePolicy` (Phase 3) — distance < 1000м, hitChance = base × (1 - dist/maxRange)² × pilot_dexterity.

### 2.5 DamageResult (POCO struct)

**Файл (новый):** `Assets/_Project/Scripts/Combat/Core/DamageResult.cs`

```csharp
public struct DamageResult {
    public int baseAttack;            // 1dN + base + STR
    public float locMult;            // 1.0 (отключён в real-time, 2.17)
    public float critMult;           // 2.0 если crit, иначе 1.0
    public float skillMult;          // от навыков (opt-in, без cap, 2.18)
    public float hitChance;          // 0..1 (до броска)
    public int preDefenseDamage;     // round(base * loc * crit * skill)
    public int effectiveDefense;     // round(armor * typeMult)
    public int finalDamage;          // max(0, preDefense - defense)
    public bool isCrit;              // (1d100 + critMod) >= 100
    public bool isHit;               // random < hitChance
    public byte hitLocation;         // 0=Limbs, 1=Torso, 2=Head (Phase 3, real-time = 1)
    public DamageType damageType;    // Physical/Ballistic/Antigrav/Explosive/Mesium
    public ulong attackerId;         // 0 = NPC
    public ulong targetId;           // 0 = NPC
    public ulong sourceId;           // id IDamageSource
    public Vector3 attackerPosition;
    public Vector3 targetPosition;
}
```

**Вердикт:** `DamageResult` — **полная информация** для UI / logs / analytics. **Не нужен refactor** при добавлении поля (просто добавляем в struct).

---

## 3. Реализации (MVP)

### 3.1 `PlayerAttacker : NetworkBehaviour, IAttacker`

**Файл (новый):** `Assets/_Project/Scripts/Combat/Implementations/PlayerAttacker.cs`
**Namespace:** `ProjectC.Combat`

```csharp
public class PlayerAttacker : NetworkBehaviour, IAttacker {
    private ulong _clientId;
    private List<IDamageSource> _activeSources = new();
    
    public void Initialize(ulong clientId) {
        _clientId = clientId;
        RebuildSources();  // читает EquipmentWorld
    }
    
    public void RebuildSources() {
        _activeSources.Clear();
        var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
        if (equip.TryGetItemId(EquipSlot.WeaponMain, out var mainId)) {
            var data = InventoryWorld.Instance.GetItemDataById(mainId);
            if (data is WeaponItemData weapon) {
                _activeSources.Add(new WeaponDamageSource(weapon, mainId));
            }
        }
        if (equip.TryGetItemId(EquipSlot.WeaponOff, out var offId)) {
            var data = InventoryWorld.Instance.GetItemDataById(offId);
            if (data is WeaponItemData weapon) {
                _activeSources.Add(new WeaponDamageSource(weapon, offId));
            }
        }
    }
    
    public Vector3 GetPosition() => transform.position;
    public int GetStrength() => StatsWorld.Instance.GetOrCreateStats(_clientId).strengthTier * 5 + 10;
    public int GetDexterity() => StatsWorld.Instance.GetOrCreateStats(_clientId).dexterityTier * 5 + 10;
    public int GetIntelligence() => StatsWorld.Instance.GetOrCreateStats(_clientId).intelligenceTier * 5 + 10;
    public IReadOnlyList<IDamageSource> GetActiveDamageSources() => _activeSources;
    public IDamageSource GetDamageSource(ulong sourceId) => _activeSources.FirstOrDefault(s => s.GetSourceId() == sourceId);
    public bool IsAlive() => /* read from PlayerTarget */;
    public bool IsPlayer() => true;
    public bool CanAttack(IDamageSource source, float now) {
        var cooldown = CooldownTracker.Instance.GetCooldown(_clientId, source.GetSourceId());
        return now >= cooldown;
    }
    public void SetCooldown(IDamageSource source, float until) {
        CooldownTracker.Instance.SetCooldown(_clientId, source.GetSourceId(), until);
    }
}
```

### 3.2 `PlayerTarget : NetworkBehaviour, IDamageTarget`

**Файл (новый):** `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs`

```csharp
public class PlayerTarget : NetworkBehaviour, IDamageTarget {
    [SerializeField] private NetworkVariable<int> _currentHp = new(20);
    [SerializeField] private NetworkVariable<int> _maxHp = new(20);
    private ulong _clientId;
    
    public int GetCurrentHp() => _currentHp.Value;
    public int GetMaxHp() => _maxHp.Value;
    public int GetArmorDefense() {
        var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
        int total = 0;
        foreach (var slot in new[] { EquipSlot.Head, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Feet, EquipSlot.Back }) {
            if (equip.TryGetItemId(slot, out var itemId)) {
                var data = InventoryWorld.Instance.GetItemDataById(itemId);
                if (data is ClothingItemData clothing) total += clothing.armorDefense;
            }
        }
        return total;
    }
    public void ApplyDamage(DamageResult result, ulong attackerClientId) {
        // Server-side only
        if (!IsServer) return;
        _currentHp.Value = Mathf.Max(0, _currentHp.Value - result.finalDamage);
        // Broadcast через CombatServer (см. 20_TECHNICAL.md §5)
        CombatServer.Instance.BroadcastDamageDealt(result);
        if (_currentHp.Value == 0) {
            CombatServer.Instance.BroadcastEntityKilled(result);
        }
    }
    public bool IsAlive() => _currentHp.Value > 0;
    public bool IsPlayer() => true;
    public string GetDisplayName() => /* from CharacterSaveData */;
    public Vector3 GetPosition() => transform.position;
}
```

### 3.3 `NpcAttacker : MonoBehaviour, IAttacker`

**Файл (новый):** `Assets/_Project/Scripts/Combat/Implementations/NpcAttacker.cs`

```csharp
public class NpcAttacker : MonoBehaviour, IAttacker {
    [SerializeField] private NpcCombatData _data;  // SO или ScriptableObject
    
    public Vector3 GetPosition() => transform.position;
    public int GetStrength() => _data.strength;
    public int GetDexterity() => _data.dexterity;
    public int GetIntelligence() => _data.intelligence;
    public IReadOnlyList<IDamageSource> GetActiveDamageSources() => _data.weapons;  // array
    public IDamageSource GetDamageSource(ulong sourceId) => _data.weapons.FirstOrDefault(w => w.GetSourceId() == sourceId);
    public bool IsAlive() => _currentHp > 0;
    public bool IsPlayer() => false;
    public bool CanAttack(IDamageSource source, float now) {
        return now >= _lastAttackTime + source.GetCooldownSeconds();
    }
    public void SetCooldown(IDamageSource source, float until) {
        _lastAttackTime = until;
    }
}
```

**`NpcCombatData` (новый, SO):** HP, weapons[], STR/DEX/INT, aggression, и т.п. Создаётся в **отдельной подсистеме** (NPC-враги).

### 3.4 `NpcTarget : NetworkBehaviour, IDamageTarget`

Аналогично `PlayerTarget`, но с `NpcCombatData`.

### 3.5 `WeaponDamageSource : IDamageSource`

**Файл (новый):** `Assets/_Project/Scripts/Combat/Implementations/WeaponDamageSource.cs`

```csharp
public class WeaponDamageSource : IDamageSource {
    private readonly WeaponItemData _weapon;
    private readonly ulong _sourceId;
    
    public WeaponDamageSource(WeaponItemData weapon, ulong sourceId) {
        _weapon = weapon;
        _sourceId = sourceId;
    }
    
    public ulong GetSourceId() => _sourceId;
    public DamageType GetDamageType() => _weapon.damageType;
    public DamageDice GetDamageDice() => _weapon.damageDice;
    public int GetBaseDamage() => _weapon.baseDamage;
    public int GetCritModifier() => _weapon.critModifier;
    public float GetRange() => _weapon.range;
    public float GetCooldownSeconds() {
        // Default: 1 сек для мелкого оружия, 2 сек для тяжёлого
        return _weapon.damageDice switch {
            DamageDice.d4 or DamageDice.d6 => 1.0f,
            DamageDice.d8 or DamageDice.d10 => 1.5f,
            DamageDice.d12 or DamageDice.d20 => 2.5f,
            _ => 1.0f,
        };
    }
    public float GetSkillMultiplier(ulong attackerId) {
        // Навыки (opt-in) — после T-CB01..T-CB09
        // Сейчас: 1.0
        return 1.0f;
    }
    public string GetDisplayName() => _weapon.itemName;
}
```

**До T-CB03** (WeaponItemData): `WeaponDamageSource` использует **дефолтные значения** (`damageDice = d6`, `baseDamage = 1`, `critModifier = 0`). Движок работает.

### 3.6 `MeleeRangePolicy : IRangePolicy`

```csharp
public class MeleeRangePolicy : IRangePolicy {
    public bool IsInRange(IAttacker a, IDamageTarget t, IDamageSource s) {
        return Distance(a, t) <= s.GetRange() + 0.5f;  // допуск 0.5м
    }
    public float Distance(IAttacker a, IDamageTarget t) => Vector3.Distance(a.GetPosition(), t.GetPosition());
    public bool RequiresLineOfSight => false;  // MVP
    public float CalculateHitChance(IAttacker a, IDamageTarget t, IDamageSource s) {
        float dist = Distance(a, t);
        float distMod = Mathf.Clamp01(1f - (dist - 1.5f) / 2f);  // 0..1 на дистанции 1.5-3.5м
        float dexMod = 0.85f + (a.GetDexterity() - 10) * 0.015f;   // DEX 10 → 0.85, DEX 20 → 0.925 (per 2.1)
        return Mathf.Clamp01(0.85f * distMod * dexMod);
    }
}
```

### 3.7 `RangedRangePolicy : IRangePolicy`

```csharp
public class RangedRangePolicy : IRangePolicy {
    public bool IsInRange(IAttacker a, IDamageTarget t, IDamageSource s) {
        return Distance(a, t) <= s.GetRange();
    }
    public float Distance(IAttacker a, IDamageTarget t) => Vector3.Distance(a.GetPosition(), t.GetPosition());
    public bool RequiresLineOfSight => false;  // Phase 2
    public float CalculateHitChance(IAttacker a, IDamageTarget t, IDamageSource s) {
        float dist = Distance(a, t);
        float maxRange = s.GetRange();
        float distMod = Mathf.Clamp01(1f - dist / maxRange);
        float dexMod = 0.85f + (a.GetDexterity() - 10) * 0.015f;   // DEX 10 → 0.85, DEX 20 → 0.925 (per 2.1)
        return Mathf.Clamp01(0.75f * distMod * dexMod);  // ranged harder than melee
    }
}
```

---

## 4. DamageCalculator (static class)

**Файл (новый):** `Assets/_Project/Scripts/Combat/DamageCalculator.cs`
**Namespace:** `ProjectC.Combat`

```csharp
public static class DamageCalculator {
    public static DamageResult Calculate(
        IAttacker attacker,
        IDamageTarget defender,
        IDamageSource source,
        SkillNodeConfig skill = null  // T-CB01..T-CB09, opt-in
    ) {
        // === ERPR formula (Battle/10_DESIGN.md §7) ===

        // 1. Base attack: roll dN + base + STR
        int roll = source.GetDamageDice().Roll();  // UnityEngine.Random.Range(1, N+1)
        int baseAttack = roll + source.GetBaseDamage() + attacker.GetStrength();

        // 2. Hit chance
        float hitChance = /* range policy.CalculateHitChance(attacker, defender, source) */ 0.95f;
        bool isHit = UnityEngine.Random.value < hitChance;
        if (!isHit) {
            return new DamageResult {
                isHit = false,
                hitChance = hitChance,
                damageType = source.GetDamageType(),
                attackerId = GetAttackerId(attacker),
                targetId = GetTargetId(defender),
                sourceId = source.GetSourceId(),
                attackerPosition = attacker.GetPosition(),
                targetPosition = defender.GetPosition(),
            };
        }

        // 3. Hit location — ОТКЛЮЧЕН в real-time (2.17)
        float locMult = 1.0f;
        byte hitLocation = 1;  // Torso (default)

        // 4. Crit (1d100 + critMod >= 100 → ×2)
        int critRoll = UnityEngine.Random.Range(1, 101);
        bool isCrit = (critRoll + source.GetCritModifier()) >= 100;
        float critMult = isCrit ? 2.0f : 1.0f;

        // 5. Skill multiplier (от навыков, opt-in, БЕЗ CAP per 2.18)
        float skillMult = source.GetSkillMultiplier(GetAttackerId(attacker));

        // 6. Pre-defense damage
        int preDefense = Mathf.RoundToInt(baseAttack * locMult * critMult * skillMult);

        // 7. Defense (armor × typeMultiplier)
        int totalArmor = defender.GetArmorDefense();
        float armorMult = source.GetDamageType() switch {
            DamageType.Physical or DamageType.Ballistic => 1.0f,
            DamageType.Antigrav => 0.5f,
            DamageType.Explosive => 0.7f,
            DamageType.Mesium => 0.0f,
            _ => 1.0f,
        };
        int effectiveDefense = Mathf.RoundToInt(totalArmor * armorMult);

        // 8. Final
        int final = Mathf.Max(0, preDefense - effectiveDefense);

        return new DamageResult {
            baseAttack = baseAttack,
            locMult = locMult,
            critMult = critMult,
            skillMult = skillMult,
            hitChance = hitChance,
            preDefenseDamage = preDefense,
            effectiveDefense = effectiveDefense,
            finalDamage = final,
            isCrit = isCrit,
            isHit = true,
            hitLocation = hitLocation,
            damageType = source.GetDamageType(),
            attackerId = GetAttackerId(attacker),
            targetId = GetTargetId(defender),
            sourceId = source.GetSourceId(),
            attackerPosition = attacker.GetPosition(),
            targetPosition = defender.GetPosition(),
        };
    }
    
    private static ulong GetAttackerId(IAttacker a) {
        return a is PlayerAttacker pa ? pa.GetClientId() : 0;
    }
    private static ulong GetTargetId(IDamageTarget t) {
        return t is PlayerTarget pt ? pt.GetClientId() : 0;
    }
}

public enum DamageType : byte {
    Physical = 0,
    Ballistic = 1,
    Antigrav = 2,
    Explosive = 3,
    Mesium = 4,
}

public enum DamageDice : byte {
    d4 = 4, d6 = 6, d8 = 8, d10 = 10, d12 = 12, d20 = 20,
}

public static class DamageDiceExtensions {
    public static int Roll(this DamageDice dice) => UnityEngine.Random.Range(1, (int)dice + 1);
    public static float Average(this DamageDice dice) => ((int)dice + 1) / 2f;
}
```

**Вердикт:** **одна формула** для пешего и корабельного. `DamageCalculator` не знает о Player/Ship/Npc.

---

## 5. CombatServer (NetworkBehaviour)

**Файл (новый):** `Assets/_Project/Scripts/Combat/Network/CombatServer.cs`
**Namespace:** `ProjectC.Combat`
**Сцена:** `BootstrapScene.unity`, рядом с `[StatsServer]`, `[SkillsServer]`, `[EquipmentServer]`.

```csharp
public class CombatServer : NetworkBehaviour {
    public static CombatServer Instance { get; private set; }
    
    private Dictionary<ulong, IAttacker> _attackers = new();
    private Dictionary<ulong, IDamageTarget> _targets = new();
    private Dictionary<ulong, List<IDamageSource>> _sources = new();
    
    public override void OnNetworkSpawn() {
        if (!IsServer) return;
        Instance = this;
    }
    
    public override void OnNetworkDespawn() {
        if (Instance == this) Instance = null;
    }
    
    // === Registration ===
    public void RegisterAttacker(ulong id, IAttacker attacker) {
        _attackers[id] = attacker;
    }
    public void RegisterTarget(ulong id, IDamageTarget target) {
        _targets[id] = target;
    }
    public void RegisterSource(ulong attackerId, IDamageSource source) {
        if (!_sources.ContainsKey(attackerId)) _sources[attackerId] = new();
        _sources[attackerId].Add(source);
    }
    public void UnregisterAttacker(ulong id) { _attackers.Remove(id); _sources.Remove(id); }
    public void UnregisterTarget(ulong id) { _targets.Remove(id); }
    
    // === Client → Server RPCs ===
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestAttackRpc(ulong targetId, ulong sourceId, RpcParams rpcParams = default) {
        ulong attackerId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(attackerId)) return;
        ResolveAttack(attackerId, targetId, sourceId);
    }
    
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestSkillRpc(ulong targetId, string skillId, RpcParams rpcParams = default) {
        ulong attackerId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(attackerId)) return;
        // Phase 2: skill-based attack
    }
    
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestDefendRpc(RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;
        // Установить стойку (defense bonus на ход)
    }
    
    // === Server-side damage flow ===
    public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId) {
        if (!_attackers.TryGetValue(attackerId, out var attacker)) return;
        if (!_targets.TryGetValue(targetId, out var target)) return;
        if (!target.IsAlive()) return;
        
        var source = attacker.GetDamageSource(sourceId);
        if (source == null) return;
        
        // Cooldown
        float now = Time.unscaledTime;
        if (!attacker.CanAttack(source, now)) return;
        
        // Range check
        var rangePolicy = source is WeaponDamageSource wds && wds.IsMelee()
            ? (IRangePolicy)new MeleeRangePolicy()
            : new RangedRangePolicy();
        if (!rangePolicy.IsInRange(attacker, target, source)) {
            SendOutOfRangeTargetRpc(attackerId);
            return;
        }
        
        // Calculate damage
        var result = DamageCalculator.Calculate(attacker, target, source);
        
        // Cooldown set
        attacker.SetCooldown(source, now + source.GetCooldownSeconds());
        
        // Apply
        if (result.isHit) {
            target.ApplyDamage(result, attackerId);
        }
        
        // Broadcast
        BroadcastAttackLanded(result);
        WorldEventBus.Publish(new AttackLandedEvent { result = result });
        if (result.isHit) {
            WorldEventBus.Publish(new DamageDealtEvent { result = result });
        }
        if (!target.IsAlive()) {
            WorldEventBus.Publish(new EntityKilledEvent { result = result });
        }
    }
    
    // === Server → Client TargetRPCs (multicast) ===
    public void BroadcastAttackLanded(DamageResult result) {
        // Multicast to all in range (или all participants in scene)
        var rpcParams = new RpcParams {
            Send = new RpcSendParams { Target = RpcTarget.Everyone }
        };
        AttackLandedTargetRpc(result, rpcParams);
    }
    
    [Rpc(SendTo.SpecifiedInParams)]
    public void AttackLandedTargetRpc(DamageResult result, RpcParams rpcParams) {
        CombatClientState.Instance.HandleAttackLanded(result);
    }
    
    [Rpc(SendTo.SpecifiedInParams)]
    public void OutOfRangeTargetRpc(ulong clientId, RpcParams rpcParams) {
        CombatClientState.Instance.HandleOutOfRange();
    }
    
    // === Rate limit ===
    private readonly Dictionary<ulong, float> _nextAllowedTime = new();
    private bool RateLimit(ulong clientId) {
        float now = Time.unscaledTime;
        if (_nextAllowedTime.TryGetValue(clientId, out var next) && now < next) {
            return false;
        }
        _nextAllowedTime[clientId] = now + 0.1f;  // 10 ops/sec
        return true;
    }
}
```

**Вердикт:** `CombatServer` **generic** — не знает о Player/Ship/Npc. Работает с `IAttacker/IDamageTarget/IDamageSource`.

---

## 6. CombatClientState (singleton)

**Файл (новый):** `Assets/_Project/Scripts/Combat/Client/CombatClientState.cs`
**Namespace:** `ProjectC.Combat`

```csharp
public class CombatClientState : MonoBehaviour {
    public static CombatClientState Instance { get; private set; }
    
    public event Action<DamageResult> OnAttackLanded;
    public event Action<DamageResult> OnDamageDealt;
    public event Action<DamageResult> OnEntityKilled;
    public event Action OnOutOfRange;
    
    void OnEnable() {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public void HandleAttackLanded(DamageResult result) {
        OnAttackLanded?.Invoke(result);
        // UI: trigger hit animation, damage number, hit flash
    }
    
    public void HandleDamageDealt(DamageResult result) {
        OnDamageDealt?.Invoke(result);
        // UI: floating damage number
    }
    
    public void HandleEntityKilled(DamageResult result) {
        OnEntityKilled?.Invoke(result);
        // UI: death animation, loot popup (Phase 2)
    }
    
    public void HandleOutOfRange() {
        OnOutOfRange?.Invoke();
        // UI: "Out of range" toast
    }
}
```

---

## 7. CombatConfig (SO, server-side)

**Файл (новый):** `Assets/_Project/Scripts/Combat/Config/CombatConfig.cs`

```csharp
[CreateAssetMenu(menuName = "Project C/Combat/Combat Config")]
public class CombatConfig : ScriptableObject {
    [Header("Hit")]
    [Range(0f, 1f)] public float baseMeleeHitChance = 0.95f;
    [Range(0f, 1f)] public float baseRangedHitChance = 0.75f;
    [Range(0f, 1f)] public float dexHitMultiplier = 0.01f;  // DEX 10 = +10% hit chance
    
    [Header("Crit")]
    [Range(1, 200)] public int baseCritThreshold = 100;
    [Range(1f, 5f)] public float critMultiplier = 2.0f;
    
    [Header("Defense")]
    [Range(0f, 1f)] public float antigravArmorMult = 0.5f;
    [Range(0f, 1f)] public float explosiveArmorMult = 0.7f;
    [Range(0f, 1f)] public float mesiumArmorMult = 0.0f;
    
    [Header("Cooldown")]
    [Range(0.1f, 5f)] public float baseMeleeCooldown = 1.0f;
    [Range(0.5f, 5f)] public float baseRangedCooldown = 1.5f;
    
    [Header("Network")]
    [Range(10, 60)] public int serverTickRate = 30;
    
    [Header("UI (Phase 2)")]
    public bool showDamageNumbers = true;
    public bool showHitFlash = true;
    public float damageNumberDuration = 1.5f;
}
```

**Path:** `Assets/_Project/Resources/Combat/CombatConfig_Default.asset`.

---

## 8. WorldEvent — 4 новых event-класса

**Файл (расширение):** `Assets/_Project/Core/WorldEvent.cs`

```csharp
[Serializable]
public class AttackLandedEvent : WorldEvent {
    public DamageResult result;
}

[Serializable]
public class DamageDealtEvent : WorldEvent {
    public DamageResult result;
}

[Serializable]
public class EntityKilledEvent : WorldEvent {
    public DamageResult result;
}

[Serializable]
public class AttackStartedEvent : WorldEvent {
    public ulong attackerId;
    public ulong targetId;
    public ulong sourceId;
}
```

**Подписки:**
- `QuestServer` — отслеживает kills для квестов.
- `StatsServer` — начисляет XP за combat (T-P05).
- `NpcAttacker` AI — реагирует на атаки (Phase 2, враждебные NPC).
- `CombatClientState` (через TargetRPC) — UI-нотификации.

---

## 9. Lifecycle: как атакующий регистрируется

```csharp
// При старте (для игрока):
public class NetworkPlayer : NetworkBehaviour {
    private PlayerAttacker _attacker;
    private PlayerTarget _target;
    
    public override void OnNetworkSpawn() {
        _attacker = gameObject.AddComponent<PlayerAttacker>();
        _target = gameObject.AddComponent<PlayerTarget>();
        _attacker.Initialize(OwnerClientId);
        _target.Initialize(OwnerClientId);
        
        if (IsServer) {
            CombatServer.Instance.RegisterAttacker(OwnerClientId, _attacker);
            CombatServer.Instance.RegisterTarget(OwnerClientId, _target);
        }
    }
    
    public override void OnNetworkDespawn() {
        if (IsServer) {
            CombatServer.Instance.UnregisterAttacker(OwnerClientId);
            CombatServer.Instance.UnregisterTarget(OwnerClientId);
        }
    }
}
```

**Для NPC** (отдельная подсистема, не наш scope): NPC-враги регистрируются при спавне.

---

## 10. Hooks для будущего ship combat

### 10.1 `ShipAttacker : NetworkBehaviour, IAttacker` (FUTURE, Phase 3)

```csharp
public class ShipAttacker : NetworkBehaviour, IAttacker {
    private ShipController _ship;  // существующий
    private List<Turret> _turrets;  // новые модули
    
    public Vector3 GetPosition() => _ship.transform.position;
    public int GetStrength() => /* ship armor */ 50;
    public int GetDexterity() => /* pilot dexterity */ 10;
    public int GetIntelligence() => /* pilot intelligence */ 10;
    public IReadOnlyList<IDamageSource> GetActiveDamageSources() {
        return _turrets.Where(t => t.IsActive).Select(t => (IDamageSource)t).ToList();
    }
    public IDamageSource GetDamageSource(ulong sourceId) =>
        _turrets.FirstOrDefault(t => t.GetSourceId() == sourceId);
    public bool IsAlive() => _ship.GetCurrentHp() > 0;
    public bool IsPlayer() => _ship.HasPlayerPilot();
    // ... CanAttack, SetCooldown — на турелях
}
```

**`Turret : MonoBehaviour, IDamageSource` (FUTURE):**
```csharp
public class Turret : MonoBehaviour, IDamageSource {
    [SerializeField] private TurretConfig _config;
    private float _lastFireTime = 0f;
    
    public ulong GetSourceId() => (ulong)GetInstanceID();
    public DamageType GetDamageType() => _config.damageType;
    public DamageDice GetDamageDice() => _config.damageDice;
    public int GetBaseDamage() => _config.baseDamage;
    public int GetCritModifier() => _config.critModifier;
    public float GetRange() => _config.range;  // 100-1000м для турели
    public float GetCooldownSeconds() => _config.cooldownSeconds;
    public float GetSkillMultiplier(ulong attackerId) => /* pilot skill */ 1.0f;
    public string GetDisplayName() => _config.turretName;
}
```

**Вердикт:** **0 изменений** в `CombatServer.cs`, `DamageCalculator.cs`, `IAttacker/IDamageTarget/IDamageSource` interfaces. Просто **новые классы**, реализующие интерфейсы. `CombatServer` не знает, что это корабль.

### 10.2 `ShipTarget : NetworkBehaviour, IDamageTarget` (FUTURE)

```csharp
public class ShipTarget : NetworkBehaviour, IDamageTarget {
    private ShipController _ship;  // существующий
    
    public int GetCurrentHp() => _ship.GetCurrentHp();
    public int GetMaxHp() => _ship.GetMaxHp();
    public int GetArmorDefense() {
        // armorHull + armorShield (Phase 3)
        return _ship.GetArmorHull() + _ship.GetArmorShield();
    }
    public void ApplyDamage(DamageResult result, ulong attackerClientId) {
        _ship.ApplyDamage(result.finalDamage, result.damageType);
    }
    public bool IsAlive() => _ship.GetCurrentHp() > 0;
    public bool IsPlayer() => _ship.HasPlayerPilot();
    public string GetDisplayName() => _ship.GetShipName();
    public Vector3 GetPosition() => _ship.transform.position;
}
```

**Вердикт:** `CombatServer.ResolveAttack(playerShipId, enemyShipId, turretId)` — **тот же код**, что для пешего. Damage-формула та же. Defense = `armorHull + armorShield` (вместо `armorDefense`).

### 10.3 ShipRangePolicy : IRangePolicy (FUTURE)

```csharp
public class ShipRangePolicy : IRangePolicy {
    public bool IsInRange(IAttacker a, IDamageTarget t, IDamageSource s) {
        return Distance(a, t) <= s.GetRange();  // 100-1000м
    }
    public float Distance(IAttacker a, IDamageTarget t) => Vector3.Distance(a.GetPosition(), t.GetPosition());
    public bool RequiresLineOfSight => true;  // Phase 3: турели нужна прямая видимость
    public float CalculateHitChance(IAttacker a, IDamageTarget t, IDamageSource s) {
        // Зависит от маневренности корабля, дистанции, угла, и т.д.
        // Phase 3
        return 0.5f;
    }
}
```

### 10.4 Пример: ship-vs-ship combat (FUTURE)

```csharp
// Player's ship attacks NPC ship
[Rpc(SendTo.Server)]
public void RequestTurretFireRpc(ulong enemyShipId, ulong turretId) {
    // Точно тот же flow, что и для пешего:
    CombatServer.Instance.ResolveAttack(playerShipId, enemyShipId, turretId);
}

// CombatServer (БЕЗ ИЗМЕНЕНИЙ):
public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId) {
    if (!_attackers.TryGetValue(attackerId, out var attacker)) return;  // ShipAttacker implements IAttacker ✓
    if (!_targets.TryGetValue(targetId, out var target)) return;  // ShipTarget implements IDamageTarget ✓
    var source = attacker.GetDamageSource(sourceId);  // Turret implements IDamageSource ✓
    var result = DamageCalculator.Calculate(attacker, target, source);  // ERPR-формула ✓
    target.ApplyDamage(result, attackerId);  // ShipTarget.ApplyDamage ✓
    // ... broadcast ...
}
```

**Вердикт:** **0 изменений** в ядре движка. Только новые классы-реализации. **Anti-restrictive design работает**.

---

## 11. End-to-end сценарий (MVP, пеший)

**Сценарий:** Игрок с мечом атакует NPC-врага.

```
1. Игрок нажимает ЛКМ на NPC → PlayerInput.OnAttack()
2. PlayerAttacker.OnAttack(targetId, sourceId) → вычисляет target через raycast/closest
3. CombatClientState.RequestAttackRpc(targetId, sourceId) → отправляет на сервер
4. Server: CombatServer.RequestAttackRpc:
     - Валидация: ownership, rate limit
     - CombatServer.ResolveAttack(playerId, npcId, swordId):
       - playerAttacker = _attackers[playerId] (PlayerAttacker)
       - npcTarget = _targets[npcId] (NpcTarget)
       - sword = playerAttacker.GetDamageSource(swordId) (WeaponDamageSource)
       - meleeRangePolicy.IsInRange(player, npc, sword) → true/false
       - hitChance = meleeRangePolicy.CalculateHitChance(...) → 0.92
       - result = DamageCalculator.Calculate(player, npc, sword):
         - roll d6 = 4
         - baseAttack = 4 + 3 (base) + 10 (STR) = 17
         - isHit = random < 0.92 → true
         - locMult = 1.0
         - critRoll 1d100 = 87, 87 + 0 (critMod) < 100 → no crit
         - critMult = 1.0
         - skillMult = 1.0
         - preDefense = 17
         - defense = npc.armorDefense (e.g., 3) × 1.0 (Physical) = 3
         - final = 17 - 3 = 14
       - target.ApplyDamage(result, playerId) → npc.currentHp -= 14
       - broadcast AttackLandedTargetRpc(result) → все клиенты
       - WorldEventBus.Publish(DamageDealtEvent)
5. Client: CombatClientState.HandleAttackLanded(result)
     - UI: floating damage number "14" на NPC
     - UI: hit flash (red)
     - Audio: hit sound
6. Если result.finalDamage == npc.currentHp (kill) → broadcast EntityKilled
7. NPC-AI (отдельная подсистема): реакция на урон, ответная атака
```

**Трудозатраты:** ~30-40 ч (T-RTC01..T-RTC10).

---

## 12. Что НЕ делаем в этой сессии

- ❌ Не пишем код.
- ❌ Не модифицируем `docs/gdd/`.
- ❌ Не пишем .meta / .asmdef.
- ❌ Не делаем ship combat (Phase 3, отложено).
- ❌ Не делаем NPC-AI для open world (отдельная подсистема).
- ❌ Не делаем UI damage numbers (T-RTC10, Phase 2).
- ❌ Не делаем PvP duel flow (T-RTC11..T-RTC15, Phase 2).
- ❌ Не делаем turn-based (`turn-based-battles/`, parking).
- ❌ Не трогаем существующий код (`NetworkPlayer`, `ShipController`).
- ❌ Не делаем line-of-sight (Phase 2).
- ❌ Не делаем anti-cheat beyond server-authoritative (Phase 3).
