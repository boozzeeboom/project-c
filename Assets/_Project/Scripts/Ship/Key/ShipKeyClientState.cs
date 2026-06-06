// =====================================================================================
// ShipKeyClientState.cs — DEPRECATED АЛИАС с сохранением старого API
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md
//
// Этот файл сохранён для backward-compat с NetworkPlayer.ReceiveShipKey*TargetRpc
// (вызывающими методы OnCanBoardResponse / OnBindingsPushed). Поведение идентично
// оригиналу: получает response от ShipKeyServer, при allowed вызывает SubmitSwitchModeRpc,
// при denied эмитит OnBoardDenied event (на который подписывается ShipKeyToast).
//
// НОВЫЕ interactable'ы (не-корабли) используют MetaRequirementClientState напрямую.
// Через 1-2 релиз-цикла: удалить.
// =====================================================================================

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace ProjectC.Ship.Key
{
    /// <summary>
    /// DEPRECATED: устаревший алиас. Для НОВЫХ interactable'ов используйте
    /// ProjectC.MetaRequirement.MetaRequirementClientState. Этот класс оставлен
    /// только для совместимости со старыми Target RPC от ShipKeyServer (binding'и кораблей).
    /// </summary>
    [Obsolete("Use ProjectC.MetaRequirement.MetaRequirementClientState. ShipKeyClientState kept for legacy ship-key RPCs only.")]
    public class ShipKeyClientState : MonoBehaviour
    {
        public static ShipKeyClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly Dictionary<ulong, string> _shipNames = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, int> _shipKeyIds = new Dictionary<ulong, int>();

        public event Action OnBindingsUpdated;
        public event Action<string> OnBoardDenied;

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

        public bool HasBinding(ulong shipNetId) => _shipNames.ContainsKey(shipNetId);
        public string GetShipDisplayName(ulong shipNetId)
            => _shipNames.TryGetValue(shipNetId, out var n) ? n : $"Корабль #{shipNetId}";
        public int GetKeyItemId(ulong shipNetId)
            => _shipKeyIds.TryGetValue(shipNetId, out var k) ? k : -1;

        /// <summary>Legacy API. Делегирует в ShipKeyServer.RequestCanBoardRpc.</summary>
        public void RequestCanBoard(ulong shipNetId)
        {
            if (ShipKeyServer.Instance == null)
            {
                Debug.LogWarning("[ShipKeyClientState-ALIAS] RequestCanBoard: ShipKeyServer.Instance==null. Доступ ЗАПРЕЩЁН.");
                EmitDenyToast(shipNetId, "Сервер ключей недоступен");
                return;
            }
            ShipKeyServer.Instance.RequestCanBoardRpc(shipNetId);
        }

        public void OnCanBoardResponse(ulong shipNetId, bool allowed, string reason)
        {
            if (allowed)
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm == null) return;
                var playerObj = nm.SpawnManager.GetPlayerNetworkObject(nm.LocalClientId);
                if (playerObj == null) return;
                var netPlayer = playerObj.GetComponent<ProjectC.Player.NetworkPlayer>();
                if (netPlayer == null) return;
                netPlayer.SubmitSwitchModeRpc();
            }
            else
            {
                string msg = !string.IsNullOrEmpty(reason)
                    ? reason
                    : $"Нет ключа корабля ({GetShipDisplayName(shipNetId)})";
                EmitDenyToast(shipNetId, msg);
            }
        }

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
            catch (Exception ex) { Debug.LogError($"[ShipKeyClientState-ALIAS] OnBindingsUpdated handler threw: {ex}"); }
        }

        private void EmitDenyToast(ulong shipNetId, string reason)
        {
            try { OnBoardDenied?.Invoke(reason); }
            catch (Exception ex) { Debug.LogError($"[ShipKeyClientState-ALIAS] OnBoardDenied handler threw: {ex}"); }
            Debug.LogWarning($"[ShipKeyClientState-ALIAS] Board denied: shipNetId={shipNetId}, reason='{reason}'");
        }
    }
}
