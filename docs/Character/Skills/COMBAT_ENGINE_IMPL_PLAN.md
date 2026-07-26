# Real-Time Combat Engine — Implementation Plan

> **Дата:** 2026-06-25 (start сессии)
> **Базируется на:** `docs/Character/Skills/real-time-combat/{00_README,10_DESIGN,20_TECHNICAL,30_PITFALLS}*.md`
> **Стратегия:** 3 этапа. На каждом этапе — отчёт + инструкция для пользователя (Play Mode).

---

## Стратегия сессии

| Этап | Тикеты | Файлы | Verify (что ты делаешь) |
|---|---|---|---|
| **A. Фундамент** | T-RTC01, T-RTC04, T-RTC05 | interfaces, enums, `DamageCalculator`, `DefaultDamageSource`, `MeleeRangePolicy`, `RangedRangePolicy` | Открыть Unity → 0 errors в Console |
| **B. Сервер + клиент** | T-RTC02, T-RTC03, T-RTC06, T-RTC07, T-RTC08, T-RTC09 | `PlayerAttacker/Target`, `NpcAttacker/Target`, `CombatServer`, `CombatClientState`, `DamageResultDto`, 4 events, hook в `NetworkManagerController` + `NetworkPlayer` | Открыть Unity → 0 errors → StartHost в Play Mode → проверить, что в Console: `[NMC] Created [CombatClientState]`, `[CombatServer] OnNetworkSpawn` |
| **C. Scene integration** | (manual) | scene-placed `[CombatServer]` GO в `BootstrapScene` + placeholder NPC в `WorldScene_0_0` | Запустить Play Mode, подойти к NPC, нажать ЛКМ → проверить damage log в Console |

**Между этапами** я даю тебе verify-команды, ты тестируешь в Play Mode, говоришь «ок» / пастишь ошибки. **Не** запускаю `run_tests` MCP, **не** коммичу.

---

## Этап A — Фундамент (T-RTC01, T-RTC04, T-RTC05)

### Файлы (5)

```
Assets/_Project/Scripts/Combat/Core/
├── IAttacker.cs
├── IDamageTarget.cs
├── IDamageSource.cs
├── IRangePolicy.cs
├── DamageType.cs          (enum + extension methods)
├── DamageResult.cs        (POCO struct)
├── DamageDice.cs          (enum)
└── (нет файла — внутри DamageType.cs)

Assets/_Project/Scripts/Combat/Implementations/
├── DefaultDamageSource.cs
├── MeleeRangePolicy.cs
└── RangedRangePolicy.cs

Assets/_Project/Scripts/Combat/
└── DamageCalculator.cs    (static)
```

### Что в каждом файле

**IAttacker.cs** (server + client readable):
- `Vector3 GetPosition()`
- `int GetStrength()`, `GetDexterity()`, `GetIntelligence()`
- `IReadOnlyList<IDamageSource> GetActiveDamageSources()`
- `IDamageSource GetDamageSource(ulong sourceId)`
- `bool IsAlive()`, `bool IsPlayer()`
- `bool CanAttack(IDamageSource source, float now)`, `void SetCooldown(IDamageSource source, float until)`
- `ulong GetClientId()` (для damage attribution)

**IDamageTarget.cs**:
- `Vector3 GetPosition()`
- `int GetCurrentHp()`, `GetMaxHp()`
- `int GetArmorDefense()`
- `void ApplyDamage(DamageResult result, ulong attackerClientId)` — server-side only
- `bool IsAlive()`, `bool IsPlayer()`
- `string GetDisplayName()`
- `ulong GetClientId()`

**IDamageSource.cs**:
- `ulong GetSourceId()`
- `DamageType GetDamageType()`, `DamageDice GetDamageDice()`
- `int GetBaseDamage()`, `GetCritModifier()`
- `float GetRange()`, `GetCooldownSeconds()`
- `float GetSkillMultiplier(ulong attackerId)` — MVP = 1.0
- `string GetDisplayName()`

**IRangePolicy.cs**:
- `bool IsInRange(IAttacker a, IDamageTarget t, IDamageSource s)`
- `float Distance(IAttacker a, IDamageTarget t)`
- `bool RequiresLineOfSight { get; }` — MVP false
- `float CalculateHitChance(IAttacker a, IDamageTarget t, IDamageSource s)`

**DamageType.cs** (enum + extensions):
- `enum DamageType : byte { Physical=0, Ballistic=1, Antigrav=2, Explosive=3, Mesium=4 }`
- `enum DamageDice : byte { d4=4, d6=6, d8=8, d10=10, d12=12, d20=20 }`
- `static class DamageDiceExtensions { Roll(); Average(); }`
- `static class DamageTypeExtensions { ArmorMultiplier(DamageType); }` — для формулы

**DamageResult.cs** (struct, server-authoritative, не NetworkSerialize на этом этапе — DTO в B):
- `baseAttack, locMult, critMult, skillMult, hitChance, preDefenseDamage, effectiveDefense, finalDamage`
- `isCrit, isHit`
- `byte hitLocation` (1=default в MVP, после 2.17 locMult=1.0)
- `DamageType damageType`
- `ulong attackerId, targetId, sourceId`
- `Vector3 attackerPosition, targetPosition`
- `static DamageResult Miss(...)` — helper

**DefaultDamageSource.cs** (fallback до T-CB03):
- `d6, base=1, critMod=0, range=2m, type=Physical`
- `GetSkillMultiplier() = 1.0f`
- `GetCooldownSeconds() = 1.0f`
- `GetSourceId() = 0` (placeholder)

**MeleeRangePolicy.cs**:
- `IsInRange`: `dist <= source.range + 0.5`
- `Distance`: `Vector3.Distance`
- `RequiresLineOfSight = false`
- `CalculateHitChance` (per answer 2.1): `0.85 * dexMod * distMod`, где `dexMod = 0.85 + (DEX-10)*0.015` (clamp 0..1.0)

**RangedRangePolicy.cs**:
- `IsInRange`: `dist <= source.range`
- `Distance`: `Vector3.Distance`
- `RequiresLineOfSight = false`
- `CalculateHitChance` (per answer 2.1): `0.75 * dexMod * (1 - dist/range)`, clamp 0..1.0

**DamageCalculator.cs** (static, server-authoritative):
- Принимает `(IAttacker, IDamageTarget, IDamageSource, IRangePolicy, CombatConfig?)` — config опционально
- Шаги: baseAttack → hitChance → isHit → (if miss return Miss) → locMult=1.0 → crit → skillMult → preDefense → defense → final
- `static DamageResult Miss(...)` helper
- `static DamageResult Calculate(...)` — full

### Namespace
- `ProjectC.Combat.Core` — interfaces, enums, DamageResult
- `ProjectC.Combat` — DamageCalculator, CombatServer, CombatClientState, implementations
- Это разделение даёт ясность: core = переиспользуемые абстракции, namespace Combat = конкретный engine.

### Verify (Этап A)
1. **Compile:** `Window → General → Console` в Unity → 0 errors.
2. Damage-формула sanity: `[DamageCalculator] Damage log: baseAttack=17, isHit=true, critMult=1.0, skillMult=1.0, preDefense=17, defense=0, final=17` — лог через `Debug.Log` при первом `Calculate()` вызове (опционально, можно без).

---

## Этап B — Серверная интеграция (T-RTC02, T-RTC03, T-RTC06, T-RTC07, T-RTC08, T-RTC09)

### Файлы (~10)

```
Assets/_Project/Scripts/Combat/Implementations/
├── PlayerAttacker.cs     (MonoBehaviour, IAttacker)
├── PlayerTarget.cs       (NetworkBehaviour, IDamageTarget)
├── NpcAttacker.cs        (MonoBehaviour, IAttacker)
├── NpcTarget.cs          (NetworkBehaviour, IDamageTarget)
└── NpcCombatData.cs      (ScriptableObject — placeholder для NPC)

Assets/_Project/Scripts/Combat/Network/
├── CombatServer.cs       (NetworkBehaviour, scene-placed в BootstrapScene)
└── DamageResultDto.cs    (INetworkSerializable)

Assets/_Project/Scripts/Combat/Client/
└── CombatClientState.cs  (singleton MonoBehaviour)

Assets/_Project/Scripts/Combat/Config/
└── CombatConfig.cs       (ScriptableObject — настройки)
```

### Изменения в существующих файлах (add-only)

**`Assets/_Project/Core/WorldEvent.cs`** — добавить 4 event-класса:
- `AttackStartedEvent` (attackerId, targetId, sourceId)
- `AttackLandedEvent` (DamageResult result)
- `DamageDealtEvent` (DamageResult result)
- `EntityKilledEvent` (DamageResult result)

**`Assets/_Project/Scripts/Core/NetworkManagerController.cs`** — в `Awake()` добавить вызов `CreateCombatClientState()` по аналогии с другими. И сама функция по тому же паттерну.

**`Assets/_Project/Scripts/Player/NetworkPlayer.cs`** — в `OnNetworkSpawn` (после `if (GetComponent<NetworkPlayerSpawner>() != null) return;` guard) добавить add-only блок: при `IsServer` создать `PlayerAttacker` + `PlayerTarget` (через `AddComponent` или `GetOrAddComponent`?) и зарегистрировать в `CombatServer.Instance`. В `OnNetworkDespawn` — unregister.

**⚠️ Важно:** NetworkPlayer очень большой файл. Изменения — **add-only** в конце `OnNetworkSpawn/OnNetworkDespawn`, **никаких** других правок.

### Что в каждом новом файле

**PlayerAttacker.cs** (server-side registered):
- `private ulong _clientId;` после `Initialize(clientId)`
- `private List<IDamageSource> _activeSources = new();`
- `RebuildSources()`: читает `EquipmentWorld.GetEquipment(_clientId)`, для каждого `WeaponMain/Off` слот пытается найти `WeaponItemData`. До T-CB03 — fallback `DefaultDamageSource`.
- `GetStrength/Dexterity/Intelligence` через `StatsWorld.GetOrCreateStats(_clientId)`: `tier * 5 + 10` (default tier=0 → 10, как в дизайне).
- Cooldown — хранить в `Dictionary<(ulong, ulong), float>` (per-attacker+source) внутри `CombatServer` (а не в PlayerAttacker — централизованно, ответ 2.3).

**PlayerTarget.cs** (NetworkBehaviour, IDamageTarget):
- `NetworkVariable<int> _currentHp` (default 20) + `_maxHp` (default 20)
- `GetArmorDefense()`: sum `armorDefense` всех экипированных `ClothingItemData`. **⚠️ Поля `armorDefense` в `ClothingItemData` пока нет** (T-CB06). Решение: использовать `armorDefense` через `EquipmentWorld.GetEquipStatBonuses` косвенно? Нет — там только STR/DEX/INT. Решение: пока `GetArmorDefense() => 0` с комментарием, после T-CB06 — реальный подсчёт.
- `ApplyDamage()`: server-side, `_currentHp.Value = Max(0, _currentHp - result.finalDamage)`. Если HP=0 → CombatServer broadcast.
- `GetPosition() => transform.position` (или через NetworkPlayer если inShip).

**NpcAttacker.cs / NpcTarget.cs / NpcCombatData.cs**:
- `NpcCombatData` (SO) — placeholder: `maxHp`, `strength/dexterity/intelligence` (int), `cooldownSeconds`, `defaultDamageType/Dice/BaseDamage/CritModifier/Range` (для простоты — дефолтные d6, base=1, range=2m, Physical).
- `NpcAttacker` — читает `NpcCombatData`, реализует `IAttacker` аналогично `PlayerAttacker`, но `IsPlayer() => false`. Cooldown — per-NPC `float _lastAttackTime`.
- `NpcTarget` — `NetworkBehaviour` с `NetworkVariable<int> _currentHp`, реализует `IDamageTarget` аналогично `PlayerTarget`, но `IsPlayer() => false`.

**CombatServer.cs** (NetworkBehaviour, scene-placed):
- Singleton `Instance` (как `StatsServer`).
- `Dictionary<ulong, IAttacker> _attackers`
- `Dictionary<ulong, IDamageTarget> _targets`
- `Dictionary<(ulong, ulong), float> _cooldowns` (per 2.3 — централизованно в CombatServer)
- `Dictionary<ulong, float> _nextAllowedTime` (rate limit 10 ops/sec)
- `OnNetworkSpawn`: if `IsServer` — `Instance = this`.
- `[Rpc(SendTo.Server, RequireOwnership = true)] RequestAttackRpc(ulong targetNetId, ulong sourceId, RpcParams)`
- `ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId)`:
  - Lookup `attacker = _attackers[attackerId]`, `target = _targets[targetId]`
  - Validate: `target.IsAlive()`, `attacker.CanAttack(source, now)`, `rangePolicy.IsInRange(...)`
  - `var rangePolicy = GetRangePolicy(source)` — определяет melee vs ranged по source (например, по `source.GetRange() < 3.0f` = melee, иначе ranged)
  - `var hitChance = rangePolicy.CalculateHitChance(...)`
  - `var result = DamageCalculator.Calculate(attacker, target, source, rangePolicy)` — server rolls dice
  - `attacker.SetCooldown(source, now + source.GetCooldownSeconds())`
  - If `result.isHit && target.IsPlayer() == false` OR hit: `target.ApplyDamage(result, attackerId)`
  - Broadcast `AttackLandedTargetRpc(DamageResultDto.FromResult(result), RpcTarget.Everyone)` — all clients
  - Publish `AttackLandedEvent`, `DamageDealtEvent`, `EntityKilledEvent`
- `[Rpc(SendTo.SpecifiedInParams)] AttackLandedTargetRpc(DamageResultDto dto, RpcParams rpc)` — клиент вызывает `CombatClientState.HandleAttackLanded(dto)`
- `RegisterAttacker(id, IAttacker)`, `RegisterTarget(id, IDamageTarget)`, `UnregisterAttacker/Target(id)`

**DamageResultDto.cs** (INetworkSerializable):
- Все поля `DamageResult`, сериализация через `BufferSerializer<T>`.

**CombatClientState.cs** (singleton MonoBehaviour):
- Создаётся в `NetworkManagerController.CreateCombatClientState()` как root GO с `DontDestroyOnLoad`.
- Events: `OnAttackLanded, OnDamageDealt, OnEntityKilled, OnOutOfRange` (Action<DamageResult> и т.п.)
- `HandleAttackLanded(DamageResultDto dto)`: convert → DamageResult, fire event, `Debug.Log` для verify.

**CombatConfig.cs** (ScriptableObject):
- `baseMeleeHitChance = 0.85f`
- `baseRangedHitChance = 0.75f`
- `dexHitMultiplier = 0.015f` (per 2.1)
- `baseCritThreshold = 100`
- `critMultiplier = 2.0f`
- `antigravArmorMult = 0.5f, explosiveArmorMult = 0.7f, mesiumArmorMult = 0.0f`
- `serverTickRate = 30`
- Создадим default asset позже (в Этапе C).

### Verify (Этап B)
1. **Compile:** `Window → General → Console` → 0 errors.
2. **StartHost в Play Mode:** в Scene `BootstrapScene` → нажать кнопку "Start Host" (или Play → StartHost).
3. **В Console должны появиться:**
   - `[NMC] Created [CombatClientState] as root GameObject` (после моих изменений NetworkManagerController).
   - `[CombatServer] OnNetworkSpawn: Instance set, IsServer=True` — НЕТ, потому что CombatServer не scene-placed ещё (Этап C). Это ожидаемо.
4. **PlayerAttacker регистрация:** после спавна игрока должно быть `[CombatServer] Registered attacker for clientId=X` (добавлю Debug.Log в RegisterAttacker при первом вызове).

---

## Этап C — Scene integration (manual + tests)

### Шаги (после Этапа B compiles)

1. **Создать `Assets/_Project/Resources/Combat/CombatConfig_Default.asset`** (через меню Create → Project C → Combat → Combat Config) — стандартные значения из дизайна.
2. **Scene-placed `[CombatServer]` GameObject** в `BootstrapScene`:
   - Уже scene-placed: `[StatsServer]`, `[SkillsServer]`, `[EquipmentServer]`. Создать рядом `[CombatServer]` с компонентами `NetworkObject` + `CombatServer`.
3. **Placeholder NPC** в `WorldScene_0_0`:
   - Создать GameObject `NPC_TestEnemy` с компонентами `NetworkObject`, `NpcAttacker`, `NpcTarget`.
   - `NpcAttacker` — ссылка на `NpcCombatData` (создадим SO `Npc_Goblin.asset` с дефолтными статами).
4. **Test LKM attack** (Phase 2 — UI; для Этапа C — простой debug-trigger):
   - Добавить временный debug-key (например, `K`) в `NetworkPlayer.Update` (add-only) → `CombatServer.Instance.RequestAttackRpc(NetworkObjectId ближайшего NPC, sourceId=0)`. **Только** для теста движка. В финале уберём или заменим на input binding.

### Verify (Этап C)
1. **StartHost** → `[ScenePlacedObjectSpawner] Scene (0,0): spawned=N` (без изменений) + `[CombatServer] OnNetworkSpawn: Instance set, IsServer=True`.
2. **Spawn player** → `[CombatServer] Registered attacker for clientId=0`, `[CombatServer] Registered target for clientId=0`.
3. **Нажать K** (debug) → в Console:
   - `[CombatServer] ResolveAttack: clientId=0 → npcId=X, source=DefaultDamageSource`
   - `[DamageCalculator] baseAttack=Y, isHit=true, preDefense=Z, defense=0, final=W`
   - `[CombatServer] BroadcastAttackLanded: finalDamage=W, targetId=X`
   - `[CombatClientState] HandleAttackLanded: damage=W`
4. **Спамить K**: после первого удара — `[CombatServer] OnCooldown: clientId=0, source=DefaultDamageSource` (cooldown 1 сек).

---

## Open / риски (зафиксировано)

| # | Риск | Mitigation |
|---|---|---|
| R-A1 | `armorDefense` в `ClothingItemData` нет (T-CB06) | MVP `PlayerTarget.GetArmorDefense() => 0` + TODO-комментарий. После T-CB06 — реальный подсчёт. |
| R-A2 | `WeaponItemData` нет (T-CB03) | `DefaultDamageSource` fallback с дефолтами (d6, base=1, critMod=0, range=2m). |
| R-A3 | `NetworkPlayer` очень большой (1456 строк), patch-инструмент может сломать indent | Использую `write_file` для точечных вставок, если patch провалится. |
| R-A4 | `CombatServer` scene-placed вручную — если в сцене уже есть NetworkObject с тем же `GlobalObjectIdHash` → конфликт | Уникальное имя GO + `NetworkObject` AutoGenerateGlobalObjectIdHash (Unity делает сам). |
| R-A5 | Первый `Refresh_unity` после новых скриптов может дать 100 ошибок о `type not found` | Соблюдаю порядок: создаю файлы → `refresh_unity` → `read_console` — последовательно, не batch. |
| R-A6 | `IRangePolicy` нужен в `DamageCalculator` — но `GetRangePolicy(source)` = где? | В `CombatServer.ResolveAttack` (там уже есть `source`). `DamageCalculator.Calculate` принимает `IRangePolicy` параметром. |

---

## Что НЕ делаем в этой сессии

- ❌ UI damage numbers (T-RTC10, Phase 2).
- ❌ Line-of-sight (Phase 2).
- ❌ Client prediction (Phase 2).
- ❌ PvP duel (T-RTC11..15, Phase 2).
- ❌ Ship combat (T-RTC16..20, Phase 3).
- ❌ NPC-AI (отдельная подсистема).
- ❌ `WeaponItemData` / `armorDefense` поля (T-CB03, T-CB06 — отложены в MVP+1).
- ❌ `CombatConfig_Default.asset` через Editor-tool — создадим вручную в Этапе C.
- ❌ EditMode/PlayMode tests (no asmdef).
- ❌ git commit / push.
