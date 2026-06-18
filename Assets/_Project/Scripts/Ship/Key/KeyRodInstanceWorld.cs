// =====================================================================================
// KeyRodInstanceWorld.cs — server-only static facade для KeyRodInstance (R2-SHIP-KEY-003, T-KEY-01)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/20_UNIQUE_KEY_INSTANCE.md §2.2
//   • docs/Ships/Key-subsystem/21_SHIP_OWNERSHIP_MODEL.md §2.1
//   • docs/Ships/Key-subsystem/23_ROADMAP.md T-KEY-01
//
// Назначение: SINGLE SOURCE OF TRUTH для всех KeyRodInstance в текущей сессии.
// Паттерн скопирован с CraftingWorld (Assets/_Project/Scripts/Crafting/CraftingWorld.cs):
//   • static class (не MonoBehaviour)
//   • CreateAndInitialize() / Shutdown() lifecycle
//   • Dictionary<int, ...> registry с монотонным counter
//   • public static API + private state
//
// Что НЕ делается в T-KEY-01:
//   • Persist через IPlayerDataRepository → T-KEY-PERSIST (отдельный тикет)
//   • Subscription на OnOwnershipChanged → T-KEY-07 (ShipOwnershipRegistry)
//   • Integration с InventoryWorld → T-KEY-02
//
// MVP-граница: 1 корабль ↔ 1 ключ (1:1). Расширение до 1:N — фаза 2.
// =====================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Ship.Key
{
    /// <summary>
    /// Server-only static facade. Single source of truth для всех KeyRodInstance
    /// в текущей серверной сессии. Создаётся в KeyRodInstanceBinding.OnNetworkSpawn
    /// (T-KEY-04, Q11 explicit binding), закрывается при серверном shutdown.
    /// </summary>
    public static class KeyRodInstanceWorld
    {
        // ===========================================================
        // State
        // ===========================================================

        /// <summary>instanceId → KeyRodInstance. Primary registry.</summary>
        private static readonly Dictionary<int, KeyRodInstance> _instancesById = new Dictionary<int, KeyRodInstance>();

        /// <summary>shipNetId → instanceId. 1:1 в MVP (расширяется до 1:N в фазе 2).</summary>
        private static readonly Dictionary<ulong, int> _primaryInstanceByShipId = new Dictionary<ulong, int>();

        /// <summary>clientId → List&lt;instanceId&gt;. Per-player index для быстрого GetInstancesForPlayer.
        /// Содержит ТОЛЬКО Active instance'ы в чьём-то инвентаре (owner != OWNER_NONE).</summary>
        private static readonly Dictionary<ulong, List<int>> _instancesByPlayer = new Dictionary<ulong, List<int>>();

        /// <summary>Монотонный counter для новых instanceId. Начинается с 1 (0 = "no instance").</summary>
        private static int _nextInstanceId = 1;

        /// <summary>True после CreateAndInitialize(). Сбрасывается в Shutdown().</summary>
        public static bool IsInitialized { get; private set; }

        // ===========================================================
        // Lifecycle
        // ===========================================================

        /// <summary>Инициализация реестра. Идемпотентно. Вызывается из KeyRodInstanceBinding.OnNetworkSpawn
        /// (T-KEY-04) или из NetworkManagerController при StartHost (fallback).</summary>
        public static void CreateAndInitialize()
        {
            if (IsInitialized) return;
            _instancesById.Clear();
            _primaryInstanceByShipId.Clear();
            _instancesByPlayer.Clear();
            _nextInstanceId = 1;
            IsInitialized = true;
            Debug.Log($"[KeyRodInstanceWorld] CreateAndInitialize: empty registry ready.");
        }

        /// <summary>Очистка всех instance'ов. Вызывается при серверном shutdown.</summary>
        public static void Shutdown()
        {
            _instancesById.Clear();
            _primaryInstanceByShipId.Clear();
            _instancesByPlayer.Clear();
            _nextInstanceId = 1;
            IsInitialized = false;
            Debug.Log($"[KeyRodInstanceWorld] Shutdown: registry cleared.");
        }

        // ===========================================================
        // Public read-only API
        // ===========================================================

        /// <summary>Получить instance по id. Null если не найден.</summary>
        public static KeyRodInstance GetInstance(int instanceId)
        {
            _instancesById.TryGetValue(instanceId, out var inst);
            return inst;
        }

        /// <summary>Получить instanceId, привязанный к конкретному кораблю (1:1 в MVP).
        /// Возвращает 0 если корабль не зарегистрирован (не имеет экземпляра ключа).</summary>
        public static int GetInstanceIdForShip(ulong shipNetworkObjectId)
        {
            return _primaryInstanceByShipId.TryGetValue(shipNetworkObjectId, out var id) ? id : 0;
        }

        /// <summary>Получить instance, привязанный к конкретному кораблю.
        /// Null если корабль не зарегистрирован.</summary>
        public static KeyRodInstance GetInstanceForShip(ulong shipNetworkObjectId)
        {
            int id = GetInstanceIdForShip(shipNetworkObjectId);
            return id > 0 ? GetInstance(id) : null;
        }

        /// <summary>Все instance'ы, принадлежащие указанному игроку (только Active).
        /// Используется для UI/HUD/ServerFilter.</summary>
        public static List<int> GetInstancesForPlayer(ulong clientId)
        {
            if (_instancesByPlayer.TryGetValue(clientId, out var list))
            {
                return new List<int>(list); // defensive copy
            }
            return new List<int>();
        }

        /// <summary>Пары (instanceId, shipNetId) для всех KeyRodInstance игрока.
        /// Удобно для UI "Мои корабли".</summary>
        public static List<(int instanceId, ulong shipNetworkObjectId)> GetPlayerShips(ulong clientId)
        {
            var result = new List<(int, ulong)>();
            if (!_instancesByPlayer.TryGetValue(clientId, out var list)) return result;
            foreach (var instanceId in list)
            {
                var inst = GetInstance(instanceId);
                if (inst != null && inst.state == KeyRodInstanceState.Active)
                {
                    result.Add((instanceId, inst.registeredShipId));
                }
            }
            return result;
        }

        /// <summary>True если данный clientId владеет instance с указанным instanceId.
        /// Включает проверку state == Active.</summary>
        public static bool IsOwnerOfInstance(ulong clientId, int instanceId)
        {
            var inst = GetInstance(instanceId);
            return inst != null
                && inst.state == KeyRodInstanceState.Active
                && inst.ownerPlayerId == clientId;
        }

        /// <summary>True если данный clientId владеет ключом от указанного корабля.
        /// Используется в ShipOwnershipRequirement (T-KEY-03).</summary>
        public static bool IsOwnerOfShip(ulong clientId, ulong shipNetworkObjectId)
        {
            int id = GetInstanceIdForShip(shipNetworkObjectId);
            return id > 0 && IsOwnerOfInstance(clientId, id);
        }

        // ===========================================================
        // Public write API (server-only)
        // ===========================================================

        /// <summary>Создать новый экземпляр ключа. Серверный метод, не вызывается с клиента.
        /// Возвращает instanceId (всегда > 0) или -1 при ошибке валидации.</summary>
        /// <param name="itemId">→ ItemData definition. Должен быть зарегистрирован в InventoryWorld._itemDatabase.</param>
        /// <param name="registeredShipId">NetworkObjectId корабля. 0 = без привязки (TODO, фаза 2).</param>
        /// <param name="ownerPlayerId">ClientId владельца. OWNER_NONE = ключ в мире (drop / pickup ещё не подобран).</param>
        public static int CreateInstance(int itemId, ulong registeredShipId, ulong ownerPlayerId)
        {
            if (!IsInitialized)
            {
                Debug.LogError($"[KeyRodInstanceWorld] CreateInstance called but NOT initialized. Call CreateAndInitialize() first.");
                return -1;
            }

            // Валидация: itemId должен быть в ItemDatabase (если InventoryWorld уже готов)
            // InventoryWorld не имеет HasItemDefinition — используем GetItemDefinition != null.
            if (ProjectC.Items.InventoryWorld.Instance != null
                && ProjectC.Items.InventoryWorld.Instance.GetItemDefinition(itemId) == null)
            {
                Debug.LogError($"[KeyRodInstanceWorld] CreateInstance: itemId={itemId} not found in InventoryWorld._itemDatabase. " +
                               $"Make sure the ItemData SO is in Resources/Items/ (Resources.LoadAll is non-recursive).");
                return -1;
            }

            // Проверка: корабль уже привязан? (1:1 в MVP)
            if (registeredShipId != 0
                && _primaryInstanceByShipId.TryGetValue(registeredShipId, out var existingId))
            {
                Debug.LogWarning($"[KeyRodInstanceWorld] CreateInstance: ship={registeredShipId} already has instance={existingId}. " +
                                 $"1:1 binding violated. Returning existing instance id.");
                return existingId;
            }

            var inst = new KeyRodInstance
            {
                instanceId = _nextInstanceId++,
                itemId = itemId,
                registeredShipId = registeredShipId,
                ownerPlayerId = ownerPlayerId,
                originalOwnerId = ownerPlayerId,
                state = KeyRodInstanceState.Active,
                createdAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };

            _instancesById[inst.instanceId] = inst;

            if (registeredShipId != 0)
            {
                _primaryInstanceByShipId[registeredShipId] = inst.instanceId;
            }

            if (ownerPlayerId != KeyRodInstance.OWNER_NONE)
            {
                if (!_instancesByPlayer.TryGetValue(ownerPlayerId, out var list))
                {
                    list = new List<int>();
                    _instancesByPlayer[ownerPlayerId] = list;
                }
                list.Add(inst.instanceId);
            }

            Debug.Log($"[KeyRodInstanceWorld] CreateInstance: id={inst.instanceId}, itemId={itemId}, " +
                      $"ship={registeredShipId}, owner={ownerPlayerId}");

            OnOwnershipChanged?.Invoke(inst.instanceId, ownerPlayerId);

            return inst.instanceId;
        }

        /// <summary>Передать экземпляр от одного владельца к другому (drop → pickup).
        /// В MVP: меняет только ownerPlayerId (instanceId сохраняется — связь с кораблём стабильна).
        /// Если from == OWNER_NONE (ключ в мире, не pickup игрока) — просто ставит нового владельца.</summary>
        public static bool TransferInstance(int instanceId, ulong fromClientId, ulong toClientId)
        {
            var inst = GetInstance(instanceId);
            if (inst == null)
            {
                Debug.LogError($"[KeyRodInstanceWorld] TransferInstance: instanceId={instanceId} not found.");
                return false;
            }
            if (inst.state != KeyRodInstanceState.Active)
            {
                Debug.LogWarning($"[KeyRodInstanceWorld] TransferInstance: instanceId={instanceId} state={inst.state}. Skip.");
                return false;
            }

            // Ownership validation: если from != OWNER_NONE, должен совпадать с текущим владельцем
            if (fromClientId != KeyRodInstance.OWNER_NONE && inst.ownerPlayerId != fromClientId)
            {
                Debug.LogError($"[KeyRodInstanceWorld] TransferInstance: ownership mismatch. " +
                               $"Instance owner={inst.ownerPlayerId}, from={fromClientId}. Abort.");
                return false;
            }

            // Удалить из старого owner-индекса
            if (inst.ownerPlayerId != KeyRodInstance.OWNER_NONE
                && _instancesByPlayer.TryGetValue(inst.ownerPlayerId, out var oldList))
            {
                oldList.Remove(instanceId);
                if (oldList.Count == 0) _instancesByPlayer.Remove(inst.ownerPlayerId);
            }

            // Назначить нового владельца
            inst.ownerPlayerId = toClientId;

            // Добавить в новый owner-индекс (если to != OWNER_NONE)
            if (toClientId != KeyRodInstance.OWNER_NONE)
            {
                if (!_instancesByPlayer.TryGetValue(toClientId, out var newList))
                {
                    newList = new List<int>();
                    _instancesByPlayer[toClientId] = newList;
                }
                if (!newList.Contains(instanceId)) newList.Add(instanceId);
            }

            Debug.Log($"[KeyRodInstanceWorld] TransferInstance: id={instanceId}, " +
                      $"{fromClientId} → {toClientId}");

            OnOwnershipChanged?.Invoke(instanceId, toClientId);
            return true;
        }

        /// <summary>Обновить state (Active → Lost при drop, Lost → Active при pickup, Active → Destroyed при уничтожении).
        /// Не меняет ownerPlayerId — для Lost/Destroyed сохраняется последний валидный owner для истории.</summary>
        public static bool UpdateState(int instanceId, KeyRodInstanceState newState)
        {
            var inst = GetInstance(instanceId);
            if (inst == null)
            {
                Debug.LogError($"[KeyRodInstanceWorld] UpdateState: instanceId={instanceId} not found.");
                return false;
            }
            var oldState = inst.state;
            inst.state = newState;
            Debug.Log($"[KeyRodInstanceWorld] UpdateState: id={instanceId}, {oldState} → {newState}");
            return true;
        }

        /// <summary>Удалить экземпляр навсегда. Используется при уничтожении ключа (фаза 2, сейчас
        /// не вызывается из gameplay — ключи живут пока не Salvaged).</summary>
        public static bool DestroyInstance(int instanceId)
        {
            var inst = GetInstance(instanceId);
            if (inst == null) return false;

            // Удалить из owner-индекса
            if (inst.ownerPlayerId != KeyRodInstance.OWNER_NONE
                && _instancesByPlayer.TryGetValue(inst.ownerPlayerId, out var list))
            {
                list.Remove(instanceId);
                if (list.Count == 0) _instancesByPlayer.Remove(inst.ownerPlayerId);
            }

            // Удалить из ship-индекса
            if (inst.registeredShipId != 0)
            {
                _primaryInstanceByShipId.Remove(inst.registeredShipId);
            }

            _instancesById.Remove(instanceId);
            inst.state = KeyRodInstanceState.Destroyed;
            Debug.Log($"[KeyRodInstanceWorld] DestroyInstance: id={instanceId} removed.");
            OnOwnershipChanged?.Invoke(instanceId, KeyRodInstance.OWNER_NONE);
            return true;
        }

        // ===========================================================
        // Events (для подписки из ShipOwnershipRegistry, T-KEY-07)
        // ===========================================================

        /// <summary>Вызывается при изменении ownerPlayerId или state.
        /// Аргументы: (instanceId, newOwner). newOwner == OWNER_NONE = ключ в мире / уничтожен.</summary>
        public static event Action<int, ulong> OnOwnershipChanged;

        // ===========================================================
        // Diagnostics (для Play Mode тестов / MCP execute_code)
        // ===========================================================

        /// <summary>Все instance'ы (для отладки). Возвращает snapshot, не live-ссылку.</summary>
        public static List<KeyRodInstance> GetAllInstances()
        {
            return new List<KeyRodInstance>(_instancesById.Values);
        }

        /// <summary>Сколько instance'ов зарегистрировано.</summary>
        public static int GetInstanceCount() => _instancesById.Count;
    }
}