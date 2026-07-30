// NpcDefinitionEditor — кастомный Editor для NpcDefinition с организованными блоками.
// Ключевые улучшения:
// - Drag-and-drop QuestDefinition[] для quest offers / turn-ins (вместо строковых ID)
// - Цветные блоки по категориям (Identity, Quests, Dialogue, etc.)
// - Сводка наверху: фракция, количество квестов, сервисы
// - Скрытие legacy string[] полей когда заданы object refs
//
// См. docs/NPC_quests/NPC_EDITOR_v2.md

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    [CustomEditor(typeof(NpcDefinition))]
    public class NpcDefinitionEditor : UnityEditor.Editor
    {
        // ── Colors ──
        private static readonly Color HeaderBg = new Color(0.18f, 0.22f, 0.32f, 1f);
        private static readonly Color BlockBg = new Color(0.22f, 0.22f, 0.22f, 0.5f);
        private static readonly Color QuestColor = new Color(0.6f, 0.8f, 1.0f);
        private static readonly Color FactionColor = new Color(0.9f, 0.7f, 0.3f);
        private static readonly Color WarnColor = new Color(0.9f, 0.5f, 0.2f);

        private bool _showIdentity = true;
        private bool _showVisuals = true;
        private bool _showDialogue = true;
        private bool _showQuests = true;
        private bool _showServices = true;
        private bool _showInteraction = true;
        private bool _showAttitude = true;
        private bool _showAudio = true;

        public override void OnInspectorGUI()
        {
            var npc = (NpcDefinition)target;
            if (npc == null) return;

            serializedObject.Update();

            DrawHeader(npc);
            EditorGUILayout.Space(6);
            DrawSummary(npc);
            EditorGUILayout.Space(6);
            DrawIdentityBlock(npc);
            DrawVisualsBlock();
            DrawDialogueBlock();
            DrawQuestsBlock(npc);
            DrawServicesBlock();
            DrawInteractionBlock();
            DrawAttitudeBlock();
            DrawAudioBlock();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                EditorUtility.SetDirty(npc);
        }

        // ══════════════════════════════════════════
        // HEADER
        // ══════════════════════════════════════════

        private void DrawHeader(NpcDefinition npc)
        {
            var headerStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 0, 0)
            };
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = HeaderBg;
            EditorGUILayout.BeginVertical(headerStyle);
            GUI.backgroundColor = oldBg;

            EditorGUILayout.BeginHorizontal();

            // Portrait placeholder
            if (npc.portrait != null)
            {
                var portraitRect = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48), GUILayout.Height(48));
                EditorGUI.DrawPreviewTexture(portraitRect, npc.portrait.texture, null, ScaleMode.ScaleToFit);
            }

            EditorGUILayout.BeginVertical();

            // Display name
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            var displayName = string.IsNullOrEmpty(npc.displayName) ? "Unnamed NPC" : npc.displayName;
            EditorGUILayout.LabelField(displayName, titleStyle);

            // NPC ID + faction
            EditorGUILayout.BeginHorizontal();
            var idStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.7f, 0.9f) }
            };
            EditorGUILayout.LabelField($"ID: {npc.npcId}", idStyle, GUILayout.Width(180));

            var factionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = FactionColor }
            };
            EditorGUILayout.LabelField($"⚑ {npc.faction}", factionStyle);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Quick button: select in Project
            if (GUILayout.Button("📍 Ping", EditorStyles.miniButton, GUILayout.Width(60)))
                EditorGUIUtility.PingObject(npc);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // SUMMARY
        // ══════════════════════════════════════════

        private void DrawSummary(NpcDefinition npc)
        {
            var boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 6, 6),
                richText = true
            };
            EditorGUILayout.BeginHorizontal(boxStyle);

            var sb = new System.Text.StringBuilder();

            // Quest offers
            int offerCount = (npc.questOfferRefs != null && npc.questOfferRefs.Length > 0)
                ? npc.questOfferRefs.Length
                : (npc.questOffers != null ? npc.questOffers.Length : 0);
            int turnInCount = (npc.questTurnInRefs != null && npc.questTurnInRefs.Length > 0)
                ? npc.questTurnInRefs.Length
                : (npc.questTurnIns != null ? npc.questTurnIns.Length : 0);

            sb.Append($"<b>📜 Offers:</b> <color=#88ccff>{offerCount}</color>    ");
            sb.Append($"<b>✅ Turn‑ins:</b> <color=#88ccff>{turnInCount}</color>    ");

            // Services
            if (npc.services != NpcService.None)
                sb.Append($"<b>🛠 Services:</b> <color=#ccff88>{npc.services}</color>    ");

            // Dialog tree
            sb.Append($"<b>💬 Tree:</b> ");
            if (npc.defaultDialogTree != null)
                sb.Append($"<color=#aaccff>{npc.defaultDialogTree.name}</color>");
            else
                sb.Append("<color=#ff9944>auto (fallback)</color>");

            EditorGUILayout.LabelField(sb.ToString(), new GUIStyle(EditorStyles.label) { richText = true, fontSize = 11 });
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════
        // BLOCK: IDENTITY
        // ══════════════════════════════════════════

        private void DrawIdentityBlock(NpcDefinition npc)
        {
            _showIdentity = DrawBlockHeader("🆔 Identity", _showIdentity);
            if (!_showIdentity) return;

            EditorGUILayout.BeginVertical(BlockBoxStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("npcId"), new GUIContent("NPC ID"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Display Name"));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("faction"), new GUIContent("Faction"));
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // BLOCK: VISUALS
        // ══════════════════════════════════════════

        private void DrawVisualsBlock()
        {
            _showVisuals = DrawBlockHeader("🖼 Visuals", _showVisuals);
            if (!_showVisuals) return;

            EditorGUILayout.BeginVertical(BlockBoxStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("portrait"), new GUIContent("Portrait"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("prefab"), new GUIContent("Prefab"));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("animatorTriggerPrefix"), new GUIContent("Animator Trigger Prefix"));
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // BLOCK: DIALOGUE
        // ══════════════════════════════════════════

        private void DrawDialogueBlock()
        {
            _showDialogue = DrawBlockHeader("💬 Dialogue", _showDialogue);
            if (!_showDialogue) return;

            EditorGUILayout.BeginVertical(BlockBoxStyle);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultDialogTree"),
                new GUIContent("Default Dialog Tree (drag .asset)"));

            if (serializedObject.FindProperty("defaultDialogTree").objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No DialogTree assigned. Runtime will auto-build a fallback tree\n" +
                    "from questOffers/questTurnIns/greetingText (T-Q28).",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // BLOCK: QUESTS (ключевое изменение)
        // ══════════════════════════════════════════

        private void DrawQuestsBlock(NpcDefinition npc)
        {
            _showQuests = DrawBlockHeader("📜 Quests", _showQuests);
            if (!_showQuests) return;

            EditorGUILayout.BeginVertical(BlockBoxStyle);

            // ── Quest Offers ──
            EditorGUILayout.LabelField("Offers (NPC can GIVE these quests):", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            var offerRefsProp = serializedObject.FindProperty("questOfferRefs");
            if (offerRefsProp != null && offerRefsProp.isArray)
            {
                DrawQuestArray(offerRefsProp, "Offer", "перетащи .asset квеста сюда",
                    "Квесты, которые этот NPC может предложить игроку");

                // Show legacy string fallback only if no object refs set
                if (offerRefsProp.arraySize == 0)
                {
                    EditorGUILayout.Space(2);
                    var offerStrProp = serializedObject.FindProperty("questOffers");
                    EditorGUILayout.PropertyField(offerStrProp, new GUIContent("Offer IDs (string[], legacy CSV)"), true);
                }
            }

            EditorGUILayout.Space(8);

            // ── Quest Turn-Ins ──
            EditorGUILayout.LabelField("Turn‑Ins (NPC ACCEPTS these quests back):", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            var turnInRefsProp = serializedObject.FindProperty("questTurnInRefs");
            if (turnInRefsProp != null && turnInRefsProp.isArray)
            {
                DrawQuestArray(turnInRefsProp, "Turn‑In", "перетащи .asset квеста сюда",
                    "Квесты, которые игрок может сдать этому NPC");

                if (turnInRefsProp.arraySize == 0)
                {
                    EditorGUILayout.Space(2);
                    var turnInStrProp = serializedObject.FindProperty("questTurnIns");
                    EditorGUILayout.PropertyField(turnInStrProp, new GUIContent("Turn‑In IDs (string[], legacy CSV)"), true);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuestArray(SerializedProperty arrayProp, string singularLabel, string placeholderText, string tooltip)
        {
            // Draw each element as a row with quest name preview
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var el = arrayProp.GetArrayElementAtIndex(i);
                if (el == null) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Quest object field
                var questRef = el.objectReferenceValue as QuestDefinition;
                var label = questRef != null
                    ? $"{(string.IsNullOrEmpty(questRef.displayName) ? questRef.questId : questRef.displayName)}"
                    : $"{singularLabel} #{i + 1}";

                EditorGUILayout.PropertyField(el, new GUIContent(label, tooltip));

                // Quest ID preview
                if (questRef != null)
                {
                    var idStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.4f, 0.7f, 1.0f) }
                    };
                    EditorGUILayout.LabelField(questRef.questId, idStyle, GUILayout.Width(140));
                }

                // Up/Down/Delete
                if (i > 0 && GUILayout.Button("▲", GUILayout.Width(22)))
                {
                    arrayProp.MoveArrayElement(i, i - 1);
                    break;
                }
                if (i < arrayProp.arraySize - 1 && GUILayout.Button("▼", GUILayout.Width(22)))
                {
                    arrayProp.MoveArrayElement(i, i + 1);
                    break;
                }
                if (GUILayout.Button("×", GUILayout.Width(22)))
                {
                    arrayProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            // Add button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"+ Add {singularLabel}", GUILayout.Width(140), GUILayout.Height(24)))
            {
                arrayProp.arraySize++;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Placeholder hint when empty
            if (arrayProp.arraySize == 0)
            {
                var hintRect = EditorGUILayout.GetControlRect(false, 36);
                var hintStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
                };
                EditorGUI.LabelField(hintRect, $"← {placeholderText}", hintStyle);
            }
        }

        // ══════════════════════════════════════════
        // BLOCK: SERVICES
        // ══════════════════════════════════════════

        private void DrawServicesBlock()
        {
            _showServices = DrawBlockHeader("🛠 Services", _showServices);
            if (!_showServices) return;

            EditorGUILayout.BeginVertical(BlockBoxStyle);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("services"), new GUIContent("Services (bitmask)"));

            // Show which services are active
            var svcProp = serializedObject.FindProperty("services");
            if (svcProp != null)
            {
                var svc = (NpcService)svcProp.enumValueFlag;
                EditorGUILayout.BeginHorizontal();
                DrawServiceChip("Trade", svc.HasFlag(NpcService.Trade));
                DrawServiceChip("Repair", svc.HasFlag(NpcService.Repair));
                DrawServiceChip("Refuel", svc.HasFlag(NpcService.Refuel));
                DrawServiceChip("Restock", svc.HasFlag(NpcService.Restock));
                DrawServiceChip("Banking", svc.HasFlag(NpcService.Banking));
                DrawServiceChip("Healing", svc.HasFlag(NpcService.Healing));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawServiceChip(string name, bool active)
        {
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = active ? new Color(0.3f, 0.7f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
            var chipStyle = new GUIStyle(EditorStyles.miniButton)
            {
                normal = { textColor = active ? Color.white : new Color(0.5f, 0.5f, 0.5f) }
            };
            GUILayout.Button(name, chipStyle, GUILayout.Width(60));
            GUI.backgroundColor = oldBg;
        }

        // ══════════════════════════════════════════
        // BLOCK: INTERACTION
        // ══════════════════════════════════════════

        private void DrawInteractionBlock()
        {
            _showInteraction = DrawBlockHeader("🤝 Interaction", _showInteraction);
            if (!_showInteraction) return;

            EditorGUILayout.BeginVertical(BlockBoxStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionRadius"),
                new GUIContent("Interaction Radius (m)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showGreeting"),
                new GUIContent("Show Greeting"));
            EditorGUILayout.EndHorizontal();

            var showGreeting = serializedObject.FindProperty("showGreeting").boolValue;
            if (showGreeting)
            {
                var greetingProp = serializedObject.FindProperty("greetingText");
                EditorGUILayout.PropertyField(greetingProp, new GUIContent("Greeting Text"));
            }

            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // BLOCK: ATTITUDE
        // ══════════════════════════════════════════

        private void DrawAttitudeBlock()
        {
            _showAttitude = DrawBlockHeader("📈 Attitude & Reputation Links", _showAttitude);
            if (!_showAttitude) return;

            EditorGUILayout.BeginVertical(BlockBoxStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("personalAttitudeMin"),
                new GUIContent("Min Attitude"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("personalAttitudeMax"),
                new GUIContent("Max Attitude"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("attitudeLinks"),
                new GUIContent("Cross-Faction Attitude Links"), true);

            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // BLOCK: AUDIO
        // ══════════════════════════════════════════

        private void DrawAudioBlock()
        {
            _showAudio = DrawBlockHeader("🔊 Audio (optional)", _showAudio);
            if (!_showAudio) return;

            EditorGUILayout.BeginVertical(BlockBoxStyle);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("voicePrefix"),
                new GUIContent("Voice Prefix"));
            if (string.IsNullOrEmpty(serializedObject.FindProperty("voicePrefix").stringValue))
            {
                EditorGUILayout.HelpBox("Voice lines disabled (prefix empty).", MessageType.None);
            }
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════

        private bool DrawBlockHeader(string label, bool expanded)
        {
            var style = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
            var rect = EditorGUILayout.GetControlRect(false, 22);
            return EditorGUI.Foldout(rect, expanded, label, true, style);
        }

        private GUIStyle BlockBoxStyle => new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8),
            margin = new RectOffset(4, 4, 2, 4)
        };
    }
}
#endif
