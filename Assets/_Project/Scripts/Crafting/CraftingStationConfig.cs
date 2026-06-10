// =====================================================================================
// CraftingStationConfig.cs — конфигурация станции крафта (Project C: The Clouds, Crafting)
// =====================================================================================
// Документация:
//   • docs/Crafting_system/00_OVERVIEW.md
//   • docs/Crafting_system/10_DESIGN.md §2.1
//   • docs/Crafting_system/ROADMAP.md T-C01
//
// Назначение: ScriptableObject с параметрами одной станции крафта.
//   - displayName, icon, description — для UI
//   - stationType — категория станции (Shipyard / CraftingTable / ...)
//   - allowedRecipes — какие рецепты доступны на этой станции
//   - craftSpeedMultiplier — множитель времени крафта
//   - maxConcurrentJobs — сколько одновременных заказов (MVP: 1)
//   - interactRadius — дистанция взаимодействия (IInteractable)
//
// Станция НЕ хранит runtime-состояние — это статическая конфигурация.
// Runtime-состояние на CraftingStation (NetworkBehaviour) + CraftingWorld (POCO).
// =====================================================================================

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Crafting
{
    /// <summary>Тип станции крафта. Влияет на визуал + фильтр UI.</summary>
    public enum StationType
    {
        CraftingTable = 0,
        Shipyard = 1,
        Forge = 2,
        Loom = 3,
        Alchemy = 4,  // Phase 2+
    }

    [CreateAssetMenu(menuName = "Project C/Crafting/Station Config", fileName = "CraftingStation_", order = 10)]
    public class CraftingStationConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Имя станции, показываемое в UI при взаимодействии (Title окна).")]
        [SerializeField] private string _displayName;

        [Tooltip("Иконка станции для списка / tooltip.")]
        [SerializeField] private Sprite _icon;

        [Tooltip("Описание назначения станции (для UI).")]
        [TextArea] [SerializeField] private string _description;

        [Tooltip("Тип станции — влияет на какие рецепты предлагать и какой визуал.")]
        [SerializeField] private StationType _stationType = StationType.CraftingTable;

        [Header("Recipes")]
        [Tooltip("Какие рецепты можно крафтить на этой станции. " +
                 "Порядок — UI order. Дубликаты игнорируются.")]
        [SerializeField] private List<RecipeData> _allowedRecipes = new List<RecipeData>();

        [Header("Limits")]
        [Tooltip("Максимум одновременных заказов. MVP: жёстко 1 (range не даёт поднять).")]
        [Range(1, 1)] [SerializeField] private int _maxConcurrentJobs = 1;

        [Header("Timing")]
        [Tooltip("Множитель скорости крафта для этой станции. " +
                 "1.0 = базовая скорость по рецепту. 0.5 = вдвое быстрее.")]
        [Range(0.1f, 10f)] [SerializeField] private float _craftSpeedMultiplier = 1.0f;

        [Header("Interaction")]
        [Tooltip("Дистанция, с которой игрок может взаимодействовать со станцией (F / E). " +
                 "Стандартная: 4 метра (как NetworkChestContainer, ResourceNode).")]
        [Min(0.5f)] [SerializeField] private float _interactRadius = 4f;

        // === Публичный API ===

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public StationType StationType => _stationType;
        public IReadOnlyList<RecipeData> AllowedRecipes => _allowedRecipes.AsReadOnly();
        public int MaxConcurrentJobs => _maxConcurrentJobs;
        public float CraftSpeedMultiplier => _craftSpeedMultiplier;
        public float InteractRadius => _interactRadius;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_displayName))
            {
                Debug.LogWarning("[CraftingStationConfig] " + name + ": displayName не задан.");
            }

            if (_allowedRecipes == null || _allowedRecipes.Count == 0)
            {
                Debug.LogWarning("[CraftingStationConfig] " + name + ": allowedRecipes пуст — станция не сможет ничего крафтить.");
            }
        }
#endif
    }
}
