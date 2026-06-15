// Project C: Character Progression — T-P08 (STUB) → полная версия T-P10
// EquipmentClientState: client-side projection. Stub — public surface совпадает с T-P10 версией,
// чтобы T-P09 (EquipmentServer) + NetworkPlayer.ReceiveEquipmentSnapshotTargetRpc компилировались.
//
// В T-P08 stub: методы существуют, но только логируют. Events (OnEquipmentUpdated, OnEquipResult)
// не объявлены — добавятся в T-P10. (T-P09 не использует events, только direct calls.)

using UnityEngine;

namespace ProjectC.Equipment
{
    /// <summary>
    /// Client-side projection of server equipment state. T-P08 STUB — полная версия в T-P10.
    /// </summary>
    public class EquipmentClientState : MonoBehaviour
    {
        public static EquipmentClientState Instance { get; private set; }

        public Equipment.Dto.EquipmentSnapshotDto? CurrentSnapshot { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
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

        /// <summary>
        /// Server → client handler. Вызывается из NetworkPlayer.ReceiveEquipmentSnapshotTargetRpc.
        /// T-P08: только сохраняет snapshot + log. T-P10: добавит events.
        /// </summary>
        public void OnEquipmentSnapshotReceived(Equipment.Dto.EquipmentSnapshotDto snapshot)
        {
            CurrentSnapshot = snapshot;
            if (Debug.isDebugBuild)
            {
                int occupied = 0;
                foreach (var _ in snapshot.equip.EnumerateOccupiedSlots()) occupied++;
                Debug.Log($"[EquipmentClientState] (T-P08 stub) snapshot received: {occupied} occupied slots");
            }
        }

        /// <summary>
        /// T-P08 STUB: server → client equip result (success/denied). T-P10 добавит OnEquipResult event.
        /// </summary>
        public void OnEquipResultReceived(Equipment.Dto.EquipResultDto result)
        {
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[EquipmentClientState] (T-P08 stub) equip result: code={result.code} itemId={result.itemId} slot={result.slot} reason='{result.reason}'");
            }
        }
    }
}
