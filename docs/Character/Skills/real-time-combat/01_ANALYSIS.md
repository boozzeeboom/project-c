# Analysis — что есть / чего нет / Sequencing / Ship-combat extensibility

> **Дата:** 2026-06-25 (v0.3 — новый sequencing, после ответов пользователя)
> **Метод:** read_file существующих .cs + .md + grep по `Assets/`
> **Цель:** зафиксировать фактическое состояние, gaps и anti-restrictive patterns для движка, который должен **сегодня** работать для пешего боя и **в будущем** — для ship combat без рефакторинга.

---

## 1. Что УЖЕ реализовано (на что опираемся)

### 1.1 Skill tree (T-P11..T-P13, ✅) — навыки как **opt-in**

`Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` (98 строк) + `SkillsServer.cs` (206 строк) + `SkillsWorld.cs` + `SkillsClientState.cs`.

**Для движка:**
- `SkillsWorld.GetLearnedSkills(clientId)` — движок читает, какие навыки изучены.
- `SkillEffect.Type` (StatMod, AbilityUnlock, PassiveEffect) — hooks для бонусов.
- **Движок работает БЕЗ навыков** — навыки только дают бонусы (skillMult, critMod, damageMult).

**Вердикт:** навыки — **opt-in** слой, движок не блокируется их отсутствием. Это критично для MVP.

### 1.2 Equipment (T-P07..T-P09, ✅) — оружие как `IDamageSource`

`Assets/_Project/Scripts/Equipment/EquipSlot.cs` (enum: Head..WeaponOff..Module3) + `ClothingItemData.cs` + `ModuleItemData.cs` + `EquipmentWorld.cs` + `EquipmentServer.cs`.

**Для движка:**
- `EquipmentData.equipment[EquipSlot.WeaponMain/Off]` — ID экипированного оружия.
- `InventoryWorld.GetItemDataById(itemId)` — возвращает `ItemData`, нужен `is WeaponItemData` check.
- **`WeaponItemData`** (после T-CB03) — содержит `damageDice`, `baseDamage`, `critModifier`, `range`, `damageType`. **Готов к использованию движком**.

**Вердикт:** после T-CB03 — `WeaponItemData` = `IDamageSource` (через adapter).

### 1.3 Stats (T-P01..T-P06, ✅) — STR/DEX/INT модификаторы

`Assets/_Project/Scripts/Stats/StatsConfig.cs` + `PlayerStats.cs` + `StatsWorld.cs` + `StatsClientState.cs` + `StatsServer.cs`.

**Для движка:**
- `StatsWorld.GetOrCreateStats(clientId)` → `PlayerStats.strength/dexterity/intelligence/tiers`.
- **STR** → damage bonus.
- **DEX** → hit chance, dodge.
- **INT** → skill effectiveness.

**Вердикт:** готов. Движок читает `StatsWorld` для модификаторов.

### 1.4 NetworkManager + NGO 2.x — server-authoritative

`Assets/_Project/Scripts/Core/NetworkManagerController.cs` + `Unity.Netcode`.

**Для движка:**
- `[Rpc(SendTo.Server, RequireOwnership = true)]` — client → server.
- `[Rpc(SendTo.SpecifiedInParams)]` — server → client (multicast через `RpcTarget.Group`).
- `NetworkVariable<T>` — replicated state (HP, alive).
- `NetworkBehaviour.OnNetworkSpawn/OnNetworkDespawn` — lifecycle.

**Вердикт:** готов. Pattern копируется с `SkillsServer`/`EquipmentServer`/`StatsServer`.

### 1.5 WorldEventBus — для publish/subscribe

`Assets/_Project/Core/WorldEventBus.cs` (82 строки, реализован) + `WorldEvent.cs` (154 строки).

**Для движка (4 новых events):**
- `AttackStartedEvent` — игрок/NPC/Ship начал атаку.
- `AttackLandedEvent` — атака достигла цели (hit, не miss).
- `DamageDealtEvent` — урон нанесён (с breakdown: base, hit, crit, final).
- `EntityKilledEvent` — HP = 0, сущность уничтожена.

**Вердикт:** готов. Добавляем 4 event-класса в `WorldEvent.cs`.

### 1.6 ShipController (player-ship) — основа для будущего ship combat

`Assets/_Project/Scripts/Player/ShipController.cs` (~939 строк, реализован, M3.2.15+).

**Что есть:**
- `ShipController._rb` (Rigidbody) — позиция, скорость.
- `ShipController._pilots` (HashSet<ulong>) — кто в корабле.
- `ShipController.FixedUpdate` (line 352-355, 442-460) — physics, добавление пилотов.
- `ShipController.AddPilotRpc/RemovePilotRpc` (line 921, 939) — RPC для входа/выхода.
- `ShipController._fuelSystem` (мезий) — ShipFuelSystem.
- **Нет** оружейной системы (турели, damage). **Боёвка на кораблях — future**.

**Вердикт:** для **ship combat** (future, Phase 3) — `ShipAttacker` adapter использует `ShipController._rb.position`, `ShipController._pilots`, но **добавляет** `IDamageSource` (турели) + `IDamageTarget` (броня/щит). **Без изменения `ShipController.cs`**.

### 1.7 NpcShipController (peaceful ship) — мирные NPC-корабли

`Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` + NpcShipWorld + NpcShipServer (network) + NpcShipClientState.

**Что есть:**
- NPC-корабли летают по маршрутам (`NpcShipRoute.cs`).
- Network: spawn/traffic manager (`NpcShipServer.cs`, `NpcShipTrafficManager.cs`).
- **Мирные** — без оружия, без damage.

**Вердикт:** для ship combat (Phase 3) — `NpcShipAttacker` adapter для враждебных NPC-кораблей. Сейчас — не нужно.

### 1.8 Damage-формула ERPR (готова в `Battle/10_DESIGN.md §7`)

```csharp
// Готова, переиспользуется
public static int CalculateDamage(IAttacker attacker, IDamageTarget defender, IDamageSource source, SkillNodeConfig skill = null) {
    int roll = source.DamageDice.Roll();
    int baseAttack = roll + source.BaseDamage + attacker.GetStrength();
    float locMult = 1.0f;  // hit_location ОТКЛЮЧЕН в real-time (2.17)
    int critRoll = UnityEngine.Random.Range(1, 101);
    bool isCrit = (critRoll + source.CritModifier) >= 100;
    float critMult = isCrit ? 2.0f : 1.0f;
    float skillMult = 1.0f;  // skillMult БЕЗ cap (2.18)
    int preDefense = Mathf.RoundToInt(baseAttack * locMult * critMult * skillMult);
    int totalArmor = defender.GetArmorDefense();
    float armorMult = source.DamageType switch { ... };
    int effectiveDefense = Mathf.RoundToInt(totalArmor * armorMult);
    return Mathf.Max(0, preDefense - effectiveDefense);
}
```

**Вердикт:** готова. `Battle/10_DESIGN.md §7` — спецификация. Реализация в `Assets/_Project/Scripts/Combat/DamageCalculator.cs` (T-RTC05).

### 1.9 ERPR-пакет в `WeaponItemData` (T-CB03, готов к реализации)

3 новых поля: `damageDice`, `baseDamage`, `critModifier`, `range` (ERPR, см. `Battle/10_DESIGN.md §3.1`).

**Вердикт:** дизайн готов, реализация в T-CB03 (MVP+1). До этого движок работает **без ERPR-полей** (дефолты в `OnValidate`).

---

## 2. Чего НЕТ (gaps) — что создаём

| # | Gap | Что делаем | Тикет |
|---|---|---|---|
| G1 | `IAttacker` interface | абстракция для атакующего | T-RTC01 |
| G2 | `IDamageTarget` interface | абстракция для цели | T-RTC01 |
| G3 | `IDamageSource` interface | абстракция для источника урона | T-RTC01 |
| G4 | `IRangePolicy` interface | distance check | T-RTC01 |
| G5 | `DamageType` enum (Physical/Ballistic/Antigrav/Explosive/Mesium) | готов в `Battle/10_DESIGN.md §3.1`, переиспользуем | T-CB03 (после движка) |
| G6 | `DamageDice` enum (d4/d6/d8/d10/d12/d20) + extensions | готов | T-CB03 / T-RTC01 |
| G7 | `PlayerAttacker : MonoBehaviour, IAttacker` | реализация для игрока | T-RTC02 |
| G8 | `PlayerTarget : NetworkBehaviour, IDamageTarget` | реализация для игрока-цели | T-RTC02 |
| G9 | `NpcAttacker : MonoBehaviour, IAttacker` | реализация для NPC | T-RTC03 |
| G10 | `NpcTarget : NetworkBehaviour, IDamageTarget` | реализация для NPC-цели | T-RTC03 |
| G11 | `WeaponDamageSource : IDamageSource` | adapter для `WeaponItemData` | T-RTC04 |
| G12 | `MeleeRangePolicy : IRangePolicy` | ближний бой (1-2м) | T-RTC04 |
| G13 | `RangedRangePolicy : IRangePolicy` | дальний бой (5-100м) | T-RTC04 |
| G14 | `DamageCalculator` (static class) | ERPR-формула | T-RTC05 |
| G15 | `CombatServer : NetworkBehaviour` | server-authoritative hub | T-RTC06 |
| G16 | `CombatWorld` (POCO) | server-side state (registry IAttacker/IDamageTarget) | T-RTC06 |
| G17 | `CombatClientState` (singleton) | клиентский event-bus | T-RTC07 |
| G18 | NGO RPC: `RequestAttackRpc`, `RequestSkillRpc`, `RequestDefendRpc` | client → server | T-RTC08 |
| G19 | TargetRPC: `AttackLandedTargetRpc`, `DamageDealtTargetRpc`, `EntityKilledTargetRpc` | server → client | T-RTC08 |
| G20 | `CombatConfig` (SO) | настройки баланса | T-RTC09 |
| G21 | 4 новых event-класса в `WorldEvent.cs` | `AttackStartedEvent`, `AttackLandedEvent`, `DamageDealtEvent`, `EntityKilledEvent` | T-RTC09 |
| G22 | Damage numbers (floating text) + UI hit-feedback | визуальная обратная связь | T-RTC10 (Phase 2, MVP без него) |
| G23 | `ShipAttacker : NetworkBehaviour, IAttacker` (FUTURE) | адаптер для ship combat | T-RTC16 (Phase 3) |
| G24 | `ShipTarget : NetworkBehaviour, IDamageTarget` (FUTURE) | адаптер для ship combat | T-RTC17 (Phase 3) |
| G25 | `TurretDamageSource : IDamageSource` (FUTURE) | турель на корабле | T-RTC18 (Phase 3) |
| G26 | `ShipRangePolicy : IRangePolicy` (FUTURE) | distance между кораблями | T-RTC19 (Phase 3) |
| G27 | PvP duel flow | 1v1 duel между игроками | T-RTC11..T-RTC15 (Phase 2) |
| G28 | NPC-враги (HostileNPC) | NPC-AI для враждебных NPC в open world | **отдельная подсистема** |

**Вердикт:** **10 тикетов для пешего MVP** (T-RTC01..T-RTC10, ~30-40 ч), **5 тикетов для ship combat** (T-RTC16..T-RTC20, Phase 3), **5 тикетов для PvP** (T-RTC11..T-RTC15, Phase 2). NPC-враги — отдельная подсистема, вне scope.

---

## 3. Sequencing (новый, после ответов пользователя)

### 3.1 Раньше (v0.2)

```
1. T-CB01..T-CB09 (навыки + ERPR, ~16-21 ч)
2. T-CB10 (real-time combat engine, ~30-40 ч)
3. T-TB01..T-TB14 (turn-based, ~46 ч, отложен)
```

### 3.2 Сейчас (v0.3, после ответов)

```
1. T-RTC01..T-RTC10 (real-time combat engine, ~30-40 ч) ← MVP
2. T-CB01..T-CB09 (навыки + ERPR, ~16-21 ч) ← MVP+1, после движка
3. T-RTC11..T-RTC15 (PvP duel, ~15-20 ч) ← Phase 2
4. T-RTC16..T-RTC20 (ship combat adapter, ~25-35 ч) ← Phase 3
5. T-TB01..T-TB14 (turn-based, ~46 ч) ← PARKING, после ЗБТ
```

### 3.3 Почему такой порядок

1. **Движок сначала** — даёт **играбельный combat** даже без навыков (PvE с базовым оружием).
2. **Навыки потом** — подключаются к движку как **opt-in** слой (бонус урона, crit chance, и т.п.).
3. **PvP после пешего** — использует инфраструктуру движка, добавляет duel flow + UI.
4. **Ship combat после PvP** — это **другая подсистема**, требует стабильного ядра движка + сетевой синхронизации кораблей.
5. **Turn-based после ЗБТ** — отложен, parking.

### 3.4 Критическая зависимость: движок работает БЕЗ навыков

**Ключевая идея:** движок **не блокируется** навыками. На MVP (T-RTC01..T-RTC10) можно:
- Дать игроку базовое оружие (`Weapon_WoodenSword` с дефолтными ERPR-параметрами).
- Спавнить NPC-врагов.
- Бой работает: roll d6 (hardcoded default), damage = STR + 1d6 + base, defense = armor.
- **Без навыков** — только базовый combat.

**После T-CB01..T-CB09 (MVP+1)**:
- Навыки дают бонусы (skillMult, critMod, и т.п.).
- Прогрессия игрока.

**Вердикт:** движок + дефолтное оружие = играбельный бой БЕЗ навыков. Навыки — opt-in, не блокируют.

---

## 4. Anti-restrictive patterns (для ship combat extensibility)

### 4.1 Принцип: "Engine doesn't know what's attacking, only that something attacks"

**❌ ПЛОХО (restrictive):**
```csharp
public class CombatServer : NetworkBehaviour {
    public void ResolveAttack(ulong attackerId, ulong targetId) {
        var attacker = NetworkManager.Singleton.ConnectedClients[attackerId].PlayerObject.GetComponent<NetworkPlayer>();
        var target = NetworkManager.Singleton.ConnectedClients[targetId].PlayerObject.GetComponent<NetworkPlayer>();
        if (attacker == null || target == null) return;  // ❌ не работает для NPC, кораблей
        // damage logic ...
    }
}
```

**✅ ХОРОШО (anti-restrictive):**
```csharp
public class CombatServer : NetworkBehaviour {
    private Dictionary<ulong, IAttacker> _attackers = new();
    private Dictionary<ulong, IDamageTarget> _targets = new();

    public void RegisterAttacker(ulong id, IAttacker attacker) {
        _attackers[id] = attacker;
    }

    public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId) {
        if (!_attackers.TryGetValue(attackerId, out var attacker)) return;
        if (!_targets.TryGetValue(targetId, out var target)) return;
        var source = attacker.GetActiveDamageSources().FirstOrDefault(s => /* find by id */);
        var result = DamageCalculator.Calculate(attacker, target, source, /* skill */ null);
        target.ApplyDamage(result, attackerId);
        // broadcast ...
    }
}
```

### 4.2 Принцип: "Composition over inheritance"

**❌ ПЛОХО:**
```csharp
public abstract class BaseAttacker {
    public abstract int GetStrength();
    public abstract IWeapon GetWeapon();
}

public class PlayerAttacker : BaseAttacker { ... }
public class ShipAttacker : BaseAttacker { ... }  // ❌ наследование усложняет
```

**✅ ХОРОШО:**
```csharp
public interface IAttacker {
    int GetStrength();
    IReadOnlyList<IDamageSource> GetActiveDamageSources();
    // ...
}

// PlayerAttacker реализует через композицию
public class PlayerAttacker : MonoBehaviour, IAttacker {
    private PlayerStats _stats;
    private EquipmentData _equipment;
    public int GetStrength() => _stats?.strength ?? 10;
    public IReadOnlyList<IDamageSource> GetActiveDamageSources() {
        // возвращает [MeleeWeapon из main hand, OffHandWeapon]
    }
}

// ShipAttacker (FUTURE) — тоже композиция
public class ShipAttacker : NetworkBehaviour, IAttacker {
    private List<Turret> _turrets;
    public IReadOnlyList<IDamageSource> GetActiveDamageSources() {
        // возвращает [Turret1, Turret2, ...]
    }
}
```

**Вердикт:** никаких абстрактных классов, только интерфейсы + композиция. Расширение = новый класс, реализующий интерфейс.

### 4.3 Принцип: "Damage is a Result, not a verb"

**❌ ПЛОХО:**
```csharp
target.TakeDamage(15);  // ❌ неясно, как был вычислен урон
```

**✅ ХОРОШО:**
```csharp
var result = new DamageResult {
    baseAttack = 17,
    locMult = 1.0f,
    critMult = 2.0f,
    skillMult = 1.0f,
    preDefenseDamage = 34,
    effectiveDefense = 5,
    finalDamage = 29,
    isCrit = true,
    hitLocation = HitLocation.Torso,
    damageType = DamageType.Physical,
    attackerId = ...,
    targetId = ...,
};
target.ApplyDamage(result, attackerId);
// Broadcast
SendDamageDealtTargetRpc(result);
// UI
ShowDamageNumber(result);
```

**Вердикт:** `DamageResult` — структура с **полной информацией**. UI / logs / analytics — все читают из неё. **Не нужен** refactor при добавлении нового поля (просто добавляем в struct).

### 4.4 Принцип: "Server-authoritative, но client-предсказание"

```csharp
// Server (authoritative)
[Rpc(SendTo.Server)]
public void RequestAttackRpc(ulong targetId, RpcParams rpcParams = default) {
    ulong attackerId = rpcParams.Receive.SenderClientId;
    ResolveAttack(attackerId, targetId);  // server-side
    // Отправляет результат всем через TargetRPC
}

// Client (предсказание)
public class CombatClientState {
    public void OnLocalAttack(ulong targetId) {
        // Показать анимацию, damage number СРАЗУ
        // Дождаться подтверждения от сервера, скорректировать
    }
}
```

**Вердикт:** сервер — истина, клиент — UX. Подробности в `20_TECHNICAL.md §5`.

### 4.5 Принцип: "Один движок, разные damage sources"

```csharp
// DamageCalculator — generic, не знает о Player/Ship
public static DamageResult Calculate(IAttacker attacker, IDamageTarget defender, IDamageSource source) {
    // ... ERPR-формула ...
}

// PlayerAttacker использует:
var sword = new WeaponDamageSource(playerEquipment, weaponData);
var result = DamageCalculator.Calculate(playerAttacker, npcTarget, sword);

// ShipAttacker (FUTURE) использует:
var turret = new TurretDamageSource(turretConfig);
var result = DamageCalculator.Calculate(shipAttacker, playerShip, turret);
```

**Вердикт:** `DamageCalculator` **generic** — работает с любыми `IAttacker/IDamageTarget/IDamageSource`. **Один и тот же код** для пешего и корабельного боя.

---

## 5. Расхождения и конфликты

### 5.1 С `Battle/01_ANALYSIS.md §3.2` (GDD 20 расхождение) — **сохраняется**

GDD 20 описывает **корабельный** бой (Pilot/Merchant/Explorer, 4 стата корабля, 50 уровней). v2 character progression — **пехотный** (Сила/Ловкость/Интеллект). **Разные сущности**.

**Решение:** не трогаем GDD 20 (gdd/ read-only). Зафиксировано в `Battle/01_ANALYSIS.md §3.2`.

### 5.2 С `Battle/01_ANALYSIS.md §3.3` (ItemType.Meziy коллизия) — **сохраняется**

`ItemType.Meziy` сейчас — для **ресурса** (газ-топливо). Мезиевое оружие = `ItemType.Equipment` + `WeaponSubType.MeziyBased`. Решение (a) подтверждено пользователем (2.3).

### 5.3 HitLocation — отключен в real-time (2.17) ✅

`locMult = 1.0` в real-time combat-движок. HitLocation = **только для turn-based** (который отложен). **Сейчас в движке просто const = 1.0**.

### 5.4 SkillMult — без cap (2.18) ✅

Без `skillMult <= 2.0` clamp. Дизайнеры сами отвечают за баланс. Это даёт **гибкость для ship combat** (где master-tier навыки могут давать ×3+).

### 5.5 Парадигма сменилась: real-time first (2.9, 2.20) ✅

Раньше: навыки → движок. Теперь: **движок → навыки**. Движок **работает без навыков**. Навыки подключаются позже.

### 5.6 Antigrav-щит есть (2.4) ✅

Defense-ветка получает +1 навык (`defense_antigrav_shield`, prereq = `antigrav_basic`). Документируем в `Battle/20_SKILL_TREES.md §5.1`.

### 5.7 DEX-штраф heavy armor (2.5) ✅

`defense_heavy_armor` = `StatMod(STR+3, DEX-2)`. Подтверждено.

---

## 6. Сводка рисков (real-time specific)

| # | Риск | Severity | Mitigation |
|---|---|---|---|
| R1 | Damage dice без `WeaponItemData` (T-CB03 ещё не сделан) | medium | T-RTC01..T-RTC10 работают с **дефолтными значениями** (d6, base=1, critMod=0). Движок не блокируется. |
| R2 | Навыки не подключены в MVP | low (намеренно) | Движок работает. Навыки = opt-in. |
| R3 | Ship combat адаптация ломает существующий код | low | Anti-restrictive design: `ShipAttacker` = новый класс, `ShipController.cs` не трогаем. |
| R4 | PvP-duel flow пересекается с NPC-взаимодействиями | medium | `CombatServer` различает `IAttacker`/`IDamageTarget`, duel = special mode flag в RPC. |
| R5 | Damage-формула без HitLocation слишком простая | low | В TB = полная формула (locMult + crit + skillMult). В real-time = упрощённая (crit + skillMult, locMult=1). |
| R6 | 3D-мир — raycast, line-of-sight | medium | Phase 2: `IRangePolicy.RequiresLineOfSight` hook. MVP: простая distance check. |
| R7 | Server tick rate (60 Hz) vs client tick rate | high | Server-authoritative, client-prediction, snap-to-truth каждые 100ms. |
| R8 | Network lag — клиент видит "мертвого" NPC который ещё жив | medium | Server-side `ApplyDamage` после broadcast, client-side prediction с revert. |
| R9 | Anti-cheat — клиент подменяет damage | high | Server-side damage calculation, только `[Rpc(SendTo.Server)]` для actions. См. `Battle/30_PITFALLS §1.14`. |
| R10 | ShipCombatAdapter рефакторит CombatServer | low (намеренно anti-restrictive) | CombatServer не знает о Ship. Адаптер в T-RTC16+ — отдельный код. |
| R11 | NPC-враги не существуют | accepted | Отдельная подсистема (NPC-враги/AI). Движок может работать с placeholder-врагами. |
| R12 | CombatServer + SkillsServer + EquipmentServer — три singleton, race conditions | medium | CombatServer только читает из Skills/Equipment (не пишет). Rate limit на каждом. |

---

## 7. Что НЕ делаем в этой сессии

- ❌ Не пишем код (research + design-doc only).
- ❌ Не модифицируем `docs/gdd/`.
- ❌ Не модифицируем `docs/WORLD_LORE_BOOK.md`.
- ❌ Не пишем .meta / .asmdef.
- ❌ Не запускаем `run_tests` MCP.
- ❌ Не делаем git commit / push.
- ❌ Не трогаем существующий `Battle/30_PITFALLS_AND_OPEN_QUESTIONS.md` (только обновляем в этой сессии).
- ❌ Не удаляем `turn-based-battles/` (parking).
- ❌ Не проектируем NPC-AI для open world (отдельная подсистема).
- ❌ Не проектируем ship combat полностью (только архитектурные hooks в движке).
