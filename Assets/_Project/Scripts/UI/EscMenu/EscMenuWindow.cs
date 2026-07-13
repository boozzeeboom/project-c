// Project C: Input System — Phase 3 (safe refactoring)
// EscMenuWindow — сохраняет Clear+CloneTree паттерн, добавляет стек-навигацию.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ProjectC.UI;

namespace ProjectC.UI.EscMenu
{
    /// <summary>
    /// EscMenu с поддержкой подменю через Stack&lt;VisualElement&gt;.
    /// DefaultExecutionOrder = -150: запасной Esc-handler для submenu,
    /// бежит между UIManager(-200) и CharacterWindow(default 0).
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [RequireComponent(typeof(UIDocument))]
    public class EscMenuWindow : MonoBehaviour
    {
        public static EscMenuWindow Instance { get; private set; }

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset escUxml;
        [SerializeField] private StyleSheet escUss;

        private UIDocument _doc;
        private VisualElement _root;
        private Button _backBtn;
        private Label _titleLabel;
        private ScrollView _contentScroll;
        private bool _built = false;

        /// <summary>Стек экранов: корень меню + подменю.</summary>
        private readonly Stack<VisualElement> _menuStack = new Stack<VisualElement>();
        private VisualElement _currentPanel;

        // ==================== Lifecycle ====================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _doc = GetComponent<UIDocument>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void OnEnable() { EnsureBuilt(); }
        private void Start() { EnsureBuilt(); }

        // ==================== Build (Clear+CloneTree — рабочий паттерн) ====================

        public void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null || _doc.rootVisualElement == null) return;

            if (escUxml == null) escUxml = Resources.Load<VisualTreeAsset>("UI/EscMenuWindow");
            if (escUss  == null) escUss  = Resources.Load<StyleSheet>("UI/EscMenuStyles");
            if (escUxml == null) { Debug.LogError("[EscMenuWindow] UXML not found"); return; }

            _doc.rootVisualElement.Clear();
            if (escUss != null) _doc.rootVisualElement.styleSheets.Add(escUss);
            _root = escUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            // Query UI elements
            _backBtn = _root.Q<Button>("esc-back-btn");
            _titleLabel = _root.Q<Label>("esc-title");
            _contentScroll = _root.Q<ScrollView>("esc-content");

            // Root menu (buttons are in UXML inside esc-buttons)
            var rootPanel = _root.Q<VisualElement>("esc-buttons");
            if (rootPanel != null)
            {
                WireRootButtons(rootPanel);
                _menuStack.Push(rootPanel);
                _currentPanel = rootPanel;
            }

            // Back button handler
            if (_backBtn != null)
                _backBtn.clicked += NavigateBack;

            _built = true;
            SetOpen(false);
            Debug.Log($"[EscMenuWindow] Built (refactored). uss={escUss?.name}");
        }

        // ==================== Root Menu Buttons ====================

        private void WireRootButtons(VisualElement rootPanel)
        {
            // ПРОДОЛЖИТЬ — закрыть меню
            var continueBtn = rootPanel.Q<Button>("esc-continue-btn");
            if (continueBtn != null)
                continueBtn.clicked += Hide;

            // НАСТРОЙКИ — открыть подменю настроек
            var settingsBtn = rootPanel.Q<Button>("esc-settings-btn");
            if (settingsBtn != null)
                settingsBtn.clicked += NavigateToSettingsMenu;

            // ВЫХОД В МЕНЮ
            var exitBtn = rootPanel.Q<Button>("esc-exit-btn");
            if (exitBtn != null)
                exitBtn.clicked += OnExitToMenuClicked;
        }

        // ==================== Public API (КРАСНЫЕ ЛИНИИ — НЕ МЕНЯТЬ СИГНАТУРЫ) ====================

        public void Toggle() { if (IsOpen()) SetOpen(false); else SetOpen(true); }
        public void Show() => SetOpen(true);
        public void Hide() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (!_built) EnsureBuilt();
            if (_root == null) return;
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _root.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
                _root.MarkDirtyRepaint();
                _root.schedule.Execute(() => _root.MarkDirtyRepaint()).StartingIn(50);
                UIManager.EnsureExists().OpenPanel("EscMenu", 100, Hide, gameObject);
                // Возврат на корень при каждом открытии
                NavigateToRoot();
            }
            else
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                { UnityEngine.Cursor.lockState = CursorLockMode.Locked; UnityEngine.Cursor.visible = false; }
            }
        }

        public bool IsOpen() => _built && _root != null && _root.style.display.value == DisplayStyle.Flex;

        // ==================== Submenu Navigation ====================

        /// <summary>Показать новый экран в esc-content, спрятав текущий.</summary>
        public void NavigateTo(VisualElement panel, string title = null)
        {
            if (panel == null) return;

            // Прячем текущий
            if (_currentPanel != null)
                _currentPanel.style.display = DisplayStyle.None;

            // Добавляем новый в ScrollView
            if (panel.parent != _contentScroll)
                _contentScroll.Add(panel);
            panel.style.display = DisplayStyle.Flex;
            panel.style.flexGrow = 1;

            _menuStack.Push(panel);
            _currentPanel = panel;

            if (title != null && _titleLabel != null)
                _titleLabel.text = title;
            UpdateBackButton();
        }

        /// <summary>Вернуться на уровень выше.</summary>
        public void NavigateBack()
        {
            if (_menuStack.Count <= 1) return;

            var old = _menuStack.Pop();
            if (old != null) old.style.display = DisplayStyle.None;

            _currentPanel = _menuStack.Peek();
            if (_currentPanel != null)
                _currentPanel.style.display = DisplayStyle.Flex;

            if (_menuStack.Count == 1 && _titleLabel != null)
                _titleLabel.text = "МЕНЮ";
            UpdateBackButton();
        }

        /// <summary>Сброс на корень меню.</summary>
        public void NavigateToRoot()
        {
            while (_menuStack.Count > 1)
            {
                var old = _menuStack.Pop();
                if (old != null) old.style.display = DisplayStyle.None;
            }

            if (_menuStack.Count > 0)
            {
                _currentPanel = _menuStack.Peek();
                if (_currentPanel != null) _currentPanel.style.display = DisplayStyle.Flex;
            }

            if (_titleLabel != null) _titleLabel.text = "МЕНЮ";
            UpdateBackButton();
        }

        private void UpdateBackButton()
        {
            if (_backBtn == null) return;
            _backBtn.style.display = _menuStack.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ==================== Esc Handling ====================

        /// <summary>UIManager делегирует Esc только если мы в подменю (иначе CloseTopPanel сам всё сделает).</summary>
        public bool IsInSubmenu() => _menuStack.Count > 1;

        // ==================== Settings Submenu ====================

        private void NavigateToSettingsMenu()
        {
            var panel = new VisualElement();
            panel.style.flexDirection = FlexDirection.Column;

            var header = new Label("Настройки");
            header.AddToClassList("esc-section-header");
            panel.Add(header);

            panel.Add(MakeSettingsButton("Управление", OpenKeybindingsSubPage));
            panel.Add(MakeSettingsButton("Графика", NavigateToGraphics));
            panel.Add(MakeSettingsButton("Звук", NavigateToAudio));
            panel.Add(MakeSettingsButton("Геймплей", NavigateToGameplay));

            NavigateTo(panel, "НАСТРОЙКИ");
        }

        private static Button MakeSettingsButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList("esc-btn");
            return btn;
        }

        // ==================== Placeholder Sub-pages (Этапы 3a-3d) ====================

        private void OpenKeybindingsSubPage()
        {
            var p = MakePlaceholder("Управление — Этап 3d");
            NavigateTo(p, "УПРАВЛЕНИЕ");
        }

        private void NavigateToGraphics()
        {
            var p = MakePlaceholder("Графика — Этап 3a");
            NavigateTo(p, "ГРАФИКА");
        }

        private void NavigateToAudio()
        {
            var p = MakePlaceholder("Звук — Этап 3b");
            NavigateTo(p, "ЗВУК");
        }

        private void NavigateToGameplay()
        {
            var p = MakePlaceholder("Геймплей — Этап 3c");
            NavigateTo(p, "ГЕЙМПЛЕЙ");
        }

        private static VisualElement MakePlaceholder(string text)
        {
            var el = new Label(text);
            el.style.color = Color.gray;
            el.style.marginTop = 20;
            el.style.unityTextAlign = TextAnchor.MiddleCenter;
            return el;
        }

        // ==================== Exit to Menu ====================

        private void OnExitToMenuClicked()
        {
            Debug.Log("[EscMenuWindow] Exit to menu (Stage 4 pending)");
            Hide();
        }
    }
}
