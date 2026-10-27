// ProjectC main menu — the project's own blocks, ported from the mod's src/project_panels.js.
//
// Step 8 of docs/UI/MainMenu/02_smoke_menu_port.md. The smoke menu (steps 3-7) is
// the mod's invention; these six blocks are what the prototype was missing and what
// MainMenuWindow.cs already had: the IP screen, the ten admin resets, the remote
// changelog, the five external links, the nine locales, the first-launch disclaimer.
//
// The mod says it itself: «they do not use the smoke assembly (ui.js owns that); they
// are added next to it and stay readable». So there is no --a here, no part list and
// no gust — the layer is a plain sibling of .pc-ui, appended last, and PCMM_Menu.uss
// already carries every pc-proj-* rule (step 2 ported them).
//
// Four things the DOM can do and UI Toolkit cannot, and what stands in for them:
//   * fetch / AbortController -> UnityWebRequest, its completion arrives through
//     AsyncOperation.completed and the 8 s abort becomes a scheduled Abort();
//   * window.open              -> Application.OpenURL;
//   * localStorage             -> PlayerPrefs, on the key MainMenuWindow already uses;
//   * <input> / <select>       -> TextField / DropdownField, which are the only real
//     text entry and only native popup the runtime panel offers. They are theme-drawn,
//     so their inner elements are flattened inline (see FlattenControl) and the
//     .pc-proj-input / .pc-proj-select frame stays the only visible edge.
//
// Four application seams — IpConnectHandler, ResetHandler, LocaleHandler and
// UrlOpenHandler — default to the real project APIs (NetworkManagerController,
// PersistenceDebugTools, LocaleSelector, Application.OpenURL). They exist so a
// verification harness can press every button without deleting the player's saves,
// dialling a server or opening a browser; step 9 may override them from the prefab.
using System;
using System.Collections.Generic;
using System.Globalization;
using ProjectC.Core;
using ProjectC.Localization;
using ProjectC.UI.MainMenu;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    public sealed partial class ProjectCMainMenuWindow
    {
        // project_panels.js CHANGELOG_URL
        public const string ChangelogUrl =
            "https://raw.githubusercontent.com/boozzeeboom/project-c/main/docs/changelogs.md";

        // project_panels.js DISCLAIMER_KEY is projectc.mainMenu.disclaimerAccepted;
        // this is the port plan's key, and it is the one MainMenuWindow already wrote,
        // so the old menu and this one agree on whether the disclaimer was accepted.
        public const string DisclaimerPrefsKey = "ProjectC.MainMenu.FirstLaunchDisclaimerAccepted";

        // project_panels.js: `Подключение к ${ip}:7777`
        public const ushort DefaultPort = 7777;

        // project_panels.js: setTimeout(() => abort.abort(), 8000)
        private const long ChangelogAbortMs = 8000L;

        // project_panels.js DEBUG_BUTTONS — ids, labels, and the one dangerous entry
        private static readonly (string Id, string Label, bool Danger)[] DebugButtons =
        {
            ("all", "[Delete All Saves]", true),
            ("position", "[Delete Character Position Saves]", false),
            ("inventory", "[Delete Character Inventory]", false),
            ("progression", "[Delete Character Progression]", false),
            ("customisation", "[Delete Character Customisation]", false),
            ("quests", "[Delete Quests]", false),
            ("skills", "[Delete Skill Slot Bindings]", false),
            ("keys", "[Delete Key Instance Saves]", false),
            ("time", "[Delete World Time]", false),
            ("trade", "[Delete Trade / Cargo Saves]", false),
        };

        // project_panels.js LINKS
        private static readonly (string Label, string Url)[] Links =
        {
            ("THEGRAVITY.RU", "https://thegravity.ru"),
            ("PROJECT C", "https://thegravity.ru/project-c/"),
            ("GITHUB", "https://github.com/boozzeeboom/project-c"),
            ("TELEGRAM", "https://t.me/thegravity_ru"),
            ("VK", "https://vk.ru/thegravity_ru"),
        };

        // project_panels.js /^[✅➡🚢⚔🎧🖥🤝🌐🏆]/u, as code points
        private static readonly int[] MilestoneCodePoints =
        {
            0x2705, 0x27A1, 0x1F6A2, 0x2694, 0x1F3A7, 0x1F5A5, 0x1F91D, 0x1F310, 0x1F3C6,
        };

        // ------------------------------------------------------------- state

        public bool IsIpOpen { get; private set; }
        public string PanelStatus { get; private set; } = string.Empty;
        public string Locale { get; private set; } = "ru";
        public string ChangelogState { get; private set; } = "idle";
        public bool DisclaimerOpen { get; private set; }

        // ------------------------------------------------------- exposed nodes

        public VisualElement ProjectLayer { get; private set; }
        public VisualElement AdminPanel { get; private set; }
        public VisualElement ChangelogPanel { get; private set; }
        public VisualElement LangHost { get; private set; }
        public VisualElement LinksPanel { get; private set; }
        public VisualElement IpSheet { get; private set; }
        public VisualElement Disclaimer { get; private set; }

        // -------------------------------------------------------------- seams

        /// <summary>«ПОДКЛЮЧИТЬСЯ» → ip, port. Default: NetworkManagerController.ConnectToServer.</summary>
        public Action<string, ushort> IpConnectHandler { get; set; } = DefaultIpConnect;

        /// <summary>Admin reset id → the line to show. Default: PersistenceDebugTools.</summary>
        public Func<string, string> ResetHandler { get; set; } = DefaultDebugReset;

        /// <summary>Language picker → locale code. Default: LocaleSelector.SetLocale.</summary>
        public Action<string> LocaleHandler { get; set; } = LocaleSelector.SetLocale;

        /// <summary>External link → url. Default: Application.OpenURL.</summary>
        public Action<string> UrlOpenHandler { get; set; } = DefaultUrlOpen;

        /// <summary>project_panels.js fire(action, payload) — ip-connect / debug-reset / locale.</summary>
        public Action<string, string> OnProjectAction { get; set; }

        // ------------------------------------------------------------ fields

        private TextField _ipField;
        private DropdownField _langSelect;
        private List<string> _langNames;
        private Label _adminStatus;
        private Label _changelogStatus;
        private ScrollView _changelogBody;

        private UnityWebRequest _changelogRequest;
        private IVisualElementScheduledItem _changelogAbort;
        private bool _panelsDisposed;

        // ------------------------------------------------------------- build

        /// <summary>
        /// Builds the six blocks and appends them to Root, after .pc-ui, so the layer
        /// paints over the smoke menu exactly as it paints over the mod's <c>root</c>.
        /// Called from Start(), i.e. after Mount().
        /// </summary>
        private void BuildProjectPanels()
        {
            if (ProjectLayer != null || Root == null) return;

            _panelsDisposed = false;

            ProjectLayer = El("pc-proj-layer");
            ProjectLayer.name = "pc-proj-layer";
            // CSS .pc-proj-layer { pointer-events: none } with `> * { auto }`, so the
            // empty layer never eats a click meant for the menu underneath it.
            ProjectLayer.pickingMode = PickingMode.Ignore;

            AdminPanel = BuildAdminPanel();
            ChangelogPanel = BuildChangelogPanel();
            LangHost = BuildLangPanel();
            LinksPanel = BuildLinksPanel();
            IpSheet = BuildIpSheet();
            Disclaimer = BuildDisclaimer();

            ProjectLayer.Add(AdminPanel);
            ProjectLayer.Add(ChangelogPanel);
            ProjectLayer.Add(LangHost);
            ProjectLayer.Add(LinksPanel);
            ProjectLayer.Add(IpSheet);
            ProjectLayer.Add(Disclaimer);

            Root.Add(ProjectLayer);

            // CSS @media (max-width: 900px) { .pc-proj-changelog { display: none } }
            ProjectLayer.RegisterCallback<GeometryChangedEvent>(OnProjectLayerGeometry);

            // the menu's «ПОДКЛЮЧИТЬСЯ К СЕРВЕРУ» opens this sheet; step 9 may replace it
            if (OnConnectRequested == null) OnConnectRequested = OpenIp;

            if (PlayerPrefs.GetInt(DisclaimerPrefsKey, 0) == 1)
            {
                Disclaimer.style.display = DisplayStyle.None;
                DisclaimerOpen = false;
            }
            else
            {
                Disclaimer.style.display = DisplayStyle.Flex;
                DisclaimerOpen = true;
            }

            RefreshChangelog();
        }

        private VisualElement BuildAdminPanel()
        {
            var title = Txt("[Debug]", "pc-proj-admin-title");

            var list = NewScroll("pc-proj-admin-list", "pc-proj-scroll");

            for (var i = 0; i < DebugButtons.Length; i++)
            {
                var def = DebugButtons[i];
                var button = FlatButton(def.Label, "pc-proj-btn", "pc-proj-btn-debug");
                button.name = "pc-proj-reset-" + def.Id;
                if (def.Danger) button.AddToClassList("is-danger");

                var id = def.Id;
                WireActivate(button, () => RunDebugReset(id));
                list.Add(button);
            }

            _adminStatus = Lbl(string.Empty, "pc-proj-status");
            _adminStatus.name = "pc-proj-admin-status";

            var panel = El("pc-proj-admin");
            panel.name = "pc-proj-admin";
            panel.pickingMode = PickingMode.Position;
            panel.Add(title);
            panel.Add(list);
            panel.Add(_adminStatus);
            return panel;
        }

        private VisualElement BuildChangelogPanel()
        {
            var title = Txt("ИЗМЕНЕНИЯ", "pc-proj-changelog-title");

            var refresh = FlatButton("↻", "pc-proj-btn", "pc-proj-btn-icon");
            refresh.name = "pc-proj-changelog-refresh";
            refresh.tooltip = "Обновить изменения";
            WireActivate(refresh, RefreshChangelog);

            var head = El("pc-proj-changelog-head");
            head.pickingMode = PickingMode.Ignore;
            Add(head, title);
            Add(head, refresh);

            _changelogStatus = Lbl("Загрузка…", "pc-proj-status");
            _changelogStatus.name = "pc-proj-changelog-status";

            _changelogBody = NewScroll("pc-proj-changelog-body", "pc-proj-scroll", "pc-proj-changelog-body");

            var panel = El("pc-proj-changelog");
            panel.name = "pc-proj-changelog";
            panel.pickingMode = PickingMode.Position;
            panel.Add(head);
            panel.Add(_changelogStatus);
            panel.Add(_changelogBody);
            return panel;
        }

        private VisualElement BuildLangPanel()
        {
            // The mod used a real <select>; DropdownField is the runtime panel's native
            // popup, so the list itself looks like the OS menu the browser drew.
            _langNames = new List<string>(LocaleSelector.Locales.Length);
            for (var i = 0; i < LocaleSelector.Locales.Length; i++)
                _langNames.Add(LocaleSelector.Locales[i].nativeName);

            var saved = SettingsManager.Locale;
            Locale = string.IsNullOrEmpty(saved) ? "ru" : saved;

            _langSelect = new DropdownField();
            _langSelect.name = "pc-proj-lang-select";
            _langSelect.AddToClassList("pc-proj-select");
            _langSelect.tooltip = "Язык интерфейса";
            _langSelect.choices = new List<string>(_langNames);
            _langSelect.SetValueWithoutNotify(_langNames[LocaleIndex(Locale)]);
            FlattenControl(_langSelect);
            _langSelect.RegisterValueChangedCallback(OnLangChanged);

            var host = El("pc-proj-lang");
            host.name = "pc-proj-lang";
            host.pickingMode = PickingMode.Position;
            host.Add(_langSelect);
            return host;
        }

        private VisualElement BuildLinksPanel()
        {
            var panel = El("pc-proj-links");
            panel.name = "pc-proj-links";
            panel.pickingMode = PickingMode.Position;

            for (var i = 0; i < Links.Length; i++)
            {
                var url = Links[i].Url;
                var button = FlatButton(Links[i].Label, "pc-proj-btn", "pc-proj-btn-link");
                button.name = "pc-proj-link-" + i.ToString(CultureInfo.InvariantCulture);
                button.tooltip = url;
                WireActivate(button, () => OpenUrl(url));
                Add(panel, button);
            }

            return panel;
        }

        private VisualElement BuildIpSheet()
        {
            var title = Txt("ПОДКЛЮЧЕНИЕ К СЕРВЕРУ", "pc-proj-sheet-title");
            var label = Txt("Введите IP-адрес сервера:", "pc-proj-label");

            _ipField = new TextField();
            _ipField.name = "pc-proj-ip-field";
            _ipField.AddToClassList("pc-proj-input");
            _ipField.value = "127.0.0.1";
            FlattenControl(_ipField);

            var connect = FlatButton("ПОДКЛЮЧИТЬСЯ", "pc-proj-btn");
            connect.name = "pc-proj-ip-connect";
            WireActivate(connect, () => SubmitIp(null));

            var back = FlatButton("НАЗАД", "pc-proj-btn", "pc-proj-btn-quiet");
            back.name = "pc-proj-ip-back";
            WireActivate(back, CloseIp);

            var row = El("pc-proj-row");
            row.pickingMode = PickingMode.Position;
            Add(row, connect);
            Add(row, back);

            var sheet = El("pc-proj-sheet");
            sheet.name = "pc-proj-ip";
            sheet.pickingMode = PickingMode.Position;
            Add(sheet, title);
            Add(sheet, label);
            Add(sheet, _ipField);
            Add(sheet, row);
            sheet.style.display = DisplayStyle.None;
            IsIpOpen = false;
            return sheet;
        }

        private VisualElement BuildDisclaimer()
        {
            var title = Txt("ДИСКЛЕЙМЕР", "pc-proj-sheet-title");

            var body = Txt(
                "Я делаю эту игру в одиночку и понимаю, что до версии 0.5.0 она может быть " +
                "неиграбельной end-to-end.\n\nСпасибо за понимание и терпение.",
                "pc-proj-disclaimer-body");

            var ok = FlatButton("OK", "pc-proj-btn");
            ok.name = "pc-proj-disclaimer-ok";
            WireActivate(ok, AcceptDisclaimer);

            var card = El("pc-proj-disclaimer-card");
            card.pickingMode = PickingMode.Position;
            Add(card, title);
            Add(card, body);
            Add(card, ok);

            var overlay = El("pc-proj-disclaimer");
            overlay.name = "pc-proj-disclaimer";
            // a modal backdrop: it is the one block that MUST swallow the click
            overlay.pickingMode = PickingMode.Position;
            overlay.Add(card);
            overlay.style.display = DisplayStyle.None;
            return overlay;
        }

        // ------------------------------------------------------------ actions

        /// <summary>project_panels.js openIp.</summary>
        public void OpenIp()
        {
            if (IpSheet == null) return;
            IsIpOpen = true;
            IpSheet.style.display = DisplayStyle.Flex;
        }

        /// <summary>project_panels.js closeIp.</summary>
        public void CloseIp()
        {
            if (IpSheet == null) return;
            IsIpOpen = false;
            IpSheet.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// project_panels.js «ПОДКЛЮЧИТЬСЯ»: read the field, say it, hand the address to
        /// the application, then close. Pass <paramref name="ip"/> to skip the field.
        /// </summary>
        public void SubmitIp(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                ip = _ipField != null ? (_ipField.value ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

            Notify("Подключение к " + ip + ":" + DefaultPort.ToString(CultureInfo.InvariantCulture));

            if (IpConnectHandler != null) IpConnectHandler(ip, DefaultPort);

            OnProjectAction?.Invoke("ip-connect", ip);
            CloseIp();
        }

        /// <summary>project_panels.js setStatus — the admin panel's line under the list.</summary>
        public void SetPanelStatus(string text)
        {
            PanelStatus = text ?? string.Empty;
            if (_adminStatus != null) _adminStatus.text = PanelStatus;
        }

        /// <summary>
        /// project_panels.js «Сброс: id» + fire('debug-reset'). The reset itself goes to
        /// PersistenceDebugTools, whose own line ("removed N file(s)…") is appended to the
        /// id so the panel reports what was actually removed.
        /// </summary>
        public void RunDebugReset(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            SetPanelStatus("Сброс: " + id);

            var result = ResetHandler != null ? ResetHandler(id) : null;
            if (!string.IsNullOrEmpty(result)) SetPanelStatus("Сброс: " + id + " — " + result);

            OnProjectAction?.Invoke("debug-reset", id);
        }

        /// <summary>project_panels.js setLocale + the <c>locale</c> action.</summary>
        public void ChooseLocale(int index)
        {
            if (index < 0 || index >= LocaleSelector.Locales.Length) return;

            var code = LocaleSelector.Locales[index].code;
            var name = LocaleSelector.Locales[index].nativeName;

            Locale = code;
            if (LocaleHandler != null) LocaleHandler(code);

            if (_langSelect != null && _langNames != null)
                _langSelect.SetValueWithoutNotify(_langNames[index]);

            Notify("Язык: " + name);
            OnProjectAction?.Invoke("locale", code);
        }

        private void OnLangChanged(ChangeEvent<string> evt)
        {
            if (_langNames == null) return;
            var index = _langNames.IndexOf(evt.newValue);
            if (index >= 0) ChooseLocale(index);
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (UrlOpenHandler != null) UrlOpenHandler(url);
            OnProjectAction?.Invoke("link", url);
        }

        /// <summary>Shows the first-launch disclaimer (it is also shown on boot when unaccepted).</summary>
        public void ShowDisclaimer()
        {
            if (Disclaimer == null) return;
            DisclaimerOpen = true;
            Disclaimer.style.display = DisplayStyle.Flex;
        }

        /// <summary>project_panels.js acceptDisclaimer — persists before it hides.</summary>
        public void AcceptDisclaimer()
        {
            PlayerPrefs.SetInt(DisclaimerPrefsKey, 1);
            PlayerPrefs.Save();

            DisclaimerOpen = false;
            if (Disclaimer != null) Disclaimer.style.display = DisplayStyle.None;
        }

        // ---------------------------------------------------------- changelog

        /// <summary>project_panels.js loadChangelog.</summary>
        public void RefreshChangelog()
        {
            if (_changelogBody == null) return;

            AbortChangelog();

            ChangelogState = "loading";
            SetChangelogStatus("Загрузка…");

            var request = UnityWebRequest.Get(ChangelogUrl);
            // the scheduled abort below is the port of the mod's 8 s AbortController; the
            // transport timeout is its belt, in case the window is not attached to a panel
            // yet and the UI scheduler cannot run the abort
            request.timeout = (int)(ChangelogAbortMs / 1000L);
            _changelogRequest = request;

            var operation = request.SendWebRequest();
            if (operation == null)
            {
                _changelogRequest = null;
                FailChangelog(request, "Запрос не отправлен");
                return;
            }

            operation.completed += _ => FinishChangelog(request);

            if (Ui != null)
                _changelogAbort = Ui.schedule.Execute(AbortChangelog).StartingIn(ChangelogAbortMs);
        }

        /// <summary>Renders changelog markdown exactly as project_panels.js renderChangelog.</summary>
        public void RenderChangelog(string markdown)
        {
            if (_changelogBody == null) return;

            _changelogBody.Clear();

            var text = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = text.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (line.Length == 0)
                {
                    _changelogBody.Add(El("pc-proj-gap"));
                    continue;
                }

                if (line.StartsWith("---", StringComparison.Ordinal))
                {
                    _changelogBody.Add(El("pc-proj-divider"));
                    continue;
                }

                if (line[0] == '#')
                {
                    AddChangelogLine(line.TrimStart('#').Trim(), "is-heading");
                    continue;
                }

                if (line[0] == '•' || line.StartsWith("- ", StringComparison.Ordinal))
                {
                    AddChangelogLine(line, "is-bullet");
                    continue;
                }

                if (IsMilestone(line))
                {
                    AddChangelogLine(line, "is-milestone");
                    continue;
                }

                AddChangelogLine(line, null);
            }
        }

        private void FinishChangelog(UnityWebRequest request)
        {
            if (request == null) return;

            var current = ReferenceEquals(request, _changelogRequest);
            if (current)
            {
                _changelogRequest = null;
                _changelogAbort?.Pause();
                _changelogAbort = null;
            }

            if (!current || _panelsDisposed || _changelogBody == null)
            {
                // a newer request (or the window going away) owns the panel now
                request.Dispose();
                return;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                var reason = string.IsNullOrEmpty(request.error)
                    ? "HTTP " + request.responseCode.ToString(CultureInfo.InvariantCulture)
                    : request.error;
                request.Dispose();
                FailChangelog(null, reason);
                return;
            }

            var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            request.Dispose();

            RenderChangelog(body);
            SetChangelogStatus("Обновлено из GitHub");
            ChangelogState = "ok";
        }

        private void FailChangelog(UnityWebRequest request, string reason)
        {
            request?.Dispose();

            SetChangelogStatus("Не удалось загрузить changelog");
            ChangelogState = "error";

            if (_changelogBody != null)
            {
                _changelogBody.Clear();
                AddChangelogLine("Проверьте соединение с GitHub.", "is-error");
                if (!string.IsNullOrEmpty(reason)) AddChangelogLine(reason, "is-error");
            }

            Debug.LogWarning("[ProjectCMainMenu] Changelog request failed: " + reason);
        }

        /// <summary>project_panels.js abort.abort() — the 8 s timer and the dispose path.</summary>
        private void AbortChangelog()
        {
            _changelogAbort?.Pause();
            _changelogAbort = null;

            // The request stays in _changelogRequest on purpose: its completion handler
            // is the only place that disposes it and renders the cancelled state.
            _changelogRequest?.Abort();
        }

        private void AddChangelogLine(string text, string modifier)
        {
            if (_changelogBody == null) return;
            var label = Txt(NormalizeChangelogText(text), "pc-proj-log");
            if (!string.IsNullOrEmpty(modifier)) label.AddToClassList(modifier);
            _changelogBody.Add(label);
        }

        private static string NormalizeChangelogText(string text)
        {
            return (text ?? string.Empty)
                .Replace("**", string.Empty)
                .Replace("__", string.Empty)
                .Replace("`", string.Empty);
        }

        private static bool IsMilestone(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;

            int codePoint = line.Length > 1 && char.IsHighSurrogate(line[0]) && char.IsLowSurrogate(line[1])
                ? char.ConvertToUtf32(line[0], line[1])
                : line[0];

            for (var i = 0; i < MilestoneCodePoints.Length; i++)
                if (MilestoneCodePoints[i] == codePoint) return true;

            return false;
        }

        private void SetChangelogStatus(string text)
        {
            if (_changelogStatus != null) _changelogStatus.text = text ?? string.Empty;
        }

        private void OnProjectLayerGeometry(GeometryChangedEvent evt)
        {
            if (ChangelogPanel == null) return;
            var width = evt.newRect.width;
            if (width <= 0f) return;
            ChangelogPanel.EnableInClassList("is-hidden", width < 900f);
        }

        // -------------------------------------------------------- defaults

        private static void DefaultIpConnect(string ip, ushort port)
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<NetworkManagerController>();
            if (controller != null) controller.ConnectToServer(ip, port);
            else Debug.LogError("[ProjectCMainMenu] NetworkManagerController not found: cannot connect to " + ip);
        }

        private static string DefaultDebugReset(string id)
        {
            switch (id)
            {
                case "all": return PersistenceDebugTools.DeleteAllSaves();
                case "position": return PersistenceDebugTools.DeleteCharacterPositionSaves();
                case "inventory": return PersistenceDebugTools.DeleteCharacterInventory();
                case "progression": return PersistenceDebugTools.DeleteCharacterProgression();
                case "customisation": return PersistenceDebugTools.DeleteCharacterCustomisation();
                case "quests": return PersistenceDebugTools.DeleteQuestSaves();
                case "skills": return PersistenceDebugTools.DeleteSkillBindingSaves();
                case "keys": return PersistenceDebugTools.DeleteKeyInstanceSaves();
                case "time": return PersistenceDebugTools.DeleteWorldTimeSaves();
                case "trade": return PersistenceDebugTools.DeleteTradeSaves();
                default: return "Неизвестная цель сброса: " + id;
            }
        }

        private static void DefaultUrlOpen(string url)
        {
            Application.OpenURL(url);
        }

        private int LocaleIndex(string code)
        {
            if (string.IsNullOrEmpty(code)) return 0;
            for (var i = 0; i < LocaleSelector.Locales.Length; i++)
                if (string.Equals(LocaleSelector.Locales[i].code, code, StringComparison.OrdinalIgnoreCase))
                    return i;
            return 0;
        }

        // ---------------------------------------------------------- teardown

        private void DisposeProjectPanels()
        {
            _panelsDisposed = true;

            AbortChangelog();
            _changelogStatus = null;
            _changelogBody = null;

            if (ProjectLayer != null)
            {
                ProjectLayer.UnregisterCallback<GeometryChangedEvent>(OnProjectLayerGeometry);
                ProjectLayer.RemoveFromHierarchy();
                ProjectLayer = null;
            }

            AdminPanel = null;
            ChangelogPanel = null;
            LangHost = null;
            LinksPanel = null;
            IpSheet = null;
            Disclaimer = null;
            _ipField = null;
            _langSelect = null;
            _langNames = null;
            _adminStatus = null;

            IsIpOpen = false;
            DisclaimerOpen = false;
        }

        // ---------------------------------------------------------- snapshot

        public struct ProjectPanelsSnapshot
        {
            public bool LayerPresent;
            public string LayerChildren;
            public string IpDisplay;
            public bool IsIpOpen;
            public string IpValue;
            public string Status;
            public string ChangelogState;
            public string ChangelogStatus;
            public int ChangelogLines;
            public string ChangelogLineClasses;
            public string DisclaimerDisplay;
            public bool DisclaimerOpen;
            public string Locale;
            public string LocaleValue;
            public int LocaleCount;
            public int DebugCount;
            public string DebugLabels;
            public int LinkCount;
            public string LinkLabels;
        }

        /// <summary>
        /// The panels' numbers for tests and journals. Everything is formatted with the
        /// invariant culture: a report has to read the same on a ru-RU machine.
        /// </summary>
        public ProjectPanelsSnapshot TakePanelsSnapshot()
        {
            var snapshot = new ProjectPanelsSnapshot
            {
                LayerPresent = ProjectLayer != null,
                LayerChildren = ProjectLayer != null
                    ? ProjectLayer.childCount.ToString(CultureInfo.InvariantCulture)
                    : "0",
                IpDisplay = Display(IpSheet),
                IsIpOpen = IsIpOpen,
                IpValue = _ipField != null ? _ipField.value ?? string.Empty : string.Empty,
                Status = PanelStatus ?? string.Empty,
                ChangelogState = ChangelogState ?? string.Empty,
                ChangelogStatus = _changelogStatus != null ? _changelogStatus.text : string.Empty,
                ChangelogLines = _changelogBody != null ? _changelogBody.contentContainer.childCount : 0,
                DisclaimerDisplay = Display(Disclaimer),
                DisclaimerOpen = DisclaimerOpen,
                Locale = Locale ?? string.Empty,
                LocaleValue = _langSelect != null ? _langSelect.value ?? string.Empty : string.Empty,
            };

            snapshot.ChangelogLineClasses = JoinLineClasses(_changelogBody);

            if (_langNames != null)
                snapshot.LocaleCount = _langNames.Count;

            var debugLabels = new List<string>(DebugButtons.Length);
            for (var i = 0; i < DebugButtons.Length; i++)
                debugLabels.Add(DebugButtons[i].Label);
            snapshot.DebugCount = debugLabels.Count;
            snapshot.DebugLabels = string.Join(" | ", debugLabels);

            var linkLabels = new List<string>(Links.Length);
            for (var i = 0; i < Links.Length; i++)
                linkLabels.Add(Links[i].Label + "=" + Links[i].Url);
            snapshot.LinkCount = linkLabels.Count;
            snapshot.LinkLabels = string.Join(" | ", linkLabels);

            return snapshot;
        }

        private static string Display(VisualElement element)
        {
            if (element == null) return "absent";
            return element.resolvedStyle.display.ToString();
        }

        private static string JoinLineClasses(ScrollView body)
        {
            if (body == null) return string.Empty;

            var content = body.contentContainer;
            var items = new List<string>(content.childCount);

            for (var i = 0; i < content.childCount; i++)
            {
                var child = content[i];
                string token;

                if (child.ClassListContains("pc-proj-gap")) token = "gap";
                else if (child.ClassListContains("pc-proj-divider")) token = "divider";
                else
                {
                    token = "log";
                    if (child.ClassListContains("is-heading")) token += ".heading";
                    if (child.ClassListContains("is-bullet")) token += ".bullet";
                    if (child.ClassListContains("is-milestone")) token += ".milestone";
                    if (child.ClassListContains("is-error")) token += ".error";
                }

                items.Add(token);
            }

            return string.Join("|", items);
        }

        // ----------------------------------------------------------- helpers

        private static Label Lbl(string text, params string[] classes)
        {
            return (Label)Txt(text, classes);
        }

        /// <summary>
        /// A pc-proj button is a plain Label, like the smoke menu's controls: the default
        /// .unity-button theme must never leak into these plates.
        /// </summary>
        private static VisualElement FlatButton(string text, params string[] classes)
        {
            var button = Txt(text, classes);
            button.focusable = true;
            button.pickingMode = PickingMode.Position;
            return button;
        }

        private static ScrollView NewScroll(string name, params string[] classes)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = name };
            for (var i = 0; i < classes.Length; i++) scroll.AddToClassList(classes[i]);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.pickingMode = PickingMode.Position;
            return scroll;
        }

        private static void WireActivate(VisualElement element, Action action)
        {
            if (element == null || action == null) return;

            element.RegisterCallback<PointerUpEvent>(_ => action());
            element.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!IsActivateKey(evt.keyCode)) return;
                evt.StopPropagation();
                action();
            });
        }

        /// <summary>
        /// TextField and DropdownField are theme-drawn: the elements inside them carry the
        /// default .unity-* background, border and box edges, which would paint a grey plate
        /// inside the .pc-proj-input / .pc-proj-select frame. Those rules live in the theme
        /// stylesheet, so they are neutralised inline two levels down instead — background,
        /// borders, margins and padding only; the text, the caret and the popup arrow stay.
        /// </summary>
        private static void FlattenControl(VisualElement control)
        {
            FlattenLevel(control, 2);
        }

        private static void FlattenLevel(VisualElement parent, int depth)
        {
            if (parent == null || depth <= 0) return;

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent[i];
                if (child == null) continue;

                child.style.backgroundColor = Color.clear;
                child.style.borderTopWidth = 0;
                child.style.borderRightWidth = 0;
                child.style.borderBottomWidth = 0;
                child.style.borderLeftWidth = 0;
                child.style.marginTop = 0;
                child.style.marginRight = 0;
                child.style.marginBottom = 0;
                child.style.marginLeft = 0;
                child.style.paddingTop = 0;
                child.style.paddingRight = 0;
                child.style.paddingBottom = 0;
                child.style.paddingLeft = 0;

                FlattenLevel(child, depth - 1);
            }
        }
    }
}
