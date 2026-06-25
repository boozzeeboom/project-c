# Real-Time Combat Engine — пеший бой + extensible на ship combat

> **Подсистема:** Real-Time Combat Engine (пеший бой MVP, ship combat future)
> **Статус:** 🟢 Проектирование завершено (v0.3, 2026-06-25) — **все решения приняты, ждём команду «делай»**
> **Принятые решения:** 16 вопросов в `30_PITFALLS_AND_OPEN_QUESTIONS.md` — ответы пользователя подтверждены. Финальная таблица — §3.
> **Ключевая идея:** строим **combat-engine сначала**, навыки подключаются позже. Engine **extensible** для будущего ship combat (без рефакторинга) через **abstractions + composition**.
> **Scope сессии:** research + design-doc only. **Без кода.** Реализация — отдельные сессии по тикетам T-RTC01..T-RTC10.

---

## TL;DR

Пользователь дал ответы на 25 open questions в `Battle/30_PITFALLS_AND_OPEN_QUESTIONS.md` и **изменил стратегический порядок разработки**:

1. **Real-time combat-движок = MVP** (пеший бой). Тикеты T-RTC01..T-RTC10, оценка **~30-40 ч кодинга** (3-4 сессии).
2. **Навыки (T-CB01..T-CB09) = MVP+1** — подключаются **после движка** («когда уже можно будет»). Оценка **~16-21 ч**.
3. **Turn-based battles = отложен на неопределённый срок** (отдельная работа после ЗБТ). Документ `turn-based-battles/` остаётся как **parking-lot reference** (не удаляем — ЗБТ может пересмотреть приоритеты).
4. **Ship combat = future** (GDD 10 пиллар «Co-Op-first» + M3.2.15+ roadmap). Engine **должен** быть extensible под ship combat **с самого начала** — anti-restrictive design, не refactor.
5. **PvP-aware с самого начала** (2.10) — duel-флоу, faction combat, рейтинги. Реализация после пешего MVP.

**Ключевое архитектурное решение:** движок оперирует **абстракциями** (`IAttacker`, `ITarget`, `IWeapon`, `IDamageType`, `IRangePolicy`, `IDamageSource`), а не конкретными типами (`Player`, `Npc`, `Ship`). Конкретные реализации — **композиция** + **стратегии**. Это позволяет добавить ship combat в будущем **без изменения ядра движка**.

**Ключевые ответы пользователя** (влияют на дизайн):
- **2.1** — 5 дисциплин (Melee/Ranged/Explosives/Antigrav/Defense), все 35 нод.
- **2.4** — **антиграв-щит есть** (Defense-ветка +1 навык).
- **2.5** — DEX-штраф heavy armor = `-2`.
- **2.9** — **сначала real-time движок, без turn-based**.
- **2.10** — PvP-aware (с самого начала).
- **2.14** — Damage defaults **по weaponClass** (auto в `OnValidate`).
- **2.17** — **HitLocation только в TB** (real-time = `locMult = 1.0`).
- **2.18** — **SkillMult без cap** (без ограничений, для гибкости в ship combat).
- **2.20** — **начинать с движка, навыки потом**.

**Что это значит для `Battle/`:**
- `Battle/10_DESIGN.md §7` (ERPR-формула) — **готова**, переиспользуется движком.
- `Battle/20_SKILL_TREES.md` — **остаётся**, но **сдвигается в roadmap** (после T-RTC).
- `Battle/30_PITFALLS_AND_OPEN_QUESTIONS.md` — обновляется с ответами пользователя.
- `turn-based-battles/` — **parking** (не удаляем, не правим, не развиваем).

**Структура подсистемы:**
```
Real-Time Combat Engine
├── CombatServer (NetworkBehaviour) — server-authoritative hub
├── CombatClient (singleton) — клиентский event-bus
├── CombatWorld (POCO) — server-side state
├── IDamageSource interface — что наносит урон (PlayerWeapon, NpcWeapon, ShipTurret, Explosion)
├── IDamageTarget interface — что получает урон (Player, Npc, Ship, Building)
├── IWeapon interface — оружие (range, ammo, damageType)
├── IDamageType — Physical/Ballistic/Antigrav/Explosive/Mesium (ERPR-пакет)
├── IRangePolicy — distance check (PlayerMelee, PlayerRanged, ShipTurret, ShipCannon)
├── DamageCalculator (static) — ERPR-формула (готова в Battle/10_DESIGN.md §7)
├── CombatConfig (SO) — настройки баланса (server-authoritative)
├── CombatEvents (4 новых) — AttackStarted, AttackLanded, DamageDealt, EntityKilled
├── PvPDuelSystem (Phase 2) — server-authoritative duel flow
└── ShipCombatAdapter (Phase 3) — адаптер для будущего ship combat
```

**Связь с существующим:**
- Переиспользует `SkillNodeConfig` (T-P11) — навыки как **opt-in** (engine работает без навыков, навыки дают бонусы).
- Переиспользует `StatsWorld`/`EquipmentWorld` — для модификаторов урона.
- Переиспользует `WorldEventBus` — публикует combat events.
- Переиспользует `DamageCalculator` (ERPR-формула) — общая для real-time и turn-based (если/когда TB вернётся).

**Трудозатраты (обновлённые):**

| Что | Трудозатраты | Когда |
|---|---|---|
| **T-RTC01..T-RTC10** (real-time combat engine, пеший) | **~30-40 ч** | **MVP (3-4 сессии)** |
| T-CB01..T-CB09 (навыки + ERPR-пакет) | ~16-21 ч | MVP+1 (после движка) |
| T-RTC11..T-RTC15 (PvP duel flow) | ~15-20 ч | Phase 2 |
| T-RTC16..T-RTC20 (ship combat adapter) | ~25-35 ч | Phase 3 (ЗБТ+) |
| T-TB01..T-TB14 (turn-based, **отложен**) | ~46 ч | **после ЗБТ, parking** |
| **ИТОГО до играбельного combat (пеший)** | **~46-61 ч** (real-time + skills) | 6-8 сессий |

**Вердикт:** **снижение общего объёма** (~90-110 ч в v0.2 → ~46-61 ч в v0.3), потому что **turn-based отложен**. Фокус на real-time = выше качество к концу MVP.

---

## Карта документов

```
docs/Character/Skills/
├── Battle/                              ← навыки (отложены в MVP+1)
│   ├── 00_README.md
│   ├── 01_ANALYSIS.md
│   ├── 02_LORE.md
│   ├── ERPR_collaboration.md            ← damage-формула (готова, переиспользуется)
│   ├── 10_DESIGN.md                     ← §7 damage-формула (готова)
│   ├── 20_SKILL_TREES.md                ← 35 нод (отложены в roadmap)
│   ├── 30_PITFALLS_AND_OPEN_QUESTIONS.md ← обновлён с ответами
│   └── 40_REFERENCES.md
│
├── real-time-combat/                    ← ЭТОТ каталог, MVP
│   ├── 00_README.md                     ← этот файл (манифест, новый sequencing)
│   ├── 01_ANALYSIS.md                   ← что есть / gaps / anti-restrictive design
│   ├── 02_LORE.md                       ← пеший vs корабельный бой
│   ├── 10_DESIGN.md                     ← архитектура, IAttacker/ITarget abstractions
│   ├── 20_TECHNICAL.md                  ← NGO RPC, server-authoritative, hooks
│   ├── 30_SCENARIOS.md                  ← пеший MVP, ship-extensibility примеры
│   ├── 30_PITFALLS_AND_OPEN_QUESTIONS.md ← обновлённые вопросы
│   └── 40_REFERENCES.md
│
└── turn-based-battles/                  ← PARKING (не удаляем, не правим)
    ├── 00_README.md
    ├── 01_ANALYSIS.md
    ├── 10_DESIGN.md
    ├── 20_TECHNICAL.md
    ├── 30_SCENARIOS.md
    ├── 30_PITFALLS_AND_OPEN_QUESTIONS.md
    └── 40_REFERENCES.md
```

---

## Архитектурные принципы (anti-restrictive)

Движок **не знает** о конкретных сущностях (Player, Npc, Ship). Знает только **абстракции**:

### 1. Compositional abstractions

```csharp
// Что угодно, что может атаковать
public interface IAttacker {
    Vector3 GetPosition();
    int GetStrength();
    int GetDexterity();
    int GetIntelligence();
    IWeapon GetEquippedWeapon();
    IReadOnlyList<IDamageSource> GetActiveDamageSources();  // оружие И турели И ...
    bool IsAlive();
}

// Что угодно, что может получать урон
public interface IDamageTarget {
    Vector3 GetPosition();
    int GetCurrentHp();
    int GetMaxHp();
    int GetArmorDefense();
    void ApplyDamage(DamageResult result, ulong attackerClientId);
    bool IsAlive();
    bool IsPlayer();
}

// Что угодно, что наносит урон (меч, турель, граната, мина, AoE)
public interface IDamageSource {
    DamageType DamageType { get; }
    DamageDice DamageDice { get; }
    int BaseDamage { get; }
    int CritModifier { get; }
    float Range { get; }
    int AttackSecondsCost { get; }
    int GetSkillMult(ulong attackerId);  // навыки модифицируют
    int GetHitLocationBias(ulong attackerId);  // Phase 3
}

// Что угодно, что считает «в радиусе»
public interface IRangePolicy {
    bool IsInRange(IAttacker attacker, IDamageTarget target, IDamageSource source);
    float Distance(IAttacker attacker, IDamageTarget target);
    bool RequiresLineOfSight { get; }
}
```

### 2. Конкретные реализации (различные, переиспользуют ядро)

```csharp
// Пеший игрок
public class PlayerAttacker : MonoBehaviour, IAttacker { ... }
public class PlayerTarget : NetworkBehaviour, IDamageTarget { ... }

// Пеший NPC
public class NpcAttacker : MonoBehaviour, IAttacker { ... }
public class NpcTarget : NetworkBehaviour, IDamageTarget { ... }

// Корабль (FUTURE)
public class ShipAttacker : NetworkBehaviour, IAttacker {
    public IReadOnlyList<IDamageSource> GetActiveDamageSources() {
        return turrets;  // массив турелей
    }
}
public class ShipTarget : NetworkBehaviour, IDamageTarget {
    public int GetArmorDefense() => armorHull + armorShield;
}

// Турель на корабле (FUTURE)
public class TurretDamageSource : IDamageSource { ... }

// Граната / мина (FUTURE)
public class ExplosiveDamageSource : MonoBehaviour, IDamageSource { ... }
```

### 3. CombatServer — ядро, не знает о Player/Ship

```csharp
public class CombatServer : NetworkBehaviour {
    // Абстракции, не конкретные типы
    private Dictionary<ulong, IAttacker> _attackers = new();
    private Dictionary<ulong, IDamageTarget> _targets = new();

    public void RegisterAttacker(ulong id, IAttacker attacker) { ... }
    public void RegisterTarget(ulong id, IDamageTarget target) { ... }
    public void Unregister(ulong id) { ... }

    // Server-side damage flow — работает для ЛЮБЫХ IAttacker/IDamageTarget
    public DamageResult ResolveAttack(ulong attackerId, ulong targetId, IDamageSource source) {
        var attacker = _attackers[attackerId];
        var target = _targets[targetId];
        return DamageCalculator.Calculate(attacker, target, source, /* skill = */ null);
    }
}
```

### 4. Почему это anti-restrictive

- **Нет** `if (attacker is PlayerAttacker) ... else if (attacker is ShipAttacker) ...` — движок работает через полиморфизм.
- **Нет** `if (source is SwordDamageSource) ...` — каждый `IDamageSource` сам знает свой `DamageDice`, `CritModifier`.
- **Нет** жёстких enum'ов типа `AttackerType.Player | AttackerType.Ship` — есть просто `IAttacker`.
- **Расширяемость** — добавить `BuildingAttacker` (турель на стене) = реализовать `IAttacker` + `IDamageSource`, зарегистрировать в `CombatServer`. **0 изменений в ядре**.

### 5. Что НЕ делает движок

- ❌ Не знает, что «пеший» или «корабельный».
- ❌ Не знает про конкретные классы оружия.
- ❌ Не знает про анимации.
- ❌ Не знает про UI.
- ❌ Не знает про NPC-AI (NPC-AI подключается через IAttacker).

---

## Связь с другими подсистемами

| Подсистема | Связь |
|---|---|
| `Battle/ERPR_collaboration.md` | damage-формула (готова) |
| `Battle/10_DESIGN.md §7` | формула `CalculateDamage` (готова, переиспользуется) |
| `Battle/20_SKILL_TREES.md` | навыки (отложены, но `SkillEffect.Type` = hooks для движка) |
| `Character/StatsWorld` | STR/DEX/INT — модификаторы урона |
| `Character/EquipmentWorld` | `WeaponItemData` (после T-CB03) — `IDamageSource` |
| `Character/SkillsWorld` | `SkillNodeConfig` — навыки (opt-in, не блокирует движок) |
| `Core/WorldEventBus` | 4 новых event-класса (AttackStarted, AttackLanded, DamageDealt, EntityKilled) |
| `Core/NetworkManagerController` | `CreateCombatClientState()` |
| `Player/ShipController` (player-ship) | **FUTURE** — `ShipAttacker` adapter, Phase 3 |
| `PeacefulShip/NpcShipController` | **FUTURE** — мирные корабли, без боя (вне scope) |
| `NPC_quests/` | quest-events (например, "kill pirate") подключаются через `EntityKilledEvent` |
| `Crafting_system/` | рецепты (гранаты/мины, после T-CB04) — `ExplosiveDamageSource` |
| `gdd/GDD_10_Ship_System.md` | vision doc для ship combat, **не трогаем** (gdd/ read-only) |
| `gdd/GDD_20_Progression_RPG.md` | расхождение (см. `Battle/01_ANALYSIS.md §3.2`) |
| `turn-based-battles/` | **PARKING** — отложен, но архитектурно совместим (общий `DamageCalculator`) |

---

## Следующий шаг

1. Прочитай `01_ANALYSIS.md` — что есть / чего нет / gaps / anti-restrictive patterns.
2. Прочитай `02_LORE.md` — пеший vs корабельный бой в лоре.
3. Прочитай `10_DESIGN.md` — архитектура с `IAttacker/ITarget/IWeapon/IDamageSource`.
4. Прочитай `20_TECHNICAL.md` — NGO RPC, server-authoritative, hooks для навыков/корабля.
5. Прочитай `30_SCENARIOS.md` — пеший MVP, ship-extensibility примеры.
6. Прочитай `30_PITFALLS_AND_OPEN_QUESTIONS.md` — обновлённые вопросы.
7. Прочитай `40_REFERENCES.md` — file:line.
