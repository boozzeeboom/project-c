using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Core.Theming
{
    [CreateAssetMenu(menuName = "Project C/UIv2/Theme Data", fileName = "ThemeData")]
    public class ThemeData : ScriptableObject
    {
        public string themeId = "dark";
        public StyleSheet themeStyleSheet;
        public string[] tokenOverrides;
    }
}
