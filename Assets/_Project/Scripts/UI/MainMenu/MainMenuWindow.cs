// Project C: Main Menu — replaces NetworkTestCanvas with full-featured main menu.
// UI Toolkit based, same pattern as EscMenuWindow: UIDocument + stack navigation.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization.Settings;
using ProjectC.Core;
using ProjectC.Localization;
using ProjectC.UI.Client;
using ProjectC.UI.EscMenu;

namespace ProjectC.UI.MainMenu
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuWindow : MonoBehaviour
    {
        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset mainUxml;
        [SerializeField] private StyleSheet mainUss;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentWindow;
        private bool _built;

        private VisualElement _rootButtons;
        private VisualElement _ipPanel;
        private TextField _ipField;
        private Button _hostBtn, _connectBtn, _settingsBtn, _quitBtn;
        private Button _ipConnectBtn, _ipBackBtn;
        private Label _titleLabel, _subtitleLabel;
        private CustomDropdown _langDropdown;

        private readonly Stack<VisualElement> _menuStack = new Stack<VisualElement>();
        private VisualElement _currentPanel;

        private void Awake() { _doc = GetComponent<UIDocument>(); }
        private void OnEnable() { EnsureBuilt(); }
        private void Start() { EnsureBuilt(); Show(); }

        public void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null || _doc.rootVisualElement == null) return;

            if (mainUxml == null) mainUxml = Resources.Load<VisualTreeAsset>("UI/MainMenuWindow");
            if (mainUss == null) mainUss = Resources.Load<StyleSheet>("UI/MainMenuStyles");
            if (mainUxml == null) { Debug.LogError("[MainMenuWindow] UXML not found"); return; }

            _doc.rootVisualElement.Clear();
            if (mainUss != null) _doc.rootVisualElement.styleSheets.Add(mainUss);
            var sUss = Resources.Load<StyleSheet>("UI/EscMenuSettingsStyles");
            if (sUss != null) _doc.rootVisualElement.styleSheets.Add(sUss);
            _root = mainUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _doc.rootVisualElement.Add(_root);

            _contentWindow = _root.Q<VisualElement>("main-menu-window");
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

            if (_hostBtn != null) _hostBtn.clicked += OnHostClicked;
            if (_connectBtn != null) _connectBtn.clicked += OnConnectClicked;
            if (_settingsBtn != null) _settingsBtn.clicked += OnSettingsClicked;
            if (_quitBtn != null) _quitBtn.clicked += OnQuitClicked;
            if (_ipConnectBtn != null) _ipConnectBtn.clicked += OnIpConnectClicked;
            if (_ipBackBtn != null) _ipBackBtn.clicked += NavigateToRoot;
            if (_ipField != null) _ipField.value = "127.0.0.1";

            LocalizeAll();
            BuildLanguageSelector();
            BuildLinkButtons();

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
            if (_titleLabel != null) Loc.Bind(_titleLabel, "ui.main_menu.title", _titleLabel.text);
            if (_subtitleLabel != null) Loc.Bind(_subtitleLabel, "ui.main_menu.subtitle", _subtitleLabel.text);
            if (_hostBtn != null) Loc.Bind(_hostBtn, "ui.main_menu.button.host", _hostBtn.text);
            if (_connectBtn != null) Loc.Bind(_connectBtn, "ui.main_menu.button.connect", _connectBtn.text);
            if (_settingsBtn != null) Loc.Bind(_settingsBtn, "ui.main_menu.button.settings", _settingsBtn.text);
            if (_quitBtn != null) Loc.Bind(_quitBtn, "ui.main_menu.button.quit", _quitBtn.text);
            if (_ipConnectBtn != null) Loc.Bind(_ipConnectBtn, "ui.main_menu.button.ip_connect", _ipConnectBtn.text);
            if (_ipBackBtn != null) Loc.Bind(_ipBackBtn, "ui.main_menu.button.back", _ipBackBtn.text);
        }

        private void BuildLanguageSelector()
        {
            var container = _root.Q<VisualElement>("main-lang-selector");
            if (container == null)
            {
                Debug.LogWarning("[MainMenuWindow] main-lang-selector not found in UXML.");
                return;
            }

            _langDropdown = new CustomDropdown();
            var choices = new List<string>();
            foreach (var entry in LocaleSelector.Locales)
                choices.Add(entry.nativeName);

            _langDropdown.SetChoices(choices, LocaleIndexForCode(SettingsManager.Locale));
            _langDropdown.OnSelectionChanged += idx =>
            {
                if (idx < 0 || idx >= LocaleSelector.Locales.Length) return;
                LocaleSelector.SetLocale(LocaleSelector.Locales[idx].code);
            };
            container.Add(_langDropdown);

            Loc.OnLocaleChanged += OnLocaleChangedSync;
            Debug.Log("[MainMenuWindow] Language selector built.");
        }

        private void BuildLinkButtons()
        {
            var container = _root.Q<VisualElement>("main-links");
            if (container == null)
            {
                Debug.LogWarning("[MainMenuWindow] main-links not found in UXML.");
                return;
            }

            var links = new (string label, string url)[]
            {
                ("THEGRAVITY.RU", "https://thegravity.ru"),
                ("PROJECT C", "https://thegravity.ru/project-c/"),
                ("GITHUB", "https://github.com/boozzeeboom/project-c"),
                ("TELEGRAM", "https://t.me/thegravity_ru"),
                ("VK", "https://vk.ru/thegravity_ru"),
            };

            foreach (var (label, url) in links)
            {
                var urlCopy = url;
                var btn = new Button(() => Application.OpenURL(urlCopy));
                btn.text = label;
                btn.tooltip = urlCopy;
                btn.AddToClassList("main-link-btn");
                container.Add(btn);
            }
        }

        private int LocaleIndexForCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return 0;
            for (int i = 0; i < LocaleSelector.Locales.Length; i++)
                if (LocaleSelector.Locales[i].code.Equals(code, StringComparison.OrdinalIgnoreCase))
                    return i;
            return 0;
        }

        private void OnLocaleChangedSync()
        {
            if (_langDropdown == null) return;
            var code = LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.Code
                : SettingsManager.Locale;
            _langDropdown.SetSelectedIndex(LocaleIndexForCode(code), fireEvent: false);
        }

        private void OnDestroy()
        {
            Loc.OnLocaleChanged -= OnLocaleChangedSync;
            if (_langDropdown != null) _langDropdown.Cleanup();
        }

        public void Show() { if (!_built) EnsureBuilt(); if (_root == null) return; _root.style.display = DisplayStyle.Flex; _root.pickingMode = PickingMode.Position; NavigateToRoot(); }
        public void Hide() { if (_root == null) return; _root.style.display = DisplayStyle.None; _root.pickingMode = PickingMode.Ignore; }

        private void NavigateTo(VisualElement panel)
        {
            if (panel == null) return;
            if (_currentPanel != null) _currentPanel.style.display = DisplayStyle.None;
            SetHeaderVisible(false);
            if (panel.parent != _contentWindow && _contentWindow != null) _contentWindow.Add(panel);
            panel.style.display = DisplayStyle.Flex;
            _menuStack.Push(panel);
            _currentPanel = panel;
        }

        private void NavigateToRoot()
        {
            while (_menuStack.Count > 1) { var old = _menuStack.Pop(); if (old != null) old.style.display = DisplayStyle.None; }
            if (_menuStack.Count > 0) { _currentPanel = _menuStack.Peek(); if (_currentPanel != null) _currentPanel.style.display = DisplayStyle.Flex; }
            SetHeaderVisible(true);
        }

        private void SetHeaderVisible(bool v)
        {
            var d = v ? DisplayStyle.Flex : DisplayStyle.None;
            if (_titleLabel != null) _titleLabel.style.display = d;
            if (_subtitleLabel != null) _subtitleLabel.style.display = d;
        }

        private void OnHostClicked() { var n = FindAnyObjectByType<NetworkManagerController>(); if (n != null) { n.StartHost(); Hide(); } else Debug.LogError("[MainMenuWindow] NMC not found"); }
        private void OnConnectClicked() { if (_ipPanel != null) { if (_ipField != null) _ipField.value = "127.0.0.1"; NavigateTo(_ipPanel); } }
        private void OnIpConnectClicked() { var ip = _ipField?.value?.Trim() ?? "127.0.0.1"; if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1"; var n = FindAnyObjectByType<NetworkManagerController>(); if (n != null) { n.ConnectToServer(ip, 7777); Hide(); } else Debug.LogError("[MainMenuWindow] NMC not found"); }
        private void OnSettingsClicked() { NavigateTo(BuildSettingsPanel()); }
        private void OnQuitClicked() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private VisualElement BuildSettingsPanel()
        {
            var panel = new VisualElement();
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignItems = Align.Stretch;
            panel.style.width = 480;
            panel.style.backgroundColor = new Color(0.071f, 0.086f, 0.125f, 0.95f);
            panel.style.borderTopLeftRadius = 8; panel.style.borderTopRightRadius = 8;
            panel.style.borderBottomLeftRadius = 8; panel.style.borderBottomRightRadius = 8;
            panel.style.borderLeftWidth = 2; panel.style.borderRightWidth = 2;
            panel.style.borderTopWidth = 2; panel.style.borderBottomWidth = 2;
            var bc = new Color(0.314f, 0.392f, 0.549f);
            panel.style.borderLeftColor = bc; panel.style.borderRightColor = bc;
            panel.style.borderTopColor = bc; panel.style.borderBottomColor = bc;
            panel.style.paddingTop = 12; panel.style.paddingBottom = 16;
            panel.style.paddingLeft = 16; panel.style.paddingRight = 16;
            panel.style.maxHeight = 460;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 12;

            var backBtn = new Button(NavigateToRoot);
            Loc.Bind(backBtn, "ui.main_menu.button.back", "<- BACK");
            backBtn.AddToClassList("main-menu-btn"); backBtn.AddToClassList("main-menu-btn-back");
            backBtn.style.width = 100;
            headerRow.Add(backBtn);

            var st = new Label();
            Loc.Bind(st, "ui.esc_menu.root_title", "SETTINGS");
            st.style.color = new Color(0.863f, 0.863f, 0.863f);
            st.style.fontSize = 20; st.style.unityFontStyleAndWeight = FontStyle.Bold;
            st.style.unityTextAlign = TextAnchor.MiddleCenter; st.style.flexGrow = 1;
            headerRow.Add(st);
            panel.Add(headerRow);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1; scroll.style.width = Length.Percent(100);
            scroll.Add(GraphicsSettingsSection.Create());
            scroll.Add(AudioSettingsSection.Create());
            scroll.Add(GameplaySettingsSection.Create());
            panel.Add(scroll);
            return panel;
        }
    }
}
