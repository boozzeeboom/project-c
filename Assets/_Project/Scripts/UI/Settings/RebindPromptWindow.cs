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
        private bool _built = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _doc = GetComponent<UIDocument>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void OnEnable() { EnsureBuilt(); }
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
            if (_titleLabel != null)
                _titleLabel.text = isSkill ? "Переназначение навыка:" : "Переназначение клавиши:";
            if (_hintLabel != null)
                _hintLabel.text = $"«{actionName}» — нажмите клавишу";
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
