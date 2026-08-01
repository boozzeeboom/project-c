// Project C: Knowledge System V3
// KnowledgeRevealTriggerEditor: кастомный инспектор с preview названий и валидацией.
// Design: docs/Character/Knowledges/07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md §4 V3.10

using UnityEditor;
using UnityEngine;
using ProjectC.Skills;
using ProjectC.Crafting;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Knowledge.Editor
{
    [CustomEditor(typeof(KnowledgeRevealTrigger))]
    public class KnowledgeRevealTriggerEditor : UnityEditor.Editor
    {
        private SerializedProperty _skillsToReveal;
        private SerializedProperty _recipesToReveal;
        private SerializedProperty _factionsToReveal;
        private SerializedProperty _npcsToReveal;
        private SerializedProperty _triggerOnce;
        private SerializedProperty _playerTags;
        private SerializedProperty _onRevealed;

        private bool _foldoutSkills = true;
        private bool _foldoutRecipes = true;
        private bool _foldoutFactions = true;
        private bool _foldoutNpcs = true;

        private void OnEnable()
        {
            _skillsToReveal = serializedObject.FindProperty("skillsToReveal");
            _recipesToReveal = serializedObject.FindProperty("recipesToReveal");
            _factionsToReveal = serializedObject.FindProperty("factionsToReveal");
            _npcsToReveal = serializedObject.FindProperty("npcsToReveal");
            _triggerOnce = serializedObject.FindProperty("triggerOnce");
            _playerTags = serializedObject.FindProperty("playerTags");
            _onRevealed = serializedObject.FindProperty("onRevealed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Knowledge Reveal Trigger", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_triggerOnce);
            EditorGUILayout.PropertyField(_playerTags);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Assets to Reveal", EditorStyles.boldLabel);

            // Skills
            _foldoutSkills = EditorGUILayout.Foldout(_foldoutSkills,
                $"Skills ({_skillsToReveal.arraySize})", true);
            if (_foldoutSkills)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_skillsToReveal);
                // Preview names
                for (int i = 0; i < _skillsToReveal.arraySize; i++)
                {
                    var elem = _skillsToReveal.GetArrayElementAtIndex(i);
                    var obj = elem.objectReferenceValue as SkillNodeConfig;
                    if (obj != null)
                    {
                        string name = !string.IsNullOrEmpty(obj.displayName) ? obj.displayName : obj.skillId;
                        string status = "";
                        if (string.IsNullOrEmpty(obj.skillId)) status = " ⚠ empty skillId";
                        EditorGUILayout.LabelField($"  [{i}] {name}{status}", EditorStyles.miniLabel);
                    }
                }
                EditorGUI.indentLevel--;
            }

            // Recipes
            _foldoutRecipes = EditorGUILayout.Foldout(_foldoutRecipes,
                $"Recipes ({_recipesToReveal.arraySize})", true);
            if (_foldoutRecipes)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_recipesToReveal);
                for (int i = 0; i < _recipesToReveal.arraySize; i++)
                {
                    var elem = _recipesToReveal.GetArrayElementAtIndex(i);
                    var obj = elem.objectReferenceValue as RecipeData;
                    if (obj != null)
                    {
                        string status = "";
                        if (string.IsNullOrEmpty(obj.RecipeId)) status = " ⚠ empty recipeId";
                        EditorGUILayout.LabelField($"  [{i}] {obj.DisplayName}{status}", EditorStyles.miniLabel);
                    }
                }
                EditorGUI.indentLevel--;
            }

            // Factions
            _foldoutFactions = EditorGUILayout.Foldout(_foldoutFactions,
                $"Factions ({_factionsToReveal.arraySize})", true);
            if (_foldoutFactions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_factionsToReveal);
                for (int i = 0; i < _factionsToReveal.arraySize; i++)
                {
                    var elem = _factionsToReveal.GetArrayElementAtIndex(i);
                    var obj = elem.objectReferenceValue as FactionDefinition;
                    if (obj != null)
                    {
                        EditorGUILayout.LabelField($"  [{i}] {obj.displayName} ({obj.factionId})", EditorStyles.miniLabel);
                    }
                }
                EditorGUI.indentLevel--;
            }

            // NPCs
            _foldoutNpcs = EditorGUILayout.Foldout(_foldoutNpcs,
                $"NPCs ({_npcsToReveal.arraySize})", true);
            if (_foldoutNpcs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_npcsToReveal);
                for (int i = 0; i < _npcsToReveal.arraySize; i++)
                {
                    var elem = _npcsToReveal.GetArrayElementAtIndex(i);
                    var obj = elem.objectReferenceValue as NpcDefinition;
                    if (obj != null)
                    {
                        EditorGUILayout.LabelField($"  [{i}] {obj.displayName} ({obj.npcId})", EditorStyles.miniLabel);
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_onRevealed);

            // Summary
            EditorGUILayout.Space();
            int total = _skillsToReveal.arraySize + _recipesToReveal.arraySize
                      + _factionsToReveal.arraySize + _npcsToReveal.arraySize;
            EditorGUILayout.HelpBox($"Total assets to reveal: {total}\n" +
                                    $"  Skills: {_skillsToReveal.arraySize}\n" +
                                    $"  Recipes: {_recipesToReveal.arraySize}\n" +
                                    $"  Factions: {_factionsToReveal.arraySize}\n" +
                                    $"  NPCs: {_npcsToReveal.arraySize}",
                                    MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}