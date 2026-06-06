// =====================================================================================
// MetaRequirementClientState.cs — клиентская проекция реестра требований (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/MetaRequirement/00_OVERVIEW.md
//
// Назначение: singleton-проекция server-state реестра требований на клиентский процесс.
// Один инстанс на клиента (НЕ NetworkBehaviour). Создаётся в NetworkManagerController.Awake
// (рядом с InventoryClientState, ContractClientState, MarketClientState).
//
// Клиент использует этот класс для:
//   • RequestCanUse(netId) — отправить запрос на сервер, получить ответ
//   • Получить уведомление о привязках (для UI)
//   • Получить уведомление о deny (для toast'а)
//   • Получить уведомление о allow (для запуска анимации на самом Interactable)
//
// Создание: auto-spawn в NetworkManagerController.Awake (FIX C2-паттерн).
// =====================================================================================

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Player;

namespace ProjectC.MetaRequirement
{
    public class MetaRequirementClientState : MonoBehaviour
    {
        public static MetaRequirementClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // ============================================================
        // State — клиентская проекция
        // ============================================================

        // netId → DTO (для UI и tooltip'ов)
        private readonly Dictionary<ulong, MetaRequirementDto> _requirements = new Dictionary<ulong, MetaRequirementDto>();

        // ============================================================
        // Events (UI подписывается)
        // ============================================================

        /// <summary>Дёргается при обновлении реестра требований (на данный момент —
        /// при Push от сервера на OnClientConnected).</summary>
        public event Action OnRequirementsUpdated;

        /// <summary>Дёргается когда сервер отказал в доступе. UI показывает toast.
        /// (netId, reason) — netId для фильтрации stale-ids, reason — human-readable.</summary>
        public event Action<ulong, string> OnAccessDenied;

        /// <summary>Дёргается когда сервер разрешил доступ. Сам Interactable (например,
        /// LockBox) подписывается и запускает визуальную анимацию. (netId, reason="").</summary>
        public event Action<ulong> OnAccessAllowed;

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

        public bool HasRequirement(ulong netId) => _requirements.ContainsKey(netId);

        public string GetDisplayName(ulong netId)
            => _requirements.TryGetValue(netId, out var r) ? r.displayName.ToString() : $"#{netId}";

        public int[] GetItemIds(ulong netId)
            => _requirements.TryGetValue(netId, out var r) ? r.itemIds : null;

        public MetaRequirementDto? GetDto(ulong netId)
            => _requirements.TryGetValue(netId, out var r) ? r : (MetaRequirementDto?)null;

        /// <summary>Запросить у сервера разрешение на использование interactable.
        /// Ответ придёт через NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc.</summary>
        public void RequestCanUse(ulong netId)
        {
            if (MetaRequirementRegistry.Instance == null)
            {
                Debug.LogWarning("[MetaRequirementClientState] RequestCanUse: MetaRequirementRegistry.Instance==null " +
                                 "(server not started?). Доступ ЗАПРЕЩЁН по умолчанию.");
                EmitDeny(netId, "Сервер требований недоступен");
                return;
            }
            MetaRequirementRegistry.Instance.RequestCanUseRpc(netId);
        }

        // ============================================================
        // Server → Client delivery (вызывается из NetworkPlayer.ReceiveMetaRequirement*TargetRpc)
        // ============================================================

        /// <summary>Вызывается из NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc.</summary>
        public void OnCanUseResponse(ulong netId, bool allowed, string reason)
        {
            // Stale-id фильтр: если interactable уже не в реестре (scene transition
            // или unregister) — не дёргаем UI событие
            bool stillTracked = _requirements.ContainsKey(netId);
            if (!allowed)
            {
                string msg = !string.IsNullOrEmpty(reason) ? reason : "Нет доступа";
                EmitDeny(netId, msg);
            }
            else
            {
                try { OnAccessAllowed?.Invoke(netId); }
                catch (Exception ex) { Debug.LogError($"[MetaRequirementClientState] OnAccessAllowed handler threw: {ex}"); }
                Debug.Log($"[MetaRequirementClientState] Use allowed: netId={netId}, reason='{reason}'{(stillTracked ? "" : " [stale, no UI notify]")}");
            }
        }

        /// <summary>Вызывается из NetworkPlayer.ReceiveMetaRequirementBindingsTargetRpc.
        /// MVP: bulk-push всех требований (для UI). В будущем — диффы.</summary>
        public void OnRequirementsPushed(
            ulong[] netIds,
            FixedString64Bytes[] displayNames,
            int[][] itemIdsArr,
            byte[] logics,
            int[] requiredCounts,
            bool[] consumeOnUses)
        {
            _requirements.Clear();
            int n = Mathf.Min(netIds.Length,
                Mathf.Min(displayNames.Length,
                Mathf.Min(itemIdsArr.Length,
                Mathf.Min(logics.Length,
                Mathf.Min(requiredCounts.Length, consumeOnUses.Length)))));
            for (int i = 0; i < n; i++)
            {
                _requirements[netIds[i]] = new MetaRequirementDto
                {
                    interactableNetworkObjectId = netIds[i],
                    displayName = displayNames[i],
                    itemIds = itemIdsArr[i] ?? new int[0],
                    logic = logics[i],
                    requiredCount = requiredCounts[i],
                    consumeOnUse = consumeOnUses[i],
                };
            }
            try { OnRequirementsUpdated?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[MetaRequirementClientState] OnRequirementsUpdated handler threw: {ex}"); }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void EmitDeny(ulong netId, string reason)
        {
            try { OnAccessDenied?.Invoke(netId, reason); }
            catch (Exception ex) { Debug.LogError($"[MetaRequirementClientState] OnAccessDenied handler threw: {ex}"); }
            Debug.LogWarning($"[MetaRequirementClientState] Access denied: netId={netId}, reason='{reason}'");
        }
    }
}
