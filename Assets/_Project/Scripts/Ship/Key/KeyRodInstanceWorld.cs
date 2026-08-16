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
    /// 
    /// T-KEY-PERSIST: поддержка auto-save через IKeyRodInstanceRepository.
    /// </summary>
    public static class KeyRodInstanceWorld
    {
        // ===========================================================
        // State
        // ===========================================================

        private static readonly Dictionary<int, KeyRodInstance> _instancesById = new Dictionary<int, KeyRodInstance>();
        private static readonly Dictionary<ulong, int> _primaryInstanceByShipId = new Dictionary<ulong, int>();
        private static readonly Dictionary<ulong, List<int>> _instancesByPlayer = new Dictionary<ulong, List<int>>();
        /// <summary>T-KEY-PERSIST-FIX: стабильный индекс persistentShipId → instanceId.
        /// Используется для перепривязки registeredShipId при спавне корабля в новой сессии.</summary>
        private static readonly Dictionary<string, int> _instancesByPersistentId = new Dictionary<string, int>();
        private static int _nextInstanceId = 1;
        public static bool IsInitialized { get; private set; }

        // T-KEY-PERSIST: optional repository (null = no persistence)
        private static IKeyRodInstanceRepository _repository;

        // ===========================================================
        // Lifecycle
        // ===========================================================

        /// <summary>Инициализация реестра без persistence. Идемпотентно.</summary>
        public static void CreateAndInitialize()
        {
            CreateAndInitialize(null);  // делегируем с null-репозиторием
        }

        /// <summary>Инициализация реестра с persistence (T-KEY-PERSIST). Идемпотентно.
        /// Загружает сохранённые instance'ы из репозитория и восстанавливает _nextInstanceId.</summary>
        /// <param name="repository">Репозиторий (null = без persistence).</param>
        public static void CreateAndInitialize(IKeyRodInstanceRepository repository)
        {
            if (IsInitialized) return;
            _instancesById.Clear();
            _primaryInstanceByShipId.Clear();
            _instancesByPlayer.Clear();
            _instancesByPersistentId.Clear();
            _nextInstanceId = 1;
            _repository = repository;
            IsInitialized = true;

            // Загрузка из репозитория (если есть)
            int loaded = 0;
            if (_repository != null)
            {
                var saved = _repository.LoadAll();
                foreach (var dto in saved)
                {
                    if (dto == null) continue;
                    var inst = new KeyRodInstance
                    {
                        instanceId       = _nextInstanceId++,
                        itemId           = dto.itemId,
                        registeredShipId = dto.registeredShipId,
                        ownerPlayerId    = dto.ownerPlayerId,
                        originalOwnerId  = dto.originalOwnerId,
                        state            = (KeyRodInstanceState)dto.state,
                        createdAtUnix    = dto.createdAtUnix,
                        persistentShipId = string.IsNullOrEmpty(dto.persistentShipId) ? null : dto.persistentShipId,
                    };
                    _instancesById[inst.instanceId] = inst;

                    if (inst.registeredShipId != 0)
                        _primaryInstanceByShipId[inst.registeredShipId] = inst.instanceId;

                    // T-KEY-PERSIST-FIX: индекс по стабильному persistentShipId
                    if (!string.IsNullOrEmpty(inst.persistentShipId))
                        _instancesByPersistentId[inst.persistentShipId] = inst.instanceId;

                    if (inst.ownerPlayerId != KeyRodInstance.OWNER_NONE
                        && inst.state == KeyRodInstanceState.Active)
                    {
                        if (!_instancesByPlayer.TryGetValue(inst.ownerPlayerId, out var list))
                        {
                            list = new List<int>();
                            _instancesByPlayer[inst.ownerPlayerId] = list;
                        }
                        list.Add(inst.instanceId);
                    }

                    loaded++;
                }
            }

            Debug.Log($"[KeyRodInstanceWorld] CreateAndInitialize: registry ready. Loaded {loaded} persisted instances. Next ID = {_nextInstanceId}");
        }

        /// <summary>Очистка всех instance'ов. Перед очисткой — auto-save всех активных (T-KEY-PERSIST).</summary>
        public static void Shutdown()
        {
            // Auto-save перед shutdown
            AutoSave();

            _instancesById.Clear();
            _primaryInstanceByShipId.Clear();
            _instancesByPlayer.Clear();
            _instancesByPersistentId.Clear();
            _nextInstanceId = 1;
            _repository = null;
            IsInitialized = false;
            Debug.Log($"[KeyRodInstanceWorld] Shutdown: registry cleared and saved.");
        }

        // ===========================================================
        // Auto-save (T-KEY-PERSIST)
        // ===========================================================

        /// <summary>Сохранить все active instance'ы в репозиторий (если репозиторий задан).</summary>
        private static void AutoSave()
        {
            if (_repository == null) return;
            if (!IsInitialized) return;

            var list = new List<KeyRodInstance>();
            foreach (var kvp in _instancesById)
            {
                // Сохраняем только Active (Destroyed не восстанавливаются)
                if (kvp.Value.state == KeyRodInstanceState.Active)
                    list.Add(kvp.Value);
            }
            _repository.SaveAll(list);
            Debug.Log($"[KeyRodInstanceWorld] AutoSave: saved {list.Count} active instances.");
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

        /// <summary>T-KEY-09: поиск Active instance для clientId+itemId (при pickup drop-нутого ключа).</summary>
        public static int FindActiveKeyInstance(ulong clientId, int itemId)
        {
            foreach (var kvp in _instancesById)
            {
                var inst = kvp.Value;
                if (inst != null
                    && inst.itemId == itemId
                    && inst.ownerPlayerId == clientId
                    && inst.state == KeyRodInstanceState.Active)
                {
                    return inst.instanceId;
                }
            }
            return 0;
        }

        /// <summary>
        /// Найти существующий экземпляр ключа по itemId и передать его игроку.
        /// Если корабль уже загружен, но его экземпляр ещё не успел создаться,
        /// создаёт его по ShipController.KeyItemData. При нескольких совпадениях
        /// отказывает безопасно, чтобы не выдать ключ от другого корабля.
        /// </summary>
        public static bool TryClaimOrFindInstanceForItem(ulong clientId, int itemId, out int instanceId, out string reason)
        {
            instanceId = 0;
            reason = string.Empty;
            if (!IsInitialized)
            {
                reason = "Сервер ключей не инициализирован";
                return false;
            }
            if (itemId <= 0)
            {
                reason = "Некорректный itemId ключа";
                return false;
            }

            int matches = 0;
            int ownedByClient = 0;
            int available = 0;
            foreach (var inst in _instancesById.Values)
            {
                if (inst == null || inst.itemId != itemId || inst.state != KeyRodInstanceState.Active)
                    continue;
                matches++;
                if (inst.ownerPlayerId == clientId) ownedByClient = ownedByClient == 0 ? inst.instanceId : -1;
                if (inst.ownerPlayerId == KeyRodInstance.OWNER_NONE) available = available == 0 ? inst.instanceId : -1;
            }

            if (matches > 1 || ownedByClient < 0 || available < 0)
            {
                reason = $"Для itemId={itemId} найдено несколько экземпляров ключа; выдача остановлена для защиты привязки к кораблю";
                return false;
            }

            if (ownedByClient > 0)
            {
                instanceId = ownedByClient;
                return true;
            }

            if (available <= 0)
            {
                // Fallback для окна между OnNetworkSpawn и корутиной ShipController.
                var ships = UnityEngine.Object.FindObjectsOfType<ProjectC.Player.ShipController>(true);
                for (int i = 0; i < ships.Length; i++)
                {
                    var ship = ships[i];
                    if (ship == null || !ship.IsSpawned || !ship.IsServer || ship.KeyItemData == null)
                        continue;
                    int shipItemId = ProjectC.Items.InventoryWorld.Instance != null
                        ? ProjectC.Items.InventoryWorld.Instance.GetOrRegisterItemId(ship.KeyItemData) : -1;
                    if (shipItemId != itemId) continue;
                    available = CreateInstance(itemId, ship.NetworkObjectId, KeyRodInstance.OWNER_NONE, ship.ShipPersistentId);
                    break;
                }
            }

            if (available <= 0)
            {
                reason = $"Не найден свободный экземпляр ключа itemId={itemId} у загруженного корабля";
                return false;
            }

            if (!TransferInstance(available, KeyRodInstance.OWNER_NONE, clientId))
            {
                reason = $"Не удалось передать экземпляр ключа instanceId={available} игроку {clientId}";
                return false;
            }

            instanceId = available;
            return true;
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
        /// <param name="persistentShipId">T-KEY-PERSIST-FIX: стабильный ID корабля (ShipPersistentId).
        /// Если задан и найден существующий instance — перепривязывает registeredShipId к новому netId,
        /// сохраняя ownerPlayerId и state. Предотвращает дубликаты при смене NetworkObjectId между сессиями.</param>
        public static int CreateInstance(int itemId, ulong registeredShipId, ulong ownerPlayerId, string persistentShipId = null)
        {
            if (!IsInitialized)
            {
                Debug.LogError($"[KeyRodInstanceWorld] CreateInstance called but NOT initialized. Call CreateAndInitialize() first.");
                return -1;
            }

            // Валидация: itemId должен быть в ItemDatabase (если InventoryWorld уже готов)
            if (ProjectC.Items.InventoryWorld.Instance != null
                && ProjectC.Items.InventoryWorld.Instance.GetItemDefinition(itemId) == null)
            {
                Debug.LogError($"[KeyRodInstanceWorld] CreateInstance: itemId={itemId} not found in InventoryWorld._itemDatabase. " +
                               $"Make sure the ItemData SO is in Resources/Items/ (Resources.LoadAll is non-recursive).");
                return -1;
            }

            // === T-KEY-PERSIST-FIX: rebind по стабильному persistentShipId ===
            if (!string.IsNullOrEmpty(persistentShipId) && registeredShipId != 0)
            {
                if (_instancesByPersistentId.TryGetValue(persistentShipId, out var existingId))
                {
                    var existingInst = GetInstance(existingId);
                    if (existingInst != null && existingInst.state == KeyRodInstanceState.Active)
                    {
                        // Rebind: обновить registeredShipId на актуальный netId новой сессии.
                        // Если кораблю назначили новый уникальный ItemData, мигрируем itemId
                        // у уже сохранённого экземпляра вместо создания дубликата.
                        if (existingInst.itemId != itemId)
                        {
                            Debug.Log($"[KeyRodInstanceWorld] CreateInstance: migrate persistent key " +
                                      $"instanceId={existingId}, itemId {existingInst.itemId} → {itemId}");
                            existingInst.itemId = itemId;
                        }
                        ulong oldShipId = existingInst.registeredShipId;
                        if (oldShipId != registeredShipId)
                        {
                            if (oldShipId != 0)
                                _primaryInstanceByShipId.Remove(oldShipId);
                            _primaryInstanceByShipId[registeredShipId] = existingId;
                            existingInst.registeredShipId = registeredShipId;
                            Debug.Log($"[KeyRodInstanceWorld] CreateInstance: REBIND persistentShipId='{persistentShipId}' " +
                                      $"instanceId={existingId}, netId {oldShipId} → {registeredShipId}, owner={existingInst.ownerPlayerId}");
                        }
                        AutoSave();
                        return existingId;
                    }
                }

                // Не найден по persistentShipId — но может быть stale-инстанс (без persistentShipId)
                // занимает этот netId. Удаляем его — он из дофиксовой эры.
                if (_primaryInstanceByShipId.TryGetValue(registeredShipId, out var staleId))
                {
                    var staleInst = GetInstance(staleId);
                    if (staleInst != null && string.IsNullOrEmpty(staleInst.persistentShipId))
                    {
                        Debug.LogWarning($"[KeyRodInstanceWorld] CreateInstance: removing stale instance (id={staleId}) " +
                                         $"with colliding netId={registeredShipId}, no persistentShipId");
                        _primaryInstanceByShipId.Remove(registeredShipId);
                        if (staleInst.ownerPlayerId != KeyRodInstance.OWNER_NONE
                            && _instancesByPlayer.TryGetValue(staleInst.ownerPlayerId, out var playerList))
                        {
                            playerList.Remove(staleId);
                            if (playerList.Count == 0) _instancesByPlayer.Remove(staleInst.ownerPlayerId);
                        }
                        _instancesById.Remove(staleId);
                    }
                }
            }

            // Проверка: корабль уже привязан? (1:1 в MVP)
            if (registeredShipId != 0
                && _primaryInstanceByShipId.TryGetValue(registeredShipId, out var collisionId))
            {
                Debug.Log($"[KeyRodInstanceWorld] CreateInstance: ship={registeredShipId} already has instance={collisionId}. " +
                                 $"1:1 binding violated. Returning existing instance id.");
                return collisionId;
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
                persistentShipId = persistentShipId,
            };

            _instancesById[inst.instanceId] = inst;

            if (registeredShipId != 0)
            {
                _primaryInstanceByShipId[registeredShipId] = inst.instanceId;
            }

            if (!string.IsNullOrEmpty(persistentShipId))
            {
                _instancesByPersistentId[persistentShipId] = inst.instanceId;
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
                      $"ship={registeredShipId}, persistentShipId='{persistentShipId}', owner={ownerPlayerId}");

            OnOwnershipChanged?.Invoke(inst.instanceId, ownerPlayerId);
            AutoSave();  // T-KEY-PERSIST

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
            AutoSave();  // T-KEY-PERSIST
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
            AutoSave();  // T-KEY-PERSIST
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

            // Удалить из persistent-индекса
            if (!string.IsNullOrEmpty(inst.persistentShipId))
            {
                _instancesByPersistentId.Remove(inst.persistentShipId);
            }

            _instancesById.Remove(instanceId);
            inst.state = KeyRodInstanceState.Destroyed;
            Debug.Log($"[KeyRodInstanceWorld] DestroyInstance: id={instanceId} removed.");
            OnOwnershipChanged?.Invoke(instanceId, KeyRodInstance.OWNER_NONE);
            AutoSave();  // T-KEY-PERSIST
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
