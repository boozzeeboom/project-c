# T-Q12 — QuestTracker overlay + DialogWindow typewriter/F-skip

**Дата:**2026-06-08
**Ветка:** `feature/npc-quest-v2`
**Зависимости:** T-Q07 (QuestClientState), T-Q10 (DialogWindow), T-Q11 (CharacterWindow quests tab).
**Блокирует:** T-Q15+ (action executors), M5 (Reputation + NpcAttitude).
**Трудоёмкость:** ~60-90 мин.

---

## Цель

Завершить M4 (Quest log + tracker):

1. **DialogWindow typewriter** — текст появляется char-by-char (~40 chars/sec). F или click мышью = моментальный skip до конца строки.
2. **QuestTracker overlay** — top-right HUD-панель с именем отслеживаемого квеста + текущей целью. Появляется когда есть tracked quest, скрывается когда нет.
3. **CharacterWindow integration** — кнопки «Следить» в Discovered-списке и «Следить/Не следить» в Active-списке. Click → `QuestTracker.Track(questId)` / `Untrack()`.

---

## Scope

### Часть A: DialogWindow typewriter + F/click skip

| # | Файл | Изменение | LOC |
|---|------|-----------|-----|
| A1 | `Assets/_Project/Quests/UI/DialogWindow.cs` | +`[SerializeField] charsPerSecond =40f`; +`StartTypewriter(text)` coroutine; +`SkipTypewriter()` мгновенный fill; +`SetTextImmediate(text)` для action-result; +`OnEnable` подписка на `PlayerInputReader.OnModeSwitchPressed` → SkipTypewriter (только при IsOpen + typewriter в процессе); +`OnDisable` unsubscribe; +click handler на `_panel` (`RegisterCallback<PointerDownEvent>`) → SkipTypewriter; +новые поля `_typewriterCoroutine`, `_fullText`, `_displayedCharCount` | +60 |
| A2 | `Assets/_Project/Quests/UI/DialogWindow.uxml` | без изменений | — |
| A3 | `Assets/_Project/Quests/UI/DialogWindow.uss` | без изменений | — |

### Часть B: QuestTracker overlay (NEW)

| # | Файл | Изменение | LOC |
|---|------|-----------|-----|
| B1 | `Assets/_Project/Quests/UI/QuestTracker.cs` (NEW) | MonoBehaviour singleton, `Instance` static; `[SerializeField]` UXML/USS; `_trackedQuestId` (локальный, MVP); `Track(questId)`/`Untrack()`/`Toggle(questId)`; Subscribe `QuestClientState.OnSnapshotUpdated` → refresh; подписка lazy в Update если Instance == null. EnsureBuilt: pattern из DialogWindow (styleSheets.Add + CloneTree + pickingMode=Ignore на root). Show/Hide: display toggle. |130 |
| B2 | `Assets/_Project/Quests/Resources/UI/QuestTracker.uxml` (NEW) | root > panel > Label "Имя квеста" + Label "Цель" + Button "Скрыть" |10 |
| B3 | `Assets/_Project/Quests/Resources/UI/QuestTracker.uss` (NEW) | `.quest-tracker-panel` (position absolute top-right, ~280×100, dark blue), `.quest-tracker-name` (bold), `.quest-tracker-objective` (smaller, gray), `.quest-tracker-hide` (small button). Все с `!important`. |50 |
| B4 | `Assets/_Project/Quests/Resources/UI/QuestTrackerPanelSettings.asset` (NEW) | Копия DialogPanelSettings.asset: themeUss guid `1cad08e114acf014d94b2301632cffa9` (UnityDefaultRuntimeTheme), refRes1920×1080. |53 |
| B5 | `Assets/_Project/Scenes/BootstrapScene.unity` (MOD) | +1 GameObject `[QuestTracker]` с компонентами: UIDocument (panelSettings=QuestTrackerPanelSettings, sourceAsset=QuestTracker.uxml) + QuestTracker (dialogWindowUxml=QuestTracker.uxml, dialogWindowUss=QuestTracker.uss). **Создание через MCP execute_code** (per pattern из T-Q11c). | scene |

### Часть C: CharacterWindow integration — кнопки «Следить»

| # | Файл | Изменение | LOC |
|---|------|-----------|-----|
| C1 | `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | +кнопка «Следить» per row в `quests-discovered-list` (Button внутри row) → `QuestTracker.Instance?.Track(q.questId)`; +кнопка «Не следить» per row в `quests-active-list` если `q.questId == QuestTracker.Instance.TrackedQuestId` → `Untrack()`; +handler'ы `OnTrackQuestClicked(questId)` / `OnUntrackQuestClicked(questId)`; +RefreshQuestsCache пересчитывает видимость tracker-кнопок при изменении state. | +50 |

**Total LOC: ~290** (без учета bootstrap scene +uxml/uss).

---

## Архитектура

### Typewriter coroutine

```csharp
private IEnumerator TypewriterRoutine(string fullText) {
 _fullText = fullText;
 _displayedCharCount =0;
 float interval =1f / charsPerSecond;
 while (_displayedCharCount < fullText.Length) {
 _textLabel.text = fullText.Substring(0, _displayedCharCount +1);
 _displayedCharCount++;
 yield return new WaitForSeconds(interval);
 }
 _textLabel.text = fullText;
 _typewriterCoroutine = null;
}
```

**Skip** = `_typewriterCoroutine = null; StopAllCoroutines() (локально); _textLabel.text = _fullText`.

**Race conditions:**
- Если приходит новый `OnDialogStepReceived` пока typewriter идёт → **StopAllCoroutines()** (или `_typewriterCoroutine = null`) + Start нового.
- Если action-result (DialogActionResultDto) приходит → `SetTextImmediate(message)` (без typewriter — мгновенно).
- F press: только если `IsOpen && _typewriterCoroutine != null` → skip.

**Click handler:**
```csharp
_panel.RegisterCallback<PointerDownEvent>(evt => {
 if (_typewriterCoroutine != null) SkipTypewriter();
});
```

Важно: click handler НЕ блокирует клик по option-кнопкам — потому что option-кнопки **внутри** `_optionsContainer` (child), а panel — parent. PointerDown на button'е сначала достигает button (child), затем bubble up to panel. Чтобы не было двойной реакции — guard: проверять `evt.target` НЕ button (или просто: skip on pointer down ВСЕГДА, options тоже skip — это OK, options после typewriter уже нет смысла показывать).

**Решение:** click по body текста = skip. Click по option button = advance (existing). Чтобы отличить — используем `_textLabel.RegisterCallback` вместо `_panel`:

```csharp
_textLabel.RegisterCallback<PointerDownEvent>(evt => {
 if (_typewriterCoroutine != null) SkipTypewriter();
});
```

Это безопаснее — click на тексте = skip, click на option = advance (existing).

### F-skip wiring

DialogWindow.OnEnable:
```csharp
var input = PlayerInputReader.Instance;
if (input != null) input.OnModeSwitchPressed += OnFSkipTypewriter;
```

OnDisable:
```csharp
var input = PlayerInputReader.Instance;
if (input != null) input.OnModeSwitchPressed -= OnFSkipTypewriter;
```

Handler:
```csharp
private void OnFSkipTypewriter() {
 if (!IsOpen || _typewriterCoroutine == null) return;
 SkipTypewriter();
}
```

**Conflict с PlayerStateMachine**: PlayerStateMachine регистрирует СВОЙ InputAction (`<Keyboard>/f`), не подписан на PlayerInputReader. Так что **оба сработают** при F press в dialog — это OK: boarding без ship = no-op.

### QuestTracker overlay

**Pattern:** как DialogWindow / MarketWindow — singleton MonoBehaviour, scene-placed в BootstrapScene, DontDestroyOnLoad.

**Architecture:**
```
QuestTracker (MonoBehaviour, singleton)
├─ Instance static
├─ EnsureBuilt() — clone UXML, attach USS, set pickingMode=Ignore
├─ Subscribe QuestClientState.OnSnapshotUpdated → HandleSnapshot → RefreshDisplay
├─ Track(questId) / Untrack() / Toggle(questId) public API
├─ _trackedQuestId (local, MVP)
├─ RefreshDisplay() — find tracked quest in snapshot, update panel or hide
└─ OnHideClicked() — Untrack()
```

**UI:**
- top-right corner: position absolute, top:4%, right:4%, width ~280px
- Panel: имя квеста (bold) + текущая цель (1 строка) + "Скрыть" button
- Auto-hide: `display: none` когда `_trackedQuestId == null`

**MVP local state** (Variant C):
- `_trackedQuestId` хранится в QuestTracker, не на сервере.
- При reconnect (server restart) → track reset (acceptable for MVP).
- Когда T-Q15 сделает `QuestWorld.SetTracked` real impl → миграция на server-side.

### CharacterWindow integration

**Per-row buttons in quest lists:**
- `quests-discovered-list`: каждая row имеет кнопку «Следить» (или «Принять и следить» если combine).
- `quests-active-list`: каждая row имеет кнопку toggle «Следить» / «Не следить».

В character window уже есть Button factories. Добавлю новый per-row button в `MakeQuestRow`:

```csharp
var trackBtn = new Button { name = "row-track-btn" };
trackBtn.AddToClassList("quest-row-track-btn");
trackBtn.text = "Следить";
row.Add(trackBtn);
// bind:
trackBtn.clicked += () => OnTrackQuestClicked(q.questId);
trackBtn.text = (QuestTracker.Instance?.TrackedQuestId == q.questId) ? "Не следить" : "Следить";
```

**Wait:** кнопка внутри row click event — ListView reuses VisualElement rows. click handler нужно привязывать в BindItem (не в MakeItem), потому что row переиспользуется. Action передаётся через closure захватывающий `q` (но `q` это item из snapshot — после re-bind может быть stale). Лучше передавать `questId` через `row.userData`:

```csharp
private void BindQuestRow(VisualElement row, int index) {
 // ... existing
 var trackBtn = row.Q<Button>("row-track-btn");
 if (trackBtn != null) {
 var srcList = ResolveQuestRowList(row);
 if (srcList != null && index < srcList.Count) {
 var q = srcList[index];
 row.userData = q.questId; // store for handler
 trackBtn.text = (QuestTracker.Instance?.TrackedQuestId == q.questId) ? "Не следить" : "Следить";
 trackBtn.clicked += () => OnTrackRowClicked(row);
 }
 }
}

private void OnTrackRowClicked(VisualElement row) {
 var questId = row.userData as string;
 if (string.IsNullOrEmpty(questId)) return;
 var tracker = QuestTracker.Instance;
 if (tracker == null) { SetMessage("QuestTracker недоступен", true); return; }
 if (tracker.TrackedQuestId == questId) tracker.Untrack();
 else tracker.Track(questId);
 // rebuild обоих списков для обновления текста кнопок.
 RefreshQuestsCache();
}
```

OK. Это работает.

---

## Pitfalls

1. **Coroutine + Domain Reload** — Unity coroutines не переживают domain reload (script recompile). Если recompile во время typewriter → coroutine потеряется. Mitigation: при `EnsureBuilt` (после reload) → `StopAllCoroutines()` + `SetTextImmediate(_fullText)`. Иначе `_textLabel.text` останется partial.

2. **Click через ListView rows** — UI Toolkit ListView reused rows. Click handlers должны быть **per-bind**, не per-make. Иначе handler держит stale data.

3. **PanelSettings.themeUss** — копия DialogPanelSettings.asset ОБЯЗАТЕЛЬНО, иначе panel = strip (см. T-Q11c bug #1).

4. **USS `!important`** — все class-стили с `!important`, кроме `display` (чтобы inline toggle работал). Skip на `display`.

5. **Scene-placement** — `[QuestTracker]` GameObject в **BootstrapScene** (DontDestroyOnLoad через `dontDestroyOnLoad:1` на самом QuestTracker). НЕ в WorldScene_X_Z (server infra rule).

6. **Subscribe BEFORE data ready** — `QuestClientState.Instance` всегда есть (AutoSpawn через `RuntimeInitializeOnLoadMethod`), но `CurrentSnapshot` может быть null. QuestTracker должен gracefully handle null snapshot → "no tracked quest to show".

---

## Verify (юзер делает руками)

1. **Compile:** Unity Editor → Console → **0 errors expected**.
2. **DialogWindow typewriter:**
 - E → Mira → dialog открылся → текст появляется **char-by-char** (видимая задержка ~25ms между символами).
 - Нажать **F** во время typewriter → текст мгновенно полный.
 - Click **на тексте** во время typewriter → текст мгновенно полный.
 - Click **на option** (button) → advance как обычно, НЕ skip typewriter для следующего step (отдельный typewriter для каждого step).
3. **QuestTracker:**
 - Открыть CharacterWindow → таб КВЕСТЫ → Discovered → click «Следить».
 - Top-right corner: появляется panel с именем квеста.
 - Close CharacterWindow → panel остаётся (DontDestroyOnLoad).
 - Open CharacterWindow → таб КВЕСТЫ → Active → у того же квеста кнопка показывает «Не следить».
 - Click «Не следить» → panel исчезает.
4. **Console:**
 - `[DialogWindow] Typewriter started: 'Привет, путник...' (52 chars @40 chars/sec)`
 - `[DialogWindow] Typewriter skipped (F or click)`
 - `[QuestTracker] Track: questId=find_artifact`
 - `[QuestTracker] RefreshDisplay: showing 'Найти артефакт'`
 - `[QuestTracker] Untrack: questId=find_artifact`

---

## Open / следующий тикет

- **T-Q15** — `QuestWorld.TryAccept` + `SetTracked` server impl (нужно для полного цикла Accept + server-side tracking).
- **T-Q13** — Reputation + NpcAttitude ClientState (следующая сессия по плану).

---

## Файлы изменены / созданы

```
M Assets/_Project/Quests/UI/DialogWindow.cs (+60 LOC: typewriter + F/click skip)
A Assets/_Project/Quests/UI/QuestTracker.cs (NEW,130 LOC: singleton overlay)
A Assets/_Project/Quests/Resources/UI/QuestTracker.uxml (NEW,10 LOC)
A Assets/_Project/Quests/Resources/UI/QuestTracker.uss (NEW,50 LOC)
A Assets/_Project/Quests/Resources/UI/QuestTrackerPanelSettings.asset (NEW,53 LOC)
M Assets/_Project/Scripts/UI/Client/CharacterWindow.cs (+50 LOC: track buttons)
M Assets/_Project/Scenes/BootstrapScene.unity (+[QuestTracker] GameObject)
A docs/dev/T-Q12_DESIGN_NOTE.md (NEW, design note)
```
