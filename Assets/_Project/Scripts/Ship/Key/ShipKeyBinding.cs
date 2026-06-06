// =====================================================================================
// ShipKeyBinding.cs — серверный компонент привязки корабль↔ключ (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/00_OVERVIEW.md
//
// Назначение: вешается на ShipController (рядом с NetworkObject). Содержит серверную
// привязку между этим кораблём и ItemData-ключом. На OnNetworkSpawn (server-only)
// регистрирует себя в ShipKeyServer. ShipKeyServer выдаёт связь по запросу.
//
// Это НЕ NetworkBehaviour-hub (он — ShipKeyServer). Этот компонент — пассивный
// контейнер данных + lifecycle hooks. Удобно для дизайнеров: "вот этот ключ —
// к этому кораблю", указываем в инспекторе.
//
// MVP-граница: одна привязка на один ShipController. Несколько ключей /
// комбинированные замки — вне MVP.
// =====================================================================================

using Unity.Netcode;
using UnityEngine;
using ProjectC.Items;

namespace ProjectC.Ship.Key
{
    /// <summary>
    /// Связь "этот корабль ↔ этот ItemData-ключ". Хранится на самом ShipController.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShipKeyBinding : NetworkBehaviour
    {
        [Header("Ключ корабля (ItemData)")]
        [Tooltip("ItemData-ключ, без которого игрок НЕ сможет сесть в корабль по F. " +
                 "Должен лежать в Resources/Items/ (тогда InventoryWorld его подхватит при StartHost).")]
        [SerializeField] private ItemData _keyItemData;

        [Header("Отображение")]
        [Tooltip("Человекочитаемое имя корабля (для toast'а 'Нет ключа корабля (...)').")]
        [SerializeField] private string _shipDisplayName = "Корабль";

        // Server-side resolved keyItemId. Заполняется на сервере при OnNetworkSpawn
        // через InventoryWorld.GetOrRegisterItemId(_keyItemData). Клиенту не шлём —
        // он получает весь реестр через ShipKeyServer.PushBindingsRpc.
        private int _serverKeyItemId = -1;

        // Public read-only API
        public string ShipDisplayName =>
            string.IsNullOrEmpty(_shipDisplayName) ? gameObject.name : _shipDisplayName;

        public ItemData KeyItemData => _keyItemData;

        /// <summary>Server-only. Получить resolved keyItemId из InventoryWorld.
        /// -1 если _keyItemData == null или сервер ещё не разрешил.</summary>
        public int ServerKeyItemId
        {
            get
            {
                if (!IsServer) return -1;
                if (_serverKeyItemId > 0) return _serverKeyItemId;
                ResolveKeyItemId();
                return _serverKeyItemId;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer) return;

            // Резолвим keyItemId (если ItemData не задан — _serverKeyItemId остаётся -1,
            // и в реестре этот корабль будет помечен как "ключ не требуется").
            ResolveKeyItemId();

            // Регистрация в ShipKeyServer. Идемпотентно.
            if (ShipKeyServer.Instance != null)
            {
                ShipKeyServer.Instance.RegisterBinding(NetworkObjectId, this);
            }
            else
            {
                Debug.LogWarning($"[ShipKeyBinding] OnNetworkSpawn for ship={NetworkObjectId} " +
                                 "но ShipKeyServer.Instance==null (server not ready? " +
                                 "scene-placed hub ещё не spawn'нут?). Регистрация будет пропущена.");
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && ShipKeyServer.Instance != null)
            {
                ShipKeyServer.Instance.UnregisterBinding(NetworkObjectId);
            }
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Server-only: вычислить _serverKeyItemId через InventoryWorld.
        /// Вызывается из OnNetworkSpawn и при первом обращении к ServerKeyItemId.
        /// </summary>
        private void ResolveKeyItemId()
        {
            if (!IsServer) return;
            if (_keyItemData == null)
            {
                _serverKeyItemId = -1;
                return;
            }
            if (InventoryWorld.Instance == null)
            {
                Debug.LogWarning($"[ShipKeyBinding] ResolveKeyItemId: InventoryWorld.Instance==null " +
                                 $"(ship={NetworkObjectId}). Будет резолвнуто позже при первом CanPlayerBoard.");
                return;
            }
            _serverKeyItemId = InventoryWorld.Instance.GetOrRegisterItemId(_keyItemData);
        }

#if UNITY_EDITOR
        // Помощь дизайнеру: проверить, что у каждого ShipKeyBinding в сцене
        // уникальный keyItemData (иначе один ключ подходит к нескольким кораблям — баг).
        private void OnValidate()
        {
            if (_keyItemData == null) return;
            // Сканируем все ShipKeyBinding в текущей сцене (включая неактивные).
            var all = FindObjectsByType<ShipKeyBinding>(FindObjectsInactive.Include);
            int dupes = 0;
            foreach (var other in all)
            {
                if (other == this) continue;
                if (other._keyItemData == _keyItemData)
                {
                    dupes++;
                    Debug.LogWarning($"[ShipKeyBinding] ДУБЛЬ! {gameObject.name} и {other.gameObject.name} " +
                                     $"оба ссылаются на keyItemData={_keyItemData.name}. " +
                                     "Это нарушает правило 1 ключ ↔ 1 корабль.", this);
                }
            }
        }
#endif
    }
}
