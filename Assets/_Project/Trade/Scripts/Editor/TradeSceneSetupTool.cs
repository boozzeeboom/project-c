#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ProjectC.Trade;
using ProjectC.Player;

namespace ProjectC.Trade.Editor
{
    /// <summary>
    /// Editor-скрипт для автоматической настройки торговой системы в сцене.
    /// Menu: Tools > Project C > Setup Trade Scene
    /// </summary>
    public class TradeSceneSetupEditor
    {
        [MenuItem("Tools/Project C/Setup Trade Scene")]
        public static void SetupTradeScene()
        {
            // 1. Находим первый LocationMarket (Market_Primium)
            var market = FindMarket("Market_Primium");
            if (market == null)
            {
                EditorUtility.DisplayDialog("Trade Setup", "Не найден Market_Primium_v01.asset!\nУбедись что он есть в Assets/_Project/Trade/Data/Markets/", "OK");
                return;
            }

            // 2. Создаём TradeTrigger
            var trigger = Object.FindAnyObjectByType<TradeTrigger>();
            if (trigger == null)
            {
                var go = new GameObject("TradeTrigger");
                go.transform.position = new Vector3(0, 1, 0);
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(5f, 4f, 5f);
                trigger = go.AddComponent<TradeTrigger>();
                Debug.Log("[TradeSetup] Создан TradeTrigger");
            }
            trigger.market = market;
            trigger.npcName = "Торговец Примум";
            Debug.Log("[TradeSetup] TradeTrigger настроен на Market_Primium");

            // 3. Создаём PlayerTradeStorage
            var storage = Object.FindAnyObjectByType<PlayerTradeStorage>();
            if (storage == null)
            {
                var go = new GameObject("PlayerTradeStorage");
                storage = go.AddComponent<PlayerTradeStorage>();
                Debug.Log("[TradeSetup] Создан PlayerTradeStorage");
            }

            // 4. Создаём TradeUI
            var tradeUI = Object.FindAnyObjectByType<TradeUI>();
            if (tradeUI == null)
            {
                var go = new GameObject("TradeUI");
                tradeUI = go.AddComponent<TradeUI>();
                Debug.Log("[TradeSetup] Создан TradeUI");
            }
            tradeUI.currentMarket = market;
            tradeUI.playerStorage = storage;
            if (tradeUI.tradeLocation == null)
                tradeUI.tradeLocation = trigger.transform;

            // 5. Добавляем CargoSystem на все ShipController
            var ships = Object.FindObjectsByType<ShipController>(FindObjectsInactive.Exclude);
            foreach (var ship in ships)
            {
                var cargo = ship.GetComponent<CargoSystem>();
                if (cargo == null)
                {
                    cargo = ship.gameObject.AddComponent<CargoSystem>();
                    Debug.Log($"[TradeSetup] Добавлен CargoSystem на {ship.gameObject.name}");
                }
            }

            // 6. TradeSceneSetup
            var sceneSetup = Object.FindAnyObjectByType<TradeSceneSetup>();
            if (sceneSetup == null)
            {
                var go = new GameObject("TradeSceneSetup");
                sceneSetup = go.AddComponent<TradeSceneSetup>();
                Debug.Log("[TradeSetup] Создан TradeSceneSetup");
            }
            sceneSetup.market = market;
            sceneSetup.tradeLocation = trigger.transform;

            EditorUtility.SetDirty(trigger);
            EditorUtility.SetDirty(tradeUI);
            EditorUtility.SetDirty(sceneSetup);

            Debug.Log("[TradeSetup] === ТОРГОВАЯ СИСТЕМА НАСТРОЕНА ===");
            EditorUtility.DisplayDialog("Trade Setup", "Торговая система настроена!\n\n" +
                "Создано:\n" +
                "- TradeTrigger (зона торговли)\n" +
                "- PlayerTradeStorage (склад игрока)\n" +
                "- TradeUI (интерфейс)\n" +
                "- CargoSystem на всех кораблях\n" +
                "- TradeSceneSetup (runtime)\n\n" +
                "Нажми Play и подойди к TradeTrigger, нажми E.", "OK");
        }

        private static LocationMarket FindMarket(string nameContains)
        {
            string[] guids = AssetDatabase.FindAssets($"t:LocationMarket {nameContains}");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<LocationMarket>(path);
            }
            return null;
        }
    }
}
#endif
