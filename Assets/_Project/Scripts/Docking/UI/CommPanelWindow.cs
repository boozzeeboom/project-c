// T-DOCK-07: CommPanelWindow — UI Toolkit окно диспетчерской связи.
// UIDocument singleton (как DialogWindow). Подписывается на DockingClientState (T-DOCK-03)
// и отображает двусторонний диалог с диспетчером (Q7).
//
// Q7 (принято 2026-06-19): AwaitingConfirmation state с кнопками [Хорошо]/[Отбой].
// Q9 (принято 2026-06-19): F закрывает CommPanel (стандартное boarding).
// Q10 (принято 2026-06-19): T игнорируется вне кресла (check в T-key handler, не здесь).
//
// Паттерн: см. Assets/_Project/Quests/UI/DialogWindow.cs.

using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Docking.Client;  // DockingClientState
using ProjectC.Docking.Dto;     // DockingAssignmentDto, DockingStatusDto, DockingStatus
using ProjectC.Docking.Network; // DockingServer, DockingZoneRegistry
using ProjectC.Network;          // NetworkPlayerSpawner
using ProjectC.Player;          // NetworkPlayer, NetworkPlayerSpawner

namespace ProjectC.Docking.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class CommPanelWindow : MonoBehaviour
    {
        public static CommPanelWindow Instance { get; private set; }

        [Header("UI Assets (можно Resources fallback)")]
        [SerializeField] private VisualTreeAsset commPanelUxml;
        [SerializeField] private StyleSheet commPanelUss;

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
        private float _windowExpireTime;

        // Q7: AwaitingConfirmation — отдельное состояние UI (между request и confirm)
        private DockingAssignmentDto? _awaitingConfirmation;

        private bool _built = false;
        private bool _subscribed = false;

        // ====================================================
        // LIFECYCLE (как DialogWindow.cs / CharacterWindow.cs)
        // ====================================================

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            // Resources fallback
            if (commPanelUxml == null)
                commPanelUxml = Resources.Load<VisualTreeAsset>("UI/CommPanel");
            if (commPanelUss == null)
                commPanelUss = Resources.Load<StyleSheet>("UI/CommPanel");
        }

        private void OnEnable()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                Debug.LogError("[CommPanelWindow] нет UIDocument на GameObject");
                return;
            }
            EnsureBuilt();
            TrySubscribe();
        }

        private void Start()
        {
            // FIX: после всех OnEnable (включая UIDocument, который мог подвесить
            // свой UXML-auto-load поверх нашего дерева) — перепроверяем состояние.
            if (!_built || !IsLayoutValid())
            {
                Debug.LogWarning("[CommPanelWindow] Start(): layout invalid, rebuilding");
                EnsureBuilt();
            }
        }

        private void OnDisable()
        {
            TryUnsubscribe();
        }

        private void OnDestroy()
        {
            TryUnsubscribe();
            if (Instance == this) Instance = null;
        }

        private bool IsLayoutValid()
        {
            // Не полагаемся на resolvedStyle.width — на первом кадре после
            // Clear()+CloneTree() он бывает NaN/0 (USS layout не успел посчитаться).
            // Достаточно проверить, что дерево существует и panel на месте.
            return _built && _root != null && _panel != null;
        }

        private void EnsureBuilt()
        {
            if (_doc.rootVisualElement == null) return;
            if (commPanelUxml == null)
                commPanelUxml = Resources.Load<VisualTreeAsset>("UI/CommPanel");
            if (commPanelUss == null)
                commPanelUss = Resources.Load<StyleSheet>("UI/CommPanel");
            if (commPanelUxml == null)
            {
                Debug.LogError("[CommPanelWindow] UXML не найден ни в Inspector, ни в Resources/UI/");
                return;
            }

            // КРИТИЧНО: очищаем и подвешиваем стили КАЖДЫЙ раз (после UIDocument.OnEnable
            // может подвесить свой UXML-auto-load поверх нашего, и USS слетит).
            _doc.rootVisualElement.Clear();
            if (commPanelUss != null)
                _doc.rootVisualElement.styleSheets.Add(commPanelUss);

            _root = commPanelUxml.CloneTree();
            // CloneTree возвращает TemplateContainer с position:relative 0×0. Растягиваем на
            // весь rootVE — иначе .comm-panel-root (position:absolute) уезжает в (-W/2,0).
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            // pickingMode=Ignore на root — клики "снаружи" диалога не пробрасываются в game
            // во время модального окна. Кнопки внутри (.comm-panel-button-*) ловят mouse сами.
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            _panel = _root.Q<VisualElement>("panel");
            _header = _root.Q<Label>("header");
            _message = _root.Q<Label>("dispatcher-message");
            _progressBar = _root.Q<ProgressBar>("landing-window-bar");
            _primaryButton = _root.Q<Button>("primary-action-button");
            _secondaryButton = _root.Q<Button>("secondary-action-button");

            if (_primaryButton != null) _primaryButton.clicked += OnPrimaryClicked;
            if (_secondaryButton != null) _secondaryButton.clicked += OnSecondaryClicked;

            _built = true;
            // Изначально скрыто — Show()/SetOpen(true) переключит на Flex
            if (_root != null) _root.style.display = DisplayStyle.None;

            if (Debug.isDebugBuild)
                Debug.Log($"[CommPanelWindow] Built: rootVE.children={_doc.rootVisualElement.childCount}, styleSheets={_doc.rootVisualElement.styleSheets.count}");
        }

        // ====================================================
        // SUBSCRIPTIONS (как DialogWindow)
        // ====================================================

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (DockingClientState.Instance == null) return;

            DockingClientState.Instance.OnAwaitingConfirmation += HandleAwaitingConfirmation;
            DockingClientState.Instance.OnAssignmentFailed += HandleAssignmentFailed;
            DockingClientState.Instance.OnStatusReceived += HandleStatusReceived;
            DockingClientState.Instance.OnTakeoffApproved += HandleTakeoffApproved;
            DockingClientState.Instance.OnTouchedDown += HandleTouchedDown;

            _subscribed = true;
        }

        private void TryUnsubscribe()
        {
            if (!_subscribed) return;
            if (DockingClientState.Instance != null)
            {
                DockingClientState.Instance.OnAwaitingConfirmation -= HandleAwaitingConfirmation;
                DockingClientState.Instance.OnAssignmentFailed -= HandleAssignmentFailed;
                DockingClientState.Instance.OnStatusReceived -= HandleStatusReceived;
                DockingClientState.Instance.OnTakeoffApproved -= HandleTakeoffApproved;
                DockingClientState.Instance.OnTouchedDown -= HandleTouchedDown;
            }
            _subscribed = false;
        }

        // ====================================================
        // OPEN/CLOSE
        // ====================================================

        public void SetOpen(bool open)
        {
            if (!_built) EnsureBuilt();
            if (!_built) return;
            IsOpen = open;
            if (_root != null)
            {
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                _root.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            }
            if (open)
            {
                UpdateUI();
                // Inline fallback styles для frame-1 (USS может не успеть)
                if (_panel != null) ApplyInlineFallbackStyles(_panel);
            }

            // Cursor — flight-режим держит курсор залоченным. При открытом UI отпускаем.
            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                // Frame-1 repaint fix: USS не успел примениться → принудительный repaint
                _doc?.rootVisualElement?.MarkDirtyRepaint();
                if (_doc?.rootVisualElement != null)
                {
                    _doc.rootVisualElement.schedule.Execute(() => _doc.rootVisualElement.MarkDirtyRepaint()).StartingIn(50);
                }
            }
            else
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        public void ToggleOpen()
        {
            SetOpen(!IsOpen);
        }

        // ====================================================
        // STATE HANDLERS
        // ====================================================

        private void HandleAwaitingConfirmation(DockingAssignmentDto assignment, bool isSuccess)
        {
            if (!isSuccess) return;
            _awaitingConfirmation = assignment;
            _currentStationId = assignment.stationId;
            _currentPadId = assignment.padId;
            _windowExpireTime = Time.time + assignment.landingWindowSeconds;
            UpdateUI();
        }

        private void HandleAssignmentFailed(DockingAssignmentDto assignment)
        {
            if (_message == null) return;
            string msg = assignment.failReason switch
            {
                "NO_SUITABLE_PAD" => "Диспетчер: «Свободных pad'ов нет, попробуйте позже».",
                "RATE_LIMITED"    => "Диспетчер: «Слишком частые запросы, подождите».",
                "STATION_FULL"    => "Диспетчер: «Станция переполнена, попробуйте позже».",
                "STATION_NOT_FOUND" => "Диспетчер: «Связь потеряна, повторите».",
                "SHIP_NOT_FOUND"  => "Диспетчер: «Корабль не найден, подойдите ближе».",
                "NOT_YOUR_SHIP"   => "Диспетчер: «Это не ваш корабль».",
                _                 => $"Диспетчер: «Ошибка: {assignment.failReason}»."
            };
            _message.text = msg;
            _currentStatus = DockingStatus.Idle;
            UpdateUI();
        }

        private void HandleStatusReceived(DockingStatusDto status)
        {
            _currentStatus = status.status;
            _currentStationId = status.stationId;
            _currentPadId = status.padId;

            if (status.status == DockingStatus.Assigned)
            {
                _awaitingConfirmation = null;
            }
            else if (status.status == DockingStatus.Cancelled)
            {
                _awaitingConfirmation = null;
            }
            UpdateUI();
        }

        private void HandleTouchedDown(DockingStatusDto status)
        {
            // Зарезервировано для визуальных эффектов (Phase 3)
            UpdateUI();
        }

        private void HandleTakeoffApproved(ulong shipNetId)
        {
            _currentStatus = DockingStatus.Idle;
            _currentStationId = null;
            _currentPadId = null;
            _awaitingConfirmation = null;
            UpdateUI();
        }

        // ====================================================
        // UI UPDATE
        // ====================================================

        private void UpdateUI()
        {
            if (!_built) return;

            // Q7: AwaitingConfirmation — кнопки [Хорошо]/[Отбой]
            if (_awaitingConfirmation.HasValue)
            {
                if (_header != null)
                {
                    string stName = "Диспетчерская";
                    var st = DockingZoneRegistry.GetStation(_awaitingConfirmation.Value.stationId);
                    if (st != null) stName = $"{st.DisplayName} — Диспетчерская";
                    _header.text = stName;
                }
                if (_message != null)
                {
                    var a = _awaitingConfirmation.Value;
                    _message.text = $"Диспетчер: «{a.voiceLine} " +
                                    $"Подход: высота {(int)a.approachAltitude}, " +
                                    $"курс {(int)a.approachHeading}. " +
                                    $"Окно: {(int)a.landingWindowSeconds} сек. Подтверждаете?»";
                }
                if (_progressBar != null) _progressBar.RemoveFromClassList("is-active");
                if (_primaryButton != null)
                {
                    _primaryButton.text = "Хорошо";
                    _primaryButton.style.display = DisplayStyle.Flex;
                }
                if (_secondaryButton != null)
                {
                    _secondaryButton.text = "Отбой";
                    _secondaryButton.style.display = DisplayStyle.Flex;
                }
                return;
            }

            // Header
            if (_header != null)
            {
                string stationName = "Диспетчерская";
                if (!string.IsNullOrEmpty(_currentStationId))
                {
                    var st = DockingZoneRegistry.GetStation(_currentStationId);
                    if (st != null) stationName = $"{st.DisplayName} — Диспетчерская";
                }
                else
                {
                    var nearest = DockingZoneRegistry.LocalPlayerStation
                                  ?? DockingZoneRegistry.LocalPlayerShipStation;
                    if (nearest != null) stationName = $"{nearest.DisplayName} — Диспетчерская";
                }
                _header.text = stationName;
            }

            // Message
            if (_message != null)
                _message.text = GetMessageForStatus();

            // Progress bar
            if (_progressBar != null)
            {
                bool active = _currentStatus == DockingStatus.Assigned;
                _progressBar.EnableInClassList("is-active", active);
                if (active)
                {
                    float remain = Mathf.Max(0f, _windowExpireTime - Time.time);
                    float total = Mathf.Max(1f, _windowExpireTime - (_windowExpireTime - 90f));
                    _progressBar.value = (remain / total) * 100f;
                    _progressBar.title = $"{(int)remain / 60}:{(int)remain % 60:D2}";
                }
            }

            // Buttons
            ConfigureButtons();
        }

        private string GetMessageForStatus()
        {
            if (_currentStatus == DockingStatus.Idle)
                return "Диспетчер: «На связи, жду ваших распоряжений».";
            if (_currentStatus == DockingStatus.Assigned)
                return $"Диспетчер: «Борт, добро. Следуйте к pad #{(string.IsNullOrEmpty(_currentPadId) ? "?" : _currentPadId)}».";
            if (_currentStatus == DockingStatus.Docked)
                return "Диспетчер: «Стыковка зафиксирована. Двигатели заблокированы. Удачной торговли».";
            if (_currentStatus == DockingStatus.WrongPad)
                return $"Диспетчер: «Борт, вы на чужом pad'е (#{_currentPadId}). Перепаркуйтесь».";
            if (_currentStatus == DockingStatus.Cancelled)
                return "Диспетчер: «Окно посадки истекло. Повторите запрос».";
            return "";
        }

        private void ConfigureButtons()
        {
            if (_primaryButton == null || _secondaryButton == null) return;

            if (_currentStatus == DockingStatus.Idle || _currentStatus == DockingStatus.Cancelled)
            {
                _primaryButton.text = "Запросить посадку";
                _primaryButton.style.display = DisplayStyle.Flex;
                _secondaryButton.text = "Отмена";
                _secondaryButton.style.display = DisplayStyle.Flex;
            }
            else if (_currentStatus == DockingStatus.Assigned)
            {
                _primaryButton.style.display = DisplayStyle.None;
                _secondaryButton.text = "Отменить запрос";
                _secondaryButton.style.display = DisplayStyle.Flex;
            }
            else if (_currentStatus == DockingStatus.Docked)
            {
                _primaryButton.text = "F — Отстыковка";
                _primaryButton.style.display = DisplayStyle.Flex;
                _secondaryButton.style.display = DisplayStyle.None;
            }
            else if (_currentStatus == DockingStatus.WrongPad)
            {
                _primaryButton.text = "Перепарковаться";
                _primaryButton.style.display = DisplayStyle.Flex;
                _secondaryButton.text = "Закрыть";
                _secondaryButton.style.display = DisplayStyle.Flex;
            }
        }

        // ====================================================
        // BUTTON HANDLERS
        // ====================================================

        private void OnPrimaryClicked()
        {
            // Q7: AwaitingConfirmation → [Хорошо]
            if (_awaitingConfirmation.HasValue)
            {
                ConfirmAssignment(true);
                return;
            }

            if (_currentStatus == DockingStatus.Idle || _currentStatus == DockingStatus.Cancelled)
            {
                RequestDocking();
            }
            else if (_currentStatus == DockingStatus.Docked)
            {
                SetOpen(false);
                // F-handler (NetworkPlayer) на уровне выше сделает RequestTakeoffRpc
            }
            else if (_currentStatus == DockingStatus.WrongPad)
            {
                RequestDocking();
            }
        }

        private void OnSecondaryClicked()
        {
            // Q7: AwaitingConfirmation → [Отбой]
            if (_awaitingConfirmation.HasValue)
            {
                ConfirmAssignment(false);
                return;
            }

            if (_currentStatus == DockingStatus.Idle
                || _currentStatus == DockingStatus.WrongPad
                || _currentStatus == DockingStatus.Cancelled
                || _currentStatus == DockingStatus.Docked)
            {
                SetOpen(false);
            }
            else if (_currentStatus == DockingStatus.Assigned)
            {
                CancelAssignment();
            }
        }

        private void RequestDocking()
        {
            var server = DockingServer.Instance;
            if (server == null || !server.IsSpawned) return;

            var station = DockingZoneRegistry.LocalPlayerStation
                          ?? DockingZoneRegistry.LocalPlayerShipStation;
            if (station == null) return;

            ulong shipNetId = GetLocalShipNetworkObjectId();
            if (shipNetId == 0) return;

            server.RequestDockingRpc(station.StationId, shipNetId);
        }

        private void ConfirmAssignment(bool accept)
        {
            ulong shipNetId = GetLocalShipNetworkObjectId();
            if (shipNetId == 0) return;

            var server = DockingServer.Instance;
            if (server == null || !server.IsSpawned) return;

            server.RequestConfirmAssignmentRpc(shipNetId, accept);
        }

        private void CancelAssignment()
        {
            ulong shipNetId = GetLocalShipNetworkObjectId();
            if (shipNetId == 0) return;

            var server = DockingServer.Instance;
            if (server == null || !server.IsSpawned) return;

            server.RequestTakeoffRpc(shipNetId);
        }

        private ulong GetLocalShipNetworkObjectId()
        {
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null) return 0;
            if (!localPlayer.IsInShip) return 0;
            return localPlayer.CurrentShip != null ? localPlayer.CurrentShip.NetworkObjectId : 0;
        }

        private static NetworkPlayer FindLocalPlayer()
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].IsOwner) continue;
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                return players[i];
            }
            return null;
        }

        // ====================================================
        // UPDATE LOOP (прогресс-бар)
        // ====================================================

        private void Update()
        {
            if (!IsOpen || !_built) return;

            // AwaitingConfirmation — если время подтверждения истекло
            if (_awaitingConfirmation.HasValue && Time.time > _windowExpireTime)
            {
                // Сервер сам отправит Cancelled, но UI должен отреагировать
                _awaitingConfirmation = null;
                _currentStatus = DockingStatus.Cancelled;
                UpdateUI();
                return;
            }

            // Assigned — прогресс-бар
            if (_currentStatus == DockingStatus.Assigned && _progressBar != null)
            {
                float remain = Mathf.Max(0f, _windowExpireTime - Time.time);
                float total = Mathf.Max(1f, _windowExpireTime - (_windowExpireTime - 90f));
                _progressBar.value = (remain / total) * 100f;
                _progressBar.title = $"{(int)remain / 60}:{(int)remain % 60:D2}";

                if (remain <= 0f)
                {
                    // Сервер пришлёт Cancelled, но обновим для отзывчивости
                    _currentStatus = DockingStatus.Cancelled;
                    UpdateUI();
                }
            }
        }

        // ====================================================
        // Helpers
        // ====================================================

        private static void ApplyInlineFallbackStyles(VisualElement main)
        {
            // FIX: на 1-м кадре resolvedStyle=initial (USS не успел примениться) — задаём
            // только позиционирование и размеры inline. Всё остальное (фон, рамка, шрифт,
            // padding, цвет) в CommPanel.uss с !important, который перебивает
            // UnityDefaultRuntimeTheme. Дублировать эти свойства inline больше не нужно.
            main.style.position = Position.Absolute;
            main.style.top    = new Length(4,  LengthUnit.Percent);
            main.style.left   = new Length(50, LengthUnit.Percent);
            main.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), 0));
            main.style.width      = 560;
            main.style.maxWidth   = new Length(90, LengthUnit.Percent);
            main.style.maxHeight  = new Length(92, LengthUnit.Percent);
        }
    }
}