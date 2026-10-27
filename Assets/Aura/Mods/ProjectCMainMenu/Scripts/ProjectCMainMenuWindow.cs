// ProjectC main menu — the smoke interface, ported from the mod's src/ui.js.
//
// Step 3 of docs/UI/MainMenu/02_smoke_menu_port.md: the element tree only.
// Behaviour (assembly values, gusts, letters, tabs, toasts, parallax) arrives in
// step 7 on this same class; the smoke field, text blower and altimeter are
// separate components (steps 4-6) that attach to the nodes exposed here.
//
// Two deliberate differences from the DOM, both forced by UI Toolkit:
//   * there is no UXML — the tree is built in code exactly where ui.js builds it;
//   * styled controls are plain VisualElements with focusable = true instead of
//     Button, so the default .unity-button theme can never leak through and
//     PCMM_Menu.uss stays the only source of appearance.
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    public sealed partial class ProjectCMainMenuWindow
    {
        public const string StyleSheetPath = "Assets/Aura/Mods/ProjectCMainMenu/UI/PCMM_Menu.uss";

        // ui.js BUTTONS — the four entries of the project's own main menu. They
        // hang on the altimeter: «Выход» 800 m, «Настройки» 1200 m,
        // «Подключиться к серверу» 2200 m, «Одиночная игра» 3000 m.
        public struct ButtonDef
        {
            public string Action;
            public string Label;
            public int Alt;
        }

        public static readonly ButtonDef[] Buttons =
        {
            new ButtonDef { Action = "host", Label = "ОДИНОЧНАЯ ИГРА", Alt = 3000 },
            new ButtonDef { Action = "connect", Label = "ПОДКЛЮЧИТЬСЯ К СЕРВЕРУ", Alt = 2200 },
            new ButtonDef { Action = "settings", Label = "НАСТРОЙКИ", Alt = 1200 },
            new ButtonDef { Action = "quit", Label = "ВЫХОД", Alt = 800 },
        };

        public struct TabDef
        {
            public string Id;
            public string Label;
        }

        public static readonly TabDef[] Tabs =
        {
            new TabDef { Id = "video", Label = "Графика" },
            new TabDef { Id = "audio", Label = "Звук" },
            new TabDef { Id = "game", Label = "Геймплей" },
        };

        private static readonly string[] BlobKeys = { "a", "b", "c", "d", "e", "f", "g" };

        // ---- exposed nodes (steps 4-9 attach to these) --------------------
        public VisualElement Root { get; private set; }
        public VisualElement Ui { get; private set; }

        public VisualElement Scan { get; private set; }
        public VisualElement Main { get; private set; }
        public VisualElement MainScrim { get; private set; }
        public VisualElement TitleBlock { get; private set; }
        public VisualElement Wordmark { get; private set; }
        public VisualElement Rule { get; private set; }
        public VisualElement Subtitle { get; private set; }
        public VisualElement NavRow { get; private set; }
        public VisualElement Nav { get; private set; }
        public VisualElement AltHost { get; private set; }
        public VisualElement AltTape { get; private set; }
        public VisualElement AltBand { get; private set; }
        public VisualElement AltReadout { get; private set; }

        public VisualElement[] NavButtons { get; private set; }
        public VisualElement[] NavButtonLabels { get; private set; }

        public VisualElement Settings { get; private set; }
        public VisualElement Card { get; private set; }
        public VisualElement CardCloud { get; private set; }
        public VisualElement CardMist { get; private set; }
        public VisualElement CardHead { get; private set; }
        public VisualElement CardTitle { get; private set; }
        public VisualElement CloseButton { get; private set; }
        public VisualElement TabsBar { get; private set; }
        public VisualElement[] TabButtons { get; private set; }
        public VisualElement PanelsHost { get; private set; }
        public VisualElement[] Panels { get; private set; }

        public VisualElement Toast { get; private set; }
        public VisualElement ToastLabel { get; private set; }
        public VisualElement Foot { get; private set; }

        // ui.js reads the action off the clicked button; keep the mapping here so
        // step 7 never has to parse class names.
        private readonly Dictionary<string, VisualElement> _buttonByAction =
            new Dictionary<string, VisualElement>();

        public ProjectCMainMenuWindow()
        {
            Build();
        }

        public VisualElement ButtonFor(string action)
        {
            VisualElement el;
            return _buttonByAction.TryGetValue(action, out el) ? el : null;
        }

        /// <summary>Appends the interface to <paramref name="host"/> and takes ownership of it.</summary>
        public void Mount(VisualElement host)
        {
            Root = host;
            Root.AddToClassList("pc-ui-root");
            Root.pickingMode = PickingMode.Ignore;
            Root.Add(Ui);

            // the faces live in an asset, not in the stylesheet (see
            // ProjectCMainMenuWindow.Fonts.cs); the tree has to exist before they are set
            ApplyFonts();
        }

        // ------------------------------------------------------------------
        // construction
        // ------------------------------------------------------------------

        private void Build()
        {
            // ---- scan overlay: vignette + scanlines + the travelling sweep ----
            var scanSweep = El("pc-scan__sweep");
            scanSweep.pickingMode = PickingMode.Ignore;
            Scan = El("pc-scan");
            Scan.name = "pc-scan";
            Scan.pickingMode = PickingMode.Ignore;
            Scan.Add(scanSweep);

            // ---- main column ------------------------------------------------
            MainScrim = El("pc-main__scrim");
            MainScrim.pickingMode = PickingMode.Ignore;

            TitleBlock = El("pc-title-block");
            TitleBlock.pickingMode = PickingMode.Ignore;

            // ui.js opened the title block with an «eyebrow» line («THE CLOUDS ·
            // ТЕРМИНАЛ ДОСТУПА»). It is placeholder copy, not part of the project's
            // identity, so the block starts on the wordmark.
            Wordmark = El("pc-wordmark");
            Wordmark.pickingMode = PickingMode.Ignore;
            Add(Wordmark, Txt("PROJECT", "pc-wordmark-text"));
            Add(Wordmark, Txt("C", "pc-wordmark-c"));
            Add(TitleBlock, Wordmark);

            Rule = El("pc-rule");
            Rule.pickingMode = PickingMode.Ignore;
            Add(TitleBlock, Rule);

            Subtitle = Txt(Up("Версия Alpha 0.1.94"), "pc-subtitle");
            Add(TitleBlock, Subtitle);

            AltHost = BuildAltimeterHost();
            Nav = BuildNav();
            NavRow = El("pc-nav-row");
            NavRow.pickingMode = PickingMode.Ignore;
            Add(NavRow, AltHost);
            Add(NavRow, Nav);

            Main = El("pc-main");
            Main.name = "pc-menu-main";
            Main.pickingMode = PickingMode.Ignore;
            Add(Main, MainScrim);
            Add(Main, TitleBlock);
            Add(Main, NavRow);

            // ---- settings window --------------------------------------------
            Settings = BuildSettings();

            // ---- toast -------------------------------------------------------
            // CSS puts the text on .pc-toast itself and a Label can carry both the
            // text and the pill styling, so there is no wrapper element.
            Toast = Txt(string.Empty, "pc-toast");
            Toast.pickingMode = PickingMode.Ignore;
            ToastLabel = Toast;

            // ---- footer ------------------------------------------------------
            Foot = BuildFoot();

            // ---- the whole interface ----------------------------------------
            // ui.js: el('div', { class: 'pc-ui', lang: 'ru' },
            //            [scan, mainEl, settingsEl, toastEl, foot])
            Ui = El("pc-ui");
            Ui.pickingMode = PickingMode.Ignore;
            Ui.Add(Scan);
            Ui.Add(Main);
            Ui.Add(Settings);
            Ui.Add(Toast);
            Ui.Add(Foot);

            // the gradients the stylesheet can only describe (see PCMM_Menu.uss)
            ApplyRamps();
        }

        private VisualElement BuildAltimeterHost()
        {
            // The tape's contents (800/1200/2200/3000 m ticks) belong to step 6;
            // the host and its furniture exist now so the layout is final.
            var fadeTop = El("pc-alt__fade-top");
            fadeTop.pickingMode = PickingMode.Ignore;
            var fadeBottom = El("pc-alt__fade-bottom");
            fadeBottom.pickingMode = PickingMode.Ignore;

            AltTape = El("pc-alt-tape");
            AltTape.pickingMode = PickingMode.Ignore;

            AltBand = El("pc-alt-band");
            AltBand.pickingMode = PickingMode.Ignore;

            AltReadout = Txt(string.Empty, "pc-alt-readout");

            var host = El("pc-alt");
            host.pickingMode = PickingMode.Ignore;
            host.Add(AltTape);
            host.Add(fadeTop);
            host.Add(fadeBottom);
            host.Add(AltBand);
            host.Add(AltReadout);
            return host;
        }

        private VisualElement BuildNav()
        {
            var nav = El("pc-nav");
            nav.name = "pc-nav";
            nav.pickingMode = PickingMode.Position;   // CSS: pointer-events: auto

            NavButtons = new VisualElement[Buttons.Length];
            NavButtonLabels = new VisualElement[Buttons.Length];

            for (var i = 0; i < Buttons.Length; i++)
            {
                VisualElement label;
                var btn = BuildNavButton(Buttons[i], i, out label);
                NavButtons[i] = btn;
                NavButtonLabels[i] = label;
                Add(nav, btn);
            }

            return nav;
        }

        private VisualElement BuildNavButton(ButtonDef def, int index, out VisualElement labelOut)
        {
            // The button is a cloud, not a plate: seven non-repeating blobs that
            // boil slowly around the label, plus the pale rim the cloud catches
            // from the sky above it (CSS ::after, painted last).
            var cloud = El("pc-btn-cloud");
            cloud.pickingMode = PickingMode.Ignore;
            for (var k = 0; k < BlobKeys.Length; k++)
            {
                var blob = El("pc-blob", "pc-blob-" + BlobKeys[k]);
                blob.pickingMode = PickingMode.Ignore;
                cloud.Add(blob);
            }

            var rim = El("pc-btn-cloud__rim");
            rim.pickingMode = PickingMode.Ignore;
            cloud.Add(rim);

            var label = Txt(def.Label, "pc-btn-label");
            labelOut = label;

            var btn = El("pc-btn", "pc-btn-n" + (index + 1));
            btn.name = "pc-btn-" + def.Action;
            btn.focusable = true;
            btn.pickingMode = PickingMode.Position;
            btn.Add(cloud);
            btn.Add(label);

            _buttonByAction[def.Action] = btn;
            return btn;
        }

        private VisualElement BuildSettings()
        {
            // The window is not a plate: a bank of cloud the content hangs in. The
            // blobs sit outside the content box; the content itself has no frame.
            CardCloud = El("pc-card-cloud");
            CardCloud.pickingMode = PickingMode.Ignore;
            for (var i = 1; i <= 10; i++)
            {
                var cld = El("pc-cld", "pc-cld-" + i);
                cld.pickingMode = PickingMode.Ignore;
                CardCloud.Add(cld);
            }

            var cloudRim = El("pc-card-cloud__rim");
            cloudRim.pickingMode = PickingMode.Ignore;
            CardCloud.Add(cloudRim);

            // the mist the text sits in (CSS .pc-card::before)
            CardMist = El("pc-card__mist");
            CardMist.pickingMode = PickingMode.Ignore;

            // ---- head ------------------------------------------------------
            CardTitle = Txt(Up("Настройки"), "pc-card-title");
            CardTitle.name = "pc-settings-title";

            CloseButton = BuildCloseButton();

            CardHead = El("pc-card-head");
            CardHead.pickingMode = PickingMode.Position;
            CardHead.Add(CardTitle);
            CardHead.Add(CloseButton);

            var headRule = El("pc-card-head__rule");
            headRule.pickingMode = PickingMode.Ignore;
            CardHead.Add(headRule);

            // ---- tabs + panels ---------------------------------------------
            TabsBar = El("pc-tabs");
            TabsBar.name = "pc-tabs";
            TabsBar.pickingMode = PickingMode.Position;

            PanelsHost = El("pc-panels");
            PanelsHost.name = "pc-panels";
            PanelsHost.pickingMode = PickingMode.Position;

            TabButtons = new VisualElement[Tabs.Length];
            Panels = new VisualElement[Tabs.Length];

            for (var i = 0; i < Tabs.Length; i++)
            {
                var on = i == 0;

                // A TextElement that owns a child stops measuring its own text: with the glow
                // inside it the tab collapsed to its padding alone (measured 5.1 px against
                // 65.6 px for the same label in a plain container), so the three tabs overlapped
                // inside the 623.7 px bar. The tab is a container now and the label is a child;
                // class names and nesting stay the same, so the USS rules are unchanged.
                var tab = El("pc-tab");
                tab.name = "pc-tab-" + Tabs[i].Id;
                tab.focusable = true;
                tab.pickingMode = PickingMode.Position;

                var glow = El("pc-tab__glow");
                glow.pickingMode = PickingMode.Ignore;
                tab.Add(glow);

                var tabLabel = Txt(Up(Tabs[i].Label), "pc-tab__label");
                tabLabel.pickingMode = PickingMode.Ignore;
                tab.Add(tabLabel);

                if (on) tab.AddToClassList("is-active");
                Add(TabsBar, tab);
                TabButtons[i] = tab;

                var panel = El("pc-panel");
                panel.name = "pc-panel-" + Tabs[i].Id;
                panel.pickingMode = PickingMode.Position;
                BuildTabContent(panel, Tabs[i].Id);
                if (on) panel.AddToClassList("is-active");
                else panel.AddToClassList("is-hidden");
                Add(PanelsHost, panel);
                Panels[i] = panel;
            }

            // CSS .pc-tabs::after — the haze that dissolves at both ends, painted
            // over the bar, so it is appended last.
            var tabsRule = El("pc-tabs__rule");
            tabsRule.pickingMode = PickingMode.Ignore;
            TabsBar.Add(tabsRule);

            // ---- the card ---------------------------------------------------
            Card = El("pc-card");
            Card.pickingMode = PickingMode.Position;
            Card.Add(CardCloud);
            Card.Add(CardMist);
            Card.Add(CardHead);
            Card.Add(TabsBar);
            Card.Add(PanelsHost);

            // ---- the window -------------------------------------------------
            var settings = El("pc-settings");
            settings.name = "pc-settings";
            settings.AddToClassList("is-hidden");   // CSS: hidden until opened
            settings.pickingMode = PickingMode.Position;
            settings.Add(Card);
            return settings;
        }

        private VisualElement BuildCloseButton()
        {
            var line1 = El("pc-close-line", "pc-close-line-1");
            line1.pickingMode = PickingMode.Ignore;
            var line2 = El("pc-close-line", "pc-close-line-2");
            line2.pickingMode = PickingMode.Ignore;

            var close = El("pc-close");
            close.name = "pc-close";
            close.focusable = true;
            close.pickingMode = PickingMode.Position;
            close.Add(line1);
            close.Add(line2);
            return close;
        }

        private VisualElement BuildFoot()
        {
            // ui.js filled the footer with the control hint («Кнопки и окна сгущаются
            // из дыма…» + the Enter/←/→/Esc legend) and the build stamp
            // («PROJECTC · MENU BUILD 0.5.0 · MESMIUM»). All of it is placeholder copy
            // that is not part of the project's own interface, so the footer stays in
            // the tree as an empty host and nothing is drawn on it.
            var foot = El("pc-foot");
            foot.pickingMode = PickingMode.Ignore;
            return foot;
        }

        // ------------------------------------------------------------------
        // tab contents (ui.js buildVideoTab / buildAudioTab / buildGameTab)
        // ------------------------------------------------------------------

        private void BuildTabContent(VisualElement panel, string tabId)
        {
            switch (tabId)
            {
                case "video":
                    Add(panel, GroupTitle("Качество"));
                    Add(panel, SelectRow("Качество", new[] { "Низкое", "Среднее", "Высокое", "Ультра" }, "Высокое"));
                    Add(panel, GroupTitle("Экран"));
                    Add(panel, SelectRow("Разрешение",
                        new[] { "1280 × 720", "1600 × 900", "1920 × 1080", "2560 × 1440" }, "1920 × 1080"));
                    Add(panel, SwitchRow("Полный экран", true, null));
                    Add(panel, SwitchRow("Вертикальная синхронизация", true, null));
                    Add(panel, SelectRow("Сглаживание", new[] { "Выкл.", "2x", "4x", "8x" }, "4x"));
                    Add(panel, NoteLine("Страница повторяет GraphicsSettingsSection проекта."));
                    break;

                case "audio":
                    Add(panel, GroupTitle("Микшер"));
                    Add(panel, RangeRow("Общая громкость", 78f, null));
                    Add(panel, RangeRow("Музыка", 55f, "placeholder до AudioMixer"));
                    Add(panel, RangeRow("Эффекты", 84f, "placeholder до AudioMixer"));
                    Add(panel, RangeRow("Голос", 70f, "placeholder до AudioMixer"));
                    Add(panel, RangeRow("Интерфейс", 70f, "placeholder до AudioMixer"));
                    Add(panel, NoteLine("Страница повторяет AudioSettingsSection проекта."));
                    break;

                case "game":
                    Add(panel, GroupTitle("Управление"));
                    Add(panel, RangeRow("Чувствительность прицела", 48f, null));
                    Add(panel, SwitchRow("Инверсия по вертикали", false, null));
                    Add(panel, GroupTitle("Сессия"));
                    Add(panel, SwitchRow("Автосохранение", false, "Чекпойнт каждые 5 минут"));
                    Add(panel, KeysRow("Пауза", new[] { "Esc" }));
                    Add(panel, NoteLine("Страница повторяет GameplaySettingsSection проекта."));
                    break;
            }
        }

        // ---- row helpers (ui.js rowEl / groupTitle / noteLine / *Row) ------

        private static VisualElement Row(string label, VisualElement control, string note)
        {
            var text = El("pc-row-text");
            text.pickingMode = PickingMode.Ignore;
            Add(text, Txt(label, "pc-row-label"));
            if (!string.IsNullOrEmpty(note)) Add(text, Txt(note, "pc-row-note"));

            var row = El("pc-row");
            row.pickingMode = PickingMode.Position;
            Add(row, text);
            Add(row, control);
            return row;
        }

        private static VisualElement GroupTitle(string text)
        {
            return Txt(Up(text), "pc-group-title");
        }

        private static VisualElement NoteLine(string text)
        {
            return Txt(text, "pc-note-line");
        }

        private static VisualElement RangeRow(string label, float value, string note)
        {
            // CSS styled ::-webkit-slider-*; UI Toolkit has none, so the track and
            // the thumb are real children (see PCMM_Menu.uss .pc-range-track /
            // .pc-range-thumb). Step 7 wires the drag.
            var track = El("pc-range-track");
            track.pickingMode = PickingMode.Ignore;
            var thumb = El("pc-range-thumb");
            thumb.pickingMode = PickingMode.Ignore;

            var range = El("pc-range");
            range.pickingMode = PickingMode.Position;
            range.focusable = true;
            range.Add(track);
            range.Add(thumb);

            var output = Txt(FormatPercent(value), "pc-out");

            var ctl = El("pc-ctl", "pc-ctl-range");
            ctl.pickingMode = PickingMode.Ignore;
            Add(ctl, range);
            Add(ctl, output);
            return Row(label, ctl, note);
        }

        private static VisualElement SwitchRow(string label, bool isOn, string note)
        {
            var glow = El("pc-switch__glow");
            glow.pickingMode = PickingMode.Ignore;
            var knob = El("pc-switch-knob");
            knob.pickingMode = PickingMode.Ignore;

            var toggle = El("pc-switch");
            toggle.pickingMode = PickingMode.Position;
            toggle.focusable = true;
            if (isOn) toggle.AddToClassList("is-on");
            toggle.Add(glow);
            toggle.Add(knob);
            return Row(label, toggle, note);
        }

        private static VisualElement SelectRow(string label, string[] options, string selected)
        {
            // The mod used a real <select>. UI Toolkit has no styleable one, so the
            // closed control is a label inside the chevron wrapper; the popup is
            // step 7's job. Options are kept on the element for it.
            var select = Txt(selected ?? (options.Length > 0 ? options[0] : string.Empty), "pc-select");
            select.pickingMode = PickingMode.Position;
            select.focusable = true;
            select.userData = options;

            var chevron = El("pc-select-wrap__chevron");
            chevron.pickingMode = PickingMode.Ignore;

            var wrap = El("pc-select-wrap");
            wrap.pickingMode = PickingMode.Ignore;
            Add(wrap, select);
            wrap.Add(chevron);
            return Row(label, wrap, null);
        }

        private static VisualElement KeysRow(string label, string[] keys)
        {
            var host = El("pc-keys");
            host.pickingMode = PickingMode.Ignore;
            for (var i = 0; i < keys.Length; i++) Add(host, Txt(keys[i], "pc-kbd"));
            return Row(label, host, null);
        }

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------

        private static VisualElement El(params string[] classes)
        {
            var el = new VisualElement();
            for (var i = 0; i < classes.Length; i++) el.AddToClassList(classes[i]);
            return el;
        }

        private static Label Txt(string text, params string[] classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            for (var i = 0; i < classes.Length; i++) label.AddToClassList(classes[i]);
            return label;
        }

        /// <summary>
        /// Appends and, when the child is the first one, marks it — USS has neither
        /// `gap` nor the `+` combinator, so containers carry the gap as a child
        /// margin and the first child cancels it (see PCMM_Menu.uss).
        /// </summary>
        private static T Add<T>(VisualElement parent, T child) where T : VisualElement
        {
            if (parent.childCount == 0) child.AddToClassList("is-first-in-group");
            parent.Add(child);
            return child;
        }

        private static void SetVisible(VisualElement el, bool visible)
        {
            el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string Up(string text)
        {
            // CSS text-transform: uppercase
            return string.IsNullOrEmpty(text) ? text : text.ToUpperInvariant();
        }

        private static string FormatPercent(float value)
        {
            return UnityEngine.Mathf.RoundToInt(value) + "%";
        }
    }
}
