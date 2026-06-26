# NPC P2 — Visual Config + Chunk Integration (T-NPC-05 + T-NPC-09)

> **Дата:** 2026-06-26
> **Статус:** 📝 Дизайн перед кодом. Реализация — следующие шаги.
> **Сессия:** продолжаем NPC Roadmap после P0 (Brain/Spawner/Prefab/Animator/Scene) + P1 (LootTable credits + NpcLootPickup + NpcTarget death-flow).
> **Документ-источник:** `docs/Character/Skills/real-time-combat/70_NPC_ENEMIES.md` §2.4, §3.4.

---

## 0. Что уже есть (фактическое состояние проекта)

Прочитано из `Assets/_Project/Scripts/AI/` + `Assets/_Project/Scripts/World/Streaming/ChunkNetworkSpawner.cs` + префаб `Npc_Goblin.prefab`:

| Что | Где | Состояние |
|---|---|---|
| `NpcSpawner` (server-side, scene-placed в `BootstrapScene`) | `AI/NpcSpawner.cs` (224 строки) | ✅ Готов, есть полная логика: surface validation, rate-limit, activationRadius |
| `NpcSpawnerConfig` SO | `AI/NpcSpawnerConfig.cs` | ✅ rad=[5,20], max=5, interval=4s, chance=0.5 |
| `NpcBrain` (FSM Idle/Chase/Attack/Dead) | `AI/NpcBrain.cs` (296 строк) | ✅ Готов. Использует `GetComponentInChildren<Animator>()` + triggers "Attack"/"Death" |
| `Npc_Goblin.prefab` | `Prefabs/AI/Npc_Goblin.prefab` (61 объект) | ✅ Готов: NetworkObject + NetworkTransform + CharacterController + NavMeshAgent + NpcBrain + NpcAttacker + NpcTarget; child `Visual/HumanM_Model` (nested Kevin Iglesias FBX, `HumanM_BodyMesh` = SkinnedMeshRenderer) |
| `NpcAnimatorController` | `Animation/AI/NpcAnimatorController.controller` | ✅ 5 states (Idle/Walk/Run/Attack/Death), 4 parameters (Speed/IsAttacking/Attack/Death) |
| `ChunkNetworkSpawner` | `World/Streaming/ChunkNetworkSpawner.cs` | ⚠️ Частично: сундуки спавнятся (`chestPrefab`/`InstantiateChest`), NPC — **placeholder** (`if (chunk.Peaks != null && npcPrefab != null) { /* placeholder */ }`) |
| `WorldChunkManager` | `World/Streaming/WorldChunkManager.cs` | ✅ Есть, reflection подтверждает: `GetChunk(ChunkId)`, `GetChunkAtPosition`, `GetChunksInRadius`, `TotalChunkCount` |
| `WorldChunk` | `World/Streaming/WorldChunk` | ✅ Reflection: fields `CloudSeed, Farms, Id, Peaks, State, WorldBounds` |
| `ChunkLoader` | `World/Streaming/ChunkLoader.cs` | ✅ События `OnChunkLoaded` / `OnChunkUnloaded` уже подписаны в `ChunkNetworkSpawner` |

**Ключевые ограничения, выявленные при чтении:**

1. `HumanM_Model` — **nested prefab** (assetPath = `Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx`). `modify_contents` на `Npc_Goblin.prefab` может менять `Visual` (его собственный объект) и создавать новые children, но **не может удалять или перемещать детей внутри `HumanM_Model`** — это часть импортированного FBX.
2. `NpcBrain._animator = GetComponentInChildren<Animator>()` — найдёт **первый** Animator в детях. Сейчас в префабе их два: на `Visual` и на `HumanM_Model`. `GetComponentInChildren` идёт в DFS-порядке → сначала `Visual/Animator`, что и нужно (`NpcAnimatorController` сидит на `Visual`).
3. `NpcSpawner` имеет полный pipeline (surface/rate-limit/leash) — **T-NPC-09 не должен его дублировать**, иначе будут два разных источника правды (race conditions).
4. `ChunkNetworkSpawner.InstantiateNPC(Vector3)` существует, но не вызывается — это «сырой» spawn без surface validation, без rate-limit. Сейчас использовать его напрямую = регрессия vs P0.

---

## 1. T-NPC-05: NpcVisualConfig + NpcVisualApplier

### 1.1 Проблема

Сейчас префаб `Npc_Goblin` визуально = Kevin Iglesias HumanM_Model (базовый скин Human Male). Anti-restrictive визуал нужен для:

1. **Фракционное разнообразие** — Goblin (зелёная кожа), Bandit (грязная одежда), Guard (броня) — все используют один и тот же `Npc_Goblin.prefab`-like префаб, но с разными материалами/мешами.
2. **Quick swap без редактирования префаба** — дизайнер меняет SO в инспекторе, не открывает префаб (важно когда префаб shared между мобами).
3. **Future-proof** — если завтра появится `Orc_Model.fbx`, не надо переписывать prefab, просто SO подменяет mesh.

### 1.2 Решение — два слоя

**Слой A: Material override (cheap, MVP-достаточно)**

- Меняем **только материалы** на `HumanM_BodyMesh.SkinnedMeshRenderer.sharedMaterials[]` (или цвет через `_Color`).
- Плюсы: 0 новых ассетов, 0 поломок rigged HumanM_Model, мгновенный swap.
- Минусы: все NPC остаются HumanM по силуэту.

**Слой B: Full mesh swap (дороже, опционально)**

- Если `NpcVisualConfig.overrideMesh != null` → Destroy `HumanM_Model` child + Instantiate `overrideMesh` как child `Visual`, перенести `Animator` binding.
- Используется редко, для полностью отличных рас (Skeleton, Dragon — но это не «humanoid bandit», это уже отдельный префаб).

**MVP-решение:** **только слой A** (material/color override). Слой B делаем, но реальный hook оставляем пустым (только API), чтобы не сломать HumanM_Model.

### 1.3 NpcVisualConfig : ScriptableObject

```csharp
namespace ProjectC.AI
{
    [CreateAssetMenu(menuName = "Project C/AI/Npc Visual Config")]
    public class NpcVisualConfig : ScriptableObject
    {
        [Header("Material overrides (by SkinnedMeshRenderer.sharedMaterial slot)")]
        public Material[] bodyMaterials;        // → HumanM_BodyMesh.sharedMaterials
        public Color tintColor = Color.white;   // → применяется через MaterialPropertyBlock (дёшево, без instance leak)
        public string tintColorProperty = "_BaseColor"; // URP Lit / Built-in Standard

        [Header("Optional scale (не ломает NavMeshAgent)")]
        public float uniformScale = 1f;        // → transform.localScale multiplier (на root или Visual)

        [Header("Display name (для UI: имя моба над головой, опционально)")]
        public string displayName = "Goblin";
    }
}
```

Дизайнерские `.asset`-ы:
- `Assets/_Project/Resources/AI/NpcVisual_Goblin.asset` — bodyMaterials=null (default HumanM), tintColor=green, scale=1.0, displayName="Goblin".
- `Assets/_Project/Resources/AI/NpcVisual_Bandit.asset` — tintColor=brown, displayName="Bandit" (для post-MVP).

### 1.4 NpcVisualApplier : MonoBehaviour

Компонент на root-объекте NPC. При `OnNetworkSpawn` (server-side, чтобы избежать race с NetworkTransform на клиенте):

1. Читает `NpcSpawnerConfig.visualConfig` (через спавнер, **опционально** — может быть null → no-op).
2. Находит `SkinnedMeshRenderer` по имени `HumanM_BodyMesh` (избегаем хардкода индекса в иерархии).
3. Применяет `tintColor` через `MaterialPropertyBlock` (zero instance allocation, revert-safe).
4. Если `bodyMaterials != null && length > 0` → `renderer.sharedMaterials = bodyMaterials` (shared, чтобы не лить instance-копии).
5. Если `uniformScale != 1f` → `transform.localScale = Vector3.one * uniformScale`.

Анти-рестриктивное:
- Если `visualConfig = null` → no-op (логирование через Debug.Log verbose flag).
- Если `HumanM_BodyMesh` не найден → warning, no-op.
- Работает и на клиенте (через NetworkVariable replication? нет, **через обычный Apply в `Start()` на клиенте тоже**, потому что sharedMaterials на префабе уже дефолтные, нам нужно лишь material/color override — это локальная визуальная фича).

**Решение по timing:** вызываем `Apply()` в `Start()` (не `Awake()`) — на клиенте `NetworkObject` уже синхронизирован, `SkinnedMeshRenderer` уже есть. На сервере — то же самое.

### 1.5 Как NpcSpawner передаёт config

`NpcSpawnerConfig` расширяется полем:

```csharp
[Header("Visual (anti-restrictive T-NPC-05)")]
[Tooltip("Опционально. Применяется к NPC при спавне (материал, цвет, размер). " +
         "Если null — дефолтный HumanM-вид из префаба.")]
public NpcVisualConfig visualConfig;
```

В `NpcSpawner.TickSpawn()` после `Instantiate(_prefab, ...)`:

```csharp
if (_config != null && _config.visualConfig != null)
{
    var applier = go.GetComponent<NpcVisualApplier>();
    if (applier == null) applier = go.AddComponent<NpcVisualApplier>();
    applier.Apply(_config.visualConfig);
}
```

Идемпотентно (повторный Apply заменяет).

### 1.6 Файлы T-NPC-05

| Файл | Назначение |
|---|---|
| `Assets/_Project/Scripts/AI/NpcVisualConfig.cs` (новый, ~40 строк) | SO с material/color/scale/name |
| `Assets/_Project/Scripts/AI/NpcVisualApplier.cs` (новый, ~70 строк) | Component, Apply() |
| `Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs` (edit) | +1 поле `visualConfig` |
| `Assets/_Project/Scripts/AI/NpcSpawner.cs` (edit) | +5 строк в `TickSpawn` (применить visual) |
| `Assets/_Project/Resources/AI/NpcVisual_Goblin.asset` (новый) | Default для Goblin (зелёный tint) |

---

## 2. T-NPC-09: ChunkIntegration — NPC спавн по чанкам

### 2.1 Проблема

`ChunkNetworkSpawner` уже подписан на `ChunkLoader.OnChunkLoaded/OnChunkUnloaded`, но блок NPC-spawn — placeholder. Это означает:
- Сундуки в чанках появляются при загрузке чанка ✅
- NPC — НЕ появляются, пока живут в `BootstrapScene` через `NpcSpawner` (который сейчас активируется только в зоне `[NpcSpawner_Zone]` в `WorldScene_0_0`, рядом с тестовой площадкой).
- Для **бесконечного мира** (24 streaming scene) нужен chunk-based spawn — NPC появляются в загруженных чанках, деспавнятся при выгрузке.

### 2.2 Архитектурное решение — НЕ дублировать NpcSpawner

`NpcSpawner` уже содержит:
- `activationRadius` (игрок должен быть в зоне)
- surface validation (raycast вниз)
- rate-limit per player per minute
- `FindNearestPlayer()` (для активации)
- alive-count tracking

**Решение:** добавить в `NpcSpawner` публичный API для **внешнего запроса спавна в конкретной точке**, а `ChunkNetworkSpawner` его дёргает.

```csharp
// NpcSpawner — новые публичные методы (server-only)
public bool TrySpawnAtPoint(Vector3 worldPos, out NetworkObject spawned);
public bool TrySpawnAtPoint(Vector3 worldPos, ulong targetClientId, out NetworkObject spawned);
```

Метод делает **ровно то, что делает TickSpawn**, но:
- anchorPos = worldPos (вместо anchor transform)
- skip `FindNearestPlayer` (chunk integration знает, что чанк загружен → игрок в зоне)
- skip rate-limit? НЕТ — оставляем, иначе при chunk-load одной зоны массово заспавним 100 NPC и положим сервер. Rate-limit лимитирует по targetClientId = 0 (chunk spawn не привязан к игроку, это «spawn population»).

Альтернативно — **отдельная subsystem** `NpcChunkPopulator`, который слушает `ChunkLoader.OnChunkLoaded` и спавнит NPC напрямую. Минусы:
- Дублирует surface validation, FindObjectByType, alive-count.
- Race condition: два spawner-а (zone-based + chunk-based) в одной сцене → двойной alive-count.

**Решение MVP:** добавить в `NpcSpawner` публичный `TrySpawnAtPoint` + опциональный `subscribeToChunkLoader: bool` флаг, который auto-подписывается на `ChunkLoader` в `OnNetworkSpawn`.

### 2.3 NpcSpawner — расширение

В `NpcSpawner.cs` добавляем:

```csharp
[Header("Chunk integration (T-NPC-09)")]
[Tooltip("Подписаться на ChunkLoader.OnChunkLoaded → спавнить NPC в центре каждого загруженного чанка. " +
         "Если false — только zone-based spawn (старый поведение).")]
[SerializeField] private bool _autoPopulateChunks = false;

[Tooltip("Радиус спавна вокруг центра чанка (метры).")]
[Range(5f, 100f)] [SerializeField] private float _chunkSpawnRadius = 30f;

[Tooltip("Максимум NPC на один чанк (в дополнение к maxAliveCount глобально).")]
[Range(0, 20)] [SerializeField] private int _maxAlivePerChunk = 3;

private ChunkLoader _chunkLoader;
private readonly Dictionary<ChunkId, int> _chunkAliveCount = new Dictionary<ChunkId, int>();

public override void OnNetworkSpawn()
{
    // ...existing...
    if (_autoPopulateChunks)
    {
        _chunkLoader = FindAnyObjectByType<ChunkLoader>();
        if (_chunkLoader != null)
        {
            _chunkLoader.OnChunkLoaded += OnChunkLoaded_Spawn;
            _chunkLoader.OnChunkUnloaded += OnChunkUnloaded_Cleanup;
        }
    }
}

public override void OnNetworkDespawn()
{
    if (_chunkLoader != null)
    {
        _chunkLoader.OnChunkLoaded -= OnChunkLoaded_Spawn;
        _chunkLoader.OnChunkUnloaded -= OnChunkUnloaded_Cleanup;
    }
    base.OnNetworkDespawn();
}

private void OnChunkLoaded_Spawn(ChunkId chunkId)
{
    if (!IsServer || _prefab == null) return;
    var chunkManager = FindAnyObjectByType<WorldChunkManager>();
    if (chunkManager == null) return;
    var chunk = chunkManager.GetChunk(chunkId);
    if (chunk == null) return;

    // Центр чанка = его WorldBounds.center.
    Vector3 chunkCenter = chunk.WorldBounds != default ? chunk.WorldBounds.center : Vector3.zero;
    int spawned = 0;
    for (int i = 0; i < _maxAlivePerChunk && spawned < _maxAlivePerChunk; i++)
    {
        if (!TrySpawnAtPoint(chunkCenter, 0, out var no)) break;
        spawned++;
        if (_chunkAliveCount.ContainsKey(chunkId)) _chunkAliveCount[chunkId]++;
        else _chunkAliveCount[chunkId] = 1;
    }
    if (_showDebugLogs) Debug.Log($"[NpcSpawner] Chunk {chunkId} loaded → spawned {spawned}/{_maxAlivePerChunk} NPC");
}

private void OnChunkUnloaded_Cleanup(ChunkId chunkId)
{
    _chunkAliveCount.Remove(chunkId);
    // Сам Despawn делает ChunkNetworkSpawner (для chests — там своя логика).
    // NPC спавнятся с destroyWithScene=true → при unload сцены автоматически Destroy.
    // Поэтому специального cleanup не нужно — только счётчик обнуляем.
}

public bool TrySpawnAtPoint(Vector3 worldPos, ulong targetClientId, out NetworkObject spawned)
{
    spawned = null;
    if (!IsServer || _prefab == null) return false;
    if (_spawned.Count >= _maxAlive) return false;
    if (!TryFindSpawnPoint(worldPos, out Vector3 spawnPos)) return false;
    if (IsTooCloseToOtherNpc(spawnPos)) return false;

    var go = Instantiate(_prefab, spawnPos, Quaternion.identity);
    var netObj = go.GetComponent<NetworkObject>();
    if (netObj == null) { Debug.LogError(...); Destroy(go); return false; }
    netObj.Spawn(destroyWithScene: true);
    _spawned.Add(netObj);
    spawned = netObj;
    return true;
}
```

Замечание: `WorldChunk.WorldBounds` существует (reflection подтверждает). Тип `Bounds` имеет `.center`.

### 2.4 ChunkNetworkSpawner — НЕ дублировать, использовать NpcSpawner

В `ChunkNetworkSpawner.cs` оставляем блок `npcPrefab` **deprecated** (поле остаётся для обратной совместимости, но в логике не используется). Реальная интеграция — через NpcSpawner.

Это **НЕ редактирует** ChunkNetworkSpawner, а только документирует deprecated статус.

### 2.5 Файлы T-NPC-09

| Файл | Изменение |
|---|---|
| `Assets/_Project/Scripts/AI/NpcSpawner.cs` (edit) | +1 header "Chunk integration", +2 поля, +1 публичный метод, +2 event handlers, +1 dict |
| `Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs` (edit) | **опционально** — вынести `_autoPopulateChunks`/`_chunkSpawnRadius`/`_maxAlivePerChunk` в config. Делаем так: эти поля остаются inline (spawner-specific tuning), config оставляем для prefab/visual/range. |

Без новых файлов — расширяем существующий NpcSpawner.

---

## 3. Sequence: что происходит при chunk load

1. `ChunkLoader.OnChunkLoaded(ChunkId)` (server-side).
2. Если `NpcSpawner._autoPopulateChunks = true` → `NpcSpawner.OnChunkLoaded_Spawn(chunkId)`.
3. NpcSpawner спрашивает у `WorldChunkManager.GetChunk(chunkId)` → получает `WorldChunk`.
4. Берёт `chunk.WorldBounds.center` как центр спавна.
5. До `_maxAlivePerChunk` раз вызывает `TrySpawnAtPoint(center, 0, ...)`.
6. `TrySpawnAtPoint` валидирует surface (raycast), дистанцию до других NPC, создаёт `Instantiate + NetworkObject.Spawn(destroyWithScene:true)`.
7. `_chunkAliveCount[chunkId]++`.
8. На клиенте NGO реплицирует новый NPC → анимация/движение работают как раньше.

При chunk unload:
1. `ChunkLoader.OnChunkUnloaded(chunkId)`.
2. NpcSpawner убирает счётчик (`_chunkAliveCount.Remove`).
3. NGO сам деспавнит NPC (потому что `destroyWithScene=true`).

---

## 4. Тестирование (Play Mode verify, пост-реализации)

После реализации, рекомендуемая пользователем верификация:

### T-NPC-05 verify
1. Открыть `Npc_Goblin.prefab` → нет изменений в `HumanM_Model` (мы не трогаем nested prefab).
2. Создать `NpcVisual_Goblin.asset` (Resources/AI) → tintColor = green (0.2, 0.8, 0.2).
3. В `NpcSpawner_Default.asset` (Resources/AI) установить `visualConfig = NpcVisual_Goblin.asset`.
4. Play Mode → StartHost → NPC_Goblin спавнится в зелёном оттенке (HumanM материал через MaterialPropertyBlock).

### T-NPC-09 verify
1. В `BootstrapScene` (или где лежит NpcSpawner) установить `_autoPopulateChunks = true`, `_chunkSpawnRadius = 30`, `_maxAlivePerChunk = 3`.
2. Play Mode → StartHost → загружается `WorldScene_0_0` через `ClientSceneLoader` → `ChunkLoader` фиксирует chunk (0,0) → `OnChunkLoaded(ChunkId(0,0))` → NpcSpawner спавнит до 3 NPC в зоне 30м от центра чанка.
3. Console: `[NpcSpawner] Chunk 0,0 loaded → spawned 3/3 NPC` (плюс zone-spawn из `NpcSpawner_Zone`, если включён — тогда 3+5 alive, лимит `maxAliveCount=5` ограничит общий count).
4. **Edge case test**: выключить `autoPopulateChunks` → NPC спавнятся только из `NpcSpawner_Zone` (текущее поведение P0).

---

## 5. Чек-лист перед кодом

- [x] Прочитаны NpcSpawner / NpcSpawnerConfig / NpcBrain / Npc_Goblin.prefab (hierarchy) / ChunkNetworkSpawner / WorldChunk / WorldChunkManager.
- [x] Выявлено: HumanM_Model — nested prefab (Kevin Iglesias FBX), не редактируется через `modify_contents`.
- [x] Выявлено: `NpcBrain._animator = GetComponentInChildren` берёт `Visual/Animator` первым — это правильный (наш `NpcAnimatorController`), дополнительные Animator-ы на HumanM_Model — не наш.
- [x] Решено: `MaterialPropertyBlock` (не instance material) — zero-leak.
- [x] Решено: chunk-integration через расширение `NpcSpawner` (а не отдельный subsystem) — DRY.
- [x] Решено: `destroyWithScene:true` для chunk-NPC → NGO сам деспавнит при unload.
- [x] Visual: только слой A (material/color override), слой B (full mesh swap) — API готов, но default path не вызывает.
- [x] Anti-restrictive: всё опционально (visualConfig=null → default HumanM, autoPopulateChunks=false → no chunk spawn).

---

## 6. Порядок реализации

1. **Сначала T-NPC-05** (Visual Config + Applier) — изолированный компонент, не ломает существующего.
2. **Потом T-NPC-09** (Chunk integration в NpcSpawner) — расширение существующего спавнера.
3. После каждого — `refresh_unity + read_console` через MCP.
4. **Пользователь** делает verify в Play Mode (я не запускаю Play Mode).

Ожидаемое время:
- T-NPC-05: ~1.5 ч (SO + Applier + edit Config + edit Spawner + .asset)
- T-NPC-09: ~1 ч (edit NpcSpawner, public API, chunk subscription)
- Итого P2: ~2.5 ч.