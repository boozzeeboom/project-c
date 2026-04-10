namespace ProjectC.Items
{
    /// <summary>
    /// Семантические названия типов для UI (локализованные)
    /// </summary>
    public static class ItemTypeNames
    {
        private static readonly string[] _names = new string[]
        {
            "Ресурсы",
            "Оборудование",
            "Еда",
            "Топливо",
            "Антигравий",
            "Мезий",
            "Медикаменты",
            "Техника"
        };

        public static string GetDisplayName(ItemType type)
        {
            int idx = (int)type;
            return idx >= 0 && idx < _names.Length ? _names[idx] : type.ToString();
        }
    }
}
