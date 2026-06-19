# 04 — Dialog & Dispatcher UI

> **Цель:** Спроектировать UI Toolkit окно `CommPanelWindow` для общения
> с диспетчером и `CommPanelToast` для wrong-pad warning. По канону
> `DialogWindow` (см. `Assets/_Project/Quests/UI/DialogWindow.cs`).

---

## 1. UX-flow коммуникации (Q7 — двусторонняя обязательная)

### 1.1 Базовая идея

Игрок входит в **OuterCommZone** → система показывает **HUD-hint**
«T — связаться с диспетчером» → игрок нажимает **T** → открывается
**CommPanel** (модальное окно). Диспетчер говорит приветственную фразу,
предлагает «Запросить посадку» / «Отмена». После запроса — диспетчер
**назначает pad** → игрок **подтверждает («Хорошо» / «Отбой»)** → переходит
в режим ожидания с таймером окна, кнопкой «Отменить запрос» (Q7: двусторонняя
связь обязательна для MVP).

```
┌──────────────────────────────────────────────────┐
│ ╔══════════════ Примум — Диспетчерская ════════╗ │
│ ║                                              ║ │
│ ║  «Борт 7-Альфа, Примум на связи.            ║ │
│ ║   Чем могу помочь?»                         ║ │
│ ║                                              ║ │
│ ║  ─────────────────────────────────────       ║ │
│ ║                                              ║ │
│ ║  ┌──────────────┐    ┌──────────────┐         ║ │
│ ║  │  Запросить   │    │    Отмена    │         ║ │
│ ║  │   посадку    │    │              │         ║ │
│ ║  └──────────────┘    └──────────────┘         ║ │
│ ║                                              ║ │
│ ╚══════════════════════════════════════════════╝ │
└──────────────────────────────────────────────────┘
```

### 1.2 После назначения pad (Q7: ждёт подтверждения)

```
┌──────────────────────────────────────────────────┐
│ ╔══════════════ Примум — Диспетчерская ════════╗ │
│ ║                                              ║ │
│ ║  «Борт 7-Альфа, назначаю pad #5.             ║ │
│ ║   Подход: высота 4378, курс 270.             ║ │
│ ║   Окно посадки: 1:30.                        ║ │
│ ║   Подтверждаете?»                            ║ │
│ ║                                              ║ │
│ ║  ─────────────────────────────────────       ║ │
│ ║                                              ║ │
│ ║  ┌──────────────┐    ┌──────────────┐         ║ │
│ ║  │   Хорошо     │    │    Отбой     │         ║ │
│ ║  │              │    │              │         ║ │
│ ║  └──────────────┘    └──────────────┘         ║ │
│ ║                                              ║ │
│ ╚══════════════════════════════════════════════╝ │
└──────────────────────────────────────────────────┘
```

### 1.3 После подтверждения (Assigned, идёт таймер)

```
┌──────────────────────────────────────────────────┐
│ ╔══════════════ Примум — Диспетчерская ════════╗ │
│ ║                                              ║ │
│ ║  «Борт 7-Альфа, добро. Следуйте к pad #5.   ║ │
│ ║   Удачной посадки.»                          ║ │
│ ║                                              ║ │
│ ║  ⏱  01:30  ━━━━━●━━━━━━━━━━━━━━━━  00:00    ║ │
│ ║                                              ║ │
│ ║  ┌──────────────┐                            ║ │
│ ║  │   Отменить   │                            ║ │
│ ║  │    запрос    │                            ║ │
│ ║  └──────────────┘                            ║ │
│ ║                                              ║ │
│ ╚══════════════════════════════════════════════╝ │
└──────────────────────────────────────────────────┘
```

### 1.4 После успешной стыковки

```
┌──────────────────────────────────────────────────┐
│ ╔══════════════ Примум — Диспетчерская ════════╗ │
│ ║                                              ║ │
│ ║  «Борт 7-Альфа, зафиксирована стыковка.     ║ │
│ ║   Двигатели заблокированы.                   ║ │
│ ║   Удачной торговли.»                         ║ │
│ ║                                              ║ │
│ ║  ─────────────────────────────────────       ║ │
│ ║                                              ║ │
│ ║  ┌──────────────┐                            ║ │
│ ║  │    F —       │                            ║ │
│ ║  │  Отстыковка  │                            ║ │
│ ║  └──────────────┘                            ║ │
│ ║                                              ║ │
│ ╚══════════════════════════════════════════════╝ │
└──────────────────────────────────────────────────┘
```

### 1.5 Wrong-pad toast

**Отдельный элемент** — маленький toast в углу экрана (НЕ модальный):

```
┌────────────────────────────────────┐
│ ⚠️  Вы на чужом pad'е.            │
│     Перепаркуйтесь на pad #N       │
└────────────────────────────────────┘
```

Fade-out 4 сек. Не блокирует игру.

---

## 2. UX-детали

### 2.1 Открытие / закрытие

| Действие | Результат |
|----------|-----------|
| Игрок в OuterCommZone + нажал **T** (без Docked) | Открыть CommPanel |
| Игрок вне OuterCommZone + нажал **T** | Ничего (или hint «нет связи») |
| Игрок нажал **T** во время Docked | Открыть CommPanel в режиме Docked (предлагает F для отстыковки) |
| Игрок нажал **Esc** при открытой CommPanel | Закрыть |
| CommPanel в режиме Assigned, окно истекло | Автозакрытие + переход в Idle |
| Клик вне CommPanel (pickingMode=Ignore на root) | Ничего (модальное окно) |

### 2.2 Тон диспетчера

| Контекст | Тон | Пример фразы |
|----------|-----|--------------|
| Greeting | Деловой, нейтральный | «Борт [ID], [Станция] на связи» |
| Assigned | Деловой, чёткий | «Назначаю pad #N, окно X секунд» |
| WindowExpired | Терпеливый | «Борт [ID], окно истекло. Повторите запрос» |
| Touchdown | Подтверждающий | «Зафиксирована стыковка, добро пожаловать» |
| Takeoff | Нейтральный | «Отстыковка разрешена, удачного полёта» |
| WrongPad | Нейтрально-предупреждающий | «Борт [ID], вы на чужом pad'е» |

### 2.3 Что НЕ показываем в MVP

- Никакой typewriter-эффект (диспетчер — оператор, не нарратив).
- Никаких иконок эмоций / portrait.
- Никакого sound (Phase 3: voice line через AudioSource).
- Никаких быстрых ответов игрока текстом (только кнопки действий).
- Никакой reputation badge (Phase 3).

---

## 3. UXML структура `CommPanel.uxml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
  <ui:VisualElement name="root" class="comm-panel-root">
    <ui:VisualElement name="panel" class="comm-panel">
      <ui:Label name="header" text="Примум — Диспетчерская" class="comm-panel-header" />
      <ui:VisualElement name="divider1" class="comm-panel-divider" />
      <ui:Label name="dispatcher-message" text="" class="comm-panel-message" />
      <ui:VisualElement name="divider2" class="comm-panel-divider" />
      <ui:ProgressBar name="landing-window-bar" low-value="0" high-value="100" class="comm-panel-progress" />
      <ui:VisualElement name="buttons-container" class="comm-panel-buttons">
        <ui:Button name="primary-action-button" text="Запросить посадку" class="comm-panel-button-primary" />
        <ui:Button name="secondary-action-button" text="Отмена" class="comm-panel-button-secondary" />
      </ui:VisualElement>
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

### 3.1 Состояние UI (state-driven) — Q7: добавлен AwaitingConfirmation

UI показывает **разный набор элементов** в зависимости от текущего
`DockingStatus` + `PendingAssignment`:

| Status | Header | Message | Progress | Primary button | Secondary button |
|--------|--------|---------|----------|----------------|------------------|
| **Idle** (в зоне, не assigned) | `[Станция] — Диспетчерская` | Greeting фраза | Hidden | `Запросить посадку` | `Отмена` (закрыть) |
| **AwaitingConfirmation** (Q7) | То же | Assigned фраза + подход | Hidden | `Хорошо` | `Отбой` |
| **Assigned** (confirmed) | То же | «Следуйте к pad #N» фраза | Visible (timer) | Hidden | `Отменить запрос` |
| **TouchedDown** (на правильном pad'е) | То же | Touchdown фраза | Hidden | `F — Отстыковка` | Hidden |
| **TouchedDown** (на wrong pad'е) | То же | WrongPad фраза | Hidden | `Перепарковаться` | `Отмена` |
| **Cancelled** | То же | WindowExpired фраза | Hidden | `Запросить снова` | `Отмена` |

---

## 4. USS структура `CommPanel.uss`

```css
/* === Root === */
.comm-panel-root {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    align-items: center;
    justify-content: center;
    background-color: rgba(0, 0, 0, 0.4);  /* полупрозрачный overlay */
    display: none;  /* Hidden по умолчанию, panel показывается через IsOpen=true */
}

.comm-panel-root.is-open {
    display: flex;
}

/* === Panel === */
.comm-panel {
    width: 560px;
    background-color: rgb(15, 25, 38);
    border-width: 2px;
    border-color: rgb(80, 110, 145);
    border-radius: 6px;
    padding: 18px 22px;
    flex-direction: column;
    picking-mode: ignore;  /* клик "снаружи" не пробрасывается */
}

.comm-panel-header {
    color: rgb(220, 200, 110);
    font-size: 18px;
    -unity-font-style: bold;
    margin-bottom: 10px;
}

.comm-panel-divider {
    height: 1px;
    background-color: rgb(60, 75, 95);
    margin: 10px 0;
}

.comm-panel-message {
    color: rgb(220, 230, 245);
    font-size: 15px;
    white-space: normal;
    min-height: 60px;
}

.comm-panel-progress {
    height: 6px;
    margin: 10px 0;
    display: none;
}

.comm-panel-progress.is-active {
    display: flex;
}

.comm-panel-buttons {
    flex-direction: row;
    justify-content: flex-end;
    margin-top: 14px;
}

.comm-panel-button-primary {
    background-color: rgb(70, 130, 200);
    color: white;
    padding: 8px 20px;
    margin-left: 8px;
    border-radius: 4px;
    -unity-font-style: bold;
}

.comm-panel-button-primary:hover {
    background-color: rgb(95, 155, 220);
}

.comm-panel-button-secondary {
    background-color: rgb(60, 75, 95);
    color: rgb(180, 195, 215);
    padding: 8px 20px;
    margin-left: 8px;
    border-radius: 4px;
}

.comm-panel-button-secondary:hover {
    background-color: rgb(80, 95, 115);
}

.comm-panel-button-hidden {
    display: none;
}
```

---

## 5. Структура `CommPanelWindow.cs`

### 5.1 Singleton pattern (как в DialogWindow)

```csharp
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Docking.Client;
using ProjectC.Docking.Dto;
using ProjectC.Docking.Network;
using ProjectC.Player;
using ProjectC.Quests.Client;  // QuestClientState — нет, не нужен

namespace ProjectC.Docking.UI {
    /// <summary>
    /// Окно диспетчерской связи. UI Toolkit, модальное.
    /// Подписывается на DockingClientState, обновляет UI по RPCs.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CommPanelWindow : MonoBehaviour {
        public static CommPanelWindow Instance { get; private set; }

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset commPanelUxml;
        [SerializeField] private StyleSheet commPanelUss;

        [Header("Toast link")]
        [SerializeField] private CommPanelToast toast;

        public bool IsOpen { get; private set; }

        // UI refs
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _panel;
        private Label _header;
        private Label _message;
        private ProgressBar _progressBar;
        private Button _primaryButton;
        private Button _secondaryButton;

        // State
        private DockingStatus _currentStatus = DockingStatus.Idle;
        private string _currentStationId;
        private string _currentPadId;
        private float _windowExpireTime;  // для прогресс-бара
        // Q7: AwaitingConfirmation — отдельное состояние UI (между request и confirm)
        private DockingAssignmentDto? _awaitingConfirmation;

        private bool _built = false;
        private bool _subscribed = false;

        private void Awake() {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();
            if (commPanelUxml == null) commPanelUxml = Resources.Load<VisualTreeAsset>("UI/CommPanel");
            if (commPanelUss == null) commPanelUss = Resources.Load<StyleSheet>("UI/CommPanel");
        }

        private void OnEnable() {
            EnsureBuilt();
            TrySubscribe();
        }

        private void Start() {
            EnsureBuilt();  // backup
        }

        private void OnDisable() {
            TryUnsubscribe();
        }

        private void OnDestroy() {
            if (Instance == this) Instance = null;
        }

        private void EnsureBuilt() {
            if (_built) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) {
                Debug.LogError("[CommPanelWindow] нет UIDocument", this);
                return;
            }
            // Resources.Load fallback (см. DialogWindow.cs:80-84)
            if (commPanelUxml == null) commPanelUxml = Resources.Load<VisualTreeAsset>("UI/CommPanel");
            if (commPanelUss == null) commPanelUss = Resources.Load<StyleSheet>("UI/CommPanel");
            if (commPanelUxml == null) {
                Debug.LogError("[CommPanelWindow] нет UXML", this);
                return;
            }
            _doc.visualTreeAsset = commPanelUxml;
            _root = _doc.rootVisualElement;
            _root.styleSheets.Add(commPanelUss);
            // pickingMode для модального
            _root.pickingMode = PickingMode.Ignore;
            _panel = _root.Q<VisualElement>("panel");
            if (_panel != null) _panel.pickingMode = PickingMode.Position;  // кнопки внутри ловят мышь

            _header = _root.Q<Label>("header");
            _message = _root.Q<Label>("dispatcher-message");
            _progressBar = _root.Q<ProgressBar>("landing-window-bar");
            _primaryButton = _root.Q<Button>("primary-action-button");
            _secondaryButton = _root.Q<Button>("secondary-action-button");

            // Подписки на кнопки
            _primaryButton.clicked += OnPrimaryClicked;
            _secondaryButton.clicked += OnSecondaryClicked;

            _built = true;
            SetOpen(false);
            UpdateUI();
        }

        private void TrySubscribe() {
            if (_subscribed) return;
            if (DockingClientState.Instance == null) return;
            DockingClientState.Instance.OnAwaitingConfirmation += HandleAwaitingConfirmation;  // Q7
            DockingClientState.Instance.OnAssignmentReceived += HandleAssignmentFailed;       // Q7: только failure
            DockingClientState.Instance.OnStatusReceived += HandleStatusReceived;
            DockingClientState.Instance.OnTakeoffApproved += HandleTakeoffApproved;
            DockingClientState.Instance.OnTouchedDown += HandleTouchedDown;
            _subscribed = true;
        }

        private void TryUnsubscribe() {
            if (!_subscribed) return;
            if (DockingClientState.Instance != null) {
                DockingClientState.Instance.OnAwaitingConfirmation -= HandleAwaitingConfirmation;
                DockingClientState.Instance.OnAssignmentReceived -= HandleAssignmentFailed;
                DockingClientState.Instance.OnStatusReceived -= HandleStatusReceived;
                DockingClientState.Instance.OnTakeoffApproved -= HandleTakeoffApproved;
                DockingClientState.Instance.OnTouchedDown -= HandleTouchedDown;
            }
            _subscribed = false;
        }

        // ====================================================
        // OPEN/CLOSE
        // ====================================================

        public void SetOpen(bool open) {
            if (!_built) return;
            IsOpen = open;
            if (_root != null) _root.EnableInClassList("is-open", open);
        }

        public void ToggleOpen() {
            SetOpen(!IsOpen);
            if (IsOpen) UpdateUI();
        }

        // ====================================================
        // STATE HANDLERS
        // ====================================================

        private void HandleAwaitingConfirmation(DockingAssignmentDto assignment, bool isSuccess) {
            // Q7: сервер назначил pad, ждём подтверждения игрока
            _awaitingConfirmation = assignment;
            _currentStationId = assignment.stationId;
            _currentPadId = assignment.padId;
            UpdateUI();
        }

        private void HandleAssignmentFailed(DockingAssignmentDto assignment) {
            // Q7: failure (no pad, rate limit, etc) — UI покажет сообщение
            HandleFail(assignment);
        }

        private void HandleStatusReceived(DockingStatusDto status) {
            _currentStatus = status.status;
            _currentStationId = status.stationId;
            _currentPadId = status.padId;
            // Q7: если Assigned — клиент подтвердил, чистим awaiting
            if (status.status == DockingStatus.Assigned) {
                _awaitingConfirmation = null;
            } else if (status.status == DockingStatus.Cancelled) {
                _awaitingConfirmation = null;
            }
            // Неправильный pad → показать toast
            if (status.status == DockingStatus.WrongPad && toast != null) {
                toast.ShowWrongPadWarning(_currentStationId, _currentPadId);
            }
            UpdateUI();
        }

        private void HandleTouchedDown(DockingStatusDto status) {
            // Зарезервировано для визуальных эффектов (Phase 3)
            UpdateUI();
        }

        private void HandleTakeoffApproved(ulong shipNetId) {
            _currentStatus = DockingStatus.Idle;
            _currentStationId = null;
            _currentPadId = null;
            _awaitingConfirmation = null;
            UpdateUI();
        }

        private void HandleFail(DockingAssignmentDto assignment) {
            string msg = assignment.failReason switch {
                "NO_SUITABLE_PAD" => "Диспетчер: «Свободных pad'ов нет, попробуйте позже». ",
                "RATE_LIMITED"     => "Диспетчер: «Слишком частые запросы, подождите». ",
                "STATION_FULL"     => "Диспетчер: «Станция переполнена, попробуйте позже». ",
                "STATION_NOT_FOUND"=> "Диспетчер: «Связь потеряна, повторите». ",
                _                  => $"Диспетчер: «Ошибка: {assignment.failReason}». "
            };
            if (_message != null) _message.text = msg;
            _currentStatus = DockingStatus.Idle;
            UpdateUI();
        }

        // ====================================================
        // UI UPDATE
        // ====================================================

        private void UpdateUI() {
            if (!_built) return;

            // Q7: если ждём подтверждения — UI в AwaitingConfirmation
            if (_awaitingConfirmation.HasValue && _awaitingConfirmation.Value.success) {
                // Header
                if (_header != null) {
                    string stationName = "Диспетчерская";
                    var st = DockingZoneRegistry.GetStation(_awaitingConfirmation.Value.stationId);
                    if (st != null) stationName = $"{st.DisplayName} — Диспетчерская";
                    _header.text = stationName;
                }
                // Message (фраза диспетчера + подход)
                if (_message != null) {
                    var a = _awaitingConfirmation.Value;
                    _message.text = $"Диспетчер: «{a.voiceLine} " +
                                    $"Подход: высота {(int)a.approachAltitude}, курс {(int)a.approachHeading}. " +
                                    $"Окно — {(int)a.landingWindowSeconds} сек. Подтверждаете?»";
                }
                // Progress bar hidden
                if (_progressBar != null) _progressBar.RemoveFromClassList("is-active");
                // Buttons
                _primaryButton.text = "Хорошо";
                _primaryButton.RemoveFromClassList("comm-panel-button-hidden");
                _secondaryButton.text = "Отбой";
                _secondaryButton.RemoveFromClassList("comm-panel-button-hidden");
                return;
            }

            // Header (название станции)
            if (_header != null) {
                string stationName = "Диспетчерская";
                if (!string.IsNullOrEmpty(_currentStationId)) {
                    var st = DockingZoneRegistry.GetStation(_currentStationId);
                    if (st != null) stationName = $"{st.DisplayName} — Диспетчерская";
                } else {
                    var nearest = DockingZoneRegistry.LocalPlayerStation ?? DockingZoneRegistry.LocalPlayerShipStation;
                    if (nearest != null) stationName = $"{nearest.DisplayName} — Диспетчерская";
                }
                _header.text = stationName;
            }

            // Message
            if (_message != null) {
                _message.text = GetMessageForStatus(_currentStatus);
            }

            // Progress bar (для Assigned)
            if (_progressBar != null) {
                bool active = _currentStatus == DockingStatus.Assigned;
                _progressBar.EnableInClassList("is-active", active);
                if (active) {
                    float remain = Mathf.Max(0f, _windowExpireTime - Time.time);
                    float total = 90f;  // из DockingWorld, для MVP hardcode; в Phase 3 передавать
                    _progressBar.value = (remain / total) * 100f;
                    _progressBar.title = $"{Mathf.FloorToInt(remain / 60f)}:{Mathf.FloorToInt(remain % 60f):00}";
                }
            }

            // Buttons
            if (_primaryButton != null && _secondaryButton != null) {
                ConfigureButtons(_currentStatus);
            }
        }

        private string GetMessageForStatus(DockingStatus status) {
            return status switch {
                DockingStatus.Idle        => "Диспетчер: «На связи, жду ваших распоряжений».",
                DockingStatus.Assigned    => $"Диспетчер: «Борт, добро. Следуйте к pad #{(string.IsNullOrEmpty(_currentPadId) ? "?" : _currentPadId)}».",
                DockingStatus.Docked      => "Диспетчер: «Стыковка зафиксирована. Двигатели заблокированы. Удачной торговли».",
                DockingStatus.WrongPad    => $"Диспетчер: «Борт, вы на чужом pad'е (#{_currentPadId}). Перепаркуйтесь на назначенный».",
                DockingStatus.Cancelled   => "Диспетчер: «Окно посадки истекло. Повторите запрос, если ещё нужно».",
                _                         => ""
            };
        }

        private void ConfigureButtons(DockingStatus status) {
            switch (status) {
                case DockingStatus.Idle:
                    _primaryButton.text = "Запросить посадку";
                    _primaryButton.RemoveFromClassList("comm-panel-button-hidden");
                    _secondaryButton.text = "Отмена";
                    _secondaryButton.RemoveFromClassList("comm-panel-button-hidden");
                    break;
                case DockingStatus.Assigned:
                    _primaryButton.AddToClassList("comm-panel-button-hidden");
                    _secondaryButton.text = "Отменить запрос";
                    _secondaryButton.RemoveFromClassList("comm-panel-button-hidden");
                    break;
                case DockingStatus.Docked:
                    _primaryButton.text = "F — Отстыковка";
                    _primaryButton.RemoveFromClassList("comm-panel-button-hidden");
                    _secondaryButton.AddToClassList("comm-panel-button-hidden");
                    break;
                case DockingStatus.WrongPad:
                    _primaryButton.text = "Перепарковаться";
                    _primaryButton.RemoveFromClassList("comm-panel-button-hidden");
                    _secondaryButton.text = "Закрыть";
                    _secondaryButton.RemoveFromClassList("comm-panel-button-hidden");
                    break;
                case DockingStatus.Cancelled:
                    _primaryButton.text = "Запросить снова";
                    _primaryButton.RemoveFromClassList("comm-panel-button-hidden");
                    _secondaryButton.text = "Закрыть";
                    _secondaryButton.RemoveFromClassList("comm-panel-button-hidden");
                    break;
            }
        }

        // ====================================================
        // BUTTON HANDLERS
        // ====================================================

        private void OnPrimaryClicked() {
            // Q7: AwaitingConfirmation — кнопка "Хорошо"
            if (_awaitingConfirmation.HasValue) {
                ConfirmAssignment(true);
                return;
            }
            switch (_currentStatus) {
                case DockingStatus.Idle:
                case DockingStatus.Cancelled:
                    RequestDocking();
                    break;
                case DockingStatus.Docked:
                    // F выходит — вызывающий код отправит RequestTakeoffRpc.
                    // Здесь просто закрываем панель и подсказываем F.
                    SetOpen(false);
                    break;
                case DockingStatus.WrongPad:
                    RequestDocking();  // повторный запрос на правильный pad
                    break;
            }
        }

        private void OnSecondaryClicked() {
            // Q7: AwaitingConfirmation — кнопка "Отбой"
            if (_awaitingConfirmation.HasValue) {
                ConfirmAssignment(false);
                return;
            }
            switch (_currentStatus) {
                case DockingStatus.Idle:
                case DockingStatus.WrongPad:
                case DockingStatus.Cancelled:
                case DockingStatus.Docked:
                    SetOpen(false);
                    break;
                case DockingStatus.Assigned:
                    RequestCancelAssignment();
                    break;
            }
        }

        private void RequestDocking() {
            // Q10: проверяем что игрок в корабле (пилотирует)
            if (!DockingClientState.IsLocalPlayerPilotingShip()) {
                // T должно было игнорироваться вне кресла, но защита на сервере тоже
                if (_message != null) _message.text = "Диспетчер: «Борт, мне нужен корабль. Сядьте в кресло пилота».";
                return;
            }
            var server = DockingServer.Instance;
            if (server == null) return;
            var station = DockingZoneRegistry.LocalPlayerStation ?? DockingZoneRegistry.LocalPlayerShipStation;
            if (station == null) return;
            ulong shipNetId = GetLocalShipNetworkObjectId();
            if (shipNetId == 0) {
                if (_message != null) _message.text = "Диспетчер: «Борт, без корабля на стыковку не пущу».";
                return;
            }
            server.RequestDockingRpc(station.StationId, shipNetId);
        }

        // Q7: новая — отправляет RequestConfirmAssignmentRpc
        private void ConfirmAssignment(bool accept) {
            ulong shipNetId = GetLocalShipNetworkObjectId();
            if (shipNetId == 0) return;
            var server = DockingServer.Instance;
            if (server == null) return;
            server.RequestConfirmAssignmentRpc(shipNetId, accept);
        }

        private void RequestCancelAssignment() {
            var server = DockingServer.Instance;
            if (server == null) return;
            ulong shipNetId = GetLocalShipNetworkObjectId();
            if (shipNetId == 0) return;
            server.RequestTakeoffRpc(shipNetId);  // в Phase 3 — отдельный RequestCancelRpc; для MVP reuse
        }

        private ulong GetLocalShipNetworkObjectId() {
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null) return 0;
            if (!localPlayer.IsInShip) return 0;
            return localPlayer.CurrentShip != null ? localPlayer.CurrentShip.NetworkObjectId : 0;
        }

        private static NetworkPlayer FindLocalPlayer() {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++) {
                if (players[i] == null || !players[i].IsOwner) continue;
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                return players[i];
            }
            return null;
        }

        // ====================================================
        // UPDATE LOOP (для прогресс-бара)
        // ====================================================

        private void Update() {
            if (!IsOpen || !_built) return;
            if (_currentStatus == DockingStatus.Assigned && _progressBar != null) {
                float remain = Mathf.Max(0f, _windowExpireTime - Time.time);
                float total = 90f;  // hardcode; см.выше
                _progressBar.value = (remain / total) * 100f;
                _progressBar.title = $"{Mathf.FloorToInt(remain / 60f)}:{Mathf.FloorToInt(remain % 60f):00}";
                if (remain <= 0f) {
                    // Сервер уже отправит Cancelled, но мы дополнительно проверим
                }
            }
        }
    }
}
```

---

## 6. Структура `CommPanelToast.cs`

### 6.1 Назначение

Маленький toast в правом нижнем углу — wrong-pad warning. Fade-out 4 сек.

### 6.2 UXML (`CommPanelToast.uxml`)

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <ui:VisualElement name="root" class="comm-toast-root">
    <ui:VisualElement name="toast" class="comm-toast">
      <ui:Label name="warning-icon" text="⚠" class="comm-toast-icon" />
      <ui:VisualElement name="content" class="comm-toast-content">
        <ui:Label name="title" text="Вы на чужом pad'е" class="comm-toast-title" />
        <ui:Label name="message" text="Перепаркуйтесь на назначенный pad." class="comm-toast-message" />
      </ui:VisualElement>
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

### 6.3 USS (`CommPanelToast.uss`)

```css
.comm-toast-root {
    position: absolute;
    bottom: 40px;
    right: 40px;
    display: none;
}

.comm-toast-root.is-visible {
    display: flex;
}

.comm-toast {
    flex-direction: row;
    align-items: center;
    background-color: rgba(180, 60, 60, 0.92);
    border-width: 2px;
    border-color: rgb(220, 100, 100);
    border-radius: 4px;
    padding: 12px 18px;
    max-width: 320px;
    transition-property: opacity;
    transition-duration: 0.4s;
}

.comm-toast.is-fading {
    opacity: 0;
}

.comm-toast-icon {
    font-size: 24px;
    color: rgb(255, 230, 130);
    margin-right: 12px;
}

.comm-toast-title {
    color: white;
    font-size: 14px;
    -unity-font-style: bold;
}

.comm-toast-message {
    color: rgb(230, 230, 230);
    font-size: 12px;
    margin-top: 2px;
    white-space: normal;
}
```

### 6.4 CS (`CommPanelToast.cs`)

```csharp
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using ProjectC.Docking.Client;

namespace ProjectC.Docking.UI {
    [RequireComponent(typeof(UIDocument))]
    public class CommPanelToast : MonoBehaviour {
        [SerializeField] private VisualTreeAsset toastUxml;
        [SerializeField] private StyleSheet toastUss;

        [SerializeField, Min(0.5f)] private float fadeOutDuration = 4f;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _toast;
        private Label _title;
        private Label _message;
        private bool _built;
        private Coroutine _fadeCoroutine;

        private void Awake() {
            _doc = GetComponent<UIDocument>();
            if (toastUxml == null) toastUxml = Resources.Load<VisualTreeAsset>("UI/CommPanelToast");
            if (toastUss == null) toastUss = Resources.Load<StyleSheet>("UI/CommPanelToast");
        }

        private void OnEnable() { EnsureBuilt(); }
        private void Start() { EnsureBuilt(); }

        private void EnsureBuilt() {
            if (_built) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (toastUxml == null || _doc == null) {
                Debug.LogError("[CommPanelToast] нет UXML или UIDocument", this);
                return;
            }
            _doc.visualTreeAsset = toastUxml;
            _root = _doc.rootVisualElement;
            _root.styleSheets.Add(toastUss);
            _toast = _root.Q<VisualElement>("toast");
            _title = _root.Q<Label>("title");
            _message = _root.Q<Label>("message");
            _built = true;
            SetVisible(false);
        }

        public void ShowWrongPadWarning(string stationId, string padId) {
            if (!_built) return;
            if (_title != null) _title.text = "Вы на чужом pad'е";
            if (_message != null) {
                _message.text = $"Вы на #{padId}, но вам назначен другой. См. CommPanel.";
            }
            SetVisible(true);
            // Перезапустить fade-out таймер
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutAfter(fadeOutDuration));
        }

        public void ShowGeneric(string title, string message, float duration = 4f) {
            if (!_built) return;
            if (_title != null) _title.text = title;
            if (_message != null) _message.text = message;
            SetVisible(true);
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutAfter(duration));
        }

        private void SetVisible(bool v) {
            if (_root == null) return;
            _root.EnableInClassList("is-visible", v);
            if (_toast != null) _toast.RemoveFromClassList("is-fading");
        }

        private IEnumerator FadeOutAfter(float seconds) {
            yield return new WaitForSeconds(seconds);
            if (_toast != null) _toast.AddToClassList("is-fading");
            yield return new WaitForSeconds(0.4f);  // длительность transition
            SetVisible(false);
        }
    }
}
```

---

## 7. Bootstrapping и lifecycle

### 7.1 Где размещаются UI-объекты

| Объект | Сцена | Назначение |
|--------|-------|-----------|
| `[CommPanelWindow]` (UIDocument) | `BootstrapScene` (DontDestroyOnLoad) | Все клиенты имеют свой экземпляр |
| `[CommPanelToast]` (UIDocument) | `BootstrapScene` (DontDestroyOnLoad) | Все клиенты |
| `CommPanelPanelSettings.asset` | `Assets/_Project/UI/Panels/` | PanelSettings с themeUss |
| `CommPanel.uxml` | `Assets/_Project/Resources/UI/` | Resources.Load fallback |
| `CommPanel.uss` | `Assets/_Project/Resources/UI/` | Resources.Load fallback |
| `CommPanelToast.uxml` | `Assets/_Project/Resources/UI/` | то же |
| `CommPanelToast.uss` | `Assets/_Project/Resources/UI/` | то же |

### 7.2 NetworkManagerController.Awake — добавляем bootstrap

```csharp
// В NetworkManagerController.cs, рядом с CreateQuestClientState и т.п.:

private void CreateCommPanelUI() {
    if (CommPanelWindow.Instance != null) return;
    // Загружаем PanelSettings
    var panelSettings = Resources.Load<PanelSettings>("UI/CommPanelPanelSettings");
    // Создаём GameObject
    var go = new GameObject("[CommPanelWindow]");
    DontDestroyOnLoad(go);
    go.AddComponent<UIDocument>().panelSettings = panelSettings;
    go.AddComponent<CommPanelWindow>();
    // Toast
    var toastGo = new GameObject("[CommPanelToast]");
    DontDestroyOnLoad(toastGo);
    toastGo.AddComponent<UIDocument>().panelSettings = panelSettings;
    toastGo.AddComponent<CommPanelToast>();
}

private void CreateDockingClientState() {
    if (DockingClientState.Instance != null) return;
    var go = new GameObject("[DockingClientState]");
    DontDestroyOnLoad(go);
    DockingClientState.Instance = go.AddComponent<DockingClientState>();
}

// Вызываем обе в Awake (или в зависимости от gameMode):
private void Awake() {
    base.Awake();  // или как в существующем коде
    CreateDockingClientState();
    CreateCommPanelUI();
}
```

### 7.3 Подписка на Input (T)

`NetworkPlayer` уже имеет F-key chain. Добавляем T-key handler:

```csharp
// В NetworkPlayer.cs, в Input handler:

private void OnCommPanelPressed() {
    if (!IsOwner) return;
    // Только если рядом станция
    var station = ProjectC.Docking.Network.DockingZoneRegistry.LocalPlayerStation
                ?? ProjectC.Docking.Network.DockingZoneRegistry.LocalPlayerShipStation;
    if (station == null) {
        // hint: «Нет связи с диспетчером»
        return;
    }
    if (CommPanelWindow.Instance == null) return;
    CommPanelWindow.Instance.ToggleOpen();
}
```

Подписка через `PlayerInputReader.OnCommPanelPressed` (новый event, добавляется в `PlayerInputReader`).

---

## 8. Esc для закрытия

В существующем проекте `PlayerInputReader.OnMenuPressed` (или похожий)
обрабатывает Esc. Добавляем:

```csharp
// В PlayerInputReader.cs (если нет — добавить):
public event Action OnCommPanelPressed;

// В InputAction OnPerformed:
private void OnCommPanelActionPerformed(InputAction.CallbackContext ctx) {
    OnCommPanelPressed?.Invoke();
}
```

И в `NetworkPlayer.Update` (или `DialogWindow.OnFSkipTypewriter` аналогии):
```csharp
// В OnMenuPressed handler:
if (CommPanelWindow.Instance != null && CommPanelWindow.Instance.IsOpen) {
    CommPanelWindow.Instance.SetOpen(false);
    return;  // не пробрасываем дальше
}
```

---

## 9. Edge-cases

### 9.1 CommPanel открыт, игрок вышел из OuterCommZone

**Текущее поведение:** панель остаётся открытой. Игрок может нажать
«Запросить посадку» — сервер ответит «STATION_NOT_FOUND».

**Исправление:** `CommPanelWindow.Update()` проверяет
`DockingZoneRegistry.LocalPlayerStation` и если == null — показывает
предупреждение в `_message` (но не закрывает насильно — может быть
лаг в 1-2 секунды).

### 9.2 Сервер назначил pad, игрок закрыл CommPanel, летит, коснулся pad

**Текущее поведение:** `_currentStatus == Assigned` сохранён в `DockingClientState`.
Когда придёт `NotifyTouchedDownRpc`, статус сменится на Docked. Открытие CommPanel
снова покажет Docked state.

**Плюс:** игрок может вернуться в CommPanel в любой момент посмотреть
назначенный pad (помогает если забыл).

### 9.3 Два игрока в одной OuterCommZone

**Текущее поведение:** `OuterCommZone` обновляет `LocalPlayerStation` только
для локального игрока. Каждый клиент видит только свою «nearest station».
CommPanel per-client (на каждом клиенте свой экземпляр). Нет конфликтов.

### 9.4 Игрок не в корабле, но в OuterCommZone

**Текущее поведение:** CommPanel открывается, показывает «Запросить посадку».
При нажатии — `RequestDockingRpc(shipNetId=0)` → сервер отвечает «NO_SUITABLE_PAD»
(нет корабля — некого сажать). UI показывает «без корабля не пущу».

**Альтернатива:** кнопка «Запросить посадку» серая (`enabled=false`) пока
игрок не в корабле. Это лучше UX — нет failed-attempt.

**Решение для MVP:** показать disabled-кнопку с tooltip «Сядьте в корабль».

---

## 10. Связь с DialogWindow (для consistency)

`DialogWindow` (NPC-диалоги) и `CommPanelWindow` (диспетчер) — **разные
системы** с разным UX. Они НЕ переиспользуют компоненты, но следуют
**одним конвенциям**:

| Аспект | DialogWindow | CommPanelWindow |
|--------|--------------|-----------------|
| UIDocument | ✓ | ✓ |
| Singleton Instance | ✓ | ✓ |
| OnEnable subscribe | ✓ | ✓ |
| Start() backup | ✓ | ✓ |
| TrySubscribe/TryUnsubscribe | ✓ | ✓ |
| pickingMode Ignore | ✓ | ✓ |
| styleSheets.Add | ✓ | ✓ |
| Resources.Load fallback | ✓ | ✓ |
| Typewriter | ✓ | ✗ |
| F-skip | ✓ | ✗ (T открывает, Esc закрывает) |
| OptionsContainer | ✓ (button list) | ✓ (но primary/secondary) |
| ClientState singleton | `QuestClientState` | `DockingClientState` |

**`CommPanelToast` ↔ `ShipKeyToast`** — оба UI Toolkit toast с fade-out.
Можно в будущем сделать общий `ToastController` (Phase 3+).

---

## 11. Связь с другими документами

| Документ | Что используем |
|----------|----------------|
| `02_V2_ARCHITECTURE.md` §5 RPCs | серверная логика |
| `02_V2_ARCHITECTURE.md` §7 ClientState | подписки |
| `05_FLOW_AND_INTERACTION.md` | полный flow с T |
| `06_ROADMAP.md` T-DOCK-04, T-DOCK-08 | тикеты на UI |
| `Assets/_Project/Quests/UI/DialogWindow.cs` | паттерн UI Toolkit |
| `Assets/_Project/Ship/Key/ShipKeyToast.cs` | паттерн toast |

---

*Создано: 2026-06-19 | Аналитическая сессия | Без кода.*