# Scenarios — пеший MVP, ship-extensibility примеры

> **Дата:** 2026-06-25 (v0.3)
> **Базируется на:** `10_DESIGN.md` (архитектура), `20_TECHNICAL.md` (NGO, RPC), `02_LORE.md` (лор-база)
> **5 сценариев:** §1 Пеший MVP (базовый), §2 Пеший + навыки (T-CB01..T-CB09, MVP+1), §3 PvP-дуэль 1v1 (Phase 2), §4 Ship combat (Phase 3, anti-restrictive), §5 Гибрид (теоретический).

---

## 1. Пеший MVP (базовый combat)

### 1.1 Концепция

**Самый простой use case.** Игрок с мечом (default `WeaponItemData` или базовый placeholder) атакует NPC-врага (placeholder-NPC с `NpcCombatData`). Damage считается по ERPR-формуле. UI показывает damage number.

**БЕЗ навыков** (T-CB01..T-CB09 ещё не сделаны) — движок работает на default `GetSkillMultiplier = 1.0`.

### 1.2 Setup

- **Игрок** заходит в `WorldScene_0_0`, видит NPC-врага (placeholder GameObject с `NpcAttacker` + `NpcTarget` + `NpcCombatData`).
- NPC имеет: `maxHp = 30, currentHp = 30, weapon = WoodenSword (d6, base=2)`.
- Игрок имеет: `WeaponItemData` (после T-CB03) или `DefaultDamageSource` (до T-CB03, fallback).
- Игрок экипировал меч (через `EquipmentServer.TryEquip` — но пока без proficiency check, T-CB06).

### 1.3 Sequence: «Игрок атакует NPC мечом»

```
1. Игрок нажимает ЛКМ на NPC.
2. PlayerInput.OnAttack() → raycast → определяет targetId = npcId.
3. PlayerAttacker.OnAttack(targetId, swordSourceId):
     - Cooldown check (local): 1.0 сек (меч d6) — OK.
     - CombatServer.Instance.RequestAttackRpc(npcId, swordSourceId).
4. Server: CombatServer.RequestAttackRpc:
     - Валидация: ownership, rate limit.
     - ResolveAttack(playerId, npcId, swordSourceId):
       - attacker = _attackers[playerId] (PlayerAttacker)
       - target = _targets[npcId] (NpcTarget)
       - source = attacker.GetDamageSource(swordSourceId) (WeaponDamageSource)
       - Cooldown check (server): now=1234.5, _cooldowns[(playerId, swordSourceId)] = 1233.5 → OK (1 сек прошло)
       - rangePolicy = MeleeRangePolicy (sword → melee)
       - IsInRange: distance(5.0, 5.0, 0.5) → 0.5m ≤ sword.range (2m) + 0.5 → OK
       - hitChance = 0.85 * distMod(1.0) * dexMod(10/20=0.5) = 0.425 → слишком мало! ПЕРЕСМОТР.
         // PITFALL: default DEX 10 → dexMod 0.5. Hit chance 0.85 * 0.5 = 0.425.
         // Нужно: dexMod = (DEX / 10) * 0.5 + 0.5 = 1.0 на DEX 10, 1.25 на DEX 20.
         // Изменяем: dexMod = 0.5 + (DEX - 10) * 0.025 (1.0 на DEX 10, 1.5 на DEX 30).
       - isHit = random < hitChance (например, 0.92 на DEX 10 с правильным dexMod).
       - DamageCalculator.Calculate(player, npc, sword):
         - roll d6 = 4
         - baseAttack = 4 + 2 (base) + 10 (STR, default tier 0) = 16
         - locMult = 1.0 (отключён)
         - critRoll 1d100 = 23, 23 + 0 (critMod) < 100 → no crit
         - critMult = 1.0
         - skillMult = 1.0 (навыков нет)
         - preDefense = 16
         - defense = npc.armorDefense (0, placeholder) × 1.0 (Physical) = 0
         - final = 16 - 0 = 16
       - npc.ApplyDamage(result, playerId) → npc.currentHp = 30 - 16 = 14
       - BroadcastAttackLanded(DamageResultDto(result)) → все клиенты.
       - WorldEventBus.Publish(AttackLandedEvent, DamageDealtEvent).
       - SetCooldown: (playerId, swordSourceId) = 1235.5
5. Client: CombatClientState.HandleAttackLanded(DamageResultDto)
     - UI: floating damage number "16" (Phase 2, T-RTC10).
     - UI: hit flash on NPC (red, T-RTC10).
     - Audio: hit sound.
6. NPC-AI (отдельная подсистема, в MVP не существует): нет реакции.
7. Игрок нажимает ЛКМ ещё раз → cooldown check (1 сек) → wait → ready → attack.
8. После 2-3 ударов: NPC.currentHp = 0 → BroadcastEntityKilled.
9. NPC-AI: respawn (Phase 2) или disappear (MVP, designer config).
```

### 1.4 Edge cases

| Случай | Решение |
|---|---|
| Игрок нажимает ЛКМ, target уже мёртв | `target.IsAlive()` false → `AttackErrorCode.AlreadyDead` |
| Игрок нажимает ЛКМ, target за пределами range | `rangePolicy.IsInRange` false → `OutOfRange` |
| Игрок нажимает ЛКМ, не выбрал source (нет weapon) | `GetDamageSource` returns null → `InvalidSource` |
| Игрок спамит ЛКМ (cooldown bypass) | `attacker.CanAttack` false → `OnCooldown` |
| NPC убит, игрок продолжает атаковать | `target.IsAlive()` false → `AlreadyDead` |
| Player disconnect в середине combat | NPC-AI решает (Phase 2), MVP — NPC стоит на месте |
| Server crash | respawn при reconnect, NPC reset |

### 1.5 Pitfall: HitChance слишком низкое на default DEX

**Проблема:** `dexMod = 0.85 + (DEX - 10) * 0.015`. На DEX 10 → 0.85. `hitChance = 0.85 * 0.85 = 0.72`. **Приемлемо** (промах ~28% времени).

**Решение (подтверждено 2.1):** `dexMod = 0.85f + (DEX - 10) * 0.015f`. Базовая hitChance = `0.85 × dexMod`. На DEX 10 → 0.81 × 0.85 = 0.72. На DEX 20 → 0.925 × 0.85 = 0.79. На DEX 30 → 1.0 × 0.85 = 0.85. **Игрок попадает 72-85% времени** для базового DEX 10-30.

### 1.6 Пример: damage log

```
[T-RTC] Player 0 attacks NPC 5 with Weapon_Sword (d6, base=2)
[T-RTC]   roll d6 = 4, base = 2, STR = 10 → baseAttack = 16
[T-RTC]   locMult = 1.0, critRoll 1d100 = 23 < 100 → no crit, critMult = 1.0
[T-RTC]   skillMult = 1.0 (no skills), preDefense = 16
[T-RTC]   defense = 0 (no armor) × 1.0 (Physical) = 0
[T-RTC]   final = 16 - 0 = 16
[T-RTC]   dexMod = 0.85 + (10 - 10) × 0.015 = 0.85
[T-RTC]   hitChance = 0.85 × 0.85 = 0.72 → isHit = random < 0.72 → true
[T-RTC]   NPC 5 HP: 30 → 14
[T-RTC]   BroadcastAttackLanded(DamageResultDto) → all clients
[T-RTC]   WorldEventBus.Publish(AttackLandedEvent, DamageDealtEvent)
```

### 1.7 Что нужно реализовать (T-RTC01..T-RTC10)

| # | Тикет | Что | Время |
|---|---|---|---|
| T-RTC01 | Core interfaces | `IAttacker`, `IDamageTarget`, `IDamageSource`, `IRangePolicy`, `DamageResult`, enums | ~2-3 ч |
| T-RTC02 | PlayerAttacker + PlayerTarget | Player реализации | ~3-4 ч |
| T-RTC03 | NpcAttacker + NpcTarget + NpcCombatData | NPC реализации + placeholder-NPC | ~3-4 ч |
| T-RTC04 | WeaponDamageSource + MeleeRangePolicy + RangedRangePolicy | Default оружие + range policies | ~2-3 ч |
| T-RTC05 | DamageCalculator (static) | ERPR-формула | ~2-3 ч |
| T-RTC06 | CombatServer (NetworkBehaviour) | RPC hub, registries | ~4-5 ч |
| T-RTC07 | CombatClientState (singleton) | UI event-bus | ~2-3 ч |
| T-RTC08 | NGO RPC + DTO | `RequestAttackRpc`, `AttackLandedTargetRpc`, `DamageResultDto` | ~3-4 ч |
| T-RTC09 | CombatConfig (SO) + 4 WorldEvent | `AttackStartedEvent`, `AttackLandedEvent`, `DamageDealtEvent`, `EntityKilledEvent` | ~2-3 ч |
| T-RTC10 | UI: damage numbers + hit flash (Phase 2, опц.) | UI hook (но НЕ обязательно для MVP-1) | (Phase 2) |
| **ИТОГО** | — | — | **~23-32 ч** (3-4 сессии) |

---

## 2. Пеший + навыки (T-CB01..T-CB09, MVP+1)

### 2.1 Концепция

**После T-CB01..T-CB09** навыки подключаются к движку через hooks в `IDamageSource.GetSkillMultiplier` и `IDamageTarget.GetDefenseModifier`. Игрок учит `melee_basic_sword` (XP=0) → equip sword → combat эффективнее.

**Без изменений** в `CombatServer.cs`, `DamageCalculator.cs`, интерфейсах. Только новые хуки в `WeaponDamageSource` и `PlayerTarget`.

### 2.2 Пример: игрок учил `melee_basic_sword` + `melee_great_sword` + `melee_heavy_swing`

| Навык | Эффект | multiplier |
|---|---|---|
| `melee_basic_sword` | `StatMod(STR+1)`, `WeaponProficiencyUnlock("sword")` | ×1.0 (StatMod +STR не mult) |
| `melee_great_sword` | `StatMod(STR+3, ×1.15)`, `WeaponProficiencyUnlock("great_sword")` | **×1.15** (mult) |
| `melee_heavy_swing` (T-P11) | `StatMod(STR+5, ×1.2)` | **×1.2** (mult) |

**При атаке двуручным мечом (`Weapon_GreatSword_Antigrav`):**
- `GetSkillMultiplier(playerId)` → `melee_basic_sword × melee_great_sword × HeavySwing = 1.0 × 1.15 × 1.2 = 1.38`. **Без cap** (per 2.18).
- `GetStrength()` = `StatsConfig` STR + (StatMod +1 + +3 + +5 = +9) = 10 + 9 = 19.
- Damage = `roll d10 + base + STR × locMult × critMult × skillMult - defense = 7 + 4 + 19 × 1.0 × 1.0 × 1.38 - 0 = 41.4 → 41`.

**Без навыков:** damage = `7 + 4 + 10 = 21` (vs 41). **Навыки удваивают damage.**

### 2.3 Пример: игрок учил `defense_heavy_armor` + надет SteelChestplate

| Источник | Значение |
|---|---|
| `armorDefense` SteelChestplate | 8 |
| `armorDefense` WorkerHelmet | 2 |
| `armorDefense` TravelerBoots | 1 |
| **Итого base** | **11** |
| `defense_master_defender` | `StatMod(STR+3, DEX+2)`, **defense ×1.2** (Phase 2) |
| **Итого с навыком** | **11 × 1.2 = 13.2 → 13** |

**При атаке Antigrav-клинком:** `effectiveDefense = 13 × 0.5 = 6.5 → 7` (Antigrav пробивает половину).

### 2.4 Пример: Antigrav-клинок (antigrav_blade) — special ERPR

| Поле | Значение |
|---|---|
| `damageDice` | d8 |
| `baseDamage` | 3 |
| `critModifier` | +10 (g-волна «притягивает» к уязвимости) |
| `damageType` | Antigrav |
| Дизайнер | +5% базовая hit chance (g-волна помогает прицелиться) |

**Damage с навыками:**
- `roll d8 = 6, base = 3, STR = 19 (с навыками), critMod = +10` → `baseAttack = 28`.
- `critRoll 1d100 = 95 + 10 = 105 >= 100` → **CRIT!** → `critMult = 2.0`.
- `preDefense = 28 × 1.0 × 2.0 × 1.38 = 77.28 → 77`.
- `defense = 13 × 0.5 (Antigrav vs armor) = 6`.
- `final = 77 - 6 = 71`.

**Без навыков, без crit:** `damage = 6 + 3 + 10 - 0 = 19` (без armor, для placeholder-NPC).

**Вердикт:** с навыками + crit → **×4 damage** относительно базового. Дизайнер балансирует через NPC HP, количество, и т.п.

### 2.5 Что нужно (T-CB01..T-CB09)

- T-CB01..T-CB07 (навыки + effects) — все выше.
- T-CB08 (35 .asset с damage-параметрами) — все выше.
- T-CB09 (UI фильтр по discipline) — Phase 2.

**Без изменений** в `CombatServer.cs` или `DamageCalculator.cs` — только хуки в `WeaponDamageSource.GetSkillMultiplier`.

---

## 3. PvP-дуэль 1v1 (Phase 2, T-RTC11..T-RTC15)

### 3.1 Концепция

Два игрока вызывают друг друга на дуэль. Бой 1v1, server-authoritative, consent-based. Победитель получает credits + honor. Проигравший — XP loss или permadeath (consent-based).

**Зачем PvP-aware с самого начала (2.10):** архитектура CombatServer **уже поддерживает** `IAttacker/IDamageTarget` для игроков. PvP = просто `playerId` атакует `playerId`. **0 изменений** в ядре движка.

### 3.2 Sequence: «A вызывает B на дуэль»

```
1. Player A в социальном хабе нажимает «Вызвать на дуэль» → вводит имя B.
2. A нажимает «Отправить» → CombatServer.Instance.RequestDuelRpc(B.clientId).
3. Server: CombatServer.RequestDuelRpc:
     - Валидация: B online, B не в бою, B не отказал в последние 5 мин.
     - SendDuelInviteTargetRpc(B, duelId).
4. Client B: CombatClientState.HandleDuelInvite(duelId, A.name).
     - UI: «A вызывает вас на дуэль. Принять? [✓] [✗]».
5. B нажимает [✓] → CombatServer.Instance.RespondDuelRpc(duelId, accept=true).
   Или [✗] → decline → A получает уведомление.
   Или 30 сек timeout → decline.
6. Server (accept): CombatServer.AcceptDuel(duelId):
     - Создаёт DuelInstance с двумя IAttacker/IDamageTarget.
     - Регистрирует обоих как `DuelParticipant[0]`, `DuelParticipant[1]`.
     - Отправляет DuelStartedTargetRpc обоим.
7. Client: CombatClientState.HandleDuelStarted(duelInstanceId, opponentName).
     - UI: дуэльный экран (HP/AP обоих, таймер).
8. Бой идёт (стандартный combat, но **cooldown в 2x медленнее** для баланса, Phase 2):
     - A нажимает ЛКМ → RequestAttackRpc(B.id, sword.id).
     - Server: ResolveAttack(A.id, B.id, sword.id) — тот же код, что и PvE!
     - Broadcast (только 2 клиента, не multicast).
9. Win condition: один HP=0 → BroadcastDuelEnded(winnerId, loserId).
10. Server: DuelEnded:
     - Credits transfer: A.credits += stake; B.credits -= stake.
     - XP penalty: B.ApplyDeathPenalty(20% XP loss).
11. Client: UI "Victory!" или "Defeat".
```

### 3.3 Что нужно (T-RTC11..T-RTC15)

| # | Тикет | Что | Время |
|---|---|---|---|
| T-RTC11 | PvP-duel data structures | `DuelInstance`, `DuelParticipant`, `DuelConfig` (SO) | ~3 ч |
| T-RTC12 | Duel invite flow | `RequestDuelRpc`, `RespondDuelRpc`, `SendDuelInviteTargetRpc` | ~3 ч |
| T-RTC13 | Duel battle integration | `RegisterDuelParticipant`, `DuelStartedTargetRpc`, `DuelEndedTargetRpc` | ~3 ч |
| T-RTC14 | Duel rewards + XP penalty | Credits transfer, ApplyDeathPenalty (20% XP loss) | ~2-3 ч |
| T-RTC15 | Duel UI | «Вызвать на дуэль», «Принять?», duel HUD, victory/defeat screen | ~4-5 ч |
| **ИТОГО** | — | — | **~15-20 ч** (2 сессии) |

### 3.4 Anti-cheat в PvP

**Те же принципы:** server-authoritative, rate limit, distance check, cooldown. **0 специальной защиты** — стандартные механизмы покрывают.

---

## 4. Ship combat (Phase 3, anti-restrictive)

### 4.1 Концепция

**FUTURE, не MVP.** Игрок управляет кораблём (через `ShipController.cs`, существующий), на корабле есть турели (`Turret : IDamageSource`, новые). Игрок стреляет по NPC-кораблю.

**Ключевая идея:** **0 изменений** в `CombatServer.cs`, `DamageCalculator.cs`, интерфейсах. Только **новые классы-реализации**.

### 4.2 Phase 3: новые файлы (без изменений в ядре)

```csharp
// Новый файл: Assets/_Project/Scripts/Combat/Implementations/ShipAttacker.cs
public class ShipAttacker : NetworkBehaviour, IAttacker {
    private ShipController _ship;
    private List<Turret> _turrets;
    private ulong _pilotClientId;
    
    public Vector3 GetPosition() => _ship.transform.position;
    public int GetStrength() => /* ship armor / 10 */ 5;
    public int GetDexterity() => /* pilot */ 10;
    public int GetIntelligence() => /* pilot */ 10;
    public IReadOnlyList<IDamageSource> GetActiveDamageSources() => _turrets.Cast<IDamageSource>().ToList();
    public IDamageSource GetDamageSource(ulong sourceId) => _turrets.FirstOrDefault(t => t.GetSourceId() == sourceId);
    public bool IsAlive() => _ship.GetCurrentHp() > 0;
    public bool IsPlayer() => _pilotClientId != 0;
    // ... CanAttack, SetCooldown на турелях ...
}

// Новый файл: ShipTarget.cs
public class ShipTarget : NetworkBehaviour, IDamageTarget {
    private ShipController _ship;
    public int GetCurrentHp() => _ship.GetCurrentHp();
    public int GetMaxHp() => _ship.GetMaxHp();
    public int GetArmorDefense() => _ship.GetArmorHull() + _ship.GetArmorShield();
    public void ApplyDamage(DamageResult result, ulong attackerClientId) {
        _ship.ApplyDamage(result.finalDamage, result.damageType);
    }
    // ...
}

// Новый файл: Turret.cs
public class Turret : NetworkBehaviour, IDamageSource {
    [SerializeField] private TurretConfig _config;
    public ulong GetSourceId() => (ulong)GetInstanceID();
    public DamageType GetDamageType() => _config.damageType;
    public DamageDice GetDamageDice() => _config.damageDice;  // d20 для крупных снарядов
    public int GetBaseDamage() => _config.baseDamage;
    public int GetCritModifier() => _config.critModifier;
    public float GetRange() => _config.range;  // 100-1000м
    public float GetCooldownSeconds() => _config.cooldownSeconds;  // 2-5 сек (турель)
    public float GetSkillMultiplier(ulong attackerId) => /* pilot skill */ 1.0f;
    public string GetDisplayName() => _config.turretName;
}

// Новый файл: ShipRangePolicy.cs
public class ShipRangePolicy : IRangePolicy {
    public bool IsInRange(IAttacker a, IDamageTarget t, IDamageSource s) {
        return Distance(a, t) <= s.GetRange();
    }
    public bool RequiresLineOfSight => true;
    public float CalculateHitChance(IAttacker a, IDamageTarget t, IDamageSource s) {
        // Phase 3: учитывает маневренность, дистанцию, угла
        return 0.5f;
    }
}
```

### 4.3 Sequence: «Player's ship fires turret at NPC ship»

```
1. Player (как пилот корабля) нажимает ЛКМ (target lock на NPC-корабль).
2. PlayerInput → ShipController.RequestTurretFireRpc(enemyShipId, turretId).
3. Server: ShipController.RequestTurretFireRpc:
     - Валидация: pilotId owner of this ShipController, RateLimit.
     - CombatServer.Instance.RequestAttackRpc(shipId, enemyShipId, turretId).
       // ^^^^^ ТОТ ЖЕ RPC, что и для пешего.
4. Server: CombatServer.ResolveAttack(shipId, enemyShipId, turretId):
     - attacker = _attackers[shipId] (ShipAttacker)
     - target = _targets[enemyShipId] (ShipTarget)
     - source = attacker.GetDamageSource(turretId) (Turret)
     - rangePolicy = ShipRangePolicy (turret → range 100-1000м)
     - IsInRange: distance(enemyShip) ≤ turret.range → OK
     - hitChance = 0.5 (Phase 3)
     - isHit = random < 0.5 → true/false
     - DamageCalculator.Calculate(ship, enemyShip, turret):
       - roll d20 = 12 (турель — крупные снаряды)
       - baseAttack = 12 + 30 (base) + 5 (ship STR) = 47
       - critMult = 1.0 (no crit в этом броске)
       - skillMult = 1.0 (без навыков турели)
       - preDefense = 47
       - defense = (enemyShip.armorHull + enemyShip.armorShield) × 1.0 (Ballistic) = 50 + 20 = 70
       - final = 47 - 70 = max(0, 47 - 70) = 0
       // Ой, не пробил! У NPC-корабля слишком много брони.
     - enemyShip.ApplyDamage(result, shipId) → enemyShip.currentHp -= 0.
5. Client: AttackLandedTargetRpc broadcast (2 пилота + nearby).
6. UI: damage number "0" на NPC-корабле (или "miss").
7. Player's ship стреляет ещё раз → hits → enemyShip.HP -= 15.
8. После N попаданий → enemyShip.HP = 0 → BroadcastEntityKilled.
```

### 4.4 Anti-restrictive в действии

**CombatServer.cs** — **НИЧЕГО НЕ МЕНЯЕТСЯ** для ship combat. Тот же код, что и для пешего.

```csharp
// CombatServer.ResolveAttack — универсальный:
public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId) {
    if (!_attackers.TryGetValue(attackerId, out var attacker)) return;  // ShipAttacker or PlayerAttacker
    if (!_targets.TryGetValue(targetId, out var target)) return;  // ShipTarget or NpcTarget
    var source = attacker.GetDamageSource(sourceId);  // Turret or WeaponDamageSource
    var rangePolicy = GetRangePolicy(source);  // ShipRangePolicy or MeleeRangePolicy
    // ... ERPR-формула ...
}
```

**Вердикт:** anti-restrictive design работает. **0 изменений** в ядре.

### 4.5 Трудозатраты ship combat (Phase 3)

| # | Тикет | Что | Время |
|---|---|---|---|
| T-RTC16 | ShipAttacker + ShipTarget | Player + NPC ship реализации | ~6-8 ч |
| T-RTC17 | Turret + TurretConfig (SO) | Турель как IDamageSource | ~4-5 ч |
| T-RTC18 | ShipRangePolicy + line-of-sight | Range policy + raycast | ~5-6 ч |
| T-RTC19 | Ship combat UI (HUD, target lock, fire button) | UI для турелей | ~5-7 ч |
| T-RTC20 | NPC-ship враждебный (агрессивный AI) | NpcShipAttacker + AI | ~5-7 ч |
| **ИТОГО** | — | — | **~25-33 ч** (3 сессии) |

### 4.6 Что нужно в `ShipController.cs` (Phase 3, минорные изменения)

- `ShipController.GetCurrentHp()` (новый метод).
- `ShipController.GetArmorHull()` (новый).
- `ShipController.GetArmorShield()` (новый).
- `ShipController.ApplyDamage(int, DamageType)` (новый).
- `ShipController.RequestTurretFireRpc(...)` (новый RPC).

**Без изменений** в существующей логике (pilots, fuel, AddPilotRpc/RemovePilotRpc). Только **добавления**.

### 4.7 Открытые лор-вопросы для ship combat

См. `02_LORE.md §8`:
- L7: турели (тип урона, скорострельность, дальность) — открыто для game-designer'а.
- L8: damage-type для корабля (отдельный enum или те же 5) — предполагаем те же 5.
- L9: NPC-враждебные корабли — открыто.
- L10: PvP-корабль vs корабль — открыто.
- L11: armorHull + armorShield (два слоя) — предполагаем.
- L12: Co-Op несколько пилотов — предполагаем среднее.

**Вердикт:** ship combat — **после ЗБТ**. Сейчас проектируем **hooks** (`IAttacker/IDamageTarget/IDamageSource`), ship combat реализуется как новые классы.

---

## 5. Гибрид (теоретический)

### 5.1 Абордаж (пеший внутри корабля)

**Сценарий:** Player A в корабле подходит к NPC-кораблю, бросает абордаж-крюк, переходит внутрь. Бой пеший, но в restricted space корабля.

**Реализация (Phase 3+):**
- Player заходит в NPC-корабль → `ShipController.RequestBoardingAsync()` → сервер переключает на "boarding mode" → игрок контролирует пешего персонажа на палубе NPC-корабля.
- Damage считается стандартно (пеший combat), но `range` оружия уменьшен (тесные коридоры), `hitChance` снижен (плохая видимость).
- `ShipAttacker` (NPC-корабль) НЕ атакует в boarding mode (только NPC-экипаж, пеший combat).

**Вердикт:** специфический сценарий, Phase 4 (после ЗБТ). Hooks уже есть.

### 5.2 Осада города (mixed scale)

**Сценарий:** Корабли обстреливают город (турели), защитники на стенах стреляют в корабль (anti-air пехота). Mixed scale.

**Реализация (Phase 4+):**
- City building как `IDamageTarget` (с `armorDefense` = wall+building).
- Anti-air gun на стене как `IDamageSource` (тип = Ballistic, range = 200м, dmg dice d12).
- `ShipAttacker` обстреливает город.
- Пешие защитники обстреливают корабль.

**Вердикт:** Phase 4. Hooks уже есть.

### 5.3 NPC-враг в пешем combat (Phase 2)

**Сценарий:** NPC-враг в open world (не в TB-данже). Игрок замечает NPC → атакует → NPC отвечает.

**Реализация (Phase 2):**
- NPC-враг спавнится отдельной подсистемой (NPC-AI).
- NPC-враг имеет `NpcAttacker` + `NpcTarget` (уже спроектировано).
- CombatServer не различает PvE/PvP/NPC — работает с `IAttacker/IDamageTarget`.

**Вердикт:** Phase 2, отдельная подсистема для NPC-AI.

---

## 6. Сводная таблица сценариев

| # | Сценарий | Когда | Трудозатраты | Зависимости |
|---|---|---|---|---|
| §1 | Пеший MVP (basic combat без навыков) | T-RTC01..T-RTC10 | **~23-32 ч** | нет (самодостаточный) |
| §2 | Пеший + навыки (skill hook) | T-RTC01..T-RTC10 + T-CB01..T-CB09 | +16-21 ч | T-RTC*, T-CB* |
| §3 | PvP-дуэль 1v1 | T-RTC11..T-RTC15 | +15-20 ч | T-RTC01..T-RTC10 |
| §4 | Ship combat (FUTURE) | T-RTC16..T-RTC20 | +25-33 ч | T-RTC01..T-RTC10 (anti-restrictive) |
| §5 | Гибрид (абордаж, осада) | Phase 4 | TBD | T-RTC16..T-RTC20 |

**Общий объём (включая ship combat):** ~80-110 ч (10-15 сессий).

**MVP (пеший без навыков + навыки):** ~40-53 ч (5-7 сессий).

---

## 7. Что НЕ делаем (явные запреты)

- ❌ Ship combat (Phase 3, отложено).
- ❌ NPC-AI для open world (отдельная подсистема).
- ❌ PvP-дуэль flow (Phase 2, T-RTC11..T-RTC15).
- ❌ UI damage numbers (T-RTC10, Phase 2, не блокирует MVP).
- ❌ Turn-based battles (parking, `turn-based-battles/`).
- ❌ Магия (lore).
- ❌ VFX, sound, animations (отдельные отделы).
- ❌ Line-of-sight (Phase 2).
- ❌ Client prediction (Phase 2).
- ❌ Anti-cheat beyond server-authoritative (Phase 3).
- ❌ Anti-restrictive refactoring существующего кода (только add-only).
- ❌ Писать код в этой сессии.
