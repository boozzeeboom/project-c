using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// ShipModuleCatalog — статический реестр всех ShipModule в проекте.
    /// Инициализируется из ModuleShopDatabase при загрузке.
    /// Используется ShipModuleServer для lookup'а модулей по moduleId на клиенте.
    /// </summary>
    public static class ShipModuleCatalog
    {
        private static readonly Dictionary<string, ShipModule> _modulesById = new Dictionary<string, ShipModule>();
        private static bool _initialized;

        /// <summary>Инициализировать каталог из ModuleShopDatabase.</summary>
        public static void Initialize(ModuleShopDatabase database)
        {
            if (_initialized) return;
            if (database == null)
            {
                Debug.LogWarning("[ShipModuleCatalog] Initialize: database is null");
                return;
            }

            _modulesById.Clear();
            foreach (var entry in database.entries)
            {
                if (entry != null && entry.module != null && !string.IsNullOrEmpty(entry.module.moduleId))
                {
                    if (!_modulesById.ContainsKey(entry.module.moduleId))
                        _modulesById[entry.module.moduleId] = entry.module;
                }
            }

            _initialized = true;
            Debug.Log($"[ShipModuleCatalog] Initialized with {_modulesById.Count} modules");
        }

        /// <summary>Сбросить каталог (при смене сцены).</summary>
        public static void Reset()
        {
            _modulesById.Clear();
            _initialized = false;
        }

        /// <summary>Найти ShipModule по moduleId.</summary>
        public static ShipModule Find(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return null;
            _modulesById.TryGetValue(moduleId, out var module);
            return module;
        }

        /// <summary>Все зарегистрированные модули.</summary>
        public static IEnumerable<ShipModule> AllModules => _modulesById.Values;

        /// <summary>Количество модулей в каталоге.</summary>
        public static int Count => _modulesById.Count;
    }
}
