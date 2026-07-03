using System;
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Пара (itemId, amount) для требования ресурса на установку модуля.
    /// </summary>
    [Serializable]
    public struct ResourceRequirement
    {
        [Tooltip("ID предмета (itemId) из базы ItemData")]
        public string itemId;

        [Tooltip("Требуемое количество")]
        [Min(1)]
        public int amount;
    }

    /// <summary>
    /// ModuleShopEntry — запись в каталоге модулей ремонтного менеджера.
    /// Связывает ShipModule с ценой в кредитах и требуемыми ресурсами.
    /// Используется RepairManager + RepairManagerWindow.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Ship/Module Shop Entry", fileName = "ShopEntry_")]
    public class ModuleShopEntry : ScriptableObject
    {
        [Header("Модуль")]
        [Tooltip("Ссылка на ShipModule ScriptableObject")]
        public ShipModule module;

        [Header("Цена")]
        [Tooltip("Стоимость установки в кредитах")]
        [Min(0)]
        public int costCredits;

        [Header("Ресурсы")]
        [Tooltip("Список ресурсов, требуемых для установки (itemId + количество)")]
        public ResourceRequirement[] requiredResources;
    }
}
