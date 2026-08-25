using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace ProjectC.Localization.Editor
{
    public static class ValidateQ001DialogueLocalization
    {
        private const string SharedPath = "Assets/_Project/Settings/Localization/Dialogue_Table Shared Data.asset";
        private const string RuPath = "Assets/_Project/Settings/Localization/Dialogue_Table_ru.asset";
        private const string EnPath = "Assets/_Project/Settings/Localization/Dialogue_Table_en.asset";
        private static readonly string[] DialogPaths =
        {
            "Assets/_Project/Quests/Data/Dialogs/dlg_lyra_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_bram_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_veska_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_noll_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_kael_q001.asset",
            "Assets/_Project/Quests/Data/Dialogs/dlg_sela_q001.asset"
        };

        [MenuItem("ProjectC/Localization/Q001/Stage 4 - Validate Dialogue Localization")]
        public static void Execute()
        {
            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(SharedPath);
            var ru = AssetDatabase.LoadAssetAtPath<StringTable>(RuPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(EnPath);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var oldPrefix = 0;
            var dangling = 0;
            var unreachable = 0;
            var nodeCount = 0;

            foreach (var path in DialogPaths)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                var so = new SerializedObject(asset);
                var local = new HashSet<string>(StringComparer.Ordinal);
                Add(so.FindProperty("displayName"), keys, local, ref oldPrefix);
                var nodes = so.FindProperty("nodes");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < nodes.arraySize; i++)
                    ids.Add(nodes.GetArrayElementAtIndex(i).FindPropertyRelative("nodeId").stringValue);
                nodeCount += nodes.arraySize;
                for (var i = 0; i < nodes.arraySize; i++)
                {
                    var node = nodes.GetArrayElementAtIndex(i);
                    Add(node.FindPropertyRelative("text"), keys, local, ref oldPrefix);
                    var edges = node.FindPropertyRelative("edges");
                    for (var j = 0; j < edges.arraySize; j++)
                    {
                        var edge = edges.GetArrayElementAtIndex(j);
                        Add(edge.FindPropertyRelative("label"), keys, local, ref oldPrefix);
                        var target = edge.FindPropertyRelative("targetNodeId").stringValue;
                        if (!string.IsNullOrEmpty(target) && !ids.Contains(target)) dangling++;
                    }
                }

                var reachable = new HashSet<string>(StringComparer.Ordinal);
                var queue = new Queue<string>();
                queue.Enqueue(so.FindProperty("rootNodeId").stringValue);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    if (!reachable.Add(id) || !ids.Contains(id)) continue;
                    var node = Enumerable.Range(0, nodes.arraySize)
                        .Select(nodes.GetArrayElementAtIndex)
                        .First(n => n.FindPropertyRelative("nodeId").stringValue == id);
                    var edges = node.FindPropertyRelative("edges");
                    for (var j = 0; j < edges.arraySize; j++)
                    {
                        var target = edges.GetArrayElementAtIndex(j).FindPropertyRelative("targetNodeId").stringValue;
                        if (!string.IsNullOrEmpty(target)) queue.Enqueue(target);
                    }
                }
                unreachable += ids.Count - reachable.Count;
            }

            var missingShared = keys.Count(k => shared.GetEntry(k) == null);
            var missingRu = keys.Count(k => ru.GetEntry(k) == null);
            var missingEn = keys.Count(k => en.GetEntry(k) == null);
            var invalid = keys.Count(k =>
            {
                var r = ru.GetEntry(k)?.LocalizedValue;
                var e = en.GetEntry(k)?.LocalizedValue;
                return string.IsNullOrWhiteSpace(r) || string.IsNullOrWhiteSpace(e) || r == k || e == k;
            });
            var pass = keys.Count == 102 && nodeCount == 42 && oldPrefix == 0 && dangling == 0 && unreachable == 0 && missingShared == 0 && missingRu == 0 && missingEn == 0 && invalid == 0;
            Debug.Log($"[Q001 Stage4] {(pass ? "PASS" : "FAIL")}: q001Keys={keys.Count}; nodes={nodeCount}; oldPrefixes={oldPrefix}; dangling={dangling}; unreachable={unreachable}; missingShared={missingShared}; missingRU={missingRu}; missingEN={missingEn}; invalidTranslations={invalid}; dialogueSharedTotal={shared.Entries.Count}; dialogueRuTotal={ru.Values.Count(v => v != null)}; dialogueEnTotal={en.Values.Count(v => v != null)}.");
        }

        private static void Add(SerializedProperty property, HashSet<string> all, HashSet<string> local, ref int oldPrefix)
        {
            if (property == null || property.propertyType != SerializedPropertyType.String) return;
            var value = property.stringValue;
            if (value.StartsWith("dialog.", StringComparison.Ordinal)) oldPrefix++;
            if (value.StartsWith("dialogue.dlg_", StringComparison.Ordinal) || value.StartsWith("dialogue.option.common.", StringComparison.Ordinal))
            {
                all.Add(value);
                local.Add(value);
            }
        }
    }
}
