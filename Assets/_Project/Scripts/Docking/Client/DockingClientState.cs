// T-DOCK-01 stub: клиентская проекция серверного DockingWorld.
// Полная реализация в T-DOCK-03 (с OnAwaitingConfirmation, PendingAssignment и т.д.)

using ProjectC.Docking.Dto;
using UnityEngine;

namespace ProjectC.Docking.Client
{
    public class DockingClientState : MonoBehaviour
    {
        public static DockingClientState Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("[DockingClientState]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DockingClientState>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Stub handlers — T-DOCK-03 расширит (события + state).
        public void HandleAssignmentReceived(DockingAssignmentDto assignment)
        {
            Debug.Log($"[DockingClientState] HandleAssignmentReceived success={assignment.success} pad={assignment.padId}");
        }

        public void HandleStatusReceived(DockingStatusDto status)
        {
            Debug.Log($"[DockingClientState] HandleStatusReceived status={status.status}");
        }

        public void HandleTakeoffApproved(ulong shipNetId)
        {
            Debug.Log($"[DockingClientState] HandleTakeoffApproved ship={shipNetId}");
        }
    }
}
