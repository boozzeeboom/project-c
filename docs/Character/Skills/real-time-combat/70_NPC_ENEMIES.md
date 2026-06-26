# NPC Enemies — пешие враждебные мобы (пешая фаза)

> **Дата:** 2026-06-26 (v0.4 design + v0.1.4 implementation context)
> **Статус:** 📝 **Дизайн + roadmap**, код НЕ написан. Реализация — следующая сессия.
> **Контекст:** Combat-движок (T-RTC01..T-RTC09) + Skills (T-CB03..08) — ✅ работает. NPC-враги принимают урон и умирают (corpse 3 сек, v0.1.4). **Что отсутствует** — спавн в мире, AI-движение, death-loot, визуал + анимация.
> **Связанные документы:** `00_README.md` (combat), `10_DESIGN.md` §3.3 NpcAttacker, `20_TECHNICAL.md` (CombatServer).

---

## 1. Текущее состояние (что есть)

| Компонент | Файл | Статус |
|---|---|---|
| `NpcAttacker : NetworkBehaviour, IAttacker` | `Combat/Implementations/NpcAttacker.cs` | ✅ Реализован (T-RTC03) — может атаковать через CombatServer, имеет cooldown |
| `NpcTarget : NetworkBehaviour, IDamageTarget` | `Combat/Implementations/NpcTarget.cs` | ✅ Реализован (T-RTC03 + v0.1.4) — HP, death corpse delay 3 сек |
| `NpcCombatData : ScriptableObject` | `Combat/Implementations/NpcCombatData.cs` | ✅ Минимальный — HP, STR/DEX/INT, оружие defaults (d6/base=2/Physical/range=2м) |
| 1 SO `Npc_Goblin.asset` | `Resources/Combat/` | ✅ Создан (maxHp=20, defaults) |
| 1 scene-placed `NPC_TestEnemy` | `WorldScene_0_0` | ✅ Создан вручную через execute_code (NetworkObject + NpcAttacker + NpcTarget + красная капсула-маркер) |
| `NPC_AI` / movement / spawn / loot | — | ❌ **Не реализовано** — это скоуп данного документа |

**Что умеет NPC сейчас:** стоять на месте (визуально невидимый без mesh, кроме `VisualMarker` capsule), принимать damage, умирать через 3 сек (Destroy). **Не умеет:** двигаться, атаковать игрока (AI), спавниться в мире, выдавать loot, иметь нормальный визуал.

---

## 2. Что нужно сделать (минимум для MVP)

### 2.1 Спавн в радиусе вокруг игрока
- `NpcSpawner : NetworkBehaviour` — сервер-сайд singleton (scene-placed в `BootstrapScene` рядом с `[CombatServer]`).
- Читает `NpcSpawnerConfig : ScriptableObject`:
  - `prefab: NetworkObject` — префаб NPC-врага.
  - `spawnRadius: float = 60m` — от игрока.
  - `maxAliveCount: int = 5` — лимит одновременно живых NPC.
  - `spawnInterval: float = 4f` — сек между проверками спавна.
  - `spawnChance: float = 0.5f` — вероятность спавна за проверку.
  - `difficultyMultiplier: AnimationCurve` — от расстояния до игрока (ближе = сильнее).
- Каждые `spawnInterval` сек сервер:
  1. Найти ближайшего игрока (NetworkPlayer среди ConnectedClients).
  2. Если alive count < max — выбрать случайную точку в радиусе (на поверхности через `Physics.Raycast` вниз от `player.pos + Random.insideUnitSphere * spawnRadius.y`).
  3. Validate: точка не в воздухе, не в воде, не слишком близко к другому NPC/игроку.
  4. Spawn prefab через `Instantiate + NetworkObject.Spawn()`.
- Hook в `NetworkManagerController.Awake` (как `[CombatServer]`).

### 2.2 Нормальная смерть (animation → loot → despawn)
- В `NpcTarget.ApplyDamage` после `newHp == 0` (вместо текущего `Destroy(gameObject, 3.0f)`):
  1. Проиграть **death animation** через `NpcAnimator.SetTrigger("Death")` (длительность ~2 сек).
  2. По окончании death animation — **spawn LootChest** на месте смерти:
     - `NetworkChestContainer` с `LootTable` (создать `LootTable_Goblin.asset` с шансом кредитов + редкий дроп).
     - Кредиты: через `IPlayerDataRepository.AddCredits(playerId, Random.Range(min, max))` (но `LootTable` сейчас не поддерживает credits — нужна extension).
     - **MVP**: кладём только кредиты через **ServerSpawn** нового `CreditLootEntity` или добавляем поле `credits` в `LootTable` (см. §3).
  3. Despawn NPC через `NetworkObject.Despawn(true)` (вызывает Destroy).
- **Fallback:** если игрок не лутает chest — despawn через 30 сек (existing `ChestContainer` уже имеет fallback на `autoDestroy`).

### 2.3 Tree behaviour AI (ожидание → преследование → атака → группирование)
- `NpcBrain : NetworkBehaviour` (на том же GO что NpcAttacker/NpcTarget).
- Реализует **Finite State Machine** (server-side only):
  ```
  [Idle] --player in AggroRange (10m)--> [Chase]
  [Chase] --dist <= AttackRange (2m)--> [Attack]
  [Chase] --dist > LeashRange (40m)--> [Idle]  (return to spawn)
  [Attack] --cooldownElapsed + dist<=AttackRange--> [Attack]
  [Any] --HP<=0--> [Dead]  (handed by NpcTarget)
  ```
- Update loop (server-side, ~30 Hz):
  - Найти ближайшего `IDamageTarget` среди `NetworkPlayer` в радиусе AggroRange.
  - Если найден → `Chase` (через `NavMeshAgent.SetDestination(player.pos)`).
  - Если dist <= AttackRange → `Attack` → `CombatServer.Instance.RequestAttackRpc(playerNetId, sourceId=0)`.
- **Группирование (grouping)**: NPC одного типа помогают друг другу. Если NPC_A в `Attack` состоянии — все NPC_A в радиусе 15м агрятся на ту же цель (агрессия заразна). Реализуется через shared `AggroTable` в `NpcSpawner` (server-side Dictionary<ulong targetId, List<NpcBrain>>).

### 2.4 Визуал + анимация (Kevin Iglesias Human Animations)
- **Префаб NPC:** `Assets/_Project/Prefabs/AI/Npc_Goblin.prefab` (server-authoritative spawn):
  - root: `NetworkObject` + `CharacterController` + `NpcBrain` + `NpcAttacker` + `NpcTarget`.
  - child `Visual`: `HumanM_Model.fbx` (Male, generic) + `Animator` + `NpcAnimatorController` (создаём новый):
    - **Layers:** Base Layer (Locomotion: Idle/Walk/Run/Death) + Upper Layer (Combat: Attack).
    - **Parameters:** `Speed` (float, 0..6), `IsAttacking` (bool), `Death` (trigger).
    - **States:** Idle (HumanM@Idle01), Walk (HumanM@Walk01, Speed 0..0.5), Run (HumanM@Run01, Speed 0.5..6), Attack (HumanM@Attack01, triggered), Death (HumanM@Death01, once, no exit).
  - child `VisualMarker` (опционально): маленькая красная точка для debug (как сейчас).
- **Anti-restrictive визуал:** `NpcVisualConfig : ScriptableObject` — `[MeshFilter.mesh, MeshRenderer.material]` ссылки для quick-swapping без редактирования префаба (placeholder cubes → final model в будущем).

### 2.5 Animator Controller (новый, server-side)
- `Assets/_Project/Animation/AI/NpcAnimatorController.controller` (создать вручную в Editor или через EditorTool).
- Это **третья** версия контроллера: первая — `HumanBasicMotionsScene` (Kevin Iglesias), вторая — `[CharacterWindow]/Skills Animator`, третья — **AI NPC Animator**.
- Параметры: `Speed` (float, [0..6]), `IsAttacking` (bool), `Death` (trigger).
- State machine — см. §2.4.

---

## 3. Дополнительный скоуп (что я считаю нужным для MVP)

### 3.1 LootTable с поддержкой кредитов
Текущий `LootTable : ScriptableObject` поддерживает только `ItemData`. **Нужно расширить:**
```csharp
[Header("Credits drop (T-NPC-04)")]
[Range(0, 10000)] public int minCredits = 10;
[Range(0, 10000)] public int maxCredits = 50;
[Tooltip("Multiplier от difficulty (для scaling в зависимости от зоны)")]
public float creditsMultiplier = 1.0f;
```
- `LootTable.GenerateLoot()` → возвращает `(List<ItemData> items, int credits)`.
- `NetworkChestContainer` принимает credits и через `CombatServer`/`LootServer`/`ChestServer` начисляет их `Repository.AddCredits(playerId, credits)`.
- **Альтернатива MVP:** loot для NPC — только кредиты (без items). Кладём через **отдельный entity** `NpcLootPickup` с `itemId=0 + credits`.

### 3.2 NavMeshAgent vs CharacterController
- **Kevin Iglesias Animations** не имеют root motion. Два варианта movement:
  - **A. NavMeshAgent** (server-authoritative, простой для server-side AI). Replicates position через `NetworkTransform`.
  - **B. CharacterController + сервер-side physics.Move** (как у `NetworkPlayer`). Больше кода, лучший контроль.
- **Рекомендация MVP:** **NavMeshAgent** (быстрее, проверенный pattern). Минусы: требует baked NavMesh на террейне. В тестовой сцене `WorldScene_0_0` есть ground, можно запечь.

### 3.3 Anti-restrictive: NpcSpawner не блокирует Player flow
- `NpcSpawner` — **opt-in** через `[Header("Spawn Config")]` в `BootstrapScene`.
- Если `prefab = null` — spawner no-op (не спавнит ничего, не падает).
- Если `enabled = false` — отключается целиком (manual control).

### 3.4 Multi-scene spawning (chunk-based)
- `ChunkNetworkSpawner` уже существует в `World/Streaming/`. **Hook в него:** `ChunkNetworkSpawner.OnChunkLoaded` → `NpcSpawner.TrySpawnForChunk(chunkId)`.
- Это даёт **infinite scaling** — NPC появляются только в loaded chunks.

### 3.5 Combat XP + Reputation rewards
- При убийстве NPC → publish `EntityKilledEvent` (уже есть в CombatServer).
- Подписчики: `StatsServer` (combat XP), `QuestServer` (kill objectives), `ReputationServer` (faction attitude).
- **В scope**: ничего не нужно менять — events уже publish'ятся в v0.1.4. Достаточно **подписчиков** добавить в StatsServer/QuestServer/ReputationServer.

---

## 4. Скоуп тикетов (T-NPC-01..T-NPC-10)

| Тикет | Название | Файлы | Оценка |
|---|---|---|---|
| **T-NPC-01** | `NpcBrain` MonoBehaviour (FSM: Idle/Chase/Attack/Dead) | `Assets/_Project/Scripts/AI/NpcBrain.cs` | ~2-3 ч |
| **T-NPC-02** | `NpcSpawner` NetworkBehaviour + `NpcSpawnerConfig` SO | `AI/NpcSpawner.cs`, `AI/NpcSpawnerConfig.cs` | ~2 ч |
| **T-NPC-03** | `NpcLootPickup` (кредиты-only в MVP) | `AI/NpcLootPickup.cs` | ~1 ч |
| **T-NPC-04** | `LootTable` extension: credits (min/max + multiplier) | edit `Items/LootTable.cs` | ~30 мин |
| **T-NPC-05** | `NpcVisualConfig` SO + `NpcVisualApplier` компонент | `AI/NpcVisualConfig.cs`, `AI/NpcVisualApplier.cs` | ~1.5 ч |
| **T-NPC-06** | `NpcPrefab` (HumanM_Model + CharacterController + NavMeshAgent + NpcBrain + NpcAttacker + NpcTarget) | `Prefabs/AI/Npc_Goblin.prefab` (Editor + Roslyn) | ~1 ч |
| **T-NPC-07** | `NpcAnimatorController` (Speed / IsAttacking / Death states) | `Animation/AI/NpcAnimatorController.controller` (Editor) | ~1 ч |
| **T-NPC-08** | Scene-placed `NpcSpawner` в `BootstrapScene` + `Npc_Goblin.prefab` reference | scene edit | ~30 мин |
| **T-NPC-09** | `NpcSpawner` ↔ `ChunkNetworkSpawner` integration (chunk-based) | edit `World/Streaming/ChunkNetworkSpawner.cs` | ~1 ч |
| **T-NPC-10** | Hook в `NetworkManagerController.Awake` (CreateNpcSpawner) | edit `Core/NetworkManagerController.cs` | ~30 мин |

**Total estimate:** ~11-13 ч (2-3 сессии).

**После T-NPC-01..10** (verify):
- Play Mode → StartHost → NPC спавнятся вокруг игрока.
- NPC бегают за игроком (Chase).
- NPC атакуют вблизи (Attack → damage log → player HP ↓).
- Player может убежать → NPC возвращается в Idle.
- Player убивает NPC → death animation → loot chest → credits.
- NPC спавнятся каждые 4 сек, max 5 одновременно.
- Подписки на `EntityKilledEvent` → combat XP + reputation gain.

---

## 5. Что я предлагаю реализовать первым (минимальный MVP)

| Приоритет | Тикеты | Что даёт |
|---|---|---|
| **P0 (must-have)** | T-NPC-01 + T-NPC-02 + T-NPC-06 + T-NPC-07 + T-NPC-08 | NPC спавнятся, бегают за игроком, атакуют. **Без loot, без death anim** (просто Destroy через 0.5 сек). ~6-7 ч |
| **P1 (nice-to-have)** | T-NPC-04 + T-NPC-03 + T-NPC-10 | Death animation + loot (credits) + CombatServer.OnSpawn hook. ~2-3 ч |
| **P2 (production)** | T-NPC-05 + T-NPC-09 | Anti-restrictive визуал (swap models) + chunk-based spawning. ~2-3 ч |

**P0** даёт рабочий combat-loop с живыми NPC-врагами. **P1** — death + loot (требуется для XP/rep). **P2** — масштабирование и polish.

---

## 6. Архитектурные решения (open questions)

### Q1: Animator root motion vs root motion off?
**Решение MVP:** root motion OFF. `NavMeshAgent` двигает root → `Animator.SetFloat("Speed", navMesh.velocity.magnitude)` синхронизирует animation. Без root motion — проще репликация через `NetworkTransform`.

### Q2: NPC агрятся на других NPC (faction / friendly fire)?
**Решение MVP:** `NpcBrain` ищет только `IDamageTarget` где `IsPlayer()=true`. Friendly fire между NPC — **disabled** в MVP. После T-NPC-11 (faction system) — добавить.

### Q3: NPC despawn при потере игрока?
**Решение MVP:** да, через `NpcSpawner.CheckLeash` — если dist от spawn-point > 100м → Despawn. Cleanup race-free через server-authority.

### Q4: NavMesh requirement?
**Решение MVP:** да. `NpcSpawner` требует baked NavMesh в сцене. Если нет — `NavMesh.SamplePosition` fail → spawn skip с warning. Дизайнеры должны bake `WorldScene_0_0` перед тестом.

---

## 7. Файлы, которые будут добавлены/изменены

### Новые файлы (~10):
- `Assets/_Project/Scripts/AI/NpcBrain.cs` — FSM AI (T-NPC-01)
- `Assets/_Project/Scripts/AI/NpcSpawner.cs` — server-side spawner (T-NPC-02)
- `Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs` — SO config (T-NPC-02)
- `Assets/_Project/Scripts/AI/NpcLootPickup.cs` — credits pickup (T-NPC-03)
- `Assets/_Project/Scripts/AI/NpcVisualConfig.cs` — SO for mesh/material (T-NPC-05)
- `Assets/_Project/Scripts/AI/NpcVisualApplier.cs` — runtime swap (T-NPC-05)
- `Assets/_Project/Prefabs/AI/Npc_Goblin.prefab` — root NPC (T-NPC-06)
- `Assets/_Project/Animation/AI/NpcAnimatorController.controller` — animator (T-NPC-07)
- `Assets/_Project/Resources/AI/NpcSpawner_Default.asset` — default config (T-NPC-02)
- `Assets/_Project/Resources/AI/LootTable_Goblin.asset` — credits loot (T-NPC-04)

### Изменённые файлы (~3):
- `Assets/_Project/Scripts/Items/Core/LootTable.cs` — credits field (T-NPC-04)
- `Assets/_Project/Scripts/Combat/Implementations/NpcTarget.cs` — death flow (T-NPC-01, hook)
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — CreateNpcSpawner (T-NPC-10)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — register AggroTable subscription (T-NPC-01)
- `Assets/_Project/Scripts/World/Streaming/ChunkNetworkSpawner.cs` — chunk-based spawn (T-NPC-09)

### Новые scene objects:
- `[NpcSpawner]` GameObject в `BootstrapScene` (NetworkObject + NpcSpawner).
- Prefab `Npc_Goblin.prefab` в `Assets/_Project/Prefabs/AI/` + добавлен в `NetworkManager.NetworkConfig.Prefabs`.

---

## 8. Тестирование (Play Mode verify)

1. **Press Play** в `BootstrapScene` → StartHost.
2. **Ожидаемое в Console:**
   - `[NMC] Created [NpcSpawner] as root GameObject`
   - `[NpcSpawner] Tick: alive=0/max=5, players=1`
   - `[NpcSpawner] Spawned NPC at (40050, 2502, 40010) target=(40050, 2502, 40010)`
3. **Через 4 сек после спавна** → NPC_Goblin в 30-60м от игрока.
4. **Подойти к NPC** (WASD) → через ~1 сек NPC переходит в Chase state → бежит к игроку.
5. **NPC подбежал** → переходит в Attack → `[NpcAttacker] RequestAttackRpc → PlayerTarget damage` в Console.
6. **Игрок убивает NPC** → `[NpcTarget] killed → SpawnLootChest + Destroy in 3s` → кредиты начислены через `[TradeServer] AddCredits player=0 amount=42`.
7. **Спам K + бег** → NPC постоянно агрятся → их спавнится 5 одновременно.

---

## 9. План реализации — следующая сессия

1. **Сессия A (P0):** T-NPC-01 (Brain) + T-NPC-02 (Spawner) + T-NPC-06 (Prefab) + T-NPC-07 (Animator) + T-NPC-08 (Scene-placed). NPC спавнятся, гонятся, атакуют.
2. **Сессия B (P1):** T-NPC-04 (LootTable credits) + T-NPC-03 (LootPickup) + T-NPC-10 (NMC hook). Death animation + loot.
3. **Сессия C (P2, optional):** T-NPC-05 (Visual config) + T-NPC-09 (Chunk integration). Polish + scaling.

**Старт:** скажи "продолжай P0" — начну с T-NPC-01 + T-NPC-02 + T-NPC-06 + T-NPC-07 + T-NPC-08.

---

## 10. Зависимости (что потребуется)

- **Unity NavMesh** (`UnityEngine.AI`) — встроен.
- **NetworkObject** + **NetworkTransform** (NGO) — уже в проекте.
- **CombatServer** (для атак) — ✅ готов.
- **TradeWorld.Repository** (для credits) — ✅ готов.
- **NetworkChestContainer** (для loot) — ✅ готов.
- **LootTable** (для дропа) — ✅ готов (нужно расширить credits).
- **Kevin Iglesias Human Animations** — ✅ импортирован.
- **NetworkPlayer** (цель NPC) — ✅ готов.
- **WorldChunkManager** + **ChunkNetworkSpawner** (chunk spawning) — ✅ есть (нужно хук).

**Всё готово для начала.** T-NPC-01..10 не требуют новых зависимостей — только организация существующих.