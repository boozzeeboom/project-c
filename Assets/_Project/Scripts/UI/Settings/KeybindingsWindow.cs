// Project C: Input System — Phase 2.1 → Phase 3 (embedded sub-page support)
// KeybindingsWindow — standalone + embedded режимы.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using ProjectC.Input;

namespace ProjectC.UI.Settings
{
    [RequireComponent(typeof(UIDocument))]
    public class KeybindingsWindow : MonoBehaviour
    {
        public static KeybindingsWindow Instance { get; private set; }

        [SerializeField] private VisualTreeAsset kbUxml;
        [SerializeField] private StyleSheet kbUss;

        private UIDocument _doc;
        private VisualElement _root;
        private ScrollView _skillListScroll;
        private ScrollView _actionListScroll;
        private bool _built = false;

        /// <summary>Callback для Esc при embedded-режиме (возврат в меню настроек).</summary>
        public System.Action OnBackRequested;
        /// <summary>USS для загрузки родительским UIDocument при встраивании.</summary>
        public StyleSheet StyleSheet => kbUss;
        private bool _isEmbedded = false;

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
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null || _doc.rootVisualElement == null) return;

            if (kbUxml == null) kbUxml = Resources.Load<VisualTreeAsset>("UI/KeybindingsWindow");
            if (kbUss  == null) kbUss  = Resources.Load<StyleSheet>("UI/KeybindingsWindow");
            if (kbUxml == null) { Debug.LogError("[KeybindingsWindow] UXML not found"); return; }

            _doc.rootVisualElement.Clear();
            if (kbUss != null) _doc.rootVisualElement.styleSheets.Add(kbUss);
            _root = kbUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            _skillListScroll = _root.Q<ScrollView>("skill-list-scroll");
            _actionListScroll = _root.Q<ScrollView>("action-list-scroll");

            // Кнопки header
            var saveBtn = _root.Q<Button>("save-btn");
            if (saveBtn != null)
            {
                saveBtn.clicked += () =>
                {
                    if (InputBindingsRuntime.Instance != null)
                    {
                        InputBindingsRuntime.Instance.Save();
                        Debug.Log("[KeybindingsWindow] Manual Save → PlayerPrefs");
                    }
                };
            }
            var reloadBtn = _root.Q<Button>("reload-btn");
            if (reloadBtn != null)
            {
                reloadBtn.clicked += () =>
                {
                    if (InputBindingsRuntime.Instance != null)
                    {
                        InputBindingsRuntime.Instance.Load();
                        RebuildLists();
                        Debug.Log("[KeybindingsWindow] Manual Reload from PlayerPrefs");
                    }
                };
            }
            var resetBtn = _root.Q<Button>("reset-defaults-btn");
            if (resetBtn != null)
            {
                resetBtn.clicked += () =>
                {
                    if (InputBindingsRuntime.Instance != null)
                    {
                        InputBindingsRuntime.Instance.ResetToDefaults();
                        RebuildLists();
                        Debug.Log("[KeybindingsWindow] Reset to defaults applied");
                    }
                };
            }

            _built = true;
            SetOpen(false);
            Debug.Log($"[KeybindingsWindow] Built. uxml={kbUxml.name} uss={(kbUss != null ? kbUss.name : "null")}");
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
                UIManager.EnsureExists().OpenPanel("KeybindingsWindow", 200, Hide, gameObject);
                RebuildLists();
            }
        }

        public bool IsOpen() => _built && _root != null && _root.style.display.value == DisplayStyle.Flex;

        /// <summary>
        /// Возвращает VisualElement для встраивания в EscMenu (sub-page).
        /// Снимает абсолютное позиционирование, заменяет на flex-контейнер.
        /// </summary>
        public VisualElement GetPageRoot()
        {
            EnsureBuilt();
            if (_root == null) return null;

            _isEmbedded = true;

            // Снимаем абсолютное позиционирование для встраивания (inline override USS !important)
            _root.style.position = Position.Relative;
            _root.style.left = StyleKeyword.Initial;
            _root.style.top = StyleKeyword.Initial;
            _root.style.right = StyleKeyword.Initial;
            _root.style.bottom = StyleKeyword.Initial;
            _root.style.translate = StyleKeyword.Initial;
            _root.style.width = new StyleLength(StyleKeyword.Auto);
            _root.style.height = new StyleLength(StyleKeyword.Auto);
            _root.style.maxWidth = new StyleLength(StyleKeyword.None);
            _root.style.maxHeight = new StyleLength(StyleKeyword.None);
            _root.style.display = DisplayStyle.Flex;
            _root.pickingMode = PickingMode.Position;

            RebuildLists();
            Debug.Log("[KeybindingsWindow] GetPageRoot: embedded mode ready");
            return _root;
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            // Esc: embedded → OnBackRequested, standalone → SetOpen(false)
            if (kb.escapeKey.wasPressedThisFrame && IsOpen())
            {
                if (_listeningFor != null) { CancelListening(); return; }
                if (_isEmbedded && OnBackRequested != null)
                {
                    Debug.Log("[KeybindingsWindow] Esc in embedded mode → OnBackRequested");
                    OnBackRequested.Invoke();
                    return;
                }
                Debug.Log("[KeybindingsWindow] self-close on Esc");
                SetOpen(false);
                return;
            }

            // Режим прослушивания — ждём нажатия.
            if (_listeningFor != null)
            {
                // Mouse buttons
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    if (mouse.leftButton.wasPressedThisFrame)   { ApplyRebind(_listeningFor.Value, Key.None, 1); return; }
                    if (mouse.rightButton.wasPressedThisFrame)  { ApplyRebind(_listeningFor.Value, Key.None, 2); return; }
                    if (mouse.middleButton.wasPressedThisFrame) { ApplyRebind(_listeningFor.Value, Key.None, 3); return; }
                }
                // Keyboard keys (skip Escape — used for cancel)
                foreach (var keyControl in kb.allKeys)
                {
                    if (keyControl.keyCode == Key.Escape) continue;
                    if (keyControl.wasPressedThisFrame)
                    {
                        ApplyRebind(_listeningFor.Value, keyControl.keyCode, 0);
                        return;
                    }
                }
            }
        }

        // ===== Rebind State =====

        private struct ListeningState
        {
            public bool isSkill; // false = action binding
            public ProjectC.Skills.SkillInputSlot skillSlot;
            public InputBindingsConfig.GameAction action;
        }
        private ListeningState? _listeningFor = null;

        private void StartListening(ListeningState state)
        {
            _listeningFor = state;
            string name = state.isSkill
                ? InputBindingsRuntime.Instance?.GetSkillDisplayName(state.skillSlot) ?? state.skillSlot.ToString()
                : InputBindingsRuntime.Instance?.GetActionDisplayName(state.action) ?? state.action.ToString();
            RebindPromptWindow.Show(name, state.isSkill);
            Debug.Log($"[KeybindingsWindow] Listening for: {name}");
        }

        private void CancelListening()
        {
            Debug.Log("[KeybindingsWindow] Cancel rebind");
            _listeningFor = null;
            RebindPromptWindow.Hide();
            RebuildLists();
        }

        private void ApplyRebind(ListeningState state, Key key, int mouseButtonRaw)
        {
            var rt = InputBindingsRuntime.Instance;
            if (rt == null) return;

            bool ok = false;
            if (state.isSkill)
                ok = rt.RebindSkill(state.skillSlot, mouseButtonRaw, Key.None, key);
            else
                ok = rt.RebindAction(state.action, key, mouseButtonRaw);

            string name = state.isSkill ? state.skillSlot.ToString() : state.action.ToString();
            if (ok)
                Debug.Log($"[KeybindingsWindow] Rebound: {name} → key={key} mouse={mouseButtonRaw}");
            else
                Debug.LogWarning($"[KeybindingsWindow] Rebind failed for: {name}");

            _listeningFor = null;
            RebindPromptWindow.Hide();
            RebuildLists();
        }

        private void RebuildLists()
        {
            var cfg = InputBindingsRuntime.Instance?.Config;
            if (cfg == null) { Debug.LogWarning("[KeybindingsWindow] InputBindingsConfig not loaded"); return; }

            if (_skillListScroll != null && cfg.combatSkills != null)
            {
                _skillListScroll.Clear();
                foreach (var b in cfg.combatSkills) _skillListScroll.Add(MakeSkillRow(b));
            }
            if (_actionListScroll != null && cfg.actions != null)
            {
                _actionListScroll.Clear();
                var byCat = new Dictionary<InputBindingsConfig.ActionCategory, List<InputBindingsConfig.ActionBinding>>();
                foreach (var a in cfg.actions)
                {
                    if (!byCat.TryGetValue(a.category, out var list)) { list = new List<InputBindingsConfig.ActionBinding>(); byCat[a.category] = list; }
                    list.Add(a);
                }
                foreach (var kvp in byCat)
                {
                    var h = new Label($"--- {kvp.Key} ---");
                    h.AddToClassList("kb-cat-header");
                    _actionListScroll.Add(h);
                    foreach (var a in kvp.Value) _actionListScroll.Add(MakeActionRow(a));
                }
            }
        }

        private static VisualElement MakeSkillRow(InputBindingsConfig.SkillKeyBinding b)
        {
            var row = new VisualElement(); row.AddToClassList("kb-row");
            var l = new Label($"{b.slot}"); l.AddToClassList("kb-row-action"); row.Add(l);
            var k = new Label(b.displayName); k.AddToClassList("kb-row-key"); row.Add(k);
            MakeRowClickable(row, b.slot);
            return row;
        }

        private static void MakeRowClickable(VisualElement row, ProjectC.Skills.SkillInputSlot slot)
        {
            row.RegisterCallback<ClickEvent>(_ =>
            {
                var inst = UnityEngine.Object.FindAnyObjectByType<KeybindingsWindow>();
                if (inst != null)
                {
                    inst.StartListening(new ListeningState
                    {
                        isSkill = true,
                        skillSlot = slot,
                        action = default
                    });
                }
            });
        }

        private static VisualElement MakeActionRow(InputBindingsConfig.ActionBinding a)
        {
            var row = new VisualElement(); row.AddToClassList("kb-row");
            var label = new Label(a.displayName); label.AddToClassList("kb-row-action"); row.Add(label);
            string keyStr = "";
            if (a.mouseButtonRaw == 1) keyStr = "ЛКМ";
            else if (a.mouseButtonRaw == 2) keyStr = "ПКМ";
            else if (a.mouseButtonRaw == 3) keyStr = "СКМ";
            else if (a.mouseButtonRaw == 0 && a.key != Key.None) keyStr = a.key.ToString();
            else if (a.mouseButtonRaw != 0) keyStr = $"Mouse{a.mouseButtonRaw}";
            var k = new Label(keyStr); k.AddToClassList("kb-row-key"); row.Add(k);
            MakeRowClickable(row, a.action);
            return row;
        }

        private static void MakeRowClickable(VisualElement row, InputBindingsConfig.GameAction action)
        {
            row.RegisterCallback<ClickEvent>(_ =>
            {
                var inst = UnityEngine.Object.FindAnyObjectByType<KeybindingsWindow>();
                if (inst != null)
                {
                    inst.StartListening(new ListeningState
                    {
                        isSkill = false,
                        skillSlot = default,
                        action = action
                    });
                }
            });
        }
    }
}
