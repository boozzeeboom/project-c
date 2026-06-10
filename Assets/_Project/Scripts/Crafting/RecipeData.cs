// =====================================================================================
// RecipeData.cs — конфигурация рецепта крафта (Project C: The Clouds, Crafting)
// =====================================================================================
// Документация:
//   • docs/Crafting_system/00_OVERVIEW.md
//   • docs/Crafting_system/10_DESIGN.md §2.2
//   • docs/Crafting_system/ROADMAP.md T-C01
//
// Назначение: ScriptableObject с параметрами одного рецепта.
//   - displayName, icon, description — для UI
//   - ingredients: что нужно положить в буфер (ItemData + quantity)
//   - outputs: что выдаётся (ItemData + quantity XOR ShipKeyBinding)
//   - craftSeconds: базовое время крафта (умножается на station.craftSpeedMultiplier)
//   - requiredSkillLevel: задел на Phase 2 (0 = нет требования в MVP)
//
// Рецепты НЕ содержат стоимость в кредитах — это чисто ресурсный крафт.
// Для валютных затрат — отдельный бизнес-флоу (не в MVP).
//
// MVP-граница (T-C01):
//   - Один output за рецепт (список — задел на многовыходные рецепты Phase 2)
//   - ShipKeyBinding — для рецептов-кораблей (в MVP: только ItemData)
//   - Skill gate — не проверяется в MVP (int = 0 на SO, код не использует)
// =====================================================================================

using UnityEngine;
using ProjectC.Items;

namespace ProjectC.Crafting
{
    /// <summary>Категория рецепта для UI-фильтра.</summary>
    public enum RecipeCategory
    {
        Module = 0,
        Consumable = 1,
        Ship = 2,
        Material = 3,
        Misc = 4,
    }

    /// <summary>Навык для Phase 2 skill gate (MVP: не проверяется).</summary>
    public enum SkillType
    {
        None = 0,
        Engineering = 1,
        Piloting = 2,
        Trading = 3,
        Combat = 4,
    }

    /// <summary>Один ингредиент рецепта (что и сколько нужно положить в буфер).</summary>
    [System.Serializable]
    public struct RecipeIngredient
    {
        [Tooltip("Какой предмет нужен.")]
        public ItemData item;

        [Tooltip("Сколько штук.")]
        public int quantity;
    }

    /// <summary>Один output рецепта (что выдаётся после крафта).</summary>
    [System.Serializable]
    public struct RecipeOutput
    {
        [Tooltip("Обычный предмет выдачи. Если null — рецепт-корабль (Phase 2).")]
        public ItemData item;

        [Tooltip("Сколько штук выдаётся.")]
        public int quantity;
    }

    [CreateAssetMenu(menuName = "Project C/Crafting/Recipe", fileName = "CraftingRecipe_", order = 10)]
    public class RecipeData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Имя рецепта, показываемое в списке крафта и в окне станции.")]
        [SerializeField] private string _displayName;

        [Tooltip("Иконка рецепта (спрайт, 64x64).")]
        [SerializeField] private Sprite _icon;

        [Tooltip("Описание: что получается, из чего, для чего используется.")]
        [TextArea] [SerializeField] private string _description;

        [Tooltip("Категория рецепта для UI-фильтра / сортировки.")]
        [SerializeField] private RecipeCategory _category = RecipeCategory.Material;

        [Header("Inputs (ingredients)")]
        [Tooltip("Что нужно положить в буфер станции. Дубликаты НЕ допускаются (OnValidate).")]
        [SerializeField] private RecipeIngredient[] _ingredients;

        [Header("Outputs")]
        [Tooltip("Что выдаётся по завершению крафта. MVP: 1 элемент. Список — для будущих многовыходных рецептов.")]
        [SerializeField] private RecipeOutput[] _outputs;

        [Header("Timing")]
        [Tooltip("Сколько секунд серверного времени нужно для крафта. Умножается на station.craftSpeedMultiplier.")]
        [Min(1f)] [SerializeField] private float _craftSeconds = 600f;

        [Header("Skill Gate (Phase 2)")]
        [Tooltip("MVP: 0 = нет требования. Phase 2: минимальный уровень навыка для крафта.")]
        [Min(0)] [SerializeField] private int _requiredSkillLevel = 0;

        [Tooltip("Какой навык нужен (Phase 2). MVP: None = не проверяется.")]
        [SerializeField] private SkillType _requiredSkill = SkillType.None;

        // ---------- Server-resolved item ids (кеш, выставляется при старте) ----------
        private int[] _ingredientItemIds;
        private int[] _outputItemIds;

        // ---------- Публичный API ----------

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public RecipeCategory Category => _category;
        public RecipeIngredient[] Ingredients => _ingredients;
        public RecipeOutput[] Outputs => _outputs;
        public float CraftSeconds => _craftSeconds;
        public int RequiredSkillLevel => _requiredSkillLevel;
        public SkillType RequiredSkill => _requiredSkill;

        public int[] IngredientItemIds => _ingredientItemIds;
        public int[] OutputItemIds => _outputItemIds;

        /// <summary>
        /// Резолвит ItemData -> int id через InventoryWorld.
        /// Вызывается на сервере при регистрации рецепта (CraftingWorld.RegisterRecipe).
        /// На клиенте не нужен.
        /// </summary>
        public void ResolveItemIds()
        {
            if (InventoryWorld.Instance == null)
            {
                Debug.LogWarning("[RecipeData] ResolveItemIds: InventoryWorld.Instance == null. Id останутся null.");
                return;
            }

            if (_ingredients != null)
            {
                _ingredientItemIds = new int[_ingredients.Length];
                for (int i = 0; i < _ingredients.Length; i++)
                {
                    _ingredientItemIds[i] = _ingredients[i].item != null
                        ? InventoryWorld.Instance.GetOrRegisterItemId(_ingredients[i].item)
                        : -1;
                }
            }

            if (_outputs != null)
            {
                _outputItemIds = new int[_outputs.Length];
                for (int i = 0; i < _outputs.Length; i++)
                {
                    _outputItemIds[i] = _outputs[i].item != null
                        ? InventoryWorld.Instance.GetOrRegisterItemId(_outputs[i].item)
                        : -1;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_outputs == null || _outputs.Length == 0)
                Debug.LogWarning("[RecipeData] " + name + ": нет outputs. Рецепт не будет работать.");

            if (_ingredients == null || _ingredients.Length == 0)
                Debug.LogWarning("[RecipeData] " + name + ": нет ingredients. Рецепт не имеет затрат.");

            // Проверка дубликатов ingredients по itemName
            if (_ingredients != null && _ingredients.Length > 1)
            {
                for (int i = 0; i < _ingredients.Length; i++)
                {
                    if (_ingredients[i].item == null) continue;
                    for (int j = i + 1; j < _ingredients.Length; j++)
                    {
                        if (_ingredients[j].item == null) continue;
                        if (_ingredients[i].item.itemName == _ingredients[j].item.itemName)
                        {
                            Debug.LogWarning("[RecipeData] " + name + ": дубликат ingredient '"
                                + _ingredients[i].item.itemName + "' на позициях " + i + " и " + j);
                        }
                    }
                }
            }

            // Сбрасываем кеш id — пере-ResolveItemIds будет на сервере
            _ingredientItemIds = null;
            _outputItemIds = null;
        }
#endif
    }
}
