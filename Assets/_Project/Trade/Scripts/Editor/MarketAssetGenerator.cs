#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Trade;
using System.IO;
using System.Collections.Generic;

public class MarketAssetGenerator : EditorWindow
{
    [MenuItem("Tools/ProjectC/Generate Trade Markets")]
    public static void ShowWindow()
    {
        GenerateMarketAssets();
    }

    public static void GenerateMarketAssets()
    {
        string marketsPath = "Assets/_Project/Trade/Data/Markets";
        string databasePath = "Assets/_Project/Trade/Data/TradeItemDatabase.asset";

        if (!Directory.Exists(marketsPath))
        {
            Directory.CreateDirectory(marketsPath);
            AssetDatabase.Refresh();
        }

        // Загружаем базу товаров
        TradeDatabase database = AssetDatabase.LoadAssetAtPath<TradeDatabase>(databasePath);
        if (database == null)
        {
            Debug.LogError("[MarketAssetGenerator] TradeItemDatabase.asset не найден! Сначала запусти 'Generate Trade Test Assets'.");
            return;
        }

        if (database.allItems.Count == 0)
        {
            Debug.LogError("[MarketAssetGenerator] TradeItemDatabase пуст! Сначала запусти 'Generate Trade Test Assets'.");
            return;
        }

        // Определение рынков (GDD_25 секция 3, GDD_22 секция 3)
        var marketDefs = new[]
        {
            new MarketDef
            {
                id = "primium",
                name = "Примум (Столица)",
                file = "Market_Primium_v01",
                // Мезий дёшево (производство), антигравий дёшево (Аврора)
                priceModifiers = new Dictionary<string, float>
                {
                    { "mesium_canister_v01", 0.0f },    // 10 CR (базовая)
                    { "antigrav_ingot_v01", -0.2f },    // 40 CR (дешевле на 20%)
                },
                stockModifiers = new Dictionary<string, int>
                {
                    { "mesium_canister_v01", 100 },
                    { "antigrav_ingot_v01", 50 },
                }
            },
            new MarketDef
            {
                id = "secundus",
                name = "Секундус (Военная база)",
                file = "Market_Secundus_v01",
                // Мезий средне, броня дёшево (Титан)
                priceModifiers = new Dictionary<string, float>
                {
                    { "mesium_canister_v01", 0.2f },     // 12 CR
                    { "antigrav_ingot_v01", 0.1f },      // 55 CR
                },
                stockModifiers = new Dictionary<string, int>
                {
                    { "mesium_canister_v01", 60 },
                    { "antigrav_ingot_v01", 30 },
                }
            },
            new MarketDef
            {
                id = "tertius",
                name = "Тертиус (Торговый хаб)",
                file = "Market_Tertius_v01",
                // Латекс дёшево (Гермес), всё средне
                priceModifiers = new Dictionary<string, float>
                {
                    { "mesium_canister_v01", 0.1f },     // 11 CR
                    { "antigrav_ingot_v01", 0.15f },     // 57.5 CR (дефицит)
                },
                stockModifiers = new Dictionary<string, int>
                {
                    { "mesium_canister_v01", 70 },
                    { "antigrav_ingot_v01", 40 },
                }
            },
            new MarketDef
            {
                id = "quartus",
                name = "Квартус (Научный центр)",
                file = "Market_Quartus_v01",
                // Мезий дорого (удалённость), МНП дёшево (Прометей)
                priceModifiers = new Dictionary<string, float>
                {
                    { "mesium_canister_v01", 0.5f },     // 15 CR (дорого)
                    { "antigrav_ingot_v01", 0.2f },      // 60 CR
                },
                stockModifiers = new Dictionary<string, int>
                {
                    { "mesium_canister_v01", 30 },
                    { "antigrav_ingot_v01", 60 },
                }
            },
        };

        int createdCount = 0;

        foreach (var def in marketDefs)
        {
            string assetPath = $"{marketsPath}/{def.file}.asset";
            LocationMarket market = AssetDatabase.LoadAssetAtPath<LocationMarket>(assetPath);

            if (market == null)
            {
                market = ScriptableObject.CreateInstance<LocationMarket>();
                AssetDatabase.CreateAsset(market, assetPath);
                Debug.Log($"[MarketAssetGenerator] Создан: {assetPath}");
            }
            else
            {
                Debug.Log($"[MarketAssetGenerator] Обновлён: {assetPath}");
            }

            market.locationId = def.id;
            market.locationName = def.name;
            market.items.Clear();

            // Добавляем все товары из базы с модификаторами
            foreach (var item in database.allItems)
            {
                if (item == null) continue;

                var marketItem = new MarketItem
                {
                    item = item,
                    basePrice = item.basePrice,
                };

                // Применяем модификатор цены
                if (def.priceModifiers.TryGetValue(item.itemId, out float priceMod))
                {
                    marketItem.currentPrice = item.basePrice * (1f + priceMod);
                }
                else
                {
                    marketItem.currentPrice = item.basePrice;
                }

                // Применяем модификатор стока
                if (def.stockModifiers.TryGetValue(item.itemId, out int stockMod))
                {
                    marketItem.availableStock = stockMod;
                }
                else
                {
                    marketItem.availableStock = 50; // дефолт
                }

                // Начальные факторы спроса/предложения
                marketItem.demandFactor = 0f;
                marketItem.supplyFactor = 0f;

                market.items.Add(marketItem);
            }

            EditorUtility.SetDirty(market);
            createdCount++;

            // Логируем цены для проверки
            Debug.Log($"[MarketAssetGenerator] {market.locationName}:");
            foreach (var mi in market.items)
            {
                if (mi.item != null)
                    Debug.Log($"  - {mi.item.displayName}: {mi.currentPrice:F1} CR (сток: {mi.availableStock})");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MarketAssetGenerator] Создано/обновлено {createdCount} рынков");
    }

    private struct MarketDef
    {
        public string id;
        public string name;
        public string file;
        public Dictionary<string, float> priceModifiers;
        public Dictionary<string, int> stockModifiers;
    }
}
#endif
