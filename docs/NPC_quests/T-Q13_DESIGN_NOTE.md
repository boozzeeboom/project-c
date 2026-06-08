# T-Q13 — ReputationClientState + NpcAttitudeClientState + tab fix (medium)

**Дата:** 2026-06-08
**Roadmap:** `docs/NPC_quests/08_ROADMAP.md` §8.3 T-Q13
**Связь:** 09_OPEN_QUESTIONS.md §G

## Решения по результатам согласования

1. **Строго по roadmap:** создаём **2 отдельных singleton'а** `ReputationClientState` + `NpcAttitudeClientState` в namespace `ProjectC.Reputation` (НЕ дублируем в `QuestClientState`).
2. **Cross-faction influence:** НЕ добавляем `weight` поле. `NpcDefinition.AttitudeLink` уже имеет `deltaOnLike`/`deltaOnDislike` (абсолютные дельты, не вес) — используем их. Если delta > 0 (NPC стал лучше относиться) → для каждой link `ModifyReputation(faction, link.deltaOnLike)`. Если delta < 0 → `link.deltaOnDislike`.
3. **`QuestClientState`:** убрать из него `CurrentReputation`/`CurrentNpcAttitude`/`OnReputationSnapshotReceived`/`OnNpcAttitudeSnapshotReceived` — это переезжает в новые singletons. Server-side `BuildReputationSnapshot`/`BuildNpcAttitudeSnapshot` остаются в `QuestServer` (он их отправляет).

## Цель

Поднять репутацию и отношение к NPC из серверной модели в UI. Сейчас:

1. **Сервер НИКОГДА не отправляет** rep+attitude snapshot'ы — нет хука `OnClientConnected` → push.
2. **`QuestWorld` имеет только getters** (`GetReputation`, `GetNpcAttitude`) — нет `ModifyReputation`/`ModifyNpcAttitude`. Невозможно накрутить значение.
3. **CharacterWindow.RefreshReputationCache** — placeholder 5 фракций с value=0 (явно сказано в коде).
4. **DialogWindow header** — нет NpcAttitude badge'а.

## Скоуп (что делаем)

### 1. Server: `QuestWorld.ModifyReputation` / `ModifyNpcAttitude` (1 файл)
- Файл: `Assets/_Project/Quests/Core/QuestWorld.cs`
- Добавить:
  - `public int ModifyReputation(ulong clientId, FactionId faction, int delta, int min = -100, int max = 100) → int newValue` (clamp + return new value).
  - `public int ModifyNpcAttitude(ulong clientId, string npcId, int delta) → int newValue` (clamp через `NpcDefinition.personalAttitudeMin/Max` для конкретного NPC, fallback `NpcAttitude.MinValue`/`MaxValue` = `-100..200` если NPC не найден).
- При `ModifyNpcAttitude`: применить cross-faction influence:
  - Найти `NpcDefinition` для `npcId` через `QuestDatabase.GetNpc(npcId)`.
  - Для каждой `attitudeLink`:
    - Если `delta > 0` → `ModifyReputation(link.targetFaction, link.deltaOnLike)` (silent, без broadcast — избежать recursion).
    - Если `delta < 0` → `ModifyReputation(link.targetFaction, link.deltaOnDislike)`.
  - Вернуть new value.

### 2. Server: `QuestServer` broadcast + connect hook (1 файл)
- Файл: `Assets/_Project/Quests/Network/QuestServer.cs`
- Добавить:
  - `public void BroadcastReputationChange(ulong clientId)` — вызвать `BuildReputationSnapshot` + `SendReputationSnapshotToClient`.
  - `public void BroadcastNpcAttitudeChange(ulong clientId)` — аналогично.
  - `public void BroadcastBothChange(ulong clientId)` — если attitude изменил faction rep тоже (cross-influence) — вызвать оба.
- В `OnNetworkSpawn` (server) подписаться на `NetworkManager.OnClientConnectedCallback`:
  - При коннекте нового clientId → push initial rep+attitude snapshot (вызвать broadcast'ы).
  - Отписаться в `OnNetworkDespawn`.
- В существующих RPC'ах, где меняется rep/attitude (T-Q15/T-Q16), вызывать broadcast.

### 3. Client: `ReputationClientState` singleton (1 файл NEW)
- Файл: `Assets/_Project/Reputation/ReputationClientState.cs` (NEW)
- Namespace: `ProjectC.Reputation`
- Pattern: копия `QuestClientState` но для reputation.
- API:
  - `public static ReputationClientState Instance { get; private set; }`
  - `[SerializeField] bool dontDestroyOnLoad = true` + `Awake/OnDestroy` (DDOL).
  - `public ReputationSnapshotDto? CurrentReputation { get; private set; }`
  - `public event Action<ReputationSnapshotDto> OnReputationUpdated`
  - `public void OnReputationSnapshotReceived(ReputationSnapshotDto snapshot)` — update + fire event + debug log.
- **GameObject в BootstrapScene** — `execute_code` через MCP добавит [ReputationClientState] GO + UIDocument НЕ нужен (singleton MonoBehaviour без UI). Просто GO + component.

### 4. Client: `NpcAttitudeClientState` singleton (1 файл NEW)
- Файл: `Assets/_Project/Reputation/NpcAttitudeClientState.cs` (NEW)
- Namespace: `ProjectC.Reputation`
- Идентичная структура: `CurrentNpcAttitude` + `OnNpcAttitudeUpdated` + `OnNpcAttitudeSnapshotReceived`.
- **GameObject в BootstrapScene** — `[NpcAttitudeClientState]`.

### 5. Cleanup `QuestClientState` (1 файл)
- Файл: `Assets/_Project/Quests/Client/QuestClientState.cs`
- Удалить:
  - `CurrentReputation` / `CurrentNpcAttitude` поля.
  - `OnReputationSnapshotReceived` / `OnNpcAttitudeSnapshotReceived` методы.
  - `OnReputationUpdated` / `OnNpcAttitudeUpdated` events.
- Удалить из `NetworkPlayer.cs`:
  - В `ReceiveReputationSnapshotTargetRpc` → `ReputationClientState.Instance?.OnReputationSnapshotReceived(snapshot)` (вместо `QuestClientState`).
  - В `ReceiveNpcAttitudeSnapshotTargetRpc` → `NpcAttitudeClientState.Instance?.OnNpcAttitudeSnapshotReceived(snapshot)`.

### 6. CharacterWindow + QuestWindow subscribe (2 файла)
- Файл: `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`
- Subscribe `ReputationClientState.Instance.OnReputationUpdated` + `NpcAttitudeClientState.Instance.OnNpcAttitudeUpdated` в `EnsureBuilt`.
- Unsubscribe в `OnDisable`.
- Lazy-subscribe в `Update()` (по аналогии с T-Q12 dialog fix — race condition с `QuestClientState`).
- `RefreshReputationCache()` — **заменить placeholder** на чтение из `ReputationClientState.Instance.CurrentReputation`. Если snapshot null → fallback на placeholder (5 фракций, value=0) для UX.
- NpcAttitude под-список в табе РЕПУТАЦИЯ — добавить секцию (заголовок "Отношения NPC" + ListView) между factions и footer.

### 7. UXML + USS (2 файла)
- Файл: `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — добавить секцию `<npc-attitude-section>` с Label `<npc-attitude-title>` + ListView `<npc-attitude-list>`.
- Файл: `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — стили `.npc-attitude-row*`.

### 8. DialogWindow NpcAttitude badge (1 файл)
- Файл: `Assets/_Project/Quests/UI/DialogWindow.cs`
- В header добавить Label `_npcAttitudeLabel` (рядом с `_npcNameLabel`).
- При `OnDialogStepReceived` (или при старте диалога) → resolve `npcId` из `_currentStep.speakerNpcId` → `NpcAttitudeClientState.Instance.CurrentNpcAttitude` → достать value → показать "❤ +15" (или "❤ -20").
- Lazy-subscribe на `NpcAttitudeClientState.OnNpcAttitudeUpdated` в Update (race condition fix).
- UXML/USS: возможно нужны правки `DialogWindow.uxml`/`DialogWindow.uss` (добавить Label в header — зависит от текущей структуры, см. `DialogWindow.uxml`).

## Файлы для изменения/создания

**Create (2):**
- `Assets/_Project/Reputation/ReputationClientState.cs`
- `Assets/_Project/Reputation/NpcAttitudeClientState.cs`

**Edit (7):**
- `Assets/_Project/Quests/Core/QuestWorld.cs` — +2 setter + cross-faction
- `Assets/_Project/Quests/Network/QuestServer.cs` — +broadcast + OnClientConnected hook
- `Assets/_Project/Quests/Client/QuestClientState.cs` — удалить reputation/npcAttitude
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — переключить на новые singletons
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — subscribe + NpcAttitude секция + заменить placeholder
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — +npc-attitude-section
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — стили
- `Assets/_Project/Quests/UI/DialogWindow.cs` — NpcAttitude badge + lazy-subscribe

**Scene (1):**
- `Assets/_Project/Scenes/BootstrapScene.unity` — +[ReputationClientState] GO + [NpcAttitudeClientState] GO

## Verify

1. **Compile:** `refresh_unity` → 0 errors, 0 warnings (кроме pre-existing).
2. **Play Mode test (host):**
   - Start host → `[QuestServer] OnClientConnected client=0` → в Console должно появиться `[QuestServer] SendReputationSnapshotToClient` + `SendNpcAttitudeSnapshotToClient`.
   - `[ReputationClientState] OnReputationSnapshotReceived: X factions` + `[NpcAttitudeClientState] OnNpcAttitudeSnapshotReceived: Y NPCs`.
3. **CharacterWindow test:**
   - P → таб РЕПУТАЦИЯ → 5 фракций (placeholder fallback OK если snapshot не пришёл, real values если пришёл).
   - Под-секция "Отношения NPC" — пустая (Mira attitude ещё не меняли).
4. **DialogWindow test:**
   - Подойти к Mira → E → dialog открывается → header показывает "❤ +0" (mira attitude value=0).
5. **Cross-faction test (через execute_code):**
   - `QuestWorld.Instance.ModifyNpcAttitude(0, "mira_01", +10)` → должен сработать cross-faction: для каждой `attitudeLink` Mira применить `deltaOnLike=10` к faction rep.
   - Console: `[ReputationClientState] OnReputationSnapshotReceived: ...` (новые values).
   - CharacterWindow таб РЕПУТАЦИЯ → обновился (lazy-subscribe OK).

## Что НЕ делаем (deferred)

- **Dialog actions** GiveCredits/AddReputation/AddNpcAttitude — T-Q15/T-Q16. Без них изменения можно увидеть только через `execute_code` прямой вызов `QuestWorld.Instance.ModifyReputation`.
- **Persistence** (T-Q18) — репутация сбросится при рестарте сервера.

## Открытые вопросы

1. **BootstrapScene GameObject placement** — куда положить [ReputationClientState] и [NpcAttitudeClientState]? Они singleton'ы, рядом с [QuestClientState] — OK?
2. **DialogWindow header structure** — посмотрю UXML в начале работы; если Label для badge уже есть в header — используем; если нет — добавлю.
