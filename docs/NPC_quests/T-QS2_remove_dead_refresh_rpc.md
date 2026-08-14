# T-QS2: Удаление мёртвых Refresh-RPC (RequestRefresh*)

**Дата:** 2026-08-13
**Источник:** `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` → пункт **S2**
**Тип:** чистка мёртвого кода (не баг-фикс, не фича, не изменение протокола)

---

## TL;DR

Удалены 3 серверных RPC, у которых не было **ни одного** клиентского вызова:

| RPC | Файл / строка до правки |
|---|---|
| `RequestRefreshQuestsRpc` | `Assets/_Project/Quests/Network/QuestServer.cs:781` |
| `RequestRefreshReputationRpc` | `QuestServer.cs:796` |
| `RequestRefreshNpcAttitudeRpc` | `QuestServer.cs:811` |

Это были «pull-запросы полного снапшота», но клиент квестов работает по **push-модели**,
поэтому RPC оказались мёртвым кодом. Удаление не меняет протокол: сервер и так шлёт
снапшоты при подключении и после каждой мутации состояния.

---

## Что именно удалено

Файл `Assets/_Project/Quests/Network/QuestServer.cs`:

1. Удалены 3 метода вместе с doc-комментариями (`RequestRefreshQuestsRpc`,
   `RequestRefreshReputationRpc`, `RequestRefreshNpcAttitudeRpc`).
2. Обновлена шапка файла — из списка «All RPCs declared» убраны
   `RequestRefreshQuests`, `RequestRefreshReputation`, `RequestRefreshNpcAttitude`.

### Точные сигнатуры (для grep / отката)
=======

```csharp
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestRefreshQuestsRpc(RpcParams rpcParams = default)

[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestRefreshReputationRpc(RpcParams rpcParams = default)

[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestRefreshNpcAttitudeRpc(RpcParams rpcParams = default)
```

Полный текст удалённых методов — в конце этого файла, секция «Rollback».

---

## Почему это безопасно (доказательства)

1. **Ноль вызовов.** `grep "RequestRefresh"` по всему `Assets/_Project` показывает:
   - имена трёх quest-RPC встречаются **только** в `QuestServer.cs` (объявления)
     и в комментарии `QuestSnapshotDto.cs:4`;
   - в клиенте (`QuestClientState`, `CharacterWindow`, `DialogWindow`, `NetworkPlayer`)
     вызовов этих RPC нет.

2. **Клиент квестов работает по push, а не по pull.** `CharacterWindow` не запрашивает
   данные — он пересобирает локальный кэш из уже пришедших DTO:
   - `RefreshQuestsCache()` — `CharacterWindow.cs:3263`
   - `RefreshReputationCache()` — `CharacterWindow.cs:1128`
   - `RefreshNpcAttitudeCache()` — `CharacterWindow.cs:1218`
   - триггеры: `HandleQuestSnapshotUpdated` (3454), `HandleReputationSnapshot` (1194),
     `HandleNpcAttitudeSnapshot` (1206).

3. **Сервер и так шлёт всё по push:**
   - при подключении (`OnNetworkSpawn` → `BroadcastBothChange(clientId)` + `SendQuestSnapshotToClient(clientId)`,
     `QuestServer.cs:1144` и `:1147`);
   - после каждого перехода состояния (`SendQuestSnapshotToClient` в `:472`, `:717`, `:751`,
     `:771`, `:1368`, `:1396`, `:1444`, `:1480`, `:1523`);
   - репутация/attitude — `BroadcastReputationChange` / `BroadcastNpcAttitudeChange` /
     `BroadcastBothChange` / `BroadcastKnowledgeChange` (`:1091`, `:1100`, `:1108`, `:1116`).

4. **RPC-каналы NGO надёжные (reliable ordered).** «Пропущенный» push возможен только
   при реальном reconnect — а reconnect снова запускает `OnNetworkSpawn` → полный push.
   Поэтому pull-«refresh» избыточен.

---

## Что НЕ трогали (важно не перепутать)

- `InventoryServer.RequestRefreshRpc` (`Items`, `InventoryServer.cs:238`) — **живой** RPC.
  Вызывается через `InventoryClientState.RequestRefresh()` (`InventoryClientState.cs:191`)
  из `InventoryUI.cs:562`, `MarketWindow.cs:1521`, `InventoryTab.cs:199/206/262`.
  Это **другая подсистема** (инвентарь, не квесты) и к S2 отношения не имеет.

- Хелперы `BuildQuestSnapshot` / `BuildReputationSnapshot` / `BuildNpcAttitudeSnapshot`
  и сендеры `SendQuestSnapshotToClient` / `SendReputationSnapshotToClient` /
  `SendNpcAttitudeSnapshotToClient` — **остались**, т.к. используются push-путём.

---

## Как теперь выглядит поток данных (единственный источник)

```
[сервер] OnNetworkSpawn
   └─ BroadcastBothChange(clientId)      → reputation + npcAttitude snapshot
   └─ SendQuestSnapshotToClient(clientId) → quest snapshot

[сервер] мутация (TryAccept / TryTurnIn / FireDialogAction / Track / Discover / Fail)
   └─ SendQuestSnapshotToClient(clientId)  (+ Broadcast*Change для rep/attitude)

[клиент] NetworkPlayer.Receive*TargetRpc
   └─ QuestClientState.Raise*/Handle*
      └─ CharacterWindow.Handle*Snapshot
         └─ Refresh*Cache() → перерисовка таба
```

---

## Диагностика багов (если после удаления что-то «сломается»)

> Ключевой момент: **удалённые RPC не могли вызываться**, поэтому любой новый баг
> «не обновился квест/репутация/attitude» — это проблема **push-пути**, а не удаления.

Ищите по симптомам:

| Симптом | Вероятная причина | Куда смотреть |
|---|---|---|
| Квест не появился в `CharacterWindow` после Accept/Discover/TurnIn | сервер не вызвал `SendQuestSnapshotToClient` после конкретного перехода | `QuestServer.cs` — конкретный transition-хэндлер (`FireDialogAction` для AcceptQuest/DiscoverQuest/FailQuest, `TryTurnIn`, `Track`) |
| Репутация / attitude не обновились | сервер не вызвал `Broadcast*Change` после `ModifyReputation` / изменения attitude | `QuestServer.cs` — `BroadcastReputationChange` / `BroadcastNpcAttitudeChange` (`:1091`, `:1100`) |
| UI пуст при первом открытии таба | кэш строится до прихода снапшота, или `CharacterWindow` не подписан на события | `CharacterWindow.cs` — `OnTabShown` (961-963), `Refresh*Cache`, `Handle*Snapshot`; `QuestClientState` — подписка/события |
| После reconnect всё пустое | `OnNetworkSpawn` не отправил начальный snapshot, либо `QuestWorld.Instance == null` в момент спавна | `QuestServer.cs:60-121` (`OnNetworkSpawn`), `:1140-1148` (initial push) |
| «no NetworkPlayer for client» в логе | `FindNetworkPlayer` не нашёл игрока → push молча пропущен | `QuestServer.cs:1044-1054` (`SendQuestSnapshotToClient`) |

### Быстрый способ подтвердить, что данные вообще приходят

- Включить `debugMode` у `QuestServer` → в консоли должны появляться:
  `[QuestServer] SendQuestSnapshotToClient: client=… quests=…`, `BroadcastReputationChange`, `BroadcastNpcAttitudeChange`.
- На клиенте — логи `CharacterWindow.Handle*Snapshot` / `QuestClientState.Handle*`.

Если этих логов нет в момент, когда UI должен был обновиться — проблема в **push-сендере**,
а не в удалённых RPC.

---

## Rollback (как вернуть)

Вариант 1 — git:
```
git revert <commit>
```

Вариант 2 — вручную вернуть три метода в `QuestServer.cs` (например, после `RequestTrackQuestRpc`
или в любом месте внутри класса `QuestServer`):

```csharp
/// <summary>
/// Player requests full quest list snapshot (e.g. при открытии CharacterWindow).
/// T-Q07: real impl — build DTO + send TargetRpc to client.
/// </summary>
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestRefreshQuestsRpc(RpcParams rpcParams = default)
{
    ulong clientId = rpcParams.Receive.SenderClientId;
    if (!CheckRateLimit(clientId)) return;
    if (QuestWorld.Instance == null) return;
    if (debugMode) Debug.Log($"[QuestServer] RequestRefreshQuests client={clientId}");
    var snapshot = BuildQuestSnapshot(clientId);
    SendQuestSnapshotToClient(clientId, snapshot);
}

/// <summary>
/// Player requests full reputation snapshot.
/// T-Q07: real impl — build DTO + send TargetRpc to client.
/// </summary>
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestRefreshReputationRpc(RpcParams rpcParams = default)
{
    ulong clientId = rpcParams.Receive.SenderClientId;
    if (!CheckRateLimit(clientId)) return;
    if (QuestWorld.Instance == null) return;
    if (debugMode) Debug.Log($"[QuestServer] RequestRefreshReputation client={clientId}");
    var snapshot = BuildReputationSnapshot(clientId);
    SendReputationSnapshotToClient(clientId, snapshot);
}

/// <summary>
/// Player requests full NpcAttitude snapshot (per NPC relationship values).
/// T-Q07: real impl — build DTO + send TargetRpc to client.
/// </summary>
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestRefreshNpcAttitudeRpc(RpcParams rpcParams = default)
{
    ulong clientId = rpcParams.Receive.SenderClientId;
    if (!CheckRateLimit(clientId)) return;
    if (QuestWorld.Instance == null) return;
    if (debugMode) Debug.Log($"[QuestServer] RequestRefreshNpcAttitude client={clientId}");
    var snapshot = BuildNpcAttitudeSnapshot(clientId);
    SendNpcAttitudeSnapshotToClient(clientId, snapshot);
}
```

И вернуть в шапку файла список RPC:
```
//     RequestTurnInQuest, RequestTrackQuest, RequestRefreshQuests, RequestRefreshReputation,
//     RequestRefreshNpcAttitude, RequestDiscoverQuest). Stub logic — real impl in T-Q06+.
```

---

## Статус

- [x] Удалены 3 мёртвых RPC.
- [x] Шапка файла обновлена.
- [x] Компиляция чистая (`check_compile_errors` → no errors).
- [ ] Play-тест квестов (accept/turn-in/discover/reconnect) — **выполняет пользователь** (агент не запускает).
