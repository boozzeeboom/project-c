// T-Q09b / M17 v7: QuestGraphWindow — EditorWindow с переписанным QuestGraphView.
// Открыть: Tools > ProjectC > Quests > Quest Graph View (или для selected quest).

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Quests;

namespace ProjectC.Quests.Editor
{
    public class QuestGraphWindow : EditorWindow
    {
        private QuestGraphView _graph;
        private ObjectField _questField;
        private Label _statusLabel;

        [MenuItem("Tools/ProjectC/Quests/Quest Graph View", priority = 101)]
        public static void Open()
        {
            var w = GetWindow<QuestGraphWindow>();
            w.titleContent = new GUIContent("Quest Graph");
            w.minSize = new Vector2(720, 480);
            w.Show();
        }

        [MenuItem("Assets/ProjectC/Open Quest Graph", priority = 1000)]
        public static void OpenFromAsset()
        {
            var sel = Selection.activeObject;
            if (sel is QuestDefinition qd)
            {
                var w = GetWindow<QuestGraphWindow>();
                w.titleContent = new GUIContent($"Quest Graph: {qd.questId}");
                w.LoadQuest(qd);
                w.Show();
            }
        }

        [MenuItem("Assets/ProjectC/Open Quest Graph", validate = true)]
        public static bool OpenFromAssetValidate() => Selection.activeObject is QuestDefinition;

        private void OnEnable()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;

            // Toolbar at top
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingTop = 4;
            toolbar.style.paddingBottom = 4;
            toolbar.style.paddingLeft = 6;
            toolbar.style.paddingRight = 6;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 1f));

            _questField = new ObjectField("Quest") { objectType = typeof(QuestDefinition), allowSceneObjects = false };
            _questField.style.flexGrow = 1;
            _questField.RegisterValueChangedCallback(evt => LoadQuest(evt.newValue as QuestDefinition));
            toolbar.Add(_questField);

            var refreshBtn = new Button(() => LoadQuest(_questField.value as QuestDefinition)) { text = "🔄" };
            refreshBtn.style.marginLeft = 4;
            toolbar.Add(refreshBtn);

            var fitBtn = new Button(() => _graph?.MarkDirtyRepaint()) { text = "⊡ Refresh" };
            fitBtn.style.marginLeft = 4;
            toolbar.Add(fitBtn);

            root.Add(toolbar);

            // Graph view (VisualElement, not GraphView)
            _graph = new QuestGraphView();
            _graph.name = "QuestGraphView";
            _graph.style.flexGrow = 1;
            root.Add(_graph);

            // Status bar
            _statusLabel = new Label("(no quest loaded)");
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.bottom = 4;
            _statusLabel.style.left = 4;
            _statusLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f));
            _statusLabel.style.fontSize = 10;
            root.Add(_statusLabel);
        }

        public void LoadQuest(QuestDefinition quest)
        {
            if (_questField != null) _questField.value = quest;
            if (_graph == null) return;
            _graph.LoadQuest(quest);
            if (_statusLabel != null)
            {
                if (quest == null) _statusLabel.text = "(no quest loaded)";
                else _statusLabel.text = $"Quest: {quest.questId} | stages: {quest.stages?.Length ?? 0} | rewards: CR={quest.rewards?.credits ?? 0}";
            }
        }
    }
}
#endif
