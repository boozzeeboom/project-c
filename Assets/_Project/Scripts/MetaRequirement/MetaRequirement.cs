// =====================================================================================
// MetaRequirement.cs — обобщённый "замок" для любых Interactable (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/MetaRequirement/00_OVERVIEW.md
//   • docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md
//
// Назначение: компонент-контейнер требований (1..N ItemData + логика All/Any/AtLeastN).
// Вешается на ЛЮБОЙ GameObject рядом с NetworkObject — корабль, блок, NPC, зона и т.д.
// На OnNetworkSpawn (server-only) регистрирует себя в MetaRequirementRegistry.
// ShipKeyServer проверяет наличие требуемых предметов в инвентаре игрока через
// InventoryWorld.HasAllItems/HasAnyItem/CountOf.
//
// MVP-граница (Этап 1):
//   • _consumeOnUse — поле есть, но НЕ реализовано (TODO: транзакционное потребление)
//   • _requiredItems[] — должен лежать в Resources/Items/ (как в Ship Key)
//   • Визуальный feedback (анимация) — на стороне самого Interactable (LockBox),
//     MetaRequirement только авторизует доступ и эмитит event.
// =====================================================================================

using Unity.Netcode;
using UnityEngine;
using ProjectC.Items;

namespace ProjectC.MetaRequirement
{
    /// <summary>
    /// Универсальный "замок" — проверяет, что у игрока в инвентаре есть требуемые предметы
    /// по логике All/Any/AtLeastN. Только авторизация; визуальное "открытие" делает
    /// сам Interactable (например, LockBox), подписанный на OnClientAllowed event.
    /// </summary>
    [DisallowMultipleComponent]
    public class MetaRequirement : NetworkBehaviour
    {
        [Header("Требования (предметы)")]
        [Tooltip("ItemData, которые требуются. Должны лежать в Resources/Items/. " +
                 "Порядок не важен; дубликаты игнорируются (HasAllItems через HashSet).")]
        [SerializeField] private ItemData[] _requiredItems = new ItemData[0];

        [Tooltip("All — нужны ВСЕ; Any — любой один; AtLeastN — минимум N разных (см. _requiredCount).")]
        [SerializeField] private RequirementLogic _logic = RequirementLogic.All;

        [Tooltip("Минимальное кол-во РАЗНЫХ предметов из списка (только для AtLeastN). " +
                 "1 = фактически как All при 1-элементном списке.")]
        [SerializeField, Min(1)] private int _requiredCount = 1;

        [Tooltip("ЗАБРАТЬ предметы из инвентаря после успешного использования. " +
                 "MVP: НЕ реализовано. Поле сохранено для v2 (транзакционное потребление).")]
        [SerializeField] private bool _consumeOnUse = false;

        [Header("UI / display")]
        [Tooltip("Человекочитаемое имя (для toast'а 'Нужен ключ для ...').")]
        [SerializeField] private string _interactableDisplayName = "Object";

        [Tooltip("Кастомное сообщение при отказе. Если пусто — генерируется автоматически " +
                 "(список недостающих предметов).")]
        [SerializeField] private string _customFailureMessage = "";

        // Server-side resolved itemIds. Заполняется на сервере при OnNetworkSpawn
        // через InventoryWorld.GetOrRegisterItemId(_requiredItems[i]).
        private int[] _serverItemIds = new int[0];

        // Cache lazy-resolved ids; после первого вызова — immutable до OnNetworkDespawn
        public int[] ServerItemIds
        {
            get
            {
                if (!IsServer) return System.Array.Empty<int>();
                if (_serverItemIds.Length != _requiredItems.Length)
                {
                    ResolveItemIds();
                }
                return _serverItemIds;
            }
        }

        // ===========================================================
        // Public read-only API (UI / scripts)
        // ===========================================================

        public string InteractableDisplayName =>
            string.IsNullOrEmpty(_interactableDisplayName) ? gameObject.name : _interactableDisplayName;

        public ItemData[] RequiredItems => _requiredItems;
        public RequirementLogic Logic => _logic;
        public int RequiredCount => _requiredCount;
        public bool ConsumeOnUse => _consumeOnUse;
        public string CustomFailureMessage => _customFailureMessage;

        // ===========================================================
        // Server-side authorization
        // ===========================================================

        /// <summary>Server-only: авторитетная проверка доступа. Причина (reason) —
        /// human-readable строка для toast'а (например "Нужен ключ для Синий Сундук").</summary>
        public bool CanPlayerUse(ulong clientId, out string reason)
        {
            reason = "";
            if (!IsServer) { reason = "not_server"; return false; }

            int[] ids = ServerItemIds;

            // Пустой список + All → trivially true (нет требований)
            if (ids == null || ids.Length == 0)
            {
                return _logic == RequirementLogic.All; // All пустой = OK; Any/AtLeastN пустой = нет
            }

            if (InventoryWorld.Instance == null)
            {
                reason = $"Нужен предмет для: {InteractableDisplayName}";
                return false;
            }

            bool allowed;
            switch (_logic)
            {
                case RequirementLogic.All:
                    allowed = InventoryWorld.Instance.HasAllItems(clientId, ids);
                    break;
                case RequirementLogic.Any:
                    allowed = InventoryWorld.Instance.HasAnyItem(clientId, ids);
                    break;
                case RequirementLogic.AtLeastN:
                    // Считаем сколько РАЗНЫХ itemId из списка у игрока есть
                    int have = CountUniqueHave(clientId, ids);
                    int need = Mathf.Max(1, _requiredCount);
                    allowed = have >= need;
                    break;
                default:
                    allowed = false;
                    break;
            }

            if (!allowed)
            {
                reason = !string.IsNullOrEmpty(_customFailureMessage)
                    ? _customFailureMessage
                    : BuildAutoReason(clientId, ids);
            }
            return allowed;
        }

        /// <summary>Server-only: прогресс игрока (для UI tooltip'а).</summary>
        public ProgressInfo GetPlayerProgress(ulong clientId)
        {
            var info = new ProgressInfo();
            int[] ids = ServerItemIds;
            if (ids == null) ids = System.Array.Empty<int>();

            int uniqueHave = CountUniqueHave(clientId, ids);
            int required;
            switch (_logic)
            {
                case RequirementLogic.All: required = ids.Length; break;
                case RequirementLogic.Any: required = 1; break;
                case RequirementLogic.AtLeastN: required = Mathf.Max(1, _requiredCount); break;
                default: required = ids.Length; break;
            }

            info.Required = required;
            info.Have = Mathf.Min(uniqueHave, required);
            info.MissingIds = InventoryWorld.Instance != null
                ? InventoryWorld.Instance.GetMissingItems(clientId, ids)
                : ids;
            info.Satisfied = CanPlayerUse(clientId, out _);
            return info;
        }

        // ===========================================================
        // Lifecycle (NetworkBehaviour)
        // ===========================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            ResolveItemIds();

            if (MetaRequirementRegistry.Instance != null)
            {
                MetaRequirementRegistry.Instance.RegisterRequirement(NetworkObjectId, this);
            }
            else
            {
                Debug.LogWarning($"[MetaRequirement] OnNetworkSpawn for interactable={NetworkObjectId} " +
                                 "но MetaRequirementRegistry.Instance==null. Регистрация пропущена.");
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && MetaRequirementRegistry.Instance != null)
            {
                MetaRequirementRegistry.Instance.UnregisterRequirement(NetworkObjectId);
            }
            base.OnNetworkDespawn();
        }

        // ===========================================================
        // Helpers
        // ===========================================================

        private void ResolveItemIds()
        {
            if (!IsServer) return;
            if (_requiredItems == null || _requiredItems.Length == 0)
            {
                _serverItemIds = new int[0];
                return;
            }
            if (InventoryWorld.Instance == null)
            {
                // Может быть null, если MetaRequirement.OnNetworkSpawn отработал раньше
                // InventoryServer.OnNetworkSpawn. Registry перевызовет lazy resolve при
                // первом CanPlayerUse через ServerItemIds getter.
                _serverItemIds = new int[_requiredItems.Length];
                for (int i = 0; i < _requiredItems.Length; i++) _serverItemIds[i] = -1;
                return;
            }
            _serverItemIds = new int[_requiredItems.Length];
            for (int i = 0; i < _requiredItems.Length; i++)
            {
                _serverItemIds[i] = _requiredItems[i] != null
                    ? InventoryWorld.Instance.GetOrRegisterItemId(_requiredItems[i])
                    : -1;
            }
        }

        /// <summary>Считает количество РАЗНЫХ (уникальных) itemId из списка, которые есть у игрока.</summary>
        private int CountUniqueHave(ulong clientId, int[] ids)
        {
            if (ids == null || ids.Length == 0) return 0;
            if (InventoryWorld.Instance == null) return 0;
            var missing = InventoryWorld.Instance.GetMissingItems(clientId, ids);
            int missingCount = missing != null ? missing.Length : 0;
            return ids.Length - missingCount;
        }

        /// <summary>Сгенерировать reason автоматически: список недостающих через имена.</summary>
        private string BuildAutoReason(ulong clientId, int[] ids)
        {
            if (InventoryWorld.Instance == null) return InteractableDisplayName;
            int[] missing = InventoryWorld.Instance.GetMissingItems(clientId, ids);
            if (missing == null || missing.Length == 0) return InteractableDisplayName;

            if (missing.Length == 1)
            {
                var def = InventoryWorld.Instance.GetItemDefinition(missing[0]);
                string itemName = def != null ? def.itemName : $"#{missing[0]}";
                return $"Нужен предмет: {itemName}";
            }
            // Несколько — выводим имена через запятую
            var sb = new System.Text.StringBuilder();
            sb.Append("Не хватает: ");
            for (int i = 0; i < missing.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                var def = InventoryWorld.Instance.GetItemDefinition(missing[i]);
                sb.Append(def != null ? def.itemName : $"#{missing[i]}");
            }
            return sb.ToString();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_requiredItems == null || _requiredItems.Length == 0)
            {
                Debug.LogWarning($"[MetaRequirement] '{gameObject.name}': _requiredItems пуст. " +
                                 "Если _logic=All — объект будет всегда доступен. " +
                                 "Если _logic=Any/AtLeastN — всегда заблокирован.", this);
            }
            // Проверка на дубликаты
            if (_requiredItems != null && _requiredItems.Length > 1)
            {
                var seen = new System.Collections.Generic.HashSet<ItemData>();
                foreach (var item in _requiredItems)
                {
                    if (item == null) continue;
                    if (!seen.Add(item))
                    {
                        Debug.LogWarning($"[MetaRequirement] '{gameObject.name}': дубликат ItemData={item.name} в _requiredItems. " +
                                         "HasAllItems игнорирует дубликаты, проверьте настройку.", this);
                    }
                }
            }
        }
#endif
    }
}
