using UnityEngine;
using ProjectC.Items;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// Автоматически регистрирует все предметы из Resources и сцены в NetworkInventory.
    /// Вешается на стартовый объект сцены.
    /// </summary>
    public class ItemDatabaseInitializer : MonoBehaviour
    {
        [Header("Настройки")]
        [SerializeField] private string itemsResourcePath = "Items";
        [SerializeField] private int startItemId = 1;

        private void Awake()
        {
            RegisterAllItems();
        }

        /// <summary>
        /// Загрузить все предметы из Resources и сцены
        /// </summary>
        private void RegisterAllItems()
        {
            var allItems = new List<ItemData>();

            // 1. Из Resources
            var resourceItems = Resources.LoadAll<ItemData>(itemsResourcePath);
            allItems.AddRange(resourceItems);

            // 2. Из PickupItem на сцене
            var pickups = FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
            foreach (var pickup in pickups)
            {
                if (pickup.itemData != null && !allItems.Contains(pickup.itemData))
                {
                    allItems.Add(pickup.itemData);
                }
            }

            // 3. Из ChestContainer (LootTable) на сцене
            var chests = FindObjectsByType<ChestContainer>(FindObjectsInactive.Include);
            foreach (var chest in chests)
            {
                if (chest.lootTable != null)
                {
                    // entries
                    if (chest.lootTable.entries != null)
                    {
                        foreach (var entry in chest.lootTable.entries)
                        {
                            if (entry.item != null && !allItems.Contains(entry.item))
                            {
                                allItems.Add(entry.item);
                            }
                        }
                    }

                    // guaranteedItems
                    if (chest.lootTable.guaranteedItems != null)
                    {
                        foreach (var item in chest.lootTable.guaranteedItems)
                        {
                            if (item != null && !allItems.Contains(item))
                            {
                                allItems.Add(item);
                            }
                        }
                    }
                }
            }

            // Регистрируем все найденные предметы
            int id = startItemId;
            foreach (var item in allItems)
            {
                NetworkInventory.RegisterItem(id, item);
                id++;
            }

        }
    }
}
