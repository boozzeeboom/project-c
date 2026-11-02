using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectC.UIv2.Tools.Agent
{
    /// <summary>
    /// Generates UXML/USS scaffold for a new UIv2 screen from a template.
    /// Usage: Project C → UIv2 → Agent → Generate Screen Scaffold
    /// </summary>
    public static class UIv2TemplateGenerator
    {
        [MenuItem("Project C/UIv2/Agent/Generate Screen Scaffold")]
        public static void GenerateScaffold()
        {
            string screenName = "NewScreen";
            if (!EditorUtility.DisplayDialog("Generate Scaffold", "Generate screen scaffold named 'NewScreen'?\nRename folder/files after generation.", "OK", "Cancel")) return;

            string folder = "Assets/_Project/UIv2/Screens/" + screenName;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            // UXML
            string uxml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <ui:VisualElement name=""root"" class=""{screenName.ToLower()}-root"">
        <ui:VisualElement name=""panel"" class=""{screenName.ToLower()}-panel"">
            <ui:VisualElement class=""{screenName.ToLower()}-header"">
                <ui:Label text=""{screenName}"" class=""{screenName.ToLower()}-title"" />
            </ui:VisualElement>
            <ui:VisualElement class=""{screenName.ToLower()}-content"">
                <!-- Content here -->
            </ui:VisualElement>
            <ui:VisualElement class=""{screenName.ToLower()}-footer"">
                <ui:Button text=""OK"" class=""{screenName.ToLower()}-btn-primary"" />
            </ui:VisualElement>
            <ui:Label name=""status"" text=""Status: ready"" class=""{screenName.ToLower()}-status"" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>";
            File.WriteAllText(folder + "/" + screenName + ".uxml", uxml);

            // USS (with DesignTokens variables, no @import)
            string uss = $@"/* {screenName}.uss — generated scaffold. DesignTokens added separately in EnsureBuilt. */

.{screenName.ToLower()}-root {{
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    align-items: center;
    justify-content: center;
    background-color: var(--color-bg-deep);
    display: flex;
}}

.{screenName.ToLower()}-panel {{
    width: 480px;
    max-width: 90%;
    background-color: var(--color-bg-panel);
    border-width: 1px;
    border-color: var(--color-border-accent);
    border-radius: var(--radius-lg);
    padding: var(--space-xl);
    flex-direction: column;
    gap: var(--space-md);
}}

.{screenName.ToLower()}-header {{
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: var(--space-md);
}}

.{screenName.ToLower()}-title {{
    font-size: var(--font-size-xxl);
    color: var(--color-text-primary);
    -unity-font-style: bold;
}}

.{screenName.ToLower()}-content {{
    flex-grow: 1;
    margin-bottom: var(--space-md);
}}

.{screenName.ToLower()}-footer {{
    flex-direction: row;
    justify-content: flex-end;
    gap: var(--space-sm);
}}

.{screenName.ToLower()}-btn-primary {{
    height: 36px;
    padding: 0 var(--space-lg);
    background-color: var(--color-accent);
    color: var(--color-bg-deep);
    border-width: 0;
    border-radius: var(--radius-md);
    font-size: var(--font-size-md);
    -unity-font-style: bold;
}}

.{screenName.ToLower()}-status {{
    font-size: var(--font-size-xs);
    color: var(--color-text-muted);
    padding: var(--space-sm) var(--space-md);
    background-color: var(--color-bg-secondary);
    border-radius: var(--radius-sm);
    border-left-width: 3px;
    border-left-color: var(--color-accent);
}}
";
            File.WriteAllText(folder + "/" + screenName + ".uss", uss);

            // C# Screen
            string cs = $@"using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Screens.{screenName}
{{
    [RequireComponent(typeof(UIDocument))]
    public class {screenName}Screen : MonoBehaviour
    {{
        [SerializeField] private VisualTreeAsset {screenName.ToLower()}Uxml;
        [SerializeField] private StyleSheet {screenName.ToLower()}Uss;
        [SerializeField] private StyleSheet designTokensUss;

        private UIDocument _doc;
        private VisualElement _root;
        private Label _status;
        private {screenName}ViewModel _vm;
        private bool _built = false;

        private void Awake()
        {{
            _doc = GetComponent<UIDocument>();
            _vm = new {screenName}ViewModel();
            if ({screenName.ToLower()}Uxml == null)
                {screenName.ToLower()}Uxml = Resources.Load<VisualTreeAsset>(""UIv2/Screens/{screenName}/{screenName}"");
        }}

        private void OnEnable() => StartCoroutine(BuildWhenReady());
        private void OnDisable() {{ }}

        private IEnumerator BuildWhenReady()
        {{
            while (_doc == null || _doc.rootVisualElement == null)
            {{
                if (_doc == null) _doc = GetComponent<UIDocument>();
                yield return null;
            }}
            EnsureBuilt();
        }}

        public void EnsureBuilt()
        {{
            if (_built) return;
            if (_doc == null || _doc.rootVisualElement == null) return;
            if ({screenName.ToLower()}Uxml == null) {{ Debug.LogError(""[{screenName}Screen] UXML not assigned""); return; }}

            _root = _doc.rootVisualElement;
            if (designTokensUss != null && !_root.styleSheets.Contains(designTokensUss))
                _root.styleSheets.Add(designTokensUss);
            if ({screenName.ToLower()}Uss != null && !_root.styleSheets.Contains({screenName.ToLower()}Uss))
                _root.styleSheets.Add({screenName.ToLower()}Uss);

            _status = _root.Q<Label>(""status"");
            if (_status != null)
                _vm.OnStatusChanged += s => _status.text = ""Status: "" + s;

            _root.style.display = DisplayStyle.Flex;
            _built = true;
            Debug.Log($""[{screenName}Screen] Built. styleSheets={{_root.styleSheets.count}}"");
            StartCoroutine(ProjectC.UIv2.Core.Animation.UIAnimator.FadeIn(_root, 0.25f));
        }}
    }}
}}
";
            File.WriteAllText(folder + "/" + screenName + "Screen.cs", cs);

            // C# ViewModel
            string vm = $@"using System;

namespace ProjectC.UIv2.Screens.{screenName}
{{
    public class {screenName}ViewModel
    {{
        public event Action<string> OnStatusChanged;
        private string _status = ""ready"";
        public string Status => _status;
        public void SetStatus(string value)
        {{
            _status = value;
            OnStatusChanged?.Invoke(value);
        }}
    }}
}}
";
            File.WriteAllText(folder + "/" + screenName + "ViewModel.cs", vm);

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Scaffold Generated",
                $"Created {screenName} screen scaffold in:\n{folder}\n\nUXML, USS, C#, ViewModel generated.", "OK");
        }
    }
}
