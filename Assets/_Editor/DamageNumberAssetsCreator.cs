// Editor script: creates/updates PF_DamageNumber.prefab with Billboard component.
// Run via execute_script.

using UnityEngine;
using UnityEditor;
using TMPro;

public static class DamageNumberAssetsCreator
{
    public static void Execute()
    {
        CreateConfig();
        RecreatePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DamageNumberAssetsCreator] Done.");
    }

    private static void CreateConfig()
    {
        string dir = "Assets/_Project/Resources/Combat";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        string path = dir + "/DamageNumberConfig_Default.asset";
        if (AssetDatabase.LoadAssetAtPath<ProjectC.Combat.Config.DamageNumberConfig>(path) != null)
        {
            Debug.Log("[DamageNumberAssetsCreator] DamageNumberConfig_Default.asset already exists, skipping.");
            return;
        }

        var config = ScriptableObject.CreateInstance<ProjectC.Combat.Config.DamageNumberConfig>();
        AssetDatabase.CreateAsset(config, path);
        Debug.Log("[DamageNumberAssetsCreator] Created DamageNumberConfig_Default.asset");
    }

    private static void RecreatePrefab()
    {
        string dir = "Assets/_Project/Resources/Prefabs";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        string prefabPath = dir + "/PF_DamageNumber.prefab";

        // Delete old prefab if exists
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
            Debug.Log("[DamageNumberAssetsCreator] Deleted old PF_DamageNumber.prefab");
        }

        // Root GO
        var root = new GameObject("PF_DamageNumber");

        // World Space Canvas
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var rectTransform = root.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200f, 60f);
        rectTransform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        // Billboard component (from ProjectC.UI)
        var billboard = root.AddComponent<ProjectC.UI.Billboard>();
        billboard.keepVertical = true;

        // TMP Text child
        var textGo = new GameObject("DamageText");
        textGo.transform.SetParent(root.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "999";
        tmp.fontSize = 36f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        // Рекомендуем SDF-шрифт для масштабирования без потери качества
        var defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (defaultFont != null) tmp.font = defaultFont;

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.sizeDelta = Vector2.zero;
        textRt.anchoredPosition = Vector2.zero;

        // DamageNumberInstance component
        var instance = root.AddComponent<ProjectC.Combat.Client.DamageNumberInstance>();

        // Serialize references via reflection
        var serializedObj = new SerializedObject(instance);
        serializedObj.FindProperty("_tmpText").objectReferenceValue = tmp;
        serializedObj.FindProperty("_canvas").objectReferenceValue = canvas;
        serializedObj.FindProperty("_rectTransform").objectReferenceValue = rectTransform;
        serializedObj.ApplyModifiedPropertiesWithoutUndo();

        // Save prefab
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("[DamageNumberAssetsCreator] Recreated PF_DamageNumber.prefab (with Billboard)");
    }
}
