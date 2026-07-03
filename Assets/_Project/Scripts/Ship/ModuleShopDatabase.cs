using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// ModuleShopDatabase — база данных каталога модулей для ремонтного менеджера.
    /// Содержит список ModuleShopEntry. Назначается в RepairManager через инспектор.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Ship/Module Shop Database", fileName = "ModuleShopDatabase")]
    public class ModuleShopDatabase : ScriptableObject
    {
        [Header("Каталог модулей")]
        [Tooltip("Список записей каталога. Каждая запись — модуль + цена + ресурсы.")]
        public List<ModuleShopEntry> entries = new List<ModuleShopEntry>();

        /// <summary>
        /// Найти запись каталога по moduleId.
        /// </summary>
        public ModuleShopEntry FindEntry(string moduleId)
        {
            foreach (var entry in entries)
            {
                if (entry != null && entry.module != null && entry.module.moduleId == moduleId)
                    return entry;
            }
            return null;
        }
    }
}
