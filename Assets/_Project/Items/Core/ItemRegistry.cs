using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Items
{
    /// <summary>
    /// T-Q26: single source of truth for ItemData → int id mapping.
    /// Loaded at startup, used by InventoryWorld и QuestWorld.
    /// Заменяет двойную нумерацию (Resources.LoadAll scan) на explicit registration.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "ProjectC/Items/Item Registry")]
    public class ItemRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public int id;
            public ItemData item;
        }

        [Tooltip("Explicit list of all items with their ids. Order = id assignment (1, 2, 3...).")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        // Runtime cache: id → item, item → id.
        private Dictionary<int, ItemData> _byId;
        private Dictionary<ItemData, int> _byItem;

        public static ItemRegistry Instance { get; private set; }

        public void EnsureLoaded()
        {
            if (Instance == null)
            {
                // Try load from Resources as fallback.
                Instance = Resources.Load<ItemRegistry>("ItemRegistry");
            }
            if (Instance == null)
            {
                Debug.LogError("[ItemRegistry] No instance set AND no Resources/ItemRegistry.asset found. " +
                    "Create one via menu 'ProjectC/Items/Item Registry' and assign manually.");
                return;
            }
            Instance.BuildCache();
        }

        public static void SetInstance(ItemRegistry registry)
        {
            Instance = registry;
            Instance?.BuildCache();
        }

        private void BuildCache()
        {
            _byId = new Dictionary<int, ItemData>();
            _byItem = new Dictionary<ItemData, int>();
            foreach (var e in entries)
            {
                if (e.item == null) continue;
                if (_byId.ContainsKey(e.id))
                {
                    Debug.LogWarning($"[ItemRegistry] Duplicate id {e.id} for {e.item.itemName}, skipping");
                    continue;
                }
                _byId[e.id] = e.item;
                _byItem[e.item] = e.id;
            }
            Debug.Log($"[ItemRegistry] Loaded {_byId.Count} items from {name}");
        }

        public bool TryGetItem(int id, out ItemData item)
        {
            if (_byId == null) BuildCache();
            return _byId.TryGetValue(id, out item);
        }

        public bool TryGetId(ItemData item, out int id)
        {
            if (_byItem == null) BuildCache();
            return _byItem.TryGetValue(item, out id);
        }

        public int GetIdOrDefault(ItemData item, int defaultId = 0)
        {
            return TryGetId(item, out int id) ? id : defaultId;
        }

        public ItemData GetItemOrDefault(int id, ItemData defaultItem = null)
        {
            return TryGetItem(id, out var item) ? item : defaultItem;
        }

        /// <summary>Resolve by name (item.itemName). Used for legacy quest assets.</summary>
        public bool TryGetIdByName(string itemName, out int id)
        {
            id = 0;
            if (string.IsNullOrEmpty(itemName)) return false;
            if (_byItem == null) BuildCache();
            foreach (var e in entries)
            {
                if (e.item == null) continue;
                if (e.item.itemName == itemName)
                {
                    id = e.id;
                    return true;
                }
            }
            return false;
        }

        public IReadOnlyList<Entry> GetEntries() => entries;
        public int Count => entries?.Count ?? 0;

        public void AddEntry(int id, ItemData item)
        {
            if (item == null) return;
            entries.Add(new Entry { id = id, item = item });
            BuildCache();
        }
    }
}
