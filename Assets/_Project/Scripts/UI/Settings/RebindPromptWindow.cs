// Project C: Input System — Phase 2.4
// RebindPromptWindow: модальное окно-подсказка во время rebind.
// "Нажмите клавишу для переназначения"
//
// Логика:
// - EscMenu → [НАСТРОЙКИ] → клик на строку → открывается RebindPromptWindow
// - Слушаем ввод в KeybindingsWindow.Update
// - Клавиша нажата → rebind → закрываем окно
// - Esc → отмена rebind → закрываем окно

using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.UI;
using ProjectC.Localization;

namespace ProjectC.UI.Settings
{
    [RequireComponent(typeof(UIDocument))]
    public class RebindPromptWindow : MonoBehaviour
    {
        public static RebindPromptWindow Instance { get; private set; }

        [SerializeField] private VisualTreeAsset promptUxml;
        [SerializeField] private StyleSheet promptUss;

        private UIDocument _doc;
        private VisualElement _root;
        private Label _titleLabel;
        private Label _hintLabel;
        
        private bool _isLocaleSubscribed;
        private string _currentActionName;
        private bool _currentIsSkill;
private bool _built = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _doc = GetComponent<UIDocument>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

private void OnDisable()
        {
            UnsubscribeLocale();
        }

        private void SubscribeLocale()
        {
            if (_isLocaleSubscribed) return;
            Loc.OnLocaleChanged += HandleLocaleChanged;
            _isLocaleSubscribed = true;
            if (_built) LocalizePromptTexts();
        }

        private void UnsubscribeLocale()
        {
            if (!_isLocaleSubscribed) return;
            Loc.OnLocaleChanged -= HandleLocaleChanged;
            _isLocaleSubscribed = false;
        }

        private void HandleLocaleChanged()
        {
            if (!_built || _root == null) return;
            LocalizePromptTexts();
            _root.MarkDirtyRepaint();
        }

        private void LocalizePromptTexts()
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = string.IsNullOrEmpty(_currentActionName)
                    ? Loc.Get("ui.rebind.title")
                    : (_currentIsSkill
                        ? Loc.Get("ui.keybindings.rebind_skill_title", "Remap skill:")
                        : Loc.Get("ui.keybindings.rebind_key_title", "Remap key:"));
            }
            if (_hintLabel != null)
            {
                _hintLabel.text = string.IsNullOrEmpty(_currentActionName)
                    ? Loc.Get("ui.rebind.hint")
                    : Loc.Format("ui.keybindings.rebind_hint", _currentActionName);
            }
            var cancelLabel = _root.Q<Label>("prompt-cancel");
            if (cancelLabel != null) cancelLabel.text = Loc.Get("ui.rebind.cancel_hint");
        }

private void OnEnable()
        {
            EnsureBuilt();
            SubscribeLocale();
        }
        private void Start() { EnsureBuilt(); }

        public void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null || _doc.rootVisualElement == null) return;

            // CharacterWindow pattern
            if (promptUxml == null) promptUxml = Resources.Load<VisualTreeAsset>("UI/RebindPromptWindow");
            if (promptUss  == null) promptUss  = Resources.Load<StyleSheet>("UI/RebindPromptStyles");
            if (promptUxml == null) { Debug.LogError("[RebindPrompt] UXML not found"); return; }

            _doc.rootVisualElement.Clear();
            if (promptUss != null) _doc.rootVisualElement.styleSheets.Add(promptUss);
            _root = promptUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            _titleLabel = _root.Q<Label>("prompt-title");
            _hintLabel = _root.Q<Label>("prompt-hint");
            // Localize UXML text
            if (_titleLabel != null) _titleLabel.text = Loc.Get("ui.rebind.title");
            if (_hintLabel != null) _hintLabel.text = Loc.Get("ui.rebind.hint");
            var cancelLabel = _root.Q<Label>("prompt-cancel");
            if (cancelLabel != null) cancelLabel.text = Loc.Get("ui.rebind.cancel_hint");
            _built = true;
            SetVisible(false);
            Debug.Log($"[RebindPrompt] Built. uxml={promptUxml.name} uss={(promptUss != null ? promptUss.name : "null")}");
        }

        public static void Show(string actionName, bool isSkill = false)
        {
            if (Instance == null)
            {
                EnsureExists();
            }
            Instance?.ShowInternal(actionName, isSkill);
        }

        private static void EnsureExists()
        {
            var existing = Object.FindObjectsByType<RebindPromptWindow>(FindObjectsInactive.Include);
            if (existing != null && existing.Length > 0) return;
            var go = new GameObject("[RebindPromptWindow]");
            DontDestroyOnLoad(go);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UI/RebindPromptPanelSettings");
            go.AddComponent<RebindPromptWindow>();
        }

public void ShowInternal(string actionName, bool isSkill)
        {
            if (!_built) EnsureBuilt();
            _currentActionName = actionName ?? "";
            _currentIsSkill = isSkill;
            LocalizePromptTexts();
            SetVisible(true);
        }

        public static void Hide()
        {
            Instance?.SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_root == null) return;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _root.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        }

        public bool IsOpen() => _built && _root != null && _root.style.display.value == DisplayStyle.Flex;
    }
}
