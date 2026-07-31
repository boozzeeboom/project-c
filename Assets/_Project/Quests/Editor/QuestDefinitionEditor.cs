// QuestDefinitionEditor — кастомный Editor для QuestDefinition с 3 вкладками.
// Спроектирован для не-технических пользователей:
// - Контекстно-зависимые поля (через PropertyDrawer'ы)
// - Drag-and-drop для NPC, предметов, квестов
// - Сводка flow наверху
// - Валидация на лету с цветным индикатором

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    [CustomEditor(typeof(QuestDefinition))]
    public class QuestDefinitionEditor : UnityEditor.Editor
    {
        private enum Tab { Stages, Rewards, Prerequisites }
        private Tab _currentTab = Tab.Stages;

        private QuestDefinitionValidator.ValidationResult _lastValidation;
        private bool _validated;

        private static readonly Color OkColor = new Color(0.3f, 0.7f, 0.3f);
        private static readonly Color WarnColor = new Color(0.9f, 0.7f, 0.2f);
        private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f);

        public override void OnInspectorGUI()
        {
            var def = (QuestDefinition)target;
            if (def == null) return;

            serializedObject.Update();

            // ═══════════════ HEADER ═══════════════
            DrawHeader(def);

            // ═══════════════ SUMMARY ═══════════════
            DrawSummary(def);

            // ═══════════════ VALIDATION STATUS ═══════════════
            DrawValidationStatus(def);

            // ═══════════════ TABS ═══════════════
            EditorGUILayout.Space(6);
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, new[] { "📋 Stages", "🎁 Rewards", "⚙️ Prerequisites" });

            EditorGUILayout.Space(4);

            switch (_currentTab)
            {
                case Tab.Stages:
                    DrawStagesTab(def);
                    break;
                case Tab.Rewards:
                    DrawRewardsTab(def);
                    break;
                case Tab.Prerequisites:
                    DrawPrerequisitesTab(def);
                    break;
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                _validated = false;
                EditorUtility.SetDirty(def);
            }
        }

        // ══════════════════════════════════════════
        // HEADER
        // ══════════════════════════════════════════

        private void DrawHeader(QuestDefinition def)
        {
            EditorGUILayout.BeginHorizontal();

            // Icon
            var iconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField("📜", iconStyle, GUILayout.Width(28));

            // Title
            EditorGUILayout.BeginVertical();
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField(string.IsNullOrEmpty(def.displayName) ? def.questId : def.displayName, titleStyle);

            var idStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.7f, 0.9f) }
            };
            EditorGUILayout.LabelField($"ID: {def.questId}", idStyle);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Quick buttons
            // T-U10: Open in Unified Graph
            if (GUILayout.Button("🔗 Unified Graph", GUILayout.Width(120)))
            {
                UnifiedQuestGraphIntegration.OpenUnified(def);
            }

            if (GUILayout.Button("Validate", GUILayout.Width(70)))
            {
                _lastValidation = QuestDefinitionValidator.Validate(def);
                _validated = true;
            }

            if (GUILayout.Button("Graph", GUILayout.Width(55)))
            {
                var w = EditorWindow.GetWindow<QuestNodeGraphWindow>();
                w.titleContent = new GUIContent("Quest Graph");
                w.LoadQuest(def);
                w.Show();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // ══════════════════════════════════════════
        // SUMMARY
        // ══════════════════════════════════════════

        private void DrawSummary(QuestDefinition def)
        {
            var boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                richText = true
            };

            EditorGUILayout.BeginHorizontal(boxStyle);

            // Build flow description
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>Flow:</b>  ");

            int stageCount = def.stages?.Length ?? 0;
            if (stageCount == 0)
            {
                sb.Append("<color=#ff6666>No stages!</color>");
            }
            else
            {
                for (int i = 0; i < stageCount; i++)
                {
                    var st = def.stages[i];
                    if (st == null) continue;
                    sb.Append($"<color=#aaccff>{st.stageId}</color>");
                    if (i < stageCount - 1) sb.Append("  →  ");
                }
            }

            // Reward summary
            sb.Append("  │  ");
            if (def.rewards != null)
            {
                bool hasReward = false;
                if (def.rewards.credits > 0) { sb.Append($"💰{def.rewards.credits} "); hasReward = true; }
                if (def.rewards.items != null && def.rewards.items.Length > 0)
                {
                    int itemCount = 0;
                    foreach (var it in def.rewards.items) if (it != null) itemCount += it.count;
                    sb.Append($"📦×{itemCount} ");
                    hasReward = true;
                }
                if (def.rewards.reputation != null && def.rewards.reputation.Length > 0)
                {
                    sb.Append($"📈×{def.rewards.reputation.Length} ");
                    hasReward = true;
                }
                if (!hasReward) sb.Append("<color=#888888>No rewards</color>");
            }

            // Objective count
            int objCount = 0;
            if (def.stages != null)
                foreach (var st in def.stages)
                    if (st?.objectives != null) objCount += st.objectives.Length;
            sb.Append($"  │  🎯{objCount} objectives");

            // Prerequisite count
            int preqCount = def.prerequisites?.Length ?? 0;
            if (preqCount > 0) sb.Append($"  │  ⚙️{preqCount} prereqs");

            EditorGUILayout.LabelField(sb.ToString(), new GUIStyle(EditorStyles.label) { richText = true, fontSize = 11 });

            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════
        // VALIDATION STATUS
        // ══════════════════════════════════════════

        private void DrawValidationStatus(QuestDefinition def)
        {
            // Auto-validate once per change
            if (!_validated)
            {
                _lastValidation = QuestDefinitionValidator.Validate(def);
                _validated = true;
            }

            if (_lastValidation == null) return;

            int errors = _lastValidation.errors;
            int warnings = _lastValidation.warnings;

            Color bgColor;
            string statusText;

            if (errors > 0)
            {
                bgColor = ErrorColor;
                statusText = $"❌ {errors} error(s), {warnings} warning(s)";
            }
            else if (warnings > 0)
            {
                bgColor = WarnColor;
                statusText = $"⚠️ {warnings} warning(s) — quest functional but check details";
            }
            else
            {
                bgColor = OkColor;
                statusText = "✅ Quest valid — all checks passed";
            }

            var statusStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 4, 4)
            };
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginHorizontal(statusStyle);

            EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);

            GUI.backgroundColor = oldColor;

            // Expand details button
            if (_lastValidation.issues.Count > 0)
            {
                if (GUILayout.Button("Details", EditorStyles.miniButton, GUILayout.Width(55)))
                {
                    var report = new System.Text.StringBuilder();
                    foreach (var issue in _lastValidation.issues)
                    {
                        report.AppendLine($"[{issue.severity}] {issue.message}");
                    }
                    Debug.Log($"=== Validation: {def.questId} ===\n{report}");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════
        // TAB: STAGES
        // ══════════════════════════════════════════

        private void DrawStagesTab(QuestDefinition def)
        {
            var stagesProp = serializedObject.FindProperty("stages");

            EditorGUILayout.LabelField("Quest Stages", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            if (stagesProp == null || !stagesProp.isArray)
            {
                EditorGUILayout.HelpBox("No stages array found.", MessageType.Error);
                return;
            }

            // Draw each stage
            for (int i = 0; i < stagesProp.arraySize; i++)
            {
                var stageProp = stagesProp.GetArrayElementAtIndex(i);
                if (stageProp == null) continue;

                var stageId = stageProp.FindPropertyRelative("stageId")?.stringValue ?? "?";

                // Stage box
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Stage header
                EditorGUILayout.BeginHorizontal();
                var headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(0.5f, 0.9f, 0.5f) }
                };
                EditorGUILayout.LabelField($"Stage {i + 1}: {stageId}", headerStyle);
                GUILayout.FlexibleSpace();

                // Move up/down
                if (i > 0 && GUILayout.Button("▲", GUILayout.Width(24)))
                {
                    stagesProp.MoveArrayElement(i, i - 1);
                    break;
                }
                if (i < stagesProp.arraySize - 1 && GUILayout.Button("▼", GUILayout.Width(24)))
                {
                    stagesProp.MoveArrayElement(i, i + 1);
                    break;
                }

                // Delete
                if (GUILayout.Button("×", GUILayout.Width(24)))
                {
                    stagesProp.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                // Stage content via PropertyDrawer
                float stageH = EditorGUI.GetPropertyHeight(stageProp, new GUIContent($"Stage {i}"));
                var stageRect = EditorGUILayout.GetControlRect(false, stageH);
                EditorGUI.PropertyField(stageRect, stageProp, new GUIContent($"Stage {i}"), true);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            // Add stage button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Stage", GUILayout.Width(120), GUILayout.Height(28)))
            {
                stagesProp.arraySize++;
                var newStage = stagesProp.GetArrayElementAtIndex(stagesProp.arraySize - 1);
                newStage.FindPropertyRelative("stageId").stringValue = "new_stage";
                newStage.FindPropertyRelative("description").stringValue = "";
                newStage.isExpanded = true;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════
        // TAB: REWARDS
        // ══════════════════════════════════════════

        private void DrawRewardsTab(QuestDefinition def)
        {
            EditorGUILayout.LabelField("Quest Rewards", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var rewardsProp = serializedObject.FindProperty("rewards");
            if (rewardsProp != null)
            {
                float h = EditorGUI.GetPropertyHeight(rewardsProp, new GUIContent("Rewards"));
                var r = EditorGUILayout.GetControlRect(false, h);
                EditorGUI.PropertyField(r, rewardsProp, new GUIContent("Rewards"), true);
            }
        }

        // ══════════════════════════════════════════
        // TAB: PREREQUISITES & FLAGS
        // ══════════════════════════════════════════

        private void DrawPrerequisitesTab(QuestDefinition def)
        {
            // ── Identity fields ──
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("questId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));

            var descProp = serializedObject.FindProperty("description");
            EditorGUILayout.PropertyField(descProp, new GUIContent("Description"));
            EditorGUILayout.Space(8);

            // ── Faction gating ──
            EditorGUILayout.LabelField("Faction Gating", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("faction"), new GUIContent("Faction"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minReputation"), new GUIContent("Min Rep"));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);

            // ── Prerequisites ──
            EditorGUILayout.LabelField("Prerequisites (AND — all must be true)", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            var preqProp = serializedObject.FindProperty("prerequisites");
            if (preqProp != null && preqProp.isArray)
            {
                for (int i = 0; i < preqProp.arraySize; i++)
                {
                    var el = preqProp.GetArrayElementAtIndex(i);
                    if (el == null) continue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    var typeProp = el.FindPropertyRelative("type");
                    var typeName = ((QuestPrerequisiteType)typeProp.enumValueIndex).ToString();
                    EditorGUILayout.LabelField($"#{i}: {typeName}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        preqProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();

                    float elH = EditorGUI.GetPropertyHeight(el, new GUIContent($"#{i}"));
                    var elRect = EditorGUILayout.GetControlRect(false, elH);
                    EditorGUI.PropertyField(elRect, el, new GUIContent($"#{i}"), true);

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                // Add button
                if (GUILayout.Button("+ Add Prerequisite", GUILayout.Height(24)))
                {
                    preqProp.arraySize++;
                }
            }
            EditorGUILayout.Space(8);

            // ── Behavior flags ──
            EditorGUILayout.LabelField("Behavior Flags", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("oneShot"), new GUIContent("One Shot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("discoverable"), new GUIContent("Discoverable"));
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
