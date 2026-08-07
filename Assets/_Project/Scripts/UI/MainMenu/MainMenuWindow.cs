// Project C: Main Menu — replaces NetworkTestCanvas with full-featured main menu.
// UI Toolkit based, same pattern as EscMenuWindow: UIDocument + stack navigation.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Core;
using ProjectC.Localization;
using ProjectC.UI.EscMenu;

namespace ProjectC.UI.MainMenu
{
    /// <summary>
    /// Main menu shown on BootstrapScene. Buttons: Host (solo), Connect (→ IP screen), Settings, Quit.
    /// Reuses EscMenu settings sections (Graphics, Audio, Gameplay).
    /// All texts localized via Loc.Bind/Loc.Get with ui.main_menu.* keys.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuWindow : MonoBehaviour
    {
        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset mainUxml;
        [SerializeField] private StyleSheet mainUss;

        private UIDocument _doc;
        private VisualElement _root;
        private bool _built;

        private VisualElement _rootButtons;
        private VisualElement _ipPanel;
        private TextField _ipField;
        private Button _hostBtn;
        private Button _connectBtn;
        private Button _settingsBtn;
        private Button _quitBtn;
        private Button _ipConnectBtn;
        private Button _ipBackBtn;
        private Label _titleLabel;
        private Label _subtitleLabel;

        private readonly Stack<VisualElement> _menuStack = new Stack<VisualElement>();
        private VisualElement _currentPanel;

        // ===== Lifecycle =====

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            EnsureBuilt();
        }

        private void Start()
        {
            EnsureBuilt();
            Show();
        }

        // ===== Build =====

        public void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null || _doc.rootVisualElement == null) return;

            if (mainUxml == null)
                mainUxml = Resources.Load<VisualTreeAsset>("UI/MainMenuWindow");
            if (mainUss == null)
                mainUss = Resources.Load<StyleSheet>("UI/MainMenuStyles");
            if (mainUxml == null)
            {
                Debug.LogError("[MainMenuWindow] UXML not found");
                return;
            }

            _doc.rootVisualElement.Clear();
            if (mainUss != null)
                _doc.rootVisualElement.styleSheets.Add(mainUss);
            _root = mainUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _doc.rootVisualElement.Add(_root);

            // Query UI elements
            _titleLabel = _root.Q<Label>("main-menu-title");
            _subtitleLabel = _root.Q<Label>("main-menu-subtitle");
            _rootButtons = _root.Q<VisualElement>("main-menu-buttons");
            _ipPanel = _root.Q<VisualElement>("main-ip-panel");
            _ipField = _root.Q<TextField>("main-ip-field");

            _hostBtn = _root.Q<Button>("main-host-btn");
            _connectBtn = _root.Q<Button>("main-connect-btn");
            _settingsBtn = _root.Q<Button>("main-settings-btn");
            _quitBtn = _root.Q<Button>("main-quit-btn");
            _ipConnectBtn = _root.Q<Button>("main-ip-connect-btn");
            _ipBackBtn = _root.Q<Button>("main-ip-back-btn");

            // Wire buttons
            if (_hostBtn != null) _hostBtn.clicked += OnHostClicked;
            if (_connectBtn != null) _connectBtn.clicked += OnConnectClicked;
            if (_settingsBtn != null) _settingsBtn.clicked += OnSettingsClicked;
            if (_quitBtn != null) _quitBtn.clicked += OnQuitClicked;
            if (_ipConnectBtn != null) _ipConnectBtn.clicked += OnIpConnectClicked;
            if (_ipBackBtn != null) _ipBackBtn.clicked += NavigateToRoot;

            // Set IP field placeholder
            if (_ipField != null)
            {
                _ipField.value = "127.0.0.1";
            }

            // Localize all static texts
            LocalizeAll();

            // Initialize stack with root panel
            if (_rootButtons != null)
            {
                _rootButtons.style.display = DisplayStyle.Flex;
                _menuStack.Push(_rootButtons);
                _currentPanel = _rootButtons;
            }

            _built = true;
            Debug.Log("[MainMenuWindow] Built.");
        }

        private void LocalizeAll()
        {
            if (_titleLabel != null)
                Loc.Bind(_titleLabel, "ui.main_menu.title", _titleLabel.text);
            if (_subtitleLabel != null)
                Loc.Bind(_subtitleLabel, "ui.main_menu.subtitle", _subtitleLabel.text);
            if (_hostBtn != null)
                Loc.Bind(_hostBtn, "ui.main_menu.button.host", _hostBtn.text);
            if (_connectBtn != null)
                Loc.Bind(_connectBtn, "ui.main_menu.button.connect", _connectBtn.text);
            if (_settingsBtn != null)
                Loc.Bind(_settingsBtn, "ui.main_menu.button.settings", _settingsBtn.text);
            if (_quitBtn != null)
                Loc.Bind(_quitBtn, "ui.main_menu.button.quit", _quitBtn.text);
            if (_ipConnectBtn != null)
                Loc.Bind(_ipConnectBtn, "ui.main_menu.button.ip_connect", _ipConnectBtn.text);
            if (_ipBackBtn != null)
                Loc.Bind(_ipBackBtn, "ui.main_menu.button.back", _ipBackBtn.text);
        }

        // ===== Show / Hide =====

        public void Show()
        {
            if (!_built) EnsureBuilt();
            if (_root == null) return;
            _root.style.display = DisplayStyle.Flex;
            _root.pickingMode = PickingMode.Position;
            NavigateToRoot();
        }

        public void Hide()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.None;
            _root.pickingMode = PickingMode.Ignore;
        }

        // ===== Stack Navigation =====

        private void NavigateTo(VisualElement panel)
        {
            if (panel == null) return;

            if (_currentPanel != null)
                _currentPanel.style.display = DisplayStyle.None;

            panel.style.display = DisplayStyle.Flex;
            _menuStack.Push(panel);
            _currentPanel = panel;
        }

        private void NavigateToRoot()
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
        }

        // ===== Button Handlers =====

        private void OnHostClicked()
        {
            var nmc = FindAnyObjectByType<NetworkManagerController>();
            if (nmc != null)
            {
                nmc.StartHost();
                Hide();
            }
            else
            {
                Debug.LogError("[MainMenuWindow] NetworkManagerController not found!");
            }
        }

        private void OnConnectClicked()
        {
            if (_ipPanel != null)
            {
                if (_ipField != null) _ipField.value = "127.0.0.1";
                NavigateTo(_ipPanel);
            }
        }

        private void OnIpConnectClicked()
        {
            var ip = _ipField != null ? _ipField.value.Trim() : "127.0.0.1";
            if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

            Debug.Log($"[MainMenuWindow] Connecting to {ip}:7777...");
            var nmc = FindAnyObjectByType<NetworkManagerController>();
            if (nmc != null)
            {
                nmc.ConnectToServer(ip, 7777);
                Hide();
            }
            else
            {
                Debug.LogError("[MainMenuWindow] NetworkManagerController not found!");
            }
        }

        private void OnSettingsClicked()
        {
            var settingsPanel = BuildSettingsPanel();
            NavigateTo(settingsPanel);
        }

        private void OnQuitClicked()
        {
            Debug.Log("[MainMenuWindow] Quit requested.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ===== Settings Panel (reuses EscMenu sections) =====

        private VisualElement BuildSettingsPanel()
        {
            var panel = new VisualElement();
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignItems = Align.Stretch;
            panel.style.width = Length.Percent(100);
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;
            panel.style.paddingLeft = 8;
            panel.style.paddingRight = 8;

            // Back button at top
            var backBtn = new Button(NavigateToRoot);
            Loc.Bind(backBtn, "ui.main_menu.button.back", "← НАЗАД");
            backBtn.AddToClassList("main-menu-btn");
            backBtn.AddToClassList("main-menu-btn-back");
            backBtn.style.width = 120;
            backBtn.style.marginBottom = 12;
            panel.Add(backBtn);

            // ScrollView for settings content
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.width = Length.Percent(100);

            // Add EscMenu settings sections
            scroll.Add(GraphicsSettingsSection.Create());
            scroll.Add(AudioSettingsSection.Create());
            scroll.Add(GameplaySettingsSection.Create());

            panel.Add(scroll);
            return panel;
        }
    }
}
