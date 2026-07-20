# Система Знаний (Knowledge System) — Глубокий Технический Анализ

> **Статус:** Анализ завершён, точки сопряжения с `01_KNOWLEDGE_SYSTEM_ANALYSIS.md` сведены.
> **Дата:** 2026-07-20
> **Охват:** Сервер (QuestWorld) → Persistence (QuestSaveData) → DTO (ReputationSnapshotDto / NpcAttitudeSnapshotDto) → Client (ReputationClientState / NpcAttitudeClientState) → UI (CharacterWindow)

---

## 0. Цель и Scope

Ввести server-authoritative систему «знаний» (Knowledge), которая:

1. **Сохраняется** в файл персонажа при trigger-ивентах (диалог с NPC)
2. **Загружается** при входе на сервер (через существующий `LoadPlayer`)
3. **Своевременно обновляется** на клиенте (через существующие snapshot-каналы)
4. **Фильтрует UI**: пока игрок не «знает» о фракции/NPC — эта запись не показывается в CharacterWindow → Репутация

v1: фракции + NPC. Архитектура рассчитана на расширение (локации, предметы, рецепты).

---

## 1. Текущая Архитектура — Полная Карта Зависимостей (что мы трогаем, что НЕ трогаем)

### 1.1. Поток данных от сервера к UI

```
QuestWorld (сервер)
  │
  ├── BuildSaveData() ──────────────────────────► QuestSaveData (JSON-файл, disk persistence)
  │        ↑ (сохраняется при каждом SavePlayer)
  │
  ├── BuildReputationSnapshot(clientId) ────────► ReputationSnapshotDto ──┐
  │       (итерация по ВСЕМ FactionId 0..15)                              │
  │                                                                        ├──► NetworkPlayer.ReceiveReputationSnapshotTargetRpc
  │                                                                        │       │
  ├── BuildNpcAttitudeSnapshot(clientId) ────────► NpcAttitudeSnapshotDto ─┘       │
  │       (итерация по targetNpcId из всех      │                                  │
  │        квестовых objectives)                 │                                  │
  │                                              │                                  │
  │     ┌────────────────────────────────────────┘                                  │
  │     │                                                                           │
  │     ▼                                                                           ▼
  │  ReputationClientState.OnReputationSnapshotReceived ◄───────────────────────────┘
  │  NpcAttitudeClientState.OnNpcAttitudeSnapshotReceived ◄──────────────────────────┘
  │     │
  │     ▼
  │  CharacterWindow.RefreshReputationCache()
  │  CharacterWindow.RefreshNpcAttitudeCache()
  │     │
  │     ▼
  │  ListView (reputationList / npcAttitudeList)
  │
  └── MarkNpcTalked(clientId, npcId) ───────► _npcTalkedTo[clientId].Add(npcId) + SavePlayer()
```

### 1.2. Ключевые структуры данных (все в одном месте для сверки)

| Где | Структура | Ключ | Значение | Назначение |
|---|---|---|---|---|
| QuestWorld | `_reputation` | `(ulong clientId, FactionId)` | `int` | Репутация с фракцией |
| QuestWorld | `_npcAttitude` | `(ulong clientId, string npcId)` | `int` | Персональное отношение с NPC |
| QuestWorld | `_npcTalkedTo` | `ulong clientId` | `HashSet<string>` | **Уже есть** — с кем говорил |
| QuestWorld | `_eventsOccurred` | `ulong clientId` | `HashSet<string>` | События |
| QuestWorld | `_worldFlags` | `(ulong, string)` | `bool` | Флаги |
| QuestWorld | `_dialogByPlayer` | `ulong clientId` | `DialogSession` | Активные диалоги (не персистятся) |
| QuestSaveData | `stringSets` | `(setName, string[])` | JSON | npcTalkedTo — уже персистится! |

### 1.3. Что НЕ Меняем (Safety Zone)

| Компонент | Причина |
|---|---|
| `QuestWorld.ModifyReputation()` | Логика изменения репутации не затрагивается |
| `QuestWorld.ModifyNpcAttitude()` | Логика изменения отношения не затрагивается |
| `QuestWorld.TryOffer/TryAccept/TryTurnIn/TryAdvanceStage` | Логика квестов не фильтруется знанием |
| Dialog system (`QuestServer.RequestOpenDialogueRpc`, `FireDialogAction`) | `MarkNpcTalked` уже вызывается, мы только добавляем side-effect |
| `JsonQuestStateRepository` | Новая data passes through as-is (два новых поля) |
| `IQuestStateRepository` interface | Не меняется — это transparent pipe |
| `FactionDefinition`, `NpcDefinition` SO | Data assets не меняются |
| `FactionId` enum | Не меняется |
| `NetworkPlayer` RPCs | Не добавляем новые RPC — расширяем существующие DTO |
| BootstrapScene | Не меняется |

---

## 2. Детальный Технический Проект

### 2.1. Сервер: QuestWorld — Новые Dictionaries

**Файл:** `Assets/_Project/Quests/Core/QuestWorld.cs`

Добавить два поля (рядом с `_npcTalkedTo`):

```csharp
/// <summary>T-KNOW: какие фракции игрок «знает» (имеет право видеть в UI).</summary>
private readonly Dictionary<ulong, HashSet<FactionId>> _knownFactions = new();

/// <summary>T-KNOW: какие NPC игрок «знает». Отдельно от _npcTalkedTo — в будущем
/// знание может открываться через книги/квесты, а не только через диалог.</summary>
private readonly Dictionary<ulong, HashSet<string>> _knownNpcs = new();
```

**Решение: отдельный `_knownNpcs`, не реюз `_npcTalkedTo`.**

Несмотря на то что сейчас они эквивалентны, семантика разная:
- `_npcTalkedTo` = технический флаг «была ли открыта диалоговая сессия»  
- `_knownNpcs` = семантическое знание «игрок знает об этом NPC и может видеть его в UI»

В будущем (v2):
- Книга-артефакт → `_knownNpcs` без `_npcTalkedTo`
- Случайная встреча в мире → `_knownNpcs` без `_npcTalkedTo`
- Но `_npcTalkedTo` всегда триггерит `_knownNpcs`

### 2.2. Сервер: QuestWorld — Новые Методы

```csharp
// ============ T-KNOW: Knowledge ============

public bool IsFactionKnown(ulong clientId, FactionId faction)
{
    return _knownFactions.TryGetValue(clientId, out var set) && set.Contains(faction);
}

public bool IsNpcKnown(ulong clientId, string npcId)
{
    if (string.IsNullOrEmpty(npcId)) return false;
    return _knownNpcs.TryGetValue(clientId, out var set) && set.Contains(npcId);
}

public void UnlockFactionKnowledge(ulong clientId, FactionId faction)
{
    if (faction == FactionId.None) return;
    if (!_knownFactions.TryGetValue(clientId, out var set))
    {
        set = new HashSet<FactionId>();
        _knownFactions[clientId] = set;
    }
    if (set.Add(faction))
    {
        if (Debug.isDebugBuild)
            Debug.Log($"[QuestWorld] Knowledge unlocked: player={clientId} faction={faction}");
        // NOT calling SavePlayer here — caller (MarkNpcTalked) already does
    }
}

public void UnlockNpcKnowledge(ulong clientId, string npcId)
{
    if (string.IsNullOrEmpty(npcId)) return;
    if (!_knownNpcs.TryGetValue(clientId, out var set))
    {
        set = new HashSet<string>();
        _knownNpcs[clientId] = set;
    }
    set.Add(npcId);
    // NOT calling SavePlayer — caller already does
}
```

### 2.3. Сервер: MarkNpcTalked — расширение

**Current code (line 755-764):**
```csharp
public void MarkNpcTalked(ulong clientId, string npcId)
{
    if (string.IsNullOrEmpty(npcId)) return;
    if (!_npcTalkedTo.TryGetValue(clientId, out var set))
    {
        set = new HashSet<string>();
        _npcTalkedTo[clientId] = set;
    }
    if (set.Add(npcId)) SavePlayer(clientId); // T-Q18
}
```

**After:**
```csharp
public void MarkNpcTalked(ulong clientId, string npcId)
{
    if (string.IsNullOrEmpty(npcId)) return;
    if (!_npcTalkedTo.TryGetValue(clientId, out var set))
    {
        set = new HashSet<string>();
        _npcTalkedTo[clientId] = set;
    }
    bool needSave = set.Add(npcId); // T-Q18

    // T-KNOW: unlock NPC knowledge (always — даже если уже был в _npcTalkedTo,
    // чтобы наверняка знание было; idempotent через HashSet)
    UnlockNpcKnowledge(clientId, npcId);

    // T-KNOW: unlock faction knowledge via npcDefinition.faction
    if (Database != null && !string.IsNullOrEmpty(npcId))
    {
        var npcDef = Database.GetNpc(npcId);
        if (npcDef != null && npcDef.faction != FactionId.None)
        {
            UnlockFactionKnowledge(clientId, npcDef.faction);
        }
    }

    if (needSave) SavePlayer(clientId);
}
```

**Важно:** Сохраняем `needSave` — только если действительно `set.Add` дал true, чтобы не было лишних вызовов SavePlayer при повторном диалоге. `Unlock*Knowledge` сами не вызывают SavePlayer.

### 2.4. Persistence: QuestSaveData — два новых поля

**Файл:** `Assets/_Project/Quests/Persistence/QuestSaveData.cs`

```csharp
// === T-KNOW ===
[SerializeField] public List<int> knownFactions = new List<int>();   // FactionId как int для JsonUtility
[SerializeField] public List<string> knownNpcs = new List<string>();
```

**Риск:** `JsonUtility` требует `[Serializable]` на самом классе (есть), но `List<int>` и `List<string>` сериализуются корректно.  
**Backward compat:** Пустые списки для старых сейвов — при первом диалоге знания откроются.

### 2.5. Persistence: BuildSaveData / LoadPlayer — интеграция knowledge

**BuildSaveData (добавить блок после stringSets, перед return):**

```csharp
// T-KNOW: known factions
if (_knownFactions.TryGetValue(clientId, out var knownFactionsSet) && knownFactionsSet.Count > 0)
{
    foreach (var fid in knownFactionsSet)
        data.knownFactions.Add((int)fid);
}

// T-KNOW: known NPCs
if (_knownNpcs.TryGetValue(clientId, out var knownNpcsSet) && knownNpcsSet.Count > 0)
{
    data.knownNpcs.AddRange(knownNpcsSet);
}

// T-KNOW: Neutral (11) auto-known для новых персонажей — гарантируем что всегда в сейве
if (!data.knownFactions.Contains((int)FactionId.Neutral))
    data.knownFactions.Add((int)FactionId.Neutral);
```

**LoadPlayer (добавить блок после stringSets, перед Debug.Log):**

```csharp
// T-KNOW: restore known factions
if (data.knownFactions != null && data.knownFactions.Count > 0)
{
    var knownF = new HashSet<FactionId>();
    foreach (int id in data.knownFactions)
        knownF.Add((FactionId)id);
    _knownFactions[clientId] = knownF;
}
else
{
    // New player / old save: auto-know Neutral
    _knownFactions[clientId] = new HashSet<FactionId> { FactionId.Neutral };
}

// T-KNOW: restore known NPCs
if (data.knownNpcs != null && data.knownNpcs.Count > 0)
{
    _knownNpcs[clientId] = new HashSet<string>(data.knownNpcs);
}
else
{
    _knownNpcs[clientId] = new HashSet<string>();
}
```

**Зачем Neutral auto-known в BuildSaveData:** даже если при первом входе Neutral был добавлен в `_knownFactions`, нужно гарантировать, что он попадёт в save. Если сервер упадёт до первого `SavePlayer`, Neutral не сохранится — но при `LoadPlayer` (else-ветка) мы снова его добавим. Двойная страховка (BuildSaveData + LoadPlayer) — intentional.

### 2.6. Зачистка в Shutdown()

Добавить в `QuestWorld.Shutdown()` (после `_worldFlags.Clear()`):
```csharp
_knownFactions.Clear();
_knownNpcs.Clear();
```

### 2.7. DTO: ReputationSnapshotDto — расширение

Самый простой путь (минимальное количество новых файлов):

**Вариант A (рекомендуется):** Добавить `byte[] knownFactionIds` в `ReputationSnapshotDto`.

```csharp
public struct ReputationSnapshotDto : INetworkSerializable
{
    public ReputationEntryDto[] entries;
    public byte[] knownFactionIds;  // T-KNOW: какие фракции показывать

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        int len = entries?.Length ?? 0;
        s.SerializeValue(ref len);
        if (s.IsReader) entries = len > 0 ? new ReputationEntryDto[len] : null;
        for (int i = 0; i < len; i++)
        {
            var e = entries != null ? entries[i] : default;
            e.NetworkSerialize(s);
            if (entries != null) entries[i] = e;
        }
        // T-KNOW: serialize known faction ids
        int knownLen = knownFactionIds?.Length ?? 0;
        s.SerializeValue(ref knownLen);
        if (s.IsReader) knownFactionIds = knownLen > 0 ? new byte[knownLen] : null;
        for (int i = 0; i < knownLen; i++)
        {
            byte val = knownFactionIds != null ? knownFactionIds[i] : (byte)0;
            s.SerializeValue(ref val);
            if (knownFactionIds != null) knownFactionIds[i] = val;
        }
    }
}
```

Аналогично для `NpcAttitudeSnapshotDto` — добавить `public string[] knownNpcIds`.

**Почему так, а не отдельный DTO:**
1. Ноль новых RPC-каналов (экономия сетевого оверхеда)
2. Knowledge синхронизирован с reputation snapshot — arrives at the same time, никакого race condition вида «я знаю фракцию, но репутация ещё не пришла»
3. Клиентский код проще: `ReputationClientState` держит и reputation, и known-фильтр в одном месте

**Минус:** При изменении reputation без изменения knowledge — лишние `knownFactionIds` байты в пакете. Но 16 байт на 15 factionId — пренебрежимо.

**Альтернатива — Вариант B (отдельный `KnowledgeSnapshotDto`):**
Чище архитектурно, но:
- Новый RPC (3-й канал к существующим Reputation + NpcAttitude)
- Дополнительный раунд синхронизации
- `KnowledgeClientState` must wait for `ReputationClientState` to arrive — race condition
- В 3-4 раза больше изменяемых файлов

**Вывод: Вариант A — практичнее для v1.**

### 2.8. Сервер: QuestServer.BuildReputationSnapshot — фильтрация

**Current (lines 934-947):** Отправляет ВСЕ 15 фракций.

**After:** Отправляем knownFactionIds отдельно, но entries остаются полными. Почему?

**Решение:** entries отправляем **все** (фракции + значения репутации), а knownFactionIds — как фильтр на клиенте. Это даёт:
1. Если репутация изменилась (через `BroadcastReputationChange`) — клиент получает новый entry value, но фильтр не меняется
2. Если knowledge изменился (через `BroadcastKnowledgeChange`) — мы можем отправить тоже через этот же механизм (просто новый snapshot с обновлённым knownFactionIds)
3. Данные не теряются: если убрать фракцию из снапшота, а потом добавить — value будет 0, а не реальное значение. Сейчас value сохраняется всегда.

```csharp
private ReputationSnapshotDto BuildReputationSnapshot(ulong clientId)
{
    var w = QuestWorld.Instance;
    if (w == null) return new ReputationSnapshotDto { entries = null };

    var arr = new System.Collections.Generic.List<ReputationEntryDto>();
    foreach (ProjectC.Factions.FactionId fid in System.Enum.GetValues(typeof(ProjectC.Factions.FactionId)))
    {
        if (fid == ProjectC.Factions.FactionId.None) continue;
        int v = w.GetReputation(clientId, fid);
        arr.Add(new ReputationEntryDto { faction = (byte)fid, value = v });
    }

    // T-KNOW: build known faction ids array
    var knownList = new System.Collections.Generic.List<byte>();
    foreach (ProjectC.Factions.FactionId fid in System.Enum.GetValues(typeof(ProjectC.Factions.FactionId)))
    {
        if (fid == ProjectC.Factions.FactionId.None) continue;
        if (w.IsFactionKnown(clientId, fid))
            knownList.Add((byte)fid);
    }

    return new ReputationSnapshotDto
    {
        entries = arr.ToArray(),
        knownFactionIds = knownList.ToArray()
    };
}
```

Аналогично для `BuildNpcAttitudeSnapshot`:

```csharp
private NpcAttitudeSnapshotDto BuildNpcAttitudeSnapshot(ulong clientId)
{
    var w = QuestWorld.Instance;
    if (w == null) return new NpcAttitudeSnapshotDto { entries = null };

    // Build all NPC ids from quest objectives (existing logic)
    var allNpcIds = new System.Collections.Generic.HashSet<string>();
    foreach (var def in w.GetAllQuests())
    {
        if (def == null) continue;
        for (int s = 0; s < def.stages.Length; s++)
        {
            for (int o = 0; o < def.stages[s].objectives.Length; o++)
            {
                var obj = def.stages[s].objectives[o];
                if (obj != null && !string.IsNullOrEmpty(obj.targetNpcId))
                    allNpcIds.Add(obj.targetNpcId);
            }
        }
    }

    // T-KNOW: filter by known NPCs
    var arr = new System.Collections.Generic.List<NpcAttitudeEntryDto>();
    var knownNpcList = new System.Collections.Generic.List<string>();
    foreach (var npcId in allNpcIds)
    {
        int v = w.GetNpcAttitude(clientId, npcId);
        arr.Add(new NpcAttitudeEntryDto { npcId = npcId, value = v });
        if (w.IsNpcKnown(clientId, npcId))
            knownNpcList.Add(npcId);
    }

    return new NpcAttitudeSnapshotDto
    {
        entries = arr.ToArray(),
        knownNpcIds = knownNpcList.ToArray()
    };
}
```

### 2.9. Клиент: ReputationClientState — два новых поля

```csharp
// ============ T-KNOW: Known entities ============
public HashSet<byte> KnownFactionIds { get; private set; } = new HashSet<byte>();
public HashSet<string> KnownNpcIds { get; private set; } = new HashSet<string>();

public void OnReputationSnapshotReceived(ReputationSnapshotDto snapshot)
{
    CurrentReputation = snapshot;

    // T-KNOW: update known factions
    KnownFactionIds.Clear();
    if (snapshot.knownFactionIds != null)
    {
        for (int i = 0; i < snapshot.knownFactionIds.Length; i++)
            KnownFactionIds.Add(snapshot.knownFactionIds[i]);
    }
    // Always ensure Neutral (11) is known — server-side гарантирует, но на клиенте тоже страховка
    KnownFactionIds.Add((byte)ProjectC.Factions.FactionId.Neutral);

    OnReputationUpdated?.Invoke(snapshot);
    if (Debug.isDebugBuild)
        Debug.Log($"[ReputationClientState] OnReputationSnapshotReceived: {snapshot.entries?.Length ?? 0} factions, {KnownFactionIds.Count} known");
}
```

Аналогично для `NpcAttitudeClientState`:

```csharp
public HashSet<string> KnownNpcIds { get; private set; } = new HashSet<string>();

public void OnNpcAttitudeSnapshotReceived(NpcAttitudeSnapshotDto snapshot)
{
    CurrentNpcAttitude = snapshot;

    KnownNpcIds.Clear();
    if (snapshot.knownNpcIds != null)
    {
        for (int i = 0; i < snapshot.knownNpcIds.Length; i++)
            KnownNpcIds.Add(snapshot.knownNpcIds[i]);
    }

    OnNpcAttitudeUpdated?.Invoke(snapshot);
}
```

### 2.10. UI: CharacterWindow — фильтрация

**RefreshReputationCache (lines 959-1012):**

Текущая логика — три режима:
1. Нет snapshot → FactionFallback (5 hardcoded)
2. Snapshot есть, entries null/empty → FactionFallback
3. Snapshot есть → все entries

**После фильтрации:**

```csharp
private void RefreshReputationCache()
{
    _reputationCache.Clear();
    var repState = ReputationClientState.Instance;

    if (repState == null || !repState.CurrentReputation.HasValue)
    {
        // Snapshot ещё не пришёл — показываем только Neutral (known всем)
        // Placeholder для новых игроков: пустой список — нормально
        if (Debug.isDebugBuild) Debug.Log("[CharacterWindow] RefreshReputationCache: no snapshot yet, showing known-only");
        // Nothing added — UI увидит пустой список, что корректно для нового персонажа
    }
    else
    {
        var entries = repState.CurrentReputation.Value.entries;
        var knownIds = repState.KnownFactionIds;
        if (entries != null && knownIds != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                byte factionByte = e.faction;
                // T-KNOW: только known faction
                if (!knownIds.Contains(factionByte)) continue;

                var fid = (FactionId)factionByte;
                var fb = FindFactionFallback(fid);
                _reputationCache.Add(new ReputationListItem
                {
                    factionId = fb.id,
                    displayName = fb.name,
                    value = e.value,
                    color = fb.color
                });
            }
        }

        if (_reputationCache.Count == 0)
        {
            // Если после фильтрации пусто — показываем хотя бы Neutral
            var neutralFb = FindFactionFallback(FactionId.Neutral);
            _reputationCache.Add(new ReputationListItem
            {
                factionId = neutralFb.id,
                displayName = neutralFb.name,
                value = repState.CurrentReputation.HasValue
                    ? GetRepValueForFaction(repState.CurrentReputation.Value.entries, (byte)FactionId.Neutral)
                    : 0,
                color = neutralFb.color
            });
        }
    }

    if (_reputationList != null)
    {
        _reputationList.itemsSource = _reputationCache;
        _reputationList.Rebuild();
    }
}

/// <summary>T-KNOW: найти значение репутации для конкретной фракции в массиве entries.</summary>
private static int GetRepValueForFaction(ReputationEntryDto[] entries, byte factionId)
{
    if (entries == null) return 0;
    for (int i = 0; i < entries.Length; i++)
    {
        if (entries[i].faction == factionId) return entries[i].value;
    }
    return 0;
}
```

**RefreshNpcAttitudeCache (lines 1053-1085) — аналогично:**

```csharp
private void RefreshNpcAttitudeCache()
{
    _npcAttitudeCache.Clear();
    var attState = NpcAttitudeClientState.Instance;
    if (attState != null && attState.CurrentNpcAttitude.HasValue)
    {
        var entries = attState.CurrentNpcAttitude.Value.entries;
        var knownNpcIds = attState.KnownNpcIds;
        if (entries != null && knownNpcIds != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                // T-KNOW: только known NPC
                if (!knownNpcIds.Contains(e.npcId)) continue;

                string displayName = FormatNpcDisplayName(e.npcId);
                Color c = e.value > 0
                    ? new Color(0.5f, 0.85f, 0.5f)
                    : (e.value < 0 ? new Color(0.95f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f));
                _npcAttitudeCache.Add(new NpcAttitudeListItem
                {
                    npcId = e.npcId,
                    displayName = displayName,
                    value = e.value,
                    color = c
                });
            }
        }
    }
    if (_npcAttitudeList != null)
    {
        _npcAttitudeList.itemsSource = _npcAttitudeCache;
        _npcAttitudeList.Rebuild();
    }
}
```

### 2.11. BroadcastKnowledgeChange — новый метод в QuestServer

После изменения knowledge (новый диалог) reputation snapshot уже отправляется?  
**Да** (через `BroadcastReputationChange`)?

**Чек:** `MarkNpcTalked` вызывается из `QuestServer.RequestOpenDialogueRpc` (line 506) и `FireDialogAction` (line 850). После `MarkNpcTalked` вызывается ли `BroadcastReputationChange`?

**Поиск показывает:** `MarkNpcTalked` только сохраняет, НЕ триггерит broadcast. Для reputation изменения — `BroadcastReputationChange` вызывается отдельно. Для knowledge — нужно добавить broadcast:

```csharp
/// <summary>T-KNOW: отправляем свежий reputation snapshot (с knownFactionIds) клиенту после изменения knowledge.</summary>
public void BroadcastKnowledgeChange(ulong clientId)
{
    if (!IsServer) return;
    var snapshot = BuildReputationSnapshot(clientId);
    SendReputationSnapshotToClient(clientId, snapshot);
    var npcSnapshot = BuildNpcAttitudeSnapshot(clientId);
    SendNpcAttitudeSnapshotToClient(clientId, npcSnapshot);
}
```

И вызывать его в `QuestServer.RequestOpenDialogueRpc` после `MarkNpcTalked` (если это первый разговор с NPC данной фракции).

**Оптимизация:** не вызывать `BroadcastKnowledgeChange` если knowledge уже был. Можно добавить `bool knowledgeChanged` как out-параметр:

```csharp
public bool MarkNpcTalked(ulong clientId, string npcId)
{
    // ... existing logic ...
    bool hadFactionBefore = IsFactionKnown(clientId, npcDef?.faction ?? FactionId.None);
    // ... unlock ...
    bool knowledgeChanged = !hadFactionBefore && npcDef != null && npcDef.faction != FactionId.None;
    // ...
    return knowledgeChanged;
}
```

Но для v1 можно просто вызывать `BroadcastKnowledgeChange` всегда после `MarkNpcTalked` — ReputationSnapshot отправляется часто, один лишний раз не повредит.

---

## 3. Изменяемые Файлы — Итоговая Таблица

| # | Файл | Тип изменений | Строки |
|---|---|---|---|
| 1 | `QuestWorld.cs` | +2 Dictionary `_knownFactions`, `_knownNpcs` | ~30 |
| 2 | `QuestWorld.cs` | +6 методов (IsFactionKnown, IsNpcKnown, UnlockFactionKnowledge, UnlockNpcKnowledge, IsFactionKnown, IsNpcKnown) | ~40 |
| 3 | `QuestWorld.cs` | Модификация `MarkNpcTalked` (+ unlock knowledge) | ~10 |
| 4 | `QuestWorld.cs` | Модификация `BuildSaveData` (+ knownFactions, knownNpcs) | ~15 |
| 5 | `QuestWorld.cs` | Модификация `LoadPlayer` (+ restore known) | ~20 |
| 6 | `QuestWorld.cs` | Модификация `Shutdown` (+ clear known) | ~2 |
| 7 | `QuestSaveData.cs` | +2 поля (knownFactions, knownNpcs) | ~4 |
| 8 | `ReputationSnapshotDto.cs` | +поле knownFactionIds в ReputationSnapshotDto, +поле knownNpcIds в NpcAttitudeSnapshotDto | ~20 |
| 9 | `QuestServer.cs` | Модификация `BuildReputationSnapshot` (+ knownFactionIds) | ~10 |
| 10 | `QuestServer.cs` | Модификация `BuildNpcAttitudeSnapshot` (+ knownNpcIds) | ~10 |
| 11 | `QuestServer.cs` | + метод `BroadcastKnowledgeChange` | ~12 |
| 12 | `ReputationClientState.cs` | + HashSet KnownFactionIds, HashSet KnownNpcIds, обновление в обработчике | ~20 |
| 13 | `NpcAttitudeClientState.cs` | + HashSet KnownNpcIds, обновление в обработчике | ~10 |
| 14 | `CharacterWindow.cs` | Фильтрация в `RefreshReputationCache` + `RefreshNpcAttitudeCache` | ~40 |
| | **Итого** | | **~243 строки** |

**Новых файлов:** 0. Ни одного нового `.cs`, SO, префаба или сцены.

---

## 4. Риски, Которые Пропущены в Предыдущем Анализе

### Риск 4.1: FactionFallback в CharacterWindow — конфликт с knowledge

**Проблема:** `FactionFallback` (5 hardcoded записей с названиями GDD-23) используется как placeholder **во всех трёх режимах** RefreshReputationCache. Если snapshot не пришёл — показываем 5 фракций. Если пришёл — всё равно показываем через FindFactionFallback.

С knowledge это сломается: новый игрок откроет CharacterWindow, увидит 5 фракций (потому что snapshot ещё не пришёл), хотя знает только Neutral.

**Решение:** Fallback должен быть пустым или только Neutral — как описано в §2.10 выше.

### Риск 4.2: Client-side stampede при `BroadcastKnowledgeChange`

**Проблема:** Если `BroadcastKnowledgeChange` + `BroadcastReputationChange` вызываются подряд (например, при диалоге меняется и reputation, и knowledge), клиент получит два snapshot-пакета и дважды перестроит UI.

**Решение:** `BroadcastKnowledgeChange` включает reputation snapshot — не нужно вызывать `BroadcastReputationChange` отдельно. Если reputation изменилась через `ModifyReputation` (вызывается `SavePlayer` + event, НО не broadcast), то нужно убедиться что `BroadcastReputationChange` вызывается после — текущий код это уже делает (line 1028-1032 QuestServer).

### Риск 4.3: FactionId.None проходит фильтр

FactionId.None = 0. Если NPC имеет `faction = None` — его знание не будет открыто через `UnlockFactionKnowledge`. Это корректно. Но в `BuildReputationSnapshot` мы пропускаем `None` через `if (fid == FactionId.None) continue;` — так и остаётся.

**Никаких действий.**

### Риск 4.4: NullReference в CharacterWindow после refresh

`ReputationClientState.Instance` может быть null если scene-placed объект не создался. Текущий код проверяет `repState == null` — OK.
Но `knownIds` может быть null если snapshot с пустым `knownFactionIds = null` (а не пустой массив). В коде §2.10 есть `knownIds != null` check.

### Риск 4.5: Race condition при первом входе

Последовательность:
1. `QuestServer.OnNetworkSpawn` → `QuestWorld.CreateAndInitialize` → `LoadPlayer(clientId)` — знание Neutral установлено
2. `QuestServer` вызывает `SendReputationSnapshotToClient` / `SendNpcAttitudeSnapshotToClient` для нового клиента
3. Клиент получает snapshot, обновляет `KnownFactionIds` — Neutral есть
4. CharacterWindow открывается — видит Neutral (или другое если уже говорил)

**Этот сценарий покрыт.** Единственная проблема: если snapshot ещё не пришёл к моменту открытия CharacterWindow — UI покажет пустой список. Это допустимое поведение (см. §2.10 — пустой список при отсутствии snapshot).

---

## 5. Сверка с Существующим Анализом (01_KNOWLEDGE_SYSTEM_ANALYSIS.md)

### Точки согласия

| Пункт | 01_ANALYSIS | 02_ANALYSIS (этот) |
|---|---|---|
| `_knownFactions` как `Dictionary<ulong, HashSet<FactionId>>` | ✅ | ✅ |
| `_knownNpcs` отдельно от `_npcTalkedTo` | ❌ (предлагает реюз) | ✅ (отдельно — семантическая разница) |
| Neutral (11) auto-known | ✅ | ✅ + двойная страховка в LoadPlayer + BuildSaveData |
| `MarkNpcTalked` → unlock faction | ✅ | ✅ |
| KnownFactionIds как `byte[]` в DTO | ✅ (рекомендует расширить существующие DTO) | ✅ (Вариант A) |
| QuestSaveData.knownFactions как `List<int>` | ✅ | ✅ |
| Фильтрация в CharacterWindow | ✅ | ✅ + детали по fallback и граничным случаям |
| Отдельный `KnowledgeSnapshotDto` | ❌ (рекомендует расширение существующих) | ❌ (те же аргументы) |

### Точки расхождения

| Пункт | 01_ANALYSIS | 02_ANALYSIS (этот) | Резолюция |
|---|---|---|---|
| `_knownNpcs` реюз `_npcTalkedTo` | Да (можно реиспользовать) | Нет (отдельная структура) | **02_ANALYSIS** — отдельно. В будущем `_knownNpcs` будет пополняться из других источников (книги, квесты) |
| FactionFallback placeholder в UI | Не упомянут | Детальный разбор и замена на пустой/Neutral | **02_ANALYSIS** закрывает gap |
| BroadcastKnowledgeChange | Не описан | Новый метод в QuestServer | **02_ANALYSIS** — необходим для своевременного обновления UI |
| ReputationSnapshot отправляет все entries + knownFactionIds | Не уточнено | Да — entries полные, knownFactionIds как фильтр | **02_ANALYSIS** — детализация |
| QuestServer.RequestOpenDialogueRpc broadcast | Не описан | Нужно добавить вызов BroadcastKnowledgeChange после MarkNpcTalked | **02_ANALYSIS** |

### Рекомендуемый финальный план — синтез

1. **Persistence** (QuestSaveData — +2 поля) — из 01_ANALYSIS шаг 1
2. **Server state** (QuestWorld — _knownFactions, _knownNpcs, методы) — из 01_ANALYSIS шаг 2 + 02_ANALYSIS §2.1-2.2
3. **MarkNpcTalked** (QuestWorld — unlock + faction) — из 01_ANALYSIS шаг 3 + 02_ANALYSIS §2.3
4. **Save/Load** (BuildSaveData, LoadPlayer) — 01_ANALYSIS шаг 4 + 02_ANALYSIS §2.5
5. **DTO расширение** (ReputationSnapshotDto, NpcAttitudeSnapshotDto — +knownFactionIds / knownNpcIds) — 01_ANALYSIS шаг 5 + 02_ANALYSIS §2.7
6. **Серверная сборка** (QuestServer.BuildReputationSnapshot / BuildNpcAttitudeSnapshot) — 02_ANALYSIS §2.8
7. **Клиентские стейты** (ReputationClientState, NpcAttitudeClientState — +KnownFactionIds/KnownNpcs) — 01_ANALYSIS шаг 6 + 02_ANALYSIS §2.9
8. **BroadcastKnowledgeChange** — 02_ANALYSIS §2.11
9. **UI фильтрация** (CharacterWindow — RefreshReputationCache / RefreshNpcAttitudeCache) — 01_ANALYSIS шаг 8 + 02_ANALYSIS §2.10
10. **Зачистка Fallback** — 02_ANALYSIS §4.1

---

## 6. Архитектурные Решения (ADRs)

### ADR-1: Knowledge — server-authoritative

**Решение:** Все изменения knowledge происходят на сервере. Клиент только потребляет `knownFactionIds`/`knownNpcIds` из snapshot.

**Обоснование:** Исключение читерства (клиент не может сам себе «открыть» знание).

### ADR-2: Единый канал синхронизации (reputation snapshot)

**Решение:** Knowledge передаётся клиенту внутри существующих `ReputationSnapshotDto` и `NpcAttitudeSnapshotDto`, а не отдельным RPC.

**Обоснование:** Нет race condition между reputation и knowledge на клиенте. Меньше кода. Пренебрежимо малый сетевой оверхед.

### ADR-3: KnownNpcs — отдельная структура

**Решение:** `_knownNpcs` — отдельный `Dictionary<ulong, HashSet<string>>`, не реюз `_npcTalkedTo`.

**Обоснование:** Будущее расширение. `_npcTalkedTo` = technical flag (был диалог). `_knownNpcs` = semantic flag (игрок знает об NPC). В будущем: прочитал книгу → known, но не talked.

### ADR-4: All-entries отправка + фильтр

**Решение:** `ReputationSnapshotDto.entries` содержит ВСЕ фракции (с их значениями), а `knownFactionIds` — какие показать. Клиент фильтрует сам.

**Обоснование:** Репутация меняется чаще knowledge. При `BroadcastReputationChange` мы не хотим переотправлять knowledge (он не изменился). При `BroadcastKnowledgeChange` — отправляем и то, и другое. Данные не теряются.

---

## 7. Оценка Трудозатрат (Уточнённая)

| Шаг | Компонент | Файлы | Оценка |
|---|---|---|---|
| 1 | Persistence: QuestSaveData | 1 | 10 мин |
| 2 | Server state: QuestWorld — новые поля + методы | 1 | 30 мин |
| 3 | Server logic: MarkNpcTalked + Save/Load | 1 | 30 мин |
| 4 | DTO: ReputationSnapshotDto + NpcAttitudeSnapshotDto | 1 | 20 мин |
| 5 | Server: QuestServer — Build-методы + BroadcastKnowledgeChange | 1 | 20 мин |
| 6 | Client: ReputationClientState + NpcAttitudeClientState | 2 | 20 мин |
| 7 | UI: CharacterWindow — фильтрация + убрать FactionFallback | 1 | 30 мин |
| 8 | Тестирование | — | 1 ч |
| | **Итого** | **8 файлов** | **~3-4 ч** (против 5-7ч в 01_ANALYSIS — оптимизация за счёт reuse existing DTO) |

---

## 8. Что Не Покрывает v1 (Будущие Тикеты)

- **Knowledge для локаций** (map system) — `_knownLocations` в QuestWorld
- **Knowledge для предметов/рецептов** — отдельная система крафта
- **Knowledge для кораблей/фракций NPC-кораблей** — отдельная система
- **Quest-gated knowledge** (knowledge открывается как награда за квест)
- **Knowledge decay** (забывание фракции если долго не взаимодействовал)
- **Admin tools** для просмотра/редактирования knowledge
- **Батч-инициализация** knowledge для существующих персонажей (миграция)
