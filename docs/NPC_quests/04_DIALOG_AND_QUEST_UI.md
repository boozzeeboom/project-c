# 04 — Dialog UI + Quest UI (UI Toolkit)

> **UX-цель:** классический point-and-talk. Подошёл → нажал `E` → открылся
> диалог с портретом, typewriter-эффектом, репутационно-окрашенными ответами
> с outline на ховере. Quest log в CharacterWindow. Quest tracker на HUD.

---

## 4.1 Где какой UI живёт

| UI surface | Расположение | Окно | UXML файл |
|------------|--------------|------|-----------|
| **Dialog (active conversation)** | center-screen modal | Новое | `Assets/_Project/UI/Resources/UI/DialogWindow.uxml` |
| **Quest log (persistent list)** | таб в `CharacterWindow` | Существующий | `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` (добавить `<ui:VisualElement name="quests-section">`) |
| **Quest tracker (compact HUD)** | top-right corner overlay | Новое | `Assets/_Project/UI/Resources/UI/QuestTracker.uxml` |
| **NPC reputation badge (active)** | внутри dialog header | (часть DialogWindow) | (внутри DialogWindow.uxml) |
| **Reputation tab (persistent)** | таб в `CharacterWindow` | Существующий | уже есть (но `_reputationCache` пуст) |

**Правило (из `project-c-ui-as-tab` skill):** всё per-player + persistent + same context → **таб в существующем окне**. Контекстный (per-NPC) UI → **отдельное floating window**. Dialog = контекстный, поэтому **floating DialogWindow**, не таб. Quest log = persistent + per-player → **таб в CharacterWindow**.

---

## 4.2 DialogWindow — структура UXML

**Файл:** `Assets/_Project/UI/Resources/UI/DialogWindow.uxml` (NEW)

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement name="main-container" class="dialog-window" picking-mode="Ignore">

        <!-- HEADER: portrait + name/title + reputation badge -->
        <ui:VisualElement name="dialog-header" class="dialog-header">
            <ui:VisualElement name="portrait-frame" class="portrait-frame">
                <ui:VisualElement name="portrait" class="portrait" />
            </ui:VisualElement>
            <ui:VisualElement name="header-text" class="header-text">
                <ui:Label name="npc-name" class="npc-name" text="—" />
                <ui:Label name="npc-title" class="npc-title" text="—" />
                <ui:VisualElement name="rep-badge" class="rep-badge rep-neutral">
                    <ui:Label name="rep-text" text="Нейтрален" class="rep-text" />
                </ui:VisualElement>
            </ui:VisualElement>
            <ui:Button name="close-btn" class="close-btn" text="✕" />
        </ui:VisualElement>

        <!-- BODY: typewriter text -->
        <ui:ScrollView name="body-scroll" class="dialog-body" mode="Vertical">
            <ui:Label name="dialogue-text" class="dialogue-text" text="" />
        </ui:ScrollView>

        <!-- OPTIONS: 1..N buttons (built dynamically) -->
        <ui:VisualElement name="options-container" class="options-container" />

        <!-- FOOTER: hint text ("Пробел — пропустить, Esc — выйти") -->
        <ui:Label name="footer-hint" class="footer-hint"
                  text="Пробел — пропустить текст · Esc — выйти" />
    </ui:VisualElement>
</ui:UXML>
```

---

## 4.3 DialogWindow — USS стили

**Файл:** `Assets/_Project/UI/Resources/UI/DialogWindow.uss` (NEW)

```css
/* === MAIN CONTAINER === */
.dialog-window {
    position: absolute !important;
    top: 10% !important;
    left: 50% !important;
    translate: -50% 0 !important;
    width: 640px !important;
    max-width: 90% !important;
    max-height: 80% !important;
    background-color: rgb(15, 18, 26, 0.97) !important;
    border-width: 1px !important;
    border-color: rgba(120, 150, 200, 0.7) !important;
    border-radius: 6px !important;
    padding: 12px !important;
    display: flex !important;
    flex-direction: column !important;
    /* 4 FIX #1: pickingMode = Position on Show, Ignore on Hide (set in C#) */
}

/* === HEADER === */
.dialog-header {
    flex-direction: row !important;
    align-items: center !important;
    margin-bottom: 8px !important;
}

.portrait-frame {
    width: 80px !important;
    height: 80px !important;
    border-width: 2px !important;
    border-color: rgba(120, 150, 200, 0.5) !important;
    margin-right: 12px !important;
    background-color: rgb(30, 30, 40) !important;
    overflow: hidden !important;
}
.portrait {
    width: 100% !important;
    height: 100% !important;
    -unity-background-scale-mode: scale-to-fit !important;
}

.header-text {
    flex-grow: 1 !important;
    flex-direction: column !important;
}

.npc-name {
    font-size: 16px !important;
    color: rgb(255, 220, 130) !important;
    -unity-font-style: bold !important;
}
.npc-title {
    font-size: 11px !important;
    color: rgb(180, 180, 200) !important;
    margin-bottom: 4px !important;
}

/* === REPUTATION BADGE === */
.rep-badge {
    font-size: 10px !important;
    padding: 2px 6px !important;
    border-radius: 3px !important;
    -unity-font-style: bold !important;
    align-self: flex-start !important;
    margin-top: 4px !important;
}
.rep-positive {
    background-color: rgba(60, 140, 80, 0.4) !important;
    color: rgb(120, 220, 140) !important;
    border-width: 1px !important;
    border-color: rgb(80, 180, 100) !important;
}
.rep-negative {
    background-color: rgba(140, 50, 50, 0.4) !important;
    color: rgb(240, 130, 130) !important;
    border-width: 1px !important;
    border-color: rgb(200, 80, 80) !important;
}
.rep-neutral {
    background-color: rgba(80, 80, 100, 0.4) !important;
    color: rgb(180, 180, 200) !important;
    border-width: 1px !important;
    border-color: rgb(120, 120, 140) !important;
}

/* === BODY === */
.dialog-body {
    flex-grow: 1 !important;
    min-height: 120px !important;
    max-height: 50vh !important;
    padding: 8px 12px !important;
    background-color: rgba(0, 0, 0, 0.3) !important;
    border-radius: 4px !important;
}
.dialogue-text {
    font-size: 14px !important;
    color: rgb(220, 220, 230) !important;
    white-space: normal !important;
    margin: 0 !important;
}

/* === OPTIONS CONTAINER === */
.options-container {
    flex-direction: column !important;
    margin-top: 8px !important;
}

/* === DIALOG OPTION (default + hover + focus + availability tints) === */
.dialog-option {
    font-size: 13px !important;
    padding: 6px 12px !important;
    margin: 2px 0 !important;
    background-color: rgba(30, 35, 50, 0.9) !important;
    color: rgb(200, 200, 210) !important;
    border-width: 1px !important;
    border-color: rgba(120, 150, 200, 0.3) !important;
    border-radius: 3px !important;
    text-align: left !important;
    transition-property: border-color, color !important;
    transition-duration: 0.15s !important;
}

/* Hover: gold outline (default) */
.dialog-option:hover {
    border-color: rgb(255, 220, 130) !important;
    color: rgb(255, 255, 240) !important;
}

/* Focus (gamepad/keyboard): blue focus ring */
.dialog-option:focus {
    border-color: rgb(77, 163, 255) !important;
}

/* === REPUTATION-TINTED HOVER === */
/* (class set programmatically in C# based on DialogueOption.reputationTint) */
.dialog-option.req-rep-positive:hover {
    border-color: rgb(120, 220, 140) !important;  /* green */
}
.dialog-option.req-rep-negative:hover {
    border-color: rgb(240, 130, 130) !important;  /* red */
}
.dialog-option.req-rep-neutral:hover {
    border-color: rgb(180, 180, 200) !important;  /* gray */
}

/* === UNAVAILABLE OPTION (gated by condition failed) === */
.dialog-option.unavailable {
    color: rgba(180, 180, 200, 0.4) !important;
    background-color: rgba(20, 22, 30, 0.7) !important;
}
.dialog-option.unavailable:hover {
    border-color: rgba(140, 60, 60, 0.5) !important;  /* dim red */
}

/* === CLOSE BUTTON === */
.close-btn {
    width: 28px !important;
    height: 28px !important;
    background-color: rgba(40, 30, 30, 0.7) !important;
    color: rgb(200, 100, 100) !important;
    border-width: 1px !important;
    border-color: rgba(180, 80, 80, 0.5) !important;
    border-radius: 3px !important;
    font-size: 14px !important;
    -unity-font-style: bold !important;
}
.close-btn:hover {
    background-color: rgba(80, 40, 40, 0.9) !important;
    border-color: rgb(220, 100, 100) !important;
}

/* === FOOTER HINT === */
.footer-hint {
    font-size: 10px !important;
    color: rgba(180, 180, 200, 0.5) !important;
    margin-top: 8px !important;
    -unity-text-align: middle-center !important;
}
```

---

## 4.4 DialogWindow — C# code-behind

**Файл:** `Assets/_Project/Quests/UI/DialogWindow.cs` (NEW)

**Паттерн:** mirror `CharacterWindow.cs:1-1345` (UI Toolkit window с 4 FIX'ами).

### Class skeleton

```csharp
public class DialogWindow : MonoBehaviour
{
    public static DialogWindow Instance { get; private set; }

    [SerializeField] private UIDocument _doc;
    [SerializeField] private PanelSettings _panelSettings;
    [SerializeField] private VisualTreeAsset _visualTree;
    [SerializeField] private StyleSheet _styles;

    // Cached elements
    private VisualElement _root, _mainContainer, _portrait, _optionsContainer, _bodyScroll;
    private Label _npcName, _npcTitle, _repText, _dialogueText, _footerHint;
    private VisualElement _repBadge, _portraitFrame;
    private Button _closeBtn;

    // State
    private DialogueStepDto? _currentStep;
    private Coroutine _typewriter;
    private bool _isTyping;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        DontDestroyOnLoad(gameObject);

        // 4 FIX #1: hide root initially with pickingMode=Ignore
        _root = _doc.rootVisualElement;
        _root.pickingMode = PickingMode.Ignore;
        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;

        // Load UXML/USS as fallback if Inspector slot is empty
        if (_visualTree == null) _visualTree = Resources.Load<VisualTreeAsset>("UI/DialogWindow");
        if (_styles == null) _styles = Resources.Load<StyleSheet>("UI/DialogWindow");
        _visualTree.CloneTree(_root);
        _root.styleSheets.Add(_styles);

        // Cache elements
        _mainContainer = _root.Q<VisualElement>("main-container");
        _npcName = _root.Q<Label>("npc-name");
        _npcTitle = _root.Q<Label>("npc-title");
        _repText = _root.Q<Label>("rep-text");
        _repBadge = _root.Q<VisualElement>("rep-badge");
        _portrait = _root.Q<VisualElement>("portrait");
        _portraitFrame = _root.Q<VisualElement>("portrait-frame");
        _dialogueText = _root.Q<Label>("dialogue-text");
        _bodyScroll = _root.Q<ScrollView>("body-scroll");
        _optionsContainer = _root.Q<VisualElement>("options-container");
        _closeBtn = _root.Q<Button>("close-btn");
        _footerHint = _root.Q<Label>("footer-hint");

        // 4 FIX #3: MarkDirtyRepaint + schedule at +50ms
        _doc.rootVisualElement.MarkDirtyRepaint();
        _doc.rootVisualElement.schedule.Execute(() => _doc.rootVisualElement.MarkDirtyRepaint()).StartingIn(50);

        // Subscribe to client state
        if (QuestClientState.Instance != null)
            QuestClientState.Instance.OnDialogueStep += OnDialogueStepReceived;
        else
            // Retry on Update until Instance != null
            StartCoroutine(RetrySubscribe());

        // Close button
        _closeBtn.clicked += Close;

        // Hide on start
        Hide();
    }

    private void OnDestroy()
    {
        if (QuestClientState.Instance != null)
            QuestClientState.Instance.OnDialogueStep -= OnDialogueStepReceived;
    }
}
```

### Show / Hide с 4 FIX'ами

```csharp
public void Show(DialogueStepDto step)
{
    if (_root == null) return;
    _currentStep = step;

    // 4 FIX #2: inline fallback styles at first Show()
    if (!_hasShownBefore)
    {
        _mainContainer.style.position = Position.Absolute;
        _mainContainer.style.left = 50; _mainContainer.style.right = 50;
        _mainContainer.style.top = 50; _mainContainer.style.bottom = 50;
        _hasShownBefore = true;
    }

    // Populate UI
    _npcName.text = step.speakerName.ToString();
    _npcTitle.text = "";  // TODO: from NpcDefinition lookup
    SetPortrait(step.portraitRef);
    SetReputationBadge(step.reputation);   // int -100..+100
    SetDialogueText(step.text.ToString()); // typewriter effect
    BuildOptions(step.options);

    // 4 FIX #1: enable picking
    _root.pickingMode = PickingMode.Position;

    // 4 FIX #4: cursor unlock
    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;

    // 4 FIX #3: MarkDirtyRepaint
    _doc.rootVisualElement.MarkDirtyRepaint();
    _doc.rootVisualElement.schedule.Execute(() => _doc.rootVisualElement.MarkDirtyRepaint()).StartingIn(50);
}

public void Hide()
{
    if (_root == null) return;
    _root.pickingMode = PickingMode.Ignore;
    _currentStep = null;
    // 4 FIX #4: cursor lock (only if NetworkManager is listening)
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    if (_typewriter != null) { StopCoroutine(_typewriter); _typewriter = null; }
}
```

### Typewriter effect (UI Toolkit)

```csharp
private void SetDialogueText(string fullText)
{
    if (_typewriter != null) StopCoroutine(_typewriter);
    _typewriter = StartCoroutine(TypewriterRoutine(fullText, 40f)); // 40 chars/sec
}

private IEnumerator TypewriterRoutine(string fullText, float charsPerSec)
{
    _isTyping = true;
    _dialogueText.text = "";
    int len = fullText.Length;
    float delay = 1f / charsPerSec;
    for (int i = 0; i <= len; i++)
    {
        _dialogueText.text = fullText.Substring(0, i);
        // Auto-scroll to bottom
        _bodyScroll.scrollOffset = new Vector2(0, _bodyScroll.contentContainer.layout.height);
        yield return new WaitForSeconds(delay);
    }
    _isTyping = false;
    _typewriter = null;
}

public void SkipTypewriter()
{
    if (!_isTyping || _currentStep == null) return;
    if (_typewriter != null) StopCoroutine(_typewriter);
    _dialogueText.text = _currentStep.Value.text.ToString();
    _isTyping = false;
    _typewriter = null;
}
```

### Build options (с reputation-tint)

```csharp
private void BuildOptions(DialogueOptionDto[] options)
{
    _optionsContainer.Clear();
    if (options == null || options.Length == 0) return;

    for (int i = 0; i < options.Length; i++)
    {
        var opt = options[i];
        var btn = new Button(() => OnOptionClicked(i)) { text = opt.label.ToString() };
        btn.AddToClassList("dialog-option");

        // Reputation-tint class for outline color
        btn.RemoveFromClassList("req-rep-positive");
        btn.RemoveFromClassList("req-rep-negative");
        btn.RemoveFromClassList("req-rep-neutral");
        btn.RemoveFromClassList("unavailable");

        if (!opt.isAvailable)
        {
            btn.AddToClassList("unavailable");
            btn.SetEnabled(false);
            btn.tooltip = opt.hintIfUnavailable.ToString();
        }
        else
        {
            switch (opt.reputationTint)
            {
                case 1: btn.AddToClassList("req-rep-positive"); break;
                case 2: btn.AddToClassList("req-rep-negative"); break;
                default: btn.AddToClassList("req-rep-neutral"); break;
            }
        }

        _optionsContainer.Add(btn);
    }
}

private void OnOptionClicked(int optionIndex)
{
    if (_currentStep == null) return;
    var step = _currentStep.Value;
    QuestClientState.Instance?.RequestAdvanceDialogue(
        step.dialogTreeId.ToString(),
        step.currentNodeId.ToString(),
        optionIndex,
        step.speakerNpcId.ToString());
}
```

### Reputation badge color

```csharp
private void SetReputationBadge(int repValue)
{
    _repBadge.RemoveFromClassList("rep-positive");
    _repBadge.RemoveFromClassList("rep-negative");
    _repBadge.RemoveFromClassList("rep-neutral");

    if (repValue >= 50) { _repBadge.AddToClassList("rep-positive"); _repText.text = $"Дружелюбен ({repValue})"; }
    else if (repValue <= -50) { _repBadge.AddToClassList("rep-negative"); _repText.text = $"Враждебен ({repValue})"; }
    else { _repBadge.AddToClassList("rep-neutral"); _repText.text = $"Нейтрален ({repValue})"; }
}
```

---

## 4.5 Quest Log таб в CharacterWindow

**Существующий файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (1345 строк, 5 табов).

**Изменения:**

### UXML (CharacterWindow.uxml): добавить таб-кнопку + section

```xml
<!-- В <ui:VisualElement class="tabs"> добавить: -->
<ui:Button name="tab-quests" class="tab-btn" text="КВЕСТЫ" />

<!-- В <ui:VisualElement class="list-section-container"> добавить: -->
<ui:VisualElement name="quests-section" class="list-section" style="display: none;">
    <ui:ListView name="quests-list"
                 class="quest-list"
                 fixed-item-height="56"
                 show-border="false"
                 show-alternating-row-backgrounds="ContentOnly" />
    <ui:VisualElement name="quest-detail-panel" class="quest-detail-panel">
        <ui:Label name="quest-title" class="quest-title" text="—" />
        <ui:Label name="quest-stage" class="quest-stage" text="—" />
        <ui:VisualElement name="quest-objectives" class="quest-objectives" />
        <ui:Button name="track-quest-btn" class="track-btn" text="Отслеживать" />
        <ui:Button name="abandon-quest-btn" class="abandon-btn" text="Отказаться" />
    </ui:VisualElement>
</ui:VisualElement>
```

### C# (CharacterWindow.cs):

```csharp
// В EnsureBuilt() — добавить:
_questsList = _root.Q<ListView>("quests-list");
_questTitle = _root.Q<Label>("quest-title");
_questStage = _root.Q<Label>("quest-stage");
_questObjectives = _root.Q<VisualElement>("quest-objectives");
_trackBtn = _root.Q<Button>("track-quest-btn");
_abandonBtn = _root.Q<Button>("abandon-quest-btn");

// ListView: makeItem + bindItem + selectionChanged
_questsList.makeItem = MakeQuestRow;
_questsList.bindItem = BindQuestRow;
_questsList.selectionType = SelectionType.Single;
_questsList.onSelectionChange += OnQuestSelected;

_trackBtn.clicked += () => {
    if (_selectedQuestId == null) return;
    QuestClientState.Instance?.RequestTrackQuest(_selectedQuestId, track: true);
};
_abandonBtn.clicked += () => { /* TODO: not in v1 */ };

// Lazy subscribe (idempotent)
SubscribeQuests();

private void SubscribeQuests()
{
    if (_isQuestsSubscribed) return;
    if (QuestClientState.Instance == null) return;
    QuestClientState.Instance.OnSnapshotUpdated += OnQuestsSnapshotUpdated;
    QuestClientState.Instance.OnQuestResult += OnQuestResult;
    _isQuestsSubscribed = true;
    QuestClientState.Instance.RequestRefreshQuests();
}

// Cross-tab cache refresh pattern (R3-005 lesson, per project-c-ui-as-tab skill)
private void OnQuestsSnapshotUpdated(QuestSnapshotDto snapshot)
{
    // Always update shared labels
    if (_creditsLabel != null) _creditsLabel.text = $"Кредиты: {snapshot.credits:F0} CR";

    // Always refresh cache (projection of server state)
    RefreshQuestsCache(snapshot);

    // Only rebuild UI on active tab
    if (_activeTab == "quests") ApplyQuestsFilters();
}

private void OnDisable()
{
    if (QuestClientState.Instance != null)
    {
        QuestClientState.Instance.OnSnapshotUpdated -= OnQuestsSnapshotUpdated;
        QuestClientState.Instance.OnQuestResult -= OnQuestResult;
        _isQuestsSubscribed = false;
    }
}

// SwitchTab — добавить case:
case "quests":
    SetActiveTabVisual(_questsTabBtn);
    _characterSection.style.display = DisplayStyle.None;
    _shipSection.style.display = DisplayStyle.None;
    _reputationSection.style.display = DisplayStyle.None;
    _contractsSection.style.display = DisplayStyle.None;
    _inventorySection.style.display = DisplayStyle.None;
    _questsSection.style.display = DisplayStyle.Flex;
    _activeTab = "quests";
    if (QuestClientState.Instance != null)
    {
        ApplyQuestsFilters();
    }
    break;
```

---

## 4.6 Quest Tracker (compact HUD overlay)

**Файл:** `Assets/_Project/UI/Resources/UI/QuestTracker.uxml` (NEW)

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="tracker-root" class="quest-tracker" picking-mode="Ignore">
        <ui:Label name="tracker-title" class="tracker-title" text="АКТИВНЫЙ КВЕСТ" />
        <ui:Label name="tracker-quest-name" class="tracker-quest-name" text="—" />
        <ui:VisualElement name="tracker-objectives" class="tracker-objectives" />
    </ui:VisualElement>
</ui:UXML>
```

**USS:**

```css
.quest-tracker {
    position: absolute !important;
    top: 12px !important;
    right: 12px !important;
    width: 320px !important;
    background-color: rgba(10, 14, 22, 0.85) !important;
    border-left-width: 3px !important;
    border-left-color: rgb(255, 220, 130) !important;
    padding: 8px 12px !important;
    color: rgb(220, 220, 230) !important;
    display: flex !important;
    flex-direction: column !important;
    /* 4 FIX #1: pickingMode = Ignore always (не stealing world clicks) */
}
.tracker-title {
    font-size: 9px !important;
    color: rgba(180, 180, 200, 0.6) !important;
    -unity-font-style: bold !important;
    margin-bottom: 2px !important;
}
.tracker-quest-name {
    font-size: 13px !important;
    color: rgb(255, 220, 130) !important;
    -unity-font-style: bold !important;
    margin-bottom: 4px !important;
}
.tracker-objectives {
    flex-direction: column !important;
}
.tracker-objective {
    flex-direction: row !important;
    align-items: center !important;
    font-size: 11px !important;
    margin: 1px 0 !important;
}
.tracker-bullet {
    width: 12px !important;
    height: 12px !important;
    margin-right: 4px !important;
    border-radius: 6px !important;
    background-color: rgba(120, 150, 200, 0.3) !important;
    border-width: 1px !important;
    border-color: rgba(120, 150, 200, 0.6) !important;
}
.tracker-objective.completed .tracker-bullet {
    background-color: rgba(80, 180, 100, 0.7) !important;
    border-color: rgb(120, 220, 140) !important;
}
.tracker-objective.completed .tracker-text {
    color: rgba(180, 180, 200, 0.5) !important;
    -unity-font-style: italic !important;
}
.tracker-text {
    color: rgb(220, 220, 230) !important;
    flex-grow: 1 !important;
}
.tracker-counter {
    color: rgb(120, 200, 120) !important;
    font-size: 10px !important;
    margin-left: 4px !important;
}
```

**C# code-behind** (`Assets/_Project/Quests/UI/QuestTracker.cs`):

```csharp
public class QuestTracker : MonoBehaviour
{
    public static QuestTracker Instance { get; private set; }
    [SerializeField] private UIDocument _doc;
    private VisualElement _root, _objectivesContainer;
    private Label _questName;

    private void OnEnable()
    {
        if (QuestClientState.Instance != null)
            QuestClientState.Instance.OnSnapshotUpdated += OnSnapshotUpdated;
    }

    private void OnDisable()
    {
        if (QuestClientState.Instance != null)
            QuestClientState.Instance.OnSnapshotUpdated -= OnSnapshotUpdated;
    }

    private void OnSnapshotUpdated(QuestSnapshotDto snapshot)
    {
        if (snapshot.trackedQuestId.IsEmpty)
        {
            _root.style.display = DisplayStyle.None;
            return;
        }

        var tracked = snapshot.activeQuests.FirstOrDefault(
            q => q.questId.Equals(snapshot.trackedQuestId));
        if (tracked.Equals(default(QuestDto)))
        {
            _root.style.display = DisplayStyle.None;
            return;
        }

        _root.style.display = DisplayStyle.Flex;
        _questName.text = tracked.displayName.ToString();
        // For each objective in currentStage, render row with bullet + text + counter
        // (this requires extra DTO data — see OPEN QUESTION #5)
    }
}
```

---

## 4.7 Reputation в CharacterWindow

**Существующий код:** `CharacterWindow.cs:80, 89, 393-398, 507` — таб "РЕПУТАЦИЯ" с `ListView _reputationList` и `ReputationListItem { factionId, displayName, value, color }`. `RefreshReputationCache` (line 507) — empty cache.

**v2 изменение:**

```csharp
// In EnsureBuilt (already exists for _reputationList)
_subscribeReputation = false;  // new field

// New method:
private void SubscribeReputation()
{
    if (_isReputationSubscribed) return;
    if (ReputationClientState.Instance == null) return;
    ReputationClientState.Instance.OnReputationUpdated += OnReputationUpdated;
    _isReputationSubscribed = true;
    ReputationClientState.Instance.RequestRefresh();
}

private void OnReputationUpdated(ReputationSnapshotDto snapshot)
{
    // Always refresh cache
    RefreshReputationCache(snapshot);

    // Only rebuild UI on active tab
    if (_activeTab == "reputation") ApplyReputationFilters();
}

// OnDisable
if (ReputationClientState.Instance != null && _isReputationSubscribed)
{
    ReputationClientState.Instance.OnReputationUpdated -= OnReputationUpdated;
    _isReputationSubscribed = false;
}
```

**`ReputationClientState` (NEW, `ProjectC.Reputation` namespace):**

```csharp
public class ReputationClientState : MonoBehaviour
{
    public static ReputationClientState Instance { get; private set; }
    public ReputationSnapshotDto? CurrentSnapshot { get; private set; }
    public event Action<ReputationSnapshotDto> OnReputationUpdated;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void OnSnapshotReceived(ReputationSnapshotDto snapshot)
    {
        CurrentSnapshot = snapshot;
        OnReputationUpdated?.Invoke(snapshot);
    }

    public void RequestRefresh()
    {
        // Find QuestServer, send RequestRefreshReputationRpc
        // OR — if server is auto-broadcasting reputation changes, no action needed
    }
}
```

---

## 4.8 Pitfall-лист (UI-specific)

| # | Pitfall | Источник |
|---|---------|---------|
| 1 | `pickingMode = Ignore` on Hide, `Position` on Show | `CharacterWindow.cs:1180, 1203` |
| 2 | Inline fallback styles at first Show() | `CharacterWindow.cs:1244` |
| 3 | Cursor unlock on Show, lock on Hide | `CharacterWindow.cs:1228-1241` |
| 4 | `MarkDirtyRepaint()` + `schedule.Execute(StartingIn(50))` | `CharacterWindow.cs:441-445, 1193-1197` |
| 5 | Cross-tab cache: refresh always, rebuild only on active tab | `project-c-ui-as-tab` skill R3-005 |
| 6 | UI Toolkit `Label` typewriter via coroutine + `Substring` (no built-in) | subagent analysis §5.7 |
| 7 | `:hover` and `:focus` USS pseudo-classes (not UnityEvents) | subagent analysis §5.5 |
| 8 | Reputation-tint classes via `AddToClassList`/`RemoveFromClassList` (not `SetStyleProperty`) | subagent analysis §5.6 |
| 9 | Gamepad navigation: `Tab/Arrow` keys move focus (default `FocusController`) | subagent analysis §6.5 |
| 10 | `DisplayStyle.None` not `display: none` for hiding sections | UI Toolkit API |

---

## 4.9 UX flows (end-to-end)

### Flow 1: Player talks to NPC
1. Player approaches NPC within `InteractionRadius` (default 3m).
2. HUD shows floating tooltip: "Нажмите E чтобы поговорить".
3. Player presses E → `QuestInteractor.TryTalkToNpc()` → `InteractableManager.FindNearestNpc` → `QuestClientState.RequestTalkToNpc(npcId)`.
4. Server validates (zone, distance, NPC exists, is not busy).
5. Server builds `DialogueStepDto` (root node of NPC's `defaultDialogTree`, options filtered by conditions).
6. Server sends `DialogueStepDto` via `NetworkPlayer.ReceiveDialogueStepTargetRpc`.
7. `QuestClientState.OnDialogueStep` fires → `DialogWindow.Show(step)`.
8. Window: 4 FIX'ы apply, cursor unlock, typewriter starts.
9. Player reads text (40 chars/sec), options appear below.
10. Player hovers an option → outline color depends on `reputationTint`.
11. Player clicks option → `OnOptionClicked(i)` → `RequestAdvanceDialogueRpc`.
12. Server validates option, fires `DialogueAction`s (e.g. `AddReputation`, `OfferQuest`), computes next node.
13. Repeat 5-12 until node has no edges OR option `EndConversation` chosen.
14. `DialogWindow.Hide()`, cursor lock, player back in world.

### Flow 2: Player accepts a quest
1. ... (continuing from Flow 1) ...
2. Player chooses option with `action = OfferQuest(questId)`.
3. Server fires `OfferQuest` action: `QuestWorld.TryOffer(playerId, questId)`.
4. Server validates prerequisites (faction rep ≥ required, no other active quest with same id).
5. Server adds `QuestInstance` to `_questsByPlayer[playerId]`, state = `Active`, current stage = first stage.
6. Server fires `onEnterActions` of first stage (e.g. `EmitEvent("started_quest_find_artifact")`).
7. Server builds `QuestSnapshotDto` and pushes to player.
8. `QuestClientState.OnSnapshotUpdated` fires → CharacterWindow quest tab updates (if active).
9. QuestTracker updates if quest is tracked.
10. Toast notification: "Новый квест: Найди артефакт".

### Flow 3: Player completes an objective
1. Player performs an action (e.g. picks up an item).
2. `InventoryServer.AddItem` is called, `InventoryWorld.CountOf` increases.
3. `QuestTriggerService` polls every 5 sec (or fires from `InventoryWorld.AddItem` if event bus added).
4. Trigger `ItemInInventoryTrigger` evaluates all active quests for this player.
5. For each quest with `HaveItem(itemId, qty)` objective → mark completed if satisfied.
6. If all required objectives in current stage are completed → advance to next stage.
7. Fire `onCompleteActions` of old stage + `onEnterActions` of new stage.
8. If new stage is null (end) → fire `rewards` of quest, mark quest state = `Completed`, fire `EmitEvent("quest_completed")`.
9. Server sends updated `QuestSnapshotDto` + `QuestResultDto`.
10. UI: quest log shows progress, quest tracker updates, toast "Цель выполнена!".

### Flow 4: Player turns in quest
1. Player returns to `questTurnIns` NPC, talks.
2. Dialog tree has `TurnIn` node (visually marked).
3. Player chooses option with `action = CompleteObjective(questId, finalObjectiveId)`.
4. Server validates: quest is in stage that allows turn-in, player in zone, all required objectives met.
5. Server fires `rewards`: `GiveCredits`, `GiveItem`, `AddReputation`.
6. Server marks quest state = `TurnedIn`, removes from `_questsByPlayer[active]`, moves to `completedQuests`.
7. Server sends updated `QuestSnapshotDto` + multiple `QuestResultDto` (one per reward).
8. UI: toast "Квест выполнен! +500 CR + iron_ingot × 3 + 25 репутации с GuildOfThoughts".
9. QuestTracker: quest disappears (or moves to "completed" section if filter set).
