using UnityEngine;

namespace ProjectC.Items
{
    public enum ItemType
    {
        Type1 = 0,
        Type2 = 1,
        Type3 = 2,
        Type4 = 3,
        Type5 = 4,
        Type6 = 5,
        Type7 = 6,
        Type8 = 7
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
