using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Ship.Key;

namespace ProjectC.Ship
{
    /// <summary>
    /// ShipModuleServer — NetworkBehaviour на корне корабля.
    /// Обрабатывает серверные RPC для установки/снятия модулей в рантайме.
    ///
    /// Валидация на сервере:
    ///   1. Владение ключом (KeyRodInstanceWorld.IsOwnerOfInstance)
    ///   2. Корабль пристыкован (ShipController.IsDocked)
    ///   3. Совместимость + энергия (ShipModuleManager)
    ///
    /// После установки/снятия — ClientRpc синхронизирует изменение всем клиентам.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ShipModuleServer : NetworkBehaviour
    {
        [Header("Каталог модулей (для lookup по moduleId)")]
        [Tooltip("Ссылка на базу модулей. Используется для поиска ShipModule по moduleId на клиенте после RPC.")]
        [SerializeField] private ModuleShopDatabase _shopDatabase;

        // Ссылки на компоненты корабля
        private ProjectC.Player.ShipController _shipController;
        private ShipModuleManager _moduleManager;
        private NetworkObject _netObj;

        // Событие для UI (вызывается на клиенте после синхронизации)
        public static event Action<ulong /*shipNetId*/> OnModuleChanged;

        private void Awake()
        {
            _shipController = GetComponent<ProjectC.Player.ShipController>();
            _moduleManager = GetComponent<ShipModuleManager>();
            _netObj = GetComponent<NetworkObject>();
        }

        // ============================================================
        // Public API (для RepairManagerWindow)
        // ============================================================

        /// <summary>Клиент отправляет запрос на установку модуля.</summary>
        public void RequestInstallModule(int keyInstanceId, string slotName, string moduleId)
        {
            if (!IsClient) return;
            RequestInstallModuleRpc(keyInstanceId, slotName, moduleId);
        }

        /// <summary>Клиент отправляет запрос на снятие модуля.</summary>
        public void RequestRemoveModule(int keyInstanceId, string slotName)
        {
            if (!IsClient) return;
            RequestRemoveModuleRpc(keyInstanceId, slotName);
        }

        // ============================================================
        // Server RPC
        // ============================================================

        [Rpc(SendTo.Server)]
        private void RequestInstallModuleRpc(int keyInstanceId, string slotName, string moduleId,
            RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong clientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"[ShipModuleServer] Install request: client={clientId}, keyInstance={keyInstanceId}, " +
                      $"slot='{slotName}', module='{moduleId}'");

            // --- Валидация 1: владение ключом ---
            if (!KeyRodInstanceWorld.IsInitialized ||
                !KeyRodInstanceWorld.IsOwnerOfInstance(clientId, keyInstanceId))
            {
                NotifyClientError(clientId, "У вас нет ключа от этого корабля.");
                return;
            }

            // Проверка: ключ привязан к ЭТОМУ кораблю
            var instance = KeyRodInstanceWorld.GetInstance(keyInstanceId);
            if (instance == null || instance.registeredShipId != _netObj.NetworkObjectId)
            {
                NotifyClientError(clientId, "Ключ не подходит к этому кораблю.");
                return;
            }

            // --- Валидация 2: корабль пристыкован ---
            if (_shipController != null && !_shipController.IsDocked)
            {
                NotifyClientError(clientId, "Корабль не в доке. Установка модулей возможна только в доке.");
                return;
            }

            // --- Валидация 3: найти слот ---
            if (_moduleManager == null)
            {
                NotifyClientError(clientId, "Менеджер модулей не найден на корабле.");
                return;
            }

            ModuleSlot targetSlot = null;
            foreach (var slot in _moduleManager.slots)
            {
                if (slot != null && slot.gameObject.name == slotName)
                {
                    targetSlot = slot;
                    break;
                }
            }
            if (targetSlot == null)
            {
                NotifyClientError(clientId, $"Слот '{slotName}' не найден на корабле.");
                return;
            }

            // --- Валидация 4: найти модуль ---
            ShipModule module = FindModuleById(moduleId);
            if (module == null)
            {
                NotifyClientError(clientId, $"Модуль '{moduleId}' не найден в каталоге.");
                return;
            }

            // --- Валидация 5: совместимость + энергия + требования ---
            // Если слот занят — сначала снимаем старый для проверки энергии
            ShipModule oldModule = targetSlot.installedModule;
            if (oldModule != null)
            {
                targetSlot.RemoveModule();
            }

            bool installOk = _moduleManager.InstallModule(targetSlot, module);
            if (!installOk)
            {
                // Восстановить старый модуль
                if (oldModule != null)
                    targetSlot.InstallModule(oldModule);
                NotifyClientError(clientId, $"Не удалось установить модуль '{module.displayName}'. " +
                    "Проверьте совместимость, энергию и требования.");
                return;
            }

            // --- Успех: синхронизировать клиентов ---
            Debug.Log($"[ShipModuleServer] Module '{moduleId}' installed in slot '{slotName}' " +
                      $"on ship {_netObj.NetworkObjectId}");

            NotifyClientSuccess(clientId, slotName, moduleId, isInstall: true);
            OnModuleChangedClientRpc(slotName, moduleId, isInstall: true);
        }

        [Rpc(SendTo.Server)]
        private void RequestRemoveModuleRpc(int keyInstanceId, string slotName,
            RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong clientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"[ShipModuleServer] Remove request: client={clientId}, keyInstance={keyInstanceId}, " +
                      $"slot='{slotName}'");

            // --- Валидация 1: владение ключом ---
            if (!KeyRodInstanceWorld.IsInitialized ||
                !KeyRodInstanceWorld.IsOwnerOfInstance(clientId, keyInstanceId))
            {
                NotifyClientError(clientId, "У вас нет ключа от этого корабля.");
                return;
            }

            var instance = KeyRodInstanceWorld.GetInstance(keyInstanceId);
            if (instance == null || instance.registeredShipId != _netObj.NetworkObjectId)
            {
                NotifyClientError(clientId, "Ключ не подходит к этому кораблю.");
                return;
            }

            // --- Валидация 2: корабль пристыкован ---
            if (_shipController != null && !_shipController.IsDocked)
            {
                NotifyClientError(clientId, "Корабль не в доке.");
                return;
            }

            // --- Валидация 3: найти слот ---
            if (_moduleManager == null)
            {
                NotifyClientError(clientId, "Менеджер модулей не найден.");
                return;
            }

            ModuleSlot targetSlot = null;
            foreach (var slot in _moduleManager.slots)
            {
                if (slot != null && slot.gameObject.name == slotName)
                {
                    targetSlot = slot;
                    break;
                }
            }
            if (targetSlot == null)
            {
                NotifyClientError(clientId, $"Слот '{slotName}' не найден.");
                return;
            }

            if (!targetSlot.isOccupied)
            {
                NotifyClientError(clientId, $"Слот '{slotName}' уже пуст.");
                return;
            }

            string removedModuleId = targetSlot.installedModuleId;
            _moduleManager.RemoveModule(targetSlot);

            Debug.Log($"[ShipModuleServer] Module removed from slot '{slotName}' on ship {_netObj.NetworkObjectId}");

            NotifyClientSuccess(clientId, slotName, removedModuleId, isInstall: false);
            OnModuleChangedClientRpc(slotName, string.Empty, isInstall: false);
        }

        /// <summary>Клиент отправляет запрос на продажу модуля (снятие + кредиты).</summary>
        public void RequestSellModule(int keyInstanceId, string slotName, int sellCredits)
        {
            if (!IsClient) return;
            RequestSellModuleRpc(keyInstanceId, slotName, sellCredits);
        }

        [Rpc(SendTo.Server)]
        private void RequestSellModuleRpc(int keyInstanceId, string slotName, int sellCredits,
            RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong clientId = rpcParams.Receive.SenderClientId;

            // --- Validation (same as remove) ---
            if (!KeyRodInstanceWorld.IsInitialized ||
                !KeyRodInstanceWorld.IsOwnerOfInstance(clientId, keyInstanceId))
            {
                NotifyClientError(clientId, "У вас нет ключа от этого корабля.");
                return;
            }

            var instance = KeyRodInstanceWorld.GetInstance(keyInstanceId);
            if (instance == null || instance.registeredShipId != _netObj.NetworkObjectId)
            {
                NotifyClientError(clientId, "Ключ не подходит к этому кораблю.");
                return;
            }

            if (_shipController != null && !_shipController.IsDocked)
            {
                NotifyClientError(clientId, "Корабль не в доке.");
                return;
            }

            if (_moduleManager == null)
            {
                NotifyClientError(clientId, "Менеджер модулей не найден.");
                return;
            }

            ModuleSlot targetSlot = null;
            foreach (var slot in _moduleManager.slots)
            {
                if (slot != null && slot.gameObject.name == slotName)
                {
                    targetSlot = slot;
                    break;
                }
            }
            if (targetSlot == null)
            {
                NotifyClientError(clientId, $"Слот '{slotName}' не найден.");
                return;
            }

            if (!targetSlot.isOccupied)
            {
                NotifyClientError(clientId, $"Слот '{slotName}' уже пуст.");
                return;
            }

            string removedModuleId = targetSlot.installedModuleId;
            _moduleManager.RemoveModule(targetSlot);

            // --- Give credits ---
            if (sellCredits > 0)
            {
                var trade = ProjectC.Trade.Core.TradeWorld.Instance;
                if (trade?.Repository != null)
                {
                    if (trade.Repository.TryModifyCredits(clientId, sellCredits, out float newCredits, out _))
                    {
                        Debug.Log($"[ShipModuleServer] Player {clientId} sold '{removedModuleId}' for {sellCredits} CR (new={newCredits:F0})");
                    }
                }
            }

            NotifyClientSuccess(clientId, slotName, removedModuleId, isInstall: false);
            OnModuleChangedClientRpc(slotName, string.Empty, isInstall: false);
        }

        // ============================================================
        // Client RPC (синхронизация всем клиентам)
        // ============================================================

        [Rpc(SendTo.Everyone)]
        private void OnModuleChangedClientRpc(string slotName, string moduleId, bool isInstall)
        {
            Debug.Log($"[ShipModuleServer] ClientRpc: slot='{slotName}', module='{moduleId}', install={isInstall}");

            if (_moduleManager == null) return;

            ModuleSlot targetSlot = null;
            foreach (var slot in _moduleManager.slots)
            {
                if (slot != null && slot.gameObject.name == slotName)
                {
                    targetSlot = slot;
                    break;
                }
            }
            if (targetSlot == null) return;

            if (isInstall)
            {
                ShipModule module = FindModuleById(moduleId);
                if (module != null)
                {
                    // Если слот занят — сначала снять
                    if (targetSlot.isOccupied)
                        targetSlot.RemoveModule();
                    targetSlot.InstallModule(module);
                }
            }
            else
            {
                if (targetSlot.isOccupied)
                    targetSlot.RemoveModule();
            }

            OnModuleChanged?.Invoke(_netObj != null ? _netObj.NetworkObjectId : 0);
        }

        // ============================================================
        // Notifications (TargetRpc)
        // ============================================================

        private void NotifyClientError(ulong targetClientId, string message)
        {
            Debug.LogWarning($"[ShipModuleServer] Denied client {targetClientId}: {message}");
            // TODO: TargetRpc для показа toast/уведомления клиенту.
            // Пока используем ClientRpc с проверкой localClientId.
            NotifyErrorClientRpc(targetClientId, message);
        }

        private void NotifyClientSuccess(ulong targetClientId, string slotName, string moduleId, bool isInstall)
        {
            NotifySuccessClientRpc(targetClientId, slotName, moduleId, isInstall);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyErrorClientRpc(ulong targetClientId, string message)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                Debug.LogWarning($"[ShipModuleServer] ERROR: {message}");
                // UI toast будет добавлен в RepairManagerWindow
            }
        }

        [Rpc(SendTo.Everyone)]
        private void NotifySuccessClientRpc(ulong targetClientId, string slotName, string moduleId, bool isInstall)
        {
            // Клиент, отправивший запрос, увидит toast
        }

        // ============================================================
        // Ship Repainting
        // ============================================================

        public void RequestRepaintShip(int keyInstanceId, Color color, int cost)
        {
            if (!IsClient) return;
            RequestRepaintShipRpc(keyInstanceId, (byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255), cost);
        }

        [Rpc(SendTo.Server)]
        private void RequestRepaintShipRpc(int keyInstanceId, byte r, byte g, byte b, int cost,
            RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong clientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"[ShipModuleServer] Repaint request: client={clientId}, keyInstance={keyInstanceId}, " +
                      $"color=({r},{g},{b}), cost={cost}");

            // --- Валидация 1: владение ключом ---
            if (!KeyRodInstanceWorld.IsInitialized ||
                !KeyRodInstanceWorld.IsOwnerOfInstance(clientId, keyInstanceId))
            {
                NotifyClientError(clientId, "У вас нет ключа от этого корабля.");
                return;
            }

            var instance = KeyRodInstanceWorld.GetInstance(keyInstanceId);
            if (instance == null || instance.registeredShipId != _netObj.NetworkObjectId)
            {
                NotifyClientError(clientId, "Ключ не подходит к этому кораблю.");
                return;
            }

            // --- Валидация 2: корабль пристыкован ---
            if (_shipController != null && !_shipController.IsDocked)
            {
                NotifyClientError(clientId, "Корабль не в доке. Покраска возможна только в доке.");
                return;
            }

            // --- Списание кредитов ---
            if (cost > 0)
            {
                var trade = ProjectC.Trade.Core.TradeWorld.Instance;
                if (trade?.Repository != null)
                {
                    if (!trade.Repository.TryModifyCredits(clientId, -cost, out float newCredits, out string failReason))
                    {
                        NotifyClientError(clientId, $"Недостаточно кредитов: {failReason}");
                        return;
                    }
                    Debug.Log($"[ShipModuleServer] Player {clientId} paid {cost} CR for repaint (new={newCredits:F0})");
                }
            }

            // --- Применить цвет ---
            Color shipColor = new Color32(r, g, b, 255);
            _shipController.SetShipColor(shipColor);
            _shipController.ApplyShipColor(shipColor);

            // Синхронизировать всем клиентам
            ApplyPaintClientRpc(r, g, b);

            NotifyClientSuccess(clientId, string.Empty, $"color({r},{g},{b})", isInstall: true);
        }

        [Rpc(SendTo.Everyone)]
        private void ApplyPaintClientRpc(byte r, byte g, byte b)
        {
            if (_shipController != null)
                _shipController.ApplyShipColor(new Color32(r, g, b, 255));
        }

        // ============================================================
        // Hull Repair (T-HULL)
        // ============================================================

        /// <summary>Клиент отправляет запрос на ремонт корпуса.</summary>
        public void RequestRepairHull(int keyInstanceId)
        {
            if (!IsClient) return;
            RequestRepairHullRpc(keyInstanceId);
        }

        [Rpc(SendTo.Server)]
        private void RequestRepairHullRpc(int keyInstanceId,
            RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong clientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"[ShipModuleServer] RepairHull request: client={clientId}, keyInstance={keyInstanceId}");

            // --- Валидация 1: владение ключом ---
            if (!KeyRodInstanceWorld.IsInitialized ||
                !KeyRodInstanceWorld.IsOwnerOfInstance(clientId, keyInstanceId))
            {
                NotifyClientError(clientId, "У вас нет ключа от этого корабля.");
                return;
            }

            var instance = KeyRodInstanceWorld.GetInstance(keyInstanceId);
            if (instance == null || instance.registeredShipId != _netObj.NetworkObjectId)
            {
                NotifyClientError(clientId, "Ключ не подходит к этому кораблю.");
                return;
            }

            // --- Валидация 2: корабль пристыкован ---
            if (_shipController != null && !_shipController.IsDocked)
            {
                NotifyClientError(clientId, "Корабль не в доке. Ремонт возможен только в доке.");
                return;
            }

            // --- Валидация 3: ShipHull компонент ---
            var hull = _shipController != null ? _shipController.Hull : null;
            if (hull == null)
            {
                NotifyClientError(clientId, "Корпус корабля не найден (ShipHull не установлен).");
                return;
            }

            // --- Проверка: нужен ли ремонт ---
            if (hull.CurrentHull >= hull.MaxHull)
            {
                NotifyClientError(clientId, "Корпус не нуждается в ремонте.");
                return;
            }

            // --- Списание кредитов ---
            int cost = hull.Config != null ? hull.Config.repairCostCredits : 0;
            if (cost > 0)
            {
                var trade = ProjectC.Trade.Core.TradeWorld.Instance;
                if (trade?.Repository != null)
                {
                    if (!trade.Repository.TryModifyCredits(clientId, -cost, out float newCredits, out string failReason))
                    {
                        NotifyClientError(clientId, $"Недостаточно кредитов: {failReason}");
                        return;
                    }
                    Debug.Log($"[ShipModuleServer] Player {clientId} paid {cost} CR for hull repair (new={newCredits:F0})");
                }
            }

            // --- Ремонт ---
            hull.RepairFull();
            if (_shipController != null)
            {
                _shipController.ClearHullBroken();
            }

            Debug.Log($"[ShipModuleServer] Hull repaired on ship {_netObj.NetworkObjectId} (HP → {hull.MaxHull})");

            NotifyClientSuccess(clientId, string.Empty, "hull_repair", isInstall: true);
        }

        // ============================================================
        // Helpers
        // ============================================================

        /// <summary>Поиск ShipModule по moduleId через каталог.</summary>

        private ShipModule FindModuleById(string moduleId)
        {
            if (_shopDatabase != null)
            {
                return _shopDatabase.FindEntry(moduleId);
            }
            // Fallback: поиск через ShipModuleCatalog (статический реестр)
            return ShipModuleCatalog.Find(moduleId);
        }
    }
}
