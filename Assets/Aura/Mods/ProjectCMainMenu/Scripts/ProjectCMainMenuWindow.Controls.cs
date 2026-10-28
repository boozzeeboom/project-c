// ProjectC Main Menu — the settings controls' interaction and state visuals.
//
// The mod gets these for free from the browser: <input type=range> drags and places its
// thumb, a button flips aria-checked and the stylesheet swaps the switch background,
// :hover / :focus-visible swap the select's gradient and grow its box-shadow. UI Toolkit
// has no styleable equivalents, and an inline backgroundImage (which is how every ramp
// reaches an element) always beats a USS rule, so the state swaps live here:
//
//   .pc-range  — thumb + glow placed at the value (range.userData, 0-100), pointer drag
//                with capture, ←/→ and Home/End; the .pc-out label follows;
//   .pc-switch — SwitchOn / SwitchOff background with the .is-on class;
//   .pc-select — SelectHover + glow on hover / focus, click / Enter / → steps forward,
//                ← steps back through the options kept in userData;
//   .pc-close  — CloseHaloHover on hover / focus.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    public sealed partial class ProjectCMainMenuWindow
    {
        private const float RangeKeyStep = 5f;

        private void WireControls()
        {
            if (Settings == null) return;

            WireSwitches(Settings);
            WireRanges(Settings);
            WireSelects(Settings);
            WireClose();
        }

        // ---- switches ---------------------------------------------------------

        private void WireSwitches(VisualElement root)
        {
            var switches = Snapshot(root, "pc-switch");
            for (var i = 0; i < switches.Length; i++)
            {
                var toggle = switches[i];
                RefreshSwitch(toggle);

                toggle.RegisterCallback<PointerUpEvent>(_ => FlipSwitch(toggle));
                toggle.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (!IsActivateKey(evt.keyCode)) return;
                    evt.StopPropagation();
                    FlipSwitch(toggle);
                });
            }
        }

        private static void FlipSwitch(VisualElement toggle)
        {
            toggle.ToggleInClassList("is-on");
            RefreshSwitch(toggle);
        }

        private static void RefreshSwitch(VisualElement toggle)
        {
            var on = toggle.ClassListContains("is-on");
            toggle.style.backgroundImage = new StyleBackground(
                on ? ProjectCMainMenuRamps.SwitchOn() : ProjectCMainMenuRamps.SwitchOff());
        }

        // ---- ranges -----------------------------------------------------------

        private void WireRanges(VisualElement root)
        {
            var ranges = Snapshot(root, "pc-range");
            for (var i = 0; i < ranges.Length; i++)
            {
                var range = ranges[i];
                PlaceRange(range, RangeValue(range));

                range.RegisterCallback<PointerDownEvent>(evt =>
                {
                    range.CapturePointer(evt.pointerId);
                    SetRangeFromPointer(range, evt.position);
                    evt.StopPropagation();
                });
                range.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!range.HasPointerCapture(evt.pointerId)) return;
                    SetRangeFromPointer(range, evt.position);
                });
                range.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (range.HasPointerCapture(evt.pointerId)) range.ReleasePointer(evt.pointerId);
                });
                range.RegisterCallback<KeyDownEvent>(evt =>
                {
                    var value = RangeValue(range);
                    switch (evt.keyCode)
                    {
                        case KeyCode.LeftArrow:
                        case KeyCode.DownArrow:
                            value -= RangeKeyStep;
                            break;
                        case KeyCode.RightArrow:
                        case KeyCode.UpArrow:
                            value += RangeKeyStep;
                            break;
                        case KeyCode.Home:
                            value = 0f;
                            break;
                        case KeyCode.End:
                            value = 100f;
                            break;
                        default:
                            return;
                    }

                    // the arrows also switch tabs at the window level; a focused slider owns them
                    evt.StopPropagation();
                    PlaceRange(range, value);
                });
            }
        }

        private static float RangeValue(VisualElement range) =>
            range.userData is float f ? f : 0f;

        private static void SetRangeFromPointer(VisualElement range, Vector3 panelPosition)
        {
            var width = range.layout.width;
            if (float.IsNaN(width) || width <= 1f) return;

            var local = range.WorldToLocal(panelPosition);
            PlaceRange(range, Mathf.Round(Mathf.Clamp01(local.x / width) * 100f));
        }

        private static void PlaceRange(VisualElement range, float value)
        {
            value = Mathf.Clamp(value, 0f, 100f);
            range.userData = value;

            var left = new StyleLength(Length.Percent(value));
            var hierarchy = range.hierarchy;
            for (var i = 0; i < hierarchy.childCount; i++)
            {
                var child = hierarchy[i];
                if (child.ClassListContains("pc-range-thumb") || child.ClassListContains("pc-range-thumb__glow"))
                {
                    child.style.left = left;
                }
            }

            var output = range.parent?.Q<Label>(className: "pc-out");
            if (output != null) output.text = Mathf.RoundToInt(value) + "%";
        }

        // ---- selects ----------------------------------------------------------

        private void WireSelects(VisualElement root)
        {
            var selects = SnapshotLabels(root, "pc-select");
            for (var i = 0; i < selects.Length; i++)
            {
                var select = selects[i];
                var glow = select.parent?.Q(className: "pc-select__glow");
                if (glow != null) glow.style.backgroundImage = new StyleBackground(ProjectCMainMenuRamps.SelectGlow());

                var state = new SelectState();
                void Refresh()
                {
                    var lit = state.Hover || state.Focus;
                    select.style.backgroundImage = new StyleBackground(
                        lit ? ProjectCMainMenuRamps.SelectHover() : ProjectCMainMenuRamps.SelectIdle());
                    if (glow != null) glow.style.opacity = lit ? 1f : 0f;
                }

                Refresh();

                select.RegisterCallback<PointerEnterEvent>(_ => { state.Hover = true; Refresh(); });
                select.RegisterCallback<PointerLeaveEvent>(_ => { state.Hover = false; Refresh(); });
                select.RegisterCallback<FocusInEvent>(_ => { state.Focus = true; Refresh(); });
                select.RegisterCallback<FocusOutEvent>(_ => { state.Focus = false; Refresh(); });
                select.RegisterCallback<PointerUpEvent>(_ => StepSelect(select, 1));
                select.RegisterCallback<KeyDownEvent>(evt =>
                {
                    int step;
                    if (IsActivateKey(evt.keyCode) || evt.keyCode == KeyCode.RightArrow || evt.keyCode == KeyCode.DownArrow) step = 1;
                    else if (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.UpArrow) step = -1;
                    else return;

                    evt.StopPropagation();
                    StepSelect(select, step);
                });
            }
        }

        private sealed class SelectState
        {
            public bool Hover;
            public bool Focus;
        }

        private static void StepSelect(Label select, int step)
        {
            if (!(select.userData is string[] options) || options.Length == 0) return;

            var index = System.Array.IndexOf(options, select.text);
            if (index < 0) index = 0;
            index = (index + step + options.Length) % options.Length;
            select.text = options[index];
        }

        // ---- close ------------------------------------------------------------

        private void WireClose()
        {
            if (CloseButton == null) return;

            var hover = false;
            var focus = false;
            void Refresh()
            {
                CloseButton.style.backgroundImage = new StyleBackground(
                    hover || focus ? ProjectCMainMenuRamps.CloseHaloHover() : ProjectCMainMenuRamps.CloseHalo());
            }

            CloseButton.RegisterCallback<PointerEnterEvent>(_ => { hover = true; Refresh(); });
            CloseButton.RegisterCallback<PointerLeaveEvent>(_ => { hover = false; Refresh(); });
            CloseButton.RegisterCallback<FocusInEvent>(_ => { focus = true; Refresh(); });
            CloseButton.RegisterCallback<FocusOutEvent>(_ => { focus = false; Refresh(); });
        }

        // ---- helpers ----------------------------------------------------------

        /// <summary>CollectByClass reuses one scratch list; the wiring loops need their own copy.</summary>
        private Label[] SnapshotLabels(VisualElement root, string className)
        {
            var found = CollectByClass(root, className);
            var list = new List<Label>(found.Count);
            for (var i = 0; i < found.Count; i++)
            {
                if (found[i] is Label label) list.Add(label);
            }

            return list.ToArray();
        }

        private VisualElement[] Snapshot(VisualElement root, string className)
        {
            return CollectByClass(root, className).ToArray();
        }
    }
}
