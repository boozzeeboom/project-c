// =====================================================================================
// ResourceNodeConfig.cs — конфигурация узла сбора (Project C: The Clouds, ResourceNode)
// =====================================================================================
// Документация:
//   • docs/Mining/00_OVERVIEW.md
//   • docs/Mining/10_DESIGN.md §1.1
//   • docs/Mining/ROADMAP.md T-G01
//
// Назначение: ScriptableObject с параметрами одного типа ресурсного узла.
//   - что выпадает (_resultItem, _resultItemType)
//   - какой инструмент нужен (_requiredTool) — null = без требований
//   - время сбора (_gatherSeconds), кол-во попыток (_maxHarvests), перезарядка (_cooldownSeconds)
//   - параметры анимации (scale-pulse + emissive, паттерн LockBox)
//
// Поведение:
//   - ResolveItemIds() вызывается на сервере в OnNetworkSpawn (через MetaRequirementRegistry
//     parent-компонент) и на клиенте для UI-строки (itemName, icon).
//   - ResultItemId / RequiredToolId — кешированные int id в InventoryWorld (после resolve).
//
// MVP-граница (T-G01):
//   - Один предмет за сбор (1 шт). Random yield (1-3) — post-MVP.
//   - _requiredTool — только проверка наличия. Tool durability — post-MVP.
// =====================================================================================

using UnityEngine;
using ProjectC.Items;

namespace ProjectC.ResourceNode
{
    [CreateAssetMenu(fileName = "ResourceNode_", menuName = "Project C/Resource Node Config", order = 2)]
    public class ResourceNodeConfig : ScriptableObject
    {
        [Header("Result")]
        [Tooltip("Предмет, который игрок получает после сбора (ItemData SO).")]
        [SerializeField] private ItemData _resultItem;

        [Tooltip("Тип предмета (enum ItemType). Используется InventoryWorld при добавлении в инвентарь. " +
                 "Если задан _resultItem — берётся из него, иначе это поле.")]
        [SerializeField] private ItemType _resultItemType = ItemType.Resources;

        [Header("Tool (optional)")]
        [Tooltip("Инструмент, который должен быть у игрока. null = инструмент не требуется. " +
                 "Проверка через MetaRequirement компонент на ResourceNode, не через это поле напрямую.")]
        [SerializeField] private ItemData _requiredTool;

        [Header("Gather Timing")]
        [Tooltip("Сколько секунд идёт сбор одной единицы ресурса.")]
        [SerializeField] [Min(0.1f)] private float _gatherSeconds = 3f;

        [Tooltip("Сколько раз подряд можно собрать с узла до истощения. После — cooldown.")]
        [SerializeField] [Min(1)] private int _maxHarvests = 5;

        [Tooltip("Сколько секунд узел нельзя собирать после истощения. После — Idle (можно собирать).")]
        [SerializeField] [Min(0f)] private float _cooldownSeconds = 60f;

        [Tooltip("Максимальная дистанция игрока до узла при нажатии F. Внутри этой дистанции " +
                 "F-key начинает сбор, дальше — игнорируется.")]
        [SerializeField] [Min(0.5f)] private float _gatherRange = 3f;

        [Header("UI / Display")]
        [Tooltip("Имя узла, показываемое в тосте. Если пусто — берётся _resultItem.itemName.")]
        [SerializeField] private string _nodeDisplayName = "";

        [Header("Gather Animation (client, паттерн LockBox)")]
        [Tooltip("Амплитуда пульсации scale во время сбора (0 = без анимации, 0.15 = ±15%).")]
        [SerializeField] [Range(0f, 0.5f)] private float _animScaleAmplitude = 0.15f;

        [Tooltip("Период пульсации scale в секундах. 0.4 = 2.5 цикла/сек.")]
        [SerializeField] [Range(0.1f, 1.5f)] private float _animPulsePeriod = 0.4f;

        [Tooltip("Длительность анимации появления/исчезания (scale 0→1 или 1→0) в секундах.")]
        [SerializeField] [Range(0.1f, 1.5f)] private float _animHiddenDuration = 0.3f;

        [Header("Emissive Colors (client animation)")]
        [Tooltip("Цвет emission материала в покое (Idle).")]
        [SerializeField] private Color _animIdleEmission = new Color(0.05f, 0.05f, 0.1f);

        [Tooltip("Цвет emission при сборе (Occupied) — к которому пульсирует анимация.")]
        [SerializeField] private Color _animGatherEmission = new Color(0.3f, 1.5f, 0.3f);

        // === Server-resolved item ids (кеш, выставляется в OnNetworkSpawn) ===
        private int _resultItemId = -1;
        private int _requiredToolId = -1;

        // === Публичный API ===

        public ItemData ResultItem => _resultItem;
        public ItemData RequiredTool => _requiredTool;
        public ItemType ResultItemType =>
            _resultItem != null ? _resultItem.itemType : _resultItemType;
        public float GatherSeconds => _gatherSeconds;
        public int MaxHarvests => _maxHarvests;
        public float CooldownSeconds => _cooldownSeconds;
        public float GatherRange => _gatherRange;

        public string NodeDisplayName =>
            !string.IsNullOrEmpty(_nodeDisplayName)
                ? _nodeDisplayName
                : _resultItem != null ? _resultItem.itemName : "Ресурс";

        public float AnimScaleAmplitude => _animScaleAmplitude;
        public float AnimPulsePeriod => _animPulsePeriod;
        public float AnimHiddenDuration => _animHiddenDuration;
        public Color AnimIdleEmission => _animIdleEmission;
        public Color AnimGatherEmission => _animGatherEmission;

        public int ResultItemId => _resultItemId;
        public int RequiredToolId => _requiredToolId;

        /// <summary>
        /// Резолвит ItemData → int id через InventoryWorld. Вызывается на сервере в
        /// ResourceNode.OnNetworkSpawn (после MetaRequirement). На клиенте не нужен.
        /// </summary>
        public void ResolveItemIds()
        {
            if (InventoryWorld.Instance == null)
            {
                Debug.LogWarning("[ResourceNodeConfig] ResolveItemIds: InventoryWorld.Instance == null. " +
                                 "Id останутся -1. ResourceNode будет deny при попытке сбора.");
                return;
            }

            _resultItemId = _resultItem != null
                ? InventoryWorld.Instance.GetOrRegisterItemId(_resultItem)
                : -1;

            _requiredToolId = _requiredTool != null
                ? InventoryWorld.Instance.GetOrRegisterItemId(_requiredTool)
                : -1;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Если пользователь в Editor поменял _resultItem/_requiredTool — сбрасываем кеш id.
            // ResolveItemIds() будет вызван на сервере при следующем OnNetworkSpawn.
            // В editor-режиме мы не можем знать их int id до запуска, поэтому просто warn.
            if (_resultItemId > 0 && _resultItem == null)
            {
                Debug.LogWarning("[ResourceNodeConfig] " + name + ": _resultItem был null, но _resultItemId > 0. " +
                                 "Скоуп-копия старого _resultItem, пере-ResolveItemIds на сервере при следующем OnNetworkSpawn.");
            }
        }
#endif
    }
}
