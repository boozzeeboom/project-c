# T-Q18 — Persistence + ApplyQuestRewards (large, 90 мин) ✅ DONE 2026-06-08

**Дата:** 2026-06-08
**Roadmap:** `docs/NPC_quests/08_ROADMAP.md` §8.3 T-Q18
**Связь:** 09_OPEN_QUESTIONS.md §A5, §H (immediate save, no debounce)

## Скоуп (как в roadmap + ApplyQuestRewards)

### Готово

**Persistence (M8):**
- `Assets/_Project/Quests/Persistence/QuestSaveData.cs` (NEW) — POCO: quests + reputation + npcAttitude + 5 string sets. ✅
- `Assets/_Project/Quests/Persistence/IQuestStateRepository.cs` (NEW) — interface. ✅
- `Assets/_Project/Quests/Persistence/JsonQuestStateRepository.cs` (NEW) — `Application.persistentDataPath/quest_state_<clientId>.json`, atomic write (tmp → rename), in-memory cache. ✅
- `QuestWorld.Repository` property + `SetRepository(IQuestStateRepository)` — optional, nullable. ✅
- `QuestWorld.SavePlayer(ulong)` — build + save. ✅
- `QuestWorld.LoadPlayer(ulong)` — wipe existing state → apply from save. ✅
- `QuestWorld.BuildSaveData(ulong)` — aggregates all 4 dictionaries + 5 string sets. ✅
- `QuestWorld.Shutdown()` — flush all players перед clear. ✅

**Save hooks (immediate, no debounce, per §H):**
- `TryOffer` → `SavePlayer` ✅
- `TryAccept` → `SavePlayer` ✅
- `TryTurnIn` → `SavePlayer` ✅
- `SetTracked` → `SavePlayer` ✅
- `ModifyReputation` → `SavePlayer` ✅
- `ModifyNpcAttitude` → `SavePlayer` ✅
- `MarkContractCompleted/Accepted` → `SavePlayer` (только если set.Add = true, идемпотентно) ✅
- `MarkEventOccurred` → `SavePlayer` (idem) ✅
- `MarkNpcTalked` → `SavePlayer` (idem) ✅

**Load on player connect:**
- `QuestServer.OnClientConnectedForSnapshot(clientId)` → `QuestWorld.Instance.LoadPlayer(clientId)` (до BroadcastBothChange + SendQuestSnapshotToClient) ✅
- `QuestServer.OnNetworkSpawn` → `QuestWorld.Instance.SetRepository(new JsonQuestStateRepository())` ✅

**ApplyQuestRewards (deferred с T-Q15/T-Q16, now real):**
- Credits: `TradeWorld.Repository.GetCredits + delta → SetCredits` (push to client через PushPlayerSnapshot — нужно бы, but out of scope this iteration). ✅
- Items: `int.TryParse(QuestRewardItem.tradeItemId)` → `InventoryWorld.AddItemDirect(legacyIntId, ItemType.Resources)`. **TODO T-Q19:** proper string→int mapping via TradeItemDefinitionResolver.TryGet. ✅
- Cargo items: log warning (out of scope T-Q18, need active ship tracking). ✅
- Reputation: `ModifyReputation(silent: true)` для каждого reward entry. ✅
- Unlocks (DialogTree/Zone/Recipe/Achievement): log only (T-Q19 cleanup). ✅

## Файлы

### New
- `Assets/_Project/Quests/Persistence/QuestSaveData.cs`
- `Assets/_Project/Quests/Persistence/IQuestStateRepository.cs`
- `Assets/_Project/Quests/Persistence/JsonQuestStateRepository.cs`
- `T-Q18_DESIGN_NOTE.md` (этот файл)

### Modified
- `Assets/_Project/Quests/Core/QuestWorld.cs`:
  - `+Repository` field, `+SetRepository()`, `+BuildSaveData()`, `+SavePlayer()`, `+LoadPlayer()`, `+ApplyQuestRewards()` private
  - 9 save hooks (см. выше)
  - `Shutdown()` flushes
- `Assets/_Project/Quests/Network/QuestServer.cs`:
  - `+SetRepository(new JsonQuestStateRepository())` в OnNetworkSpawn
  - `+LoadPlayer(clientId)` в OnClientConnectedForSnapshot

## Verify (твои тесты)

### Persistence flow
1. Start host
2. Accept quest, modify reputation (через dialog AddReputation action — T-Q16)
3. Check Console: `[JsonQuestStateRepository] Saved player 0 state (X.X KB)`
4. **Проверить файл:** `C:\Users\<user>\AppData\LocalLow\<Company>\<Product>\quest_state_0.json` (Windows) — exists, contains quests + reputation
5. Stop host (Exit Play Mode)
6. Start host снова
7. Console должно показать: `[QuestWorld] LoadPlayer: client=0 restored 1 quests, 1 factions, 0 npcAttitudes`
8. P → CharacterWindow → таб КВЕСТЫ → quest в "Активных" (восстановлен)

### ApplyQuestRewards flow
1. Quest должен быть в `Completed` state (auto-complete happens in TryTurnIn if Active)
2. Walk to Mira → dialog edge с `TurnedIn`-friendly text → RequestTurnInQuestRpc
3. Console должно показать:
   - `[QuestWorld] TryTurnIn: client=0 quest=X toNpc=Y → TurnedIn`
   - `[QuestWorld] ApplyQuestRewards: credits ... → ... (+50)` (если reward.credits != 0)
   - `[QuestWorld] ApplyQuestRewards: items[0] id=N x1 → code=...` (если reward.items не пустой)
   - `[QuestWorld] ApplyQuestRewards: reputation faction=Z delta=W → V` (если reward.reputation не пустой)
4. **Reward setup:** автор должен заполнить `QuestDefinition.rewards` в SO. По дефолту Mira's `find_artifact` reward может быть пустой — log "ApplyQuestRewards: rewards null" — это OK, no rewards to apply.

## Известные ограничения (T-Q19 cleanup)

1. **TradeItemDefinition legacy int mapping** — `int.TryParse(tradeItemId)` с warning если fails. Решение: добавить `int legacyId` field в `TradeItemDefinition` SO, использовать в rewards. **T-Q19.**
2. **Cargo rewards** — `reward.cargoItems[]` log warning. Need active ship tracking + `ShipCargo.AddItem`. **T-Q19 or T-Q22.**
3. **QuestRewardUnlocks** — log only. Dialog tree unlock требует `QuestWorld.UnlockDialogTree(clientId, treeId)` + перенаправление RequestTalkToNpc на новый tree. **T-Q19.**
4. **Version migration** — `data.version = 1`. Old saves с `version=0` (no field) → JsonUtility defaults to 0 → load silently works (or fail). **T-Q19+ version migration framework.**
5. **No debounce** — каждый state change = 1 disk write. Per §H — acceptable для MVP (1-5 KB, ~1ms). Если perf проблемы → debounce (batch writes в Shutdown) — **T-Q19+**.
6. **Save file location** — `Application.persistentDataPath/quest_state_<clientId>.json`. Если изменить `companyName/productName` в Unity → все saves ломаются. **T-Q19 doc note.**

## Pitfalls

- **JsonUtility limitations** — `Dictionary<>` не сериализуется напрямую. Используем List<T> wrapper (см. `FactionRepSaveEntry[]`).
- **HashSet → List<string>** round-trip теряет порядок (но не ломает логику).
- **Server-only** — `JsonQuestStateRepository` пишет в `Application.persistentDataPath` server-стороны. На dedicated server это будет `ServerData/quest_state_*.json`.
- **Race conditions** — concurrent Save + Load для одного clientId теоретически проблематичны, но single-threaded Unity server → не актуально. `lock` добавлен на всякий случай.
- **Shutdown flush** — `Shutdown()` сначала SaveAll (для всех players), потом Clear. Если save fails — log error но continue clear (не блокируем shutdown).
- **LoadPlayer wipes** — перед apply вызывает Remove для всех dictionaries этого clientId. Нельзя skip wipe иначе дублей / stale data.

## Compile status

**0 errors.** Только pre-existing toolbar warning + MCP WebSocket exception (не наш).

## Uncommitted

- 3 NEW persistence files
- 1 design note
- 2 modified files (QuestWorld, QuestServer)
- 1 design note for T-Q18
