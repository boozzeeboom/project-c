using System;
using System.Collections.Generic;
using ProjectC.Items;
using UnityEngine;

namespace ProjectC.Admin
{
    /// <summary>
    /// T-ADM-10: локальный каталог всех ItemData для вкладки «Выдача».
    /// Источник id — ItemRegistry (единый id-контракт с сервером); экипировка
    /// (Clothing/Modules/Weapons) в реестре отсутствует, у неё id = 0 и на сервер
    /// уходит имя ассета — InventoryServer резолвит его в runtime-базу.
    /// Каталог строится лениво, только когда открыта вкладка «Выдача».
    /// </summary>
    public sealed class AdminItemEntry
    {
        public int Id { get; }
        public string AssetName { get; }
        public string DisplayName { get; }
        public ItemType Type { get; }
        public int BasePrice { get; }

        public string TypeLabel => AdminItemCatalog.TypeLabel(Type);

        public AdminItemEntry(int id, string assetName, string displayName, ItemType type, int basePrice)
        {
            Id = id;
            AssetName = assetName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Type = type;
            BasePrice = basePrice;
        }
    }

    public static class AdminItemCatalog
    {
        private static readonly List<AdminItemEntry> _entries = new List<AdminItemEntry>(1200);
        private static bool _loaded;

        public static IReadOnlyList<AdminItemEntry> All
        {
            get
            {
                if (!_loaded) Reload();
                return _entries;
            }
        }

        public static void Reload()
        {
            _entries.Clear();

            var registry = ItemRegistry.Instance;
            if (registry == null)
            {
                registry = Resources.Load<ItemRegistry>("ItemRegistry");
                if (registry == null)
                    registry = Resources.Load<ItemRegistry>("Items/Data/ItemRegistry");
                if (registry != null) ItemRegistry.SetInstance(registry);
            }

            var seen = new HashSet<ItemData>();
            if (registry != null)
            {
                var registryEntries = registry.GetEntries();
                for (int i = 0; i < registryEntries.Count; i++)
                {
                    var item = registryEntries[i].item;
                    if (item == null || !seen.Add(item)) continue;
                    _entries.Add(new AdminItemEntry(
                        registryEntries[i].id,
                        item.name,
                        DisplayNameOf(item),
                        item.itemType,
                        item.basePrice));
                }
            }

            // Resources.LoadAll рекурсивен: захватывает Items/Clothing, /Modules, /Weapons и т.д.
            var all = Resources.LoadAll<ItemData>("Items");
            for (int i = 0; i < all.Length; i++)
            {
                var item = all[i];
                if (item == null || !seen.Add(item)) continue;
                int id = 0;
                if (registry != null && registry.TryGetId(item, out int registryId)) id = registryId;
                _entries.Add(new AdminItemEntry(
                    id,
                    item.name,
                    DisplayNameOf(item),
                    item.itemType,
                    item.basePrice));
            }

            _entries.Sort((a, b) =>
            {
                int byType = ((int)a.Type).CompareTo((int)b.Type);
                if (byType != 0) return byType;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            _loaded = true;
            Debug.Log($"[AdminItemCatalog] Загружено предметов: {_entries.Count}");
        }

        public static string TypeLabel(ItemType type)
        {
            switch (type)
            {
                case ItemType.Resources: return "Ресурсы";
                case ItemType.Equipment: return "Экипировка";
                case ItemType.Food: return "Еда";
                case ItemType.Fuel: return "Топливо";
                case ItemType.Antigrav: return "Антиграв";
                case ItemType.Meziy: return "Мезий";
                case ItemType.Medical: return "Медицина";
                case ItemType.Tech: return "Техника";
                case ItemType.Key: return "Ключ";
                default: return type.ToString();
            }
        }

        private static string DisplayNameOf(ItemData item)
        {
            return string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName;
        }
    }
}
