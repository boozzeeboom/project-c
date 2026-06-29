// Project C: Character Progression — T-P10
// EquipmentClientState: client-side projection. T-P08 stub → ПОЛНАЯ ВЕРСИЯ.
// Design: docs/Character/05_CLOTHING_AND_MODULES.md, docs/Character/08_ROADMAP.md T-P10
//
// Pattern: копия StatsClientState (T-P04) для event-architecture.
// Events для UI (CharacterWindow T-P17):
//   - OnEquipmentUpdated: новый snapshot пришёл
//   - OnEquipResult: ack/deny от TryEquip/TryUnequip (toast)

using System;
using ProjectC.Equipment.Dto;
using ProjectC.Items;
using UnityEngine;

namespace ProjectC.Equipment
{
    /// <summary>
    /// Client-side projection of server equipment state. T-P10 FULL — events + snapshot.
    /// </summary>
    public class EquipmentClientState : MonoBehaviour
    {
        public static EquipmentClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // ============ State ============
        public EquipmentSnapshotDto? CurrentSnapshot { get; private set; }

        // ============ Events для UI ============
        /// <summary>Data event: новый snapshot пришёл. UI вызывает RefreshDisplay.</summary>
        public event Action<EquipmentSnapshotDto> OnEquipmentUpdated;

        /// <summary>Notification event: equip/unequip ack/deny. UI показывает toast.</summary>
        public event Action<EquipResultDto> OnEquipResult;

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

        /// <summary>
        /// Server → client handler. Вызывается из NetworkPlayer.ReceiveEquipmentSnapshotTargetRpc (T-P09).
        /// Обновляет CurrentSnapshot + fire'ит OnEquipmentUpdated.
        /// </summary>
        public void OnEquipmentSnapshotReceived(EquipmentSnapshotDto snapshot)
        {
            CurrentSnapshot = snapshot;
            OnEquipmentUpdated?.Invoke(snapshot);
            if (Debug.isDebugBuild)
            {
                int occupied = 0;
                foreach (var _ in snapshot.equip.EnumerateOccupiedSlots()) occupied++;
                Debug.Log($"[EquipmentClientState] OnEquipmentSnapshotReceived: {occupied} slots occupied");
            }
        }

        /// <summary>
        /// Server → client handler. Вызывается из NetworkPlayer.ReceiveEquipResultTargetRpc (T-P09).
        /// Fire'ит OnEquipResult для UI toast.
        /// </summary>
        public void OnEquipResultReceived(EquipResultDto result)
        {
            OnEquipResult?.Invoke(result);
            if (Debug.isDebugBuild)
            {
                string msg = result.code switch
                {
                    EquipResultCode.Equipped   => $"✅ Надето: itemId={result.itemId} slot={result.slot}",
                    EquipResultCode.Unequipped => $"✅ Снято: slot={result.slot}",
                    EquipResultCode.Denied     => $"❌ {result.reason}",
                    _ => $"? unknown code={result.code}",
                };
                Debug.Log($"[EquipmentClientState] OnEquipResultReceived: {msg}");
            }
        }

        /// <summary>Convenience для UI: clear state (scene reload без DontDestroyOnLoad).</summary>
        public void ClearState()
        {
            CurrentSnapshot = null;
        }

        /// <summary>
        /// T-INP-09: вернуть WeaponItemData из указанного слота (WeaponMain/WeaponOff).
        /// null если слот пуст, нет snapshot, item не оружие, или InventoryWorld не загружен.
        /// Используется SkillInputService.CheckWeaponMask (T-INP-09) для гейтинга skill cast по типу оружия.
        /// </summary>
        public WeaponItemData GetWeaponInSlot(EquipSlot slot)
        {
            if (CurrentSnapshot == null) return null;
            if (!CurrentSnapshot.Value.equip.TryGetItemId(slot, out int itemId) || itemId <= 0) return null;

            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null) return null;

            var def = inv.GetItemDefinition(itemId);
            return def as WeaponItemData;
        }

        /// <summary>
        /// T-INP-09: первое оружие в WeaponMain ИЛИ WeaponOff (приоритет Main).
        /// null если ни в одном слоте нет оружия. Используется CheckWeaponMask.
        /// </summary>
        public WeaponItemData GetActiveWeapon()
        {
            var w = GetWeaponInSlot(EquipSlot.WeaponMain);
            if (w != null) return w;
            return GetWeaponInSlot(EquipSlot.WeaponOff);
        }
    }
}
