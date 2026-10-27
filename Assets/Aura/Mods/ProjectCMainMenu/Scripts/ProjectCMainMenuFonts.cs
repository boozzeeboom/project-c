// ProjectC Main Menu — the two faces the menu is set in.
//
// The web mod declared no font at all (there is no font-family and no --pc-font in
// .Aura/Mods/projectc-main-menu), so the port had nothing to carry over and the menu
// fell back to the engine's default UI Toolkit face. The faces are project assets, so
// they are referenced from an asset rather than from code: this ScriptableObject lives
// in a Resources folder and holds the two Font assets, which keeps the reference alive
// in a player build without a scene or prefab edit.
//
//   Body    — every label in the menu;
//   Display — the PROJECT / C wordmark only.
using UnityEngine;

namespace ProjectCMainMenu
{
    [CreateAssetMenu(menuName = "ProjectC/Main Menu Fonts", fileName = "PCMM_Fonts")]
    public sealed class ProjectCMainMenuFonts : ScriptableObject
    {
        [Tooltip("Every label of the menu.")]
        public Font Body;

        [Tooltip("The PROJECT / C wordmark only.")]
        public Font Display;

        private const string ResourcePath = "PCMM_Fonts";

        private static ProjectCMainMenuFonts _cached;

        /// <summary>The menu's faces, or null when the asset has not been created.</summary>
        public static ProjectCMainMenuFonts Load()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<ProjectCMainMenuFonts>(ResourcePath);
            return _cached;
        }
    }
}
