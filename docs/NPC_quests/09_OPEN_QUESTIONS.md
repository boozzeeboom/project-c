# 09 — Открытые вопросы (требуют решения перед кодом)

> **Саммари:** 15 вопросов, на которые пользователь должен ответить до старта
> кодинга. Сгруппированы по доменам. **Без этих решений я не начну писать код
> — будут reworks.**

---

## A. Core design (architecture)

### A1. F vs E для NPC talk
**Контекст:** пользователь сказал "F (наша кнопка интерактивности) иногда E", но в коде F = board ship, E = pickup/chest. См. `05_INPUT_AND_INTERACTION.md` §5.2.

**Варианты:**
- (a) **E** (recommended) — расширить существующий E-key pipeline. F остаётся boarding.
- (b) **F** — remap F (boarding → другая клавиша, e.g. RMB или Tab). Ломает PlayerStateMachine.
- (c) **Оба** — F primary, E fallback (но это непривычно).

**Моя рекомендация: (a) E.** Минимальный риск, естественное расширение.

**ответ::** сейчас F это посадка, E подбор сундуки и открыть рынок. Мы оставляем E на взаимодействие с нпс (так как в итоге рынок будет "npc управляющий рынком") а F для взаимодействия с предметами. поэтому нам нам нужно будет разграничение. со всеми NPC диалог на E начинается, но если с npc нужно будет "взаимодействовать както ещк(к примеру осмотреть,обокрасть и т.п.) будет доступна клавиша F. резюмируя: E - диалог F - действие. Исходя из этого возможно подбор предметов переназначим на F в будущем и запишем в todo.

---

### A2. Quest vs Contract — общая или раздельная система?
**Контекст:** в коде `DialogueNode.contractId` ссылается на `Trade.ContractData` (delivery board). См. `01_CURRENT_STATE_AUDIT.md` §1.9.

**Варианты:**
- (a) **Раздельные** (recommended) — `QuestDefinition` отдельная от `ContractData`. `DialogueAction.OfferQuest` vs `DialogueAction.OfferContract`. Чище, но bridge.
- (b) **Объединить** — `QuestDefinition` extends `ContractData`. Меньше работы, но смешивает narrative и trade.
- (c) **Quest ⊃ Contract** — `ContractData` = упрощённый `QuestDefinition`. Усложняет оба.

**Моя рекомендация: (a).** Quest и Contract имеют разную природу (narrative vs trade), разделение даст более чистый код.

**ответ:** А  - подходит. но нужна оговорка. квесты должны будут смотреть данные контрактов. можно связать тем, что контракты будут создавать какуюто метаинфу на персонаже мол сделал тото-томуто как дата, и квесты будут смотреть что как сделано.

---

### A3. Faction reputation granularity
**Контекст:** репутация с фракцией может быть per-faction, per-NPC, или both. См. `02_V2_ARCHITECTURE.md` §2.12.

**Варианты:**
- (a) **Per-faction** (recommended) — одно int -100..+100 на фракцию, shared по всем NPC фракции. Просто, matches GDD-23.
- (b) **Per-NPC** — отдельный int на каждого NPC. Много данных, но personal arcs.
- (c) **Both layered** — per-NPC стартует с per-faction, drift'ится независимо.

**Моя рекомендация: (a) для v1, (c) как future v2.**

**ответ:** оба варианта. для mvp отношения исистему с npc и фракциями можно оставить на самый конец отладка и добавление фич . NPC должен обладать характеристиками, в том числе привязаность к фракциям другим нпс и тд. поэтому мы бы могли "прокачать" связь с нпс ухудшая отношение с другой фракцией для чего-то и т.п.

---

### A4. DialogTree: top-level SO или sub-resource of NPC?
**Контекст:** сейчас `DialogueNode[]` массив на `NpcData`. Где жить диалогу в v2? См. `02_V2_ARCHITECTURE.md` §2.3.7.

**Варианты:**
- (a) **Top-level SO** (recommended) — `DialogTree` отдельный SO, `NpcDefinition.defaultDialogTree` ref. Shared между NPC. "Generic dockworker" сцена.
- (b) **Sub-resource** — `NpcDefinition.dialogues[]` массив. Per-NPC. Просто, но не reuse.
- (c) **Both** — default tree + `DialogueAction.SwitchDialogTree(treeId)` для runtime swap.

**Моя рекомендация: (a) + (c).** Default tree на NPC, swap через action.

**ответ:** тут не знаю главное не противоречим архитектуре основной. бустрап сцена и пордргузка сцен, обработка сервером с уклоном ммо сервер-клиент.

---

### A5. Persistence scope (server vs client cache)
**Контекст:** квесты могут длиться часы. Server restart = потеря прогресса? См. `02_V2_ARCHITECTURE.md` §2.9.

**Варианты:**
- (a) **Server-side JSON, save on every change** (recommended) — IQuestStateRepository, debounced 1 sec write. Server = source of truth.
- (b) **Client-side cache** — server pushes, client кэш для UI. UI работает offline. Server = source of truth на reconnect.
- (c) **Both** — server persists + client caches. Best UX, more code.

**Моя рекомендация: (a) для v1, (b) добавить позже если потребуется.**

**ответ:** не знаю технически. квесты и прогресс хранятся сервером всегда. пишутся кудато жестко, сервер упал - прогресс сохранился по какому-то ключевому моменту (разговор передача тригер и т.п.)

---

## B. Editor tooling

### B1. Editor tool: view-only или full CRUD?
**Контекст:** Quest Database Explorer — только browse/view или также edit?

**Варианты:**
- (a) **View-only** (recommended) — окно показывает данные, edit = native Inspector. Меньше работы, использует проверенный Unity Inspector.
- (b) **Full CRUD** — кастомные PropertyField в окне, save on commit. Удобнее, но дублирует Inspector.

**Моя рекомендация: (a) v1, (b) optional в v2.**

**ответ:** полный редактор. сразу все под рукой должно быть.

---

### B2. Включать ли GraphView для DialogTree?
**Контекст:** GraphView (`UnityEditor.Experimental.GraphView`) для visual dialog editing.

**Варианты:**
- (a) **Нет** (recommended) — для v1 достаточно indented tree в окне. GraphView = много работы, отдельный tab.
- (b) **Да, как opt-in sub-tab** — для сложных квестов с branching. Yarn Spinner-style.

**Моя рекомендация: (a) для v1.**

**ответ:** b - делаем сразу.

---

### B3. Multi-user collaboration (Perforce / Git LFS)
**Контекст:** SO ассеты — текстовые, но в проекте может быть Git LFS или Perforce. Конфликты при merge.

**Варианты:**
- (a) **Ничего специального** — Unity serializes SO в YAML, merge конфликты разрешаются вручную.
- (b) **GUID-rename detection** — при merge если 2 человека переименовали одинаковый quest, lost data.
- (c) **External DB (SQLite/JSON)** вместо SO — лучше для merge, но теряется Inspector integration.

**Моя рекомендация: (a) для v1.** Project не настроен на collaborative editing, не блокер.

**ответ:** a - не думаем про мультиюзеринг. 

---

## C. Input/Interaction

### C1. Typewriter skip key
**Контекст:** внутри диалога Space (jump) — skip typewriter? Или другая клавиша?

**Варианты:**
- (a) **Space** (recommended) — `PlayerInputReader.OnJumpPressed` → DialogWindow.OnSpacePressed. Когда диалог открыт, jump не работает (т.к. курсор не на игре).
- (b) **E** — но E = "interact", и в диалоге нет interact.
- (c) **Click on body** — клик мышью = skip.

**Моя рекомендация: (a) Space + (c) click on body.** Multiple options.

**ответ:** клик мышью и F она у нас для действий - описано выше.

---

### C2. Gamepad navigation
**Контекст:** UI Toolkit `FocusController` поддерживает Tab/arrow keys.

**Варианты:**
- (a) **Default UI Toolkit navigation** (recommended) — focusable buttons + gamepad/stick.
- (b) **Numbered hotkeys (1-6)** — `InputAction("DialogOption1")` etc. Дополнительная работа.
- (c) **Both** — keyboard 1-6, gamepad default navigation.

**Моя рекомендация: (a) v1, (c) later если user запросит.**

**ответ:**А - потом если что для геймпада отдельно

---

### C3. PlayerInputReader.Instance singleton
**Контекст:** события declared but no subscribers. Делать singleton? Подписывать NetworkPlayer?

**Варианты:**
- (a) **Singleton + minimal wire-up** (recommended) — `PlayerInputReader.Instance`, DialogWindow subscribes.
- (b) **Full refactor** — NetworkPlayer подписывается на все events, internal handlers. Чище, но рефактор.

**Моя рекомендация: (a) для quest work, (b) отдельный cleanup тикет.**

**ответ:** незнаю. скорее всего фулл рефактор. не противоречим главной архитектуре. но мне кажется плеер должен на все подписываться, так будет правильнее и чище.

---

## D. Triggers/Integration

### D1. Inventory persistence — фиксить до quest rewards?
**Контекст:** `InventoryWorld.cs:21` явно TODO — НЕ персистится. Квест rewards дают items, но они пропадут при server restart.

**Варианты:**
- (a) **Фиксить ДО quest rewards** (recommended) — добавить `IInventoryRepository` + JSON, save on change. Без этого quest rewards = placeholder.
- (b) **Рискнуть** — реализовать quest rewards, принять что items пропадут при restart. Починить persistence позже.

**Моя рекомендация: (a).** T-X0 (новый тикет) перед T-Q14-T-Q15.

**ответ:** фиксим до. но внимательно смотрим так ли это.

---

### D2. Event-bus или polling-only для v1?
**Контекст:** trigger'ы могут быть event-driven или poll-based. См. `06_TRIGGERS_AND_INTEGRATION.md` §6.7.

**Варианты:**
- (a) **Polling-only** (recommended для v1) — `QuestWorld.Tick()` каждые 5 сек, check all triggers. Simple, no cross-namespace dependencies.
- (b) **Hybrid** — event-bus для discrete events (TalkedToNpc, ReputationChanged), polling для continuous (HaveItem, Location).
- (c) **Full event-bus** — все triggers event-driven. Требует хуков во ВСЕ серверы (Inventory, Trade, Ship, DayNight).

**Моя рекомендация: (a) для v1, (b) в v2.**

**ответ:**фулл бас

---

### D3. Quest → Combat в scope?
**Контекст:** `KilledEntityTrigger` упоминается, но combat system не существует. См. `06_TRIGGERS_AND_INTEGRATION.md` §6.6.

**Варианты:**
- (a) **Out of scope** (recommended) — `KilledEntityTrigger` остаётся stub до combat. Quest progression работает для non-combat objectives.
- (b) **Stub trigger** — `KilledEntityTrigger` существует, но всегда returns false. Placeholder.
- (c) **Простой kill counter** — добавить в `NetworkPlayer` `killsByType` dict, server-side. Хак, но даёт combat-квесты.

**Моя рекомендация: (a) для v1.** Combat в roadmap, но out of scope для текущей сессии.

**ответ:** мне кажется Б оставляем плейсхолдер в туду на реализацию позднюю когда комбат систем будет

---

## E. Misc

### E1. NPC gender / pronouns для локализации
**Контекст:** в локализации NPC reference может быть gendered (он/она/оно).

**Варианты:**
- (a) **Manual entry per NPC** — `string masculinePronoun, femininePronoun, neutralPronoun` в `FactionDefinition` или `NpcDefinition`.
- (b) **Per-language loc keys** — `mira_greeting_ru`, `mira_greeting_en`. Полная локализация.
- (c) **Generic they/them** — для всех NPC.

**Моя рекомендация: (c) v1 (generic), (b) v2 (когда localization subsystem).**

**ответ:**для начала можем оставить вобще пусто, так как тот кто пишет квесты знает это М или Ж и как его\ее зовут. в будущем в туду локализация полная сабсистемы.

---

### E2. Renaming `Trade.Core.NPCTrader` → `MarketTrader`?
**Контекст:** naming collision с `ProjectC.World.Npc`. См. `01_CURRENT_STATE_AUDIT.md` §1.7.

**Варианты:**
- (a) **Rename** (recommended) — чисто, убирает путаницу.
- (b) **Оставить + документировать** — namespace overlap допустим.

**Моя рекомендация: (a).** Отдельный тикет T-X1.

**ответ:** ренейм. делаем по общему в одном и правильном архитектурно варианте.

---

### E3. Acceptance flow UI
**Контекст:** когда NPC предлагает квест, player должен явно accept. Какой UI?

**Варианты:**
- (a) **В dialog** (recommended) — option "Я помогу. (Принять квест)" в dialog tree, action = OfferQuest. Server validates, transitions to "active" state.
- (b) **Popup окно** — "Мира предлагает вам квест 'Найти Кристалл Времён'. [Принять] [Отклонить]".
- (c) **Auto-accept** — quest offered = quest active. Нет в lore MMO обычно.

**Моя рекомендация: (a).** В dialog — natural flow, no extra UI.

**ответ:** в диалоговой форме будет принять да\нет. по событию : зашел кудато увидел чтото и т.п. - будет запись в журнале - а следовать ли ей, решает игрок.

---

## F. Что МОЖНО решить позже (не блокер)

| # | Вопрос | Когда решить |
|---|--------|--------------|
| F1 | Точные tier'ы reputation ("Honored" / "Revered" / "Exalted" — текст и пороги) | После v1 SO созданы, можно tune |
| F2 | Greeting animation timing | При интеграции с animator |
| F3 | Toast notification styling | При первом quest accept в Play Mode |
| F4 | Quest log pagination (если > 50 quests) | Если достигли |
| F5 | Multi-language (English версия локализации) | Когда localization subsystem готов |
| F6 | Save file versioning (для migration между версиями SO) | При breaking change в SO shape |

---

## Финальные решения (приняты 2026-06-07, зафиксированы)

| # | Вопрос | Финальное решение | Влияние на roadmap |
|---|--------|-------------------|---------------------|
| **A1** | F vs E для NPC talk | **E = диалог с NPC, F = действие (осмотреть/обокрасть).** Подбор предметов → F в будущем (отдельный TODO-тикет T-X4) | + T-X4 (remap pickup E→F) |
| **A2** | Quest vs Contract | **Раздельные** + bridge: квесты читают meta-инфу о контрактах (дата, кто выдал, что сделано) | + ContractMetaBridge (sub-тикет T-Q15) |
| **A3** | Reputation | **Both: per-faction + per-NPC с перекрёстным влиянием.** NPC привязан к фракции, имеет личные отношения; репутация с фракцией может ухудшаться при "прокачке" отношений с NPC вражеской фракции | 🔥 расширение `FactionDefinition` + новый `NpcAttitude` слой; см. §G ниже |
| **A4** | DialogTree | **Top-level SO + Swap (default + `SwitchDialogTree` action)** | OK как было |
| **A5** | Persistence | **Server JSON, hard save на ключевых событиях** (разговор/передача/триггер), не теряется при падении | + "checkpoint" в `QuestWorld.Save` — flush сразу на state change без debounce |
| **B1** | Editor tool | **Full CRUD** (всё под рукой, не view-only) | 🔥 T-Q09 расширен (больше работы) |
| **B2** | GraphView | **ДА, сразу, для DialogTree** | + T-Q09b (GraphView sub-tab, новый тикет) |
| **B3** | Multi-user collab | **Не думаем** | OK |
| **C1** | Typewriter skip | **Click мышью + F** (F = "действие" в dialog, как описано в A1) | USS обработчик + InputAction |
| **C2** | Gamepad | **UI Toolkit default**, gamepad later | OK |
| **C3** | PlayerInputReader | **Full refactor** (NetworkPlayer подписывается на все events, internal handlers) | 🔥 T-X3 расширен (полный input pipeline) |
| **D1** | Inventory persistence | **Фиксим ДО**, внимательно | + T-X0 (новый тикет, перед T-Q14) |
| **D2** | Event-bus | **Full bus** (все triggers event-driven, хуки во все серверы) | 🔥 T-Q06 расширен (WorldEvent + хуки в Inventory/Trade/Ship/DayNight) |
| **D3** | Combat квесты | **Stub + TODO** (placeholder, реализуем когда combat) | OK |
| **E1** | NPC gender | **Пусто** (кто пишет квесты — знает М/Ж). Полная локализация → later TODO | OK |
| **E2** | Rename NPCTrader | **ДА** | OK (T-X1) |
| **E3** | Acceptance UI | **В dialog (да/нет)** + **событийные квесты** → запись в журнал, player сам решает следовать | + `EventDrivenQuest` тип objective (sub-тикет T-Q04) |

---

## G. Решение A3 — детальная спецификация (Both: per-faction + per-NPC)

**Структура reputation:**

```
FactionReputation (per player, per faction)
  - guildOfThoughts: int (-100..+200, default 0)

NpcAttitude (per player, per NPC)
  - mira_01: int (-100..+200, default = factionRep[guildOfThoughts])
  - zoric_01: int (-100..+200, default = factionRep[guildOfCreation])

Cross-faction influence (server-side, в QuestWorld.Tick или event-bus)
  - "ухудшил отношения с NPC X" → автоматически ухудшает factionRep[X.faction] на N
  - "улучшил отношения с NPC Y" → автоматически улучшает factionRep[Y.faction] на M
  - Конфигурируется через NpcDefinition.attitudeLinks[]: { targetFaction: FactionId, deltaOnLike: int, deltaOnDislike: int }
```

**Где хранится:**
- `FactionReputation` → `ProjectC.Reputation.ReputationClientState` (singleton), persisted в `IQuestStateRepository` (T-Q18).
- `NpcAttitude` → `ProjectC.Reputation.NpcAttitudeClientState` (singleton), persisted там же.

**UI:**
- В `DialogWindow` header — два badge'а: factionRep + npcAttitude.
- В `CharacterWindow` таб "РЕПУТАЦИЯ" — список фракций + "личные отношения" под-список.
- В `QuestDatabaseWindow` — фильтр по "минимальная factionRep" и "минимальная NpcAttitude" при поиске квестов.

**Связи и примеры:**
- NPC Mira (GuildOfThoughts) — даёт квест "Найти Кристалл Времён". Требует factionRep[GuildOfThoughts] ≥ 0.
- NPC Zoric (GuildOfCreation, враги GuildOfThoughts) — даёт квест "Украсть у Миры". Требует factionRep[GuildOfCreation] ≥ 25, NpcAttitude[mira_01] ≤ -50.
- Игрок улучшает NpcAttitude[zoric_01] → cross-link ухудшает factionRep[GuildOfThoughts] на 5 (конфигурируется).

**MVA (Minimum Viable Approach) для v1:**
- `FactionReputation` — полная реализация.
- `NpcAttitude` — базовая (per-NPC int, не связан с faction).
- Cross-faction influence — stub конфиг (поля в `NpcDefinition.attitudeLinks[]` есть, но cross-calc отложен в v2).
- TODO в `02_V2_ARCHITECTURE.md` §2.12: "Cross-faction attitude link calc — v2, см. `09_OPEN_QUESTIONS.md` §G."

**Изменённые тикеты:**
- T-Q01 расширен: добавить `NpcAttitude` enum / struct (или просто int wrapper) рядом с `FactionId`.
- T-Q13 расширен: создать `NpcAttitudeClientState` параллельно с `ReputationClientState`.
- T-Q18 расширен: persistence для `NpcAttitude` dict.
- T-X0 (новый): перед T-Q01 — зафиксировать inventory persistence.

---

## H. Решение A5 — checkpoint-based persistence (детали)

**Текущий план:** `QuestWorld.SavePlayer` debounced 1 sec.
**Новое:** **fire-and-forget save на каждом state change**, без debounce. Если сервер упадёт между saves — потеря максимум 1 RPC (acceptable).

**State change events, инициирующие save:**
- `QuestStateTransition` (quest added / advanced / completed / failed)
- `StageTransition` (currentStage changed)
- `ObjectiveProgressed` (counter changed)
- `ReputationChanged` (factionRep delta)
- `NpcAttitudeChanged`
- `DialogVisitedNode` (для `WasNodeVisited` condition)
- `FlagSet` (для `FlagIsSet` condition)

**Транзакционность:** `QuestWorld.SavePlayer` пишет ВСЕ данные игрока (quests + rep + npcAttitude + flags) атомарно в ОДИН JSON файл. Несколько мелких saves = inconsistency risk. Один большой save = атомарно.

**Файл:** `Application.persistentDataPath/quest_state_<clientId>.json` (по аналогии с `ServerFileRepository`).

**Performance:** JSON write ~1-5 KB per save, ~1 ms. На каждом state change — OK.

**Изменённые тикеты:**
- T-Q18 расширен: убрать debounce, добавить immediate save on state change.

---

## I. Решение B1 — Full CRUD в EditorWindow (детали)

**Что именно "full CRUD":**
- ✅ Просмотр (TreeView, MultiColumnListView, search, filters) — как было запланировано.
- ✅ **Создание** новых SO-ассетов через кнопки "+ NPC", "+ Quest", "+ Dialog Tree" в toolbar.
- ✅ **Редактирование** полей через inline `PropertyField` (UI Toolkit SerializedProperty).
- ✅ **Удаление** с подтверждением (modal dialog "Точно удалить? Это необратимо").
- ✅ **Drag-and-drop** ассетов в панели для re-parenting / linking.
- ✅ **Копирование/дублирование** — context menu "Duplicate".
- ✅ **Валидация** в real-time (как в `03_EDITOR_TOOLING.md` §3.7, но inline badges на invalid rows).

**Что НЕ full CRUD:**
- ❌ Visual graph editing (NodeView с edges) — это T-Q09b (GraphView).
- ❌ Bulk import/export — out of scope v1.
- ❌ Diff with previous version — out of scope v1.

**Изменённые тикеты:**
- T-Q09 расширен: добавить Create/Edit/Delete/Duplicate секции. Больше работы, но не меняет scope.
- T-Q09b (новый): GraphView sub-tab для DialogTree (визуальный редактор нод + edges).

---

## J. Решение D2 — Full event bus (детали)

**Что значит "full bus":**
- Серверный singleton `ProjectC.Core.WorldEventBus : MonoBehaviour` (или static).
- Тип `WorldEvent` (уже в `06_TRIGGERS_AND_INTEGRATION.md` §6.3) — tagged union.
- API: `WorldEventBus.Publish<T>(T ev) where T : WorldEvent` + `WorldEventBus.Subscribe<T>(Action<T> handler)`.
- **Все серверы** (Quest, Contract, Market, Inventory, Ship, DayNight) подписываются и публикуют.

**Изменения в существующих серверах (T-Q06, T-Q14-T-Q17):**

| Сервер | Подписки | Публикации |
|--------|----------|-----------|
| `InventoryServer` | — | `ItemAddedEvent`, `ItemRemovedEvent` |
| `MarketServer` | `ItemAddedEvent` (для quest trigger'а) | `ItemTradedEvent` (новый) |
| `ContractServer` | — | `ContractAcceptedEvent`, `ContractCompletedEvent`, `ContractFailedEvent` (для quest bridge) |
| `QuestServer` | все quest-relevant events | `QuestStateChangedEvent`, `ReputationChangedEvent`, `NpcAttitudeChangedEvent` |
| `ShipController` (server-side) | — | `ShipDockedEvent` (будущее) |
| `DayNightController` | — | `DayNightPhaseChangedEvent` |
| `MetaRequirement` (server-side) | — | `AccessGrantedEvent` (для quest log) |

**Тесты:** `WorldEventBus` должен быть testable в EditMode (без NetworkBehaviour). Static singleton + `Reset()` для test isolation.

**Изменённые тикеты:**
- T-Q06 расширен: WorldEventBus singleton + 3+ базовых trigger'а + подписки в QuestWorld.
- T-Q14 расширен: InventoryServer публикует events.
- T-X0 (новый, inventory persistence) включает WorldEventBus hooks.
- + Новый тикет T-X5 (после T-Q14): publish events из ContractServer для quest bridge (A2).

---

## K. Решение E3 — событийные квесты (новый тип objective)

**Новый `QuestObjectiveType`: `EventDriven = 7`**

**Поля:**
- `string eventId` — уникальный ID события, на которое подписывается квест.
- `string description` — текст для журнала ("Зайти в руины и увидеть свечение").

**Поведение:**
- Когда сервер публикует `CustomEvent { eventId = "..." }` → `QuestTriggerService.OnWorldEvent` fires.
- `EventDrivenTrigger` evaluates all quests with `EventDriven` objective matching this eventId.
- Если matched → objective completed, `QuestInstance` advances.
- **Но!** До того как игрок зайдёт в журнал и выберет "следовать", квест не показывается в Active. Он в состоянии `Offered` (или `Discovered`).

**Новый `QuestState`: `Discovered = 0`** (предшествует `Offered`).

**UI:**
- Когда `CustomEvent` fires → если у игрока есть `Discovered` quest matching → показать notification "Новая запись в журнале: <quest name>".
- Игрок открывает `CharacterWindow → Quests` → видит раздел "Discovered" (или "События").
- Игрок кликает "Принять" → transition `Discovered → Active` (только в этом случае квест становится полноценным Active).

**Пример:**
- Квест "Найти логово контрабандистов" имеет objective `EventDriven(eventId="player_visited_smuggler_lookout")`.
- Когда `ZoneEntered(zoneId="smuggler_lookout")` published → событие fires → квест becomes `Discovered`.
- Игрок видит "Новая запись в журнале". Открывает → читает описание → принимает → становится Active.
- Дальше — обычный flow с objectives.

**Изменённые тикеты:**
- T-Q04 расширен: добавить `EventDriven` objective type + `Discovered` state.
- T-Q11 (CharacterWindow quest log) расширен: добавить "Discovered" раздел + "Accept" кнопку.

---

## L. Решение A1 — Input remap план

**Текущее состояние:**
- F = boarding (`NetworkPlayer.cs:286`)
- E = pickup/chest/market (`NetworkPlayer.cs:375`)

**Целевое:**
- E = NPC talk (только диалог)
- F = boarding + future "action" (осмотреть/обокрасть) для NPC
- Pickup = **F** (remap E → F)

**Когда делать remap:**
- **НЕ сейчас** (T-Q01..T-Q10). Текущий E = pickup/chest остаётся. Добавляем NPC branch **в начало** E-pipeline: если есть NPC в радиусе — talk, иначе fallthrough к pickup/chest.
- **T-X4 (после T-Q19 cleanup)**: глобальный remap E→F для pickup. F boarding остаётся. NPC talk по-прежнему на E. Параллельно — реализация F-action для NPC (inspect, steal).

**T-Q08 уточнение:**
```csharp
// В NetworkPlayer.Update E-handler:
// 0. NPC (highest priority)
if (QuestInteractor.Instance != null && QuestInteractor.Instance.TryTalkToNpc()) return;

// 1. (existing) MetaRequirement / chest / pickup / market
if (TryInteractNearestMetaRequirement()) return;
// ... rest unchanged
```

**T-Q10 (DialogWindow) уточнение:**
- Skip typewriter: F (вместо Space) + click мышью.
- Confirm option: Enter / click (без изменений).
- Закрыть диалог: Esc (без изменений).

**T-X3 (PlayerInputReader full refactor) расширение:**
- F-key в dialog context = skip typewriter (НЕ boarding).
- F-key в world context = boarding (existing) + future action (NPC inspect/steal).
- Контекстная логика через state machine (DialogWindow.IsVisible → DialogInputMode, иначе WorldInputMode).

---

## M. Решение C3 — Full PlayerInputReader refactor (детали)

**Текущее:** `PlayerInputReader` declares events, no subscribers. `NetworkPlayer` polls `Keyboard.current.*Key.wasPressedThisFrame` directly.

**Новое:** `PlayerInputReader` = source of truth. `NetworkPlayer` + все UI подписываются на events.

**Шаги:**
1. В `PlayerInputReader.cs` добавить `public static PlayerInputReader Instance { get; private set; }` + `Awake` setter.
2. В `PlayerInputReader.cs` все events становятся reliable:
   - `OnMoveInput(Vector2)`, `OnJumpPressed`, `OnRunPressed/Released`, `OnInteractPressed` (E), `OnModeSwitchPressed` (F), `OnMouseDelta`, `OnPausePressed` (Esc).
3. В `NetworkPlayer.Awake`: подписаться на все events, internal handlers.
4. Удалить direct `Keyboard.current.*Key.wasPressedThisFrame` polling из `NetworkPlayer.Update`.
5. В `PlayerStateMachine.Awake`: подписаться на `OnModeSwitchPressed` (F) → board/disembark.
6. `DialogWindow.OnEnable/OnDisable`: подписаться на `OnModeSwitchPressed` (F) → skip typewriter when dialog visible.

**Влияние на тикеты:**
- T-X3 (новый) — переименовать или сделать полноценным: `T-X3: PlayerInputReader full refactor`.
- Расширяет scope: не только Instance, но и вся input pipeline.
- **Делать ДО T-Q08** (иначе T-Q08 придётся дважды переподписываться).

**Переупорядочение roadmap:**
- T-X3 → **между T-Q07 и T-Q08** (перед добавлением NPC branch в input pipeline).
- Все T-Q* тикеты после T-Q07 должны использовать `PlayerInputReader.Instance?.OnInteractPressed` вместо `Keyboard.current.eKey.wasPressedThisFrame`.

---

## N. Резюме: изменения в roadmap

**Новые тикеты:**
- **T-X0** — `InventoryWorld` persistence (фиксим ДО quest rewards).
- **T-X4** — input remap (E pickup → F pickup, TBD после T-Q19).
- **T-X5** — ContractServer publish events для quest bridge.
- **T-Q09b** — GraphView sub-tab для DialogTree.

**Расширенные тикеты:**
- **T-Q04** — + EventDriven objective + Discovered state (см. §K).
- **T-Q06** — + full event bus, WorldEventBus singleton (см. §J).
- **T-Q08** — + NPC branch в E-pipeline (после T-X3).
- **T-Q09** — + full CRUD в EditorWindow (см. §I).
- **T-Q10** — + F skip typewriter (вместо Space).
- **T-Q11** — + Discovered section в CharacterWindow.
- **T-Q13** — + NpcAttitudeClientState (per-NPC reputation, см. §G).
- **T-Q14** — + event bus hooks в InventoryServer.
- **T-Q15** — + ContractMetaBridge для A2.
- **T-Q18** — + immediate save (no debounce) + NpcAttitude persistence.
- **T-X1** — без изменений.
- **T-X2** — без изменений.
- **T-X3** — **расширен до full PlayerInputReader refactor** (см. §M).

**Переупорядочение:**
- T-X3 перемещён: было "OPTIONAL" в конце → теперь **между T-Q07 и T-Q08** (обязательный).
- T-X0 (inventory persistence) **между T-Q05 и T-Q06** (обязательный).

**Финальный порядок:**
```
T-Q01 → T-Q02 → T-Q03 → T-Q04 → T-Q05 → T-X0 → T-Q06 → T-Q07 → T-X3 → T-Q08 → T-Q09 → T-Q09b → T-Q10 → T-Q11 → T-Q12 → T-Q13 → T-Q14 → T-X5 → T-Q15 → T-Q16 → T-Q17 → T-Q18 → T-Q19 → T-X1 → T-X2 → T-X4
```

**См. `08_ROADMAP.md` §8.3 для деталей каждого тикета (обновлено 2026-06-10).**
