// =====================================================================================
// ShipKeyClientState.cs — клиентская проекция привязок корабль↔ключ (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/00_OVERVIEW.md
//
// Назначение: singleton-проекция server-state привязок на клиентский процесс.
// Один инстанс на клиента (НЕ NetworkBehaviour). Создаётся в NetworkManagerController.Awake
// (рядом с InventoryClientState, ContractClientState, MarketClientState).
//
// Клиент использует этот класс для:
//   • RequestCanBoard(shipNetId) — отправить запрос на сервер, получить ответ
//   • Получить уведомление о привязках (для UI)
//   • Получить уведомление о deny (для toast'а)
//
// Создание: auto-spawn в NetworkManagerController.Awake (FIX C2-паттерн).
// =====================================================================================

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Player;
using ProjectC.Trade.Network;

namespace ProjectC.Ship.Key
{
    public class ShipKeyClientState : MonoBehaviour
    {
        public static ShipKeyClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // ============================================================
        // State
        // ============================================================

        // shipNetId → displayName (для тостов и UI)
        private readonly Dictionary<ulong, string> _shipNames = new Dictionary<ulong, string>();

        // shipNetId → keyItemId (нужен UI, чтобы показать "X не подходит" — опционально)
        private readonly Dictionary<ulong, int> _shipKeyIds = new Dictionary<ulong, int>();

        // ============================================================
        // Events
        // ============================================================

        /// <summary>Дёргается при обновлении реестра привязок (на данный момент не вызывается —
        /// в MVP сервер пушит напрямую через NetworkPlayer).</summary>
        public event Action OnBindingsUpdated;

        /// <summary>Дёргается когда сервер отказал в посадке. UI показывает toast.</summary>
        public event Action<string> OnBoardDenied;

        // ============================================================
        // Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        // Public API
        // ============================================================

        /// <summary>Проверить, есть ли привязка для корабля (на клиенте — для UI; источник истины — сервер).</summary>
        public bool HasBinding(ulong shipNetId) => _shipNames.ContainsKey(shipNetId);

        public string GetShipDisplayName(ulong shipNetId)
            => _shipNames.TryGetValue(shipNetId, out var n) ? n : $"Корабль #{shipNetId}";

        public int GetKeyItemId(ulong shipNetId)
            => _shipKeyIds.TryGetValue(shipNetId, out var k) ? k : -1;

        /// <summary>Запросить у сервера разрешение на посадку. Ответ придёт через
        /// NetworkPlayer.ReceiveShipKeyCanBoardResponseTargetRpc.</summary>
        public void RequestCanBoard(ulong shipNetId)
        {
            if (ShipKeyServer.Instance == null)
            {
                Debug.LogWarning("[ShipKeyClientState] RequestCanBoard: ShipKeyServer.Instance==null " +
                                 "(server not started?). Доступ ЗАПРЕЩЁН по умолчанию.");
                EmitDenyToast(shipNetId, "Сервер ключей недоступен");
                return;
            }
            ShipKeyServer.Instance.RequestCanBoardRpc(shipNetId);
        }

        // ============================================================
        // Server → Client delivery (вызывается из NetworkPlayer.ReceiveShipKey*TargetRpc)
        // ============================================================

        /// <summary>Вызывается из NetworkPlayer.ReceiveShipKeyCanBoardResponseTargetRpc.</summary>
        public void OnCanBoardResponse(ulong shipNetId, bool allowed, string reason)
        {
            if (allowed)
            {
                // Клиент: отправляем штатный SubmitSwitchModeRpc.
                var nm = NetworkManager.Singleton;
                if (nm == null) return;
                var playerObj = nm.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);
                if (playerObj == null) return;
                var netPlayer = playerObj.GetComponent<NetworkPlayer>();
                if (netPlayer == null) return;
                netPlayer.SubmitSwitchModeRpc();
            }
            else
            {
                // Показываем toast.
                string msg = !string.IsNullOrEmpty(reason)
                    ? reason
                    : $"Нет ключа корабля ({GetShipDisplayName(shipNetId)})";
                EmitDenyToast(shipNetId, msg);
            }
        }

        /// <summary>Вызывается из NetworkPlayer.ReceiveShipKeyBindingsTargetRpc.
        /// MVP: bulk-push всех привязок (для UI). В будущем — диффы.</summary>
        public void OnBindingsPushed(ulong[] shipNetIds, int[] keyItemIds, FixedString64Bytes[] displayNames)
        {
            _shipNames.Clear();
            _shipKeyIds.Clear();
            int n = Mathf.Min(shipNetIds.Length, Mathf.Min(keyItemIds.Length, displayNames.Length));
            for (int i = 0; i < n; i++)
            {
                _shipNames[shipNetIds[i]] = displayNames[i].ToString();
                _shipKeyIds[shipNetIds[i]] = keyItemIds[i];
            }
            try { OnBindingsUpdated?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[ShipKeyClientState] OnBindingsUpdated handler threw: {ex}"); }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void EmitDenyToast(ulong shipNetId, string reason)
        {
            try { OnBoardDenied?.Invoke(reason); }
            catch (Exception ex) { Debug.LogError($"[ShipKeyClientState] OnBoardDenied handler threw: {ex}"); }
            // Доп. лог — чтобы было видно в Console без UI
            Debug.LogWarning($"[ShipKeyClientState] Board denied: shipNetId={shipNetId}, reason='{reason}'");
        }
    }
}
