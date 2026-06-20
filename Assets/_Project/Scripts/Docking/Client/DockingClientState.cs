// T-DOCK-03: DockingClientState — клиентская проекция серверного DockingWorld.
// Singleton MonoBehaviour (DontDestroyOnLoad). Auto-spawn через NetworkManagerController.Awake
// (как MarketClientState, CraftingClientState и т.д. — канон проекта).
// Паттерн: см. Assets/_Project/Quests/Client/QuestClientState.cs.
//
// Q3 (принято 2026-06-19): Сервер — SOT, клиент не хранит занятость, получает push по RPC.
// Q7 (принято 2026-06-19): PendingAssignment state — двусторонняя связь обязательна.
// Q10 (принято 2026-06-19): IsLocalPlayerPilotingShip() helper для T-key check.

using System;
using ProjectC.Docking.Dto;
using ProjectC.Network; // NetworkPlayerSpawner
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.Docking.Client
{
    public class DockingClientState : MonoBehaviour
    {
        public static DockingClientState Instance { get; private set; }

        // === Events для UI (T-DOCK-07 CommPanelWindow подпишется здесь) ===

        /// <summary>Q7: сервер назначил pad, ждёт подтверждения игрока.</summary>
        public event Action<DockingAssignmentDto, bool> OnAwaitingConfirmation;

        /// <summary>Сервер отказал в назначении (no pad, rate limit, station not found, etc).</summary>
        public event Action<DockingAssignmentDto> OnAssignmentFailed;

        /// <summary>Status update: Assigned / Docked / Cancelled / WrongPad.</summary>
        public event Action<DockingStatusDto> OnStatusReceived;

        /// <summary>Сервер подтвердил отстыковку (Docked → Idle).</summary>
        public event Action<ulong> OnTakeoffApproved;

        /// <summary>Пилот коснулся pad'а (Docked или WrongPad).</summary>
        public event Action<DockingStatusDto> OnTouchedDown;

        // === State ===

        /// <summary>Q7: текущее pending назначение (ждёт подтверждения).</summary>
        public DockingAssignmentDto? PendingAssignment { get; private set; }

        /// <summary>Подтверждённое назначение (окно посадки идёт).</summary>
        public DockingAssignmentDto? CurrentAssignment { get; private set; }

        public DockingStatusDto? CurrentStatus { get; private set; }

        /// <summary>Ближайшая станция (для HUD indicator, T-DOCK-05 установит).</summary>
        public string NearestStationId { get; private set; }

        // === Lifecycle ===

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("[DockingClientState]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DockingClientState>();
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this)
            {
                Debug.LogWarning("[DockingClientState] Second instance detected, destroying", this);
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // === Handlers (called by DockingServer via TargetRpc) ===

        public void HandleAssignmentReceived(DockingAssignmentDto assignment)
        {
            if (assignment.success)
            {
                // Q7: pending — ждём подтверждения игрока
                PendingAssignment = assignment;
                OnAwaitingConfirmation?.Invoke(assignment, true);
            }
            else
            {
                // Failure (no pad, rate limit, station not found, etc)
                OnAssignmentFailed?.Invoke(assignment);
            }
        }

        public void HandleStatusReceived(DockingStatusDto status)
        {
            // Q7: если Assigned — клиент подтвердил, переводим pending → current
            if (status.status == DockingStatus.Assigned)
            {
                if (PendingAssignment.HasValue)
                {
                    CurrentAssignment = PendingAssignment;
                    PendingAssignment = null;
                }
            }
            else if (status.status == DockingStatus.Cancelled)
            {
                // Отбой — чистим оба
                PendingAssignment = null;
                CurrentAssignment = null;
            }
            else if (status.status == DockingStatus.Docked
                  || status.status == DockingStatus.WrongPad)
            {
                OnTouchedDown?.Invoke(status);
            }

            CurrentStatus = status;
            OnStatusReceived?.Invoke(status);
        }

        public void HandleTakeoffApproved(ulong shipNetId)
        {
            CurrentAssignment = null;
            CurrentStatus = null;
            OnTakeoffApproved?.Invoke(shipNetId);
        }

        /// <summary>Будет вызываться из DockingZoneRegistry.LocalPlayerStation (T-DOCK-05).</summary>
        public void SetNearestStation(string stationId)
        {
            NearestStationId = stationId;
        }

        // === Q10: helper для T-key check ===

        /// <summary>
        /// Проверяет, что локальный игрок в корабле (грубая проверка для MVP).
        /// Phase 2: точная проверка через PilotSeatController.IsPilotOccupant.
        /// </summary>
        public static bool IsLocalPlayerPilotingShip()
        {
            var np = GetLocalPlayer();
            if (np == null) return false;
            if (!np.IsInShip) return false;
            return np.CurrentShip != null;
        }

        private static NetworkPlayer GetLocalPlayer()
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].IsOwner) continue;
                // Skip scene-placed ghost (footgun per project convention).
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                return players[i];
            }
            return null;
        }
    }
}
