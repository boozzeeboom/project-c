// T-NS02 stub: NpcShipWorld — минимальный placeholder.
// Полная реализация (FSM tick, RegisterNpc logic, events) в T-NS03.
// Создан сейчас чтобы NpcShipController мог компилироваться без forward-reference error.

using System.Collections.Generic;
using ProjectC.PeacefulShip.Stations;
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Server-only state machine для всех NPC-кораблей.
    /// STUB в T-NS02 — полная реализация в T-NS03.
    /// Pattern: DockingWorld (Docking/Core/DockingWorld.cs:19).
    /// </summary>
    public class NpcShipWorld : MonoBehaviour
    {
        public static NpcShipWorld Instance { get; private set; }

        private readonly Dictionary<ulong, NpcShipState> _npcByInstanceId = new Dictionary<ulong, NpcShipState>();

        // Stub events — в T-NS03 будут реальные
#pragma warning disable 0067  // event never used (stub, real impl T-NS03)
        public event System.Action<ulong, string> OnNpcShipArrived;
        public event System.Action<ulong, string> OnNpcShipDeparted;
#pragma warning restore 0067

        /// <summary>Stub: только регистрирует NPC. FSM tick в T-NS03.</summary>
        public void RegisterNpc(ulong id, ShipController ship, NpcShipSchedule schedule)
        {
            if (id == 0 || ship == null) return;
            if (_npcByInstanceId.ContainsKey(id)) return;
            _npcByInstanceId[id] = new NpcShipState(id, ship);
            Debug.Log($"[NpcShipWorld:STUB] RegisterNpc id={id} (real impl in T-NS03)");
        }

        /// <summary>Stub: удаляет NPC из реестра.</summary>
        public void UnregisterNpc(ulong id)
        {
            if (_npcByInstanceId.Remove(id))
            {
                Debug.Log($"[NpcShipWorld:STUB] UnregisterNpc id={id}");
            }
        }

        /// <summary>Stub: returns null (real impl in T-NS03).</summary>
        public NpcShipState GetNpc(ulong id)
            => _npcByInstanceId.TryGetValue(id, out var s) ? s : null;

        /// <summary>Stub: returns 0 (real impl in T-NS03).</summary>
        public int AllNpcCount => _npcByInstanceId.Count;

        /// <summary>CreateAndInitialize — full impl в T-NS03.</summary>
        public static void CreateAndInitialize()
        {
            if (Instance != null) return;
            var go = new GameObject("[NpcShipWorld]");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<NpcShipWorld>();
            Debug.Log("[NpcShipWorld:STUB] Created — full FSM in T-NS03");
        }

        /// <summary>Shutdown — full impl в T-NS03.</summary>
        public static void Shutdown()
        {
            if (Instance != null) Object.Destroy(Instance.gameObject);
        }
    }
}