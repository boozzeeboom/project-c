using UnityEngine;

namespace ProjectC.Items
{
    public enum ItemType
    {
        Resources = 0,
        Equipment = 1,
        Food = 2,
        Fuel = 3,
        Antigrav = 4,
        Meziy = 5,
        Medical = 6,
        Tech = 7
    }

    [CreateAssetMenu(fileName = "NewItem", menuName = "Project C/Item Data", order = 1)]
    public class ItemData : ScriptableObject
    {
        public string itemName;
        public ItemType itemType;
        [TextArea]
        public string description;
        public Sprite icon;
    }
}
