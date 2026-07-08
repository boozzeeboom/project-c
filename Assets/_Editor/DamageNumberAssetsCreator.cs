// Editor script: creates DamageNumberConfig_Default.asset + PF_DamageNumber.prefab
// Run once via execute_script.

using UnityEngine;
using UnityEditor;
using TMPro;

public static class DamageNumberAssetsCreator
{
    public static void Execute()
    {
        CreateConfig();
        CreatePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DamageNumberAssetsCreator] Done: Config + Prefab created.");
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

    private static void CreatePrefab()
    {
        string dir = "Assets/_Project/Resources/Prefabs";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        string prefabPath = dir + "/PF_DamageNumber.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.Log("[DamageNumberAssetsCreator] PF_DamageNumber.prefab already exists, skipping.");
            return;
        }

        // Root GO
        var root = new GameObject("PF_DamageNumber");

        // World Space Canvas
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 100; // above most UI

        var rectTransform = root.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200f, 60f);
        rectTransform.localScale = Vector3.one * 0.01f; // world-space canvas scale

        // TMP Text child
        var textGo = new GameObject("DamageText");
        textGo.transform.SetParent(root.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "999";
        tmp.fontSize = 36f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = TMPro.FontStyles.Bold;

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

        Debug.Log("[DamageNumberAssetsCreator] Created PF_DamageNumber.prefab with " + (prefab != null ? "success" : "failure"));
    }
}
