# Real-Time Combat Engine — пеший бой + extensible на ship combat

> **Подсистема:** Real-Time Combat Engine (пеший бой MVP, ship combat future)
> **Статус:** ✅ **MVP РЕАЛИЗОВАН** (T-RTC01..T-RTC09, v0.1.4, 2026-06-25). End-to-end combat flow работает: hit/miss/crit/defense/cooldown/kill.
> **Следующий этап:** T-CB01..T-CB09 (навыки) — см. `60_NEXT_STEPS_T-CB01.md`.
> **Ключевая идея:** строим **combat-engine сначала**, навыки подключаются как **opt-in** слой через hooks. Engine **extensible** для будущего ship combat (без рефакторинга) через **abstractions + composition**.

---

## TL;DR

**Combat Engine (MVP) реализован и работает.** Пеший бой player vs NPC — end-to-end:
- 19 новых файлов в `Assets/_Project/Scripts/Combat/`
- 2 SO assets (`CombatConfig_Default`, `Npc_Goblin`)
- Scene integration (`[CombatServer]` в `BootstrapScene`, `NPC_TestEnemy` с красной капсулой-маркером в `WorldScene_0_0`)
- add-only в `WorldEvent.cs`, `NetworkManagerController.cs`, `NetworkPlayer.cs` (без refactor существующего)
- Server-authoritative, NGO 2.x RPC, NetworkVariable HP, broadcast, race condition фиксы (push-down + second-chance + pull-up)

**Что НЕ реализовано (отложено):**
- T-RTC10 (UI damage numbers, hit flash) — Phase 2
- T-RTC11..T-RTC15 (PvP duel) — Phase 2
- T-RTC16..T-RTC20 (ship combat) — Phase 3
- T-CB01..T-CB09 (навыки) — **следующая сессия**, см. `60_NEXT_STEPS_T-CB01.md`
- T-TB01..T-TB14 (turn-based) — parking, не развиваем

**T-CB03 (`WeaponItemData`) и T-CB06 (`armorDefense` поле в `ClothingItemData`)** — ещё не сделаны. Движок работает с `DefaultDamageSource` fallback (d6, base=1, critMod=0, range=2м) и `armorDefense=0`. После T-CB03/06 — реальные значения.

---

## Стратегия сессий (что сделано)

| Сессия | Что | Результат |
|---|---|---|
| **#1** (2026-06-25) | T-RTC01, T-RTC04, T-RTC05 — фундамент | 10 файлов: interfaces, enums, `DamageCalculator`, `DefaultDamageSource`, range policies. 0 errors. |
| **#2** (2026-06-25) | T-RTC02, T-RTC03, T-RTC06, T-RTC07, T-RTC08, T-RTC09 — server+client интеграция | 11 файлов: `PlayerAttacker/Target`, `NpcAttacker/Target`, `CombatServer`, `CombatClientState`, `DamageResultDto`, 4 events, hook в `NetworkManagerController` + `NetworkPlayer`. |
| **#3** (2026-06-25) | Scene integration через MCP | 2 SO assets, `[CombatServer]` в `BootstrapScene`, `NPC_TestEnemy` (с `VisualMarker` капсулой) в `WorldScene_0_0`, debug K-key в `NetworkPlayer`. |
| **#4** (2026-06-25) | Playtest #1 + race condition fix v0.1 | 3 патча: `PlayerAttacker` MonoBehaviour→NetworkBehaviour + self-register, `NpcAttacker/Target` self-register, `CombatServer.RecoverExistingEntities` push-down. |
| **#5** (2026-06-25) | Playtest #2 + register fix v0.1.1 | `NetworkPlayer.RegisterWithCombatServer`: убран ранний return, `AddComponent` всегда. |
| **#6** (2026-06-25) | Playtest #3 + host player fix v0.1.2 | `RecoverExistingEntities`: убран skip `id==0` для Player (0 = host clientId), second-chance `Invoke` через 1 сек. |
| **#7** (2026-06-25) | Playtest #4 + unarmed fallback v0.1.3 | `PlayerAttacker.EnsureUnarmedFallback()`: если оба weapon-слота пустые, добавляет `DefaultDamageSource(0, "Unarmed")`. |
| **#8** (2026-06-25) | Playtest #5 + corpse delay v0.1.4 | `NpcTarget.ApplyDamage`: при HP=0 → `Destroy(gameObject, 3.0f)` (3 сек corpse delay). |
| **#9** (2026-06-25) | T-CB03 — WeaponItemData | 4 weapon .asset, WeaponDamageSource, патчи PlayerAttacker + EquipmentServer + InventoryTab + EquipmentWorld. ✅ Play Mode — работает. |
| **#10** (2026-06-25) | T-CB06 + T-CB07 + T-CB08 — armorDefense + skillMult + 23 skills | Код готов. Runtime verify — после NPC-AI. |

**Подробный changelog:** `50_IMPL_CHANGELOG.md`.

---

## Что работает прямо сейчас (Play Mode verify)

1. **Press Play** в `BootstrapScene` → **Start Host** (через `NetworkTestMenu` или `NetworkManagerController.StartHost()`).
2. **Ожидаемое в Console:**
   - `[NMC] Created [CombatClientState] as root GameObject` (✓ — singleton root)
   - `[CombatServer] OnNetworkSpawn: Instance set, IsServer=True.`
   - `[CombatServer] RecoverExistingEntities done: attackers=2, targets=2` (после second-chance)
3. **Player spawns** → автоматически добавляются `PlayerAttacker/PlayerTarget` компоненты + регистрация в `CombatServer`.
4. **Teleport к NPC** (или подойти WASD) → нажать **K** (debug) → `RequestAttackRpc(targetId, 0)` → server вычисляет damage через `DamageCalculator` (ERPR-формула) → broadcast `AttackLandedTargetRpc(DamageResultDto)` → клиент получает через `CombatClientState`.
5. **Пример успешного боя** (из Play Mode #5):
   ```
   K-attack: targetId=45, dist=1,48м
   DamageCalculator: baseAttack=15, hitChance=0,79, isHit=True, preDefense=15, defense=0, final=15
   NpcTarget took 15 (HP 20 → 5)
   AttackLanded: dmg=15, crit=False
   ... cooldown 1s ...
   K-attack → MISS (random<0,79) — промах
   ... cooldown ...
   K-attack → OnCooldown (spam)
   ... 
   K-attack → hit (HP 5 → 0) + EntityKilled: target=45
   NpcTarget killed. Destroying in 3s (corpse delay).
   ... 3 сек ...
   NetworkObject OnDestroy → капсула исчезает.
   ```

---

## Архитектура (как есть в коде, v0.1.4)

### Структура файлов

```
Assets/_Project/Scripts/Combat/
├── Core/                       (ProjectC.Combat.Core namespace)
│   ├── IAttacker.cs            (interface: что угодно, что может атаковать)
│   ├── IDamageTarget.cs        (interface: что угодно, что может получать урон)
│   ├── IDamageSource.cs        (interface: что угодно, что наносит урон)
│   ├── IRangePolicy.cs         (interface: distance check + hit chance)
│   ├── DamageType.cs           (enum DamageType + DamageDice + extensions: Roll/Average/ArmorMultiplier)
│   └── DamageResult.cs         (POCO struct, server-authoritative, не сериализуется)
│
├── Implementations/            (ProjectC.Combat namespace)
│   ├── PlayerAttacker.cs       (NetworkBehaviour, IAttacker)
│   ├── PlayerTarget.cs         (NetworkBehaviour, IDamageTarget)
│   ├── NpcAttacker.cs          (NetworkBehaviour, IAttacker) — v0.1: был MonoBehaviour
│   ├── NpcTarget.cs            (NetworkBehaviour, IDamageTarget)
│   ├── NpcCombatData.cs        (SO: HP, STR/DEX/INT, weapon defaults)
│   ├── DefaultDamageSource.cs  (fallback: d6, base=1, critMod=0, range=2м, type=Physical)
│   ├── MeleeRangePolicy.cs     (range<3м, baseHit=0.85, dexMod=0.85+(DEX-10)*0.015)
│   └── RangedRangePolicy.cs    (range>=3м, baseHit=0.75, аналогичный dexMod)
│
├── Network/                    (ProjectC.Combat.Network namespace)
│   ├── CombatServer.cs         (NetworkBehaviour, server-authoritative hub, RPC, registries)
│   └── DamageResultDto.cs      (INetworkSerializable struct)
│
├── Client/                     (ProjectC.Combat.Client namespace)
│   └── CombatClientState.cs    (singleton MonoBehaviour, event-bus, debug-логи)
│
├── Config/                     (ProjectC.Combat.Config namespace)
│   └── CombatConfig.cs         (SO: hit/crit/defense multipliers, serverTickRate)
│
└── DamageCalculator.cs         (static, ERPR-формула, server-authoritative)
```

### Namespace map

| Namespace | Содержимое |
|---|---|
| `ProjectC.Combat.Core` | `IAttacker`, `IDamageTarget`, `IDamageSource`, `IRangePolicy`, `DamageType`, `DamageDice`, `DamageResult` |
| `ProjectC.Combat` | `DamageCalculator`, `DefaultDamageSource`, `MeleeRangePolicy`, `RangedRangePolicy`, `PlayerAttacker`, `PlayerTarget`, `NpcAttacker`, `NpcTarget`, `NpcCombatData` |
| `ProjectC.Combat.Network` | `CombatServer`, `DamageResultDto` |
| `ProjectC.Combat.Client` | `CombatClientState` |
| `ProjectC.Combat.Config` | `CombatConfig` |

### End-to-end flow (как есть в коде)

```
[Client] Player нажимает K
    ↓
[Client] NetworkPlayer.DebugAttackNearestNpc() (UPDATE, add-only)
    ↓
[Client] CombatServer.Instance.RequestAttackRpc(targetId=45, sourceId=0)
    ↓ [RPC: SendTo.Server, RequireOwnership=true]
[Server] CombatServer.RequestAttackRpc:
    - RateLimit check (10 ops/sec)
    ↓
[Server] CombatServer.ResolveAttack(attackerId, targetId, sourceId):
    1. Lookup attacker = _attackers[0] (PlayerAttacker)
    2. Lookup target = _targets[45] (NpcTarget)
    3. target.IsAlive() check
    4. attacker.IsAlive() check
    5. source = attacker.GetDamageSource(0) → DefaultDamageSource("Unarmed")
    6. attacker.CanAttack(source, now) — cooldown check
    7. rangePolicy auto-select: source.GetRange()<3 → MeleeRangePolicy
    8. rangePolicy.IsInRange(attacker, target, source) — distance check
    9. DamageCalculator.Calculate(attacker, target, source, rangePolicy):
       - roll d6 = 3 (Random)
       - baseAttack = 3 + 1 + 10 (STR) = 14
       - hitChance = rangePolicy.CalculateHitChance() = 0.79
       - isHit = Random < 0.79 = true
       - locMult = 1.0 (real-time, 2.17)
       - critRoll 1d100 = 45 + 0 (critMod) < 100 → no crit
       - critMult = 1.0
       - skillMult = 1.0 (no skills, opt-in)
       - preDefense = 14
       - defense = target.GetArmorDefense() = 0 (NPC placeholder)
       - armorMult = Physical → 1.0
       - effectiveDefense = 0
       - final = 14
    10. attacker.SetCooldown(source, now + 1.0s)
    11. target.ApplyDamage(result, attackerId=0)
    ↓
[Server] NpcTarget.ApplyDamage:
    - _currentHp.Value = max(0, 20 - 14) = 6
    - if newHp == 0 → Destroy(gameObject, 3.0f) (v0.1.4 corpse delay)
    ↓
[Server] CombatServer.ResolveAttack (continue):
    12. DamageResultDto.FromResult(result)
    13. AttackLandedTargetRpc(dto, RpcParams { Target = RpcTarget.Everyone })
    14. WorldEventBus.Publish(AttackLandedEvent { PlayerId = 0, Result = result })
    15. if isHit: WorldEventBus.Publish(DamageDealtEvent)
    16. if isHit && !target.IsAlive(): WorldEventBus.Publish(EntityKilledEvent) + EntityKilledTargetRpc
    ↓ [RPC: SendTo.SpecifiedInParams, Target=Everyone]
[Client] CombatClientState.HandleAttackLanded(dto):
    - OnAttackLanded event → UI subscribers (Phase 2)
    - Debug.Log: "AttackLanded: attacker=0 → target=45, dmg=14, crit=False, type=Physical"
    - if isHit: OnDamageDealt event
[Client] (если EntityKilledTargetRpc) → HandleEntityKilled → OnEntityKilled event
```

### Registration flow (race-safe)

```
[Scene load] BootstrapScene
    - [CombatServer] GameObject (NetworkObject + CombatServer)
    - [PlayerSpawner] GameObject (scene-placed, skip guard)
    - WorldScene_0_0 (additive, client-side)
        - NPC_TestEnemy GameObject (NetworkObject + NpcAttacker + NpcTarget + VisualMarker)

[StartHost] (в любом порядке, NGO не гарантирует)
    1. NetworkManager.OnServerStarted
    2. ScenePlacedObjectSpawner.HandleServerStarted → Spawn(destroyWithScene=true) scene-placed NetworkObjects
    3. [CombatServer].OnNetworkSpawn:
        - Instance = this
        - RecoverExistingEntities():
            - FindObjectsByType<PlayerAttacker>: ПУСТО (Player ещё не spawned)
            - FindObjectsByType<PlayerTarget>: ПУСТО
            - FindObjectsByType<NpcAttacker>: 1 (NPC_TestEnemy уже spawned) → Register(atkId=GetInstanceID(), this)
            - FindObjectsByType<NpcTarget>: 1 → Register(tgtId=45, this) + fallback-init HP=20
        - Result: attackers=1, targets=1
        - Invoke(nameof(RecoverExistingEntities), 1.0f) — second-chance через 1 сек
    4. [NetworkPlayer].OnNetworkSpawn:
        - IsServer && !IsPlayerSpawner marker → RegisterWithCombatServer()
        - GetComponent<PlayerAttacker>() ?? gameObject.AddComponent<PlayerAttacker>() + Initialize(OwnerClientId=0)
        - GetComponent<PlayerTarget>() ?? gameObject.AddComponent<PlayerTarget>() + Initialize(0)
        - CombatServer.Instance==null (race!) → НЕ регистрирует сейчас, ждёт push-down
        - AddComponent вызывает PlayerAttacker/Target.OnNetworkSpawn → Instance==null → НЕ регистрирует
    5. [PlayerAttacker].OnNetworkSpawn: 
        - _clientId=0, CombatServer.Instance==null → НЕ регистрирует
    6. [PlayerTarget].OnNetworkSpawn: same
    7. ScenePlacedObjectSpawner re-spawn → [NPC_TestEnemy].OnNetworkSpawn:
        - NpcAttacker.OnNetworkSpawn → Register(atkId=137562, this) ✓
        - NpcTarget.OnNetworkSpawn → fallback-init, Register(tgtId=45, this) ✓
    8. +1 sec → RecoverExistingEntities (second-chance):
        - FindObjectsByType<PlayerAttacker>: 1 (ClientId=0) → Register(0, this) ✓ (v0.1.2: 0 валидный id для host)
        - FindObjectsByType<PlayerTarget>: 1 → Register(0, this) ✓
        - NpcAttacker/Target уже зарегистрированы, ContainsKey=true → skip
        - Result: attackers=2, targets=2
```

**Итог:** 2-сторонняя защита:
- **Pull-up** (NetworkBehaviour.OnNetworkSpawn → Register в CombatServer.Instance): для тех, чей OnNetworkSpawn срабатывает ПОСЛЕ `CombatServer.Instance = this`.
- **Push-down** (CombatServer.OnNetworkSpawn → RecoverExistingEntities + second-chance Invoke): для тех, чей OnNetworkSpawn сработал ДО `CombatServer.Instance = this` (но они уже в сцене, FindObjectsByType найдёт).

---

## Подробные ссылки

- **Что реализовано, какие баги, какие фиксы:** `50_IMPL_CHANGELOG.md`
- **Дизайн (что планировали, что в итоге):** `10_DESIGN.md` (обновлён со status)
- **Технический flow (RPC, registries, race fixes):** `20_TECHNICAL.md` (переписан под факт)
- **Pitfalls + race condition фиксы:** `30_PITFALLS_AND_OPEN_QUESTIONS.md` (обновлён)
- **Что нужно для следующего этапа (T-CB01..09 навыки):** `60_NEXT_STEPS_T-CB01.md`
- **Сценарии использования (пеший MVP, ship-extensibility):** `30_SCENARIOS.md` (без изменений)
- **Лор-обоснование:** `02_LORE.md` (без изменений)
- **Анализ + gaps:** `01_ANALYSIS.md` (без изменений)
- **file:line index:** `40_REFERENCES.md` (обновить, см. changelog)

---

## Карта документов

```
docs/Character/Skills/
├── Battle/                              ← навыки-в-процессе (T-CB01..09)
│   ├── 00_README.md
│   ├── 01_ANALYSIS.md
│   ├── 02_LORE.md
│   ├── ERPR_collaboration.md            ← damage-формула (готова, переиспользуется)
│   ├── 10_DESIGN.md                     ← §7 damage-формула (готова, реализована в Combat/DamageCalculator.cs)
│   ├── 20_SKILL_TREES.md                ← 35 нод (отложены)
│   ├── 30_PITFALLS_AND_OPEN_QUESTIONS.md
│   └── 40_REFERENCES.md
│
├── real-time-combat/                    ← ЭТОТ каталог, MVP РЕАЛИЗОВАН
│   ├── 00_README.md                     ← этот файл (манифест + статус)
│   ├── 01_ANALYSIS.md                   ← без изменений
│   ├── 02_LORE.md                       ← без изменений
│   ├── 10_DESIGN.md                     ← обновлён: status реализации
│   ├── 20_TECHNICAL.md                  ← переписан: факт. flow + race fixes
│   ├── 30_SCENARIOS.md                  ← без изменений
│   ├── 30_PITFALLS_AND_OPEN_QUESTIONS.md ← обновлён: race fixes
│   ├── 40_REFERENCES.md                 ← обновить file:line
│   ├── 50_IMPL_CHANGELOG.md             ← НОВЫЙ: v0.1 → v0.1.4 changelog
│   └── 60_NEXT_STEPS_T-CB01.md          ← НОВЫЙ: гайд для T-CB01..09
│
└── turn-based-battles/                  ← PARKING (не удаляем, не правим, не развиваем)
```

---

## Связь с другими подсистемами (как реализовано)

| Подсистема | Связь | Hook |
|---|---|---|
| `Battle/10_DESIGN.md §7` ERPR-формула | `DamageCalculator.Calculate` — статический метод, идентичная логика | ✓ готова |
| `Skills/SkillNodeConfig` (T-P11) | `IDamageSource.GetSkillMultiplier(attackerId)` — MVP возвращает 1.0 | hook готов, после T-CB01..09 — реальная интеграция |
| `Stats/StatsWorld` | `PlayerAttacker.GetStrength/Dexterity/Intelligence` — читает `tier*5+10` | ✓ готова (default 10) |
| `Equipment/EquipmentWorld` | `PlayerAttacker.RebuildSources` — читает EquipSlot.WeaponMain/Off | ✓ готова, fallback DefaultDamageSource (v0.1.3 unarmed) |
| `Items/InventoryWorld` | `PlayerAttacker.TryAddSourceFromSlot` — `GetItemDefinition(itemId)` | ✓ готова |
| `Core/WorldEventBus` | 4 новых event: `AttackStartedEvent`, `AttackLandedEvent`, `DamageDealtEvent`, `EntityKilledEvent` | ✓ готова |
| `Core/NetworkManagerController` | `CreateCombatClientState()` (root GO, DontDestroyOnLoad) | ✓ готова |
| `Player/NetworkPlayer` | `RegisterWithCombatServer` / `UnregisterFromCombatServer` / `DebugAttackNearestNpc` (add-only) | ✓ готова |
| `Player/ShipController` (Phase 3) | `ShipAttacker/ShipTarget` adapter — НЕ трогаем сейчас | future, anti-restrictive |
| `PeacefulShip/NpcShipController` | мирные корабли, без боя | out of scope |
| `Crafting` | рецепты гранат/мин → `ExplosiveDamageSource` | после T-CB04 |
| `NPC_quests` | quest-events (kill pirate) → `EntityKilledEvent` подписка | future |

---

## Трудозатраты (факт)

| Тикет | Оценка | Факт | Статус |
|---|---|---|---|
| T-RTC01 (core interfaces) | 2-3 ч | ~1 ч | ✅ |
| T-RTC02 (PlayerAttacker/Target) | 3-4 ч | ~2 ч (+ 4 fix-итерации) | ✅ |
| T-RTC03 (NpcAttacker/Target + NpcCombatData) | 3-4 ч | ~2 ч (+ 2 fix-итерации) | ✅ |
| T-RTC04 (DefaultDamageSource + range policies) | 2-3 ч | ~1 ч | ✅ |
| T-RTC05 (DamageCalculator) | 2-3 ч | ~1 ч | ✅ |
| T-RTC06 (CombatServer) | 4-5 ч | ~3 ч (+ 3 race fixes) | ✅ |
| T-RTC07 (CombatClientState) | 2-3 ч | ~1 ч | ✅ |
| T-RTC08 (NGO RPC + DTO) | 3-4 ч | ~2 ч | ✅ |
| T-RTC09 (CombatConfig + 4 WorldEvent) | 2-3 ч | ~1 ч | ✅ |
| T-RTC10 (UI) | (Phase 2) | — | ⏸ |
| **ИТОГО MVP (8 сессий)** | **~23-32 ч** | **~14 ч реализации + 4 ч fix-итерации** | ✅ |

**Вердикт:** оценка занижена (быстрее, чем думали), но **5 race condition багов** потребовали итеративного фикса. Общее время включая fix — ~18 ч, в рамках 23-32 ч оценки. **Хорошо.**

---

## Следующий шаг

См. `60_NEXT_STEPS_T-CB01.md` — что нужно для T-CB01..T-CB09 (навыки + skillMult hook), какие файлы трогать, какие зависимости.
