// Project C: Input System — Phase 3 (v2)
// SkillBindingWindow: drag-to-slot UI.
//
// Логика:
// - Слева: список всех SkillInputSlot (Primary/Secondary/Slot1-Slot4)
// - Клик на слот → открывает модалку со списком skills
// - В модалке: каждый skill имеет кнопку [+] которая вызывает SkillInputService.BindSlot
// - Skills уже привязанные — отмечены (зелёный фон) и имеют [✕] для отвязки

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ProjectC.Skills;
using ProjectC.UI;

namespace ProjectC.UI.Settings
{
    [RequireComponent(typeof(UIDocument))]
    public class SkillBindingWindow : MonoBehaviour
    {
        public static SkillBindingWindow Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        private ScrollView _slotsScroll;
        private ScrollView _skillsScroll;
        private VisualElement _modalOverlay;
        private Label _modalTitle;
        private bool _built = false;

        private SkillInputSlot? _activeSlot = null;

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

            var uxml = Resources.Load<VisualTreeAsset>("UI/SkillBindingWindow");
            var uss = Resources.Load<StyleSheet>("UI/SkillBindingWindow");
            if (uxml == null) { Debug.LogError("[SkillBindingWindow] UXML not found"); return; }

            _doc.rootVisualElement.Clear();
            if (uss != null) _doc.rootVisualElement.styleSheets.Add(uss);
            _root = uxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            _slotsScroll = _root.Q<ScrollView>("slots-scroll");
            _skillsScroll = _root.Q<ScrollView>("skills-scroll");
            _modalOverlay = _root.Q<VisualElement>("modal-overlay");
            _modalTitle = _root.Q<Label>("modal-title");

            var closeBtn = _root.Q<Button>("sk-close-btn");
            if (closeBtn != null) closeBtn.clicked += () => SetOpen(false);

            var modalCloseBtn = _root.Q<Button>("modal-close-btn");
            if (modalCloseBtn != null) modalCloseBtn.clicked += CloseModal;

            _built = true;
            SetOpen(false);
            Debug.Log("[SkillBindingWindow] Built.");
        }

        public static void Show()
        {
            if (Instance == null) EnsureExists();
            Instance?.ShowInternal();
        }

        private static void EnsureExists()
        {
            var existing = Object.FindObjectsByType<SkillBindingWindow>(FindObjectsInactive.Include);
            if (existing != null && existing.Length > 0) return;
            var go = new GameObject("[SkillBindingWindow]");
            DontDestroyOnLoad(go);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UI/SkillBindingPanelSettings");
            go.AddComponent<SkillBindingWindow>();
        }

        public void ShowInternal()
        {
            if (!_built) EnsureBuilt();
            SetOpen(true);
            RebuildSlots();
            CloseModal();
        }

        public void Toggle() { if (IsOpen()) SetOpen(false); else ShowInternal(); }

        public bool IsOpen() => _built && _root != null && _root.style.display.value == DisplayStyle.Flex;

        private void SetOpen(bool open)
        {
            if (_root == null) return;
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _root.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                if (UIManager.Instance != null) UIManager.Instance.OpenPanel("SkillBindingWindow", 150);
            }
            else
            {
                _activeSlot = null;
                if (UIManager.Instance != null) UIManager.Instance.ClosePanel("SkillBindingWindow");
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        private void RebuildSlots()
        {
            if (_slotsScroll == null) return;
            _slotsScroll.Clear();

            var sis = SkillInputService.Instance;
            if (sis == null)
            {
                _slotsScroll.Add(new Label("SkillInputService ещё не создан"));
                return;
            }

            var allSlots = new[] { SkillInputSlot.Primary, SkillInputSlot.Secondary, SkillInputSlot.Slot1, SkillInputSlot.Slot2, SkillInputSlot.Slot3, SkillInputSlot.Slot4 };
            foreach (var slot in allSlots)
            {
                _slotsScroll.Add(MakeSlotRow(slot, sis));
            }
        }

        private void RebuildModal()
        {
            if (_skillsScroll == null || !_activeSlot.HasValue) return;
            _skillsScroll.Clear();

            if (_modalTitle != null) _modalTitle.text = $"Выберите навык для слота {_activeSlot.Value}";

            var sis = SkillInputService.Instance;
            if (sis == null) return;

            var skills = sis.GetAllSkillIds();
            if (skills == null || skills.Count == 0)
            {
                _skillsScroll.Add(new Label("(нет доступных навыков)"));
                return;
            }

            string currentSkill = sis.GetSkillForSlot(_activeSlot.Value);

            foreach (var sid in skills)
            {
                _skillsScroll.Add(MakeSkillRow(sid, currentSkill, sis));
            }
        }

        private void OpenModalForSlot(SkillInputSlot slot)
        {
            _activeSlot = slot;
            if (_modalOverlay != null)
            {
                _modalOverlay.style.display = DisplayStyle.Flex;
                _modalOverlay.pickingMode = PickingMode.Position;
            }
            RebuildModal();
        }

        private void CloseModal()
        {
            _activeSlot = null;
            if (_modalOverlay != null)
            {
                _modalOverlay.style.display = DisplayStyle.None;
                _modalOverlay.pickingMode = PickingMode.Ignore;
            }
        }

        private void Update()
        {
            if (!_built || _root == null || !IsOpen()) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_activeSlot.HasValue) CloseModal();
                else SetOpen(false);
            }
        }

        // ===== Row builders =====

        private VisualElement MakeSlotRow(SkillInputSlot slot, SkillInputService sis)
        {
            var row = new VisualElement(); row.AddToClassList("sk-slot-row");
            var info = new VisualElement(); info.AddToClassList("sk-slot-info"); info.style.flexGrow = 1;
            var name = new Label(slot.ToString()); name.AddToClassList("sk-slot-name");
            info.Add(name);
            string boundSkill = sis.GetSkillForSlot(slot);
            var value = new Label(string.IsNullOrEmpty(boundSkill) ? "(пусто)" : boundSkill);
            value.AddToClassList("sk-slot-value");
            if (!string.IsNullOrEmpty(boundSkill)) value.AddToClassList("sk-slot-value-bound");
            info.Add(value);
            row.Add(info);

            var btn = new Button { text = "✎" };
            btn.AddToClassList("sk-edit-btn");
            btn.clicked += () => OpenModalForSlot(slot);
            row.Add(btn);

            // Если занят — кнопка очистки
            if (!string.IsNullOrEmpty(boundSkill))
            {
                var clearBtn = new Button { text = "✕" };
                clearBtn.AddToClassList("sk-clear-btn");
                clearBtn.clicked += () =>
                {
                    sis.BindSlot(slot, "");
                    RebuildSlots();
                };
                row.Add(clearBtn);
            }

            return row;
        }

        private VisualElement MakeSkillRow(string skillId, string currentSkill, SkillInputService sis)
        {
            var row = new VisualElement(); row.AddToClassList("sk-skill-row");

            var info = new VisualElement(); info.style.flexGrow = 1;
            var name = new Label(skillId); name.AddToClassList("sk-skill-name");
            info.Add(name);

            // Если уже привязан к чему-то — показать
            string boundTo = "";
            foreach (var kvp in sis.GetAllBindings())
                if (kvp.Value == skillId) { boundTo = kvp.Key.ToString(); break; }
            if (!string.IsNullOrEmpty(boundTo))
            {
                var tag = new Label("→ " + boundTo); tag.AddToClassList("sk-skill-bound-tag");
                info.Add(tag);
            }
            row.Add(info);

            var bindBtn = new Button { text = "+" };
            bindBtn.AddToClassList("sk-bind-btn");
            bindBtn.clicked += () =>
            {
                if (!_activeSlot.HasValue) return;
                // Если этот навык уже привязан к другому слоту — отвязываем оттуда
                SkillInputSlot? oldSlot = null;
                foreach (var kvp in sis.GetAllBindings())
                    if (kvp.Value == skillId) { oldSlot = kvp.Key; break; }
                if (oldSlot.HasValue && oldSlot.Value != _activeSlot.Value)
                {
                    sis.BindSlot(oldSlot.Value, "");
                }
                sis.BindSlot(_activeSlot.Value, skillId);
                RebuildSlots();
                CloseModal();
            };
            row.Add(bindBtn);

            return row;
        }
    }
}
