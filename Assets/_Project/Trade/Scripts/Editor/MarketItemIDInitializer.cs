#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ProjectC.Trade;

/// <summary>
/// Editor-инструмент для инициализации MarketItem.itemId в существующих LocationMarket.
/// Сессия 8D: добавлено поле itemId для восстановления ссылок при сериализации.
/// 
/// Использование:
/// - Tools → Project C → Trade → Initialize Market Item IDs
/// - Автоматически запускается при открытии если есть неинициализированные рынки
/// </summary>
public class MarketItemIDInitializer : EditorWindow
{
    [MenuItem("Tools/Project C/Trade/Initialize Market Item IDs")]
    public static void ShowWindow()
    {
        var window = GetWindow<MarketItemIDInitializer>("MarketItem ID Initializer");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private Vector2 _scrollPosition;
    private string _log = "";

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("MarketItem ID Initializer", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        EditorGUILayout.HelpBox(
            "Инициализирует поле itemId во всех MarketItem на основе TradeItemDefinition.\n" +
            "Сессия 8D: itemId используется для восстановления ссылок при потере item reference.",
            MessageType.Info);
        
        EditorGUILayout.Space(10);

        if (GUILayout.Button("Инициализировать все рынки", GUILayout.Height(30)))
        {
            InitializeAllMarkets();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Проверить все рынки", GUILayout.Height(30)))
        {
            CheckAllMarkets();
        }

        EditorGUILayout.Space(10);

        if (!string.IsNullOrEmpty(_log))
        {
            EditorGUILayout.LabelField("Лог:", EditorStyles.boldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// Инициализировать itemId во всех LocationMarket ScriptableObject
    /// </summary>
    public static void InitializeAllMarkets()
    {
        string log = "";
        int marketCount = 0;
        int itemInitCount = 0;

        string[] guids = AssetDatabase.FindAssets("t:LocationMarket");
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var market = AssetDatabase.LoadAssetAtPath<LocationMarket>(path);
            
            if (market == null)
            {
                log += $"⚠️ Не удалось загрузить рынок: {path}\n";
                continue;
            }

            marketCount++;
            log += $"\n📦 Рынок: {market.locationName} ({market.locationId})\n";

            foreach (var marketItem in market.items)
            {
                if (marketItem == null) continue;

                bool wasEmpty = string.IsNullOrEmpty(marketItem.itemId);
                
                // Инициализируем itemId из TradeItemDefinition
                if (marketItem.item != null)
                {
                    marketItem.itemId = marketItem.item.itemId;
                    marketItem.basePrice = marketItem.item.basePrice;
                    
                    if (wasEmpty)
                    {
                        itemInitCount++;
                        log += $"  ✅ {marketItem.item.displayName}: itemId='{marketItem.itemId}'\n";
                    }
                    else
                    {
                        log += $"  ✓ {marketItem.item.displayName}: itemId='{marketItem.itemId}' (уже было)\n";
                    }
                }
                else
                {
                    log += $"  ❌ item == null! itemId='{marketItem.itemId ?? "NULL"}'\n";
                }
            }

            // Помечаем рынок как изменённый
            EditorUtility.SetDirty(market);
        }

        log += $"\n📊 Итого: {marketCount} рынков, {itemInitCount} предметов инициализировано\n";
        
        if (marketCount > 0)
        {
            AssetDatabase.SaveAssets();
            log += "💾 Сохранено!\n";
        }
        else
        {
            log += "⚠️ Рынки не найдены! Проверьте Assets/_Project/Trade/Data/Markets/\n";
        }

        Debug.Log(log);
    }

    /// <summary>
    /// Проверить состояние всех рынков без модификации
    /// </summary>
    public static void CheckAllMarkets()
    {
        string log = "";
        int marketCount = 0;
        int nullItemCount = 0;
        int nullItemIdCount = 0;

        string[] guids = AssetDatabase.FindAssets("t:LocationMarket");
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var market = AssetDatabase.LoadAssetAtPath<LocationMarket>(path);
            
            if (market == null) continue;

            marketCount++;
            log += $"\n📦 Рынок: {market.locationName} ({market.locationId})\n";
            log += $"  Предметов: {market.items.Count}\n";

            foreach (var marketItem in market.items)
            {
                if (marketItem == null)
                {
                    log += $"  ⚠️ marketItem == null!\n";
                    nullItemCount++;
                    continue;
                }

                string itemStatus = marketItem.item != null ? marketItem.item.displayName : "NULL";
                string itemIdStatus = !string.IsNullOrEmpty(marketItem.itemId) ? marketItem.itemId : "NULL";
                
                if (marketItem.item == null) nullItemCount++;
                if (string.IsNullOrEmpty(marketItem.itemId)) nullItemIdCount++;

                log += $"  [{(marketItem.item != null ? "✓" : "❌")}] {itemStatus} | itemId: {itemIdStatus} | price: {marketItem.currentPrice:F0}\n";
            }
        }

        log += $"\n📊 Итого: {marketCount} рынков\n";
        log += $"  ⚠️ item == null: {nullItemCount}\n";
        log += $"  ⚠️ itemId пустой: {nullItemIdCount}\n";

        if (nullItemCount > 0)
        {
            log += "\n❌ Есть проблемы с ссылками! Запустите 'Инициализировать все рынки'\n";
        }
        else if (nullItemIdCount > 0)
        {
            log += "\n⚠️ itemId не установлен! Запустите 'Инициализировать все рынки'\n";
        }
        else
        {
            log += "\n✅ Все рынки в порядке!\n";
        }

        Debug.Log(log);
    }

    /// <summary>
    /// Автоматическая проверка при открытии Unity Editor
    /// </summary>
    [InitializeOnLoadMethod]
    private static void AutoCheckOnStartup()
    {
        // Проверяем только при первом запуске Unity
        if (SessionState.GetBool("MarketIDAutoChecked", false)) return;
        SessionState.SetBool("MarketIDAutoChecked", true);

        string[] guids = AssetDatabase.FindAssets("t:LocationMarket");
        bool needsInit = false;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var market = AssetDatabase.LoadAssetAtPath<LocationMarket>(path);
            
            if (market != null)
            {
                foreach (var marketItem in market.items)
                {
                    if (marketItem != null && string.IsNullOrEmpty(marketItem.itemId) && marketItem.item != null)
                    {
                        needsInit = true;
                        break;
                    }
                }
            }
            if (needsInit) break;
        }

        if (needsInit)
        {
            Debug.LogWarning("[MarketItemIDInitializer] Обнаружены неинициализированные рынки. Запустите Tools → Project C → Trade → Initialize Market Item IDs");
        }
    }
}
#endif
