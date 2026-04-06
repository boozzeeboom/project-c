#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Trade;
using System.IO;

public class TradeAssetGenerator : EditorWindow
{
    [MenuItem("Tools/ProjectC/Generate Trade Test Assets")]
    public static void ShowWindow()
    {
        GenerateTestAssets();
    }

    public static void GenerateTestAssets()
    {
        string itemsPath = "Assets/_Project/Trade/Data/Items";
        string databasePath = "Assets/_Project/Trade/Data/TradeItemDatabase.asset";

        // Создаём папку если нет
        if (!Directory.Exists(itemsPath))
        {
            Directory.CreateDirectory(itemsPath);
            AssetDatabase.Refresh();
        }

        // 1. Мезий (канистра)
        TradeItemDefinition mesium = CreateOrFindItem<TradeItemDefinition>("TradeItem_Mesium_v01", itemsPath);
        if (mesium != null)
        {
            mesium.itemId = "mesium_canister_v01";
            mesium.displayName = "Мезий (канистра)";
            mesium.basePrice = 10f;
            mesium.weight = 10f;
            mesium.volume = 0.5f;
            mesium.slots = 1;
            mesium.isDangerous = true;
            mesium.isFragile = false;
            mesium.isContraband = false;
            mesium.requiredFaction = Faction.None;
            EditorUtility.SetDirty(mesium);
        }

        // 2. Антигравий (слиток)
        TradeItemDefinition antigrav = CreateOrFindItem<TradeItemDefinition>("TradeItem_Antigrav_v01", itemsPath);
        if (antigrav != null)
        {
            antigrav.itemId = "antigrav_ingot_v01";
            antigrav.displayName = "Антигравий (слиток)";
            antigrav.basePrice = 50f;
            antigrav.weight = 5f;
            antigrav.volume = 0.2f;
            antigrav.slots = 1;
            antigrav.isDangerous = false;
            antigrav.isFragile = false;
            antigrav.isContraband = false;
            antigrav.requiredFaction = Faction.None;
            EditorUtility.SetDirty(antigrav);
        }

        // 3. База данных
        TradeDatabase database = AssetDatabase.LoadAssetAtPath<TradeDatabase>(databasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<TradeDatabase>();
            AssetDatabase.CreateAsset(database, databasePath);
        }

        database.allItems.Clear();
        if (mesium != null) database.allItems.Add(mesium);
        if (antigrav != null) database.allItems.Add(antigrav);

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TradeAssetGenerator] Созданы тестовые ассеты торговли: 2 предмета + база данных");
        Debug.Log($"[TradeAssetGenerator] Мезий: {mesium.itemId}, {mesium.basePrice} CR, {mesium.weight} кг, isDangerous={mesium.isDangerous}");
        Debug.Log($"[TradeAssetGenerator] Антигравий: {antigrav.itemId}, {antigrav.basePrice} CR, {antigrav.weight} кг");
        Debug.Log($"[TradeAssetGenerator] База данных: {database.allItems.Count} предметов");
    }

    private static T CreateOrFindItem<T>(string assetName, string folderPath) where T : ScriptableObject
    {
        string path = $"{folderPath}/{assetName}.asset";
        T item = AssetDatabase.LoadAssetAtPath<T>(path);

        if (item == null)
        {
            item = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"[TradeAssetGenerator] Создан: {path}");
        }
        else
        {
            Debug.Log($"[TradeAssetGenerator] Найден существующий: {path}");
        }

        return item;
    }
}
#endif
