using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Items
{
    [System.Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Tooltip("Шанс выпадения (0-1). 1 = 100%")]
        [Range(0f, 1f)]
        public float chance = 1f;
        [Tooltip("Минимальное количество")]
        public int minCount = 1;
        [Tooltip("Максимальное количество")]
        public int maxCount = 1;
    }

    /// <summary>
    /// ScriptableObject таблицы добычи для сундуков/контейнеров.
    /// Определяет какие предметы и с каким шансом выпадают.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "Project C/Loot Table", order = 2)]
    public class LootTable : ScriptableObject
    {
        [Tooltip("Список возможных предметов")]
        public List<LootEntry> entries = new List<LootEntry>();

        [Tooltip("Гарантированные предметы (игнорируют шанс)")]
        public List<ItemData> guaranteedItems = new List<ItemData>();

        [Header("Credits (T-NPC-12)")]
        [Tooltip("Минимальное количество кредитов. Если min=max=0 — кредиты не выпадают.")]
        [Range(0, 10000)] public int minCredits = 0;

        [Tooltip("Максимальное количество кредитов.")]
        [Range(0, 10000)] public int maxCredits = 0;

        /// <summary>
        /// Сгенерировать случайное количество кредитов из [minCredits, maxCredits].
        /// Если оба 0 — возвращает 0 (no credits).
        /// </summary>
        public int GenerateCredits()
        {
            if (minCredits <= 0 && maxCredits <= 0) return 0;
            int lo = Mathf.Max(0, minCredits);
            int hi = Mathf.Max(lo, maxCredits);
            return Random.Range(lo, hi + 1);
        }

        /// <summary>
        /// Сгенерировать список предметов из таблицы.
        /// </summary>
        public List<ItemData> GenerateLoot()
        {
            var result = new List<ItemData>();

            // Гарантированные предметы
            foreach (var item in guaranteedItems)
            {
                if (item != null)
                    result.Add(item);
            }

            // Предметы по шансу
            foreach (var entry in entries)
            {
                if (entry.item == null) continue;

                int count = Random.Range(entry.minCount, entry.maxCount + 1);
                for (int i = 0; i < count; i++)
                {
                    if (Random.value <= entry.chance)
                    {
                        result.Add(entry.item);
                    }
                }
            }

            return result;
        }
    }
}
