# Stats Progression — формула роста, источники XP, NPC-spam protection

> **Дата:** 2026-06-14
> **Базируется на:** `WorldEventBus` (готов, T-X0), `StatsConfig` (новый SO), `PlayerStats` struct
> **Источник XP:** 10 разных активностей → 3 характеристики через configurable mapping

---

## 1. Геометрическая формула роста

### 1.1 Постановка задачи

Из спецификации пользователя:
> "рост характеристик в прогрессии геометрической (тут скрыта стандартная лвл система где каждый следующий требует больше) без капа по макс"

**Интерпретация:**
- Формула `XP_for_next_tier(tier) = baseXp * growthRate^tier` (geometric growth)
- `baseXp = 100`, `growthRate = 1.5` (default) → tier 0→1: 100, tier 1→2: 150, tier 2→3: 225, tier 3→4: 337.5, ...
- **Без cap** — каждый следующий tier требует больше XP, но не запрещено бесконечно расти
- Tier promotion: `currentXp += gainXp; while (currentXp >= xpForNextTier) { currentXp -= xpForNextTier; currentTier++; }`

### 1.2 Почему float, не int

**Аргумент:**
- При `growthRate = 1.5` и `tier = 20`: `XP_for_next_tier = 100 * 1.5^20 = 33252.5` — не целое
- Если бы int + округление: видимые "ступеньки" в прогрессии (4.5 / 5 XP → резко 5.0 / 6.0)
- Float даёт **плавный** прогресс, без visual артефактов

**Решение:** Храним `currentXp` как float. Tier promotion — float arithmetic, кастуем tier к int (количество полных тиров).

### 1.3 Защита от бесконечного cap (overflow)

**Проблема:** При `tier = 100`, `1.5^100 ≈ 4 × 10^17` → float precision теряется (~7 значащих цифр).

**Решение:** В `StatsConfig.OnValidate` warning при `tier > 50` (теоретический cap для визуальной целостности). В runtime — soft cap: если `tier > 50` → отображаем как "Tier 50+" (без реального cap, но UI не показывает большие числа).

### 1.4 Tier promotion loop (в StatsServer.ApplyXp)

```csharp
private void ApplyXp(ulong clientId, StatType stat, float rawXp) {
    // 1. Global multiplier (test/event buff)
    float xp = _config.ApplyGlobalMultiplier(rawXp);

    // 2. Per-stat multiplier
    xp = _config.ApplyStatMultiplier(stat, xp);
    if (xp <= 0f) return;  // ничего не делаем

    // 3. Get current state
    var stats = _world.GetOrCreateStats(clientId);
    ref float currentXp = ref GetXpRef(ref stats, stat);
    ref int currentTier = ref GetTierRef(ref stats, stat);
    ref float totalXp = ref GetTotalXpRef(ref stats, stat);

    // 4. Add XP
    currentXp += xp;
    totalXp += xp;

    // 5. Tier promotion loop
    int promotionsThisCall = 0;
    while (currentXp >= _config.XpForNextTier(currentTier)) {
        currentXp -= _config.XpForNextTier(currentTier);
        currentTier++;
        promotionsThisCall++;
        // Optional: emit tier-up event for UI pulse
        WorldEventBus.Publish(new StatTierUpEvent {
            PlayerId = clientId, StatType = stat, NewTier = currentTier
        });
    }

    // 6. Update world
    _world.SetStats(clientId, stats);

    // 7. Persist + send snapshot
    SavePlayerAsync(clientId);
    SendSnapshotToOwner(clientId);
}
```

### 1.5 Рефакторинг по принципу "не дублируй"

**Проблема:** 3 характеристики × повторяющийся код = копипаст.

**Решение:** используем `ref` returns для sub-field access (`GetXpRef(ref stats, stat)`).

```csharp
private static ref float GetXpRef(ref PlayerStats stats, StatType stat) {
    switch (stat) {
        case StatType.Strength: return ref stats.strength;
        case StatType.Dexterity: return ref stats.dexterity;
        case StatType.Intelligence: return ref stats.intelligence;
        default: throw new ArgumentOutOfRangeException(nameof(stat));
    }
}
private static ref int GetTierRef(ref PlayerStats stats, StatType stat) {
    switch (stat) {
        case StatType.Strength: return ref stats.strengthTier;
        case StatType.Dexterity: return ref stats.dexterityTier;
        case StatType.Intelligence: return ref stats.intelligenceTier;
        default: throw new ArgumentOutOfRangeException(nameof(stat));
    }
}
```

---

## 2. Источники XP — детальная таблица

### 2.1 Маппинг источник → характеристика

| Источник XP | Stat | Базовое кол-во | Редактируется | NPC-spam protection |
|-------------|------|----------------|---------------|---------------------|
| Mining (`MiningCompletedEvent`) | Strength | 1 XP per item | `_miningXpPerItem` | нет (mining естественно rate-limited) |
| Crafting (`CraftingCompletedEvent`) | Intelligence | 5 XP | `_craftingXpPerItem` | нет (craft rate-limited) |
| Exchange Pack (`ExchangeCompletedEvent`) | Intelligence | 2 XP per op | `_exchangeXpPerOp` | нет (UI rate-limited) |
| Exchange Unpack (`ExchangeCompletedEvent`) | Intelligence | 2 XP per op | `_exchangeXpPerOp` | нет |
| Market Buy (`MarketTradedEvent`) | Intelligence | 1 XP per op | `_marketXpPerOp` | нет (UI rate-limited) |
| Market Sell (`MarketTradedEvent`) | Intelligence | 1 XP per op | `_marketXpPerOp` | нет |
| Quest Accepted (`QuestAcceptedEvent`) | Intelligence | 3 XP | `_questAcceptedXp` | нет (quests are rate-limited) |
| Quest Completed (`QuestCompletedEvent`) | Intelligence | 10 XP | `_questCompletedXp` | нет |
| Dialog with NPC (`DialogVisitedEvent`) | Intelligence | 1 XP | `_dialogXpPerVisit` | **да, 60 sec cooldown per (player, npcId)** |
| Walk (per 10m, `StatsServer.FixedUpdate`) | Dexterity | 1 XP per 10m | `_walkXpPer10m` | нет (always accumulating) |
| Jump (`PlayerJumpedEvent`) | Dexterity | 0.5 XP | `_jumpXp` | нет |
| Pilot ship (per 100m, `ShipPilotTickEvent`) | Intelligence | 1 XP per 100m | `_pilotXpPer100m` | нет |

**Total источников:** 12, мапятся на 3 стата через `StatsConfig._miningTarget/_craftingTarget/...`.

### 2.2 Subscriptions в StatsServer.OnNetworkSpawn

```csharp
public override void OnNetworkSpawn() {
    if (!IsServer) return;
    Instance = this;

    _handleMiningCompleted = OnMiningCompleted;
    WorldEventBus.Subscribe<MiningCompletedEvent>(_handleMiningCompleted);

    _handleCraftingCompleted = OnCraftingCompleted;
    WorldEventBus.Subscribe<CraftingCompletedEvent>(_handleCraftingCompleted);

    _handleExchangeCompleted = OnExchangeCompleted;
    WorldEventBus.Subscribe<ExchangeCompletedEvent>(_handleExchangeCompleted);

    _handleMarketTraded = OnMarketTraded;
    WorldEventBus.Subscribe<MarketTradedEvent>(_handleMarketTraded);

    _handleQuestAccepted = OnQuestAccepted;
    WorldEventBus.Subscribe<QuestAcceptedEvent>(_handleQuestAccepted);

    _handleQuestCompleted = OnQuestCompleted;
    WorldEventBus.Subscribe<QuestCompletedEvent>(_handleQuestCompleted);

    _handleDialogVisited = OnDialogVisited;
    WorldEventBus.Subscribe<DialogVisitedEvent>(_handleDialogVisited);

    _handleShipPilotTick = OnShipPilotTick;
    WorldEventBus.Subscribe<ShipPilotTickEvent>(_handleShipPilotTick);

    _handlePlayerJumped = OnPlayerJumped;
    WorldEventBus.Subscribe<PlayerJumpedEvent>(_handlePlayerJumped);
}
```

### 2.3 Handlers (delegated to ApplyXp)

```csharp
private void OnMiningCompleted(MiningCompletedEvent ev) {
    if (ev.PlayerId == 0 || ev.Quantity <= 0) return;
    var stat = _config.GetStatFor(XpSource.Mining);
    float xp = _config.GetBaseXp(XpSource.Mining) * ev.Quantity;
    ApplyXp(ev.PlayerId, stat, xp);
}

private void OnCraftingCompleted(CraftingCompletedEvent ev) { /* аналогично */ }
private void OnExchangeCompleted(ExchangeCompletedEvent ev) { /* аналогично */ }
private void OnMarketTraded(MarketTradedEvent ev) { /* аналогично */ }
private void OnQuestAccepted(QuestAcceptedEvent ev) { /* аналогично */ }
private void OnQuestCompleted(QuestCompletedEvent ev) { /* аналогично */ }
private void OnShipPilotTick(ShipPilotTickEvent ev) { /* distance accumulator */ }
private void OnPlayerJumped(PlayerJumpedEvent ev) { /* ApplyXp */ }

private void OnDialogVisited(DialogVisitedEvent ev) {
    if (ev.PlayerId == 0 || string.IsNullOrEmpty(ev.NpcId)) return;

    // Anti-spam protection
    if (!CanGainDialogXp(ev.PlayerId, ev.NpcId)) return;
    MarkDialogXpGained(ev.PlayerId, ev.NpcId);

    var stat = _config.GetStatFor(XpSource.Dialog);
    float xp = _config.GetBaseXp(XpSource.Dialog);
    ApplyXp(ev.PlayerId, stat, xp);
}
```

---

## 3. Unique-event tracking (НЕ cooldown — Q1.4)

### 3.1 Проблема (ОТВЕТ ПОЛЬЗОВАТЕЛЯ)

**Решение пользователя (Q1.4, дословно):**
> "здесь не должен быть кулдаун. должен быть per uniq dialog\нажатие или т.п. тоесть когда есть уникальное событие, встретил впервые - даются очки, поговрил на новую тему - которую раньше не жал, даются очки, сделал квест - даются очки и т.п. в такой системе не нужен кулдаун. так как любой кулдаун объодится автокликером."

**Принцип:** XP даётся только за **уникальные события**, не за повторения. Никакого cooldown — его обходят автокликеры. Каждое уникальное событие = +1 (или +N) XP, и больше никогда.

### 3.2 Какие события считаются "уникальными"

| Событие | Уникальный ключ | +XP |
|---------|-----------------|-----|
| Встретил NPC впервые | `(playerId, npcId)` | +1 INT |
| Поговорил с NPC на новую тему | `(playerId, npcId, dialogNodeId)` | +1 INT |
| Завершил квест | `(playerId, questId)` | уже считается через `QuestCompletedEvent` (см. Q1.4 — это covered) |
| Сел в корабль впервые | `(playerId, shipTypeId)` | (если нужно — будущее) |
| Зашёл в новую зону | `(playerId, sceneId)` | (если нужно — будущее) |

**Минимальный MVP:** только `(playerId, npcId)` — встретил NPC впервые = +1 INT. Дальше расширяем.

### 3.3 Реализация — Set<string> с уникальными ключами

```csharp
// Per-player, per-... уникальные ключи
private Dictionary<ulong, HashSet<string>> _uniqueEventsPerPlayer = new();
// Где ключ может быть: $"{npcId}" или $"{npcId}:{dialogNodeId}" и т.д.

private bool IsUniqueEvent(ulong clientId, string eventKey) {
    if (!_uniqueEventsPerPlayer.TryGetValue(clientId, out var set)) {
        set = new HashSet<string>();
        _uniqueEventsPerPlayer[clientId] = set;
    }
    return set.Add(eventKey);  // returns true если НОВЫЙ, false если уже был
}
```

### 3.4 Handler для DialogVisitedEvent

```csharp
private void OnDialogVisited(DialogVisitedEvent ev) {
    if (ev.PlayerId == 0 || string.IsNullOrEmpty(ev.NpcId)) return;

    // Q1.4: уникальное событие — встретил NPC впервые ИЛИ поговорил на новую тему
    // eventKey включает dialogNodeId если есть, иначе просто npcId
    string eventKey = string.IsNullOrEmpty(ev.NodeId) ? ev.NpcId : $"{ev.NpcId}:{ev.NodeId}";

    if (!IsUniqueEvent(ev.PlayerId, eventKey)) {
        if (_config.DebugLogging) {
            Debug.Log($"[StatsServer] Player {ev.PlayerId} repeat dialog with {eventKey} — no XP");
        }
        return;  // уже было — НЕ даём XP
    }

    // Новое уникальное событие — даём XP
    var stat = _config.GetStatFor(XpSource.Dialog);
    float xp = _config.GetBaseXp(XpSource.Dialog);
    ApplyXp(ev.PlayerId, stat, xp);

    if (_config.DebugLogging) {
        Debug.Log($"[StatsServer] Player {ev.PlayerId} NEW unique dialog '{eventKey}' → +{xp} INT");
    }
}
```

### 3.5 Дополнительные уникальные источники XP (Phase 2)

Сейчас в MVP — только DialogVisitedEvent. Phase 2 (не блокер):
- **Discovered new location** — `WorldEventBus.Publish(new LocationDiscoveredEvent { PlayerId, LocationId })` → уникальный ключ
- **Discovered new NPC** — то же (вместо простого dialog)
- **First time in scene** — `OnSceneLoaded` в `NetworkManagerController` → unique key

Все эти события идут через **тот же** `IsUniqueEvent` метод — просто другой eventKey.

### 3.6 Persistence (в CharacterSaveData)

**В CharacterSaveData** (см. `08_ROADMAP.md` T-P06):
```csharp
[Serializable]
public class StatsSave {
    // ... stats fields ...
    public string[] uniqueDialogEvents = Array.Empty<string>();
}
```

**При load** (OnPlayerConnected):
```csharp
_uniqueEventsPerPlayer[clientId] = new HashSet<string>(savedData.uniqueDialogEvents);
```

**При save** (periodic / disconnect):
```csharp
data.stats.uniqueDialogEvents = _uniqueEventsPerPlayer[clientId].ToArray();
```

### 3.7 Альтернативы (отвергнуты)

| Подход | Почему нет |
|--------|-----------|
| Cooldown per (playerId, npcId) | обходится автокликером (Q1.4 пользователь явно отверг) |
| Cooldown global | обходится переключением NPC |
| Rate-limit в QuestServer | смешивает concerns |
| Client-side tracking | bypass через RPC spoofing |

---

## 4. Distance tracking (DEX walk + INT pilot)

### 4.1 Walked distance (DEX)

**Триггер:** `StatsServer.FixedUpdate` (server tick, 50 Hz).

**Логика:**
```csharp
private Dictionary<ulong, Vector3> _lastPosPerPlayer = new();
private Dictionary<ulong, float> _walkedDistanceBuffer = new();

private void FixedUpdate() {
    if (!IsServer) return;
    if (NetworkManager.Singleton == null) return;

    var currentServerTime = (float)NetworkManager.Singleton.ServerTime.Time;

    foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds) {
        var player = NetworkManager.Singleton.ConnectedClients[clientId]?.PlayerObject;
        if (player == null) continue;
        var netPlayer = player.GetComponent<NetworkPlayer>();
        if (netPlayer == null) continue;

        // Skip if in ship (pilot distance обрабатывается отдельно)
        if (netPlayer.IsInShip) {
            _lastPosPerPlayer[clientId] = netPlayer.transform.position;
            continue;
        }

        Vector3 currentPos = netPlayer.transform.position;
        if (_lastPosPerPlayer.TryGetValue(clientId, out var lastPos)) {
            float dist = Vector3.Distance(currentPos, lastPos);
            if (dist > 0.01f) {  // ignore micro-jitter
                AccumulateWalkedXp(clientId, dist);
            }
        }
        _lastPosPerPlayer[clientId] = currentPos;
    }
}

private void AccumulateWalkedXp(ulong clientId, float deltaDistance) {
    // Q1.5: per 1m walked, настраиваемо, +track total
    if (!_walkedDistanceBuffer.TryGetValue(clientId, out var buffer)) buffer = 0f;
    buffer += deltaDistance;

    // Зацемпить total walked для ачивок/трекеров (Q1.5)
    if (_config.TrackTotalDistance) {
        if (!_totalWalkedDistance.TryGetValue(clientId, out var total)) total = 0f;
        total += deltaDistance;
        _totalWalkedDistance[clientId] = total;
    }

    if (buffer >= _config.WalkDistanceXpThreshold) {
        float overshoot = buffer - _config.WalkDistanceXpThreshold;
        float xpAmount = _config.GetBaseXp(XpSource.Walk) * (buffer / _config.WalkDistanceXpThreshold);
        var stat = _config.GetStatFor(XpSource.Walk);
        ApplyXp(clientId, stat, xpAmount);
        _walkedDistanceBuffer[clientId] = overshoot;  // save remainder
    } else {
        _walkedDistanceBuffer[clientId] = buffer;
    }
}

// Q1.5: total walked/piloted distance (для ачивок и трекеров)
private Dictionary<ulong, float> _totalWalkedDistance = new();
private Dictionary<ulong, float> _totalPilotedDistance = new();

public float GetTotalWalkedDistance(ulong clientId) =>
    _totalWalkedDistance.TryGetValue(clientId, out var v) ? v : 0f;
public float GetTotalPilotedDistance(ulong clientId) =>
    _totalPilotedDistance.TryGetValue(clientId, out var v) ? v : 0f;
```

**Важно:**
- `_inShip == true` → skip (п pilot distance отдельно через `ShipPilotTickEvent`)
- `dist > 0.01f` → ignore micro-jitter (NetworkTransform sync может дёргать позицию)
- `_walkedDistanceBuffer` — накапливаем остаток (если прошёл 25m, threshold 10m → 1 XP + 15m в буфер)
- `_lastPosPerPlayer[clientId]` обновляется **каждый кадр** (включая in-ship = current pos, чтобы при disembark не было гигантского delta)

### 4.2 Piloted distance (INT)

**Триггер:** публикуется из `ShipController.FixedUpdate` (server, per pilot).

**Пример публикации** (минимальное изменение в `ShipController.cs:355`):
```csharp
private void FixedUpdate() {
    if (!IsServer) return;
    if (_pilots.Count == 0) return;
    // ... existing physics integration ...

    // Distance accumulator per pilot (new)
    Vector3 currentPos = _rb.position;
    if (_lastPilotPos != Vector3.zero) {
        float deltaDistance = Vector3.Distance(currentPos, _lastPilotPos);
        if (deltaDistance > 0.01f) {
            // Split среди пилотов (для multi-pilot ships — будущее)
            foreach (var pilotId in _pilots) {
                WorldEventBus.Publish(new ShipPilotTickEvent {
                    PlayerId = pilotId,
                    DeltaDistance = deltaDistance / _pilots.Count,
                });
            }
        }
    }
    _lastPilotPos = currentPos;
}

private Vector3 _lastPilotPos;
```

**Подписка в StatsServer:**
```csharp
private Dictionary<ulong, float> _pilotDistanceBuffer = new();

private void OnShipPilotTick(ShipPilotTickEvent ev) {
    if (ev.PlayerId == 0 || ev.DeltaDistance <= 0f) return;

    // Q1.5: track total piloted distance
    if (_config.TrackTotalDistance) {
        if (!_totalPilotedDistance.TryGetValue(ev.PlayerId, out var total)) total = 0f;
        total += ev.DeltaDistance;
        _totalPilotedDistance[ev.PlayerId] = total;
    }

    if (!_pilotDistanceBuffer.TryGetValue(ev.PlayerId, out var buffer)) buffer = 0f;
    buffer += ev.DeltaDistance;
    if (buffer >= _config.PilotDistanceXpThreshold) {
        float overshoot = buffer - _config.PilotDistanceXpThreshold;
        float xpAmount = _config.GetBaseXp(XpSource.Pilot) * (buffer / _config.PilotDistanceXpThreshold);
        var stat = _config.GetStatFor(XpSource.Pilot);
        ApplyXp(ev.PlayerId, stat, xpAmount);
        _pilotDistanceBuffer[ev.PlayerId] = overshoot;
    } else {
        _pilotDistanceBuffer[ev.PlayerId] = buffer;
    }
}
```

### 4.3 Jump detection (DEX)

**Проблема:** `Keyboard.current.spaceKey.wasPressedThisFrame` читается только на owner client (`NetworkPlayer.cs:400`). Server не знает о прыжках.

**Решение:** Добавляем `SubmitJumpRpc` (owner→server) в `NetworkPlayer.cs`.

**Минимальное изменение в NetworkPlayer:**
```csharp
// В NetworkPlayer.cs, добавить:

[Rpc(SendTo.Server, RequireOwnership = true)]
public void SubmitJumpRpc(RpcParams rpcParams = default) {
    ulong clientId = rpcParams.Receive.SenderClientId;
    WorldEventBus.Publish(new PlayerJumpedEvent { PlayerId = clientId });
}

// В Update(), где-то рядом с line 400 (jump processing):
if (Keyboard.current.spaceKey.wasPressedThisFrame && IsOwner) {
    SubmitJumpRpc();
}
```

---

## 5. Snapshot sync (server → client)

### 5.1 TargetRPC pattern

```csharp
private void SendSnapshotToOwner(ulong clientId) {
    if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
    var playerObject = client.PlayerObject;
    if (playerObject == null) return;
    var netPlayer = playerObject.GetComponent<NetworkPlayer>();
    if (netPlayer == null) return;

    var stats = _world.GetOrCreateStats(clientId);
    var snap = new StatsSnapshotDto {
        strength = stats.strength,
        dexterity = stats.dexterity,
        intelligence = stats.intelligence,
        strengthTier = stats.strengthTier,
        dexterityTier = stats.dexterityTier,
        intelligenceTier = stats.intelligenceTier,
        strengthXpForNextTier = _config.XpForNextTier(stats.strengthTier),
        dexterityXpForNextTier = _config.XpForNextTier(stats.dexterityTier),
        intelligenceXpForNextTier = _config.XpForNextTier(stats.intelligenceTier),
        strengthTotalXp = stats.strengthTotalXp,
        dexterityTotalXp = stats.dexterityTotalXp,
        intelligenceTotalXp = stats.intelligenceTotalXp,
    };

    netPlayer.ReceiveStatsSnapshotTargetRpc(snap);
}
```

### 5.2 OnPlayerConnected — send initial snapshot

```csharp
public override void OnNetworkSpawn() {
    // ... subscribe ...
    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
}

private void OnClientConnected(ulong clientId) {
    if (!IsServer) return;
    // Load from repository (if exists)
    if (_repository.TryLoad(clientId, out var savedData)) {
        _world.LoadPlayer(clientId, savedData);
    }
    // Send initial snapshot
    SendSnapshotToOwner(clientId);
}

public override void OnNetworkDespawn() {
    if (NetworkManager.Singleton != null)
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    // Save all players + unsubscribe
    foreach (var clientId in _world.GetAllPlayerIds()) {
        _repository.Save(clientId, _world.BuildSaveData(clientId));
    }
    // ... unsubscribe ...
}
```

---

## 6. Persistence (save on disconnect / periodic)

### 6.1 Save триггеры

| Триггер | Когда |
|---------|-------|
| OnNetworkDespawn (server shutdown) | один раз для всех игроков |
| OnClientDisconnect (player leaves) | один раз для уходящего игрока |
| Periodic save (каждые 5 минут) | safety net |
| After tier-up | "major event", надёжно сохранить |
| After large XP gain (>10 XP) | safety net |

### 6.2 Что сохраняем

```csharp
// В StatsWorld.BuildSaveData(clientId):
public CharacterSaveData BuildSaveData(ulong clientId) {
    var stats = GetOrCreateStats(clientId);
    var data = new CharacterSaveData();

    // Stats
    data.stats.strength = stats.strength;
    data.stats.dexterity = stats.dexterity;
    data.stats.intelligence = stats.intelligence;
    data.stats.strengthTier = stats.strengthTier;
    data.stats.dexterityTier = stats.dexterityTier;
    data.stats.intelligenceTier = stats.intelligenceTier;
    data.stats.strengthTotalXp = stats.strengthTotalXp;
    data.stats.dexterityTotalXp = stats.dexterityTotalXp;
    data.stats.intelligenceTotalXp = stats.intelligenceTotalXp;

    // Equipment
    var equip = _equipmentWorld.GetEquipment(clientId);
    Array.Copy(equip.slotOccupied, data.equipment.slotOccupied, EquipmentData.SLOT_COUNT);
    Array.Copy(equip.slotItemIds, data.equipment.slotItemIds, EquipmentData.SLOT_COUNT);

    // Skills
    if (_skillsWorld != null) {
        var learned = _skillsWorld.GetLearnedSkillIds(clientId);
        data.skills.learnedSkillIds = learned.ToArray();
        var cooldowns = _statsServer._lastDialogPerPlayerNpc.TryGetValue(clientId, out var perNpc)
            ? perNpc.Select(kvp => new NpcCooldownSave { npcId = kvp.Key, lastTimestamp = kvp.Value }).ToArray()
            : Array.Empty<NpcCooldownSave>();
        data.skills.dialogCooldowns = cooldowns;
    }

    return data;
}
```

### 6.3 Что НЕ сохраняем (transient)

- `_walkedDistanceBuffer` / `_pilotDistanceBuffer` — расстояние в буфере < threshold теряется при рестарте (acceptable)
- `_lastPosPerPlayer` — последняя позиция (recomputed на первом FixedUpdate)
- `WorldEventBus` subscriptions — переподписываемся на OnNetworkSpawn

---

## 7. Edge cases

### 7.1 Player connects → disconnects → reconnects (rapid)

**Сценарий:** игрок быстро зашёл-вышел-зашёл. Каждый connect → save (from memory), disconnect → save (to file).

**Решение:** Используем `OnClientDisconnect` callback для save. Connect → load (if file exists). Race condition между двумя saves — last-write-wins (acceptable для MVP).

### 7.2 Dedicated server crash — save lost

**Сценарий:** server крашится до того как save завершился.

**Решение:** Periodic save (каждые 5 минут) — safety net. Также `JsonQuestStateRepository.cs:74-85` pattern для atomic write (tmp + Move).

### 7.3 StatsConfig._globalMultiplier changed at runtime (event start)

**Сценарий:** server admin включает "double XP event" — меняет `_globalMultiplier = 2.0` в инспекторе.

**Решение:** Все новые XP gains используют текущее значение `_globalMultiplier`. Уже накопленный XP не пересчитывается (snapshot = текущий, не "с учётом нового множителя").

### 7.4 MiningCompletedEvent fires multiple times in same frame

**Сценарий:** игрок собирает 2 разных ресурса одновременно (невозможно в текущем дизайне, но defensive).

**Решение:** Все handlers stateless — каждый event независимо. Если 2 events в одном frame — 2 ApplyXp, может быть tier-up.

### 7.5 Player walks 0.5m → 0.5m → 0.5m → ... (rapid teleport detection)

**Сценарий:** игрок перемещается по 0.5m каждый кадр (50 Hz) — это 25 m/s, нормальная бегущая скорость. Но если 50 m/s — подозрительно.

**Решение:** Не детектируем anti-cheat в MVP (dedicated server = trusted). Можем добавить max walk speed cap (5 m/s) — warning + no XP for delta beyond cap.

### 7.6 StatsConfig._globalMultiplier = 0 (деление)

**Сценарий:** случайно поставил globalMultiplier = 0.

**Решение:** `[Range(0.01f, 10f)]` — минимум 0.01. Plus warning в OnValidate.

### 7.7 NetworkPlayer._inShip field race (during boarding animation)

**Сценарий:** игрок в процессе boarding (между `SubmitSwitchModeRpc` и `ApplyShipState`). `_inShip` уже true на server, но client ещё не в корабле.

**Решение:** Distance tracker работает на server, читает server-side `_inShip`. Если `_inShip == true` → skip. Это правильно: server-authoritative.

### 7.8 StatTierUpEvent — spam для UI (tier-up +5 levels одновременно)

**Сценарий:** игрок получил огромный XP gain → 5 tier promotions. 5 StatTierUpEvent'ов в одной ApplyXp.

**Решение:** UI toast queue (как `QuestToast` / `GatheringToast`) — handle один, остальные в очередь. Или debounce — показать только последний tier.

---

## 8. Pitfalls

### 8.1 Race condition WorldEventBus subscribers

**Проблема:** Если StatsServer.OnNetworkSpawn срабатывает ПОСЛЕ того как InventoryWorld опубликовал ItemAddedEvent для этого игрока → пропускаем событие.

**Решение:** В OnNetworkSpawn после Subscribe — отправляем текущий snapshot через TargetRPC. Snapshot + deltas = double-safety.

### 8.2 Stat XP formula не учитывает tier как base для следующих

**Проблема:** Игрок на tier 5 накопил 1000 XP. Перешёл на tier 6, ещё 500 XP. Стартовал новый сезон (сброс XP?). Если reset до 0 — unfair.

**Решение:** Пока нет seasonal reset. Tier promotion НЕ сбрасывает totalXp — только currentXp. TotalXp монотонно растёт.

### 8.3 StatsConfig.OnValidate не ловит всё

**Проблема:** Дизайнер поставил `miningXpPerItem = 10000` и `globalMultiplier = 100`. Один mining = 1M XP. Tier promotion spam.

**Решение:** В OnValidate warning если `miningXpPerItem * globalMultiplier > 100` — рекомендация снизить.

### 8.4 Snapshot sync — слишком часто

**Проблема:** Если игрок делает craft → market buy → exchange (3 события в 1 sec), 3 ApplyXp → 3 SendSnapshotToOwner → 3 RPC → bandwidth waste.

**Решение:** Throttle — `SendSnapshotToOwner` не чаще 1 раза в 200ms. Очередь последних snapshot'ов (coalesce).

### 8.5 Persist на disconnect — concurrent file write

**Проблема:** 2 игрока disconnect одновременно → 2 параллельных save.

**Решение:** `JsonCharacterDataRepository.Save` не thread-safe — каждый save делаем через `_saveQueue` + однопоточный save worker coroutine.

### 8.6 StatsClientState.OnStatsUpdated — sync vs async

**Проблема:** CharacterWindow подписан на OnStatsUpdated, handler обновляет UI. Если handler делает `MarkDirtyRepaint()` в каждом update — фризы.

**Решение:** Idempotent handler + update только если `_activeTab == "stats"` (не обновляем скрытую вкладку).

### 8.7 WalkedDistance buffer overflow

**Проблема:** Игрок disconnected посреди 25m буфера. Threshold 10m → 1 XP + 15m остаток. При reconnect — буфер пустой, остаток потерян.

**Решение:** Сохраняем `_walkedDistanceBuffer` в `CharacterSaveData` (transient field). При reconnect — восстанавливаем буфер.

### 8.8 NetworkVariable vs snapshot RPC для stats

**Проблема:** Если бы stats были NetworkVariable на NetworkPlayer — 9 отдельных переменных × sync per change. 3 floats + 3 ints + 3 tierXp = 9 NetworkVariable = 9 NetworkVariable sync overhead per ApplyXp.

**Решение:** Один `StatsSnapshotDto` через TargetRPC. Один snapshot = одно сообщение, batched.

---

## 9. Что НЕ делаем

- ❌ Не храним `Stats` как `NetworkVariable` на `NetworkPlayer` (snapshot RPC проще и быстрее)
- ❌ Не используем `Time.realtimeSinceStartup` для cooldown (drift)
- ❌ Не anti-cheat на dedicated server (trusted)
- ❌ Не seasonal reset XP (пока)
- ❌ Не делаем `WalkedDistanceBuffer` persistence в MVP (acceptable loss < threshold)
- ❌ Не подписываемся на `ItemAddedEvent` для STR (mining proxy слишком неточный — лучше MiningCompletedEvent)
- ❌ Не делаем XP → multiplier on tier-up (XP gain не зависит от tier)
- ❌ Не пишем `.meta` / `.asmdef` файлы
