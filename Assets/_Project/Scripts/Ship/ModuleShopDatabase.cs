using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// ModuleShopDatabase — база данных каталога модулей для ремонтного менеджера.
    /// Содержит список ShipModule (с ценами и ресурсами внутри самого модуля).
    /// Назначается в RepairManager через инспектор.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Ship/Module Shop Database", fileName = "ModuleShopDatabase")]
    public class ModuleShopDatabase : ScriptableObject
    {
        [Header("Каталог модулей")]
        [Tooltip("Список модулей. Цена и ресурсы — в самом ShipModule.")]
        public List<ShipModule> entries = new List<ShipModule>();

        /// <summary>
        /// Найти модуль в каталоге по moduleId.
        /// </summary>
        public ShipModule FindEntry(string moduleId)
        {
            foreach (var mod in entries)
            {
                if (mod != null && mod.moduleId == moduleId)
                    return mod;
            }
            return null;
        }
    }
}
