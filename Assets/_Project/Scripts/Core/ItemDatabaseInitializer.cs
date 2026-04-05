using UnityEngine;
using ProjectC.Items;

namespace ProjectC.Core
{
    /// <summary>
    /// Автоматически регистрирует все предметы из Resources в NetworkInventory.
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
        /// Загрузить все предметы из Resources и зарегистрировать
        /// </summary>
        private void RegisterAllItems()
        {
            var items = Resources.LoadAll<ItemData>(itemsResourcePath);
            
            if (items.Length == 0)
            {
                Debug.LogWarning($"[ItemDatabase] Не найдено предметов в Resources/{itemsResourcePath}. Создай ScriptableObject предметы!");
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                int itemId = startItemId + i;
                NetworkInventory.RegisterItem(itemId, items[i]);
                Debug.Log($"[ItemDatabase] Зарегистрирован: ID {itemId} - {items[i].itemName}");
            }

            Debug.Log($"[ItemDatabase] Всего зарегистрировано: {items.Length}");
        }
    }
}
