// Project C: Skills/Social — T-SOC-01
// SocialSkillTreeWindow: полноэкранный overlay для просмотра/изучения/забывания социальных навыков.
// Упрощённая копия SkillTreeWindow (T-INP-09):
//   - только SkillCategory.Social
//   - нет slot overview (все социальные навыки пассивные)
//   - нет bind-кнопок
//   - нет AOE-форматирования
// Design: docs/Character/Social-skills/01_IMPLEMENTATION.md

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ProjectC.Skills.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class SocialSkillTreeWindow : MonoBehaviour
    {
        public static SocialSkillTreeWindow Instance { get; private set; }

        [Header("Resources paths")]
        [SerializeField] private string _uxmlResourcePath = "UI/SocialSkillTreeWindow";
        [SerializeField] private string _ussResourcePath = "UI/SocialSkillTreeWindow";

        private UIDocument _doc;
        private VisualElement _rootContainer;
        private VisualElement _rootInner;
        private bool _built = false;

        private readonly List<SkillNodeConfig> _allSkillConfigs = new List<SkillNodeConfig>();
        private readonly List<SkillNodeConfig> _filteredSkills = new List<SkillNodeConfig>();
        private string _selectedSkillId;
        private string _searchQuery = "";

        private VisualElement _treeContent;
        private readonly Dictionary<string, VisualElement> _treeNodeRefs = new Dictionary<string, VisualElement>();
        // Pan: minimal drag-to-pan, no zoom/fit. Only changes scrollOffset.
        private ScrollView _treeScroll;
        private bool _isPanning = false;
        private Vector2 _panStartMouse;
        private Vector2 _panStartScroll;
        // Zoom: plain wheel. transform.scale on _treeContent.
        private float _zoom = 1.0f;
        private VisualElement _detailName;
        private Label _detailDesc;
        private Label _detailEffects;
        private Label _detailCost;
        private Label _detailTier;
        private VisualElement _detailPrereqContainer;
        private VisualElement _detailDepsContainer;
        private VisualElement _btnLearn;
        private VisualElement _btnForget;
        private TextField _searchField;
        private bool _isSkillsSubscribed;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc != null && _doc.panelSettings == null)
            {
                var ps = Resources.Load<PanelSettings>("UI/SkillTreePanelSettings");
                if (ps != null) _doc.panelSettings = ps;
            }
        }

        private void OnEnable() { EnsureBuilt(); TrySubscribeSkills(); }
        private void OnDisable() { UnsubscribeSkills(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Start() { EnsureBuilt(); TrySubscribeSkills(); }

        private void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null || _doc.rootVisualElement == null) return;

            var uxml = Resources.Load<VisualTreeAsset>(_uxmlResourcePath);
            var uss = Resources.Load<StyleSheet>(_ussResourcePath);
            if (uxml == null) { Debug.LogError("[SocialSkillTreeWindow] UXML not found"); return; }

            _doc.rootVisualElement.Clear();
            if (uss != null) _doc.rootVisualElement.styleSheets.Add(uss);

            _rootContainer = uxml.CloneTree();
            _rootContainer.name = "social-skill-tree-container";
            if (uss != null && !_rootContainer.styleSheets.Contains(uss))
                _rootContainer.styleSheets.Add(uss);
            _doc.rootVisualElement.Add(_rootContainer);

            _rootContainer.style.position = Position.Absolute;
            _rootContainer.style.left = 0;
            _rootContainer.style.top = 0;
            _rootContainer.style.right = 0;
            _rootContainer.style.bottom = 0;
            _rootContainer.pickingMode = PickingMode.Ignore;
            _rootContainer.style.display = DisplayStyle.None;

            // Cache UI refs
            _rootInner = _rootContainer.Q<VisualElement>("social-skill-tree-root");
            _treeContent = _rootContainer.Q<VisualElement>("tree-content");
            _treeContent.generateVisualContent += OnTreePaintEdges;
            _treeScroll = _rootContainer.Q<ScrollView>("tree-canvas-scroll");
            RegisterTreePan();
            _detailName = _rootContainer.Q<VisualElement>("detail-name");
            _detailDesc = _rootContainer.Q<Label>("detail-desc");
            _detailEffects = _rootContainer.Q<Label>("detail-effects");
            _detailCost = _rootContainer.Q<Label>("detail-cost");
            _detailTier = _rootContainer.Q<Label>("detail-tier");
            _detailPrereqContainer = _rootContainer.Q<VisualElement>("detail-prereq-container");
            _detailDepsContainer = _rootContainer.Q<VisualElement>("detail-deps-container");
            _btnLearn = _rootContainer.Q<VisualElement>("btn-learn");
            _btnForget = _rootContainer.Q<VisualElement>("btn-forget");
            _searchField = _rootContainer.Q<TextField>("skill-search");

            InitSearchField();
            InitActionButtons();

            _built = true;
            SetOpen(false);
            Debug.Log($"[SocialSkillTreeWindow] Built. uxml={uxml.name} uss={(uss != null ? uss.name : "<none>")}");
        }

        public void Toggle() { if (IsOpen()) SetOpen(false); else SetOpen(true); }
        public void Show() => SetOpen(true);
        public void Hide() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (!_built) EnsureBuilt();
            if (_rootContainer == null) return;
            if (open)
            {
                _rootContainer.style.display = DisplayStyle.Flex;
                _rootContainer.pickingMode = PickingMode.Position;
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                _rootContainer.MarkDirtyRepaint();
                _rootContainer.schedule.Execute(() => _rootContainer.MarkDirtyRepaint()).StartingIn(50);
                LoadAllSkills();
                ApplyFilterAndSearch();
            }
            else
            {
                _rootContainer.style.display = DisplayStyle.None;
                _rootContainer.pickingMode = PickingMode.Ignore;
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm != null && nm.IsListening) { UnityEngine.Cursor.lockState = CursorLockMode.Locked; UnityEngine.Cursor.visible = false; }
            }
        }

        public bool IsOpen() => _rootContainer != null && _rootContainer.style.display.value == DisplayStyle.Flex;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsOpen()) { SetOpen(false); return; }
            if (!_isSkillsSubscribed) TrySubscribeSkills();
        }

        private void TrySubscribeSkills()
        {
            if (_isSkillsSubscribed) return;
            var s = SkillsClientState.Instance;
            if (s == null) return;
            s.OnSkillsUpdated += HandleSkillsUpdated;
            _isSkillsSubscribed = true;
        }

        private void UnsubscribeSkills()
        {
            if (!_isSkillsSubscribed) return;
            var s = SkillsClientState.Instance;
            if (s != null) s.OnSkillsUpdated -= HandleSkillsUpdated;
            _isSkillsSubscribed = false;
        }

        private void HandleSkillsUpdated(HashSet<string> learned)
        {
            if (IsOpen()) ApplyFilterAndSearch();
            if (!string.IsNullOrEmpty(_selectedSkillId))
            {
                var cfg = _allSkillConfigs.Find(s => s != null && s.skillId == _selectedSkillId);
                if (cfg != null) UpdateDetailPanel(cfg);
            }
        }

        private void LoadAllSkills()
        {
            _allSkillConfigs.Clear();
            var all = Resources.LoadAll<SkillNodeConfig>("Skills");
            foreach (var s in all)
            {
                if (s != null && !string.IsNullOrEmpty(s.skillId) && s.category == SkillCategory.Social)
                    _allSkillConfigs.Add(s);
            }
            _allSkillConfigs.Sort((a, b) =>
            {
                int yA = (a.treeY == 0 && a.treeX == 0) ? int.MaxValue : a.treeY;
                int yB = (b.treeY == 0 && b.treeX == 0) ? int.MaxValue : b.treeY;
                if (yA != yB) return yA.CompareTo(yB);
                return a.treeX.CompareTo(b.treeX);
            });
        }

        private void ApplyFilterAndSearch()
        {
            _filteredSkills.Clear();
            var learned = SkillsClientState.Instance?.CurrentSkills ?? new HashSet<string>();
            foreach (var s in _allSkillConfigs)
            {
                if (s == null) continue;
                if (!MatchesSearch(s)) continue;
                _filteredSkills.Add(s);
            }
            RebuildSkillTree();
        }

        private bool MatchesSearch(SkillNodeConfig s)
        {
            if (string.IsNullOrEmpty(_searchQuery)) return true;
            var q = _searchQuery.ToLower();
            if (s.skillId?.ToLower().Contains(q) == true) return true;
            if (s.displayName?.ToLower().Contains(q) == true) return true;
            if (s.effects != null)
            {
                foreach (var e in s.effects)
                {
                    if (e.statType.ToString().ToLower().Contains(q)) return true;
                    if (e.floatValue > 0 && e.floatValue.ToString("F0").Contains(q)) return true;
                    if (e.multiplier > 0 && e.multiplier.ToString("F2").Contains(q)) return true;
                }
            }
            return false;
        }

        private void InitSearchField()
        {
            if (_searchField == null) return;
            _searchField.RegisterValueChangedCallback(evt => { _searchQuery = evt.newValue ?? ""; ApplyFilterAndSearch(); });
        }

        private void InitActionButtons()
        {
            var btnClose = _rootContainer.Q<VisualElement>("btn-close");
            if (btnClose != null) btnClose.RegisterCallback<ClickEvent>(_ => SetOpen(false));
            if (_btnLearn != null) _btnLearn.RegisterCallback<ClickEvent>(_ => OnLearnClicked());
            if (_btnForget != null) _btnForget.RegisterCallback<ClickEvent>(_ => OnForgetClicked());
        }

        private void RebuildSkillTree()
        {
            if (_treeContent == null) return;
            _treeContent.Clear();
            _treeNodeRefs.Clear();

            var learned = SkillsClientState.Instance?.CurrentSkills ?? new HashSet<string>();

            const float SCALE = 2.5f;
            const float PAD_X = 10f;
            const float PAD_Y = 10f;
            const float NODE_W = 140f;
            const float NODE_H = 28f;

            float maxX = 1000f, maxY = 1000f;
            foreach (var s in _filteredSkills)
            {
                var node = MakeTreeNode(s, learned);
                node.style.left = s.treeX * SCALE + PAD_X;
                node.style.top = s.treeY * SCALE + PAD_Y;
                if (node.style.left.value.value + NODE_W > maxX) maxX = node.style.left.value.value + NODE_W + 100f;
                if (node.style.top.value.value + NODE_H > maxY) maxY = node.style.top.value.value + NODE_H + 100f;
                _treeContent.Add(node);
                _treeNodeRefs[s.skillId] = node;
            }

            _treeContent.style.width = maxX;
            _treeContent.style.height = maxY;
            _treeContent.MarkDirtyRepaint();
        }

        private VisualElement MakeTreeNode(SkillNodeConfig s, HashSet<string> learned)
        {
            var node = new VisualElement();
            node.AddToClassList("tree-node");
            bool isLearned = learned.Contains(s.skillId);
            bool isAvailable = !isLearned && CanLearn(s, learned);
            node.AddToClassList(isLearned ? "tree-node-learned" : (isAvailable ? "tree-node-available" : "tree-node-locked"));

            // All social skills are passive
            node.AddToClassList("tree-node-passive");

            node.name = "tree-node-" + s.skillId;

            var badge = new Label { text = isLearned ? "✓" : (isAvailable ? "○" : "✕") };
            badge.AddToClassList("tree-node-badge");
            node.Add(badge);

            var title = new Label { text = s.displayName ?? s.skillId };
            title.AddToClassList("tree-node-title");
            node.Add(title);

            // [P] badge — all social skills are passive
            var typeBadge = new Label { text = "P" };
            typeBadge.AddToClassList("tree-node-type-badge");
            typeBadge.AddToClassList("tree-node-type-passive");
            typeBadge.tooltip = "Пассивный (применяется автоматически)";
            node.Add(typeBadge);

            var capturedId = s.skillId;
            node.RegisterCallback<ClickEvent>(_ => SelectSkill(capturedId));
            return node;
        }

        private void OnTreePaintEdges(MeshGenerationContext ctx)
        {
            if (_treeContent == null || _treeNodeRefs.Count == 0) return;
            var learned = SkillsClientState.Instance?.CurrentSkills ?? new HashSet<string>();
            var painter = ctx.painter2D;
            if (painter == null) return;
            painter.lineWidth = 2f;

            foreach (var s in _filteredSkills)
            {
                if (s.prerequisites == null) continue;
                foreach (var prereq in s.prerequisites)
                {
                    if (prereq == null) continue;
                    if (!_treeNodeRefs.TryGetValue(prereq.skillId, out var fromNode)) continue;
                    if (!_treeNodeRefs.TryGetValue(s.skillId, out var toNode)) continue;
                    var fromLayout = fromNode.layout;
                    var toLayout = toNode.layout;
                    float x1 = fromLayout.x + fromLayout.width * 0.5f;
                    float y1 = fromLayout.y + fromLayout.height;
                    float x2 = toLayout.x + toLayout.width * 0.5f;
                    float y2 = toLayout.y;

                    bool fromLearned = learned.Contains(prereq.skillId);
                    painter.strokeColor = fromLearned
                        ? new Color(0.4f, 0.85f, 0.5f, 0.9f)
                        : new Color(0.4f, 0.4f, 0.45f, 0.5f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x1, y1));
                    painter.LineTo(new Vector2(x2, y2));
                    painter.Stroke();
                }
            }
        }

        private void SelectSkill(string skillId)
        {
            if (_selectedSkillId == skillId) return;
            _selectedSkillId = skillId;
            var cfg = _allSkillConfigs.Find(s => s != null && s.skillId == skillId);
            if (cfg != null) UpdateDetailPanel(cfg);
        }

        private void UpdateDetailPanel(SkillNodeConfig s)
        {
            if (s == null) return;
            var learned = SkillsClientState.Instance?.CurrentSkills ?? new HashSet<string>();
            bool isLearned = learned.Contains(s.skillId);
            bool canLearn = CanLearn(s, learned);
            if (_detailName != null) _detailName.Q<Label>()!.text = s.displayName ?? s.skillId;
            if (_detailDesc != null) _detailDesc.text = s.description ?? "(нет описания)";

            // Type line: all social skills are passive
            string typeLine = "Тип: Пассивный (применяется автоматически)";
            string effectsLine = $"Эффекты: {FormatEffectsText(s)}";
            if (_detailEffects != null) _detailEffects.text = $"{typeLine}\n{effectsLine}";

            if (_detailCost != null) _detailCost.text = $"Стоимость: {(s.LearnXpCost > 0 ? s.LearnXpCost.ToString("F0") + " XP" : "Free")}";
            if (_detailTier != null)
            {
                var parts = new List<string>();
                if (s.RequiredStrengthTier > 0) parts.Add($"STR {s.RequiredStrengthTier}+");
                if (s.RequiredDexterityTier > 0) parts.Add($"DEX {s.RequiredDexterityTier}+");
                if (s.RequiredIntelligenceTier > 0) parts.Add($"INT {s.RequiredIntelligenceTier}+");
                _detailTier.text = parts.Count > 0
                    ? $"Требования: {string.Join(", ", parts)}"
                    : "Требования: нет";
            }
            if (_detailPrereqContainer != null) RebuildPrereqList(s, learned);
            if (_detailDepsContainer != null) RebuildDependentsList(s);
            if (_btnLearn != null) _btnLearn.style.display = (canLearn && !isLearned) ? DisplayStyle.Flex : DisplayStyle.None;
            if (_btnForget != null) _btnForget.style.display = isLearned ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool CanLearn(SkillNodeConfig s, HashSet<string> learned)
        {
            if (learned.Contains(s.skillId)) return false;
            if (s.prerequisites != null)
                foreach (var p in s.prerequisites)
                    if (p != null && !learned.Contains(p.skillId)) return false;
            return true;
        }

        private string FormatEffectsText(SkillNodeConfig s)
        {
            if (s.effects == null || s.effects.Length == 0) return "(нет)";
            var parts = new List<string>();
            foreach (var e in s.effects)
            {
                if (e.type == SkillEffect.Type.StatMod)
                {
                    if (e.floatValue > 0f) parts.Add($"{e.statType}+{e.floatValue:F0}");
                    if (e.multiplier > 0f) parts.Add($"x{e.multiplier:F2}");
                }
                else if ((int)e.type >= 3 && !string.IsNullOrEmpty(e.stringParam))
                    parts.Add($"[{e.stringParam}]");
            }
            return parts.Count > 0 ? string.Join(" ", parts) : "(нет)";
        }

        private void RebuildPrereqList(SkillNodeConfig s, HashSet<string> learned)
        {
            _detailPrereqContainer.Clear();
            if (s.prerequisites == null || s.prerequisites.Length == 0)
            {
                _detailPrereqContainer.Add(new Label { text = "(нет)" });
                return;
            }
            foreach (var p in s.prerequisites)
            {
                if (p == null) continue;
                var l = new Label { text = $"{(learned.Contains(p.skillId) ? "✓" : "✕")} {p.displayName ?? p.skillId}" };
                l.AddToClassList(learned.Contains(p.skillId) ? "stw-prereq-have" : "stw-prereq-missing");
                _detailPrereqContainer.Add(l);
            }
        }

        private void RebuildDependentsList(SkillNodeConfig s)
        {
            _detailDepsContainer.Clear();
            var deps = new List<string>();
            foreach (var other in _allSkillConfigs)
            {
                if (other == null || other == s) continue;
                if (other.prerequisites != null)
                    foreach (var p in other.prerequisites)
                        if (p != null && p.skillId == s.skillId) { deps.Add(other.displayName ?? other.skillId); break; }
            }
            if (deps.Count == 0)
                _detailDepsContainer.Add(new Label { text = "(ничего)" });
            else
                foreach (var d in deps)
                    _detailDepsContainer.Add(new Label { text = "→ " + d });
        }

        private void OnLearnClicked()
        {
            if (string.IsNullOrEmpty(_selectedSkillId)) return;
            try
            {
                var t = Type.GetType("ProjectC.Skills.SkillsServer, Assembly-CSharp");
                if (t == null) { Debug.LogWarning("[SocialSkillTreeWindow] SkillsServer type not found"); return; }
                var inst = t.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (inst == null) { Debug.LogWarning("[SocialSkillTreeWindow] SkillsServer.Instance is null"); return; }
                var mi = t.GetMethod("RequestLearnSkillRpc");
                if (mi == null) { Debug.LogWarning("[SocialSkillTreeWindow] RequestLearnSkillRpc not found"); return; }
                var rpcParams = System.Activator.CreateInstance(typeof(Unity.Netcode.RpcParams));
                mi.Invoke(inst, new object[] { _selectedSkillId, rpcParams });
                Debug.Log($"[SocialSkillTreeWindow] RequestLearnSkillRpc: skillId={_selectedSkillId}");
            }
            catch (Exception ex) { Debug.LogWarning($"[SocialSkillTreeWindow] OnLearnClicked error: {ex.Message}"); }
        }

        private void OnForgetClicked()
        {
            if (string.IsNullOrEmpty(_selectedSkillId)) return;
            try
            {
                var t = Type.GetType("ProjectC.Skills.SkillsServer, Assembly-CSharp");
                if (t == null) { Debug.LogWarning("[SocialSkillTreeWindow] SkillsServer type not found"); return; }
                var inst = t.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (inst == null) { Debug.LogWarning("[SocialSkillTreeWindow] SkillsServer.Instance is null"); return; }
                var mi = t.GetMethod("RequestForgetSkillRpc");
                if (mi == null) { Debug.LogWarning("[SocialSkillTreeWindow] RequestForgetSkillRpc not found"); return; }
                var rpcParams = System.Activator.CreateInstance(typeof(Unity.Netcode.RpcParams));
                mi.Invoke(inst, new object[] { _selectedSkillId, rpcParams });
                Debug.Log($"[SocialSkillTreeWindow] RequestForgetSkillRpc: skillId={_selectedSkillId}");
            }
            catch (Exception ex) { Debug.LogWarning($"[SocialSkillTreeWindow] OnForgetClicked error: {ex.Message}"); }
        }

        // =================== Pan ===================
        private void RegisterTreePan()
        {
            if (_treeContent == null || _treeScroll == null) return;
            _treeContent.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown);
            _treeContent.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove);
            _treeContent.RegisterCallback<PointerUpEvent>(OnCanvasPointerUp);
            _treeScroll.RegisterCallback<WheelEvent>(OnCanvasWheel);
        }

        private void OnCanvasPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            if (_treeScroll == null || _treeContent == null) return;
            _isPanning = true;
            _panStartMouse = evt.position;
            _panStartScroll = _treeScroll.scrollOffset;
            evt.StopPropagation();
        }

        private void OnCanvasPointerMove(PointerMoveEvent evt)
        {
            if (!_isPanning) return;
            if (_treeScroll == null) return;
            Vector2 delta = (Vector2)evt.position - _panStartMouse;
            _treeScroll.scrollOffset = _panStartScroll - delta;
            evt.StopPropagation();
        }

        private void OnCanvasPointerUp(PointerUpEvent evt)
        {
            if (!_isPanning) return;
            if (evt.button != 0 && evt.button != -1) return;
            _isPanning = false;
        }

        // =================== Zoom ===================
        private const float MIN_ZOOM = 0.5f;
        private const float MAX_ZOOM = 2.0f;
        private const float ZOOM_STEP = 0.1f;

        private void OnCanvasWheel(WheelEvent evt)
        {
            if (_treeContent == null) return;
            evt.StopPropagation();
            float delta = evt.delta.y > 0 ? -ZOOM_STEP : ZOOM_STEP;
            float newZoom = Mathf.Clamp(_zoom + delta, MIN_ZOOM, MAX_ZOOM);
            if (Mathf.Approximately(newZoom, _zoom)) return;
            _zoom = newZoom;
            _treeContent.style.scale = new UnityEngine.UIElements.Scale(new Vector3(_zoom, _zoom, 1f));
            _treeContent.MarkDirtyRepaint();
        }
    }
}