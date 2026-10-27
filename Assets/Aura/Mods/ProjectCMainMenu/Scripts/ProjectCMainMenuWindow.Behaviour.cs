// ProjectC main menu — behaviour, ported 1:1 from the mod's src/ui.js.
//
// Step 7 of docs/UI/MainMenu/02_smoke_menu_port.md. The element tree lives in
// ProjectCMainMenuWindow.cs (step 3) and is not touched here: this file gives it
// the assembly values, the gusts, the buttons, the settings window, the tab
// switch, the title parallax, the toasts and the smoke readouts.
//
// The one thing the DOM can do and UI Toolkit cannot is the custom property:
// `--a` is written with setProperty() in the mod, but VisualElement exposes
// customStyle read-only. PCMM_Menu.uss therefore keeps the authored default
// (opacity: var(--a, 1)) and this file writes, for each part, the inline style
// that rule would have produced — opacity, filter blur, translate and scale.
//
// Two deliberate differences, both forced:
//   * `queue(fn, ms)` (setTimeout) becomes a one-shot scheduled item on the UI;
//   * `window.addEventListener` on pointermove / keydown becomes a callback on
//     the panel's root, which is the element that sees every pointer move even
//     when the menu itself is pickingMode = Ignore.
//
// Animation values stay clock/damp based, never accumulated per call, so the
// same Update() replayed with the same dt gives the same numbers.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    public sealed partial class ProjectCMainMenuWindow
    {
        // ui.js PRESS_MS / APPEAR_STAGGER / DISSOLVE_STAGGER
        public const float PressMs = 120f;
        public const float AppearStagger = 0.058f;
        public const float DissolveStagger = 0.046f;

        // ui.js GUST and WIND: one strength and one duration per gesture
        private const float GustHover = 0.5f;
        private const float GustPress = 1f;
        private const float GustOpen = 0.85f;
        private const float GustTab = 0.7f;
        private const float GustClose = 0.72f;

        private const float WindHoverMs = 520f;
        private const float WindPressMs = 720f;
        private const float WindTabMs = 620f;
        private const float WindOpenMs = 700f;
        private const float WindCloseMs = 640f;

        // ui.js CARD_TEXT / PANEL_TEXT — the hosts whose letters the wind takes
        private static readonly string[] CardTextClasses =
        {
            "pc-card-title", "pc-tab", "pc-group-title", "pc-row-label", "pc-row-note", "pc-note-line",
        };

        private static readonly string[] PanelTextClasses =
        {
            "pc-group-title", "pc-row-label", "pc-row-note", "pc-note-line",
        };

        // ------------------------------------------------------------- types

        /// <summary>One gust handed to the atmosphere (onBlow in the mod).</summary>
        public struct Gust
        {
            public float X;
            public float Y;
            public float DirX;
            public float DirY;
            public float Strength;
            public float DurationMs;

            public override string ToString() =>
                string.Format(CultureInfo.InvariantCulture,
                    "({0:F3}, {1:F3}) dir ({2:F2}, {3:F2}) s {4:F2} {5:F0}ms",
                    X, Y, DirX, DirY, Strength, DurationMs);
        }

        private struct Wind
        {
            public int Dir;
            public float DurationMs;
        }

        private enum PartKind
        {
            Title,
            Button,
            Note,
            Card,
        }

        private sealed class Part
        {
            public VisualElement El;
            public VisualElement Cloud;   // buttons only: the alpha follows --a
            public PartKind Kind;
            public float A;
            public float Target;
            public float Delay;
            public int Order;
            public bool Soft;
            public float Written = -1f;
            public float LastBlur;
            public float LastCloudOpacity = -1f;
            public string Name;
        }

        public struct PartSnapshot
        {
            public string Name;
            public float A;
            public float Target;
            public float Delay;
            public bool Forming;
            public float Opacity;
            public float BlurPx;
            public float CloudOpacity;
        }

        public struct WindowSnapshot
        {
            public string View;
            public bool SettingsOpen;
            public int ActiveTab;
            public string Tab;
            public string Hovered;
            public bool Busy;
            public string TabPhase;
            public float MenuA;
            public float CardA;
            public float TabA;
            public float MenuTarget;
            public int PendingTab;
            public int TabDir;
            public int WindDir;
            public float WindDuration;
            public int WindSeq;
            public int TabSeq;
            public float PointerX;
            public float PointerY;
            public float ExitY;
            public int Puffs;
            public int Ghosts;
            public int Blows;
            public string LastBlow;
            public string MainVisibility;
            public bool MainBehind;
            public bool SettingsHidden;
            public string ScrimOpacity;
            public string AltOpacity;
            public List<PartSnapshot> Parts;
            public string ActivePanel;
            public string Items;
        }

        // ------------------------------------------------------------ fields

        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Part> _menuParts = new List<Part>();
        private readonly List<IVisualElementScheduledItem> _timers = new List<IVisualElementScheduledItem>();

        private Part _cardPart;
        private int _menuMaxOrder;

        private float _menuWritten = -1f;
        private float _targetX;
        private float _targetY;
        private float _curX;
        private float _curY;
        private float _appliedX = 999f;
        private float _appliedY = 999f;
        private int _exitTick;
        private bool _suppressFocusBlow;
        private bool _started;

        private IVisualElementScheduledItem _autoTickItem;
        private IVisualElementScheduledItem _toastOff;
        private double _lastTickClock;
        private VisualElement _pointerTarget;

        // -------------------------------------------------------------- state

        public string View { get; private set; } = "menu";
        public bool SettingsOpen { get; private set; }
        public int ActiveTab { get; private set; }
        public string Tab { get; private set; } = "video";
        public string Hovered { get; private set; }
        public bool Busy { get; private set; }
        public Gust? LastBlow { get; private set; }
        public float MenuTarget { get; private set; } = 1f;
        public int PendingTab { get; private set; }
        public int TabDir { get; private set; } = 1;
        public int Puffs { get; private set; }
        public float MenuA { get; private set; } = 1f;
        public float CardA { get; private set; }
        public float TabA { get; private set; } = 1f;
        public string TabPhase { get; private set; } = "idle";
        public float SmokeDensity { get; private set; } = 1f;
        public float WindStrength { get; private set; } = 1f;
        public int WindDir { get; private set; } = 1;
        public ProjectCMainMenuSmokeField.Flow Flow { get; private set; }
        public int Ghosts { get; private set; }
        public int Blows { get; private set; }
        public float DissolveSpeed { get; private set; } = 1f;
        public float TabOut { get; private set; }
        public float PointerX { get; private set; } = 0.5f;
        public float PointerY { get; private set; } = 0.5f;
        public float ExitY { get; private set; } = 0.22f;
        public float WindDuration { get; private set; } = WindPressMs;
        public int WindSeq { get; private set; }
        public int TabSeq { get; private set; }

        // ----------------------------------------------------------- hooks

        /// <summary>Drives Update from a 16 ms tick. Turn it off to step the window by hand.</summary>
        public bool AutoTick { get; set; } = true;

        /// <summary>ui.js onBlow — the atmosphere parts its clouds on the same wind.</summary>
        public Action<Gust> OnBlow { get; set; }

        /// <summary>ui.js onAction — host / quit (settings and connect are handled inside).</summary>
        public Action<string, string> OnAction { get; set; }

        /// <summary>Step 8 hangs the IP screen here; until then «connect» falls through to OnAction.</summary>
        public Action OnConnectRequested { get; set; }

        public ProjectCMainMenuSmokeField Field { get; private set; }
        public ProjectCMainMenuTextBlower Blower { get; private set; }
        public ProjectCMainMenuAltimeter Altimeter { get; private set; }
        public int PartCount => _parts.Count;

        // -------------------------------------------------------------- boot

        /// <summary>
        /// Wires the window up: the two smoke components, the altitude tape, the
        /// assembly parts and the boot condensation. Call once, after Mount().
        /// </summary>
        public void Start()
        {
            if (_started) return;
            _started = true;

            // the tapes and the two smoke layers live in .pc-ui, appended in the
            // same order ui.js appends them (after the footer, so on top)
            Altimeter = new ProjectCMainMenuAltimeter(AltHost, Marks());
            Field = new ProjectCMainMenuSmokeField(Ui, 9137, 96);
            Blower = new ProjectCMainMenuTextBlower(Ui, 5573, 150);

            BuildParts();
            WireEvents();
            BuildProjectPanels();   // step 8: IP, resets, changelog, links, locale, disclaimer
            HookPointer();

            Queue(BootSmoke, 50f);

            if (AutoTick)
            {
                _lastTickClock = Time.realtimeSinceStartupAsDouble;
                _autoTickItem = Ui.schedule.Execute(AutoTickFrame).Every(16);
            }
        }

        private List<ProjectCMainMenuAltimeter.Mark> Marks()
        {
            var marks = new List<ProjectCMainMenuAltimeter.Mark>(Buttons.Length);
            for (var i = 0; i < Buttons.Length; i++)
            {
                marks.Add(new ProjectCMainMenuAltimeter.Mark { Value = Buttons[i].Alt, El = NavButtons[i] });
            }

            return marks;
        }

        // ------------------------------------------------------ parts (--a)

        private void BuildParts()
        {
            // ui.js: the title, the four buttons and the note hang on staggered
            // orders; the settings window is order 0 of its own list and starts at 0
            var titlePart = AddPart(TitleBlock, PartKind.Title, 0, 1f, "title");
            for (var i = 0; i < NavButtons.Length; i++)
            {
                var cloud = NavButtons[i].Q(className: "pc-btn-cloud");
                _menuParts.Add(AddPart(NavButtons[i], PartKind.Button, 1 + i, 1f, "btn-" + Buttons[i].Action, cloud));
            }

            // the note that used to close the menu («В глубине качаются тросы…») is
            // placeholder copy and is gone, so the last staggered part is the fourth button
            _menuParts.Insert(0, titlePart);
            _menuMaxOrder = _menuParts.Count - 1;

            _cardPart = AddPart(Settings, PartKind.Card, 0, 0f, "card");
        }

        private Part AddPart(VisualElement node, PartKind kind, int order, float initial, string name,
            VisualElement cloud = null)
        {
            var part = new Part
            {
                El = node,
                Kind = kind,
                Cloud = cloud,
                Name = name,
                A = initial,
                Target = initial,
                Order = order,
                Soft = initial < 0.985f,
            };

            WritePart(part);
            _parts.Add(part);
            return part;
        }

        private void WritePart(Part part)
        {
            // the gate keeps style writes down, but it must not leave the rest pose
            // hanging a fraction short: 0 and 1 always go through exactly
            var endpoint = part.A <= 0f || part.A >= 1f;
            var changed = Mathf.Abs(part.A - part.Written) > 0.0025f || (endpoint && part.Written != part.A);
            if (changed) part.Written = part.A;

            var soft = part.A < 0.985f;
            var softChanged = soft != part.Soft;
            if (softChanged)
            {
                part.Soft = soft;
                part.El.EnableInClassList("is-forming", soft);
            }

            if (!changed && !softChanged) return;

            // the inline half of every `var(--a, …)` rule that touches this part
            part.El.style.opacity = part.A;

            switch (part.Kind)
            {
                case PartKind.Title:
                    SetBlur(part, soft ? (1f - part.A) * 6f : 0f);
                    break;

                case PartKind.Note:
                    SetBlur(part, soft ? (1f - part.A) * 4f : 0f);
                    break;

                case PartKind.Button:
                    SetBlur(part, soft ? (1f - part.A) * 5f : 0f);
                    if (part.Cloud != null)
                    {
                        // while forming, the cloud's alpha is part of the smoke; at
                        // rest the USS rules own it (0.88 base, 0.92 hover, 0 on
                        // the press burst) and an inline value would win over them
                        if (soft)
                        {
                            part.LastCloudOpacity = 0.22f + 0.66f * part.A;
                            part.Cloud.style.opacity = part.LastCloudOpacity;
                        }
                        else
                        {
                            part.LastCloudOpacity = -1f;
                            part.Cloud.style.opacity = StyleKeyword.Null;
                        }
                    }

                    break;

                case PartKind.Card:
                    // ui.css .pc-card: translateY((1 - a) * 16px) scale(0.966 + 0.034a)
                    Card.style.translate = new Translate(0f, Length.Pixels((1f - part.A) * 16f));
                    var scale = 0.966f + 0.034f * part.A;
                    Card.style.scale = new Scale(new Vector3(scale, scale, 1f));
                    if (CardCloud != null) CardCloud.style.opacity = 0.4f + 0.6f * part.A;
                    if (CardMist != null) CardMist.style.opacity = 0.55f + 0.45f * part.A;
                    SetBlur(part, soft ? (1f - part.A) * 7f : 0f, Card);
                    break;
            }
        }

        private void SetBlur(Part part, float px, VisualElement target = null)
        {
            target = target ?? part.El;
            part.LastBlur = px;

            // Dynamic filters abort the panel repaint in this editor — see
            // ProjectCMainMenuTextBlower.BlurFiltersEnabled.
            if (!ProjectCMainMenuTextBlower.BlurFiltersEnabled) return;

            if (px <= 0.001f)
            {
                target.style.filter = StyleKeyword.Null;
                return;
            }

            // FilterFunction has no Blur() factory (see step 5's journal)
            var blur = new FilterFunction(FilterFunctionType.Blur);
            blur.AddParameter(new FilterParameter(px));
            target.style.filter = new StyleList<FilterFunction>(new List<FilterFunction> { blur });
        }

        private void TickParts(float step)
        {
            var sum = 0f;
            for (var i = 0; i < _parts.Count; i++)
            {
                var part = _parts[i];
                if (part.Delay > 0f)
                {
                    part.Delay = Mathf.Max(0f, part.Delay - step);
                }
                else if (part.A != part.Target)
                {
                    var lambda = part.A < part.Target ? 7f : 9.5f;
                    part.A = ProjectCMainMenuPalette.Damp(part.A, part.Target, lambda, step);
                    if (Mathf.Abs(part.A - part.Target) < 0.004f) part.A = part.Target;
                }

                WritePart(part);
                if (part != _cardPart) sum += part.A;
            }

            MenuA = sum / Mathf.Max(1, _menuParts.Count);
            CardA = _cardPart.A;

            // `--a` on .pc-main is inherited by the scrim and by the tape; same rule
            // as the parts: the end state is written exactly, never rounded short
            var menuEndpoint = MenuA <= 0f || MenuA >= 1f;
            if (Mathf.Abs(MenuA - _menuWritten) > 0.004f || (menuEndpoint && _menuWritten != MenuA))
            {
                _menuWritten = MenuA;
                // `--a` on .pc-main is inherited by the scrim and by the tape
                MainScrim.style.opacity = MenuA;
                AltHost.style.opacity = MenuA;
            }

            if (Mathf.Approximately(MenuTarget, 0f))
            {
                if (MenuA <= 0.004f && Main.resolvedStyle.visibility != Visibility.Hidden)
                {
                    Main.style.visibility = Visibility.Hidden;
                }
            }
            else if (MenuA > 0.32f && Main.ClassListContains("is-behind"))
            {
                Main.RemoveFromClassList("is-behind");
            }

            if (!SettingsOpen && CardA <= 0.002f && !Settings.ClassListContains("is-hidden"))
            {
                Settings.AddToClassList("is-hidden");
            }
        }

        // ------------------------------------------------------------- menu

        private void SetMenuVisible(bool on, float delay = 0f)
        {
            MenuTarget = on ? 1f : 0f;

            if (on)
            {
                Main.style.visibility = Visibility.Visible;
            }
            else
            {
                Main.AddToClassList("is-behind");
            }

            for (var i = 0; i < _menuParts.Count; i++)
            {
                var part = _menuParts[i];
                var ord = on ? part.Order : _menuMaxOrder - part.Order;
                part.Target = on ? 1f : 0f;
                part.Delay = delay + ord * (on ? AppearStagger : DissolveStagger);
            }

            for (var i = 0; i < _menuParts.Count; i++)
            {
                var part = _menuParts[i];
                var ord = on ? part.Order : _menuMaxOrder - part.Order;
                var wait = delay + ord * (on ? AppearStagger : DissolveStagger);
                var captured = part;
                Queue(() =>
                {
                    var rect = RectOf(captured.El);
                    if (rect == null) return;

                    var isButton = captured.El.ClassListContains("pc-btn");
                    var baseCount = isButton ? 10 : 8;
                    var dir = WindOf(captured.El);

                    if (on)
                    {
                        Field.Gather(rect.Value, PuffCount(baseCount), PuffStrength(), WindSpread(), 640f, 0f, dir);
                    }
                    else
                    {
                        Field.Disperse(rect.Value, PuffCount(baseCount), PuffStrength(), WindSpread(), 660f, 0f, dir);
                    }
                }, wait * 1000f + 16f);
            }
        }

        private void BootSmoke()
        {
            for (var i = 0; i < _menuParts.Count; i++)
            {
                var part = _menuParts[i];
                part.A = 0f;
                part.Written = -1f;
                part.Target = 1f;
                part.Delay = 0.12f + part.Order * AppearStagger;
                WritePart(part);
            }

            MenuTarget = 1f;
            Main.style.visibility = Visibility.Visible;
            Main.AddToClassList("is-behind");

            for (var i = 0; i < _menuParts.Count; i++)
            {
                var part = _menuParts[i];
                var captured = part;
                Queue(() =>
                {
                    var rect = RectOf(captured.El);
                    if (rect == null) return;
                    var isButton = captured.El.ClassListContains("pc-btn");
                    Field.Gather(rect.Value, PuffCount(isButton ? 9 : 7), PuffStrength() * 0.6f,
                        WindSpread(), 720f, 0f, WindOf(captured.El));
                }, 130f + part.Order * AppearStagger * 1000f);
            }
        }

        // ----------------------------------------------------------- buttons

        public void HoverButton(string action) => ButtonEnter(action, 1f, 0.6f);

        public void FocusButton(string action) => ButtonEnter(action, 0.85f, 0f);

        private void ButtonEnter(string action, float gustScale, float gatherStrength)
        {
            var index = IndexOf(action);
            if (index < 0) return;

            var button = NavButtons[index];
            Hovered = action;
            button.AddToClassList("is-hover");

            var wind = StartWind(button, GustHover * gustScale, WindHoverMs);

            if (gatherStrength <= 0f) return;
            var rect = RectOf(button);
            if (rect == null) return;

            WindDir = wind.Dir;
            Field.Gather(rect.Value, PuffCount(6), PuffStrength() * gatherStrength,
                WindSpread() * 0.9f, 520f, 0f, WindDir);
        }

        public void UnhoverButton(string action)
        {
            var index = IndexOf(action);
            if (index < 0) return;

            NavButtons[index].RemoveFromClassList("is-hover");
            if (Hovered == action) Hovered = null;
        }

        /// <summary>The press: the whole menu comes apart, then the next screen condenses.</summary>
        public void PressButton(string action)
        {
            var index = IndexOf(action);
            if (index < 0) return;

            Press(index);
        }

        private void Press(int index)
        {
            if (Busy) return;

            Busy = true;

            var button = NavButtons[index];
            var def = Buttons[index];
            var wind = StartWind(button, GustPress, WindPressMs);
            button.AddToClassList("is-clearing");

            BlowMenuTexts(wind);

            var rect = RectOf(button);
            if (rect != null)
            {
                Field.Disperse(rect.Value, PuffCount(13), PuffStrength() * 1.1f, WindSpread(), 560f, 0f, WindDir);
            }

            // the next screen is pulled out of the same smoke immediately: the
            // letters are still in the air while it condenses, no dead beat
            Queue(() =>
            {
                button.RemoveFromClassList("is-clearing");
                Busy = false;

                if (def.Action == "settings")
                {
                    OpenSettings();
                    return;
                }

                if (def.Action == "connect" && OnConnectRequested != null)
                {
                    OnConnectRequested();
                    return;
                }

                OnAction?.Invoke(def.Action, def.Label);
            }, PressMs);
        }

        private int IndexOf(string action)
        {
            for (var i = 0; i < Buttons.Length; i++)
            {
                if (Buttons[i].Action == action) return i;
            }

            return -1;
        }

        // ------------------------------------------------------ settings

        public void OpenSettings(bool focus = true)
        {
            if (SettingsOpen)
            {
                if (focus) TabButtons[ActiveTab].Focus();
                return;
            }

            SettingsOpen = true;
            View = "settings";

            Settings.RemoveFromClassList("is-hidden");
            Settings.RemoveFromClassList("is-closing");
            Settings.AddToClassList("is-open");
            Settings.pickingMode = PickingMode.Position;
            Blower.RestoreWithin(Settings);

            _cardPart.Target = 1f;
            _cardPart.Delay = 0.06f;

            SetMenuVisible(false);

            TabDir = 1;
            TabA = 0.08f;
            TabPhase = "in";
            ApplyTab(ActiveTab);

            StartWind(Settings, GustOpen, WindOpenMs);

            var rect = RectOf(Settings);
            if (rect != null)
            {
                Field.Gather(rect.Value, PuffCount(22), PuffStrength() * 1.1f, WindSpread() * 1.05f,
                    760f, 0f, WindOf(Settings));
            }

            Queue(() =>
            {
                var cardRect = RectOf(Card);
                if (cardRect == null || !SettingsOpen) return;
                Field.Gather(cardRect.Value, PuffCount(12), PuffStrength(), WindSpread() * 0.8f,
                    560f, 0f, WindOf(Settings));
            }, 170f);

            if (focus) TabButtons[ActiveTab].Focus();
        }

        public void CloseSettings(bool focus = true, bool blow = true)
        {
            if (!SettingsOpen) return;

            SettingsOpen = false;
            View = "menu";

            var wind = blow
                ? StartWind(Settings, GustClose, WindCloseMs)
                : new Wind
                {
                    Dir = WindOf(Settings),
                    DurationMs = Mathf.Max(200f, WindCloseMs / DissolveSpeedFactor()),
                };

            BlowCardTexts(wind.Dir, wind.DurationMs);

            Settings.RemoveFromClassList("is-open");
            Settings.AddToClassList("is-closing");
            Settings.pickingMode = PickingMode.Ignore;
            _cardPart.Target = 0f;
            _cardPart.Delay = 0f;

            TabPhase = "idle";
            TabOut = 0f;
            TabA = 1f;

            var rect = RectOf(Settings);
            if (rect != null)
            {
                Field.Disperse(rect.Value, PuffCount(24), PuffStrength() * 1.05f, WindSpread() * 1.1f,
                    720f, 0f, wind.Dir);
            }

            // the menu gathers back out of the smoke the window leaves behind
            SetMenuVisible(true, 0.18f);

            if (!focus) return;

            var index = IndexOf("settings");
            if (index < 0) return;

            _suppressFocusBlow = true;
            try
            {
                NavButtons[index].Focus();
            }
            finally
            {
                _suppressFocusBlow = false;
            }
        }

        private void OnClosePressed()
        {
            CloseSettings(true, true);
        }

        // ------------------------------------------------------------- tabs

        private void MarkTabButtons(int active)
        {
            for (var i = 0; i < TabButtons.Length; i++)
            {
                TabButtons[i].EnableInClassList("is-active", i == active);
            }
        }

        private void ApplyTab(int index)
        {
            MarkTabButtons(index);

            for (var i = 0; i < Panels.Length; i++)
            {
                var on = i == index;
                Panels[i].EnableInClassList("is-hidden", !on);
                Panels[i].EnableInClassList("is-active", on);
            }

            ActiveTab = index;
            Tab = Tabs[index].Id;
            WritePanel(TabA);
        }

        private void WritePanel(float a)
        {
            var index = ActiveTab;
            if (index < 0 || index >= Panels.Length) return;

            var panel = Panels[index];
            panel.style.opacity = a;

            // translate3d((1 - a) * --sx, 0, 0); --sx is +20 / -20 px by direction
            var sx = (TabDir > 0 ? 20f : -20f) * (1f - a);
            panel.style.translate = new Translate(Length.Pixels(sx), 0f);

            // Dynamic filters abort the panel repaint in this editor — see
            // ProjectCMainMenuTextBlower.BlurFiltersEnabled.
            if (!ProjectCMainMenuTextBlower.BlurFiltersEnabled) return;

            if (a < 0.985f)
            {
                var blur = new FilterFunction(FilterFunctionType.Blur);
                blur.AddParameter(new FilterParameter((1f - a) * 6f));
                panel.style.filter = new StyleList<FilterFunction>(new List<FilterFunction> { blur });
            }
            else
            {
                panel.style.filter = StyleKeyword.Null;
            }
        }

        /// <summary>Two-phase smoke switch: the old content leaves, the new one is drawn back.</summary>
        public void RequestTab(int index, bool focus = false, bool blow = true, bool instant = false,
            bool force = false)
        {
            var total = Tabs.Length;
            var i = ((index % total) + total) % total;
            var previous = ActiveTab;
            TabDir = i == previous ? 1 : i > previous ? 1 : -1;

            if (instant)
            {
                PendingTab = i;
                TabPhase = "idle";
                TabA = 1f;
                ApplyTab(i);
                if (focus) TabButtons[i].Focus();
                return;
            }

            if (i == ActiveTab && !force && TabPhase == "idle")
            {
                if (focus) TabButtons[i].Focus();
                return;
            }

            // the underline leads, the smoke follows: one wind, one direction,
            // one window for the gust and for the letters of the outgoing panel
            var ms = Mathf.Max(200f, WindTabMs / DissolveSpeedFactor());
            MarkTabButtons(i);
            if (focus) TabButtons[i].Focus();

            WindDir = -TabDir;
            WindDuration = ms;
            WindSeq++;
            TabSeq++;

            if (blow)
            {
                EmitBlow(new Vector2(0.5f, 0.42f), new Vector2(WindDir, -0.14f), GustTab, ms);
            }

            var rect = RectOf(PanelsHost);
            BlowPanelTexts(Panels[ActiveTab], WindDir, ms);
            if (rect != null)
            {
                Field.Disperse(rect.Value, PuffCount(11), PuffStrength() * 0.95f, WindSpread(),
                    480f, 0f, WindDir);
            }

            PendingTab = i;
            TabOut = 0f;
            TabPhase = "out";
        }

        private void TickTabs(float step)
        {
            if (TabPhase == "out")
            {
                // the outgoing letters get a moment in the air before the panel
                // itself sinks into the smoke and the new one is gathered in place
                TabOut += step;
                if (TabOut > 0.26f) TabA = ProjectCMainMenuPalette.Damp(TabA, 0f, 16f, step);

                if (TabA <= 0.14f)
                {
                    ApplyTab(PendingTab);
                    TabA = 0.05f;
                    TabPhase = "in";

                    var rect = RectOf(PanelsHost);
                    if (rect != null)
                    {
                        Field.Gather(rect.Value, PuffCount(16), PuffStrength() * 1.05f, WindSpread(),
                            540f, 0f, WindDir != 0 ? WindDir : -TabDir);
                    }
                }
            }
            else if (TabPhase == "in")
            {
                TabA = ProjectCMainMenuPalette.Damp(TabA, 1f, 7.5f, step);
                if (TabA > 0.985f)
                {
                    TabA = 1f;
                    TabPhase = "idle";
                }
            }

            WritePanel(TabA);
        }

        // ------------------------------------------------- letters into smoke

        private float DissolveSpeedFactor()
        {
            var value = DissolveSpeed <= 0f ? 1f : DissolveSpeed;
            return ProjectCMainMenuPalette.Clamp(value, 0.3f, 3f);
        }

        private int BlowTextOf(VisualElement host, int dir, float duration, float stagger, float strength,
            int puffs, bool whole = false, float delay = 0f)
        {
            if (host == null || Blower == null) return 0;

            var sign = dir < 0 ? -1 : 1;
            var n = Blower.Blow(
                host,
                dir: sign,
                stagger: stagger,
                duration: duration,
                strength: 0.55f + 0.55f * ProjectCMainMenuPalette.Clamp(strength, 0f, 1.6f),
                puffs: puffs,
                whole: whole,
                delay: delay,
                onPuff: (rect, i) => Field?.Disperse(rect, i == 0 ? 3 : 1,
                    PuffStrength() * 0.85f, WindSpread() * 1.05f, 680f, 0f, sign));

            if (n > 0) Blows++;
            return n;
        }

        private void BlowMenuTexts(Wind wind)
        {
            var dir = wind.Dir < 0 ? -1 : 1;
            var duration = wind.DurationMs > 0f ? wind.DurationMs : WindPressMs;

            BlowTextOf(Wordmark, dir, duration, 22f, 1f, 2, whole: true);
            BlowTextOf(Subtitle, dir, duration, 12f, 1f, 1);

            for (var i = 0; i < NavButtonLabels.Length; i++)
            {
                BlowTextOf(NavButtonLabels[i], dir, duration, 24f, 1.15f, 2);
            }
        }

        private int BlowCardTexts(int dir, float duration)
        {
            var hosts = new List<VisualElement>(32);
            CollectByClasses(Settings, CardTextClasses, hosts, 26);

            for (var i = 0; i < hosts.Count; i++)
            {
                BlowTextOf(hosts[i], dir, duration, 14f, 1f, i < 6 ? 1 : 0, delay: Mathf.Min(i * 10f, 90f));
            }

            return hosts.Count;
        }

        private int BlowPanelTexts(VisualElement panel, int dir, float duration)
        {
            if (panel == null) return 0;

            var hosts = new List<VisualElement>(16);
            CollectByClasses(panel, PanelTextClasses, hosts, 64);

            for (var i = 0; i < hosts.Count; i++)
            {
                BlowTextOf(hosts[i], dir, duration, 14f, 0.9f, i < 4 ? 1 : 0, delay: Mathf.Min(i * 12f, 80f));
            }

            return hosts.Count;
        }

        /// <summary>querySelectorAll over a class list, in document order, skipping hidden subtrees.</summary>
        private static void CollectByClasses(VisualElement root, string[] classes, List<VisualElement> into,
            int limit)
        {
            if (root == null) return;

            var hierarchy = root.hierarchy;
            for (var i = 0; i < hierarchy.childCount && into.Count < limit; i++)
            {
                var child = hierarchy[i];
                if (child.ClassListContains("is-hidden")) continue;

                for (var c = 0; c < classes.Length; c++)
                {
                    if (!child.ClassListContains(classes[c])) continue;
                    into.Add(child);
                    break;
                }

                CollectByClasses(child, classes, into, limit);
            }
        }

        // ------------------------------------------------------------- gusts

        private Vector2 Viewport()
        {
            var rect = Root != null ? Root.worldBound : new Rect(0f, 0f, 1920f, 1080f);
            var w = rect.width > 1f ? rect.width : 1920f;
            var h = rect.height > 1f ? rect.height : 1080f;
            return new Vector2(w, h);
        }

        private static Rect? RectOf(VisualElement el)
        {
            if (el == null) return null;

            var rect = el.worldBound;
            if (!(rect.width > 2f) || !(rect.height > 2f)) return null;
            return rect;
        }

        private Vector2 CenterOf(VisualElement el)
        {
            var rect = el.worldBound;
            var viewport = Viewport();
            return new Vector2(
                ProjectCMainMenuPalette.Clamp01((rect.x + rect.width * 0.5f) / viewport.x),
                ProjectCMainMenuPalette.Clamp01((rect.y + rect.height * 0.5f) / viewport.y));
        }

        private int WindOf(VisualElement el)
        {
            var rect = RectOf(el);
            if (rect == null) return 1;

            var viewport = Viewport();
            return rect.Value.x + rect.Value.width * 0.5f < viewport.x * 0.5f ? -1 : 1;
        }

        private void EmitBlow(Vector2 center, Vector2 dir, float strength, float durationMs)
        {
            var length = Mathf.Sqrt(dir.x * dir.x + dir.y * dir.y);
            if (length < 1e-6f) length = 1f;

            var payload = new Gust
            {
                X = ProjectCMainMenuPalette.Clamp01(center.x),
                Y = ProjectCMainMenuPalette.Clamp01(center.y),
                DirX = dir.x / length,
                DirY = dir.y / length,
                Strength = ProjectCMainMenuPalette.Clamp(strength, 0f, 1f),
                DurationMs = Mathf.Max(160f, durationMs),
            };

            LastBlow = payload;
            OnBlow?.Invoke(payload);
        }

        private Wind StartWind(VisualElement el, float strength, float durationMs)
        {
            var dir = WindOf(el);
            var center = CenterOf(el);
            var ms = Mathf.Max(200f, durationMs / DissolveSpeedFactor());

            EmitBlow(center, new Vector2(dir, -0.12f), strength, ms);

            WindDir = dir;
            WindDuration = ms;
            WindSeq++;

            return new Wind { Dir = dir, DurationMs = ms };
        }

        private float PuffStrength() =>
            ProjectCMainMenuPalette.Clamp(0.55f + 0.45f * ProjectCMainMenuPalette.Clamp(SmokeDensity, 0f, 2f),
                0.4f, 1.35f);

        private int PuffCount(int baseCount)
        {
            var k = ProjectCMainMenuPalette.Clamp(SmokeDensity, 0f, 2f);
            return Mathf.Max(3, Mathf.RoundToInt(baseCount * (0.55f + 0.55f * k)));
        }

        private float WindSpread() =>
            ProjectCMainMenuPalette.Clamp(0.85f + 0.4f * ProjectCMainMenuPalette.Clamp(WindStrength, 0f, 2f),
                0.45f, 1.9f);

        // ------------------------------------------------------------- toast

        public void Notify(string text)
        {
            if (Toast == null) return;

            if (Toast is Label label) label.text = text ?? string.Empty;
            Toast.RemoveFromClassList("is-on");

            // the mod forces a reflow so the transition restarts; UI Toolkit
            // resolves styles once per frame, so the class comes back on the next
            Queue(() =>
            {
                if (Toast != null) Toast.AddToClassList("is-on");
            }, 16f);

            _toastOff?.Pause();
            _toastOff = Ui.schedule.Execute(() =>
            {
                if (Toast != null) Toast.RemoveFromClassList("is-on");
            }).StartingIn(2800L);
        }

        // ------------------------------------------------------------ wiring

        private void WireEvents()
        {
            for (var i = 0; i < NavButtons.Length; i++)
            {
                var index = i;
                var action = Buttons[i].Action;

                var button = NavButtons[i];
                button.RegisterCallback<PointerEnterEvent>(_ => HoverButton(action));
                button.RegisterCallback<PointerLeaveEvent>(_ => UnhoverButton(action));

                button.RegisterCallback<FocusInEvent>(_ =>
                {
                    if (_suppressFocusBlow) return;
                    FocusButton(action);
                });
                button.RegisterCallback<FocusOutEvent>(_ => UnhoverButton(action));

                button.RegisterCallback<PointerUpEvent>(_ => Press(index));
                button.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (!IsActivateKey(evt.keyCode)) return;
                    evt.StopPropagation();
                    Press(index);
                });
            }

            for (var i = 0; i < TabButtons.Length; i++)
            {
                var index = i;
                TabButtons[i].RegisterCallback<PointerUpEvent>(_ => RequestTab(index, true, true));
                TabButtons[i].RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (!IsActivateKey(evt.keyCode)) return;
                    evt.StopPropagation();
                    RequestTab(index, true, true);
                });
            }

            if (CloseButton != null)
            {
                CloseButton.RegisterCallback<PointerUpEvent>(_ => OnClosePressed());
                CloseButton.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (!IsActivateKey(evt.keyCode)) return;
                    evt.StopPropagation();
                    OnClosePressed();
                });
            }

            // the mod's switchRow: click flips aria-checked, and the USS draws the
            // knob and the glow from .is-on (there is no attribute API here)
            WireSwitches(Settings);

            if (Root != null) Root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void WireSwitches(VisualElement root)
        {
            if (root == null) return;

            var switches = root.Query<VisualElement>(className: "pc-switch").ToList();
            for (var i = 0; i < switches.Count; i++)
            {
                var toggle = switches[i];
                toggle.RegisterCallback<PointerUpEvent>(_ => toggle.ToggleInClassList("is-on"));
                toggle.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (!IsActivateKey(evt.keyCode)) return;
                    evt.StopPropagation();
                    toggle.ToggleInClassList("is-on");
                });
            }
        }

        private static bool IsActivateKey(KeyCode key) =>
            key == KeyCode.Return || key == KeyCode.KeypadEnter || key == KeyCode.Space;

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                if (!SettingsOpen) return;
                evt.StopPropagation();
                CloseSettings(true, true);
                return;
            }

            if (!SettingsOpen) return;

            if (evt.keyCode == KeyCode.RightArrow)
            {
                evt.StopPropagation();
                RequestTab(ActiveTab + 1, true, true);
            }
            else if (evt.keyCode == KeyCode.LeftArrow)
            {
                evt.StopPropagation();
                RequestTab(ActiveTab - 1, true, true);
            }
        }

        private void HookPointer()
        {
            if (Root == null) return;

            // the panel root is the DOM's `window`: it sees every pointer move,
            // even where the menu itself is pickingMode = Ignore
            var target = Root.panel != null ? Root.panel.visualTree : Root;
            if (_pointerTarget == target) return;

            if (_pointerTarget != null)
                _pointerTarget.UnregisterCallback<PointerMoveEvent>(OnPointerMove);

            _pointerTarget = target;
            _pointerTarget.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            var viewport = Viewport();
            var x = evt.position.x / viewport.x;
            var y = evt.position.y / viewport.y;

            _targetX = ProjectCMainMenuPalette.Clamp((x - 0.5f) * 2f, -1f, 1f);
            _targetY = ProjectCMainMenuPalette.Clamp((y - 0.5f) * 2f, -1f, 1f);

            // the vertical position of the pointer drives the altimeter and the gas
            PointerX = ProjectCMainMenuPalette.Clamp01(x);
            PointerY = ProjectCMainMenuPalette.Clamp01(y);
        }

        // ------------------------------------------------------------- update

        private void AutoTickFrame()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var dt = _lastTickClock > 0d ? (float)(now - _lastTickClock) : 1f / 60f;
            _lastTickClock = now;
            Update(dt);
        }

        /// <summary>One frame of ui.js update(dt). Safe to call by hand when AutoTick is off.</summary>
        public void Update(float dt)
        {
            if (!_started) Start();

            var step = Mathf.Clamp(dt, 0f, 0.1f);

            // ---- title parallax ------------------------------------------
            _curX = ProjectCMainMenuPalette.Damp(_curX, _targetX, 5.5f, step);
            _curY = ProjectCMainMenuPalette.Damp(_curY, _targetY, 5.5f, step);

            var tx = -_curX * 7f;
            var ty = -_curY * 5f;
            if (Mathf.Abs(tx - _appliedX) > 0.04f || Mathf.Abs(ty - _appliedY) > 0.04f)
            {
                _appliedX = tx;
                _appliedY = ty;
                TitleBlock.style.translate = new Translate(Length.Pixels(tx), Length.Pixels(ty));
            }

            TickParts(step);
            TickTabs(step);

            // ---- altimeter ------------------------------------------------
            var hoveredIndex = IndexOf(Hovered ?? string.Empty);
            Altimeter?.Update(step, PointerY, hoveredIndex >= 0 ? Buttons[hoveredIndex].Alt : (int?)null);

            if (_exitTick > 0)
            {
                _exitTick--;
            }
            else
            {
                _exitTick = 15;
                var quitIndex = IndexOf("quit");
                if (quitIndex >= 0)
                {
                    var rect = NavButtons[quitIndex].worldBound;
                    var h = Mathf.Max(1f, Viewport().y);
                    if (rect.height > 1f)
                    {
                        ExitY = ProjectCMainMenuPalette.Clamp(1f - (rect.y + rect.height * 0.5f) / h, 0.05f, 0.6f);
                    }
                }
            }

            if (Field != null)
            {
                Puffs = Field.Count();
                Flow = Field.GetFlow();
            }

            if (Blower != null) Ghosts = Blower.Count();

            HookPointer();
        }

        /// <summary>ui.js setView('settings' | 'menu').</summary>
        public string SetView(string name)
        {
            if (name == "settings")
            {
                OpenSettings();
                return View;
            }

            if (name == "menu")
            {
                CloseSettings(false, true);
                View = "menu";
                return View;
            }

            return View;
        }

        // ------------------------------------------------------------ helpers

        private void Queue(Action action, float delayMs)
        {
            if (Ui == null) return;

            for (var i = _timers.Count - 1; i >= 0; i--)
            {
                if (!_timers[i].isActive) _timers.RemoveAt(i);
            }

            var item = Ui.schedule.Execute(action).StartingIn((long)Mathf.Max(0f, delayMs));
            _timers.Add(item);
        }

        public void Dispose()
        {
            _autoTickItem?.Pause();
            _autoTickItem = null;

            for (var i = 0; i < _timers.Count; i++) _timers[i]?.Pause();
            _timers.Clear();

            _toastOff?.Pause();
            _toastOff = null;

            if (_pointerTarget != null)
            {
                _pointerTarget.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                _pointerTarget = null;
            }

            if (Root != null) Root.UnregisterCallback<KeyDownEvent>(OnKeyDown);

            Field?.Clear();
            Blower?.Clear();
            Field?.Node?.RemoveFromHierarchy();
            Blower?.Node?.RemoveFromHierarchy();

            Altimeter?.Dispose();

            // the project layer is a sibling of .pc-ui, so it is not carried away with it
            DisposeProjectPanels();

            Ui?.RemoveFromHierarchy();
            Root?.RemoveFromClassList("pc-ui-root");

            SettingsOpen = false;
            Hovered = null;
            Busy = false;
            _started = false;
        }

        // ----------------------------------------------------------- snapshot

        /// <summary>
        /// The window's numbers for tests and journals. Floats are formatted with the
        /// invariant culture so a snapshot reads the same on a ru-RU machine.
        /// </summary>
        public WindowSnapshot TakeSnapshot()
        {
            var parts = new List<PartSnapshot>(_parts.Count);
            var items = new List<string>(_parts.Count);
            for (var i = 0; i < _parts.Count; i++)
            {
                var part = _parts[i];
                items.Add(string.Format(CultureInfo.InvariantCulture, "{0}={1:F3}/{2:F1}/d{3:F3}",
                    part.Name, part.A, part.Target, part.Delay));
                parts.Add(new PartSnapshot
                {
                    Name = part.Name,
                    A = Mathf.Round(part.A * 1000f) / 1000f,
                    Target = part.Target,
                    Delay = Mathf.Round(part.Delay * 1000f) / 1000f,
                    Forming = part.Soft,
                    Opacity = Mathf.Round(part.El.resolvedStyle.opacity * 1000f) / 1000f,
                    BlurPx = Mathf.Round(part.LastBlur * 100f) / 100f,
                    CloudOpacity = part.LastCloudOpacity < 0f
                        ? -1f
                        : Mathf.Round(part.LastCloudOpacity * 1000f) / 1000f,
                });
            }

            var activePanel = Panels != null && ActiveTab < Panels.Length ? Panels[ActiveTab].name : string.Empty;

            return new WindowSnapshot
            {
                View = View,
                SettingsOpen = SettingsOpen,
                ActiveTab = ActiveTab,
                Tab = Tab,
                Hovered = Hovered,
                Busy = Busy,
                TabPhase = TabPhase,
                MenuA = Mathf.Round(MenuA * 1000f) / 1000f,
                CardA = Mathf.Round(CardA * 1000f) / 1000f,
                TabA = Mathf.Round(TabA * 1000f) / 1000f,
                MenuTarget = MenuTarget,
                PendingTab = PendingTab,
                TabDir = TabDir,
                WindDir = WindDir,
                WindDuration = Mathf.Round(WindDuration),
                WindSeq = WindSeq,
                TabSeq = TabSeq,
                PointerX = Mathf.Round(PointerX * 1000f) / 1000f,
                PointerY = Mathf.Round(PointerY * 1000f) / 1000f,
                ExitY = Mathf.Round(ExitY * 1000f) / 1000f,
                Puffs = Puffs,
                Ghosts = Ghosts,
                Blows = Blows,
                LastBlow = LastBlow?.ToString() ?? "—",
                MainVisibility = Main.resolvedStyle.visibility.ToString(),
                MainBehind = Main.ClassListContains("is-behind"),
                SettingsHidden = Settings.ClassListContains("is-hidden"),
                ScrimOpacity = MainScrim.resolvedStyle.opacity.ToString("F3", CultureInfo.InvariantCulture),
                AltOpacity = AltHost.resolvedStyle.opacity.ToString("F3", CultureInfo.InvariantCulture),
                Parts = parts,
                ActivePanel = activePanel,
                Items = string.Join(" | ", items),
            };
        }
    }
}
