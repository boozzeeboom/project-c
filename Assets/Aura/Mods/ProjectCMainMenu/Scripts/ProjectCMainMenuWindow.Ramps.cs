// ProjectC Main Menu — the [C#] half of every gradient the ported stylesheet
// documents but cannot declare.
//
// PCMM_Menu.uss carries a comment on each rule that used a CSS gradient instead of
// the gradient itself (UI Toolkit has neither linear-gradient() nor
// radial-gradient()). ProjectCMainMenuRamps bakes those gradients; this file is
// where they are handed to the elements, once, right after the tree is built.
//
// Assigning here rather than per call site keeps ProjectCMainMenuWindow.cs the plain
// transcription of ui.js's element tree, and keeps the whole ramp inventory in one
// readable list.
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    public sealed partial class ProjectCMainMenuWindow
    {
        private readonly List<VisualElement> _rampScratch = new List<VisualElement>(64);

        private void ApplyRamps()
        {
            ApplyMenuRamps();
            ApplyButtonRamps();
            ApplyCardRamps();
            ApplyControlRamps();
        }

        // ---- the menu column ------------------------------------------------

        private void ApplyMenuRamps()
        {
            // .pc-main__scrim — the dark bank the wordmark and the buttons sit on
            SetRamp(MainScrim, ProjectCMainMenuRamps.MainScrim());

            // .pc-rule — linear-gradient(90deg, --pc-hair-strong, transparent)
            SetRamp(Rule, ProjectCMainMenuRamps.TitleRule());
        }

        // ---- the four menu buttons ------------------------------------------

        private void ApplyButtonRamps()
        {
            for (var i = 0; i < NavButtons.Length; i++)
            {
                var cloud = NavButtons[i].Q(className: "pc-btn-cloud");
                if (cloud == null) continue;

                // the pale wash that gives the button a light background at all
                SetRamp(cloud, ProjectCMainMenuRamps.ButtonHalo());

                var hierarchy = cloud.hierarchy;
                for (var c = 0; c < hierarchy.childCount; c++)
                {
                    var child = hierarchy[c];
                    if (child.ClassListContains("pc-blob"))
                    {
                        // the charcoal body of the cloud, on top of the pale wash
                        SetRamp(child, ProjectCMainMenuSmokeTextures.Cloud);
                    }
                    else if (child.ClassListContains("pc-btn-cloud__rim"))
                    {
                        SetRamp(child, ProjectCMainMenuRamps.ButtonRim());
                    }
                }
            }
        }

        // ---- the settings window --------------------------------------------

        private void ApplyCardRamps()
        {
            if (CardCloud != null)
            {
                var hierarchy = CardCloud.hierarchy;
                for (var c = 0; c < hierarchy.childCount; c++)
                {
                    var child = hierarchy[c];
                    if (child.ClassListContains("pc-cld"))
                    {
                        // the bank is pale: it has to read against the veiled
                        // atmosphere the way the charcoal sheets read against a sky
                        SetRamp(child, ProjectCMainMenuSmokeTextures.Light);
                    }
                    else if (child.ClassListContains("pc-card-cloud__rim"))
                    {
                        SetRamp(child, ProjectCMainMenuRamps.CardRim());
                    }
                }
            }

            // .pc-card__mist — the dark mist the labels are readable in
            SetRamp(CardMist, ProjectCMainMenuRamps.CardMist());

            // the two smoke dividers, dense in the middle and gone at the ends
            SetRamp(FirstOfClass(CardHead, "pc-card-head__rule"), ProjectCMainMenuRamps.Hairline());
            SetRamp(FirstOfClass(TabsBar, "pc-tabs__rule"), ProjectCMainMenuRamps.Hairline());

            // the close button's radial halo
            SetRamp(CloseButton, ProjectCMainMenuRamps.CloseHalo());

            // .pc-tab__glow — the soft smudge under the active tab
            for (var i = 0; i < TabButtons.Length; i++)
            {
                SetRamp(FirstOfClass(TabButtons[i], "pc-tab__glow"), ProjectCMainMenuRamps.TabGlow());
            }
        }

        // ---- the panel controls ---------------------------------------------

        private void ApplyControlRamps()
        {
            if (Settings == null) return;

            // .pc-row — linear-gradient(90deg, --pc-row 0%, transparent 92%)
            var rows = CollectByClass(Settings, "pc-row");
            for (var i = 0; i < rows.Count; i++) SetRamp(rows[i], ProjectCMainMenuRamps.RowWash());

            // .pc-range-track
            var tracks = CollectByClass(Settings, "pc-range-track");
            for (var i = 0; i < tracks.Count; i++) SetRamp(tracks[i], ProjectCMainMenuRamps.RangeTrack());

            // .pc-switch / .pc-select / .pc-kbd — pale pills, not charcoal ones
            var switches = CollectByClass(Settings, "pc-switch");
            for (var i = 0; i < switches.Count; i++) SetRamp(switches[i], ProjectCMainMenuRamps.SoftPill(0.22f));

            var selects = CollectByClass(Settings, "pc-select");
            for (var i = 0; i < selects.Count; i++) SetRamp(selects[i], ProjectCMainMenuRamps.SoftPill(0.14f));

            var keys = CollectByClass(Settings, "pc-kbd");
            for (var i = 0; i < keys.Count; i++) SetRamp(keys[i], ProjectCMainMenuRamps.SoftPill(0.16f));
        }

        // ---- helpers ---------------------------------------------------------

        private static void SetRamp(VisualElement element, UnityEngine.Texture2D texture)
        {
            if (element == null || texture == null) return;
            element.style.backgroundImage = new StyleBackground(texture);
        }

        private static VisualElement FirstOfClass(VisualElement root, string className)
        {
            return root?.Q(className: className);
        }

        /// <summary>
        /// Every element under <paramref name="root"/> carrying <paramref name="className"/>,
        /// including subtrees the window currently hides — the panels of the inactive tabs
        /// are .is-hidden, and they still need their ramps before they are shown.
        /// </summary>
        private List<VisualElement> CollectByClass(VisualElement root, string className)
        {
            _rampScratch.Clear();
            Walk(root, className, _rampScratch);
            return _rampScratch;
        }

        private static void Walk(VisualElement element, string className, List<VisualElement> into)
        {
            if (element == null) return;

            var hierarchy = element.hierarchy;
            for (var i = 0; i < hierarchy.childCount; i++)
            {
                var child = hierarchy[i];
                if (child.ClassListContains(className)) into.Add(child);
                Walk(child, className, into);
            }
        }
    }
}
