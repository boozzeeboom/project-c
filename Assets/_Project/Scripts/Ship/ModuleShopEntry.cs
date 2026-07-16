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
    /// DEPRECATED (T-MOD03): ModuleShopEntry объединён с ShipModule.
    /// Цена и ресурсы теперь в ShipModule.costCredits / ShipModule.requiredResources.
    /// ModuleShopDatabase.entries теперь List&lt;ShipModule&gt;.
    /// Этот класс оставлен для справки; старые ShopEntry_*.asset файлы можно удалить.
    /// </summary>
    [Obsolete("Use ShipModule.costCredits + ShipModule.requiredResources instead. ModuleShopDatabase.entries is now List<ShipModule>.")]
    [CreateAssetMenu(menuName = "ProjectC/Ship/Module Shop Entry (DEPRECATED)", fileName = "ShopEntry_")]
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
