// T-NS03: NpcShipWorld — server-only state machine для всех NPC-кораблей.
// Полная FSM реализация. Stub (T-NS02) заменён этим файлом.
//
// Pattern: DockingWorld (Docking/Core/DockingWorld.cs:19), QuestWorld (Quests/Core/QuestWorld.cs).
// FSM transition diagram: docs/NPC_others_peacfull/pc_ship/04_LIVING_BEHAVIOR.md §2.

using System.Collections.Generic;
using ProjectC.Core.ShipPosition; // T-PERSIST: ShipPositionSaveData
using ProjectC.PeacefulShip.Network;  // NpcShipZoneRegistry (T-NS02)
using ProjectC.PeacefulShip.Stations;  // NpcShipSchedule, NpcShipController
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Server-only state machine для всех NPC-кораблей.
    /// Singleton MonoBehaviour, DontDestroyOnLoad.
    /// Created by NpcShipServer.OnNetworkSpawn (T-NS06).
    /// </summary>
    public class NpcShipWorld : MonoBehaviour
    {
        public static NpcShipWorld Instance { get; private set; }

        // === State ===

        // NpcInstanceId → NpcShipState (server-only SOT)
        private readonly Dictionary<ulong, NpcShipState> _npcByInstanceId = new Dictionary<ulong, NpcShipState>();

        // NpcInstanceId → NpcShipSchedule (SO ссылка для логики контроллера)
        private readonly Dictionary<ulong, NpcShipSchedule> _scheduleByNpcInstanceId = new Dictionary<ulong, NpcShipSchedule>();

        // === Events (server fires, others subscribe) ===
        // Q10 stubs (v2 subscribers: TradeWorld, QuestServer)
#pragma warning disable 0067
        public event System.Action<ulong, string> OnNpcShipArrived;
        public event System.Action<ulong, string> OnNpcShipDeparted;
#pragma warning restore 0067

        // === Lifecycle ===

        /// <summary>
        /// Singleton init. Вызывается из NpcShipServer.OnNetworkSpawn (T-NS06).
        /// </summary>
        public static void CreateAndInitialize()
        {
            if (Instance != null) return;
            var go = new GameObject("[NpcShipWorld]");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<NpcShipWorld>();
            Debug.Log("[NpcShipWorld] Created");
        }

        public static void Shutdown()
        {
            if (Instance != null) Object.Destroy(Instance.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // === Public API (server-side) ===

        public void RegisterNpc(ulong id, ShipController ship, NpcShipSchedule schedule)
                {
                    if (id == 0 || ship == null) return;
                    if (_npcByInstanceId.ContainsKey(id)) return; // idempotent

                    var state = new NpcShipState(id, ship);
                    // M3.2.1: задать первый route при регистрации. Без этого state.CurrentRoute пустой,
                    // и NavTick.ResolveTargetStation() возвращает null → NPC зависает в Lifting без цели.
                    if (schedule != null && schedule.routes != null && schedule.routes.Length > 0)
                    {
                        state.CurrentRoute = schedule.routes[0];
                        state.ScheduleIndex = 0;
                    }
                    _npcByInstanceId[id] = state;
                    _scheduleByNpcInstanceId[id] = schedule;
                    Debug.Log($"[NpcShipWorld] RegisterNpc id={id:X}");
                }

        public void UnregisterNpc(ulong id)
        {
            if (_npcByInstanceId.Remove(id))
            {
                _scheduleByNpcInstanceId.Remove(id);
                Debug.Log($"[NpcShipWorld] UnregisterNpc id={id:X}");
            }
        }

        public NpcShipState GetNpc(ulong id)
            => _npcByInstanceId.TryGetValue(id, out var s) ? s : null;

        public NpcShipSchedule GetSchedule(ulong id)
            => _scheduleByNpcInstanceId.TryGetValue(id, out var s) ? s : null;

        public int AllNpcCount => _npcByInstanceId.Count;

        // === T-PERSIST: RestoreNpcState ===

        /// <summary>T-PERSIST: восстановить FSM-состояние NPC из сохранённых данных.</summary>
        public void RestoreNpcState(ulong npcInstanceId, ShipPositionSaveData data)
        {
            if (!_npcByInstanceId.TryGetValue(npcInstanceId, out var state)) return;
            if (!_scheduleByNpcInstanceId.TryGetValue(npcInstanceId, out var schedule)) return;

            state.ScheduleIndex = data.scheduleIndex;
            state.LastKnownPosition = new Vector3(data.px, data.py, data.pz);
            state.StateEnteredAt = Time.time; // сервер перезапущен — таймер состояния сброшен

            // Восстановить CurrentRoute из schedule по сохранённому индексу
            if (schedule.routes != null && schedule.routes.Length > 0
                && data.scheduleIndex >= 0 && data.scheduleIndex < schedule.routes.Length)
            {
                state.CurrentRoute = schedule.routes[data.scheduleIndex];
            }

            if (Debug.isDebugBuild)
                Debug.Log($"[NpcShipWorld] RestoreNpcState id={npcInstanceId:X} idx={state.ScheduleIndex} " +
                      $"route={state.CurrentRoute.fromLocationId}→{state.CurrentRoute.toLocationId}");
        }

        /// <summary>Read-only iterate all NPCs (for dispatch, debugging).</summary>
        public IEnumerable<NpcShipState> AllNpcs => _npcByInstanceId.Values;

        // === Dispatch ===

        /// <summary>
        /// Per-frame dispatch — NavTick каждого зарегистрированного NPC.
        /// Старая FSM (TickNpc + movement-хелперы) удалена в T-NS-P2 (была недостижима:
        /// FixedUpdate всегда шёл в NavTick напрямую). Логика расписания — в контроллере.
        /// </summary>
        private void FixedUpdate()
                {
                    if (!ProjectC.Trade.Network.NetworkingUtils.IsServerSafe()) return;

                    float dt = Time.fixedDeltaTime;
                    foreach (var state in _npcByInstanceId.Values)
                    {
                        if (state == null || state.Ship == null) continue;
                        var controller = NpcShipZoneRegistry.Get(state.NpcInstanceId);
                        if (controller == null) continue;

                        // M3.2: только NavTick с прямым Rigidbody control.
                        // ShipController.FixedUpdate пропускает физику если _hasNpcPilot && _pilots.Count==0.
                        controller.NavTick(dt);
                    }
                }

        // === T-NS-P2: удалено — TickNpc + movement/transition/schedule helpers (были недостижимы) ===
    }
}