// Project C: Input System — Phase 2.1
// EscMenuWindow по CharacterWindow/SkillTreeWindow паттерну (рабочий).
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Netcode;
using ProjectC.UI;

namespace ProjectC.UI.EscMenu
{
    [RequireComponent(typeof(UIDocument))]
    public class EscMenuWindow : MonoBehaviour
    {
        public static EscMenuWindow Instance { get; private set; }

        [SerializeField] private VisualTreeAsset escUxml;
        [SerializeField] private StyleSheet escUss;

        private UIDocument _doc;
        private VisualElement _root;
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

            // CharacterWindow pattern: Resources fallback + Clear + CloneTree + Add
            if (escUxml == null) escUxml = Resources.Load<VisualTreeAsset>("UI/EscMenuWindow");
            if (escUss  == null) escUss  = Resources.Load<StyleSheet>("UI/EscMenuStyles");
            if (escUxml == null) { Debug.LogError("[EscMenuWindow] UXML not found"); return; }

            _doc.rootVisualElement.Clear();
            if (escUss != null) _doc.rootVisualElement.styleSheets.Add(escUss);
            _root = escUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            InitSettingsButton();
            _built = true;
            SetOpen(false);
            Debug.Log($"[EscMenuWindow] Built. uxml={escUxml.name} uss={(escUss != null ? escUss.name : "null")}");
        }

        public void Toggle() { if (IsOpen()) SetOpen(false); else SetOpen(true); }
        public void Show() => SetOpen(true);
        public void Hide() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (!_built) EnsureBuilt();
            if (_root == null) return;
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _root.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
                _root.MarkDirtyRepaint();
                _root.schedule.Execute(() => _root.MarkDirtyRepaint()).StartingIn(50);
                UIManager.EnsureExists().OpenPanel("EscMenu", 100, Hide, gameObject);
            }
            else
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                { UnityEngine.Cursor.lockState = CursorLockMode.Locked; UnityEngine.Cursor.visible = false; }
            }
        }

        public bool IsOpen() => _built && _root != null && _root.style.display.value == DisplayStyle.Flex;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            // Если UIManager только что закрыл стековую панель — не мешаем.
            if (UIManager.Instance != null && UIManager.Instance._escConsumedThisFrame) return;

            // Всё остальное — просто Toggle.
            // Если CharacterWindow была открыта, она уже закрылась в своём Update (или закроется).
            // Toggle() сам проверит: если меню открыто → закроет, если закрыто → откроет.
            Toggle();
        }

        private void LateUpdate()
        {
            _externalWasVisible = IsAnyExternalWindowOpen();
        }

        private bool _externalWasVisible = false;

        private static bool IsAnyExternalWindowOpen()
        {
            if (ProjectC.UI.Client.CharacterWindow.Instance != null
                && ProjectC.UI.Client.CharacterWindow.Instance.IsVisible())
                return true;
            return false;
        }

        private void InitSettingsButton()
        {
            var btn = _root?.Q<VisualElement>("esc-settings-btn");
            if (btn == null) { Debug.LogWarning("[EscMenuWindow] btn not found"); return; }
            btn.RegisterCallback<ClickEvent>(_ => {
                SetOpen(false);
                UIManager.Instance?.ClosePanel("EscMenu");
                if (Settings.KeybindingsWindow.Instance != null)
                    Settings.KeybindingsWindow.Instance.Show();
                else
                    Debug.LogError("[EscMenuWindow] KeybindingsWindow.Instance == null");
            });
        }
    }
}
