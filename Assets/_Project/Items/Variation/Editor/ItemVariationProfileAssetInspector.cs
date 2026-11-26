// =====================================================================================
// ItemVariationProfileAssetInspector.cs — инспектор профиля вариации (T-VAR01)
// =====================================================================================
// Документация: docs/pickble_items/variation_generator/03_EDITOR_TOOL.md
// =====================================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectC.Items.Variation.EditorTools
{
    [CustomEditor(typeof(ItemVariationProfileAsset))]
    public class ItemVariationProfileAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var asset = (ItemVariationProfileAsset)target;
            if (asset == null || asset.profile == null) return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Авторинг", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture base stats from item"))
            {
                Undo.RecordObject(asset, "Capture variation base stats");
                asset.CaptureFromItem(GetItemId(asset.baseItem));
                EditorUtility.SetDirty(asset);
            }
            if (GUILayout.Button("Open in Generator"))
                ItemVariationWindow.OpenWith(asset);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Validate"))
            {
                var issues = ItemVariationProfileFactory.Validate(asset.profile);
                int errors = 0;
                int warnings = 0;
                for (int i = 0; i < issues.Count; i++)
                {
                    if (issues[i].severity == ItemVariationSeverity.Error) errors++;
                    else if (issues[i].severity == ItemVariationSeverity.Warning) warnings++;
                    if (issues[i].severity != ItemVariationSeverity.Info)
                        Debug.Log($"[ItemVariation] {asset.name}: {issues[i].severity} — {issues[i].message}");
                }
                EditorUtility.DisplayDialog(
                    "Validate variation profile",
                    errors == 0
                        ? $"Ошибок: 0, предупреждений: {warnings}. Подробности в Console (если есть)."
                        : $"Ошибок: {errors}, предупреждений: {warnings}. Подробности в Console.",
                    "OK");
            }

            if (GUILayout.Button("Export server JSON…"))
            {
                string folder = "Assets/_Project/Items/Data/Variation";
                EnsureFolder(folder);
                string fileName = string.IsNullOrEmpty(asset.profile.profileId)
                    ? asset.name
                    : asset.profile.profileId;
                string path = EditorUtility.SaveFilePanelInProject(
                    "Export variation profile for server",
                    fileName + "_server.json", "json",
                    "JSON с профилем и снапшотом базовых статов", folder);
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllText(path, asset.ToServerJson());
                    AssetDatabase.Refresh();
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                }
            }
        }

        private static int GetItemId(ItemData item)
        {
            if (item == null) return 0;
            var registry = Resources.Load<ItemRegistry>("Items/Data/ItemRegistry");
            if (registry != null && registry.TryGetId(item, out int id)) return id;
            return 0;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path);
            string leaf = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            parent = parent.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
