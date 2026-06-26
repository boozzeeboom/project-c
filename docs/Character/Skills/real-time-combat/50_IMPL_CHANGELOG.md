# Implementation Changelog — Real-Time Combat Engine

> **Формат:** version-based changelog, сессия за сессией.
> **v0.0** (2026-06-25) — design doc, тикеты T-RTC01..T-RTC10, без кода.
> **v0.1** (2026-06-25) — первая реализация Combat MVP (T-RTC01..T-RTC09). 19 файлов. 3 race condition fixes.
> **v0.2** (2026-06-25) — T-CB03 (WeaponItemData). 4 weapon .asset, WeaponDamageSource, InventoryTab+EquipmentWorld patches.
> **v0.3** (2026-06-25) — T-CB06 + T-CB07 + T-CB08. armorDefense + skillMult + 23 combat skills. **Код готов, runtime verify — после NPC-AI.**
> **v0.1.2** (2026-06-25, сессия #6) — fix: убран skip `id==0` для Player, second-chance recovery.
> **v0.1.3** (2026-06-25, сессия #7) — fix: `EnsureUnarmedFallback()` (DefaultDamageSource когда нет оружия).
> **v0.1.4** (2026-06-25, сессия #8) — fix: corpse delay `Destroy(gameObject, 3.0f)`.

---

## v0.0 — 2026-06-25 (до кода)

- Создан `docs/Character/Skills/real-time-combat/{00_README,01_ANALYSIS,02_LORE,10_DESIGN,20_TECHNICAL,30_SCENARIOS,30_PITFALLS,40_REFERENCES}.md`.
- Приняты 16 решений пользователя (см. `30_PITFALLS §3`).
- План реализации: `docs/dev/COMBAT_ENGINE_IMPL_PLAN.md`.

---

## v0.1 — 2026-06-25 (MVP)

### Сессия #1 — Фундамент (T-RTC01, T-RTC04, T-RTC05)

**Созданы 10 файлов:**

```
Assets/_Project/Scripts/Combat/Core/
├── IAttacker.cs          (interface, generic abstraction)
├── IDamageTarget.cs      (interface, generic abstraction)
├── IDamageSource.cs      (interface, generic abstraction)
├── IRangePolicy.cs       (interface, distance check strategy)
├── DamageType.cs         (enum DamageType + DamageDice + extensions: Roll/Average/ArmorMultiplier)
└── DamageResult.cs       (POCO struct, server-authoritative, не сериализуется)

Assets/_Project/Scripts/Combat/Implementations/
├── DefaultDamageSource.cs  (fallback: d6, base=1, critMod=0, range=2м, type=Physical, cd=1s)
├── MeleeRangePolicy.cs     (baseMelee=0.85, dexMod=0.85+(DEX-10)*0.015)
└── RangedRangePolicy.cs    (baseRanged=0.75, аналогичный dexMod)

Assets/_Project/Scripts/Combat/
└── DamageCalculator.cs    (static, ERPR-формула, server-authoritative)
```

**Решения:**
- Namespace: `ProjectC.Combat.Core` (абстракции) + `ProjectC.Combat` (формула, реализации).
- ERPR per `Battle/10_DESIGN.md §7` + real-time override (locMult=1.0, hitLocation=1, skillMult=1.0).
- hitChance per answer 2.1: `dexMod = 0.85 + (DEX-10)*0.015`, `baseMelee=0.85`, `baseRanged=0.75`.

**Verify:** compile clean, 0 errors.

---

### Сессия #2 — Server+Client интеграция (T-RTC02, T-RTC03, T-RTC06, T-RTC07, T-RTC08, T-RTC09)

**Созданы 11 файлов:**

```
Assets/_Project/Scripts/Combat/
├── Config/CombatConfig.cs               (SO: hit/crit/defense multipliers, designer-tunable)
├── Implementations/
│   ├── PlayerAttacker.cs                (MonoBehaviour, IAttacker — в v0.1 стал NetworkBehaviour)
│   ├── PlayerTarget.cs                  (NetworkBehaviour, IDamageTarget)
│   ├── NpcAttacker.cs                   (MonoBehaviour, IAttacker — в v0.1 стал NetworkBehaviour)
│   ├── NpcTarget.cs                     (NetworkBehaviour, IDamageTarget)
│   ├── NpcCombatData.cs                 (SO: HP, STR/DEX/INT, weapon defaults)
├── Network/
│   ├── CombatServer.cs                  (NetworkBehaviour, RPC hub, registries, cooldowns, rate limit)
│   └── DamageResultDto.cs               (INetworkSerializable struct)
└── Client/CombatClientState.cs          (singleton MonoBehaviour, event-bus, debug-логи)
```

**Add-only правки в существующих файлах:**

| Файл | Что |
|---|---|
| `Core/WorldEvent.cs` | +4 event-класса: `AttackStartedEvent`, `AttackLandedEvent`, `DamageDealtEvent`, `EntityKilledEvent` |
| `Scripts/Core/NetworkManagerController.cs` | +`CreateCombatClientState()` (root GO, DontDestroyOnLoad) |
| `Scripts/Player/NetworkPlayer.cs` | +`using ProjectC.Combat;` + `RegisterWithCombatServer()` / `UnregisterFromCombatServer()` / `DebugAttackNearestNpc()` + K-key в `Update` |

**Решения:**
- `CombatServer` — singleton scene-placed, server-authoritative, RPC hub, registries (`_attackers`, `_targets`, `_cooldowns`, `_nextAllowedTime`).
- `CombatClientState` — singleton MonoBehaviour, `OnAttackLanded/OnDamageDealt/OnEntityKilled/OnAttackError` events.
- Rate limit 10 ops/sec per client (anti-spam).
- Cooldown централизован в `CombatServer` (per answer 2.3).
- `PlayerAttacker.RebuildSources()` — читает `EquipmentData.TryGetItemId(EquipSlot.WeaponMain/Off)`, fallback `DefaultDamageSource((ulong)itemId)`.
- `PlayerTarget.GetArmorDefense()` → 0 (T-CB06 не сделан).
- `PlayerAttacker.GetStrength/Dexterity/Intelligence` → `StatsWorld.GetOrCreateStats(_clientId).strengthTier*5+10` (default 10).
- `NpcAttacker/Target` — scene-placed NPC с `NpcCombatData` SO.

**Verify:** compile clean, 0 errors.

---

### Сессия #3 — Scene integration через MCP

**Созданы 2 SO assets** (через `execute_code` Edit Mode):
- `Assets/_Project/Resources/Combat/CombatConfig_Default.asset` (default values, не подключён к CombatServer)
- `Assets/_Project/Resources/Combat/Npc_Goblin.asset` (displayName="Goblin Test", maxHp=20, STR/DEX=10, INT=8, d6, base=2, range=2м, cooldown=1.5s)

**Scene edits:**

`BootstrapScene.unity`:
- Добавлен `[CombatServer]` GameObject (root, NetworkObject + CombatServer).

`WorldScene_0_0.unity`:
- Добавлен `NPC_TestEnemy` GameObject (NetworkObject + NpcAttacker + NpcTarget + child `VisualMarker` capsule).
- `VisualMarker` — Unity primitive Capsule, scale 0.8×1×0.8, pos +1м Y, URP Lit shader, color `(0.9, 0.15, 0.15)`, collider disabled.

**Debug key:** K в `NetworkPlayer.Update` → `DebugAttackNearestNpc()` → `CombatServer.RequestAttackRpc(targetId, 0)`.

**Verify:** scene integrity OK, 0 compile errors.

---

## v0.1.1 — 2026-06-25 (Playtest #1, fix race condition)

### Проблема

`NetworkPlayer.OnNetworkSpawn` (лог #71) сработал **раньше** `CombatServer.OnNetworkSpawn` (лог #87). `RegisterWithCombatServer` делал ранний `return` на `if (CombatServer.Instance == null)` — до `AddComponent<PlayerAttacker/PlayerTarget>`. Без компонентов — `OnNetworkSpawn` PlayerAttacker/Target не сработал (push-up нет). NPC спавнились, но Player не зарегистрировался.

### Fix

`NetworkPlayer.RegisterWithCombatServer` — убран ранний return. `AddComponent<PlayerAttacker/PlayerTarget>` ВСЕГДА, регистрация в `CombatServer.Instance` — только если не null. Push-down в `CombatServer.OnNetworkSpawn → RecoverExistingEntities` страхует.

### Результат

После v0.1.1: `PlayerAttacker=True, PlayerTarget=True` компоненты есть. Но всё ещё `attackers=1, targets=1` (только NPC, Player не зарегистрирован — пуш-даун не нашёл).

---

## v0.1.2 — 2026-06-25 (Playtest #2, fix host clientId)

### Проблема

`RecoverExistingEntities` имел `if (id == 0) continue;` для **Player** — но `0` это валидный `clientId` для **host player** (server's own client). Skip отфильтровывал host.

Также: `PlayerAttacker.IsSpawned = False` на момент `RecoverExistingEntities` (NetworkObject ещё не spawned'ился) — push-down в OnNetworkSpawn не нашёл его вовремя.

### Fix (2 правки)

1. **`CombatServer.RecoverExistingEntities`**: убран `if (id == 0) continue;` для `PlayerAttacker/PlayerTarget` (0 = валидный host id). Оставлен для `NpcAttacker/NpcTarget` (там id==0 = не инициализирован).
2. **`CombatServer.OnNetworkSpawn`**: добавлен `Invoke(nameof(RecoverExistingEntities), 1.0f)` — second-chance recovery через 1 сек (для случая когда Player NetworkObject spawned'ится ПОЗЖЕ CombatServer).

**Также исправлено** (в процессе):
- Убрал сломанный `private void Start()` (NetworkBehaviour не имеет `Start()` virtual) — перенёс логику в `OnNetworkSpawn`.

### Результат

```
attackers=2, targets=2 (Player id=0 + Npc id=140956 / Npc id=45)
K-attack: targetId=45, dist=1,48м → работает!
```

---

## v0.1.3 — 2026-06-25 (Playtest #4, fix unarmed fallback)

### Проблема

`K-attack → InvalidSource` error. У host player на `Head` экипирована Рабочая каска, но **WeaponMain/WeaponOff пусты**. `PlayerAttacker.RebuildSources` → пустой `_activeSources`. `K` шлёт `sourceId=0` → `GetDamageSource(0)` → null → `InvalidSource`.

### Fix

`PlayerAttacker.EnsureUnarmedFallback()` — новый private метод. Если после `RebuildSources` список пуст, добавить `DefaultDamageSource(0UL, "Unarmed")`. Вызывается в конце `RebuildSources`.

```csharp
private void EnsureUnarmedFallback() {
    if (_activeSources.Count > 0) return;
    _activeSources.Add(new DefaultDamageSource(0UL, "Unarmed"));
}
```

### Результат

`K-attack → DamageCalculator: baseAttack=15, hitChance=0,79, isHit=True, preDefense=15, defense=0, final=15, type=Physical` ✓

---

## v0.1.4 — 2026-06-25 (Playtest #5, fix corpse delay)

### Проблема

После убийства NPC (`HP=0`) — `VisualMarker` (красная капсула) оставалась видимой. `NpcTarget` не скрывал GameObject при HP=0.

### Fix

`NpcTarget.ApplyDamage` — после `_currentHp.Value = 0` → `Destroy(gameObject, 3.0f)`. 3 сек corpse delay — клиенты успеют увидеть анимацию/feedback, `EntityKilledTargetRpc` дойдёт. После — `NetworkObject` NGO-удалится у всех клиентов автоматически.

```csharp
if (newHp == 0) {
    if (Debug.isDebugBuild) Debug.Log($"[NpcTarget] npc={_targetId} killed. Destroying in 3s (corpse delay).");
    Destroy(gameObject, 3.0f);
}
```

### Результат

NPC умирает → 3 сек → капсула пропадает. ✓

---

## Summary таблица

| Версия | Дата | Что | Файлов |
|---|---|---|---|
| v0.0 | 2026-06-25 | Design doc (8 файлов .md) | 8 |
| v0.1 #1 | 2026-06-25 | Фундамент (T-RTC01, T-RTC04, T-RTC05) | 10 |
| v0.1 #2 | 2026-06-25 | Server+Client (T-RTC02, T-RTC03, T-RTC06, T-RTC07, T-RTC08, T-RTC09) + add-only в 3 файлах | 11 |
| v0.1 #3 | 2026-06-25 | Scene integration через MCP (2 SO, 2 scene edits) | 2 SO + 2 scenes |
| v0.1.1 | 2026-06-25 | Fix race: убран ранний return | 1 |
| v0.1.2 | 2026-06-25 | Fix: убран skip id==0, second-chance recovery | 1 |
| v0.1.3 | 2026-06-25 | Fix: EnsureUnarmedFallback | 1 |
| v0.1.4 | 2026-06-25 | Fix: corpse delay 3s | 1 |
| **ИТОГО** | | **21 файл .cs + 2 SO + 2 scenes + 3 add-only** | |

---

## Известные баги / TODO

| # | Что | Severity | Когда фиксить |
|---|---|---|---|
| T1 | `PlayerAttacker.IsSpawned = False` после spawn | cosmetic | Phase 2 (T-RTC10+ — возможно NGO bag с child NetworkBehaviour) |
| T2 | `NpcTarget._targetId = 0` на runtime (нужен fallback-init в OnNetworkSpawn) | ✅ уже исправлено в v0.1.3 для NPC | — |
| T3 | `CombatConfig` SO не подключён к CombatServer (hardcoded defaults) | low | Phase 2 — дизайнер-конфигурируемо |
| T4 | `armorDefense = 0` для всех (TODO в PlayerTarget) | low | T-CB06 |
| T5 | NPC не self-initialize через Edit Mode (нужен reflection в execute_code или runtime fallback) | low | ✅ исправлено v0.1.3 fallback-init |
| T6 | `PlayerAttacker/Target` не имеют NetworkVariable `_currentHp` сам по себе (наследуют от NetworkBehaviour без variables) | non-issue | — |
| T7 | K-key debug handler оставлен в NetworkPlayer.Update (временный код) | low | Удалить после T-RTC10 |
| T8 | `RpcAttribute.RequireOwnership` deprecation warning (consistent с NetworkPlayer.cs:1055) | non-issue | Refactor в отдельном тикете |
| T9 | `Object.GetInstanceID()` deprecation warning в NpcAttacker | non-issue | Refactor в отдельном тикете |
| T10 | `[CombatClientState]` не отписывается от `WorldEventBus` (нет подписок) | non-issue | — |

---

## Lessons learned (для будущих сессий)

1. **С самого начала делать push-down + pull-up + second-chance** для singleton-on-server pattern. Сразу в первом патче.
2. **Host player имеет clientId=0** — никогда не skip его в find-логике по `id==0`.
3. **`AddComponent` ДО проверки Instance**, не после. Иначе push-up никогда не сработает.
4. **NetworkBehaviour.OnNetworkSpawn** — лучше MonoBehaviour.Awake для self-registration в singleton (race-safe).
5. **Debug K-key** — оставлять в NetworkPlayer.Update как временный hook для verify, удалять в T-RTC10.
6. **Visual marker** — капсула-примитив с URP Lit, collider disabled, scale 0.8×1×0.8, pos +1м Y. Persistent scene edit.
7. **Destroy(gameObject, 3f)** — corpse delay для visual feedback перед NGO auto-destroy.
8. **`Invoke(nameof(recover), 1f)`** — second-chance recovery для race conditions, лучше чем Update polling.
